// Tests for src/core/dungeon/NodeKind.js.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { enumValues } from '../../src/shared/cs.js';
import { NodeKind, NodeKindTokens } from '../../src/core/dungeon/NodeKind.js';
import { caught, expectedError } from './_helpers.mjs';

test('declaration order is the C# underlying order', () => {
  assert.deepEqual(enumValues(NodeKind), ['Battle', 'Elite', 'Boss', 'Rest', 'Survey', 'Event', 'Trace', 'Carving']);
});

test('tokens are the stable lowercase names', () => {
  assert.deepEqual(enumValues(NodeKind).map((k) => NodeKindTokens.ToToken(k)),
    ['battle', 'elite', 'boss', 'rest', 'survey', 'event', 'trace', 'carving']);
});

test('detours are elite, trace and carving', () => {
  assert.deepEqual(enumValues(NodeKind).filter((k) => NodeKindTokens.IsDetour(k)), ['Elite', 'Trace', 'Carving']);
});

test('combat kinds are battle, elite and boss — a trace holds no fight', () => {
  assert.deepEqual(enumValues(NodeKind).filter((k) => NodeKindTokens.IsCombat(k)), ['Battle', 'Elite', 'Boss']);
  assert.equal(NodeKindTokens.IsCombat(NodeKind.Trace), false);
});

test('an undefined value throws the C# message', () => {
  // (NodeKind)99 in C# prints "Actual value was 99."
  assert.equal(caught(() => NodeKindTokens.ToToken(99)), expectedError('token.invalid'));
});
