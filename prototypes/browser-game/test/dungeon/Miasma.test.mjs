// Tests for src/core/dungeon/Miasma.js — ports DungeonCore.Tests/MiasmaTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Miasma } from '../../src/core/dungeon/Miasma.js';
import { caught, expectedError } from './_helpers.mjs';

test('one point of max stamina per full twenty percent', () => {
  const cases = [[0, 0], [19, 0], [20, 1], [39, 1], [40, 2], [59, 2], [60, 3], [80, 4], [99, 4], [100, 4]];
  for (const [percent, expected] of cases) assert.equal(Miasma.MaxStaminaPenalty(percent), expected, `${percent}%`);
});

test('eighty percent is max stamina six', () => {
  assert.equal(Miasma.MaxStamina(0, 80), 6);
});

test('temporary modifiers clamp at four', () => {
  assert.equal(Miasma.MaxStamina(9, 0), 14);
  assert.equal(Miasma.MaxStamina(-9, 0), 6);
  assert.equal(Miasma.MaxStamina(-9, 80), 3);
});

test('max stamina stays inside its band', () => {
  for (let temp = -8; temp <= 8; temp++) {
    for (let percent = 0; percent <= 100; percent++) {
      const v = Miasma.MaxStamina(temp, percent);
      assert.ok(v >= 3 && v <= 14);
    }
  }
});

test('relief never makes a time unit free', () => {
  assert.equal(Miasma.EffectiveDensity(1, 1), 1);
  assert.equal(Miasma.EffectiveDensity(1, 5), 1);
  assert.equal(Miasma.EffectiveDensity(7, 1), 6);
  assert.equal(Miasma.EffectiveDensity(3, 0), 3);
  assert.equal(Miasma.EffectiveDensity(3, -2), 3, 'negative relief counts as none');
});

test('the gauge stops at one hundred, and at zero when relieved', () => {
  assert.equal(Miasma.Accumulate(98, 7), 100);
  assert.equal(Miasma.IsLethal(Miasma.Accumulate(98, 7)), true);
  assert.equal(Miasma.IsLethal(99), false);
  assert.equal(Miasma.Relieve(5, 10), 0);
  assert.equal(Miasma.Relieve(35, 10), 25);
  assert.equal(Miasma.Accumulate(-5, 3), 3);
});

test('constants', () => {
  assert.deepEqual(
    [Miasma.DeathAt, Miasma.StepPercent, Miasma.MaxPenalty, Miasma.DensityFloor, Miasma.BaseMaxStamina,
      Miasma.MaxStaminaFloor, Miasma.MaxStaminaCeil, Miasma.TempModClamp, Miasma.FieldRecovery],
    [100, 20, 4, 1, 10, 3, 14, 4, 3]);
});

test('a negative layer density throws the C# message', () => {
  assert.equal(caught(() => Miasma.EffectiveDensity(-1, 0)), expectedError('miasma.negative'));
});
