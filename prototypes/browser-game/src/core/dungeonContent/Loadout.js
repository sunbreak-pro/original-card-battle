// Port of unity-port/DungeonContent/Loadout.cs
import { sum } from '../../shared/cs.js';
import { argNull, argOutOfRange } from '../dungeon/_errors.js';
import { ItemCatalogue } from './ItemCatalogue.js';
import { ItemEffect } from './ItemEffect.js';

/** Raised when a loadout breaks one of the rules the 出立 screen has to enforce. */
export class LoadoutRuleException extends Error {
  constructor(message) {
    super(message);
    this.name = 'LoadoutRuleException';
  }
}

/**
 * What was chosen at 出立, and what changes during a run. Three tool slots and three
 * consumable slots, common to exploration and battle (concept-v3.md §14). The 生存者
 * ボーナス raises the tool slots to four for one life (§8.2).
 *
 * Immutable: picking something up returns a new loadout, so a run can be replayed.
 *
 * The exploration core does not carry this type: it carries DungeonCore.RunLoadout, which holds
 * only what the core acts on. The rules kept here and nowhere else — ids checked against the
 * catalogue, picking up and swapping on the way, dropping consumables on leaving — belong to the
 * 出立 screen and the run around it. #104 is where a chosen Loadout becomes a RunLoadout; until
 * then only the tests build one (#517).
 */
export class Loadout {
  static ToolSlots = 3;
  static ConsumableSlots = 3;
  static SurvivorToolSlots = Loadout.ToolSlots + 1;

  /**
   * C# private constructor: build one with Loadout.Of / Loadout.Empty.
   * @param {import('./ItemCatalogue.js').ItemDef[]} tools
   * @param {import('./ItemCatalogue.js').ItemDef[]} consumables
   * @param {number} toolSlots
   */
  constructor(tools, consumables, toolSlots) {
    this._tools = tools;
    this._consumables = consumables;
    this.ToolSlotCount = toolSlots;
  }

  get Tools() { return this._tools; }

  get Consumables() { return this._consumables; }

  /**
   * Builds a loadout from catalogue ids. **The same tool may be taken twice**: the slot
   * count is the constraint, and no canon rule forbids a second copy. That is what makes
   * the survivor's fourth slot able to hold a second 防瘴の面 and reach the density
   * relief of 2 that seven_layers_v4.md §3.3 counts on (Issue #100 decision).
   */
  static Of(toolIds, consumableIds = null, toolSlots = Loadout.ToolSlots) {
    if (toolIds === null || toolIds === undefined) throw argNull('toolIds');
    if (toolSlots < 0) throw argOutOfRange('toolSlots');

    const consumables = consumableIds ?? [];
    if (toolIds.length > toolSlots)
      throw new LoadoutRuleException(`${toolIds.length} 個のツールは ${toolSlots} 枠に入りません`);
    if (consumables.length > Loadout.ConsumableSlots)
      throw new LoadoutRuleException(`${consumables.length} 個の消耗品は ${Loadout.ConsumableSlots} 枠に入りません`);

    return new Loadout(
      toolIds.map((id) => ItemCatalogue.Tool(id)),
      consumables.map((id) => ItemCatalogue.Consumable(id)),
      toolSlots);
  }

  /** How much every layer's 瘴気 density drops. Tools stack. */
  get MiasmaDensityRelief() {
    return sum(this._tools.filter((t) => t.Effect === ItemEffect.MiasmaDensity), (t) => t.Amount);
  }

  /** How much 刻限 every layer gains. */
  get TimeLimitBonus() {
    return sum(this._tools.filter((t) => t.Effect === ItemEffect.TimeLimit), (t) => t.Amount);
  }

  Has(effect) { return this._tools.some((t) => t.Effect === effect); }

  /**
   * Picks something up. With a free slot it goes in; with none, the player swaps it for
   * one they are carrying. Consumables are never carried back to camp, so there is no
   * storage to fall back on (concept-v3.md §14).
   */
  Take(consumableId, replaceSlot = -1) {
    const item = ItemCatalogue.Consumable(consumableId);
    const next = this._consumables.slice();

    if (next.length < Loadout.ConsumableSlots) {
      next.push(item);
      return new Loadout(this._tools, next, this.ToolSlotCount);
    }

    if (replaceSlot < 0 || replaceSlot >= next.length)
      throw new LoadoutRuleException('消耗品の枠が埋まっています。入れ替える枠を指定してください');

    next[replaceSlot] = item;
    return new Loadout(this._tools, next, this.ToolSlotCount);
  }

  /**
   * Uses a consumable up and reports what it was.
   * C# `Loadout Use(int slot, out ItemDef used)` → returns `{ value, used }`, `value` being the
   * C# return value.
   */
  Use(slot) {
    if (slot < 0 || slot >= this._consumables.length)
      throw new LoadoutRuleException('その枠に消耗品がありません');

    const used = this._consumables[slot];
    const next = this._consumables.slice();
    next.splice(slot, 1);
    return { value: new Loadout(this._tools, next, this.ToolSlotCount), used };
  }

  /** Closing a life keeps nothing consumable (concept-v3.md §14). */
  LeaveTheDungeon() {
    return new Loadout(this._tools, [], this.ToolSlotCount);
  }

  /** Public properties, in declaration order, for the parity JSON. */
  toJSON() {
    return {
      $type: 'Loadout',
      Tools: this.Tools,
      Consumables: this.Consumables,
      ToolSlotCount: this.ToolSlotCount,
      MiasmaDensityRelief: this.MiasmaDensityRelief,
      TimeLimitBonus: this.TimeLimitBonus,
    };
  }
}

/** Nothing carried. What a bare run sets out with. (C# get-only static property.) */
Object.defineProperty(Loadout, 'Empty', { value: new Loadout([], [], Loadout.ToolSlots), enumerable: true });
