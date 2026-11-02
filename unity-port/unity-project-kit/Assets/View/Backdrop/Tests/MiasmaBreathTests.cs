// #728: the breath a screen keeps while the reading moves. The phase is carried, so a new reading only
// changes the pace: it starts from rest at 80%, finishes at its last pace below 80%, and a pause does not
// jump it.
using System;
using NUnit.Framework;

namespace Backdrop.Tests
{
    public class MiasmaBreathTests
    {
        // Frames of 16 ms move the fastest breath (1200 ms a cycle) by at most π × 16 / 1200 ≈ 0.042.
        private const double FrameMs = 16.0;
        private const float LargestFrameMove = 0.05f;

        /// <summary>Calls Advance every frame from <paramref name="fromMs"/> up to <paramref name="toMs"/> and returns the last breath.</summary>
        private static float Run(MiasmaBreath breath, double fromMs, double toMs, float percent)
        {
            float last = 0f;
            for (double at = fromMs; at <= toMs; at += FrameMs) last = breath.Advance(at, percent);
            return last;
        }

        [Test]
        public void TheCosine_IsZeroAtRestAndOneHalfway()
        {
            Assert.That(BackdropMotion.CosineDip(0.0), Is.EqualTo(0.0).Within(1e-9));
            Assert.That(BackdropMotion.CosineDip(0.25), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(BackdropMotion.CosineDip(0.5), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(BackdropMotion.CosineDip(1.0), Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void AdvanceBreath_MovesAtThePaceOfTheReadingAndWraps()
        {
            int period = MiasmaLook.BreathPeriodMs(85f);
            Assert.That(MiasmaLook.AdvanceBreath(0.0, period / 4.0, 85f), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(MiasmaLook.AdvanceBreath(0.75, period / 2.0, 85f), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(MiasmaLook.AdvanceBreath(0.5, MiasmaLook.BreathFastMs / 4.0, 99f), Is.EqualTo(0.75).Within(1e-9));
        }

        [Test]
        public void AdvanceBreath_BelowTheBreathingLine_LeavesThePhase()
        {
            Assert.That(MiasmaLook.AdvanceBreath(0.3, 500.0, 79f), Is.EqualTo(0.3));
        }

        [Test]
        public void ThePhaseForms_MatchTheClockForms()
        {
            int period = MiasmaLook.BreathPeriodMs(90f);
            double ms = period * 3.25;
            float breath = MiasmaLook.BreathAtPhase(0.25);
            Assert.That(MiasmaLook.Breath(90f, ms), Is.EqualTo(breath).Within(1e-6f));
            Assert.That(MiasmaLook.FillAlpha(90f, ms), Is.EqualTo(MiasmaLook.FillAlphaOf(breath)).Within(1e-6f));
            Assert.That(MiasmaLook.ShadeAt(90f, ms).InnerRadius,
                Is.EqualTo(MiasmaLook.ShadeBreathing(90f, breath).InnerRadius).Within(1e-6f));
            Assert.That(MiasmaLook.ShadeBreathing(90f, 0f).InnerRadius, Is.EqualTo(MiasmaLook.ShadeAt(90f).InnerRadius));
        }

        [Test]
        public void ABreath_StartsFromRestWhenTheReadingReachesTheLine()
        {
            var breath = new MiasmaBreath();
            Assert.That(Run(breath, 0.0, 5000.0, 79f), Is.EqualTo(0f), "below the line nothing moves");
            Assert.That(breath.Advance(5000.0 + FrameMs, 80f), Is.LessThan(LargestFrameMove), "the first frame at the line is still at rest");

            var fresh = new MiasmaBreath();
            Assert.That(fresh.Advance(600000.0, 90f), Is.EqualTo(0f), "the clock's size does not matter, only its steps");
            int period = MiasmaLook.BreathPeriodMs(90f);
            float halfway = Run(fresh, 600000.0 + FrameMs, 600000.0 + period / 2.0, 90f);
            Assert.That(halfway, Is.GreaterThan(0.99f), "half a period in is the bottom of the breath");
        }

        [Test]
        public void ABreath_UnderWay_FinishesAtItsLastPaceBelowTheLine()
        {
            var breath = new MiasmaBreath();
            int period = MiasmaLook.BreathPeriodMs(85f);
            double start = period / 4.0;
            float before = Run(breath, 0.0, start, 85f);
            Assert.That(before, Is.GreaterThan(0.4f));

            // Falling below the line does not snap it back: the next frame is next to the last.
            double fellAt = start + FrameMs;
            float next = breath.Advance(fellAt, 70f);
            Assert.That(Math.Abs(next - before), Is.LessThan(LargestFrameMove));

            // Three quarters of a period remain at 85%'s pace, not at any other.
            double lateByTwoFrames = fellAt + period * 0.75 - 2.0 * FrameMs;
            Assert.That(Run(breath, fellAt + FrameMs, lateByTwoFrames, 70f), Is.GreaterThan(0f), "still finishing just before the end");
            double end = fellAt + period * 0.75 + 2.0 * FrameMs;
            Assert.That(Run(breath, lateByTwoFrames + FrameMs, end, 70f), Is.EqualTo(0f), "at rest once the cycle ends");
            Assert.That(Run(breath, end + FrameMs, end + 3000.0, 70f), Is.EqualTo(0f), "and it does not start again");
        }

        [Test]
        public void ABreath_Finishing_TakesTheNewPaceWhenTheReadingClimbsBack()
        {
            var breath = new MiasmaBreath();
            const double last = 38.0 * FrameMs;
            float before = Run(breath, 0.0, last, 85f);
            float falling = breath.Advance(last + FrameMs, 70f);
            float back = breath.Advance(last + 2.0 * FrameMs, 95f);
            Assert.That(Math.Abs(falling - before), Is.LessThan(LargestFrameMove));
            Assert.That(Math.Abs(back - falling), Is.LessThan(LargestFrameMove));

            // Back above the line it keeps breathing past the end of the cycle instead of settling at rest.
            double halfOfTheNext = last + 2.0 * FrameMs + MiasmaLook.BreathPeriodMs(95f);
            Assert.That(Run(breath, last + 3.0 * FrameMs, halfOfTheNext, 95f), Is.GreaterThan(0f));
        }

        [Test]
        public void APause_ResumesWhereItStopped()
        {
            var paused = new MiasmaBreath();
            var steady = new MiasmaBreath();
            paused.Advance(0.0, 90f);
            steady.Advance(0.0, 90f);

            // Ten minutes under a battle move the breath no further than one capped step.
            float afterPause = paused.Advance(600000.0, 90f);
            float oneStep = steady.Advance(MiasmaBreath.MaxStepMs, 90f);
            Assert.That(afterPause, Is.EqualTo(oneStep));

            // A clock that runs back moves nothing.
            Assert.That(paused.Advance(599000.0, 90f), Is.EqualTo(afterPause));
        }

        [Test]
        public void TheBreath_NeverJumpsWhileTheReadingEasesUp()
        {
            // Ten minutes in, the rise from 85% to 88% (the case #728 reports): the shown reading changes every frame.
            const double start = 600000.0;
            var carried = new MiasmaBreath();
            carried.Advance(start, 85f);
            float lastCarried = 0f;
            float lastClock = MiasmaLook.Breath(85f, start);
            float largestCarried = 0f;
            float largestClock = 0f;
            for (double elapsed = FrameMs; elapsed <= MiasmaLook.RiseMs + 200.0; elapsed += FrameMs)
            {
                float shown = MiasmaLook.Rise(85, 88, elapsed);
                float now = carried.Advance(start + elapsed, shown);
                largestCarried = Math.Max(largestCarried, Math.Abs(now - lastCarried));
                lastCarried = now;

                float clock = MiasmaLook.Breath(shown, start + elapsed);
                largestClock = Math.Max(largestClock, Math.Abs(clock - lastClock));
                lastClock = clock;
            }
            Assert.That(largestCarried, Is.LessThan(LargestFrameMove));
            Assert.That(largestClock, Is.GreaterThan(LargestFrameMove), "the absolute clock is what strobes");
        }

        [Test]
        public void TheBreath_NeverJumpsWhenTheReadingLeapsToAnotherPace()
        {
            var breath = new MiasmaBreath();
            const double lastFrame = 44.0 * FrameMs;
            float last = Run(breath, 0.0, lastFrame, 80f);
            float now = breath.Advance(lastFrame + FrameMs, 99f);
            Assert.That(Math.Abs(now - last), Is.LessThan(LargestFrameMove));
        }
    }
}
