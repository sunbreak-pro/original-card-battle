// Port of unity-port/DungeonCore/MapGenerator.cs
import { idiv, enumIndex, stableSortBy } from '../../shared/cs.js';
import { argNull, argOutOfRange } from './_errors.js';
import { IRng, SeededRng, RngExtensions } from './IRng.js';
import { NodeKind, NodeKindTokens } from './NodeKind.js';
import { MapNode, LayerMap } from './LayerMap.js';
import { MapValidator } from './MapValidator.js';

/**
 * Builds one layer's node map from a spec and a seed. Nothing here reads the clock or
 * the platform RNG, so the same spec and seed give the same map everywhere (#97).
 *
 * The Web build's `src/domain/dungeon/logic/dungeonLogic.ts` was read for reference and
 * deliberately not ported: it draws from `Math.random()` and assumes the old five-depth
 * loop with lives and Gold.
 */
export const MapGenerator = {
  /**
   * The two C# overloads in one:
   *   Generate(LayerMapSpec spec, ulong seed)
   *   Generate(LayerMapSpec spec, IRng rng, ulong seed = 0)
   * The second argument is an IRng when it has NextDouble(); otherwise it is the ulong seed
   * (number or BigInt).
   */
  Generate(spec, seedOrRng, seed = 0n) {
    if (IRng.is(seedOrRng)) return generateWith(spec, seedOrRng, seed);
    if (spec === null || spec === undefined) throw argNull('spec');
    if (seedOrRng === null || seedOrRng === undefined) throw argNull('rng');
    return generateWith(spec, new SeededRng(seedOrRng), seedOrRng);
  },
};

function generateWith(spec, rng, seed) {
  if (spec === null || spec === undefined) throw argNull('spec');
  if (rng === null || rng === undefined) throw argNull('rng');

  const kinds = AssignKinds(spec, rng);
  const nodes = BuildNodes(spec, kinds);
  const edges = BuildEdges(spec, nodes, rng);

  const map = new LayerMap(spec.Layer, seed, nodes, edges);
  MapValidator.Validate(map, spec);
  return map;
}

/**
 * Picks a kind for every slot. Detour kinds (精鋭 / 痕跡 / 彫り) go into rows that keep
 * at least one node the player can take instead, so a detour is never forced (§3.1).
 */
function AssignKinds(spec, rng) {
  const rows = spec.RowCount;
  const grid = new Array(rows);
  // default(NodeKind) is Battle (0).
  for (let r = 0; r < rows; r++) grid[r] = new Array(spec.RowWidths[r]).fill(NodeKind.Battle);

  grid[0][0] = NodeKind.Battle;
  grid[rows - 1][0] = NodeKind.Boss;

  const detourBag = [];
  const plainBag = [];
  // OrderBy(p => p.Key): by the enum's underlying value, i.e. declaration order.
  for (const [key, value] of stableSortBy([...spec.Quotas], ([k]) => enumIndex(NodeKind, k))) {
    for (let i = 0; i < value; i++) {
      if (NodeKindTokens.IsDetour(key)) detourBag.push(key);
      else plainBag.push(key);
    }
  }
  RngExtensions.Shuffle(rng, detourBag);
  RngExtensions.Shuffle(rng, plainBag);

  // One slot per middle row is held back for a non-detour kind. That single rule is
  // what makes every elite avoidable without a second pass over the map.
  const reserved = new Array(rows - 2);
  for (let r = 1; r < rows - 1; r++) {
    reserved[r - 1] = { Row: r, Column: RngExtensions.NextInt(rng, 0, spec.RowWidths[r]) };
  }

  const eligible = [];
  for (let r = 1; r < rows - 1; r++) {
    for (let c = 0; c < spec.RowWidths[r]; c++) {
      if (reserved[r - 1].Column === c) continue;
      eligible.push({ Row: r, Column: c });
    }
  }
  RngExtensions.Shuffle(rng, eligible);

  const taken = new Set();
  for (let i = 0; i < detourBag.length; i++) {
    if (i >= eligible.length) throw argOutOfRange('index');
    const slot = eligible[i];
    grid[slot.Row][slot.Column] = detourBag[i];
    taken.add(`${slot.Row},${slot.Column}`);
  }

  let plainIndex = 0;
  for (let r = 1; r < rows - 1; r++) {
    for (let c = 0; c < spec.RowWidths[r]; c++) {
      if (taken.has(`${r},${c}`)) continue;
      if (plainIndex >= plainBag.length) throw argOutOfRange('index');
      grid[r][c] = plainBag[plainIndex++];
    }
  }

  return grid;
}

function BuildNodes(spec, kinds) {
  const nodes = [];
  let id = 0;
  for (let r = 0; r < spec.RowCount; r++) {
    for (let c = 0; c < spec.RowWidths[r]; c++) {
      nodes.push(new MapNode(id++, r, c, kinds[r][c]));
    }
  }
  return nodes;
}

/**
 * Links each row to the next. Every node keeps at least one way forward and every node
 * keeps at least one way back, so no node can be stranded. Links never cross, which is
 * what lets the map be drawn as a ladder later.
 */
function BuildEdges(spec, nodes, rng) {
  const edges = new Map();
  for (const node of nodes) edges.set(node.Id, Object.freeze([]));

  const rowStart = new Array(spec.RowCount);
  let running = 0;
  for (let r = 0; r < spec.RowCount; r++) {
    rowStart[r] = running;
    running += spec.RowWidths[r];
  }

  for (let r = 0; r < spec.RowCount - 1; r++) {
    const a = spec.RowWidths[r];
    const b = spec.RowWidths[r + 1];

    const lo = new Array(a);
    const hi = new Array(a);
    for (let i = 0; i < a; i++) {
      lo[i] = idiv(i * b, a);
      hi[i] = idiv((i + 1) * b + a - 1, a) - 1;
      if (hi[i] < lo[i]) hi[i] = lo[i];
    }

    // Widening one node's reach by a single column is what creates a fork. It is only
    // allowed where it cannot overtake the next node's reach, so links stay uncrossed.
    // (&& short-circuits: no draw is taken when the widening is not allowed.)
    for (let i = 0; i < a - 1; i++) {
      if (hi[i] + 1 <= hi[i + 1] && RngExtensions.NextChance(rng, spec.ExtraEdgeChance)) hi[i]++;
    }

    for (let i = 0; i < a; i++) {
      const targets = [];
      for (let j = lo[i]; j <= hi[i]; j++) targets.push(rowStart[r + 1] + j);
      edges.set(rowStart[r] + i, targets);
    }
  }

  return edges;
}
