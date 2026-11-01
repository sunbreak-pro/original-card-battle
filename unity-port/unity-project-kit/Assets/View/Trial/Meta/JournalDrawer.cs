// Port of scenes/meta/journal.js: 手記のドロワー (dungeon_exploration_v4.md §6.7, battle-visual-v1.md §7.5,
// CAMP_FACILITIES_DESIGN.md §3.3) — a sheet of washi from the right, 576 px (30 % of the screen) wide, reading a
// Journal.Script JournalBook: which tabs open where (in battle only the enemies' pages and the terms), which rows read
// 「？」, every word. The tabs run 敵ごと / ダンジョン / メモ / 用語; in battle 「敵ごと」 is one tab per enemy being fought
// (named after it, smaller type at 5 and 6 tabs), while exploring the enemies met sit under one 「敵」 tab with a row of
// per-enemy tabs below it. J or Esc closes it.
//
// The engine-free half of journal.js (bookOf, tabsOf, withStanceTerm, rarePagesPage, expeditionPages, enemiesOf) is
// Trial.Script.JournalBookOf, fed by JournalCtx.ToScript(); this file is the drawer: mountJournal and its closures
// subLabel / go / rowsOf / flushMemo / writesMemo / memoOf / draw / disclosureDots / close / step / the keys / destroy.
// The look is meta.css's .ocbg-meta-journal block (meta.css:352-387, :426-433) and the washi (meta.css:268-273), copied
// verbatim below; the drawer lays its column out by hand from those declarations (flex column, block margins, grid).
//
// The memo sheet (the JS textarea) is a TMP_InputField: Japanese input goes through the IME. TMP_InputField reads the
// composition from the input module's BaseInput, whose stock implementation is the legacy Input class; while the sheet
// has the focus, JournalMemoIme (below) stands in for it with the Input System's own IME API (Keyboard.
// onIMECompositionChange / SetIMEEnabled / SetIMECursorPosition), so composition shows in the sheet under the
// "Input System only" setting. KeyStack already keeps every key but Escape away from the screens while the sheet is
// focused (journal.js:260 stopPropagation) and turns the IME on for it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using Trial.Bridge;
using Trial.Geometry;
using Trial.Kit;
using Trial.Script;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using JBook = global::Journal.JournalBook;
using JPage = global::Journal.JournalPage;
using JRow = global::Journal.JournalRow;
using JTab = global::Journal.JournalTab;
using JTerms = global::Journal.JournalTerms;

namespace Trial.Meta
{
    public sealed class JournalDrawer : IOverlay
    {
        // ---- meta.css: the journal drawer, verbatim ------------------------------------------------------------------

        /// <summary>meta.css:268 .ocbg-meta-washi's color: what every text of the drawer inherits.</summary>
        public const string WashiColorCss = "color: var(--washi-ink);";

        /// <summary>meta.css:353 .ocbg-meta-journal .ocbg-meta-drawer.</summary>
        public const string DrawerCss = "display: flex; flex-direction: column; padding: 26px 28px 18px; box-shadow: -18px 0 40px color-mix(in srgb, var(--ground) 70%, transparent); border-left: 1px solid color-mix(in srgb, var(--washi-ink) 25%, transparent);";

        /// <summary>meta.css:354 .ocbg-meta-drawer-head.</summary>
        public const string HeadCss = "display: flex; align-items: center; gap: var(--space-12);";

        /// <summary>meta.css:355 .ocbg-meta-drawer-head h2.</summary>
        public const string HeadH2Css = "display: inline-flex; align-items: center; gap: 12px; font: 800 36px/1 var(--font-display); letter-spacing: .3em;";

        /// <summary>meta.css:356 .ocbg-meta-drawer-hint.</summary>
        public const string HintCss = "margin-left: auto; font: 400 16px/1.3 var(--font-body);";

        /// <summary>meta.css:357 .ocbg-meta-drawer-close.</summary>
        public const string CloseCss = "margin-left: auto; height: 40px; padding: 0 14px; border: var(--stroke-rule) solid color-mix(in srgb, var(--washi-ink) 45%, transparent); border-radius: var(--radius-tag); font: 700 16px/1 var(--font-body); color: var(--washi-ink);";

        /// <summary>meta.css:358 .ocbg-meta-drawer-hint + .ocbg-meta-drawer-close.</summary>
        public const string HintCloseCss = "margin-left: var(--space-8);";

        /// <summary>meta.css:359 .ocbg-meta-drawer-close:hover.</summary>
        public const string CloseHoverCss = "background: color-mix(in srgb, var(--washi-ink) 8%, transparent);";

        /// <summary>meta.css:360 .ocbg-meta-drawer-tabs (journal.js:274 sets the columns to repeat(n, minmax(0, 1fr))).</summary>
        public const string TabsCss = "display: grid; grid-template-columns: repeat(4, 1fr); gap: 6px; margin-top: 20px;";

        /// <summary>meta.css:361 .ocbg-meta-drawer-tab.</summary>
        public const string TabCss = "height: 46px; font: 700 20px/1 var(--font-display); letter-spacing: .08em; color: var(--washi-ink); background: color-mix(in srgb, var(--washi-ink) 6%, transparent); border-bottom: 3px solid transparent; border-radius: 4px 4px 0 0; transition: background .14s;";

        /// <summary>meta.css:362 .ocbg-meta-drawer-tab:hover:not([disabled]).</summary>
        public const string TabHoverCss = "background: color-mix(in srgb, var(--washi-ink) 11%, transparent);";

        /// <summary>meta.css:363 .ocbg-meta-drawer-tab.is-on.</summary>
        public const string TabOnCss = "background: color-mix(in srgb, var(--washi-ink) 13%, transparent); border-bottom-color: var(--washi-ink);";

        /// <summary>meta.css:364 .ocbg-meta-drawer-tab.is-shut.</summary>
        public const string TabShutCss = "opacity: .32;";

        /// <summary>meta.css:365 .ocbg-meta-drawer-tab.is-enemy.</summary>
        public const string TabEnemyCss = "padding: 0 4px; font: 700 18px/1.15 var(--font-display); letter-spacing: .02em; word-break: keep-all; overflow-wrap: anywhere;";

        /// <summary>meta.css:366 .ocbg-meta-drawer-tabs.is-n5 .ocbg-meta-drawer-tab.</summary>
        public const string TabN5Css = "padding: 0 2px; font: 700 17px/1.15 var(--font-display); letter-spacing: 0; word-break: keep-all; overflow-wrap: anywhere;";

        /// <summary>meta.css:367 .ocbg-meta-drawer-tabs.is-n6 .ocbg-meta-drawer-tab.</summary>
        public const string TabN6Css = "padding: 0 2px; font: 700 15px/1.15 var(--font-display); letter-spacing: 0; word-break: keep-all; overflow-wrap: anywhere;";

        /// <summary>meta.css:368 .ocbg-meta-drawer-sub.</summary>
        public const string SubCss = "display: flex; flex-wrap: wrap; gap: 6px 8px; margin-top: 12px;";

        /// <summary>meta.css:369 .ocbg-meta-drawer-subtab.</summary>
        public const string SubtabCss = "padding: 6px 14px; border: var(--stroke-rule) solid color-mix(in srgb, var(--washi-ink) 35%, transparent); border-radius: 16px; font: 700 16px/1.2 var(--font-body); color: var(--washi-ink);";

        /// <summary>meta.css:370 .ocbg-meta-drawer-subtab.is-on.</summary>
        public const string SubtabOnCss = "border: var(--stroke-frame) solid var(--washi-ink); background: color-mix(in srgb, var(--washi-ink) 10%, transparent);";

        /// <summary>meta.css:371 .ocbg-meta-drawer-titlerow.</summary>
        public const string TitleRowCss = "display: flex; align-items: baseline; justify-content: space-between; gap: var(--space-12); margin-top: 20px; padding-bottom: 12px; border-bottom: var(--stroke-rule) solid color-mix(in srgb, var(--washi-ink) 25%, transparent);";

        /// <summary>meta.css:372 .ocbg-meta-drawer-titlerow h3.</summary>
        public const string TitleH3Css = "font: 700 28px/1.25 var(--font-display); letter-spacing: .06em;";

        /// <summary>meta.css:373 .ocbg-meta-disclosure.</summary>
        public const string DisclosureCss = "display: inline-flex; align-items: center; gap: 10px; font: 700 16px/1 var(--font-body); white-space: nowrap;";

        /// <summary>meta.css:374 .ocbg-meta-disclosure-dots.</summary>
        public const string DotsCss = "display: inline-flex; gap: 4px;";

        /// <summary>meta.css:375 .ocbg-meta-disclosure-dots i.</summary>
        public const string DotCss = "width: 10px; height: 10px; border-radius: 50%; border: 1.5px solid var(--washi-ink);";

        /// <summary>meta.css:376 .ocbg-meta-disclosure-dots i.is-on.</summary>
        public const string DotOnCss = "background: var(--washi-ink);";

        /// <summary>meta.css:377 .ocbg-meta-drawer-body.</summary>
        public const string BodyCss = "flex: 1; min-height: 0; overflow-y: auto; padding: 4px 8px 12px 0;";

        /// <summary>meta.css:378 .ocbg-meta-drawer-body::-webkit-scrollbar-thumb (over meta.css:25's radius).</summary>
        public const string BodyThumbCss = "background: color-mix(in srgb, var(--washi-ink) 30%, transparent);";

        /// <summary>meta.css:379 .ocbg-meta-rows-h.</summary>
        public const string RowsHCss = "font: 700 18px/1 var(--font-display); letter-spacing: .14em; margin: 20px 0 8px;";

        /// <summary>meta.css:380 .ocbg-meta-rows-pair.</summary>
        public const string RowsPairCss = "display: grid; grid-template-columns: 132px 1fr; gap: 14px; padding: 8px 0; border-bottom: 1px dotted color-mix(in srgb, var(--washi-ink) 25%, transparent);";

        /// <summary>meta.css:381 .ocbg-meta-rows-pair b.</summary>
        public const string RowsPairBCss = "font: 700 18px/1.35 var(--font-display);";

        /// <summary>meta.css:382 .ocbg-meta-rows-pair span, .ocbg-meta-rows-stack span.</summary>
        public const string RowsSpanCss = "font: 400 16px/1.5 var(--font-body); white-space: pre-line;";

        /// <summary>meta.css:383 .ocbg-meta-rows-stack.</summary>
        public const string RowsStackCss = "display: flex; flex-direction: column; gap: 4px; padding: 10px 0; border-bottom: 1px dotted color-mix(in srgb, var(--washi-ink) 25%, transparent);";

        /// <summary>meta.css:384 .ocbg-meta-rows-stack b.</summary>
        public const string RowsStackBCss = "font: 700 18px/1.35 var(--font-display); letter-spacing: .04em;";

        /// <summary>meta.css:385 .ocbg-meta-rows-p.</summary>
        public const string RowsPCss = "font: 400 18px/1.7 var(--font-body); padding: 10px 0; white-space: pre-line;";

        /// <summary>meta.css:386 .ocbg-meta-hidden (the 「？」 tag).</summary>
        public const string HiddenCss = "display: inline-block; min-width: 44px; padding: 0 8px; text-align: center; border: 1px dashed color-mix(in srgb, var(--washi-ink) 45%, transparent); border-radius: 3px; font: 700 16px/1.6 var(--font-body);";

        /// <summary>meta.css:387 .ocbg-meta-drawer-keys.</summary>
        public const string KeysCss = "margin-top: 10px; padding-top: 10px; border-top: var(--stroke-rule) solid color-mix(in srgb, var(--washi-ink) 18%, transparent); font: 400 16px/1 var(--font-body); color: var(--washi-ink);";

        /// <summary>meta.css:426 .ocbg-meta-memo.</summary>
        public const string MemoCss = "display: flex; flex-direction: column; min-height: 100%;";

        /// <summary>meta.css:427 .ocbg-meta-rows-stack.is-scribble span.</summary>
        public const string ScribbleSpanCss = "font-family: var(--font-display); font-size: 18px; line-height: 1.6;";

        /// <summary>meta.css:428-431 .ocbg-meta-memo-sheet (the textarea).</summary>
        public const string MemoSheetCss = "display: block; width: 100%; flex: 1; min-height: 360px; margin-top: 12px; padding: 4px 6px; resize: none; border: 0; outline: none; border-radius: 0; background-color: transparent; color: var(--washi-ink); caret-color: var(--washi-ink); font: 400 20px/36px var(--font-display); letter-spacing: .04em; background-image: linear-gradient(180deg, transparent 35px, color-mix(in srgb, var(--washi-ink) 22%, transparent) 35px, color-mix(in srgb, var(--washi-ink) 22%, transparent) 36px); background-size: 100% 36px; background-attachment: local;";

        /// <summary>meta.css:432 .ocbg-meta-memo-sheet::placeholder.</summary>
        public const string MemoPlaceholderCss = "color: var(--washi-ink); opacity: .55;";

        /// <summary>meta.css:433 .ocbg-meta-memo-sheet:focus.</summary>
        public const string MemoFocusCss = "background-color: color-mix(in srgb, var(--washi-ink) 4%, transparent);";

        // ---- the box model the declarations above give (stage px) --------------------------------------------------

        // js: scenes/meta/journal.js:21 (DRAWER_W, 30% of 1920)
        private const float DrawerW = JournalBookOf.DrawerW;
        private const float BorderL = 1f, PadT = 26f, PadX = 28f, PadB = 18f;          // .ocbg-meta-drawer
        private const float ContentX = BorderL + PadX;                                  // 29
        private const float ContentW = DrawerW - BorderL - PadX * 2f;                   // 519
        private const float HeadGap = 12f, IconSize = 30f, CloseH = 40f, ClosePadX = 14f, CloseBorder = 1f, HintGap = 8f;
        private const float TabsTop = 20f, TabsGap = 6f, TabH = 46f, TabBorderB = 3f;
        private const float SubTop = 12f, SubGapRow = 6f, SubGapCol = 8f, SubPadY = 6f, SubPadX = 14f;
        private const float TitleTop = 20f, TitlePadB = 12f, TitleBorder = 1f, TitleGap = 12f;
        private const float DotSize = 10f, DotsGap = 4f, DisclosureGap = 10f;
        private const float BodyPadT = 4f, BodyPadR = 8f, BodyPadB = 12f, ScrollbarW = 8f;
        private const float KeysTop = 10f, KeysPadT = 10f, KeysBorder = 1f;
        private const float RowsHTop = 20f, RowsHBottom = 8f, PairCol = 132f, PairGap = 14f, PairPadY = 8f, StackGap = 4f, StackPadY = 10f, PPadY = 10f, RowRule = 1f;
        private const float HiddenMinW = 44f, HiddenPadX = 8f, HiddenBorder = 1f;
        private const float SheetTop = 12f, SheetMinH = 360f;

        // ---- state (the closure variables of mountJournal) ----------------------------------------------------------

        private readonly JournalCtx _ctx;
        private readonly MetaSound _sound;
        private readonly RectTransform _root;
        private readonly SceneClock _clock;
        private readonly JBook _book;
        private readonly bool _perEnemy;
        private readonly bool _showsMemo;
        private bool _alive = true;
        private bool _destroyed;
        private bool _closing;
        private JPage _page;

        private BoxGraphic _shade;
        private WashiBox _drawer;
        private RectTransform _tabs, _subtabs, _titleRow;
        private ScrollArea _body;
        private float _tabsY;
        private global::Trial.Kit.Styler _pd, _ps;
        private Action _popKeys;

        // the memo (journal.js:233-238)
        private string _memoText;
        private bool _memoTimer;
        private int _memoToken;
        private MemoSheet _sheet;

        // js: scenes/meta/journal.js:169
        private JournalDrawer(RectTransform host, JournalCtx ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _sound = MetaUtil.SoundOf(ctx);
            _root = MetaUtil.SceneRoot(host, "journal");
            _clock = MetaUtil.SceneClock(null, ctx.Stage);
            _book = JournalBookOf.BookOf(ScriptCtx(ctx));
            _page = (!string.IsNullOrEmpty(ctx.PageId) ? _book.Find(ctx.PageId) : _book.First) ?? null;
            _perEnemy = _book.Context == global::Journal.JournalContext.Battle;   // js: journal.js:200 enemyTabs(book)
            _memoText = ctx.Memo ?? "";                                            // js: journal.js:233
            _showsMemo = ctx.OnMemo != null || ctx.Memo != null;                   // js: journal.js:238

            // js: scenes/meta/journal.js:178 — the light shade closes on a click
            _shade = MetaStyle.Shade(_root, light: true, onClick: () => Close());

            // js: scenes/meta/journal.js:180 — the washi sheet (role dialog, aria-label 手記)
            _drawer = MetaStyle.Washi(_root, MetaUtil.W - DrawerW, 0f, DrawerW, MetaUtil.H, DrawerCss, "ocbg-meta-washi ocbg-meta-drawer");
            Hit.Area(_drawer.Root);   // a click on the sheet stays on it (the shade under it closes)

            BuildHead();

            // js: scenes/meta/journal.js:189-197
            _tabs = Ui.Div(_drawer.Root, "ocbg-meta-drawer-tabs", ContentX, _tabsY, ContentW, TabH);
            _subtabs = Ui.Div(_drawer.Root, "ocbg-meta-drawer-sub", ContentX, _tabsY + TabH + SubTop, ContentW, 0f);
            UiNode.Of(_subtabs);
            _titleRow = Ui.Div(_drawer.Root, "ocbg-meta-drawer-titlerow", ContentX, _tabsY + TabH + TitleTop, ContentW, 0f);
            UiNode.Of(_titleRow);
            _body = ScrollArea.Make(_drawer.Root, "ocbg-meta-drawer-body", ContentX, _tabsY + TabH, ContentW, BodyBottom - (_tabsY + TabH));
            _body.ShowThumb = true;
            _body.ThumbWidth = ScrollbarW;
            BoxGraphic.On(_body.Thumb, MetaStyle.ScrollbarThumbCss + BodyThumbCss);
            BuildKeys();

            Draw(false);

            // js: scenes/meta/journal.js:309 — the sheet slides in from the right (220 ms)
            _pd = global::Trial.Kit.Styler.Of(_drawer.Node, x: DrawerW);
            _ps = global::Trial.Kit.Styler.Of(UiNode.Of(_shade.rectTransform), o: 0f);
            _clock.Tween(_pd, "x", 0f, 220f, Ease.Out);
            _clock.Tween(_ps, "o", 1f, 220f, Ease.Out);
            _sound.Play("journalOpen");

            // js: scenes/meta/journal.js:333
            _popKeys = KeyStack.Push(OnKey);
            KeyStack.KeyDownCapture += OnKeyCapture;
        }

        // js: scenes/meta/journal.js:169
        /// <summary>
        /// mountJournal(host, ctx) → { destroy(), close() }: the drawer over host (the router's overlay layer, or the battle's
        /// journal host). ctx.OnClose runs when the close slide ends; ctx.OnMemo gets the memo 400 ms after typing stops,
        /// on close and on destroy.
        /// </summary>
        public static IOverlay Mount(RectTransform host, JournalCtx ctx) => new JournalDrawer(host, ctx);

        /// <summary>
        /// The ctx JournalBookOf.BookOf reads. The JS reads the enemies' pages from EnemyPages itself (journal.js:76); a ctx
        /// that hands in neither EnemyPages delegate gets Trial.Bridge's.
        /// </summary>
        private static global::Trial.Script.JournalCtx ScriptCtx(JournalCtx ctx)
        {
            global::Trial.Script.JournalCtx s = ctx.ToScript();
            if (s.HasEnemyPage == null && s.EnemyPage == null)
            {
                s.HasEnemyPage = EnemyPages.Has;
                s.EnemyPage = EnemyPages.Page;
            }
            return s;
        }

        private static float FooterY => MetaUtil.H - PadB - (KeysBorder + KeysPadT + KeysLineH);
        private static float BodyBottom => FooterY - KeysTop;
        private const float KeysLineH = 16f;   // font: 400 16px/1

        private static TextStyle TextOf(string css) => MetaStyle.Text(WashiColorCss + css);

        /// <summary>A p / h3 of the drawer (meta.css:435, :438: word-break auto-phrase).</summary>
        private static TextStyle PhraseOf(string css) => MetaStyle.P(WashiColorCss + css);

        private static TextMeshProUGUI Text(RectTransform parent, string text, TextStyle style, float x, float y, float w, float h, string name) =>
            Label.Make(parent, text ?? "", style, x, y, w, h, name);

        // ---- the head and the keys line (built once) ----------------------------------------------------------------

        // js: scenes/meta/journal.js:184
        private void BuildHead()
        {
            TextStyle h2 = TextOf(HeadH2Css);
            h2.WhiteSpace = WhiteSpace.NoWrap;
            TextStyle closeStyle = TextOf(MetaStyle.ButtonCss + CloseCss);
            closeStyle.WhiteSpace = WhiteSpace.NoWrap;
            closeStyle.Align = TextAlign.Center;
            string hint = _book.ClosingHint;
            TextStyle hintStyle = TextOf(HintCss);
            hintStyle.WhiteSpace = WhiteSpace.NoWrap;
            float h2H = h2.LineHeightPx;
            float hintH = string.IsNullOrEmpty(hint) ? 0f : hintStyle.LineHeightPx;
            float headH = Math.Max(Math.Max(h2H, IconSize), Math.Max(CloseH, hintH));
            _tabsY = PadT + headH + TabsTop;

            RectTransform head = Ui.Div(_drawer.Root, "ocbg-meta-drawer-head", ContentX, PadT, ContentW, headH);

            // h2: journal 30 washi-ink and 「手記」 (inline-flex, centred, gap 12)
            float h2TextW = TextMeasure.Width(JBook.Heading, h2);
            RectTransform title = Ui.Div(head, "h2", 0f, (headH - h2H) / 2f, IconSize + HeadGap + h2TextW, h2H);
            PathGraphic icon = MetaUtil.Icon(title, "journal", IconSize, DsTokens.WashiInk);
            Ui.Place(icon.rectTransform, 0f, (h2H - IconSize) / 2f);
            Text(title, JBook.Heading, h2, IconSize + HeadGap, 0f, h2TextW + 0.01f, h2H, "text");

            // the close button 「閉じる（J）」 at the right (margin-left: auto)
            float closeTextW = TextMeasure.Width(JBook.CloseLabel, closeStyle);
            float closeW = closeTextW + ClosePadX * 2f + CloseBorder * 2f;
            float closeX = ContentW - closeW;
            string closeBase = MetaStyle.ButtonCss + CloseCss + (string.IsNullOrEmpty(hint) ? "" : HintCloseCss);
            BoxGraphic close = BoxGraphic.Make(head, closeBase, closeX, (headH - CloseH) / 2f, closeW, CloseH, "ocbg-meta-drawer-close");
            float lh = closeStyle.LineHeightPx;
            Text(close.rectTransform, JBook.CloseLabel, closeStyle, CloseBorder + ClosePadX, (CloseH - lh) / 2f, closeTextW + 0.01f, lh, "text");
            ClickArea closeArea = ClickArea.On(close);
            closeArea.HoverChanged += on => { if (close) close.SetCss(closeBase + (on ? CloseHoverCss : "")); };
            closeArea.Click += _ => Close();

            // battle: 「閉じるとプレイできます」 right before the button (margin-left: auto). Between them the head's flex gap (12)
            // and the button's margin-left (8, meta.css:358) add up: 20 px (613-battle-journal.png: text ends 1754, button 1774).
            if (!string.IsNullOrEmpty(hint))
            {
                float hintW = TextMeasure.Width(hint, hintStyle);
                Text(head, hint, hintStyle, closeX - HintGap - HeadGap - hintW, (headH - hintH) / 2f, hintW + 0.01f, hintH, "ocbg-meta-drawer-hint");
            }
        }

        // js: scenes/meta/journal.js:197
        private void BuildKeys()
        {
            float h = KeysBorder + KeysPadT + KeysLineH;
            BoxGraphic.Make(_drawer.Root, KeysCss, ContentX, FooterY, ContentW, h, "ocbg-meta-drawer-keys");
            TextStyle s = TextOf(KeysCss);
            s.WhiteSpace = WhiteSpace.NoWrap;
            Text(_drawer.Root, MetaText.JournalKeys, s, ContentX, FooterY + KeysBorder + KeysPadT, ContentW, s.LineHeightPx, "ocbg-meta-drawer-keys-text");
        }

        // ---- the closures of mountJournal ----------------------------------------------------------------------------

        // js: scenes/meta/journal.js:199
        private List<JPage> PagesOf(JTab tab) => _book.Pages.Where(p => p.Tab == tab).ToList();

        // js: scenes/meta/journal.js:202
        /// <summary>A page's label on the row below its tab: the enemy's name, or a dungeon page's title (the layer's short: 「第 N 層」).</summary>
        private static string SubLabel(JPage p)
        {
            if (p.Tab != JTab.Dungeon) return JBook.TabText(p);
            Match m = Regex.Match(p.Id, "^dungeon-([0-9]+)$");
            return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? MetaText.LayerShort(n) : p.Title;
        }

        // js: scenes/meta/journal.js:208
        private void Go(JPage next, bool quiet = false)
        {
            if (next == null || !_book.CanOpen(next) || (_page != null && next.Id == _page.Id)) return;
            _page = next;
            if (!quiet) _sound.Play("nodeHover");
            Draw(true);
        }

        /// <summary>
        /// The rows of a block list (.ocbg-meta-rows, a block box: neighbouring vertical margins collapse) or of the memo
        /// list (.ocbg-meta-memo, a flex column: they add up). The first child's top margin stays inside the list, where
        /// the body's 4px padding keeps it.
        /// </summary>
        private sealed class Flow
        {
            private readonly bool _collapse;
            private float _y, _pending;
            private bool _any;

            public Flow(bool collapse) { _collapse = collapse; }

            /// <summary>Where the next box's border edge goes, its top margin given.</summary>
            public float Next(float marginTop)
            {
                if (!_any) _y += marginTop;
                else _y += _collapse ? Math.Max(_pending, marginTop) : _pending + marginTop;
                _any = true;
                _pending = 0f;
                return _y;
            }

            public void Took(float height, float marginBottom)
            {
                _y += height;
                _pending = marginBottom;
            }

            /// <summary>The content's height so far (the last bottom margin included).</summary>
            public float Height => _y + _pending;
        }

        // js: scenes/meta/journal.js:215
        /// <summary>
        /// rowsOf(p): a heading per label-only row (after 【用語】 on the terms page the rows pair label and text in two
        /// columns, else they stack), a 「？」 tag for a row the 開示度 does not reach, a plain paragraph for a row without
        /// a label. Laid out `width` wide at the top of the body (its 4px padding); height = the list's.
        /// </summary>
        private RectTransform RowsOf(RectTransform parent, JPage p, float width, out float height)
        {
            RectTransform list = Ui.Div(parent, "ocbg-meta-rows", 0f, BodyPadT, width, 0f);
            UiNode.Of(list);
            var flow = new Flow(collapse: true);
            string mode = "stack";
            foreach (JRow row in p.Rows)
            {
                if (row.Label.Length > 0 && row.Text.Length == 0)
                {
                    RowsH(list, row.Label, width, flow);
                    mode = p.Tab == JTab.Terms && row.Label == JTerms.TermsHeading ? "pair" : "stack";
                    continue;
                }
                bool hidden = p.IsHidden(row);
                string text = hidden ? JRow.Hidden : p.TextOf(row);
                if (row.Label.Length == 0) RowsP(list, text, hidden, width, flow);
                else if (mode == "pair") RowsPair(list, row.Label, text, hidden, width, flow);
                else RowsStack(list, row.Label, text, hidden, width, flow, scribble: false);
            }
            height = flow.Height;
            UiNode.Of(list).H = height;
            return list;
        }

        /// <summary>h4.ocbg-meta-rows-h: margin 20 0 8.</summary>
        private static void RowsH(RectTransform list, string label, float width, Flow flow)
        {
            TextStyle s = TextOf(RowsHCss);
            float h = TextMeasure.Height(label, s, width);
            float y = flow.Next(RowsHTop);
            Text(list, label, s, 0f, y, width, h, "ocbg-meta-rows-h");
            flow.Took(h, RowsHBottom);
        }

        /// <summary>p.ocbg-meta-rows-p: the text alone (or the 「？」 tag inline), 400 18px/1.7, padding 10 0.</summary>
        private static void RowsP(RectTransform list, string text, bool hidden, float width, Flow flow)
        {
            TextStyle s = PhraseOf(RowsPCss);
            // the player's own words (the memo) are text, never TMP tags: phrase wrapping needs rich text, so it goes
            if (text != null && text.IndexOf('<') >= 0) s.AutoPhrase = false;
            float y = flow.Next(0f);
            float lh = s.LineHeightPx;
            float textH;
            if (hidden)
            {
                // an inline-block on the paragraph's first line, on its baseline
                TextStyle hs = HiddenStyle();
                float tagH = hs.LineHeightPx + HiddenBorder * 2f;
                float tagBase = HiddenBorder + TextMath.CssBaselineFromTop(hs.Spec);
                float strutBase = TextMath.CssBaselineFromTop(s.Spec);
                float above = Math.Max(strutBase, tagBase);
                float below = Math.Max(lh - strutBase, tagH - tagBase);
                textH = above + below;
                float tagW = Math.Max(HiddenMinW, TextMeasure.Width(text, hs) + HiddenPadX * 2f + HiddenBorder * 2f);
                Hidden(list, 0f, y + PPadY + (above - tagBase), Math.Min(tagW, width), tagH);
            }
            else
            {
                textH = TextMeasure.Height(text, s, width);
                Text(list, text, s, 0f, y + PPadY, width, textH, "ocbg-meta-rows-p");
            }
            flow.Took(PPadY + textH + PPadY, 0f);
        }

        /// <summary>div.ocbg-meta-rows-pair: grid 132px | 1fr, gap 14, padding 8 0, a dotted rule under it.</summary>
        private static void RowsPair(RectTransform list, string label, string text, bool hidden, float width, Flow flow)
        {
            TextStyle b = TextOf(RowsPairBCss);
            TextStyle span = TextOf(RowsSpanCss);
            float colX = PairCol + PairGap;
            float colW = Math.Max(0f, width - colX);
            float bH = TextMeasure.Height(label, b, PairCol);
            float spanH = hidden ? HiddenStyle(inRowSpan: true).LineHeightPx + HiddenBorder * 2f : TextMeasure.Height(text, span, colW);
            float cell = Math.Max(bH, spanH);
            float rowH = PairPadY + cell + PairPadY + RowRule;
            float y = flow.Next(0f);
            BoxGraphic row = BoxGraphic.Make(list, RowsPairCss, 0f, y, width, rowH, "ocbg-meta-rows-pair");
            Text(row.rectTransform, label, b, 0f, PairPadY, PairCol, bH, "b");
            // grid items stretch to the row: the tag fills its cell
            if (hidden) Hidden(row.rectTransform, colX, PairPadY, colW, cell, inRowSpan: true);
            else Text(row.rectTransform, text, span, colX, PairPadY, colW, spanH, "span");
            flow.Took(rowH, 0f);
        }

        /// <summary>div.ocbg-meta-rows-stack (.is-scribble): label over text, gap 4, padding 10 0, a dotted rule under it.</summary>
        private static void RowsStack(RectTransform list, string label, string text, bool hidden, float width, Flow flow, bool scribble)
        {
            TextStyle b = TextOf(RowsStackBCss);
            TextStyle span = TextOf(RowsSpanCss + (scribble ? ScribbleSpanCss : ""));
            float bH = TextMeasure.Height(label, b, width);
            float spanH = hidden ? HiddenStyle(inRowSpan: true).LineHeightPx + HiddenBorder * 2f : TextMeasure.Height(text, span, width);
            float rowH = StackPadY + bH + StackGap + spanH + StackPadY + RowRule;
            float y = flow.Next(0f);
            BoxGraphic row = BoxGraphic.Make(list, RowsStackCss, 0f, y, width, rowH, scribble ? "ocbg-meta-rows-stack is-scribble" : "ocbg-meta-rows-stack");
            Text(row.rectTransform, label, b, 0f, StackPadY, width, bH, "b");
            float sy = StackPadY + bH + StackGap;
            // flex items stretch across the column: the tag is as wide as the row
            if (hidden) Hidden(row.rectTransform, 0f, sy, width, spanH, inRowSpan: true);
            else if (spanH > 0f) Text(row.rectTransform, text, span, 0f, sy, width, spanH, "span");
            flow.Took(rowH, 0f);
        }

        /// <summary>
        /// The 「？」 tag's text. Inside a pair or stack row the tag is that row's span, and `.ocbg-meta-journal
        /// .ocbg-meta-rows-pair span, .ocbg-meta-journal .ocbg-meta-rows-stack span` (0,2,1) outranks `.ocbg-meta-journal
        /// .ocbg-meta-hidden` (0,2,0): its font 400 16px/1.5 wins, the tag is 24 + 2 tall (613-battle-journal.png: borders at
        /// y 389 and 414, a thin 「？」). In a paragraph row (inRowSpan false) the tag keeps its own 700 16px/1.6.
        /// </summary>
        private static TextStyle HiddenStyle(bool inRowSpan = false)
        {
            TextStyle s = TextOf(HiddenCss + (inRowSpan ? RowsSpanCss : ""));
            s.WhiteSpace = WhiteSpace.NoWrap;
            s.Align = TextAlign.Center;
            return s;
        }

        /// <summary>span.ocbg-meta-hidden: 「？」 on a dashed tag, title 「？」; its text at the top of its box, centred.</summary>
        private static void Hidden(RectTransform parent, float x, float y, float w, float h, bool inRowSpan = false)
        {
            BoxGraphic tag = BoxGraphic.Make(parent, HiddenCss, x, y, w, h, "ocbg-meta-hidden");
            TextStyle s = HiddenStyle(inRowSpan);
            float inner = Math.Max(0f, w - (HiddenPadX + HiddenBorder) * 2f);
            Text(tag.rectTransform, JRow.Hidden, s, HiddenBorder + HiddenPadX, HiddenBorder, inner, s.LineHeightPx, "text");
            TitleTip.On(tag, JRow.Hidden);
        }

        // js: scenes/meta/journal.js:235
        private void FlushMemo()
        {
            if (_memoTimer)
            {
                _memoTimer = false;
                _memoToken++;
            }
            if (_ctx.OnMemo != null)
            {
                try { _ctx.OnMemo(_memoText); }
                catch (Exception)
                {
                    // the router's
                }
            }
        }

        // js: scenes/meta/journal.js:236
        private bool WritesMemo(JPage p) =>
            p.Tab == JTab.Memo && (_ctx.OnMemo != null || _ctx.Memo != null || (_ctx.Scribbles != null && _ctx.Scribbles.Count > 0));

        // js: scenes/meta/journal.js:239
        /// <summary>
        /// memoOf(p): the scribbles handed down (「受け継いだ走り書き」, then 「自分のメモ」), then the player's own sheet when
        /// ctx.OnMemo is given (it fills the body: flex 1, min-height 360), else the memo as a paragraph (the page's empty
        /// line when there is none). minHeight = the body's content height (.ocbg-meta-memo's min-height: 100%).
        /// </summary>
        private RectTransform MemoOf(RectTransform parent, JPage p, float width, float minHeight, out float height)
        {
            RectTransform list = Ui.Div(parent, "ocbg-meta-rows ocbg-meta-memo", 0f, BodyPadT, width, 0f);
            UiNode.Of(list);
            var flow = new Flow(collapse: false);
            IReadOnlyList<InheritanceCore.Scribble> scribbles = _ctx.Scribbles ?? Array.Empty<InheritanceCore.Scribble>();
            if (scribbles.Count > 0)
            {
                RowsH(list, MetaText.MemoInherited, width, flow);
                foreach (InheritanceCore.Scribble sc in scribbles)
                    RowsStack(list, InheritanceCore.InheritanceText.ScribbleName(sc.Generation, sc.Name), sc.Text ?? "", false, width, flow, scribble: true);
                RowsH(list, MetaText.MemoMine, width, flow);
            }
            JRow emptyRow = p.Rows.FirstOrDefault(r => r.Label.Length == 0);
            string empty = emptyRow != null ? emptyRow.Text ?? "" : "";
            if (_ctx.OnMemo != null)
            {
                float y = flow.Next(SheetTop);
                float h = Math.Max(SheetMinH, minHeight - y);
                _sheet = new MemoSheet(this, list, 0f, y, width, h, _memoText, empty);
                flow.Took(h, 0f);
            }
            else
            {
                RowsP(list, string.IsNullOrEmpty(_memoText) ? empty : _memoText, false, width, flow);
            }
            height = Math.Max(flow.Height, minHeight);
            UiNode.Of(list).H = height;
            return list;
        }

        // js: scenes/meta/journal.js:254 (the textarea's input listener)
        private void OnMemoInput(string value)
        {
            _memoText = value ?? "";
            _memoTimer = true;
            int token = ++_memoToken;
            _clock.WaitReal(400f).Then(() =>
            {
                if (token == _memoToken && _memoTimer) FlushMemo();
            });
        }

        // js: scenes/meta/journal.js:271
        private void Draw(bool animate)
        {
            // the tabs: an even grid, smaller type at 5 and 6 tabs
            Ui.Clear(_tabs);
            IReadOnlyList<JournalTabEntry> entries = JournalBookOf.TabsOf(_book, _page);
            int n = Math.Max(1, entries.Count);
            float tabW = (ContentW - TabsGap * (n - 1)) / n;
            bool n5 = entries.Count == 5, n6 = entries.Count >= 6;
            for (int i = 0; i < entries.Count; i++) MakeTab(entries[i], i * (tabW + TabsGap), tabW, n5, n6);

            // the row below a tab that holds several pages: the enemies met while exploring, the ダンジョン tab's pages
            Ui.Clear(_subtabs);
            List<JPage> tabPages = _page != null && (_page.Tab == JTab.Enemy ? !_perEnemy : _page.Tab == JTab.Dungeon)
                ? PagesOf(_page.Tab) : new List<JPage>();
            bool sub = tabPages.Count > 1;
            float y = _tabsY + TabH;
            UiNode.Of(_subtabs).Visible = sub;
            if (sub)
            {
                float subH = DrawSubtabs(tabPages);
                UiNode.Of(_subtabs).Place(ContentX, y + SubTop, ContentW, subH);
                y += SubTop + subH;
            }

            // the title row and the body
            Ui.Clear(_titleRow);
            ClearBody();
            float rowH = DrawTitleRow(y + TitleTop);
            float bodyY = y + TitleTop + rowH;
            Ui.Place(_body.Viewport, ContentX, bodyY, ContentW, Math.Max(0f, BodyBottom - bodyY));
            _body.ScrollHeight = 0f;
            _body.ScrollTop = 0f;
            _body.Refresh();
            if (_page == null) return;

            float client = _body.ClientHeight;
            float minHeight = Math.Max(0f, client - BodyPadT - BodyPadB);
            float width = ContentW - BodyPadR;
            RectTransform list = BuildPage(width, minHeight, out float listH);
            if (BodyPadT + listH + BodyPadB > client + 0.5f)
            {
                // overflow-y: auto: the 8px scrollbar takes its room, and the rows lay out again narrower
                ClearBody();
                width -= ScrollbarW;
                list = BuildPage(width, minHeight, out listH);
            }
            _body.ScrollHeight = BodyPadT + listH + BodyPadB;
            _body.ScrollTop = 0f;
            _body.Refresh();
            if (animate)
            {
                global::Trial.Kit.Styler s = global::Trial.Kit.Styler.Of(list, o: 0f, y: 8f);
                _clock.Tween(s, new[] { ("o", 1f), ("y", 0f) }, 200f, Ease.Out);
            }
        }

        // js: scenes/meta/journal.js:298
        private RectTransform BuildPage(float width, float minHeight, out float height) =>
            WritesMemo(_page)
                ? MemoOf(_body.Content, _page, width, minHeight, out height)
                : RowsOf(_body.Content, _showsMemo ? EnemyPages.WithMemo(_page, _memoText, _book.Context) : _page, width, out height);

        private void ClearBody()
        {
            if (_sheet != null)
            {
                _sheet.Dispose();
                _sheet = null;
            }
            Ui.Clear(_body.Content);
        }

        // js: scenes/meta/journal.js:278
        private void MakeTab(JournalTabEntry t, float x, float w, bool n5, bool n6)
        {
            // the cascade: .ocbg-meta button, the tab, .is-on / .is-shut / .is-enemy (0,3,0), :hover:not([disabled]) (0,4,0),
            // then .is-n5 / .is-n6 (0,4,0, later in the sheet)
            string css = MetaStyle.ButtonCss + TabCss + (t.On ? TabOnCss : "") + (t.Open ? "" : TabShutCss) + (t.Enemy ? TabEnemyCss : "");
            string after = n5 ? TabN5Css : n6 ? TabN6Css : "";
            RectTransform rt = Ui.Div(_tabs, "ocbg-meta-drawer-tab" + (t.Enemy ? " is-enemy" : "") + (t.On ? " is-on" : "") + (t.Open ? "" : " is-shut"), x, 0f, w, TabH);
            UiNode.Of(rt);
            BoxGraphic box = BoxGraphic.On(rt);
            CssStates st = CssStates.Of(rt);
            st.Base(MetaStyle.Without(css, "width", "height"));
            st.State(":hover", TabHoverCss);
            st.State(".is-n", MetaStyle.Without(after, "width", "height"), after.Length > 0);
            st.Apply(instant: true);

            // the label: centred in the content box (46 − the 3px bottom border), keep-all where the rule says so
            TextStyle s = TextOf(css + after);
            s.Align = TextAlign.Center;
            bool keepAll = t.Enemy || n5 || n6;
            float pad = n5 || n6 ? 2f : t.Enemy ? 4f : 0f;
            float cw = Math.Max(1f, w - pad * 2f);
            string label = keepAll ? KeepAll(t.Label, s, cw) : t.Label;
            if (keepAll) s.WhiteSpace = WhiteSpace.PreLine;
            float h = TextMeasure.Height(label, s, cw);
            Text(rt, label, s, pad, (TabH - TabBorderB - h) / 2f, cw, h, "text");

            ClickArea area = ClickArea.On(box);
            area.HoverChanged += on => { if (st) st.SetState(":hover", on && t.Open); };
            area.Click += _ => { if (t.Open) Go(t.Page); };
            TitleTip.On(box, t.Label);
        }

        /// <summary>
        /// word-break: keep-all; overflow-wrap: anywhere (meta.css:365-367): a name breaks only at its spaces; a word wider
        /// than the tab still breaks anywhere (TMP's own wrapping of the line). Returns the text with its line breaks.
        /// </summary>
        private static string KeepAll(string text, TextStyle s, float width)
        {
            if (string.IsNullOrEmpty(text) || TextMeasure.Width(text, s) <= width + 0.01f) return text ?? "";
            var lines = new List<string>();
            string line = null;
            foreach (string word in text.Split(' '))
            {
                string next = line == null ? word : line + " " + word;
                if (line != null && TextMeasure.Width(next, s) > width + 0.01f)
                {
                    lines.Add(line);
                    line = word;
                }
                else line = next;
            }
            if (line != null) lines.Add(line);
            return string.Join("\n", lines);
        }

        // js: scenes/meta/journal.js:289
        /// <summary>The sub-tabs (flex, wrap, gap 6 8; a line is as tall as its tallest tag). Returns the row's height.</summary>
        private float DrawSubtabs(List<JPage> pages)
        {
            var items = new List<(JPage P, string Label, bool On, float W, float H, TextStyle S, float Bw)>();
            foreach (JPage p in pages)
            {
                bool on = _page != null && p.Id == _page.Id;
                string label = SubLabel(p);
                TextStyle s = TextOf(MetaStyle.ButtonCss + SubtabCss + (on ? SubtabOnCss : ""));
                s.WhiteSpace = WhiteSpace.NoWrap;
                s.Align = TextAlign.Center;
                float bw = on ? 2f : 1f;
                items.Add((p, label, on, TextMeasure.Width(label, s) + SubPadX * 2f + bw * 2f, s.LineHeightPx + SubPadY * 2f + bw * 2f, s, bw));
            }
            // flex-wrap lines
            var lines = new List<List<int>>();
            float x = 0f;
            foreach (int i in Enumerable.Range(0, items.Count))
            {
                if (lines.Count == 0 || (x > 0f && x + items[i].W > ContentW + 0.01f))
                {
                    lines.Add(new List<int>());
                    x = 0f;
                }
                lines[lines.Count - 1].Add(i);
                x += items[i].W + SubGapCol;
            }
            float y = 0f;
            for (int l = 0; l < lines.Count; l++)
            {
                float lineH = lines[l].Max(i => items[i].H);
                float lx = 0f;
                foreach (int i in lines[l])
                {
                    var it = items[i];
                    JPage target = it.P;
                    bool open = _book.CanOpen(target);
                    BoxGraphic tag = BoxGraphic.Make(_subtabs, MetaStyle.ButtonCss + SubtabCss + (it.On ? SubtabOnCss : ""), lx, y, it.W, lineH,
                        "ocbg-meta-drawer-subtab" + (it.On ? " is-on" : ""));
                    float lh = it.S.LineHeightPx;
                    Text(tag.rectTransform, it.Label, it.S, it.Bw + SubPadX, (lineH - lh) / 2f, Math.Max(1f, it.W - (it.Bw + SubPadX) * 2f) + 0.01f, lh, "text");
                    ClickArea area = ClickArea.On(tag);
                    area.Click += _ => { if (open) Go(target); };   // disabled: no click
                    lx += it.W + SubGapCol;
                }
                y += lineH + (l < lines.Count - 1 ? SubGapRow : 0f);
            }
            return y;
        }

        // js: scenes/meta/journal.js:293
        /// <summary>
        /// The title row (flex, baseline, space-between): the page's title and, for an enemy page, its 開示度 — dots and
        /// 「開示度 n / 2」 on the title's baseline; padding-bottom 12 and a rule under it, even with no page. Returns its height.
        /// </summary>
        private float DrawTitleRow(float y)
        {
            float h3H = 0f, contentH = 0f, h3Top = 0f, discTop = 0f, discW = 0f;
            TextStyle h3 = PhraseOf(TitleH3Css);
            TextStyle ds = TextOf(DisclosureCss);
            ds.WhiteSpace = WhiteSpace.NoWrap;
            string disc = _page != null ? _page.DisclosureLabel : null;
            bool hasDisc = !string.IsNullOrEmpty(disc);
            float dotsW = JPage.MaxDisclosure * DotSize + (JPage.MaxDisclosure - 1) * DotsGap;
            float discH = Math.Max(DotSize, ds.LineHeightPx);
            if (_page != null)
            {
                if (hasDisc) discW = dotsW + DisclosureGap + TextMeasure.Width(disc, ds);
                float h3W = Math.Max(1f, ContentW - (hasDisc ? discW + TitleGap : 0f));
                h3H = TextMeasure.Height(_page.Title, h3, h3W);
                // baselines: the h3's first; the disclosure's is its dots' (the startmost item, synthesized at its bottom)
                float h3Base = TextMath.CssBaselineFromTop(h3.Spec);
                float discBase = (discH - DotSize) / 2f + DotSize;
                float above = hasDisc ? Math.Max(h3Base, discBase) : h3Base;
                h3Top = above - h3Base;
                discTop = above - discBase;
                contentH = Math.Max(h3Top + h3H, hasDisc ? discTop + discH : 0f);
                Text(_titleRow, _page.Title, h3, 0f, h3Top, h3W, h3H, "h3");
            }
            float rowH = contentH + TitlePadB + TitleBorder;
            UiNode.Of(_titleRow).Place(ContentX, y, ContentW, rowH);
            BoxGraphic rule = BoxGraphic.Make(_titleRow, TitleRowCss, 0f, 0f, ContentW, rowH, "border");
            rule.transform.SetAsFirstSibling();
            if (_page != null && hasDisc)
            {
                RectTransform d = Ui.Div(_titleRow, "ocbg-meta-disclosure", ContentW - discW, discTop, discW, discH);
                DisclosureDots(d, _page, 0f, (discH - DotSize) / 2f);
                Text(d, disc, ds, dotsW + DisclosureGap, (discH - ds.LineHeightPx) / 2f, TextMeasure.Width(disc, ds) + 0.01f, ds.LineHeightPx, "text");
            }
            return rowH;
        }

        // js: scenes/meta/journal.js:302
        private static RectTransform DisclosureDots(RectTransform parent, JPage p, float x, float y)
        {
            float w = JPage.MaxDisclosure * DotSize + (JPage.MaxDisclosure - 1) * DotsGap;
            RectTransform dots = Ui.Div(parent, "ocbg-meta-disclosure-dots", x, y, w, DotSize);
            for (int i = 1; i <= JPage.MaxDisclosure; i++)
            {
                bool on = i <= p.Disclosure;
                BoxGraphic.Make(dots, DotCss + (on ? DotOnCss : ""), (i - 1) * (DotSize + DotsGap), 0f, DotSize, DotSize, on ? "i is-on" : "i");
            }
            return dots;
        }

        // js: scenes/meta/journal.js:316
        /// <summary>close(): the memo handed over, journalClose, the shade out (180 ms in) and the sheet back out to the right; then ctx.onClose().</summary>
        public void Close() => CloseAsync().Forget();

        private async Promise CloseAsync()
        {
            if (_closing) return;
            _closing = true;
            FlushMemo();
            _sound.Play("journalClose");
            _clock.Tween(_ps, "o", 0f, 180f, Ease.In).Forget();
            await _clock.Tween(_pd, "x", DrawerW, 180f, Ease.In);
            if (_alive && _ctx.OnClose != null) _ctx.OnClose();
        }

        // js: scenes/meta/journal.js:327
        /// <summary>←→ walks the pages that may be opened here, in tab order.</summary>
        private void Step(int by)
        {
            List<JPage> open = _book.Pages.Where(_book.CanOpen).ToList();
            if (open.Count == 0) return;
            int i = Math.Max(0, open.FindIndex(p => _page != null && p.Id == _page.Id));
            Go(open[(i + by + open.Count) % open.Count]);
        }

        // js: scenes/meta/journal.js:333
        private bool OnKey(KeyEvent e)
        {
            if (_closing) return true;
            // the sheet's own keydown (journal.js:260): Esc leaves it and closes the drawer; while the IME composes, Esc is
            // the IME's (the browser's keydown says 'Process'). The input field may have seen the same Esc first this frame
            // (it deactivates itself but stays selected): the sheet is still left, so the 4% focus ground goes at once.
            if (e.Key == "Escape" && _sheet != null && (_sheet.Focused || _sheet.Selected))
            {
                if (_sheet.Composing) return true;
                if (!e.Repeat)
                {
                    _sheet.Blur();
                    Close();
                }
                return true;
            }
            // Only a fresh press closes: a held J or Esc repeats, and the screen under the drawer would open it again on
            // the next repeat (its openers ignore repeats too).
            if (e.Key == "Escape" || e.Key == "j" || e.Key == "J")
            {
                if (!e.Repeat) Close();
                return true;
            }
            if (e.Key == "ArrowLeft") { Step(-1); return true; }
            if (e.Key == "ArrowRight" || e.Key == "Tab") { Step(1); return true; }
            if (e.Key == "ArrowDown") { _body.ScrollTop += 60f; return true; }
            if (e.Key == "ArrowUp") { _body.ScrollTop -= 60f; return true; }
            return MetaUtil.ModalSwallow(e);
        }

        /// <summary>
        /// Tab in the sheet: the browser moves the focus out of the textarea (its keydown stops the event, so the drawer's
        /// Tab does not step). KeyStack shows the capture listeners every key, the sheet's included.
        /// </summary>
        // js: scenes/meta/journal.js:260
        private void OnKeyCapture(KeyEvent e)
        {
            if (e.Key == "Tab" && _sheet != null && _sheet.Focused && !_closing) _sheet.Blur();
        }

        // js: scenes/meta/journal.js:347
        public void Destroy()
        {
            if (_destroyed) return;
            _destroyed = true;
            _alive = false;
            if (_memoTimer) FlushMemo();
            _popKeys?.Invoke();
            _popKeys = null;
            KeyStack.KeyDownCapture -= OnKeyCapture;
            if (_sheet != null)
            {
                _sheet.Dispose();
                _sheet = null;
            }
            _clock.Stop();
            Ui.Remove(_root);
        }

        /// <summary>The JS drawer has no refresh().</summary>
        public void Refresh() { }

        // ---- the memo sheet (the textarea) ---------------------------------------------------------------------------

        /// <summary>
        /// textarea.ocbg-meta-memo-sheet as a TMP_InputField: Shippori 400 20px/36px, .04em, washi-ink text and caret,
        /// padding 4 6, a ruled line every 36px that scrolls with the text (background-attachment: local), the page's empty
        /// line as the placeholder (.55), washi-ink 4% while focused, and the textarea's own 8px steel scrollbar when the
        /// text outgrows it. Typing reaches OnMemoInput (journal.js:254).
        /// </summary>
        private sealed class MemoSheet
        {
            private readonly JournalDrawer _owner;
            private readonly float _w, _h;
            private readonly RectTransform _rt;
            private readonly BoxGraphic _bg, _rules, _thumb;
            private readonly TextMeshProUGUI _text, _placeholder;
            private readonly JournalMemoInput _input;
            private readonly JournalMemoIme _ime;
            private readonly TextStyle _style;
            private readonly float _marginTop, _marginRight;
            private BaseInputModule _module;
            private BaseInput _prevOverride;
            private bool _over;
            private bool _disposed;

            public MemoSheet(JournalDrawer owner, RectTransform parent, float x, float y, float w, float h, string text, string placeholder)
            {
                _owner = owner;
                _w = w;
                _h = h;
                _rt = Ui.Div(parent, "ocbg-meta-memo-sheet", x, y, w, h);
                GameObject go = _rt.gameObject;
                go.SetActive(false);

                // background-color (transparent; washi-ink 4% on :focus), the element's own box: it takes the pointer
                _bg = BoxGraphic.On(_rt, "background-color: transparent;");
                _bg.raycastTarget = true;

                // the scrolling area (the padding box clips the text)
                RectTransform area = Ui.Div(_rt, "text-area");
                Ui.Fill(area);
                area.gameObject.AddComponent<RectMask2D>();

                // background-image + background-size: the ruled lines, moved with the text (Sync)
                _rules = BoxGraphic.Make(area, MemoSheetCss, 0f, 0f, w, h, "rules");

                _style = TextOf(MemoSheetCss);
                _style.WhiteSpace = WhiteSpace.PreWrap;   // a textarea's white-space
                TextStyle ph = _style.Clone();
                ph.Color = DsTokens.WashiInk.MulAlpha(0.55f);   // MemoPlaceholderCss: color washi-ink, opacity .55
                _placeholder = NewText(area, "placeholder", ph);
                _placeholder.text = placeholder ?? "";
                _text = NewText(area, "text", _style);
                LabelState st = Label.StateOf(_text);
                _marginTop = st.HalfLeading + 4f;    // padding-top 4
                _marginRight = 6f + st.MarginRight; // padding-right 6 (+ the trailing letter-spacing)
                SetMargins(_placeholder, false);
                SetMargins(_text, false);

                // ::-webkit-scrollbar (8px) and its thumb (meta.css:24-25: steel, radius 4)
                _thumb = BoxGraphic.Make(_rt, MetaStyle.ScrollbarThumbCss, w - ScrollbarW, 0f, ScrollbarW, 18f, "thumb");
                _thumb.gameObject.SetActive(false);

                _ime = go.AddComponent<JournalMemoIme>();
                _input = go.AddComponent<JournalMemoInput>();
                _input.Outer = owner._body;
                _input.transition = Selectable.Transition.None;
                _input.navigation = new Navigation { mode = Navigation.Mode.None };
                _input.targetGraphic = _bg;
                _input.textViewport = area;
                _input.textComponent = _text;
                _input.placeholder = _placeholder;
                _input.lineType = TMP_InputField.LineType.MultiLineNewline;
                _input.richText = false;
                _input.restoreOriginalTextOnEscape = false;
                _input.onFocusSelectAll = false;
                // A blurred textarea shows no caret and no selection. With resetOnDeActivation false TMP keeps the selection
                // "still active" after a deselect (Tab, a click elsewhere) and goes on blinking the caret in the unfocused sheet.
                _input.resetOnDeActivation = true;
                _input.customCaretColor = true;
                _input.caretBlinkRate = 1000f / 1060f;   // Chrome on Windows: the system's 530 ms on, 530 ms off
                ApplyColors();
                _input.SetTextWithoutNotify(text ?? "");
                _input.onValueChanged.AddListener(OnValue);
                _input.onSelect.AddListener(_ => OnFocus(true));
                _input.onDeselect.AddListener(_ => OnFocus(false));
                Materials.GammaEncodingChanged += ApplyColors;
                Canvas.willRenderCanvases += Sync;
                go.SetActive(true);
                // TMP_InputField.OnEnable put its caret (and selection) object first under the viewport; the ruled lines are
                // the textarea's background and go under it, as Chrome paints background, selection, text, caret.
                _rules.rectTransform.SetAsFirstSibling();
                Measure(text ?? "");
            }

            /// <summary>The sheet has the keyboard (:focus).</summary>
            public bool Focused => !_disposed && _input && _input.isFocused;

            /// <summary>The EventSystem still has the sheet selected (the field deactivates itself on its own Esc and stays selected).</summary>
            public bool Selected
            {
                get
                {
                    if (_disposed || !_input) return false;
                    EventSystem es = EventSystem.current;
                    return es && es.currentSelectedGameObject == _input.gameObject;
                }
            }

            /// <summary>The IME is composing (or just stopped: its Esc cancels the composition, not the drawer).</summary>
            public bool Composing => _ime && _ime.Composing;

            private static TextMeshProUGUI NewText(RectTransform parent, string name, TextStyle style)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.layer = parent.gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var t = go.AddComponent<TextMeshProUGUI>();
                Label.Apply(t, style);
                t.raycastTarget = false;
                return t;
            }

            private void SetMargins(TMP_Text t, bool scrollbar) =>
                t.margin = new Vector4(6f, _marginTop, _marginRight + (scrollbar ? ScrollbarW : 0f), 4f);

            private void ApplyColors()
            {
                if (!_input) return;
                _input.caretColor = Materials.VertexColor(DsTokens.WashiInk);              // caret-color: var(--washi-ink)
                _input.selectionColor = Materials.VertexColor(Rgba.Bytes(168, 206, 255, 0.75f));
            }

            // js: scenes/meta/journal.js:254
            private void OnValue(string value)
            {
                Measure(value);
                _owner.OnMemoInput(value);
            }

            /// <summary>
            /// Whether the text outgrows the sheet laid out at its full width (padding 6 + 6): the textarea then shows its
            /// scrollbar and wraps 8px narrower. Text that fits at the full width shows none.
            /// </summary>
            private void Measure(string value)
            {
                float inner = Math.Max(1f, _w - 12f);
                bool over = !string.IsNullOrEmpty(value) && TextMeasure.Height(value, _style, inner) + 8f > _h + 0.5f;
                if (over == _over) return;
                _over = over;
                SetMargins(_text, over);
            }

            private void OnFocus(bool on)
            {
                if (_disposed) return;
                if (_bg) _bg.SetCss("background-color: transparent;" + (on ? MemoFocusCss : ""));
                if (on) InstallIme();
                else RemoveIme();
            }

            /// <summary>The Input System's IME in place of the legacy Input class's while the sheet is focused.</summary>
            private void InstallIme()
            {
                EventSystem es = EventSystem.current;
                BaseInputModule m = es ? es.currentInputModule : null;
                if (!m || !_ime) return;
                if (m.inputOverride != _ime)
                {
                    _prevOverride = m.inputOverride;
                    m.inputOverride = _ime;
                }
                _module = m;
                _ime.Hook();
            }

            private void RemoveIme()
            {
                if (_module && _module.inputOverride == _ime) _module.inputOverride = _prevOverride;
                _module = null;
                _prevOverride = null;
                if (_ime) _ime.Unhook();
            }

            /// <summary>textarea.blur().</summary>
            public void Blur()
            {
                if (!_input) return;
                EventSystem es = EventSystem.current;
                if (es && es.currentSelectedGameObject == _input.gameObject) es.SetSelectedGameObject(null);
                else _input.DeactivateInputField();
            }

            /// <summary>
            /// Every frame after the canvases' rebuild (the input field has placed its text by then): the ruled lines follow
            /// the text's scroll (background-attachment: local) over the whole scrolled area, and the thumb follows it.
            /// </summary>
            private void Sync()
            {
                if (!_rt || !_text)
                {
                    // destroyed without Dispose (the play session ended): a static event outlives it with domain reload off
                    Canvas.willRenderCanvases -= Sync;
                    return;
                }
                if (_disposed) return;
                float top = Math.Max(0f, _text.rectTransform.anchoredPosition.y);
                int lines = _text.textInfo != null ? _text.textInfo.lineCount : 0;
                float scrollH = Math.Max(_h, lines * _style.LineHeightPx + 8f);
                bool show = _over && scrollH > _h + 0.5f;
                // the background fills the padding box: with the scrollbar shown, its 8px gutter (track transparent) has no lines
                Ui.Place(_rules.rectTransform, 0f, -top, show ? _w - ScrollbarW : _w, scrollH + _style.LineHeightPx);
                if (_thumb.gameObject.activeSelf != show) _thumb.gameObject.SetActive(show);
                if (!show) return;
                float len = Math.Max(18f, _h * _h / scrollH);
                float max = scrollH - _h;
                float ty = max > 0f ? Math.Min(1f, top / max) * (_h - len) : 0f;
                Ui.Place(_thumb.rectTransform, _w - ScrollbarW, ty, ScrollbarW, len);
            }

            public void Dispose()
            {
                if (_disposed) return;
                if (_input && _input.isFocused) Blur();
                RemoveIme();
                _disposed = true;
                Canvas.willRenderCanvases -= Sync;
                Materials.GammaEncodingChanged -= ApplyColors;
                EventSystem es = EventSystem.current;
                if (es && _rt && es.currentSelectedGameObject && es.currentSelectedGameObject.transform.IsChildOf(_rt)) es.SetSelectedGameObject(null);
            }
        }
    }

    /// <summary>
    /// The memo sheet's input field, typed into as a textarea is: the Tab key types no tab character (Tab moves the focus
    /// out); Shift+Enter breaks the line as Enter does (TMP turns it into a vertical tab, which it drops); a pasted CR LF
    /// is one line break (the textarea's value normalizes line breaks to LF). The wheel over a sheet with nothing to
    /// scroll, or already at its end in the wheel's direction, goes on to the drawer's body (scroll chaining); otherwise
    /// it moves the sheet 100px a notch, as Chrome's wheel does.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class JournalMemoInput : TMP_InputField
    {
        /// <summary>The drawer's body (the scroll container around the sheet).</summary>
        public ScrollArea Outer;

        // KeyPressed hands the typed character here first: '\t' is the Tab key, '\v' is Shift+Enter.
        protected override bool IsValidChar(char c) => c != '\t' && (c == '\v' || base.IsValidChar(c));

        // Every character inserted comes through here (typed, committed by the IME, pasted).
        protected override void Append(char input)
        {
            if (input == '\r') return;          // pasted CR LF: the LF that follows is the line break
            if (input == '\v') input = '\n';    // Shift+Enter
            base.Append(input);
        }

        public override void OnScroll(PointerEventData eventData)
        {
            if (!textComponent || !textViewport) return;
            float content = textComponent.preferredHeight, view = textViewport.rect.height;
            float max = content - view;
            float down = -eventData.scrollDelta.y;   // > 0: the wheel turned toward the player, the text moves up
            float top = textComponent.rectTransform.anchoredPosition.y;
            if (max <= 0.5f || (down > 0f && top >= max - 0.5f) || (down < 0f && top <= 0.5f))
            {
                if (Outer) Outer.OnScroll(eventData);
                return;
            }
            float perTick = 1f;
            if (EventSystem.current && EventSystem.current.currentInputModule is InputSystemUIInputModule m && Math.Abs(m.scrollDeltaPerTick) > 1e-5f)
                perTick = m.scrollDeltaPerTick;
            int lines = Math.Max(1, textComponent.textInfo != null ? textComponent.textInfo.lineCount : 1);
            // TMP moves (1 / lines) × delta × sensitivity of the overflow per event: 100 px a notch
            scrollSensitivity = 100f / perTick * lines / Math.Max(1f, content - view);
            base.OnScroll(eventData);
        }
    }

    /// <summary>
    /// The input module's BaseInput while the memo sheet has the focus: TMP_InputField reads the IME composition and sets
    /// the IME mode and candidate-window position through it. The stock BaseInput forwards to the legacy Input class; this
    /// one uses the Input System (Keyboard.onIMECompositionChange, SetIMEEnabled, SetIMECursorPosition) and never touches
    /// the legacy class.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class JournalMemoIme : BaseInput
    {
        private Keyboard _kb;
        private string _composition = "";
        private IMECompositionMode _mode = IMECompositionMode.Auto;
        private Vector2 _cursor;
        private int _lastComposingFrame = -10;

        /// <summary>A composition is open, or closed this frame or the last (the key that closed it is the IME's).</summary>
        public bool Composing => _composition.Length > 0 || Time.frameCount - _lastComposingFrame <= 1;

        public void Hook()
        {
            Keyboard kb = Keyboard.current;
            if (kb == _kb) return;
            Unhook();
            _kb = kb;
            if (_kb != null) _kb.onIMECompositionChange += OnComposition;
        }

        public void Unhook()
        {
            if (_kb != null) _kb.onIMECompositionChange -= OnComposition;
            _kb = null;
            if (_composition.Length > 0) _lastComposingFrame = Time.frameCount;
            _composition = "";
        }

        private void OnComposition(IMECompositionString s)
        {
            string next = s.ToString() ?? "";
            // The frame a composition ends counts too: an Esc that cancels it arrives with the empty composition, and that
            // Esc is the IME's (the browser's keydown says 'Process'), not the drawer's.
            if (_composition.Length > 0 || next.Length > 0) _lastComposingFrame = Time.frameCount;
            _composition = next;
        }

        public override string compositionString => _composition;

        public override IMECompositionMode imeCompositionMode
        {
            get => _mode;
            set
            {
                _mode = value;
                if (value != IMECompositionMode.On) return;
                try { Keyboard.current?.SetIMEEnabled(true); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public override Vector2 compositionCursorPos
        {
            get => _cursor;
            set
            {
                _cursor = value;
                try { Keyboard.current?.SetIMECursorPosition(value); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public override bool mousePresent => Mouse.current != null;

        public override bool GetMouseButtonDown(int button) => Button(button, b => b.wasPressedThisFrame);

        public override bool GetMouseButtonUp(int button) => Button(button, b => b.wasReleasedThisFrame);

        public override bool GetMouseButton(int button) => Button(button, b => b.isPressed);

        private static bool Button(int button, Func<UnityEngine.InputSystem.Controls.ButtonControl, bool> read)
        {
            Mouse m = Mouse.current;
            if (m == null) return false;
            UnityEngine.InputSystem.Controls.ButtonControl b = button == 0 ? m.leftButton : button == 1 ? m.rightButton : button == 2 ? m.middleButton : null;
            return b != null && read(b);
        }

        public override Vector2 mousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        public override Vector2 mouseScrollDelta => Mouse.current != null ? Mouse.current.scroll.ReadValue() : Vector2.zero;

        public override bool touchSupported => false;

        public override int touchCount => 0;

        public override Touch GetTouch(int index) => default;

        public override float GetAxisRaw(string axisName) => 0f;

        public override bool GetButtonDown(string buttonName) => false;

        protected override void OnDestroy()
        {
            Unhook();
            base.OnDestroy();
        }
    }
}
