// The ambience as it sounds: rendered offline through the engine (the cave, the aligned dry path, the volume and the
// duck), then through the trial's compressor as the master stage applies it. Levels per place against docs/AUDIO.md
// (max momentary loudness, BS.1770, master 1, ambience 1), the high-frequency share, the alignment of the dry path with
// the cave's block latency, the crossfade and kill timing in rendered time, the duck and the volume as exact gains, the
// freeze, any block size giving the same samples, and commands posted while another thread renders.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Trial.Audio.Synth;

namespace Trial.Audio.Synth.Tests
{
    public class AmbienceRenderTests
    {
        private const int Sr = 48000;

        /// <summary>Renders <paramref name="seconds"/> of the engine as Unity would pull it (stereo, blocks of <paramref name="block"/>).</summary>
        internal static float[] RenderSeconds(AmbienceEngine e, double seconds, int block = 1024)
        {
            int frames = (int)Math.Round(seconds * e.SampleRate);
            var all = new float[frames * 2];
            var buf = new float[block * 2];
            for (int at = 0; at < frames; at += block)
            {
                int n = Math.Min(block, frames - at);
                e.Render(buf, n, 2);
                Array.Copy(buf, 0, all, at * 2, n * 2);
            }
            return all;
        }

        private static AmbienceEngine Playing(string id, double volume = 1, double fadeMs = 20, int caveBlock = AmbienceEngine.CaveBlockFrames)
        {
            var e = new AmbienceEngine(Sr, volume, 1, null, caveBlock);
            e.Unlock();
            Assert.That(e.SetAmbience(id, fadeMs), Is.True);
            return e;
        }

        private static double PeakAbs(float[] a, int from, int to)
        {
            double p = 0;
            for (int i = from; i < to; i++) p = Math.Max(p, Math.Abs(a[i]));
            return p;
        }

        /// <summary>docs/AUDIO.md, the ambience table (LUFS at master 1, ambience 1).</summary>
        private static readonly Dictionary<string, double> Levels = new Dictionary<string, double>
        {
            ["title"] = -36, ["hall"] = -37, ["interlude"] = -36.5, ["layer1"] = -36.5, ["layer2"] = -36,
            ["layer3"] = -34, ["layer4"] = -37.5, ["layer5"] = -36, ["layer6"] = -36, ["layer7"] = -34.5,
        };

        [Test, Category("Slow")]
        public void EveryPlaceSitsAtItsDocumentedLevel([ValueSource(typeof(SoundData), nameof(SoundData.AmbienceIds))] string id)
        {
            // 30 s: every sparse voice of every place has fired at least once by then.
            AmbienceEngine e = Playing(id);
            float[] pcm = RenderSeconds(e, 30);
            Assert.That(pcm.All(x => !float.IsNaN(x) && !float.IsInfinity(x)), Is.True);
            DynamicsCompressor comp = DynamicsCompressor.Trial(Sr);
            comp.WarmUp();
            var mastered = (float[])pcm.Clone();
            comp.Process(mastered, mastered.Length / 2, 2);
            double lufs = Loudness.MaxMomentary(mastered, 2, Sr);
            TestContext.Out.WriteLine($"{id}: {lufs:F2} LUFS (AUDIO.md {Levels[id]})");
            Assert.That(lufs, Is.EqualTo(Levels[id]).Within(1.5), id);
            // AUDIO.md: above 4 kHz under 3 % of the power, except layer 1's leaves (14 %).
            double hf = Loudness.PowerShareAbove(pcm, 2, Sr, 4000);
            if (id == "layer1") Assert.That(hf, Is.InRange(0.08, 0.2), id);
            else Assert.That(hf, Is.LessThan(0.03), id);
            Assert.That(e.Errors, Is.EqualTo(0));
        }

        [Test]
        public void TheDryPathWaitsForTheCaveSoBothStayAligned()
        {
            // The 4096-frame cave lags its input by 4096 frames and the dry path is delayed by the same: the whole output is
            // the zero-latency graph's (a 128-frame cave and no delay), 4096 frames later.
            AmbienceEngine big = Playing("layer3");
            AmbienceEngine exact = Playing("layer3", caveBlock: AudioContextLite.RenderQuantumFrames);
            const double seconds = 1.5;
            float[] a = RenderSeconds(big, seconds);
            float[] b = RenderSeconds(exact, seconds);
            int lag = AmbienceEngine.CaveBlockFrames;
            Assert.That(PeakAbs(a, 0, 2 * lag), Is.EqualTo(0), "nothing before the lag");
            Assert.That(PeakAbs(b, 0, b.Length), Is.GreaterThan(1e-4));
            double worst = 0;
            for (int i = 2 * lag; i < a.Length; i++) worst = Math.Max(worst, Math.Abs(a[i] - b[i - 2 * lag]));
            Assert.That(worst, Is.LessThan(1e-6), "the output equals the zero-latency graph's, delayed");
        }

        [Test]
        public void AnyBlockSizeGivesTheSameSamples()
        {
            float[] a = RenderSeconds(Playing("layer1"), 9, 1024);
            AmbienceEngine e = Playing("layer1");
            float[] b = RenderSeconds(e, 9, 333);
            Assert.That(b, Is.EqualTo(a), "the timers fire at the same frames whatever the caller's block");
            float[] c = RenderSeconds(Playing("layer1"), 9, 4096);
            Assert.That(c, Is.EqualTo(a));
        }

        [Test]
        public void TheOldLayerIsKilledFadePlus150MsAfterItsStopInRenderedTime()
        {
            AmbienceEngine e = Playing("layer1", fadeMs: 30);
            RenderSeconds(e, 1);
            Assert.That(e.Layers.Count, Is.EqualTo(1));
            e.SetAmbience("layer2", 30);
            RenderSeconds(e, 0.17, 64);
            Assert.That(e.Layers.Select(L => (L.Id, L.Stopping)), Is.EqualTo(new[] { ("layer1", true), ("layer2", false) }), "both sound during the fade");
            RenderSeconds(e, 0.02, 64);
            Assert.That(e.Layers.Select(L => L.Id), Is.EqualTo(new[] { "layer2" }), "killed 180 ms after the stop");
        }

        [Test]
        public void TheDuckAndTheVolumeAreExactGainsOnceSettled()
        {
            // Two engines fed the same calls; one is ducked (τ 0.2 s) and its volume halved (τ 0.03 s) at 2 s. From 4 s on its
            // output is the other's × 0.4 × 0.5.
            AmbienceEngine plain = Playing("layer6"), ducked = Playing("layer6");
            RenderSeconds(plain, 2);
            RenderSeconds(ducked, 2);
            ducked.DuckAmbience(SoundData.BattleDuck);
            ducked.SetVolume(0.5);
            float[] a = RenderSeconds(plain, 4);
            float[] b = RenderSeconds(ducked, 4);
            double worst = 0, peak = 0;
            for (int i = 2 * 2 * Sr; i < a.Length; i++)
            {
                worst = Math.Max(worst, Math.Abs(b[i] - a[i] * 0.4 * 0.5));
                peak = Math.Max(peak, Math.Abs(a[i]));
            }
            Assert.That(peak, Is.GreaterThan(1e-3));
            Assert.That(worst, Is.LessThan(peak * 1e-5));
            // It glides, it does not jump: over the first 64 frames the gain is still within 5 % of 1.
            double early = 0, earlyPeak = 0;
            for (int i = 0; i < 2 * 64; i++)
            {
                early = Math.Max(early, Math.Abs(b[i] - a[i]));
                earlyPeak = Math.Max(earlyPeak, Math.Abs(a[i]));
            }
            Assert.That(earlyPeak, Is.GreaterThan(0));
            Assert.That(early, Is.LessThan(0.05 * earlyPeak));
        }

        [Test]
        public void AFrozenEngineIsSilentAndItsTimeStandsStill()
        {
            AmbienceEngine kept = Playing("layer5"), frozen = Playing("layer5");
            float[] a1 = RenderSeconds(kept, 2);
            float[] b1 = RenderSeconds(frozen, 2);
            Assert.That(b1, Is.EqualTo(a1));
            frozen.SetFrozen(true);
            double t = frozen.PlayedTime, now = frozen.Now;
            float[] silent = RenderSeconds(frozen, 3);
            Assert.That(PeakAbs(silent, 0, silent.Length), Is.EqualTo(0));
            Assert.That((frozen.PlayedTime, frozen.Now), Is.EqualTo((t, now)), "time stood still");
            frozen.SetFrozen(false);
            float[] a2 = RenderSeconds(kept, 2);
            float[] b2 = RenderSeconds(frozen, 2);
            Assert.That(b2, Is.EqualTo(a2), "it carries on where it stopped");
        }

        [Test]
        public void ADestroyedEngineIsSilent()
        {
            AmbienceEngine e = Playing("title");
            RenderSeconds(e, 1);
            e.Destroy();
            float[] after = RenderSeconds(e, 0.5);
            Assert.That(PeakAbs(after, 0, after.Length), Is.EqualTo(0));
            Assert.That(e.Layers, Is.Empty);
        }

        [Test]
        public void MonoAndSurroundOutputsGetTheSameMix()
        {
            AmbienceEngine st = Playing("hall"), mono = Playing("hall"), six = Playing("hall");
            const int frames = 48000;
            var s = new float[frames * 2];
            var m = new float[frames];
            var x = new float[frames * 6];
            st.Render(s, frames, 2);
            mono.Render(m, frames, 1);
            six.Render(x, frames, 6);
            for (int i = 0; i < frames; i++)
            {
                Assert.That(m[i], Is.EqualTo(0.5f * (s[2 * i] + s[2 * i + 1])), "frame " + i);
                Assert.That((x[6 * i], x[6 * i + 1], x[6 * i + 2], x[6 * i + 5]), Is.EqualTo((s[2 * i], s[2 * i + 1], 0f, 0f)));
            }
        }

        [Test]
        public void CommandsPostedWhileAnotherThreadRendersAreTakenInOrder()
        {
            var e = new AmbienceEngine(Sr, 0.6, 1);
            e.Unlock();
            e.SetAmbience("title", 50);
            var stop = new ManualResetEventSlim();
            Exception failure = null;
            var render = new Thread(() =>
            {
                try
                {
                    var buf = new float[1024 * 2];
                    while (!stop.IsSet) e.Render(buf, 1024, 2);
                }
                catch (Exception ex) { failure = ex; }
            });
            render.Start();
            string[] places = { "hall", "layer1", "layer2", "interlude", "layer7", null, "layer4" };
            foreach (string id in places)
            {
                e.SetAmbience(id, 40);
                e.DuckAmbience(id == null ? 1 : 0.4, 100);
                e.SetVolume(id == null ? 0.2 : 0.6);
                Thread.Sleep(30);
            }
            Thread.Sleep(400);
            stop.Set();
            render.Join();
            Assert.That(failure, Is.Null);
            Assert.That(e.Errors, Is.EqualTo(0));
            Assert.That(e.Ambience, Is.EqualTo("layer4"));
            Assert.That(e.LiveLayer.Id, Is.EqualTo("layer4"));
        }
    }
}
