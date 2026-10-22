// Trial extension (port of the browser trial's src/bridge/exploration/Loadouts.js; docs/EXTENSIONS.md 「探索の持ち物と
// イベント」). The C# leaves the step from the departure's choice to the exploration's loadout to #104
// (DungeonContent/Loadout.cs: 「#104 is where a chosen Loadout becomes a RunLoadout」), and its RunLoadout holds only
// what relieves 瘴気. This is that step for the trial: every tool and consumable the departure chose rides along as the
// core item that says what it does (Exploration.TrialItems), so trial mode (RunStart.Trial / ExpeditionOptions.Trial)
// can act on each (tools_and_consumables_v4.md §2〜§3).
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonContent;
using DungeonCore;
using InheritanceCore;

namespace Exploration.Bridge
{
    /// <summary>js: bridge/exploration/Loadouts.js — the departure's Loadout as the exploration's RunLoadout, and as save data.</summary>
    public static class Loadouts
    {
        /// <summary>
        /// The departure's Loadout (catalogue ItemDefs) as the RunLoadout the exploration reads, with the trial items:
        /// all tools (the four battle tools as ReliefKind.Battle, carried for the slot count and the inventory only —
        /// Expedition's battleToolIds is what the battles read) and all consumables. <paramref name="toolSlots"/> is
        /// the life's slot count (3, or 4 with the 生存者ボーナス); the loadout's own when null. A null loadout is an
        /// empty trial RunLoadout.
        ///
        /// Outside trial mode the core reads this RunLoadout exactly as the C# would: only the density tools lower
        /// the density and only 浄化の香 moves the gauge.
        /// </summary>
        // js: bridge/exploration/Loadouts.js:19
        public static RunLoadout ToRunLoadout(Loadout loadout, int? toolSlots = null)
        {
            if (loadout == null) return new RunLoadout(null, null, toolSlots ?? RunLoadout.ToolSlots, true);
            var tools = loadout.Tools.Select(TrialItems.ReliefOf).ToList();
            var consumables = loadout.Consumables.Select(TrialItems.ReliefOf).ToList();
            int slots = Math.Max(toolSlots ?? loadout.ToolSlotCount, tools.Count);
            return new RunLoadout(tools, consumables, slots, true);
        }

        /// <summary>
        /// A RunLoadout as plain data for a save: <c>{ tools: [[id, kind, amount]…], consumables: […], slots, trial }</c>
        /// (the JS's keys and order; the kind is the ReliefKind's name). The trial flag rides along, so a trial
        /// loadout's tools load again.
        /// </summary>
        // js: bridge/exploration/Loadouts.js:32
        public static JsonObject RunLoadoutToData(RunLoadout runLoadout)
        {
            if (runLoadout == null) throw new ArgumentNullException(nameof(runLoadout));
            return new JsonObject
            {
                ["tools"] = runLoadout.Tools.Select(Row).ToList(),
                ["consumables"] = runLoadout.Consumables.Select(Row).ToList(),
                ["slots"] = runLoadout.ToolSlotCount,
                ["trial"] = runLoadout.Trial,
            };
        }

        /// <summary>
        /// A RunLoadout back from <see cref="RunLoadoutToData"/>'s data (or Json.Parse of it). A save without
        /// <c>trial</c> reads as the C#'s RunLoadout, and one without <c>slots</c> has the C#'s
        /// <see cref="RunLoadout.ToolSlots"/> (the JS hands <c>undefined</c> to the constructor, whose default applies).
        /// Throws ArgumentException on another shape.
        /// </summary>
        // js: bridge/exploration/Loadouts.js:42
        public static RunLoadout RunLoadoutFromData(object data)
        {
            if (!(data is JsonObject o)) throw new ArgumentException("Loadouts: a loadout is a JSON object", nameof(data));
            int slots = o.ContainsKey("slots") ? IntOf(o["slots"], "slots") : RunLoadout.ToolSlots;
            return new RunLoadout(Items(o["tools"], "tools"), Items(o["consumables"], "consumables"), slots, Equals(o["trial"], true));
        }

        private static object Row(ReliefItem item) => new List<object> { item.Id, item.Kind.ToString(), item.Amount };

        private static List<ReliefItem> Items(object list, string key)
        {
            if (!(list is IEnumerable<object> rows)) throw new ArgumentException($"Loadouts: {key} is a list", nameof(list));
            var items = new List<ReliefItem>();
            foreach (object row in rows)
            {
                if (!(row is IList<object> cells) || cells.Count < 3 || !(cells[0] is string id))
                    throw new ArgumentException($"Loadouts: a row of {key} is [id, kind, amount]", nameof(list));
                items.Add(new ReliefItem(id, KindOf(cells[1], key), IntOf(cells[2], key)));
            }
            return items;
        }

        private static ReliefKind KindOf(object value, string key)
        {
            if (value is string name && Enum.IsDefined(typeof(ReliefKind), name)) return (ReliefKind)Enum.Parse(typeof(ReliefKind), name);
            throw new ArgumentException($"Loadouts: an unknown kind in {key}", nameof(value));
        }

        private static int IntOf(object value, string key)
        {
            if (!Json.IsInteger(value)) throw new ArgumentException($"Loadouts: {key} needs a whole number", nameof(value));
            return (int)Json.ToDouble(value);
        }
    }
}
