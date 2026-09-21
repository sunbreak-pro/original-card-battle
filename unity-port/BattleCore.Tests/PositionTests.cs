using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>§7.1: position is two-valued, so one switch always lands on the other side.</summary>
    public class PositionTests
    {
        [Test]
        public void Opposite_AlwaysLandsOnTheOtherSide()
        {
            Assert.That(Position.Near.Opposite(), Is.EqualTo(Position.Far));
            Assert.That(Position.Far.Opposite(), Is.EqualTo(Position.Near));
        }

        [Test]
        public void SwitchingTwice_ComesBack()
        {
            Assert.That(Position.Near.Opposite().Opposite(), Is.EqualTo(Position.Near));
        }

        [Test]
        public void Labels_AreTheOneCharacterCards()
        {
            Assert.That(Position.Near.ToLabel(), Is.EqualTo("近"));
            Assert.That(Position.Far.ToLabel(), Is.EqualTo("遠"));
        }

        [Test]
        public void ACombatantWithoutAPosition_CarriesNull()
        {
            // The polearm holds no position (§7.1); only bosses and elites do.
            var polearm = Fixtures.Combatant(hp: 60, position: null);
            Assert.That(polearm.Position, Is.Null);
        }

        [Test]
        public void PushFlipsTheOpponent_AndIsNotReducedByGuard()
        {
            // §2.4: push is the enemy-only 相手の位置を反転する. It moves the player even through Guard.
            var shove = Fixtures.EnemyAction(
                "shove", column: 2,
                face: new Face(Power: 5, Push: true),
                attributes: BattleAttribute.Attack | BattleAttribute.Move);

            var player = Fixtures.Combatant(position: Position.Near, guard: 9);
            Assert.That(shove.Face.Push, Is.True);

            var after = player with { Position = player.Position!.Value.Opposite() };
            Assert.That(after.Position, Is.EqualTo(Position.Far));

            // The Guard still soaks the damage; it just does not stop the shove.
            var (damage, _, _) = Combat.ApplyGuard(Combat.ComputeRawPower(shove.Face.Power), player.Guard);
            Assert.That(damage, Is.EqualTo(0));
        }

        [Test]
        public void AMoveFaceFlipsTheOwnSide()
        {
            var stepBack = Fixtures.Card(
                "step_back", column: Columns.MoveCardColumn,
                face: new Face(FlipsSelfPosition: true, Guard: 4),
                attributes: BattleAttribute.Move);

            Assert.That(stepBack.Cost, Is.EqualTo(1));
            Assert.That(stepBack.Face.FlipsSelfPosition, Is.True);
        }

        [Test]
        public void ATwoBranchEnemy_ReadsThePlayerSide()
        {
            // §6.1: an enemy without a position branches on the player's side only.
            var def = PolearmShapedDef();
            Assert.Multiple(() =>
            {
                Assert.That(def.HasPosition, Is.False);
                Assert.That(def.StartPosition, Is.Null);
                Assert.That(def.BranchWhenPlayerNear, Is.Not.Empty);
                Assert.That(def.BranchWhenPlayerFar, Is.Not.Empty);
            });
        }

        /// <summary>
        /// The shape #70 fills in, not its numbers: two branches, no position, its own recovery.
        /// The id and the name are the roster's; renaming waits for #128.
        /// </summary>
        private static EnemyDef PolearmShapedDef()
        {
            var sweep = Fixtures.EnemyAction(
                "sweep", column: 2, face: new Face(Power: 8),
                omen: new OmenLabel(OmenKind.Attack, Position.Far));
            var shove = Fixtures.EnemyAction(
                "shove", column: 2, face: new Face(Power: 5, Push: true),
                omen: new OmenLabel(OmenKind.Attack, Position.Near));

            return new EnemyDef(
                "polearm_warped", "長柄の歪み兵",
                MaxHp: 60, MaxStamina: 10, Recovery: 2,
                HasPosition: false, StartPosition: null,
                BranchWhenPlayerNear: new[] { "shove" },
                BranchWhenPlayerFar: new[] { "sweep" },
                Actions: new System.Collections.Generic.Dictionary<string, EnemyActionDef>
                {
                    ["sweep"] = sweep,
                    ["shove"] = shove,
                });
        }
    }
}
