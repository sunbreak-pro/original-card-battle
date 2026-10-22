// The scene clock: tweens, runs, waits and per-frame callbacks on one frame loop, returning Promises.
// Port of stage.js createClock merged with the battle clock's extras (clock.js): a scale read every frame,
// runs, paced runs, real-time waits, and "work added from inside a callback is kept".
//
// Engine-free. A MonoBehaviour (the stage, a scene) calls Step(dtRealMs) once per frame with the unscaled
// frame time; Step resolves what is due and drains the microtask queue at its end, so continuations run in
// the same frame, after the frame's own work (the browser's rAF + microtask order).
using System;
using System.Collections.Generic;

namespace Trial
{
    /// <summary>Something a tween can drive by key (the JS tweens set numeric properties of plain objects).</summary>
    public interface ITweenable
    {
        float GetTween(string key);
        void SetTween(string key, float value);
    }

    /// <summary>A bag of named numbers a tween can drive; Changed fires after every set (e.g. to re-apply a transform).</summary>
    public sealed class TweenVars : ITweenable
    {
        private readonly Dictionary<string, float> _values = new Dictionary<string, float>(StringComparer.Ordinal);
        public event Action Changed;

        public TweenVars() { }

        public TweenVars(params (string Key, float Value)[] initial)
        {
            foreach (var (k, v) in initial) _values[k] = v;
        }

        public float this[string key]
        {
            get => _values.TryGetValue(key, out float v) ? v : 0f;
            set { _values[key] = value; Changed?.Invoke(); }
        }

        public bool Has(string key) => _values.ContainsKey(key);
        public float GetTween(string key) => this[key];
        public void SetTween(string key, float value) => this[key] = value;
    }

    public sealed class TrialClock
    {
        private sealed class TweenItem
        {
            public ITweenable Obj;
            public Dictionary<string, float> To;
            public Dictionary<string, float> From;
            public float Ms;
            public EaseFn Ease;
            public float T0;
            public PromiseSource Done;
        }

        private sealed class RunItem
        {
            public float Ms;
            public EaseFn Ease;
            public Action<float, float> Fn;
            public float T0;
            public float Elapsed;
            public PromiseSource Done;
        }

        private sealed class TimerItem
        {
            public float At;
            public PromiseSource Done;
        }

        /// <summary>Longest frame step the scene time takes (ms), as the JS clocks clamp dt to 50.</summary>
        public const float MaxStepMs = 50f;

        private readonly Func<float> _scale;
        private readonly Func<float> _pacedScale;
        private readonly bool _clampScene;

        private List<TweenItem> _tweens = new List<TweenItem>();
        private List<RunItem> _runs = new List<RunItem>();
        private List<RunItem> _paced = new List<RunItem>();
        private List<TimerItem> _timers = new List<TimerItem>();
        private List<TimerItem> _realTimers = new List<TimerItem>();
        private readonly List<Action<float, float>> _frameFns = new List<Action<float, float>>();

        private float _now;
        private float _realNow;
        private float _lastScale = 1f;
        private bool _alive = true;

        /// <param name="scale">Read once per frame: the factor scene time runs at (null = 1). 0 holds scene time still.</param>
        /// <param name="pacedScale">Read once per frame: the factor RunPaced advances at (null = the scale).</param>
        /// <param name="clampSceneStep">
        /// true (the battle clock): scene time advances by at most MaxStepMs × scale per frame.
        /// false (stage.js createClock): scene time follows real time, only the dt handed to OnFrame is clamped.
        /// </param>
        public TrialClock(Func<float> scale = null, Func<float> pacedScale = null, bool clampSceneStep = true)
        {
            _scale = scale;
            _pacedScale = pacedScale;
            _clampScene = clampSceneStep;
        }

        /// <summary>Scene time in ms (nominal ms when a scale is set).</summary>
        public float Now => _now;

        /// <summary>Real time in ms since the clock started.</summary>
        public float RealNow => _realNow;

        /// <summary>The factor scene time ran at in the last step.</summary>
        public float Scale => _lastScale;

        public bool Alive => _alive;

        // ---- tweens -------------------------------------------------------------------------------

        /// <summary>Tweens one key of obj to `to` over `ms`. Replaces that key in running tweens of the same obj.</summary>
        public Promise Tween(ITweenable obj, string key, float to, float ms, EaseFn ease = null, float delay = 0f)
            => Tween(obj, new[] { (key, to) }, ms, ease, delay);

        /// <summary>clock.tween(obj, {k: v, …}, ms, ease, delay). The start values are read when the delay ends.</summary>
        public Promise Tween(ITweenable obj, (string Key, float To)[] to, float ms, EaseFn ease = null, float delay = 0f)
        {
            if (!_alive) return Promise.Never();
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            var keys = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var (k, v) in to) keys[k] = v;
            // Drop these keys from older tweens of the same object; an older tween left with no key resolves.
            _tweens.RemoveAll(tw =>
            {
                if (!ReferenceEquals(tw.Obj, obj)) return false;
                foreach (string k in keys.Keys) tw.To.Remove(k);
                if (tw.To.Count > 0) return false;
                tw.Done.Resolve();
                return true;
            });
            var item = new TweenItem
            {
                Obj = obj,
                To = keys,
                Ms = Math.Max(0f, ms),
                Ease = Trial.Ease.OrOut(ease),
                T0 = _now + Math.Max(0f, delay),
                Done = new PromiseSource(),
            };
            _tweens.Add(item);
            return item.Done.Promise;
        }

        /// <summary>Stops every running tween of obj without resolving them (they never resolve).</summary>
        public void Kill(ITweenable obj) => _tweens.RemoveAll(tw => ReferenceEquals(tw.Obj, obj));

        // ---- runs and waits -------------------------------------------------------------------------

        /// <summary>UiTween.Run: fn(easedT, rawT) every frame for `ms`; fn(1, 1) last. ms 0 calls fn(1, 1) now.</summary>
        public Promise Run(float ms, EaseFn ease, Action<float, float> fn)
        {
            if (!_alive) return Promise.Never();
            ms = Math.Max(0f, ms);
            if (ms == 0f)
            {
                try { fn?.Invoke(1f, 1f); } catch (Exception e) { Microtasks.Report(e); }
                return Promise.Resolved();
            }
            var item = new RunItem { Ms = ms, Ease = Trial.Ease.OrOut(ease), Fn = fn, T0 = _now, Done = new PromiseSource() };
            _runs.Add(item);
            return item.Done.Promise;
        }

        /// <summary>A run that advances by the paced scale (the battle's floating numbers stay readable under fast-forward).</summary>
        public Promise RunPaced(float ms, EaseFn ease, Action<float, float> fn)
        {
            if (!_alive) return Promise.Never();
            ms = Math.Max(0f, ms);
            if (ms == 0f || _pacedScale == null) return Run(ms, ease, fn);
            var item = new RunItem { Ms = ms, Ease = Trial.Ease.OrOut(ease), Fn = fn, Elapsed = 0f, Done = new PromiseSource() };
            _paced.Add(item);
            return item.Done.Promise;
        }

        /// <summary>Resolves after `ms` of scene time (0 = an already resolved promise).</summary>
        public Promise Wait(float ms)
        {
            if (!_alive) return Promise.Never();
            ms = Math.Max(0f, ms);
            if (ms == 0f) return Promise.Resolved();
            var t = new TimerItem { At = _now + ms, Done = new PromiseSource() };
            _timers.Add(t);
            return t.Done.Promise;
        }

        /// <summary>Resolves after `ms` of real time (input timings: the refusal line's 3.5 s, long presses).</summary>
        public Promise WaitReal(float ms)
        {
            if (!_alive) return Promise.Never();
            var t = new TimerItem { At = _realNow + Math.Max(0f, ms), Done = new PromiseSource() };
            _realTimers.Add(t);
            return t.Done.Promise;
        }

        /// <summary>fn(dtScene, dtReal) every frame, after the frame's tweens and waits. Dispose to stop.</summary>
        public IDisposable OnFrame(Action<float, float> fn)
        {
            if (fn == null) throw new ArgumentNullException(nameof(fn));
            _frameFns.Add(fn);
            return new Unsubscribe(() => _frameFns.Remove(fn));
        }

        private sealed class Unsubscribe : IDisposable
        {
            private Action _a;
            public Unsubscribe(Action a) { _a = a; }
            public void Dispose() { _a?.Invoke(); _a = null; }
        }

        // ---- the frame ------------------------------------------------------------------------------

        /// <summary>
        /// One frame: real waits, then scene time (tweens, runs, waits, paced runs), then OnFrame callbacks, then
        /// the microtask queue (unless drain is false). Work started from inside a callback this frame is kept.
        /// </summary>
        public void Step(float dtRealMs, bool drain = true)
        {
            if (!_alive) return;
            if (dtRealMs < 0f) dtRealMs = 0f;
            float dtClamped = Math.Min(MaxStepMs, dtRealMs);
            var done = new List<PromiseSource>();

            // Real time and its waits (the base clock of the JS, which runs first).
            _realNow += dtRealMs;
            {
                List<TimerItem> cur = _realTimers;
                _realTimers = new List<TimerItem>();
                var left = new List<TimerItem>();
                foreach (TimerItem tm in cur)
                    if (_realNow >= tm.At) done.Add(tm.Done); else left.Add(tm);
                left.AddRange(_realTimers);
                _realTimers = left;
            }

            float scale = _scale != null ? _scale() : 1f;
            if (float.IsNaN(scale) || scale < 0f) scale = 0f;
            _lastScale = scale;
            float dtScene = (_clampScene ? dtClamped : dtRealMs) * scale;
            _now += dtScene;

            // Tweens
            {
                List<TweenItem> cur = _tweens;
                _tweens = new List<TweenItem>();
                var left = new List<TweenItem>();
                foreach (TweenItem tw in cur)
                {
                    if (tw.To.Count == 0) continue; // emptied by a newer tween this frame (already resolved)
                    if (_now < tw.T0) { left.Add(tw); continue; }
                    if (tw.From == null)
                    {
                        tw.From = new Dictionary<string, float>(StringComparer.Ordinal);
                        foreach (string k in tw.To.Keys) tw.From[k] = tw.Obj.GetTween(k);
                    }
                    float p = tw.Ms > 0f ? Math.Min(1f, (_now - tw.T0) / tw.Ms) : 1f;
                    float e = tw.Ease(p);
                    // Copy the keys: a setter may start a tween that edits tw.To.
                    var keys = new List<string>(tw.To.Keys);
                    foreach (string k in keys)
                    {
                        if (!tw.To.TryGetValue(k, out float to)) continue;
                        float from = tw.From.TryGetValue(k, out float f) ? f : tw.Obj.GetTween(k);
                        try { tw.Obj.SetTween(k, from + (to - from) * e); }
                        catch (Exception ex) { Microtasks.Report(ex); }
                    }
                    if (p >= 1f) done.Add(tw.Done); else left.Add(tw);
                }
                left.AddRange(_tweens);
                _tweens = left;
            }

            // Runs
            {
                List<RunItem> cur = _runs;
                _runs = new List<RunItem>();
                var left = new List<RunItem>();
                foreach (RunItem r in cur)
                {
                    float p = r.Ms > 0f ? Math.Min(1f, (_now - r.T0) / r.Ms) : 1f;
                    try { r.Fn?.Invoke(r.Ease(p), p); } catch (Exception ex) { Microtasks.Report(ex); }
                    if (p >= 1f) done.Add(r.Done); else left.Add(r);
                }
                left.AddRange(_runs);
                _runs = left;
            }

            // Scene waits
            {
                List<TimerItem> cur = _timers;
                _timers = new List<TimerItem>();
                var left = new List<TimerItem>();
                foreach (TimerItem tm in cur)
                    if (_now >= tm.At) done.Add(tm.Done); else left.Add(tm);
                left.AddRange(_timers);
                _timers = left;
            }

            // Paced runs
            if (_paced.Count > 0)
            {
                float paced = _pacedScale != null ? _pacedScale() : scale;
                if (float.IsNaN(paced) || paced < 0f) paced = 0f;
                float dtPaced = (_clampScene ? dtClamped : dtRealMs) * paced;
                List<RunItem> cur = _paced;
                _paced = new List<RunItem>();
                var left = new List<RunItem>();
                foreach (RunItem r in cur)
                {
                    r.Elapsed += dtPaced;
                    float p = r.Ms > 0f ? Math.Min(1f, r.Elapsed / r.Ms) : 1f;
                    try { r.Fn?.Invoke(r.Ease(p), p); } catch (Exception ex) { Microtasks.Report(ex); }
                    if (p >= 1f) done.Add(r.Done); else left.Add(r);
                }
                left.AddRange(_paced);
                _paced = left;
            }

            foreach (PromiseSource s in done) s.TryResolve();

            if (_frameFns.Count > 0)
            {
                var fns = _frameFns.ToArray();
                foreach (Action<float, float> fn in fns)
                {
                    if (!_alive) break;
                    if (!_frameFns.Contains(fn)) continue;
                    try { fn(_clampScene ? dtScene : dtClamped * scale, dtClamped); }
                    catch (Exception ex) { Microtasks.Report(ex); }
                }
            }

            if (drain) Microtasks.Drain();
        }

        /// <summary>
        /// Stops the clock. resolvePending true resolves every running tween and wait (stage.js clock.stop());
        /// false drops them so they never resolve (the battle clock's destroy: a destroyed scene's flow stops).
        /// Later calls return promises that never settle.
        /// </summary>
        public void Stop(bool resolvePending)
        {
            if (!_alive) return;
            _alive = false;
            if (resolvePending)
            {
                foreach (TweenItem t in _tweens) t.Done.TryResolve();
                foreach (RunItem r in _runs) r.Done.TryResolve();
                foreach (RunItem r in _paced) r.Done.TryResolve();
                foreach (TimerItem t in _timers) t.Done.TryResolve();
                foreach (TimerItem t in _realTimers) t.Done.TryResolve();
            }
            _tweens.Clear();
            _runs.Clear();
            _paced.Clear();
            _timers.Clear();
            _realTimers.Clear();
            _frameFns.Clear();
        }
    }
}
