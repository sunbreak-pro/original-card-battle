// The small animation helpers the scenes share, on the scene clock (Trial.Async's TrialClock):
// - battle/hud/util.js: PopScale, Pop, Shake, ScaleTo (Unity's UiTween shapes, on the CSS `scale` / `translate`
//   properties, so left / top and the transform stay untouched);
// - meta/util.js: RiseIn (a staggered rise-in), RollNumber (a number that rolls to its new value).
// The maths and the toFixed() rounding are the JS's.
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace Trial.Kit
{
    public static class Tweens
    {
        // js: scenes/battle/hud/util.js:12
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // js: scenes/battle/hud/util.js:13
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;

        /// <summary>Number.prototype.toFixed(digits) read back as a number (round half away from zero).</summary>
        public static float Fixed(float x, int digits) => (float)Math.Round((double)x, digits, MidpointRounding.AwayFromZero);

        // js: scenes/battle/hud/util.js:16
        /// <summary>UiTween.Pop's curve: scale 1.2 → 0.8 → 1 over the (eased) t.</summary>
        public static float PopScale(float t) => t < 0.5f ? Lerp(1.2f, 0.8f, t * 2f) : Lerp(0.8f, 1f, (t - 0.5f) * 2f);

        // js: scenes/battle/hud/util.js:21
        /// <summary>UiTween.Pop(rt, ms) on the scene clock (Ease.Out), on the CSS `scale` property; cleared at the end.</summary>
        public static Promise Pop(TrialClock clock, UiNode el, float ms)
        {
            if (!el) return clock.Wait(ms);
            return clock.Run(ms, Ease.Out, (t, _) => { if (el) el.PropScale = Fixed(PopScale(t), 4); })
                .Then(() => { if (el) el.PropScale = 1f; });
        }

        // js: scenes/battle/hud/util.js:27
        /// <summary>UiTween.Shake(rt, amplitude, cycles, ms): a horizontal shake that dies out (Ease.Linear), on the CSS `translate` property.</summary>
        public static Promise Shake(TrialClock clock, UiNode el, float amplitude, float cycles, float ms)
        {
            if (!el) return clock.Wait(ms);
            return clock.Run(ms, Ease.Linear, (t, _) =>
            {
                if (!el) return;
                float x = (float)(Math.Sin(t * Math.PI * 2.0 * cycles) * amplitude * (1.0 - t));
                el.PropTranslateX = Fixed(x, 2);
                el.PropTranslateY = 0f;
            }).Then(() => { if (el) { el.PropTranslateX = 0f; el.PropTranslateY = 0f; } });
        }

        // js: scenes/battle/hud/util.js:36
        /// <summary>UiTween.Scale(rt, from, to, ms, ease) on the CSS `scale` property (ease null = out).</summary>
        public static Promise ScaleTo(TrialClock clock, UiNode el, float from, float to, float ms, EaseFn ease = null)
        {
            if (!el) return clock.Wait(ms);
            return clock.Run(ms, ease ?? Ease.Out, (t, _) => { if (el) el.PropScale = Fixed(Lerp(from, to, t), 4); })
                .Then(() => { if (el) el.PropScale = to; });
        }

        // js: scenes/meta/util.js:104
        /// <summary>
        /// riseIn(clock, els, {delay, step, dy, ms}): each element starts at opacity 0, translateY dy and tweens to 1 / 0
        /// (out) after delay + i·step; its transform is cleared at the end. Resolves when all have risen.
        /// </summary>
        public static Promise RiseIn(TrialClock clock, IReadOnlyList<UiNode> els, float delay = 0f, float step = 70f, float dy = 18f, float ms = 420f)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            var all = new List<Promise>();
            if (els != null)
            {
                for (int i = 0; i < els.Count; i++)
                {
                    UiNode el = els[i];
                    Styler p = Styler.Of(el, o: 0f, y: dy);
                    all.Add(clock.Tween(p, new[] { ("o", 1f), ("y", 0f) }, ms, Ease.Out, delay + i * step)
                        .Then(() => { if (el) el.ResetTransform(); }));
                }
            }
            return Promise.All(all);
        }

        /// <summary>RiseIn over RectTransforms (each gets a UiNode).</summary>
        public static Promise RiseIn(TrialClock clock, IReadOnlyList<RectTransform> els, float delay = 0f, float step = 70f, float dy = 18f, float ms = 420f)
        {
            var nodes = new List<UiNode>();
            if (els != null) foreach (RectTransform rt in els) nodes.Add(UiNode.Of(rt));
            return RiseIn(clock, nodes, delay, step, dy, ms);
        }

        /// <summary>JS Math.round: floor(x + 0.5).</summary>
        public static double JsRound(double x) => Math.Floor(x + 0.5);
    }

    /// <summary>meta/util.js rollNumber: a number that rolls to its new value (mono digits keep their width).</summary>
    public sealed class RollNumber : ITweenable
    {
        private readonly TrialClock _clock;
        private readonly Action<string> _write;
        private float _v;

        private RollNumber(TrialClock clock, Action<string> write, float initial)
        {
            _clock = clock;
            _write = write;
            _v = initial;
            Show();
        }

        // js: scenes/meta/util.js:51
        /// <summary>rollNumber(clock, el, initial): shows Math.round(v) in el's text (el.textContent: a kit label re-lays out, as the browser does).</summary>
        public static RollNumber Of(TrialClock clock, TMP_Text el, float initial = 0f)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            return new RollNumber(clock, s =>
            {
                if (!el) return;
                if (el.GetComponent<LabelState>()) Label.SetText(el, s);
                else el.text = s;
            }, initial);
        }

        /// <summary>The same with a writer of the shown text.</summary>
        public static RollNumber Of(TrialClock clock, Action<string> write, float initial = 0f)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            return new RollNumber(clock, write, initial);
        }

        /// <summary>The value (not rounded).</summary>
        public float V
        {
            get => _v;
            set { _v = value; Show(); }
        }

        private string _shown;

        private void Show()
        {
            long n = (long)Tweens.JsRound(_v);
            string s = n.ToString(CultureInfo.InvariantCulture);
            // The same digits again change nothing on screen (the JS rewrites them every frame); skip the relayout.
            if (s == _shown) return;
            _shown = s;
            _write?.Invoke(s);
        }

        /// <summary>to(v, ms = 260): rolls to v (out); a value already shown just snaps.</summary>
        public Promise To(float v, float ms = 260f)
        {
            if (Tweens.JsRound(_v) == v) { _v = v; Show(); return Promise.Resolved(); }
            return _clock.Tween(this, "v", v, ms, Ease.Out);
        }

        /// <summary>set(v): shows v at once (always writes the text, as the JS does).</summary>
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
}
