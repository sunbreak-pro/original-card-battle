// The numbers that rise off a figure when it is hit (Script/FloatNumberSpec.cs, battle-visual-v1
// §5.5, battle_ui_ux_v2 §5.0 / §5.6, #455): their size and colour, the plate under them, the path
// from the chest to the head, and how a Guard number and an HP number of one blow stand as a pair.
using System.Collections.Generic;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class FloatNumberSpecTests
    {
        [Test]
        public void GuardLost_IsTheShieldInTheGuardColourAt34Px()
        {
            Assert.That(FloatNumberSpec.FontPx(FloatNumberKind.GuardLoss), Is.EqualTo(34));
            Assert.That(FloatNumberSpec.IconPx(FloatNumberKind.GuardLoss), Is.EqualTo(34));
            Assert.That(FloatNumberSpec.ColorHex(FloatNumberKind.GuardLoss), Is.EqualTo("#8fc4d8"));
            Assert.That(FloatNumberSpec.LeadsWithShield(FloatNumberKind.GuardLoss), Is.True);
        }

        [TestCase(FloatNumberKind.HpLoss)]
        [TestCase(FloatNumberKind.HpGain)]
        public void HpLostOrWonBack_IsTheHeartInTheHpInkAt48Px(FloatNumberKind kind)
        {
            Assert.That(FloatNumberSpec.FontPx(kind), Is.EqualTo(48));
            Assert.That(FloatNumberSpec.IconPx(kind), Is.EqualTo(48));
            Assert.That(FloatNumberSpec.ColorHex(kind), Is.EqualTo("#e36a5e"));
            Assert.That(FloatNumberSpec.LeadsWithShield(kind), Is.False);
        }

        [Test]
        public void ThePlate_IsBlack80_AndTheCauseIs16PxInTheBodyInk()
        {
            Assert.That(FloatNumberSpec.PlateAlpha, Is.EqualTo(0.8f));
            Assert.That(FloatNumberSpec.LabelFontPx, Is.EqualTo(16));
            Assert.That(FloatNumberSpec.LabelInkHex, Is.EqualTo("#ecebf2"));
        }

        [TestCase(FloatNumberKind.GuardLoss, 3, "−3")]
        [TestCase(FloatNumberKind.HpLoss, 12, "−12")]
        [TestCase(FloatNumberKind.HpLoss, -4, "−4")]
        [TestCase(FloatNumberKind.HpGain, 6, "+6")]
        public void TheNumber_CarriesItsSignOnceAndTheScriptsAmount(FloatNumberKind kind, int amount, string expected)
        {
            Assert.That(FloatNumberSpec.NumberText(kind, amount), Is.EqualTo(expected));
        }

        [Test]
        public void AnHpChange_IsALossOrAGainByItsSign()
        {
            Assert.That(FloatNumberSpec.OfHpChange(-2), Is.EqualTo(FloatNumberKind.HpLoss));
            Assert.That(FloatNumberSpec.OfHpChange(4), Is.EqualTo(FloatNumberKind.HpGain));
        }

        [Test]
        public void TheNumber_RisesFromTheChestToTheHead()
        {
            // battle_ui_ux_v2 §5.0: feet on y 570 and a 226 px figure give y 423 to y 380 (y down).
            const float feetY = 570f;
            Assert.That(feetY - FloatNumberSpec.StartAbove(226f), Is.EqualTo(423f).Within(0.5f));
            Assert.That(feetY - FloatNumberSpec.EndAbove(226f), Is.EqualTo(380f).Within(0.5f));
            // The 48 px number's top stops at y 356, under the omen's bottom edge (y 330).
            Assert.That(feetY - FloatNumberSpec.EndAbove(226f) - FloatNumberSpec.HpFontPx / 2f, Is.EqualTo(356f).Within(0.5f));
            // The elite on two cells (264 px): its top, y 324, stays under its omen's bottom edge, y 292.
            Assert.That(feetY - FloatNumberSpec.EndAbove(264f) - FloatNumberSpec.HpFontPx / 2f, Is.GreaterThan(292f));
        }

        [Test]
        public void ThePlate_HoldsTheIconTheNumberAndTheLabelInARow()
        {
            FloatPlateLayout bare = FloatNumberSpec.Layout(FloatNumberKind.HpLoss, 60f, 0f);
            Assert.That(bare.IconCentre, Is.EqualTo(12f + 24f));
            Assert.That(bare.NumberCentre, Is.EqualTo(12f + 48f + 8f + 30f));
            Assert.That(bare.LabelCentre, Is.EqualTo(0f));
            Assert.That(bare.Width, Is.EqualTo(12f + 48f + 8f + 60f + 12f));
            Assert.That(bare.Height, Is.EqualTo(48f + 8f));

            FloatPlateLayout labelled = FloatNumberSpec.Layout(FloatNumberKind.HpLoss, 60f, 32f);
            Assert.That(labelled.LabelCentre, Is.EqualTo(bare.Width - 12f + 8f + 16f));
            Assert.That(labelled.Width, Is.EqualTo(bare.Width + 8f + 32f));

            FloatPlateLayout guard = FloatNumberSpec.Layout(FloatNumberKind.GuardLoss, 40f, 0f);
            Assert.That(guard.Height, Is.EqualTo(34f + 8f));
        }

        [Test]
        public void APair_StandsGuardLeftAndHpRight_24PxApart_CentredOnTheFigure()
        {
            float guardWidth = FloatNumberSpec.Layout(FloatNumberKind.GuardLoss, 40f, 0f).Width;
            float hpWidth = FloatNumberSpec.Layout(FloatNumberKind.HpLoss, 60f, 0f).Width;
            float guardX = FloatNumberSpec.CentreX(FloatNumberKind.GuardLoss, true, guardWidth);
            float hpX = FloatNumberSpec.CentreX(FloatNumberKind.HpLoss, true, hpWidth);
            float guardRight = guardX + guardWidth / 2f;
            float hpLeft = hpX - hpWidth / 2f;
            Assert.That(guardX, Is.LessThan(0f));
            Assert.That(hpX, Is.GreaterThan(0f));
            Assert.That(hpLeft - guardRight, Is.EqualTo(24f));
            Assert.That(guardRight + hpLeft, Is.EqualTo(0f), "the gap's middle is on the figure's centre line");

            Assert.That(FloatNumberSpec.CentreX(FloatNumberKind.HpLoss, false, hpWidth), Is.EqualTo(0f));
            Assert.That(FloatNumberSpec.CentreX(FloatNumberKind.GuardLoss, false, guardWidth), Is.EqualTo(0f));
        }

        [Test]
        public void TheBlowGuardTookPartOf_PairsItsGuardAndHpNumbers()
        {
            // The core's three beats (§5.5): the swing, the shield, the wound.
            var cues = new List<Cue>
            {
                new Cue { Kind = CueKind.Slash, Target = UnitSide.Player, Source = UnitSide.Enemy, Amount = 13 },
                new Cue { Kind = CueKind.GuardBlock, Target = UnitSide.Player, Amount = 3, GuardAfter = 0 },
                new Cue { Kind = CueKind.Hit, Target = UnitSide.Player, Amount = 10, HpAfter = 40 },
            };
            Assert.That(FloatNumberSpec.GuardPairsWithHp(cues, 1), Is.True);
            Assert.That(FloatNumberSpec.HpPairsWithGuard(cues, 2), Is.True);
            Assert.That(FloatNumberSpec.HpPairsWithGuard(cues, 0), Is.False);
        }

        [Test]
        public void AGuardThatTookItAll_AndAWoundWithNoGuard_StandAlone()
        {
            var allGuard = new List<Cue>
            {
                new Cue { Kind = CueKind.Slash, Target = UnitSide.Player, Source = UnitSide.Enemy, Amount = 3 },
                new Cue { Kind = CueKind.GuardBlock, Target = UnitSide.Player, Amount = 3, GuardAfter = 2 },
                new Cue { Kind = CueKind.GuardGain, Target = UnitSide.Enemy, Amount = 3 },
            };
            Assert.That(FloatNumberSpec.GuardPairsWithHp(allGuard, 1), Is.False);

            var noGuard = new List<Cue>
            {
                new Cue { Kind = CueKind.Slash, Target = UnitSide.Player, Source = UnitSide.Enemy, Amount = 6 },
                new Cue { Kind = CueKind.Hit, Target = UnitSide.Player, Amount = 6, HpAfter = 44 },
            };
            Assert.That(FloatNumberSpec.HpPairsWithGuard(noGuard, 1), Is.False);
        }

        [Test]
        public void AReturnedBlow_PairsWithTheGuardItCrossed_AndOnlyOnTheSameUnit()
        {
            // A 見切り return: the Guard it crossed, then HP off the same unit (#188).
            var returned = new List<Cue>
            {
                new Cue { Kind = CueKind.GuardBlock, Target = UnitSide.Enemy, Amount = 2, GuardAfter = 0 },
                new Cue { Kind = CueKind.HpChange, Target = UnitSide.Enemy, Amount = -3, Text = "見切り" },
            };
            Assert.That(FloatNumberSpec.GuardPairsWithHp(returned, 0), Is.True);
            Assert.That(FloatNumberSpec.HpPairsWithGuard(returned, 1), Is.True);

            var otherUnit = new List<Cue>
            {
                new Cue { Kind = CueKind.GuardBlock, Target = UnitSide.Enemy, Amount = 2, GuardAfter = 0 },
                new Cue { Kind = CueKind.Hit, Target = UnitSide.Player, Amount = 4 },
            };
            Assert.That(FloatNumberSpec.GuardPairsWithHp(otherUnit, 0), Is.False);

            var heal = new List<Cue>
            {
                new Cue { Kind = CueKind.GuardBlock, Target = UnitSide.Player, Amount = 2, GuardAfter = 0 },
                new Cue { Kind = CueKind.HpChange, Target = UnitSide.Player, Amount = 2, Text = "再生" },
            };
            Assert.That(FloatNumberSpec.GuardPairsWithHp(heal, 0), Is.False, "a gain is not the wound of the blow");
        }

        [Test]
        public void ThePairQuestions_AreSafeAtTheEdges()
        {
            Assert.That(FloatNumberSpec.GuardPairsWithHp(null, 0), Is.False);
            Assert.That(FloatNumberSpec.GuardPairsWithHp(new List<Cue>(), 0), Is.False);
            Assert.That(FloatNumberSpec.HpPairsWithGuard(new List<Cue> { new Cue { Kind = CueKind.Hit } }, 0), Is.False);
            Assert.That(FloatNumberSpec.GuardPairsWithHp(new List<Cue> { new Cue { Kind = CueKind.GuardBlock } }, -1), Is.False);
        }
    }
}
