// Tests for src/core/dungeon/RunLoadout.js — the slot cases of ExplorationReducerTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { RunLoadout, ReliefItem, ReliefKind } from '../../src/core/dungeon/RunLoadout.js';
import { caught, expectedError } from './_helpers.mjs';

test('there are three tool slots and three consumable slots', () => {
  const four = [0, 1, 2, 3].map((i) => ReliefItem.Density(`t${i}`, 1));
  assert.throws(() => new RunLoadout(four), { name: 'ArgumentException' });
  assert.doesNotThrow(() => new RunLoadout(four, null, RunLoadout.SurvivorBonusToolSlots));
  const fourConsumables = [0, 1, 2, 3].map((i) => ReliefItem.Gauge(`c${i}`, 10));
  assert.throws(() => new RunLoadout(null, fourConsumables), { name: 'ArgumentException' });
  assert.equal(RunLoadout.SurvivorBonusToolSlots, 4);
});

test('density relief sums the tools', () => {
  const run = new RunLoadout([ReliefItem.Density('a', 1), ReliefItem.Density('b', 2)]);
  assert.equal(run.DensityRelief, 3);
  assert.equal(RunLoadout.Empty.DensityRelief, 0);
  assert.equal(RunLoadout.Empty.ToolSlotCount, 3);
});

test('spending a consumable reports its gauge relief and leaves the original alone', () => {
  const run = new RunLoadout(null, [ReliefItem.Gauge('joka_no_ko', 10), ReliefItem.Density('odd', 2)]);
  const first = run.Spend(0);
  assert.equal(first.gaugeRelief, 10);
  assert.deepEqual(first.value.Consumables.map((c) => c.Id), ['odd']);
  assert.equal(run.Consumables.length, 2);
  const second = first.value.Spend(0);
  assert.equal(second.gaugeRelief, 0, 'a density item in a consumable slot pulls nothing off the gauge');
  assert.equal(second.value.Consumables.length, 0);
});

test('records compare by value and copy with with()', () => {
  const a = ReliefItem.Gauge('joka_no_ko', 10);
  assert.ok(a.equals(new ReliefItem('joka_no_ko', ReliefKind.Gauge, 10)));
  assert.ok(!a.equals(a.with({ Amount: 11 })));
  assert.equal(String(a), 'ReliefItem { Id = joka_no_ko, Kind = Gauge, Amount = 10 }');
});

test('errors carry the C# messages', () => {
  assert.equal(caught(() => new RunLoadout([0, 1, 2, 3].map((i) => ReliefItem.Density(`t${i}`, 1)))), expectedError('loadout.tools'));
  assert.equal(caught(() => new RunLoadout(null, [0, 1, 2, 3].map((i) => ReliefItem.Gauge(`c${i}`, 10)))), expectedError('loadout.consumables'));
  assert.equal(caught(() => new RunLoadout([ReliefItem.Gauge('x', 1)])), expectedError('loadout.gaugeTool'));
  assert.equal(caught(() => new RunLoadout(null, null, -1)), expectedError('loadout.negativeSlots'));
  assert.equal(caught(() => RunLoadout.Empty.Spend(0)), expectedError('loadout.spend'));
});
