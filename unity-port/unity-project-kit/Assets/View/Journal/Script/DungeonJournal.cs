// The dungeon pages of the journal (#107): what the journal can learn of each layer beyond its numbers
// — 近道, 条件出現 and the events (concept-v3.md §5, dungeon_exploration_v4.md §6.11). Each entry has its
// own 開示度: 0 until it happens to this character, 1 once it has (the line of what happened), 2 once a
// rare page of the layer tells it in full (the line of how often or on what condition).
//
// Only the events have content in the 80% version: encounters_and_rewards_v4.md §5 puts two on each of
// layers 1〜3 and one on layer 7, and says an event that happened is kept here as 「条件がそろうと現れる
// 出来事」. 近道 waits for layers of ten nodes or more and 条件出現の敵 for content that does not exist
// yet (dungeon_exploration_v4.md §2.2); the kinds are here so their entries need no new shape.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Journal
{
    /// <summary>What a dungeon entry is about (concept-v3.md §5's ダンジョンの頁).</summary>
    public enum DungeonEntryKind
    {
        /// <summary>近道 / ショートカット. No entry yet (§2.2).</summary>
        Shortcut,

        /// <summary>条件出現: something that appears only when its condition holds. No entry yet (§2.2).</summary>
        ConditionalAppearance,

        /// <summary>イベント: 泉 / 重い宝箱 / 特殊な食事 (encounters_and_rewards_v4.md §5).</summary>
        Event,
    }

    /// <summary>One thing the journal can learn of a layer.</summary>
    public sealed class DungeonEntry
    {
        public DungeonEntry(string id, DungeonEntryKind kind, int layer, string name, string met, string full)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("an entry needs an id", nameof(id));
            if (layer < 1) throw new ArgumentOutOfRangeException(nameof(layer), layer, "layers start at 1");
            Id = id;
            Kind = kind;
            Layer = layer;
            Name = name ?? "";
            Met = met ?? "";
            Full = full ?? "";
        }

        public string Id { get; }

        public DungeonEntryKind Kind { get; }

        public int Layer { get; }

        public string Name { get; }

        /// <summary>The line 開示度 1 opens: what happened when the character met it.</summary>
        public string Met { get; }

        /// <summary>The line 開示度 2 adds: how often, or on what condition, it comes.</summary>
        public string Full { get; }
    }

    public static class DungeonJournal
    {
        /// <summary>The entry happened to this character on its layer.</summary>
        public const int MetDisclosure = 1;

        /// <summary>A rare page of the layer told it in full.</summary>
        public const int FullDisclosure = 2;

        /// <summary>A dungeon page's id: the same as the exploration screen's page of the layer.</summary>
        public static string PageId(int layer) => $"dungeon-{layer}";

        public static string KindLabel(DungeonEntryKind kind) => kind switch
        {
            DungeonEntryKind.Shortcut => "近道",
            DungeonEntryKind.ConditionalAppearance => "条件出現",
            DungeonEntryKind.Event => "イベント",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        /// <summary>
        /// Every entry, layer by layer. The events are encounters_and_rewards_v4.md §5's table: the 泉
        /// is not drawn on layer 7 (weight 0), so it has no entry there; layers 4〜6 hold no event.
        /// </summary>
        public static readonly IReadOnlyList<DungeonEntry> All = new[]
        {
            Spring(1, 20, 50),
            HeavyChest(1, "カード 1 枚と消耗品 1 個", 20),
            StrangeMeal(1, 30),
            Spring(2, 25, 40),
            HeavyChest(2, "カード 1 枚と頁 1 枚", 30),
            StrangeMeal(2, 30),
            Spring(3, 30, 30),
            HeavyChest(3, "カード 1 枚と頁 1 枚と消耗品 1 個", 40),
            StrangeMeal(3, 30),
            HeavyChest(7, "カード 1 枚と頁 1 枚と消耗品 1 個", 70),
            StrangeMeal(7, 30),
        };

        private static readonly Dictionary<string, DungeonEntry> ById = All.ToDictionary(e => e.Id, StringComparer.Ordinal);

        /// <summary>The entry with this id, or null.</summary>
        public static DungeonEntry Find(string id) =>
            id != null && ById.TryGetValue(id, out DungeonEntry entry) ? entry : null;

        /// <summary>The entries of one layer, in the catalogue's order. Empty for a layer with none.</summary>
        public static IReadOnlyList<DungeonEntry> OfLayer(int layer) => All.Where(e => e.Layer == layer).ToList();

        /// <summary>The event entry of this kind on this layer (「spring」「heavy_chest」「strange_meal」), or null.</summary>
        public static DungeonEntry Event(int layer, string eventKind) => Find(EventId(layer, eventKind));

        public static string EventId(int layer, string eventKind) => $"event-{layer}-{eventKind}";

        /// <summary>
        /// The rows a layer's page shows for its entries: a heading per kind present, then each entry
        /// with the lines its 開示度 opens. An entry at 0 reads 「？」, so the page shows that something is
        /// there to learn. Empty for a layer with no entry.
        /// </summary>
        public static IReadOnlyList<JournalRow> Rows(int layer, Func<string, int> levelOf)
        {
            if (levelOf == null) throw new ArgumentNullException(nameof(levelOf));
            var rows = new List<JournalRow>();
            foreach (var group in OfLayer(layer).GroupBy(e => e.Kind))
            {
                rows.Add(new JournalRow($"【{KindLabel(group.Key)}】", ""));
                rows.AddRange(group.Select(e => new JournalRow(e.Name, Text(e, levelOf(e.Id)))));
            }
            return rows;
        }

        /// <summary>What an entry's row says at this 開示度.</summary>
        public static string Text(DungeonEntry entry, int level)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (level >= FullDisclosure) return entry.Met + "\n" + entry.Full;
            if (level >= MetDisclosure) return entry.Met;
            return JournalRow.Hidden;
        }

        private const string Choose = "受けるかどうかを選べる";

        /// <summary>The one layer that holds a single event (seven_layers_v4.md §4.1); layers 1〜3 hold two.</summary>
        private const int SingleEventLayer = 7;

        private static string Odds(int layer, string name, int weight) => layer == SingleEventLayer
            ? $"この層のイベントは 1 つで、{name}になる見込みは {weight}%"
            : $"この層のイベントは 2 つで、1 つ目が{name}になる見込みは {weight}%。2 つ目は 1 つ目と違う種類になる";

        private static DungeonEntry Spring(int layer, int percent, int weight) =>
            new DungeonEntry(EventId(layer, "spring"), DungeonEntryKind.Event, layer, "泉",
                $"HP が最大の {percent}% 戻る", Odds(layer, "泉", weight));

        private static DungeonEntry HeavyChest(int layer, string contents, int weight) =>
            new DungeonEntry(EventId(layer, "heavy_chest"), DungeonEntryKind.Event, layer, "重い宝箱",
                $"最大スタミナ -2。中身は{contents}。{Choose}", Odds(layer, "重い宝箱", weight));

        private static DungeonEntry StrangeMeal(int layer, int weight) =>
            new DungeonEntry(EventId(layer, "strange_meal"), DungeonEntryKind.Event, layer, "特殊な食事",
                $"最大スタミナ +1。代わりに HP が最大の 10% 減る。{Choose}", Odds(layer, "特殊な食事", weight));
    }
}
