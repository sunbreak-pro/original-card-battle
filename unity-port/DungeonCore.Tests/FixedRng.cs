using DungeonCore;

namespace DungeonCore.Tests
{
    /// <summary>
    /// Always returns the same value. For tests that need one fixed branch. It lives with the tests,
    /// not in DungeonCore, because nothing outside them draws from it (#517).
    /// </summary>
    internal sealed class FixedRng : IRng
    {
        private readonly double _value;

        public FixedRng(double value = 0.0)
        {
            _value = value;
        }

        public double NextDouble() => _value;
    }
}
