// dotnet-only: the Trial battle's slower stance / guard / move lengths (BattlePacing, 2026-11-02) and the opt-in
// EffectSwitches.SetLength they ride on. The catalog itself and switches that never call SetLength stay as they were.
using Depiction;
using NUnit.Framework;

namespace Trial.Battle.Flow.Tests
{
    public class BattlePacingTests
    {
        [Test]
        public void SwitchesThatSetNoLengthKeepTheCatalog()
        {
            var on = EffectSwitches.AllOn();
            Assert.That(on.Ms(EffectId.FloorMove), Is.EqualTo(EffectCatalog.Of(EffectId.FloorMove).Ms));
            Assert.That(on.WaitMs(EffectId.FloorMove, 3), Is.EqualTo(EffectCatalog.Of(EffectId.FloorMove).TotalMs(3)));
            Assert.That(on.WaitMs(EffectId.FloorMove, 0), Is.EqualTo(0f));
        }

        [Test]
        public void ASetLengthIsStillZeroWhenSwitchedOff()
        {
            var s = EffectSwitches.AllOn().SetLength(EffectId.GuardGain, 500f);
            s.Set(EffectId.GuardGain, false);
            Assert.That(s.Ms(EffectId.GuardGain), Is.EqualTo(0f));
            Assert.That(s.WaitMs(EffectId.GuardGain), Is.EqualTo(0f));
        }

        [Test]
        public void ANegativeLengthIsRefused()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => EffectSwitches.AllOn().SetLength(EffectId.GuardGain, -1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => EffectSwitches.AllOn().SetLength(EffectId.GuardGain, 1f, float.NaN));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TheWalkIsTwoToFourTenthsSlowerForOneToThreeCells(int cells)
        {
            float slower = BattlePacing.Switches().WaitMs(EffectId.FloorMove, cells) - EffectSwitches.AllOn().WaitMs(EffectId.FloorMove, cells);
            Assert.That(slower, Is.InRange(200f, 400f));
        }

        [TestCase(EffectId.GuardGain)]
        [TestCase(EffectId.GuardBlock)]
        [TestCase(EffectId.GuardBreak)]
        [TestCase(EffectId.StanceCue)]
        [TestCase(EffectId.StanceGlow)]
        public void StanceAndGuardAreTwoToFourTenthsSlower(EffectId id)
        {
            float slower = BattlePacing.Switches().Ms(id) - EffectSwitches.AllOn().Ms(id);
            Assert.That(slower, Is.InRange(200f, 400f));
        }

        [Test]
        public void EverythingElseKeepsTheCatalog()
        {
            var paced = BattlePacing.Switches();
            foreach (EffectSpec spec in EffectCatalog.All)
            {
                if (spec.Id == EffectId.FloorMove || spec.Id == EffectId.GuardGain || spec.Id == EffectId.GuardBlock
                    || spec.Id == EffectId.GuardBreak || spec.Id == EffectId.StanceCue || spec.Id == EffectId.StanceGlow) continue;
                Assert.That(paced.Ms(spec.Id), Is.EqualTo(spec.Ms), spec.Id.ToString());
                Assert.That(paced.StaggerMs(spec.Id), Is.EqualTo(spec.StaggerMs), spec.Id.ToString());
            }
        }

        [Test]
        public void TheStanceCardFliesSlowerAndStillRestsInItsSlot()
        {
            Assert.That(L.StanceFlyMs - 300f, Is.InRange(200f, 400f));
            Assert.That(BattlePacing.StanceExileDelayMs - L.StanceFlyMs, Is.EqualTo(EffectCatalog.CardToExileDelayMs - 300f));
        }

        [Test]
        public void OnlyTheGuardRingAndTheStanceCrestMarkSlower()
        {
            float catalog = EffectCatalog.Of(EffectId.GrammarMark).Ms;
            Assert.That(BattlePacing.MarkMs(null, catalog), Is.EqualTo(catalog));
            Assert.That(BattlePacing.MarkMs(FxSystem.Form, 0f), Is.EqualTo(0f), "a switched-off mark stays 0");
            foreach (FxSystem fx in System.Enum.GetValues(typeof(FxSystem)))
            {
                FxForm? form;
                try { form = EffectGrammar.FormOf(fx); }
                catch (System.Exception) { form = null; }
                float expected = form == FxForm.Ring || form == FxForm.Crest ? BattlePacing.GuardStanceMarkMs : catalog;
                Assert.That(BattlePacing.MarkMs(fx, catalog), Is.EqualTo(expected), fx.ToString());
            }
        }
    }
}
