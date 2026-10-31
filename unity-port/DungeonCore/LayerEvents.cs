// Trial extension (port of the browser trial's src/core/dungeon/LayerEvents.js; docs/EXTENSIONS.md
// 「探索の持ち物とイベント」): what an イベント node holds. Designed from encounters_and_rewards_v4.md §5 (the
// three kinds, their effects and their weights per layer) and dungeon_exploration_v4.md §2.1 (イベント 3 種).
// The C# DungeonCore stops at NodeKind.Event (「泉 / 重い宝箱 / 特殊な食事 のどれかが起きる」); trial mode
// (RunStart.Trial) draws the kind here and ExplorationReducer.ResolveEvent applies it. Like LayerEncounters,
// the draw is seeded from the node, so a life replays the same events on the same nodes.
using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCore
{
    /// <summary>The three events of the 80% build (encounters_and_rewards_v4.md §5).</summary>
    public enum EventKind
    {
        /// <summary>泉: HP back by a share of the maximum. No choice.</summary>
        Spring,

        /// <summary>重い宝箱: max stamina −2 for a card, a consumable and/or a page. 受ける / 受けない.</summary>
        HeavyChest,

        /// <summary>特殊な食事: max stamina +1 for 10% of the HP maximum. 受ける / 受けない.</summary>
        StrangeMeal,
    }

    /// <summary>§5 重い宝箱: what the chest holds on a layer.</summary>
    public sealed record ChestContents(int Cards, int Consumables, int Pages);

    public static class LayerEvents
    {
        /// <summary>§5 重い宝箱: max stamina −2, a temporary change (miasma_and_battle_v4.md §5).</summary>
        public const int ChestStaminaCost = 2;

        /// <summary>§5 特殊な食事: max stamina +1, a temporary change.</summary>
        public const int MealStaminaGain = 1;

        /// <summary>§5 特殊な食事: 「代わりに HP が最大の 10% 減る」.</summary>
        public const int MealHpPercent = 10;

        private static readonly EventKind[] Order = { EventKind.Spring, EventKind.HeavyChest, EventKind.StrangeMeal };

        // §5's weights in EventKind order (泉 / 重い宝箱 / 特殊な食事), layer 1 first. Layers 1〜3 and 7 are the
        // table's. Layers 4〜6 hold no event node (seven_layers_v4.md §4); a map that gives them one (a map
        // seam) draws from §5's own extension of the line: 泉 falls 10 a layer and 重い宝箱 takes it, 「泉は六層で
        // 0 になり」 (試作で決めたこと).
        private static readonly int[][] WeightTable =
        {
            new[] { 50, 20, 30 },
            new[] { 40, 30, 30 },
            new[] { 30, 40, 30 },
            new[] { 20, 50, 30 },
            new[] { 10, 60, 30 },
            new[] { 0, 70, 30 },
            new[] { 0, 70, 30 },
        };

        // §5 泉: the share of the HP maximum, layer 1 first. Layer 7 never draws a spring (weight 0); layers 4〜6
        // keep layer 3's 30% (試作で決めたこと: §5 gives no value there).
        private static readonly int[] SpringPercentTable = { 20, 25, 30, 30, 30, 30, 30 };

        // §5 重い宝箱: what the chest holds, layer 1 first. 「カード 1 枚 + 消耗品 1 個 / カード 1 枚 + 頁 1 枚 /
        // カード 1 枚 + 頁 1 枚 + 消耗品 1 個 / 三層と同じ」; layers 4〜6 hold layer 3's too (試作で決めたこと).
        private static readonly ChestContents[] ChestContentsTable =
        {
            new ChestContents(1, 1, 0),
            new ChestContents(1, 0, 1),
            new ChestContents(1, 1, 1),
            new ChestContents(1, 1, 1),
            new ChestContents(1, 1, 1),
            new ChestContents(1, 1, 1),
            new ChestContents(1, 1, 1),
        };

        // js: core/dungeon/LayerEvents.js:52
        private static void Check(int layer)
        {
            if (layer < 1 || layer > SevenLayers.Count)
                throw new ArgumentOutOfRangeException(nameof(layer), layer, $"the lair has {SevenLayers.Count} layers");
        }

        /// <summary>§5's weights on a layer, in EventKind order.</summary>
        // js: core/dungeon/LayerEvents.js:67
        public static IReadOnlyList<int> Weights(int layer)
        {
            Check(layer);
            return WeightTable[layer - 1].ToArray();
        }

        /// <summary>The weight one kind has on a layer.</summary>
        // js: core/dungeon/LayerEvents.js:73
        public static int WeightOf(EventKind kind, int layer)
        {
            Check(layer);
            return WeightTable[layer - 1][Array.IndexOf(Order, kind)];
        }

        /// <summary>§5 泉: the share of the HP maximum it gives back on a layer.</summary>
        // js: core/dungeon/LayerEvents.js:79
        public static int SpringPercent(int layer)
        {
            Check(layer);
            return SpringPercentTable[layer - 1];
        }

        /// <summary>§5 重い宝箱: what it holds on a layer.</summary>
        // js: core/dungeon/LayerEvents.js:85
        public static ChestContents ChestContents(int layer)
        {
            Check(layer);
            return ChestContentsTable[layer - 1];
        }

        /// <summary>§5: 「重い宝箱と特殊な食事は、踏んだ後に「受ける / 受けない」を選べます」. 泉 just happens.</summary>
        // js: core/dungeon/LayerEvents.js:91
        public static bool HasChoice(EventKind kind) =>
            kind == EventKind.HeavyChest || kind == EventKind.StrangeMeal;

        /// <summary>
        /// The kind behind every event node of a layer's map (node id → EventKind, in id order). The nodes are
        /// drawn in id order, each from its own seed (<paramref name="seedOf"/>(nodeId)), by §5's weights; a kind
        /// already drawn on the layer is left out of the next node's draw — 「同じ層の 2 つは、同じ種類を重ねずに
        /// 引きます」. Should a layer ever hold more event nodes than kinds, the full table is drawn from again.
        /// One NextInt per node.
        /// </summary>
        // js: core/dungeon/LayerEvents.js:102
        public static IReadOnlyDictionary<int, EventKind> Draw(LayerMap map, int layer, Func<int, ulong> seedOf)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (seedOf == null) throw new ArgumentNullException(nameof(seedOf));
            Check(layer);
            var weights = WeightTable[layer - 1];
            var drawn = new SortedDictionary<int, EventKind>();
            var used = new HashSet<EventKind>();
            var nodes = map.Nodes.Where(n => n.Kind == NodeKind.Event).Select(n => n.Id).OrderBy(id => id).ToList();
            foreach (int id in nodes)
            {
                var kinds = Order.Where((k, i) => weights[i] > 0 && !used.Contains(k)).ToList();
                if (kinds.Count == 0) kinds = Order.Where((k, i) => weights[i] > 0).ToList();
                var w = kinds.Select(k => weights[Array.IndexOf(Order, k)]).ToList();
                var rng = new SeededRng(seedOf(id));
                int roll = rng.NextInt(0, w.Sum());
                var kind = kinds[kinds.Count - 1];
                for (int i = 0; i < kinds.Count; i++)
                {
                    if (roll < w[i])
                    {
                        kind = kinds[i];
                        break;
                    }
                    roll -= w[i];
                }
                drawn[id] = kind;
                used.Add(kind);
            }
            return drawn;
        }
    }
}
