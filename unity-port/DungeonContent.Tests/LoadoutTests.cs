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
                new[] { "kaerimichi_no_ito", "hiyomi_no_suna", "maai_no_kutsu", "ikitsugi_no_fue" }));
            Assert.Throws<LoadoutRuleException>(() => Loadout.Of(
                Array.Empty<string>(),
                new[] { "hayagake_no_gan", "yakuso", "wasuremizu", "koi_hoshiniku" }));
        }

        [Test]
        public void TheSurvivorBonusAddsAFourthToolSlot()
        {
            // concept-v3.md §8.2 / §14, for one life only.
            var four = Loadout.Of(
                new[] { "kaerimichi_no_ito", "hiyomi_no_suna", "maai_no_kutsu", "ikitsugi_no_fue" },
                null,
                Loadout.SurvivorToolSlots);
            Assert.That(four.Tools.Count, Is.EqualTo(4));
        }

        [Test]
        public void TheSameToolCanBeTakenTwice()
        {
            // Issue #100 decision: the slot count is the only limit. A second calendar tool adds nothing.
            var doubled = Loadout.Of(new[] { "kaerimichi_no_ito", "kaerimichi_no_ito", "hiyomi_no_suna" });
            Assert.That(doubled.Tools.Count, Is.EqualTo(3));
            Assert.That(doubled.ShortHandover, Is.True);
            Assert.That(doubled.EntryDayFree, Is.True);
        }

        [Test]
        public void ABareLoadoutReliefsNothing()
        {
            Assert.That(Loadout.Empty.EntryDayFree, Is.False);
            Assert.That(Loadout.Empty.ShortHandover, Is.False);
            Assert.That(Loadout.Empty.Has(ItemEffect.WiderStart), Is.False);
        }

        [Test]
        public void AConsumableGoesIntoAFreeSlot()
        {
            var carried = Loadout.Empty.Take("hayagake_no_gan").Take("yakuso");
            Assert.That(carried.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "hayagake_no_gan", "yakuso" }));
        }

        [Test]
        public void AFullConsumableBeltIsSwappedInPlace()
        {
            // concept-v3.md §14: picked up on the spot, swapped on the spot, no storage.
            var full = Loadout.Of(Array.Empty<string>(), new[] { "hayagake_no_gan", "yakuso", "wasuremizu" });
            Assert.Throws<LoadoutRuleException>(() => full.Take("koi_hoshiniku"));

            var swapped = full.Take("koi_hoshiniku", replaceSlot: 1);
            Assert.That(swapped.Consumables.Select(c => c.Id),
                Is.EqualTo(new[] { "hayagake_no_gan", "koi_hoshiniku", "wasuremizu" }));
        }

        [Test]
        public void UsingAConsumableSpendsIt()
        {
            var carried = Loadout.Of(Array.Empty<string>(), new[] { "hayagake_no_gan", "yakuso" });
            var after = carried.Use(0, out var used);

            Assert.That(used.Id, Is.EqualTo("hayagake_no_gan"));
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
            var carried = Loadout.Of(new[] { "kaerimichi_no_ito" }, new[] { "hayagake_no_gan", "yakuso" });
            var home = carried.LeaveTheDungeon();

            Assert.That(home.Consumables, Is.Empty);
            Assert.That(home.Tools.Select(t => t.Id), Is.EqualTo(new[] { "kaerimichi_no_ito" }));
        }

        [Test]
        public void ALoadoutIsNotChangedByWhatComesAfterIt()
        {
            var start = Loadout.Of(Array.Empty<string>(), new[] { "hayagake_no_gan" });
            start.Take("yakuso");
            Assert.That(start.Consumables.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReachingDeeperAndFightingHarderCannotBothBeMaxedOut()
        {
            // The point of sharing the slots: three tools cannot cover both sides.
            var deep = Loadout.Of(new[] { "kaerimichi_no_ito", "hiyomi_no_suna", "utsushi_no_tobari" });
            Assert.That(deep.Tools.All(t => t.Side == EffectSide.Exploration), Is.True);
            Assert.That(deep.Has(ItemEffect.WiderStart), Is.False);
            Assert.That(deep.Has(ItemEffect.OpeningGuard), Is.False);

            var fighty = Loadout.Of(new[] { "maai_no_kutsu", "ikitsugi_no_fue", "ryurin_no_kakera" });
            Assert.That(fighty.EntryDayFree, Is.False);
            Assert.That(fighty.ShortHandover, Is.False);
        }
    }
}
