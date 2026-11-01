// The pure rules: settings (the cases of test/audio/Audio.test.mjs:304-305), ambience ids, miasma lines, the step plan
// and its calls, the alternation and the step pan, the gaps and the hover throttle, and the battle's two-sound budget.
using System.Collections.Generic;
using NUnit.Framework;
using Trial.Audio.Synth;

namespace Trial.Audio.Synth.Tests
{
    public class SoundRulesTests
    {
        [Test]
        public void NormalizeSettingsLikeTheJs()
        {
            Assert.That(SoundRules.NormalizeSettings((IReadOnlyDictionary<string, object>)null), Is.EqualTo(SoundData.SoundSettingsDefaults()));
            var blob = new Dictionary<string, object> { ["master"] = 3.0, ["effects"] = -1.0, ["ambience"] = "x", ["reverb"] = 0.3, ["muted"] = 1.0 };
            Assert.That(SoundRules.NormalizeSettings(blob), Is.EqualTo(new SoundSettings { Master = 1, Effects = 0, Ambience = 0.6, Reverb = 0.3, Muted = false }));
            var partial = new Dictionary<string, object> { ["master"] = 0.6, ["ambience"] = 0.4, ["muted"] = false };
            Assert.That(SoundRules.NormalizeSettings(partial), Is.EqualTo(new SoundSettings { Master = 0.6, Effects = 1, Ambience = 0.4, Reverb = 0.18, Muted = false }));
            // JS Number(): null → 0, "0.5" → 0.5, true → 1
            var odd = new Dictionary<string, object> { ["master"] = null, ["effects"] = "0.5", ["ambience"] = true, ["muted"] = true };
            Assert.That(SoundRules.NormalizeSettings(odd), Is.EqualTo(new SoundSettings { Master = 0, Effects = 0.5, Ambience = 1, Reverb = 0.18, Muted = true }));
            Assert.That(SoundRules.NormalizeSettings(new SoundSettings { Master = double.NaN, Effects = 2, Ambience = -1, Reverb = 0.2, Muted = true }),
                Is.EqualTo(new SoundSettings { Master = 0.8, Effects = 1, Ambience = 0, Reverb = 0.2, Muted = true }));
            Assert.That(SoundData.GameSoundSettingsDefaults().Effects, Is.EqualTo(0.9), "the game starts at effects 0.9 (save.js)");
        }

        [Test]
        public void AmbienceIdsLikeTheJs()
        {
            for (int n = 1; n <= 7; n++)
            {
                Assert.That(SoundRules.TryAmbienceId(n, out string id), Is.True);
                Assert.That(id, Is.EqualTo("layer" + n));
                Assert.That(SoundRules.TryAmbienceId(n.ToString(), out id), Is.True);
                Assert.That(id, Is.EqualTo("layer" + n));
            }
            foreach (string place in SoundData.AmbienceIds)
            {
                Assert.That(SoundRules.TryAmbienceId(place, out string id), Is.True);
                Assert.That(id, Is.EqualTo(place));
            }
            foreach (object silence in new object[] { null, "", "none" })
            {
                Assert.That(SoundRules.TryAmbienceId(silence, out string id), Is.True);
                Assert.That(id, Is.Null);
            }
            foreach (object bad in new object[] { "nowhere", 0, 8, "0", "8", double.NaN, true, "layer8" }) Assert.That(SoundRules.TryAmbienceId(bad, out _), Is.False, bad?.ToString());
            Assert.That(SoundRules.TryAmbienceId("03", out string z), Is.True);
            Assert.That(z, Is.EqualTo("layer3"));
            // the JS quirk, kept: a fractional number gives an id that is not a place
            Assert.That(SoundRules.TryAmbienceId(3.5, out string q), Is.True);
            Assert.That(q, Is.EqualTo("layer3.5"));
            // JS /^\d+$/ has no multiline $: "3\n", " 3" and "+3" are not digit strings (and not places)
            foreach (string notDigits in new[] { "3\n", " 3", "3 ", "+3", "3.0", "３" }) Assert.That(SoundRules.TryAmbienceId(notDigits, out _), Is.False, notDigits);
        }

        [Test]
        public void JsNumberReadsStringsLikeTheJs()
        {
            Assert.That(SoundRules.JsNumber(" 0.5 "), Is.EqualTo(0.5));
            Assert.That(SoundRules.JsNumber(""), Is.EqualTo(0));
            Assert.That(SoundRules.JsNumber("  "), Is.EqualTo(0));
            Assert.That(SoundRules.JsNumber(".5"), Is.EqualTo(0.5));
            Assert.That(SoundRules.JsNumber("1e-1"), Is.EqualTo(0.1));
            Assert.That(SoundRules.JsNumber("0x10"), Is.EqualTo(16));
            Assert.That(SoundRules.JsNumber("0X1f"), Is.EqualTo(31));
            Assert.That(SoundRules.JsNumber("0b101"), Is.EqualTo(5));
            Assert.That(SoundRules.JsNumber("0o17"), Is.EqualTo(15));
            foreach (string nan in new[] { "x", "0x", "0x1g", "0b2", "-0x10", "1,5", "1e", "NaN" }) Assert.That(SoundRules.JsNumber(nan), Is.NaN, nan);
            Assert.That(SoundRules.JsNumber("Infinity"), Is.EqualTo(double.PositiveInfinity));
            // a hex volume clamps like the JS's (Number("0x1") is 1), not to the default
            var blob = new Dictionary<string, object> { ["master"] = "0x0", ["effects"] = "0x1" };
            SoundSettings s = SoundRules.NormalizeSettings(blob);
            Assert.That((s.Master, s.Effects), Is.EqualTo((0.0, 1.0)));
        }

        [TestCase(20, 0)]
        [TestCase(40, 1)]
        [TestCase(60, 2)]
        [TestCase(80, 3)]
        [TestCase(100, 3)]
        [TestCase(0, 0)]
        [TestCase(-20, 0)]
        [TestCase(10, 0)]
        [TestCase(30, 1)]
        [TestCase(double.NaN, 0)]
        [TestCase(double.PositiveInfinity, 0)]
        public void MiasmaLineIndexLikeTheJs(double pct, int index) => Assert.That(SoundRules.MiasmaLineIndex(pct), Is.EqualTo(index));

        [Test]
        public void MiasmaLineCallIsLouderAndLowerPerLine()
        {
            SoundCall c = SoundRules.MiasmaLineCall(60, 280);
            Assert.That(c.Name, Is.EqualTo("miasmaLine"));
            Assert.That(c.DelayMs, Is.EqualTo(280));
            Assert.That(c.Mult, Is.EqualTo(1.2).Within(1e-12));
            Assert.That(c.PitchMul, Is.EqualTo(0.794));
        }

        [Test]
        public void StepPlanKeepsTheTwoSoundsRule()
        {
            Assert.That(SoundRules.StepPlan(), Is.EqualTo(new[] { "nodeStep" }));
            Assert.That(SoundRules.StepPlan(fresh: false), Is.EqualTo(new[] { "nodeReturn" }));
            Assert.That(SoundRules.StepPlan(result: "infoGained"), Is.EqualTo(new[] { "nodeStep", "infoGained" }));
            Assert.That(SoundRules.StepPlan(line: 40, result: "infoGained"), Is.EqualTo(new[] { "nodeStep", "miasmaLine" }), "the warning matters more");
            Assert.That(SoundRules.StepPlan(line: 40, miasmaDeath: true, result: "infoGained"), Is.EqualTo(new[] { "nodeStep", "miasmaDeath" }));
            Assert.That(SoundRules.StepPlan(fresh: false, result: ""), Is.EqualTo(new[] { "nodeReturn" }), "'' || null");
            // every combination names presets that exist, at most two (Audio.test.mjs:205)
            foreach (bool fresh in new[] { true, false })
                foreach (double line in new double[] { 0, 20, 40, 60, 80 })
                    foreach (bool death in new[] { false, true })
                        foreach (string result in new[] { null, "infoGained", "eventResolve", "traceTake" })
                        {
                            string[] plan = SoundRules.StepPlan(fresh, line, death, result);
                            Assert.That(plan.Length, Is.InRange(1, 2));
                            foreach (string n in plan) Assert.That(SoundData.DefaultPreset(n), Is.Not.Null, n);
                        }
        }

        [Test]
        public void StepToCallsPutTheResultAfter280Ms()
        {
            SoundCall[] calls = SoundRules.StepToCalls(line: 40);
            Assert.That(calls.Length, Is.EqualTo(2));
            Assert.That(calls[0].Name, Is.EqualTo("nodeStep"));
            Assert.That(calls[0].DelayMs, Is.EqualTo(0));
            Assert.That(calls[1].Name, Is.EqualTo("miasmaLine"));
            Assert.That(calls[1].DelayMs, Is.EqualTo(280));
            Assert.That(calls[1].Mult, Is.EqualTo(1.1).Within(1e-12));
            Assert.That(calls[1].PitchMul, Is.EqualTo(0.891));
            SoundCall[] rest = SoundRules.StepToCalls(result: "train");
            Assert.That(rest[1].Name, Is.EqualTo("train"));
            Assert.That(rest[1].Mult, Is.Null);
        }

        [Test]
        public void AlternationAndStepPan()
        {
            Assert.That(SoundRules.Wobble(1), Is.EqualTo(0));
            Assert.That(SoundRules.Wobble(2), Is.EqualTo(0.03));
            Assert.That(SoundRules.Wobble(3), Is.EqualTo(-0.03));
            Assert.That(SoundRules.Wobble(4), Is.EqualTo(0));
            Assert.That(SoundRules.StepPan(1), Is.EqualTo(0.12), "the first step is on the right");
            Assert.That(SoundRules.StepPan(2), Is.EqualTo(-0.12));
            Assert.That(SoundRules.Wobbles("paper", false), Is.True);
            Assert.That(SoundRules.Wobbles("step", false), Is.False);
            Assert.That(SoundRules.Wobbles("step", true), Is.True);
            Assert.That(SoundRules.Wobbles("seal", true), Is.False, "uiConfirm's inner wood does not wobble: the preset's timbre decides");
        }

        [Test]
        public void GapsCountFromTheLastAcceptedPlay()
        {
            var gate = new SoundRules.GapGate();
            Assert.That(gate.TryAccept("nodeHover", 60, 0.10), Is.True);
            Assert.That(gate.TryAccept("nodeHover", 60, 0.15), Is.False);
            Assert.That(gate.TryAccept("nodeHover", 60, 0.17), Is.True, "the refused play did not move the clock");
            Assert.That(gate.TryAccept("uiClick", 40, 0.17), Is.True, "per name");
            Assert.That(gate.TryAccept("uiClick", 40, 0.20), Is.False);
            Assert.That(gate.TryAccept("rest", null, 0.20), Is.True);
            Assert.That(gate.TryAccept("rest", null, 0.20), Is.True, "no gap: always");
        }

        [Test]
        public void DesignSystemHoverThrottle()
        {
            Assert.That(SoundRules.DsHoverThrottled(null, 0.01), Is.False);
            Assert.That(SoundRules.DsHoverThrottled(0, 0.01), Is.False, "a last hover at time 0 does not count (JS quirk)");
            Assert.That(SoundRules.DsHoverThrottled(0.02, 0.06), Is.True);
            Assert.That(SoundRules.DsHoverThrottled(0.02, 0.08), Is.False);
        }

        [Test]
        public void BudgetGivesTheEssentialSoundsTheirSlots()
        {
            var b = new SoundRules.SoundBudget();
            b.Begin(2);
            Assert.That(b.TryPlay("guardUp"), Is.False, "both slots are kept for the blow and the body");
            Assert.That(b.TryPlay("hitSlash", true), Is.True);
            Assert.That(b.TryPlay("hitBody", true), Is.True);
            Assert.That(b.TryPlay("hitGuard", true), Is.False, "the budget is spent");

            b.Begin(1);
            Assert.That(b.TryPlay("guardUp"), Is.True, "one free slot");
            Assert.That(b.TryPlay("seal"), Is.False);
            Assert.That(b.TryPlay("hitGuard", true), Is.True);

            b.Begin(5);
            Assert.That(b.EssentialLeft, Is.EqualTo(2));
            b.BeginCardPlay();
            Assert.That(b.Budget, Is.EqualTo(1), "the hand's release sound is the action");
            Assert.That(b.EssentialLeft, Is.EqualTo(1));
            Assert.That(b.TryPlay("stance"), Is.False);
            Assert.That(b.TryPlay(null, true), Is.False);
            Assert.That(b.TryPlay("hitThrust", true), Is.True);

            b.Begin(0);
            Assert.That(b.TryPlay("breath"), Is.True);
            Assert.That(b.TryPlay("reserve"), Is.True);
            Assert.That(b.TryPlay("guardUp"), Is.False);
        }

        [Test]
        public void HitSoundsByStrikeSystem()
        {
            Assert.That(SoundRules.HitSoundOf("Slash"), Is.EqualTo("hitSlash"));
            Assert.That(SoundRules.HitSoundOf("Thrust"), Is.EqualTo("hitThrust"));
            Assert.That(SoundRules.HitSoundOf("Sweep"), Is.EqualTo("hitSlash"));
            Assert.That(SoundRules.HitSoundOf("Strike"), Is.EqualTo("hitBlunt"));
            Assert.That(SoundRules.HitSoundOf("Push"), Is.Null);
        }
    }
}
