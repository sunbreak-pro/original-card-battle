// ExploreScene — the result flows (port of ExploreScene.js:1057 eventResultFlow, :1600-1727 resultFlow, :1729-1982 the
// trace and the taken card, :1984-2135 training; explore.css:257-348, 404-435; explore.md §5.13-§5.15). Owned by
// EX-RESULTS.
//
// The JS appends each card's content to the plate (openPlate) and lets the browser lay it out: the plate is a flex
// column, gap 14, padding 36 44 32 inside its 1px border (explore.css:223), and every block in it is a flex row, a grid
// or a column of its own. Here every block is measured and placed the way that CSS places it (ResLayPlate and the builders
// below), and an auto-height card gets its height before FitPlate centres it (the JS fitPlate reads offsetHeight).
// Heights follow Blink's line boxes: a line that holds a digit run in the number face (IBM Plex Mono 600) is taller
// than line-height when the faces' metrics differ (ResNumLineBox; the reference shots 701, 726 and 836 measure it).
//
// Nothing here decides a rule: every line and number comes from the expedition (Offer, OfferFits, PendingResult,
// Learn, Train …), the bridges (TraceGain, MasteryView, Inheritance.PickState, ExploreCardFace) or the scene text
// table (ExploreText).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Trial.Explore
{
    public sealed partial class ExploreScene
    {
        // ================================================================== the stylesheet (explore.css, verbatim)

        /// <summary>explore.css:223 .ocbg-explore-plate (its box is the plates unit's; its layout numbers are below).</summary>
        private const string ResPlateCss = "pointer-events: auto; display: flex; flex-direction: column; gap: 14px; padding: 36px 44px 32px; border-radius: var(--radius-panel); box-shadow: var(--shadow-tip); animation: ocbg-explore-plate-in 300ms cubic-bezier(.22,.61,.36,1) both;";

        /// <summary>explore.css:227 .ocbg-explore-plate h2.</summary>
        private const string ResH2Css = "margin: 0; font: 800 44px/54px var(--font-display); letter-spacing: .06em;";

        /// <summary>explore.css:229 .ocbg-explore-plate-kicker.</summary>
        private const string ResKickerCss = "display: flex; align-items: center; gap: 10px; font: 700 18px/24px var(--font-display); letter-spacing: .08em;";

        /// <summary>explore.css:230 .ocbg-explore-plate-buttons.</summary>
        private const string ResButtonsCss = "margin-top: auto; padding-top: 10px; display: flex; align-items: center; justify-content: flex-end; gap: 16px;";

        /// <summary>explore.css:231 .ocbg-explore-plate-buttons .ocbg-btn.</summary>
        private const string ResButtonsBtnCss = "min-width: 220px; display: inline-flex; align-items: center; justify-content: center; gap: 10px;";

        /// <summary>explore.css:235-237 .is-washi .ocbg-btn (.is-primary, :hover).</summary>
        private const string ResWashiBtnCss = "background: var(--panel-solid);";
        private const string ResWashiPrimaryCss = "background: color-mix(in srgb, var(--amber) 24%, var(--panel-solid));";
        private const string ResWashiPrimaryHoverCss = "background: color-mix(in srgb, var(--amber) 36%, var(--panel-solid));";

        /// <summary>explore.css:56 .is-washi .ocbg-btn:focus-visible.</summary>
        private const string ResWashiFocusCss = "outline: 3px solid var(--washi-ink); outline-offset: 3px;";

        // ---- the result and the reward pick (explore.css:258-298)

        /// <summary>explore.css:259 .ocbg-explore-plate.is-compact .ocbg-explore-result-side.</summary>
        private const string ResCompactSideCss = "display: grid; grid-template-columns: 1fr 1fr; column-gap: 40px; row-gap: 14px; padding: 4px 0 6px; border-right: 0;";

        /// <summary>explore.css:260 .ocbg-explore-plate.is-result .ocbg-explore-plate-buttons.</summary>
        private const string ResResultButtonsCss = "padding-top: 18px;";

        /// <summary>explore.css:261 .ocbg-explore-result-head h2.</summary>
        private const string ResHeadH2Css = "white-space: nowrap;";

        /// <summary>explore.css:262 .ocbg-explore-result-head.</summary>
        private const string ResHeadCss = "display: flex; align-items: center; justify-content: space-between; gap: 24px; padding-bottom: 14px; border-bottom: 1px solid color-mix(in srgb, var(--washi-ink) 25%, transparent);";

        /// <summary>explore.css:263 .ocbg-explore-result-stats.</summary>
        private const string ResStatsCss = "display: flex; gap: 14px;";

        /// <summary>explore.css:264 .ocbg-explore-result-stats &gt; span.</summary>
        private const string ResStatCss = "white-space: nowrap; display: inline-flex; align-items: center; gap: 8px; padding: 6px 14px; font: 700 22px/28px var(--font-body); color: var(--ink); background: var(--panel-solid); border-radius: var(--radius-panel);";

        /// <summary>explore.css:265 .ocbg-explore-result-body.</summary>
        private const string ResBodyCss = "display: grid; grid-template-columns: 300px 1fr; column-gap: 36px; padding-top: 4px;";

        /// <summary>explore.css:266 .ocbg-explore-result-side.</summary>
        private const string ResSideCss = "display: flex; flex-direction: column; gap: 16px; padding: 6px 28px 0 0; border-right: 1px solid color-mix(in srgb, var(--washi-ink) 22%, transparent); overflow: hidden;";

        /// <summary>explore.css:267 .ocbg-explore-result-group.</summary>
        private const string ResGroupCss = "display: flex; flex-direction: column; gap: 6px;";

        /// <summary>explore.css:268 .ocbg-explore-result-side h4.</summary>
        private const string ResSideH4Css = "margin: 0 0 2px; font: 700 18px/24px var(--font-display); letter-spacing: .06em;";

        /// <summary>explore.css:269 .ocbg-explore-result-side p.</summary>
        private const string ResSidePCss = "margin: 0; display: flex; align-items: center; gap: 8px; font: 400 18px/26px var(--font-body);";

        /// <summary>explore.css:270 .ocbg-explore-result-side p.is-quiet.</summary>
        private const string ResQuietCss = "opacity: .7;";

        /// <summary>explore.css:272-274 the rank-up row, its icon, its 段 run.</summary>
        private const string ResRankUpCss = "align-items: flex-start;";
        private const string ResRankUpSvgCss = "flex: none; margin-top: 5px;";
        private const string ResRankUpStepsCss = "display: inline-block; margin-left: .5em; white-space: nowrap;";

        /// <summary>explore.css:276 .ocbg-explore-result-note.</summary>
        private const string ResNoteCss = "display: block; margin: 0; font: 400 16px/23px var(--font-body);";

        /// <summary>explore.css:277 .ocbg-explore-plate.is-compact .ocbg-explore-result-note.</summary>
        private const string ResCompactNoteCss = "grid-column: 1 / -1;";

        /// <summary>explore.css:278 .ocbg-explore-result-main .ocbg-explore-result-note.</summary>
        private const string ResMainNoteCss = "margin-top: -10px;";

        /// <summary>explore.css:280-286 the 受け取った遺産 card.</summary>
        private const string ResTakenSideCss = "display: flex; flex-direction: column;";
        private const string ResTakenRowCss = "display: grid; grid-template-columns: 22px 150px 1fr auto; column-gap: 12px; align-items: center;";
        private const string ResTakenStrongCss = "font: 700 20px/26px var(--font-body);";
        private const string ResTakenItemCss = "display: flex; flex-direction: column; gap: 2px;";
        private const string ResTakenGainCss = "display: block; margin: 0 0 4px 34px; font: 400 16px/22px var(--font-body);";
        private const string ResNothingCss = "font-weight: 700;";

        /// <summary>explore.css:287-289 .ocbg-explore-result-main, its h3, the cards row.</summary>
        private const string ResMainCss = "display: flex; flex-direction: column; gap: 18px;";
        private const string ResMainH3Css = "margin: 0; font: 700 26px/34px var(--font-display); letter-spacing: .06em;";
        private const string ResCardsCss = "display: flex; justify-content: center; gap: 32px; padding: 18px 0 44px;";

        /// <summary>explore.css:290-297 a card of the pick: its deal, its frame, its key.</summary>
        private const string ResPickCss = "position: relative; width: 216px; height: 304px; animation: ocbg-explore-deal 460ms cubic-bezier(.22,.61,.36,1) both; transition: transform 120ms cubic-bezier(.4,0,.2,1);";
        private const string ResPickChosenCss = "transform: translateY(-10px);";
        private const string ResPickFrameCss = "position: absolute; left: -10px; top: -10px; opacity: 0; transition: opacity 140ms; pointer-events: none;";
        private const string ResPickFrameChosenCss = "opacity: 1;";
        private const string ResPickKeyCss = "position: absolute; left: 50%; bottom: -38px; transform: translateX(-50%);";
        private const string ResPickKeyChosenCss = "border-color: var(--amber);";

        /// <summary>explore.css:298 .ocbg-explore-cardstub.</summary>
        private const string ResCardStubCss = "width: 216px; height: 304px; display: flex; align-items: center; justify-content: center; background: var(--card); color: var(--ink); border-radius: var(--radius-panel); font: 700 22px/28px var(--font-display);";

        // ---- the trace (explore.css:300-328)

        private const string ResTraceHeirCss = "margin: -6px 0 0; font: 700 22px/30px var(--font-display);";
        private const string ResTraceNoteCss = "margin: 0; font: 400 18px/26px var(--font-body);";
        private const string ResTraceListCss = "flex: 1; min-height: 0; overflow: auto; display: flex; flex-direction: column; border-top: 1px solid color-mix(in srgb, var(--washi-ink) 25%, transparent);";
        private const string ResTraceListH4Css = "margin: 14px 0 4px; font: 700 18px/24px var(--font-display);";
        private const string ResTraceRowCss = "display: grid; grid-template-columns: 28px 150px 1fr auto; align-items: center; column-gap: 14px; min-height: 52px; padding: 6px 8px; text-align: left; color: var(--washi-ink); background: none; border: 0; border-bottom: 1px solid color-mix(in srgb, var(--washi-ink) 15%, transparent); cursor: pointer; font: 400 18px/24px var(--font-body);";
        private const string ResTraceRowHoverCss = "background: color-mix(in srgb, var(--washi-ink) 6%, transparent);";
        private const string ResTraceRowStrongCss = "font: 700 20px/26px var(--font-body);";
        private const string ResTraceRowLockedCss = "opacity: .5;";
        private const string ResTraceRowCursorCss = "background: color-mix(in srgb, var(--washi-ink) 8%, transparent); box-shadow: inset 3px 0 0 var(--amber);";
        private const string ResTraceRowFocusCss = "outline: 2px solid var(--amber); outline-offset: -2px;";
        private const string ResTraceRowWithheldCss = "grid-template-columns: 28px 1fr auto; opacity: .7; cursor: default;";
        private const string ResTraceGainCss = "grid-column: 3 / -1; margin-top: 2px; font: 400 16px/22px var(--font-body); color: var(--washi-ink);";
        private const string ResTraceLeadWarnCss = "display: flex; align-items: center; gap: 8px; font-weight: 700;";
        private const string ResTraceWithheldCursorCss = "opacity: .9;";
        private const string ResTraceKindCss = "justify-self: start; padding: 1px 10px; font: 700 16px/22px var(--font-body); border: 1px solid color-mix(in srgb, var(--washi-ink) 40%, transparent); border-radius: var(--radius-tag); white-space: nowrap;";
        private const string ResCheckCss = "width: 22px; height: 22px; border: 2px solid color-mix(in srgb, var(--washi-ink) 55%, transparent); border-radius: 50%;";
        private const string ResCheckOnCss = "border-color: var(--amber); background: radial-gradient(closest-side, var(--amber) 60%, transparent 66%);";
        private const string ResCheckWithheldCss = "border-style: dashed;";
        private const string ResTraceCountCss = "margin-right: auto; font: 700 24px/30px var(--font-body);";

        /// <summary>explore.css:321-323 the scroll cue of the trace and training lists (the mask-image of each state).</summary>
        private const string ResListMoreBelow = "linear-gradient(180deg, var(--ground) 82%, transparent)";
        private const string ResListMoreAbove = "linear-gradient(0deg, var(--ground) 82%, transparent)";
        private const string ResListMoreBoth = "linear-gradient(180deg, transparent, var(--ground) 14%, var(--ground) 86%, transparent)";

        // ---- the training pick (explore.css:331-348)

        private const string ResTrainKickerCss = "color: var(--ink);";
        private const string ResTrainLeadCss = "margin: -4px 0 0; font: 400 20px/28px var(--font-body);";
        private const string ResTrainBodyCss = "flex: 1; min-height: 0; display: grid; grid-template-columns: 1fr 216px; column-gap: 40px; align-items: start;";
        private const string ResTrainListCss = "display: flex; flex-direction: column; max-height: 470px; overflow: auto; border-top: 1px solid var(--rule);";
        private const string ResTrainRowCss = "display: grid; grid-template-columns: 28px 1fr 48px 132px 96px; align-items: center; column-gap: 14px; min-height: 46px; padding: 4px 12px; color: var(--ink); background: none; border: 0; border-bottom: 1px solid var(--rule); text-align: left; cursor: pointer; font: 400 18px/24px var(--font-body);";
        private const string ResTrainRowStrongCss = "font: 700 20px/26px var(--font-display);";
        private const string ResTrainRowHoverCss = "background: color-mix(in srgb, var(--ink) 6%, transparent);";
        private const string ResTrainRowChosenCss = "background: color-mix(in srgb, var(--amber) 16%, transparent); box-shadow: inset 3px 0 0 var(--amber);";
        private const string ResTrainCopiesCss = "font: 400 18px/24px var(--font-body);";
        private const string ResRankCss = "justify-self: end; padding: 1px 10px; font: 700 16px/22px var(--font-body); border: 1px solid var(--amber); border-radius: var(--radius-chip-self); white-space: nowrap;";
        private const string ResRankZeroCss = "border-color: var(--steel); font-weight: 400;";
        private const string ResTrainPreviewCss = "position: relative; width: 216px; height: 304px; border: 1px dashed var(--steel); border-radius: var(--radius-panel);";
        private const string ResTrainPreviewCardAnim = "ocbg-explore-fade 160ms ease-out both";
        private const string ResTrainTicksCss = "justify-self: end; font: 400 18px/24px var(--font-body); white-space: nowrap;";
        private const string ResTrainTicksQuietCss = "font-size: 16px;";
        private const string ResTrainAboutCss = "position: absolute; left: -1px; top: 318px; width: 236px; display: flex; flex-direction: column; gap: 4px;";
        private const string ResTrainAboutPCss = "margin: 0; font: 400 18px/26px var(--font-body);";

        // ---- trial mode's event result (explore.css:405, 434-435)

        private const string ResKickerSubCss = "margin-left: 10px; padding-left: 12px; border-left: 1px solid color-mix(in srgb, var(--washi-ink) 30%, transparent); font-weight: 400;";
        private const string ResEventCompactSideCss = "display: flex; grid-template-columns: none;";
        private const string ResEventSidePCss = "font-size: 22px; line-height: 32px;";

        /// <summary>
        /// Chrome's own :focus-visible ring on a button the stylesheet gives none (the training rows: `outline: auto`):
        /// a 2px dark ring with a 1px white halo, on the border edge.
        /// </summary>
        private const string ResChromeFocusRingCss = "outline: 2px solid #101010; outline-offset: 0px; box-shadow: 0 0 0 3px #ffffff;";

        // ================================================================== the layout numbers of that stylesheet

        /// <summary>explore.css:223: padding 36 44 32 inside the skins' 1px border, gap 14.</summary>
        private const float ResPadT = 36f, ResPadR = 44f, ResPadB = 32f, ResPadL = 44f, ResBorder = 1f, ResGap = 14f;

        /// <summary>The cards' widths (ExploreScene.js:1083, 1644, 1808, 1962, 2065).</summary>
        private const float ResWide = 1180f, ResCompact = 840f, ResEventCompact = 760f, ResTraceW = 1100f, ResTraceH = 760f, ResTrainW = 1060f, ResTrainH = 780f;

        /// <summary>The design system's card (OCB.Card) and the pick's frame (TargetFrame 236 × 324, hook 24, width 3).</summary>
        private const float ResCardW = 216f, ResCardH = 304f, ResFrameW = 236f, ResFrameH = 324f;

        /// <summary>
        /// The double-click time a dblclick needs (Windows' GetDoubleClickTime default, which Chrome uses; the battle's
        /// layout.js:162 DoubleClickMs is the same 500).
        /// </summary>
        private const float ResDoubleClickMs = 500f;

        // ================================================================== trial: what an event did

        // js: scenes/explore/ExploreScene.js:1058
        /// <summary>Trial: what an event did (EventOutcome), and a chest's card pick (Expedition.Offer / Learn).</summary>
        private Promise EventResultFlow(EventOutcome outcome)
        {
            var src = new PromiseSource();
            if (outcome == null)
            {
                src.Resolve();
                return src.Promise;
            }
            var book = Expedition.Mastery;
            IReadOnlyList<string> pile = Expedition.Offer;
            List<string> offer = outcome.Cards.Count > 0 && pile.Count > 0 && outcome.Cards.All(id => pile.Contains(id))
                ? pile.ToList() : new List<string>();
            IReadOnlyList<bool> fits = offer.Count > 0 ? Expedition.OfferFits ?? (IReadOnlyList<bool>)Array.Empty<bool>() : (IReadOnlyList<bool>)Array.Empty<bool>();
            int chosen = -1;
            bool closed = false;
            var picks = new List<ResultsPick>();
            OcbgButton learnBtn = null;

            void Finish(bool learn)
            {
                if (closed) return;
                closed = true;
                if (learn && chosen >= 0) { if (Expedition.Learn(offer[chosen])) Sfx("cardAdded"); }
                else if (offer.Count > 0) Sfx("uiCancel");
                Expedition.CloseResult();
                Persist();
                ClosePlate();
                Render();
                Timeout(Dur(240), () => src.TryResolve());
            }

            void Select(int i)
            {
                chosen = i;
                ResSetChosen(picks, i);
                if (learnBtn != null) learnBtn.Disabled = chosen < 0;
                Sfx("rewardPick");
            }

            bool Keys(KeyEvent e)
            {
                if (offer.Count > 0 && (e.Key == "ArrowLeft" || e.Key == "ArrowRight"))
                {
                    int d = e.Key == "ArrowLeft" ? -1 : 1;
                    Select(chosen < 0 ? (d > 0 ? 0 : offer.Count - 1) : (chosen + d + offer.Count) % offer.Count);
                    return true;
                }
                int n = ResKeyNumber(e);
                if (offer.Count > 0 && n >= 1 && n <= offer.Count) { Select(n - 1); return true; }
                if (e.Key == "Enter") { if (offer.Count == 0) Finish(false); else if (chosen >= 0) Finish(true); return true; }
                if (e.Key == "Escape") { Finish(false); return true; }
                return false;
            }

            bool compact = offer.Count == 0;
            float w = compact ? ResEventCompact : ResWide;
            RectTransform el = OpenPlate(new PlateOpts
            {
                Cls = "is-washi is-result is-event" + (compact ? " is-compact" : ""),
                W = w,
                Ht = null,
                Kind = ExploreStyle.PlateResult,
                Legend = offer.Count > 0
                    ? new[] { LegendEntry.Of("←→", ExploreText.KeySelect), LegendEntry.Of(ExploreText.KeyEnter, ExploreText.LearnButton), LegendEntry.Of("Esc", ExploreText.SkipButton) }
                    : new[] { LegendEntry.Of(ExploreText.KeyEnter, ExploreText.BackToMap) },
                Keys = Keys,
            });
            float innerW = ResInnerW(w);
            Rgba ink = ExploreStyle.WashiInk;

            // what the event changed, one line each (.is-event .ocbg-explore-result-side p: 22/32, the notes too)
            TextStyle lineStyle = ResFont(ResSidePCss + ResEventSidePCss, ink);
            TextStyle noteStyle = ResFont(ResNoteCss + ResEventSidePCss, ink);
            var rows = new List<Func<RectTransform, float, RectTransform>>();
            foreach (string l in outcome.Lines ?? Array.Empty<string>())
            {
                string line = l ?? "";
                rows.Add((parent, width) => ResEventLineRow(parent, width, line, ink, lineStyle));
                // the pages have no content yet (#107): the reward says so where it is given
                if (line.StartsWith("希少な頁", StringComparison.Ordinal))
                    rows.Add((parent, width) => ResNoteRow(parent, width, ExploreText.PagesNote, false, noteStyle));
            }
            var side = new List<ResultsSideItem> { new ResultsSideItem { Build = (parent, width) => ResSideGroup(parent, width, null, rows, ink) } };

            var blocks = new List<ResultsBlock>
            {
                ResKicker(el, "Event", 26f, ink, ExploreText.EventKicker, false, ResFont(ResKickerCss, ink), true, Screen.LayerTitle),
                ResHead(el, innerW, outcome.Name, true, ink),
            };
            if (offer.Count > 0)
            {
                string note = fits.Any(f => !f) ? ExploreText.OfferBenched(Expedition.DeckMax) : null;
                blocks.Add(ResBody(el, innerW, side, (main, width) =>
                    ResMain(main, width, ExploreText.PickOne, offer, true, book, picks, Select, () => Finish(true), note)));
            }
            else
            {
                // .is-event.is-compact: the side is a column (row-gap 14 of the compact grid)
                blocks.Add(ResCompactSide(el, innerW, side, false));
            }
            RectTransform row = Ui.Div(el, "ocbg-explore-plate-buttons");
            OcbgButton[] buttons;
            if (offer.Count > 0)
            {
                OcbgButton skip = ResPlateButton(row, ExploreText.SkipButton, false, false, true, () => Finish(false));
                learnBtn = ResPlateButton(row, ExploreText.LearnButton, true, false, true, () => Finish(true));
                learnBtn.Disabled = true;
                buttons = new[] { skip, learnBtn };
            }
            else
            {
                buttons = new[] { ResPlateButton(row, ExploreText.BackToMap, true, false, true, () => Finish(false)) };
            }
            blocks.Add(ResLayButtons(row, innerW, 18f, null, 0f, buttons));
            ResLayAutoPlate(el, blocks);
            Sfx(offer.Count > 0 ? "rewardReveal" : "eventResolve", 80);
            return src.Promise;
        }

        // ================================================================== after a battle: the result and the reward pick

        // js: scenes/explore/ExploreScene.js:1602
        /// <summary>
        /// resultFlow(res): after a battle, the result and the reward pick. `res` is Expedition.PendingResult (trial: Back
        /// plus Raised / PileSpent / Benched / DeckMax) or null with `back` alone (the JS { Back }).
        /// </summary>
        private Promise ResultFlow(BattleReturn back, ResultCard res = null)
        {
            var src = new PromiseSource();
            BattleReturn bk = res != null && res.Back != null ? res.Back : back;
            if (bk == null)
            {
                src.Resolve();
                return src.Promise;
            }
            var book = Expedition.Mastery;
            var offer = new List<string>(Expedition.Offer);
            IReadOnlyList<bool> fits = offer.Count > 0 ? Expedition.OfferFits ?? (IReadOnlyList<bool>)Array.Empty<bool>() : (IReadOnlyList<bool>)Array.Empty<bool>();
            int? chosen = offer.Count > 0 ? -1 : (int?)null;
            bool closed = false;
            var picks = new List<ResultsPick>();
            OcbgButton learnBtn = null;

            void Finish(bool learn)
            {
                if (closed) return;
                closed = true;
                if (learn && chosen != null && chosen >= 0)
                {
                    if (Expedition.Learn(offer[chosen.Value])) Sfx("cardAdded");
                }
                else Sfx("uiCancel");
                Expedition.CloseResult();
                Persist();
                ClosePlate();
                Render();
                if (Session.State.Phase == RunPhase.LayerCleared) Sfx("layerCleared", 200);
                Timeout(Dur(240), () => src.TryResolve());
            }

            void Select(int i)
            {
                chosen = i;
                ResSetChosen(picks, i);
                if (learnBtn != null) learnBtn.Disabled = chosen < 0;
                Sfx("rewardPick");
            }

            bool Keys(KeyEvent e)
            {
                if (e.Key == "ArrowLeft" || e.Key == "ArrowRight")
                {
                    if (offer.Count == 0) return true;
                    int d = e.Key == "ArrowLeft" ? -1 : 1;
                    int c = chosen ?? -1;
                    Select(c < 0 ? (d > 0 ? 0 : offer.Count - 1) : (c + d + offer.Count) % offer.Count);
                    return true;
                }
                int n = ResKeyNumber(e);
                if (offer.Count > 0 && n >= 1 && n <= offer.Count) { Select(n - 1); return true; }
                if (e.Key == "Enter")
                {
                    if (offer.Count == 0) Finish(false);
                    else if (chosen >= 0) Finish(true);
                    return true;
                }
                if (e.Key == "Escape") { Finish(false); return true; }
                return false;
            }

            IReadOnlyList<string> showCards = offer.Count > 0 ? offer : bk.Learned;
            bool compact = showCards.Count == 0;
            float w = compact ? ResCompact : ResWide;
            RectTransform el = OpenPlate(new PlateOpts
            {
                Cls = "is-washi is-result" + (compact ? " is-compact" : ""),
                W = w,
                Ht = null,
                Kind = ExploreStyle.PlateResult,
                Keys = Keys,
                Legend = offer.Count > 0
                    ? new[] { LegendEntry.Of("←→", ExploreText.KeySelect), LegendEntry.Of(ExploreText.KeyEnter, ExploreText.LearnButton), LegendEntry.Of("Esc", ExploreText.SkipButton) }
                    : new[] { LegendEntry.Of(ExploreText.KeyEnter, ExploreText.BackToMap) },
            });
            float innerW = ResInnerW(w);
            Rgba ink = ExploreStyle.WashiInk;
            TextStyle pStyle = ResFont(ResSidePCss, ink);
            TextStyle noteStyle = ResFont(ResNoteCss, ink);

            // what the encounter brought back, one group per kind
            var side = new List<ResultsSideItem>();
            void Section(string title, List<Func<RectTransform, float, RectTransform>> rows)
            {
                if (rows.Count == 0) return;
                side.Add(new ResultsSideItem { Build = (parent, width) => ResSideGroup(parent, width, title, rows, ink) });
            }
            // learned cards are listed here unless they are the cards shown on the right
            if (offer.Count > 0)
            {
                Section(ExploreText.Learned, bk.Learned.Select(id => (Func<RectTransform, float, RectTransform>)((parent, width) =>
                    ResIconRow(parent, width, "seal", 18f, ink, null, ExploreCardFace.CardName(id, book), false, pStyle))).ToList());
            }
            // trial: a learned card a full deck kept out is owned only (encounters_and_rewards_v4 §4.3 「デッキの上限の中で」)
            IReadOnlyList<string> benched = res != null ? res.Benched : Array.Empty<string>();
            if (benched.Count > 0)
            {
                string text = ExploreText.Benched(string.Join("・", benched.Select(id => ExploreCardFace.CardName(id, book))), res.DeckMax);
                side.Add(new ResultsSideItem { Full = true, Build = (parent, width) => ResNoteRow(parent, width, text, true, noteStyle) });
            }
            // a 段 can rename the card (突き → 鋭い突き): the line names it as it was in the hand too (X-15)
            // (the names and the 段 are two runs: the narrow column breaks between them, never inside 「0 → 1 段目」)
            Section(ExploreText.RankUps, bk.RankUps.Select(r => (Func<RectTransform, float, RectTransform>)((parent, width) =>
                ResRankUpRow(parent, width,
                    ExploreText.RankUpNames(MasteryView.NameAtRank(book, r.CardId, r.To), MasteryView.NameAtRank(book, r.CardId, r.From)),
                    ExploreText.RankUpSteps(r.From, r.To), ink, pStyle))).ToList());
            var journalRows = new List<Func<RectTransform, float, RectTransform>>();
            // trial: only the enemies whose 開示度 rose (Raised); without it every enemy fought is listed
            IEnumerable<KeyValuePair<string, int>> levels = res != null ? res.Raised : (IEnumerable<KeyValuePair<string, int>>)bk.Disclosure;
            foreach (KeyValuePair<string, int> pair in levels)
            {
                string name = Expedition.EnemyName(pair.Key); // Enemies.ById(id).Name, the id when it is unknown
                string line = ExploreText.JournalLine(name, pair.Value, global::Journal.JournalPage.MaxDisclosure);
                journalRows.Add((parent, width) => ResIconRow(parent, width, "journal", 18f, ink, null, line, true, pStyle));
            }
            Section(ExploreText.Journal, journalRows);
            var foundRows = bk.Reward.Consumables.Select(id => (Func<RectTransform, float, RectTransform>)((parent, width) =>
                ResIconRow(parent, width, ExploreIcons.ItemIcon(id), 18f, ink, null, ExplorationText.ItemName(id), false, pStyle))).ToList();
            if (bk.Reward.Pages > 0)
            {
                int pages = bk.Reward.Pages;
                foundRows.Add((parent, width) => ResIconRow(parent, width, "page", 18f, ink, null, ExploreText.Pages(pages), true, pStyle));
                // the pages have no content yet (#107): say so where they are given, so the reward does not read as a gain
                foundRows.Add((parent, width) => ResNoteRow(parent, width, ExploreText.PagesNote, false, noteStyle));
            }
            // trial: the card the reward wanted came as a consumable (§4.3 「すべての山が尽きたときは、札の代わりに消耗品」)
            if (res != null && res.PileSpent) foundRows.Add((parent, width) => ResNoteRow(parent, width, ExploreText.PileSpentBattle, false, noteStyle));
            Section(ExploreText.Found, foundRows.Count > 0 ? foundRows : new List<Func<RectTransform, float, RectTransform>>
            {
                (parent, width) => ResQuietRow(parent, width, ExploreText.NothingFound, pStyle),
            });

            var blocks = new List<ResultsBlock> { ResHead(el, innerW, bk.Won ? ExploreText.BattleWon : ExploreText.BattleLost, true, ink) };
            if (compact)
            {
                blocks.Add(ResCompactSide(el, innerW, side, true));
            }
            else
            {
                string h3 = offer.Count > 0 ? ExploreText.PickOne : ExploreText.Learned;
                string note = offer.Count > 0 && fits.Any(f => !f) ? ExploreText.OfferBenched(Expedition.DeckMax) : null;
                blocks.Add(ResBody(el, innerW, side, (main, width) =>
                    ResMain(main, width, h3, showCards, offer.Count > 0, book, picks, Select, () => Finish(true), note)));
            }
            RectTransform row = Ui.Div(el, "ocbg-explore-plate-buttons");
            OcbgButton[] buttons;
            if (offer.Count > 0)
            {
                OcbgButton skip = ResPlateButton(row, ExploreText.SkipButton, false, false, true, () => Finish(false));
                learnBtn = ResPlateButton(row, ExploreText.LearnButton, true, false, true, () => Finish(true));
                learnBtn.Disabled = true;
                buttons = new[] { skip, learnBtn };
            }
            else
            {
                buttons = new[] { ResPlateButton(row, ExploreText.BackToMap, true, false, true, () => Finish(false)) };
            }
            blocks.Add(ResLayButtons(row, innerW, 18f, null, 0f, buttons));
            ResLayAutoPlate(el, blocks);
            if (showCards.Count > 0) Sfx("rewardReveal", 120);
            return src.Promise;
        }

        // ================================================================== 死亡地点の痕跡 (Inheritance through ctx.inheritance)

        // js: scenes/explore/ExploreScene.js:1731
        /// <summary>The 走り書き the life carries now (the app's list), so a trace's copy of one reads as adding nothing.</summary>
        private IReadOnlyList<Scribble> HeldScribbles()
        {
            try
            {
                return Ctx.Inheritance != null && Ctx.Inheritance.Scribbles != null ? Ctx.Inheritance.Scribbles() ?? Array.Empty<Scribble>() : Array.Empty<Scribble>();
            }
            catch (Exception)
            {
                return Array.Empty<Scribble>();
            }
        }

        // js: scenes/explore/ExploreScene.js:1734
        /// <summary>traceFlow(t): the trace's card (pick up to 3, take, or leave after asking).</summary>
        private Promise TraceFlow(ExploreTraceRef t)
        {
            var src = new PromiseSource();
            if (t == null || Ctx.Inheritance == null || Ctx.Inheritance.TraceView == null)
            {
                src.Resolve();
                return src.Promise;
            }
            // a life that is over takes no legacy (the 瘴気死 on the trace's own step: R-09 / R-10 keep it for the next heir)
            if (Session.State.IsOver)
            {
                src.Resolve();
                return src.Promise;
            }
            TraceNodeView view = null;
            try { view = Ctx.Inheritance.TraceView(t.Layer, t.NodeId); }
            catch (Exception e) { Debug.LogError("[explore] traceView " + e); }
            if (view == null)
            {
                src.Resolve();
                return src.Promise;
            }
            TraceOffered = t.Key;
            var picks = new List<string>();
            int cursor = -1;
            bool closed = false;
            bool asking = false;
            var rows = new List<ResultsTraceRow>();
            var stops = new List<ResultsTraceRow>(); // the cursor's rows: the candidates, then the withheld cards
            ScrollArea list = null;
            RectTransform listEl = null;
            RectTransform heading = null;
            TMP_Text count = null;
            TextStyle countStyle = null;
            OcbgButton takeBtn = null;
            float rowW = ResInnerW(ResTraceW);
            Rgba ink = ExploreStyle.WashiInk;

            void Done(bool take)
            {
                if (closed) return;
                closed = true;
                ExploreTaken taken = null;
                LegacyReceipt receipt = null;
                List<string> chosen = take ? picks.ToList() : new List<string>();
                if (view.CanTake && !Session.State.IsOver)
                {
                    // what the picks give this heir: written from what the take did (its Result: the 段 and 開示度 this life
                    // ends with), else from the same steps tried before the take (X-11)
                    IReadOnlyList<Scribble> before = HeldScribbles();
                    IReadOnlyDictionary<string, TraceGainRow> preview = chosen.Count > 0
                        ? TraceGain.TraceGains(Expedition.Raw, view.Candidates, chosen, done: true, scribbles: before) : null;
                    try { taken = Ctx.Inheritance.Take != null ? Ctx.Inheritance.Take(chosen) : null; }
                    catch (Exception e) { Debug.LogError("[explore] take " + e); }
                    Sfx(take && picks.Count > 0 ? "traceTake" : "uiCancel");
                    if (taken != null && chosen.Count > 0)
                    {
                        IReadOnlyDictionary<string, TraceGainRow> gains = taken.Result != null
                            ? TraceGain.TraceGains(Expedition.Raw, view.Candidates, chosen, done: true, result: taken.Result, scribbles: before)
                            : preview;
                        receipt = TakenCard(view, chosen, taken, gains);
                        // the card waits on the expedition until it is closed, so a reload shows it again (X-3; trial mode)
                        if (Expedition.ShowTaken(receipt)) Persist();
                    }
                }
                ClosePlate();
                Render();
                // what was received, row by row as the card listed it (nothing to show for 「何も受け取らない」)
                if (receipt != null)
                {
                    LegacyReceipt card = receipt;
                    Timeout(Dur(240), () => TakenFlow(card).Then(() => { src.TryResolve(); }));
                }
                else Timeout(Dur(240), () => src.TryResolve());
            }

            // Leaving takes nothing and still collects the trace (R-10, 試作で決めたこと 5): it asks first, since Esc
            // is the close key everywhere else and what is not chosen is lost (concept-v3 §5).
            void Leave()
            {
                if (closed || asking) return;
                if (!view.CanTake) { Done(false); return; }
                asking = true;
                Sfx("uiClick");
                Ask(new ConfirmOpts
                {
                    Title = ExploreText.TraceLeaveTitle,
                    Body = ExploreText.TraceLeaveBody(picks.Count),
                    Ok = view.LeaveButton,
                    Cancel = ExploreText.TraceLeaveCancel,
                    Danger = true,
                }).Then(yes =>
                {
                    asking = false;
                    if (!Alive) return;
                    if (yes) Done(false);
                    else Sfx("uiCancel");
                });
            }

            void Toggle(string id)
            {
                int at = picks.IndexOf(id);
                if (at >= 0) picks.RemoveAt(at);
                else if (Inheritance.PickState(picks).CanPickMore) picks.Add(id);
                else return;
                Sfx("rewardPick");
                RefreshTrace();
            }

            // The cursor walks the candidates, then the withheld cards under their heading (they only show why they
            // stayed: R-05), so the keys reach every row of the list, as the mouse wheel does.
            void PlaceCursor(int i, bool quiet = false)
            {
                if (i < 0 || i >= stops.Count) return;
                cursor = i;
                for (int k = 0; k < stops.Count; k++)
                {
                    stops[k].Cursor = k == cursor;
                    ResRestyleTraceRow(stops[k]);
                }
                ResultsTraceRow at = stops[cursor];
                if (at.Heading) KeepInView(listEl, at.Heading);
                KeepInView(listEl, at.El);
                if (cursor == rows.Count - 1 && cursor + 1 >= stops.Count) list.ScrollTop = list.ScrollHeight; // the end of the list
                if (!quiet) Sfx("nodeHover");
            }

            void MoveCursor(int d)
            {
                if (stops.Count == 0) return;
                PlaceCursor(cursor < 0 ? (d > 0 ? 0 : stops.Count - 1) : (cursor + d + stops.Count) % stops.Count);
            }

            // scrollTop += d·round(clientHeight·0.8) (clientHeight leaves the border-top out)
            void Page(int d) => list.ScrollTop = list.ScrollTop + d * (float)Math.Floor((list.ClientHeight - ResListBorder) * 0.8 + 0.5);

            bool Keys(KeyEvent e)
            {
                if (asking) return true; // the leave confirmation answers its own keys; nothing else moves under it
                if (e.Key == "ArrowUp" || e.Key == "ArrowDown") { MoveCursor(e.Key == "ArrowUp" ? -1 : 1); return true; }
                if (e.Key == "PageUp" || e.Key == "PageDown") { Page(e.Key == "PageUp" ? -1 : 1); return true; }
                if (e.Key == "Home" || e.Key == "End") { list.ScrollTop = e.Key == "Home" ? 0f : list.ScrollHeight; return true; }
                if (view.CanTake && e.Key == " " && cursor >= 0) { if (stops[cursor].Id != null) Toggle(stops[cursor].Id); return true; }
                if (e.Key == "Enter") { if (!view.CanTake) Done(false); else if (picks.Count > 0) Done(true); return true; }
                if (e.Key == "Escape") { Leave(); return true; }
                return false;
            }

            // What each row gives *this* heir (X-11): the row is printed as this life will hold it — a card's name and 段
            // after the take (capped by the one-card rule on 3 段, or the higher 段 the life holds), an enemy page's 開示度
            // after the journal keeps the higher one — and the notes under it say what the trace held when that differs and
            // when a pick adds nothing (a 段 or a page held already, a 走り書き already carried, a deck with no room). The
            // picks are tried together (they share the deck's room and the 3 段), the others as the next pick.
            void RefreshTrace()
            {
                PickState ps = Inheritance.PickState(picks);
                Label.SetText(count, Rich(ps.Line, countStyle, true));
                IReadOnlyDictionary<string, TraceGainRow> gains = view.CanTake
                    ? TraceGain.TraceGains(Expedition.Raw, view.Candidates, picks, scribbles: HeldScribbles())
                    : new Dictionary<string, TraceGainRow>();
                foreach (ResultsTraceRow r in rows)
                {
                    bool on = picks.Contains(r.Id);
                    r.On = on;
                    r.Locked = !on && !ps.CanPickMore;
                    TraceGainRow g = gains.TryGetValue(r.Id, out TraceGainRow got) ? got : null;
                    r.Nothing = g != null && g.Nothing;
                    r.NameText = g != null && !string.IsNullOrEmpty(g.Name) ? g.Name : r.C.Name;
                    r.DetailText = g != null && !string.IsNullOrEmpty(g.Detail) ? g.Detail : r.C.Detail ?? "";
                    r.Notes = g != null ? g.Notes : null;
                    ResRestyleTraceRow(r);
                }
                if (takeBtn != null) takeBtn.Disabled = picks.Count == 0;
                ResLayTraceList(list, rows, heading, stops, rowW);
                FitTrace();
            }

            // the list keeps its scroll cue right as the notes under the card rows come and go
            void FitTrace() => MoreCue(listEl);

            RectTransform el = OpenPlate(new PlateOpts
            {
                Cls = "is-washi is-trace",
                W = ResTraceW,
                Ht = ResTraceH,
                Kind = ExploreStyle.PlateTrace,
                Legend = view.CanTake
                    ? new[] { LegendEntry.Of("↑↓", ExploreText.KeySelect), LegendEntry.Of("Space", ExploreText.KeyToggle), LegendEntry.Of(ExploreText.KeyEnter, view.TakeButton), LegendEntry.Of("Esc", view.LeaveButton) }
                    : new[] { LegendEntry.Of("↑↓", ExploreText.KeyScroll), LegendEntry.Of("Esc", ExploreText.Close) },
                Keys = Keys,
            });

            TextStyle noteStyle = ResFont(ResTraceNoteCss, ink);
            var blocks = new List<ResultsBlock>
            {
                ResKicker(el, "Trace", 28f, ink, view.PlaceLine, false, ResFont(ResKickerCss, ink), true),
                ResTextBlock(el, rowW, "h2", view.Title, ResFont(ResH2Css, ink), false, true),
                ResTextBlock(el, rowW, "ocbg-explore-trace-heir", view.HeirLine, ResFont(ResTraceHeirCss, ink), false, true, -6f),
            };
            if (!string.IsNullOrEmpty(view.Note)) blocks.Add(ResTextBlock(el, rowW, "ocbg-explore-trace-note", view.Note, noteStyle, false, true));
            blocks.Add(ResTextBlock(el, rowW, "ocbg-explore-trace-lead", view.CanTake ? view.Lead : view.Refusal, noteStyle, false, true));
            // the deck is at its cap: every card taken here is owned only (X-11, §4.3 「デッキの上限の中で」)
            if (view.CanTake && TraceGain.DeckIsFull(Expedition.Raw) && view.Candidates.Any(c => c.Kind == CandidateKind.Card))
            {
                RectTransform warn = Ui.Div(el, "ocbg-explore-trace-lead is-warn");
                RectTransform p = ResIconRow(warn, rowW, "caution", 20f, ExploreStyle.Caution, null,
                    TraceGainText.DeckFullLead(Expedition.DeckMax), true, ResFont(ResTraceNoteCss + ResTraceLeadWarnCss, ink));
                UiNode.Of(warn).Place(0f, 0f, rowW, UiNode.Of(p).H);
                blocks.Add(new ResultsBlock { El = warn, H = UiNode.Of(p).H });
            }
            listEl = Ui.Div(el, "ocbg-explore-trace-list");
            var listBlock = new ResultsBlock { El = listEl, Grow = true };
            blocks.Add(listBlock);
            RectTransform buttonsRow = Ui.Div(el, "ocbg-explore-plate-buttons");
            if (view.CanTake)
            {
                countStyle = RichStyle(ResFont(ResTraceCountCss, ink));
                count = Label.Make(buttonsRow, "", countStyle, 0f, 0f, null, null, "ocbg-explore-trace-count");
            }
            else countStyle = RichStyle(ResFont(ResTraceCountCss, ink));
            OcbgButton leaveBtn = ResPlateButton(buttonsRow, view.CanTake ? view.LeaveButton : ExploreText.Close, false, false, true, () => Leave());
            takeBtn = view.CanTake ? ResPlateButton(buttonsRow, view.TakeButton, true, false, true, () => Done(true)) : null;
            // the count's own line box (its digits are the number face) stands in the row's height
            float countH = count != null ? ResNumLineBox(countStyle) : 0f;
            blocks.Add(ResLayButtons(buttonsRow, rowW, 10f, count != null ? count.rectTransform : null, countH,
                takeBtn != null ? new[] { leaveBtn, takeBtn } : new[] { leaveBtn }));
            ResLayPlate(el, blocks, ResTraceH);

            // ---- the list (it fills the rest of the card and scrolls)
            list = ResListArea(listEl, rowW, listBlock.H, ResTraceListCss);
            RectTransform content = list.Content;
            TextStyle rowStyle = ResFont(ResTraceRowCss, ink);
            foreach (CandidateView c in view.Candidates)
            {
                var r = new ResultsTraceRow { Id = c.Id, C = c, Disabled = !view.CanTake, NameText = c.Name, DetailText = c.Detail ?? "" };
                ResBuildTraceRow(r, content, rowStyle, ink);
                ClickArea area = ClickArea.On(r.Box);
                area.HoverChanged += on => { r.Hover = on; ResRestyleTraceRow(r); };
                if (!r.Disabled)
                {
                    // a click also moves the cursor there, so Space goes on from the row the mouse last used
                    area.Click += _ =>
                    {
                        if (closed) return;
                        PlaceCursor(stops.IndexOf(r), true);
                        Toggle(c.Id);
                    };
                }
                AddFocus(r.El, () =>
                {
                    if (closed || r.Disabled) return;
                    PlaceCursor(stops.IndexOf(r), true);
                    Toggle(c.Id);
                }, on =>
                {
                    r.Focus = on;
                    ResRestyleTraceRow(r);
                    if (on) ResFocusIntoView(listEl, r.El); // focus() scrolls the row into the list's view
                }, () => !r.Disabled, () => ExploreText.KeyToggle);
                rows.Add(r);
                stops.Add(r);
            }
            if (view.Withheld != null && view.Withheld.Count > 0)
            {
                heading = Label.Make(content, view.WithheldHeading ?? "", ResFont(ResTraceListH4Css, ink), 0f, 0f, rowW, null, "h4").rectTransform;
                for (int i = 0; i < view.Withheld.Count; i++)
                {
                    WithheldView wv = view.Withheld[i];
                    var r = new ResultsTraceRow { W = wv, Withheld = true, Heading = i == 0 ? heading : null, NameText = wv.Name, DetailText = wv.Detail ?? "" };
                    ResBuildTraceRow(r, content, rowStyle, ink);
                    ClickArea area = ClickArea.On(r.Box); // :hover (the withheld row is a div: no click)
                    area.HoverChanged += on => { r.Hover = on; ResRestyleTraceRow(r); };
                    ResRestyleTraceRow(r);
                    stops.Add(r);
                }
            }
            // the list fades at the edge it runs on past, like the hall's (a scroll cue: nothing below reads as empty)
            list.Scrolled += () => MoreCue(listEl);
            RefreshTrace();
            // the legend offers Space at once: the cursor starts on the first candidate (a reload brings the card back the same)
            if (view.CanTake && rows.Count > 0) PlaceCursor(0, true);
            MoreCue(listEl);
            return src.Promise;
        }

        // js: scenes/explore/ExploreScene.js:1908
        /// <summary>A scrolling list fades at the edge it continues past (the hall's lists do the same: is-more-above / is-more-below).</summary>
        private void MoreCue(RectTransform list)
        {
            if (!list) return;
            ScrollArea area = list.GetComponent<ScrollArea>();
            // ScrollArea.MoreAbove / MoreBelow are the JS's tests (scrollHeight > clientHeight + 2 …); Refresh sets the mask
            if (area) area.Refresh();
        }

        // js: scenes/explore/ExploreScene.js:1918
        /// <summary>
        /// Scrolls a list so its row stays in view. A list that fades at its edges (MoreCue) keeps the row clear of the
        /// fade, so the row the keys are on is never dimmed.
        /// </summary>
        private void KeepInView(RectTransform list, RectTransform row)
        {
            if (!list || !row) return;
            ScrollArea area = list.GetComponent<ScrollArea>();
            if (!area) return;
            // the row's box in the list's content (the rows and the heading are the content's own children)
            Vector2 pos = row.parent == area.Content ? Ui.PosOf(row) : ResContentPos(area, row);
            float top = pos.y - area.ScrollTop;
            float bottom = top + row.rect.height;
            float client = area.ClientHeight - ResListBorder; // clientHeight: the border-top is not in it
            bool fades = area.More;
            float m = fades ? (float)Math.Floor(client * 0.18 + 0.5) : 8f; // the fade's depth (is-more-* masks: 82%)
            if (top < m) area.ScrollTop = area.ScrollTop - (m - top);
            else if (bottom > area.ClientHeight - m) area.ScrollTop = area.ScrollTop + (bottom - (area.ClientHeight - m));
            MoreCue(list);
        }

        /// <summary>
        /// What the browser does when Tab focuses a button in a scrolling list (the trace's and the training's rows):
        /// focus() scrolls it into view with Blink's ScrollAlignment::CenterIfNeeded — a row wholly inside the scrollport
        /// (under the 1px border-top) stays put, one partly inside comes to its nearest edge, one out of sight is centred.
        /// </summary>
        private void ResFocusIntoView(RectTransform list, RectTransform row)
        {
            if (!list || !row) return;
            ScrollArea area = list.GetComponent<ScrollArea>();
            if (!area) return;
            float client = area.ClientHeight - ResListBorder;
            float top = (row.parent == area.Content ? Ui.PosOf(row) : ResContentPos(area, row)).y - ResListBorder;
            float h = row.rect.height;
            float st = area.ScrollTop;
            if (top >= st - 0.5f && top + h <= st + client + 0.5f) return;
            bool partial = top < st + client && top + h > st;
            area.ScrollTop = partial ? (top < st ? top : top + h - client) : top + h / 2f - client / 2f;
            MoreCue(list);
        }

        // js: scenes/explore/ExploreScene.js:1934
        /// <summary>
        /// The 受け取った遺産 card's content as plain data (the expedition keeps it until it is closed: X-3): the rows the trace
        /// card listed for the picks (Inheritance's own rows), each card with what it gave this heir (X-11), and how many
        /// candidates were lost (concept-v3 §5 「選ばなかった頁と資源は失われる」).
        /// </summary>
        private LegacyReceipt TakenCard(TraceNodeView view, IReadOnlyList<string> chosen, ExploreTaken taken, IReadOnlyDictionary<string, TraceGainRow> gains)
        {
            var picked = chosen ?? Array.Empty<string>();
            IEnumerable<LegacyReceiptRow> rows = view.Candidates.Where(c => picked.Contains(c.Id)).Select(c =>
            {
                TraceGainRow g = gains != null && gains.TryGetValue(c.Id, out TraceGainRow got) ? got : null;
                return new LegacyReceiptRow(
                    c.Kind,
                    c.KindLabel,
                    g != null && !string.IsNullOrEmpty(g.Name) ? g.Name : c.Name,
                    g != null && !string.IsNullOrEmpty(g.Detail) ? g.Detail : c.Detail ?? "",
                    g != null ? g.Notes.ToList() : new List<string>(),
                    g != null && g.Nothing);
            });
            return new LegacyReceipt(view.PlaceLine, view.HeirLine, rows, taken != null ? taken.Lost : 0);
        }

        // js: scenes/explore/ExploreScene.js:1950
        /// <summary>
        /// After 受け取る: what the heir took from the trace (takenCard), as the trace card listed it. Only shows; nothing is
        /// decided. Closing it closes the expedition's pending card (CloseTaken), so a reload no longer brings it back.
        /// </summary>
        private Promise TakenFlow(LegacyReceipt card)
        {
            var src = new PromiseSource();
            bool closed = false;
            card = card ?? new LegacyReceipt("", "", null, 0);

            void Finish()
            {
                if (closed) return;
                closed = true;
                if (Expedition.PendingTaken != null)
                {
                    Expedition.CloseTaken();
                    Persist();
                }
                ClosePlate();
                Render();
                Timeout(Dur(240), () => src.TryResolve());
            }

            RectTransform el = OpenPlate(new PlateOpts
            {
                Cls = "is-washi is-result is-taken is-compact",
                W = ResCompact,
                Ht = null,
                Kind = ExploreStyle.PlateResult,
                Legend = new[] { LegendEntry.Of(ExploreText.KeyEnter, ExploreText.BackToMap) },
                Keys = e =>
                {
                    if (e.Key == "Enter" || e.Key == "Escape") { Finish(); return true; }
                    return false;
                },
            });
            float innerW = ResInnerW(ResCompact);
            Rgba ink = ExploreStyle.WashiInk;
            var items = (card.Rows ?? (IReadOnlyList<LegacyReceiptRow>)Array.Empty<LegacyReceiptRow>())
                .Select(c => (Func<RectTransform, float, RectTransform>)((parent, width) => ResTakenItem(parent, width, c, ink))).ToList();
            int lost = card.Lost;
            var side = new List<ResultsSideItem> { new ResultsSideItem { Build = (parent, width) => ResSideGroup(parent, width, card.HeirLine, items, ink) } };
            if (lost > 0)
            {
                side.Add(new ResultsSideItem
                {
                    Full = true,
                    Build = (parent, width) => ResNoteRow(parent, width, ExploreText.TraceLost(lost), true, ResFont(ResNoteCss, ink)),
                });
            }
            var blocks = new List<ResultsBlock>
            {
                ResKicker(el, "Trace", 26f, ink, card.PlaceLine, false, ResFont(ResKickerCss, ink), true),
                ResHead(el, innerW, ExploreText.TakenTitle, false, ink),
                // .is-taken .ocbg-explore-result-side: a column (row-gap 14 of the compact grid)
                ResCompactSide(el, innerW, side, false),
            };
            RectTransform row = Ui.Div(el, "ocbg-explore-plate-buttons");
            OcbgButton back = ResPlateButton(row, ExploreText.BackToMap, true, false, true, () => Finish());
            blocks.Add(ResLayButtons(row, innerW, 18f, null, 0f, back));
            ResLayAutoPlate(el, blocks);
            return src.Promise;
        }

        // ================================================================== 習熟訓練: spend a mark on a card (Expedition.Train)

        // js: scenes/explore/ExploreScene.js:1989
        /// <summary>
        /// trainingFlow({ descend }): spend a mark on a card (Expedition.Train). Resolves with 'trained', 'later' (「あとで」 /
        /// 「やめる」) or 'descend' (「刻みを捨てて降りる」, only with descend). `descend` is the card 次の層へ降りる opens while
        /// marks are left: a mark is earned on this layer and the next layer starts with none (ExplorationReducer.Enter), so
        /// the heir chooses before going down instead of losing a paid tick without a word (dungeon_exploration_v4 §2.1, §3.2).
        /// </summary>
        private Promise<string> TrainingFlow(bool descend = false)
        {
            var src = new PromiseSource<string>();
            if (!(Session.State.TrainingMarks > 0))
            {
                src.Resolve("later");
                return src.Promise;
            }
            var book = Expedition.Mastery;
            var ids = new List<string>();
            var copies = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var c in Expedition.Deck)
            {
                string id = c.Def.Id;
                if (!ids.Contains(id)) ids.Add(id);
                copies[id] = (copies.TryGetValue(id, out int n) ? n : 0) + 1;
            }
            int chosen = -1;
            bool closed = false;
            var rows = new List<ResultsTrainRow>();
            RectTransform preview = null;
            ScrollArea list = null;
            RectTransform listEl = null;
            OcbgButton trainBtn = null;
            bool lastMark = Session.State.TrainingMarks == 1;
            // each card's 習熟 now (MasteryView): the rows' 刻み, the preview's lines, and the line after the training
            List<MasteryProgressInfo> progress = ids.Select(id => MasteryView.MasteryProgress(book, id)).ToList();
            string holderName = !string.IsNullOrEmpty(book.TopRankHolder) ? MasteryView.NameAtRank(book, book.TopRankHolder, 3) : "";
            int shownFace = -1;
            Rgba ink = ExploreStyle.Ink;

            void Done(string how)
            {
                if (closed) return;
                closed = true;
                string result = how;
                string said = null;
                if (how == "train" && chosen >= 0 && Expedition.Train(ids[chosen]))
                {
                    Sfx("uiConfirm");
                    result = "trained";
                    // what the mark did, said once the card is gone (X-15): the 刻み toward the next 段, or the 段 it bought
                    string id = ids[chosen];
                    MasteryProgressInfo was = progress[chosen];
                    MasteryProgressInfo now = MasteryView.MasteryProgress(Expedition.Mastery, id);
                    said = now.Rank > was.Rank
                        ? ExploreText.RankUp(now.Name, was.Rank, now.Rank, was.Name)
                        : ExploreText.Trained(now.Name, now.Ticks, now.Need ?? 0);
                }
                else
                {
                    Sfx("uiCancel");
                    if (how == "train") result = "later";
                    // 「あとで」: the card is put off, so a reload does not bring it back; the band keeps the marks
                    if (Session.TrainingWaiting) Session.PutOffTraining();
                }
                Persist();
                ClosePlate();
                Render();
                if (said != null) NoteBanner(ExploreText.TrainedKicker, said, "Rest");
                string r = result;
                Timeout(Dur(240), () => src.TryResolve(r));
            }

            void ShowFace(int i)
            {
                shownFace = i;
                if (!preview) return;
                Ui.Clear(preview);
                if (i < 0 || i >= ids.Count) return;
                // .ocbg-explore-train-preview .ocb-card: absolute at −1, −1 (over the dashed border), faded in; the stub is
                // no .ocb-card: it stands in the flow inside the 1px border and does not fade
                RectTransform face = ResCardFace(preview, ids[i], book, out bool stub);
                if (stub) Ui.Place(face, 1f, 1f);
                else CssAnimator.Play(face, ResTrainPreviewCardAnim);
                // under the card: the name it had, and what one more 刻み does (X-15)
                MasteryProgressInfo p = progress[i];
                var lines = new List<string>();
                if (p.Name != p.BaseName) lines.Add(ExploreText.TrainBaseName(p.BaseName));
                if (p.Need.HasValue && p.Need.Value != 0)
                {
                    lines.Add(ExploreText.TrainNext(p.Ticks + 1, p.Need.Value, p.Rank + 1));
                    if (!string.IsNullOrEmpty(p.NextName)) lines.Add(ExploreText.TrainNextName(p.NextName));
                }
                else if (p.Capped) lines.Add(ExploreText.TrainCappedNote(holderName));
                else lines.Add(p.Top ? ExploreText.TrainTicksTop : ExploreText.TrainTicksFlat);
                ResTrainAbout(preview, lines, ink);
            }

            void Select(int i)
            {
                chosen = i;
                for (int k = 0; k < rows.Count; k++)
                {
                    rows[k].Chosen = k == i;
                    ResRestyleTrainRow(rows[k]);
                }
                if (trainBtn != null) trainBtn.Disabled = chosen < 0;
                ShowFace(i);
                // the chosen row stays in sight (the keys move it; the preview alone must not be the only sign of it)
                if (i >= 0 && i < rows.Count) KeepInView(listEl, rows[i].El);
                Sfx("rewardPick");
            }

            string laterLabel = descend ? ExploreText.TrainStay : ExploreText.TrainLater;
            var legend = new List<LegendEntry>
            {
                LegendEntry.Of("↑↓", ExploreText.KeySelect),
                LegendEntry.Of(ExploreText.KeyEnter, descend && lastMark ? ExploreText.TrainAndDescend : ExploreText.TrainButton),
            };
            if (descend) legend.Add(LegendEntry.Of(new[] { ExploreText.KeyShift, ExploreText.KeyEnter }, ExploreText.TrainDiscard));
            legend.Add(LegendEntry.Of("Esc", laterLabel));
            RectTransform el = OpenPlate(new PlateOpts
            {
                Cls = "is-solid is-train" + (descend ? " is-descend" : ""),
                W = ResTrainW,
                Ht = ResTrainH,
                Kind = ExploreStyle.PlateTrain,
                Legend = legend,
                Keys = e =>
                {
                    int move = e.Key == "ArrowUp" || e.Key == "ArrowLeft" ? -1 : e.Key == "ArrowDown" || e.Key == "ArrowRight" ? 1 : 0;
                    if (move != 0)
                    {
                        if (ids.Count > 0) Select(chosen < 0 ? 0 : (chosen + move + ids.Count) % ids.Count);
                        return true;
                    }
                    if (e.Key == "Enter" && descend && e.ShiftKey) { Done("descend"); return true; }
                    if (e.Key == "Enter") { if (chosen >= 0) Done("train"); return true; }
                    if (e.Key == "Escape") { Done("later"); return true; }
                    return false;
                },
            });
            float innerW = ResInnerW(ResTrainW);
            var blocks = new List<ResultsBlock>
            {
                ResKicker(el, "Rest", 26f, ExploreStyle.Ink, Screen.MarksLabel, true, ResFont(ResKickerCss + ResTrainKickerCss, ink), false),
                ResTextBlock(el, innerW, "h2", descend ? ExploreText.TrainBeforeDescend : ExploreText.TrainTitle, ResFont(ResH2Css, ink), false, false),
                // .ocbg-explore-train-lead: the h3 rule's letter-spacing (.06em) stays under its own font (explore.css:288, 332)
                ResTextBlock(el, innerW, "ocbg-explore-train-lead", descend ? ExploreText.TrainLeadDescend : ExploreText.TrainLead,
                    ResFont(ResMainH3Css + ResTrainLeadCss, ink), false, false, -4f),
            };
            RectTransform body = Ui.Div(el, "ocbg-explore-train-body");
            var bodyBlock = new ResultsBlock { El = body, Grow = true };
            blocks.Add(bodyBlock);
            RectTransform buttonsRow = Ui.Div(el, "ocbg-explore-plate-buttons");
            OcbgButton laterBtn = ResPlateButton(buttonsRow, laterLabel, false, false, false, () => Done("later"));
            OcbgButton discardBtn = descend ? ResPlateButton(buttonsRow, ExploreText.TrainDiscard, false, true, false, () => Done("descend")) : null;
            trainBtn = ResPlateButton(buttonsRow, descend && lastMark ? ExploreText.TrainAndDescend : ExploreText.TrainButton, true, false, false, () => Done("train"));
            trainBtn.Disabled = true;
            blocks.Add(ResLayButtons(buttonsRow, innerW, 10f, null, 0f,
                discardBtn != null ? new[] { laterBtn, discardBtn, trainBtn } : new[] { laterBtn, trainBtn }));
            ResLayPlate(el, blocks, ResTrainH);

            // ---- the body: the list (1fr) and the preview (216), gap 40, top-aligned
            float listW = innerW - 40f - ResCardW;
            listEl = Ui.Div(body, "ocbg-explore-train-list");
            TextStyle rowStyle = ResFont(ResTrainRowCss, ink);
            var built = new List<ResultsTrainRow>();
            // the rows are built into a holder first: the list's height (max-height 470) follows their height
            float rowsH = 0f;
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                OcbCardDef def = null;
                try { def = ExploreCardFace.CardDefById(id, book); }
                catch (Exception) { /* the name still shows */ }
                OcbAttrInfo attr = def != null && def.Attr != null && Ocb.Attr.TryGetValue(def.Attr, out OcbAttrInfo a) ? a : null;
                MasteryProgressInfo p = progress[i];
                int rank = p.Rank;
                bool need = p.Need.HasValue && p.Need.Value != 0;
                string ticks = need ? ExploreText.TrainTicks(p.Ticks, p.Need.Value) : p.Capped ? ExploreText.TrainTicksCapped : p.Top ? ExploreText.TrainTicksTop : ExploreText.TrainTicksFlat;
                var r = new ResultsTrainRow
                {
                    Name = def != null ? def.Name : ExploreCardFace.CardName(id, book),
                    Copies = "×" + (copies.TryGetValue(id, out int n) ? n : 0).ToString(CultureInfo.InvariantCulture),
                    Ticks = ticks,
                    Quiet = !need,
                    Rank = rank,
                    Attr = attr,
                };
                built.Add(r);
            }
            list = ResListArea(listEl, listW, ResTrainListMax, ResTrainListCss);
            foreach (ResultsTrainRow r in built)
            {
                ResBuildTrainRow(r, list.Content, listW, rowStyle, ink);
                rowsH += r.H;
            }
            // max-height 470 (border-box): shorter when the rows are fewer
            float listH = Math.Min(ResTrainListMax, ResListBorder + rowsH);
            UiNode.Of(listEl).Place(0f, 0f, listW, listH);
            ScrollArea.Attach(listEl).Refresh();
            float y = ResListBorder;
            for (int i = 0; i < built.Count; i++)
            {
                ResultsTrainRow r = built[i];
                UiNode.Of(r.El).Place(0f, y, listW, r.H);
                y += r.H;
                int at = i;
                ClickArea area = ClickArea.On(r.Box);
                area.Click += _ => { if (!closed) Select(at); };
                area.HoverChanged += on => { r.Hover = on; ResRestyleTrainRow(r); };
                // The mouse previews the card it moves over. Only a real move counts: when the keys scroll the list under a
                // resting pointer, a different row slides under it (and gets mouseenter), but the preview must stay on the
                // row the keys chose — the one 訓練する / Enter trains (X-8).
                ExploreResultsPointerMove move = r.El.gameObject.AddComponent<ExploreResultsPointerMove>();
                move.OnMove = () => { if (!closed && shownFace != at) ShowFace(at); };
                AddFocus(r.El, () => { if (!closed) Select(at); }, on =>
                {
                    r.Focus = on;
                    ResRestyleTrainRow(r);
                    if (on) ResFocusIntoView(listEl, r.El); // focus() scrolls the row into the list's view
                }, null, () => r.Copies);
                rows.Add(r);
            }
            list.Content.sizeDelta = new Vector2(listW, y);
            list.Scrolled += () => MoreCue(listEl);
            ClickArea listArea = ClickArea.On(list.Viewport);
            listArea.Exit += _ => { if (!closed) ShowFace(chosen < 0 ? 0 : chosen); };

            // .ocbg-explore-train-preview: the chosen card on a dashed place, its lines under it
            BoxGraphic pv = BoxGraphic.Make(body, ResTrainPreviewCss, listW + 40f, 0f, ResCardW, ResCardH, "ocbg-explore-train-preview");
            preview = pv.rectTransform;
            UiNode.Of(preview);
            ShowFace(0);
            MoreCue(listEl);
            return src.Promise;
        }

        // js: scenes/explore/ExploreScene.js:2122
        /// <summary>
        /// 次の層へ降りる with marks left: the training card asks first, one mark at a time. True when the descent goes on
        /// (every mark spent, or 「刻みを捨てて降りる」); false when the heir stays at the interlude (「やめる」).
        /// </summary>
        private async Promise<bool> TrainBeforeDescend()
        {
            if (FlowRunning) return false;
            FlowRunning = true;
            try
            {
                for (int guard = 0; guard < 8 && Alive && Session.State.TrainingMarks > 0; guard++)
                {
                    string how = await TrainingFlow(descend: true);
                    if (how == "descend") return true;
                    if (how != "trained") return false;
                }
                return Alive;
            }
            finally
            {
                FlowRunning = false;
            }
        }

        // ================================================================== the result card's parts

        /// <summary>
        /// .ocbg-explore-result-head: the title (nowrap) and, for a battle or an event, the stat chips (HP, スタミナ) on
        /// the right; padding-bottom 14 over a 1px washi-ink rule.
        /// </summary>
        private ResultsBlock ResHead(RectTransform plate, float innerW, string title, bool stats, Rgba ink)
        {
            RectTransform head = Ui.Div(plate, "ocbg-explore-result-head");
            TextStyle h2 = ResFont(ResH2Css + ResHeadH2Css, ink);
            TMP_Text t = Label.Make(head, title ?? "", h2, 0f, 0f, null, null, "h2");
            float rowH = h2.LineHeightPx;
            var chips = new List<RectTransform>();
            if (stats && Screen != null)
            {
                // .ocbg-explore-result-stats: HP and スタミナ as the band prints them
                chips.Add(ResStatChip(head, "heart", 22f, ExploreStyle.Crimson, Screen.HpLabel));
                chips.Add(ResStatChip(head, "pip", 20f, ExploreStyle.Amber, Screen.StaminaLabel));
                foreach (RectTransform c in chips) rowH = Math.Max(rowH, UiNode.Of(c).H);
            }
            Ui.Place(t.rectTransform, 0f, (rowH - h2.LineHeightPx) / 2f);
            float x = innerW;
            for (int k = chips.Count - 1; k >= 0; k--)
            {
                UiNode n = UiNode.Of(chips[k]);
                x -= n.W;
                n.Place(x, (rowH - n.H) / 2f);
                x -= 14f; // .ocbg-explore-result-stats gap
            }
            float h = rowH + 14f + 1f;
            UiNode.Of(head).Place(0f, 0f, innerW, h);
            BoxGraphic.On(head, ResHeadCss);
            return new ResultsBlock { El = head, H = h };
        }

        /// <summary>A stat chip (explore.css:264): icon, gap 8, the label (its digits in the number face), padding 6 14, ink on panel-solid.</summary>
        private static RectTransform ResStatChip(RectTransform parent, string icon, float size, Rgba color, string text)
        {
            TextStyle s = RichStyle(TextStyle.FromCss(ResStatCss));
            string shown = Rich(text, s);
            float tw = TextMeasure.Width(shown, s);
            float lb = ResHasDigit(text) ? ResNumLineBox(s) : s.LineHeightPx;
            float w = 14f + size + 8f + tw + 14f, h = 6f + lb + 6f;
            BoxGraphic box = BoxGraphic.Make(parent, ResStatCss, 0f, 0f, w, h, "stat");
            RectTransform rt = box.rectTransform;
            PathGraphic g = ExploreIcons.Ico(rt, icon, size, color);
            Ui.Place(g.rectTransform, 14f, (h - size) / 2f);
            Label.Make(rt, shown, s, 14f + size + 8f, 6f, null, null, "label");
            UiNode.Of(rt);
            return rt;
        }

        /// <summary>
        /// .ocbg-explore-result-body: the side (300, padding 6 28 0 0, a rule on its right) and the main column (1fr),
        /// gap 36, padding-top 4; the row is as tall as the taller of the two.
        /// </summary>
        private ResultsBlock ResBody(RectTransform plate, float innerW, List<ResultsSideItem> items, Func<RectTransform, float, float> main)
        {
            RectTransform body = Ui.Div(plate, "ocbg-explore-result-body");
            RectTransform side = Ui.Div(body, "ocbg-explore-result-side");
            float sideH = 6f + ResLaySideItems(side, items, 0f, 6f, 300f - 28f - 1f, false, 16f);
            RectTransform mainEl = Ui.Div(body, "ocbg-explore-result-main");
            float mainW = innerW - 300f - 36f;
            float mainH = main != null ? main(mainEl, mainW) : 0f;
            float rowH = Math.Max(sideH, mainH);
            UiNode.Of(side).Place(0f, 4f, 300f, rowH);
            BoxGraphic.On(side, ResSideCss);
            UiNode.Of(mainEl).Place(300f + 36f, 4f, mainW, mainH);
            float h = 4f + rowH;
            UiNode.Of(body).Place(0f, 0f, innerW, h);
            return new ResultsBlock { El = body, H = h };
        }

        /// <summary>
        /// The side alone (.is-compact): a grid of two columns (gap 40 / 14), the notes across both, padding 4 0 6, no rule;
        /// the event's and the taken card's side is a column instead (explore.css:280, :434), keeping the grid's row-gap 14.
        /// </summary>
        private ResultsBlock ResCompactSide(RectTransform plate, float innerW, List<ResultsSideItem> items, bool grid)
        {
            RectTransform side = Ui.Div(plate, "ocbg-explore-result-side");
            float h = 4f + ResLaySideItems(side, items, 0f, 4f, innerW, grid, 14f) + 6f;
            UiNode.Of(side).Place(0f, 0f, innerW, h);
            return new ResultsBlock { El = side, H = h };
        }

        /// <summary>
        /// Lays the side's items: a column (gap) or the compact grid (two columns, gap 40, rows gap; a Full item spans
        /// both). Returns the height they take.
        /// </summary>
        private static float ResLaySideItems(RectTransform side, IList<ResultsSideItem> items, float x0, float y0, float width, bool grid, float gap)
        {
            float y = y0;
            if (!grid)
            {
                bool first = true;
                foreach (ResultsSideItem it in items)
                {
                    if (!first) y += gap;
                    first = false;
                    UiNode n = UiNode.Of(it.Build(side, width));
                    n.Place(x0, y);
                    y += n.H;
                }
                return y - y0;
            }
            const float colGap = 40f;
            float colW = (width - colGap) / 2f;
            int col = 0;
            float rowH = 0f;
            bool any = false;
            foreach (ResultsSideItem it in items)
            {
                if (it.Full)
                {
                    if (col > 0) { y += rowH + gap; col = 0; rowH = 0f; }
                    else if (any) y += gap;
                    UiNode n = UiNode.Of(it.Build(side, width));
                    n.Place(x0, y);
                    y += n.H;
                    any = true;
                    continue;
                }
                if (col == 0 && any) y += gap;
                UiNode c = UiNode.Of(it.Build(side, colW));
                c.Place(x0 + col * (colW + colGap), y);
                rowH = Math.Max(rowH, c.H);
                any = true;
                col++;
                if (col == 2) { y += rowH; col = 0; rowH = 0f; }
            }
            if (col > 0) y += rowH;
            return y - y0;
        }

        /// <summary>.ocbg-explore-result-group: the h4 (margin-bottom 2) and the rows, a column with gap 6.</summary>
        private static RectTransform ResSideGroup(RectTransform side, float width, string title, IList<Func<RectTransform, float, RectTransform>> rows, Rgba ink)
        {
            RectTransform g = Ui.Div(side, "ocbg-explore-result-group");
            float y = 0f;
            bool first = true;
            if (title != null)
            {
                TextStyle h4 = ResFont(ResSideH4Css, ink);
                TMP_Text t = Label.Make(g, title, h4, 0f, 0f, width, null, "h4");
                y = ResCssHeight(t, h4, false) + 2f;
                first = false;
            }
            foreach (Func<RectTransform, float, RectTransform> build in rows)
            {
                if (!first) y += 6f;
                first = false;
                UiNode n = UiNode.Of(build(g, width));
                n.Place(0f, y);
                y += n.H;
            }
            UiNode.Of(g).Place(0f, 0f, width, y);
            return g;
        }

        /// <summary>
        /// A side row (.ocbg-explore-result-side p): the icon (flex none), gap 8, the text wrapping in what is left, centred
        /// on one another. `icon` names an explore icon; `opts` its options.
        /// </summary>
        private static RectTransform ResIconRow(RectTransform parent, float width, string icon, float size, Rgba color, IcoOpts opts, string text, bool rich, TextStyle style)
        {
            RectTransform p = Ui.Div(parent, "p");
            PathGraphic g = icon != null ? ExploreIcons.Ico(p, icon, size, color, opts) : null;
            float tx = g ? size + 8f : 0f;
            TextStyle s = rich ? RichStyle(style) : style;
            TMP_Text t = Label.Make(p, rich ? Rich(text, s, true) : text ?? "", s, tx, 0f, Math.Max(1f, width - tx), null, "text");
            float th = ResCssHeight(t, s, rich);
            float h = Math.Max(g ? size : 0f, th);
            if (g) Ui.Place(g.rectTransform, 0f, (h - size) / 2f);
            Ui.Place(t.rectTransform, tx, (h - th) / 2f);
            UiNode.Of(p).Place(0f, 0f, width, h);
            return p;
        }

        /// <summary>An event's line (ExploreScene.js:1102): lineIcon (the plates unit's) and rich(line).</summary>
        private RectTransform ResEventLineRow(RectTransform parent, float width, string line, Rgba ink, TextStyle style)
        {
            RectTransform p = Ui.Div(parent, "p");
            RectTransform icon = LineIcon(p, line, ink);
            Vector2 box = ResLineIconBox(line);
            float tx = box.x + 8f;
            TextStyle s = RichStyle(style);
            TMP_Text t = Label.Make(p, Rich(line, s, true), s, tx, 0f, Math.Max(1f, width - tx), null, "text");
            float th = ResCssHeight(t, s, true);
            float h = Math.Max(box.y, th);
            if (icon) Ui.Place(icon, 0f, (h - box.y) / 2f);
            Ui.Place(t.rectTransform, tx, (h - th) / 2f);
            UiNode.Of(p).Place(0f, 0f, width, h);
            return p;
        }

        // js: scenes/explore/ExploreScene.js:963 (lineIcon's sizes: heart 20, pip 18, miasma / seal / page / pouch 20, the dot 20 × 0)
        /// <summary>The box lineIcon(line) takes in its row: its width and its height (the blank dot has none).</summary>
        private static Vector2 ResLineIconBox(string line)
        {
            string l = line ?? "";
            if (l.StartsWith("HP", StringComparison.Ordinal)) return new Vector2(20f, 20f);
            if (l.StartsWith("最大スタミナ", StringComparison.Ordinal) || l.StartsWith("スタミナ", StringComparison.Ordinal)) return new Vector2(18f, 18f);
            if (l.StartsWith("瘴気", StringComparison.Ordinal) || l.StartsWith("カード", StringComparison.Ordinal) ||
                l.StartsWith("希少な頁", StringComparison.Ordinal) || l.StartsWith("頁", StringComparison.Ordinal) ||
                l.StartsWith("消耗品", StringComparison.Ordinal)) return new Vector2(20f, 20f);
            return new Vector2(20f, 0f); // .ocbg-explore-fc-dot: width 20, flex none, empty
        }

        /// <summary>A note (p.ocbg-explore-result-note: display block, a size down, wraps).</summary>
        private static RectTransform ResNoteRow(RectTransform parent, float width, string text, bool rich, TextStyle style)
        {
            RectTransform p = Ui.Div(parent, "ocbg-explore-result-note");
            TextStyle s = rich ? RichStyle(style) : style;
            TMP_Text t = Label.Make(p, rich ? Rich(text, s, true) : text ?? "", s, 0f, 0f, Math.Max(1f, width), null, "text");
            UiNode.Of(p).Place(0f, 0f, width, ResCssHeight(t, s, rich));
            return p;
        }

        /// <summary>p.is-quiet: the plain line at opacity .7 (「拾った物はない」).</summary>
        private static RectTransform ResQuietRow(RectTransform parent, float width, string text, TextStyle style)
        {
            RectTransform p = ResIconRow(parent, width, null, 0f, default, null, text, false, style);
            p.name = "p is-quiet";
            UiNode.Of(p).Opacity = 0.7f;
            return p;
        }

        /// <summary>
        /// A rank-up (p.ocbg-explore-rankup): the outline pip (16, margin-top 5), then 「突き → 鋭い突き」 and 「0 → 1 段目」 as an
        /// inline-block (margin-left .5em, nowrap) that goes to a line of its own when it does not fit after the names.
        /// </summary>
        private static RectTransform ResRankUpRow(RectTransform parent, float width, string names, string steps, Rgba ink, TextStyle style)
        {
            RectTransform p = Ui.Div(parent, "p ocbg-explore-rankup");
            PathGraphic g = ExploreIcons.Ico(p, "pip", 16f, ink, IcoOpts.OutlineOnly);
            Ui.Place(g.rectTransform, 0f, 5f); // flex: none; margin-top: 5px (align-items: flex-start)
            float tx = 16f + 8f;
            float spanW = Math.Max(1f, width - tx);
            TextStyle s = RichStyle(style);
            TMP_Text t = Label.Make(p, Rich(names, s, true), s, tx, 0f, spanW, null, "names");
            TextStyle ss = RichStyle(style.WithWhiteSpace(WhiteSpace.NoWrap));
            string stepsShown = Rich(steps, ss, true);
            float sw = TextMeasure.Width(stepsShown, ss);
            float ml = 0.5f * style.Size; // margin-left: .5em
            float L = style.LineHeightPx, box = ResNumLineBox(s);
            float stepBox = ResHasDigit(steps) ? box : L;
            // the names' lines: the top of the last one, its box and its width
            t.ForceMeshUpdate(true, true);
            TMP_TextInfo info = t.textInfo;
            float top = 0f, lastBox = L, lastW;
            if (info != null && info.lineCount > 0)
            {
                for (int i = 0; i < info.lineCount - 1; i++) top += ResLineHasDigit(info, i) ? box : L;
                lastBox = ResLineHasDigit(info, info.lineCount - 1) ? box : L;
                lastW = info.lineInfo[info.lineCount - 1].length;
            }
            else lastW = TextMeasure.Width(Label.StateOf(t).Text, s);
            float sx, sy, h;
            if (lastW + ml + sw <= spanW + 0.5f)
            {
                // the 段 run fits after the names: their last line holds it (its digits grow that line box)
                sx = tx + lastW + ml;
                sy = top;
                h = top + Math.Max(lastBox, stepBox);
            }
            else
            {
                // an inline-block that does not fit breaks to a line of its own (its margin-left kept)
                sx = tx + ml;
                sy = top + lastBox;
                h = sy + stepBox;
            }
            Label.Make(p, stepsShown, ss, sx, sy, null, null, "ocbg-explore-rankup-steps");
            UiNode.Of(p).Place(0f, 0f, width, Math.Max(16f + 5f, h));
            return p;
        }

        /// <summary>
        /// .ocbg-explore-result-main: the h3, the cards row (padding 18 0 44, centred, gap 32) and the note a card that
        /// would not fit the deck leaves (margin-top −10). Returns its height.
        /// </summary>
        private float ResMain(RectTransform main, float width, string h3, IReadOnlyList<string> ids, bool pickable, BattleCore.MasteryBook book,
            List<ResultsPick> picks, Action<int> select, Action learn, string note)
        {
            Rgba ink = ExploreStyle.WashiInk;
            TextStyle h3s = ResFont(ResMainH3Css, ink);
            TMP_Text t = Label.Make(main, h3 ?? "", h3s, 0f, 0f, width, null, "h3");
            float y = ResCssHeight(t, h3s, false) + 18f;
            RectTransform row = Ui.Div(main, "ocbg-explore-result-cards");
            float rowH = 18f + ResCardH + 44f;
            UiNode.Of(row).Place(0f, y, width, rowH);
            int n = ids.Count;
            float total = n * ResCardW + Math.Max(0, n - 1) * 32f;
            float x = (width - total) / 2f;
            for (int i = 0; i < n; i++)
            {
                picks.Add(ResPick(row, ids[i], i, pickable, book, x, 18f, select, learn));
                x += ResCardW + 32f;
            }
            y += rowH;
            if (!string.IsNullOrEmpty(note))
            {
                y += 18f - 10f; // the column's gap, the note's margin-top −10
                RectTransform p = ResNoteRow(main, width, note, true, ResFont(ResNoteCss, ink));
                UiNode np = UiNode.Of(p);
                np.Place(0f, y);
                y += np.H;
            }
            return y;
        }

        /// <summary>
        /// A card of the pick (.ocbg-explore-pick): the card face, the TargetFrame (shown when chosen, 140 ms), the key under
        /// it, dealt in (ocbg-explore-deal 460 ms, delay dur(120 + i·110)). Click selects, double-click learns.
        /// The stylesheet lifts a hovered or chosen card (translateY(−10)), but the deal's `both` fill keeps `transform:
        /// none` over that rule (a CSS animation wins over the cascade), so the JS cards never lift (reference shots 702,
        /// 703): none is ported.
        /// </summary>
        private ResultsPick ResPick(RectTransform row, string id, int i, bool pickable, BattleCore.MasteryBook book, float x, float y, Action<int> select, Action learn)
        {
            RectTransform wrap = Ui.Div(row, "ocbg-explore-pick" + (pickable ? " is-pickable" : " is-learned"));
            UiNode.Of(wrap).Place(x, y, ResCardW, ResCardH);
            ResCardFace(wrap, id, book);
            RectTransform frameEl = Ui.Div(wrap, "ocbg-explore-pick-frame");
            UiNode frameNode = UiNode.Of(frameEl);
            frameNode.Place(-10f, -10f, ResFrameW, ResFrameH);
            frameNode.PointerEvents = PointerEvents.None;
            Ocb.TargetFrame(frameEl, ResFrameW, ResFrameH, new OcbFrameOpts { Hook = 24f, Width = 3f });
            CssStates frame = CssStates.Of(frameEl).Base(ResPickFrameCss).State(".is-chosen", ResPickFrameChosenCss);
            frame.Apply(instant: true);
            BoxGraphic key = null;
            if (pickable)
            {
                key = ResPickKey(wrap, (i + 1).ToString(CultureInfo.InvariantCulture));
                ClickArea area = ClickArea.On(wrap);
                // The browser's click count (UIEvent.detail): it grows while each click comes within the system's
                // double-click time of the one before (uGUI's own clickCount allows only 0.3 s).
                float lastClick = float.NegativeInfinity;
                int clicks = 0;
                area.Click += e =>
                {
                    float now = CssTime.Now;
                    clicks = now - lastClick <= ResDoubleClickMs ? clicks + 1 : 1;
                    lastClick = now;
                    select(i);
                    // dblclick follows the second click: select(i) again, then learn
                    if (clicks == 2)
                    {
                        select(i);
                        learn();
                    }
                };
            }
            CssAnimator.Play(wrap, ResDealAnim(Dur(120 + i * 110)));
            return new ResultsPick { Wrap = wrap, Frame = frame, Key = key };
        }

        /// <summary>The deal's `animation` with its delay (ExploreScene.js:1116: wrap.style.animationDelay).</summary>
        private static string ResDealAnim(float delayMs) =>
            "ocbg-explore-deal 460ms " + delayMs.ToString("0.###", CultureInfo.InvariantCulture) + "ms cubic-bezier(.22,.61,.36,1) both";

        /// <summary>The pick's chosen card: its frame shows and its key turns amber (the card's .is-chosen).</summary>
        private static void ResSetChosen(List<ResultsPick> picks, int i)
        {
            for (int k = 0; k < picks.Count; k++)
            {
                ResultsPick p = picks[k];
                bool on = k == i;
                if (p.Frame) p.Frame.SetState(".is-chosen", on);
                if (p.Key) p.Key.SetCss(ExploreStyle.KbdCss + (on ? ResPickKeyChosenCss : ""));
            }
        }

        /// <summary>The pick's kbd (explore.css:11, :295): centred 38 under the card. A click on it is a click on its card.</summary>
        private static BoxGraphic ResPickKey(RectTransform wrap, string key)
        {
            TextStyle s = TextStyle.FromCss(ExploreStyle.KbdCss);
            s.Align = TextAlign.Center;
            s.WhiteSpace = WhiteSpace.NoWrap;
            float kw = Math.Max(24f, TextMeasure.Width(key, s) + 12f + 2f);
            BoxGraphic box = BoxGraphic.Make(wrap, ExploreStyle.KbdCss, (ResCardW - kw) / 2f, ResCardH + 38f - 24f, kw, 24f, "ocbg-explore-pick-key");
            Label.Make(box.rectTransform, key, s, 1f, 1f + (22f - s.LineHeightPx) / 2f, kw - 2f, s.LineHeightPx, "key");
            box.raycastTarget = true;
            return box;
        }

        /// <summary>
        /// OCB.Card(cardDefById(id, book), { fit: 'b' }) at (0, 0) of parent, or the stub with the card's name when the face
        /// cannot be written (ExploreScene.js:1702).
        /// </summary>
        private static RectTransform ResCardFace(RectTransform parent, string id, BattleCore.MasteryBook book) =>
            ResCardFace(parent, id, book, out _);

        /// <summary>ResCardFace; `stub` tells the stand-in (.ocbg-explore-cardstub, not an .ocb-card) from the card.</summary>
        private static RectTransform ResCardFace(RectTransform parent, string id, BattleCore.MasteryBook book, out bool stub)
        {
            stub = true;
            OcbCardDef def;
            try { def = ExploreCardFace.CardDefById(id, book); }
            catch (Exception) { return ResCardStub(parent, ExploreCardFace.CardName(id, book)); }
            try
            {
                OcbCard card = Ocb.Card(parent, def, new OcbCardState { Fit = "b" });
                stub = false;
                return card.Root;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return ResCardStub(parent, ExploreCardFace.CardName(id, book));
            }
        }

        /// <summary>.ocbg-explore-cardstub (explore.css:298): the card's name on a plain card.</summary>
        private static RectTransform ResCardStub(RectTransform parent, string name)
        {
            BoxGraphic box = BoxGraphic.Make(parent, ResCardStubCss, 0f, 0f, ResCardW, ResCardH, "ocbg-explore-cardstub");
            TextStyle s = TextStyle.FromCss(ResCardStubCss);
            s.Align = TextAlign.Center;
            TMP_Text t = Label.Make(box.rectTransform, name ?? "", s, 0f, 0f, ResCardW, null, "name");
            Ui.Place(t.rectTransform, 0f, (ResCardH - ResCssHeight(t, s, false)) / 2f);
            UiNode.Of(box.rectTransform);
            return box.rectTransform;
        }

        // ================================================================== the trace's and the taken card's rows

        /// <summary>A row of the trace's list: a candidate (a button) or a withheld card (a div), and its state classes.</summary>
        private sealed class ResultsTraceRow
        {
            public RectTransform El;
            public BoxGraphic Box;
            public string Id;
            public CandidateView C;
            public WithheldView W;
            /// <summary>The withheld heading this row brings into view with it (the first withheld row's).</summary>
            public RectTransform Heading;
            public BoxGraphic Check;
            public RectTransform Kind;
            public float KindW, KindH;
            public TMP_Text Name;
            public TMP_Text Detail;
            public RectTransform Gain;
            public string NameText;
            public string DetailText;
            public IReadOnlyList<string> Notes;
            public bool Withheld, Disabled, On, Locked, Nothing, Cursor, Hover, Focus;
            public float H;
            public TextStyle RowStyle;
            public Rgba Ink;
        }

        /// <summary>Builds a trace row's parts (the check, the kind tag, the name, the detail, the gain notes) into the list.</summary>
        private static void ResBuildTraceRow(ResultsTraceRow r, RectTransform content, TextStyle rowStyle, Rgba ink)
        {
            r.RowStyle = rowStyle;
            r.Ink = ink;
            r.El = Ui.Div(content, "ocbg-explore-trace-row" + (r.Withheld ? " is-withheld" : ""));
            UiNode.Of(r.El);
            r.Box = BoxGraphic.On(r.El, ResTraceRowCss + (r.Withheld ? ResTraceRowWithheldCss : ""));
            r.Check = BoxGraphic.Make(r.El, ResCheckCss + (r.Withheld ? ResCheckWithheldCss : ""), 0f, 0f, 22f, 22f, "ocbg-explore-check");
            if (!r.Withheld)
            {
                TextStyle ks = ResFont(ResTraceKindCss, ink).WithWhiteSpace(WhiteSpace.NoWrap);
                string kind = r.C.KindLabel ?? "";
                // padding 1 10 inside a 1px border: the label's line (22) + 4, its width + 22
                r.KindW = TextMeasure.Width(kind, ks) + 20f + 2f;
                r.KindH = ks.LineHeightPx + 4f;
                BoxGraphic tag = BoxGraphic.Make(r.El, ResTraceKindCss, 0f, 0f, r.KindW, r.KindH, "ocbg-explore-trace-kind");
                Label.Make(tag.rectTransform, kind, ks, 11f, 2f, null, null, "label");
                r.Kind = tag.rectTransform;
            }
            r.Name = Label.Make(r.El, r.NameText ?? "", ResFont(ResTraceRowStrongCss, ink), 0f, 0f, 1f, null, "strong");
            TextStyle ds = RichStyle(rowStyle);
            r.Detail = Label.Make(r.El, Rich(r.DetailText, ds, true), ds, 0f, 0f, null, null, "ocbg-explore-trace-detail");
            if (!r.Withheld)
            {
                r.Gain = Ui.Div(r.El, "ocbg-explore-trace-gain");
                r.Gain.gameObject.SetActive(false);
            }
        }

        /// <summary>A trace row's classes as its box, its check and its opacity (explore.css:305-327, in cascade order).</summary>
        private static void ResRestyleTraceRow(ResultsTraceRow r)
        {
            if (r == null || !r.El) return;
            string css = ResTraceRowCss
                + (r.Withheld ? ResTraceRowWithheldCss : "")
                + (r.Cursor ? ResTraceRowCursorCss : "")
                // :hover:not([disabled]) (0,4,0) wins over .is-cursor's background (0,3,0)
                + (r.Hover && !r.Disabled ? ResTraceRowHoverCss : "")
                + (r.Focus ? ResTraceRowFocusCss : "");
            r.Box.SetCss(css);
            float opacity = r.Withheld ? (r.Cursor ? 0.9f : 0.7f) : r.Locked ? 0.5f : 1f;
            UiNode.Of(r.El).Opacity = opacity;
            if (r.Check) r.Check.SetCss(ResCheckCss + (r.On ? ResCheckOnCss : "") + (r.Withheld ? ResCheckWithheldCss : ""));
        }

        /// <summary>
        /// Lays a trace row out (grid 28 | 150 | 1fr | auto, gap 14, padding 6 8, min-height 52, its bottom rule; a withheld
        /// row 28 | 1fr | auto): its texts as refresh() left them, the gain notes on a second row from the third column on.
        /// </summary>
        private static void ResLayTraceRow(ResultsTraceRow r, float width)
        {
            const float padX = 8f, padY = 6f, gap = 14f, border = 1f, minH = 52f;
            float innerW = width - 2f * padX;
            // the name and the detail as refresh() wrote them
            Label.SetText(r.Name, r.NameText ?? "");
            TextStyle ds = Label.StyleOf(r.Detail) ?? RichStyle(r.RowStyle);
            Label.SetText(r.Detail, Rich(r.DetailText ?? "", ds, true));
            float detailW = Math.Min(TextMeasure.Width(Label.StateOf(r.Detail).Text, ds), innerW * 0.5f);
            float nameX = r.Withheld ? padX + 28f + gap : padX + 28f + gap + 150f + gap;
            float nameW = Math.Max(1f, innerW - (nameX - padX) - gap - detailW);
            ResFitLabel(r.Name, nameW);
            TextStyle ns = Label.StyleOf(r.Name);
            float nameH = ResCssHeight(r.Name, ns, false);
            ResFitLabel(r.Detail, detailW + 0.5f);
            float detailH = ResCssHeight(r.Detail, ds, true);
            float r1 = Math.Max(22f, Math.Max(nameH, detailH));
            if (!r.Withheld) r1 = Math.Max(r1, 26f);

            // the gain notes: one block each, 16/22 (bold when the pick adds nothing)
            float gainH = 0f;
            bool hasGain = !r.Withheld && r.Notes != null && r.Notes.Count > 0;
            if (r.Gain)
            {
                Ui.Clear(r.Gain);
                r.Gain.gameObject.SetActive(hasGain);
                if (hasGain)
                {
                    float gw = innerW - (nameX - padX);
                    TextStyle gs = RichStyle(ResFont(ResTraceGainCss + (r.Nothing ? ResNothingCss : ""), r.Ink));
                    foreach (string note in r.Notes)
                    {
                        TMP_Text l = Label.Make(r.Gain, Rich(note, gs, true), gs, 0f, gainH, gw, null, "span");
                        gainH += ResCssHeight(l, gs, true);
                    }
                    UiNode.Of(r.Gain).Place(nameX, 0f, gw, gainH);
                }
            }
            float content = r1 + (hasGain ? 2f + gainH : 0f);
            float h = Math.Max(minH, padY + content + padY + border);
            // a single track stretches to the min-height; two tracks are already taller
            if (!hasGain) r1 = h - padY - padY - border;
            float cy = padY;
            Ui.Place(r.Check.rectTransform, padX, cy + (r1 - 22f) / 2f); // the 22 check at the start of its 28 column
            if (r.Kind) Ui.Place(r.Kind, padX + 28f + gap, cy + (r1 - r.KindH) / 2f); // justify-self: start
            Ui.Place(r.Name.rectTransform, nameX, cy + (r1 - nameH) / 2f);
            Ui.Place(r.Detail.rectTransform, width - padX - detailW, cy + (r1 - detailH) / 2f);
            if (hasGain) UiNode.Of(r.Gain).Place(nameX, cy + r1 + 2f);
            r.H = h;
            UiNode.Of(r.El).W = width;
            UiNode.Of(r.El).H = h;
        }

        /// <summary>The trace list's content: the candidate rows, the withheld heading (margin 14 0 4) and its rows, from the border down.</summary>
        private static void ResLayTraceList(ScrollArea list, List<ResultsTraceRow> rows, RectTransform heading, List<ResultsTraceRow> stops, float width)
        {
            if (!list) return;
            float y = ResListBorder;
            foreach (ResultsTraceRow r in rows)
            {
                ResLayTraceRow(r, width);
                UiNode.Of(r.El).Place(0f, y);
                y += r.H;
            }
            if (heading)
            {
                y += 14f;
                Ui.Place(heading, 0f, y);
                y += heading.rect.height + 4f;
                foreach (ResultsTraceRow r in stops)
                {
                    if (!r.Withheld) continue;
                    ResLayTraceRow(r, width);
                    UiNode.Of(r.El).Place(0f, y);
                    y += r.H;
                }
            }
            list.Content.sizeDelta = new Vector2(width, y);
            list.Refresh();
        }

        /// <summary>
        /// A row of the 受け取った遺産 card (.ocbg-explore-taken-item): grid 22 | 150 | 1fr | auto, gap 12 (icon, kind tag,
        /// name, detail), then its gain notes (margin 0 0 4 34, 16/22; bold when the pick gave nothing), a column with gap 2.
        /// </summary>
        private static RectTransform ResTakenItem(RectTransform parent, float width, LegacyReceiptRow c, Rgba ink)
        {
            RectTransform item = Ui.Div(parent, "ocbg-explore-taken-item" + (c.Nothing ? " is-nothing" : ""));
            RectTransform row = Ui.Div(item, "ocbg-explore-taken-row");
            const float gap = 12f;
            TextStyle ps = ResFont(ResSidePCss, ink);
            string icon = c.Kind == CandidateKind.Card ? "seal" : c.Kind == CandidateKind.EnemyPage ? "journal" : "page";
            PathGraphic g = ExploreIcons.Ico(row, icon, 18f, ink);
            TextStyle ks = ResFont(ResTraceKindCss, ink).WithWhiteSpace(WhiteSpace.NoWrap);
            string kind = c.KindLabel ?? "";
            float kw = TextMeasure.Width(kind, ks) + 20f + 2f, kh = ks.LineHeightPx + 4f;
            BoxGraphic tag = BoxGraphic.Make(row, ResTraceKindCss, 0f, 0f, kw, kh, "ocbg-explore-trace-kind");
            Label.Make(tag.rectTransform, kind, ks, 11f, 2f, null, null, "label");
            TextStyle ns = ResFont(ResSidePCss + ResTakenStrongCss, ink);
            TMP_Text detail = null;
            float detailW = 0f, detailH = 0f;
            TextStyle ds = RichStyle(ps);
            if (!string.IsNullOrEmpty(c.Detail))
            {
                string shown = Rich(c.Detail, ds, true);
                detailW = TextMeasure.Width(shown, ds);
                detail = Label.Make(row, shown, ds, 0f, 0f, null, null, "ocbg-explore-trace-detail");
                detailH = ResCssHeight(detail, ds, true);
            }
            float nameX = 22f + gap + 150f + gap;
            float nameW = Math.Max(1f, width - nameX - gap - detailW);
            TMP_Text name = Label.Make(row, c.Name ?? "", ns, nameX, 0f, nameW, null, "strong");
            float nameH = ResCssHeight(name, ns, false);
            float rowH = Math.Max(Math.Max(18f, kh), Math.Max(nameH, detailH));
            Ui.Place(g.rectTransform, 0f, (rowH - 18f) / 2f);
            Ui.Place(tag.rectTransform, 22f + gap, (rowH - kh) / 2f);
            Ui.Place(name.rectTransform, nameX, (rowH - nameH) / 2f);
            if (detail) Ui.Place(detail.rectTransform, width - detailW, (rowH - detailH) / 2f);
            UiNode.Of(row).Place(0f, 0f, width, rowH);
            float h = rowH;
            if (c.Notes != null && c.Notes.Count > 0)
            {
                // p.ocbg-explore-taken-gain: each note a block (span), margin 0 0 4 34
                RectTransform gain = Ui.Div(item, "ocbg-explore-taken-gain");
                TextStyle gs = RichStyle(ResFont(ResTakenGainCss + (c.Nothing ? ResNothingCss : ""), ink));
                float gw = Math.Max(1f, width - 34f), gy = 0f;
                foreach (string note in c.Notes)
                {
                    TMP_Text l = Label.Make(gain, Rich(note, gs, true), gs, 0f, gy, gw, null, "span");
                    gy += ResCssHeight(l, gs, true);
                }
                h += 2f;
                UiNode.Of(gain).Place(34f, h, gw, gy);
                h += gy + 4f;
            }
            UiNode.Of(item).Place(0f, 0f, width, h);
            return item;
        }

        // ================================================================== the training card's rows and preview

        /// <summary>A row of the training list (a button) and its state classes.</summary>
        private sealed class ResultsTrainRow
        {
            public RectTransform El;
            public BoxGraphic Box;
            public BoxGraphic Ring;
            public string Name, Copies, Ticks;
            public bool Quiet;
            public int Rank;
            public OcbAttrInfo Attr;
            public bool Hover, Chosen, Focus;
            public float H;
        }

        /// <summary>
        /// Builds a training row (grid 28 | 1fr | 48 | 132 | 96, gap 14, padding 4 12, min-height 46, its bottom rule): the
        /// attribute's icon in its ink, the name, ×copies, the 刻み (right; quiet 16 when no 段 can come), the 段 chip.
        /// </summary>
        private static void ResBuildTrainRow(ResultsTrainRow r, RectTransform content, float width, TextStyle rowStyle, Rgba ink)
        {
            const float padX = 12f, padY = 4f, gap = 14f, border = 1f, minH = 46f;
            float innerW = width - 2f * padX;
            float c1 = padX, c2 = c1 + 28f + gap;
            float nameW = innerW - 28f - 48f - 132f - 96f - 4f * gap;
            float c3 = c2 + nameW + gap, c4 = c3 + 48f + gap, c5 = c4 + 132f + gap;
            r.El = Ui.Div(content, "ocbg-explore-train-row");
            UiNode.Of(r.El);
            r.Box = BoxGraphic.On(r.El, ResTrainRowCss);
            PathGraphic icon = r.Attr != null ? Ocb.Icon(r.El, r.Attr.Icon, 24f, r.Attr.Ink) : null;
            TextStyle ns = ResFont(ResTrainRowCss + ResTrainRowStrongCss, ink);
            TMP_Text name = Label.Make(r.El, r.Name ?? "", ns, c2, 0f, Math.Max(1f, nameW), null, "strong");
            float nameH = ResCssHeight(name, ns, false);
            TextStyle cs = RichStyle(ResFont(ResTrainRowCss + ResTrainCopiesCss, ink));
            TMP_Text copies = Label.Make(r.El, Rich(r.Copies, cs), cs, c3, 0f, null, null, "ocbg-explore-train-copies");
            float copiesH = ResCssHeight(copies, cs, true);
            TextStyle ts = RichStyle(ResFont(ResTrainRowCss + ResTrainTicksCss + (r.Quiet ? ResTrainTicksQuietCss : ""), ink));
            string ticksShown = Rich(r.Ticks, ts);
            float tw = TextMeasure.Width(ticksShown, ts);
            TMP_Text ticks = Label.Make(r.El, ticksShown, ts, c4 + 132f - tw, 0f, null, null, "ocbg-explore-train-ticks");
            float ticksH = ResCssHeight(ticks, ts, true);
            // .ocbg-explore-rank: a chip at the end of its column (0 段: steel, 400)
            string rankCss = ResRankCss + (r.Rank > 0 ? "" : ResRankZeroCss);
            TextStyle rs = RichStyle(ResFont(rankCss, ink));
            string rankText = ExploreText.Rank(r.Rank);
            string rankShown = Rich(rankText, rs);
            float rw = TextMeasure.Width(rankShown, rs) + 20f + 2f;
            float rh = (ResHasDigit(rankText) ? ResNumLineBox(rs) : rs.LineHeightPx) + 2f + 2f;
            BoxGraphic chip = BoxGraphic.Make(r.El, rankCss, c5 + 96f - rw, 0f, rw, rh, "ocbg-explore-rank");
            Label.Make(chip.rectTransform, rankShown, rs, 11f, 2f, null, null, "label");

            float content1 = Math.Max(Math.Max(24f, nameH), Math.Max(Math.Max(copiesH, ticksH), rh));
            float h = Math.Max(minH, padY + content1 + padY + border);
            float track = h - padY - padY - border;
            if (icon) Ui.Place(icon.rectTransform, c1, padY + (track - 24f) / 2f);
            Ui.Place(name.rectTransform, c2, padY + (track - nameH) / 2f);
            Ui.Place(copies.rectTransform, c3, padY + (track - copiesH) / 2f);
            Ui.Place(ticks.rectTransform, c4 + 132f - tw, padY + (track - ticksH) / 2f);
            Ui.Place(chip.rectTransform, c5 + 96f - rw, padY + (track - rh) / 2f);
            r.H = h;
            UiNode.Of(r.El).Place(0f, 0f, width, h);
        }

        /// <summary>A training row's classes (explore.css:335-338: :hover, then .is-chosen, which wins) and Chrome's focus ring.</summary>
        private static void ResRestyleTrainRow(ResultsTrainRow r)
        {
            if (r == null || !r.El) return;
            r.Box.SetCss(ResTrainRowCss + (r.Hover ? ResTrainRowHoverCss : "") + (r.Chosen ? ResTrainRowChosenCss : ""));
            if (r.Focus && !r.Ring)
            {
                r.Ring = BoxGraphic.Make(r.El, ResChromeFocusRingCss, 0f, 0f, UiNode.Of(r.El).W, r.H, "focus-ring");
            }
            if (r.Ring) r.Ring.gameObject.SetActive(r.Focus);
        }

        /// <summary>.ocbg-explore-train-about: the preview's lines (left −1, top 318, width 236, gap 4; 18/26).</summary>
        private static void ResTrainAbout(RectTransform preview, List<string> lines, Rgba ink)
        {
            RectTransform about = Ui.Div(preview, "ocbg-explore-train-about");
            TextStyle s = RichStyle(ResFont(ResTrainAboutPCss, ink));
            float y = 0f;
            bool first = true;
            foreach (string line in lines)
            {
                if (!first) y += 4f;
                first = false;
                TMP_Text t = Label.Make(about, Rich(line, s), s, 0f, y, 236f, null, "p");
                y += ResCssHeight(t, s, true);
            }
            // absolute from the padding box (inside the 1px dashed border): the border box's 0, 319
            UiNode.Of(about).Place(0f, 1f + 318f, 236f, y);
        }

        // ================================================================== the plate's layout

        /// <summary>One child of a plate (explore.css:223: a flex column, gap 14): its element, height and flex-item rules.</summary>
        private sealed class ResultsBlock
        {
            public RectTransform El;
            public float H;
            /// <summary>CSS margin-top (negative pulls the block up).</summary>
            public float MarginTop;
            /// <summary>flex: 1; min-height: 0: the block takes the free height of a fixed-height plate.</summary>
            public bool Grow;
            /// <summary>margin-top: auto (the buttons row sits at the bottom of a fixed-height plate with no growing block).</summary>
            public bool AutoTop;
        }

        /// <summary>An item of .ocbg-explore-result-side: a group, or a note that spans the compact grid's two columns.</summary>
        private sealed class ResultsSideItem
        {
            public bool Full;
            public Func<RectTransform, float, RectTransform> Build;
        }

        /// <summary>A card of the pick: its wrapper, its frame's states and its key.</summary>
        private sealed class ResultsPick
        {
            public RectTransform Wrap;
            public CssStates Frame;
            public BoxGraphic Key;
        }

        /// <summary>The plate's content width: its width less the 1px border and the padding 44 on each side.</summary>
        private static float ResInnerW(float w) => w - 2f * ResBorder - ResPadL - ResPadR;

        /// <summary>
        /// The plate's flex column (explore.css:223): the blocks one under the other from the padding, gap 14, each its
        /// margin-top; in a fixed-height plate a growing block takes the free height, else margin-top: auto does. An
        /// auto-height plate takes the height of what it holds.
        /// </summary>
        private static void ResLayPlate(RectTransform el, List<ResultsBlock> blocks, float? fixedH)
        {
            var live = blocks.Where(b => b != null && b.El).ToList();
            float used = 0f;
            foreach (ResultsBlock b in live) used += (b.Grow ? 0f : b.H) + b.MarginTop;
            used += Math.Max(0, live.Count - 1) * ResGap;
            float inner = fixedH.HasValue ? fixedH.Value - 2f * ResBorder - ResPadT - ResPadB : used;
            float free = Math.Max(0f, inner - used);
            bool grows = live.Any(b => b.Grow);
            UiNode plate = UiNode.Of(el);
            float innerW = plate.W - 2f * ResBorder - ResPadL - ResPadR;
            float y = ResBorder + ResPadT;
            for (int i = 0; i < live.Count; i++)
            {
                ResultsBlock b = live[i];
                if (i > 0) y += ResGap;
                if (b.AutoTop && !grows) y += free;
                y += b.MarginTop;
                if (b.Grow) b.H = free;
                UiNode.Of(b.El).Place(ResBorder + ResPadL, y, innerW, b.H);
                y += b.H;
            }
            if (!fixedH.HasValue) plate.H = y + ResPadB + ResBorder;
        }

        /// <summary>An auto-height card: laid out, then centred on the map (fitPlate: never under the band).</summary>
        private void ResLayAutoPlate(RectTransform el, List<ResultsBlock> blocks)
        {
            ResLayPlate(el, blocks, null);
            UiNode plate = UiNode.Of(el);
            // fitPlate's own rule (ExploreScene.js:886), set here too so the card stands right whatever FitPlate reads
            plate.Y = Math.Max(120f, (float)Math.Floor(ExploreStyle.MapCenterY - plate.H / 2f + 0.5f));
            FitPlate(el);
        }

        /// <summary>A block of text the width of the plate (h2, the trace's lines, the training's lead).</summary>
        private static ResultsBlock ResTextBlock(RectTransform plate, float innerW, string name, string text, TextStyle style, bool rich, bool onWashi, float marginTop = 0f)
        {
            RectTransform b = Ui.Div(plate, name);
            TextStyle s = rich ? RichStyle(style) : style;
            TMP_Text t = Label.Make(b, rich ? Rich(text, s, onWashi) : text ?? "", s, 0f, 0f, innerW, null, "text");
            float h = ResCssHeight(t, s, rich);
            UiNode.Of(b).Place(0f, 0f, innerW, h);
            return new ResultsBlock { El = b, H = h, MarginTop = marginTop };
        }

        /// <summary>
        /// .ocbg-explore-plate-kicker: the icon, gap 10, the words (rich when the JS writes rich()), and the event's sub
        /// (margin-left 10, padding-left 12, a rule on its left, weight 400), centred on one another.
        /// </summary>
        private static ResultsBlock ResKicker(RectTransform plate, string icon, float size, Rgba iconColor, string text, bool rich, TextStyle style, bool onWashi, string sub = null)
        {
            RectTransform k = Ui.Div(plate, "ocbg-explore-plate-kicker");
            PathGraphic g = ExploreIcons.Ico(k, icon, size, iconColor);
            TextStyle ts = rich ? RichStyle(style) : style.WithWhiteSpace(WhiteSpace.NoWrap);
            if (rich) ts.WhiteSpace = WhiteSpace.NoWrap;
            TMP_Text t = Label.Make(k, rich ? Rich(text, ts, onWashi) : text ?? "", ts, size + 10f, 0f, null, null, "text");
            float th = rich && ResHasDigit(text) ? ResNumLineBox(ts) : ts.LineHeightPx;
            float h = Math.Max(size, th);
            float x = size + 10f + t.rectTransform.sizeDelta.x;
            RectTransform subEl = null;
            TMP_Text subText = null;
            float subH = 0f;
            if (sub != null)
            {
                TextStyle ss = RichStyle(TextStyle.FromCss(ResKickerCss + ResKickerSubCss).WithColor(style.Color));
                ss.WhiteSpace = WhiteSpace.NoWrap;
                string shown = Rich(sub, ss, onWashi);
                float sw = TextMeasure.Width(shown, ss);
                subH = ResHasDigit(sub) ? ResNumLineBox(ss) : ss.LineHeightPx;
                x += 10f + 10f; // the row's gap, the sub's margin-left
                BoxGraphic sb = BoxGraphic.Make(k, ResKickerSubCss, x, 0f, 1f + 12f + sw, subH, "ocbg-explore-kicker-sub");
                subEl = sb.rectTransform;
                subText = Label.Make(subEl, shown, ss, 1f + 12f, 0f, null, null, "text");
                h = Math.Max(h, subH);
            }
            Ui.Place(g.rectTransform, 0f, (h - size) / 2f);
            Ui.Place(t.rectTransform, size + 10f, (h - th) / 2f);
            if (subEl) Ui.Place(subEl, x, (h - subH) / 2f);
            UiNode.Of(k).Place(0f, 0f, 0f, h);
            return new ResultsBlock { El = k, H = h };
        }

        /// <summary>
        /// An .ocbg-btn of a card's buttons row: min-width 220, its label centred; on washi (explore.css:235-237, :56) the
        /// panel-solid fill, the primary's amber 24 % (36 % hovered) and the 3px washi-ink focus ring. In the scene's Tab order.
        /// </summary>
        private OcbgButton ResPlateButton(RectTransform row, string text, bool primary, bool danger, bool washi, Action onClick)
        {
            OcbgButton b = OcbgButton.Make(row, text, primary, danger, "ocbg-btn");
            Action<bool> ring = null;
            if (washi)
            {
                b.States.State(".is-washi .ocbg-btn", ResWashiBtnCss, true)
                    .State(".is-washi .ocbg-btn.is-primary", ResWashiPrimaryCss, primary)
                    .State(".is-washi .ocbg-btn.is-primary:hover", ResWashiPrimaryHoverCss, false)
                    .State(".is-washi .ocbg-btn:focus-visible", ResWashiFocusCss, false);
                b.States.Apply(instant: true);
                b.Area.HoverChanged += on => { if (b.Root) b.States.SetState(".is-washi .ocbg-btn.is-primary:hover", b.Primary && on); };
                ring = on => { if (b.Root) b.States.SetState(".is-washi .ocbg-btn:focus-visible", on); };
            }
            if (onClick != null) b.Click += onClick;
            AddFocus(b, null, ring);
            return b;
        }

        /// <summary>
        /// The buttons row (explore.css:230-231): right-aligned, gap 16, centred on one another, padding-top `padTop`
        /// (18 on a result card), margin-top auto; `left` (the trace's count) is pushed to the start (margin-right auto).
        /// </summary>
        private static ResultsBlock ResLayButtons(RectTransform row, float innerW, float padTop, RectTransform left, float leftH, params OcbgButton[] buttons)
        {
            float rowH = leftH;
            var sizes = new List<Vector2>();
            foreach (OcbgButton b in buttons)
            {
                Vector2 n = b.NaturalSize;
                sizes.Add(n);
                rowH = Math.Max(rowH, n.y);
            }
            float x = innerW;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                float bw = Math.Max(220f, sizes[i].x);
                x -= bw;
                buttons[i].SetBox(x, padTop + (rowH - sizes[i].y) / 2f, bw, sizes[i].y);
                x -= 16f;
            }
            if (left) Ui.Place(left, 0f, padTop + (rowH - leftH) / 2f);
            float h = padTop + rowH;
            UiNode.Of(row).Place(0f, 0f, innerW, h);
            return new ResultsBlock { El = row, H = h, AutoTop = true };
        }

        /// <summary>The 1px rule over a list (border-top): the rows start under it.</summary>
        private const float ResListBorder = 1f;

        /// <summary>.ocbg-explore-train-list max-height.</summary>
        private const float ResTrainListMax = 470f;

        /// <summary>
        /// A scrolling list (overflow: auto) w × h at (0, 0) of `el`: its top rule (border-top in `ruleColor`, the washi-ink
        /// 25 % of the trace's list by default) stays put while the rows scroll; the edge fades of explore.css:321-323.
        /// </summary>
        private static ScrollArea ResListArea(RectTransform el, float w, float h, string css)
        {
            UiNode node = UiNode.Of(el);
            node.W = w;
            node.H = h;
            ScrollArea area = ScrollArea.Attach(el);
            area.ShowThumb = false; // the reference build shows no scrollbar over these lists
            area.Content.sizeDelta = new Vector2(w, 0f);
            area.SetFades(ResListMoreBelow, ResListMoreAbove, ResListMoreBoth);
            // the list's own declarations on a 1px box: its border-top (the box has no fill), drawn over the rows
            BoxGraphic line = BoxGraphic.Make(el, css, 0f, 0f, w, ResListBorder, "border-top");
            line.transform.SetAsLastSibling();
            return area;
        }

        /// <summary>A row's top-left in the scroll content's px (for a row deeper than the content's own children).</summary>
        private static Vector2 ResContentPos(ScrollArea area, RectTransform row)
        {
            var corners = new Vector3[4];
            row.GetWorldCorners(corners); // bl, tl, tr, br
            Vector3 tl = area.Content.InverseTransformPoint(corners[1]);
            Rect cr = area.Content.rect;
            return new Vector2(tl.x - cr.xMin, cr.yMax - tl.y);
        }

        // ================================================================== text

        /// <summary>A text style from declarations, in the card's ink.</summary>
        private static TextStyle ResFont(string css, Rgba ink) => TextStyle.FromCss(css).WithColor(ink);

        /// <summary>The digit (0-9) the number face sets (rich: .ocbg-explore-num).</summary>
        private static bool ResHasDigit(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (c >= '0' && c <= '9') return true;
            return false;
        }

        /// <summary>A key's number (JS Number(e.key) for the keys 1 … 9), 0 otherwise.</summary>
        private static int ResKeyNumber(KeyEvent e) => e != null && e.Key != null && e.Key.Length == 1 && e.Key[0] >= '0' && e.Key[0] <= '9' ? e.Key[0] - '0' : 0;

        // js: (Blink's line box: LayoutNG NGLineHeightMetrics::AddLeading)
        /// <summary>
        /// The height of a line that holds a digit run in the number face (IBM Plex Mono 600, .ocbg-explore-num) besides the
        /// element's own face, as Blink lays it out: each face's ascent and descent rounded to px, the half-leading floored
        /// on the ascent side, the line from the highest ascent to the lowest descent. A 22px/28px line of Zen Kaku Gothic
        /// New with Plex digits is 30 tall (the result card's stat chips are 42: reference shots 701, 726), an 18px/26px one 28.
        /// </summary>
        private static float ResNumLineBox(TextStyle s)
        {
            float L = s.LineHeightPx;
            void Metric(Family f, int weight, out float a, out float d)
            {
                FontMetrics m = TextMath.Metrics(f, weight);
                float asc = (float)Math.Floor(m.AscentEm * s.Size + 0.5);
                float desc = (float)Math.Floor(m.DescentEm * s.Size + 0.5);
                float half = (L - (asc + desc)) / 2f;
                a = asc + (float)Math.Floor(half);
                d = L - a;
            }
            Metric(s.Family, s.Weight, out float sa, out float sd);
            Metric(Family.Mono, 600, out float na, out float nd);
            return Math.Max(sa, na) + Math.Max(sd, nd);
        }

        /// <summary>
        /// A label's CSS height: each line is line-height tall, and a rich label's line that holds a digit is the taller
        /// mixed line box (ResNumLineBox). TMP lays the lines at line-height, so a digit line's growth is below its text.
        /// </summary>
        private static float ResCssHeight(TMP_Text t, TextStyle s, bool rich)
        {
            if (!t || s == null) return 0f;
            LabelState st = Label.StateOf(t);
            if (string.IsNullOrEmpty(st.Text)) return 0f;
            float L = s.LineHeightPx;
            t.ForceMeshUpdate(true, true);
            TMP_TextInfo info = t.textInfo;
            if (info == null || info.lineCount == 0)
            {
                // not laid out (no live canvas yet): the measuring label's count
                float w = t.rectTransform.rect.width;
                int n = st.AutoW ? 1 : TextMeasure.Lines(st.Text, s, w > 0f ? w : 100000f);
                return n * L + (rich && ResHasDigit(st.Text) ? ResNumLineBox(s) - L : 0f);
            }
            if (!rich) return info.lineCount * L;
            float box = ResNumLineBox(s);
            float h = 0f;
            for (int i = 0; i < info.lineCount; i++) h += ResLineHasDigit(info, i) ? box : L;
            return h;
        }

        /// <summary>Whether a laid-out line holds a digit.</summary>
        private static bool ResLineHasDigit(TMP_TextInfo info, int line)
        {
            TMP_LineInfo li = info.lineInfo[line];
            for (int c = li.firstCharacterIndex; c <= li.lastCharacterIndex && c < info.characterCount; c++)
            {
                char ch = info.characterInfo[c].character;
                if (ch >= '0' && ch <= '9') return true;
            }
            return false;
        }

        /// <summary>Whether the label's last line holds a digit.</summary>
        private static bool ResHasDigitLastLine(TMP_Text t)
        {
            TMP_TextInfo info = t ? t.textInfo : null;
            if (info == null || info.lineCount == 0) return false;
            return ResLineHasDigit(info, info.lineCount - 1);
        }

        /// <summary>Gives a label a width to wrap in (its height follows its lines).</summary>
        private static void ResFitLabel(TMP_Text t, float w)
        {
            if (!t) return;
            LabelState st = Label.StateOf(t);
            st.AutoW = false;
            st.AutoH = true;
            Ui.Size(t.rectTransform, Math.Max(1f, w), t.rectTransform.sizeDelta.y);
            Label.Relayout(t);
        }
    }

    /// <summary>
    /// mousemove with a real movement (e.movementX || e.movementY, ExploreScene.js:2100): the pointer moved over the
    /// element, not the element under a resting pointer (a list the keys scrolled). The browser's mousemove also goes to
    /// the element the pointer has just moved into (with its mouseover), while uGUI sends a move only to what was
    /// hovered before the move (InputSystemUIInputModule: move events first, then enter / exit): an enter that comes with
    /// a movement counts as that move. An enter with no movement (rows sliding under a resting pointer) does not.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    internal sealed class ExploreResultsPointerMove : MonoBehaviour, IPointerMoveHandler, IPointerEnterHandler
    {
        public Action OnMove;

        public void OnPointerMove(PointerEventData eventData) => Moved(eventData);

        public void OnPointerEnter(PointerEventData eventData) => Moved(eventData);

        private void Moved(PointerEventData eventData)
        {
            if (eventData == null || eventData.delta.sqrMagnitude <= 0f) return;
            try { OnMove?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            Microtasks.Drain();
        }
    }
}
