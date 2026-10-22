// Band-limited oscillator tables as Chromium's PeriodicWave builds them for the basic shapes: a table of 4096 frames
// (2048 at ≤ 24 kHz, 16384 above 88.2 kHz), three ranges per octave, range r keeping the partials under
// 2048·2^(−r/3) (the top range has none), every range scaled by 1/max|range 0|. A frequency reads the two ranges
// around 1 + (cents above the lowest fundamental)/400 and blends them; inside a table it reads linearly (the trial's
// table oscillators all run above 27 Hz, where Chromium reads linearly too).
using System;
using System.Collections.Generic;

namespace Trial.Audio.Synth
{
    public enum OscType
    {
        Sine,
        Square,
        Sawtooth,
        Triangle,
    }

    public sealed class WaveTables
    {
        private const int OctaveBands = 3;
        private const double CentsPerRange = 1200.0 / OctaveBands;

        private static readonly Dictionary<long, WaveTables> Cache = new Dictionary<long, WaveTables>();
        private static readonly object CacheLock = new object();

        public int TableSize { get; }
        public int NumberOfRanges { get; }
        public double LowestFundamental { get; }
        /// <summary>[range][frame]</summary>
        public readonly float[][] Tables;

        public static WaveTables For(OscType type, int sampleRate)
        {
            if (type == OscType.Sine) throw new ArgumentException("sine is computed directly", nameof(type));
            long key = ((long)sampleRate << 4) | (long)type;
            lock (CacheLock)
            {
                if (!Cache.TryGetValue(key, out WaveTables t))
                {
                    t = new WaveTables(type, sampleRate);
                    Cache[key] = t;
                }
                return t;
            }
        }

        public static int PeriodicWaveSize(int sampleRate) => sampleRate <= 24000 ? 2048 : sampleRate <= 88200 ? 4096 : 16384;

        private WaveTables(OscType type, int sampleRate)
        {
            int size = PeriodicWaveSize(sampleRate);
            TableSize = size;
            int half = size / 2;
            NumberOfRanges = (int)Math.Ceiling(OctaveBands * Math.Log(size, 2) - 1e-9);
            LowestFundamental = sampleRate / 2.0 / half;
            // The Fourier coefficients of the shape (imaginary parts: sine terms).
            var b = new double[half];
            for (int n = 1; n < half; n++)
            {
                double piFactor = 2 / (n * Math.PI);
                switch (type)
                {
                    case OscType.Square: b[n] = piFactor * ((n & 1) != 0 ? 2 : 0); break;
                    case OscType.Sawtooth: b[n] = piFactor * ((n & 1) != 0 ? 1 : -1); break;
                    default:
                        b[n] = (n & 1) != 0 ? 8.0 / (n * (double)n * Math.PI * Math.PI) * ((((n - 1) >> 1) & 1) != 0 ? -1 : 1) : 0;
                        break;
                }
            }
            Tables = new float[NumberOfRanges][];
            Fft fft = Fft.Of(size);
            double scale = 1;
            var re = new double[size];
            var im = new double[size];
            for (int r = 0; r < NumberOfRanges; r++)
            {
                double cullingScale = Math.Pow(2, -(r * CentsPerRange) / 1200);
                int partials = (int)(float)(cullingScale * half);
                int keep = Math.Min(half, partials + 1);
                Array.Clear(re, 0, size);
                Array.Clear(im, 0, size);
                // x[k] = Σ b_n sin(2πnk/N): Y[n] = −i·b_n/2, Y[N−n] = +i·b_n/2.
                for (int n = 1; n < keep; n++)
                {
                    im[n] = -b[n] / 2;
                    im[size - n] = b[n] / 2;
                }
                fft.Inverse(re, im);
                if (r == 0)
                {
                    double max = 0;
                    for (int k = 0; k < size; k++) max = Math.Max(max, Math.Abs(re[k]));
                    if (max > 0) scale = 1 / max;
                }
                var table = new float[size];
                for (int k = 0; k < size; k++) table[k] = (float)(re[k] * scale);
                Tables[r] = table;
            }
        }

        /// <summary>The two tables a frequency reads and the blend between them (Chromium's WaveDataForFundamentalFrequency).</summary>
        public void Select(double frequency, out float[] higher, out float[] lower, out double blend)
        {
            frequency = Math.Abs(frequency);
            double ratio = frequency > 0 ? frequency / LowestFundamental : 0.5;
            double cents = Math.Log(ratio, 2) * 1200;
            double pitchRange = 1 + cents / CentsPerRange;
            if (pitchRange < 0) pitchRange = 0;
            if (pitchRange > NumberOfRanges - 1) pitchRange = NumberOfRanges - 1;
            int r1 = (int)pitchRange;
            int r2 = r1 < NumberOfRanges - 1 ? r1 + 1 : r1;
            higher = Tables[r1];
            lower = Tables[r2];
            blend = pitchRange - r1;
        }

        /// <summary>The value at <paramref name="phase"/> (cycles, [0, 1)) from two tables blended.</summary>
        public float Read(double phase, float[] higher, float[] lower, double blend)
        {
            double index = phase * TableSize;
            int i0 = (int)index;
            double frac = index - i0;
            i0 &= TableSize - 1;
            int i1 = (i0 + 1) & (TableSize - 1);
            double h = (1 - frac) * higher[i0] + frac * higher[i1];
            double l = (1 - frac) * lower[i0] + frac * lower[i1];
            return (float)((1 - blend) * h + blend * l);
        }
    }
}
