// The ambience builds the same graph as the JS, call for call: for every place, setAmbience(id, 20) right after unlock and
// then one minute of scheduler ticks every 0.25 s (unity-port/tools/audio-golden.mjs ambienceScenario): every node, type,
// buffer, connection, param call and start / stop, numbers within 1e-6, and the layer's summary (kept sources, sparse
// firings).
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Trial.Audio.Synth;

namespace Trial.Audio.Synth.Tests
{
    public class AmbienceParityTests
    {
        private static IEnumerable<string> Places() => Golden.Array("ambience").Select(a => a.GetProperty("id").GetString());

        private static JsonElement Scenario(string id) => Golden.Array("ambience").First(a => a.GetProperty("id").GetString() == id);

        /// <summary>js: freshAudio() + segment + setAmbience(id, fadeMs) + the tick loop of ambienceScenario.</summary>
        internal static (AmbienceEngine engine, RecordingCallLog log, bool ok) Replay(string id, double fadeMs, double seconds, double step)
        {
            var log = new RecordingCallLog();
            var e = new AmbienceEngine(48000, 0.6, 1, log) { NowOverride = 0 };
            e.Unlock();
            e.Flush();
            log.BeginSegment(e.Ctx);
            bool ok = e.SetAmbience(id, fadeMs);
            e.Flush();
            int ticks = (int)System.Math.Round(seconds / step);
            for (int i = 1; i <= ticks; i++)
            {
                e.NowOverride = i * step;
                e.Tick();
            }
            return (e, log, ok);
        }

        [Test]
        public void EveryPlaceIsInTheGolden()
        {
            Assert.That(Places(), Is.EqualTo(SoundData.AmbienceIds));
            Assert.That(AmbienceBuilders.Ids, Is.EqualTo(SoundData.AmbienceIds), "a builder per place, in the JS key order");
        }

        [TestCaseSource(nameof(Places))]
        public void OneMinuteOfAPlaceEqualsTheJs(string id)
        {
            JsonElement g = Scenario(id);
            (AmbienceEngine e, RecordingCallLog log, bool ok) = Replay(id, g.GetProperty("fadeMs").GetDouble(), g.GetProperty("seconds").GetDouble(), g.GetProperty("tick").GetDouble());
            Assert.That(ok, Is.EqualTo(g.GetProperty("ok").GetBoolean()));
            Golden.AssertLog(g.GetProperty("log"), log.Entries, id);
            Assert.That(log.Entries.Count(x => x.Length == 2 && (string)x[1] == "new"), Is.EqualTo(g.GetProperty("nodes").GetInt32()), "nodes made");

            JsonElement[] gl = g.GetProperty("layers").EnumerateArray().ToArray();
            Assert.That(e.Layers.Count, Is.EqualTo(gl.Length));
            for (int i = 0; i < gl.Length; i++)
            {
                AmbienceLayer L = e.Layers[i];
                Assert.That(L.Id, Is.EqualTo(gl[i].GetProperty("id").GetString()));
                Assert.That(L.Stopping, Is.EqualTo(gl[i].GetProperty("stopping").GetBoolean()));
                Assert.That(L.Nodes.Count, Is.EqualTo(gl[i].GetProperty("nodes").GetInt32()), id + ": kept sources");
                Assert.That(L.Sparse.Select(s => s.K), Is.EqualTo(gl[i].GetProperty("sparse").EnumerateArray().Select(x => x.GetInt32())), id + ": sparse firings");
            }
            Assert.That(e.Errors, Is.EqualTo(0));
        }

        [Test]
        public void TheLayerGeneratorIsSeededByThePlace()
        {
            JsonElement seeds = Golden.Root.GetProperty("seeds").GetProperty("layerRnd");
            foreach (string id in SoundData.AmbienceIds)
            {
                var L = new AmbienceLayer(id, SoundData.AmbienceDefs[id]);
                double[] expected = seeds.GetProperty(id).EnumerateArray().Select(x => x.GetDouble()).ToArray();
                for (int i = 0; i < expected.Length; i++) Assert.That(L.R(), Is.EqualTo(expected[i]), id + " #" + i);
            }
        }
    }
}
