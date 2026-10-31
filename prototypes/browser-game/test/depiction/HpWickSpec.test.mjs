// Tests for src/bridge/depictionScript/HpWickSpec.js (port of View/Depiction/Script/HpWickSpec.cs).
// Ported cases: Assets/View/Depiction/Tests/HpWickSpecTests.cs (all).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { HpWickSpec } from '../../src/bridge/depictionScript/HpWickSpec.js';

const near = (a, b, tol, msg) => assert.ok(Math.abs(a - b) <= tol, `${msg || ''} expected ${b} ± ${tol}, got ${a}`);

test('TheStripe_IsSection4_5s', () => {
  assert.equal(HpWickSpec.AngleDegrees, 135);
  assert.equal(HpWickSpec.StripeWidth, 3);
  assert.equal(HpWickSpec.Gap, 3);
  assert.equal(HpWickSpec.ColorHex, '#ffd89a');
});

test('TheTile_DrawsTheSpacingWithinTwoHundredthsOfAPixel', () => {
  assert.equal(HpWickSpec.TileSize, 17);
  near(HpWickSpec.TilePeriod, HpWickSpec.Period, 0.02);
});

test('TheStripes_RunUpToTheRight', () => {
  // 135° in CSS: constant along x − y with y upwards, so a step right and up stays on a stripe.
  const tile = HpWickSpec.TileSize;
  for (let x = 0; x < tile; x++) {
    for (let y = 0; y < tile; y++) {
      near(HpWickSpec.Alpha(x + 1, y + 1), HpWickSpec.Alpha(x, y), 1e-5, `(${x}, ${y})`);
    }
  }
});

test('ARowOfTheTile_CrossesTwoStripesAndIsHalfWick', () => {
  const tile = HpWickSpec.TileSize;
  let rises = 0;
  let sum = Math.fround(0);
  for (let x = 0; x < tile; x++) {
    const here = HpWickSpec.Alpha(x, 0);
    const before = HpWickSpec.Alpha(x - 1, 0);
    if (before < 0.5 && here >= 0.5) rises++;
    sum = Math.fround(sum + here);
  }
  assert.equal(rises, HpWickSpec.PeriodsPerTile, 'one stripe for each 6 px period');
  near(sum / tile, 0.5, 0.06, '3 px of wick for every 3 px of gap');
});

test('TheTile_RepeatsWithoutASeam', () => {
  const tile = HpWickSpec.TileSize;
  for (let y = 0; y < tile; y++) {
    near(HpWickSpec.Alpha(tile, y), HpWickSpec.Alpha(0, y), 1e-5);
    near(HpWickSpec.Alpha(y, tile), HpWickSpec.Alpha(y, 0), 1e-5);
  }
});

test('TheStripe_CoversTheHpTheCardTakesOff', () => {
  // 30 of 40 HP, 10 taken off: the bar is filled to 0.75 and the stripe runs back to 0.5.
  const { ok, from, to } = HpWickSpec.Span(30, 40, 10);
  assert.equal(ok, true);
  assert.equal(from, 0.5);
  assert.equal(to, 0.75);
});

test('ALossOfAllTheHp_RunsTheStripeToTheBarsLeftEnd', () => {
  const { ok, from, to } = HpWickSpec.Span(12, 40, 12);
  assert.equal(ok, true);
  assert.equal(from, 0);
  near(to, 0.3, 1e-6);
});

test('ALossOfZero_DrawsNoStripe', () => {
  const { ok, from, to } = HpWickSpec.Span(30, 40, 0);
  assert.equal(ok, false);
  assert.equal(to - from, 0);
  assert.equal(HpWickSpec.Span(30, 40, -2).ok, false, 'nothing the script could write draws a stripe below 0');
  assert.equal(HpWickSpec.Span(0, 40, 5).ok, false, 'an empty bar has nothing to stripe');
});

// ---- port-specific (checked against the C#) ----

test('Port_TheTilesValues', () => {
  assert.equal(HpWickSpec.Period, 6);
  assert.equal(HpWickSpec.TilePeriod, Math.fround(17 / (2 * 1.4142135623730951)));
  assert.equal(HpWickSpec.Span(12, 40, 12).to, Math.fround(12 / 40));
  assert.deepEqual(HpWickSpec.Span(5, 40, 10), { ok: true, from: 0, to: Math.fround(5 / 40) });
  assert.equal(HpWickSpec.Alpha(-3, 5), HpWickSpec.Alpha(-3 + 17, 5), 'a negative texel wraps like a positive one');
});
