// dotnet-only tests of BattleClock (port of scenes/battle/clock.js): the scale table (speed × fast-forward × 演出短縮 /
// halt × pause), the paced runs' cap, real waits, the 50 ms step cap, destroy, and settings.speed ↔ BattleSpeedStep.
// clock.js has no JS unit test of its own; these hold the behaviour docs/BATTLE_SCENE.md §2.2 describes.
using System;
using System.Collections.Generic;
using Depiction;
using NUnit.Framework;

namespace Trial.Battle.Flow.Tests
{
    public class BattleClockTests
    {
        private List<Exception> _unhandled;

        [SetUp]
        public void SetUp()
        {
            Microtasks.Reset();
            _unhandled = new List<Exception>();
            Microtasks.Unhandled = e => _unhandled.Add(e);
        }

        [TearDown]
        public void TearDown()
        {
            Assert.That(_unhandled, Is.Empty, "no exception may reach Microtasks.Unhandled");
            Microtasks.Reset();
        }

        private static void Frames(BattleClock clock, int count, float dt)
        {
            for (int i = 0; i < count; i++) clock.Step(dt);
        }

        // ---- the scale --------------------------------------------------------------------------

        [Test]
        public void Scale_IsOne_BeforeTheFirstFrame()
        {
            var clock = new BattleClock(new BattleSettings { Speed = 1f });
            Assert.That(clock.Scale, Is.EqualTo(1f));
            Assert.That(clock.Now, Is.EqualTo(0f));
        }

        [TestCase(1f, 0.8f)]
        [TestCase(1.25f, 1f)]
        [TestCase(1.5f, 1.2f)]
        [TestCase(2f, 1f)]
        [TestCase(0f, 1f)]
        public void Scale_IsTheSpeedStepsTweenSpeed(float speed, float scale)
        {
            var clock = new BattleClock(new BattleSettings { Speed = speed });
            clock.Step(20f);
            Assert.That(clock.Scale, Is.EqualTo(scale).Within(1e-6));
            Assert.That(clock.Now, Is.EqualTo(20f * scale).Within(1e-4));
        }

        [Test]
        public void NullSettings_ReadAsTheDefaultSpeed()
        {
            var clock = new BattleClock(null);
            clock.Step(20f);
            Assert.That(clock.Scale, Is.EqualTo(BattleSpeed.TweenSpeed(BattleSpeed.Default)));
        }

        [Test]
        public void FastForward_MultipliesTheScaleByFfScale()
        {
            var clock = new BattleClock(new BattleSettings { Speed = 1.5f });
            clock.SetFastForward(true);
            Assert.That(clock.FastForward, Is.True);
            clock.Step(10f);
            Assert.That(clock.Scale, Is.EqualTo(1.2f * L.FfScale).Within(1e-5));
        }

        [Test]
        public void ShortFx_MultipliesByShortScale_AndWinsOverFastForward()
        {
            var settings = new BattleSettings { Speed = 1.25f, ShortFx = true };
            var clock = new BattleClock(settings);
            clock.SetFastForward(true);
            clock.Step(10f);
            Assert.That(clock.Scale, Is.EqualTo(L.ShortScale), "×1000, not ×4000");
            Assert.That(clock.Now, Is.EqualTo(10f * L.ShortScale).Within(1e-2));
        }

        [Test]
        public void Halted_RunsAtShortScale_WithoutShortFx()
        {
            var clock = new BattleClock(new BattleSettings { Speed = 1f });
            clock.SetHalted(true);
            clock.SetFastForward(true);
            clock.Step(10f);
            Assert.That(clock.Scale, Is.EqualTo(0.8f * L.ShortScale).Within(1e-2));
        }

        [Test]
        public void Paused_HoldsSceneTimeStill_WhileRealTimeAndOnFrameRunOn()
        {
            var clock = new BattleClock(new BattleSettings());
            clock.SetPaused(true);
            clock.SetFastForward(true);
            Assert.That(clock.Paused, Is.True);
            Promise scene = clock.Wait(10f);
            Promise real = clock.Real.Wait(30f);
            var dts = new List<(float scene, float real)>();
            clock.OnFrame((dtScene, dtReal) => dts.Add((dtScene, dtReal)));
            Frames(clock, 2, 20f);
            Assert.That(clock.Scale, Is.EqualTo(0f));
            Assert.That(clock.Now, Is.EqualTo(0f));
            Assert.That(scene.IsPending, Is.True, "a scene wait holds while paused");
            Assert.That(real.IsResolved, Is.True, "a real wait runs on");
            Assert.That(dts, Is.EqualTo(new[] { (0f, 20f), (0f, 20f) }));
            clock.SetPaused(false);
            clock.Step(20f);
            Assert.That(scene.IsResolved, Is.True);
        }

        [Test]
        public void TheScale_IsReadEveryFrame_SoAChangeActsOnWaitsAlreadyRunning()
        {
            var settings = new BattleSettings { Speed = 1.25f };
            var clock = new BattleClock(settings);
            Promise wait = clock.Wait(100f);
            clock.Step(40f);
            Assert.That(clock.Now, Is.EqualTo(40f).Within(1e-4));
            Assert.That(wait.IsPending, Is.True);
            clock.SetFastForward(true); // ×4: 15 real ms are the 60 scene ms left
            clock.Step(15f);
            Assert.That(wait.IsResolved, Is.True);

            settings.Speed = 1f; // the speed switch writes settings.speed; the clock reads it on its next frame
            clock.SetFastForward(false);
            clock.Step(10f);
            Assert.That(clock.Scale, Is.EqualTo(0.8f).Within(1e-6));
        }

        [Test]
        public void ASceneStep_IsClampedTo50Ms_RealTimeIsNot()
        {
            var clock = new BattleClock(new BattleSettings());
            clock.Step(200f);
            Assert.That(clock.Now, Is.EqualTo(50f).Within(1e-4), "a hitch slows the beat instead of skipping it");
            Assert.That(clock.Real.Now, Is.EqualTo(200f).Within(1e-4));
        }

        // ---- tweens, runs, waits ------------------------------------------------------------------

        [Test]
        public void Tween_DrivesItsKeys_OnScaledTime()
        {
            var clock = new BattleClock(new BattleSettings { Speed = 1.5f });
            var v = new TweenVars(("x", 0f));
            Promise p = clock.Tween(v, "x", 100f, 100f, Ease.Linear);
            clock.Step(50f); // 60 scene ms
            Assert.That(v["x"], Is.EqualTo(60f).Within(1e-3));
            clock.Step(50f);
            Assert.That(v["x"], Is.EqualTo(100f));
            Assert.That(p.IsResolved, Is.True);
        }

        [Test]
        public void Tween_ReplacesTheSameKey_AndTheEmptiedTweenResolves()
        {
            var clock = new BattleClock(new BattleSettings());
            var v = new TweenVars(("x", 0f), ("y", 0f));
            Promise first = clock.Tween(v, new[] { ("x", 10f) }, 100f);
            Promise second = clock.Tween(v, new[] { ("x", 5f), ("y", 5f) }, 20f, Ease.Linear);
            Assert.That(first.IsResolved, Is.True);
            clock.Step(20f);
            Assert.That(v["x"], Is.EqualTo(5f).Within(1e-4));
            Assert.That(second.IsResolved, Is.True);
        }

        [Test]
        public void Run_CallsFnEveryFrame_AndWithOneLast()
        {
            var clock = new BattleClock(new BattleSettings());
            var seen = new List<(float e, float raw)>();
            Promise p = clock.Run(40f, Ease.Linear, (e, raw) => seen.Add((e, raw)));
            Frames(clock, 3, 20f);
            Assert.That(seen, Is.EqualTo(new[] { (0.5f, 0.5f), (1f, 1f) }));
            Assert.That(p.IsResolved, Is.True);
        }

        [Test]
        public void Run_WithZeroMs_CallsFnOnceAtOnce_AndResolves()
        {
            var clock = new BattleClock(new BattleSettings());
            var seen = new List<float>();
            Promise p = clock.Run(0f, Ease.Out, t => seen.Add(t));
            Assert.That(seen, Is.EqualTo(new[] { 1f }));
            Assert.That(p.IsResolved, Is.True);
        }

        [Test]
        public void Wait_OfZeroNaNOrNegative_IsAlreadyResolved()
        {
            var clock = new BattleClock(new BattleSettings());
            Assert.That(clock.Wait(0f).IsResolved, Is.True);
            Assert.That(clock.Wait(float.NaN).IsResolved, Is.True, "JS `ms || 0`");
            Assert.That(clock.Wait(-5f).IsResolved, Is.True);
        }

        [Test]
        public void WorkStartedInsideACallback_IsKept()
        {
            // clock.js:71 (review 19: a 払's motes, started inside its own run, stayed on the screen for good).
            var clock = new BattleClock(new BattleSettings());
            Promise inner = null;
            clock.Run(30f, Ease.Linear, (e, raw) => { if (inner == null) inner = clock.Wait(25f); });
            Frames(clock, 3, 10f); // the wait was asked for at 10 and is due at 35
            Assert.That(inner, Is.Not.Null);
            Assert.That(inner.IsPending, Is.True);
            clock.Step(10f);
            Assert.That(inner.IsResolved, Is.True);
        }

        [Test]
        public void ContinuationsRun_AfterTheFramesOnFrameCallbacks()
        {
            var clock = new BattleClock(new BattleSettings());
            var order = new List<string>();
            clock.OnFrame((s, r) => order.Add("frame"));
            clock.Wait(10f).Then(() => order.Add("wait"));
            clock.Step(16f);
            Assert.That(order, Is.EqualTo(new[] { "frame", "wait" }));
        }

        // ---- paced runs ------------------------------------------------------------------------------

        [Test]
        public void RunPaced_UnderFastForward_RunsAtMostFfFloatScaleTimesTheSpeed()
        {
            var clock = new BattleClock(new BattleSettings { Speed = 1.25f });
            clock.SetFastForward(true);
            Promise run = clock.Run(300f, Ease.Linear, t => { });
            float pacedRaw = -1f;
            Promise paced = clock.RunPaced(300f, Ease.Linear, (e, raw) => pacedRaw = raw);
            Frames(clock, 4, 16f); // 64 real ms: the run has 256 of 300 at ×4
            Assert.That(run.IsPending, Is.True);
            clock.Step(16f);
            Assert.That(run.IsResolved, Is.True);
            Assert.That(pacedRaw, Is.EqualTo(80f * 1.5f / 300f).Within(1e-4), "the float advanced ×1.5, not ×4");
            Frames(clock, 7, 16f); // 192 real ms: 288 of 300
            Assert.That(paced.IsPending, Is.True);
            clock.Step(16f);
            Assert.That(paced.IsResolved, Is.True);
        }

        [Test]
        public void RunPaced_AtTheSlowStep_IsCappedAtItsTweenSpeedTimesFfFloatScale()
        {
            var clock = new BattleClock(new BattleSettings { Speed = 1f });
            clock.SetFastForward(true);
            float raw = 0f;
            clock.RunPaced(1000f, Ease.Linear, (e, r) => raw = r);
            clock.Step(10f);
            Assert.That(clock.Scale, Is.EqualTo(0.8f * 4f).Within(1e-5));
            Assert.That(raw, Is.EqualTo(10f * 0.8f * 1.5f / 1000f).Within(1e-6));
        }

        [Test]
        public void RunPaced_WithoutFastForward_RunsAtTheSceneScale()
        {
            var clock = new BattleClock(new BattleSettings { Speed = 1f });
            Promise paced = clock.RunPaced(80f, Ease.Linear, t => { });
            Frames(clock, 4, 20f); // ×0.8: 64 of 80
            Assert.That(paced.IsPending, Is.True);
            clock.Step(20f);
            Assert.That(paced.IsResolved, Is.True);
        }

        [Test]
        public void RunPaced_AskedForUnderShortFx_IsAPlainRun_EvenOnceShortFxIsOff()
        {
            // clock.js:153: 演出短縮 is read when the run is asked for; such a run follows the scene scale to its end.
            var settings = new BattleSettings { ShortFx = true };
            var clock = new BattleClock(settings);
            Promise p = clock.RunPaced(600f, Ease.Linear, t => { });
            settings.ShortFx = false;
            clock.SetFastForward(true);
            Frames(clock, 9, 16f); // 144 real ms ×4 = 576
            Assert.That(p.IsPending, Is.True);
            clock.Step(16f); // 160 ×4 = 640: a paced run would have 240
            Assert.That(p.IsResolved, Is.True);
        }

        [Test]
        public void RunPaced_WhileHalted_IsAPlainRun()
        {
            var clock = new BattleClock(new BattleSettings());
            clock.SetHalted(true);
            Promise p = clock.RunPaced(600f, Ease.Linear, t => { });
            clock.Step(16f);
            Assert.That(p.IsResolved, Is.True);
        }

        [Test]
        public void RunPaced_HoldsWhilePaused()
        {
            var clock = new BattleClock(new BattleSettings());
            float raw = 0f;
            Promise p = clock.RunPaced(100f, Ease.Linear, (e, r) => raw = r);
            clock.SetPaused(true);
            Frames(clock, 5, 20f);
            Assert.That(raw, Is.EqualTo(0f));
            Assert.That(p.IsPending, Is.True);
        }

        [Test]
        public void RunPaced_TakesItsPaceAtTheFramesStart_EvenWhenACallbackTurnsFastForwardOff()
        {
            // clock.js:66-69: dtPaced = dtReal × pacedScale() is taken before the frame's tweens and runs call back. A run
            // that turns fast-forward off in that frame leaves the frame's paced step at min(×4, ×1.5) = ×1.5.
            var clock = new BattleClock(new BattleSettings { Speed = 1.25f });
            clock.SetFastForward(true);
            clock.Run(100f, Ease.Linear, (e, raw) => clock.SetFastForward(false));
            float pacedRaw = -1f;
            clock.RunPaced(1000f, Ease.Linear, (e, raw) => pacedRaw = raw);
            clock.Step(16f);
            Assert.That(clock.FastForward, Is.False);
            Assert.That(pacedRaw, Is.EqualTo(16f * 1.5f / 1000f).Within(1e-6), "not 16 × 4: the flag changed after the frame's start");
            clock.Step(16f);
            Assert.That(pacedRaw, Is.EqualTo((24f + 16f) / 1000f).Within(1e-6), "the next frame reads the flag anew");
        }

        [Test]
        public void RunPaced_TakesItsPaceAtTheFramesStart_EvenWhenACallbackPauses()
        {
            var clock = new BattleClock(new BattleSettings { Speed = 1.25f });
            clock.Run(100f, Ease.Linear, (e, raw) => clock.SetPaused(true));
            float pacedRaw = -1f;
            clock.RunPaced(1000f, Ease.Linear, (e, raw) => pacedRaw = raw);
            clock.Step(20f);
            Assert.That(pacedRaw, Is.EqualTo(20f / 1000f).Within(1e-6));
            clock.Step(20f);
            Assert.That(pacedRaw, Is.EqualTo(20f / 1000f).Within(1e-6), "paused from the next frame on");
        }

        [Test]
        public void RunPaced_WithZeroMs_CallsFnAtOnce()
        {
            var clock = new BattleClock(new BattleSettings());
            int calls = 0;
            Promise p = clock.RunPaced(0f, Ease.Out, t => calls++);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(p.IsResolved, Is.True);
        }

        // ---- real time --------------------------------------------------------------------------------

        [Test]
        public void RealWait_RunsOnRealTime_NeitherScaledNorPaused()
        {
            var clock = new BattleClock(new BattleSettings { Speed = 1f, ShortFx = true });
            clock.SetPaused(true);
            Promise p = clock.Real.Wait(100f);
            clock.Step(60f);
            Assert.That(p.IsPending, Is.True);
            clock.Step(60f);
            Assert.That(p.IsResolved, Is.True);
            Assert.That(clock.Real.Now, Is.EqualTo(120f).Within(1e-4));
        }

        [Test]
        public void RealWait_OfZero_ResolvesOnTheNextFrame()
        {
            var clock = new BattleClock(new BattleSettings());
            Promise p = clock.Real.Wait(0f);
            Microtasks.Drain();
            Assert.That(p.IsPending, Is.True, "base.wait(0) resolves on the next frame, not at once");
            clock.Step(1f);
            Assert.That(p.IsResolved, Is.True);
        }

        [Test]
        public void RealWait_ContinuesAfterTheFrame_InAnAwaitChain()
        {
            var clock = new BattleClock(new BattleSettings());
            var log = new List<string>();
            async Promise Hold()
            {
                await clock.Real.Wait(50f);
                log.Add("held");
            }
            Hold().Forget();
            clock.Step(30f);
            Assert.That(log, Is.Empty);
            clock.Step(30f);
            Assert.That(log, Is.EqualTo(new[] { "held" }));
        }

        // ---- on frame --------------------------------------------------------------------------------

        [Test]
        public void OnFrame_GetsScaledAndRealDt_AndStopsWhenUnsubscribed()
        {
            var clock = new BattleClock(new BattleSettings { Speed = 1.5f });
            var dts = new List<(float, float)>();
            Action off = clock.OnFrame((s, r) => dts.Add((s, r)));
            clock.Step(20f);
            clock.Step(80f);
            off();
            clock.Step(20f);
            Assert.That(dts.Count, Is.EqualTo(2));
            Assert.That(dts[0].Item1, Is.EqualTo(24f).Within(1e-4));
            Assert.That(dts[0].Item2, Is.EqualTo(20f));
            Assert.That(dts[1].Item1, Is.EqualTo(60f).Within(1e-4), "dtReal is clamped to 50 before the scale");
            Assert.That(dts[1].Item2, Is.EqualTo(50f));
        }

        [Test]
        public void OnFrame_TheSameFunctionTwice_RunsOnce()
        {
            var clock = new BattleClock(new BattleSettings());
            int calls = 0;
            Action<float, float> fn = (s, r) => calls++;
            Action off1 = clock.OnFrame(fn);
            Action off2 = clock.OnFrame(fn);
            clock.Step(10f);
            Assert.That(calls, Is.EqualTo(1), "the JS keeps a Set");
            off2();
            clock.Step(10f);
            Assert.That(calls, Is.EqualTo(1));
            off1(); // a second unsubscribe is harmless
        }

        [Test]
        public void OnFrame_AFunctionAddedInsideTheFramesCallbacks_RunsInThatFrame()
        {
            // clock.js:108 frameFns.forEach: a Set's forEach visits what is added while it runs.
            var clock = new BattleClock(new BattleSettings());
            var log = new List<string>();
            Action<float, float> late = (s, r) => log.Add("late");
            bool added = false;
            clock.OnFrame((s, r) =>
            {
                log.Add("first");
                if (!added) { added = true; clock.OnFrame(late); }
            });
            clock.Step(16f);
            Assert.That(log, Is.EqualTo(new[] { "first", "late" }));
            clock.Step(16f);
            Assert.That(log, Is.EqualTo(new[] { "first", "late", "first", "late" }));
        }

        [Test]
        public void OnFrame_AFunctionRemovedBeforeItsTurn_IsSkippedInThatFrame_AndOneReAddedRunsLast()
        {
            var clock = new BattleClock(new BattleSettings());
            var log = new List<string>();
            Action<float, float> b = (s, r) => log.Add("b");
            Action<float, float> c = (s, r) => log.Add("c");
            Action offB = null;
            Action offC = null;
            bool once = false;
            clock.OnFrame((s, r) =>
            {
                log.Add("a");
                if (once) return;
                once = true;
                offC(); // c has not run yet this frame: skipped
                offB(); // b neither, but it comes back at the end of the set
                offB = clock.OnFrame(b);
            });
            offB = clock.OnFrame(b);
            offC = clock.OnFrame(c);
            clock.Step(16f);
            Assert.That(log, Is.EqualTo(new[] { "a", "b" }));
            log.Clear();
            clock.Step(16f);
            Assert.That(log, Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void OnFrame_AThrowingFunction_IsReported_AndTheOthersRun()
        {
            var clock = new BattleClock(new BattleSettings());
            var log = new List<string>();
            clock.OnFrame((s, r) => throw new InvalidOperationException("frame"));
            clock.OnFrame((s, r) => log.Add("next"));
            clock.Step(16f);
            Assert.That(log, Is.EqualTo(new[] { "next" }));
            Assert.That(_unhandled.Count, Is.EqualTo(1), "console.error once");
            _unhandled.Clear();
        }

        [Test]
        public void OnFrame_DestroyFromACallback_StopsTheRestOfTheFrame()
        {
            var clock = new BattleClock(new BattleSettings());
            var log = new List<string>();
            clock.OnFrame((s, r) => { log.Add("a"); clock.Destroy(); });
            clock.OnFrame((s, r) => log.Add("b"));
            clock.Step(16f);
            clock.Step(16f);
            Assert.That(log, Is.EqualTo(new[] { "a" }), "destroy clears frameFns");
        }

        [Test]
        public void ARealWaitsContinuation_RunsAfterASceneWaitsOfTheSameFrame()
        {
            // clock.js:180: real.wait is base.wait(ms).then(…), one step more than a scene wait.
            var clock = new BattleClock(new BattleSettings());
            var order = new List<string>();
            clock.Real.Wait(10f).Then(() => order.Add("real"));
            clock.Wait(10f).Then(() => order.Add("scene"));
            clock.Step(16f);
            Assert.That(order, Is.EqualTo(new[] { "scene", "real" }));
        }

        // ---- destroy ----------------------------------------------------------------------------------

        [Test]
        public void Destroy_LeavesEverythingPendingForever_AndNewWorkNeverSettles()
        {
            var clock = new BattleClock(new BattleSettings());
            var v = new TweenVars(("x", 0f));
            int frames = 0;
            clock.OnFrame((s, r) => frames++);
            var pending = new List<Promise>
            {
                clock.Tween(v, "x", 1f, 50f),
                clock.Run(50f, Ease.Out, t => { }),
                clock.RunPaced(50f, Ease.Out, t => { }),
                clock.Wait(50f),
                clock.Real.Wait(50f),
            };
            clock.Step(10f);
            Assert.That(frames, Is.EqualTo(1));
            clock.Destroy();
            Assert.That(clock.Alive, Is.False);

            int calledAfter = 0;
            pending.Add(clock.Wait(0f));
            pending.Add(clock.Run(0f, Ease.Out, t => calledAfter++));
            pending.Add(clock.RunPaced(10f, Ease.Out, t => calledAfter++));
            pending.Add(clock.Tween(v, "x", 2f, 10f));
            pending.Add(clock.Real.Wait(0f));
            clock.OnFrame((s, r) => frames++);
            Frames(clock, 20, 50f);
            Microtasks.Drain();

            Assert.That(pending.TrueForAll(p => p.IsPending), Is.True, "a destroyed scene's flow stops where it stands");
            Assert.That(calledAfter, Is.EqualTo(0), "after destroy a 0 ms run never calls its fn either (clock.js:137)");
            Assert.That(frames, Is.EqualTo(1));
            Assert.That(clock.Now, Is.EqualTo(10f).Within(1e-4));
            clock.Destroy(); // twice is harmless
        }

        [Test]
        public void Destroy_FromInsideAFlow_StopsItAtItsNextAwait()
        {
            var clock = new BattleClock(new BattleSettings());
            var log = new List<string>();
            async Promise Flow()
            {
                await clock.Wait(20f);
                log.Add("first");
                clock.Destroy();
                await clock.Wait(10f);
                log.Add("never");
            }
            Flow().Forget();
            Frames(clock, 10, 16f);
            Assert.That(log, Is.EqualTo(new[] { "first" }));
        }

        // ---- settings.speed ↔ BattleSpeedStep (clock.js:17-27) ---------------------------------------------

        [TestCase(1f, BattleSpeedStep.Slow)]
        [TestCase(1.25f, BattleSpeedStep.Normal)]
        [TestCase(1.5f, BattleSpeedStep.Fast)]
        [TestCase(2f, BattleSpeedStep.Normal)]
        [TestCase(0f, BattleSpeedStep.Normal)]
        [TestCase(1.2f, BattleSpeedStep.Normal)]
        [TestCase(float.NaN, BattleSpeedStep.Normal)]
        public void StepOfSpeed_ReadsTheThreeNumbers_AndDefaultsTheRest(float speed, BattleSpeedStep step)
        {
            Assert.That(BattleClock.StepOfSpeed(speed), Is.EqualTo(step));
        }

        [TestCase("1", BattleSpeedStep.Slow)]
        [TestCase("1.0", BattleSpeedStep.Slow)]
        [TestCase("1.25", BattleSpeedStep.Normal)]
        [TestCase("1.5", BattleSpeedStep.Fast)]
        [TestCase("1.50", BattleSpeedStep.Normal)]
        [TestCase("", BattleSpeedStep.Normal)]
        [TestCase(null, BattleSpeedStep.Normal)]
        public void StepOfSpeed_ReadsTheStringForms(string speed, BattleSpeedStep step)
        {
            Assert.That(BattleClock.StepOfSpeed(speed), Is.EqualTo(step));
        }

        [TestCase(BattleSpeedStep.Slow, 1f)]
        [TestCase(BattleSpeedStep.Normal, 1.25f)]
        [TestCase(BattleSpeedStep.Fast, 1.5f)]
        public void SpeedOfStep_IsTheNumberSettingsKeep_AndRoundTrips(BattleSpeedStep step, float speed)
        {
            Assert.That(BattleClock.SpeedOfStep(step), Is.EqualTo(speed));
            Assert.That(BattleClock.StepOfSpeed(BattleClock.SpeedOfStep(step)), Is.EqualTo(step));
        }

        [Test]
        public void TheSpeedSwitchesCycle_ReadsBackThroughSettings()
        {
            // player.js cycleSpeed: settings.speed = speedOfStep(BattleSpeed.Next(stepOfSpeed(settings.speed))).
            var settings = new BattleSettings { Speed = 1.25 };
            var seen = new List<double>();
            for (int i = 0; i < 3; i++)
            {
                settings.Speed = BattleClock.SpeedOfStep(BattleSpeed.Next(BattleClock.StepOfSpeed(settings.Speed)));
                seen.Add(settings.Speed);
            }
            Assert.That(seen, Is.EqualTo(new[] { 1.5, 1.0, 1.25 }));
        }

        [TestCase(1.0000000001)]
        [TestCase(0.9999999999)]
        [TestCase(1.2500000001)]
        [TestCase(1.4999999999)]
        public void StepOfSpeed_ComparesTheStoredDoubleExactly(double speed)
        {
            // clock.js:18-20 `speed === 1` on a JS number: a value a float would round onto a step is not that step.
            Assert.That(BattleClock.StepOfSpeed(speed), Is.EqualTo(BattleSpeed.Default));
            var clock = new BattleClock(new BattleSettings { Speed = speed });
            clock.Step(10f);
            Assert.That(clock.Scale, Is.EqualTo(BattleSpeed.TweenSpeed(BattleSpeed.Default)));
        }
    }
}
