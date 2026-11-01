// One unit's number panel: a port of StatusBarView (battle-visual-v1 §4.5, §7.3, #242 / #455) drawn with the design system's
// panel, bar and chip looks (docs/BATTLE_SCENE.md §6c, battle-views §3.1). The HP row (heart, number, bar with the 10 HP marks,
// the grey band that follows the bar down, the wick stripes of a held card's HP loss), the Guard row (shield and number; an
// enemy's stamina dot, number and 構えの盾 on the right) and the status chips under them, which wrap, fold into "+n" and open
// their 説明パネル on hover (a right click pins it). It shows the values it is handed; the bar's width is the only thing derived
// here (a drawing scale, not a rule). In layer.panels. Port of scenes/battle/hud/status.js (+ hud.css:5-54).
//
// The browser lays the absolutely placed parts out from their CSS numbers and lets percentages follow the bar; here the same
// numbers are written into UiNode boxes, the bar's parts are sized in px from the same percentages each time the bar or the
// values change, and the flex rows (the Guard badge, the enemy's stamina group, the chips, the chip's 説明パネル head) run
// through the kit's flex solver (Ocb.Flow) with the CSS rules below, copied verbatim from hud.css and bundle.css.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Depiction;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;
using UnityEngine.EventSystems;
using SPL = Depiction.StatusPanelLayout;

namespace Trial.Battle
{
    /// <summary>createStatusPanel's deps: { tip, onStamina(stamina, max), onPip(index, ms), onStaminaPop(ms) } (the player's go to the stamina tag).</summary>
    // js: scenes/battle/hud/status.js:71
    public sealed class StatusPanelDeps
    {
        public HudTip Tip;
        /// <summary>onStamina(stamina, max): the player's stamina is handed on to the stamina tag (StaminaShown).</summary>
        public Action<int, int> OnStamina;
        /// <summary>onPip(index, ms): one recovered point lights (the stamina tag's lightPip).</summary>
        public Action<int, float> OnPip;
        /// <summary>onStaminaPop(ms): a spend pops (the stamina tag's number).</summary>
        public Func<float, Promise> OnStaminaPop;
    }

    /// <summary>createStatusPanel(env, side, deps) → the panel's api (status.js:349). Implements hud.status(side, unit)'s contract.</summary>
    // js: scenes/battle/hud/status.js:73
    public sealed class HudStatusPanel : IStatusPanel
    {
        // ---- StatusBarView's own numbers (the prefab's, laid out again at run time) ----------------------------------------

        // js: scenes/battle/hud/status.js:15
        private const int HpPerTick = 10;
        /// <summary>
        /// battle-visual-v1 §4.5 「10 HP ごとに…目盛り」 and Unity's StatusBarView.RenderTicks (review 19): a mark every 10 HP always.
        /// Where they would stand closer than TickThinGap px they are drawn 1 px wide (2 px otherwise).
        /// </summary>
        // js: scenes/battle/hud/status.js:22
        private const float TickThinGap = 8f;
        // js: scenes/battle/hud/status.js:23
        private const int PlayerChipsMax = 6;
        // js: scenes/battle/hud/status.js:24
        private const int EnemyChipsMax = 4;
        // js: scenes/battle/hud/status.js:25
        private const float Heart = 20f;
        // js: scenes/battle/hud/status.js:26
        private const float HpNumberWidth = 44f;
        // js: scenes/battle/hud/status.js:27
        private const float PartGap = 6f;
        // js: scenes/battle/hud/status.js:28
        private const float StaminaNumberWidth = 30f;
        // js: scenes/battle/hud/status.js:29
        private const float BarHeight = 14f;
        // js: scenes/battle/hud/status.js:30
        private const float ReserveShield = 24f;
        // js: scenes/battle/hud/status.js:31
        private const float StaminaDot = 16f;
        /// <summary>The chips' 説明パネル: 340 wide, its bottom 38 px over the chip's bottom, 16 px inside the screen.</summary>
        // js: scenes/battle/hud/status.js:33
        private const float TipWidth = 340f;
        // js: scenes/battle/hud/status.js:34
        private const float TipAbove = 38f;
        // js: scenes/battle/hud/status.js:35
        private const float TipEdge = 16f;
        /// <summary>The prefix a lasting stance's chip label carries (StatusChipText / StatusBarView.ChipText).</summary>
        // js: scenes/battle/hud/status.js:37
        private const string LastingPrefix = "構え・";
        /// <summary>The 説明パネル's two row heads (StatusBarView.ShowPanel). scene text</summary>
        // js: scenes/battle/hud/status.js:39
        private const string EffectHead = "効き方";
        // js: scenes/battle/hud/status.js:40
        private const string DecayHead = "減り方";
        /// <summary>The +n chip's rows join a chip and its effect with this (StatusBarView.ShowPanel). scene text</summary>
        // js: scenes/battle/hud/status.js:42
        private const string RestJoin = "：";

        // js: scenes/battle/hud/status.js:44
        private const float HpRowMid = SPL.PadTop + SPL.HpRowHeight / 2f; // 20
        // js: scenes/battle/hud/status.js:45
        private const float GuardRowTop = SPL.PadTop + SPL.HpRowHeight + SPL.RowGap; // 40
        // js: scenes/battle/hud/status.js:46
        private const float BarLeft = SPL.PadSide + Heart + PartGap + HpNumberWidth + PartGap; // 86
        /// <summary>battle-visual-v1 §4.5 (two or more enemies): a 16 px swatch at the left end of the HP row, before the heart.</summary>
        // js: scenes/battle/hud/status.js:48
        private const float Swatch = 16f;

        // ---- hud.css:5-54, verbatim (the rules the parts wear; the DS rules are Ocb.*Css) ---------------------------------

        /// <summary>hud.css:6 .ocbg-battle .ocbg-battle-stat</summary>
        private const string StatCss = "position: absolute; top: 614px; box-sizing: border-box; background: var(--panel); border-radius: var(--radius-panel); color: var(--ink);";
        /// <summary>hud.css:7 .ocbg-battle-stat-edge</summary>
        private const string EdgeCss = "position: absolute; inset: 0; box-sizing: border-box; border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); pointer-events: none;";
        /// <summary>hud.css:8 .ocbg-battle-stat.is-elite .ocbg-battle-stat-edge</summary>
        private const string EdgeEliteCss = "border: var(--stroke-frame) solid var(--phosphor);";
        /// <summary>hud.css:10 .ocbg-battle-stat.is-focus .ocbg-battle-stat-edge</summary>
        private const string EdgeFocusCss = "border: var(--stroke-frame) solid var(--amber); box-shadow: 0 0 10px color-mix(in srgb, var(--amber) 45%, transparent);";
        /// <summary>hud.css:12 .ocbg-battle-stat-hp</summary>
        private const string HpNumCss = "position: absolute; height: 30px; font: 700 24px/30px var(--font-mono); font-variant-numeric: tabular-nums; color: var(--ink); white-space: nowrap;";
        /// <summary>hud.css:13 .ocbg-battle-bar</summary>
        private const string BarCss = "position: absolute; box-sizing: border-box; border: var(--stroke-rule) solid var(--rule); border-radius: 3px; overflow: hidden; background: color-mix(in srgb, var(--ground) 80%, transparent);";
        /// <summary>hud.css:14 .ocbg-battle-bar &gt; i</summary>
        private const string BarPartCss = "position: absolute; top: 0; bottom: 0; left: 0;";
        /// <summary>hud.css:15 .ocbg-battle-bar-fill</summary>
        private const string BarFillCss = "background: var(--crimson);";
        /// <summary>hud.css:17 .ocbg-battle-bar-ticks</summary>
        private const string BarTicksCss = "position: absolute; inset: 0;";
        /// <summary>hud.css:18 .ocbg-battle-bar-ticks i</summary>
        private const string BarTickCss = "position: absolute; top: 0; bottom: 0; width: 2px; background: color-mix(in srgb, var(--ground) 72%, transparent);";
        /// <summary>hud.css:20 .ocbg-battle-bar-ticks.is-thin i</summary>
        private const string BarTickThinCss = "width: 1px;";
        /// <summary>hud.css:21 .ocbg-battle-guard</summary>
        private const string GuardCss = "position: absolute; display: flex; align-items: center; gap: 6px; transform-origin: 14px 50%;";
        /// <summary>hud.css:22 .ocbg-battle-guard-icon</summary>
        private const string GuardIconCss = "display: flex; width: 28px; height: 28px;";
        /// <summary>hud.css:23 .ocbg-battle-guard-num</summary>
        private const string GuardNumCss = "min-width: 48px; font: 700 24px/28px var(--font-mono); font-variant-numeric: tabular-nums; color: var(--guard);";
        /// <summary>hud.css:24-25 .ocbg-battle-guard.is-zero .ocbg-battle-guard-icon, … .ocbg-battle-guard-num</summary>
        private const string GuardZeroCss = "opacity: var(--opacity-guard-zero);";
        /// <summary>hud.css:26 .ocbg-battle-guard.is-zero .ocbg-battle-guard-num</summary>
        private const string GuardZeroNumCss = "color: var(--ink-muted);";
        /// <summary>hud.css:27 .ocbg-battle-foe-st</summary>
        private const string FoeStCss = "position: absolute; display: flex; align-items: center; gap: 6px;";
        /// <summary>hud.css:29 .ocbg-battle-dot</summary>
        private const string DotCss = "display: block; flex: none; border-radius: 50%;";
        /// <summary>hud.css:30 .ocbg-battle-foe-st-num</summary>
        private const string FoeStNumCss = "display: inline-block; text-align: right; font: 700 24px/28px var(--font-mono); font-variant-numeric: tabular-nums; color: var(--ink); transform-origin: 50% 50%;";
        /// <summary>hud.css:31 .ocbg-battle-foe-st-shield</summary>
        private const string FoeStShieldCss = "display: flex; width: 24px; height: 24px;";
        /// <summary>hud.css:32 .ocbg-battle-stat.is-narrow .ocbg-battle-foe-st</summary>
        private const string NarrowFoeStCss = "gap: 3px;";
        /// <summary>hud.css:33-34 .is-narrow .ocbg-battle-foe-st &gt; .ocbg-battle-dot</summary>
        private const string NarrowDotCss = "width: 12px !important; height: 12px !important;";
        /// <summary>hud.css:35-36 .is-narrow .ocbg-battle-foe-st-shield (and its .ocb-icon)</summary>
        private const string NarrowShieldCss = "width: 20px; height: 20px;";
        /// <summary>hud.css:37 .is-narrow .ocbg-battle-foe-st-num</summary>
        private const string NarrowFoeStNumCss = "min-width: 0;";
        /// <summary>hud.css:38 .is-narrow .ocbg-battle-guard-num</summary>
        private const string NarrowGuardNumCss = "min-width: 0;";
        /// <summary>hud.css:39 .ocbg-battle-stat.is-solid</summary>
        private const string SolidCss = "background: var(--panel-solid);";
        /// <summary>hud.css:42 .ocbg-battle-stat.is-down (opacity --opacity-unplayed 0.55, grayscale 1: applied by ApplyLook)</summary>
        private const string DownCss = "opacity: var(--opacity-unplayed); filter: grayscale(1);";
        /// <summary>hud.css:43 .ocbg-battle-stat.is-down .ocbg-battle-bar-fill</summary>
        private const string DownFillCss = "background: var(--steel);";
        /// <summary>hud.css:44 .ocbg-battle-chips</summary>
        private const string ChipsCss = "position: absolute; display: flex; flex-wrap: wrap; align-content: flex-start; gap: 5px;";
        /// <summary>hud.css:45 .ocbg-battle-chip</summary>
        private const string ChipCss = "pointer-events: auto; box-sizing: border-box; color: var(--ink); transform-origin: 50% 50%;";
        /// <summary>hud.css:46 .ocbg-battle-chip .ocb-mono</summary>
        private const string ChipMonoCss = "font-variant-numeric: tabular-nums;";
        /// <summary>hud.css:49 .ocbg-battle-tip (added by the hud's tip)</summary>
        private const string TipCss = "position: absolute; pointer-events: none; box-sizing: border-box;";
        /// <summary>hud.css:50 .ocbg-battle-chiptip .ocb-tip-head</summary>
        private const string ChipTipHeadCss = "color: var(--ink);";
        /// <summary>hud.css:51 .ocbg-battle-chiptip .ocb-tip-head small</summary>
        private const string ChipTipSmallCss = "color: var(--ink); font-weight: 400;";
        /// <summary>hud.css:52 .ocbg-battle-chiptip p</summary>
        private const string ChipTipPCss = "color: var(--ink);";
        /// <summary>hud.css:53 .ocbg-battle-chiptip p + p</summary>
        private const string ChipTipPPlusPCss = "margin-top: 4px;";
        /// <summary>hud.css:54 .ocbg-battle-chiptip p span</summary>
        private const string ChipTipPSpanCss = "color: var(--ink); font-weight: 700; margin-right: 10px;";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>One chip's view: { el, chip, rest } (status.js:233), with its node, box and :hover.</summary>
        private sealed class ChipView
        {
            public RectTransform El;
            public UiNode Node;
            public StatusChip Chip;
            /// <summary>The chips a "+n" chip stands for; null for a word's own chip.</summary>
            public List<StatusChip> Rest;
            public bool Self;
            public bool Boss;
            public bool Hover;
        }

        /// <summary>chipStamps' value: { stamp, stacks }.</summary>
        private struct ChipStamp
        {
            public int Stamp;
            public int Stacks;
        }

        private readonly BattleViewEnv _env;
        private readonly StatusPanelDeps _deps;
        private readonly bool _foe;
        private readonly HudTip _tip;

        // ---- st (status.js:78) ----
        private float _width;
        private float _left;
        private int _hpMax = 1;
        private int _hp;
        private float _fill;
        private float _trail;
        private int _loss;
        private int _guard = -1;
        private int _stamina;
        // st.showStamina (status.js:453) is written and never read in the JS: not kept.
        private bool? _reserve;
        private bool _elite;
        private bool _down;
        private List<StatusChip> _chips = new List<StatusChip>();
        private string _chipKey = "";
        private int _rows;
        private float _chipsW;
        private bool _shown = true;
        private float? _swatch;
        private bool _yielded;
        private bool _placed;
        private bool _greying;
        private int _greyToken;
        // The look the classes give (is-solid / is-focus / is-narrow), and greyOut's inline opacity / grayscale.
        private bool _solid;
        private bool _focus;
        private bool? _narrow;
        private bool? _guardZero;
        private float? _greyOpacity;
        private float _greyGray;

        // ---- parts (status.js:84-110) ----
        private readonly RectTransform _el;
        private readonly UiNode _elNode;
        private readonly RectTransform _edge;
        private readonly PathGraphic _heart;
        private readonly TextMeshProUGUI _hpNum;
        private readonly RectTransform _trailEl;
        private readonly RectTransform _fillEl;
        private readonly HatchGraphic _wickEl;
        private readonly RectTransform _ticksEl;
        private readonly RectTransform _bar;
        private readonly RectTransform _barInner;
        private readonly RectTransform _guardIconBox;
        private readonly TextMeshProUGUI _guardNum;
        private readonly RectTransform _guardBadge;
        private readonly TextMeshProUGUI _stNum;
        private readonly RectTransform _reserveBox;
        private readonly RectTransform _stDot;
        private readonly RectTransform _stGroup;
        private readonly RectTransform _chipsEl;
        private OcbElement _swatchEl;
        private PathGraphic _reserveIcon;

        /// <summary>How far the heart, the number and the bar stand right of their places (the swatch and its gap, or 0).</summary>
        // js: scenes/battle/hud/status.js:115
        private float _hpShift;
        // js: scenes/battle/hud/status.js:159
        private string _ticksFor = "";
        private bool _ticksThin;
        // The bar's padding box (the parts' containing block) and its rounded clip (overflow: hidden, radius 3 − 1).
        private float _barPw;
        private float _barPh;
        private List<Vec2> _barClip = new List<Vec2>();

        // js: scenes/battle/hud/status.js:193
        private List<ChipView> _chipViews = new List<ChipView>();
        // js: scenes/battle/hud/status.js:200
        private readonly Dictionary<string, ChipStamp> _chipStamps = new Dictionary<string, ChipStamp>(StringComparer.Ordinal);
        // js: scenes/battle/hud/status.js:201
        private int _chipStampNow;

        // js: scenes/battle/hud/status.js:73
        private HudStatusPanel(BattleViewEnv env, UnitSide side, StatusPanelDeps deps)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
            Side = side;
            _deps = deps ?? new StatusPanelDeps();
            _foe = side == UnitSide.Enemy;
            _tip = _deps.Tip;

            // ---- parts ----
            // js: scenes/battle/hud/status.js:84
            _el = Ocb.El(env.Layer.Panels, "ocbg-battle-stat" + (_foe ? " is-foe" : " is-self"), StatCss);
            _elNode = UiNode.Of(_el);
            // js: scenes/battle/hud/status.js:85
            _edge = Ocb.El(_el, "ocbg-battle-stat-edge", EdgeCss);
            // js: scenes/battle/hud/status.js:86 OCB.icon('heart', HEART, tok('crimson')), .ocbg-battle-stat-heart (position: absolute)
            _heart = Ocb.Icon(_el, "heart", Heart, DsTokens.Crimson);
            _heart.gameObject.name = "ocbg-battle-stat-heart";
            // js: scenes/battle/hud/status.js:88
            _hpNum = Label.Make(_el, "", Ocb.TextOf(StatCss + HpNumCss), SPL.PadSide + Heart + PartGap, HpRowMid - 15f, HpNumberWidth, 30f, "ocbg-battle-stat-hp");
            // js: scenes/battle/hud/status.js:93 the bar: trail, fill, wick, ticks (absolutely placed in its padding box)
            _bar = Ocb.El(_el, "ocbg-battle-bar", BarCss);
            _barInner = Ui.Div(_bar, "padding-box");
            UiNode.Of(_barInner);
            // js: scenes/battle/hud/status.js:575 the grey band (whiff 85 %) behind the fill: trailEl.style.background = mix('miss', 85)
            _trailEl = Ocb.El(_barInner, "ocbg-battle-bar-trail", BarPartCss + "background: " + HudUtil.Mix("miss", 85f) + ";");
            _fillEl = Ocb.El(_barInner, "ocbg-battle-bar-fill", BarPartCss + BarFillCss);
            // js: scenes/battle/hud/status.js:139 HpWickSpec: 3 px of wick, 3 px of the bar between, running ／ (the mockup's 135° gradient).
            _wickEl = HatchGraphic.Stripes(_barInner, 0f, 0f, 0f, 0f, HpWickSpec.AngleDegrees, 0f, HpWickSpec.StripeWidth, HpWickSpec.Period,
                DsTokens.Wick, 0f, "ocbg-battle-bar-wick");
            _wickEl.TrySetStripesCss("repeating-linear-gradient(" + Ocb.Num(HpWickSpec.AngleDegrees) + "deg, " + HudUtil.Tok("wick") + " 0 "
                + Ocb.Num(HpWickSpec.StripeWidth) + "px, transparent " + Ocb.Num(HpWickSpec.StripeWidth) + "px " + Ocb.Num(HpWickSpec.Period) + "px)");
            _wickEl.gameObject.SetActive(false); // .ocbg-battle-bar-wick { display: none }
            _ticksEl = Ocb.El(_barInner, "ocbg-battle-bar-ticks", BarTicksCss);
            // js: scenes/battle/hud/status.js:95 The Guard badge: the shield with its number beside it, one part that pops and shakes.
            _guardBadge = Ocb.El(_el, "ocbg-battle-guard", GuardCss);
            _guardIconBox = Ocb.El(_guardBadge, "ocbg-battle-guard-icon", GuardIconCss);
            Ocb.Fixed(_guardIconBox);
            _guardNum = Ocb.Text(_guardBadge, "", GuardNumStyle(false), "ocbg-battle-guard-num");
            UiNode.Of(_guardNum.rectTransform);
            // js: scenes/battle/hud/status.js:99 An enemy's stamina dot, number and 構えの盾, right-aligned on the Guard row.
            _stGroup = Ocb.El(_el, "ocbg-battle-foe-st", FoeStCss);
            // js: scenes/battle/hud/status.js:103 the enemy's dot is a full 16×16 circle (§4.5 粒 16 px)
            _stDot = Ocb.El(_stGroup, "ocbg-battle-dot", DotCss + "width: " + Ocb.Num(StaminaDot) + "px; height: " + Ocb.Num(StaminaDot)
                + "px; background: " + HudUtil.Tok("ink-muted") + ";");
            Ocb.NoShrink(_stDot);
            _stNum = Ocb.Text(_stGroup, "", Ocb.TextOf(StatCss + FoeStCss + FoeStNumCss), "ocbg-battle-foe-st-num");
            UiNode.Of(_stNum.rectTransform); // transform-origin: 50% 50% (the UiNode default)
            _reserveBox = Ocb.El(_stGroup, "ocbg-battle-foe-st-shield", FoeStShieldCss);
            Ocb.Fixed(_reserveBox);
            // js: scenes/battle/hud/status.js:108
            _chipsEl = Ocb.El(_el, "ocbg-battle-chips", ChipsCss);

            // ---- fixed parts of the layout (StatusBarView.EnsureLayout) ----
            // js: scenes/battle/hud/status.js:123-137
            UiNode.Of(_hpNum.rectTransform).Place(SPL.PadSide + Heart + PartGap, HpRowMid - 15f, HpNumberWidth, 30f);
            UiNode.Of(_guardBadge).Place(SPL.PadSide, GuardRowTop, null, SPL.GuardRowHeight);
            UiNode.Of(_stGroup).Place(0f, GuardRowTop, null, SPL.GuardRowHeight);
            FlexBox.Attrs(_stNum.rectTransform).MinMain = StaminaNumberWidth; // stNum.style.minWidth
            UiNode.Of(_stGroup).Visible = false; // stGroup.style.display = 'none'
            UiNode.Of(_chipsEl).Place(SPL.PadSide, SPL.ChipRowTop);

            // js: scenes/battle/hud/status.js:570
            SetWidth(_foe ? SPL.EnemyWidth : SPL.PlayerWidth);
            PlaceAt(0f);
            SetGuardNow(0);
            SetReserve(false);
            RenderChips(true);
            LayoutStGroup();
        }

        /// <summary>createStatusPanel(env, side, deps): the panel in layer.panels, at its default width, placed at 0, Guard 0, no chips.</summary>
        // js: scenes/battle/hud/status.js:73
        public static HudStatusPanel Create(BattleViewEnv env, UnitSide side, StatusPanelDeps deps = null) => new HudStatusPanel(env, side, deps);

        private BattleClock Clock => _env.Clock;

        // js: scenes/battle/hud/status.js:50
        /// <summary>
        /// StatusBarView.Copy: a source from before #242 marks a stance as a chip with no stacks and no family. A copy, so the
        /// frame's chips are never written to.
        /// </summary>
        private static StatusChip CopyChip(StatusChip c)
        {
            string label = c.Label ?? "";
            bool lasting = c.Kind == ChipKind.Lasting || (c.Stacks <= 0 && label.StartsWith(LastingPrefix, StringComparison.Ordinal));
            return new StatusChip
            {
                Label = label,
                Stacks = c.Stacks,
                Kind = lasting ? ChipKind.Lasting : c.Kind,
                Title = c.Title ?? "",
                Group = c.Group ?? "",
                Effect = c.Effect ?? "",
                Decay = c.Decay ?? "",
            };
        }

        /// <summary>chipText(c) = StatusBarView.ChipText: "出血 2", a stance "岩の構え ∞ ×2" (no "×1"), or "+n".</summary>
        // js: scenes/battle/hud/status.js:58
        public static string ChipText(StatusChip c)
        {
            if (c == null) return "";
            string label = c.Label ?? "";
            if (c.Kind == ChipKind.Lasting)
            {
                string name = label.StartsWith(LastingPrefix, StringComparison.Ordinal) ? label.Substring(LastingPrefix.Length) : label;
                return name + " ∞" + (c.Stacks > 1 ? " ×" + c.Stacks.ToString(Inv) : "");
            }
            if (label.StartsWith("+", StringComparison.Ordinal)) return label;
            return label + " " + c.Stacks.ToString(Inv);
        }

        // ---- fixed parts of the layout ------------------------------------------------------------------------------

        /// <summary>
        /// layoutHpRow (StatusBarView.EnsureLayout): the heart, the number and the bar at their places, moved right by the swatch
        /// when it has one; the bar as wide as the panel leaves it. Here the bar's padding box and its parts follow in px (the
        /// browser's percentages do it by themselves).
        /// </summary>
        // js: scenes/battle/hud/status.js:116
        private void LayoutHpRow()
        {
            Ui.Place(_heart.rectTransform, SPL.PadSide + _hpShift, HpRowMid - Heart / 2f);
            UiNode.Of(_hpNum.rectTransform).Place(SPL.PadSide + _hpShift + Heart + PartGap, HpRowMid - 15f);
            float barW = Math.Max(8f, _width - SPL.PadSide - BarLeft - _hpShift);
            UiNode.Of(_bar).Place(BarLeft + _hpShift, HpRowMid - BarHeight / 2f, barW, BarHeight);
            SizeBarInside(barW);
            DrawTicks();
            PlaceTicks();
            DrawBar();
            DrawWick();
        }

        /// <summary>The bar's padding box (inside its 1 px border) and the rounded clip its parts share (overflow: hidden, radius 3).</summary>
        private void SizeBarInside(float barW)
        {
            BoxGraphic box = _bar.GetComponent<BoxGraphic>();
            Insets bw = box ? box.Style.BorderWidths : Insets.All(1f);
            float pw = Math.Max(0f, barW - bw.Horizontal), ph = Math.Max(0f, BarHeight - bw.Vertical);
            if (Math.Abs(pw - _barPw) < 1e-4f && Math.Abs(ph - _barPh) < 1e-4f && _barClip.Count > 0) return;
            _barPw = pw;
            _barPh = ph;
            UiNode.Of(_barInner).Place(bw.Left, bw.Top, pw, ph);
            UiNode.Of(_ticksEl).Place(0f, 0f, pw, ph); // inset: 0
            _barClip = Shapes.RoundRect(new RectF(0f, 0f, pw, ph), CornerSet.All(Math.Max(0f, 3f - bw.Top))).Points;
            Clip(_trailEl, 0f);
            Clip(_fillEl, 0f);
        }

        private void Clip(RectTransform part, float left)
        {
            BoxGraphic g = part ? part.GetComponent<BoxGraphic>() : null;
            if (g) g.ClipConvex = Translate(_barClip, -left);
        }

        private static List<Vec2> Translate(List<Vec2> pts, float dx)
        {
            var o = new List<Vec2>(pts.Count);
            foreach (Vec2 p in pts) o.Add(new Vec2(p.X + dx, p.Y));
            return o;
        }

        // ---- drawing --------------------------------------------------------------------------------------------------

        // js: scenes/battle/hud/status.js:143
        private float WidthOf(int hp) => HudUtil.Clamp(hp / (float)_hpMax, 0f, 1f);

        // js: scenes/battle/hud/status.js:145
        private void DrawBar()
        {
            // fillEl.style.width = (st.fill * 100).toFixed(3) + '%' (of the bar's padding box)
            UiNode.Of(_fillEl).Place(0f, 0f, _barPw * Fixed(_fill * 100f, 3) / 100f, _barPh);
            UiNode.Of(_trailEl).Place(0f, 0f, _barPw * Fixed(_trail * 100f, 3) / 100f, _barPh);
        }

        /// <summary>StatusBarView.RenderWick: the stripes over the part of the bar a held card would empty.</summary>
        // js: scenes/battle/hud/status.js:151
        private void DrawWick()
        {
            float from = 0f, to = 0f;
            bool ok = _foe && HpWickSpec.Span(_hp, _hpMax, _loss, out from, out to);
            if (!ok)
            {
                _wickEl.gameObject.SetActive(false);
                return;
            }
            _wickEl.gameObject.SetActive(true);
            float left = _barPw * Fixed(from * 100f, 3) / 100f;
            float width = _barPw * Fixed((to - from) * 100f, 3) / 100f;
            Ui.Place(_wickEl.rectTransform, left, 0f, width, _barPh);
            _wickEl.ClipConvex = Translate(_barClip, -left);
        }

        /// <summary>StatusBarView.RenderTicks: a black 55 % mark every 10 HP of the maximum (1 px wide on a narrow bar).</summary>
        // js: scenes/battle/hud/status.js:161
        private void DrawTicks()
        {
            float barW = Math.Max(8f, _width - SPL.PadSide - BarLeft - _hpShift);
            string key = _hpMax.ToString(Inv) + "@" + JsRound(barW).ToString(Inv);
            if (_ticksFor == key) return;
            _ticksFor = key;
            Ui.Clear(_ticksEl);
            float pxPerHp = barW / _hpMax;
            bool thin = HpPerTick * pxPerHp < TickThinGap;
            _ticksThin = thin; // ticksEl.classList.toggle('is-thin', thin)
            int count = (_hpMax - 1) / HpPerTick;
            for (int i = 0; i < count; i++) Ocb.El(_ticksEl, "i", BarTickCss + (thin ? BarTickThinCss : ""));
            PlaceTicks();
        }

        /// <summary>
        /// Each mark at calc(at% − 1px) of the bar's padding box (calc(at% − 0.5px) when thin), at = (i + 1)·10 / max; placed again
        /// whenever the bar's width changes (a percentage follows it in the browser).
        /// </summary>
        // js: scenes/battle/hud/status.js:171
        private void PlaceTicks()
        {
            float w = _ticksThin ? 1f : 2f;
            for (int i = 0; i < _ticksEl.childCount; i++)
            {
                var tick = (RectTransform)_ticksEl.GetChild(i);
                float at = (i + 1) * HpPerTick / (float)_hpMax;
                float left = _barPw * Fixed(at * 100f, 3) / 100f - (_ticksThin ? 0.5f : 1f);
                UiNode.Of(tick).Place(left, 0f, w, _barPh);
                Clip(tick, left);
            }
        }

        /// <summary>setWidth(w): the panel's width (the HP bar follows).</summary>
        // js: scenes/battle/hud/status.js:179 (setWidth)
        public void SetWidth(float w)
        {
            float width = Math.Max(SPL.PadSide * 2f, w);
            if (Math.Abs(width - _width) < 0.01f) return;
            _width = width;
            _elNode.W = width;
            // A panel narrower than the 210 of §4.5 (an enemy on a line of 7 or 8 cells: EnemyPanelWidth) packs its Guard row
            // tighter, so two-digit Guard and stamina numbers keep apart at 24 px.
            SetNarrow(width < SPL.EnemyWidth);
            LayoutHpRow();
            UiNode.Of(_chipsEl).W = SPL.ContentWidth(width);
            RenderChips(true);
            LayoutStGroup(); // right: 10px follows the width
        }

        /// <summary>el.classList.toggle('is-narrow', on) (hud.css:32-38): the Guard row's tighter look.</summary>
        // js: scenes/battle/hud/status.js:186
        private void SetNarrow(bool on)
        {
            if (_narrow == on) return;
            _narrow = on;
            float dot = on ? 12f : StaminaDot; // NarrowDotCss (!important over the inline 16)
            UiNode.Of(_stDot).Place(0f, 0f, dot, dot);
            float shield = on ? 20f : ReserveShield; // NarrowShieldCss (over the svg's own width / height attributes)
            UiNode.Of(_reserveBox).Place(0f, 0f, shield, shield);
            if (_reserveIcon) Ui.Place(_reserveIcon.rectTransform, 0f, 0f, shield, shield);
            // NarrowFoeStNumCss (min-width: 0) loses to stNum's inline min-width 30 (status.js:134): the number keeps its 30.
            FlexBox.Attrs(_stNum.rectTransform).MinMain = StaminaNumberWidth;
            LayoutGuardRow();
            LayoutStGroup();
        }

        private static float? MinWidthOf(string css) => Ocb.Px(Ocb.Rule(css).Layout.MinWidth);

        // ---- chips (§4.5 / §7.3) ---------------------------------------------------------------------------------------

        /// <summary>
        /// battle_ui_ux_v2 §1.6 「並べる順は、最後にスタックが変わった順（新しいものが左）」 and §5.10 順 2 「新規は列の左端に入り」 (review 21):
        /// each word keeps the stamp of the last time its stacks changed, and the row reads newest first, so the oldest fold into
        /// 「+n」. The opening frame keeps the frame's own order. The sort is stable, as Array.prototype.sort.
        /// </summary>
        // js: scenes/battle/hud/status.js:202
        private List<StatusChip> OrderChips(List<StatusChip> list)
        {
            bool fresh = _chipStamps.Count == 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int k = 0; k < list.Count; k++)
            {
                StatusChip c = list[k];
                seen.Add(c.Label);
                if (!_chipStamps.TryGetValue(c.Label, out ChipStamp was))
                    _chipStamps[c.Label] = new ChipStamp { Stamp = fresh ? -k : ++_chipStampNow, Stacks = c.Stacks };
                else if (was.Stacks != c.Stacks)
                    _chipStamps[c.Label] = new ChipStamp { Stamp = ++_chipStampNow, Stacks = c.Stacks };
            }
            var gone = new List<string>();
            foreach (string label in _chipStamps.Keys) if (!seen.Contains(label)) gone.Add(label);
            foreach (string label in gone) _chipStamps.Remove(label);
            var order = new List<KeyValuePair<int, StatusChip>>(list.Count);
            for (int k = 0; k < list.Count; k++) order.Add(new KeyValuePair<int, StatusChip>(k, list[k]));
            order.Sort((a, b) =>
            {
                int d = _chipStamps[b.Value.Label].Stamp.CompareTo(_chipStamps[a.Value.Label].Stamp);
                return d != 0 ? d : a.Key.CompareTo(b.Key);
            });
            var sorted = new List<StatusChip>(list.Count);
            foreach (KeyValuePair<int, StatusChip> kv in order) sorted.Add(kv.Value);
            return sorted;
        }

        /// <summary>The chip's declarations: .ocb-chip, .is-self, .is-boss, :hover (bundle.css order), then .ocbg-battle-chip.</summary>
        private static string ChipBoxCss(bool self, bool boss, bool hover) =>
            Ocb.ChipCss + (self ? Ocb.ChipSelfCss : "") + (boss ? Ocb.ChipBossCss : "") + (hover ? Ocb.ChipHoverCss : "") + ChipCss;

        // js: scenes/battle/hud/status.js:215
        private ChipView ChipEl(StatusChip c, List<StatusChip> rest)
        {
            bool self = c.Kind == ChipKind.OnSelf || c.Kind == ChipKind.Lasting;
            bool boss = c.Kind == ChipKind.Boss;
            HudStatusIcon? known = HudUtil.StatusIconOf(c.Label);
            string css = ChipBoxCss(self, boss, false);
            RectTransform node = Ocb.El(_chipsEl, "ocb-chip ocbg-battle-chip" + (self ? " is-self" : "") + (boss ? " is-boss" : ""), css);
            TextStyle text = Ocb.TextOf(Ocb.ChipCss + ChipCss);
            TextStyle mono = Ocb.TextOf(Ocb.ChipCss + ChipCss + Ocb.MonoCss + Ocb.ChipMonoCss + ChipMonoCss);
            if (rest != null)
            {
                Ocb.Text(node, c.Label, mono, "ocb-mono");
            }
            else if (c.Kind == ChipKind.Lasting)
            {
                string name = c.Label.StartsWith(LastingPrefix, StringComparison.Ordinal) ? c.Label.Substring(LastingPrefix.Length) : c.Label;
                Ocb.Text(node, name, text, "span");
                Ocb.Icon(node, "infinity", 18f, DsTokens.HpText);
                if (c.Stacks > 1) Ocb.Text(node, "×" + c.Stacks.ToString(Inv), mono, "ocb-mono");
            }
            else
            {
                if (known.HasValue) Ocb.Icon(node, known.Value.Id, 18f, DsTokens.Ink);
                Ocb.Text(node, c.Label, text, "span");
                Ocb.Text(node, c.Stacks.ToString(Inv), mono, "ocb-mono");
            }
            Ocb.Flow(node, css);
            var view = new ChipView { El = node, Node = UiNode.Of(node), Chip = c, Rest = rest, Self = self, Boss = boss };
            // pointer-events: auto (.ocbg-battle-chip): the chip's own box takes the pointer
            ClickArea area = ClickArea.On(node);
            area.HoverChanged += on =>
            {
                // .ocb-chip:hover { border-color: var(--ink); background: rgba(255,255,255,.1) }
                view.Hover = on;
                if (view.El) Ocb.Restyle(view.El, ChipBoxCss(view.Self, view.Boss, on));
            };
            // js: scenes/battle/hud/status.js:234
            area.Enter += _ => ShowChipTip(view);
            // js: scenes/battle/hud/status.js:235
            area.Exit += _ => { if (_tip != null) _tip.Hide(view); };
            // js: scenes/battle/hud/status.js:237 §3.13 (review 21): a right click pins the same panel (効き方 / 減り方) until a
            // click, Esc or another right click. contextmenu fires on the right button's release (Windows), on the chip under it.
            area.Up += e =>
            {
                if (e == null || e.button != PointerEventData.InputButton.Right || !area.Hovered) return;
                _env.MarkContextPinned(); // e.ocbgPinned = true
                ShowChipTip(view, true);
            };
            return view;
        }

        /// <summary>
        /// StatusBarView.RenderChips: the player shows six and an enemy four; the rest gather in one "+n" chip whose panel lists
        /// them. The row wraps 5 px apart and the panel grows down to hold its rows.
        /// </summary>
        // js: scenes/battle/hud/status.js:245
        private void RenderChips(bool force)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _chips.Count; i++)
            {
                StatusChip c = _chips[i];
                if (i > 0) sb.Append(';');
                sb.Append((int)c.Kind).Append('|').Append(c.Label).Append('|').Append(c.Stacks.ToString(Inv));
            }
            sb.Append('@').Append(Ocb.Num(_width));
            string key = sb.ToString();
            int most = _foe ? EnemyChipsMax : PlayerChipsMax;
            bool gather = _chips.Count > most;
            int shown = gather ? most - 1 : _chips.Count;
            if (!force && key == _chipKey)
            {
                // Review 16: the same chips, but their texts may be new — a cue's setStatus made the chip with empty Group /
                // Effect / Decay, and the event's frame brings them. The views take the frame's chips (their 説明パネル reads them).
                for (int i = 0; i < _chipViews.Count; i++)
                {
                    ChipView v = _chipViews[i];
                    if (v.Rest != null) v.Rest = _chips.GetRange(shown, _chips.Count - shown);
                    else if (i < _chips.Count) v.Chip = _chips[i];
                }
                return;
            }
            _chipKey = key;
            if (_tip != null) foreach (ChipView v in _chipViews) _tip.Hide(v);
            Ui.Clear(_chipsEl);
            _chipViews = new List<ChipView>();
            for (int i = 0; i < shown; i++) _chipViews.Add(ChipEl(_chips[i], null));
            if (gather)
            {
                List<StatusChip> rest = _chips.GetRange(shown, _chips.Count - shown);
                _chipViews.Add(ChipEl(new StatusChip { Label = "+" + rest.Count.ToString(Inv), Stacks = 0, Kind = ChipKind.OnSelf }, rest));
            }
            // The rows the wrap made (one per distinct top), for the panel's height (StatusPanelLayout.Height).
            var tops = new HashSet<int>();
            if (_chipViews.Count > 0)
            {
                FlexResult r = Ocb.Flow(_chipsEl, ChipsCss, null, SPL.ContentWidth(_width));
                if (r.Line != null) foreach (int line in r.Line) tops.Add(line);
            }
            _rows = _chipViews.Count > 0 ? Math.Max(1, tops.Count) : 0;
            _elNode.H = SPL.Height(_rows);
            SizeEdge();
            // How far from the panel's left edge its chips reach (the hover line keeps off them when it must cover the panel).
            float chipsW = 0f;
            foreach (ChipView v in _chipViews) chipsW = Math.Max(chipsW, SPL.PadSide + v.Node.X + v.Node.W);
            _chipsW = chipsW;
        }

        // js: scenes/battle/hud/status.js:280
        private ChipView ChipOf(string label)
        {
            foreach (ChipView v in _chipViews) if (v.Rest == null && v.Chip.Label == label) return v;
            return null;
        }

        /// <summary>StatusBarView.ShowPanel: the word's 説明パネル above the chip, kept inside the screen.</summary>
        // js: scenes/battle/hud/status.js:285
        private void ShowChipTip(ChipView view, bool pin = false)
        {
            if (_tip == null || !_shown || view == null || !view.El) return;
            RectTransform node = BuildChipTip(view);
            Action<RectTransform> place = n =>
            {
                if (!n || !view.El) return;
                UiNode nn = UiNode.Of(n);
                float chipLeft = _left + SPL.PadSide + view.Node.X;
                float chipBottom = SPL.Top + SPL.ChipRowTop + view.Node.Y + SPL.ChipHeight;
                nn.Place(HudUtil.Clamp(chipLeft, TipEdge, L.W - TipEdge - TipWidth), chipBottom - TipAbove - nn.H);
            };
            if (pin) _tip.Pin(node, place, view);
            else _tip.Show(node, place, view);
        }

        /// <summary>
        /// The 説明パネル's node: div.ocb-tip.ocbg-battle-chiptip (340 wide) — the head (the word's icon 20, or ∞ for a stance; the
        /// title; the family as small at the right), then 効き方 / 減り方 rows, or for "+n" one row per hidden chip
        /// (chipText：effect). Laid out as the DS's .ocb-tip with hud.css's chip-tip rules over it.
        /// </summary>
        // js: scenes/battle/hud/status.js:286-302
        private RectTransform BuildChipTip(ChipView view)
        {
            StatusChip c = view.Chip;
            bool rest = view.Rest != null;
            // node.style.width = TIP_WIDTH + 'px' (inline, after the classes)
            string tipCss = Ocb.TipCss + TipCss + "width: " + Ocb.Num(TipWidth) + "px;";
            RectTransform node = Ocb.El(_env.Layer.Over, "ocb-tip ocbg-battle-chiptip", tipCss);
            UiNode nn = UiNode.Of(node);
            BoxGraphic box = node.GetComponent<BoxGraphic>();
            BoxStyle bs = box ? box.Style : Ocb.BoxOf(tipCss);
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            float innerW = nn.W - bw.Horizontal - pad.Horizontal;
            float x = bw.Left + pad.Left, y = bw.Top + pad.Top;

            // js: scenes/battle/hud/status.js:288 the head
            string headCss = Ocb.TipHeadCss + ChipTipHeadCss;
            RectTransform head = Ocb.El(node, "ocb-tip-head", headCss);
            HudStatusIcon? known = !rest ? HudUtil.StatusIconOf(c.Label) : (HudStatusIcon?)null;
            if (known.HasValue) Ocb.Icon(head, known.Value.Id, 20f, DsTokens.Ink);
            else if (c.Kind == ChipKind.Lasting) Ocb.Icon(head, "infinity", 20f, DsTokens.HpText);
            string title = rest ? c.Label : (string.IsNullOrEmpty(c.Title) ? ChipText(c) : c.Title);
            Ocb.Text(head, title ?? "", Ocb.TextOf(Ocb.TipCss + headCss), "span");
            if (!rest && !string.IsNullOrEmpty(c.Group))
            {
                TextMeshProUGUI small = Ocb.Text(head, c.Group, Ocb.TextOf(Ocb.TipCss + headCss + Ocb.TipHeadSmallCss + ChipTipSmallCss), "small");
                Ocb.AutoMarginLeft(small);
            }
            Ocb.Flow(head, headCss, null, innerW);
            UiNode headNode = UiNode.Of(head);
            headNode.Place(x, y);
            y += headNode.H + (Ocb.Px(Ocb.Rule(Ocb.TipHeadCss).Layout.MarginBottom) ?? 6f);

            // js: scenes/battle/hud/status.js:294 the body
            TextStyle ps = Ocb.TextOf(Ocb.TipCss + Ocb.TipPCss + ChipTipPCss);
            ps.RichText = true;
            float spanGap = Ocb.Px(Ocb.Rule(Ocb.TipPSpanCss + ChipTipPSpanCss).Layout.MarginRight) ?? 10f;
            // p span { font-weight: 700 }: the head word in the body face's bold
            string bold = "<font=\"" + TrialFonts.Tag(Family.Body, 700) + "\">";
            var rows = new List<string>();
            if (rest)
            {
                foreach (StatusChip o in view.Rest) rows.Add(Esc(ChipText(o) + RestJoin + (o.Effect ?? "")));
            }
            else if (!string.IsNullOrEmpty(c.Effect) || !string.IsNullOrEmpty(c.Decay))
            {
                rows.Add(bold + Esc(EffectHead) + "</font><space=" + Ocb.Num(spanGap) + ">" + Esc(c.Effect ?? ""));
                rows.Add(bold + Esc(DecayHead) + "</font><space=" + Ocb.Num(spanGap) + ">" + Esc(c.Decay ?? ""));
            }
            float pGap = Ocb.Px(Ocb.Rule(ChipTipPPlusPCss).Layout.MarginTop) ?? 4f;
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) y += pGap; // p + p { margin-top: 4px }
                float h = Ocb.TextHeight(rows[i], ps, innerW);
                Label.Make(node, rows[i], ps, x, y, innerW, h, "p");
                y += h;
            }
            nn.H = y + pad.Bottom + bw.Bottom;
            return node;
        }

        /// <summary>Text made safe for TMP rich text (a literal '&lt;' cannot open a tag).</summary>
        private static string Esc(string text) => string.IsNullOrEmpty(text) ? "" : text.Replace("<", "<noparse><</noparse>");

        // ---- the Guard row --------------------------------------------------------------------------------------------

        /// <summary>setGuard(guard) (internal): the number, the solid / dashed shield when it turns, .is-zero.</summary>
        // js: scenes/battle/hud/status.js:313
        private void SetGuardNow(int guard)
        {
            Label.SetText(_guardNum, guard.ToString(Inv));
            bool on = guard > 0;
            if ((_guard > 0) != on || _guard < 0)
            {
                Ui.Clear(_guardIconBox);
                HudUtil.ShieldIcon(_guardIconBox, SPL.GuardRowHeight, on);
            }
            _guard = guard;
            SetGuardZero(!on);
            LayoutGuardRow();
        }

        /// <summary>guardBadge.classList.toggle('is-zero', zero) (hud.css:24-26): the icon and the number at .7, the number ink-muted.</summary>
        private void SetGuardZero(bool zero)
        {
            if (_guardZero == zero) return;
            _guardZero = zero;
            float op = zero ? DsTokens.OpacityGuardZero : 1f; // GuardZeroCss: opacity: var(--opacity-guard-zero)
            UiNode.Of(_guardIconBox).Opacity = op;
            UiNode.Of(_guardNum.rectTransform).Opacity = op;
            Label.Apply(_guardNum, GuardNumStyle(zero));
        }

        private static TextStyle GuardNumStyle(bool zero) => Ocb.TextOf(StatCss + GuardCss + GuardNumCss + (zero ? GuardZeroNumCss : ""));

        /// <summary>The Guard badge's flex row (icon 28, gap 6, the number at least 48 wide; narrow: no minimum), its transform-origin 14px 50%.</summary>
        // js: scenes/battle/hud/status.js:128 (+ hud.css:21-23, 38)
        private void LayoutGuardRow()
        {
            Remeasure(_guardNum);
            FlexBox.Attrs(_guardNum.rectTransform).MinMain = MinWidthOf(GuardNumCss + (_narrow == true ? NarrowGuardNumCss : ""));
            Ocb.Flow(_guardBadge, GuardCss, null, null, SPL.GuardRowHeight);
            UiNode badge = UiNode.Of(_guardBadge);
            badge.Place(SPL.PadSide, GuardRowTop);
            badge.SetOrigin(14f / Math.Max(1f, badge.W), 0.5f);
        }

        /// <summary>The enemy's stamina group: dot, number (right-aligned, at least 30 wide), 構えの盾; right: 10px, top 40, 28 tall.</summary>
        // js: scenes/battle/hud/status.js:131 (+ hud.css:27-37)
        private void LayoutStGroup()
        {
            Remeasure(_stNum);
            Ocb.Flow(_stGroup, FoeStCss + (_narrow == true ? NarrowFoeStCss : ""), null, null, SPL.GuardRowHeight);
            UiNode g = UiNode.Of(_stGroup);
            g.Place(_width - SPL.PadSide - g.W, GuardRowTop);
        }

        /// <summary>A label measured again from its text before a flex layout (the solver fixed its last box).</summary>
        private static void Remeasure(TMP_Text t)
        {
            LabelState st = Label.StateOf(t);
            st.AutoW = true;
            st.AutoH = true;
        }

        // js: scenes/battle/hud/status.js:324
        private void SetReserve(bool likely)
        {
            if (_reserve == likely) return;
            _reserve = likely;
            Ui.Clear(_reserveBox);
            // §4.5 構えの盾: Guard-coloured, solid when 構え is expected, dashed when not.
            _reserveIcon = likely
                ? Ocb.Icon(_reserveBox, "shield", ReserveShield, DsTokens.Guard, DsTokens.Guard, 0.2f)
                : Ocb.Icon(_reserveBox, "shield", ReserveShield, DsTokens.Guard, dashed: true);
            float shield = _narrow == true ? 20f : ReserveShield; // .is-narrow .ocbg-battle-foe-st-shield .ocb-icon { 20 × 20 }
            Ui.Place(_reserveIcon.rectTransform, 0f, 0f, shield, shield);
        }

        // ---- HP -------------------------------------------------------------------------------------------------------

        // js: scenes/battle/hud/status.js:335
        private void SetHpKeepingTrailCore(int hp)
        {
            _hp = hp;
            Label.SetText(_hpNum, hp.ToString(Inv));
            _fill = WidthOf(hp);
            if (_trail < _fill) _trail = _fill; // a heal leaves no band behind
            DrawBar();
            DrawWick();
        }

        // js: scenes/battle/hud/status.js:343
        private void SnapTrailCore()
        {
            _trail = _fill;
            DrawBar();
        }

        // ---- the look the classes give -------------------------------------------------------------------------------

        /// <summary>The edge (inset: 0) as large as the panel, in its elite / focus look.</summary>
        private void SizeEdge()
        {
            UiNode.Of(_edge).Place(0f, 0f, _elNode.W, _elNode.H);
        }

        private void RestyleEdge()
        {
            Ocb.Restyle(_edge, EdgeCss + (_elite ? EdgeEliteCss : "") + (_focus ? EdgeFocusCss : ""));
        }

        /// <summary>
        /// The panel's opacity and filter: greyOut's inline values while it runs, else .is-down's (DownCss: --opacity-unplayed,
        /// grayscale(1)), else none.
        /// </summary>
        private void ApplyLook()
        {
            float op;
            ColorFilter f;
            if (_greyOpacity.HasValue)
            {
                op = _greyOpacity.Value;
                f = new ColorFilter(1f, _greyGray);
            }
            else if (_down)
            {
                op = DsTokens.OpacityUnplayed;
                f = new ColorFilter(1f, 1f);
            }
            else
            {
                op = 1f;
                f = ColorFilter.None;
            }
            if (!_el) return;
            _elNode.Opacity = op;
            if (!_elNode.Filter.Equals(f)) _elNode.Filter = f;
        }

        private void ApplyVisible()
        {
            // display: none (show) or visibility: hidden (setYielded): nothing drawn, nothing hovered
            if (_el) _elNode.Visible = _shown && !_yielded;
        }

        // ---- api ------------------------------------------------------------------------------------------------------

        /// <summary>el: the panel element (the hud fades it at the battle's end).</summary>
        // js: scenes/battle/hud/status.js:350
        public RectTransform El => _el;

        /// <summary>side.</summary>
        // js: scenes/battle/hud/status.js:351
        public UnitSide Side { get; }

        /// <summary>width.</summary>
        // js: scenes/battle/hud/status.js:352
        public float Width => _width;

        /// <summary>left: its left edge now (stage px).</summary>
        // js: scenes/battle/hud/status.js:353
        public float Left => _left;

        /// <summary>height: StatusPanelLayout.Height(chip rows).</summary>
        // js: scenes/battle/hud/status.js:354
        public float Height => SPL.Height(_rows);

        /// <summary>stamina: the stamina it shows.</summary>
        // js: scenes/battle/hud/status.js:355
        public int Stamina => _stamina;

        /// <summary>The HP the panel shows now (the bar's number).</summary>
        // js: scenes/battle/hud/status.js:357
        public int Hp => _hp;

        /// <summary>Shown and holding its place (a fallen enemy's panel that gave its place up to a standing one's is not).</summary>
        // js: scenes/battle/hud/status.js:359
        public bool Shown => _shown && !_yielded;

        /// <summary>The panel's box on the stage as it stands (from its layout numbers: no layout read, it is asked every frame).</summary>
        // js: scenes/battle/hud/status.js:361
        public PanelBox Box() => new PanelBox(_left, SPL.Top, _width, SPL.Height(_rows), _rows > 0 ? _chipsW : 0f);

        /// <summary>placeAt(left) = PlaceAt: the panel's top left at (left, StatusPanelLayout.Top).</summary>
        // js: scenes/battle/hud/status.js:364
        public void PlaceAt(float left)
        {
            if (_placed && Math.Abs(left - _left) < 0.05f) return;
            _placed = true;
            _left = left;
            // el.style.left = left.toFixed(1) + 'px'; el.style.top = SPL.Top + 'px'
            _elNode.Place(Fixed(left, 1), SPL.Top);
        }

        /// <summary>
        /// setSwatch(angle) (battle-visual §4.2 / §4.5, several enemies): the enemy's hatch swatch at the left end of its HP row,
        /// 16 px, before the heart (the heart, the number and the bar move right to make room); null takes it away.
        /// </summary>
        // js: scenes/battle/hud/status.js:376
        public void SetSwatch(float? angle)
        {
            if (_swatch == angle) return;
            _swatch = angle;
            if (_swatchEl != null)
            {
                _swatchEl.Remove();
                _swatchEl = null;
            }
            _hpShift = angle == null ? 0f : Swatch + PartGap;
            LayoutHpRow();
            if (angle == null) return;
            _swatchEl = Ocb.Swatch(_el, angle.Value, Swatch);
            _swatchEl.Root.gameObject.name = "ocb-swatch ocbg-battle-stat-swatch";
            _swatchEl.Place(SPL.PadSide, HpRowMid - Swatch / 2f);
        }

        /// <summary>setSolid(on): opaque (panel-solid) while another layer could show through (several enemies).</summary>
        // js: scenes/battle/hud/status.js:390
        public void SetSolid(bool on)
        {
            if (_solid == on) return;
            _solid = on;
            Ocb.Restyle(_el, StatCss + (on ? SolidCss : ""));
        }

        /// <summary>Review 19: the battle's focus (the amber edge) — the enemy a Tab at rest chose (the floor's N is read to it).</summary>
        // js: scenes/battle/hud/status.js:392
        public void SetFocus(bool on)
        {
            if (_focus == on) return;
            _focus = on;
            RestyleEdge();
        }

        /// <summary>show(on): shown or not (display).</summary>
        // js: scenes/battle/hud/status.js:393
        public void Show(bool on)
        {
            _shown = on;
            ApplyVisible();
            if (!on && _tip != null) foreach (ChipView v in _chipViews) _tip.Hide(v);
        }

        /// <summary>
        /// setYielded(on) (several enemies, review fix): a fallen enemy's grey panel gives its place up — hidden — while a standing
        /// enemy's panel (or the hero's) stands over it; it comes back when that one moves on.
        /// </summary>
        // js: scenes/battle/hud/status.js:402
        public void SetYielded(bool on)
        {
            if (_yielded == on) return;
            _yielded = on;
            ApplyVisible();
            if (on && _tip != null) foreach (ChipView v in _chipViews) _tip.Hide(v);
        }

        /// <summary>battle_ui_ux_v2 §2.2.6: a fallen enemy's panel stays, grey (a greyOut under way ends here).</summary>
        // js: scenes/battle/hud/status.js:411
        public void SetDown(bool on)
        {
            if (_greying)
            {
                // a greyOut under way ends here: its inline look gives way to the class's
                _greying = false;
                _greyToken++;
                _greyOpacity = null;
                _greyGray = 0f;
                ApplyLook();
            }
            if (_down == on) return;
            _down = on;
            ApplyLook();
            Ocb.Restyle(_fillEl, BarPartCss + BarFillCss + (on ? DownFillCss : ""));
            if (on) ShowHpLoss(0);
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.15 順 5 (several enemies; review fix: the panel turned grey at once): the panel turns grey over `ms`
        /// (linear) — to the fallen look's opacity and grey — and is then down (setDown).
        /// </summary>
        // js: scenes/battle/hud/status.js:429
        public Promise GreyOut(float ms)
        {
            if (_down) return Promise.Resolved();
            if (!(ms > 0f))
            {
                SetDown(true);
                return Promise.Resolved();
            }
            int mine = ++_greyToken;
            _greying = true;
            // parseFloat(getComputedStyle(el).getPropertyValue('--opacity-unplayed')) || 0.55
            float to = DsTokens.OpacityUnplayed;
            ShowHpLoss(0);
            return Clock.Run(ms, Ease.Linear, t =>
            {
                if (_greyToken != mine) return;
                _greyOpacity = Fixed(HudUtil.Lerp(1f, to, t), 3);
                _greyGray = Fixed(t, 3);
                ApplyLook();
            }).Then(() =>
            {
                if (_greyToken == mine) SetDown(true);
            });
        }

        /// <summary>bind(unit) = StatusBarView.Bind: HP max and ticks, HP, Guard, the stamina group, elite, the chips (no stances).</summary>
        // js: scenes/battle/hud/status.js:443
        public void Bind(UnitFrame unit)
        {
            if (unit == null) return;
            _hpMax = Math.Max(1, unit.HpMax);
            DrawTicks();
            SetHp(unit.Hp);
            SetGuardNow(unit.Guard);
            UiNode.Of(_stGroup).Visible = unit.ShowStamina && _foe;
            SetReserve(unit.ReserveLikely);
            if (_elite != unit.Elite)
            {
                _elite = unit.Elite;
                RestyleEdge(); // el.classList.toggle('is-elite', st.elite)
            }
            if (unit.ShowStamina) SetStamina(unit.Stamina, unit.StaminaMax);
            // battle-visual-v1 §4.6 (v1.5, #505): a stance is not a chip here — its frame in the stance list alone carries "×n"
            // and 永続, so stances never push 出血 or 鈍足 into "+n".
            var list = new List<StatusChip>();
            if (unit.Statuses != null)
                foreach (StatusChip c in unit.Statuses)
                {
                    if (c == null) continue;
                    StatusChip copy = CopyChip(c);
                    if (copy.Kind != ChipKind.Lasting) list.Add(copy);
                }
            _chips = OrderChips(list);
            RenderChips(false);
        }

        /// <summary>setGuard(n): the Guard number at once (n &lt; 0 ignored).</summary>
        // js: scenes/battle/hud/status.js:460
        public void SetGuard(int n)
        {
            if (n >= 0) SetGuardNow(n);
        }

        /// <summary>StatusBarView.PopGuard: the badge pops (UiTween.Pop) with the new number.</summary>
        // js: scenes/battle/hud/status.js:462
        public Promise PopGuard(int n, float ms = 260f)
        {
            if (n >= 0) SetGuardNow(n);
            return HudUtil.Pop(Clock, UiNode.Of(_guardBadge), ms);
        }

        /// <summary>StatusBarView.CrackGuard: the shield flashes white and the badge shakes as it breaks.</summary>
        // js: scenes/battle/hud/status.js:467
        public Promise CrackGuard(float ms)
        {
            UiNode icon = UiNode.Of(_guardIconBox);
            Clock.Run(ms, Ease.Out, t =>
            {
                if (!icon) return;
                icon.Filter = t < 1f ? new ColorFilter(Fixed(HudUtil.Lerp(2.4f, 1f, t), 3), 0f) : ColorFilter.None;
            }).Forget();
            return HudUtil.Shake(Clock, UiNode.Of(_guardBadge), 7f, 2f, ms);
        }

        /// <summary>setHp(hp): the bar and its trail at once.</summary>
        // js: scenes/battle/hud/status.js:471
        public void SetHp(int hp)
        {
            SetHpKeepingTrailCore(hp);
            SnapTrailCore();
        }

        /// <summary>StatusBarView.AnimateHp: the number lands at once; the bar slides (Ease.Out).</summary>
        // js: scenes/battle/hud/status.js:473
        public Promise AnimateHp(int hp, float ms)
        {
            _hp = hp;
            Label.SetText(_hpNum, hp.ToString(Inv));
            DrawWick();
            float from = _fill;
            float to = WidthOf(hp);
            if (_trail < to) _trail = to;
            return Clock.Run(ms, Ease.Out, t =>
            {
                _fill = HudUtil.Lerp(from, to, t);
                if (_el) DrawBar();
            });
        }

        /// <summary>StatusBarView.AnimateTrail: the grey band catches up with the bar (Ease.InOut).</summary>
        // js: scenes/battle/hud/status.js:483
        public Promise AnimateTrail(float ms)
        {
            float from = _trail;
            float to = _fill;
            return Clock.Run(ms, Ease.InOut, t =>
            {
                _trail = HudUtil.Lerp(from, to, t);
                if (_el) DrawBar();
            });
        }

        /// <summary>snapTrail(): the grey band onto the bar at once.</summary>
        // js: scenes/battle/hud/status.js:488
        public void SnapTrail() => SnapTrailCore();

        /// <summary>setHpKeepingTrail(hp): the number and the bar at once, the grey band where it was.</summary>
        // js: scenes/battle/hud/status.js:489
        public void SetHpKeepingTrail(int hp) => SetHpKeepingTrailCore(hp);

        /// <summary>StatusBarView.ShowHpLoss: the wick stripes (HpWickSpec.Span); 0 takes them away. An enemy's panel only.</summary>
        // js: scenes/battle/hud/status.js:491
        public void ShowHpLoss(int n)
        {
            if (n == _loss) return;
            _loss = n;
            DrawWick();
        }

        /// <summary>StatusBarView.SetStatus: one word's chip settled at once — added, restacked, or gone at 0.</summary>
        // js: scenes/battle/hud/status.js:498
        public void SetStatus(string label, int stacks)
        {
            label = label ?? "";
            int i = _chips.FindIndex(c => c.Label == label);
            if (stacks <= 0)
            {
                if (i >= 0) _chips.RemoveAt(i);
            }
            else if (i >= 0)
            {
                // Object.assign({}, st.chips[i], { Stacks: stacks }): a new chip, the view's old one untouched
                StatusChip was = _chips[i];
                var copy = new StatusChip
                {
                    Label = was.Label, Stacks = stacks, Kind = was.Kind, Title = was.Title, Group = was.Group, Effect = was.Effect, Decay = was.Decay,
                };
                if (copy.Kind != ChipKind.Lasting) copy.Title = label + " " + stacks.ToString(Inv);
                _chips[i] = copy;
            }
            else
            {
                // The cue names the word only; the frame that settles the event brings its panel text. The corner is read off the
                // design system's status table until then (Unity draws the default, square).
                HudStatusIcon? known = HudUtil.StatusIconOf(label);
                _chips.Add(new StatusChip
                {
                    Label = label,
                    Stacks = stacks,
                    Kind = known.HasValue && known.Value.Self ? ChipKind.OnSelf : ChipKind.OnFoe,
                    Title = label + " " + stacks.ToString(Inv),
                    Group = "",
                    Effect = "",
                    Decay = "",
                });
            }
            _chips = OrderChips(_chips);
            RenderChips(false);
        }

        /// <summary>StatusBarView.PlayStatus: StatusApply swells in, StatusStack pops, StatusTick shrinks for a moment, StatusVanish fades.</summary>
        // js: scenes/battle/hud/status.js:515
        public Promise PlayStatus(string label, int stacks, EffectId beat, float ms)
        {
            if (beat == EffectId.StatusVanish)
            {
                ChipView going = ChipOf(label);
                Promise done;
                if (going != null)
                {
                    UiNode gn = going.Node;
                    done = Clock.Run(ms, Ease.Linear, t =>
                    {
                        if (!gn) return;
                        gn.Opacity = Fixed(1f - t, 3);
                        gn.PropScale = Fixed(HudUtil.Lerp(1f, 0.7f, t), 3);
                    });
                }
                else done = Promise.Resolved();
                return done.Then(() => SetStatus(label, 0));
            }
            SetStatus(label, stacks);
            ChipView shown = ChipOf(label);
            if (shown == null) return Clock.Wait(ms);
            UiNode chip = shown.Node;
            Promise run;
            if (beat == EffectId.StatusApply)
            {
                run = Clock.Run(ms, Ease.Out, t =>
                {
                    float s = t < 0.6f ? HudUtil.Lerp(0.3f, 1.2f, t / 0.6f) : HudUtil.Lerp(1.2f, 1f, (t - 0.6f) / 0.4f);
                    if (chip) chip.PropScale = Fixed(s, 3);
                });
            }
            else if (beat == EffectId.StatusStack)
            {
                run = HudUtil.Pop(Clock, chip, ms);
            }
            else
            {
                run = Clock.Run(ms, Ease.Out, t =>
                {
                    if (chip) chip.PropScale = Fixed(1f - 0.18f * (float)Math.Sin(t * Math.PI), 3);
                });
            }
            return run.Then(() =>
            {
                if (chip) chip.PropScale = 1f; // chip.style.scale = ''
            });
        }

        /// <summary>StatusBarView.SetStamina (the player's is handed on to the stamina tag: StaminaShown).</summary>
        // js: scenes/battle/hud/status.js:541
        public void SetStamina(int stamina, int max)
        {
            _stamina = stamina;
            Label.SetText(_stNum, stamina.ToString(Inv));
            LayoutStGroup();
            _deps.OnStamina?.Invoke(stamina, max);
        }

        /// <summary>StatusBarView.LightPips: the recovered points light one at a time, perPipMs apart.</summary>
        // js: scenes/battle/hud/status.js:547
        public async Promise LightPips(int stamina, int max, float perPipMs)
        {
            int from = _stamina < 0 ? 0 : _stamina > max ? max : _stamina;
            if (stamina <= from)
            {
                SetStamina(stamina, max);
                return;
            }
            SetStamina(from, max);
            for (int next = from + 1; next <= stamina; next++)
            {
                await Clock.Wait(perPipMs);
                SetStamina(next, max);
                if (_deps.OnPip != null) _deps.OnPip(next - 1, perPipMs);
                else if (_foe) HudUtil.Pop(Clock, UiNode.Of(_stNum.rectTransform), perPipMs).Forget();
            }
        }

        /// <summary>A spend pops beside the card's beats (Unity pops the pip row it has hidden; here the number shown).</summary>
        // js: scenes/battle/hud/status.js:559
        public Promise PopPips(float ms)
        {
            if (_deps.OnStaminaPop != null) return _deps.OnStaminaPop(ms);
            return HudUtil.Pop(Clock, UiNode.Of(_stNum.rectTransform), ms);
        }

        /// <summary>destroy(): hover-out and removal.</summary>
        // js: scenes/battle/hud/status.js:564
        public void Destroy()
        {
            if (_tip != null) foreach (ChipView v in _chipViews) _tip.Hide(v);
            if (_el) Ui.Remove(_el);
        }

        // ---- numbers --------------------------------------------------------------------------------------------------

        /// <summary>Number.prototype.toFixed(digits) read back as a number (the style string the JS writes).</summary>
        private static float Fixed(float x, int digits) => (float)Math.Round((double)x, digits, MidpointRounding.AwayFromZero);

        /// <summary>Math.round (halves up).</summary>
        private static int JsRound(float x) => (int)Math.Floor(x + 0.5f);
    }
}
