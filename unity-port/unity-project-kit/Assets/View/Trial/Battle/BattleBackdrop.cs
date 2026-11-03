// The battle's backdrop for the exploration's layer (docs/BATTLE_SCENE.md §6b, battle-views §2.1): BackdropSpec.For(layer)
// — the sky's stops, the dark earth from the horizon down, the hazes (top glow, band, radial, light shafts with the 560 px
// fade baked in) breathing on real time (BackdropMotion.FlickerAlpha), the motes (MoteAt / MoteAlpha), the horizon at
// y 555. 演出短縮 holds everything still. In layer.backdrop. Port of scenes/battle/field/backdrop.js.
//
// The layer's backdrop behind the battle: a port of unity-project-kit/Assets/View/Backdrop/BackdropLayers.cs (the sky
// gradient, the haze with its breathing light, the drifting specks). Every value and every motion comes from
// BackdropSpec / BackdropMotion (Backdrop.Script); this file only lays them out.
//
// One addition from the design system (README 「背景のテーマ」, layout.md 地平線): from the horizon at y 556 down the ground
// is dark, so the floor, the hand and the numbers always stand on dark ground. The earth is the ground token laid over the
// layer's own sky (field.css: 80% at the horizon, 92% at the bottom edge), so each layer's ground keeps its hue in shadow,
// and the light shafts fade out by the horizon (DS wakakusa-b: 光の筋は上から 560 px で消える). Motion runs on real time
// (BackdropMotion is a function of the clock); the battle speed and fast-forward do not touch it, as Unity's BackdropLayers
// reads unscaled time. 演出短縮 holds it still (battle_ui_ux_v2 §6.2).
//
// Meshes: the sky, the earth, the top glows and bands are the kit's linear gradients (exact); the light shafts are one
// mesh painted once (the JS canvas): each shaft a strip split at its profile's stops, the 560 px fade multiplied in
// (MeshBuilder.ApplyAlphaRamp, the canvas's destination-in) and cut at it; a radial haze is a fan of its closest-side
// radial gradient (alpha linear in r: exact along every spoke) instead of the kit's 6 px refinement, which would put tens
// of thousands of vertices into a haze 970 px across. Only opacities and the motes' places change per frame.
//
// Unity additions of 2026-11-03, which the JS does not have (こうだいさん's request, a deliberate departure from the JS look:
// a far view drawn in code until the painted art lands, and more sets of specks): each group of BackdropScenery is one
// static mesh over the stage between the sky and the earth, where the layer's art will go (battle-visual-v1 §2.4), and
// every set of BackdropSpec.Specks drifts as the JS's one set of motes does (the first set is LayerBackdrop.Motes, as before).
// Once a layer's art is painted (#858, Resources/Backdrops/layer-n, BackdropArt), it takes the far view's place over the
// sky, and the earth, the hazes and the specks stay on top of it; without the art the backdrop is drawn as before.
using System;
using System.Collections.Generic;
using System.Globalization;
using Backdrop;
using Trial.Kit;
using UnityEngine;
using UnityEngine.UI;
using CssColor = Trial.Geometry.CssColor;
using GRgba = Trial.Geometry.Rgba;
using MeshBuilder = Trial.Geometry.MeshBuilder;
using RectF = Trial.Geometry.RectF;
using Vec2 = Trial.Geometry.Vec2;

namespace Trial.Battle
{
    /// <summary>createBackdrop(env, layerNo) → { spec, tile, destroy() }.</summary>
    // js: scenes/battle/field/backdrop.js:31
    public sealed class BattleBackdrop
    {
        /// <summary>W / H: the stage.</summary>
        // js: scenes/battle/field/backdrop.js:17
        private const float W = 1920f;
        private const float H = 1080f;
        /// <summary>SHAFT_SPACING: BackdropLayers.ShaftSpacing: how far apart the shafts of one haze stand, as a share of the width.</summary>
        // js: scenes/battle/field/backdrop.js:20
        private const float ShaftSpacing = 0.18f;
        /// <summary>SHAFT_FADE_PX: DS wakakusa-b 「光の筋は上から 560 px で消える」: the shafts fade out by this depth from the top.</summary>
        // js: scenes/battle/field/backdrop.js:22
        private const float ShaftFadePx = 560f;
        /// <summary>ALPHA_STEP: a haze's opacity is written again only when it moved this much (review 16: fewer style writes).</summary>
        // js: scenes/battle/field/backdrop.js:24
        private const float AlphaStep = 0.004f;

        /// <summary>How many rows a shaft's strip is cut into (the 560 px fade is multiplied per vertex: 36 px rows keep it within .01).</summary>
        private const int ShaftRows = 48;
        /// <summary>How many spokes a radial haze's fan has (the chord error at 128 is under .0003 of its alpha).</summary>
        private const int RadialSpokes = 128;

        // ---- field.css:7-15 (verbatim) ----------------------------------------------------------------------------

        /// <summary>field.css:7 .ocbg-battle .ocbg-battle-backdrop</summary>
        private const string BackdropCss = "position: absolute; left: 0; top: 0; width: 1920px; height: 1080px; overflow: hidden; background: var(--ground);";
        /// <summary>field.css:9 .ocbg-battle .ocbg-battle-earth (its background image)</summary>
        private const string EarthImage = "linear-gradient(180deg, color-mix(in srgb, var(--ground) 80%, transparent), color-mix(in srgb, var(--ground) 92%, transparent))";
        /// <summary>field.css:9 .ocbg-battle .ocbg-battle-earth: left 0, top 556, 1920 × 524.</summary>
        private const float EarthTop = 556f;
        private const float EarthH = 524f;
        /// <summary>field.css:15 .ocbg-battle .ocbg-battle-horizon</summary>
        private const string HorizonCss = "position: absolute; left: 0; top: 555px; width: 1920px; height: 1px; background: color-mix(in srgb, var(--ink) 22%, transparent);";
        private const float HorizonTop = 555f;

        /// <summary>One haze element and the haze it draws ({ el, haze, drawn }).</summary>
        private sealed class HazeEl
        {
            public VectorGraphic El;
            public Haze Haze;
            public float? Drawn;
        }

        /// <summary>One set of specks and its elements (Unity addition of 2026-11-03: the JS draws LayerBackdrop.Motes alone).</summary>
        private sealed class SpeckSet
        {
            public Motes Motes;
            public readonly List<GradientGraphic> Els = new List<GradientGraphic>();
        }

        private readonly BattleViewEnv _env;
        private RectTransform _root;
        private readonly List<HazeEl> _hazes = new List<HazeEl>();
        private readonly List<SpeckSet> _specks = new List<SpeckSet>();
        private bool _drawnStill;
        private Action _off;

        private BattleBackdrop(BattleViewEnv env, int layerNo)
        {
            _env = env;
            try { Spec = BackdropSpec.For(layerNo); }
            catch (Exception e) { env?.Log.Error(null, e); }
            if (env != null && env.Layer.Backdrop) _root = Ui.Div(env.Layer.Backdrop, "ocbg-battle-backdrop", 0f, 0f, L.W, L.H);
            if (!_root) return;
            // field.css:7: background var(--ground), overflow hidden
            BoxGraphic.On(_root, BackdropCss);
            _root.gameObject.AddComponent<RectMask2D>();
            if (Spec == null)
            {
                // js: scenes/battle/field/backdrop.js:37 (the earth alone, style 'top:0;height:1080px')
                NoFringe(GradientGraphic.Css(_root, EarthImage, 0f, 0f, W, H, false, "ocbg-battle-earth"));
                return;
            }
            Build(Spec);
            Animate();
            if (env.Clock != null) _off = env.Clock.OnFrame((dt, dtReal) => Animate());
        }

        /// <summary>createBackdrop(env, layerNo): the layer's backdrop in layer.backdrop (a spec that fails: the earth alone, full height).</summary>
        // js: scenes/battle/field/backdrop.js:31
        public static BattleBackdrop Create(BattleViewEnv env, int layerNo) => new BattleBackdrop(env, layerNo);

        /// <summary>The sky, the layer's art or the far view (Unity additions), the earth, the hazes, the motes and the horizon, bottom → top.</summary>
        // js: scenes/battle/field/backdrop.js:41
        private void Build(LayerBackdrop spec)
        {
            // ---- the sky: the stops over the whole height, as BackdropLayers' SkySprite (At 0 top → 1 bottom) ----
            var stops = new List<string>();
            foreach (SkyStop s in spec.Sky ?? Array.Empty<SkyStop>()) stops.Add(RgbaCss(s.Color) + " " + Fixed(s.At * 100f, 2) + "%");
            NoFringe(GradientGraphic.Css(_root, "linear-gradient(180deg, " + string.Join(", ", stops) + ")", 0f, 0f, W, H, false, "ocbg-battle-sky"));

            // ---- the layer's art, or while it has none the far view (Unity additions of 2026-11-03, こうだいさん's
            // requests: a far view drawn in code, and a slot for the painted art, #858; the JS has neither) ----
            // A group that fails is logged like a spec that fails, and the backdrop draws on without the rest of the far view.
            bool hasArt = LayArt(_root, spec);
            if (BackdropArt.DrawsFarView(hasArt))
            {
                try
                {
                    foreach (SceneryMesh mesh in BackdropScenery.Meshes(spec)) SceneryElement(_root, mesh);
                }
                catch (Exception e) { _env?.Log.Error(null, e); }
            }

            // ---- the earth (DS addition): dark from the horizon down ----
            NoFringe(GradientGraphic.Css(_root, EarthImage, 0f, EarthTop, W, EarthH, false, "ocbg-battle-earth"));

            // ---- the hazes, each with its own breath (BackdropMotion.FlickerAlpha) ----
            foreach (Haze haze in spec.Hazes ?? Array.Empty<Haze>())
            {
                if (haze == null) continue;
                foreach (VectorGraphic el in HazeElements(_root, haze)) _hazes.Add(new HazeEl { El = el, Haze = haze });
            }

            // ---- the specks (BackdropMotion.MoteAt / MoteAlpha) ----
            // Unity addition of 2026-11-03 (こうだいさん's request: more sets of specks; the JS draws spec.Motes alone): every
            // set of BackdropSpec.Specks, each drawn as the JS draws its one.
            foreach (Motes m in BackdropSpec.Specks(spec))
            {
                var set = new SpeckSet { Motes = m };
                float size = m.SizePx;
                string image = "radial-gradient(closest-side, " + RgbaCss(m.Color) + ", " + RgbaCss(m.Color, 0.6f) + " 60%, transparent)";
                for (int i = 0; i < m.Count; i++)
                {
                    // .ocbg-battle-mote: border-radius 50%, margin −size/2 (the transform places its middle)
                    GradientGraphic el = GradientGraphic.Css(_root, image, -size / 2f, -size / 2f, size, size, true, "ocbg-battle-mote");
                    set.Els.Add(el);
                }
                _specks.Add(set);
            }

            // ---- the horizon (layout.md: y 556) ----
            BoxGraphic.Make(_root, HorizonCss, 0f, HorizonTop, W, 1f, "ocbg-battle-horizon");
        }

        /// <summary>
        /// The layer's painted art (Resources/Backdrops/layer-n, the path the old battle screen reads too) over the whole
        /// stage, cropped to fill it; false while the layer has none. The earth, the hazes and the specks go on top of it.
        /// </summary>
        private static bool LayArt(RectTransform root, LayerBackdrop spec)
        {
            Sprite art = ArtImage.Load(BackdropArt.ResourcePath(spec.Layer));
            if (!art) return false;
            ArtCrop c = BackdropArt.Cover(art.rect.width, art.rect.height, W, H);
            return ArtImage.Lay(root, art, 0f, 0f, W, H, c.X, c.Y, c.W, c.H, "ocbg-battle-art") != null;
        }

        /// <summary>A full-width fill: no anti-aliased fringe (CSS draws a pixel-aligned box edge hard).</summary>
        private static void NoFringe(GradientGraphic g)
        {
            if (g) g.Fringe = false;
        }

        /// <summary>
        /// stillAt(flicker): battle_ui_ux_v2 §6.2 (常時の揺れ): with 演出短縮 on, the flickers and the drift hold still. Each haze
        /// stands at the light it keeps between its beats (a breath at its middle, a flash at its rest) and each speck where and
        /// as lit as it is at time 0 — the motion functions read at fixed moments.
        /// </summary>
        // js: scenes/battle/field/backdrop.js:83
        private static double StillAt(Flicker flicker) =>
            !flicker.Moves ? 0.0 : flicker.Flash ? BackdropMotion.FlashMs : flicker.PeriodMs / 4.0;

        /// <summary>animate(): every frame (real time) the hazes' breath and the specks' drift; once and then nothing under 演出短縮.</summary>
        // js: scenes/battle/field/backdrop.js:85
        private void Animate()
        {
            if (!_root) return;
            bool still = _env != null && _env.Settings != null && _env.Settings.ShortFx;
            if (still && _drawnStill) return;
            _drawnStill = still;
            double now = _env != null ? _env.Real.Now : 0.0;
            foreach (HazeEl hz in _hazes)
            {
                float a = BackdropMotion.FlickerAlpha(hz.Haze.Flicker, still ? StillAt(hz.Haze.Flicker) : now);
                if (hz.Drawn.HasValue && Math.Abs(a - hz.Drawn.Value) < AlphaStep) continue;
                hz.Drawn = a;
                if (hz.El) hz.El.Opacity = Round(a, 3);
            }
            double t = still ? 0.0 : now;
            foreach (SpeckSet set in _specks)
            {
                Motes m = set.Motes;
                float half = m.SizePx / 2f;
                for (int i = 0; i < set.Els.Count; i++)
                {
                    GradientGraphic el = set.Els[i];
                    if (!el) continue;
                    BackdropMotion.MoteAt(m, i, t, W, H, out float x, out float y);
                    // transform: translate(x.toFixed(1)px, y.toFixed(1)px) over margin −size/2
                    el.rectTransform.anchoredPosition = new Vector2(Round(x, 1) - half, -(Round(y, 1) - half));
                    el.Opacity = Round(BackdropMotion.MoteAlpha(m, i, t), 3);
                }
            }
        }

        /// <summary>spec: BackdropSpec.For(layerNo), or null when it failed.</summary>
        // js: scenes/battle/field/backdrop.js:107
        public LayerBackdrop Spec { get; }

        /// <summary>tile: 床のタイル, the fill of a floor cell on this layer (LayerBackdrop.Tile); null without a spec.</summary>
        // js: scenes/battle/field/backdrop.js:109
        public global::Backdrop.Rgba? Tile => Spec != null ? Spec.Tile : (global::Backdrop.Rgba?)null;

        /// <summary>destroy(): the frame loop off, the backdrop gone.</summary>
        // js: scenes/battle/field/backdrop.js:110
        public void Destroy()
        {
            _off?.Invoke();
            _off = null;
            if (_root) Ui.Remove(_root);
            _root = null;
            _hazes.Clear();
            _specks.Clear();
        }

        // ---- the hazes ---------------------------------------------------------------------------------------

        /// <summary>hazeElements(haze) = BackdropLayers.HazeImages: the elements one haze draws, each carrying the haze's colour at its own alpha.</summary>
        // js: scenes/battle/field/backdrop.js:118
        private static List<VectorGraphic> HazeElements(RectTransform root, Haze haze)
        {
            global::Backdrop.Rgba c = haze.Color;
            var output = new List<VectorGraphic>();
            switch (haze.Shape)
            {
                case HazeShape.TopGlow:
                {
                    // Light from above: the colour at the top, gone at Size of the height.
                    string image = "linear-gradient(180deg, " + RgbaCss(c) + " 0%, " + RgbaCss(c, 0f) + " " + Fixed(haze.Size * 100f, 2) + "%)";
                    GradientGraphic el = GradientGraphic.Css(root, image, 0f, 0f, W, H, false, "ocbg-battle-haze");
                    NoFringe(el);
                    output.Add(el);
                    break;
                }
                case HazeShape.Band:
                {
                    // A level band centred on Y, Size of the height across, soft at both edges.
                    float half = haze.Size / 2f;
                    string image = "linear-gradient(180deg, " + RgbaCss(c, 0f) + " " + Fixed((haze.Y - half) * 100f, 2) + "%, "
                        + RgbaCss(c) + " " + Fixed(haze.Y * 100f, 2) + "%, " + RgbaCss(c, 0f) + " " + Fixed((haze.Y + half) * 100f, 2) + "%)";
                    GradientGraphic el = GradientGraphic.Css(root, image, 0f, 0f, W, H, false, "ocbg-battle-haze");
                    NoFringe(el);
                    output.Add(el);
                    break;
                }
                case HazeShape.Radial:
                {
                    // A soft round glow at (X, Y), Size of the height across, alpha 1 − d:
                    // .ocbg-battle-haze.is-radial { border-radius: 50% } with
                    // background: radial-gradient(closest-side, rgba(c), rgba(c, 0)).
                    float across = haze.Size * H;
                    float left = Round(haze.X * W - across / 2f, 1);
                    float top = Round(haze.Y * H - across / 2f, 1);
                    float size = Round(across, 1);
                    GRgba centre = CssColor.Parse(RgbaCss(c));
                    GRgba rim = CssColor.Parse(RgbaCss(c, 0f));
                    VectorGraphic el = VectorGraphic.Draw(root, left, top, size, size, (mb, box, feather) => RadialFan(mb, box, centre, rim),
                        "ocbg-battle-haze is-radial");
                    output.Add(el);
                    break;
                }
                case HazeShape.Shafts:
                {
                    // Count shafts of light, Size of the width each, leaning Angle degrees from upright (Unity rotates by −Angle
                    // about z, clockwise, which CSS and the canvas write as +Angle), 1.6 × the height long. Review 16 (layers 1, 2
                    // and 4 ran at 15–20 fps): the shafts are painted once into one surface, the horizon's fade (DS wakakusa-b:
                    // 光の筋は上から 560 px で消える) baked in, and only the surface's opacity moves each frame.
                    GRgba lit = CssColor.Parse(RgbaCss(c));
                    VectorGraphic el = VectorGraphic.Draw(root, 0f, 0f, W, H, (mb, box, feather) => PaintShafts(mb, haze, lit),
                        "ocbg-battle-shafts");
                    output.Add(el);
                    break;
                }
                default:
                    break;
            }
            return output;
        }

        /// <summary>
        /// The canvas of the shafts (backdrop.js:158-185): for shaft i, translate(X·W + (i − (Count−1)/2)·spacing, H/2), rotate(Angle),
        /// a linear gradient from −w/2 to w/2 (rgba(c, 0) → c at .25 → c at .75 → rgba(c, 0)) filling (−w/2, −0.8H, w, 1.6H); then
        /// destination-in with a vertical gradient (alpha 1 at y 0 → 0 at y 560).
        /// </summary>
        // js: scenes/battle/field/backdrop.js:156
        private static void PaintShafts(MeshBuilder mb, Haze haze, GRgba c)
        {
            float width = haze.Size * W;
            float spacing = W * ShaftSpacing;
            double rad = haze.Angle * Math.PI / 180.0;
            float cos = (float)Math.Cos(rad);
            float sin = (float)Math.Sin(rad);
            // ShaftSprite: full in the middle half, falling to nothing at the sides (the gradient's stops along u)
            float[] us = { -width / 2f, -width / 4f, width / 4f, width / 2f };
            float[] ks = { 0f, 1f, 1f, 0f };
            float v0 = -H * 0.8f;
            float len = H * 1.6f;
            for (int i = 0; i < haze.Count; i++)
            {
                float x = haze.X * W + (i - (haze.Count - 1) / 2f) * spacing;
                int b = mb.VertexCount;
                for (int r = 0; r <= ShaftRows; r++)
                {
                    float v = v0 + len * r / ShaftRows;
                    for (int k = 0; k < 4; k++)
                    {
                        float u = us[k];
                        // canvas rotate(θ) on a y-down surface: (u, v) → (u cos θ − v sin θ, u sin θ + v cos θ)
                        var p = new Vec2(x + u * cos - v * sin, H / 2f + u * sin + v * cos);
                        mb.AddVertex(p, c.MulAlpha(ks[k]));
                    }
                }
                for (int r = 0; r < ShaftRows; r++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int a0 = b + r * 4 + k, a1 = a0 + 1, b0 = a0 + 4, b1 = b0 + 1;
                        mb.AddTri(a0, a1, b1);
                        mb.AddTri(a0, b1, b0);
                    }
                }
            }
            // globalCompositeOperation = 'destination-in' with createLinearGradient(0, 0, 0, SHAFT_FADE_PX): 1 → 0 (held past the ends)
            mb.ApplyAlphaRamp(true, new List<(float at, float alpha)> { (0f, 1f), (ShaftFadePx, 0f) });
            // what lies under the fade's end is transparent, and the canvas ends at the stage's edges
            mb.ClipConvex(new List<Vec2> { new Vec2(0f, 0f), new Vec2(W, 0f), new Vec2(W, ShaftFadePx), new Vec2(0f, ShaftFadePx) });
        }

        /// <summary>radial-gradient(closest-side, centre, rim) on its box with border-radius 50%: a fan from the middle to the ellipse.</summary>
        private static void RadialFan(MeshBuilder mb, RectF box, GRgba centre, GRgba rim)
        {
            if (box.W <= 0f || box.H <= 0f) return;
            float cx = box.X + box.W / 2f, cy = box.Y + box.H / 2f;
            float rx = box.W / 2f, ry = box.H / 2f;
            int mid = mb.AddVertex(new Vec2(cx, cy), centre);
            int first = mb.VertexCount;
            for (int k = 0; k < RadialSpokes; k++)
            {
                double a = 2.0 * Math.PI * k / RadialSpokes;
                mb.AddVertex(new Vec2(cx + rx * (float)Math.Cos(a), cy + ry * (float)Math.Sin(a)), rim);
            }
            for (int k = 0; k < RadialSpokes; k++) mb.AddTri(mid, first + k, first + (k + 1) % RadialSpokes);
        }

        // ---- the far view (Unity addition of 2026-11-03; the JS has none) ------------------------------------

        /// <summary>
        /// One group of BackdropScenery as a static graphic over the stage: its corners scaled from fractions of the stage to
        /// px, each in its own colour (rgba(c) parsed, as the hazes take theirs), laid straight over what is under it.
        /// </summary>
        private static void SceneryElement(RectTransform root, SceneryMesh mesh)
        {
            SceneryVertex[] vs = mesh?.Vertices ?? Array.Empty<SceneryVertex>();
            int[] tris = mesh?.Triangles ?? Array.Empty<int>();
            if (vs.Length == 0 || tris.Length < 3) return;
            // converted once here, so a rebuild of the geometry (the stage resized) only copies them
            var pos = new Vec2[vs.Length];
            var col = new GRgba[vs.Length];
            for (int i = 0; i < vs.Length; i++)
            {
                pos[i] = new Vec2(vs[i].X * W, vs[i].Y * H);
                col[i] = CssColor.Parse(RgbaCss(vs[i].Color));
            }
            VectorGraphic.Draw(root, 0f, 0f, W, H, (mb, box, feather) =>
            {
                int b = mb.VertexCount;
                for (int i = 0; i < pos.Length; i++) mb.AddVertex(pos[i], col[i]);
                // VectorGraphic drops only an index past the end; a negative one would reach uGUI, so a corner the mesh lacks drops its triangle here
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int p = tris[t], q = tris[t + 1], r = tris[t + 2];
                    if ((uint)p >= (uint)pos.Length || (uint)q >= (uint)pos.Length || (uint)r >= (uint)pos.Length) continue;
                    mb.AddTri(b + p, b + q, b + r);
                }
            }, "ocbg-battle-scenery");
        }

        // ---- helpers ---------------------------------------------------------------------------------------

        /// <summary>rgba(c, mult) of field/art.js:15 (import { rgba } from './art.js'): FieldArt.RgbaCss.</summary>
        private static string RgbaCss(global::Backdrop.Rgba c, float mult = 1f) => FieldArt.RgbaCss(c, mult);

        /// <summary>n.toFixed(digits).</summary>
        private static string Fixed(float v, int digits) =>
            Math.Round((double)v, digits, MidpointRounding.AwayFromZero).ToString("F" + digits, CultureInfo.InvariantCulture);

        /// <summary>The number a style write of toFixed(digits) reads back as.</summary>
        private static float Round(float v, int digits) => (float)Math.Round(v, digits, MidpointRounding.AwayFromZero);
    }
}
