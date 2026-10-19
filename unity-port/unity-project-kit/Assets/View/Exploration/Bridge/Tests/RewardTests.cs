// #625: the rest of a won encounter's reward (encounters_and_rewards_v4.md §4.2 / §4.3) — three cards
// from the layer's pile to pick one from, consumables and pages — drawn from the exploration's seed.
// Runs under `dotnet test` (Exploration.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using DungeonContent;
using DungeonCore;
using NUnit.Framework;

namespace Exploration.Bridge.Tests
{
    public class RewardTests
    {
        private const int Draws = 4000;

        // ---- 通常戦闘 ----

        [Test]
        public void ANormalBattle_DropsOneOfCardConsumableOrPage_AtTheLayersChances()
        {
            for (int layer = 1; layer <= 6; layer++)
            {
                (int card, int consumable, int page) = BattleRewards.NormalChances(layer);
                Assert.That(card + consumable + page, Is.EqualTo(100), $"layer {layer}");
                int cards = 0, consumables = 0, pages = 0;
                var rng = new DungeonCore.SeededRng((ulong)layer);
                for (int i = 0; i < Draws; i++)
                {
                    BattleReward reward = Roll(NodeKind.Battle, layer, Normal(), rng);
                    int parts = (reward.CardChoices.Count > 0 ? 1 : 0) + reward.Consumables.Count + reward.Pages;
                    Assert.That(parts, Is.EqualTo(1), $"layer {layer}: one of the three");
                    if (reward.CardChoices.Count > 0) cards++;
                    if (reward.Consumables.Count > 0) consumables++;
                    if (reward.Pages > 0) pages++;
                }
                Assert.Multiple(() =>
                {
                    Assert.That(Percent(cards), Is.EqualTo(card).Within(3), $"layer {layer}: card");
                    Assert.That(Percent(consumables), Is.EqualTo(consumable).Within(3), $"layer {layer}: consumable");
                    Assert.That(Percent(pages), Is.EqualTo(page).Within(3), $"layer {layer}: page");
                });
            }
        }

        [Test]
        public void ANormalBattlesRoll_SplitsAtTheTablesEdges()
        {
            // Layer 2 is 35 / 60 / 5: a roll of 34 is a card, 35 a consumable, 94 a consumable, 95 a page.
            Assert.Multiple(() =>
            {
                Assert.That(Roll(NodeKind.Battle, 2, Normal(), new Fixed(0.34)).CardChoices, Is.Not.Empty);
                Assert.That(Roll(NodeKind.Battle, 2, Normal(), new Fixed(0.35)).Consumables, Is.Not.Empty);
                Assert.That(Roll(NodeKind.Battle, 2, Normal(), new Fixed(0.94)).Consumables, Is.Not.Empty);
                Assert.That(Roll(NodeKind.Battle, 2, Normal(), new Fixed(0.95)).Pages, Is.EqualTo(1));
                Assert.That(Roll(NodeKind.Battle, 1, Normal(), new Fixed(0.999)).Pages, Is.EqualTo(0), "layer 1 drops no page");
            });
        }

        [Test]
        public void TheLastLayersBattle_AlwaysDropsAPage_AndACardHalfTheTime()
        {
            int cards = 0;
            var rng = new DungeonCore.SeededRng(7UL);
            for (int i = 0; i < Draws; i++)
            {
                BattleReward reward = Roll(NodeKind.Battle, 7, Normal(), rng);
                Assert.That(reward.Pages, Is.EqualTo(1));
                Assert.That(reward.Consumables, Is.Empty, "layer 7 drops no consumable");
                if (reward.CardChoices.Count > 0) cards++;
            }
            Assert.That(Percent(cards), Is.EqualTo(BattleRewards.LastLayerCardPercent).Within(3));
        }

        [Test]
        public void TheSameSeed_DrawsTheSameReward()
        {
            BattleReward a = Roll(NodeKind.Battle, 5, Normal(), new DungeonCore.SeededRng(42UL));
            BattleReward b = Roll(NodeKind.Battle, 5, Normal(), new DungeonCore.SeededRng(42UL));
            Assert.That(b.CardChoices, Is.EqualTo(a.CardChoices));
            Assert.That(b.Consumables, Is.EqualTo(a.Consumables));
            Assert.That(b.Pages, Is.EqualTo(a.Pages));
        }

        // ---- 精鋭 ----

        [Test]
        public void ARealElite_GivesItsOfferAndAConsumable_NotAPileCard()
        {
            var elite = Single("pack_alpha", standIn: false);
            UniqueCardReward unique = UniqueCards.For(NodeKind.Elite, elite, new HashSet<string>());
            BattleReward reward = BattleRewards.Roll(NodeKind.Elite, 7, elite, unique, new HashSet<string>(), new DungeonCore.SeededRng(1UL));
            Assert.Multiple(() =>
            {
                Assert.That(unique.Offered, Is.EqualTo(UniqueCards.OfElite("pack_alpha")));
                Assert.That(reward.CardChoices, Is.Empty, "the elite's two cards are the card");
                Assert.That(reward.Consumables.Count, Is.EqualTo(1));
                Assert.That(reward.Pages, Is.EqualTo(1), "layer 7's elite always drops a page");
            });
        }

        [Test]
        public void AStandInElite_OrARealOneWhoseCardsAreOwned_GivesThreeCardsFromThePile()
        {
            var owned = new HashSet<string>(UniqueCards.OfElite("polearm_unyielding"));
            var real = Single("polearm_unyielding", standIn: false);
            BattleReward fromReal = BattleRewards.Roll(
                NodeKind.Elite, 5, real, UniqueCards.For(NodeKind.Elite, real, owned), owned, new DungeonCore.SeededRng(1UL));
            BattleReward fromStandIn = Roll(NodeKind.Elite, 1, Chain("polearm_warped", "shadow_hound"), new DungeonCore.SeededRng(1UL));
            Assert.Multiple(() =>
            {
                Assert.That(fromReal.CardChoices, Is.EquivalentTo(CardPile.Of(5)));
                Assert.That(fromReal.Consumables.Count, Is.EqualTo(1));
                Assert.That(fromStandIn.CardChoices, Is.EquivalentTo(CardPile.Of(1)));
                Assert.That(fromStandIn.Consumables.Count, Is.EqualTo(1));
                Assert.That(fromStandIn.Pages, Is.EqualTo(0), "layer 1's elite drops no page");
            });
        }

        // ---- 階層ボス ----

        [Test]
        public void TheBosses_AddWhatTheirRowOfTheTableSays()
        {
            BattleReward real = Roll(NodeKind.Boss, 3, Single("miasma_priest", standIn: false), new DungeonCore.SeededRng(1UL));
            BattleReward eliteStandIn = Roll(NodeKind.Boss, 4, Single("armored_warden", standIn: true), new DungeonCore.SeededRng(1UL));
            BattleReward normalStandIn = Roll(NodeKind.Boss, 1, Single("shadow_hound", standIn: true), new Fixed(0.0));
            Assert.Multiple(() =>
            {
                Assert.That(real.CardChoices, Is.Empty);
                Assert.That(real.Consumables, Is.Empty);
                Assert.That(real.Pages, Is.EqualTo(1), "a real boss: its three cards and a rare page");

                Assert.That(eliteStandIn.CardChoices, Is.Empty, "it teaches the elite's two cards instead");
                Assert.That(eliteStandIn.Consumables.Count, Is.EqualTo(1));
                Assert.That(eliteStandIn.Pages, Is.EqualTo(1));

                Assert.That(normalStandIn.CardChoices, Is.EquivalentTo(CardPile.Of(1)));
                Assert.That(normalStandIn.Consumables.Count, Is.EqualTo(1));
                Assert.That(normalStandIn.Pages, Is.EqualTo(1), "a 50% page that the roll 0 lands");
            });
        }

        // ---- 札の山 ----

        [Test]
        public void ThePile_FollowsTheCardTable()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CardPile.Of(1), Is.EqualTo(new[] { CardCatalog.ShieldPush.Id, CardCatalog.SnapGuard.Id, CardCatalog.LeapBack.Id }), "#58〜#60");
                Assert.That(CardPile.Of(7).Last(), Is.EqualTo(CardCatalog.LastStand.Id), "#80");
                Assert.That(Enumerable.Range(1, 7).SelectMany(CardPile.Of).Count(), Is.EqualTo(23), "#58〜#80");
            });
        }

        [Test]
        public void ThePile_DealsTheLayersOwnCardsFirst_ThenTheShallowerLayers()
        {
            var owned = new HashSet<string>(CardPile.Of(3).Take(2));
            IReadOnlyList<string> dealt = CardPile.Deal(3, owned, new DungeonCore.SeededRng(9UL));
            Assert.That(dealt.Count, Is.EqualTo(CardPile.Choices));
            Assert.That(dealt[0], Is.EqualTo(CardPile.Of(3)[2]), "the one card layer 3 has left");
            Assert.That(dealt.Skip(1).All(CardPile.Of(2).Contains), Is.True, "layer 2 fills the rest");
            Assert.That(dealt.Distinct().Count(), Is.EqualTo(dealt.Count));
        }

        [Test]
        public void ASpentPile_TurnsTheCardIntoAConsumable()
        {
            var owned = new HashSet<string>(Enumerable.Range(1, 2).SelectMany(CardPile.Of));
            Assert.That(CardPile.Deal(2, owned, new DungeonCore.SeededRng(1UL)), Is.Empty);

            BattleReward reward = BattleRewards.Roll(NodeKind.Battle, 2, Normal(), UniqueCardReward.None, owned, new Fixed(0.0));
            Assert.That(reward.CardChoices, Is.Empty);
            Assert.That(reward.Consumables.Count, Is.EqualTo(1));
        }

        // ---- 消耗品 ----

        [Test]
        public void TheConsumables_FollowTheLayersWeights()
        {
            for (int layer = 1; layer <= 7; layer++)
            {
                Assert.That(BattleRewards.ConsumableIds.Sum(id => BattleRewards.WeightOf(id, layer)), Is.EqualTo(100), $"layer {layer}");
                var counts = BattleRewards.ConsumableIds.ToDictionary(id => id, _ => 0);
                var rng = new DungeonCore.SeededRng((ulong)(100 + layer));
                for (int i = 0; i < Draws; i++) counts[BattleRewards.DrawConsumable(layer, rng)]++;
                foreach (string id in BattleRewards.ConsumableIds)
                {
                    Assert.That(Percent(counts[id]), Is.EqualTo(BattleRewards.WeightOf(id, layer)).Within(3), $"layer {layer}: {id}");
                }
            }
            Assert.That(BattleRewards.WeightOf("shirube_no_ishi", 7), Is.EqualTo(0), "§4.3: no 標の石 on layer 7");
        }

        [Test]
        public void EveryConsumableTheTableNames_IsInTheCatalogue()
        {
            Assert.That(BattleRewards.ConsumableIds, Is.EquivalentTo(ItemCatalogue.Consumables.Select(c => c.Id)));
        }

        // ---- helpers ----

        private static double Percent(int count) => count * 100.0 / Draws;

        private static Encounter Normal() => Single("polearm_warped", standIn: false);

        private static Encounter Single(string id, bool standIn) => new Encounter(new[] { new Fight(new[] { id }) }, standIn);

        private static Encounter Chain(params string[] ids) => new Encounter(ids.Select(id => new Fight(new[] { id })).ToList(), isStandIn: true);

        private static BattleReward Roll(NodeKind kind, int layer, Encounter encounter, DungeonCore.IRng rng)
        {
            var owned = new HashSet<string>();
            return BattleRewards.Roll(kind, layer, encounter, UniqueCards.For(kind, encounter, owned), owned, rng);
        }

        /// <summary>Always draws the same value, to land a roll on a table's edge.</summary>
        private sealed class Fixed : DungeonCore.IRng
        {
            private readonly double _value;

            public Fixed(double value) => _value = value;

            public double NextDouble() => _value;
        }
    }
}
