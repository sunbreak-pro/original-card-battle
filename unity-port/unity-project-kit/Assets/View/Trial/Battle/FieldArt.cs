// The field's colours and silhouettes (docs/BATTLE_SCENE.md §6b): Unity's BattleTheme roles turned into tokens, the
// ProceduralArt glow as a CSS radial gradient, and the figures — the design system's silhouettes (OCB.FIGURES) with the
// roster's variants (VARIANTS: weapons dropped and added, the serpent bosses, the distorted root), drawn with Unity's
// four-copy edge light, the stance's screened body light and the flash overlay. Port of scenes/battle/field/art.js.
//
// Every colour is a design-system token (tokens.css) or, for the layer's backdrop, a BackdropSpec value handed over as
// data; this file only turns Unity's BattleTheme roles into the tokens and the sprite shapes of ProceduralArt into CSS
// strings and kit graphics.
//
// Unity differences (scene look, JS wins): the svg of drawFigure is one PathGraphic whose groups are PathLayers (the four
// rim copies, the fill, the details), painted in the SVG order; a 'currentColor' fill is built in white and coloured by
// the graphic's tint (VectorGraphic.Tint), as the flash overlay's CSS `color` colours it.
using System;
using System.Collections.Generic;
using System.Globalization;
using Depiction;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    /// <summary>figureDef's answer: { w, h, body, cx, paths, details } in the figure's own box (facing left, feet on its bottom edge).</summary>
    // js: scenes/battle/field/art.js:159
    public sealed class FigureArtDef
    {
        public float W;
        public float H;
        /// <summary>body: the body width the hooks frame.</summary>
        public float Body;
        /// <summary>cx: the feet's x in the box.</summary>
        public float Cx;
        /// <summary>The silhouette's paths (SVG d strings, filled).</summary>
        public IReadOnlyList<string> Paths = Array.Empty<string>();
        /// <summary>A variant's inner lines, stroked 2 screen px in the rim colour over the fill.</summary>
        public IReadOnlyList<string> Details = Array.Empty<string>();
    }

    /// <summary>
    /// The stance's body light (field.js BODY_LIGHT): a radial light inside the silhouette — colour, its centre `At` of the
    /// height up, radius `R` of the height, stops (offset, opacity).
    /// </summary>
    // js: scenes/battle/field.js:110
    public sealed class FigureLight
    {
        public string Color = "var(--crimson)";
        public float At = 0.6f;
        public float R = 0.46f;
        public IReadOnlyList<(float At, float Alpha)> Stops = new[] { (0f, 1f), (0.35f, 0.7f), (0.7f, 0.22f), (1f, 0f) };
    }

    /// <summary>drawFigure's o: { fill, rim, scaleTo, foe, light } ('transparent' rim draws the fill alone).</summary>
    // js: scenes/battle/field/art.js:175
    public sealed class DrawFigureOpts
    {
        /// <summary>fill (CSS); null = --figure-enemy for a foe, --figure-self for the player; 'currentColor' for the flash overlay.</summary>
        public string Fill;
        /// <summary>rim (CSS); null = violet 35 % (foe) / amber 28 %; 'transparent' = no edge light.</summary>
        public string Rim;
        /// <summary>scaleTo: the height to draw at (0 = the def's own).</summary>
        public float ScaleTo;
        public bool Foe;
        /// <summary>light: the body light fills the silhouette instead of `Fill` (null = none).</summary>
        public FigureLight Light;
    }

    // js: scenes/battle/field/art.js
    public static class FieldArt
    {
        /// <summary>tok(name, alpha): a token, optionally at an alpha (BattleTheme.WithAlpha), as a CSS colour.</summary>
        // js: scenes/battle/field/art.js:7
        public static string Tok(string name, float? alpha = null)
        {
            string v = "var(--" + name + ")";
            if (alpha == null || alpha.Value >= 1f) return v;
            float pct = Math.Max(0f, Math.Min(100f, alpha.Value * 100f));
            return "color-mix(in srgb, " + v + " " + pct.ToString("F2", CultureInfo.InvariantCulture) + "%, transparent)";
        }

        /// <summary>rgba(c, mult): a BackdropSpec Rgba (0..1 channels) as a CSS colour, its alpha times `mult`.</summary>
        // js: scenes/battle/field/art.js:15
        public static string RgbaCss(global::Backdrop.Rgba c, float mult = 1f)
        {
            // JS `c.A == null ? 1 : c.A`: the C# struct always carries its alpha.
            float a = c.A * mult;
            return "rgba(" + JsRound(c.R * 255f) + "," + JsRound(c.G * 255f) + "," + JsRound(c.B * 255f) + "," + Fixed(a, 4) + ")";
        }

        /// <summary>
        /// glowGradient(color, alpha) = ProceduralArt.Glow: a radial light whose alpha falls as (1 − r)², stretched to its box —
        /// radial-gradient(closest-side, color(α(1−r)²) r% at r 0 / .25 / .5 / .75, transparent 100%). `color(a)` gives the CSS colour at alpha a.
        /// </summary>
        // js: scenes/battle/field/art.js:25
        public static string GlowGradient(Func<float, string> color, float alpha)
        {
            if (color == null) return "transparent";
            float[] rs = { 0f, 0.25f, 0.5f, 0.75f };
            var stops = new string[rs.Length];
            for (int i = 0; i < rs.Length; i++)
            {
                float r = rs[i];
                stops[i] = color(alpha * (1f - r) * (1f - r)) + " " + (r * 100f).ToString("R", CultureInfo.InvariantCulture) + "%";
            }
            return "radial-gradient(closest-side, " + string.Join(", ", stops) + ", transparent 100%)";
        }

        /// <summary>
        /// Unity's BattleTheme role names (as the player hands them to the field) → the token of a shape drawn in that role.
        /// BattleTheme.Omen is violet, Warm / Accent amber, White and Ink the near-white ink.
        /// </summary>
        // js: scenes/battle/field/art.js:34 (SHAPE)
        private static string RoleName(FxRole role)
        {
            switch (role)
            {
                case FxRole.Ink: return "ink";
                case FxRole.Ink2: return "ink2";
                case FxRole.Omen: return "omen";
                case FxRole.Guard: return "guard";
                case FxRole.Warm: return "warm";
                case FxRole.Heal: return "heal";
                case FxRole.Whiff: return "whiff";
                case FxRole.White: return "white";
                default: return "";
            }
        }

        // js: scenes/battle/field/art.js:34
        private static readonly Dictionary<string, string> Shape = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ink"] = "ink", ["white"] = "ink", ["omen"] = "violet", ["guard"] = "guard", ["warm"] = "amber", ["heal"] = "hp-text",
            ["whiff"] = "miss", ["ink2"] = "ink-muted",
        };

        /// <summary>shapeToken(role): Unity's BattleTheme role → the token a shape is drawn in (omen violet, warm amber, white / ink the ink …).</summary>
        // js: scenes/battle/field/art.js:37
        public static string ShapeToken(FxRole role)
        {
            return Shape.TryGetValue(RoleName(role), out string t) ? t : "ink";
        }

        // ---- the silhouettes (art.js:41-160) ----------------------------------------------------------------
        //
        // The silhouette the figure wears until standing art exists (FigureView wears ProceduralArt's when the shelf has no
        // art for the id). The design system draws five (OCB.FIGURES); which one an enemy wears, and what the scene adds to
        // or takes from it, is a scene choice by the roster (review fix: six enemies wore the same spearman, the elite hound
        // the normal hound's shape and the two last bosses shapes of others): the crossbowman, the archer and the twin-blade
        // soldier put the spear down for their own weapons, the crystal spear carries a crystal head, the armoured soldier its
        // plates, the unyielding elite a shield, the pack leader a mane, 獄竜 ガルド an angler's lure, and 歪みの根 a root of
        // its own. Coordinates are the DS figure's own box (facing left, the feet on its bottom edge); every addition stays
        // inside the box, so the badge 14 px over the head and the hooks round the body stand where they did.

        /// <summary>A bar `w` wide from (x1, y1) to (x2, y2), as a closed path (the DS poly()).</summary>
        // js: scenes/battle/field/art.js:54
        private static string Bar(double x1, double y1, double x2, double y2, double w)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len == 0d) len = 1d;
            double nx = (-dy / len) * (w / 2);
            double ny = (dx / len) * (w / 2);
            string P(double x, double y) => Fixed(x, 1) + " " + Fixed(y, 1);
            return "M" + P(x1 + nx, y1 + ny) + "L" + P(x2 + nx, y2 + ny) + " " + P(x2 - nx, y2 - ny) + " " + P(x1 - nx, y1 - ny) + "Z";
        }

        /// <summary>A round part of radius r at (x, y).</summary>
        // js: scenes/battle/field/art.js:64
        private static string Disc(double x, double y, double r)
        {
            return "M" + Num(x - r) + " " + Num(y) + "A" + Num(r) + " " + Num(r) + " 0 1 0 " + Num(x + r) + " " + Num(y) + "A" + Num(r) + " " + Num(r)
                + " 0 1 0 " + Num(x - r) + " " + Num(y) + "Z";
        }

        /// <summary>The spearman's spear (its 8th and 9th parts: the shaft and the head).</summary>
        // js: scenes/battle/field/art.js:69
        private static readonly int[] Spear = { 7, 8 };

        /// <summary>
        /// The bosses' serpent (review 21: the DS serpent's neck met its coil with a notch and a spike, its coil was a thin ring,
        /// and the boss carried less mass than the hero): the DS head and tongue on a thick neck that runs into two solid coils
        /// stacked on the ground, a hood behind the head and a curled tail, the coils parted by a line in the edge light. A scene
        /// shape until the boss art lands (#92); the head stays where the DS has it, so 獄竜's lure still hangs before the jaws.
        /// </summary>
        // js: scenes/battle/field/art.js:76
        private static readonly FigureArtDef SerpentDef = new FigureArtDef { W = 220f, H = 234f, Body = 184f, Cx = 124f };

        // js: scenes/battle/field/art.js:77
        private static readonly string[] SerpentPaths =
        {
            "M58 8C72 0 92 4 98 18L102 34 84 42 62 38 42 30C40 20 46 12 58 8Z",
            "M44 22L18 26 22 34 44 32Z",
            "M84 10C104 2 124 14 126 36C127 54 116 66 102 72L94 42Z",
            "M66 34C56 70 82 96 86 120C90 138 84 150 90 162L138 158C130 140 128 118 122 96C114 66 112 44 100 28Z",
            "M60 168A72 25 0 1 0 204 168A72 25 0 1 0 60 168Z",
            "M30 206A94 28 0 1 0 218 206A94 28 0 1 0 30 206Z",
            "M198 194C216 180 222 158 212 140C206 130 194 128 188 133C200 142 204 158 192 176Z",
        };

        // js: scenes/battle/field/art.js:86
        private static readonly string[] SerpentDetails =
            { "M66 182C96 198 160 199 186 188", "M92 152C106 162 124 162 136 154", "M70 60C80 62 92 60 100 54" };

        /// <summary>One VARIANTS row: a DS figure (`Kind`) with parts dropped and added, or a shape of its own (`Def` + `Paths`).</summary>
        private sealed class Variant
        {
            public string Kind;
            public int[] Drop;
            public string[] Add;
            public string[] Details;
            public FigureArtDef Def;
            public string[] Paths;
        }

        private static string[] Concat(string[] a, params string[] b)
        {
            var o = new string[a.Length + b.Length];
            Array.Copy(a, o, a.Length);
            Array.Copy(b, 0, o, a.Length, b.Length);
            return o;
        }

        // js: scenes/battle/field/art.js:87 (VARIANTS)
        private static readonly Dictionary<string, Variant> Variants = new Dictionary<string, Variant>(StringComparer.Ordinal)
        {
            ["polearm_warped"] = new Variant { Kind = "spearman" },
            // 瘴甲: plates standing off its back and shoulder, a crest on the helm
            ["rusted_revenant"] = new Variant
            {
                Kind = "spearman",
                Add = new[] { "M132 54L152 46 141 66Z", "M137 76L156 72 141 88Z", "M134 98L152 100 136 110Z", "M112 8L118 0 124 10Z", "M86 52L94 42 104 52Z" },
            },
            // 灰弩: a crossbow held level at the front hand
            ["crossbow_hunter"] = new Variant
            {
                Kind = "spearman",
                Drop = Spear,
                Add = new[] { Bar(30, 101, 110, 101, 8), "M44 78Q28 101 44 124L38 126Q20 101 38 76Z", Bar(42, 79, 42, 123, 2), "M30 96L16 101 30 106Z" },
            },
            // 燐弓: a tall bow at the front hand, an arrow on the string
            ["mist_archer"] = new Variant
            {
                Kind = "spearman",
                Drop = Spear,
                Add = new[] { "M62 36Q16 104 62 172L55 175Q6 104 55 33Z", Bar(60, 37, 60, 171, 2), Bar(28, 104, 104, 104, 3), "M30 98L14 104 30 110Z" },
            },
            // 燐刃: a blade in each hand, one low and forward, one raised
            ["twin_blade_warped"] = new Variant
            {
                Kind = "spearman",
                Drop = Spear,
                Add = new[] { Bar(64, 107, 14, 136, 6), "M17 130L2 146 12 140Z", Bar(104, 100, 54, 44, 6), "M57 47L46 30 51 49Z" },
            },
            // 晶槍: the spear's head is a crystal
            ["polearm_crystal"] = new Variant
            {
                Kind = "spearman",
                Drop = new[] { 8 },
                Add = new[] { "M34 146L6 160 0 190 22 178Z", "M42 142L30 130 24 146Z", "M26 170L12 168 14 182Z" },
            },
            ["shadow_hound"] = new Variant { Kind = "hound" },
            // 統牙 (elite): a mane of spines down its back
            ["pack_alpha"] = new Variant
            {
                Kind = "hound",
                Add = new[] { "M92 62L100 40 112 62Z", "M116 60L128 34 138 60Z", "M142 61L156 38 164 64Z", "M168 66L182 48 188 74Z", "M66 52L74 30 82 52Z" },
            },
            // 不退 (elite): a heater shield on the front arm, the spear behind it (review fix: a round disc on the shaft read as
            // a mace). Its rim is drawn inside the silhouette in the edge light's colour (`details`).
            ["polearm_unyielding"] = new Variant
            {
                Kind = "spearman",
                Add = new[] { "M28 78H82V106C82 128 69 141 55 150C41 141 28 128 28 106Z" },
                Details = new[] { "M34 84H76V106C76 124 66 134 55 142C44 134 34 124 34 106Z", "M55 86V138" },
            },
            ["armored_warden"] = new Variant { Kind = "warden" },
            ["miasma_priest"] = new Variant { Def = SerpentDef, Paths = SerpentPaths, Details = SerpentDetails },
            // 獄竜 ガルド: an angler's lure hanging before the jaws, spines under them
            ["abyss_angler"] = new Variant
            {
                Def = SerpentDef,
                Details = SerpentDetails,
                Paths = Concat(SerpentPaths,
                    "M72 8C60 0 40 0 26 10L29 15C42 7 58 7 68 14Z", Disc(21, 21, 8), "M28 34L20 50 34 40Z", "M40 38L36 54 46 42Z", "M54 40L52 56 60 42Z"),
            },
            // 歪みの根 (the last boss; scene-drawn, no DS figure is a root). Review 21 (a bare sapling of square-ended bars and a
            // 30 px trunk, lighter than the hero beside it): a gnarled trunk 60–90 px thick flaring into roots, branches that
            // taper to points, a crown of twisted shoots, and a knot like an eye in the edge light.
            ["distortion_root"] = new Variant
            {
                Def = new FigureArtDef { W = 220f, H = 264f, Body = 170f, Cx = 110f },
                Details = new[]
                {
                    "M92 166C92 154 116 152 118 164C120 178 94 180 92 166Z", "M100 166C102 162 108 162 110 166", "M80 214C88 200 92 188 90 176",
                    "M134 216C128 202 126 190 128 178",
                },
                Paths = new[]
                {
                    "M70 264C62 238 76 214 74 190C72 160 88 138 84 112C82 90 92 66 104 46L126 50C120 70 124 92 130 112C138 140 124 164 132 192C138 218 156 240 150 264Z",
                    "M24 264C40 248 58 238 76 232L90 248C70 252 50 258 40 264Z",
                    "M196 264C182 250 162 240 142 236L130 250C150 254 168 258 178 264Z",
                    "M52 264C70 240 88 228 110 228C134 228 152 242 168 264Z",
                    "M88 122C66 116 48 98 36 72C32 62 24 54 14 50C28 48 40 56 48 66C60 88 78 102 100 108Z",
                    "M84 156C62 154 42 162 24 178C36 158 56 146 82 142Z",
                    "M120 96C142 84 162 66 174 40C178 30 188 22 200 20C190 30 186 40 182 50C172 76 152 96 126 110Z",
                    "M128 134C152 130 176 138 196 154C176 146 152 144 130 148Z",
                    "M104 50C96 34 94 18 100 2C106 18 112 32 116 46Z",
                    "M118 52C124 34 136 20 152 10C144 24 134 38 126 56Z",
                    "M100 58C86 44 70 38 54 38C70 30 90 34 106 48Z",
                    "M66 90C58 84 50 82 40 84C48 76 60 76 70 82Z",
                    "M150 74C160 70 170 72 178 78C168 80 160 80 152 82Z",
                },
            },
        };

        /// <summary>silhouetteOf(side, artId, size): the player is the swordsman; an enemy by its id (VARIANTS), else warden (size ≥ 2) or spearman.</summary>
        // js: scenes/battle/field/art.js:143
        public static string SilhouetteOf(UnitSide side, string artId, int size)
        {
            if (side == UnitSide.Player) return "swordsman";
            if (!string.IsNullOrEmpty(artId) && Variants.ContainsKey(artId)) return artId;
            return size >= 2 ? "warden" : "spearman";
        }

        /// <summary>figureDef(OCB, key): the figure a silhouette key draws — a DS figure (OCB.FIGURES) or a roster id of VARIANTS.</summary>
        // js: scenes/battle/field/art.js:153
        public static FigureArtDef FigureDef(string key)
        {
            Variants.TryGetValue(key ?? "", out Variant v);
            if (v != null && v.Def != null)
            {
                return new FigureArtDef
                {
                    W = v.Def.W, H = v.Def.H, Body = v.Def.Body, Cx = v.Def.Cx,
                    Paths = (string[])v.Paths.Clone(),
                    Details = v.Details != null ? (string[])v.Details.Clone() : Array.Empty<string>(),
                };
            }
            // OCB.FIGURES[(v && v.kind) || key] || OCB.FIGURES.swordsman (DsFigures.Get falls back to the swordsman).
            var dsBase = DsFigures.Get(v != null && !string.IsNullOrEmpty(v.Kind) ? v.Kind : key);
            var drop = new HashSet<int>(v != null && v.Drop != null ? v.Drop : Array.Empty<int>());
            var paths = new List<string>();
            for (int i = 0; i < dsBase.Paths.Count; i++)
                if (!drop.Contains(i)) paths.Add(dsBase.Paths[i]);
            if (v != null && v.Add != null) paths.AddRange(v.Add);
            return new FigureArtDef
            {
                W = dsBase.W, H = dsBase.H, Body = dsBase.Body, Cx = dsBase.Cx,
                Paths = paths,
                Details = v != null && v.Details != null ? v.Details : Array.Empty<string>(),
            };
        }

        /// <summary>Unity's Outline (FigureView edge light): four copies of the shape offset by (±d, ±d) screen px.</summary>
        // js: scenes/battle/field/art.js:164
        private static readonly (int X, int Y)[] EdgeOffsets = { (1, 1), (1, -1), (-1, 1), (-1, -1) };

        /// <summary>
        /// drawFigure(def, o): OCB.Figure for a def, scaled to o.ScaleTo tall, with its edge light as Unity's Outline draws it —
        /// four copies of the silhouette in the rim colour offset (±2, ±2) screen px under the fill; `details` stroked over it.
        /// Built under parent at (0, 0); the element's size is the art's (w × scaleTo / h, scaleTo).
        /// </summary>
        // js: scenes/battle/field/art.js:175
        public static RectTransform DrawFigure(RectTransform parent, FigureArtDef def, DrawFigureOpts o = null)
        {
            o = o ?? new DrawFigureOpts();
            if (def == null) def = FigureDef("swordsman");
            // JS `o.fill || …` / `o.rim || …`: an empty string falls back to the default as a missing one does.
            string fill = !string.IsNullOrEmpty(o.Fill) ? o.Fill : (o.Foe ? "var(--figure-enemy)" : "var(--figure-self)");
            string rim = !string.IsNullOrEmpty(o.Rim) ? o.Rim : (o.Foe ? Tok("violet", 0.35f) : Tok("amber", 0.28f));
            float h = o.ScaleTo > 0f ? o.ScaleTo : def.H;
            float w = o.ScaleTo > 0f && def.H > 0f ? def.W * o.ScaleTo / def.H : def.W;
            // class 'ocb-figure-svg', overflow visible, display block: the mesh may reach outside its box.
            PathGraphic s = PathGraphic.Make(parent, 0f, 0f, w, h, "ocb-figure-svg");
            s.ViewBox = new RectF(0f, 0f, def.W, def.H);
            // Screen px → the figure's own units (the edge stays 2 px on the screen at every height).
            float unit = o.ScaleTo > 0f ? def.H / o.ScaleTo : 1f;
            float d = FigureFrame.EdgeLightWidth * unit;
            string[] paths = ToArray(def.Paths);
            bool rimShown = !IsTransparent(rim);
            Rgba rimColor = rimShown ? CssColor.Parse(rim) : Rgba.Transparent;
            if (rimShown)
            {
                foreach ((int sx, int sy) in EdgeOffsets)
                {
                    var gr = new PathLayer(paths).WithFill(rimColor);
                    gr.Transform = Affine.Translate(sx * d, sy * d);
                    s.Add(gr);
                }
            }
            var gf = new PathLayer(paths);
            if (o.Light != null)
            {
                // Review 21 (the stance's body light, §5.11 順 2 「身体に薄い光」, read as a flat maroon cut-out): a light, not a
                // fill — the stance colour strongest at the chest and gone before the silhouette's edge, so the figure's own
                // edge light stays (Unity's FigureView.BuildStanceGlow is a radial ProceduralArt.Glow). In the figure's own
                // units (an SVG radialGradient, userSpaceOnUse: stop-color with stop-opacity, straight interpolation).
                Rgba lc = CssColor.Parse(o.Light.Color);
                var stops = new List<GradientStop>();
                foreach ((float at, float a) in o.Light.Stops) stops.Add(GradientStop.At(lc.WithAlpha(lc.A * a), at));
                gf.WithFill(RadialGradient.Points(new Vec2(def.Cx, def.H * (1f - o.Light.At)), 0f, def.H * o.Light.R, stops.ToArray()));
            }
            else gf.WithFill(FillColor(fill));
            s.Add(gf);
            if (rimShown && def.Details != null && def.Details.Count > 0)
            {
                var gd = new PathLayer(ToArray(def.Details));
                gd.Stroke = new SolidPaint(rimColor);
                gd.StrokeStyle = new StrokeStyle(2f * unit, Cap.Butt, Join.Round);
                s.Add(gd);
            }
            return s.rectTransform;
        }

        /// <summary>SHIELD_PATH: ProceduralArt.Shield's outline (straight top, pointed bottom) in a 64×72 box.</summary>
        // js: scenes/battle/field/art.js:238
        public const string ShieldPath = "M3 2 H61 V31 C61 50 46 63 32 70 C18 63 3 50 3 31 Z";

        // ---- helpers --------------------------------------------------------------------------------------------

        /// <summary>A fill colour: 'currentColor' is white (the graphic's tint gives it its colour), else the CSS colour.</summary>
        private static Rgba FillColor(string css)
        {
            if (string.Equals(css?.Trim(), "currentColor", StringComparison.OrdinalIgnoreCase)) return Rgba.White;
            return CssColor.Parse(css);
        }

        private static bool IsTransparent(string css) => string.Equals(css?.Trim(), "transparent", StringComparison.OrdinalIgnoreCase);

        private static string[] ToArray(IReadOnlyList<string> list)
        {
            if (list == null) return Array.Empty<string>();
            var o = new string[list.Count];
            for (int i = 0; i < o.Length; i++) o[i] = list[i];
            return o;
        }

        /// <summary>JS Math.round: half up (toward +∞).</summary>
        private static long JsRound(double v) => (long)Math.Floor(v + 0.5);

        /// <summary>JS Number.prototype.toFixed(n) (no "-0": JS writes −0 as "0").</summary>
        private static string Fixed(double v, int digits)
        {
            string s = (v + 0d).ToString("F" + digits, CultureInfo.InvariantCulture);
            // .NET writes a negative value that rounds to zero as "-0.0"; JS keeps the sign too (−0.04.toFixed(1) is "-0.0"),
            // but writes −0 itself as "0.0".
            return v == 0d ? (0d).ToString("F" + digits, CultureInfo.InvariantCulture) : s;
        }

        /// <summary>A JS number as String(n) writes it (integers plain, fractions shortest round-trip).</summary>
        private static string Num(double v) => (v + 0d).ToString("R", CultureInfo.InvariantCulture);
    }
}
