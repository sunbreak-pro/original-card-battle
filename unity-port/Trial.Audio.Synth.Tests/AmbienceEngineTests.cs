// The ambience's rules, driven like the JS tests drive createAudio over the recording mock (test/audio/Audio.test.mjs):
// the context's time is set by hand (NowOverride) and the scheduler and the timers are run by hand, nothing is rendered.
// Ports: "imports … and stays silent" (the ambience ids and the surface), "every event and every ambience runs against a
// strict WebAudio mock" (its ambience part), "is deterministic" (its ambience part), "crossfade: the old place fades
// while the new one rises; footsteps follow the place", and the ambience half of "settings" (the duck and the volume).
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Trial.Audio.Synth;

namespace Trial.Audio.Synth.Tests
{
    public class AmbienceEngineTests
    {
        private static AmbienceEngine Engine(ICallLog log = null)
        {
            var e = new AmbienceEngine(48000, 0.6, 1, log) { NowOverride = 0 };
            return e;
        }

        /// <summary>js: advance(audio, ctx, seconds, step): the clock moves in 0.25 s steps and the scheduler runs at each; the timers run too.</summary>
        private static void Advance(AmbienceEngine e, double seconds, double step = 0.25)
        {
            double start = e.NowOverride ?? 0;
            int n = (int)Math.Round(seconds / step);
            for (int i = 1; i <= n; i++)
            {
                e.NowOverride = start + i * step;
                e.Flush();
                e.Tick();
                e.RunTimers();
            }
        }

        /// <summary>Lets the engine's time pass without scheduler ticks of its own (js: await sleep(ms) with the context clock moving).</summary>
        private static void Wait(AmbienceEngine e, double ms)
        {
            e.NowOverride = (e.NowOverride ?? 0) + ms / 1000;
            e.Flush();
            e.RunTimers();
        }

        [Test]
        public void ThePlaceAndItsGroundAreKnownBeforeUnlock()
        {
            // js: Audio.test.mjs:148-154
            var e = Engine();
            Assert.That(e.SetAmbience(1), Is.True);
            Assert.That(e.Ambience, Is.EqualTo("layer1"));
            Assert.That(e.Surface, Is.EqualTo("litter"));
            Assert.That(e.SetAmbience("nowhere"), Is.False);
            Assert.That(e.Ambience, Is.EqualTo("layer1"), "an unknown place changes nothing");
            Assert.That(e.SetAmbience(null), Is.True);
            Assert.That(e.Ambience, Is.Null);
            Assert.That(e.Surface, Is.EqualTo("litter"), "silence keeps the last ground");
            e.Flush();
            Assert.That(e.Layers, Is.Empty, "nothing plays before unlock");
            Assert.That(e.SetAmbience("3"), Is.True);
            Assert.That(e.Ambience, Is.EqualTo("layer3"));
            Assert.That(e.SetAmbience(""), Is.True);
            Assert.That(e.SetAmbience("none"), Is.True);
            Assert.That(e.Ambience, Is.Null);
            Assert.That(e.SetAmbience(8), Is.False);
        }

        [Test]
        public void AFractionalLayerThrowsAsTheJsDoes()
        {
            // ambienceId(3.5) is "layer3.5", which no place has: AMBIENCE_DEFS[nid].surface throws in the JS.
            var e = Engine();
            Assert.Throws<KeyNotFoundException>(() => e.SetAmbience(3.5));
        }

        [Test]
        public void ThePendingPlaceStartsOnUnlockWithALongFade()
        {
            // js: Audio.test.mjs:235-239, Audio.js:780
            var log = new RecordingCallLog();
            var e = Engine(log);
            e.SetAmbience("title");
            e.Flush();
            Assert.That(e.Layers, Is.Empty);
            log.BeginSegment(e.Ctx);
            Assert.That(e.Unlock(), Is.True);
            e.Flush();
            Assert.That(e.Layers.Select(L => L.Id), Is.EqualTo(new[] { "title" }));
            Assert.That(log.Entries.Any(x => x.Length == 5 && (string)x[0] == "Gain+0" && (string)x[2] == "lin" && (double)x[3] == 0.5 && (double)x[4] == 2.5), Is.True,
                "the fade-in runs 2500 ms to the place's level");
            Assert.That(e.Unlock(), Is.True);
            e.Flush();
            Assert.That(e.Layers.Count, Is.EqualTo(1), "a second unlock does not start it again");
        }

        [Test]
        public void EveryPlaceRunsAMinuteAndReleasesThePreviousOne()
        {
            // js: Audio.test.mjs:231-276 (the ambience part)
            var log = new RecordingCallLog();
            var e = Engine(log);
            e.SetAmbience("title");
            e.Unlock();
            e.Flush();
            Assert.That(e.Layers.Count, Is.EqualTo(1), "pending ambience starts on unlock");
            foreach (string id in SoundData.AmbienceIds)
            {
                Assert.That(e.SetAmbience(id, 20), Is.True);
                e.Flush();
                Advance(e, 60);
                AmbienceLayer[] live = e.Layers.Where(L => !L.Stopping).ToArray();
                Assert.That(live.Length, Is.EqualTo(1), id);
                Assert.That(live[0].Id, Is.EqualTo(id));
                Assert.That(live[0].Nodes.Count, Is.GreaterThan(0), id + ": beds and drones");
                Assert.That(live[0].Sparse.Count > 0 && live[0].Sparse.All(s => s.K > 0), Is.True, id + ": sparse events fired " + string.Join(",", live[0].Sparse.Select(s => s.K)));
                Wait(e, 200);
                Assert.That(e.Layers.Count, Is.EqualTo(1), id + ": the previous layer was released after its fade");
            }
            Assert.That(e.Errors, Is.EqualTo(0), "no voice threw");
            // every started source is stopped once the ambience is cleared
            e.SetAmbience(null, 20);
            e.Flush();
            Wait(e, 220);
            Assert.That(e.Layers, Is.Empty);
            var started = new HashSet<string>();
            var stopped = new HashSet<string>();
            foreach (object[] x in log.Entries)
            {
                if ((string)x[1] == "start") started.Add((string)x[0]);
                if ((string)x[1] == "stop") stopped.Add((string)x[0]);
            }
            Assert.That(started.Except(stopped), Is.Empty, "sources left running");
            // the scheduler never produced NaN times
            foreach (object[] x in log.Entries)
                foreach (object v in x)
                    if (v is double d) Assert.That(double.IsNaN(d) || double.IsInfinity(d), Is.False, Golden.Show(x));
        }

        [Test]
        public void TheSameCallsGiveTheSameGraphCalls()
        {
            // js: Audio.test.mjs:278-301 (the ambience part)
            List<object[]> Run()
            {
                var log = new RecordingCallLog();
                var e = Engine(log);
                e.Unlock();
                e.SetAmbience(3, 500);
                e.Flush();
                Advance(e, 45);
                e.SetAmbience(6, 500);
                e.Flush();
                Advance(e, 45);
                return log.Entries;
            }
            List<object[]> a = Run(), b = Run();
            Assert.That(a.Count, Is.GreaterThan(300));
            Assert.That(b.Count, Is.EqualTo(a.Count));
            for (int i = 0; i < a.Count; i++) Assert.That(Golden.Show(b[i]), Is.EqualTo(Golden.Show(a[i])), "entry " + i);
        }

        [Test]
        public void TheOldPlaceFadesWhileTheNewOneRises()
        {
            // js: Audio.test.mjs:340-356
            var e = Engine();
            e.Unlock();
            e.SetAmbience("layer1", 30);
            e.Flush();
            Assert.That(e.Surface, Is.EqualTo("litter"));
            Assert.That(e.SetAmbience("layer1"), Is.True);
            e.Flush();
            Assert.That(e.Layers.Count, Is.EqualTo(1), "same place: no restart");
            e.SetAmbience("layer2", 30);
            e.Flush();
            Assert.That(e.Surface, Is.EqualTo("sand"));
            Assert.That(e.Layers.Select(L => (L.Id, L.Stopping)), Is.EqualTo(new[] { ("layer1", true), ("layer2", false) }));
            Wait(e, 150);
            Assert.That(e.Layers.Count, Is.EqualTo(2), "the old layer lives until fadeMs + 150");
            Wait(e, 100);
            Assert.That(e.Layers.Select(L => L.Id), Is.EqualTo(new[] { "layer2" }));
            e.DuckAmbience(0.4, 300);
            e.Destroy();
            e.Flush();
            Assert.That(e.Layers, Is.Empty, "destroyed: every layer killed");
            Assert.That(e.Unlock(), Is.False, "destroyed: no unlock");
        }

        [Test]
        public void TheStopFadeStartsFromTheValueReachedAndRunsToSilence()
        {
            // js: Audio.js:670-677: cancel(now), setValueAtTime(cur, now), linearRamp(0, now + max(0.05, fade/1000))
            var log = new RecordingCallLog();
            var e = Engine(log);
            e.Unlock();
            e.SetAmbience("hall", 1000);
            e.Flush();
            e.NowOverride = 0.5;
            log.BeginSegment(e.Ctx);
            e.SetAmbience(null, 2000);
            e.Flush();
            string[] shown = log.Entries.Select(Golden.Show).ToArray();
            // The engine was never rendered, so p.value is still the value the last .value write left (0.0001).
            Assert.That(shown, Is.EqualTo(new[]
            {
                "[\"Gain#8\",\"gain\",\"cancel\",0.5]",
                "[\"Gain#8\",\"gain\",\"set\",0.0001,0.5]",
                "[\"Gain#8\",\"gain\",\"lin\",0,2.5]",
            }));
            Assert.That(e.Layers[0].KillAt, Is.EqualTo(0.5 + 2.15).Within(1e-12));
        }

        [Test]
        public void ADuckRampsWithAThirdOfItsFadeAsTimeConstant()
        {
            // js: Audio.js:811-814
            var log = new RecordingCallLog();
            var e = Engine(log);
            e.Unlock();
            e.Flush();
            e.NowOverride = 2;
            log.BeginSegment(e.Ctx);
            e.DuckAmbience(SoundData.BattleDuck);
            e.DuckAmbience(1, 1500);
            e.DuckAmbience(-3, 0);
            e.DuckAmbience(double.NaN, 30);
            Assert.That(e.Duck, Is.EqualTo(1), "NaN reads as the default 1");
            e.Flush();
            Assert.That(log.Entries.Select(Golden.Show), Is.EqualTo(new[]
            {
                "[\"ambDuck\",\"gain\",\"cancel\",2]", "[\"ambDuck\",\"gain\",\"target\",0.4,2,0.2]",
                "[\"ambDuck\",\"gain\",\"cancel\",2]", "[\"ambDuck\",\"gain\",\"target\",1,2,0.5]",
                "[\"ambDuck\",\"gain\",\"cancel\",2]", "[\"ambDuck\",\"gain\",\"target\",0,2,0.01]",
                "[\"ambDuck\",\"gain\",\"cancel\",2]", "[\"ambDuck\",\"gain\",\"target\",1,2,0.01]",
            }));
        }

        [Test]
        public void TheVolumeRampsWithA30MsTimeConstant()
        {
            // js: Audio.js:698, 817
            var log = new RecordingCallLog();
            var e = Engine(log);
            e.Unlock();
            e.Flush();
            e.NowOverride = 1;
            log.BeginSegment(e.Ctx);
            e.SetVolume(0.25);
            Assert.That(e.Volume, Is.EqualTo(0.25));
            e.Flush();
            Assert.That(log.Entries.Select(Golden.Show), Is.EqualTo(new[] { "[\"ambVolume\",\"gain\",\"cancel\",1]", "[\"ambVolume\",\"gain\",\"target\",0.25,1,0.03]" }));
        }

        [Test]
        public void BeforeUnlockTheDuckAndTheVolumeAreSetAtOnce()
        {
            // js: Audio.js:811-817 touch no param before unlock (no graph yet); ensure() then builds the buses with the values
            // in force (Audio.js:295-296: ambDuck = G(duck), amb = G(state.ambience)), so nothing glides.
            var log = new RecordingCallLog();
            var e = Engine(log);
            e.NowOverride = 3;
            log.BeginSegment(e.Ctx);
            e.DuckAmbience(SoundData.BattleDuck);
            e.SetVolume(0.25);
            e.Flush();
            Assert.That(log.Entries.Select(Golden.Show), Is.EqualTo(new[]
            {
                "[\"ambDuck\",\"gain\",\"cancel\",3]", "[\"ambDuck\",\"gain\",\"set\",0.4,3]",
                "[\"ambVolume\",\"gain\",\"cancel\",3]", "[\"ambVolume\",\"gain\",\"set\",0.25,3]",
            }));
            // Posted in the caller's order: a duck asked for after unlock glides again.
            log.BeginSegment(e.Ctx);
            e.Unlock();
            e.DuckAmbience(1);
            e.Flush();
            Assert.That(log.Entries.Select(Golden.Show), Is.EqualTo(new[] { "[\"ambDuck\",\"gain\",\"cancel\",3]", "[\"ambDuck\",\"gain\",\"target\",1,3,0.2]" }));
        }

        [Test]
        public void SilenceThenTheSamePlaceStartsItAgain()
        {
            var e = Engine();
            e.Unlock();
            e.SetAmbience("hall", 20);
            e.SetAmbience(null, 20);
            e.SetAmbience("hall", 20);
            e.Flush();
            Assert.That(e.Layers.Select(L => (L.Id, L.Stopping)), Is.EqualTo(new[] { ("hall", true), ("hall", false) }));
        }

        [Test]
        public void AfterALongSuspendTheSparseVoicesDoNotBurst()
        {
            // js: Audio.js:685: a voice due more than 1 s ago is moved to now + 0.05
            var e = Engine();
            e.Unlock();
            e.SetAmbience("layer1", 20);
            e.Flush();
            int[] before = e.Layers[0].Sparse.Select(s => s.K).ToArray();
            e.NowOverride = 120;
            e.Tick();
            int[] after = e.Layers[0].Sparse.Select(s => s.K).ToArray();
            for (int i = 0; i < before.Length; i++) Assert.That(after[i] - before[i], Is.EqualTo(1), "one firing for voice " + i);
            Assert.That(e.Layers[0].Sparse.All(s => s.Next > 120 + 0.05), Is.True);
        }

        [Test]
        public void ABuilderExistsForEveryPlaceAndNoOther()
        {
            foreach (string id in SoundData.AmbienceIds) Assert.That(AmbienceBuilders.Has(id), Is.True, id);
            Assert.That(AmbienceBuilders.Has("layer8"), Is.False);
            Assert.That(AmbienceBuilders.Has(null), Is.False);
        }

        [Test]
        public void ACloneCarriesTheState()
        {
            var e = new AmbienceEngine(48000, 0.3, 1);
            e.Unlock();
            e.SetAmbience("layer5", 20);
            e.DuckAmbience(0.4);
            e.SetFrozen(true);
            AmbienceEngine c = e.CloneAt(44100);
            Assert.That(e.Destroyed, Is.True);
            Assert.That(c.SampleRate, Is.EqualTo(44100));
            Assert.That((c.Ambience, c.Surface, c.Volume, c.Duck, c.Unlocked, c.Frozen), Is.EqualTo(("layer5", "salt", 0.3, 0.4, true, true)));
            c.Flush();
            Assert.That(c.Layers.Select(L => L.Id), Is.EqualTo(new[] { "layer5" }));
        }
    }
}
