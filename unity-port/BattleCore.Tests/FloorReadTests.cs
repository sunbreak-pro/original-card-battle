using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// #455: the three things the battle screen reads off the core for battle-visual-v1 §4.2 and §4.7 —
    /// the aimed cells that meet the boundary an omen's action reads (§6 境目を満たすマス), the cell an
    /// enemy's moving omen takes it to, and the stamina the player gets back at the next turn start.
    /// The line is the slice's 6 cells with the player on cell 2.
    /// </summary>
    public class FloorReadTests
    {
        private const int Cells = BattleSetup.SliceFieldCells;

        private static readonly IRng NoRng = new FixedRng(0.9999999);

        private static readonly EnemyActionDef Wait =
            Fixtures.EnemyAction("wait", face: new Face(), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        /// <summary>An enemy that never touches the player.</summary>
        private static readonly EnemyDef Idle = Fixtures.Enemy("idle", atZero: Wait, atOneToTwo: Wait, atThreePlus: Wait);

        private static readonly CardDef Filler =
            Fixtures.Card("filler", 1, new Face(Guard: 1), BattleAttribute.Guard, targets: TargetKind.Self);

        private static BattleState Opened(EnemyDef enemy, int gap)
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < Constants.HandDraw * 3; i++) deck.Add(new CardInstance(Filler.Id + "-" + i, Filler));
            var start = TurnLoop.Start(new BattleSetup(enemy, deck, Cells, StartGap: gap), NoRng).State;
            return TurnLoop.BeginPlayerTurn(start, NoRng).State;
        }

        // ---- §6 境目を満たすマス ----------------------------------------------------------------------

        [Test]
        public void TheBoundary_IsTheGapTraitOfTheAction()
        {
            var sweep = Enemies.PolearmWarped.Actions["sweep"];
            Assert.That(EnemyAi.BoundaryOf(sweep), Is.EqualTo(new GapBoundary(AtLeast: true, 2)), "払い斬り: 間合い 2 以上");
            Assert.That(EnemyAi.BoundaryOf(Enemies.ArmoredWarden.Actions["helm_splitter"]),
                Is.EqualTo(new GapBoundary(AtLeast: false, 0)), "打ち下ろし: 間合い 0 以下");
            Assert.That(EnemyAi.BoundaryOf(Enemies.PolearmWarped.Actions["shove"]), Is.Null, "無防備 reads no gap");
            Assert.That(EnemyAi.BoundaryOf(Enemies.PolearmWarped.Actions["reach_thrust"]), Is.Null, "no trait");
        }

        [Test]
        public void ThePolearmsSweep_ThickensOnlyTheAimedCellAtGapTwo()
        {
            // Gap 2: the enemy on cell 5. 払い斬り reaches 1〜2 (cells 2 and 3); 間合い 2 以上 is cell 2.
            BattleState state = Opened(Enemies.PolearmWarped, 2);
            Assert.That(state.Enemies[0].Omen!.ActionId, Is.EqualTo("sweep"));
            Assert.That(EnemyAi.TargetCells(state.Enemies[0].Omen!.Label, state.Enemy), Is.EqualTo(new[] { 2, 3 }));
            Assert.That(TurnLoop.OmenBoundaryCells(state, 0), Is.EqualTo(new[] { 2 }));

            // Gap 1: the enemy on cell 4, aimed cells 1 and 2, gap 2 is cell 1.
            BattleState close = Opened(Enemies.PolearmWarped, 1);
            Assert.That(close.Enemies[0].Omen!.ActionId, Is.EqualTo("sweep"));
            Assert.That(TurnLoop.OmenBoundaryCells(close, 0), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void TheBoundaryCells_FollowTheEnemyCell()
        {
            var sweep = Enemies.PolearmWarped.Actions["sweep"];
            var body = Fixtures.Combatant(cell: 6);
            // From cell 6: gap 1 is cell 4, gap 2 is cell 3.
            Assert.That(EnemyAi.BoundaryCells(sweep, body), Is.EqualTo(new[] { 3 }));
            Assert.That(EnemyAi.BoundaryCells(sweep, body with { Cell = 4 }), Is.EqualTo(new[] { 1 }));
            // A cell past the line's start is not aimed at, so it is not thickened either.
            Assert.That(EnemyAi.BoundaryCells(sweep, body with { Cell = 3 }), Is.Empty);
        }

        [Test]
        public void AnAtMostBoundary_ThickensTheAdjacentCell()
        {
            // 鎌首の打ち込み (0〜1, 間合い 0 以下: 威力 +5) from cell 6: aimed 4 and 5, gap 0 is cell 5.
            var strike = Enemies.MiasmaPriest.Actions["staff_strike"];
            Assert.That(EnemyAi.BoundaryCells(strike, Fixtures.Combatant(cell: 6)), Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void AnOmenWithoutABoundary_ThickensNothing()
        {
            // Gap 0: 柄尻の押し込み reads 無防備, not a gap.
            BattleState state = Opened(Enemies.PolearmWarped, 0);
            Assert.That(state.Enemies[0].Omen!.ActionId, Is.EqualTo("shove"));
            Assert.That(TurnLoop.OmenBoundaryCells(state, 0), Is.Empty);
            // A rest aims at nothing.
            BattleState rested = state.WithOmen(0, EnemyAi.OmenOf(null));
            Assert.That(TurnLoop.OmenBoundaryCells(rested, 0), Is.Empty);
        }

        // ---- 敵が動く予兆の移る先 ------------------------------------------------------------------------

        [Test]
        public void ThePolearmsStep_TakesItOneCellIn()
        {
            // Gap 3: the enemy on cell 6 shows 踏み込み (前へ 1).
            BattleState state = Opened(Enemies.PolearmWarped, 3);
            Assert.That(state.Enemies[0].Omen!.ActionId, Is.EqualTo("step_forward"));
            Assert.That(TurnLoop.OmenMoveCell(state, 0), Is.EqualTo(5));
        }

        [Test]
        public void TheHoundsLunge_StopsOneCellPastThePlayer()
        {
            // Gap 2: the hound on cell 5 shows 跳びかかり (前へ 2) and lands on cell 3, next to the player.
            BattleState state = Opened(Enemies.ShadowHound, 2);
            Assert.That(state.Enemies[0].Omen!.ActionId, Is.EqualTo("lunge_in"));
            Assert.That(TurnLoop.OmenMoveCell(state, 0), Is.EqualTo(3));

            // Gap 1: from cell 4 it asks for two cells and gets one.
            BattleState close = Opened(Enemies.ShadowHound, 1);
            Assert.That(TurnLoop.OmenMoveCell(close, 0), Is.EqualTo(3));
        }

        [Test]
        public void AMoveThatGoesNowhere_HasNoCell()
        {
            BattleState state = Opened(Enemies.PolearmWarped, 3);
            // 鈍足 takes the one cell off.
            BattleState slowed = state.WithEnemy(state.Enemy with { Statuses = StatusSet.Of((StatusKind.Slow, 2)) });
            Assert.That(TurnLoop.OmenMoveCell(slowed, 0), Is.EqualTo(-1));
            // An action that does not move the enemy.
            Assert.That(TurnLoop.OmenMoveCell(Opened(Enemies.PolearmWarped, 2), 0), Is.EqualTo(-1), "払い斬り");
        }

        // ---- §4.7 次のターンに回復する分 -------------------------------------------------------------------

        [Test]
        public void TheRecovery_IsThreeUnderTheMaximum()
        {
            BattleState state = Opened(Idle, 3);
            BattleState spent = state with { Player = state.Player with { Stamina = 4 } };
            Assert.That(TurnLoop.PreviewRecovery(spent), Is.EqualTo(Constants.StaminaRecovery));
            Assert.That(TurnLoop.PreviewRecovery(spent with { Player = spent.Player with { Stamina = 9 } }), Is.EqualTo(1), "capped at 10");
            Assert.That(TurnLoop.PreviewRecovery(state), Is.EqualTo(0), "full");
        }

        [Test]
        public void TheRecovery_CountsFatigueAndTheBonusLeftForIt()
        {
            BattleState state = Opened(Idle, 3);
            BattleState spent = state with { Player = state.Player with { Stamina = 2 } };
            BattleState tired = spent with { Player = spent.Player with { Statuses = StatusSet.Of((StatusKind.Fatigue, 1)) } };
            Assert.That(TurnLoop.PreviewRecovery(tired), Is.EqualTo(2), "疲労: −1");
            BattleState saved = spent with { Player = spent.Player with { NextTurnRecoveryBonus = 1 } };
            Assert.That(TurnLoop.PreviewRecovery(saved), Is.EqualTo(4), "温存: +1");
            BattleState heavy = spent with { Player = spent.Player with { NextTurnRecoveryBonus = -1 } };
            Assert.That(TurnLoop.PreviewRecovery(heavy), Is.EqualTo(2), "重撃: −1");
        }

        [Test]
        public void TheRecovery_MatchesWhatTheNextTurnStartGives()
        {
            BattleState state = Opened(Idle, 3);
            foreach (var (stamina, statuses, bonus) in new[]
            {
                (4, StatusSet.Empty, 0),
                (2, StatusSet.Of((StatusKind.Fatigue, 1)), 0),
                (8, StatusSet.Empty, 1),
                (5, StatusSet.Of((StatusKind.Fatigue, 2)), 2),
            })
            {
                BattleState set = state with { Player = state.Player with { Stamina = stamina, Statuses = statuses, NextTurnRecoveryBonus = bonus } };
                int preview = TurnLoop.PreviewRecovery(set);
                BattleState ended = TurnLoop.EndTurn(set, NoRng).State;
                Assert.That(TurnLoop.PreviewRecovery(ended), Is.EqualTo(preview), "the same estimate once the turn has ended");
                var begun = TurnLoop.BeginPlayerTurn(ended, NoRng);
                int recovered = begun.Events.OfType<StaminaRecovered>().First(e => e.Actor == Actor.Player).Amount;
                Assert.That(preview, Is.EqualTo(recovered), $"stamina {stamina}, bonus {bonus}");
            }
        }

        [Test]
        public void AFinishedBattle_RecoversNothing()
        {
            BattleState state = Opened(Idle, 3);
            BattleState over = state with { Player = state.Player with { Stamina = 2 }, Result = GameResult.Won };
            Assert.That(TurnLoop.PreviewRecovery(over), Is.EqualTo(0));
        }
    }
}
