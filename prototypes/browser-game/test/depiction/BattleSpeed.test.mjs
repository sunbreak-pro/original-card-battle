// Tests for src/bridge/depictionScript/BattleSpeed.js (port of View/Depiction/Script/BattleSpeed.cs).
// Ported cases: Assets/View/Depiction/Tests/BattleSpeedTests.cs (all). The cases that read EffectCatalog,
// EffectPlan, EffectFlow or TurnSliceScript run on those modules and are skipped, naming the module,
// while it is missing.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { BattleSpeed, BattleSpeedStep } from '../../src/bridge/depictionScript/BattleSpeed.js';

const f = Math.fround;
const near = (a, b, tol, msg) => assert.ok(Math.abs(a - b) <= tol, `${msg || ''} expected ${b} ± ${tol}, got ${a}`);

async function load(rel) {
  try { return await import(new URL('../../src/' + rel, import.meta.url).href); } catch { return null; }
}
const Effects = await load('bridge/depictionScript/Effects.js');
const Audit = await load('bridge/depictionScript/EffectAudit.js');
const Slice = await load('bridge/depictionScript/TurnSliceScript.js');
const needEffects = Effects ? false : 'needs bridge/depictionScript/Effects.js';
const needAll = !Effects ? 'needs bridge/depictionScript/Effects.js' : !Audit ? 'needs bridge/depictionScript/EffectAudit.js' : !Slice ? 'needs bridge/depictionScript/TurnSliceScript.js' : false;

/** 60 fps, the frame the PlayMode measurement runs at. */
const FrameSeconds = f(1 / 60);

function RunFrames(ms, speed) {
  const duration = f(ms / 1000);
  let t = f(0);
  let frames = 0;
  while (t < 1) {
    t = f(t + f(f(FrameSeconds * speed) / duration));
    frames++;
  }
  return frames;
}

function WaitFrames(ms, speed) {
  const duration = f(ms / 1000);
  let t = f(0);
  let frames = 0;
  while (t < duration) {
    t = f(t + f(FrameSeconds * speed));
    frames++;
  }
  return frames;
}

test('TheThreeSteps_AreOnePointZero_OnePointTwoFive_AndOnePointFive_AndTheDefaultIsTodaysPace', { skip: needEffects }, () => {
  near(BattleSpeed.Multiplier(BattleSpeedStep.Slow), 1, 1e-6);
  near(BattleSpeed.Multiplier(BattleSpeedStep.Normal), 1.25, 1e-6);
  near(BattleSpeed.Multiplier(BattleSpeedStep.Fast), 1.5, 1e-6);
  assert.equal(BattleSpeed.Default, BattleSpeedStep.Normal);
  assert.equal(BattleSpeed.Steps.length, 3);

  // The default plays today's lengths, to the millisecond.
  assert.equal(BattleSpeed.TweenSpeed(BattleSpeedStep.Normal), 1);
  for (const spec of Effects.EffectCatalog.All) {
    assert.equal(BattleSpeed.WallMs(spec.Ms, BattleSpeedStep.Normal), spec.Ms, spec.Id);
  }
});

for (const [step, scale] of [[BattleSpeedStep.Slow, 1.25], [BattleSpeedStep.Normal, 1], [BattleSpeedStep.Fast, f(0.8333333)]]) {
  test(`AWait_LastsTodaysLengthTimesOnePointTwoFiveOverTheMultiplier(${step})`, { skip: needAll }, () => {
    const on = Effects.EffectSwitches.AllOn();
    const events = Slice.TurnSliceScript.Build().Events;
    assert.ok(events.length > 0);
    for (const ev of events) {
      near(Effects.EffectPlan.BlockingMs(ev, on, step), Effects.EffectPlan.BlockingMs(ev, on) * scale, 0.01, `event ${ev.Order} ${ev.Title}`);
    }
    near(Audit.EffectFlow.AfterPlayerEventMsAt(step), 250 * scale, 0.01);
    near(Audit.EffectFlow.AfterAutoEventMsAt(step), 350 * scale, 0.01);
  });
}

for (const step of [BattleSpeedStep.Slow, BattleSpeedStep.Normal, BattleSpeedStep.Fast]) {
  test(`ATween_HandedTheWrittenLength_EndsWhenTheWaitDoes(${step})`, { skip: needAll }, () => {
    const speed = BattleSpeed.TweenSpeed(step);
    const FrameMs = Audit.EffectFlow.FrameMs;
    for (const spec of Effects.EffectCatalog.All) {
      const wall = BattleSpeed.WallMs(spec.Ms, step);
      // The written length played at UiTween.Speed lasts exactly the speed's length.
      near(f(speed * wall), spec.Ms, 0.01, spec.Id);

      const runMs = f(f(RunFrames(spec.Ms, speed) * FrameSeconds) * 1000);
      const waitMs = f(f(WaitFrames(spec.Ms, speed) * FrameSeconds) * 1000);
      assert.ok(runMs >= f(wall - f(0.01)), 'Run ' + spec.Id);
      assert.ok(runMs <= f(f(wall + FrameMs) + f(0.01)), 'Run ' + spec.Id);
      assert.ok(waitMs >= f(wall - f(0.01)), 'Wait ' + spec.Id);
      assert.ok(waitMs <= f(f(wall + FrameMs) + f(0.01)), 'Wait ' + spec.Id);
    }

    // The gaps between events go through UiTween.Wait as well.
    const gap = f(f(WaitFrames(Audit.EffectFlow.AfterAutoEventMs, speed) * FrameSeconds) * 1000);
    assert.ok(gap >= f(Audit.EffectFlow.AfterAutoEventMsAt(step) - f(0.01)));
    assert.ok(gap <= f(f(Audit.EffectFlow.AfterAutoEventMsAt(step) + FrameMs) + f(0.01)));
  });
}

test('Halted_SettlesAtReducedMotion_WhateverTheSpeed', () => {
  for (const step of BattleSpeed.Steps) {
    assert.equal(BattleSpeed.TweenSpeed(step, true), BattleSpeed.HaltedTweenSpeed, step);
    assert.equal(BattleSpeed.TweenSpeed(step, false), BattleSpeed.TweenSpeed(step), step);
  }
  assert.equal(BattleSpeed.HaltedTweenSpeed, 1000);
});

test('TheSwitch_GoesSlowNormalFastAndRoundAgain_AndSaysItsSpeed', () => {
  assert.equal(BattleSpeed.Next(BattleSpeedStep.Slow), BattleSpeedStep.Normal);
  assert.equal(BattleSpeed.Next(BattleSpeedStep.Normal), BattleSpeedStep.Fast);
  assert.equal(BattleSpeed.Next(BattleSpeedStep.Fast), BattleSpeedStep.Slow);
  // battle-visual-v1 §4.1 (#389): the number only, no word.
  assert.equal(BattleSpeed.Label(BattleSpeedStep.Slow), '×1.0');
  assert.equal(BattleSpeed.Label(BattleSpeedStep.Normal), '×1.25');
  assert.equal(BattleSpeed.Label(BattleSpeedStep.Fast), '×1.5');
});

test('TheIcon_StacksOneTwoOrThreeChevrons_AndTheTooltipNamesTheThreeSteps', () => {
  // 付録 A 「速さ」: one chevron at ×1.0, two at ×1.25, three at ×1.5.
  assert.equal(BattleSpeed.Chevrons(BattleSpeedStep.Slow), 1);
  assert.equal(BattleSpeed.Chevrons(BattleSpeedStep.Normal), 2);
  assert.equal(BattleSpeed.Chevrons(BattleSpeedStep.Fast), 3);
  assert.equal(BattleSpeed.Tooltip, '戦闘の速さ。押すたびに ×1.0 → ×1.25 → ×1.5 と巡ります');
  // The tooltip walks the steps in the switch's order, written the way the button writes them.
  const order = BattleSpeed.Steps.map(BattleSpeed.Label).join(' → ');
  assert.ok(BattleSpeed.Tooltip.includes(order));
  for (const step of BattleSpeed.Steps) {
    assert.ok(!BattleSpeed.Label(step).includes('速さ') && !BattleSpeed.Label(step).includes('倍'), 'the switch shows no word');
  }
});

test('TheSavedSpeed_ComesBack_AndAnythingElseIsTheDefault', () => {
  for (const step of BattleSpeed.Steps) {
    assert.equal(BattleSpeed.Load(true, BattleSpeed.Save(step)), step, step);
  }
  assert.equal(BattleSpeed.Load(false, '1.5'), BattleSpeed.Default, 'nothing stored');
  assert.equal(BattleSpeed.Load(true, null), BattleSpeed.Default);
  assert.equal(BattleSpeed.Load(true, ''), BattleSpeed.Default);
  assert.equal(BattleSpeed.Load(true, '2'), BattleSpeed.Default);
  assert.equal(BattleSpeed.Load(true, '1,25'), BattleSpeed.Default, "a culture's decimal comma is not ours");
  assert.equal(BattleSpeed.Load(true, ' 1.5 '), BattleSpeedStep.Fast);
});

test('TheCap_IsTwoSecondsAtTheDefaultAndFaster_AndTwoAndAHalfAtOnePointZero', () => {
  near(BattleSpeed.EventCapMsAt(BattleSpeedStep.Slow), 2500, 0.01);
  assert.equal(BattleSpeed.EventCapMsAt(BattleSpeedStep.Normal), 2000);
  assert.equal(BattleSpeed.EventCapMsAt(BattleSpeedStep.Fast), 2000);
});

// ---- port-specific: the float values, C#'s Trim and .NET's messages (checked against the C#) ----

test('Port_TheFactorsAreFloat32_AsTheCsComputesThem', () => {
  assert.equal(BattleSpeed.TweenSpeed(BattleSpeedStep.Slow), f(0.8));
  assert.equal(BattleSpeed.TweenSpeed(BattleSpeedStep.Fast), f(1.2));
  assert.equal(BattleSpeed.WallMs(250, BattleSpeedStep.Fast), f(f(250 * 1.25) / 1.5));
  assert.equal(BattleSpeed.WallMs(333.3, BattleSpeedStep.Slow), f(f(f(333.3) * 1.25) / 1));
});

test('Port_LoadTrimsAsCsTrimDoes', () => {
  assert.equal(BattleSpeed.Load(true, '\u00851.25\u0085'), BattleSpeedStep.Normal, 'C# trims U+0085');
  assert.equal(BattleSpeed.Load(true, '﻿1.5'), BattleSpeedStep.Normal, 'C# keeps U+FEFF, so this is no saved speed');
  assert.equal(BattleSpeed.Load(true, '　1.0　'), BattleSpeedStep.Slow);
});

test('Port_AnUnknownStep_ThrowsWithDotNetsMessage', () => {
  assert.throws(() => BattleSpeed.Multiplier(7), { name: 'ArgumentOutOfRangeException', message: "No multiplier is written for this speed. (Parameter 'step')\nActual value was 7." });
  assert.throws(() => BattleSpeed.Label('X'), { name: 'ArgumentOutOfRangeException', message: "No label is written for this speed. (Parameter 'step')\nActual value was X." });
});
