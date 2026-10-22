// ExploreScene — the cards over the map (ExploreScene.js:845-1055 closePlate … drawChoiceCard, :1137-1238 openConfirm /
// onCardAction), afterDescend (:1404), the banners (:1417-1492), the flow after a step (:1494-1598 proceed, battleFlow,
// returnFlow) and the interlude (:2137-2191); explore.css:114-131, 217-255, 350-371, 404-433; explore.md §5.8-§5.12,
// §5.16, §5.18. Owned by EX-PLATES.
//
// Layout: the browser lays these cards out (flex columns and grids, box-sizing: border-box). Here each card is laid out
// by hand with the stylesheet's own numbers (padding, gap, margins, line heights, border widths), its text measured
// with TMP (TextMeasure) first, so what is measured is what is drawn. The CSS strings are explore.css verbatim
// (ExplorePlateKit), so a reviewer can diff them; the boxes take them through the kit's CSS parser.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using TMPro;
using Trial.Bridge;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Explore
{
    public sealed partial class ExploreScene
    {
        // ------------------------------------------------------------------ cards over the map

        /// <summary>The open plate's classes (openPlate's cls, split): what the stylesheet's plate rules read.</summary>
        private readonly HashSet<string> PlateClassSet = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The text colour of the open plate: washi-ink on a washi plate (.is-washi), else the root's ink.</summary>
        private Rgba PlateTextColor => PlateClassSet.Contains("is-washi") ? ExploreStyle.WashiInk : ExploreStyle.Ink;

        // js: scenes/explore/ExploreScene.js:846
        /// <summary>closePlate(): the plate leaves (plate-out 220 ms, removed after dur(220)); the detail and the hint come back.</summary>
        private void ClosePlate()
        {
            if (Plate == null) return;
            ExplorePlate p = Plate;
            Plate = null;
            PlateClassSet.Clear();
            // carried.classList.remove('is-held') then syncMarks(): SyncMarks syncs the slots' held look (SyncHeld)
            SyncMarks();
            SyncEndLife();
            p.Pop?.Invoke();
            RectTransform el = p.El, dim = p.Dim;
            // .is-leaving: the `animation` property is replaced (the entry stops where it is, the exit starts)
            if (el) CssAnimator.Set(el, ExplorePlateKit.PlateOutAnim);
            if (dim) CssAnimator.Set(dim, ExplorePlateKit.DimOutAnim);
            Timeout(Dur(220f), () =>
            {
                if (el) Ui.Remove(el);
                if (dim) Ui.Remove(dim);
            });
            DetailKey = "";
            DrawDetail();
            DrawHint();
        }

        // js: scenes/explore/ExploreScene.js:867
        /// <summary>
        /// openPlate({ cls, w, ht, keys, kind, x, y, legend }): a card over the map; dims the map and the detail, leaves the
        /// band and the carried items above it. Ht null lets the card take the height of what it holds (FitPlate once it
        /// is filled). Returns the plate's element (Plate holds it with its dim, kind, legend and key pop). The element is
        /// the plate's border box (box-sizing: border-box) with its look (ExplorePlateKit.PlateBoxCss of the classes);
        /// ExplorePlateKit.ContentOf(el) gives its content box.
        /// </summary>
        private RectTransform OpenPlate(PlateOpts o)
        {
            ClosePlate();
            // .ocbg-explore-dim: full screen, ground at 64 %, takes the clicks; fades in 260 ms
            RectTransform dim = Ui.Div(CardLayer, "ocbg-explore-dim", 0f, 0f, ExploreStyle.StageW, ExploreStyle.StageH);
            UiNode.Of(dim).PointerEvents = PointerEvents.Auto;
            BoxGraphic dimBox = BoxGraphic.On(dim, ExplorePlateKit.DimCss);
            dimBox.raycastTarget = true;
            CssAnimator.Set(dim, ExplorePlateKit.DimInAnim);

            PlateClassSet.Clear();
            foreach (string c in (o.Cls ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) PlateClassSet.Add(c);
            float x = o.X ?? ExplorePlateKit.JsRound(ExploreStyle.MapCenterX - o.W / 2f);
            float y = o.Y ?? ExplorePlateKit.JsRound(ExploreStyle.MapCenterY - (o.Ht ?? 0f) / 2f);
            RectTransform el = Ui.Div(CardLayer, ("ocbg-explore-plate " + (o.Cls ?? "")).Trim(), x, y, o.W, o.Ht ?? 0f);
            UiNode.Of(el).PointerEvents = PointerEvents.Auto;
            BoxGraphic box = BoxGraphic.On(el, ExplorePlateKit.PlateBoxCss(PlateClassSet));
            box.raycastTarget = true; // pointer-events: auto
            CssAnimator.Set(el, ExplorePlateKit.PlateInAnim);
            Plate = new ExplorePlate
            {
                El = el,
                Dim = dim,
                Kind = o.Kind,
                Legend = o.Legend,
                Pop = o.Keys != null ? KeysWithFocus(o.Keys) : null,
            };
            // carried.classList.toggle('is-held', kind !== 'screen') then syncMarks(): the slots wait under this card (W-8);
            // SyncMarks syncs their held look (SyncHeld)
            SyncMarks();
            SyncEndLife();
            DetailKey = "";
            DrawDetail();
            DrawHint();
            return el;
        }

        // js: scenes/explore/ExploreScene.js:884
        /// <summary>
        /// Centres an auto-height card on the map once its content is in (it never rises under the band). The card's height
        /// is its UiNode.H when the builder set it; when it is still 0, it is measured from what the card holds (the lowest
        /// child's bottom edge, then the padding and the border).
        /// </summary>
        private void FitPlate(RectTransform el)
        {
            if (!el) return;
            UiNode n = UiNode.Of(el);
            float ht = n.H;
            if (!(ht > 0f))
            {
                ht = ExplorePlateKit.MeasureHeight(el);
                n.H = ht;
            }
            n.Y = Math.Max(120f, ExplorePlateKit.JsRound(ExploreStyle.MapCenterY - ht / 2f));
        }

        // js: scenes/explore/ExploreScene.js:908
        /// <summary>
        /// cardButtons(card, onAct): the card's buttons in a row (.ocbg-explore-plate-buttons; the first is primary) under
        /// parent, at (0, 0) and sized; the caller places it. Its width, alignment and top padding follow the open plate's
        /// classes as the stylesheet does (.is-end: centred, shrink-to-fit, padding-top 22; .is-break 26; .is-result 18;
        /// else the content width, right-aligned, 10); on a washi plate the buttons take the washi rules.
        /// </summary>
        private RectTransform CardButtons(RectTransform parent, ScreenCard card, Action<ScreenAction> onAct)
        {
            var items = new List<PlateButtonSpec>();
            for (int i = 0; i < card.Buttons.Count; i++)
            {
                CardButton b = card.Buttons[i];
                items.Add(new PlateButtonSpec(b.Label, i == 0, () => onAct?.Invoke(b.Action)));
            }
            return PlatesButtonRow(parent, items, ExplorePlateKit.ContentOf(parent).W, PlateClassSet.Contains("is-end"),
                ExplorePlateKit.ButtonsPadTop(PlateClassSet), PlateClassSet.Contains("is-washi") ? PlateButtonSkin.Washi : PlateButtonSkin.Plain);
        }

        /// <summary>
        /// A row of .ocbg-btn (min-width 220, gap 16, align-items: center) under parent, at (0, 0) and sized: as wide as
        /// `avail` with the buttons at its right end, or (center) as wide as the buttons. padTop is the row's padding-top.
        /// Each button joins the Tab order (its own words name it in the legend).
        /// </summary>
        private RectTransform PlatesButtonRow(RectTransform parent, IReadOnlyList<PlateButtonSpec> items, float avail, bool center, float padTop, PlateButtonSkin skin)
        {
            RectTransform row = Ui.Div(parent, "ocbg-explore-plate-buttons");
            var buttons = new List<OcbgButton>();
            foreach (PlateButtonSpec it in items)
            {
                OcbgButton btn = ExplorePlateKit.Button(row, it.Label, it.Primary, false, skin, out Action<bool> ring);
                Action click = it.Click;
                btn.Click += () => click?.Invoke();
                AddFocus(btn, null, ring);
                buttons.Add(btn);
            }
            ExplorePlateKit.RowOfButtons(row, buttons, avail, center, padTop, 220f, 16f, false);
            return row;
        }

        // js: scenes/explore/ExploreScene.js:916
        /// <summary>drawScreenCard(): the screen's card (layer break / end of life / stand-in battle / confirmation / trial choice).</summary>
        private void DrawScreenCard()
        {
            if (Screen == null) return;
            ScreenCard card = Screen.Card;
            if (Busy || FlowRunning) return;
            if (!card.IsShown || card.Kind == CardKind.Interlude)
            {
                if (Plate != null && Plate.Kind == ExploreStyle.PlateScreen) ClosePlate();
                return;
            }
            if (card.IsConfirmation)
            {
                if (Plate != null && Plate.Kind == ExploreStyle.PlateScreen) ClosePlate();
                if (!ConfirmOpen) OpenConfirm(card);
                return;
            }
            if (card.Kind == CardKind.Battle && !BattleFallback) return; // the battle flow handles it
            if (card.IsChoice)
            {
                DrawChoiceCard(card); // trial mode's event / swap / survey cards
                return;
            }
            string key = card.Kind + "|" + card.Title + "|" + string.Join("|", card.Lines);
            if (Plate != null && Plate.Kind == ExploreStyle.PlateScreen && Plate.Key == key) return;
            bool end = card.Kind == CardKind.MiasmaDeath || card.Kind == CardKind.Fallen || card.Kind == CardKind.Completed || card.Kind == CardKind.Survived;
            bool brk = card.Kind == CardKind.LayerCleared || card.Kind == CardKind.PushedOut;
            void Act(ScreenAction action) => OnCardAction(action).Forget();
            RectTransform el = OpenPlate(new PlateOpts
            {
                Cls = end ? "is-end is-" + card.Kind.ToString().ToLowerInvariant() : brk ? "is-washi is-break" : "is-washi",
                W = end ? 980f : 900f,
                Ht = null,
                Kind = ExploreStyle.PlateScreen,
                Legend = card.Buttons.Count > 0 ? new[] { new LegendEntry(card.Buttons[0].Label, ExploreText.KeyEnter) } : null,
                Keys = e =>
                {
                    if (e.Key == "Enter")
                    {
                        if (card.Buttons.Count > 0) Act(card.Buttons[0].Action);
                        return true;
                    }
                    return false;
                },
            });
            Plate.Key = key;

            Rgba ink = PlateTextColor;
            bool washi = !end;
            PlateBoxContent c = ExplorePlateKit.ContentOf(el);
            var flow = new ExplorePlateFlow(c.X, c.Y, c.W, 14f);

            // the end card's mark: miasma / a fallen heart / the player's own way out (survived, cleared)
            if (end)
            {
                IconSpec mark = card.Kind == CardKind.MiasmaDeath ? new IconSpec("miasma", ExploreStyle.Phosphor)
                    : card.Kind == CardKind.Fallen ? new IconSpec("heart", ExploreStyle.Crimson, outline: true)
                    : new IconSpec("stairs", ExploreStyle.Amber);
                float my = flow.Take(76f);
                BoxGraphic markBox = BoxGraphic.Make(el, ExplorePlateKit.EndMarkCss, c.X + (c.W - 76f) / 2f, my, 76f, 76f, "ocbg-explore-end-mark");
                PathGraphic mi = ExploreIcons.Ico(markBox.rectTransform, mark.Name, 40f, mark.Color, mark.Outline ? IcoOpts.OutlineOnly : null);
                Ui.Place(mi.rectTransform, 18f, 18f);
            }
            if (brk)
            {
                RectTransform kicker = PlatesKicker(el, card.Kind == CardKind.LayerCleared ? "Boss" : "hourglass", 28f, ExploreStyle.WashiInk,
                    Screen.LayerTitle, true, null, ink, true, out float kh);
                Ui.Place(kicker, c.X, flow.Take(kh));
            }

            // h2
            TextStyle h2 = ExplorePlateKit.Style(ExplorePlateKit.PlateH2Css + (brk ? ExplorePlateKit.BreakH2Css : "") + (end ? ExplorePlateKit.EndH2Css : ""), ink,
                end ? TextAlign.Center : (TextAlign?)null);
            flow.Block(el, card.Title, h2, "h2");

            // the lines (.ocbg-explore-plate-lines p: margin 0 0 6px; 400 22/34; .is-break: a rule above; .is-end: padding
            // 14 40 6 and a rule above, shrink-to-fit and centred)
            TextStyle pStyle = ExplorePlateKit.Style(ExplorePlateKit.PlateLinesPCss, ink, end ? TextAlign.Center : (TextAlign?)null);
            TextStyle pRich = RichStyle(pStyle);
            var shown = card.Lines.Where(l => !string.IsNullOrEmpty(l)).Select(l => Rich(l, pStyle, washi)).ToList();
            float padTop = 0f, padX = 0f, padBottom = 0f, rule = 0f;
            string linesCss = null;
            if (brk)
            {
                padTop = 12f;
                rule = 1f;
                linesCss = ExplorePlateKit.BreakLinesCss;
            }
            else if (end)
            {
                padTop = 14f;
                padX = 40f;
                padBottom = 6f;
                rule = 1f;
                linesCss = ExplorePlateKit.EndLinesCss;
            }
            float boxW = c.W;
            if (end)
            {
                float widest = 0f;
                foreach (string s in shown) widest = Math.Max(widest, Math.Min(c.W - 2f * padX, TextMeasure.Width(s, pRich) + ExplorePlateKit.Slack));
                boxW = Math.Min(c.W, widest + 2f * padX);
            }
            RectTransform lines = Ui.Div(el, "ocbg-explore-plate-lines");
            float ly = rule + padTop;
            foreach (string s in shown)
            {
                ExplorePlateKit.Txt(lines, s, pRich, padX, ly, boxW - 2f * padX, out _, out float lh, "p");
                ly += lh + 6f;
            }
            ly += padBottom;
            Ui.Size(lines, boxW, ly);
            if (linesCss != null) BoxGraphic.On(lines, linesCss);
            Ui.Place(lines, end ? c.X + (c.W - boxW) / 2f : c.X, flow.Take(ly));

            // the buttons
            RectTransform row = CardButtons(el, card, Act);
            Vector2 rs = row.sizeDelta;
            Ui.Place(row, end ? c.X + (c.W - rs.x) / 2f : c.X, flow.Take(rs.y));

            UiNode.Of(el).H = flow.Y + c.Bottom;
            FitPlate(el);
        }

        // ---- trial mode: the cards that wait on a choice (ExplorationScreenBuilder's TrialCardKind) ----

        // js: scenes/explore/ExploreScene.js:963
        /// <summary>
        /// lineIcon(line, ink): a line's icon on the trial cards, by the word it starts with (the line itself is the
        /// bridge's), at (0, 0) of parent: HP heart 20, 最大スタミナ pip outline 18, スタミナ pip 18, 瘴気 miasma 20, カード seal
        /// 20, 頁 page 20, 消耗品 pouch 20; otherwise .ocbg-explore-fc-dot (an empty 20 px wide spacer, 0 tall). Its size is
        /// the returned element's sizeDelta.
        /// </summary>
        private RectTransform LineIcon(RectTransform parent, string line, Rgba ink)
        {
            string l = line ?? "";
            if (l.StartsWith("HP", StringComparison.Ordinal)) return ExploreIcons.Ico(parent, "heart", 20f, ExploreStyle.Crimson).rectTransform;
            if (l.StartsWith("最大スタミナ", StringComparison.Ordinal)) return ExploreIcons.Ico(parent, "pip", 18f, ExploreStyle.Amber, IcoOpts.OutlineOnly).rectTransform;
            if (l.StartsWith("スタミナ", StringComparison.Ordinal)) return ExploreIcons.Ico(parent, "pip", 18f, ExploreStyle.Amber).rectTransform;
            if (l.StartsWith("瘴気", StringComparison.Ordinal)) return ExploreIcons.Ico(parent, "miasma", 20f, ExploreStyle.Phosphor).rectTransform;
            if (l.StartsWith("カード", StringComparison.Ordinal)) return ExploreIcons.Ico(parent, "seal", 20f, ink).rectTransform;
            if (l.StartsWith("希少な頁", StringComparison.Ordinal) || l.StartsWith("頁", StringComparison.Ordinal)) return ExploreIcons.Ico(parent, "page", 20f, ink).rectTransform;
            if (l.StartsWith("消耗品", StringComparison.Ordinal)) return ExploreIcons.Ico(parent, "pouch", 20f, ink).rectTransform;
            return Ui.Div(parent, "ocbg-explore-fc-dot", 0f, 0f, 20f, 0f);
        }

        // js: scenes/explore/ExploreScene.js:974
        /// <summary>drawChoiceCard(card): trial mode's event / swap / survey cards.</summary>
        private void DrawChoiceCard(ScreenCard card)
        {
            string key = card.Kind + "|" + card.Title + "|" + string.Join("|", card.Lines) + "|" +
                         string.Join("|", card.Buttons.Select(b => b.Label + (b.Arg ?? "null")));
            if (Plate != null && Plate.Kind == ExploreStyle.PlateChoice && Plate.Key == key) return;
            void Act(CardButton b) => OnCardAction(b.Action, b.Arg).Forget();
            CardButton leave = card.Buttons.FirstOrDefault(b => b.Action == ScreenAction.LeaveFound);
            List<CardButton> picks = card.Buttons.Where(b => !ReferenceEquals(b, leave)).ToList();
            var legend = new List<LegendEntry>();
            for (int i = 0; i < picks.Count; i++) legend.Add(new LegendEntry(picks[i].Label, (i + 1).ToString(CultureInfo.InvariantCulture)));
            if (leave != null) legend.Add(new LegendEntry(leave.Label, "Esc"));
            CardKind kind = card.Kind;
            RectTransform el = OpenPlate(new PlateOpts
            {
                Cls = "is-washi is-choice is-" + kind.ToString().ToLowerInvariant(),
                W = kind == CardKind.Swap ? 1180f : kind == CardKind.EventChoice ? 1000f : 900f,
                Ht = null,
                Kind = ExploreStyle.PlateChoice,
                Legend = legend,
                Keys = e =>
                {
                    // Number(e.key): 1 … n picks
                    string k = e.Key ?? "";
                    if (k.Length == 1 && k[0] >= '1' && k[0] <= '9')
                    {
                        int n = k[0] - '0';
                        if (n <= picks.Count)
                        {
                            Act(picks[n - 1]);
                            return true;
                        }
                    }
                    if (k == "Escape" && leave != null)
                    {
                        Act(leave);
                        return true;
                    }
                    return false;
                },
            });
            Plate.Key = key;
            Rgba ink = ExploreStyle.WashiInk;
            PlateBoxContent c = ExplorePlateKit.ContentOf(el);
            var flow = new ExplorePlateFlow(c.X, c.Y, c.W, 14f);
            TextStyle h2 = ExplorePlateKit.Style(ExplorePlateKit.PlateH2Css, ink);
            TextStyle leadStyle = ExplorePlateKit.Style(ExplorePlateKit.ChoiceLeadCss, ink);

            if (kind == CardKind.EventChoice)
            {
                MapNode node = Session.PendingEvent != null ? Session.State.Map.Node(Session.PendingEvent.NodeId) : null;
                string lead = card.Lines.Count > 0 ? card.Lines[0] : "";
                RectTransform kicker = PlatesKicker(el, "Event", 28f, ink, ExploreText.EventKicker, false, node != null ? Screen.LayerTitle : null, ink, true, out float kh);
                Ui.Place(kicker, c.X, flow.Take(kh));
                flow.Block(el, card.Title, h2, "h2");
                flow.Block(el, lead, leadStyle, "ocbg-explore-choice-lead");

                // .ocbg-explore-plate-lines.is-list: padding 8 0 4, a rule above; p: margin 0, 400 20/32
                TextStyle lp = ExplorePlateKit.Style(ExplorePlateKit.PlateLinesPCss + ExplorePlateKit.ListLinesPCss, ink);
                RectTransform list = Ui.Div(el, "ocbg-explore-plate-lines is-list");
                float ly = 1f + 8f;
                for (int i = 1; i < card.Lines.Count; i++)
                {
                    ExplorePlateKit.Txt(list, Rich(card.Lines[i], lp, true), RichStyle(lp), 0f, ly, c.W, out _, out float lh, "p");
                    ly += lh;
                }
                ly += 4f;
                Ui.Size(list, c.W, ly);
                BoxGraphic.On(list, ExplorePlateKit.ListLinesCss);
                Ui.Place(list, c.X, flow.Take(ly));

                // .ocbg-explore-choice-tiles: two columns, gap 20, padding-top 6; each row as tall as its tallest tile
                float tileW = (c.W - 20f) / 2f;
                var tiles = new List<(RectTransform El, float H)>();
                for (int i = 0; i < picks.Count; i++)
                {
                    CardButton b = picks[i];
                    RectTransform tile = PlatesChoiceTile(el, b, i, tileW, () => Act(b), out float th);
                    tiles.Add((tile, th));
                }
                int rows = (tiles.Count + 1) / 2;
                var rowH = new float[rows];
                for (int i = 0; i < tiles.Count; i++) rowH[i / 2] = Math.Max(rowH[i / 2], tiles[i].H);
                float gridH = rowH.Sum() + 20f * Math.Max(0, rows - 1);
                float gy = flow.Take(6f + gridH) + 6f;
                for (int i = 0; i < tiles.Count; i++)
                {
                    int r = i / 2, col = i % 2;
                    float ty = gy;
                    for (int k = 0; k < r; k++) ty += rowH[k] + 20f;
                    UiNode.Of(tiles[i].El).Place(c.X + col * (tileW + 20f), ty, tileW, rowH[r]);
                }
            }
            else if (kind == CardKind.Swap)
            {
                string foundId = Session.PendingFound != null && Session.PendingFound.Count > 0 ? Session.PendingFound[0] : null;
                string lead = card.Lines.Count > 0 ? card.Lines[0] : "";
                List<string> carriedIds = Session.State.Loadout.Consumables.Select(x => x.Id).ToList();
                RectTransform kicker = PlatesKicker(el, "pouch", 28f, ink, ExploreText.FoundItem, false, null, ink, true, out float kh);
                Ui.Place(kicker, c.X, flow.Take(kh));
                flow.Block(el, card.Title, h2, "h2");
                flow.Block(el, lead, leadStyle, "ocbg-explore-choice-lead");

                // .ocbg-explore-swap-body: grid 300 | 1fr, gap 28, padding-top 6, the two sides stretched to the row
                const float foundW = 300f;
                float sideX = c.X + foundW + 28f, sideW = c.W - foundW - 28f;
                RectTransform found = PlatesSwapFound(el, foundId, foundW, out float foundH);
                TextStyle labelStyle = ExplorePlateKit.Style(ExplorePlateKit.SwapLabelCss, ink);
                TMP_Text sideLabel = ExplorePlateKit.Txt(el, ExploreText.Carried, labelStyle, 0f, 0f, null, out _, out float labelH, "ocbg-explore-swap-label");
                float slotW = (sideW - 2f * 14f) / 3f;
                var slots = new List<(RectTransform El, RectTransform Go, float H)>();
                for (int i = 0; i < picks.Count; i++)
                {
                    CardButton b = picks[i];
                    int at = int.TryParse(b.Arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ai) ? ai : -1;
                    string id = at >= 0 && at < carriedIds.Count ? carriedIds[at] : null;
                    RectTransform slot = PlatesSwapSlot(el, b, i, id, slotW, () => Act(b), out RectTransform go, out float sh);
                    slots.Add((slot, go, sh));
                }
                int rows = (slots.Count + 2) / 3;
                var rowH = new float[rows];
                for (int i = 0; i < slots.Count; i++) rowH[i / 3] = Math.Max(rowH[i / 3], slots[i].H);
                float slotsH = rowH.Sum() + 14f * Math.Max(0, rows - 1);
                float bodyH = Math.Max(foundH, labelH + 10f + slotsH);
                // flex: 1 — the slots grid fills the side; a taller row of the body stretches its rows
                if (rows > 0)
                {
                    float extra = (bodyH - labelH - 10f - slotsH) / rows;
                    for (int r = 0; r < rows; r++) rowH[r] += extra;
                }
                float by = flow.Take(6f + bodyH) + 6f;
                UiNode.Of(found).Place(c.X, by, foundW, bodyH);
                Ui.Place(sideLabel.rectTransform, sideX, by);
                for (int i = 0; i < slots.Count; i++)
                {
                    int r = i / 3, col = i % 3;
                    float sy = by + labelH + 10f;
                    for (int k = 0; k < r; k++) sy += rowH[k] + 14f;
                    UiNode.Of(slots[i].El).Place(sideX + col * (slotW + 14f), sy, slotW, rowH[r]);
                    // .ocbg-explore-swap-go: margin-top auto (the slot's bottom: padding 14, border 2)
                    RectTransform go = slots[i].Go;
                    if (go) Ui.Place(go, Ui.PosOf(go).x, rowH[r] - 2f - 14f - go.sizeDelta.y);
                }
                if (leave != null)
                {
                    RectTransform row = PlatesButtonRow(el, new[] { new PlateButtonSpec(leave.Label, false, () => Act(leave)) }, c.W, false,
                        ExplorePlateKit.ButtonsPadTop(PlateClassSet), PlateButtonSkin.Washi);
                    Ui.Place(row, c.X, flow.Take(row.sizeDelta.y));
                }
            }
            else
            {
                RectTransform kicker = PlatesKicker(el, "Survey", 28f, ink, ExploreText.SurveyKicker, false, null, ink, true, out float kh);
                Ui.Place(kicker, c.X, flow.Take(kh));
                flow.Block(el, card.Title, h2, "h2");
                // .ocbg-explore-plate-lines p: margin 0 0 6px, 400 22/34
                TextStyle lp = ExplorePlateKit.Style(ExplorePlateKit.PlateLinesPCss, ink);
                RectTransform lines = Ui.Div(el, "ocbg-explore-plate-lines");
                float ly = 0f;
                foreach (string l in card.Lines)
                {
                    ExplorePlateKit.Txt(lines, Rich(l, lp, true), RichStyle(lp), 0f, ly, c.W, out _, out float lh, "p");
                    ly += lh + 6f;
                }
                Ui.Size(lines, c.W, ly);
                Ui.Place(lines, c.X, flow.Take(ly));
                // .ocbg-explore-survey-list: a column, gap 10, padding-top 4
                var rowsList = new List<(RectTransform El, float H)>();
                for (int i = 0; i < picks.Count; i++)
                {
                    CardButton b = picks[i];
                    RectTransform r = PlatesSurveyRow(el, b, i, c.W, () => Act(b), out float rh);
                    rowsList.Add((r, rh));
                }
                float listH = rowsList.Sum(q => q.H) + 10f * Math.Max(0, rowsList.Count - 1);
                float sy = flow.Take(4f + listH) + 4f;
                foreach ((RectTransform El, float H) r in rowsList)
                {
                    UiNode.Of(r.El).Place(c.X, sy, c.W, r.H);
                    sy += r.H + 10f;
                }
            }
            UiNode.Of(el).H = flow.Y + c.Bottom;
            FitPlate(el);
            Sfx(kind == CardKind.Swap ? "rewardReveal" : "uiClick");
        }

        /// <summary>
        /// .ocbg-explore-plate-kicker (explore.css:229): an icon, gap 10, the text (a bridge line through rich(), or a scene
        /// word as it is), and for the event card a .ocbg-explore-kicker-sub (margin-left 10, padding-left 12, a rule on the
        /// left, weight 400) holding rich(sub). At (0, 0) of parent and sized; align-items: center, so h = the tallest of the
        /// icon and the text items, each item centred in it. A text item is its line box: 24, or 26 with rich()'s digits
        /// (ExplorePlateKit.MonoLineGrowth; the event card's sub measured 26 tall in 820).
        /// </summary>
        private RectTransform PlatesKicker(RectTransform parent, string icon, float size, Rgba iconColor, string text, bool textRich, string sub,
            Rgba ink, bool onWashi, out float h)
        {
            RectTransform row = Ui.Div(parent, "ocbg-explore-plate-kicker");
            TextStyle ks = ExplorePlateKit.Style(ExplorePlateKit.PlateKickerCss, ink);
            PathGraphic ic = ExploreIcons.Ico(row, icon, size, iconColor);
            float x = size + 10f;
            string shown = textRich ? Rich(text, ks, onWashi) : text ?? "";
            TMP_Text t = ExplorePlateKit.Txt(row, shown, textRich ? RichStyle(ks) : ks, x, 0f, null, out float tw, out float th, "text");
            x += tw;
            BoxGraphic subBox = null;
            float subX = 0f, sh = 0f;
            if (sub != null)
            {
                x += 10f + 10f; // the row's gap, the sub's margin-left
                subX = x;
                TextStyle ss = ExplorePlateKit.Style(ExplorePlateKit.PlateKickerCss + ExplorePlateKit.KickerSubCss, ink);
                string sShown = Rich(sub, ss, onWashi);
                TextStyle srs = RichStyle(ss);
                // one line: its line box (with the digits' growth) is the span's height, its border-left that tall
                float sw = TextMeasure.Width(sShown, srs);
                sh = srs.LineHeightPx + ExplorePlateKit.MonoLineGrowth(srs, sShown);
                subBox = BoxGraphic.Make(row, ExplorePlateKit.KickerSubCss, x, 0f, 1f + 12f + sw + ExplorePlateKit.Slack, sh, "ocbg-explore-kicker-sub");
                ExplorePlateKit.Txt(subBox.rectTransform, sShown, srs, 13f, 0f, null, out _, out _, "text");
                x += 13f + sw;
            }
            h = Math.Max(size, Math.Max(th, sh));
            Ui.Place(ic.rectTransform, 0f, (h - size) / 2f);
            Ui.Place(t.rectTransform, size + 10f, (h - th) / 2f);
            if (subBox) Ui.Place(subBox.rectTransform, subX, (h - sh) / 2f);
            Ui.Size(row, x, h);
            return row;
        }

        /// <summary>
        /// .ocbg-explore-choice-tile (explore.css:410-416): a button with its kbd (left 20, top 22), the choice's label and
        /// its note lines (split at 「　」, each with lineIcon). Built at (0, 0), tileW wide; naturalH = its own height
        /// (min-height 150); the caller places it and stretches it to its grid row.
        /// </summary>
        private RectTransform PlatesChoiceTile(RectTransform parent, CardButton b, int i, float tileW, Action click, out float naturalH)
        {
            RectTransform tile = Ui.Div(parent, "ocbg-explore-choice-tile" + (i == 0 ? " is-primary" : ""), 0f, 0f, tileW, 0f);
            UiNode.Of(tile);
            ExplorePlateKit.Interactive(tile, ExplorePlateKit.ChoiceTileCss, ".is-primary", ExplorePlateKit.ChoiceTilePrimaryCss, i == 0,
                ExplorePlateKit.ChoiceTileHoverCss, click, out Action<bool> ring);
            // kbd: position absolute from the padding box (inside the 2 px border)
            ExplorePlateKit.Kbd(tile, (i + 1).ToString(CultureInfo.InvariantCulture), 2f + 20f, 2f + 22f);
            float cx = 2f + 64f, cy = 2f + 20f, cw = tileW - 4f - 64f - 24f;
            TextStyle strong = ExplorePlateKit.Style(ExplorePlateKit.ChoiceTileStrongCss, ExploreStyle.Ink);
            ExplorePlateKit.TxtCap(tile, b.Label, strong, cx, cy, cw, out _, out float sh, "strong");
            // the note's span is there even without a note (an empty flex item: 0 tall, it still takes the gap)
            float y = cy + sh + 10f;
            string noteText = "";
            if (!string.IsNullOrEmpty(b.Note))
            {
                RectTransform note = Ui.Div(tile, "ocbg-explore-choice-note", cx, y, cw, 0f);
                float noteH = PlatesNoteLines(note, b.Note.Split('　'), ExplorePlateKit.Style(ExplorePlateKit.ChoiceNoteCss, ExploreStyle.Ink), cw, out noteText);
                Ui.Size(note, cw, noteH);
                y += noteH;
            }
            naturalH = Math.Max(150f, y + 18f + 2f);
            // buttonLabel(b): the tile's first span is the note's (made even when the button has no note: then empty)
            string label = noteText;
            AddFocus(tile, click, ring, null, () => label);
            return tile;
        }

        /// <summary>
        /// .ocbg-explore-choice-note's lines (a column, gap 4): each an inline-flex row (gap 8, centred) of lineIcon and
        /// rich(line), wrapping in what is left of `w`. Returns the height; `text` is the lines' text joined (its textContent).
        /// </summary>
        private float PlatesNoteLines(RectTransform note, IReadOnlyList<string> lines, TextStyle style, float w, out string text)
        {
            float y = 0f;
            var joined = new System.Text.StringBuilder();
            TextStyle rs = RichStyle(style);
            for (int k = 0; k < lines.Count; k++)
            {
                string l = lines[k];
                joined.Append(l);
                RectTransform lineRow = Ui.Div(note, "line");
                RectTransform icon = LineIcon(lineRow, l, ExploreStyle.Ink);
                Vector2 isz = icon.sizeDelta;
                // the tile sits on the washi plate: `.is-washi .ocbg-explore-sealed` (explore.css:9) reaches its runs too
                TMP_Text t = ExplorePlateKit.TxtCap(lineRow, Rich(l, style, true), rs, isz.x + 8f, 0f, Math.Max(1f, w - isz.x - 8f), out float tw, out float th, "text");
                float rh = Math.Max(isz.y, th);
                Ui.Place(icon, 0f, (rh - isz.y) / 2f);
                Ui.Place(t.rectTransform, isz.x + 8f, (rh - th) / 2f);
                Ui.Place(lineRow, 0f, y, isz.x + 8f + tw, rh);
                y += rh + (k < lines.Count - 1 ? 4f : 0f);
            }
            text = joined.ToString();
            return y;
        }

        /// <summary>
        /// .ocbg-explore-swap-found (explore.css:419-421): the found item — 「拾った物」, its icon 56, its name, its note —
        /// centred in a 300 px amber tile. Built at (0, 0); naturalH = its own height.
        /// </summary>
        private RectTransform PlatesSwapFound(RectTransform parent, string foundId, float w, out float naturalH)
        {
            Rgba ink = ExploreStyle.WashiInk;
            RectTransform found = Ui.Div(parent, "ocbg-explore-swap-found", 0f, 0f, w, 0f);
            UiNode.Of(found);
            BoxGraphic.On(found, ExplorePlateKit.SwapFoundCss);
            // flex column, centred, gap 10, padding 18 18 20 inside a 2 px border
            float cw = w - 4f - 36f;
            float y = 2f + 18f;
            void Centre(RectTransform rt, float iw, float ih)
            {
                Ui.Place(rt, 2f + 18f + (cw - iw) / 2f, y);
                y += ih + 10f;
            }
            TextStyle labelStyle = ExplorePlateKit.Style(ExplorePlateKit.SwapLabelCss, ink, TextAlign.Center);
            TMP_Text label = ExplorePlateKit.TxtCap(found, ExploreText.FoundItem, labelStyle, 0f, 0f, cw, out float lw, out float lh, "ocbg-explore-swap-label");
            Centre(label.rectTransform, lw, lh);
            PathGraphic ic = ExploreIcons.Ico(found, ExploreIcons.ItemIcon(foundId), 56f, ink);
            Centre(ic.rectTransform, 56f, 56f);
            TextStyle strong = ExplorePlateKit.Style(ExplorePlateKit.SwapFoundStrongCss, ink, TextAlign.Center);
            TMP_Text name = ExplorePlateKit.TxtCap(found, foundId != null ? ExplorationText.ItemName(foundId) : "", strong, 0f, 0f, cw, out float nw, out float nh, "strong");
            Centre(name.rectTransform, nw, nh);
            TextStyle noteStyle = ExplorePlateKit.Style(ExplorePlateKit.ChoiceNoteCss + ExplorePlateKit.SwapFoundNoteCss, ink, TextAlign.Center);
            TMP_Text note = ExplorePlateKit.TxtCap(found, foundId != null ? ExplorationText.PlayerNote(foundId) : "", noteStyle, 0f, 0f, cw, out float tw, out float th, "ocbg-explore-choice-note");
            Ui.Place(note.rectTransform, 2f + 18f + (cw - tw) / 2f, y);
            y += th;
            naturalH = y + 20f + 2f;
            return found;
        }

        /// <summary>
        /// .ocbg-explore-swap-slot (explore.css:424-429): a carried slot to swap into — kbd (10, 10), icon 40, name, note,
        /// and the 「入れ替える」 chip pushed to the bottom (margin-top: auto; `go`, placed by the caller once the slot has its
        /// row's height). Built at (0, 0); naturalH = its own height.
        /// </summary>
        private RectTransform PlatesSwapSlot(RectTransform parent, CardButton b, int i, string id, float w, Action click, out RectTransform go, out float naturalH)
        {
            RectTransform slot = Ui.Div(parent, "ocbg-explore-swap-slot", 0f, 0f, w, 0f);
            UiNode.Of(slot);
            ExplorePlateKit.Interactive(slot, ExplorePlateKit.SwapSlotCss, null, null, false, ExplorePlateKit.SwapSlotHoverCss, click, out Action<bool> ring);
            ExplorePlateKit.Kbd(slot, (i + 1).ToString(CultureInfo.InvariantCulture), 2f + 10f, 2f + 10f);
            float cw = w - 4f - 24f;
            float x0 = 2f + 12f;
            float y = 2f + 16f;
            void Centre(RectTransform rt, float iw, float ih)
            {
                Ui.Place(rt, x0 + (cw - iw) / 2f, y);
                y += ih + 8f;
            }
            if (id != null)
            {
                PathGraphic ic = ExploreIcons.Ico(slot, ExploreIcons.ItemIcon(id), 40f, ExploreStyle.Ink);
                Centre(ic.rectTransform, 40f, 40f);
            }
            TextStyle strong = ExplorePlateKit.Style(ExplorePlateKit.SwapSlotStrongCss, ExploreStyle.Ink, TextAlign.Center);
            TMP_Text name = ExplorePlateKit.TxtCap(slot, id != null ? ExplorationText.ItemName(id) : b.Label, strong, 0f, 0f, cw, out float nw, out float nh, "strong");
            Centre(name.rectTransform, nw, nh);
            TextStyle noteStyle = ExplorePlateKit.Style(ExplorePlateKit.ChoiceNoteCss + ExplorePlateKit.SwapSlotNoteCss, ExploreStyle.Ink, TextAlign.Center);
            TMP_Text note = ExplorePlateKit.TxtCap(slot, b.Note ?? "", noteStyle, 0f, 0f, cw, out float tw, out float th, "ocbg-explore-choice-note");
            Centre(note.rectTransform, tw, th);
            // the chip: padding 4 14, 1 px amber border, radius 15, 700 16/22
            TextStyle goStyle = ExplorePlateKit.Style(ExplorePlateKit.SwapGoCss, ExploreStyle.Ink);
            float gw = TextMeasure.Width(ExploreText.Swap, goStyle) + ExplorePlateKit.Slack;
            float gh = goStyle.LineHeightPx + 8f + 2f;
            BoxGraphic chip = BoxGraphic.Make(slot, ExplorePlateKit.SwapGoCss, x0 + (cw - (gw + 30f)) / 2f, y, gw + 28f + 2f, gh, "ocbg-explore-swap-go");
            ExplorePlateKit.Txt(chip.rectTransform, ExploreText.Swap, goStyle, 15f, 5f, null, out _, out _, "text");
            go = chip.rectTransform;
            y += gh;
            naturalH = y + 14f + 2f;
            // buttonLabel(b): its first span is the note
            string label = b.Note ?? "";
            AddFocus(slot, click, ring, null, () => label);
            return slot;
        }

        /// <summary>
        /// .ocbg-explore-survey-row (explore.css:431-433): kbd, the enemy icon (Battle 22 violet) and rich(label) in bold,
        /// in a row (gap 14, padding 14 18, centred). Built at (0, 0), w wide; naturalH = its own height.
        /// </summary>
        private RectTransform PlatesSurveyRow(RectTransform parent, CardButton b, int i, float w, Action click, out float naturalH)
        {
            RectTransform row = Ui.Div(parent, "ocbg-explore-survey-row", 0f, 0f, w, 0f);
            UiNode.Of(row);
            ExplorePlateKit.Interactive(row, ExplorePlateKit.SurveyRowCss, null, null, false, ExplorePlateKit.SurveyRowHoverCss, click, out Action<bool> ring);
            string key = (i + 1).ToString(CultureInfo.InvariantCulture);
            TextStyle strong = ExplorePlateKit.Style(ExplorePlateKit.SurveyRowCss + ExplorePlateKit.SurveyRowStrongCss, ExploreStyle.Ink);
            float x = 2f + 18f;
            RectTransform kbd = ExplorePlateKit.Kbd(row, key, x, 0f);
            float kw = kbd.sizeDelta.x;
            x += kw + 14f;
            PathGraphic ic = ExploreIcons.Ico(row, "Battle", 22f, ExploreStyle.Violet);
            float ix = x;
            x += 22f + 14f;
            // on the washi plate: `.is-washi .ocbg-explore-sealed` (explore.css:9) reaches the row's runs too
            TMP_Text t = ExplorePlateKit.TxtCap(row, Rich(b.Label, strong, true), RichStyle(strong), x, 0f, Math.Max(1f, w - 2f - 18f - x), out _, out float th, "strong");
            float ch = Math.Max(24f, Math.Max(22f, th));
            float top = 2f + 14f;
            Ui.Place(kbd, 2f + 18f, top + (ch - 24f) / 2f);
            Ui.Place(ic.rectTransform, ix, top + (ch - 22f) / 2f);
            Ui.Place(t.rectTransform, x, top + (ch - th) / 2f);
            naturalH = top + ch + 14f + 2f;
            // buttonLabel(b): no span of its own, so its whole text
            string label = key + (b.Label ?? "");
            AddFocus(row, click, ring, null, () => label);
            return row;
        }

        // js: scenes/explore/ExploreScene.js:1137
        /// <summary>openConfirm(card): a confirmation through ask(); cancel → session.Act(Cancel), ok → onCardAction(Buttons[0]).</summary>
        private void OpenConfirm(ScreenCard card)
        {
            ConfirmOpen = true;
            SyncMarks();
            SyncEndLife();
            Sfx("uiClick");
            bool lethal = card.Kind != CardKind.ConfirmEndLife;
            Ask(new ConfirmOpts
            {
                Title = card.Title,
                Body = string.Join("\n", card.Lines),
                Ok = card.Buttons.Count > 0 ? card.Buttons[0].Label : null,
                Cancel = card.Buttons.Count > 1 ? card.Buttons[1].Label : "やめる",
                Danger = lethal,
            }).Then(yes =>
            {
                ConfirmOpen = false;
                if (!Alive) return;
                SyncEndLife();
                if (!yes)
                {
                    StepInfo = null;
                    Session.Act(ScreenAction.Cancel);
                    Persist();
                    Sfx("uiCancel");
                    Render();
                    return;
                }
                if (card.Buttons.Count == 0) return;
                ScreenAction action = card.Buttons[0].Action;
                OnCardAction(action).Forget();
            }).Forget();
        }

        // js: scenes/explore/ExploreScene.js:1166
        /// <summary>onCardAction(action, arg): what a card's button does (trial actions, NewLife, AskEndLife, Descend …).</summary>
        private async Promise OnCardAction(ScreenAction action, string arg = null)
        {
            if (!Alive) return;
            if (ExploreStyle.IsTrialAction(action))
            {
                // trial mode: an event answered, a find swapped or left, the survey's target picked
                if (!Session.Act(action, arg)) return;
                Persist();
                ClosePlate();
                if (action == ScreenAction.TakeFound) Sfx("consumableUse");
                else if (action == ScreenAction.ChooseSurvey) Sfx("infoGained");
                else if (action == ScreenAction.AcceptEvent) Sfx("eventResolve");
                else Sfx("uiCancel");
                Render();
                if (action == ScreenAction.ChooseSurvey) NoteBanner(ExploreText.SurveyKicker, ExplorationScreenBuilder.SurveyLine(Session.Raw), "Survey");
                Proceed().Forget();
                return;
            }
            ExploreStepInfo waiting = StepInfo;
            if (action == ScreenAction.ConfirmStep || action == ScreenAction.Cancel) StepInfo = null;
            if (action == ScreenAction.NewLife)
            {
                ExploreLifeEnd end = LifeEnd(Session, Screen);
                // The app takes the life's end from here (the ending card, 継承の間); the card stays until it does.
                if (Ctx.OnLifeEnd != null)
                {
                    Ctx.OnLifeEnd(end);
                    return;
                }
                ClosePlate();
                Session.Act(ScreenAction.NewLife);
                BattleFallback = false;
                MapRef = null;
                Render(enter: true);
                LayerBanner().Forget();
                Proceed().Forget();
                return;
            }
            if (action == ScreenAction.AskEndLife)
            {
                if (Session.Act(action))
                {
                    Persist();
                    Render();
                }
                return;
            }
            // 次の層へ降りる with training marks left: the marks are this layer's, so the card asks for them first
            if (action == ScreenAction.Descend && Session.State.TrainingMarks > 0 && Session.IsIdle)
            {
                if (Busy || Plate != null || ConfirmOpen) return;
                bool go = await TrainBeforeDescend();
                if (!Alive) return;
                if (!go)
                {
                    Render();
                    return;
                }
            }
            if (!Session.Act(action)) return;
            Persist();
            ClosePlate();
            switch (action)
            {
                case ScreenAction.GoToInterlude:
                    Sfx("interlude");
                    Render();
                    break;
                case ScreenAction.Descend:
                case ScreenAction.ConfirmDescend:
                    if (Session.ConfirmingDescend)
                    {
                        Render();
                        break;
                    }
                    await AfterDescend();
                    break;
                case ScreenAction.ConfirmStep:
                    if (waiting != null) await AfterMove(waiting.Before, waiting.Node, waiting.Choice, waiting.Jump);
                    else Render();
                    break;
                case ScreenAction.ConfirmEndLife:
                    Sfx("uiConfirm");
                    Render();
                    break;
                case ScreenAction.WinBattle:
                case ScreenAction.FallInBattle:
                    BattleFallback = false;
                    Render();
                    Proceed().Forget();
                    break;
                default:
                    Render();
                    break;
            }
        }

        // js: scenes/explore/ExploreScene.js:1404
        /// <summary>afterDescend(): the new layer assembles; a death on entry sounds after 600 ms, else the layer banner, then proceed.</summary>
        private async Promise AfterDescend()
        {
            Sfx("descend");
            BattleFallback = false;
            Render(enter: true);
            ExplorationState st = Session.State;
            if (st.Phase == RunPhase.MiasmaDeath)
            {
                Sfx("miasmaDeath", 600);
                return;
            }
            await LayerBanner();
            Proceed().Forget();
        }

        // ------------------------------------------------------------------ banners
        // Each banner owns the banner element until the next one starts: an older banner's timers must not take down a
        // newer one (the layer banner's fade used to cut the encounter banner short).

        /// <summary>bannerSeq: each banner owns the banner element until the next one starts.</summary>
        private int BannerSeq;

        /// <summary>The banner's box width (explore.css:115 width: 1256px; left 24).</summary>
        private const float BannerW = 1256f;

        /// <summary>The banner's state machine: .is-on (and the root's .is-interlude) over its base rule.</summary>
        private CssStates BannerStates;

        /// <summary>What the banner holds (banner.textContent), between its ::before and ::after rules.</summary>
        private RectTransform BannerContent;

        /// <summary>::before / ::after: the two 2 px rules (amber at .7; violet for the encounter).</summary>
        private BoxGraphic BannerBefore, BannerAfter;

        /// <summary>The banner's .is-encounter / .is-note classes.</summary>
        private bool BannerIsEncounter, BannerIsNote;

        /// <summary>The banner's children as the JS appends them (laid out again whenever a class changes).</summary>
        private readonly List<BannerPart> BannerParts = new List<BannerPart>();

        /// <summary>One child of the banner: small / strong / em (text, through rich() or as it is), icon, or the empty rule.</summary>
        private sealed class BannerPart
        {
            public string Role;
            public string Text;
            public bool Rich;
            public string Icon;
            public Rgba IconColor;
            public float IconSize;

            public static BannerPart Of(string role, string text, bool rich, string icon = null, Rgba iconColor = default, float iconSize = 0f) =>
                new BannerPart { Role = role, Text = text ?? "", Rich = rich, Icon = icon, IconColor = iconColor, IconSize = iconSize };
        }

        // js: scenes/explore/ExploreScene.js:266
        /// <summary>Styles the banner element (.ocbg-explore-banner) and registers `.is-interlude .banner` (opacity 0). Called once from Mount.</summary>
        private void BuildBanner()
        {
            UiNode node = UiNode.Of(Banner);
            node.Place(24f, 392f, BannerW, 200f);
            BannerBefore = BoxGraphic.Make(Banner, ExplorePlateKit.BannerRuleCss, 0f, 0f, 0f, 2f, "::before");
            BannerBefore.Opacity = 0.7f;
            BannerContent = Ui.Div(Banner, "content", 0f, 0f, BannerW, 200f);
            BannerAfter = BoxGraphic.Make(Banner, ExplorePlateKit.BannerRuleCss, 0f, 0f, 0f, 2f, "::after");
            BannerAfter.Opacity = 0.7f;
            BoxGraphic.On(Banner);
            BannerStates = CssStates.Of(Banner);
            BannerStates.Base(ExplorePlateKit.BannerCss).State(".is-on", ExplorePlateKit.BannerOnCss);
            BannerStates.Apply(instant: true);
            // explore.css:370 .ocbg-explore.is-interlude .ocbg-explore-banner (after the banner's own rules)
            WatchRootClass(".is-interlude", BannerStates, ExploreStyle.InterludeHiddenCss);
            LayOutBanner();
        }

        /// <summary>
        /// The banner's box and children for its classes (explore.css:115-131): top 392 / h 200 / gap 4; .is-encounter
        /// top 362 / h 260 / gap 6 / violet rules / strong 56/64; .is-note top 412 / h 160 / small a row with its icon /
        /// strong 40/52 .06em, at most 1120 wide, centred. A flex column, centred both ways.
        /// </summary>
        private void LayOutBanner()
        {
            if (!Banner || !BannerContent) return;
            bool enc = BannerIsEncounter, note = BannerIsNote;
            float top = enc ? 362f : note ? 412f : 392f;
            float h = enc ? 260f : note ? 160f : 200f;
            float gap = enc ? 6f : 4f;
            UiNode.Of(Banner).Place(24f, top, BannerW, h);
            // ::before / ::after: left 24%, right 24%, 2 px, top 10 / bottom 10
            float rx = BannerW * 0.24f, rw = BannerW * 0.52f;
            string ruleCss = ExplorePlateKit.BannerRuleCss + (enc ? ExplorePlateKit.BannerEncounterRuleCss : "");
            if (BannerBefore)
            {
                Ui.Place(BannerBefore.rectTransform, rx, 10f, rw, 2f);
                BannerBefore.SetCss(ruleCss);
            }
            if (BannerAfter)
            {
                Ui.Place(BannerAfter.rectTransform, rx, h - 10f - 2f, rw, 2f);
                BannerAfter.SetCss(ruleCss);
            }
            Ui.Clear(BannerContent);
            Ui.Place(BannerContent, 0f, 0f, BannerW, h);
            Rgba ink = ExploreStyle.Ink;
            var placed = new List<(RectTransform El, float W, float H)>();
            foreach (BannerPart part in BannerParts)
            {
                switch (part.Role)
                {
                    case "icon":
                    {
                        // .ocbg-explore-banner-icon: display flex around the svg
                        RectTransform span = Ui.Div(BannerContent, "ocbg-explore-banner-icon", 0f, 0f, part.IconSize, part.IconSize);
                        ExploreIcons.Ico(span, part.Icon, part.IconSize, part.IconColor);
                        placed.Add((span, part.IconSize, part.IconSize));
                        break;
                    }
                    case "small":
                    {
                        TextStyle s = ExplorePlateKit.Style(ExplorePlateKit.BannerSmallCss, ink);
                        RectTransform small = Ui.Div(BannerContent, "small");
                        float x = 0f, sh = 0f;
                        if (part.Icon != null)
                        {
                            // .is-note small: display flex, centred, gap 10
                            sh = Math.Max(part.IconSize, s.LineHeightPx);
                            PathGraphic ic = ExploreIcons.Ico(small, part.Icon, part.IconSize, part.IconColor);
                            Ui.Place(ic.rectTransform, 0f, (sh - part.IconSize) / 2f);
                            x = part.IconSize + 10f;
                        }
                        string shown = part.Rich ? Rich(part.Text, s) : part.Text;
                        if (shown.Length > 0)
                        {
                            TMP_Text t = ExplorePlateKit.TxtCap(small, shown, part.Rich ? RichStyle(s) : s, x, 0f, BannerW - x, out float tw, out float th, "text");
                            sh = Math.Max(sh, th);
                            Ui.Place(t.rectTransform, x, (sh - th) / 2f);
                            x += tw;
                        }
                        Ui.Size(small, x, sh);
                        placed.Add((small, x, sh));
                        break;
                    }
                    case "strong":
                    {
                        string css = ExplorePlateKit.BannerStrongCss + (enc ? ExplorePlateKit.BannerEncounterStrongCss : "") + (note ? ExplorePlateKit.BannerNoteStrongCss : "");
                        TextStyle s = ExplorePlateKit.Style(css, ink, note ? TextAlign.Center : (TextAlign?)null);
                        string shown = part.Rich ? Rich(part.Text, s) : part.Text;
                        TMP_Text t = ExplorePlateKit.TxtCap(BannerContent, shown, part.Rich ? RichStyle(s) : s, 0f, 0f, note ? 1120f : BannerW, out float tw, out float th, "strong");
                        placed.Add((t.rectTransform, tw, th));
                        break;
                    }
                    case "em":
                    {
                        TextStyle s = ExplorePlateKit.Style(ExplorePlateKit.BannerEmCss, ink);
                        TMP_Text t = ExplorePlateKit.TxtCap(BannerContent, part.Text, s, 0f, 0f, BannerW, out float tw, out float th, "em");
                        placed.Add((t.rectTransform, tw, th));
                        break;
                    }
                    default:
                    {
                        // <i class="ocbg-explore-banner-rule">: an empty flex item (0 × 0; it still takes a gap)
                        RectTransform i = Ui.Div(BannerContent, "ocbg-explore-banner-rule", 0f, 0f, 0f, 0f);
                        placed.Add((i, 0f, 0f));
                        break;
                    }
                }
            }
            float total = placed.Sum(q => q.H) + gap * Math.Max(0, placed.Count - 1);
            float y = (h - total) / 2f;
            foreach ((RectTransform El, float W, float H) p in placed)
            {
                Ui.Place(p.El, (BannerW - p.W) / 2f, y);
                y += p.H + gap;
            }
        }

        /// <summary>classList.remove('is-on' …); reflow; classList.add('is-on'): the banner's classes, its content, and on.</summary>
        private void BannerOn(bool encounter, bool note)
        {
            if (BannerStates == null) return;
            BannerStates.SetState(".is-on", false);
            BannerIsEncounter = encounter;
            BannerIsNote = note;
            LayOutBanner();
            BannerStates.SetState(".is-on", true);
        }

        /// <summary>classList.remove('is-on'): the banner fades (opacity, transform 420 ms ease-out).</summary>
        private void BannerOff()
        {
            if (BannerStates != null) BannerStates.SetState(".is-on", false);
        }

        // js: scenes/explore/ExploreScene.js:1421
        /// <summary>layerBanner(): the layer's title over the map; hides after dur(1700), resolves at dur(1500).</summary>
        private Promise LayerBanner()
        {
            if (Screen == null) return Promise.Resolved();
            int seq = ++BannerSeq;
            string title = Screen.LayerTitle ?? "";
            int cut = title.IndexOf('　');
            BannerParts.Clear();
            BannerParts.Add(BannerPart.Of("small", cut > 0 ? title.Substring(0, cut) : "", true));
            BannerParts.Add(BannerPart.Of("strong", cut > 0 ? title.Substring(cut + 1) : title, true));
            BannerParts.Add(BannerPart.Of("rule", "", false));
            BannerOn(false, false);
            Timeout(Dur(1700f), () =>
            {
                if (seq == BannerSeq) BannerOff();
            });
            return Delay(Dur(1500f));
        }

        // js: scenes/explore/ExploreScene.js:1444
        /// <summary>
        /// noteBanner(kicker, line, icon): what a step or a card just did, said once over the map (X-15 / X-16): the
        /// training's 刻み, the survey's enemy. It fades by itself (dur(2800)) and never takes a key or a click.
        /// </summary>
        private void NoteBanner(string kicker, string line, string icon = null)
        {
            if (string.IsNullOrEmpty(line)) return;
            int seq = ++BannerSeq;
            BannerParts.Clear();
            BannerParts.Add(BannerPart.Of("small", kicker, true, icon, ExploreStyle.Ink, 26f));
            BannerParts.Add(BannerPart.Of("strong", line, true));
            BannerParts.Add(BannerPart.Of("rule", "", false));
            BannerOn(false, true);
            Timeout(Dur(2800f), () =>
            {
                if (seq == BannerSeq) BannerOff();
            });
        }

        // js: scenes/explore/ExploreScene.js:1459
        /// <summary>encounterBanner(): the fight ahead; the map dims (is-engaging); resolves after dur(1200).</summary>
        private Promise EncounterBanner()
        {
            var done = new PromiseSource();
            MapNode node = Session.State.Map.Node(Session.PendingBattleNode);
            Encounter enc = Session.PendingEncounter;
            int seq = ++BannerSeq;
            // A name shows from 開示度 1 (§6.3), the same rule the node detail follows; an enemy not yet met stays 「？」.
            string NameOfEnemy(string id)
            {
                if (!(Expedition.DisclosureOf(id) >= 1)) return ExplorationText.UnknownEnemy;
                try { return EnemyPages.NameOf(id); }
                catch (Exception) { return ExplorationText.UnknownEnemy; }
            }
            // the map's marks (J-9, enemy_roster_v4 §9): 「 + 」 between the enemies of one fight, 「 → 」 between fights
            string names = enc != null ? EnemyPages.EncounterName(enc.Fights.Select(fight => fight.EnemyIds.Select(NameOfEnemy))) : "";
            bool known = enc != null && enc.EnemyIds.Any(id => Expedition.DisclosureOf(id) >= 1);
            NodeKind kind = node != null ? node.Kind : NodeKind.Battle;
            BannerParts.Clear();
            BannerParts.Add(BannerPart.Of("icon", "", false, kind.ToString(), KindColor(kind), 56f));
            BannerParts.Add(BannerPart.Of("strong", ExplorationText.KindLabel(kind), false));
            if (known) BannerParts.Add(BannerPart.Of("em", names, false));
            if (enc != null && enc.Fights.Count > 1) BannerParts.Add(BannerPart.Of("small", ExploreText.FightCount(enc.Fights.Count), true));
            BannerParts.Add(BannerPart.Of("rule", "", false));
            BannerOn(true, false);
            SetRootClass(".is-engaging", true);
            Timeout(Dur(1200f), () =>
            {
                if (seq == BannerSeq)
                {
                    BannerOff();
                    Timeout(Dur(300f), () =>
                    {
                        if (seq != BannerSeq) return;
                        BannerIsEncounter = false; // classList.remove('is-encounter')
                        LayOutBanner();
                    });
                }
                done.TryResolve();
            });
            return done.Promise;
        }

        // ------------------------------------------------------------------ the flow after a step

        // js: scenes/explore/ExploreScene.js:1495
        /// <summary>proceed(): returns a fought encounter, starts a waiting one, shows pending results, the trace, training (≤ 8 rounds).</summary>
        private async Promise Proceed()
        {
            if (FlowRunning || !Alive) return;
            FlowRunning = true;
            try
            {
                for (int guard = 0; guard < 8 && Alive; guard++)
                {
                    // The encounter is over (here, or in the app's battle scene before this scene was mounted again):
                    // bring it back and show what it gave.
                    RecordedBattle fought = Expedition.Battle;
                    if (fought != null && fought.Over)
                    {
                        await ReturnFlow();
                        continue;
                    }
                    // A battle waits, or an encounter of several fights is part-fought (a reload after its first fight: the
                    // save keeps every finished fight, and the next one starts over — ARCH 「Save by replay」).
                    if (Session.HasPendingBattle && (fought == null || !fought.Over) && !BattleFallback)
                    {
                        await BattleFlow();
                        continue;
                    }
                    // trial: a result card the player has not closed — just shown by returnFlow or an event, or brought back
                    // by a reload (Expedition.PendingResult, closed with CloseResult), its pick included
                    ResultCard pending = Expedition.PendingResult;
                    object pendingKey = pending == null ? null : pending.Kind == ResultCardKind.Battle ? (object)pending : pending.Outcome;
                    if (pending != null && !ReferenceEquals(pendingKey, ShownResult))
                    {
                        ShownResult = pendingKey;
                        if (pending.Kind == ResultCardKind.Battle) await ResultFlow(pending.Back, pending);
                        else await EventResultFlow(pending.Outcome);
                        continue;
                    }
                    // trial: the 受け取った遺産 card the player has not closed (a reload while it was up: X-3)
                    LegacyReceipt taken = Expedition.PendingTaken;
                    if (taken != null && !ReferenceEquals(taken, ShownTaken))
                    {
                        ShownTaken = taken;
                        await TakenFlow(taken);
                        continue;
                    }
                    // The heir stands on a trace not yet decided (a reload while its card was open): the card again.
                    if (PendingTrace == null)
                    {
                        ExploreTraceRef waitingTrace = TraceWaiting();
                        if (waitingTrace != null && waitingTrace.Key != TraceOffered) PendingTrace = waitingTrace;
                    }
                    if (PendingTrace != null)
                    {
                        ExploreTraceRef trace = PendingTrace;
                        PendingTrace = null;
                        await TraceFlow(trace);
                        continue;
                    }
                    // a mark waits on its card: just earned at a rest, or brought back by a reload while the card was up
                    // (trial: Session.TrainingWaiting, put off with 「あとで」)
                    if (PendingTraining || (Session.TrainingWaiting && Session.State.TrainingMarks > 0))
                    {
                        PendingTraining = false;
                        await TrainingFlow();
                        continue;
                    }
                    break;
                }
            }
            finally
            {
                FlowRunning = false;
            }
            if (Alive) Render();
        }

        // js: scenes/explore/ExploreScene.js:1556
        /// <summary>battleFlow(): the encounter banner, then ctx.onBattle (the app replaces the scene); the stand-in card without one.</summary>
        private async Promise BattleFlow()
        {
            if (Ctx.OnBattle == null)
            {
                // No battle scene to play it in: the stand-in card's buttons (dungeon_exploration_v4 §6.8).
                BattleFallback = true;
                Render();
                return;
            }
            Busy = true;
            Render();
            await EncounterBanner();
            InBattle = true;
            try
            {
                Promise fight = Ctx.OnBattle();
                if (fight != null) await fight;
            }
            catch (Exception e)
            {
                Debug.LogError("[explore] the battle failed " + e);
            }
            InBattle = false;
            // The app may have replaced this scene with the battle scene and mounted a new one after it; that one brings
            // the encounter back (proceed → returnFlow).
            if (!Alive) return;
            SetRootClass(".is-engaging", false);
            RecordedBattle battle = Expedition.Battle;
            if (battle == null || !battle.Over)
            {
                // No battle scene played it (Unity: the stand-in card's buttons stay).
                if (battle != null) Expedition.Abandon();
                BattleFallback = true;
                Busy = false;
                Render();
            }
        }

        // js: scenes/explore/ExploreScene.js:1588
        /// <summary>Expedition.Return: the encounter's HP, stamina, mastery and journal go home; then its reward.</summary>
        private async Promise ReturnFlow()
        {
            Busy = true;
            BattleReturn back = Expedition.Return();
            Persist();
            Busy = false;
            SetRootClass(".is-engaging", false);
            Render();
            // trial: the result waits on the expedition (PendingResult) and proceed shows it, the same way as after a
            // reload; without trial the card is shown from here
            if (back != null && !back.Died && Expedition.PendingResult == null) await ResultFlow(back, null);
        }

        // ------------------------------------------------------------------ the interlude (§6.5)
        // The rest on washi where the map was; the next layer (a window onto its backdrop, its line and any warning) in the
        // right column where the node detail was; the carried items stay usable below it.

        /// <summary>lullKey: what the interlude plates show (redrawn only when it changes).</summary>
        private string LullKey = "";

        // js: scenes/explore/ExploreScene.js:2141
        /// <summary>drawInterlude(on): the rest on washi where the map was and the next layer where the detail was.</summary>
        private void DrawInterlude(bool on)
        {
            if (!on)
            {
                if (LullKey.Length > 0)
                {
                    Ui.Clear(Lull);
                    LullKey = "";
                }
                return;
            }
            if (Screen == null) return;
            // a confirmation (この生を終える, or a descent into 瘴気死) is up over the rest: its own modal asks, and the rest's
            // card stays under it (one confirmation, §6.6) — drawn from the interlude card itself when this mount has not
            // drawn it yet (a reload with the confirmation open: X-8)
            if (Screen.Card.Kind != CardKind.Interlude && LullKey.Length > 0) return;
            ScreenCard card = Screen.Card.Kind == CardKind.Interlude ? Screen.Card : ExplorationScreenBuilder.InterludeCard(Session.Raw);
            if (card == null) return;
            string key = string.Join("|", card.Lines) + string.Join("|", card.Buttons.Select(b => b.Label));
            if (key == LullKey) return;
            LullKey = key;
            Ui.Clear(Lull);
            ExplorationState below = Session.TryDescend();
            IconSpec[] icons =
            {
                new IconSpec("heart", ExploreStyle.Crimson),
                new IconSpec("pip", ExploreStyle.Amber),
                new IconSpec("miasma", ExploreStyle.Phosphor),
            };
            var restLines = new List<string>();
            var ahead = new List<string>();
            for (int i = 0; i < card.Lines.Count; i++)
            {
                if (i < 3) restLines.Add(card.Lines[i]);
                else ahead.Add(card.Lines[i]);
            }

            // ---- the rest plate (.ocbg-explore-lull-plate, explore.css:352-356, 367-369) ----
            Rgba washiInk = ExploreStyle.WashiInk;
            RectTransform restPlate = Ui.Div(Lull, "ocbg-explore-lull-plate", 96f, 236f, 1112f, 0f);
            UiNode.Of(restPlate).PointerEvents = PointerEvents.Auto;
            BoxGraphic restBox = BoxGraphic.On(restPlate, ExplorePlateKit.LullPlateCss);
            restBox.raycastTarget = true;
            PlateBoxContent rc = ExplorePlateKit.ContentOf(restPlate);
            var flow = new ExplorePlateFlow(rc.X, rc.Y, rc.W, 18f);
            RectTransform kicker = PlatesKicker(restPlate, "Rest", 28f, washiInk, Screen.LayerTitle, true, null, washiInk, true, out float kh);
            Ui.Place(kicker, rc.X, flow.Take(kh));
            flow.Block(restPlate, card.Title, ExplorePlateKit.Style(ExplorePlateKit.LullH2Css, washiInk), "h2");
            // .ocbg-explore-lull-rest: a column, gap 14, padding 22 0, rules above and below; p: a row (gap 16, centred)
            RectTransform rest = Ui.Div(restPlate, "ocbg-explore-lull-rest");
            TextStyle restP = ExplorePlateKit.Style(ExplorePlateKit.LullRestPCss, washiInk);
            float ry = 1f + 22f;
            for (int i = 0; i < restLines.Count; i++)
            {
                RectTransform p = Ui.Div(rest, "p");
                PathGraphic ic = ExploreIcons.Ico(p, icons[i].Name, 28f, icons[i].Color);
                TMP_Text t = ExplorePlateKit.TxtCap(p, Rich(restLines[i], restP, true), RichStyle(restP), 28f + 16f, 0f, rc.W - 28f - 16f, out float tw, out float th, "text");
                float ph = Math.Max(28f, th);
                Ui.Place(ic.rectTransform, 0f, (ph - 28f) / 2f);
                Ui.Place(t.rectTransform, 28f + 16f, (ph - th) / 2f);
                Ui.Place(p, 0f, ry, rc.W, ph);
                ry += ph + (i < restLines.Count - 1 ? 14f : 0f);
            }
            ry += 22f + 1f;
            Ui.Size(rest, rc.W, ry);
            BoxGraphic.On(rest, ExplorePlateKit.LullRestCss);
            Ui.Place(rest, rc.X, flow.Take(ry));
            // .ocbg-explore-lull-buttons: margin-top 12, right-aligned, gap 16; min-width 260; 次の層へ降りる with its stairs
            RectTransform buttonsRow = Ui.Div(restPlate, "ocbg-explore-lull-buttons");
            var buttons = new List<OcbgButton>();
            var withIcon = new List<bool>();
            for (int i = 0; i < card.Buttons.Count; i++)
            {
                CardButton b = card.Buttons[i];
                bool descend = b.Action == ScreenAction.Descend;
                OcbgButton btn = ExplorePlateKit.Button(buttonsRow, b.Label, i == 0 && descend, false, PlateButtonSkin.Lull, out Action<bool> ring);
                btn.Click += () => OnCardAction(b.Action).Forget();
                string label = b.Label;
                AddFocus(btn, () => label, ring);
                buttons.Add(btn);
                withIcon.Add(descend);
            }
            ExplorePlateKit.RowOfButtons(buttonsRow, buttons, rc.W, false, 0f, 260f, 16f, true,
                k => withIcon[k] ? ExplorePlateKit.IconButtonSize(buttons[k], 24f, 12f) : buttons[k].NaturalSize);
            for (int k = 0; k < buttons.Count; k++)
                if (withIcon[k]) ExplorePlateKit.IconInButton(buttons[k], "stairs", 24f, ExploreStyle.Ink, 12f);
            float rowH = buttonsRow.sizeDelta.y;
            Ui.Place(buttonsRow, rc.X, flow.Take(12f + rowH) + 12f);
            float restH = flow.Y + rc.Bottom;
            UiNode restNode = UiNode.Of(restPlate);
            restNode.H = restH;
            restNode.Y = Math.Max(120f, ExplorePlateKit.JsRound(ExploreStyle.MapCenterY - restH / 2f));
            CssAnimator.Set(restPlate, ExplorePlateKit.LullPlateInAnim);

            // ---- the next layer (.ocbg-explore-lull-next, explore.css:357-366) at DETAIL.x, DETAIL.y, DETAIL.w ----
            Rgba ink = ExploreStyle.Ink;
            RectTransform next = Ui.Div(Lull, "ocbg-explore-lull-next", ExploreStyle.DetailX, ExploreStyle.DetailY, ExploreStyle.DetailW, 0f);
            UiNode.Of(next).PointerEvents = PointerEvents.Auto;
            BoxGraphic nextBox = BoxGraphic.On(next, ExplorePlateKit.LullNextCss);
            nextBox.raycastTarget = true;
            PlateBoxContent nc = ExplorePlateKit.ContentOf(next);
            var nflow = new ExplorePlateFlow(nc.X, nc.Y, nc.W, 16f);
            RectTransform nk = PlatesKicker(next, "stairs", 26f, ink, ExploreText.NextLayer, false, null, ink, false, out float nkh);
            Ui.Place(nk, nc.X, nflow.Take(nkh));
            if (below != null)
            {
                // .ocbg-explore-lull-window: 536 × 302, a 1 px rule, radius 4, clipped; the strip drifts (9 s alternate)
                // under an inset vignette (::after)
                RectTransform win = Ui.Div(next, "ocbg-explore-lull-window", nc.X, nflow.Take(302f), 536f, 302f);
                BoxGraphic.On(win, ExplorePlateKit.LullWindowCss);
                RectTransform clip = Ui.Div(win, "clip", 1f, 1f, 534f, 300f);
                clip.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
                RectTransform strip = ExploreBackdrop.SkyStrip(clip, below.Profile.Layer, 534f, 300f);
                // @media (prefers-reduced-motion: reduce) … .ocbg-explore-lull-window canvas { animation: none; }
                if (strip && !Reduce) CssAnimator.Set(strip, ExplorePlateKit.LullWindowAnim);
                BoxGraphic.Make(clip, ExplorePlateKit.LullWindowAfterCss, 0f, 0f, 534f, 300f, "::after");
            }
            TextStyle warnText = ExplorePlateKit.Style(ExplorePlateKit.WarnCss + ExplorePlateKit.LullNextPCss + ExplorePlateKit.LullNextWarnCss, ink);
            TextStyle nameStyle = ExplorePlateKit.Style(ExplorePlateKit.LullNameCss, ink);
            TextStyle aheadStyle = ExplorePlateKit.Style(ExplorePlateKit.LullNextPCss + ExplorePlateKit.LullAheadCss, ink);
            TextStyle pStyle = ExplorePlateKit.Style(ExplorePlateKit.LullNextPCss, ink);
            foreach (string line in ahead)
            {
                if (line.StartsWith("！ ", StringComparison.Ordinal))
                {
                    // p.ocbg-explore-warn: caution icon 22 (margin-top 1) + the line, padding 8 10, a 3 px caution rule
                    RectTransform warn = Ui.Div(next, "ocbg-explore-warn");
                    PathGraphic ic = ExploreIcons.Ico(warn, "caution", 22f, ExploreStyle.Caution);
                    float tx = 3f + 10f + 22f + 10f;
                    ExplorePlateKit.Txt(warn, line.Substring(2), warnText, tx, 8f, nc.W - tx - 10f, out _, out float th, "span");
                    Ui.Place(ic.rectTransform, 3f + 10f, 8f + 1f);
                    float wh = 8f + Math.Max(1f + 22f, th) + 8f;
                    Ui.Size(warn, nc.W, wh);
                    BoxGraphic.On(warn, ExplorePlateKit.WarnCss);
                    Ui.Place(warn, nc.X, nflow.Take(wh));
                    continue;
                }
                // 「次は第 2 層　■■の秘跡: 1 歩ごとに瘴気 +1%、刻限 10」: the layer as a heading, its numbers under it
                int at = line.IndexOf(": ", StringComparison.Ordinal);
                if (below != null && at > 0)
                {
                    // h3.ocbg-explore-lull-name: margin 2 0 -6, the digits in the display face
                    TMP_Text h3 = ExplorePlateKit.Txt(next, Rich(line.Substring(0, at), nameStyle, false, numsInFace: true), RichStyle(nameStyle), nc.X, 0f, nc.W, out _, out float h3h, "h3");
                    Ui.Place(h3.rectTransform, nc.X, nflow.Take(2f + h3h - 6f) + 2f);
                    nflow.Block(next, Rich(line.Substring(at + 2), aheadStyle), RichStyle(aheadStyle), "ocbg-explore-lull-ahead");
                }
                else nflow.Block(next, Rich(line, pStyle), RichStyle(pStyle), "p");
            }
            UiNode.Of(next).H = nflow.Y + nc.Bottom;
            CssAnimator.Set(next, ExplorePlateKit.LullNextInAnim);
        }
    }

    /// <summary>One button of a plate's button row: its words, whether it is primary, what a click does.</summary>
    internal readonly struct PlateButtonSpec
    {
        public PlateButtonSpec(string label, bool primary, Action click)
        {
            Label = label ?? "";
            Primary = primary;
            Click = click;
        }

        public string Label { get; }
        public bool Primary { get; }
        public Action Click { get; }
    }

    /// <summary>Which stylesheet rules an .ocbg-btn on a card takes: none, the washi plate's, or the interlude plate's.</summary>
    internal enum PlateButtonSkin
    {
        Plain,
        Washi,
        Lull,
    }

    /// <summary>A plate's content box in its own px (box-sizing: border-box): left, top, width, and the padding + border below.</summary>
    internal readonly struct PlateBoxContent
    {
        public PlateBoxContent(float x, float y, float w, float bottom)
        {
            X = x;
            Y = y;
            W = w;
            Bottom = bottom;
        }

        public float X { get; }
        public float Y { get; }
        public float W { get; }
        public float Bottom { get; }
    }

    /// <summary>
    /// A flex column's cursor (flex-direction: column; gap): Take(h) gives the next item's top (after the gap) and moves
    /// past it; Y is the bottom of the last item. Block lays a text block (wrapped at the column's width) as the next item.
    /// </summary>
    internal sealed class ExplorePlateFlow
    {
        private bool _any;

        public ExplorePlateFlow(float x, float y, float w, float gap)
        {
            X = x;
            Y = y;
            W = w;
            Gap = gap;
        }

        public float X { get; }
        public float W { get; }
        public float Gap { get; }
        public float Y { get; private set; }

        /// <summary>The top of the next item (its margin box h tall) and the cursor past it.</summary>
        public float Take(float h)
        {
            if (_any) Y += Gap;
            _any = true;
            float at = Y;
            Y += h;
            return at;
        }

        /// <summary>A text block as the next item, as wide as the column (its text wraps there). Returns its height.</summary>
        public float Block(RectTransform parent, string shown, TextStyle style, string name)
        {
            TMP_Text t = ExplorePlateKit.Txt(parent, shown, style, X, 0f, W, out _, out float h, name);
            Ui.Place(t.rectTransform, X, Take(h));
            return h;
        }
    }

    /// <summary>
    /// The plates' CSS (explore.css, verbatim; the rule's line in the comment) and the small builders the plates share:
    /// text blocks, kbd, buttons with the plate skins, button rows, the interactive boxes (choice tiles, swap slots, survey
    /// rows). Engine objects only through the kit; no state.
    /// </summary>
    internal static class ExplorePlateKit
    {
        // ---- cards over the map (explore.css:218-255) ----

        /// <summary>explore.css:219 .ocbg-explore-dim.</summary>
        public const string DimCss = "position: absolute; inset: 0; pointer-events: auto; background: color-mix(in srgb, var(--ground) 64%, transparent); animation: ocbg-explore-fade 260ms ease-out both;";
        public const string DimInAnim = "ocbg-explore-fade 260ms ease-out both";
        /// <summary>explore.css:220 .ocbg-explore-dim.is-leaving.</summary>
        public const string DimOutAnim = "ocbg-explore-fadeout 220ms ease-in both";
        /// <summary>explore.css:223 .ocbg-explore-plate.</summary>
        public const string PlateCss = "pointer-events: auto; display: flex; flex-direction: column; gap: 14px; padding: 36px 44px 32px; border-radius: var(--radius-panel); box-shadow: var(--shadow-tip); animation: ocbg-explore-plate-in 300ms cubic-bezier(.22,.61,.36,1) both;";
        public const string PlateInAnim = "ocbg-explore-plate-in 300ms cubic-bezier(.22,.61,.36,1) both";
        /// <summary>explore.css:224 .ocbg-explore-plate.is-leaving.</summary>
        public const string PlateOutAnim = "ocbg-explore-plate-out 220ms ease-in both";
        /// <summary>explore.css:227 .ocbg-explore-plate h2.</summary>
        public const string PlateH2Css = "margin: 0; font: 800 44px/54px var(--font-display); letter-spacing: .06em;";
        /// <summary>explore.css:228 .ocbg-explore-plate-lines p.</summary>
        public const string PlateLinesPCss = "margin: 0 0 6px; font: 400 22px/34px var(--font-body);";
        /// <summary>explore.css:229 .ocbg-explore-plate-kicker.</summary>
        public const string PlateKickerCss = "display: flex; align-items: center; gap: 10px; font: 700 18px/24px var(--font-display); letter-spacing: .08em;";
        /// <summary>explore.css:230 .ocbg-explore-plate-buttons.</summary>
        public const string PlateButtonsCss = "margin-top: auto; padding-top: 10px; display: flex; align-items: center; justify-content: flex-end; gap: 16px;";
        /// <summary>explore.css:231 .ocbg-explore-plate-buttons .ocbg-btn.</summary>
        public const string PlateButtonsBtnCss = "min-width: 220px; display: inline-flex; align-items: center; justify-content: center; gap: 10px;";
        /// <summary>explore.css:233-234 .ocbg-explore-plate.is-washi.</summary>
        public const string WashiCss = "background: var(--washi); color: var(--washi-ink); border: 1px solid color-mix(in srgb, var(--washi-ink) 22%, transparent); box-shadow: inset 0 0 90px color-mix(in srgb, var(--washi-ink) 14%, transparent), var(--shadow-tip);";
        /// <summary>explore.css:235 .is-washi .ocbg-btn.</summary>
        public const string WashiBtnCss = "background: var(--panel-solid);";
        /// <summary>explore.css:236 .is-washi .ocbg-btn.is-primary.</summary>
        public const string WashiBtnPrimaryCss = "background: color-mix(in srgb, var(--amber) 24%, var(--panel-solid));";
        /// <summary>explore.css:237 .is-washi .ocbg-btn.is-primary:hover.</summary>
        public const string WashiBtnPrimaryHoverCss = "background: color-mix(in srgb, var(--amber) 36%, var(--panel-solid));";
        /// <summary>explore.css:56 .is-washi .ocbg-btn:focus-visible, .ocbg-explore-lull-plate .ocbg-btn:focus-visible.</summary>
        public const string WashiBtnFocusCss = "outline: 3px solid var(--washi-ink); outline-offset: 3px;";
        /// <summary>explore.css:238 .ocbg-explore-plate.is-break h2.</summary>
        public const string BreakH2Css = "font-size: 52px; line-height: 64px;";
        /// <summary>explore.css:239 .ocbg-explore-plate.is-break .ocbg-explore-plate-lines.</summary>
        public const string BreakLinesCss = "padding-top: 12px; border-top: 1px solid color-mix(in srgb, var(--washi-ink) 25%, transparent);";
        /// <summary>explore.css:241 .ocbg-explore-plate.is-solid, .ocbg-explore-plate.is-end.</summary>
        public const string SolidCss = "background: var(--panel-solid); border: 1px solid var(--steel);";
        /// <summary>explore.css:242-243 .ocbg-explore-plate.is-end.</summary>
        public const string EndCss = "align-items: center; text-align: center; padding-top: 44px; background: radial-gradient(80% 70% at 50% 0%, color-mix(in srgb, var(--violet) 14%, transparent), transparent 70%), var(--panel-solid);";
        /// <summary>explore.css:244 .ocbg-explore-plate.is-end h2.</summary>
        public const string EndH2Css = "font-size: 64px; line-height: 76px; letter-spacing: .2em;";
        /// <summary>explore.css:245 .ocbg-explore-plate.is-end .ocbg-explore-plate-lines.</summary>
        public const string EndLinesCss = "padding: 14px 40px 6px; border-top: 1px solid var(--rule);";
        /// <summary>explore.css:246 .ocbg-explore-plate.is-end .ocbg-explore-plate-buttons.</summary>
        public const string EndButtonsCss = "padding-top: 22px;";
        /// <summary>explore.css:247 .ocbg-explore-plate.is-break .ocbg-explore-plate-buttons.</summary>
        public const string BreakButtonsCss = "padding-top: 26px;";
        /// <summary>explore.css:248 .ocbg-explore-plate.is-end .ocbg-explore-plate-buttons.</summary>
        public const string EndButtonsJustifyCss = "justify-content: center;";
        /// <summary>explore.css:249-250 .ocbg-explore-plate.is-survived, .ocbg-explore-plate.is-completed.</summary>
        public const string SurvivedCss = "background: radial-gradient(80% 70% at 50% 0%, color-mix(in srgb, var(--amber) 12%, transparent), transparent 70%), var(--panel-solid); border-color: color-mix(in srgb, var(--amber) 50%, var(--steel));";
        /// <summary>explore.css:251 .ocbg-explore-plate.is-fallen.</summary>
        public const string FallenCss = "border-top: 3px solid var(--crimson);";
        /// <summary>explore.css:252 .ocbg-explore-end-mark.</summary>
        public const string EndMarkCss = "width: 76px; height: 76px; display: flex; align-items: center; justify-content: center; border: 1px solid var(--rule); border-radius: 50%;";
        /// <summary>explore.css:260 .ocbg-explore-plate.is-result .ocbg-explore-plate-buttons.</summary>
        public const string ResultButtonsCss = "padding-top: 18px;";

        // ---- trial choice cards (explore.css:404-433) ----

        /// <summary>explore.css:405 .ocbg-explore-kicker-sub.</summary>
        public const string KickerSubCss = "margin-left: 10px; padding-left: 12px; border-left: 1px solid color-mix(in srgb, var(--washi-ink) 30%, transparent); font-weight: 400;";
        /// <summary>explore.css:406 .ocbg-explore-choice-lead.</summary>
        public const string ChoiceLeadCss = "margin: 0; font: 700 24px/34px var(--font-body);";
        /// <summary>explore.css:407 .ocbg-explore-plate-lines.is-list.</summary>
        public const string ListLinesCss = "padding: 8px 0 4px; border-top: 1px solid color-mix(in srgb, var(--washi-ink) 22%, transparent);";
        /// <summary>explore.css:408 .ocbg-explore-plate-lines.is-list p.</summary>
        public const string ListLinesPCss = "margin: 0; font-size: 20px; line-height: 32px;";
        /// <summary>explore.css:409 .ocbg-explore-choice-tiles.</summary>
        public const string ChoiceTilesCss = "display: grid; grid-template-columns: 1fr 1fr; gap: 20px; padding-top: 6px;";
        /// <summary>explore.css:410 .ocbg-explore-choice-tile.</summary>
        public const string ChoiceTileCss = "position: relative; display: flex; flex-direction: column; align-items: flex-start; gap: 10px; min-height: 150px; padding: 20px 24px 18px 64px; text-align: left; color: var(--ink); background: var(--panel-solid); border: 2px solid var(--steel); border-radius: var(--radius-panel); cursor: pointer; transition: transform 90ms, border-color 120ms, background-color 120ms;";
        /// <summary>explore.css:411 .ocbg-explore-choice-tile.is-primary.</summary>
        public const string ChoiceTilePrimaryCss = "border-color: var(--amber);";
        /// <summary>explore.css:412 .ocbg-explore-choice-tile:hover.</summary>
        public const string ChoiceTileHoverCss = "transform: translateY(-4px); background: color-mix(in srgb, var(--amber) 12%, var(--panel-solid));";
        /// <summary>explore.css:413 .ocbg-explore-choice-tile kbd.</summary>
        public const string ChoiceTileKbdCss = "position: absolute; left: 20px; top: 22px;";
        /// <summary>explore.css:414 .ocbg-explore-choice-tile strong.</summary>
        public const string ChoiceTileStrongCss = "font: 800 32px/40px var(--font-display); letter-spacing: .08em;";
        /// <summary>explore.css:415 .ocbg-explore-choice-note.</summary>
        public const string ChoiceNoteCss = "display: flex; flex-direction: column; gap: 4px; font: 400 18px/26px var(--font-body);";
        /// <summary>explore.css:416 .ocbg-explore-choice-note &gt; span.</summary>
        public const string ChoiceNoteSpanCss = "display: inline-flex; align-items: center; gap: 8px;";
        /// <summary>explore.css:417 .ocbg-explore-swap-body.</summary>
        public const string SwapBodyCss = "display: grid; grid-template-columns: 300px 1fr; gap: 28px; align-items: stretch; padding-top: 6px;";
        /// <summary>explore.css:418 .ocbg-explore-swap-label.</summary>
        public const string SwapLabelCss = "font: 700 16px/22px var(--font-display); letter-spacing: .08em;";
        /// <summary>explore.css:419 .ocbg-explore-swap-found.</summary>
        public const string SwapFoundCss = "display: flex; flex-direction: column; align-items: center; gap: 10px; padding: 18px 18px 20px; text-align: center; border: 2px solid var(--amber); border-radius: var(--radius-panel); background: color-mix(in srgb, var(--amber) 10%, transparent);";
        /// <summary>explore.css:420 .ocbg-explore-swap-found strong.</summary>
        public const string SwapFoundStrongCss = "font: 800 30px/38px var(--font-display);";
        /// <summary>explore.css:421 .ocbg-explore-swap-found .ocbg-explore-choice-note.</summary>
        public const string SwapFoundNoteCss = "font-size: 17px; line-height: 24px;";
        /// <summary>explore.css:422 .ocbg-explore-swap-side.</summary>
        public const string SwapSideCss = "display: flex; flex-direction: column; gap: 10px;";
        /// <summary>explore.css:423 .ocbg-explore-swap-slots.</summary>
        public const string SwapSlotsCss = "display: grid; grid-template-columns: repeat(3, 1fr); gap: 14px; flex: 1;";
        /// <summary>explore.css:424 .ocbg-explore-swap-slot.</summary>
        public const string SwapSlotCss = "position: relative; display: flex; flex-direction: column; align-items: center; gap: 8px; padding: 16px 12px 14px; text-align: center; color: var(--ink); background: var(--panel-solid); border: 2px solid var(--steel); border-radius: var(--radius-panel); cursor: pointer; transition: transform 90ms, border-color 120ms;";
        /// <summary>explore.css:425 .ocbg-explore-swap-slot:hover.</summary>
        public const string SwapSlotHoverCss = "transform: translateY(-4px); border-color: var(--amber);";
        /// <summary>explore.css:426 .ocbg-explore-swap-slot kbd.</summary>
        public const string SwapSlotKbdCss = "position: absolute; left: 10px; top: 10px;";
        /// <summary>explore.css:427 .ocbg-explore-swap-slot strong.</summary>
        public const string SwapSlotStrongCss = "font: 700 22px/28px var(--font-body);";
        /// <summary>explore.css:428 .ocbg-explore-swap-slot .ocbg-explore-choice-note.</summary>
        public const string SwapSlotNoteCss = "font-size: 15px; line-height: 21px;";
        /// <summary>explore.css:429 .ocbg-explore-swap-go.</summary>
        public const string SwapGoCss = "margin-top: auto; padding: 4px 14px; font: 700 16px/22px var(--font-body); border: 1px solid var(--amber); border-radius: var(--radius-chip-self);";
        /// <summary>explore.css:430 .ocbg-explore-survey-list.</summary>
        public const string SurveyListCss = "display: flex; flex-direction: column; gap: 10px; padding-top: 4px;";
        /// <summary>explore.css:431 .ocbg-explore-survey-row.</summary>
        public const string SurveyRowCss = "display: flex; align-items: center; gap: 14px; padding: 14px 18px; text-align: left; color: var(--ink); background: var(--panel-solid); border: 2px solid var(--steel); border-radius: var(--radius-panel); cursor: pointer; font: 400 22px/28px var(--font-body); transition: transform 90ms, border-color 120ms;";
        /// <summary>explore.css:432 .ocbg-explore-survey-row:hover.</summary>
        public const string SurveyRowHoverCss = "border-color: var(--amber); transform: translateX(4px);";
        /// <summary>explore.css:433 .ocbg-explore-survey-row strong.</summary>
        public const string SurveyRowStrongCss = "font-weight: 700;";

        /// <summary>
        /// The browser's own :focus-visible ring on a button the stylesheet gives none (the choice tiles, swap slots and survey
        /// rows): Chrome's `outline: auto 1px -webkit-focus-ring-color`, drawn here as a 2 px dark ring just outside.
        /// </summary>
        public const string UaFocusCss = "outline: 2px solid #101010; outline-offset: 1px;";

        // ---- banner (explore.css:115-131) ----

        /// <summary>explore.css:115 .ocbg-explore-banner: its place (left 24, width 1256, top 392, height 200, z 4; laid out by LayOutBanner).</summary>
        public const string BannerPlaceCss = "position: absolute; left: 24px; width: 1256px; top: 392px; height: 200px; z-index: 4;";
        /// <summary>explore.css:115-116 .ocbg-explore-banner: the rest of the rule.</summary>
        public const string BannerCss = "display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px; pointer-events: none; opacity: 0; transform: translateY(8px); transition: opacity 420ms ease-out, transform 420ms ease-out; background: linear-gradient(90deg, transparent, color-mix(in srgb, var(--ground) 92%, transparent) 18%, color-mix(in srgb, var(--ground) 92%, transparent) 82%, transparent);";
        /// <summary>explore.css:117 .ocbg-explore-banner.is-encounter.</summary>
        public const string BannerEncounterCss = "top: 362px; height: 260px; gap: 6px;";
        /// <summary>explore.css:118 .ocbg-explore-banner.is-on.</summary>
        public const string BannerOnCss = "opacity: 1; transform: none;";
        /// <summary>explore.css:119-121 .ocbg-explore-banner::before / ::after (top 10 / bottom 10).</summary>
        public const string BannerRuleCss = "content: \"\"; position: absolute; left: 24%; right: 24%; height: 2px; background: var(--amber); opacity: .7;";
        /// <summary>explore.css:122 .ocbg-explore-banner.is-encounter::before / ::after.</summary>
        public const string BannerEncounterRuleCss = "background: var(--violet);";
        /// <summary>explore.css:123 .ocbg-explore-banner small.</summary>
        public const string BannerSmallCss = "font: 700 26px/32px var(--font-display); letter-spacing: .2em;";
        /// <summary>explore.css:124 .ocbg-explore-banner strong.</summary>
        public const string BannerStrongCss = "font: 800 64px/76px var(--font-display); letter-spacing: .14em;";
        /// <summary>explore.css:125 .ocbg-explore-banner em.</summary>
        public const string BannerEmCss = "font: 400 24px/32px var(--font-body); font-style: normal; letter-spacing: .04em;";
        /// <summary>explore.css:126 .ocbg-explore-banner.is-encounter strong.</summary>
        public const string BannerEncounterStrongCss = "font-size: 56px; line-height: 64px;";
        /// <summary>explore.css:128 .ocbg-explore-banner.is-note.</summary>
        public const string BannerNoteCss = "top: 412px; height: 160px;";
        /// <summary>explore.css:129 .ocbg-explore-banner.is-note small.</summary>
        public const string BannerNoteSmallCss = "display: flex; align-items: center; gap: 10px;";
        /// <summary>explore.css:130 .ocbg-explore-banner.is-note strong.</summary>
        public const string BannerNoteStrongCss = "max-width: 1120px; font-size: 40px; line-height: 52px; letter-spacing: .06em; text-align: center;";

        // ---- the interlude (explore.css:352-369) and the warning line (:162-163) ----

        /// <summary>explore.css:352-353 .ocbg-explore-lull-plate (its place: left 96, top 236, width 1112).</summary>
        public const string LullPlateCss = "position: absolute; left: 96px; top: 236px; width: 1112px; pointer-events: auto; display: flex; flex-direction: column; gap: 18px; padding: 44px 56px 40px; color: var(--washi-ink); background: var(--washi); border: 1px solid color-mix(in srgb, var(--washi-ink) 22%, transparent); border-radius: var(--radius-panel); box-shadow: inset 0 0 110px color-mix(in srgb, var(--washi-ink) 14%, transparent), var(--shadow-tip); animation: ocbg-explore-plate-in 600ms cubic-bezier(.22,.61,.36,1) both;";
        public const string LullPlateInAnim = "ocbg-explore-plate-in 600ms cubic-bezier(.22,.61,.36,1) both";
        /// <summary>explore.css:354 .ocbg-explore-lull-plate h2.</summary>
        public const string LullH2Css = "margin: 0; font: 800 60px/72px var(--font-display); letter-spacing: .16em;";
        /// <summary>explore.css:355 .ocbg-explore-lull-rest.</summary>
        public const string LullRestCss = "display: flex; flex-direction: column; gap: 14px; padding: 22px 0; border-top: 1px solid color-mix(in srgb, var(--washi-ink) 25%, transparent); border-bottom: 1px solid color-mix(in srgb, var(--washi-ink) 25%, transparent);";
        /// <summary>explore.css:356 .ocbg-explore-lull-rest p.</summary>
        public const string LullRestPCss = "margin: 0; display: flex; align-items: center; gap: 16px; font: 400 26px/36px var(--font-body);";
        /// <summary>explore.css:357 .ocbg-explore-lull-next.</summary>
        public const string LullNextCss = "pointer-events: auto; display: flex; flex-direction: column; gap: 16px; padding: 24px 28px; color: var(--ink); background: var(--panel); border: 1px solid var(--rule); border-radius: var(--radius-panel); animation: ocbg-explore-plate-in 700ms 120ms cubic-bezier(.22,.61,.36,1) both;";
        public const string LullNextInAnim = "ocbg-explore-plate-in 700ms 120ms cubic-bezier(.22,.61,.36,1) both";
        /// <summary>explore.css:358 .ocbg-explore-lull-window.</summary>
        public const string LullWindowCss = "position: relative; width: 536px; height: 302px; border: 1px solid var(--rule); border-radius: var(--radius-tag); overflow: hidden;";
        /// <summary>explore.css:359 .ocbg-explore-lull-window canvas.</summary>
        public const string LullWindowAnim = "ocbg-explore-window 9s ease-in-out infinite alternate";
        /// <summary>explore.css:360 .ocbg-explore-lull-window::after.</summary>
        public const string LullWindowAfterCss = "content: \"\"; position: absolute; inset: 0; box-shadow: inset 0 0 40px color-mix(in srgb, var(--ground) 70%, transparent); pointer-events: none;";
        /// <summary>explore.css:362 .ocbg-explore-lull-next p.</summary>
        public const string LullNextPCss = "margin: 0; font: 400 20px/30px var(--font-body);";
        /// <summary>explore.css:363 .ocbg-explore-lull-next h3.ocbg-explore-lull-name (:364 its digits stay in the display face).</summary>
        public const string LullNameCss = "margin: 2px 0 -6px; font: 700 32px/42px var(--font-display); letter-spacing: .06em;";
        /// <summary>explore.css:365 .ocbg-explore-lull-next p.ocbg-explore-lull-ahead.</summary>
        public const string LullAheadCss = "font: 400 22px/34px var(--font-body);";
        /// <summary>explore.css:366 .ocbg-explore-lull-next .ocbg-explore-warn.</summary>
        public const string LullNextWarnCss = "font-size: 18px; line-height: 26px;";
        /// <summary>explore.css:367 .ocbg-explore-lull-buttons.</summary>
        public const string LullButtonsCss = "margin-top: 12px; display: flex; justify-content: flex-end; gap: 16px;";
        /// <summary>explore.css:368 .ocbg-explore-lull-buttons .ocbg-btn.</summary>
        public const string LullBtnCss = "min-width: 260px; display: inline-flex; align-items: center; justify-content: center; gap: 12px; background: var(--panel-solid);";
        /// <summary>explore.css:369 .ocbg-explore-lull-buttons .ocbg-btn.is-primary.</summary>
        public const string LullBtnPrimaryCss = "background: color-mix(in srgb, var(--amber) 24%, var(--panel-solid));";
        /// <summary>explore.css:162 .ocbg-explore-warn.</summary>
        public const string WarnCss = "display: flex; align-items: flex-start; gap: 10px; margin: 2px 0 0; padding: 8px 10px; font: 400 17px/24px var(--font-body); background: color-mix(in srgb, var(--caution) 10%, transparent); border-left: 3px solid var(--caution); border-radius: 0 var(--radius-tag) var(--radius-tag) 0;";

        /// <summary>The room given to a one-line box beyond its measured width, so TMP never wraps its last glyph.</summary>
        public const float Slack = 1f;

        // ---- geometry ----

        /// <summary>Math.round (JS): halves round up.</summary>
        public static float JsRound(float v) => (float)Math.Floor(v + 0.5);

        /// <summary>
        /// The plate's box CSS for its classes, in cascade order (explore.css:223 .ocbg-explore-plate, :233 .is-washi, :241
        /// .is-solid / .is-end, :242 .is-end, :249 .is-survived / .is-completed, :251 .is-fallen).
        /// </summary>
        public static string PlateBoxCss(ICollection<string> cls)
        {
            var sb = new System.Text.StringBuilder(PlateCss);
            if (cls.Contains("is-washi")) sb.Append(WashiCss);
            if (cls.Contains("is-solid") || cls.Contains("is-end")) sb.Append(SolidCss);
            if (cls.Contains("is-end")) sb.Append(EndCss);
            if (cls.Contains("is-survived") || cls.Contains("is-completed")) sb.Append(SurvivedCss);
            if (cls.Contains("is-fallen")) sb.Append(FallenCss);
            return sb.ToString();
        }

        /// <summary>.ocbg-explore-plate-buttons' padding-top for the plate's classes (:230 10, :246 .is-end 22, :247 .is-break 26, :260 .is-result 18).</summary>
        public static float ButtonsPadTop(ICollection<string> cls)
        {
            if (cls.Contains("is-result")) return 18f;
            if (cls.Contains("is-break")) return 26f;
            if (cls.Contains("is-end")) return 22f;
            return 10f;
        }

        /// <summary>An element's content box (its BoxGraphic's border and padding inside its UiNode box; the whole box without one).</summary>
        public static PlateBoxContent ContentOf(RectTransform el)
        {
            if (!el) return new PlateBoxContent(0f, 0f, 0f, 0f);
            UiNode n = UiNode.Of(el);
            BoxGraphic g = el.GetComponent<BoxGraphic>();
            if (!g) return new PlateBoxContent(0f, 0f, n.W, 0f);
            Insets bw = g.Style.BorderWidths, pad = g.Style.Padding;
            return new PlateBoxContent(bw.Left + pad.Left, bw.Top + pad.Top, n.W - bw.Horizontal - pad.Horizontal, bw.Bottom + pad.Bottom);
        }

        /// <summary>An auto-height box's height from what it holds: the lowest active child's bottom, then the padding and the border.</summary>
        public static float MeasureHeight(RectTransform el)
        {
            float bottom = 0f;
            for (int i = 0; i < el.childCount; i++)
            {
                var c = el.GetChild(i) as RectTransform;
                if (c == null || !c.gameObject.activeSelf) continue;
                bottom = Math.Max(bottom, Ui.PosOf(c).y + c.rect.height);
            }
            return bottom + ContentOf(el).Bottom;
        }

        // ---- text ----

        /// <summary>A text style from CSS declarations in a colour (the colour the element inherits), aligned when given.</summary>
        public static TextStyle Style(string css, Rgba color, TextAlign? align = null)
        {
            TextStyle s = TextStyle.FromCss(css).WithColor(color);
            if (align.HasValue) s.Align = align.Value;
            return s;
        }

        /// <summary>
        /// A text box at (x, y) of parent: w null = one line at its max-content width; else wrapped at w. bw / bh = the
        /// box's width (max-content, or w) and height (lines × line height, plus MonoLineGrowth for each line that holds
        /// one of rich()'s digit runs; 0 when empty).
        /// </summary>
        public static TMP_Text Txt(RectTransform parent, string shown, TextStyle style, float x, float y, float? w, out float bw, out float bh, string name = "text")
        {
            string s = shown ?? "";
            bool empty = s.Length == 0;
            bw = w ?? (empty ? 0f : TextMeasure.Width(s, style));
            float boxW = w ?? (empty ? 0f : bw + Slack);
            bh = empty ? 0f : TextMeasure.Height(s, style, Math.Max(1f, boxW));
            TMP_Text t = Label.Make(parent, s, style, x, y, boxW, bh, name);
            float grow = empty ? 0f : MonoLineGrowth(style, s);
            if (grow > 0f)
            {
                int lines = Math.Max(1, (int)Math.Round(bh / style.LineHeightPx));
                bh += grow * MonoLines(t, lines);
                Ui.Size(t.rectTransform, boxW, bh);
            }
            return t;
        }

        /// <summary>
        /// What a digit run of rich() (.ocbg-explore-num: IBM Plex Mono 600 at the line's size and line-height) adds to a
        /// line of `style` in Chrome's inline layout, 0 when the text holds none. Every inline box has its face's ascent and
        /// descent rounded to px, the half-leading floor((L − (A + D)) / 2) above and the rest below; the line box spans
        /// the highest top and the lowest bottom. Plex (1.025 / 0.275 em) sits lower in the line than Shippori Mincho and
        /// Zen Kaku (1.160 / 0.288 em), so such a line is taller at its bottom and its baseline stays: 22/34 → 36, 26/36 →
        /// 37, 20/32 → 33, 22/28 → 30, 18/24 → 26, as measured in the reference shots (741, 743, 820, 720, 842). TMP keeps
        /// the line pitch at L (Plex is shorter both ways), so only the box grows.
        /// </summary>
        public static float MonoLineGrowth(TextStyle style, string shown)
        {
            if (style == null || !style.RichText || style.Family == Family.Mono || float.IsNaN(style.LineHeight)) return 0f;
            if (string.IsNullOrEmpty(shown) || shown.IndexOf(MonoTag, StringComparison.Ordinal) < 0) return 0f;
            float size = style.Size, lh = style.LineHeightPx;
            (float Top, float Bottom) Box(FontMetrics f)
            {
                float a = (float)Math.Floor(f.AscentEm * size + 0.5f), d = (float)Math.Floor(f.DescentEm * size + 0.5f);
                float lead = lh - (a + d);
                float above = (float)Math.Floor(lead / 2f);
                return (a + above, d + lead - above);
            }
            (float Top, float Bottom) b = Box(TextMath.Metrics(style.Family, style.Weight));
            (float Top, float Bottom) m = Box(TextMath.Metrics(Family.Mono, 600));
            return Math.Max(0f, m.Top - b.Top) + Math.Max(0f, m.Bottom - b.Bottom);
        }

        /// <summary>The tag rich() opens a digit run with (State: AppendNum).</summary>
        private static string MonoTag => "<font=\"" + TrialFonts.Tag(Family.Mono, 600) + "\">";

        /// <summary>
        /// How many of a label's `lines` lines hold a glyph of the number face: 1 for one line; for a wrapped text, counted
        /// from TMP's layout (each line's characters), 1 when TMP has not laid it out (an inactive canvas).
        /// </summary>
        private static int MonoLines(TMP_Text t, int lines)
        {
            if (lines <= 1 || !t) return 1;
            TMP_TextInfo ti = t.textInfo;
            TMP_FontAsset mono = TrialFonts.Get(Family.Mono, 600);
            if (ti == null || ti.lineCount == 0 || !mono) return 1;
            int n = 0;
            for (int li = 0; li < ti.lineCount && li < ti.lineInfo.Length; li++)
            {
                TMP_LineInfo line = ti.lineInfo[li];
                for (int c = line.firstCharacterIndex; c <= line.lastCharacterIndex && c < ti.characterCount; c++)
                {
                    if (ti.characterInfo[c].fontAsset != mono) continue;
                    n++;
                    break;
                }
            }
            return Math.Max(1, n);
        }

        /// <summary>A shrink-to-fit text box: one line at its max-content width when that fits in cap, else wrapped at cap.</summary>
        public static TMP_Text TxtCap(RectTransform parent, string shown, TextStyle style, float x, float y, float cap, out float bw, out float bh, string name = "text")
        {
            string s = shown ?? "";
            float mw = s.Length == 0 ? 0f : TextMeasure.Width(s, style);
            if (mw + Slack <= cap) return Txt(parent, s, style, x, y, null, out bw, out bh, name);
            return Txt(parent, s, style, x, y, cap, out bw, out bh, name);
        }

        /// <summary>The scene's kbd (explore.css:11) at (x, y) of parent: min 24 × 24, padding 0 6, 600 16/1 mono, centred.</summary>
        public static RectTransform Kbd(RectTransform parent, string key, float x, float y)
        {
            TextStyle ts = TextStyle.FromCss(ExploreStyle.KbdCss);
            ts.Align = TextAlign.Center;
            ts.WhiteSpace = WhiteSpace.NoWrap;
            float w = Math.Max(24f, TextMeasure.Width(key, ts) + 12f + 2f);
            BoxGraphic box = BoxGraphic.Make(parent, ExploreStyle.KbdCss, x, y, w, 24f, "kbd");
            Label.Make(box.rectTransform, key, ts, 1f, 1f + (22f - ts.LineHeightPx) / 2f, w - 2f, ts.LineHeightPx, "key");
            return box.rectTransform;
        }

        // ---- buttons ----

        /// <summary>
        /// An .ocbg-btn with a plate's skin: on a washi plate (explore.css:235-237: panel-solid; primary amber 24 %, 36 %
        /// hovered) or on the interlude plate (:368-369: min-width 260, panel-solid, primary amber 24 %), whose Tab ring is
        /// 3 px washi-ink at 3 px (:56). ring = the button's Tab ring (null: the button's own 2 px wick ring).
        /// </summary>
        public static OcbgButton Button(RectTransform parent, string text, bool primary, bool danger, PlateButtonSkin skin, out Action<bool> ring, string name = "ocbg-btn")
        {
            OcbgButton b = OcbgButton.Make(parent, text, primary, danger, name);
            ring = null;
            switch (skin)
            {
                case PlateButtonSkin.Washi:
                    b.States.State(".is-washi .ocbg-btn", WashiBtnCss, true)
                        .State(".is-washi .ocbg-btn.is-primary", WashiBtnPrimaryCss, primary)
                        .State(".is-washi .ocbg-btn.is-primary:hover", WashiBtnPrimaryHoverCss, false)
                        .State(".is-washi .ocbg-btn:focus-visible", WashiBtnFocusCss, false);
                    b.Area.HoverChanged += on =>
                    {
                        if (b.Root) b.States.SetState(".is-washi .ocbg-btn.is-primary:hover", primary && on);
                    };
                    ring = on =>
                    {
                        if (b.Root) b.States.SetState(".is-washi .ocbg-btn:focus-visible", on);
                    };
                    break;
                case PlateButtonSkin.Lull:
                    b.States.State(".ocbg-explore-lull-buttons .ocbg-btn", LullBtnCss, true)
                        .State(".ocbg-explore-lull-buttons .ocbg-btn.is-primary", LullBtnPrimaryCss, primary)
                        .State(".ocbg-explore-lull-plate .ocbg-btn:focus-visible", WashiBtnFocusCss, false);
                    ring = on =>
                    {
                        if (b.Root) b.States.SetState(".ocbg-explore-lull-plate .ocbg-btn:focus-visible", on);
                    };
                    break;
            }
            b.States.Apply(instant: true);
            b.Layout();
            return b;
        }

        /// <summary>
        /// Lays out a row of buttons (flex, gap) and sizes the row: each at least minW wide; stretch = align-items: stretch
        /// (every button as tall as the tallest), else centred; right-aligned in `avail`, or (center) the row as wide as the
        /// buttons. natural(i) = the i-th button's max-content size (default NaturalSize).
        /// </summary>
        public static Vector2 RowOfButtons(RectTransform row, IList<OcbgButton> buttons, float avail, bool center, float padTop, float minW, float gap, bool stretch,
            Func<int, Vector2> natural = null)
        {
            int n = buttons.Count;
            var ws = new float[n];
            var hs = new float[n];
            float maxH = 0f, total = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 s = natural != null ? natural(i) : buttons[i].NaturalSize;
                ws[i] = Math.Max(minW, s.x);
                hs[i] = s.y;
                maxH = Math.Max(maxH, s.y);
                total += ws[i];
            }
            total += gap * Math.Max(0, n - 1);
            float rowW = center ? total : avail;
            float x = center ? 0f : avail - total;
            for (int i = 0; i < n; i++)
            {
                float h = stretch ? maxH : hs[i];
                buttons[i].SetBox(x, padTop + (maxH - h) / 2f, ws[i], h);
                x += ws[i] + gap;
            }
            Ui.Size(row, rowW, padTop + maxH);
            return new Vector2(rowW, padTop + maxH);
        }

        /// <summary>A button's max-content size with an icon before its words (inline-flex, gap): the icon's width and gap more, at least the icon tall.</summary>
        public static Vector2 IconButtonSize(OcbgButton b, float size, float gap)
        {
            Vector2 n = b.NaturalSize;
            BoxStyle s = b.Box.Style;
            return new Vector2(n.x + size + gap, Math.Max(n.y, size + s.Padding.Vertical + s.BorderWidths.Vertical));
        }

        /// <summary>
        /// An icon before a button's words (`ico(…)` then `<span>label</span>`, inline-flex, centred, gap): the icon and the
        /// words centred together in the content box (the label keeps the icon's room as its left margin). Call after the
        /// button has its box.
        /// </summary>
        public static PathGraphic IconInButton(OcbgButton b, string icon, float size, Rgba color, float gap)
        {
            PathGraphic ic = ExploreIcons.Ico(b.Root, icon, size, color);
            void Place()
            {
                if (!b.Root || !ic) return;
                BoxStyle s = b.Box.Style;
                RectF c = s.ContentBox(new RectF(0f, 0f, b.Node.W, b.Node.H));
                TextStyle ts = Label.StyleOf(b.Label) ?? CommonStyles.BtnText();
                float tw = TextMeasure.Width(b.Text, ts);
                Ui.Place(ic.rectTransform, c.X + (c.W - (size + gap + tw)) / 2f, c.Y + (c.H - size) / 2f);
                // the words centre in the room right of the icon: together they centre in the content box
                Vector4 m = b.Label.margin;
                if (Math.Abs(m.x - (size + gap)) > 0.01f)
                {
                    m.x = size + gap;
                    b.Label.margin = m;
                    Label.Relayout(b.Label);
                }
            }
            Place();
            b.Area.HoverChanged += _ => Place();
            return ic;
        }

        /// <summary>
        /// A box that is a button (a choice tile, a swap slot, a survey row): its CSS states in cascade order (the base rule
        /// with its transition, an optional class state, :hover from the pointer, :focus-visible from Tab), a click, and its
        /// Tab ring (ring).
        /// </summary>
        public static CssStates Interactive(RectTransform el, string baseCss, string stateName, string stateCss, bool stateOn, string hoverCss, Action click, out Action<bool> ring)
        {
            BoxGraphic box = BoxGraphic.On(el);
            CssStates st = CssStates.Of(el);
            st.Base(baseCss);
            if (stateName != null) st.State(stateName, stateCss, stateOn);
            st.State(":hover", hoverCss).State(":focus-visible", UaFocusCss);
            st.Apply(instant: true);
            ClickArea area = ClickArea.On(box);
            area.Click += _ => click?.Invoke();
            area.HoverChanged += on =>
            {
                if (st) st.SetState(":hover", on);
            };
            ring = on =>
            {
                if (st) st.SetState(":focus-visible", on);
            };
            return st;
        }
    }
}
