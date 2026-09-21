using System;
using System.Collections.Generic;

namespace DungeonCore
{
    /// <summary>
    /// Random source abstraction, mirroring BattleCore.IRng in shape so the two cores
    /// read the same way. It is a separate type on purpose: the exploration core does
    /// not reference the battle core (#97). NextDouble() returns [0, 1).
    /// </summary>
    public interface IRng
    {
        double NextDouble();
    }

    /// <summary>
    /// Seeded RNG whose output depends only on the seed, not on the runtime.
    /// SplitMix64: the same seed yields the same map under .NET, Mono and IL2CPP.
    /// System.Random cannot promise that — its algorithm differs between runtimes —
    /// so the exploration core does not use it.
    /// </summary>
    public sealed class SeededRng : IRng
    {
        private const ulong Gamma = 0x9E3779B97F4A7C15UL;
        private const ulong Mix1 = 0xBF58476D1CE4E5B9UL;
        private const ulong Mix2 = 0x94D049BB133111EBUL;

        /// <summary>2^53, the number of doubles SplitMix64's top 53 bits can address.</summary>
        private const double TwoPow53 = 9007199254740992.0;

        private ulong _state;

        public SeededRng(ulong seed)
        {
            _state = seed;
        }

        public SeededRng(int seed) : this(unchecked((ulong)seed))
        {
        }

        /// <summary>Raw 64-bit draw. Exposed so tests can pin known vectors.</summary>
        public ulong NextUInt64()
        {
            unchecked
            {
                _state += Gamma;
                ulong z = _state;
                z = (z ^ (z >> 30)) * Mix1;
                z = (z ^ (z >> 27)) * Mix2;
                return z ^ (z >> 31);
            }
        }

        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / TwoPow53);
    }

    /// <summary>Always returns the same value. For tests that need one fixed branch.</summary>
    public sealed class FixedRng : IRng
    {
        private readonly double _value;

        public FixedRng(double value = 0.0)
        {
            _value = value;
        }

        public double NextDouble() => _value;
    }

    public static class RngExtensions
    {
        /// <summary>
        /// Uniform integer in [minInclusive, maxExclusive). Clamps the draw so a
        /// degenerate IRng that returns 1.0 cannot walk off the end of a list.
        /// </summary>
        public static int NextInt(this IRng rng, int minInclusive, int maxExclusive)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (maxExclusive <= minInclusive) return minInclusive;

            int span = maxExclusive - minInclusive;
            double draw = rng.NextDouble();
            if (draw < 0.0) draw = 0.0;
            if (draw >= 1.0) draw = 0.9999999999999999;
            return minInclusive + (int)(draw * span);
        }

        /// <summary>True with the given probability. Chance &lt;= 0 never fires, &gt;= 1 always does.</summary>
        public static bool NextChance(this IRng rng, double chance)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (chance <= 0.0) return false;
            if (chance >= 1.0) return true;
            return rng.NextDouble() < chance;
        }

        /// <summary>Fisher-Yates, in place. Same rng draws give the same order.</summary>
        public static void Shuffle<T>(this IRng rng, IList<T> items)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (items == null) throw new ArgumentNullException(nameof(items));

            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                T swap = items[i];
                items[i] = items[j];
                items[j] = swap;
            }
        }
    }
}
