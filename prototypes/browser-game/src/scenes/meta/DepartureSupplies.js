// Port of unity-port/unity-project-kit/Assets/View/Depiction/DepartureSupplies.cs
//
// The tools and consumables the departure screen offers (#58), read from the dungeon side's
// catalogue (DungeonContent.ItemCatalogue, tools_and_consumables_v4.md) and handed to the Bridge as
// SupplyOptions. In Unity it lives in the View assembly because Depiction.Bridge does not reference
// DungeonContent; here it lives beside the scene that uses it, for the same reason.
import { ItemCatalogue } from '../../core/dungeonContent/ItemCatalogue.js';
import { ItemEffect, EffectSide } from '../../core/dungeonContent/ItemEffect.js';
import { Loadout } from '../../core/dungeonContent/Loadout.js';
import { DeckLimits } from '../../core/battle/DeckRules.js';
import { SupplyOption, DepartureBuilder } from '../../bridge/depiction/DepartureBuilder.js';

function AmountIf(item, effect) {
  return item.Effect === effect ? item.Amount : 0;
}

function Map(items) {
  const options = [];
  for (const item of items) options.push(DepartureSupplies.Of(item));
  return options;
}

export const DepartureSupplies = {
  /** The eight tools, in the catalogue's order. */
  get Tools() { return Map(ItemCatalogue.Tools); },

  /** The five consumables, in the catalogue's order. */
  get Consumables() { return Map(ItemCatalogue.Consumables); },

  /**
   * A departure builder over the whole catalogue. JS extension (docs/EXTENSIONS.md 「出立の枠（DeckLimits）」):
   * `limits` is the life's DeckLimits (4 tool slots with the survivor's bonus, concept-v3.md §14); left
   * out, the C#'s 3 + 3.
   */
  NewBuilder(limits = undefined) { return new DepartureBuilder(DepartureSupplies.Tools, DepartureSupplies.Consumables, limits); },

  /**
   * App-side (not in the C#; the departure scene's 出立する, kept here so it can be tested without a DOM):
   * what a built Departure hands on. `battleToolIds` are the tools that act inside a battle (Expedition's
   * battleToolIds); `loadout` is the whole choice as the DungeonContent Loadout with the life's slot count
   * (`limits.ToolSlots`: 4 with the survivor's bonus, concept-v3.md §14; DeckLimits.From reads null as the
   * C#'s 3). The router turns `loadout` into the exploration's RunLoadout with the WORLD job's
   * toRunLoadout(loadout, limits.ToolSlots) (src/bridge/exploration/Loadouts.js): a Loadout's tools are
   * catalogue ItemDefs and carry no relief Kind of their own.
   */
  Carry(departure, limits = undefined) {
    const slots = DeckLimits.From(limits).ToolSlots;
    const tools = [...departure.Tools];
    const consumables = [...departure.Consumables];
    const battleToolIds = tools.filter((id) => ItemCatalogue.Tool(id).Side === EffectSide.Battle);
    return { battleToolIds, consumableIds: consumables, loadout: Loadout.Of(tools, consumables, Math.max(slots, tools.length)) };
  },

  /**
   * One item as the battle reads it (tools_and_consumables_v4.md §2, battle_core_v4 §9.1), each by
   * its Amount: WiderStart moves the start back, OpeningStamina adds stamina at the start,
   * OpeningGuard is the Guard before the first enemy blow, BluntBossState the stacks the boss-only
   * words sit lower. The exploration effects do not reach the battle.
   */
  Of(item) {
    return new SupplyOption(item.Id, item.Name, item.Note,
      AmountIf(item, ItemEffect.WiderStart),
      AmountIf(item, ItemEffect.OpeningStamina),
      AmountIf(item, ItemEffect.OpeningGuard),
      AmountIf(item, ItemEffect.BluntBossState));
  },
};
