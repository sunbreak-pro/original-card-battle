// Port of unity-port/DungeonCore/IRng.cs
import { argNull, toUlong } from './_errors.js';

/**
 * Random source abstraction, mirroring BattleCore.IRng in shape so the two cores read the same
 * way. It is a separate type on purpose: the exploration core does not reference the battle core
 * (#97). NextDouble() returns [0, 1).
 *
 * C# interface: anything with a `NextDouble()` method is an IRng. Exported as a marker so imports
 * by name resolve; `IRng.is(x)` tells whether a value can be drawn from.
 * @typedef {{ NextDouble(): number }} IRngLike
 */
export const IRng = Object.freeze({
  /** True when `rng` has the one member the interface asks for. */
  is(rng) {
    return rng !== null && rng !== undefined && typeof rng.NextDouble === 'function';
  },
});

const MASK64 = (1n << 64n) - 1n;

/**
 * Seeded RNG whose output depends only on the seed, not on the runtime.
 * SplitMix64: the same seed yields the same map under .NET, Mono and IL2CPP — and here.
 * System.Random cannot promise that — its algorithm differs between runtimes —
 * so the exploration core does not use it.
 *
 * `ulong` is a BigInt in this port. The constructor takes a number or a BigInt.
 */
export class SeededRng {
  static Gamma = 0x9E3779B97F4A7C15n;
  static Mix1 = 0xBF58476D1CE4E5B9n;
  static Mix2 = 0x94D049BB133111EBn;

  /** 2^53, the number of doubles SplitMix64's top 53 bits can address. */
  static TwoPow53 = 9007199254740992.0;

  /** @param {number|bigint} seed ulong */
  constructor(seed) {
    this._state = toUlong(seed);
  }

  /** Raw 64-bit draw (BigInt). Exposed so tests can pin known vectors. */
  NextUInt64() {
    // unchecked ulong arithmetic: every step masked to 64 bits.
    this._state = (this._state + SeededRng.Gamma) & MASK64;
    let z = this._state;
    z = ((z ^ (z >> 30n)) * SeededRng.Mix1) & MASK64;
    z = ((z ^ (z >> 27n)) * SeededRng.Mix2) & MASK64;
    return z ^ (z >> 31n);
  }

  NextDouble() {
    // (ulong >> 11) < 2^53, so the Number conversion is exact; the product is exact too.
    return Number(this.NextUInt64() >> 11n) * (1.0 / SeededRng.TwoPow53);
  }
}

/**
 * C# extension methods on IRng. Call as `RngExtensions.NextInt(rng, a, b)` where the C# reads
 * `rng.NextInt(a, b)`.
 */
export const RngExtensions = {
  /**
   * Uniform integer in [minInclusive, maxExclusive). Clamps the draw so a
   * degenerate IRng that returns 1.0 cannot walk off the end of a list.
   */
  NextInt(rng, minInclusive, maxExclusive) {
    if (rng === null || rng === undefined) throw argNull('rng');
    if (maxExclusive <= minInclusive) return minInclusive;

    const span = (maxExclusive - minInclusive) | 0;
    let draw = rng.NextDouble();
    if (draw < 0.0) draw = 0.0;
    if (draw >= 1.0) draw = 0.9999999999999999;
    return (minInclusive + Math.trunc(draw * span)) | 0;
  },

  /** True with the given probability. Chance <= 0 never fires, >= 1 always does. */
  NextChance(rng, chance) {
    if (rng === null || rng === undefined) throw argNull('rng');
    if (chance <= 0.0) return false;
    if (chance >= 1.0) return true;
    return rng.NextDouble() < chance;
  },

  /** Fisher-Yates, in place. Same rng draws give the same order. */
  Shuffle(rng, items) {
    if (rng === null || rng === undefined) throw argNull('rng');
    if (items === null || items === undefined) throw argNull('items');

    for (let i = items.length - 1; i > 0; i--) {
      const j = RngExtensions.NextInt(rng, 0, i + 1);
      const swap = items[i];
      items[i] = items[j];
      items[j] = swap;
    }
  },
};
