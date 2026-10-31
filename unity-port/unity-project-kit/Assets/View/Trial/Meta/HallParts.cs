// The parts of 継承の間 (scenes/meta/hall.js, meta-a.md §3.1.1〜3.1.4): the past lives' cards, the rules box, the seven
// strata with their marks and flames and the key under them, the selected trace's body (the candidate grid, the 走り書き
// rows, the cards the shallow rule kept out, the empty state), and the next heir's pieces (generation, the name with
// its 襲名 seal, the 装束, the figure, the survivor's box). HallScene mounts them in hall.js's order and owns the state
// (selection, scrolling, the arrival); this file only builds. Every rule of .ocbg-meta-hall is copied verbatim below,
// so a reviewer can diff it against meta.css; the layout numbers (gaps, paddings, margins) are read off those strings.
//
// Nothing here computes a rule: the texts and verdicts come from Inheritance.HallView (InheritanceCore), MetaText and
// MetaLines (Trial.Script); the layers' names and skies from DungeonCore / Backdrop.Script.
//
// Layout: the CSS flex rows (headers, chips, list rows) run through the kit's flex solver (Ocb.Flow) with the rule's
// own padding, border, gap and alignment; the columns are stacked here with the rule's gap, each block of text measured
// with TMP (TextMeasure) at the width it gets, so what is measured is what is drawn. The candidate rows' grid
// (136px | minmax(0, 1fr) | auto, and the 走り書き's two rows) is placed by hand from the same numbers.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InheritanceCore;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using Trial.Script;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Trial.Meta
{
    internal static class HallParts
    {
        // ---- meta.css: 継承の間, verbatim -----------------------------------------------------------------------------

        /// <summary>meta.css:64 .ocbg-meta-hall-title.</summary>
        internal const string TitleCss = "font: 800 56px/1.1 var(--font-display); letter-spacing: .24em; color: var(--ink); text-shadow: 0 0 30px color-mix(in srgb, var(--amber) 30%, transparent);";

        /// <summary>meta.css:65 .ocbg-meta-hall-lead.</summary>
        internal const string LeadCss = "font: 400 20px/1.4 var(--font-body); letter-spacing: .04em;";

        /// <summary>meta.css:69 .ocbg-meta-hall-lives.</summary>
        internal const string LivesCss = "display: flex; flex-direction: column; gap: var(--space-16); overflow-y: auto; padding-right: 6px;";

        /// <summary>meta.css:70 .ocbg-meta-hall-life.</summary>
        internal const string LifeCss = "position: relative; background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-left: 3px solid var(--steel); border-radius: var(--radius-panel); padding: 14px 16px 12px 18px; display: flex; flex-direction: column; gap: 6px;";

        /// <summary>meta.css:71 .ocbg-meta-hall-life.is-alive.</summary>
        internal const string LifeAliveCss = "border-left-color: var(--amber);";

        /// <summary>meta.css:72 .ocbg-meta-hall-life header.</summary>
        internal const string LifeHeaderCss = "display: flex; align-items: baseline; gap: var(--space-12);";

        /// <summary>meta.css:73 .ocbg-meta-gen.</summary>
        internal const string GenCss = "font: 600 18px/1 var(--font-mono); letter-spacing: .02em;";

        /// <summary>meta.css:74 .ocbg-meta-hall-lifename.</summary>
        internal const string LifeNameCss = "font: 700 28px/1.1 var(--font-display); letter-spacing: .1em;";

        /// <summary>meta.css:75 .ocbg-meta-ending.</summary>
        internal const string EndingCss = "margin-left: auto; display: inline-flex; align-items: center; gap: 6px; padding: 3px 10px 3px 6px; border: var(--stroke-rule) solid var(--steel); border-radius: var(--radius-tag); font: 700 18px/1.2 var(--font-body); white-space: nowrap; align-self: center;";

        /// <summary>meta.css:76 .ocbg-meta-ending.is-alive.</summary>
        internal const string EndingAliveCss = "border-color: var(--amber);";

        /// <summary>meta.css:77 .ocbg-meta-hall-line.</summary>
        internal const string LineCss = "font: 400 18px/1.45 var(--font-body);";

        /// <summary>meta.css:78 .ocbg-meta-hall-place.</summary>
        internal const string PlaceCss = "display: flex; align-items: center; gap: 8px; font: 400 18px/1.4 var(--font-body);";

        /// <summary>meta.css:79 .ocbg-meta-hall-life footer.</summary>
        internal const string LifeFooterCss = "display: flex; justify-content: space-between; gap: var(--space-12); margin-top: 6px; padding-top: 8px; border-top: var(--stroke-rule) solid var(--rule); font: 400 18px/1.4 var(--font-body);";

        /// <summary>meta.css:80 .ocbg-meta-tier.</summary>
        internal const string TierCss = "white-space: nowrap;";

        /// <summary>meta.css:81 .ocbg-meta-hall-none.</summary>
        internal const string NoneCss = "font: 400 18px/1.6 var(--font-body); padding: var(--space-16) 0;";

        /// <summary>meta.css:83 .ocbg-meta-hall-column.</summary>
        internal const string ColumnCss = "position: absolute;";

        /// <summary>meta.css:84 .ocbg-meta-stratum.</summary>
        internal const string StratumCss = "overflow: hidden; border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); transition: box-shadow .2s, border-color .2s;";

        /// <summary>meta.css:85 .ocbg-meta-stratum-veil.</summary>
        internal const string StratumVeilCss = "position: absolute; inset: 0; background: linear-gradient(90deg, color-mix(in srgb, var(--ground) 86%, transparent), color-mix(in srgb, var(--ground) 18%, transparent) 70%, color-mix(in srgb, var(--ground) 40%, transparent));";

        /// <summary>meta.css:86 .ocbg-meta-stratum-label.</summary>
        internal const string StratumLabelCss = "position: absolute; left: 12px; top: 12px; display: flex; align-items: center; gap: 10px; padding: 5px 12px 5px 6px; background: var(--panel); border-radius: var(--radius-tag); font: 700 20px/1 var(--font-display); letter-spacing: .08em; white-space: nowrap;";

        /// <summary>meta.css:87 .ocbg-meta-stratum-n.</summary>
        internal const string StratumNCss = "width: 28px; height: 28px; display: inline-flex; align-items: center; justify-content: center; border: var(--stroke-rule) solid var(--steel); border-radius: 50%; font: 600 16px/1 var(--font-mono); letter-spacing: 0;";

        /// <summary>meta.css:88 .ocbg-meta-stratum-marks.</summary>
        internal const string StratumMarksCss = "position: absolute; right: 12px; bottom: 12px; display: flex; gap: var(--space-8);";

        /// <summary>meta.css:89 .ocbg-meta-stratum.is-sel.</summary>
        internal const string StratumSelCss = "border-color: var(--wick); box-shadow: 0 0 0 1px var(--wick), 0 0 22px color-mix(in srgb, var(--wick) 30%, transparent);";

        /// <summary>meta.css:90 .ocbg-meta-mark.</summary>
        internal const string MarkCss = "position: relative; width: 44px; height: 44px; border-radius: 50%; background: var(--panel-solid); border: var(--stroke-frame) solid var(--steel); display: inline-flex; align-items: center; justify-content: center; cursor: default;";

        /// <summary>meta.css:91 .ocbg-meta-mark-n.</summary>
        internal const string MarkNCss = "font: 600 18px/1 var(--font-mono);";

        /// <summary>meta.css:92 .ocbg-meta-mark.is-alive.</summary>
        internal const string MarkAliveCss = "border-color: var(--amber);";

        /// <summary>meta.css:93 .ocbg-meta-mark.is-taken.</summary>
        internal const string MarkTakenCss = "border-style: dashed;";

        /// <summary>meta.css:94 .ocbg-meta-mark.is-trace.</summary>
        internal const string MarkTraceCss = "border-color: var(--wick); cursor: pointer; box-shadow: var(--shadow-lamp);";

        /// <summary>meta.css:95 .ocbg-meta-mark.is-trace:hover.</summary>
        internal const string MarkTraceHoverCss = "transform: translateY(-2px);";

        /// <summary>meta.css:96 .ocbg-meta-flame.</summary>
        internal const string FlameCss = "position: absolute; right: -5px; top: -5px; width: 14px; height: 14px; border-radius: 50%; background: radial-gradient(closest-side, var(--wick), color-mix(in srgb, var(--wick) 0%, transparent));";

        /// <summary>meta.css:98 .ocbg-meta-hall-trace.</summary>
        internal const string TraceCss = "display: flex; flex-direction: column; gap: var(--space-16); background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); padding: 20px 22px 18px;";

        /// <summary>meta.css:99 .ocbg-meta-hall-trace-body.</summary>
        internal const string TraceBodyCss = "display: flex; flex-direction: column; gap: var(--space-12); min-height: 0; overflow-y: auto;";

        /// <summary>meta.css:100 .ocbg-meta-hall-trace header.</summary>
        internal const string TraceHeaderCss = "display: flex; flex-direction: column; gap: var(--space-8);";

        /// <summary>meta.css:101 .ocbg-meta-hall-trace-top.</summary>
        internal const string TraceTopCss = "display: flex; align-items: center;";

        /// <summary>meta.css:102 .ocbg-meta-hall-trace-top .ocbg-meta-ending.</summary>
        internal const string TraceTopEndingCss = "margin-left: 0;";

        /// <summary>meta.css:103 .ocbg-meta-pager.</summary>
        internal const string PagerCss = "margin-left: auto; font: 600 18px/1 var(--font-mono);";

        /// <summary>meta.css:104 .ocbg-meta-hall-trace h3.</summary>
        internal const string TraceH3Css = "font: 700 26px/1.25 var(--font-display); letter-spacing: .06em;";

        /// <summary>meta.css:105 .ocbg-meta-hall-appears.</summary>
        internal const string AppearsCss = "display: flex; align-items: center; gap: 10px; font: 400 18px/1.4 var(--font-body);";

        /// <summary>meta.css:106 .ocbg-meta-hall-appears .ocbg-meta-dot.</summary>
        internal const string AppearsDotCss = "background: var(--wick); box-shadow: 0 0 8px var(--wick);";

        /// <summary>meta.css:107 .ocbg-meta-hall-appears.is-waiting .ocbg-meta-dot.</summary>
        internal const string AppearsWaitingDotCss = "background: var(--steel); box-shadow: none;";

        /// <summary>meta.css:108 .ocbg-meta-hall-note.</summary>
        internal const string NoteCss = "font: 400 16px/1.45 var(--font-body); padding: 8px 10px; border-left: 2px solid var(--amber); background: color-mix(in srgb, var(--amber) 8%, transparent);";

        /// <summary>meta.css:109 .ocbg-meta-hall-count.</summary>
        internal const string CountCss = "font: 700 18px/1.4 var(--font-body); padding-top: var(--space-8); border-top: var(--stroke-rule) solid var(--rule);";

        /// <summary>meta.css:110 .ocbg-meta-cands li.</summary>
        internal const string CandCss = "display: grid; grid-template-columns: 136px 1fr auto; align-items: center; gap: var(--space-12); min-height: 42px; border-bottom: var(--stroke-rule) solid var(--rule);";

        /// <summary>meta.css:400 .ocbg-meta-cands li (the review pass's columns).</summary>
        internal const string CandColsCss = "grid-template-columns: 136px minmax(0, 1fr) auto;";

        /// <summary>meta.css:111 .ocbg-meta-cand-kind.</summary>
        internal const string CandKindCss = "display: inline-flex; align-items: center; gap: 6px; font: 400 16px/1 var(--font-body); white-space: nowrap;";

        /// <summary>meta.css:112 .ocbg-meta-cands b.</summary>
        internal const string CandNameCss = "font: 700 20px/1.2 var(--font-display); letter-spacing: .04em;";

        /// <summary>meta.css:399 .ocbg-meta-cands li b.</summary>
        internal const string CandNameClipCss = "white-space: nowrap; overflow: hidden; text-overflow: ellipsis; min-width: 0;";

        /// <summary>meta.css:113 .ocbg-meta-cand-detail.</summary>
        internal const string CandDetailCss = "font: 400 16px/1.2 var(--font-body); white-space: nowrap;";

        /// <summary>meta.css:402 .ocbg-meta-cands li.is-long.</summary>
        internal const string CandLongCss = "grid-template-columns: 136px minmax(0, 1fr); row-gap: 2px; padding: 6px 0;";

        /// <summary>meta.css:403 .ocbg-meta-cands li.is-long .ocbg-meta-cand-detail.</summary>
        internal const string CandLongDetailCss = "grid-column: 2 / 3; white-space: normal;";

        /// <summary>meta.css:413 .ocbg-meta-cands li.is-long b.</summary>
        internal const string CandLongNameCss = "white-space: normal; overflow: visible; text-overflow: clip; line-height: 1.3; padding-top: 4px;";

        /// <summary>meta.css:434 .ocbg-meta-cands li.is-long b.</summary>
        internal const string CandLongNameSizeCss = "font-size: 18px; letter-spacing: 0;";

        /// <summary>meta.css:114 .ocbg-meta-hall-trace h4.</summary>
        internal const string TraceH4Css = "font: 700 18px/1.3 var(--font-body); margin-top: var(--space-8);";

        /// <summary>meta.css:115 .ocbg-meta-withheld li.</summary>
        internal const string WithheldLiCss = "display: flex; justify-content: space-between; gap: var(--space-12); padding: 6px 0 6px 12px; border-left: 2px dashed var(--steel); font: 400 16px/1.4 var(--font-body);";

        /// <summary>meta.css:116 .ocbg-meta-withheld b.</summary>
        internal const string WithheldBCss = "font: 700 18px/1.3 var(--font-display);";

        /// <summary>meta.css:117 .ocbg-meta-hall-rules.</summary>
        internal const string RulesCss = "display: flex; flex-direction: column; justify-content: center; gap: 6px;";

        /// <summary>meta.css:398 .ocbg-meta-hall-rules (the review pass's box).</summary>
        internal const string RulesBoxCss = "background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); padding: 14px 16px; margin: 0;";

        /// <summary>meta.css:118 .ocbg-meta-hall-rules li.</summary>
        internal const string RulesLiCss = "position: relative; padding-left: 16px; font: 400 18px/1.4 var(--font-body);";

        /// <summary>meta.css:119 .ocbg-meta-hall-rules li::before.</summary>
        internal const string RulesLiBeforeCss = "content: \"\"; position: absolute; left: 2px; top: 10px; width: 5px; height: 5px; border-radius: 50%; background: var(--ink);";

        /// <summary>meta.css:121 .ocbg-meta-hall-heir.</summary>
        internal const string HeirCss = "display: flex; flex-direction: column; gap: var(--space-16);";

        /// <summary>meta.css:122 .ocbg-meta-heir-gen.</summary>
        internal const string HeirGenCss = "display: flex; align-items: baseline; gap: var(--space-12); font: 700 26px/1 var(--font-display); letter-spacing: .1em;";

        /// <summary>meta.css:123 .ocbg-meta-heir-gen span.</summary>
        internal const string HeirGenSpanCss = "font: 400 18px/1 var(--font-body); letter-spacing: .1em;";

        /// <summary>meta.css:124 .ocbg-meta-heir-namerow.</summary>
        internal const string NameRowCss = "display: flex; align-items: center; gap: var(--space-24); height: 96px;";

        /// <summary>meta.css:125 .ocbg-meta-heir-name.</summary>
        internal const string NameCss = "font: 800 72px/1 var(--font-display); letter-spacing: .14em; white-space: nowrap; text-shadow: 0 0 34px color-mix(in srgb, var(--amber) 32%, transparent);";

        /// <summary>meta.css:126 .ocbg-meta-heir-name span.</summary>
        internal const string NameSpanCss = "display: inline-block;";

        /// <summary>meta.css:127 .ocbg-meta-seal.</summary>
        internal const string SealCss = "width: 62px; height: 92px; flex: none; display: flex; flex-direction: column; align-items: center; justify-content: center; border: 3px solid var(--amber); border-radius: var(--radius-tag); font: 800 30px/1.08 var(--font-display); color: var(--amber); background: color-mix(in srgb, var(--amber) 10%, transparent);";

        /// <summary>meta.css:128 .ocbg-meta-seal.is-stamped's animation (@keyframes ocbg-meta-stamp, meta.css:129).</summary>
        internal const string SealStampedAnimation = "ocbg-meta-stamp .7s ease-out";

        /// <summary>meta.css:130 .ocbg-meta-heir-attire.</summary>
        internal const string AttireCss = "width: 262px; display: flex; flex-direction: column; gap: 6px;";

        /// <summary>meta.css:131 .ocbg-meta-heir-attire h4.</summary>
        internal const string AttireH4Css = "font: 700 18px/1 var(--font-body); letter-spacing: .2em;";

        /// <summary>meta.css:132 .ocbg-meta-heir-tier.</summary>
        internal const string TierNameCss = "font: 700 26px/1.2 var(--font-display); letter-spacing: .08em;";

        /// <summary>meta.css:133 .ocbg-meta-heir-reason.</summary>
        internal const string ReasonCss = "font: 400 18px/1.4 var(--font-body);";

        /// <summary>meta.css:134 .ocbg-meta-heir-attire ul.</summary>
        internal const string AttireUlCss = "display: flex; flex-direction: column; gap: 6px; margin-top: 6px;";

        /// <summary>meta.css:135 .ocbg-meta-heir-attire li.</summary>
        internal const string AttireLiCss = "display: flex; align-items: center; gap: 10px; font: 400 16px/1.3 var(--font-body);";

        /// <summary>meta.css:136 .ocbg-meta-swatch.</summary>
        internal const string SwatchCss = "width: 24px; height: 24px; flex: none; border-radius: var(--radius-tag); border: var(--stroke-rule) solid var(--steel);";

        /// <summary>meta.css:137 .ocbg-meta-swatch.is-none.</summary>
        internal const string SwatchNoneCss = "border-style: dashed;";

        /// <summary>meta.css:138 .ocbg-meta-part.</summary>
        internal const string PartCss = "width: 96px; flex: none;";

        /// <summary>meta.css:139 .ocbg-meta-heir-figure.</summary>
        internal const string FigureCss = "position: absolute; right: 16px; top: 166px; width: 140px; height: 226px;";

        /// <summary>meta.css:142 .ocbg-meta-heir-survivor.</summary>
        internal const string SurvivorCss = "margin-top: var(--space-8); padding: 14px 16px; border: var(--stroke-rule) solid var(--amber); border-radius: var(--radius-panel); background: color-mix(in srgb, var(--amber) 8%, var(--panel)); display: flex; flex-direction: column; gap: 6px;";

        /// <summary>meta.css:412 .ocbg-meta-heir-survivor (the review pass: the box sits at the bottom of the heir's column).</summary>
        internal const string SurvivorBottomCss = "margin-top: auto;";

        /// <summary>meta.css:143 .ocbg-meta-heir-survivor h4.</summary>
        internal const string SurvivorH4Css = "font: 700 18px/1.2 var(--font-body); letter-spacing: .12em;";

        /// <summary>meta.css:144 .ocbg-meta-heir-survivor-line.</summary>
        internal const string SurvivorLineCss = "font: 700 24px/1.2 var(--font-display); letter-spacing: .06em;";

        /// <summary>meta.css:145 .ocbg-meta-heir-survivor li.</summary>
        internal const string SurvivorLiCss = "display: flex; align-items: center; gap: 8px; font: 400 18px/1.4 var(--font-body);";

        /// <summary>meta.css:147 .ocbg-meta-heir-survivor.is-spent.</summary>
        internal const string SurvivorSpentCss = "border-color: color-mix(in srgb, var(--ink) 30%, transparent); background: var(--panel);";

        /// <summary>meta.css:148 .ocbg-meta-heir-duration.</summary>
        internal const string DurationCss = "font: 400 16px/1.3 var(--font-body);";

        /// <summary>meta.css:396 .ocbg-meta-hall-trace-body.is-empty.</summary>
        internal const string TraceBodyEmptyCss = "flex: 1; align-items: center; justify-content: center; gap: var(--space-16);";

        /// <summary>meta.css:397 .ocbg-meta-wick-off.</summary>
        internal const string WickOffCss = "width: 18px; height: 18px; border-radius: 50%; border: var(--stroke-frame) solid var(--steel);";

        /// <summary>meta.css:414 .ocbg-meta-hall-lives.is-more-below's mask-image.</summary>
        internal const string LivesMoreBelow = "linear-gradient(180deg, var(--ground) 84%, transparent)";

        /// <summary>meta.css:415 .ocbg-meta-hall-lives.is-more-above's mask-image.</summary>
        internal const string LivesMoreAbove = "linear-gradient(0deg, var(--ground) 84%, transparent)";

        /// <summary>meta.css:416 .ocbg-meta-hall-lives.is-more-above.is-more-below's mask-image.</summary>
        internal const string LivesMoreBoth = "linear-gradient(180deg, transparent, var(--ground) 14%, var(--ground) 86%, transparent)";

        /// <summary>meta.css:436 .ocbg-meta-stratum.is-live.</summary>
        internal const string StratumLiveCss = "cursor: pointer;";

        /// <summary>meta.css:437 .ocbg-meta-stratum.is-live:hover:not(.is-sel).</summary>
        internal const string StratumLiveHoverCss = "border-color: color-mix(in srgb, var(--wick) 55%, var(--rule));";

        /// <summary>meta.css:440 .ocbg-meta-hall-line, .ocbg-meta-heir-reason.</summary>
        internal const string BalanceCss = "text-wrap: balance;";

        /// <summary>meta.css:451 .ocbg-meta-hall-trace-body.is-more-below's mask-image.</summary>
        internal const string TraceMoreBelow = "linear-gradient(180deg, var(--ground) 86%, transparent)";

        /// <summary>meta.css:452 .ocbg-meta-hall-trace-body.is-more-above's mask-image.</summary>
        internal const string TraceMoreAbove = "linear-gradient(0deg, var(--ground) 86%, transparent)";

        /// <summary>meta.css:453 .ocbg-meta-hall-trace-body.is-more-above.is-more-below's mask-image.</summary>
        internal const string TraceMoreBoth = "linear-gradient(180deg, transparent, var(--ground) 12%, var(--ground) 88%, transparent)";

        /// <summary>meta.css:455 .ocbg-meta-mark-key.</summary>
        internal const string MarkKeyCss = "display: flex; flex-wrap: wrap; align-items: center; gap: 6px 18px; font: 400 18px/24px var(--font-body);";

        /// <summary>meta.css:456 .ocbg-meta-mark-key > span.</summary>
        internal const string MarkKeySpanCss = "display: inline-flex; align-items: center; gap: 8px; white-space: nowrap;";

        /// <summary>meta.css:457 .ocbg-meta-mark.is-key.</summary>
        internal const string MarkKeyMarkCss = "width: 22px; height: 22px; box-shadow: none; cursor: default;";

        /// <summary>meta.css:458 .ocbg-meta-mark.is-key.is-trace.</summary>
        internal const string MarkKeyTraceCss = "box-shadow: 0 0 8px color-mix(in srgb, var(--wick) 60%, transparent);";

        // ---- hall.js's layout numbers (stage px) ---------------------------------------------------------------------

        // js: scenes/meta/hall.js:171 place(s, 0, (layer - 1) * 108, 360, 102)
        internal const float StratumPitch = 108f, StratumW = 360f, StratumH = 102f;

        /// <summary>grid-template-columns: 136px … (meta.css:110, :400, :402): the kind column of a candidate row.</summary>
        internal const float CandKindColumn = 136f;

        /// <summary>The slack a measured width gets so TMP does not wrap a line that fits (the kit's measuring convention).</summary>
        private const float Slack = 0.01f;

        // ---- hall.js's small helpers ---------------------------------------------------------------------------------

        // js: scenes/meta/hall.js:14
        /// <summary>DEATHS: a life that ended in a death (倒れた, 瘴気死); the others closed alive.</summary>
        internal static bool IsDeath(LifeEnding? ending) => ending == LifeEnding.Fallen || ending == LifeEnding.MiasmaDeath;

        // js: scenes/meta/hall.js:32
        /// <summary>endingIcon(ending) → [icon, colour]: miasma phosphor / person steel / star amber / person amber (also for none).</summary>
        internal static (string Icon, string Color) EndingIcon(LifeEnding? ending)
        {
            switch (ending)
            {
                case LifeEnding.MiasmaDeath: return ("miasma", "var(--phosphor)");
                case LifeEnding.Fallen: return ("person", "var(--steel)");
                case LifeEnding.Completed: return ("star", "var(--amber)");
                default: return ("person", "var(--amber)");
            }
        }

        // js: scenes/meta/hall.js:41
        /// <summary>kindIcon(kind) → [icon, colour]: 技術 star amber, 敵の頁 journal ink, ダンジョンの頁 stairs ink, 走り書き journal ink.</summary>
        internal static (string Icon, string Color) KindIcon(CandidateKind kind)
        {
            switch (kind)
            {
                case CandidateKind.Card: return ("star", "var(--amber)");
                case CandidateKind.EnemyPage: return ("journal", "var(--ink)");
                case CandidateKind.DungeonPage: return ("stairs", "var(--ink)");
                default: return ("journal", "var(--ink)");
            }
        }

        // js: scenes/meta/hall.js:51
        /// <summary>skyCss(layer): a stratum's sky as CSS, read off the bridge's per-layer stops (HallBackdropSpec.StratumSky).</summary>
        internal static string SkyCss(int layer)
        {
            var sb = new StringBuilder("linear-gradient(180deg, ");
            IReadOnlyList<StratumStop> stops = HallBackdropSpec.StratumSky(layer);
            for (int i = 0; i < stops.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(MetaUtil.RgbaCss(stops[i].Color)).Append(' ').Append(stops[i].Percent.ToString(CultureInfo.InvariantCulture)).Append('%');
            }
            return sb.Append(')').ToString();
        }

        // ---- reading the rules ---------------------------------------------------------------------------------------

        /// <summary>The row gap of a rule's `gap` (between the items of a column).</summary>
        internal static float RowGap(string css) => Ocb.Rule(css).Layout.RowGap ?? 0f;

        /// <summary>The column gap of a rule's `gap` (between the items of a row).</summary>
        internal static float ColGap(string css) => Ocb.Rule(css).Layout.ColumnGap ?? 0f;

        /// <summary>A rule's margin-top in px (auto and undeclared are 0).</summary>
        internal static float MarginTop(string css) => Ocb.Px(Ocb.Rule(css).Layout.MarginTop) ?? 0f;

        /// <summary>A rule's border widths plus its padding (where its content starts).</summary>
        internal static Insets Edge(string css)
        {
            BoxStyle b = Ocb.BoxOf(css);
            return b.BorderWidths + b.Padding;
        }

        // ---- elements ------------------------------------------------------------------------------------------------

        /// <summary>h(tag, { class }): an element with the rule's box (Ocb.El: a UiNode, its px size, a BoxGraphic when it paints).</summary>
        private static RectTransform El(RectTransform parent, string name, string css) => Ocb.El(parent, name, css);

        /// <summary>A span (a text node): a TMP label sized to its text, at (0, 0).</summary>
        private static TextMeshProUGUI Span(RectTransform parent, string text, TextStyle style, string name = "span") =>
            Label.Make(parent, text ?? "", style, name);

        /// <summary>A block of text (p, li, h3, h4) w wide at (x, y), as tall as its lines (CSS line boxes); h = that height.</summary>
        internal static TextMeshProUGUI Block(RectTransform parent, string text, TextStyle style, float x, float y, float w, out float h, string name)
        {
            float bw = Math.Max(0f, w) + Slack;
            h = string.IsNullOrEmpty(text) ? 0f : TextMeasure.Height(text, style, bw);
            TextMeshProUGUI t = Label.Make(parent, text ?? "", style, x, y, bw, h, name);
            // in a flex row it keeps the width it was measured at (the slack must not be shrunk away)
            FlexBox.Attrs(t.rectTransform).Shrink = 0f;
            return t;
        }

        /// <summary>A label's large text-shadows (the underlay draws only small ones): the glow behind it.</summary>
        internal static void Glow(TMP_Text label)
        {
            if (label && Label.GlowShadows(label).Count > 0) TextGlow.For(label);
        }

        private static Rgba ColorOf(string css) => CssColor.Parse(css);

        private static UiNode NodeOf(RectTransform rt) => UiNode.Of(rt);

        // js: scenes/meta/hall.js:133, :236
        /// <summary>
        /// span.ocbg-meta-ending (.is-alive): how a life ended, its icon (22) and label in a 1px steel / amber tag. In a life
        /// card's header it is pushed right (margin-left: auto) and centred; in the trace's top row margin-left is 0.
        /// </summary>
        internal static RectTransform EndingTag(RectTransform parent, LifeEnding? ending, string label, bool alive, bool inTraceTop)
        {
            string css = EndingCss + (alive ? EndingAliveCss : "") + (inTraceTop ? TraceTopEndingCss : "");
            RectTransform el = El(parent, "ocbg-meta-ending" + (alive ? " is-alive" : ""), css);
            (string icon, string color) = EndingIcon(ending);
            MetaUtil.Icon(el, icon, 22f, ColorOf(color));
            Span(el, label, MetaStyle.Text(css));
            Ocb.Flow(el, css);
            FlexAttrs a = FlexBox.Attrs(el);
            a.Self = Align.Center;            // align-self: center
            a.AutoMarginL = !inTraceTop;      // margin-left: auto (0 in the trace's top row)
            a.FixedCross = true;
            a.Shrink = 0f;                    // white-space: nowrap: it keeps its width
            return el;
        }

        // js: scenes/meta/hall.js:135, :239
        /// <summary>p.ocbg-meta-hall-place: stairs (18, ink) and the place line, w wide.</summary>
        internal static RectTransform PlaceRow(RectTransform parent, string text, float w)
        {
            RectTransform p = El(parent, "ocbg-meta-hall-place", PlaceCss);
            MetaUtil.Icon(p, "stairs", 18f, DsTokens.Ink);
            Block(p, text, MetaStyle.P(PlaceCss), 0f, 0f, w - 18f - ColGap(PlaceCss), out _, "text");
            Ocb.Flow(p, PlaceCss, null, w, null);
            return p;
        }

        // ---- the past lives (hall.js:123-151) ------------------------------------------------------------------------

        // js: scenes/meta/hall.js:127
        /// <summary>
        /// article.ocbg-meta-hall-life (.is-alive): 第 n 代, the name and how it ended; the end sentence; where; and the
        /// footer with 「装束　＜段＞」 and the trace line (MetaLines.PastTraceLine). w wide, as tall as its content.
        /// </summary>
        internal static RectTransform LifeCard(RectTransform parent, PastLifeView life, string traceLine, float w)
        {
            bool lived = !IsDeath(life.Ending);
            string css = LifeCss + (lived ? LifeAliveCss : "");
            RectTransform card = El(parent, "ocbg-meta-hall-life" + (lived ? " is-alive" : ""), css);
            Insets edge = Edge(css);
            float gap = RowGap(css), x = edge.Left, inner = w - edge.Horizontal, y = edge.Top;

            // header: span.ocbg-meta-gen, b.ocbg-meta-hall-lifename, span.ocbg-meta-ending
            RectTransform header = El(card, "header", LifeHeaderCss);
            TextStyle genStyle = MetaStyle.Text(LifeHeaderCss + GenCss);
            Span(header, life.GenerationLabel, genStyle, "ocbg-meta-gen");
            RectTransform tag = EndingTag(header, life.Ending, life.EndingLabel, lived, false);
            // the name (b) is the one item that can give: a name too long for the row wraps in what is left (flex-shrink)
            TextStyle nameStyle = MetaStyle.Text(LifeHeaderCss + LifeNameCss);
            float hgap = ColGap(LifeHeaderCss);
            float nameRoom = inner - TextMeasure.Width(life.GenerationLabel, genStyle) - NodeOf(tag).W - 2f * hgap;
            TextMeshProUGUI nameLabel = TextMeasure.Width(life.Name, nameStyle) <= nameRoom
                ? Span(header, life.Name, nameStyle, "ocbg-meta-hall-lifename")
                : Block(header, life.Name, nameStyle, 0f, 0f, nameRoom, out _, "ocbg-meta-hall-lifename");
            nameLabel.transform.SetSiblingIndex(1);   // DOM order: gen, name, ending
            Ocb.Flow(header, LifeHeaderCss, null, inner, null);
            NodeOf(header).Place(x, y);
            y += NodeOf(header).H + gap;

            // p.ocbg-meta-hall-line (text-wrap: balance)
            Block(card, life.EndingLine, MetaStyle.P(LineCss + BalanceCss, balance: true), x, y, inner, out float lineH, "ocbg-meta-hall-line");
            y += lineH + gap;

            // p.ocbg-meta-hall-place
            RectTransform place = PlaceRow(card, life.PlaceLine, inner);
            NodeOf(place).Place(x, y);
            y += NodeOf(place).H + gap;

            // footer (margin-top 6): 装束　＜段＞ (nowrap) | the trace line, space-between
            y += MarginTop(LifeFooterCss);
            RectTransform footer = El(card, "footer", LifeFooterCss);
            TextStyle tierStyle = MetaStyle.Text(LifeFooterCss + TierCss);
            string tier = InheritanceText.AttireHeading + "　" + life.TierName;
            float tierW = TextMeasure.Width(tier, tierStyle);
            TextMeshProUGUI tierLabel = Span(footer, tier, tierStyle, "ocbg-meta-tier");
            FlexBox.Attrs(tierLabel.rectTransform).Shrink = 0f;   // white-space: nowrap: min-content is its whole width
            TextStyle lineStyle = MetaStyle.Text(LifeFooterCss);
            float room = inner - tierW - ColGap(LifeFooterCss);
            if (TextMeasure.Width(traceLine, lineStyle) <= room) Span(footer, traceLine, lineStyle);
            else Block(footer, traceLine, lineStyle, 0f, 0f, room, out _, "span");   // flex-shrink: the line wraps in what is left
            Ocb.Flow(footer, LifeFooterCss, null, inner, null);
            NodeOf(footer).Place(x, y);
            y += NodeOf(footer).H;

            NodeOf(card).Place(0f, 0f, w, y + edge.Bottom);
            return card;
        }

        // js: scenes/meta/hall.js:140
        /// <summary>p.ocbg-meta-hall-none in the lives list: 「最初の踏破者。継ぐものはまだ無い。」 (padding 16 0), w wide.</summary>
        internal static RectTransform LivesNone(RectTransform parent, string text, float w)
        {
            RectTransform p = El(parent, "ocbg-meta-hall-none", NoneCss);
            Insets e = Edge(NoneCss);
            Block(p, text, MetaStyle.P(NoneCss), e.Left, e.Top, w - e.Horizontal, out float h, "text");
            NodeOf(p).Place(0f, 0f, w, h + e.Vertical);
            return p;
        }

        // js: scenes/meta/hall.js:153
        /// <summary>
        /// ul.ocbg-meta-hall-rules at (x, y), w × h: the rules of traces (R-12), each li with its 5px ink dot (::before),
        /// centred in the box (justify-content: center).
        /// </summary>
        internal static RectTransform Rules(RectTransform parent, IReadOnlyList<string> rules, float x, float y, float w, float h)
        {
            string css = RulesCss + RulesBoxCss;
            RectTransform ul = El(parent, "ocbg-meta-hall-rules", css);
            NodeOf(ul).Place(x, y, w, h);
            Insets edge = Edge(css);
            float inner = w - edge.Horizontal, gap = RowGap(css);
            Insets liPad = Ocb.BoxOf(RulesLiCss).Padding;
            CssBlock before = Ocb.Rule(RulesLiBeforeCss);
            float dotX = Ocb.Px(before.Layout.Left) ?? 0f, dotY = Ocb.Px(before.Layout.Top) ?? 0f;
            float dotW = Ocb.Px(before.Layout.Width) ?? 0f, dotH = Ocb.Px(before.Layout.Height) ?? 0f;
            TextStyle s = MetaStyle.P(RulesCss + RulesLiCss);
            var items = new List<(UiNode Li, float H)>();
            float total = 0f;
            foreach (string rule in rules ?? Array.Empty<string>())
            {
                RectTransform li = Ui.Div(ul, "li");
                UiNode node = NodeOf(li);
                BoxGraphic.Make(li, RulesLiBeforeCss, dotX, dotY, dotW, dotH, "before");
                Block(li, rule, s, liPad.Left, 0f, inner - liPad.Horizontal, out float th, "text");
                node.Place(0f, 0f, inner, th);
                if (items.Count > 0) total += gap;
                total += th;
                items.Add((node, th));
            }
            float top = edge.Top + (h - edge.Vertical - total) / 2f;   // justify-content: center
            foreach ((UiNode li, float lh) in items)
            {
                li.Place(edge.Left, top);
                top += lh + gap;
            }
            return ul;
        }

        // ---- the seven strata (hall.js:156-211) ----------------------------------------------------------------------

        // js: scenes/meta/hall.js:161
        /// <summary>
        /// div.ocbg-meta-stratum for `layer` in the column: its sky (inline background), the veil, the label (number and
        /// name) and the marks' row; placed at (0, (layer − 1) × 108), 360 × 102. overflow: hidden is a RectMask2D on its
        /// padding box (the marks' lamp glow stops at the band's edge).
        /// </summary>
        internal static HallStratum Stratum(RectTransform column, int layer, string name)
        {
            // the stylesheet's rule, then the inline style (style: { background: skyCss(layer) }) which wins
            string css = StratumCss + "background: " + SkyCss(layer) + ";";
            RectTransform root = El(column, "ocbg-meta-stratum " + layer.ToString(CultureInfo.InvariantCulture), css);
            UiNode node = NodeOf(root);
            node.Place(0f, (layer - 1) * StratumPitch, StratumW, StratumH);
            Insets bw = Ocb.BoxOf(css).BorderWidths;
            float cw = StratumW - bw.Horizontal, ch = StratumH - bw.Vertical;
            RectTransform clip = Ui.Div(root, "overflow", bw.Left, bw.Top, cw, ch);
            NodeOf(clip);
            clip.gameObject.AddComponent<RectMask2D>();

            // .ocbg-meta-stratum-veil (inset: 0, inside the 6px radius's 5px inner curve)
            CssLength radius = Ocb.BoxOf(css).Radius.TLx;
            float inner = Math.Max(0f, (radius.IsPx ? radius.Px : 0f) - bw.Left);
            BoxGraphic.Make(clip, StratumVeilCss + "border-radius: " + inner.ToString(CultureInfo.InvariantCulture) + "px;", 0f, 0f, cw, ch, "ocbg-meta-stratum-veil");

            // .ocbg-meta-stratum-label: the number in its ring, then the layer's name
            RectTransform label = El(clip, "ocbg-meta-stratum-label", StratumLabelCss);
            RectTransform n = El(label, "ocbg-meta-stratum-n", StratumNCss);
            Span(n, layer.ToString(CultureInfo.InvariantCulture), MetaStyle.Text(StratumLabelCss + StratumNCss));
            Ocb.Flow(n, StratumNCss);
            Ocb.NoShrink(n);
            Span(label, name, MetaStyle.Text(StratumLabelCss));
            Ocb.Flow(label, StratumLabelCss);
            CssBlock lr = Ocb.Rule(StratumLabelCss);
            NodeOf(label).Place(Ocb.Px(lr.Layout.Left) ?? 0f, Ocb.Px(lr.Layout.Top) ?? 0f);

            // .ocbg-meta-stratum-marks (right 12, bottom 12)
            RectTransform marks = El(clip, "ocbg-meta-stratum-marks", StratumMarksCss);

            var s = new HallStratum
            {
                Layer = layer, Root = root, Node = node, Clip = clip, Marks = marks, ClipW = cw, ClipH = ch,
                States = CssStates.Of(root),
            };
            s.States.Base(css);
            s.States.State(".is-sel", StratumSelCss);
            s.States.State(":hover", StratumLiveHoverCss);   // .is-live:hover:not(.is-sel), set by HallStratum
            s.States.Apply(instant: true);
            s.LayMarks();
            return s;
        }

        // js: scenes/meta/hall.js:185
        /// <summary>
        /// button.ocbg-meta-mark on the stratum where a life ended: 44 × 44, the generation in it; .is-alive (closed
        /// alive, amber), .is-taken (its trace was collected, dashed), .is-trace (an untaken trace: wick, the lamp
        /// shadow, lifted 2px under the pointer, and the flame at its top right). title = its hover line.
        /// </summary>
        internal static HallMark Mark(HallStratum s, int generation, bool lived, bool untaken, bool taken, string title)
        {
            // the state classes in stylesheet order (meta.css:92-94, one specificity): is-trace's colour wins
            string css = MarkCss + (lived ? MarkAliveCss : "") + (taken ? MarkTakenCss : "") + (untaken ? MarkTraceCss : "");
            string cls = "ocbg-meta-mark" + (lived ? " is-alive" : "") + (untaken ? " is-trace" : "") + (taken ? " is-taken" : "");
            RectTransform el = El(s.Marks, cls, css);
            Span(el, generation.ToString(CultureInfo.InvariantCulture), MetaStyle.Text(MarkCss + MarkNCss), "ocbg-meta-mark-n");
            Ocb.Flow(el, css);
            Ocb.NoShrink(el);
            var m = new HallMark { Root = el, Box = el.GetComponent<BoxGraphic>(), Stratum = s };
            m.Area = ClickArea.On(m.Box);
            TitleTip.On(m.Box, title);
            if (untaken)
            {
                // i.ocbg-meta-flame: right −5, top −5 of the mark's padding box
                CssBlock fr = Ocb.Rule(FlameCss);
                Insets bw = Ocb.BoxOf(css).BorderWidths;
                float fw = Ocb.Px(fr.Layout.Width) ?? 0f, fh = Ocb.Px(fr.Layout.Height) ?? 0f;
                float fx = NodeOf(el).W - bw.Right - (Ocb.Px(fr.Layout.Right) ?? 0f) - fw;
                float fy = bw.Top + (Ocb.Px(fr.Layout.Top) ?? 0f);
                BoxGraphic flame = BoxGraphic.Make(el, FlameCss, fx, fy, fw, fh, "ocbg-meta-flame");
                Ocb.Absolute(flame);
                m.Flame = NodeOf(flame.rectTransform);
                // :hover lifts it (no transition)
                CssStates st = CssStates.Of(el);
                st.Base(css);
                st.State(":hover", MarkTraceHoverCss);
                st.Apply(instant: true);
                m.Area.HoverChanged += on => { if (st) st.SetState(":hover", on); };
            }
            return m;
        }

        // js: scenes/meta/hall.js:203-211
        /// <summary>
        /// div.ocbg-meta-mark-key at (x, y), w × h: a small mark (22 × 22, .is-key) and its word for each look the column
        /// shows (未回収 → 回収済み → 生きて閉じた), wrapping onto a second line when they do not fit.
        /// </summary>
        internal static RectTransform MarkKey(RectTransform parent, IReadOnlyList<(string Cls, string Word)> looks, float x, float y, float w, float h)
        {
            RectTransform key = El(parent, "ocbg-meta-mark-key", MarkKeyCss);
            foreach ((string cls, string word) in looks)
            {
                RectTransform span = El(key, "span", MarkKeySpanCss);
                string css = MarkCss
                    + (cls == "is-alive" ? MarkAliveCss : "") + (cls == "is-taken" ? MarkTakenCss : "") + (cls == "is-trace" ? MarkTraceCss : "")
                    + MarkKeyMarkCss + (cls == "is-trace" ? MarkKeyTraceCss : "");
                RectTransform i = El(span, "ocbg-meta-mark is-key " + cls, css);
                Ocb.NoShrink(i);
                Span(span, word, MetaStyle.Text(MarkKeyCss + MarkKeySpanCss));
                Ocb.Flow(span, MarkKeySpanCss);
            }
            NodeOf(key).Place(x, y, w, h);
            Ocb.Flow(key, MarkKeyCss, null, w, h);
            return key;
        }

        // ---- the selected trace (hall.js:214-288) --------------------------------------------------------------------

        // js: scenes/meta/hall.js:226-256 (drawTrace's body)
        /// <summary>
        /// div.ocbg-meta-hall-trace-body inside the trace section `detail` (its first child): the selected trace (top row
        /// with how it ended and the 「n / 総数」 pager, whose it was, where, how it appears, the note, the count, the
        /// candidates, the cards kept out), or, with no trace (sel &lt; 0), the unlit wick and NoTraces filling the panel.
        /// The body scrolls (overflow-y: auto) and is as tall as its content, at most the panel's inside.
        /// </summary>
        internal static HallTraceBody TraceBody(RectTransform detail, HallView view, int sel, Lineage lineage)
        {
            Insets edge = Edge(TraceCss);
            UiNode dn = NodeOf(detail);
            float w = dn.W - edge.Horizontal, maxH = dn.H - edge.Vertical;
            RectTransform body = Ui.Div(detail, "ocbg-meta-hall-trace-body" + (sel < 0 ? " is-empty" : ""), edge.Left, edge.Top, w, maxH);
            UiNode node = NodeOf(body);
            ScrollArea area = ScrollArea.Attach(body);
            area.ShowThumb = false;
            area.SetFades(TraceMoreBelow, TraceMoreAbove, TraceMoreBoth);
            HallTouchScroll.On(area);
            RectTransform c = area.Content;
            float contentH;
            if (sel < 0)
            {
                // .is-empty: flex 1 (the whole panel), centred: i.ocbg-meta-wick-off and p.ocbg-meta-hall-none
                float gap = RowGap(TraceBodyEmptyCss);
                RectTransform ring = El(c, "ocbg-meta-wick-off", WickOffCss);
                UiNode rn = NodeOf(ring);
                TextStyle ns = MetaStyle.P(NoneCss);
                Insets np = Edge(NoneCss);
                string none = view.NoTraces ?? "";
                float tw = Math.Min(w - np.Horizontal, TextMeasure.Width(none, ns));   // align-items: center: as wide as its line
                float th = string.IsNullOrEmpty(none) ? 0f : TextMeasure.Height(none, ns, tw + Slack);
                float ph = th + np.Vertical;
                float top = (maxH - (rn.H + gap + ph)) / 2f;
                rn.Place((w - rn.W) / 2f, top);
                RectTransform p = Ui.Div(c, "ocbg-meta-hall-none", (w - tw - np.Horizontal) / 2f, top + rn.H + gap, tw + np.Horizontal, ph);
                NodeOf(p);
                Label.Make(p, none, ns, np.Left, np.Top, tw + Slack, th, "text");
                contentH = maxH;
            }
            else
            {
                contentH = TraceContent(c, view, sel, lineage, w);
            }
            node.H = sel < 0 ? maxH : Math.Min(contentH, maxH);   // min-height: 0: it shrinks to the panel and scrolls
            area.ScrollHeight = contentH;
            area.Refresh();
            return new HallTraceBody { Root = body, Node = node, Area = area };
        }

        // js: scenes/meta/hall.js:232-255
        private static float TraceContent(RectTransform c, HallView view, int sel, Lineage lineage, float w)
        {
            TraceView t = view.Traces[sel];
            float gap = RowGap(TraceBodyCss), hgap = RowGap(TraceHeaderCss);
            float y = 0f;

            // header: the top row, h3, the place, how it appears
            RectTransform top = El(c, "ocbg-meta-hall-trace-top", TraceTopCss);
            LifeEnding? ending = lineage.Traces.FirstOrDefault(x => x.Id == t.Id)?.Ending;
            EndingTag(top, ending, t.EndingLabel, t.Cleared, true);
            if (view.Traces.Count > 1)
            {
                string pager = (sel + 1).ToString(CultureInfo.InvariantCulture) + " / " + view.Traces.Count.ToString(CultureInfo.InvariantCulture);
                TextMeshProUGUI pg = Span(top, pager, MetaStyle.Text(TraceTopCss + PagerCss), "ocbg-meta-pager");
                Ocb.AutoMarginLeft(pg);
            }
            Ocb.Flow(top, TraceTopCss, null, w, null);
            NodeOf(top).Place(0f, y);
            y += NodeOf(top).H + hgap;

            Block(c, t.HeirLine, MetaStyle.P(TraceH3Css), 0f, y, w, out float h3, "h3");
            y += h3 + hgap;

            RectTransform place = PlaceRow(c, t.PlaceLine, w);
            NodeOf(place).Place(0f, y);
            y += NodeOf(place).H + hgap;

            string appearsCss = AppearsCss;
            RectTransform appears = El(c, "ocbg-meta-hall-appears" + (t.Visible ? "" : " is-waiting"), appearsCss);
            RectTransform dot = El(appears, "ocbg-meta-dot", MetaStyle.DotCss + (t.Visible ? AppearsDotCss : AppearsWaitingDotCss));
            Ocb.NoShrink(dot);
            Block(appears, t.AppearsLine, MetaStyle.P(appearsCss), 0f, 0f, w - NodeOf(dot).W - ColGap(appearsCss), out _, "text");
            Ocb.Flow(appears, appearsCss, null, w, null);
            NodeOf(appears).Place(0f, y);
            y += NodeOf(appears).H;

            // p.ocbg-meta-hall-note (a cleared life's trace only)
            if (!string.IsNullOrEmpty(t.Note))
            {
                y += gap;
                RectTransform note = El(c, "ocbg-meta-hall-note", NoteCss);
                Insets ne = Edge(NoteCss);
                Block(note, t.Note, MetaStyle.P(NoteCss), ne.Left, ne.Top, w - ne.Horizontal, out float nh, "text");
                NodeOf(note).Place(0f, y, w, nh + ne.Vertical);
                y += nh + ne.Vertical;
            }

            // p.ocbg-meta-hall-count
            y += gap;
            RectTransform count = El(c, "ocbg-meta-hall-count", CountCss);
            Insets ce = Edge(CountCss);
            Block(count, t.CountLine, MetaStyle.P(CountCss), ce.Left, ce.Top, w - ce.Horizontal, out float cth, "text");
            NodeOf(count).Place(0f, y, w, cth + ce.Vertical);
            y += cth + ce.Vertical;

            // ul.ocbg-meta-cands
            y += gap;
            RectTransform list = Ui.Div(c, "ocbg-meta-cands", 0f, y, w, 0f);
            UiNode ln = NodeOf(list);
            float ly = 0f;
            foreach (CandidateView cand in t.Candidates) ly += CandRow(list, cand, ly, w);
            ln.H = ly;
            y += ly;

            // h4 and ul.ocbg-meta-withheld: the cards the shallow rule kept out
            if (t.Withheld.Count > 0)
            {
                y += gap + MarginTop(TraceH4Css);
                Block(c, InheritanceText.WithheldHeading, MetaStyle.Text(TraceH4Css), 0f, y, w, out float h4, "h4");
                y += h4 + gap;
                RectTransform wl = Ui.Div(c, "ocbg-meta-withheld", 0f, y, w, 0f);
                UiNode wn = NodeOf(wl);
                float wy = 0f;
                foreach (WithheldView wv in t.Withheld) wy += WithheldRow(wl, wv, wy, w);
                wn.H = wy;
                y += wy;
            }
            return y;
        }

        // js: scenes/meta/hall.js:245
        /// <summary>
        /// One li of ul.ocbg-meta-cands at y: the grid 136px | minmax(0, 1fr) | auto (kind with its icon, the name cut with
        /// …, the detail at the right), at least 42 tall; a 走り書き (.is-long) is 136px | minmax(0, 1fr) on two rows, the
        /// name and the detail wrapping. Returns its height.
        /// </summary>
        private static float CandRow(RectTransform ul, CandidateView cand, float y, float w)
        {
            bool longRow = cand.Kind == CandidateKind.Scribble;
            string css = CandCss + CandColsCss + (longRow ? CandLongCss : "");
            RectTransform li = El(ul, "li is-" + cand.Kind.ToString().ToLowerInvariant() + (longRow ? " is-long" : ""), css);
            Insets edge = Edge(css);
            CssBlock rule = Ocb.Rule(css);
            float colGap = rule.Layout.ColumnGap ?? 0f, rowGap = rule.Layout.RowGap ?? 0f;
            float minInner = (Ocb.Px(rule.Layout.MinHeight) ?? 0f) - edge.Vertical;   // box-sizing: border-box
            float col2X = CandKindColumn + colGap;

            // span.ocbg-meta-cand-kind: the icon (18) and the kind, stretched to its 136px cell
            RectTransform kind = El(li, "ocbg-meta-cand-kind", CandKindCss);
            (string icon, string color) = KindIcon(cand.Kind);
            MetaUtil.Icon(kind, icon, 18f, ColorOf(color));
            Span(kind, cand.KindLabel, MetaStyle.Text(CandCss + CandKindCss));
            Ocb.Flow(kind, CandKindCss, null, CandKindColumn, null);
            UiNode kn = NodeOf(kind);

            float inner;
            if (!longRow)
            {
                TextStyle ds = MetaStyle.Text(CandCss + CandDetailCss);
                TextStyle ns = MetaStyle.Text(CandCss + CandNameCss + CandNameClipCss);
                float dw = TextMeasure.Width(cand.Detail, ds);
                float nameW = Math.Max(0f, w - col2X - colGap - dw);
                float nh = ns.LineHeightPx, dh = ds.LineHeightPx;
                inner = Math.Max(minInner, Math.Max(kn.H, Math.Max(nh, dh)));   // one row, align-items: center
                kn.Place(0f, edge.Top + (inner - kn.H) / 2f);
                Label.Make(li, cand.Name ?? "", ns, col2X, edge.Top + (inner - nh) / 2f, nameW, nh, "b");
                Label.Make(li, cand.Detail ?? "", ds, w - dw, edge.Top + (inner - dh) / 2f, dw + Slack, dh, "ocbg-meta-cand-detail");
            }
            else
            {
                // the li's word-break: auto-phrase (meta.css:438) reaches the name and the detail, which wrap here
                float col2 = Math.Max(0f, w - col2X);
                TextStyle ns = MetaStyle.P(CandCss + CandNameCss + CandNameClipCss + CandLongNameCss + CandLongNameSizeCss);
                float namePad = Ocb.BoxOf(CandLongNameCss).Padding.Top;
                float nth = string.IsNullOrEmpty(cand.Name) ? 0f : TextMeasure.Height(cand.Name, ns, col2 + Slack);
                float r1 = Math.Max(kn.H, namePad + nth);
                TextStyle ds = MetaStyle.P(CandCss + CandDetailCss + CandLongDetailCss);
                float r2 = string.IsNullOrEmpty(cand.Detail) ? 0f : TextMeasure.Height(cand.Detail, ds, col2 + Slack);
                // min-height: the auto rows share what is left (align-content: normal stretches them)
                float extra = minInner - (r1 + rowGap + r2);
                if (extra > 0f) { r1 += extra / 2f; r2 += extra / 2f; }
                kn.Place(0f, edge.Top + (r1 - kn.H) / 2f);
                Label.Make(li, cand.Name ?? "", ns, col2X, edge.Top + (r1 - namePad - nth) / 2f + namePad, col2 + Slack, nth, "b");
                float dth = string.IsNullOrEmpty(cand.Detail) ? 0f : TextMeasure.Height(cand.Detail, ds, col2 + Slack);
                Label.Make(li, cand.Detail ?? "", ds, col2X, edge.Top + r1 + rowGap + (r2 - dth) / 2f, col2 + Slack, dth, "ocbg-meta-cand-detail");
                inner = r1 + rowGap + r2;
            }
            float h = inner + edge.Vertical;
            NodeOf(li).Place(0f, y, w, h);
            return h;
        }

        // js: scenes/meta/hall.js:253
        /// <summary>
        /// One li of ul.ocbg-meta-withheld at y: the card's name (b) and why it stayed out, space-between, both shrinking
        /// by their widths when they do not fit (flex-shrink 1). Returns its height.
        /// </summary>
        private static float WithheldRow(RectTransform ul, WithheldView wv, float y, float w)
        {
            RectTransform li = El(ul, "li", WithheldLiCss);
            Insets e = Edge(WithheldLiCss);
            float inner = w - e.Horizontal, gap = ColGap(WithheldLiCss);
            TextStyle bs = MetaStyle.P(WithheldLiCss + WithheldBCss);
            TextStyle ss = MetaStyle.P(WithheldLiCss);
            float bw = TextMeasure.Width(wv.Name, bs), dw = TextMeasure.Width(wv.Detail, ss);
            float over = bw + gap + dw - inner;
            if (over > 0f && bw + dw > 0f)
            {
                float sum = bw + dw;
                bw -= over * bw / sum;
                dw -= over * dw / sum;
            }
            Block(li, wv.Name, bs, e.Left, e.Top, bw, out float bh, "b");
            Block(li, wv.Detail, ss, w - e.Right - dw, e.Top, dw, out float dh, "span");
            float h = Math.Max(bh, dh) + e.Vertical;
            NodeOf(li).Place(0f, y, w, h);
            return h;
        }

        // ---- the next heir (hall.js:290-334) -------------------------------------------------------------------------

        // js: scenes/meta/hall.js:295
        /// <summary>div.ocbg-meta-heir-gen, w wide: 「第 n 代」 and 「踏破者」 on one baseline.</summary>
        internal static RectTransform HeirGen(RectTransform box, HeirView heir, float w)
        {
            RectTransform gen = El(box, "ocbg-meta-heir-gen", HeirGenCss);
            Span(gen, heir.GenerationLabel, MetaStyle.Text(HeirGenCss), "text");
            Span(gen, InheritanceText.RoleName, MetaStyle.Text(HeirGenCss + HeirGenSpanCss), "span");
            Ocb.Flow(gen, HeirGenCss, null, w, null);
            return gen;
        }

        // js: scenes/meta/hall.js:296-299
        /// <summary>
        /// div.ocbg-meta-heir-namerow, w wide, 96 tall: the name one letter to a box (each with the amber glow), then the
        /// 襲名 seal (「襲」「名」 down a 62 × 92 amber frame). Returns the row, the letter boxes and the seal.
        /// </summary>
        internal static (RectTransform Row, List<RectTransform> Chars, RectTransform Seal) NameRow(RectTransform box, string name, float w)
        {
            RectTransform row = El(box, "ocbg-meta-heir-namerow", NameRowCss);
            RectTransform nameEl = El(row, "ocbg-meta-heir-name", NameCss);
            TextStyle s = MetaStyle.Text(NameCss + NameSpanCss);
            var chars = new List<RectTransform>();
            float x = 0f;
            foreach (string ch in MetaTimelines.Chars(name))
            {
                // span (display: inline-block): its glyph and the letter-spacing after it
                float cw = TextMeasure.Width(ch, s);
                RectTransform span = Ui.Div(nameEl, "span", x, 0f, cw, s.LineHeightPx);
                NodeOf(span);
                TextMeshProUGUI l = Label.Make(span, ch, s, 0f, 0f, null, null, "text");
                Glow(l);
                chars.Add(span);
                x += cw;
            }
            NodeOf(nameEl).Place(0f, 0f, x, s.LineHeightPx);
            Ocb.NoShrink(nameEl);
            RectTransform seal = El(row, "ocbg-meta-seal", SealCss);
            TextStyle ss = MetaStyle.Text(SealCss);
            foreach (string c in MetaText.NameSeal) Span(seal, c, ss);
            Ocb.Flow(seal, SealCss);
            Ocb.NoShrink(seal);
            Ocb.Flow(row, NameRowCss, null, w, null);
            return (row, chars, seal);
        }

        // js: scenes/meta/hall.js:303
        /// <summary>
        /// div.ocbg-meta-heir-attire (262 wide): h4 「装束」, the tier's name, why (text-wrap: balance), and the three parts
        /// with their swatches (a dashed empty one when the part has no colour).
        /// </summary>
        internal static RectTransform Attire(RectTransform box, HeirView heir)
        {
            RectTransform el = El(box, "ocbg-meta-heir-attire", AttireCss);
            float w = NodeOf(el).W, gap = RowGap(AttireCss), y = 0f;
            Block(el, InheritanceText.AttireHeading, MetaStyle.Text(AttireCss + AttireH4Css), 0f, y, w, out float hh, "h4");
            y += hh + gap;
            Block(el, heir.TierName, MetaStyle.Text(AttireCss + TierNameCss), 0f, y, w, out float th, "ocbg-meta-heir-tier");
            y += th + gap;
            Block(el, heir.TierReason, MetaStyle.P(AttireCss + ReasonCss + BalanceCss, balance: true), 0f, y, w, out float rh, "ocbg-meta-heir-reason");
            y += rh + gap + MarginTop(AttireUlCss);

            RectTransform ul = Ui.Div(el, "ul", 0f, y, w, 0f);
            UiNode un = NodeOf(ul);
            float ugap = RowGap(AttireUlCss), uy = 0f, lgap = ColGap(AttireLiCss);
            float partW = Ocb.Px(Ocb.Rule(PartCss).Layout.Width) ?? 0f;
            TextStyle ls = MetaStyle.P(AttireCss + AttireLiCss);
            for (int i = 0; i < heir.Attire.Count; i++)
            {
                AttirePart a = heir.Attire[i];
                if (i > 0) uy += ugap;
                RectTransform li = El(ul, "li", AttireLiCss);
                // i.ocbg-meta-swatch (.is-none), its colour as the inline background
                string swatchCss = SwatchCss + (a.Color != null ? "" : SwatchNoneCss) + (a.Color != null ? "background: " + a.Color + ";" : "");
                RectTransform swatch = El(li, "ocbg-meta-swatch" + (a.Color != null ? "" : " is-none"), swatchCss);
                Ocb.NoShrink(swatch);
                TextMeshProUGUI part = Block(li, a.Part, ls, 0f, 0f, partW, out _, "ocbg-meta-part");
                Ocb.NoShrink(part);
                Block(li, a.Text, ls, 0f, 0f, w - NodeOf(swatch).W - partW - 2f * lgap, out _, "span");
                Ocb.Flow(li, AttireLiCss, null, w, null);
                NodeOf(li).Place(0f, uy);
                uy += NodeOf(li).H;
            }
            un.H = uy;
            y += uy;
            NodeOf(el).Place(0f, 0f, w, y);
            return el;
        }

        // js: scenes/meta/hall.js:312
        /// <summary>div.ocbg-meta-heir-figure (right 16, top 166 of the box, 140 × 226): the foot light (180, at −20, 196) and the swordsman.</summary>
        internal static RectTransform Figure(RectTransform box, float boxW)
        {
            CssBlock r = Ocb.Rule(FigureCss);
            float fw = Ocb.Px(r.Layout.Width) ?? 0f, fh = Ocb.Px(r.Layout.Height) ?? 0f;
            float fx = boxW - (Ocb.Px(r.Layout.Right) ?? 0f) - fw, fy = Ocb.Px(r.Layout.Top) ?? 0f;
            RectTransform fig = Ui.Div(box, "ocbg-meta-heir-figure", fx, fy, fw, fh);
            NodeOf(fig);
            // js: scenes/meta/hall.js:313-314 O.FootLight(180, false) placed at (-20, 196)
            GradientGraphic foot = Ocb.FootLight(fig, 180f, false);
            Ui.Place(foot.rectTransform, -20f, 196f);
            Ocb.Figure(fig, "swordsman");
            return fig;
        }

        // js: scenes/meta/hall.js:319-333
        /// <summary>
        /// div.ocbg-meta-heir-survivor, w wide: h4 (the heading), whose survivor, what the bonus gives (each with an amber
        /// plus) and for how long; spent (.is-spent: the bonus was used up, R-14) has no warm colour, no icons, no duration.
        /// </summary>
        internal static RectTransform Survivor(RectTransform box, string heading, string line, IReadOnlyList<string> lines, string duration, bool spent, float w)
        {
            string css = SurvivorCss + SurvivorBottomCss + (spent ? SurvivorSpentCss : "");
            RectTransform el = El(box, "ocbg-meta-heir-survivor" + (spent ? " is-spent" : ""), css);
            Insets e = Edge(css);
            float inner = w - e.Horizontal, gap = RowGap(css), y = e.Top;
            Block(el, heading, MetaStyle.Text(css + SurvivorH4Css), e.Left, y, inner, out float hh, "h4");
            y += hh + gap;
            Block(el, line, MetaStyle.P(css + SurvivorLineCss), e.Left, y, inner, out float lh, "ocbg-meta-heir-survivor-line");
            y += lh + gap;
            // ul (display: block): the li stack without a gap
            RectTransform ul = Ui.Div(el, "ul", e.Left, y, inner, 0f);
            UiNode un = NodeOf(ul);
            TextStyle ls = MetaStyle.P(css + SurvivorLiCss);
            float liGap = ColGap(SurvivorLiCss), uy = 0f;
            foreach (string l in lines ?? Array.Empty<string>())
            {
                RectTransform li = El(ul, "li", SurvivorLiCss);
                float tw = inner;
                if (!spent)
                {
                    PathGraphic plus = MetaUtil.Icon(li, "plus", 18f, DsTokens.Amber);
                    tw -= 18f + liGap;
                    _ = plus;
                }
                Block(li, l, ls, 0f, 0f, tw, out _, "text");
                Ocb.Flow(li, SurvivorLiCss, null, inner, null);
                NodeOf(li).Place(0f, uy);
                uy += NodeOf(li).H;
            }
            un.H = uy;
            y += uy;
            if (!spent && !string.IsNullOrEmpty(duration))
            {
                y += gap;
                Block(el, duration, MetaStyle.P(css + DurationCss), e.Left, y, inner, out float dh, "ocbg-meta-heir-duration");
                y += dh;
            }
            NodeOf(el).Place(0f, 0f, w, y + e.Bottom);
            return el;
        }
    }

    /// <summary>One band of the seven-layer column (div.ocbg-meta-stratum) and its state classes (.is-sel, .is-live:hover).</summary>
    internal sealed class HallStratum
    {
        public int Layer;
        public RectTransform Root;
        public UiNode Node;

        /// <summary>The padding box (overflow: hidden), where the veil, the label and the marks are.</summary>
        public RectTransform Clip;
        public float ClipW, ClipH;

        /// <summary>div.ocbg-meta-stratum-marks.</summary>
        public RectTransform Marks;
        public CssStates States;

        /// <summary>s.traceIndex − 1: the trace the whole band answers for (.is-live), −1 when none lies there.</summary>
        public int TraceIndex = -1;

        /// <summary>The mark whose life left that trace (a click inside it is the mark's own).</summary>
        public HallMark LiveMark;

        public ClickArea Area;
        public bool Selected { get; private set; }
        public bool Hovered { get; private set; }
        public bool Live => TraceIndex >= 0;

        // js: scenes/meta/hall.js:194-198
        /// <summary>The band answers the pointer for the trace lying there: .is-live, its click and hover.</summary>
        public void MakeLive(int traceIndex, HallMark mark, Action click)
        {
            TraceIndex = traceIndex;
            LiveMark = mark;
            BoxGraphic box = Root.GetComponent<BoxGraphic>();
            Area = ClickArea.On(box ? (Component)box : Root);
            Area.Click += _ => click?.Invoke();
            Area.HoverChanged += on =>
            {
                Hovered = on;
                Restyle();
            };
        }

        /// <summary>
        /// .is-sel on or off (box-shadow / border-color .2s); instant: the class is there before the band is first drawn
        /// (mountHall's first drawTrace), so nothing transitions.
        /// </summary>
        public void SetSel(bool on, bool instant = false)
        {
            if (Selected == on) return;
            Selected = on;
            Restyle(instant);
        }

        private void Restyle(bool instant = false)
        {
            if (!Root || States == null) return;
            States.SetState(".is-sel", Selected, false);
            States.SetState(":hover", Live && Hovered && !Selected, false);   // .is-live:hover:not(.is-sel)
            States.Apply(instant);
        }

        /// <summary>The marks' row laid out and placed at right 12, bottom 12 of the padding box.</summary>
        public void LayMarks()
        {
            if (!Marks) return;
            Ocb.Flow(Marks, HallParts.StratumMarksCss);
            CssBlock r = Ocb.Rule(HallParts.StratumMarksCss);
            UiNode n = UiNode.Of(Marks);
            n.Place(ClipW - (Ocb.Px(r.Layout.Right) ?? 0f) - n.W, ClipH - (Ocb.Px(r.Layout.Bottom) ?? 0f) - n.H);
        }
    }

    /// <summary>A life's mark on its stratum (button.ocbg-meta-mark) and, on an untaken trace, its flame.</summary>
    internal sealed class HallMark
    {
        public RectTransform Root;
        public BoxGraphic Box;
        public ClickArea Area;
        public HallStratum Stratum;

        /// <summary>The trace it selects (view.Traces index), −1 when its life left none still lying there.</summary>
        public int TraceIndex = -1;

        /// <summary>i.ocbg-meta-flame (null unless .is-trace).</summary>
        public UiNode Flame;
    }

    /// <summary>The selected trace's body (div.ocbg-meta-hall-trace-body): its node (the select fade moves it) and its scroll.</summary>
    internal sealed class HallTraceBody
    {
        public RectTransform Root;
        public UiNode Node;
        public ScrollArea Area;
    }

    /// <summary>
    /// overflow-y: auto under a finger: a touch drag scrolls the box, as the browser does (hall.js:144 「wheel or drag to
    /// read on」). A mouse drag does not (desktop Chrome scrolls with the wheel); the wheel is ScrollArea's.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    internal sealed class HallTouchScroll : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private ScrollArea _area;
        private bool _dragging;

        public static HallTouchScroll On(ScrollArea area)
        {
            HallTouchScroll t = area.GetComponent<HallTouchScroll>();
            if (!t) t = area.gameObject.AddComponent<HallTouchScroll>();
            t._area = area;
            return t;
        }

        private static bool IsTouch(PointerEventData e) =>
            e is ExtendedPointerEventData x && x.pointerType == UIPointerType.Touch;

        public void OnBeginDrag(PointerEventData e)
        {
            _dragging = _area && IsTouch(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging || !_area) return;
            TrialStage st = TrialStage.Current;
            float dy = st ? st.ToStage(e.position).y - st.ToStage(e.position - e.delta).y : -e.delta.y;
            _area.ScrollTop = _area.ScrollTop - dy;
        }

        public void OnEndDrag(PointerEventData e)
        {
            _dragging = false;
        }
    }
}
