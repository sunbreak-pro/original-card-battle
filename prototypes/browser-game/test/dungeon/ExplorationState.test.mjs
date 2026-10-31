// Tests for src/core/dungeon/ExplorationState.js.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { toParityJson, enumValues } from '../../src/shared/cs.js';
import { ExplorationState, RunPhase, RestChoice } from '../../src/core/dungeon/ExplorationState.js';
import { ExplorationReducer } from '../../src/core/dungeon/ExplorationReducer.js';
import { SevenLayers } from '../../src/core/dungeon/SevenLayers.js';
import { RunLoadout, ReliefItem } from '../../src/core/dungeon/RunLoadout.js';

const enter = (layer, loadout = RunLoadout.Empty) => {
  const p = SevenLayers.Of(layer);
  return ExplorationReducer.Enter(p, p.Map(11n), loadout, 50, 50, 10);
};

test('the enums keep the C# order', () => {
  assert.deepEqual(enumValues(RunPhase),
    ['Exploring', 'LayerCleared', 'PushedOut', 'Interlude', 'MiasmaDeath', 'Fallen', 'Completed', 'Survived']);
  assert.deepEqual(enumValues(RestChoice), ['Rest', 'Train']);
});

test('With copies, overriding only what is given (zero counts as given)', () => {
  const state = enter(1);
  const zeroed = state.With({ timeLeft: 0 });
  assert.equal(zeroed.TimeLeft, 0);
  assert.equal(zeroed.Hp, state.Hp);
  assert.equal(zeroed.Map, state.Map);
  assert.equal(state.TimeLeft, 9, 'the original is untouched');
  const same = state.With();
  assert.notEqual(same, state);
  assert.equal(same.Resolved, state.Resolved);
  assert.equal(state.With({ phase: null }).Phase, state.Phase, 'null keeps the value, like ??');
});

test('a temp modifier never pushes max stamina past its clamp', () => {
  assert.equal(enter(1).With({ tempMaxStaminaMod: 9 }).MaxStamina, 14);
});

test('effective density counts the tools', () => {
  const masked = new RunLoadout([ReliefItem.Density('bosho_no_men', 1)]);
  assert.equal(enter(7, masked).EffectiveDensity, 6);
  assert.equal(enter(1, masked).EffectiveDensity, 1);
});

test('IsOver for the four ending phases only', () => {
  const state = enter(1);
  const over = enumValues(RunPhase).filter((p) => state.With({ phase: p }).IsOver);
  assert.deepEqual(over, ['MiasmaDeath', 'Fallen', 'Completed', 'Survived']);
});

test('reachable unresolved nodes are the unresolved neighbours', () => {
  const state = enter(1);
  assert.deepEqual(state.ReachableUnresolved(), state.Map.Neighbours(state.CurrentNodeId).filter((id) => id !== state.Map.EntryId));
  assert.equal(state.HasResolved(state.Map.EntryId), true);
  const asArray = state.With({ resolved: [0, 1] });
  assert.equal(asArray.HasResolved(1), true, 'an array works as the collection too');
});

test('constructor nulls throw in the C# order', () => {
  const s = enter(1);
  const args = [s.Profile, s.Map, 0, new Set(), 1, 0, 1, 1, 1, 0, 0, s.Loadout, RunPhase.Exploring];
  for (const [i, name] of [[0, 'profile'], [1, 'map'], [3, 'resolved'], [11, 'loadout']]) {
    const broken = args.slice();
    broken[i] = null;
    assert.throws(() => new ExplorationState(...broken), { name: 'ArgumentNullException', message: `Value cannot be null. (Parameter '${name}')` });
  }
});

test('the parity JSON carries every public property', () => {
  const json = toParityJson(enter(3));
  assert.equal(json.$type, 'ExplorationState');
  assert.deepEqual(json.Resolved, [0]);
  assert.deepEqual([json.TimeLeft, json.MiasmaPercent, json.Phase, json.EffectiveDensity, json.MaxStamina, json.IsOver],
    [9, 2, 'Exploring', 2, 10, false]);
  assert.equal(json.Loadout.$type, 'RunLoadout');
  assert.equal(json.Map.$type, 'LayerMap');
});
