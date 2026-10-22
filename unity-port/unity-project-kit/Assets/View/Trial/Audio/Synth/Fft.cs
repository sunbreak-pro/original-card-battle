// An iterative radix-2 complex FFT on split real / imaginary double arrays, with cached bit-reversal and twiddle
// tables per size (thread-safe: the tables are built once under a lock and never change).
using System;
using System.Collections.Generic;

namespace Trial.Audio.Synth
{
    public sealed class Fft
    {
        private static readonly Dictionary<int, Fft> Plans = new Dictionary<int, Fft>();
        private static readonly object PlansLock = new object();

        public int Size { get; }
        private readonly int[] reverse;
        private readonly double[] cos, sin;

        private Fft(int size)
        {
            Size = size;
            int bits = 0;
            while ((1 << bits) < size) bits++;
            reverse = new int[size];
            for (int i = 0; i < size; i++)
            {
                int r = 0;
                for (int b = 0; b < bits; b++) if ((i & (1 << b)) != 0) r |= 1 << (bits - 1 - b);
                reverse[i] = r;
            }
            cos = new double[size / 2];
            sin = new double[size / 2];
            for (int i = 0; i < size / 2; i++)
            {
                double a = -2 * Math.PI * i / size;
                cos[i] = Math.Cos(a);
                sin[i] = Math.Sin(a);
            }
        }

        /// <summary>The plan for a power-of-two size (≥ 2).</summary>
        public static Fft Of(int size)
        {
            if (size < 2 || (size & (size - 1)) != 0) throw new ArgumentOutOfRangeException(nameof(size), "power of two expected");
            lock (PlansLock)
            {
                if (!Plans.TryGetValue(size, out Fft f))
                {
                    f = new Fft(size);
                    Plans[size] = f;
                }
                return f;
            }
        }

        public static int NextPowerOfTwo(int n)
        {
            int p = 1;
            while (p < n) p <<= 1;
            return p;
        }

        /// <summary>In place: X[k] = Σ x[n]·e^(−2πikn/N) (no scaling).</summary>
        public void Forward(double[] re, double[] im) => Transform(re, im, false);

        /// <summary>In place: x[n] = Σ X[k]·e^(+2πikn/N) (no 1/N scaling).</summary>
        public void Inverse(double[] re, double[] im) => Transform(re, im, true);

        private void Transform(double[] re, double[] im, bool inverse)
        {
            int n = Size;
            for (int i = 0; i < n; i++)
            {
                int j = reverse[i];
                if (j > i)
                {
                    double tr = re[i]; re[i] = re[j]; re[j] = tr;
                    double ti = im[i]; im[i] = im[j]; im[j] = ti;
                }
            }
            double sign = inverse ? -1 : 1;
            for (int len = 2; len <= n; len <<= 1)
            {
                int half = len >> 1, step = n / len;
                for (int i = 0; i < n; i += len)
                {
                    for (int k = 0; k < half; k++)
                    {
                        double wr = cos[k * step], wi = sign * sin[k * step];
                        int a = i + k, b = a + half;
                        double xr = re[b] * wr - im[b] * wi;
                        double xi = re[b] * wi + im[b] * wr;
                        re[b] = re[a] - xr; im[b] = im[a] - xi;
                        re[a] += xr; im[a] += xi;
                    }
                }
            }
        }
    }
}
