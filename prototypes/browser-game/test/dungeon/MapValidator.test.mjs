// Tests for src/core/dungeon/MapValidator.js — every rule's failure, with the C# message.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { MapValidator, MapValidationException } from '../../src/core/dungeon/MapValidator.js';
import { LayerMap, MapNode } from '../../src/core/dungeon/LayerMap.js';
import { LayerMapSpec } from '../../src/core/dungeon/LayerMapSpec.js';
import { NodeKind } from '../../src/core/dungeon/NodeKind.js';
import { SevenLayers } from '../../src/core/dungeon/SevenLayers.js';
import { caught, expectedError } from './_helpers.mjs';

const N = (id, row, col, kind) => new MapNode(id, row, col, kind);
const line3 = () => new LayerMapSpec(1, [1, 1, 1], new Map([[NodeKind.Rest, 1]]));
const fork = () => new LayerMapSpec(1, [1, 2, 1], new Map([[NodeKind.Rest, 1], [NodeKind.Survey, 1]]));

test('every generated layer map validates', () => {
  for (const p of SevenLayers.All) {
    for (let seed = 0; seed < 40; seed++) assert.doesNotThrow(() => MapValidator.Validate(p.Map(seed), p.MapSpec));
  }
});

test('each broken promise throws a MapValidationException with the C# message', () => {
  const cases = {
    'validate.noFork': () => MapValidator.Validate(
      new LayerMap(1, 0, [N(0, 0, 0, NodeKind.Battle), N(1, 1, 0, NodeKind.Rest), N(2, 2, 0, NodeKind.Boss)], new Map([[0, [1]], [1, [2]]])), line3()),
    'validate.entryElite': () => MapValidator.Validate(
      new LayerMap(1, 0, [N(0, 0, 0, NodeKind.Elite), N(1, 1, 0, NodeKind.Rest), N(2, 2, 0, NodeKind.Boss)], new Map([[0, [1]], [1, [2]]])), line3()),
    'validate.shape': () => MapValidator.Validate(SevenLayers.Of(1).Map(1n), LayerMapSpec.Minimal(1)),
    'validate.skipRow': () => MapValidator.Validate(
      new LayerMap(1, 0, [N(0, 0, 0, NodeKind.Battle), N(1, 1, 0, NodeKind.Rest), N(2, 2, 0, NodeKind.Boss)], new Map([[0, [2]], [1, [2]]])), line3()),
    'validate.quota': () => MapValidator.Validate(
      new LayerMap(1, 0, [N(0, 0, 0, NodeKind.Battle), N(1, 1, 0, NodeKind.Rest), N(2, 1, 1, NodeKind.Rest), N(3, 2, 0, NodeKind.Boss)],
        new Map([[0, [1, 2]], [1, [3]], [2, [3]]])), fork()),
    'validate.detours': () => MapValidator.Validate(
      new LayerMap(1, 0, [N(0, 0, 0, NodeKind.Battle), N(1, 1, 0, NodeKind.Elite), N(2, 1, 1, NodeKind.Trace), N(3, 2, 0, NodeKind.Boss)],
        new Map([[0, [1, 2]], [1, [3]], [2, [3]]])), fork()),
    'validate.deadEnd': () => MapValidator.Validate(
      new LayerMap(1, 0, [N(0, 0, 0, NodeKind.Battle), N(1, 1, 0, NodeKind.Rest), N(2, 1, 1, NodeKind.Survey), N(3, 2, 0, NodeKind.Boss)],
        new Map([[0, [1, 2]], [1, [3]]])), fork()),
    'validate.unreachable': () => MapValidator.Validate(
      new LayerMap(1, 0, [N(0, 0, 0, NodeKind.Battle), N(1, 1, 0, NodeKind.Rest), N(2, 1, 1, NodeKind.Survey), N(3, 2, 0, NodeKind.Boss)],
        new Map([[0, [1]], [1, [3]], [2, [3]]])), fork()),
  };
  for (const [key, fn] of Object.entries(cases)) {
    assert.equal(caught(fn), expectedError(key), key);
    assert.throws(fn, MapValidationException);
  }
});

test('null arguments throw ArgumentNullException', () => {
  assert.throws(() => MapValidator.Validate(null, LayerMapSpec.Minimal()), { name: 'ArgumentNullException' });
  assert.throws(() => MapValidator.Validate(SevenLayers.Of(1).Map(1), null), { name: 'ArgumentNullException' });
});
