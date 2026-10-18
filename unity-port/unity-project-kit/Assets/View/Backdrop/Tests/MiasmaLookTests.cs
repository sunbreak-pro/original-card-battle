// #90: the miasma gauge and the shade that closes in on the exploration screen (battle-visual-v1 §2.5).
// The readings it hangs on match concept-v3 §6 and seven_layers_v4 §3, the share ahead warns of a line
// and of death, the fill stays apart from its track, and the shade grows with the reading without ever
// costing the map its text.
using NUnit.Framework;

namespace Backdrop.Tests
{
    public class MiasmaLookTests
    {
        [Test]
        public void TheReadings_MatchTheRules()
        {
            Assert.That(MiasmaLook.DeathAt, Is.EqualTo(100));
            Assert.That(MiasmaLook.StepPercent, Is.EqualTo(20));
            Assert.That(MiasmaLook.LastStepLine, Is.EqualTo(80));
        }

        [Test]
        public void TheFill_DeepensFromPhosphorToViolet()
        {
            AssertSame(MiasmaLook.FillAt(0), MiasmaLook.FillThin);
            AssertSame(MiasmaLook.FillAt(100), MiasmaLook.FillThick);
            Assert.That(BackdropSpec.Luminance(MiasmaLook.FillAt(60)), Is.LessThan(BackdropSpec.Luminance(MiasmaLook.FillAt(20))));
        }

        [Test]
        public void TheFill_StaysApartFromItsTrack_AtEveryReading()
        {
            for (int percent = 0; percent <= 100; percent++)
            {
                Assert.That(BackdropSpec.Contrast(MiasmaLook.FillAt(percent), MiasmaLook.Track),
                    Is.GreaterThanOrEqualTo(MiasmaLook.FillMinContrast), percent + "%");
            }
        }

        [Test]
        public void TheCaution_ReadsOnTheTrack()
        {
            Assert.That(BackdropSpec.Contrast(MiasmaLook.Caution, MiasmaLook.Track), Is.GreaterThanOrEqualTo(MiasmaLook.FillMinContrast));
        }

        [TestCase(10, 13, MiasmaWarning.None)]
        [TestCase(17, 20, MiasmaWarning.StepLine)]
        [TestCase(38, 41, MiasmaWarning.StepLine)]
        [TestCase(78, 85, MiasmaWarning.StepLine)]
        [TestCase(82, 89, MiasmaWarning.None)]
        [TestCase(93, 100, MiasmaWarning.Death)]
        [TestCase(75, 104, MiasmaWarning.Death)]
        [TestCase(40, 40, MiasmaWarning.None)]
        [TestCase(45, 35, MiasmaWarning.None)]
        public void TheShareAhead_WarnsOfALineAndOfDeath(int percent, int next, MiasmaWarning warning)
        {
            Assert.That(MiasmaLook.Warning(percent, next), Is.EqualTo(warning));
        }

        [Test]
        public void TheShareAhead_IsTheFaintFill_OrTheCautionWithItsMark()
        {
            Rgba plain = MiasmaLook.AheadColor(10, 13);
            AssertSame(plain.WithAlpha(1f), MiasmaLook.FillAt(13));
            Assert.That(plain.A, Is.EqualTo(MiasmaLook.AheadAlpha));
            Assert.That(MiasmaLook.ForecastMark(10, 13), Is.EqualTo(""));

            Rgba line = MiasmaLook.AheadColor(38, 41);
            AssertSame(line.WithAlpha(1f), MiasmaLook.Caution);
            Assert.That(line.A, Is.EqualTo(MiasmaLook.AheadStepAlpha));
            Assert.That(MiasmaLook.ForecastMark(38, 41), Is.EqualTo("△ "));

            Rgba death = MiasmaLook.AheadColor(95, 100);
            AssertSame(death, MiasmaLook.Caution);
            Assert.That(MiasmaLook.ForecastMark(95, 100), Is.EqualTo("△ "));
        }

        [Test]
        public void TheGauge_HasTenCells_WithFullLinesEvery20()
        {
            GaugeTick[] ticks = MiasmaLook.Ticks();
            Assert.That(ticks, Has.Length.EqualTo(9));
            for (int i = 0; i < ticks.Length; i++)
            {
                Assert.That(ticks[i].Percent, Is.EqualTo((i + 1) * 10));
                bool line = ticks[i].Percent % 20 == 0;
                Assert.That(ticks[i].IsStepLine, Is.EqualTo(line), ticks[i].Percent + "%");
                if (line) Assert.That(ticks[i].Height, Is.EqualTo(1f));
                else Assert.That(ticks[i].Height, Is.LessThan(1f));
            }
        }

        [Test]
        public void TheFill_ClimbsToANewReading_Softly()
        {
            Assert.That(MiasmaLook.Rise(38, 41, 0), Is.EqualTo(38f));
            Assert.That(MiasmaLook.Rise(38, 41, MiasmaLook.RiseMs / 2.0), Is.EqualTo(39.5f).Within(0.001f));
            Assert.That(MiasmaLook.Rise(38, 41, MiasmaLook.RiseMs), Is.EqualTo(41f));
            Assert.That(MiasmaLook.Rise(50, 40, MiasmaLook.RiseMs * 2.0), Is.EqualTo(40f));
            float early = MiasmaLook.Rise(0, 100, MiasmaLook.RiseMs * 0.1);
            Assert.That(early, Is.LessThan(10f), "it starts slower than straight");
        }

        [Test]
        public void TheBreath_StartsAt80_AndQuickensTowardsDeath()
        {
            Assert.That(MiasmaLook.BreathPeriodMs(79), Is.EqualTo(0));
            Assert.That(MiasmaLook.Breath(79, 600), Is.EqualTo(0f));
            Assert.That(MiasmaLook.FillAlpha(79, 600), Is.EqualTo(1f));
            Assert.That(MiasmaLook.BreathPeriodMs(80), Is.EqualTo(MiasmaLook.BreathSlowMs));
            Assert.That(MiasmaLook.BreathPeriodMs(99), Is.EqualTo(MiasmaLook.BreathFastMs));
            Assert.That(MiasmaLook.BreathPeriodMs(90), Is.LessThan(MiasmaLook.BreathSlowMs).And.GreaterThan(MiasmaLook.BreathFastMs));

            int period = MiasmaLook.BreathPeriodMs(80);
            Assert.That(MiasmaLook.Breath(80, 0), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(MiasmaLook.Breath(80, period / 2.0), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(MiasmaLook.FillAlpha(80, period / 2.0), Is.EqualTo(1f - MiasmaLook.BreathDepth).Within(1e-6f));
            Assert.That(MiasmaLook.Breath(80, period), Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void TheShade_IsClearAtTheStartOfALife()
        {
            MiasmaShade shade = MiasmaLook.ShadeAt(0f);
            Assert.That(shade.Strength, Is.EqualTo(0f));
            Assert.That(MiasmaLook.ShadeAlphaAt(shade, 1f), Is.EqualTo(0f));
            Assert.That(MiasmaLook.ShadeAlphaAt(shade, 0f), Is.EqualTo(0f));
            Assert.That(shade.SeepAlpha, Is.EqualTo(0f));
        }

        [Test]
        public void TheShade_ClosesIn_AsTheReadingClimbs()
        {
            float corner = MiasmaLook.CornerDistance(0f, 0f, 16f / 9f);
            Assert.That(corner, Is.EqualTo(1f).Within(1e-5f));
            float lastAlpha = -1f;
            float lastInner = 2f;
            for (int percent = 0; percent <= 100; percent += 10)
            {
                MiasmaShade shade = MiasmaLook.ShadeAt(percent);
                float alpha = MiasmaLook.ShadeAlphaAt(shade, corner);
                Assert.That(alpha, Is.GreaterThan(lastAlpha), percent + "%");
                Assert.That(shade.InnerRadius, Is.LessThan(lastInner), percent + "%");
                lastAlpha = alpha;
                lastInner = shade.InnerRadius;
            }
            Assert.That(MiasmaLook.ShadeAlphaAt(MiasmaLook.ShadeAt(20f), corner), Is.GreaterThan(0.05f), "the first line already shows");
            Assert.That(MiasmaLook.ShadeAlphaAt(MiasmaLook.ShadeAt(100f), corner),
                Is.EqualTo(1f - (1f - MiasmaLook.ShadeVeilMax) * (1f - MiasmaLook.ShadeEdgeMax)).Within(1e-5f));
        }

        [Test]
        public void TheShade_IsDarkerAtTheCorners_ThanAtTheCentre()
        {
            MiasmaShade shade = MiasmaLook.ShadeAt(70f);
            float last = -1f;
            for (int step = 0; step <= 10; step++)
            {
                float alpha = MiasmaLook.ShadeAlphaAt(shade, step / 10f);
                Assert.That(alpha, Is.GreaterThanOrEqualTo(last), "distance " + step / 10f);
                last = alpha;
            }
            Assert.That(MiasmaLook.ShadeAlphaAt(shade, 0f), Is.EqualTo(shade.VeilAlpha).Within(1e-6f));
        }

        [Test]
        public void TheCentre_IsOnlyEverVeiled_SoTheMapKeepsItsMiddle()
        {
            for (int percent = 0; percent <= 100; percent += 5)
            {
                MiasmaShade shade = MiasmaLook.ShadeAt(percent);
                Assert.That(shade.InnerRadius, Is.GreaterThanOrEqualTo(MiasmaLook.ShadeInnerClosed - 1e-6f));
                Assert.That(MiasmaLook.ShadeAlphaAt(shade, 0.3f), Is.LessThanOrEqualTo(MiasmaLook.ShadeVeilMax + 1e-6f), percent + "%");
            }
        }

        [Test]
        public void TheShade_BreathesWithTheGauge_From80()
        {
            Assert.That(MiasmaLook.ShadeAt(70f, 600).InnerRadius, Is.EqualTo(MiasmaLook.ShadeAt(70f).InnerRadius));
            int period = MiasmaLook.BreathPeriodMs(85f);
            MiasmaShade rest = MiasmaLook.ShadeAt(85f, 0);
            MiasmaShade deep = MiasmaLook.ShadeAt(85f, period / 2.0);
            Assert.That(rest.InnerRadius, Is.EqualTo(MiasmaLook.ShadeAt(85f).InnerRadius).Within(1e-6f));
            Assert.That(deep.InnerRadius, Is.EqualTo(rest.InnerRadius - MiasmaLook.ShadeBreathRadius).Within(1e-6f));
        }

        [Test]
        public void TheMapsText_ReadsUnderTheShade_OnEveryLayerAndReading()
        {
            float[] distances = { 0f, 0.3f, 0.5f, 0.7f, 0.85f, 1f };
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++)
            {
                ExplorationBackdrop backdrop = ExplorationBackdropSpec.For(layer);
                for (int percent = 0; percent <= 100; percent += 10)
                {
                    MiasmaShade shade = MiasmaLook.ShadeAt(percent);
                    for (int step = 0; step <= 10; step++)
                    {
                        Rgba under = ExplorationBackdropSpec.BrightestAt(backdrop, step / 10f);
                        foreach (float d in distances)
                        {
                            Rgba shaded = MiasmaLook.Shaded(under, shade, d);
                            Assert.That(BackdropSpec.Contrast(ExplorationBackdropSpec.Ink, shaded),
                                Is.GreaterThanOrEqualTo(ExplorationBackdropSpec.InkMinContrast), "layer " + layer + ", " + percent + "%");
                            Assert.That(BackdropSpec.Contrast(ExplorationBackdropSpec.Amber, shaded),
                                Is.GreaterThanOrEqualTo(ExplorationBackdropSpec.AmberMinContrast), "layer " + layer + ", " + percent + "%");
                        }
                    }
                }
            }
        }

        private static void AssertSame(Rgba actual, Rgba expected)
        {
            Assert.That(actual.R, Is.EqualTo(expected.R).Within(1e-6f));
            Assert.That(actual.G, Is.EqualTo(expected.G).Within(1e-6f));
            Assert.That(actual.B, Is.EqualTo(expected.B).Within(1e-6f));
            Assert.That(actual.A, Is.EqualTo(expected.A).Within(1e-6f));
        }
    }
}
