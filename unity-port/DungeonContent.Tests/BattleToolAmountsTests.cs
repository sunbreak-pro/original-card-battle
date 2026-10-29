using DungeonContent;

namespace DungeonContent.Tests
{
    /// <summary>
    /// #648: the one mapping from a battle-side item effect to the four numbers the battle starts with.
    /// The exploration's hand-over and the departure screen both read it, so an effect added to the
    /// battle side without a mapping here fails these tests instead of going quiet on one screen.
    /// </summary>
    public class BattleToolAmountsTests
    {
        private static IEnumerable<ItemEffect> AllEffects() => Enum.GetValues(typeof(ItemEffect)).Cast<ItemEffect>();

        private static int[] Numbers(BattleToolAmounts a) =>
            new[] { a.StartCellsBack, a.StartStamina, a.OpeningGuard, a.BossWordWard };

        [Test]
        public void EveryBattleSideEffect_MapsToExactlyOneOfTheFourNumbers_AtItsAmount()
        {
            var battle = AllEffects().Where(e => e.Side() == EffectSide.Battle).ToList();
            Assert.That(battle.Count, Is.GreaterThan(0));
            foreach (ItemEffect effect in battle)
            {
                BattleToolAmounts amounts = default;
                Assert.DoesNotThrow(() => amounts = BattleToolAmounts.Of(effect, 3), effect + " has no mapping to the battle's start");
                int[] numbers = Numbers(amounts);
                Assert.That(numbers.Count(n => n == 3), Is.EqualTo(1), effect + " carries its amount into one number");
                Assert.That(numbers.Count(n => n == 0), Is.EqualTo(3), effect + " leaves the other three at 0");
            }
        }

        [Test]
        public void EveryOneOfTheFourNumbers_IsReachedByABattleSideEffect()
        {
            var sum = AllEffects()
                .Where(e => e.Side() == EffectSide.Battle)
                .Aggregate(BattleToolAmounts.None, (acc, e) => acc.Plus(BattleToolAmounts.Of(e, 1)));
            Assert.That(Numbers(sum).All(n => n >= 1), Is.True, "every number has an effect behind it: " + sum);
        }

        [Test]
        public void EveryExplorationSideEffect_DoesNotReachTheBattle()
        {
            foreach (ItemEffect effect in AllEffects().Where(e => e.Side() == EffectSide.Exploration))
                Assert.That(BattleToolAmounts.Of(effect, 5), Is.EqualTo(BattleToolAmounts.None), effect.ToString());
        }

        [Test]
        public void TheCataloguesBattleTools_ReadTheirAmounts()
        {
            // tools_and_consumables_v4.md §2: the four battle tools as the departure screen and the
            // hand-over have read them since #452.
            Assert.That(BattleToolAmounts.Of(ItemCatalogue.Tool("maai_no_kutsu")), Is.EqualTo(new BattleToolAmounts(1, 0, 0, 0)));
            Assert.That(BattleToolAmounts.Of(ItemCatalogue.Tool("ikitsugi_no_fue")), Is.EqualTo(new BattleToolAmounts(0, 3, 0, 0)));
            Assert.That(BattleToolAmounts.Of(ItemCatalogue.Tool("ryurin_no_kakera")), Is.EqualTo(new BattleToolAmounts(0, 0, 5, 0)));
            Assert.That(BattleToolAmounts.Of(ItemCatalogue.Tool("ryugo_no_fu")), Is.EqualTo(new BattleToolAmounts(0, 0, 0, 1)));
        }

        [Test]
        public void Plus_AddsEachNumber()
        {
            var a = new BattleToolAmounts(1, 2, 3, 4);
            var b = new BattleToolAmounts(10, 20, 30, 40);
            Assert.That(a.Plus(b), Is.EqualTo(new BattleToolAmounts(11, 22, 33, 44)));
            Assert.That(a.Plus(BattleToolAmounts.None), Is.EqualTo(a));
        }
    }
}
