// Tests for the JS extension 「出立の枠（DeckLimits）」 (docs/EXTENSIONS.md): the survivor's bonus at 出立
// (concept-v3.md §8.2 / §12-12 / §14 生存者ボーナス 「ツール枠 +1、デッキ上限 +5 を次の 1 生だけ」;
// docs/INHERITANCE.md R-14). One test or more per rule, named after it:
//   L-1  DeckLimits.Default is the C#'s constants, and every check without limits is the C#'s.
//   L-2  The limits come from Inheritance.startLife: 4 / 45 after a survivor, the C#'s after a death.
//   L-3  Cards.Validate / DeckRules.ValidateDeck hold the deck to DeckMin..DeckMax.
//   L-4  DeckRules.CanAdd refuses the card past DeckMax (the 41st by default, the 46th with the bonus).
//   L-5  DeckRules.Validate holds the tools to ToolSlots and the consumables to ConsumableSlots.
//   L-6  DeckBuilder: Add / CanAdd / Validate / StatusText / Title / Random / Prototype / Load read the limits.
//   L-7  DepartureBuilder: the slots, the lines and the headings read the limits; the 4th slot is the bonus's.
//   L-8  A new heir's first 出立 opens on the full prototype deck (owned = the forty ∪ the prototype's kinds).
//   L-9  Bad limits are refused.
// The hand-off tests (「… hand-off」) follow the payload of 出立する through the shared contract the way the
// router is told to wire it (docs/EXTENSIONS.md 「For the router and the other jobs」): the departure's
// Loadout through the WORLD job's toRunLoadout, the limits through Expedition's options to the battle.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Constants } from '../../src/core/battle/Constants.js';
import { Cards } from '../../src/core/battle/Cards.js';
import { DeckRules, DeckLimits, Departure } from '../../src/core/battle/DeckRules.js';
import { CardCatalog, PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { CardInstance } from '../../src/core/battle/Types.js';
import { DeckBuilder } from '../../src/bridge/depiction/DeckBuilder.js';
import { DepartureBuilder, SupplyOption } from '../../src/bridge/depiction/DepartureBuilder.js';
import { DepartureSupplies } from '../../src/scenes/meta/DepartureSupplies.js';
import { Expedition } from '../../src/bridge/exploration/Expedition.js';
import { Loadout } from '../../src/core/dungeonContent/Loadout.js';
import { newLineage, startLife, recordSurvival, recordDeath } from '../../src/core/inheritance/Inheritance.js';
import { ItemCatalogue } from '../../src/core/dungeonContent/ItemCatalogue.js';
import { EffectSide } from '../../src/core/dungeonContent/ItemEffect.js';
import { toRunLoadout, runLoadoutToData, runLoadoutFromData } from '../../src/bridge/exploration/Loadouts.js';
import { RunStart } from '../../src/bridge/explorationScript/ExplorationSession.js';
import { ScreenAction } from '../../src/bridge/explorationScript/ExplorationScreen.js';
import { ExplorationReducer } from '../../src/core/dungeon/ExplorationReducer.js';

const Survivor = new DeckLimits({ DeckMax: 45, ToolSlots: 4 });

/** `n` cards over the catalogue in canon order, three of a kind, instance ids "id-copy". */
function deckOf(n) {
  const deck = [];
  for (const def of CardCatalog.All) {
    for (let copy = 0; copy < 3 && deck.length < n; copy++) deck.push(new CardInstance(`${def.Id}-${copy}`, def));
    if (deck.length >= n) break;
  }
  return deck;
}
const allKinds = CardCatalog.All.map((d) => d.Id);

/** A builder holding `n` cards (three of a kind, canon order). */
function filled(n, limits) {
  const b = new DeckBuilder(null, limits);
  for (const def of CardCatalog.All) {
    for (let i = 0; i < 3 && b.Total < n; i++) assert.equal(b.Add(def.Id), true, `card ${b.Total + 1}`);
    if (b.Total >= n) break;
  }
  return b;
}

function supplies(limits) {
  const tools = [
    new SupplyOption('bosho_no_men', '防瘴の面', '層の濃度を 1 下げる'),
    new SupplyOption('maai_no_kutsu', '間合いの履', '開始のマスが 2 から 1 になる', 1),
    new SupplyOption('ikitsugi_no_fue', '息継ぎの笛', '戦闘開始時のスタミナ +3', 0, 3),
  ];
  const consumables = [new SupplyOption('yakuso', '薬草', 'HP が最大の 20% 戻る')];
  return limits === undefined ? new DepartureBuilder(tools, consumables) : new DepartureBuilder(tools, consumables, limits);
}

/** A lineage whose last life closed alive (dev/meta.js's sample, shortened). */
function survivorLineage() {
  let L = newLineage(20261020);
  const s1 = startLife(L);
  L = recordDeath(s1.Lineage, null, { Layer: 3, NodeId: 14, NodeKind: 'Boss', Ending: 'Fallen' },
    [{ CardId: 'thrust', Rank: 1 }], { Disclosure: {}, Pages: 0, Memo: '' });
  const s2 = startLife(L);
  return recordSurvival(s2.Lineage, null, { Layer: 4, NodeId: 9, NodeKind: 'Rest', Ending: 'Survived' },
    [{ CardId: 'reach_thrust', Rank: 1 }], { Disclosure: {}, Pages: 0, Memo: '' });
}

// ------------------------------------------------------------------ L-1 the C#'s by default

test('L-1 DeckLimits.Default is the C# constants, frozen and shared', () => {
  const d = DeckLimits.Default;
  assert.deepEqual({ ...d }, { DeckMin: Constants.DeckMin, DeckMax: Constants.DeckMax, ToolSlots: DeckRules.ToolSlots, ConsumableSlots: DeckRules.ConsumableSlots });
  assert.deepEqual({ ...d }, { DeckMin: 20, DeckMax: 40, ToolSlots: 3, ConsumableSlots: 3 });
  assert.equal(DeckLimits.Default, d);
  assert.equal(Object.isFrozen(d), true);
  assert.equal(d.IsDefault, true);
  assert.equal(new DeckLimits().Equals(d), true);
  assert.equal(new DeckLimits({}).IsDefault, true);
  assert.equal(Survivor.IsDefault, false);
  assert.equal(DeckRules.ToolSlots, 3, 'the C# constants stay');
  assert.equal(DeckRules.ConsumableSlots, 3);
});

test('L-1 every check without limits gives what the explicit Default and null give', () => {
  for (const n of [19, 20, 33, 40, 41, 45, 46]) {
    const deck = deckOf(n);
    const plain = Cards.Validate(deck);
    assert.deepEqual(Cards.Validate(deck, DeckLimits.Default), plain, `Validate ${n}`);
    assert.deepEqual(Cards.Validate(deck, null), plain);
    assert.deepEqual(DeckRules.ValidateDeck(deck, allKinds, DeckLimits.Default), DeckRules.ValidateDeck(deck, allKinds));
    assert.equal(DeckRules.CanAdd(deck, 'thrust', allKinds, DeckLimits.Default), DeckRules.CanAdd(deck, 'thrust', allKinds));
    assert.equal(DeckRules.CanAdd(deck, 'thrust', allKinds, null), DeckRules.CanAdd(deck, 'thrust', allKinds));
    for (const tools of [[], ['a', 'b', 'c'], ['a', 'b', 'c', 'd']]) {
      const dep = new Departure(deck, tools, ['x', 'y', 'z', 'w'].slice(0, tools.length), 0, 'thrust');
      assert.deepEqual(DeckRules.Validate(dep, allKinds, DeckLimits.Default), DeckRules.Validate(dep, allKinds));
    }
  }
  assert.deepEqual(Cards.Validate(deckOf(41)).Errors, ['Deck has 41 cards; the maximum is 40.']);
});

test('L-1 a builder made without limits prints what the C# prints', () => {
  const b = new DeckBuilder();
  assert.equal(b.Limits, DeckLimits.Default);
  assert.equal(b.Title, DeckBuilder.Title);
  assert.equal(DeckBuilder.Title, 'デッキを組む（80 種から 20〜40 枚、1 種 3 枚まで）');
  assert.equal(DeckBuilder.TitleFor(null), DeckBuilder.Title);
  assert.equal(filled(40).StatusText, '40 枚です。20〜40 枚、1 種 3 枚を満たしています');
  assert.equal(filled(40).CanAdd('kesa_cut'), false, 'the 41st');
  assert.equal(b.RaisedLine, '');
  const d = supplies();
  assert.equal(d.Limits, DeckLimits.Default);
  assert.equal(d.Title, DepartureBuilder.Title);
  assert.equal(d.ToolHeading, DepartureBuilder.ToolHeading);
  assert.equal(d.ConsumableHeading, DepartureBuilder.ConsumableHeading);
  assert.equal(DepartureBuilder.Title, '出立の支度（ツール 3 枠・消耗品 3 枠・才能 1 枚）');
  assert.equal(d.RaisedLine, '');
  assert.equal(d.IsBonusToolSlot(3), false);
  for (let i = 0; i < 3; i++) assert.equal(d.AddTool('bosho_no_men'), true);
  assert.equal(d.AddTool('bosho_no_men'), false, 'the 4th tool');
  assert.equal(d.ToolsLine, 'ツール 3 / 3: 防瘴の面、防瘴の面、防瘴の面');
  assert.equal(DepartureSupplies.NewBuilder().Limits, DeckLimits.Default);
  assert.equal(DeckBuilder.Random(7).Save(), DeckBuilder.Random(7, DeckBuilder.RandomDefaultSize, DeckLimits.Default).Save());
});

// ------------------------------------------------------------------ L-2 from the lineage

test('L-2 a survivor\'s heir sets out with 4 tool slots and a deck of 20〜45 (concept-v3 §14)', () => {
  const ls = startLife(survivorLineage());
  assert.equal(ls.SurvivorBonus, true);
  const limits = DeckLimits.From(ls);
  assert.deepEqual({ ...limits }, { DeckMin: 20, DeckMax: 45, ToolSlots: 4, ConsumableSlots: 3 });
  assert.equal(limits.Equals(Survivor), true);
  // §14 「次の 1 生だけ」: the life after that is back to the C#'s.
  const after = recordDeath(ls.Lineage, null, { Layer: 2, NodeId: 3, NodeKind: 'Battle', Ending: 'Fallen' }, [], { Disclosure: {}, Pages: 0, Memo: '' });
  assert.equal(DeckLimits.From(startLife(after)).IsDefault, true);
});

test('L-2 a first life (and a life after a death) gets the C#\'s limits', () => {
  assert.equal(DeckLimits.From(startLife(newLineage(7))).IsDefault, true);
  assert.equal(DeckLimits.From(null), DeckLimits.Default);
  assert.equal(DeckLimits.From(undefined), DeckLimits.Default);
  assert.equal(DeckLimits.From(Survivor), Survivor);
  assert.deepEqual({ ...DeckLimits.From({ DeckMax: 45 }) }, { DeckMin: 20, DeckMax: 45, ToolSlots: 3, ConsumableSlots: 3 }, 'a missing key is the default\'s');
});

// ------------------------------------------------------------------ L-3 the deck's bounds

test('L-3 Cards.Validate holds the deck to the limits\' DeckMin..DeckMax', () => {
  for (const n of [41, 42, 43, 44, 45]) {
    assert.equal(Cards.Validate(deckOf(n), Survivor).Ok, true, `${n} cards with the bonus`);
    assert.equal(Cards.Validate(deckOf(n)).Ok, false, `${n} cards without`);
  }
  assert.deepEqual(Cards.Validate(deckOf(46), Survivor).Errors, ['Deck has 46 cards; the maximum is 45.']);
  assert.deepEqual(Cards.Validate(deckOf(19), Survivor).Errors, ['Deck has 19 cards; the minimum is 20.'], 'the floor stays 20');
  assert.equal(DeckRules.ValidateDeck(deckOf(45), allKinds, Survivor).Ok, true);
  assert.deepEqual(DeckRules.ValidateDeck(deckOf(45), allKinds).Errors, ['Deck has 45 cards; the maximum is 40.']);
  // the other §8 rules do not move: three of a kind
  const four = [...deckOf(40), new CardInstance('thrust-9', CardCatalog.ById('thrust'))];
  assert.deepEqual(Cards.Validate(four, Survivor).Errors, ['Deck holds 4 copies of "thrust"; the maximum is 3.']);
});

// ------------------------------------------------------------------ L-4 CanAdd

test('L-4 DeckRules.CanAdd: the 41st with the bonus, never the 46th', () => {
  const forty = deckOf(40);
  assert.equal(DeckRules.CanAdd(forty, 'observe', allKinds), false);
  assert.equal(DeckRules.CanAdd(forty, 'observe', allKinds, Survivor), true);
  assert.equal(DeckRules.CanAdd(deckOf(44), 'observe', allKinds, Survivor), true);
  assert.equal(DeckRules.CanAdd(deckOf(45), 'observe', allKinds, Survivor), false);
  assert.equal(DeckRules.CanAdd(forty, 'thrust', allKinds, Survivor), false, 'still three of a kind');
  assert.equal(DeckRules.CanAdd(forty, 'observe', ['thrust'], Survivor), false, 'still owned kinds only');
});

// ------------------------------------------------------------------ L-5 the slots

test('L-5 DeckRules.Validate: a 4th tool fits only with the bonus; the consumables stay 3', () => {
  const deck = deckOf(45);
  const four = new Departure(deck, ['a', 'b', 'c', 'd'], ['x', 'y', 'z'], 0, 'thrust');
  assert.deepEqual(DeckRules.Validate(four, allKinds, Survivor).Errors, []);
  assert.deepEqual(DeckRules.Validate(four, allKinds).Errors, ['Deck has 45 cards; the maximum is 40.', '4 tools do not fit 3 slots.']);
  const five = new Departure(deck, ['a', 'b', 'c', 'd', 'e'], ['x', 'y', 'z', 'w'], 0, 'thrust');
  assert.deepEqual(DeckRules.Validate(five, allKinds, Survivor).Errors, ['5 tools do not fit 4 slots.', '4 consumables do not fit 3 slots.']);
});

// ------------------------------------------------------------------ L-6 DeckBuilder

test('L-6 DeckBuilder with the bonus: up to 45, the lines print 45', () => {
  const b = filled(45, Survivor);
  assert.equal(b.Total, 45);
  assert.equal(b.CanAdd('observe'), false, 'the 46th');
  assert.equal(b.Add('observe'), false);
  assert.equal(b.IsValid, true);
  assert.equal(b.StatusText, '45 枚です。20〜45 枚、1 種 3 枚を満たしています');
  assert.equal(filled(19, Survivor).StatusText, '19 枚です。あと 1 枚入れると戦えます');
  assert.equal(b.Title, 'デッキを組む（80 種から 20〜45 枚、1 種 3 枚まで）');
  assert.equal(DeckBuilder.TitleFor(Survivor), b.Title);
  assert.equal(DeckBuilder.Title, 'デッキを組む（80 種から 20〜40 枚、1 種 3 枚まで）', 'the static Title stays the C#\'s');
  assert.equal(b.RaisedLine, 'デッキ上限 +5（40 → 45）');
  assert.equal(filled(41, Survivor).Validate().Ok, true);
});

test('L-6 DeckBuilder.Random draws up to the limits\' cap, fixed by the seed', () => {
  for (const seed of [1, 7, 17, 42]) {
    const r = DeckBuilder.Random(seed, 45, Survivor);
    assert.equal(r.Total, 45, `seed ${seed}`);
    assert.equal(r.IsValid, true);
    assert.equal(r.Limits, Survivor);
    assert.equal(r.Save(), DeckBuilder.Random(seed, 45, Survivor).Save(), 'deterministic');
    assert.equal(DeckBuilder.Random(seed, 99, Survivor).Total, 45, 'clamped to 45');
    assert.equal(DeckBuilder.Random(seed, 45).Total, 40, 'the C# clamps to 40');
    // 30 is inside both: the same deck either way
    assert.equal(DeckBuilder.Random(seed, 30, Survivor).Save(), DeckBuilder.Random(seed, 30).Save());
  }
});

test('L-6 Prototype and the saved string carry the limits', () => {
  const p = DeckBuilder.Prototype(Survivor);
  assert.equal(p.Limits, Survivor);
  assert.equal(p.Total, PrototypeDeck.Kinds.length * PrototypeDeck.Copies);
  const saved = filled(45, Survivor).Save();
  const read = DeckBuilder.Load(saved, Survivor);
  assert.equal(read.Total, 45);
  assert.equal(read.Limits, Survivor);
  assert.equal(DeckBuilder.Load(saved).Total, 0, 'the C#\'s 40 refuses a 45-card string');
  const out = DeckBuilder.LoadOrPrototype(true, saved, true, Survivor);
  assert.equal(out.ok.Total, 45);
  assert.equal(out.value, '');
  const refused = DeckBuilder.LoadOrPrototype(true, saved, true);
  assert.equal(refused.ok.Total, 0);
  assert.equal(refused.value, '保存したデッキがデッキの決まりに合わないため、空のデッキから始めます');
  const plain = DeckBuilder.LoadOrPrototype(false, null, Survivor);
  assert.equal(plain instanceof DeckBuilder, true, 'limits alone is not the out overload');
  assert.equal(plain.Limits, Survivor);
  assert.equal(plain.Total, 20);
  assert.equal(DeckBuilder.LoadOrPrototype(false, null).Limits, DeckLimits.Default);
  const broken = DeckBuilder.Read('thrust:2,???', Survivor);
  assert.equal(broken.ok.Limits, Survivor, 'an empty deck from a broken string keeps the limits');
});

test('L-6 a survivor\'s 45-card save opens whole after the bonus ended, and asks for 5 cards out (試作 7)', () => {
  const saved = filled(45, Survivor).Save();
  const r = DeckBuilder.ReadOverCap(saved);
  assert.equal(r.ok.Total, 45, 'every card kept');
  assert.equal(r.ok.Limits, DeckLimits.Default, 'held to the life\'s limits');
  assert.equal(r.ok.Save(), saved);
  assert.equal(r.value, '生存者ボーナスが終わり、デッキ上限が 40 枚に戻りました。保存したデッキ 45 枚から、外す 5 枚を選んでください');
  assert.equal(r.ok.IsValid, false);
  assert.equal(r.ok.StatusText, '45 枚です。あと 5 枚外すと戦えます');
  assert.equal(r.ok.CanAdd('thrust'), false, '＋ stays greyed out over the cap');
  // the cards come out one by one; at the cap the deck is the C#'s again
  const ids = r.ok.Build().map((c) => c.Def.Id).reverse();
  for (let i = 0; i < 4; i++) assert.equal(r.ok.Remove(ids[i]), true);
  assert.equal(r.ok.StatusText, '41 枚です。あと 1 枚外すと戦えます');
  assert.equal(r.ok.IsValid, false);
  r.ok.Remove(ids[4]);
  assert.equal(r.ok.IsValid, true);
  assert.equal(r.ok.StatusText, '40 枚です。20〜40 枚、1 種 3 枚を満たしています');
  assert.equal(r.ok.CanAdd('thrust'), false, 'thrust already holds 3');
});

test('L-6 ReadOverCap reads every other string as the C#\'s Read', () => {
  for (const [saved, limits] of [
    ['', DeckLimits.Default], [null, Survivor], ['thrust:2,kesa_cut:3', DeckLimits.Default],
    [filled(45, Survivor).Save(), Survivor], [filled(40).Save(), DeckLimits.Default],
    ['thrust:4', DeckLimits.Default], ['thrust:2,???', Survivor], ['no_such_card:1', DeckLimits.Default],
    ['thrust:2,thrust:1', DeckLimits.Default],
  ]) {
    const a = DeckBuilder.ReadOverCap(saved, limits);
    const b = DeckBuilder.Read(saved, limits);
    assert.equal(a.value, b.value, String(saved));
    assert.equal(a.ok.Save(), b.ok.Save(), String(saved));
    assert.equal(a.ok.Limits, b.ok.Limits);
  }
  // past the cap and broken in another way too (a 4th copy): the C#'s empty deck and notice
  const lastKind = CardCatalog.All[CardCatalog.All.length - 1].Id;
  const pastAndBroken = filled(42, Survivor).Save() + ',' + lastKind + ':4';
  const broken = DeckBuilder.ReadOverCap(pastAndBroken);
  assert.equal(broken.ok.Total, 0);
  assert.equal(broken.value, DeckBuilder.Read(pastAndBroken).value);
  assert.equal(DeckBuilder.ReadOverCap(filled(42, Survivor).Save()).ok.Total, 42, 'the same deck without the 4th copy opens whole');
});

test('L-6 OverCapNotice names the bonus only when the cap is back at the C#\'s 40', () => {
  assert.equal(DeckBuilder.OverCapNotice(43), '生存者ボーナスが終わり、デッキ上限が 40 枚に戻りました。保存したデッキ 43 枚から、外す 3 枚を選んでください');
  assert.equal(DeckBuilder.OverCapNotice(46, Survivor), 'この生のデッキ上限は 45 枚です。保存したデッキ 46 枚から、外す 1 枚を選んでください');
});

test('L-6 WithOwned keeps the owned kinds and a deck over the cap; a kind not owned is left out', () => {
  const over = DeckBuilder.ReadOverCap(filled(45, Survivor).Save()).ok;
  const owned = DeckBuilder.WithPrototypeKinds(Expedition.InitialKinds);
  const kept = over.WithOwned(owned);
  assert.equal(kept.Limits, over.Limits);
  assert.deepEqual(kept.Owned, owned);
  const ownedSet = new Set(owned);
  const expect = over.Build().filter((c) => ownedSet.has(c.Def.Id)).length;
  assert.equal(kept.Total, expect);
  assert.equal(over.WithOwned(null).Total, 45);
  const proto = DeckBuilder.Prototype().WithOwned(owned);
  assert.equal(proto.Total, 20);
  assert.equal(proto.IsValid, true);
});

// ------------------------------------------------------------------ L-7 DepartureBuilder

test('L-7 DepartureBuilder with the bonus: four tool slots, the 4th is the bonus\'s', () => {
  const d = supplies(Survivor);
  assert.equal(d.Title, '出立の支度（ツール 4 枠・消耗品 3 枠・才能 1 枚）');
  assert.equal(d.ToolHeading, 'ツール（探索と戦闘で共通の 4 枠。同じものを 2 つ持てます）');
  assert.equal(d.ConsumableHeading, '消耗品（3 枠。拠点へは持ち帰りません）');
  assert.equal(DepartureBuilder.TitleFor(Survivor), d.Title);
  assert.equal(DepartureBuilder.ToolHeadingFor(Survivor), d.ToolHeading);
  assert.equal(DepartureBuilder.ConsumableHeadingFor(Survivor), d.ConsumableHeading);
  assert.equal(DepartureBuilder.Title, '出立の支度（ツール 3 枠・消耗品 3 枠・才能 1 枚）', 'the static Title stays the C#\'s');
  assert.equal(d.RaisedLine, 'ツール枠 +1（3 → 4）');
  assert.equal(DepartureBuilder.BonusLabel, '生存者ボーナス');
  assert.deepEqual([0, 1, 2, 3, 4].map((i) => d.IsBonusToolSlot(i)), [false, false, false, true, false]);
  assert.equal(d.AddTool('bosho_no_men'), true);
  assert.equal(d.AddTool('bosho_no_men'), true);
  assert.equal(d.AddTool('maai_no_kutsu'), true);
  assert.equal(d.CanAddTool('ikitsugi_no_fue'), true, 'the 4th slot');
  assert.equal(d.AddTool('ikitsugi_no_fue'), true);
  assert.equal(d.AddTool('bosho_no_men'), false, 'no 5th');
  assert.equal(d.ToolsLine, 'ツール 4 / 4: 防瘴の面、防瘴の面、間合いの履、息継ぎの笛');
  for (let i = 0; i < 3; i++) assert.equal(d.AddConsumable('yakuso'), true);
  assert.equal(d.AddConsumable('yakuso'), false, 'the consumables stay 3');
  assert.equal(d.ConsumablesLine, '消耗品 3 / 3: 薬草、薬草、薬草');
  // set out with a 45-card deck and four tools
  const deck = filled(45, Survivor);
  assert.equal(d.ChooseTalent(deck, 'thrust'), true);
  assert.deepEqual(d.Validate(deck).Errors, []);
  assert.equal(d.CanSetOut(deck), true);
  assert.equal(d.StatusText(deck), '出立できます');
  const dep = d.Build(deck);
  assert.equal(dep.Deck.length, 45);
  assert.equal(dep.Tools.length, 4);
  assert.equal(dep.StartStaminaBonus, 3);
  assert.equal(dep.PlayerStartCell, 1);
  // the scene's loadout for the exploration takes four tools when the life has four slots
  assert.equal(Loadout.Of(dep.Tools.map(() => 'bosho_no_men'), [], Survivor.ToolSlots).ToolSlotCount, 4);
  assert.equal(DepartureSupplies.NewBuilder(Survivor).Limits, Survivor);
});

test('L-7 the deck\'s bounds come from the deck builder, the slots from the departure builder', () => {
  // A survivor's deck under a departure builder made without limits: the deck passes (as its IsValid
  // says), the 4th tool is still refused.
  const deck = filled(45, Survivor);
  const d = supplies();
  d.ChooseTalent(deck, 'thrust');
  assert.equal(deck.IsValid, true);
  assert.equal(d.CanSetOut(deck), true);
  assert.equal(d.StatusText(deck), '出立できます');
  assert.equal(d.CanAddTool('bosho_no_men'), true);
  for (let i = 0; i < 3; i++) d.AddTool('bosho_no_men');
  assert.equal(d.AddTool('bosho_no_men'), false);
  // and the other way: a C# deck builder under a survivor's departure is held to 40
  const plainDeck = filled(40);
  const s = supplies(Survivor);
  s.ChooseTalent(plainDeck, 'thrust');
  assert.equal(s.CanSetOut(plainDeck), true);
  assert.equal(plainDeck.CanAdd('observe'), false);
});

// ------------------------------------------------------------------ L-8 the first 出立

test('L-8 the prototype deck opens whole when the prototype\'s kinds join the owned forty', () => {
  const adopt = (src, owned, limits) => {
    const b = new DeckBuilder(owned, limits);
    for (const card of src.Build()) b.Add(card.Def.Id);
    return b;
  };
  const forty = Expedition.InitialKinds;
  const outside = PrototypeDeck.Kinds.filter((d) => !forty.includes(d.Id)).map((d) => d.Id);
  assert.deepEqual(outside, ['shield_bash'], '盾打ち (#51) is the prototype\'s one kind outside the forty');
  assert.equal(adopt(DeckBuilder.Prototype(), forty).Total, 18, 'the bug: two 盾打ち refused');
  const owned = DeckBuilder.WithPrototypeKinds(forty);
  assert.equal(owned.length, 41);
  assert.deepEqual(owned.slice(0, 40), [...forty], 'the forty first, in their order');
  assert.equal(owned[40], 'shield_bash');
  assert.deepEqual(DeckBuilder.WithPrototypeKinds(owned), owned, 'idempotent');
  assert.deepEqual(DeckBuilder.WithPrototypeKinds(null), PrototypeDeck.Kinds.map((d) => d.Id));
  for (const limits of [undefined, Survivor]) {
    const deck = adopt(DeckBuilder.Prototype(limits), owned, limits);
    assert.equal(deck.Total, 20);
    assert.equal(deck.IsValid, true);
    assert.equal(deck.Save(), DeckBuilder.Prototype().Save());
  }
  // The C# Expedition owns the start deck's kinds anyway (BeginLife): the same set.
  const ex = new Expedition(null, DeckBuilder.Prototype().Build());
  assert.deepEqual([...ex.Owned].sort(), [...owned].sort());
});

// ------------------------------------------------------------------ the hand-off (出立する → exploration → battle)

/** The departure's Loadout as the saved start keeps it: toRunLoadout, then plain data and back (life.js). */
function savedRunLoadout(loadout, toolSlots) {
  return runLoadoutFromData(JSON.parse(JSON.stringify(runLoadoutToData(toRunLoadout(loadout, toolSlots)))));
}

/** Steps the life to its first fight (fresh nodes first) and returns the expedition's EncounterBattle. */
function toFirstFight(ex) {
  const s = ex.Session;
  for (let guard = 0; guard < 200 && !s.HasPendingBattle; guard++) {
    if (s.HasPendingStep) { s.Act(ScreenAction.ConfirmStep); continue; }
    const st = s.State;
    const open = st.Map.Nodes.filter((n) => ExplorationReducer.CanStep(st, n.Id));
    const fresh = open.filter((n) => !st.HasResolved(n.Id));
    s.Step((fresh.length ? fresh : open)[0].Id);
  }
  assert.equal(s.HasPendingBattle, true, 'a fight waits');
  return ex.StartEncounter();
}

/** A survivor's 出立 as the screen builds it: 45 cards, 4 tools (the 4th the bonus's), a consumable, a talent. */
function survivorDeparture() {
  const limits = DeckLimits.From(startLife(survivorLineage()));
  const owned = DeckBuilder.WithPrototypeKinds(Expedition.InitialKinds);
  const deck = new DeckBuilder(owned, limits);
  for (const card of DeckBuilder.Prototype(limits).Build()) deck.Add(card.Def.Id);
  for (const id of owned) { while (deck.Total < 45 && deck.Add(id)); if (deck.Total >= 45) break; }
  const sup = DepartureSupplies.NewBuilder(limits);
  for (const id of ['bosho_no_men', 'bosho_no_men', 'maai_no_kutsu', 'ikitsugi_no_fue']) assert.equal(sup.AddTool(id), true, id);
  assert.equal(sup.AddConsumable('yakuso'), true);
  assert.equal(sup.ChooseTalent(deck, deck.Build()[0].Def.Id), true);
  assert.equal(sup.CanSetOut(deck), true);
  return { limits, deck, departure: sup.Build(deck) };
}

test('L-2 hand-off: a survivor\'s 45 cards and 4 tools reach the exploration and the battle launch', () => {
  const { limits, departure } = survivorDeparture();
  assert.equal(limits.Equals(Survivor), true);
  assert.equal(departure.Deck.length, 45);
  const carry = DepartureSupplies.Carry(departure, limits);
  assert.deepEqual(carry.battleToolIds, ['maai_no_kutsu', 'ikitsugi_no_fue']);
  assert.deepEqual(carry.consumableIds, ['yakuso']);
  assert.equal(carry.loadout.ToolSlotCount, 4);
  // the exploration's RunLoadout (WORLD contract), through a save: 4 tools in 4 slots
  const runLoadout = savedRunLoadout(carry.loadout, limits.ToolSlots);
  assert.equal(runLoadout.ToolSlotCount, 4);
  assert.deepEqual(runLoadout.Tools.map((t) => t.Id), ['bosho_no_men', 'bosho_no_men', 'maai_no_kutsu', 'ikitsugi_no_fue']);
  const begin = (options) => new Expedition(new RunStart({ Seed: 20261020n, Loadout: runLoadout }), departure.Deck,
    carry.battleToolIds, departure.TalentCardId, null, null, options);
  // without the limits the C#'s Expedition refuses the 45 cards: the router must forward them
  assert.throws(() => begin(null), /Deck has 45 cards; the maximum is 40\./);
  const ex = begin({ limits });
  assert.equal(ex.Deck.length, 45);
  // ... and they ride to the battle: the launch carries them and its deck passes Cards.Validate with them
  const launch = toFirstFight(ex).NextLaunch();
  assert.equal(launch.Limits, limits);
  assert.equal(launch.Deck.length, 45);
  assert.equal(Cards.Validate(launch.Deck, launch.Limits).Ok, true);
  assert.deepEqual(Cards.Validate(launch.Deck).Errors, ['Deck has 45 cards; the maximum is 40.']);
});

test('L-3 hand-off: a survivor\'s 45-card deck starts its first fight', () => {
  const { limits, departure } = survivorDeparture();
  const carry = DepartureSupplies.Carry(departure, limits);
  const ex = new Expedition(new RunStart({ Seed: 20261020n, Loadout: savedRunLoadout(carry.loadout, limits.ToolSlots) }),
    departure.Deck, carry.battleToolIds, departure.TalentCardId, null, null, { limits });
  const battle = toFirstFight(ex);
  assert.doesNotThrow(() => battle.StartNext(true));
});

test('L-5 hand-off: on a first life every tool in the C#\'s 3 slots reaches the exploration', () => {
  const deck = DeckBuilder.Prototype();
  for (const tool of ItemCatalogue.Tools) {
    const sup = DepartureSupplies.NewBuilder();
    assert.equal(sup.AddTool(tool.Id), true);
    assert.equal(sup.AddTool(tool.Id), true);
    assert.equal(sup.AddTool('bosho_no_men'), true);
    assert.equal(sup.AddTool('bosho_no_men'), false, 'the C#\'s 3 slots');
    sup.AddConsumable('joka_no_ko');
    sup.ChooseTalent(deck, 'thrust');
    const departure = sup.Build(deck);
    const carry = DepartureSupplies.Carry(departure);
    assert.equal(carry.loadout.ToolSlotCount, 3);
    assert.deepEqual(carry.battleToolIds, tool.Side === EffectSide.Battle ? [tool.Id, tool.Id] : []);
    const runLoadout = savedRunLoadout(carry.loadout, DeckLimits.Default.ToolSlots);
    assert.equal(runLoadout.Tools.length, 3, tool.Id);
    const ex = new Expedition(new RunStart({ Seed: 7n, Loadout: runLoadout }), departure.Deck, carry.battleToolIds, departure.TalentCardId);
    assert.equal(ex.Deck.length, 20, tool.Id);
  }
});

// ------------------------------------------------------------------ L-9 bad limits

test('L-9 bad limits are refused', () => {
  const range = (e) => e && e.name === 'ArgumentOutOfRangeException';
  assert.throws(() => new DeckLimits({ DeckMin: 0 }), range);
  assert.throws(() => new DeckLimits({ DeckMax: 19 }), (e) => range(e) && e.message.includes("'DeckMax'"));
  assert.throws(() => new DeckLimits({ ToolSlots: -1 }), range);
  assert.throws(() => new DeckLimits({ ConsumableSlots: 2.5 }), range);
  assert.throws(() => new DeckLimits({ DeckMax: '45' }), range);
  assert.deepEqual({ ...Survivor.With({ ToolSlots: 5 }) }, { DeckMin: 20, DeckMax: 45, ToolSlots: 5, ConsumableSlots: 3 });
  assert.equal(Object.isFrozen(Survivor), true);
});
