// Tests for src/core/dungeon/IRng.js — ports DungeonCore.Tests/SeededRngTests.cs and pins the
// oracle's vectors (fixtures/rng.json).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { IRng, SeededRng, RngExtensions } from '../../src/core/dungeon/IRng.js';
import { fixture, FixedRng, caught, expectedError } from './_helpers.mjs';

test('SplitMix64 matches the reference vectors', () => {
  const rng = new SeededRng(0n);
  assert.equal(rng.NextUInt64(), 0xE220A8397B1DCDAFn);
  assert.equal(rng.NextUInt64(), 0x6E789E6AA1B965F4n);
  assert.equal(rng.NextUInt64(), 0x06C45D188009454Fn);
});

test('NextDouble stays in range', () => {
  const rng = new SeededRng(12345);
  for (let i = 0; i < 2000; i++) {
    const value = rng.NextDouble();
    assert.ok(value >= 0.0);
    assert.ok(value < 1.0);
  }
});

test('same seed, same sequence', () => {
  const a = new SeededRng(99n);
  const b = new SeededRng(99);
  for (let i = 0; i < 50; i++) assert.equal(b.NextUInt64(), a.NextUInt64());
});

test('NextInt never leaves the range, even when the source returns one', () => {
  assert.equal(RngExtensions.NextInt(new FixedRng(1.0), 0, 3), 2);
  const rng = new SeededRng(7);
  for (let i = 0; i < 2000; i++) {
    const v = RngExtensions.NextInt(rng, 5, 9);
    assert.ok(v >= 5 && v <= 8);
  }
});

test('NextInt on an empty range returns the minimum', () => {
  assert.equal(RngExtensions.NextInt(new SeededRng(1), 4, 4), 4);
});

test('Shuffle is a permutation and depends on the seed', () => {
  const items = Array.from({ length: 20 }, (_, i) => i);
  RngExtensions.Shuffle(new SeededRng(3), items);
  assert.deepEqual(items.slice().sort((x, y) => x - y), Array.from({ length: 20 }, (_, i) => i));

  const a = Array.from({ length: 20 }, (_, i) => i);
  const b = Array.from({ length: 20 }, (_, i) => i);
  RngExtensions.Shuffle(new SeededRng(3), a);
  RngExtensions.Shuffle(new SeededRng(4), b);
  assert.notDeepEqual(a, b);
});

test('NextChance: <= 0 never draws or fires, >= 1 always fires without a draw', () => {
  let draws = 0;
  const counting = { NextDouble() { draws++; return 0.5; } };
  assert.equal(RngExtensions.NextChance(counting, 0), false);
  assert.equal(RngExtensions.NextChance(counting, 1), true);
  assert.equal(draws, 0);
  assert.equal(RngExtensions.NextChance(counting, 0.6), true);
  assert.equal(RngExtensions.NextChance(counting, 0.5), false);
  assert.equal(draws, 2);
});

test('the C# oracle vectors: u64, doubles, ints, chances, shuffles', () => {
  for (const c of fixture('rng')) {
    const seed = BigInt(c.seed);
    const a = new SeededRng(seed);
    assert.deepEqual(c.u64.map(() => a.NextUInt64().toString()), c.u64, `u64 seed ${c.seed}`);
    const b = new SeededRng(seed);
    // "R" formatting is the shortest round-trip text, so parse and compare as numbers.
    assert.deepEqual(c.doubles.map(() => b.NextDouble()), c.doubles.map(Number), `doubles seed ${c.seed}`);
    const r = new SeededRng(seed);
    assert.deepEqual(c.ints.map((_, i) => RngExtensions.NextInt(r, 0, 1 + (i % 7))), c.ints, `ints seed ${c.seed}`);
    const e = new SeededRng(seed);
    assert.deepEqual(c.chances.map(() => RngExtensions.NextChance(e, 0.5)), c.chances, `chances seed ${c.seed}`);
    const f = new SeededRng(seed);
    const items = Array.from({ length: 12 }, (_, i) => i);
    RngExtensions.Shuffle(f, items);
    assert.deepEqual(items, c.shuffled, `shuffle seed ${c.seed}`);
  }
});

test('a negative number seed wraps like an unchecked ulong cast', () => {
  const a = new SeededRng(-1);
  const b = new SeededRng(0xFFFFFFFFFFFFFFFFn);
  assert.equal(a.NextUInt64(), b.NextUInt64());
});

test('null arguments throw the C# messages', () => {
  assert.equal(caught(() => RngExtensions.NextInt(null, 0, 3)), expectedError('rng.null'));
  assert.equal(caught(() => RngExtensions.Shuffle(new SeededRng(1), null)), expectedError('shuffle.null'));
});

test('IRng.is recognises anything with NextDouble', () => {
  assert.equal(IRng.is(new SeededRng(1)), true);
  assert.equal(IRng.is(new FixedRng()), true);
  assert.equal(IRng.is(5n), false);
  assert.equal(IRng.is(null), false);
});
