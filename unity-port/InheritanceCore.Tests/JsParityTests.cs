using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonCore;
using InheritanceCore;
using NUnit.Framework;
using static InheritanceCore.Tests.InheritanceTests;

namespace InheritanceCore.Tests
{
    /// <summary>
    /// The C# against what the JS gives (Fixtures/js-oracle.json, written by Fixtures/js-oracle.mjs from the trial's
    /// Inheritance.js): names, seeds, every word of the text table, and whole lineages and view-models byte for byte.
    /// </summary>
    public class JsParityTests
    {
        private static JsonObject? _oracle;

        internal static JsonObject Oracle
        {
            get
            {
                if (_oracle == null)
                {
                    string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "js-oracle.json");
                    _oracle = (JsonObject)Json.Parse(File.ReadAllText(path))!;
                }
                return _oracle;
            }
        }

        private static string O(string key) => (string)Oracle[key]!;

        private static List<object?> L(string key) => (List<object?>)Oracle[key]!;

        private static string S(Lineage l) => Inheritance.Serialize(l);

        private static readonly Journal NoJournal = null!;

        [Test]
        public void HeirNames_AndTheirOrderPerSeed_AreTheJs()
        {
            Assert.That(HeirNames.All, Is.EqualTo(L("heirNames")));
            var names = Enumerable.Range(1, 81).Select(g => Inheritance.HeirName(42UL, g)).ToList();
            Assert.That(names, Is.EqualTo(L("names42")), "two full passes and one more: the pass boundary rule too");
            Assert.That(Enumerable.Range(0, 8).Select(i => Inheritance.HeirName(7UL + (ulong)i, 1)), Is.EqualTo(L("firstNames")));
            Assert.That(Inheritance.HeirName("18446744073709551615", 3), Is.EqualTo(O("bigSeedName")));
            Assert.That(new[] { Inheritance.HeirName("18446744073709551616", 1), Inheritance.HeirName(0UL, 1) },
                Is.EqualTo(L("wrapSeedName")), "a seed of 2^64 wraps to 0, as BigInt.asUintN(64)");
        }

        [Test]
        public void LifeSeeds_AreTheJs()
        {
            var l = Inheritance.NewLineage(42);
            Assert.That(new[] { 1, 2, 3 }.Select(g => Inheritance.LifeSeed(l, g).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Is.EqualTo(L("lifeSeeds")));
        }

        [Test]
        public void TheTextTable_IsTheJs()
        {
            var t = (JsonObject)Oracle["text"]!;
            var consts = new Dictionary<string, string>
            {
                ["HallTitle"] = InheritanceText.HallTitle,
                ["HallLead"] = InheritanceText.HallLead,
                ["HallEmpty"] = InheritanceText.HallEmpty,
                ["RoleName"] = InheritanceText.RoleName,
                ["StartButton"] = InheritanceText.StartButton,
                ["ToHallButton"] = InheritanceText.ToHallButton,
                ["TraceHeading"] = InheritanceText.TraceHeading,
                ["NoTraces"] = InheritanceText.NoTraces,
                ["PastLivesHeading"] = InheritanceText.PastLivesHeading,
                ["SurvivorHeading"] = InheritanceText.SurvivorHeading,
                ["NextHeirHeading"] = InheritanceText.NextHeirHeading,
                ["AttireHeading"] = InheritanceText.AttireHeading,
                ["TraceNodeTitle"] = InheritanceText.TraceNodeTitle,
                ["TakeButton"] = InheritanceText.TakeButton,
                ["LeaveButton"] = InheritanceText.LeaveButton,
                ["AlreadyTaken"] = InheritanceText.AlreadyTaken,
                ["Appears"] = InheritanceText.Appears,
                ["Waits"] = InheritanceText.Waits,
                ["ClearedTraceNote"] = InheritanceText.ClearedTraceNote,
                ["WithheldHeading"] = InheritanceText.WithheldHeading,
                ["DungeonPageDetail"] = InheritanceText.DungeonPageDetail,
                ["TierFirst"] = InheritanceText.TierFirst,
                ["NothingLeft"] = InheritanceText.NothingLeft,
                ["JournalPasses"] = InheritanceText.JournalPasses,
                ["SurvivorJournal"] = InheritanceText.SurvivorJournal,
                ["GenerationLabel"] = InheritanceText.GenerationLabel(3),
                ["HeirTitle"] = InheritanceText.HeirTitle("ルカ"),
                ["HeirLine"] = InheritanceText.HeirLine(3, "ルカ"),
                ["LayerTitle"] = InheritanceText.LayerTitle(2, "■■の秘跡"),
                ["RankDetail"] = InheritanceText.RankDetail(2),
                ["DisclosureDetail"] = InheritanceText.DisclosureDetail(1),
                ["ScribbleName"] = InheritanceText.ScribbleName(4, "ルカ"),
                ["RarePageTitle"] = InheritanceText.RarePageTitle(7),
                ["WithheldReason"] = InheritanceText.WithheldReason(3),
                ["PickLead"] = InheritanceText.PickLead(3),
                ["PickCount"] = InheritanceText.PickCount(1, 3),
                ["TraceOf"] = InheritanceText.TraceOf(2, "ルカ"),
                ["CandidateCount"] = InheritanceText.CandidateCount(1, 2, 3),
                ["TierReason"] = InheritanceText.TierReason(2, "ルカ", 5),
                ["TierCleared"] = InheritanceText.TierCleared(2, "ルカ"),
                ["TraceLeft"] = InheritanceText.TraceLeft("第 1 層　燦光の樹海"),
                ["TraceVisit"] = InheritanceText.TraceVisit(4, 3),
                ["WithheldLine"] = InheritanceText.WithheldLine(new[] { "a", "b" }),
                ["ClearedTraceLeft"] = InheritanceText.ClearedTraceLeft("X"),
                ["SurvivorReplaced"] = InheritanceText.SurvivorReplaced(1, "ルカ"),
                ["NextHeirLine"] = InheritanceText.NextHeirLine(5, "ルカ"),
                ["SurvivorTools"] = InheritanceText.SurvivorTools(3, 4),
                ["SurvivorDeck"] = InheritanceText.SurvivorDeck(40, 45),
                ["SurvivorSpent"] = InheritanceText.SurvivorSpent(2, "ルカ"),
                ["SurvivorSpentLimits"] = InheritanceText.SurvivorSpentLimits(3, 40),
            };
            foreach (var pair in consts) Assert.That(pair.Value, Is.EqualTo((string)t[pair.Key]!), pair.Key);
            Assert.That(InheritanceText.TraceRules, Is.EqualTo((List<object?>)t["TraceRules"]!));
            foreach (CandidateKind k in Enum.GetValues(typeof(CandidateKind)))
                Assert.That(InheritanceText.KindLabel(k), Is.EqualTo(((JsonObject)t["KindLabel"]!)[k.ToString()]), k.ToString());
            foreach (LifeEnding e in Enum.GetValues(typeof(LifeEnding)))
                Assert.That(InheritanceText.EndingTitle(e), Is.EqualTo(((JsonObject)t["EndingTitle"]!)[e.ToString()]), e.ToString());
            foreach (NodeKind n in Enum.GetValues(typeof(NodeKind)))
                Assert.That(InheritanceText.NodeKindLabel(n), Is.EqualTo(((JsonObject)t["NodeKindLabel"]!)[n.ToString()]), n.ToString());
            Assert.That(new[] { LifeEnding.MiasmaDeath, LifeEnding.Fallen, LifeEnding.Completed, LifeEnding.Survived }
                .Select(e => InheritanceText.EndingSentence(e, 6)), Is.EqualTo((List<object?>)t["EndingSentence"]!));
            Assert.That(new[] { InheritanceText.SurvivorBonusLine(1), InheritanceText.SurvivorBonusLine(2) }, Is.EqualTo((List<object?>)t["SurvivorBonusLine"]!));
            Assert.That(new[] { InheritanceText.SurvivorDuration(1), InheritanceText.SurvivorDuration(3) }, Is.EqualTo((List<object?>)t["SurvivorDuration"]!));
            Assert.That(new[] { InheritanceText.TraceStatus.None, InheritanceText.TraceStatus.Untaken, InheritanceText.TraceStatus.Taken(2, "ルカ") },
                Is.EqualTo((List<object?>)t["TraceStatus"]!));
        }

        [Test]
        public void AttireTiers_AreTheJs()
        {
            var json = AttireTiers.All.Select(a => (object?)new JsonObject
            {
                ["Tier"] = a.Tier,
                ["Name"] = a.Name,
                ["Mask"] = a.Mask,
                ["Lining"] = a.Lining,
                ["Clasp"] = a.Clasp,
                ["MaskColor"] = a.MaskColor,
                ["LiningColor"] = a.LiningColor,
                ["ClaspColor"] = a.ClaspColor,
            }).ToList();
            Assert.That(Json.Stringify(json), Is.EqualTo(Json.Stringify(Oracle["attire"])));
        }

        [Test]
        public void R23_TheSameCallsGiveTheJsLineage_ByteForByte()
        {
            var l = Inheritance.NewLineage(99);
            l = DieAt(l, 3, seed: 5);
            l = SurviveAt(l, 4);
            var s = Inheritance.StartLife(l);
            Assert.That(S(Inheritance.TakeTrace(s.Lineage, "trace-1", new[] { "memo" }).Lineage), Is.EqualTo(O("r23")));
        }

        [Test]
        public void AMixedLineage_PagesScribblesIndexLikeEnemyIdsAndATake_IsTheJs()
        {
            var j = new Journal(
                D(("sabiyari", 2), ("shoga", 1), ("shokou", 0)),
                new[] { new JournalPage("r-2", "崩れた天井の頁", "大穴の下は陽が届く"), new JournalPage("page-x") },
                "門竜は押すと崩れる",
                new[] { new Scribble(1, "X", "古い") });
            var l = DieAt(Inheritance.NewLineage(42), 4, j: j);
            l = SurviveAt(l, 2, new Journal(D(("10", 1), ("b", 2), ("2", 1)), memo: "生存者"));
            var s = Inheritance.StartLife(l, 123456789012345678UL);
            var r = Inheritance.TakeTrace(s.Lineage, "trace-1", new[] { "card:tsuki", "memo", "page:r-2" }, 4);
            var mixed = L("mixed");
            Assert.That(S(l), Is.EqualTo(mixed[0]));
            Assert.That(S(s.Lineage), Is.EqualTo(mixed[1]));
            Assert.That(S(r.Lineage), Is.EqualTo(mixed[2]));
            Assert.That(Json.Stringify(r.Taken.ToJson()), Is.EqualTo(mixed[3]));
            var start = new JsonObject
            {
                ["Journal"] = s.Journal.ToJson(),
                ["JournalFrom"] = s.JournalFrom?.ToJson(),
                ["heir"] = Inheritance.HeirJournal(s.Journal, s.JournalFrom).ToJson(),
                ["TraceLayers"] = s.TraceLayers.Cast<object?>().ToList(),
                ["ToolSlots"] = s.ToolSlots,
                ["DeckMax"] = s.DeckMax,
                ["Seed"] = s.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            Assert.That(Json.Stringify(start), Is.EqualTo(O("mixedStart")));
            // the take's JSON reads back as the same take (the recorder logs it for Expedition.Inherit)
            Assert.That(Json.Stringify(TakenLegacy.FromJson(Json.Parse((string)mixed[3]!)).ToJson()), Is.EqualTo(mixed[3]));
        }

        [Test]
        public void AClearedLineage_ItsHallAndEnding_AreTheJs()
        {
            var opts = new EndOptions { ExtraBossWon = true, RootCards = new List<CardEntry> { "ne_watari", "ne_shibari", new CardEntry("tsuki", 0) } };
            var c = SurviveAt(Inheritance.NewLineage(42), 7, opts: opts, ending: RunPhase.Completed);
            Assert.That(S(c), Is.EqualTo(O("cleared")));
            Assert.That(Json.Stringify(Inheritance.HallView(c).ToJson()), Is.EqualTo(O("clearedHall")));
            Assert.That(Json.Stringify(Inheritance.EndingView(c).ToJson()), Is.EqualTo(O("clearedEnding")));
        }

        [Test]
        public void AHallWithEveryBlock_ItsEndingAndTraceNode_AreTheJs()
        {
            var h = DieAt(Inheritance.NewLineage(42), 3);
            h = SurviveAt(h, 2);
            var hs = Inheritance.StartLife(h, 1);
            var ht = Inheritance.TakeTrace(hs.Lineage, "trace-1", new[] { "card:kansatsu" }, 3);
            h = Inheritance.RecordDeath(ht.Lineage, null, new LifePlace(5, 3, NodeKind.Boss, RunPhase.MiasmaDeath), FullDeck,
                new Journal(memo: new string('x', 25)));
            Assert.That(Json.Stringify(Inheritance.HallView(h).ToJson()), Is.EqualTo(O("hall")));
            Assert.That(Json.Stringify(Inheritance.EndingView(h).ToJson()), Is.EqualTo(O("ending")));
            Assert.That(S(h), Is.EqualTo(O("hallLineage")));
            var hs2 = Inheritance.StartLife(h, 9);
            Assert.That(Json.Stringify(Inheritance.TraceNodeView(hs2.Lineage, 5)!.ToJson()), Is.EqualTo(O("traceNode")));
        }

        [Test]
        public void TheSpentBonusAndTheEmptyHall_AreTheJs()
        {
            var sp = SurviveAt(Inheritance.NewLineage(42), 3);
            var sp2 = Inheritance.StartLife(sp, 2);
            sp = Inheritance.RecordDeath(sp2.Lineage, null, new LifePlace(1, 0, NodeKind.Battle, RunPhase.Fallen), Array.Empty<CardEntry>(), NoJournal);
            Assert.That(Json.Stringify(Inheritance.HallView(sp).ToJson()), Is.EqualTo(O("hallSpent")));
            Assert.That(S(sp), Is.EqualTo(O("hallSpentLineage")));
            Assert.That(Json.Stringify(Inheritance.HallView(Inheritance.NewLineage(42)).ToJson()), Is.EqualTo(O("hallEmpty")));
        }

        [Test]
        public void EveryOracleLineage_ReadsBackAndWritesTheSameBytes()
        {
            foreach (var text in new[] { O("r23"), O("cleared"), O("hallLineage"), O("hallSpentLineage") }.Concat(L("mixed").Take(3).Cast<string>()))
                Assert.That(S(Inheritance.Deserialize(text)), Is.EqualTo(text));
        }
    }
}
