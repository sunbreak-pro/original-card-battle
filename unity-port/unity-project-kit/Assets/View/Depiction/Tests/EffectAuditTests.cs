// The effect table of #78: every EffectId has a length, a family, what it carries and a judgement.
// An effect added later fails here until it has a row, so it cannot skip the measurement.
using System;
using System.Linq;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class EffectAuditTests
    {
        [Test]
        public void EveryEffect_HasAJudgement_WithAReason()
        {
            foreach (EffectId id in Enum.GetValues(typeof(EffectId)))
            {
                Assert.That(EffectAudit.Has(id), Is.True, id + " has no row in EffectAudit (Script/EffectAudit.cs)");
                EffectAuditEntry entry = EffectAudit.Of(id);
                Assert.That(entry.Family, Is.Not.Empty, id.ToString());
                Assert.That(entry.Reason.Length, Is.GreaterThan(10), id + " needs a reason");
            }
            Assert.That(EffectAudit.All.Count, Is.EqualTo(Enum.GetValues(typeof(EffectId)).Length), "a row for a name that is not an effect");
        }

        [Test]
        public void ADroppedEffect_CarriesNothingTheFrameLacks_AndNeededOnesCarrySomething()
        {
            foreach (EffectAuditEntry entry in EffectAudit.All)
            {
                if (entry.Verdict == Verdict.Needed)
                {
                    Assert.That(entry.Carries, Is.Not.EqualTo(Carries.None), entry.Id + " is judged needed but carries nothing");
                }
                if (entry.Carries == Carries.None)
                {
                    Assert.That(entry.Verdict, Is.EqualTo(Verdict.Drop), entry.Id + " carries nothing, so it cannot be kept as it is");
                }
            }
        }

        [Test]
        public void ADecisionThatRestsOnHowItFeels_IsHandedTo79()
        {
            // A "drop" judged from the data alone would be a claim nobody looked at: every drop is provisional.
            foreach (EffectAuditEntry entry in EffectAudit.All.Where(e => e.Verdict == Verdict.Drop))
            {
                Assert.That(entry.Basis, Is.EqualTo(Basis.HandsOn), entry.Id + " is dropped without a hands-on check");
            }
        }

        [Test]
        public void TheFlowGaps_AreTheOnesTheScreenWaits()
        {
            Assert.That(EffectFlow.AfterPlayerEventMs, Is.EqualTo(250f));
            Assert.That(EffectFlow.AfterAutoEventMs, Is.EqualTo(350f));
        }
    }
}
