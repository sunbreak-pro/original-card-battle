using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// 長柄の歪み兵 (enemy_roster_v4.md §2.1) as data: the numbers match the roster table, the tree
    /// has three branches (§6.1) that fall through when stamina runs short, and the omen is one step.
    /// The reaches and the gap thresholds are the v4.3 provisional values (#162) until #160 lands.
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
                Assert.That(Polearm.Size, Is.EqualTo(1));
            });
        }

        [TestCase("sweep", "薙ぎ払い", 2, 8, 0, 0, 0, "1〜2", "攻撃・1〜2")]
        [TestCase("shove", "石突きの押し込み", 2, 5, 0, 2, 0, "0", "攻撃・0")]
        [TestCase("reach_thrust", "穂先の突き", 1, 4, 0, 0, 0, "0〜2", "攻撃・0〜2")]
        [TestCase("guard_up", "柄で受ける", 1, 0, 3, 0, 0, "", "守り")]
        [TestCase("step_forward", "踏み込み", 1, 0, 2, 0, 1, "", "動")]
        public void Actions_MatchTheRosterTable(
            string id, string name, int column, int power, int guard, int push, int move, string reach, string omen)
        {
            var action = Polearm.Actions[id];
            var label = EnemyAi.LabelOf(action);
            Assert.Multiple(() =>
            {
                Assert.That(action.Name, Is.EqualTo(name));
                Assert.That(action.Column, Is.EqualTo(column));
                Assert.That(action.Cost, Is.EqualTo(column));
                Assert.That(action.Face.Power, Is.EqualTo(power));
                Assert.That(action.Face.Guard, Is.EqualTo(guard));
                Assert.That(action.Face.Push, Is.EqualTo(push));
                Assert.That(action.Face.Move, Is.EqualTo(move));
                Assert.That(label.Reach == null ? "" : label.Reach.ToText(), Is.EqualTo(reach));
                Assert.That(label.ToText(), Is.EqualTo(omen));
            });
        }

        [Test]
        public void ThereAreExactlyFiveActions_AndNoneCarriesAStatus()
        {
            Assert.That(Polearm.Actions.Keys, Is.EquivalentTo(new[] { "sweep", "shove", "reach_thrust", "guard_up", "step_forward" }));
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
                Assert.That(Polearm.Actions["step_forward"].Attributes, Is.EqualTo(BattleAttribute.Move | BattleAttribute.Guard));
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
                Assert.That(sweep, Is.EqualTo(new Trait(TraitCondition.GapAtLeast, TraitEffect.PowerBonus, 3, Threshold: 2)));
                Assert.That(shove, Is.EqualTo(new Trait(TraitCondition.Unguarded, TraitEffect.PowerBonus, 3)));
                Assert.That(guardUp, Is.EqualTo(new Trait(TraitCondition.Reserve, TraitEffect.NextTurnRecovery, 1, Threshold: 4)));
                Assert.That(Polearm.Actions["reach_thrust"].Trait, Is.Null);
                Assert.That(Polearm.Actions["step_forward"].Trait, Is.Null);
            });
        }

        [Test]
        public void TheSelfActions_ReadNoReach()
        {
            Assert.That(Polearm.Actions["guard_up"].Targets, Is.EqualTo(TargetKind.Self));
            Assert.That(Polearm.Actions["step_forward"].Targets, Is.EqualTo(TargetKind.Self));
            Assert.That(EnemyAi.IsOpponentDirected(
                Polearm.Actions["step_forward"].Attributes, Polearm.Actions["step_forward"].Face, TargetKind.Self), Is.False);
        }

        [Test]
        public void TheCatalogue_FindsThePolearmById_AndRefusesAnUnknownId()
        {
            Assert.That(Enemies.ById("polearm_warped"), Is.SameAs(Polearm));
            Assert.That(Enemies.All, Has.Count.EqualTo(1));
            Assert.That(() => Enemies.ById("nobody"), Throws.InstanceOf<System.Collections.Generic.KeyNotFoundException>());
        }

        // ---- The three branches ----

        [Test]
        public void Branches_AreTheRosterOrder()
        {
            Assert.That(Polearm.BranchAtGapZero, Is.EqualTo(new[] { "shove", "guard_up", "reach_thrust" }));
            Assert.That(Polearm.BranchAtGapOneToTwo, Is.EqualTo(new[] { "sweep", "reach_thrust", "guard_up" }));
            Assert.That(Polearm.BranchAtGapThreePlus, Is.EqualTo(new[] { "step_forward", "guard_up" }));
        }

        [TestCase(0, "shove")]
        [TestCase(1, "sweep")]
        [TestCase(2, "sweep")]
        [TestCase(3, "step_forward")]
        [TestCase(5, "step_forward")]
        public void TheBranch_FollowsTheGapBand(int gap, string first)
        {
            Assert.That(EnemyAi.ChooseAction(Polearm, gap, stamina: 10)!.Id, Is.EqualTo(first));
        }

        // ---- Falling through when stamina runs short ----

        [Test]
        public void Adjacent_WithOneStamina_FallsToTheGuard()
        {
            // shove costs 2, so the gap-0 branch falls to its second entry.
            var action = EnemyAi.ChooseAction(Polearm, 0, stamina: 1);
            Assert.That(action!.Id, Is.EqualTo("guard_up"));
        }

        [Test]
        public void AtGapTwo_WithOneStamina_FallsToTheThrust()
        {
            var action = EnemyAi.ChooseAction(Polearm, 2, stamina: 1);
            Assert.That(action!.Id, Is.EqualTo("reach_thrust"));
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(4)]
        public void WithNoStamina_NothingFits_AndTheOmenIsARest(int gap)
        {
            Assert.That(EnemyAi.ChooseAction(Polearm, gap, stamina: 0), Is.Null);

            var omen = EnemyAi.DecideOmen(Polearm, gap, stamina: 0);
            Assert.That(omen.ActionId, Is.EqualTo(EnemyAi.RestActionId));
            Assert.That(omen.Label.ToText(), Is.EqualTo("休み"));
        }

        [Test]
        public void ExactlyTwoStamina_StillPaysForTheColumnTwoAction()
        {
            Assert.That(EnemyAi.ChooseAction(Polearm, 2, stamina: 2)!.Id, Is.EqualTo("sweep"));
        }

        // ---- The one-step omen ----

        [Test]
        public void TheOmen_IsTheNextActionsLabel()
        {
            Assert.Multiple(() =>
            {
                Assert.That(EnemyAi.DecideOmen(Polearm, 2, 10),
                    Is.EqualTo(new Omen("sweep", new OmenLabel(OmenKind.Attack, new Reach(1, 2)))));
                Assert.That(EnemyAi.DecideOmen(Polearm, 0, 10),
                    Is.EqualTo(new Omen("shove", new OmenLabel(OmenKind.Attack, Reach.Only(0)))));
                Assert.That(EnemyAi.DecideOmen(Polearm, 2, 1).Label.ToText(), Is.EqualTo("攻撃・0〜2"));
                Assert.That(EnemyAi.DecideOmen(Polearm, 0, 1).Label.ToText(), Is.EqualTo("守り"));
                Assert.That(EnemyAi.DecideOmen(Polearm, 3, 10).Label.ToText(), Is.EqualTo("動"));
            });
        }

        [Test]
        public void TheAimedCells_AreCountedFromTheEnemysNearEdge()
        {
            // The polearm on cell 5: the sweep (1〜2) aims at cells 2 and 3, the shove at cell 4.
            var enemy = Fixtures.Combatant(hp: 60, cell: 5);
            Assert.That(EnemyAi.TargetCells(EnemyAi.LabelOf(Polearm.Actions["sweep"]), enemy), Is.EqualTo(new[] { 2, 3 }));
            Assert.That(EnemyAi.TargetCells(EnemyAi.LabelOf(Polearm.Actions["shove"]), enemy), Is.EqualTo(new[] { 4 }));
            Assert.That(EnemyAi.TargetCells(EnemyAi.LabelOf(Polearm.Actions["guard_up"]), enemy), Is.Empty);
            // On cell 3 the sweep's far cell would be 0: off the line, so only cell 1 remains.
            Assert.That(EnemyAi.TargetCells(EnemyAi.LabelOf(Polearm.Actions["sweep"]), enemy with { Cell = 3 }), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void TheOmenIsACommitment_AnUnpayableOmenBecomesARest_NotACheaperAction()
        {
            var omen = EnemyAi.DecideOmen(Polearm, 2, 10);

            Assert.That(EnemyAi.ActionToExecute(Polearm, omen, stamina: 2)!.Id, Is.EqualTo("sweep"));
            // Drained below the cost in between: the sweep is not swapped for the 1-cost thrust.
            Assert.That(EnemyAi.ActionToExecute(Polearm, omen, stamina: 1), Is.Null);
        }

        [Test]
        public void TheOmenIsACommitment_ItDoesNotFollowThePlayerAcrossTheLine()
        {
            // Declared at gap 2; the player then steps in to 0. The sweep still comes — and whiffs
            // there (§6), which the turn loop tests cover.
            var omen = EnemyAi.DecideOmen(Polearm, 2, 10);
            Assert.That(EnemyAi.ActionToExecute(Polearm, omen, 10)!.Id, Is.EqualTo("sweep"));
        }

        [Test]
        public void ANegativeGap_IsRefused()
        {
            Assert.That(() => EnemyAi.BranchFor(Polearm, -1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }

        // ---- The push and pull this enemy exists to teach (roster §2.1) ----

        [Test]
        public void StayingAtGapTwo_EatsTheSweepAtEleven_EveryTurn()
        {
            for (int turn = 0; turn < 3; turn++)
            {
                var action = EnemyAi.ChooseAction(Polearm, 2, stamina: 10)!;
                Assert.That(action.Id, Is.EqualTo("sweep"));
                Assert.That(RawPower(action, gap: 2, guard: 0, staminaBefore: 10), Is.EqualTo(11));
            }
        }

        [Test]
        public void SteppingInToGapOne_LightensTheSweepToEight()
        {
            Assert.That(RawPower(Polearm.Actions["sweep"], gap: 1, guard: 0, staminaBefore: 10), Is.EqualTo(8));
        }

        [Test]
        public void SteppingAdjacent_GetsShovedBackTwo_AndTheNextOmenIsTheSweep()
        {
            var shove = EnemyAi.ChooseAction(Polearm, 0, stamina: 10)!;
            Assert.That(shove.Id, Is.EqualTo("shove"));
            Assert.That(RawPower(shove, gap: 0, guard: 0, staminaBefore: 10), Is.EqualTo(8), "5 + 3 against Guard 0");
            Assert.That(shove.Face.Push, Is.EqualTo(2));

            // After a two-cell push the gap is 2, and the next omen is the sweep.
            var next = EnemyAi.DecideOmen(Polearm, 2, stamina: 10);
            Assert.That(next.ActionId, Is.EqualTo("sweep"));
            Assert.That(next.Label.ToText(), Is.EqualTo("攻撃・1〜2"));
        }

        [Test]
        public void TheShove_IsFiveAgainstAGuard_ButStillPushes()
        {
            var shove = Polearm.Actions["shove"];

            int raw = RawPower(shove, gap: 0, guard: 4, staminaBefore: 10);
            var (damage, guardAfter, _) = Combat.ApplyGuard(raw, 4);

            Assert.Multiple(() =>
            {
                Assert.That(raw, Is.EqualTo(5));
                Assert.That(damage, Is.EqualTo(1));
                Assert.That(guardAfter, Is.EqualTo(0));
                Assert.That(shove.Face.Push, Is.EqualTo(2), "Guard soaks the damage, not the push (§2.4)");
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
        public void Slow_ShortensTheShove_ToOneCell()
        {
            // §5 (v4.3): 鈍足 on the polearm takes one cell off the push it lands (§21.3).
            var slowed = StatusSet.Of((StatusKind.Slow, 2));
            Assert.That(Combat.CellsAfterSlow(Polearm.Actions["shove"].Face.Push, slowed), Is.EqualTo(1));
            Assert.That(Combat.CellsAfterSlow(Polearm.Actions["step_forward"].Face.Move, slowed), Is.EqualTo(0));
        }

        /// <summary>Trait first, then the attack face (§2.2) — the same two calls the turn loop makes.</summary>
        private static int RawPower(EnemyActionDef action, int gap, int guard, int staminaBefore)
        {
            var outcome = Traits.Evaluate(action.Trait, new TraitContext(
                Gap: gap,
                OpponentGuard: guard,
                StaminaAfterUse: staminaBefore - action.Cost));
            return Combat.ComputeRawPower(action.Face.Power, outcome.PowerBonus);
        }
    }
}
