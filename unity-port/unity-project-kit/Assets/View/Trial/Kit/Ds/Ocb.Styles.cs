// bundle.css (vendor/ds/bundle.css) of the design system: every `.ocb-root .ocb-*` rule (lines 3-206) as a named
// declaration block, the CSS text copied verbatim and in file order, so a reviewer can diff this file against the
// stylesheet line by line (generated from bundle.css: the text between the braces, nothing changed). The builders of
// Ocb.*.cs compose these in cascade order (the base rule, then the state rules in stylesheet order) and parse them with
// Trial.Geometry's Css (var(), the tokens, shorthands). The prototype-only rules after line 206 (the .ocb-game backdrop,
// the lab, the gallery) are not ported (kit.md §2.0); the guide line of line 166 is kept for reference.
namespace Trial.Kit
{
    public static partial class Ocb
    {
        // ---- shared rules ----------------------------------------------------------------------------------------

        /// <summary>bundle.css:3 <c>.ocb-root, .ocb-root *, .ocb-root *::before, .ocb-root *::after</c></summary>
        public const string BoxSizingCss = "box-sizing: border-box;";

        /// <summary>bundle.css:4 <c>.ocb-root</c></summary>
        public const string RootCss = "font-family: var(--font-body); color: var(--ink); -webkit-font-smoothing: antialiased; line-height: 1.375;";

        /// <summary>bundle.css:5 <c>.ocb-root p</c></summary>
        public const string PCss = "margin: 0;";

        /// <summary>bundle.css:6 <c>.ocb-mono</c></summary>
        public const string MonoCss = "font-family: var(--font-mono); font-variant-numeric: tabular-nums;";

        /// <summary>bundle.css:7 <c>.ocb-disp</c></summary>
        public const string DispCss = "font-family: var(--font-display);";

        /// <summary>bundle.css:8 <c>.ocb-icon</c></summary>
        public const string IconCss = "display: inline-block; flex: none; overflow: visible; vertical-align: middle;";

        // ---- panels ----------------------------------------------------------------------------------------------

        /// <summary>bundle.css:11 <c>.ocb-root .ocb-panel</c></summary>
        public const string PanelCss = "background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel);";

        /// <summary>bundle.css:12 <c>.ocb-root .ocb-solid</c></summary>
        public const string SolidCss = "background: var(--panel-solid); border: var(--stroke-rule) solid var(--steel); border-radius: var(--radius-panel);";

        // ---- card (216×304) --------------------------------------------------------------------------------------

        /// <summary>bundle.css:15 <c>.ocb-root .ocb-card</c></summary>
        public const string CardCss = "position: relative; width: 216px; height: 304px; border-radius: var(--radius-panel); background: var(--card); box-shadow: var(--shadow-card); --attr: var(--steel); --attr-ink: var(--ink); user-select: none;";

        /// <summary>bundle.css:16 <c>.ocb-root .ocb-card.is-attack</c></summary>
        public const string CardAttackCss = "--attr: var(--violet); --attr-ink: var(--phosphor);";

        /// <summary>bundle.css:17 <c>.ocb-root .ocb-card.is-defense</c></summary>
        public const string CardDefenseCss = "--attr: var(--guard); --attr-ink: var(--guard);";

        /// <summary>bundle.css:18 <c>.ocb-root .ocb-card.is-skill</c></summary>
        public const string CardSkillCss = "--attr: var(--amber); --attr-ink: var(--amber);";

        /// <summary>bundle.css:19 <c>.ocb-root .ocb-card.is-stance</c></summary>
        public const string CardStanceCss = "--attr: var(--crimson); --attr-ink: var(--hp-text);";

        /// <summary>bundle.css:20 <c>.ocb-root .ocb-card-clip</c></summary>
        public const string CardClipCss = "position: absolute; inset: 0; border-radius: var(--radius-panel); overflow: hidden; pointer-events: none;";

        /// <summary>bundle.css:21 <c>.ocb-root .ocb-card-clip::before</c></summary>
        public const string CardClipBeforeCss = "content: \"\"; position: absolute; left: 0; top: 0; bottom: 0; width: 8px; background: var(--attr);";

        /// <summary>bundle.css:22 <c>.ocb-root .ocb-card-clip::after</c></summary>
        public const string CardClipAfterCss = "content: \"\"; position: absolute; left: 0; top: 0; right: 0; height: 5px; background: var(--attr);";

        /// <summary>bundle.css:23 <c>.ocb-root .ocb-card-edge</c></summary>
        public const string CardEdgeCss = "position: absolute; inset: 0; border: var(--stroke-frame) solid var(--attr); border-radius: var(--radius-panel); pointer-events: none;";

        /// <summary>bundle.css:24 <c>.ocb-root .ocb-card.is-lit .ocb-card-edge</c></summary>
        public const string CardLitEdgeCss = "border-color: var(--wick);";

        /// <summary>bundle.css:25 <c>.ocb-root .ocb-card.is-lit</c></summary>
        public const string CardLitCss = "box-shadow: var(--shadow-card), var(--shadow-lamp);";

        /// <summary>bundle.css:26 <c>.ocb-root .ocb-cost</c></summary>
        public const string CostCss = "position: absolute; left: 16px; top: 11px; width: 44px; height: 44px; border-radius: 50%; border: var(--stroke-frame) solid var(--amber); background: rgba(0,0,0,.45); display: flex; align-items: center; justify-content: center; font: 700 28px/1 var(--font-mono); color: var(--amber);";

        /// <summary>bundle.css:27 <c>.ocb-root .ocb-cost.is-down</c></summary>
        public const string CostDownCss = "background: var(--amber); color: var(--ground);";

        /// <summary>bundle.css:28 <c>.ocb-root .ocb-cost .ocb-down</c></summary>
        public const string CostDownTagCss = "position: absolute; left: 50%; transform: translateX(-50%); bottom: -9px; height: 18px; padding: 0 4px; border-radius: 3px; background: var(--panel-solid); border: 1px solid var(--amber); font: 400 14px/16px var(--font-body); color: var(--ink); white-space: nowrap;";

        /// <summary>bundle.css:29 <c>.ocb-root .ocb-attr-icon</c></summary>
        public const string AttrIconCss = "position: absolute; left: 68px; top: 21px;";

        /// <summary>bundle.css:30 <c>.ocb-root .ocb-reach</c></summary>
        public const string ReachCss = "position: absolute; right: 12px; top: 11px; height: 32px; padding: 0 8px; display: flex; align-items: center; gap: 4px; background: var(--panel-solid); border: 1px solid var(--steel); border-radius: var(--radius-tag); font: 700 20px/1 var(--font-mono); color: var(--ink); white-space: nowrap;";

        /// <summary>bundle.css:31 <c>.ocb-root .ocb-reach small</c></summary>
        public const string ReachSmallCss = "font: 400 14px/1 var(--font-body);";

        /// <summary>bundle.css:32 <c>.ocb-root .ocb-card-name</c></summary>
        public const string CardNameCss = "position: absolute; left: 16px; top: 60px; right: 12px; font: 700 22px/28px var(--font-display); letter-spacing: .02em; white-space: nowrap;";

        /// <summary>bundle.css:33 <c>.ocb-root .ocb-values</c></summary>
        public const string ValuesCss = "position: absolute; left: 16px; top: 94px; display: flex; gap: 14px;";

        /// <summary>bundle.css:34 <c>.ocb-root .ocb-val</c></summary>
        public const string ValCss = "display: grid; grid-template-columns: 24px auto; column-gap: 4px; align-items: center;";

        /// <summary>bundle.css:35 <c>.ocb-root .ocb-val b</c></summary>
        public const string ValBCss = "font: 700 36px/40px var(--font-mono); color: var(--val-ink, var(--attr-ink));";

        /// <summary>bundle.css:36 <c>.ocb-root .ocb-val small</c></summary>
        public const string ValSmallCss = "grid-column: 2; font: 400 16px/18px var(--font-body); color: var(--ink-muted); margin-top: -2px;";

        /// <summary>bundle.css:37 <c>.ocb-root .ocb-desc</c></summary>
        public const string DescCss = "position: absolute; left: 16px; top: 156px; width: 188px; height: 66px; font: 400 16px/22px var(--font-body); color: var(--ink); line-break: strict; word-break: normal; overflow-wrap: anywhere;";

        /// <summary>bundle.css:38 <c>.ocb-root .ocb-card.is-stance .ocb-desc</c></summary>
        public const string StanceDescCss = "top: 94px; height: 88px;";

        /// <summary>bundle.css:39 <c>.ocb-root .ocb-desc .ocb-inf</c></summary>
        public const string DescInfCss = "display: flex; gap: 6px; align-items: center; height: 22px; white-space: nowrap;";

        /// <summary>bundle.css:40 <c>.ocb-root .ocb-desc.fit-unity</c></summary>
        public const string DescFitUnityCss = "white-space: nowrap; overflow: visible;";

        /// <summary>bundle.css:41 <c>.ocb-root .ocb-desc.fit-clip</c></summary>
        public const string DescFitClipCss = "overflow: hidden;";

        /// <summary>bundle.css:42 <c>.ocb-root .ocb-desc .ocb-more</c></summary>
        public const string DescMoreCss = "color: var(--ink-muted);";

        /// <summary>bundle.css:43 <c>.ocb-root .ocb-trait</c></summary>
        public const string TraitCss = "position: absolute; left: 12px; right: 7px; bottom: 7px; height: 62px; background: rgba(0,0,0,.34); border: 1px solid var(--rule); border-radius: var(--radius-tag); display: flex; align-items: center; padding: 0 4px 0 0;";

        /// <summary>bundle.css:44 <c>.ocb-root .ocb-trait.is-plain</c></summary>
        public const string TraitPlainCss = "border-style: dashed; border-color: var(--steel); color: var(--ink-muted); padding-left: 12px;";

        /// <summary>bundle.css:45 <c>.ocb-root .ocb-trait .ocb-lamp-slot</c></summary>
        public const string TraitLampSlotCss = "width: 26px; display: flex; justify-content: center; flex: none;";

        /// <summary>bundle.css:46 <c>.ocb-root .ocb-trait p</c></summary>
        public const string TraitPCss = "font: 400 16px/22px var(--font-body); white-space: nowrap;";

        /// <summary>bundle.css:47 <c>.ocb-root .ocb-trait p + p</c></summary>
        public const string TraitPPCss = "font-weight: 700;";

        /// <summary>bundle.css:48 <c>.ocb-root .ocb-lamp</c></summary>
        public const string LampCss = "width: 10px; height: 10px; border-radius: 50%; background: var(--steel);";

        /// <summary>bundle.css:49 <c>.ocb-root .ocb-lamp.is-on</c></summary>
        public const string LampOnCss = "width: 16px; height: 16px; background: var(--wick); box-shadow: 0 0 10px rgba(255,216,154,.8);";

        /// <summary>bundle.css:50 <c>.ocb-root .ocb-card-dim</c></summary>
        public const string CardDimCss = "position: absolute; inset: 0; border-radius: var(--radius-panel); background: #000; opacity: 0; pointer-events: none;";

        /// <summary>bundle.css:51 <c>.ocb-root .ocb-card.is-unpayable .ocb-card-dim</c></summary>
        public const string CardUnpayableDimCss = "opacity: var(--opacity-unpayable);";

        /// <summary>bundle.css:52 <c>.ocb-root .ocb-term</c></summary>
        public const string TermCss = "text-decoration: underline dotted var(--ink-muted) 2px; text-underline-offset: 4px;";

        // ---- omen plate (200×98) ---------------------------------------------------------------------------------

        /// <summary>bundle.css:55 <c>.ocb-root .ocb-omen</c></summary>
        public const string OmenCss = "position: relative; width: 200px; height: 98px; background: var(--panel-solid); border: var(--stroke-frame) solid var(--violet); border-radius: var(--radius-panel); padding: 8px 12px; display: flex; flex-direction: column; justify-content: space-between; transition: box-shadow .12s;";

        /// <summary>bundle.css:56 <c>.ocb-root .ocb-omen.is-active</c></summary>
        public const string OmenActiveCss = "border-color: var(--phosphor); box-shadow: var(--shadow-omen-active);";

        /// <summary>bundle.css:57 <c>.ocb-root .ocb-omen.is-rest</c></summary>
        public const string OmenRestCss = "border: var(--stroke-frame) dashed var(--steel);";

        /// <summary>bundle.css:58 <c>.ocb-root .ocb-omen-row1</c></summary>
        public const string OmenRow1Css = "display: flex; align-items: center; gap: 8px; height: 52px;";

        /// <summary>bundle.css:59 <c>.ocb-root .ocb-omen-row1 b</c></summary>
        public const string OmenRow1BCss = "font: 700 48px/52px var(--font-mono);";

        /// <summary>bundle.css:60 <c>.ocb-root .ocb-omen-row1 .ocb-swatch</c></summary>
        public const string OmenRow1SwatchCss = "margin-left: auto;";

        /// <summary>bundle.css:61 <c>.ocb-root .ocb-omen-row2</c></summary>
        public const string OmenRow2Css = "display: flex; align-items: center; justify-content: space-between; height: 26px; font: 400 16px/22px var(--font-body);";

        /// <summary>bundle.css:62 <c>.ocb-root .ocb-hitmark</c></summary>
        public const string HitmarkCss = "width: 30px; height: 26px; display: flex; align-items: center; justify-content: center; border-radius: 4px; position: relative;";

        /// <summary>bundle.css:63 <c>.ocb-root .ocb-hitmark.is-hit</c></summary>
        public const string HitmarkHitCss = "border: 2px solid var(--phosphor);";

        /// <summary>bundle.css:64 <c>.ocb-root .ocb-hitmark.is-miss</c></summary>
        public const string HitmarkMissCss = "border: 2px dashed var(--miss);";

        /// <summary>bundle.css:65 <c>.ocb-root .ocb-hitmark.is-miss::after</c></summary>
        public const string HitmarkMissAfterCss = "content: \"\"; position: absolute; left: 2px; right: 2px; top: 50%; height: 2px; background: var(--miss); transform: rotate(-24deg);";

        /// <summary>bundle.css:66 <c>.ocb-root .ocb-omen-reserve</c></summary>
        public const string OmenReserveCss = "width: 170px; height: 66px; opacity: .62; border: var(--stroke-frame) dashed var(--ink-muted); background: var(--panel-solid); border-radius: var(--radius-panel); padding: 6px 10px; display: flex; align-items: center; gap: 8px; position: relative;";

        /// <summary>bundle.css:67 <c>.ocb-root .ocb-omen-reserve b</c></summary>
        public const string OmenReserveBCss = "font: 700 28px/1 var(--font-mono);";

        /// <summary>bundle.css:68 <c>.ocb-root .ocb-omen-reserve .ocb-plan</c></summary>
        public const string OmenReservePlanCss = "position: absolute; right: 6px; bottom: 6px; font: 400 14px/16px var(--font-body); border: 1px dashed var(--ink-muted); border-radius: 3px; padding: 0 4px;";

        // ---- floor -----------------------------------------------------------------------------------------------

        /// <summary>bundle.css:71 <c>.ocb-root .ocb-tile</c></summary>
        public const string TileCss = "position: absolute; height: 40px; border-radius: var(--radius-panel); background: var(--floor-tile); border: var(--stroke-rule) solid var(--rule);";

        /// <summary>bundle.css:72 <c>.ocb-root .ocb-tile-reach</c></summary>
        public const string TileReachCss = "position: absolute; inset: 0; border-radius: var(--radius-panel); background: linear-gradient(rgba(232,168,76,.55), rgba(232,168,76,.18)); box-shadow: var(--shadow-reach); opacity: 0;";

        /// <summary>bundle.css:73 <c>.ocb-root .ocb-tile-reach-line</c></summary>
        public const string TileReachLineCss = "position: absolute; left: 0; right: 0; top: 0; height: 3px; border-radius: 3px 3px 0 0; background: var(--amber); opacity: 0;";

        /// <summary>bundle.css:74 <c>.ocb-root .ocb-tile-threat</c></summary>
        public const string TileThreatCss = "position: absolute; inset: -1px; border-radius: var(--radius-panel); border: var(--stroke-frame) solid var(--reach-red); box-shadow: var(--shadow-threat); overflow: hidden;";

        /// <summary>bundle.css:75 <c>.ocb-root .ocb-tile-threat svg</c></summary>
        public const string TileThreatSvgCss = "position: absolute; inset: 0;";

        /// <summary>bundle.css:76 <c>.ocb-root .ocb-tile-move</c></summary>
        public const string TileMoveCss = "position: absolute; inset: -1px; border-radius: var(--radius-panel); border: var(--stroke-frame) dashed var(--violet);";

        /// <summary>bundle.css:77 <c>.ocb-root .ocb-dist</c></summary>
        public const string DistCss = "position: absolute; height: 34px; display: flex; align-items: center; justify-content: center;";

        /// <summary>bundle.css:78 <c>.ocb-root .ocb-dist-chip</c></summary>
        public const string DistChipCss = "position: relative; z-index: 1; height: 34px; padding: 0 10px; display: flex; align-items: center; gap: 6px; background: var(--panel-solid); border: 1px solid var(--steel); border-radius: var(--radius-panel); font: 700 28px/1 var(--font-mono);";

        /// <summary>bundle.css:79 <c>.ocb-root .ocb-dist-arm</c></summary>
        public const string DistArmCss = "position: absolute; top: 50%; height: 12px; margin-top: -6px; border: 1px solid rgba(236,235,242,.55); border-right: 0; border-left-width: 1px;";

        // ---- figures ---------------------------------------------------------------------------------------------

        /// <summary>bundle.css:82 <c>.ocb-root .ocb-figure</c></summary>
        public const string FigureCss = "position: absolute;";

        /// <summary>bundle.css:83 <c>.ocb-root .ocb-footlight</c></summary>
        public const string FootLightCss = "position: absolute; height: 60px; border-radius: 50%;";

        // ---- status panel ----------------------------------------------------------------------------------------

        /// <summary>bundle.css:86 <c>.ocb-root .ocb-stat</c></summary>
        public const string StatCss = "position: relative; padding: 8px 10px 10px; display: flex; flex-direction: column; gap: 8px;";

        /// <summary>bundle.css:87 <c>.ocb-root .ocb-stat.is-elite</c></summary>
        public const string StatEliteCss = "border-color: var(--phosphor);";

        /// <summary>bundle.css:88 <c>.ocb-root .ocb-hp</c></summary>
        public const string HpCss = "display: flex; align-items: center; gap: 6px; height: 28px;";

        /// <summary>bundle.css:89 <c>.ocb-root .ocb-hp b</c></summary>
        public const string HpBCss = "font: 700 24px/28px var(--font-mono); min-width: 40px; text-align: right;";

        /// <summary>bundle.css:90 <c>.ocb-root .ocb-bar</c></summary>
        public const string BarCss = "position: relative; flex: 1; height: 14px; border-radius: 3px; background: rgba(0,0,0,.5); border: 1px solid var(--rule); overflow: hidden;";

        /// <summary>bundle.css:91 <c>.ocb-root .ocb-bar-fill</c></summary>
        public const string BarFillCss = "position: absolute; left: 0; top: 0; bottom: 0; background: var(--crimson); transition: width .3s ease-out;";

        /// <summary>bundle.css:92 <c>.ocb-root .ocb-bar-loss</c></summary>
        public const string BarLossCss = "position: absolute; top: 0; bottom: 0; background: repeating-linear-gradient(135deg, var(--wick) 0 2px, transparent 2px 5px);";

        /// <summary>bundle.css:93 <c>.ocb-root .ocb-bar-ticks</c></summary>
        public const string BarTicksCss = "position: absolute; inset: 0;";

        /// <summary>bundle.css:94 <c>.ocb-root .ocb-bar-ticks i</c></summary>
        public const string BarTickCss = "position: absolute; top: 0; bottom: 0; width: 2px; background: rgba(0,0,0,.55);";

        /// <summary>bundle.css:95 <c>.ocb-root .ocb-guardrow</c></summary>
        public const string GuardRowCss = "display: flex; align-items: center; gap: 6px; height: 30px;";

        /// <summary>bundle.css:96 <c>.ocb-root .ocb-guardrow b</c></summary>
        public const string GuardRowBCss = "font: 700 24px/28px var(--font-mono); color: var(--guard);";

        /// <summary>bundle.css:97 <c>.ocb-root .ocb-guardrow.is-zero</c></summary>
        public const string GuardRowZeroCss = "opacity: var(--opacity-guard-zero);";

        /// <summary>bundle.css:98 <c>.ocb-root .ocb-guardrow.is-zero b</c></summary>
        public const string GuardRowZeroBCss = "color: var(--ink-muted);";

        /// <summary>bundle.css:99 <c>.ocb-root .ocb-foe-st</c></summary>
        public const string FoeStCss = "margin-left: auto; display: flex; align-items: center; gap: 4px;";

        /// <summary>bundle.css:100 <c>.ocb-root .ocb-foe-st b</c></summary>
        public const string FoeStBCss = "color: var(--ink);";

        /// <summary>bundle.css:101 <c>.ocb-root .ocb-chips</c></summary>
        public const string ChipsCss = "display: flex; flex-wrap: wrap; gap: 5px; min-height: 0;";

        /// <summary>bundle.css:102 <c>.ocb-root .ocb-chips:empty</c></summary>
        public const string ChipsEmptyCss = "display: none;";

        // ---- status chip + tooltip -------------------------------------------------------------------------------

        /// <summary>bundle.css:105 <c>.ocb-root .ocb-chip</c></summary>
        public const string ChipCss = "height: 30px; padding: 0 8px 0 6px; display: inline-flex; align-items: center; gap: 4px; background: rgba(0,0,0,.35); border: 1px solid var(--ink-muted); border-radius: var(--radius-chip-foe); font: 400 16px/1 var(--font-body); white-space: nowrap; cursor: default;";

        /// <summary>bundle.css:106 <c>.ocb-root .ocb-chip.is-self</c></summary>
        public const string ChipSelfCss = "border-color: var(--steel); border-radius: var(--radius-chip-self);";

        /// <summary>bundle.css:107 <c>.ocb-root .ocb-chip.is-boss</c></summary>
        public const string ChipBossCss = "border: 3px double var(--boss);";

        /// <summary>bundle.css:108 <c>.ocb-root .ocb-chip:hover, .ocb-root .ocb-chip.is-hover</c></summary>
        public const string ChipHoverCss = "border-color: var(--ink); background: rgba(255,255,255,.1);";

        /// <summary>bundle.css:109 <c>.ocb-root .ocb-chip .ocb-mono</c></summary>
        public const string ChipMonoCss = "font-weight: 600;";

        /// <summary>bundle.css:110 <c>.ocb-root .ocb-tip</c></summary>
        public const string TipCss = "width: 340px; padding: 10px 12px 12px; background: var(--panel-solid); border: 1px solid var(--steel); border-radius: var(--radius-panel); box-shadow: var(--shadow-tip);";

        /// <summary>bundle.css:111 <c>.ocb-root .ocb-tip-head</c></summary>
        public const string TipHeadCss = "display: flex; align-items: center; gap: 6px; font: 700 18px/24px var(--font-body); margin-bottom: 6px;";

        /// <summary>bundle.css:112 <c>.ocb-root .ocb-tip-head small</c></summary>
        public const string TipHeadSmallCss = "margin-left: auto; font: 400 14px/18px var(--font-body); color: var(--ink-muted);";

        /// <summary>bundle.css:113 <c>.ocb-root .ocb-tip p</c></summary>
        public const string TipPCss = "font: 400 16px/22px var(--font-body);";

        /// <summary>bundle.css:114 <c>.ocb-root .ocb-tip p span</c></summary>
        public const string TipPSpanCss = "color: var(--ink-muted); margin-right: 8px;";

        // ---- stance slot (250×52) --------------------------------------------------------------------------------

        /// <summary>bundle.css:117 <c>.ocb-root .ocb-stance</c></summary>
        public const string StanceCss = "position: relative; width: 250px; height: 52px; padding: 0 12px; display: flex; align-items: center; gap: 8px; background: var(--panel); border: var(--stroke-frame) solid var(--hp-text); border-radius: var(--radius-panel); font: 700 20px/26px var(--font-display);";

        /// <summary>bundle.css:118 <c>.ocb-root .ocb-stance.is-foe</c></summary>
        public const string StanceFoeCss = "border-color: var(--violet);";

        /// <summary>bundle.css:119 <c>.ocb-root .ocb-stance.is-empty</c></summary>
        public const string StanceEmptyCss = "background: none; border: var(--stroke-frame) dashed var(--steel);";

        /// <summary>bundle.css:120 <c>.ocb-root .ocb-stance .ocb-x</c></summary>
        public const string StanceXCss = "margin-left: auto; width: 40px; text-align: right; font: 600 16px/1 var(--font-mono);";

        /// <summary>bundle.css:121 <c>.ocb-root .ocb-stance.is-hover</c></summary>
        public const string StanceHoverCss = "background: rgba(40,44,58,.92);";

        // ---- stamina plate + piles -------------------------------------------------------------------------------

        /// <summary>bundle.css:124 <c>.ocb-root .ocb-stamina</c></summary>
        public const string StaminaCss = "position: relative; width: 236px; height: 84px; padding: 10px 12px; background: var(--card); border: 1px solid var(--steel); border-radius: var(--radius-plate);";

        /// <summary>bundle.css:125 <c>.ocb-root .ocb-st-row1</c></summary>
        public const string StRow1Css = "display: flex; align-items: center; gap: 8px; height: 36px;";

        /// <summary>bundle.css:126 <c>.ocb-root .ocb-st-row1 b</c></summary>
        public const string StRow1BCss = "font: 700 34px/36px var(--font-mono); color: var(--amber);";

        /// <summary>bundle.css:127 <c>.ocb-root .ocb-st-pay</c></summary>
        public const string StPayCss = "margin-left: auto; font: 700 24px/1 var(--font-mono); color: var(--wick); opacity: 0;";

        /// <summary>bundle.css:128 <c>.ocb-root .ocb-st-pay.is-on</c></summary>
        public const string StPayOnCss = "opacity: 1;";

        /// <summary>bundle.css:129 <c>.ocb-root .ocb-pips</c></summary>
        public const string PipsCss = "display: flex; align-items: center; gap: 4px; height: 20px; margin-top: 8px;";

        /// <summary>bundle.css:130 <c>.ocb-root .ocb-pip</c></summary>
        public const string PipCss = "width: 16px; height: 16px; border-radius: 50%; flex: none;";

        /// <summary>bundle.css:131 <c>.ocb-root .ocb-pip.is-have</c></summary>
        public const string PipHaveCss = "background: var(--amber);";

        /// <summary>bundle.css:132 <c>.ocb-root .ocb-pip.is-pay</c></summary>
        public const string PipPayCss = "border: 3px solid var(--amber);";

        /// <summary>bundle.css:133 <c>.ocb-root .ocb-pip.is-next</c></summary>
        public const string PipNextCss = "border: 2px dashed var(--amber);";

        /// <summary>bundle.css:134 <c>.ocb-root .ocb-pip.is-empty</c></summary>
        public const string PipEmptyCss = "border: 2px solid var(--steel);";

        /// <summary>bundle.css:135 <c>.ocb-root .ocb-pip.is-lit</c></summary>
        public const string PipLitCss = "animation: ocb-pip-lit .32s ease-out;";

        /// <summary>bundle.css:136 <c>@keyframes ocb-pip-lit</c> (the text Trial.Geometry's CssKeyframes plays under that name).</summary>
        public const string PipLitKeyframes = "@keyframes ocb-pip-lit { 0% { box-shadow: 0 0 0 0 rgba(255,216,154,.9); transform: scale(1.35); } 100% { box-shadow: 0 0 0 8px rgba(255,216,154,0); transform: scale(1); } }";

        /// <summary>bundle.css:137 <c>.ocb-root .ocb-pip-div</c></summary>
        public const string PipDivCss = "width: 2px; height: 20px; background: rgba(236,235,242,.7); flex: none;";

        /// <summary>bundle.css:138 <c>.ocb-root .ocb-pile</c></summary>
        public const string PileCss = "position: relative; width: 64px; height: 84px; cursor: pointer;";

        /// <summary>bundle.css:139 <c>.ocb-root .ocb-pile-sheet</c></summary>
        public const string PileSheetCss = "position: absolute; left: 0; width: 64px; height: 84px; border-radius: 5px; background: var(--card); border: 1px solid var(--steel);";

        /// <summary>bundle.css:140 <c>.ocb-root .ocb-pile-face</c></summary>
        public const string PileFaceCss = "position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; border-radius: 5px; background: var(--card); border: 1px solid var(--steel);";

        /// <summary>bundle.css:141 <c>.ocb-root .ocb-pile.is-empty .ocb-pile-face</c></summary>
        public const string PileEmptyFaceCss = "background: none; border-style: dashed;";

        /// <summary>bundle.css:142 <c>.ocb-root .ocb-pile:hover .ocb-pile-face</c></summary>
        public const string PileHoverFaceCss = "border-color: var(--ink);";

        // ---- target frame + prediction + throw line --------------------------------------------------------------

        /// <summary>bundle.css:145 <c>.ocb-root .ocb-frame</c></summary>
        public const string FrameCss = "position: absolute; pointer-events: none;";

        /// <summary>bundle.css:146 <c>.ocb-root .ocb-frame svg</c></summary>
        public const string FrameSvgCss = "position: absolute; inset: 0; overflow: visible;";

        /// <summary>bundle.css:147 <c>.ocb-root .ocb-pred</c></summary>
        public const string PredCss = "position: absolute; height: 60px; padding: 0 14px; display: flex; align-items: center; gap: 8px; background: var(--panel-solid); border: var(--stroke-frame) solid var(--wick); border-radius: var(--radius-panel); font: 700 48px/1 var(--font-mono); color: var(--wick); white-space: nowrap; transform: translate(-50%, -50%);";

        /// <summary>bundle.css:148 <c>.ocb-root .ocb-throwline</c></summary>
        public const string ThrowLineCss = "position: absolute; height: 3px; background: repeating-linear-gradient(90deg, var(--amber) 0 9px, transparent 9px 15px); filter: drop-shadow(0 0 0 var(--keyline));";

        // ---- notices ---------------------------------------------------------------------------------------------

        /// <summary>bundle.css:151 <c>.ocb-root .ocb-hoverline</c></summary>
        public const string HoverLineCss = "width: 420px; min-height: 48px; padding: 10px 14px; display: flex; align-items: center; gap: 10px; background: var(--panel-solid); border: 1px solid var(--steel); border-radius: var(--radius-panel); font: 400 18px/26px var(--font-body);";

        /// <summary>bundle.css:152 <c>.ocb-root .ocb-reachpanel</c></summary>
        public const string ReachPanelCss = "width: 280px; height: 76px; padding: 12px 16px; display: flex; align-items: center; gap: 12px; background: var(--panel-solid); border: 1px dashed var(--miss); border-radius: var(--radius-panel); font: 400 18px/26px var(--font-body);";

        /// <summary>bundle.css:153 <c>.ocb-root .ocb-refusal</c></summary>
        public const string RefusalCss = "height: 48px; padding: 0 20px 0 16px; display: inline-flex; align-items: center; gap: 12px; background: var(--panel-solid); border: 1px solid var(--steel); border-radius: var(--radius-panel); font: 400 20px/28px var(--font-body); white-space: nowrap;";

        /// <summary>bundle.css:154 <c>.ocb-root .ocb-dot</c></summary>
        public const string DotCss = "width: 16px; height: 16px; border-radius: 50%; background: var(--amber); flex: none;";

        /// <summary>bundle.css:155 <c>.ocb-root .ocb-banner</c></summary>
        public const string BannerCss = "position: absolute; left: 0; right: 0; height: 132px; display: flex; align-items: center; justify-content: center; background: linear-gradient(90deg, rgba(10,12,18,0), rgba(10,12,18,.8) 28%, rgba(10,12,18,.8) 72%, rgba(10,12,18,0)); font: 800 64px/72px var(--font-display); letter-spacing: .12em; pointer-events: none;";

        /// <summary>bundle.css:156 <c>.ocb-root .ocb-banner::before, .ocb-root .ocb-banner::after</c></summary>
        public const string BannerLinesCss = "content: \"\"; position: absolute; left: 22%; right: 22%; height: 2px; background: var(--banner-line, var(--amber)); opacity: .7;";

        /// <summary>bundle.css:157 <c>.ocb-root .ocb-banner::before</c></summary>
        public const string BannerBeforeCss = "top: 10px;";

        /// <summary>bundle.css:157 <c>.ocb-root .ocb-banner::after</c></summary>
        public const string BannerAfterCss = "bottom: 10px;";

        /// <summary>bundle.css:158 <c>.ocb-root .ocb-banner.is-foe</c></summary>
        public const string BannerFoeCss = "--banner-line: var(--violet);";

        /// <summary>bundle.css:159 <c>.ocb-root .ocb-float</c></summary>
        public const string FloatCss = "position: absolute; display: flex; align-items: center; gap: 6px; padding: 2px 10px; border-radius: 6px; background: rgba(0,0,0,.8); white-space: nowrap; pointer-events: none; transform: translate(-50%, -50%);";

        /// <summary>bundle.css:160 <c>.ocb-root .ocb-float b</c></summary>
        public const string FloatBCss = "font: 700 48px/52px var(--font-mono); color: var(--hp-text);";

        /// <summary>bundle.css:161 <c>.ocb-root .ocb-float.is-guard b</c></summary>
        public const string FloatGuardBCss = "font-size: 34px; line-height: 40px; color: var(--guard);";

        /// <summary>bundle.css:162 <c>.ocb-root .ocb-float.is-miss b</c></summary>
        public const string FloatMissBCss = "font: 400 24px/32px var(--font-body); color: var(--ink); text-decoration: line-through 2px var(--miss);";

        /// <summary>bundle.css:163 <c>.ocb-root .ocb-float small</c></summary>
        public const string FloatSmallCss = "font: 400 16px/20px var(--font-body); color: var(--ink);";

        // ---- guide line (above the hand; .ocb-game, the DS prototype) --------------------------------------------

        /// <summary>bundle.css:166 <c>.ocb-game .ocb-guide</c></summary>
        public const string GuideCss = "position: absolute; left: 0; right: 0; top: 743px; height: 28px; display: flex; align-items: center; justify-content: center; gap: 10px; font: 400 22px/28px var(--font-body); color: var(--ink); text-shadow: 0 1px 3px rgba(0,0,0,.7); pointer-events: none; transition: opacity .2s;";

        // ---- HUD -------------------------------------------------------------------------------------------------

        /// <summary>bundle.css:169 <c>.ocb-root .ocb-corner</c></summary>
        public const string CornerCss = "height: 44px; padding: 0 16px; display: inline-flex; align-items: center; gap: 18px; font: 600 20px/1 var(--font-mono); white-space: nowrap;";

        /// <summary>bundle.css:170 <c>.ocb-root .ocb-corner span</c></summary>
        public const string CornerSpanCss = "display: inline-flex; align-items: center; gap: 6px;";

        /// <summary>bundle.css:171 <c>.ocb-root .ocb-btn-sq</c></summary>
        public const string BtnSqCss = "width: 56px; height: 56px; display: flex; align-items: center; justify-content: center; cursor: pointer;";

        /// <summary>bundle.css:172 <c>.ocb-root .ocb-speed</c></summary>
        public const string SpeedCss = "width: 120px; height: 56px; padding: 0 12px; display: flex; align-items: center; justify-content: center; gap: 8px; font: 600 20px/1 var(--font-mono); cursor: pointer;";

        /// <summary>bundle.css:173 <c>.ocb-root .ocb-speed:hover, .ocb-root .ocb-btn-sq:hover</c></summary>
        public const string SpeedHoverCss = "border-color: var(--ink); background: rgba(40,44,58,.92);";

        /// <summary>bundle.css:174 <c>.ocb-root .ocb-attrs</c></summary>
        public const string AttrsCss = "display: flex; gap: 6px;";

        /// <summary>bundle.css:175 <c>.ocb-root .ocb-attr</c></summary>
        public const string AttrCss = "position: relative; width: 40px; height: 40px; display: flex; align-items: center; justify-content: center; opacity: var(--opacity-unplayed);";

        /// <summary>bundle.css:176 <c>.ocb-root .ocb-attr.is-on</c></summary>
        public const string AttrOnCss = "opacity: 1;";

        /// <summary>bundle.css:177 <c>.ocb-root .ocb-attr .ocb-count</c></summary>
        public const string AttrCountCss = "position: absolute; right: -8px; top: -8px; width: 20px; height: 20px; border-radius: 50%; background: var(--panel-solid); border: 1px solid var(--ink-muted); font: 400 14px/18px var(--font-mono); text-align: center;";

        /// <summary>bundle.css:178 <c>.ocb-root .ocb-guardpre</c></summary>
        public const string GuardPreCss = "height: 56px; padding: 0 14px; display: inline-flex; align-items: center; gap: 8px; border: 1px dashed var(--guard); border-radius: var(--radius-panel); background: rgba(16,20,30,.6); font: 700 24px/1 var(--font-mono); color: var(--guard);";

        /// <summary>bundle.css:179 <c>.ocb-root .ocb-endturn</c></summary>
        public const string EndTurnCss = "width: 224px; height: 72px; display: flex; align-items: center; justify-content: center; border: var(--stroke-frame) solid var(--amber); border-radius: var(--radius-panel); background: rgba(232,168,76,.16); font: 700 24px/28px var(--font-body); letter-spacing: .08em; color: var(--ink); cursor: pointer;";

        /// <summary>bundle.css:180 <c>.ocb-root .ocb-endturn:hover</c></summary>
        public const string EndTurnHoverCss = "background: rgba(232,168,76,.28);";

        /// <summary>bundle.css:181 <c>.ocb-root .ocb-endturn.is-off</c></summary>
        public const string EndTurnOffCss = "opacity: var(--opacity-enemy-turn); cursor: default;";

        /// <summary>bundle.css:182 <c>.ocb-root .ocb-detail</c></summary>
        public const string DetailCss = "width: 560px; height: 280px; padding: 16px; display: flex; gap: 16px; background: var(--panel-solid); border: var(--stroke-frame) solid var(--steel); border-radius: var(--radius-panel); box-shadow: var(--shadow-tip);";

        /// <summary>bundle.css:183 <c>.ocb-root .ocb-detail-card</c></summary>
        public const string DetailCardCss = "width: 173px; height: 243px; flex: none; position: relative;";

        /// <summary>bundle.css:184 <c>.ocb-root .ocb-detail-card &gt; .ocb-card</c></summary>
        public const string DetailCardCardCss = "transform: scale(.8); transform-origin: 0 0;";

        /// <summary>bundle.css:185 <c>.ocb-root .ocb-detail-body</c></summary>
        public const string DetailBodyCss = "display: flex; flex-direction: column; gap: 6px; min-width: 0;";

        /// <summary>bundle.css:186 <c>.ocb-root .ocb-detail-body h3</c></summary>
        public const string DetailH3Css = "margin: 0; font: 700 24px/32px var(--font-display);";

        /// <summary>bundle.css:187 <c>.ocb-root .ocb-detail-body .ocb-meta</c></summary>
        public const string DetailMetaCss = "font: 400 16px/22px var(--font-body); color: var(--ink-muted);";

        /// <summary>bundle.css:188 <c>.ocb-root .ocb-detail-body .ocb-full</c></summary>
        public const string DetailFullCss = "font: 400 16px/22px var(--font-body);";

        /// <summary>bundle.css:189 <c>.ocb-root .ocb-detail-terms</c></summary>
        public const string DetailTermsCss = "margin-top: auto; padding: 8px 10px; background: rgba(0,0,0,.35); border-radius: 4px; font: 400 16px/22px var(--font-body);";

        /// <summary>bundle.css:190 <c>.ocb-root .ocb-detail-terms b</c></summary>
        public const string DetailTermsBCss = "margin-right: 6px;";

        // ---- pile drawer variants --------------------------------------------------------------------------------

        /// <summary>bundle.css:193 <c>.ocb-root .ocb-drawer</c></summary>
        public const string DrawerCss = "position: absolute; display: flex; flex-direction: column;";

        /// <summary>bundle.css:194 <c>.ocb-root .ocb-drawer-tabs</c></summary>
        public const string DrawerTabsCss = "display: flex; gap: 4px;";

        /// <summary>bundle.css:195 <c>.ocb-root .ocb-drawer-tab</c></summary>
        public const string DrawerTabCss = "font: 700 18px/1 var(--font-body); color: var(--ink-muted); padding: 10px 14px; cursor: pointer; border-bottom: 2px solid transparent;";

        /// <summary>bundle.css:196 <c>.ocb-root .ocb-drawer-tab.is-on</c></summary>
        public const string DrawerTabOnCss = "color: var(--ink); border-bottom-color: var(--amber);";

        /// <summary>bundle.css:197 <c>.ocb-root .ocb-drawer-row</c></summary>
        public const string DrawerRowCss = "display: flex; align-items: center; gap: 10px; height: 40px; padding: 0 12px; border-bottom: 1px solid var(--rule); font: 400 18px/1 var(--font-body);";

        /// <summary>bundle.css:198 <c>.ocb-root .ocb-drawer-row .ocb-mono</c></summary>
        public const string DrawerRowMonoCss = "margin-left: auto; font-weight: 600;";

        /// <summary>bundle.css:199 <c>.ocb-root .ocb-drawer-row i</c></summary>
        public const string DrawerRowICss = "width: 6px; height: 24px; border-radius: 2px; background: var(--attr); flex: none;";

        // ---- end screen ------------------------------------------------------------------------------------------

        /// <summary>bundle.css:202 <c>.ocb-root .ocb-end</c></summary>
        public const string EndCss = "position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: rgba(8,10,14,.62);";

        /// <summary>bundle.css:203 <c>.ocb-root .ocb-end-paper</c></summary>
        public const string EndPaperCss = "width: 720px; padding: 48px 56px 40px; background: var(--washi); color: var(--washi-ink); border-radius: 4px; box-shadow: 0 18px 40px rgba(0,0,0,.5); text-align: center;";

        /// <summary>bundle.css:204 <c>.ocb-root .ocb-end-paper h2</c></summary>
        public const string EndPaperH2Css = "margin: 0 0 8px; font: 800 64px/72px var(--font-display); letter-spacing: .12em;";

        /// <summary>bundle.css:205 <c>.ocb-root .ocb-end-paper p</c></summary>
        public const string EndPaperPCss = "font: 400 20px/32px var(--font-body);";

        /// <summary>bundle.css:206 <c>.ocb-root .ocb-end-paper button</c></summary>
        public const string EndPaperButtonCss = "margin-top: 28px; font: 700 22px/1 var(--font-body); letter-spacing: .08em; color: var(--washi-ink); background: none; border: 2px solid var(--washi-ink); border-radius: 6px; padding: 16px 36px; cursor: pointer;";
    }
}
