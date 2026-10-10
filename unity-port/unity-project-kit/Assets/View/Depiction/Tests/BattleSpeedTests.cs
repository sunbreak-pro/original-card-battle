// The battle speed (Script/BattleSpeed.cs, #348). The View sets UiTween.Speed to BattleSpeed.TweenSpeed
// and hands UiTween the written ms, so what is held here is how long the screen's tweens and waits
// last at each of the three steps.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class BattleSpeedTests
    {
        /// <summary>60 fps, the frame the PlayMode measurement runs at.</summary>
        private const float FrameSeconds = 1f / 60f;

        [Test]
        public void TheThreeSteps_AreOnePointZero_OnePointTwoFive_AndOnePointFive_AndTheDefaultIsTodaysPace()
        {
            Assert.That(BattleSpeed.Multiplier(BattleSpeedStep.Slow), Is.EqualTo(1f).Within(1e-6));
            Assert.That(BattleSpeed.Multiplier(BattleSpeedStep.Normal), Is.EqualTo(1.25f).Within(1e-6));
            Assert.That(BattleSpeed.Multiplier(BattleSpeedStep.Fast), Is.EqualTo(1.5f).Within(1e-6));
            Assert.That(BattleSpeed.Default, Is.EqualTo(BattleSpeedStep.Normal));
            Assert.That(BattleSpeed.Steps.Length, Is.EqualTo(3));

            // The default plays today's lengths, to the millisecond.
            Assert.That(BattleSpeed.TweenSpeed(BattleSpeedStep.Normal), Is.EqualTo(1f));
            foreach (EffectSpec spec in EffectCatalog.All)
            {
                Assert.That(BattleSpeed.WallMs(spec.Ms, BattleSpeedStep.Normal), Is.EqualTo(spec.Ms), spec.Id.ToString());
            }
        }

        [TestCase(BattleSpeedStep.Slow, 1.25f)]
        [TestCase(BattleSpeedStep.Normal, 1f)]
        [TestCase(BattleSpeedStep.Fast, 0.8333333f)]
        public void AWait_LastsTodaysLengthTimesOnePointTwoFiveOverTheMultiplier(BattleSpeedStep step, float scale)
        {
            var on = EffectSwitches.AllOn();
            List<DepictionEvent> events = TurnSliceScript.Build().Events.Concat(LiveTurnEvents()).ToList();
            Assert.That(events, Is.Not.Empty);
            foreach (DepictionEvent ev in events)
            {
                Assert.That(EffectPlan.BlockingMs(ev, on, step), Is.EqualTo(EffectPlan.BlockingMs(ev, on) * scale).Within(0.01),
                    "event " + ev.Order + " " + ev.Title);
            }
            Assert.That(EffectFlow.AfterPlayerEventMsAt(step), Is.EqualTo(250f * scale).Within(0.01));
            Assert.That(EffectFlow.AfterAutoEventMsAt(step), Is.EqualTo(350f * scale).Within(0.01));
        }

        [TestCase(BattleSpeedStep.Slow)]
        [TestCase(BattleSpeedStep.Normal)]
        [TestCase(BattleSpeedStep.Fast)]
        public void ATween_HandedTheWrittenLength_EndsWhenTheWaitDoes(BattleSpeedStep step)
        {
            float speed = BattleSpeed.TweenSpeed(step);
            foreach (EffectSpec spec in EffectCatalog.All)
            {
                float wall = BattleSpeed.WallMs(spec.Ms, step);
                // The written length played at UiTween.Speed lasts exactly the speed's length.
                Assert.That(speed * wall, Is.EqualTo(spec.Ms).Within(0.01), spec.Id.ToString());

                // And frame by frame, as Assets/View/UiTween.cs runs it:
                //   Run:  t += Time.unscaledDeltaTime * Speed / duration;  while (t < 1)
                //   Wait: t += Time.unscaledDeltaTime * Speed;             while (t < duration)
                float runMs = RunFrames(spec.Ms, speed) * FrameSeconds * 1000f;
                float waitMs = WaitFrames(spec.Ms, speed) * FrameSeconds * 1000f;
                Assert.That(runMs, Is.GreaterThanOrEqualTo(wall - 0.01f), "Run " + spec.Id);
                Assert.That(runMs, Is.LessThanOrEqualTo(wall + EffectFlow.FrameMs + 0.01f), "Run " + spec.Id);
                Assert.That(waitMs, Is.GreaterThanOrEqualTo(wall - 0.01f), "Wait " + spec.Id);
                Assert.That(waitMs, Is.LessThanOrEqualTo(wall + EffectFlow.FrameMs + 0.01f), "Wait " + spec.Id);
            }

            // The gaps between events go through UiTween.Wait as well.
            float gap = WaitFrames(EffectFlow.AfterAutoEventMs, speed) * FrameSeconds * 1000f;
            Assert.That(gap, Is.GreaterThanOrEqualTo(EffectFlow.AfterAutoEventMsAt(step) - 0.01f));
            Assert.That(gap, Is.LessThanOrEqualTo(EffectFlow.AfterAutoEventMsAt(step) + EffectFlow.FrameMs + 0.01f));
        }

        [Test]
        public void Halted_SettlesAtReducedMotion_WhateverTheSpeed()
        {
            foreach (BattleSpeedStep step in BattleSpeed.Steps)
            {
                Assert.That(BattleSpeed.TweenSpeed(step, true), Is.EqualTo(BattleSpeed.HaltedTweenSpeed), step.ToString());
                Assert.That(BattleSpeed.TweenSpeed(step, false), Is.EqualTo(BattleSpeed.TweenSpeed(step)), step.ToString());
            }
            Assert.That(BattleSpeed.HaltedTweenSpeed, Is.EqualTo(1000f));
        }

        [Test]
        public void TheSwitch_GoesSlowNormalFastAndRoundAgain_AndSaysItsSpeed()
        {
            Assert.That(BattleSpeed.Next(BattleSpeedStep.Slow), Is.EqualTo(BattleSpeedStep.Normal));
            Assert.That(BattleSpeed.Next(BattleSpeedStep.Normal), Is.EqualTo(BattleSpeedStep.Fast));
            Assert.That(BattleSpeed.Next(BattleSpeedStep.Fast), Is.EqualTo(BattleSpeedStep.Slow));
            // battle-visual-v1 §4.1 (#389): the number only, no word.
            Assert.That(BattleSpeed.Label(BattleSpeedStep.Slow), Is.EqualTo("×1.0"));
            Assert.That(BattleSpeed.Label(BattleSpeedStep.Normal), Is.EqualTo("×1.25"));
            Assert.That(BattleSpeed.Label(BattleSpeedStep.Fast), Is.EqualTo("×1.5"));
        }

        [Test]
        public void TheIcon_StacksOneTwoOrThreeChevrons_AndTheTooltipNamesTheThreeSteps()
        {
            // 付録 A 「速さ」: one chevron at ×1.0, two at ×1.25, three at ×1.5.
            Assert.That(BattleSpeed.Chevrons(BattleSpeedStep.Slow), Is.EqualTo(1));
            Assert.That(BattleSpeed.Chevrons(BattleSpeedStep.Normal), Is.EqualTo(2));
            Assert.That(BattleSpeed.Chevrons(BattleSpeedStep.Fast), Is.EqualTo(3));
            Assert.That(BattleSpeed.Tooltip, Is.EqualTo("戦闘の速さ。押すたびに ×1.0 → ×1.25 → ×1.5 と巡ります"));
            // The tooltip walks the steps in the switch's order, written the way the button writes them.
            string order = string.Join(" → ", BattleSpeed.Steps.Select(BattleSpeed.Label));
            Assert.That(BattleSpeed.Tooltip, Does.Contain(order));
            foreach (BattleSpeedStep step in BattleSpeed.Steps)
            {
                Assert.That(BattleSpeed.Label(step), Does.Not.Contain("速さ").And.Not.Contain("倍"), "the switch shows no word");
            }
        }

        [Test]
        public void TheSavedSpeed_ComesBack_AndAnythingElseIsTheDefault()
        {
            foreach (BattleSpeedStep step in BattleSpeed.Steps)
            {
                Assert.That(BattleSpeed.Load(true, BattleSpeed.Save(step)), Is.EqualTo(step), step.ToString());
            }
            Assert.That(BattleSpeed.Load(false, "1.5"), Is.EqualTo(BattleSpeed.Default), "nothing stored");
            Assert.That(BattleSpeed.Load(true, null), Is.EqualTo(BattleSpeed.Default));
            Assert.That(BattleSpeed.Load(true, ""), Is.EqualTo(BattleSpeed.Default));
            Assert.That(BattleSpeed.Load(true, "2"), Is.EqualTo(BattleSpeed.Default));
            Assert.That(BattleSpeed.Load(true, "1,25"), Is.EqualTo(BattleSpeed.Default), "a culture's decimal comma is not ours");
            Assert.That(BattleSpeed.Load(true, " 1.5 "), Is.EqualTo(BattleSpeedStep.Fast));
        }

        [Test]
        public void TheCap_IsTwoSecondsAtTheDefaultAndFaster_AndTwoAndAHalfAtOnePointZero()
        {
            Assert.That(BattleSpeed.EventCapMsAt(BattleSpeedStep.Slow), Is.EqualTo(2500f).Within(0.01));
            Assert.That(BattleSpeed.EventCapMsAt(BattleSpeedStep.Normal), Is.EqualTo(2000f));
            Assert.That(BattleSpeed.EventCapMsAt(BattleSpeedStep.Fast), Is.EqualTo(2000f));
        }

        // ---- Helpers ----

        private static int RunFrames(float ms, float speed)
        {
            float duration = ms / 1000f;
            float t = 0f;
            int frames = 0;
            while (t < 1f)
            {
                t += FrameSeconds * speed / duration;
                frames++;
            }
            return frames;
        }

        private static int WaitFrames(float ms, float speed)
        {
            float duration = ms / 1000f;
            float t = 0f;
            int frames = 0;
            while (t < duration)
            {
                t += FrameSeconds * speed;
                frames++;
            }
            return frames;
        }

        /// <summary>One live turn with nobody at the mouse, as EffectTests plays it.</summary>
        private static List<DepictionEvent> LiveTurnEvents()
        {
            var live = new LiveTurn();
            var events = new List<DepictionEvent>();
            while (!live.Finished && !live.WaitingForPlayer) events.Add(live.AdvanceAuto());
            string first = live.Frame.Hand.Select(c => c.Id).FirstOrDefault(id => live.Inspect(id) == PlayVerdict.Accepted);
            if (first != null)
            {
                CardFace face = DepictionText.Find(live.Frame.Hand, first);
                Assert.That(live.TryPlay(first, DepictionText.RequiredZone(face.Aim), out DepictionEvent played), Is.EqualTo(PlayVerdict.Accepted));
                events.Add(played);
            }
            events.Add(live.EndTurn());
            while (!live.Finished && !live.WaitingForPlayer) events.Add(live.AdvanceAuto());
            return events;
        }
    }
}
