// Tests for src/core/dungeon/BattleField.js — ports DungeonCore.Tests/BattleFieldTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { sum } from '../../src/shared/cs.js';
import { BattleField } from '../../src/core/dungeon/BattleField.js';
import { NodeKind } from '../../src/core/dungeon/NodeKind.js';
import { LayerEncounters } from '../../src/core/dungeon/LayerEncounters.js';
import { caught, expectedError } from './_helpers.mjs';

test('each layer hands the table\'s line', () => {
  const rows = [[1, 6, 7, 8], [2, 6, 7, 8], [3, 6, 7, 8], [4, 7, 8, 8], [5, 7, 8, 8], [6, 7, 8, 8], [7, 8, 8, 8]];
  for (const [layer, normal, elite, boss] of rows) {
    assert.equal(BattleField.Cells(layer, NodeKind.Battle), normal, `layer ${layer}`);
    assert.equal(BattleField.Cells(layer, NodeKind.Elite), elite, `layer ${layer}`);
    assert.equal(BattleField.Cells(layer, NodeKind.Boss), boss, `layer ${layer}`);
  }
});

test('a line is widened until every enemy stands', () => {
  assert.equal(BattleField.CellsFor(1, NodeKind.Battle, 2), 7);
  assert.equal(BattleField.CellsFor(1, NodeKind.Battle, 1), 6);
  assert.equal(BattleField.CellsFor(2, NodeKind.Elite, 2), 7);
});

test('no fight needing more than eight cells is put on a line', () => {
  assert.throws(() => BattleField.CellsFor(7, NodeKind.Boss, 4), { name: 'ArgumentException' });
});

test('a node without a fight has no line; a layer outside the lair is refused', () => {
  for (const kind of [NodeKind.Rest, NodeKind.Survey, NodeKind.Event, NodeKind.Carving]) {
    assert.throws(() => BattleField.Cells(1, kind), { name: 'ArgumentException' });
  }
  for (const layer of [0, 8]) {
    assert.throws(() => BattleField.Cells(layer, NodeKind.Battle), { name: 'ArgumentOutOfRangeException' });
  }
});

test('every fight of the encounter tables fits its line', () => {
  const sizeTwo = new Set(['armored_warden', 'miasma_priest', 'abyss_angler', 'distortion_root']);
  for (let layer = 1; layer <= 7; layer++) {
    for (const kind of [NodeKind.Battle, NodeKind.Elite, NodeKind.Boss]) {
      for (const encounter of LayerEncounters.Of(layer).PoolFor(kind)) {
        for (const fight of encounter.Fights) {
          const sizes = sum(fight.EnemyIds, (id) => (sizeTwo.has(id) ? 2 : 1));
          assert.ok(BattleField.CellsFor(layer, kind, sizes) <= BattleField.MaxCells, `layer ${layer} ${kind}: ${fight}`);
        }
      }
    }
  }
});

test('errors carry the C# messages', () => {
  assert.equal(caught(() => BattleField.Cells(1, NodeKind.Rest)), expectedError('battleField.rest'));
  assert.equal(caught(() => BattleField.Cells(0, NodeKind.Battle)), expectedError('battleField.layer0'));
  assert.equal(caught(() => BattleField.CellsFor(7, NodeKind.Boss, 4)), expectedError('battleField.tooWide'));
  assert.equal(caught(() => BattleField.CellsFor(1, NodeKind.Battle, 0)), expectedError('battleField.noEnemy'));
  assert.equal(caught(() => BattleField.Cells(1, 99)), expectedError('battleField.invalidKind'));
});
