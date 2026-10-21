// JS extension (not in the C#; docs/EXTENSIONS.md 「探索の持ち物とイベント」). The C# leaves the step from the
// departure's choice to the exploration's loadout to #104 (DungeonContent/Loadout.cs: 「#104 is where a chosen
// Loadout becomes a RunLoadout」), and its RunLoadout holds only what relieves 瘴気. This is that step for the
// browser game: every tool and consumable the departure chose rides along as the core item that says what it
// does (src/bridge/explorationScript/TrialItems.js), so trial mode (RunStart.Trial / Expedition options.trial)
// can act on each (tools_and_consumables_v4.md §2〜§3).
import { ReliefItem, RunLoadout } from '../../core/dungeon/RunLoadout.js';
import { TrialItems } from '../explorationScript/TrialItems.js';

/**
 * The departure's Loadout (DungeonContent.Loadout: catalogue ItemDefs) as the RunLoadout the exploration
 * reads, with the trial items: all tools (the four battle tools as ReliefKind.Battle, carried for the slot
 * count and the inventory only — Expedition's battleToolIds is what the battles read) and all consumables.
 * `toolSlots` is the life's slot count (3, or 4 with the 生存者ボーナス); the loadout's own when omitted.
 *
 * Outside trial mode the core reads this RunLoadout exactly as the C# would: only the density tools lower
 * the density and only 浄化の香 moves the gauge.
 */
export function toRunLoadout(loadout, toolSlots = null) {
  if (loadout == null) return new RunLoadout(null, null, toolSlots ?? RunLoadout.ToolSlots, true);
  const tools = (loadout.Tools ?? []).map((def) => TrialItems.ReliefOf(def));
  const consumables = (loadout.Consumables ?? []).map((def) => TrialItems.ReliefOf(def));
  const slots = Math.max(toolSlots ?? loadout.ToolSlotCount ?? RunLoadout.ToolSlots, tools.length);
  return new RunLoadout(tools, consumables, slots, true);
}

/**
 * A RunLoadout as plain data for a save ({ tools: [[id, kind, amount]…], consumables: […], slots, trial }),
 * and back. The trial flag rides along, so a trial loadout's tools load again (life.js keeps its own copy of
 * the C# shape; these are the same rows plus `trial`).
 */
export function runLoadoutToData(runLoadout) {
  const row = (r) => [r.Id, r.Kind, r.Amount];
  return {
    tools: runLoadout.Tools.map(row),
    consumables: runLoadout.Consumables.map(row),
    slots: runLoadout.ToolSlotCount,
    trial: runLoadout.Trial === true,
  };
}

export function runLoadoutFromData(data) {
  const item = ([id, kind, amount]) => new ReliefItem(id, kind, amount);
  return new RunLoadout(data.tools.map(item), data.consumables.map(item), data.slots, data.trial === true);
}
