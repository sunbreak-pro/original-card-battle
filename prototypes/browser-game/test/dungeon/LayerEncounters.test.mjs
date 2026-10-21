// Tests for src/core/dungeon/LayerEncounters.js — ports DungeonCore.Tests/LayerEncountersTests.cs and
// pins the C# pick sequences (fixtures/encounters.json).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { sum, toParityJson } from '../../src/shared/cs.js';
import { Fight, Encounter, LayerEncounterTable, LayerEncounters } from '../../src/core/dungeon/LayerEncounters.js';
import { NodeKind } from '../../src/core/dungeon/NodeKind.js';
import { SeededRng } from '../../src/core/dungeon/IRng.js';
import { SevenLayers } from '../../src/core/dungeon/SevenLayers.js';
import { fixture, caught, expectedError, FixedRng, CountingRng } from './_helpers.mjs';

const RosterLayers = {
  polearm_warped: [1, 2], shadow_hound: [1, 2], rusted_revenant: [2, 4], crossbow_hunter: [2, 3],
  mist_archer: [4, 6], twin_blade_warped: [4, 7], polearm_crystal: [3, 4], armored_warden: [4, 5],
  pack_alpha: [5, 7], polearm_unyielding: [5, 6], miasma_priest: [3, 3], abyss_angler: [6, 6], distortion_root: [7, 7],
};
const Sizes = {
  polearm_warped: 1, shadow_hound: 1, rusted_revenant: 1, crossbow_hunter: 1, mist_archer: 1, twin_blade_warped: 1,
  polearm_crystal: 1, armored_warden: 2, pack_alpha: 1, polearm_unyielding: 1, miasma_priest: 2, abyss_angler: 2, distortion_root: 2,
};
const ThePair = 'polearm_warped+crossbow_hunter';
const PairLayers = [2, 3];
const SectionNineNormal = [
  ['polearm_warped', 'shadow_hound'],
  ['polearm_warped', 'shadow_hound', 'rusted_revenant', 'crossbow_hunter'],
  ['rusted_revenant', 'crossbow_hunter', 'polearm_crystal'],
  ['rusted_revenant', 'mist_archer', 'twin_blade_warped', 'polearm_crystal'],
  ['mist_archer', 'twin_blade_warped'],
  ['mist_archer', 'twin_blade_warped'],
  ['twin_blade_warped'],
];
const SectionNinePair = [[], [ThePair], [ThePair], [], [], [], []];
const SectionNineElite = [[], [], [], ['armored_warden'], ['armored_warden', 'pack_alpha', 'polearm_unyielding'], ['pack_alpha', 'polearm_unyielding'], ['pack_alpha']];
const SectionNineBoss = [[], [], ['miasma_priest'], [], [], ['abyss_angler'], ['distortion_root']];
const NormalCells = [6, 6, 6, 7, 7, 7, 8];
const BossCells = 8;
const CellsBeforeTheEnemies = 5;
const CombatKinds = [NodeKind.Battle, NodeKind.Elite, NodeKind.Boss];
const NonCombat = [NodeKind.Rest, NodeKind.Survey, NodeKind.Event, NodeKind.Carving, NodeKind.Trace];

const render = (pool) => pool.map((e) => e.ToString()).join(' | ');

test('there is one table per layer; asking outside the lair throws', () => {
  assert.equal(LayerEncounters.All.length, SevenLayers.Count);
  for (let layer = 1; layer <= 7; layer++) assert.equal(LayerEncounters.Of(layer).Layer, layer);
  assert.throws(() => LayerEncounters.Of(0), { name: 'ArgumentOutOfRangeException' });
  assert.throws(() => LayerEncounters.Of(8), { name: 'ArgumentOutOfRangeException' });
  assert.throws(() => LayerEncounters.Pick(NodeKind.Battle, 0, new SeededRng(1)), { name: 'ArgumentOutOfRangeException' });
  assert.throws(() => LayerEncounters.Pick(NodeKind.Battle, 8, new SeededRng(1)), { name: 'ArgumentOutOfRangeException' });
});

test('the table matches the design doc and the C#', () => {
  const expected = [
    ['polearm_warped | shadow_hound', 'polearm_warped,shadow_hound', 'shadow_hound'],
    ['polearm_warped | shadow_hound | rusted_revenant | crossbow_hunter', ThePair, 'rusted_revenant'],
    ['rusted_revenant | crossbow_hunter | polearm_crystal', ThePair, 'miasma_priest'],
    [
      'rusted_revenant | mist_archer | twin_blade_warped | polearm_crystal',
      'rusted_revenant,mist_archer | rusted_revenant,twin_blade_warped | rusted_revenant,polearm_crystal | ' +
      'mist_archer,rusted_revenant | mist_archer,twin_blade_warped | mist_archer,polearm_crystal | ' +
      'twin_blade_warped,rusted_revenant | twin_blade_warped,mist_archer | twin_blade_warped,polearm_crystal | ' +
      'polearm_crystal,rusted_revenant | polearm_crystal,mist_archer | polearm_crystal,twin_blade_warped',
      'armored_warden',
    ],
    ['mist_archer | twin_blade_warped', 'armored_warden | pack_alpha', 'polearm_unyielding'],
    ['mist_archer | twin_blade_warped', 'pack_alpha | polearm_unyielding', 'abyss_angler'],
    ['twin_blade_warped', 'pack_alpha', 'distortion_root'],
  ];
  for (let layer = 1; layer <= 7; layer++) {
    const table = LayerEncounters.Of(layer);
    const [normal, elite, boss] = expected[layer - 1];
    assert.equal(render(table.Normal), normal, `layer ${layer} normal`);
    assert.equal(render(table.Elite), elite, `layer ${layer} elite`);
    assert.equal(render(table.Boss), boss, `layer ${layer} boss`);
  }
  for (const t of fixture('encounters').tables) {
    const table = LayerEncounters.Of(t.layer);
    assert.equal(render(table.Normal), t.normal);
    assert.equal(render(table.Elite), t.elite);
    assert.equal(render(table.Boss), t.boss);
    assert.equal([...table.Normal, ...table.Elite, ...table.Boss].map((e) => (e.IsStandIn ? '1' : '0')).join(''), t.standIn);
  }
});

test('every combat pool holds something; normals are single and never stand-ins', () => {
  for (const table of LayerEncounters.All) {
    for (const kind of CombatKinds) assert.ok(table.PoolFor(kind).length > 0);
    assert.ok(table.Normal.every((e) => !e.IsStandIn));
    assert.ok(table.Normal.every((e) => e.Fights.length === 1 && e.Fights[0].EnemyIds.length === 1));
  }
});

test('bosses are real only on layers 3, 6, 7; elites only on 5..7', () => {
  for (const table of LayerEncounters.All) {
    const realBoss = [3, 6, 7].includes(table.Layer);
    assert.ok(table.Boss.every((e) => e.IsStandIn === !realBoss), `layer ${table.Layer}`);
    const realElite = table.Layer >= 5;
    assert.ok(table.Elite.every((e) => e.IsStandIn === !realElite), `layer ${table.Layer}`);
  }
});

test('the pair appears only in the elite slot of layers two and three', () => {
  for (const table of LayerEncounters.All) {
    for (const kind of CombatKinds) {
      for (const encounter of table.PoolFor(kind)) {
        for (const fight of encounter.Fights.filter((f) => f.EnemyIds.length > 1)) {
          assert.equal(fight.ToString(), ThePair);
          assert.equal(kind, NodeKind.Elite);
          assert.ok(PairLayers.includes(table.Layer));
        }
      }
    }
  }
  for (const layer of PairLayers) assert.ok(LayerEncounters.Of(layer).Elite.some((e) => e.ToString() === ThePair));
});

test('every enemy is on a roster layer and comes from the right §9 column', () => {
  for (const table of LayerEncounters.All) {
    const row = table.Layer - 1;
    const anyColumn = [...SectionNineNormal[row], ...SectionNinePair[row], ...SectionNineElite[row], ...SectionNineBoss[row]];
    for (const kind of CombatKinds) {
      const ownColumn = kind === NodeKind.Battle ? SectionNineNormal[row] : kind === NodeKind.Elite ? SectionNineElite[row] : SectionNineBoss[row];
      for (const encounter of table.PoolFor(kind)) {
        for (const fight of encounter.Fights) {
          for (const id of fight.EnemyIds) assert.ok(id in RosterLayers, id);
          if (fight.EnemyIds.length === 1) {
            const [from, to] = RosterLayers[fight.EnemyIds[0]];
            assert.ok(table.Layer >= from && table.Layer <= to, `${fight} on layer ${table.Layer}`);
          }
          assert.ok((encounter.IsStandIn ? anyColumn : ownColumn).includes(fight.ToString()), `layer ${table.Layer} ${kind}: ${fight}`);
        }
      }
    }
  }
});

test('every roster enemy is used somewhere; every fight fits its node\'s field', () => {
  const used = new Set(LayerEncounters.All.flatMap((t) => CombatKinds.flatMap((k) => t.PoolFor(k))).flatMap((e) => e.EnemyIds));
  assert.deepEqual([...used].sort(), Object.keys(RosterLayers).sort());
  for (const table of LayerEncounters.All) {
    for (const kind of CombatKinds) {
      const cells = kind === NodeKind.Battle ? NormalCells[table.Layer - 1]
        : kind === NodeKind.Elite ? Math.min(NormalCells[table.Layer - 1] + 1, BossCells) : BossCells;
      for (const fight of table.PoolFor(kind).flatMap((e) => e.Fights)) {
        assert.ok(CellsBeforeTheEnemies + sum(fight.EnemyIds, (id) => Sizes[id]) <= cells, `layer ${table.Layer} ${kind}: ${fight}`);
      }
    }
  }
});

test('nodes without a fight return null and draw nothing; a fight draws exactly once', () => {
  const rng = new SeededRng(5);
  for (const kind of NonCombat) {
    for (let layer = 1; layer <= 7; layer++) assert.equal(LayerEncounters.Pick(kind, layer, rng), null);
  }
  for (let layer = 1; layer <= 7; layer++) {
    for (const kind of CombatKinds) {
      const c = new CountingRng(new SeededRng(7));
      LayerEncounters.Pick(kind, layer, c);
      assert.equal(c.Draws, 1, `layer ${layer} ${kind}`);
    }
    for (const kind of NonCombat) {
      const c = new CountingRng(new SeededRng(7));
      LayerEncounters.Pick(kind, layer, c);
      assert.equal(c.Draws, 0, `layer ${layer} ${kind}`);
    }
  }
});

test('pick draws from the node\'s pool, reaches every entry, and covers both ends', () => {
  const rng = new SeededRng(11);
  for (let layer = 1; layer <= 7; layer++) {
    for (const kind of CombatKinds) {
      for (let i = 0; i < 50; i++) assert.ok(LayerEncounters.Of(layer).PoolFor(kind).includes(LayerEncounters.Pick(kind, layer, rng)));
    }
  }
  const r21 = new SeededRng(21);
  const seen = new Set();
  for (let i = 0; i < 300; i++) seen.add(LayerEncounters.Pick(NodeKind.Elite, 4, r21));
  assert.equal(seen.size, LayerEncounters.Of(4).Elite.length);
  const pool = LayerEncounters.Of(2).Normal;
  assert.equal(LayerEncounters.Pick(NodeKind.Battle, 2, new FixedRng(0.0)), pool[0]);
  assert.equal(LayerEncounters.Pick(NodeKind.Battle, 2, new FixedRng(1.0)), pool[pool.length - 1]);
});

test('layer four chains put every enemy first and second equally often', () => {
  const chains = LayerEncounters.Of(4).Elite;
  assert.equal(chains.length, 12);
  const count = (i) => chains.reduce((m, e) => m.set(e.Fights[i].ToString(), (m.get(e.Fights[i].ToString()) ?? 0) + 1), new Map());
  assert.ok([...count(0).values()].every((v) => v === 3));
  assert.ok([...count(1).values()].every((v) => v === 3));
  assert.ok(chains.every((e) => e.Fights[0].ToString() !== e.Fights[1].ToString()));
});

test('encounters compare by value', () => {
  const a = new Encounter([new Fight(['polearm_warped', 'crossbow_hunter'])], true);
  const b = new Encounter([new Fight(['polearm_warped', 'crossbow_hunter'])], true);
  const hound = new Fight(['shadow_hound']);
  const otherHound = new Fight(['shadow_hound']);
  assert.ok(hound.Equals(otherHound));
  assert.equal(hound.GetHashCode(), otherHound.GetHashCode());
  assert.ok(a.Equals(b));
  assert.ok(a.equals(b));
  assert.equal(a.GetHashCode(), b.GetHashCode());
  assert.ok(!a.Equals(new Encounter([new Fight(['crossbow_hunter', 'polearm_warped'])], true)));
  assert.ok(!a.Equals(new Encounter([new Fight(['polearm_warped', 'crossbow_hunter'])], false)));
  assert.ok(LayerEncounters.Pick(NodeKind.Boss, 3, new SeededRng(1)).Equals(new Encounter([new Fight(['miasma_priest'])], false)));
  assert.equal(`${a}`, ThePair);
  assert.deepEqual(new Encounter([new Fight(['a']), new Fight(['b', 'c'])], true).EnemyIds, ['a', 'b', 'c']);
});

test('the tables and pools cannot be replaced from outside', () => {
  assert.throws(() => { LayerEncounters.All[0] = LayerEncounters.Of(2); }, TypeError);
  assert.throws(() => { LayerEncounters.Of(1).Normal.push(null); }, TypeError);
  assert.throws(() => { new Fight(['x']).EnemyIds.push('y'); }, TypeError);
});

test('the C# pick sequences replay exactly', () => {
  for (const s of fixture('encounters').seqs) {
    const rng = new SeededRng(BigInt(s.seed));
    const picks = [];
    for (let i = 0; i < 120; i++) picks.push(LayerEncounters.Pick(CombatKinds[i % 3], 1 + (i % 7), rng).ToString());
    assert.deepEqual(picks, s.picks, `seed ${s.seed}`);
  }
});

test('the parity JSON of an encounter carries its enemy ids', () => {
  const json = toParityJson(LayerEncounters.Of(1).Elite[0]);
  assert.deepEqual(json, {
    $type: 'Encounter',
    Fights: [{ $type: 'Fight', EnemyIds: ['polearm_warped'] }, { $type: 'Fight', EnemyIds: ['shadow_hound'] }],
    IsStandIn: true,
    EnemyIds: ['polearm_warped', 'shadow_hound'],
  });
});

test('errors carry the C# messages', () => {
  const cases = {
    'encounters.pickNull': () => LayerEncounters.Pick(NodeKind.Battle, 1, null),
    'encounters.of8': () => LayerEncounters.Of(8),
    'encounters.table0': () => new LayerEncounterTable(0, [], [], []),
    'encounters.tableEmpty': () => new LayerEncounterTable(1, [], [], []),
    'fight.empty': () => new Fight([]),
    'fight.blank': () => new Fight(['']),
    'encounter.empty': () => new Encounter([], false),
    'encounter.nullFight': () => new Encounter([null], false),
  };
  for (const [key, fn] of Object.entries(cases)) assert.equal(caught(fn), expectedError(key), key);
});
