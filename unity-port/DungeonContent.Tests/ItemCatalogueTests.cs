using DungeonContent;

namespace DungeonContent.Tests
{
    public class ItemCatalogueTests
    {
        [Test]
        public void ThereAreSixToEightTools()
        {
            // Issue #100 asks for a first pass of six to eight.
            Assert.That(ItemCatalogue.Tools.Count, Is.InRange(6, 8));
        }

        [Test]
        public void EverySlotIsContestedBetweenExplorationAndBattle()
        {
            // concept-v3.md §14 removed the 探索用 / 戦闘用 split, so the tension only exists
            // if both kinds are worth taking. Four of each, three slots.
            int exploration = ItemCatalogue.Tools.Count(t => t.Side == EffectSide.Exploration);
            int battle = ItemCatalogue.Tools.Count(t => t.Side == EffectSide.Battle);

            Assert.That(exploration, Is.GreaterThanOrEqualTo(3));
            Assert.That(battle, Is.GreaterThanOrEqualTo(3));
            Assert.That(exploration + battle, Is.EqualTo(ItemCatalogue.Tools.Count));
            Assert.That(exploration + battle, Is.GreaterThan(Loadout.ToolSlots));
        }

        [Test]
        public void IdsAreUniqueAcrossTheWholeCatalogue()
        {
            var ids = ItemCatalogue.All.Select(i => i.Id).ToList();
            Assert.That(ids, Is.Unique);
        }

        [Test]
        public void EveryItemIsNamedAndExplained()
        {
            foreach (var item in ItemCatalogue.All)
            {
                Assert.That(item.Name, Is.Not.Empty, item.Id);
                Assert.That(item.Reading, Is.Not.Empty, item.Id);
                Assert.That(item.Note, Is.Not.Empty, item.Id);
                Assert.That(item.Amount, Is.GreaterThan(0), item.Id);
            }
        }

        [Test]
        public void TheMaskAndTheIncenseMatchTheSevenLayerTable()
        {
            // seven_layers_v4.md §3.2 counts on exactly these two numbers.
            var mask = ItemCatalogue.Tool("bosho_no_men");
            Assert.That(mask.Effect, Is.EqualTo(ItemEffect.MiasmaDensity));
            Assert.That(mask.Amount, Is.EqualTo(1));

            var incense = ItemCatalogue.Consumable("joka_no_ko");
            Assert.That(incense.Effect, Is.EqualTo(ItemEffect.MiasmaGauge));
            Assert.That(incense.Amount, Is.EqualTo(10));
        }

        [Test]
        public void OneToolMovesTheStartingPosition()
        {
            // battle_core_v4.md §7.1 and the Definition of Done of #58.
            var movers = ItemCatalogue.Tools.Where(t => t.Effect == ItemEffect.StartFar).ToList();
            Assert.That(movers.Count, Is.EqualTo(1));
            Assert.That(movers[0].Id, Is.EqualTo("toma_no_kutsu"));
        }

        [Test]
        public void OneToolGuardsOnce()
        {
            // concept-v3.md §7.2 lists 一度だけ防御. Guard is the only defence left (armour is frozen).
            Assert.That(ItemCatalogue.Tools.Count(t => t.Effect == ItemEffect.OpeningGuard), Is.EqualTo(1));
        }

        [Test]
        public void NoToolLeansOnSomethingThePostponedListHolds()
        {
            // dungeon_exploration_v4.md §2.2 postpones hidden rooms, shortcuts and traps, so
            // concept-v3 §7.2's "open a door / unlock a hidden route" tools have nothing to act on.
            foreach (var item in ItemCatalogue.All)
            {
                Assert.That(Enum.IsDefined(typeof(ItemEffect), item.Effect), Is.True, item.Id);
            }
            Assert.That(ItemCatalogue.All.Select(i => i.Effect).Distinct().Count(),
                Is.EqualTo(ItemCatalogue.All.Count()), "every item does something different");
        }

        [Test]
        public void TheLayerTwoClueIsNotACarriedItem()
        {
            // world-v1.md §5.2: it is a node, and picking it up has no combat effect.
            Assert.That(ItemCatalogue.All.Any(i => i.Id.Contains("hiseki") || i.Name.Contains("秘跡")), Is.False);
            Assert.That(ItemCatalogue.All.Any(i => i.Name.Contains("彫り")), Is.False);
        }

        [Test]
        public void EveryEffectKnowsWhichCoreReadsIt()
        {
            foreach (ItemEffect effect in Enum.GetValues(typeof(ItemEffect)))
            {
                Assert.DoesNotThrow(() => effect.Side());
                Assert.That(effect.ToToken(), Is.Not.Empty);
            }
        }

        [Test]
        public void EffectTokensAreUnique()
        {
            var tokens = Enum.GetValues(typeof(ItemEffect)).Cast<ItemEffect>().Select(e => e.ToToken()).ToList();
            Assert.That(tokens, Is.Unique);
        }

        [Test]
        public void AskingForSomethingTheCatalogueDoesNotHoldThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ItemCatalogue.Tool("nope"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ItemCatalogue.Consumable("nope"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ItemCatalogue.Tool("joka_no_ko"));
        }
    }
}
