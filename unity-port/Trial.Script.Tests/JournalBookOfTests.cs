using System;
using System.Collections.Generic;
using System.Linq;
using Exploration;
using Exploration.Bridge;
using Journal;
using NUnit.Framework;

namespace Trial.Script.Tests
{
    /// <summary>
    /// The drawer's book and tabs (journal.js bookOf / tabsOf / withStanceTerm / rarePagesPage): the trial's J-16
    /// (test/ext/journal.test.mjs), R-27's page (test/app/inherit-gains.test.mjs) and X-17 (test/ext/last-round.test.mjs),
    /// with the parts that need JS-only modules (JournalIntel, EnemyPages, Expedition.InheritedPages) stood in or left
    /// out as marked.
    /// </summary>
    public class JournalBookOfTests
    {
        private static Expedition LifeWith(ulong seed, Dictionary<string, int> journal = null) =>
            new Expedition(new RunStart { Seed = seed }, journal: journal);

        private static List<(string, bool)> Labels(JournalBook book) =>
            JournalBookOf.TabsOf(book, book.First).Select(t => (t.Label, t.Open)).ToList();

        private static string NameOf(string enemyId) => BattleCore.Enemies.ById(enemyId).Name;

        // ------------------------------------------------------------------ J-16

        [Test]
        public void J16_InBattle_OneTabPerEnemyFought_Named_ThenDungeonMemoTerms_AtTheBattlesDisclosure()
        {
            Expedition ex = LifeWith(4, new Dictionary<string, int> { ["polearm_warped"] = 1, ["crossbow_hunter"] = 2, ["shadow_hound"] = 2 });

            JournalBook one = JournalBookOf.BookOf(new JournalCtx
            {
                Mode = JournalCtx.Battle, Expedition = ex, EnemyIds = new[] { "shadow_hound" }, DisclosureOf = ex.DisclosureOf,
            });
            Assert.That(Labels(one), Is.EqualTo(new List<(string, bool)>
            {
                ("瘴牙の走竜", true), ("ダンジョン", false), ("メモ", false), ("用語", true),
            }), "1 体戦は 4 (§4.7)");
            Assert.That(JournalBookOf.TabsOf(one, one.First)[0].On, Is.True);

            JournalBook two = JournalBookOf.BookOf(new JournalCtx
            {
                Mode = JournalCtx.Battle, Expedition = ex, EnemyIds = new[] { "polearm_warped", "crossbow_hunter" }, DisclosureOf = ex.DisclosureOf,
            });
            Assert.That(Labels(two).Select(l => l.Item1), Is.EqualTo(new[] { "錆槍の竜兵", "灰弩の竜兵", "ダンジョン", "メモ", "用語" }),
                "2 体戦は 5, in the fight's order");
            Assert.That(two.Pages.Where(p => p.Tab == JournalTab.Enemy).Select(p => (p.Id, p.Disclosure)),
                Is.EqualTo(new[] { ("enemy-polearm_warped", 1), ("enemy-crossbow_hunter", 2) }));

            JournalBook three = JournalBookOf.BookOf(new JournalCtx
            {
                Mode = JournalCtx.Battle, Expedition = ex, EnemyIds = new[] { "shadow_hound", "polearm_warped", "miasma_priest" }, DisclosureOf = ex.DisclosureOf,
            });
            Assert.That(JournalBookOf.TabsOf(three, three.First).Count, Is.EqualTo(6), "3 体戦は 6");
            Assert.That(three.Pages.First(p => p.Id == "enemy-miasma_priest").Disclosure, Is.EqualTo(0), "a first encounter: 名前と HP 最大だけ");

            // In battle a foe's page is drawn at the battle's value even when the book handed in says otherwise.
            var handedIn = new JournalPage("enemy-polearm_warped", JournalTab.Enemy, NameOf("polearm_warped"),
                new[] { new JournalRow("名前", NameOf("polearm_warped")) }, 2);
            JournalBook stale = JournalBookOf.BookOf(new JournalCtx
            {
                Mode = JournalCtx.Battle, Pages = new[] { handedIn }, EnemyIds = new[] { "polearm_warped" }, DisclosureOf = _ => 1,
            });
            Assert.That(stale.Pages.First(p => p.Id == "enemy-polearm_warped").Disclosure, Is.EqualTo(1));

            // While exploring the 敵 tab is one (every enemy met sits on the row under it), and every tab opens.
            // (The JS also checks the enemy pages the JournalIntel lists; the C# session has no JournalIntel yet.)
            JournalBook explore = JournalBookOf.BookOf(new JournalCtx { Mode = JournalCtx.Explore, Expedition = ex });
            Assert.That(Labels(explore), Is.EqualTo(new List<(string, bool)>
            {
                ("敵", true), ("ダンジョン", true), ("メモ", true), ("用語", true),
            }));
        }

        // What node printed for journal.js bookOf / tabsOf on an Expedition without a JournalIntel (the C#'s screen:
        // new Expedition(new RunStart({ Seed: 4n }), null, null, null, null, journal)). Each page as id, tab, title,
        // 開示度 and row count (an enemy page's rows are EnemyPages', which the C# has not yet: its count is not
        // compared), then each tab of tabsOf(book, book.First) as label, open, on, the page it opens, enemy.
        private static readonly Dictionary<string, int> ParityJournal =
            new Dictionary<string, int> { ["polearm_warped"] = 1, ["crossbow_hunter"] = 2, ["shadow_hound"] = 2 };

        private static void AssertBook(JournalBook book, JournalContext context,
            (string Id, JournalTab Tab, string Title, int Disclosure, int Rows)[] pages,
            (string Label, bool Open, bool On, string Page, bool Enemy)[] tabs)
        {
            Assert.That(book.Context, Is.EqualTo(context));
            Assert.That(book.Pages.Select(p => (p.Id, p.Tab, p.Title, p.Disclosure)),
                Is.EqualTo(pages.Select(p => (p.Id, p.Tab, p.Title, p.Disclosure))));
            Assert.That(book.Pages.Where(p => p.Tab != JournalTab.Enemy).Select(p => p.Rows.Count),
                Is.EqualTo(pages.Where(p => p.Tab != JournalTab.Enemy).Select(p => p.Rows)));
            Assert.That(JournalBookOf.TabsOf(book, book.First).Select(t => (t.Label, t.Open, t.On, t.Page?.Id, t.Enemy)),
                Is.EqualTo(tabs));
        }

        [Test]
        public void BookOfAndTabsOf_AreTheJs_OnALifeWithoutIntel()
        {
            Expedition ex = LifeWith(4, ParityJournal);
            const string layer1 = "第 1 層　燦光の樹海";

            AssertBook(JournalBookOf.BookOf(new JournalCtx { Mode = JournalCtx.Explore, Expedition = ex }), JournalContext.Exploration,
                new[]
                {
                    ("enemy-none", JournalTab.Enemy, "敵の頁", 0, 1), ("dungeon-1", JournalTab.Dungeon, layer1, 2, 4),
                    ("memo", JournalTab.Memo, "メモ", 2, 1), ("terms", JournalTab.Terms, "用語と状態", 2, 29),
                },
                new[]
                {
                    ("敵", true, true, "enemy-none", false), ("ダンジョン", true, false, "dungeon-1", false),
                    ("メモ", true, false, "memo", false), ("用語", true, false, "terms", false),
                });

            AssertBook(JournalBookOf.BookOf(new JournalCtx
                {
                    Mode = JournalCtx.Battle, Expedition = ex, EnemyIds = new[] { "shadow_hound" }, DisclosureOf = ex.DisclosureOf,
                }), JournalContext.Battle,
                new[]
                {
                    ("enemy-shadow_hound", JournalTab.Enemy, "瘴牙の走竜", 2, 8), ("dungeon-1", JournalTab.Dungeon, layer1, 2, 4),
                    ("memo", JournalTab.Memo, "メモ", 2, 1), ("terms", JournalTab.Terms, "用語と状態", 2, 29),
                },
                new[]
                {
                    ("瘴牙の走竜", true, true, "enemy-shadow_hound", true), ("ダンジョン", false, false, (string)null, false),
                    ("メモ", false, false, null, false), ("用語", true, false, "terms", false),
                });

            // an id twice: one page, one tab (the fight's order)
            AssertBook(JournalBookOf.BookOf(new JournalCtx
                {
                    Mode = JournalCtx.Battle, Expedition = ex, EnemyIds = new[] { "polearm_warped", "crossbow_hunter", "polearm_warped" },
                    DisclosureOf = ex.DisclosureOf,
                }), JournalContext.Battle,
                new[]
                {
                    ("enemy-polearm_warped", JournalTab.Enemy, "錆槍の竜兵", 1, 8), ("enemy-crossbow_hunter", JournalTab.Enemy, "灰弩の竜兵", 2, 8),
                    ("dungeon-1", JournalTab.Dungeon, layer1, 2, 4), ("memo", JournalTab.Memo, "メモ", 2, 1),
                    ("terms", JournalTab.Terms, "用語と状態", 2, 29),
                },
                new[]
                {
                    ("錆槍の竜兵", true, true, "enemy-polearm_warped", true), ("灰弩の竜兵", true, false, "enemy-crossbow_hunter", true),
                    ("ダンジョン", false, false, (string)null, false), ("メモ", false, false, null, false), ("用語", true, false, "terms", false),
                });

            // no disclosureOf handed in: the expedition's
            AssertBook(JournalBookOf.BookOf(new JournalCtx { Mode = JournalCtx.Battle, Expedition = ex, EnemyIds = new[] { "crossbow_hunter" } }),
                JournalContext.Battle,
                new[]
                {
                    ("enemy-crossbow_hunter", JournalTab.Enemy, "灰弩の竜兵", 2, 8), ("dungeon-1", JournalTab.Dungeon, layer1, 2, 4),
                    ("memo", JournalTab.Memo, "メモ", 2, 1), ("terms", JournalTab.Terms, "用語と状態", 2, 29),
                },
                new[]
                {
                    ("灰弩の竜兵", true, true, "enemy-crossbow_hunter", true), ("ダンジョン", false, false, (string)null, false),
                    ("メモ", false, false, null, false), ("用語", true, false, "terms", false),
                });

            // nothing handed in: the terms page alone
            AssertBook(JournalBookOf.BookOf(new JournalCtx()), JournalContext.Exploration,
                new[] { ("terms", JournalTab.Terms, "用語と状態", 2, 29) },
                new[]
                {
                    ("敵", false, false, (string)null, false), ("ダンジョン", false, false, null, false),
                    ("メモ", false, false, null, false), ("用語", true, true, "terms", false),
                });
        }

        [Test]
        public void J16_TheTabsNameTheCoresEnemies()
        {
            Assert.That(new[] { "shadow_hound", "polearm_warped", "crossbow_hunter" }.Select(NameOf),
                Is.EqualTo(new[] { "瘴牙の走竜", "錆槍の竜兵", "灰弩の竜兵" }));
        }

        // ------------------------------------------------------------------ bookOf's pieces

        [Test]
        public void AFoeWithoutAPageOfItsOwn_ReadsThePendingLine_AtItsDisclosure()
        {
            JournalBook book = JournalBookOf.BookOf(new JournalCtx
            {
                Mode = JournalCtx.Battle, Pages = Array.Empty<JournalPage>(), EnemyIds = new[] { "shadow_hound" }, DisclosureOf = _ => 5,
            });
            JournalPage page = book.Pages.First(p => p.Tab == JournalTab.Enemy);
            Assert.That(page.Id, Is.EqualTo("enemy-shadow_hound"));
            Assert.That(page.Title, Is.EqualTo(NameOf("shadow_hound")));
            Assert.That(page.Disclosure, Is.EqualTo(JournalPage.MaxDisclosure), "held to 0..2");
            Assert.That(page.Rows.Select(r => (r.Label, r.Text)), Is.EqualTo(new[] { ("", MetaText.EnemyPagePending) }));
        }

        [Test]
        public void AFoeWithAPageOfItsOwn_ReadsIt_ThroughTheEnemyPagesHook()
        {
            var asked = new List<(string, int)>();
            JournalBook book = JournalBookOf.BookOf(new JournalCtx
            {
                Mode = JournalCtx.Battle, Pages = Array.Empty<JournalPage>(), EnemyIds = new[] { "shadow_hound", "pack_alpha" }, DisclosureOf = _ => 1,
                HasEnemyPage = id => id == "shadow_hound",
                EnemyPage = (id, level) =>
                {
                    asked.Add((id, level));
                    return new JournalPage(JournalBookOf.EnemyPageId(id), JournalTab.Enemy, "roster", new[] { new JournalRow("名前", "roster") }, level);
                },
            });
            Assert.That(asked, Is.EqualTo(new[] { ("shadow_hound", 1) }));
            Assert.That(book.Pages.Where(p => p.Tab == JournalTab.Enemy).Select(p => p.Title), Is.EqualTo(new[] { "roster", NameOf("pack_alpha") }));
        }

        [Test]
        public void TheFightsRows_AndAnUnknownId()
        {
            var rows = new[] { new JournalRow("弱点", "火", 2) };
            JournalBook book = JournalBookOf.BookOf(new JournalCtx
            {
                Mode = JournalCtx.Battle, Pages = Array.Empty<JournalPage>(),
                Enemies = new[] { new JournalFoe("training_dummy", "木偶", rows) },
                EnemyIds = new[] { "training_dummy", "no_such_enemy" },
            });
            List<JournalPage> foes = book.Pages.Where(p => p.Tab == JournalTab.Enemy).ToList();
            Assert.That(foes.Select(p => (p.Id, p.Title, p.Disclosure)),
                Is.EqualTo(new[] { ("enemy-training_dummy", "木偶", 0), ("enemy-no_such_enemy", "no_such_enemy", 0) }),
                "listed once; no disclosureOf and no expedition read 0; an unknown id names itself");
            Assert.That(foes[0].Rows, Is.SameAs(rows));
        }

        [Test]
        public void WhileExploring_ABooksOwnEnemyPage_IsKept()
        {
            var own = new JournalPage("enemy-shadow_hound", JournalTab.Enemy, "own", new[] { new JournalRow("", "x") }, 2);
            JournalBook book = JournalBookOf.BookOf(new JournalCtx
            {
                Mode = JournalCtx.Explore, Pages = new[] { own }, EnemyIds = new[] { "shadow_hound", "pack_alpha" }, DisclosureOf = _ => 1,
            });
            List<JournalPage> foes = book.Pages.Where(p => p.Tab == JournalTab.Enemy).ToList();
            Assert.That(foes[0], Is.SameAs(own), "not redrawn outside a battle");
            Assert.That(foes[1].Id, Is.EqualTo("enemy-pack_alpha"), "a foe without a page gets one");
            Assert.That(foes[1].Disclosure, Is.EqualTo(1));
        }

        [Test]
        public void InBattle_OtherEnemiesPagesLeave_AndTheFightsComeInItsOrder()
        {
            JournalPage P(string id, JournalTab tab) => new JournalPage(id, tab, id, Array.Empty<JournalRow>(), tab == JournalTab.Enemy ? 1 : 2);
            var pages = new[]
            {
                P("enemy-pack_alpha", JournalTab.Enemy), P("enemy-shadow_hound", JournalTab.Enemy), P("dungeon-1", JournalTab.Dungeon),
                P("memo", JournalTab.Memo), P("enemy-polearm_warped", JournalTab.Enemy),
            };
            JournalBook book = JournalBookOf.BookOf(new JournalCtx { Mode = JournalCtx.Battle, Pages = pages, EnemyIds = new[] { "polearm_warped", "shadow_hound" } });
            Assert.That(book.Pages.Select(p => p.Id),
                Is.EqualTo(new[] { "enemy-polearm_warped", "enemy-shadow_hound", "dungeon-1", "memo", JournalTerms.PageId }));
            Assert.That(book.Context, Is.EqualTo(JournalContext.Battle));
            Assert.That(book.Pages[0], Is.SameAs(pages[4]), "kept as handed in: no disclosureOf to redraw at");
        }

        [Test]
        public void TheContext_FollowsTheMode_ThenTheBooksOwn()
        {
            var battleBook = new JournalBook(new[] { JournalTerms.Page() }, JournalContext.Battle);
            Assert.That(JournalBookOf.BookOf(new JournalCtx { Book = battleBook }).Context, Is.EqualTo(JournalContext.Battle));
            Assert.That(JournalBookOf.BookOf(new JournalCtx { Book = battleBook, Mode = JournalCtx.Explore }).Context, Is.EqualTo(JournalContext.Exploration));
            Assert.That(JournalBookOf.BookOf(new JournalCtx { Pages = Array.Empty<JournalPage>() }).Context, Is.EqualTo(JournalContext.Exploration));
        }

        [Test]
        public void TheTermsPage_IsAddedWhenMissing_WithTheStanceRow()
        {
            JournalBook book = JournalBookOf.BookOf(new JournalCtx { Pages = Array.Empty<JournalPage>() });
            JournalPage terms = book.Pages.Single();
            Assert.That(terms.Id, Is.EqualTo(JournalTerms.PageId));
            Assert.That(terms.Rows.Any(r => r.Label == MetaText.StanceTerm), Is.True);
        }

        [Test]
        public void TheBook_ReadsTheTabOrder()
        {
            JournalPage P(string id, JournalTab tab) => new JournalPage(id, tab, id, Array.Empty<JournalRow>());
            var pages = new[] { P("t", JournalTab.Terms), P("m", JournalTab.Memo), P("d", JournalTab.Dungeon), P("d2", JournalTab.Dungeon) };
            JournalBook book = JournalBookOf.BookOf(new JournalCtx { Mode = JournalCtx.Explore, Pages = pages });
            Assert.That(book.Pages.Select(p => p.Id), Is.EqualTo(new[] { "d", "d2", "m", "t" }));
        }

        [Test]
        public void TheRunningLifesBook_IsTheExplorationScreens()
        {
            Expedition ex = LifeWith(4);
            List<JournalPage> own = JournalBookOf.ExpeditionPages(ex);
            Assert.That(own.Select(p => p.Id), Is.EqualTo(ExplorationScreenBuilder.Build(ex.Session).Journal.Pages.Select(p => p.Id)));
            Assert.That(JournalBookOf.ExpeditionPages(null), Is.Empty);
        }

        // ------------------------------------------------------------------ R-27

        [Test]
        public void R27_TheRarePagesPage_SaysHowManyWereInherited_AndHowManyFound()
        {
            Assert.That(JournalBookOf.RarePagesPage((int?)null), Is.Null);
            Assert.That(JournalBookOf.RarePagesPage(0, 0), Is.Null, "no pages: no page");
            JournalPage both = JournalBookOf.RarePagesPage(6, 4);
            Assert.That(both.Title, Is.EqualTo(MetaText.RarePagesTitle));
            Assert.That(both.Tab, Is.EqualTo(JournalTab.Dungeon));
            Assert.That(both.Id, Is.EqualTo("rare-pages"));
            Assert.That(both.Rows.Select(r => (r.Label, r.Text)), Is.EqualTo(new[]
            {
                (MetaText.RarePagesInherited, MetaText.RarePagesCount(4)),
                (MetaText.RarePagesFound, MetaText.RarePagesCount(2)),
                ("", MetaText.RarePagesPending),
                ("", MetaText.RarePagesFate),
            }));
            JournalPage found = JournalBookOf.RarePagesPage(2);
            Assert.That(found.Rows.Select(r => r.Label), Is.EqualTo(new[] { MetaText.RarePagesFound, "", "" }), "without InheritedPages every page is found");
            JournalPage allInherited = JournalBookOf.RarePagesPage(3, 9);
            Assert.That(allInherited.Rows.Select(r => (r.Label, r.Text)).First(), Is.EqualTo((MetaText.RarePagesInherited, MetaText.RarePagesCount(3))), "held to the total");
            Assert.That(allInherited.Rows.Count, Is.EqualTo(3));
            // (the JS's last assertion reads a whole heir's life — app/life.js — and stays with the app's port)
        }

        [Test]
        public void R27_ALifeWithoutPages_HasNoRarePage()
        {
            Assert.That(JournalBookOf.RarePagesPage(LifeWith(4)), Is.Null);
            Assert.That(JournalBookOf.RarePagesPage((Expedition)null), Is.Null);
            JournalBook book = JournalBookOf.BookOf(new JournalCtx { Mode = JournalCtx.Explore, Expedition = LifeWith(4) });
            Assert.That(book.Pages.Any(p => p.Id == "rare-pages"), Is.False);
        }

        [Test]
        public void R27_AnHeirWhoSetOutWithPages_ReadsThemAsInherited_AfterTheLayersPage()
        {
            var ex = new Expedition(new RunStart { Seed = 4 }, options: new ExpeditionOptions { StartPages = 4 });
            Assert.That((ex.Pages, ex.InheritedPages), Is.EqualTo((4, 4)));
            JournalPage rare = JournalBookOf.RarePagesPage(ex);
            Assert.That(rare.Rows.Select(r => (r.Label, r.Text)), Is.EqualTo(new[]
            {
                (MetaText.RarePagesInherited, MetaText.RarePagesCount(4)),
                ("", MetaText.RarePagesPending),
                ("", MetaText.RarePagesFate),
            }));
            JournalBook book = JournalBookOf.BookOf(new JournalCtx { Mode = JournalCtx.Explore, Expedition = ex });
            List<string> dungeon = book.Pages.Where(p => p.Tab == JournalTab.Dungeon).Select(p => p.Id).ToList();
            Assert.That(dungeon.First(), Does.StartWith("dungeon-"), "the layer's page first: the tab opens on it");
            Assert.That(dungeon.Last(), Is.EqualTo("rare-pages"));
            Assert.That(JournalBookOf.TabsOf(book, book.First).First(t => t.Label == "ダンジョン").Page.Id, Does.StartWith("dungeon-"));
        }

        [Test]
        public void R27_TheRarePage_GoesAfterTheDungeonTabsPages()
        {
            JournalPage P(string id, JournalTab tab) => new JournalPage(id, tab, id, Array.Empty<JournalRow>());
            JournalPage rare = JournalBookOf.RarePagesPage(1);
            var pages = new[] { P("enemy-a", JournalTab.Enemy), P("dungeon-1", JournalTab.Dungeon), P("carving", JournalTab.Dungeon), P("memo", JournalTab.Memo) };
            Assert.That(JournalBookOf.WithRarePage(pages, rare).Select(p => p.Id),
                Is.EqualTo(new[] { "enemy-a", "dungeon-1", "carving", "rare-pages", "memo" }));
            Assert.That(JournalBookOf.WithRarePage(new[] { P("memo", JournalTab.Memo) }, rare).Select(p => p.Id),
                Is.EqualTo(new[] { "memo", "rare-pages" }), "no dungeon page: last");
            Assert.That(JournalBookOf.WithRarePage(new[] { rare }, JournalBookOf.RarePagesPage(2)).Single(), Is.SameAs(rare), "once");
            Assert.That(JournalBookOf.WithRarePage(pages, null).Count, Is.EqualTo(4));
        }

        // ------------------------------------------------------------------ X-17

        [Test]
        public void X17_TheTermsPageShowsStanceUnderKamae_Once_AndThePortedPageIsTheCSharps()
        {
            JournalPage ported = JournalTerms.Page();
            Assert.That(ported.Rows.Any(r => r.Label == MetaText.StanceTerm), Is.False);
            JournalPage page = JournalBookOf.WithStanceTerm(ported);
            List<string> labels = page.Rows.Select(r => r.Label).ToList();
            Assert.That(labels[labels.IndexOf("構え") + 1], Is.EqualTo(MetaText.StanceTerm));
            Assert.That(page.Rows.Count, Is.EqualTo(ported.Rows.Count + 1));
            Assert.That(JournalBookOf.WithStanceTerm(page).Rows.Count, Is.EqualTo(page.Rows.Count), "once");
            Assert.That((page.Id, page.Tab, page.Title, page.Disclosure), Is.EqualTo((ported.Id, ported.Tab, ported.Title, ported.Disclosure)));
            Assert.That(page.Rows[labels.IndexOf(MetaText.StanceTerm)].Text, Is.EqualTo(MetaText.StanceTermText));
        }

        [Test]
        public void X17_OtherPages_ComeBackAsTheyAre()
        {
            var memo = new JournalPage("memo", JournalTab.Memo, "メモ", new[] { new JournalRow("構え", "x") });
            Assert.That(JournalBookOf.WithStanceTerm(memo), Is.SameAs(memo));
            var noKamae = new JournalPage("terms", JournalTab.Terms, "用語", new[] { new JournalRow("間合い", "x") });
            Assert.That(JournalBookOf.WithStanceTerm(noKamae), Is.SameAs(noKamae));
            Assert.That(JournalBookOf.WithStanceTerm(null), Is.Null);
        }

        // ------------------------------------------------------------------ tabsOf

        [Test]
        public void TabsOf_WhileExploring_OneEnemyTab_OnFollowsTheOpenPage()
        {
            JournalPage P(string id, JournalTab tab) => new JournalPage(id, tab, id, Array.Empty<JournalRow>());
            var book = new JournalBook(new[] { P("enemy-a", JournalTab.Enemy), P("enemy-b", JournalTab.Enemy), P("dungeon-1", JournalTab.Dungeon), P("terms", JournalTab.Terms) },
                JournalContext.Exploration);
            IReadOnlyList<JournalTabEntry> tabs = JournalBookOf.TabsOf(book, book.Pages[1]);
            Assert.That(tabs.Select(t => (t.Label, t.Open, t.On, t.Enemy)), Is.EqualTo(new[]
            {
                ("敵", true, true, false), ("ダンジョン", true, false, false), ("メモ", false, false, false), ("用語", true, false, false),
            }));
            Assert.That(tabs[0].Page, Is.SameAs(book.Pages[0]), "the tab opens its first page");
            Assert.That(tabs[2].Page, Is.Null, "no メモ page: shut");
            Assert.That(JournalBookOf.TabsOf(book, null).Any(t => t.On), Is.False);
        }

        [Test]
        public void TabsOf_InBattle_EnemyTabsAreThePages_TheRestShut()
        {
            JournalPage P(string id, JournalTab tab, string title) => new JournalPage(id, tab, title, Array.Empty<JournalRow>());
            var book = new JournalBook(new[]
            {
                P("enemy-a", JournalTab.Enemy, "甲"), P("enemy-b", JournalTab.Enemy, "乙"), P("dungeon-1", JournalTab.Dungeon, "d"),
                P("memo", JournalTab.Memo, "m"), P("terms", JournalTab.Terms, "t"),
            }, JournalContext.Battle);
            IReadOnlyList<JournalTabEntry> tabs = JournalBookOf.TabsOf(book, book.Pages[4]);
            Assert.That(tabs.Select(t => (t.Label, t.Open, t.On, t.Enemy)), Is.EqualTo(new[]
            {
                ("甲", true, false, true), ("乙", true, false, true), ("ダンジョン", false, false, false), ("メモ", false, false, false), ("用語", true, true, false),
            }));
            Assert.That(tabs[2].Page, Is.Null, "a shut tab opens nothing");
        }
    }
}
