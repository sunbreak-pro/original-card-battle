// The rest of a won encounter's reward (encounters_and_rewards_v4.md §4.2 / §4.3, #625): cards from the
// layer's card pile to pick one from, consumables and rare pages. The unique cards of a real boss or
// elite are UniqueCards'; this file draws what the table adds around them. The pile's cards are the card
// table's (swordsman_cards_v4.md §3, cards lane) and the chances and weights are §4.2 / §4.3's; all are
// copies, changed when the canon moves. The draws come from the exploration's seed, so a life replays them.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using DungeonCore;

namespace Exploration.Bridge
{
    /// <summary>What a won encounter adds besides its unique cards.</summary>
    public sealed class BattleReward
    {
        public static readonly BattleReward None = new BattleReward(Array.Empty<string>(), Array.Empty<string>(), 0);

        public BattleReward(IReadOnlyList<string> cardChoices, IReadOnlyList<string> consumables, int pages)
        {
            if (pages < 0) throw new ArgumentOutOfRangeException(nameof(pages));
            CardChoices = cardChoices ?? Array.Empty<string>();
            Consumables = consumables ?? Array.Empty<string>();
            Pages = pages;
        }

        /// <summary>Card ids from the card pile, one to pick (§4.3: up to three). Empty when no card came.</summary>
        public IReadOnlyList<string> CardChoices { get; }

        /// <summary>Consumable ids found (ItemCatalogue.Consumables), in the order they were drawn.</summary>
        public IReadOnlyList<string> Consumables { get; }

        /// <summary>Rare journal pages found. What a page holds is #107's.</summary>
        public int Pages { get; }

        public bool IsEmpty => CardChoices.Count == 0 && Consumables.Count == 0 && Pages == 0;
    }

    /// <summary>§4.3 札の山: the learned cards each layer's fights hand out.</summary>
    public static class CardPile
    {
        /// <summary>§4.3: how many cards are laid out to pick one from.</summary>
        public const int Choices = 3;

        // §4.3's table as canon numbers (swordsman_cards_v4.md §3), layer 1 first.
        private static readonly int[][] Numbers =
        {
            new[] { 58, 59, 60 },
            new[] { 61, 62, 63 },
            new[] { 64, 65, 66 },
            new[] { 67, 68, 69 },
            new[] { 70, 71, 72 },
            new[] { 73, 74, 75, 76 },
            new[] { 77, 78, 79, 80 },
        };

        /// <summary>The card ids of a layer's pile, in canon order.</summary>
        public static IReadOnlyList<string> Of(int layer)
        {
            BattleRewards.CheckLayer(layer);
            return Numbers[layer - 1].Select(n => CardCatalog.All[n - 1].Id).ToList();
        }

        /// <summary>
        /// §4.3: up to three cards the player does not own. The layer's own pile comes first, and what it
        /// lacks is filled from the shallower layers' piles, the nearest first. Each pile is shuffled, so
        /// which cards of a four-card pile show is drawn. Empty when every pile up to the layer is spent.
        /// </summary>
        public static IReadOnlyList<string> Deal(int layer, IReadOnlyCollection<string> owned, DungeonCore.IRng rng)
        {
            BattleRewards.CheckLayer(layer);
            if (owned == null) throw new ArgumentNullException(nameof(owned));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var dealt = new List<string>();
            for (int l = layer; l >= 1 && dealt.Count < Choices; l--)
            {
                var left = Of(l).Where(id => !owned.Contains(id)).ToList();
                rng.Shuffle(left);
                dealt.AddRange(left.Take(Choices - dealt.Count));
            }
            return dealt;
        }
    }

    /// <summary>§4.2: the reward of a won encounter on a combat node, around its unique cards.</summary>
    public static class BattleRewards
    {
        // §4.2 通常戦闘, layers 1〜6: the chance in percent of a card, a consumable and a page. Each row adds to 100.
        private static readonly int[][] NormalPercent =
        {
            new[] { 30, 70, 0 },
            new[] { 35, 60, 5 },
            new[] { 40, 50, 10 },
            new[] { 45, 40, 15 },
            new[] { 50, 30, 20 },
            new[] { 50, 25, 25 },
        };

        /// <summary>§4.2: layer 7's battles always drop a page, and draw a card apart from it at this chance.</summary>
        public const int LastLayerCardPercent = 50;

        // §4.2 精鋭: the chance in percent of a page on top of the card and the consumable, layer 1 first.
        private static readonly int[] ElitePagePercent = { 0, 10, 20, 30, 40, 50, 100 };

        /// <summary>§4.2: a stand-in boss of layers 1〜2 drops a page at this chance.</summary>
        public const int StandInBossPagePercent = 50;

        /// <summary>§4.3: the consumables a fight can drop, in the weight table's column order.</summary>
        public static readonly IReadOnlyList<string> ConsumableIds = new[] { "joka_no_ko", "yakuso", "koi_hoshiniku", "wasuremizu", "shirube_no_ishi" };

        // §4.3 消耗品の重み, in ConsumableIds' order, layer 1 first. Each row adds to 100.
        private static readonly int[][] ConsumableWeights =
        {
            new[] { 10, 35, 20, 30, 5 },
            new[] { 15, 30, 20, 30, 5 },
            new[] { 20, 30, 20, 25, 5 },
            new[] { 30, 25, 20, 20, 5 },
            new[] { 35, 25, 20, 15, 5 },
            new[] { 40, 25, 20, 10, 5 },
            new[] { 50, 30, 20, 0, 0 },
        };

        /// <summary>The chance in percent a 通常戦闘 of a layer 1〜6 drops a card, a consumable and a page.</summary>
        public static (int Card, int Consumable, int Page) NormalChances(int layer)
        {
            CheckLayer(layer);
            if (layer == SevenLayers.Count) throw new ArgumentOutOfRangeException(nameof(layer), layer, "Layer 7 drops a page and draws a card apart from it.");
            int[] row = NormalPercent[layer - 1];
            return (row[0], row[1], row[2]);
        }

        /// <summary>§4.3: the weight a consumable has on a layer; 0 for an id the table does not name.</summary>
        public static int WeightOf(string consumableId, int layer)
        {
            CheckLayer(layer);
            int index = IndexOf(consumableId);
            return index < 0 ? 0 : ConsumableWeights[layer - 1][index];
        }

        /// <summary>One consumable, drawn by the layer's weights.</summary>
        public static string DrawConsumable(int layer, DungeonCore.IRng rng)
        {
            CheckLayer(layer);
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            int[] weights = ConsumableWeights[layer - 1];
            int roll = rng.NextInt(0, weights.Sum());
            for (int i = 0; i < weights.Length; i++)
            {
                if (roll < weights[i]) return ConsumableIds[i];
                roll -= weights[i];
            }
            throw new InvalidOperationException("BattleRewards: the weights did not cover the roll.");
        }

        /// <summary>
        /// §4.2 for a won encounter on a node of <paramref name="kind"/>, given the unique cards it already
        /// teaches (<see cref="UniqueCards.For"/>).
        ///
        /// - 通常戦闘, layers 1〜6: a card, a consumable or a page, by the layer's chances.
        /// - 通常戦闘, layer 7: a page, and a card at 50% apart from it.
        /// - 精鋭: a card and a consumable, and a page at the layer's chance. The card is the real elite's
        ///   offer when there is one; otherwise (a stand-in, or both its cards owned) it is the pile's.
        /// - A real boss: a page on top of its three cards.
        /// - A stand-in boss that is an elite (layers 4〜5): a consumable and a page on top of its cards.
        /// - Any other stand-in boss (layers 1〜2): a card from the pile, a consumable, and a page at 50%.
        ///
        /// A card the piles can no longer deal becomes a consumable (§4.3).
        /// </summary>
        public static BattleReward Roll(
            NodeKind kind,
            int layer,
            Encounter encounter,
            UniqueCardReward unique,
            IReadOnlyCollection<string> owned,
            DungeonCore.IRng rng)
        {
            CheckLayer(layer);
            if (encounter == null) throw new ArgumentNullException(nameof(encounter));
            if (unique == null) throw new ArgumentNullException(nameof(unique));
            if (owned == null) throw new ArgumentNullException(nameof(owned));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var draw = new Draw(layer, owned, rng);
            switch (kind)
            {
                case NodeKind.Battle when layer == SevenLayers.Count:
                    draw.Page(100);
                    if (rng.NextChance(LastLayerCardPercent / 100.0)) draw.Card();
                    break;
                case NodeKind.Battle:
                    int[] percent = NormalPercent[layer - 1];
                    int roll = rng.NextInt(0, 100);
                    if (roll < percent[0]) draw.Card();
                    else if (roll < percent[0] + percent[1]) draw.Consumable();
                    else draw.Page(100);
                    break;
                case NodeKind.Elite:
                    if (unique.Offered.Count == 0) draw.Card();
                    draw.Consumable();
                    draw.Page(ElitePagePercent[layer - 1]);
                    break;
                case NodeKind.Boss when !encounter.IsStandIn:
                    draw.Page(100);
                    break;
                case NodeKind.Boss when IsEliteStandIn(encounter):
                    draw.Consumable();
                    draw.Page(100);
                    break;
                case NodeKind.Boss:
                    draw.Card();
                    draw.Consumable();
                    draw.Page(StandInBossPagePercent);
                    break;
                default:
                    return BattleReward.None;
            }
            return draw.Result();
        }

        internal static void CheckLayer(int layer)
        {
            if (layer < 1 || layer > SevenLayers.Count) throw new ArgumentOutOfRangeException(nameof(layer), layer, "Layers run 1〜7.");
        }

        /// <summary>A stand-in boss borrowed from a §9 elite (layers 4〜5), whose cards it teaches.</summary>
        private static bool IsEliteStandIn(Encounter encounter)
        {
            var enemies = encounter.EnemyIds.ToList();
            return enemies.Count == 1 && UniqueCards.OfElite(enemies[0]).Count > 0;
        }

        private static int IndexOf(string consumableId)
        {
            for (int i = 0; i < ConsumableIds.Count; i++)
            {
                if (string.Equals(ConsumableIds[i], consumableId, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        /// <summary>The reward as it is drawn, one part at a time in the rule's order.</summary>
        private sealed class Draw
        {
            private readonly int _layer;
            private readonly IReadOnlyCollection<string> _owned;
            private readonly DungeonCore.IRng _rng;
            private readonly List<string> _consumables = new List<string>();
            private IReadOnlyList<string> _cards = Array.Empty<string>();
            private int _pages;

            public Draw(int layer, IReadOnlyCollection<string> owned, DungeonCore.IRng rng)
            {
                _layer = layer;
                _owned = owned;
                _rng = rng;
            }

            public void Card()
            {
                var cards = CardPile.Deal(_layer, _owned, _rng);
                if (cards.Count == 0) Consumable();
                else _cards = cards;
            }

            public void Consumable() => _consumables.Add(DrawConsumable(_layer, _rng));

            public void Page(int percent)
            {
                if (_rng.NextChance(percent / 100.0)) _pages++;
            }

            public BattleReward Result() => new BattleReward(_cards, _consumables, _pages);
        }
    }
}
