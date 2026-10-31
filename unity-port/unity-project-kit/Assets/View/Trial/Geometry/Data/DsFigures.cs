// The design system's figure silhouettes (vendor/ds/bundle.js:575-656, FIGURES, poly and Figure), strings verbatim.
// Each figure: viewBox w×h with the feet on the bottom edge, `body` = the width the target frames hook, `cx` = the
// feet's centre x, `side` self / foe. The poly() bars are stored as the exact strings the JS computes.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Trial.Geometry
{
    public sealed class FigureDef
    {
        public readonly string Name;
        public readonly float W, H, Body, Cx;
        /// <summary>"self" or "foe".</summary>
        public readonly string Side;
        /// <summary>One filled path per part, in the JS order (each part is filled on its own).</summary>
        public readonly IReadOnlyList<string> Paths;

        public FigureDef(string name, float w, float h, float body, float cx, string side, string[] paths)
        {
            Name = name; W = w; H = h; Body = body; Cx = cx; Side = side; Paths = paths;
        }

        public bool IsFoe => Side == "foe";
        public RectF ViewBox => new RectF(0f, 0f, W, H);

        /// <summary>js: bundle.js:646 Figure(): the default rim colour (rgba(139,111,208,.35) foe, rgba(232,168,76,.28) self).</summary>
        public Rgba DefaultRim => IsFoe ? DsFigures.RimFoe : DsFigures.RimSelf;

        /// <summary>The token name of the default fill (--figure-enemy / --figure-self).</summary>
        public string DefaultFillToken => IsFoe ? "figure-enemy" : "figure-self";

        /// <summary>js: bundle.js:652 `scaleTo`: height scaleTo, width w·scaleTo/h.</summary>
        public (float W, float H) SizeFor(float scaleTo) => (W * scaleTo / H, scaleTo);
    }

    public static class DsFigures
    {
        /// <summary>The rim group's stroke width (bundle.js:648 `'stroke-width': 4`, round joins).</summary>
        public const float RimStrokeWidth = 4f;
        public static readonly Rgba RimSelf = Rgba.Bytes(232, 168, 76, 0.28f);
        public static readonly Rgba RimFoe = Rgba.Bytes(139, 111, 208, 0.35f);

        /// <summary>
        /// js: vendor/ds/bundle.js:579 poly(): a bar of width w from (x1,y1) to (x2,y2) as the closed quad
        /// (p1+n)(p2+n)(p2−n)(p1−n), n = (−dy, dx)/len · w/2. Numbers are written round-trip ("R"), which matches
        /// the JS strings on .NET Core; the figures below hold the JS output itself.
        /// </summary>
        public static string Poly(double x1, double y1, double x2, double y2, double w)
        {
            double dx = x2 - x1, dy = y2 - y1, l = Math.Sqrt(dx * dx + dy * dy), nx = -dy / l * w / 2, ny = dx / l * w / 2;
            return "M" + N(x1 + nx) + " " + N(y1 + ny) + "L" + N(x2 + nx) + " " + N(y2 + ny) + "L" + N(x2 - nx) + " " + N(y2 - ny)
                + "L" + N(x1 - nx) + " " + N(y1 - ny) + "Z";
        }

        private static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        // js: vendor/ds/bundle.js:583 FIGURES
        private static readonly FigureDef[] All =
        {
            new FigureDef("swordsman", 140, 226, 100, 62, "self", new[]
            {
                "M62 6c8 0 14 6 14 14s-6 14-14 14-14-6-14-14 6-14 14-14z",
                "M50 12C42 16 39 26 43 34L50 26Z",
                "M56 32h12v10H56z",
                "M40 46C46 40 56 38 62 38S80 40 86 46L90 80 86 112 96 150H28L38 112 34 80Z",
                "M80 48L94 70 100 94 92 98 86 74 74 56Z",
                "M44 50L40 76 60 94 94 90 94 99 56 104 32 80 36 52Z",
                // poly(100, 92, 136, 22, 5)
                "M102.22321950579537 93.14337003155191L138.22321950579536 23.143370031551903L133.77678049420464 20.856629968448097L97.77678049420463 90.85662996844809Z",
                // poly(91, 87, 109, 97, 4)
                "M90.02871413764274 88.74831455224307L108.02871413764274 98.74831455224307L109.97128586235726 95.25168544775693L91.97128586235726 85.25168544775693Z",
                // poly(93, 106, 100, 92, 4)
                "M94.78885438199983 106.89442719099992L101.78885438199983 92.89442719099992L98.21114561800017 91.10557280900008L91.21114561800017 105.10557280900008Z",
                "M66 148H80L92 188 104 222 90 224 78 192Z",
                "M34 148H50L46 186 36 222H22L32 186Z",
                "M88 220H110L112 226H86ZM18 220H40V226H16Z",
            }),
            new FigureDef("spearman", 170, 226, 110, 104, "foe", new[]
            {
                "M120 8C110 0 96 2 90 10L70 16Q63 21 68 27L92 31 96 39 118 41C127 32 129 16 120 8Z",
                "M110 9L132 1 121 15Z",
                "M100 36L118 38 123 55 96 57Z",
                "M88 55C96 48 124 48 132 56L137 92 128 128H92L86 92Z",
                "M126 116C148 128 161 160 167 204L160 207C150 172 138 148 120 130Z",
                "M92 60L74 86 61 104 68 110 82 93 99 70Z",
                "M128 62L121 92 104 99 106 106 126 99 137 70Z",
                // poly(20, 164, 152, 30, 5)
                "M21.781008092278007 165.7544258819455L153.781008092278 31.7544258819455L150.218991907722 28.2455741180545L18.218991907721993 162.2455741180545Z",
                "M30 148L4 180 38 158Z",
                "M92 124H108L100 160 108 196 96 222 72 226 71 219 89 214 92 197 84 160Z",
                "M112 124H128L124 162 132 198 124 222 102 226 101 219 117 214 116 198 108 160Z",
            }),
            new FigureDef("hound", 230, 218, 180, 110, "foe", new[]
            {
                "M40 70C46 54 64 48 78 54L94 66 88 82 60 86 34 84Z",
                "M30 70L6 64 10 76 30 82Z",
                "M72 52L84 36 88 58Z",
                "M80 70C110 60 160 62 190 76L204 104 186 132 120 134 86 120 76 96Z",
                "M196 84C214 92 226 118 228 150L220 152C216 126 206 106 190 98Z",
                "M88 116L76 160 70 216 84 218 92 162 104 124Z",
                "M112 122L114 170 104 216 118 218 128 170 128 124Z",
                "M160 124L170 168 160 216 174 218 186 166 178 122Z",
                "M182 118L196 164 192 216 206 218 210 162 196 114Z",
            }),
            new FigureDef("serpent", 220, 234, 160, 96, "foe", new[]
            {
                "M58 8C72 0 92 4 98 18L102 34 84 42 62 38 42 30C40 20 46 12 58 8Z",
                "M44 22L18 26 22 34 44 32Z",
                "M80 36C96 40 104 60 100 84 96 110 82 132 86 156L60 160C54 132 70 108 74 84 78 64 72 48 68 40Z",
                "M60 150C40 166 34 196 56 214 84 234 150 234 186 218 214 204 216 178 196 166 172 152 130 160 110 176L120 196C138 186 164 180 176 188 188 196 180 208 160 212 128 220 92 218 72 206 58 196 62 176 78 166Z",
                "M108 178C124 196 156 200 184 194L190 206C160 216 118 212 98 192Z",
            }),
            new FigureDef("warden", 220, 264, 180, 120, "foe", new[]
            {
                "M136 10C124 0 106 4 100 14L80 22Q72 28 78 34L104 38 108 48 132 50C142 38 144 20 136 10Z",
                "M126 10L150 0 138 18Z",
                "M108 46H138L144 66 102 68Z",
                "M86 66C100 56 150 56 166 68L176 118 164 168H92L80 118Z",
                "M160 160C186 174 204 210 212 252L202 256C192 218 176 192 152 178Z",
                "M30 70H86L92 92 88 190 58 206 28 190 24 92Z",
                "M96 160H120L114 214 122 258 92 264 88 256 102 250 98 214Z",
                "M136 160H160L160 214 170 256 140 264 136 256 150 250 140 214Z",
            }),
        };

        private static readonly Dictionary<string, FigureDef> ByName = Index();

        private static Dictionary<string, FigureDef> Index()
        {
            var m = new Dictionary<string, FigureDef>(StringComparer.Ordinal);
            foreach (FigureDef f in All) m[f.Name] = f;
            return m;
        }

        /// <summary>The fallback of an unknown kind (bundle.js:643 `FIGURES[kind] || FIGURES.swordsman`).</summary>
        public const string Fallback = "swordsman";

        public static IReadOnlyList<FigureDef> Figures => All;

        public static bool Has(string kind) => kind != null && ByName.ContainsKey(kind);

        /// <summary>js: vendor/ds/bundle.js:641 Figure(): the figure of `kind`, the swordsman when unknown.</summary>
        public static FigureDef Get(string kind) => kind != null && ByName.TryGetValue(kind, out FigureDef f) ? f : ByName[Fallback];

        /// <summary>
        /// js: scenes/battle/field/art.js:25 glowGradient(): a closest-side radial whose alpha falls as (1 − r)², as
        /// the stops α·(1 − r)² at r = 0, .25, .5, .75 and transparent at 1 (positions 0..1).
        /// </summary>
        public static (float Pos, float Alpha)[] GlowStops(float alpha)
        {
            var o = new (float, float)[5];
            float[] r = { 0f, 0.25f, 0.5f, 0.75f };
            for (int i = 0; i < 4; i++) o[i] = (r[i], alpha * (1f - r[i]) * (1f - r[i]));
            o[4] = (1f, 0f);
            return o;
        }
    }
}
