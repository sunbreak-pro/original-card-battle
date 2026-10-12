// #57: the samples the Editor's capture (Tools > Depiction > Capture Effect Grammar Samples) plays.
// Each one must be a card a run starts with, play the beat it is named after, and leave the effect the
// capture waits for on the screen's plan. Runs under `dotnet test` and in Unity's EditMode runner.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class GrammarSamplesTests
    {
        [Test]
        public void ThereIsOneSamplePerSystem_InTheTablesOrder_ThenTheTraitCue()
        {
            IReadOnlyList<GrammarSample> samples = GrammarSamples.All;
            Assert.That(samples.Count, Is.EqualTo(EffectGrammar.Systems.Length + 1));
            for (int i = 0; i < EffectGrammar.Systems.Length; i++)
            {
                Assert.That(samples[i].System, Is.EqualTo(EffectGrammar.Systems[i]));
                Assert.That(samples[i].TraitAccent, Is.False, samples[i].Label);
            }
            GrammarSample accent = samples[samples.Count - 1];
            Assert.That(accent.TraitAccent, Is.True);
            Assert.That(EffectGrammar.IsBlow(accent.System), Is.True, "the trait cue rides a blow");
            Assert.That(samples.Select(s => s.Index), Is.EqualTo(Enumerable.Range(1, samples.Count)));
            Assert.That(samples.Select(s => s.FileName).Distinct().Count(), Is.EqualTo(samples.Count), "one file per sample");
            TestContext.Out.Write(string.Join("\n", samples.Select(s => s.FileName + "  " + s.Label)) + "\n");
        }

        [Test]
        public void EverySample_IsOneOfTheInitialForty_AndPlaysItsSystemAlone()
        {
            var initial = new HashSet<string>(CardCatalog.All.Take(40).Select(d => d.Id));
            foreach (GrammarSample sample in GrammarSamples.All)
            {
                Assert.That(initial.Contains(sample.Card.Id), Is.True, sample.Label + ": a card a run starts with (§2)");
                Assert.That(CardSystems.Of(sample.Card), Is.EqualTo(new[] { sample.System }), sample.Label + ": nothing else on the picture");
            }
        }

        [Test]
        public void TheSlashPicture_AndTheTraitPicture_AreTheSameCard_KesaCut()
        {
            // The issue's example: 袈裟斬り (斬) lights its +5 at gap 0, and is plain from gap 1.
            GrammarSample slash = GrammarSamples.All[0];
            GrammarSample accent = GrammarSamples.All[GrammarSamples.All.Count - 1];
            Assert.That(slash.Card.Id, Is.EqualTo("kesa_cut"));
            Assert.That(accent.Card.Id, Is.EqualTo("kesa_cut"));
            Assert.That(accent.StartGap, Is.EqualTo(0));
            Assert.That(slash.StartGap, Is.EqualTo(1));
        }

        /// <summary>
        /// The effect a sample is pictured by, read off §5.3 / §5.9 and the system alone, not off the play:
        /// the trait cue is TraitAccent, a blow its StrikeShape, 盾 / 型 / 気 their mark, and 息 the chest's
        /// rise on a heal (the player opens at half HP so a heal has room to show).
        /// </summary>
        private static EffectId ExpectedWatch(GrammarSample sample)
        {
            if (sample.TraitAccent) return EffectId.TraitAccent;
            if (EffectGrammar.IsBlow(sample.System)) return EffectId.StrikeShape;
            if (sample.System == FxSystem.Breath) return EffectId.BreathHeave;
            return EffectId.GrammarMark;
        }

        [Test]
        public void EverySamplesSource_PlaysTheCard_AndTheWatchedEffectIsOnTheScreensPlan()
        {
            foreach (GrammarSample sample in GrammarSamples.All)
            {
                EffectId expected = ExpectedWatch(sample);
                Assert.That(sample.Watch, Is.EqualTo(expected), sample.Label + ": the effect the capture waits for");
                DepictionEvent play = GrammarSamples.PlayFirstCard(sample.CreateSource());
                Assert.That(play, Is.Not.Null, sample.Label + ": the auto drag plays nothing");
                Assert.That(play.Title, Is.EqualTo(sample.Card.Name), sample.Label);
                Assert.That(GrammarSamples.WatchOf(play, sample.System, sample.TraitAccent), Is.EqualTo(expected), sample.Label + ": the play shows it");
                // The eight show their system alone; only the last one shows a trait firing.
                Assert.That(GrammarSamples.TraitFired(play), Is.EqualTo(sample.TraitAccent), sample.Label + ": a trait fires");
                if (EffectCatalog.Of(sample.Watch).Blocking)
                {
                    // A waited beat is one of the plan's steps, so the screen holds it and the trace records it.
                    Assert.That(EffectPlan.StepsOf(play).Any(s => s.Id == sample.Watch), Is.True, sample.Label);
                }
            }
        }

        [Test]
        public void TheSamplePictures_ShowEightDifferentLooks()
        {
            var looks = GrammarSamples.All.Where(s => !s.TraitAccent).Select(s => EffectGrammar.LookId(s.System)).ToList();
            Assert.That(looks.Distinct().Count(), Is.EqualTo(EffectGrammar.Systems.Length));
        }
    }
}
