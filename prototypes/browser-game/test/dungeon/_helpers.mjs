// Shared helpers for the dungeon port tests (not a test file itself).
// Fixtures under ./fixtures were printed by the C# oracle in scratch-csharp/dungeon
// (dotnet run -c Release --project scratch-csharp/dungeon -- test/dungeon/fixtures).
import { readFileSync } from 'node:fs';

const cache = new Map();

/** Parsed fixture JSON by base name ('maps', 'walks', 'errors', ...). */
export function fixture(name) {
  if (!cache.has(name)) {
    cache.set(name, JSON.parse(readFileSync(new URL(`./fixtures/${name}.json`, import.meta.url), 'utf8')));
  }
  return cache.get(name);
}

/** "<ExceptionType>: <Message>" the way the oracle's Catch() prints it, or "(no throw)". */
export function caught(fn) {
  try {
    fn();
    return '(no throw)';
  } catch (e) {
    return `${e.name}: ${e.message}`;
  }
}

/** The oracle's expected text for an error case. */
export function expectedError(key) {
  const all = fixture('errors');
  if (!(key in all)) throw new Error(`no error fixture ${key}`);
  return all[key];
}

/** Always returns the same value (DungeonCore.Tests/FixedRng.cs). */
export class FixedRng {
  constructor(value = 0.0) { this._value = value; }
  NextDouble() { return this._value; }
}

/** Counts how often the wrapped rng is drawn from (LayerEncountersTests.CountingRng). */
export class CountingRng {
  constructor(inner) { this._inner = inner; this.Draws = 0; }
  NextDouble() { this.Draws++; return this._inner.NextDouble(); }
}

/** ExplorationSession.LayerSeed: unchecked(runSeed * 0x9E3779B97F4A7C15 + layer) as a ulong BigInt. */
export function layerSeed(runSeed, layer) {
  return BigInt.asUintN(64, BigInt(runSeed) * 0x9E3779B97F4A7C15n + BigInt(layer));
}
