// Tests for src/core/battle/IRng.js (port of unity-port/BattleCore/IRng.cs).
// The pinned sequences were produced by running the C# IRng.cs under dotnet 10
// (scratch-csharp/foundation): `new SeededRng(seed).NextDouble()` and `new SystemRng(seed).NextDouble()`,
// printed with "R" (round-trip) formatting.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { IRng, SystemRng, SeededRng, FixedRng } from '../../src/core/battle/IRng.js';

const SEEDED = {
  1: [0.5665615751722809, 0.7457817572627011, 0.9710027535867962, 0.4443592170557721, 0.44426470082635805],
  42: [0.7415648787718233, 0.1599103928769201, 0.27860113025513866, 0.34419071652363753, 0.03803016854024621],
  0: [0.8833108082136426, 0.43152799704850997, 0.026433771592597743, 0.9708819781538285, 0.10634669156721244],
  [-1]: [0.8939429202831845, 0.9125972035944532, 0.21948196289526756, 0.4262344494451664, 0.7055706489695709],
  [-2147483648]: [0.9300166317571541, 0.290676167971483, 0.2980643817094617, 0.938704182346349, 0.015904646323716354],
  2147483647: [0.3827242046009839, 0.03702493655006345, 0.3953685901255065, 0.8718711067059306, 0.7232512777182792],
};

const SYSTEM = {
  1: [0.24866858415709278, 0.11074397718102856, 0.46701067987224587, 0.7716041220219825, 0.657518893786482],
  42: [0.6681064659115423, 0.14090729837348093, 0.12551828945312568, 0.5227642760252413, 0.16843422416990353],
  0: [0.7262432699679598, 0.8173253595909687, 0.7680226893946634, 0.5581611914365372, 0.2060331540210327],
  [-5]: [0.33836984091362443, 0.2844184475412678, 0.2629626417825756, 0.6253758443637638, 0.46346185284827923],
  [-2147483648]: [0.7262432699679598, 0.8173253595909687, 0.7680226921886312, 0.5581611914365372, 0.2060331540210327],
  2147483647: [0.7262432699679598, 0.8173253595909687, 0.7680226921886312, 0.5581611914365372, 0.2060331540210327],
  12345: [0.06674693481379511, 0.07015950887937075, 0.7747651351498278, 0.5111392687592372, 0.7974905584927139],
};

function firstFive(rng) {
  return [rng.NextDouble(), rng.NextDouble(), rng.NextDouble(), rng.NextDouble(), rng.NextDouble()];
}

test('SeededRng: seed 1 replays the C# SplitMix64 sequence exactly', () => {
  assert.deepEqual(firstFive(new SeededRng(1)), SEEDED[1]);
});

test('SeededRng: seed 42 replays the C# SplitMix64 sequence exactly', () => {
  assert.deepEqual(firstFive(new SeededRng(42)), SEEDED[42]);
});

test('SeededRng: zero, negative (sign-extended ulong) and extreme int seeds match C#', () => {
  for (const seed of [0, -1, -2147483648, 2147483647]) {
    assert.deepEqual(firstFive(new SeededRng(seed)), SEEDED[seed], `seed ${seed}`);
  }
});

test('SeededRng: the same seed replays the same sequence; values stay in [0, 1)', () => {
  const a = new SeededRng(7);
  const b = new SeededRng(7);
  for (let i = 0; i < 1000; i++) {
    const x = a.NextDouble();
    assert.equal(x, b.NextDouble());
    assert.ok(x >= 0 && x < 1);
  }
});

test('SystemRng(int seed) reproduces .NET new Random(seed).NextDouble()', () => {
  for (const seed of [1, 42, 0, -5, -2147483648, 2147483647, 12345]) {
    assert.deepEqual(firstFive(new SystemRng(seed)), SYSTEM[seed], `seed ${seed}`);
  }
});

test('SystemRng(Random) wraps any NextDouble source; null is refused like C#', () => {
  const source = new FixedRng(0.25);
  const rng = new SystemRng(source);
  assert.equal(rng.NextDouble(), 0.25);
  assert.throws(() => new SystemRng(null), (e) => e.name === 'ArgumentNullException' && e.message === "Value cannot be null. (Parameter 'random')");
});

test('FixedRng always returns its value, 0 by default', () => {
  const zero = new FixedRng();
  assert.equal(zero.NextDouble(), 0);
  assert.equal(zero.NextDouble(), 0);
  const almostOne = new FixedRng(0.9999999);
  assert.equal(almostOne.NextDouble(), 0.9999999);
  assert.equal(almostOne.NextDouble(), 0.9999999);
});

test('IRng is exported (interface placeholder)', () => {
  assert.equal(typeof IRng, 'object');
});

// 200 values × 20 seeds of each generator, written by the C# code under dotnet 10.
import { readFileSync } from 'node:fs';
const FIXTURE = JSON.parse(readFileSync(new URL('./IRng.fixture.json', import.meta.url), 'utf8'));

test('SeededRng: 4000 values over 20 seeds match the C# fixture', () => {
  for (const [seed, values] of Object.entries(FIXTURE.seeded)) {
    const rng = new SeededRng(Number(seed));
    values.forEach((v, i) => assert.equal(rng.NextDouble(), v, `seed ${seed} #${i}`));
  }
});

test('SystemRng(seed): 4000 values over 20 seeds match the C# fixture (.NET seeded Random)', () => {
  for (const [seed, values] of Object.entries(FIXTURE.system)) {
    const rng = new SystemRng(Number(seed));
    values.forEach((v, i) => assert.equal(rng.NextDouble(), v, `seed ${seed} #${i}`));
  }
});
