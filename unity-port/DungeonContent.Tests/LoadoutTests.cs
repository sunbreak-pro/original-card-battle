using DungeonContent;

namespace DungeonContent.Tests
{
    public class LoadoutTests
    {
        [Test]
        public void ThreeToolsAndThreeConsumables()
        {
            // concept-v3.md §14.
            Assert.That(Loadout.ToolSlots, Is.EqualTo(3));
            Assert.That(Loadout.ConsumableSlots, Is.EqualTo(3));

            Assert.Throws<LoadoutRuleException>(() => Loadout.Of(
                new[] { "bosho_no_men", "kokugen_no_suna", "toma_no_kutsu", "ikitsugi_no_fue" }));
            Assert.Throws<LoadoutRuleException>(() => Loadout.Of(
                Array.Empty<string>(),
                new[] { "joka_no_ko", "yakuso", "wasuremizu", "koi_hoshiniku" }));
        }

        [Test]
        public void TheSurvivorBonusAddsAFourthToolSlot()
        {
            // concept-v3.md §8.2 / §14, for one life only.
            var four = Loadout.Of(
                new[] { "bosho_no_men", "kokugen_no_suna", "toma_no_kutsu", "ikitsugi_no_fue" },
                null,
                Loadout.SurvivorToolSlots);
            Assert.That(four.Tools.Count, Is.EqualTo(4));
        }

        [Test]
        public void TheSameToolCanBeTakenTwice()
        {
            // Issue #100 decision. It is what lets the survivor's fourth slot reach the density
            // relief of 2 that seven_layers_v4.md §3.3 counts on.
            var doubled = Loadout.Of(new[] { "bosho_no_men", "bosho_no_men", "kokugen_no_suna" });
            Assert.That(doubled.MiasmaDensityRelief, Is.EqualTo(2));
            Assert.That(doubled.TimeLimitBonus, Is.EqualTo(1));
        }

        [Test]
        public void ABareLoadoutReliefsNothing()
        {
            Assert.That(Loadout.Empty.MiasmaDensityRelief, Is.Zero);
            Assert.That(Loadout.Empty.TimeLimitBonus, Is.Zero);
            Assert.That(Loadout.Empty.Has(ItemEffect.StartFar), Is.False);
        }

        [Test]
        public void AConsumableGoesIntoAFreeSlot()
        {
            var carried = Loadout.Empty.Take("joka_no_ko").Take("yakuso");
            Assert.That(carried.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "joka_no_ko", "yakuso" }));
        }

        [Test]
        public void AFullConsumableBeltIsSwappedInPlace()
        {
            // concept-v3.md §14: picked up on the spot, swapped on the spot, no storage.
            var full = Loadout.Of(Array.Empty<string>(), new[] { "joka_no_ko", "yakuso", "wasuremizu" });
            Assert.Throws<LoadoutRuleException>(() => full.Take("koi_hoshiniku"));

            var swapped = full.Take("koi_hoshiniku", replaceSlot: 1);
            Assert.That(swapped.Consumables.Select(c => c.Id),
                Is.EqualTo(new[] { "joka_no_ko", "koi_hoshiniku", "wasuremizu" }));
        }

        [Test]
        public void UsingAConsumableSpendsIt()
        {
            var carried = Loadout.Of(Array.Empty<string>(), new[] { "joka_no_ko", "yakuso" });
            var after = carried.Use(0, out var used);

            Assert.That(used.Id, Is.EqualTo("joka_no_ko"));
            Assert.That(after.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "yakuso" }));
        }

        [Test]
        public void UsingAnEmptySlotThrows()
        {
            Assert.Throws<LoadoutRuleException>(() => Loadout.Empty.Use(0, out _));
        }

        [Test]
        public void NothingConsumableComesHome()
        {
            var carried = Loadout.Of(new[] { "bosho_no_men" }, new[] { "joka_no_ko", "yakuso" });
            var home = carried.LeaveTheDungeon();

            Assert.That(home.Consumables, Is.Empty);
            Assert.That(home.Tools.Select(t => t.Id), Is.EqualTo(new[] { "bosho_no_men" }));
        }

        [Test]
        public void ALoadoutIsNotChangedByWhatComesAfterIt()
        {
            var start = Loadout.Of(Array.Empty<string>(), new[] { "joka_no_ko" });
            start.Take("yakuso");
            Assert.That(start.Consumables.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReachingDeeperAndFightingHarderCannotBothBeMaxedOut()
        {
            // The point of sharing the slots: three tools cannot cover both sides.
            var deep = Loadout.Of(new[] { "bosho_no_men", "kokugen_no_suna", "utsushi_no_tobari" });
            Assert.That(deep.Tools.All(t => t.Side == EffectSide.Exploration), Is.True);
            Assert.That(deep.Has(ItemEffect.StartFar), Is.False);
            Assert.That(deep.Has(ItemEffect.OpeningGuard), Is.False);

            var fighty = Loadout.Of(new[] { "toma_no_kutsu", "ikitsugi_no_fue", "ryurin_no_kakera" });
            Assert.That(fighty.MiasmaDensityRelief, Is.Zero);
            Assert.That(fighty.TimeLimitBonus, Is.Zero);
        }
    }
}
