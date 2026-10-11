// The tools and consumables the departure screen offers (#58), read from the dungeon side's
// catalogue (DungeonContent.ItemCatalogue, tools_and_consumables_v4.md) and handed to the Bridge as
// SupplyOptions. It lives here, not in Depiction.Bridge, because the Bridge assembly does not
// reference DungeonContent; Assembly-CSharp references both.
//
// Pure C#, no UnityEngine and no #if: Depiction.Bridge.Tests compiles this file too, so the mapping
// to the battle's four wired effects (間合いの履, 息継ぎの笛, 竜鱗の欠片, 竜語の符) is held under
// `dotnet test`.
using System.Collections.Generic;
using Depiction.Bridge;
using DungeonContent;

namespace Depiction.View
{
    public static class DepartureSupplies
    {
        /// <summary>The eight tools, in the catalogue's order.</summary>
        public static IReadOnlyList<SupplyOption> Tools => Map(ItemCatalogue.Tools);

        /// <summary>The five consumables, in the catalogue's order.</summary>
        public static IReadOnlyList<SupplyOption> Consumables => Map(ItemCatalogue.Consumables);

        /// <summary>A departure builder over the whole catalogue.</summary>
        public static DepartureBuilder NewBuilder() => new DepartureBuilder(Tools, Consumables);

        /// <summary>
        /// One item as the battle reads it (tools_and_consumables_v4.md §2, battle_core_v4 §9.1), each by
        /// its Amount: WiderStart moves the start back, OpeningStamina adds stamina at the start,
        /// OpeningGuard is the Guard before the first enemy blow, BluntBossState the stacks the boss-only
        /// words sit lower. The exploration effects do not reach the battle.
        /// </summary>
        public static SupplyOption Of(ItemDef item)
        {
            return new SupplyOption(item.Id, item.Name, item.Note,
                startCellsBack: AmountIf(item, ItemEffect.WiderStart),
                startStaminaBonus: AmountIf(item, ItemEffect.OpeningStamina),
                firstHitGuard: AmountIf(item, ItemEffect.OpeningGuard),
                bossWordWard: AmountIf(item, ItemEffect.BluntBossState));
        }

        private static int AmountIf(ItemDef item, ItemEffect effect) => item.Effect == effect ? item.Amount : 0;

        private static IReadOnlyList<SupplyOption> Map(IReadOnlyList<ItemDef> items)
        {
            var options = new List<SupplyOption>();
            foreach (ItemDef item in items) options.Add(Of(item));
            return options;
        }
    }
}
