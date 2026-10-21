// Port of unity-port/BattleCore/IRng.cs

import { ArgumentNullException } from '../../shared/cs.js';

/**
 * Random source abstraction. The TS core calls the global Math.random()
 * directly; the C# port injects this instead so parity fixtures can pin
 * the RNG to a fixed value. NextDouble() returns [0, 1) like Math.random().
 *
 * C# `interface IRng { double NextDouble(); }`. In JS any object with a NextDouble() method is an
 * IRng; this export only exists so that `import { IRng }` resolves.
 *
 * @typedef {{ NextDouble(): number }} IRngLike
 */
export const IRng = Object.freeze({});

const UINT64 = (1n << 64n) - 1n;
const INT_MAX = 2147483647;
const INT_MIN = -2147483648;

/**
 * .NET's System.Random as built by `new Random(int seed)`: the seeded "Net5CompatSeedImpl" (the
 * Knuth subtractive generator .NET Framework, .NET Core and Unity's Mono all share for a seeded
 * Random). Ported so `new SystemRng(seed)` replays exactly what C# gives for the same seed.
 * Not a C# type of IRng.cs (internal to this module).
 */
class CompatRandom {
  constructor(seed) {
    const seedArray = new Array(56).fill(0);
    const subtraction = seed === INT_MIN ? INT_MAX : Math.abs(seed);
    // All int arithmetic wraps like C# (unchecked by default): `| 0` after every + / −.
    let mj = (161803398 - subtraction) | 0; // MSEED
    seedArray[55] = mj;
    let mk = 1;
    let ii = 0;
    for (let i = 1; i < 55; i++) {
      if ((ii += 21) >= 55) ii -= 55;
      seedArray[ii] = mk;
      mk = (mj - mk) | 0;
      if (mk < 0) mk = (mk + INT_MAX) | 0;
      mj = seedArray[ii];
    }
    for (let k = 1; k < 5; k++) {
      for (let i = 1; i < 56; i++) {
        let n = i + 30;
        if (n >= 55) n -= 55;
        seedArray[i] = (seedArray[i] - seedArray[1 + n]) | 0;
        if (seedArray[i] < 0) seedArray[i] = (seedArray[i] + INT_MAX) | 0;
      }
    }
    this._seedArray = seedArray;
    this._inext = 0;
    this._inextp = 21;
  }

  _internalSample() {
    let locINext = this._inext;
    if (++locINext >= 56) locINext = 1;
    let locINextp = this._inextp;
    if (++locINextp >= 56) locINextp = 1;
    let retVal = (this._seedArray[locINext] - this._seedArray[locINextp]) | 0;
    if (retVal === INT_MAX) retVal--;
    if (retVal < 0) retVal = (retVal + INT_MAX) | 0;
    this._seedArray[locINext] = retVal;
    this._inext = locINext;
    this._inextp = locINextp;
    return retVal;
  }

  NextDouble() {
    return this._internalSample() * (1.0 / INT_MAX);
  }
}

/**
 * System.Random-backed RNG for real gameplay.
 *
 * C# overloads: `SystemRng(int seed)` (→ `new Random(seed)`, reproduced exactly by CompatRandom)
 * and `SystemRng(Random random)`. JS has no System.Random, so the second overload takes any object
 * with a NextDouble() method (e.g. `{ NextDouble: Math.random }` for an unseeded game).
 */
export class SystemRng {
  constructor(seedOrRandom) {
    if (typeof seedOrRandom === 'number') {
      this._random = new CompatRandom(seedOrRandom | 0);
      return;
    }
    if (seedOrRandom == null) throw new ArgumentNullException("Value cannot be null. (Parameter 'random')");
    this._random = seedOrRandom;
  }

  NextDouble() {
    return this._random.NextDouble();
  }
}

/**
 * A seeded generator whose sequence is written out here (SplitMix64), so the same seed replays
 * the same battle under `dotnet test` and inside Unity. System.Random makes no such promise
 * across runtimes, which is why a pinned battle does not use SystemRng.
 *
 * JS: the ulong state is a BigInt kept masked to 64 bits (C# `unchecked` wraparound).
 */
export class SeededRng {
  constructor(seed) {
    // unchecked((ulong)seed): an int seed sign-extends (−1 → 0xFFFFFFFFFFFFFFFF).
    this._state = BigInt.asUintN(64, BigInt(seed | 0));
  }

  NextDouble() {
    this._state = (this._state + 0x9e3779b97f4a7c15n) & UINT64;
    let z = this._state;
    z = ((z ^ (z >> 30n)) * 0xbf58476d1ce4e5b9n) & UINT64;
    z = ((z ^ (z >> 27n)) * 0x94d049bb133111ebn) & UINT64;
    z ^= z >> 31n;
    // 53 high bits → [0, 1), the same range Math.random() and System.Random give.
    return Number(z >> 11n) * (1.0 / 9007199254740992.0);
  }
}

/**
 * Deterministic RNG that always returns a fixed value (default 0).
 * Mirrors the parity fixture generation where Math.random() was pinned to 0.
 */
export class FixedRng {
  constructor(value = 0.0) {
    this._value = value;
  }

  NextDouble() {
    return this._value;
  }
}
