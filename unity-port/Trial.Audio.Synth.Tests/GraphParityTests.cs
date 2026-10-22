// The C# synths build the same graph as the JS, call for call: every preset's three plays (node creation, types,
// buffers, connections, every param call, start / stop, numbers within 1e-6), the footsteps on every surface, the calls
// with delays / mults / pans / pitchMul, the step plan, the miasma lines, the gaps and the design system's hover
// throttle, and the design system's own graph setup (ensure()).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Trial.Audio.Synth;

namespace Trial.Audio.Synth.Tests
{
    /// <summary>
    /// The routing of createAudio().play / stepTo / miasmaLine (scenes/audio/Audio.js:713-808), as small as the replay needs:
    /// battle names to the design system's synth (returning "unlocked"), meta names through the gap gate to the meta synth.
    /// TrialAudio (the Unity side) holds the real one.
    /// </summary>
    internal sealed class JsAudioRouter
    {
        public readonly AudioContextLite DsCtx, MetaCtx;
        public readonly RecordingCallLog DsLog = new RecordingCallLog(), MetaLog = new RecordingCallLog();
        public readonly DsSound Ds;
        public readonly MetaSynth Meta;
        private readonly SoundRules.GapGate gaps = new SoundRules.GapGate();

        public JsAudioRouter()
        {
            DsCtx = new AudioContextLite(48000, DsLog);
            GainNode outBus = DsCtx.CreateGain(); outBus.LogName = "out";
            GainNode wetBus = DsCtx.CreateGain(); wetBus.LogName = "wet";
            Ds = DsSound.OnBuses(DsCtx, outBus, wetBus, Golden.DsNoise);
            MetaCtx = new AudioContextLite(48000, MetaLog);
            GainNode fx = MetaCtx.CreateGain(); fx.LogName = "fx";
            GainNode fxWet = MetaCtx.CreateGain(); fxWet.LogName = "fxWet";
            Meta = new MetaSynth(MetaCtx, fx, fxWet, Golden.Bank);
            DsLog.BeginSegment(DsCtx);
            MetaLog.BeginSegment(MetaCtx);
        }

        public void At(double now)
        {
            DsCtx.RenderUntil(now);
            MetaCtx.RenderUntil(now);
            Assert.That(DsCtx.CurrentTime, Is.EqualTo(now).Within(1e-12), "the scenario's times are whole quanta");
        }

        // js: scenes/audio/Audio.js:713
        public bool Play(string name, double delayMs = 0, double? mult = null, VoiceRouting o = null)
        {
            if (SoundData.IsBattleName(name))
            {
                Ds.Play(name, delayMs, mult);
                return true; // unlocked
            }
            SoundPreset pr = SoundData.DefaultPreset(name);
            if (pr == null) return false;
            double now = MetaCtx.CurrentTime;
            if (!gaps.TryAccept(name, pr.Gap, now)) return false;
            return Meta.PlayPreset(pr, now, delayMs, mult, o);
        }

        // js: scenes/audio/Audio.js:797
        public string[] StepTo(bool fresh, double line, bool miasmaDeath, string result)
        {
            string[] plan = SoundRules.StepPlan(fresh, line, miasmaDeath, result);
            Play(plan[0]);
            if (plan.Length > 1 && plan[1] == "miasmaLine") MiasmaLine(line, SoundData.ResultAfterStepMs);
            else if (plan.Length > 1) Play(plan[1], SoundData.ResultAfterStepMs);
            return plan;
        }

        // js: scenes/audio/Audio.js:805
        public bool MiasmaLine(double pct, double delayMs = 0)
        {
            SoundCall c = SoundRules.MiasmaLineCall(pct, delayMs);
            return Play("miasmaLine", c.DelayMs, c.Mult, new VoiceRouting { PitchMul = c.PitchMul });
        }
    }

    public class GraphParityTests
    {
        public static IEnumerable<string> PresetNames() => SoundData.DsSoundNames.Concat(SoundData.MetaSoundNames);

        private static VoiceRouting Routing(JsonElement opts)
        {
            if (opts.ValueKind != JsonValueKind.Object) return null;
            var o = new VoiceRouting();
            if (opts.TryGetProperty("surface", out JsonElement s)) o.Surface = s.GetString();
            if (opts.TryGetProperty("pan", out JsonElement p)) o.Pan = p.GetDouble();
            if (opts.TryGetProperty("pitchMul", out JsonElement m)) o.PitchMul = m.GetDouble();
            return o;
        }

        private static void ReplayPreset(JsonElement golden, string label)
        {
            string name = golden.GetProperty("name").GetString();
            VoiceRouting o = Routing(golden.GetProperty("options"));
            var router = new JsAudioRouter();
            bool battle = golden.GetProperty("kind").GetString() == "ds";
            Assert.That(battle, Is.EqualTo(SoundData.IsBattleName(name)));
            var ends = new List<double>();
            foreach (JsonElement play in golden.GetProperty("plays").EnumerateArray())
            {
                double now = play.GetProperty("now").GetDouble();
                router.At(now);
                AudioContextLite ctx = battle ? router.DsCtx : router.MetaCtx;
                ctx.LatestStopTime = 0;
                bool ok = router.Play(name, 0, null, o);
                Assert.That(ok, Is.EqualTo(play.GetProperty("ok").GetBoolean()), label + " play result");
                Assert.That(ctx.LatestStopTime - now, Is.EqualTo(play.GetProperty("end").GetDouble()).Within(1e-6), label + " end");
            }
            Golden.AssertLog(golden.GetProperty("log"), battle ? router.DsLog.Entries : router.MetaLog.Entries, label);
            Assert.That(battle ? router.MetaLog.Entries.Count : router.DsLog.Entries.Count, Is.EqualTo(0), "nothing on the other context");
        }

        [TestCaseSource(nameof(PresetNames))]
        public void PresetBuildsTheJsGraph(string name) => ReplayPreset(Golden.Preset(name), name);

        public static IEnumerable<TestCaseData> SurfaceCases() =>
            Golden.Array("surfaces").Select(s => new TestCaseData(s.GetProperty("name").GetString(), s.GetProperty("surface").GetString()).SetName($"Step {{0}} on {{1}}"));

        [TestCaseSource(nameof(SurfaceCases))]
        public void StepOnEverySurfaceBuildsTheJsGraph(string name, string surface)
        {
            JsonElement g = Golden.Array("surfaces").First(s => s.GetProperty("name").GetString() == name && s.GetProperty("surface").GetString() == surface);
            ReplayPreset(g, name + " on " + surface);
        }

        public static IEnumerable<string> CallScenarios() => Golden.Array("calls").Select(c => c.GetProperty("label").GetString());

        [TestCaseSource(nameof(CallScenarios))]
        public void CallScenarioBuildsTheJsGraph(string label)
        {
            JsonElement scenario = Golden.Array("calls").First(c => c.GetProperty("label").GetString() == label);
            var router = new JsAudioRouter();
            JsonElement[] results = scenario.GetProperty("results").EnumerateArray().ToArray();
            int i = 0;
            foreach (JsonElement call in scenario.GetProperty("calls").EnumerateArray())
            {
                router.At(call.GetProperty("now").GetDouble());
                string fn = call.GetProperty("fn").GetString();
                JsonElement[] a = call.GetProperty("args").EnumerateArray().ToArray();
                JsonElement expected = results[i++];
                switch (fn)
                {
                    case "play":
                        {
                            string name = a[0].GetString();
                            double delay = a.Length > 1 && a[1].ValueKind == JsonValueKind.Number ? a[1].GetDouble() : 0;
                            double? mult = a.Length > 2 && a[2].ValueKind == JsonValueKind.Number ? a[2].GetDouble() : (double?)null;
                            VoiceRouting o = a.Length > 3 ? Routing(a[3]) : null;
                            Assert.That(router.Play(name, delay, mult, o), Is.EqualTo(expected.GetBoolean()), label + " " + call.GetRawText());
                            break;
                        }
                    case "stepTo":
                        {
                            JsonElement s = a[0];
                            bool fresh = !s.TryGetProperty("fresh", out JsonElement f) || f.GetBoolean();
                            double line = s.TryGetProperty("line", out JsonElement l) ? l.GetDouble() : 0;
                            bool death = s.TryGetProperty("miasmaDeath", out JsonElement d) && d.GetBoolean();
                            string result = s.TryGetProperty("result", out JsonElement r) ? r.GetString() : null;
                            string[] plan = router.StepTo(fresh, line, death, result);
                            Assert.That(plan, Is.EqualTo(expected.EnumerateArray().Select(x => x.GetString()).ToArray()), label + " plan");
                            break;
                        }
                    case "miasmaLine":
                        {
                            double delay = a.Length > 1 ? a[1].GetDouble() : 0;
                            Assert.That(router.MiasmaLine(a[0].GetDouble(), delay), Is.EqualTo(expected.GetBoolean()));
                            break;
                        }
                    default:
                        Assert.Fail("unknown call " + fn);
                        break;
                }
            }
            Golden.AssertLog(scenario.GetProperty("meta").GetProperty("log"), router.MetaLog.Entries, label + " (meta context)");
            Golden.AssertLog(scenario.GetProperty("ds").GetProperty("log"), router.DsLog.Entries, label + " (design system context)");
        }

        [Test]
        public void HoverIsThrottledLikeTheDesignSystem()
        {
            // the "gaps" scenario: hover at 0, 0.024, 0.064, 0.08 → the JS plays 0, 0.024 (a last hover at time 0 is falsy: no
            // throttle), refuses 0.064 (40 ms after 0.024), plays 0.08 (56 ms after it)
            JsonElement scenario = Golden.Array("calls").First(c => c.GetProperty("label").GetString() == "gaps");
            int starts = scenario.GetProperty("ds").GetProperty("log").EnumerateArray().Count(e => e[1].GetString() == "start");
            Assert.That(starts, Is.EqualTo(3), "three hovers sounded (one BufferSource each)");
        }

        [Test]
        public void DesignSystemSetupBuildsTheJsGraph()
        {
            var log = new RecordingCallLog();
            var ctx = new AudioContextLite(48000, log);
            var ds = new DsSound(ctx);
            ds.Unlock();
            // createAudio's applyBase after unlock (master × effects, reverb)
            ds.SetMaster(0.8 * 1.0);
            ds.SetReverb(0.18);
            List<object[]> golden = Golden.Canonical(Golden.ToObjects(Golden.Root.GetProperty("setup").GetProperty("ds")));
            List<object[]> mine = Golden.Canonical(log.Entries);
            var doc = JsonDocument.Parse(JsonSerializer.Serialize(golden));
            Golden.AssertLog(doc.RootElement, mine, "ds setup");
            Assert.That(ds.Out.Gain.Value, Is.EqualTo(0.8));
            Assert.That(ds.Wet.Gain.Value, Is.EqualTo(0.18));
        }

        [Test]
        public void DesignSystemPlayRules()
        {
            var ctx = new AudioContextLite(48000);
            var ds = new DsSound(ctx);
            Assert.That(ds.Play("hitSlash"), Is.False, "silent before unlock");
            ds.Unlock();
            Assert.That(ds.Play("hitSlash"), Is.True);
            Assert.That(ds.Turn, Is.EqualTo(1));
            Assert.That(ds.Play("nowhere"), Is.False, "unknown");
            Assert.That(ds.Turn, Is.EqualTo(1), "a refused play does not count");
            ds.Muted = true;
            Assert.That(ds.Play("hitSlash"), Is.False, "muted");
            ds.Muted = false;
            ds.Presets["hitSlash"].Timbre = "nothing";
            Assert.That(ds.Play("hitSlash"), Is.False, "a bad preset is ignored");
            ds.Reset();
            Assert.That(ds.Presets["hitSlash"].Timbre, Is.EqualTo("slash"));
        }

        [Test]
        public void MetaPlayCountsAndAlternates()
        {
            var ctx = new AudioContextLite(48000);
            var meta = new MetaSynth(ctx, ctx.CreateGain(), ctx.CreateGain(), Golden.Bank);
            SoundPreset step = SoundData.DefaultPreset("nodeStep");
            Assert.That(meta.PlayPreset(step, 0), Is.True);
            Assert.That(meta.Turn, Is.EqualTo(1));
            Assert.That(meta.StepTurn, Is.EqualTo(1));
            Assert.That(meta.PlayPreset(SoundData.DefaultPreset("uiClick"), 0), Is.True);
            Assert.That(meta.StepTurn, Is.EqualTo(1), "only steps count steps");
            var bad = SoundData.DefaultPreset("uiClick");
            bad.Timbre = "nothing";
            Assert.That(meta.PlayPreset(bad, 0), Is.False);
            Assert.That(meta.Turn, Is.EqualTo(3), "turn++ happens before the timbre lookup (Audio.js:722-724)");
            Assert.That(meta.PlayPreset(SoundData.DefaultPreset("uiClick"), 0, 0, double.NaN), Is.False, "a voice that throws (NaN volume) is skipped");
            Assert.That(meta.Errors, Is.EqualTo(1));
        }
    }
}
