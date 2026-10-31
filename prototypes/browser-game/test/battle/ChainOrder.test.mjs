// Tests for src/core/battle/ChainOrder.js (port of unity-port/BattleCore/ChainOrder.cs).
// Cases ported from BattleCore.Tests/ChainOrderTests.cs, plus the C# dump (BattleCards.fixture.json):
// 26 specs through Parse (results, or the exception type and message), the default three and the
// nine with their lines, and the densities.
//
// ChainOrder.js imports Enemies.js and TurnLoop.js (BattleSetup.SliceFieldCells), as the C# does:
// the module is loaded dynamically and the tests skip until those load.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fixture, errorOf, late } from './_battle_cards.mjs';
import { Constants } from '../../src/core/battle/Constants.js';

const deps = await late(import.meta.url, ['../../src/core/battle/ChainOrder.js']);
const skip = deps.skip;
const M = deps.mods ?? {};

/** A parsed order as the dump writes it: [ToString(), FieldCells] per battle. */
const battles = (list) => list.map((b) => [b.ToString(), b.FieldCells]);

test('TheDefaultOrder_IsSection12sThree_AndNineMayBePicked', { skip }, () => {
  const { ChainOrder } = M;
  assert.deepEqual(ChainOrder.Default.map((b) => b.ToString()), ['polearm_warped@1', 'shadow_hound@2', 'armored_warden@4']);
  assert.equal(ChainOrder.Default.length, Constants.ChainBattlesDefault);
  assert.equal(ChainOrder.Nine.length, 9);
  assert.deepEqual([...ChainOrder.Nine[2].EnemyIds], ['polearm_warped', 'crossbow_hunter'], "roster §9's third is the pair");
  const layers = ChainOrder.Nine.map((b) => b.Layer);
  assert.deepEqual(layers, [...layers].sort((a, b) => a - b), 'the layer only goes down');
  assert.deepEqual(ChainOrder.Nine.map((b) => b.FieldCells), [6, 6, 7, 6, 6, 7, 6, 6, 7], '7 for a size-2 enemy and for the pair');
  assert.ok(ChainOrder.Nine.flatMap((b) => b.Defs).every((d) => d != null), 'every id is one the core knows');
});

test('AnOrderSpeltOut_IsParsed_AndAWrongOneIsRefused', { skip }, () => {
  const { ChainOrder } = M;
  assert.equal(ChainOrder.Parse('3'), ChainOrder.Default);
  assert.equal(ChainOrder.Parse('9'), ChainOrder.Nine);
  assert.equal(ChainOrder.Parse('nine'), ChainOrder.Nine);
  const custom = ChainOrder.Parse('shadow_hound, mist_archer@5,polearm_warped+crossbow_hunter');
  assert.deepEqual(custom.map((b) => b.ToString()), ['shadow_hound@1', 'mist_archer@5', 'polearm_warped+crossbow_hunter@5'],
    'a battle with no layer stays on the one before');
  assert.equal(errorOf(() => ChainOrder.Parse('polearm_warped,nobody')).$error, 'KeyNotFoundException');
  assert.equal(errorOf(() => ChainOrder.Parse('polearm_warped@8')).$error, 'ArgumentOutOfRangeException');
  assert.equal(errorOf(() => ChainOrder.Parse('polearm_warped,,shadow_hound')).$error, 'ArgumentException');
  assert.equal(errorOf(() => ChainOrder.Parse('polearm_warped+shadow_hound+mist_archer+pack_alpha')).$error, 'ArgumentException', 'ENEMIES_MAX 3');
  assert.equal(errorOf(() => ChainOrder.Parse('armored_warden+abyss_angler')).$error, 'ArgumentException', 'two size-2 enemies need 9 cells');
});

test('C#: the default three, the nine, and the densities', { skip }, () => {
  const { ChainOrder } = M;
  assert.deepEqual(battles(ChainOrder.Default), fixture.ChainDefault);
  assert.deepEqual(battles(ChainOrder.Nine), fixture.ChainNine);
  const densities = [0, 1, 2, 3, 4, 5, 6, 7, 8].map((layer) => {
    const e = errorOf(() => ChainOrder.DensityOf(layer));
    return e === 'no exception' ? ChainOrder.DensityOf(layer) : e;
  });
  assert.deepEqual(densities, fixture.Densities);
});

test('C#: Parse over 26 specs (.NET Trim and int.TryParse(NumberStyles.None) included)', { skip }, () => {
  const { ChainOrder } = M;
  for (const [spec, expected] of fixture.Parse) {
    const e = errorOf(() => ChainOrder.Parse(spec));
    const got = e === 'no exception' ? battles(ChainOrder.Parse(spec)) : e;
    assert.deepEqual(got, expected, JSON.stringify(spec));
  }
  assert.deepEqual(errorOf(() => ChainOrder.Parse(null)), fixture.ParseNull);
});

test('ChainBattle: Defs is a fresh list of the enemies, toString is the C# ToString', { skip }, () => {
  const { ChainOrder, ChainBattle } = M;
  const pair = ChainOrder.Nine[2];
  assert.ok(pair instanceof ChainBattle);
  assert.notEqual(pair.Defs, pair.Defs);
  assert.deepEqual(pair.Defs.map((d) => d.Id), ['polearm_warped', 'crossbow_hunter']);
  assert.equal(`${pair}`, 'polearm_warped+crossbow_hunter@3');
  assert.ok(Object.isFrozen(ChainOrder.Default) && Object.isFrozen(ChainOrder.Nine) && Object.isFrozen(ChainOrder.Densities));
});
