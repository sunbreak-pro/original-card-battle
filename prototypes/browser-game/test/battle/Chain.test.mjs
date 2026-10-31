// Tests for src/core/battle/Chain.js (port of unity-port/BattleCore/Chain.cs).
// Cases ported from BattleCore.Tests/ChainTests.cs, plus the C# dump (BattleCards.fixture.json) for
// the rest table, the max stamina by 瘴気 and the chain total. The battle-driven cases need
// TurnLoop.js (and its Mastery.js / Enemies.js) and skip until those load.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Chain, BattleTally } from '../../src/core/battle/Chain.js';
import { Actor, BattleAttribute, CardInstance, Face, GameResult, Trait, TraitCondition, TraitEffect } from '../../src/core/battle/Types.js';
import { CardPlayed, TraitEvaluated, TurnStarted } from '../../src/core/battle/BattleEvents.js';
import { TraitOutcome } from '../../src/core/battle/Traits.js';
import { Constants } from '../../src/core/battle/Constants.js';
import { CardCatalog, PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { Cards } from '../../src/core/battle/Cards.js';
import { FixedRng, SeededRng } from '../../src/core/battle/IRng.js';
import { fixture, Card, errorOf, late } from './_battle_cards.mjs';

const A = BattleAttribute;
const NoShuffle = new FixedRng(0.9999999);

/** A finished state as far as Chain reads one (BattleState.EnemyDef / Result / Turn / Player). */
const finishedState = ({ enemyId = 'polearm_warped', result = GameResult.Won, turn = 7, hp = 31, maxHp = 50, stamina = 4 } = {}) => ({
  EnemyDef: { Id: enemyId }, Result: result, Turn: turn, Player: { Hp: hp, MaxHp: maxHp, Stamina: stamina },
});

/** The four cards of TheTally_CountsATwoFaceCardOnce_AsTheAttributeItFoldsTo, as events. */
function twoFaceEvents() {
  const strikeAndBrace = Card('strike_brace', { attributes: A.Attack | A.Guard, face: new Face({ Power: 4, Guard: 3 }) });
  const braceAndDraw = Card('brace_draw', { attributes: A.Guard | A.Skill, face: new Face({ Guard: 3, Draw: 1 }) });
  const stepBack = Card('step_back', { attributes: A.None, face: new Face({ Guard: 4, Move: -1 }) });
  const foeStrike = Card('foe_strike', { attributes: A.Attack | A.Guard, face: new Face({ Power: 4, Guard: 3 }) });
  return [
    new CardPlayed(Actor.Player, new CardInstance('strike_brace-0', strikeAndBrace), 2),
    new CardPlayed(Actor.Player, new CardInstance('brace_draw-0', braceAndDraw), 2),
    new CardPlayed(Actor.Player, new CardInstance('step_back-0', stepBack), 2),
    new CardPlayed(Actor.Enemy, new CardInstance('foe_strike-0', foeStrike), 2),
  ];
}

const sumValues = (map) => [...map.values()].reduce((s, n) => s + n, 0);

// ---- Rules with no battle ----

test('TheRest_GivesBackThirtyPercentAndFullStamina', () => {
  for (const [hp, expectedHp, expectedStamina] of [[20, 35, 10], [40, 50, 10], [50, 50, 10], [1, 16, 10]]) {
    const { Hp, Stamina } = Chain.Rest(hp, Constants.PlayerMaxHp, Constants.BaseMaxStamina);
    assert.equal(Hp, expectedHp, `hp ${hp}`);
    assert.equal(Stamina, expectedStamina);
  }
});

test('TheMaxStamina_FallsByOneEveryTwentyPercentOfMiasma_ToSixAtMost', () => {
  assert.equal(Chain.MaxStaminaAt(19), 10);
  assert.equal(Chain.MaxStaminaAt(20), 9);
  assert.equal(Chain.MaxStaminaAt(80), 6, '§13: max stamina 6 at 80%');
  assert.equal(Chain.MaxStaminaAt(100), 6, '−4 at most');
});

test('C#: Rest over HP × max HP (rounded away from zero), MaxStaminaAt −5..105, the constants', () => {
  for (const [hp, maxHp, restedHp, restedStamina] of fixture.Rest) {
    assert.deepEqual(Chain.Rest(hp, maxHp, 10), { Hp: restedHp, Stamina: restedStamina }, `hp ${hp} of ${maxHp}`);
  }
  for (const [percent, max] of fixture.MaxStaminaAt) assert.equal(Chain.MaxStaminaAt(percent), max, `${percent}%`);
  assert.deepEqual([Chain.MiasmaMax, Chain.MiasmaStepPercent, Chain.MiasmaMaxPenalty], fixture.ChainConsts);
});

test('BattleTally.Order and CountOf', () => {
  assert.deepEqual(BattleTally.Order, [A.Attack, A.Guard, A.Skill, A.Stance]);
  const t = new BattleTally('x', GameResult.Won, 1, 1, 1, 2, new Map([[A.Guard, 2]]), 0);
  assert.equal(t.CountOf(A.Guard), 2);
  assert.equal(t.CountOf(A.Attack), 0);
});

test('C#: Total adds two battles up (attribute order: first seen, in Order)', () => {
  const t1 = new BattleTally('a', GameResult.Won, 3, 40, 50, 5, new Map([[A.Guard, 2], [A.Attack, 3]]), 2);
  const t2 = new BattleTally('b', GameResult.Lost, 4, 0, 50, 6, new Map([[A.Stance, 1], [A.Skill, 2], [A.Attack, 3]]), 1);
  const json = (t) => [t.EnemyId, t.Result, t.Turns, t.HpLeft, t.MaxHp, t.CardsPlayed, [...t.Attributes].map(([k, v]) => [k, v]), t.TraitsFired];
  assert.deepEqual(json(Chain.Total([t1, t2])), fixture.Total);
  assert.deepEqual(json(Chain.Total([t2, t1])), fixture.Total2);
  assert.deepEqual(errorOf(() => Chain.Total([])), fixture.Errors.TotalEmpty);
  assert.match(errorOf(() => Chain.Total(null)).Message, /Parameter 'tallies'/);
});

test('TheTally_CountsATwoFaceCardOnce_AsTheAttributeItFoldsTo (on a stand-in finished state)', () => {
  const state = finishedState();
  const events = [...twoFaceEvents(), new TurnStarted(Actor.Player, 1)];
  const tally = Chain.Tally(state, events);
  const total = Chain.Total([tally, tally]);
  assert.equal(tally.CardsPlayed, 3);
  assert.equal(tally.CountOf(A.Attack), 1);
  assert.equal(tally.CountOf(A.Guard), 2);
  assert.equal(tally.CountOf(A.Skill), 0, 'the skill face of 防御 + スキル is not counted again');
  assert.equal(tally.CountOf(A.Stance), 0);
  assert.equal(sumValues(tally.Attributes), tally.CardsPlayed);
  assert.equal(total.CountOf(A.Attack), 2);
  assert.equal(total.CountOf(A.Guard), 4);
  assert.equal(sumValues(total.Attributes), total.CardsPlayed, 'a chain adds up the same way');
  assert.deepEqual([tally.EnemyId, tally.Result, tally.Turns, tally.HpLeft, tally.MaxHp], ['polearm_warped', GameResult.Won, 7, 31, 50]);
  assert.equal(total.EnemyId, '');
  assert.equal(total.Turns, 14);
});

test('Tally counts the player\'s triggered traits only, from any iterable', () => {
  const trait = new Trait(TraitCondition.FirstPlay, TraitEffect.Draw, 1);
  const fired = new TraitOutcome(true, { Draw: 1 });
  function* events() {
    yield new TraitEvaluated(Actor.Player, 'a-0', trait, fired);
    yield new TraitEvaluated(Actor.Player, 'b-0', trait, TraitOutcome.None);
    yield new TraitEvaluated(Actor.Enemy, 'c', trait, fired);
    yield new TraitEvaluated(Actor.Player, 'd-0', trait, fired);
  }
  const tally = Chain.Tally(finishedState(), events());
  assert.equal(tally.TraitsFired, 2);
  assert.equal(tally.CardsPlayed, 0);
  assert.equal(tally.Attributes.size, 0);
  assert.match(errorOf(() => Chain.Tally(null, [])).Message, /Parameter 'finished'/);
  assert.match(errorOf(() => Chain.Tally(finishedState(), null)).Message, /Parameter 'events'/);
});

test('Carry hands on HP and current stamina', () => {
  assert.deepEqual(Chain.Carry(finishedState({ hp: 23, stamina: 4 })), { Hp: 23, Stamina: 4 });
  assert.match(errorOf(() => Chain.Carry(null)).Message, /Parameter 'finished'/);
});

// ---- With the turn loop (TurnLoop.js needs Mastery.js and Enemies.js) ----

const loop = await late(import.meta.url, ['../../src/core/battle/TurnLoop.js', '../../src/core/battle/Enemies.js']);

test('ACarriedStart_BeginsWithThatHpAndStamina_ClampedToTheMaximum', { skip: loop.skip }, () => {
  const { TurnLoop, BattleSetup, Enemies } = loop.mods;
  const deck = PrototypeDeck.Build();
  const carried = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 6, { PlayerStartHp: 23, PlayerStartStamina: 4 }), NoShuffle).State;
  assert.equal(carried.Player.Hp, 23);
  assert.equal(carried.Player.MaxHp, Constants.PlayerMaxHp);
  assert.equal(carried.Player.Stamina, 4);
  const fresh = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 6), NoShuffle).State;
  assert.equal(fresh.Player.Hp, Constants.PlayerMaxHp, 'null is full');
  assert.equal(fresh.Player.Stamina, Constants.BaseMaxStamina);
});

test('TheNextBattle_StartsFromAFreshShuffle_WithNoStanceStatusOrGuard', { skip: loop.skip }, () => {
  const { TurnLoop, BattleSetup, Enemies } = loop.mods;
  const deck = Cards.BuildDeck([CardCatalog.RockStance, CardCatalog.Focus], 1);
  for (let i = 0; i < 18; i++) deck.push(new CardInstance(`brace-${i}`, CardCatalog.Brace));
  let state = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 6), NoShuffle).State;
  state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
  state = TurnLoop.PlayCard(state, 'rock_stance-0', NoShuffle).State;
  state = TurnLoop.PlayCard(state, 'focus-0', NoShuffle).State;
  assert.equal(state.Player.StanceList.length, 1);
  assert.equal(state.Exiled.length, 1);

  const { Hp: hp, Stamina: stamina } = Chain.Carry(state);
  const next = TurnLoop.Start(new BattleSetup(Enemies.ShadowHound, deck, 6, { PlayerStartHp: hp, PlayerStartStamina: stamina }), new SeededRng(3)).State;
  assert.equal(next.Player.Hp, state.Player.Hp);
  assert.equal(next.Player.Stamina, state.Player.Stamina);
  assert.deepEqual(next.Player.StanceList, []);
  assert.equal(next.Player.Statuses.KindCount, 0);
  assert.equal(next.Player.Guard, 0);
  assert.deepEqual(next.Exiled, []);
  assert.deepEqual(next.DrawPile.map((c) => c.InstanceId).sort(), deck.map((c) => c.InstanceId).sort());
  assert.deepEqual(next.Hand, []);
  assert.deepEqual(next.DiscardPile, []);
});

test('TheTally_CountsTheCardsTheirAttributesAndTheTraitsThatHeld', { skip: loop.skip }, () => {
  const { TurnLoop, BattleSetup, PlayRefusal, Enemies } = loop.mods;
  const rng = new SeededRng(11);
  const start = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), 6), rng);
  let state = start.State;
  const events = [...start.Events];
  while (state.Result === GameResult.Ongoing && state.Turn < 60) {
    let step = TurnLoop.BeginPlayerTurn(state, rng);
    events.push(...step.Events);
    state = step.State;
    while (state.Result === GameResult.Ongoing) {
      const c = state.Hand.find((x) => TurnLoop.CanPlay(state, x.InstanceId) === PlayRefusal.None);
      if (c == null) break;
      step = TurnLoop.PlayCard(state, c.InstanceId, rng);
      events.push(...step.Events);
      state = step.State;
    }
    if (state.Result !== GameResult.Ongoing) break;
    step = TurnLoop.EndTurn(state, rng);
    events.push(...step.Events);
    state = step.State;
  }
  const tally = Chain.Tally(state, events);
  const played = events.filter((e) => e instanceof CardPlayed);
  assert.equal(tally.EnemyId, 'polearm_warped');
  assert.equal(tally.Result, state.Result);
  assert.equal(tally.Turns, state.Turn);
  assert.equal(tally.HpLeft, state.Player.Hp);
  assert.equal(tally.CardsPlayed, played.length);
  assert.equal(tally.CountOf(A.Attack), played.filter((p) => p.Card.Def.Attribute === A.Attack).length);
  assert.equal(tally.CountOf(A.Guard), played.filter((p) => p.Card.Def.Attribute === A.Guard).length);
  assert.equal(tally.TraitsFired, events.filter((e) => e instanceof TraitEvaluated && e.Actor === Actor.Player && e.Outcome.Triggered).length);
  assert.ok(tally.TraitsFired > 0);
  assert.equal(sumValues(tally.Attributes), tally.CardsPlayed);
});

test('TheTally_CountsATwoFaceCardOnce_AsTheAttributeItFoldsTo (on a real started state)', { skip: loop.skip }, () => {
  const { TurnLoop, BattleSetup, Enemies } = loop.mods;
  const state = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), 6), NoShuffle).State;
  const tally = Chain.Tally(state, twoFaceEvents());
  assert.equal(tally.CardsPlayed, 3);
  assert.equal(tally.CountOf(A.Attack), 1);
  assert.equal(tally.CountOf(A.Guard), 2);
  assert.equal(tally.EnemyId, 'polearm_warped');
});
