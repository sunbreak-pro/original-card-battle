// CSS colour values: #rgb #rgba #rrggbb #rrggbbaa, rgb()/rgba() (comma or space syntax, numbers or %), hsl()/hsla(),
// transparent, currentcolor (from the vars' "color"), the named colours, var(--x[, fallback]) through DsTokens, and
// color-mix(in srgb, A p%?, B q%?) per CSS Color 5 (premultiplied sRGB; percentages normalised, the alpha scaled by
// their sum when it is below 100 %). Results are straight-alpha sRGB 0..1 floats; Chrome keeps the mix unrounded too.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Trial.Geometry
{
    public static class CssColor
    {
        /// <summary>The colour, or throws FormatException when the text is not a colour this parser knows.</summary>
        public static Rgba Parse(string css, DsTheme theme = DsTheme.Dusk, CssVars vars = null)
        {
            if (TryParse(css, out Rgba c, theme, vars)) return c;
            throw new FormatException("Not a CSS colour: '" + css + "'");
        }

        public static bool TryParse(string css, out Rgba color, DsTheme theme = DsTheme.Dusk, CssVars vars = null)
        {
            color = Rgba.Transparent;
            if (string.IsNullOrWhiteSpace(css)) return false;
            string s = css.Trim();
            if (s.IndexOf("var(", StringComparison.Ordinal) >= 0)
            {
                if (!CssLex.TrySubstitute(s, vars, theme, null, out s)) return false;
                s = s.Trim();
            }
            return TryParseResolved(s, out color, theme, vars);
        }

        private static bool TryParseResolved(string s, out Rgba color, DsTheme theme, CssVars vars)
        {
            color = Rgba.Transparent;
            if (s.Length == 0) return false;
            if (s[0] == '#') return TryHex(s, out color);
            string lower = s.ToLowerInvariant();
            int open = lower.IndexOf('(');
            if (open < 0)
            {
                if (lower == "currentcolor")
                {
                    if (vars != null && vars.TryGet("color", out string cur) && !string.Equals(cur.Trim(), "currentcolor", StringComparison.OrdinalIgnoreCase))
                        return TryParse(cur, out color, theme, vars);
                    return false;
                }
                return Named.TryGetValue(lower, out color);
            }
            if (!lower.EndsWith(")", StringComparison.Ordinal)) return false;
            string fn = lower.Substring(0, open).Trim();
            string inner = s.Substring(open + 1, s.Length - open - 2);
            switch (fn)
            {
                case "rgb":
                case "rgba":
                    return TryRgb(inner, out color);
                case "hsl":
                case "hsla":
                    return TryHsl(inner, out color);
                case "color-mix":
                    return TryMix(inner, out color, theme, vars);
                default:
                    return false;
            }
        }

        /// <summary>
        /// color-mix(in srgb, a pa, b pb) with the percentages in 0..100. Pass float.NaN for an omitted percentage
        /// (CSS Color 5 §2.1: one omitted = 100 − the other; both omitted = 50 / 50). Interpolates premultiplied;
        /// when pa + pb is below 100 the result's alpha is multiplied by (pa + pb) / 100.
        /// </summary>
        public static Rgba Mix(Rgba a, float pa, Rgba b, float pb)
        {
            bool na = float.IsNaN(pa), nb = float.IsNaN(pb);
            if (na && nb) { pa = 50f; pb = 50f; }
            else if (na) pa = 100f - pb;
            else if (nb) pb = 100f - pa;
            float sum = pa + pb;
            if (sum <= 0f) return Rgba.Transparent;
            float ta = pa / sum, tb = pb / sum;
            float alphaMul = sum < 100f ? sum / 100f : 1f;
            float alpha = a.A * ta + b.A * tb;
            if (alpha <= 0f) return Rgba.Transparent;
            float r = (a.R * a.A * ta + b.R * b.A * tb) / alpha;
            float g = (a.G * a.A * ta + b.G * b.A * tb) / alpha;
            float bl = (a.B * a.A * ta + b.B * b.A * tb) / alpha;
            return new Rgba(r, g, bl, alpha * alphaMul);
        }

        /// <summary>rgba(r, g, b, a) with byte channels (rounded) and the alpha to 3 decimals: for round trips and logs.</summary>
        public static string ToCss(Rgba c) =>
            string.Format(CultureInfo.InvariantCulture, "rgba({0},{1},{2},{3})", c.R8, c.G8, c.B8,
                Math.Round(Math.Max(0f, Math.Min(1f, c.A)), 3).ToString("0.###", CultureInfo.InvariantCulture));

        // ---- forms -----------------------------------------------------------------------------------------

        private static bool TryHex(string s, out Rgba color)
        {
            color = Rgba.Transparent;
            string h = s.Substring(1);
            for (int i = 0; i < h.Length; i++) if (Hex(h[i]) < 0) return false;
            switch (h.Length)
            {
                case 3:
                case 4:
                {
                    int r = Hex(h[0]) * 17, g = Hex(h[1]) * 17, b = Hex(h[2]) * 17;
                    float a = h.Length == 4 ? Hex(h[3]) * 17 / 255f : 1f;
                    color = Rgba.Bytes(r, g, b, a);
                    return true;
                }
                case 6:
                case 8:
                {
                    int r = Hex(h[0]) * 16 + Hex(h[1]), g = Hex(h[2]) * 16 + Hex(h[3]), b = Hex(h[4]) * 16 + Hex(h[5]);
                    float a = h.Length == 8 ? (Hex(h[6]) * 16 + Hex(h[7])) / 255f : 1f;
                    color = Rgba.Bytes(r, g, b, a);
                    return true;
                }
                default:
                    return false;
            }
        }

        private static int Hex(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        private static bool SplitArgs(string inner, out List<string> parts, out string alpha)
        {
            alpha = null;
            parts = null;
            List<string> commas = CssLex.SplitTopLevel(inner, ',');
            if (commas.Count >= 3)
            {
                parts = commas.GetRange(0, 3);
                if (commas.Count == 4) alpha = commas[3];
                else if (commas.Count > 4) return false;
                return true;
            }
            // space syntax: "r g b / a"
            string body = inner;
            int slash = CssLex.IndexOfTopLevel(inner, '/');
            if (slash >= 0)
            {
                body = inner.Substring(0, slash);
                alpha = inner.Substring(slash + 1).Trim();
            }
            parts = CssLex.SplitSpaces(body);
            return parts.Count == 3;
        }

        private static bool TryRgb(string inner, out Rgba color)
        {
            color = Rgba.Transparent;
            if (!SplitArgs(inner, out List<string> p, out string alphaText)) return false;
            var ch = new float[3];
            for (int i = 0; i < 3; i++)
            {
                if (!CssLex.TryNumber(p[i], out float v, out string unit)) return false;
                if (unit == "%") v = v * 255f / 100f;
                else if (unit.Length != 0) return false;
                ch[i] = Math.Max(0f, Math.Min(255f, v)) / 255f;
            }
            float a = 1f;
            if (alphaText != null && !TryAlpha(alphaText, out a)) return false;
            color = new Rgba(ch[0], ch[1], ch[2], a);
            return true;
        }

        private static bool TryAlpha(string text, out float a)
        {
            a = 1f;
            if (!CssLex.TryNumber(text.Trim(), out float v, out string unit)) return false;
            if (unit == "%") v /= 100f;
            else if (unit.Length != 0) return false;
            a = Math.Max(0f, Math.Min(1f, v));
            return true;
        }

        private static bool TryHsl(string inner, out Rgba color)
        {
            color = Rgba.Transparent;
            if (!SplitArgs(inner, out List<string> p, out string alphaText)) return false;
            if (!CssLex.TryNumber(p[0], out float h, out string hu)) return false;
            if (hu == "turn") h *= 360f;
            else if (hu == "rad") h = (float)(h * 180.0 / Math.PI);
            else if (hu.Length != 0 && hu != "deg") return false;
            if (!CssLex.TryNumber(p[1], out float sat, out _)) return false;
            if (!CssLex.TryNumber(p[2], out float lig, out _)) return false;
            float a = 1f;
            if (alphaText != null && !TryAlpha(alphaText, out a)) return false;
            sat = Math.Max(0f, Math.Min(100f, sat)) / 100f;
            lig = Math.Max(0f, Math.Min(100f, lig)) / 100f;
            h = ((h % 360f) + 360f) % 360f;
            // CSS Color 4 §7.1 hslToRgb
            float F(float n)
            {
                float k = (n + h / 30f) % 12f;
                float aa = sat * Math.Min(lig, 1f - lig);
                return lig - aa * Math.Max(-1f, Math.Min(Math.Min(k - 3f, 9f - k), 1f));
            }
            color = new Rgba(F(0f), F(8f), F(4f), a);
            return true;
        }

        private static bool TryMix(string inner, out Rgba color, DsTheme theme, CssVars vars)
        {
            color = Rgba.Transparent;
            List<string> parts = CssLex.SplitTopLevel(inner, ',');
            if (parts.Count != 3) return false;
            string space = parts[0].Trim().ToLowerInvariant();
            if (space != "in srgb") return false; // the only space the trial writes
            if (!TryMixArg(parts[1], out Rgba a, out float pa, theme, vars)) return false;
            if (!TryMixArg(parts[2], out Rgba b, out float pb, theme, vars)) return false;
            if (!float.IsNaN(pa) && !float.IsNaN(pb) && pa + pb <= 0f) return false;
            color = Mix(a, pa, b, pb);
            return true;
        }

        /// <summary>"var(--ink) 55%" → colour + 55; "transparent" → colour + NaN. The percentage may come first.</summary>
        private static bool TryMixArg(string arg, out Rgba c, out float pct, DsTheme theme, CssVars vars)
        {
            c = Rgba.Transparent;
            pct = float.NaN;
            List<string> tokens = CssLex.SplitSpaces(arg);
            string colourText = null;
            foreach (string t in tokens)
            {
                if (t.EndsWith("%", StringComparison.Ordinal) && CssLex.TryNumber(t, out float v, out string unit) && unit == "%")
                {
                    pct = v;
                    continue;
                }
                colourText = colourText == null ? t : colourText + " " + t;
            }
            return colourText != null && TryParse(colourText, out c, theme, vars);
        }

        // The named colours a stylesheet may write (CSS Color 4 basic set plus the few extended names that are
        // common in hand-written CSS). The trial's own CSS uses only `transparent`; the rest keep pasted values valid.
        private static readonly Dictionary<string, Rgba> Named = new Dictionary<string, Rgba>(StringComparer.Ordinal)
        {
            ["transparent"] = Rgba.Transparent,
            ["black"] = Rgba.Hex(0x000000), ["white"] = Rgba.Hex(0xffffff), ["red"] = Rgba.Hex(0xff0000),
            ["lime"] = Rgba.Hex(0x00ff00), ["green"] = Rgba.Hex(0x008000), ["blue"] = Rgba.Hex(0x0000ff),
            ["yellow"] = Rgba.Hex(0xffff00), ["cyan"] = Rgba.Hex(0x00ffff), ["aqua"] = Rgba.Hex(0x00ffff),
            ["magenta"] = Rgba.Hex(0xff00ff), ["fuchsia"] = Rgba.Hex(0xff00ff), ["gray"] = Rgba.Hex(0x808080),
            ["grey"] = Rgba.Hex(0x808080), ["silver"] = Rgba.Hex(0xc0c0c0), ["maroon"] = Rgba.Hex(0x800000),
            ["olive"] = Rgba.Hex(0x808000), ["purple"] = Rgba.Hex(0x800080), ["teal"] = Rgba.Hex(0x008080),
            ["navy"] = Rgba.Hex(0x000080), ["orange"] = Rgba.Hex(0xffa500), ["gold"] = Rgba.Hex(0xffd700),
            ["pink"] = Rgba.Hex(0xffc0cb), ["crimson"] = Rgba.Hex(0xdc143c), ["darkgray"] = Rgba.Hex(0xa9a9a9),
            ["lightgray"] = Rgba.Hex(0xd3d3d3), ["dimgray"] = Rgba.Hex(0x696969),
        };
    }
}
