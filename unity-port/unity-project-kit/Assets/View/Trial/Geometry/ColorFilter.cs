// CSS filter: brightness(k) grayscale(g), the only filter functions the trial's screens use on colours.
// Evaluated in sRGB with the Rec.709 luminance weights of the Filter Effects spec (as Chrome does for the shorthand
// functions); alpha is untouched. Brightness goes first and is clamped to 0..1 before grayscale, as the browser
// clamps each filter's output: the game's only mixes are a grayscale(.85) brightness(.62) list (linear, no clamp
// reached) and brightness flashes (> 1) on elements inside a grayscale(1) ancestor, where the inner flash clamps
// before the outer grayscale.
using System;
using System.Globalization;

namespace Trial.Geometry
{
    public readonly struct ColorFilter : IEquatable<ColorFilter>
    {
        public readonly float Brightness, Grayscale;

        public ColorFilter(float brightness = 1f, float grayscale = 0f) { Brightness = brightness; Grayscale = grayscale; }

        public static readonly ColorFilter None = new ColorFilter(1f, 0f);

        public bool IsNone => Brightness == 1f && Grayscale == 0f;

        public Rgba Apply(Rgba c)
        {
            float r = Clamp01(c.R * Brightness), g = Clamp01(c.G * Brightness), b = Clamp01(c.B * Brightness);
            if (Grayscale != 0f)
            {
                float k = 1f - Math.Max(0f, Math.Min(1f, Grayscale));
                float y = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                r = y + k * (r - y);
                g = y + k * (g - y);
                b = y + k * (b - y);
            }
            return new Rgba(Clamp01(r), Clamp01(g), Clamp01(b), c.A);
        }

        /// <summary>
        /// Both filters applied (an element's filter inside an ancestor's). Brightness and grayscale are linear and
        /// commute, so the result is brightness k1·k2 and grayscale 1 − (1 − g1)(1 − g2); Apply clamps after the
        /// combined brightness, which is the browser's order when the brightness is the inner filter (a flash inside
        /// a greyed panel). An outer brightness below 1 over an inner one above 1 is not modelled.
        /// </summary>
        public ColorFilter Then(ColorFilter inner) =>
            new ColorFilter(Brightness * inner.Brightness,
                1f - (1f - Math.Max(0f, Math.Min(1f, Grayscale))) * (1f - Math.Max(0f, Math.Min(1f, inner.Grayscale))));

        /// <summary>
        /// "brightness(.6)", "grayscale(1)", "brightness(0.55) grayscale(0.4)", "none"; percentages allowed. Other
        /// functions (drop-shadow(0 0 8px rgba(…))) are skipped whole, nested parentheses included.
        /// </summary>
        public static ColorFilter Parse(string css)
        {
            var f = None;
            if (string.IsNullOrWhiteSpace(css)) return f;
            string s = css.Trim();
            int i = 0;
            while (i < s.Length)
            {
                int open = s.IndexOf('(', i);
                if (open < 0) break;
                int close = -1;
                for (int k = open + 1, depth = 1; k < s.Length; k++)
                {
                    if (s[k] == '(') depth++;
                    else if (s[k] == ')' && --depth == 0) { close = k; break; }
                }
                if (close < 0) break;
                string name = s.Substring(i, open - i).Trim();
                string arg = s.Substring(open + 1, close - open - 1).Trim();
                float v = ParseAmount(arg);
                if (name == "brightness") f = new ColorFilter(f.Brightness * v, f.Grayscale);
                else if (name == "grayscale") f = f.Then(new ColorFilter(1f, Math.Min(1f, v)));
                i = close + 1;
            }
            return f;
        }

        private static float ParseAmount(string arg)
        {
            if (arg.Length == 0) return 1f;
            bool pct = arg.EndsWith("%", StringComparison.Ordinal);
            string num = pct ? arg.Substring(0, arg.Length - 1) : arg;
            float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float v);
            return pct ? v / 100f : v;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        public bool Equals(ColorFilter o) => Brightness == o.Brightness && Grayscale == o.Grayscale;
        public override bool Equals(object obj) => obj is ColorFilter f && Equals(f);
        public override int GetHashCode() => Brightness.GetHashCode() * 397 ^ Grayscale.GetHashCode();
    }
}
