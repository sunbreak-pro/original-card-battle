// Tests for src/core/dungeon/SevenLayers.js — ports DungeonCore.Tests/SevenLayersTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { toParityJson } from '../../src/shared/cs.js';
import { SevenLayers, LayerProfile } from '../../src/core/dungeon/SevenLayers.js';
import { LayerMapSpec } from '../../src/core/dungeon/LayerMapSpec.js';
import { Miasma } from '../../src/core/dungeon/Miasma.js';
import { NodeKind, NodeKindTokens } from '../../src/core/dungeon/NodeKind.js';
import { ExplorationReducer } from '../../src/core/dungeon/ExplorationReducer.js';
import { RunPhase, RestChoice } from '../../src/core/dungeon/ExplorationState.js';
import { RunLoadout } from '../../src/core/dungeon/RunLoadout.js';
import { caught, expectedError } from './_helpers.mjs';

test('there are seven layers with the canon names', () => {
  assert.equal(SevenLayers.All.length, 7);
  assert.deepEqual(SevenLayers.All.map((l) => l.Name),
    ['燦光の樹海', '■■の秘跡', '大黒蛇の住処', '輝晶の間', '血染めの死海', '獄雷峡', '果てなき世界']);
});

test('the table matches the design doc', () => {
  assert.deepEqual(SevenLayers.All.map((l) => l.Density), [1, 1, 2, 3, 4, 5, 7]);
  assert.deepEqual(SevenLayers.All.map((l) => l.TimeLimit), [10, 10, 10, 7, 7, 6, 4]);
  assert.deepEqual(SevenLayers.All.map((l) => l.MapSpec.NodeCount), [11, 11, 11, 8, 8, 8, 6]);
  assert.deepEqual(SevenLayers.All.map((l) => l.ShortestPathCost), [5, 5, 5, 4, 4, 4, 3]);
});

test('every layer offers two nodes that move max stamina', () => {
  for (const layer of SevenLayers.All) {
    const map = layer.Map(7n);
    const rests = map.CountOf(NodeKind.Rest);
    const events = layer.Layer === SevenLayers.RootLayer ? map.CountOf(NodeKind.Event) : 0;
    assert.ok(rests + events >= 2, `layer ${layer.Layer}`);
  }
});

test('the deep layers widen a row instead of adding one', () => {
  assert.deepEqual(SevenLayers.Of(6).MapSpec.RowWidths, [1, 3, 3, 1]);
  assert.deepEqual(SevenLayers.Of(7).MapSpec.RowWidths, [1, 4, 1]);
  const middle = SevenLayers.Of(7).Map(7n).Row(1).map((n) => n.Kind).sort();
  assert.deepEqual(middle, [NodeKind.Battle, NodeKind.Elite, NodeKind.Rest, NodeKind.Event].sort());
});

test('no layer can be seen in full; a layer whose time limit covers every node is rejected', () => {
  for (const layer of SevenLayers.All) assert.ok(layer.TimeLimit < layer.MapSpec.NodeCount);
  assert.throws(() => new LayerProfile(1, 'test', null, 1, 6, LayerMapSpec.Minimal(1)), /out of reach/);
});

test('miasma totals: 89 / 147, 69 / 113 with one mask, 54 / 89 with two', () => {
  assert.equal(SevenLayers.BareMinimumMiasma(), 89);
  assert.equal(Miasma.MaxStamina(0, 89), 6);
  assert.equal(SevenLayers.FullSpendMiasma(), 147);
  assert.equal(SevenLayers.BareMinimumMiasma(1), 69);
  assert.equal(SevenLayers.FullSpendMiasma(1), 113);
  assert.equal(SevenLayers.BareMinimumMiasma(2), 54);
  assert.equal(SevenLayers.FullSpendMiasma(2), 89);
});

test('layer two hides its name until the carving is found; only layer two carries it', () => {
  const sacrament = SevenLayers.Of(SevenLayers.SacramentLayer);
  assert.equal(sacrament.DisplayName(false), '■■の秘跡');
  assert.equal(sacrament.DisplayName(true), '竜神の秘跡');
  assert.equal(SevenLayers.Of(1).DisplayName(true), '燦光の樹海');
  for (const layer of SevenLayers.All) {
    assert.equal(layer.Map(7n).CountOf(NodeKind.Carving), layer.Layer === SevenLayers.SacramentLayer ? 1 : 0);
  }
});

function routeTo(map, from, to) {
  const previous = new Map();
  const queue = [from];
  const seen = new Set([from]);
  while (queue.length) {
    const current = queue.shift();
    if (current === to) break;
    for (const next of map.Neighbours(current)) {
      if (seen.has(next)) continue;
      seen.add(next);
      previous.set(next, current);
      queue.push(next);
    }
  }
  const route = [];
  for (let at = to; at !== from; at = previous.get(at)) route.push(at);
  return route.reverse();
}

test('the carving is a detour and changes no combat value', () => {
  const sacrament = SevenLayers.Of(SevenLayers.SacramentLayer);
  for (let seed = 0; seed < 200; seed++) {
    const map = sacrament.Map(seed);
    const carvings = map.Nodes.filter((n) => n.Kind === NodeKind.Carving);
    assert.equal(carvings.length, 1);
    const carving = carvings[0];
    assert.ok(NodeKindTokens.IsDetour(carving.Kind));
    assert.notEqual(carving.Row, 0);
    assert.notEqual(carving.Row, map.RowCount - 1);

    let state = ExplorationReducer.Enter(sacrament, map, RunLoadout.Empty, 20, 30, 4);
    let before = null;
    let after = null;
    for (const id of routeTo(map, state.CurrentNodeId, carving.Id)) {
      if (state.Phase !== RunPhase.Exploring) break;
      const prev = state;
      state = ExplorationReducer.Step(state, id, RestChoice.Train);
      if (id === carving.Id) { before = prev; after = state; }
    }
    if (after === null) continue;
    assert.equal(after.CurrentNodeId, carving.Id);
    assert.equal(after.Hp, before.Hp);
    assert.equal(after.TempMaxStaminaMod, before.TempMaxStaminaMod);
    assert.equal(after.TrainingMarks, before.TrainingMarks);
    assert.equal(after.TimeLeft, before.TimeLeft - 1);
    assert.equal(after.MiasmaPercent, before.MiasmaPercent + sacrament.Density);
  }
});

test('every seed gives every layer a valid map', () => {
  for (const layer of SevenLayers.All) {
    for (let seed = 0; seed < 150; seed++) assert.doesNotThrow(() => layer.Map(seed), `layer ${layer.Layer} seed ${seed}`);
  }
});

test('the root and the rift sit on layer seven', () => {
  assert.equal(SevenLayers.RootLayer, 7);
  assert.equal(SevenLayers.Of(SevenLayers.RootLayer).IsLast, true);
  assert.equal(SevenLayers.Of(6).IsLast, false);
  assert.equal(SevenLayers.Count, 7);
});

test('the parity JSON of a profile carries its computed properties', () => {
  const json = toParityJson(SevenLayers.Of(2));
  assert.equal(json.$type, 'LayerProfile');
  assert.equal(json.TrueName, '竜神の秘跡');
  assert.equal(json.ShortestPathCost, 5);
  assert.equal(json.IsLast, false);
  assert.equal(json.MapSpec.$type, 'LayerMapSpec');
  assert.equal(toParityJson(SevenLayers.Of(1)).TrueName, null);
});

test('errors carry the C# messages', () => {
  assert.equal(caught(() => SevenLayers.Of(0)), expectedError('sevenLayers.of0'));
  assert.throws(() => SevenLayers.Of(8), { name: 'ArgumentOutOfRangeException' });
  assert.equal(caught(() => new LayerProfile(1, 'test', null, 1, 6, LayerMapSpec.Minimal(1))), expectedError('profile.timeLimit'));
  assert.equal(caught(() => new LayerProfile(2, 'test', null, 1, 5, LayerMapSpec.Minimal(1))), expectedError('profile.layerMismatch'));
  assert.equal(caught(() => new LayerProfile(0, 'test', null, 1, 5, LayerMapSpec.Minimal(0))), expectedError('profile.layer0'));
});
