// Tests for src/core/battle/Cards.js (port of unity-port/BattleCore/Cards.cs).
// Cases ported from BattleCore.Tests/CardsTests.cs, plus the C# dump (BattleCards.fixture.json):
// shuffles under SystemRng / SeededRng / FixedRng, a 192-row draw matrix, the validation messages
// in their order, the enemy-action check and the exceptions.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Cards, DrawResult, DeckValidation } from '../../src/core/battle/Cards.js';
import {
  BattleAttribute, CardDef, CardInstance, EnemyActionDef, Face, StanceDef, StanceHook, StatusGrant, StatusKind,
  TargetKind, Trait, TraitCondition, TraitEffect,
} from '../../src/core/battle/Types.js';
import { AttributeRule } from '../../src/core/battle/Attributes.js';
import { Constants } from '../../src/core/battle/Constants.js';
import { Combat } from '../../src/core/battle/Combat.js';
import { SystemRng, SeededRng, FixedRng } from '../../src/core/battle/IRng.js';
import { CardCatalog } from '../../src/core/battle/CardCatalog.js';
import { fixture, Card, TwentyCardDeck, errorOf, ids } from './_battle_cards.mjs';

const A = BattleAttribute;
const Seeded = (seed = 12345) => new SystemRng(seed);
const range = (n) => Array.from({ length: n }, (_, i) => i);

// ---- CardsTests ----

test('BuildDeck_LaysOutCopiesWithUniqueInstanceIds', () => {
  const deck = TwentyCardDeck();
  assert.equal(deck.length, 20);
  assert.equal(new Set(ids(deck)).size, 20);
  assert.equal(deck.filter((c) => c.Def.Id === 'card_0').length, 2);
  assert.deepEqual(ids(deck).slice(0, 4), ['card_0-0', 'card_0-1', 'card_1-0', 'card_1-1']);
  assert.ok(deck.every((c) => c instanceof CardInstance));
});

test('ATurnDrawsSix', () => {
  const result = Cards.Draw(TwentyCardDeck(), [], [], Combat.DrawCount(), Seeded());
  assert.ok(result instanceof DrawResult);
  assert.equal(result.Hand.length, 6);
  assert.equal(result.DrawPile.length, 14);
  assert.deepEqual(result.DiscardPile, []);
  assert.equal(result.Drawn, 6);
});

test('TurnEndThrowsTheWholeHandAway', () => {
  const drawn = Cards.Draw(TwentyCardDeck(), [], [], 5, Seeded());
  const { Hand: hand, DiscardPile: discard } = Cards.DiscardHand(drawn.Hand, drawn.DiscardPile);
  assert.deepEqual(hand, []);
  assert.equal(discard.length, 5);
  assert.deepEqual(ids(discard), ids(drawn.Hand), 'the hand goes on top in its order');
});

test('AnEmptyDrawPileReshufflesTheDiscardPile', () => {
  const deck = TwentyCardDeck();
  const result = Cards.Draw(deck.slice(0, 2), deck.slice(2), [], 5, Seeded());
  assert.equal(result.Reshuffled, true);
  assert.equal(result.Hand.length, 5);
  assert.equal(result.DrawPile.length, 15);
  assert.deepEqual(result.DiscardPile, []);
});

test('ADeckThatRunsOutEntirely_DrawsWhatIsLeft', () => {
  const result = Cards.Draw(TwentyCardDeck().slice(0, 3), [], [], 5, Seeded());
  assert.equal(result.Drawn, 3);
  assert.equal(result.Hand.length, 3);
});

test('ADrawPastTheHandLimitGoesToTheDiscardPile', () => {
  const deck = TwentyCardDeck();
  const result = Cards.Draw(deck.slice(Constants.HandLimit), [], deck.slice(0, Constants.HandLimit), 2, Seeded());
  assert.equal(result.Hand.length, Constants.HandLimit);
  assert.equal(result.OverflowToDiscard, 2);
  assert.equal(result.DiscardPile.length, 2);
});

test('NoCardIsEverLost', () => {
  const result = Cards.Draw(TwentyCardDeck(), [], [], 5, Seeded());
  assert.equal(result.Hand.length + result.DrawPile.length + result.DiscardPile.length, 20);
});

test('TheSameSeedShufflesTheSameWay / DrawsTheSameHand / ADifferentSeedShufflesDifferently / ShuffleKeepsEveryCard', () => {
  const deck = TwentyCardDeck();
  assert.deepEqual(ids(Cards.Shuffle(deck, new SystemRng(999))), ids(Cards.Shuffle(deck, new SystemRng(999))));
  const handA = Cards.Draw(Cards.Shuffle(deck, new SystemRng(7)), [], [], 5, new SystemRng(7));
  const handB = Cards.Draw(Cards.Shuffle(deck, new SystemRng(7)), [], [], 5, new SystemRng(7));
  assert.deepEqual(ids(handA.Hand), ids(handB.Hand));
  assert.notDeepEqual(ids(Cards.Shuffle(deck, new SystemRng(1))), ids(Cards.Shuffle(deck, new SystemRng(2))));
  assert.deepEqual([...ids(Cards.Shuffle(deck, Seeded()))].sort(), [...ids(deck)].sort());
  assert.deepEqual(ids(deck).slice(0, 2), ['card_0-0', 'card_0-1'], 'the input is not touched');
});

test('TwentyCardsOfTenKindsPasses', () => {
  const result = Cards.Validate(TwentyCardDeck());
  assert.ok(result instanceof DeckValidation);
  assert.equal(result.Ok, true, result.Errors.join(' / '));
});

test('ADeckUnderTwentyIsRefused / ADeckOverFortyIsRefused / AFourthCopyOfOneKindIsRefused', () => {
  const under = Cards.Validate(TwentyCardDeck().slice(0, 19));
  assert.equal(under.Ok, false);
  assert.ok(under.Errors.some((e) => e.includes('minimum')));
  const over = Cards.Validate(Cards.BuildDeck(range(21).map((i) => Card(`k${i}`)), 2));
  assert.equal(over.Ok, false);
  assert.ok(over.Errors.some((e) => e.includes('maximum')));
  const four = Cards.Validate(Cards.BuildDeck(range(5).map((i) => Card(`k${i}`)), 4));
  assert.equal(four.Ok, false);
  assert.ok(four.Errors.some((e) => e.includes('copies')));
});

/** A card with a stance face (§4). */
const StanceCard = (id) => Card(id, { attributes: A.Stance, face: new Face({ Stance: new StanceDef(StanceHook.TurnStart, { Guard: 1 }) }) });

test('ManyStanceCards_PassTheDeckCheck_ThereIsNoCapAnyMore', () => {
  const plain = range(8).map((i) => Card(`k${i}`));
  const stances = range(4).map((i) => StanceCard(`s${i}`));
  const deck = [...Cards.BuildDeck(plain, 2), ...Cards.BuildDeck(stances, 3)];
  const result = Cards.Validate(deck);
  assert.equal(deck.length, 28);
  assert.equal(deck.filter((c) => Cards.IsStanceCard(c.Def)).length, 12);
  assert.equal(result.Ok, true, result.Errors.join(' / '));
});

test('TheFourteenStanceCards_CountAsStance_AndEachStandsAlone', () => {
  const stances = CardCatalog.All.filter((c) => Cards.IsStanceCard(c));
  const rewritten = ['purge_flash', 'abyss_stance', 'wolf_stance', 'iron_wall', 'spear_wall', 'anchor_stance', 'root_stride', 'mist_step', 'root_bind', 'keen_eye'];
  assert.equal(Cards.IsStanceCard(CardCatalog.IronWall), true);
  assert.equal(Cards.IsStanceCard(CardCatalog.Thrust), false);
  assert.equal(stances.length, 14);
  assert.ok(rewritten.every((id) => stances.some((c) => c.Id === id)), 'the ten #257 rewrote are among them');
  assert.deepEqual(stances.filter((c) => !AttributeRule.StanceStandsAlone(c.Attributes, c.Face)).map((c) => c.Id), []);
  assert.deepEqual(stances.filter((c) => c.Attributes !== A.Stance).map((c) => c.Id), [], 'a stance declares nothing else');
  assert.deepEqual(stances.filter((c) => c.Targets !== TargetKind.Self).map((c) => c.Id), [], 'a stance aims at nobody');
  assert.ok(stances.every((c) => Cards.StanceStandsAlone(c)));
});

test('AStanceThatAlsoAttacksOrMoves_IsRefused', () => {
  const attacks = Card('attacking_stance', { attributes: A.Attack | A.Stance, face: new Face({ Power: 8, Stance: new StanceDef(StanceHook.TurnStart, { Guard: 1 }) }) });
  const moves = Card('moving_stance', { attributes: A.Stance, face: new Face({ Move: -1, Stance: new StanceDef(StanceHook.TurnStart, { Guard: 1 }) }) });
  const stanceFaceOnly = Card('undeclared_stance', { attributes: A.Guard, face: new Face({ Guard: 3, Stance: new StanceDef(StanceHook.TurnStart, { Guard: 1 }) }) });
  const plain = range(9).map((i) => Card(`k${i}`));
  for (const bad of [attacks, moves, stanceFaceOnly]) {
    const result = Cards.Validate(Cards.BuildDeck([...plain, bad], 2));
    assert.deepEqual(result.Errors, [`Card "${bad.Id}" is a stance and also declares another attribute or moves; a stance stands alone.`], bad.Id);
  }
  assert.equal(Cards.StanceStandsAlone(StanceCard('pure')), true);
});

test('TheEightyCards_FoldToThirtySixNineteenElevenFourteen', () => {
  const counts = new Map();
  for (const c of CardCatalog.All) counts.set(c.Attribute, (counts.get(c.Attribute) ?? 0) + 1);
  assert.equal(CardCatalog.All.length, 80);
  assert.equal(counts.get(A.Attack), 36);
  assert.equal(counts.get(A.Guard), 19);
  assert.equal(counts.get(A.Skill), 11);
  assert.equal(counts.get(A.Stance), 14);
  assert.equal(counts.has(A.None), false, 'no card folds to nothing');
});

test('AMultiHitCard_WithAnOpponentStatus_OnTheFaceOrInATrait_IsRefused', () => {
  const onFace = Card('twin_bleed', { face: new Face({ Power: 4, Hits: 2, Statuses: [new StatusGrant(StatusKind.Bleed, 1)] }) });
  const inTrait = Card('twin_fragile', {
    face: new Face({ Power: 4, Hits: 2 }),
    trait: new Trait(TraitCondition.FirstPlay, TraitEffect.Status, { Grant: new StatusGrant(StatusKind.Fragile, 1) }),
  });
  const inExtraTrait = new CardDef('twin_slow', 'twin_slow', A.Attack, 1, new Face({ Power: 4, Hits: 2 }), {
    ExtraTrait: new Trait(TraitCondition.FirstPlay, TraitEffect.Status, { Grant: new StatusGrant(StatusKind.Slow, 1) }),
  });
  assert.equal(Cards.MultiHitCarriesNoFoeStatus(onFace), false);
  assert.equal(Cards.MultiHitCarriesNoFoeStatus(inTrait), false);
  assert.equal(Cards.MultiHitCarriesNoFoeStatus(inExtraTrait), false);
});

test('AMultiHitCard_MayGiveItsOwnStatus_AndASingleBlowMayGiveTheOpponentOne', () => {
  const selfOnly = Card('twin_empower', {
    face: new Face({ Power: 4, Hits: 2, Statuses: [new StatusGrant(StatusKind.Empower, 1, { OnSelf: true })] }),
    trait: new Trait(TraitCondition.FirstPlay, TraitEffect.Status, { Grant: new StatusGrant(StatusKind.Regen, 1, { OnSelf: true }) }),
  });
  const singleBlow = Card('bleed_cut', { face: new Face({ Power: 6, Statuses: [new StatusGrant(StatusKind.Bleed, 1)] }) });
  const twoHitsStance = Card('twin_stance', {
    face: new Face({ Power: 4, Hits: 2, Stance: new StanceDef(StanceHook.StatusOnAttack, { Status: StatusKind.Bleed, StatusStacks: 1 }) }),
    attributes: A.Attack | A.Stance,
  });
  assert.equal(Cards.MultiHitCarriesNoFoeStatus(selfOnly), true);
  assert.equal(Cards.MultiHitCarriesNoFoeStatus(singleBlow), true);
  assert.equal(Cards.MultiHitCarriesNoFoeStatus(twoHitsStance), true, 'rule 2: a stance gives it blow by blow');
  // The three-argument overload, and an attribute set without Attack.
  assert.equal(Cards.MultiHitCarriesNoFoeStatus(A.Attack, new Face({ Power: 4, Hits: 2 }), []), true);
  assert.equal(Cards.MultiHitCarriesNoFoeStatus(A.Guard, new Face({ Power: 4, Hits: 2, Statuses: [new StatusGrant(StatusKind.Bleed, 1)] }), []), true);
  assert.equal(Cards.MultiHitCarriesNoFoeStatus(A.Attack | A.Guard, new Face({ Power: 4, Hits: 2, Statuses: [new StatusGrant(StatusKind.Bleed, 1)] }), []), false);
});

test('ADeckHoldingAMultiHitCardWithAnOpponentStatus_IsRefused', () => {
  const bad = Card('twin_bleed', { face: new Face({ Power: 4, Hits: 2, Statuses: [new StatusGrant(StatusKind.Bleed, 1)] }) });
  const result = Cards.Validate(Cards.BuildDeck([...range(9).map((i) => Card(`k${i}`)), bad], 2));
  assert.equal(result.Ok, false);
  assert.deepEqual(result.Errors, ['Card "twin_bleed" strikes more than once and gives the opponent a status.']);
});

// ---- The C# dump ----

test('C#: Shuffle under SystemRng (seeded .NET Random), SeededRng and FixedRng', () => {
  const deck = TwentyCardDeck();
  const got = {};
  for (const seed of [1, 2, 7, 999, 12345, -1, 0]) got[`System:${seed}`] = ids(Cards.Shuffle(deck, new SystemRng(seed)));
  for (const seed of [1, 42, 7]) got[`Seeded:${seed}`] = ids(Cards.Shuffle(deck, new SeededRng(seed)));
  got['Fixed:0.9999999'] = ids(Cards.Shuffle(deck, new FixedRng(0.9999999)));
  got['Fixed:0'] = ids(Cards.Shuffle(deck, new FixedRng(0)));
  assert.deepEqual(got, fixture.Shuffles);
});

test('C#: Draw over 192 pile shapes (draw × discard × hand × count), SeededRng', () => {
  const deck30 = Cards.BuildDeck(range(10).map((i) => Card(`k${i}`, { face: new Face({ Power: i }) })), 3);
  const json = (r) => ({
    Hand: ids(r.Hand), DrawPile: ids(r.DrawPile), DiscardPile: ids(r.DiscardPile),
    Drawn: r.Drawn, OverflowToDiscard: r.OverflowToDiscard, Reshuffled: r.Reshuffled,
  });
  assert.equal(fixture.Draws.length, 192);
  for (const row of fixture.Draws) {
    const drawPile = deck30.slice(0, row.D);
    const discard = deck30.slice(row.D, row.D + row.X);
    const hand = deck30.slice(row.D + row.X, row.D + row.X + row.H);
    assert.deepEqual(json(Cards.Draw(drawPile, discard, hand, row.Count, new SeededRng(row.Seed))), row.Result, JSON.stringify([row.D, row.X, row.H, row.Count]));
  }
  const deck = TwentyCardDeck();
  assert.deepEqual(json(Cards.Draw(deck, [], [], 6, new SystemRng(12345))), fixture.DrawSix);
  assert.deepEqual(json(Cards.Draw(deck.slice(0, 2), deck.slice(2), [], 5, new SystemRng(12345))), fixture.DrawReshuffle);
});

test('C#: Validate messages, in the C# order (ordinal id sort, first-seen groups)', () => {
  const plain9 = range(9).map((i) => Card(`k${i}`));
  const decks = {
    twenty: TwentyCardDeck(),
    nineteen: TwentyCardDeck().slice(0, 19),
    fortyTwo: Cards.BuildDeck(range(21).map((i) => Card(`k${i}`)), 2),
    fourCopies: Cards.BuildDeck(range(5).map((i) => Card(`k${i}`)), 4),
    manyStances: [...Cards.BuildDeck(range(8).map((i) => Card(`k${i}`)), 2), ...Cards.BuildDeck(range(4).map((i) => StanceCard(`s${i}`)), 3)],
    empty: [],
    attackingStance: Cards.BuildDeck([...plain9, Card('attacking_stance', { attributes: A.Attack | A.Stance, face: new Face({ Power: 8, Stance: new StanceDef(StanceHook.TurnStart, { Guard: 1 }) }) })], 2),
    movingStance: Cards.BuildDeck([...plain9, Card('moving_stance', { attributes: A.Stance, face: new Face({ Move: -1, Stance: new StanceDef(StanceHook.TurnStart, { Guard: 1 }) }) })], 2),
    undeclaredStance: Cards.BuildDeck([...plain9, Card('undeclared_stance', { attributes: A.Guard, face: new Face({ Guard: 3, Stance: new StanceDef(StanceHook.TurnStart, { Guard: 1 }) }) })], 2),
    twinBleed: Cards.BuildDeck([...plain9, Card('twin_bleed', { face: new Face({ Power: 4, Hits: 2, Statuses: [new StatusGrant(StatusKind.Bleed, 1)] }) })], 2),
  };
  const messy = [
    ...Cards.BuildDeck([Card('zeta')], 4),
    ...Cards.BuildDeck([Card('Beta', { column: 0 }), Card('alpha', { column: 5 }), Card('_under', { column: 9 })], 1),
    ...Cards.BuildDeck([Card('stance_hit', { attributes: A.Attack | A.Stance, face: new Face({ Power: 3, Stance: new StanceDef(StanceHook.TurnStart, { Guard: 1 }) }) })], 2),
    ...Cards.BuildDeck([Card('twin_frag', { face: new Face({ Power: 4, Hits: 2 }), trait: new Trait(TraitCondition.FirstPlay, TraitEffect.Status, { Grant: new StatusGrant(StatusKind.Fragile, 1) }) })], 1),
    new CardInstance('dup-0', Card('dup_a')),
    new CardInstance('dup-0', Card('dup_b')),
    new CardInstance('Dup-0', Card('dup_c')),
    new CardInstance('zeta-0', Card('zeta')),
  ];
  decks.messy = messy;
  for (const [name, deck] of Object.entries(decks)) {
    const expected = fixture.Validate[name];
    assert.deepEqual(ids(deck), expected.Ids, `${name}: the same deck as the C# built`);
    const v = Cards.Validate(deck);
    assert.deepEqual({ Ok: v.Ok, Errors: v.Errors }, expected.Result, name);
  }
});

test('C#: ValidateEnemyAction and the enemy-action overload', () => {
  const actions = {
    fine: new EnemyActionDef('fine', 'fine', A.Attack, 2, new Face({ Power: 5 }), null, TargetKind.One, ''),
    col0: new EnemyActionDef('col0', 'col0', A.Attack, 0, new Face({ Power: 5 }), null, TargetKind.One, ''),
    stanceHit: new EnemyActionDef('stance_hit', 'x', A.Attack | A.Stance, 5, new Face({ Power: 5, Stance: new StanceDef(StanceHook.OnHit, { Guard: 1 }) }), null, TargetKind.One, ''),
    twinTrait: new EnemyActionDef('twin', 'x', A.Attack, 2, new Face({ Power: 3, Hits: 2 }), new Trait(TraitCondition.Unguarded, TraitEffect.Status, { Grant: new StatusGrant(StatusKind.Slow, 1) }), TargetKind.One, ''),
    twinSelf: new EnemyActionDef('twin_self', 'x', A.Attack, 2, new Face({ Power: 3, Hits: 2 }), new Trait(TraitCondition.Unguarded, TraitEffect.Status, { Grant: new StatusGrant(StatusKind.Regen, 1, { OnSelf: true }) }), TargetKind.One, ''),
  };
  for (const [name, action] of Object.entries(actions)) {
    assert.deepEqual({ Errors: Cards.ValidateEnemyAction(action), MultiHit: Cards.MultiHitCarriesNoFoeStatus(action) }, fixture.EnemyActions[name], name);
  }
});

test('C#: exceptions', () => {
  const twenty = TwentyCardDeck();
  assert.deepEqual(errorOf(() => Cards.BuildDeck([Card('a')], 0)), fixture.Errors.BuildDeck0);
  assert.deepEqual(errorOf(() => Cards.BuildDeck(null, 1)), fixture.Errors.BuildDeckNull);
  assert.deepEqual(errorOf(() => Cards.Shuffle(twenty, null)), fixture.Errors.ShuffleNullRng);
  assert.deepEqual(errorOf(() => Cards.Draw(twenty, twenty, null, 1, new SeededRng(1))), fixture.Errors.DrawNullHand);
  assert.deepEqual(errorOf(() => Cards.IsStanceCard(null)), fixture.Errors.IsStanceNull);
  assert.deepEqual(errorOf(() => Cards.MultiHitCarriesNoFoeStatus(null)), fixture.Errors.IsStanceNull, 'the CardDef overload names def');
  assert.match(errorOf(() => Cards.MultiHitCarriesNoFoeStatus(A.Attack, null, [])).Message, /Parameter 'face'/);
  assert.match(errorOf(() => Cards.MultiHitCarriesNoFoeStatus(A.Attack, new Face(), null)).Message, /Parameter 'traits'/);
  assert.match(errorOf(() => Cards.Validate(null)).Message, /Parameter 'deck'/);
  assert.match(errorOf(() => Cards.DiscardHand([], null)).Message, /Parameter 'discardPile'/);
  assert.match(errorOf(() => Cards.ValidateEnemyAction(null)).Message, /Parameter 'action'/);
});
