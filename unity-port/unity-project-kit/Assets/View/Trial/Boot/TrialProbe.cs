// Dev only: drives the mouse in stage px so a test run (uloop execute-dynamic-code) can hover, press, drag and
// release exactly like the reference captures did with Playwright's page.mouse. Every call writes Mouse.current's
// state at once (InputState.Change, as uloop does, so the Game view needs no focus); a drag runs over frames.
//
//   Trial.Boot.TrialProbe.Move(960, 540);              // pointer to stage (960, 540)
//   Trial.Boot.TrialProbe.Down(); TrialProbe.Up();     // left button
//   Trial.Boot.TrialProbe.Drag(770, 960, 1240, 470, 300);   // press, glide 300 ms, release
//   Trial.Boot.TrialProbe.Busy                          // true while a glide runs
using System.Collections;
using Trial.Kit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Trial.Boot
{
    [AddComponentMenu("")]
    public sealed class TrialProbe : MonoBehaviour
    {
        private static TrialProbe _runner;
        private static Vector2 _screen;
        private static int _buttons;
        private static int _running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (_hooked) InputSystem.onBeforeUpdate -= OnBeforeUpdate;
            _hooked = false;
            Steps.Clear();
            _runner = null;
            _screen = Vector2.zero;
            _buttons = 0;
            _running = 0;
        }

        /// <summary>True while a Glide or Drag is still moving.</summary>
        public static bool Busy => _running > 0;

        /// <summary>The pointer's last stage position the probe set.</summary>
        public static Vector2 Stage => TrialStage.Current ? TrialStage.Current.ToStage(_screen) : _screen;

        public static void Move(float stageX, float stageY)
        {
            _screen = ToScreen(stageX, stageY);
            Send();
        }

        /// <summary>button 0 left, 1 right, 2 middle.</summary>
        public static void Down(int button = 0) => Step(() => { _buttons |= 1 << button; Send(); });

        public static void Up(int button = 0) => Step(() => { _buttons &= ~(1 << button); Send(); });

        public static void Click(float stageX, float stageY, int button = 0)
        {
            Move(stageX, stageY);
            Runner().StartCoroutine(ClickLater(button));
        }

        public static void RightClick(float stageX, float stageY) => Click(stageX, stageY, 1);

        /// <summary>Moves the pointer from where it is to (x, y) over ms (real time), one step per frame.</summary>
        public static void Glide(float stageX, float stageY, float ms)
        {
            Runner().StartCoroutine(GlideRoutine(Stage, new Vector2(stageX, stageY), ms, false, false));
        }

        /// <summary>Press at (x0, y0), glide to (x1, y1) over ms, release (release false keeps the button held).</summary>
        public static void Drag(float x0, float y0, float x1, float y1, float ms, bool release = true)
        {
            Move(x0, y0);
            Runner().StartCoroutine(GlideRoutine(new Vector2(x0, y0), new Vector2(x1, y1), ms, true, release));
        }

        /// <summary>Taps a key (Input System Key name, e.g. "Enter", "Escape", "Digit3", "X") over two frames.</summary>
        public static void KeyTap(string key)
        {
            if (!System.Enum.TryParse(key, true, out Key k)) throw new System.ArgumentException("TrialProbe.KeyTap: unknown key " + key);
            Runner().StartCoroutine(KeyRoutine(k));
        }

        /// <summary>Holds or releases a key (KeyDown(true) / KeyDown(false)).</summary>
        public static void KeyDown(string key, bool down)
        {
            if (!System.Enum.TryParse(key, true, out Key k)) throw new System.ArgumentException("TrialProbe.KeyDown: unknown key " + key);
            Step(() => SetKey(k, down));
        }

        /// <summary>
        /// Saves screenshots of the Game view `ms` real milliseconds from now, as `folder/name-NNNNms.png` (or
        /// `folder/name.png` for a single 0 with exact true). Call it in the same dynamic-code snippet as the action
        /// whose frames it should catch, so the times are measured from that action.
        /// </summary>
        public static void Shots(string folder, string name, params int[] ms)
        {
            System.IO.Directory.CreateDirectory(folder);
            foreach (int t in ms) Runner().StartCoroutine(ShotRoutine(System.IO.Path.Combine(folder, ms.Length == 1 ? name + ".png" : name + "-" + t.ToString("0000") + "ms.png"), t));
        }

        private static IEnumerator ShotRoutine(string path, int ms)
        {
            _running++;
            float t0 = Time.realtimeSinceStartup;
            while ((Time.realtimeSinceStartup - t0) * 1000f < ms) yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            _running--;
        }

        private static readonly System.Collections.Generic.HashSet<Key> Held = new System.Collections.Generic.HashSet<Key>();

        private static void SetKey(Key k, bool down)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (down) Held.Add(k); else Held.Remove(k);
            var state = new KeyboardState();
            foreach (Key h in Held) state.Set(h, true);
            InputState.Change(kb, state, InputUpdateType.Dynamic);
        }

        private static IEnumerator KeyRoutine(Key k)
        {
            _running++;
            Step(() => SetKey(k, true));
            Step(null);
            Step(() => SetKey(k, false));
            while (Steps.Count > 0) yield return null;
            _running--;
        }

        // Device changes run at the start of an input update (InputSystem.onBeforeUpdate), one queued step per
        // dynamic update, so a press lands in the frame the views poll it in (wasPressedThisFrame).
        private static readonly System.Collections.Generic.Queue<System.Action> Steps = new System.Collections.Generic.Queue<System.Action>();
        private static bool _hooked;

        private static void Step(System.Action a)
        {
            if (!_hooked) { InputSystem.onBeforeUpdate += OnBeforeUpdate; _hooked = true; }
            Steps.Enqueue(a);
        }

        private static void OnBeforeUpdate()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic || Steps.Count == 0) return;
            System.Action a = Steps.Dequeue();
            a?.Invoke();
        }

        /// <summary>Takes the Shots when the next key press reaches the key stack (times measured from it).</summary>
        public static void ShotsAfterKey(string folder, string name, params int[] ms)
        {
            System.Action<KeyEvent> once = null;
            once = e => { KeyStack.KeyDownCapture -= once; Shots(folder, name, ms); };
            KeyStack.KeyDownCapture += once;
        }

        /// <summary>Takes the Shots when the next pointer press is seen (times measured from it).</summary>
        public static void ShotsAfterPointerDown(string folder, string name, params int[] ms)
        {
            System.Action once = null;
            once = () => { KeyStack.PointerDownCapture -= once; Shots(folder, name, ms); };
            KeyStack.PointerDownCapture += once;
        }

        public static void Wheel(float dy)
        {
            Mouse m = Mouse.current;
            if (m == null) return;
            var state = new MouseState { position = _screen, buttons = (ushort)_buttons, scroll = new Vector2(0f, dy) };
            InputState.Change(m, state, InputUpdateType.Dynamic);
        }

        private static IEnumerator ClickLater(int button)
        {
            _running++;
            Step(() => { _buttons |= 1 << button; Send(); });
            Step(null);
            Step(() => { _buttons &= ~(1 << button); Send(); });
            while (Steps.Count > 0) yield return null;
            _running--;
        }

        private static IEnumerator GlideRoutine(Vector2 from, Vector2 to, float ms, bool press, bool release)
        {
            _running++;
            yield return null;
            if (press) { Step(() => { _buttons |= 1; Send(); }); while (Steps.Count > 0) yield return null; yield return null; }
            float t0 = Time.realtimeSinceStartup;
            float dur = Mathf.Max(0.001f, ms / 1000f);
            while (true)
            {
                float k = Mathf.Clamp01((Time.realtimeSinceStartup - t0) / dur);
                Vector2 p = Vector2.Lerp(from, to, k);
                Move(p.x, p.y);
                if (k >= 1f) break;
                yield return null;
            }
            yield return null;
            if (press && release) { Step(() => { _buttons &= ~1; Send(); }); while (Steps.Count > 0) yield return null; yield return null; }
            _running--;
        }

        private static Vector2 ToScreen(float x, float y)
        {
            TrialStage stage = TrialStage.Current;
            return stage ? stage.ToScreen(new Vector2(x, y)) : new Vector2(x, Screen.height - y);
        }

        private static void Send()
        {
            Mouse m = Mouse.current;
            if (m == null) return;
            var state = new MouseState { position = _screen, buttons = (ushort)_buttons };
            InputState.Change(m, state, InputUpdateType.Dynamic);
        }

        private static TrialProbe Runner()
        {
            if (_runner) return _runner;
            var go = new GameObject("TrialProbe (dev)") { hideFlags = HideFlags.HideAndDontSave };
            _runner = go.AddComponent<TrialProbe>();
            return _runner;
        }
    }
}
