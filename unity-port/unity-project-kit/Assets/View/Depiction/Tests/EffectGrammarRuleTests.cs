// #57: the grammar's rule on its own (battle_ui_ux_v2 §5.3): the words, their priority, the order
// the faces resolve in, and one look per system. The cards of the core are held in the Bridge's
// EffectGrammarTests.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class EffectGrammarRuleTests
    {
        [TestCase("突き打ち", FxSystem.Thrust)]   // 突 before 打
        [TestCase("打ち払い", FxSystem.Strike)]   // 打 before 払
        [TestCase("払い斬り", FxSystem.Sweep)]    // 払 before 斬
        [TestCase("袈裟斬り", FxSystem.Slash)]    // 斬 is the default
        [TestCase("一閃", FxSystem.Slash)]
        [TestCase("貫き", FxSystem.Thrust)]
        [TestCase("投げ刃", FxSystem.Thrust)]
        [TestCase("体当たり", FxSystem.Strike)]
        [TestCase("石礫", FxSystem.Strike)]
        [TestCase("圧殺", FxSystem.Strike)]
        [TestCase("横薙ぎ", FxSystem.Sweep)]
        [TestCase("牽制", FxSystem.Sweep)]
        [TestCase("", FxSystem.Slash)]
        [TestCase(null, FxSystem.Slash)]
        public void TheBlow_ReadsTheNameInTheOrder_Thrust_Strike_Sweep_Slash(string name, FxSystem expected)
        {
            Assert.That(EffectGrammar.BlowOf(name, pushes: false), Is.EqualTo(expected));
        }

        [Test]
        public void APushOrPull_IsStrike_BeforeAnyWordIsRead()
        {
            Assert.That(EffectGrammar.BlowOf("石突きの押し込み", pushes: true), Is.EqualTo(FxSystem.Strike));
            Assert.That(EffectGrammar.BlowOf("払い寄せ", pushes: true), Is.EqualTo(FxSystem.Strike));
        }

        [Test]
        public void TheFaces_PlayInResolveOrder_AttackGuardSkillStance()
        {
            var shieldBash = new FxFaces { Name = "盾打ち", Attacks = true, Guards = true };
            Assert.That(EffectGrammar.Of(shieldBash), Is.EqualTo(new[] { FxSystem.Strike, FxSystem.Shield }));

            var rend = new FxFaces { Name = "裂き斬り", Attacks = true, PutsStatus = true };
            Assert.That(EffectGrammar.Of(rend), Is.EqualTo(new[] { FxSystem.Slash, FxSystem.Spirit }));

            var observe = new FxFaces { Name = "観察", Guards = true, Breathes = true };
            Assert.That(EffectGrammar.Of(observe), Is.EqualTo(new[] { FxSystem.Shield, FxSystem.Breath }));

            var stance = new FxFaces { Name = "水の構え", Stance = true };
            Assert.That(EffectGrammar.Of(stance), Is.EqualTo(new[] { FxSystem.Form }));

            var move = new FxFaces { Name = "足運び" };
            Assert.That(EffectGrammar.Of(move), Is.Empty, "§5.3: a move has no system");
        }

        [Test]
        public void AStatus_IsSpirit_EvenWhenTheSkillAlsoHealsOrDraws()
        {
            var both = new FxFaces { Name = "息継ぎ", PutsStatus = true, Breathes = true };
            Assert.That(EffectGrammar.Of(both), Is.EqualTo(new[] { FxSystem.Spirit }));
            var breath = new FxFaces { Name = "応急処置", Breathes = true };
            Assert.That(EffectGrammar.Of(breath), Is.EqualTo(new[] { FxSystem.Breath }));
        }

        [Test]
        public void EverySystem_HasItsOwnForm_LookAndGlyph()
        {
            Assert.That(EffectGrammar.Systems, Is.EquivalentTo((FxSystem[])Enum.GetValues(typeof(FxSystem))));
            Assert.That(EffectGrammar.Systems.Select(EffectGrammar.FormOf).Distinct().Count(), Is.EqualTo(8));
            Assert.That(EffectGrammar.Systems.Select(EffectGrammar.LookId).Distinct().Count(), Is.EqualTo(8));
            Assert.That(EffectGrammar.Systems.Select(EffectGrammar.Glyph), Is.EqualTo(new[] { "斬", "突", "払", "打", "盾", "型", "気", "息" }));
        }

        [Test]
        public void TheLook_IsReadOffTheSystemAlone()
        {
            // Two calls for the same system give the same id: nothing about the card goes in.
            foreach (FxSystem system in EffectGrammar.Systems)
            {
                Assert.That(EffectGrammar.LookId(system), Is.EqualTo(EffectGrammar.LookId(system)));
            }
            Assert.That(EffectGrammar.LookId(FxSystem.Shield), Is.EqualTo("fx.ring"));
        }

        [Test]
        public void TheViewDraws_TheShapeOfShieldFormAndSpirit_AndBreathIsThePipsWithTheChestOnlyOnAHeal()
        {
            // §5.3: 盾 / 型 / 気 draw their shape beside the beat; a blow draws its own inside the attack.
            Assert.That(EffectGrammar.MarkOf(new Cue { Kind = CueKind.GuardGain, Amount = 3, Fx = FxSystem.Shield }), Is.EqualTo(FxMark.Shape));
            Assert.That(EffectGrammar.MarkOf(new Cue { Kind = CueKind.TraitFire, Fx = FxSystem.Form }), Is.EqualTo(FxMark.Shape));
            Assert.That(EffectGrammar.MarkOf(new Cue { Kind = CueKind.StatusChange, Amount = 1, Fx = FxSystem.Spirit }), Is.EqualTo(FxMark.Shape));
            Assert.That(EffectGrammar.MarkOf(new Cue { Kind = CueKind.Slash, Amount = 9, Fx = FxSystem.Thrust }), Is.EqualTo(FxMark.None));
            // 息's form is the pips lighting (the stamina beat itself) and the draw is its own beat; a heal adds the chest's 400.
            Assert.That(EffectGrammar.MarkOf(new Cue { Kind = CueKind.StaminaChange, Amount = 1, Fx = FxSystem.Breath }), Is.EqualTo(FxMark.None));
            Assert.That(EffectGrammar.MarkOf(new Cue { Kind = CueKind.DrawHand, Amount = 1, Fx = FxSystem.Breath }), Is.EqualTo(FxMark.None));
            Assert.That(EffectGrammar.MarkOf(new Cue { Kind = CueKind.HpChange, Amount = 4, Fx = FxSystem.Breath }), Is.EqualTo(FxMark.ChestHeave));
            Assert.That(EffectGrammar.MarkOf(new Cue { Kind = CueKind.HpChange, Amount = 4 }), Is.EqualTo(FxMark.None), "no system, nothing beside the beat");
            Assert.That(EffectGrammar.FormOf(FxSystem.Breath), Is.EqualTo(FxForm.Pips));
            Assert.That(EffectCatalog.Of(EffectId.BreathHeave).Ms, Is.EqualTo(400f), "§5.3 息: 回復は + 400");
        }

        [Test]
        public void TheBlowSystems_MapToTheirShapeAndBack()
        {
            var blows = new List<FxSystem>();
            foreach (FxSystem system in EffectGrammar.Systems)
            {
                if (!EffectGrammar.IsBlow(system)) continue;
                blows.Add(system);
                Assert.That(EffectGrammar.FromStrike(EffectGrammar.ToStrike(system)), Is.EqualTo(system));
            }
            Assert.That(blows, Is.EqualTo(new[] { FxSystem.Slash, FxSystem.Thrust, FxSystem.Sweep, FxSystem.Strike }));
            Assert.That(EffectGrammar.FromStrike(StrikeSystem.Step), Is.Null, "a step has no system");
        }

        [Test]
        public void StrikeSystems_ReadsTheSameWordsAsTheGrammar()
        {
            foreach (string name in new[] { "突き", "峰打ち", "横薙ぎ", "袈裟斬り", "伸び突き", "打ち下ろし" })
            {
                Assert.That(StrikeSystems.Of(name, true, false, false, false),
                    Is.EqualTo(EffectGrammar.ToStrike(EffectGrammar.BlowOf(name, false))), name);
            }
        }

        // §5.3 / §5.9 順 1 (#57): the lengths the grammar's own shapes play for, beside the beats. A blow's
        // shape is the attack beat's StrikeShape, one 120 ms for all four blow systems (#76, held in
        // EffectTests): the per-system lengths in §5.3's table are the design to come back to with the
        // four strengths (#30).
        [TestCase(EffectId.GrammarMark, 300f)]
        [TestCase(EffectId.TraitAccent, 150f)]
        [TestCase(EffectId.BreathHeave, 400f)]
        public void TheGrammarsShapes_PlayForTheLengthsTheCanonWrites(EffectId id, float ms)
        {
            EffectSpec spec = EffectCatalog.Of(id);
            Assert.That(spec.Ms, Is.EqualTo(ms));
            Assert.That(spec.StaggerMs, Is.EqualTo(0f));
            Assert.That(spec.Blocking, Is.False);
        }

        [Test]
        public void TheGrammarsEffects_RunBesideTheBeats_SoNoEventWaitsLonger()
        {
            Assert.That(EffectCatalog.Of(EffectId.GrammarMark).Blocking, Is.False);
            Assert.That(EffectCatalog.Of(EffectId.TraitAccent).Blocking, Is.False);
            Assert.That(EffectCatalog.Of(EffectId.BreathHeave).Blocking, Is.False);
            var ev = new DepictionEvent { Kind = DepictionEventKind.PlayCard };
            ev.Cues.Add(new Cue { Kind = CueKind.Slash, Source = UnitSide.Player, Target = UnitSide.Enemy, Amount = 9, HpAfter = 40, Intensity = EffectStrength.Uniform });
            ev.Cues.Add(new Cue { Kind = CueKind.GuardGain, Target = UnitSide.Player, Amount = 3, GuardAfter = 3, Intensity = EffectStrength.Uniform });
            float bare = EffectPlan.BlockingMs(ev, EffectSwitches.AllOn());
            ev.Cues[0].Fx = FxSystem.Slash;
            ev.Cues[0].TraitAccent = true;
            ev.Cues[1].Fx = FxSystem.Shield;
            Assert.That(EffectPlan.BlockingMs(ev, EffectSwitches.AllOn()), Is.EqualTo(bare));
            Assert.That(ev.Cues[0].Intensity, Is.EqualTo(EffectStrength.Uniform), "the trait's cue leaves the tier alone");
        }
    }
}
