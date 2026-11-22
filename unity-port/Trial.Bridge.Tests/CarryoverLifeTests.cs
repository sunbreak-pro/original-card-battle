// The carry-over through the app layer (#1025): a life walks, ends, and the next life's saved start and Expedition carry
// what it grew — the 段 reached after a fall, every tick after a return, the kinds learned and the journal — with no
// trace and no survivor's bonus. A start without the carry-over writes and reads as before.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;
using NUnit.Framework;
using static Trial.Bridge.Tests.LifeTestKit;

namespace Trial.Bridge.Tests
{
    public sealed class CarryoverLifeTests
    {
        /// <summary>One life of <paramref name="lineage"/> from its saved start to its end on the lineage.</summary>
        private static (Lineage Lineage, Expedition Raw, ExplorationState End, LifeStart Start) OneLife(Lineage lineage, int? surviveAt = null)
        {
            LifeStart ls = Inheritance.StartLife(lineage);
            // The way LifeTests' survivor walks (its tools, the last open node) comes back from layer 1.
            LifeStartData start = Clone(Life.MakeStart(ls, surviveAt.HasValue ? Departure(ls) : Plain(ls)));
            Expedition raw = Life.LifeSetup(start).Expedition;
            ExplorationState end = Walk(Recorder.RecordExpedition(raw),
                (open, st) => surviveAt.HasValue ? open[open.Count - 1] : open[0], new WalkOptions { SurviveAt = surviveAt });
            Lineage next = Inheritance.RecordEnd(ls.Lineage, null, Inheritance.PlaceFromState(end), raw.Mastery,
                Names.LifeJournal(raw, "次は左へ"), Names.EndOptions(raw, end));
            return (next, raw, end, ls);
        }

        private static int Threshold(MasteryBook book, string cardId, int rank) =>
            rank == 0 ? 0 : (book.ProfileOf(cardId)?.Thresholds ?? Constants.MasteryThresholds)[rank - 1];

        [Test]
        public void AStartWithoutTheCarryover_WritesNoNewKeys_AndOneWithIt_ReadsBack()
        {
            LifeStart plain = Inheritance.StartLife(Inheritance.NewLineage(7));
            JsonObject json = Life.MakeStart(plain, Plain(plain)).ToJson();
            Assert.That(json.ContainsKey("mastery") || json.ContainsKey("owned"), Is.False, "the generations' start is as before");

            var start = new LifeStartData
            {
                Mastery = new List<CardMastery> { new CardMastery("tsuki", 9, 2) },
                Owned = new List<string> { "fang" },
            };
            LifeStartData back = Clone(start);
            Assert.That(back.Mastery, Is.EqualTo(start.Mastery));
            Assert.That(back.Owned, Is.EqualTo(new[] { "fang" }));

            JsonObject bad = start.ToJson();
            bad["mastery"] = new List<object> { new JsonObject { ["cardId"] = "tsuki", ["ticks"] = 1, ["rank"] = 4 } };
            Assert.Throws<SaveFormatException>(() => LifeStartData.FromJson(bad));
        }

        [Test]
        public void TheEndOptionsName_OnlyTheKindsLearnedBeyondTheInitialOnes()
        {
            LifeStart ls = Inheritance.StartLife(Inheritance.NewLineage(7, carryOver: true));
            Expedition raw = Life.LifeSetup(Clone(Life.MakeStart(ls, Plain(ls)))).Expedition;
            EndOptions opts = Names.EndOptions(raw, raw.Session.State);
            Assert.That(opts.Owned.Intersect(Expedition.InitialKinds), Is.Empty);
            Assert.That(opts.Owned, Is.EqualTo(raw.Owned.Except(Expedition.InitialKinds).OrderBy(x => x, StringComparer.Ordinal)));
        }

        [Test]
        public void AfterAFall_TheNextLifeStartsWithTheRanksReached_TheKindsAndTheJournal()
        {
            var first = OneLife(Inheritance.NewLineage(7, carryOver: true));
            Assert.That(Inheritance.EndingOf(first.End.Phase), Is.Not.EqualTo(LifeEnding.Survived), "the walk goes down until it falls");
            Lineage l = first.Lineage;
            Assert.That(l.Traces, Is.Empty, "no trace under the carry-over");
            Carryover carried = l.Carryover;
            MasteryBook grown = first.Raw.Mastery;
            var expected = grown.Records.Values.Where(r => r.Rank > 0)
                .Select(r => (r.CardId, r.Rank, Threshold(grown, r.CardId, r.Rank)))
                .OrderBy(r => r.CardId, StringComparer.Ordinal);
            Assert.That(carried.Cards.Select(c => (c.CardId, c.Rank, c.Ticks)), Is.EqualTo(expected), "the 段 reached, without the ticks past it");
            Assert.That(carried.Cards, Is.Not.Empty, "the walk grows at least one card");
            Assert.That(carried.Journal.Memo, Is.EqualTo("次は左へ"));

            LifeStart ls = Inheritance.StartLife(l);
            Assert.That(ls.SurvivorBonus, Is.False);
            Assert.That(ls.Life.Name, Is.EqualTo(l.Lives[0].Name));
            Assert.That(ls.Memo, Is.EqualTo("次は左へ"));
            LifeStartData start = Clone(Life.MakeStart(ls, Plain(ls)));
            Assert.That(start.TraceLayers, Is.Empty);
            Expedition next = Life.LifeSetup(start).Expedition;
            foreach (CardMastery c in carried.Cards)
            {
                Assert.That(next.Mastery.RankOf(c.CardId), Is.EqualTo(c.Rank), c.CardId);
                Assert.That(next.Mastery.TicksOf(c.CardId), Is.EqualTo(c.Ticks), c.CardId);
            }
            foreach (string id in carried.Owned) Assert.That(next.Owned, Does.Contain(id));
            foreach (var pair in carried.Journal.Disclosure) Assert.That(next.DisclosureOf(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            Assert.That(next.InheritedPages, Is.EqualTo(carried.Journal.Pages.Count));
        }

        [Test]
        public void AfterAReturn_TheNextLifeStartsWithEveryTick_AndNoBonus()
        {
            var first = OneLife(Inheritance.NewLineage(3, carryOver: true), surviveAt: 1);
            Assert.That(first.End.Phase, Is.EqualTo(RunPhase.Survived));
            Lineage l = first.Lineage;
            Assert.That(l.Survivor, Is.Null, "no survivor under the carry-over");
            MasteryBook grown = first.Raw.Mastery;
            var expected = grown.Records.Values.Where(r => r.Ticks > 0 || r.Rank > 0)
                .Select(r => (r.CardId, r.Rank, r.Ticks))
                .OrderBy(r => r.CardId, StringComparer.Ordinal);
            Assert.That(l.Carryover.Cards.Select(c => (c.CardId, c.Rank, c.Ticks)), Is.EqualTo(expected), "every tick stays");
            Assert.That(l.Carryover.Cards, Is.Not.Empty);

            LifeStart ls = Inheritance.StartLife(l);
            Assert.That(ls.SurvivorBonus, Is.False);
            Assert.That(ls.ToolSlots, Is.EqualTo(RunLoadout.ToolSlots));
            Assert.That(ls.DeckMax, Is.EqualTo(Constants.DeckMax));
            Expedition next = Life.LifeSetup(Clone(Life.MakeStart(ls, Plain(ls)))).Expedition;
            foreach (CardMastery c in l.Carryover.Cards) Assert.That(next.Mastery.TicksOf(c.CardId), Is.EqualTo(c.Ticks), c.CardId);
            Assert.That(next.DeckMax, Is.EqualTo(Constants.DeckMax));
        }

        [Test]
        public void ACarriedLife_ReplaysFromItsSavedStart()
        {
            Lineage l = OneLife(Inheritance.NewLineage(7, carryOver: true)).Lineage;
            LifeStart ls = Inheritance.StartLife(l);
            LifeStartData start = Life.MakeStart(ls, Plain(ls));
            Expedition raw = Life.LifeSetup(Clone(start)).Expedition;
            RecordedExpedition rec = Recorder.RecordExpedition(raw);
            Walk(rec, (open, st) => open[0]);
            Expedition again = Life.LifeSetup(Clone(start)).Expedition;
            Recorder.Replay(again, Saved(rec.Log));
            Assert.That(Snap(again), Is.EqualTo(Snap(raw)));
        }
    }
}
