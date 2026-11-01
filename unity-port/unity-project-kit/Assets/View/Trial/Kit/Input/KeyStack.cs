// The keyboard of every scene: a port of stage.js pushKeys (the key stack) plus what the browser gave the JS for free
// (kit.md §4.10, app.md §4).
//
// - The stack: Push(handler) → pop. The top handler (last pushed) gets a keydown first; true swallows it
//   (preventDefault), false passes it down. stage.js:136.
// - Keys carry JS KeyboardEvent.key names ("Escape", "Enter", "Tab", " ", "ArrowUp", "PageDown", "j" / "J", "1",
//   "+", "Shift", "F5"), so every handler ports with the same comparisons. Printable keys follow the layout:
//   US positional by default, the JIS table when the keyboard is Japanese (Shift+; = "+", Shift+- = "=", ^ @ [ ] :).
// - Auto-repeat is synthesized (Input System gives no OS repeat for keys): the most recently pressed non-modifier key
//   repeats while held, first after 500 ms, then every 33 ms, with Repeat = true. Releasing it stops it; pressing
//   another key moves the repeat there (a modifier, which does not repeat itself, so stops it, as Windows does).
// - Every window listener (capture, keyup, blur, pointerdown) runs on its own with a microtask drain after it.
// - KeyUp is not stacked (the battle listens on window); KeyDownCapture / PointerDownCapture are the capture-phase
//   window listeners (the Tab-focus rule).
// - Blur (focus lost): Blurred is raised, repeats stop and the held keys are forgotten without synthetic keyups.
// - While a TMP_InputField has the focus (the journal memo), only Escape reaches the stack (the textarea keeps the
//   rest; the capture listeners still see every key), and IME is enabled for the field (off otherwise).
// - Browser default actions of an unswallowed key: Tab moves the focus (Focus.MoveTab), Enter presses the focused
//   button, Space presses it on release.
// - The microtask queue is drained after every listener, as the browser does.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Trial.Kit
{
    /// <summary>A keydown / keyup as the JS handlers read it (the KeyboardEvent fields they use).</summary>
    public sealed class KeyEvent
    {
        /// <summary>e.key</summary>
        public string Key;
        /// <summary>The physical key (Input System, positional).</summary>
        public Key InputKey;
        /// <summary>e.repeat</summary>
        public bool Repeat;
        public bool ShiftKey;
        public bool CtrlKey;
        public bool AltKey;
        public bool MetaKey;

        /// <summary>A handler swallowed it (e.preventDefault()).</summary>
        public bool DefaultPrevented { get; private set; }

        public void PreventDefault() => DefaultPrevented = true;

        public KeyEvent() { }

        public KeyEvent(string key, bool repeat = false, bool shift = false, bool ctrl = false, bool alt = false, bool meta = false)
        {
            Key = key;
            Repeat = repeat;
            ShiftKey = shift;
            CtrlKey = ctrl;
            AltKey = alt;
            MetaKey = meta;
        }

        public override string ToString() => (Repeat ? "repeat " : "") + (CtrlKey ? "Ctrl+" : "") + (AltKey ? "Alt+" : "") + (ShiftKey ? "Shift+" : "") + "'" + Key + "'";
    }

    public static class KeyStack
    {
        /// <summary>The first repeat after a press (Windows default delay).</summary>
        public const float RepeatDelayMs = 500f;

        /// <summary>The repeat interval (Windows default rate, about 30 per second).</summary>
        public const float RepeatIntervalMs = 33f;

        private static readonly List<Func<KeyEvent, bool>> Stack = new List<Func<KeyEvent, bool>>();

        /// <summary>window keydown, capture phase: runs before the stack (stage.js watchTabFocus, explore's onKeyCapture).</summary>
        public static event Action<KeyEvent> KeyDownCapture;

        /// <summary>window keyup (not stacked).</summary>
        public static event Action<KeyEvent> KeyUp;

        /// <summary>window blur / visibilitychange hidden: no keyup follows for the keys held then.</summary>
        public static event Action Blurred;

        /// <summary>window pointerdown, capture phase (any button), before the EventSystem's handlers of that frame run.</summary>
        public static event Action PointerDownCapture;

        private static int _pumpedFrame = -1;
        private static int _blurFrame = -1;
        private static Key _repeatKey = Key.None;
        private static double _nextRepeatAt;
        private static readonly HashSet<Key> BlurHeld = new HashSet<Key>();
        private static bool _textField;
        private static bool _imeKnown;
        private static bool _imeOn;
        private static Focusable _spaceArmed;
        private static int _layoutDevice = -1;
        private static string _layoutName;
        private static bool _jis;

        /// <summary>How many handlers are pushed.</summary>
        public static int Depth => Stack.Count;

        // js: scenes/meta/util.js:117
        /// <summary>modalSwallow(e): what a modal overlay keeps from the screen under it — characters, arrows, Enter, Tab, PageUp / PageDown (not F5 / F12).</summary>
        public static bool ModalSwallow(KeyEvent e) =>
            e.Key.Length == 1 || e.Key.StartsWith("Arrow", StringComparison.Ordinal) || e.Key == "Enter" || e.Key == "Tab" || e.Key == "PageUp" || e.Key == "PageDown";

        // js: scenes/common/stage.js:136
        /// <summary>pushKeys(handler): the handler gets keys before every handler pushed earlier; returns the pop function.</summary>
        public static Action Push(Func<KeyEvent, bool> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Stack.Add(handler);
            return () =>
            {
                int i = Stack.LastIndexOf(handler);
                if (i >= 0) Stack.RemoveAt(i);
            };
        }

        /// <summary>
        /// One keydown through the window: capture listeners, then the stack top first (stage.js:139-141), then the
        /// browser's default action when nobody swallowed it. Returns whether a handler swallowed it.
        /// </summary>
        public static bool Dispatch(KeyEvent e)
        {
            if (e == null) return false;
            Capture(e);

            // js: scenes/common/stage.js:140
            var handlers = Stack.ToArray();
            for (int i = handlers.Length - 1; i >= 0; i--)
            {
                // A handler popped by an earlier one during this event is skipped (the JS loop indexes the live array).
                if (!Stack.Contains(handlers[i])) continue;
                bool swallowed;
                try { swallowed = handlers[i](e); }
                catch (Exception ex) { Debug.LogException(ex); swallowed = false; }
                if (swallowed) { e.PreventDefault(); break; }
            }
            Microtasks.Drain();

            if (!e.DefaultPrevented) DefaultAction(e);
            return e.DefaultPrevented;
        }

        // The window's capture-phase keydown listeners (they run before any element's own listener, the memo
        // textarea's included), then stage.js's own (watchTabFocus, stage.js:171).
        private static void Capture(KeyEvent e)
        {
            Raise(KeyDownCapture, e);
            Focus.OnKeyDownCapture(e);
        }

        // Every listener on its own, as the browser calls them: a throwing one does not stop the others, and the
        // microtask queue drains after each.
        private static void Raise<T>(Action<T> listeners, T arg)
        {
            if (listeners == null) return;
            foreach (Delegate d in listeners.GetInvocationList())
            {
                try { ((Action<T>)d)(arg); }
                catch (Exception ex) { Debug.LogException(ex); }
                Microtasks.Drain();
            }
        }

        private static void Raise(Action listeners)
        {
            if (listeners == null) return;
            foreach (Delegate d in listeners.GetInvocationList())
            {
                try { ((Action)d)(); }
                catch (Exception ex) { Debug.LogException(ex); }
                Microtasks.Drain();
            }
        }

        /// <summary>One keyup through the window.</summary>
        public static void DispatchUp(KeyEvent e)
        {
            if (e == null) return;
            Raise(KeyUp, e);
            if (e.Key == " " && _spaceArmed != null)
            {
                Focusable armed = _spaceArmed;
                _spaceArmed = null;
                if (Focus.Current == armed) Focus.Press(armed);
                Microtasks.Drain();
            }
        }

        // The browser's default actions on a focused button (and sequential focus navigation).
        private static void DefaultAction(KeyEvent e)
        {
            if (e.Key == "Tab")
            {
                if (!e.CtrlKey && !e.AltKey && !e.MetaKey) Focus.MoveTab(e.ShiftKey);
            }
            else if (e.Key == "Enter")
            {
                Focusable f = Focus.Current;
                if (f != null) Focus.Press(f);
            }
            else if (e.Key == " ")
            {
                if (!e.Repeat) _spaceArmed = Focus.Current;
            }
            Microtasks.Drain();
        }

        /// <summary>Focus lost (OnApplicationFocus(false) / OnApplicationPause(true)): Blurred, repeats stop, no synthetic keyups.</summary>
        public static void Blur()
        {
            // The boot and the stage both report a focus loss (app.md §5.2): the browser fires blur once.
            int frame = Time.frameCount;
            if (frame == _blurFrame) return;
            _blurFrame = frame;
            _repeatKey = Key.None;
            _spaceArmed = null;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                foreach (KeyControl k in kb.allKeys)
                    if (k != null && k.isPressed) BlurHeld.Add(k.keyCode);
            }
            Raise(Blurred);
        }

        /// <summary>True while a TMP_InputField has the keyboard (the stack then sees only Escape).</summary>
        public static bool TextFieldFocused
        {
            get
            {
                EventSystem es = EventSystem.current;
                if (!es) return false;
                GameObject sel = es.currentSelectedGameObject;
                if (!sel) return false;
                var field = sel.GetComponent<TMP_InputField>();
                return field && field.isFocused;
            }
        }

        /// <summary>Whether the key is held now (and was not held through a blur).</summary>
        public static bool IsHeld(Key key)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || key == Key.None) return false;
            return kb[key].isPressed && !BlurHeld.Contains(key);
        }

        // ---- the frame -----------------------------------------------------------------------------------

        /// <summary>Reads the keyboard and the pointer buttons once per frame and dispatches (a second call in the same frame does nothing).</summary>
        public static void Pump()
        {
            int frame = Time.frameCount;
            if (frame == _pumpedFrame) return;
            _pumpedFrame = frame;

            PumpPointer();

            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            UpdateLayout(kb);

            bool textField = TextFieldFocused;
            if (textField != _textField || !_imeKnown)
            {
                _textField = textField;
                SetIme(kb, textField);
            }

            double now = Time.realtimeSinceStartupAsDouble * 1000.0;
            var presses = new List<Key>();
            var releases = new List<Key>();
            foreach (KeyControl k in kb.allKeys)
            {
                if (k == null) continue;
                Key code = k.keyCode;
                if (code == Key.None) continue;
                if (k.wasPressedThisFrame) presses.Add(code);
                if (k.wasReleasedThisFrame) releases.Add(code);
            }
            // The true order inside a frame is lost; a chord is pressed modifier first, so modifiers go first.
            OrderPresses(presses);

            // The OS repeats the key pressed last (Windows typematic). A modifier does not repeat here (app.md §4.8),
            // but pressing one moves the repeat off the key held before it, so that key stops repeating; a modifier
            // pressed in the same frame as another key is taken as pressed before it.
            Key newRepeat = Key.None;
            bool modifierPressed = false;
            foreach (Key code in presses)
            {
                if (IsModifier(code)) modifierPressed = true;
                else newRepeat = code;
            }
            if (newRepeat != Key.None)
            {
                _repeatKey = newRepeat;
                _nextRepeatAt = now + RepeatDelayMs;
            }
            else if (modifierPressed) _repeatKey = Key.None;

            foreach (Key code in presses)
            {
                BlurHeld.Remove(code);
                Down(kb, code, repeat: false);
            }

            if (_repeatKey != Key.None && !presses.Contains(_repeatKey))
            {
                if (!kb[_repeatKey].isPressed || BlurHeld.Contains(_repeatKey)) _repeatKey = Key.None;
                else
                {
                    int n = 0;
                    while (now >= _nextRepeatAt && n < 4 && _repeatKey != Key.None)
                    {
                        _nextRepeatAt += RepeatIntervalMs;
                        n++;
                        Down(kb, _repeatKey, repeat: true);
                    }
                    // A long frame does not queue a burst of repeats.
                    if (now - _nextRepeatAt > 4 * RepeatIntervalMs) _nextRepeatAt = now + RepeatIntervalMs;
                }
            }

            foreach (Key code in releases)
            {
                if (code == _repeatKey) _repeatKey = Key.None;
                if (BlurHeld.Remove(code)) continue;
                if (_textField) continue;
                DispatchUp(Make(kb, code, false));
            }
        }

        /// <summary>Puts the modifier presses of one frame before the others, keeping each group's order (stable).</summary>
        public static void OrderPresses(List<Key> presses)
        {
            if (presses == null || presses.Count < 2) return;
            var mods = new List<Key>();
            var rest = new List<Key>();
            foreach (Key k in presses) (IsModifier(k) ? mods : rest).Add(k);
            if (mods.Count == 0 || rest.Count == 0) return;
            presses.Clear();
            presses.AddRange(mods);
            presses.AddRange(rest);
        }

        private static void Down(Keyboard kb, Key code, bool repeat)
        {
            KeyEvent e = Make(kb, code, repeat);
            // The memo textarea keeps every key but Escape (journal.js:260 stops its propagation): the window's
            // capture listeners still see the key (they run before the textarea's), the stack and the defaults do not.
            if (_textField && e.Key != "Escape") { Capture(e); return; }
            Dispatch(e);
        }

        private static KeyEvent Make(Keyboard kb, Key code, bool repeat)
        {
            bool shift = kb.shiftKey.isPressed, ctrl = kb.ctrlKey.isPressed, alt = kb.altKey.isPressed;
            bool meta = kb.leftMetaKey.isPressed || kb.rightMetaKey.isPressed;
            return new KeyEvent(KeyName(code, shift, _jis), repeat, shift, ctrl, alt, meta) { InputKey = code };
        }

        private static void PumpPointer()
        {
            bool down = false;
            Mouse m = Mouse.current;
            if (m != null)
                down = m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame || m.middleButton.wasPressedThisFrame
                       || (m.backButton != null && m.backButton.wasPressedThisFrame) || (m.forwardButton != null && m.forwardButton.wasPressedThisFrame);
            if (!down)
            {
                Pointer p = Pointer.current;
                if (p != null && p != m && p.press.wasPressedThisFrame) down = true;
            }
            if (!down) return;
            _spaceArmed = null;
            Focus.OnPointerDownCapture();
            Microtasks.Drain();
            Raise(PointerDownCapture);
        }

        private static void SetIme(Keyboard kb, bool on)
        {
            if (_imeKnown && _imeOn == on) return;
            try { kb.SetIMEEnabled(on); }
            catch (Exception ex) { Debug.LogException(ex); }
            _imeOn = on;
            _imeKnown = true;
        }

        private static void UpdateLayout(Keyboard kb)
        {
            string layout = kb.keyboardLayout;
            if (kb.deviceId == _layoutDevice && string.Equals(layout, _layoutName, StringComparison.Ordinal)) return;
            _layoutDevice = kb.deviceId;
            _layoutName = layout;
            _jis = IsJisLayout(layout, SafeDisplayName(kb, Key.Equals));
        }

        private static string SafeDisplayName(Keyboard kb, Key key)
        {
            try { return kb[key].displayName; }
            catch { return null; }
        }

        /// <summary>Whether the keyboard layout is the Japanese (JIS) one: by its name, or by the key right of 0 printing ^.</summary>
        public static bool IsJisLayout(string layoutName, string equalsKeyDisplayName)
        {
            if (!string.IsNullOrEmpty(layoutName))
            {
                string l = layoutName.ToLowerInvariant();
                if (l.Contains("japan") || l.Contains("0411") || l.Contains("jis") || l.Contains("日本")) return true;
            }
            return equalsKeyDisplayName == "^";
        }

        /// <summary>The JIS layout (detected from the keyboard; settable for tests and odd setups).</summary>
        public static bool Jis
        {
            get => _jis;
            set { _jis = value; _layoutDevice = int.MinValue; }
        }

        // ---- key names (JS e.key) ------------------------------------------------------------------------

        public static bool IsModifier(Key k) =>
            k == Key.LeftShift || k == Key.RightShift || k == Key.LeftCtrl || k == Key.RightCtrl ||
            k == Key.LeftAlt || k == Key.RightAlt || k == Key.LeftMeta || k == Key.RightMeta ||
            k == Key.CapsLock || k == Key.NumLock || k == Key.ScrollLock;

        /// <summary>The KeyboardEvent.key a physical key gives with Shift held or not, on the US or the JIS layout.</summary>
        public static string KeyName(Key k, bool shift, bool jis = false)
        {
            switch (k)
            {
                case Key.Space: return " ";
                case Key.Enter:
                case Key.NumpadEnter: return "Enter";
                case Key.Tab: return "Tab";
                case Key.Escape: return "Escape";
                case Key.Backspace: return "Backspace";
                case Key.Delete: return "Delete";
                case Key.Insert: return "Insert";
                case Key.Home: return "Home";
                case Key.End: return "End";
                case Key.PageUp: return "PageUp";
                case Key.PageDown: return "PageDown";
                case Key.UpArrow: return "ArrowUp";
                case Key.DownArrow: return "ArrowDown";
                case Key.LeftArrow: return "ArrowLeft";
                case Key.RightArrow: return "ArrowRight";
                case Key.LeftShift:
                case Key.RightShift: return "Shift";
                case Key.LeftCtrl:
                case Key.RightCtrl: return "Control";
                case Key.LeftAlt:
                case Key.RightAlt: return "Alt";
                case Key.LeftMeta:
                case Key.RightMeta: return "Meta";
                case Key.ContextMenu: return "ContextMenu";
                case Key.CapsLock: return "CapsLock";
                case Key.NumLock: return "NumLock";
                case Key.ScrollLock: return "ScrollLock";
                case Key.PrintScreen: return "PrintScreen";
                case Key.Pause: return "Pause";
                case Key.NumpadDivide: return "/";
                case Key.NumpadMultiply: return "*";
                case Key.NumpadPlus: return "+";
                case Key.NumpadMinus: return "-";
                case Key.NumpadPeriod: return ".";
                case Key.NumpadEquals: return "=";
            }
            if (k >= Key.Numpad0 && k <= Key.Numpad9) return ((char)('0' + (k - Key.Numpad0))).ToString();
            if (k >= Key.F1 && k <= Key.F12) return "F" + (1 + (k - Key.F1));
            if (k >= Key.F13 && k <= Key.F24) return "F" + (13 + (k - Key.F13));
            if (k >= Key.A && k <= Key.Z)
            {
                char c = (char)('a' + (k - Key.A));
                return (shift ? char.ToUpperInvariant(c) : c).ToString();
            }
            if (k >= Key.Digit1 && k <= Key.Digit9 || k == Key.Digit0)
            {
                int d = k == Key.Digit0 ? 0 : 1 + (k - Key.Digit1);
                if (!shift) return ((char)('0' + d)).ToString();
                return jis ? JisShiftDigit[d] : UsShiftDigit[d];
            }
            if (jis)
            {
                switch (k)
                {
                    case Key.Minus: return shift ? "=" : "-";
                    case Key.Equals: return shift ? "~" : "^";
                    case Key.LeftBracket: return shift ? "`" : "@";
                    case Key.RightBracket: return shift ? "{" : "[";
                    case Key.Backslash: return shift ? "}" : "]";
                    case Key.Semicolon: return shift ? "+" : ";";
                    case Key.Quote: return shift ? "*" : ":";
                    case Key.Comma: return shift ? "<" : ",";
                    case Key.Period: return shift ? ">" : ".";
                    case Key.Slash: return shift ? "?" : "/";
                    case Key.Backquote: return "Zenkaku";
                }
            }
            else
            {
                switch (k)
                {
                    case Key.Minus: return shift ? "_" : "-";
                    case Key.Equals: return shift ? "+" : "=";
                    case Key.LeftBracket: return shift ? "{" : "[";
                    case Key.RightBracket: return shift ? "}" : "]";
                    case Key.Backslash: return shift ? "|" : "\\";
                    case Key.Semicolon: return shift ? ":" : ";";
                    case Key.Quote: return shift ? "\"" : "'";
                    case Key.Comma: return shift ? "<" : ",";
                    case Key.Period: return shift ? ">" : ".";
                    case Key.Slash: return shift ? "?" : "/";
                    case Key.Backquote: return shift ? "~" : "`";
                }
            }
            return "Unidentified";
        }

        private static readonly string[] UsShiftDigit = { ")", "!", "@", "#", "$", "%", "^", "&", "*", "(" };
        // JIS: Shift+0 prints nothing; the browser still reports "0".
        private static readonly string[] JisShiftDigit = { "0", "!", "\"", "#", "$", "%", "&", "'", "(", ")" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Stack.Clear();
            KeyDownCapture = null;
            KeyUp = null;
            Blurred = null;
            PointerDownCapture = null;
            _pumpedFrame = -1;
            _blurFrame = -1;
            _repeatKey = Key.None;
            _nextRepeatAt = 0;
            BlurHeld.Clear();
            _textField = false;
            _imeKnown = false;
            _imeOn = false;
            _spaceArmed = null;
            _layoutDevice = -1;
            _layoutName = null;
            _jis = false;
        }
    }
}
