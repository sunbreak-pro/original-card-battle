// Tests for src/core/battle/TurnLoop.js — ported from unity-port/BattleCore.Tests:
// TurnLoopTests, MultiEnemyTests, TwoEnemyBattleTests, OmenPlanTests, PreviewTests,
// SwiftAndFocusTests (the TurnLoop half), FloorReadTests (TurnLoop half), StanceStopTests,
// DepartureToolTests, BossStatusTests / BossSwitchTests / PriestAwayTests (the cases that drive TurnLoop).

import { describe as nodeDescribe, test } from 'node:test';
import assert from 'node:assert/strict';

import { InvalidOperationException, ArgumentOutOfRangeException, ArgumentException } from '../../src/shared/cs.js';
import {
  loadDeps, fixturesSkip, NoRng, Card, EnemyAction, Combatant, Enemy, TwentyCardDeck, DeckOf, SS, InHand, ofType,
  single, last, typeNames, eq, assertInOrder, statusText,
} from './_turnloop_fixtures.mjs';

// The battle modules load dynamically: while a sibling module is not written yet, every suite reports
// as skipped with the missing module named (see _turnloop_fixtures.mjs). TurnLoop.js imports
// Enemies.js and CardCatalog.js (BattleSetup.Slice), so the whole core has to be there.
const B = '../../src/core/battle/';
const core = await loadDeps(import.meta.url, [
  B + 'Types.js', B + 'BattleEvents.js', B + 'Constants.js', B + 'Combat.js', B + 'Field.js', B + 'Cards.js',
  B + 'Statuses.js', B + 'IRng.js', B + 'EnemyAi.js', B + 'Enemies.js', B + 'CardCatalog.js', B + 'TurnLoop.js',
]);
const chain = core.skip ? core : await loadDeps(import.meta.url, [B + 'ChainOrder.js']);
const deckRules = core.skip ? core : await loadDeps(import.meta.url, [B + 'DeckRules.js']);
const SKIP = core.skip || fixturesSkip;
/** describe, skipped with the reason while a module it needs is missing. */
const describe = (name, fn, extraSkip = false) => {
  const skip = SKIP || extraSkip;
  return skip ? nodeDescribe(name, { skip }, fn) : nodeDescribe(name, fn);
};

const {
  BattleAttribute, FaceKind, Actor, GameResult, BattlePhase, TargetKind, StatusKind, StanceHook, TraitCondition,
  TraitEffect, Face, Reach, Trait, StatusGrant, StanceDef, CardInstance, EnemyDef, EnemyRank, TreeSwitch,
  BattleHistory, PlayerTurnEnd,
  TurnStarted, GuardCleared, StaminaRecovered, StatusTicked, Drawn, DeckReshuffled, OmenSet, PlanSet,
  PlanChanged, TreeSwitched, ReserveChecked, HandDiscarded, TurnEnded, CardPlayed, ActionExecuted,
  ActionWhiffed, Rested, StaminaSpent, TraitEvaluated, FaceResolved, DamageDealt, GuardGained, CellsMoved,
  WallHit, FreeStepTaken, MoveBlocked, StatusApplied, StatusConsumed, StatusHpChanged, Healed,
  StaminaBroken, StanceFired, StanceStopped, StanceResumed, EnemyDefeated, DefeatChecked, BattleEnded,
  MaxStaminaChanged, StatusCleared,
  Constants, Combat, Field, Shift, Cards, StatusSet, SeededRng, EnemyAi, Enemies, CardCatalog, PrototypeDeck,
  TurnLoop, BattleSetup, PlayRefusal, PlayPreview, OmenPreview, FocusStep,
} = core.mods ?? {};
const { ChainOrder } = chain.mods ?? {};
const { Departure, DeckRules } = deckRules.mods ?? {};

const A = BattleAttribute;
const text = (label) => EnemyAi.ToText(label);

// ======================================================================== TurnLoopTests

describe('TurnLoopTests', () => {
  const Seed = 20260921;
  const ScenarioGap = 2;

  const AtGap = (gap, deck = null) =>
    new BattleSetup(Enemies.PolearmWarped, deck ?? PrototypeDeck.Build(), BattleSetup.SliceFieldCells, { StartGap: gap });
  const StartUnshuffledAt = (gap, ...kinds) => TurnLoop.Start(AtGap(gap, Cards.BuildDeck(kinds, 1)), NoRng);
  const StartUnshuffled = (...kinds) => StartUnshuffledAt(ScenarioGap, ...kinds);
  const Kinds = () => [...PrototypeDeck.Kinds];

  // ---- Start ----

  test('Start_UsesTheCanonValues_AndDecidesTheFirstOmen', () => {
    const start = TurnLoop.Start(BattleSetup.Slice(), new SeededRng(Seed));
    const s = start.State;
    assert.equal(s.Turn, 0);
    assert.equal(s.Phase, BattlePhase.AwaitingTurnStart);
    assert.equal(s.FieldCells, 6);
    assert.equal(s.Player.Hp, 50);
    assert.equal(s.Player.Stamina, 10);
    assert.equal(s.Player.Guard, 0);
    assert.equal(s.Player.Cell, 2);
    assert.equal(s.Player.Size, 1);
    assert.equal(s.Enemy.Hp, 60);
    assert.equal(s.Enemy.Stamina, 10);
    assert.equal(s.Enemy.Cell, 6);
    assert.equal(s.Enemy.Size, 1);
    assert.equal(s.Gap, 3);
    assert.equal(s.Hand.length, 0);
    assert.equal(s.DrawPile.length, 20);
    assert.equal(s.Omen.ActionId, 'step_forward');
    eq(single(start.Events), new OmenSet(Actor.Enemy, s.Omen, true));
  });

  test('Start_RefusesALineTheSidesDoNotFit', () => {
    const deck = PrototypeDeck.Build();
    assert.throws(() => TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 4), NoRng), ArgumentOutOfRangeException, '5〜8 cells');
    assert.throws(() => TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 9), NoRng), ArgumentOutOfRangeException, '5〜8 cells');
    assert.throws(() => TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 6, { StartGap: -1 }), NoRng), ArgumentOutOfRangeException);
    assert.throws(() => TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 6, { PlayerStartCell: 6 }), NoRng),
      (e) => e instanceof ArgumentException || e instanceof ArgumentOutOfRangeException);
    assert.doesNotThrow(() => TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 8, { StartGap: 5 }), NoRng));
  });

  for (const [fieldCells, enemyCell, gap] of [[5, 5, 2], [6, 6, 3], [7, 6, 3], [8, 6, 3]]) {
    test(`Start_PutsTheEnemyStartGapAway_OrAtTheEndOfAShortLine(${fieldCells})`, () => {
      const s = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), fieldCells), NoRng).State;
      assert.equal(s.FieldCells, fieldCells);
      assert.equal(s.Player.Cell, Constants.PlayerStartCell);
      assert.equal(s.Enemy.Cell, enemyCell);
      assert.equal(s.Gap, gap);
    });
  }

  test('TheConstants_AreThe2026_09_23Decisions', () => {
    assert.equal(Constants.StartGap, 3);
    assert.equal(Constants.CellCapacity, 1);
    assert.equal(Constants.EnemiesMax, 3);
    assert.equal(BattleSetup.Slice().FieldCells, 6);
  });

  // ---- The seventeen, in order ----

  test('OneTurn_EmitsTheSeventeenInOrder', () => {
    const rng = NoRng;
    const state = StartUnshuffled(...Kinds()).State;
    const begin = TurnLoop.BeginPlayerTurn(state, rng);
    const traitCard = begin.State.Hand.find((c) => c.Def.Trait != null && TurnLoop.CanPlay(begin.State, c.InstanceId) === PlayRefusal.None);
    assert.equal(traitCard.Def.Id, 'reach_thrust');
    const play = TurnLoop.PlayCard(begin.State, traitCard.InstanceId, rng);
    const end = TurnLoop.EndTurn(play.State, rng);
    const all = [...begin.Events, ...play.Events, ...end.Events];

    assertInOrder(all,
      TurnStarted, GuardCleared, StaminaRecovered,
      Drawn, Drawn, Drawn, Drawn, Drawn, Drawn,
      OmenSet, CardPlayed, StaminaSpent, TraitEvaluated, FaceResolved, DefeatChecked, ReserveChecked,
      HandDiscarded, TurnEnded, GuardCleared, StaminaRecovered, ActionExecuted, DefeatChecked, OmenSet);

    assert.equal(ofType(all, Drawn).length, 6);
    assert.ok(last(all) instanceof OmenSet, 'the turn closes on the next omen');
    assert.equal(ofType(end.Events, OmenSet).length, 1);
    assert.equal(last(all).Decided, true);
  });

  test('BeginPlayerTurn_IsStepsOneToFive_Exactly', () => {
    const state = StartUnshuffled(...Kinds()).State;
    const begin = TurnLoop.BeginPlayerTurn(state, NoRng);
    assert.deepEqual(typeNames(begin.Events), [
      'TurnStarted', 'GuardCleared', 'StaminaRecovered',
      'Drawn', 'Drawn', 'Drawn', 'Drawn', 'Drawn', 'Drawn',
      'OmenSet',
    ]);
    assert.equal(last(begin.Events).Decided, false, 'step 5 shows the standing omen');
    assert.equal(begin.State.Phase, BattlePhase.PlayerAction);
    assert.equal(begin.State.Turn, 1);
  });

  test('TheTraitIsJudgedBeforeAnyFace_AndFacesComeInOrder', () => {
    let state = StartUnshuffled(CardCatalog.BoarRush, CardCatalog.Thrust, CardCatalog.Brace, CardCatalog.KesaCut, CardCatalog.Feint).State;
    state = TurnLoop.BeginPlayerTurn(state, NoRng).State;
    const play = TurnLoop.PlayCard(state, InHand(state, 'boar_rush'), NoRng);
    assert.deepEqual(typeNames(play.Events), [
      'CardPlayed', 'StaminaSpent', 'TraitEvaluated',
      'FaceResolved', 'DamageDealt',
      'FaceResolved', 'CellsMoved',
      'DefeatChecked',
    ]);
    assert.deepEqual(ofType(play.Events, FaceResolved).map((f) => f.Face), [FaceKind.Attack, FaceKind.Move]);
    assert.equal(single(ofType(play.Events, CardPlayed)).GapBefore, 2);
    assert.equal(single(ofType(play.Events, DamageDealt)).Raw, 17, 'the gap is read before the move');
    eq(single(ofType(play.Events, CellsMoved)), new CellsMoved(Actor.Player, 2, 4, false));
    assert.equal(play.State.Gap, 0);
  });

  test('ACardWithoutATrait_EmitsNoTraitEvaluated', () => {
    let state = StartUnshuffledAt(1, CardCatalog.Thrust, CardCatalog.Brace, CardCatalog.KesaCut, CardCatalog.Feint, CardCatalog.BoarRush).State;
    state = TurnLoop.BeginPlayerTurn(state, NoRng).State;
    const play = TurnLoop.PlayCard(state, InHand(state, 'thrust'), NoRng);
    assert.equal(ofType(play.Events, TraitEvaluated).length, 0);
  });

  // ---- Reach (§2.4) and the whiff (§6) ----

  test('ACardOutOfReach_IsRefused_AndThePreviewSaysSo', () => {
    let s = StartUnshuffled(...Kinds()).State;
    s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
    assert.equal(TurnLoop.CanPlay(s, InHand(s, 'kesa_cut')), PlayRefusal.OutOfReach);
    assert.equal(TurnLoop.CanPlay(s, InHand(s, 'reach_thrust')), PlayRefusal.None);
    assert.equal(TurnLoop.CanPlay(s, InHand(s, 'thrust')), PlayRefusal.None);
    assert.equal(TurnLoop.CanPlay(s, InHand(s, 'brace')), PlayRefusal.None);
    assert.equal(TurnLoop.Preview(s, InHand(s, 'kesa_cut')).InReach, false);
    assert.equal(TurnLoop.Preview(s, InHand(s, 'reach_thrust')).InReach, true);
    assert.throws(() => TurnLoop.PlayCard(s, InHand(s, 'kesa_cut'), NoRng), InvalidOperationException);
    assert.throws(() => TurnLoop.PlayCard(s, InHand(s, 'kesa_cut'), NoRng), { message: `Card "${InHand(s, 'kesa_cut')}" cannot be played: OutOfReach.` });

    const broke = s.with({ Player: s.Player.with({ Stamina: 1 }) });
    assert.equal(TurnLoop.CanPlay(broke, InHand(broke, 'kesa_cut')), PlayRefusal.NotEnoughStamina);
  });

  test('AnEnemyBlow_WhiffsWhenThePlayerLeftItsReach_AndTheCostIsStillPaid', () => {
    let s = StartUnshuffled(CardCatalog.BoarRush, CardCatalog.Thrust, CardCatalog.KesaCut, CardCatalog.Brace, CardCatalog.Feint).State;
    s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
    s = TurnLoop.PlayCard(s, InHand(s, 'boar_rush'), NoRng).State;
    assert.equal(s.Gap, 0);
    const end = TurnLoop.EndTurn(s, NoRng);
    assert.equal(single(ofType(end.Events, ActionExecuted)).Action.Id, 'sweep');
    assert.equal(single(ofType(end.Events, ActionExecuted)).GapBefore, 0);
    assert.equal(single(ofType(end.Events, StaminaSpent), (e) => e.Actor === Actor.Enemy).Amount, 2);
    eq(single(ofType(end.Events, ActionWhiffed)), new ActionWhiffed(Actor.Enemy, 'sweep', 0, new Reach(1, 2)));
    assert.equal(ofType(end.Events, DamageDealt).length, 0);
    assert.equal(ofType(end.Events, FaceResolved).filter((f) => f.Actor === Actor.Enemy).length, 0);
    assert.equal(end.State.Player.Hp, 50);
    assert.equal(end.State.Omen.ActionId, 'shove');
  });

  test('TheEnemyStepsIn_FromGapThree_AndTheNextOmenIsTheSweep', () => {
    let s = TurnLoop.Start(AtGap(3), NoRng).State;
    assert.equal(text(s.Omen.Label), '移動');
    s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
    const end = TurnLoop.EndTurn(s, NoRng);
    assert.equal(single(ofType(end.Events, ActionExecuted)).Action.Id, 'step_forward');
    eq(single(ofType(end.Events, CellsMoved)), new CellsMoved(Actor.Enemy, 6, 5, false));
    eq(single(ofType(end.Events, GuardGained)), new GuardGained(Actor.Enemy, 2, 2));
    assert.equal(ofType(end.Events, ActionWhiffed).length, 0, 'a self action reads no reach');
    assert.equal(end.State.Gap, 2);
    assert.equal(end.State.Omen.ActionId, 'sweep');
  });

  // ---- #49: the player's recovery is 3 at every range ----

  for (const gap of [0, 1, 2, 3, 4, 5]) {
    test(`PlayerRecovery_IsThreeAtEveryRange(${gap})`, () => {
      const deck = Cards.BuildDeck(Kinds(), 1);
      let state = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 8, { StartGap: gap }), NoRng).State;
      state = state.with({ Player: state.Player.with({ Stamina: 0 }) });
      const begin = TurnLoop.BeginPlayerTurn(state, NoRng);
      assert.equal(begin.State.Gap, gap);
      eq(single(ofType(begin.Events, StaminaRecovered)), new StaminaRecovered(Actor.Player, 3, 3, 10));
    });
  }

  // ---- The eight numbers ----

  test('Number1_Recovery_PlayerThreeCappedAtTen_EnemyTwo', () => {
    const state = StartUnshuffledAt(1, ...Kinds()).State;
    const begin = TurnLoop.BeginPlayerTurn(state, NoRng);
    eq(single(ofType(begin.Events, StaminaRecovered)), new StaminaRecovered(Actor.Player, 0, 10, 10), 'already full');
    let s = begin.State;
    s = TurnLoop.PlayCard(s, InHand(s, 'thrust'), NoRng).State;
    s = TurnLoop.PlayCard(s, InHand(s, 'kesa_cut'), NoRng).State;
    const end = TurnLoop.EndTurn(s, NoRng);
    const enemy = single(ofType(end.Events, StaminaRecovered));
    assert.equal(enemy.Actor, Actor.Enemy);
    assert.equal(enemy.Amount, 0, 'the polearm also starts full');
    const next = TurnLoop.BeginPlayerTurn(end.State, NoRng);
    eq(single(ofType(next.Events, StaminaRecovered)), new StaminaRecovered(Actor.Player, 3, 8, 10));
    const end2 = TurnLoop.EndTurn(next.State, NoRng);
    eq(single(ofType(end2.Events, StaminaRecovered)), new StaminaRecovered(Actor.Enemy, 2, 10, 10));
  });

  test('Number2_StaminaFalls_ByExactlyTheSumOfTheColumns', () => {
    const state = StartUnshuffledAt(1, ...Kinds()).State;
    let s = TurnLoop.BeginPlayerTurn(state, NoRng).State;
    const played = ['thrust', 'kesa_cut', 'brace'];
    for (const id of played) s = TurnLoop.PlayCard(s, InHand(s, id), NoRng).State;
    assert.equal(s.Player.Stamina, 10 - (3 + 2 + 2));
    assert.equal(s.Hand.length, 3);
    assert.deepEqual(s.DiscardPile.map((c) => c.Def.Id), played);
  });

  test('Number2_AnUnpayableCard_IsRefused', () => {
    const state = StartUnshuffledAt(1, ...Kinds()).State;
    let s = TurnLoop.BeginPlayerTurn(state, NoRng).State;
    for (const id of ['thrust', 'kesa_cut', 'reach_thrust', 'brace']) s = TurnLoop.PlayCard(s, InHand(s, id), NoRng).State;
    assert.equal(s.Player.Stamina, 1);
    const feint = InHand(s, 'feint');
    assert.equal(TurnLoop.CanPlay(s, feint), PlayRefusal.NotEnoughStamina);
    assert.throws(() => TurnLoop.PlayCard(s, feint, NoRng), InvalidOperationException);
    assert.equal(TurnLoop.CanPlay(s, 'nothing-0'), PlayRefusal.NotInHand);
  });

  test('Number3_Reserve_OnlyWithThreeOrMoreLeft', () => {
    const state = StartUnshuffledAt(1, ...Kinds()).State;
    const s = TurnLoop.BeginPlayerTurn(state, NoRng).State;
    let rich = s;
    for (const id of ['thrust', 'kesa_cut', 'reach_thrust']) rich = TurnLoop.PlayCard(rich, InHand(rich, id), NoRng).State;
    const richEnd = TurnLoop.EndTurn(rich, NoRng);
    eq(ofType(richEnd.Events, ReserveChecked)[0], new ReserveChecked(Actor.Player, 3, 3, 3));
    const poor = TurnLoop.PlayCard(rich, InHand(rich, 'brace'), NoRng).State;
    const poorEnd = TurnLoop.EndTurn(poor, NoRng);
    eq(ofType(poorEnd.Events, ReserveChecked)[0], new ReserveChecked(Actor.Player, 1, 0, 9), "brace's 9 stands; 構え adds nothing");
  });

  test('Number3_TheEnemyTakesItsReserveToo', () => {
    const state = StartUnshuffled(...Kinds()).State;
    const s = TurnLoop.BeginPlayerTurn(state, NoRng).State;
    const end = TurnLoop.EndTurn(s, NoRng);
    eq(last(ofType(end.Events, ReserveChecked)), new ReserveChecked(Actor.Enemy, 8, 3, 3));
    assert.equal(end.State.Enemy.Guard, 3);
  });

  test('Number4_TheOmenBranch_FollowsTheGapBand', () => {
    assert.equal(TurnLoop.Start(AtGap(0), NoRng).State.Omen.ActionId, 'shove');
    assert.equal(TurnLoop.Start(AtGap(1), NoRng).State.Omen.ActionId, 'sweep');
    assert.equal(TurnLoop.Start(AtGap(2), NoRng).State.Omen.ActionId, 'sweep');
    assert.equal(TurnLoop.Start(AtGap(3), NoRng).State.Omen.ActionId, 'step_forward');
  });

  test('Number5_TheSweep_IsTwelveAtGapTwo_AndNineAtGapOne', () => {
    const stay = TurnLoop.BeginPlayerTurn(TurnLoop.Start(AtGap(2), NoRng).State, NoRng).State;
    const stayEnd = TurnLoop.EndTurn(stay, NoRng);
    eq(single(ofType(stayEnd.Events, DamageDealt)), new DamageDealt(Actor.Enemy, Actor.Player, 12, 3, 9, 0, 41));

    const deck = Cards.BuildDeck([CardCatalog.StepInGuard, CardCatalog.Thrust, CardCatalog.KesaCut, CardCatalog.Brace, CardCatalog.Feint], 1);
    let s = TurnLoop.Start(AtGap(2, deck), NoRng).State;
    s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
    s = TurnLoop.PlayCard(s, InHand(s, 'step_in_guard'), NoRng).State;
    assert.equal(s.Gap, 1);
    const end = TurnLoop.EndTurn(s, NoRng);
    assert.equal(single(ofType(end.Events, ActionExecuted)).Action.Id, 'sweep');
    assert.equal(single(ofType(end.Events, DamageDealt)).Raw, 9);
  });

  test('Number6_TheShove_IsNineAgainstNoGuard_SixAgainstGuard_AndPushesTwo_IntoTheWall', () => {
    const state = StartUnshuffledAt(0, ...Kinds()).State;
    let bare = TurnLoop.BeginPlayerTurn(state, NoRng).State;
    bare = bare.with({ Player: bare.Player.with({ Stamina: 2 }) });
    const bareEnd = TurnLoop.EndTurn(bare, NoRng);
    assert.equal(single(ofType(bareEnd.Events, ActionExecuted)).Action.Id, 'shove');
    eq(single(ofType(bareEnd.Events, DamageDealt)), new DamageDealt(Actor.Enemy, Actor.Player, 9, 0, 9, 0, 41));
    eq(single(ofType(bareEnd.Events, CellsMoved)), new CellsMoved(Actor.Player, 2, 1, true));
    eq(single(ofType(bareEnd.Events, WallHit)), new WallHit(Actor.Player, 1, 3, 0, 3, 0, 38));
    assert.equal(bareEnd.State.Gap, 1);
    assert.equal(bareEnd.State.Omen.ActionId, 'sweep');

    let guarded = TurnLoop.BeginPlayerTurn(state, NoRng).State;
    guarded = TurnLoop.PlayCard(guarded, InHand(guarded, 'brace'), NoRng).State;
    const guardedEnd = TurnLoop.EndTurn(guarded, NoRng);
    eq(single(ofType(guardedEnd.Events, DamageDealt)), new DamageDealt(Actor.Enemy, Actor.Player, 6, 6, 0, 6, 50));
    eq(single(ofType(guardedEnd.Events, CellsMoved)), new CellsMoved(Actor.Player, 2, 1, true));
    eq(single(ofType(guardedEnd.Events, WallHit)), new WallHit(Actor.Player, 1, 3, 3, 0, 3, 50));
  });

  test('Number7_Damage_IsFacePlusTraitMinusGuard_AndNeverBelowZero', () => {
    const state = StartUnshuffledAt(0, CardCatalog.Thrust, CardCatalog.KesaCut, CardCatalog.ReachThrust,
      CardCatalog.Brace, CardCatalog.Feint, CardCatalog.ShieldBash, CardCatalog.BoarRush,
      CardCatalog.BodyCheck, CardCatalog.StepInGuard, CardCatalog.StepOutGuard).State;
    const s = TurnLoop.BeginPlayerTurn(state, NoRng).State;
    const kesa = TurnLoop.PlayCard(s, InHand(s, 'kesa_cut'), NoRng);
    eq(single(ofType(kesa.Events, DamageDealt)), new DamageDealt(Actor.Player, Actor.Enemy, 20, 0, 20, 0, 40));
    const thrust = TurnLoop.PlayCard(s, InHand(s, 'thrust'), NoRng);
    assert.equal(single(ofType(thrust.Events, DamageDealt)).Raw, 23);
    assert.equal(TurnLoop.CanPlay(s, InHand(s, 'reach_thrust')), PlayRefusal.OutOfReach);

    const end = TurnLoop.EndTurn(s, NoRng);
    const t2 = TurnLoop.BeginPlayerTurn(end.State, NoRng).State;
    assert.equal(t2.Player.Hp, 50 - 3 - 3);
    assert.equal(t2.Enemy.Guard, 3);
    assert.equal(t2.Gap, 1);
    const intoGuard = TurnLoop.PlayCard(t2, InHand(t2, 'boar_rush'), NoRng);
    eq(single(ofType(intoGuard.Events, DamageDealt)), new DamageDealt(Actor.Player, Actor.Enemy, 11, 3, 8, 0, 52));
    assert.equal(intoGuard.State.Gap, 0);
  });

  test('Number7_GuardSoaksFirst_ThenHp', () => {
    const deck = Cards.BuildDeck([CardCatalog.BodyCheck, CardCatalog.Thrust, CardCatalog.KesaCut,
      CardCatalog.Brace, CardCatalog.Feint, CardCatalog.ShieldBash, CardCatalog.ReachThrust,
      CardCatalog.BoarRush, CardCatalog.StepInGuard, CardCatalog.StepOutGuard], 1);
    let s = TurnLoop.Start(AtGap(ScenarioGap, deck), NoRng).State;
    s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
    s = TurnLoop.EndTurn(s, NoRng).State;
    s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
    assert.equal(s.Gap, 2);
    const hit = TurnLoop.PlayCard(s, InHand(s, 'reach_thrust'), NoRng);
    eq(single(ofType(hit.Events, DamageDealt)), new DamageDealt(Actor.Player, Actor.Enemy, 20, 3, 17, 0, 43));
  });

  test('Number8_TheHand_IsZeroAtTurnEnd_AndSixAtTheNextStart', () => {
    const rng = new SeededRng(Seed);
    let s = TurnLoop.Start(BattleSetup.Slice(), rng).State;
    s = TurnLoop.BeginPlayerTurn(s, rng).State;
    assert.equal(s.Hand.length, 6);
    const end = TurnLoop.EndTurn(s, rng);
    assert.equal(end.State.Hand.length, 0);
    assert.equal(single(ofType(end.Events, HandDiscarded)).Count, 6);
    assert.equal(end.State.DiscardPile.length, 6);
    const next = TurnLoop.BeginPlayerTurn(end.State, rng);
    assert.equal(next.State.Hand.length, 6);
  });

  // ---- The three end points ----

  test('EndPoint_ShovedBack_SoTheNextOmenIsTheSweep', () => {
    const rng = new SeededRng(Seed);
    let s = TurnLoop.Start(AtGap(0), rng).State;
    assert.equal(text(s.Omen.Label), '攻撃・0');
    s = TurnLoop.BeginPlayerTurn(s, rng).State;
    const end = TurnLoop.EndTurn(s, rng);
    eq(single(ofType(end.Events, CellsMoved)), new CellsMoved(Actor.Player, 2, 1, true));
    assert.equal(single(ofType(end.Events, WallHit)).BlockedCells, 1);
    assert.equal(end.State.Player.Cell, 1);
    assert.equal(end.State.Gap, 1);
    const omens = ofType(end.Events, OmenSet);
    assert.equal(omens.length, 1);
    assert.equal(omens[0].Omen.ActionId, 'sweep');
    assert.equal(text(omens[0].Omen.Label), '攻撃・1〜2');
    eq(end.State.Omen, omens[0].Omen);
    assert.equal(end.State.Result, GameResult.Ongoing);
    assert.ok(end.State.Player.Hp > 0);
    assert.ok(end.State.Enemy.Hp > 0);
    assert.equal(end.State.Phase, BattlePhase.AwaitingTurnStart);
  });

  // ---- Statuses, moves, reshuffle ----

  test('Slow_LandsOnThePolearm_TicksAtItsTurnStart_AndShortensTheShove', () => {
    const deck = Cards.BuildDeck([CardCatalog.BodyCheck, CardCatalog.Thrust, CardCatalog.KesaCut, CardCatalog.Brace, CardCatalog.Feint], 1);
    let s = TurnLoop.Start(AtGap(0, deck), NoRng).State;
    s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
    const play = TurnLoop.PlayCard(s, InHand(s, 'body_check'), NoRng);
    eq(single(ofType(play.Events, StatusApplied)), new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Slow, 2, 2, false));
    assert.equal(single(ofType(play.Events, DamageDealt)).Raw, 11);
    assert.equal(play.State.Player.NextTurnRecoveryBonus, -1);
    const end = TurnLoop.EndTurn(play.State, NoRng);
    eq(single(ofType(end.Events, StatusTicked)), new StatusTicked(Actor.Enemy, StatusKind.Slow, 1));
    eq(single(ofType(end.Events, CellsMoved)), new CellsMoved(Actor.Player, 2, 1, true));
    assert.equal(ofType(end.Events, WallHit).length, 0);
    const next = TurnLoop.BeginPlayerTurn(end.State, NoRng);
    eq(single(ofType(next.Events, StaminaRecovered)), new StaminaRecovered(Actor.Player, 1, 10, 10));
    assert.equal(next.State.Player.NextTurnRecoveryBonus, 0);
  });

  test('ASlowedPlayer_PlaysTheMoveCard_ButDoesNotMove', () => {
    let s = StartUnshuffledAt(1, CardCatalog.Feint, CardCatalog.Thrust, CardCatalog.KesaCut, CardCatalog.Brace, CardCatalog.BoarRush).State;
    s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
    s = s.with({ Player: s.Player.with({ Statuses: SS([StatusKind.Slow, 1]) }) });
    const play = TurnLoop.PlayCard(s, InHand(s, 'feint'), NoRng);
    eq(single(ofType(play.Events, MoveBlocked)), new MoveBlocked(Actor.Player, StatusKind.Slow));
    assert.equal(ofType(play.Events, CellsMoved).length, 0);
    assert.equal(play.State.Player.Cell, 2);
    assert.equal(single(ofType(play.Events, DamageDealt)).Raw, 10);
  });

  test('AMoveIntoTheEnemy_StopsShort_AndEmitsNoCellsMoved', () => {
    let s = StartUnshuffledAt(0, CardCatalog.StepInGuard, CardCatalog.Thrust, CardCatalog.KesaCut, CardCatalog.Brace, CardCatalog.BoarRush).State;
    s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
    const play = TurnLoop.PlayCard(s, InHand(s, 'step_in_guard'), NoRng);
    assert.equal(ofType(play.Events, CellsMoved).length, 0);
    assert.equal(ofType(play.Events, MoveBlocked).length, 0);
    eq(single(ofType(play.Events, GuardGained)), new GuardGained(Actor.Player, 12, 12));
  });

  test('AGuardTrait_LandsAsGuard_OnACardWithoutAGuardFace', () => {
    let s = StartUnshuffledAt(1, CardCatalog.Feint, CardCatalog.Thrust, CardCatalog.KesaCut, CardCatalog.Brace, CardCatalog.BoarRush).State;
    s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
    const play = TurnLoop.PlayCard(s, InHand(s, 'feint'), NoRng);
    eq(single(ofType(play.Events, GuardGained)), new GuardGained(Actor.Player, 3, 3));
    assert.deepEqual(ofType(play.Events, FaceResolved).map((f) => f.Face), [FaceKind.Attack, FaceKind.Move]);
    assert.equal(play.State.Player.Cell, 1);
  });

  test('TheDrawPileRunsOut_OnTurnFour_AndTheDiscardPileComesBack', () => {
    const rng = new SeededRng(Seed);
    let s = TurnLoop.Start(BattleSetup.Slice(), rng).State;
    for (let turn = 1; turn <= 3; turn++) {
      const begin = TurnLoop.BeginPlayerTurn(s, rng);
      assert.equal(ofType(begin.Events, DeckReshuffled).length, 0, `turn ${turn}`);
      s = TurnLoop.EndTurn(begin.State, rng).State;
    }
    const fourth = TurnLoop.BeginPlayerTurn(s, rng);
    assert.equal(single(ofType(fourth.Events, DeckReshuffled)).DrawPileCount, 18);
    assert.equal(fourth.State.Hand.length, 6);
    assert.equal(fourth.State.DrawPile.length, 14);
    assert.equal(fourth.State.DiscardPile.length, 0);
  });

  // ---- The end of the battle ----

  test('TheBattleEnds_TheMomentTheEnemyReachesZero', () => {
    let s = StartUnshuffled(CardCatalog.BoarRush, CardCatalog.Thrust, CardCatalog.KesaCut, CardCatalog.Brace, CardCatalog.Feint).State;
    s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
    s = s.WithEnemy(s.Enemy.with({ Hp: 10 }));
    const play = TurnLoop.PlayCard(s, InHand(s, 'boar_rush'), NoRng);
    assert.equal(play.State.Enemy.Hp, 0);
    assert.equal(play.State.Result, GameResult.Won);
    assert.equal(play.State.Phase, BattlePhase.Finished);
    assert.deepEqual(ofType(play.Events, FaceResolved).map((f) => f.Face), [FaceKind.Attack]);
    assert.equal(ofType(play.Events, CellsMoved).length, 0);
    assert.equal(play.State.Player.Cell, 2);
    eq(last(play.Events), new BattleEnded(Actor.Player, GameResult.Won));
    assert.equal(TurnLoop.CanPlay(play.State, InHand(play.State, 'thrust')), PlayRefusal.BattleOver);
    assert.throws(() => TurnLoop.EndTurn(play.State, NoRng), { name: 'InvalidOperationException', message: 'EndTurn: the battle is over (Won).' });
    assert.throws(() => TurnLoop.BeginPlayerTurn(play.State, NoRng), InvalidOperationException);
  });

  test('TheBattleEnds_WhenThePlayerReachesZero_AndNoOmenFollows', () => {
    let s = TurnLoop.Start(AtGap(ScenarioGap), NoRng).State;
    s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
    s = s.with({ Player: s.Player.with({ Hp: 3, Stamina: 0 }) });
    const end = TurnLoop.EndTurn(s, NoRng);
    assert.equal(end.State.Player.Hp, 0);
    assert.equal(end.State.Result, GameResult.Lost);
    assert.equal(ofType(end.Events, OmenSet).length, 0);
    eq(last(end.Events), new BattleEnded(Actor.Enemy, GameResult.Lost));
  });

  test('TheWall_CanEndTheBattleToo', () => {
    let s = TurnLoop.Start(AtGap(0), NoRng).State;
    s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
    s = s.with({ Player: s.Player.with({ Hp: 2, Stamina: 0, Guard: 6 }) });
    const end = TurnLoop.EndTurn(s, NoRng);
    eq(single(ofType(end.Events, DamageDealt)), new DamageDealt(Actor.Enemy, Actor.Player, 6, 6, 0, 0, 2));
    assert.equal(single(ofType(end.Events, WallHit)).HpAfter, 0);
    assert.equal(end.State.Result, GameResult.Lost);
    assert.equal(ofType(end.Events, OmenSet).length, 0);
  });

  test('MovesOutOfPhase_AreRefused', () => {
    const start = TurnLoop.Start(BattleSetup.Slice(), NoRng).State;
    assert.throws(() => TurnLoop.EndTurn(start, NoRng),
      { name: 'InvalidOperationException', message: 'EndTurn: expected phase PlayerAction, but the battle is in AwaitingTurnStart.' });
    const open = TurnLoop.BeginPlayerTurn(start, NoRng).State;
    assert.throws(() => TurnLoop.BeginPlayerTurn(open, NoRng), InvalidOperationException);
    assert.equal(TurnLoop.CanPlay(start, 'thrust-0'), PlayRefusal.NotPlayerTurn);
  });

  test('AnOmenThatCanNoLongerBePaidFor_BecomesARest', () => {
    let s = TurnLoop.Start(AtGap(ScenarioGap), NoRng).State;
    s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
    s = s.WithEnemy(s.Enemy.with({ Stamina: 0, NextTurnRecoveryBonus: -2 }));
    const end = TurnLoop.EndTurn(s, NoRng);
    assert.equal(single(ofType(end.Events, Rested)).Declared.ActionId, 'sweep');
    assert.equal(ofType(end.Events, ActionExecuted).length, 0);
    assert.equal(ofType(end.Events, DamageDealt).length, 0);
    assert.equal(end.State.Gap, 2);
  });

  // ---- Three turns under a fixed seed (#72 Definition of Done) ----

  function PlayGreedyTurn(state, rng) {
    const events = [];
    const begin = TurnLoop.BeginPlayerTurn(state, rng);
    events.push(...begin.Events);
    state = begin.State;
    let played = true;
    while (played && state.Result === GameResult.Ongoing) {
      played = false;
      for (const card of state.Hand) {
        if (TurnLoop.CanPlay(state, card.InstanceId) !== PlayRefusal.None) continue;
        const step = TurnLoop.PlayCard(state, card.InstanceId, rng);
        events.push(...step.Events);
        state = step.State;
        played = true;
        break;
      }
    }
    if (state.Result === GameResult.Ongoing) {
      const end = TurnLoop.EndTurn(state, rng);
      events.push(...end.Events);
      state = end.State;
    }
    return { State: state, Events: events };
  }

  const Summary = (s) =>
    `T${s.Turn} P ${s.Player.Hp}/${s.Player.Stamina}/${s.Player.Guard}/c${s.Player.Cell} `
    + `E ${s.Enemy.Hp}/${s.Enemy.Stamina}/${s.Enemy.Guard}/c${s.Enemy.Cell}/${statusText(s.Enemy.Statuses)} gap ${s.Gap} omen ${text(s.Omen.Label)}`;

  const PinnedHands = [
    'shield_bash,body_check,step_out_guard,step_in_guard,step_out_guard,body_check',
    'kesa_cut,brace,boar_rush,thrust,reach_thrust,feint',
    'feint,kesa_cut,reach_thrust,step_in_guard,shield_bash,boar_rush',
  ];
  const PinnedSummaries = [
    'T1 P 50/1/36/c1 E 60/9/5/c5/— gap 3 omen 移動',
    'T2 P 50/2/9/c1 E 60/9/5/c4/— gap 2 omen 攻撃・1〜2',
    'T3 P 50/0/3/c2 E 45/8/3/c4/— gap 1 omen 攻撃・1〜2',
  ];
  const PinnedEventCounts = [45, 30, 36];

  test('ThreeTurns_UnderAFixedSeed_ArePinned', () => {
    const rng = new SeededRng(Seed);
    let state = TurnLoop.Start(BattleSetup.Slice(), rng).State;
    const summaries = [];
    const hands = [];
    const counts = [];
    for (let turn = 0; turn < 3; turn++) {
      const { State: next, Events: events } = PlayGreedyTurn(state, rng);
      hands.push(ofType(events, Drawn).map((d) => d.Card.Def.Id).join(','));
      summaries.push(Summary(next));
      counts.push(events.length);
      assert.ok(last(events) instanceof OmenSet, `turn ${turn + 1} closes on the next omen`);
      state = next;
    }
    assert.deepEqual(hands, PinnedHands);
    assert.deepEqual(summaries, PinnedSummaries);
    assert.deepEqual(counts, PinnedEventCounts);
  });

  test('TheSameSeed_ReplaysTheSameEventStream', () => {
    const Run = () => {
      const rng = new SeededRng(Seed);
      let state = TurnLoop.Start(BattleSetup.Slice(), rng).State;
      const all = [];
      for (let turn = 0; turn < 3; turn++) {
        const r = PlayGreedyTurn(state, rng);
        all.push(...r.Events);
        state = r.State;
      }
      return all;
    };
    eq(Run(), Run());
  });

  test('ADifferentSeed_DealsADifferentHand', () => {
    const a = TurnLoop.BeginPlayerTurn(TurnLoop.Start(BattleSetup.Slice(), new SeededRng(1)).State, new SeededRng(1));
    const b = TurnLoop.BeginPlayerTurn(TurnLoop.Start(BattleSetup.Slice(), new SeededRng(2)).State, new SeededRng(2));
    assert.notDeepEqual(a.State.Hand.map((c) => c.InstanceId), b.State.Hand.map((c) => c.InstanceId));
  });
});

// ======================================================================== BattleSetup (Types of TurnLoop.cs)

describe('BattleSetup', () => {
  test('StartHp: null is full, above max is kept to max, 0 or less throws (#574)', () => {
    const e = Enemy();
    assert.equal(new BattleSetup(e, [], 6).StartHp, 50);
    assert.equal(new BattleSetup(e, [], 6, { PlayerStartHp: 70 }).StartHp, 50);
    assert.equal(new BattleSetup(e, [], 6, { PlayerStartHp: 12 }).StartHp, 12);
    assert.throws(() => new BattleSetup(e, [], 6, { PlayerStartHp: 0 }).StartHp, {
      name: 'ArgumentOutOfRangeException',
      message: "A carried HP is 1 or more: a fallen character does not start a battle. (Parameter 'PlayerStartHp')\nActual value was 0.",
    });
  });

  test('StartStamina and Tools', () => {
    const e = Enemy();
    assert.equal(new BattleSetup(e, [], 6, { PlayerStartStamina: -3 }).StartStamina, 0);
    assert.equal(new BattleSetup(e, [], 6, { PlayerStartStamina: 4 }).StartStamina, 4);
    assert.equal(new BattleSetup(e, [], 6).Tools, null);
    eq(new BattleSetup(e, [], 6, { FirstHitGuard: 5 }).Tools.ScaleGuard, 5);
    assert.equal(new BattleSetup(e, [], 6, { BossWordWard: 1 }).Tools.BossWordWard, 1);
  });

  test('positional parameters follow the C# order', () => {
    const e = Enemy();
    const s = new BattleSetup(e, [], 8, 2, 3, 40, 9);
    assert.equal(s.StartGap, 2);
    assert.equal(s.PlayerStartCell, 3);
    assert.equal(s.PlayerMaxHp, 40);
    assert.equal(s.PlayerMaxStamina, 9);
    assert.deepEqual(s.EnemyStartCells, [6]);
    assert.equal(BattleSetup.SliceFieldCells, 6);
  });
});

// ======================================================================== MultiEnemyTests

describe('MultiEnemyTests', () => {
  const E = (id, size = 1) => Enemy(id, { size });
  const Jab = Card('jab', { face: new Face({ Power: 5, Reach: Reach.Only(0) }) });

  function Battle(fieldCells, startGap, enemies, ...hand) {
    const deck = Cards.BuildDeck(hand.length === 0 ? [Card('filler')] : hand, 1);
    const setup = new BattleSetup(enemies[0], deck, fieldCells, { StartGap: startGap, MoreEnemies: enemies.slice(1) });
    const s = TurnLoop.Start(setup, NoRng).State;
    return TurnLoop.BeginPlayerTurn(s, NoRng).State;
  }
  function Place(s, ...cells) {
    for (let i = 0; i < cells.length; i++) s = s.WithEnemy(i, s.Enemies[i].Body.with({ Cell: cells[i] }));
    return s;
  }

  test('EachEnemy_StartsRightBehindTheOneBefore', () => {
    const three = TurnLoop.Start(new BattleSetup(E('a'), TwentyCardDeck(), 8, { MoreEnemies: [E('b'), E('c')] }), NoRng);
    assert.deepEqual(three.State.Enemies.map((u) => u.Body.Cell), [6, 7, 8]);
    assert.deepEqual([0, 1, 2].map((i) => three.State.GapTo(i)), [3, 4, 5]);
    assert.deepEqual(ofType(three.Events, OmenSet).map((o) => o.Unit), [0, 1, 2]);
    const close = TurnLoop.Start(new BattleSetup(E('a'), TwentyCardDeck(), 6, { StartGap: 1, MoreEnemies: [E('b'), E('c')] }), NoRng);
    assert.deepEqual(close.State.Enemies.map((u) => u.Body.Cell), [4, 5, 6]);
    const large = TurnLoop.Start(new BattleSetup(E('a'), TwentyCardDeck(), 8, { MoreEnemies: [E('b', 2)] }), NoRng);
    assert.deepEqual(large.State.Enemies.map((u) => u.Body.Cell), [6, 7]);
  });

  test('ALineTooShortForEveryone_OrAFourthEnemy_IsRefused', () => {
    const deck = TwentyCardDeck();
    assert.throws(() => TurnLoop.Start(new BattleSetup(E('a'), deck, 6, { MoreEnemies: [E('b')] }), NoRng), ArgumentOutOfRangeException);
    assert.throws(() => TurnLoop.Start(new BattleSetup(E('a'), deck, 8, { StartGap: 0, MoreEnemies: [E('b'), E('c'), E('d')] }), NoRng), ArgumentOutOfRangeException);
  });

  test('EachEnemy_DecidesFromItsOwnGap', () => {
    const s = Battle(8, 0, [E('a'), E('b'), E('c')]);
    assert.deepEqual(s.Enemies.map((u) => u.Omen.ActionId), ['near_hit', 'mid_hit', 'mid_hit']);
  });

  test('TheFrontEnemy_CoversTheOneBehind_ByReachAlone', () => {
    const s = Battle(8, 0, [E('a'), E('b')], Jab);
    const jab = InHand(s, 'jab');
    assert.equal(TurnLoop.CanPlay(s, jab, 0), PlayRefusal.None);
    assert.equal(TurnLoop.CanPlay(s, jab, 1), PlayRefusal.OutOfReach);
    assert.equal(TurnLoop.CanPlay(s, jab, 2), PlayRefusal.NoSuchTarget);
    assert.equal(TurnLoop.CanPlay(s, jab, -1), PlayRefusal.NoSuchTarget);
    const play = TurnLoop.PlayCard(s, jab, NoRng, 0);
    const hit = single(ofType(play.Events, DamageDealt));
    assert.equal(hit.Unit, 0);
    assert.equal(play.State.Enemies[0].Body.Hp, 55);
    assert.equal(play.State.Enemies[1].Body.Hp, 60);
    assert.equal(single(ofType(play.Events, CardPlayed)).Unit, 0);
  });

  test('ACardAimedAtAll_HitsEveryoneInReach_AndNobodyElse', () => {
    const sweep = Card('sweep', { face: new Face({ Power: 5, Reach: new Reach(0, 1) }), targets: TargetKind.All });
    const s = Battle(8, 0, [E('a'), E('b'), E('c')], sweep);
    const play = TurnLoop.PlayCard(s, InHand(s, 'sweep'), NoRng);
    assert.deepEqual(ofType(play.Events, DamageDealt).map((d) => d.Unit), [0, 1]);
    assert.deepEqual(play.State.Enemies.map((u) => u.Body.Hp), [55, 55, 60]);
    const far = Battle(8, 3, [E('a'), E('b')], sweep);
    assert.equal(TurnLoop.CanPlay(far, InHand(far, 'sweep')), PlayRefusal.OutOfReach);
  });

  test('ASelfCard_ReadsTheNearestEnemy_NotTheFirstInOrder', () => {
    const brace = Card('brace', {
      face: new Face({ Guard: 2 }), attributes: A.Guard,
      trait: new Trait(TraitCondition.GapAtMost, TraitEffect.GuardBonus, 3, { Threshold: 0 }), targets: TargetKind.Self,
    });
    const s = Place(Battle(8, 3, [E('a'), E('b')], brace), 6, 3);
    assert.equal(s.Nearest, 1);
    const play = TurnLoop.PlayCard(s, InHand(s, 'brace'), NoRng);
    eq(single(ofType(play.Events, CardPlayed)), new CardPlayed(Actor.Player, s.Hand.find((c) => c.Def.Id === 'brace'), 0).with({ Unit: 1 }));
    assert.equal(single(ofType(play.Events, GuardGained)).Amount, 5);
  });

  test('ThePreview_ReadsTheEnemyTheCardIsHeldOver', () => {
    const cut = Card('cut', { face: new Face({ Power: 6, Reach: new Reach(0, 1) }) });
    let s = Battle(8, 0, [E('a'), E('b')], cut);
    s = s.WithEnemy(1, s.Enemies[1].Body.with({ Guard: 3 }));
    const id = InHand(s, 'cut');
    assert.equal(TurnLoop.Preview(s, id, 0).Damage, 6);
    assert.equal(TurnLoop.Preview(s, id, 1).Damage, 3);
    assert.equal(TurnLoop.Preview(s, id, 1).InReach, true);
    assert.equal(TurnLoop.Preview(s, id, 4).InReach, false, 'nobody stands there');
  });

  test('ThePlayer_StopsShortOfTheNearestEnemy', () => {
    const s = Place(Battle(8, 3, [E('a'), E('b')]), 6, 4);
    eq(Field.Move(s, Actor.Player, 3), new Shift(2, 3, 2));
  });

  test('PushedIntoANeighbour_TheWallIsThatNeighbour', () => {
    const shove = Card('shove', { face: new Face({ Push: 2, Reach: new Reach(0, 2) }), attributes: A.Attack });
    const s = Place(Battle(8, 3, [E('a'), E('b')], shove), 3, 5);
    const play = TurnLoop.PlayCard(s, InHand(s, 'shove'), NoRng, 0);
    eq(single(ofType(play.Events, CellsMoved)), new CellsMoved(Actor.Enemy, 3, 4, true).with({ Unit: 0 }));
    eq(single(ofType(play.Events, WallHit)), new WallHit(Actor.Enemy, 1, 3, 0, 3, 0, 57).with({ Unit: 0 }));
    assert.equal(play.State.Enemies[1].Body.Cell, 5);
  });

  test('TheEnemies_TakeTheirPhasesInOrder_EachThroughStepTwelve', () => {
    let s = Battle(8, 0, [E('a'), E('b'), E('c')]);
    s = s.with({ Player: s.Player.with({ Stamina: 0 }) });
    const end = TurnLoop.EndTurn(s, NoRng);
    const events = end.Events;
    assert.deepEqual(ofType(events, ActionExecuted).map((a) => a.Unit), [0, 1, 2]);
    assert.deepEqual(ofType(events, DamageDealt).map((d) => [d.Unit, d.Damage]), [[0, 5], [1, 4], [2, 4]]);
    assert.equal(end.State.Player.Hp, 50 - 13);
    const omenOfA = events.findIndex((e) => e instanceof OmenSet && e.Decided && e.Unit === 0);
    const guardOfB = events.findIndex((e) => e instanceof GuardCleared && e.Actor === Actor.Enemy && e.Unit === 1);
    const actOfA = events.findIndex((e) => e instanceof ActionExecuted && e.Unit === 0);
    assert.ok(actOfA < omenOfA);
    assert.ok(omenOfA < guardOfB);
    assert.deepEqual(ofType(events, OmenSet).map((o) => o.Unit), [0, 1, 2]);
  });

  test('OneEnemyFalls_TheCardGoesOn_AndTheBattleToo', () => {
    const bash = Card('bash', { column: 2, face: new Face({ Power: 5, Guard: 4, Reach: Reach.Only(0) }), attributes: A.Attack | A.Guard });
    let s = Battle(8, 0, [E('a'), E('b')], bash);
    s = s.WithEnemy(0, s.Enemies[0].Body.with({ Hp: 5 }));
    const play = TurnLoop.PlayCard(s, InHand(s, 'bash'), NoRng, 0);
    assert.equal(single(ofType(play.Events, EnemyDefeated)).Unit, 0);
    assert.equal(single(ofType(play.Events, GuardGained)).Amount, 4);
    assert.equal(play.State.Result, GameResult.Ongoing);
    assert.equal(single(ofType(play.Events, DefeatChecked)).Result, GameResult.Ongoing);
    assert.equal(play.State.Enemies[0].Omen, null);
    assert.deepEqual(play.State.Living, [1]);
    eq(Field.Move(play.State, Actor.Player, 2), new Shift(2, 3, 1));
    const end = TurnLoop.EndTurn(play.State, NoRng);
    assert.deepEqual(ofType(end.Events, ActionExecuted).map((a) => a.Unit), [1]);
    const next = TurnLoop.BeginPlayerTurn(end.State, NoRng);
    assert.deepEqual(ofType(next.Events, OmenSet).map((o) => o.Unit), [1]);
  });

  test('TheLastEnemyToFall_EndsTheBattle', () => {
    const sweep = Card('sweep', { face: new Face({ Power: 5, Reach: new Reach(0, 1) }), targets: TargetKind.All });
    let s = Battle(8, 0, [E('a'), E('b')], sweep);
    s = s.WithEnemy(0, s.Enemies[0].Body.with({ Hp: 5 })).WithEnemy(1, s.Enemies[1].Body.with({ Hp: 5 }));
    const play = TurnLoop.PlayCard(s, InHand(s, 'sweep'), NoRng);
    assert.deepEqual(ofType(play.Events, EnemyDefeated).map((e) => e.Unit), [0]);
    assert.equal(play.State.Result, GameResult.Won);
    eq(last(play.Events), new BattleEnded(Actor.Player, GameResult.Won));
  });
});

// ======================================================================== TwoEnemyBattleTests

describe('TwoEnemyBattleTests', () => {
  const Lance = Card('lance', { face: new Face({ Power: 6, Reach: new Reach(0, 4) }) });
  function Pair(...hand) {
    const deck = Cards.BuildDeck(hand.length === 0 ? [Card('filler')] : hand, 1);
    const battle = single(ChainOrder.Nine, (b) => b.EnemyIds.length === 2);
    const setup = new BattleSetup(battle.Defs[0], deck, battle.FieldCells, { MoreEnemies: [battle.Defs[1]] });
    return TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoRng).State, NoRng).State;
  }

  test('ThePair_StandsAsRosterSection9Says', () => {
    const s = Pair();
    assert.equal(s.FieldCells, 7);
    assert.deepEqual(s.Enemies.map((u) => u.Def.Id), ['polearm_warped', 'crossbow_hunter']);
    assert.deepEqual([0, 1].map((i) => s.GapTo(i)), [3, 4]);
    assert.ok(s.Enemies.every((u) => u.Omen != null));
  });

  test('EachEnemy_TakesItsOwnPhaseInOrder_AndTheBattleGoesOnAfterOneFalls', () => {
    let s = Pair(Lance);
    s = s.with({ Player: s.Player.with({ Stamina: 0 }) });
    const first = TurnLoop.EndTurn(s, NoRng);
    const events = first.Events;
    assert.deepEqual(ofType(events, ActionExecuted).map((a) => a.Unit), [0, 1]);
    assert.deepEqual(ofType(events, OmenSet).filter((o) => o.Decided).map((o) => o.Unit), [0, 1]);
    const omenOfFront = events.findIndex((e) => e instanceof OmenSet && e.Decided && e.Unit === 0);
    const actOfBack = events.findIndex((e) => e instanceof ActionExecuted && e.Unit === 1);
    assert.ok(omenOfFront < actOfBack);

    let next = TurnLoop.BeginPlayerTurn(first.State, NoRng).State;
    next = next.WithEnemy(0, next.Enemies[0].Body.with({ Hp: 1, Guard: 0 }));
    const play = TurnLoop.PlayCard(next, InHand(next, 'lance'), NoRng, 0);
    assert.equal(single(ofType(play.Events, EnemyDefeated)).Unit, 0);
    assert.equal(play.State.Result, GameResult.Ongoing);
    assert.deepEqual(play.State.Living, [1]);
    const after = TurnLoop.EndTurn(play.State, NoRng);
    assert.deepEqual(ofType(after.Events, ActionExecuted).map((a) => a.Unit), [1]);
    assert.equal(after.State.Result, GameResult.Ongoing);
    const third = TurnLoop.BeginPlayerTurn(after.State, NoRng);
    assert.deepEqual(ofType(third.Events, OmenSet).map((o) => o.Unit), [1]);
  });

  test('TheEnemyBehind_DecidesFromItsOwnGap_NotFromWhereTheFrontOneStands', () => {
    const front = Enemy('front');
    const deck = Cards.BuildDeck([Card('filler')], 1);
    let start = TurnLoop.Start(new BattleSetup(front, deck, 7, { MoreEnemies: [Enemies.CrossbowHunter] }), NoRng).State;
    start = TurnLoop.BeginPlayerTurn(start, NoRng).State;
    start = start.with({ Player: start.Player.with({ Stamina: 0 }) });
    assert.equal(start.GapTo(1), 4);
    const decided = [];
    const frontOmens = [];
    for (const cell of [3, 4, 5, 6]) {
      const s = start.WithEnemy(0, start.Enemies[0].Body.with({ Cell: cell }));
      const end = TurnLoop.EndTurn(s, NoRng).State;
      assert.equal(end.GapTo(1), 4);
      decided.push(end.Enemies[1].Omen);
      frontOmens.push(end.Enemies[0].Omen.ActionId);
    }
    const fallen = start.WithEnemy(0, start.Enemies[0].Body.with({ Hp: 0 }));
    decided.push(TurnLoop.EndTurn(fallen, NoRng).State.Enemies[1].Omen);
    let alone = TurnLoop.Start(new BattleSetup(Enemies.CrossbowHunter, deck, 7, { StartGap: 4 }), NoRng).State;
    alone = TurnLoop.BeginPlayerTurn(alone, NoRng).State;
    alone = alone.with({ Player: alone.Player.with({ Stamina: 0 }) });
    const aloneOmen = TurnLoop.EndTurn(alone, NoRng).State.Enemies[0].Omen;
    assert.ok(new Set(frontOmens).size > 1);
    for (const o of decided) eq(o, aloneOmen);
  });
}, chain.skip);

// ======================================================================== OmenPlanTests

describe('OmenPlanTests', () => {
  const Cells = 8;
  const Hit = (id, power = 1, reach = null, column = 1) => EnemyAction(id, { column, face: new Face({ Power: power, Reach: reach }) });
  const Retreat = (id, cells) => EnemyAction(id, { face: new Face({ Move: -cells }), attributes: A.None, targets: TargetKind.Self });
  function Twice(atZero, atMid, atFar, { actionsPerPhase = 2, maxStamina = 10, recovery = 2, switches = null, maxHp = 60 } = {}) {
    const actions = new Map();
    for (const a of [...atZero, ...atMid, ...atFar]) actions.set(a.Id, a);
    return new EnemyDef('twice', 'twice', maxHp, maxStamina, recovery, 1,
      atZero.map((a) => a.Id), atMid.map((a) => a.Id), atFar.map((a) => a.Id),
      actions, EnemyRank.Elite, actionsPerPhase, switches);
  }
  const Filler = Card('filler', { face: new Face({ Guard: 1 }), attributes: A.Guard, targets: TargetKind.Self });
  const Deck = (...cards) => DeckOf(Filler, Constants.DeckMin, ...cards);
  const Started = (enemy, gap = 0, ...cards) => TurnLoop.Start(new BattleSetup(enemy, Deck(...cards), Cells, { StartGap: gap }), NoRng);
  const Opened = (enemy, gap = 0, ...cards) => TurnLoop.BeginPlayerTurn(Started(enemy, gap, ...cards).State, NoRng).State;
  const End = (s) => TurnLoop.EndTurn(s, NoRng);
  const shot = () => Hit('shot', 1, new Reach(1, 2));

  test('AnEliteShowsItsSecondActionAsAPlan_AtTheBattleStart_AndTheNextTurnRestatesIt', () => {
    const enemy = Twice([Hit('strike'), Hit('jab')], [shot()], [Retreat('far', 0)]);
    const start = Started(enemy);
    const begin = TurnLoop.BeginPlayerTurn(start.State, NoRng);
    assert.equal(start.State.Omen.ActionId, 'strike');
    assert.equal(start.State.Enemies[0].Plan.ActionId, 'jab');
    assert.equal(single(ofType(start.Events, PlanSet)).Plan.ActionId, 'jab');
    assert.equal(single(ofType(start.Events, PlanSet)).Decided, true);
    assert.equal(single(ofType(begin.Events, PlanSet)).Decided, false);
    assert.equal(single(ofType(begin.Events, PlanSet)).Plan.ActionId, 'jab');
  });

  test('AnEnemyThatActsOnce_HasNoPlan', () => {
    const enemy = Twice([Hit('strike'), Hit('jab')], [shot()], [Retreat('far', 0)], { actionsPerPhase: 1 });
    const start = Started(enemy);
    assert.equal(start.State.Enemies[0].Plan, null);
    assert.equal(ofType(start.Events, PlanSet).length, 0);
    assert.equal(ofType(TurnLoop.BeginPlayerTurn(start.State, NoRng).Events, PlanSet).length, 0);
  });

  test('ThePlanCountsTheFirstActionsCost_SoASecondItCannotPayForIsPlannedAsARest', () => {
    const enemy = Twice([Hit('strike', 1, null, 3), Hit('heavy', 1, null, 3)], [shot()], [Retreat('far', 0)], { maxStamina: 3, recovery: 3 });
    const start = Started(enemy);
    const end = End(TurnLoop.BeginPlayerTurn(start.State, NoRng).State);
    assert.equal(start.State.Omen.ActionId, 'strike');
    assert.equal(start.State.Enemies[0].Plan.ActionId, EnemyAi.RestActionId);
    assert.deepEqual(ofType(end.Events, ActionExecuted).map((e) => e.Action.Id), ['strike']);
    assert.equal(ofType(end.Events, PlanChanged).length, 0);
  });

  test('WhenTheFirstActionCannotBePaid_TheEnemyRests_AndThePlanShownIsReplacedByTheRest', () => {
    const enemy = Twice([Hit('strike', 1, null, 3), Hit('heavy', 1, null, 3)], [shot()], [Retreat('far', 0)]);
    let s = Started(enemy).State;
    assert.equal(s.Enemies[0].Plan.ActionId, 'heavy');
    s = s.WithEnemy(s.Enemy.with({ Stamina: 0 }));
    const end = End(TurnLoop.BeginPlayerTurn(s, NoRng).State);
    assert.equal(single(ofType(end.Events, Rested)).Declared.ActionId, 'strike');
    const changed = single(ofType(end.Events, PlanChanged));
    assert.equal(changed.Was.ActionId, 'heavy');
    assert.equal(changed.Now.ActionId, EnemyAi.RestActionId);
  });

  test('TheRosterElitesAndBosses_ShowAPlan_AndTheNormalEnemiesDoNot', () => {
    for (const enemy of Enemies.All) {
      const start = TurnLoop.Start(new BattleSetup(enemy, Deck(), Cells), NoRng).State;
      const twice = enemy.ActionsPerPhase >= 2;
      assert.equal(start.Enemies[0].Plan != null, twice && start.Omen.ActionId !== EnemyAi.RestActionId, enemy.Id);
    }
    assert.equal(Enemies.All.filter((e) => e.ActionsPerPhase >= 2).length, 6);
  });

  test('WhenTheTreeAnswersTheSameAfterTheFirstAction_NothingChanges', () => {
    const enemy = Twice([Hit('strike'), Hit('jab')], [shot()], [Retreat('far', 0)]);
    const end = End(Opened(enemy));
    assert.deepEqual(ofType(end.Events, ActionExecuted).map((e) => e.Action.Id), ['strike', 'jab']);
    assert.equal(ofType(end.Events, PlanChanged).length, 0);
  });

  test('WhenTheFirstActionMovesTheEnemyOffTheBand_ThePlanChanges_AndTheNewActionIsTheOneTaken', () => {
    const enemy = Twice([Retreat('back_off', 2), Hit('strike')], [shot()], [Retreat('far', 0)]);
    const opened = Opened(enemy);
    assert.equal(opened.Enemies[0].Plan.ActionId, 'strike');
    const end = End(opened);
    const changed = single(ofType(end.Events, PlanChanged));
    assert.equal(changed.Was.ActionId, 'strike');
    assert.equal(changed.Now.ActionId, 'shot');
    assert.deepEqual(ofType(end.Events, ActionExecuted).map((e) => e.Action.Id), ['back_off', 'shot']);
    const kinds = typeNames(end.Events);
    const change = kinds.indexOf('PlanChanged');
    const second = kinds.lastIndexOf('ActionExecuted');
    assert.ok(change < second);
    assert.ok(change > kinds.indexOf('ActionExecuted'));
  });

  test('AfterThePhase_TheNextOmenDecidesAFreshPlan', () => {
    const enemy = Twice([Hit('strike'), Hit('jab')], [shot()], [Retreat('far', 0)]);
    const end = End(Opened(enemy));
    assert.equal(end.State.Enemies[0].Plan.ActionId, 'jab');
    assert.equal(last(ofType(end.Events, PlanSet)).Decided, true);
    assert.equal(last(ofType(end.Events, OmenSet)).Decided, true);
  });

  const Base = Hit('base_hit');
  const Rage = Hit('rage_hit', 3);
  const Turtle = EnemyAction('shell', { face: new Face({ Guard: 4 }), attributes: A.Guard, targets: TargetKind.Self });
  const Boss = (...switches) => Twice([Base, Rage, Turtle], [shot()], [Retreat('far', 0)], { actionsPerPhase: 1, switches });

  test('AStageByHp_SwapsTheTree_ThroughTheHook', () => {
    const stage = new TreeSwitch('stage_2', (view) => view.Hp * 2 <= view.MaxHp, { BranchAtGapZero: ['rage_hit'] });
    const enemy = Boss(stage);
    const opened = Opened(enemy);
    assert.equal(opened.Omen.ActionId, 'base_hit');
    const hurt = opened.WithEnemy(opened.Enemy.with({ Hp: 30 }));
    const end = End(hurt);
    eq(single(ofType(end.Events, TreeSwitched)), new TreeSwitched(Actor.Enemy, null, 'stage_2'));
    assert.equal(end.State.Omen.ActionId, 'rage_hit');
    assert.equal(end.State.Enemies[0].ActiveSwitch, 'stage_2');
  });

  test('AnAdaptationByTheHistory_SwapsTheTreeThroughTheSameHook', () => {
    const adapt = new TreeSwitch('hand_made_shell',
      (view) => view.Player.Cards.length >= 2 && view.Player.Cards.slice(view.Player.Cards.length - 2).every((c) => c === A.Attack),
      { BranchAtGapZero: ['shell'] });
    const enemy = Boss(adapt);
    const jab = Card('jab', { face: new Face({ Power: 1 }) });
    const s = Opened(enemy, 0, jab, Filler, Filler, Filler, Filler, Filler, jab);
    const afterOne = TurnLoop.PlayCard(s, s.Hand.find((c) => c.Def.Id === 'jab').InstanceId, NoRng).State;
    assert.equal(End(afterOne).State.Omen.ActionId, 'base_hit');
    const next = TurnLoop.BeginPlayerTurn(End(afterOne).State, NoRng).State;
    const afterTwo = TurnLoop.PlayCard(next, next.Hand.find((c) => c.Def.Id === 'jab').InstanceId, NoRng).State;
    const end = End(afterTwo);
    assert.deepEqual([...afterTwo.History.Cards], [A.Attack, A.Attack]);
    assert.equal(single(ofType(end.Events, TreeSwitched)).To, 'hand_made_shell');
    assert.equal(end.State.Omen.ActionId, 'shell');
  });

  test('OfTheSwitchesThatHold_TheLastWins_AndABranchItLeavesNullKeepsTheBase', () => {
    const stage2 = new TreeSwitch('stage_2', (view) => view.Hp * 3 <= view.MaxHp * 2, { BranchAtGapZero: ['rage_hit'] });
    const stage3 = new TreeSwitch('stage_3', (view) => view.Hp * 3 <= view.MaxHp, { BranchAtGapZero: ['shell'] });
    const enemy = Boss(stage2, stage3);
    const opened = Opened(enemy);
    const both = End(opened.WithEnemy(opened.Enemy.with({ Hp: 15 })));
    const onlyFirst = End(opened.WithEnemy(opened.Enemy.with({ Hp: 35 })));
    assert.equal(both.State.Enemies[0].ActiveSwitch, 'stage_3');
    assert.equal(both.State.Omen.ActionId, 'shell');
    assert.equal(onlyFirst.State.Enemies[0].ActiveSwitch, 'stage_2');
    assert.equal(onlyFirst.State.Omen.ActionId, 'rage_hit');
    assert.deepEqual([...EnemyAi.BranchFor(enemy, 2, stage3)], [...enemy.BranchAtGapOneToTwo]);
  });

  test('ASwitchThatStopsHolding_LetsTheBaseTreeBack_AndIsAnnounced', () => {
    const stage = new TreeSwitch('stage_2', (view) => view.Hp * 2 <= view.MaxHp, { BranchAtGapZero: ['rage_hit'] });
    const enemy = Boss(stage);
    const opened = Opened(enemy);
    const hurt = End(opened.WithEnemy(opened.Enemy.with({ Hp: 30 }))).State;
    assert.equal(hurt.Enemies[0].ActiveSwitch, 'stage_2');
    const healed = hurt.WithEnemy(hurt.Enemy.with({ Hp: 60 }));
    const end = End(TurnLoop.BeginPlayerTurn(healed, NoRng).State);
    eq(single(ofType(end.Events, TreeSwitched)), new TreeSwitched(Actor.Enemy, 'stage_2', null));
    assert.equal(end.State.Omen.ActionId, 'base_hit');
  });

  test('AnEnemyWithoutSwitches_NeverAsksTheHook', () => {
    const end = End(Opened(Boss()));
    assert.equal(ofType(end.Events, TreeSwitched).length, 0);
    assert.equal(end.State.Enemies[0].ActiveSwitch, null);
  });

  test('TheHistory_RecordsTheAttributesPlayed_AndEachTurnsEnd', () => {
    const guard = Card('guard4', { face: new Face({ Guard: 4 }), attributes: A.Guard, targets: TargetKind.Self });
    const step = Card('step', { face: new Face({ Move: 1 }), attributes: A.None, targets: TargetKind.Self });
    let s = Opened(Twice([Base], [Base], [Retreat('far', 0)], { actionsPerPhase: 1 }), 2, guard, step);
    s = TurnLoop.PlayCard(s, s.Hand.find((c) => c.Def.Id === 'guard4').InstanceId, NoRng).State;
    s = TurnLoop.PlayCard(s, s.Hand.find((c) => c.Def.Id === 'step').InstanceId, NoRng).State;
    const end = End(s);
    assert.deepEqual([...end.State.History.Cards], [A.Guard, A.Skill]);
    const turn = single(end.State.History.TurnEnds);
    assert.ok(turn.Guard >= 4);
    assert.equal(turn.Moved, true);
    assert.equal(turn.Gap, 1);
  });
});

// ======================================================================== PreviewTests

describe('PreviewTests', () => {
  const Cells = 8;
  const Wait = EnemyAction('wait', { face: new Face(), attributes: A.Guard, targets: TargetKind.Self });
  const Idle = Enemy('idle', { atZero: Wait, atOneToTwo: Wait, atThreePlus: Wait });
  const Filler = Card('filler', { face: new Face({ Guard: 1 }), attributes: A.Guard, targets: TargetKind.Self });
  const TwoHits = Card('two_hits', { face: new Face({ Power: 5, Hits: 2 }) });
  const TwinCut = Card('twin_cut', { face: new Face({ Power: 6, Hits: 2, Statuses: [new StatusGrant(StatusKind.Fragile, 1)] }) });

  function Opened(enemy, gap, ...cards) {
    const deck = DeckOf(Filler, Constants.HandDraw, ...cards);
    const start = TurnLoop.Start(new BattleSetup(enemy, deck, Cells, { StartGap: gap }), NoRng).State;
    return TurnLoop.BeginPlayerTurn(start, NoRng).State;
  }
  function OnEnemy(events, unit = 0) {
    let raw = 0; let damage = 0;
    for (const e of events) {
      if (e instanceof DamageDealt && e.Actor === Actor.Player && e.Unit === unit) { raw += e.Raw; damage += e.Damage; }
      if (e instanceof WallHit && e.Actor === Actor.Enemy && e.Unit === unit) { raw += e.Raw; damage += e.Damage; }
    }
    return [raw, damage];
  }
  function OnPlayer(events) {
    let raw = 0; let damage = 0;
    for (const e of events) {
      if (e instanceof DamageDealt && e.Target === Actor.Player) { raw += e.Raw; damage += e.Damage; }
      if (e instanceof WallHit && e.Actor === Actor.Player) { raw += e.Raw; damage += e.Damage; }
    }
    return [raw, damage];
  }
  function AssertPreviewIsWhatPlayDeals(s, cardId, raw, damage) {
    const preview = TurnLoop.Preview(s, InHand(s, cardId));
    const dealt = OnEnemy(TurnLoop.PlayCard(s, InHand(s, cardId), NoRng).Events);
    assert.deepEqual([preview.RawPower, preview.Damage], dealt, 'the preview is what the card deals');
    assert.deepEqual(dealt, [raw, damage]);
  }

  test('TwoBlows_AddUp_AndEachMeetsGuardOnItsOwn', () => {
    let s = Opened(Idle, 1, TwoHits);
    s = s.WithEnemy(s.Enemy.with({ Guard: 4 }));
    AssertPreviewIsWhatPlayDeals(s, 'two_hits', 10, 6);
  });

  test('TheFragileTheFirstBlowLeaves_CountsOnTheSecond', () => {
    AssertPreviewIsWhatPlayDeals(Opened(Idle, 1, TwinCut), 'twin_cut', 15, 15);
  });

  test('FragileIsSpentPerBlow_AndEmpowerRidesTheBlowsItDoesNotTake', () => {
    let s = Opened(Idle, 1, TwoHits);
    s = s.with({ Player: s.Player.with({ Statuses: SS([StatusKind.Empower, 1]) }) });
    s = s.WithEnemy(s.Enemy.with({ Statuses: SS([StatusKind.Fragile, 1]) }));
    AssertPreviewIsWhatPlayDeals(s, 'two_hits', 16, 16);
  });

  test('AWaitingFollowUp_RidesTheFirstBlowOnly', () => {
    let s = Opened(Idle, 1, TwoHits);
    s = s.with({ Player: s.Player.with({ FollowUp: 5 }) });
    AssertPreviewIsWhatPlayDeals(s, 'two_hits', 15, 15);
  });

  test('APushIntoTheWall_AddsTheWallDamage', () => {
    let s = Opened(Idle, 0, CardCatalog.ShieldPush);
    s = s.with({ Player: s.Player.with({ Cell: Cells - 1 }) });
    s = s.WithEnemy(s.Enemy.with({ Cell: Cells, Guard: 2 }));
    AssertPreviewIsWhatPlayDeals(s, 'shield_push', 3, 1);
    assert.equal(TurnLoop.Preview(s, InHand(s, 'shield_push')).Attacks, false);
  });

  test('ACardThePlayerCannotPayFor_StillPreviewsItsNumber', () => {
    const s = Opened(Idle, 1, TwinCut);
    const poor = s.with({ Player: s.Player.with({ Stamina: 0 }) });
    const preview = TurnLoop.Preview(poor, InHand(poor, 'twin_cut'));
    assert.equal(TurnLoop.CanPlay(poor, InHand(poor, 'twin_cut')), PlayRefusal.NotEnoughStamina);
    assert.deepEqual([preview.RawPower, preview.Damage], [15, 15]);
  });

  test('EveryBlowIsCounted_EvenWhenTheFirstWouldFellTheEnemy', () => {
    let s = Opened(Idle, 1, TwoHits);
    s = s.WithEnemy(s.Enemy.with({ Hp: 4 }));
    const preview = TurnLoop.Preview(s, InHand(s, 'two_hits'));
    assert.deepEqual([preview.RawPower, preview.Damage], [10, 10]);
    assert.equal(s.Enemy.Hp, 4);
  });

  test('OutOfReach_TheCardShowsOneBlowWorkedOutWithoutResolving', () => {
    const s = Opened(Idle, 3, TwoHits);
    const preview = TurnLoop.Preview(s, InHand(s, 'two_hits'));
    assert.equal(preview.InReach, false);
    assert.equal(preview.RawPower, 5);
    eq(preview, new PlayPreview(false, true, 5, 5, 0, false, 1));
  });

  const FragileOnAttack = new StanceDef(StanceHook.StatusOnAttack, { Status: StatusKind.Fragile, StatusStacks: 1 });

  test('TheFragileAStanceGivesOnTheFirstBlow_CountsOnTheSecond', () => {
    const stanceCard = Card('sting_stance', { face: new Face({ Stance: FragileOnAttack }), attributes: A.Stance, targets: TargetKind.Self });
    const twoSix = Card('two_six', { face: new Face({ Power: 6, Hits: 2 }) });
    let s = Opened(Idle, 1, stanceCard, twoSix);
    s = TurnLoop.PlayCard(s, InHand(s, 'sting_stance'), NoRng).State;
    AssertPreviewIsWhatPlayDeals(s, 'two_six', 15, 15);
    assert.equal(s.Enemy.Statuses.Has(StatusKind.Fragile), false);
  });

  test('ATwoBlowOmen_CountsTheFragileTheEnemysStanceGivesOnTheFirstBlow', () => {
    const twin = EnemyAction('twin', { column: 3, face: new Face({ Power: 6, Hits: 2, Reach: Reach.Only(0) }) });
    let s = Opened(Enemy('twin_fixture', { atZero: twin }), 0);
    s = s.WithEnemy(s.Enemy.WithStance(FragileOnAttack, 'enemy_stance'));
    s = s.with({ Player: s.Player.with({ Stamina: 0 }) });
    assert.equal(s.Omen.ActionId, 'twin');
    const omen = TurnLoop.PreviewOmen(s, 0);
    const dealt = OnPlayer(TurnLoop.EndTurn(s, NoRng).Events);
    eq(omen, new OmenPreview(15, 15, true, false));
    assert.deepEqual(dealt, [15, 15]);
  });

  test('ATwoBlowOmen_IsOneSum_OfBothBlows', () => {
    let s = Opened(Enemies.TwinBladeWarped, 0);
    s = s.with({ Player: s.Player.with({ Stamina: 0 }) });
    assert.equal(s.Omen.ActionId, 'twin_slash');
    const omen = TurnLoop.PreviewOmen(s, 0);
    const dealt = OnPlayer(TurnLoop.EndTurn(s, NoRng).Events);
    eq(omen, new OmenPreview(16, 16, true, false));
    assert.deepEqual(dealt, [16, 16]);
  });

  test('ATwoBlowOmen_MeetsThePlayersGuardBlowByBlow', () => {
    let s = Opened(Enemies.TwinBladeWarped, 0);
    s = s.with({ Player: s.Player.with({ Stamina: 0, Guard: 10 }) });
    const omen = TurnLoop.PreviewOmen(s, 0);
    const dealt = OnPlayer(TurnLoop.EndTurn(s, NoRng).Events);
    assert.deepEqual([omen.RawPower, omen.Damage], [16, 6]);
    assert.deepEqual(dealt, [16, 6]);
  });

  test('AnOmenThatDoesNotReach_SaysSo_AndStillShowsWhatItWouldDoIfItLanded', () => {
    let s = Opened(Enemies.TwinBladeWarped, 0);
    s = s.with({ Player: s.Player.with({ Cell: s.Player.Cell - 1 }) });
    assert.equal(s.Gap, 1);
    const omen = TurnLoop.PreviewOmen(s, 0);
    assert.equal(omen.Lands, false);
    assert.equal(omen.RawPower, 16);
    assert.equal(s.Player.Cell, 1);
  });

  test('AnOmenTheEnemyCannotPayFor_IsARest_WorthNothing', () => {
    let s = Opened(Enemies.TwinBladeWarped, 0);
    s = s.WithEnemy(s.Enemy.with({ Stamina: 0 }));
    eq(TurnLoop.PreviewOmen(s, 0), new OmenPreview(0, 0, false, true));
  });

  test('AShoveOmen_CountsItsTraitAndTheWall', () => {
    let s = Opened(Enemies.PolearmWarped, 0);
    s = s.with({ Player: s.Player.with({ Stamina: 0 }) });
    assert.equal(s.Omen.ActionId, 'shove');
    const omen = TurnLoop.PreviewOmen(s, 0);
    const dealt = OnPlayer(TurnLoop.EndTurn(s, NoRng).Events);
    assert.deepEqual([omen.RawPower, omen.Damage], [12, 12]);
    assert.deepEqual(dealt, [12, 12]);
  });

  test('TheReserveGuardTheTurnEndWouldGive_IsCountedBeforeTheOmen', () => {
    const s = Opened(Enemies.PolearmWarped, 0);
    assert.ok(s.Player.Stamina >= Constants.ReserveThreshold);
    assert.equal(s.Player.Guard, 0);
    const omen = TurnLoop.PreviewOmen(s, 0);
    const dealt = OnPlayer(TurnLoop.EndTurn(s, NoRng).Events);
    assert.deepEqual([omen.RawPower, omen.Damage], [9, 6]);
    assert.deepEqual(dealt, [9, 6]);
    assert.equal(s.Player.Guard, 0);
  });

  test('PreviewOmen and Preview return null where the C# does', () => {
    const s = Opened(Idle, 1, TwoHits);
    assert.equal(TurnLoop.Preview(s, 'nobody-0'), null);
    assert.equal(TurnLoop.PreviewOmen(s, 3), null);
    assert.equal(TurnLoop.PreviewOmen(s.WithOmen(0, null), 0), null);
    eq(TurnLoop.PreviewOmen(s, 0), new OmenPreview(0, 0, false, false), 'an omen aimed at nobody');
  });
});

// ======================================================================== SwiftAndFocusTests (TurnLoop)

describe('SwiftAndFocusTests', () => {
  const Cells = 8;
  const Wait = EnemyAction('wait', { face: new Face(), attributes: A.Guard, targets: TargetKind.Self });
  const Idle = Enemy('idle', { atZero: Wait, atOneToTwo: Wait, atThreePlus: Wait });
  const Lunger = Enemy('lunger', { atOneToTwo: EnemyAction('lunge', { face: new Face({ Power: 5, Reach: Reach.Only(0) }) }) });
  const Spearman = Enemy('spearman', { atOneToTwo: EnemyAction('long_thrust', { face: new Face({ Power: 4, Reach: new Reach(2, 3) }) }) });
  const Filler = Card('filler', { face: new Face({ Guard: 1 }), attributes: A.Guard, targets: TargetKind.Self });
  const Started = (gap, enemy, ...cards) => TurnLoop.Start(new BattleSetup(enemy, DeckOf(Filler, Constants.HandDraw, ...cards), Cells, { StartGap: gap }), NoRng).State;
  const Opened = (gap, enemy, ...cards) => TurnLoop.BeginPlayerTurn(Started(gap, enemy, ...cards), NoRng).State;
  const Play = (s, id) => TurnLoop.PlayCard(s, InHand(s, id), NoRng);
  const Begin = (s) => TurnLoop.BeginPlayerTurn(s, NoRng);
  const End = (s) => TurnLoop.EndTurn(s, NoRng);
  const WithPlayerStatuses = (s, ...e) => s.with({ Player: s.Player.with({ Statuses: SS(...e) }) });
  const WithEnemyStatuses = (s, ...e) => s.WithEnemy(s.Enemy.with({ Statuses: SS(...e) }));
  const IndexOf = (events, T) => events.findIndex((e) => e instanceof T);

  test('Swift_OpensOneFreeCell_AtTheTurnStart_ForwardOrBack', () => {
    const t1 = Begin(WithPlayerStatuses(Started(2, Idle), [StatusKind.Swift, 2]));
    const forward = TurnLoop.TakeFreeStep(t1.State, 1);
    const back = TurnLoop.TakeFreeStep(t1.State, -1);
    assert.equal(TurnLoop.CanTakeFreeStep(t1.State), true);
    eq(forward.Events, [new FreeStepTaken(Actor.Player, 1), new CellsMoved(Actor.Player, 2, 3, false)]);
    assert.equal(forward.State.Player.Cell, 3);
    assert.equal(forward.State.Player.Stamina, t1.State.Player.Stamina);
    assert.equal(forward.State.Player.Moved, true);
    assert.equal(back.State.Player.Cell, 1);
    assert.equal(TurnLoop.CanTakeFreeStep(forward.State), false);
    assert.throws(() => TurnLoop.TakeFreeStep(forward.State, 1), InvalidOperationException);
    assert.throws(() => TurnLoop.TakeFreeStep(t1.State, 2), {
      name: 'ArgumentOutOfRangeException',
      message: "The free step is one cell: +1 (forward) or -1 (back). (Parameter 'direction')\nActual value was 2.",
    });
  });

  test('Swift_WithoutTheWord_OpensNoFreeCell', () => {
    const t1 = Opened(2, Idle);
    assert.equal(TurnLoop.CanTakeFreeStep(t1), false);
    assert.throws(() => TurnLoop.TakeFreeStep(t1, 1), {
      message: 'TakeFreeStep: the player has no free step this turn (no 俊敏, or already taken or let go).',
    });
  });

  test('Swift_TheFreeCell_IsAnOwnMove_ForTheBossWords_AndRecordsItsWay', () => {
    const bound = Begin(WithPlayerStatuses(Started(2, Idle), [StatusKind.Swift, 2], [StatusKind.Binding, 2]));
    const hooked = Begin(WithPlayerStatuses(Started(2, Idle), [StatusKind.Swift, 2], [StatusKind.Hook, 1]));
    const free = Begin(WithPlayerStatuses(Started(2, Idle), [StatusKind.Swift, 2]));
    const boundStep = TurnLoop.TakeFreeStep(bound.State, 1);
    const caughtBack = TurnLoop.TakeFreeStep(hooked.State, -1);
    const forward = TurnLoop.TakeFreeStep(free.State, 1);
    assert.equal(single(ofType(boundStep.Events, MoveBlocked)).By, StatusKind.Binding);
    assert.equal(boundStep.State.Player.Cell, 2);
    assert.equal(single(ofType(caughtBack.Events, MoveBlocked)).By, StatusKind.Hook);
    assert.equal(caughtBack.State.Player.Cell, 2);
    assert.equal(caughtBack.State.Player.Statuses.Has(StatusKind.Hook), false);
    assert.equal(forward.State.Player.LastMove, 1);
  });

  test('Swift_LosesOneStackPerTurn_AndGivesOneTurnPerStack', () => {
    const t1 = Begin(WithPlayerStatuses(Started(2, Idle), [StatusKind.Swift, 2]));
    const t2 = Begin(End(t1.State).State);
    const t3 = Begin(End(t2.State).State);
    eq(single(ofType(t1.Events, StatusTicked)), new StatusTicked(Actor.Player, StatusKind.Swift, 1));
    assert.equal(TurnLoop.CanTakeFreeStep(t1.State), true);
    eq(single(ofType(t2.Events, StatusTicked)), new StatusTicked(Actor.Player, StatusKind.Swift, 0));
    assert.equal(t2.State.Player.Statuses.Has(StatusKind.Swift), false);
    assert.equal(TurnLoop.CanTakeFreeStep(t2.State), true);
    assert.equal(ofType(t3.Events, StatusTicked).length, 0);
    assert.equal(TurnLoop.CanTakeFreeStep(t3.State), false);
  });

  test('Swift_StopsAtFourStacks', () => {
    const quicken = Card('quicken', { face: new Face({ Statuses: [new StatusGrant(StatusKind.Swift, 3, true)] }), attributes: A.Skill, targets: TargetKind.Self });
    const s = WithPlayerStatuses(Opened(2, Idle, quicken), [StatusKind.Swift, 3]);
    const play = Play(s, 'quicken');
    const next = Begin(End(play.State).State);
    eq(single(ofType(play.Events, StatusApplied)), new StatusApplied(Actor.Player, Actor.Player, StatusKind.Swift, 3, 4, false).with({ Dropped: 2 }));
    assert.equal(play.State.Player.Statuses.Stacks(StatusKind.Swift), 4);
    eq(single(ofType(next.Events, StatusTicked)), new StatusTicked(Actor.Player, StatusKind.Swift, 3));
  });

  test('Swift_PlayingACard_OrEndingTheTurn_LetsTheFreeCellGo', () => {
    const t1 = Begin(WithPlayerStatuses(Started(2, Idle), [StatusKind.Swift, 2]));
    const played = Play(t1.State, 'filler');
    const ended = End(t1.State);
    assert.equal(TurnLoop.CanTakeFreeStep(played.State), false);
    assert.throws(() => TurnLoop.TakeFreeStep(played.State, 1), InvalidOperationException);
    assert.equal(ended.State.Player.FreeStep, false);
    assert.equal(ended.State.Player.Cell, 2);
  });

  test('Swift_UnderSlow_IsSpentWithoutMoving', () => {
    const t1 = Begin(WithPlayerStatuses(Started(2, Idle), [StatusKind.Swift, 2], [StatusKind.Slow, 2]));
    const step = TurnLoop.TakeFreeStep(t1.State, 1);
    eq(step.Events, [new FreeStepTaken(Actor.Player, 1), new MoveBlocked(Actor.Player, StatusKind.Slow)]);
    assert.equal(step.State.Player.Cell, 2);
    assert.equal(step.State.Player.Moved, false);
    assert.equal(TurnLoop.CanTakeFreeStep(step.State), false);
  });

  test('Swift_TheFreeCell_CountsAsMoved_ForTheCardAfterIt', () => {
    const afterStep = Card('after_step', { face: new Face({ Power: 6 }), trait: new Trait(TraitCondition.Moved, TraitEffect.PowerBonus, 3) });
    const t1 = TurnLoop.BeginPlayerTurn(WithPlayerStatuses(Started(1, Idle, afterStep), [StatusKind.Swift, 1]), NoRng);
    const step = TurnLoop.TakeFreeStep(t1.State, 1);
    const play = Play(step.State, 'after_step');
    assert.equal(single(ofType(play.Events, DamageDealt)).Raw, 9);
  });

  test('AnEnemy_WithSwift_StepsForward_IntoItsOmensReach', () => {
    const opened = Opened(1, Lunger);
    const swift = WithEnemyStatuses(opened, [StatusKind.Swift, 1]);
    const plain = End(opened);
    const stepped = End(swift);
    assert.equal(ofType(plain.Events, ActionWhiffed).length, 1);
    eq(single(ofType(stepped.Events, FreeStepTaken)), new FreeStepTaken(Actor.Enemy, 1));
    eq(ofType(stepped.Events, CellsMoved)[0], new CellsMoved(Actor.Enemy, 4, 3, false));
    assert.equal(ofType(stepped.Events, ActionWhiffed).length, 0);
    assert.equal(single(ofType(stepped.Events, DamageDealt)).Raw, 5);
    assert.ok(IndexOf(stepped.Events, FreeStepTaken) < IndexOf(stepped.Events, ActionExecuted));
    assert.ok(IndexOf(stepped.Events, StatusTicked) < IndexOf(stepped.Events, FreeStepTaken));
    assert.equal(TurnLoop.PreviewOmen(opened, 0).Lands, false);
    assert.equal(TurnLoop.PreviewOmen(swift, 0).Lands, true);
  });

  test('AnEnemy_WithSwift_StepsBack_WhenThePlayerIsTooClose', () => {
    const s = WithEnemyStatuses(Opened(1, Spearman), [StatusKind.Swift, 1]);
    const end = End(s);
    eq(single(ofType(end.Events, FreeStepTaken)), new FreeStepTaken(Actor.Enemy, -1));
    eq(ofType(end.Events, CellsMoved)[0], new CellsMoved(Actor.Enemy, 4, 5, false));
    assert.equal(ofType(end.Events, ActionWhiffed).length, 0);
    assert.equal(single(ofType(end.Events, DamageDealt)).Raw, 4);
  });

  test('AnEnemy_WithSwift_Stays_WhenItsOmenAlreadyReaches_OrAimsAtNobody', () => {
    const inReach = End(WithEnemyStatuses(Opened(1, Enemy()), [StatusKind.Swift, 2]));
    const idle = End(WithEnemyStatuses(Opened(1, Idle), [StatusKind.Swift, 2]));
    assert.equal(ofType(inReach.Events, FreeStepTaken).length, 0);
    eq(single(ofType(inReach.Events, StatusTicked)), new StatusTicked(Actor.Enemy, StatusKind.Swift, 1));
    assert.equal(ofType(idle.Events, FreeStepTaken).length, 0);
    assert.equal(ofType(idle.Events, CellsMoved).length, 0);
  });

  test('Focus_TakesTheBleedStacks_FromTheRightColumn_WithThePower', () => {
    const cut = Card('cut', { column: 2, face: new Face({ Power: 13, Statuses: [new StatusGrant(StatusKind.Bleed, 2)] }) });
    const s = WithPlayerStatuses(Opened(1, Idle, cut), [StatusKind.Focus, 1]);
    const play = Play(s, 'cut');
    eq(FocusStep.Of(A.Attack, 2, cut.Face), new FocusStep(8, 0, 0, 1));
    assert.equal(single(ofType(play.Events, DamageDealt)).Raw, 21);
    eq(single(ofType(play.Events, StatusApplied)), new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Bleed, 3, 3, false));
    assert.equal(play.State.Enemy.Statuses.Stacks(StatusKind.Bleed), 3);
  });

  test('Focus_TakesTheRegenStacks_AndTheHeal_FromTheRightColumn', () => {
    const mend = Card('mend', { column: 3, face: new Face({ Heal: 13, Statuses: [new StatusGrant(StatusKind.Regen, 3, true)] }), attributes: A.Skill, targets: TargetKind.Self });
    let s = Opened(1, Idle, mend);
    s = s.with({ Player: s.Player.with({ Hp: 20, Statuses: SS([StatusKind.Focus, 1]) }) });
    const play = Play(s, 'mend');
    eq(single(ofType(play.Events, Healed)), new Healed(Actor.Player, 19, 39));
    eq(single(ofType(play.Events, StatusApplied)), new StatusApplied(Actor.Player, Actor.Player, StatusKind.Regen, 4, 4, false));
  });

  test('Focus_AtColumnFour_LeavesTheStacksAsTheyAre', () => {
    const deep = Card('deep_cut', { column: 4, face: new Face({ Power: 30, Statuses: [new StatusGrant(StatusKind.Bleed, 4)] }) });
    const s = WithPlayerStatuses(Opened(1, Idle, deep), [StatusKind.Focus, 1]);
    const play = Play(s, 'deep_cut');
    assert.equal(single(ofType(play.Events, DamageDealt)).Raw, 30);
    assert.equal(play.State.Enemy.Statuses.Stacks(StatusKind.Bleed), 4);
  });

  test('Focus_LeavesTheOtherWords_TheBreak_AndTheTraitsGrant_AsTheyAre', () => {
    const card = Card('rend', {
      column: 2,
      face: new Face({ Power: 13, Break: 1, Statuses: [new StatusGrant(StatusKind.Bleed, 2), new StatusGrant(StatusKind.Fragile, 2)] }),
      trait: new Trait(TraitCondition.FirstPlay, TraitEffect.Status, { Grant: new StatusGrant(StatusKind.Regen, 1, true) }),
    });
    const s = WithPlayerStatuses(Opened(1, Idle, card), [StatusKind.Focus, 1]);
    const play = Play(s, 'rend');
    eq(ofType(play.Events, StatusApplied), [
      new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Bleed, 3, 3, false),
      new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Fragile, 2, 2, false),
      new StatusApplied(Actor.Player, Actor.Player, StatusKind.Regen, 1, 1, false),
    ]);
    assert.equal(single(ofType(play.Events, StaminaBroken)).Amount, 1);
  });

  test('WithoutFocus_TheStacksAreTheCardsOwn', () => {
    const cut = Card('cut', { column: 2, face: new Face({ Power: 13, Statuses: [new StatusGrant(StatusKind.Bleed, 2)] }) });
    const play = Play(Opened(1, Idle, cut), 'cut');
    assert.equal(play.State.Enemy.Statuses.Stacks(StatusKind.Bleed), 2);
  });

  test('FocusStep: None, the dual scale, and StacksOf', () => {
    eq(FocusStep.None, new FocusStep(0, 0, 0, 0));
    eq(FocusStep.Of(A.Attack, 5, new Face({ Power: 3 })), FocusStep.None, 'an invalid column is None');
    eq(FocusStep.Of(A.Attack | A.Guard, 1, new Face({ Power: 4, Guard: 3 })), new FocusStep(4, 3, 0, 0), 'two faces read the dual scale');
    eq(FocusStep.Of(A.Attack, 1, new Face({ Power: 4, Move: 1 })), new FocusStep(4, 0, 0, 0), 'a face beside a move is dual');
    eq(FocusStep.Of(A.None, 1, new Face({ Guard: 2, Move: 1 })), FocusStep.None, "a move card's small Guard does not grow");
    const step = new FocusStep(0, 0, 0, 2);
    assert.equal(step.StacksOf(new StatusGrant(StatusKind.Regen, 1)), 3);
    assert.equal(step.StacksOf(new StatusGrant(StatusKind.Slow, 2)), 2);
  });
});

// ======================================================================== FloorReadTests (TurnLoop)

describe('FloorReadTests', () => {
  const Cells = BattleSetup.SliceFieldCells;
  const Wait = EnemyAction('wait', { face: new Face(), attributes: A.Guard, targets: TargetKind.Self });
  const Idle = Enemy('idle', { atZero: Wait, atOneToTwo: Wait, atThreePlus: Wait });
  const Filler = Card('filler', { face: new Face({ Guard: 1 }), attributes: A.Guard, targets: TargetKind.Self });
  function Opened(enemy, gap) {
    const deck = DeckOf(Filler, Constants.HandDraw * 3);
    const start = TurnLoop.Start(new BattleSetup(enemy, deck, Cells, { StartGap: gap }), NoRng).State;
    return TurnLoop.BeginPlayerTurn(start, NoRng).State;
  }

  test('ThePolearmsSweep_ThickensOnlyTheAimedCellAtGapTwo', () => {
    const state = Opened(Enemies.PolearmWarped, 2);
    assert.equal(state.Enemies[0].Omen.ActionId, 'sweep');
    assert.deepEqual([...EnemyAi.TargetCells(state.Enemies[0].Omen.Label, state.Enemy)], [2, 3]);
    assert.deepEqual([...TurnLoop.OmenBoundaryCells(state, 0)], [2]);
    const close = Opened(Enemies.PolearmWarped, 1);
    assert.equal(close.Enemies[0].Omen.ActionId, 'sweep');
    assert.deepEqual([...TurnLoop.OmenBoundaryCells(close, 0)], [1]);
  });

  test('AnOmenWithoutABoundary_ThickensNothing', () => {
    const state = Opened(Enemies.PolearmWarped, 0);
    assert.equal(state.Enemies[0].Omen.ActionId, 'shove');
    assert.deepEqual([...TurnLoop.OmenBoundaryCells(state, 0)], []);
    const rested = state.WithOmen(0, EnemyAi.OmenOf(null));
    assert.deepEqual([...TurnLoop.OmenBoundaryCells(rested, 0)], []);
  });

  test('ThePolearmsStep_TakesItOneCellIn', () => {
    const state = Opened(Enemies.PolearmWarped, 3);
    assert.equal(state.Enemies[0].Omen.ActionId, 'step_forward');
    assert.equal(TurnLoop.OmenMoveCell(state, 0), 5);
  });

  test('TheHoundsLunge_StopsOneCellPastThePlayer', () => {
    const state = Opened(Enemies.ShadowHound, 2);
    assert.equal(state.Enemies[0].Omen.ActionId, 'lunge_in');
    assert.equal(TurnLoop.OmenMoveCell(state, 0), 3);
    assert.equal(TurnLoop.OmenMoveCell(Opened(Enemies.ShadowHound, 1), 0), 3);
  });

  test('AMoveThatGoesNowhere_HasNoCell', () => {
    const state = Opened(Enemies.PolearmWarped, 3);
    const slowed = state.WithEnemy(state.Enemy.with({ Statuses: SS([StatusKind.Slow, 2]) }));
    assert.equal(TurnLoop.OmenMoveCell(slowed, 0), -1);
    assert.equal(TurnLoop.OmenMoveCell(Opened(Enemies.PolearmWarped, 2), 0), -1, '払い斬り');
  });

  test('TheRecovery_IsThreeUnderTheMaximum', () => {
    const state = Opened(Idle, 3);
    const spent = state.with({ Player: state.Player.with({ Stamina: 4 }) });
    assert.equal(TurnLoop.PreviewRecovery(spent), Constants.StaminaRecovery);
    assert.equal(TurnLoop.PreviewRecovery(spent.with({ Player: spent.Player.with({ Stamina: 9 }) })), 1);
    assert.equal(TurnLoop.PreviewRecovery(state), 0);
  });

  test('TheRecovery_CountsFatigueAndTheBonusLeftForIt', () => {
    const state = Opened(Idle, 3);
    const spent = state.with({ Player: state.Player.with({ Stamina: 2 }) });
    assert.equal(TurnLoop.PreviewRecovery(spent.with({ Player: spent.Player.with({ Statuses: SS([StatusKind.Fatigue, 1]) }) })), 2);
    assert.equal(TurnLoop.PreviewRecovery(spent.with({ Player: spent.Player.with({ NextTurnRecoveryBonus: 1 }) })), 4);
    assert.equal(TurnLoop.PreviewRecovery(spent.with({ Player: spent.Player.with({ NextTurnRecoveryBonus: -1 }) })), 2);
  });

  test('TheRecovery_MatchesWhatTheNextTurnStartGives', () => {
    const state = Opened(Idle, 3);
    for (const [stamina, statuses, bonus] of [
      [4, StatusSet.Empty, 0],
      [2, SS([StatusKind.Fatigue, 1]), 0],
      [8, StatusSet.Empty, 1],
      [5, SS([StatusKind.Fatigue, 2]), 2],
    ]) {
      const set = state.with({ Player: state.Player.with({ Stamina: stamina, Statuses: statuses, NextTurnRecoveryBonus: bonus }) });
      const preview = TurnLoop.PreviewRecovery(set);
      const ended = TurnLoop.EndTurn(set, NoRng).State;
      assert.equal(TurnLoop.PreviewRecovery(ended), preview);
      const begun = TurnLoop.BeginPlayerTurn(ended, NoRng);
      const recovered = ofType(begun.Events, StaminaRecovered).find((e) => e.Actor === Actor.Player).Amount;
      assert.equal(preview, recovered, `stamina ${stamina}, bonus ${bonus}`);
    }
  });

  test('AFinishedBattle_RecoversNothing', () => {
    const state = Opened(Idle, 3);
    const over = state.with({ Player: state.Player.with({ Stamina: 2 }), Result: GameResult.Won });
    assert.equal(TurnLoop.PreviewRecovery(over), 0);
  });
});

// ======================================================================== StanceStopTests

describe('StanceStopTests', () => {
  const Cells = 8;
  const Older = Card('older_stance', { face: new Face({ Stance: new StanceDef(StanceHook.TurnStart, { Guard: 1 }) }), attributes: A.Stance, targets: TargetKind.Self });
  const Newer = Card('newer_stance', { face: new Face({ Stance: new StanceDef(StanceHook.TurnStart, { Guard: 2 }) }), attributes: A.Stance, targets: TargetKind.Self });
  const Filler = Card('filler', { face: new Face({ Guard: 1 }), attributes: A.Guard, targets: TargetKind.Self });
  const Deck = (...first) => DeckOf(Filler, Constants.DeckMin, ...first);
  const Fired = (events) => ofType(events, StanceFired).filter((f) => f.Actor === Actor.Player && f.Hook === StanceHook.TurnStart).map((f) => f.SourceId);
  function PlaceTwoStances() {
    let s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(Enemies.DistortionRoot, Deck(Older, Newer), Cells, { StartGap: 3 }), NoRng).State, NoRng).State;
    s = TurnLoop.PlayCard(s, InHand(s, 'older_stance'), NoRng).State;
    s = TurnLoop.PlayCard(s, InHand(s, 'newer_stance'), NoRng).State;
    assert.deepEqual(s.Player.StanceList.map((e) => e.Source), ['older_stance', 'newer_stance']);
    return s;
  }

  test('RootSt_TheBreathLanding_StopsTheNewestStance_ForTheNextTurn_ThenItIsBack', () => {
    const s = PlaceTwoStances();
    const turn1 = TurnLoop.EndTurn(s, NoRng);
    assert.equal(turn1.State.Enemies[0].ActiveSwitch, 'root_st');
    assert.equal(turn1.State.Omen.ActionId, 'wither_breath');
    assert.equal(ofType(turn1.Events, StanceStopped).length, 0);
    const turn2Start = TurnLoop.BeginPlayerTurn(turn1.State, NoRng);
    assert.deepEqual(Fired(turn2Start.Events), ['older_stance', 'newer_stance']);
    const turn2 = TurnLoop.EndTurn(turn2Start.State, NoRng);
    const stopped = ofType(turn2.Events, StanceStopped);
    const turn3Start = TurnLoop.BeginPlayerTurn(turn2.State, NoRng);
    const turn3 = TurnLoop.EndTurn(turn3Start.State, NoRng);
    const turn4Start = TurnLoop.BeginPlayerTurn(turn3.State, NoRng);

    eq(stopped, [new StanceStopped(Actor.Player, 'newer_stance', 1, 3).with({ Unit: 0 })]);
    assert.equal(turn2.State.Player.StanceList.length, 2);
    assert.equal(turn2.State.Player.StanceList[1].StoppedThrough, 3);
    assert.equal(turn3Start.State.Turn, 3);
    assert.deepEqual(Fired(turn3Start.Events), ['older_stance']);
    assert.equal(turn3Start.State.Player.Guard, 1);
    assert.equal(ofType(turn3Start.Events, StanceResumed).length, 0);
    assert.equal(ofType(turn3.Events, StanceStopped).length, 0);
    eq(ofType(turn4Start.Events, StanceResumed), [new StanceResumed(Actor.Player, 'newer_stance', 1).with({ Unit: 0 })]);
    assert.deepEqual(Fired(turn4Start.Events), ['older_stance', 'newer_stance']);
    assert.equal(turn4Start.State.Player.Guard, 3);
    assert.ok(turn4Start.State.Player.StanceList.every((e) => e.StoppedThrough === 0));
    const resumedAt = turn4Start.Events.findIndex((e) => e instanceof StanceResumed);
    const firedAt = turn4Start.Events.findIndex((e) => e instanceof StanceFired);
    assert.ok(resumedAt < firedAt);
  });

  test('RootSt_TheBreathLanding_OnAPlayerWithNoStance_StopsNothing', () => {
    const s = PlaceTwoStances();
    const turn1 = TurnLoop.EndTurn(s, NoRng);
    assert.equal(turn1.State.Enemies[0].ActiveSwitch, 'root_st');
    let turn2Start = TurnLoop.BeginPlayerTurn(turn1.State, NoRng).State;
    turn2Start = turn2Start.with({ Player: turn2Start.Player.with({ Stances: null }) });
    const turn2 = TurnLoop.EndTurn(turn2Start, NoRng);
    assert.equal(ofType(turn2.Events, ActionExecuted)[0].Action.Face.StopStance, true);
    assert.equal(ofType(turn2.Events, ActionWhiffed).length, 0);
    assert.equal(ofType(turn2.Events, StanceStopped).length, 0);
    assert.equal(turn2.State.Player.StanceList.length, 0);
  });

  test('RootSt_TheBreathThatWhiffs_StopsNothing', () => {
    const s = PlaceTwoStances();
    const turn1 = TurnLoop.EndTurn(s, NoRng);
    let turn2Start = TurnLoop.BeginPlayerTurn(turn1.State, NoRng).State;
    turn2Start = turn2Start.with({ Player: turn2Start.Player.with({ Cell: turn2Start.Enemy.Cell - 1 }) });
    assert.equal(turn2Start.Gap, 0);
    const turn2 = TurnLoop.EndTurn(turn2Start, NoRng);
    assert.ok(ofType(turn2.Events, ActionWhiffed).map((w) => w.SourceId).includes('wither_breath'));
    assert.equal(ofType(turn2.Events, StanceStopped).length, 0);
  });
});

// ======================================================================== DepartureToolTests

describe('DepartureToolTests', () => {
  const Cells = 8;
  const Filler = Card('filler', { face: new Face({ Guard: 1 }), attributes: A.Guard, targets: TargetKind.Self });
  const Deck = () => DeckOf(Filler, Constants.DeckMin);
  const Always = (action) => Enemy('always', { atZero: action, atOneToTwo: action, atThreePlus: action });
  const Striker = Always(EnemyAction('strike', { face: new Face({ Power: 9, Reach: new Reach(0, 4) }) }));
  const Setup = (enemy, { gap = 3, staminaBonus = 0, firstHitGuard = 0, ward = 0, startStamina = null } = {}) =>
    new BattleSetup(enemy, Deck(), Cells, { StartGap: gap, PlayerStartStamina: startStamina, PlayerStartStaminaBonus: staminaBonus, FirstHitGuard: firstHitGuard, BossWordWard: ward });
  const Opened = (setup) => TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoRng).State, NoRng).State;
  const EndSpent = (s) => TurnLoop.EndTurn(s.with({ Player: s.Player.with({ Stamina: 0 }) }), NoRng);

  test('Flute_OpensThreePastTheMaximum_AndTurnOneRecoveryNeitherAddsNorCuts', () => {
    const plain = TurnLoop.Start(Setup(Striker), NoRng).State;
    const start = TurnLoop.Start(Setup(Striker, { staminaBonus: 3 }), NoRng).State;
    const turn1 = TurnLoop.BeginPlayerTurn(start, NoRng);
    assert.equal(plain.Player.Stamina, 10);
    assert.equal(start.Player.Stamina, 13);
    assert.equal(start.Player.MaxStamina, 10);
    assert.equal(turn1.State.Player.Stamina, 13);
    eq(single(ofType(turn1.Events, StaminaRecovered), (r) => r.Actor === Actor.Player), new StaminaRecovered(Actor.Player, 0, 13, 10));
  });

  test('Flute_TheExtraIsSpent_ThenRecoveryStopsAtTheMaximum', () => {
    const s = Opened(Setup(Striker, { staminaBonus: 3 }));
    const spent = s.with({ Player: s.Player.with({ Stamina: 11 }) });
    const next = TurnLoop.BeginPlayerTurn(TurnLoop.EndTurn(spent, NoRng).State, NoRng).State;
    const low = s.with({ Player: s.Player.with({ Stamina: 8 }) });
    const after = TurnLoop.BeginPlayerTurn(TurnLoop.EndTurn(low, NoRng).State, NoRng).State;
    assert.equal(next.Player.Stamina, 11);
    assert.equal(after.Player.Stamina, 10);
  });

  test('Flute_AddsToTheCarriedStamina_AndNeverPassesTheCeiling', () => {
    assert.equal(Setup(Striker, { staminaBonus: 3, startStamina: 4 }).StartStamina, 7);
    assert.equal(Setup(Striker, { staminaBonus: 3, startStamina: 12 }).StartStamina, 13);
    assert.equal(Setup(Striker, { staminaBonus: 6 }).StartStamina, Constants.MaxStaminaCeil);
    assert.equal(Setup(Striker).StartStamina, 10);
  });

  test('Flute_ReachesTheBattleThroughTheDeparture', { skip: deckRules.skip }, () => {
    const departure = new Departure(Deck(), ['ikitsugi_no_fue'], [], 0, 'filler', { StartStaminaBonus: 3 });
    const setup = departure.SetupFor(Striker, Cells);
    assert.equal(TurnLoop.Start(setup, NoRng).State.Player.Stamina, 13);
    assert.ok(DeckRules.Validate(departure.with({ StartStaminaBonus: -1 }), ['filler']).Errors.some((e) => e.includes('never negative')));
  });

  test('Scale_GuardsTheFirstBlowOnly', () => {
    const plain = EndSpent(Opened(Setup(Striker)));
    const scaled = EndSpent(Opened(Setup(Striker, { firstHitGuard: 5 })));
    const second = EndSpent(TurnLoop.BeginPlayerTurn(scaled.State, NoRng).State);
    assert.equal(plain.State.Player.Hp, Constants.PlayerMaxHp - 9);
    assert.equal(scaled.State.Player.Hp, Constants.PlayerMaxHp - 4);
    eq(single(ofType(scaled.Events, GuardGained), (g) => g.Actor === Actor.Player), new GuardGained(Actor.Player, 5, 5));
    assert.equal(scaled.State.Tools.ScaleGuard, 0);
    assert.equal(second.State.Player.Hp, Constants.PlayerMaxHp - 4 - 9);
    assert.equal(ofType(second.Events, GuardGained).some((g) => g.Actor === Actor.Player), false);
  });

  test('Scale_WaitsThroughTurnsWithoutABlow', () => {
    const walker = Enemy('walker', {
      atZero: EnemyAction('bite', { face: new Face({ Power: 7 }) }),
      atOneToTwo: EnemyAction('step', { face: new Face({ Move: 1 }), attributes: A.None, targets: TargetKind.Self }),
      atThreePlus: EnemyAction('walk', { face: new Face({ Move: 1 }), attributes: A.None, targets: TargetKind.Self }),
    });
    let s = Opened(Setup(walker, { firstHitGuard: 5 }));
    for (let turn = 0; turn < 3; turn++) s = TurnLoop.BeginPlayerTurn(EndSpent(s).State, NoRng).State;
    assert.equal(s.Gap, 0);
    assert.equal(s.Player.Hp, Constants.PlayerMaxHp);
    assert.equal(s.Tools.ScaleGuard, 5);
    const bitten = EndSpent(s);
    assert.equal(bitten.State.Player.Hp, Constants.PlayerMaxHp - 2);
    assert.equal(bitten.State.Tools.ScaleGuard, 0);
  });

  test('Scale_ShowsInTheOmenPreview_UntilItIsUsed', () => {
    const plain = Opened(Setup(Striker));
    const scaled = Opened(Setup(Striker, { firstHitGuard: 5 }));
    assert.equal(TurnLoop.PreviewOmen(plain.with({ Player: plain.Player.with({ Stamina: 0 }) }), 0).Damage, 9);
    assert.equal(TurnLoop.PreviewOmen(scaled.with({ Player: scaled.Player.with({ Stamina: 0 }) }), 0).Damage, 4);
  });

  const OpenedPriest = (ward) => Opened(Setup(Enemies.MiasmaPriest, { ward }));

  test('Charm_KeepsTheFirstStackOfMiasmaShroudOff_SoTheMaximumHolds', () => {
    const plain = TurnLoop.EndTurn(OpenedPriest(0), NoRng);
    const warded = TurnLoop.EndTurn(OpenedPriest(1), NoRng);
    const rite = single(ofType(warded.Events, StatusApplied), (a) => a.Kind === StatusKind.MiasmaShroud);
    assert.equal(plain.State.Player.Statuses.Stacks(StatusKind.MiasmaShroud), 1);
    assert.equal(plain.State.Player.MaxStamina, 9);
    assert.equal(warded.State.Player.Statuses.Stacks(StatusKind.MiasmaShroud), 0);
    assert.equal(warded.State.Player.MaxStamina, 10);
    assert.equal(ofType(warded.Events, MaxStaminaChanged).length, 0);
    assert.equal(rite.Warded, 1);
    assert.equal(rite.Landed, 0);
    assert.equal(rite.Refused, false);
  });

  test('Charm_ALastingWordWithACapOfTwo_SitsOneLowerAtEveryStep', () => {
    const deep = Always(EnemyAction('deep', {
      face: new Face({ Reach: new Reach(0, 4), Statuses: [new StatusGrant(StatusKind.Depths, 1, false, 2)] }), attributes: A.Skill,
    }));
    const Run = (ward) => {
      let s = Opened(Setup(deep, { ward }));
      const held = [];
      for (let turn = 0; turn < 3; turn++) {
        const end = TurnLoop.EndTurn(s, NoRng).State;
        held.push(end.Player.Statuses.Stacks(StatusKind.Depths));
        s = TurnLoop.BeginPlayerTurn(end, NoRng).State;
      }
      return held;
    };
    assert.deepEqual(Run(0), [1, 2, 2]);
    assert.deepEqual(Run(1), [0, 1, 1]);
  });

  test('Charm_AWordThatWearsOff_ComesOnOneLowerEachTime', () => {
    const binder = Always(EnemyAction('bind', {
      face: new Face({ Reach: new Reach(0, 4), Statuses: [new StatusGrant(StatusKind.Binding, 2)] }), attributes: A.Skill,
    }));
    const plain = TurnLoop.EndTurn(Opened(Setup(binder)), NoRng).State;
    const warded = TurnLoop.EndTurn(Opened(Setup(binder, { ward: 1 })), NoRng);
    assert.equal(plain.Player.Statuses.Stacks(StatusKind.Binding), 2);
    assert.equal(warded.State.Player.Statuses.Stacks(StatusKind.Binding), 1);
    assert.equal(single(ofType(warded.Events, StatusApplied), (a) => a.Kind === StatusKind.Binding).Warded, 1);
  });

  test('Charm_LeavesTheCommonWordsAlone', () => {
    const tiring = Always(EnemyAction('tire', {
      face: new Face({ Reach: new Reach(0, 4), Statuses: [new StatusGrant(StatusKind.Fatigue, 1)] }), attributes: A.Skill,
    }));
    const warded = TurnLoop.EndTurn(Opened(Setup(tiring, { ward: 1 })), NoRng).State;
    assert.equal(warded.Player.Statuses.Stacks(StatusKind.Fatigue), 1);
  });
});

// ======================================================================== BossStatusTests (TurnLoop)

describe('BossStatusTests', () => {
  const Cells = 8;
  const Filler = Card('filler', { face: new Face({ Guard: 1 }), attributes: A.Guard, targets: TargetKind.Self });
  const Deck = (...first) => DeckOf(Filler, Constants.DeckMin, ...first);
  const wait = () => EnemyAction('wait', { face: new Face({ Guard: 1 }), attributes: A.Guard, targets: TargetKind.Self });
  const Idle = Enemy('idle', { atZero: wait(), atOneToTwo: wait(), atThreePlus: wait() });
  const Opened = (enemy, gap, ...first) =>
    TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(enemy, Deck(...first), Cells, { StartGap: gap }), NoRng).State, NoRng).State;
  const WithPlayer = (s, ...words) => s.with({ Player: s.Player.with({ Statuses: SS(...words) }) });

  test('MiasmaShroud_LowersTheMaximumStamina_AndCutsWhatIsHeld_NotTheRecovery', () => {
    const s = Opened(Enemies.MiasmaPriest, 3);
    assert.equal(s.Omen.ActionId, 'miasma_rite');
    const end = TurnLoop.EndTurn(s, NoRng);
    const next = TurnLoop.BeginPlayerTurn(end.State.with({ Player: end.State.Player.with({ Stamina: 0 }) }), NoRng);
    assert.equal(end.State.Player.Statuses.Stacks(StatusKind.MiasmaShroud), 1);
    assert.equal(end.State.Player.Statuses.Has(StatusKind.Fatigue), false);
    eq(single(ofType(end.Events, MaxStaminaChanged)), new MaxStaminaChanged(Actor.Player, -1, 9, 9));
    assert.equal(end.State.Player.MaxStamina, 9);
    assert.equal(end.State.Player.Stamina, 9);
    eq(single(ofType(next.Events, StaminaRecovered), (r) => r.Actor === Actor.Player), new StaminaRecovered(Actor.Player, 3, 3, 9));
  });

  test('MiasmaShroud_StaysAtOneUntilTheSecondStage_ThenReachesTwo', () => {
    let s = Opened(Enemies.MiasmaPriest, 3);
    const first = TurnLoop.EndTurn(s, NoRng).State;
    s = TurnLoop.BeginPlayerTurn(first, NoRng).State;
    s = s.WithEnemy(s.Enemy.with({ Hp: 68 }));
    const second = TurnLoop.EndTurn(s, NoRng);
    s = TurnLoop.BeginPlayerTurn(second.State, NoRng).State;
    const third = TurnLoop.EndTurn(s, NoRng);
    assert.ok(!ofType(second.Events, ActionExecuted).map((e) => e.Action.Id).includes('miasma_rite'));
    assert.equal(ofType(second.Events, StatusApplied).filter((a) => a.Kind === StatusKind.MiasmaShroud).length, 0);
    assert.equal(second.State.Player.Statuses.Stacks(StatusKind.MiasmaShroud), 1);
    assert.equal(second.State.Player.MaxStamina, 9);
    assert.equal(second.State.Enemies[0].ActiveSwitch, 'second_stage');
    assert.equal(ofType(third.Events, ActionExecuted)[0].Action.Id, 'miasma_rite');
    assert.equal(third.State.Player.Statuses.Stacks(StatusKind.MiasmaShroud), 2);
    assert.equal(third.State.Player.MaxStamina, 8);
  });

  test('Binding_StopsTheHoldersOwnMove_AndKeepsTheMovementCardsOutOfPlay', () => {
    const s = WithPlayer(Opened(Idle, 3, CardCatalog.Footwork, CardCatalog.StepInGuard), [StatusKind.Binding, 1]);
    const step = TurnLoop.PlayCard(s, InHand(s, 'step_in_guard'), NoRng);
    assert.equal(TurnLoop.CanPlay(s, InHand(s, 'footwork')), PlayRefusal.Bound);
    assert.equal(TurnLoop.CanPlay(s, InHand(s, 'step_in_guard')), PlayRefusal.None);
    assert.equal(step.State.Player.Cell, s.Player.Cell);
    eq(single(ofType(step.Events, MoveBlocked)), new MoveBlocked(Actor.Player, StatusKind.Binding));
    assert.equal(ofType(step.Events, CellsMoved).length, 0);
    assert.equal(step.State.Player.Guard, 12);
  });

  test('Binding_FromTheBindingWord_LastsTheNextPlayerTurnOnly', () => {
    let s = Opened(Enemies.MiasmaPriest, 2, CardCatalog.Footwork);
    s = s.with({ Player: s.Player.with({ Cell: s.Enemy.Cell - 3 }) });
    const bound = TurnLoop.EndTurn(s, NoRng);
    const next = TurnLoop.BeginPlayerTurn(bound.State, NoRng).State;
    const after = TurnLoop.BeginPlayerTurn(TurnLoop.EndTurn(next, NoRng).State, NoRng).State;
    assert.equal(single(ofType(bound.Events, StatusApplied), (a) => a.Kind === StatusKind.Binding).StacksAfter, 2);
    assert.equal(bound.State.Player.Statuses.Has(StatusKind.Slow), false);
    assert.equal(next.Player.Statuses.Stacks(StatusKind.Binding), 1);
    assert.equal(after.Player.Statuses.Has(StatusKind.Binding), false);
  });

  test('Binding_IsNotSlow_APushFromTheOtherSideStillMovesTheHolder', () => {
    let s = Opened(Enemies.PolearmWarped, 0);
    assert.equal(s.Omen.ActionId, 'shove');
    s = WithPlayer(s, [StatusKind.Binding, 2]);
    const end = TurnLoop.EndTurn(s, NoRng);
    assert.ok(ofType(end.Events, CellsMoved).some((m) => m.Actor === Actor.Player && m.Pushed));
  });

  test('Hook_CatchesAMoveBack_SpendingAStack_AndLetsAMoveForwardThrough', () => {
    let s = WithPlayer(Opened(Idle, 3, CardCatalog.BackLeap, CardCatalog.BackLeap, CardCatalog.Footwork), [StatusKind.Hook, 1]);
    s = s.with({ Player: s.Player.with({ Cell: 3 }) });
    const forward = TurnLoop.PlayCard(s, InHand(s, 'footwork'), NoRng);
    const caught = TurnLoop.PlayCard(forward.State, InHand(forward.State, 'back_leap'), NoRng);
    const free = TurnLoop.PlayCard(caught.State, InHand(caught.State, 'back_leap'), NoRng);
    assert.equal(forward.State.Player.Cell, 4);
    assert.equal(forward.State.Player.Statuses.Stacks(StatusKind.Hook), 1);
    assert.equal(caught.State.Player.Cell, 4);
    eq(single(ofType(caught.Events, MoveBlocked)), new MoveBlocked(Actor.Player, StatusKind.Hook));
    eq(single(ofType(caught.Events, StatusConsumed)), new StatusConsumed(Actor.Player, StatusKind.Hook, 0));
    assert.deepEqual([...caught.State.History.HookSnags], [1]);
    assert.ok(caught.State.Player.Guard > 0);
    assert.equal(free.State.Player.Cell, 2);
  });

  test('Hook_DoesNotSpendAStack_OnAMoveBackTheEdgeStopsAnyway', () => {
    let s = WithPlayer(Opened(Idle, 3, CardCatalog.BackLeap), [StatusKind.Hook, 1]);
    s = s.with({ Player: s.Player.with({ Cell: 1 }) });
    const played = TurnLoop.PlayCard(s, InHand(s, 'back_leap'), NoRng);
    assert.equal(played.State.Player.Cell, 1);
    assert.equal(played.State.Player.Statuses.Stacks(StatusKind.Hook), 1);
    assert.equal(ofType(played.Events, MoveBlocked).length, 0);
    assert.equal(played.State.History.HookSnags.length, 0);
  });

  test('Hook_HasNoStackLimit', () => {
    let s = Opened(Enemies.AbyssAngler, 3);
    assert.equal(s.Omen.ActionId, 'hook_cast');
    s = WithPlayer(s, [StatusKind.Hook, 2]);
    const end = TurnLoop.EndTurn(s, NoRng);
    assert.equal(single(ofType(end.Events, StatusApplied), (a) => a.Kind === StatusKind.Hook).StacksAfter, 3);
  });

  for (const [gap, stacks, recovered] of [[0, 2, 1], [1, 2, 3], [0, 1, 2]]) {
    test(`Depths_ThinsTheRecovery_OnlyWhenTheTurnStartsAdjacent(${gap},${stacks})`, () => {
      let s = TurnLoop.Start(new BattleSetup(Idle, Deck(), Cells, { StartGap: gap }), NoRng).State;
      s = s.with({ Player: s.Player.with({ Stamina: 0, Statuses: SS([StatusKind.Depths, stacks]) }) });
      const begin = TurnLoop.BeginPlayerTurn(s, NoRng);
      assert.equal(single(ofType(begin.Events, StaminaRecovered)).Amount, recovered);
      assert.equal(begin.State.Player.Statuses.Stacks(StatusKind.Depths), stacks);
    });
  }

  for (const [stacks, fatigue, recovered] of [[1, 0, 2], [2, 0, 1], [1, 1, 1], [2, 1, 1]]) {
    test(`Withering_ThinsEveryRecovery_DownToOne(${stacks},${fatigue})`, () => {
      let s = TurnLoop.Start(new BattleSetup(Idle, Deck(), Cells, { StartGap: 3 }), NoRng).State;
      const words = fatigue > 0 ? SS([StatusKind.Withering, stacks], [StatusKind.Fatigue, fatigue]) : SS([StatusKind.Withering, stacks]);
      s = s.with({ Player: s.Player.with({ Stamina: 0, Statuses: words }) });
      const begin = TurnLoop.BeginPlayerTurn(s, NoRng);
      assert.equal(single(ofType(begin.Events, StaminaRecovered)).Amount, recovered);
      assert.equal(begin.State.Player.Statuses.Stacks(StatusKind.Withering), stacks);
    });
  }

  for (const [withering, fatigue, depths, recovered] of [[2, 0, 1, 0], [1, 0, 1, 1], [2, 1, 1, 0], [2, 0, 2, 0]]) {
    test(`Withering_Floor_DoesNotShieldDepths(${withering},${fatigue},${depths})`, () => {
      let s = TurnLoop.Start(new BattleSetup(Idle, Deck(), Cells, { StartGap: 0 }), NoRng).State;
      const words = [[StatusKind.Withering, withering], [StatusKind.Depths, depths]];
      if (fatigue > 0) words.push([StatusKind.Fatigue, fatigue]);
      s = s.with({ Player: s.Player.with({ Stamina: 0, Statuses: SS(...words) }) });
      const begin = TurnLoop.BeginPlayerTurn(s, NoRng);
      assert.equal(single(ofType(begin.Events, StaminaRecovered)).Amount, recovered);
    });
  }

  for (const [stacks, move, hpChange] of [[1, false, -4], [2, false, -8], [1, true, 0]]) {
    test(`Rooting_CostsHp_AfterTwoTurnsEndedOnTheSameCell(${stacks},${move})`, () => {
      let s = WithPlayer(Opened(Idle, 3, Filler, Filler, Filler, Filler, Filler, Filler, CardCatalog.Footwork, CardCatalog.BreakOff), [StatusKind.Rooting, stacks]);
      s = TurnLoop.EndTurn(s, NoRng).State;
      s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
      if (move) {
        s = TurnLoop.PlayCard(s, InHand(s, 'footwork'), NoRng).State;
        s = TurnLoop.PlayCard(s, InHand(s, 'break_off'), NoRng).State;
      }
      s = TurnLoop.EndTurn(s, NoRng).State;
      const hpBefore = s.Player.Hp;
      const third = TurnLoop.BeginPlayerTurn(s, NoRng);
      assert.equal(third.State.Player.Hp - hpBefore, hpChange);
      assert.equal(ofType(third.Events, StatusHpChanged).some((e) => e.Kind === StatusKind.Rooting), hpChange !== 0);
      assert.equal(third.State.Player.Statuses.Has(StatusKind.Bleed), false);
    });
  }

  for (const [gap, gapAfter] of [[1, 2], [2, 1], [3, 2]]) {
    test(`TheCreepingRoot_BringsThePlayerTowardOneToTwo_InPlay(${gap})`, () => {
      const creeping = Enemies.DistortionRoot.Actions.get('creeping_root');
      const only = [creeping.Id];
      const root = new EnemyDef('creeper', 'creeper', 200, 14, 3, 2, only, only, only, new Map([[creeping.Id, creeping]]), EnemyRank.Boss, 1);
      const s = Opened(root, gap);
      const end = TurnLoop.EndTurn(s, NoRng);
      assert.equal(end.State.Gap, gapAfter);
      assert.equal(single(ofType(end.Events, CellsMoved), (m) => m.Actor === Actor.Player).Pushed, true);
    });
  }
});

// ======================================================================== BossSwitchTests (TurnLoop)

describe('BossSwitchTests', () => {
  const Cells = 8;
  const Filler = Card('filler', { face: new Face({ Guard: 1 }), attributes: A.Guard, targets: TargetKind.Self });
  const Deck = (...first) => {
    const deck = [];
    for (let i = 0; i < first.length; i++) deck.push(new CardInstance(first[i].Id + '-' + i, first[i]));
    for (let i = deck.length; i < Constants.DeckMin; i++) deck.push(new CardInstance('filler-' + i, Filler));
    return deck;
  };
  const None = [];
  const Turn = (cards, { guard = 0, gap = 3, moved = false, cell = 2, lastMove = 0 } = {}) => ({ cards, guard, gap, moved, cell, lastMove });
  function History(...turns) {
    let history = new BattleHistory();
    for (const t of turns) {
      for (const card of t.cards) history = history.WithCard(card);
      history = history.WithTurnEnd(new PlayerTurnEnd(t.guard, t.gap, t.moved, t.cell, t.cards.length, t.lastMove));
    }
    return history;
  }
  const Opened = (boss, gap, ...first) =>
    TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(boss, Deck(...first), Cells, { StartGap: gap }), NoRng).State, NoRng).State;

  test('Priest_TheBreakingStaff_BreaksTwoInPlay', () => {
    const priest = Enemies.MiasmaPriest;
    let s = Opened(priest, 1);
    s = s.with({ PlayerHistory: History(Turn(None, { guard: 10, gap: 1 })), Player: s.Player.with({ Guard: 10, Stamina: 0 }) });
    const decided = TurnLoop.EndTurn(s, NoRng);
    assert.equal(decided.State.Enemies[0].ActiveSwitch, 'breaker');
    assert.equal(decided.State.Omen.ActionId, 'staff_strike');
    s = TurnLoop.BeginPlayerTurn(decided.State, NoRng).State;
    s = s.with({ Player: s.Player.with({ Guard: 10, Cell: s.Enemy.Cell - 2 }) });
    const struck = TurnLoop.EndTurn(s, NoRng);
    assert.equal(ofType(struck.Events, StaminaBroken).find((b) => b.Target === Actor.Player).Amount, 2);
  });

  test('Angler_SecondStage_StepsOutTwo_DropsTheSlackLine_AndHooksTwo', () => {
    const angler = Enemies.AbyssAngler;
    const stage = angler.Switches.find((w) => w.Id === 'second_stage');
    let s = Opened(angler, 4);
    s = s.WithEnemy(s.Enemy.with({ Hp: 80 }));
    const cellBefore = s.Enemy.Cell;
    const end = TurnLoop.EndTurn(s, NoRng);
    const stepped = last(ofType(end.Events, CellsMoved).filter((m) => m.Actor === Actor.Enemy));
    assert.equal(stage.EnterMove, 2);
    assert.equal(single(ofType(end.Events, TreeSwitched)).To, 'second_stage');
    assert.ok(stepped.To < stepped.From);
    assert.ok(end.State.Enemy.Cell < cellBefore);
    assert.equal(single(stage.Override('hook_cast').Face.StatusList).Stacks, 2);
    const again = TurnLoop.EndTurn(TurnLoop.BeginPlayerTurn(end.State, NoRng).State, NoRng);
    assert.equal(ofType(again.Events, TreeSwitched).length, 0);
  });

  test('Root_StageThree_SwaysForwardAndBack_PhaseByPhase', () => {
    const root = Enemies.DistortionRoot;
    let s = Opened(root, 3);
    s = s.WithEnemy(s.Enemy.with({ Hp: 60 }));
    s = TurnLoop.EndTurn(s, NoRng).State;
    const moves = [];
    for (let turn = 0; turn < 3; turn++) {
      s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
      s = s.with({ Player: s.Player.with({ Cell: 1 }) });
      const end = TurnLoop.EndTurn(s, NoRng);
      const sway = ofType(end.Events, CellsMoved).find((m) => m.Actor === Actor.Enemy && !m.Pushed);
      moves.push(sway == null ? 0 : sway.From - sway.To);
      s = end.State;
      if (s.Result !== GameResult.Ongoing) break;
    }
    const two = moves.slice(0, 2);
    assert.ok((two[0] === -1 && two[1] === 1) || (two[0] === 1 && two[1] === -1), `moves ${moves}`);
  });

  test('Root_ShakesOffItsWords_WhenTwoStatusesHaveLanded', () => {
    const root = Enemies.DistortionRoot;
    let s = Opened(root, 3);
    s = s.WithEnemy(s.Enemy.with({ Statuses: SS([StatusKind.Bleed, 2], [StatusKind.Fragile, 1]) }));
    s = s.with({ PlayerHistory: new BattleHistory().WithInfliction(s.Turn).WithInfliction(s.Turn) });
    const end = TurnLoop.EndTurn(s, NoRng);
    assert.equal(end.State.Enemies[0].ActiveSwitch, 'root_sk');
    eq(ofType(end.Events, StatusCleared), [
      new StatusCleared(Actor.Enemy, StatusKind.Bleed, 1).with({ Unit: 0 }),
      new StatusCleared(Actor.Enemy, StatusKind.Fragile, 1).with({ Unit: 0 }),
    ]);
    assert.equal(end.State.Enemy.Statuses.KindCount, 0);
    assert.equal(end.State.Omen.ActionId, 'bark');
  });

  test('Root_ShakesOffItsWords_AgainWhenTriggeredTwoTurnsInARow', () => {
    const root = Enemies.DistortionRoot;
    let s = Opened(root, 3);
    s = s.WithEnemy(s.Enemy.with({ Statuses: SS([StatusKind.Bleed, 2], [StatusKind.Fragile, 1]) }));
    s = s.with({ PlayerHistory: s.History.WithInfliction(s.Turn).WithInfliction(s.Turn) });
    const first = TurnLoop.EndTurn(s, NoRng);
    assert.equal(first.State.Enemies[0].ActiveSwitch, 'root_sk');
    s = TurnLoop.BeginPlayerTurn(first.State, NoRng).State;
    s = s.WithEnemy(s.Enemy.with({ Statuses: SS([StatusKind.Bleed, 3], [StatusKind.Fragile, 1]) }));
    s = s.with({ PlayerHistory: s.History.WithInfliction(s.Turn).WithInfliction(s.Turn) });
    const second = TurnLoop.EndTurn(s, NoRng);
    assert.equal(second.State.Enemies[0].ActiveSwitch, 'root_sk');
    assert.equal(ofType(second.Events, TreeSwitched).length, 0);
    assert.deepEqual(ofType(second.Events, StatusCleared).map((c) => c.Kind).sort(), [StatusKind.Bleed, StatusKind.Fragile].sort());
    assert.equal(second.State.Enemy.Statuses.KindCount, 0);
  });

  test('Root_Moved_ReadsTheWayThePlayerLastStepped_NotTheChangeOfCell', () => {
    const forward = Card('forward', { face: new Face({ Move: 1 }), attributes: A.None, targets: TargetKind.Self });
    const root = Enemies.DistortionRoot;
    let s = Opened(root, 3, forward);
    const lastEnd = s.Player.Cell + 1;
    s = s.with({ PlayerHistory: History(Turn(None, { moved: true, cell: lastEnd, lastMove: 1 })) });
    s = TurnLoop.PlayCard(s, InHand(s, 'forward'), NoRng).State;
    const end = TurnLoop.EndTurn(s, NoRng);
    const turn = last(end.State.History.TurnEnds);
    assert.equal(turn.Cell, lastEnd);
    assert.equal(turn.LastMove, 1);
    assert.equal(end.State.Enemies[0].ActiveSwitch, 'root_m_in');
  });

  test('TheHistory_KeepsTheWayOfTheLastOwnMove_EvenWhenTheCellEndsWhereItBegan', () => {
    const forward = Card('forward', { face: new Face({ Move: 1 }), attributes: A.None, targets: TargetKind.Self });
    const back = Card('back', { face: new Face({ Move: -1 }), attributes: A.None, targets: TargetKind.Self });
    let s = Opened(Enemies.DistortionRoot, 3, forward, back);
    s = s.with({ Player: s.Player.with({ Cell: s.Player.Cell + 1 }) });
    const began = s.Player.Cell;
    s = TurnLoop.PlayCard(s, InHand(s, 'back'), NoRng).State;
    s = TurnLoop.PlayCard(s, InHand(s, 'forward'), NoRng).State;
    const end = TurnLoop.EndTurn(s, NoRng);
    const turn = last(end.State.History.TurnEnds);
    assert.equal(turn.Cell, began);
    assert.equal(turn.Moved, true);
    assert.equal(turn.LastMove, 1);
  });

  test('TheHistory_CountsTheWordsThePlayerPuts_OnAnEnemy', () => {
    const s = Opened(Enemies.DistortionRoot, 0, CardCatalog.Rend);
    const played = TurnLoop.PlayCard(s, InHand(s, 'rend'), NoRng);
    assert.deepEqual([...played.State.History.Inflictions], [1]);
  });
});

// ======================================================================== PriestAwayTests (TurnLoop)

describe('PriestAwayTests', () => {
  const Cells = 8;
  const Filler = Card('filler', { face: new Face({ Guard: 1 }), attributes: A.Guard, targets: TargetKind.Self });
  const Deck = () => DeckOf(Filler, Constants.DeckMin);
  const Priest = () => Enemies.MiasmaPriest;
  function Phases(setup, count) {
    let state = TurnLoop.Start(setup, NoRng).State;
    const phases = [];
    for (let i = 0; i < count; i++) {
      state = TurnLoop.BeginPlayerTurn(state, NoRng).State;
      const end = TurnLoop.EndTurn(state.with({ Player: state.Player.with({ Stamina: 0 }) }), NoRng);
      phases.push(end);
      state = end.State;
    }
    return phases;
  }
  const Taken = (phase) => ofType(phase.Events, ActionExecuted).map((e) => e.Action.Id);
  const BoltRaw = (phase) => single(ofType(phase.Events, DamageDealt), (d) => d.Actor === Actor.Enemy && d.Target === Actor.Player).Raw;

  test('AtGapThree_TheRiteOpens_ThenTheBoltComesEveryPhase', () => {
    const phases = Phases(new BattleSetup(Priest(), Deck(), Cells, { StartGap: 3 }), 3);
    assert.deepEqual(Taken(phases[0]), ['miasma_rite', 'ward']);
    assert.deepEqual(Taken(phases[1]), ['miasma_bolt', 'coil']);
    assert.deepEqual(Taken(phases[2]), ['miasma_bolt', 'coil']);
    assert.equal(BoltRaw(phases[1]), 15);
    assert.equal(phases[2].State.Player.Statuses.Stacks(StatusKind.MiasmaShroud), 1);
  });

  test('WithTheCharm_TheRiteIsTakenOnce_AsWithoutIt', () => {
    const phases = Phases(new BattleSetup(Priest(), Deck(), Cells, { StartGap: 3, BossWordWard: 1 }), 2);
    assert.deepEqual(Taken(phases[0]), ['miasma_rite', 'ward']);
    assert.equal(phases[0].State.Player.Statuses.Stacks(StatusKind.MiasmaShroud), 0);
    assert.deepEqual(Taken(phases[1]), ['miasma_bolt', 'coil']);
  });

  test('TheBolt_IsFifteenFromGapThree_AndEightNearer', () => {
    const atTwo = Phases(new BattleSetup(Priest(), Deck(), Cells, { StartGap: 2 }), 1)[0];
    assert.equal(Taken(atTwo)[0], 'miasma_bolt');
    assert.equal(BoltRaw(atTwo), 8);
  });

  test('Breaker_AtGapThree_TheOmenIsTheBolt_NotAStaffOutOfReach', () => {
    let s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(Priest(), Deck(), Cells, { StartGap: 3 }), NoRng).State, NoRng).State;
    const history = new BattleHistory().WithTurnEnd(new PlayerTurnEnd(10, 3, false, s.Player.Cell));
    s = s.with({ PlayerHistory: history, Player: s.Player.with({ Guard: 10, Stamina: 0, Statuses: SS([StatusKind.MiasmaShroud, 1]) }) });
    const decided = TurnLoop.EndTurn(s, NoRng);
    assert.equal(decided.State.Enemies[0].ActiveSwitch, 'breaker');
    assert.equal(decided.State.Omen.ActionId, 'miasma_bolt');
  });
});

// ======================================================================== Port-specific checks (no C# counterpart)

describe('TurnLoop port surface', () => {
  test('the public members are the C# public static methods', () => {
    assert.deepEqual(Object.keys(TurnLoop).sort(), [
      'BeginPlayerTurn', 'CanPlay', 'CanTakeFreeStep', 'CostNow', 'EndTurn', 'OmenBoundaryCells', 'OmenMoveCell',
      'PlayCard', 'Preview', 'PreviewOmen', 'PreviewRecovery', 'Start', 'TakeFreeStep',
    ]);
    assert.deepEqual(Object.keys(PlayRefusal), ['None', 'BattleOver', 'NotPlayerTurn', 'NotInHand', 'NotEnoughStamina', 'OutOfReach', 'NoSuchTarget', 'Bound']);
  });

  test('null arguments throw ArgumentNullException with the .NET message', () => {
    assert.throws(() => TurnLoop.Start(null, NoRng), { name: 'ArgumentNullException', message: "Value cannot be null. (Parameter 'setup')" });
    assert.throws(() => TurnLoop.Start(BattleSetup.Slice(), null), { name: 'ArgumentNullException', message: "Value cannot be null. (Parameter 'rng')" });
    assert.throws(() => TurnLoop.CanPlay(null, 'x'), { name: 'ArgumentNullException' });
    assert.throws(() => TurnLoop.PlayCard(null, 'x', null), { message: "Value cannot be null. (Parameter 'rng')" });
  });

  test('CostNow: the column cost after a CostDown trait and a CostDiscount stance (§4)', () => {
    const Wait = EnemyAction('wait', { face: new Face(), attributes: A.Guard, targets: TargetKind.Self });
    const Idle = Enemy('idle', { atZero: Wait, atOneToTwo: Wait, atThreePlus: Wait });
    const wall = Card('iron_wall_stance', {
      face: new Face({ Stance: new StanceDef(StanceHook.CostDiscount, { Attribute: A.Guard }) }), attributes: A.Stance, targets: TargetKind.Self,
    });
    const guard3 = Card('guard3', { column: 3, face: new Face({ Guard: 15 }), attributes: A.Guard, targets: TargetKind.Self });
    const cheap = Card('cheap', { column: 2, face: new Face({ Guard: 9 }), attributes: A.Guard, targets: TargetKind.Self,
      trait: new Trait(TraitCondition.FirstPlay, TraitEffect.CostDown, 1) });
    const Filler = Card('filler', { face: new Face({ Guard: 1 }), attributes: A.Guard, targets: TargetKind.Self });
    let s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(Idle, DeckOf(Filler, 6, wall, guard3, cheap, guard3), 8), NoRng).State, NoRng).State;
    const cheapId = InHand(s, 'cheap');
    assert.equal(TurnLoop.CostNow(s, s.Hand.find((c) => c.InstanceId === cheapId).Def), 1, 'first play: 2 − 1');
    s = TurnLoop.PlayCard(s, InHand(s, 'iron_wall_stance'), NoRng).State;
    const g3 = s.Hand.find((c) => c.Def.Id === 'guard3');
    assert.equal(TurnLoop.CostNow(s, g3.Def), 2, 'the stance takes 1 off a guard card');
    assert.equal(TurnLoop.Preview(s, g3.InstanceId).Cost, 2);
    const stamina = s.Player.Stamina;
    const played = TurnLoop.PlayCard(s, g3.InstanceId, NoRng);
    assert.equal(played.State.Player.Stamina, stamina - 2);
    assert.ok(ofType(played.Events, StanceFired).some((f) => f.Hook === StanceHook.CostDiscount));
    const other = played.State.Hand.find((c) => c.Def.Id === 'guard3');
    assert.equal(TurnLoop.CostNow(played.State, other.Def), 3, 'one discount a turn per entry');
  });
});
