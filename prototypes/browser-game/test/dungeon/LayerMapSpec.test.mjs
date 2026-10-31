// Tests for src/core/dungeon/LayerMapSpec.js — ports DungeonCore.Tests/LayerMapSpecTests.cs, and pins
// the C# Dictionary enumeration order With() leaves behind (fixtures/specs.json).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { toParityJson } from '../../src/shared/cs.js';
import { LayerMapSpec } from '../../src/core/dungeon/LayerMapSpec.js';
import { MapGenerator } from '../../src/core/dungeon/MapGenerator.js';
import { NodeKind, NodeKindTokens } from '../../src/core/dungeon/NodeKind.js';
import { SevenLayers } from '../../src/core/dungeon/SevenLayers.js';
import { fixture, caught, expectedError } from './_helpers.mjs';

const quotas = (spec) => [...spec.Quotas].map(([k, v]) => `${k}=${v}`).join(',');

test('a spec without a single entry row is rejected', () => {
  assert.throws(() => new LayerMapSpec(1, [2, 2, 1], new Map([[NodeKind.Battle, 3]])), { name: 'ArgumentException' });
});

test('a spec whose quotas do not fill the map is rejected', () => {
  assert.throws(() => new LayerMapSpec(1, [1, 2, 2, 1], new Map([[NodeKind.Battle, 2]])), /middle nodes/);
});

test('a spec that would force a detour is rejected', () => {
  assert.throws(() => new LayerMapSpec(1, [1, 2, 1], new Map([[NodeKind.Elite, 2]])), /avoidable/);
});

test('the boss cannot be asked for by quota', () => {
  assert.throws(() => new LayerMapSpec(1, [1, 2, 2, 1], new Map([[NodeKind.Boss, 1], [NodeKind.Battle, 3]])), { name: 'ArgumentException' });
});

test('With trades a battle for another kind', () => {
  const full = LayerMapSpec.Full(2);
  const spec = full.With(NodeKind.Carving, 1);
  assert.equal(spec.Quotas.get(NodeKind.Carving), 1);
  assert.equal(spec.Quotas.get(NodeKind.Battle), full.Quotas.get(NodeKind.Battle) - 1);
  assert.equal(spec.NodeCount, full.NodeCount);
});

test('With refuses to trade more battles than there are', () => {
  assert.throws(() => LayerMapSpec.Minimal().With(NodeKind.Trace, 3), { name: 'ArgumentException' });
});

test('the carving can be placed and is still avoidable', () => {
  const spec = LayerMapSpec.Full(2).With(NodeKind.Carving, 1);
  const map = MapGenerator.Generate(spec, 41n);
  assert.equal(map.CountOf(NodeKind.Carving), 1);
  const carving = map.Nodes.filter((n) => n.Kind === NodeKind.Carving);
  assert.equal(carving.length, 1);
  assert.notEqual(carving[0].Row, 0);
  assert.notEqual(carving[0].Row, map.RowCount - 1);
  assert.ok(map.Row(carving[0].Row).some((n) => !NodeKindTokens.IsDetour(n.Kind)));
});

test('the minimal and full specs have the design doc\'s numbers', () => {
  const min = LayerMapSpec.Minimal();
  assert.deepEqual([min.NodeCount, min.MiddleNodeCount, min.RowCount, min.ShortestPathCost, min.FullSweepCost], [6, 4, 4, 4, 6]);
  const full = LayerMapSpec.Full(1);
  assert.ok(full.FullSweepCost > 10);
  assert.equal(full.ShortestPathCost, 5);
  assert.equal(full.ExtraEdgeChance, LayerMapSpec.DefaultExtraEdgeChance);
});

test('quota order after With matches the C# Dictionary (freed slots are reused)', () => {
  const expected = fixture('specs');
  const noBattle = new LayerMapSpec(1, [1, 2, 2, 1], new Map([[NodeKind.Rest, 2], [NodeKind.Survey, 2]]));
  const actual = {
    'full2.carving': quotas(LayerMapSpec.Full(2).With(NodeKind.Carving, 1)),
    'minimal.battle0': quotas(LayerMapSpec.Minimal().With(NodeKind.Battle, 0)),
    'minimal.battle5': quotas(LayerMapSpec.Minimal().With(NodeKind.Battle, 5)),
    'minimal.survey0': quotas(LayerMapSpec.Minimal().With(NodeKind.Survey, 0)),
    'minimal.trace1': quotas(LayerMapSpec.Minimal().With(NodeKind.Trace, 1)),
    'noBattle.rest0': quotas(noBattle.With(NodeKind.Rest, 0)),
    'noBattle.survey1': quotas(noBattle.With(NodeKind.Survey, 1)),
    'full3.event0.carving1': quotas(LayerMapSpec.Full(3).With(NodeKind.Event, 0).With(NodeKind.Carving, 1)),
    'full3.battle0': quotas(LayerMapSpec.Full(3).With(NodeKind.Battle, 0)),
  };
  for (const p of SevenLayers.All) actual[`layer${p.Layer}`] = quotas(p.MapSpec);
  assert.deepEqual(actual, expected);
});

test('With(Battle, n) gives the battle count back unchanged (C# quirk, ported)', () => {
  assert.equal(LayerMapSpec.Minimal().With(NodeKind.Battle, 5).Quotas.get(NodeKind.Battle), 1);
});

test('the parity JSON writes quotas as an object keyed by kind name', () => {
  const json = toParityJson(LayerMapSpec.Minimal(3));
  assert.equal(json.$type, 'LayerMapSpec');
  assert.deepEqual(json.Quotas, { Rest: 1, Survey: 1, Battle: 1, Elite: 1 });
  assert.deepEqual(json.RowWidths, [1, 2, 2, 1]);
  assert.equal(json.FullSweepCost, 6);
});

test('errors carry the C# messages', () => {
  const m = (entries) => new Map(entries);
  const cases = {
    'spec.entryRow': () => new LayerMapSpec(1, [2, 2, 1], m([[NodeKind.Battle, 3]])),
    'spec.tooFewRows': () => new LayerMapSpec(1, [1, 1], m([])),
    'spec.bossRow': () => new LayerMapSpec(1, [1, 2, 2], m([[NodeKind.Battle, 3]])),
    'spec.zeroRow': () => new LayerMapSpec(1, [1, 0, 1], m([])),
    'spec.quotaMismatch': () => new LayerMapSpec(1, [1, 2, 2, 1], m([[NodeKind.Battle, 2]])),
    'spec.detourForced': () => new LayerMapSpec(1, [1, 2, 1], m([[NodeKind.Elite, 2]])),
    'spec.boss': () => new LayerMapSpec(1, [1, 2, 2, 1], m([[NodeKind.Boss, 1], [NodeKind.Battle, 3]])),
    'spec.negative': () => new LayerMapSpec(1, [1, 2, 2, 1], m([[NodeKind.Rest, -1], [NodeKind.Battle, 5]])),
    'spec.chance': () => new LayerMapSpec(1, [1, 2, 2, 1], m([[NodeKind.Battle, 4]]), 1.5),
    'spec.plainTooFew': () => new LayerMapSpec(1, [1, 2, 2, 2, 1], m([[NodeKind.Elite, 3], [NodeKind.Battle, 2], [NodeKind.Trace, 1]])),
    'spec.withRefuse': () => LayerMapSpec.Minimal().With(NodeKind.Trace, 3),
  };
  for (const [key, fn] of Object.entries(cases)) assert.equal(caught(fn), expectedError(key), key);
});
