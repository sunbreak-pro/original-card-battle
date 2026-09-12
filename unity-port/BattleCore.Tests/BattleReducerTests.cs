using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    public class BattleReducerTests
    {
        private static readonly IRng Rng = new FixedRng(0);

        private static CardInstance Card(CardDefId id, string instanceId) => new CardInstance(instanceId, Cards.Def(id));

        private static BattleState Base(
            int distance = 1, int playerStamina = 10, int enemyStamina = 10, int playerGuard = 0,
            int enemyGuard = 0, int playerHp = 30, int enemyHp = 38, Omen? omen = null,
            CardInstance[]? hand = null)
        {
            return new BattleState(
                Turn: 1,
                DistanceIndex: distance,
                PlayerHp: playerHp,
                PlayerMaxHp: 30,
                PlayerStamina: playerStamina,
                PlayerMaxStamina: 10,
                PlayerGuard: playerGuard,
                PendingBonusRecovery: 0,
                EnemyHp: enemyHp,
                EnemyMaxHp: 38,
                EnemyStamina: enemyStamina,
                EnemyMaxStamina: 10,
                EnemyGuard: enemyGuard,
                Omen: omen ?? Enemy.ChooseOmen(distance, enemyStamina),
                Hand: new List<CardInstance>(hand ?? new CardInstance[0]),
                DrawPile: new List<CardInstance>(),
                DiscardPile: new List<CardInstance>(),
                Log: new List<LogEntry>(),
                LogSeq: 0,
                Events: new List<BattleEvent>(),
                Result: GameResult.Ongoing,
                Init: new BattleInit());
        }

        private static BattleState Play(BattleState s, string id, int invest) =>
            BattleReducer.Reduce(s, new PlayCardAction(id, invest), Rng);

        private static BattleState End(BattleState s) => BattleReducer.Reduce(s, new EndTurnAction(), Rng);

        // ---- init ----

        [Test]
        public void InitState_UsesBattleInit()
        {
            var s = BattleReducer.InitState(Rng, new BattleInit(PlayerMaxStamina: 9, PlayerHp: 22, Floor: 2, MiasmaPercent: 32));
            Assert.That(s.PlayerMaxStamina, Is.EqualTo(9));
            Assert.That(s.PlayerStamina, Is.EqualTo(9));
            Assert.That(s.PlayerHp, Is.EqualTo(22));
            Assert.That(s.EnemyStamina, Is.EqualTo(10));
            Assert.That(s.DistanceIndex, Is.EqualTo(1));
            Assert.That(s.Omen, Is.Not.Null);
            Assert.That(s.Omen!.ActionId, Is.EqualTo(EnemyActionId.Sweep));
            Assert.That(s.Omen.TargetRange, Is.EqualTo(RangeBand.Mid));
            Assert.That(s.Hand.Count, Is.EqualTo(3));
            Assert.That(s.Hand.Count + s.DrawPile.Count, Is.EqualTo(12));
            Assert.That(s.Events.OfType<OmenDeclaredEvent>().Count(), Is.EqualTo(1));
            Assert.That(s.Init.Floor, Is.EqualTo(2));
        }

        [Test]
        public void InitState_ClampsMaxStaminaAndCarriedStamina()
        {
            var s = BattleReducer.InitState(Rng, new BattleInit(PlayerMaxStamina: 20, PlayerStamina: 4));
            Assert.That(s.PlayerMaxStamina, Is.EqualTo(14));
            Assert.That(s.PlayerStamina, Is.EqualTo(4));
        }

        // ---- play card ----

        [Test]
        public void PlayCard_InvestIsCost_AndTierPowerApplies()
        {
            var s = Base(distance: 0, hand: new[] { Card(CardDefId.Thrust, "thrust-0") });
            var n = Play(s, "thrust-0", 3);
            Assert.That(n.EnemyHp, Is.EqualTo(38 - 11));
            Assert.That(n.PlayerStamina, Is.EqualTo(7));
            Assert.That(n.Hand.Count, Is.EqualTo(0));
            Assert.That(n.DiscardPile.Count, Is.EqualTo(1));
            var hit = n.Events.OfType<AttackResolvedEvent>().Single();
            Assert.That(hit.Damage, Is.EqualTo(11));
            Assert.That(hit.Diff, Is.EqualTo(0));
        }

        [Test]
        public void PlayCard_RejectsBelowMinInvestOrUnaffordable()
        {
            var s = Base(playerStamina: 2, hand: new[] { Card(CardDefId.Thrust, "thrust-0") });
            Assert.That(ReferenceEquals(Play(s, "thrust-0", 0), s), Is.True, "T0 not allowed for minInvest 1");
            Assert.That(ReferenceEquals(Play(s, "thrust-0", 3), s), Is.True, "cannot afford 3");
            Assert.That(ReferenceEquals(Play(s, "thrust-0", 4), s), Is.True, "above max invest");
            Assert.That(ReferenceEquals(Play(s, "thrust-0", 2), s), Is.False);
        }

        [Test]
        public void PlayCard_OffRangeHalvesAndWhiffs()
        {
            var mid = Base(distance: 1, hand: new[] { Card(CardDefId.Thrust, "thrust-0") });
            Assert.That(Play(mid, "thrust-0", 2).EnemyHp, Is.EqualTo(38 - 4));  // 8 × 0.5

            var far = Base(distance: 2, hand: new[] { Card(CardDefId.Thrust, "thrust-0") });
            var n = Play(far, "thrust-0", 3);
            Assert.That(n.EnemyHp, Is.EqualTo(38 - 2));                          // 11 × 0.15 = 1.65 → 2
            Assert.That(n.Events.OfType<AttackResolvedEvent>().Single().Diff, Is.EqualTo(2));
        }

        [Test]
        public void PlayCard_DamageThenMove_ExtrasOnlyAtDiffZero()
        {
            // Lunge T3 at mid: damage at diff 1 (×0.5), then closes in, no break (diff was 1).
            var mid = Base(distance: 1, enemyStamina: 10, hand: new[] { Card(CardDefId.Lunge, "lunge-0") });
            var n = Play(mid, "lunge-0", 3);
            Assert.That(n.EnemyHp, Is.EqualTo(38 - 4));       // 7 × 0.5 = 3.5 → 4
            Assert.That(n.DistanceIndex, Is.EqualTo(0));
            Assert.That(n.EnemyStamina, Is.EqualTo(10), "break applies only at diff 0");

            // Lunge T3 at close: diff 0 → full 7 and break 1; move clamps at 0.
            var close = Base(distance: 0, enemyStamina: 10, hand: new[] { Card(CardDefId.Lunge, "lunge-0") });
            var m = Play(close, "lunge-0", 3);
            Assert.That(m.EnemyHp, Is.EqualTo(38 - 7));
            Assert.That(m.EnemyStamina, Is.EqualTo(9));
            Assert.That(m.Events.OfType<StaminaBrokenEvent>().Single().Target, Is.EqualTo(Actor.Enemy));
            Assert.That(m.Events.OfType<MovedEvent>().Single().Clamped, Is.True);
        }

        [Test]
        public void PlayCard_EnemyGuardAbsorbs()
        {
            var s = Base(distance: 0, enemyGuard: 5, hand: new[] { Card(CardDefId.Thrust, "thrust-0") });
            var n = Play(s, "thrust-0", 2); // 8 raw
            Assert.That(n.EnemyHp, Is.EqualTo(38 - 3));
            Assert.That(n.EnemyGuard, Is.EqualTo(0));
            Assert.That(n.Events.OfType<AttackResolvedEvent>().Single().GuardAbsorbed, Is.EqualTo(5));
        }

        [Test]
        public void PlayCard_MoveCardGivesGuardAndShifts()
        {
            var s = Base(distance: 1, hand: new[] { Card(CardDefId.StepOut, "step_out-0") });
            var n = Play(s, "step_out-0", 2);
            Assert.That(n.DistanceIndex, Is.EqualTo(2));
            Assert.That(n.PlayerGuard, Is.EqualTo(2));
            Assert.That(n.PlayerStamina, Is.EqualTo(8));
            Assert.That(Play(s, "step_out-0", 0).PlayerGuard, Is.EqualTo(0));
        }

        [Test]
        public void PlayCard_BraceCalmSetsBonusRecovery()
        {
            var s = Base(playerStamina: 8, hand: new[] { Card(CardDefId.Brace, "brace-0") });
            var n = Play(s, "brace-0", 2);           // remaining 6 ≥ 6 → calm
            Assert.That(n.PlayerGuard, Is.EqualTo(6));
            Assert.That(n.PendingBonusRecovery, Is.EqualTo(1));
            Assert.That(n.Events.OfType<CalmTriggeredEvent>().Count(), Is.EqualTo(1));

            var no = Play(s, "brace-0", 3);          // remaining 5 → no calm
            Assert.That(no.PendingBonusRecovery, Is.EqualTo(0));
        }

        [Test]
        public void PlayCard_HealCapsAtMax()
        {
            var s = Base(playerHp: 27, hand: new[] { Card(CardDefId.FirstAid, "first_aid-0") });
            var n = Play(s, "first_aid-0", 3);
            Assert.That(n.PlayerHp, Is.EqualTo(30));
            Assert.That(n.Events.OfType<HealedEvent>().Single().Amount, Is.EqualTo(3));
        }

        [Test]
        public void PlayCard_WinEndsBattle()
        {
            var s = Base(distance: 0, enemyHp: 5, hand: new[] { Card(CardDefId.Thrust, "thrust-0") });
            var n = Play(s, "thrust-0", 1);
            Assert.That(n.Result, Is.EqualTo(GameResult.Won));
            Assert.That(n.Events.Last(), Is.TypeOf<BattleEndedEvent>());
            Assert.That(ReferenceEquals(End(n), n), Is.True, "no actions after the battle ends");
        }

        // ---- end turn ----

        [Test]
        public void EndTurn_ReserveGuardThenEnemySweepAtMid()
        {
            // Player keeps 7 → Guard +2. Enemy at mid with 10 (+2 recovery → 10 cap) sweeps at invest 3 (power 8).
            var s = Base(distance: 1, playerStamina: 7, enemyStamina: 8, hand: new[] { Card(CardDefId.Thrust, "thrust-0") });
            var n = End(s);
            Assert.That(n.Events.OfType<ReserveGuardEvent>().First().Who, Is.EqualTo(Actor.Player));
            var hit = n.Events.OfType<AttackResolvedEvent>().Single();
            Assert.That(hit.Attacker, Is.EqualTo(Actor.Enemy));
            Assert.That(hit.Raw, Is.EqualTo(8));
            Assert.That(hit.GuardAbsorbed, Is.EqualTo(2));
            Assert.That(hit.Damage, Is.EqualTo(6));
            Assert.That(hit.TargetHpAfter, Is.EqualTo(24));
            Assert.That(n.PlayerHp, Is.EqualTo(24));
            Assert.That(n.EnemyStamina, Is.EqualTo(10 - 3 ), "8 + 2 → 10, minus invest 3");
            Assert.That(n.Turn, Is.EqualTo(2));
            Assert.That(n.PlayerGuard, Is.EqualTo(0), "player guard resets at own turn start");
            Assert.That(n.PlayerStamina, Is.EqualTo(9), "7 + mid recovery 2");
            Assert.That(n.Hand.Count, Is.EqualTo(1), "only one card existed in the piles");
            Assert.That(n.Omen, Is.Not.Null);
        }

        [Test]
        public void EndTurn_EnemyReserveGuardAndNextOmenDeclared()
        {
            var s = Base(distance: 1, playerStamina: 0, enemyStamina: 10);
            var n = End(s);
            // enemy: 10 → invest 3 → 7 ≥ 3 → reserve +2
            Assert.That(n.EnemyGuard, Is.EqualTo(2));
            Assert.That(n.Events.OfType<ReserveGuardEvent>().Any(e => e.Who == Actor.Enemy), Is.True);
            Assert.That(n.Events.OfType<OmenDeclaredEvent>().Count(), Is.EqualTo(1));
            Assert.That(n.Omen!.ActionId, Is.EqualTo(EnemyActionId.Sweep));
        }

        [Test]
        public void EndTurn_WhiffAvoidance_RepositionsInsteadOfAttacking()
        {
            // Omen declared "sweep (mid)" but the player is now far (diff 1 → still hits at 0.5).
            // Use shove (close) declared while the player is at far → diff 2 → whiff → reposition to mid.
            var omen = new Omen(EnemyActionId.Shove, RangeBand.Close);
            var s = Base(distance: 2, playerStamina: 0, enemyStamina: 10, omen: omen);
            var n = End(s);
            Assert.That(n.Events.OfType<OmenWhiffedEvent>().Count(), Is.EqualTo(1));
            Assert.That(n.Events.OfType<AttackResolvedEvent>().Any(), Is.False);
            Assert.That(n.PlayerHp, Is.EqualTo(30));
            Assert.That(n.DistanceIndex, Is.EqualTo(1));
            Assert.That(n.EnemyStamina, Is.EqualTo(10), "no invest spent on a whiff");
        }

        [Test]
        public void EndTurn_ShovePushesPlayerToMid()
        {
            var s = Base(distance: 0, playerStamina: 0, enemyStamina: 10);   // omen = shove (close)
            var n = End(s);
            var hit = n.Events.OfType<AttackResolvedEvent>().Single();
            Assert.That(hit.Raw, Is.EqualTo(3), "shove T3 = 3");
            Assert.That(n.DistanceIndex, Is.EqualTo(1));
            Assert.That(n.Events.OfType<MovedEvent>().Single().Mover, Is.EqualTo(Actor.Enemy));
        }

        [Test]
        public void EndTurn_DefeatStopsBeforeNextTurn()
        {
            var s = Base(distance: 1, playerStamina: 0, enemyStamina: 10, playerHp: 5);
            var n = End(s);
            Assert.That(n.Result, Is.EqualTo(GameResult.Lost));
            Assert.That(n.PlayerHp, Is.EqualTo(0));
            Assert.That(n.Hand.Count, Is.EqualTo(0));
            Assert.That(n.Turn, Is.EqualTo(1));
            Assert.That(n.Events.Last(), Is.TypeOf<BattleEndedEvent>());
        }

        [Test]
        public void EndTurn_CalmBonusAppliesToNextRecovery()
        {
            var s = Base(distance: 1, playerStamina: 10) with { PendingBonusRecovery = 1 };
            var n = End(s);
            // 10 → reserve keeps 10; enemy sweep 8 − guard 2 = 6 dmg; next: min(10, 10 + 2 + 1) = 10
            Assert.That(n.PlayerStamina, Is.EqualTo(10));
            Assert.That(n.PendingBonusRecovery, Is.EqualTo(0));
            Assert.That(n.Events.OfType<TurnStartedEvent>().Single().BonusRecovery, Is.EqualTo(1));
        }

        [Test]
        public void Restart_KeepsInit()
        {
            var init = new BattleInit(PlayerMaxStamina: 6, Floor: 5);
            var s = BattleReducer.InitState(Rng, init);
            var r = BattleReducer.Reduce(s, new RestartAction(), Rng);
            Assert.That(r.PlayerMaxStamina, Is.EqualTo(6));
            Assert.That(r.Init.Floor, Is.EqualTo(5));
        }

        [Test]
        public void FullBattle_WithFixedRng_Terminates()
        {
            var s = BattleReducer.InitState(Rng);
            int guard = 0;
            while (s.Result == GameResult.Ongoing && guard++ < 200)
            {
                var playable = s.Hand.FirstOrDefault(c => s.PlayerStamina >= c.Def.MinInvest);
                if (playable != null)
                {
                    int invest = Combat.ChooseInvest(playable.Def.MinInvest, s.PlayerStamina, Constants.ReserveThreshold) ?? playable.Def.MinInvest;
                    s = Play(s, playable.InstanceId, invest);
                }
                else
                {
                    s = End(s);
                }
            }
            Assert.That(s.Result, Is.Not.EqualTo(GameResult.Ongoing));
            Assert.That(guard, Is.LessThan(200));
        }
    }
}
