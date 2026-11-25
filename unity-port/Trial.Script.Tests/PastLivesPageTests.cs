using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;
using InheritanceCore;
using Journal;
using NUnit.Framework;
using JournalPage = Journal.JournalPage;

namespace Trial.Script.Tests
{
    /// <summary>
    /// The journal's page of past lives and the rules of traces (#1053): what the hall's list and rules print
    /// (HallScene: HallParts.LifeCard with MetaLines.PastTraceLine, HallParts.Rules), readable in the drawer outside
    /// battle and never in it.
    /// </summary>
    public class PastLivesPageTests
    {
        private static readonly CardEntry[] Deck = { new CardEntry("tsuki", 2), new CardEntry("harai", 1) };

        private static InheritanceCore.Journal JournalOf() =>
            InheritanceCore.Journal.WithPageCount(0, new[] { new KeyValuePair<string, int>("sabiyari", 1) }, "");

        private static Lineage DieAt(Lineage lineage, int layer)
        {
            LifeStart s = Inheritance.StartLife(lineage, 1UL);
            return Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(layer, 3, null, RunPhase.Fallen), Deck, JournalOf());
        }

        private static Lineage SurviveAt(Lineage lineage, int layer)
        {
            LifeStart s = Inheritance.StartLife(lineage, 1UL);
            return Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(layer, 2, null, RunPhase.Survived), Deck, JournalOf(), new EndOptions());
        }

        /// <summary>A death, a trace visited by the next life, that life's death, then a survival: every trace line the hall has.</summary>
        private static Lineage Mixed()
        {
            Lineage l = DieAt(Inheritance.NewLineage(7UL), 4);
            LifeStart s = Inheritance.StartLife(l, 1UL);
            l = Inheritance.TakeTrace(s.Lineage, l.Traces[0].Id, Array.Empty<string>(), 4).Lineage;
            l = Inheritance.RecordDeath(l, null, new LifePlace(2, 1, null, RunPhase.Fallen), Deck, JournalOf());
            return SurviveAt(l, 3);
        }

        /// <summary>What a hall card prints, read off the hall's own view: the header, then the lines below it.</summary>
        private static List<(string[] Header, string Body)> HallCards(Lineage lineage) =>
            Inheritance.HallView(lineage).PastLives.Select(p => (
                new[] { p.GenerationLabel, p.Name, p.EndingLabel },
                string.Join("\n", p.EndingLine, p.PlaceLine, InheritanceText.AttireHeading + "　" + p.TierName, MetaLines.PastTraceLine(lineage, p))))
                .ToList();

        /// <summary>The page's rows before the rules.</summary>
        private static List<JournalRow> LifeRows(JournalPage page) => page.Rows.Take(page.Rows.Count - InheritanceText.TraceRules.Count).ToList();

        private static void AssertSameAsHall(Lineage lineage)
        {
            JournalPage page = JournalBookOf.PastLivesPage(lineage);
            List<(string[] Header, string Body)> cards = HallCards(lineage);
            List<JournalRow> rows = LifeRows(page);
            Assert.That(page.Title, Is.EqualTo(Inheritance.HallView(lineage).PastLivesHeading));
            Assert.That(rows.Count, Is.EqualTo(cards.Count));
            for (int i = 0; i < cards.Count; i++)
            {
                Assert.That(rows[i].Label, Is.EqualTo(string.Join("　", cards[i].Header)), "the card's header, newest first");
                Assert.That(rows[i].Text, Is.EqualTo(cards[i].Body));
            }
        }

        [Test]
        public void NoLifeYet_ThePageSaysWhatTheEmptyHallSays()
        {
            Lineage l = Inheritance.NewLineage(7UL);
            JournalPage page = JournalBookOf.PastLivesPage(l);
            Assert.That(page.Title, Is.EqualTo(InheritanceText.PastLivesHeading));
            Assert.That(LifeRows(page).Select(r => (r.Label, r.Text)), Is.EqualTo(new[] { ("", InheritanceText.HallEmpty) }));

            // a life out on its first descent: still none closed
            Lineage running = Inheritance.StartLife(l, 1UL).Lineage;
            Assert.That(LifeRows(JournalBookOf.PastLivesPage(running)).Select(r => r.Text), Is.EqualTo(new[] { InheritanceText.HallEmpty }));
        }

        [Test]
        public void OneLife_TheRowIsTheHallsCard()
        {
            Lineage l = DieAt(Inheritance.NewLineage(7UL), 4);
            AssertSameAsHall(l);
            JournalRow row = JournalBookOf.PastLivesPage(l).Rows[0];
            Assert.That(row.Label, Does.StartWith("第 1 代　"));
            Assert.That(row.Text, Does.EndWith(InheritanceText.TraceStatus.Untaken));
        }

        [Test]
        public void SeveralLives_TheRowsAreTheHallsCards_NewestFirst()
        {
            Lineage l = Mixed();
            AssertSameAsHall(l);
            List<JournalRow> rows = LifeRows(JournalBookOf.PastLivesPage(l));
            Assert.That(rows.Select(r => r.Label.Split('　')[0]), Is.EqualTo(new[] { "第 3 代", "第 2 代", "第 1 代" }));
            Assert.That(rows[0].Text, Does.EndWith(InheritanceText.TraceStatus.None));
            Assert.That(rows[2].Text, Does.EndWith(MetaText.TraceCollected(2, l.Lives[1].Name)), "回収した, as the hall says it");
        }

        [Test]
        public void WhileALifeIsOut_TheClosedLivesAreTheHallsCards()
        {
            Lineage running = Inheritance.StartLife(Mixed(), 1UL).Lineage;
            AssertSameAsHall(running);
        }

        [Test]
        public void TheRules_AreInheritanceTextsTraceRules_AfterTheLives()
        {
            foreach (Lineage l in new[] { Inheritance.NewLineage(7UL), DieAt(Inheritance.NewLineage(7UL), 4), Mixed() })
            {
                JournalPage page = JournalBookOf.PastLivesPage(l);
                List<JournalRow> rules = page.Rows.Skip(page.Rows.Count - InheritanceText.TraceRules.Count).ToList();
                Assert.That(rules.Select(r => r.Text), Is.EqualTo(InheritanceText.TraceRules));
                Assert.That(rules.Select(r => r.Label), Is.All.Empty);
                Assert.That(rules.Select(r => r.NeedsDisclosure), Is.All.EqualTo(0));
            }
        }

        [Test]
        public void UnderTheCarryOver_TheTitleAndHeadersCountDescents_AndALifeWithoutATraceHasNoTraceLine()
        {
            Lineage none = Inheritance.NewLineage(7UL, carryOver: true);
            JournalPage empty = JournalBookOf.PastLivesPage(none);
            Assert.That(empty.Title, Is.EqualTo(InheritanceText.PastDescentsHeading));
            Assert.That(LifeRows(empty).Select(r => r.Text), Is.EqualTo(new[] { InheritanceText.BaseEmpty }));

            // a trace left before the switch, taken on the first carried descent, which then falls; then a return
            Lineage before = Inheritance.CarryingOver(DieAt(Inheritance.NewLineage(7UL), 4));
            LifeStart s = Inheritance.StartLife(before, 1UL);
            Lineage l = Inheritance.TakeTrace(s.Lineage, before.Traces[0].Id, Array.Empty<string>(), 4).Lineage;
            l = Inheritance.RecordDeath(l, null, new LifePlace(2, 1, null, RunPhase.Fallen), Deck, JournalOf());
            l = SurviveAt(l, 3);

            JournalPage page = JournalBookOf.PastLivesPage(l);
            HallView hall = Inheritance.HallView(l);
            Assert.That(page.Title, Is.EqualTo(hall.PastLivesHeading).And.EqualTo(InheritanceText.PastDescentsHeading));
            List<JournalRow> rows = LifeRows(page);
            Assert.That(rows.Select(r => r.Label.Split('　')[0]), Is.EqualTo(new[] { "3 回目", "2 回目", "1 回目" }));
            Assert.That(rows.Select(r => r.Label), Has.None.Contains("代"));

            // the fall and the return under the carry-over left no trace: the hall's 「痕跡なし（生きて閉じた）」 is not printed
            Assert.That(hall.PastLives.Take(2).Select(p => p.TraceLine), Is.All.EqualTo(InheritanceText.TraceStatus.None));
            for (int i = 0; i < 2; i++)
            {
                PastLifeView p = hall.PastLives[i];
                Assert.That(rows[i].Text, Is.EqualTo(string.Join("\n", p.EndingLine, p.PlaceLine, InheritanceText.AttireHeading + "　" + p.TierName)));
                Assert.That(rows[i].Text, Does.Not.Contain("痕跡"));
            }
            // the trace left before the switch keeps its line
            Assert.That(rows[2].Text, Does.EndWith(InheritanceText.TraceStatus.TakenOnDescent(2)));
            Assert.That(page.Rows.Skip(rows.Count).Select(r => r.Text), Is.EqualTo(InheritanceText.TraceRules));
        }

        [Test]
        public void NoLineage_NoPage()
        {
            Assert.That(JournalBookOf.PastLivesPage(null), Is.Null);
            JournalBook book = JournalBookOf.BookOf(new JournalCtx { Mode = JournalCtx.Explore });
            Assert.That(book.Pages.Select(p => p.Id), Has.None.EqualTo(JournalBookOf.PastLivesPageId));
        }

        [Test]
        public void OutsideBattle_ThePageIsTheLastOfTheDungeonTab_AndOpens()
        {
            Lineage l = Mixed();
            var layer = new JournalPage("dungeon-1", JournalTab.Dungeon, "第 1 層", new[] { new JournalRow("", "x") });
            JournalBook book = JournalBookOf.BookOf(new JournalCtx { Mode = JournalCtx.Explore, Pages = new[] { layer }, Lineage = l });
            List<JournalPage> dungeon = book.Pages.Where(p => p.Tab == JournalTab.Dungeon).ToList();
            Assert.That(dungeon.Select(p => p.Id), Is.EqualTo(new[] { "dungeon-1", JournalBookOf.PastLivesPageId }));
            JournalPage past = book.Find(JournalBookOf.PastLivesPageId);
            Assert.That(past.Id, Is.EqualTo(JournalBookOf.PastLivesPageId));
            Assert.That(past.Rows.Select(r => (r.Label, r.Text)), Is.EqualTo(JournalBookOf.PastLivesPage(l).Rows.Select(r => (r.Label, r.Text))));

            // the hall: no running life, so the ダンジョン tab holds this page alone and the book opens on it
            JournalBook hall = JournalBookOf.BookOf(new JournalCtx { Lineage = l });
            Assert.That(hall.First.Id, Is.EqualTo(JournalBookOf.PastLivesPageId));
            Assert.That(JournalBookOf.TabsOf(hall, hall.First).Select(t => (t.Label, t.Open, t.On)), Is.EqualTo(new[]
            {
                ("敵", false, false), ("ダンジョン", true, true), ("メモ", false, false), ("用語", true, false),
            }));
        }

        [Test]
        public void InBattle_ThePageIsNotInTheBook()
        {
            Lineage l = Mixed();
            JournalBook battle = JournalBookOf.BookOf(new JournalCtx
            {
                Mode = JournalCtx.Battle, Lineage = l, EnemyIds = new[] { "shadow_hound" },
            });
            Assert.That(battle.Pages.Select(p => p.Id), Has.None.EqualTo(JournalBookOf.PastLivesPageId));
            Assert.That(battle.Pages.Where(battle.CanOpen).Select(p => p.Tab).Distinct(), Is.EquivalentTo(new[] { JournalTab.Enemy, JournalTab.Terms }));

            // nor when a page of it is handed in
            JournalBook handed = JournalBookOf.BookOf(new JournalCtx
            {
                Mode = JournalCtx.Battle, Pages = new[] { JournalBookOf.PastLivesPage(l) }, EnemyIds = new[] { "shadow_hound" },
            });
            Assert.That(handed.Pages.Select(p => p.Id), Has.None.EqualTo(JournalBookOf.PastLivesPageId));
            Assert.That(handed.Find(JournalBookOf.PastLivesPageId).Tab, Is.EqualTo(JournalTab.Enemy), "Find falls back to the first that opens");
        }
    }
}
