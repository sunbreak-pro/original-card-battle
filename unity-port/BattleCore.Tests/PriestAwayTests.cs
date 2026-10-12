using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace BattleCore.Tests
{
    /// <summary>
    /// #497: 大黒蛇 セルク on enemy_roster_v4 v4.18 (#450), which ends the stall against a player who
    /// stays away (Away: 779 of 1,200 fights reached the 60-turn limit on v4.17). Three changes:
    /// 瘴気の儀 is not taken while 瘴気纏い is at its cap (§4.3), breaker puts 鎌首の打ち込み on top
    /// of the 0 and 1〜2 bands only (§4.4), and 瘴気の矢 is +7 at gap 3 or more (§4.2).
    /// </summary>
    public class PriestAwayTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private const int Cells = 8;

        private static readonly CardDef Filler = Fixtures.Card("filler", face: new Face(Guard: 1), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        private static List<CardInstance> Deck()
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < Constants.DeckMin; i++) deck.Add(new CardInstance("filler-" + i, Filler));
            return deck;
        }

        private static readonly EnemyDef Priest = Enemies.MiasmaPriest;

        private static TreeSwitch Switch(string id) => Priest.Switches!.Single(s => s.Id == id);

        private static StatusSet Shroud(int stacks) => StatusSet.Of((StatusKind.MiasmaShroud, stacks));

        /// <summary>The player stands still and plays nothing; each finished phase is the enemy's actions in order.</summary>
        private static List<StepResult> Phases(BattleSetup setup, int count)
        {
            var state = TurnLoop.Start(setup, NoShuffle).State;
            var phases = new List<StepResult>();
            for (int i = 0; i < count; i++)
            {
                state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
                var end = TurnLoop.EndTurn(state with { Player = state.Player with { Stamina = 0 } }, NoShuffle);
                phases.Add(end);
                state = end.State;
            }
            return phases;
        }

        private static string[] Taken(StepResult phase) => phase.Events.OfType<ActionExecuted>().Select(e => e.Action.Id).ToArray();

        private static int BoltRaw(StepResult phase) =>
            phase.Events.OfType<DamageDealt>().Single(d => d.Actor == Actor.Enemy && d.Target == Actor.Player).Raw;

        // ---- §4.3: 瘴気の儀 waits while 瘴気纏い is at its cap ----

        [Test]
        public void TheRite_IsBarred_WhileTheShroudIsAtTheCapOfTheSwitch()
        {
            // 「上限は第 1 段階で 1、第 2 段階で 2」: the cap is read off the rite as it stands under the
            // switch, so the second stage's override (cap 2) and every adaptation over it carry it.
            var stage = Switch("second_stage");
            var stageBreaker = Switch("second_stage+breaker");
            Assert.Multiple(() =>
            {
                Assert.That(EnemyAi.Barred(Priest, StatusSet.Empty, beforeFoeTurn: true), Does.Not.Contain("miasma_rite"));
                Assert.That(EnemyAi.Barred(Priest, Shroud(1), beforeFoeTurn: true), Does.Contain("miasma_rite"));
                Assert.That(EnemyAi.Barred(Priest, Shroud(1), beforeFoeTurn: false), Does.Contain("miasma_rite"), "a lasting word does not tick");
                Assert.That(EnemyAi.Barred(Priest, Shroud(1), beforeFoeTurn: true, active: stage), Does.Not.Contain("miasma_rite"));
                Assert.That(EnemyAi.Barred(Priest, Shroud(2), beforeFoeTurn: true, active: stage), Does.Contain("miasma_rite"));
                Assert.That(EnemyAi.Barred(Priest, Shroud(1), beforeFoeTurn: false, active: stageBreaker), Does.Not.Contain("miasma_rite"));
                Assert.That(EnemyAi.Barred(Priest, Shroud(2), beforeFoeTurn: false, active: stageBreaker), Does.Contain("miasma_rite"));
            });
        }

        [Test]
        public void TheRite_ReadsTheShroudAsItWouldSitWithoutTheCharm()
        {
            // §9.1 竜語の符 (#452) keeps the first stack off. The rite counts that stack as held, so the
            // charm lowers the word and leaves セルク's choice as it is.
            var warded = Shroud(1);
            Assert.Multiple(() =>
            {
                Assert.That(EnemyAi.Barred(Priest, StatusSet.Empty, beforeFoeTurn: true, warded: warded), Does.Contain("miasma_rite"));
                Assert.That(EnemyAi.Barred(Priest, StatusSet.Empty, beforeFoeTurn: true, active: Switch("second_stage"), warded: warded),
                    Does.Not.Contain("miasma_rite"));
                Assert.That(EnemyAi.Barred(Priest, Shroud(1), beforeFoeTurn: true, active: Switch("second_stage"), warded: warded),
                    Does.Contain("miasma_rite"));
            });
        }

        [Test]
        public void TheCapBar_IsOnlyWhereTheRosterSaysIt()
        {
            // 獄竜 ガルド's 深みへの呼び声 also puts a capped word on (深み up to 2, §5.1), but the roster
            // does not bar it at the cap: it keeps being taken and the stack past the cap is dropped.
            var angler = Enemies.AbyssAngler;
            Assert.Multiple(() =>
            {
                Assert.That(angler.Actions["fathom_call"].NotAtFoeCap, Is.False);
                Assert.That(EnemyAi.Barred(angler, StatusSet.Of((StatusKind.Depths, 2)), beforeFoeTurn: true), Is.Empty);
                Assert.That(Priest.Actions.Values.Where(a => a.NotAtFoeCap).Select(a => a.Id), Is.EqualTo(new[] { "miasma_rite" }));
            });
        }

        [Test]
        public void AtGapThree_TheRiteOpens_ThenTheBoltComesEveryPhase()
        {
            // roster §4.3 典型: 「瘴気の儀 → 瘴気の帳」, then 「瘴気の矢（8 + 7 = 15）→ とぐろを締める」 every
            // phase. On v4.17 the rite stayed on top of the 3+ band and used up the recovery of 3.
            var phases = Phases(new BattleSetup(Priest, Deck(), Cells, StartGap: 3), 3);
            Assert.Multiple(() =>
            {
                Assert.That(Taken(phases[0]), Is.EqualTo(new[] { "miasma_rite", "ward" }));
                Assert.That(Taken(phases[1]), Is.EqualTo(new[] { "miasma_bolt", "coil" }), "the omen and the second action both pass the rite");
                Assert.That(Taken(phases[2]), Is.EqualTo(new[] { "miasma_bolt", "coil" }));
                Assert.That(BoltRaw(phases[1]), Is.EqualTo(15));
                Assert.That(phases[2].State.Player.Statuses.Stacks(StatusKind.MiasmaShroud), Is.EqualTo(1));
            });
        }

        [Test]
        public void WithTheCharm_TheRiteIsTakenOnce_AsWithoutIt()
        {
            // The charm kept the stack off, so the player holds none; the rite still is not taken again.
            var phases = Phases(new BattleSetup(Priest, Deck(), Cells, StartGap: 3, BossWordWard: 1), 2);
            Assert.Multiple(() =>
            {
                Assert.That(Taken(phases[0]), Is.EqualTo(new[] { "miasma_rite", "ward" }));
                Assert.That(phases[0].State.Player.Statuses.Stacks(StatusKind.MiasmaShroud), Is.EqualTo(0));
                Assert.That(Taken(phases[1]), Is.EqualTo(new[] { "miasma_bolt", "coil" }));
            });
        }

        // ---- §4.2: 瘴気の矢, 間合い 3 以上: 威力 +7 ----

        [Test]
        public void TheBolt_IsFifteenFromGapThree_AndEightNearer()
        {
            var trait = Priest.Actions["miasma_bolt"].Trait!;
            var atTwo = Phases(new BattleSetup(Priest, Deck(), Cells, StartGap: 2), 1)[0];
            Assert.Multiple(() =>
            {
                Assert.That(trait, Is.EqualTo(new Trait(TraitCondition.GapAtLeast, TraitEffect.PowerBonus, 7, Threshold: 3)));
                Assert.That(Taken(atTwo)[0], Is.EqualTo("miasma_bolt"));
                Assert.That(BoltRaw(atTwo), Is.EqualTo(8), "2 以下の間合いでは 8 のまま");
            });
        }

        // ---- §4.4: breaker leaves the 3+ band alone ----

        [Test]
        public void Breaker_PutsTheStaffOnTopOfZeroAndOneToTwo_AndLeavesThreePlus()
        {
            var breaker = Switch("breaker");
            var stage = Switch("second_stage");
            var stageBreaker = Switch("second_stage+breaker");
            Assert.Multiple(() =>
            {
                Assert.That(breaker.BranchAtGapZero, Is.EqualTo(new[] { "staff_strike", "coil" }));
                Assert.That(breaker.BranchAtGapOneToTwo, Is.EqualTo(new[] { "staff_strike", "miasma_bolt", "binding_word", "coil" }));
                Assert.That(breaker.BranchAtGapThreePlus, Is.EqualTo(Priest.BranchAtGapThreePlus), "3 以上の枝は変えない");
                Assert.That(stageBreaker.BranchAtGapOneToTwo![0], Is.EqualTo("staff_strike"));
                Assert.That(stageBreaker.BranchAtGapThreePlus, Is.EqualTo(stage.BranchAtGapThreePlus));
                Assert.That(stageBreaker.BranchAtGapThreePlus![0], Is.EqualTo("miasma_rite"));
            });
        }

        [Test]
        public void Breaker_AtGapThree_TheOmenIsTheBolt_NotAStaffOutOfReach()
        {
            // A player on Guard 10 two turns running at gap 3, already holding 瘴気纏い 1: breaker holds,
            // and the next omen is 瘴気の矢 (the rite at its cap, 帳 spent), not the staff that reaches 0〜1.
            var s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(Priest, Deck(), Cells, StartGap: 3), NoShuffle).State, NoShuffle).State;
            var history = new BattleHistory().WithTurnEnd(new PlayerTurnEnd(10, 3, false, s.Player.Cell));
            s = s with { PlayerHistory = history, Player = s.Player with { Guard = 10, Stamina = 0, Statuses = Shroud(1) } };
            var decided = TurnLoop.EndTurn(s, NoShuffle);
            Assert.Multiple(() =>
            {
                Assert.That(decided.State.Enemies[0].ActiveSwitch, Is.EqualTo("breaker"));
                Assert.That(decided.State.Omen!.ActionId, Is.EqualTo("miasma_bolt"));
            });
        }
    }
}
