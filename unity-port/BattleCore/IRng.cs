using System;

namespace BattleCore
{
    /// <summary>
    /// Random source abstraction. The TS core calls the global Math.random()
    /// directly; the C# port injects this instead so parity fixtures can pin
    /// the RNG to a fixed value. NextDouble() returns [0, 1) like Math.random().
    /// </summary>
    public interface IRng
    {
        double NextDouble();
    }

    /// <summary>System.Random-backed RNG for real gameplay.</summary>
    public sealed class SystemRng : IRng
    {
        private readonly Random _random;

        public SystemRng() : this(new Random()) { }

        public SystemRng(int seed) : this(new Random(seed)) { }

        public SystemRng(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public double NextDouble() => _random.NextDouble();
    }

    /// <summary>
    /// A seeded generator whose sequence is written out here (SplitMix64), so the same seed replays
    /// the same battle under `dotnet test` and inside Unity. System.Random makes no such promise
    /// across runtimes, which is why a pinned battle does not use <see cref="SystemRng"/>.
    /// </summary>
    public sealed class SeededRng : IRng
    {
        private ulong _state;

        public SeededRng(int seed)
        {
            _state = unchecked((ulong)seed);
        }

        public double NextDouble()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                // 53 high bits → [0, 1), the same range Math.random() and System.Random give.
                return (z >> 11) * (1.0 / 9007199254740992.0);
            }
        }
    }

    /// <summary>
    /// Deterministic RNG that always returns a fixed value (default 0).
    /// Mirrors the parity fixture generation where Math.random() was pinned to 0.
    /// </summary>
    public sealed class FixedRng : IRng
    {
        private readonly double _value;

        public FixedRng(double value = 0.0)
        {
            _value = value;
        }

        public double NextDouble() => _value;
    }
}
