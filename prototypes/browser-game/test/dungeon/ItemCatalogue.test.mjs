// Tests for src/core/dungeonContent/ItemCatalogue.js — ports DungeonContent.Tests/ItemCatalogueTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { toParityJson } from '../../src/shared/cs.js';
import { ItemCatalogue, ItemDef } from '../../src/core/dungeonContent/ItemCatalogue.js';
import { ItemEffect, EffectSide } from '../../src/core/dungeonContent/ItemEffect.js';
import { Loadout } from '../../src/core/dungeonContent/Loadout.js';
import { caught, expectedError } from './_helpers.mjs';

test('there are six to eight tools, split between exploration and battle', () => {
  assert.ok(ItemCatalogue.Tools.length >= 6 && ItemCatalogue.Tools.length <= 8);
  const exploration = ItemCatalogue.Tools.filter((t) => t.Side === EffectSide.Exploration).length;
  const battle = ItemCatalogue.Tools.filter((t) => t.Side === EffectSide.Battle).length;
  assert.ok(exploration >= 3);
  assert.ok(battle >= 3);
  assert.equal(exploration + battle, ItemCatalogue.Tools.length);
  assert.ok(exploration + battle > Loadout.ToolSlots);
});

test('ids are unique; every item is named and explained', () => {
  const ids = ItemCatalogue.All.map((i) => i.Id);
  assert.equal(new Set(ids).size, ids.length);
  for (const item of ItemCatalogue.All) {
    assert.ok(item.Name.length > 0 && item.Reading.length > 0 && item.Note.length > 0, item.Id);
    assert.ok(item.Amount > 0, item.Id);
  }
});

test('the catalogue is the C# table, text for text', () => {
  assert.deepEqual(ItemCatalogue.All.map((i) => `${i}`), [
    'bosho_no_men(miasma_density:1)', 'kokugen_no_suna(time_limit:1)', 'utsushi_no_tobari(choose_survey_target:1)',
    'toshin_no_kakera(interlude_heal:15)', 'maai_no_kutsu(wider_start:1)', 'ikitsugi_no_fue(opening_stamina:3)',
    'ryurin_no_kakera(opening_guard:5)', 'ryugo_no_fu(blunt_boss_state:1)',
    'joka_no_ko(miasma_gauge:10)', 'yakuso(heal_hp:20)', 'koi_hoshiniku(max_stamina:1)', 'wasuremizu(refill_stamina:1)',
    'shirube_no_ishi(teleport:1)',
  ]);
  assert.deepEqual(ItemCatalogue.All.map((i) => [i.Name, i.Reading]), [
    ['防瘴の面', 'ぼうしょうのめん'], ['刻限の砂', 'こくげんのすな'], ['写しの帳', 'うつしのとばり'],
    ['灯芯の欠片', 'とうしんのかけら'], ['間合いの履', 'まあいのくつ'], ['息継ぎの笛', 'いきつぎのふえ'],
    ['竜鱗の欠片', 'りゅうりんのかけら'], ['竜語の符', 'りゅうごのふ'],
    ['浄化の香', 'じょうかのこう'], ['薬草', 'やくそう'], ['濃い干し肉', 'こいほしにく'], ['忘れ水', 'わすれみず'],
    ['標の石', 'しるべのいし'],
  ]);
  assert.equal(ItemCatalogue.Tool('bosho_no_men').Note, 'この生の間、層の濃度を 1 下げる。下限は 1（seven_layers_v4.md §3.2）');
  assert.equal(ItemCatalogue.Tool('maai_no_kutsu').Note, '開始のマスが 2 から 1 になり、開始の間合いが 3 から 4 に広がる。後ろに下がる余地は無くなる');
  assert.equal(ItemCatalogue.Tool('ryurin_no_kakera').Note, '1 戦につき最初の 1 回だけ Guard 5。防御は Guard だけ（アーマーは凍結）');
  assert.equal(ItemCatalogue.Consumable('koi_hoshiniku').Note, '最大スタミナが 1 増える。ほかの一時的な増減と合わせて ±4 まで');
  assert.equal(ItemCatalogue.Consumable('joka_no_ko').Note, '瘴気の蓄積が 10% 戻る（concept-v3.md §6）');
});

test('the mask and the incense match the seven-layer table', () => {
  const mask = ItemCatalogue.Tool('bosho_no_men');
  assert.equal(mask.Effect, ItemEffect.MiasmaDensity);
  assert.equal(mask.Amount, 1);
  const incense = ItemCatalogue.Consumable('joka_no_ko');
  assert.equal(incense.Effect, ItemEffect.MiasmaGauge);
  assert.equal(incense.Amount, 10);
});

test('one tool moves the starting position; one guards once', () => {
  const movers = ItemCatalogue.Tools.filter((t) => t.Effect === ItemEffect.WiderStart);
  assert.equal(movers.length, 1);
  assert.equal(movers[0].Id, 'maai_no_kutsu');
  assert.equal(movers[0].Amount, 1);
  assert.equal(ItemCatalogue.Tools.filter((t) => t.Effect === ItemEffect.OpeningGuard).length, 1);
});

test('every item does something different; the layer-two clue is not a carried item', () => {
  assert.equal(new Set(ItemCatalogue.All.map((i) => i.Effect)).size, ItemCatalogue.All.length);
  assert.ok(!ItemCatalogue.All.some((i) => i.Id.includes('hiseki') || i.Name.includes('秘跡')));
  assert.ok(!ItemCatalogue.All.some((i) => i.Name.includes('彫り')));
});

test('All is a fresh sequence each read', () => {
  assert.notEqual(ItemCatalogue.All, ItemCatalogue.All);
  assert.equal(ItemCatalogue.Tools, ItemCatalogue.Tools);
});

test('the parity JSON of an item carries its side', () => {
  assert.deepEqual(toParityJson(ItemCatalogue.Consumable('yakuso')), {
    $type: 'ItemDef', Id: 'yakuso', Name: '薬草', Reading: 'やくそう', Effect: 'HealHp', Amount: 20, Note: 'HP が最大の 20% 戻る', Side: 'Exploration',
  });
});

test('asking for something the catalogue does not hold throws the C# message', () => {
  assert.equal(caught(() => ItemCatalogue.Tool('nope')), expectedError('item.tool'));
  assert.equal(caught(() => ItemCatalogue.Consumable('nope')), expectedError('item.consumable'));
  assert.throws(() => ItemCatalogue.Tool('joka_no_ko'), { name: 'ArgumentOutOfRangeException' });
  assert.equal(caught(() => new ItemDef(' ', 'x', 'x', ItemEffect.HealHp, 1, '')), expectedError('item.noId'));
  assert.equal(caught(() => new ItemDef('x', '', 'x', ItemEffect.HealHp, 1, '')), expectedError('item.noName'));
  assert.throws(() => new ItemDef('　', 'x', 'x', ItemEffect.HealHp, 1, ''), { name: 'ArgumentException' });
  assert.doesNotThrow(() => new ItemDef('﻿', 'x', 'x', ItemEffect.HealHp, 1, ''), 'U+FEFF is not white space in C#');
});
