// ExploreScene — the node detail panel (ExploreScene.js:597-659 drawDetail; explore.css:133-178, 254-255;
// explore.md §5.5). Owned by EX-PANELS.
//
// The panel shows the hovered node (read only) or the pinned node with its ways in. It is rebuilt only when what it
// shows changed (drawDetail's key), and laid out here in stage px as the stylesheet's flex column lays it out: the head
// (grid 72 | 1fr | auto), the lines (grid 92 | 1fr, baseline), the options pinned to the bottom (margin-top: auto). When
// the content overflows the panel it is set again tighter (is-tight, then is-tighter), as the JS does.
using System;
using System.Collections.Generic;
using Exploration;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace Trial.Explore
{
    public sealed partial class ExploreScene
    {
        // ------------------------------------------------------------------ explore.css, verbatim (the node detail)

        /// <summary>explore.css:134 .ocbg-explore-detail.</summary>
        private const string DetailCss = "z-index: 3; display: flex; flex-direction: column; gap: 16px; padding: 24px 28px; background: var(--panel); border: 1px solid var(--rule); border-radius: var(--radius-panel); overflow: hidden; transition: opacity 400ms ease-in-out;";

        /// <summary>explore.css:135 .ocbg-explore-detail-head.</summary>
        private const string DetailHeadCss = "display: grid; grid-template-columns: 72px 1fr auto; column-gap: 16px; align-items: center;";

        /// <summary>explore.css:136 .ocbg-explore-detail-token.</summary>
        private const string TokenCss = "width: 72px; height: 72px; display: flex; align-items: center; justify-content: center; background: var(--panel-solid); border: 2px solid var(--steel); border-radius: var(--radius-panel);";

        /// <summary>explore.css:137 .ocbg-explore-detail-token.is-current, .is-reachable.</summary>
        private const string TokenLitCss = "border-color: var(--amber);";

        /// <summary>explore.css:138 .ocbg-explore-detail-token.is-far.</summary>
        private const string TokenFarCss = "border-style: dashed;";

        /// <summary>explore.css:139 .ocbg-explore-detail-token.kind-boss.</summary>
        private const string TokenBossCss = "outline: 2px solid var(--boss); outline-offset: 3px;";

        /// <summary>explore.css:140 .ocbg-explore-detail-titles h2.</summary>
        private const string DetailH2Css = "margin: 0; font: 700 32px/40px var(--font-display); letter-spacing: .04em;";

        /// <summary>explore.css:141 .ocbg-explore-detail-titles p.</summary>
        private const string DetailNoteCss = "margin: 2px 0 0; font: 400 18px/26px var(--font-body);";

        /// <summary>explore.css:142 .ocbg-explore-pill.</summary>
        private const string PillCss = "align-self: start; padding: 3px 12px; font: 700 16px/22px var(--font-body); border: 1px solid var(--steel); border-radius: var(--radius-chip-self); white-space: nowrap;";

        /// <summary>explore.css:143 .ocbg-explore-pill.is-current.</summary>
        private const string PillCurrentCss = "border-color: var(--amber); background: color-mix(in srgb, var(--amber) 22%, transparent);";

        /// <summary>explore.css:144 .ocbg-explore-pill.is-reachable.</summary>
        private const string PillReachableCss = "border-color: var(--amber);";

        /// <summary>explore.css:145 .ocbg-explore-pill.is-revisit.</summary>
        private const string PillRevisitCss = "border-color: var(--ink);";

        /// <summary>explore.css:146 .ocbg-explore-pill.is-far.</summary>
        private const string PillFarCss = "border-style: dashed;";

        /// <summary>explore.css:147 .ocbg-explore-detail-lines.</summary>
        private const string DetailLinesCss = "display: flex; flex-direction: column; gap: 8px; padding-top: 14px; border-top: 1px solid var(--rule);";

        /// <summary>explore.css:148 .ocbg-explore-kv (explore.css:149 .is-wide: grid-template-columns: 1fr).</summary>
        private const string KvCss = "display: grid; grid-template-columns: 92px 1fr; column-gap: 12px; align-items: baseline;";

        /// <summary>explore.css:150 .ocbg-explore-k (also the carried rows' key).</summary>
        private const string KeyCss = "font: 700 16px/24px var(--font-body); letter-spacing: .04em;";

        /// <summary>explore.css:151 .ocbg-explore-v.</summary>
        private const string ValueCss = "font: 400 20px/28px var(--font-body);";

        /// <summary>explore.css:152 .ocbg-explore-detail-far.</summary>
        private const string DetailFarCss = "margin: 0; padding-top: 14px; border-top: 1px solid var(--rule); font: 400 18px/26px var(--font-body);";

        /// <summary>explore.css:153 .ocbg-explore-options (explore.css:154 .n2: grid-template-columns: 1fr 1fr).</summary>
        private const string OptionsCss = "margin-top: auto; display: grid; gap: 14px;";

        /// <summary>explore.css:155 .ocbg-explore-option.</summary>
        private const string OptionCss = "display: flex; flex-direction: column; gap: 6px; padding: 12px 14px 14px; background: color-mix(in srgb, var(--panel-solid) 85%, transparent); border: 1px solid var(--rule); border-radius: var(--radius-panel);";

        /// <summary>explore.css:156 .ocbg-explore-option.is-lethal.</summary>
        private const string OptionLethalCss = "border-color: var(--caution);";

        /// <summary>explore.css:157 .ocbg-explore-option.is-bare.</summary>
        private const string OptionBareCss = "padding: 0; background: none; border: 0;";

        /// <summary>explore.css:158 .ocbg-explore-option-label.</summary>
        private const string OptionLabelCss = "display: flex; align-items: center; justify-content: space-between; gap: 8px; font: 700 18px/24px var(--font-body);";

        /// <summary>explore.css:160 .ocbg-explore-fc.</summary>
        private const string FcCss = "display: flex; align-items: center; gap: 10px; font: 400 18px/26px var(--font-body); white-space: nowrap;";

        /// <summary>explore.css:162 .ocbg-explore-warn (explore.css:163 its svg: flex: none; margin-top: 1px).</summary>
        private const string WarnCss = "display: flex; align-items: flex-start; gap: 10px; margin: 2px 0 0; padding: 8px 10px; font: 400 17px/24px var(--font-body); background: color-mix(in srgb, var(--caution) 10%, transparent); border-left: 3px solid var(--caution); border-radius: 0 var(--radius-tag) var(--radius-tag) 0;";

        /// <summary>explore.css:164 .ocbg-explore-option-foot.</summary>
        private const string OptionFootCss = "margin-top: auto; padding-top: 6px;";

        /// <summary>explore.css:165 .ocbg-explore-go (over .ocbg-btn.is-primary).</summary>
        private const string GoCss = "width: 100%; display: flex; align-items: center; justify-content: center; gap: 12px; padding: 14px 18px; font-size: 24px; letter-spacing: .12em; white-space: nowrap;";

        // explore.css:168-178: a rest node's two forecasts with warnings — tighter type so the buttons stay in the panel
        private const string DetailTightCss = "gap: 12px; padding-top: 20px; padding-bottom: 20px;";
        private const string DetailLinesTightCss = "gap: 4px; padding-top: 10px;";
        private const string ValueTightCss = "font-size: 18px; line-height: 25px;";
        private const string OptionTightCss = "gap: 4px; padding: 10px 12px 12px;";
        private const string FcTightCss = "font-size: 17px; line-height: 23px;";
        private const string WarnTightCss = "padding: 6px 8px; font-size: 16px; line-height: 22px;";
        private const string GoTightCss = "padding: 10px 14px; font-size: 22px;";
        private const string DetailHeadTighterCss = "grid-template-columns: 56px 1fr auto;";
        private const string TokenTighterCss = "width: 56px; height: 56px;";
        private const string DetailH2TighterCss = "font-size: 28px; line-height: 34px;";
        private const string ValueFcTighterCss = "font-size: 16px; line-height: 21px;";

        /// <summary>explore.css:254 .ocbg-explore-detail-mark (the node kind's icon, 260 px, behind the content).</summary>
        private const string DetailMarkCss = "position: absolute; right: -36px; top: 150px; opacity: .05; pointer-events: none;";

        // ------------------------------------------------------------------ the panel's elements

        /// <summary>The panel's content, clipped to its padding box (overflow: hidden).</summary>
        private RectTransform DetailBody;

        /// <summary>The ways in the panel added to the Tab scope (dropped when it is rebuilt).</summary>
        private readonly List<Focusable> DetailFocus = new List<Focusable>();

        /// <summary>
        /// Styles the detail panel (.ocbg-explore-detail: panel, 1px rule, radius 6, padding 24 28, clipped). The core made
        /// Detail at DETAIL with z 3 and owns its root-class rule (.is-interlude: opacity 0, 400 ms ease-in-out); the
        /// panel's height follows FitCarried. Called once from Mount.
        /// </summary>
        // js: scenes/explore/ExploreScene.js:271
        private void BuildDetail()
        {
            BoxGraphic box = BoxGraphic.On(Detail, DetailCss);
            box.raycastTarget = true; // a div takes the pointer
            // overflow: hidden — the content (the watermark reaches 36 px past the right edge) is clipped to the padding box
            DetailBody = Ui.Div(Detail, "ocbg-explore-detail-content", 0f, 0f, ExploreStyle.DetailW, ExploreStyle.DetailH);
            RectMask2D mask = DetailBody.gameObject.AddComponent<RectMask2D>();
            mask.padding = new Vector4(1f, 1f, 1f, 1f);
        }

        // js: scenes/explore/ExploreScene.js:600
        /// <summary>
        /// drawDetail(): the hovered node (read only) or the pinned node (with its ways in unless busy); redrawn only when
        /// (node, hovering, busy, plate) or the screen changes (DetailKey / DetailScreen). Calls DrawLegend first.
        /// </summary>
        private void DrawDetail()
        {
            if (Screen == null) return;
            bool hovering = Hovered >= 0 && Hovered != Pinned;
            NodeCard node = Screen.Node(hovering ? Hovered : Pinned);
            string key = (node != null ? node.Id : -1) + "|" + hovering + "|" + Busy + "|" + (Plate != null);
            if (Screen == DetailScreen && key == DetailKey) return;
            DrawLegend(hovering ? Screen.Node(Pinned) : node);
            DetailScreen = Screen;
            DetailKey = key;
            ClearDetail();
            if (node == null) return;
            IReadOnlyList<StepOption> options = hovering || Busy ? (IReadOnlyList<StepOption>)Array.Empty<StepOption>() : node.Options;
            bool far = options.Count == 0 && !hovering && Screen.MapIsLive && node.Standing != NodeStanding.Current && node.Lines.Count == 0;
            // A rest node with both forecasts and their warnings can outgrow the panel: set it tighter, step by step, so
            // the buttons always stay inside (never clipped away).
            float clientH = UiNode.Of(Detail).H - 2f;
            for (int level = 0; ; level++)
            {
                float scrollH = LayOutDetail(node, options, far, level);
                if (level >= 2 || scrollH <= clientH + 1f) break;
                ClearDetail();
            }
        }

        /// <summary>detail.textContent = '': the content goes (and its buttons leave the Tab order); the content box follows the panel's height.</summary>
        private void ClearDetail()
        {
            DropPanelFocus(DetailFocus);
            if (!DetailBody) return;
            Ui.Clear(DetailBody);
            Ui.Size(DetailBody, ExploreStyle.DetailW, UiNode.Of(Detail).H);
        }

        /// <summary>
        /// Builds the panel's content at a fit level (0, 1 = is-tight, 2 = is-tight is-tighter) and returns its
        /// scrollHeight: the flow from the padding box's top to its bottom padding, or the watermark's bottom when lower.
        /// </summary>
        private float LayOutDetail(NodeCard node, IReadOnlyList<StepOption> options, bool far, int level)
        {
            bool tight = level >= 1, tighter = level >= 2;
            RectTransform body = DetailBody;
            float panelH = UiNode.Of(Detail).H;
            BoxStyle panel = global::Trial.Geometry.Css.Box(DetailCss + (tight ? DetailTightCss : ""));
            float padT = panel.Padding.Top, padB = panel.Padding.Bottom, padL = panel.Padding.Left;
            float gap = tight ? 12f : 16f;
            float x0 = 1f + padL;
            float w = ExploreStyle.DetailW - 2f - panel.Padding.Horizontal;
            float y = 1f + padT;

            // ---- the watermark: ico(kind, 260, ink, { width: 1.2 }) at right −36, top 150, opacity .05 ----
            PathGraphic mark = ExploreIcons.Ico(body, node.Kind, 260f, ExploreStyle.Ink, new IcoOpts { Width = 1.2f });
            Ui.Place(mark.rectTransform, 1f + (ExploreStyle.DetailW - 2f) + 36f - 260f, 1f + 150f);
            mark.Opacity = 0.05f;
            mark.gameObject.name = "ocbg-explore-detail-mark";

            // ---- the head: token | titles | pill ----
            y += DetailHead(body, node, x0, y, w, tighter);

            // ---- the lines ----
            if (node.Lines.Count > 0)
            {
                y += gap;
                y += DetailLines(body, node, x0, y, w, tight, tighter);
            }

            float end = y;
            if (options.Count > 0)
            {
                // ---- the ways in, pinned to the bottom (margin-top: auto), two columns when there are two ----
                y += gap;
                int n = options.Count;
                bool two = n == 2;
                float colW = two ? (w - 14f) / 2f : w;
                var heights = new float[n];
                for (int i = 0; i < n; i++) heights[i] = DetailOption(null, node, options[i], i, 0f, 0f, colW, null, level);
                float optionsH;
                if (two) optionsH = Math.Max(heights[0], heights[1]);
                else
                {
                    optionsH = 0f;
                    for (int i = 0; i < n; i++) optionsH += heights[i] + (i > 0 ? 14f : 0f);
                }
                float oy = Math.Max(y, panelH - 1f - padB - optionsH);
                if (two)
                {
                    DetailOption(body, node, options[0], 0, x0, oy, colW, optionsH, level);
                    DetailOption(body, node, options[1], 1, x0 + colW + 14f, oy, colW, optionsH, level);
                }
                else
                {
                    float cy = oy;
                    for (int i = 0; i < n; i++)
                    {
                        DetailOption(body, node, options[i], i, x0, cy, w, heights[i], level);
                        cy += heights[i] + 14f;
                    }
                }
                end = y + optionsH;
            }
            else if (far)
            {
                // ---- a far node: only its kind shows ----
                y += gap;
                TextStyle fs = PanelText(DetailFarCss);
                float fh = TextMeasure.Height(ExplorationText.FarNode, fs, w);
                BoxGraphic p = BoxGraphic.Make(body, DetailFarCss, x0, y, w, 1f + 14f + fh, "ocbg-explore-detail-far");
                Label.Make(p.rectTransform, ExplorationText.FarNode, fs, 0f, 1f + 14f, w, fh, "p");
                end = y + 1f + 14f + fh;
            }
            float flow = end - 1f + padB;
            return Math.Max(flow, 150f + DetailMarkSpanH());
        }

        /// <summary>
        /// The watermark's span (position: absolute; display: block) holds the 260 px svg on one line box: the svg's
        /// bottom sits on the baseline of the panel's 16px / 1.375 body text, so the span reaches the strut's descent below it.
        /// </summary>
        private static float DetailMarkSpanH() =>
            260f + (22f - TextMath.CssBaselineFromTop(new FontSpec(Family.Body, 400, 16f, 22f)));

        /// <summary>The head (grid 72 | 1fr | auto, centred; the pill top-aligned). Returns its height.</summary>
        private float DetailHead(RectTransform body, NodeCard node, float x0, float y, float w, bool tighter)
        {
            float tok = tighter ? 56f : 72f;
            TextStyle pill = PanelText(PillCss);
            string word = ExploreText.Standing.TryGetValue(node.Standing, out string sw) ? sw : "";
            float pillTextW = TextMeasure.Width(word, pill);
            float pillW = pillTextW + 12f + 12f + 2f, pillH = pill.LineHeightPx + 3f + 3f + 2f;
            float titlesX = x0 + tok + 16f;
            float titlesW = Math.Max(1f, w - tok - 16f - 16f - pillW);
            TextStyle h2 = PanelText(DetailH2Css + (tighter ? DetailH2TighterCss : ""));
            TextStyle note = PanelText(DetailNoteCss);
            float h2H = TextMeasure.Height(node.KindLabel ?? "", h2, titlesW);
            float noteH = TextMeasure.Height(node.Note ?? "", note, titlesW);
            float titlesH = h2H + 2f + noteH;
            float headH = Math.Max(tok, Math.Max(titlesH, pillH));

            // the token: the node's icon (40) in its colour, the standing's border, the boss's outline
            NodeStanding s = node.Standing;
            string tokenCss = TokenCss
                              + (s == NodeStanding.Current || s == NodeStanding.Reachable ? TokenLitCss : "")
                              + (s == NodeStanding.Far ? TokenFarCss : "")
                              + (node.Kind == DungeonCore.NodeKind.Boss ? TokenBossCss : "")
                              + (tighter ? TokenTighterCss : "");
            BoxGraphic token = BoxGraphic.Make(body, tokenCss, x0, y + (headH - tok) / 2f, tok, tok,
                "ocbg-explore-detail-token kind-" + ExploreStyle.KindClass(node.Kind) + " " + ExploreStyle.StandingClass(s));
            PathGraphic icon = ExploreIcons.Ico(token.rectTransform, node.Kind, 40f, NodeIconColor(node));
            Ui.Place(icon.rectTransform, (tok - 40f) / 2f, (tok - 40f) / 2f);

            // the titles: h2 KindLabel, p Note
            float ty = y + (headH - titlesH) / 2f;
            Label.Make(body, node.KindLabel ?? "", h2, titlesX, ty, titlesW, h2H, "h2");
            Label.Make(body, node.Note ?? "", note, titlesX, ty + h2H + 2f, titlesW, noteH, "p");

            // the pill: the standing's word
            string pillCss = PillCss
                             + (s == NodeStanding.Current ? PillCurrentCss : "")
                             + (s == NodeStanding.Reachable ? PillReachableCss : "")
                             + (s == NodeStanding.Revisit ? PillRevisitCss : "")
                             + (s == NodeStanding.Far ? PillFarCss : "");
            BoxGraphic p = BoxGraphic.Make(body, pillCss, x0 + w - pillW, y, pillW, pillH, "ocbg-explore-pill " + ExploreStyle.StandingClass(s));
            Label.Make(p.rectTransform, word, pill, 1f + 12f, 1f + 3f, pillTextW, pill.LineHeightPx, "label");
            return headH;
        }

        /// <summary>The lines (column, top rule): "key: value" in two columns on a shared baseline, the rest full width. Returns the block's height.</summary>
        private float DetailLines(RectTransform body, NodeCard node, float x0, float y, float w, bool tight, bool tighter)
        {
            BoxGraphic block = BoxGraphic.Make(body, DetailLinesCss + (tight ? DetailLinesTightCss : ""), x0, y, w, 0f, "ocbg-explore-detail-lines");
            RectTransform rt = block.rectTransform;
            float rowGap = tight ? 4f : 8f;
            float ly = 1f + (tight ? 10f : 14f);
            TextStyle ks = PanelText(KeyCss);
            TextStyle vs = PanelText(ValueCss + (tight ? ValueTightCss : "") + (tighter ? ValueFcTighterCss : ""), true);
            float kb = TextMath.CssBaselineFromTop(ks.Spec), vb = TextMath.CssBaselineFromTop(vs.Spec);
            for (int i = 0; i < node.Lines.Count; i++)
            {
                (string k, string v) = SplitRow(node.Lines[i]);
                string vm = Rich(v, vs);
                // a value with numbers sets them in the mono face: its lines are taller than 28 (29 at 20px/28px)
                bool digits = HasDigits(v);
                float rowH;
                if (k != null)
                {
                    // grid 92px 1fr, column-gap 12, align-items: baseline
                    float vw = w - 92f - 12f;
                    float kh = TextMeasure.Height(k, ks, 92f);
                    float vh = PanelRichBlock(vm, vs, vw, digits, out TextStyle vds, out float vShift);
                    float shift = Math.Max(kb, vb);
                    float ky = ly + shift - kb, vy = ly + shift - vb;
                    Label.Make(rt, k, ks, 0f, ky, 92f, kh, "ocbg-explore-k");
                    Label.Make(rt, vm, vds, 92f + 12f, vy + vShift, vw, vh, "ocbg-explore-v");
                    rowH = Math.Max(ky + kh, vy + vh) - ly;
                }
                else
                {
                    // is-wide: the value alone, full width
                    float vh = PanelRichBlock(vm, vs, w, digits, out TextStyle vds, out float vShift);
                    Label.Make(rt, vm, vds, 0f, ly + vShift, w, vh, "ocbg-explore-v");
                    rowH = vh;
                }
                ly += rowH;
                if (i < node.Lines.Count - 1) ly += rowGap;
            }
            Ui.Size(rt, w, ly);
            return ly;
        }

        /// <summary>
        /// One way in (ExploreScene.js:630-646): with a forecast, its box (【label】, the forecast lines with their icons,
        /// the warnings) over the foot's button; without one (「移る」), just the button. parent null only measures:
        /// returns the column's natural height. With parent, builds it at (x, y) colW × rowH (the grid's row: the foot
        /// sits at the bottom, margin-top: auto).
        /// </summary>
        private float DetailOption(RectTransform parent, NodeCard node, StepOption opt, int index, float x, float y, float colW, float? rowH, int level)
        {
            bool tight = level >= 1, tighter = level >= 2;
            bool bare = opt.Forecast == null;
            string css = OptionCss + (opt.NeedsConfirm ? OptionLethalCss : "") + (bare ? OptionBareCss : "") + (tight ? OptionTightCss : "");
            BoxStyle box = global::Trial.Geometry.Css.Box(css);
            Insets bw = box.BorderWidths, pad = box.Padding;
            float cx = bw.Left + pad.Left, cw = colW - bw.Horizontal - pad.Horizontal;
            float gap = tight ? 4f : 6f;
            bool build = parent != null;
            RectTransform col = null;
            if (build)
            {
                col = BoxGraphic.Make(parent, css, x, y, colW, rowH ?? 0f,
                    "ocbg-explore-option" + (opt.NeedsConfirm ? " is-lethal" : "") + (bare ? " is-bare" : "")).rectTransform;
            }

            float cy = bw.Top + pad.Top;
            int items = 0;
            if (!bare)
            {
                // 【label】
                TextStyle ls = PanelText(OptionLabelCss);
                string label = "【" + opt.Label + "】";
                float lh = TextMeasure.Height(label, ls, cw);
                if (build) Label.Make(col, label, ls, cx, cy, cw, lh, "ocbg-explore-option-label");
                cy += lh;
                items++;

                // the forecast lines (flex, align-items: center): an icon by the line's first word (or the 20 px blank
                // fc-dot, as wide but empty), then the line, whose numbers make its line box 28 at 18px/26px
                TextStyle fs = PanelText(FcCss + (tight ? FcTightCss : "") + (tighter ? ValueFcTighterCss : ""), true);
                foreach (string line in opt.Forecast.Lines)
                {
                    IconSpec icon = ForecastIcon(line);
                    float fl = PanelLineOf(fs, HasDigits(line)).H;
                    float fh = Math.Max(fl, icon != null ? 20f : 0f);
                    cy += gap;
                    if (build)
                    {
                        if (icon != null)
                            PanelIcon(col, icon.Name, 20f, icon.Color, cx, cy + (fh - 20f) / 2f, icon.Outline ? IcoOpts.OutlineOnly : null);
                        string m = Rich(line, fs);
                        Label.Make(col, m, fs, cx + 20f + 10f, cy + (fh - fl) / 2f, TextMeasure.Width(m, fs), fl, "ocbg-explore-fc");
                    }
                    cy += fh;
                    items++;
                }

                // the warnings: caution 22 + the text, on a caution wash with a 3 px left border
                string warnCss = WarnCss + (tight ? WarnTightCss : "");
                BoxStyle wb = global::Trial.Geometry.Css.Box(warnCss);
                TextStyle ws = PanelText(warnCss);
                foreach (string warning in opt.Forecast.Warnings)
                {
                    float innerW = cw - wb.BorderWidths.Horizontal - wb.Padding.Horizontal;
                    float textW = Math.Max(1f, innerW - 22f - 10f);
                    float th = TextMeasure.Height(warning, ws, textW);
                    float wh = wb.BorderWidths.Vertical + wb.Padding.Vertical + Math.Max(1f + 22f, th);
                    cy += gap + 2f; // margin: 2px 0 0
                    if (build)
                    {
                        BoxGraphic warn = BoxGraphic.Make(col, warnCss, cx, cy, cw, wh, "ocbg-explore-warn");
                        float ix = wb.BorderWidths.Left + wb.Padding.Left, iy = wb.BorderWidths.Top + wb.Padding.Top;
                        PanelIcon(warn.rectTransform, "caution", 22f, ExploreStyle.Caution, ix, iy + 1f);
                        Label.Make(warn.rectTransform, warning, ws, ix + 22f + 10f, iy, textW, th, "span");
                    }
                    cy += wh;
                    items++;
                }
            }

            // the foot: the button (ocbg-btn is-primary ocbg-explore-go, is-danger when lethal), full width
            float goH = DetailGoHeight(tight);
            float footH = 6f + goH;
            if (items > 0) cy += gap;
            float natural = cy + footH + pad.Bottom + bw.Bottom;
            if (!build) return natural;

            float footY = (rowH ?? natural) - bw.Bottom - pad.Bottom - footH;
            RectTransform foot = Ui.Div(col, "ocbg-explore-option-foot", cx, footY, cw, footH);
            OcbgButton go = OcbgButton.Make(foot, opt.Label, true, opt.NeedsConfirm, "ocbg-btn is-primary ocbg-explore-go");
            go.States.State(".ocbg-explore-go", GoCss, true);
            if (tight) go.States.State(".is-tight .ocbg-explore-go", GoTightCss, true);
            go.States.Apply(instant: true);
            go.SetBox(0f, 6f, cw, goH);
            int at = index;
            go.Click += () => TakeOption(node, at);
            Focusable f = AddFocus(go);
            if (f != null) DetailFocus.Add(f);
            return natural;
        }

        /// <summary>
        /// The go button's height: its line (.ocbg-btn's `font: 700 22px/1` keeps the line height a number, so it is the
        /// font-size) + padding + the primary's 2 px border.
        /// </summary>
        private float DetailGoHeight(bool tight)
        {
            string css = CommonStyles.BtnCss + CommonStyles.BtnPrimaryCss + GoCss + (tight ? GoTightCss : "");
            BoxStyle b = global::Trial.Geometry.Css.Box(css);
            TextStyle t = PanelText(CommonStyles.OcbRootCss + css);
            return t.LineHeightPx + b.Padding.Vertical + b.BorderWidths.Vertical;
        }
    }
}
