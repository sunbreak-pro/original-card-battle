// Port of unity-port/DungeonCore/MapValidator.cs
import { argNull } from './_errors.js';
import { NodeKind, NodeKindTokens } from './NodeKind.js';

/** Raised when a generated map breaks one of the rules the generator promises. */
export class MapValidationException extends Error {
  constructor(message) {
    super(message);
    this.name = 'MapValidationException';
  }
}

/**
 * Checks the promises a layer map makes. The generator runs this on every map it
 * produces, so a bad seed fails loudly at generation instead of quietly at play time.
 */
export const MapValidator = {
  Validate(map, spec) {
    if (map === null || map === undefined) throw argNull('map');
    if (spec === null || spec === undefined) throw argNull('spec');

    CheckShape(map, spec);
    CheckEnds(map);
    CheckEdgesGoForwardOneRow(map);
    CheckEveryNodeIsOnARoute(map);
    CheckDetoursAreAvoidable(map);
    CheckThereIsAFork(map);
    CheckQuotas(map, spec);
  },
};

function CheckShape(map, spec) {
  if (map.NodeCount !== spec.NodeCount)
    throw new MapValidationException(`map has ${map.NodeCount} nodes, spec asks for ${spec.NodeCount}`);
  if (map.RowCount !== spec.RowCount)
    throw new MapValidationException(`map has ${map.RowCount} rows, spec asks for ${spec.RowCount}`);

  for (let r = 0; r < spec.RowCount; r++) {
    const width = map.Row(r).length;
    if (width !== spec.RowWidths[r])
      throw new MapValidationException(`row ${r} holds ${width} nodes, spec asks for ${spec.RowWidths[r]}`);
  }
}

function CheckEnds(map) {
  const entry = map.Node(map.EntryId);
  if (entry.Row !== 0)
    throw new MapValidationException('the entry is not on row 0');
  if (entry.Kind !== NodeKind.Battle)
    throw new MapValidationException(`the entry is ${NodeKindTokens.ToToken(entry.Kind)}, it must be a battle`);

  const bossCount = map.CountOf(NodeKind.Boss);
  if (bossCount !== 1)
    throw new MapValidationException(`a layer holds exactly one boss, this one holds ${bossCount}`);

  const boss = map.Node(map.BossId);
  if (boss.Row !== map.RowCount - 1)
    throw new MapValidationException('the boss is not on the last row');
  if (map.Successors(boss.Id).length !== 0)
    throw new MapValidationException('the boss leads somewhere; it is the layer\'s exit');
}

function CheckEdgesGoForwardOneRow(map) {
  for (const node of map.Nodes) {
    for (const target of map.Successors(node.Id)) {
      const next = map.Node(target);
      if (next.Row !== node.Row + 1)
        throw new MapValidationException(
          `node ${node.Id} (row ${node.Row}) links to node ${target} (row ${next.Row}); links go to the next row only`);
    }

    if (node.Id !== map.BossId && map.Successors(node.Id).length === 0)
      throw new MapValidationException(`node ${node.Id} is a dead end that is not the boss`);
    if (node.Id !== map.EntryId && map.Predecessors(node.Id).length === 0)
      throw new MapValidationException(`node ${node.Id} cannot be entered`);
  }
}

/**
 * The Definition of Done for #97: no node is unreachable. A node counts as reachable
 * only if the entry can get to it AND it can get to the boss, so nothing is a cul-de-sac.
 */
function CheckEveryNodeIsOnARoute(map) {
  const fromEntry = Reach(map, map.EntryId, true);
  const toBoss = Reach(map, map.BossId, false);

  for (const node of map.Nodes) {
    if (!fromEntry.has(node.Id))
      throw new MapValidationException(`node ${node.Id} cannot be reached from the entry`);
    if (!toBoss.has(node.Id))
      throw new MapValidationException(`node ${node.Id} cannot reach the boss`);
  }
}

function CheckDetoursAreAvoidable(map) {
  for (let r = 1; r < map.RowCount - 1; r++) {
    const row = map.Row(r);
    if (row.every((n) => NodeKindTokens.IsDetour(n.Kind)))
      throw new MapValidationException(`row ${r} is nothing but detours, so the player cannot avoid them`);
  }
}

function CheckThereIsAFork(map) {
  if (!map.Nodes.some((n) => map.Successors(n.Id).length >= 2))
    throw new MapValidationException('the map has no fork; every layer must offer a choice');
}

function CheckQuotas(map, spec) {
  for (const [key, value] of spec.Quotas) {
    const wanted = value;
    const got = map.CountOf(key);
    // The entry is a battle the spec does not count, so battles run one over.
    const expected = key === NodeKind.Battle ? wanted + 1 : wanted;
    if (got !== expected)
      throw new MapValidationException(
        `map holds ${got} × ${NodeKindTokens.ToToken(key)}, expected ${expected}`);
  }
}

function Reach(map, from, forward) {
  const seen = new Set([from]);
  const queue = [from];
  let head = 0;
  while (head < queue.length) {
    const current = queue[head++];
    const next = forward ? map.Successors(current) : map.Predecessors(current);
    for (const id of next) {
      if (!seen.has(id)) {
        seen.add(id);
        queue.push(id);
      }
    }
  }
  return seen;
}
