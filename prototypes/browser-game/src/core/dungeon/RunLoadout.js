// Port of unity-port/DungeonCore/RunLoadout.cs
import { defineEnum, defineRecord, sum } from '../../shared/cs.js';
import { argException, argOutOfRange } from './_errors.js';

/**
 * What a relief carried into the dungeon actually does. The exploration core only needs
 * the shape; which items exist and what they are called is #100's job.
 *
 * Density  Lowers the layer's density for as long as the life lasts (防瘴の面).
 * Gauge    Pulls the gauge back down once, then the item is gone (浄化の香).
 */
export const ReliefKind = defineEnum([
  'Density', 'Gauge',
  // JS extension (trial mode, docs/EXTENSIONS.md 「探索の持ち物とイベント」): the rest of the departure's
  // items, so the exploration can carry every tool and consumable of tools_and_consumables_v4.md §2 / §3.
  // Only a trial RunLoadout holds tools of these kinds; the C# shape (Density tools, any consumable) is
  // unchanged.
  'Time', // 刻限の砂: every layer's 刻限 + Amount (§2)
  'Survey', // 写しの帳: the player picks the 情報収集 target (§2.2)
  'Interlude', // 灯芯の欠片: 階層間の休憩 heals Amount more points of the maximum (§2)
  'Battle', // 間合いの履 / 息継ぎの笛 / 竜鱗の欠片 / 竜語の符: acts in battle only; carried for the slot count
  'Heal', // 薬草: HP back by Amount% of the maximum (§3)
  'MaxStamina', // 濃い干し肉: max stamina + Amount, a temporary change inside ±4 (§3)
  'Refill', // 忘れ水: current stamina full (§3)
  'Teleport', // 標の石: jump to an unresolved node of the layer, paying its 刻限 (§3)
]);

/** The tool kinds a trial RunLoadout may hold (ReliefKind's JS extension). */
const TrialToolKinds = new Set([ReliefKind.Density, ReliefKind.Time, ReliefKind.Survey, ReliefKind.Interlude, ReliefKind.Battle]);

/** One carried item, reduced to its effect on 瘴気. Name and art live elsewhere. */
export class ReliefItem extends defineRecord('ReliefItem', ['Id', 'Kind', 'Amount']) {
  static Density(id, amount) { return new ReliefItem(id, ReliefKind.Density, amount); }

  static Gauge(id, amount) { return new ReliefItem(id, ReliefKind.Gauge, amount); }

  /** The compiler-generated record ToString. */
  ToString() { return `ReliefItem { Id = ${this.Id}, Kind = ${this.Kind}, Amount = ${this.Amount} }`; }

  toString() { return this.ToString(); }
}

/**
 * What the explorer set out with. Three tool slots and three consumable slots
 * (concept-v3.md §14); the 生存者ボーナス raises the tool slots to four for one life (§8.2).
 *
 * Tools stay for the whole life. Consumables are spent where they are used and are not
 * carried back to camp.
 */
export class RunLoadout {
  static ToolSlots = 3;
  static ConsumableSlots = 3;
  static SurvivorBonusToolSlots = RunLoadout.ToolSlots + 1;

  /**
   * @param {ReliefItem[]|null} [tools]
   * @param {ReliefItem[]|null} [consumables]
   * @param {number} [toolSlots]
   * @param {boolean} [trial] JS extension (not in the C#): a trial loadout may also carry the tools of
   *   kind Time / Survey / Interlude / Battle (src/bridge/exploration/Loadouts.js toRunLoadout). False
   *   keeps the C# rule that every tool lowers density.
   */
  constructor(tools = null, consumables = null, toolSlots = RunLoadout.ToolSlots, trial = false) {
    const toolList = tools ?? [];
    const consumableList = consumables ?? [];

    if (toolSlots < 0) throw argOutOfRange('toolSlots');
    if (toolList.length > toolSlots)
      throw argException(`${toolList.length} tools do not fit in ${toolSlots} slots`, 'tools');
    if (consumableList.length > RunLoadout.ConsumableSlots)
      throw argException(
        `${consumableList.length} consumables do not fit in ${RunLoadout.ConsumableSlots} slots`, 'consumables');
    if (!trial && toolList.some((t) => t.Kind !== ReliefKind.Density))
      throw argException('a tool thins the air for the whole life, so it lowers density', 'tools');
    if (trial && toolList.some((t) => !TrialToolKinds.has(t.Kind)))
      throw argException('a trial tool is one of the eight tools of tools_and_consumables_v4.md §2', 'tools');

    // C# keeps the tools list it was handed (no copy) and copies the consumables.
    this.Tools = toolList;
    this.ToolSlotCount = toolSlots;
    this._consumables = consumableList.slice();
    /** JS extension: this loadout carries the trial items (see the constructor). */
    this.Trial = trial;
  }

  get Consumables() { return this._consumables; }

  /**
   * How much every layer's density drops for this life. A C# loadout holds density tools only, so
   * its sum is over every tool; a trial loadout counts its density tools.
   */
  get DensityRelief() {
    return this.Trial ? this.SumOf(ReliefKind.Density) : sum(this.Tools, (t) => t.Amount);
  }

  // ---- JS extension (trial mode): what the other exploration tools add (tools_and_consumables_v4.md §2).

  /** 刻限の砂: 刻限 every layer gains. Copies stack, like Loadout.TimeLimitBonus. 0 outside trial. */
  get TimeBonus() { return this.Trial ? this.SumOf(ReliefKind.Time) : 0; }

  /** 灯芯の欠片: points of the HP maximum 階層間の休憩 heals on top of its 30%. 0 outside trial. */
  get InterludeBonus() { return this.Trial ? this.SumOf(ReliefKind.Interlude) : 0; }

  /** 写しの帳 is carried: the player picks the 情報収集 target (§2.2). False outside trial. */
  get ChoosesSurvey() { return this.Trial && this.Tools.some((t) => t.Kind === ReliefKind.Survey); }

  /** The tools of one kind summed. (JS extension.) */
  SumOf(kind) { return sum(this.Tools.filter((t) => t.Kind === kind), (t) => t.Amount); }

  /** A consumable slot can take one more item. (JS extension.) */
  get HasFreeSlot() { return this._consumables.length < RunLoadout.ConsumableSlots; }

  /**
   * JS extension (trial mode): picks a consumable up (encounters_and_rewards_v4.md §4.3,
   * tools_and_consumables_v4.md §3: 「枠が埋まっているときは、その場で入れ替えるか諦めます」). With a free
   * slot it goes in after the others; with none it replaces the item in `replaceSlot`. The same rule
   * as DungeonContent.Loadout.Take, on the core's items.
   */
  Take(item, replaceSlot = -1) {
    if (item === null || item === undefined) throw argException('a consumable to take', 'item');
    const next = this._consumables.slice();
    if (next.length < RunLoadout.ConsumableSlots) next.push(item);
    else if (replaceSlot < 0 || replaceSlot >= next.length) throw argOutOfRange('replaceSlot', replaceSlot, 'the consumable slots are full; name the slot to swap');
    else next[replaceSlot] = item;
    return new RunLoadout(this.Tools, next, this.ToolSlotCount, this.Trial);
  }

  /**
   * Spends one consumable and reports what it pulls off the gauge.
   * C# `RunLoadout Spend(int index, out int gaugeRelief)` → returns `{ value, gaugeRelief }`,
   * `value` being the C# return value. (JS extension: `item` is the item spent, for the trial effects.)
   */
  Spend(index) {
    if (index < 0 || index >= this._consumables.length)
      throw argOutOfRange('index', index, 'no consumable in that slot');

    const item = this._consumables[index];
    const gaugeRelief = item.Kind === ReliefKind.Gauge ? item.Amount : 0;

    const rest = this._consumables.slice();
    rest.splice(index, 1);
    return { value: new RunLoadout(this.Tools, rest, this.ToolSlotCount, this.Trial), gaugeRelief, item };
  }

  /** Public properties, in declaration order, for the parity JSON. */
  toJSON() {
    return {
      $type: 'RunLoadout',
      Tools: this.Tools,
      ToolSlotCount: this.ToolSlotCount,
      Consumables: this.Consumables,
      DensityRelief: this.DensityRelief,
    };
  }
}

/** Nothing carried. The baseline a bare run starts from. (C# get-only static property.) */
Object.defineProperty(RunLoadout, 'Empty', { value: new RunLoadout(), enumerable: true });
