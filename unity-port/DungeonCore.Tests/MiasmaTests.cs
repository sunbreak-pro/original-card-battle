using DungeonCore;

namespace DungeonCore.Tests
{
    public class MiasmaTests
    {
        [TestCase(0, 0)]
        [TestCase(19, 0)]
        [TestCase(20, 1)]
        [TestCase(39, 1)]
        [TestCase(40, 2)]
        [TestCase(59, 2)]
        [TestCase(60, 3)]
        [TestCase(80, 4)]
        [TestCase(99, 4)]
        [TestCase(100, 4)]
        public void OnePointOfMaxStaminaPerFullTwentyPercent(int percent, int expected)
        {
            Assert.That(Miasma.MaxStaminaPenalty(percent), Is.EqualTo(expected));
        }

        [Test]
        public void EightyPercentIsMaxStaminaSix()
        {
            // The condition battle_core_v4.md §13 sets a win-rate target for.
            Assert.That(Miasma.MaxStamina(0, 80), Is.EqualTo(6));
        }

        [Test]
        public void TemporaryModifiersClampAtFour()
        {
            Assert.That(Miasma.MaxStamina(9, 0), Is.EqualTo(14));
            Assert.That(Miasma.MaxStamina(-9, 0), Is.EqualTo(6));
            Assert.That(Miasma.MaxStamina(-9, 80), Is.EqualTo(3));
        }

        [Test]
        public void MaxStaminaStaysInsideItsBand()
        {
            for (int temp = -8; temp <= 8; temp++)
            {
                for (int percent = 0; percent <= 100; percent++)
                {
                    Assert.That(Miasma.MaxStamina(temp, percent), Is.InRange(3, 14));
                }
            }
        }

        [Test]
        public void ReliefNeverMakesATimeUnitFree()
        {
            // seven_layers_v4.md §3.2: density floors at 1 so the shallow layers keep their price.
            Assert.That(Miasma.EffectiveDensity(1, 1), Is.EqualTo(1));
            Assert.That(Miasma.EffectiveDensity(1, 5), Is.EqualTo(1));
            Assert.That(Miasma.EffectiveDensity(7, 1), Is.EqualTo(6));
            Assert.That(Miasma.EffectiveDensity(3, 0), Is.EqualTo(3));
        }

        [Test]
        public void TheGaugeStopsAtOneHundred()
        {
            Assert.That(Miasma.Accumulate(98, 7), Is.EqualTo(100));
            Assert.That(Miasma.IsLethal(Miasma.Accumulate(98, 7)), Is.True);
            Assert.That(Miasma.IsLethal(99), Is.False);
        }

        [Test]
        public void TheGaugeStopsAtZeroWhenRelieved()
        {
            Assert.That(Miasma.Relieve(5, 10), Is.Zero);
            Assert.That(Miasma.Relieve(35, 10), Is.EqualTo(25));
        }
    }
}
