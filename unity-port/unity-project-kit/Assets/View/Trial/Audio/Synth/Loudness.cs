// Loudness as docs/AUDIO.md measured it: ITU-R BS.1770 K-weighting (the shelving pre-filter and the RLB high-pass,
// coefficients for any sample rate as libebur128 derives them; at 48 kHz they are the standard's), mean square per
// channel over 400 ms windows every 100 ms, LUFS = −0.691 + 10·log10(Σ_ch mean(z²)); the maximum momentary loudness is
// the loudest window. Also the share of power above a frequency (AUDIO.md's high-frequency check).
using System;

namespace Trial.Audio.Synth
{
    public static class Loudness
    {
        private static void KWeighting(double sr, out double[] pb, out double[] pa, out double[] rb, out double[] ra)
        {
            double f0 = 1681.974450955533;
            double g = 3.999843853973347;
            double q = 0.7071752369554196;
            double k = Math.Tan(Math.PI * f0 / sr);
            double vh = Math.Pow(10.0, g / 20.0);
            double vb = Math.Pow(vh, 0.4996667741545416);
            double a0 = 1.0 + k / q + k * k;
            pb = new[] { (vh + vb * k / q + k * k) / a0, 2.0 * (k * k - vh) / a0, (vh - vb * k / q + k * k) / a0 };
            pa = new[] { 1.0, 2.0 * (k * k - 1.0) / a0, (1.0 - k / q + k * k) / a0 };
            f0 = 38.13547087602444;
            q = 0.5003270373238773;
            k = Math.Tan(Math.PI * f0 / sr);
            rb = new[] { 1.0, -2.0, 1.0 };
            ra = new[] { 1.0, 2.0 * (k * k - 1.0) / (1.0 + k / q + k * k), (1.0 - k / q + k * k) / (1.0 + k / q + k * k) };
        }

        /// <summary>The K-weighted squares of one channel of an interleaved buffer.</summary>
        private static double[] WeightedSquares(float[] interleaved, int channels, int channel, int sampleRate)
        {
            KWeighting(sampleRate, out double[] pb, out double[] pa, out double[] rb, out double[] ra);
            int frames = interleaved.Length / channels;
            var z = new double[frames];
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0, u1 = 0, u2 = 0;
            for (int i = 0; i < frames; i++)
            {
                double x = interleaved[i * channels + channel];
                // the shelving pre-filter, then the RLB high-pass on its output (whose input history is y1, y2)
                double y = pb[0] * x + pb[1] * x1 + pb[2] * x2 - pa[1] * y1 - pa[2] * y2;
                double u = rb[0] * y + rb[1] * y1 + rb[2] * y2 - ra[1] * u1 - ra[2] * u2;
                x2 = x1; x1 = x; y2 = y1; y1 = y; u2 = u1; u1 = u;
                z[i] = u * u;
            }
            return z;
        }

        /// <summary>The maximum momentary loudness (LUFS) of an interleaved buffer: 400 ms windows every 100 ms from frame 0.</summary>
        public static double MaxMomentary(float[] interleaved, int channels, int sampleRate)
        {
            int frames = interleaved.Length / channels;
            int window = (int)Math.Round(0.4 * sampleRate), hop = (int)Math.Round(0.1 * sampleRate);
            var cumulative = new double[frames + 1];
            for (int c = 0; c < Math.Min(channels, 2); c++)
            {
                double[] z = WeightedSquares(interleaved, channels, c, sampleRate);
                for (int i = 0; i < frames; i++) cumulative[i + 1] += z[i];
            }
            for (int i = 0; i < frames; i++) cumulative[i + 1] += cumulative[i];
            double best = double.NegativeInfinity;
            if (frames < window)
            {
                double sum = cumulative[frames];
                return sum > 0 ? -0.691 + 10 * Math.Log10(sum / window) : double.NegativeInfinity;
            }
            for (int start = 0; start + window <= frames; start += hop)
            {
                double sum = cumulative[start + window] - cumulative[start];
                if (sum > 0) best = Math.Max(best, -0.691 + 10 * Math.Log10(sum / window));
            }
            return best;
        }

        /// <summary>The share of the power above <paramref name="hz"/> (both channels; a DFT of 8192-frame Hann blocks).</summary>
        public static double PowerShareAbove(float[] interleaved, int channels, int sampleRate, double hz)
        {
            const int n = 8192;
            Fft fft = Fft.Of(n);
            var re = new double[n];
            var im = new double[n];
            int frames = interleaved.Length / channels;
            int cut = (int)Math.Ceiling(hz * n / sampleRate);
            double above = 0, total = 0;
            for (int c = 0; c < Math.Min(channels, 2); c++)
            {
                for (int start = 0; start + n <= frames; start += n / 2)
                {
                    for (int i = 0; i < n; i++)
                    {
                        double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / n);
                        re[i] = interleaved[(start + i) * channels + c] * w;
                        im[i] = 0;
                    }
                    fft.Forward(re, im);
                    for (int k = 1; k < n / 2; k++)
                    {
                        double p = re[k] * re[k] + im[k] * im[k];
                        total += p;
                        if (k >= cut) above += p;
                    }
                }
            }
            return total > 0 ? above / total : 0;
        }
    }
}
