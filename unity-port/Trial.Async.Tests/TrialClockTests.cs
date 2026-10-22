using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Trial.Async.Tests
{
    public class TrialClockTests
    {
        [SetUp]
        public void SetUp()
        {
            Microtasks.Reset();
            Microtasks.Unhandled = e => throw e;
        }

        [Test]
        public void Tween_ReachesItsTarget_WithTheEase_AndResolves()
        {
            var clock = new TrialClock();
            var v = new TweenVars(("x", 0f));
            Promise p = clock.Tween(v, "x", 100f, 100f, Ease.Linear);
            clock.Step(16f);   // from is read on the first step past the delay
            Assert.That(v["x"], Is.EqualTo(16f).Within(1e-3));
            clock.Step(50f);
            Assert.That(v["x"], Is.EqualTo(66f).Within(1e-3));
            Assert.That(p.IsPending, Is.True);
            clock.Step(50f);
            Assert.That(v["x"], Is.EqualTo(100f));
            Assert.That(p.IsResolved, Is.True);
        }

        [Test]
        public void ANewTweenOfTheSameKey_ReplacesTheOld_WhichResolves()
        {
            var clock = new TrialClock();
            var v = new TweenVars(("o", 0f), ("y", 0f));
            Promise first = clock.Tween(v, new[] { ("o", 1f) }, 200f, Ease.Linear);
            Promise both = clock.Tween(v, new[] { ("o", 0.5f), ("y", 10f) }, 100f, Ease.Linear);
            Assert.That(first.IsResolved, Is.True, "the older tween lost its only key");
            clock.Step(50f);
            clock.Step(50f);
            Assert.That(v["o"], Is.EqualTo(0.5f).Within(1e-4));
            Assert.That(v["y"], Is.EqualTo(10f).Within(1e-4));
            Assert.That(both.IsResolved, Is.True);
        }

        [Test]
        public void Delay_ReadsTheStartValueWhenItEnds()
        {
            var clock = new TrialClock();
            var v = new TweenVars(("x", 0f));
            clock.Tween(v, "x", 10f, 100f, Ease.Linear, delay: 50f);
            v["x"] = 5f;
            clock.Step(50f);
            Assert.That(v["x"], Is.EqualTo(5f));
            clock.Step(50f);
            Assert.That(v["x"], Is.EqualTo(7.5f).Within(1e-4));
        }

        [Test]
        public void Scale_StretchesSceneTime_ButNotRealWaits()
        {
            float scale = 0.5f;
            var clock = new TrialClock(() => scale);
            Promise scene = clock.Wait(100f);
            Promise real = clock.WaitReal(100f);
            clock.Step(50f);
            clock.Step(50f);
            Assert.That(real.IsResolved, Is.True);
            Assert.That(scene.IsPending, Is.True);
            Assert.That(clock.Now, Is.EqualTo(50f).Within(1e-4));
            scale = 0f;
            clock.Step(50f);
            Assert.That(clock.Now, Is.EqualTo(50f).Within(1e-4), "scale 0 holds the scene still");
            scale = 2f;
            clock.Step(25f);
            Assert.That(scene.IsResolved, Is.True);
        }

        [Test]
        public void TheSceneStep_IsClampedTo50ms_WhenAsked()
        {
            var clamped = new TrialClock();
            var free = new TrialClock(clampSceneStep: false);
            clamped.Step(200f);
            free.Step(200f);
            Assert.That(clamped.Now, Is.EqualTo(50f));
            Assert.That(free.Now, Is.EqualTo(200f));
        }

        [Test]
        public void Run_CallsEveryFrame_AndLastWithOne()
        {
            var clock = new TrialClock();
            var seen = new List<float>();
            Promise p = clock.Run(100f, Ease.Linear, (e, raw) => seen.Add(raw));
            clock.Step(40f);
            clock.Step(40f);
            clock.Step(40f);
            Assert.That(seen, Is.EqualTo(new[] { 0.4f, 0.8f, 1f }).Within(1e-4));
            Assert.That(p.IsResolved, Is.True);

            var now = new List<float>();
            clock.Run(0f, Ease.Linear, (e, raw) => now.Add(raw));
            Assert.That(now, Is.EqualTo(new[] { 1f }), "a zero-length run calls fn(1, 1) at once");
        }

        [Test]
        public void WorkStartedInsideACallback_IsKept()
        {
            var clock = new TrialClock();
            int inner = 0;
            bool started = false;
            clock.Run(20f, Ease.Linear, (e, raw) =>
            {
                if (started) return;
                started = true;
                clock.Run(1000f, Ease.Linear, (e2, r2) => inner++);
            });
            clock.Step(10f);
            clock.Step(10f);
            clock.Step(10f);
            Assert.That(inner, Is.EqualTo(2), "the inner run, added mid-frame, runs from the next frame on");
        }

        [Test]
        public void ContinuationsRunAtTheEndOfTheStep_AfterOnFrame()
        {
            var clock = new TrialClock();
            var log = new List<string>();
            async Promise Flow() { await clock.Wait(10f); log.Add("continued"); }
            clock.OnFrame((dt, real) => log.Add("frame"));
            Flow();
            clock.Step(16f);
            Assert.That(log, Is.EqualTo(new[] { "frame", "continued" }));
        }

        [Test]
        public void Stop_EitherResolvesOrDropsWhatIsPending()
        {
            var keep = new TrialClock();
            Promise a = keep.Wait(1000f);
            keep.Stop(resolvePending: true);
            Assert.That(a.IsResolved, Is.True);

            var drop = new TrialClock();
            Promise b = drop.Wait(1000f);
            drop.Stop(resolvePending: false);
            Assert.That(b.IsPending, Is.True);
            Assert.That(drop.Wait(1f).IsPending, Is.True);
            drop.Step(5000f);
            Assert.That(b.IsPending, Is.True);
        }

        [Test]
        public void Eases_MatchTheJsSet_AndCssBezier()
        {
            Assert.That(Ease.Out(0.5f), Is.EqualTo(0.875f).Within(1e-6));
            Assert.That(Ease.In(0.5f), Is.EqualTo(0.125f).Within(1e-6));
            Assert.That(Ease.InOut(0.25f), Is.EqualTo(0.0625f).Within(1e-6));
            Assert.That(Ease.Smooth(0.5f), Is.EqualTo(0.5f).Within(1e-6));
            // Chromium: ease at .25/.5/.75 → .4085, .8024, .9604 (rounded)
            Assert.That(Ease.Css(0.25f), Is.EqualTo(0.4085f).Within(2e-3));
            Assert.That(Ease.Css(0.5f), Is.EqualTo(0.8024f).Within(2e-3));
            Assert.That(Ease.Css(0.75f), Is.EqualTo(0.9604f).Within(2e-3));
            Assert.That(Ease.Of("cubic-bezier(0.22, 0.61, 0.36, 1)")(0.5f), Is.EqualTo(Ease.Decel(0.5f)).Within(1e-6));
            Assert.That(Ease.Of(null)(0.5f), Is.EqualTo(Ease.Out(0.5f)));
        }
    }
}
