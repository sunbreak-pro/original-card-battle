// Every one of the 56 named sounds renders at 48 kHz: the render length is the golden voice end plus the 1.4 s room,
// no NaN, a sane peak, the same request gives the same samples, the alternation and the surfaces change the sound, a
// delay is silent before its time; the loudness matches docs/AUDIO.md (±1.5 dB); and every render matches headless
// Chromium's own render of the JS (golden "chromium": loudness and peak within 0.05 dB, the 20 ms envelope within
// 0.1 dB).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Trial.Audio.Synth;

namespace Trial.Audio.Synth.Tests
{
    public class PresetRenderTests
    {
        private const int Sr = 48000;

        public static IEnumerable<string> AllNames() => SoundData.DsSoundNames.Concat(SoundData.MetaSoundNames);

        private static readonly Dictionary<string, RenderedSound> Cache = new Dictionary<string, RenderedSound>();

        private static RenderedSound Rendered(string name)
        {
            lock (Cache)
            {
                if (!Cache.TryGetValue(name, out RenderedSound r))
                {
                    r = PresetRenderer.Render(new SoundRequest { Name = name }, Sr);
                    Cache[name] = r;
                }
                return r;
            }
        }

        /// <summary>The level as AUDIO.md measured it: master 1, effects 1, reverb 0.18, through the (settled) compressor.</summary>
        internal static double MeasuredLufs(RenderedSound r)
        {
            var buf = (float[])r.Interleaved.Clone();
            Array.Resize(ref buf, buf.Length + Sr); // half a second after the sound, past the compressor's 6 ms look-ahead
            DynamicsCompressor comp = DynamicsCompressor.Trial(Sr);
            comp.WarmUp(0.5);
            comp.Process(buf, buf.Length / 2, 2);
            return Loudness.MaxMomentary(buf, 2, Sr);
        }

        private static bool Same(float[] a, float[] b) => a.Length == b.Length && a.SequenceEqual(b);

        // ---- length, peak, determinism -------------------------------------------------------------------------------

        [TestCaseSource(nameof(AllNames))]
        public void RendersWithTheGoldenLengthAndASanePeak(string name)
        {
            RenderedSound r = Rendered(name);
            JsonElement play = Golden.Preset(name).GetProperty("plays")[0];
            Assert.That(r.Ok, Is.True, name + " built");
            Assert.That(r.End, Is.EqualTo(play.GetProperty("end").GetDouble()).Within(1e-6), name + " end");
            Assert.That(r.Frames, Is.EqualTo(play.GetProperty("frames").GetInt32()), name + " frames");
            Assert.That(r.Frames, Is.EqualTo((int)Math.Ceiling(r.End * Sr - 1e-7) + (int)Math.Floor(1.4 * Sr)));
            Assert.That(PresetRenderer.VoiceEnd(new SoundRequest { Name = name }), Is.EqualTo(r.End));
            Assert.That(r.Interleaved.Length, Is.EqualTo(2 * r.Frames));
            Assert.That(r.Interleaved.All(x => !float.IsNaN(x) && !float.IsInfinity(x)), Is.True, name + " finite");
            Assert.That(r.Peak, Is.GreaterThan(0f).And.LessThan(1.6f), name + " peak");
        }

        [TestCaseSource(nameof(AllNames))]
        public void TheSameRequestGivesTheSameSamples(string name)
        {
            RenderedSound again = PresetRenderer.Render(new SoundRequest { Name = name }, Sr);
            Assert.That(Same(again.Interleaved, Rendered(name).Interleaved), Is.True);
        }

        [Test]
        public void RendersOnWorkerThreadsAtOnceGiveTheSameSamples()
        {
            // SfxClipCache renders on worker threads: fresh contexts, shared read-only buffers and tables (at a sample rate no
            // other test has cached yet, so the caches are built under contention too).
            string[] names = { "hitSlash", "guardUp", "stance", "nodeStep", "journalOpen", "consumableUse", "titleEnter", "uiClick" };
            const int rate = 44100;
            var parallel = new RenderedSound[names.Length * 2];
            System.Threading.Tasks.Parallel.For(0, parallel.Length, i => parallel[i] = PresetRenderer.Render(new SoundRequest { Name = names[i % names.Length], Turn = i / names.Length }, rate));
            for (int i = 0; i < parallel.Length; i++)
            {
                RenderedSound one = PresetRenderer.Render(new SoundRequest { Name = names[i % names.Length], Turn = i / names.Length }, rate);
                Assert.That(Same(parallel[i].Interleaved, one.Interleaved), Is.True, names[i % names.Length]);
            }
        }

        [Test]
        public void TheAlternationChangesTheSound()
        {
            float[][] draws = Enumerable.Range(0, 3).Select(t => PresetRenderer.Render(new SoundRequest { Name = "draw", Turn = t }, Sr).Interleaved).ToArray();
            Assert.That(Same(draws[0], draws[1]) || Same(draws[1], draws[2]) || Same(draws[0], draws[2]), Is.False, "three pitch classes");
            float[][] steps = Enumerable.Range(0, 2).Select(t => PresetRenderer.Render(new SoundRequest { Name = "nodeStep", StepTurn = t }, Sr).Interleaved).ToArray();
            // the first step pans right, the second left: the channels swap their levels
            double Energy(float[] x, int ch) => Enumerable.Range(0, x.Length / 2).Sum(i => (double)x[2 * i + ch] * x[2 * i + ch]);
            Assert.That(Energy(steps[0], 1), Is.GreaterThan(Energy(steps[0], 0)), "first step: right");
            Assert.That(Energy(steps[1], 0), Is.GreaterThan(Energy(steps[1], 1)), "second step: left");
        }

        [Test]
        public void EverySurfaceChangesTheStep()
        {
            var renders = SoundData.SurfaceNames.ToDictionary(s => s, s => PresetRenderer.Render(new SoundRequest { Name = "nodeStep", Surface = s }, Sr).Interleaved);
            foreach (string a in SoundData.SurfaceNames)
                foreach (string b in SoundData.SurfaceNames)
                    if (string.CompareOrdinal(a, b) < 0) Assert.That(Same(renders[a], renders[b]), Is.False, a + " vs " + b);
            Assert.That(Same(renders["stone"], Rendered("nodeStep").Interleaved), Is.True, "stone is the start surface");
        }

        [Test]
        public void ADelayIsSilentBeforeItsTime()
        {
            RenderedSound now = Rendered("draw");
            RenderedSound later = PresetRenderer.Render(new SoundRequest { Name = "draw", DelayMs = 100 }, Sr);
            Assert.That(later.End, Is.EqualTo(now.End + 0.1).Within(1e-9));
            // the voice is exactly 0 there; the FFT room adds rounding noise of the order of 1e-18
            Assert.That(later.Interleaved.Take(2 * 4800).Max(Math.Abs), Is.LessThan(1e-12f), "silent for the first 100 ms");
            // the body of the voice (60 ms) is the same, shifted; its last frame may not be: 0.1 + 0.06 + 0.05 s rounds to one
            // frame later than 0.06 + 0.05 s, so the delayed noise plays one more frame at the 1e-4 hold (as in Chromium)
            for (int i = 0; i < 2880; i += 7)
                Assert.That(later.Interleaved[2 * (4800 + i)], Is.EqualTo(now.Interleaved[2 * i]).Within(1e-6), "frame " + i);
        }

        [Test]
        public void ReverbZeroIsTheDryVoiceAndUnknownNamesDoNotBuild()
        {
            RenderedSound dry = PresetRenderer.Render(new SoundRequest { Name = "hitBlunt", Reverb = 0 }, Sr);
            Assert.That(dry.Interleaved.Skip(2 * (int)Math.Ceiling(dry.End * Sr + 0.3 * Sr)).All(x => Math.Abs(x) < 1e-6f), Is.True, "no room tail");
            RenderedSound none = PresetRenderer.Render(new SoundRequest { Name = "nowhere" }, Sr);
            Assert.That(none.Ok, Is.False);
        }

        // ---- levels ---------------------------------------------------------------------------------------------------

        /// <summary>docs/AUDIO.md:119-130 (max momentary loudness, BS.1770, master 1).</summary>
        private static readonly Dictionary<string, double> AudioMdLevels = new Dictionary<string, double>
        {
            ["foeTurn"] = -20.5, ["stance"] = -20.5,
            ["descend"] = -19.4, ["miasmaDeath"] = -20.4, ["lifeEndDeath"] = -19.7, ["layerCleared"] = -21, ["heirNamed"] = -22.6, ["miasmaLine"] = -22.7,
            ["traceTake"] = -24.7, ["survived"] = -25.3, ["interlude"] = -26, ["eventResolve"] = -31,
            ["nodeStep"] = -35, ["nodeReturn"] = -42, ["nodeHover"] = -61,
        };

        /// <summary>docs/AUDIO.md's ranges: the battle's hits −26 to −29 (the blows' system hits and the body's), the UI −38 to −48.</summary>
        private static readonly Dictionary<string, (double lo, double hi)> AudioMdRanges = new Dictionary<string, (double, double)>
        {
            ["hitSlash"] = (-29, -26), ["hitThrust"] = (-29, -26), ["hitBlunt"] = (-29, -26), ["hitBody"] = (-29, -26),
            ["uiClick"] = (-48, -38), ["uiConfirm"] = (-48, -38), ["uiCancel"] = (-48, -38),
        };

        public static IEnumerable<string> AudioMdNames() => AudioMdLevels.Keys.Concat(AudioMdRanges.Keys);

        [TestCaseSource(nameof(AudioMdNames))]
        public void LoudnessMatchesAudioMd(string name)
        {
            double lufs = MeasuredLufs(Rendered(name));
            if (AudioMdLevels.TryGetValue(name, out double target)) Assert.That(lufs, Is.EqualTo(target).Within(1.5), name);
            else
            {
                (double lo, double hi) = AudioMdRanges[name];
                Assert.That(lufs, Is.InRange(lo - 1.5, hi + 1.5), name);
            }
        }

        /// <summary>
        /// AUDIO.md gives −23.5 for titleEnter, measured with node-web-audio-api, whose sawtooth is not Chromium's band-limited,
        /// peak-normalised table. Chromium renders the same graph at −25.08 (golden "chromium"), and so does the port; the
        /// browser wins.
        /// </summary>
        [Test]
        public void TitleEnterFollowsChromiumNotAudioMd()
        {
            double lufs = MeasuredLufs(Rendered("titleEnter"));
            Assert.That(lufs, Is.EqualTo(-25.08).Within(0.1));
            Assert.That(lufs, Is.Not.EqualTo(-23.5).Within(1.5), "if this fails, AUDIO.md and the port agree again: fold the row back into the table");
        }

        // ---- Chromium ------------------------------------------------------------------------------------------------

        public static IEnumerable<string> ChromiumKeys() =>
            Golden.Root.TryGetProperty("chromium", out JsonElement c) ? c.GetProperty("renders").EnumerateObject().Select(p => p.Name).ToArray() : new string[0];

        private static SoundRequest RequestFor(JsonElement render, double delayMs)
        {
            var r = new SoundRequest { Name = render.GetProperty("name").GetString(), DelayMs = delayMs };
            if (render.GetProperty("opts").ValueKind == JsonValueKind.Object)
            {
                JsonElement o = render.GetProperty("opts");
                if (o.TryGetProperty("surface", out JsonElement s)) r.Surface = s.GetString();
                if (o.TryGetProperty("pan", out JsonElement p)) r.Pan = p.GetDouble();
            }
            if (render.GetProperty("mult").ValueKind == JsonValueKind.Number) r.Mult = render.GetProperty("mult").GetDouble();
            if (render.GetProperty("line").ValueKind == JsonValueKind.Number)
            {
                SoundCall c = SoundRules.MiasmaLineCall(render.GetProperty("line").GetDouble(), delayMs);
                r.Mult = c.Mult;
                r.PitchMul = c.PitchMul;
            }
            return r;
        }

        [TestCaseSource(nameof(ChromiumKeys))]
        public void MatchesChromiumsRender(string key)
        {
            JsonElement chromium = Golden.Root.GetProperty("chromium");
            JsonElement render = chromium.GetProperty("renders").GetProperty(key);
            double start = chromium.GetProperty("startSeconds").GetDouble();
            int block = (int)(chromium.GetProperty("blockMs").GetDouble() * Sr / 1000);
            RenderedSound r = PresetRenderer.Render(RequestFor(render, start * 1000), Sr);
            Assert.That(r.Ok, Is.True);
            // the game's graph at master 1, effects 1: the sound, then the compressor (fresh, as the context's; 0.5 s of silence settles it)
            var buf = (float[])r.Interleaved.Clone();
            Array.Resize(ref buf, 2 * 7 * Sr);
            DynamicsCompressor.Trial(Sr).Process(buf, 7 * Sr, 2);
            Assert.That(Loudness.MaxMomentary(buf, 2, Sr), Is.EqualTo(render.GetProperty("lufs").GetDouble()).Within(0.05), key + " loudness");
            float peak = buf.Max(Math.Abs);
            Assert.That(20 * Math.Log10(peak), Is.EqualTo(render.GetProperty("peakDb").GetDouble()).Within(0.05), key + " peak");
            JsonElement[] blocks = render.GetProperty("blocks").EnumerateArray().ToArray();
            for (int b = 0; b < blocks.Length; b++)
            {
                if (blocks[b].ValueKind != JsonValueKind.Number) continue;
                double refDb = blocks[b].GetDouble();
                if (refDb < -90) continue;
                double e = 0;
                for (int i = b * block; i < (b + 1) * block; i++) e += (double)buf[2 * i] * buf[2 * i] + (double)buf[2 * i + 1] * buf[2 * i + 1];
                double db = 10 * Math.Log10(e / (2 * block));
                Assert.That(db, Is.EqualTo(refDb).Within(refDb > -60 ? 0.1 : 1.0), $"{key} block {b} ({b * 20} ms)");
            }
        }
    }
}
