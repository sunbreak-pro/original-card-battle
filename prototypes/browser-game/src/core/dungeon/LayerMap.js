// Port of unity-port/DungeonCore/LayerMap.cs
import { defineRecord, stableSortBy, max, InvalidOperationException, KeyNotFoundException } from '../../shared/cs.js';
import { argException, argNull, argOutOfRange, toUlong } from './_errors.js';
import { NodeKind, NodeKindTokens } from './NodeKind.js';

/** One node on a layer's map. Rows run from the entry (0) down to the boss. */
export class MapNode extends defineRecord('MapNode', ['Id', 'Row', 'Column', 'Kind']) {
  /** The compiler-generated record ToString. */
  ToString() { return `MapNode { Id = ${this.Id}, Row = ${this.Row}, Column = ${this.Column}, Kind = ${this.Kind} }`; }

  toString() { return this.ToString(); }
}

const EMPTY = Object.freeze([]);

/** Enumerable.Single(predicate) with the BCL's messages. */
function single(items, predicate) {
  let found;
  let count = 0;
  for (const item of items) {
    if (!predicate(item)) continue;
    found = item;
    count++;
    if (count > 1) throw new InvalidOperationException('Sequence contains more than one matching element');
  }
  if (count === 0) throw new InvalidOperationException('Sequence contains no matching element');
  return found;
}

/** IReadOnlyDictionary<int, IReadOnlyList<int>> handed in as a Map (or a plain object keyed by id). */
function asEdgeMap(edges) {
  if (edges instanceof Map) return edges;
  return new Map(Object.entries(edges).map(([k, v]) => [Number(k), v]));
}

/**
 * A generated layer map: nodes in rows, forward-only edges, one entry and one boss.
 * The map is immutable; progress through it lives in the exploration run (#98).
 */
export class LayerMap {
  /**
   * @param {number} layer
   * @param {number|bigint} seed ulong (kept as a BigInt)
   * @param {MapNode[]} nodes
   * @param {Map<number, number[]>} edges
   */
  constructor(layer, seed, nodes, edges) {
    if (nodes === null || nodes === undefined) throw argNull('nodes');
    if (edges === null || edges === undefined) throw argNull('edges');
    if (nodes.length === 0) throw argException('a layer map needs at least one node', 'nodes');

    this.Layer = layer;
    this.Seed = toUlong(seed);
    this.Nodes = nodes;
    this._edges = asEdgeMap(edges);

    // nodes.ToDictionary(n => n.Id): a repeated id throws.
    const byId = new Map();
    for (const n of nodes) {
      if (byId.has(n.Id)) throw argException(`An item with the same key has already been added. Key: ${n.Id}`);
      byId.set(n.Id, n);
    }
    this._byId = byId;

    this.RowCount = max(nodes, (n) => n.Row) + 1;
    this.EntryId = single(nodes, (n) => n.Row === 0).Id;
    this.BossId = single(nodes, (n) => n.Kind === NodeKind.Boss).Id;

    const incoming = new Map();
    for (const node of nodes) incoming.set(node.Id, []);
    for (const [key, value] of this._edges) {
      for (const target of value) {
        if (!incoming.has(target)) throw new KeyNotFoundException(`The given key '${target}' was not present in the dictionary.`);
        incoming.get(target).push(key);
      }
    }
    this._incoming = incoming;
  }

  /** 1-based layer number (1..7). */
  // Layer — set in the constructor.

  /** The seed this map was generated from. Same seed and spec, same map. */
  // Seed — set in the constructor (BigInt).

  get NodeCount() { return this.Nodes.length; }

  /**
   * 刻限 needed to walk from the entry to the boss without detouring. Every row
   * costs one node, so this is the row count — the floor on the cost of a layer.
   */
  get ShortestPathCost() { return this.RowCount; }

  Node(id) {
    if (this._byId.has(id)) return this._byId.get(id);
    throw argOutOfRange('id', id, 'no such node');
  }

  Successors(id) {
    return this._edges.has(id) ? this._edges.get(id) : EMPTY;
  }

  Predecessors(id) {
    return this._incoming.has(id) ? this._incoming.get(id) : EMPTY;
  }

  /**
   * Everywhere the player may step from this node. Movement runs both ways along a link:
   * walking back over a node that is already resolved is free, which is how a run takes
   * both sides of a fork (dungeon_exploration_v4.md §4.2). Only entering an unresolved
   * node costs 刻限, and that accounting belongs to the exploration run, not to the map.
   */
  Neighbours(id) {
    const distinct = [...new Set([...this.Successors(id), ...this.Predecessors(id)])];
    return stableSortBy(distinct, (x) => x);
  }

  Row(row) {
    return stableSortBy(this.Nodes.filter((n) => n.Row === row), (n) => n.Column);
  }

  CountOf(kind) {
    let count = 0;
    for (const n of this.Nodes) if (n.Kind === kind) count++;
    return count;
  }

  /**
   * Stable one-line rendering of the whole map. Two maps with the same fingerprint
   * are the same map, so determinism tests compare this instead of walking the graph.
   */
  Fingerprint() {
    let sb = '';
    sb += 'L' + this.Layer + '|';
    for (const node of stableSortBy(this.Nodes, (n) => n.Id)) {
      sb += node.Id + ':' + node.Row + ',' + node.Column
        + ',' + NodeKindTokens.ToToken(node.Kind)
        + '->' + stableSortBy(this.Successors(node.Id).slice(), (x) => x).join('.')
        + ';';
    }
    return sb;
  }

  /** Public properties, in declaration order, for the parity JSON. */
  toJSON() {
    return {
      $type: 'LayerMap',
      Layer: this.Layer,
      Seed: this.Seed,
      Nodes: this.Nodes,
      RowCount: this.RowCount,
      EntryId: this.EntryId,
      BossId: this.BossId,
      NodeCount: this.NodeCount,
      ShortestPathCost: this.ShortestPathCost,
    };
  }
}
