// The tools and consumables the departure screen offers, and what a built Departure hands on (port of the browser
// trial's src/scenes/meta/DepartureSupplies.js, itself a port of View/Depiction/DepartureSupplies.cs plus the trial's
// NewBuilder(limits) and Carry). The Unity View's DepartureSupplies lives in Assembly-CSharp, which no Trial assembly
// can reference, and adding the limits there would put a BattleCore type into a View folder (the View rules boundary);
// so the trial's copy lives here, beside the other seams, and the View's file is left as it is.
//
// Engine-free (no UnityEngine).
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction.Bridge;
using DungeonContent;

namespace Trial.Bridge
{
    /// <summary>
    /// What a built Departure hands on (js: DepartureSupplies.Carry's <c>{ battleToolIds, consumableIds, loadout }</c>): the
    /// tools that act inside a battle (Expedition's battleToolIds), the consumables, and the whole choice as the
    /// DungeonContent Loadout with the life's slot count.
    /// </summary>
    public sealed class CarriedSupplies
    {
        public CarriedSupplies(IReadOnlyList<string> battleToolIds, IReadOnlyList<string> consumableIds, Loadout loadout)
        {
            BattleToolIds = battleToolIds ?? Array.Empty<string>();
            ConsumableIds = consumableIds ?? Array.Empty<string>();
            Loadout = loadout;
        }

        public IReadOnlyList<string> BattleToolIds { get; }

        public IReadOnlyList<string> ConsumableIds { get; }

        public Loadout Loadout { get; }
    }

    public static class DepartureCarry
    {
        // js: scenes/meta/DepartureSupplies.js:13
        private static int AmountIf(ItemDef item, ItemEffect effect) => item.Effect == effect ? item.Amount : 0;

        // js: scenes/meta/DepartureSupplies.js:17
        private static IReadOnlyList<SupplyOption> Map(IReadOnlyList<ItemDef> items)
        {
            var options = new List<SupplyOption>();
            foreach (ItemDef item in items) options.Add(Of(item));
            return options;
        }

        /// <summary>The eight tools, in the catalogue's order.</summary>
        // js: scenes/meta/DepartureSupplies.js:25
        public static IReadOnlyList<SupplyOption> Tools => Map(ItemCatalogue.Tools);

        /// <summary>The five consumables, in the catalogue's order.</summary>
        // js: scenes/meta/DepartureSupplies.js:28
        public static IReadOnlyList<SupplyOption> Consumables => Map(ItemCatalogue.Consumables);

        /// <summary>
        /// A departure builder over the whole catalogue. Trial extension (docs/EXTENSIONS.md 「出立の枠（DeckLimits）」):
        /// <paramref name="limits"/> is the life's DeckLimits (4 tool slots with the survivor's bonus, concept-v3.md §14);
        /// null: the C#'s 3 + 3.
        /// </summary>
        // js: scenes/meta/DepartureSupplies.js:35
        public static DepartureBuilder NewBuilder(DeckLimits limits = null) => new DepartureBuilder(Tools, Consumables, limits);

        /// <summary>
        /// App-side (not in the C#; the departure scene's 出立する): what a built Departure hands on. BattleToolIds are the
        /// tools that act inside a battle (Expedition's battleToolIds); Loadout is the whole choice as the DungeonContent
        /// Loadout with the life's slot count (<paramref name="limits"/>.ToolSlots: 4 with the survivor's bonus,
        /// concept-v3.md §14; null reads as the C#'s 3), never fewer than the tools carried. The router turns Loadout into
        /// the exploration's RunLoadout with Loadouts.ToRunLoadout(loadout, limits.ToolSlots) (Life.ChoiceToData): a
        /// Loadout's tools are catalogue ItemDefs and carry no relief Kind of their own.
        /// </summary>
        // js: scenes/meta/DepartureSupplies.js:46
        public static CarriedSupplies Carry(Departure departure, DeckLimits limits = null)
        {
            if (departure == null) throw new ArgumentNullException(nameof(departure));
            int slots = DeckLimits.From(limits).ToolSlots;
            List<string> tools = departure.Tools.ToList();
            List<string> consumables = departure.Consumables.ToList();
            List<string> battleToolIds = tools.Where(id => ItemCatalogue.Tool(id).Side == EffectSide.Battle).ToList();
            return new CarriedSupplies(battleToolIds, consumables, Loadout.Of(tools, consumables, Math.Max(slots, tools.Count)));
        }

        /// <summary>
        /// One item as the battle reads it (tools_and_consumables_v4.md §2, battle_core_v4 §9.1), each by its Amount:
        /// WiderStart moves the start back, OpeningStamina adds stamina at the start, OpeningGuard is the Guard before the
        /// first enemy blow, BluntBossState the stacks the boss-only words sit lower. The exploration effects do not reach
        /// the battle.
        /// </summary>
        // js: scenes/meta/DepartureSupplies.js:60
        public static SupplyOption Of(ItemDef item)
        {
            return new SupplyOption(item.Id, item.Name, item.Note,
                AmountIf(item, ItemEffect.WiderStart),
                AmountIf(item, ItemEffect.OpeningStamina),
                AmountIf(item, ItemEffect.OpeningGuard),
                AmountIf(item, ItemEffect.BluntBossState));
        }
    }
}
