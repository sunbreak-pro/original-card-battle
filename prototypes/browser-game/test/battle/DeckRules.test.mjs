// Tests for src/core/battle/DeckRules.js (port of unity-port/BattleCore/DeckRules.cs).
// Cases ported from BattleCore.Tests/DeckRulesTests.cs, plus the C# dump (BattleCards.fixture.json)
// for every message, CanAdd answer and the BattleSetup a departure hands a battle.
//
// DeckRules.js imports BattleSetup from TurnLoop.js (as the C# does), and TurnLoop.js needs
// Mastery.js and Enemies.js: the module is loaded dynamically and every test skips until they load.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CardCatalog } from '../../src/core/battle/CardCatalog.js';
import { Cards } from '../../src/core/battle/Cards.js';
import { CardInstance } from '../../src/core/battle/Types.js';
import { Constants } from '../../src/core/battle/Constants.js';
import { SystemRng } from '../../src/core/battle/IRng.js';
import { fixture, errorOf, late } from './_battle_cards.mjs';

const deps = await late(import.meta.url, [
  '../../src/core/battle/DeckRules.js',
  '../../src/core/battle/TurnLoop.js',
  '../../src/core/battle/Enemies.js',
]);
const skip = deps.skip;
const M = deps.mods ?? {};

const AllOwned = CardCatalog.All.map((d) => d.Id);
/** The first `kinds` kinds of the catalog × `copies`. */
const Deck = (kinds, copies) => Cards.BuildDeck(CardCatalog.All.slice(0, kinds), copies);
const Ready = (deck = null, cellsBack = 0) => new M.Departure(deck ?? Deck(10, 2), ['maai_no_kutsu'], ['yakuso'], cellsBack, CardCatalog.All[0].Id);
const V = (v) => ({ Ok: v.Ok, Errors: v.Errors });

// ---- the deck ----

test('NineteenCardsCannotSetOut', { skip }, () => {
  const result = M.DeckRules.Validate(Ready(Deck(10, 2).slice(0, 19)), AllOwned);
  assert.equal(result.Ok, false);
  assert.ok(result.Errors.some((e) => e.includes('minimum is 20')));
});

test('TwentyAndFortyCardsSetOut_FortyOneDoNot', { skip }, () => {
  assert.equal(M.DeckRules.Validate(Ready(Deck(10, 2)), AllOwned).Ok, true, '20');
  assert.equal(M.DeckRules.Validate(Ready(Deck(20, 2)), AllOwned).Ok, true, '40');
  const fortyOne = [...Deck(20, 2), ...Cards.BuildDeck([CardCatalog.All[30]], 1)];
  assert.equal(M.DeckRules.Validate(Ready(fortyOne), AllOwned).Ok, false, '41');
});

test('AFourthCopyDoesNotGoIn / TheFortyFirstCardDoesNotGoIn', { skip }, () => {
  const deck = Deck(7, 3);
  const id = deck[0].Def.Id;
  assert.equal(M.DeckRules.CanAdd(deck, id, AllOwned), false, 'the fourth copy');
  assert.equal(M.DeckRules.CanAdd(deck, CardCatalog.All[7].Id, AllOwned), true, 'a new kind');
  const four = [...deck, new CardInstance(`${id}-3`, deck[0].Def)];
  const result = M.DeckRules.Validate(Ready(four), AllOwned);
  assert.equal(result.Ok, false);
  assert.ok(result.Errors.some((e) => e.includes(`copies of "${id}"`)));
  assert.equal(M.DeckRules.CanAdd(Deck(20, 2), CardCatalog.All[30].Id, AllOwned), false);
});

test('ACardNotOwnedIsRefused (owned kinds as an array or a Set)', { skip }, () => {
  const deck = Deck(10, 2);
  const owned = AllOwned.filter((id) => id !== deck[0].Def.Id);
  for (const kinds of [owned, new Set(owned)]) {
    const result = M.DeckRules.ValidateDeck(deck, kinds);
    assert.equal(result.Ok, false);
    assert.ok(result.Errors.includes(`Card "${deck[0].Def.Id}" is not owned.`));
    assert.equal(M.DeckRules.CanAdd(Deck(1, 1), deck[0].Def.Id, kinds), false);
  }
});

test('TheCatalogsCardChecksStillHold', { skip }, () => {
  const deck = Deck(10, 2);
  assert.deepEqual(M.DeckRules.ValidateDeck(deck, AllOwned).Errors, Cards.Validate(deck).Errors);
});

// ---- the slots ----

test('ThreeToolsAndThreeConsumablesFit_AFourthDoesNot', { skip }, () => {
  const deck = Deck(10, 2);
  const talent = deck[0].Def.Id;
  const three = ['a', 'b', 'a'];
  const four = ['a', 'b', 'c', 'd'];
  assert.equal(M.DeckRules.Validate(new M.Departure(deck, three, three, 0, talent), AllOwned).Ok, true);
  assert.ok(M.DeckRules.Validate(new M.Departure(deck, four, three, 0, talent), AllOwned).Errors.includes('4 tools do not fit 3 slots.'));
  assert.ok(M.DeckRules.Validate(new M.Departure(deck, three, four, 0, talent), AllOwned).Errors.includes('4 consumables do not fit 3 slots.'));
});

// ---- the start ----

test('TheDefaultStartIsCellTwoAtGapThree / ACellBackStartsOnCellOneAtGapFour_AndNoFurther', { skip }, () => {
  const plain = M.Departure.Plain(Deck(10, 2));
  assert.equal(plain.PlayerStartCell, Constants.PlayerStartCell);
  assert.equal(plain.StartGap, Constants.StartGap);
  assert.equal(M.DeckRules.PlayerStartCell(1), 1);
  assert.equal(M.DeckRules.StartGap(1), 4);
  assert.equal(M.DeckRules.PlayerStartCell(2), 1);
  assert.equal(M.DeckRules.StartGap(2), 4);
});

test('TheStartReachesTheBattle_TheEnemyKeepsItsCell', { skip }, () => {
  const enemy = M.Enemies.PolearmWarped;
  const plain = M.TurnLoop.Start(M.Departure.Plain(Deck(10, 2)).SetupFor(enemy, 6), new SystemRng(1)).State;
  const back = M.TurnLoop.Start(Ready(null, 1).SetupFor(enemy, 6), new SystemRng(1)).State;
  assert.equal(plain.Player.Cell, 2);
  assert.equal(plain.Gap, 3);
  assert.equal(back.Player.Cell, 1);
  assert.equal(back.Gap, 4);
  assert.equal(back.Enemy.Cell, plain.Enemy.Cell);
});

test('AStartThatMovesForwardIsRefused', { skip }, () => {
  assert.equal(M.DeckRules.Validate(Ready(null, -1), AllOwned).Ok, false);
});

test('TheDrawPileHoldsTheChosenDeck', { skip }, () => {
  const deck = [...Cards.BuildDeck([CardCatalog.All[13], CardCatalog.All[41], CardCatalog.All[77]], 3), ...Deck(6, 2)];
  const state = M.TurnLoop.Start(Ready(deck).SetupFor(M.Enemies.PolearmWarped, 6), new SystemRng(5)).State;
  const sorted = (cards) => cards.map((c) => c.InstanceId).sort();
  assert.deepEqual(sorted(state.DrawPile), sorted(deck));
});

// ---- the talent ----

test('TheTalentIsRecorded / NoTalentOrOneOutsideTheDeckCannotSetOut', { skip }, () => {
  const deck = Deck(10, 2);
  const departure = new M.Departure(deck, [], [], 0, deck[5].Def.Id);
  assert.equal(M.DeckRules.Validate(departure, AllOwned).Ok, true);
  assert.equal(departure.TalentCardId, deck[5].Def.Id);
  const none = new M.Departure(deck, [], [], 0, null);
  const outside = new M.Departure(deck, [], [], 0, CardCatalog.All[60].Id);
  assert.ok(M.DeckRules.Validate(none, AllOwned).Errors.includes('No talent card is chosen.'));
  assert.ok(M.DeckRules.Validate(outside, AllOwned).Errors.includes(`The talent card "${CardCatalog.All[60].Id}" is not in the deck.`));
});

// ---- The C# dump ----

test('C#: the start for −2..4 cells back', { skip }, () => {
  assert.equal(M.DeckRules.StartCellsBackMax, fixture.DeckRules.StartCellsBackMax);
  for (const [n, cell, gap] of fixture.DeckRules.Start) {
    assert.deepEqual([M.DeckRules.PlayerStartCell(n), M.DeckRules.StartGap(n)], [cell, gap], `${n} back`);
  }
  assert.deepEqual([M.DeckRules.ToolSlots, M.DeckRules.ConsumableSlots], [3, 3]);
});

test('C#: every Validate message, in order', { skip }, () => {
  const d10 = Deck(10, 2);
  const notOwned = AllOwned.filter((id) => id !== 'thrust' && id !== 'brace');
  const R = M.DeckRules;
  const got = {
    ready: V(R.Validate(Ready(), AllOwned)),
    nineteen: V(R.Validate(Ready(Deck(10, 2).slice(0, 19)), AllOwned)),
    forty: V(R.Validate(Ready(Deck(20, 2)), AllOwned)),
    fortyOne: V(R.Validate(Ready([...Deck(20, 2), ...Cards.BuildDeck([CardCatalog.All[30]], 1)]), AllOwned)),
    fourth: V(R.Validate(Ready([...Deck(7, 3), new CardInstance('thrust-3', CardCatalog.Thrust)]), AllOwned)),
    back: V(R.Validate(Ready(null, -1), AllOwned)),
    slots: V(R.Validate(new M.Departure(d10, ['a', 'b', 'c', 'd'], ['a', 'b', 'c', 'd', 'e'], 0, 'thrust'), AllOwned)),
    negative: V(R.Validate(new M.Departure(d10, [], [], 0, 'thrust', { StartStaminaBonus: -1 }), AllOwned)),
    noTalent: V(R.Validate(new M.Departure(d10, [], [], 0, null), AllOwned)),
    emptyTalent: V(R.Validate(new M.Departure(d10, [], [], 0, ''), AllOwned)),
    outsideTalent: V(R.Validate(new M.Departure(d10, [], [], 0, CardCatalog.All[60].Id), AllOwned)),
    notOwned: V(R.Validate(Ready(), notOwned)),
    everything: V(R.Validate(new M.Departure(Deck(10, 2).slice(0, 19), ['a', 'b', 'c', 'd'], [], -2, 'zzz', { FirstHitGuard: -1 }), notOwned)),
    nullTools: errorOf(() => R.Validate(new M.Departure(d10, null, [], 0, 'thrust'), AllOwned)),
    nullConsumables: errorOf(() => R.Validate(new M.Departure(d10, [], null, 0, 'thrust'), AllOwned)),
    nullDeparture: errorOf(() => R.Validate(null, AllOwned)),
    nullOwned: errorOf(() => R.ValidateDeck(d10, null)),
  };
  assert.deepEqual(got, fixture.DeckRules.Validate);
});

test('C#: CanAdd', { skip }, () => {
  const notOwned = AllOwned.filter((id) => id !== 'thrust' && id !== 'brace');
  const R = M.DeckRules;
  assert.deepEqual({
    fourth: R.CanAdd(Deck(7, 3), 'thrust', AllOwned),
    newKind: R.CanAdd(Deck(7, 3), CardCatalog.All[7].Id, AllOwned),
    fortyFirst: R.CanAdd(Deck(20, 2), CardCatalog.All[30].Id, AllOwned),
    notOwned: R.CanAdd(Deck(1, 1), 'thrust', notOwned),
    empty: R.CanAdd(Deck(1, 1), '', AllOwned),
    null: R.CanAdd(Deck(1, 1), null, AllOwned),
    third: R.CanAdd(Deck(1, 2), 'thrust', AllOwned),
  }, fixture.DeckRules.CanAdd);
});

test('C#: Departure.Plain and SetupFor hand the battle the C#\'s BattleSetup', { skip }, () => {
  const d10 = Deck(10, 2);
  const plain = M.Departure.Plain(d10);
  assert.deepEqual([plain.Tools.length, plain.Consumables.length, plain.StartCellsBack, plain.TalentCardId, plain.StartStaminaBonus,
    plain.FirstHitGuard, plain.BossWordWard, plain.PlayerStartCell, plain.StartGap], fixture.DeckRules.Plain);
  const row = (s) => [s.Enemy.Id, s.Deck.length, s.FieldCells, s.StartGap, s.PlayerStartCell, s.PlayerMaxHp, s.PlayerMaxStamina,
    s.MoreEnemies == null, s.PlayerStartHp, s.PlayerStartStamina, s.PlayerStartStaminaBonus, s.FirstHitGuard, s.BossWordWard];
  const full = new M.Departure(d10, ['x'], [], 1, 'thrust', 3, 5, 1);
  const setup = full.SetupFor(M.Enemies.PolearmWarped, 7, 20, 4);
  assert.ok(setup instanceof M.BattleSetup);
  assert.deepEqual(row(setup), fixture.DeckRules.SetupFor);
  assert.equal(setup.Deck, d10, 'the deck itself, shuffled later by TurnLoop.Start');
  assert.deepEqual(row(plain.SetupFor(M.Enemies.PolearmWarped, 6)), fixture.DeckRules.SetupForPlain);
  assert.deepEqual(errorOf(() => plain.SetupFor(null, 6)), fixture.DeckRules.SetupForNull);
});

test('Departure takes C# named arguments; a typo throws', { skip }, () => {
  const d = new M.Departure(Deck(10, 2), [], [], 0, 'thrust', { BossWordWard: 2 });
  assert.deepEqual([d.StartStaminaBonus, d.FirstHitGuard, d.BossWordWard], [0, 0, 2]);
  assert.throws(() => new M.Departure(Deck(10, 2), [], [], 0, 'thrust', { BossWordWard: 2, Bogus: 1 }), /unknown named argument/);
  assert.equal(d.with({ StartCellsBack: 1 }).StartGap, 4);
});
