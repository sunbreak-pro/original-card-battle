using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// #822 (battle_core_v4 §1): the maximum stamina is 10, more only with a special event or trait, and
    /// never past 20. The core's ways above 10 are an elite's or a boss's own maximum (roster §0: 12 / 14,
    /// read as their trait) and 息継ぎの笛, which opens a battle above the maximum up to 14 (§9.1). The
    /// exploration's rest, meal and relief items raise the maximum it hands a battle (DungeonCore,
    /// up to 14); BattleCore.Sim.Tests' CoreAgreementTests keeps that inside the battle's range.
    /// </summary>
    public class MaxStaminaPathsTests
    {
        [Test]
        public void TheMaximum_IsTenUpToTwenty_AndTheFluteStopsAtFourteen()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Constants.BaseMaxStamina, Is.EqualTo(10));
                Assert.That(Constants.MaxStaminaCeil, Is.EqualTo(20));
                Assert.That(Constants.StartStaminaCeil, Is.EqualTo(14));
                Assert.That(Combat.ClampMaxStamina(int.MaxValue), Is.EqualTo(20));
            });
        }

        [Test]
        public void OnlyElitesAndBosses_HoldAMaximumAboveTen()
        {
            Assert.Multiple(() =>
            {
                foreach (EnemyDef enemy in Enemies.All)
                {
                    Assert.That(enemy.MaxStamina, Is.InRange(Constants.MaxStaminaFloor, Constants.MaxStaminaCeil), enemy.Id);
                    if (enemy.MaxStamina > Constants.BaseMaxStamina)
                    {
                        Assert.That(enemy.Rank, Is.Not.EqualTo(EnemyRank.Normal), enemy.Id + " is a normal enemy above 10");
                    }
                }
                Assert.That(Enemies.All.Where(e => e.MaxStamina > Constants.BaseMaxStamina).Select(e => e.MaxStamina).Distinct().OrderBy(m => m),
                    Is.EqualTo(new[] { 12, 14 }), "roster §0: 精鋭 12 / ボス 14");
            });
        }

        [Test]
        public void ThePlayersMaximum_IsTen_AndNoGaugeRaisesIt()
        {
            Assert.Multiple(() =>
            {
                Assert.That(BattleSetup.Slice().PlayerMaxStamina, Is.EqualTo(Constants.BaseMaxStamina));
                for (int percent = 0; percent < 100; percent++)
                {
                    Assert.That(Chain.MaxStaminaAt(percent), Is.LessThanOrEqualTo(Constants.BaseMaxStamina), percent + "%");
                }
            });
        }

        [TestCase(0, 10)]
        [TestCase(3, 13)]
        [TestCase(6, 14)]
        public void OnlyTheFlute_OpensABattleAboveTheMaximum(int bonus, int expected)
        {
            var setup = BattleSetup.Slice() with { PlayerStartStaminaBonus = bonus };
            Assert.That(setup.StartStamina, Is.EqualTo(expected));
        }

        [Test]
        public void RecoveryAndGains_NeverPassTheMaximum()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Combat.RecoverStamina(8, 10, 3), Is.EqualTo(10));
                Assert.That(Combat.RecoverStamina(13, 10, 3), Is.EqualTo(13), "above it (the flute), nothing is added");
                Assert.That(Combat.RecoverStamina(18, 20, 5), Is.EqualTo(20));
            });
        }
    }
}
