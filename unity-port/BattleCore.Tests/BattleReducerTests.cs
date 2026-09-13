using System.Collections.Generic;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    public class BattleReducerTests
    {
        private static readonly IRng Rng = new FixedRng(0);

        private static PrototypeCard Card(
            CardDefId defId, string name, CardType type, int cost,
            RangeBand? effRange, int basePower, int shift, int guard,
            string instanceId)
        {
            return new PrototypeCard(instanceId, defId, name, type, cost, effRange, basePower, shift, guard, "");
        }

        private static BattleState BaseState()
        {
            return new BattleState(
                Turn: 1,
                DistanceIndex: 1,
                PlayerHp: 30,
                PlayerStamina: 20,
                PlayerGuard: 0,
                EnemyHp: 38,
                EnemyStamina: 20,
                Hand: new List<PrototypeCard>(),
                DrawPile: new List<PrototypeCard>(),
                DiscardPile: new List<PrototypeCard>(),
                Log: new List<LogEntry>(),
                LogSeq: 0,
                Result: GameResult.Ongoing);
        }

        // ---- initState ----

        [Test]
        public void InitState_SetsUpFreshBattle()
        {
            var s = BattleReducer.InitState(Rng);
            Assert.That(s.Turn, Is.EqualTo(1));
            Assert.That(s.DistanceIndex, Is.EqualTo(1));
            Assert.That(s.PlayerHp, Is.EqualTo(30));
            Assert.That(s.EnemyHp, Is.EqualTo(38));
            Assert.That(s.PlayerStamina, Is.EqualTo(20));
            Assert.That(s.EnemyStamina, Is.EqualTo(20));
            Assert.That(s.Result, Is.EqualTo(GameResult.Ongoing));
            Assert.That(s.Hand.Count, Is.EqualTo(3));
            Assert.That(s.DrawPile.Count, Is.EqualTo(9));
            Assert.That(s.Hand.Count + s.DrawPile.Count + s.DiscardPile.Count, Is.EqualTo(12));
            Assert.That(s.Log.Count, Is.GreaterThan(0));
        }

        // ---- chooseEnemyAction ----

        [Test]
        public void ChooseEnemyAction_CloseRange()
        {
            Assert.That(Enemy.ChooseEnemyAction(0, 20)!.Id, Is.EqualTo(EnemyActionId.Shove));
            Assert.That(Enemy.ChooseEnemyAction(0, 2)!.Id, Is.EqualTo(EnemyActionId.Reposition));
            Assert.That(Enemy.ChooseEnemyAction(0, 1), Is.Null);
        }

        [Test]
        public void ChooseEnemyAction_MidRange()
        {
            Assert.That(Enemy.ChooseEnemyAction(1, 20)!.Id, Is.EqualTo(EnemyActionId.Sweep));
            Assert.That(Enemy.ChooseEnemyAction(1, 4)!.Id, Is.EqualTo(EnemyActionId.ReachThrust));
            Assert.That(Enemy.ChooseEnemyAction(1, 3), Is.Null);
        }

        [Test]
        public void ChooseEnemyAction_FarRange()
        {
            Assert.That(Enemy.ChooseEnemyAction(2, 20)!.Id, Is.EqualTo(EnemyActionId.ReachThrust));
            Assert.That(Enemy.ChooseEnemyAction(2, 3)!.Id, Is.EqualTo(EnemyActionId.Reposition));
            Assert.That(Enemy.ChooseEnemyAction(2, 1), Is.Null);
        }

        // ---- resolveEnemyTurn ----

        [Test]
        public void ResolveEnemyTurn_SweepAtMid()
        {
            var o = Enemy.ResolveEnemyTurn(1, 20, 0);
            Assert.That(o.Action!.Id, Is.EqualTo(EnemyActionId.Sweep));
            Assert.That(o.Damage, Is.EqualTo(8));
            Assert.That(o.NewDistanceIndex, Is.EqualTo(1));
            Assert.That(o.StaminaSpent, Is.EqualTo(5));
        }

        [Test]
        public void ResolveEnemyTurn_ShoveAtClose()
        {
            var o = Enemy.ResolveEnemyTurn(0, 20, 0);
            Assert.That(o.Action!.Id, Is.EqualTo(EnemyActionId.Shove));
            Assert.That(o.Damage, Is.EqualTo(2));
            Assert.That(o.NewDistanceIndex, Is.EqualTo(1));
        }

        [Test]
        public void ResolveEnemyTurn_GuardAbsorb()
        {
            var o = Enemy.ResolveEnemyTurn(1, 20, 6);
            Assert.That(o.RawDamage, Is.EqualTo(8));
            Assert.That(o.Damage, Is.EqualTo(2));
            Assert.That(o.NewGuard, Is.EqualTo(0));
        }

        [Test]
        public void ResolveEnemyTurn_Rest()
        {
            var o = Enemy.ResolveEnemyTurn(0, 1, 0);
            Assert.That(o.Action, Is.Null);
            Assert.That(o.Damage, Is.EqualTo(0));
            Assert.That(o.NewDistanceIndex, Is.EqualTo(0));
        }

        // ---- PLAY_CARD ----

        [Test]
        public void PlayCard_AttackDealsDamageAndDiscards()
        {
            var thrust = Card(CardDefId.Thrust, "突き", CardType.Attack, 4, RangeBand.Close, 9, 0, 0, "thrust-x");
            var prev = BaseState() with
            {
                DistanceIndex = 0,
                Hand = new List<PrototypeCard> { thrust },
                PlayerStamina = 20,
                EnemyHp = 42,
            };
            var next = BattleReducer.Reduce(prev, new PlayCardAction("thrust-x"), Rng);
            Assert.That(next.EnemyHp, Is.EqualTo(33));
            Assert.That(next.PlayerStamina, Is.EqualTo(16));
            Assert.That(next.Hand, Is.Empty);
            Assert.That(next.DiscardPile, Does.Contain(thrust));
        }

        [Test]
        public void PlayCard_MoveShiftsDistance()
        {
            var stepIn = Card(CardDefId.StepIn, "足捌き・前", CardType.Move, 2, null, 0, -1, 0, "step_in-x");
            var prev = BaseState() with
            {
                DistanceIndex = 1,
                Hand = new List<PrototypeCard> { stepIn },
            };
            var next = BattleReducer.Reduce(prev, new PlayCardAction("step_in-x"), Rng);
            Assert.That(next.DistanceIndex, Is.EqualTo(0));
        }

        [Test]
        public void PlayCard_GuardAddsGuard()
        {
            var brace = Card(CardDefId.Brace, "呼吸を整える", CardType.Guard, 2, null, 0, 0, 6, "brace-x");
            var prev = BaseState() with { Hand = new List<PrototypeCard> { brace } };
            var next = BattleReducer.Reduce(prev, new PlayCardAction("brace-x"), Rng);
            Assert.That(next.PlayerGuard, Is.EqualTo(6));
        }

        [Test]
        public void PlayCard_UnknownInstanceReturnsSameState()
        {
            var prev = BaseState();
            var next = BattleReducer.Reduce(prev, new PlayCardAction("nope"), Rng);
            Assert.That(next, Is.SameAs(prev));
        }

        [Test]
        public void PlayCard_InsufficientStaminaReturnsSameState()
        {
            var thrust = Card(CardDefId.Thrust, "突き", CardType.Attack, 4, RangeBand.Close, 9, 0, 0, "thrust-x");
            var prev = BaseState() with
            {
                Hand = new List<PrototypeCard> { thrust },
                PlayerStamina = 3,
            };
            var next = BattleReducer.Reduce(prev, new PlayCardAction("thrust-x"), Rng);
            Assert.That(next, Is.SameAs(prev));
        }

        [Test]
        public void PlayCard_LethalHitWins()
        {
            var thrust = Card(CardDefId.Thrust, "突き", CardType.Attack, 4, RangeBand.Close, 9, 0, 0, "thrust-x");
            var prev = BaseState() with
            {
                DistanceIndex = 0,
                Hand = new List<PrototypeCard> { thrust },
                EnemyHp = 5,
            };
            var next = BattleReducer.Reduce(prev, new PlayCardAction("thrust-x"), Rng);
            Assert.That(next.EnemyHp, Is.EqualTo(0));
            Assert.That(next.Result, Is.EqualTo(GameResult.Won));
        }

        [Test]
        public void PlayCard_WhenWonReturnsSameState()
        {
            var thrust = Card(CardDefId.Thrust, "突き", CardType.Attack, 4, RangeBand.Close, 9, 0, 0, "thrust-x");
            var prev = BaseState() with
            {
                Hand = new List<PrototypeCard> { thrust },
                Result = GameResult.Won,
            };
            var next = BattleReducer.Reduce(prev, new PlayCardAction("thrust-x"), Rng);
            Assert.That(next, Is.SameAs(prev));
        }

        // ---- END_TURN ----

        [Test]
        public void EndTurn_AtMidTakesSweepDamage()
        {
            var prev = BaseState() with
            {
                DistanceIndex = 1,
                EnemyStamina = 20,
                PlayerHp = 30,
                PlayerStamina = 10,
                PlayerGuard = 0,
                Hand = new List<PrototypeCard>(),
            };
            var next = BattleReducer.Reduce(prev, new EndTurnAction(), Rng);
            Assert.That(next.PlayerHp, Is.EqualTo(22));
            Assert.That(next.EnemyStamina, Is.EqualTo(15));
            Assert.That(next.DistanceIndex, Is.EqualTo(1));
            Assert.That(next.PlayerStamina, Is.EqualTo(12));
            Assert.That(next.Turn, Is.EqualTo(2));
            Assert.That(next.Result, Is.EqualTo(GameResult.Ongoing));
        }

        [Test]
        public void EndTurn_AtCloseGetsShovedToMid()
        {
            var prev = BaseState() with
            {
                DistanceIndex = 0,
                EnemyStamina = 20,
                PlayerHp = 30,
                PlayerStamina = 10,
                PlayerGuard = 0,
                Hand = new List<PrototypeCard>(),
            };
            var next = BattleReducer.Reduce(prev, new EndTurnAction(), Rng);
            Assert.That(next.DistanceIndex, Is.EqualTo(1));
            Assert.That(next.PlayerHp, Is.EqualTo(28));
            Assert.That(next.EnemyStamina, Is.EqualTo(17));
        }

        [Test]
        public void EndTurn_GuardReducesDamageThenClears()
        {
            var prev = BaseState() with
            {
                DistanceIndex = 1,
                EnemyStamina = 20,
                PlayerHp = 30,
                PlayerStamina = 10,
                PlayerGuard = 6,
                Hand = new List<PrototypeCard>(),
            };
            var next = BattleReducer.Reduce(prev, new EndTurnAction(), Rng);
            Assert.That(next.PlayerHp, Is.EqualTo(28));
            Assert.That(next.PlayerGuard, Is.EqualTo(0));
        }

        [Test]
        public void EndTurn_LethalDamageLoses()
        {
            var prev = BaseState() with
            {
                DistanceIndex = 1,
                EnemyStamina = 20,
                PlayerHp = 5,
                PlayerStamina = 10,
                PlayerGuard = 0,
                Hand = new List<PrototypeCard>(),
            };
            var next = BattleReducer.Reduce(prev, new EndTurnAction(), Rng);
            Assert.That(next.PlayerHp, Is.EqualTo(0));
            Assert.That(next.Result, Is.EqualTo(GameResult.Lost));
        }

        [Test]
        public void EndTurn_WhenLostReturnsSameState()
        {
            var prev = BaseState() with { Result = GameResult.Lost };
            var next = BattleReducer.Reduce(prev, new EndTurnAction(), Rng);
            Assert.That(next, Is.SameAs(prev));
        }

        // ---- RESTART ----

        [Test]
        public void Restart_ResetsBattle()
        {
            var prev = BaseState() with
            {
                PlayerHp = 1,
                EnemyHp = 1,
                Turn = 9,
                Result = GameResult.Lost,
            };
            var next = BattleReducer.Reduce(prev, new RestartAction(), Rng);
            Assert.That(next.PlayerHp, Is.EqualTo(30));
            Assert.That(next.EnemyHp, Is.EqualTo(38));
            Assert.That(next.Turn, Is.EqualTo(1));
            Assert.That(next.Result, Is.EqualTo(GameResult.Ongoing));
        }
    }
}
