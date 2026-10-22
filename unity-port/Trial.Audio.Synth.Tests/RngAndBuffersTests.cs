// The seeded generators and the procedural buffers equal the JS ones (golden, 48 kHz): the LCG sequences, the FNV seeds,
// the layer generators, and every noise buffer and room impulse sample for sample (head, tail, energy, FNV of the
// Float32 bytes); and the convolver normalisation follows the formula.
using System;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Trial.Audio.Synth;

namespace Trial.Audio.Synth.Tests
{
    public class RngAndBuffersTests
    {
        private static JsonElement Seeds => Golden.Root.GetProperty("seeds");

        [Test]
        public void LcgSequencesEqualTheJs()
        {
            foreach (JsonProperty p in Seeds.GetProperty("lcg").EnumerateObject())
            {
                double seed = double.Parse(p.Name, System.Globalization.CultureInfo.InvariantCulture);
                var r = new Lcg(seed);
                double[] expected = p.Value.EnumerateArray().Select(x => x.GetDouble()).ToArray();
                for (int i = 0; i < expected.Length; i++) Assert.That(r.Next(), Is.EqualTo(expected[i]), $"lcg({p.Name})[{i}]");
            }
        }

        [Test]
        public void HashSeedEqualsTheJs()
        {
            foreach (JsonProperty p in Seeds.GetProperty("hashSeed").EnumerateObject())
                Assert.That(ParkMiller.HashSeed(p.Name), Is.EqualTo(p.Value.GetUInt32()), "hashSeed(" + p.Name + ")");
            Assert.That(ParkMiller.HashSeed("layer3"), Is.EqualTo(Seeds.GetProperty("hashSeed").GetProperty("layer3").GetUInt32()));
        }

        [Test]
        public void LayerGeneratorsEqualTheJs()
        {
            foreach (JsonProperty p in Seeds.GetProperty("layerRnd").EnumerateObject())
            {
                var r = new Lcg(ParkMiller.HashSeed(p.Name));
                foreach (JsonElement x in p.Value.EnumerateArray()) Assert.That(r.Next(), Is.EqualTo(x.GetDouble()), p.Name);
            }
        }

        private static void AssertBuffer(AudioBuffer b, string goldenName)
        {
            JsonElement g = Golden.Root.GetProperty("buffers").GetProperty(goldenName);
            Assert.That(b.Length, Is.EqualTo(g.GetProperty("length").GetInt32()), goldenName + " length");
            Assert.That(b.SampleRate, Is.EqualTo(g.GetProperty("sampleRate").GetInt32()));
            JsonElement[] chans = g.GetProperty("channels").EnumerateArray().ToArray();
            Assert.That(b.NumberOfChannels, Is.EqualTo(chans.Length), goldenName + " channels");
            for (int c = 0; c < chans.Length; c++)
            {
                float[] d = b.Channels[c];
                double[] head = chans[c].GetProperty("head").EnumerateArray().Select(x => x.GetDouble()).ToArray();
                double[] tail = chans[c].GetProperty("tail").EnumerateArray().Select(x => x.GetDouble()).ToArray();
                for (int i = 0; i < head.Length; i++) Assert.That((double)d[i], Is.EqualTo(head[i]), $"{goldenName}[{c}] head {i}");
                for (int i = 0; i < tail.Length; i++) Assert.That((double)d[d.Length - tail.Length + i], Is.EqualTo(tail[i]), $"{goldenName}[{c}] tail {i}");
                double energy = 0, peak = 0;
                // the JS multiplies the Float32 samples in double
                for (int i = 0; i < d.Length; i++) { energy += (double)d[i] * d[i]; peak = Math.Max(peak, Math.Abs(d[i])); }
                Assert.That(energy, Is.EqualTo(chans[c].GetProperty("energy").GetDouble()).Within(1e-9).Percent, goldenName + " energy");
                Assert.That(peak, Is.EqualTo(chans[c].GetProperty("peak").GetDouble()), goldenName + " peak");
                Assert.That(AudioBuffers.Fnv32(d), Is.EqualTo(chans[c].GetProperty("fnv").GetUInt32()), goldenName + " every sample (FNV of the Float32 bytes)");
            }
        }

        [Test]
        public void MetaNoisesEqualTheJs()
        {
            AssertBuffer(Golden.Bank.White, "white");
            AssertBuffer(Golden.Bank.Pink, "pink");
            AssertBuffer(Golden.Bank.Brown, "brown");
            Assert.That(Golden.Bank.Pink.Channels[0].Max(Math.Abs), Is.EqualTo(0.9f).Within(1e-6), "scaled to peak 0.9");
        }

        [Test]
        public void DesignSystemNoiseEqualsTheJs() => AssertBuffer(Golden.DsNoise, "dsNoise");

        [Test]
        public void RoomsEqualTheJs()
        {
            AssertBuffer(AudioBuffers.MakeDampRoom(48000), "room");
            AssertBuffer(AudioBuffers.MakeCave(48000), "cave");
        }

        [Test]
        public void ConvolverNormalisationFollowsTheFormula()
        {
            AudioBuffer ir = AudioBuffers.MakeDampRoom(48000);
            double sum = 0;
            foreach (float[] ch in ir.Channels) foreach (float x in ch) sum += (double)x * x;
            double power = Math.Sqrt(sum / (2.0 * ir.Length));
            double expected = 1 / power * Math.Pow(10, -58 / 20.0) * (44100.0 / 48000);
            Assert.That(AudioBuffers.ConvolverNormalizationScale(ir), Is.EqualTo(expected).Within(1e-12));
            Assert.That(AudioBuffers.ConvolverNormalizationScale(ir), Is.InRange(0.004, 0.007), "about 45 dB down, as the plan computed");
            // a silent impulse is protected by the minimum power
            var silent = new AudioBuffer(2, 100, 48000);
            Assert.That(AudioBuffers.ConvolverNormalizationScale(silent), Is.EqualTo(1 / 0.000125 * Math.Pow(10, -2.9) * (44100.0 / 48000)).Within(1e-9));
        }
    }
}
