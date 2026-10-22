// The field module of the battle scene (docs/BATTLE_SCENE.md §6b): the layer's backdrop, the floor of cells, the figures
// and their motion, every DepictionFx effect, the floating numbers, the enemy-turn shade, the low-HP vignette and 俊敏's
// marks. Port of scenes/battle/field.js (Unity: FigureView, FloorView, DepictionFx, EnemyTurnShadeView, BackdropLayers and
// the figure / floor parts of DepictionPlayer: ApplyFloor, FollowFigures' positions, Lunge, ReturnHome, SnapHome,
// EnemyMotion, Recoil, Heave, ShakeArena, MoveOnFloor, SnapOnFloor, FlyGhostTo, SlideDiscardOntoDraw, the stance-glow
// parts). Named FieldView, not the JS module name: the View rules boundary.
//
// It computes no rule: where a figure stands, what the floor marks, which shape a blow draws and how strong it is all come
// from the frame and the cues the player hands over; the layout numbers from the ported FloorSpec / FigureFrame /
// EnemyMotionShape / FigureFacing / DefeatShape / StanceGlow / EnemyTurnShadeLayout / BackdropSpec. Every length runs on
// env.Clock with the nominal ms it is given. The field plays no sound (§4.4).
//
// Coordinates are stage px with y down: Unity's y-up offsets are negated, and Unity's z rotations (counter-clockwise
// positive) are written as CSS rotate(−deg) (UiNode.Rotate is the CSS angle).
//
// Several enemies (scene addition with the JS writer's extension, docs/port-notes/depiction-multi-enemy.md; Unity draws
// Enemies[0] only): every enemy of DepictionFrame.Enemies has its own figure, standing on its own Cell. Every method that
// takes a side takes an optional enemy `unit` last (Cue.Unit / UnitFrame.Unit, −1 = the JS undefined); without one, 'Enemy'
// is the frame's focus enemy (Frame.Enemy, the first standing in the line), as before.
//
// Parts: BattleBackdrop (layer backdrop), FieldFloor (layer field), FieldFx (layers fx / float), FieldFreeStep (layer marks),
// FieldArt (silhouettes, tokens). Fills env.Geom.Figure / Receiver / Receivers / FreeStepBoxes / CellBox.
//
// Unity differences (look, JS wins; kit limits):
// - setStyle's cache (f.drawn) keeps the quantized values the JS writes (px to 0.1, degrees to 0.01, opacity / scale to
//   0.001), so the figures move in the same steps and nothing is written twice.
// - CSS opacity is per graphic here (a CanvasGroup), not a composited group: a fading body shows its rim copies through
//   its fill (kit.md §6 risk 2).
// - The front enemy's `filter: drop-shadow(0 0 1.5px …)` on its art is a soft band along the silhouette's outline under
//   the art (a PathLayer drop-shadow, drawn under the four rim copies), not the shadow of the rendered picture.
using System;
using System.Collections.Generic;
using Depiction;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    // js: scenes/battle/field.js:112 (createField)
    public sealed class FieldView : IField
    {
        // ---- constants (field.js:35-110) ----------------------------------------------------------------------

        /// <summary>Where the figures stand when the script draws no floor (the prefab's slots; the core always draws one).</summary>
        // js: scenes/battle/field.js:38 (NO_FLOOR_X)
        private const float NoFloorXPlayer = 660f;
        private const float NoFloorXEnemy = 1260f;

        /// <summary>DepictionPlayer.KeepX: the parts tied to the enemy keep 24 px inside the screen's edges.</summary>
        // js: scenes/battle/field.js:40
        private const float Edge = 24f;

        /// <summary>
        /// battle_ui_ux_v2 §2.3 / §2.4: while the journal drawer (right 30 %) is open the figures slide 15 % of the screen to
        /// the left, and while the pile list (left) is open 15 % to the right, over 220 ms (out); both open, they stay. The
        /// floor slides with them (the figures stand on its cells), and so does everything placed from the figures (panels,
        /// badges, hooks, receivers, effects) through env.Geom. Review 19 / 21: at least 15 %, and as much more as brings
        /// every standing figure's art DRAWER_KEEP inside what the drawer leaves; never so far that a figure's art leaves
        /// the screen on the other side (SCREEN_KEEP). The room is read every frame.
        /// </summary>
        // js: scenes/battle/field.js:57
        private const float ArenaShiftShare = 0.15f;
        private const float ArenaShiftMs = 220f;
        /// <summary>battle_ui_ux_v2 §2.4: either drawer takes 30 % of the screen; a figure the slide brings out of it keeps this far inside.</summary>
        // js: scenes/battle/field.js:60
        private const float DrawerShare = 0.3f;
        private const float DrawerKeep = 12f;
        /// <summary>Scene choice: while a drawer slides the arena, every figure's art keeps this far inside the screen.</summary>
        // js: scenes/battle/field.js:63
        private const float ScreenKeep = 0f;
        /// <summary>An enemy's lunge stops this far short of an ally's body in its way (roomToward; scene choice, Unity has one enemy).</summary>
        // js: scenes/battle/field.js:65
        private const float AllyClear = 8f;
        /// <summary>Review 19: a blow's shape stops this far short of another standing figure's body, and reaches at least this far.</summary>
        // js: scenes/battle/field.js:67
        private const float BlowClear = 8f;
        private const float BlowMin = 40f;
        /// <summary>How far from the chest a blocked blow's shield stands, toward the blow (scene choice; Unity: on the chest).</summary>
        // js: scenes/battle/field.js:70
        private const float ShieldToward = 40f;
        /// <summary>battle_ui_ux_v2 §5.7 順 5: how far a figure leans the way it meant to move when the move went nowhere (scene choice).</summary>
        // js: scenes/battle/field.js:72
        private const float BumpPx = 12f;

        /// <summary>
        /// Paint order of the figures (battle_ui_ux_v2 §2.2.1, §5.7): the hero stands in front; then the enemies, the one
        /// nearest the hero in front of the next; an enemy walking on the floor goes behind every other enemy until it
        /// arrives. The gap number (z 20) stays over them all.
        /// </summary>
        // js: scenes/battle/field.js:80
        private const int ZHero = 9;
        private const int ZFrontEnemy = 8;
        private const int ZWalkingEnemy = 2;

        /// <summary>
        /// Depth separation with two or three enemies (scene choice, review fix): the nearest enemy wears a sharper violet
        /// edge (drop-shadow(0 0 1.5px rgba(139, 111, 208, 0.85))), each enemy further back is drawn a step lighter
        /// (brightness 1 + 0.5 × rank, opaque). One enemy is drawn as Unity draws it.
        /// </summary>
        // js: scenes/battle/field.js:90
        private const string FrontEdgeCss = "drop-shadow(0 0 1.5px rgba(139, 111, 208, 0.85))";
        private static readonly DropShadow FrontEdge = ParseFrontEdge();
        private const float BackLiftStep = 0.5f;

        /// <summary>FRONT_EDGE's drop-shadow, read from the CSS as the JS writes it.</summary>
        private static DropShadow ParseFrontEdge()
        {
            try
            {
                FilterSpec spec = global::Trial.Geometry.Css.Filter(FrontEdgeCss);
                if (spec != null && spec.DropShadows.Count > 0) return spec.DropShadows[0];
            }
            catch (FormatException)
            {
                // the kit could not read it: the same numbers by hand
            }
            return new DropShadow(0f, 0f, 1.5f, Rgba.Bytes(139, 111, 208, 0.85f));
        }

        /// <summary>battle_ui_ux_v2 §5.6 順 5: the low-HP vignette rests at 0.25 and a landed blow pulses it from 0.5 over 200 ms.</summary>
        // js: scenes/battle/field.js:93
        private const float VignetteRest = 0.25f;
        private const float VignettePeak = 0.5f;
        private const float VignettePulseMs = 200f;

        /// <summary>
        /// Review 21: over a bright sky (BackdropSpec's top sky stop above BRIGHT_SKY relative luminance — layers 1, 2 and 4)
        /// the vignette is the deep form (.is-bright) at BRIGHT_BOOST times the strength (the peak held at 1).
        /// </summary>
        // js: scenes/battle/field.js:102
        private const float BrightSky = 0.4f;
        private const float BrightBoost = 2.6f;

        /// <summary>
        /// §5.11 順 2 「身体に薄い光」 (review 21): the body's light is a radial light inside the silhouette — centred on the chest,
        /// full at its middle and gone at 46 % of the figure's height, screened over the art, so the middle of the body glows
        /// and its edge keeps the figure's own rim. Its strength is StanceGlow.Alpha (RenderFigure).
        /// </summary>
        // js: scenes/battle/field.js:110 (BODY_LIGHT)
        private static readonly FigureLight BodyLight = new FigureLight
        {
            Color = "var(--crimson)", At = 0.6f, R = 0.46f, Stops = new[] { (0f, 1f), (0.35f, 0.7f), (0.7f, 0.22f), (1f, 0f) },
        };

        // field.css:72 / :76-80 (the shade and the vignette's backgrounds, verbatim)
        // js: scenes/battle/field.css:77
        private const string VignetteCss =
            "radial-gradient(ellipse 72% 66% at 50% 50%, transparent 58%, color-mix(in srgb, var(--crimson) 70%, transparent) 86%, var(--crimson) 100%)";
        // js: scenes/battle/field.css:80
        private const string VignetteBrightCss =
            "radial-gradient(ellipse 60% 58% at 50% 50%, transparent 66%, color-mix(in srgb, var(--crimson) 70%, transparent) 86%, color-mix(in srgb, var(--crimson) 75%, var(--flood, #06090c)) 100%)";

        private static readonly StagePoint NoPoint = new StagePoint(960f, 400f);

        // ---- a figure (FigureView) ------------------------------------------------------------------------------

        /// <summary>own()'s channels: a newer run on a channel stops the older one touching the figure.</summary>
        private enum Ch { Pos, Body, Shake, Flash, Glow, Heave, Fall, Count }

        /// <summary>setStyle's keys (f.drawn).</summary>
        private enum Drawn { Down, Appear, Tx, Ty, RootRot, Alpha, FootA, FootS, BRot, BSx, BSy, Bright, Flash, Glow, GlowBody, Z, Edge, Count }

        /// <summary>makeFigure's record: the elements and the state the beats move.</summary>
        // js: scenes/battle/field.js:150
        private sealed class Fig
        {
            public UnitSide Side;
            public int Unit;
            public int Size = 1;
            // elements: .ocbg-battle-figure > foot, halo, stance-glow, body > (art, glowbody, flash)
            public RectTransform El, Halo, Body, Art, GlowBody, FlashEl;
            public UiNode ElNode, BodyNode, ArtNode, GlowBodyNode, FlashNode, FootNode;
            public GradientGraphic Glow;
            public GradientGraphic Foot;
            public PathGraphic ArtSvg, FlashSvg, GlowBodySvg;
            /// <summary>The front edge's drop-shadow group under the art (null while it has none).</summary>
            public PathLayer EdgeLayer;
            public FigureArtDef Def;
            /// <summary>flashEl.style.color: the flash overlay's colour (CSS inherits --ink from the root until a flash sets it).</summary>
            public Rgba FlashColor = DsTokens.Ink;
            public string Kind = "";
            public string Rim = "";
            public float Height = FigureFrame.StandardHeight;
            public float Width = 100f;
            public float SvgW = 140f;
            public float Cx = 62f;
            public float X, HomeX, Nudge, Y, Shake;
            public float Rot, Sx = 1f, Sy = 1f, Hv = 1f, RootRot, Alpha = 1f, Bright = 1f, FlashA;
            public float GlowA;
            public bool GlowShown, Down, Falling, Walking, Acting;
            public FigurePose Pose = FigurePose.Idle;
            public float FootA = 1f, FootS = 1f;
            public bool FootKeep;
            public readonly int[] T = new int[(int)Ch.Count];
            public readonly float[] DrawnV = NewDrawn();

            public static float[] NewDrawn()
            {
                var d = new float[(int)Drawn.Count];
                for (int i = 0; i < d.Length; i++) d[i] = float.NaN;
                return d;
            }

            public void ResetDrawn()
            {
                for (int i = 0; i < DrawnV.Length; i++) DrawnV[i] = float.NaN;
            }
        }

        private readonly BattleViewEnv _env;
        private readonly BattleClock _clock;
        private readonly FieldOpts _opts;
        private readonly BattleBackdrop _backdrop;
        private readonly FieldFloor _floor;
        private readonly FieldFx _fx;
        private readonly FieldFreeStep _freeStep;
        private readonly UiNode _fieldLayer;

        private readonly Fig _hero;
        /// <summary>unit → the enemy's figure (scene addition: one per DepictionFrame.Enemies entry), and their creation order (the JS Map's).</summary>
        private readonly Dictionary<int, Fig> _foes = new Dictionary<int, Fig>();
        private readonly List<Fig> _foeList = new List<Fig>();
        /// <summary>The frame's focus enemy (Frame.Enemy.Unit): the one 'Enemy' means when no unit is named.</summary>
        private int _focus;

        /// <summary>§5.18 順 7 (review 21): the next enemy of a chain comes out of the haze — every enemy figure's opacity (1 at rest).</summary>
        private float _enemiesAppear = 1f;
        private float _arenaShake;
        private float _arenaDrawn;
        /// <summary>The arena's slide beside an open drawer (stage px; animated on real time, the journal pauses the scene).</summary>
        private float _shiftX;
        private int _shiftDir;
        private float _shiftTo;
        private int _shiftToken;
        private readonly Action _offFrame;
        private int _arenaToken;
        private bool _alive = true;

        // the enemy-turn shade and the low-HP vignette
        private readonly GradientGraphic _shade;
        private readonly UiNode _shadeNode;
        private bool _shadeOn;
        private readonly GradientGraphic _vignette;
        private readonly float _vigK;
        private float _vigA;
        private float _vigRest;
        private int _vigToken;

        // frame state
        private int _sizePlayer = 1;
        private int _sizeEnemy = 1;
        private bool _multi;
        private DepictionFrame _lastFrame;
        private int? _gapUnit;

        private readonly List<Fig> _standing = new List<Fig>();
        private readonly List<Fig> _all = new List<Fig>();

        private FieldView(BattleViewEnv env, FieldOpts opts)
        {
            _env = env;
            _clock = env.Clock;
            _opts = opts ?? new FieldOpts();
            // js: scenes/battle/field.js:114 (opts.layer || env.layerNo || 1)
            int layerNo = _opts.Layer != 0 ? _opts.Layer : env.LayerNo;
            // js: scenes/battle/field.js:117-124
            _backdrop = BattleBackdrop.Create(env, layerNo);
            _floor = FieldFloor.Create(env, _backdrop.Tile);
            _fx = FieldFx.Create(env);
            // 俊敏's choice on the floor (scene addition with the source's free step; field/freestep.js).
            _freeStep = FieldFreeStep.Create(env, new FreeStepOpts
            {
                OnPick = dir => _opts.OnFreeStepPick?.Invoke(dir),
                OnFocus = dir => _opts.OnFreeStepFocus?.Invoke(dir),
            });
            _fieldLayer = env.Layer.Field ? env.Layer.Node(BattleLayer.Field) : null;

            // ---- figures (FigureView) ----
            // js: scenes/battle/field.js:160-173
            _hero = MakeFigure(UnitSide.Player, -1);
            FoeFig(0);

            // js: scenes/battle/field.js:331
            _offFrame = _clock.OnFrame((dtScene, dtReal) =>
            {
                if (_shiftDir != 0) SlideTo(_shiftDir * ShiftRoom(_shiftDir));
                Render();
            });

            // ---- the enemy-turn shade (EnemyTurnShadeView, §5.5) ----
            // js: scenes/battle/field.js:374-380
            // Black from TopAlpha to BottomAlpha (EnemyTurnShadeLayout's values; Unity's gradient is black).
            string shadeCss = "linear-gradient(180deg, rgba(0,0,0," + JsNum(EnemyTurnShadeLayout.TopAlpha) + "), rgba(0,0,0,"
                + EnemyTurnShadeLayout.BottomAlpha.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "))";
            _shade = GradientGraphic.Css(env.Layer.Shade, shadeCss, 0f, EnemyTurnShadeLayout.Top, L.W, EnemyTurnShadeLayout.Height,
                name: "ocbg-battle-shade");
            _shade.Fringe = false;
            _shadeNode = UiNode.Of(_shade.rectTransform);
            // .ocbg-battle-shade { display: none } until .is-on
            _shadeNode.Visible = false;

            // ---- the low-HP vignette (battle_ui_ux_v2 §5.6 順 5; scene addition, Unity has none) ----
            // With the player's HP under 15 a crimson vignette lines the screen's edge at α 0.25, and each blow that lands pulses
            // it 0.5 → 0.25 over 200 ms (out). In the shade's layer: over the field and the hud, under the hand.
            // js: scenes/battle/field.js:385-394
            bool brightSky = false;
            global::Backdrop.SkyStop[] sky = _backdrop.Spec != null ? _backdrop.Spec.Sky : null;
            if (sky != null && sky.Length > 0)
            {
                global::Backdrop.Rgba top = sky[0].Color;
                brightSky = 0.2126 * Lin(top.R) + 0.7152 * Lin(top.G) + 0.0722 * Lin(top.B) > BrightSky;
            }
            _vignette = GradientGraphic.Css(env.Layer.Shade, brightSky ? VignetteBrightCss : VignetteCss, 0f, 0f, L.W, L.H,
                name: "ocbg-battle-vignette" + (brightSky ? " is-bright" : ""));
            _vignette.Fringe = false;
            _vigK = brightSky ? BrightBoost : 1f;
            DrawVignette();

            // ---- geometry (docs/BATTLE_SCENE.md §2.4) ----
            RegisterGeometry();

            Render();
        }

        /// <summary>createField(env, opts).</summary>
        // js: scenes/battle/field.js:112
        public static FieldView Create(BattleViewEnv env, FieldOpts opts) => new FieldView(env, opts);

        // ---- figures (FigureView) ---------------------------------------------------------------------------------

        // js: scenes/battle/field.js:128
        private Fig MakeFigure(UnitSide side, int unit)
        {
            RectTransform el = Ui.Div(_env.Layer.Field, "ocbg-battle-figure " + (side == UnitSide.Player ? "is-player" : "is-enemy"));
            // el.dataset.unit
            if (side != UnitSide.Player) el.gameObject.name += " unit-" + unit;
            var f = new Fig { Side = side, Unit = side == UnitSide.Player ? -1 : unit, El = el, ElNode = UiNode.Of(el) };
            f.Halo = Ui.Div(el, "ocbg-battle-halo");
            // §5.11 順 2 StanceGlow: the stance attribute's colour (crimson) at α 0.25, a soft light over the body.
            f.Glow = GradientGraphic.Css(el, FieldArt.GlowGradient(a => FieldArt.Tok("crimson", a), StanceGlow.Alpha), 0f, 0f, 0f, 0f,
                name: "ocbg-battle-stance-glow");
            // .ocbg-battle-stance-glow { opacity: 0 }
            f.Glow.Opacity = 0f;
            f.Body = Ui.Div(el, "ocbg-battle-body");
            f.BodyNode = UiNode.Of(f.Body);
            f.Art = Ui.Div(f.Body, "ocbg-battle-art");
            f.ArtNode = UiNode.Of(f.Art);
            // Review 19 (the stance light sat behind the opaque silhouette, so only a red fringe showed): the body's own light
            // (§5.11 順 2 「身体に薄い光」) is the silhouette again in the stance colour at StanceGlow.Alpha, laid over the art; the
            // soft glow behind stays as its halo.
            f.GlowBody = Ui.Div(f.Body, "ocbg-battle-glowbody");
            f.GlowBodyNode = UiNode.Of(f.GlowBody);
            // .ocbg-battle-glowbody { opacity: 0; mix-blend-mode: screen }
            f.GlowBodyNode.Opacity = 0f;
            f.FlashEl = Ui.Div(f.Body, "ocbg-battle-flash");
            f.FlashNode = UiNode.Of(f.FlashEl);
            // .ocbg-battle-flash { opacity: 0 }
            f.FlashNode.Opacity = 0f;
            float x = side == UnitSide.Player ? NoFloorXPlayer : NoFloorXEnemy;
            f.X = x;
            f.HomeX = x;
            return f;
        }

        /// <summary>The enemy figure of `unit`, made when the frame first names it.</summary>
        // js: scenes/battle/field.js:165
        private Fig FoeFig(int unit)
        {
            if (!_foes.TryGetValue(unit, out Fig f))
            {
                f = MakeFigure(UnitSide.Enemy, unit);
                _foes[unit] = f;
                _foeList.Add(f);
            }
            return f;
        }

        /// <summary>The figure of `side` (and of enemy `unit` when given, ≥ 0); null for an enemy the frame never had.</summary>
        // js: scenes/battle/field.js:175
        private Fig FigOf(UnitSide side, int unit)
        {
            if (side == UnitSide.Player) return _hero;
            if (side != UnitSide.Enemy) return null;
            if (unit >= 0) return _foes.TryGetValue(unit, out Fig u) ? u : null;
            if (_foes.TryGetValue(_focus, out Fig f)) return f;
            return _foeList.Count > 0 ? _foeList[0] : null;
        }

        /// <summary>Which way a side steps toward the other: the player faces right, the enemy left.</summary>
        // js: scenes/battle/field.js:182
        private static int Toward(UnitSide side) => side == UnitSide.Player ? 1 : -1;

        /// <summary>
        /// FigureView.ShowArt / StandTall: the DS silhouette for the art id, `height` px tall (FigureFrame.HeightFor), with its
        /// edge light (§4.3: amber 28 % for the player, violet 35 % for every enemy, elites and bosses included) and the foot
        /// light under it (2 × the body, 60 tall).
        /// </summary>
        // js: scenes/battle/field.js:190
        private void Dress(Fig f, string kind, float height, string rim)
        {
            if (f.Kind == kind && f.Height == height && f.Rim == rim) return;
            FigureArtDef def = FieldArt.FigureDef(kind);
            bool foe = f.Side != UnitSide.Player;
            float k = height / def.H;
            f.Kind = kind;
            f.Height = height;
            f.Rim = rim;
            f.Def = def;
            f.Width = def.Body * k;
            f.SvgW = def.W * k;
            f.Cx = def.Cx * k;
            float svgW = R1(f.SvgW);
            float cx = R1(f.Cx);
            float ox = svgW > 0f ? cx / svgW : 0f;
            // style.width / height / transformOrigin = cx px height px, on the figure and on its body
            f.ElNode.Place(0f, 0f, svgW, height);
            f.ElNode.SetOrigin(ox, 1f);
            f.BodyNode.Place(0f, 0f, svgW, height);
            f.BodyNode.SetOrigin(ox, 1f);
            f.ArtNode.Place(0f, 0f, svgW, height);
            f.GlowBodyNode.Place(0f, 0f, svgW, height);
            f.FlashNode.Place(0f, 0f, svgW, height);
            Ui.Clear(f.Art);
            f.EdgeLayer = null;
            f.ArtSvg = FieldArt.DrawFigure(f.Art, def, rim.Length > 0
                ? new DrawFigureOpts { ScaleTo = height, Rim = rim, Foe = foe }
                : new DrawFigureOpts { ScaleTo = height, Foe = foe }).GetComponent<PathGraphic>();
            Ui.Clear(f.FlashEl);
            f.FlashSvg = FieldArt.DrawFigure(f.FlashEl, def, new DrawFigureOpts { ScaleTo = height, Fill = "currentColor", Rim = "transparent", Foe = foe })
                .GetComponent<PathGraphic>();
            if (f.FlashSvg) f.FlashSvg.Tint = f.FlashColor;
            Ui.Clear(f.GlowBody);
            f.GlowBodySvg = FieldArt.DrawFigure(f.GlowBody, def, new DrawFigureOpts { ScaleTo = height, Rim = "transparent", Foe = foe, Light = BodyLight })
                .GetComponent<PathGraphic>();
            if (f.GlowBodySvg) f.GlowBodySvg.ScreenBlend = true;
            f.Halo.gameObject.SetActive(false);
            // §4.3 足元の灯り: a radial light in an ellipse under the feet, behind the body.
            if (f.Foot) Ui.Remove(f.Foot);
            f.Foot = Ocb.FootLight(f.El, f.Width * FigureFrame.FootLightWidthRatio, f.Side != UnitSide.Player);
            f.Foot.gameObject.name += " ocbg-battle-foot";
            f.FootNode = UiNode.Of(f.Foot.rectTransform);
            f.FootNode.Place(R1(f.Cx - f.Width), height - FigureFrame.FootLightHeight / 2f, f.Width * FigureFrame.FootLightWidthRatio,
                FigureFrame.FootLightHeight);
            f.Foot.rectTransform.SetAsFirstSibling();
            // StanceGlow's rect: anchors (−0.2, −0.02)〜(1.2, 1.04) of the frame, the frame as wide as the body.
            Ui.Place(f.Glow.rectTransform, R1(f.Cx - 0.7f * f.Width), R1(-0.04f * height), R1(1.4f * f.Width), R1(1.06f * height));
            f.ResetDrawn();
        }

        /// <summary>setStyle(f, el, key, prop, value): writes only what changed since the last write.</summary>
        // js: scenes/battle/field.js:230
        private static bool SetStyle(Fig f, Drawn key, float value)
        {
            int i = (int)key;
            if (f.DrawnV[i].Equals(value)) return false;
            f.DrawnV[i] = value;
            return true;
        }

        // js: scenes/battle/field.js:238
        private void RenderFigure(Fig f)
        {
            if (SetStyle(f, Drawn.Down, f.Down ? 1f : 0f)) f.ElNode.Visible = !f.Down;
            if (f.Down) return;
            float appear = f.Side == UnitSide.Player ? 1f : _enemiesAppear;
            float ap = appear >= 1f ? 1f : R3(appear);
            if (SetStyle(f, Drawn.Appear, ap)) f.ElNode.Opacity = ap;
            float tx = R1(f.X + f.Shake - f.Cx);
            float ty = R1(L.FeetY - f.Height + f.Y);
            float rr = f.RootRot != 0f ? R2(f.RootRot) : 0f;
            if (SetStyle(f, Drawn.Tx, tx) | SetStyle(f, Drawn.Ty, ty) | SetStyle(f, Drawn.RootRot, rr))
                f.ElNode.SetTransform(tx, ty, 1f, 1f, rr);
            // DefeatShape's fade is the body's (and its stance light's); the foot light fades with it, unless a fall among
            // standing enemies keeps it for its own 300 ms after (battle_ui_ux_v2 §5.15 順 5, fadeFoot).
            float alpha = f.Alpha >= 1f ? 1f : R3(f.Alpha);
            if (SetStyle(f, Drawn.Alpha, alpha)) f.BodyNode.Opacity = alpha;
            float footA = (f.FootKeep ? 1f : f.Alpha) * f.FootA;
            if (f.Foot)
            {
                float fa = footA >= 1f ? 1f : R3(footA);
                if (SetStyle(f, Drawn.FootA, fa)) f.Foot.Opacity = fa;
                float fs = f.FootS >= 1f ? 1f : R3(f.FootS);
                if (SetStyle(f, Drawn.FootS, fs)) f.FootNode.ScaleX = fs;
            }
            float sy = f.Sy * f.Hv;
            float brot = f.Rot != 0f ? R2(f.Rot) : 0f;
            bool scaled = f.Sx != 1f || sy != 1f;
            float bsx = scaled ? R3(f.Sx) : 1f;
            float bsy = scaled ? R3(sy) : 1f;
            if (SetStyle(f, Drawn.BRot, brot) | SetStyle(f, Drawn.BSx, bsx) | SetStyle(f, Drawn.BSy, bsy))
                f.BodyNode.SetTransform(0f, 0f, bsx, bsy, brot);
            float bright = f.Bright < 1f ? R3(f.Bright) : 1f;
            if (SetStyle(f, Drawn.Bright, bright)) f.BodyNode.Filter = bright < 1f ? new ColorFilter(bright) : ColorFilter.None;
            float flash = f.FlashA > 0f ? R3(f.FlashA) : 0f;
            if (SetStyle(f, Drawn.Flash, flash)) f.FlashNode.Opacity = flash;
            float glowA = f.GlowA * f.Alpha;
            float glow = glowA > 0f ? R3(glowA) : 0f;
            if (SetStyle(f, Drawn.Glow, glow)) f.Glow.Opacity = glow;
            float bodyLight = glowA * StanceGlow.Alpha;
            float bl = bodyLight > 0f ? R3(bodyLight) : 0f;
            if (SetStyle(f, Drawn.GlowBody, bl)) f.GlowBodyNode.Opacity = bl;
        }

        /// <summary>The paint order and the depth look of every figure (Z_HERO … FRONT_EDGE).</summary>
        // js: scenes/battle/field.js:267
        private void RenderDepth()
        {
            if (SetStyle(_hero, Drawn.Z, ZHero)) _hero.ElNode.Z = ZHero;
            _standing.Clear();
            foreach (Fig f in _foeList)
                if (!f.Down) _standing.Add(f);
            bool several = _standing.Count > 1;
            // a stable sort, as Array.prototype.sort
            for (int i = 1; i < _standing.Count; i++)
            {
                Fig cur = _standing[i];
                float key = Math.Abs(cur.X - _hero.X);
                int j = i - 1;
                while (j >= 0 && Math.Abs(_standing[j].X - _hero.X) > key)
                {
                    _standing[j + 1] = _standing[j];
                    j--;
                }
                _standing[j + 1] = cur;
            }
            // Review fix (a back enemy's lunge merged with the front enemy's body): the enemy acting now stands in front of the
            // others until it is home again; the rest keep the nearest-first order behind it.
            Fig acting = null;
            if (several)
                foreach (Fig f in _standing)
                    if (f.Acting && !f.Walking) { acting = f; break; }
            int rank = 0;
            int zRank = acting != null ? 1 : 0;
            foreach (Fig f in _standing)
            {
                int z = f.Walking && several ? ZWalkingEnemy : f == acting ? ZFrontEnemy : ZFrontEnemy - zRank++;
                if (SetStyle(f, Drawn.Z, z)) f.ElNode.Z = z;
                int r = several ? rank : -1;
                if (SetStyle(f, Drawn.Edge, r)) ApplyEdge(f, r);
                rank += 1;
            }
        }

        /// <summary>
        /// The art's `filter`: r 0 = FRONT_EDGE (the violet drop-shadow), r &gt; 0 = brightness(1 + BACK_LIFT_STEP × r), else none.
        /// </summary>
        // js: scenes/battle/field.js:281
        private static void ApplyEdge(Fig f, int r)
        {
            f.ArtNode.Filter = r > 0 ? new ColorFilter(R2(1f + BackLiftStep * r)) : ColorFilter.None;
            PathGraphic svg = f.ArtSvg;
            if (!svg) return;
            bool front = r == 0;
            if (front && f.EdgeLayer == null && f.Def != null)
            {
                // FRONT_EDGE (FrontEdgeCss): the shadow of the silhouette's outline, under every group of the art.
                var paths = new string[f.Def.Paths.Count];
                for (int i = 0; i < paths.Length; i++) paths[i] = f.Def.Paths[i];
                f.EdgeLayer = new PathLayer(paths).WithFill(Rgba.Transparent).WithShadow(FrontEdge.X, FrontEdge.Y, FrontEdge.Blur, FrontEdge.Color);
                f.EdgeLayer.FillFringe = false;
                svg.Layers.Insert(0, f.EdgeLayer);
                svg.LayersChanged();
            }
            else if (!front && f.EdgeLayer != null)
            {
                svg.Layers.Remove(f.EdgeLayer);
                f.EdgeLayer = null;
                svg.LayersChanged();
            }
        }

        // js: scenes/battle/field.js:286
        private void Render()
        {
            RenderDepth();
            RenderFigure(_hero);
            foreach (Fig f in _foeList) RenderFigure(f);
            float dx = _arenaShake + _shiftX;
            float a = Math.Abs(dx) > 0.05f ? R1(dx) : 0f;
            if (a != _arenaDrawn)
            {
                _arenaDrawn = a;
                if (_fieldLayer) _fieldLayer.TranslateX = a;
            }
        }

        /// <summary>
        /// How far the arena slides toward `dir` now: ARENA_SHIFT_SHARE — or further (review 21: the trio's third enemy stayed
        /// cut by the journal's edge), as far as brings every standing figure's art out from under the drawer (DRAWER_KEEP
        /// inside what it leaves) — held short only where a figure's art would leave the screen on the other side.
        /// </summary>
        // js: scenes/battle/field.js:299
        private float ShiftRoom(int dir)
        {
            if (dir == 0) return 0f;
            float want = ArenaShiftShare * L.W;
            float room = float.PositiveInfinity;
            float lo = L.W * DrawerShare + DrawerKeep;
            float hi = L.W * (1f - DrawerShare) - DrawerKeep;
            foreach (Fig f in AllFigures())
            {
                if (f.Down) continue;
                float left = f.HomeX - f.Cx;
                float right = left + f.SvgW;
                want = Math.Max(want, dir > 0 ? lo - left : right - hi);
                room = Math.Min(room, dir > 0 ? L.W - ScreenKeep - right : left - ScreenKeep);
            }
            return Math.Max(0f, Math.Min(want, room));
        }

        /// <summary>The slide toward `to` (stage px) over ARENA_SHIFT_MS (out), on real time.</summary>
        // js: scenes/battle/field.js:315
        private void SlideTo(float to)
        {
            if (Math.Abs(to - _shiftTo) < 0.5f) return;
            _shiftTo = to;
            int mine = ++_shiftToken;
            float from = _shiftX;
            float t0 = _env.Real.Now;
            void Step()
            {
                if (mine != _shiftToken || !_alive) return;
                float t = Math.Min(1f, (_env.Real.Now - t0) / ArenaShiftMs);
                float e = 1f - (1f - t) * (1f - t);
                _shiftX = from + (to - from) * e;
                Render();
                if (t < 1f) _env.Real.Wait(16f).Then(() => Step()).Forget();
            }
            Step();
        }

        /// <summary>A run that stops touching the figure once a newer one on the same channel starts; resolves to whether it is still the newest.</summary>
        // js: scenes/battle/field.js:337
        private Promise<bool> Own(Fig f, Ch channel, float ms, EaseFn ease, Action<float> fn)
        {
            int c = (int)channel;
            int mine = ++f.T[c];
            var s = new PromiseSource<bool>();
            Promise run = _clock.Run(ms, ease, t => { if (f.T[c] == mine) fn(t); });
            run.OnSettled(() => s.Resolve(f.T[c] == mine));
            return s.Promise;
        }

        /// <summary>FigureView.RestBody: no tilt, no stretch, its colour (the flash gone).</summary>
        // js: scenes/battle/field.js:343
        private static void RestBody(Fig f)
        {
            f.T[(int)Ch.Body]++;
            f.T[(int)Ch.Heave]++;
            f.T[(int)Ch.Flash]++;
            f.Rot = 0f;
            f.Sx = 1f;
            f.Sy = 1f;
            f.Hv = 1f;
            f.FlashA = 0f;
            f.Bright = 1f;
        }

        /// <summary>FigureView.Settle: whatever a stopped effect left on the figure (position is the caller's).</summary>
        // js: scenes/battle/field.js:356
        private static void Settle(Fig f)
        {
            f.Falling = false;
            f.Acting = false;
            f.T[(int)Ch.Fall]++;
            f.T[(int)Ch.Shake]++;
            f.Shake = 0f;
            f.RootRot = 0f;
            f.Alpha = 1f;
            f.FootA = 1f;
            f.FootS = 1f;
            f.FootKeep = false;
            f.Y = 0f;
            RestBody(f);
            f.Pose = FigurePose.Idle;
        }

        // ---- the low-HP vignette ----------------------------------------------------------------------------------

        // js: scenes/battle/field.js:393
        private void DrawVignette()
        {
            if (_vignette) _vignette.Opacity = _vigA > 0.001f ? R3(_vigA) : 0f;
        }

        // ---- frame state ------------------------------------------------------------------------------------------

        // js: scenes/battle/field.js:402
        private float FeetX(UnitSide side, int cell, int size)
        {
            float? x = _floor.FeetX(cell, size);
            return x ?? (side == UnitSide.Player ? NoFloorXPlayer : NoFloorXEnemy);
        }

        /// <summary>
        /// Where figure `f` stands on `cell`: its feet in the middle of its cells (FloorSpec.FeetX; battle_ui_ux_v2 §2.1).
        /// Review 19: a silhouette wider than its cell is no longer moved in off the stance lists; its art may reach behind the
        /// stance list's frame, which is drawn over it. f.Nudge stays 0 (dev/battle-read.js reads it).
        /// </summary>
        // js: scenes/battle/field.js:412
        private float StandX(Fig f, int cell, int size)
        {
            f.Nudge = 0f;
            return FeetX(f.Side, cell, size);
        }

        /// <summary>
        /// How far enemy figure `f` may move toward `dir` before its body meets another standing enemy's (review fix, several
        /// enemies). The lunge stops ALLY_CLEAR short of the ally's body; with no ally in the way it is not limited.
        /// </summary>
        // js: scenes/battle/field.js:423
        private float RoomToward(Fig f, int dir)
        {
            float room = float.PositiveInfinity;
            if (!_multi) return room;
            float edge = f.X + dir * f.Width / 2f;
            foreach (Fig o in _foeList)
            {
                if (o == f || o.Down || o.Falling) continue;
                if ((o.X - f.X) * dir <= 0f) continue; // behind it
                float near = o.X - dir * o.Width / 2f;
                room = Math.Min(room, Math.Max(0f, (near - edge) * dir - AllyClear));
            }
            return room;
        }

        /// <summary>
        /// blowRoom(f, striker): how far a blow's shape may reach left and right of figure `f`'s chest before it would cross
        /// another standing figure (its striker apart): BLOW_CLEAR short of that figure's body, never under BLOW_MIN (review
        /// 19). Named BlowRoomOf: BlowRoom is the answer's type (Fx.cs).
        /// </summary>
        // js: scenes/battle/field.js:440
        private BlowRoom BlowRoomOf(Fig f, Fig striker)
        {
            float left = float.PositiveInfinity;
            float right = float.PositiveInfinity;
            foreach (Fig o in AllFigures())
            {
                if (o == f || o == striker || o.Down || o.Falling) continue;
                if (o.X > f.X) right = Math.Min(right, Math.Max(BlowMin, o.X - o.Width / 2f - f.X - BlowClear));
                else left = Math.Min(left, Math.Max(BlowMin, f.X - (o.X + o.Width / 2f) - BlowClear));
            }
            return new BlowRoom(left, right);
        }

        /// <summary>A point of the figure on the screen (the arena's slide included: the effects' layers do not slide).</summary>
        // js: scenes/battle/field.js:451
        private StagePoint Point(Fig f, float frac) => new StagePoint(f.X + _shiftX, L.FeetY + f.Y - f.Height * frac);

        // js: scenes/battle/field.js:454
        private StagePoint ChestOf(Fig f) => Point(f, L.Chest);

        // js: scenes/battle/field.js:455
        private StagePoint HeadOf(Fig f) => Point(f, L.Head);

        // js: scenes/battle/field.js:456
        private StagePoint FeetOf(Fig f) => new StagePoint(f.X + _shiftX, L.FeetY + f.Y);

        /// <summary>
        /// battle_ui_ux_v2 §7 (review 21): the enemies read to 開示度 0 — their omen's Disclosure — whose aimed cells the floor does
        /// not draw. unit → bool; −1 asks for the frame's one omen.
        /// </summary>
        // js: scenes/battle/field.js:464
        private static Func<int, bool> UnreadOf(DepictionFrame frame)
        {
            var omens = new List<OmenFrame>();
            if (frame != null && frame.Omens != null && frame.Omens.Count > 0) omens.AddRange(frame.Omens);
            else if (frame != null && frame.Omen != null) omens.Add(frame.Omen);
            bool Low(OmenFrame o) => o != null && o.Disclosure < 1;
            return unit =>
            {
                if (unit < 0) return Low(frame?.Omen);
                OmenFrame found = null;
                foreach (OmenFrame x in omens)
                    if (x != null && x.Unit == unit) { found = x; break; }
                return Low(found ?? (omens.Count == 1 ? omens[0] : null));
            };
        }

        /// <summary>One enemy a frame describes: { unit, u, size, cell }.</summary>
        private readonly struct FrameEnemy
        {
            public readonly int Unit;
            public readonly UnitFrame U;
            public readonly int Size;
            public readonly int Cell;

            public FrameEnemy(int unit, UnitFrame u, int size, int cell)
            {
                Unit = unit;
                U = u;
                Size = size;
                Cell = cell;
            }
        }

        /// <summary>The enemies a frame describes, with their cells: every one when it carries the list, else its one enemy.</summary>
        // js: scenes/battle/field.js:474
        private static List<FrameEnemy> EnemiesOf(DepictionFrame frame, FloorFrame fl)
        {
            var list = new List<UnitFrame>();
            if (frame.Enemies != null && frame.Enemies.Count > 0) list.AddRange(frame.Enemies);
            else list.Add(frame.Enemy);
            var o = new List<FrameEnemy>(list.Count);
            for (int k = 0; k < list.Count; k++)
            {
                UnitFrame u = list[k];
                int unit = u != null && u.Unit >= 0 ? u.Unit : k;
                bool isFocus = u == frame.Enemy || list.Count == 1;
                int focusSize = isFocus && fl != null ? fl.EnemySize : 1;
                int size = Math.Max(1, u != null && u.Size != 0 ? u.Size : focusSize != 0 ? focusSize : 1);
                int cell = u != null && u.Cell > 0 ? u.Cell : isFocus && fl != null ? fl.EnemyCell : 0;
                o.Add(new FrameEnemy(unit, u ?? new UnitFrame(), size, cell));
            }
            return o;
        }

        /// <summary>
        /// The enemy a RangeSwitch cue moves, and its cell after (MoveOnFloor / SnapOnFloor). Several enemies (review fix: a card
        /// that killed the front enemy and then moved slid the dead enemy onto the next one): only Cue.Unit with its
        /// UnitCellAfter moves an enemy (−1 when the player moved). One enemy: the frame's enemy and EnemyCellAfter, as Unity.
        /// </summary>
        // js: scenes/battle/field.js:492
        private (Fig E, int ECell) MovedEnemyOf(Cue cue)
        {
            bool named = cue.Unit >= 0 && _foes.ContainsKey(cue.Unit);
            if (named)
            {
                int cell = cue.UnitCellAfter > 0 ? cue.UnitCellAfter : _multi ? 0 : cue.EnemyCellAfter;
                return (_foes[cue.Unit], cell);
            }
            if (_multi) return (null, 0);
            return (FigOf(UnitSide.Enemy, -1), cue.EnemyCellAfter);
        }

        /// <summary>[hero, ...foes.values()].</summary>
        private List<Fig> AllFigures()
        {
            _all.Clear();
            _all.Add(_hero);
            _all.AddRange(_foeList);
            return _all;
        }

        // ---- the api: frame ---------------------------------------------------------------------------------------

        /// <summary>
        /// ApplyFrame's field half: art and Down, heights (FigureFrame.HeightFor(size)), the floor (FloorView.Bind), the player
        /// on its cells at once, the enemy's HOME on its cells (moved there now only when first), the stance lights snapped
        /// when first, the enemy-turn shade.
        /// </summary>
        // js: scenes/battle/field.js:509
        public void ApplyFrame(DepictionFrame frame, bool first = false)
        {
            if (frame == null) return;
            FloorFrame fl = frame.Floor;
            _lastFrame = frame;
            _floor.Bind(fl, new FloorBindOpts { Unread = UnreadOf(frame) });
            if (_gapUnit != null) ShowGapTo(_gapUnit.Value);
            _sizePlayer = fl != null ? Math.Max(1, fl.PlayerSize != 0 ? fl.PlayerSize : 1) : 1;
            _sizeEnemy = fl != null ? Math.Max(1, fl.EnemySize != 0 ? fl.EnemySize : 1) : 1;
            Fig p = _hero;
            Dress(p, FieldArt.SilhouetteOf(UnitSide.Player, frame.Player != null ? frame.Player.ArtId : null, _sizePlayer),
                FigureFrame.HeightFor(_sizePlayer), "");
            SetDown(UnitSide.Player, frame.Player != null && frame.Player.Down);
            // ApplyFloor: the player stands on its cells now; each enemy's home is its cells (the next return or snap moves it).
            p.HomeX = fl != null && _floor.Shown ? StandX(p, fl.PlayerCell, _sizePlayer) : NoFloorXPlayer;
            p.T[(int)Ch.Pos]++;
            p.X = p.HomeX;
            List<FrameEnemy> list = EnemiesOf(frame, fl);
            _multi = list.Count > 1;
            _focus = frame.Enemy != null && frame.Enemy.Unit >= 0 ? frame.Enemy.Unit : list[0].Unit;
            foreach (FrameEnemy e in list)
            {
                Fig f = FoeFig(e.Unit);
                f.Size = e.Size;
                Dress(f, FieldArt.SilhouetteOf(UnitSide.Enemy, e.U.ArtId, e.Size), FigureFrame.HeightFor(e.Size), "");
                SetDown(UnitSide.Enemy, e.U.Down, e.Unit);
                f.HomeX = fl != null && _floor.Shown && e.Cell > 0 ? StandX(f, e.Cell, e.Size) : NoFloorXEnemy;
                if (first)
                {
                    f.T[(int)Ch.Pos]++;
                    f.X = f.HomeX;
                    f.Y = 0f;
                    SetStanceGlow(UnitSide.Enemy, StanceGlow.IsOn(e.U), e.Unit);
                }
            }
            if (first) SetStanceGlow(UnitSide.Player, StanceGlow.IsOn(frame.Player));
            // Review fix: once the battle is decided the gap number goes (it measured to the fallen enemy's empty cell).
            _floor.SetGapHidden(frame.Outcome != BattleOutcome.Ongoing);
            SetEnemyTurnShade(frame.EnemyTurn);
            Render();
        }

        /// <summary>
        /// battle-visual-v1 §4.2 間合いの数字 with several enemies (port notes, "The one N"): while a held card frames enemy
        /// `unit`, the floor's one number is that enemy's N (UnitFrame.Gap) over the empty cells up to its Cell; −1 puts the
        /// frame's own back (Floor.Gap, the nearest standing enemy). With one enemy nothing changes.
        /// </summary>
        // js: scenes/battle/field.js:552
        public void ShowGapTo(int unit)
        {
            _gapUnit = unit >= 0 ? unit : (int?)null;
            DepictionFrame fr = _lastFrame;
            if (fr == null || fr.Floor == null || !_floor.Shown || !_multi) return;
            FloorFrame fl = fr.Floor;
            UnitFrame u = null;
            if (_gapUnit != null && fr.Enemies != null)
                foreach (UnitFrame e in fr.Enemies)
                    if (e != null && e.Unit == _gapUnit.Value) { u = e; break; }
            int playerSize = Math.Max(1, fl.PlayerSize != 0 ? fl.PlayerSize : 1);
            if (u != null && !u.Down && u.Gap >= 0) _floor.SetGap(u.Gap, fl.PlayerCell, playerSize, u.Cell);
            else _floor.SetGap(fl.Gap, fl.PlayerCell, playerSize, fl.EnemyCell);
        }

        /// <summary>The enemies the field draws (standing or not), by unit.</summary>
        // js: scenes/battle/field.js:563
        public IReadOnlyList<int> EnemyUnits()
        {
            var units = new int[_foeList.Count];
            for (int i = 0; i < units.Length; i++) units[i] = _foeList[i].Unit;
            return units;
        }

        /// <summary>
        /// Kept for the contract (docs/BATTLE_SCENE.md §6b): the back enemies used to be drawn from source.State as static
        /// extras. Every enemy of DepictionFrame.Enemies now has its own figure (ApplyFrame), so this draws nothing.
        /// </summary>
        // js: scenes/battle/field.js:569
        public void SetExtras(IReadOnlyList<object> list)
        {
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.8 提示 (scene addition; Unity redraws the floor with the event's frame): an omen is shown — the floor
        /// takes `frame`'s cells now (the event's After), the newly aimed ones drawn left to right, 30 ms a cell (`now`: at once).
        /// </summary>
        // js: scenes/battle/field.js:576
        public Promise DrawAim(DepictionFrame frame, int unit, bool now = false)
        {
            if (frame == null || frame.Floor == null) return Promise.Resolved();
            _floor.Bind(frame.Floor, new FloorBindOpts
            {
                Reveal = new FloorReveal { Unit = unit, Ms = now ? 0f : (float?)null },
                Unread = UnreadOf(frame),
            });
            if (_gapUnit != null) ShowGapTo(_gapUnit.Value);
            return Promise.Resolved();
        }

        // js: scenes/battle/field.js:582
        public void ShowReach(IReadOnlyList<int> cells) => _floor.ShowReach(cells);

        /// <summary>The battle is over: the enemies' aimed / dense / move cells fade over `ms` with their omen badges, and stay clear.</summary>
        // js: scenes/battle/field.js:584
        public Promise RetireAim(float ms) => _floor.RetireAim(ms);

        /// <summary>
        /// battle_ui_ux_v2 §5.15 順 5 (review fix): enemy `unit` falls — its aimed, dense and move cells go now, with its omen
        /// badge, instead of with the frame after its fall.
        /// </summary>
        // js: scenes/battle/field.js:589
        public void RetireAimOf(int unit) => _floor.RetireUnit(unit >= 0 ? unit : -1, !_multi);

        /// <summary>battle_ui_ux_v2 §2.2.2: enemy `unit`'s aimed cells alone stay lit (its omen badge is hovered); −1 for all.</summary>
        // js: scenes/battle/field.js:591
        public void FocusAim(int unit) => _floor.FocusAim(unit);

        // js: scenes/battle/field.js:592
        public void HideReach() => _floor.HideReach();

        /// <summary>
        /// battle_ui_ux_v2 §2.4: the arena slides `dir` × 15 % of the screen (−1 left for the journal, +1 right for the pile
        /// list, 0 back) over 220 ms (out), on real time so it runs while the journal holds the scene.
        /// </summary>
        // js: scenes/battle/field.js:598
        public void SetShift(int dir)
        {
            _shiftDir = Math.Max(-1, Math.Min(1, dir));
            SlideTo(_shiftDir * ShiftRoom(_shiftDir));
        }

        // js: scenes/battle/field.js:602
        public float ShiftX => _shiftX;

        /// <summary>How far figure `side` (enemy `unit`) stands moved in from its cell's middle (StandX; 0 for most).</summary>
        // js: scenes/battle/field.js:604
        public float NudgeOf(UnitSide side, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            return f != null ? f.Nudge : 0f;
        }

        /// <summary>§5.6 順 5: the vignette rests at VIGNETTE_REST while `on` (HP under 15), at 0 otherwise (a 200 ms fade).</summary>
        // js: scenes/battle/field.js:607
        public Promise SetLowHp(bool on)
        {
            float rest = on ? VignetteRest * _vigK : 0f;
            if (rest == _vigRest) return Promise.Resolved();
            _vigRest = rest;
            int mine = ++_vigToken;
            float from = _vigA;
            return _clock.Run(VignettePulseMs, Ease.Out, t =>
            {
                if (mine == _vigToken)
                {
                    _vigA = Lerp(from, rest, t);
                    DrawVignette();
                }
            });
        }

        /// <summary>§5.6 順 5: a blow landed with HP under 15: the vignette pulses VIGNETTE_PEAK → its rest over `ms` (out).</summary>
        // js: scenes/battle/field.js:616
        public Promise PulseLowHp(float ms)
        {
            if (_vigRest <= 0f) return Promise.Resolved();
            int mine = ++_vigToken;
            float peak = Math.Min(1f, VignettePeak * _vigK);
            _vigA = peak;
            DrawVignette();
            return _clock.Run(ms, Ease.Out, t =>
            {
                if (mine == _vigToken)
                {
                    _vigA = Lerp(peak, _vigRest, t);
                    DrawVignette();
                }
            });
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.18 順 7 (review 21): a chain's next enemy comes out of the haze — the enemy figures from `from` to 1
        /// over `ms` (out). `ms` 0 puts them at `from` (0: hidden until the next call).
        /// </summary>
        // js: scenes/battle/field.js:629
        public Promise AppearEnemies(float ms, float from = 0f)
        {
            _enemiesAppear = from;
            Render();
            if (!(ms > 0f)) return Promise.Resolved();
            return _clock.Run(ms, Ease.Out, t => { _enemiesAppear = from + (1f - from) * t; }).Then(() =>
            {
                _enemiesAppear = 1f;
                Render();
            });
        }

        /// <summary>EnemyTurnShadeView.Show: the black below y 790 is on or off (SetActive, no fade).</summary>
        // js: scenes/battle/field.js:636
        public void SetEnemyTurnShade(bool on)
        {
            if (on == _shadeOn) return;
            _shadeOn = on;
            // .ocbg-battle-shade.is-on { display: block }
            if (_shadeNode) _shadeNode.Visible = on;
        }

        // ---- the api: figure state --------------------------------------------------------------------------------

        /// <summary>FigureView.SetPose: the silhouette has one picture; the pose is kept for art. A falling figure stays down.</summary>
        // js: scenes/battle/field.js:645
        public void SetPose(UnitSide side, FigurePose pose, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            if (f == null) return;
            if (f.Falling && pose != FigurePose.Down) return;
            f.Pose = pose;
        }

        /// <summary>FigureView.SetDown: gone (hidden whole) or standing, at rest either way.</summary>
        // js: scenes/battle/field.js:653
        public void SetDown(UnitSide side, bool down, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            if (f == null) return;
            if (down == f.Down) return;
            f.Down = down;
            Settle(f);
            Render();
        }

        // js: scenes/battle/field.js:662
        public bool EnemyAway(int unit = -1)
        {
            Fig e = FigOf(UnitSide.Enemy, unit);
            if (e == null || e.Down) return false;
            return Math.Abs(e.X - e.HomeX) > 0.5f || Math.Abs(e.Y) > 0.5f;
        }

        // js: scenes/battle/field.js:667
        public void SetEnemyHomeCell(int cell, int size, int unit = -1)
        {
            Fig e = FigOf(UnitSide.Enemy, unit);
            if (!_floor.Shown || e == null || !(cell > 0)) return;
            int s = size != 0 ? size : e.Size != 0 ? e.Size : _sizeEnemy;
            e.HomeX = StandX(e, cell, Math.Max(1, s));
        }

        /// <summary>SnapHome: onto its home at once, standing.</summary>
        // js: scenes/battle/field.js:673
        public void SnapHome(UnitSide side, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            if (f == null) return;
            f.T[(int)Ch.Pos]++;
            f.X = f.HomeX;
            f.Y = 0f;
            f.Walking = false;
            f.Acting = false;
            SetPose(side, FigurePose.Idle, f.Unit);
            Render();
        }

        // js: scenes/battle/field.js:684
        public bool StanceGlowShown(UnitSide side, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            return f != null && f.GlowShown;
        }

        /// <summary>FigureView.SetStanceGlow: on or off at once; a fade still running stops touching the light.</summary>
        // js: scenes/battle/field.js:686
        public void SetStanceGlow(UnitSide side, bool on, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            if (f == null) return;
            f.GlowShown = on;
            f.T[(int)Ch.Glow]++;
            f.GlowA = on ? 1f : 0f;
            Render();
        }

        /// <summary>FigureView.FadeStanceGlow: in or out over ms (ease out); a newer call takes the light over.</summary>
        // js: scenes/battle/field.js:695
        public Promise FadeStanceGlow(UnitSide side, bool on, float ms, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            if (f == null) return Promise.Resolved();
            f.GlowShown = on;
            float from = f.GlowA;
            float to = on ? 1f : 0f;
            return Own(f, Ch.Glow, ms, Ease.Out, t => { f.GlowA = Lerp(from, to, t); });
        }

        // ---- the api: beats ---------------------------------------------------------------------------------------

        /// <summary>Lunge: Act pose, `px` toward the other side (ease out). The player's home is unchanged.</summary>
        // js: scenes/battle/field.js:706
        public Promise Lunge(UnitSide side, float px, float ms, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            if (f == null) return Promise.Resolved();
            SetPose(side, FigurePose.Act, f.Unit);
            float from = f.X;
            // An enemy's step in stops short of an ally standing in its way, as its motion does (RoomToward).
            float to = from + (side == UnitSide.Player ? px : Math.Min(px, RoomToward(f, Toward(side)))) * Toward(side);
            if (side != UnitSide.Player) f.Acting = true;
            return Own(f, Ch.Pos, ms, Ease.Out, t => { f.X = Lerp(from, to, t); });
        }

        /// <summary>ReturnHome: back to its home (inout), then standing.</summary>
        // js: scenes/battle/field.js:717
        public Promise ReturnHome(UnitSide side, float ms, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            if (f == null) return Promise.Resolved();
            float fromX = f.X;
            float fromY = f.Y;
            return Own(f, Ch.Pos, ms, Ease.InOut, t =>
            {
                f.X = Lerp(fromX, f.HomeX, t);
                f.Y = Lerp(fromY, 0f, t);
            }).Then((Action<bool>)(still =>
            {
                if (still)
                {
                    f.Acting = false;
                    SetPose(side, FigurePose.Idle, f.Unit);
                }
            }));
        }

        /// <summary>
        /// EnemyMotion: the enemy moves into its action, shaped by its system (EnemyMotionShape): 払 swings across, 突 drives
        /// straight in, 打 shoves with its weight, 盾 raises the haft; a step just steps. The stretch is taken on the body's
        /// scale at rest (FigureFacing.During; the DS silhouettes are drawn facing the way they face, as art is). The figure
        /// stays where the move took it until it returns.
        /// </summary>
        // js: scenes/battle/field.js:733
        public Promise EnemyMotion(StrikeSystem system, float ms, int unit = -1)
        {
            Fig f = FigOf(UnitSide.Enemy, unit);
            if (f == null) return _clock.Wait(ms);
            int dir = Toward(UnitSide.Enemy);
            float fromX = f.X;
            float reach = Math.Min(EnemyMotionShape.Reach(system), RoomToward(f, dir));
            float homeScaleX = FigureFacing.HomeScaleX(UnitSide.Enemy, true);
            SetPose(UnitSide.Enemy, FigurePose.Act, f.Unit);
            int bodyMine = ++f.T[(int)Ch.Body];
            // Several enemies: the one acting is drawn in front of the others until it is home again (RenderDepth).
            f.Acting = true;
            return Own(f, Ch.Pos, ms, Ease.Out, t =>
            {
                float bell = (float)Math.Sin(t * Math.PI);
                f.X = fromX + reach * dir * t;
                f.Y = system == StrikeSystem.Shield ? -16f * bell : 0f;
                if (f.T[(int)Ch.Body] != bodyMine) return;
                (float sx, float sy) = FigureFacing.During(homeScaleX, system, bell);
                f.Sx = sx;
                f.Sy = sy;
                if (system == StrikeSystem.Sweep) f.Rot = dir * Lerp(-18f, 14f, t) * bell; // wound back, then across
                else if (system == StrikeSystem.Strike) f.Rot = dir * 10f * bell; // leaning its weight into the push
                else f.Rot = 0f;
            }).Then((Action<bool>)(_ =>
            {
                if (f.T[(int)Ch.Body] == bodyMine)
                {
                    f.Rot = 0f;
                    f.Sx = homeScaleX;
                    f.Sy = 1f;
                }
            }));
        }

        /// <summary>
        /// Recoil: the struck figure leans away from the blow (7°, 12° when strong) and shakes (UiTween.Shake: 10 px, 20 when
        /// strong, 3 round trips) in its Hit pose.
        /// </summary>
        // js: scenes/battle/field.js:763
        public Promise Recoil(UnitSide side, bool strong, float ms, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            if (f == null) return Promise.Resolved();
            float lean = (strong ? 12f : 7f) * (side == UnitSide.Player ? -1f : 1f);
            float amp = strong ? 20f : 10f;
            SetPose(side, FigurePose.Hit, f.Unit);
            Own(f, Ch.Shake, ms, Ease.Linear, t => { f.Shake = (float)Math.Sin(t * Math.PI * 2 * 3) * amp * (1f - t); })
                .Then((Action<bool>)(still => { if (still) f.Shake = 0f; })).Forget();
            return Own(f, Ch.Body, ms, Ease.Out, t => { f.Rot = lean * (float)Math.Sin(t * Math.PI); }).Then((Action<bool>)(still =>
            {
                if (still) f.Rot = 0f;
                if (!f.Falling) SetPose(side, FigurePose.Idle, f.Unit);
            }));
        }

        /// <summary>FigureView.Flash: the body blinks the role's colour ('white' the ink, 'omen' the enemy's violet) and back.</summary>
        // js: scenes/battle/field.js:776
        public Promise Flash(UnitSide side, FxRole role, float ms, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            if (f == null || f.Falling) return Promise.Resolved();
            // flashEl.style.color = tok(shapeToken(role))
            f.FlashColor = CssColor.Parse(FieldArt.Tok(FieldArt.ShapeToken(role)));
            if (f.FlashSvg) f.FlashSvg.Tint = f.FlashColor;
            f.FlashA = 1f;
            return Own(f, Ch.Flash, ms, Ease.Out, t => { f.FlashA = 1f - t; }).Then((Action<bool>)(still => { if (still) f.FlashA = 0f; }));
        }

        /// <summary>Heave (§5.3 息 on a heal): the body rises a little and settles once (y-scale 1 → 1.05 → 1, inout).</summary>
        // js: scenes/battle/field.js:784
        public Promise Heave(UnitSide side, float ms, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            if (f == null) return Promise.Resolved();
            return Own(f, Ch.Heave, ms, Ease.InOut, t => { f.Hv = 1f + 0.05f * (float)Math.Sin(t * Math.PI); })
                .Then((Action<bool>)(still => { if (still) f.Hv = 1f; }));
        }

        /// <summary>
        /// FigureView.Fall (DefeatShape): from the body at rest, the figure tilts about its feet away from the other side, sinks
        /// 40 and darkens to 0.35, fades from 60 %, and is gone (its foot light with it).
        /// </summary>
        // js: scenes/battle/field.js:793
        public Promise Fall(UnitSide side, float ms, int unit = -1, bool keepFoot = false)
        {
            Fig f = FigOf(side, unit);
            if (f == null) return Promise.Resolved();
            RestBody(f);
            f.T[(int)Ch.Shake]++;
            f.Shake = 0f;
            f.Falling = true;
            // keepFoot (battle_ui_ux_v2 §5.15 順 5, an enemy falling while others stand): the foot light stays lit through the
            // fall, and the figure is not put down at its end — FadeFoot then takes the light out and empties the cell.
            f.FootKeep = keepFoot;
            f.FootA = 1f;
            f.Pose = FigurePose.Down;
            float fromY = f.Y;
            int mine = ++f.T[(int)Ch.Fall];
            return _clock.Run(ms, Ease.Linear, t =>
            {
                if (f.T[(int)Ch.Fall] != mine) return;
                f.RootRot = -DefeatShape.Tilt(side, t);
                f.Y = fromY - DefeatShape.Sink(t);
                f.Alpha = DefeatShape.Alpha(t);
                f.Bright = DefeatShape.Brightness(t);
            }).Then(() =>
            {
                if (f.T[(int)Ch.Fall] != mine) return;
                f.Y = fromY;
                if (keepFoot)
                {
                    f.Alpha = 0f;
                    Render();
                    return;
                }
                SetDown(side, true, f.Unit);
            });
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.15 順 5 (scene addition with several enemies; review fix): after a fall made with keepFoot, the foot
        /// light goes out over `ms` (linear) and the cell is empty — the figure is put down. 0 ms: at once.
        /// </summary>
        // js: scenes/battle/field.js:826
        public Promise FadeFoot(UnitSide side, float ms, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            if (f == null) return Promise.Resolved();
            int mine = ++f.T[(int)Ch.Fall];
            f.FootKeep = true;
            f.Alpha = 0f;
            void Done()
            {
                if (f.T[(int)Ch.Fall] == mine) SetDown(side, true, f.Unit);
            }
            if (!(ms > 0f))
            {
                Done();
                return Promise.Resolved();
            }
            return _clock.Run(ms, Ease.Linear, t => { if (f.T[(int)Ch.Fall] == mine) f.FootA = 1f - t; }).Then(() => Done());
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.19 順 1 (review 19; scene addition): a lost battle — every standing figure's foot light shrinks and
        /// goes out over `ms` (in). 0 ms: at once.
        /// </summary>
        // js: scenes/battle/field.js:840
        public Promise DimFeet(float ms)
        {
            var list = new List<Fig>();
            foreach (Fig f in AllFigures())
                if (!f.Down && f.Foot) list.Add(f);
            void Set(float k)
            {
                foreach (Fig f in list)
                {
                    f.FootA = k;
                    f.FootS = k;
                }
                Render();
            }
            if (!(ms > 0f))
            {
                Set(0f);
                return Promise.Resolved();
            }
            return _clock.Run(ms, Ease.In, t => Set(1f - t));
        }

        /// <summary>
        /// battle_ui_ux_v2 §3.14 / §5.7 順 5 (scene addition; Unity and the script have no beat for it): a move that went nowhere
        /// (the edge of the line, or someone in the way) — the figure leans BUMP_PX the way it meant to go over half of `ms` and
        /// comes back over the other half (inout).
        /// </summary>
        // js: scenes/battle/field.js:851
        public Promise Bump(UnitSide side, int dir, float ms, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            if (f == null || f.Down || !(ms > 0f)) return Promise.Resolved();
            int d = dir < 0 ? -1 : 1;
            int mine = ++f.T[(int)Ch.Shake];
            return _clock.Run(ms, Ease.InOut, t => { if (f.T[(int)Ch.Shake] == mine) f.Shake = d * BumpPx * (float)Math.Sin(t * Math.PI); })
                .Then(() => { if (f.T[(int)Ch.Shake] == mine) f.Shake = 0f; });
        }

        /// <summary>ShakeArena: a strong blow shakes the figures' parent (the floor with them) 6 px, 3 round trips.</summary>
        // js: scenes/battle/field.js:860
        public Promise ShakeArena(float ms)
        {
            int mine = ++_arenaToken;
            return _clock.Run(ms, Ease.Linear, t =>
            {
                if (mine == _arenaToken) _arenaShake = (float)Math.Sin(t * Math.PI * 2 * 3) * 6f * (1f - t);
            }).Then(() => { if (mine == _arenaToken) _arenaShake = 0f; });
        }

        /// <summary>
        /// MoveOnFloor (§4.2 / §5.7 順 1): the player walks to its new cell, an enemy that moved (a push, a pull, its own step)
        /// slides to its own, over `ms` (160 a cell, the player's wait); a push starts fast and settles.
        /// </summary>
        // js: scenes/battle/field.js:870
        public Promise MoveOnFloor(Cue cue, int cells, float ms)
        {
            Fig p = _hero;
            (Fig e, int eCell) = MovedEnemyOf(cue);
            float fromX = p.X;
            float toX = StandX(p, cue.PlayerCellAfter, _sizePlayer);
            float eFrom = e != null ? e.X : 0f;
            float eTo = e != null && eCell > 0 ? StandX(e, eCell, e.Size != 0 ? e.Size : _sizeEnemy) : eFrom;
            bool enemyMoves = e != null && (eTo - e.HomeX) * (eTo - e.HomeX) > 0.25f;
            if (enemyMoves) e.HomeX = eTo;
            p.HomeX = toX; // the panels set off for the new cells as the figures do
            EaseFn ease = cue.Pushed ? Ease.Out : Ease.InOut;
            // Walking (review fix): the number panels and the badge ride with the figure until it arrives (geom.Figure's
            // Walking), and an enemy that walks goes behind the others (RenderDepth).
            if (enemyMoves)
            {
                e.Walking = true;
                Own(e, Ch.Pos, ms, ease, t => { e.X = Lerp(eFrom, eTo, t); }).Then((Action<bool>)(still => { if (still) e.Walking = false; })).Forget();
            }
            bool heroMoves = Math.Abs(toX - fromX) > 0.5f;
            if (heroMoves) p.Walking = true;
            return Own(p, Ch.Pos, ms, ease, t => { p.X = Lerp(fromX, toX, t); }).Then(still =>
            {
                if (still) p.Walking = false;
                return still;
            });
        }

        /// <summary>SnapOnFloor: the walk's end state at once, both figures on their cells.</summary>
        // js: scenes/battle/field.js:892
        public void SnapOnFloor(Cue cue)
        {
            Fig p = _hero;
            p.HomeX = StandX(p, cue.PlayerCellAfter, _sizePlayer);
            p.T[(int)Ch.Pos]++;
            p.X = p.HomeX;
            p.Walking = false;
            (Fig e, int eCell) = MovedEnemyOf(cue);
            if (e != null && eCell > 0)
            {
                e.HomeX = StandX(e, eCell, e.Size != 0 ? e.Size : _sizeEnemy);
                SnapHome(UnitSide.Enemy, e.Unit);
            }
            Render();
        }

        // ---- 俊敏's choice (battle_ui_ux_v2 §3.8; field/freestep.js) ----

        /// <summary>The arrows over the open ways (`ways`, the source's) and 留まる on `playerCell`.</summary>
        // js: scenes/battle/field.js:907
        public Promise ShowFreeStep(int playerCell, FreeStepWays ways) => _freeStep.Show(playerCell, ways);

        /// <summary>The amber edge on mark `dir` (−1, 0, +1); returns the focus it took.</summary>
        // js: scenes/battle/field.js:909
        public int? FocusFreeStep(int dir) => _freeStep.SetFocus(dir);

        // js: scenes/battle/field.js:910
        public Promise HideFreeStep(float? ms = null) => ms.HasValue ? _freeStep.Hide(ms.Value) : _freeStep.Hide();

        // js: scenes/battle/field.js:911
        public bool FreeStepShown => _freeStep.Shown;

        // js: scenes/battle/field.js:912
        public int? FreeStepFocus => _freeStep.Focus;

        // js: scenes/battle/field.js:913
        public IReadOnlyList<int> FreeStepDirs() => _freeStep.Dirs();

        // js: scenes/battle/field.js:914
        public StagePoint? FreeStepCentre(int dir) => _freeStep.MarkCentre(dir);

        // js: scenes/battle/field.js:915
        public Promise RecountGap(int gap, int playerCell, int playerSize, int enemyCell, float ms) =>
            _floor.RecountGap(gap, playerCell, playerSize, enemyCell, ms);

        // js: scenes/battle/field.js:916
        public void SetGap(int gap, int playerCell, int playerSize, int enemyCell) => _floor.SetGap(gap, playerCell, playerSize, enemyCell);

        // ---- the api: fx (DepictionFx) ----------------------------------------------------------------------------

        /// <summary>
        /// DepictionFx.Strike across `side`'s chest: the player's blow in ink, the enemy's in its violet. Review 19: the shape
        /// reaches no further than BLOW_CLEAR short of any other standing figure but its striker (BlowRoomOf), and past the
        /// struck body only a little (fx.js BLOW_PAST).
        /// </summary>
        // js: scenes/battle/field.js:926
        public Promise Strike(UnitSide side, StrikeOpts o)
        {
            o = o ?? new StrikeOpts();
            Fig f = FigOf(side, o.Unit);
            Fig striker;
            if (o.ByPlayer) striker = _hero;
            else
            {
                striker = o.Source >= 0 && _foes.TryGetValue(o.Source, out Fig s) ? s : null;
                if (striker == null)
                    foreach (Fig e in _foeList)
                        if (e.Acting) { striker = e; break; }
            }
            return _fx.Strike(f != null ? ChestOf(f) : NoPoint, new FxStrikeArgs
            {
                ColorName = o.ByPlayer ? "ink" : "violet",
                Tier = o.Intensity,
                TowardLeft = !o.ByPlayer,
                System = o.System,
                Ms = o.Ms,
                Room = f != null ? BlowRoomOf(f, striker) : (BlowRoom?)null,
                Half = f != null ? f.Width / 2f : 50f,
            });
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.7 順 7 (review 19; scene addition — the writer marks the wall by its event, Unity draws none): `side`
        /// (enemy `unit`) was pushed toward `dir` (−1 left, +1 right) into what stopped it. The edge it hit lights for `ms` — the
        /// floor's end when it stands on the last cell that way, else the figure in its way flashes white — and the figure takes
        /// 打's ripple from that side.
        /// </summary>
        // js: scenes/battle/field.js:940
        public Promise WallHit(UnitSide side, int dir, float ms, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            if (f == null || f.Down) return Promise.Resolved();
            int d = dir < 0 ? -1 : 1;
            FloorFrame fl = _lastFrame?.Floor;
            Fig other = null;
            foreach (Fig o in AllFigures())
            {
                if (o == f || o.Down || o.Falling || (o.X - f.X) * d <= 0f) continue;
                if (other == null || Math.Abs(o.X - f.X) < Math.Abs(other.X - f.X)) other = o;
            }
            int cell;
            if (side == UnitSide.Player) cell = fl != null ? fl.PlayerCell : 0;
            else
            {
                UnitFrame mine = null;
                if (_lastFrame != null && _lastFrame.Enemies != null)
                    foreach (UnitFrame e in _lastFrame.Enemies)
                        if (e != null && e.Unit == f.Unit) { mine = e; break; }
                cell = mine != null && mine.Cell != 0 ? mine.Cell : fl != null ? fl.EnemyCell : 0;
            }
            int size = Math.Max(1, f.Size != 0 ? f.Size : 1);
            bool atEnd = fl == null || !(cell > 0) ? other == null : d < 0 ? cell <= 1 : cell + size - 1 >= fl.Cells;
            Promise lit = atEnd || other == null
                ? _floor.EdgeFlash(d, ms)
                : Flash(other.Side, FxRole.White, ms, other.Side == UnitSide.Player ? -1 : other.Unit);
            Promise ripple = _fx.Strike(ChestOf(f), new FxStrikeArgs
            {
                ColorName = "ink", Tier = 2, TowardLeft = d > 0, System = StrikeSystem.Strike, Ms = Math.Max(ms, 200f),
            });
            // Promise.all([lit, ripple]).then(() => {}): the same microtask hop as the JS.
            return Promise.All(lit, ripple).Then(() => { });
        }

        /// <summary>DepictionFx.TraitAccent at the point of the blow on `side`; warm when the trait is the player's.</summary>
        // js: scenes/battle/field.js:960
        public Promise TraitAccent(UnitSide side, float ms, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            return _fx.TraitAccent(f != null ? ChestOf(f) : NoPoint, side == UnitSide.Enemy, ms, side == UnitSide.Player);
        }

        /// <summary>DepictionFx.Mark: 盾 ring, 型 crest at the feet (+ body light when crestLight), 気 mark; 息 and blows draw nothing.</summary>
        // js: scenes/battle/field.js:965
        public Promise Mark(UnitSide side, MarkOpts o)
        {
            o = o ?? new MarkOpts();
            Fig f = FigOf(side, o.Unit);
            if (f == null) return Promise.Resolved();
            // Review 17 (an elite's 構え: its action step took it 70 px toward the hero, and the crest stamped there stayed off
            // its feet once it stepped back): the crest and the ring ride with the figure for as long as they show.
            return _fx.Mark(new FxMarkArgs
            {
                Chest = ChestOf(f),
                Head = HeadOf(f),
                Feet = FeetOf(f),
                Facing = Toward(side),
                Fx = o.Fx,
                Tier = o.Intensity,
                Ms = o.Ms,
                CrestLight = o.CrestLight,
                Warm = side == UnitSide.Player,
                TrackX = () => f.Down ? (float?)null : f.X + _shiftX,
            });
        }

        // js: scenes/battle/field.js:976
        public void Burst(StagePoint at, FxRole role, float size)
        {
            _fx.Burst(at, FieldArt.ShapeToken(role), size).Forget();
        }

        // js: scenes/battle/field.js:977
        public Promise FloatText(StagePoint at, string text, FxRole role, float fontSize, FloatOpts o = null) =>
            _fx.FloatText(at, text, role, fontSize, o);

        /// <summary>
        /// DepictionFx.ShieldBlock, stood 40 px toward the blow from the chest (review fix: square on the chest it hid the whole
        /// figure and its number sat under the HP float; the figure's back now shows beside it).
        /// </summary>
        // js: scenes/battle/field.js:982
        public Promise ShieldBlock(UnitSide side, string amountText, float ms, int unit = -1, bool broken = false)
        {
            Fig f = FigOf(side, unit);
            return _fx.ShieldBlock(f != null ? ShieldAt(side, unit) : NoPoint, amountText, ms, broken, side == UnitSide.Enemy);
        }

        /// <summary>Where the shield of a blocked blow stands (the Guard float rises from it).</summary>
        // js: scenes/battle/field.js:987
        public StagePoint ShieldAt(UnitSide side, int unit = -1)
        {
            Fig f = FigOf(side, unit);
            if (f == null) return NoPoint;
            StagePoint c = ChestOf(f);
            return new StagePoint(c.X + Toward(side) * ShieldToward, c.Y);
        }

        // js: scenes/battle/field.js:993
        public Promise Ghost(StagePoint from, StagePoint to, GhostOpts o) => _fx.Ghost(from, to, o);

        // js: scenes/battle/field.js:995
        public void Destroy()
        {
            if (!_alive) return;
            _alive = false;
            _offFrame?.Invoke();
            _fx.Clear();
            _freeStep.Destroy();
            _floor.Destroy();
            _backdrop.Destroy();
            if (_hero.El) Ui.Remove(_hero.El);
            foreach (Fig f in _foeList)
                if (f.El) Ui.Remove(f.El);
            _foes.Clear();
            _foeList.Clear();
            if (_shade) Ui.Remove(_shade);
            if (_vignette) Ui.Remove(_vignette);
            // layer.field.style.transform = ''
            if (_fieldLayer) _fieldLayer.TranslateX = 0f;
        }

        // ---- geometry (docs/BATTLE_SCENE.md §2.4) -------------------------------------------------------------------

        // js: scenes/battle/field.js:1014-1058
        private void RegisterGeometry()
        {
            BattleGeom geom = _env.Geom;
            // js: scenes/battle/field.js:1014
            geom.Figure = (side, unit) =>
            {
                Fig f = FigOf(side, unit);
                if (f == null) return null;
                return new FigureGeom
                {
                    X = f.X + _shiftX,
                    FeetY = L.FeetY + f.Y,
                    Height = f.Height,
                    Width = f.Width,
                    HomeX = f.HomeX + _shiftX,
                    Top = L.FeetY + f.Y - f.Height,
                    // The art's own box across (the silhouette's svg, wider than the body for a tail or a spear; review 21: the
                    // stance lists keep clear of it).
                    ArtX0 = f.X + _shiftX - f.Cx,
                    ArtX1 = f.X + _shiftX - f.Cx + f.SvgW,
                    Chest = Point(f, L.Chest),
                    Head = Point(f, L.Head),
                    Unit = f.Unit,
                    Down = f.Down,
                    // It is walking on the floor (MoveOnFloor): what follows it rides with X, not HomeX (already the new cell).
                    Walking = f.Walking,
                };
            };
            // js: scenes/battle/field.js:1034
            geom.Receiver = unit =>
            {
                Fig e = FigOf(UnitSide.Enemy, unit);
                return e != null ? ReceiverOf(e) : (ScreenBox?)null;
            };
            // Scene addition (several enemies): one receiver per standing enemy, { unit, box }, left to right. Two neighbours'
            // squares would overlap, so each is cut at the middle between their homes.
            // js: scenes/battle/field.js:1042
            geom.Receivers = () =>
            {
                var list = new List<Fig>();
                foreach (Fig f in _foeList)
                    if (!f.Down) list.Add(f);
                // a stable sort by homeX
                for (int i = 1; i < list.Count; i++)
                {
                    Fig cur = list[i];
                    int j = i - 1;
                    while (j >= 0 && list[j].HomeX > cur.HomeX)
                    {
                        list[j + 1] = list[j];
                        j--;
                    }
                    list[j + 1] = cur;
                }
                var o = new List<ReceiverBox>(list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    Fig f = list[i];
                    ScreenBox box = ReceiverOf(f);
                    float left = box.X;
                    float right = box.X + box.Width;
                    if (i > 0) left = Math.Max(left, (list[i - 1].HomeX + f.HomeX) / 2f + _shiftX);
                    if (i < list.Count - 1) right = Math.Min(right, (f.HomeX + list[i + 1].HomeX) / 2f + _shiftX);
                    o.Add(new ReceiverBox(f.Unit, new ScreenBox(left, box.Y, Math.Max(1f, right - left), box.Height)));
                }
                return o;
            };
            // 俊敏's choice marks as they stand (stage px), [] while none shows (the card detail keeps off them, §3.13).
            // js: scenes/battle/field.js:1054
            geom.FreeStepBoxes = () => _freeStep.Boxes();
            // js: scenes/battle/field.js:1055
            geom.CellBox = cell =>
            {
                StageBox? b = _floor.CellBox(cell);
                if (b.HasValue && _shiftX != 0f) return new StageBox(b.Value.X + _shiftX, b.Value.Y, b.Value.W, b.Value.H);
                return b;
            };
        }

        /// <summary>ReceiverView (#568): the invisible 340×340 square over an enemy's home, its middle at the body's middle.</summary>
        // js: scenes/battle/field.js:1027
        private ScreenBox ReceiverOf(Fig e)
        {
            float w = L.Receiver.W;
            float ht = L.Receiver.H;
            float x = Math.Max(Edge + w / 2f, Math.Min(L.W - Edge - w / 2f, e.HomeX + _shiftX));
            float cy = L.FeetY - e.Height / 2f;
            return new ScreenBox(x - w / 2f, cy - ht / 2f, w, ht);
        }

        // ---- helpers ----------------------------------------------------------------------------------------------

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>The sRGB channel → linear (relative luminance, field.js:388).</summary>
        // js: scenes/battle/field.js:388
        private static double Lin(float c) => c <= 0.04045f ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        /// <summary>toFixed(1) / (2) / (3) as numbers: the steps the JS writes its styles in.</summary>
        private static float R1(float v) => (float)Math.Round(v, 1, MidpointRounding.AwayFromZero);
        private static float R2(float v) => (float)Math.Round(v, 2, MidpointRounding.AwayFromZero);
        private static float R3(float v) => (float)Math.Round(v, 3, MidpointRounding.AwayFromZero);

        /// <summary>String(n) for a JS number.</summary>
        private static string JsNum(float v) => ((double)v + 0d).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }
}
