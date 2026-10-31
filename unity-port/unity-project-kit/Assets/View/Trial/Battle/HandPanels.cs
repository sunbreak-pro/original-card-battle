// The panels around the battle's hand (CardPanels, RefusalLineView, the guide line; docs/BATTLE_SCENE.md §6a): the guide
// line (layer hud), the refusal line (layer float), the hover line, the reach panel, the term tip and the card detail
// (layer over), with their placements (clear of the number panels, the omen badges, 俊敏's marks and the floor row). Port of
// scenes/battle/hand-panels.js.
//
// A port of CardPanels.cs (the hover line above a hovered card §7.2, the reason it cannot be played from here on its right
// §5.1, the right-click detail above it §7.4, the meaning of an underlined term §7.1), RefusalLineView.cs (§5.4: why a
// released card went back, 3.5 s) and the scene's guide line (DepictionPlayer.handGuide). Every word comes from the card's
// face or the source; these only place and print them. Looks: Ocb's hover line, .ocb-reachpanel, .ocb-detail, .ocb-tip,
// Ocb's refusal line. Every piece is pointer-transparent (hand.css: pointer-events none). Times are ms (the clocks' dt).
using System;
using System.Collections.Generic;
using System.Linq;
using Depiction;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    /// <summary>createPanels(env) → the panels' api (hand-panels.js:209).</summary>
    // js: scenes/battle/hand-panels.js:150
    public sealed class HandPanels
    {
        // ---- CardPanels' measures ----------------------------------------------------------------------------

        /// <summary>§7.2: a 420 px tag above the card.</summary>
        private const float HoverW = 420f; // js: scenes/battle/hand-panels.js:11
        private const float HoverGap = 8f; // js: scenes/battle/hand-panels.js:12
        /// <summary>The text's width inside the DS hover line: 420 − padding 14 × 2 − the lamp's 16 − the gap 10.</summary>
        private const float HoverTextW = HoverW - 28f - 16f - 10f; // js: scenes/battle/hand-panels.js:14

        /// <summary>§5.1 REACH: { w: 280, h: 76, gap: 12, rightLimit: 1620 }.</summary>
        private static class ReachBox // js: scenes/battle/hand-panels.js:15
        {
            public const float W = 280f;
            public const float H = 76f;
            public const float Gap = 12f;
            public const float RightLimit = 1620f;
        }

        /// <summary>§7.4 DETAIL: { w: 560, h: 280 }.</summary>
        private static class DetailBox // js: scenes/battle/hand-panels.js:16
        {
            public const float W = 560f;
            public const float H = 280f;
        }

        /// <summary>
        /// Review 17 (§3.13): where the detail may stand over the field — the floor's row (FloorView: its top at y 560, its tiles
        /// 40 tall) stays clear by DetailGap, as do the omen badges; its top no higher than under the corner buttons (y 20–76).
        /// </summary>
        private static class DetailFloor // js: scenes/battle/hand-panels.js:21
        {
            public const float Top = 560f;
            public const float H = 40f;
        }

        private const float DetailGap = 4f; // js: scenes/battle/hand-panels.js:22
        private const float DetailMinTop = 84f; // js: scenes/battle/hand-panels.js:23
        /// <summary>§7.1.</summary>
        private const float TermW = 340f; // js: scenes/battle/hand-panels.js:24
        /// <summary>ClampX: inside the screen with 16 px to spare.</summary>
        private const float Edge = 16f; // js: scenes/battle/hand-panels.js:25

        /// <summary>
        /// How fast the guide line fades out and back. Review fix (scene choice): it steps aside while a hovered card's panels
        /// (the hover line, the reach panel) or the detail stand in its band (y ≈ 757, where the lifted card's top, the hover
        /// line and the detail land), as it already does for the refusal line — torn pieces of it showed between them. It
        /// stays up under a held or selected card (Unity's RefreshPlayableLook and the design system's guideIdle).
        /// </summary>
        private const float GuideFadeMs = 120f; // js: scenes/battle/hand-panels.js:32

        /// <summary>Review 19: the guide line keeps this far inside an open drawer's edge.</summary>
        private const float GuideClear = 16f; // js: scenes/battle/hand-panels.js:34

        /// <summary>
        /// Review fix (a two-line hover line over a card under a number panel covered the panel's bottom edge): the line keeps
        /// PanelGap off every number panel (env.Geom.StatusBoxes) where it can (ClearOfPanels).
        /// </summary>
        private const float PanelGap = 4f; // js: scenes/battle/hand-panels.js:57
        private const float ChipWeight = 4f; // js: scenes/battle/hand-panels.js:58

        /// <summary>
        /// Review fix (§7.4 / §7 ③ 全文と用語の定義: the glossary ended on 「詰める / 離れる：自分が動いて、相手との」, cut where the
        /// box's whole lines ran out): a definition is shown whole or not at all, and where the box cannot hold the rest, the
        /// last line that fits ends in 「…」 (scene text), so a cut never reads as the end of a sentence. The body text likewise.
        /// </summary>
        private const string Ellipsis = "…"; // js: scenes/battle/hand-panels.js:94 (scene text)

        // ---- hand.css, verbatim ----------------------------------------------------------------------------------

        /// <summary>hand.css:34 <c>.ocbg-battle-hoverline</c></summary>
        private const string HoverLineCss = "position: absolute; left: 0; top: 0; pointer-events: none;";

        /// <summary>hand.css:35 <c>.ocbg-battle-hoverline span</c></summary>
        private const string HoverLineSpanCss = "white-space: pre-line;";

        /// <summary>hand.css:36 <c>.ocbg-battle-reachpanel</c></summary>
        private const string ReachPanelCss = "position: absolute; left: 0; top: 0; pointer-events: none;";

        /// <summary>hand.css:37 <c>.ocbg-battle-termtip</c></summary>
        private const string TermTipCss = "position: absolute; left: 0; top: 0; pointer-events: none;";

        /// <summary>hand.css:49 <c>.ocbg-battle-guide</c> (handGuide: centre x 960, y ≈ 757, body 22 px in ink, no panel)</summary>
        private const string GuideCss = "position: absolute; left: 0; width: 1920px; display: flex; align-items: center; justify-content: center; font: 400 22px/28px var(--font-body); color: var(--ink); text-shadow: 0 1px 3px var(--ground), 0 0 8px var(--ground); white-space: nowrap; pointer-events: none;";

        /// <summary>hand.css:52 <c>.ocbg-battle-refusal</c> (RefusalLineView: RefusalLineLayout's box, centred on x 960)</summary>
        private const string RefusalCss = "position: absolute; transform: translateX(-50%); pointer-events: none;";

        // ---- the parts' state ------------------------------------------------------------------------------------

        /// <summary>hover: { el, text, lit, plain, height }.</summary>
        private sealed class HoverPart
        {
            public RectTransform El;
            public string Text;
            public bool Lit;
            public bool Plain;
            public float Height;
        }

        /// <summary>reach: { el, text }.</summary>
        private sealed class ReachPart
        {
            public RectTransform El;
            public string Text;
        }

        /// <summary>detail: { el, id, h }.</summary>
        private sealed class DetailPart
        {
            public RectTransform El;
            public string Id;
            public float H;
        }

        /// <summary>term: { el, mark, height }.</summary>
        private sealed class TermPart
        {
            public RectTransform El;
            public TermMark Mark;
            public float Height;
        }

        private readonly BattleViewEnv _env;

        // the guide line (handGuide): centre x 960, y ≈ 757, body 22 px, no panel
        private RectTransform _guide;
        private TextMeshProUGUI _guideSpan;
        private string _guideShown = "";
        private string _guideText = "";
        private float _guideAlpha = 1f;
        private bool _guideAway;
        private float _guideW = -1f;

        // the refusal line (RefusalLineView): real ms it has left to stand (it counts only while it shows), its alpha, its
        // width, what to call at its end
        private RectTransform _refusalEl;
        private float _refusalLeft;
        private float _refusalAlpha = 1f;
        private float _refusalW;
        private Action _refusalOnEnd;

        // hover line / reach hint / detail / term (CardPanels)
        private HoverPart _hover;
        private ReachPart _reach;
        private DetailPart _detail;
        private TermPart _term;

        private HandPanels(BattleViewEnv env)
        {
            _env = env;
            // ---- the guide line (handGuide): centre x 960, y ≈ 757, body 22 px, no panel ----------------------
            _guide = Ui.Div(env != null ? env.Layer.Hud : null, "ocbg-battle-guide");
            UiNode.Of(_guide).Place(0f, L.Guide.Y, L.W, L.Guide.H);
            _guideSpan = Label.Make(_guide, "", Ocb.TextOf(GuideCss), "span");
            // text-shadow 0 0 8px var(--ground): past the underlay's reach, drawn behind the span
            TextGlow.For(_guideSpan);
        }

        /// <summary>createPanels(env): the guide line in layer.hud at L.Guide; the rest made as they are shown.</summary>
        // js: scenes/battle/hand-panels.js:150
        public static HandPanels Create(BattleViewEnv env) => new HandPanels(env);

        // ---- helpers ---------------------------------------------------------------------------------------------

        /// <summary>RefusalLineLayout.Top is a C# property; the port may keep it as a getter or a method.</summary>
        // js: scenes/battle/hand-panels.js:37
        private static float RefusalTop() => RefusalLineLayout.Top;

        /// <summary>CardPanels.BreakAfter: the reach hint breaks after 「相手との間合いが」.</summary>
        // js: scenes/battle/hand-panels.js:43
        private static string[] BreakAfter(string text, string mark)
        {
            int at = text.IndexOf(mark, StringComparison.Ordinal);
            return at < 0 ? new[] { text } : new[] { text.Substring(0, at + 1), text.Substring(at + mark.Length) };
        }

        /// <summary>
        /// Review fix (a two-line hover line over a card under a number panel covered the panel's bottom edge): the line
        /// keeps PanelGap off every number panel (env.geom.statusBoxes) where it can. Review 17: it always stands where
        /// Unity's CardPanels.ShowHover puts it, HoverGap over the raised card — moved down onto the card it covered the card's
        /// top edge and the reach panel's corner (battle-visual-v1 §5.1 「パネルとは重なりません」), and set beside the card it
        /// left the card altogether. At that height it slides sideways off the panels in its way, keeping the card's middle
        /// under it, inside the screen; when no slide clears them, it stands, still over the card's middle, where it covers the
        /// least of them.
        /// </summary>
        // js: scenes/battle/hand-panels.js:59 (makeClearOfPanels → clearOfPanels)
        private (float Left, float Top, bool Ok) ClearOfPanels(float left, float top, float height, StageBox card)
        {
            List<PanelBox> panels = StatusPanels();
            bool Across(float l, PanelBox p) => l < p.X + p.W + PanelGap && l + HoverW > p.X - PanelGap;
            bool Under(PanelBox p) => top < p.Y + p.H + PanelGap && top + height > p.Y - PanelGap;
            List<PanelBox> inRow = panels.Where(Under).ToList();
            List<PanelBox> Hits(float l) => inRow.Where(p => Across(l, p)).ToList();
            List<PanelBox> hit = Hits(left);
            if (hit.Count == 0) return (left, top, true);
            // Sideways: the smaller slide that clears the panels in the way, if the line still covers the card's middle.
            float lo = Math.Max(Edge, card.X + card.W / 2f - HoverW);
            float hi = Math.Min(L.W - Edge - HoverW, card.X + card.W / 2f);
            float toLeft = hit.Min(p => p.X - PanelGap - HoverW);
            float toRight = hit.Max(p => p.X + p.W + PanelGap);
            List<float> options = new[] { toLeft, toRight }
                .Where(l => l >= lo && l <= hi && Hits(l).Count == 0)
                .OrderBy(l => Math.Abs(l - left))
                .ToList();
            if (options.Count > 0) return (options[0], top, true);
            // None clears: where, over the card's middle, it covers the least of the panels in its row — their status chips (the
            // panel's last row, left-aligned: StatusBarView's chipsW) counting ChipWeight times as much as the rest.
            float Span(float l, float a, float b) => Math.Max(0f, Math.Min(l + HoverW, b) - Math.Max(l, a));
            float Cover(float l) => inRow.Sum(p => Span(l, p.X - PanelGap, p.X + p.W + PanelGap)
                + (p.ChipsW != 0f ? ChipWeight * Span(l, p.X - PanelGap, p.X + p.ChipsW + PanelGap) : 0f));
            IEnumerable<float> tries = new[] { lo, hi }
                .Concat(inRow.Where(p => p.ChipsW != 0f).Select(p => p.X + p.ChipsW + PanelGap))
                .Where(l => l >= lo && l <= hi);
            float best = Math.Max(lo, Math.Min(hi, left));
            foreach (float l in tries)
                if (Cover(l) < Cover(best) - 0.5f) best = l;
            return (best, top, false);
        }

        /// <summary>The longest head of a text that, with the ellipsis, keeps `fits()`; '' when none does.</summary>
        // js: scenes/battle/hand-panels.js:96
        private static bool CutToFit(Action<string> set, string text, Func<bool> fits)
        {
            text = text ?? "";
            int lo = 0;
            int hi = text.Length;
            set(text);
            if (fits()) return true;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2; // Math.ceil((lo + hi) / 2)
                set(text.Substring(0, mid).TrimEnd() + Ellipsis);
                if (fits()) lo = mid;
                else hi = mid - 1;
            }
            set(lo > 0 ? text.Substring(0, lo).TrimEnd() + Ellipsis : "");
            return lo > 0;
        }

        /// <summary>
        /// fitDetail(el): the body text cut in 「…」 where its box (max 264, shrunk when the detail is at its 600) cannot hold
        /// it; then the terms box's rows: the first that does not fit keeps the lines it has room for, ending in 「…」; with no
        /// room for a line of it, the definition before it takes the 「…」; the rows after it go. (tailText: the text node a
        /// row ends with, the definition after 「：」, is the detail's TermTail.)
        /// </summary>
        // js: scenes/battle/hand-panels.js:117
        private static void FitDetail(HandDetailState el)
        {
            if (!el) return;
            if (el.FullOverflows())
                CutToFit(t => el.SetFullText(t), el.FullText, () => !el.FullOverflows());
            el.Layout();
            if (!el.HasTerms) return;
            float limit = el.TermsClientHeight;
            int n = el.TermRowCount;
            bool FitsRow(int r) => el.TermRowTop(r) + el.TermRowHeight(r) <= limit + 0.5f;
            for (int i = 0; i < n; i++)
            {
                if (FitsRow(i)) continue;
                // This definition does not fit whole: it keeps the lines it has room for, ending in 「…」; with no room for a
                // line of it, the definition before it takes the 「…」.
                int row = i;
                bool kept = el.TermRowTop(row) < limit && CutToFit(t => el.SetTermTail(row, t), el.TermTail(row), () => FitsRow(row));
                if (!kept)
                {
                    el.RemoveTermRow(row);
                    if (row > 0)
                    {
                        int prev = row - 1;
                        CutToFit(t => el.SetTermTail(prev, t), el.TermTail(prev) + Ellipsis, () => FitsRow(prev));
                    }
                }
                for (int j = row + 1; j < n; j++) el.RemoveTermRow(j);
                break;
            }
            el.Layout();
        }

        // js: scenes/battle/hand-panels.js:145
        private static float ClampX(float cx, float width)
        {
            float half = width / 2f;
            return Math.Max(Edge + half, Math.Min(L.W - Edge - half, cx));
        }

        /// <summary>A length as `.toFixed(1)` writes it into the style (one decimal, halves away from zero).</summary>
        private static float Fixed1(float v) => (float)Math.Round(v, 1, MidpointRounding.AwayFromZero);

        /// <summary>The number panels' boxes (env.geom.statusBoxes().map(p => p.box)), or none.</summary>
        private List<PanelBox> StatusPanels()
        {
            IReadOnlyList<StatusPanelBox> boxes = _env?.Geom.StatusBoxes?.Invoke();
            return boxes == null ? new List<PanelBox>() : boxes.Select(p => p.Box).ToList();
        }

        /// <summary>env.geom.omenBoxes(), or none.</summary>
        private List<StageBox> OmenBoxes()
        {
            IReadOnlyList<StageBox> boxes = _env?.Geom.OmenBoxes?.Invoke();
            return boxes == null ? new List<StageBox>() : boxes.ToList();
        }

        /// <summary>env.geom.freeStepBoxes(), or none.</summary>
        private List<StageBox> FreeStepBoxes()
        {
            IReadOnlyList<FreeStepBox> boxes = _env?.Geom.FreeStepBoxes?.Invoke();
            return boxes == null ? new List<StageBox>() : boxes.Select(b => b.Box).ToList();
        }

        private RectTransform Layer(BattleLayer layer) => _env != null ? _env.Layer[layer] : null;

        // ---- the guide line ----------------------------------------------------------------------------------------

        // js: scenes/battle/hand-panels.js:180
        private void ShowGuideText()
        {
            string text = _refusalEl ? "" : _guideText;
            if (_guideShown == text || !_guideSpan) return;
            _guideShown = text;
            Label.SetText(_guideSpan, text);
            _guideW = -1f;
            // display: flex; align-items: center; justify-content: center — the span in the middle of the 1920 × 28 line
            RectTransform rt = _guideSpan.rectTransform;
            Ui.Place(rt, (L.W - rt.rect.width) / 2f, (L.Guide.H - rt.rect.height) / 2f);
        }

        /// <summary>
        /// Review 19 (with the pile list open while 俊敏's choice waited, the guide's first character stood under the drawer):
        /// the line keeps GuideClear inside the part of the screen an open drawer leaves — the pile list's (left) or the
        /// journal's (right) — moving off its centre only as far as that needs.
        /// </summary>
        // js: scenes/battle/hand-panels.js:189
        private void PlaceGuide()
        {
            if (!_guide) return;
            UiNode node = UiNode.Of(_guide);
            float left = _env?.Geom.DrawerRight != null ? _env.Geom.DrawerRight() : 0f;
            float? journal = _env?.Geom.JournalEdge != null ? _env.Geom.JournalEdge() : null;
            if (!(left > 0f) && journal == null)
            {
                if (node.PropTranslateX != 0f) node.PropTranslateX = 0f;
                return;
            }
            // guideSpan.offsetWidth: the layout width rounded to a whole px
            if (_guideW < 0f) _guideW = _guideSpan ? (float)Math.Round(_guideSpan.rectTransform.rect.width, MidpointRounding.AwayFromZero) : 0f;
            float lo = left > 0f ? left + GuideClear : 0f;
            float hi = journal.HasValue ? journal.Value - GuideClear : L.W;
            float x0 = L.W / 2f - _guideW / 2f;
            float x1 = L.W / 2f + _guideW / 2f;
            float dx = x0 < lo ? lo - x0 : x1 > hi ? hi - x1 : 0f;
            float t = Math.Abs(dx) > 0.5f ? Fixed1(dx) : 0f;
            if (node.PropTranslateX != t) node.PropTranslateX = t;
        }

        // ---- the hover panels ------------------------------------------------------------------------------------

        /// <summary>hideHover(): the hover line, the reach panel and the term tip go.</summary>
        // js: scenes/battle/hand-panels.js:203
        public void HideHover()
        {
            if (_hover != null) { Ui.Remove(_hover.El); _hover = null; }
            if (_reach != null) { Ui.Remove(_reach.El); _reach = null; }
            HideTerm();
        }

        // ---- guide ----

        /// <summary>setGuide(text): the guide's text ('' quiet); blank while a refusal line shows.</summary>
        // js: scenes/battle/hand-panels.js:211
        public void SetGuide(string text)
        {
            _guideText = text ?? "";
            ShowGuideText();
        }

        /// <summary>stepGuide(dtScene, away) — per frame: the guide fades out (120 ms of dt) while `away`, and back after; it keeps 16 px inside an open drawer.</summary>
        // js: scenes/battle/hand-panels.js:213
        public void StepGuide(float dtScene, bool away)
        {
            _guideAway = away;
            float target = _guideAway ? 0f : 1f;
            float step = GuideFadeMs > 0f ? dtScene / GuideFadeMs : 1f;
            _guideAlpha = target > _guideAlpha ? Math.Min(target, _guideAlpha + step) : Math.Max(target, _guideAlpha - step);
            if (_guide) UiNode.Of(_guide).Opacity = (float)Math.Round(_guideAlpha, 3, MidpointRounding.AwayFromZero);
            PlaceGuide();
        }

        /// <summary>panelsUp: a hovered card's panels or the detail are up (the guide steps aside for them).</summary>
        // js: scenes/battle/hand-panels.js:223
        public bool PanelsUp => _hover != null || _reach != null || _detail != null;

        // ---- refusal (§5.4) ----

        /// <summary>refusalShown.</summary>
        // js: scenes/battle/hand-panels.js:226
        public bool RefusalShown => _refusalEl;

        /// <summary>refusalBox(): the refusal line's box on the stage, or null when none is up.</summary>
        // js: scenes/battle/hand-panels.js:228
        public StageBox? RefusalBox()
        {
            if (!_refusalEl) return null;
            // refusalEl.offsetWidth: the layout width rounded to a whole px
            if (!(_refusalW > 0f)) _refusalW = (float)Math.Round(UiNode.Of(_refusalEl).W, MidpointRounding.AwayFromZero);
            if (!(_refusalW > 0f)) _refusalW = 900f;
            return new StageBox(RefusalLineLayout.CenterX - _refusalW / 2f, RefusalTop(), _refusalW, RefusalLineLayout.Height);
        }

        /// <summary>
        /// stepRefusal(dtReal, away) — per frame, real ms: while a raised card stands over the line (`away`) it fades out (120
        /// ms) and its RefusalLineLayout.Seconds stop counting; otherwise the seconds run out and the line goes (then onEnd).
        /// </summary>
        // js: scenes/battle/hand-panels.js:238
        public void StepRefusal(float dtReal, bool away)
        {
            if (!_refusalEl) return;
            float step = GuideFadeMs > 0f ? dtReal / GuideFadeMs : 1f;
            _refusalAlpha = away ? Math.Max(0f, _refusalAlpha - step) : Math.Min(1f, _refusalAlpha + step);
            UiNode.Of(_refusalEl).Opacity = _refusalAlpha >= 1f ? 1f : (float)Math.Round(_refusalAlpha, 3, MidpointRounding.AwayFromZero);
            if (away) return;
            _refusalLeft -= dtReal;
            if (_refusalLeft > 0f) return;
            Action end = _refusalOnEnd;
            HideRefusal();
            end?.Invoke();
        }

        /// <summary>showRefusal(text, onEnd): the reason on one line above the hand for RefusalLineLayout.Seconds (real time); the guide steps aside.</summary>
        // js: scenes/battle/hand-panels.js:251
        public void ShowRefusal(string text, Action onEnd = null)
        {
            HideRefusal();
            if (string.IsNullOrEmpty(text)) return;
            _refusalLeft = RefusalLineLayout.Seconds * 1000f;
            _refusalAlpha = 1f;
            _refusalW = 0f;
            _refusalOnEnd = onEnd;
            _refusalEl = BuildRefusalLine(Layer(BattleLayer.Float), text);
            ShowGuideText();
        }

        /// <summary>
        /// OCB.RefusalLine(text) with the scene's .ocbg-battle-refusal and RefusalLineLayout's inline sizes: top, left 960
        /// (translateX(−50%)), height 48, padding 0 16, gap 10, 20 px text, the dot 16.
        /// </summary>
        // js: scenes/battle/hand-panels.js:258
        private static RectTransform BuildRefusalLine(RectTransform parent, string text)
        {
            string css = Ocb.RefusalCss + RefusalCss
                + "top: " + Ocb.Num(RefusalTop()) + "px; left: " + Ocb.Num(RefusalLineLayout.CenterX) + "px; height: " + Ocb.Num(RefusalLineLayout.Height)
                + "px; padding: 0 " + Ocb.Num(RefusalLineLayout.Pad) + "px; gap: " + Ocb.Num(RefusalLineLayout.DotGap) + "px; font-size: "
                + Ocb.Num(RefusalLineLayout.TextSize) + "px;";
            RectTransform el = Ocb.El(parent, "ocb-refusal ocbg-battle-refusal", css);
            string dotCss = Ocb.DotCss + "width: " + Ocb.Num(RefusalLineLayout.DotSize) + "px; height: " + Ocb.Num(RefusalLineLayout.DotSize) + "px;";
            RectTransform dot = Ocb.El(el, "ocb-dot", dotCss);
            Ocb.NoShrink(dot);
            Ocb.Text(el, text, Ocb.TextOf(css), "text");
            Ocb.Flow(el, css);
            UiNode node = UiNode.Of(el);
            node.SetStyleTransform(pctX: -0.5f); // transform: translateX(-50%)
            node.Place(RefusalLineLayout.CenterX, RefusalTop());
            return el;
        }

        /// <summary>hideRefusal().</summary>
        // js: scenes/battle/hand-panels.js:271
        public void HideRefusal()
        {
            _refusalOnEnd = null;
            if (_refusalEl) Ui.Remove(_refusalEl);
            _refusalEl = null;
            ShowGuideText();
        }

        // ---- hover (§7.2 / §5.1) ----

        /// <summary>
        /// showHover(face, box, lit) = ShowHover: the hover line centred above `box` (the card's upright bounds now), kept off
        /// the number panels, and — when the script says nobody is in reach (CardFace.ReachHint) — the reach panel on its right
        /// (on its left past x 1620). A null face hides them.
        /// </summary>
        // js: scenes/battle/hand-panels.js:282
        public void ShowHover(CardFace face, StageBox box, bool lit)
        {
            if (face == null) { HideHover(); return; }
            bool reachRight = box.X + box.W + ReachBox.Gap + ReachBox.W <= ReachBox.RightLimit;
            if (!string.IsNullOrEmpty(face.HoverLine))
            {
                if (_hover == null || _hover.Text != face.HoverLine || _hover.Lit != lit || _hover.Plain != face.Plain)
                {
                    if (_hover != null) Ui.Remove(_hover.El);
                    RectTransform el = BuildHoverLine(Layer(BattleLayer.Over), HandCard.WrapLine(face.HoverLine, HoverTextW, 18f), lit, face.Plain);
                    float h = UiNode.Of(el).H;
                    _hover = new HoverPart { El = el, Text = face.HoverLine, Lit = lit, Plain = face.Plain, Height = h > 0f ? h : 48f };
                }
                var at = ClearOfPanels(ClampX(box.X + box.W / 2f, HoverW) - HoverW / 2f, box.Y - HoverGap - _hover.Height, _hover.Height, box);
                if (_hover.El) UiNode.Of(_hover.El).Place(Fixed1(at.Left), Fixed1(at.Top));
            }
            else if (_hover != null)
            {
                Ui.Remove(_hover.El);
                _hover = null;
            }

            if (!string.IsNullOrEmpty(face.ReachHint))
            {
                if (_reach == null || _reach.Text != face.ReachHint)
                {
                    if (_reach != null) Ui.Remove(_reach.El);
                    string[] parts = BreakAfter(face.ReachHint, "が ");
                    _reach = new ReachPart { El = BuildReachPanel(Layer(BattleLayer.Over), parts), Text = face.ReachHint };
                }
                if (_reach.El)
                    UiNode.Of(_reach.El).Place(Fixed1(reachRight ? box.X + box.W + ReachBox.Gap : box.X - ReachBox.Gap - ReachBox.W), Fixed1(box.Y));
            }
            else if (_reach != null)
            {
                Ui.Remove(_reach.El);
                _reach = null;
            }
        }

        /// <summary>
        /// OCB.HoverLine(text, lit) + .ocbg-battle-hoverline: .ocb-hoverline 420 wide, at least 48 tall (padding 10 14, flex
        /// centred, gap 10, panel-solid, 1px steel, radius 6, 400 18/26 body): the lamp (10 steel; lit: 16 wick with its glow)
        /// and the span, whose text keeps wrapLine's breaks (white-space: pre-line, hand.css:35). The lamp-like dot is off on a
        /// plain card (CardPanels: _hoverDot.enabled = !face.Plain). The two flex items shrink as Chrome shrinks them when the
        /// text's widest line is wider than the row (Ocb.HoverLine's rule).
        /// </summary>
        // js: scenes/battle/hand-panels.js:288 (vendor/ds/bundle.js:528)
        private static RectTransform BuildHoverLine(RectTransform parent, string text, bool lit, bool plain)
        {
            string css = Ocb.HoverLineCss + HoverLineCss;
            RectTransform el = Ocb.El(parent, "ocb-hoverline ocbg-battle-hoverline", css);
            UiNode node = UiNode.Of(el);
            BoxStyle bs = Ocb.BoxOf(css);
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            CssBlock rule = Ocb.Rule(css);
            float gap = rule.Layout.ColumnGap ?? 10f;
            float innerW = node.W - bw.Horizontal - pad.Horizontal;
            RectTransform lamp = Ocb.El(el, "ocb-lamp" + (lit ? " is-on" : ""), Ocb.LampCss + (lit ? Ocb.LampOnCss : ""));
            UiNode ln = UiNode.Of(lamp);
            TextStyle ts = Ocb.TextOf(css + HoverLineSpanCss);
            text = text ?? "";
            float lampW = ln.W, tw = Ocb.TextWidth(text, ts);
            float over = lampW + gap + tw - innerW;
            if (over > 0f && lampW + tw > 0f)
            {
                float sum = lampW + tw;
                lampW = Math.Max(0f, lampW - over * lampW / sum);
                tw = Math.Max(0f, innerW - gap - lampW);
            }
            float th = Ocb.TextHeight(text, ts, tw + 0.01f);
            float contentH = Math.Max(ln.H, th);
            float minH = Ocb.Px(rule.Layout.MinHeight) ?? 48f;
            float h = Math.Max(minH, contentH + pad.Vertical + bw.Vertical);
            node.H = h;
            float cy = bw.Top + pad.Top, ch = h - bw.Vertical - pad.Vertical;
            ln.Place(bw.Left + pad.Left, cy + (ch - ln.H) * 0.5f, lampW, ln.H);
            Label.Make(el, text, ts, bw.Left + pad.Left + lampW + gap, cy + (ch - th) * 0.5f, tw + 0.01f, th, "span");
            if (plain) ln.Visible = false; // dot.style.visibility = 'hidden'
            return el;
        }

        /// <summary>
        /// .ocb-reachpanel.ocbg-battle-reachpanel: 280×76 (padding 12 16, flex centred, gap 12, panel-solid, 1px dashed --miss,
        /// radius 6, 400 18/26 body): `range` 22 ink and the hint's parts, one block per part.
        /// </summary>
        // js: scenes/battle/hand-panels.js:305
        private static RectTransform BuildReachPanel(RectTransform parent, IReadOnlyList<string> parts)
        {
            string css = Ocb.ReachPanelCss + ReachPanelCss;
            RectTransform el = Ocb.El(parent, "ocb-reachpanel ocbg-battle-reachpanel", css);
            UiNode node = UiNode.Of(el);
            BoxStyle bs = Ocb.BoxOf(css);
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            float gap = Ocb.Rule(css).Layout.ColumnGap ?? 12f;
            float innerW = node.W - bw.Horizontal - pad.Horizontal, innerH = node.H - bw.Vertical - pad.Vertical;
            PathGraphic icon = Ocb.Icon(el, "range", 22f, DsTokens.Ink);
            TextStyle ts = Ocb.TextOf(css);
            // the text block: its max-content width, shrunk to what is left after the icon and the gap
            float avail = Math.Max(0f, innerW - 22f - gap);
            float blockW = 0f;
            foreach (string t in parts) blockW = Math.Max(blockW, Ocb.TextWidth(t ?? "", ts));
            blockW = Math.Min(blockW, avail);
            var heights = new List<float>();
            float bh = 0f;
            foreach (string t in parts)
            {
                float h = Ocb.TextHeight(t ?? "", ts, blockW + 0.01f);
                heights.Add(h);
                bh += h;
            }
            float x0 = bw.Left + pad.Left, y0 = bw.Top + pad.Top;
            Ui.Place(icon.rectTransform, x0, y0 + (innerH - 22f) * 0.5f);
            RectTransform block = Ocb.El(el, "div");
            UiNode.Of(block).Place(x0 + 22f + gap, y0 + (innerH - bh) * 0.5f, blockW, bh);
            float y = 0f;
            for (int i = 0; i < parts.Count; i++)
            {
                Label.Make(block, parts[i] ?? "", ts, 0f, y, blockW + 0.01f, heights[i], "div");
                y += heights[i];
            }
            return el;
        }

        // ---- term (§7.1) ----

        /// <summary>showTerm(word, mark) = ShowTerm: the meaning above the underlined word (`word`: its stage box); null hides it.</summary>
        // js: scenes/battle/hand-panels.js:318
        public void ShowTerm(StageBox? word, TermMark mark)
        {
            if (word == null || mark == null) { HideTerm(); return; }
            if (_term == null || !ReferenceEquals(_term.Mark, mark))
            {
                HideTerm();
                RectTransform el = BuildTermTip(Layer(BattleLayer.Over), mark);
                float h = UiNode.Of(el).H;
                _term = new TermPart { El = el, Mark = mark, Height = h > 0f ? h : 60f };
            }
            StageBox w = word.Value;
            if (_term.El)
                UiNode.Of(_term.El).Place(Fixed1(ClampX(w.X + w.W / 2f, TermW) - TermW / 2f), Fixed1(w.Y - HoverGap - _term.Height));
        }

        /// <summary>
        /// .ocb-tip.ocbg-battle-termtip: 340 wide (padding 10 12 12, panel-solid, 1px steel, radius 6, shadow-tip): the head
        /// (700 18/24, margin-bottom 6) = mark.Word, the p (400 16/22) = mark.Meaning, both wrapping in the tip's width.
        /// </summary>
        // js: scenes/battle/hand-panels.js:322
        private static RectTransform BuildTermTip(RectTransform parent, TermMark mark)
        {
            string css = Ocb.TipCss + TermTipCss;
            RectTransform el = Ocb.El(parent, "ocb-tip ocbg-battle-termtip", css);
            UiNode node = UiNode.Of(el);
            BoxStyle bs = Ocb.BoxOf(css);
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            float cw = Math.Max(0f, node.W - bw.Horizontal - pad.Horizontal);
            float x0 = bw.Left + pad.Left, y = bw.Top + pad.Top;
            // .ocb-tip-head: a flex row holding the word (a flex item: its max-content width, shrunk to the row, wrapping)
            string headCss = css + Ocb.TipHeadCss;
            TextStyle hs = Ocb.TextOf(headCss);
            string word = mark.Word ?? "";
            float hw = Math.Min(Ocb.TextWidth(word, hs), cw);
            float hh = Ocb.TextHeight(word, hs, hw + 0.01f);
            RectTransform head = Ocb.El(el, "ocb-tip-head");
            UiNode.Of(head).Place(x0, y, cw, hh);
            Label.Make(head, word, hs, 0f, 0f, hw + 0.01f, hh, "text");
            y += hh + (Ocb.Px(Ocb.Rule(Ocb.TipHeadCss).Layout.MarginBottom) ?? 6f);
            // p: the meaning
            TextStyle ps = Ocb.TextOf(css + Ocb.PCss + Ocb.TipPCss);
            string meaning = mark.Meaning ?? "";
            float ph = Ocb.TextHeight(meaning, ps, cw);
            Label.Make(el, meaning, ps, x0, y, cw, ph, "p");
            y += ph + pad.Bottom + bw.Bottom;
            node.H = y;
            return el;
        }

        /// <summary>hideTerm().</summary>
        // js: scenes/battle/hand-panels.js:331
        public void HideTerm()
        {
            if (_term != null) { Ui.Remove(_term.El); _term = null; }
        }

        // ---- detail (§7.4) ----

        /// <summary>detailShown.</summary>
        // js: scenes/battle/hand-panels.js:336
        public bool DetailShown => _detail != null;

        /// <summary>detailFor: the card id whose detail is open ('' for none).</summary>
        // js: scenes/battle/hand-panels.js:337
        public string DetailFor => _detail != null ? _detail.Id : "";

        /// <summary>toggleDetail(face, box, extra) = ToggleDetail: opens the detail of `face` over `box`, or closes it when it is open for that card.</summary>
        // js: scenes/battle/hand-panels.js:339
        public void ToggleDetail(CardFace face, StageBox box, DetailExtra extra)
        {
            if (_detail != null && face != null && _detail.Id == (face.Id ?? "")) { HideDetail(); return; }
            ShowDetail(face, box, extra);
        }

        /// <summary>showDetail(face, box, extra): the detail (buildDetail, fitted to its whole text, 280 … 600 tall) placed by PlaceDetail; the hover line goes.</summary>
        // js: scenes/battle/hand-panels.js:343
        public void ShowDetail(CardFace face, StageBox box, DetailExtra extra)
        {
            HideDetail();
            if (face == null || face.Detail == null) return;
            RectTransform el = HandCard.BuildDetail(face, extra, Layer(BattleLayer.Over));
            HandDetailState st = el ? el.GetComponent<HandDetailState>() : null;
            // Review 19 (a repeat: the glossary still cut a definition short, now in 「…」): the detail grows to hold its whole
            // text and every definition (battle-visual-v1 §7 ③ 全文と用語の定義); only past 600 does a cut fall back.
            try { FitDetail(st); }
            catch (Exception) { /* a layout read that fails leaves the CSS's cut */ }
            float h = st ? st.Height : 0f;
            _detail = new DetailPart { El = el, Id = face.Id ?? "", H = Math.Max(DetailBox.H, h > 0f ? h : DetailBox.H) };
            // The detail stands where the hover line was.
            if (_hover != null) { Ui.Remove(_hover.El); _hover = null; }
            HideTerm();
            PlaceDetail(box);
        }

        /// <summary>
        /// placeDetail(box) (§3.13): 1. straight above the card when that covers no number panel, badge, 俊敏's mark or the
        /// floor row; 2. over the floor (bottom 4 px over its top) when no badge is in the way; 3. that height slid clear of
        /// the badges; 4. Unity's place raised off the panels (top ≥ 84).
        /// </summary>
        // js: scenes/battle/hand-panels.js:369
        public void PlaceDetail(StageBox? box)
        {
            if (_detail == null || box == null) return;
            StageBox b = box.Value;
            float dh = _detail.H > 0f ? _detail.H : DetailBox.H;
            float left = ClampX(b.X + b.W / 2f, DetailBox.W) - DetailBox.W / 2f;
            float unity = b.Y - HoverGap - dh;
            List<StageBox> panels = StatusPanels().Select(p => p.Box).ToList();
            // Review 19: 俊敏's pending choice is no less to keep clear than an omen badge (the detail hid its 「前へ」 mark).
            List<StageBox> freeSteps = FreeStepBoxes();
            List<StageBox> badges = OmenBoxes().Concat(freeSteps).ToList();
            var floorRow = new StageBox(0f, DetailFloor.Top, L.W, DetailFloor.H);
            List<StageBox> HitsAny(float l, float t, IEnumerable<StageBox> rects) => rects
                .Where(r => l < r.X + r.W + DetailGap && l + DetailBox.W > r.X - DetailGap && t < r.Y + r.H + DetailGap && t + dh > r.Y - DetailGap)
                .ToList();
            (float Left, float Top)? at = null;
            if (HitsAny(left, unity, panels.Concat(badges).Concat(new[] { floorRow })).Count == 0) at = (left, unity);
            float over = DetailFloor.Top - DetailGap - dh;
            if (at == null && over >= DetailMinTop)
            {
                List<StageBox> inWay = HitsAny(left, over, badges);
                if (inWay.Count == 0) at = (left, over);
                else
                {
                    float lo = Edge;
                    float hi = L.W - Edge - DetailBox.W;
                    List<float> options = new[] { inWay.Min(r => r.X) - DetailGap - DetailBox.W, inWay.Max(r => r.X + r.W) + DetailGap }
                        .Where(l => l >= lo && l <= hi && HitsAny(l, over, badges).Count == 0)
                        .OrderBy(l => Math.Abs(l - left))
                        .ToList();
                    if (options.Count > 0) at = (options[0], over);
                }
            }
            if (at == null)
            {
                float top = unity;
                foreach (StageBox p in panels.Concat(freeSteps))
                {
                    if (left < p.X + p.W + PanelGap && left + DetailBox.W > p.X - PanelGap && top + dh > p.Y - PanelGap)
                        top = Math.Min(top, p.Y - PanelGap - dh);
                }
                at = (left, Math.Max(DetailMinTop, top));
            }
            if (_detail.El) UiNode.Of(_detail.El).Place(Fixed1(at.Value.Left), Fixed1(at.Value.Top));
        }

        /// <summary>hideDetail().</summary>
        // js: scenes/battle/hand-panels.js:404
        public void HideDetail()
        {
            if (_detail != null) { Ui.Remove(_detail.El); _detail = null; }
        }

        /// <summary>hideAll(): the hover panels and the detail.</summary>
        // js: scenes/battle/hand-panels.js:408
        public void HideAll()
        {
            HideHover();
            HideDetail();
        }

        /// <summary>destroy(): every panel, the refusal line and the guide line go.</summary>
        // js: scenes/battle/hand-panels.js:412
        public void Destroy()
        {
            HideAll();
            HideRefusal();
            if (_guide) Ui.Remove(_guide);
            _guide = null;
            _guideSpan = null;
        }
    }
}
