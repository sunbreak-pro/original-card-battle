using DungeonCore;

namespace DungeonCore.Tests
{
    /// <summary>Max stamina on the exploration side, now that the 瘴気 gauge no longer lowers it (#888).</summary>
    public class StaminaRulesTests
    {
        [Test]
        public void TheBaseIsTen()
        {
            Assert.That(StaminaRules.MaxStamina(0), Is.EqualTo(10));
            Assert.That(StaminaRules.BaseMaxStamina, Is.EqualTo(10));
            Assert.That(StaminaRules.FieldRecovery, Is.EqualTo(3));
        }

        [TestCase(1, 11)]
        [TestCase(-2, 8)]
        [TestCase(4, 14)]
        [TestCase(9, 14)]
        [TestCase(-4, 6)]
        [TestCase(-9, 6)]
        public void TemporaryModifiersClampAtFour(int temp, int expected)
        {
            Assert.That(StaminaRules.MaxStamina(temp), Is.EqualTo(expected));
        }

        [Test]
        public void MaxStaminaStaysInsideItsBand()
        {
            for (int temp = -20; temp <= 20; temp++)
            {
                Assert.That(StaminaRules.MaxStamina(temp), Is.InRange(StaminaRules.MaxStaminaFloor, StaminaRules.MaxStaminaCeil));
                Assert.That(StaminaRules.ClampTemp(temp), Is.InRange(-StaminaRules.TempModClamp, StaminaRules.TempModClamp));
            }
        }
    }
}
