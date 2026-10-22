// The field's effects (DepictionFx, docs/BATTLE_SCENE.md §6b, battle-views §2.4): the blows (斬 arc, 突 line + impact, 払
// band + motes, 打 ripple), the grammar marks (盾 ring, 型 crest + light, 気 sigil), the trait accent, the burst, the floating
// numbers and words (dressed as the DS FloatNum, placed under the badges and the floats on screen, on runPaced), the
// shield block (whole or breaking) and the ghosts (a card to its pile, the discard sheet onto the draw pile). Layers fx and
// float. Port of scenes/battle/field/fx.js.
//
// js: scenes/battle/field/fx.js:1 — Transient effects of the battle: a port of DepictionFx.cs (and DepictionPlayer's
// FlyGhostTo / SlideDiscardOntoDraw) on the scene's clock. Strength comes in as the 1..4 tier the script decided; the shape
// as the system the script wrote. Every length is the nominal ms (EffectCatalog at ×1.25), run by env.clock, so the battle
// speed, 演出短縮 and fast-forward stretch it. Looks: the shapes are Unity's (ProceduralArt sprites as CSS); the floating
// numbers wear the design system's FloatNum (battle-visual-v1 §5.5: a black 80% tag, an icon with every number, HP in the
// HP ink, Guard in the Guard colour) at the size DepictionFx.NumberFont gives the tier.
//
// How the DOM becomes uGUI here:
// - piece(): a div with a UiNode (its box in stage px, its CSS transform, its opacity); its CSS background, the SVGs inside
//   it and its filter are kit graphics laid over its box as children, so the node's transform and opacity carry them.
// - svg(): the SVG elements (ringSvg, the slash arc, the shield) are PathGraphic layers with the same paths, strokes,
//   gradients and dashes; `pathLength` 1000 becomes the arc's measured length.
// - filter: drop-shadow() (no general form in the kit): on the SVGs, the kit's stroke / fill shadow of each painted layer,
//   weighted by that layer's share of the content's alpha; on the gradient bands (突, 払), a graphic under the band whose
//   alpha is the band's alpha blurred with the Gaussian of σ = the blur (Blur.SigmaOfDropShadow) — exact for the bands'
//   separable alpha (DropShadowOf below).
// - mask-image (払's band, which the kit parses but does not draw): the band's paint times the mask's alpha (MaskedPaint);
//   CSS masks after the filter and only inside the border box, so the band's drop-shadow is masked too and cut at its box.
// - The float tags are the DS FloatNum rules (Ocb.Float*Css) cascaded with field.css's float rules in specificity order.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Depiction;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    /// <summary>blowRoom's answer: how far a blow's shape may reach left and right of the chest (∞ = no limit).</summary>
    // js: scenes/battle/field.js:440
    public readonly struct BlowRoom
    {
        public readonly float Left;
        public readonly float Right;

        public BlowRoom(float left, float right)
        {
            Left = left;
            Right = right;
        }

        public static BlowRoom None => new BlowRoom(float.PositiveInfinity, float.PositiveInfinity);
    }

    /// <summary>fx.strike's o: { colorName, tier, towardLeft, system, ms, room, half }.</summary>
    // js: scenes/battle/field/fx.js:327
    public sealed class FxStrikeArgs
    {
        /// <summary>colorName: the token ('ink' for the player's blow, 'violet' for an enemy's).</summary>
        public string ColorName = "ink";
        /// <summary>tier: Cue.Intensity (clamped 1..4, default 2).</summary>
        public int Tier = 2;
        /// <summary>towardLeft: the blow travels right to left (the enemy's).</summary>
        public bool TowardLeft;
        public StrikeSystem System;
        public float Ms;
        /// <summary>room: field.js blowRoom (null: no limit).</summary>
        public BlowRoom? Room;
        /// <summary>half: the struck body's half width (0 = 50).</summary>
        public float Half;
    }

    /// <summary>fx.mark's p: { chest, head, feet, facing, fx, tier, ms, crestLight, warm, trackX }.</summary>
    // js: scenes/battle/field/fx.js:351
    public sealed class FxMarkArgs
    {
        public StagePoint Chest;
        public StagePoint Head;
        public StagePoint Feet;
        /// <summary>facing: +1 faces right (the player), −1 left.</summary>
        public int Facing = 1;
        public FxSystem? Fx;
        public int Tier = 2;
        public float Ms;
        public bool CrestLight = true;
        /// <summary>warm: the unit is the player's (amber / wick; violet / phosphor on the enemy).</summary>
        public bool Warm;
        /// <summary>trackX(): the figure's x now (the ring and the crest ride with it); null = they stay.</summary>
        public Func<float?> TrackX;
    }

    /// <summary>createFx(env) → { strike, mark, traitAccent, burst, floatText, shieldBlock, ghost, clear } (fx.js:801).</summary>
    // js: scenes/battle/field/fx.js:92
    public sealed class FieldFx
    {
        // ---- constants (fx.js:15-60) ------------------------------------------------------------------

        /// <summary>DepictionFx: the blow's length and thickness by tier 1..4.</summary>
        // js: scenes/battle/field/fx.js:16
        private static readonly float[] SlashLength = { 300f, 380f, 460f, 560f };
        // js: scenes/battle/field/fx.js:17
        private static readonly float[] SlashThickness = { 8f, 12f, 16f, 22f };
        /// <summary>Review 19: a blow's shape runs this far past the struck body's far edge, and reaches at least BLOW_MIN either side.</summary>
        // js: scenes/battle/field/fx.js:19
        private const float BlowPast = 48f;
        // js: scenes/battle/field/fx.js:20
        private const float BlowMin = 40f;
        /// <summary>DepictionFx.MarkScale (§5.3 大きさ): 0.6 / 1.0 / 1.3 / 1.7 by tier.</summary>
        // js: scenes/battle/field/fx.js:22
        private static readonly float[] MarkScale = { 0.6f, 1f, 1.3f, 1.7f };
        /// <summary>
        /// battle_ui_ux_v2 §5.0 / the DS Notices (浮かぶ数字; review 19): a floating number rises from the chest to the head in
        /// 600 ms (ease-out). The rise is the caller's (the figure's chest-to-head span); DEFAULT_RISE is the 226 px figure's
        /// (0.19 × 226).
        /// </summary>
        // js: scenes/battle/field/fx.js:29
        private const float FloatMs = 600f;
        // js: scenes/battle/field/fx.js:30
        private const float DefaultRise = 43f;
        /// <summary>DepictionFx.FloatRoutine: a float starts at 1.35 × its size and settles to 1 in its first 18 %.</summary>
        // js: scenes/battle/field/fx.js:32
        private const float PopScale = 1.35f;
        /// <summary>DepictionFx.BurstRoutine.</summary>
        // js: scenes/battle/field/fx.js:34
        private const float BurstMs = 320f;
        /// <summary>
        /// Scene addition (review fix): a float never crosses the path of a float still on screen. It stands this far under the
        /// lowest one in its way (and its widths count this much wider), so the floats of one action read top to bottom in the
        /// order they came.
        /// </summary>
        // js: scenes/battle/field/fx.js:42
        private const float FloatStackGap = 6f;
        // js: scenes/battle/field/fx.js:43
        private const float FloatStackMarginX = 4f;
        /// <summary>A float this faded (or more) no longer holds its place: what comes next may stand on it.</summary>
        // js: scenes/battle/field/fx.js:45
        private const float FloatStackMinAlpha = 0.15f;
        /// <summary>A float set beside the others (its column would reach under o.lowest) keeps this far from the stage's edges.</summary>
        // js: scenes/battle/field/fx.js:47
        private const float FloatStageW = 1920f;
        // js: scenes/battle/field/fx.js:48
        private const float FloatStageEdge = 16f;

        /// <summary>
        /// The blocked blow's shield (review 17): 100×114, about two thirds of Unity's 150×170, its face see-through; a broken
        /// one's crack (in the 64×72 box of SHIELD_PATH) and how its halves part.
        /// </summary>
        // js: scenes/battle/field/fx.js:54
        private const float ShieldW = 100f;
        private const float ShieldH = 114f;
        // js: scenes/battle/field/fx.js:55
        private const string ShieldCrack = "M33 3 L29 17 L36 29 L30 43 L35 56 L32 69";
        // js: scenes/battle/field/fx.js:56
        private const float ShieldPartDx = 16f;
        private const float ShieldPartDy = 12f;
        private const float ShieldPartDeg = 14f;

        /// <summary>The played card's ghost (o.card): the card is 216 wide (battle-visual-v1 §6), shrunk to the ghost; it starts at 90 %.</summary>
        // js: scenes/battle/field/fx.js:59
        private const float GhostCardW = 216f;
        // js: scenes/battle/field/fx.js:60
        private const float GhostCardAlpha = 0.9f;

        /// <summary>The 打 ring's line as a share of its size (scene choice, review 17; Unity's sprite gave a tenth).</summary>
        // js: scenes/battle/field/fx.js:88
        private const float RippleLine = 0.04f;
        /// <summary>Review 17: the lines of the rings that read as light, not as prototype sprites (px at tier 2, MARK_SCALE 1).</summary>
        // js: scenes/battle/field/fx.js:90
        private const float RingLineStand = 4f;
        private const float RingLineCrestOuter = 3f;
        private const float RingLineCrestInner = 2f;
        // (RING_LINE.accentOuter 2.5 / accentInner 2: the trait accent's rings, which review 19 removed; nothing reads them.)

        // ---- the CSS of the floats (field.css:99-110; the DS rules are Ocb.Float*Css, bundle.css:159-163) ----------------

        /// <summary>field.css:99 <c>.ocbg-battle .ocbg-battle-float.is-word, .ocbg-battle .ocbg-battle-float.is-miss</c></summary>
        private const string FloatWordCss = "padding: 0 10px;";
        /// <summary>field.css:105 <c>.ocbg-battle .ocbg-battle-float</c></summary>
        private const string FloatSceneCss = "position: absolute; z-index: 1; will-change: transform, opacity;";
        /// <summary>field.css:106 <c>.ocbg-battle .ocbg-battle-float b</c></summary>
        private const string FloatSceneBCss = "font-family: var(--font-mono); font-weight: 700; white-space: nowrap;";
        /// <summary>field.css:107 <c>.ocbg-battle .ocbg-battle-float b.is-word</c></summary>
        private const string FloatSceneBWordCss = "font-family: var(--font-body);";
        /// <summary>field.css:108 <c>.ocbg-battle .ocbg-battle-float.is-miss b</c></summary>
        private const string FloatSceneMissBCss = "font-family: var(--font-body); color: var(--ink); text-decoration: line-through 3px var(--miss);";
        /// <summary>field.css:109 <c>.ocbg-battle .ocbg-battle-float small</c></summary>
        private const string FloatSceneSmallCss = "white-space: nowrap;";
        /// <summary>field.css:113 <c>.ocbg-battle .ocbg-battle-float .ocbg-battle-float-side</c></summary>
        private const string FloatSideCss = "font: 400 16px/1 var(--font-body); color: var(--ink); margin-left: 6px; white-space: nowrap;";
        /// <summary>field.css:113: the side label's margin-left (a flex item margin).</summary>
        private const float FloatSideMarginLeft = 6f;

        /// <summary>field.css:91 <c>.ocbg-battle .ocbg-battle-ghost</c></summary>
        private const string GhostCss = "border-radius: var(--radius-panel); background: color-mix(in srgb, var(--ink) 55%, transparent);";
        /// <summary>field.css:94 <c>.ocbg-battle .ocbg-battle-sheet</c></summary>
        private const string SheetCss = "box-sizing: border-box; border-radius: var(--radius-plate); background: var(--card); border: var(--stroke-rule) solid var(--steel);";

        /// <summary>fx.js dress: a cause and its number ("出血 −3", "Guard −2"); JS `\d` is ASCII.</summary>
        // js: scenes/battle/field/fx.js:508
        private static readonly Regex CauseAndNumber = new Regex(@"^(.*\S)\s+([+\-−]?[0-9]+)\z", RegexOptions.CultureInvariant);
        /// <summary>fx.js dress: a number alone.</summary>
        // js: scenes/battle/field/fx.js:509
        private static readonly Regex NumberOnly = new Regex(@"^[+\-−]?[0-9]+\z", RegexOptions.CultureInvariant);

        /// <summary>`(tier | 0 || 2)` clamped to 1..4, as an index.</summary>
        // js: scenes/battle/field/fx.js:62
        private static int Idx(int tier) => Math.Max(1, Math.Min(4, tier == 0 ? 2 : tier)) - 1;

        // js: scenes/battle/field/fx.js:63
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // ---- state ------------------------------------------------------------------------------------

        private readonly BattleViewEnv _env;
        /// <summary>live: every piece on screen (clear() removes them).</summary>
        private readonly HashSet<UiNode> _live = new HashSet<UiNode>();
        /// <summary>The floats on screen, oldest first: where each stands and where it is going (FLOAT_STACK_GAP).</summary>
        private readonly List<FloatRec> _floats = new List<FloatRec>();
        private int _arcSeq;
        private int _shieldSeq;

        private FieldFx(BattleViewEnv env)
        {
            _env = env;
        }

        /// <summary>createFx(env).</summary>
        // js: scenes/battle/field/fx.js:92
        public static FieldFx Create(BattleViewEnv env) => new FieldFx(env);

        private BattleClock Clock => _env.Clock;
        private RectTransform LayerFx => _env.Layer.Fx;
        private RectTransform LayerFloat => _env.Layer.Float;

        // ---- svg helpers --------------------------------------------------------------------------------

        /// <summary>
        /// VisualArt.CircleRing as an ellipse drawn at its real size: w×h with a `stroke` px line all round (review 17). `glow`
        /// (px) adds a soft light of `glowColor` (the line's colour by default) around the line: the svg's filter
        /// drop-shadow(0 0 glow glowColor), the kit's stroke shadow (its alpha × the line's, as the filter takes the content's).
        /// An svg w × h at (0, 0) of parent (.ocbg-battle-fx-svg).
        /// </summary>
        // js: scenes/battle/field/fx.js:79
        private static PathGraphic RingSvg(RectTransform parent, float w, float ht, float stroke, Rgba color, float glow = 0f, Rgba? glowColor = null)
        {
            PathGraphic s = PathGraphic.Make(parent, 0f, 0f, w, ht, "ocbg-battle-fx-svg ring");
            // The ring is first meshed while its piece stands at its entrance scale (打 0.3, 型 0.3 / 0.6): the feather is
            // read then and kept, so a 3 px line would grow into a 3 px blur. The browser rasterises the line at every
            // scale; its settled scale (1) keeps it crisp where it is read.
            s.NodeScaleHint = 1f;
            float rx = Math.Max(0.5f, w / 2f - stroke / 2f);
            float ry = Math.Max(0.5f, ht / 2f - stroke / 2f);
            float cx = w / 2f, cy = ht / 2f;
            // <ellipse cx cy rx ry fill="none" stroke stroke-width>
            string d = "M" + N(cx - rx) + " " + N(cy) + " A" + N(rx) + " " + N(ry) + " 0 1 0 " + N(cx + rx) + " " + N(cy)
                       + " A" + N(rx) + " " + N(ry) + " 0 1 0 " + N(cx - rx) + " " + N(cy) + " Z";
            var layer = new PathLayer();
            layer.Paths.Add(PathData.Parse(d));
            layer.Stroke = new SolidPaint(color);
            layer.StrokeStyle = new StrokeStyle(stroke);
            if (glow > 0f) layer.Shadow = new DropShadow(0f, 0f, glow, (glowColor ?? color).MulAlpha(color.A));
            s.Add(layer);
            return s;
        }

        // ---- pieces ----------------------------------------------------------------------------------------

        /// <summary>A centre-anchored element in `host` at stage point `at`, w×h.</summary>
        // js: scenes/battle/field/fx.js:99
        private UiNode Piece(RectTransform host, string cls, StagePoint at, float w, float ht)
        {
            RectTransform rt = Ui.Div(host, "ocbg-battle-fx " + cls);
            UiNode el = UiNode.Of(rt);
            el.Place(at.X - w / 2f, at.Y - ht / 2f, w, ht);
            _live.Add(el);
            return el;
        }

        // js: scenes/battle/field/fx.js:109
        private void Drop(UiNode el)
        {
            _live.Remove(el);
            if (el) Ui.Remove(el);
        }

        /// <summary>DepictionFx.FadeAndDestroy: alpha → 0 over ms (ease in), then gone. Beside the flow.</summary>
        // js: scenes/battle/field/fx.js:114
        private Promise FadeAndDrop(UiNode el, float ms, float from = 1f)
        {
            return Clock.Run(ms, Ease.In, t => { if (el) el.Opacity = from * (1f - t); }).Then(() => Drop(el));
        }

        /// <summary>
        /// Review 17 (scene addition): `els` ride along with a figure for as long as they show — `trackX()` gives its x now
        /// (null: leave them), `x0` its x when they were placed. Each frame they move by however far it has gone since.
        /// </summary>
        // js: scenes/battle/field/fx.js:121
        private void Follow(UiNode[] els, Func<float?> trackX, float? x0)
        {
            if (trackX == null || x0 == null) return;
            var placed = new List<(UiNode El, float Left)>();
            foreach (UiNode el in els)
                if (el) placed.Add((el, el.X));
            Action off = null;
            off = Clock.OnFrame((dt, dtReal) =>
            {
                bool any = false;
                foreach ((UiNode El, float Left) p in placed)
                    if (_live.Contains(p.El)) { any = true; break; }
                if (!any) { off?.Invoke(); return; }
                float? x = trackX();
                if (x == null) return;
                foreach ((UiNode El, float Left) p in placed)
                    if (_live.Contains(p.El) && p.El) p.El.X = Fix(p.Left + x.Value - x0.Value, 1);
            });
        }

        // ---- blows (DepictionFx.Strike) ------------------------------------------------------------

        /// <summary>A blow's reach: `near` px toward the striker, `far` px past the chest, and (斬) its natural length.</summary>
        private struct Reach
        {
            public float Near;
            public float Far;
            public float Natural;
        }

        /// <summary>
        /// The blow's horizontal reach (review 19): `near` px toward the striker and `far` px past the chest. The far side stops
        /// BLOW_PAST beyond the struck body's edge; with two or three enemies both sides stop short of the other figures
        /// (field.js blowRoom: `room.left` / `room.right`).
        /// </summary>
        // js: scenes/battle/field/fx.js:139
        private static Reach ReachOf(bool towardLeft, float natural, BlowRoom? room, float half)
        {
            BlowRoom r = room ?? BlowRoom.None;
            float nearRoom = towardLeft ? r.Right : r.Left;
            float farRoom = towardLeft ? r.Left : r.Right;
            return new Reach
            {
                Near = Math.Max(BlowMin, Math.Min(nearRoom, natural)),
                Far = Math.Max(BlowMin, Math.Min(farRoom, (half != 0f && !float.IsNaN(half) ? half : 50f) + BlowPast)),
            };
        }

        /// <summary>
        /// 突 (§5.3 「一直線の閃きと点の衝撃」): one straight line of light arriving from the striker's side — a bright core in a
        /// soft glow, tapered at its tail — drawn out to `far` past the chest over the first part of `ms` (linear), and a point
        /// of impact flaring where it meets the body (the chest's edge toward the striker) as the line lands.
        /// </summary>
        // js: scenes/battle/field/fx.js:151
        private Promise Thrust(StagePoint at, string colorName, bool towardLeft, Reach reach, float thickness, float ms)
        {
            int dir = towardLeft ? -1 : 1;
            float x0 = at.X - dir * reach.Near;
            float x1 = at.X + dir * reach.Far;
            float len = Math.Abs(x1 - x0);
            float h3 = thickness * 3f;
            UiNode el = Piece(LayerFx, "ocbg-battle-streak is-thrust", new StagePoint((x0 + x1) / 2f, at.Y), len, h3);
            string tail = towardLeft ? "90deg" : "270deg";
            // filter: drop-shadow(0 0 6px tok(c, .8)) of the two bands below (under them).
            float[] fs = { 0f, 0.6f, 0.7f, 1f };   // the stops of both bands (between them the alpha is smooth: the blur keeps it)
            var glowX = new List<(float, float)>();
            var coreX = new List<(float, float)>();
            foreach (float f in fs)
            {
                float ag = Ramp1(f, (0f, 0.35f), (0.6f, 0.2f), (1f, 0f));
                float ac = Ramp1(f, (0f, 1f), (0.7f, 0.95f), (1f, 0f));
                float x = towardLeft ? f * len : (1f - f) * len;   // the gradient line runs from the head (0 %) to the tail
                glowX.Add((x, ag));
                coreX.Add((x, 1f - (1f - ac) * (1f - ag) - ag));   // what the core adds over the glow in its rows
            }
            glowX.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            coreX.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            DropShadowOf(el.Rect, len, h3, 6f, Col(colorName, 0.8f), null,
                (glowX, new List<(float, float)> { (0f, 1f), (h3, 1f) }),
                (coreX, new List<(float, float)> { (thickness, 1f), (thickness * 2f, 1f) }));
            // background: <core> center / 100% th no-repeat, <glow> center / 100% 3th no-repeat (the first layer on top)
            GradientGraphic.Css(el.Rect, "linear-gradient(" + tail + ", " + FieldArt.Tok(colorName, 0.35f) + " 0%, " + FieldArt.Tok(colorName, 0.2f) + " 60%, transparent 100%)",
                0f, 0f, len, h3, false, "background glow");
            GradientGraphic.Css(el.Rect, "linear-gradient(" + tail + ", " + FieldArt.Tok(colorName, 1f) + " 0%, " + FieldArt.Tok(colorName, 0.95f) + " 70%, transparent 100%)",
                0f, (h3 - thickness) / 2f, len, thickness, false, "background core");
            el.SetOrigin(towardLeft ? 1f : 0f, 0.5f);
            el.SetTransform(0f, 0f, 0f, 1f, 0f);   // scaleX(0)
            float lineMs = Math.Max(1f, ms * 0.6f);
            var hit = new StagePoint(at.X - dir * ImpactIn, at.Y);
            return Clock.Run(lineMs, Ease.Linear, t => { if (el) el.SetTransform(0f, 0f, Fix(t, 3), 1f, 0f); }).Then(() =>
            {
                Impact(hit, colorName, 70f, Math.Max(1f, ms - lineMs) + 80f).Forget();
                FadeAndDrop(el, 180f).Forget();
            });
        }

        /// <summary>The point of impact: a small hard flash that grows and fades (beside the flow).</summary>
        // js: scenes/battle/field/fx.js:172
        private Promise Impact(StagePoint at, string colorName, float size, float ms)
        {
            UiNode el = Piece(LayerFx, "ocbg-battle-impact", at, size, size);
            GradientGraphic.Css(el.Rect, "radial-gradient(closest-side, " + FieldArt.Tok("ink", 1f) + " 0%, " + FieldArt.Tok(colorName, 0.9f) + " 35%, " + FieldArt.Tok(colorName, 0f) + " 100%)",
                0f, 0f, size, size, false, "background");
            el.Scale = 0.4f;
            return Clock.Run(ms, Ease.Out, t =>
            {
                if (!el) return;
                el.SetTransform(0f, 0f, Fix(Lerp(0.4f, 1.2f, t), 3), Fix(Lerp(0.4f, 1.2f, t), 3), 0f);
                el.Opacity = Fix(1f - t * t, 3);
            }).Then(() => Drop(el));
        }

        /// <summary>
        /// 斬 (§5.3 「弧を描く刃の光。相手の胸を横切る」; review 19): an arc of blade light swept across the chest from high on the
        /// striker's side to low past it — a soft glow under a bright core, revealed along the arc over `ms` (out), its ends
        /// thin. The arc's span keeps to the blow's reach.
        /// </summary>
        // js: scenes/battle/field/fx.js:188
        private Promise Slash(StagePoint at, string colorName, bool towardLeft, Reach reach, float thickness, float ms)
        {
            int dir = towardLeft ? -1 : 1;
            float R = Math.Max(60f, Math.Min(reach.Natural * 0.42f, Math.Min(reach.Near / 0.85f, reach.Far / 0.85f)));
            float w = R * 2f + thickness * 4f;
            float ht = R * 1.6f + thickness * 4f;
            UiNode el = Piece(LayerFx, "ocbg-battle-arc", at, w, ht);
            float cx = w / 2f;
            float cy = ht / 2f;
            // From upper-near to lower-far, the chord through the chest, bowing a little up and away from the striker (the
            // swing's path): a radius of 1.8 × the half chord keeps the bow to about a sixth of it.
            float sx = cx - dir * R * 0.85f;
            float sy = cy - R * 0.38f;
            float ex = cx + dir * R * 0.85f;
            float ey = cy + R * 0.62f;
            int sweep = dir > 0 ? 1 : 0;
            float rr = (float)Math.Sqrt((ex - sx) * (ex - sx) + (ey - sy) * (ey - sy)) * 0.9f;
            string d = "M" + F1(sx) + " " + F1(sy) + " A" + F1(rr) + " " + F1(rr) + " 0 0 " + sweep + " " + F1(ex) + " " + F1(ey);
            string id = "ocbgArc" + (++_arcSeq);
            PathGraphic s2 = PathGraphic.Make(el.Rect, 0f, 0f, w, ht, "ocbg-battle-fx-svg " + id);
            PathData pd = PathData.Parse(d);
            // <linearGradient gradientUnits="userSpaceOnUse" x1 y1 x2 y2> along the chord
            // x1 / y1 / x2 / y2 are set from the numbers themselves (setAttribute(String(sx))), not toFixed(1) as the path is.
            LinearGradient grad = LinearGradient.Points(new Vec2(sx, sy), new Vec2(ex, ey),
                GradientStop.At(Col(colorName, 0f), 0f),
                GradientStop.At(Col(colorName, 0.95f), 0.35f),
                GradientStop.At(Col(colorName, 1f), 0.8f),
                GradientStop.At(Col(colorName, 0.2f), 1f));
            // pathLength 1000 / stroke-dasharray "1000 1000" / stroke-dashoffset 1000: in user units, the arc's own length.
            float total = 0f;
            foreach (Polyline line in Flatten.Run(pd, Affine.Identity)) total += line.Length;
            if (!(total > 0f)) total = 1f;
            float[] dash = { total, total };
            float glowW = thickness * 2.6f;
            float coreW = Math.Max(3f, thickness * 0.9f);
            // filter: drop-shadow(0 0 8px tok(c, .7)) of the svg, under all three strokes: the glow's band (.35 of the alpha)
            // and the core's (the rest of it, where the core and the edge lie).
            Rgba shadow = Col(colorName, 0.7f);
            s2.Add(ShadowOnlyStroke(pd, glowW, dash, total, shadow.MulAlpha(0.35f)));
            s2.Add(ShadowOnlyStroke(pd, coreW, dash, total, shadow.MulAlpha(0.65f)));
            var glow = new PathLayer { Stroke = new SolidPaint(Col(colorName, 0.35f)), StrokeStyle = new StrokeStyle(glowW, Cap.Round, Join.Miter, dash, total) };
            glow.Paths.Add(pd);
            var core = new PathLayer { Stroke = grad, StrokeStyle = new StrokeStyle(coreW, Cap.Round, Join.Miter, dash, total) };
            core.Paths.Add(pd);
            var edge = new PathLayer { Stroke = new SolidPaint(Col("ink", 0.9f)), StrokeStyle = new StrokeStyle(Math.Max(1.5f, thickness * 0.3f), Cap.Round, Join.Miter, dash, total) };
            edge.Paths.Add(pd);
            s2.Add(glow);
            s2.Add(core);
            s2.Add(edge);
            s2.DashOffset = total;
            return Clock.Run(ms, Ease.Out, t =>
            {
                if (s2) s2.DashOffset = Fix(total * (1f - t), 1);
            }).Then(() => { FadeAndDrop(el, 200f).Forget(); });
        }

        /// <summary>
        /// 払 (§5.3 「横に広い帯が薙ぐ」, 飾りの変数「粒子の量」; review 19): a wide band of light swept across the chest from the
        /// striker's side — soft edges, a bright core line, a lit leading edge — that stops a little past the body, throwing a
        /// few motes behind its front as it goes.
        /// </summary>
        // js: scenes/battle/field/fx.js:237
        private Promise Sweep(StagePoint at, string colorName, string coreName, bool towardLeft, Reach reach, float thickness, float ms)
        {
            int dir = towardLeft ? -1 : 1;
            float x0 = at.X - dir * reach.Near;
            float x1 = at.X + dir * reach.Far;
            float len = Math.Abs(x1 - x0);
            float tilt = towardLeft ? 6f : -6f;
            UiNode el = Piece(LayerFx, "ocbg-battle-band", new StagePoint((x0 + x1) / 2f, at.Y), len, thickness);
            string background = "linear-gradient(180deg, transparent 0%, " + FieldArt.Tok(colorName, 0.55f) + " 30%, " + FieldArt.Tok(coreName, 0.95f) + " 47%, "
                                + FieldArt.Tok("ink", 0.95f) + " 50%, " + FieldArt.Tok(coreName, 0.95f) + " 53%, " + FieldArt.Tok(colorName, 0.55f) + " 70%, transparent 100%)";
            string mask = "linear-gradient(" + (towardLeft ? "270deg" : "90deg") + ", transparent 0%, #000 22%, #000 100%)";
            // filter: drop-shadow(0 0 8px tok(c, .75)) of the band (its gradient's alpha down, the whole box across), blurred,
            // under it; the mask then takes the band and its light alike, and nothing shows past the band's box (CSS paints
            // filter, then mask; mask-clip border-box). Left to the browser's order, the light reads as a soft fill of the
            // band's clear top and bottom rows, cut at its edges, not as a halo round it.
            var bandY = new List<(float, float)>
            {
                (0f, 0f), (0.3f * thickness, 0.55f), (0.47f * thickness, 0.95f), (0.5f * thickness, 0.95f), (0.53f * thickness, 0.95f),
                (0.7f * thickness, 0.55f), (thickness, 0f),
            };
            List<(float, float)> maskX = towardLeft
                ? new List<(float, float)> { (0f, 1f), (0.78f * len, 1f), (len, 0f) }
                : new List<(float, float)> { (0f, 0f), (0.22f * len, 1f), (len, 1f) };
            DropShadowOf(el.Rect, len, thickness, 8f, Col(colorName, 0.75f), maskX, (new List<(float, float)> { (0f, 1f), (len, 1f) }, bandY));
            Paint bandPaint(RectF box) => new MaskedPaint(
                global::Trial.Geometry.Css.Image(background).Resolve(box),
                global::Trial.Geometry.Css.Image(mask).Resolve(box));
            GradientGraphic.Make(el.Rect, 0f, 0f, len, thickness, "background").SetPaint(bandPaint);
            el.SetOrigin(towardLeft ? 1f : 0f, 0.5f);
            UiNode front = Piece(LayerFx, "ocbg-battle-band-front", new StagePoint(x0, at.Y), 14f, thickness * 1.6f);
            GradientGraphic.Css(front.Rect, "radial-gradient(closest-side, " + FieldArt.Tok("ink", 1f) + ", " + FieldArt.Tok(coreName, 0.8f) + " 45%, " + FieldArt.Tok(colorName, 0f) + ")",
                0f, 0f, 14f, thickness * 1.6f, false, "background");
            var motes = new List<UiNode>();
            const int Motes = 7;
            el.SetTransform(0f, 0f, 0f, 1f, tilt);   // rotate(tilt) scaleX(0)
            return Clock.Run(ms, Ease.Out, t =>
            {
                if (el) el.SetTransform(0f, 0f, Fix(t, 3), 1f, tilt);
                float fx0 = x0 + (x1 - x0) * t;
                if (front)
                {
                    front.X = Fix(fx0 - 7f, 1);
                    front.Y = Fix(at.Y + (float)Math.Tan(tilt * Math.PI / 180.0) * (fx0 - (x0 + x1) / 2f) * -1f - thickness * 0.8f, 1);
                }
                while (motes.Count < (int)Math.Floor(t * Motes))
                {
                    int k = motes.Count;
                    UiNode m = Piece(LayerFx, "ocbg-battle-mote", new StagePoint(fx0, at.Y + ((k % 3) - 1) * thickness * 0.35f), 5f, 5f);
                    BoxGraphic.Make(m.Rect, "background: " + FieldArt.Tok(k % 2 != 0 ? coreName : "ink", 0.95f) + "; border-radius: 50%;", 0f, 0f, 5f, 5f, "background");
                    motes.Add(m);
                    float mx = fx0;
                    float my = at.Y + ((k % 3) - 1) * thickness * 0.35f;
                    float vx = -dir * (18f + 6f * (k % 3));
                    float vy = -10f - 4f * (k % 2);
                    Clock.Run(320f, Ease.Out, q =>
                    {
                        if (!m) return;
                        m.X = Fix(mx + vx * q - 2.5f, 1);
                        m.Y = Fix(my + vy * q - 2.5f, 1);
                        m.Opacity = Fix(1f - q, 3);
                    }).Then(() => Drop(m)).Forget();
                }
            }).Then(() =>
            {
                FadeAndDrop(front, 120f).Forget();
                FadeAndDrop(el, 200f).Forget();
            });
        }

        /// <summary>
        /// Ripple (打). Review fix (a look call): the blow lands where it meets the body — the chest's edge toward the striker,
        /// IMPACT_IN px in — as a thin ring snapping open there (0.3 → 1, its inside clear) and five short spikes thrown out the
        /// way the blow travels (±50°), so it reads as a weight arriving from one side. The sizes stay Unity's (the ring is
        /// half the disc, the spikes a third of it).
        /// </summary>
        // js: scenes/battle/field/fx.js:288
        private const float ImpactIn = 36f;
        // js: scenes/battle/field/fx.js:289
        private static readonly float[] Spikes = { -50f, -25f, 0f, 25f, 50f };

        // js: scenes/battle/field/fx.js:290
        private Promise Ripple(StagePoint at, string colorName, float size, float ms, bool towardLeft)
        {
            int dir = towardLeft ? -1 : 1;
            var hit = new StagePoint(at.X - dir * ImpactIn, at.Y);
            float ringSize = size * 0.5f;
            UiNode ring = Piece(LayerFx, "ocbg-battle-ripple", hit, ringSize, ringSize);
            // §6b 「a thin ring」 (review 17: at Unity's sprite line it was 13 px, flat): an even line with a soft light.
            RingSvg(ring.Rect, ringSize, ringSize, Math.Max(3f, ringSize * RippleLine), Col(colorName, 0.9f), 6f);
            ring.Scale = 0.3f;
            float len = size / 3f;
            var spikes = new List<(UiNode El, float A)>();
            foreach (float deg in Spikes)
            {
                float a = towardLeft ? 180f - deg : deg;
                UiNode el = Piece(LayerFx, "ocbg-battle-spike", hit, len, 5f);
                el.Place(hit.X, hit.Y - 2.5f);
                BoxGraphic.Make(el.Rect, "background: linear-gradient(90deg, " + FieldArt.Tok(colorName, 0.95f) + ", transparent); border-radius: 3px;",
                    0f, 0f, len, 5f, "background").NodeScaleHint = 1f;   // meshed at scaleX(0): feather of the settled bar
                el.SetOrigin(0f, 0.5f);
                RotateTranslateScaleX(el, a, Fix(ringSize * 0.15f, 1), 0f);
                spikes.Add((el, a));
            }
            return Clock.Run(ms, Ease.Out, t =>
            {
                float k = Fix(Lerp(0.3f, 1f, t), 3);
                if (ring) ring.SetTransform(0f, 0f, k, k, 0f);
                foreach ((UiNode El, float A) sp in spikes)
                    if (sp.El) RotateTranslateScaleX(sp.El, sp.A, Fix(ringSize * (0.15f + 0.3f * t), 1), Fix(t, 3));
            }).Then(() =>
            {
                FadeAndDrop(ring, 180f).Forget();
                foreach ((UiNode El, float A) sp in spikes) FadeAndDrop(sp.El, 140f).Forget();
            });
        }

        /// <summary>DepictionFx.Strike: the blow's shape by system at the chest `at`, drawn in `a.Ms`.</summary>
        /// <remarks>
        /// 斬 an arc, 突 one straight line and its point of impact, 払 a wide band, 打 a ripple where it lands. `towardLeft` when
        /// the blow travels right to left (the enemy's). `room` / `half` (review 19): how far it may reach beside the struck
        /// figure (field.js blowRoom) and that body's half width, so it reads as a hit on that figure alone.
        /// </remarks>
        // js: scenes/battle/field/fx.js:327
        public Promise Strike(StagePoint at, FxStrikeArgs a)
        {
            if (a == null) a = new FxStrikeArgs();
            string colorName = a.ColorName ?? "ink";
            float length = SlashLength[Idx(a.Tier)];
            float thickness = SlashThickness[Idx(a.Tier)];
            switch (a.System)
            {
                case StrikeSystem.Thrust:
                    return Thrust(at, colorName, a.TowardLeft, ReachOf(a.TowardLeft, length * 0.85f, a.Room, a.Half), Math.Max(4f, thickness * 0.45f), a.Ms);
                case StrikeSystem.Sweep:
                    return Sweep(at, colorName, colorName == "violet" ? "phosphor" : colorName, a.TowardLeft, ReachOf(a.TowardLeft, length * 0.7f, a.Room, a.Half), thickness * 2.4f, a.Ms);
                case StrikeSystem.Strike:
                    return Ripple(at, colorName, length * 0.7f, a.Ms, a.TowardLeft);
                default:
                {
                    Reach r = ReachOf(a.TowardLeft, length, a.Room, a.Half);
                    r.Natural = length;
                    return Slash(at, colorName, a.TowardLeft, r, thickness, a.Ms);
                }
            }
        }

        // ---- the shapes of the other systems (DepictionFx.Mark, §5.3) ------------------------------

        /// <summary>DepictionFx.Mark: 盾 a ring standing in front, 型 a crest at the feet (+ body light), 気 a sigil falling onto the chest; blows and 息 draw nothing.</summary>
        /// <remarks>
        /// `p` = { chest, head, feet, facing (+1 faces right), fx (FxSystem), tier, ms, crestLight, warm }. `warm` = the unit is
        /// the player's: the crest is amber / wick there and violet / phosphor on the enemy (README 色: warm colours belong to the
        /// player's side only; Unity draws amber for both).
        /// </remarks>
        // js: scenes/battle/field/fx.js:351
        public Promise Mark(FxMarkArgs p)
        {
            if (p == null) return Promise.Resolved();
            float s = MarkScale[Idx(p.Tier)];
            FxForm? form = null;
            try { form = p.Fx.HasValue ? EffectGrammar.FormOf(p.Fx.Value) : (FxForm?)null; }
            catch (Exception) { form = null; }
            float? x0 = p.TrackX != null ? p.TrackX() : null;
            switch (form)
            {
                case FxForm.Ring:
                    return StandingRing(new StagePoint(p.Chest.X + 70f * p.Facing, p.Chest.Y), s, p.Ms, p.TrackX, x0);
                case FxForm.Crest:
                    return Crest(p.Feet, s, p.Ms, p.CrestLight, p.Warm, p.TrackX, x0);
                case FxForm.Sigil:
                    return Sigil(new StagePoint(p.Head.X, p.Head.Y - 60f), p.Chest, s, p.Ms);
                default:
                    return Promise.Resolved();
            }
        }

        /// <summary>盾: a ring seen edge-on, rising to full height in front of the figure (and riding with it).</summary>
        // js: scenes/battle/field/fx.js:369
        private Promise StandingRing(StagePoint at, float s, float ms, Func<float?> trackX, float? x0)
        {
            UiNode el = Piece(LayerFx, "ocbg-battle-ring", at, 110f * s, 230f * s);
            RingSvg(el.Rect, 110f * s, 230f * s, Math.Max(2f, RingLineStand * s), Col("guard", 0.95f), 7f * s, Col("guard", 0.75f));
            Follow(new[] { el }, trackX, x0);
            Burst(at, "guard", 160f * s).Forget();
            el.SetTransform(0f, 0f, 0.6f, 0f, 0f);   // scale(0.6, 0)
            return Clock.Run(ms, Ease.Out, t =>
            {
                if (el) el.SetTransform(0f, 0f, Fix(Lerp(0.6f, 1f, t), 3), Fix(t, 3), 0f);
            }).Then(() => { FadeAndDrop(el, 220f).Forget(); });
        }

        /// <summary>
        /// 型: two flat rings open under the feet; the crest's light lingers 600 ms before it fades. Review 17: the rings are even
        /// lines with a soft light, the ground inside the outer one lit faintly, and they ride with the figure's feet.
        /// </summary>
        // js: scenes/battle/field/fx.js:385
        private Promise Crest(StagePoint feet, float s, float ms, bool withLight, bool warm, Func<float?> trackX, float? x0)
        {
            Rgba outerColor = warm ? Col("amber", 0.95f) : Col("violet", 0.95f);
            Rgba innerColor = warm ? Col("wick", 0.85f) : Col("phosphor", 0.85f);
            string lightName = warm ? "amber" : "violet";
            UiNode outer = Piece(LayerFx, "ocbg-battle-crest", feet, 280f * s, 72f * s);
            BoxGraphic.Make(outer.Rect, "border-radius: 50%; background: radial-gradient(closest-side, " + FieldArt.Tok(lightName, 0.22f) + ", " + FieldArt.Tok(lightName, 0f) + ");",
                0f, 0f, 280f * s, 72f * s, "background");
            RingSvg(outer.Rect, 280f * s, 72f * s, Math.Max(1.5f, RingLineCrestOuter * s), outerColor, 6f * s);
            UiNode inner = Piece(LayerFx, "ocbg-battle-crest", feet, 170f * s, 44f * s);
            RingSvg(inner.Rect, 170f * s, 44f * s, Math.Max(1.25f, RingLineCrestInner * s), innerColor, 4f * s);
            UiNode glow = null;
            if (withLight)
            {
                glow = Piece(LayerFx, "ocbg-battle-crest-glow", new StagePoint(feet.X, feet.Y - 140f * s), 200f * s, 320f * s);
                GradientGraphic.Glow(glow.Rect, 0f, 0f, 200f * s, 320f * s, Col(lightName), 0.35f, "background");
            }
            Follow(new[] { outer, inner, glow }, trackX, x0);
            outer.Scale = 0.3f;
            inner.Scale = 0.6f;
            return Clock.Run(ms, Ease.Out, t =>
            {
                float ko = Fix(Lerp(0.3f, 1f, t), 3), ki = Fix(Lerp(0.6f, 1f, t), 3);
                if (outer) outer.SetTransform(0f, 0f, ko, ko, 0f);
                if (inner) inner.SetTransform(0f, 0f, ki, ki, 0f);
            }).Then(() =>
            {
                FadeAndDrop(outer, 600f).Forget();
                FadeAndDrop(inner, 600f).Forget();
                if (glow) FadeAndDrop(glow, 600f).Forget();
            });
        }

        /// <summary>
        /// 気: a diamond drops from above the head onto the chest and bursts there. Review fix (a look call): the diamond is lit —
        /// a phosphor-to-violet gradient with a bright edge — carries the crest's soft Glow behind it, and leaves a short trail of
        /// fading copies along its fall, so it reads as a curse landing.
        /// </summary>
        // js: scenes/battle/field/fx.js:418
        private static readonly float[] SigilTrail = { 0.07f, 0.14f, 0.21f };

        // js: scenes/battle/field/fx.js:419
        private Promise Sigil(StagePoint from, StagePoint to, float s, float ms)
        {
            float size = 40f * s;
            StagePoint At(float t) => new StagePoint(Lerp(from.X, to.X, t), Lerp(from.Y, to.Y, t));
            float EaseIn(float t) => t * t * t; // the fall's 'in'
            UiNode glow = Piece(LayerFx, "ocbg-battle-sigil-glow", from, size * 3.2f, size * 3.2f);
            GradientGraphic.Glow(glow.Rect, 0f, 0f, size * 3.2f, size * 3.2f, Col("phosphor"), 0.55f, "background");
            var trail = new List<(UiNode El, float Lag, float W)>();
            for (int i = 0; i < SigilTrail.Length; i++)
            {
                float tw = size * (0.9f - 0.12f * i);
                UiNode tr = Piece(LayerFx, "ocbg-battle-sigil is-trail", from, tw, tw);
                BoxGraphic.Make(tr.Rect, "background: " + FieldArt.Tok("phosphor", 0.5f - 0.14f * i) + ";", 0f, 0f, tw, tw, "background");
                tr.Rotate = 45f;
                tr.Opacity = 0f;
                trail.Add((tr, SigilTrail[i], tw));
            }
            UiNode el = Piece(LayerFx, "ocbg-battle-sigil", from, size, size);
            BoxGraphic.Make(el.Rect,
                "background: linear-gradient(135deg, " + FieldArt.Tok("ink", 0.95f) + " 0%, " + FieldArt.Tok("phosphor", 0.98f) + " 38%, " + FieldArt.Tok("violet", 0.95f) + " 100%); "
                + "box-shadow: 0 0 " + F1(10f * s) + "px " + FieldArt.Tok("phosphor", 0.85f) + ", inset 0 0 0 2px " + FieldArt.Tok("ink", 0.55f) + ";",
                0f, 0f, size, size, "background");
            el.Rotate = 45f;
            void Place(UiNode node, StagePoint p, float w)
            {
                if (node) node.Place(p.X - w / 2f, p.Y - w / 2f);
            }
            return Clock.Run(ms, Ease.Linear, raw =>
            {
                StagePoint p = At(EaseIn(raw));
                Place(el, p, size);
                Place(glow, p, size * 3.2f);
                foreach ((UiNode El, float Lag, float W) tr in trail)
                {
                    float lt = Math.Max(0f, raw - tr.Lag);
                    Place(tr.El, At(EaseIn(lt)), tr.W);
                    if (tr.El) tr.El.Opacity = raw > tr.Lag ? 1f : 0f;
                }
            }).Then(() =>
            {
                Burst(to, "phosphor", 140f * s).Forget();
                FadeAndDrop(el, 150f).Forget();
                FadeAndDrop(glow, 220f).Forget();
                foreach ((UiNode El, float Lag, float W) tr in trail) FadeAndDrop(tr.El, 120f).Forget();
            });
        }

        /// <summary>
        /// DepictionFx.TraitAccent (§5.9 J2): a short flare laid over the blow where it lands. `warm`: the trait is the player's
        /// (wick and amber); an enemy's is drawn in phosphor and violet. Review 19: a burst of light — a soft core and eight short
        /// rays thrown out from the point of contact (the chest's edge toward the striker) — grows 0.5 → 1.35 and fades; no rings.
        /// </summary>
        // js: scenes/battle/field/fx.js:460
        private const int AccentRays = 8;

        /// <summary>DepictionFx.TraitAccent (§5.9 J2): a core and eight rays at the point of contact (warm: wick / amber; else phosphor / violet).</summary>
        // js: scenes/battle/field/fx.js:461
        public Promise TraitAccent(StagePoint at, bool warm, float ms, bool towardLeft)
        {
            int dir = towardLeft ? -1 : 1;
            var hit = new StagePoint(at.X - dir * ImpactIn, at.Y);
            string coreColor = warm ? "amber" : "phosphor";
            string rayColor = warm ? "wick" : "violet";
            UiNode core = Piece(LayerFx, "ocbg-battle-accent", hit, 96f, 96f);
            GradientGraphic.Css(core.Rect, "radial-gradient(closest-side, " + FieldArt.Tok("ink", 0.9f) + " 0%, " + FieldArt.Tok(coreColor, 0.75f) + " 30%, " + FieldArt.Tok(coreColor, 0f) + " 100%)",
                0f, 0f, 96f, 96f, false, "background");
            var rays = new List<(UiNode El, float Deg)>();
            for (int k = 0; k < AccentRays; k++)
            {
                UiNode el = Piece(LayerFx, "ocbg-battle-accent-ray", hit, 46f, 4f);
                el.Place(hit.X, hit.Y - 2f);
                el.SetOrigin(0f, 0.5f);
                BoxGraphic.Make(el.Rect, "border-radius: 2px; background: linear-gradient(90deg, " + FieldArt.Tok(rayColor, 0f) + ", " + FieldArt.Tok(rayColor, 0.95f) + " 45%, "
                                         + FieldArt.Tok("ink", 0.9f) + " 70%, " + FieldArt.Tok(rayColor, 0f) + ");", 0f, 0f, 46f, 4f, "background");
                rays.Add((el, k * (360f / AccentRays) + 22.5f));
            }
            return Clock.Run(ms, Ease.Out, t =>
            {
                float s2 = Fix(Lerp(0.5f, 1.35f, t), 3);
                if (core) core.SetTransform(0f, 0f, s2, s2, 0f);
                foreach ((UiNode El, float Deg) r in rays)
                    if (r.El) RotateTranslateScaleX(r.El, r.Deg, Fix(10f + 26f * t, 1), s2);
            }).Then(() =>
            {
                FadeAndDrop(core, 140f).Forget();
                foreach ((UiNode El, float Deg) r in rays) FadeAndDrop(r.El, 120f).Forget();
            });
        }

        /// <summary>DepictionFx.Burst: a soft glow (glowGradient at 0.8) growing 0.4 → 1.5 and fading over 320 (out). Fire and forget.</summary>
        // js: scenes/battle/field/fx.js:489
        public Promise Burst(StagePoint at, string colorName, float size)
        {
            UiNode el = Piece(LayerFx, "ocbg-battle-burst", at, size, size);
            GradientGraphic g = GradientGraphic.Glow(el.Rect, 0f, 0f, size, size, Col(colorName), 0.8f, "background");
            g.Shape = GradientGraphic.ShapeKind.Ellipse;   // .ocbg-battle-burst { border-radius: 50% }
            el.Scale = 0.4f;
            return Clock.Run(BurstMs, Ease.Out, t =>
            {
                if (!el) return;
                float k = Fix(Lerp(0.4f, 1.5f, t), 3);
                el.SetTransform(0f, 0f, k, k, 0f);
                el.Opacity = 1f - t;
            }).Then(() => Drop(el));
        }

        // ---- floating text (DepictionFx.FloatText, dressed as the DS FloatNum) ------------------------

        /// <summary>
        /// What a float says and how it is dressed. A number alone, or a cause and a number ("出血 -3", "Guard −2"), wears an icon
        /// by its role; anything else is a word on the tag. `plain` keeps the text whole as a word, with no icon: a fired trait's
        /// line ("間合い2以上 +3") is a cue, not an HP or Guard number. Built in layer.float (appended last).
        /// </summary>
        // js: scenes/battle/field/fx.js:507
        private UiNode DressFloat(string text, FxRole role, float fontSize, bool struck, bool plain, string side)
        {
            Match m = plain ? null : CauseAndNumber.Match(text);
            if (m != null && !m.Success) m = null;
            bool numberOnly = !plain && NumberOnly.IsMatch(text);
            bool isNumber = numberOnly || m != null;
            string main = numberOnly ? text : m != null ? m.Groups[2].Value : text;
            string label = !numberOnly && m != null ? m.Groups[1].Value : "";
            string kind = "word";
            string ink = "ink";
            string iconName = null;
            if (struck || role == FxRole.Whiff)
            {
                kind = "miss";
            }
            else if (isNumber && (role == FxRole.Ink || role == FxRole.Omen || role == FxRole.White))
            {
                kind = "hp"; ink = "hp-text"; iconName = "heart";
            }
            else if (isNumber && role == FxRole.Heal)
            {
                kind = "hp"; ink = "hp-text"; iconName = "heal";
            }
            else if (role == FxRole.Guard)
            {
                kind = "guard"; ink = "guard"; iconName = isNumber ? "shield" : null;
            }
            else if (role == FxRole.Warm)
            {
                ink = "amber";
            }
            else if (role == FxRole.Ink2)
            {
                ink = "ink-muted";
            }
            else if (role == FxRole.Omen)
            {
                ink = "phosphor";
            }
            // h('div', { class: 'ocb-float ocbg-battle-float is-' + kind })
            string boxCss = Ocb.FloatCss + (kind == "word" || kind == "miss" ? FloatWordCss : "") + FloatSceneCss;
            RectTransform rt = Ocb.El(LayerFloat, "ocb-float ocbg-battle-float is-" + kind, boxCss);
            UiNode el = UiNode.Of(rt);
            if (iconName != null)
            {
                float k = JsRound(fontSize * 0.62f);
                // opts = iconName === 'shield' ? { fill: 'var(--guard)', fillOpacity: 0.2 } : undefined
                if (iconName == "shield") Ocb.Icon(rt, iconName, k, Col(ink), Col("guard"), 0.2f);
                else Ocb.Icon(rt, iconName, k, Col(ink));
            }
            bool wordClass = !isNumber && !struck;   // b.classList.add('is-word')
            // The cascade of the b (specificity, then source order): the DS b, the scene's b, the DS .is-guard / .is-miss b,
            // the scene's b.is-word and .is-miss b, then the inline font-size / line-height / color.
            string bCss = Ocb.FloatCss + Ocb.FloatBCss + FloatSceneBCss
                          + (kind == "guard" ? Ocb.FloatGuardBCss : "")
                          + (kind == "miss" ? Ocb.FloatMissBCss : "")
                          + (wordClass ? FloatSceneBWordCss : "")
                          + (kind == "miss" ? FloatSceneMissBCss : "")
                          + "font-size: " + Ocb.Num(fontSize) + "px; line-height: " + Ocb.Num(JsRound(fontSize * 1.1f)) + "px; color: " + FieldArt.Tok(ink) + ";";
            // The text's own order (review fix: "Guard −3" read "−3 Guard"): a cause written before its number stays before it,
            // as a small word between the icon and the number.
            if (label.Length > 0)
                Ocb.Text(rt, label, Ocb.TextOf(Ocb.FloatCss + Ocb.FloatSmallCss + FloatSceneSmallCss), "small ocbg-battle-float-cause");
            TextMeshProUGUI b = Ocb.Text(rt, main, Ocb.TextOf(bCss), "b" + (wordClass ? " is-word" : ""));
            // §5.0 「主数字 + 脇ラベル」 (review 19): a side label after the number in 16 px body ink — a fired trait's 「+5 間合い」,
            // the wall's 「壁」.
            if (!string.IsNullOrEmpty(side))
            {
                TextMeshProUGUI small = Ocb.Text(rt, side, Ocb.TextOf(Ocb.FloatCss + Ocb.FloatSmallCss + FloatSceneSmallCss + FloatSideCss), "small ocbg-battle-float-side");
                FlexBox.Attrs(small.rectTransform).MarginL = FloatSideMarginLeft;
            }
            Ocb.Flow(rt, boxCss);
            if (kind == "miss") TextDecor.FromStyle(b);   // text-decoration: line-through 3px var(--miss)
            return el;
        }

        /// <summary>One float on screen: its span across, how far it has run, its pop and alpha, and the box it covers from now on.</summary>
        // js: scenes/battle/field/fx.js:578 (me)
        private sealed class FloatRec
        {
            public float X0;
            public float X1;
            public float T;
            public float Pop = PopScale;
            public float Alpha = 1f;
            /// <summary>swept(): the box it covers from now to its end (top, bottom).</summary>
            public Func<(float Top, float Bottom)> Swept;
        }

        /// <summary>
        /// DepictionFx.FloatText: the text rises o.Rise px over 600 (out, runPaced), pops 1.35 → 1 in its first 18 % and fades
        /// over its last 40 %; placed under the badges and the floats on screen (placeFloat). Fire and forget.
        /// </summary>
        // js: scenes/battle/field/fx.js:557
        public Promise FloatText(StagePoint at, string text, FxRole role, float fontSize, FloatOpts o = null)
        {
            if (o == null) o = new FloatOpts();
            UiNode el = DressFloat(text ?? "", role, fontSize, o.Struck, o.Plain, o.Side ?? "");
            // o.anchor (scene addition): 'left' / 'right' puts that edge of the tag on at.x (it grows away from it), so a tag
            // can stand beside something without reaching over it; the middle otherwise.
            float ax = o.Anchor == FloatAnchor.Left ? 0f : o.Anchor == FloatAnchor.Right ? -1f : -0.5f;
            if (o.Anchor == FloatAnchor.Left || o.Anchor == FloatAnchor.Right) el.SetOrigin(o.Anchor == FloatAnchor.Left ? 0f : 1f, 0.5f);
            el.Place(at.X, at.Y);
            el.SetTransform(0f, 0f, PopScale, PopScale, 0f, ax, -0.5f);   // translate(ax, -50%) scale(1.35)
            el.Z = 1;   // .ocbg-battle-float { z-index: 1 } (appended last: the newest of the raised)
            _live.Add(el);
            Placed pl = PlaceFloat(el, at, fontSize, o);
            float rise = pl.Rise;
            el.Place(pl.X, pl.Y0);
            // o.followY (scene addition, review 16): a function giving the middle y of the float it stands beside; it rides at
            // that height (its own rise is then 0), so a pair that started apart in time still reads side by side.
            Func<float> followY = o.FollowY;
            var me = new FloatRec { X0 = pl.Sx0, X1 = pl.Sx1, T = 0f, Pop = PopScale, Alpha = 1f };
            me.Swept = () =>
            {
                float hNow = pl.HalfS * me.Pop;
                if (followY != null)
                {
                    // It rides with the float beside it: from where it is now up to that float's end (its rise at most).
                    float c = followY();
                    return (c - hNow - o.FollowRise * (1f - me.T), c + hNow);
                }
                float cNow = pl.Y0 - rise * me.T;
                float cEnd = pl.Y0 - rise;
                return (Math.Min(cNow - hNow, cEnd - pl.HalfS), Math.Max(cNow + hNow, cEnd + pl.HalfS));
            };
            // o.onPlaced (scene addition): where it stands, settled — { x0, x1 } its span, y its start, yNow() its middle now
            // (the HP number lines up beside a "Guard −n" tag and rides with it).
            if (o.OnPlaced != null)
            {
                try { o.OnPlaced(new FloatSpan { X0 = pl.Sx0, X1 = pl.Sx1, Y = pl.Y0, Rise = pl.Rise, YNow = () => pl.Y0 - rise * me.T }); }
                catch (Exception) { /* geometry only */ }
            }
            _floats.Add(me);
            void ForgetFloat() => _floats.Remove(me);   // js: forget()
            // o.track (scene addition, review fix): a function giving the x of the figure the float is about; the float rides
            // along by however far that figure has moved since it rose (a shove carries its number and 「押し出し」 with it).
            Func<float?> track = o.Track;
            float? tx0 = track?.Invoke();
            float dxNow = 0f;
            // Paced (review 16): fast-forward speeds the floats up less than the motion, so its numbers stay readable.
            return Clock.RunPaced(FloatMs, Ease.Out, t =>
            {
                float pop = t < 0.18f ? Lerp(PopScale, 1f, t / 0.18f) : 1f;
                float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                me.T = t;
                me.Pop = pop;
                me.Alpha = alpha;
                if (track != null && tx0 != null)
                {
                    float? x = track();
                    float dx = x == null ? dxNow : x.Value - tx0.Value;
                    if (dx != dxNow)
                    {
                        me.X0 += dx - dxNow;
                        me.X1 += dx - dxNow;
                        dxNow = dx;
                        if (el) el.X = Fix(pl.X + dx, 1);
                    }
                }
                float up = followY != null ? pl.Y0 - followY() : rise * t;
                if (!el) return;
                el.SetTransform(0f, -Fix(up, 1), Fix(pop, 3), Fix(pop, 3), 0f, ax, -0.5f);   // translate(ax, calc(-50% - up px)) scale(pop)
                el.Opacity = alpha;
            }).Then(() => { ForgetFloat(); Drop(el); });
        }

        /// <summary>placeFloat's answer: where a float starts and how far it rises.</summary>
        private sealed class Placed
        {
            public float X;
            public float Y0;
            public float Rise;
            /// <summary>Its settled width's span.</summary>
            public float Sx0;
            public float Sx1;
            /// <summary>Its span at the first pop.</summary>
            public float Px0;
            public float Px1;
            public float HalfS;
        }

        /// <summary>
        /// Where a float starts and how far it rises (scene additions over DepictionFx's anchor and rise):
        /// - o.avoid: boxes ({ x, y, w, h }, the omen badges) it must stay under. Of those over its path (its width at the first
        ///   pop), the lowest bottom + o.avoidGap is its ceiling: it starts low enough that its first pop stays under it, and
        ///   rises only as far as the room allows.
        /// - the floats still on screen (FLOAT_STACK_GAP): it stands under every one whose path its own would cross.
        /// - o.lowest: the stage y its path must not reach under (the floor line). When standing under the others would take
        ///   it there, it stands beside them instead, at its own height — on the side of the figure's back (away from the
        ///   stage's middle) when there is room, else the other — and only when neither side has room does it go on down.
        /// Layout sizes: the transform does not count.
        /// </summary>
        // js: scenes/battle/field/fx.js:636
        private Placed PlaceFloat(UiNode el, StagePoint at, float fontSize, FloatOpts o)
        {
            float rise0 = o.Rise ?? DefaultRise;
            // offsetWidth / offsetHeight: the laid-out box in whole px (`|| fontSize` when it rounds to 0).
            float ow = JsRound(el.W), oh = JsRound(el.H);
            float wS = ow > 0f ? ow : fontSize;
            float halfS = (oh > 0f ? oh : fontSize * 1.2f) / 2f;
            float halfP = halfS * PopScale;
            (float, float) SpanAt(float x, float w) =>
                o.Anchor == FloatAnchor.Left ? (x, x + w) : o.Anchor == FloatAnchor.Right ? (x - w, x) : (x - w / 2f, x + w / 2f);
            IReadOnlyList<StageBox> badges = rise0 > 0f && o.Avoid != null ? (o.Avoid() ?? Array.Empty<StageBox>()) : Array.Empty<StageBox>();
            float? lowest = o.Lowest;
            (float Top, float Bottom) PathOf(Placed pl) => (
                Math.Min(pl.Y0 - halfP, pl.Y0 - Math.Max(pl.Rise, 0f) - halfS),
                Math.Max(pl.Y0 + halfP, pl.Y0 - Math.Min(pl.Rise, 0f) + halfS));
            void Under(Placed pl, float ceiling)
            {
                float start = Math.Max(pl.Y0, ceiling + halfP);
                if (pl.Rise > 0f) pl.Rise = Math.Max(0f, start - Math.Max(start - pl.Rise, ceiling + halfS));
                pl.Y0 = start;
            }
            // The floats on screen whose swept box its path would cross: side by side by its settled width (one under another:
            // the first pop's few px of overreach at an edge is let be), or (wide) by its width at the first pop (one beside
            // another: the pop would reach over the neighbour).
            List<(FloatRec F, (float Top, float Bottom) B)> InTheWay(Placed pl, bool wide)
            {
                (float Top, float Bottom) path = PathOf(pl);
                float x0 = wide ? pl.Px0 : pl.Sx0;
                float x1 = wide ? pl.Px1 : pl.Sx1;
                var hits = new List<(FloatRec F, (float Top, float Bottom) B)>();
                foreach (FloatRec f in _floats)
                {
                    if (f.Alpha <= FloatStackMinAlpha) continue;
                    if (f.X1 + FloatStackMarginX <= x0 || f.X0 >= x1 + FloatStackMarginX) continue;
                    (float Top, float Bottom) b = f.Swept();
                    if (b.Bottom + FloatStackGap <= path.Top || b.Top >= path.Bottom + FloatStackGap) continue;
                    hits.Add((f, b));
                }
                return hits;
            }
            // At x: under the badges, and (stack) under every float in its way.
            Placed PlaceAt(float x, bool stack)
            {
                (float px0, float px1) = SpanAt(x, wS * PopScale);
                (float sx0, float sx1) = SpanAt(x, wS);
                var pl = new Placed { X = x, Y0 = at.Y, Rise = rise0, Sx0 = sx0, Sx1 = sx1, Px0 = px0, Px1 = px1, HalfS = halfS };
                float ceiling = float.NegativeInfinity;
                foreach (StageBox bx in badges)
                    if (px1 > bx.X && px0 < bx.X + bx.W) ceiling = Math.Max(ceiling, bx.Y + bx.H + o.AvoidGap);
                if (ceiling > float.NegativeInfinity) Under(pl, ceiling);
                for (int pass = 0; stack && pass < 8; pass++)
                {
                    List<(FloatRec F, (float Top, float Bottom) B)> hits = InTheWay(pl, false);
                    if (hits.Count == 0) break;
                    float lowestBottom = float.NegativeInfinity;
                    foreach ((FloatRec F, (float Top, float Bottom) B) k in hits) lowestBottom = Math.Max(lowestBottom, k.B.Bottom);
                    Under(pl, lowestBottom + FloatStackGap);
                }
                return pl;
            }
            bool Fits(Placed pl) => lowest == null || PathOf(pl).Bottom <= lowest.Value;
            // Beside the floats in its way, toward dir (±1), at its own height; null when the stage has no room.
            Placed BesideAt(int dir)
            {
                float x = at.X;
                for (int pass = 0; pass < 6; pass++)
                {
                    Placed pl = PlaceAt(x, false);
                    List<(FloatRec F, (float Top, float Bottom) B)> hits = InTheWay(pl, true);
                    if (hits.Count == 0) return pl.Px0 >= FloatStageEdge && pl.Px1 <= FloatStageW - FloatStageEdge && Fits(pl) ? pl : null;
                    if (dir > 0)
                    {
                        float right = float.NegativeInfinity;
                        foreach ((FloatRec F, (float Top, float Bottom) B) k in hits) right = Math.Max(right, k.F.X1);
                        x += right + FloatStackGap - pl.Px0;
                    }
                    else
                    {
                        float left = float.PositiveInfinity;
                        foreach ((FloatRec F, (float Top, float Bottom) B) k in hits) left = Math.Min(left, k.F.X0);
                        x += left - FloatStackGap - pl.Px1;
                    }
                }
                return null;
            }
            Placed stacked = PlaceAt(at.X, true);
            if (Fits(stacked)) return stacked;
            int back = at.X >= FloatStageW / 2f ? 1 : -1;
            return BesideAt(back) ?? BesideAt(-back) ?? stacked;
        }

        // ---- the shield (DepictionFx.ShieldBlock) -----------------------------------------------------

        /// <summary>
        /// The shield's art: SHIELD_PATH (64×72) at 100×114 (meet), filled with a see-through guard gradient (.34 → .06 down
        /// its box), rimmed in guard .95 (2.5 px, non-scaling, round joins), an inner keyline of ink .35 at 0.84 about its
        /// middle, and the svg's drop-shadow(0 0 8px guard .7): the rim's stroke shadow (its α .95) and the face's (its own
        /// gradient) under it.
        /// </summary>
        // js: scenes/battle/field/fx.js:718
        private static PathGraphic ShieldArt(RectTransform parent, string id)
        {
            PathGraphic s = PathGraphic.Make(parent, 0f, 0f, ShieldW, ShieldH, "ocbg-battle-fx-svg " + id);
            s.ViewBox = new RectF(0f, 0f, 64f, 72f);
            // <linearGradient x1 0 y1 0 x2 0 y2 1> (objectBoundingBox: the path's box, y 2 → 70)
            Paint FaceOf(float k) => LinearGradient.Points(new Vec2(3f, 2f), new Vec2(3f, 70f),
                GradientStop.At(Col("guard", 0.34f * k), 0f), GradientStop.At(Col("guard", 0.06f * k), 1f));
            Rgba shadow = Col("guard", 0.7f);
            // the drop-shadow, under the art: the face's own (its alpha × .7) and the rim's soft band
            s.Add(new PathLayer(FieldArt.ShieldPath) { Fill = FaceOf(0.7f) });
            s.Add(new PathLayer(FieldArt.ShieldPath)
            {
                Stroke = new SolidPaint(Rgba.Transparent),
                StrokeStyle = new StrokeStyle(2.5f, Cap.Butt, Join.Round) { NonScaling = true },
                Shadow = new DropShadow(0f, 0f, 8f, shadow.MulAlpha(0.95f)),
            });
            s.Add(new PathLayer(FieldArt.ShieldPath)
            {
                Fill = FaceOf(1f),
                Stroke = new SolidPaint(Col("guard", 0.95f)),
                StrokeStyle = new StrokeStyle(2.5f, Cap.Butt, Join.Round) { NonScaling = true },
            });
            // an inner keyline, as light caught on the rim
            s.Add(new PathLayer(FieldArt.ShieldPath)
            {
                Stroke = new SolidPaint(Col("ink", 0.35f)),
                StrokeStyle = new StrokeStyle(1f) { NonScaling = true },
                Transform = Affine.ScaleAbout(0.84f, new Vec2(32f, 36f)),   // translate(32 36) scale(0.84) translate(-32 -36)
            });
            return s;
        }

        /// <summary>
        /// DepictionFx.ShieldBlock: a 100×114 shield of light pops (240), waits, fades 220; o.broken: a crack and its halves
        /// part. o.towardLeft (field.js passes it: the struck side is the enemy) is carried for the contract; the JS draws nothing by it.
        /// </summary>
        /// <remarks>
        /// A shield snaps up in front of the target as Guard takes the blow — pop 240 (1.2 → 0.8 → 1), 80 more, then it fades over
        /// 220 beside the flow. Review 17: a shield of light — a Guard-coloured rim with a soft glow over a faint see-through face,
        /// so the figure shows through it — with no number of its own (the "Guard −n" tag beside it says how much). `broken`: the
        /// blow took the Guard to 0 — a crack runs down it and its halves part as it fades (battle_ui_ux_v2 §5.6 順 1 割れる).
        /// </remarks>
        // js: scenes/battle/field/fx.js:732
        public Promise ShieldBlock(StagePoint at, string amountText, float ms, bool broken = false, bool towardLeft = false)
        {
            if (!(ms > 0f)) return Promise.Resolved();
            UiNode el = Piece(LayerFx, "ocbg-battle-shield", at, ShieldW, ShieldH);
            // Two halves (one piece unless it breaks): each clipped to its side, so a break can part them.
            var halves = new List<UiNode>();
            int count = broken ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                RectTransform hrt = Ui.Div(el.Rect, "ocbg-battle-shield-half");
                UiNode half = UiNode.Of(hrt);
                half.Place(0f, 0f, ShieldW, ShieldH);   // position: absolute; inset: 0
                half.SetOrigin(0.5f, 0.9f);             // transform-origin: 50% 90%
                PathGraphic art = ShieldArt(hrt, "ocbgShield" + (++_shieldSeq));
                if (broken)
                {
                    // clip-path: inset(0 50% 0 0) / inset(0 0 0 50%)
                    float cx0 = i == 0 ? 0f : ShieldW / 2f;
                    float cx1 = i == 0 ? ShieldW / 2f : ShieldW;
                    art.ClipConvex = new List<Vec2> { new Vec2(cx0, 0f), new Vec2(cx1, 0f), new Vec2(cx1, ShieldH), new Vec2(cx0, ShieldH) };
                }
                halves.Add(half);
            }
            PathGraphic crack = null;
            if (broken)
            {
                crack = PathGraphic.Make(el.Rect, 0f, 0f, ShieldW, ShieldH, "ocbg-battle-fx-svg crack");
                crack.ViewBox = new RectF(0f, 0f, 64f, 72f);
                crack.Add(new PathLayer(ShieldCrack)
                {
                    Stroke = new SolidPaint(Col("ink", 0.9f)),
                    StrokeStyle = new StrokeStyle(2f, Cap.Butt, Join.Round) { NonScaling = true },
                });
                crack.Opacity = 0f;
            }
            Burst(at, "guard", 220f).Forget();
            float pop = Math.Min(240f, ms);
            float rest = Math.Max(0f, ms - pop);
            return Clock.Run(pop, Ease.Out, t =>
            {
                float k = t < 0.5f ? Lerp(1.2f, 0.8f, t * 2f) : Lerp(0.8f, 1f, (t - 0.5f) * 2f);
                if (el) el.SetTransform(0f, 0f, Fix(k, 3), Fix(k, 3), 0f);
                if (crack) crack.Opacity = t < 0.5f ? 0f : Fix((t - 0.5f) * 2f, 3);
            }).Then((Func<Promise>)(() => Clock.Wait(rest))).Then(() =>
            {
                if (!broken) { FadeAndDrop(el, 220f).Forget(); return; }
                // The halves part: each turns and drifts out from the crack and down as the whole fades.
                Clock.Run(220f, Ease.In, t =>
                {
                    for (int i = 0; i < halves.Count; i++)
                    {
                        UiNode half = halves[i];
                        if (!half) continue;
                        float d = i == 0 ? -1f : 1f;
                        // translate(dx, dy) rotate(deg)
                        half.SetTransform(Fix(d * ShieldPartDx * t, 1), Fix(ShieldPartDy * t, 1), 1f, 1f, Fix(d * ShieldPartDeg * t, 1));
                    }
                    if (el) el.Opacity = 1f - t;
                }).Then(() => Drop(el)).Forget();
            });
        }

        // ---- cards in flight (FlyGhostTo / SlideDiscardOntoDraw) ---------------------------------------

        /// <summary>FlyGhostTo / SlideDiscardOntoDraw: the played card's ghost (76×104, or its own face via o.Card) or the 64×84 sheet, in layer.float.</summary>
        /// <remarks>
        /// kind 'discard' | 'exile': the played card's ghost, 76×104 ink at 55%, fading as it flies (FlyGhostTo). kind 'sheet':
        /// a card back of the piles' 64×84 crossing from the discard pile to the draw pile (§3.14). o.card (review fix: the plain
        /// 55 % sheet read as a missing texture): the played card's own face, shrunk to the ghost's 76 × 104, fading out late so
        /// it reads as the card going to its pile.
        /// </remarks>
        // js: scenes/battle/field/fx.js:777
        public Promise Ghost(StagePoint? from, StagePoint? to, GhostOpts o)
        {
            if (from == null || to == null) return Promise.Resolved();
            if (o == null) o = new GhostOpts();
            StagePoint a = from.Value, z = to.Value;
            bool sheet = o.Kind == GhostKind.Sheet;
            float w = sheet ? PileLayout.Width : 76f;
            float ht = sheet ? PileLayout.Height : 104f;
            RectTransform card = !sheet ? CardRectOf(o.Card) : null;
            UiNode el = Piece(LayerFloat, sheet ? "ocbg-battle-sheet" : card ? "ocbg-battle-ghost is-card" : "ocbg-battle-ghost", a, w, ht);
            StackAuto(el.Rect);
            // .ocbg-battle-fx { pointer-events: none }: the card face handed over may carry pointer targets of its own (the
            // hand's hold only suppressed them, and a refresh above would give them back); none of it takes the pointer.
            el.PointerEvents = PointerEvents.None;
            if (sheet) BoxGraphic.Make(el.Rect, SheetCss, 0f, 0f, w, ht, "background");
            else if (!card) BoxGraphic.Make(el.Rect, GhostCss, 0f, 0f, w, ht, "background");
            if (card)
            {
                float k = w / GhostCardW;
                card.SetParent(el.Rect, false);
                UiNode cn = UiNode.Of(card);
                cn.Place(0f, 0f);                     // .is-card > * { position: absolute; left: 0; top: 0 }
                cn.SetOrigin(0f, 0f);                 // transform-origin: 0 0
                cn.SetTransform(0f, 0f, Fix(k, 4), Fix(k, 4), 0f);   // scale(k)
            }
            EaseFn ease = o.Ease ?? (sheet ? Ease.Out : Ease.InOut);
            bool hasCard = card;
            return Clock.Run(o.Ms, ease, t =>
            {
                if (!el) return;
                el.Place(Lerp(a.X, z.X, t) - w / 2f, Lerp(a.Y, z.Y, t) - ht / 2f);
                if (hasCard) el.Opacity = Fix(GhostCardAlpha * (1f - t * t), 3);
                else if (!sheet) el.Opacity = 1f - t;
            }).Then(() => Drop(el));
        }

        /// <summary>clear(): every effect on screen goes.</summary>
        // js: scenes/battle/field/fx.js:803
        public void Clear()
        {
            foreach (UiNode el in _live)
                if (el) Ui.Remove(el);
            _live.Clear();
            _floats.Clear();
        }

        // ---- scene helpers (the DOM / CSS pieces the kit has no one call for) ---------------------------------

        /// <summary>The token `name` at `alpha` (art.js tok) as a colour; an unknown name is transparent, as an invalid CSS colour paints nothing.</summary>
        private static Rgba Col(string name, float? alpha = null)
        {
            return CssColor.TryParse(FieldArt.Tok(name, alpha), out Rgba c) ? c : Rgba.Transparent;
        }

        /// <summary>CSS `rotate(deg) translateX(d) scaleX(s)` (transform-origin set by the caller): the translation turns with the rotation.</summary>
        private static void RotateTranslateScaleX(UiNode el, float deg, float d, float sx)
        {
            double r = deg * Math.PI / 180.0;
            el.SetTransform((float)(d * Math.Cos(r)), (float)(d * Math.Sin(r)), sx, 1f, deg);
        }

        /// <summary>Number.prototype.toFixed(digits) as a number (the JS writes its styles rounded so).</summary>
        private static float Fix(float v, int digits) => (float)Math.Round(v, digits, MidpointRounding.AwayFromZero);

        /// <summary>toFixed(1) as SVG path text.</summary>
        private static string F1(float v) => Fix(v, 1).ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>A number as SVG attribute text (String(n)).</summary>
        private static string N(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>Math.round (half up).</summary>
        private static float JsRound(float v) => (float)Math.Floor(v + 0.5f);

        /// <summary>A piecewise-linear ramp's value at f (stops sorted; clamped at the ends).</summary>
        private static float Ramp1(float f, params (float At, float A)[] stops)
        {
            if (f <= stops[0].At) return stops[0].A;
            for (int i = 1; i < stops.Length; i++)
                if (f <= stops[i].At)
                {
                    float span = stops[i].At - stops[i - 1].At;
                    return span > 0f ? Lerp(stops[i - 1].A, stops[i].A, (f - stops[i - 1].At) / span) : stops[i].A;
                }
            return stops[stops.Length - 1].A;
        }

        /// <summary>A stroke that draws only its drop-shadow (the stroke itself transparent): a filter's light under the layers after it.</summary>
        private static PathLayer ShadowOnlyStroke(PathData pd, float width, float[] dash, float dashOffset, Rgba shadow)
        {
            var l = new PathLayer
            {
                Stroke = new SolidPaint(Rgba.Transparent),
                StrokeStyle = new StrokeStyle(width, Cap.Round, Join.Miter, dash, dashOffset),
                Shadow = new DropShadow(0f, 0f, 8f, shadow),
            };
            l.Paths.Add(pd);
            return l;
        }

        /// <summary>
        /// The card view IHand.GhostCard handed over (o.card): a CardFaceView, a DS element, or any uGUI object; null when there
        /// is none (or it is gone).
        /// </summary>
        private static RectTransform CardRectOf(object card)
        {
            switch (card)
            {
                case null: return null;
                case CardFaceView v: return v.Root ? v.Root : null;
                case OcbElement e: return e.Root ? e.Root : null;
                case GameObject go: return go ? go.transform as RectTransform : null;
                case Component c: return c ? c.transform as RectTransform : null;
                default: return null;
            }
        }

        /// <summary>
        /// A z-index: auto child of layer.float appended now: it stands under the raised ones (the floats' z-index 1, the
        /// prediction tags'), as the browser stacks a positioned z-auto element under every positive z-index sibling.
        /// </summary>
        private static void StackAuto(RectTransform rt)
        {
            Transform parent = rt ? rt.parent : null;
            if (!parent) return;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c == rt) return;
                UiNode n = c.GetComponent<UiNode>();
                if (n && n.Z > 0) { rt.SetSiblingIndex(i); return; }
            }
        }

        /// <summary>
        /// filter: drop-shadow(0 0 blur color) of a w × h box whose alpha is Σ X_k(x)·Y_k(y) (each a piecewise-linear ramp in
        /// box px, 0 outside its first and last stop): a graphic under the content (added first), reaching 3σ past it, whose
        /// alpha is that field blurred by the Gaussian of σ = blur — per axis, exactly, the field being a sum of separable terms.
        /// `maskX` (null = none): the element's mask-image, an alpha ramp across the box. CSS paints the filter first and the
        /// mask over its result, and the mask's painting area is the border box (mask-clip): the shadow is then multiplied by
        /// the mask and cut at the box, so the graphic covers the box alone.
        /// </summary>
        private static VectorGraphic DropShadowOf(RectTransform parent, float w, float h, float blur, Rgba color,
            List<(float, float)> maskX, params (List<(float, float)> X, List<(float, float)> Y)[] terms)
        {
            float sigma = Blur.SigmaOfDropShadow(blur);
            float m = maskX != null ? 0f : 3f * sigma;
            var blurred = new List<(Ramp X, Ramp Y)>();
            foreach ((List<(float, float)> X, List<(float, float)> Y) t in terms)
            {
                Ramp x = BlurRamp(t.X, sigma, m);
                if (maskX != null) x = Masked(x, maskX);
                blurred.Add((x, BlurRamp(t.Y, sigma, m)));
            }
            var paint = new ShadowPaint(color, blurred);
            VectorGraphic g = VectorGraphic.Draw(parent, -m, -m, w + 2f * m, h + 2f * m, (mb, box, feather) =>
            {
                var outline = new Polyline(new List<Vec2> { box.TopLeft, box.TopRight, box.BottomRight, box.BottomLeft }, true);
                Shapes.FillConvex(outline, paint, mb);
            }, "drop-shadow");
            return g;
        }

        /// <summary>
        /// The Gaussian blur (σ) of a piecewise-linear alpha ramp (0 outside its first and last stop), sampled every 0.75 σ
        /// within 3 σ of each stop, positions shifted by `shift`: per segment, (a + b(x − u0))·[Φ(z1) − Φ(z0)] + bσ·[φ(z0) − φ(z1)]
        /// with z = (u − x)/σ.
        /// </summary>
        private static Ramp BlurRamp(List<(float, float)> src, float sigma, float shift)
        {
            var xs = new List<float>();
            float step = sigma * 0.75f;
            foreach ((float, float) s in src)
                for (int k = -4; k <= 4; k++) xs.Add(s.Item1 + k * step);
            xs.Sort();
            var pos = new List<float>();
            foreach (float x in xs)
                if (pos.Count == 0 || x - pos[pos.Count - 1] >= 1f) pos.Add(x);
            var a = new float[pos.Count];
            var p2 = new float[pos.Count];
            const double InvSqrt2Pi = 0.3989422804014327;
            for (int i = 0; i < pos.Count; i++)
            {
                float x = pos[i];
                double g = 0;
                if (!(sigma > 0f))
                {
                    g = src.Count > 0 && x >= src[0].Item1 && x <= src[src.Count - 1].Item1 ? Ramp1Of(src, x) : 0f;
                }
                else
                {
                    for (int s = 0; s + 1 < src.Count; s++)
                    {
                        float u0 = src[s].Item1, u1 = src[s + 1].Item1;
                        if (!(u1 > u0)) continue;
                        double av = src[s].Item2;
                        double bv = (src[s + 1].Item2 - src[s].Item2) / (u1 - u0);
                        double z0 = (u0 - x) / sigma, z1 = (u1 - x) / sigma;
                        double phi0 = InvSqrt2Pi * Math.Exp(-z0 * z0 / 2), phi1 = InvSqrt2Pi * Math.Exp(-z1 * z1 / 2);
                        g += (av + bv * (x - u0)) * (Blur.Phi((float)z1) - Blur.Phi((float)z0)) + bv * sigma * (phi0 - phi1);
                    }
                }
                a[i] = (float)Math.Max(0, Math.Min(1, g));
                p2[i] = x + shift;
            }
            return new Ramp(p2, a);
        }

        /// <summary>A ramp times a mask ramp (box px, clamped at its ends), sampled at the stops of both.</summary>
        private static Ramp Masked(Ramp r, List<(float, float)> mask)
        {
            var xs = new List<float>(r.P);
            foreach ((float, float) s in mask) xs.Add(s.Item1);
            xs.Sort();
            var pos = new List<float>();
            foreach (float x in xs)
                if (pos.Count == 0 || x - pos[pos.Count - 1] >= 0.5f) pos.Add(x);
            var a = new float[pos.Count];
            for (int i = 0; i < pos.Count; i++) a[i] = r.At(pos[i]) * Ramp1Of(mask, pos[i]);
            return new Ramp(pos.ToArray(), a);
        }

        private static float Ramp1Of(List<(float, float)> src, float x)
        {
            var stops = new (float At, float A)[src.Count];
            for (int i = 0; i < src.Count; i++) stops[i] = (src[i].Item1, src[i].Item2);
            return Ramp1(x, stops);
        }

        /// <summary>A sampled alpha ramp along one axis (positions ascending; clamped at the ends).</summary>
        private sealed class Ramp
        {
            public readonly float[] P;
            public readonly float[] A;

            public Ramp(float[] p, float[] a)
            {
                P = p;
                A = a;
            }

            public float At(float v)
            {
                int n = P.Length;
                if (n == 0) return 0f;
                if (v <= P[0]) return A[0];
                if (v >= P[n - 1]) return A[n - 1];
                int lo = 0, hi = n - 1;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) >> 1;
                    if (P[mid] <= v) lo = mid; else hi = mid;
                }
                float span = P[hi] - P[lo];
                return span > 0f ? A[lo] + (A[hi] - A[lo]) * (v - P[lo]) / span : A[hi];
            }
        }

        /// <summary>A colour whose alpha is Σ X_k(x)·Y_k(y) (capped at 1): split along every sample line of every ramp.</summary>
        private sealed class ShadowPaint : Paint
        {
            private readonly Rgba _color;
            private readonly List<(Ramp X, Ramp Y)> _terms;
            private readonly float[] _xs;
            private readonly float[] _ys;

            public ShadowPaint(Rgba color, List<(Ramp X, Ramp Y)> terms)
            {
                _color = color;
                _terms = terms;
                _xs = Merge(terms, true);
                _ys = Merge(terms, false);
            }

            private static float[] Merge(List<(Ramp X, Ramp Y)> terms, bool x)
            {
                var all = new List<float>();
                foreach ((Ramp X, Ramp Y) t in terms) all.AddRange(x ? t.X.P : t.Y.P);
                all.Sort();
                var o = new List<float>();
                foreach (float v in all)
                    if (o.Count == 0 || v - o[o.Count - 1] >= 0.5f) o.Add(v);
                return o.ToArray();
            }

            public override Rgba At(Vec2 p)
            {
                float a = 0f;
                foreach ((Ramp X, Ramp Y) t in _terms) a += t.X.At(p.X) * t.Y.At(p.Y);
                return _color.MulAlpha(Math.Min(1f, a));
            }

            public override IEnumerable<IsoLine> Bands(RectF bounds)
            {
                var o = new List<IsoLine>(_xs.Length + _ys.Length);
                foreach (float x in _xs) if (x > bounds.X && x < bounds.Right) o.Add(new IsoLine(new Vec2(1f, 0f), x));
                foreach (float y in _ys) if (y > bounds.Y && y < bounds.Bottom) o.Add(new IsoLine(new Vec2(0f, 1f), y));
                return o;
            }
        }

        /// <summary>mask-image: a paint's colour times another paint's alpha (both in box px).</summary>
        private sealed class MaskedPaint : Paint
        {
            private readonly Paint _base;
            private readonly Paint _mask;

            public MaskedPaint(Paint basePaint, Paint mask)
            {
                _base = basePaint;
                _mask = mask;
            }

            public override Rgba At(Vec2 p)
            {
                Rgba c = _base != null ? _base.At(p) : Rgba.Transparent;
                return _mask != null ? c.MulAlpha(_mask.At(p).A) : c;
            }

            public override IEnumerable<IsoLine> Bands(RectF bounds)
            {
                var o = new List<IsoLine>();
                IEnumerable<IsoLine> a = _base?.Bands(bounds);
                IEnumerable<IsoLine> b = _mask?.Bands(bounds);
                if (a != null) o.AddRange(a);
                if (b != null) o.AddRange(b);
                return o;
            }

            public override bool NeedsRefine => (_base != null && _base.NeedsRefine) || (_mask != null && _mask.NeedsRefine);
        }
    }
}
