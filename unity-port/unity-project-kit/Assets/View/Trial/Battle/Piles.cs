// The piles and their list (PilesView, PileListView; docs/BATTLE_SCENE.md §6c, battle-views §3.5): the three piles at
// x 32 + k·86, y 948 (sheets behind, the empty slot, the icon, the caption tip), and the drawer (layer over, 576 wide,
// sliding in over 220 ms out on the scene clock): tabs 山札 / 捨札 / 除外 / ログ全文, 閉じる（Esc）, the closing hint, the rows
// (attribute band, name, in / deck) and the log. The drawer stops pointerdown from reaching the scene and the hand
// (BattleHitTag.StopPointerDown). Port of scenes/battle/hud/piles.js.
//
// Piles: draw, discard and exile, each a 64×84 card back with its 付録 A icon at 28 px, 22 px apart from x 32, y 948
// (PileLayout). Thickness is sheets laid 4 px apart behind the front one (PileLayout.SheetsBehind); an empty pile is a steel
// dashed slot. No number is written (B9: the piles read by their thickness). A click on a pile opens its list; the same
// pile again closes it.
//
// The list: a drawer from the left (the journal drawer's mirror image) taking 30 % of the screen (PileList.WidthShare), with
// a tab per pile and one row per kind of card, "突き 2 / 3" (PileCard: how many the pile holds / how many the deck holds), in
// the order given. The rows follow every frame. The CSS strings are hud.css's (and the DS's Ocb.*Css) verbatim.
using System;
using System.Collections.Generic;
using System.Globalization;
using Depiction;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    /// <summary>createPiles's opts: { tip, onToggle(kind), onDrawer(open) }.</summary>
    // js: scenes/battle/hud/piles.js:67
    public sealed class PilesOpts
    {
        public HudTip Tip;
        /// <summary>onToggle(kind): a pile was clicked (the hud hides the tip, plays uiClick and toggles that list).</summary>
        public Action<PileKind> OnToggle;
        /// <summary>onDrawer(open): the list opened or closed (battle_ui_ux_v2 §2.4: the figures slide right while it is open).</summary>
        public Action<bool> OnDrawer;
    }

    /// <summary>createPiles(env, opts) → { bind, setLog, toggle, close, open, drawerRight, covers, middle, shown, rowHeight, destroy } (piles.js:187).</summary>
    // js: scenes/battle/hud/piles.js:67
    public sealed class HudPiles
    {
        /// <summary>
        /// KIND_BAND — DS PileDrawer (案 A, the 576 px drawer this list is; review 19 — the rows were bare text): each 40 px row
        /// carries a 6×24 band in its card's attribute colour at its left, so the list scans by attribute. A name the catalogue
        /// lacks gets the steel band.
        /// </summary>
        // js: scenes/battle/hud/piles.js:27
        private static readonly Dictionary<CardKind, string> KindBand = new Dictionary<CardKind, string>
        {
            [CardKind.Attack] = "var(--violet)",
            [CardKind.Guard] = "var(--guard)",
            [CardKind.Skill] = "var(--amber)",
            [CardKind.Stance] = "var(--crimson)",
        };

        private static Dictionary<string, CardKind> _kindByName;

        /// <summary>
        /// bandOf(name): the row (PileCard) names the card only; its attribute is the catalogue's (CardCatalog by name →
        /// CoreText.KindOf, as the writer colours a face), read for its colour alone.
        /// </summary>
        // js: scenes/battle/hud/piles.js:29
        private static string BandOf(string name)
        {
            if (_kindByName == null)
            {
                _kindByName = new Dictionary<string, CardKind>(StringComparer.Ordinal);
                try
                {
                    foreach (BattleCore.CardDef def in BattleCore.CardCatalog.All)
                    {
                        if (def != null && !string.IsNullOrEmpty(def.Name) && !_kindByName.ContainsKey(def.Name))
                            _kindByName[def.Name] = global::Depiction.Bridge.CoreText.KindOf(def.Attribute);
                    }
                }
                catch (Exception) { /* colour only */ }
            }
            return name != null && _kindByName.TryGetValue(name, out CardKind kind) && KindBand.TryGetValue(kind, out string band)
                ? band
                : "var(--steel)";
        }

        /// <summary>
        /// battle_ui_ux_v2 §1.1 C4 / DS PileDrawer 案 A (review 19: the ログ tab was missing, as in Unity's PileListView): the
        /// fourth tab, ログ全文, holds the battle's log (player.js builds it from the script's own words). scene text: the DS's
        /// tab word and its empty line. LOG_KIND is `null` in <see cref="_openKind"/>.
        /// </summary>
        // js: scenes/battle/hud/piles.js:41
        private const string LogTab = "ログ全文";
        // js: scenes/battle/hud/piles.js:43
        private const string LogEmpty = "ログ全文は、まだ中身がありません。";

        /// <summary>
        /// CLOSING_HINT — battle_ui_ux_v2 §3.5 (review fix): with the list open the cards lift on hover but are not taken, and
        /// 「閉じるとプレイできます」 stands beside the close button — the journal drawer's own closing line (JournalBook.ClosingHint, battle).
        /// </summary>
        // js: scenes/battle/hud/piles.js:49
        private static readonly string ClosingHint =
            new global::Journal.JournalBook(Array.Empty<global::Journal.JournalPage>(), global::Journal.JournalContext.Battle).ClosingHint;

        // js: scenes/battle/hud/piles.js:51
        private static readonly PileKind[] Kinds = { PileKind.Draw, PileKind.Discard, PileKind.Exile };
        /// <summary>DS 未決 1 案 A: the drawer slides in from the left over 220 ms (ease-out).</summary>
        // js: scenes/battle/hud/piles.js:53
        private const float DrawerOpenMs = 220f;

        /// <summary>ROW_HEIGHT — DS 案 A: a list row's height.</summary>
        // js: scenes/battle/hud/piles.js:55
        public const float RowHeight = 40f;

        /// <summary>
        /// CAPTION_H / CAPTION_BOTTOM: the pile's hover line names the pile only (PileList.TabLabel; no count, B9). A small
        /// caption on the hovered pile itself, under its icon, inside its 64×84 face: 16 px in ink on the opaque panel, 22 tall,
        /// 4 px over the pile's bottom edge, so the 28 px icon stays clear.
        /// </summary>
        // js: scenes/battle/hud/piles.js:64
        private const float CaptionH = 22f;
        // js: scenes/battle/hud/piles.js:65
        private const float CaptionBottom = 4f;

        // ---- hud.css:102-124 ----
        /// <summary>.ocbg-battle-pile.ocb-pile (hud.css:102).</summary>
        private const string PileHudCss = "position: absolute;";
        /// <summary>.ocbg-battle-pile .ocb-pile-sheet / .ocb-pile-face (hud.css:103).</summary>
        private const string SheetHudCss = "box-sizing: border-box;";
        /// <summary>.ocbg-battle-linetip.ocbg-battle-pilecap (hud.css:58).</summary>
        private const string PileCapCss = "height: 22px; padding: 0 6px; box-shadow: none; line-height: 20px;";
        /// <summary>.ocbg-battle-drawer.ocb-drawer (hud.css:105).</summary>
        private const string DrawerHudCss =
            "left: 0; top: 0; height: 1080px; box-sizing: border-box; background: var(--panel-solid); "
            + "border-right: var(--stroke-rule) solid var(--rule); box-shadow: var(--shadow-tip);";
        /// <summary>.ocbg-battle-drawer-head (hud.css:106 and 111).</summary>
        private const string HeadCss =
            "display: flex; align-items: flex-end; gap: 12px; padding: 20px 16px 0 16px; border-bottom: var(--stroke-rule) solid var(--rule); "
            + "flex-wrap: wrap; row-gap: 0;";
        /// <summary>.ocbg-battle-drawer-head .ocb-drawer-tabs (hud.css:112).</summary>
        private const string TabsHudCss = "flex: none;";
        /// <summary>.ocbg-battle-drawer-tab.ocb-drawer-tab (hud.css:107 and 113).</summary>
        private const string TabHudCss = "color: var(--ink); opacity: 0.72; padding: 12px 16px; white-space: nowrap;";
        /// <summary>.ocbg-battle-drawer-tab.ocb-drawer-tab.is-on / :hover (hud.css:108-109).</summary>
        private const string TabOnHudCss = "opacity: 1;";
        /// <summary>.ocbg-battle-drawer-hint (hud.css:114).</summary>
        private const string HintCss =
            "order: 3; flex: 0 0 100%; margin: 0 0 10px; text-align: right; font: 400 16px/22px var(--font-body); color: var(--ink); white-space: nowrap;";
        /// <summary>.ocbg-battle-drawer-close (hud.css:115).</summary>
        private const string CloseCss =
            "margin: 0 0 8px auto; padding: 10px 14px; font: 400 18px/1 var(--font-body); color: var(--ink); background: var(--card); "
            + "border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); cursor: pointer; white-space: nowrap;";
        /// <summary>.ocbg-battle-drawer-close:hover (hud.css:116).</summary>
        private const string CloseHoverCss = "border-color: var(--ink);";
        /// <summary>.ocbg-battle-drawer-body (hud.css:117).</summary>
        private const string BodyCss = "flex: 1; overflow: auto; padding: 8px 16px 24px;";
        /// <summary>.ocbg-battle-drawer-empty (hud.css:120).</summary>
        private const string EmptyCss = "padding: 16px 12px; font: 400 18px/1.4 var(--font-body); color: var(--ink);";
        /// <summary>.ocbg-battle-drawer-log (hud.css:122).</summary>
        private const string LogCss = "display: flex; flex-direction: column; padding: 8px 4px;";
        /// <summary>.ocbg-battle-log-line (hud.css:123).</summary>
        private const string LogLineCss = "font: 400 16px/26px var(--font-body); color: var(--ink); white-space: pre-wrap;";
        /// <summary>.ocbg-battle-log-line.is-head (hud.css:124).</summary>
        private const string LogHeadCss = "margin-top: 8px; font-weight: 700;";

        // the head's two lines (flex-wrap: tabs + close, then the hint on its own line) and the close button's margins
        private const float HeadGap = 12f;
        private const float TabsGap = 4f;
        private const float CloseMarginBottom = 8f;
        private const float HintMarginBottom = 10f;

        /// <summary>One pile's host (hosts[kind]: { el, key, count }).</summary>
        private sealed class PileHost
        {
            public PileKind Kind;
            public RectTransform El;
            public ClickArea Area;
            public RectTransform Face;
            public bool Empty;
            public string Key = "";
            public int Count;
            /// <summary>The tip's owner (the JS `self`).</summary>
            public readonly object Self = new object();
        }

        /// <summary>The drawer while it is open: { el, tabs, body, token }.</summary>
        private sealed class Drawer
        {
            public RectTransform El;
            public UiNode Node;
            public RectTransform Tabs;
            public ScrollArea Body;
            public object Token;
        }

        private readonly BattleViewEnv _env;
        private readonly PilesOpts _opts;
        private readonly Dictionary<PileKind, PileHost> _hosts = new Dictionary<PileKind, PileHost>();
        private DepictionFrame _frame;
        private bool _shown;

        // ---- the drawer ----
        private readonly float _width;
        private Drawer _drawer;
        /// <summary>openKind: the pile whose list shows, or null for LOG_KIND (the log tab).</summary>
        private PileKind? _openKind = PileKind.Draw;
        private float _slide;
        private List<string> _logLines = new List<string>();

        // js: scenes/battle/hud/piles.js:67
        private HudPiles(BattleViewEnv env, PilesOpts opts)
        {
            _env = env;
            _opts = opts ?? new PilesOpts();
            _width = (float)Math.Round(L.W * PileList.WidthShare, MidpointRounding.AwayFromZero);
            RectTransform hud = env.Layer.Hud;
            foreach (PileKind kind in Kinds)
            {
                var p = new PileHost { Kind = kind };
                RectTransform host = Ocb.El(hud, "ocbg-battle-pile ocbg-battle-hit ocb-pile " + PileList.TabLabel(kind), Ocb.PileCss + PileHudCss);
                UiNode.Of(host).Place(PileLayout.LeftOf(kind), PileLayout.Top, PileLayout.Width, PileLayout.Height);
                p.El = host;
                p.Area = ClickArea.On(host);
                BattleHitTag.Mark(host);
                p.Area.Click += _ => _opts.OnToggle?.Invoke(kind);
                p.Area.Enter += _ =>
                {
                    if (_opts.Tip == null || !_shown) return;
                    RectTransform cap = HudUtil.LineTip(PileList.TabLabel(kind), null, PileCapCss);
                    cap.name = "ocbg-battle-linetip ocbg-battle-pilecap";
                    _opts.Tip.Show(cap, n =>
                    {
                        UiNode nn = UiNode.Of(n);
                        nn.Place(PileLayout.LeftOf(kind) + PileLayout.Width / 2f - nn.W / 2f,
                            PileLayout.Top + PileLayout.Height - CaptionBottom - CaptionH);
                    }, p.Self);
                };
                p.Area.Exit += _ => _opts.Tip?.Hide(p.Self);
                // .ocb-pile:hover .ocb-pile-face { border-color: var(--ink) }
                p.Area.HoverChanged += _ => RestyleFace(p);
                _hosts[kind] = p;
            }
        }

        /// <summary>createPiles(env, opts): the three piles in layer.hud (the drawer is made when it opens).</summary>
        // js: scenes/battle/hud/piles.js:67
        public static HudPiles Create(BattleViewEnv env, PilesOpts opts) => new HudPiles(env, opts);

        private static void RestyleFace(PileHost p)
        {
            if (!p.Face) return;
            Ocb.Restyle(p.Face, Ocb.PileFaceStyle(p.Empty, p.Area && p.Area.Hovered) + SheetHudCss);
        }

        /// <summary>PilesView.Bind: the sheets behind, the empty slot, the icon.</summary>
        // js: scenes/battle/hud/piles.js:96
        private void DrawPile(PileKind kind, int count)
        {
            PileHost p = _hosts[kind];
            p.Count = count; // what the pile stands for (dataset.count, read by the dev pages' screen check)
            int behind = PileLayout.SheetsBehind(count);
            bool empty = PileLayout.IsEmpty(count);
            string key = behind.ToString(CultureInfo.InvariantCulture) + "/" + (empty ? "true" : "false");
            if (key == p.Key) return;
            p.Key = key;
            Ui.Clear(p.El);
            p.Empty = empty; // .is-empty
            for (int i = behind; i >= 1; i--)
            {
                RectTransform sheet = Ocb.El(p.El, "ocb-pile-sheet", Ocb.PileSheetCss + SheetHudCss);
                UiNode.Of(sheet).Place(0f, -PileLayout.SheetStep * i);
                // the sheets are the pile's too: a pointer over the part above the face is over the pile
                BoxGraphic g = sheet.GetComponent<BoxGraphic>();
                if (g) g.raycastTarget = true;
            }
            string faceCss = Ocb.PileFaceStyle(empty, p.Area && p.Area.Hovered) + SheetHudCss;
            RectTransform face = Ocb.El(p.El, "ocb-pile-face", faceCss);
            UiNode.Of(face).Place(0f, 0f, PileLayout.Width, PileLayout.Height);
            Ocb.Icon(face, HudUtil.PileIcon[kind], PileLayout.IconSize, empty ? DsTokens.Steel : DsTokens.Ink);
            Ocb.Flow(face, faceCss, null, PileLayout.Width, PileLayout.Height);
            BoxGraphic fg = face.GetComponent<BoxGraphic>();
            if (fg) fg.raycastTarget = true;
            p.Face = face;
        }

        // ---- the drawer ---------------------------------------------------------------------------

        private string TabCss(bool on, bool hover) =>
            Ocb.DrawerTabCss + (on ? Ocb.DrawerTabOnCss : "") + TabHudCss + (on || hover ? TabOnHudCss : "");

        // js: scenes/battle/hud/piles.js:122
        private void DrawDrawer()
        {
            Drawer d = _drawer;
            if (d == null || !d.El) return;
            RectTransform content = d.Body.Content;
            Ui.Clear(content);
            Ui.Clear(d.Tabs);
            float tx = 0f, tabsH = 0f;
            var kinds = new List<PileKind?>();
            foreach (PileKind k in Kinds) kinds.Add(k);
            kinds.Add(null);
            foreach (PileKind? kind in kinds)
            {
                bool on = kind == _openKind;
                string css = TabCss(on, false);
                string text = kind == null ? LogTab : PileList.TabLabel(kind.Value);
                RectTransform tab = Ocb.El(d.Tabs, "ocb-drawer-tab ocbg-battle-drawer-tab" + (on ? " is-on" : ""), css);
                BoxStyle bs = Ocb.BoxOf(css);
                Insets bw = bs.BorderWidths, pad = bs.Padding;
                TextStyle ts = Ocb.TextOf(css);
                Ocb.Text(tab, text, ts, "text");
                Ui.Place((RectTransform)tab.GetChild(0), bw.Left + pad.Left, bw.Top + pad.Top);
                float w = Ocb.TextWidth(text, ts) + pad.Horizontal + bw.Horizontal;
                float h = ts.LineHeightPx + pad.Vertical + bw.Vertical;
                UiNode tn = UiNode.Of(tab);
                tn.Place(tx, 0f, w, h);
                tn.Opacity = Ocb.Rule(css).Opacity ?? 1f;
                tx += w + TabsGap;
                tabsH = Math.Max(tabsH, h);
                ClickArea area = ClickArea.On(tab);
                area.HoverChanged += hov => { if (tn) tn.Opacity = Ocb.Rule(TabCss(on, hov)).Opacity ?? 1f; };
                area.Click += _ =>
                {
                    if (kind == _openKind) return;
                    _env.Audio.Play("uiClick");
                    _openKind = kind;
                    DrawDrawer();
                };
            }
            UiNode.Of(d.Tabs).W = Math.Max(0f, tx - TabsGap);
            UiNode.Of(d.Tabs).H = tabsH;

            float cw = d.Body.Viewport.rect.width;
            BoxStyle body = Ocb.BoxOf(BodyCss);
            Insets bp = body.Padding;
            float innerW = cw - bp.Horizontal;
            if (_openKind == null)
            {
                if (_logLines.Count == 0)
                {
                    EmptyLine(content, LogEmpty, bp, innerW);
                    return;
                }
                BoxStyle ls = Ocb.BoxOf(LogCss);
                Insets lp = ls.Padding;
                float lw = innerW - lp.Horizontal;
                float y = bp.Top + lp.Top;
                foreach (string line in _logLines)
                {
                    bool head = (line ?? "").StartsWith("—", StringComparison.Ordinal);
                    string css = LogLineCss + (head ? LogHeadCss : "");
                    TextStyle st = Ocb.TextOf(css);
                    if (head) y += 8f; // margin-top: 8px
                    float h = Ocb.TextHeight(line ?? "", st, lw);   // an empty line's div has no line box (0 tall)
                    Label.Make(content, line ?? "", st, bp.Left + lp.Left, y, lw, h, "ocbg-battle-log-line" + (head ? " is-head" : ""));
                    y += h;
                }
                y += lp.Bottom + bp.Bottom;
                Ui.Size(content, cw, y);
                d.Body.ScrollHeight = y;
                d.Body.ScrollTop = d.Body.MaxScroll;   // scrolled to the end
                return;
            }
            PileFrame piles = _frame != null ? _frame.Piles : null;
            List<PileCard> rows = piles != null ? piles.CardsOf(_openKind.Value) : null;
            if (rows == null || rows.Count == 0)
            {
                EmptyLine(content, PileList.Empty, bp, innerW);
                return;
            }
            float ry = bp.Top;
            foreach (PileCard r in rows)
            {
                // PileCard.RowText ("突き 2 / 3"), laid out as the DS row: the attribute band, the name, the two counts at the right.
                OcbElement row = Ocb.DrawerRow(content, innerW, CssColor.Parse(BandOf(r.Name)), r.Name ?? "",
                    r.InPile.ToString(CultureInfo.InvariantCulture) + " / " + r.InDeck.ToString(CultureInfo.InvariantCulture));
                row.Root.name = "ocb-drawer-row ocbg-battle-drawer-row";
                row.Place(bp.Left, ry);
                ry += row.H > 0f ? row.H : RowHeight;
            }
            ry += bp.Bottom;
            Ui.Size(content, cw, ry);
            d.Body.ScrollHeight = ry;
        }

        /// <summary>The list's `p.ocbg-battle-drawer-empty` line (なし / the log's empty line).</summary>
        private void EmptyLine(RectTransform content, string text, Insets bp, float innerW)
        {
            BoxStyle es = Ocb.BoxOf(EmptyCss);
            Insets ep = es.Padding;
            TextStyle st = Ocb.TextOf(EmptyCss);
            float w = innerW - ep.Horizontal;
            float h = Ocb.TextHeight(text, st, w);
            Label.Make(content, text, st, bp.Left + ep.Left, bp.Top + ep.Top, w, h, "ocbg-battle-drawer-empty");
            float total = bp.Top + ep.Vertical + h + bp.Bottom;
            Ui.Size(content, _drawer.Body.Viewport.rect.width, total);
            _drawer.Body.ScrollHeight = total;
        }

        // js: scenes/battle/hud/piles.js:159
        private void PlaceDrawer()
        {
            if (_drawer != null && _drawer.Node) _drawer.Node.PropTranslateX = HudChrome.Fixed(-_width * (1f - _slide), 1);
        }

        /// <summary>open(kind): the list of `kind` (null: the log), the drawer built and slid in when it is not open.</summary>
        // js: scenes/battle/hud/piles.js:163 (open)
        private void OpenDrawer(PileKind? kind)
        {
            _openKind = kind;
            if (_drawer == null)
            {
                string elCss = Ocb.DrawerCss + DrawerHudCss + "width: " + Ocb.Num(_width) + "px;";
                RectTransform el = Ocb.El(_env.Layer.Over, "ocb-drawer ocbg-battle-drawer ocbg-battle-hit", elCss);
                UiNode node = UiNode.Of(el);
                node.Place(0f, 0f, _width, L.H);
                // the cards and the piles under the drawer take no click through it (pointerdown stops here)
                BoxGraphic eg = el.GetComponent<BoxGraphic>();
                if (eg) eg.raycastTarget = true;
                BattleHitTag.Mark(el, hit: true, stopPointerDown: true);
                BoxStyle es = Ocb.BoxOf(elCss);
                float innerW = _width - es.BorderWidths.Horizontal;

                // the head: tabs and the close button on one line (flex-end), the hint on its own line under them
                RectTransform head = Ocb.El(el, "ocbg-battle-drawer-head", HeadCss);
                BoxStyle hs = Ocb.BoxOf(HeadCss);
                Insets hp = hs.Padding, hb = hs.BorderWidths;
                float hx0 = hp.Left + hb.Left, hInner = innerW - hp.Horizontal - hb.Horizontal;
                RectTransform tabs = Ocb.El(head, "ocb-drawer-tabs", Ocb.DrawerTabsCss + TabsHudCss);
                // a tab's height: its line + padding + the 2 px bottom border (the same for every tab)
                string tabCss = TabCss(false, false);
                BoxStyle tbs = Ocb.BoxOf(tabCss);
                float tabH = Ocb.TextOf(tabCss).LineHeightPx + tbs.Padding.Vertical + tbs.BorderWidths.Vertical;

                RectTransform close = Ocb.El(head, "ocbg-battle-drawer-close", CloseCss);
                BoxStyle cs = Ocb.BoxOf(CloseCss);
                TextStyle cts = Ocb.TextOf(CloseCss);
                Label.Make(close, PileList.CloseLabel, cts, cs.BorderWidths.Left + cs.Padding.Left, cs.BorderWidths.Top + cs.Padding.Top, null, null, "text");
                float closeW = Ocb.TextWidth(PileList.CloseLabel, cts) + cs.Padding.Horizontal + cs.BorderWidths.Horizontal;
                float closeH = cts.LineHeightPx + cs.Padding.Vertical + cs.BorderWidths.Vertical;
                ClickArea ca = ClickArea.On(close);
                ca.HoverChanged += on => { if (close) Ocb.Restyle(close, CloseCss + (on ? CloseHoverCss : "")); };
                ca.Click += _ =>
                {
                    _env.Audio.Play("uiClick");
                    Close();
                };

                // line 1 (align-items: flex-end): its cross size is the tallest margin box
                float line1 = Math.Max(tabH, closeH + CloseMarginBottom);
                float top = hp.Top + hb.Top;
                UiNode.Of(tabs).Place(hx0, top + line1 - tabH, 0f, tabH);
                UiNode.Of(close).Place(hx0 + hInner - closeW, top + line1 - CloseMarginBottom - closeH, closeW, closeH);
                float headH = top + line1;
                if (!string.IsNullOrEmpty(ClosingHint))
                {
                    TextStyle hts = Ocb.TextOf(HintCss);
                    Label.Make(head, ClosingHint, hts, hx0, headH, hInner, hts.LineHeightPx, "ocbg-battle-drawer-hint");
                    headH += hts.LineHeightPx + HintMarginBottom;
                }
                headH += hp.Bottom + hb.Bottom;
                UiNode.Of(head).Place(0f, 0f, innerW, headH);

                // the body (flex: 1; overflow: auto)
                ScrollArea body = ScrollArea.Make(el, "ocbg-battle-drawer-body", 0f, headH, innerW, L.H - headH);
                body.ShowThumb = false;

                // layer.over.appendChild(el): z-index auto, so the result card (z 3) and the chain banner (z 2) stay over it
                // (a list can be opened once the battle is over: BattleScene.js:349)
                UiNode.ResortSiblings(_env.Layer.Over);

                _drawer = new Drawer { El = el, Node = node, Tabs = tabs, Body = body };
                _slide = 0f;
                PlaceDrawer();
                object token = _drawer.Token = new object();
                _env.Clock.Run(DrawerOpenMs, Ease.Out, t =>
                {
                    if (_drawer != null && _drawer.Token == token)
                    {
                        _slide = t;
                        PlaceDrawer();
                    }
                }).Forget();
                _opts.OnDrawer?.Invoke(true);
            }
            DrawDrawer();
        }

        /// <summary>bind(f): the frame's piles — thickness, and the open list follows (PileListView.Show); a frame that shows none closes it.</summary>
        // js: scenes/battle/hud/piles.js:189
        public void Bind(DepictionFrame f)
        {
            _frame = f;
            PileFrame piles = f != null ? f.Piles : null;
            _shown = piles != null && piles.Shown;
            foreach (PileKind kind in Kinds)
            {
                PileHost p = _hosts[kind];
                if (p.El) UiNode.Of(p.El).Visible = _shown;
                if (_shown) DrawPile(kind, PileLayout.CountOf(piles, kind));
            }
            if (!_shown) Close();
            else if (_drawer != null) DrawDrawer();
        }

        /// <summary>setLog(lines): the log tab's lines (player.js); an open log tab follows.</summary>
        // js: scenes/battle/hud/piles.js:201
        public void SetLog(IReadOnlyList<string> lines)
        {
            _logLines = lines != null ? new List<string>(lines) : new List<string>();
            if (_drawer != null && _openKind == null) DrawDrawer();
        }

        /// <summary>toggle(kind) = PileListView.Toggle: opens that pile's list, or closes the drawer when that list is open already.</summary>
        // js: scenes/battle/hud/piles.js:206
        public void Toggle(PileKind kind)
        {
            if (_drawer != null && _openKind == kind)
            {
                Close();
                return;
            }
            OpenDrawer(kind);
        }

        /// <summary>close().</summary>
        // js: scenes/battle/hud/piles.js:210
        public void Close()
        {
            if (_drawer == null) return;
            if (_drawer.El) Ui.Remove(_drawer.El);
            _drawer = null;
            _opts.OnDrawer?.Invoke(false);
        }

        /// <summary>open: the drawer is open.</summary>
        // js: scenes/battle/hud/piles.js:216
        public bool Open => _drawer != null;

        /// <summary>drawerRight(): the drawer's right edge on the stage now (0 while closed; it grows as the drawer slides in).</summary>
        // js: scenes/battle/hud/piles.js:218
        public float DrawerRight() => _drawer != null ? _width * _slide : 0f;

        /// <summary>covers(pt) = PileListView.Covers: the drawer is open over this stage point.</summary>
        // js: scenes/battle/hud/piles.js:220
        public bool Covers(StagePoint pt) =>
            _drawer != null && pt.X >= 0f && pt.X < _width * _slide && pt.Y >= 0f && pt.Y <= L.H;

        /// <summary>middle(kind) = PilesView.MiddleOf.</summary>
        // js: scenes/battle/hud/piles.js:222
        public StagePoint Middle(PileKind kind) =>
            new StagePoint(PileLayout.LeftOf(kind) + PileLayout.Width / 2f, PileLayout.Top + PileLayout.Height / 2f);

        /// <summary>shown: the frame shows the piles.</summary>
        // js: scenes/battle/hud/piles.js:225
        public bool Shown => _shown;

        /// <summary>destroy().</summary>
        // js: scenes/battle/hud/piles.js:227
        public void Destroy()
        {
            Close();
            foreach (PileKind kind in Kinds)
            {
                PileHost p = _hosts[kind];
                if (p.El) Ui.Remove(p.El);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _kindByName = null;
        }
    }
}
