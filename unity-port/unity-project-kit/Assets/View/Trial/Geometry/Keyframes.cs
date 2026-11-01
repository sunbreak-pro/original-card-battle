// CSS @keyframes, the animation shorthand and transitions (kit.md §3.3, §3.4, §4.11).
//
// The 20 @keyframes of the trial are kept as their CSS text verbatim (file:line beside each) and parsed with the
// kit's CSS parser, so a reviewer can diff them against the stylesheets. Sampling follows CSS Animations / Web
// Animations: the timing function applies to each keyframe interval (not the whole run); every property is its
// own track over the keyframes that set it; the iteration, direction and fill rules give the directed progress.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Trial.Geometry
{
    public enum AnimDirection { Normal, Reverse, Alternate, AlternateReverse }

    public enum AnimFill { None, Forwards, Backwards, Both }

    /// <summary>
    /// Animated values at one moment; null = the animation does not touch the property. The transform is given as
    /// the composite translate (px and % of the element's own box), rotation (CSS degrees, clockwise) and scale.
    /// </summary>
    public struct AnimProps
    {
        public float? Opacity;
        public float? Tx, Ty;
        public float? TxPercent, TyPercent;
        public float? Sx, Sy;
        public float? Rot;
        /// <summary>box-shadow layers, CSS order.</summary>
        public BoxShadow[] BoxShadows;
        /// <summary>text-shadow layers, CSS order.</summary>
        public TextShadow[] TextShadows;
        public float? DashOffset;
        /// <summary>The interpolated transform list itself (for a full matrix with ToAffine).</summary>
        public TransformSpec Transform;

        public bool HasTransform => Transform != null;
    }

    public sealed class CssKeyframes
    {
        /// <summary>One property's keyframes: offsets (0..1, sorted) and values.</summary>
        private sealed class Track
        {
            public readonly List<float> At = new List<float>();
            public readonly List<object> Values = new List<object>();
        }

        public readonly string Name;
        private readonly Dictionary<string, Track> _tracks = new Dictionary<string, Track>(StringComparer.Ordinal);

        private CssKeyframes(string name) { Name = name; }

        /// <summary>The properties the keyframes animate.</summary>
        public IEnumerable<string> Properties => _tracks.Keys;

        /// <summary>The keyframe offsets a property is set at (0..1).</summary>
        public IReadOnlyList<float> OffsetsOf(string property) => _tracks.TryGetValue(property, out Track t) ? t.At : (IReadOnlyList<float>)Array.Empty<float>();

        // ---- the 20 @keyframes of §3.3, verbatim ------------------------------------------------------------------

        private static readonly Dictionary<string, string> Source = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // vendor/ds/bundle.css:136
            ["ocb-pip-lit"] = "@keyframes ocb-pip-lit { 0% { box-shadow: 0 0 0 0 rgba(255,216,154,.9); transform: scale(1.35); } 100% { box-shadow: 0 0 0 8px rgba(255,216,154,0); transform: scale(1); } }",
            // vendor/ds/bundle.css:227
            ["ocb-breathe"] = "@keyframes ocb-breathe { 0%,100% { opacity: 1; } 50% { opacity: .68; } }",
            // vendor/ds/bundle.css:229
            ["ocb-drift"] = "@keyframes ocb-drift { from { transform: translate(0,0); opacity: 0; } 15% { opacity: 1; } 85% { opacity: 1; } to { transform: translate(var(--dx), var(--dy)); opacity: 0; } }",
            // src/scenes/explore/explore.css:221
            ["ocbg-explore-fade"] = "@keyframes ocbg-explore-fade { from { opacity: 0; } to { opacity: 1; } }",
            // explore.css:222
            ["ocbg-explore-fadeout"] = "@keyframes ocbg-explore-fadeout { from { opacity: 1; } to { opacity: 0; } }",
            // explore.css:34
            ["ocbg-explore-roll"] = "@keyframes ocbg-explore-roll { 0% { text-shadow: 0 0 0 transparent; } 25% { text-shadow: 0 0 12px color-mix(in srgb, var(--ink) 70%, transparent); } 100% { text-shadow: 0 0 0 transparent; } }",
            // explore.css:46
            ["ocbg-explore-pulse"] = "@keyframes ocbg-explore-pulse { 0%, 100% { opacity: 1; } 50% { opacity: .45; } }",
            // explore.css:82
            ["ocbg-explore-flow"] = "@keyframes ocbg-explore-flow { from { stroke-dashoffset: 24; } to { stroke-dashoffset: 0; } }",
            // explore.css:92
            ["ocbg-explore-beckon"] = "@keyframes ocbg-explore-beckon { 0%, 100% { box-shadow: var(--shadow-card), 0 0 0 0 color-mix(in srgb, var(--amber) 0%, transparent); } 50% { box-shadow: var(--shadow-card), 0 0 18px 2px color-mix(in srgb, var(--amber) 38%, transparent); } }",
            // explore.css:102
            ["ocbg-explore-ripple"] = "@keyframes ocbg-explore-ripple { 0% { opacity: .9; transform: scale(1); } 100% { opacity: 0; transform: scale(1.55); } }",
            // explore.css:109
            ["ocbg-explore-breathe"] = "@keyframes ocbg-explore-breathe { 0%, 100% { opacity: .75; transform: scale(.94); } 50% { opacity: 1; transform: scale(1.06); } }",
            // explore.css:112
            ["ocbg-explore-land"] = "@keyframes ocbg-explore-land { 0% { transform: scale(1.18, .84); } 60% { transform: scale(.96, 1.05); } 100% { transform: none; } }",
            // explore.css:225
            ["ocbg-explore-plate-in"] = "@keyframes ocbg-explore-plate-in { from { opacity: 0; transform: translateY(18px); } to { opacity: 1; transform: none; } }",
            // explore.css:226
            ["ocbg-explore-plate-out"] = "@keyframes ocbg-explore-plate-out { from { opacity: 1; transform: none; } to { opacity: 0; transform: translateY(10px); } }",
            // explore.css:291
            ["ocbg-explore-deal"] = "@keyframes ocbg-explore-deal { from { opacity: 0; transform: translateY(36px) rotate(-4deg); } to { opacity: 1; transform: none; } }",
            // explore.css:361
            ["ocbg-explore-window"] = "@keyframes ocbg-explore-window { from { transform: scale(1.02) translateY(0); } to { transform: scale(1.08) translateY(-6px); } }",
            // explore.css:378
            ["ocbg-explore-drawer"] = "@keyframes ocbg-explore-drawer { from { transform: translateX(100%); } to { transform: none; } }",
            // explore.css:379
            ["ocbg-explore-drawer-out"] = "@keyframes ocbg-explore-drawer-out { from { transform: none; } to { transform: translateX(100%); } }",
            // src/scenes/meta/meta.css:129
            ["ocbg-meta-stamp"] = "@keyframes ocbg-meta-stamp { 0% { box-shadow: 0 0 0 0 color-mix(in srgb, var(--amber) 70%, transparent); } 100% { box-shadow: 0 0 0 22px color-mix(in srgb, var(--amber) 0%, transparent); } }",
            // meta.css:248
            ["ocbg-meta-pulse"] = "@keyframes ocbg-meta-pulse { 0% { box-shadow: 0 0 0 0 color-mix(in srgb, var(--amber) 60%, transparent); } 100% { box-shadow: 0 0 0 14px color-mix(in srgb, var(--amber) 0%, transparent); } }",
        };

        /// <summary>The names of the built-in @keyframes (§3.3).</summary>
        public static IEnumerable<string> Names => Source.Keys;

        /// <summary>The @keyframes text as the stylesheet writes it.</summary>
        public static string SourceOf(string name) => Source.TryGetValue(name, out string s) ? s : null;

        private static readonly Dictionary<string, CssKeyframes> Cache = new Dictionary<string, CssKeyframes>(StringComparer.Ordinal);

        /// <summary>A built-in @keyframes by name (null when unknown). With vars, var() inside the frames resolves against them (not cached).</summary>
        public static CssKeyframes Get(string name, DsTheme theme = DsTheme.Dusk, CssVars vars = null)
        {
            if (name == null || !Source.TryGetValue(name, out string text)) return null;
            if (vars != null) return Parse(text, theme, vars);
            string key = name + "|" + (int)theme;
            lock (Cache)
            {
                if (Cache.TryGetValue(key, out CssKeyframes k)) return k;
                k = Parse(text, theme, null);
                Cache[key] = k;
                return k;
            }
        }

        /// <summary>Parses "@keyframes name { 0% { … } 50%, 100% { … } }" (or just the block body).</summary>
        public static CssKeyframes Parse(string css, DsTheme theme = DsTheme.Dusk, CssVars vars = null)
        {
            string s = css.Trim();
            string name = "";
            if (s.StartsWith("@keyframes", StringComparison.Ordinal))
            {
                int brace = s.IndexOf('{');
                name = s.Substring(10, brace - 10).Trim();
                int close = s.LastIndexOf('}');
                s = s.Substring(brace + 1, close - brace - 1);
            }
            var kf = new CssKeyframes(name);
            int i = 0;
            while (i < s.Length)
            {
                int open = s.IndexOf('{', i);
                if (open < 0) break;
                int close = s.IndexOf('}', open);
                if (close < 0) break;
                string selector = s.Substring(i, open - i).Trim();
                string body = s.Substring(open + 1, close - open - 1);
                i = close + 1;
                var offsets = new List<float>();
                foreach (string sel in CssLex.SplitTopLevel(selector, ','))
                {
                    string t = sel.Trim().ToLowerInvariant();
                    if (t == "from") offsets.Add(0f);
                    else if (t == "to") offsets.Add(1f);
                    else if (CssLex.TryNumber(t, out float v, out string unit) && unit == "%") offsets.Add(v / 100f);
                }
                foreach ((string prop, string value) in CssLex.Declarations(body))
                {
                    string resolved = value;
                    if (value.IndexOf("var(", StringComparison.Ordinal) >= 0 && !CssLex.TrySubstitute(value, vars, theme, null, out resolved)) continue;
                    object parsed = ParseValue(prop, resolved, theme, vars);
                    if (parsed == null) continue;
                    foreach (float at in offsets) kf.Add(prop, at, parsed);
                }
            }
            return kf;
        }

        private static object ParseValue(string prop, string value, DsTheme theme, CssVars vars)
        {
            switch (prop)
            {
                case "opacity":
                case "stroke-dashoffset":
                    return CssLex.TryNumber(value.Trim(), out float v, out _) ? (object)v : null;
                case "transform":
                    return TransformSpec.TryParse(value, out TransformSpec tf) ? tf : null;
                case "box-shadow":
                    return Css.TryShadows(value, theme, vars, out List<BoxShadow> bs) ? bs.ToArray() : null;
                case "text-shadow":
                    return Css.TryTextShadows(value, theme, vars, out List<TextShadow> ts) ? ts.ToArray() : null;
                default:
                    return null;
            }
        }

        private void Add(string prop, float at, object value)
        {
            if (!_tracks.TryGetValue(prop, out Track t)) _tracks[prop] = t = new Track();
            int idx = t.At.Count;
            while (idx > 0 && t.At[idx - 1] > at) idx--;
            t.At.Insert(idx, at);
            t.Values.Insert(idx, value);
        }

        /// <summary>
        /// The values at a progress 0..1 with the timing function applied inside each keyframe interval of each
        /// property's own track. A track without a 0 % / 100 % keyframe holds its first / last value.
        /// </summary>
        public AnimProps Sample(float progress, EaseFn segmentEase)
        {
            var props = new AnimProps();
            EaseFn ease = segmentEase ?? Ease.Css;
            foreach (KeyValuePair<string, Track> kv in _tracks)
            {
                Track t = kv.Value;
                object val = SampleTrack(t, progress, ease);
                switch (kv.Key)
                {
                    case "opacity": props.Opacity = (float)val; break;
                    case "stroke-dashoffset": props.DashOffset = (float)val; break;
                    // copies: a held keyframe value is the cached frames' own array
                    case "box-shadow": props.BoxShadows = (BoxShadow[])((BoxShadow[])val).Clone(); break;
                    case "text-shadow": props.TextShadows = (TextShadow[])((TextShadow[])val).Clone(); break;
                    case "transform":
                        var tf = (TransformSpec)val;
                        props.Transform = tf;
                        TransformParts parts = tf.Compose();
                        props.Tx = parts.Tx; props.Ty = parts.Ty;
                        props.TxPercent = parts.TxPercent; props.TyPercent = parts.TyPercent;
                        props.Sx = parts.Sx; props.Sy = parts.Sy; props.Rot = parts.Rot;
                        break;
                }
            }
            return props;
        }

        private static object SampleTrack(Track t, float p, EaseFn ease)
        {
            int n = t.At.Count;
            if (p <= t.At[0] || n == 1) return t.Values[0];
            if (p >= t.At[n - 1]) return t.Values[n - 1];
            int i = 0;
            while (i + 1 < n && t.At[i + 1] <= p) i++;
            float span = t.At[i + 1] - t.At[i];
            float u = span > 0f ? (p - t.At[i]) / span : 1f;
            return Interpolate(t.Values[i], t.Values[i + 1], ease(u));
        }

        /// <summary>CSS interpolation of one property's values.</summary>
        internal static object Interpolate(object a, object b, float u)
        {
            switch (a)
            {
                case float fa: return fa + ((float)b - fa) * u;
                case TransformSpec ta: return TransformSpec.Interpolate(ta, (TransformSpec)b, u);
                case BoxShadow[] sa: return LerpShadows(sa, (BoxShadow[])b, u);
                case TextShadow[] xa: return LerpTextShadows(xa, (TextShadow[])b, u);
                default: return u < 0.5f ? a : b;
            }
        }

        /// <summary>Shadow lists pair layer by layer; the shorter list is padded with transparent zero shadows.</summary>
        public static BoxShadow[] LerpShadows(BoxShadow[] a, BoxShadow[] b, float u)
        {
            int n = Math.Max(a.Length, b.Length);
            var o = new BoxShadow[n];
            for (int i = 0; i < n; i++)
            {
                BoxShadow x = i < a.Length ? a[i] : BoxShadow.Zero(b[i].Inset);
                BoxShadow y = i < b.Length ? b[i] : BoxShadow.Zero(a[i].Inset);
                if (x.Inset != y.Inset) return u < 0.5f ? a : b;
                o[i] = BoxShadow.Lerp(x, y, u);
            }
            return o;
        }

        public static TextShadow[] LerpTextShadows(TextShadow[] a, TextShadow[] b, float u)
        {
            int n = Math.Max(a.Length, b.Length);
            var o = new TextShadow[n];
            for (int i = 0; i < n; i++)
                o[i] = TextShadow.Lerp(i < a.Length ? a[i] : TextShadow.Zero, i < b.Length ? b[i] : TextShadow.Zero, u);
            return o;
        }
    }

    /// <summary>One animation of the `animation` shorthand: name, duration, timing, delay, iterations, direction, fill.</summary>
    public sealed class CssAnimation
    {
        public string Name = "none";
        public float DurationMs;
        public float DelayMs;
        /// <summary>animation-iteration-count; float.PositiveInfinity for infinite.</summary>
        public float Iterations = 1f;
        public AnimDirection Direction = AnimDirection.Normal;
        public AnimFill Fill = AnimFill.None;
        public bool Paused;
        public EaseFn Easing = Ease.Css;
        public string EasingText = "ease";
        /// <summary>The frames to sample; Parse looks the built-ins up by name (null when unknown, e.g. "none").</summary>
        public CssKeyframes Keyframes;

        public bool IsInfinite => float.IsPositiveInfinity(Iterations);
        public float ActiveMs => DurationMs * Iterations;
        public float EndMs => DelayMs + ActiveMs;

        /// <summary>"ocbg-explore-plate-in 700ms 120ms cubic-bezier(.22,.61,.36,1) both" (the first time is the duration, the second the delay).</summary>
        public static CssAnimation Parse(string shorthand)
        {
            var a = new CssAnimation();
            bool haveDuration = false;
            foreach (string raw in CssLex.SplitSpaces(shorthand ?? ""))
            {
                string t = raw.Trim();
                string lower = t.ToLowerInvariant();
                if (CssLex.TryTimeMs(lower, out float ms))
                {
                    if (!haveDuration) { a.DurationMs = ms; haveDuration = true; }
                    else a.DelayMs = ms;
                    continue;
                }
                if (IsTiming(lower))
                {
                    a.EasingText = lower;
                    a.Easing = EaseOfCss(lower);
                    continue;
                }
                if (lower == "infinite") { a.Iterations = float.PositiveInfinity; continue; }
                if (CssLex.TryNumber(lower, out float count, out string unit) && unit.Length == 0) { a.Iterations = Math.Max(0f, count); continue; }
                switch (lower)
                {
                    case "normal": a.Direction = AnimDirection.Normal; continue;
                    case "reverse": a.Direction = AnimDirection.Reverse; continue;
                    case "alternate": a.Direction = AnimDirection.Alternate; continue;
                    case "alternate-reverse": a.Direction = AnimDirection.AlternateReverse; continue;
                    case "forwards": a.Fill = AnimFill.Forwards; continue;
                    case "backwards": a.Fill = AnimFill.Backwards; continue;
                    case "both": a.Fill = AnimFill.Both; continue;
                    case "running": a.Paused = false; continue;
                    case "paused": a.Paused = true; continue;
                }
                a.Name = t;
            }
            a.Keyframes = CssKeyframes.Get(a.Name);
            return a;
        }

        /// <summary>A comma-separated animation list ("none" → empty).</summary>
        public static List<CssAnimation> ParseList(string css)
        {
            var list = new List<CssAnimation>();
            if (string.IsNullOrWhiteSpace(css) || css.Trim() == "none") return list;
            foreach (string part in CssLex.SplitTopLevel(css, ',')) list.Add(Parse(part));
            return list;
        }

        internal static bool IsTiming(string t) =>
            t == "ease" || t == "linear" || t == "ease-in" || t == "ease-out" || t == "ease-in-out" ||
            t == "step-start" || t == "step-end" || t.StartsWith("cubic-bezier(", StringComparison.Ordinal) ||
            t.StartsWith("steps(", StringComparison.Ordinal);

        /// <summary>A CSS timing function (Ease.Of knows them; step functions run linear there).</summary>
        internal static EaseFn EaseOfCss(string t) => t == "step-start" || t == "step-end" ? Ease.Linear : Ease.Of(t);

        /// <summary>
        /// The directed progress (0..1 inside the current iteration) at tMs since the animation was applied, or false
        /// when it has no effect then (before the delay without a backwards fill, after the end without a forwards fill).
        /// </summary>
        public bool TryProgress(float tMs, out float progress, out int iteration)
        {
            progress = 0f;
            iteration = 0;
            float iters = Math.Max(0f, Iterations);
            float active = DurationMs > 0f ? DurationMs * iters : 0f;
            float overall;
            if (tMs < DelayMs)
            {
                if (Fill != AnimFill.Backwards && Fill != AnimFill.Both) return false;
                overall = 0f;
            }
            else if (!float.IsPositiveInfinity(active) && tMs >= DelayMs + active)
            {
                if (Fill != AnimFill.Forwards && Fill != AnimFill.Both) return false;
                overall = iters;
            }
            else overall = DurationMs > 0f ? (tMs - DelayMs) / DurationMs : iters;

            float simple;
            if (overall >= iters && !float.IsPositiveInfinity(iters) && iters > 0f)
            {
                // after the end: an integral count finishes at progress 1 of its last iteration
                float frac = iters - (float)Math.Floor(iters);
                if (frac == 0f) { simple = 1f; iteration = (int)iters - 1; }
                else { simple = frac; iteration = (int)Math.Floor(iters); }
            }
            else
            {
                iteration = (int)Math.Floor(overall);
                simple = overall - iteration;
            }
            bool reversed;
            switch (Direction)
            {
                case AnimDirection.Reverse: reversed = true; break;
                case AnimDirection.Alternate: reversed = (iteration & 1) == 1; break;
                case AnimDirection.AlternateReverse: reversed = (iteration & 1) == 0; break;
                default: reversed = false; break;
            }
            progress = reversed ? 1f - simple : simple;
            return true;
        }

        /// <summary>The animated values at tMs (false = not applied now, or no keyframes).</summary>
        public bool TrySample(float tMs, out AnimProps props)
        {
            props = default;
            if (Keyframes == null) return false;
            if (!TryProgress(tMs, out float p, out _)) return false;
            props = Keyframes.Sample(p, Easing);
            return true;
        }

        /// <summary>True once a finite animation has run its last iteration.</summary>
        public bool IsFinished(float tMs) => !IsInfinite && tMs >= EndMs;

        /// <summary>A copy to change (a per-element delay, as ExploreScene.js:1116 sets): the keyframes are shared, they never change.</summary>
        public CssAnimation Clone() => (CssAnimation)MemberwiseClone();
    }

    /// <summary>One item of the `transition` shorthand.</summary>
    public sealed class TransitionSpec
    {
        public string Property = "all";
        public float DurationMs;
        public float DelayMs;
        public EaseFn Easing = Ease.Css;
        public string EasingText = "ease";

        /// <summary>"border-color .14s, background .14s" / "transform 90ms cubic-bezier(.4,0,.2,1), opacity 300ms" / "none".</summary>
        public static List<TransitionSpec> ParseList(string css)
        {
            var list = new List<TransitionSpec>();
            if (string.IsNullOrWhiteSpace(css) || css.Trim() == "none") return list;
            foreach (string part in CssLex.SplitTopLevel(css, ','))
            {
                var t = new TransitionSpec();
                bool haveDuration = false;
                foreach (string raw in CssLex.SplitSpaces(part))
                {
                    string lower = raw.Trim().ToLowerInvariant();
                    if (CssLex.TryTimeMs(lower, out float ms))
                    {
                        if (!haveDuration) { t.DurationMs = ms; haveDuration = true; }
                        else t.DelayMs = ms;
                    }
                    else if (CssAnimation.IsTiming(lower))
                    {
                        t.EasingText = lower;
                        t.Easing = CssAnimation.EaseOfCss(lower);
                    }
                    else t.Property = lower;
                }
                list.Add(t);
            }
            return list;
        }

        /// <summary>Does this transition animate the property? ("all", the property itself, or its shorthand).</summary>
        public bool Covers(string property)
        {
            if (Property == "all" || Property == property) return true;
            if (property.StartsWith(Property + "-", StringComparison.Ordinal)) return true;   // background → background-color
            if (Property == "border-color" && property.StartsWith("border-", StringComparison.Ordinal) && property.EndsWith("-color", StringComparison.Ordinal)) return true;
            return false;
        }

        public string ToCss() => string.Format(CultureInfo.InvariantCulture, "{0} {1}ms {2} {3}ms", Property, DurationMs, EasingText, DelayMs);

        public TransitionSpec Clone() => (TransitionSpec)MemberwiseClone();
    }
}
