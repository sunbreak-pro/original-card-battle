// The journal drawer's book and tab row, without the drawer (scenes/meta/journal.js:20-156): which pages it
// reads (a book handed in, a list of pages, or the running life's, read through ExplorationScreenBuilder like the
// map's drawer), a page per enemy of the fight on screen at the 開示度 the battle reads, the 用語 page with
// 「スタンス」 under 「構え」 (X-17), the ダンジョン tab's 希少な頁 page (R-27), and the tabs in the order 敵ごと /
// ダンジョン / メモ / 用語 (battle_ui_ux_v2.md §2.4, dungeon_exploration_v4.md §6.7). The drawer (Trial.Meta) lays
// the result out; every word comes from here, Journal.Script, or the enemy pages.
//
// The enemies' journal pages (EnemyPages.Has / Page, Trial.Bridge) are not in C# yet: they come in through
// JournalCtx.HasEnemyPage / EnemyPage. Without them the drawer reads as the JS does for an enemy that has no page.
using System;
using System.Collections.Generic;
using System.Linq;
using Exploration;
using Exploration.Bridge;
using Journal;

namespace Trial.Script
{
    /// <summary>An enemy of the fight on screen, as the battle hands it (js: ctx.enemies[i] = { Id, Name, Rows? }).</summary>
    public sealed class JournalFoe
    {
        public JournalFoe(string id, string name = null, IReadOnlyList<JournalRow> rows = null)
        {
            Id = id;
            Name = name;
            Rows = rows;
        }

        public string Id { get; }

        /// <summary>The page's title; null falls back to the id.</summary>
        public string Name { get; }

        /// <summary>The page's rows when the battle has them; null: the enemy's own page (EnemyPages) or the pending line.</summary>
        public IReadOnlyList<JournalRow> Rows { get; }
    }

    /// <summary>
    /// What the drawer is opened with: the engine-free fields of mountJournal's ctx (journal.js:158-168). The
    /// drawer reads Mode, Book / Pages / Expedition, Enemies / EnemyIds, DisclosureOf and PageId through
    /// <see cref="JournalBookOf.BookOf"/>; Memo, Scribbles, OnMemo and OnClose are the drawer's own.
    /// </summary>
    public sealed class JournalCtx
    {
        public const string Battle = "battle";
        public const string Explore = "explore";

        /// <summary>"battle" or "explore"; anything else takes the handed-in book's context (else exploring).</summary>
        public string Mode;

        /// <summary>A book handed in (its pages are read first).</summary>
        public JournalBook Book;

        /// <summary>Pages handed in, when no book is.</summary>
        public IReadOnlyList<JournalPage> Pages;

        /// <summary>The running life: its book when none is handed in, its 開示度 and its rare pages.</summary>
        public Expedition Expedition;

        /// <summary>The fight's enemies with their names (and rows).</summary>
        public IReadOnlyList<JournalFoe> Enemies;

        /// <summary>The fight's enemies by id (named from the core's roster).</summary>
        public IReadOnlyList<string> EnemyIds;

        /// <summary>The 開示度 of an enemy (e.g. Expedition.DisclosureOf); null reads the expedition's, or 0.</summary>
        public Func<string, int> DisclosureOf;

        /// <summary>
        /// js: EnemyPages.Has — the enemy has a journal page of its own (enemy_roster_v4.md). Wire it to Trial.Bridge's
        /// EnemyPages.Has; null: no enemy has one (every foe page reads the pending line).
        /// </summary>
        public Func<string, bool> HasEnemyPage;

        /// <summary>js: EnemyPages.Page(id, 開示度) — wire it to Trial.Bridge's EnemyPages.Page.</summary>
        public Func<string, int, JournalPage> EnemyPage;

        /// <summary>The page to open on (book.Find); null: the first that opens here.</summary>
        public string PageId;

        /// <summary>The player's own memo text.</summary>
        public string Memo;

        /// <summary>The 走り書き handed down ({ Generation, Name, Text }).</summary>
        public IReadOnlyList<InheritanceCore.Scribble> Scribbles;

        /// <summary>Makes the メモ page a sheet the player writes on.</summary>
        public Action<string> OnMemo;

        public Action OnClose;
    }

    /// <summary>One tab of the drawer's row (js: { label, open, on, page, enemy }).</summary>
    public sealed class JournalTabEntry
    {
        public JournalTabEntry(string label, bool open, bool on, JournalPage page, bool enemy)
        {
            Label = label;
            Open = open;
            On = on;
            Page = page;
            Enemy = enemy;
        }

        public string Label { get; }

        /// <summary>The tab may be pressed (a page under it opens here); a shut tab reads .32.</summary>
        public bool Open { get; }

        /// <summary>The open page is under this tab.</summary>
        public bool On { get; }

        /// <summary>The page the tab opens, or null.</summary>
        public JournalPage Page { get; }

        /// <summary>A battle's per-enemy tab (named after the enemy).</summary>
        public bool Enemy { get; }
    }

    public static class JournalBookOf
    {
        // js: scenes/meta/journal.js:20
        public static readonly IReadOnlyList<JournalTab> TabOrder = new System.Collections.ObjectModel.ReadOnlyCollection<JournalTab>(
            new[] { JournalTab.Enemy, JournalTab.Dungeon, JournalTab.Memo, JournalTab.Terms });

        /// <summary>The drawer's width: 30% of 1920.</summary>
        // js: scenes/meta/journal.js:21
        public const int DrawerW = 576;

        /// <summary>An enemy page's id: the drawer finds an enemy's page by it (EnemyPages.PageId: enemy-&lt;id&gt;).</summary>
        // js: bridge/journal/EnemyPages.js:249
        public static string EnemyPageId(string enemyId) => "enemy-" + enemyId;

        /// <summary>The exploration screen's own book (ExplorationScreenBuilder: ExplorationScreen.Journal) for a running life.</summary>
        // js: scenes/meta/journal.js:24
        public static List<JournalPage> ExpeditionPages(Expedition ex)
        {
            try
            {
                ExplorationSession session = ex != null ? ex.Session : null;
                if (session == null || session.State == null) return new List<JournalPage>();
                ExplorationScreen screen = ExplorationScreenBuilder.Build(session);
                return screen != null && screen.Journal != null ? screen.Journal.Pages.ToList() : new List<JournalPage>();
            }
            catch (Exception)
            {
                return new List<JournalPage>();
            }
        }

        /// <summary>The enemies of the fight on screen: ctx.Enemies, then each of ctx.EnemyIds not among them, named from the core's roster (an unknown id is its own name).</summary>
        // js: scenes/meta/journal.js:34
        public static List<JournalFoe> EnemiesOf(JournalCtx ctx)
        {
            var list = (ctx.Enemies ?? Array.Empty<JournalFoe>()).ToList();
            foreach (string id in ctx.EnemyIds ?? Array.Empty<string>())
            {
                if (list.Any(e => e.Id == id)) continue;
                string name = id;
                try
                {
                    name = BattleCore.Enemies.ById(id).Name;
                }
                catch (Exception)
                {
                    // unknown id: its id
                }
                list.Add(new JournalFoe(id, name));
            }
            return list;
        }

        /// <summary>
        /// The book the drawer reads: ctx.Book, or ctx.Pages, or the running life's (ctx.Expedition, read through
        /// ExplorationScreenBuilder like the map's drawer); plus a page per enemy of the fight on screen (ctx.Enemies /
        /// ctx.EnemyIds, 開示度 from ctx.DisclosureOf, e.g. Expedition.DisclosureOf) and the page of terms when none was
        /// handed in. In battle the fight's enemies come first and are the only enemy pages (the others are not this
        /// fight's), in the fight's order (battle_ui_ux_v2.md §2.4 「敵のタブは戦っている敵ごとに 1 つ」). Each foe's page
        /// is its roster page at DisclosureOf's 開示度 — in battle always, so the drawer reads the value the omens read;
        /// elsewhere only when the book has none (EnemyPages.Page: 名前と HP 最大だけ at 0, battle_ui_ux_v2.md §7).
        /// </summary>
        // js: scenes/meta/journal.js:55
        public static JournalBook BookOf(JournalCtx ctx)
        {
            JournalContext context = ctx.Mode == JournalCtx.Battle ? JournalContext.Battle
                : ctx.Mode == JournalCtx.Explore ? JournalContext.Exploration
                : ctx.Book != null ? ctx.Book.Context : JournalContext.Exploration;
            List<JournalPage> pages = ctx.Book != null ? ctx.Book.Pages.ToList()
                : ctx.Pages != null ? ctx.Pages.ToList()
                : ExpeditionPages(ctx.Expedition);
            List<JournalFoe> foes = EnemiesOf(ctx);
            List<string> foeIds = foes.Select(e => EnemyPageId(e.Id)).ToList();
            if (context == JournalContext.Battle && foes.Count > 0)
            {
                var ids = new HashSet<string>(foeIds, StringComparer.Ordinal);
                pages = pages.Where(p => p.Tab != JournalTab.Enemy || ids.Contains(p.Id)).ToList();
            }
            Func<string, int> disclosureOf = ctx.DisclosureOf
                ?? (ctx.Expedition != null ? ctx.Expedition.DisclosureOf : (Func<string, int>)null);
            // In battle the fight's pages are drawn at the 開示度 the battle reads (disclosureOf: the journal's, the
            // same value its omens take — concept-v3 §5), never at another screen's.
            bool redraw = context == JournalContext.Battle && disclosureOf != null;
            foreach (JournalFoe e in foes)
            {
                string id = e.Id;
                int at = pages.FindIndex(p => p.Id == EnemyPageId(id));
                if (at >= 0 && !redraw) continue;
                int level = Math.Max(0, Math.Min(JournalPage.MaxDisclosure, disclosureOf != null ? disclosureOf(id) : 0));
                JournalPage made = e.Rows != null
                    ? new JournalPage(EnemyPageId(id), JournalTab.Enemy, e.Name ?? id, e.Rows, level)
                    : ctx.HasEnemyPage != null && ctx.EnemyPage != null && id != null && ctx.HasEnemyPage(id)
                        ? ctx.EnemyPage(id, level)
                        : new JournalPage(EnemyPageId(id), JournalTab.Enemy, e.Name ?? id,
                            new[] { new JournalRow("", MetaText.EnemyPagePending) }, level);
                if (at >= 0) pages[at] = made;
                else pages.Add(made);
            }
            if (context == JournalContext.Battle && foes.Count > 0)
            {
                pages = pages.Where(p => p.Tab != JournalTab.Enemy)
                    .Concat(pages.Where(p => p.Tab == JournalTab.Enemy).OrderBy(p => foeIds.IndexOf(p.Id)))
                    .ToList();
            }
            if (!pages.Any(p => p.Tab == JournalTab.Terms)) pages.Add(JournalTerms.Page());
            pages = pages.Select(WithStanceTerm).ToList();
            pages = WithRarePage(pages, RarePagesPage(ctx.Expedition));
            List<JournalPage> ordered = TabOrder.SelectMany(tab => pages.Where(p => p.Tab == tab)).ToList();
            return new JournalBook(ordered, context);
        }

        /// <summary>
        /// The 希少な頁 page put after the layer's own pages on the ダンジョン tab (the layer first: it is the page the
        /// tab opens on), or last when there is none; nothing when <paramref name="rare"/> is null or already there.
        /// </summary>
        // js: scenes/meta/journal.js:86
        public static List<JournalPage> WithRarePage(IReadOnlyList<JournalPage> pages, JournalPage rare)
        {
            List<JournalPage> list = pages.ToList();
            if (rare == null || list.Any(p => p.Id == rare.Id)) return list;
            int last = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Tab == JournalTab.Dungeon) last = i;
            }
            list.Insert(last >= 0 ? last + 1 : list.Count, rare);
            return list;
        }

        /// <summary>
        /// JS extension (docs/EXTENSIONS.md X-17): the 用語 page with 「スタンス」 under 「構え」. The ported page
        /// (JournalTerms, the C#'s) defines 構え only as the end-of-turn Guard rule, while most stance cards are named
        /// 「〜の構え」: the row says the attribute apart from the rule. Added here, where the drawer shows the page, so
        /// the ported page stays the C#'s. Any other page, a page that has the row, or one without 構え comes back as is.
        /// </summary>
        // js: scenes/meta/journal.js:101
        public static JournalPage WithStanceTerm(JournalPage page)
        {
            if (page == null || page.Tab != JournalTab.Terms || page.Rows.Any(r => r.Label == MetaText.StanceTerm)) return page;
            int at = -1;
            for (int i = 0; i < page.Rows.Count; i++)
            {
                if (page.Rows[i].Label == "構え")
                {
                    at = i;
                    break;
                }
            }
            if (at < 0) return page;
            List<JournalRow> rows = page.Rows.ToList();
            rows.Insert(at + 1, new JournalRow(MetaText.StanceTerm, MetaText.StanceTermText));
            return new JournalPage(page.Id, page.Tab, page.Title, rows, page.Disclosure);
        }

        /// <summary>
        /// The ダンジョン tab's 希少な頁 page while the life holds any (Expedition.Pages): how many were handed down — a
        /// survivor's whole journal, the pages a trace gave (Expedition.InheritedPages) — and how many this life found
        /// (INHERITANCE.md R-27: inherited pages are not 拾った物). What a page holds is #107's: until then the page says
        /// plainly that the pages do nothing and that an empty page is no legacy pick (試作で決めたこと 9). Null without
        /// an expedition, without pages, or when its pages cannot be read.
        /// </summary>
        // js: scenes/meta/journal.js:116
        public static JournalPage RarePagesPage(Expedition ex)
        {
            int? total;
            int inherited;
            try
            {
                total = ex != null ? ex.Pages : (int?)null;
                inherited = ex != null ? ex.InheritedPages : 0;
            }
            catch (Exception)
            {
                return null;
            }
            return RarePagesPage(total, inherited);
        }

        /// <summary>
        /// <see cref="RarePagesPage(Expedition)"/> for the counts alone (the JS tests hand it { Pages, InheritedPages }):
        /// <paramref name="pages"/> null is no expedition; <paramref name="inheritedPages"/> is held to 0..pages.
        /// </summary>
        // js: scenes/meta/journal.js:116
        public static JournalPage RarePagesPage(int? pages, int? inheritedPages = null)
        {
            int total = pages ?? 0;
            int inherited = pages != null ? Math.Max(0, Math.Min(total, inheritedPages ?? 0)) : 0;
            if (total <= 0) return null;
            var rows = new List<JournalRow>();
            if (inherited > 0) rows.Add(new JournalRow(MetaText.RarePagesInherited, MetaText.RarePagesCount(inherited)));
            if (total - inherited > 0) rows.Add(new JournalRow(MetaText.RarePagesFound, MetaText.RarePagesCount(total - inherited)));
            rows.Add(new JournalRow("", MetaText.RarePagesPending));
            rows.Add(new JournalRow("", MetaText.RarePagesFate));
            return new JournalPage("rare-pages", JournalTab.Dungeon, MetaText.RarePagesTitle, rows);
        }

        /// <summary>In battle every enemy being fought is a tab of its own (battle_ui_ux_v2.md §2.4, §4.7).</summary>
        // js: scenes/meta/journal.js:133
        private static bool EnemyTabs(JournalBook book) => book.Context == JournalContext.Battle;

        /// <summary>
        /// The drawer's tab row for a book with <paramref name="page"/> open, left to right: 敵ごと / ダンジョン / メモ /
        /// 用語. In battle 敵ごと is one tab per enemy page, labelled with the enemy's name (JournalBook.TabText) — 4 tabs
        /// for one foe, 5 for two, 6 for three; while exploring it is the one 「敵」 tab (JournalBook.TabLabel), the
        /// enemies on the row below it. A tab whose pages may not be opened here (the battle's ダンジョン and メモ) is
        /// shown shut.
        /// </summary>
        // js: scenes/meta/journal.js:144
        public static IReadOnlyList<JournalTabEntry> TabsOf(JournalBook book, JournalPage page)
        {
            var output = new List<JournalTabEntry>();
            foreach (JournalTab tab in TabOrder)
            {
                List<JournalPage> pagesOf = book.Pages.Where(p => p.Tab == tab).ToList();
                if (tab == JournalTab.Enemy && EnemyTabs(book))
                {
                    foreach (JournalPage p in pagesOf)
                        output.Add(new JournalTabEntry(JournalBook.TabText(p), book.CanOpen(p), page != null && page.Id == p.Id, p, true));
                    continue;
                }
                JournalPage first = pagesOf.FirstOrDefault(book.CanOpen);
                output.Add(new JournalTabEntry(JournalBook.TabLabel(tab), first != null, page != null && page.Tab == tab, first, false));
            }
            return output;
        }
    }
}
