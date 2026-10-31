// CSS animations (`animation: name duration timing delay iterations direction fill`, kit.md §3.3, §4.11) played on an
// element: Trial.Geometry's CssAnimation samples the @keyframes (per-segment easing, iterations, direction, fill) and
// the animator writes what they animate into the kit's pieces every frame:
//   opacity → UiNode.Opacity · transform → the UiNode's transform channel (the whole `transform` property, as CSS) ·
//   box-shadow → BoxGraphic.SetShadows · text-shadow → the label's TextGlow · stroke-dashoffset → PathGraphic.DashOffset.
// When the animation stops applying (it ended without a forwards fill, or it was removed) the values it touched go
// back to what they were when it started. Infinite animations loop until stopped or until their element is destroyed.
//
// Time: CSS animations run on the document's clock, so by default the animator follows CssTime (real time, unclamped);
// sceneTime = true follows the given scene clock instead (the battle re-runs DS keyframes on its own clock).
//
//   CssAnimator.Play(plate, "ocbg-explore-plate-in 300ms cubic-bezier(.22,.61,.36,1) both");
//   CssAnimator.Set(face, "ocbg-explore-beckon 2.4s ease-in-out infinite");      // the `animation` property: replaces
//   await CssAnimator.Play(drawer, "ocbg-explore-drawer-out 200ms ease-in both").Finished;
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>The document clock CSS animations and transitions run on, and the reduced-motion switch.</summary>
    public static class CssTime
    {
        private static TrialClock _clock;
        private static ClockDriver _driver;
        private static GameObject _host;

        /// <summary>
        /// prefers-reduced-motion: reduce (common.css:20): every CSS animation and transition lasts 1 ms. Unity has no
        /// OS signal for it; the boot may set it from the settings' 演出短縮. Off by default.
        /// </summary>
        public static bool ReducedMotion;

        /// <summary>A real-time clock (stage.js createClock semantics) on the current stage's driver, made on first use.</summary>
        public static TrialClock Clock
        {
            get
            {
                if (_clock != null && _clock.Alive && _driver) return _clock;
                _driver = Driver;
                _clock = _driver.CreateClock();
                return _clock;
            }
        }

        /// <summary>The live stage's ClockDriver, or (with no stage) a hidden one of the kit's own.</summary>
        public static ClockDriver Driver
        {
            get
            {
                TrialStage st = TrialStage.Current;
                ClockDriver d = st ? st.Clocks : null;
                if (d) return d;
                if (!_host)
                {
                    _host = new GameObject("TrialCssTime") { hideFlags = HideFlags.HideAndDontSave };
                    if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(_host);
                }
                return ClockDriver.Ensure(_host);
            }
        }

        /// <summary>The CSS clock's time in ms (real time since it started).</summary>
        public static float Now => Clock.RealNow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _clock = null;
            _driver = null;
            if (_host) UnityEngine.Object.DestroyImmediate(_host);
            _host = null;
            ReducedMotion = false;
        }
    }

    /// <summary>What an animation writes into: the element's node, its box, its label (text-shadow), its path.</summary>
    public sealed class AnimTarget
    {
        public UiNode Node;
        public BoxGraphic Box;
        public PathGraphic Path;
        /// <summary>The label whose text-shadow is animated (its TextGlow is made when needed).</summary>
        public TMP_Text Label;
        /// <summary>The glow to animate (null = the label's, made on first use).</summary>
        public TextGlow Glow;
        /// <summary>Called with every sample (properties the kit does not write, or extra work).</summary>
        public Action<AnimProps> Custom;

        /// <summary>The element: its node (added when missing), box, path and label found on it.</summary>
        public static AnimTarget Of(Component element)
        {
            if (!element) throw new ArgumentNullException(nameof(element));
            var rt = element.transform as RectTransform;
            var t = new AnimTarget();
            if (rt)
            {
                t.Node = UiNode.Of(rt);
                t.Box = rt.GetComponent<BoxGraphic>();
                t.Path = rt.GetComponent<PathGraphic>();
                t.Label = rt.GetComponent<TMP_Text>();
            }
            return t;
        }

        /// <summary>The GameObject the animations of this target are registered under.</summary>
        public GameObject Owner =>
            Node ? Node.gameObject : Box ? Box.gameObject : Path ? Path.gameObject : Label ? Label.gameObject : Glow ? Glow.gameObject : null;

        /// <summary>
        /// Whether something it writes into is still alive: one of its objects, or (a target of a Custom callback alone,
        /// with no object) the callback. Once its objects are destroyed, a Custom callback does not keep it running.
        /// </summary>
        public bool Alive
        {
            get
            {
                if (Node || Box || Path || Label || Glow) return true;
                bool hadObject = !ReferenceEquals(Node, null) || !ReferenceEquals(Box, null) || !ReferenceEquals(Path, null)
                                 || !ReferenceEquals(Label, null) || !ReferenceEquals(Glow, null);
                return !hadObject && Custom != null;
            }
        }

        internal TextGlow GlowFor(bool create)
        {
            if (Glow) return Glow;
            if (!Label) return null;
            Glow = TextGlow.Of(Label);
            if (!Glow && create) Glow = TextGlow.For(Label, Array.Empty<TextShadow>());
            return Glow;
        }
    }

    public sealed class CssAnimator : IDisposable
    {
        private static readonly Dictionary<GameObject, List<CssAnimator>> ByOwner = new Dictionary<GameObject, List<CssAnimator>>();

        private readonly AnimTarget _target;
        private readonly TrialClock _clock;
        private readonly bool _sceneTime;
        private readonly PromiseSource _done = new PromiseSource();
        private IDisposable _frame;
        private float _t0;
        private float _pausedAt = float.NaN;
        private bool _running;
        private bool _holding;
        private GameObject _owner;

        // what was there before the animation (restored when it stops applying)
        private bool _touchedOpacity, _touchedTransform, _touchedBox, _touchedText, _touchedDash;
        private float _baseOpacity = 1f;
        private float _bTx, _bTy, _bSx = 1f, _bSy = 1f, _bRot, _bPx, _bPy;
        private List<BoxShadow> _baseShadows;
        private TextShadow[] _baseText;
        private float _baseDash = float.NaN;

        /// <summary>The animation played (a copy: reduced motion shortens it).</summary>
        public CssAnimation Animation { get; }

        public AnimTarget Target => _target;

        /// <summary>animationend: resolves once a finite animation has run (never for an infinite or a stopped one).</summary>
        public Promise Finished => _done.Promise;

        /// <summary>Still sampling (not finished, not stopped).</summary>
        public bool Running => _running;

        /// <summary>
        /// Finished, and its forwards fill still holds the end values: it stays on the element (On lists it) until it is
        /// removed (Stop, StopAll, Set), and removing it brings the values back, as removing a CSS animation does.
        /// </summary>
        public bool Holding => _holding;

        /// <summary>animation-play-state: paused holds the animation where it is.</summary>
        public bool Paused
        {
            get => !float.IsNaN(_pausedAt);
            set
            {
                if (value == Paused) return;
                if (value) _pausedAt = Now;
                else { _t0 += Now - _pausedAt; _pausedAt = float.NaN; }
            }
        }

        private CssAnimator(AnimTarget target, CssAnimation anim, TrialClock clock, bool sceneTime)
        {
            _target = target;
            _clock = clock ?? CssTime.Clock;
            _sceneTime = sceneTime && clock != null;
            Animation = anim.Clone();
            if (CssTime.ReducedMotion) Animation.DurationMs = 1f;
            if (anim.Paused) _pausedAt = 0f;
        }

        private float Now => _sceneTime ? _clock.Now : _clock.RealNow;

        /// <summary>Elapsed animation time (ms since it was applied, pauses excluded).</summary>
        public float LocalTime => (Paused ? _pausedAt : Now) - _t0;

        // ---- playing ------------------------------------------------------------------------------------------

        /// <summary>Plays an animation on a target (added to the animations already on it).</summary>
        public static CssAnimator Play(AnimTarget target, CssAnimation anim, TrialClock clock = null, bool sceneTime = false)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (anim == null) throw new ArgumentNullException(nameof(anim));
            var a = new CssAnimator(target, anim, clock, sceneTime);
            a.Start();
            return a;
        }

        /// <summary>Plays an `animation` shorthand ("ocbg-explore-ripple 820ms cubic-bezier(.22,.61,.36,1)") on an element.</summary>
        public static CssAnimator Play(Component element, string shorthand, TrialClock clock = null, bool sceneTime = false) =>
            Play(AnimTarget.Of(element), CssAnimation.Parse(shorthand), clock, sceneTime);

        /// <summary>
        /// kit.md §4.11 CssAnimator.Play(node, anim, frames): a parsed animation on an element; frames (optional)
        /// replace the animation's own @keyframes (a scene's keyframes that are not among the built-in ones).
        /// </summary>
        public static CssAnimator Play(Component element, CssAnimation anim, CssKeyframes frames = null, TrialClock clock = null, bool sceneTime = false)
        {
            if (anim == null) throw new ArgumentNullException(nameof(anim));
            CssAnimation a = anim;
            if (frames != null)
            {
                a = anim.Clone();
                a.Keyframes = frames;
            }
            return Play(AnimTarget.Of(element), a, clock, sceneTime);
        }

        /// <summary>
        /// The `animation` property assigned (a class added, an inline style set): the element's animations stop (their
        /// values go back) and every animation of the comma list starts; "none" only stops.
        /// </summary>
        public static List<CssAnimator> Set(Component element, string animationCss, TrialClock clock = null, bool sceneTime = false)
        {
            AnimTarget t = AnimTarget.Of(element);
            StopAll(t.Owner);
            var list = new List<CssAnimator>();
            foreach (CssAnimation a in CssAnimation.ParseList(animationCss)) list.Add(Play(t, a, clock, sceneTime));
            return list;
        }

        /// <summary>Stops every animation of an element (restore: their values go back).</summary>
        public static void StopAll(GameObject owner, bool restore = true)
        {
            if (!owner || !ByOwner.TryGetValue(owner, out List<CssAnimator> list)) return;
            foreach (CssAnimator a in list.ToArray()) a.Stop(restore);
        }

        public static void StopAll(Component element, bool restore = true)
        {
            if (element) StopAll(element.gameObject, restore);
        }

        /// <summary>The animations on an element: running, or finished and holding a forwards fill.</summary>
        public static IReadOnlyList<CssAnimator> On(GameObject owner) =>
            owner && ByOwner.TryGetValue(owner, out List<CssAnimator> list) ? list : (IReadOnlyList<CssAnimator>)Array.Empty<CssAnimator>();

        private void Start()
        {
            Snapshot();
            _t0 = Now;
            if (Paused) _pausedAt = _t0;
            _running = true;
            _owner = _target.Owner;
            if (ByOwner.Count > 64)
            {
                var dead = new List<GameObject>();
                foreach (GameObject k in ByOwner.Keys) if (!k) dead.Add(k);
                foreach (GameObject k in dead) ByOwner.Remove(k);
            }
            if (_owner)
            {
                if (!ByOwner.TryGetValue(_owner, out List<CssAnimator> list)) ByOwner[_owner] = list = new List<CssAnimator>();
                // The same animation played again: one that holds its fill gives way (so repeated plays do not pile up),
                // and the values it found stay the ones a removal brings back.
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    CssAnimator h = list[i];
                    if (h == this || !h._holding || !string.Equals(h.Animation.Name, Animation.Name, StringComparison.Ordinal)) continue;
                    InheritBase(h);
                    h._holding = false;
                    list.RemoveAt(i);
                }
                list.Add(this);
            }
            _frame = _clock.OnFrame((dt, real) => Tick());
            Tick();
        }

        /// <summary>Starts it again from 0 (the class removed and added).</summary>
        public void Restart()
        {
            if (_running || _holding) Restore();
            _frame?.Dispose();
            _frame = null;
            _running = false;
            _holding = false;
            Unregister();
            Start();
        }

        /// <summary>Removes the animation, running or holding its fill (restore: the values it touched go back).</summary>
        public void Stop(bool restore = true)
        {
            if (!_running && !_holding) return;
            _running = false;
            _holding = false;
            _frame?.Dispose();
            _frame = null;
            if (restore) Restore();
            Unregister();
        }

        public void Dispose() => Stop(true);

        private void Unregister()
        {
            if (_owner && ByOwner.TryGetValue(_owner, out List<CssAnimator> list))
            {
                list.Remove(this);
                if (list.Count == 0) ByOwner.Remove(_owner);
            }
        }

        private void Tick()
        {
            if (!_running) return;
            if (!_target.Alive)
            {
                _running = false;
                _frame?.Dispose();
                _frame = null;
                Unregister();
                return;
            }
            float t = LocalTime;
            bool applied = Animation.TrySample(t, out AnimProps props);
            if (applied) Apply(props);
            else Restore();
            if (Animation.IsFinished(t))
            {
                _running = false;
                _frame?.Dispose();
                _frame = null;
                // A forwards fill keeps the end values while the animation stays on the element.
                _holding = applied;
                if (!_holding) Unregister();
                _done.TryResolve();
            }
        }

        // ---- writing --------------------------------------------------------------------------------------------

        private void Snapshot()
        {
            UiNode n = _target.Node;
            if (n)
            {
                _baseOpacity = n.Opacity;
                _bTx = n.TranslateX; _bTy = n.TranslateY; _bSx = n.ScaleX; _bSy = n.ScaleY; _bRot = n.Rotate;
                _bPx = n.TranslatePctX; _bPy = n.TranslatePctY;
            }
            if (_target.Box) _baseShadows = new List<BoxShadow>(_target.Box.Style.Shadows);
            TextGlow g = _target.GlowFor(false);
            _baseText = g ? ToArray(g.Shadows) : null;
            if (_target.Path) _baseDash = _target.Path.DashOffset;
        }

        // The base values a replaced animation found before it touched them.
        private void InheritBase(CssAnimator h)
        {
            if (h._touchedOpacity) _baseOpacity = h._baseOpacity;
            if (h._touchedTransform)
            {
                _bTx = h._bTx; _bTy = h._bTy; _bSx = h._bSx; _bSy = h._bSy; _bRot = h._bRot;
                _bPx = h._bPx; _bPy = h._bPy;
            }
            if (h._touchedBox) _baseShadows = h._baseShadows;
            if (h._touchedText) _baseText = h._baseText;
            if (h._touchedDash) _baseDash = h._baseDash;
        }

        private static TextShadow[] ToArray(IReadOnlyList<TextShadow> l)
        {
            var o = new TextShadow[l.Count];
            for (int i = 0; i < o.Length; i++) o[i] = l[i];
            return o;
        }

        /// <summary>Writes one sample into the target.</summary>
        public void Apply(AnimProps p)
        {
            UiNode n = _target.Node;
            if (p.Opacity.HasValue && n)
            {
                n.Opacity = p.Opacity.Value;
                _touchedOpacity = true;
            }
            if (p.Transform != null && n)
            {
                n.SetTransform(p.Tx ?? 0f, p.Ty ?? 0f, p.Sx ?? 1f, p.Sy ?? 1f, p.Rot ?? 0f, (p.TxPercent ?? 0f) / 100f, (p.TyPercent ?? 0f) / 100f);
                _touchedTransform = true;
            }
            if (p.BoxShadows != null && _target.Box)
            {
                _target.Box.SetShadows(p.BoxShadows);
                _touchedBox = true;
            }
            if (p.TextShadows != null)
            {
                TextGlow g = _target.GlowFor(true);
                if (g)
                {
                    g.SetShadows(p.TextShadows);
                    _touchedText = true;
                }
            }
            if (p.DashOffset.HasValue && _target.Path)
            {
                _target.Path.DashOffset = p.DashOffset.Value;
                _touchedDash = true;
            }
            _target.Custom?.Invoke(p);
        }

        private void Restore()
        {
            UiNode n = _target.Node;
            if (_touchedOpacity && n) n.Opacity = _baseOpacity;
            if (_touchedTransform && n) n.SetTransform(_bTx, _bTy, _bSx, _bSy, _bRot, _bPx, _bPy);
            if (_touchedBox && _target.Box) _target.Box.SetShadows(_baseShadows);
            if (_touchedText)
            {
                TextGlow g = _target.GlowFor(false);
                if (g) g.SetShadows(_baseText ?? Array.Empty<TextShadow>());
            }
            if (_touchedDash && _target.Path) _target.Path.DashOffset = _baseDash;
            _touchedOpacity = _touchedTransform = _touchedBox = _touchedText = _touchedDash = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ByOwner.Clear();
        }
    }
}
