// Port of unity-port/DungeonCore/LayerMapSpec.cs
import { sum } from '../../shared/cs.js';
import { argException, argNull, argOutOfRange } from './_errors.js';
import { NodeKind, NodeKindTokens } from './NodeKind.js';

/** IReadOnlyDictionary<NodeKind, int> handed in as a Map (a plain object is read in key order). */
function asQuotaMap(quotas) {
  if (quotas instanceof Map) return quotas;
  return new Map(Object.entries(quotas));
}

/**
 * The entry table of a C# Dictionary<TKey, TValue>, so enumeration order matches .NET (and Mono):
 * Remove frees an entry slot, and the next Add reuses the most recently freed slot (LIFO free
 * list) instead of appending. A JS Map would always append, so `With` — the only place the C#
 * removes and then adds — would enumerate differently.
 */
class DictionaryEntries {
  constructor(source) {
    this.keys = [];
    this.values = [];
    this.used = [];
    this.free = [];
    for (const [k, v] of source) {
      this.keys.push(k);
      this.values.push(v);
      this.used.push(true);
    }
  }

  indexOf(key) {
    for (let i = 0; i < this.keys.length; i++) if (this.used[i] && this.keys[i] === key) return i;
    return -1;
  }

  tryGetValue(key) {
    const i = this.indexOf(key);
    return i < 0 ? { ok: false, value: 0 } : { ok: true, value: this.values[i] };
  }

  get(key) {
    return this.values[this.indexOf(key)];
  }

  /** dictionary[key] = value */
  set(key, value) {
    const i = this.indexOf(key);
    if (i >= 0) {
      this.values[i] = value;
      return;
    }
    if (this.free.length > 0) {
      const slot = this.free.pop();
      this.keys[slot] = key;
      this.values[slot] = value;
      this.used[slot] = true;
      return;
    }
    this.keys.push(key);
    this.values.push(value);
    this.used.push(true);
  }

  remove(key) {
    const i = this.indexOf(key);
    if (i < 0) return false;
    this.used[i] = false;
    this.free.push(i);
    return true;
  }

  toMap() {
    const m = new Map();
    for (let i = 0; i < this.keys.length; i++) if (this.used[i]) m.set(this.keys[i], this.values[i]);
    return m;
  }
}

/**
 * The shape a layer's map must take. The generator fills this in with an IRng;
 * the spec itself carries no randomness, so a spec plus a seed names exactly one map.
 *
 * Rows run from the entry down to the boss. The first and last row hold one node each:
 * the entry is always a battle and the last row is always the layer boss
 * (dungeon_exploration_v4.md §4.1).
 */
export class LayerMapSpec {
  static DefaultExtraEdgeChance = 0.5;

  /**
   * @param {number} layer
   * @param {number[]} rowWidths
   * @param {Map<string, number>} quotas NodeKind → count
   * @param {number} [extraEdgeChance]
   */
  constructor(layer, rowWidths, quotas, extraEdgeChance = LayerMapSpec.DefaultExtraEdgeChance) {
    if (rowWidths === null || rowWidths === undefined) throw argNull('rowWidths');
    if (quotas === null || quotas === undefined) throw argNull('quotas');
    quotas = asQuotaMap(quotas);
    if (rowWidths.length < 3)
      throw argException('a layer needs an entry row, at least one middle row and a boss row', 'rowWidths');
    if (rowWidths[0] !== 1)
      throw argException('the entry row holds exactly one node', 'rowWidths');
    if (rowWidths[rowWidths.length - 1] !== 1)
      throw argException('the boss row holds exactly one node', 'rowWidths');
    if (rowWidths.some((w) => w < 1))
      throw argException('every row holds at least one node', 'rowWidths');
    if (quotas.has(NodeKind.Boss))
      throw argException('the boss is placed by the generator, not by a quota', 'quotas');
    if ([...quotas.values()].some((v) => v < 0))
      throw argException('a quota cannot be negative', 'quotas');
    if (extraEdgeChance < 0.0 || extraEdgeChance > 1.0)
      throw argOutOfRange('extraEdgeChance', extraEdgeChance, 'chance runs 0..1');

    this.Layer = layer;
    this.RowWidths = rowWidths;
    this.Quotas = quotas;
    /** Odds that a row gains one extra forward edge, which is what makes a fork. */
    this.ExtraEdgeChance = extraEdgeChance;

    this.NodeCount = sum(rowWidths);
    this.MiddleNodeCount = this.NodeCount - 2;

    const quotaTotal = sum([...quotas.values()]);
    if (quotaTotal !== this.MiddleNodeCount)
      throw argException(
        `quotas total ${quotaTotal} but the map has ${this.MiddleNodeCount} middle nodes`, 'quotas');

    const middleRows = rowWidths.length - 2;
    const detourTotal = sum([...quotas].filter(([k]) => NodeKindTokens.IsDetour(k)), ([, v]) => v);
    const detourCapacity = sum(rowWidths.slice(1, 1 + middleRows), (w) => w - 1);
    if (detourTotal > detourCapacity)
      throw argException(
        `${detourTotal} avoidable nodes do not fit: every row must keep one node that is not a detour, ` +
        `which leaves room for ${detourCapacity}`, 'quotas');

    const plainTotal = this.MiddleNodeCount - detourTotal;
    if (plainTotal < middleRows)
      throw argException(
        `${plainTotal} non-detour nodes cannot cover ${middleRows} middle rows`, 'quotas');
  }

  get RowCount() { return this.RowWidths.length; }

  /** 刻限 to reach the boss without detouring: one node per row. */
  get ShortestPathCost() { return this.RowCount; }

  /** 刻限 to resolve every node. Backtracking over resolved nodes is free (§4.2). */
  get FullSweepCost() { return this.NodeCount; }

  /**
   * Copy of this spec with one node of `kind` swapped in for a battle.
   * (C# quirk kept: With(NodeKind.Battle, n) gives the battle count back unchanged.)
   */
  With(kind, count) {
    const next = new DictionaryEntries(this.Quotas);
    const had = next.tryGetValue(kind);
    const current = had.ok ? had.value : 0;
    const delta = count - current;
    next.set(kind, count);
    if (next.get(kind) === 0) next.remove(kind);

    const b = next.tryGetValue(NodeKind.Battle);
    const battles = b.ok ? b.value : 0;
    if (battles - delta < 0)
      throw argException(`not enough battle nodes to trade for ${count} × ${NodeKindTokens.ToToken(kind)}`, 'count');
    next.set(NodeKind.Battle, battles - delta);
    if (next.get(NodeKind.Battle) === 0) next.remove(NodeKind.Battle);

    return new LayerMapSpec(this.Layer, this.RowWidths, next.toMap(), this.ExtraEdgeChance);
  }

  /**
   * The smallest layer that still holds the whole loop, from dungeon_exploration_v4.md §4.1:
   * six nodes, two forks, entry battle, boss exit. 刻限 for it is 6 (§3.3).
   */
  static Minimal(layer = 1) {
    return new LayerMapSpec(
      layer,
      [1, 2, 2, 1],
      new Map([
        [NodeKind.Rest, 1],
        [NodeKind.Survey, 1],
        [NodeKind.Battle, 1],
        [NodeKind.Elite, 1],
      ]));
  }

  /**
   * A full-sized layer: eleven nodes over five rows. The shortest route is five 刻限,
   * so a ten-刻限 budget leaves five to spend on detours and still cannot take
   * every node — which is what dungeon_exploration_v4.md §3.1 asks for.
   */
  static Full(layer) {
    return new LayerMapSpec(
      layer,
      [1, 3, 3, 3, 1],
      new Map([
        [NodeKind.Battle, 3],
        [NodeKind.Elite, 1],
        [NodeKind.Rest, 2],
        [NodeKind.Survey, 1],
        [NodeKind.Event, 2],
      ]));
  }

  /** Public properties, in declaration order, for the parity JSON. */
  toJSON() {
    return {
      $type: 'LayerMapSpec',
      Layer: this.Layer,
      RowWidths: this.RowWidths,
      Quotas: this.Quotas,
      ExtraEdgeChance: this.ExtraEdgeChance,
      NodeCount: this.NodeCount,
      MiddleNodeCount: this.MiddleNodeCount,
      RowCount: this.RowCount,
      ShortestPathCost: this.ShortestPathCost,
      FullSweepCost: this.FullSweepCost,
    };
  }
}
