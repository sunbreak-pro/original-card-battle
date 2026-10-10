// The tools and consumables the departure screen offers (#58), read from the dungeon side's
// catalogue (DungeonContent.ItemCatalogue, tools_and_consumables_v4.md) and handed to the Bridge as
// SupplyOptions. It lives here, not in Depiction.Bridge, because the Bridge assembly does not
// reference DungeonContent; Assembly-CSharp references both.
//
// Pure C#, no UnityEngine and no #if: Depiction.Bridge.Tests compiles this file too, so the mapping
// to the battle's one wired effect — 間合いの履 moving the start back — is held under `dotnet test`.
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
        /// One item as the battle reads it: WiderStart moves the start back by its Amount
        /// (tools_and_consumables_v4.md §2.3); no other effect reaches the battle yet.
        /// </summary>
        public static SupplyOption Of(ItemDef item)
        {
            int back = item.Effect == ItemEffect.WiderStart ? item.Amount : 0;
            return new SupplyOption(item.Id, item.Name, item.Note, back);
        }

        private static IReadOnlyList<SupplyOption> Map(IReadOnlyList<ItemDef> items)
        {
            var options = new List<SupplyOption>();
            foreach (ItemDef item in items) options.Add(Of(item));
            return options;
        }
    }
}
