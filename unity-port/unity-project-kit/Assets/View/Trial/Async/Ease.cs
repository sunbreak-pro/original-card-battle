// Easing functions: the cubic set every trial scene uses (stage.js EASE = Unity's UiTween.Apply), the CSS
// keywords and cubic-bezier() as Chromium evaluates it (UnitBezier: Newton's method, bisection fallback).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Trial
{
    public delegate float EaseFn(float t);

    public static class Ease
    {
        // stage.js EASE
        public static readonly EaseFn Out = t => 1f - Cube(1f - t);
        public static readonly EaseFn In = t => t * t * t;
        public static readonly EaseFn InOut = t => t < 0.5f ? 4f * t * t * t : 1f - Cube(-2f * t + 2f) / 2f;
        public static readonly EaseFn Linear = t => t;
        public static readonly EaseFn Smooth = t => t * t * (3f - 2f * t);

        // CSS keywords
        public static readonly EaseFn Css = CubicBezier(0.25f, 0.1f, 0.25f, 1f);          // "ease"
        public static readonly EaseFn CssIn = CubicBezier(0.42f, 0f, 1f, 1f);             // "ease-in"
        public static readonly EaseFn CssOut = CubicBezier(0f, 0f, 0.58f, 1f);            // "ease-out"
        public static readonly EaseFn CssInOut = CubicBezier(0.42f, 0f, 0.58f, 1f);       // "ease-in-out"

        // cubic-bezier values the scenes' CSS uses by name
        public static readonly EaseFn Decel = CubicBezier(0.22f, 0.61f, 0.36f, 1f);
        public static readonly EaseFn Standard = CubicBezier(0.4f, 0f, 0.2f, 1f);

        private static float Cube(float x) => x * x * x;

        private static readonly Dictionary<string, EaseFn> ByName = new Dictionary<string, EaseFn>(StringComparer.Ordinal)
        {
            ["out"] = Out, ["in"] = In, ["inout"] = InOut, ["linear"] = Linear, ["smooth"] = Smooth,
            ["ease"] = Css, ["ease-in"] = CssIn, ["ease-out"] = CssOut, ["ease-in-out"] = CssInOut,
        };

        /// <summary>
        /// "out" | "in" | "inout" | "linear" | "smooth" (the JS clock's names), the CSS keywords, or
        /// "cubic-bezier(x1, y1, x2, y2)". Null, empty or unknown is Out (the JS clock's default).
        /// </summary>
        public static EaseFn Of(string name)
        {
            if (string.IsNullOrEmpty(name)) return Out;
            string key = name.Trim();
            if (ByName.TryGetValue(key, out EaseFn fn)) return fn;
            if (key.StartsWith("cubic-bezier(", StringComparison.Ordinal) && key.EndsWith(")", StringComparison.Ordinal))
            {
                string[] parts = key.Substring(13, key.Length - 14).Split(',');
                if (parts.Length == 4)
                {
                    var v = new float[4];
                    for (int i = 0; i < 4; i++)
                        if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return Out;
                    return CubicBezier(v[0], v[1], v[2], v[3]);
                }
            }
            if (key.StartsWith("steps(", StringComparison.Ordinal)) return Linear;
            return Out;
        }

        /// <summary>Null-safe: the function itself, or Out.</summary>
        public static EaseFn OrOut(EaseFn fn) => fn ?? Out;

        /// <summary>CSS cubic-bezier(x1, y1, x2, y2) as Chromium's gfx::CubicBezier solves it.</summary>
        public static EaseFn CubicBezier(float x1, float y1, float x2, float y2)
        {
            double cx = 3.0 * x1, bx = 3.0 * (x2 - x1) - cx, ax = 1.0 - cx - bx;
            double cy = 3.0 * y1, by = 3.0 * (y2 - y1) - cy, ay = 1.0 - cy - by;
            double SampleX(double t) => ((ax * t + bx) * t + cx) * t;
            double SampleY(double t) => ((ay * t + by) * t + cy) * t;
            double SampleDX(double t) => (3.0 * ax * t + 2.0 * bx) * t + cx;

            double SolveX(double x)
            {
                const double eps = 1e-7;
                double t = x;
                for (int i = 0; i < 8; i++)
                {
                    double e = SampleX(t) - x;
                    if (Math.Abs(e) < eps) return t;
                    double d = SampleDX(t);
                    if (Math.Abs(d) < 1e-6) break;
                    t -= e / d;
                }
                double lo = 0, hi = 1;
                t = x;
                if (t < lo) return lo;
                if (t > hi) return hi;
                while (lo < hi)
                {
                    double v = SampleX(t);
                    if (Math.Abs(v - x) < eps) return t;
                    if (x > v) lo = t; else hi = t;
                    t = (hi - lo) * 0.5 + lo;
                    if (hi - lo < 1e-9) break;
                }
                return t;
            }

            return t =>
            {
                if (t <= 0f) return 0f;
                if (t >= 1f) return 1f;
                return (float)SampleY(SolveX(t));
            };
        }
    }
}
