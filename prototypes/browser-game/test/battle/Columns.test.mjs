// Tests for src/core/battle/Columns.js (port of unity-port/BattleCore/Columns.cs). Ports ColumnTests.
import { test as nodeTest } from 'node:test';
import assert from 'node:assert/strict';
import { Columns } from '../../src/core/battle/Columns.js';
import { loadDeps } from './_foundation_deps.mjs';

// Columns.js itself needs only Constants.js. The rows that build a CardDef or call Combat need
// Types.js / Combat.js, which import Statuses.js: those skip with a reason while it is missing.
const { mods, skip: SKIP } = await loadDeps(import.meta.url, ['../../src/core/battle/Combat.js', '../../src/core/battle/Types.js']);
const testWithTypes = (name, fn) => nodeTest(name, { skip: SKIP }, fn);
const test = nodeTest;
const { Combat, CardDef, EnemyActionDef, Face, BattleAttribute, TargetKind } = mods ?? {};

// Fixtures.Card / Fixtures.EnemyAction
const card = (id, column = 1, face = null, attributes = BattleAttribute.Attack, trait = null, targets = TargetKind.One) =>
  new CardDef(id, id, attributes, column, face ?? new Face(), trait, targets, '');
const enemyAction = (id, column = 1, face = null, attributes = BattleAttribute.Attack, trait = null, targets = TargetKind.One) =>
  new EnemyActionDef(id, id, attributes, column, face ?? new Face(), trait, targets, '');

for (const [column, expected] of [[1, 1], [2, 2], [3, 3], [4, 3]]) {
  test(`CostOf_IsTheColumnNumber_ExceptColumnFourWhichStaysAtThree(${column}, ${expected})`, () => {
    assert.equal(Columns.CostOf(column), expected);
  });
}

for (const column of [0, 5, -1]) {
  test(`CostOf_RejectsColumnsOutsideTheTable(${column})`, () => {
    assert.throws(() => Columns.CostOf(column), (e) => e.name === 'ArgumentOutOfRangeException');
  });
}

test('Validate: the .NET message is kept', () => {
  assert.throws(() => Columns.Validate(5), (e) =>
    e.name === 'ArgumentOutOfRangeException' && e.message === "Column must be 1..4. (Parameter 'column')\nActual value was 5.");
  assert.equal(Columns.IsValid(1), true);
  assert.equal(Columns.IsValid(4), true);
  assert.equal(Columns.IsValid(0), false);
  assert.equal(Columns.IsValid(5), false);
});

test('Scales_MatchTheCanonTable', () => {
  // The guard and skill rows are §3.1's. The two attack rows are not (#364, #451): they are
  // the v4.4 rows that §5 keeps as the base of what 集中 adds, +7 / +8 / +9 and +4 / +6 / +6.
  assert.deepEqual([...Columns.SingleAttackPower], [6, 13, 21, 30]);
  assert.deepEqual([...Columns.SingleGuard], [4, 9, 15, 22]);
  assert.deepEqual([...Columns.DualAttackPower], [4, 8, 14, 20]);
  assert.deepEqual([...Columns.DualGuard], [3, 6, 10, 14]);
  assert.deepEqual([...Columns.SingleSkillKinds], [1, 2, 3, 3]);
  assert.deepEqual([...Columns.SingleHeal], [3, 8, 13, 19]);
  assert.deepEqual([...Columns.DualHeal], [2, 5, 8, 12]);
  assert.deepEqual([...Columns.TickStacks], [1, 2, 3, 4]);
  assert.equal(Columns.Min, 1);
  assert.equal(Columns.Max, 4);
  assert.equal(Columns.DualFaceRatio, 0.65);
});

test('Value_ReadsColumnsOneBased', () => {
  // Any scale reads the same way; this one is §5's 集中 base, not §3.1's attack row.
  assert.equal(Columns.Value(Columns.SingleAttackPower, 1), 6);
  assert.equal(Columns.Value(Columns.SingleAttackPower, 4), 30);
  assert.throws(() => Columns.Value(null, 1), (e) => e.name === 'ArgumentNullException' && e.message === "Value cannot be null. (Parameter 'scale')");
});

test('StepRight: +7 / +8 / +9 on the lone attack base, +4 / +6 / +6 on the dual, 0 at column 4', () => {
  assert.deepEqual([1, 2, 3, 4].map((c) => Columns.StepRight(Columns.SingleAttackPower, c)), [7, 8, 9, 0]);
  assert.deepEqual([1, 2, 3, 4].map((c) => Columns.StepRight(Columns.DualAttackPower, c)), [4, 6, 6, 0]);
  assert.deepEqual([1, 2, 3, 4].map((c) => Columns.StepRight(Columns.TickStacks, c)), [1, 1, 1, 0]);
  assert.throws(() => Columns.StepRight(Columns.SingleGuard, 0), (e) => e.name === 'ArgumentOutOfRangeException');
});

testWithTypes('CardDef_TakesItsCostFromItsColumn', () => {
  const c = card('sweep_test', 2, new Face({ Power: 13 }));
  assert.equal(c.Cost, 2);
});

testWithTypes('EnemyActionDef_TakesItsCostFromTheSameTable', () => {
  const action = enemyAction('shove_test', 2, new Face({ Power: 5, Push: 2 }));
  assert.equal(action.Cost, 2);
});

testWithTypes('CanPay_RefusesACardTheStaminaDoesNotCover', () => {
  const c = card('heavy', 3, new Face({ Power: 21 }));
  assert.equal(Combat.CanPay(c.Cost, 2), false);
  assert.equal(Combat.CanPay(c.Cost, 3), true);
});

test('MoveCards_SitInColumnOne', () => {
  // §2.1: a single-attribute move card is cost 1 whatever it carries.
  assert.equal(Columns.CostOf(Columns.MoveCardColumn), 1);
});
