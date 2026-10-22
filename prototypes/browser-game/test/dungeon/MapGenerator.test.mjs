// Tests for src/core/dungeon/MapGenerator.js — ports DungeonCore.Tests/MapGeneratorTests.cs and
// GoldenMapTests.cs, and pins the maps the C# generates for every layer (fixtures/maps.json).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { MapGenerator } from '../../src/core/dungeon/MapGenerator.js';
import { LayerMapSpec } from '../../src/core/dungeon/LayerMapSpec.js';
import { NodeKind, NodeKindTokens } from '../../src/core/dungeon/NodeKind.js';
import { SevenLayers } from '../../src/core/dungeon/SevenLayers.js';
import { SeededRng } from '../../src/core/dungeon/IRng.js';
import { fixture, layerSeed } from './_helpers.mjs';

const Seeds = Array.from({ length: 400 }, (_, i) => i);

// Three seeds per layer, written out from the C# oracle: 1, 42 and the layer seed
// ExplorationSession derives from run seed 1 (a full 64-bit value).
// [layer, seed, fingerprint]
const PINNED = [
  [1, '1', 'L1|0:0,0,battle->1.2.3;1:1,0,battle->4.5;2:1,1,rest->5;3:1,2,event->6;4:2,0,battle->7;5:2,1,battle->8;6:2,2,event->9;7:3,0,survey->10;8:3,1,elite->10;9:3,2,rest->10;10:4,0,boss->;'],
  [1, '42', 'L1|0:0,0,battle->1.2.3;1:1,0,elite->4.5;2:1,1,rest->5.6;3:1,2,rest->6;4:2,0,battle->7.8;5:2,1,battle->8.9;6:2,2,event->9;7:3,0,event->10;8:3,1,battle->10;9:3,2,survey->10;10:4,0,boss->;'],
  [1, '11400714819323198486', 'L1|0:0,0,battle->1.2.3;1:1,0,battle->4;2:1,1,battle->5;3:1,2,rest->6;4:2,0,rest->7;5:2,1,event->8;6:2,2,battle->9;7:3,0,event->10;8:3,1,survey->10;9:3,2,elite->10;10:4,0,boss->;'],
  [2, '1', 'L2|0:0,0,battle->1.2.3;1:1,0,carving->4.5;2:1,1,battle->5;3:1,2,rest->6;4:2,0,survey->7;5:2,1,battle->8;6:2,2,rest->9;7:3,0,event->10;8:3,1,elite->10;9:3,2,event->10;10:4,0,boss->;'],
  [2, '42', 'L2|0:0,0,battle->1.2.3;1:1,0,elite->4.5;2:1,1,survey->5.6;3:1,2,rest->6;4:2,0,rest->7.8;5:2,1,battle->8.9;6:2,2,carving->9;7:3,0,event->10;8:3,1,event->10;9:3,2,battle->10;10:4,0,boss->;'],
  [2, '11400714819323198487', 'L2|0:0,0,battle->1.2.3;1:1,0,battle->4.5;2:1,1,carving->5.6;3:1,2,rest->6;4:2,0,rest->7.8;5:2,1,elite->8.9;6:2,2,event->9;7:3,0,battle->10;8:3,1,event->10;9:3,2,survey->10;10:4,0,boss->;'],
  [3, '1', 'L3|0:0,0,battle->1.2.3;1:1,0,battle->4.5;2:1,1,rest->5;3:1,2,event->6;4:2,0,battle->7;5:2,1,battle->8;6:2,2,event->9;7:3,0,survey->10;8:3,1,elite->10;9:3,2,rest->10;10:4,0,boss->;'],
  [3, '42', 'L3|0:0,0,battle->1.2.3;1:1,0,elite->4.5;2:1,1,rest->5.6;3:1,2,rest->6;4:2,0,battle->7.8;5:2,1,battle->8.9;6:2,2,event->9;7:3,0,event->10;8:3,1,battle->10;9:3,2,survey->10;10:4,0,boss->;'],
  [3, '11400714819323198488', 'L3|0:0,0,battle->1.2.3;1:1,0,elite->4.5;2:1,1,rest->5.6;3:1,2,event->6;4:2,0,event->7.8;5:2,1,battle->8;6:2,2,battle->9;7:3,0,battle->10;8:3,1,rest->10;9:3,2,survey->10;10:4,0,boss->;'],
  [4, '1', 'L4|0:0,0,battle->1.2.3;1:1,0,battle->4;2:1,1,battle->5.6;3:1,2,rest->6;4:2,0,elite->7;5:2,1,survey->7;6:2,2,rest->7;7:3,0,boss->;'],
  [4, '42', 'L4|0:0,0,battle->1.2.3;1:1,0,battle->4;2:1,1,rest->5.6;3:1,2,elite->6;4:2,0,survey->7;5:2,1,battle->7;6:2,2,rest->7;7:3,0,boss->;'],
  [4, '11400714819323198489', 'L4|0:0,0,battle->1.2.3;1:1,0,rest->4;2:1,1,battle->5;3:1,2,battle->6;4:2,0,elite->7;5:2,1,rest->7;6:2,2,survey->7;7:3,0,boss->;'],
  [5, '1', 'L5|0:0,0,battle->1.2.3;1:1,0,battle->4;2:1,1,battle->5.6;3:1,2,rest->6;4:2,0,elite->7;5:2,1,survey->7;6:2,2,rest->7;7:3,0,boss->;'],
  [5, '42', 'L5|0:0,0,battle->1.2.3;1:1,0,battle->4;2:1,1,rest->5.6;3:1,2,elite->6;4:2,0,survey->7;5:2,1,battle->7;6:2,2,rest->7;7:3,0,boss->;'],
  [5, '11400714819323198490', 'L5|0:0,0,battle->1.2.3;1:1,0,elite->4.5;2:1,1,battle->5.6;3:1,2,rest->6;4:2,0,survey->7;5:2,1,battle->7;6:2,2,rest->7;7:3,0,boss->;'],
  [6, '1', 'L6|0:0,0,battle->1.2.3;1:1,0,battle->4;2:1,1,battle->5.6;3:1,2,rest->6;4:2,0,elite->7;5:2,1,survey->7;6:2,2,rest->7;7:3,0,boss->;'],
  [6, '42', 'L6|0:0,0,battle->1.2.3;1:1,0,battle->4;2:1,1,rest->5.6;3:1,2,elite->6;4:2,0,survey->7;5:2,1,battle->7;6:2,2,rest->7;7:3,0,boss->;'],
  [6, '11400714819323198491', 'L6|0:0,0,battle->1.2.3;1:1,0,survey->4.5;2:1,1,battle->5.6;3:1,2,rest->6;4:2,0,battle->7;5:2,1,elite->7;6:2,2,rest->7;7:3,0,boss->;'],
  [7, '1', 'L7|0:0,0,battle->1.2.3.4;1:1,0,battle->5;2:1,1,event->5;3:1,2,elite->5;4:1,3,rest->5;5:2,0,boss->;'],
  [7, '42', 'L7|0:0,0,battle->1.2.3.4;1:1,0,rest->5;2:1,1,battle->5;3:1,2,event->5;4:1,3,elite->5;5:2,0,boss->;'],
  [7, '11400714819323198492', 'L7|0:0,0,battle->1.2.3.4;1:1,0,event->5;2:1,1,rest->5;3:1,2,battle->5;4:1,3,elite->5;5:2,0,boss->;'],
];

test('three pinned seeds per layer give the C# maps', () => {
  for (const [layer, seed, fingerprint] of PINNED) {
    assert.equal(SevenLayers.Of(layer).Map(BigInt(seed)).Fingerprint(), fingerprint, `layer ${layer} seed ${seed}`);
  }
  // Number seeds and BigInt seeds name the same map.
  assert.equal(SevenLayers.Of(4).Map(42).Fingerprint(), SevenLayers.Of(4).Map(42n).Fingerprint());
  assert.equal(PINNED.length, 21);
  assert.equal(layerSeed(1, 3).toString(), PINNED[8][1]);
});

test('every layer × 27 seeds (0..19, 42, five run-derived 64-bit seeds, 2^64−1) matches the C#', () => {
  for (const { layer, maps } of fixture('maps').layers) {
    assert.equal(maps.length, 27);
    for (const m of maps) {
      assert.equal(SevenLayers.Of(layer).Map(BigInt(m.seed)).Fingerprint(), m.fingerprint, `layer ${layer} seed ${m.seed}`);
    }
  }
});

test('the minimal layer, a full layer with a trace, and an always-fork spec match the C#', () => {
  const f = fixture('maps');
  for (const m of f.minimal) assert.equal(MapGenerator.Generate(LayerMapSpec.Minimal(1), BigInt(m.seed)).Fingerprint(), m.fingerprint);
  for (const m of f.fullWithTrace) {
    assert.equal(MapGenerator.Generate(LayerMapSpec.Full(4).With(NodeKind.Trace, 1), BigInt(m.seed)).Fingerprint(), m.fingerprint);
  }
  const always = new LayerMapSpec(1, [1, 3, 3, 1], new Map([[NodeKind.Battle, 3], [NodeKind.Rest, 2], [NodeKind.Elite, 1]]), 1.0);
  for (const m of f.alwaysFork) assert.equal(MapGenerator.Generate(always, BigInt(m.seed)).Fingerprint(), m.fingerprint);
});

test('the minimal layer at seed one is pinned (GoldenMapTests)', () => {
  assert.equal(MapGenerator.Generate(LayerMapSpec.Minimal(1), 1n).Fingerprint(),
    'L1|0:0,0,battle->1.2;1:1,0,battle->3;2:1,1,survey->4;3:2,0,rest->5;4:2,1,elite->5;5:3,0,boss->;');
  const map = MapGenerator.Generate(LayerMapSpec.Minimal(1), 1n);
  assert.equal(map.Successors(map.EntryId).length, 2);
  assert.equal(map.RowCount, 4);
  assert.equal(map.NodeCount, 6);
  assert.deepEqual(map.Row(1).map((n) => n.Kind).sort(), [NodeKind.Battle, NodeKind.Survey].sort());
  assert.deepEqual(map.Row(2).map((n) => n.Kind).sort(), [NodeKind.Rest, NodeKind.Elite].sort());
});

test('the IRng overload with a SeededRng gives the same map and keeps the seed it was handed', () => {
  const viaSeed = MapGenerator.Generate(LayerMapSpec.Full(3), 77n);
  const viaRng = MapGenerator.Generate(LayerMapSpec.Full(3), new SeededRng(77n));
  assert.equal(viaRng.Fingerprint(), viaSeed.Fingerprint());
  assert.equal(viaSeed.Seed, 77n);
  assert.equal(viaRng.Seed, 0n, 'the IRng overload defaults its seed to 0');
  assert.equal(MapGenerator.Generate(LayerMapSpec.Full(3), new SeededRng(77n), 5).Seed, 5n);
});

test('same seed, same map; different seeds, different maps', () => {
  const spec = LayerMapSpec.Full(3);
  for (const seed of Seeds.slice(0, 120)) {
    assert.equal(MapGenerator.Generate(spec, seed).Fingerprint(), MapGenerator.Generate(spec, BigInt(seed)).Fingerprint());
  }
  const shapes = new Set(Seeds.map((s) => MapGenerator.Generate(spec, s).Fingerprint()));
  assert.ok(shapes.size > 20);
});

test('every seed produces a valid map', () => {
  for (const spec of [LayerMapSpec.Minimal(1), LayerMapSpec.Full(4)]) {
    for (const seed of Seeds) assert.doesNotThrow(() => MapGenerator.Generate(spec, seed), `seed ${seed}`);
  }
});

function walk(map, from, forward) {
  const seen = new Set([from]);
  const queue = [from];
  while (queue.length) {
    const current = queue.shift();
    for (const id of forward ? map.Successors(current) : map.Predecessors(current)) {
      if (!seen.has(id)) { seen.add(id); queue.push(id); }
    }
  }
  return seen;
}

test('no node is unreachable', () => {
  const spec = LayerMapSpec.Full(2);
  for (const seed of Seeds) {
    const map = MapGenerator.Generate(spec, seed);
    const fromEntry = walk(map, map.EntryId, true);
    const toBoss = walk(map, map.BossId, false);
    for (const node of map.Nodes) {
      assert.ok(fromEntry.has(node.Id), `seed ${seed}: node ${node.Id} cut off from the entry`);
      assert.ok(toBoss.has(node.Id), `seed ${seed}: node ${node.Id} cannot reach the boss`);
    }
  }
});

test('the entry is a battle and the last row is the boss', () => {
  const spec = LayerMapSpec.Full(5);
  for (const seed of Seeds.slice(0, 100)) {
    const map = MapGenerator.Generate(spec, seed);
    assert.equal(map.Node(map.EntryId).Kind, NodeKind.Battle);
    assert.equal(map.Node(map.EntryId).Row, 0);
    assert.equal(map.CountOf(NodeKind.Boss), 1);
    assert.equal(map.Node(map.BossId).Row, map.RowCount - 1);
    assert.equal(map.Successors(map.BossId).length, 0);
  }
});

test('the shortest route costs one time unit per row', () => {
  const spec = LayerMapSpec.Full(6);
  for (const seed of Seeds.slice(0, 100)) {
    const map = MapGenerator.Generate(spec, seed);
    const depth = new Map([[map.EntryId, 1]]);
    const queue = [map.EntryId];
    while (queue.length) {
      const current = queue.shift();
      for (const id of map.Successors(current)) {
        if (depth.has(id)) continue;
        depth.set(id, depth.get(current) + 1);
        queue.push(id);
      }
    }
    assert.equal(depth.get(map.BossId), map.RowCount);
    assert.equal(map.ShortestPathCost, map.RowCount);
  }
});

test('every map has a fork; a detour is never forced', () => {
  for (const seed of Seeds.slice(0, 100)) {
    const map = MapGenerator.Generate(LayerMapSpec.Full(1), seed);
    assert.ok(map.Nodes.some((n) => map.Successors(n.Id).length >= 2), `seed ${seed}`);
  }
  const spec = LayerMapSpec.Full(4).With(NodeKind.Trace, 1);
  for (const seed of Seeds.slice(0, 200)) {
    const map = MapGenerator.Generate(spec, seed);
    for (let row = 1; row < map.RowCount - 1; row++) {
      assert.ok(map.Row(row).some((n) => !NodeKindTokens.IsDetour(n.Kind)), `seed ${seed} row ${row}`);
    }
  }
});

test('the minimal layer is the one in the design doc', () => {
  const map = MapGenerator.Generate(LayerMapSpec.Minimal(), 2026n);
  assert.equal(map.CountOf(NodeKind.Battle), 2);
  assert.equal(map.CountOf(NodeKind.Elite), 1);
  assert.equal(map.CountOf(NodeKind.Rest), 1);
  assert.equal(map.CountOf(NodeKind.Survey), 1);
  assert.equal(map.CountOf(NodeKind.Boss), 1);
});

test('both sides of every fork can be taken by walking back', () => {
  for (const spec of [LayerMapSpec.Minimal(), LayerMapSpec.Full(3)]) {
    for (const seed of Seeds.slice(0, 150)) {
      const map = MapGenerator.Generate(spec, seed);
      const resolved = new Set([map.EntryId]);
      let grew = true;
      while (grew) {
        grew = false;
        for (const id of [...resolved]) {
          for (const next of map.Neighbours(id)) {
            if (!resolved.has(next)) { resolved.add(next); grew = true; }
          }
        }
      }
      assert.equal(resolved.size, map.NodeCount, `seed ${seed}`);
    }
  }
});

test('quotas are honoured', () => {
  const spec = LayerMapSpec.Full(2);
  for (const seed of Seeds.slice(0, 100)) {
    const map = MapGenerator.Generate(spec, seed);
    for (const [kind, value] of spec.Quotas) {
      assert.equal(map.CountOf(kind), kind === NodeKind.Battle ? value + 1 : value, `seed ${seed}: ${kind}`);
    }
  }
});

test('links never cross', () => {
  const spec = LayerMapSpec.Full(7);
  for (const seed of Seeds.slice(0, 150)) {
    const map = MapGenerator.Generate(spec, seed);
    for (let row = 0; row < map.RowCount - 1; row++) {
      const nodes = map.Row(row);
      for (let i = 0; i + 1 < nodes.length; i++) {
        const left = map.Successors(nodes[i].Id).map((t) => map.Node(t).Column);
        const right = map.Successors(nodes[i + 1].Id).map((t) => map.Node(t).Column);
        assert.ok(Math.min(...left) <= Math.min(...right), `seed ${seed} row ${row}`);
        assert.ok(Math.max(...left) <= Math.max(...right), `seed ${seed} row ${row}`);
      }
    }
  }
});
