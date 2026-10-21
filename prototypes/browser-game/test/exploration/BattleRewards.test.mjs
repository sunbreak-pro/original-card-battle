// Port of unity-project-kit/Assets/View/Exploration/Bridge/Tests/RewardTests.cs (#625): the rest of a won
// encounter's reward (encounters_and_rewards_v4.md §4.2 / §4.3) — three cards from the layer's pile to
// pick one from, consumables and pages — drawn from the exploration's seed.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { load } from './_needs.mjs';

// Every module but node's is loaded through load(): while a battle module is still missing,
// the tests below are skipped as 'needs <module>' and run once it lands.
const { skip, m } = await load({
  m0: '../../src/shared/cs.js',
  m1: '../../src/core/battle/CardCatalog.js',
  m2: '../../src/core/dungeonContent/ItemCatalogue.js',
  m3: '../../src/core/dungeon/IRng.js',
  m4: '../../src/core/dungeon/LayerEncounters.js',
  m5: '../../src/core/dungeon/NodeKind.js',
  m6: '../../src/bridge/exploration/BattleRewards.js',
  m7: '../../src/bridge/exploration/UniqueCards.js',
}, import.meta.url);
const { ArgumentOutOfRangeException } = m.m0 ?? {};
const { CardCatalog } = m.m1 ?? {};
const { ItemCatalogue } = m.m2 ?? {};
const { SeededRng } = m.m3 ?? {};
const { Encounter, Fight } = m.m4 ?? {};
const { NodeKind } = m.m5 ?? {};
const { BattleReward, BattleRewards, CardPile } = m.m6 ?? {};
const { UniqueCardReward, UniqueCards } = m.m7 ?? {};

const Draws = 4000;

const Percent = (count) => count * 100.0 / Draws;
const Single = (id, standIn) => new Encounter([new Fight([id])], standIn);
const Normal = () => Single('polearm_warped', false);
const Chain = (...ids) => new Encounter(ids.map((id) => new Fight([id])), true);

function Roll(kind, layer, encounter, rng) {
  const owned = new Set();
  return BattleRewards.Roll(kind, layer, encounter, UniqueCards.For(kind, encounter, owned), owned, rng);
}

/** Always draws the same value, to land a roll on a table's edge. */
class Fixed {
  constructor(value) { this._value = value; }
  NextDouble() { return this._value; }
}

const within = (actual, expected, tolerance, message) =>
  assert.ok(Math.abs(actual - expected) <= tolerance, `${message}: ${actual} not within ${tolerance} of ${expected}`);

const sameMembers = (a, b, message) => assert.deepEqual([...a].sort(), [...b].sort(), message);

// ---- 通常戦闘 ----

test('ANormalBattle_DropsOneOfCardConsumableOrPage_AtTheLayersChances', { skip }, () => {
  for (let layer = 1; layer <= 6; layer++) {
    const { Card: card, Consumable: consumable, Page: page } = BattleRewards.NormalChances(layer);
    assert.equal(card + consumable + page, 100, `layer ${layer}`);
    let cards = 0, consumables = 0, pages = 0;
    const rng = new SeededRng(BigInt(layer));
    for (let i = 0; i < Draws; i++) {
      const reward = Roll(NodeKind.Battle, layer, Normal(), rng);
      const parts = (reward.CardChoices.length > 0 ? 1 : 0) + reward.Consumables.length + reward.Pages;
      assert.equal(parts, 1, `layer ${layer}: one of the three`);
      if (reward.CardChoices.length > 0) cards++;
      if (reward.Consumables.length > 0) consumables++;
      if (reward.Pages > 0) pages++;
    }
    within(Percent(cards), card, 3, `layer ${layer}: card`);
    within(Percent(consumables), consumable, 3, `layer ${layer}: consumable`);
    within(Percent(pages), page, 3, `layer ${layer}: page`);
  }
  assert.throws(() => BattleRewards.NormalChances(7), ArgumentOutOfRangeException);
});

test('ANormalBattlesRoll_SplitsAtTheTablesEdges', { skip }, () => {
  // Layer 2 is 35 / 60 / 5: a roll of 34 is a card, 35 a consumable, 94 a consumable, 95 a page.
  assert.ok(Roll(NodeKind.Battle, 2, Normal(), new Fixed(0.34)).CardChoices.length > 0);
  assert.ok(Roll(NodeKind.Battle, 2, Normal(), new Fixed(0.35)).Consumables.length > 0);
  assert.ok(Roll(NodeKind.Battle, 2, Normal(), new Fixed(0.94)).Consumables.length > 0);
  assert.equal(Roll(NodeKind.Battle, 2, Normal(), new Fixed(0.95)).Pages, 1);
  assert.equal(Roll(NodeKind.Battle, 1, Normal(), new Fixed(0.999)).Pages, 0, 'layer 1 drops no page');
});

test('TheLastLayersBattle_AlwaysDropsAPage_AndACardHalfTheTime', { skip }, () => {
  let cards = 0;
  const rng = new SeededRng(7n);
  for (let i = 0; i < Draws; i++) {
    const reward = Roll(NodeKind.Battle, 7, Normal(), rng);
    assert.equal(reward.Pages, 1);
    assert.deepEqual(reward.Consumables, [], 'layer 7 drops no consumable');
    if (reward.CardChoices.length > 0) cards++;
  }
  within(Percent(cards), BattleRewards.LastLayerCardPercent, 3, 'layer 7: card');
});

test('TheSameSeed_DrawsTheSameReward', { skip }, () => {
  const a = Roll(NodeKind.Battle, 5, Normal(), new SeededRng(42n));
  const b = Roll(NodeKind.Battle, 5, Normal(), new SeededRng(42n));
  assert.deepEqual(b.CardChoices, a.CardChoices);
  assert.deepEqual(b.Consumables, a.Consumables);
  assert.equal(b.Pages, a.Pages);
});

// ---- 精鋭 ----

test('ARealElite_GivesItsOfferAndAConsumable_NotAPileCard', { skip }, () => {
  const elite = Single('pack_alpha', false);
  const unique = UniqueCards.For(NodeKind.Elite, elite, new Set());
  const reward = BattleRewards.Roll(NodeKind.Elite, 7, elite, unique, new Set(), new SeededRng(1n));
  assert.deepEqual(unique.Offered, UniqueCards.OfElite('pack_alpha'));
  assert.deepEqual(reward.CardChoices, [], 'the elite\'s two cards are the card');
  assert.equal(reward.Consumables.length, 1);
  assert.equal(reward.Pages, 1, 'layer 7\'s elite always drops a page');
});

test('AStandInElite_OrARealOneWhoseCardsAreOwned_GivesThreeCardsFromThePile', { skip }, () => {
  const owned = new Set(UniqueCards.OfElite('polearm_unyielding'));
  const real = Single('polearm_unyielding', false);
  const fromReal = BattleRewards.Roll(NodeKind.Elite, 5, real, UniqueCards.For(NodeKind.Elite, real, owned), owned, new SeededRng(1n));
  const fromStandIn = Roll(NodeKind.Elite, 1, Chain('polearm_warped', 'shadow_hound'), new SeededRng(1n));
  sameMembers(fromReal.CardChoices, CardPile.Of(5));
  assert.equal(fromReal.Consumables.length, 1);
  sameMembers(fromStandIn.CardChoices, CardPile.Of(1));
  assert.equal(fromStandIn.Consumables.length, 1);
  assert.equal(fromStandIn.Pages, 0, 'layer 1\'s elite drops no page');
});

// ---- 階層ボス ----

test('TheBosses_AddWhatTheirRowOfTheTableSays', { skip }, () => {
  const real = Roll(NodeKind.Boss, 3, Single('miasma_priest', false), new SeededRng(1n));
  const eliteStandIn = Roll(NodeKind.Boss, 4, Single('armored_warden', true), new SeededRng(1n));
  const normalStandIn = Roll(NodeKind.Boss, 1, Single('shadow_hound', true), new Fixed(0.0));

  assert.deepEqual(real.CardChoices, []);
  assert.deepEqual(real.Consumables, []);
  assert.equal(real.Pages, 1, 'a real boss: its three cards and a rare page');

  assert.deepEqual(eliteStandIn.CardChoices, [], 'it teaches the elite\'s two cards instead');
  assert.equal(eliteStandIn.Consumables.length, 1);
  assert.equal(eliteStandIn.Pages, 1);

  sameMembers(normalStandIn.CardChoices, CardPile.Of(1));
  assert.equal(normalStandIn.Consumables.length, 1);
  assert.equal(normalStandIn.Pages, 1, 'a 50% page that the roll 0 lands');
});

test('ANodeThatHoldsNoFight_GivesNothing', { skip }, () => {
  assert.equal(Roll(NodeKind.Rest, 1, Normal(), new SeededRng(1n)), BattleReward.None);
  assert.equal(BattleReward.None.IsEmpty, true);
});

// ---- 札の山 ----

test('ThePile_FollowsTheCardTable', { skip }, () => {
  assert.deepEqual(CardPile.Of(1), [CardCatalog.ShieldPush.Id, CardCatalog.SnapGuard.Id, CardCatalog.LeapBack.Id], '#58〜#60');
  assert.equal(CardPile.Of(7).at(-1), CardCatalog.LastStand.Id, '#80');
  assert.equal([1, 2, 3, 4, 5, 6, 7].flatMap((l) => CardPile.Of(l)).length, 23, '#58〜#80');
  assert.throws(() => CardPile.Of(0), ArgumentOutOfRangeException);
  assert.throws(() => CardPile.Of(8), ArgumentOutOfRangeException);
});

test('ThePile_DealsTheLayersOwnCardsFirst_ThenTheShallowerLayers', { skip }, () => {
  const owned = new Set(CardPile.Of(3).slice(0, 2));
  const dealt = CardPile.Deal(3, owned, new SeededRng(9n));
  assert.equal(dealt.length, CardPile.Choices);
  assert.equal(dealt[0], CardPile.Of(3)[2], 'the one card layer 3 has left');
  assert.ok(dealt.slice(1).every((id) => CardPile.Of(2).includes(id)), 'layer 2 fills the rest');
  assert.equal(new Set(dealt).size, dealt.length);
});

test('ASpentPile_TurnsTheCardIntoAConsumable', { skip }, () => {
  const owned = new Set([1, 2].flatMap((l) => CardPile.Of(l)));
  assert.deepEqual(CardPile.Deal(2, owned, new SeededRng(1n)), []);

  const reward = BattleRewards.Roll(NodeKind.Battle, 2, Normal(), UniqueCardReward.None, owned, new Fixed(0.0));
  assert.deepEqual(reward.CardChoices, []);
  assert.equal(reward.Consumables.length, 1);
});

// ---- 消耗品 ----

test('TheConsumables_FollowTheLayersWeights', { skip }, () => {
  for (let layer = 1; layer <= 7; layer++) {
    assert.equal(BattleRewards.ConsumableIds.reduce((s, id) => s + BattleRewards.WeightOf(id, layer), 0), 100, `layer ${layer}`);
    const counts = new Map(BattleRewards.ConsumableIds.map((id) => [id, 0]));
    const rng = new SeededRng(BigInt(100 + layer));
    for (let i = 0; i < Draws; i++) {
      const id = BattleRewards.DrawConsumable(layer, rng);
      counts.set(id, counts.get(id) + 1);
    }
    for (const id of BattleRewards.ConsumableIds) {
      within(Percent(counts.get(id)), BattleRewards.WeightOf(id, layer), 3, `layer ${layer}: ${id}`);
    }
  }
  assert.equal(BattleRewards.WeightOf('shirube_no_ishi', 7), 0, '§4.3: no 標の石 on layer 7');
  assert.equal(BattleRewards.WeightOf('no_such_item', 1), 0);
});

test('EveryConsumableTheTableNames_IsInTheCatalogue', { skip }, () => {
  sameMembers(BattleRewards.ConsumableIds, ItemCatalogue.Consumables.map((c) => c.Id));
});

test('TheDrawsAreTheSeededSequence', { skip }, () => {
  // DrawConsumable is one NextInt(0, 100) over the weights: a roll of 0.10 on layer 1 is past 浄化の香 (10).
  assert.equal(BattleRewards.DrawConsumable(1, new Fixed(0.0)), 'joka_no_ko');
  assert.equal(BattleRewards.DrawConsumable(1, new Fixed(0.10)), 'yakuso');
  assert.equal(BattleRewards.DrawConsumable(1, new Fixed(0.999)), 'shirube_no_ishi');
  assert.equal(BattleRewards.DrawConsumable(7, new Fixed(0.999)), 'koi_hoshiniku', 'layer 7 weighs the last two at 0');
  assert.throws(() => new BattleReward([], [], -1), ArgumentOutOfRangeException);
});
