using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace BattleCore.Tests
{
    /// <summary>
    /// #51: the bosses' adaptations and stages (enemy_roster_v4 §4.4 / §5.4 / §6.1 / §6.4), put into
    /// the hook #50 built (<see cref="TreeSwitch"/>, battle_core_v4 §6.3). The conditions are read off
    /// an <see cref="AdaptationView"/> built by hand, so each test says which history it is about;
    /// a few run through the turn loop to show the moves, the cleanse and the overrides in play.
    /// </summary>
    public class BossSwitchTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private const int Cells = 8;

        // ---- Building a history ----

        private static readonly BattleAttribute A = BattleAttribute.Attack;
        private static readonly BattleAttribute G = BattleAttribute.Guard;
        private static readonly BattleAttribute St = BattleAttribute.Stance;

        /// <summary>One finished player turn: the cards it played (folded), and how it ended.</summary>
        private sealed record Turn(BattleAttribute[] Cards, int Guard = 0, int Gap = 3, bool Moved = false, int Cell = 2);

        private static BattleHistory History(params Turn[] turns)
        {
            var history = new BattleHistory();
            foreach (var turn in turns)
            {
                foreach (var card in turn.Cards) history = history.WithCard(card);
                history = history.WithTurnEnd(new PlayerTurnEnd(turn.Guard, turn.Gap, turn.Moved, turn.Cell, turn.Cards.Length));
            }
            return history;
        }

        private static AdaptationView View(EnemyDef boss, BattleHistory history, int? hp = null, int gap = 3, int? turn = null, params string[] held) =>
            new AdaptationView(turn ?? history.TurnEnds.Count, hp ?? boss.MaxHp, boss.MaxHp, Array.Empty<string>(), history, gap, held);

        private static string? Active(EnemyDef boss, AdaptationView view) => EnemyAi.ActiveSwitch(boss, view)?.Id;

        private static TreeSwitch Switch(EnemyDef boss, string id) => boss.Switches!.Single(s => s.Id == id);

        private static readonly BattleAttribute[] None = Array.Empty<BattleAttribute>();

        // ---- 大黒蛇 セルク (§4.4) ----

        [Test]
        public void Priest_Turtle_AfterThreeAttacksInARow_CoilsThenWards_ForOnePhase()
        {
            var priest = Enemies.MiasmaPriest;
            var three = History(new Turn(new[] { A, A }), new Turn(new[] { A }));
            var after = History(new Turn(new[] { A, A }), new Turn(new[] { A }), new Turn(None));
            var two = History(new Turn(new[] { A, G, A }), new Turn(new[] { A }));

            Assert.Multiple(() =>
            {
                Assert.That(Active(priest, View(priest, three)), Is.EqualTo("turtle"), "the third attack in a row came in the last turn");
                Assert.That(Switch(priest, "turtle").BranchAtGapZero, Is.EqualTo(new[] { "coil", "ward", "miasma_bolt" }));
                Assert.That(Switch(priest, "turtle").BranchAtGapThreePlus, Is.EqualTo(new[] { "coil", "ward", "miasma_bolt" }));
                Assert.That(Active(priest, View(priest, after)), Is.Null, "1 フェーズで戻る");
                Assert.That(Active(priest, View(priest, two)), Is.Null, "a guard broke the run");
            });
        }

        [Test]
        public void Priest_Breaker_AfterTwoTurnsOnGuardNine_PutsTheBreakingStaffOnTop_UntilGuardFive()
        {
            var priest = Enemies.MiasmaPriest;
            var on = History(new Turn(None, Guard: 9), new Turn(None, Guard: 12));
            var kept = History(new Turn(None, Guard: 9), new Turn(None, Guard: 12), new Turn(None, Guard: 6));
            var off = History(new Turn(None, Guard: 9), new Turn(None, Guard: 12), new Turn(None, Guard: 5));
            var breaker = Switch(priest, "breaker");

            Assert.Multiple(() =>
            {
                Assert.That(Active(priest, View(priest, on)), Is.EqualTo("breaker"));
                Assert.That(Active(priest, View(priest, kept)), Is.EqualTo("breaker"), "6 is not yet 5 or less");
                Assert.That(Active(priest, View(priest, off)), Is.Null);
                Assert.That(breaker.BranchAtGapOneToTwo![0], Is.EqualTo("staff_strike"), "決定木の先頭へ出す");
                Assert.That(EnemyAi.ActionOf(priest, "staff_strike", breaker).Face.Break, Is.EqualTo(2), "崩しを 2 付与する");
                Assert.That(EnemyAi.ActionOf(priest, "staff_strike").Face.Break, Is.EqualTo(0), "the base staff does not break");
            });
        }

        [Test]
        public void Priest_Pusher_AfterTwoTurnsAdjacent_PushesBack_UntilTheGapOpens()
        {
            var priest = Enemies.MiasmaPriest;
            var close = History(new Turn(None, Gap: 0), new Turn(None, Gap: 0));
            Assert.Multiple(() =>
            {
                Assert.That(Active(priest, View(priest, close, gap: 0)), Is.EqualTo("pusher"));
                Assert.That(Active(priest, View(priest, close, gap: 2)), Is.Null, "「間合いが 1 以上になる」で戻る");
                Assert.That(Active(priest, View(priest, History(new Turn(None, Gap: 0)), gap: 0)), Is.Null, "one turn is not two");
                Assert.That(Switch(priest, "pusher").BranchAtGapOneToTwo![0], Is.EqualTo("push_back"), "全ての枝の先頭");
            });
        }

        [Test]
        public void Priest_SecondStage_FromHpSeventy_RitesEveryPhase_AndNeverGoesBack()
        {
            var priest = Enemies.MiasmaPriest;
            var quiet = History(new Turn(None));
            var stage = Switch(priest, "second_stage");
            Assert.Multiple(() =>
            {
                Assert.That(Active(priest, View(priest, quiet, hp: 71)), Is.Null);
                Assert.That(Active(priest, View(priest, quiet, hp: 70)), Is.EqualTo("second_stage"));
                Assert.That(Active(priest, View(priest, quiet, hp: 74, held: new[] { "second_stage" })), Is.EqualTo("second_stage"), "戻らない, 再生 or not");
                Assert.That(stage.BranchAtGapZero![0], Is.EqualTo("miasma_rite"));
                Assert.That(stage.BranchAtGapThreePlus![0], Is.EqualTo("miasma_rite"));
                Assert.That(stage.Override("miasma_rite")!.Face.StatusList.Single(g => g.Kind == StatusKind.MiasmaShroud).Cap, Is.EqualTo(2));
            });
        }

        [Test]
        public void Priest_AnAdaptationOverTheStage_KeepsBoth_AndTheAdaptationLeads()
        {
            // The stage holds and the player has been adjacent for two turns: 払いのけ first, the stage's
            // 瘴気の儀 next, and the stage's 瘴気纏い 2 still in force.
            var priest = Enemies.MiasmaPriest;
            var close = History(new Turn(None, Gap: 0), new Turn(None, Gap: 0));
            var active = EnemyAi.ActiveSwitch(priest, View(priest, close, hp: 60, gap: 0))!;
            Assert.Multiple(() =>
            {
                Assert.That(active.Id, Is.EqualTo("second_stage+pusher"));
                Assert.That(active.BranchAtGapZero!.Take(2), Is.EqualTo(new[] { "push_back", "miasma_rite" }));
                Assert.That(active.Override("miasma_rite"), Is.Not.Null);
            });
        }

        [Test]
        public void Priest_TheBreakingStaff_BreaksTwoInPlay()
        {
            // Two turns on Guard 10: the next omen is the override's staff, which takes 2 stamina.
            var priest = Enemies.MiasmaPriest;
            var s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(priest, Deck(), Cells, StartGap: 1), NoShuffle).State, NoShuffle).State;
            s = s with { PlayerHistory = History(new Turn(None, Guard: 10, Gap: 1)), Player = s.Player with { Guard = 10, Stamina = 0 } };
            var decided = TurnLoop.EndTurn(s, NoShuffle);
            Assert.That(decided.State.Enemies[0].ActiveSwitch, Is.EqualTo("breaker"));
            Assert.That(decided.State.Omen!.ActionId, Is.EqualTo("staff_strike"));

            s = TurnLoop.BeginPlayerTurn(decided.State, NoShuffle).State;
            s = s with { Player = s.Player with { Guard = 10, Cell = s.Enemy.Cell - 2 } };
            var struck = TurnLoop.EndTurn(s, NoShuffle);
            Assert.That(struck.Events.OfType<StaminaBroken>().First(b => b.Target == Actor.Player).Amount, Is.EqualTo(2));
        }

        // ---- 獄竜 ガルド (§5.4) ----

        [Test]
        public void Angler_Bait_AfterTwoTurnsAtTwoOrMore_HooksAndReels_UntilAdjacent()
        {
            var angler = Enemies.AbyssAngler;
            var far = History(new Turn(None, Gap: 2), new Turn(None, Gap: 4));
            var bait = Switch(angler, "bait");
            Assert.Multiple(() =>
            {
                Assert.That(Active(angler, View(angler, far, gap: 3)), Is.EqualTo("bait"));
                Assert.That(Active(angler, View(angler, far, gap: 0)), Is.Null, "「間合いが 0 になる」で戻る");
                Assert.That(bait.BranchAtGapOneToTwo, Is.EqualTo(new[] { "hook_cast", "reel_in" }));
                Assert.That(bait.BranchAtGapThreePlus, Is.EqualTo(new[] { "hook_cast", "reel_in" }));
                Assert.That(bait.BranchAtGapZero, Is.EqualTo(angler.BranchAtGapZero), "0 is left as it is");
            });
        }

        [Test]
        public void Angler_Snap_EverySecondHookTheyShake_SlacksThenCalls_ForOnePhase()
        {
            var angler = Enemies.AbyssAngler;
            AdaptationView After(params int[] snags)
            {
                // At gap 1 throughout, so bait (2 or more) does not hold beside it.
                var history = History(new Turn(None, Gap: 1), new Turn(None, Gap: 1), new Turn(None, Gap: 1), new Turn(None, Gap: 1));
                foreach (int turn in snags) history = history.WithHookSnag(turn);
                return View(angler, history, turn: 4, gap: 1);
            }

            Assert.Multiple(() =>
            {
                Assert.That(Active(angler, After(3, 4)), Is.EqualTo("snap"), "the 2nd shaken off in the last turn");
                Assert.That(Active(angler, After(4)), Is.Null, "one is not two");
                Assert.That(Active(angler, After(2, 3)), Is.Null, "the 2nd came a turn earlier: that phase has passed");
                Assert.That(Switch(angler, "snap").BranchAtGapZero, Is.EqualTo(new[] { "slack_line", "fathom_call" }));
            });
        }

        [Test]
        public void Angler_Pin_AfterTwoTurnsAdjacent_DrownsThenWhips()
        {
            var angler = Enemies.AbyssAngler;
            var close = History(new Turn(None, Gap: 0), new Turn(None, Gap: 0));
            Assert.Multiple(() =>
            {
                Assert.That(Active(angler, View(angler, close, gap: 0)), Is.EqualTo("pin"));
                Assert.That(Switch(angler, "pin").BranchAtGapZero, Is.EqualTo(new[] { "drown", "line_whip" }));
            });
        }

        [Test]
        public void Angler_SecondStage_StepsOutTwo_DropsTheSlackLine_AndHooksTwo()
        {
            // roster §5.4: 「ガルドが前へ 2 出る（出られるだけ）。以後は糸を緩めるを使わない。鉤縄が 1 回で鉤爪 2 スタックを付ける」.
            var angler = Enemies.AbyssAngler;
            var stage = Switch(angler, "second_stage");
            var s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(angler, Deck(), Cells, StartGap: 4), NoShuffle).State, NoShuffle).State;
            s = s.WithEnemy(s.Enemy with { Hp = 80 });
            int cellBefore = s.Enemy.Cell;
            var end = TurnLoop.EndTurn(s, NoShuffle);
            var stepped = end.Events.OfType<CellsMoved>().Last(m => m.Actor == Actor.Enemy);

            Assert.Multiple(() =>
            {
                Assert.That(stage.EnterMove, Is.EqualTo(2));
                Assert.That(end.Events.OfType<TreeSwitched>().Single().To, Is.EqualTo("second_stage"));
                Assert.That(stepped.To, Is.LessThan(stepped.From), "前へ");
                Assert.That(end.State.Enemy.Cell, Is.LessThan(cellBefore));
                foreach (var each in angler.Switches!.Where(sw => sw.Id.StartsWith("second_stage", StringComparison.Ordinal)))
                {
                    foreach (var band in new[] { each.BranchAtGapZero, each.BranchAtGapOneToTwo, each.BranchAtGapThreePlus })
                    {
                        Assert.That(band ?? angler.BranchAtGapZero, Does.Not.Contain("slack_line"), each.Id);
                    }
                }
                Assert.That(stage.Override("hook_cast")!.Face.StatusList.Single().Stacks, Is.EqualTo(2));
            });

            // The stage is entered once: the next decision does not step again.
            var again = TurnLoop.EndTurn(TurnLoop.BeginPlayerTurn(end.State, NoShuffle).State, NoShuffle);
            Assert.That(again.Events.OfType<TreeSwitched>(), Is.Empty);
        }

        // ---- 歪みの根 (§6.1 / §6.4) ----

        [Test]
        public void Root_TheFiveAdaptations_HoldOnWhatTheyCount()
        {
            var root = Enemies.DistortionRoot;
            string? On(BattleHistory history) => Active(root, View(root, history));

            var fourAttacks = History(new Turn(new[] { A, A }), new Turn(new[] { A, A }));
            var threeAttacks = History(new Turn(new[] { A, A, A }));
            var guarded = History(new Turn(None, Guard: 9), new Turn(None, Guard: 10));
            var stance = History(new Turn(new[] { St }));
            var twoWords = History(new Turn(None), new Turn(None)).WithInfliction(1).WithInfliction(2);
            var steppedIn = History(new Turn(None, Moved: true, Cell: 2), new Turn(None, Moved: true, Cell: 3));
            var steppedOut = History(new Turn(None, Moved: true, Cell: 3), new Turn(None, Moved: true, Cell: 2));
            var once = History(new Turn(None, Moved: false), new Turn(None, Moved: true));

            Assert.Multiple(() =>
            {
                Assert.That(On(fourAttacks), Is.EqualTo("root_a"), "4 attacks");
                Assert.That(On(threeAttacks), Is.Null, "3 is not 4");
                Assert.That(On(guarded), Is.EqualTo("root_g"));
                Assert.That(On(stance), Is.EqualTo("root_st"));
                Assert.That(On(twoWords), Is.EqualTo("root_sk"));
                Assert.That(On(steppedIn), Is.EqualTo("root_m_in"));
                Assert.That(On(steppedOut), Is.EqualTo("root_m_out"));
                Assert.That(On(once), Is.Null);
            });
        }

        [Test]
        public void Root_TheAdaptations_ReshapeTheTree_AsTheRosterSays()
        {
            var root = Enemies.DistortionRoot;
            Assert.Multiple(() =>
            {
                Assert.That(Switch(root, "root_a").BranchAtGapOneToTwo!.Take(2), Is.EqualTo(new[] { "root_grip", "twist" }), "根を張る → 捻じる");
                Assert.That(Switch(root, "root_g").BranchAtGapThreePlus![0], Is.EqualTo("twist"));
                Assert.That(Switch(root, "root_g").Override("twist")!.Face.Break, Is.EqualTo(2), "捻じるの崩しを常に 2 にし");
                Assert.That(Switch(root, "root_st").BranchAtGapZero![0], Is.EqualTo("wither_breath"));
                Assert.That(Switch(root, "root_sk").BranchAtGapZero!.Take(2), Is.EqualTo(new[] { "bark", "sweep" }));
                Assert.That(Switch(root, "root_sk").Cleanse, Is.True);
                Assert.That(Switch(root, "root_m_in").BranchAtGapZero![0], Is.EqualTo("creeping_root"));
                Assert.That(EnemyAi.FaceAt(Switch(root, "root_m_in").Override("creeping_root")!, 3).Push, Is.EqualTo(1), "moved in: pushed back out");
                Assert.That(EnemyAi.FaceAt(Switch(root, "root_m_out").Override("creeping_root")!, 0).Push, Is.EqualTo(-1), "moved out: pulled back in");
            });
        }

        [Test]
        public void Root_TheStages_RiseAtTwoThirds_AndSwayAtOneThird()
        {
            var root = Enemies.DistortionRoot;
            var quiet = History(new Turn(None));
            var stage2 = Switch(root, "stage_2");
            var stage3 = Switch(root, "stage_3");
            Assert.Multiple(() =>
            {
                Assert.That(Active(root, View(root, quiet, hp: 134)), Is.Null);
                Assert.That(Active(root, View(root, quiet, hp: 133)), Is.EqualTo("stage_2"));
                Assert.That(Active(root, View(root, quiet, hp: 66)), Is.EqualTo("stage_3"), "the later stage wins");
                Assert.That((stage2.EnterMove, stage2.Sway), Is.EqualTo((1, false)), "せり上がる 前へ 1");
                Assert.That((stage3.EnterMove, stage3.Sway), Is.EqualTo((0, true)), "前へ 1 と後ろへ 1 を交互に");
                Assert.That(stage2.BranchAtGapZero![0], Is.EqualTo("wither_breath"));
                Assert.That(stage3.BranchAtGapZero!.Take(2), Is.EqualTo(new[] { "root_grip", "sweep" }));
                Assert.That(stage3.Override("root_grip")!.Face.ReachOrDefault, Is.EqualTo(new Reach(0, 4)), "根を張るの届く間合いも 0〜4");
                Assert.That(stage3.Override("wither_breath")!.Face.ReachOrDefault, Is.EqualTo(new Reach(0, 4)), "stage 2's reach carries on");
                Assert.That(Active(root, View(root, History(new Turn(new[] { St })), hp: 60)), Is.EqualTo("stage_3+root_st"), "the adaptations work through the stages");
            });
        }

        [Test]
        public void Root_StageThree_SwaysForwardAndBack_PhaseByPhase()
        {
            var root = Enemies.DistortionRoot;
            var s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(root, Deck(), Cells, StartGap: 3), NoShuffle).State, NoShuffle).State;
            s = s.WithEnemy(s.Enemy with { Hp = 60 });
            s = TurnLoop.EndTurn(s, NoShuffle).State;                 // stage 2 and 3 are held from this step 12
            var moves = new List<int>();
            for (int turn = 0; turn < 3; turn++)
            {
                s = TurnLoop.BeginPlayerTurn(s, NoShuffle).State;
                s = s with { Player = s.Player with { Cell = 1 } };
                var end = TurnLoop.EndTurn(s, NoShuffle);
                var sway = end.Events.OfType<CellsMoved>().FirstOrDefault(m => m.Actor == Actor.Enemy && !m.Pushed);
                moves.Add(sway == null ? 0 : sway.From - sway.To);
                s = end.State;
                if (s.Result != GameResult.Ongoing) break;
            }
            Assert.That(moves.Take(2), Is.EqualTo(new[] { -1, 1 }).Or.EqualTo(new[] { 1, -1 }), "one step each phase, the way alternating");
        }

        [Test]
        public void Root_ShakesOffItsWords_WhenTwoStatusesHaveLanded()
        {
            // root_sk: the player put two words on the root this turn; at step 12 the root takes them all off.
            var root = Enemies.DistortionRoot;
            var s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(root, Deck(), Cells, StartGap: 3), NoShuffle).State, NoShuffle).State;
            s = s.WithEnemy(s.Enemy with { Statuses = StatusSet.Of((StatusKind.Bleed, 2), (StatusKind.Fragile, 1)) });
            s = s with { PlayerHistory = new BattleHistory().WithInfliction(s.Turn).WithInfliction(s.Turn) };
            var end = TurnLoop.EndTurn(s, NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(end.State.Enemies[0].ActiveSwitch, Is.EqualTo("root_sk"));
                Assert.That(end.Events.OfType<StatusCleared>(), Is.EquivalentTo(new[]
                {
                    new StatusCleared(Actor.Enemy, StatusKind.Bleed, 1) { Unit = 0 },
                    new StatusCleared(Actor.Enemy, StatusKind.Fragile, 1) { Unit = 0 },
                }), "出血 2 ticked to 1 at the root's turn start; what is left is shaken off");
                Assert.That(end.State.Enemy.Statuses.KindCount, Is.EqualTo(0));
                Assert.That(end.State.Omen!.ActionId, Is.EqualTo("bark"));
            });
        }

        [Test]
        public void Root_ShakesOffItsWords_AgainWhenTriggeredTwoTurnsInARow()
        {
            // root_sk triggered at two decisions in a row: the switch id does not change, but each
            // trigger clears (roster §6.4 「自分に付いた状態を全て消す」 every time it holds).
            var root = Enemies.DistortionRoot;
            var s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(root, Deck(), Cells, StartGap: 3), NoShuffle).State, NoShuffle).State;
            s = s.WithEnemy(s.Enemy with { Statuses = StatusSet.Of((StatusKind.Bleed, 2), (StatusKind.Fragile, 1)) });
            s = s with { PlayerHistory = s.History.WithInfliction(s.Turn).WithInfliction(s.Turn) };
            var first = TurnLoop.EndTurn(s, NoShuffle);
            Assert.That(first.State.Enemies[0].ActiveSwitch, Is.EqualTo("root_sk"), "first trigger");

            s = TurnLoop.BeginPlayerTurn(first.State, NoShuffle).State;
            s = s.WithEnemy(s.Enemy with { Statuses = StatusSet.Of((StatusKind.Bleed, 3), (StatusKind.Fragile, 1)) });
            s = s with { PlayerHistory = s.History.WithInfliction(s.Turn).WithInfliction(s.Turn) };
            var second = TurnLoop.EndTurn(s, NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(second.State.Enemies[0].ActiveSwitch, Is.EqualTo("root_sk"), "still the same switch");
                Assert.That(second.Events.OfType<TreeSwitched>(), Is.Empty, "no change of switch is announced");
                Assert.That(second.Events.OfType<StatusCleared>().Select(c => c.Kind),
                    Is.EquivalentTo(new[] { StatusKind.Bleed, StatusKind.Fragile }), "the second trigger clears too");
                Assert.That(second.State.Enemy.Statuses.KindCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void TheHistory_CountsTheWordsThePlayerPuts_OnAnEnemy()
        {
            // root_sk counts what lands: a card that puts 出血 on the root is one.
            var root = Enemies.DistortionRoot;
            var s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(root, Deck(CardCatalog.Rend), Cells, StartGap: 0), NoShuffle).State, NoShuffle).State;
            var played = TurnLoop.PlayCard(s, s.Hand.First(c => c.Def.Id == "rend").InstanceId, NoShuffle);
            Assert.That(played.State.History.Inflictions, Is.EqualTo(new[] { 1 }));
        }

        // ---- Every boss ----

        [Test]
        public void EveryBossSwitch_NamesOnlyTheBossesOwnActions_AndEndsEachBranchOnSomethingCheap()
        {
            Assert.Multiple(() =>
            {
                foreach (var boss in Enemies.All.Where(e => e.Rank == EnemyRank.Boss))
                {
                    Assert.That(boss.Switches, Is.Not.Null.And.Not.Empty, boss.Id);
                    foreach (var each in boss.Switches!)
                    {
                        foreach (var band in new[] { GapBand.Zero, GapBand.OneToTwo, GapBand.ThreePlus })
                        {
                            var branch = EnemyAi.BranchFor(boss, band == GapBand.Zero ? 0 : band == GapBand.OneToTwo ? 1 : 3, each);
                            Assert.That(branch, Is.Not.Empty, boss.Id + "/" + each.Id);
                            Assert.That(branch.All(id => boss.Actions.ContainsKey(id)), Is.True, boss.Id + "/" + each.Id);
                            Assert.That(EnemyAi.ActionOf(boss, branch[branch.Count - 1], each).Column, Is.LessThanOrEqualTo(2), boss.Id + "/" + each.Id + " " + band);
                        }
                    }
                }
                foreach (var other in Enemies.All.Where(e => e.Rank != EnemyRank.Boss))
                {
                    Assert.That(other.Switches, Is.Null, other.Id + " fights on its base tree");
                }
            });
        }

        private static readonly CardDef Filler = Fixtures.Card("filler", face: new Face(Guard: 1), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        private static List<CardInstance> Deck(params CardDef[] first)
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < first.Length; i++) deck.Add(new CardInstance(first[i].Id + "-" + i, first[i]));
            for (int i = deck.Count; i < Constants.DeckMin; i++) deck.Add(new CardInstance("filler-" + i, Filler));
            return deck;
        }
    }
}
