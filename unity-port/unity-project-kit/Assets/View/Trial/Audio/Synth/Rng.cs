// The seeded generators of the trial's sound (no randomness anywhere: every run sounds the same).
// Lcg is Audio.js's Park-Miller LCG in [0, 1); ParkMiller.Step is the raw step the rooms and the design system's noise
// use (no +1 on the seed); HashSeed is the FNV-1a hash that seeds an ambience layer from its id.
// All arithmetic is exact in long / uint, as it is in the JS doubles (every product stays under 2^53).
using System;

namespace Trial.Audio.Synth
{
    /// <summary>js: scenes/audio/Audio.js:173 lcg(seed). The same seed gives the same sequence.</summary>
    public sealed class Lcg
    {
        private long s;

        // js: scenes/audio/Audio.js:173
        public Lcg(double seed)
        {
            // (Math.abs(Math.floor(seed)) % 2147483646) + 1
            s = (long)(Math.Abs(Math.Floor(seed)) % 2147483646d) + 1;
        }

        /// <summary>js: scenes/audio/Audio.js:175 next(): the next value in [0, 1).</summary>
        public double Next()
        {
            s = (s * 16807) % 2147483647;
            return (s - 1) / 2147483646d;
        }

        /// <summary>The generator as a delegate (the ambience builders' <c>r()</c>).</summary>
        public Func<double> AsFunc() => Next;
    }

    public static class ParkMiller
    {
        /// <summary>js: vendor/ds/bundle.js:720 / scenes/audio/Audio.js:273 <c>s = (s * 16807) % 2147483647</c>.</summary>
        public static long Step(ref long s)
        {
            s = (s * 16807) % 2147483647;
            return s;
        }

        /// <summary>
        /// js: scenes/audio/Audio.js:181 hashSeed(str): FNV-1a over the UTF-16 code units
        /// (<c>h ^= charCodeAt(i); h = Math.imul(h, 16777619) &gt;&gt;&gt; 0</c>).
        /// </summary>
        public static uint HashSeed(string str)
        {
            uint h = 2166136261;
            for (int i = 0; i < str.Length; i++)
            {
                h ^= str[i];
                h = unchecked(h * 16777619);
            }
            return h;
        }
    }
}
