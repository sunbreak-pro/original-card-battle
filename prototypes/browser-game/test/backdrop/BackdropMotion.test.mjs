// Port of unity-project-kit/Assets/View/Backdrop/Tests/BackdropMotionTests.cs (#89: a light's breath, a
// flash, the drifting specks and the 400 ms crossfade), plus the oracle's `motion` case.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { BackdropMotion } from '../../src/bridge/backdrop/BackdropMotion.js';
import { BackdropSpec, Flicker } from '../../src/bridge/backdrop/BackdropSpec.js';
import { oracleMissing, diffCase } from './_oracle.mjs';

function within(actual, expected, tol, msg) {
  assert.ok(Math.abs(actual - expected) <= tol, `${msg ?? ''} expected ${expected} ± ${tol}, got ${actual}`);
}
function inRange(v, lo, hi, msg) {
  assert.ok(v >= lo && v <= hi, `${msg ?? ''} expected ${v} in [${lo}, ${hi}]`);
}

test('AStillLight_StaysFull', () => {
  assert.equal(BackdropMotion.FlickerAlpha(Flicker.Still, 1234), 1);
});

test('ABreath_DipsByItsDepthHalfwayAndComesBack', () => {
  const breath = new Flicker(4000, 0.4);
  within(BackdropMotion.FlickerAlpha(breath, 0), 1, 1e-5);
  within(BackdropMotion.FlickerAlpha(breath, 2000), 0.6, 1e-5);
  within(BackdropMotion.FlickerAlpha(breath, 4000), 1, 1e-5);
  inRange(BackdropMotion.FlickerAlpha(breath, 1000), Math.fround(0.6), 1);
});

test('AFlash_IsLitOnlyAtTheStartOfItsPeriod', () => {
  const lightning = new Flicker(5200, 1, true);
  within(BackdropMotion.FlickerAlpha(lightning, 0), 1, 1e-5);
  within(BackdropMotion.FlickerAlpha(lightning, BackdropMotion.FlashMs / 2.0), 0.5, 1e-5);
  assert.equal(BackdropMotion.FlickerAlpha(lightning, 1000), 0);
  within(BackdropMotion.FlickerAlpha(lightning, 5200), 1, 1e-5);
});

test('TheCrossfade_Takes400msAndLandsSoftly', () => {
  assert.equal(BackdropMotion.Crossfade(-10), 0);
  assert.equal(BackdropMotion.Crossfade(0), 0);
  within(BackdropMotion.Crossfade(200), 0.5, 1e-5);
  assert.equal(BackdropMotion.Crossfade(BackdropSpec.CrossfadeMs), 1);
  assert.ok(BackdropMotion.Crossfade(390) < 1);
  assert.ok(BackdropMotion.Crossfade(390) - BackdropMotion.Crossfade(380) < BackdropMotion.Crossfade(210) - BackdropMotion.Crossfade(200));
});

test('ASpeck_StaysOnTheScreenAndMovesTheSameWayEveryRun', () => {
  const motes = BackdropSpec.For(7).Motes;
  for (let i = 0; i < motes.Count; i++) {
    for (let s = 0; s < 120; s += 7) {
      const { x, y } = BackdropMotion.MoteAt(motes, i, s * 1000.0, 1920, 1080);
      inRange(x, 0, 1920);
      inRange(y, 0, 1080);
      const again = BackdropMotion.MoteAt(motes, i, s * 1000.0, 1920, 1080);
      assert.equal(again.x, x);
      assert.equal(again.y, y);
    }
  }
});

test('TheMiasmaOfLayer7_Rises', () => {
  const motes = BackdropSpec.For(7).Motes;
  const before = BackdropMotion.MoteAt(motes, 0, 0, 1920, 100000).y;
  const after = BackdropMotion.MoteAt(motes, 0, 2000, 1920, 100000).y;
  assert.ok(after < before);
});

test('TheSpecks_StartSpreadAcrossTheScreen', () => {
  const motes = BackdropSpec.For(1).Motes;
  let left = 0;
  for (let i = 0; i < motes.Count; i++) {
    if (BackdropMotion.MoteAt(motes, i, 0, 1920, 1080).x < 960) left++;
  }
  inRange(left, Math.trunc(motes.Count / 4), Math.trunc((motes.Count * 3) / 4));
});

test('ATwinkle_DipsNoFurtherThanItsDepth', () => {
  const motes = BackdropSpec.For(4).Motes;
  for (let ms = 0; ms < 10000; ms += 137) {
    inRange(BackdropMotion.MoteAlpha(motes, 3, ms), 1 - motes.Twinkle - 1e-5, 1 + 1e-5);
  }
});

// ---- port-specific ----

test('Hash_IsTheCSharpUintMix', () => {
  // uint wraparound, negative and int.MaxValue indices (values from the C# oracle's motion case).
  assert.equal(BackdropMotion.Hash(0, 0), 0);
  assert.equal(BackdropMotion.Hash(1, 1), BackdropMotion.Hash(1, 1));
  for (const [index, salt] of [[-3, 5], [2147483647, 1], [100, 0]]) {
    const h = BackdropMotion.Hash(index, salt);
    assert.ok(h >= 0 && h < 1, `${index},${salt}`);
  }
});

test('Oracle_Motion_MatchesTheCSharp', { skip: oracleMissing ?? false }, () => {
  assert.equal(diffCase('motion'), null);
});
