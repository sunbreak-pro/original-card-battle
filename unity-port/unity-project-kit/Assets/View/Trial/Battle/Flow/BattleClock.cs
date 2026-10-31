// The battle scene's clocks: port of scenes/battle/clock.js on Trial.Async's TrialClock.
//
// The scaled scene clock (Unity's UiTween with UiTween.Speed) and an unscaled one for input timings
// (WaitForSecondsRealtime), on one frame loop. Every length the scene animates is a nominal ms written at battle
// speed ×1.25 (EffectCatalog). The scene clock runs at scale = BattleSpeed.TweenSpeed(step) (×1.0 → 0.8,
// ×1.25 → 1, ×1.5 → 1.2), times L.FfScale while fast-forward is on, and L.ShortScale when 演出短縮 is on or the
// battle is halted (battle_ui_ux_v2 §5.0: 補間と待ちの ms を 1 ms). The scale is read every frame, so a change takes
// effect at once on tweens and waits already running, as UiTween.Speed does.
//
// After Destroy() pending promises never resolve: a destroyed scene's flow stops where it stands and never reaches
// the source again (TrialClock.Stop(resolvePending: false)).
//
// The driver calls Step(dtRealMs) once a frame with the unscaled frame time; TrialClock clamps the scene's step to
// 50 ms as stage.js's frame clock does, runs the frame's work and drains the microtask queue at its end.
using System;
using System.Collections.Generic;
using Depiction;

namespace Trial.Battle
{
    /// <summary>
    /// clock.js createBattleClock(settings) → { clock, real, destroy }. This object is the scene clock (`env.clock`);
    /// <see cref="Real"/> is `env.real`; <see cref="Destroy"/> is the returned destroy().
    /// </summary>
    public sealed class BattleClock
    {
        /// <summary>One entry of frameFns. Removed marks a deletion made while the set is being walked.</summary>
        private sealed class FrameFn
        {
            public Action<float, float> Fn;
            public bool Removed;
        }

        private readonly IBattleSettings _settings;
        private readonly TrialClock _clock;
        /// <summary>
        /// JS frameFns (a Set, in insertion order): the same function subscribed twice runs once, and a walk of the set
        /// visits what is added during it and skips what is deleted before its turn (Set.prototype.forEach).
        /// </summary>
        private readonly List<FrameFn> _frameFns = new List<FrameFn>();
        private int _walking;
        private bool _compact;
        private bool _alive = true;
        private bool _ff;
        private bool _halted;
        private bool _paused;
        /// <summary>The paced factor taken at the frame's start (clock.js:69 computes dtPaced before any callback runs).</summary>
        private float _pacedNow;

        /// <param name="settings">The app's settings (speed, 演出短縮), read live every frame. Null reads as the defaults.</param>
        // js: scenes/battle/clock.js:37
        public BattleClock(IBattleSettings settings)
        {
            _settings = settings;
            _clock = new TrialClock(ComputeScale, PacedScale, clampSceneStep: true);
            // The frame's OnFrame walk is this clock's own (JS Set semantics); TrialClock calls it once a frame, after the
            // frame's tweens, runs and waits and their resolutions, before the microtask drain (clock.js:107-110).
            _clock.OnFrame(WalkFrameFns);
            Real = new BattleRealClock(this);
        }

        /// <summary>The TrialClock underneath (the real clock reads it).</summary>
        internal TrialClock Inner => _clock;

        /// <summary>`env.real`: the unscaled clock (input timings, the refusal line's 3.5 s, the result hold).</summary>
        public BattleRealClock Real { get; }

        /// <summary>False once <see cref="Destroy"/> ran (scene addition: the JS keeps it private).</summary>
        public bool Alive => _alive;

        /// <summary>clock.now: scene time in nominal ms.</summary>
        // js: scenes/battle/clock.js:117
        public float Now => _clock.Now;

        /// <summary>clock.scale: the factor scene time ran at in the last frame (1 before the first).</summary>
        // js: scenes/battle/clock.js:119
        public float Scale => _clock.Scale;

        /// <summary>clock.fastForward.</summary>
        // js: scenes/battle/clock.js:120
        public bool FastForward => _ff;

        /// <summary>clock.paused: an overlay holds scene time still (scale 0); real time runs on.</summary>
        // js: scenes/battle/clock.js:172
        public bool Paused => _paused;

        private bool ShortFx => _settings != null && _settings.ShortFx;

        private BattleSpeedStep SpeedStep => _settings != null ? StepOfSpeed(_settings.Speed) : BattleSpeed.Default;

        /// <summary>JS `Math.max(0, ms || 0)`: NaN and negatives read as 0.</summary>
        internal static float Ms(float ms) => float.IsNaN(ms) || ms < 0f ? 0f : ms;

        // ---- the scale ------------------------------------------------------------------------------

        /// <summary>
        /// The factor scene time runs at this frame. An overlay over the battle (the journal, the settings) holds the
        /// scene still (scene addition). TrialClock asks for it once at the frame's start; the paced factor is taken right
        /// after it, from the same flags (clock.js:66-69), and kept for the frame's paced runs.
        /// </summary>
        // js: scenes/battle/clock.js:49
        private float ComputeScale()
        {
            float s;
            if (_paused) s = 0f;
            else
            {
                s = BattleSpeed.TweenSpeed(SpeedStep);
                if (_halted || ShortFx) s *= L.ShortScale;
                else if (_ff) s *= L.FfScale;
            }
            // js: scenes/battle/clock.js:69 (const dtPaced = dtReal * pacedScale(), before the frame's callbacks)
            _pacedNow = PacedScaleOf(s);
            return s;
        }

        /// <summary>The factor a paced run (RunPaced) advances at: the scene's, but under fast-forward at most L.FfFloatScale × the speed.</summary>
        // js: scenes/battle/clock.js:58
        private float PacedScaleOf(float scale)
        {
            if (_paused) return 0f;
            if (!_ff || _halted || ShortFx) return scale;
            return Math.Min(scale, BattleSpeed.TweenSpeed(SpeedStep) * L.FfFloatScale);
        }

        /// <summary>TrialClock's paced factor: the one taken at this frame's start (ComputeScale).</summary>
        private float PacedScale() => _pacedNow;

        // ---- the frame ------------------------------------------------------------------------------

        /// <summary>
        /// One frame (the stage clock's rAF callback calling step(dtReal)): real waits, then scene time — tweens, runs,
        /// waits, paced runs, each list taken whole so that work started from inside a callback is kept (review 19) —,
        /// then every OnFrame(dtScene, dtReal), then the microtask queue. A destroyed clock does nothing.
        /// </summary>
        // js: scenes/battle/clock.js:65 (step) and :113 (createClock((dtReal) => { if (alive) step(dtReal); }))
        public void Step(float dtRealMs)
        {
            if (!_alive) return;
            _clock.Step(dtRealMs);
        }

        // ---- tweens, runs, waits --------------------------------------------------------------------

        /// <summary>
        /// clock.tween(obj, to, ms, ease, delay): tween numeric keys of obj to `to` over `ms` nominal ms. Replaces the
        /// same keys in running tweens of obj (an older tween left with no key resolves). Start values are read when the
        /// delay ends. Ease null = Ease.Out (the JS default).
        /// </summary>
        // js: scenes/battle/clock.js:122
        public Promise Tween(ITweenable obj, (string Key, float To)[] to, float ms, EaseFn ease = null, float delay = 0f)
        {
            if (!_alive) return Promise.Never();
            return _clock.Tween(obj, to, Ms(ms), ease, Ms(delay));
        }

        /// <summary>clock.tween(obj, { key: to }, ms, ease, delay): one key.</summary>
        // js: scenes/battle/clock.js:122
        public Promise Tween(ITweenable obj, string key, float to, float ms, EaseFn ease = null, float delay = 0f)
            => Tween(obj, new[] { (key, to) }, ms, ease, delay);

        /// <summary>
        /// clock.run(ms, ease, fn) = UiTween.Run: fn(easedT, rawT) every frame until `ms` nominal ms have passed; fn(1, 1)
        /// last. ms 0 calls fn(1, 1) at once and returns a resolved promise. A throw in fn is reported, never rethrown.
        /// </summary>
        // js: scenes/battle/clock.js:136
        public Promise Run(float ms, EaseFn ease, Action<float, float> fn)
        {
            if (!_alive) return Promise.Never();
            return _clock.Run(Ms(ms), ease, fn);
        }

        /// <summary>clock.run with a callback that reads the eased t only (the common JS call `(t) => …`).</summary>
        // js: scenes/battle/clock.js:136
        public Promise Run(float ms, EaseFn ease, Action<float> fn)
            => Run(ms, ease, fn == null ? null : (Action<float, float>)((e, raw) => fn(e)));

        /// <summary>
        /// clock.runPaced: a run that fast-forward speeds up less (review 16: at ×1000 a damage number lived two frames):
        /// as Run, but while fast-forward is on it advances at most L.FfFloatScale × the battle speed. For what stands
        /// beside the flow and never holds it — the floating numbers and words. With 演出短縮 on or the battle halted (read
        /// when it is asked for) it is a plain Run.
        /// </summary>
        // js: scenes/battle/clock.js:150
        public Promise RunPaced(float ms, EaseFn ease, Action<float, float> fn)
        {
            if (!_alive) return Promise.Never();
            ms = Ms(ms);
            if (ms == 0f || ShortFx || _halted) return Run(ms, ease, fn);
            return _clock.RunPaced(ms, ease, fn);
        }

        /// <summary>clock.runPaced with a callback that reads the eased t only.</summary>
        // js: scenes/battle/clock.js:150
        public Promise RunPaced(float ms, EaseFn ease, Action<float> fn)
            => RunPaced(ms, ease, fn == null ? null : (Action<float, float>)((e, raw) => fn(e)));

        /// <summary>clock.wait(ms) = UiTween.Wait: resolves after `ms` nominal ms of scene time; 0 = an already resolved promise.</summary>
        // js: scenes/battle/clock.js:157
        public Promise Wait(float ms)
        {
            if (!_alive) return Promise.Never();
            return _clock.Wait(Ms(ms));
        }

        /// <summary>
        /// clock.onFrame(fn): fn(dtScene, dtReal) every frame, after the frame's tweens and waits (dtReal is the frame
        /// time clamped to 50 ms, dtScene that times the scale; paused, dtScene is 0). Returns the unsubscribe function.
        /// The JS keeps a Set: the same function subscribed twice runs once (and one unsubscribe drops it); a function
        /// subscribed from inside the frame's walk runs in that frame, one unsubscribed before its turn does not.
        /// </summary>
        // js: scenes/battle/clock.js:164
        public Action OnFrame(Action<float, float> fn)
        {
            if (fn == null) throw new ArgumentNullException(nameof(fn));
            if (!_alive) return () => { };
            if (LiveFrameFn(fn) == null) _frameFns.Add(new FrameFn { Fn = fn });
            return () =>
            {
                FrameFn entry = LiveFrameFn(fn);
                if (entry == null) return;
                entry.Removed = true;
                if (_walking > 0) _compact = true;
                else _frameFns.Remove(entry);
            };
        }

        /// <summary>console.error(err): where TrialClock reports a callback's throw (Microtasks.Unhandled); a broken logger is swallowed.</summary>
        private static void ReportError(Exception ex)
        {
            try { Microtasks.Unhandled?.Invoke(ex); }
            catch (Exception) { /* a broken logger must not stop the frame */ }
        }

        private FrameFn LiveFrameFn(Action<float, float> fn)
        {
            foreach (FrameFn e in _frameFns)
                if (!e.Removed && e.Fn == fn) return e;
            return null;
        }

        /// <summary>frameFns.forEach((fn) => { try { fn(dt, dtReal); } catch (err) { console.error(err); } }).</summary>
        // js: scenes/battle/clock.js:108
        private void WalkFrameFns(float dtScene, float dtReal)
        {
            _walking++;
            try
            {
                // The count is read each turn: an entry added meanwhile is visited, as a Set's forEach does.
                for (int i = 0; i < _frameFns.Count; i++)
                {
                    if (!_alive) break;
                    FrameFn e = _frameFns[i];
                    if (e.Removed) continue;
                    try { e.Fn(dtScene, dtReal); }
                    catch (Exception ex) { ReportError(ex); }
                }
            }
            finally
            {
                _walking--;
                if (_walking == 0 && _compact)
                {
                    _compact = false;
                    _frameFns.RemoveAll(e => e.Removed);
                }
            }
        }

        /// <summary>clock.setFastForward(on): the scene's (BattleScene.updateFF).</summary>
        // js: scenes/battle/clock.js:168
        public void SetFastForward(bool on) => _ff = on;

        /// <summary>clock.setHalted(on): what is left of a halted battle settles at L.ShortScale (nothing in the trial calls it).</summary>
        // js: scenes/battle/clock.js:169
        public void SetHalted(bool on) => _halted = on;

        /// <summary>clock.setPaused(on): holds scene time still (0 scale) while an overlay covers the battle; real time runs on.</summary>
        // js: scenes/battle/clock.js:171
        public void SetPaused(bool on) => _paused = on;

        /// <summary>
        /// destroy(): every pending tween, run, wait and real wait never resolves, later ones return promises that never
        /// settle, OnFrame subscribers are dropped and Step does nothing (TrialClock.Stop(resolvePending: false)).
        /// </summary>
        // js: scenes/battle/clock.js:187
        public void Destroy()
        {
            if (!_alive) return;
            _alive = false;
            _frameFns.Clear();
            _clock.Stop(resolvePending: false);
        }

        // ---- settings.speed ↔ BattleSpeedStep -----------------------------------------------------------

        /// <summary>
        /// settings.speed (1 | 1.25 | 1.5) → BattleSpeedStep. Anything else reads as the default (×1.25). The JS compares a
        /// double with `===`, so this takes the double as the app stores it (a float widens to the same value).
        /// </summary>
        // js: scenes/battle/clock.js:17
        public static BattleSpeedStep StepOfSpeed(double speed)
        {
            if (speed == 1.0) return BattleSpeedStep.Slow;
            if (speed == 1.5) return BattleSpeedStep.Fast;
            if (speed == 1.25) return BattleSpeedStep.Normal;
            return BattleSpeed.Default;
        }

        /// <summary>settings.speed as a string ('1' | '1.0' | '1.25' | '1.5', a stored form) → BattleSpeedStep; anything else the default.</summary>
        // js: scenes/battle/clock.js:17
        public static BattleSpeedStep StepOfSpeed(string speed)
        {
            if (speed == "1" || speed == "1.0") return BattleSpeedStep.Slow;
            if (speed == "1.5") return BattleSpeedStep.Fast;
            if (speed == "1.25") return BattleSpeedStep.Normal;
            return BattleSpeed.Default;
        }

        /// <summary>BattleSpeedStep → the number settings.speed keeps (1 | 1.25 | 1.5).</summary>
        // js: scenes/battle/clock.js:25
        public static double SpeedOfStep(BattleSpeedStep step)
            => step == BattleSpeedStep.Slow ? 1.0 : step == BattleSpeedStep.Fast ? 1.5 : 1.25;
    }

    /// <summary>clock.js `real` (`env.real`): the unscaled clock of the battle — input timings, WaitForSecondsRealtime.</summary>
    public sealed class BattleRealClock
    {
        private readonly BattleClock _owner;

        internal BattleRealClock(BattleClock owner) { _owner = owner; }

        /// <summary>real.now: real ms (unclamped frame times summed; the JS reads performance.now() at the last frame).</summary>
        // js: scenes/battle/clock.js:176
        public float Now => _owner.Inner.RealNow;

        /// <summary>
        /// real.wait(ms): resolves after `ms` real ms (not scaled, not paused) — on the first frame at or past it, then one
        /// microtask later (the JS `.then`). A clock destroyed by then never resolves it.
        /// </summary>
        // js: scenes/battle/clock.js:178
        public Promise Wait(float ms)
        {
            if (!_owner.Alive) return Promise.Never();
            return _owner.Inner.WaitReal(BattleClock.Ms(ms)).Then((Func<Promise>)(() => _owner.Alive ? null : Promise.Never()));
        }
    }
}
