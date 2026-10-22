// Tests for src/core/dungeonContent/Loadout.js — ports DungeonContent.Tests/LoadoutTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Loadout, LoadoutRuleException } from '../../src/core/dungeonContent/Loadout.js';
import { ItemEffect, EffectSide } from '../../src/core/dungeonContent/ItemEffect.js';
import { caught, expectedError } from './_helpers.mjs';

test('three tools and three consumables; the survivor bonus adds a fourth tool slot', () => {
  assert.equal(Loadout.ToolSlots, 3);
  assert.equal(Loadout.ConsumableSlots, 3);
  assert.equal(Loadout.SurvivorToolSlots, 4);
  assert.throws(() => Loadout.Of(['bosho_no_men', 'kokugen_no_suna', 'maai_no_kutsu', 'ikitsugi_no_fue']), LoadoutRuleException);
  assert.throws(() => Loadout.Of([], ['joka_no_ko', 'yakuso', 'wasuremizu', 'koi_hoshiniku']), LoadoutRuleException);
  const four = Loadout.Of(['bosho_no_men', 'kokugen_no_suna', 'maai_no_kutsu', 'ikitsugi_no_fue'], null, Loadout.SurvivorToolSlots);
  assert.equal(four.Tools.length, 4);
  assert.equal(four.ToolSlotCount, 4);
});

test('the same tool can be taken twice; a bare loadout relieves nothing', () => {
  const doubled = Loadout.Of(['bosho_no_men', 'bosho_no_men', 'kokugen_no_suna']);
  assert.equal(doubled.MiasmaDensityRelief, 2);
  assert.equal(doubled.TimeLimitBonus, 1);
  assert.equal(Loadout.Empty.MiasmaDensityRelief, 0);
  assert.equal(Loadout.Empty.TimeLimitBonus, 0);
  assert.equal(Loadout.Empty.Has(ItemEffect.WiderStart), false);
});

test('a consumable goes into a free slot; a full belt is swapped in place', () => {
  const carried = Loadout.Empty.Take('joka_no_ko').Take('yakuso');
  assert.deepEqual(carried.Consumables.map((c) => c.Id), ['joka_no_ko', 'yakuso']);
  const full = Loadout.Of([], ['joka_no_ko', 'yakuso', 'wasuremizu']);
  assert.throws(() => full.Take('koi_hoshiniku'), LoadoutRuleException);
  assert.deepEqual(full.Take('koi_hoshiniku', 1).Consumables.map((c) => c.Id), ['joka_no_ko', 'koi_hoshiniku', 'wasuremizu']);
});

test('using a consumable spends it; an empty slot throws', () => {
  const carried = Loadout.Of([], ['joka_no_ko', 'yakuso']);
  const { value: after, used } = carried.Use(0);
  assert.equal(used.Id, 'joka_no_ko');
  assert.deepEqual(after.Consumables.map((c) => c.Id), ['yakuso']);
  assert.throws(() => Loadout.Empty.Use(0), LoadoutRuleException);
});

test('nothing consumable comes home; a loadout is not changed by what comes after it', () => {
  const home = Loadout.Of(['bosho_no_men'], ['joka_no_ko', 'yakuso']).LeaveTheDungeon();
  assert.equal(home.Consumables.length, 0);
  assert.deepEqual(home.Tools.map((t) => t.Id), ['bosho_no_men']);
  const start = Loadout.Of([], ['joka_no_ko']);
  start.Take('yakuso');
  assert.equal(start.Consumables.length, 1);
});

test('reaching deeper and fighting harder cannot both be maxed out', () => {
  const deep = Loadout.Of(['bosho_no_men', 'kokugen_no_suna', 'utsushi_no_tobari']);
  assert.ok(deep.Tools.every((t) => t.Side === EffectSide.Exploration));
  assert.equal(deep.Has(ItemEffect.WiderStart), false);
  assert.equal(deep.Has(ItemEffect.OpeningGuard), false);
  const fighty = Loadout.Of(['maai_no_kutsu', 'ikitsugi_no_fue', 'ryurin_no_kakera']);
  assert.equal(fighty.MiasmaDensityRelief, 0);
  assert.equal(fighty.TimeLimitBonus, 0);
});

test('errors carry the C# messages', () => {
  const cases = {
    'contentLoadout.tools': () => Loadout.Of(['bosho_no_men', 'kokugen_no_suna', 'maai_no_kutsu', 'ikitsugi_no_fue']),
    'contentLoadout.consumables': () => Loadout.Of([], ['joka_no_ko', 'yakuso', 'wasuremizu', 'koi_hoshiniku']),
    'contentLoadout.take': () => Loadout.Of([], ['joka_no_ko', 'yakuso', 'wasuremizu']).Take('koi_hoshiniku'),
    'contentLoadout.use': () => Loadout.Empty.Use(0),
    'contentLoadout.slots': () => Loadout.Of([], null, -1),
    'contentLoadout.unknown': () => Loadout.Of(['joka_no_ko']),
  };
  for (const [key, fn] of Object.entries(cases)) assert.equal(caught(fn), expectedError(key), key);
  assert.throws(() => Loadout.Of(null), { name: 'ArgumentNullException' });
});
