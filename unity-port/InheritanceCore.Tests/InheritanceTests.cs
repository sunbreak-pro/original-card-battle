using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using BattleCore;
using DungeonCore;
using InheritanceCore;
using NUnit.Framework;

namespace InheritanceCore.Tests
{
    /// <summary>
    /// Port of the trial's test/inheritance/Inheritance.test.mjs: every rule of docs/INHERITANCE.md (R-01 …) has at
    /// least one test, named after it. JS deepEqual on lineages is compared through the serialised JSON.
    /// </summary>
    public class InheritanceTests
    {
        // ---- fixtures -----------------------------------------------------------------------------------

        private const ulong Seed = 42;

        /// <summary>The JS passes `j: null` explicitly (not the default journal): this instance stands for it.</summary>
        private static readonly Journal NullJournal = new Journal();

        /// <summary>A deck as a list of mastery entries: (cardId, rank) pairs.</summary>
        internal static CardEntry[] Deck(params (string Id, int Rank)[] pairs) =>
            pairs.Select(p => new CardEntry(p.Id, p.Rank)).ToArray();

        internal static List<KeyValuePair<string, int>> D(params (string Id, int Level)[] pairs) =>
            pairs.Select(p => new KeyValuePair<string, int>(p.Id, p.Level)).ToList();

        internal static readonly CardEntry[] FullDeck = Deck(("kansatsu", 3), ("tsuki", 2), ("harai", 1), ("fumikomi", 0));

        internal static Journal FullJournal => Journal.WithPageCount(2, D(("sabiyari", 2), ("shoga", 1), ("shokou", 0)), "門竜は押すと崩れる");

        private static Journal? Pick(Journal? j) => j == null ? FullJournal : ReferenceEquals(j, NullJournal) ? null : j;

        private static void ThrowsRule(TestDelegate fn, string? pattern = null)
        {
            var e = Assert.Throws<InheritanceRuleException>(fn);
            if (pattern != null) Assert.That(e!.Message, Does.Match(pattern));
        }

        private static string S(Lineage l) => Inheritance.Serialize(l);

        private static string J(Journal j) => Json.Stringify(j.ToJson());

        /// <summary>Starts a life and kills it at <paramref name="layer"/> with the given deck and journal.</summary>
        internal static Lineage DieAt(Lineage lineage, int layer, LifeDeck? d = null, Journal? j = null,
            RunPhase ending = RunPhase.Fallen, int nodeId = 4, ulong seed = 1)
        {
            var s = Inheritance.StartLife(lineage, seed);
            return Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(layer, nodeId, null, ending), d ?? FullDeck, Pick(j));
        }

        internal static Lineage SurviveAt(Lineage lineage, int layer, Journal? j = null, EndOptions? opts = null,
            RunPhase ending = RunPhase.Survived, LifeDeck? d = null)
        {
            var s = Inheritance.StartLife(lineage, 1);
            return Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(layer, 2, null, ending), d ?? FullDeck, Pick(j), opts ?? new EndOptions());
        }

        private static Lineage NoLifeDeath(Lineage lineage, int layer) =>
            Inheritance.RecordDeath(lineage, null, new LifePlace(layer), Array.Empty<CardEntry>(), null);

        // ---- R-01 / R-02: lineage, heirs, names ------------------------------------------------------------

        [Test]
        public void R01_ANewLineageHoldsNoLifeNoTraceNoSurvivor()
        {
            var l = Inheritance.NewLineage(Seed);
            Assert.That(l.Version, Is.EqualTo(1));
            Assert.That(l.Seed, Is.EqualTo("42"));
            Assert.That(l.Lives, Is.Empty);
            Assert.That(l.Current, Is.Null);
            Assert.That(l.Traces, Is.Empty);
            Assert.That(l.Survivor, Is.Null);
            Assert.That(l.Player.CarvingTaken, Is.False);
            Assert.That(l.Player.RootDefeated, Is.False);
        }

        [Test]
        public void R02_EveryHeirHasAPersonalName_DeterministicFromTheLineageSeed()
        {
            Assert.That(Inheritance.HeirName(Seed, 1), Is.EqualTo(Inheritance.HeirName(42, 1)));
            Assert.That(Inheritance.HeirName(Seed, 1), Is.EqualTo(Inheritance.HeirName("42", 1)));
            var first = Enumerable.Range(1, HeirNames.Count).Select(g => Inheritance.HeirName(Seed, g)).ToList();
            Assert.That(first.Distinct().Count(), Is.EqualTo(HeirNames.Count), "one pass over the pool repeats no name");
            foreach (var n in first) Assert.That(HeirNames.All, Does.Contain(n));
            Assert.That(Inheritance.HeirName(Seed, HeirNames.Count + 1), Is.Not.EqualTo(Inheritance.HeirName(Seed, HeirNames.Count)),
                "a new pass does not repeat the last name");
            Assert.That(HeirNames.All, Does.Not.Contain("ガルド").And.Not.Contain("セルク"), "the bosses keep their names");
            var others = Enumerable.Range(0, 8).Select(i => Inheritance.HeirName(7UL + (ulong)i, 1)).ToList();
            Assert.That(others.Distinct().Count(), Is.GreaterThan(1), "different lineages start with different heirs");
        }

        [Test]
        public void R02_TheHallNamesTheNextHeirBeforeTheLifeStarts_AndStartLifeGivesTheSameHeir()
        {
            var l = Inheritance.NewLineage(Seed);
            var heir = Inheritance.NextHeir(l);
            var s = Inheritance.StartLife(l, 5);
            Assert.That(s.Life.Generation, Is.EqualTo(heir.Generation));
            Assert.That(s.Life.Name, Is.EqualTo(heir.Name));
            Assert.That(s.Life.Tier, Is.EqualTo(heir.Tier));
            Assert.That(Inheritance.HallView(l).NextHeir.Name, Is.EqualTo(heir.Name));
            var during = Inheritance.NextHeir(s.Lineage);
            Assert.That((during.Generation, during.Name, during.Tier), Is.EqualTo((1, heir.Name, 0)));
        }

        // ---- R-03: starting a life ------------------------------------------------------------------------

        [Test]
        public void R03_TheFirstLife_Generation1Tier0BaseSlotsEmptyJournalAnEmptyRunLoadout()
        {
            var s = Inheritance.StartLife(Inheritance.NewLineage(Seed), 9);
            Assert.That(s.Life.Generation, Is.EqualTo(1));
            Assert.That(s.Life.Tier, Is.EqualTo(0));
            Assert.That(s.Seed, Is.EqualTo(9UL));
            Assert.That(s.Life.Seed, Is.EqualTo("9"));
            Assert.That(s.ToolSlots, Is.EqualTo(RunLoadout.ToolSlots));
            Assert.That(s.ConsumableSlots, Is.EqualTo(RunLoadout.ConsumableSlots));
            Assert.That(s.DeckMin, Is.EqualTo(Constants.DeckMin));
            Assert.That(s.DeckMax, Is.EqualTo(Constants.DeckMax));
            Assert.That(s.Loadout, Is.InstanceOf<RunLoadout>());
            Assert.That(s.Loadout.ToolSlotCount, Is.EqualTo(3));
            Assert.That(J(s.Journal), Is.EqualTo("{\"Disclosure\":{},\"Pages\":[],\"Memo\":\"\",\"Scribbles\":[]}"));
            Assert.That(s.SurvivorBonus, Is.False);
            Assert.That(s.TraceLayers, Is.Empty);
            Assert.That(s.Attire.Name, Is.EqualTo(AttireTiers.All[0].Name));
        }

        [Test]
        public void R03_ALifeWithoutASeedTakesOneDrawnFromTheLineageSeed()
        {
            var l = Inheritance.NewLineage(Seed);
            var a = Inheritance.StartLife(l);
            var b = Inheritance.StartLife(l);
            Assert.That(a.Seed, Is.EqualTo(b.Seed));
            Assert.That(a.Seed, Is.EqualTo(Inheritance.LifeSeed(l, 1)));
            Assert.That(a.Seed, Is.TypeOf<ulong>());
        }

        [Test]
        public void R03_ASecondLifeCannotStartWhileOneIsInProgress()
        {
            var s = Inheritance.StartLife(Inheritance.NewLineage(Seed), 1);
            ThrowsRule(() => Inheritance.StartLife(s.Lineage, 2), "in progress");
        }

        // ---- R-04 / R-05: death leaves a trace with the candidates --------------------------------------------

        [Test]
        public void R04_ADeathClosesTheLifeAndLeavesATraceAtThePlaceOfDeath()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 4, nodeId: 7);
            Assert.That(l.Current, Is.Null);
            Assert.That(l.Lives.Count, Is.EqualTo(1));
            var life = l.Lives[0];
            Assert.That(life.Ending, Is.EqualTo(LifeEnding.Fallen));
            Assert.That(life.Layer, Is.EqualTo(4));
            Assert.That(life.NodeId, Is.EqualTo(7));
            Assert.That(life.TraceId, Is.EqualTo("trace-1"));
            Assert.That(l.Traces.Count, Is.EqualTo(1));
            var t = l.Traces[0];
            Assert.That(t.Id, Is.EqualTo("trace-1"));
            Assert.That(t.Layer, Is.EqualTo(4));
            Assert.That(t.NodeId, Is.EqualTo(7));
            Assert.That(t.Generation, Is.EqualTo(1));
            Assert.That(t.Name, Is.EqualTo(life.Name));
        }

        [Test]
        public void R04_Candidates_EveryCardAt1DanOrMoreAndEveryFilledPage_NothingElse()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 4, ending: RunPhase.MiasmaDeath);
            var ids = l.Traces[0].Candidates.Select(c => c.Id).ToList();
            Assert.That(ids, Is.EqualTo(new[]
            {
                "card:kansatsu", "card:tsuki", "card:harai", // rank 3, 2, 1 — rank 0 is not a candidate
                "enemy:sabiyari", "enemy:shoga", // disclosure 0 is not a filled page
                // the 2 rare pages hold nothing yet (#107): an empty page is not a filled page (試作で決めたこと 9)
                "memo",
            }));
            Assert.That(l.Lives[0].Ending, Is.EqualTo(LifeEnding.MiasmaDeath));
            var memo = l.Traces[0].Candidates.First(c => c.Id == "memo");
            Assert.That(memo.Kind, Is.EqualTo(CandidateKind.Scribble));
            Assert.That(memo.Text, Is.EqualTo("門竜は押すと崩れる"));
        }

        [Test]
        public void R04_ARarePageIsACandidateOnlyOnceSomethingIsWrittenOnIt()
        {
            var j = new Journal(null, new[]
            {
                new JournalPage("page-1", "希少な頁 1"),
                new JournalPage("r-2", "崩れた天井の頁", "大穴の下は陽が届く"),
            }, "");
            var l = DieAt(Inheritance.NewLineage(Seed), 4, Array.Empty<CardEntry>(), j);
            var rows = l.Traces[0].Candidates.Select(c => (c.Id, c.Kind, c.Name, c.Text)).ToList();
            Assert.That(rows, Is.EqualTo(new[] { ("page:r-2", CandidateKind.DungeonPage, "崩れた天井の頁", (string?)"大穴の下は陽が届く") }),
                "the empty page is left out");
            var s = Inheritance.StartLife(l, 1);
            var r = Inheritance.TakeTrace(s.Lineage, "trace-1", new[] { "page:r-2" });
            Assert.That(Json.Stringify(r.Taken.Journal.ToJson()["Pages"]),
                Is.EqualTo("[{\"Id\":\"r-2\",\"Title\":\"崩れた天井の頁\",\"Text\":\"大穴の下は陽が届く\"}]"), "the page keeps what is written on it");
            Assert.That(Inheritance.HallView(l).Traces[0].CountLine, Is.EqualTo(InheritanceText.CandidateCount(0, 1, 0)));
        }

        [Test]
        public void R04_ATraceSavedWhileEmptyPagesWereCandidatesHidesThem_AndAnOldTakeOfOneStillReplays()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 4);
            var json = Inheritance.ToJson(l);
            var trace0 = (JsonObject)((List<object?>)json["Traces"]!)[0]!;
            ((List<object?>)trace0["Candidates"]!).Add(new JsonObject
            {
                ["Id"] = "page:page-1",
                ["Kind"] = "DungeonPage",
                ["PageId"] = "page-1",
                ["Name"] = "希少な頁 1",
            });
            var old = Inheritance.FromJson(json);
            var t = Inheritance.HallView(old).Traces[0];
            Assert.That(t.Candidates.Any(c => c.Kind == CandidateKind.DungeonPage), Is.False, "not offered");
            Assert.That(t.CountLine, Is.EqualTo("遺産の候補：技術 3 ／ 頁 2 ／ 走り書き 1"), "not counted");
            var s = Inheritance.StartLife(old, 1);
            Assert.That(Inheritance.TraceNodeView(s.Lineage, 4)!.Candidates.Any(c => c.Id == "page:page-1"), Is.False);
            var r = Inheritance.TakeTrace(s.Lineage, "trace-1", new[] { "card:tsuki", "page:page-1" });
            Assert.That(Json.Stringify(r.Taken.Journal.ToJson()["Pages"]), Is.EqualTo("[{\"Id\":\"page-1\",\"Title\":\"希少な頁 1\"}]"),
                "a logged take from an old save loads");
        }

        [Test]
        public void R04_ThePageOfTermsIsNeverACandidate()
        {
            // The JS journal also carries Terms; the C# journal has no place for them at all.
            var j = Journal.WithPageCount(0, null, "");
            var l = DieAt(Inheritance.NewLineage(Seed), 4, Array.Empty<CardEntry>(), j);
            Assert.That(l.Traces[0].Candidates, Is.Empty);
            Assert.That(J(Inheritance.NormalizeJournal(j)), Is.EqualTo(J(Inheritance.EmptyJournal())));
        }

        [Test]
        public void R04_AnInheritedCardKeepsItsRank_ItsTicksStartAtThatRank()
        {
            var deck = new[]
            {
                new CardEntry("a", 1), new CardEntry("b", 2), new CardEntry("c", 3),
                new CardEntry("r", 2, thresholds: Constants.MasteryThresholdsReform),
            };
            var l = DieAt(Inheritance.NewLineage(Seed), 4, deck, NullJournal);
            var ticks = l.Traces[0].Candidates.Select(c => (c.CardId, c.Rank, c.Ticks)).ToList();
            Assert.That(ticks, Is.EqualTo(new[]
            {
                ((string?)"c", (int?)3, (int?)15), ("b", 2, 8), ("r", 2, 6), ("a", 1, 3),
            }));
        }

        [Test]
        public void R04_AMasteryBookDeck_RecordsAndProfileOfGiveRanksThresholdsAndNames()
        {
            var book = new MasteryBook(
                null,
                new Dictionary<string, CardMastery>
                {
                    ["kansatsu"] = new CardMastery("kansatsu", 9, 2),
                    ["zero"] = new CardMastery("zero", 1, 0),
                },
                new[] { new MasteryProfile("kansatsu", MasteryKind.Reform, new[] { "精査", "洞察", "看破" }) });
            var l = DieAt(Inheritance.NewLineage(Seed), 3, book, NullJournal);
            Assert.That(l.Traces[0].Candidates.Select(c => (c.CardId, c.Rank, c.Ticks, c.Name)).ToList(),
                Is.EqualTo(new[] { ((string?)"kansatsu", (int?)2, (int?)6, "洞察") }));

            var s = Inheritance.StartLife(Inheritance.NewLineage(Seed), 1);
            var named = Inheritance.RecordDeath(s.Lineage, null, new LifePlace(3, ending: RunPhase.Fallen), Deck(("kansatsu", 1)), null,
                new EndOptions { CardName = (id, rank) => $"{id}@{rank}", EnemyName = id => $"敵 {id}" });
            Assert.That(named.Traces[0].Candidates[0].Name, Is.EqualTo("kansatsu@1"));
            var s2 = Inheritance.StartLife(Inheritance.NewLineage(Seed), 1);
            var enemies = Inheritance.RecordDeath(s2.Lineage, null, new LifePlace(3), Array.Empty<CardEntry>(), new Journal(D(("x", 1))),
                new EndOptions { EnemyName = id => $"敵 {id}" });
            Assert.That(enemies.Traces[0].Candidates[0].Name, Is.EqualTo("敵 x"));
        }

        [Test]
        public void R05_ShallowDeathsKeepCardsAt2DanOrMoreOutOfTheLegacy([Values(1, 2)] int layer)
        {
            var t = DieAt(Inheritance.NewLineage(Seed), layer).Traces[0];
            Assert.That(t.Candidates.Where(c => c.Kind == CandidateKind.Card).Select(c => c.CardId), Is.EqualTo(new[] { "harai" }), $"layer {layer}");
            Assert.That(t.Withheld.Select(w => (w.CardId, w.Rank)), Is.EqualTo(new[] { ("kansatsu", 3), ("tsuki", 2) }));
            Assert.That(t.Candidates.Any(c => c.Kind == CandidateKind.EnemyPage), Is.True, "pages stay from any layer");
        }

        [Test]
        public void R05_FromLayer3DownEveryCardAt1DanOrMoreStays([Values(3, 5, 7)] int layer)
        {
            var t = DieAt(Inheritance.NewLineage(Seed), layer).Traces[0];
            Assert.That(t.Candidates.Where(c => c.Kind == CandidateKind.Card).Select(c => c.CardId),
                Is.EqualTo(new[] { "kansatsu", "tsuki", "harai" }));
            Assert.That(t.Withheld, Is.Empty);
        }

        [Test]
        public void R04_ADeathNeedsALifeInProgressItsGenerationADeathEndingAndALayer1To7()
        {
            var l = Inheritance.NewLineage(Seed);
            var none = Array.Empty<CardEntry>();
            ThrowsRule(() => Inheritance.RecordDeath(l, null, new LifePlace(1), none, null), "no life");
            var s = Inheritance.StartLife(l, 1);
            ThrowsRule(() => Inheritance.RecordDeath(s.Lineage, 2, new LifePlace(1), none, null), "not the life in progress");
            ThrowsRule(() => Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(1, ending: RunPhase.Survived), none, null), "Survived is not one of");
            ThrowsRule(() => Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(1, ending: RunPhase.Exploring), none, null), "Exploring");
            // JS: Ending 'toString' — a value that is not a RunPhase.
            ThrowsRule(() => Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(1, ending: (RunPhase)99), none, null), "not a RunPhase");
            ThrowsRule(() => Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(8), none, null), "layer");
            ThrowsRule(() => Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(1), none, new Journal(D(("a", 3)))), "disclosure");
            ThrowsRule(() => Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(1), Deck(("a", 1), ("a", 2)), null), "twice");
            var closed = Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(1), none, null);
            ThrowsRule(() => Inheritance.RecordDeath(closed, s.Life, new LifePlace(1), none, null), "no life");
        }

        // ---- R-06 / R-07: traces stay until collected, one per layer ----------------------------------------

        [Test]
        public void R06_TracesAreKeptUntilCollected_ALaterDeathDoesNotOverwriteThem()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 3);
            l = DieAt(l, 5);
            l = DieAt(l, 3);
            Assert.That(l.Traces.Select(t => (t.Id, t.Layer)), Is.EqualTo(new[] { ("trace-1", 3), ("trace-2", 5), ("trace-3", 3) }));
            Assert.That(Inheritance.HallView(l).Traces.Count, Is.EqualTo(3), "the hall shows every untaken trace");
        }

        [Test]
        public void R07_ALayerHoldsAtMostOneTraceNode_TheOldestUntakenOne_TheOthersWait()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 3);
            l = DieAt(l, 5);
            l = DieAt(l, 3);
            Assert.That(Inheritance.TraceForLayer(l, 3)!.Id, Is.EqualTo("trace-1"));
            Assert.That(Inheritance.TraceForLayer(l, 5)!.Id, Is.EqualTo("trace-2"));
            Assert.That(Inheritance.TraceForLayer(l, 4), Is.Null);
            Assert.That(Inheritance.TraceLayers(l), Is.EqualTo(new[] { 3, 5 }));
            var views = Inheritance.HallView(l).Traces;
            Assert.That(views.Select(v => (v.Id, v.Visible, v.AppearsLine)), Is.EqualTo(new[]
            {
                ("trace-1", true, InheritanceText.Appears),
                ("trace-2", true, InheritanceText.Appears),
                ("trace-3", false, InheritanceText.Waits),
            }));
            var s = Inheritance.StartLife(l, 1);
            Assert.That(s.TraceLayers, Is.EqualTo(new[] { 3, 5 }));
            ThrowsRule(() => Inheritance.TakeTrace(s.Lineage, "trace-3", Array.Empty<string>(), 3), "waits behind");
        }

        // ---- R-08 .. R-11: taking a trace -------------------------------------------------------------------

        [Test]
        public void R08_AtTheTraceNode_UpTo3PicksNoDuplicatesOnlyItsCandidates()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 2); // kansatsu and tsuki are withheld
            var s = Inheritance.StartLife(l, 1).Lineage;
            ThrowsRule(() => Inheritance.TakeTrace(s, "trace-1", new[] { "card:harai", "enemy:sabiyari", "page:page-1", "memo" }), "at most 3");
            ThrowsRule(() => Inheritance.TakeTrace(s, "trace-1", new[] { "memo", "memo" }), "twice");
            ThrowsRule(() => Inheritance.TakeTrace(s, "trace-1", new[] { "card:nothing" }), "not a candidate");
            ThrowsRule(() => Inheritance.TakeTrace(s, "trace-1", new[] { "card:kansatsu" }), "not a candidate");
            ThrowsRule(() => Inheritance.TakeTrace(s, "trace-9", Array.Empty<string>()), "no untaken trace");
            var r = Inheritance.TakeTrace(s, "trace-1", new[] { "card:harai", "enemy:sabiyari", "memo" }, 2);
            Assert.That(r.Taken.TraceId, Is.EqualTo("trace-1"));
            Assert.That(r.Taken.Lost, Is.EqualTo(l.Traces[0].Candidates.Count - 3));
        }

        [Test]
        public void R08_TakingNothingIsAllowedAndStillCollectsTheTrace()
        {
            var s = Inheritance.StartLife(DieAt(Inheritance.NewLineage(Seed), 3), 1).Lineage;
            var r = Inheritance.TakeTrace(s, "trace-1", Array.Empty<string>());
            Assert.That(r.Lineage.Traces, Is.Empty);
            Assert.That(r.Lineage.Current!.TakenTraceId, Is.EqualTo("trace-1"));
            Assert.That(r.Taken.Cards, Is.Empty);
            // null picks read as none, as `picks ?? []` does
            Assert.That(Inheritance.TakeTrace(s, "trace-1", null).Taken.Cards, Is.Empty);
        }

        [Test]
        public void R09_ALifeTakesOneTraceOnly_TheFirstVisited_LaterTracesStayForLaterLives()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 3);
            l = DieAt(l, 5);
            var s = Inheritance.StartLife(l, 1).Lineage;
            var r = Inheritance.TakeTrace(s, "trace-1", new[] { "card:harai" });
            ThrowsRule(() => Inheritance.TakeTrace(r.Lineage, "trace-2", Array.Empty<string>()), "already took trace-1");
            Assert.That(Inheritance.TraceForLayer(r.Lineage, 5), Is.Null, "no trace node appears for the rest of this life");
            Assert.That(Inheritance.TraceLayers(r.Lineage), Is.Empty);
            var node = Inheritance.TraceNodeView(r.Lineage, 5)!;
            Assert.That(node.CanTake, Is.False);
            Assert.That(node.Refusal, Is.EqualTo(InheritanceText.AlreadyTaken));
            var after = NoLifeDeath(r.Lineage, 1);
            Assert.That(Inheritance.StartLife(after, 1).TraceLayers, Is.EqualTo(new[] { 1, 5 }), "trace-2 is still there for the next life");
        }

        [Test]
        public void R10_TheTakenTraceIsCollected_ItLeavesTheList_TheDeadLifeRecordsWhoTookIt()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 3);
            var s = Inheritance.StartLife(l, 1).Lineage;
            var r = Inheritance.TakeTrace(s, "trace-1", new[] { "enemy:shoga" });
            Assert.That(r.Lineage.Traces, Is.Empty);
            Assert.That(r.Lineage.Lives[0].CollectedBy, Is.EqualTo(2));
            var done = NoLifeDeath(r.Lineage, 1);
            var past = Inheritance.HallView(done).PastLives.First(p => p.Generation == 1);
            Assert.That(past.TraceLine, Is.EqualTo(InheritanceText.TraceStatus.Taken(2, done.Lives[1].Name)));
        }

        [Test]
        public void R11_TheTakenCardsKeepTheirRank_PagesAndScribblesArriveAsJournalAdditions()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 4);
            var s = Inheritance.StartLife(l, 1);
            var r = Inheritance.TakeTrace(s.Lineage, "trace-1", new[] { "card:kansatsu", "enemy:sabiyari", "memo" }, 4);
            Assert.That(r.Taken.Cards.Select(c => (c.CardId, c.Rank, c.Ticks, c.Name)), Is.EqualTo(new[] { ("kansatsu", 3, 15, (string?)"kansatsu") }));
            Assert.That(r.Taken.Journal.Disclosure, Is.EqualTo(new Dictionary<string, int> { ["sabiyari"] = 2 }));
            Assert.That(r.Taken.Journal.Scribbles.Select(x => (x.Generation, x.Name, x.Text)),
                Is.EqualTo(new[] { (1, (string?)l.Lives[0].Name, (string?)"門竜は押すと崩れる") }));
            var merged = Inheritance.MergeJournal(Journal.WithPageIds(new[] { "p" }, D(("sabiyari", 1), ("other", 1)), "自分のメモ"), r.Taken.Journal);
            Assert.That(merged.Disclosure.ToList(), Is.EqualTo(D(("other", 1), ("sabiyari", 2))));
            Assert.That(merged.Memo, Is.EqualTo("自分のメモ"), "the memo stays the heir's own");
            Assert.That(merged.Scribbles.Count, Is.EqualTo(1));
            Assert.That(J(Inheritance.MergeJournal(merged, r.Taken.Journal)), Is.EqualTo(J(merged)), "merging twice changes nothing");
        }

        [Test]
        public void R11_TakingATraceNeedsALifeInProgressAndTheHeirOnItsLayer()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 3);
            ThrowsRule(() => Inheritance.TakeTrace(l, "trace-1", Array.Empty<string>()), "no life");
            var s = Inheritance.StartLife(l, 1).Lineage;
            ThrowsRule(() => Inheritance.TakeTrace(s, "trace-1", Array.Empty<string>(), 4), "on layer 3, not 4");
        }

        [Test]
        public void R11_MergeMastery_RanksKeptTicksTheHigherOneCardAtStep3()
        {
            var inherited = new[] { new CardMastery("b", 15, 3), new CardMastery("a", 3, 1) };
            var fresh = Inheritance.MergeMastery(null, inherited);
            Assert.That(fresh.Values, Is.EqualTo(new[] { new CardMastery("a", 3, 1), new CardMastery("b", 15, 3) }));
            var held = Inheritance.MergeMastery(new Dictionary<string, CardMastery>
            {
                ["z"] = new CardMastery("z", 16, 3),
                ["a"] = new CardMastery("a", 7, 1),
            }, inherited);
            Assert.That(held["b"], Is.EqualTo(new CardMastery("b", 15, 2)), "the life already holds step 3 on z");
            Assert.That(held["a"], Is.EqualTo(new CardMastery("a", 7, 1)));
            Assert.That(held.Keys, Is.EqualTo(new[] { "a", "b", "z" }));
            // The JS's record factory (make) is not needed: the C# builds CardMastery. A take's cards merge the same.
            var made = Inheritance.MergeMastery(new Dictionary<string, CardMastery>(), new[] { new TakenCard("b", 3, 15, "b"), new TakenCard("a", 1, 3, "a") });
            Assert.That(made.Values.Select(r => $"{r.CardId}:{r.Ticks}:{r.Rank}"), Is.EqualTo(new[] { "a:3:1", "b:15:3" }));
        }

        // ---- R-12: the hall only shows ----------------------------------------------------------------------

        [Test]
        public void R12_TheHallShowsTheCandidatesOfEveryTraceAndOffersNoWayToTakeOne()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 2);
            var h = Inheritance.HallView(l);
            var t = h.Traces[0];
            Assert.That(t.PlaceLine, Is.EqualTo(InheritanceText.LayerTitle(2, "■■の秘跡")));
            Assert.That(t.Candidates.Select(c => (c.KindLabel, c.Detail)), Is.EqualTo(new[]
            {
                ("技術", "1 段目"), ("敵の頁", "開示度 2 / 2"), ("敵の頁", "開示度 1 / 2"),
                ("走り書き", "門竜は押すと崩れる"),
            }));
            Assert.That(t.Withheld.Select(w => w.Detail), Is.EqualTo(new[] { "3 段目。第 3 層より浅い死では残らない", "2 段目。第 3 層より浅い死では残らない" }));
            Assert.That(t.CountLine, Is.EqualTo("遺産の候補：技術 1 ／ 頁 2 ／ 走り書き 1"));
            Assert.That(h.TraceRules, Is.EqualTo(InheritanceText.TraceRules));
            // a view-model holds data only: HallView has no method that changes anything (it is a sealed data class)
            Assert.That(typeof(HallView).GetMethods().Where(m => m.DeclaringType == typeof(HallView) && !m.IsSpecialName).Select(m => m.Name),
                Is.EqualTo(new[] { "ToJson" }));
        }

        // ---- R-13 .. R-17: survival ----------------------------------------------------------------------------

        [Test]
        public void R13_TheSurvivorsWholeJournalPassesToTheNextLife_AfterADeathTheJournalStartsEmpty()
        {
            var l = SurviveAt(Inheritance.NewLineage(Seed), 3);
            var s = Inheritance.StartLife(l, 1);
            Assert.That(J(s.Journal), Is.EqualTo(J(Inheritance.NormalizeJournal(FullJournal))));
            Assert.That(s.Life.InheritedJournal, Is.True);
            var died = NoLifeDeath(s.Lineage, 1);
            Assert.That(J(Inheritance.StartLife(died, 1).Journal), Is.EqualTo(J(Inheritance.NormalizeJournal(null))),
                "only the life right after the survivor reads its journal");
        }

        [Test]
        public void R14_TheSurvivorBonus_ToolSlots4AndDeckCap45ForTheNextLifeOnly()
        {
            var l = SurviveAt(Inheritance.NewLineage(Seed), 2);
            var s = Inheritance.StartLife(l, 1);
            Assert.That(s.SurvivorBonus, Is.True);
            Assert.That(s.ToolSlots, Is.EqualTo(RunLoadout.SurvivorBonusToolSlots));
            Assert.That(s.DeckMax, Is.EqualTo(Constants.DeckMax + 5));
            Assert.That(s.Loadout.ToolSlotCount, Is.EqualTo(4));
            var four = Enumerable.Range(0, 4).Select(_ => ReliefItem.Density("bosho_no_men", 1)).ToList();
            Assert.DoesNotThrow(() => new RunLoadout(four, null, s.ToolSlots));
            Assert.That(s.Life.SurvivorFrom, Is.EqualTo(1));
            var next = Inheritance.StartLife(NoLifeDeath(s.Lineage, 1), 1);
            Assert.That(next.SurvivorBonus, Is.False);
            Assert.That(next.ToolSlots, Is.EqualTo(3));
            Assert.That(next.DeckMax, Is.EqualTo(40));
            Assert.That(next.Lineage.Survivor, Is.Null);
        }

        [Test]
        public void R15_OneSurvivorAtMost_ANewSurvivorReplacesThePreviousOne()
        {
            var l = SurviveAt(Inheritance.NewLineage(Seed), 2, opts: new EndOptions { ExtraBossWon = false });
            l = SurviveAt(l, 3, new Journal(memo: "二人目"));
            Assert.That(l.Survivor!.Generation, Is.EqualTo(2));
            Assert.That(l.Survivor.BonusLivesLeft, Is.EqualTo(1));
            var s = Inheritance.StartLife(l, 1);
            Assert.That(s.Life.SurvivorFrom, Is.EqualTo(2));
            Assert.That(s.Journal.Memo, Is.EqualTo("二人目"));
            Assert.That(l.Lives[1].ReplacedSurvivor, Is.Null, "generation 2 already used generation 1's one life of bonus");
            Assert.That(Inheritance.EndingView(l).Lines.Any(x => x.StartsWith("生存者は一人まで", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void R15_ThePreviousSurvivorsRemainingLivesAreDroppedWhenANewOneSurvives()
        {
            var l = SurviveAt(Inheritance.NewLineage(Seed), 7, opts: new EndOptions { ExtraBossWon = true }, ending: RunPhase.Completed);
            Assert.That(l.Survivor!.BonusLivesLeft, Is.EqualTo(2));
            l = SurviveAt(l, 1);
            Assert.That(l.Survivor!.Generation, Is.EqualTo(2));
            Assert.That(l.Survivor.BonusLivesLeft, Is.EqualTo(1));
            Assert.That(l.Lives[1].ReplacedSurvivor, Is.EqualTo(1));
            Assert.That(Inheritance.EndingView(l).Lines, Does.Contain(InheritanceText.SurvivorReplaced(1, l.Lives[0].Name)));
            var s = Inheritance.StartLife(l, 1);
            var after = Inheritance.StartLife(NoLifeDeath(s.Lineage, 1), 1);
            Assert.That(after.SurvivorBonus, Is.False);
        }

        [Test]
        public void R16_TheDeadHeirsLegacyAndTheSurvivorsBonusApplyTogether()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 3);
            l = SurviveAt(l, 2, new Journal(memo: "生存者の手記"));
            var s = Inheritance.StartLife(l, 1);
            Assert.That(s.SurvivorBonus, Is.True);
            Assert.That(s.TraceLayers, Is.EqualTo(new[] { 3 }));
            Assert.That(s.Journal.Memo, Is.EqualTo("生存者の手記"));
            var r = Inheritance.TakeTrace(s.Lineage, "trace-1", new[] { "card:kansatsu", "enemy:sabiyari" }, 3);
            Assert.That(r.Taken.Cards[0].Rank, Is.EqualTo(3));
            var h = Inheritance.HallView(l);
            Assert.That(h.Survivor!.Generation, Is.EqualTo(2));
            Assert.That(h.Survivor.Lines, Is.EqualTo(new[] { "手記を丸ごと受け継ぐ", "ツール枠 +1（3 → 4）", "デッキ上限 +5（40 → 45）" }));
            Assert.That(h.Survivor.DurationLine, Is.EqualTo("次の 1 生だけ"));
            Assert.That(h.Traces.Count, Is.EqualTo(1));
        }

        [Test]
        public void R17_AWonExtraBossStretchesTheSurvivorBonusToTwoLives_TheJournalStillPassesOnce()
        {
            var l = SurviveAt(Inheritance.NewLineage(Seed), 7, opts: new EndOptions { ExtraBossWon = true }, ending: RunPhase.Completed);
            Assert.That(Inheritance.HallView(l).Survivor!.DurationLine, Is.EqualTo("あと 2 生"));
            var a = Inheritance.StartLife(l, 1);
            Assert.That(a.SurvivorBonus, Is.True);
            Assert.That(a.Lineage.Survivor!.BonusLivesLeft, Is.EqualTo(1));
            Assert.That(Inheritance.HallView(NoLifeDeath(a.Lineage, 1)).Survivor!.Lines, Does.Not.Contain("手記を丸ごと受け継ぐ"));
            var b = Inheritance.StartLife(NoLifeDeath(a.Lineage, 1), 1);
            Assert.That(b.SurvivorBonus, Is.True);
            Assert.That(b.ToolSlots, Is.EqualTo(4));
            Assert.That(J(b.Journal), Is.EqualTo(J(Inheritance.NormalizeJournal(null))));
            var c = Inheritance.StartLife(NoLifeDeath(b.Lineage, 1), 1);
            Assert.That(c.SurvivorBonus, Is.False);
            Assert.That(Inheritance.EndingView(l).Lines, Does.Contain("生存者ボーナス：ツール枠 +1 ／ デッキ上限 +5（次の 2 生）"));
        }

        [Test]
        public void R13_SurvivalNeedsASurvivalEnding()
        {
            var s = Inheritance.StartLife(Inheritance.NewLineage(Seed), 1);
            ThrowsRule(() => Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(2, ending: RunPhase.Fallen), Array.Empty<CardEntry>(), null), "not one of");
        }

        // ---- R-18: 踏破 ---------------------------------------------------------------------------------------

        [Test]
        public void R18_Touha_TheHeirSurvives_TheRootIsRecorded_AndACardTraceStaysOnLayer7WithTheRootsCards()
        {
            var opts = new EndOptions { RootCards = new List<CardEntry> { "ne_watari", "ne_shibari", new CardEntry("tsuki", 0) } };
            var l = SurviveAt(Inheritance.NewLineage(Seed), 7, opts: opts, ending: RunPhase.Completed);
            Assert.That(l.Player.RootDefeated, Is.True);
            Assert.That(l.Survivor!.Generation, Is.EqualTo(1));
            Assert.That(l.Lives[0].Ending, Is.EqualTo(LifeEnding.Completed));
            var t = l.Traces[0];
            Assert.That(t.Cleared, Is.True);
            Assert.That(t.Layer, Is.EqualTo(7));
            Assert.That(t.Candidates.Select(c => (c.CardId, c.Rank)), Is.EqualTo(new[]
            {
                ((string?)"kansatsu", (int?)3), ("tsuki", 2), ("harai", 1), ("ne_shibari", 0), ("ne_watari", 0),
            }), "mastered cards, then the root's cards at any rank; a card already listed is not repeated");
            Assert.That(t.Candidates.All(c => c.Kind == CandidateKind.Card), Is.True, "the pages went with the survivor");
            var s = Inheritance.StartLife(l, 1);
            Assert.That(s.Life.Tier, Is.EqualTo(InheritanceRules.ClearedTier));
            Assert.That(s.TraceLayers, Is.EqualTo(new[] { 7 }));
            var e = Inheritance.EndingView(l);
            Assert.That(e.Title, Is.EqualTo("踏破"));
            Assert.That(e.Sentence, Is.EqualTo("歪みの根を倒した。"));
            Assert.That(e.Lines, Does.Contain(InheritanceText.ClearedTraceLeft(InheritanceText.LayerTitle(7, "果てなき世界"))));
        }

        // ---- R-19: 装束 tier ------------------------------------------------------------------------------------

        [Test]
        public void R19_TheHeirsAttireTierComesFromThePreviousLifesReachedLayer_NotFromTheBestEver()
        {
            var expected = new Dictionary<int, int> { [1] = 0, [2] = 1, [3] = 1, [4] = 2, [5] = 2, [6] = 3, [7] = 3 };
            foreach (var pair in expected)
            {
                var dead = DieAt(Inheritance.NewLineage(Seed), pair.Key);
                Assert.That(Inheritance.StartLife(dead, 1).Life.Tier, Is.EqualTo(pair.Value), $"layer {pair.Key}");
                Assert.That(Inheritance.TierAfter(dead.Lives[0]), Is.EqualTo(pair.Value));
            }
            var l = DieAt(Inheritance.NewLineage(Seed), 6);
            l = DieAt(l, 1);
            Assert.That(Inheritance.NextHeir(l).Tier, Is.EqualTo(0), "a shallow life after a deep one sends the next heir back to tier 0");
            var h = Inheritance.HallView(DieAt(Inheritance.NewLineage(Seed), 5));
            Assert.That(h.NextHeir.TierName, Is.EqualTo("銀の縁"));
            Assert.That(h.NextHeir.TierReason, Does.Match("が第 5 層まで届いたため$"));
            Assert.That(h.NextHeir.Attire.Select(a => a.Part), Is.EqualTo(new[] { "面の縁飾り", "マントの裏地", "留め具" }));
            Assert.That(Inheritance.HallView(Inheritance.NewLineage(Seed)).NextHeir.TierReason, Is.EqualTo(InheritanceText.TierFirst));
            Assert.That(AttireTiers.Count, Is.EqualTo(5));
        }

        // ---- R-20: player knowledge, map specs ------------------------------------------------------------------

        [Test]
        public void R20_TheCarvingIsThePlayersKnowledge_ItSurvivesTheDeathAndNamesLayer2()
        {
            var j = Journal.WithPageCount(2, D(("sabiyari", 2), ("shoga", 1), ("shokou", 0)), "門竜は押すと崩れる", carvingTaken: true);
            var l = DieAt(Inheritance.NewLineage(Seed), 2, j: j);
            Assert.That(l.Player.CarvingTaken, Is.True);
            Assert.That(Inheritance.HallView(l).Traces[0].PlaceLine, Is.EqualTo("第 2 層　竜神の秘跡"));
            Assert.That(Inheritance.StartLife(l, 1).CarvingKnown, Is.True);
            var noted = Inheritance.NoteCarving(Inheritance.NewLineage(Seed));
            Assert.That(noted.Player.CarvingTaken, Is.True);
            Assert.That(Inheritance.NoteCarving(noted), Is.SameAs(noted));
        }

        [Test]
        public void R20_MapSpecFor_ATraceLayerTradesOneBattleForATraceNode_AReadCarvingIsNotOfferedAgain()
        {
            var l = Inheritance.NewLineage(Seed);
            foreach (var layer in new[] { 1, 2, 3, 4, 5, 6, 7 }) l = DieAt(l, layer);
            l = Inheritance.StartLife(l, 1).Lineage;
            foreach (var profile in SevenLayers.All)
            {
                var spec = Inheritance.MapSpecFor(l, profile);
                Assert.That(spec.Quotas[NodeKind.Trace], Is.EqualTo(1), $"layer {profile.Layer}");
                int battles(LayerMapSpec x) => x.Quotas.TryGetValue(NodeKind.Battle, out int n) ? n : 0;
                Assert.That(battles(spec), Is.EqualTo(battles(profile.MapSpec) - 1));
                foreach (ulong seed in new ulong[] { 1, 2, 3 })
                {
                    var map = MapGenerator.Generate(spec, seed);
                    Assert.That(map.Nodes.Count(n => n.Kind == NodeKind.Trace), Is.EqualTo(1));
                }
            }
            var carving = Inheritance.NoteCarving(Inheritance.NewLineage(Seed));
            var layer2 = Inheritance.MapSpecFor(carving, SevenLayers.Of(2));
            Assert.That(layer2.Quotas.ContainsKey(NodeKind.Carving), Is.False);
            Assert.That(layer2.Quotas[NodeKind.Battle], Is.EqualTo(SevenLayers.Of(2).MapSpec.Quotas[NodeKind.Battle] + 1));
            Assert.That(Inheritance.MapSpecFor(Inheritance.NewLineage(Seed), SevenLayers.Of(1)), Is.SameAs(SevenLayers.Of(1).MapSpec),
                "nothing to change, the same spec");
        }

        // ---- R-21: saving -------------------------------------------------------------------------------------

        [Test]
        public void R21_ALineageSurvivesTheJsonRoundTripUnchanged()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 4);
            l = SurviveAt(l, 2);
            var s = Inheritance.StartLife(l, 123456789012345678UL);
            var r = Inheritance.TakeTrace(s.Lineage, "trace-1", new[] { "card:tsuki", "memo" });
            foreach (var x in new[] { l, s.Lineage, r.Lineage })
            {
                Assert.That(S(Inheritance.Deserialize(Inheritance.Serialize(x))), Is.EqualTo(S(x)));
                Assert.That(S(Inheritance.FromJson(Inheritance.ToJson(x))), Is.EqualTo(S(x)));
            }
            Assert.That(Inheritance.Deserialize(Inheritance.Serialize(s.Lineage)).Current!.Seed, Is.EqualTo("123456789012345678"));
        }

        [Test]
        public void R21_ADeathSavedAtOnceIsStillThereAfterAReload_AndATakenTraceCannotBeTakenAgain()
        {
            var dead = Inheritance.Deserialize(Inheritance.Serialize(DieAt(Inheritance.NewLineage(Seed), 3)));
            Assert.That(dead.Traces.Count, Is.EqualTo(1));
            Assert.That(dead.Current, Is.Null);
            var s = Inheritance.StartLife(dead, 1);
            var took = Inheritance.Deserialize(Inheritance.Serialize(Inheritance.TakeTrace(s.Lineage, "trace-1", new[] { "card:harai" }).Lineage));
            ThrowsRule(() => Inheritance.TakeTrace(took, "trace-1", new[] { "card:kansatsu" }), "already took");
        }

        [Test]
        public void R21_ASaveThatDoesNotReadIsRefused()
        {
            JsonObject Good() => Inheritance.ToJson(DieAt(Inheritance.NewLineage(Seed), 3));
            JsonObject With(Action<JsonObject> change)
            {
                var g = Good();
                change(g);
                return g;
            }
            JsonObject First(JsonObject g, string list) => (JsonObject)((List<object?>)g[list]!)[0]!;

            ThrowsRule(() => Inheritance.FromJson(null), "not an object");
            ThrowsRule(() => Inheritance.FromJson(With(g => g["Version"] = 2)), "version");
            ThrowsRule(() => Inheritance.FromJson(With(g => g["Seed"] = "x")), "seed");
            ThrowsRule(() => Inheritance.FromJson(With(g => First(g, "Lives")["Generation"] = 2)), "out of order");
            ThrowsRule(() => Inheritance.FromJson(With(g => First(g, "Lives")["Ending"] = "Exploring")), "ending");
            ThrowsRule(() => Inheritance.FromJson(With(g => First(g, "Traces")["Candidates"] = new List<object?>
            {
                new JsonObject { ["Id"] = "x", ["Kind"] = "Gold", ["Name"] = "x" },
            })), "candidate");
        }

        // ---- R-22 / R-23: purity --------------------------------------------------------------------------------

        [Test]
        public void R22_NoFunctionChangesItsInput_EveryLineageIsDeepFrozen()
        {
            var l = DieAt(Inheritance.NewLineage(Seed), 3);
            string before = S(l);
            var s = Inheritance.StartLife(l, 1);
            Inheritance.TakeTrace(s.Lineage, "trace-1", new[] { "card:harai" });
            Inheritance.HallView(l);
            Inheritance.EndingView(l);
            Assert.That(S(l), Is.EqualTo(before));
            Assert.That(((ICollection<Trace>)l.Traces).IsReadOnly, Is.True);
            Assert.That(((ICollection<Candidate>)l.Traces[0].Candidates).IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => ((ICollection<Trace>)l.Traces).Add(l.Traces[0]));
            Assert.That(l.Traces, Is.InstanceOf<ReadOnlyCollection<Trace>>());
            // Every property of the lineage types is get-only.
            foreach (var type in new[] { typeof(Lineage), typeof(LifeRecord), typeof(Trace), typeof(Candidate), typeof(Survivor), typeof(Journal), typeof(PlayerKnowledge) })
            {
                Assert.That(type.GetProperties().Where(p => p.CanWrite).Select(p => p.Name), Is.Empty, type.Name);
            }
        }

        [Test]
        public void R23_TheSameCallsGiveTheSameLineage_ByteForByte()
        {
            string Run()
            {
                var l = Inheritance.NewLineage(99);
                l = DieAt(l, 3, seed: 5);
                l = SurviveAt(l, 4);
                var s = Inheritance.StartLife(l);
                return S(Inheritance.TakeTrace(s.Lineage, "trace-1", new[] { "memo" }).Lineage);
            }
            string first = Run();
            string second = Run();
            Assert.That(second, Is.EqualTo(first));
        }

        // ---- R-24 / R-25: the screens' text -------------------------------------------------------------------

        [Test]
        public void R24_EndingCard_TheEndSentenceWhatStaysAndTheNextHeir()
        {
            var miasma = Inheritance.EndingView(DieAt(Inheritance.NewLineage(Seed), 4, ending: RunPhase.MiasmaDeath));
            Assert.That(miasma.Title, Is.EqualTo("瘴気死"));
            Assert.That(miasma.Sentence, Is.EqualTo("瘴気が 100% に達し、第 4 層でこの生が終わった。"));
            Assert.That(miasma.Lines, Is.EqualTo(new[]
            {
                "第 4 層　輝晶の間に痕跡と手記が残った",
                "遺産の候補：技術 3 ／ 頁 2 ／ 走り書き 1",
                "次の踏破者が第 4 層を訪れると、3 つまで選んで受け取れる",
            }));
            var shallow = DieAt(Inheritance.NewLineage(Seed), 1, Deck(("kansatsu", 2)), NullJournal);
            var fallen = Inheritance.EndingView(shallow);
            Assert.That(fallen.Sentence, Is.EqualTo("第 1 層の戦いで倒れた。"));
            Assert.That(fallen.Lines, Is.EqualTo(new[] { "第 1 層　燦光の樹海に痕跡と手記が残った", InheritanceText.NothingLeft, "浅い層の死では残らない札：kansatsu" }));
            Assert.That(fallen.NextLine, Is.EqualTo($"次の踏破者：第 2 代 {Inheritance.HeirName(Seed, 2)}"));
            Assert.That(fallen.Button, Is.EqualTo("継承の間へ"));
            var survived = Inheritance.EndingView(SurviveAt(Inheritance.NewLineage(Seed), 3));
            Assert.That(survived.Sentence, Is.EqualTo("生きたまま、第 3 層でこの生を閉じた。"));
            Assert.That(survived.Lines, Is.EqualTo(new[] { "手記は丸ごと次の生へ渡る", "生存者ボーナス：ツール枠 +1 ／ デッキ上限 +5（次の 1 生）" }));
            ThrowsRule(() => Inheritance.EndingView(Inheritance.NewLineage(Seed)), "no closed life");
        }

        [Test]
        public void R25_Hall_FirstVisit_PastLivesNewestFirst_TheNextHeirLine()
        {
            var empty = Inheritance.HallView(Inheritance.NewLineage(Seed));
            Assert.That(empty.Title, Is.EqualTo("継承の間"));
            Assert.That(empty.Empty, Is.EqualTo(InheritanceText.HallEmpty));
            Assert.That(empty.NoTraces, Is.EqualTo(InheritanceText.NoTraces));
            Assert.That(empty.Survivor, Is.Null);
            Assert.That(empty.StartButton, Is.EqualTo("出立の支度へ"));
            Assert.That(new StringInfo(empty.StartButton).LengthInTextElements, Is.LessThanOrEqualTo(10), "fixed labels stay within 10 characters");
            var l = DieAt(Inheritance.NewLineage(Seed), 3);
            l = SurviveAt(l, 2);
            var h = Inheritance.HallView(l);
            Assert.That(h.Empty, Is.EqualTo(""));
            Assert.That(h.PastLives.Select(p => (p.Generation, p.EndingLabel, p.TraceLine)), Is.EqualTo(new[]
            {
                (2, "生存ルート", InheritanceText.TraceStatus.None),
                (1, "倒れた", InheritanceText.TraceStatus.Untaken),
            }));
            Assert.That(h.PastLives[1].PlaceLine, Is.EqualTo("第 3 層　大黒蛇の住処"));
            Assert.That(h.NextHeir.Line, Is.EqualTo($"第 3 代 踏破者 {Inheritance.HeirName(Seed, 3)}"));
        }

        [Test]
        public void R25_TraceNodeCard_CandidatesAtMost3ThePickCounter()
        {
            var l = Inheritance.StartLife(DieAt(Inheritance.NewLineage(Seed), 2), 1).Lineage;
            Assert.That(Inheritance.TraceNodeView(l, 3), Is.Null);
            var v = Inheritance.TraceNodeView(l, 2)!;
            Assert.That(v.Title, Is.EqualTo("死亡地点の痕跡"));
            Assert.That(v.TraceId, Is.EqualTo("trace-1"));
            Assert.That(v.MaxPicks, Is.EqualTo(3));
            Assert.That(v.CanTake, Is.True);
            Assert.That(v.Lead, Is.EqualTo("遺産を 3 つまで選んで受け取る。選ばなかったものはここで失われる。"));
            Assert.That(v.WithheldHeading, Is.EqualTo(InheritanceText.WithheldHeading));
            var two = Inheritance.PickState(new[] { "a", "b" });
            Assert.That((two.Line, two.CanPickMore), Is.EqualTo(("2 / 3", true)));
            var three = Inheritance.PickState(new[] { "a", "b", "c" });
            Assert.That((three.Line, three.CanPickMore), Is.EqualTo(("3 / 3", false)));
            var scribble = Inheritance.TraceNodeView(
                Inheritance.StartLife(DieAt(Inheritance.NewLineage(Seed), 3, Array.Empty<CardEntry>(), new Journal(memo: new string('あ', 30))), 1).Lineage, 3)!;
            Assert.That(scribble.Candidates[0].Detail, Is.EqualTo(new string('あ', 20) + "…"));
        }

        // ---- R-26: wiring to the exploration core -----------------------------------------------------------------

        [Test]
        public void R26_PlaceFromStateAndRecordEndCloseALifeStraightFromAFinishedExplorationState()
        {
            var profile = SevenLayers.Of(1);
            var entered = ExplorationReducer.Enter(profile, profile.Map(3), RunLoadout.Empty, 50, 50, 10);
            var fallen = ExplorationReducer.ReportBattle(entered, 0, 0);
            Assert.That(fallen.Phase, Is.EqualTo(RunPhase.Fallen));
            var s = Inheritance.StartLife(Inheritance.NewLineage(Seed), 3);
            var place = Inheritance.PlaceFromState(fallen);
            Assert.That((place.Layer, place.NodeId, place.NodeKind, place.Ending),
                Is.EqualTo((1, (int?)entered.Map.EntryId, (NodeKind?)NodeKind.Battle, (RunPhase?)RunPhase.Fallen)));
            var l = Inheritance.RecordEnd(s.Lineage, s.Life, place, Deck(("a", 1)), null);
            Assert.That(l.Traces[0].NodeKind, Is.EqualTo(NodeKind.Battle));
            Assert.That(Inheritance.HallView(l).Traces[0].PlaceLine, Is.EqualTo("第 1 層　燦光の樹海（通常戦闘）"));
            var alive = Inheritance.RecordEnd(Inheritance.StartLife(l, 1).Lineage, null,
                Inheritance.PlaceFromState(ExplorationReducer.EndLife(entered)), Array.Empty<CardEntry>(), null);
            Assert.That(alive.Survivor!.Generation, Is.EqualTo(2));
            ThrowsRule(() => Inheritance.RecordEnd(Inheritance.StartLife(alive, 1).Lineage, null, Inheritance.PlaceFromState(entered),
                Array.Empty<CardEntry>(), null), "does not end a life");
        }

        [Test]
        public void TheRuleNumbers()
        {
            // JS: the Inheritance namespace object carries the functions and Rules; the C# static class is the namespace.
            Assert.That(InheritanceRules.LegacyPicks, Is.EqualTo(3));
            Assert.That(InheritanceRules.DeepRank, Is.EqualTo(2));
            Assert.That(InheritanceRules.DeepLayer, Is.EqualTo(3));
            Assert.That(InheritanceRules.SurvivorToolSlots, Is.EqualTo(4));
            Assert.That(InheritanceRules.SurvivorDeckBonus, Is.EqualTo(5));
        }
    }
}
