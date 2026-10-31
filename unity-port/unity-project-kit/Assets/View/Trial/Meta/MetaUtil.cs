// Port of scenes/meta/util.js: the small presentation helpers the meta screens share — the scene root, sound calls
// that never throw, the scene clock (reduced motion), the staggered rise-in, token reads, the canvas colour helpers,
// the DS icon and modalSwallow. What util.js has that the kit already holds is reused, not copied:
//   styler(el, init)          → Trial.Kit.Styler.Of(el, o, x, y, s, r)      (Kit/Styler.cs; keys "o" "x" "y" "s" "r")
//   rollNumber(clock, el, n)  → MetaUtil.RollNumber (its tween on the scene clock, so reduced motion shortens it as the
//                               JS's clock.tween override does) / Trial.Kit.RollNumber.Of (the kit clock: never shortened)
//   riseIn(clock, els, opts)  → MetaUtil.RiseIn (the reduced-motion clock) / Trial.Kit.Tweens.RiseIn
//   modalSwallow(e)           → Trial.Kit.KeyStack.ModalSwallow (forwarded below)
//   applyWashi(el)            → WashiTile.ApplyWashi (WashiTile.cs); the whole .ocbg-meta-washi look is MetaStyle.Washi
//   pushKeys(handler)         → Trial.Kit.KeyStack.Push (stage.js)
//   EASE                      → Trial.Ease (Out / In / InOut / Linear / Smooth; Ease.Of("out"))
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using Trial.Audio;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace Trial.Meta
{
    /// <summary>soundOf(ctx)'s result: the audio (or null) and a play that never throws.</summary>
    public sealed class MetaSound
    {
        public MetaSound(ITrialAudio audio) { Audio = audio; }

        /// <summary>sound.audio: ctx.audio, or null.</summary>
        public ITrialAudio Audio { get; }

        // js: scenes/meta/util.js:18
        /// <summary>play(name, delayMs, mult): ctx.audio.play(name, delayMs || 0, mult); a silent page still runs.</summary>
        public void Play(string name, double delayMs = 0, double? mult = null)
        {
            try
            {
                Audio?.Play(name, delayMs, mult);
            }
            catch (Exception)
            {
                // silent
            }
        }
    }

    /// <summary>
    /// The scene clock (util.js sceneClock over stage.js createClock): a TrialClock stepped by the stage's ClockDriver in
    /// real time (dt to OnFrame clamped to 50 ms), whose tweens finish at once when motion is reduced (ms ≤ 1, no delay).
    /// It converts to TrialClock, so the kit's helpers (RollNumber.Of, Tweens.RiseIn, CssStates.Clock) take it.
    /// </summary>
    public sealed class SceneClock
    {
        private readonly bool _reduced;

        internal SceneClock(TrialClock inner, bool reduced)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _reduced = reduced;
        }

        /// <summary>The kit clock underneath.</summary>
        public TrialClock Inner { get; }

        /// <summary>reducedMotion() when the clock was made: every tween lasts at most 1 ms and starts at once.</summary>
        public bool Reduced => _reduced;

        public static implicit operator TrialClock(SceneClock c) => c?.Inner;

        /// <summary>clock.now: scene time in ms since the clock started (the page time is MetaUtil.PageNow).</summary>
        public float Now => Inner.Now;

        public bool Alive => Inner.Alive;

        // js: scenes/common/stage.js:94 (tween), scenes/meta/util.js:96 (the reduced-motion override)
        /// <summary>clock.tween(obj, { k: v, … }, ms, ease, delay): from-values read when the delay ends; replaces the keys in older tweens of obj.</summary>
        public Promise Tween(ITweenable obj, (string Key, float To)[] to, float ms, EaseFn ease = null, float delay = 0f)
        {
            if (_reduced) return Inner.Tween(obj, to, Math.Min(ms, 1f), ease, 0f);
            return Inner.Tween(obj, to, ms, ease, delay);
        }

        /// <summary>clock.tween(obj, { key: to }, ms, ease, delay).</summary>
        public Promise Tween(ITweenable obj, string key, float to, float ms, EaseFn ease = null, float delay = 0f) =>
            Tween(obj, new[] { (key, to) }, ms, ease, delay);

        /// <summary>A run (fn(easedT, t) every frame), shortened like a tween when motion is reduced.</summary>
        public Promise Run(float ms, EaseFn ease, Action<float, float> fn) => Inner.Run(_reduced ? Math.Min(ms, 1f) : ms, ease, fn);

        // js: scenes/common/stage.js:106
        /// <summary>clock.wait(ms): a timer in scene time (not shortened by reduced motion, as in the JS).</summary>
        public Promise Wait(float ms) => Inner.Wait(ms);

        /// <summary>A wait in real time (input holds).</summary>
        public Promise WaitReal(float ms) => Inner.WaitReal(ms);

        /// <summary>Stops every tween of obj without resolving it.</summary>
        public void Kill(ITweenable obj) => Inner.Kill(obj);

        /// <summary>
        /// onFrame(dt, now) every frame after the frame's tweens and waits: dt real ms clamped to 50, now the page time in
        /// ms (MetaUtil.PageNow, the rAF timestamp of the JS). Dispose to stop.
        /// </summary>
        public IDisposable OnFrame(Action<float, double> fn)
        {
            if (fn == null) throw new ArgumentNullException(nameof(fn));
            return Inner.OnFrame((dtScene, dtReal) => fn(dtReal, MetaUtil.PageNow));
        }

        // js: scenes/common/stage.js:107
        /// <summary>clock.stop(): every pending tween and wait resolves, and the clock stops for good.</summary>
        public void Stop() => Inner.Stop(resolvePending: true);
    }

    /// <summary>rollNumber's result (util.js:51-64): to(v, ms = 260) rolls to v (out), set(v) shows v at once.</summary>
    public sealed class MetaRollNumber : ITweenable
    {
        private readonly SceneClock _clock;
        private readonly TMP_Text _el;
        private float _v;

        internal MetaRollNumber(SceneClock clock, TMP_Text el, float initial)
        {
            _clock = clock;
            _el = el;
            _v = initial;
            Show();
        }

        private string _shown;

        // js: scenes/meta/util.js:53 show(): el.textContent = String(Math.round(n.v)). The same digits again change
        // nothing on screen (the JS rewrites them every frame), so the relayout is skipped.
        private void Show()
        {
            if (!_el) return;
            string s = MetaUtil.JsRound(_v).ToString(CultureInfo.InvariantCulture);
            if (s == _shown) return;
            _shown = s;
            MetaUtil.SetText(_el, s);
        }

        /// <summary>The value (not rounded).</summary>
        public float V
        {
            get => _v;
            set { _v = value; Show(); }
        }

        // js: scenes/meta/util.js:58
        /// <summary>to(v, ms = 260): a value already shown just snaps; else the scene clock tweens it (out).</summary>
        public Promise To(float v, float ms = 260f)
        {
            if (MetaUtil.JsRound(_v) == v) { _v = v; Show(); return Promise.Resolved(); }
            return _clock.Tween(this, "v", v, ms, Ease.Out);
        }

        // js: scenes/meta/util.js:62
        /// <summary>set(v): shows v at once.</summary>
        public void Set(float v)
        {
            _v = v;
            _shown = null;
            Show();
        }

        public float GetTween(string key) => key == "v" ? _v : 0f;

        public void SetTween(string key, float value)
        {
            if (key == "v") V = value;
        }
    }

    public static class MetaUtil
    {
        /// <summary>The stage size (stage.js W / H).</summary>
        public const float W = 1920f, H = 1080f;

        /// <summary>
        /// The page time in ms (the rAF timestamp the JS backdrops and breaths read): Unity's unscaled time since start.
        /// It is not reset per scene, so a breath runs on across scenes as it does in the page.
        /// </summary>
        public static double PageNow => Time.unscaledTimeAsDouble * 1000.0;

        // js: scenes/meta/util.js:7 ocb() — the C# design system is the static Trial.Kit.Ocb; there is nothing to fetch.

        // js: scenes/meta/util.js:14
        /// <summary>soundOf(ctx): ctx.audio, with every call guarded so a silent page still runs.</summary>
        public static MetaSound SoundOf(MetaCtx ctx) => new MetaSound(ctx?.Audio);

        /// <summary>soundOf for an audio given directly.</summary>
        public static MetaSound SoundOf(ITrialAudio audio) => new MetaSound(audio);

        // js: scenes/meta/util.js:25
        /// <summary>
        /// sceneRoot(host, name): the scene's root element (.ocbg-meta .ocbg-meta-&lt;name&gt;: absolute, inset 0, overflow
        /// hidden, body font, ink) filling the 1920×1080 host. The stage root already clips at 1920×1080 (its RectMask2D),
        /// so overflow: hidden needs nothing more here.
        /// </summary>
        public static RectTransform SceneRoot(RectTransform host, string name)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            RectTransform el = Ui.Div(host, "ocbg-meta ocbg-meta-" + name, 0f, 0f, W, H);
            UiNode.Of(el);
            return el;
        }

        // js: scenes/meta/util.js:67
        /// <summary>token(el, name, fallback): a token's CSS text (tokens.css, the dusk theme), else the fallback.</summary>
        public static string Token(string name, string fallback) =>
            DsTokens.TryResolve(name, DsTheme.Dusk, out string css) && !string.IsNullOrEmpty(css) ? css.Trim() : fallback;

        // js: scenes/meta/util.js:75
        /// <summary>hexRgb('#rrggbb') → [r, g, b] (0〜255); anything else is white.</summary>
        public static int[] HexRgb(string hex)
        {
            string s = (hex ?? "").Replace("#", "");
            if (s.Length != 6) return new[] { 255, 255, 255 };
            int Part(int at) => int.TryParse(s.Substring(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v) ? v : 0;
            return new[] { Part(0), Part(2), Part(4) };
        }

        // js: scenes/meta/util.js:82
        /// <summary>rgbaCss(c, mul): a bridge colour (0〜1 channels) as `rgba(r,g,b,a)`, its alpha × mul clamped to 0〜1 (3 decimals).</summary>
        public static string RgbaCss(Backdrop.Rgba c, float mul = 1f)
        {
            int r = JsRound(c.R * 255.0), g = JsRound(c.G * 255.0), b = JsRound(c.B * 255.0);
            double a = Math.Max(0.0, Math.Min(1.0, (double)c.A * mul));
            return "rgba(" + r.ToString(CultureInfo.InvariantCulture) + "," + g.ToString(CultureInfo.InvariantCulture) + ","
                + b.ToString(CultureInfo.InvariantCulture) + "," + Math.Round(a, 3, MidpointRounding.AwayFromZero).ToString("0.000", CultureInfo.InvariantCulture) + ")";
        }

        /// <summary>
        /// rgbaCss as a kit colour (what the canvas fill gets): the channels rounded to bytes and the alpha × mul clamped
        /// and cut to 3 decimals, as the CSS string carries them.
        /// </summary>
        public static Rgba RgbaOf(Backdrop.Rgba c, float mul = 1f)
        {
            double a = Math.Max(0.0, Math.Min(1.0, (double)c.A * mul));
            return Rgba.Bytes(JsRound(c.R * 255.0), JsRound(c.G * 255.0), JsRound(c.B * 255.0), (float)Math.Round(a, 3, MidpointRounding.AwayFromZero));
        }

        /// <summary>A kit colour as the bridge's (for BackdropSpec maths on a token).</summary>
        public static Backdrop.Rgba BridgeRgba(Rgba c) => new Backdrop.Rgba(c.R, c.G, c.B, c.A);

        // js: scenes/meta/util.js:88
        /// <summary>reducedMotion(): prefers-reduced-motion (Unity has no OS signal: the kit's CssTime.ReducedMotion switch).</summary>
        public static bool ReducedMotion() => CssTime.ReducedMotion;

        // js: scenes/meta/util.js:93
        /// <summary>
        /// sceneClock(onFrame): a clock on the stage's driver (the live stage's, or the kit's own without one) whose tweens
        /// finish at once when motion is reduced. onFrame(dt, now) as SceneClock.OnFrame.
        /// </summary>
        public static SceneClock SceneClock(Action<float, double> onFrame = null, TrialStage stage = null)
        {
            ClockDriver driver = stage ? stage.Clocks : null;
            if (!driver) driver = CssTime.Driver;
            var clock = new SceneClock(driver.CreateClock(), ReducedMotion());
            if (onFrame != null) clock.OnFrame(onFrame);
            return clock;
        }

        // js: scenes/meta/util.js:101 (export { EASE })
        /// <summary>EASE[name]: 'out' 'in' 'inout' 'linear' 'smooth' (anything else is out, as clock.tween reads it).</summary>
        public static EaseFn EaseOf(string name) => Ease.Of(name);

        // js: scenes/meta/util.js:104
        /// <summary>
        /// riseIn(clock, els, {delay = 0, step = 70, dy = 18, ms = 420}): each element from opacity 0 and translateY dy to
        /// 1 / 0 (out) after delay + i·step; its transform is cleared at the end. Resolves when all have risen.
        /// </summary>
        public static Promise RiseIn(SceneClock clock, IReadOnlyList<RectTransform> els, float delay = 0f, float step = 70f, float dy = 18f, float ms = 420f)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            var all = new List<Promise>();
            if (els != null)
            {
                for (int i = 0; i < els.Count; i++)
                {
                    RectTransform rt = els[i];
                    if (!rt) continue;
                    UiNode el = UiNode.Of(rt);
                    global::Trial.Kit.Styler p = global::Trial.Kit.Styler.Of(el, o: 0f, y: dy);
                    all.Add(clock.Tween(p, new[] { ("o", 1f), ("y", 0f) }, ms, Ease.Out, delay + i * step)
                        .Then(() => { if (el) el.ResetTransform(); }));
                }
            }
            return Promise.All(all);
        }

        // js: scenes/meta/util.js:51
        /// <summary>
        /// rollNumber(clock, el, initial): a number that rolls to its new value (mono digits keep their width). Its tween
        /// runs on the scene clock, so reduced motion cuts it to 1 ms as the JS's sceneClock does.
        /// </summary>
        public static MetaRollNumber RollNumber(SceneClock clock, TMP_Text el, float initial = 0f) =>
            new MetaRollNumber(clock ?? throw new ArgumentNullException(nameof(clock)), el, initial);

        // js: scenes/meta/util.js:112
        /// <summary>
        /// icon(name, size, color, opts): a DS icon (Ocb.Icon: 24-unit paths, stroke 2.2, round caps) at (0, 0) of parent;
        /// color null = --ink (currentColor in the meta screens).
        /// </summary>
        public static PathGraphic Icon(RectTransform parent, string name, float size = 24f, Rgba? color = null, Rgba? fill = null,
            float? fillOpacity = null, bool dashed = false, float? width = null) =>
            Ocb.Icon(parent, name, size, color, fill, fillOpacity, dashed, width);

        // js: scenes/meta/util.js:117
        /// <summary>modalSwallow(e): what a modal overlay keeps from the screen under it (characters, arrows, Enter, Tab, PageUp / PageDown).</summary>
        public static bool ModalSwallow(KeyEvent e) => KeyStack.ModalSwallow(e);

        // js: scenes/meta/util.js:127
        /// <summary>applyWashi(el): the fibre tile laid over a washi element (WashiTile.ApplyWashi).</summary>
        public static RawImage ApplyWashi(RectTransform el, float w, float h, Insets border = default) =>
            WashiTile.ApplyWashi(el, w, h, border);

        /// <summary>JS Math.round: halves go up (toward +∞).</summary>
        public static int JsRound(double v) => (int)Math.Floor(v + 0.5);

        /// <summary>A label's text set the kit's way (relayout included).</summary>
        public static void SetText(TMP_Text t, string text)
        {
            if (t) Label.SetText(t, text ?? "");
        }
    }
}
