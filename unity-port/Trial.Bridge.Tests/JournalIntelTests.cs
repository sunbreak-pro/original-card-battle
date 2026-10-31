// The map's enemy intel (js: test/ext/journal.test.mjs J-7〜J-15; src/app/intel.js). The JS tests stand a fake Expedition
// in for some cases ({ Session: { RunSeed: 7, Life: 1, … }, DisclosureOf }); here a real Expedition plays it, built with
// the journal's 開示度 the fake held (RunStart.Seed 7, no trial), since the intel reads nothing else of it. J-16 (the
// drawer in battle) is the journal drawer's: Trial.Script.Tests/JournalBookOfTests.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using DungeonContent;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;
using Journal;
using NUnit.Framework;
using static Trial.Bridge.Tests.LifeTestKit;
using JournalPage = Journal.JournalPage;

namespace Trial.Bridge.Tests
{
    public sealed class JournalIntelTests
    {
        private sealed class IntelLife
        {
            public Expedition Ex;
            public JournalIntel Intel;
            public ExplorationSession Session => Ex.Session;
        }

        /// <summary>An Expedition with its JournalIntel bound, as Life.LifeSetup builds one (js: journal.test.mjs:51 lifeWith).</summary>
        private static IntelLife LifeWith(ulong seed, IReadOnlyDictionary<string, int> journal = null, bool trial = false, RunLoadout loadout = null)
        {
            var intel = new JournalIntel();
            var start = new RunStart { Seed = seed, Trial = trial };
            if (loadout != null) start.Loadout = loadout;
            var ex = new Expedition(start, null, null, null, intel, journal);
            intel.Bind(ex);
            return new IntelLife { Ex = ex, Intel = intel };
        }

        /// <summary>js: journal.test.mjs:59 fakeExpedition(levels): the journal's 開示度 on a life of seed 7, an intel bound to it.</summary>
        private static JournalIntel Fake(params (string Id, int Level)[] levels) =>
            LifeWith(7, levels.ToDictionary(l => l.Id, l => l.Level)).Intel;

        private static Dictionary<string, int> Levels(params (string Id, int Level)[] levels) =>
            levels.ToDictionary(l => l.Id, l => l.Level, StringComparer.Ordinal);

        /// <summary>js: journal.test.mjs:68 enemyLines: the combat nodes the screen does not draw far, with their lines.</summary>
        private static List<NodeCard> EnemyLines(ExplorationScreen screen) =>
            screen.Nodes.Where(n => n.Kind.IsCombat() && n.Standing != NodeStanding.Far).ToList();

        private static List<JournalPage> EnemyPagesOf(ExplorationSession session) =>
            session.Screen().Journal.Pages.Where(p => p.Tab == JournalTab.Enemy).ToList();

        // ------------------------------------------------------------------ J-7

        // js: test/ext/journal.test.mjs:260
        [Test]
        public void J7_ACombatNode_NamesItsEnemyFromDisclosure1_AndAddsTheTendencyLineAt2()
        {
            var node = new MapNode(4, 1, 0, NodeKind.Battle);
            Encounter encounter = JournalIntel.EncounterAt(7, node, 2);
            string id = encounter.EnemyIds.First();

            Assert.That(Fake().ForNode(node, 2, false), Is.Null, "開示度 0: 「敵: ？」");
            NodeIntel one = Fake((id, 1)).ForNode(node, 2, false);
            Assert.That(one, Is.Not.Null);
            Assert.That(one.EnemyName, Is.EqualTo(Enemies.ById(id).Name));
            Assert.That(one.Disclosure, Is.EqualTo(1));
            Assert.That(one.Lines, Is.Empty);
            NodeIntel two = Fake((id, 2)).ForNode(node, 2, false);
            Assert.That(two.Disclosure, Is.EqualTo(2));
            Assert.That(two.Lines, Is.EqualTo(new[] { "傾向: 得意な間合い " + EnemyPages.Entry(id).FavoredGap }));

            Assert.That(Fake((id, 2)).ForNode(new MapNode(4, 1, 0, NodeKind.Rest), 2, false), Is.Null, "a node without a fight");
            Assert.That(Fake((id, 2)).ForNode(null, 2, false), Is.Null);

            // On the screen (dungeon_exploration_v4.md §6.3): 「敵: 名前（開示度 n / 2）」, then the 傾向 line.
            IntelLife life = LifeWith(5, Levels(("polearm_warped", 1), ("shadow_hound", 2)));
            ExplorationSession session = life.Session;
            foreach (NodeCard n in EnemyLines(session.Screen()))
            {
                Encounter enc = JournalIntel.EncounterAt(session.RunSeed, session.State.Map.Node(n.Id), 1);
                if (enc.Fights.Count == 1 && enc.EnemyIds.First() == "polearm_warped")
                {
                    Assert.That(n.Lines, Does.Contain("敵: 錆槍の竜兵（開示度 1 / 2）"), string.Join(" | ", n.Lines));
                    Assert.That(n.Lines.Any(l => l.StartsWith("傾向", StringComparison.Ordinal)), Is.False);
                }
                if (enc.Fights.Count == 1 && enc.EnemyIds.First() == "shadow_hound")
                {
                    Assert.That(n.Lines, Does.Contain("敵: 瘴牙の走竜（開示度 2 / 2）"));
                    Assert.That(n.Lines, Does.Contain("傾向: 得意な間合い 0"));
                }
            }
        }

        // ------------------------------------------------------------------ J-8

        // js: test/ext/journal.test.mjs:302
        [Test]
        public void J8_TheNodesEnemy_IsExactlyTheEncounterTheSessionDrawsOnArrival()
        {
            int checkedCount = 0;
            for (ulong seed = 1; seed <= 6; seed++)
            {
                ExplorationSession session = LifeWith(seed).Session;
                void Check()
                {
                    if (!session.HasPendingBattle) return;
                    MapNode node = session.State.Map.Node(session.PendingBattleNode);
                    Encounter drawn = JournalIntel.EncounterAt(session.RunSeed, node, session.State.Profile.Layer);
                    Assert.That(string.Join(">", drawn.Fights.Select(f => string.Join("+", f.EnemyIds))),
                        Is.EqualTo(string.Join(">", session.PendingEncounter.Fights.Select(f => string.Join("+", f.EnemyIds)))), $"seed {seed} node {node.Id}");
                    Assert.That(drawn.IsStandIn, Is.EqualTo(session.PendingEncounter.IsStandIn));
                    checkedCount++;
                }
                Check();
                session.Act(ScreenAction.WinBattle);
                // Every combat node of layers 1〜3: walk to each in turn (wins reported), then down to the boss.
                for (int layer = 1; layer <= 3; layer++)
                {
                    foreach (MapNode node in session.State.Map.Nodes.Where(n => n.Kind.IsCombat() && n.Kind != NodeKind.Boss).ToList())
                    {
                        if (session.State.Phase != RunPhase.Exploring || session.State.HasResolved(node.Id)) continue;
                        foreach (int id in RouteTo(session.State.Map, session.State.CurrentNodeId, node.Id))
                        {
                            if (session.State.Phase != RunPhase.Exploring) break;
                            if (!session.Step(id)) break;
                            Check();
                            if (session.HasPendingBattle) session.Act(ScreenAction.WinBattle);
                        }
                    }
                    if (layer < 3 && session.State.Phase == RunPhase.Exploring)
                    {
                        MapNode boss = session.State.Map.Nodes.First(n => n.Kind == NodeKind.Boss);
                        foreach (int id in RouteTo(session.State.Map, session.State.CurrentNodeId, boss.Id))
                        {
                            if (session.State.Phase != RunPhase.Exploring || !session.Step(id)) break;
                            Check();
                            if (session.HasPendingBattle) session.Act(ScreenAction.WinBattle);
                        }
                    }
                    if (session.State.Phase == RunPhase.LayerCleared || session.State.Phase == RunPhase.PushedOut)
                    {
                        session.Act(ScreenAction.GoToInterlude);
                        if (session.Act(ScreenAction.Descend))
                        {
                            Check();
                            session.Act(ScreenAction.WinBattle);
                        }
                        else break;
                    }
                    else break;
                }
            }
            Assert.That(checkedCount, Is.GreaterThanOrEqualTo(30), $"checked {checkedCount} arrivals");
        }

        // ------------------------------------------------------------------ J-9

        // js: test/ext/journal.test.mjs:348
        [Test]
        public void J9_SeveralEnemies_PlusInOneFight_ArrowBetweenFights_QuestionMarkForOneNotMet_TheLowestDisclosure()
        {
            // Layer 2's elite is the pair 錆槍 + 灰弩 (roster §9); layer 1's is the chain 錆槍 → 瘴牙.
            var pairNode = new MapNode(6, 1, 0, NodeKind.Elite);
            var chainNode = new MapNode(6, 1, 0, NodeKind.Elite);
            Assert.That(JournalIntel.EncounterAt(7, pairNode, 2).Fights.Count, Is.EqualTo(1));
            Assert.That(JournalIntel.EncounterAt(7, chainNode, 1).Fights.Count, Is.EqualTo(2));

            NodeIntel pair = Fake(("polearm_warped", 2)).ForNode(pairNode, 2, false);
            Assert.That(pair.EnemyName, Is.EqualTo("錆槍の竜兵 + ？"));
            Assert.That(pair.Disclosure, Is.EqualTo(0), "the lower of the two (dungeon_exploration_v4.md §6.8)");
            Assert.That(pair.Lines, Is.EqualTo(new[] { "傾向（錆槍の竜兵）: 得意な間合い 1〜2" }));

            NodeIntel both = Fake(("polearm_warped", 2), ("crossbow_hunter", 1)).ForNode(pairNode, 2, false);
            Assert.That(both.EnemyName, Is.EqualTo("錆槍の竜兵 + 灰弩の竜兵"));
            Assert.That(both.Disclosure, Is.EqualTo(1));
            Assert.That(both.Lines, Is.EqualTo(new[] { "傾向（錆槍の竜兵）: 得意な間合い 1〜2" }));

            NodeIntel chain = Fake(("polearm_warped", 2), ("crossbow_hunter", 1)).ForNode(chainNode, 1, false);
            Assert.That(chain.EnemyName, Is.EqualTo("錆槍の竜兵 → ？"));
            NodeIntel chain2 = Fake(("polearm_warped", 2), ("crossbow_hunter", 1), ("shadow_hound", 2)).ForNode(chainNode, 1, false);
            Assert.That(chain2.EnemyName, Is.EqualTo("錆槍の竜兵 → 瘴牙の走竜"));
            Assert.That(chain2.Disclosure, Is.EqualTo(2));
            Assert.That(chain2.Lines, Is.EqualTo(new[] { "傾向（錆槍の竜兵）: 得意な間合い 1〜2", "傾向（瘴牙の走竜）: 得意な間合い 0" }));
            Assert.That(EnemyPages.EncounterName(new[] { new[] { "a", "b" }, new[] { "c" } }), Is.EqualTo("a + b → c"));
        }

        // ------------------------------------------------------------------ J-10

        /// <summary>A layer-1 life whose map holds a 情報収集 node, explorable (js: journal.test.mjs:382 surveyLife).</summary>
        private static IntelLife SurveyLife(IReadOnlyDictionary<string, int> journal = null, ulong seedFrom = 1)
        {
            for (ulong seed = seedFrom; seed <= 200; seed++)
            {
                IntelLife life = LifeWith(seed, journal);
                Explorable(life.Session);
                if (life.Session.State.Map.Nodes.Any(n => n.Kind == NodeKind.Survey)) return life;
            }
            Assert.Fail("no seed in 1..200 has a survey node on layer 1");
            return null;
        }

        private static int SurveyNode(ExplorationSession session) => session.State.Map.Nodes.First(n => n.Kind == NodeKind.Survey).Id;

        // js: test/ext/journal.test.mjs:404
        [Test]
        public void J10_TheSurvey_TheLayerDecidesTheEnemy_AndTheJournalHoldsItAt2()
        {
            // Layer 1: the boss is 瘴牙 (the stand-in, roster §9), the elite is 錆槍 → 瘴牙.
            var map = new LayerMap(1, 0, new[]
            {
                new MapNode(3, 1, 0, NodeKind.Battle), new MapNode(9, 2, 0, NodeKind.Boss), new MapNode(5, 1, 1, NodeKind.Elite), new MapNode(1, 0, 0, NodeKind.Rest),
            }, new Dictionary<int, IReadOnlyList<int>> { [1] = new[] { 3, 5 }, [3] = new[] { 9 }, [5] = new[] { 9 }, [9] = new int[0] });
            Assert.That(Fake().SurveyTarget(1, map), Is.EqualTo("shadow_hound"), "the layer's master first");
            Assert.That(Fake(("shadow_hound", 2)).SurveyTarget(1, map), Is.EqualTo("polearm_warped"), "then the elite's enemies");
            Assert.That(Fake(("shadow_hound", 2), ("polearm_warped", 2)).SurveyTarget(1, map), Is.Null, "nothing left to show");
            Assert.That(new JournalIntel().SurveyTarget(1, map), Is.Null, "unbound");

            // A real layer with the trial rules off: the node's arrival writes the target into the Expedition's journal,
            // the one value the map, the journal and the battle read (§2.1 「戦闘の予兆の精度に直結する」).
            IntelLife life = SurveyLife();
            ExplorationSession session = life.Session;
            Assert.That(session.Trial, Is.False);
            int survey = SurveyNode(session);
            Assert.That(EnemyPagesOf(session).Select(p => p.Id), Is.EqualTo(new[] { "enemy-none" }));
            string expected = life.Intel.SurveyTarget(1, session.State.Map);
            Assert.That(life.Intel.SurveyedEnemy(1), Is.Null, "nothing before the survey");
            StepTo(session, survey);
            Assert.That(session.Surveyed, Is.True);
            string target = life.Intel.SurveyedEnemy(1);
            Assert.That(target, Is.EqualTo(expected));
            Assert.That(life.Ex.DisclosureOf(target), Is.EqualTo(2), "the Expedition holds the raise");
            Assert.That(life.Intel.SurveyTarget(1, session.State.Map), Is.Not.EqualTo(target), "asked again the question would move on — it is asked once, at the node");
            Assert.That(life.Intel.SurveyedEnemy(1), Is.EqualTo(target), "the raised enemy stays the one the node chose");
            Assert.That(EnemyPagesOf(session).Select(p => (p.Id, p.Disclosure)), Is.EqualTo(new[] { ("enemy-" + target, 2) }));
            MapNode boss = session.State.Map.Nodes.First(n => n.Kind == NodeKind.Boss);
            foreach (bool flag in new[] { true, false })
            {
                NodeIntel bossIntel = life.Intel.ForNode(boss, 1, flag);
                Assert.That(bossIntel.EnemyName, Is.EqualTo(Enemies.ById(target).Name));
                Assert.That(bossIntel.Disclosure, Is.EqualTo(2), "the journal speaks, whatever the flag");
            }
            JournalPage dungeon = session.Screen().Journal.Pages.First(p => p.Tab == JournalTab.Dungeon);
            Assert.That(dungeon.Rows[dungeon.Rows.Count - 1].Text, Is.EqualTo("済ませた。この層の敵 1 種が開示度 2 になった"));

            // The battle gets it: the boss's HandIn carries 開示度 2, and it outlives the layer.
            StepTo(session, boss.Id);
            Assert.That(session.HasPendingBattle, Is.True);
            Assert.That(life.Ex.HandIn().Disclosure[target], Is.EqualTo(2), "the omens read the survey");
            session.Act(ScreenAction.WinBattle);
            Assert.That(session.Act(ScreenAction.GoToInterlude), Is.True);
            Assert.That(session.Act(ScreenAction.Descend), Is.True);
            Assert.That(session.State.Profile.Layer, Is.EqualTo(2));
            Assert.That(life.Ex.DisclosureOf(target), Is.EqualTo(2), "still 2 on the next layer");
            Assert.That(session.Screen().Journal.Pages.Any(p => p.Id == "enemy-" + target && p.Disclosure == 2), Is.True);
            Assert.That(life.Intel.SurveyedEnemy(2), Is.Null, "layer 2 has not been surveyed");

            // An enemy already at 2 is passed over; nothing left at all raises nothing.
            IntelLife known = SurveyLife(Levels(("shadow_hound", 2)));
            StepTo(known.Session, SurveyNode(known.Session));
            Assert.That(known.Intel.SurveyedEnemy(1), Is.Not.EqualTo("shadow_hound"));
            Assert.That(known.Ex.DisclosureOf(known.Intel.SurveyedEnemy(1)), Is.EqualTo(2));
            IntelLife full = SurveyLife(Enemies.All.ToDictionary(d => d.Id, d => 2));
            StepTo(full.Session, SurveyNode(full.Session));
            Assert.That(full.Intel.SurveyedEnemy(1), Is.Null);

            // With the trial rules on, the session asks the intel itself (写しの帳 absent) and the intel adds nothing.
            bool tried = false;
            for (ulong seed = 1; seed <= 200 && !tried; seed++)
            {
                IntelLife trial = LifeWith(seed, trial: true);
                Explorable(trial.Session);
                if (!trial.Session.State.Map.Nodes.Any(n => n.Kind == NodeKind.Survey)) continue;
                string want = trial.Intel.SurveyTarget(1, trial.Session.State.Map);
                StepTo(trial.Session, SurveyNode(trial.Session));
                Assert.That(trial.Session.SurveyTarget, Is.EqualTo(want));
                Assert.That(trial.Intel.SurveyedEnemy(1), Is.EqualTo(want));
                Assert.That(trial.Ex.JournalLevels.Select(p => (p.Key, p.Value)), Is.EqualTo(new[] { (want, 2) }), "exactly one enemy raised");
                tried = true;
            }
            Assert.That(tried, Is.True);

            // 写しの帳's pick waits: no enemy is the surveyed one yet (the JS stands a fake session in; here a trial life
            // carrying 写しの帳 stands on its survey node).
            RunLoadout book = Loadouts.ToRunLoadout(Loadout.Of(new[] { "utsushi_no_tobari" }, Array.Empty<string>(), 3), 3);
            bool waited = false;
            for (ulong seed = 1; seed <= 200 && !waited; seed++)
            {
                IntelLife choosing = LifeWith(seed, trial: true, loadout: book);
                Explorable(choosing.Session);
                if (!choosing.Session.State.Map.Nodes.Any(n => n.Kind == NodeKind.Survey)) continue;
                StepTo(choosing.Session, SurveyNode(choosing.Session));
                if (!choosing.Session.HasPendingSurvey) continue;
                Assert.That(choosing.Intel.SurveyedEnemy(1), Is.Null);
                waited = true;
            }
            Assert.That(waited, Is.True);
        }

        // ------------------------------------------------------------------ J-11

        // js: test/ext/journal.test.mjs:483
        [Test]
        public void J11_TheJournal_HoldsEveryEnemyItKnows_AnyLayer_InheritedToo_InRosterOrder()
        {
            Assert.That(new JournalIntel().Pages(1, false), Is.Empty, "before bind: none");
            JournalIntel intel = Fake(("miasma_priest", 1), ("polearm_warped", 2), ("distortion_root", 0), ("mist_archer", 5));
            IReadOnlyList<JournalPage> pages = intel.Pages(1, false);
            Assert.That(pages.Select(p => (p.Id, p.Disclosure)),
                Is.EqualTo(new[] { ("enemy-polearm_warped", 2), ("enemy-mist_archer", 2), ("enemy-miasma_priest", 1) }), "roster order, 開示度 held to 0..2");
            Assert.That(intel.Pages(1, true).Select(p => p.Id), Is.EqualTo(pages.Select(p => p.Id)), "the surveyed flag adds nothing: the survey is in the journal");

            // An inherited page (the lineage's journal) is a page from the first step of the life.
            ExplorationSession session = LifeWith(3, Levels(("abyss_angler", 1))).Session;
            List<JournalPage> enemyPages = EnemyPagesOf(session);
            Assert.That(enemyPages.Select(p => p.Id), Is.EqualTo(new[] { "enemy-abyss_angler" }));
            Assert.That(enemyPages[0].Title, Is.EqualTo("獄竜 ガルド"));
        }

        // ------------------------------------------------------------------ J-12

        // js: test/ext/journal.test.mjs:503
        [Test]
        public void J12_WithoutAJournalIntel_OrBeforeBind_TheScreenIsTheCSharps()
        {
            ExplorationSession plain = Explorable(new ExplorationSession(new RunStart { Seed = 5 }));
            List<NodeCard> lines = EnemyLines(plain.Screen());
            Assert.That(lines.Count, Is.GreaterThan(0));
            foreach (NodeCard n in lines) Assert.That(n.Lines, Does.Contain("敵: ？"));
            JournalPage page = plain.Screen().Journal.Pages[0];
            Assert.That(page.Id, Is.EqualTo("enemy-none"));
            Assert.That(page.Disclosure, Is.EqualTo(0));

            ExplorationSession unbound = Explorable(new ExplorationSession(new RunStart { Seed = 5 }, new JournalIntel()));
            Assert.That(Dump.Of(unbound.Screen()), Is.EqualTo(Dump.Of(plain.Screen())), "an unbound intel answers like NoEnemyIntel");

            // Without the intel the survey raises nothing (the C#): the journal stays as the life started.
            bool tried = false;
            for (ulong seed = 1; seed <= 200 && !tried; seed++)
            {
                var ex = new Expedition(new RunStart { Seed = seed });
                Explorable(ex.Session);
                if (!ex.Session.State.Map.Nodes.Any(n => n.Kind == NodeKind.Survey)) continue;
                StepTo(ex.Session, SurveyNode(ex.Session));
                Assert.That(ex.Session.Surveyed, Is.True);
                Assert.That(ex.JournalLevels, Is.Empty);
                tried = true;
            }
            Assert.That(tried, Is.True);
        }

        // ------------------------------------------------------------------ J-13

        /// <summary>The shipped wiring's start data (Life.MakeStart's shape) with this deck (js: journal.test.mjs:531).</summary>
        private static LifeStartData StartData(ulong seed, IEnumerable<DeckRow> deck)
        {
            var o = Json.Parse(
                "{\"v\":1,\"generation\":1,\"name\":\"試\",\"seed\":\"" + seed + "\",\"toolSlots\":3,\"deckMax\":40,\"survivorBonus\":false," +
                "\"traceLayers\":[],\"carvingKnown\":false,\"journal\":{\"Disclosure\":{}},\"choice\":{\"deck\":[],\"battleToolIds\":[]," +
                "\"loadout\":{\"tools\":[],\"consumables\":[],\"slots\":3},\"talent\":null,\"toolIds\":[],\"consumableIds\":[]}}");
            LifeStartData start = LifeStartData.FromJson(o);
            start.Choice.Deck = deck.ToList();
            return start;
        }

        /// <summary>One action of a life, fights played for real through the recorder; false once the life is over (js: journal.test.mjs:540).</summary>
        private static bool WalkStep(RecordedExpedition ex, Func<IReadOnlyList<MapNode>, MapNode> pick)
        {
            RecordedSession s = ex.Session;
            ExplorationState st = s.State;
            if (s.HasPendingBattle)
            {
                RecordedBattle battle = ex.Battle ?? ex.StartEncounter();
                while (!battle.Over)
                {
                    RecordedSource src = battle.StartNext(true);
                    LifeTestKit.Fight(src);
                    battle.Finish(src);
                }
                ex.Return();
                if (ex.Offer.Count > 0) ex.Learn(ex.Offer[0]);
                return true;
            }
            if (s.HasPendingStep) { s.Act(ScreenAction.ConfirmStep); return true; }
            if (s.ConfirmingDescend) { s.Act(ScreenAction.ConfirmDescend); return true; }
            // Life.LifeSetup plays the game in trial mode: answer its choices too
            if (s.HasPendingEvent) { s.Act(ScreenAction.AcceptEvent); return true; }
            if (s.HasPendingFound) { if (!s.Act(ScreenAction.TakeFound, "0")) s.Act(ScreenAction.LeaveFound); return true; }
            if (s.HasPendingSurvey) { s.Act(ScreenAction.ChooseSurvey, s.SurveyChoices[0]); return true; }
            if (st.IsOver) return false;
            if (st.Phase == RunPhase.Interlude) { s.Act(ScreenAction.Descend); return true; }
            if (ExplorationReducer.CanInterlude(st)) { s.Act(ScreenAction.GoToInterlude); return true; }
            List<MapNode> open = st.Map.Nodes.Where(n => ExplorationReducer.CanStep(st, n.Id)).ToList();
            List<MapNode> fresh = open.Where(n => !st.HasResolved(n.Id)).ToList();
            MapNode node = pick(fresh.Count > 0 ? fresh : open);
            Assert.That(s.Step(node.Id), Is.True, "step refused");
            return true;
        }

        private static string StateSnap(Expedition ex) => Dump.Of(ex.Session.State) + "|" + Dump.Of(ex.JournalLevels);

        private static string PagesOf(Expedition ex) =>
            string.Join(",", EnemyPagesOf(ex.Session).Select(p => p.Id + ":" + p.Disclosure));

        // js: test/ext/journal.test.mjs:566
        [Test]
        public void J13_TheScreensAnswersOnlyRead_TheRaisesHappenInTheLoggedArrival_SoAReplayDrawsTheSameScreens()
        {
            // Asking changes nothing, and the same life gives the same answers.
            Dictionary<string, int> journal = Levels(("polearm_warped", 1), ("shadow_hound", 2), ("crossbow_hunter", 1));
            IntelLife a = LifeWith(11, journal);
            IntelLife b = LifeWith(11, journal);
            Explorable(a.Session);
            Explorable(b.Session);
            string before = Dump.Of(a.Session.State);
            string screenA = Dump.Of(a.Session.Screen());
            Assert.That(Dump.Of(b.Session.Screen()), Is.EqualTo(screenA));
            Assert.That(Dump.Of(a.Session.Screen()), Is.EqualTo(screenA), "asking twice changes nothing");
            Assert.That(Dump.Of(a.Session.State), Is.EqualTo(before));
            foreach (MapNode node in a.Session.State.Map.Nodes)
            {
                Assert.That(Dump.Of(a.Intel.ForNode(node, 1, true)), Is.EqualTo(Dump.Of(b.Intel.ForNode(node, 1, true))));
                a.Intel.SurveyTarget(1, a.Session.State.Map);
            }
            Assert.That(a.Ex.JournalLevels.Select(p => (p.Key, p.Value)), Is.EqualTo(journal.Select(p => (p.Key, p.Value))), "the journal itself is untouched by reading");

            // The review's case: the survey, then a trace on the same layer gives the target's page at 2 (the router's
            // take → Expedition.Inherit, logged). Reloaded (LifeSetup + replay), the same enemy is the raised one and no
            // second enemy reads 2.
            List<DeckRow> deck = PrototypeDeck.Build().Select(c => new DeckRow(c.InstanceId, c.Def.Id)).ToList();
            int traced = 0;
            for (ulong seed = 1; seed <= 40 && traced < 3; seed++)
            {
                LifeStartData start = StartData(seed, deck);
                Expedition raw = Life.LifeSetup(Clone(start)).Expedition;
                MapNode survey = raw.Session.State.Map.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Survey);
                if (survey == null) continue;
                RecordedExpedition rec = Recorder.RecordExpedition(raw);
                rec.Session.Act(ScreenAction.WinBattle);
                foreach (int id in RouteTo(raw.Session.State.Map, raw.Session.State.CurrentNodeId, survey.Id))
                {
                    rec.Session.Step(id);
                    if (raw.Session.HasPendingBattle) rec.Session.Act(ScreenAction.WinBattle);
                }
                if (!raw.Session.Surveyed || raw.Session.State.IsOver) continue;
                string target = ((JournalIntel)raw.Session.Intel).SurveyedEnemy(1);
                Assert.That(raw.DisclosureOf(target), Is.EqualTo(2));
                rec.Inherit(new TakenLegacy("t", Array.Empty<TakenCard>(),
                    new InheritanceCore.Journal(new[] { new KeyValuePair<string, int>(target, 2) }), 0));
                Expedition again = Life.LifeSetup(Clone(start)).Expedition;
                Recorder.Replay(again, Saved(rec.Log));
                Assert.That(((JournalIntel)again.Session.Intel).SurveyedEnemy(1), Is.EqualTo(target), $"seed {seed}");
                Assert.That(StateSnap(again), Is.EqualTo(StateSnap(raw)));
                Assert.That(PagesOf(again), Is.EqualTo(PagesOf(raw)), $"seed {seed}: the same pages after the reload");
                Assert.That(Dump.Of(again.Session.Screen()), Is.EqualTo(Dump.Of(raw.Session.Screen())));
                traced++;
            }
            Assert.That(traced, Is.GreaterThanOrEqualTo(3), $"traced {traced}");

            // Save and reload after every action of real lives whose deck holds 観察 (the seeds where the screen used to move).
            List<DeckRow> rows = deck.Take(deck.Count - 3)
                .Concat(new[] { new DeckRow("obs-1", "observe"), new DeckRow("obs-2", "observe"), new DeckRow("obs-3", "observe") }).ToList();
            int saves = 0;
            foreach (ulong seed in new ulong[] { 78, 90, 77, 83, 84, 88, 94 })
            {
                LifeStartData start = StartData(seed, rows);
                Expedition raw = Life.LifeSetup(Clone(start)).Expedition;
                RecordedExpedition rec = Recorder.RecordExpedition(raw);
                for (int guard = 0; guard < 400; guard++)
                {
                    bool more = WalkStep(rec, open => open.FirstOrDefault(n => n.Kind == NodeKind.Survey) ?? open.FirstOrDefault(n => n.Kind == NodeKind.Elite) ?? open[0]);
                    Expedition again = Life.LifeSetup(Clone(start)).Expedition;
                    Recorder.Replay(again, Saved(rec.Log));
                    Assert.That(StateSnap(again), Is.EqualTo(StateSnap(raw)), $"seed {seed} action {guard}: state");
                    Assert.That(Dump.Of(again.Session.Screen()), Is.EqualTo(Dump.Of(raw.Session.Screen())), $"seed {seed} action {guard}: screen");
                    saves++;
                    if (!more) break;
                }
            }
            Assert.That(saves, Is.GreaterThanOrEqualTo(60), $"saves {saves}");
        }

        // ------------------------------------------------------------------ J-14

        // js: test/ext/journal.test.mjs:656
        [Test]
        public void J14_TheGameTurnsItOn_LifeSetupBindsAJournalIntel_AndAFoughtEnemyIsNamedOnTheMap()
        {
            var proto = new Expedition(new RunStart { Seed = 1 });
            LifeStartData start = StartData(42, proto.Deck.Select(c => new DeckRow(c.InstanceId, c.Def.Id)));
            Expedition expedition = Life.LifeSetup(start).Expedition;
            ExplorationSession session = expedition.Session;
            Assert.That(session.Intel, Is.InstanceOf<JournalIntel>());

            MapNode entry = session.State.Map.Node(session.State.CurrentNodeId);
            string first = session.PendingEncounter.EnemyIds.First();
            Assert.That(session.Screen().Node(entry.Id).Lines, Does.Contain("敵: ？"), "not met yet");
            EncounterBattle battle = expedition.StartEncounter();
            while (!battle.Over)
            {
                var source = battle.StartNext(false);
                LifeTestKit.Fight(source);
                battle.Finish(source);
            }
            expedition.Return();
            Assert.That(expedition.DisclosureOf(first), Is.GreaterThanOrEqualTo(1));
            IReadOnlyList<string> lines = session.Screen().Node(entry.Id).Lines;
            Assert.That(lines.Any(l => l.StartsWith("敵: " + Enemies.ById(first).Name + "（開示度 ", StringComparison.Ordinal)), Is.True, string.Join(" | ", lines));
            Assert.That(session.Screen().Journal.Pages.Any(p => p.Id == "enemy-" + first), Is.True, "its page is in the journal");
        }

        // ------------------------------------------------------------------ J-15

        // js: test/ext/journal.test.mjs:704
        [Test]
        public void J15_SevenLayers_TheRootsPage_IsAt2FromTheMomentTheLifeReachesTheLastLayer_BattleIncluded()
        {
            Assert.That(JournalIntel.KnownOnLastLayer, Is.EqualTo(new[] { "distortion_root" }));
            foreach (bool trial in new[] { false, true })
            {
                IntelLife life = LifeWith(9, trial: trial);
                ExplorationSession session = life.Session;
                DownTo(session, 7, layer => Assert.That(life.Ex.DisclosureOf("distortion_root"), Is.EqualTo(0), $"trial {trial}: not before 七層 (layer {layer})"));
                Assert.That(session.State.Profile.IsLast, Is.True);
                Assert.That(life.Ex.DisclosureOf("distortion_root"), Is.EqualTo(2), $"trial {trial}: the journal holds it on 七層's entry");
                Explorable(session);
                JournalPage page = session.Screen().Journal.Pages.First(p => p.Id == "enemy-distortion_root");
                Assert.That(page.Disclosure, Is.EqualTo(2));
                Assert.That(page.DisclosureLabel, Is.EqualTo("開示度 2 / 2"));
                MapNode boss = session.State.Map.Nodes.First(n => n.Kind == NodeKind.Boss);
                NodeIntel bossIntel = life.Intel.ForNode(boss, 7, false);
                Assert.That(bossIntel.EnemyName, Is.EqualTo("歪みの根"));
                Assert.That(bossIntel.Disclosure, Is.EqualTo(2));
                StepTo(session, boss.Id);
                Assert.That(session.HasPendingBattle, Is.True);
                Assert.That(life.Ex.HandIn().Disclosure.Select(p => (p.Key, p.Value)), Is.EqualTo(new[] { ("distortion_root", 2) }), "the omens read it");
            }
            // Without the intel (the C#), 七層 changes nothing.
            var plain = new Expedition(new RunStart { Seed = 9 });
            DownTo(plain.Session, 7);
            Assert.That(plain.Session.State.Profile.Layer, Is.EqualTo(7));
            Assert.That(plain.DisclosureOf("distortion_root"), Is.EqualTo(0));
        }
    }
}
