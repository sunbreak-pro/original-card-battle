// What the omen badge shows for the journal's 開示度 (Script/OmenDisclosure.cs, battle-visual-v1 §4.4,
// battle_ui_ux_v2 §7, #455): the numbers and a 状態付与 omen's statuses from 2 only, the plan from 1,
// and the kind, its icon and the hit mark at every level.
using System.Collections.Generic;
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

        /// <summary>A 状態付与 omen (鈍足) with a 状態付与 plan (出血).</summary>
        private static OmenFrame Seal()
        {
            return new OmenFrame
            {
                Visible = true, Icon = OmenIcon.Seal, KindLabel = "技", Hit = OmenHit.Lands,
                Statuses = new List<OmenStatus> { new OmenStatus { Label = "鈍足", Glyph = StatusGlyph.Slow } },
                PlanVisible = true, PlanIcon = OmenIcon.Seal, PlanKindLabel = "技",
                PlanStatuses = new List<OmenStatus> { new OmenStatus { Label = "出血", Glyph = StatusGlyph.Bleed } },
            };
        }

        [Test]
        public void AFrame_IsFullyDisclosed_UntilAWriterSaysOtherwise()
        {
            Assert.That(new OmenFrame().Disclosure, Is.EqualTo(OmenDisclosure.Full));
            Assert.That(OmenDisclosure.Full, Is.EqualTo(2));
        }

        [Test]
        public void ReadToTwo_TheNumbersAndTheStatusesStay()
        {
            OmenFrame omen = OmenDisclosure.Apply(Attack(), 2);
            Assert.That(omen.ValueText, Is.EqualTo("13"));
            Assert.That(omen.PlanValueText, Is.EqualTo("4"));
            Assert.That(omen.Disclosure, Is.EqualTo(2));

            OmenFrame seal = OmenDisclosure.Apply(Seal(), 2);
            Assert.That(seal.Statuses, Has.Count.EqualTo(1));
            Assert.That(seal.PlanStatuses, Has.Count.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(0)]
        public void BelowTwo_TheNumbersGo_AndTheKindStays(int level)
        {
            OmenFrame omen = OmenDisclosure.Apply(Attack(), level);
            Assert.That(omen.ValueText, Is.Empty);
            Assert.That(omen.PlanValueText, Is.Empty);
            Assert.That(omen.Disclosure, Is.EqualTo(level));
            Assert.That(omen.Icon, Is.EqualTo(OmenIcon.Attack), "the kind's icon stays");
            Assert.That(omen.KindLabel, Is.EqualTo("攻撃"), "the kind word stays");
            Assert.That(omen.Hit, Is.EqualTo(OmenHit.Lands));
        }

        [TestCase(1)]
        [TestCase(0)]
        public void BelowTwo_AStatusApplyingOmen_KeepsItsStamp_AndLosesItsStatuses(int level)
        {
            OmenFrame omen = OmenDisclosure.Apply(Seal(), level);
            Assert.That(omen.Icon, Is.EqualTo(OmenIcon.Seal), "§4.4: the icon and the kind word show at every level");
            Assert.That(omen.Statuses, Is.Empty);
            Assert.That(omen.PlanStatuses, Is.Empty);
        }

        [Test]
        public void ReadToOne_ThePlanStays()
        {
            OmenFrame omen = OmenDisclosure.Apply(Attack(), 1);
            Assert.That(omen.PlanVisible, Is.True);
            Assert.That(omen.PlanKindLabel, Is.EqualTo("防御"));
            Assert.That(OmenDisclosure.ShowsPlan(1), Is.True);
        }

        [Test]
        public void ReadToZero_ThePlanGoes_AndTheFirstOmensKindStays()
        {
            // battle_ui_ux_v2 §7 / §5.16 順 2: 開示度 0 は 1 手目の種別だけで、2 手目を出さない.
            OmenFrame omen = OmenDisclosure.Apply(Attack(), 0);
            Assert.That(omen.PlanVisible, Is.False);
            Assert.That(omen.Visible, Is.True);
            Assert.That(omen.KindLabel, Is.EqualTo("攻撃"));
            Assert.That(OmenDisclosure.ShowsPlan(0), Is.False);
        }

        [Test]
        public void ALevelOutsideZeroToTwo_IsHeldInside()
        {
            Assert.That(OmenDisclosure.Apply(Attack(), -1).Disclosure, Is.EqualTo(0), "−1 has no parts yet and reads as 0");
            Assert.That(OmenDisclosure.Apply(Attack(), 5).Disclosure, Is.EqualTo(2));
            Assert.That(OmenDisclosure.ShowsNumbers(5), Is.True);
            Assert.That(OmenDisclosure.ShowsNumbers(-1), Is.False);
            Assert.That(OmenDisclosure.ShowsPlan(-1), Is.False);
        }
    }
}
