// The hold decorations of the battle's hand (TargetMarkView, ThrowLineView, the prediction tag; docs/BATTLE_SCENE.md
// §6a): the target hooks around each framed figure (layer fx) with their light, the prediction tag on each reached
// frame's top edge (layer float, with 重撃's recovery-cut mark), and the throw line (layer marks). Port of
// scenes/battle/hand-marks.js.
//
// The hold decorations of the hand module: ports of TargetMarkView.cs (the four corner hooks around the figure a held
// card lands on, with the one predicted value on the top edge, battle-visual-v1 §5.2 / §5.3) and ThrowLineView.cs (the
// amber dashed line at y 790 a self card is released above), plus the zone fade of DepictionPlayer.BeginHold
// (EffectId.ReceiverShow / ThrowLineShow). ReceiverView draws nothing since #242: the receiver is only the box
// ReceiverDrop tests (the field's geom.Receiver(s)).
//
// Which figure is framed and the value come from the script (CardFace.Aim / Affects / ReachHint and the source's
// PreviewFor); this file only places and lights them. Several enemies (scene addition, battle-visual-v1 §5.2): a
// single-target card frames every standing enemy, amber where CardFace.ReachFor(k) and steel where not; each frame the
// card reaches carries its own predicted value on its top edge (battle_ui_ux_v2 §2.2.5; review fix: the tag stood on the
// framed enemy only). The framed one (SetTarget) is lit full, the others at the idle 75 %. Looks: Ocb.TargetFrame,
// Ocb.PredTag, Ocb.ThrowLine. The hooks are drawn in layer fx with the figures' effects, under the omen badge and the
// panels (Unity: the mark is the figure's); the tag in layer float (layout.md 重なりの順), on the frame's top edge — the
// badge over it rises clear of it (review 19, DS tag.lift).
//
// TargetMarkView's light (review fix): a frame the card reaches carries ProceduralArt.Glow in amber at 35 %, stretched
// over the frame plus 10 px and drawn over the figure under the hooks (the mark is a child of the figure, its light the
// first sibling), so the framed enemy stands in warm light. With several enemies only the frame the card would go to now
// (SetTarget) is lit; the other reached frames keep their amber hooks.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Depiction;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    /// <summary>boxOf's answer: { x, y, w, h, cx } — the hooks' box around a figure now and the figure's middle x.</summary>
    // js: scenes/battle/hand-marks.js:95
    public readonly struct HookBox
    {
        public readonly float X;
        public readonly float Y;
        public readonly float W;
        public readonly float H;
        /// <summary>cx: the figure's x (the tag's middle).</summary>
        public readonly float Cx;

        public HookBox(float x, float y, float w, float h, float cx)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
            Cx = cx;
        }

        public StageBox Box => new StageBox(X, Y, W, H);
    }

    /// <summary>createMarks(env) → the marks' api (hand-marks.js:196).</summary>
    // js: scenes/battle/hand-marks.js:61
    public sealed class HandMarks
    {
        // ---- constants (hand-marks.js:26-59) ----

        /// <summary>OUT — TargetMarkView §5.2: the frame 46 px out on the left and right, 22 above, 12 below.</summary>
        // js: scenes/battle/hand-marks.js:27
        private const float OutX = 46f;
        private const float OutTop = 22f;
        private const float OutBottom = 12f;

        /// <summary>
        /// FRAME_APART / OUT_MIN — review 21 (two or three enemies on 7–8 cells: the 46 px sides of neighbouring frames
        /// overlapped and their hooks met as one 「TT」): a frame's side toward a standing neighbour comes in to keep this far
        /// from the neighbour's frame — each takes half of the room between the two bodies, less half this — and never closer
        /// to its body than OUT_MIN.
        /// </summary>
        // js: scenes/battle/hand-marks.js:33
        private const float FrameApart = 12f;
        // js: scenes/battle/hand-marks.js:34
        private const float OutMin = 2f;

        /// <summary>HEAVY_WORD — CoreText.EffectWord of TraitEffect.HeavyBlow, as the lamp line prints it.</summary>
        // js: scenes/battle/hand-marks.js:36
        private static readonly Regex HeavyWord = new Regex(@"(^|[\s／→])重撃(\s|$)", RegexOptions.CultureInvariant);

        /// <summary>HOOK / HOOK_W: hooks 26 long, 3 thick (steel 2).</summary>
        // js: scenes/battle/hand-marks.js:37
        private const float Hook = 26f;
        // js: scenes/battle/hand-marks.js:38
        private const float HookW = 3f;

        /// <summary>GLOW / GLOW_ALPHA — TargetMarkView: the amber light 10 px past the frame at 35%.</summary>
        // js: scenes/battle/hand-marks.js:40
        private const float Glow = 10f;
        // js: scenes/battle/hand-marks.js:41
        private const float GlowAlpha = 0.35f;

        /// <summary>IDLE — TargetMarkView.SetHot: 1 when releasing now would play it, 0.75 otherwise (--opacity-idle-hooks).</summary>
        // js: scenes/battle/hand-marks.js:43
        private const float Idle = 0.75f;

        /// <summary>LINE_IDLE — ThrowLineView.SetHot: the dashes at full amber over the line, 60% under it.</summary>
        // js: scenes/battle/hand-marks.js:45
        private const float LineIdle = 0.6f;

        /// <summary>
        /// FOCUS_IDLE / OTHER_HOOKS — several enemies (review fix): the focused frame stands full (its steel hooks brighter and
        /// 3 px, `.is-focus`), the others step back — their hooks at OTHER_HOOKS, their prediction tags greyed — so the eye goes
        /// to the one Enter or a release would play on.
        /// </summary>
        // js: scenes/battle/hand-marks.js:52
        private const float FocusIdle = 0.9f;
        // js: scenes/battle/hand-marks.js:53
        private const float OtherHooks = 0.6f;

        /// <summary>hand.css: .ocbg-battle-pred.is-other { filter: grayscale(0.85) brightness(0.62); }</summary>
        private static readonly ColorFilter OtherTagFilter = ColorFilter.Parse("grayscale(0.85) brightness(0.62)");

        /// <summary>One frame: { side, unit, reaches, w, h, el, light, tag }.</summary>
        private sealed class Frame
        {
            public UnitSide Side;
            public int Unit = -1;
            public bool Reaches;
            public float W;
            public float H;
            public OcbTargetFrame El;
            public GradientGraphic Light;
            public OcbElement Tag;
            /// <summary>The .is-focus class as it stands on El.</summary>
            public bool Focus;
            /// <summary>The .is-other class as it stands on Tag.</summary>
            public bool Other;
        }

        private readonly BattleViewEnv _env;
        private readonly List<Frame> _frames = new List<Frame>();
        // current: { face, preview(unit), target } — only its target is read again (SetTarget, Place).
        private bool _hasCurrent;
        private int _target;
        private HatchGraphic _line;
        private bool _shown;
        private bool _hot;
        /// <summary>fade.a: the zone fade (ReceiverShow / ThrowLineShow).</summary>
        private float _fadeA;
        private int _token;

        private HandMarks(BattleViewEnv env)
        {
            _env = env;
        }

        /// <summary>createMarks(env).</summary>
        // js: scenes/battle/hand-marks.js:61
        public static HandMarks Create(BattleViewEnv env) => new HandMarks(env);

        /// <summary>shown: a zone is up (a card held or selected).</summary>
        // js: scenes/battle/hand-marks.js:197
        public bool Shown => _shown;

        /// <summary>hot: releasing the card now would play it.</summary>
        // js: scenes/battle/hand-marks.js:198
        public bool Hot => _hot;

        // ---- the frame's box -----------------------------------------------------------------------------

        /// <summary>
        /// boxOf(side, unit): the hooks' box around `side`'s figure now (null when it is down or unknown), each side trimmed
        /// toward a standing neighbour enemy so 12 px stay between two frames (FRAME_APART, OUT_MIN). The click path tests a
        /// click against it. The hooks are a child of the figure in Unity, so they follow it.
        /// </summary>
        // js: scenes/battle/hand-marks.js:72
        public HookBox? BoxOf(UnitSide side, int unit = -1)
        {
            BattleGeom geom = _env.Geom;
            FigureGeom f = geom.Figure != null ? geom.Figure(side, unit) : null;
            if (f == null || f.Down) return null;
            float left = OutX;
            float right = OutX;
            if (side == UnitSide.Enemy && geom.Receivers != null)
            {
                // The standing neighbours' bodies (review 21): each side gives way to keep FRAME_APART from the next frame.
                IReadOnlyList<ReceiverBox> list = geom.Receivers();
                if (list != null)
                {
                    foreach (ReceiverBox r in list)
                    {
                        if (r.Unit == unit || r.Unit < 0) continue;
                        FigureGeom o = geom.Figure(UnitSide.Enemy, r.Unit);
                        if (o == null || o.Down) continue;
                        if (o.X > f.X)
                        {
                            float room = (o.X - o.Width / 2f) - (f.X + f.Width / 2f);
                            right = Math.Min(right, Math.Max(OutMin, room / 2f - FrameApart / 2f));
                        }
                        else if (o.X < f.X)
                        {
                            float room = (f.X - f.Width / 2f) - (o.X + o.Width / 2f);
                            left = Math.Min(left, Math.Max(OutMin, room / 2f - FrameApart / 2f));
                        }
                    }
                }
            }
            float x = f.X - f.Width / 2f - left;
            float w = f.Width + left + right;
            float top = f.Top - OutTop;
            return new HookBox(x, top, w, f.FeetY + OutBottom - top, f.X);
        }

        // js: scenes/battle/hand-marks.js:98
        private Frame MakeFrame(UnitSide side, bool reaches, string preview, CardFace face, int unit)
        {
            HookBox? box = BoxOf(side, unit);
            if (box == null) return null;
            var fr = new Frame { Side = side, Unit = unit, Reaches = reaches };
            Build(fr, box.Value);
            SetTag(fr, reaches ? preview : "", face);
            return fr;
        }

        /// <summary>§5.2 予測値: one tag on the middle of the top edge, only when the card reaches and the source gave one.</summary>
        // js: scenes/battle/hand-marks.js:107
        private void SetTag(Frame fr, string preview, CardFace face)
        {
            if (fr.Tag != null) { fr.Tag.Remove(); fr.Tag = null; }
            fr.Other = false;
            if (string.IsNullOrEmpty(preview)) return;
            CardValueKind kind = HandCard.PreviewKind(face);
            // OCB.PredTag(valueIcon(kind), preview, valueColor(kind)), class ocbg-battle-pred, appended to layer.float.
            fr.Tag = Ocb.PredTag(_env.Layer.Float, HandCard.ValueIcon(kind), preview, CssColor.Parse(HandCard.ValueColor(kind)));
            fr.Tag.Root.name = "ocb-pred ocbg-battle-pred";
            // hand.css: .ocbg-battle-pred { pointer-events: none; z-index: 1; }
            fr.Tag.Node.PointerEvents = PointerEvents.None;
            fr.Tag.Node.Z = 1;
            // battle_ui_ux_v2 §3.1 / §3.6 (c) (review 21): a 重撃 card whose lamp is lit on this enemy carries, beside the value
            // (which holds the +6), 「次の回復 −1」 as its mark — one next-recovery pip, struck through. No words (§3.2).
            if (HeavyLitOn(face, fr.Unit))
            {
                RecoveryCutMark(fr.Tag.Root);
                Ocb.Flow(fr.Tag.Root, Ocb.PredCss);
            }
        }

        /// <summary>The face's trait is 重撃 (CoreText.TraitLine's effect word on the lamp line) and its lamp is lit on enemy `unit`.</summary>
        // js: scenes/battle/hand-marks.js:120
        private static bool HeavyLitOn(CardFace face, int unit)
        {
            if (face == null || face.Plain || !HeavyWord.IsMatch((face.TraitText ?? "") + " " + (face.TraitBottom ?? ""))) return false;
            if (face.Aim == CardAim.Single) return face.LitFor(unit >= 0 ? unit : face.TargetUnit >= 0 ? face.TargetUnit : 0);
            return face.TraitLit;
        }

        /// <summary>
        /// One next-recovery pip (StaminaTagView's dashed amber dot, 20 px) with a slash across it: an svg 24×24 (viewBox 0 0
        /// 24 24) of a circle cx 12 cy 12 r 8, no fill, stroke --amber 2, dasharray 3 3, and the path M4 20L20 4, stroke
        /// --ink 2.4, round caps. Its &lt;title&gt; 「次の回復 −1」 (scene text, §3.1) is the element's name (the tag takes no pointer).
        /// </summary>
        // js: scenes/battle/hand-marks.js:126
        private static PathGraphic RecoveryCutMark(RectTransform parent)
        {
            PathGraphic svg = PathGraphic.Make(parent, 0f, 0f, 24f, 24f, "ocbg-battle-pred-cut 次の回復 −1"); // scene text (§3.1)
            svg.ViewBox = new RectF(0f, 0f, 24f, 24f);
            // <circle cx="12" cy="12" r="8">: the path the SVG circle is (from (cx + r, cy), the positive angle way round),
            // so the dashes start where the browser starts them.
            svg.AddStroke("M20 12A8 8 0 0 1 12 20A8 8 0 0 1 4 12A8 8 0 0 1 12 4A8 8 0 0 1 20 12Z", DsTokens.Amber, 2f, Cap.Butt, Join.Miter, new[] { 3f, 3f });
            svg.AddStroke("M4 20L20 4", DsTokens.Ink, 2.4f, Cap.Round);
            Ocb.NoShrink(svg);
            return svg;
        }

        // js: scenes/battle/hand-marks.js:144
        private void Build(Frame fr, HookBox box)
        {
            if (fr.El != null) fr.El.Remove();
            fr.W = JsRound(box.W);
            fr.H = JsRound(box.H);
            fr.El = Ocb.TargetFrame(_env.Layer.Fx, fr.W, fr.H, new OcbFrameOpts
            {
                Reachable = fr.Reaches,
                Hook = Hook,
                Width = HookW,
                Glow = Glow,
                GlowAlpha = GlowAlpha,
            });
            // .ocbg-battle-hooks (left 0, top 0; placed by its transform), .is-steel when it does not reach.
            fr.El.Root.name = fr.Reaches ? "ocb-frame ocbg-battle-hooks" : "ocb-frame ocbg-battle-hooks is-steel";
            fr.Focus = false;
            // TargetMarkView.BuildParts / Show(reaches): the light, only on a frame the card reaches (steel: none).
            fr.Light = null;
            if (fr.Reaches)
            {
                // .ocbg-battle-hooks-light: left / top −GLOW, (w + 2·GLOW) × (h + 2·GLOW), glowGradient(tok('amber', a), GLOW_ALPHA),
                // the frame's first child (under the hooks).
                fr.Light = GradientGraphic.Glow(fr.El.Root, -Glow, -Glow, fr.W + 2f * Glow, fr.H + 2f * Glow, DsTokens.Amber, GlowAlpha, "ocbg-battle-hooks-light");
                fr.Light.transform.SetAsFirstSibling();
            }
            // Review fix: the hooks belong to the figure (TargetMarkView is the figure's child in Unity), so they are drawn
            // with the figure's effects (layer fx, over the body), under the panels' layer: the omen badge stands over them,
            // as in Unity's 02-grab-attack-in-frame. Only the prediction tag stands in front (layer float, DS rule).
        }

        /// <summary>The tag's middle y on frame `fr` (box = its box now): the frame's top edge (the badge over it rises for it).</summary>
        // js: scenes/battle/hand-marks.js:168
        private static float TagY(Frame fr, HookBox box) => box.Y;

        /// <summary>
        /// hand.css: .ocbg-battle-hooks.is-steel.is-focus svg path:last-child { stroke: var(--ink-muted); stroke-width: 3px; }
        /// and path:first-child { stroke-width: 7px; } (the keyline, invisible in dusk) — several enemies: the focused frame of
        /// a card that cannot reach it reads brighter and thicker.
        /// </summary>
        private static void SetFocus(Frame fr, bool on)
        {
            if (fr.Focus == on) return;
            fr.Focus = on;
            if (fr.Reaches || fr.El == null || fr.El.Stroke == null || !fr.El.Svg) return;
            fr.El.Stroke.WithStroke(on ? DsTokens.InkMuted : DsTokens.Steel, on ? 3f : 2f, Cap.Square);
            if (fr.El.Keyline != null) fr.El.Keyline.StrokeStyle = new StrokeStyle(on ? 7f : 2f + 4f, Cap.Square);
            fr.El.Svg.LayersChanged();
        }

        // js: scenes/battle/hand-marks.js:172
        private void Place()
        {
            float a = _fadeA;
            bool several = _frames.Count >= 2;
            foreach (Frame fr in _frames)
            {
                HookBox? b = BoxOf(fr.Side, fr.Unit);
                if (b == null)
                {
                    if (fr.El != null) fr.El.Node.Opacity = 0f;
                    if (fr.Tag != null) fr.Tag.Node.Opacity = 0f;
                    continue;
                }
                HookBox box = b.Value;
                if (JsRound(box.W) != fr.W || JsRound(box.H) != fr.H) Build(fr, box);
                // transform: translate(x px, y px) (toFixed(1))
                fr.El.Node.SetTransform(Fixed1(box.X), Fixed1(box.Y), 1f, 1f, 0f);
                bool framed = !several || (_hasCurrent && fr.Unit == _target);
                // One frame: TargetMarkView.SetHot (1 / 0.75). Several: the focus stands out (FOCUS_IDLE … OTHER_TAG).
                float op = !several ? a * (_hot ? 1f : Idle) : a * (framed ? (_hot ? 1f : FocusIdle) : OtherHooks);
                fr.El.Node.Opacity = op;
                SetFocus(fr, several && framed);
                if (fr.Light) fr.Light.gameObject.SetActive(framed);
                if (fr.Tag != null)
                {
                    fr.Tag.Node.Place(Fixed1(box.Cx), Fixed1(TagY(fr, box)));
                    fr.Tag.Node.Opacity = several && !framed ? a : op;
                    bool other = several && !framed;
                    if (other != fr.Other)
                    {
                        fr.Other = other;
                        fr.Tag.Node.Filter = other ? OtherTagFilter : ColorFilter.None;
                    }
                }
            }
            if (_line) _line.Opacity = a * (_hot ? 1f : LineIdle);
        }

        // ---- api ---------------------------------------------------------------------------------------------

        /// <summary>
        /// show(face, preview, target) = BeginHold's zone + ShowTargetMark: a card aimed at one enemy frames every standing
        /// enemy (ReachFor; steel and no number where it does not reach), each reached frame with its own tag
        /// (`preview(unit)`); a self card shows the throw line and frames the figure CardFace.Affects names. `target`: the
        /// enemy framed in focus (−1: 0, the JS `target != null ? target : 0`). The zone fades in over ReceiverShow /
        /// ThrowLineShow (out).
        /// </summary>
        // js: scenes/battle/hand-marks.js:204
        public void Show(CardFace face, Func<int, string> preview, int target)
        {
            Hide();
            if (face == null) return;
            _shown = true;
            _hot = false;
            Func<int, string> previewOf = preview ?? (_ => "");
            _hasCurrent = true;
            _target = target >= 0 ? target : 0;
            bool single = face.Aim == CardAim.Single;
            if (single)
            {
                var units = new List<int>();
                if (_env.Geom.Receivers != null)
                {
                    IReadOnlyList<ReceiverBox> list = _env.Geom.Receivers();
                    if (list != null) foreach (ReceiverBox r in list) units.Add(r.Unit);
                }
                if (units.Count > 1)
                {
                    // Several enemies: every standing one is framed (ReachFor), each one reached with its own tag.
                    foreach (int u in units)
                    {
                        bool reaches = face.ReachOn != null && face.ReachOn.Count > 0 ? face.ReachFor(u) : string.IsNullOrEmpty(face.ReachHint);
                        Frame fr = MakeFrame(UnitSide.Enemy, reaches, previewOf(u) ?? "", face, u);
                        if (fr != null) _frames.Add(fr);
                    }
                }
                else
                {
                    int u = units.Count > 0 ? units[0] : -1;
                    Frame fr = MakeFrame(UnitSide.Enemy, string.IsNullOrEmpty(face.ReachHint), previewOf(u) ?? "", face, u);
                    if (fr != null) _frames.Add(fr);
                }
            }
            else
            {
                // h('div', { class: 'ocb-throwline ocbg-battle-throw' }): left THROW.x0, top THROW.y − 1.5, width x1 − x0, 3 tall,
                // in layer.marks (.ocbg-battle-throw's keyline shadow is transparent in dusk; it takes no pointer).
                _line = Ocb.ThrowLine(_env.Layer.Marks, L.Throw.X0, L.Throw.Y - 1.5f, L.Throw.X1 - L.Throw.X0);
                _line.gameObject.name = "ocb-throwline ocbg-battle-throw";
                if (face.Affects == null)
                {
                    _env.Log.Error("[Depiction] card " + face.Id + " is a throw-line card but the script gave no Affects");
                }
                else
                {
                    Frame fr = MakeFrame(face.Affects == UnitSide.Player ? UnitSide.Player : UnitSide.Enemy, true, previewOf(-1) ?? "", face, -1);
                    if (fr != null) _frames.Add(fr);
                }
            }
            float ms = _env.Fx.Ms(single ? EffectId.ReceiverShow : EffectId.ThrowLineShow);
            int my = ++_token;
            _fadeA = 0f;
            if (ms > 0f) _env.Clock.Run(ms, Ease.Out, t => { if (my == _token) _fadeA = t; }).Forget();
            else _fadeA = 1f;
            Place();
        }

        /// <summary>setTarget(unit) (several enemies): the card is framed over enemy `unit` now (its frame and tag lit full).</summary>
        // js: scenes/battle/hand-marks.js:246
        public void SetTarget(int unit)
        {
            if (!_hasCurrent || _target == unit) return;
            _target = unit;
            if (_frames.Count < 2) return;
            Place();
        }

        /// <summary>
        /// taggedUnits(): the enemies whose frame carries a prediction tag now (their omen badges stand higher for it) — units,
        /// or −1 for the one enemy of a battle that names none. A frame on the player is not listed.
        /// </summary>
        // js: scenes/battle/hand-marks.js:256
        public IReadOnlyList<int> TaggedUnits()
        {
            var o = new List<int>();
            foreach (Frame fr in _frames)
                if (fr.Tag != null && fr.Side == UnitSide.Enemy) o.Add(fr.Unit >= 0 ? fr.Unit : -1);
            return o;
        }

        /// <summary>setHot(on) = SetZoneHot.</summary>
        // js: scenes/battle/hand-marks.js:260
        public void SetHot(bool on)
        {
            _hot = on;
            Place();
        }

        /// <summary>hide() = Release / LetGo: the zone and the hooks go at once.</summary>
        // js: scenes/battle/hand-marks.js:265
        public void Hide()
        {
            _token++;
            _shown = false;
            _hasCurrent = false;
            _hot = false;
            foreach (Frame fr in _frames)
            {
                if (fr.El != null) fr.El.Remove();
                if (fr.Tag != null) fr.Tag.Remove();
            }
            _frames.Clear();
            if (_line) { Ui.Remove(_line); _line = null; }
        }

        /// <summary>update() — per frame: follow the figures, and the fade.</summary>
        // js: scenes/battle/hand-marks.js:277
        public void Update()
        {
            if (_shown) Place();
        }

        /// <summary>destroy().</summary>
        // js: scenes/battle/hand-marks.js:278
        public void Destroy()
        {
            Hide();
        }

        // ---- helpers -------------------------------------------------------------------------------------

        /// <summary>Math.round: half up (toward +∞), not the C# banker's rounding.</summary>
        private static float JsRound(float v) => (float)Math.Floor(v + 0.5);

        /// <summary>Number(v.toFixed(1)).</summary>
        private static float Fixed1(float v) => (float)(Math.Round(v * 10.0, MidpointRounding.AwayFromZero) / 10.0);
    }
}
