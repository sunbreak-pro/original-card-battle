using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;
using InheritanceCore;
using NUnit.Framework;

namespace Trial.Script.Tests
{
    /// <summary>
    /// Port of the trial's test/meta/traceLines.test.mjs: the meta screens' trace lines over Inheritance's views
    /// (MetaLines.WithWaitingTrace, ending.js; MetaLines.PastTraceLine, hall.js). A trace left behind an older one on
    /// the same layer waits (docs/INHERITANCE.md 試作で決めたこと 4), and a visited trace is 回収した whatever was
    /// picked (R-10, 試作 5).
    /// </summary>
    public class TraceLinesTests
    {
        private static readonly CardEntry[] Deck = { new CardEntry("tsuki", 2), new CardEntry("harai", 1) };

        // { Disclosure: { sabiyari: 1 }, Pages: 0, Memo: '' }
        private static InheritanceCore.Journal JournalOf() =>
            InheritanceCore.Journal.WithPageCount(0, new[] { new KeyValuePair<string, int>("sabiyari", 1) }, "");

        private static Lineage DieAt(Lineage lineage, int layer)
        {
            LifeStart s = Inheritance.StartLife(lineage, 1UL);
            return Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(layer, 3, null, RunPhase.Fallen), Deck, JournalOf());
        }

        private static string Visit(int layer) => InheritanceText.TraceVisit(layer, InheritanceRules.LegacyPicks);

        [Test]
        public void TheFirstTraceOnALayer_KeepsEndingViewsVisitLine()
        {
            Lineage l = DieAt(Inheritance.NewLineage(7UL), 7);
            EndingView view = Inheritance.EndingView(l);
            Assert.That(view.Lines, Does.Contain(Visit(7)));
            Assert.That(MetaLines.WithWaitingTrace(view, l), Is.SameAs(view));
        }

        [Test]
        public void ADeathBehindAnOlderUntakenTraceOnTheSameLayer_SaysItWaits_NamingTheOlderOne()
        {
            Lineage l1 = DieAt(Inheritance.NewLineage(7UL), 7);
            Lineage l2 = DieAt(l1, 7);
            LifeRecord first = l2.Lives[0];
            EndingView view = MetaLines.WithWaitingTrace(Inheritance.EndingView(l2), l2);
            Assert.That(view.Lines, Does.Not.Contain(Visit(7)));
            Assert.That(view.Lines, Does.Contain(MetaText.TraceWaits(7, first.Generation, first.Name)));
            Assert.That(view.Lines.Count, Is.EqualTo(Inheritance.EndingView(l2).Lines.Count));
            // the hall says the same of it
            Assert.That(Inheritance.HallView(l2).Traces.First(t => t.Generation == 2).AppearsLine, Is.EqualTo(InheritanceText.Waits));
        }

        [Test]
        public void TwoOlderTracesAhead_TheCountNotOneName()
        {
            Lineage l = DieAt(DieAt(DieAt(Inheritance.NewLineage(7UL), 7), 7), 7);
            EndingView view = MetaLines.WithWaitingTrace(Inheritance.EndingView(l), l);
            Assert.That(view.Lines, Does.Contain(MetaText.TraceWaitsMany(7, 2)));
        }

        [Test]
        public void ATraceOnAnotherLayer_DoesNotMakeItWait()
        {
            Lineage l = DieAt(DieAt(Inheritance.NewLineage(7UL), 6), 7);
            EndingView view = Inheritance.EndingView(l);
            Assert.That(MetaLines.WithWaitingTrace(view, l), Is.SameAs(view));
        }

        [Test]
        public void AClearedLifesCardTraceBehindAnOlderLayer7Trace_GetsTheWaitAfterItsLine()
        {
            Lineage l1 = DieAt(Inheritance.NewLineage(7UL), 7);
            LifeStart s = Inheritance.StartLife(l1, 1UL);
            Lineage l2 = Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(7, 9, null, RunPhase.Completed), Deck, JournalOf(), new EndOptions());
            EndingView before = Inheritance.EndingView(l2);
            EndingView after = MetaLines.WithWaitingTrace(before, l2);
            string wait = MetaText.TraceWaits(7, l1.Lives[0].Generation, l1.Lives[0].Name);
            int at = after.Lines.ToList().IndexOf(wait);
            Assert.That(at, Is.GreaterThan(0), string.Join(" | ", after.Lines));
            Assert.That(after.Lines[at - 1], Does.Contain("札の痕跡"));
            Assert.That(after.Lines.Count, Is.EqualTo(before.Lines.Count + 1));
            // the rest of the view is the same
            Assert.That(new[] { after.Title, after.Sentence, after.HeirLine, after.NextLine, after.Button },
                Is.EqualTo(new[] { before.Title, before.Sentence, before.HeirLine, before.NextLine, before.Button }));
            Assert.That(after.Ending, Is.EqualTo(LifeEnding.Completed));
        }

        [Test]
        public void TheHallSaysCollected_ForATraceLeftWithNothingTaken()
        {
            Lineage l1 = DieAt(Inheritance.NewLineage(7UL), 4);
            LifeStart s = Inheritance.StartLife(l1, 1UL);
            Lineage left = Inheritance.TakeTrace(s.Lineage, l1.Traces[0].Id, Array.Empty<string>(), 4).Lineage;
            Lineage l2 = Inheritance.RecordDeath(left, null, new LifePlace(2, 1, null, RunPhase.Fallen), Deck, JournalOf());
            HallView view = Inheritance.HallView(l2);
            PastLifeView first = view.PastLives.First(p => p.Generation == 1);
            PastLifeView second = view.PastLives.First(p => p.Generation == 2);
            Assert.That(MetaLines.PastTraceLine(l2, first), Is.EqualTo(MetaText.TraceCollected(2, l2.Lives[1].Name)));
            Assert.That(MetaLines.PastTraceLine(l2, second), Is.EqualTo(InheritanceText.TraceStatus.Untaken));
        }

        // ---- beyond the JS file: the guards of withWaitingTrace and pastTraceLine ----

        [Test]
        public void WithWaitingTrace_LeavesTheViewAlone_WithoutALineageOrWhileALifeIsOut()
        {
            Lineage l2 = DieAt(DieAt(Inheritance.NewLineage(7UL), 7), 7);
            EndingView view = Inheritance.EndingView(l2);
            Assert.That(MetaLines.WithWaitingTrace(view, null), Is.SameAs(view));
            Lineage running = Inheritance.StartLife(l2, 1UL).Lineage;
            Assert.That(MetaLines.WithWaitingTrace(view, running), Is.SameAs(view));
        }

        [Test]
        public void WithWaitingTrace_LeavesASurvivalAlone()
        {
            Lineage l1 = DieAt(Inheritance.NewLineage(7UL), 7);
            LifeStart s = Inheritance.StartLife(l1, 1UL);
            Lineage l2 = Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(7, 9, null, RunPhase.Survived), Deck, JournalOf(), new EndOptions());
            EndingView view = Inheritance.EndingView(l2);
            Assert.That(MetaLines.WithWaitingTrace(view, l2), Is.SameAs(view));
        }

        [Test]
        public void PastTraceLine_KeepsAnUntakenOrNoTraceLine()
        {
            Lineage l1 = DieAt(Inheritance.NewLineage(7UL), 4);
            HallView view = Inheritance.HallView(l1);
            Assert.That(MetaLines.PastTraceLine(l1, view.PastLives[0]), Is.EqualTo(InheritanceText.TraceStatus.Untaken));

            LifeStart s = Inheritance.StartLife(l1, 1UL);
            Lineage alive = Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(3, 2, null, RunPhase.Survived), Deck, JournalOf(), new EndOptions());
            PastLifeView survivor = Inheritance.HallView(alive).PastLives.First(p => p.Generation == 2);
            Assert.That(MetaLines.PastTraceLine(alive, survivor), Is.EqualTo(InheritanceText.TraceStatus.None));
        }

        // ---- what node printed for withWaitingTrace / pastTraceLine on more lineages (the same constructions) ----

        private static Lineage ClearAt(Lineage lineage)
        {
            LifeStart s = Inheritance.StartLife(lineage, 1UL);
            return Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(7, 9, null, RunPhase.Completed), Deck, JournalOf(), new EndOptions());
        }

        private static Lineage DieAt(Lineage lineage, int layer, RunPhase ending)
        {
            LifeStart s = Inheritance.StartLife(lineage, 1UL);
            return Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(layer, 3, null, ending), Deck, JournalOf());
        }

        [Test]
        public void WithWaitingTrace_IsTheJs_ForAClearedLifeBehindTwo_AMiasmaDeath_AndAnotherLayer()
        {
            Lineage cleared = ClearAt(DieAt(DieAt(Inheritance.NewLineage(7UL), 7), 7));
            Assert.That(MetaLines.WithWaitingTrace(Inheritance.EndingView(cleared), cleared).Lines, Is.EqualTo(new[]
            {
                "手記は丸ごと次の生へ渡る",
                "生存者ボーナス：ツール枠 +1 ／ デッキ上限 +5（次の 1 生）",
                "第 7 層　果てなき世界に札の痕跡が残った（歪みの根の札を含む）",
                "第 7 層には古い痕跡が 2 件ある。それらが回収されると現れる",
            }));

            Lineage alone = ClearAt(Inheritance.NewLineage(7UL));
            EndingView aloneView = Inheritance.EndingView(alone);
            Assert.That(MetaLines.WithWaitingTrace(aloneView, alone), Is.SameAs(aloneView));

            Lineage miasma = DieAt(DieAt(Inheritance.NewLineage(7UL), 3), 3, RunPhase.MiasmaDeath);
            EndingView miasmaView = MetaLines.WithWaitingTrace(Inheritance.EndingView(miasma), miasma);
            Assert.That((miasmaView.Title, MetaTimelines.Chars(miasmaView.Sentence).Count), Is.EqualTo(("瘴気死", 28)));
            Assert.That(miasmaView.Lines, Is.EqualTo(new[]
            {
                "第 3 層　大黒蛇の住処に痕跡と手記が残った",
                "遺産の候補：技術 2 ／ 頁 1 ／ 走り書き 0",
                "第 3 層には第 1 代 オリヴの古い痕跡がある。それが回収されると現れる",
            }));

            Lineage fifth = DieAt(DieAt(DieAt(Inheritance.NewLineage(7UL), 3), 5), 5);
            EndingView fifthView = MetaLines.WithWaitingTrace(Inheritance.EndingView(fifth), fifth);
            Assert.That((fifthView.Title, MetaTimelines.Chars(fifthView.Sentence).Count), Is.EqualTo(("倒れた", 13)));
            Assert.That(fifthView.Lines, Is.EqualTo(new[]
            {
                "第 5 層　血染めの死海に痕跡と手記が残った",
                "遺産の候補：技術 2 ／ 頁 1 ／ 走り書き 0",
                "第 5 層には第 2 代 オスカの古い痕跡がある。それが回収されると現れる",
            }), "the older trace on layer 3 does not count");
        }

        [Test]
        public void PastTraceLine_IsTheJs_OverAMixedLineage()
        {
            Lineage l = DieAt(Inheritance.NewLineage(7UL), 4);
            LifeStart s = Inheritance.StartLife(l, 1UL);
            l = Inheritance.TakeTrace(s.Lineage, l.Traces[0].Id, Array.Empty<string>(), 4).Lineage;
            l = Inheritance.RecordDeath(l, null, new LifePlace(2, 1, null, RunPhase.Fallen), Deck, JournalOf());
            l = ClearAt(l);
            Lineage lineage = l;
            Assert.That(Inheritance.HallView(l).PastLives.Select(p => (p.Generation, p.TraceLine, MetaLines.PastTraceLine(lineage, p))),
                Is.EqualTo(new[]
                {
                    (3, "痕跡：未回収", "痕跡：未回収"),
                    (2, "痕跡：未回収", "痕跡：未回収"),
                    (1, "痕跡：第 2 代 オスカが受け取った", "痕跡：第 2 代 オスカが回収した"),
                }));
        }

        [Test]
        public void PastTraceLine_NamesTheLifeInProgress_WhenItCollectedTheTrace()
        {
            // The collector is still out: hallView's line names lineage.Current, and so does the hall's.
            Lineage l1 = DieAt(Inheritance.NewLineage(7UL), 4);
            LifeStart s = Inheritance.StartLife(l1, 1UL);
            Lineage taken = Inheritance.TakeTrace(s.Lineage, l1.Traces[0].Id, Array.Empty<string>(), 4).Lineage;
            PastLifeView first = Inheritance.HallView(taken).PastLives.First(p => p.Generation == 1);
            Assert.That(MetaLines.PastTraceLine(taken, first), Is.EqualTo(MetaText.TraceCollected(2, taken.Current.Name)));
        }
    }
}
