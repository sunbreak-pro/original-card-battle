// Tests for src/core/dungeon/LayerMap.js — map queries against the C# oracle (fixtures/maps.json)
// plus the constructor's failure paths.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { toParityJson } from '../../src/shared/cs.js';
import { LayerMap, MapNode } from '../../src/core/dungeon/LayerMap.js';
import { NodeKind } from '../../src/core/dungeon/NodeKind.js';
import { SevenLayers } from '../../src/core/dungeon/SevenLayers.js';
import { fixture, caught, expectedError } from './_helpers.mjs';

test('neighbours and predecessors match the C# for every pinned map', () => {
  for (const { layer, maps } of fixture('maps').layers) {
    for (const m of maps) {
      const map = SevenLayers.Of(layer).Map(BigInt(m.seed));
      assert.deepEqual(map.Nodes.map((n) => map.Neighbours(n.Id).join('.')), m.neighbours, `layer ${layer} seed ${m.seed}`);
      assert.deepEqual(map.Nodes.map((n) => map.Predecessors(n.Id).join('.')), m.predecessors, `layer ${layer} seed ${m.seed}`);
    }
  }
});

test('a small hand-built map answers its queries', () => {
  const nodes = [
    new MapNode(0, 0, 0, NodeKind.Battle),
    new MapNode(2, 1, 1, NodeKind.Survey),
    new MapNode(1, 1, 0, NodeKind.Rest),
    new MapNode(3, 2, 0, NodeKind.Boss),
  ];
  const map = new LayerMap(4, 9, nodes, new Map([[0, [2, 1]], [1, [3]], [2, [3]]]));
  assert.equal(map.Seed, 9n, 'ulong seeds are BigInts');
  assert.equal(map.RowCount, 3);
  assert.equal(map.EntryId, 0);
  assert.equal(map.BossId, 3);
  assert.equal(map.NodeCount, 4);
  assert.equal(map.ShortestPathCost, 3);
  assert.deepEqual(map.Row(1).map((n) => n.Id), [1, 2], 'a row is ordered by column');
  assert.deepEqual(map.Neighbours(0), [1, 2]);
  assert.deepEqual(map.Neighbours(1), [0, 3]);
  assert.deepEqual(map.Predecessors(3), [1, 2], 'incoming follows the edge dictionary order');
  assert.deepEqual(map.Successors(3), []);
  assert.deepEqual(map.Successors(42), []);
  assert.equal(map.CountOf(NodeKind.Rest), 1);
  assert.equal(map.Fingerprint(), 'L4|0:0,0,battle->1.2;1:1,0,rest->3;2:1,1,survey->3;3:2,0,boss->;');
  assert.equal(String(map.Node(2)), 'MapNode { Id = 2, Row = 1, Column = 1, Kind = Survey }');
});

test('the parity JSON carries the public properties', () => {
  const map = SevenLayers.Of(1).Map(1);
  const json = toParityJson(map);
  assert.equal(json.$type, 'LayerMap');
  assert.equal(json.Seed, '1');
  assert.equal(json.Nodes.length, 11);
  assert.deepEqual(json.Nodes[0], { $type: 'MapNode', Id: 0, Row: 0, Column: 0, Kind: 'Battle' });
  assert.deepEqual(
    [json.RowCount, json.EntryId, json.BossId, json.NodeCount, json.ShortestPathCost],
    [5, 0, 10, 11, 5]);
});

test('constructor failures carry the C# messages', () => {
  const map = SevenLayers.Of(1).Map(1n);
  const e = () => new Map();
  assert.equal(caught(() => map.Node(99)), expectedError('map.node'));
  assert.equal(caught(() => new LayerMap(1, 0, [new MapNode(0, 0, 0, NodeKind.Battle), new MapNode(0, 1, 0, NodeKind.Boss)], e())), expectedError('map.dupe'));
  assert.equal(caught(() => new LayerMap(1, 0, [new MapNode(0, 1, 0, NodeKind.Battle), new MapNode(1, 2, 0, NodeKind.Boss)], e())), expectedError('map.noEntry'));
  assert.equal(caught(() => new LayerMap(1, 0, [new MapNode(0, 0, 0, NodeKind.Battle), new MapNode(1, 0, 1, NodeKind.Boss)], e())), expectedError('map.twoEntries'));
  assert.equal(caught(() => new LayerMap(1, 0, [new MapNode(0, 0, 0, NodeKind.Battle), new MapNode(1, 1, 0, NodeKind.Rest)], e())), expectedError('map.noBoss'));
  assert.equal(caught(() => new LayerMap(1, 0, [new MapNode(0, 0, 0, NodeKind.Battle), new MapNode(1, 1, 0, NodeKind.Boss)], new Map([[0, [5]]]))), expectedError('map.badTarget'));
  assert.equal(caught(() => new LayerMap(1, 0, [], e())), expectedError('map.empty'));
});
