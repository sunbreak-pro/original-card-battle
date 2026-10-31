// CrtPow.Pow (src/bridge/backdrop/CrtPow.js) is System.Math.Pow as the C# oracle computes it: glibc's
// pow. The fixture holds glibc 2.39's results (Python's math.pow calls the same libm) for the inputs the
// backdrop feeds it — 1200 of them ones where V8's Math.pow rounds the last bit differently — plus wide
// random inputs and the special cases of e_pow.c (zeros, infinities, NaN, negative x, subnormals, huge y).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';

import { Pow } from '../../src/bridge/backdrop/CrtPow.js';

const fixture = JSON.parse(fs.readFileSync(new URL('./CrtPow.fixture.json', import.meta.url), 'utf8'));
const dv = new DataView(new ArrayBuffer(8));
const d = (h) => {
  dv.setBigUint64(0, BigInt('0x' + h));
  return dv.getFloat64(0);
};

test('Pow_IsGlibcsPow_BitForBit', () => {
  let checked = 0;
  for (const [xh, yh, ph] of fixture.rows) {
    const x = d(xh);
    const y = d(yh);
    const want = d(ph);
    const got = Pow(x, y);
    assert.ok(Object.is(got, want), `pow(${x}, ${y}): glibc ${want}, port ${got}`);
    checked++;
  }
  assert.ok(checked > 2000);
});

test('Pow_TheFixtureHoldsCasesWhereMathPowDiffers', () => {
  // Guards the point of the helper: V8's own Math.pow would fail the fixture.
  let differs = 0;
  for (const [xh, yh, ph] of fixture.rows) if (!Object.is(Math.pow(d(xh), d(yh)), d(ph))) differs++;
  assert.ok(differs >= 1000, `${differs}`);
});

test('Pow_SpecialCases', () => {
  assert.equal(Pow(2, 10), 1024);
  assert.equal(Pow(0, 1.4), 0);
  assert.equal(Pow(1, 2.4), 1);
  assert.equal(Pow(-2, 3), -8);
  assert.ok(Number.isNaN(Pow(-2, 0.5)));
  assert.equal(Pow(0, -1), Infinity);
  assert.equal(Pow(-0, -1), -Infinity);
  assert.equal(Pow(NaN, 0), 1);
  assert.equal(Pow(1, NaN), 1);
  assert.equal(Pow(0.5, Infinity), 0);
  assert.equal(Pow(2, -Infinity), 0);
  assert.equal(Pow(2, 2000), Infinity);
  assert.equal(Pow(2, -2000), 0);
});
