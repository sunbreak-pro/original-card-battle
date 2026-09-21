using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// 長柄の歪み兵 (enemy_roster_v4.md §2.1) as data: the numbers match the roster table, the tree
    /// has two branches that fall through when stamina runs short, and the omen is one step.
    /// </summary>
    public class PolearmTests
    {
        private static readonly EnemyDef Polearm = Enemies.PolearmWarped;

        // ---- The roster row ----

        [Test]
        public void Header_MatchesTheRoster()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Polearm.Id, Is.EqualTo("polearm_warped"));
                Assert.That(Polearm.Name, Is.EqualTo("長柄の歪み兵"));
                Assert.That(Polearm.MaxHp, Is.EqualTo(60));
                Assert.That(Polearm.MaxStamina, Is.EqualTo(10));
                Assert.That(Polearm.Recovery, Is.EqualTo(2));
                Assert.That(Polearm.HasPosition, Is.False);
                Assert.That(Polearm.StartPosition, Is.Null);
            });
        }

        [TestCase("sweep", "薙ぎ払い", 2, 8, 0, false, "攻撃・遠")]
        [TestCase("shove", "石突きの押し込み", 2, 5, 0, true, "攻撃・近")]
        [TestCase("reach_thrust", "穂先の突き", 1, 4, 0, false, "攻撃")]
        [TestCase("guard_up", "柄で受ける", 1, 0, 3, false, "守り")]
        public void Actions_MatchTheRosterTable(
            string id, string name, int column, int power, int guard, bool push, string omen)
        {
            var action = Polearm.Actions[id];
            Assert.Multiple(() =>
            {
                Assert.That(action.Name, Is.EqualTo(name));
                Assert.That(action.Column, Is.EqualTo(column));
                Assert.That(action.Cost, Is.EqualTo(column));
                Assert.That(action.Face.Power, Is.EqualTo(power));
                Assert.That(action.Face.Guard, Is.EqualTo(guard));
                Assert.That(action.Face.Push, Is.EqualTo(push));
                Assert.That(action.Omen.ToText(), Is.EqualTo(omen));
            });
        }

        [Test]
        public void ThereAreExactlyFourActions_AndNoneCarriesAStatus()
        {
            Assert.That(Polearm.Actions.Keys, Is.EquivalentTo(new[] { "sweep", "shove", "reach_thrust", "guard_up" }));
            Assert.That(Polearm.Actions.Values.All(a => a.Face.Status == null), Is.True);
        }

        [Test]
        public void Attributes_MatchTheRosterTable()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Polearm.Actions["sweep"].Attributes, Is.EqualTo(BattleAttribute.Attack));
                Assert.That(Polearm.Actions["shove"].Attributes,
                    Is.EqualTo(BattleAttribute.Attack | BattleAttribute.Move));
                Assert.That(Polearm.Actions["reach_thrust"].Attributes, Is.EqualTo(BattleAttribute.Attack));
                Assert.That(Polearm.Actions["guard_up"].Attributes, Is.EqualTo(BattleAttribute.Guard));
            });
        }

        [Test]
        public void Traits_MatchTheRosterTable()
        {
            var sweep = Polearm.Actions["sweep"].Trait!;
            var shove = Polearm.Actions["shove"].Trait!;
            var guardUp = Polearm.Actions["guard_up"].Trait!;

            Assert.Multiple(() =>
            {
                Assert.That(sweep, Is.EqualTo(new Trait(
                    TraitCondition.OpponentPosition, TraitEffect.PowerBonus, 3, Position.Far)));
                Assert.That(shove, Is.EqualTo(new Trait(TraitCondition.Unguarded, TraitEffect.PowerBonus, 3)));
                Assert.That(guardUp, Is.EqualTo(new Trait(
                    TraitCondition.Reserve, TraitEffect.NextTurnRecovery, 1, null, 4)));
                Assert.That(Polearm.Actions["reach_thrust"].Trait, Is.Null);
            });
        }

        [Test]
        public void TheCatalogue_FindsThePolearmById_AndRefusesAnUnknownId()
        {
            Assert.That(Enemies.ById("polearm_warped"), Is.SameAs(Polearm));
            Assert.That(Enemies.All, Has.Count.EqualTo(1));
            Assert.That(() => Enemies.ById("nobody"), Throws.InstanceOf<System.Collections.Generic.KeyNotFoundException>());
        }

        // ---- The two branches ----

        [Test]
        public void Branches_AreTheRosterOrder()
        {
            Assert.That(Polearm.BranchWhenPlayerNear, Is.EqualTo(new[] { "shove", "guard_up", "reach_thrust" }));
            Assert.That(Polearm.BranchWhenPlayerFar, Is.EqualTo(new[] { "sweep", "reach_thrust", "guard_up" }));
        }

        [Test]
        public void PlayerNear_TakesTheShove()
        {
            var action = EnemyAi.ChooseAction(Polearm, Position.Near, stamina: 10);
            Assert.That(action!.Id, Is.EqualTo("shove"));
        }

        [Test]
        public void PlayerFar_TakesTheSweep()
        {
            var action = EnemyAi.ChooseAction(Polearm, Position.Far, stamina: 10);
            Assert.That(action!.Id, Is.EqualTo("sweep"));
        }

        // ---- Falling through when stamina runs short ----

        [Test]
        public void PlayerNear_WithOneStamina_FallsToTheGuard()
        {
            // shove costs 2, so the near branch falls to its second entry.
            var action = EnemyAi.ChooseAction(Polearm, Position.Near, stamina: 1);
            Assert.That(action!.Id, Is.EqualTo("guard_up"));
        }

        [Test]
        public void PlayerFar_WithOneStamina_FallsToTheThrust()
        {
            var action = EnemyAi.ChooseAction(Polearm, Position.Far, stamina: 1);
            Assert.That(action!.Id, Is.EqualTo("reach_thrust"));
        }

        [TestCase(Position.Near)]
        [TestCase(Position.Far)]
        public void WithNoStamina_NothingFits_AndTheOmenIsARest(Position side)
        {
            Assert.That(EnemyAi.ChooseAction(Polearm, side, stamina: 0), Is.Null);

            var omen = EnemyAi.DecideOmen(Polearm, side, stamina: 0);
            Assert.That(omen.ActionId, Is.EqualTo(EnemyAi.RestActionId));
            Assert.That(omen.Label.ToText(), Is.EqualTo("休み"));
        }

        [Test]
        public void ExactlyTwoStamina_StillPaysForTheColumnTwoAction()
        {
            Assert.That(EnemyAi.ChooseAction(Polearm, Position.Far, stamina: 2)!.Id, Is.EqualTo("sweep"));
        }

        // ---- The one-step omen ----

        [Test]
        public void TheOmen_IsTheNextActionsLabel()
        {
            Assert.Multiple(() =>
            {
                Assert.That(EnemyAi.DecideOmen(Polearm, Position.Far, 10),
                    Is.EqualTo(new Omen("sweep", new OmenLabel(OmenKind.Attack, Position.Far))));
                Assert.That(EnemyAi.DecideOmen(Polearm, Position.Near, 10),
                    Is.EqualTo(new Omen("shove", new OmenLabel(OmenKind.Attack, Position.Near))));
                Assert.That(EnemyAi.DecideOmen(Polearm, Position.Far, 1).Label.ToText(), Is.EqualTo("攻撃"));
                Assert.That(EnemyAi.DecideOmen(Polearm, Position.Near, 1).Label.ToText(), Is.EqualTo("守り"));
            });
        }

        [Test]
        public void TheOmenIsACommitment_AnUnpayableOmenBecomesARest_NotACheaperAction()
        {
            var omen = EnemyAi.DecideOmen(Polearm, Position.Far, 10);

            Assert.That(EnemyAi.ActionToExecute(Polearm, omen, stamina: 2)!.Id, Is.EqualTo("sweep"));
            // Drained below the cost in between: the sweep is not swapped for the 1-cost thrust.
            Assert.That(EnemyAi.ActionToExecute(Polearm, omen, stamina: 1), Is.Null);
        }

        [Test]
        public void TheOmenIsACommitment_ItDoesNotFollowThePlayerAcrossTheLine()
        {
            // Declared against a far player; the player then steps near. The sweep still comes,
            // it just loses the +3 (see the push-and-pull test below).
            var omen = EnemyAi.DecideOmen(Polearm, Position.Far, 10);
            Assert.That(EnemyAi.ActionToExecute(Polearm, omen, 10)!.Id, Is.EqualTo("sweep"));
        }

        [Test]
        public void AnEnemyThatCarriesAPosition_IsRefused()
        {
            var elite = Polearm with { HasPosition = true, StartPosition = Position.Far };
            Assert.That(() => EnemyAi.BranchFor(elite, Position.Near), Throws.InstanceOf<System.NotSupportedException>());
        }

        // ---- The push and pull this enemy exists to teach (roster §2.1) ----

        [Test]
        public void StayingFar_EatsTheSweepAtEleven_EveryTurn()
        {
            var player = Fixtures.Combatant(position: Position.Far);

            for (int turn = 0; turn < 3; turn++)
            {
                var action = EnemyAi.ChooseAction(Polearm, player.Position!.Value, stamina: 10)!;
                Assert.That(action.Id, Is.EqualTo("sweep"));
                Assert.That(RawPower(action, player, staminaBefore: 10), Is.EqualTo(11));
            }
        }

        [Test]
        public void SteppingNear_LightensTheSweepToEight()
        {
            var player = Fixtures.Combatant(position: Position.Near);
            Assert.That(RawPower(Polearm.Actions["sweep"], player, staminaBefore: 10), Is.EqualTo(8));
        }

        [Test]
        public void SteppingNear_GetsShovedBackFar_AndTheNextOmenIsTheSweep()
        {
            var player = Fixtures.Combatant(position: Position.Near, guard: 0);

            var shove = EnemyAi.ChooseAction(Polearm, player.Position!.Value, stamina: 10)!;
            Assert.That(shove.Id, Is.EqualTo("shove"));
            Assert.That(RawPower(shove, player, staminaBefore: 10), Is.EqualTo(8), "5 + 3 against Guard 0");

            // §2.2: power is read from before the move, then the push lands.
            var pushed = player with { Position = player.Position!.Value.Opposite() };
            Assert.That(pushed.Position, Is.EqualTo(Position.Far));

            var next = EnemyAi.DecideOmen(Polearm, pushed.Position!.Value, stamina: 10);
            Assert.That(next.ActionId, Is.EqualTo("sweep"));
            Assert.That(next.Label.ToText(), Is.EqualTo("攻撃・遠"));
        }

        [Test]
        public void TheShove_IsFiveAgainstAGuard_ButStillPushes()
        {
            var player = Fixtures.Combatant(position: Position.Near, guard: 4);
            var shove = Polearm.Actions["shove"];

            int raw = RawPower(shove, player, staminaBefore: 10);
            var (damage, guardAfter, _) = Combat.ApplyGuard(raw, player.Guard);

            Assert.Multiple(() =>
            {
                Assert.That(raw, Is.EqualTo(5));
                Assert.That(damage, Is.EqualTo(1));
                Assert.That(guardAfter, Is.EqualTo(0));
                Assert.That(shove.Face.Push, Is.True, "Guard soaks the damage, not the push (§2.4)");
            });
        }

        [Test]
        public void GuardUp_LeavesARecoveryBonus_OnlyWithFourOrMoreLeft()
        {
            var guardUp = Polearm.Actions["guard_up"];

            var rich = Traits.Evaluate(guardUp.Trait, new TraitContext(StaminaAfterUse: 5 - guardUp.Cost));
            var poor = Traits.Evaluate(guardUp.Trait, new TraitContext(StaminaAfterUse: 4 - guardUp.Cost));

            Assert.That(rich.NextTurnRecoveryBonus, Is.EqualTo(1));
            Assert.That(poor.Triggered, Is.False);
        }

        [Test]
        public void Slow_DoesNotStopTheShove()
        {
            // #116: 鈍足 pins the holder's own position and the polearm has none, so the shove still
            // comes. Implemented as the canon reads, not as the roster journal reads.
            var slowed = Fixtures.Combatant(hp: 60, position: null, statuses: StatusSet.Of((StatusKind.Slow, 2)));
            Assert.That(slowed.Position, Is.Null);
            Assert.That(EnemyAi.ChooseAction(Polearm, Position.Near, stamina: 10)!.Face.Push, Is.True);
        }

        /// <summary>Trait first, then the attack face (§2.2) — the same two calls the turn loop makes.</summary>
        private static int RawPower(EnemyActionDef action, CombatantState target, int staminaBefore)
        {
            var outcome = Traits.Evaluate(action.Trait, new TraitContext(
                SelfPosition: null,
                OpponentPosition: target.Position,
                OpponentGuard: target.Guard,
                StaminaAfterUse: staminaBefore - action.Cost));
            return Combat.ComputeRawPower(action.Face.Power, outcome.PowerBonus);
        }
    }
}
