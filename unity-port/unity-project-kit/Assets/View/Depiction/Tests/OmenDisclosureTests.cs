// What the omen badge shows for the journal's 開示度 (Script/OmenDisclosure.cs, battle-visual-v1 §4.4,
// #455): the numbers from 2 only; the kind, its icon, the plan and the hit mark at every level.
using NUnit.Framework;

namespace Depiction.Tests
{
    public class OmenDisclosureTests
    {
        private static OmenFrame Attack()
        {
            return new OmenFrame
            {
                Visible = true, Icon = OmenIcon.Attack, KindLabel = "攻撃", ValueText = "13", Hit = OmenHit.Lands,
                PlanVisible = true, PlanIcon = OmenIcon.Guard, PlanKindLabel = "防御", PlanValueText = "4",
            };
        }

        [Test]
        public void AFrame_IsFullyDisclosed_UntilAWriterSaysOtherwise()
        {
            Assert.That(new OmenFrame().Disclosure, Is.EqualTo(OmenDisclosure.Full));
            Assert.That(OmenDisclosure.Full, Is.EqualTo(2));
        }

        [Test]
        public void ReadToTwo_TheNumbersStay()
        {
            OmenFrame omen = OmenDisclosure.Apply(Attack(), 2);
            Assert.That(omen.ValueText, Is.EqualTo("13"));
            Assert.That(omen.PlanValueText, Is.EqualTo("4"));
            Assert.That(omen.Disclosure, Is.EqualTo(2));
        }

        [TestCase(1)]
        [TestCase(0)]
        public void BelowTwo_OnlyTheNumbersGo(int level)
        {
            OmenFrame omen = OmenDisclosure.Apply(Attack(), level);
            Assert.That(omen.ValueText, Is.Empty);
            Assert.That(omen.PlanValueText, Is.Empty);
            Assert.That(omen.Disclosure, Is.EqualTo(level));
            Assert.That(omen.Icon, Is.EqualTo(OmenIcon.Attack), "the kind's icon stays");
            Assert.That(omen.KindLabel, Is.EqualTo("攻撃"), "the kind word stays");
            Assert.That(omen.PlanVisible, Is.True);
            Assert.That(omen.PlanKindLabel, Is.EqualTo("防御"));
            Assert.That(omen.Hit, Is.EqualTo(OmenHit.Lands));
        }

        [Test]
        public void ALevelOutsideZeroToTwo_IsHeldInside()
        {
            Assert.That(OmenDisclosure.Apply(Attack(), -1).Disclosure, Is.EqualTo(0), "−1 has no parts yet and reads as 0");
            Assert.That(OmenDisclosure.Apply(Attack(), 5).Disclosure, Is.EqualTo(2));
            Assert.That(OmenDisclosure.ShowsNumbers(5), Is.True);
            Assert.That(OmenDisclosure.ShowsNumbers(-1), Is.False);
        }
    }
}
