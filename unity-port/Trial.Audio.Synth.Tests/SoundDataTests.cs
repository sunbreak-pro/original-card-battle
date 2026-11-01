// The data matches the JS: the preset names and their order, every timbre a preset names exists and is labelled, the
// meta presets stay under the loudest battle hit, the surfaces and places line up (test/audio/Audio.test.mjs:159-229),
// and every table equals the JS one field by field (labels byte for byte, numbers exactly; golden "data").
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Trial.Audio.Synth;

namespace Trial.Audio.Synth.Tests
{
    public class SoundDataTests
    {
        [Test]
        public void NamesAndOrderEqualTheJs()
        {
            Assert.That(SoundData.DsSoundNames, Is.EqualTo(Golden.Array("battleNames").Select(x => x.GetString()).ToArray()));
            Assert.That(SoundData.MetaSoundNames, Is.EqualTo(Golden.Array("metaNames").Select(x => x.GetString()).ToArray()));
            Assert.That(SoundData.DsSoundNames.Length, Is.EqualTo(29));
            Assert.That(SoundData.MetaSoundNames.Length, Is.EqualTo(27));
            Assert.That(SoundData.MetaSoundNames.Intersect(SoundData.DsSoundNames), Is.Empty, "a meta name never collides with a battle name");
            Assert.That(SoundData.DsSoundDefaults().Keys, Is.EquivalentTo(SoundData.DsSoundNames));
            Assert.That(SoundData.MetaSoundDefaults().Keys, Is.EquivalentTo(SoundData.MetaSoundNames));
        }

        [Test]
        public void EveryTimbreExistsAndIsLabelled()
        {
            Assert.That(SoundData.DsTimbreNames.Length, Is.EqualTo(16));
            Assert.That(SoundData.MetaTimbreNames.Length, Is.EqualTo(24));
            Assert.That(MetaSynth.Timbres.Length, Is.EqualTo(26), "the 24 labelled ones plus thud and swish");
            foreach (string t in SoundData.MetaTimbreNames) Assert.That(MetaSynth.Has(t), Is.True, "labelled timbre " + t + " is implemented");
            foreach (string t in SoundData.DsTimbreNames) Assert.That(SoundData.DsTimbres.ContainsKey(t), Is.True);
            var ctx = new AudioContextLite(48000);
            var ds = DsSound.OnBuses(ctx, ctx.CreateGain(), ctx.CreateGain(), Golden.DsNoise);
            foreach (var kv in SoundData.DsSoundDefaults())
            {
                Assert.That(SoundData.DsTimbres.ContainsKey(kv.Value.Timbre), Is.True, kv.Key);
                Assert.That(ds.Synth(kv.Value.Timbre, 0, kv.Value.Pitch, kv.Value.Dur / 1000, kv.Value.Vol), Is.True, kv.Key);
            }
            foreach (var kv in SoundData.MetaSoundDefaults())
            {
                SoundPreset p = kv.Value;
                Assert.That(MetaSynth.Has(p.Timbre), Is.True, kv.Key + ": timbre " + p.Timbre);
                Assert.That(SoundData.MetaTimbres.ContainsKey(p.Timbre), Is.True, kv.Key + ": label");
                Assert.That(SoundData.MetaGroups.ContainsKey(p.Group), Is.True, kv.Key + ": group");
                Assert.That(new[] { p.Vol, p.Pitch, p.Dur, p.Tone }.All(x => x > 0), Is.True, kv.Key);
                Assert.That(p.Vol, Is.LessThanOrEqualTo(0.3), kv.Key + ": louder than the loudest battle hit");
                Assert.That(p.Label, Is.Not.Empty);
            }
            Assert.That(SoundData.MetaSoundDefaults()["nodeHover"].Gap, Is.EqualTo(60));
            Assert.That(SoundData.MetaSoundDefaults()["uiClick"].Gap, Is.EqualTo(40));
            Assert.That(SoundData.MetaSoundDefaults().Count(kv => kv.Value.Gap.HasValue), Is.EqualTo(2));
        }

        [Test]
        public void PlacesAndSurfacesLineUp()
        {
            Assert.That(SoundData.AmbienceIds, Is.EqualTo(new[] { "title", "hall", "interlude", "layer1", "layer2", "layer3", "layer4", "layer5", "layer6", "layer7" }));
            Assert.That(SoundData.AmbienceDefs.Keys, Is.EquivalentTo(SoundData.AmbienceIds));
            foreach (var kv in SoundData.AmbienceDefs) Assert.That(SoundData.Surfaces.ContainsKey(kv.Value.Surface), Is.True, kv.Key);
            Assert.That(SoundData.SurfaceNames, Is.EquivalentTo(SoundData.Surfaces.Keys));
            Assert.That(SoundData.Surfaces["sand"].Type, Is.EqualTo(BiquadType.Highpass));
            Assert.That(SoundData.Surfaces["damp"].Wet, Is.EqualTo(0.4));
            Assert.That(SoundData.Surfaces["glass"].Ring, Is.EqualTo(0.16));
            Assert.That(SoundData.Surfaces["stone"].Crackle, Is.EqualTo(0));
        }

        // ---- byte for byte against the JS tables (golden "data") --------------------------------------------------------

        private static JsonElement Data => Golden.Root.GetProperty("data");

        private static string[] Keys(JsonElement obj) => obj.EnumerateObject().Select(p => p.Name).ToArray();

        private static double? Num(JsonElement obj, string key) => obj.TryGetProperty(key, out JsonElement v) ? v.GetDouble() : (double?)null;

        private static void AssertPresets(JsonElement js, IReadOnlyList<string> names, Dictionary<string, SoundPreset> mine, bool meta)
        {
            Assert.That(Keys(js), Is.EqualTo(names), "names in the JS key order");
            Assert.That(mine.Keys, Is.EquivalentTo(names));
            var allowed = meta ? new[] { "label", "group", "spec", "role", "timbre", "vol", "pitch", "dur", "tone", "gap" } : new[] { "label", "spec", "role", "timbre", "vol", "pitch", "dur", "tone" };
            foreach (JsonProperty p in js.EnumerateObject())
            {
                JsonElement j = p.Value;
                SoundPreset c = mine[p.Name];
                Assert.That(Keys(j).Except(allowed), Is.Empty, p.Name + ": a JS field the port does not carry");
                Assert.That(c.Label, Is.EqualTo(j.GetProperty("label").GetString()), p.Name + ".label");
                Assert.That(c.Spec, Is.EqualTo(j.GetProperty("spec").GetString()), p.Name + ".spec");
                Assert.That(c.Role, Is.EqualTo(j.GetProperty("role").GetString()), p.Name + ".role");
                Assert.That(c.Timbre, Is.EqualTo(j.GetProperty("timbre").GetString()), p.Name + ".timbre");
                Assert.That(c.Group, Is.EqualTo(j.TryGetProperty("group", out JsonElement g) ? g.GetString() : null), p.Name + ".group");
                Assert.That(c.Vol, Is.EqualTo(j.GetProperty("vol").GetDouble()), p.Name + ".vol");
                Assert.That(c.Pitch, Is.EqualTo(j.GetProperty("pitch").GetDouble()), p.Name + ".pitch");
                Assert.That(c.Dur, Is.EqualTo(j.GetProperty("dur").GetDouble()), p.Name + ".dur");
                Assert.That(c.Tone, Is.EqualTo(j.GetProperty("tone").GetDouble()), p.Name + ".tone");
                Assert.That(c.Gap, Is.EqualTo(Num(j, "gap")), p.Name + ".gap");
                Assert.That(c.Label.Length > 0 && c.Role.Length > 0 && c.Spec.Length > 0, Is.True, p.Name + ": label, role and spec (Audio.test.mjs:197)");
            }
        }

        private static void AssertLabels(JsonElement js, IReadOnlyList<string> names, IReadOnlyDictionary<string, string> mine)
        {
            Assert.That(Keys(js), Is.EqualTo(names), "keys in the JS order");
            Assert.That(mine.Keys, Is.EquivalentTo(names));
            foreach (JsonProperty p in js.EnumerateObject()) Assert.That(mine[p.Name], Is.EqualTo(p.Value.GetString()), p.Name);
        }

        [Test]
        public void PresetsAndLabelsEqualTheJsByteForByte()
        {
            AssertLabels(Data.GetProperty("dsTimbres"), SoundData.DsTimbreNames, SoundData.DsTimbres);
            AssertLabels(Data.GetProperty("metaTimbres"), SoundData.MetaTimbreNames, SoundData.MetaTimbres);
            AssertLabels(Data.GetProperty("metaGroups"), SoundData.MetaGroupNames, SoundData.MetaGroups);
            AssertPresets(Data.GetProperty("dsSounds"), SoundData.DsSoundNames, SoundData.DsSoundDefaults(), meta: false);
            AssertPresets(Data.GetProperty("metaSounds"), SoundData.MetaSoundNames, SoundData.MetaSoundDefaults(), meta: true);
        }

        [Test]
        public void SurfacesAndPlacesEqualTheJsByteForByte()
        {
            JsonElement surfaces = Data.GetProperty("surfaces");
            Assert.That(Keys(surfaces), Is.EqualTo(SoundData.SurfaceNames));
            foreach (JsonProperty p in surfaces.EnumerateObject())
            {
                JsonElement j = p.Value;
                Surface c = SoundData.Surfaces[p.Name];
                Assert.That(Keys(j).Except(new[] { "label", "thud", "heel", "type", "f", "q", "scuff", "crackle", "wet", "ring" }), Is.Empty, p.Name);
                Assert.That(c.Label, Is.EqualTo(j.GetProperty("label").GetString()), p.Name);
                Assert.That(BiquadFilterNode.TypeName(c.Type), Is.EqualTo(j.GetProperty("type").GetString()), p.Name + ".type");
                Assert.That(new[] { c.Thud, c.Heel, c.F, c.Q, c.Scuff, c.Crackle, c.Wet, c.Ring },
                    Is.EqualTo(new[] { "thud", "heel", "f", "q", "scuff", "crackle", "wet", "ring" }.Select(k => Num(j, k) ?? 0).ToArray()), p.Name);
            }
            Assert.That(SoundData.AmbienceIds, Is.EqualTo(Data.GetProperty("ambienceIds").EnumerateArray().Select(x => x.GetString()).ToArray()));
            Assert.That(SoundData.AmbienceIds, Is.EquivalentTo(Data.GetProperty("ambienceBuilderIds").EnumerateArray().Select(x => x.GetString())), "every place has a builder");
            JsonElement defs = Data.GetProperty("ambienceDefs");
            Assert.That(Keys(defs), Is.EqualTo(SoundData.AmbienceIds));
            foreach (JsonProperty p in defs.EnumerateObject())
            {
                JsonElement j = p.Value;
                AmbienceDef c = SoundData.AmbienceDefs[p.Name];
                Assert.That(Keys(j), Is.EquivalentTo(new[] { "label", "place", "surface", "level", "room", "desc" }), p.Name);
                Assert.That(new[] { c.Label, c.Place, c.Surface, c.Desc }, Is.EqualTo(new[] { "label", "place", "surface", "desc" }.Select(k => j.GetProperty(k).GetString()).ToArray()), p.Name);
                Assert.That(c.Level, Is.EqualTo(j.GetProperty("level").GetDouble()), p.Name + ".level");
                Assert.That(c.Room, Is.EqualTo(j.GetProperty("room").GetDouble()), p.Name + ".room");
            }
        }

        [Test]
        public void SettingsAndConstantsEqualTheJs()
        {
            JsonElement d = Data.GetProperty("soundSettingsDefaults");
            Assert.That(SoundData.SoundSettingsDefaults(), Is.EqualTo(new SoundSettings
            {
                Master = d.GetProperty("master").GetDouble(), Effects = d.GetProperty("effects").GetDouble(), Ambience = d.GetProperty("ambience").GetDouble(),
                Reverb = d.GetProperty("reverb").GetDouble(), Muted = d.GetProperty("muted").GetBoolean(),
            }));
            JsonElement app = Data.GetProperty("appSettingsDefaults");
            Assert.That(app.TryGetProperty("reverb", out _), Is.False, "the app never persists reverb (it stays at the module default)");
            Assert.That(SoundData.GameSoundSettingsDefaults(), Is.EqualTo(new SoundSettings
            {
                Master = app.GetProperty("master").GetDouble(), Effects = app.GetProperty("effects").GetDouble(), Ambience = app.GetProperty("ambience").GetDouble(),
                Reverb = d.GetProperty("reverb").GetDouble(), Muted = app.GetProperty("muted").GetBoolean(),
            }));
            JsonElement k = Data.GetProperty("constants");
            Assert.That(SoundData.MiasmaLinePitch, Is.EqualTo(k.GetProperty("MIASMA_LINE_PITCH").EnumerateArray().Select(x => x.GetDouble()).ToArray()));
            // (the JS value is the actual one here: the C# side is a const)
            Assert.That(k.GetProperty("RESULT_AFTER_STEP_MS").GetDouble(), Is.EqualTo(SoundData.ResultAfterStepMs));
            Assert.That(k.GetProperty("LOOKAHEAD_S").GetDouble(), Is.EqualTo(SoundData.LookaheadS));
            Assert.That(k.GetProperty("TICK_MS").GetDouble(), Is.EqualTo(SoundData.TickMs));
            Assert.That(k.GetProperty("NOISE_SECONDS").GetDouble(), Is.EqualTo(AudioBuffers.NoiseSeconds));
        }

        /// <summary>test/audio/Audio.test.mjs:210-219: every name the dev page (dev/audio.html) plays or sets exists.</summary>
        [Test]
        public void EveryNameTheDevPageUsesExists()
        {
            JsonElement dev = Data.GetProperty("devPage");
            string[] plays = dev.GetProperty("plays").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.That(plays.Length, Is.GreaterThan(5), "dev page names found");
            foreach (string n in plays) Assert.That(SoundData.DefaultPreset(n), Is.Not.Null, "dev page plays unknown preset " + n);
            foreach (JsonElement a in dev.GetProperty("ambiences").EnumerateArray())
            {
                Assert.That(SoundRules.TryAmbienceId(a.GetString(), out string id), Is.True, a.GetString());
                Assert.That(id, Is.Not.Null);
            }
        }

        [Test]
        public void PresetCopiesAreIndependent()
        {
            var a = SoundData.DsSoundDefaults();
            a["hitSlash"].Vol = 0.5;
            Assert.That(SoundData.DsSoundDefaults()["hitSlash"].Vol, Is.EqualTo(0.30));
            Assert.That(SoundData.DefaultPreset("hitSlash").Vol, Is.EqualTo(0.30));
            Assert.That(SoundData.DefaultPreset("nowhere"), Is.Null);
        }
    }
}
