// Keyboard focus as the JS relies on it: the stage.js Tab-focus rule (stage.js:161-174, departure.js:769-777,
// EXTENSIONS X-8) on top of the browser's sequential focus navigation, which uGUI does not do for us (navigation
// events are off).
//
// - Only Tab moves the focus (the browser's default action of an unswallowed Tab: KeyStack calls MoveTab). It cycles
//   through the focusable buttons of the top scope (a confirm card, a plate, the departure screen) in the order they
//   were added (DOM order), skipping disabled and hidden ones, wrapping at the ends; Shift+Tab goes backwards.
// - Any pointer press clears it (stage.js: `pointerdown` → tabFocus = null), and the focus ring goes away.
// - So Current is always what stage.js calls tabFocus: a button Tab moved to. An unswallowed Enter presses it, an
//   unswallowed Space presses it on release (KeyStack), and screens ask IsTabFocused / InScope for their own rules
//   (the confirm card: a focused 「やめる」 never confirms).
// - A Focusable draws its own ring (CSS :focus-visible outline, e.g. 2px --wick offset 2) through its Ring callback.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>A button that Tab can reach.</summary>
    public sealed class Focusable
    {
        /// <summary>The element (hidden / destroyed = not focusable).</summary>
        public RectTransform Target;

        /// <summary>b.click()</summary>
        public Action Press;

        /// <summary>Shows (true) or hides (false) the focus ring.</summary>
        public Action<bool> Ring;

        /// <summary>Extra condition (enabled, interactable…); null = always.</summary>
        public Func<bool> CanFocus;

        /// <summary>[disabled]: skipped by Tab, loses the focus.</summary>
        public bool Disabled;

        /// <summary>The scope it was added to.</summary>
        public FocusScope Scope { get; internal set; }

        public bool IsFocusable
        {
            get
            {
                if (Disabled || Target == null || !Target) return false;
                if (!Target.gameObject.activeInHierarchy) return false;
                try { return CanFocus == null || CanFocus(); }
                catch (Exception e) { Debug.LogException(e); return false; }
            }
        }
    }

    /// <summary>A group of focusables Tab cycles through while it is the top scope. Dispose pops it.</summary>
    public sealed class FocusScope : IDisposable
    {
        private readonly List<Focusable> _items = new List<Focusable>();

        public string Name { get; }
        public IReadOnlyList<Focusable> Items => _items;

        internal FocusScope(string name) { Name = name; }

        /// <summary>Adds a button (after the ones already added: DOM order).</summary>
        public Focusable Add(RectTransform target, Action press, Action<bool> ring = null, Func<bool> canFocus = null)
        {
            var f = new Focusable { Target = target, Press = press, Ring = ring, CanFocus = canFocus };
            Add(f);
            return f;
        }

        public void Add(Focusable f)
        {
            if (f == null || _items.Contains(f)) return;
            f.Scope = this;
            _items.Add(f);
        }

        /// <summary>Adds a button before another (an element inserted earlier in the DOM).</summary>
        public void Insert(int index, Focusable f)
        {
            if (f == null || _items.Contains(f)) return;
            f.Scope = this;
            _items.Insert(Math.Max(0, Math.Min(index, _items.Count)), f);
        }

        public void Remove(Focusable f)
        {
            if (f == null) return;
            if (Focus.Current == f) Focus.Blur();
            _items.Remove(f);
        }

        public void Clear()
        {
            if (Focus.Current != null && _items.Contains(Focus.Current)) Focus.Blur();
            _items.Clear();
        }

        public bool Contains(Focusable f) => f != null && _items.Contains(f);

        public void Dispose() => Focus.PopScope(this);
    }

    public static class Focus
    {
        private static readonly List<FocusScope> Scopes = new List<FocusScope>();
        private static Focusable _current;

        /// <summary>The last key was Tab (stage.js lastKeyTab).</summary>
        public static bool LastKeyTab { get; private set; }

        /// <summary>Raised when the focused button changes.</summary>
        public static event Action Changed;

        /// <summary>A new top scope: Tab cycles its buttons until it is disposed.</summary>
        public static FocusScope PushScope(string name = null)
        {
            var s = new FocusScope(name);
            Scopes.Add(s);
            return s;
        }

        /// <summary>Removes a scope (its focused button, if any, loses the focus).</summary>
        public static void PopScope(FocusScope s)
        {
            if (s == null) return;
            if (_current != null && s.Contains(_current)) Blur();
            Scopes.Remove(s);
        }

        /// <summary>The top scope, or null.</summary>
        public static FocusScope Top => Scopes.Count > 0 ? Scopes[Scopes.Count - 1] : null;

        /// <summary>The focused button (only Tab focuses; null after a pointer press or when it stopped being focusable).</summary>
        public static Focusable Current
        {
            get
            {
                if (_current != null && !_current.IsFocusable) Blur();
                return _current;
            }
        }

        /// <summary>stage.js tabFocus.</summary>
        public static Focusable TabFocused => Current;

        public static bool IsTabFocused(Focusable f) => f != null && Current == f;

        /// <summary>The focused button when it belongs to `scope` (explore's focusedButton(scope), departure's keyboardFocus()).</summary>
        public static Focusable InScope(FocusScope scope)
        {
            Focusable f = Current;
            return f != null && scope != null && scope.Contains(f) ? f : null;
        }

        /// <summary>Focuses f (as Tab does); null blurs.</summary>
        public static void Set(Focusable f)
        {
            if (f != null && !f.IsFocusable) f = null;
            if (f == _current) return;
            Focusable was = _current;
            _current = f;
            if (was != null) SafeRing(was, false);
            if (f != null) SafeRing(f, true);
            try { Changed?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>The focused element loses the focus (document.activeElement.blur()).</summary>
        public static void Blur() => Set(null);

        private static void SafeRing(Focusable f, bool on)
        {
            try { f.Ring?.Invoke(on); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>The browser's Tab: the next (Shift: previous) focusable of the top scope, wrapping. False when there is none.</summary>
        public static bool MoveTab(bool backwards)
        {
            FocusScope s = Top;
            if (s == null) return false;
            IReadOnlyList<Focusable> items = s.Items;
            int n = items.Count;
            if (n == 0) return false;
            Focusable cur = Current;
            int start = cur != null ? IndexOf(items, cur) : -1;
            for (int step = 1; step <= n; step++)
            {
                int i;
                if (start < 0) i = backwards ? n - step : step - 1;
                else i = ((start + (backwards ? -step : step)) % n + n) % n;
                if (items[i].IsFocusable)
                {
                    Set(items[i]);
                    return true;
                }
            }
            return false;
        }

        private static int IndexOf(IReadOnlyList<Focusable> items, Focusable f)
        {
            for (int i = 0; i < items.Count; i++) if (items[i] == f) return i;
            return -1;
        }

        /// <summary>b.click() on a focusable (when it still is one); the microtasks run after it.</summary>
        public static bool Press(Focusable f)
        {
            if (f == null || !f.IsFocusable) return false;
            try { f.Press?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            Microtasks.Drain();
            return true;
        }

        /// <summary>Presses the Tab-focused button; false when there is none.</summary>
        public static bool PressTabFocused() => Press(Current);

        // js: scenes/common/stage.js:171
        internal static void OnKeyDownCapture(KeyEvent e) => LastKeyTab = e.Key == "Tab";

        // js: scenes/common/stage.js:173
        internal static void OnPointerDownCapture()
        {
            LastKeyTab = false;
            Blur();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Scopes.Clear();
            _current = null;
            LastKeyTab = false;
            Changed = null;
        }
    }
}
