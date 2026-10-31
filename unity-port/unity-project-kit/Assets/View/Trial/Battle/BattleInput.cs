// The battle scene's input (scenes/battle/BattleScene.js:272-487; docs/BATTLE_SCENE.md §4.5; battle-flow §3): the keys
// (1–9, Enter and its pair, Tab, I, J, X, Esc's chain and its release, Space's one-press and held fast-forward, ← / → for
// 俊敏's choice), the pointer's fast-forward (a press on the arena, held while held, not within L.DoubleClickMs of a click
// that played a card), a click / key moving the result on, and the blur. Keys come through the kit's KeyStack (the JS
// pushKeys and window keyup / blur); the pointer is polled once a frame (Input System) and routed to the hud, the scene and
// the hand in the browser's order, each followed by a microtask drain.
//
// The DOM's e.target becomes the topmost EventSystem raycast hit at the pointer: the layer it lies in, and the classes
// the JS tests through BattleHitTag (.ocbg-battle-hit, .ocbg-battle-endturn, a pointerdown stopped from propagating).
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Depiction;
using Trial.Kit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Trial.Battle
{
    /// <summary>
    /// What the JS reads off an element's classes when it is the pointer's target (or holds it): put it on a battle element's
    /// GameObject (or an ancestor of its raycast targets).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class BattleHitTag : MonoBehaviour
    {
        /// <summary>.ocbg-battle-hit: an interactive piece (a button, a chip, a mark, a card) — a press on it never fast-forwards.</summary>
        public bool Hit = true;

        /// <summary>.ocbg-battle-endturn: the end-turn plate — it counts as the arena while the flow runs (BattleScene.js:424).</summary>
        public bool EndTurn;

        /// <summary>pointerdown stops propagating here (piles.js:175, the pile drawer): the scene and the hand do not see the press.</summary>
        public bool StopPointerDown;

        /// <summary>Tags `c`'s GameObject (added when missing) and returns the tag.</summary>
        public static BattleHitTag Mark(Component c, bool hit = true, bool endTurn = false, bool stopPointerDown = false)
        {
            if (!c) return null;
            BattleHitTag t = c.GetComponent<BattleHitTag>();
            if (!t) t = c.gameObject.AddComponent<BattleHitTag>();
            t.Hit = hit;
            t.EndTurn = endTurn;
            t.StopPointerDown = stopPointerDown;
            return t;
        }
    }

    /// <summary>The JS closures of the keys, the pointer and the fast-forward of mountBattleScene, and the scene's pointer pump.</summary>
    // js: scenes/battle/BattleScene.js:272
    internal sealed class BattleInput
    {
        /// <summary>FF_KEYS.</summary>
        // js: scenes/battle/BattleScene.js:277
        private static readonly HashSet<string> FfKeys = new HashSet<string>(StringComparer.Ordinal) { " " };

        private static readonly Regex Digit = new Regex("^[1-9]$", RegexOptions.CultureInvariant);
        private static readonly Regex Letter = new Regex("^[jJiIxX]$", RegexOptions.CultureInvariant);

        /// <summary>autoSelect: { index, turn, plays, confirm } — the card at the confirmed card's place is selected next.</summary>
        private sealed class AutoSelect
        {
            public int Index;
            public int Turn;
            public int Plays;
            public bool Confirm;
        }

        private readonly BattleSceneHandle _s;
        private readonly HashSet<string> _ffHeld = new HashSet<string>(StringComparer.Ordinal);
        private bool _pointerFF;
        /// <summary>
        /// battle_ui_ux_v2 §3.4 / §3.11 (review fix): a press arms the fast-forward until the screen event playing now has played
        /// out (player.onEventEnd); holding the key or the button keeps it on as before.
        /// </summary>
        private bool _ffArmed;
        /// <summary>Review 19: the count of event ends (player.eventEnds) at which the one-press fast-forward ends.</summary>
        private int _ffUntil;
        /// <summary>When (real ms) the one-press fast-forward was last armed: a press that would run under L.FfMinRunMs carries on.</summary>
        private float _ffArmedAt = float.NegativeInfinity;
        private AutoSelect _autoSelect;
        /// <summary>Esc with nothing left to back out of opens the settings when the key is let go (review fix).</summary>
        private bool _escToSettings;
        /// <summary>Review 16: a press within L.DoubleClickMs of a press or release made while the player was asked does not fast-forward.</summary>
        private float _lastAskedPointer = float.NegativeInfinity;
        private bool _downWhileAsked;

        private Action _offEventEnd;
        private Action _popKeys;
        private Action _offState;
        private bool _keyUpOn;
        private bool _blurOn;
        private bool _pointerOn;

        // ---- the pointer pump's state ----
        private bool _havePos;
        private Vector2 _lastScreen;
        private int _buttons;
        /// <summary>Buttons held through a window blur: their release was lost (no pointerup for them).</summary>
        private int _lostButtons;
        private bool _inside;

        internal BattleInput(BattleSceneHandle scene)
        {
            _s = scene;
        }

        private BattlePlayer Player => _s.PlayerCore;
        private Hand HandView => _s.HandView;
        private Hud HudView => _s.HudView;
        private BattleClock Clock => _s.Env.Clock;
        private BattleRealClock Real => _s.Env.Real;

        // ---- attach / detach (the JS add / removeEventListener) ----------------------------------------------

        internal void Attach()
        {
            // js: scenes/battle/BattleScene.js:312
            _offEventEnd = Player.OnEventEnd(OnEventEnd);
            // js: scenes/battle/BattleScene.js:344
            _popKeys = KeyStack.Push(OnKey);
            // js: scenes/battle/BattleScene.js:414
            KeyStack.KeyUp += OnKeyUp;
            _keyUpOn = true;
            // js: scenes/battle/BattleScene.js:455-458 (pointerdown / pointerup through the pump; blur, visibilitychange)
            _pointerOn = true;
            KeyStack.Blurred += OnBlurred;
            _blurOn = true;
            // js: scenes/battle/BattleScene.js:459
            _offState = Player.OnState(OnState);
        }

        internal void DetachState() { _offState?.Invoke(); _offState = null; }
        internal void DetachEventEnd() { _offEventEnd?.Invoke(); _offEventEnd = null; }
        internal void DetachKeys() { _popKeys?.Invoke(); _popKeys = null; }

        internal void DetachKeyUp()
        {
            if (_keyUpOn) KeyStack.KeyUp -= OnKeyUp;
            _keyUpOn = false;
        }

        internal void DetachPointer() => _pointerOn = false;

        internal void DetachBlur()
        {
            if (_blurOn) KeyStack.Blurred -= OnBlurred;
            _blurOn = false;
        }

        // ---- fast-forward ---------------------------------------------------------------------------------

        // js: scenes/battle/BattleScene.js:296
        internal void UpdateFF()
        {
            Clock.SetFastForward((_ffHeld.Count > 0 || _pointerFF || _ffArmed) && !_s.Overlays.OverlayUp() && Player.CanFastForward());
        }

        // js: scenes/battle/BattleScene.js:299
        private void ArmFF()
        {
            if (!Player.CanFastForward()) return;
            if (!_ffArmed) _ffArmedAt = Real.Now;
            _ffArmed = true;
            _ffUntil = Math.Max(_ffUntil, Player.EventEnds + (Player.AfterWaiting ? 2 : 1));
            UpdateFF();
        }

        // js: scenes/battle/BattleScene.js:306
        private void ClearFF()
        {
            _ffHeld.Clear();
            _pointerFF = false;
            _ffArmed = false;
            UpdateFF();
        }

        // js: scenes/battle/BattleScene.js:312
        private void OnEventEnd()
        {
            if (!_ffArmed || Player.EventEnds < _ffUntil) return;
            // A press in an action's last moments would hurry next to nothing: it runs on to the end of the action after it.
            if (Real.Now - _ffArmedAt < L.FfMinRunMs) { _ffUntil = Player.EventEnds + 1; return; }
            _ffArmed = false;
            UpdateFF();
        }

        // js: scenes/battle/BattleScene.js:326
        private int TurnNow()
        {
            try { return _s.Ctx.Source.Frame.Corner.Turn; }
            catch (Exception) { return -1; }
        }

        // ---- keys -------------------------------------------------------------------------------------------

        /// <summary>
        /// sceneKey(e): the keys the scene acts on (§3.4). A modal overlay takes every one of them, so one of them arriving here
        /// means none is up. ← / → are the scene's only while 俊敏's choice waits.
        /// </summary>
        // js: scenes/battle/BattleScene.js:331
        private bool SceneKey(KeyEvent e)
        {
            if (e.CtrlKey || e.MetaKey || e.AltKey) return false;
            string k = e.Key ?? "";
            if ((k == "ArrowLeft" || k == "ArrowRight") && Player != null && Player.FreeStepPending) return true;
            return k == "Escape" || k == "Enter" || k == "Tab" || FfKeys.Contains(k) || Digit.IsMatch(k) || Letter.IsMatch(k);
        }

        /// <summary>The pushKeys handler (battle_ui_ux_v2 §3.4; docs/BATTLE_SCENE.md §4.5): true swallows the key.</summary>
        // js: scenes/battle/BattleScene.js:344
        private bool OnKey(KeyEvent e)
        {
            if (Player == null || _s.Destroyed) return false;
            if (!SceneKey(e)) return false; // not the scene's: Shift, Ctrl, Alt, F-keys … pass on and prove nothing
            _s.Overlays.SettingsClosed(); // one of the scene's keys reached it: no modal overlay is up
            string k = e.Key;
            if (Player.State == BattlePlayerState.Over)
            {
                // The pile list (finish() closes it; a list opened since) closes before Esc moves past the result.
                if (k == "Escape" && HudView.DrawerOpen()) { HudView.CloseDrawer(); return true; }
                // Only a fresh press moves the result on: a key held to fast-forward the last blow repeats (review fix).
                bool mine = k == "Enter" || k == " " || k == "Escape";
                if (mine && !e.Repeat) Player.SkipResult();
                return mine;
            }
            if (k == "Escape")
            {
                // Review fix: only a fresh press backs out one step; its repeats do nothing.
                if (e.Repeat) return true;
                _escToSettings = false;
                if (HandView.Cancel()) return true;
                // Review 21: a status chip's or a stance's detail opened by a right click (§3.13) closes like the card's.
                if (HudView.ClosePinned()) return true;
                if (HudView.DrawerOpen()) { HudView.CloseDrawer(); return true; }
                if (_s.Overlays.Journal != null) { _s.Overlays.CloseJournal(); return true; }
                // 俊敏's choice (review 21): with ← / → always picking their way, Esc takes the focus back to 留まる.
                if (Player.FreeStepPending && Player.StayFreeStepFocus()) return true;
                _escToSettings = true;
                return true;
            }
            if (k == "j" || k == "J") { if (!e.Repeat) _s.Overlays.ToggleJournal(); return true; }
            if (_s.Overlays.Journal != null) return false; // the journal has the keys while it is open
            // battle_ui_ux_v2 §3.8: while 俊敏's choice waits, ← / → move its focus and Enter chooses (a held arrow repeats).
            if (Player.FreeStepPending && (k == "ArrowLeft" || k == "ArrowRight")) { Player.MoveFreeStepFocus(k == "ArrowLeft" ? -1 : 1); return true; }
            if (Player.FreeStepPending && k == "Enter") { if (!e.Repeat) Player.ConfirmFreeStep(); return true; }
            if (FfKeys.Contains(k))
            {
                if (!e.Repeat) ArmFF();
                _ffHeld.Add(k);
                UpdateFF();
                return true;
            }
            if (e.Repeat) return k == "Enter" || k == "Tab";
            if (Digit.IsMatch(k)) { _autoSelect = null; HandView.Select(k[0] - '1'); return true; }
            if (k == "Enter")
            {
                // battle_ui_ux_v2 §3 入力の端 「選択中に Enter を 2 度押す」 (review 21): an Enter while the play it confirmed
                // resolves is kept, and confirms the card the scene picks next once the player is asked again.
                if (_autoSelect != null && Player.State != BattlePlayerState.Idle) { _autoSelect.Confirm = true; return true; }
                int at = HandView.SelectedIndex();
                int plays = Player.AcceptedPlays;
                if (HandView.Confirm() && at >= 0) _autoSelect = new AutoSelect { Index = at, Turn = TurnNow(), Plays = plays, Confirm = false };
                return true;
            }
            if (k == "Tab")
            {
                int dir = e.ShiftKey ? -1 : 1;
                if (!HandView.CycleTarget(dir) && !HandView.IsHolding() && !HandView.HasSelection() && Player.State == BattlePlayerState.Idle)
                    Player.CycleGapFocus(dir);
                return true;
            }
            if (k == "i" || k == "I") { HandView.ToggleSelectedDetail(); return true; }
            if (k == "x" || k == "X")
            {
                _autoSelect = null;
                Player.RequestEndTurn(queue: true, press: true);
                return true;
            }
            return false;
        }

        /// <summary>window keyup: a held fast-forward key lets go; Esc's release opens the settings after a press with nothing left to back out of.</summary>
        // js: scenes/battle/BattleScene.js:407
        private void OnKeyUp(KeyEvent e)
        {
            if (_s.Destroyed) return;
            if (_ffHeld.Remove(e.Key ?? "")) UpdateFF();
            if (e.Key == "Escape" && _escToSettings)
            {
                _escToSettings = false;
                if (!_s.Destroyed && Player != null && Player.State != BattlePlayerState.Over) _s.Overlays.OpenSettings();
            }
        }

        // ---- pointer (the scene's listeners) ----------------------------------------------------------------

        /// <summary>
        /// isControl(target): the buttons, the panels, the hud and the overlays take their own clicks — they never fast-forward;
        /// the end-turn plate is the exception (it counts as the arena while the flow runs).
        /// </summary>
        // js: scenes/battle/BattleScene.js:422
        private static bool IsControl(BattlePointer p)
        {
            if (p == null || p.Layer == null) return false;
            if (p.OnEndTurn) return false;
            if (p.OnControl) return true;
            return p.Layer == BattleLayer.Hud || p.Layer == BattleLayer.Over || p.Layer == BattleLayer.Panels;
        }

        /// <summary>root pointerdown: a click moves the result on; a press on the arena while the flow runs fast-forwards.</summary>
        // js: scenes/battle/BattleScene.js:436
        private void OnPointerDown(BattlePointer p)
        {
            if (Player == null) return;
            _s.Overlays.SettingsClosed(); // a click reached the scene: no modal overlay is up
            if (Player.State == BattlePlayerState.Over) { Player.SkipResult(); return; }
            _downWhileAsked = Player.State == BattlePlayerState.Idle;
            if (_downWhileAsked) _lastAskedPointer = Real.Now;
            if (p.Button == 0 && Player.CanFastForward() && !IsControl(p))
            {
                if (Real.Now - _lastAskedPointer < L.DoubleClickMs) return;
                _pointerFF = true;
                ArmFF();
            }
        }

        // js: scenes/battle/BattleScene.js:448
        private void OnPointerUp(BattlePointer p)
        {
            if (_downWhileAsked) { _downWhileAsked = false; _lastAskedPointer = Real.Now; }
            if (_pointerFF) { _pointerFF = false; UpdateFF(); }
        }

        /// <summary>window blur: a held key or button will not send its release here (review fix).</summary>
        // js: scenes/battle/BattleScene.js:453
        private void OnBlur()
        {
            ClearFF();
            _escToSettings = false;
        }

        /// <summary>visibilitychange: the page hidden (BattleSceneDriver.OnApplicationPause; KeyStack.Blurred follows it too).</summary>
        // js: scenes/battle/BattleScene.js:454
        internal void OnVisibility(bool hidden)
        {
            if (_s.Destroyed || !_pointerOn) return;
            if (hidden) ClearFF();
            Microtasks.Drain();
        }

        /// <summary>KeyStack.Blurred (window blur / the page hidden): the hand's listener first (registered first), then the scene's.</summary>
        private void OnBlurred()
        {
            if (_s.Destroyed) return;
            _lostButtons |= _buttons;
            _buttons = 0;
            try { HandView.OnBlur(); }
            catch (Exception err) { _s.Env.Log.Error(null, err); }
            Microtasks.Drain();
            OnBlur();
            Microtasks.Drain();
        }

        // js: scenes/battle/BattleScene.js:459
        private void OnState(BattlePlayerState s)
        {
            // The one-press fast-forward belongs to the flow that was running: it ends when the player is asked again.
            if (s == BattlePlayerState.Idle || s == BattlePlayerState.Over || s == BattlePlayerState.Halted) { _ffArmed = false; _ffUntil = 0; }
            UpdateFF();
            if (s == BattlePlayerState.Idle && _autoSelect != null)
            {
                AutoSelect a = _autoSelect;
                _autoSelect = null;
                // After the player has made the hand take input again (it does so right after it goes idle).
                Microtasks.Post(() =>
                {
                    if (_s.Destroyed || Player.State != BattlePlayerState.Idle || a.Turn != TurnNow() || HandView.IsHolding() || HandView.HasSelection()) return;
                    if (Player.AcceptedPlays <= a.Plays) return; // refused: the card slid home, nothing is selected again
                    IReadOnlyList<CardFace> faces = HandView.Faces();
                    int from = Math.Min(a.Index, faces.Count - 1);
                    int pick = -1;
                    for (int d = 0; d < faces.Count && pick < 0; d++)
                    {
                        foreach (int i in new[] { from + d, from - d })
                        {
                            if (i >= 0 && i < faces.Count && Player.IsPlayable(faces[i].Id)) { pick = i; break; }
                        }
                    }
                    if (pick < 0) return;
                    HandView.Select(pick, new SelectOpts { Auto = true });
                    // The second Enter of the pair (above): the picked card is confirmed as the first one was, and the pair ends there.
                    if (a.Confirm)
                    {
                        int plays2 = Player.AcceptedPlays;
                        if (HandView.Confirm()) _autoSelect = new AutoSelect { Index = pick, Turn = TurnNow(), Plays = plays2, Confirm = false };
                    }
                });
            }
            else if (s == BattlePlayerState.Over || s == BattlePlayerState.Halted) _autoSelect = null;
        }

        // ---- the pointer pump ---------------------------------------------------------------------------------

        /// <summary>
        /// Polls the pointer once a frame (battle-flow §3.1) and raises what the browser would, in its order: the root's
        /// pointerleave (the hand's), before the pointermove that left the root (boundary events come first in a browser);
        /// pointermove (hud, then the hand: their window listeners in the order they were added); pointerdown — the hud's capture
        /// listener, the scene's root listener, the hand's window listener — for a button pressed with none held (a chorded press
        /// is a pointermove); pointerup — the hand's, then the scene's — for the last button let go; contextmenu on the right
        /// button's release (Windows) — the hud's root listener, then the hand's. A press and its release inside one frame (a
        /// touchpad tap, a fast click) are both raised, as the browser saw both. A microtask drain follows every listener.
        /// </summary>
        internal void PumpPointer()
        {
            if (!_pointerOn || _s.Destroyed) return;
            Mouse mouse = Mouse.current;
            Pointer pointer = mouse != null ? mouse : Pointer.current;
            if (pointer == null) return;
            bool isMouse = pointer is Mouse;
            Vector2 screen = pointer.position.ReadValue();

            int now = 0, tap = 0;
            if (isMouse)
            {
                Poll(mouse.leftButton, 1, ref now, ref tap);
                Poll(mouse.rightButton, 2, ref now, ref tap);
                Poll(mouse.middleButton, 4, ref now, ref tap);
            }
            else Poll(pointer.press, 1, ref now, ref tap);
            // A button held through a blur sends nothing until it is let go.
            _lostButtons &= now;
            now &= ~_lostButtons;

            int was = _buttons;
            // A tap is a pointerdown and a pointerup only with no other button involved (inside a chord it changes nothing).
            if (was != 0 || now != 0) tap = 0;
            bool moved = !_havePos || (screen - _lastScreen).sqrMagnitude > 0.0001f;
            _havePos = true;
            _lastScreen = screen;
            BattleViewEnv env = _s.Env;
            // A still pointer with no button change raises nothing (the browser sends no event, boundary events included).
            if (!moved && tap == 0 && now == was)
            {
                env.ClearContextPinned();
                return;
            }

            BattlePointer p = Target(screen, pointer, isMouse, out bool stopsDown);

            // ---- leave: the root's pointerleave comes before the pointermove on what the pointer moved onto ----
            bool inside = !p.Outside;
            if (_inside && !inside)
            {
                _inside = false;
                Dispatch(() => HandView.OnLeave());
                if (_s.Destroyed) return;
            }
            _inside = inside;

            // ---- move ----
            int pressed = now & ~was;
            int released = was & ~now;
            bool chorded = (pressed != 0 && was != 0) || (released != 0 && now != 0);
            if (moved || chorded)
            {
                // A move comes before a press or a release seen in the same frame (it carries the buttons held before them);
                // a chorded press or release is itself a pointermove carrying the buttons held after it.
                p.Buttons = chorded ? now : was;
                p.Button = -1;
                Dispatch(() => HudView.OnPointerMove(p));
                Dispatch(() => HandView.OnPointerMove(p));
                if (_s.Destroyed) return;
            }

            // ---- down ----
            int downs = pressed | tap;
            if (downs != 0 && was == 0)
            {
                BattlePointer d = Copy(p);
                d.Button = FirstButton(downs);
                d.Buttons = now | tap;
                // window pointerdown, capture phase (hud.js:461): before the target's own listeners and the bubble.
                Dispatch(() => HudView.OnPinDown(d));
                if (!stopsDown)
                {
                    // the root's listener (bubble: root before window), only for a target inside the scene
                    if (!d.Outside) Dispatch(() => OnPointerDown(d));
                    Dispatch(() => HandView.OnPointerDown(d));
                }
                if (_s.Destroyed) return;
            }

            // ---- up ----
            int ups = released | tap;
            if (ups != 0 && now == 0)
            {
                BattlePointer u = Copy(p);
                u.Button = FirstButton(ups);
                u.Buttons = 0;
                Dispatch(() => HandView.OnPointerUp(u));
                Dispatch(() => OnPointerUp(u));
                if (_s.Destroyed) return;
            }

            // ---- contextmenu: Windows browsers fire it on the right button's release ----
            if ((ups & 2) != 0 && !p.Outside)
            {
                BattlePointer c = Copy(p);
                c.Button = 2;
                c.Buttons = now;
                Dispatch(() => HudView.OnPinContext(c));
                Dispatch(() => HandView.OnContext(c));
            }
            env.ClearContextPinned();
            _buttons = now;
        }

        /// <summary>One button's state this frame: held now (`now`), or pressed and let go again inside the frame (`tap`).</summary>
        private static void Poll(UnityEngine.InputSystem.Controls.ButtonControl b, int bit, ref int now, ref int tap)
        {
            if (b == null) return;
            if (b.isPressed) now |= bit;
            else if (b.wasPressedThisFrame && b.wasReleasedThisFrame) tap |= bit;
        }

        private static int FirstButton(int mask) => (mask & 1) != 0 ? 0 : (mask & 4) != 0 ? 1 : 2;

        private void Dispatch(Action listener)
        {
            if (_s.Destroyed) return;
            try { listener(); }
            catch (Exception err) { _s.Env.Log.Error(null, err); }
            Microtasks.Drain();
        }

        private static BattlePointer Copy(BattlePointer p) => new BattlePointer
        {
            X = p.X, Y = p.Y, ScreenX = p.ScreenX, ScreenY = p.ScreenY, Button = p.Button, Buttons = p.Buttons, IsMouse = p.IsMouse,
            PointerId = p.PointerId, Outside = p.Outside, Layer = p.Layer, OnEndTurn = p.OnEndTurn, OnControl = p.OnControl,
            OnJournal = p.OnJournal,
        };

        /// <summary>
        /// The pointer event as the listeners read it: the stage point and what the topmost raycast hit (e.target) says. The
        /// classes are looked up as e.target.closest(…) does, each on its own: the nearest .ocbg-battle-endturn and the
        /// nearest .ocbg-battle-hit may be different ancestors. `stopsDown`: an element on the way up stops pointerdown from
        /// propagating (piles.js:175).
        /// </summary>
        private BattlePointer Target(Vector2 screen, Pointer pointer, bool isMouse, out bool stopsDown)
        {
            stopsDown = false;
            BattleViewEnv env = _s.Env;
            StagePoint at = env.ToStage(screen);
            var p = new BattlePointer
            {
                X = at.X,
                Y = at.Y,
                ScreenX = screen.x,
                ScreenY = Screen.height - screen.y,
                IsMouse = isMouse,
                PointerId = pointer.deviceId,
            };
            GameObject top = TopHit(screen);
            bool onStage = at.X >= 0f && at.Y >= 0f && at.X <= L.W && at.Y <= L.H;
            if (top == null)
            {
                // Nothing but the stage under the pointer: the root itself on the stage, the page beside it off the stage.
                p.Outside = !onStage;
                return p;
            }
            Transform t = top.transform;
            if (!env.Contains(t))
            {
                p.Outside = true;
                return p;
            }
            p.Layer = env.LayerOf(t);
            p.OnJournal = _s.Overlays.InJournal(t);
            bool endTurn = false, hit = false;
            for (Transform a = t; a != null; a = a.parent)
            {
                BattleHitTag tag = a.GetComponent<BattleHitTag>();
                if (tag)
                {
                    if (tag.EndTurn) endTurn = true;
                    if (tag.Hit) hit = true;
                    if (tag.StopPointerDown) stopsDown = true;
                }
                if (a == env.Root) break;
            }
            p.OnEndTurn = endTurn;
            // .ocbg-battle-hit: a tagged piece, or a card (the hand's cards are hits wherever they stand: hand / held).
            p.OnControl = hit || p.Layer == BattleLayer.Hand || p.Layer == BattleLayer.Held;
            return p;
        }

        private static GameObject TopHit(Vector2 screen)
        {
            if (!EventSystem.current) return null;
            List<RaycastResult> hits = Hit.RaycastAll(screen);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }
    }
}
