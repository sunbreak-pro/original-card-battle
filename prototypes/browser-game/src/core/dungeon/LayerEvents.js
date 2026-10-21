// JS extension (not in the C#; docs/EXTENSIONS.md 「探索の持ち物とイベント」): what an イベント node holds.
// Designed from encounters_and_rewards_v4.md §5 (the three kinds, their effects and their weights per
// layer) and dungeon_exploration_v4.md §2.1 (イベント 3 種). The C# DungeonCore stops at NodeKind.Event
// (「泉 / 重い宝箱 / 特殊な食事 のどれかが起きる」); trial mode (RunStart.Trial) draws the kind here and
// ExplorationReducer.ResolveEvent applies it. Like LayerEncounters, the draw is seeded from the node, so a
// life replays the same events on the same nodes.
import { defineEnum, sum } from '../../shared/cs.js';
import { argNull, argOutOfRange } from './_errors.js';
import { SeededRng, RngExtensions } from './IRng.js';
import { NodeKind } from './NodeKind.js';
import { SevenLayers } from './SevenLayers.js';

/**
 * The three events of the 80% build (encounters_and_rewards_v4.md §5).
 *
 * Spring       泉: HP back by a share of the maximum. No choice.
 * HeavyChest   重い宝箱: max stamina −2 for a card, a consumable and/or a page. 受ける / 受けない.
 * StrangeMeal  特殊な食事: max stamina +1 for 10% of the HP maximum. 受ける / 受けない.
 */
export const EventKind = defineEnum(['Spring', 'HeavyChest', 'StrangeMeal']);

// §5's weights in EventKind order (泉 / 重い宝箱 / 特殊な食事), layer 1 first. Layers 1〜3 and 7 are the
// table's. Layers 4〜6 hold no event node (seven_layers_v4.md §4); a map that gives them one (a JS map
// seam) draws from §5's own extension of the line: 泉 falls 10 a layer and 重い宝箱 takes it, 「泉は六層で
// 0 になり」 (試作で決めたこと).
const Weights = [
  [50, 20, 30],
  [40, 30, 30],
  [30, 40, 30],
  [20, 50, 30],
  [10, 60, 30],
  [0, 70, 30],
  [0, 70, 30],
];

// §5 泉: the share of the HP maximum, layer 1 first. Layer 7 never draws a spring (weight 0); layers 4〜6
// keep layer 3's 30% (試作で決めたこと: §5 gives no value there).
const SpringPercent = [20, 25, 30, 30, 30, 30, 30];

// §5 重い宝箱: what the chest holds, layer 1 first. 「カード 1 枚 + 消耗品 1 個 / カード 1 枚 + 頁 1 枚 /
// カード 1 枚 + 頁 1 枚 + 消耗品 1 個 / 三層と同じ」; layers 4〜6 hold layer 3's too (試作で決めたこと).
const ChestContents = [
  { Cards: 1, Consumables: 1, Pages: 0 },
  { Cards: 1, Consumables: 0, Pages: 1 },
  { Cards: 1, Consumables: 1, Pages: 1 },
  { Cards: 1, Consumables: 1, Pages: 1 },
  { Cards: 1, Consumables: 1, Pages: 1 },
  { Cards: 1, Consumables: 1, Pages: 1 },
  { Cards: 1, Consumables: 1, Pages: 1 },
];

function check(layer) {
  if (layer < 1 || layer > SevenLayers.Count) throw argOutOfRange('layer', layer, `the lair has ${SevenLayers.Count} layers`);
}

export const LayerEvents = {
  /** §5 重い宝箱: max stamina −2, a temporary change (miasma_and_battle_v4.md §5). */
  ChestStaminaCost: 2,

  /** §5 特殊な食事: max stamina +1, a temporary change. */
  MealStaminaGain: 1,

  /** §5 特殊な食事: 「代わりに HP が最大の 10% 減る」. */
  MealHpPercent: 10,

  /** §5's weights on a layer, in EventKind order. */
  Weights(layer) {
    check(layer);
    return Weights[layer - 1].slice();
  },

  /** The weight one kind has on a layer. */
  WeightOf(kind, layer) {
    check(layer);
    return Weights[layer - 1][EventKind.$order.indexOf(kind)];
  },

  /** §5 泉: the share of the HP maximum it gives back on a layer. */
  SpringPercent(layer) {
    check(layer);
    return SpringPercent[layer - 1];
  },

  /** §5 重い宝箱: { Cards, Consumables, Pages } it holds on a layer. */
  ChestContents(layer) {
    check(layer);
    return { ...ChestContents[layer - 1] };
  },

  /** §5: 「重い宝箱と特殊な食事は、踏んだ後に「受ける / 受けない」を選べます」. 泉 just happens. */
  HasChoice(kind) {
    return kind === EventKind.HeavyChest || kind === EventKind.StrangeMeal;
  },

  /**
   * The kind behind every event node of a layer's map (Map nodeId → EventKind). The nodes are drawn in
   * id order, each from its own seed (`seedOf(nodeId)`, a ulong), by §5's weights; a kind already drawn
   * on the layer is left out of the next node's draw — 「同じ層の 2 つは、同じ種類を重ねずに引きます」.
   * Should a layer ever hold more event nodes than kinds, the full table is drawn from again.
   * One NextInt per node.
   */
  Draw(map, layer, seedOf) {
    if (map === null || map === undefined) throw argNull('map');
    if (typeof seedOf !== 'function') throw argNull('seedOf');
    check(layer);
    const weights = Weights[layer - 1];
    const drawn = new Map();
    const used = new Set();
    const nodes = map.Nodes.filter((n) => n.Kind === NodeKind.Event).map((n) => n.Id).sort((a, b) => a - b);
    for (const id of nodes) {
      let kinds = EventKind.$order.filter((k, i) => weights[i] > 0 && !used.has(k));
      if (kinds.length === 0) kinds = EventKind.$order.filter((k, i) => weights[i] > 0);
      const w = kinds.map((k) => weights[EventKind.$order.indexOf(k)]);
      const rng = new SeededRng(seedOf(id));
      let roll = RngExtensions.NextInt(rng, 0, sum(w, (x) => x));
      let kind = kinds[kinds.length - 1];
      for (let i = 0; i < kinds.length; i++) {
        if (roll < w[i]) { kind = kinds[i]; break; }
        roll -= w[i];
      }
      drawn.set(id, kind);
      used.add(kind);
    }
    return drawn;
  },
};
