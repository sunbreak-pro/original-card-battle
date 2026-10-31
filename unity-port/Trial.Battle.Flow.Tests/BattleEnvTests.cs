// dotnet-only tests of BattleEnv (the engine-free half of scenes/battle/BattleScene.js createBattleEnv): the defaults,
// the layer clamp, the effect switches facade, SafeAudio's fast-forward gap (BattleScene.js:33-47), the logs, the layer
// order, and the small value types the contracts hand around.
using System;
using System.Collections.Generic;
using Depiction;
using NUnit.Framework;

namespace Trial.Battle.Flow.Tests
{
    public class BattleEnvTests
    {
        [SetUp]
        public void SetUp() => Microtasks.Reset();

        private sealed class RecordingAudio : IBattleAudio
        {
            public readonly List<(string Name, float Delay, float? Mult)> Played = new List<(string, float, float?)>();
            public bool Throws;

            public void Play(string name, float delayMs, float? mult)
            {
                Played.Add((name, delayMs, mult));
                if (Throws) throw new InvalidOperationException("broken synth");
            }
        }

        // ---- env ------------------------------------------------------------------------------------

        [Test]
        public void TheDefaults_AreTheJs()
        {
            var env = new BattleEnv();
            Assert.That(env.Settings, Is.Not.Null, "ctx.settings || {}");
            Assert.That(env.Settings.Speed, Is.EqualTo(1.25f));
            Assert.That(env.Settings.ShortFx, Is.False);
            Assert.That(env.LayerNo, Is.EqualTo(1));
            Assert.That(env.NodeKind, Is.EqualTo(BattleNodeKind.Battle));
            Assert.That(env.MiasmaPercent, Is.Null);
            Assert.That(env.Log, Is.SameAs(BattleLog.Console));
            Assert.That(env.Real, Is.SameAs(env.Clock.Real));
            Assert.That(env.Clock.Alive, Is.True);
            foreach (EffectId id in Enum.GetValues(typeof(EffectId))) Assert.That(env.Fx.On(id), Is.True, id.ToString());
        }

        [Test]
        public void TheEnv_KeepsWhatItIsHanded()
        {
            var settings = new BattleSettings { Speed = 1.5f, ShortFx = true };
            var log = BattleLog.Of((m, e) => { });
            var env = new BattleEnv(settings, null, 5, BattleNodeKind.Boss, 42.5f, log);
            Assert.That(env.Settings, Is.SameAs(settings));
            Assert.That(env.LayerNo, Is.EqualTo(5));
            Assert.That(env.NodeKind, Is.EqualTo(BattleNodeKind.Boss));
            Assert.That(env.MiasmaPercent, Is.EqualTo(42.5f));
            Assert.That(env.Log, Is.SameAs(log));
            env.Clock.Step(10f);
            Assert.That(env.Clock.Scale, Is.EqualTo(1.2f * L.ShortScale).Within(1e-2), "the clock reads the env's settings");
        }

        [Test]
        public void TrialBattleSettings_ReadsTheAppsSettingsLive_AndTheSpeedWritesThrough()
        {
            var app = new Trial.Bridge.TrialSettings { Speed = 1.0, ShortFx = false };
            var env = new BattleEnv(new TrialBattleSettings(app));
            env.Clock.Step(10f);
            Assert.That(env.Clock.Scale, Is.EqualTo(0.8f).Within(1e-6));
            app.ShortFx = true; // the settings overlay changed 演出短縮 mid-battle
            env.Clock.Step(10f);
            Assert.That(env.Clock.Scale, Is.EqualTo(0.8f * L.ShortScale).Within(1e-2));
            env.Settings.Speed = BattleClock.SpeedOfStep(BattleSpeedStep.Fast); // player.js cycleSpeed
            Assert.That(app.Speed, Is.EqualTo(1.5));
            Assert.That(app.SpeedStep, Is.EqualTo(BattleSpeedStep.Fast));
        }

        [Test]
        public void TrialBattleSettings_HandsTheStoredDoubleOnUnrounded()
        {
            // A stored speed a float cast would round onto ×1 reads, as in the JS (`speed === 1` is false), as the default.
            var app = new Trial.Bridge.TrialSettings { Speed = 1.0000000001 };
            var settings = new TrialBattleSettings(app);
            Assert.That(settings.Speed, Is.EqualTo(1.0000000001));
            var env = new BattleEnv(settings);
            env.Clock.Step(10f);
            Assert.That(env.Clock.Scale, Is.EqualTo(BattleSpeed.TweenSpeed(BattleSpeed.Default)));
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(4, 4)]
        [TestCase(7, 7)]
        [TestCase(8, 7)]
        [TestCase(-3, 1)]
        public void LayerNo_IsClampedToOneToSeven(int layer, int no)
        {
            Assert.That(BattleEnv.LayerNoOf(layer), Is.EqualTo(no));
            Assert.That(new BattleEnv(layer: layer).LayerNo, Is.EqualTo(no));
        }

        [Test]
        public void TheGeometryRegistry_StartsEmpty_AndHandsBackWhatAModuleFills()
        {
            var env = new BattleEnv();
            Assert.That(env.Geom.Figure?.Invoke(UnitSide.Player, -1), Is.Null);
            Assert.That(env.Geom.OmenBoxes?.Invoke() ?? Array.Empty<StageBox>(), Is.Empty, "JS `geom.omenBoxes ? … : []`");
            Assert.That(env.Geom.JournalEdge?.Invoke(), Is.Null);
            env.Geom.PileMiddle = kind => new StagePoint(kind == PileKind.Draw ? 64f : 150f, 990f);
            Assert.That(env.Geom.PileMiddle(PileKind.Draw).X, Is.EqualTo(64f));
            env.Geom.Figure = (side, unit) => new FigureGeom { X = side == UnitSide.Player ? 400f : 1200f, Unit = unit };
            Assert.That(env.Geom.Figure(UnitSide.Enemy, 2).Unit, Is.EqualTo(2));
        }

        // ---- effect switches -------------------------------------------------------------------------

        [Test]
        public void Fx_IsAFacadeOverTheSwitches()
        {
            var env = new BattleEnv();
            EffectSwitches s = env.Fx.Switches;
            Assert.That(env.Fx.Ms(EffectId.CardDraw), Is.EqualTo(EffectCatalog.Of(EffectId.CardDraw).Ms));
            Assert.That(env.Fx.Stagger(EffectId.CardDraw), Is.EqualTo(EffectCatalog.Of(EffectId.CardDraw).StaggerMs));
            Assert.That(env.Fx.Wait(EffectId.CardDraw, 3), Is.EqualTo(s.WaitMs(EffectId.CardDraw, 3)));
            Assert.That(env.Fx.Wait(EffectId.HitStop), Is.EqualTo(s.WaitMs(EffectId.HitStop, 1)), "count defaults to 1");
            s.Set(EffectId.CardDraw, false);
            Assert.That(env.Fx.On(EffectId.CardDraw), Is.False);
            Assert.That(env.Fx.Ms(EffectId.CardDraw), Is.EqualTo(0f));
            Assert.That(env.Fx.Stagger(EffectId.CardDraw), Is.EqualTo(0f));
            Assert.That(env.Fx.Wait(EffectId.CardDraw, 3), Is.EqualTo(0f));
        }

        // ---- sound ----------------------------------------------------------------------------------

        [Test]
        public void SafeAudio_WithoutFastForward_PlaysEveryRepeat()
        {
            var audio = new RecordingAudio();
            var env = new BattleEnv(audio: audio);
            env.Audio.Play("draw");
            env.Audio.Play("draw");
            env.Audio.Play("hitSlash", 120f, 0.5f);
            Assert.That(audio.Played, Is.EqualTo(new[] { ("draw", 0f, (float?)null), ("draw", 0f, (float?)null), ("hitSlash", 120f, (float?)0.5f) }));
        }

        [Test]
        public void SafeAudio_UnderFastForward_DropsARepeatWithin90RealMs()
        {
            var audio = new RecordingAudio();
            var env = new BattleEnv(audio: audio);
            env.Clock.SetFastForward(true);
            env.Audio.Play("discard");
            env.Audio.Play("draw"); // another name is its own
            env.Clock.Step(50f);
            env.Audio.Play("discard"); // 50 after: dropped
            env.Clock.Step(39f);
            env.Audio.Play("discard"); // 89 after the last that played: dropped
            env.Clock.Step(1f);
            env.Audio.Play("discard"); // 90: plays
            Assert.That(audio.Played.ConvertAll(p => p.Name), Is.EqualTo(new[] { "discard", "draw", "discard" }));
        }

        [Test]
        public void SafeAudio_CountsTheDelay_AndBothSidesOfTheLast()
        {
            var audio = new RecordingAudio();
            var env = new BattleEnv(audio: audio);
            env.Clock.SetFastForward(true);
            env.Audio.Play("pip", 0f);
            env.Audio.Play("pip", 100f); // starts 100 later: plays
            env.Audio.Play("pip", 20f); // 80 before the last one's start: dropped (|at − prev|)
            Assert.That(audio.Played.ConvertAll(p => p.Delay), Is.EqualTo(new[] { 0f, 100f }));
        }

        [Test]
        public void SafeAudio_RemembersPlaysMadeWithoutFastForward()
        {
            // BattleScene.js:43: `else if (clocks) last.set(…)` — a sound played just before fast-forward came on counts.
            var audio = new RecordingAudio();
            var env = new BattleEnv(audio: audio);
            env.Audio.Play("heartbeat");
            env.Clock.Step(40f);
            env.Clock.SetFastForward(true);
            env.Audio.Play("heartbeat");
            Assert.That(audio.Played.Count, Is.EqualTo(1));
        }

        [Test]
        public void SafeAudio_NeverThrows_AndSkipsNoSoundOrNoName()
        {
            var silent = new BattleEnv();
            Assert.DoesNotThrow(() => silent.Audio.Play("draw"));

            var audio = new RecordingAudio { Throws = true };
            var env = new BattleEnv(audio: audio);
            Assert.DoesNotThrow(() => env.Audio.Play("hitBody"));
            Assert.That(audio.Played.Count, Is.EqualTo(1));
            env.Audio.Play("");
            env.Audio.Play(null);
            Assert.That(audio.Played.Count, Is.EqualTo(1));
        }

        [Test]
        public void SafeAudio_ReadsANaNDelayAsZero()
        {
            var audio = new RecordingAudio();
            var env = new BattleEnv(audio: audio);
            env.Audio.Play("uiClick", float.NaN);
            Assert.That(audio.Played[0].Delay, Is.EqualTo(0f));
        }

        [Test]
        public void SafeAudio_WithoutClocks_PlaysEverything()
        {
            var audio = new RecordingAudio();
            var safe = new SafeAudio(audio, null);
            safe.Play("draw");
            safe.Play("draw");
            Assert.That(audio.Played.Count, Is.EqualTo(2));
        }

        [Test]
        public void BattleAudio_Of_WrapsAFunction()
        {
            Assert.That(BattleAudio.Of(null), Is.Null);
            var seen = new List<string>();
            IBattleAudio a = BattleAudio.Of((n, d, m) => seen.Add(n + "@" + d + "x" + (m.HasValue ? m.Value.ToString() : "-")));
            a.Play("foeTurn", 900f, null);
            Assert.That(seen, Is.EqualTo(new[] { "foeTurn@900x-" }));
        }

        // ---- log ------------------------------------------------------------------------------------

        [Test]
        public void BattleLog_Of_PassesTheMessageAndTheError()
        {
            var seen = new List<(string, Exception)>();
            IBattleLog log = BattleLog.Of((m, e) => seen.Add((m, e)));
            var err = new InvalidOperationException("source");
            log.Error("[battle] the source failed; the battle stops here:", err);
            log.Error("[battle] event 3 x: EffectPlan lists waits the screen did not hold: HitStop x1");
            Assert.That(seen.Count, Is.EqualTo(2));
            Assert.That(seen[0].Item2, Is.SameAs(err));
            Assert.That(seen[1].Item2, Is.Null);
            Assert.That(BattleLog.Of(null), Is.SameAs(BattleLog.Console));
            Assert.DoesNotThrow(() => BattleLog.Console.Error(null, null));
        }

        // ---- layers ---------------------------------------------------------------------------------

        [Test]
        public void TheLayers_StandInTheJsOrder_WithTheirCssZ()
        {
            // BattleScene.js:26 LAYERS and battle.css:28-38.
            string[] js = { "backdrop", "field", "fx", "panels", "marks", "hud", "shade", "hand", "held", "float", "over" };
            int[] z = { 0, 10, 15, 20, 30, 40, 50, 60, 70, 80, 90 };
            Assert.That(BattleLayers.Order.Count, Is.EqualTo(js.Length));
            Assert.That(Enum.GetValues(typeof(BattleLayer)).Length, Is.EqualTo(js.Length));
            for (int i = 0; i < js.Length; i++)
            {
                Assert.That(BattleLayers.Order[i].ToString().ToLowerInvariant(), Is.EqualTo(js[i]));
                Assert.That(BattleLayers.CssZ(BattleLayers.Order[i]), Is.EqualTo(z[i]));
            }
        }

        // ---- value types ------------------------------------------------------------------------------

        [Test]
        public void Boxes_KnowTheirEdges()
        {
            var b = new StageBox(10f, 20f, 30f, 40f);
            Assert.That(b.Right, Is.EqualTo(40f));
            Assert.That(b.Bottom, Is.EqualTo(60f));
            var p = new PanelBox(1f, 2f, 3f, 4f, 5f);
            Assert.That(p.Box, Is.EqualTo(new StageBox(1f, 2f, 3f, 4f)));
            Assert.That(p.ChipsW, Is.EqualTo(5f));
            Assert.That(new FreeStepBox(1f, 2f, 3f, 4f, -1).Box, Is.EqualTo(new StageBox(1f, 2f, 3f, 4f)));
        }

        [Test]
        public void FreeStepWays_ReadAsTheJsObject()
        {
            // player.js:1767 `ways = { [-1]: …, [1]: … }`; `freeStep.ways[dir]` for any other dir is undefined.
            var ways = new FreeStepWays(back: true, forward: false);
            Assert.That(ways[-1], Is.True);
            Assert.That(ways[1], Is.False);
            Assert.That(ways[0], Is.False);
            Assert.That(ways[2], Is.False);
            Assert.That(ways[-2], Is.False);
            Assert.That(new FreeStepWays(false, true)[1], Is.True);
        }

        [Test]
        public void TheOptionBags_HoldTheJsDefaults()
        {
            Assert.That(new MarkOpts().CrestLight, Is.True, "o.crestLight !== false");
            Assert.That(new MarkOpts().Unit, Is.EqualTo(-1));
            Assert.That(new StrikeOpts().Unit, Is.EqualTo(-1));
            Assert.That(new StrikeOpts().Source, Is.EqualTo(-1));
            Assert.That(new FloatOpts().Rise, Is.Null, "the fx's own 43");
            Assert.That(new FloatOpts().Side, Is.EqualTo(""));
            Assert.That(new HudOpts().ChainIndex, Is.EqualTo(1));
            Assert.That(new HudOpts().ChainTotal, Is.EqualTo(1));
            Assert.That(new FollowArgs().Units, Is.Empty);
            Assert.That(new ResultInfo().Lines, Is.Empty);
            Assert.That(new ReleaseOpts().TraitWord, Is.EqualTo(""));
            Assert.That(new FigureGeom().Unit, Is.EqualTo(-1));
            Assert.That(new BattlePointer().IsMouse, Is.True);
        }
    }
}
