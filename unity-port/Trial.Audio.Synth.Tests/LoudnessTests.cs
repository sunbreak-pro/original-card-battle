// The loudness meter is calibrated as BS.1770 says (a 0 dBFS 1 kHz sine in one channel reads −3.01 LUFS, in both 0),
// and the high-frequency share reads white noise and a low sine sensibly.
using System;
using NUnit.Framework;
using Trial.Audio.Synth;

namespace Trial.Audio.Synth.Tests
{
    public class LoudnessTests
    {
        private const int Sr = 48000;

        private static float[] Sine(double hz, double seconds, bool left, bool right)
        {
            int n = (int)(seconds * Sr);
            var b = new float[2 * n];
            for (int i = 0; i < n; i++)
            {
                float x = (float)Math.Sin(2 * Math.PI * hz * i / Sr);
                if (left) b[2 * i] = x;
                if (right) b[2 * i + 1] = x;
            }
            return b;
        }

        [Test]
        public void ASineAtFullScaleReadsAsTheStandardSays()
        {
            Assert.That(Loudness.MaxMomentary(Sine(997, 3, true, false), 2, Sr), Is.EqualTo(-3.01).Within(0.05), "one channel");
            Assert.That(Loudness.MaxMomentary(Sine(997, 3, true, true), 2, Sr), Is.EqualTo(0.0).Within(0.05), "both channels");
            Assert.That(Loudness.MaxMomentary(new float[2 * Sr], 2, Sr), Is.EqualTo(double.NegativeInfinity));
        }

        [Test]
        public void HighFrequencyShare()
        {
            var white = new float[2 * Sr * 2];
            var r = new Lcg(3);
            for (int i = 0; i < white.Length; i++) white[i] = (float)(r.Next() * 2 - 1);
            Assert.That(Loudness.PowerShareAbove(white, 2, Sr, 4000), Is.EqualTo(20000.0 / 24000).Within(0.02));
            Assert.That(Loudness.PowerShareAbove(Sine(500, 2, true, true), 2, Sr, 4000), Is.LessThan(1e-6));
        }
    }
}
