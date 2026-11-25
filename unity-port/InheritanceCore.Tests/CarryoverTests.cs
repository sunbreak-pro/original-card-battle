using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using DungeonCore;
using InheritanceCore;
using NUnit.Framework;
using static InheritanceCore.Tests.InheritanceTests;

namespace InheritanceCore.Tests
{
    /// <summary>
    /// #1025: the carry-over, opt-in on the lineage. One explorer goes down again and again: a fall keeps the 段 reached,
    /// a life that comes back keeps the ticks too, the kinds learned and the journal stay, no trace is left and no
    /// survivor's bonus is given. The save is version 2; a version-1 save reads.
    /// </summary>
    public class CarryoverTests
    {
        private const ulong Seed = 42;

        private static Lineage Carrying() => Inheritance.NewLineage(Seed, carryOver: true);

        /// <summary>A mastery book of cards with no profile: they read the default thresholds 3 / 8 / 15.</summary>
        private static MasteryBook Book(params (string Id, int Ticks, int Rank)[] cards) => new MasteryBook(
            cards.ToDictionary(c => c.Id, c => new CardMastery(c.Id, c.Ticks, c.Rank), StringComparer.Ordinal));

        private static Journal LifeJournal(string memo = "灯は右に寄せる") =>
            Journal.WithPageCount(1, D(("sabiyari", 2), ("shoga", 1)), memo, new[] { new Scribble(1, "X", "古い") });

        private static EndOptions Learned(params string[] owned) => new EndOptions { Owned = owned };

        private static Lineage Fall(Lineage lineage, MasteryBook book, Journal? journal = null, EndOptions? opts = null, int layer = 2)
        {
            var s = Inheritance.StartLife(lineage, 7);
            return Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(layer, 4, NodeKind.Battle, RunPhase.Fallen), book,
                journal ?? LifeJournal(), opts ?? Learned("fang"));
        }

        private static Lineage Return(Lineage lineage, MasteryBook book, Journal? journal = null, EndOptions? opts = null)
        {
            var s = Inheritance.StartLife(lineage, 7);
            return Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(3, 2, null, RunPhase.Survived), book,
                journal ?? LifeJournal(), opts ?? Learned("fang"));
        }

        private static IEnumerable<(string, int, int)> Cards(Carryover c) => c.Cards.Select(x => (x.CardId, x.Rank, x.Ticks));

        // ---- the switch ------------------------------------------------------------------------------------

        [Test]
        public void TheSwitchIsOffUnlessAsked_AndANewLineageWithItStartsEmptyAtVersion2()
        {
            var plain = Inheritance.NewLineage(Seed);
            Assert.That(plain.CarriesOver, Is.False);
            Assert.That(plain.Version, Is.EqualTo(Inheritance.PlainVersion));
            Assert.That(plain.ToJson().ContainsKey("Carryover"), Is.False, "the generations stay the JS's byte for byte");

            var l = Carrying();
            Assert.That(l.CarriesOver, Is.True);
            Assert.That(l.Version, Is.EqualTo(Inheritance.CarryoverVersion));
            Assert.That(l.Carryover!.Cards, Is.Empty);
            Assert.That(l.Carryover.Owned, Is.Empty);
            Assert.That(Inheritance.Serialize(l), Does.StartWith("{\"Version\":2,")
                .And.EndWith(",\"Carryover\":{\"Cards\":[],\"Owned\":[],\"Journal\":{\"Disclosure\":{},\"Pages\":[],\"Memo\":\"\",\"Scribbles\":[]}}}"));
            Assert.That(Inheritance.CarryingOver(l), Is.SameAs(l));
        }

        // ---- a fall --------------------------------------------------------------------------------------------

        [Test]
        public void AFall_KeepsTheRankReachedWithoutTheTicksPastIt_TheKindsAndTheJournal_AndLeavesNoTrace()
        {
            var book = Book(("a", 12, 2), ("b", 2, 0), ("c", 5, 1));
            var l = Fall(Carrying(), book);
            Assert.That(l.Traces, Is.Empty, "no trace: the cards would come back twice");
            Assert.That(l.Lives[0].TraceId, Is.Null);
            Assert.That(l.Lives[0].Ending, Is.EqualTo(LifeEnding.Fallen));
            Assert.That(l.Survivor, Is.Null);
            Assert.That(Cards(l.Carryover!), Is.EqualTo(new[] { ("a", 2, 8), ("c", 1, 3) }),
                "the ticks short of the next 段 are lost; a card at 段 0 is lost");
            Assert.That(l.Carryover!.Owned, Is.EqualTo(new[] { "fang" }));
            Assert.That(Json.Stringify(l.Carryover.Journal.ToJson()), Is.EqualTo(Json.Stringify(Inheritance.NormalizeJournal(LifeJournal()).ToJson())));
        }

        [Test]
        public void AFallAtAShallowLayer_KeepsTheHighRanksToo()
        {
            var l = Fall(Carrying(), Book(("a", 15, 3), ("b", 9, 2)), layer: 1);
            Assert.That(Cards(l.Carryover!), Is.EqualTo(new[] { ("a", 3, 15), ("b", 2, 8) }), "the shallow-death rule is the generations'");
        }

        [Test]
        public void TheLifeAfterAFall_StartsWithTheRanks_TheKinds_TheJournalAndTheMemo_AndNoBonus()
        {
            var l = Fall(Carrying(), Book(("a", 12, 2)));
            var s = Inheritance.StartLife(l, 9);
            Assert.That(s.Life.Generation, Is.EqualTo(2));
            Assert.That(s.Life.Name, Is.EqualTo(l.Lives[0].Name), "the same explorer goes down again");
            Assert.That(Inheritance.NextHeir(l).Name, Is.EqualTo(l.Lives[0].Name));
            Assert.That(s.Carryover, Is.SameAs(l.Carryover));
            Assert.That(Cards(s.Carryover!), Is.EqualTo(new[] { ("a", 2, 8) }));
            Assert.That(s.Carryover!.Owned, Is.EqualTo(new[] { "fang" }));
            Assert.That(s.Journal.Disclosure, Is.EqualTo(new Dictionary<string, int> { ["sabiyari"] = 2, ["shoga"] = 1 }));
            Assert.That(s.Journal.Pages.Count, Is.EqualTo(1));
            Assert.That(s.Journal.Scribbles.Select(x => x.Text), Is.EqualTo(new[] { "古い" }));
            Assert.That(s.Journal.Memo, Is.Empty, "the memo is the explorer's own sheet, handed apart");
            Assert.That(s.Memo, Is.EqualTo("灯は右に寄せる"));
            Assert.That(s.Life.InheritedJournal, Is.True);
            Assert.That(s.SurvivorBonus, Is.False);
            Assert.That(s.ToolSlots, Is.EqualTo(RunLoadout.ToolSlots));
            Assert.That(s.DeckMax, Is.EqualTo(Constants.DeckMax));
            Assert.That(s.TraceLayers, Is.Empty);
            Assert.That(s.Lineage.Carryover, Is.SameAs(l.Carryover), "the record stays on the lineage while the life is out");
        }

        // ---- a life that comes back ---------------------------------------------------------------------------

        [Test]
        public void AReturn_KeepsTheTicksToo_MakesNoSurvivor_AndTheNextLifeGetsNoBonus()
        {
            var l = Return(Carrying(), Book(("a", 12, 2), ("b", 2, 0)));
            Assert.That(l.Survivor, Is.Null, "no survivor: the explorer comes back themselves");
            Assert.That(l.Traces, Is.Empty);
            Assert.That(l.Lives[0].ReplacedSurvivor, Is.Null);
            Assert.That(Cards(l.Carryover!), Is.EqualTo(new[] { ("a", 2, 12), ("b", 0, 2) }), "every tick stays");
            var s = Inheritance.StartLife(l, 9);
            Assert.That(s.SurvivorBonus, Is.False);
            Assert.That(s.Life.SurvivorBonus, Is.False);
            Assert.That(s.ToolSlots, Is.EqualTo(RunLoadout.ToolSlots));
            Assert.That(s.DeckMax, Is.EqualTo(Constants.DeckMax));
            Assert.That(Cards(s.Carryover!), Is.EqualTo(new[] { ("a", 2, 12), ("b", 0, 2) }));
        }

        [Test]
        public void TheRecordGrowsLifeByLife_AFallLaterKeepsTheTicksAReturnBanked()
        {
            var l = Return(Carrying(), Book(("a", 5, 1)), opts: Learned("fang"));
            // the next life starts at 5 ticks; it gains 2 and falls: 7 is short of 段 2, the banked 5 stay
            l = Fall(l, Book(("a", 7, 1), ("d", 3, 1)), journal: Journal.WithPageCount(2, D(("shoga", 2)), "次は左"), opts: Learned("fang", "claw"));
            Assert.That(Cards(l.Carryover!), Is.EqualTo(new[] { ("a", 1, 5), ("d", 1, 3) }));
            Assert.That(l.Carryover!.Owned, Is.EqualTo(new[] { "claw", "fang" }));
            Assert.That(l.Carryover.Journal.Disclosure, Is.EqualTo(new Dictionary<string, int> { ["sabiyari"] = 2, ["shoga"] = 2 }),
                "the higher 開示度 stays, and what the earlier life knew stays");
            Assert.That(l.Carryover.Journal.Pages.Count, Is.EqualTo(2));
            Assert.That(l.Carryover.Journal.Memo, Is.EqualTo("次は左"), "the memo is as the last life left it");
            Assert.That(l.Lives.Select(x => x.Generation), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void TheFirstCardToHoldTheThirdRank_KeepsIt()
        {
            // A live book never holds two cards at 段 3 (Mastery's own rule); a record that does, as a hand-made one may,
            // still gives the third 段 to the card that held it first.
            var l = Return(Carrying(), Book(("a", 15, 3)));
            l = Fall(l, Book(("a", 15, 3), ("b", 20, 3)));
            Assert.That(Cards(l.Carryover!), Is.EqualTo(new[] { ("a", 3, 15), ("b", 2, 15) }), "MergeMastery: b stops at 2");
        }

        [Test]
        public void AClearedLife_LeavesNoTrace_AndKeepsTheRootsCardsAsLearned()
        {
            var opts = new EndOptions { Owned = new[] { "fang" }, RootCards = new List<CardEntry> { "ne_watari", "ne_shibari" } };
            var s = Inheritance.StartLife(Carrying(), 7);
            var l = Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(7, 2, NodeKind.Boss, RunPhase.Completed), Book(("a", 9, 2)),
                LifeJournal(), opts);
            Assert.That(l.Traces, Is.Empty);
            Assert.That(l.Lives[0].TraceId, Is.Null);
            Assert.That(l.Player.RootDefeated, Is.True);
            Assert.That(l.Carryover!.Owned, Is.EqualTo(new[] { "fang", "ne_shibari", "ne_watari" }));
            Assert.That(Cards(l.Carryover), Is.EqualTo(new[] { ("a", 2, 9) }));
        }

        [Test]
        public void ACalendarThatRanOut_ClosesLikeAFall()
        {
            var s = Inheritance.StartLife(Carrying(), 7);
            var l = Inheritance.RecordEnd(s.Lineage, s.Life, new LifePlace(5, 1, null, RunPhase.Expired), Book(("a", 10, 2)), LifeJournal(), Learned());
            Assert.That(l.Traces, Is.Empty);
            Assert.That(Cards(l.Carryover!), Is.EqualTo(new[] { ("a", 2, 8) }));
        }

        // ---- switching an existing lineage on ------------------------------------------------------------------

        [Test]
        public void ALineageSwitchedOn_KeepsItsLivesTracesAndSurvivor_ButTheNextLifeGetsNoBonusNorTheSurvivorsJournal()
        {
            var g = SurviveAt(DieAt(Inheritance.NewLineage(Seed), 3), 2);
            Assert.That(g.Survivor, Is.Not.Null);
            var l = Inheritance.CarryingOver(g);
            Assert.That(l.Lives.Count, Is.EqualTo(2));
            Assert.That(l.Traces.Count, Is.EqualTo(1));
            Assert.That(l.Survivor, Is.SameAs(g.Survivor));
            var s = Inheritance.StartLife(l, 3);
            Assert.That(s.SurvivorBonus, Is.False);
            Assert.That(s.ToolSlots, Is.EqualTo(RunLoadout.ToolSlots));
            Assert.That(s.JournalFrom, Is.Null);
            Assert.That(s.Journal.Disclosure, Is.Empty);
            Assert.That(s.Life.Name, Is.EqualTo(g.Lives[1].Name), "the last explorer goes down again");
            Assert.That(s.TraceLayers, Is.EqualTo(new[] { 3 }), "a trace left before the switch can still be taken");
            var took = Inheritance.TakeTrace(s.Lineage, "trace-1", new[] { "card:tsuki" }, 3);
            Assert.That(took.Lineage.Carryover, Is.SameAs(l.Carryover));
        }

        // ---- the screens -----------------------------------------------------------------------------------------

        [Test]
        public void TheHallIsTheBase_WithNoGenerationOverTheExplorer_AndNoSurvivorBlock()
        {
            var plain = Inheritance.HallView(SurviveAt(Inheritance.NewLineage(Seed), 2));
            Assert.That(plain.CarriesOver, Is.False);
            Assert.That(plain.Title, Is.EqualTo(InheritanceText.HallTitle));
            Assert.That(plain.NextHeir.GenerationLabel, Is.EqualTo("第 2 代"));
            Assert.That(plain.NextHeir.RoleLabel, Is.EqualTo(InheritanceText.RoleName));

            var h = Inheritance.HallView(Inheritance.CarryingOver(SurviveAt(Inheritance.NewLineage(Seed), 2)));
            Assert.That(h.CarriesOver, Is.True);
            Assert.That(h.Title, Is.EqualTo("拠点"));
            Assert.That(h.NextHeir.GenerationLabel, Is.Empty);
            Assert.That(h.NextHeir.RoleLabel, Is.EqualTo("2 回目"));
            Assert.That(h.Survivor, Is.Null);
            Assert.That(h.SurvivorSpent, Is.Null);
            Assert.That(Json.Stringify(h.ToJson()), Does.Not.Contain("CarriesOver"));
        }

        [Test]
        public void TheEndingSaysWhatGoesOn_AndLeadsBackToTheBase()
        {
            var fell = Inheritance.EndingView(Fall(Carrying(), Book(("a", 12, 2))));
            Assert.That(fell.Lines, Is.EqualTo(new[] { InheritanceText.CarriedAfterFall }));
            Assert.That(fell.Button, Is.EqualTo("拠点へ"));
            var back = Inheritance.EndingView(Return(Carrying(), Book(("a", 12, 2))));
            Assert.That(back.Lines, Is.EqualTo(new[] { InheritanceText.CarriedAfterReturn }));
        }

        [Test]
        public void TheEndingOfARunOutCalendar_DoesNotPromiseTheRecordGoesOn()
        {
            // The trial replaces the whole lineage when the world ends, so the card must say the record is lost.
            var s = Inheritance.StartLife(Carrying(), 7);
            var l = Inheritance.RecordEnd(s.Lineage, s.Life, new LifePlace(5, 1, null, RunPhase.Expired), Book(("a", 10, 2)), LifeJournal(), Learned());
            var view = Inheritance.EndingView(l);
            Assert.That(view.Ending, Is.EqualTo(LifeEnding.Expired));
            Assert.That(view.Lines, Is.EqualTo(new[] { InheritanceText.WorldEndedUncarried }));
            Assert.That(view.Lines, Has.None.Contains("持ち越す"));
        }

        // ---- the words: one explorer, counted by the descent (#1031) -----------------------------------------------

        /// <summary>Every word the base prints: the view the JS also has, and the role words beside the name.</summary>
        private static IEnumerable<string> Words(HallView h) => new[] { Json.Stringify(h.ToJson()), h.NextHeir.RoleLabel };

        private static IEnumerable<string> Words(EndingView e) => new[] { Json.Stringify(e.ToJson()) };

        private static void NoGenerationWords(IEnumerable<string> texts, string what)
        {
            foreach (string t in texts)
            {
                Assert.That(t, Does.Not.Match(@"第\s*\d+\s*代"), what);
                Assert.That(t, Does.Not.Contain("代"), what);
                Assert.That(t, Does.Not.Contain("踏破者"), what);
            }
        }

        [Test]
        public void TheBaseAndTheEnding_NameNoGenerationNorHeir()
        {
            var first = Carrying();
            var fell = Fall(first, Book(("a", 12, 2)));
            var back = Return(fell, Book(("a", 14, 2)));
            var s = Inheritance.StartLife(back, 7);
            var cleared = Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(7, 2, NodeKind.Boss, RunPhase.Completed), Book(("a", 9, 2)),
                LifeJournal(), Learned("fang"));
            var e = Inheritance.StartLife(Carrying(), 7);
            var expired = Inheritance.RecordEnd(e.Lineage, e.Life, new LifePlace(5, 1, null, RunPhase.Expired), Book(("a", 10, 2)), LifeJournal(), Learned());

            NoGenerationWords(Words(Inheritance.HallView(first)), "the base before the first descent");
            foreach (var (l, what) in new[] { (fell, "a fall"), (back, "a return"), (cleared, "the root defeated"), (expired, "the calendar run out") })
            {
                NoGenerationWords(Words(Inheritance.HallView(l)), "the base after " + what);
                NoGenerationWords(Words(Inheritance.EndingView(l)), "the ending of " + what);
            }
        }

        [Test]
        public void TheBase_CountsTheDescents()
        {
            var first = Inheritance.HallView(Carrying());
            Assert.That(first.Lead, Is.EqualTo("これまでの探索を振り返り、次の探索へ出る部屋。"));
            Assert.That(first.Empty, Is.EqualTo("最初の探索。持ち越すものはまだ無い。"));
            Assert.That(first.PastLivesHeading, Is.EqualTo("これまでの探索"));
            Assert.That(first.NextHeirHeading, Is.EqualTo("次の探索"));
            Assert.That(first.NextHeir.RoleLabel, Is.EqualTo("1 回目"));
            Assert.That(first.NextHeir.TierReason, Is.EqualTo("最初の探索。支給品は縁なし"));

            var l = Fall(Carrying(), Book(("a", 12, 2)));
            string name = l.Lives[0].Name;
            var h = Inheritance.HallView(l);
            Assert.That(h.Empty, Is.Empty);
            Assert.That(h.NextHeir.Name, Is.EqualTo(name));
            Assert.That(h.NextHeir.RoleLabel, Is.EqualTo("2 回目"));
            Assert.That(h.NextHeir.Line, Is.EqualTo($"2 回目の探索 {name}"));
            Assert.That(h.NextHeir.TierReason, Is.EqualTo("前の探索で第 2 層まで届いたため"));
            Assert.That(h.PastLives.Single().GenerationLabel, Is.EqualTo("1 回目"));
            Assert.That(h.PastLives.Single().Line, Is.EqualTo($"1 回目の探索 {name}"));

            var s = Inheritance.StartLife(l, 7);
            var cleared = Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(7, 2, NodeKind.Boss, RunPhase.Completed), Book(("a", 9, 2)),
                LifeJournal(), Learned("fang"));
            Assert.That(Inheritance.HallView(cleared).NextHeir.TierReason, Is.EqualTo("前の探索で歪みの根を倒したため"));
        }

        [Test]
        public void TheEnding_NamesTheDescentThatClosed_AndTheNextOne()
        {
            var l = Fall(Carrying(), Book(("a", 12, 2)));
            string name = l.Lives[0].Name;
            var view = Inheritance.EndingView(l);
            Assert.That(view.HeirLine, Is.EqualTo($"1 回目の探索 {name}"));
            Assert.That(view.NextLine, Is.EqualTo($"次の探索：2 回目 {name}"));
        }

        [Test]
        public void ALineageSwitchedOn_WordsItsOlderLivesAndTracesByTheDescentToo()
        {
            // A trace left before the switch stays and is taken on a later descent: the base still names no 代.
            var l = Inheritance.CarryingOver(DieAt(DieAt(Inheritance.NewLineage(Seed), 3), 2));
            var s = Inheritance.StartLife(l, 3);
            var took = Inheritance.TakeTrace(s.Lineage, "trace-1", new[] { "card:tsuki" }, 3);
            var fell = Inheritance.RecordDeath(took.Lineage, null, new LifePlace(4, 4, NodeKind.Battle, RunPhase.Fallen), Book(("a", 12, 2)),
                LifeJournal(), Learned("fang"));
            var h = Inheritance.HallView(fell);
            Assert.That(h.PastLives.First(p => p.Generation == 1).TraceLine, Is.EqualTo("痕跡：3 回目の探索で受け取った"));
            Assert.That(h.Traces.Single().HeirLine, Is.EqualTo($"2 回目の探索 {fell.Lives[1].Name}"));
            NoGenerationWords(new[] { h.PastLives.Select(p => p.GenerationLabel + p.Line + p.TraceLine), h.Traces.Select(t => t.HeirLine) }
                .SelectMany(x => x), "the switched lineage's lives and traces");
        }

        [Test]
        public void TheGenerationsKeepTheirWords()
        {
            var g = DieAt(Inheritance.NewLineage(Seed), 3);
            var h = Inheritance.HallView(g);
            Assert.That(h.Lead, Is.EqualTo(InheritanceText.HallLead));
            Assert.That(h.PastLivesHeading, Is.EqualTo(InheritanceText.PastLivesHeading));
            Assert.That(h.NextHeirHeading, Is.EqualTo(InheritanceText.NextHeirHeading));
            Assert.That(h.PastLives.Single().GenerationLabel, Is.EqualTo("第 1 代"));
            Assert.That(h.PastLives.Single().Line, Is.EqualTo(InheritanceText.HeirLine(1, g.Lives[0].Name)));
            var heir = Inheritance.NextHeir(g);
            Assert.That(Inheritance.EndingView(g).NextLine, Is.EqualTo(InheritanceText.NextHeirLine(heir.Generation, heir.Name)));
            Assert.That(Inheritance.HallView(Inheritance.NewLineage(Seed)).Empty, Is.EqualTo(InheritanceText.HallEmpty));
        }

        // ---- the save ---------------------------------------------------------------------------------------------

        [Test]
        public void ACarryingLineage_ReadsBackAndWritesTheSameBytes()
        {
            var l = Fall(Return(Carrying(), Book(("a", 12, 2))), Book(("a", 14, 2), ("c", 3, 1)));
            string text = Inheritance.Serialize(l);
            Assert.That(text, Does.StartWith("{\"Version\":2,"));
            var back = Inheritance.Deserialize(text);
            Assert.That(back.CarriesOver, Is.True);
            Assert.That(Inheritance.Serialize(back), Is.EqualTo(text));
            Assert.That(Cards(back.Carryover!), Is.EqualTo(Cards(l.Carryover!)));
        }

        [Test]
        public void AVersion1Save_ReadsAsVersion2_WithoutACarryover_AndWritesItsOwnBytesUntilSwitchedOn()
        {
            string v1 = Inheritance.Serialize(DieAt(Inheritance.NewLineage(Seed), 3));
            Assert.That(v1, Does.StartWith("{\"Version\":1,"));

            var upgraded = LineageMigrations.Upgrade((JsonObject)Json.Parse(v1)!);
            Assert.That(upgraded["Version"], Is.EqualTo(2));

            var read = Inheritance.Deserialize(v1);
            Assert.That(read.CarriesOver, Is.False);
            Assert.That(Inheritance.Serialize(read), Is.EqualTo(v1), "a version-1 lineage still reads in a version-1 build");

            var on = Inheritance.CarryingOver(read);
            Assert.That(on.Version, Is.EqualTo(2));
            string text = Inheritance.Serialize(on);
            Assert.That(text, Does.StartWith("{\"Version\":2,"));
            Assert.That(Inheritance.Serialize(Inheritance.Deserialize(text)), Is.EqualTo(text));
        }

        [Test]
        public void AVersion1Save_NeverReadsACarryover()
        {
            var json = Inheritance.ToJson(Inheritance.CarryingOver(Inheritance.NewLineage(Seed)));
            json["Version"] = 1;
            Assert.That(Inheritance.FromJson(json).CarriesOver, Is.False, "version 1 never wrote one");
        }

        [Test]
        public void ABadCarryoverIsRefused()
        {
            JsonObject With(Action<JsonObject> change)
            {
                var g = Inheritance.ToJson(Fall(Carrying(), Book(("a", 12, 2))));
                change((JsonObject)g["Carryover"]!);
                return g;
            }
            JsonObject Card(JsonObject c) => (JsonObject)((List<object?>)c["Cards"]!)[0]!;

            Assert.That(Assert.Throws<InheritanceRuleException>(() => Inheritance.FromJson(With(c => c["Cards"] = "x")))!.Message, Does.Contain("carried cards"));
            Assert.Throws<InheritanceRuleException>(() => Inheritance.FromJson(With(c => Card(c)["Rank"] = 4)));
            Assert.Throws<InheritanceRuleException>(() => Inheritance.FromJson(With(c => Card(c)["Ticks"] = -1)));
            Assert.Throws<InheritanceRuleException>(() => Inheritance.FromJson(With(c => Card(c)["CardId"] = 3)));
            Assert.Throws<InheritanceRuleException>(() => Inheritance.FromJson(With(c => c["Owned"] = new List<object?> { 1 })));
            Assert.Throws<InheritanceRuleException>(() => Inheritance.FromJson(With(c => c["Journal"] = "x")));
            var whole = Inheritance.ToJson(Carrying());
            whole["Carryover"] = 5;
            Assert.Throws<InheritanceRuleException>(() => Inheritance.FromJson(whole));
        }
    }
}
