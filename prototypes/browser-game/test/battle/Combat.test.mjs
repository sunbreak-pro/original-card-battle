// Tests for src/core/battle/Combat.js (port of unity-port/BattleCore/Combat.cs). Ports CombatTests,
// DepartureToolTests' RecoverStamina row and PolearmTests' CellsAfterSlow rows.
import { test as nodeTest } from 'node:test';
import assert from 'node:assert/strict';
import { Constants } from '../../src/core/battle/Constants.js';
import { loadDeps } from './_foundation_deps.mjs';

// Combat.js imports Statuses.js (CellsAfterSlow); skip with a reason while that module is missing.
const { mods, skip: SKIP } = await loadDeps(import.meta.url, ['../../src/core/battle/Combat.js']);
const test = (name, opts, fn) => (fn === undefined ? nodeTest(name, { skip: SKIP }, opts) : nodeTest(name, { skip: SKIP || opts.skip }, fn));
const { Combat } = mods ?? {};
const StatusKind = { Slow: 'Slow', Bleed: 'Bleed' }; // the two StatusKind members used below (values are the names)

test('RawPower_IsTheFacePlusTheTrait', () => {
  // 払い斬り against a player 2 or more cells away: 8 + 3.
  assert.equal(Combat.ComputeRawPower(8, 3), 11);
});

test('RawPower_IsJustTheFace_WhenNoTraitFired', () => {
  assert.equal(Combat.ComputeRawPower(8), 8);
});

test('RawPower_AddsBeforeItMultiplies', () => {
  // (6 + 4) × 1.5 = 15, not 6 + (4 × 1.5).
  assert.equal(Combat.ComputeRawPower(6, 4, undefined, undefined, 1.5), 15);
});

test('RawPower_RoundsHalfAwayFromZero', () => {
  // 5 × 1.5 = 7.5 → 8, not the banker's 7 that the design tables would not read as.
  assert.equal(Combat.ComputeRawPower(5, 0, 0, 0, 1.5), 8);
  // 3 × 1.5 = 4.5 → 5.
  assert.equal(Combat.ComputeRawPower(3, 0, 0, 0, 1.5), 5);
  // C# (dotnet) gives 0 for a negative raw: −4.5 rounds to −5, then the floor 0.
  assert.equal(Combat.ComputeRawPower(-3, 0, 0, 0, 1.5), 0);
  // 7 × 1.5 = 10.5 → 11; 9 × 1.5 = 13.5 → 14 (banker's would give 10 and 14).
  assert.equal(Combat.ComputeRawPower(7, 0, 0, 0, 1.5), 11);
  assert.equal(Combat.ComputeRawPower(9, 0, 0, 0, 1.5), 14);
});

test('RawPower_NeverGoesBelowZero', () => {
  assert.equal(Combat.ComputeRawPower(2, -5), 0);
  assert.ok(Object.is(Combat.ComputeRawPower(2, -5), 0), 'never −0');
});

test('ApplyGuard_SubtractsGuardAndSpendsIt', () => {
  const { Damage: damage, GuardAfter: guardAfter, Absorbed: absorbed } = Combat.ApplyGuard(11, 4);
  assert.equal(damage, 7);
  assert.equal(guardAfter, 0);
  assert.equal(absorbed, 4);
});

test('ApplyGuard_StopsAtZeroDamageWhenGuardCovers', () => {
  const { Damage: damage, GuardAfter: guardAfter, Absorbed: absorbed } = Combat.ApplyGuard(5, 9);
  assert.equal(damage, 0);
  assert.equal(guardAfter, 4);
  assert.equal(absorbed, 5);
});

for (const [staminaLeft, expected] of [[0, 0], [2, 0], [3, 3], [9, 3]]) {
  test(`ReserveGuard_PaysThreeOnlyAtThreeStaminaOrMore(${staminaLeft}, ${expected})`, () => {
    assert.equal(Combat.ReserveGuard(staminaLeft), expected);
  });
}

test('RecoverStamina_AddsThreeAndCapsAtTheMaximum', () => {
  assert.equal(Combat.RecoverStamina(4, 10, Constants.StaminaRecovery), 7);
  assert.equal(Combat.RecoverStamina(9, 10, Constants.StaminaRecovery), 10);
});

test('RecoverStamina_TakesTheBonusTheReserveTraitLeftBehind', () => {
  assert.equal(Combat.RecoverStamina(2, 10, 2, 1), 5);
});

test('RecoverStamina: stamina past the maximum (息継ぎの笛) is kept as it is (DepartureToolTests)', () => {
  assert.equal(Combat.RecoverStamina(13, 10, 3), 13);
  assert.equal(Combat.RecoverStamina(10, 10, 3), 10);
  // A negative recovery never takes stamina away: max(0, recovery + bonus).
  assert.equal(Combat.RecoverStamina(4, 10, -2), 4);
});

for (const [modifier, expected] of [[0, 6], [1, 7], [2, 8], [-2, 4], [-3, 3], [-9, 3], [9, 8]]) {
  test(`DrawCount_IsSixPlusTheModifier_ClampedToThreeAndEight(${modifier}, ${expected})`, () => {
    assert.equal(Combat.DrawCount(modifier), expected);
  });
}

test('DrawCount defaults to 6', () => {
  assert.equal(Combat.DrawCount(), 6);
});

test('ClampMaxStamina_StaysWithinThreeAndFourteen', () => {
  assert.equal(Combat.ClampMaxStamina(1), 3);
  assert.equal(Combat.ClampMaxStamina(10), 10);
  assert.equal(Combat.ClampMaxStamina(20), 14);
});

test('IsDefeated_TripsTheMomentHpReachesZero', () => {
  assert.equal(Combat.IsDefeated(1), false);
  assert.equal(Combat.IsDefeated(0), true);
  assert.equal(Combat.IsDefeated(-4), true);
});

test('CellsAfterSlow refuses a null status set like C#', () => {
  assert.throws(() => Combat.CellsAfterSlow(2, null), (e) =>
    e.name === 'ArgumentNullException' && e.message === "Value cannot be null. (Parameter 'moverStatuses')");
});

// PolearmTests: a slowed mover's push 2 covers 1, its step 1 covers 0. Needs StatusSet from Statuses.js.
let statusSetOf = null;
try {
  const { StatusSet } = await import('../../src/core/battle/Statuses.js');
  // StatusSet.Of(params (StatusKind Kind, int Stacks)[]) — accept either tuple shape the port chose.
  statusSetOf = (kind, stacks) => {
    try {
      const s = StatusSet.Of({ Kind: kind, Stacks: stacks });
      if (s.Stacks(kind) === stacks) return s;
    } catch { /* fall through */ }
    return StatusSet.Of([kind, stacks]);
  };
} catch { /* Statuses.js not there yet */ }

test('CellsAfterSlow: the mover\'s own 鈍足 takes one cell off, floor 0', { skip: statusSetOf ? false : 'needs Statuses.js' }, () => {
  const slowed = statusSetOf(StatusKind.Slow, 1);
  assert.equal(Combat.CellsAfterSlow(2, slowed), 1);
  assert.equal(Combat.CellsAfterSlow(1, slowed), 0);
  assert.equal(Combat.CellsAfterSlow(0, slowed), 0);
  assert.equal(Combat.CellsAfterSlow(2, statusSetOf(StatusKind.Bleed, 2)), 2, 'another word takes nothing off');
});

// 2000 random rows of ComputeRawPower written by the C# code under dotnet 10 (multipliers 0.5 / 1 / 1.5).
import { readFileSync } from 'node:fs';
const RAW = JSON.parse(readFileSync(new URL('./Combat.fixture.json', import.meta.url), 'utf8'));

test('ComputeRawPower: 2000 rows match the C# fixture (AwayFromZero rounding, floor 0)', () => {
  for (const [face, traitBonus, followUp, conversion, mult, expected] of RAW.rows) {
    assert.equal(Combat.ComputeRawPower(face, traitBonus, followUp, conversion, mult), expected, `${face},${traitBonus},${followUp},${conversion},${mult}`);
  }
});
