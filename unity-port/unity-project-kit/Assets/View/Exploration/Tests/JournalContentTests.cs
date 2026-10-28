// #107: the journal's real pages. The enemy pages are the roster's 手記 cut by 開示度, the dungeon pages
// hold the events of encounters_and_rewards_v4.md §5, the memo keeps free text it never reads, and every
// filled page fits on one line for the legacy list (dungeon_exploration_v4.md §6.11). Unity's NUnit 3.5
// runs these as well: no Assert.Multiple, no Has.Count.
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;
using Journal;
using NUnit.Framework;

namespace Exploration.Tests
{
    public class JournalContentTests
    {
        // ---- enemy pages ----

        [Test]
        public void EveryEnemyTheLayersCanPutOnANode_HasAPage()
        {
            var ids = LayerEncounters.All
                .SelectMany(t => t.Normal.Concat(t.Elite).Concat(t.Boss))
                .SelectMany(e => e.EnemyIds)
                .Distinct()
                .ToList();

            foreach (string id in ids) Assert.That(EnemyJournal.Find(id), Is.Not.Null, id);
            Assert.That(EnemyJournal.All.Select(e => e.Id), Is.Unique);
            Assert.That(EnemyJournal.All.Count, Is.EqualTo(14), "the roster's thirteen of §2〜§6 and 竜神 of §7");
        }

        [Test]
        public void AnEncounterOpensTheRanges_AndObservationTheWeakness()
        {
            JournalPage unread = EnemyJournal.Page("polearm_warped", 0);
            JournalPage met = EnemyJournal.Page("polearm_warped", 1);
            JournalPage read = EnemyJournal.Page("polearm_warped", 2);

            Assert.That(unread.Rows.Select(unread.TextOf).Distinct(), Is.EqualTo(new[] { JournalRow.Hidden }));
            Assert.That(met.Rows.Take(3).Select(met.TextOf), Has.No.Member(JournalRow.Hidden));
            Assert.That(met.Rows.Skip(3).Select(met.TextOf).Distinct(), Is.EqualTo(new[] { JournalRow.Hidden }));
            Assert.That(read.Rows.Select(read.TextOf), Has.No.Member(JournalRow.Hidden));
            Assert.That(read.Title, Is.EqualTo("錆槍の竜兵"));
            Assert.That(read.Id, Is.EqualTo("enemy-polearm_warped"));
            Assert.That(read.DisclosureLabel, Is.EqualTo("開示度 2 / 2"));
        }

        [Test]
        public void ThePageCarriesTheRostersSentences_Unchanged()
        {
            JournalPage page = EnemyJournal.Page("shadow_hound", 2);

            Assert.That(page.Rows.Select(r => r.Label), Is.EqualTo(new[] { "間合い 0", "間合い 1〜2", "間合い 3 以上", "得意な間合い", "弱点" }));
            Assert.That(page.TextOf(page.Rows[0]), Is.EqualTo("噛みつき。出血が 2 スタック残る"));
            Assert.That(page.TextOf(page.Rows[3]), Is.EqualTo("0"));
            Assert.That(page.TextOf(page.Rows[4]), Does.StartWith("HP 80 で、通常の敵では低め。"));
        }

        [Test]
        public void ABossPage_AddsItsAdaptationConditions_AtTwo()
        {
            // roster §1.5: 開示度 2 writes each adaptation's condition on the page, one row per row of the
            // tables of §4.4 / §5.4 / §6.4 / §7.11.
            var expected = new Dictionary<string, int> { ["miasma_priest"] = 4, ["abyss_angler"] = 4, ["distortion_root"] = 7, ["dragon_god"] = 4 };
            foreach (EnemyJournalEntry entry in EnemyJournal.All)
            {
                int count = expected.TryGetValue(entry.Id, out int n) ? n : 0;
                Assert.That(entry.Adaptations.Count, Is.EqualTo(count), entry.Id);
                Assert.That(entry.Adaptations, Has.No.Member(""), entry.Id);
            }

            JournalPage met = EnemyJournal.Page("miasma_priest", 1);
            JournalPage read = EnemyJournal.Page("miasma_priest", 2);
            var adaptations = read.Rows.Skip(5).ToList();
            Assert.That(adaptations.Select(r => r.Label), Is.EqualTo(new[] { "適応 1", "適応 2", "適応 3", "適応 4" }));
            Assert.That(read.TextOf(adaptations[0]), Is.EqualTo("アタックを 3 回続けて受けると、とぐろを締めて帳を張り直す"));
            Assert.That(read.TextOf(adaptations[1]), Is.EqualTo("Guard を 9 以上で 2 ターン受け続けると、崩しを狙う"));
            Assert.That(met.Rows.Skip(5).Select(met.TextOf).Distinct(), Is.EqualTo(new[] { JournalRow.Hidden }), "the conditions wait for 2");
            Assert.That(EnemyJournal.Page("polearm_warped", 2).Rows.Select(r => r.Label), Has.No.Member("適応 1"));
        }

        [Test]
        public void TheTendencyLine_WaitsForTwo()
        {
            Assert.That(EnemyJournal.Tendency("crossbow_hunter", 1), Is.Empty);
            Assert.That(EnemyJournal.Tendency("crossbow_hunter", 2), Is.EqualTo("傾向: 灰弩の竜兵は得意な間合い 2〜3"));
            Assert.That(EnemyJournal.Tendency("no_such_enemy", 2), Is.Empty);
        }

        // ---- dungeon pages ----

        [Test]
        public void TheEvents_SitOnTheLayersTheTablePutsThemOn()
        {
            Assert.That(DungeonJournal.OfLayer(1).Select(e => e.Name), Is.EqualTo(new[] { "泉", "重い宝箱", "特殊な食事" }));
            Assert.That(DungeonJournal.OfLayer(3).Select(e => e.Name), Is.EqualTo(new[] { "泉", "重い宝箱", "特殊な食事" }));
            Assert.That(DungeonJournal.OfLayer(7).Select(e => e.Name), Is.EqualTo(new[] { "重い宝箱", "特殊な食事" }), "no 泉 on layer 7");
            foreach (int layer in new[] { 4, 5, 6 }) Assert.That(DungeonJournal.OfLayer(layer), Is.Empty, $"layer {layer} holds no event");
            Assert.That(DungeonJournal.All.Select(e => e.Id), Is.Unique);
            Assert.That(DungeonJournal.All.Where(e => e.Kind != DungeonEntryKind.Event), Is.Empty, "近道 and 条件出現 wait for content (§2.2)");
            Assert.That(DungeonJournal.Event(2, "spring").Met, Is.EqualTo("HP が最大の 25% 戻る"));
        }

        [Test]
        public void AnEntryReadsHidden_ThenWhatHappened_ThenHowOften()
        {
            DungeonEntry spring = DungeonJournal.Event(1, "spring");

            Assert.That(DungeonJournal.Text(spring, 0), Is.EqualTo(JournalRow.Hidden));
            Assert.That(DungeonJournal.Text(spring, 1), Is.EqualTo("HP が最大の 20% 戻る"));
            Assert.That(DungeonJournal.Text(spring, 2), Does.EndWith("1 つ目が泉になる見込みは 50%。2 つ目は 1 つ目と違う種類になる"));

            var rows = DungeonJournal.Rows(1, id => id == spring.Id ? 1 : 0);
            Assert.That(rows.Select(r => r.Label), Is.EqualTo(new[] { "【イベント】", "泉", "重い宝箱", "特殊な食事" }));
            Assert.That(rows.Select(r => r.Text), Is.EqualTo(new[] { "", "HP が最大の 20% 戻る", JournalRow.Hidden, JournalRow.Hidden }));
            Assert.That(DungeonJournal.Rows(5, _ => 2), Is.Empty);
        }

        // ---- memo ----

        [Test]
        public void TheMemo_KeepsWhatIsWritten_AndReadsNothingOutOfIt()
        {
            var memo = new JournalMemo();
            Assert.That(memo.Page().Body, Is.EqualTo(JournalMemo.Empty));

            const string text = "錆槍の竜兵 開示度 2\n弱点: 押し込みの前に下がる";
            string first = memo.Write(text);
            string second = memo.Write("");
            Assert.That(memo.Notes.Select(n => n.Text), Is.EqualTo(new[] { text, "" }));
            Assert.That(memo.Page().Rows[0].Text, Is.EqualTo(text));
            Assert.That(memo.Page().Rows.Select(r => r.Label), Is.EqualTo(new[] { "メモ 1", "メモ 2" }));

            Assert.That(memo.Rewrite(second, "二層の泉は右"), Is.True);
            Assert.That(memo.Erase(first), Is.True);
            Assert.That(memo.Erase(first), Is.False);
            Assert.That(memo.Rewrite("memo-9", "x"), Is.False);
            Assert.That(memo.Notes.Single().Title, Is.EqualTo("メモ 2"), "a note keeps its number");
            Assert.That(memo.Write("次"), Is.Not.EqualTo(first), "an erased note's id is not given again");
        }

        // ---- one line per page ----

        [Test]
        public void EachKindOfPage_FitsOnOneLineWithTheSameThreeFields()
        {
            Assert.That(JournalLines.Enemy("polearm_warped", 0), Is.Null, "a page at 0 is no page yet");
            Assert.That(JournalLines.Enemy("polearm_warped", 1).Text, Is.EqualTo("敵｜錆槍の竜兵｜開示度 1 / 2（間合いごとの動きまで）"));
            Assert.That(JournalLines.Enemy("miasma_priest", 2).Text, Is.EqualTo("敵｜大黒蛇 セルク｜開示度 2 / 2（弱点まで）"));

            string spring = DungeonJournal.EventId(1, "spring");
            string meal = DungeonJournal.EventId(1, "strange_meal");
            Assert.That(JournalLines.Dungeon(1, _ => 0), Is.Null);
            Assert.That(JournalLines.Dungeon(1, id => id == spring ? 2 : id == meal ? 1 : 0).Text,
                Is.EqualTo("ダンジョン｜第 1 層｜分かったこと 2 件（うち中身まで 1 件）"));

            var memo = new JournalMemo();
            memo.Write("一二三四五六七八九十一二三四五六七八九十一二三\n二行目");
            memo.Write("  ");
            Assert.That(JournalLines.Memo(memo.Notes[0]).Text, Is.EqualTo("メモ｜メモ 1｜一二三四五六七八九十一二三四五六七八九十…"));
            Assert.That(JournalLines.Memo(memo.Notes[1]), Is.Null, "a blank note is not a filled page");
        }

        [Test]
        public void TheCandidates_ListEveryFilledPage_InTheDrawersOrder_WithoutTheTerms()
        {
            var memo = new JournalMemo();
            memo.Write("ガルドは同じ間合いで 2 ターン終えない");
            var enemies = new Dictionary<string, int> { ["abyss_angler"] = 2, ["polearm_warped"] = 1 };
            string chest = DungeonJournal.EventId(7, "heavy_chest");

            var lines = JournalLines.Candidates(id => enemies.TryGetValue(id, out int l) ? l : 0, id => id == chest ? 1 : 0, memo);

            Assert.That(lines.Select(l => l.PageId), Is.EqualTo(new[] { "enemy-polearm_warped", "enemy-abyss_angler", "dungeon-7", "memo-1" }));
            Assert.That(lines.Select(l => l.Tab), Has.No.Member(JournalTab.Terms));
        }

        // ---- the life's journal on a session ----

        [Test]
        public void ANodesEnemy_IsNamedFromOne_AndGetsItsTendencyAtTwo()
        {
            var levels = new Dictionary<string, int>();
            var journal = new LifeJournal(id => levels.TryGetValue(id, out int l) ? l : 0);
            var session = new ExplorationSession(new RunStart { Seed = 3 }, journal);
            journal.Follow(session);
            MapNode entry = session.State.Map.Node(session.State.CurrentNodeId);
            string enemy = session.PendingEncounter.EnemyIds.Single();

            Assert.That(journal.ForNode(entry, 1, false), Is.Null, "nothing known: the screen prints 「？」");

            levels[enemy] = 1;
            NodeIntel met = journal.ForNode(entry, 1, false);
            Assert.That(met.EnemyName, Is.EqualTo(EnemyJournal.Of(enemy).Name));
            Assert.That(met.Disclosure, Is.EqualTo(1));
            Assert.That(met.Lines, Is.Empty);

            levels[enemy] = 2;
            Assert.That(journal.ForNode(entry, 1, false).Lines, Is.EqualTo(new[] { EnemyJournal.Tendency(enemy, 2) }));
            Assert.That(session.Screen().Journal.Pages.First().Id, Is.EqualTo(EnemyJournal.PageId(enemy)));
        }

        [Test]
        public void ATwoEnemyNode_JoinsTheNames_AndShowsTheLowestLevel()
        {
            var levels = new Dictionary<string, int> { ["polearm_warped"] = 1 };
            var journal = new LifeJournal(id => levels.TryGetValue(id, out int l) ? l : 0);
            var session = new ExplorationSession(new RunStart { Seed = 3 }, journal);
            journal.Follow(session);
            MapNode elite = session.State.Map.Nodes.First(n => n.Kind == NodeKind.Elite);

            // Layer 1's elite slot is the one chain 錆槍 → 瘴牙 (encounters_and_rewards_v4.md §2).
            NodeIntel chain = journal.ForNode(elite, 1, false);
            Assert.That(chain.EnemyName, Is.EqualTo("錆槍の竜兵 → ？"));
            Assert.That(chain.Disclosure, Is.Zero);

            // Layer 2's elite slot is the one pair 錆槍 + 灰弩, so any elite node draws it.
            levels["crossbow_hunter"] = 2;
            NodeIntel pair = journal.ForNode(elite, 2, false);
            Assert.That(pair.EnemyName, Is.EqualTo("錆槍の竜兵 + 灰弩の竜兵"));
            Assert.That(pair.Disclosure, Is.EqualTo(1));
            Assert.That(pair.Lines, Is.EqualTo(new[] { EnemyJournal.Tendency("crossbow_hunter", 2) }));
        }

        [Test]
        public void TheNodePreview_NamesTheEnemyTheStepWillMeet()
        {
            var journal = new LifeJournal(_ => 1);
            var session = new ExplorationSession(new RunStart { Seed = 5 }, journal);
            journal.Follow(session);
            Assert.That(session.Act(ScreenAction.WinBattle), Is.True);

            int target = Route(session.State, NodeKind.Battle).Last();
            foreach (int node in Route(session.State, NodeKind.Battle))
            {
                string foretold = node == target ? journal.ForNode(session.State.Map.Node(node), 1, false).EnemyName : null;
                Assert.That(session.Step(node), Is.True);
                if (node != target)
                {
                    if (session.HasPendingBattle) Assert.That(session.Act(ScreenAction.WinBattle), Is.True);
                    continue;
                }
                Assert.That(foretold, Is.EqualTo(EnemyJournal.Of(session.PendingEncounter.EnemyIds.Single()).Name));
            }
        }

        [Test]
        public void ASurvey_ReadsTheLayersBossAtTwo_ForTheRestOfTheLife()
        {
            var journal = new LifeJournal();
            var session = new ExplorationSession(new RunStart { Seed = 3 }, journal);
            journal.Follow(session);
            string boss = LifeJournal.SurveyTarget(1);
            Assert.That(boss, Is.EqualTo("shadow_hound"), "layer 1's boss slot (encounters_and_rewards_v4.md §2)");
            Assert.That(journal.DisclosureOf(boss), Is.Zero);
            Assert.That(journal.DisclosureOf("distortion_root"), Is.Zero, "layer 7 has no survey, and its boss starts at 0 like the rest (§6.11)");

            WalkTo(session, NodeKind.Survey);
            Assert.That(session.HasSurveyed(1), Is.True);
            Assert.That(journal.DisclosureOf(boss), Is.EqualTo(2));
            Assert.That(journal.LegacyLines().Select(l => l.PageId), Has.Member(EnemyJournal.PageId(boss)));

            WalkTo(session, NodeKind.Boss);
            Assert.That(session.Act(ScreenAction.GoToInterlude), Is.True);
            Assert.That(session.Act(ScreenAction.Descend), Is.True);
            Assert.That(session.Surveyed, Is.False, "the new layer has its own survey");
            Assert.That(session.HasSurveyed(1), Is.True);
            Assert.That(journal.DisclosureOf(boss), Is.EqualTo(2), "the survey's 2 lasts the life");

            Assert.That(session.Act(ScreenAction.WinBattle), Is.True, "the new layer's entry battle");
            Assert.That(session.Act(ScreenAction.AskEndLife), Is.True);
            Assert.That(session.Act(ScreenAction.ConfirmEndLife), Is.True);
            Assert.That(session.Act(ScreenAction.NewLife), Is.True);
            Assert.That(session.HasSurveyed(1), Is.False);
            Assert.That(journal.DisclosureOf(boss), Is.Zero);
        }

        [Test]
        public void TheDrawer_ShowsTheLayersEntries_AndTheMemo()
        {
            var journal = new LifeJournal();
            var session = new ExplorationSession(new RunStart { Seed = 3 }, journal);
            journal.Follow(session);
            Assert.That(journal.Meet(DungeonJournal.EventId(1, "spring")), Is.True);
            Assert.That(journal.Meet("no-such-entry"), Is.False);
            journal.Memo.Write("泉は右の列");

            var pages = session.Screen().Journal.Pages;
            JournalPage dungeon = pages.Single(p => p.Tab == JournalTab.Dungeon);
            JournalPage memo = pages.Single(p => p.Tab == JournalTab.Memo);

            Assert.That(dungeon.Rows.Select(r => r.Label), Has.Member("【イベント】"));
            Assert.That(dungeon.Body, Does.Contain("泉\nHP が最大の 20% 戻る"));
            Assert.That(dungeon.Body, Does.Contain("重い宝箱\n" + JournalRow.Hidden));
            Assert.That(memo.Body, Is.EqualTo("メモ 1\n泉は右の列"));
        }

        [Test]
        public void ARarePage_TellsOneEntryOfItsLayer_UntilThePileIsEmpty()
        {
            var journal = new LifeJournal();
            var rng = new SeededRng(11);
            var told = new List<string>();
            for (int i = 0; i < 3; i++) told.Add(journal.DrawRarePage(1, rng));

            Assert.That(told, Is.Unique);
            Assert.That(told.Select(id => journal.EntryLevel(id)).Distinct(), Is.EqualTo(new[] { 2 }));
            Assert.That(journal.DrawRarePage(1, rng), Is.Null, "the pile is empty: the reward turns into a card");
            Assert.That(journal.DrawRarePage(4, rng), Is.Null, "layer 4 holds no entry");
            Assert.That(journal.DungeonPages().Single().Disclosure, Is.EqualTo(2));
        }

        [Test]
        public void ANewLife_StartsTheEntriesAndTheMemoEmpty()
        {
            var journal = new LifeJournal();
            var session = new ExplorationSession(new RunStart { Seed = 3 }, journal);
            journal.Follow(session);
            journal.Read(DungeonJournal.EventId(2, "heavy_chest"));
            journal.Memo.Write("前の生のメモ");
            Assert.That(journal.LegacyLines().Select(l => l.Tab), Is.EqualTo(new[] { JournalTab.Dungeon, JournalTab.Memo }));

            Assert.That(session.Act(ScreenAction.WinBattle), Is.True, "the entry battle");
            Assert.That(session.Act(ScreenAction.AskEndLife), Is.True);
            Assert.That(session.Act(ScreenAction.ConfirmEndLife), Is.True);
            Assert.That(session.Act(ScreenAction.NewLife), Is.True);

            Assert.That(journal.LegacyLines(), Is.Empty);
            Assert.That(journal.MemoPage().Body, Is.EqualTo(JournalMemo.Empty));
        }

        // ---- helpers ----

        /// <summary>Walks to the nearest unresolved node of this kind, winning every battle on the way.</summary>
        private static void WalkTo(ExplorationSession session, NodeKind kind)
        {
            if (session.HasPendingBattle) Assert.That(session.Act(ScreenAction.WinBattle), Is.True);
            foreach (int node in Route(session.State, kind))
            {
                Assert.That(session.Step(node), Is.True, $"step to {node}");
                if (session.HasPendingStep) Assert.That(session.Act(ScreenAction.ConfirmStep), Is.True);
                if (session.HasPendingBattle) Assert.That(session.Act(ScreenAction.WinBattle), Is.True);
            }
        }

        /// <summary>Breadth-first: the nodes from where the player stands to the nearest unresolved node of this kind, that node last.</summary>
        private static IReadOnlyList<int> Route(ExplorationState state, NodeKind kind)
        {
            LayerMap map = state.Map;
            var from = new Dictionary<int, int> { [state.CurrentNodeId] = -1 };
            var queue = new Queue<int>();
            queue.Enqueue(state.CurrentNodeId);
            int target = -1;
            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                if (map.Node(node).Kind == kind && !state.HasResolved(node))
                {
                    target = node;
                    break;
                }
                foreach (int next in map.Neighbours(node))
                {
                    if (from.ContainsKey(next)) continue;
                    from[next] = node;
                    queue.Enqueue(next);
                }
            }
            Assert.That(target, Is.Not.EqualTo(-1), $"a {kind} node is reachable");
            var path = new List<int>();
            for (int node = target; node != state.CurrentNodeId; node = from[node]) path.Add(node);
            path.Reverse();
            return path;
        }
    }
}
