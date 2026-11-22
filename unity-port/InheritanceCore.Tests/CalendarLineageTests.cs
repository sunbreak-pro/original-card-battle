using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;
using InheritanceCore;
using NUnit.Framework;
using static InheritanceCore.Tests.InheritanceTests;

namespace InheritanceCore.Tests
{
    /// <summary>
    /// #888: the lineage keeps 崩壊の暦, the new ending 期限切れ, and the lineages saved with the legacy 瘴気死 still load
    /// (dungeon_exploration_v4.md §7.2 / §7.9).
    /// </summary>
    public class CalendarLineageTests
    {
        private const ulong Seed = 42;

        private static CalendarState Moved => new CalendarState(312, new Dictionary<Region, int> { [Region.Dragon] = 41 });

        // ---- the calendar on the lineage -------------------------------------------------------------------

        [Test]
        public void ANewLineageIsOnAFreshCalendar_AndWritesNoCalendarKey()
        {
            var l = Inheritance.NewLineage(Seed);
            Assert.That(l.Calendar, Is.EqualTo(CalendarState.Fresh));
            Assert.That(l.ToJson().ContainsKey("Calendar"), Is.False, "a fresh calendar keeps the lineage the JS's byte for byte");
            Assert.That(new Lineage(1, "1", Array.Empty<LifeRecord>(), null, Array.Empty<Trace>(), null, new PlayerKnowledge()).Calendar,
                Is.SameAs(CalendarState.Fresh));
        }

        [Test]
        public void AMovedCalendarIsWrittenLast_AndReadsBack()
        {
            var l = Inheritance.NewLineage(Seed).WithCalendar(Moved);
            string text = Inheritance.Serialize(l);
            Assert.That(text, Does.EndWith(",\"Calendar\":{\"DaysLeft\":312,\"KingReturns\":{\"dragon\":41}}}"));

            var back = Inheritance.Deserialize(text);
            Assert.That(back.Calendar, Is.EqualTo(Moved));
            Assert.That(Inheritance.Serialize(back), Is.EqualTo(text));
        }

        [Test]
        public void ACalendarWithNoKingDownWritesAnEmptyKingList()
        {
            var l = Inheritance.NewLineage(Seed).WithCalendar(new CalendarState(100));
            Assert.That(Inheritance.Serialize(l), Does.EndWith(",\"Calendar\":{\"DaysLeft\":100,\"KingReturns\":{}}}"));
            Assert.That(Inheritance.Deserialize(Inheritance.Serialize(l)).Calendar, Is.EqualTo(new CalendarState(100)));
        }

        [Test]
        public void TheKingsAreWrittenInRegionOrder()
        {
            var kings = new CalendarState(50, new Dictionary<Region, int> { [Region.Ghost] = 3, [Region.Dragon] = 9, [Region.Parasite] = 1 });
            string text = Inheritance.Serialize(Inheritance.NewLineage(Seed).WithCalendar(kings));
            Assert.That(text, Does.Contain("\"KingReturns\":{\"dragon\":9,\"parasite\":1,\"ghost\":3}"));
            Assert.That(Inheritance.Deserialize(text).Calendar.ClearReached, Is.True);
        }

        [Test]
        public void ALineageSavedWithoutACalendarReadsAsFresh()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 3);
            string text = Inheritance.Serialize(l);
            Assert.That(text, Does.Not.Contain("Calendar"));
            Assert.That(Inheritance.Deserialize(text).Calendar, Is.EqualTo(CalendarState.Fresh));

            var explicitNull = (JsonObject)Json.Parse(text)!;
            explicitNull["Calendar"] = null;
            Assert.That(Inheritance.FromJson(explicitNull).Calendar, Is.EqualTo(CalendarState.Fresh));
        }

        [TestCase("{\"DaysLeft\":361,\"KingReturns\":{}}", "calendar days")]
        [TestCase("{\"DaysLeft\":-1}", "calendar days")]
        [TestCase("{\"KingReturns\":{}}", "calendar days")]
        [TestCase("{\"DaysLeft\":10,\"KingReturns\":{\"common\":5}}", "no king of common")]
        [TestCase("{\"DaysLeft\":10,\"KingReturns\":{\"dragon\":0}}", "return of the dragon king")]
        [TestCase("{\"DaysLeft\":10,\"KingReturns\":{\"dragon\":61}}", "return of the dragon king")]
        [TestCase("{\"DaysLeft\":10,\"KingReturns\":[1]}", "the kings of the calendar")]
        [TestCase("12", "the calendar")]
        public void ABadCalendarIsABadSave(string calendar, string pattern)
        {
            var data = (JsonObject)Json.Parse(Inheritance.Serialize(Inheritance.NewLineage(Seed)))!;
            data["Calendar"] = Json.Parse(calendar);
            var e = Assert.Throws<InheritanceRuleException>(() => Inheritance.FromJson(data));
            Assert.That(e!.Message, Does.Contain(pattern));
        }

        [Test]
        public void EveryRuleCarriesTheCalendarAlong_AndNoneMovesIt()
        {
            // The calendar is the caller's to move (CalendarState.Handover); the inheritance rules only keep it.
            var l = Inheritance.NewLineage(Seed).WithCalendar(Moved);
            var s = Inheritance.StartLife(l, 1);
            Assert.That(s.Lineage.Calendar, Is.EqualTo(Moved));

            var dead = Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(3, 4, null, RunPhase.Fallen), FullDeck, null);
            Assert.That(dead.Calendar, Is.EqualTo(Moved));

            var s2 = Inheritance.StartLife(dead, 2);
            var taken = Inheritance.TakeTrace(s2.Lineage, "trace-1", new[] { "card:kansatsu" });
            Assert.That(taken.Lineage.Calendar, Is.EqualTo(Moved));

            var alive = Inheritance.RecordSurvival(taken.Lineage, null, new LifePlace(2, 1, null, RunPhase.Survived), FullDeck, null);
            Assert.That(alive.Calendar, Is.EqualTo(Moved));
            Assert.That(Inheritance.NoteCarving(alive).Calendar, Is.EqualTo(Moved));
            Assert.That(alive.WithPlayer(alive.Player).WithTraces(alive.Traces).WithLives(alive.Lives).WithSurvivor(alive.Survivor)
                .WithCurrent(null).Calendar, Is.EqualTo(Moved));

            var handedOver = alive.WithCalendar(alive.Calendar.Handover(RunPhase.Survived, shortReturn: false));
            Assert.That(handedOver.Calendar.DaysLeft, Is.EqualTo(312), "a king is down, so the handover days are frozen");
            Assert.That(handedOver.Calendar.KingReturns[Region.Dragon], Is.EqualTo(38));
        }

        // ---- 期限切れ -----------------------------------------------------------------------------------------

        [Test]
        public void ExpiredMapsBothWays()
        {
            Assert.That(Inheritance.EndingOf(RunPhase.Expired), Is.EqualTo(LifeEnding.Expired));
            Assert.That(Inheritance.PhaseOf(LifeEnding.Expired), Is.EqualTo(RunPhase.Expired));
            Assert.That(Inheritance.PhaseOf(LifeEnding.MiasmaDeath), Is.Null, "瘴気死 has no RunPhase any more");
            foreach (RunPhase phase in Enum.GetValues(typeof(RunPhase)))
            {
                var ending = Inheritance.EndingOf(phase);
                if (ending.HasValue) Assert.That(Inheritance.PhaseOf(ending.Value), Is.EqualTo(phase), phase.ToString());
            }
        }

        [Test]
        public void AnExpiredLifeClosesLikeADeath()
        {
            var s = Inheritance.StartLife(Inheritance.NewLineage(Seed), 1);
            var l = Inheritance.RecordEnd(s.Lineage, s.Life, new LifePlace(4, 2, NodeKind.Rest, RunPhase.Expired), FullDeck, null);
            Assert.That(l.Lives[0].Ending, Is.EqualTo(LifeEnding.Expired));
            Assert.That(l.Traces.Single().Ending, Is.EqualTo(LifeEnding.Expired));
            Assert.That(l.Survivor, Is.Null);

            var view = Inheritance.EndingView(l);
            Assert.That(view.Title, Is.EqualTo("期限切れ"));
            Assert.That(view.Sentence, Is.EqualTo("第 4 層で暦が尽きた。龍神が目覚め、世界が終わる。"));

            string text = Inheritance.Serialize(l);
            Assert.That(text, Does.Contain("\"Ending\":\"Expired\""));
            Assert.That(Inheritance.Deserialize(text).Lives[0].Ending, Is.EqualTo(LifeEnding.Expired));
        }

        [Test]
        public void AnExpiredStateFromTheExplorationClosesTheLife()
        {
            var profile = Lair.Of(1);
            var expired = ExplorationReducer.Enter(profile, profile.Map(3), RunLoadout.Empty, 50, 50, 10, new CalendarState(1));
            Assert.That(expired.Phase, Is.EqualTo(RunPhase.Expired));
            var s = Inheritance.StartLife(Inheritance.NewLineage(Seed), 3);
            var l = Inheritance.RecordEnd(s.Lineage, s.Life, Inheritance.PlaceFromState(expired), Array.Empty<CardEntry>(), null);
            Assert.That(l.Lives[0].Ending, Is.EqualTo(LifeEnding.Expired));
        }

        [Test]
        public void ARefusedEndingNamesOnlyTheJsEndings()
        {
            // The JS golden pins these words, so Expired stays out of the list.
            var s = Inheritance.StartLife(Inheritance.NewLineage(Seed), 1);
            var e = Assert.Throws<InheritanceRuleException>(() =>
                Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(1, ending: RunPhase.Survived), Array.Empty<CardEntry>(), null));
            Assert.That(e!.Message, Is.EqualTo("Survived is not one of MiasmaDeath / Fallen"));
            var e2 = Assert.Throws<InheritanceRuleException>(() =>
                Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(1, ending: RunPhase.Expired), Array.Empty<CardEntry>(), null));
            Assert.That(e2!.Message, Is.EqualTo("Expired is not one of Survived / Completed"));
            var e3 = Assert.Throws<InheritanceRuleException>(() =>
                Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(1, null, null, LifeEnding.MiasmaDeath), Array.Empty<CardEntry>(), null));
            Assert.That(e3!.Message, Is.EqualTo("MiasmaDeath is not one of Survived / Completed"));
        }

        [Test]
        public void TheTextsForExpiredAreThere()
        {
            Assert.That(InheritanceText.EndingTitle(LifeEnding.Expired), Is.EqualTo("期限切れ"));
            Assert.That(InheritanceText.EndingSentence(LifeEnding.Expired, 6), Is.EqualTo("第 6 層で暦が尽きた。龍神が目覚め、世界が終わる。"));
            Assert.That(InheritanceText.WorldEndsLine, Does.Contain("龍神が目覚め"));
        }

        // ---- the legacy 瘴気死 --------------------------------------------------------------------------------

        [Test]
        public void ALineageWithTheLegacyMiasmaDeathStillLoads()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 4, named: LifeEnding.MiasmaDeath);
            string text = Inheritance.Serialize(l);
            Assert.That(text, Does.Contain("\"Ending\":\"MiasmaDeath\""));

            var back = Inheritance.Deserialize(text);
            Assert.That(back.Lives[0].Ending, Is.EqualTo(LifeEnding.MiasmaDeath));
            Assert.That(back.Traces[0].Ending, Is.EqualTo(LifeEnding.MiasmaDeath));
            Assert.That(back.Calendar, Is.EqualTo(CalendarState.Fresh));
            Assert.That(Inheritance.Serialize(back), Is.EqualTo(text));

            var view = Inheritance.EndingView(back);
            Assert.That(view.Title, Is.EqualTo("瘴気死"));
            Assert.That(view.Sentence, Is.EqualTo("瘴気が 100% に達し、第 4 層でこの生が終わった。"));
            Assert.That(Inheritance.StartLife(back, 2).TraceLayers, Is.EqualTo(new[] { 4 }), "its trace still waits on layer 4");
        }

        [Test]
        public void ADeathWithNoEndingIsStillTheLegacyMiasmaDeath()
        {
            // The JS reads a death with no ending as 瘴気死; the C# keeps that so the golden lineages stay byte for byte.
            var s = Inheritance.StartLife(Inheritance.NewLineage(Seed), 1);
            var l = Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(2), Array.Empty<CardEntry>(), null);
            Assert.That(l.Lives[0].Ending, Is.EqualTo(LifeEnding.MiasmaDeath));
            var viaEnd = Inheritance.RecordEnd(s.Lineage, s.Life, new LifePlace(2, null, null, LifeEnding.MiasmaDeath), Array.Empty<CardEntry>(), null);
            Assert.That(Inheritance.Serialize(viaEnd), Is.EqualTo(Inheritance.Serialize(l)));
        }

        [Test]
        public void ADeepLayerKeepsItsSevenLayerNameInTheHall()
        {
            // Traces are kept by layer number only (#888 leaves region-aware traces out), so a deep layer keeps the name
            // the trial's lair gave its depth; layer 2 reads the dragon god's name once the carving is known.
            var l = DieAt(Inheritance.NewLineage(Seed), 5);
            Assert.That(Inheritance.HallView(l).Traces[0].PlaceLine, Is.EqualTo("第 5 層　暗き死海"));
            var carving = DieAt(Inheritance.NoteCarving(Inheritance.NewLineage(Seed)), 2);
            Assert.That(Inheritance.HallView(carving).Traces[0].PlaceLine, Is.EqualTo("第 2 層　龍神の秘跡"));
        }
    }
}
