using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// The eleven enemies of enemy_roster_v4.md v4.3 (#189): the roster's numbers and tree shapes,
    /// the second action of elites and bosses (§1.3), the stance used once a battle (§1.2), the
    /// two-blow face (§2.6), and every one of them fought to the end.
    /// </summary>
    public class EnemyRosterTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        /// <summary>The line an enemy needs to start at gap 3: the player's cell 2 + 3 + its size (roster §0).</summary>
        private static int CellsFor(EnemyDef enemy) => Constants.PlayerStartCell + Constants.StartGap + enemy.Size;

        private static readonly CardDef Filler = Fixtures.Card("filler", face: new Face(Guard: 1), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        private static List<CardInstance> Fillers(int count)
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < count; i++) deck.Add(new CardInstance("filler-" + i, Filler));
            return deck;
        }

        // ---- The roster ----

        [Test]
        public void TheRoster_HoldsElevenEnemies_InRosterOrder()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Enemies.All.Select(e => e.Id), Is.EqualTo(new[]
                {
                    "polearm_warped", "shadow_hound", "rusted_revenant", "crossbow_hunter", "mist_archer", "twin_blade_warped",
                    "armored_warden", "pack_alpha",
                    "miasma_priest", "abyss_angler", "distortion_root",
                }));
                foreach (var enemy in Enemies.All) Assert.That(Enemies.ById(enemy.Id), Is.SameAs(enemy));
                Assert.That(Enemies.PolearmWarped.Name, Is.EqualTo("錆槍の竜兵"), "#179");
            });
        }

        [TestCase(EnemyRank.Normal, 6, 50, 70, 10, 2, 1)]
        [TestCase(EnemyRank.Elite, 2, 90, 110, 12, 3, 2)]
        [TestCase(EnemyRank.Boss, 3, 160, 200, 14, 4, 2)]
        public void EachRank_KeepsToTheRosterScale(EnemyRank rank, int count, int hpMin, int hpMax, int stamina, int recovery, int actions)
        {
            // roster §0: HP, max stamina and recovery by rank; §1.3: elites and bosses act twice.
            var ofRank = Enemies.All.Where(e => e.Rank == rank).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(ofRank, Has.Count.EqualTo(count));
                foreach (var enemy in ofRank)
                {
                    Assert.That(enemy.MaxHp, Is.InRange(hpMin, hpMax), enemy.Id);
                    Assert.That(enemy.MaxStamina, Is.EqualTo(stamina), enemy.Id);
                    Assert.That(enemy.Recovery, Is.EqualTo(recovery), enemy.Id);
                    Assert.That(enemy.ActionsPerPhase, Is.EqualTo(actions), enemy.Id);
                }
            });
        }

        [Test]
        public void TheLargeOnes_AreTheWardenAndTheBosses()
        {
            // roster §1.8: size 2 for 鉄壁の門竜 and the three bosses; everyone else 1.
            Assert.That(Enemies.All.Where(e => e.Size == 2).Select(e => e.Id),
                Is.EquivalentTo(new[] { "armored_warden", "miasma_priest", "abyss_angler", "distortion_root" }));
        }

        [Test]
        public void EveryTree_EndsCheap_AndItsFarBranchCanClose()
        {
            // roster §1.2 / battle_core_v4 §6.1: each branch ends on a column 1〜2 action, and the 3+
            // branch holds a forward move or something that reaches 3.
            Assert.Multiple(() =>
            {
                foreach (var enemy in Enemies.All)
                {
                    foreach (var band in new[] { GapBand.Zero, GapBand.OneToTwo, GapBand.ThreePlus })
                    {
                        var branch = enemy.Branch(band);
                        Assert.That(enemy.Actions[branch[branch.Count - 1]].Column, Is.LessThanOrEqualTo(2), enemy.Id + " " + band);
                    }
                    bool closes = enemy.BranchAtGapThreePlus.Select(id => enemy.Actions[id]).Any(a =>
                        a.Face.Move > 0
                        || (EnemyAi.IsOpponentDirected(a.Attributes, a.Face, a.Targets) && a.Face.ReachOrDefault.Contains(3)));
                    Assert.That(closes, Is.True, enemy.Id);
                }
            });
        }

        [Test]
        public void EveryAction_KeepsToTheCellLimits_AndTheSelfOnesReadNoReach()
        {
            Assert.Multiple(() =>
            {
                foreach (var enemy in Enemies.All)
                foreach (var action in enemy.Actions.Values)
                {
                    string at = enemy.Id + "." + action.Id;
                    Assert.That(Math.Abs(action.Face.Move), Is.LessThanOrEqualTo(Constants.MoveStepMax), at);
                    Assert.That(Math.Abs(action.Face.Push), Is.LessThanOrEqualTo(Constants.MoveStepMax), at);
                    bool aims = action.Attributes.HasFlag(BattleAttribute.Attack) || action.Face.GivesFoeStatus
                        || action.Face.Push != 0 || action.Face.Break > 0;
                    Assert.That(action.Targets == TargetKind.Self, Is.EqualTo(!aims), at + ": Self exactly when nothing aims at the player");
                    Assert.That(action.Face.Stance != null, Is.EqualTo(action.Attributes.HasFlag(BattleAttribute.Stance)), at);
                }
            });
        }

        [TestCase("mist_archer", "fade", OmenKind.Guard)]
        [TestCase("armored_warden", "advance_guard", OmenKind.Guard)]
        [TestCase("abyss_angler", "slack_line", OmenKind.Guard)]
        [TestCase("distortion_root", "creeping_root", OmenKind.Guard)]
        [TestCase("pack_alpha", "herd", OmenKind.Skill)]
        [TestCase("abyss_angler", "reel_in", OmenKind.Skill)]
        [TestCase("polearm_warped", "step_forward", OmenKind.Move)]
        [TestCase("rusted_revenant", "trudge", OmenKind.Move)]
        public void TheOmenKind_IsTheRostersColumn(string enemy, string action, OmenKind kind)
        {
            // roster §2〜§6 予兆: the same faces read 守り or 動 by the roster's column, not by attribute order.
            Assert.That(EnemyAi.LabelOf(Enemies.ById(enemy).Actions[action]).Kind, Is.EqualTo(kind));
        }

        [Test]
        public void TheSlowAnEnemyPuts_LastsIntoThePlayersTurn()
        {
            // #197: 鈍足 put on in the enemy phase loses a stack at the player's turn start, so it is
            // put on with 2 (battle_core_v4 §5) where the roster writes 1.
            Assert.Multiple(() =>
            {
                foreach (var enemy in Enemies.All)
                foreach (var action in enemy.Actions.Values)
                foreach (var grant in action.Face.StatusList.Where(g => g.Kind == StatusKind.Slow && !g.OnSelf))
                {
                    Assert.That(grant.Stacks, Is.GreaterThanOrEqualTo(2), enemy.Id + "." + action.Id);
                }
            });
        }

        // ---- Two actions a phase (§1.3) ----

        [Test]
        public void TheWarden_SetsItsWallThenAdvances_InOnePhase()
        {
            // roster §3.1 典型: at gap 3, 鉄壁 (column 2) then 盾を掲げて前進 (column 1) — cost 3, gap 2 after.
            var enemy = Enemies.ArmoredWarden;
            var setup = new BattleSetup(enemy, Fillers(20), CellsFor(enemy));
            var state = TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoShuffle).State, NoShuffle).State;
            var end = TurnLoop.EndTurn(state, NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<ActionExecuted>().Select(e => e.Action.Id), Is.EqualTo(new[] { "iron_wall", "advance_guard" }));
                Assert.That(end.State.Enemy.Stance, Is.EqualTo(enemy.Actions["iron_wall"].Face.Stance));
                Assert.That(end.State.Enemies[0].Spent, Is.EqualTo(new[] { "iron_wall" }));
                Assert.That(end.State.Gap, Is.EqualTo(2));
                Assert.That(end.State.Enemy.Stamina, Is.EqualTo(12 - 3));
            });
        }

        [Test]
        public void TheSecondAction_IsNeverTheFirstAgain()
        {
            // roster §1.3: the tree is read again after the first action and the same one is skipped.
            var enemy = Enemies.PackAlpha;
            var setup = new BattleSetup(enemy, Fillers(40), CellsFor(enemy));
            var state = TurnLoop.Start(setup, NoShuffle).State;
            for (int turn = 0; turn < 12 && state.Result == GameResult.Ongoing; turn++)
            {
                state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
                var end = TurnLoop.EndTurn(state, NoShuffle);
                var ids = end.Events.OfType<ActionExecuted>().Select(e => e.Action.Id).ToList();
                Assert.That(ids.Count, Is.LessThanOrEqualTo(2));
                Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count), "turn " + turn + ": " + string.Join(", ", ids));
                state = end.State;
            }
        }

        [Test]
        public void AStanceAction_IsUsedOnceABattle_AndLeavesTheTree()
        {
            // roster §1.2: 瘴甲の竜兵 opens with 鉄の身 at gap 3+, and never again.
            var enemy = Enemies.RustedRevenant;
            var setup = new BattleSetup(enemy, Fillers(40), BattleSetup.SliceFieldCells, StartGap: 3);
            var state = TurnLoop.Start(setup, NoShuffle).State;
            Assert.That(state.Omen!.ActionId, Is.EqualTo("iron_body"));

            var used = new List<string>();
            for (int turn = 0; turn < 10 && state.Result == GameResult.Ongoing; turn++)
            {
                state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
                // Keep the gap at 3+ by backing off: the player walks to cell 1 whenever it can.
                state = state with { Player = state.Player with { Cell = 1 } };
                var end = TurnLoop.EndTurn(state, NoShuffle);
                used.AddRange(end.Events.OfType<ActionExecuted>().Select(e => e.Action.Id));
                state = end.State;
            }
            Assert.That(used.Count(id => id == "iron_body"), Is.EqualTo(1), string.Join(", ", used));
            Assert.That(EnemyAi.ChooseAction(enemy, 3, 10, state.Enemies[0].Spent)!.Id, Is.EqualTo("trudge"));
        }

        [Test]
        public void TheIronBody_RaisesGuardAtEachEnemyTurnStart()
        {
            // roster §2.3: 鉄の身 is +3 Guard at the start of every enemy turn once it stands.
            var enemy = Enemies.RustedRevenant;
            var setup = new BattleSetup(enemy, Fillers(40), BattleSetup.SliceFieldCells, StartGap: 3);
            var state = TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoShuffle).State, NoShuffle).State;
            state = TurnLoop.EndTurn(state, NoShuffle).State;                 // sets 鉄の身
            state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
            var next = TurnLoop.EndTurn(state, NoShuffle);

            Assert.That(next.Events.OfType<StanceFired>().Any(e => e.Actor == Actor.Enemy && e.Hook == StanceHook.TurnStart), Is.True);
            Assert.That(next.Events.OfType<GuardGained>().First(e => e.Actor == Actor.Enemy).Amount, Is.EqualTo(3));
        }

        // ---- Faces the roster brought in ----

        [Test]
        public void TheTwinSlash_LeavesFragileOnTheFirstBlow_ForTheSecond()
        {
            // roster §2.6: 6 × 2, 脆化 1 after the first blow, so the second is 9: 15 in all.
            var enemy = Enemies.TwinBladeWarped;
            var setup = new BattleSetup(enemy, Fillers(20), BattleSetup.SliceFieldCells, StartGap: 0);
            var state = TurnLoop.Start(setup, NoShuffle).State;
            Assert.That(state.Omen!.ActionId, Is.EqualTo("twin_slash"));
            state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
            state = state with { Player = state.Player with { Guard = 0 } };
            var end = TurnLoop.EndTurn(state with { Player = state.Player with { Stamina = 0 } }, NoShuffle);

            var blows = end.Events.OfType<DamageDealt>().Where(d => d.Target == Actor.Player).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(blows.Select(b => b.Raw), Is.EqualTo(new[] { 6, 9 }));
                Assert.That(end.State.Player.Hp, Is.EqualTo(Constants.PlayerMaxHp - 15));
                Assert.That(end.State.Player.Statuses.Has(StatusKind.Fragile), Is.False, "spent by the second blow");
                AssertInOrder(end.Events, typeof(DamageDealt), typeof(StatusApplied), typeof(StatusConsumed), typeof(DamageDealt));
            });
        }

        [Test]
        public void TheTwinSlash_TakesOneMultiplierABlow_AndSpendsEmpowerOnceOrNotAtAll()
        {
            // battle_core §5.1 (§19.5 S13): one multiplier a blow, 脆化 first. 強化 rides the blows
            // 脆化 does not take and is spent once for the face; when 脆化 takes every blow it stays.
            var enemy = Enemies.TwinBladeWarped;
            var setup = new BattleSetup(enemy, Fillers(20), BattleSetup.SliceFieldCells, StartGap: 0);
            var state = TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoShuffle).State, NoShuffle).State;
            state = state.WithEnemy(state.Enemy with { Statuses = StatusSet.Of((StatusKind.Empower, 1)) });
            state = state with { Player = state.Player with { Guard = 0, Stamina = 0 } };

            // No 脆化 on the player: 強化 takes the first blow, the 脆化 it leaves takes the second.
            var bare = TurnLoop.EndTurn(state, NoShuffle);
            // 脆化 2 on the player: 脆化 takes both blows, and 強化 waits for the next attack.
            var fragile = TurnLoop.EndTurn(state with { Player = state.Player with { Statuses = StatusSet.Of((StatusKind.Fragile, 2)) } }, NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(bare.Events.OfType<DamageDealt>().Where(d => d.Target == Actor.Player).Select(b => b.Raw), Is.EqualTo(new[] { 9, 9 }), "not 6 × 1.5 × 1.5 = 14 on the second");
                Assert.That(bare.Events.OfType<StatusConsumed>().Count(c => c.Kind == StatusKind.Empower), Is.EqualTo(1));
                Assert.That(bare.State.Enemy.Statuses.Has(StatusKind.Empower), Is.False);

                Assert.That(fragile.Events.OfType<DamageDealt>().Where(d => d.Target == Actor.Player).Select(b => b.Raw), Is.EqualTo(new[] { 9, 9 }));
                Assert.That(fragile.Events.OfType<StatusConsumed>().Any(c => c.Kind == StatusKind.Empower), Is.False);
                Assert.That(fragile.State.Enemy.Statuses.Stacks(StatusKind.Empower), Is.EqualTo(1));
                Assert.That(fragile.State.Player.Statuses.Stacks(StatusKind.Fragile), Is.EqualTo(1), "2 − 1, + 1 from the first blow, − 1");
            });
        }

        [Test]
        public void TheHelmSplitter_BreaksTwoWhenAdjacent()
        {
            // roster §3.1: 崩し 1, +1 at gap 0.
            var enemy = Enemies.ArmoredWarden;
            var setup = new BattleSetup(enemy, Fillers(20), CellsFor(enemy), StartGap: 0);
            var state = TurnLoop.Start(setup, NoShuffle).State;
            Assert.That(state.Omen!.ActionId, Is.EqualTo("helm_splitter"));
            state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
            var end = TurnLoop.EndTurn(state, NoShuffle);

            var broken = end.Events.OfType<StaminaBroken>().First();
            Assert.That(broken.Amount, Is.EqualTo(2));
            Assert.That(broken.Target, Is.EqualTo(Actor.Player));
        }

        [Test]
        public void TheBossesStandIns_PutCommonWordsOnThePlayer()
        {
            // The demo's stand-ins for the boss-only statuses (roster §4.1 / §5.1 / §6.2).
            Assert.Multiple(() =>
            {
                Assert.That(Enemies.MiasmaPriest.Actions["miasma_rite"].Face.StatusList, Does.Contain(new StatusGrant(StatusKind.Fatigue, 2)));
                Assert.That(Enemies.MiasmaPriest.Actions["binding_word"].Face.StatusList, Does.Contain(new StatusGrant(StatusKind.Slow, 2)));
                Assert.That(Enemies.AbyssAngler.Actions["hook_cast"].Face.StatusList, Does.Contain(new StatusGrant(StatusKind.Slow, 2)));
                Assert.That(Enemies.AbyssAngler.Actions["fathom_call"].Face.StatusList, Does.Contain(new StatusGrant(StatusKind.Fatigue, 2)));
                Assert.That(Enemies.DistortionRoot.Actions["root_grip"].Face.StatusList, Does.Contain(new StatusGrant(StatusKind.Bleed, 2)));
                Assert.That(Enemies.DistortionRoot.Actions["wither_breath"].Face.StatusList, Does.Contain(new StatusGrant(StatusKind.Fatigue, 3)));
                Assert.That(Enemies.DistortionRoot.Actions["creeping_root"].Face.Push, Is.EqualTo(-1), "伸びる根 always pulls one");
            });
        }

        // ---- Every enemy, fought to the end ----

        /// <summary>
        /// #189 の完了条件: every enemy, fought with a fixed seed to the end without an exception. The
        /// player plays the leftmost card it can pay for, with the prototype deck and with a random
        /// deck of the eighty — except that it does not step further back once 2 or more cells part
        /// it from the enemy (<see cref="Fights"/>). Backing off to the end of the line would leave
        /// 灰弩の竜兵, 燐弓の竜兵 and 大黒蛇 セルク with nothing that reaches gap 4, and the fight would
        /// never end (a hole in the roster's far branches, taken to the cards lane). The sweep over
        /// many seeds, the stall count and the win-rate table are #192's.
        /// </summary>
        [TestCaseSource(nameof(AllIds))]
        public void EachEnemy_FightsToTheEnd_WithAFixedSeed(string id)
        {
            var enemy = Enemies.ById(id);
            for (int seed = 1; seed <= 6; seed++)
            {
                var rng = new SeededRng(seed * 104729);
                var deck = seed % 2 == 0 ? PrototypeDeck.Build() : RandomDeck(rng, 20 + seed * 3);
                var state = TurnLoop.Start(new BattleSetup(enemy, deck, CellsFor(enemy)), rng).State;
                int turns = 0;
                Assert.DoesNotThrow(() =>
                {
                    while (state.Result == GameResult.Ongoing && turns < 200)
                    {
                        state = TurnLoop.BeginPlayerTurn(state, rng).State;
                        turns++;
                        while (state.Result == GameResult.Ongoing)
                        {
                            var next = state.Hand.FirstOrDefault(c => TurnLoop.CanPlay(state, c.InstanceId) == PlayRefusal.None && Fights(state, c.Def));
                            if (next == null) break;
                            state = TurnLoop.PlayCard(state, next.InstanceId, rng).State;
                        }
                        if (state.Result == GameResult.Ongoing) state = TurnLoop.EndTurn(state, rng).State;
                    }
                }, id + " seed " + seed);
                Assert.That(state.Result, Is.Not.EqualTo(GameResult.Ongoing), id + " seed " + seed + " did not end in 200 turns");
                TestContext.Out.WriteLine(id + " seed " + seed + ": " + state.Result + " in " + turns + " turns");
            }
        }

        private static IEnumerable<string> AllIds() => Enemies.All.Select(e => e.Id);

        /// <summary>The test's player keeps its gap: a card that steps back is not played once the gap is 2 or more.</summary>
        private static bool Fights(BattleState state, CardDef def) => def.Face.Move >= 0 || state.Gap < 2;

        /// <summary>
        /// A legal random deck (§8) that holds at least three cards stepping forward, one of them a card
        /// that closes from gap 3 (駆け込み or 疾風突き). 大黒蛇 セルク never moves, never strikes from 3+ and binds the player's feet (鈍足,
        /// standing in for 呪縛) every phase, so a deck whose only steps are one cell can neither win
        /// nor lose against it — the other hole taken to the cards lane.
        /// </summary>
        private static List<CardInstance> RandomDeck(IRng rng, int size)
        {
            var counts = new Dictionary<string, int>();
            var deck = new List<CardInstance>();
            var forward = CardCatalog.All.Where(c => c.Face.Move > 0).ToList();
            // The two cards that close from gap 3 even under 鈍足: 駆け込み and 疾風突き.
            var leap = CardCatalog.All.Where(c => c.Face.Move >= 2
                && (c.Targets == TargetKind.Self || c.Face.ReachOrDefault.Contains(3))).ToList();
            while (deck.Count < size)
            {
                var pool = deck.Count(c => leap.Contains(c.Def)) < 1 ? leap
                    : deck.Count(c => c.Def.Face.Move > 0) < 3 ? forward
                    : CardCatalog.All;
                var def = pool[(int)(rng.NextDouble() * pool.Count) % pool.Count];
                counts.TryGetValue(def.Id, out int held);
                if (held >= Constants.CopiesMax) continue;
                if (Cards.IsStanceCard(def) && deck.Count(c => Cards.IsStanceCard(c.Def)) >= Constants.StanceCardsMax) continue;
                counts[def.Id] = held + 1;
                deck.Add(new CardInstance(def.Id + "-" + held, def));
            }
            return deck;
        }

        private static void AssertInOrder(IReadOnlyList<BattleEvent> actual, params Type[] expected)
        {
            int cursor = 0;
            foreach (var type in expected)
            {
                int found = -1;
                for (int i = cursor; i < actual.Count; i++)
                {
                    if (actual[i].GetType() == type) { found = i; break; }
                }
                Assert.That(found, Is.GreaterThanOrEqualTo(0), type.Name + " is missing after index " + cursor);
                cursor = found + 1;
            }
        }
    }
}
