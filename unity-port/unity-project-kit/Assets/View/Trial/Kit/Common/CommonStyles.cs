// common.css (src/scenes/common/common.css) as named declaration blocks, copied verbatim so a reviewer can diff them
// against the stylesheet, plus the two design-system rules the stage's pieces sit in (bundle.css:4 .ocb-root, :12
// .ocb-solid). Rules for one element are composed in cascade order (specificity, then stylesheet order) by the
// helpers below and parsed by Trial.Geometry's Css (var(), color-mix(), shorthands, the cascade of repeated properties).
using System.Text;
using Trial.Geometry;

namespace Trial.Kit
{
    public static class CommonStyles
    {
        // ---- common.css, verbatim ------------------------------------------------------------------------------

        /// <summary>common.css:2 html, body.</summary>
        public const string BodyCss = "margin: 0; height: 100%; background: var(--ground); overflow: hidden;";

        /// <summary>common.css:3 .ocbg-viewport.</summary>
        public const string ViewportCss = "position: fixed; inset: env(safe-area-inset-top, 0px) env(safe-area-inset-right, 0px) env(safe-area-inset-bottom, 0px) env(safe-area-inset-left, 0px); background: var(--ground); overflow: hidden; touch-action: none;";

        /// <summary>common.css:4 .ocbg-stage.</summary>
        public const string StageCss = "position: absolute; left: 0; top: 0; width: 1920px; height: 1080px; transform-origin: 0 0; overflow: hidden; user-select: none; -webkit-user-select: none; font-family: var(--font-body); color: var(--ink);";

        /// <summary>common.css:5 .ocbg-scene.</summary>
        public const string SceneCss = "position: absolute; inset: 0; overflow: hidden;";

        /// <summary>common.css:6 .ocbg-btn.</summary>
        public const string BtnCss = "font: 700 22px/1 var(--font-body); letter-spacing: .06em; color: var(--ink); background: var(--panel-solid); border: 1px solid var(--steel); border-radius: var(--radius-panel); padding: 16px 28px; cursor: pointer;";

        /// <summary>common.css:7 .ocbg-btn:hover.</summary>
        public const string BtnHoverCss = "border-color: var(--ink); background: rgba(40,44,58,.92);";

        /// <summary>common.css:8 .ocbg-btn:focus-visible (in the kit: the Tab focus ring, Focus.cs).</summary>
        public const string BtnFocusVisibleCss = "outline: 2px solid var(--wick); outline-offset: 2px;";

        /// <summary>common.css:9 .ocbg-btn.is-primary.</summary>
        public const string BtnPrimaryCss = "border: 2px solid var(--amber); background: rgba(232,168,76,.16);";

        /// <summary>common.css:10 .ocbg-btn.is-primary:hover.</summary>
        public const string BtnPrimaryHoverCss = "background: rgba(232,168,76,.28);";

        /// <summary>common.css:11 .ocbg-btn.is-danger.</summary>
        public const string BtnDangerCss = "border-color: var(--hp-text); background: rgba(194,72,63,.18);";

        /// <summary>common.css:12 .ocbg-btn[disabled].</summary>
        public const string BtnDisabledCss = "opacity: .4; cursor: default;";

        /// <summary>common.css:13 .ocbg-confirm (the shade).</summary>
        public const string ConfirmCss = "position: absolute; inset: 0; z-index: 900; display: flex; align-items: center; justify-content: center; background: rgba(5,6,10,.62);";

        /// <summary>common.css:14 .ocbg-confirm-card.</summary>
        public const string ConfirmCardCss = "width: 760px; padding: 32px 36px 28px; box-shadow: var(--shadow-tip);";

        /// <summary>common.css:15 .ocbg-confirm-card h3.</summary>
        public const string ConfirmCardH3Css = "margin: 0 0 12px; font: 700 30px/40px var(--font-display);";

        /// <summary>common.css:16 .ocbg-confirm-card p.</summary>
        public const string ConfirmCardPCss = "margin: 0; font: 400 20px/32px var(--font-body);";

        /// <summary>common.css:17 .ocbg-confirm-row.</summary>
        public const string ConfirmRowCss = "display: flex; justify-content: flex-end; gap: 16px; margin-top: 28px;";

        /// <summary>common.css:18 .ocbg-notice.</summary>
        public const string NoticeCss = "position: absolute; z-index: 880; box-sizing: border-box; padding: 12px 20px; font: 400 20px/30px var(--font-body); color: var(--ink); background: var(--panel-solid); border: 1px solid var(--rule); border-left: 3px solid var(--caution); border-radius: var(--radius-panel); box-shadow: var(--shadow-tip); white-space: pre-line; transition: opacity .6s; pointer-events: none;";

        /// <summary>common.css:19 .ocbg-notice.is-leaving.</summary>
        public const string NoticeLeavingCss = "opacity: 0;";

        /// <summary>common.css:20 @media (prefers-reduced-motion: reduce) .ocbg-stage * (in the kit: CssTime.ReducedMotion).</summary>
        public const string ReducedMotionCss = "animation-duration: 1ms !important; transition-duration: 1ms !important;";

        /// <summary>common.css:23 .ocbg-stage .ocb-card .ocb-reach small (the card's 「自分」 tag: 17 px in the game, 14 in the DS).</summary>
        public const string CardReachSmallCss = "font-size: 17px;";

        // ---- the design-system rules the stage's pieces sit in ------------------------------------------------------

        /// <summary>vendor/ds/bundle.css:4 .ocb-root (the stage root carries it, stage.js:49).</summary>
        public const string OcbRootCss = "font-family: var(--font-body); color: var(--ink); -webkit-font-smoothing: antialiased; line-height: 1.375;";

        /// <summary>vendor/ds/bundle.css:12 .ocb-root .ocb-solid (the confirm card's second class).</summary>
        public const string OcbSolidCss = "background: var(--panel-solid); border: var(--stroke-rule) solid var(--steel); border-radius: var(--radius-panel);";

        // ---- composed rules ---------------------------------------------------------------------------------------

        /// <summary>
        /// The declarations of an .ocbg-btn in a state, in cascade order: .ocbg-btn, then the (0,2,0) rules in stylesheet
        /// order (:hover, :focus-visible, .is-primary, .is-danger, [disabled]), then .is-primary:hover (0,3,0). So a
        /// hovered danger button shows the primary hover background with the danger border, as in the browser.
        /// </summary>
        public static string Btn(bool primary = false, bool danger = false, bool hover = false, bool focusVisible = false, bool disabled = false)
        {
            var sb = new StringBuilder(BtnCss);
            if (hover) sb.Append(BtnHoverCss);
            if (focusVisible) sb.Append(BtnFocusVisibleCss);
            if (primary) sb.Append(BtnPrimaryCss);
            if (danger) sb.Append(BtnDangerCss);
            if (disabled) sb.Append(BtnDisabledCss);
            if (primary && hover) sb.Append(BtnPrimaryHoverCss);
            return sb.ToString();
        }

        /// <summary>The box of an .ocbg-btn in a state.</summary>
        public static BoxStyle BtnBox(bool primary = false, bool danger = false, bool hover = false, bool focusVisible = false, bool disabled = false) =>
            Css.Box(Btn(primary, danger, hover, focusVisible, disabled));

        /// <summary>The text of an .ocbg-btn (700 22px/1 body, .06em, ink), centred as a button centres its content.</summary>
        public static TextStyle BtnText()
        {
            TextStyle s = TextStyle.FromCss(OcbRootCss + BtnCss);
            s.Align = TextAlign.Center;
            s.WhiteSpace = WhiteSpace.NoWrap;
            return s;
        }

        /// <summary>The confirm card's box (.ocbg-confirm-card.ocb-solid).</summary>
        public static BoxStyle ConfirmCardBox() => Css.Box(OcbSolidCss + ConfirmCardCss);

        /// <summary>The confirm card's title (h3: 700 30px/40px display).</summary>
        public static TextStyle ConfirmTitle() => TextStyle.FromCss(OcbRootCss + ConfirmCardH3Css);

        /// <summary>The confirm card's body (p: 400 20px/32px body).</summary>
        public static TextStyle ConfirmBody() => TextStyle.FromCss(OcbRootCss + ConfirmCardPCss);

        /// <summary>The notice's text (400 20px/30px body, ink, pre-line).</summary>
        public static TextStyle NoticeText() => TextStyle.FromCss(OcbRootCss + NoticeCss);
    }
}
