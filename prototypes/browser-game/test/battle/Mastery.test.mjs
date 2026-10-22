import { fileURLToPath } from 'node:url';
// Tests for src/core/battle/Mastery.js — ported from unity-port/BattleCore.Tests:
// - MasteryTests (the four contexts, 才能, the thresholds judged only at the battle's end, the three
//   kinds of step, step 3 for one card of the life, 習熟訓練),
// - MasteryProfilesTests (the sixty profiles, against swordsman_cards_v4.md §4.6 when the canon is
//   readable beside the repository, and 伸び on the movement-centred cards),
// plus the JS-only points of the port (overload dispatch, Maps in ordinal order, exception messages).

import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';

import {
  csEquals, toParityJson, ArgumentNullException, ArgumentOutOfRangeException, InvalidOperationException,
} from '../../src/shared/cs.js';
import {
  BattleAttribute, TargetKind, OmenKind, StatusKind, TraitCondition, TraitEffect, GameResult,
  Face, Reach, Trait, StatusGrant, CardDef, CardInstance, EnemyActionDef, EnemyDef, BattleHistory,
} from '../../src/core/battle/Types.js';
import { Constants } from '../../src/core/battle/Constants.js';
import { Columns } from '../../src/core/battle/Columns.js';
import { AttributeRule } from '../../src/core/battle/Attributes.js';
import { Traits, TraitContext } from '../../src/core/battle/Traits.js';
import { CardCatalog } from '../../src/core/battle/CardCatalog.js';
import { FixedRng } from '../../src/core/battle/IRng.js';
import { TurnLoop, BattleSetup, FocusStep } from '../../src/core/battle/TurnLoop.js';
import { StaminaSpent } from '../../src/core/battle/BattleEvents.js';
import {
  MasteryContext, MasteryKind, MasteryPlay, MasteryProfile, MasteryProfiles, Mastery, MasteryTally, CardMastery,
  RankUp, MasteryResult, MasteryBook,
} from '../../src/core/battle/Mastery.js';

// ------------------------------------------------------------------ helpers

const A = BattleAttribute;
const Cells = 8;
const NoRng = new FixedRng(0.9999999);

/** C# record equality (Is.EqualTo on records); shows a JSON diff on failure. */
function eq(actual, expected, msg) {
  if (csEquals(actual, expected)) return;
  assert.deepStrictEqual(toParityJson(actual), toParityJson(expected), msg);
  assert.fail(`${msg ?? 'not equal'} (same JSON, different record types)`);
}

/** Fixtures.Card(id, column = 1, face = null, attributes = Attack, trait = null, targets = One). */
function Card(id, column = 1, face = null, attributes = A.Attack, { trait = null, targets = TargetKind.One } = {}) {
  return new CardDef(id, id, attributes, column, face ?? new Face(), trait, targets, '');
}

/** Fixtures.EnemyAction / Fixtures.Enemy: the three-branch dummy enemy. */
function EnemyAction(id, face, attributes = A.Attack, targets = TargetKind.One) {
  return new EnemyActionDef(id, id, attributes, 1, face, null, targets, '');
}
function Enemy() {
  const atZero = EnemyAction('near_hit', new Face({ Power: 5 }));
  const atOneToTwo = EnemyAction('mid_hit', new Face({ Power: 4, Reach: new Reach(1, 2) }));
  const atThreePlus = EnemyAction('advance', new Face({ Move: 1 }), A.None, TargetKind.Self);
  const actions = new Map([[atZero.Id, atZero], [atOneToTwo.Id, atOneToTwo], [atThreePlus.Id, atThreePlus]]);
  return new EnemyDef('dummy', 'dummy', 60, 10, 2, 1, [atZero.Id], [atOneToTwo.Id], [atThreePlus.Id], actions);
}

/** An attack reaching 0〜2 whose trait wants N 2 or more: every context can be met by one play. */
const Lance = Card('lance', 1, new Face({ Power: 6, Reach: new Reach(0, 2) }), A.Attack,
  { trait: new Trait(TraitCondition.GapAtLeast, TraitEffect.PowerBonus, 2, { Threshold: 2 }) });

const Block = Card('block', 1, new Face({ Guard: 4 }), A.Guard, { targets: TargetKind.Self });

const Board = ({ gap = 0, omen = null, staminaBefore = 4 } = {}) =>
  new TraitContext({ Gap: gap, StaminaBefore: staminaBefore, OpponentOmen: omen });

const Contexts = (def, board, paid = 1) =>
  Mastery.ContextsOf(def, board, Traits.EvaluateAll(def.AllTraits, board).Triggered, paid);

const Profile = (id, kind) => new MasteryProfile(id, kind);

function Tally(...entries) {
  const ticks = new Map();
  for (const [id, n] of entries) ticks.set(id, n);
  return new MasteryTally(ticks);
}

function Deck(...cards) {
  const filler = Card('filler', 1, new Face({ Guard: 1 }), A.Guard, { targets: TargetKind.Self });
  const deck = [];
  for (let i = 0; i < cards.length; i++) deck.push(new CardInstance(cards[i].Id + '-' + i, cards[i]));
  for (let i = deck.length; i < Constants.HandDraw; i++) deck.push(new CardInstance(filler.Id + '-' + i, filler));
  return deck;
}

function InHand(s, cardId) {
  const card = s.Hand.find((c) => c.Def.Id === cardId);
  assert.ok(card, `${cardId} is not in the hand`);
  return card.InstanceId;
}

/** NUnit Is.EquivalentTo for records: the same multiset, compared by value. */
function equivalent(actual, expected, msg) {
  const key = (x) => JSON.stringify(toParityJson(x));
  assert.deepEqual(actual.map(key).sort(), expected.map(key).sort(), msg);
}

// ======================================================================== MasteryTests

describe('MasteryTests: the four contexts, 0〜4 ticks a play', () => {
  test('APlay_MeetingNoContext_GivesNoTick', () => {
    // N 0: the trait (N 2 以上) fails and 0 is not the far end of 0〜2. An attack omen wants
    // 防御. 4 before and 2 paid leaves 2: not 構え圏, and 4 before is not 死力圏.
    const met = Contexts(Lance, Board({ gap: 0, omen: OmenKind.Attack, staminaBefore: 4 }), 2);
    assert.equal(met, MasteryContext.None);
    assert.equal(new MasteryPlay(Lance.Id, met).Ticks, 0);
  });

  test('APlay_MeetingAllFour_GivesFourTicks', () => {
    const met = Contexts(Lance, Board({ gap: 2, omen: OmenKind.Move, staminaBefore: 10 }));
    assert.equal(met, MasteryContext.Gap | MasteryContext.Omen | MasteryContext.Trait | MasteryContext.Zone);
    assert.equal(met, 15, '[Flags]: an integer');
    assert.equal(new MasteryPlay(Lance.Id, met).Ticks, 4);
  });

  test('CountOf counts each of the four bits once', () => {
    assert.deepEqual([0, 1, 2, 4, 8, 3, 5, 6, 7, 9, 15].map((c) => Mastery.CountOf(c)), [0, 1, 1, 1, 1, 2, 2, 2, 3, 2, 4]);
    assert.equal(Mastery.CountOf(16), 0, 'a bit outside the four is not a context');
  });

  test('GapContext_IsTheGapTraitsSide_OrTheFarEndOfTheReach', () => {
    const plain = Card('plain', 1, new Face({ Power: 6, Reach: new Reach(0, 2) }));
    const narrow = Card('narrow', 1, new Face({ Power: 8, Reach: Reach.Only(0) }));

    // The trait's side: N 2 以上.
    assert.equal(Mastery.GapHolds(Lance, 2), true);
    assert.equal(Mastery.GapHolds(Lance, 1), false);

    // No gap trait: the far end of a reach spanning 2 N or more.
    assert.equal(Mastery.GapHolds(plain, 2), true);
    assert.equal(Mastery.GapHolds(plain, 1), false);
    assert.equal(Mastery.GapHolds(narrow, 0), false, 'a reach of one N has no edge to play at');
    assert.equal(Mastery.GapHolds(Block, 3), false, 'a self card waits for the card table to give it a range');

    // A profile's own range wins.
    assert.equal(Mastery.GapHolds(Block, 3, new MasteryProfile('block', MasteryKind.Lighten, { GapContext: new Reach(2, 9) })), true);

    assert.equal(Contexts(Lance, Board({ gap: 2, omen: OmenKind.Attack }), 2) & MasteryContext.Gap, MasteryContext.Gap);
  });

  test('OmenContext_IsTheAttributeThatAnswersTheOmen', () => {
    const skill = Card('hex', 1, new Face({ Statuses: [new StatusGrant(StatusKind.Fragile, 2)] }), A.Skill);

    assert.equal(Mastery.Answer(OmenKind.Attack), A.Guard);
    assert.equal(Mastery.Answer(OmenKind.Guard), A.Skill);
    for (const other of [OmenKind.Move, OmenKind.Skill, OmenKind.Stance, OmenKind.Rest]) {
      assert.equal(Mastery.Answer(other), A.Attack, other);
    }

    assert.equal(Contexts(Block, Board({ omen: OmenKind.Attack }), 2), MasteryContext.Omen);
    assert.equal(Contexts(Block, Board({ omen: OmenKind.Move }), 2), MasteryContext.None);
    assert.equal(Contexts(skill, Board({ omen: OmenKind.Guard }), 2), MasteryContext.Omen);
    assert.equal(Contexts(Block, Board({ omen: null }), 2), MasteryContext.None, 'no omen, nothing to answer');
  });

  test('TraitContext_IsAnyTraitHolding', () => {
    const first = Card('first', 1, new Face({ Guard: 4 }), A.Guard,
      { trait: new Trait(TraitCondition.FirstPlay, TraitEffect.GuardBonus, 2), targets: TargetKind.Self });
    const played = new TraitContext({ StaminaBefore: 4, Played: [A.Attack] });

    assert.equal(Contexts(first, Board(), 2), MasteryContext.Trait, '初手 holds');
    assert.equal(Contexts(first, played, 2), MasteryContext.None, '初手 does not');
  });

  test('ZoneContext_IsReserveLeftOrDesperatePaid', () => {
    assert.equal(Contexts(Block, Board({ staminaBefore: 4 }), 1), MasteryContext.Zone, '3 left: 構え圏');
    assert.equal(Contexts(Block, Board({ staminaBefore: 4 }), 2), MasteryContext.None, '2 left and 4 before: neither');
    assert.equal(Contexts(Block, Board({ staminaBefore: 2 }), 1), MasteryContext.Zone, '2 before: 死力圏');
    assert.equal(Contexts(Block, Board({ staminaBefore: 3 }), 1), MasteryContext.None, '3 before, 2 left');
  });

  test('ContextsOf / GapHolds refuse null as the C# does', () => {
    assert.throws(() => Mastery.ContextsOf(null, Board(), false, 1), (e) => e instanceof ArgumentNullException && e.message === "Value cannot be null. (Parameter 'def')");
    assert.throws(() => Mastery.ContextsOf(Lance, null, false, 1), (e) => e instanceof ArgumentNullException && e.message === "Value cannot be null. (Parameter 'context')");
    assert.throws(() => Mastery.GapHolds(null, 0), (e) => e instanceof ArgumentNullException);
    assert.throws(() => Mastery.RankFor(3, null), (e) => e instanceof ArgumentNullException && e.message === "Value cannot be null. (Parameter 'thresholds')");
  });

  test('PlayCard_WritesTheContextsDown_AndTheTallyCountsThem', () => {
    // Turn 1 on an 8-cell line, start gap 2: the lance plays at N 2 with 10 stamina, the
    // enemy's omen at 1〜2 is an attack (mid_hit). Gap + trait + zone = 3; the block answers
    // the attack omen and leaves 8: omen + zone = 2.
    let state = TurnLoop.Start(new BattleSetup(Enemy(), Deck(Lance, Block), Cells, { StartGap: 2 }), NoRng).State;
    state = TurnLoop.BeginPlayerTurn(state, NoRng).State;
    state = TurnLoop.PlayCard(state, InHand(state, 'lance'), NoRng).State;
    state = TurnLoop.PlayCard(state, InHand(state, 'block'), NoRng).State;

    const plays = state.History.MasteryPlays;
    const tally = MasteryTally.Of(state);
    assert.deepEqual(plays.map((p) => p.CardId), ['lance', 'block']);
    assert.ok(plays[0] instanceof MasteryPlay);
    assert.equal(plays[0].Contexts, MasteryContext.Gap | MasteryContext.Trait | MasteryContext.Zone);
    assert.equal(plays[1].Contexts, MasteryContext.Omen | MasteryContext.Zone);
    assert.equal(tally.TicksOf('lance'), 3);
    assert.equal(tally.TicksOf('block'), 2);
    assert.equal(tally.TicksOf('nobody'), 0);
  });
});

describe('MasteryTests: 才能, thresholds, the battle end', () => {
  test('Talent_DoublesThatCardsTicks_AndNoOtherCards', () => {
    const book = MasteryBook.Begin('lance', [Profile('lance', MasteryKind.Extend), Profile('block', MasteryKind.Lighten)]);
    const after = book.AfterBattle(Tally(['lance', 3], ['block', 3])).Book;

    assert.equal(after.TicksOf('lance'), 6);
    assert.equal(after.TicksOf('block'), 3);
    assert.equal(after.Train('lance').Book.TicksOf('lance'), 7, 'training gives a fixed 1, talent or not');
  });

  test('Steps_GoUpAt3_8_15', () => {
    let book = MasteryBook.Begin(null, [Profile('lance', MasteryKind.Extend)]);
    const ranks = [];
    const ups = [];
    for (const ticks of [2, 1, 4, 1, 6, 1]) {
      const result = book.AfterBattle(Tally(['lance', ticks]));
      book = result.Book;
      ranks.push(book.RankOf('lance'));
      ups.push(...result.RankUps);
    }

    // 2 → 3 → 7 → 8 → 14 → 15.
    assert.deepEqual(ranks, [0, 1, 1, 2, 2, 3]);
    eq(ups, [new RankUp('lance', 0, 1), new RankUp('lance', 1, 2), new RankUp('lance', 2, 3)]);
    assert.deepEqual([...Constants.MasteryThresholds], [3, 8, 15]);
  });

  test('ReformCards_StepUpAt2_6_12', () => {
    const book = MasteryBook.Begin(null, [Profile('seer', MasteryKind.Reform)]);
    assert.equal(book.AfterBattle(Tally(['seer', 2])).Book.RankOf('seer'), 1);
    assert.equal(book.AfterBattle(Tally(['seer', 6])).Book.RankOf('seer'), 2);
    assert.equal(book.AfterBattle(Tally(['seer', 12])).Book.RankOf('seer'), 3);
    assert.deepEqual([...Profile('seer', MasteryKind.Reform).Thresholds], [2, 6, 12]);
    assert.deepEqual([...Profile('x', MasteryKind.Lighten).Thresholds], [3, 8, 15]);
  });

  test('Steps_GoUpOnlyOnceTheBattleIsOver', () => {
    const lance = Lance;
    const book = MasteryBook.Begin('lance', [Profile('lance', MasteryKind.Extend)]);
    const deck = book.MasteredDeck(Deck(lance, lance, lance));
    let state = TurnLoop.Start(new BattleSetup(Enemy(), deck, Cells, { StartGap: 2 }), NoRng).State;
    state = TurnLoop.BeginPlayerTurn(state, NoRng).State;

    // Three lances at N 2: 3 ticks each, 18 with the talent — past 8 already.
    for (let i = 0; i < 3; i++) state = TurnLoop.PlayCard(state, InHand(state, 'lance'), NoRng).State;

    assert.equal(state.Result, GameResult.Ongoing);
    assert.equal(MasteryTally.Of(state).TicksOf('lance'), 9);
    assert.throws(() => book.AfterBattle(state),
      (e) => e instanceof InvalidOperationException && e.message === 'Mastery steps go up only once the battle is over (§17.8).',
      'not while the battle goes on');
    assert.ok(state.DiscardPile.every((c) => c.Def === lance || csEquals(c.Def, lance)), 'the cards in play keep the face they started with');
    assert.equal(book.RankOf('lance'), 0);

    // The battle ends (won here): now the steps are judged.
    const won = state.with({ Result: GameResult.Won });
    const result = book.AfterBattle(won);
    assert.equal(result.Book.TicksOf('lance'), 18);
    assert.equal(result.Book.RankOf('lance'), 3);
    eq(result.RankUps, [new RankUp('lance', 0, 3)]);
    assert.equal(result.Book.Mastered(lance).Column, 4, 'the next battle fights with the grown card');

    // A lost battle counts the same (§3.2: 勝っても負けても).
    const lost = book.AfterBattle(state.with({ Result: GameResult.Lost }));
    assert.equal(lost.Book.TicksOf('lance'), 18);
    assert.equal(lost.Book.RankOf('lance'), 3);
  });

  test('ACardWithNoProfile_GathersTicks_ButDoesNotStepUp', () => {
    const book = MasteryBook.Begin(null, []);
    const after = book.AfterBattle(Tally(['lance', 20])).Book;
    assert.equal(after.TicksOf('lance'), 20);
    assert.equal(after.RankOf('lance'), 0);
    eq(after.Mastered(Lance), Lance);
  });

  test('a tally entry of 0 or less is passed over', () => {
    const book = MasteryBook.Begin(null, [Profile('lance', MasteryKind.Extend)]);
    const after = book.AfterBattle(Tally(['lance', 0], ['block', -2])).Book;
    assert.equal(after.Records.size, 0);
    assert.equal(after.TicksOf('block'), 0);
  });
});

describe('MasteryTests: the three kinds', () => {
  test('Extend_OnACostThreeCard_GivesTheColumnFourValue_AtCostThree', () => {
    // 突き (#1): cost 3, 0〜2 (width 3), 23 = 21 + 素直 2. Column 4 of width 3 is 30, so 32.
    const thrust = CardCatalog.Thrust;
    const extended = Mastery.Master(thrust, 1, Profile(thrust.Id, MasteryKind.Extend));
    // 柄当て (#36): two faces, cost 3, 18 / Guard 10. Column 4 of a two-face card is +6 / +4.
    const guardThrust = CardCatalog.GuardThrust;
    const both = Mastery.Master(guardThrust, 1, Profile(guardThrust.Id, MasteryKind.Extend));

    assert.equal(thrust.Cost, 3);
    assert.equal(extended.Column, 4);
    assert.equal(extended.Face.Power, Columns.Value(Columns.SingleAttackPower, 4) + Constants.PlainCardBonus);
    assert.equal(extended.Face.Power, 32);
    assert.equal(extended.Cost, 3, '伸び keeps the cost');
    eq(extended.Face.Reach, thrust.Face.Reach);

    assert.equal(both.Face.Power, 24);
    assert.equal(both.Face.Guard, 14);
    assert.equal(both.Cost, 3);

    // Column 4 is the last: a further step adds nothing, and 集中 on it adds nothing either.
    assert.equal(Mastery.Master(thrust, 2, Profile(thrust.Id, MasteryKind.Extend)).Face.Power, 32);
    eq(FocusStep.Of(extended.Attributes, extended.Column, extended.Face), FocusStep.None);
  });

  test('Extend_OnACostOneCard_ClimbsAColumnAStep_AndKeepsCostOne', () => {
    const cut = Card('cut', 1, new Face({ Power: 6, Statuses: [new StatusGrant(StatusKind.Bleed, 1)] }));
    const profile = Profile('cut', MasteryKind.Extend);
    const one = Mastery.Master(cut, 1, profile);
    const three = Mastery.Master(cut, 3, profile);

    assert.equal(one.Face.Power, 13);
    assert.equal(one.Face.StatusList[0].Stacks, 2, '出血 climbs the stack scale too');
    assert.equal(one.Cost, 1);
    assert.equal(three.Face.Power, 30);
    assert.equal(three.Column, 4);
    assert.equal(three.Cost, 1);
    assert.equal(three.FixedCost, 1);
    assert.equal(cut.Face.StatusList[0].Stacks, 1, 'the card itself is untouched');
  });

  test('Lighten_TakesTheCostOneDownAStep_FloorZero', () => {
    const heavy = Card('heavy', 2, new Face({ Power: 13 }));
    const profile = Profile('heavy', MasteryKind.Lighten);
    assert.equal(Mastery.Master(heavy, 1, profile).Cost, 1);
    assert.equal(Mastery.Master(heavy, 2, profile).Cost, 0, '§19.3 S5: column 2 at step 2 is cost 0');
    assert.equal(Mastery.Master(heavy, 3, profile).Cost, 0, 'floor 0');
    eq(Mastery.Master(heavy, 2, profile).Face, heavy.Face, 'the numbers stay');
  });

  test('ALightenedCard_AtCostZero_IsPlayedWithNoStamina', () => {
    const heavy = Card('heavy', 2, new Face({ Power: 13 }));
    const light = Mastery.Master(heavy, 2, Profile('heavy', MasteryKind.Lighten));
    let state = TurnLoop.Start(new BattleSetup(Enemy(), Deck(light), Cells, { StartGap: 0 }), NoRng).State;
    state = TurnLoop.BeginPlayerTurn(state, NoRng).State;
    state = state.with({ Player: state.Player.with({ Stamina: 0 }) });

    const played = TurnLoop.PlayCard(state, InHand(state, 'heavy'), NoRng);
    assert.equal(TurnLoop.CostNow(state, light), 0);
    const spent = played.Events.filter((e) => e instanceof StaminaSpent);
    assert.equal(spent.length, 1);
    assert.equal(spent[0].Amount, 0);
  });

  test('Reform_SwapsTheTrait_AndTheName_AndKeepsTheNumbers', () => {
    // §19.3: 観察 → 精査 (締め) → 洞察 (連動(Sk)) → 看破 (初手), ドロー +1 throughout.
    const observe = CardCatalog.Observe;
    const profile = MasteryProfiles.Find('observe');
    const steps = [1, 2, 3].map((rank) => Mastery.Master(observe, rank, profile));

    assert.equal(profile.Kind, MasteryKind.Reform);
    assert.deepEqual(steps.map((c) => c.Name), ['精査', '洞察', '看破']);
    assert.deepEqual(steps.map((c) => c.Trait.Condition), [TraitCondition.Finisher, TraitCondition.Combo, TraitCondition.FirstPlay]);
    assert.ok(steps.every((c) => c.Trait.Effect === TraitEffect.Draw));
    assert.ok(steps.every((c) => c.Trait.Amount === 1));
    assert.equal(steps[1].Trait.Attribute, A.Skill);
    for (const step of steps) {
      eq(step.Face, observe.Face);
      assert.equal(step.Cost, observe.Cost);
      assert.equal(step.Id, observe.Id);
    }
  });

  test('Master: step 0 is the card itself, and a step outside 0..3 is refused', () => {
    const profile = Profile('lance', MasteryKind.Extend);
    assert.equal(Mastery.Master(Lance, 0, profile), Lance);
    assert.throws(() => Mastery.Master(Lance, 4, profile),
      (e) => e instanceof ArgumentOutOfRangeException && e.message === "A step is 0..3. (Parameter 'rank')\nActual value was 4.");
    assert.throws(() => Mastery.Master(Lance, -1, profile), (e) => e instanceof ArgumentOutOfRangeException);
    assert.throws(() => Mastery.Master(Lance, 1, null), (e) => e instanceof ArgumentNullException && e.message === "Value cannot be null. (Parameter 'profile')");
    assert.throws(() => Mastery.Master(null, 1, profile), (e) => e instanceof ArgumentNullException && e.message === "Value cannot be null. (Parameter 'def')");
  });

  test('a profile without names keeps the card\'s name; NameAt / TraitAt read steps 1-3 only', () => {
    const plain = Profile('lance', MasteryKind.Lighten);
    assert.equal(Mastery.Master(Lance, 1, plain).Name, 'lance');
    const observe = MasteryProfiles.Observe;
    assert.deepEqual([0, 1, 2, 3, 4].map((r) => observe.NameAt(r)), [null, '精査', '洞察', '看破', null]);
    assert.equal(observe.TraitAt(0), null);
    assert.equal(observe.TraitAt(4), null);
    assert.equal(plain.TraitAt(1), null);
  });
});

describe('MasteryTests: step 3 for one card, 習熟訓練', () => {
  test('Step3_CannotGoToASecondCard', () => {
    const profiles = [Profile('lance', MasteryKind.Extend), Profile('block', MasteryKind.Lighten)];
    const book = MasteryBook.Begin(null, profiles).AfterBattle(Tally(['lance', 15])).Book;
    const after = book.AfterBattle(Tally(['block', 40])).Book;

    assert.equal(book.TopRankHolder, 'lance');
    assert.equal(after.RankOf('lance'), 3);
    assert.equal(after.RankOf('block'), 2, 'the others stop at step 2');
    assert.equal(after.TicksOf('block'), 40, 'the ticks still count');
    assert.equal(after.Train('block').Book.RankOf('block'), 2);
  });

  test('Step3_ReachedByTwoAtOnce_GoesToTheMoreTicks_ThenTheTalent', () => {
    const profiles = [Profile('lance', MasteryKind.Extend), Profile('block', MasteryKind.Lighten), Profile('cut', MasteryKind.Extend)];
    const byTicks = MasteryBook.Begin(null, profiles).AfterBattle(Tally(['lance', 15], ['block', 16]));
    const byTalent = MasteryBook.Begin('lance', profiles).AfterBattle(Tally(['cut', 16], ['lance', 8]));

    assert.equal(byTicks.Book.TopRankHolder, 'block');
    assert.equal(byTicks.Book.RankOf('lance'), 2);
    equivalent(byTicks.RankUps, [new RankUp('block', 0, 3), new RankUp('lance', 0, 2)]);

    // lance 8 × 2 = 16 ties cut's 16: the talent card takes it.
    assert.equal(byTalent.Book.TopRankHolder, 'lance');
    assert.equal(byTalent.Book.RankOf('cut'), 2);
  });

  test('Step3_ReachedByTwoAtOnce_TiedWithNoTalent_GoesToTheOrdinalFirstId', () => {
    const profiles = [Profile('lance', MasteryKind.Extend), Profile('block', MasteryKind.Extend)];
    const tied = MasteryBook.Begin(null, profiles).AfterBattle(Tally(['lance', 15], ['block', 15]));
    assert.equal(tied.Book.TopRankHolder, 'block');
    assert.equal(tied.Book.RankOf('lance'), 2);
  });

  test('Training_GivesTheNamedCardOneTick_AndIsJudgedAtOnce', () => {
    const book = MasteryBook.Begin(null, [Profile('lance', MasteryKind.Extend)]).AfterBattle(Tally(['lance', 2])).Book;
    const trained = book.Train('lance');

    assert.equal(Constants.TrainingTicks, 1);
    assert.equal(trained.Book.TicksOf('lance'), 3);
    assert.equal(trained.Book.RankOf('lance'), 1);
    eq(trained.RankUps, [new RankUp('lance', 0, 1)]);
    assert.equal(trained.Book.TicksOf('block'), 0, 'only the named card');
    assert.throws(() => book.Train(null), (e) => e instanceof ArgumentNullException && e.message === "Value cannot be null. (Parameter 'cardId')");
  });
});

describe('MasteryBook / MasteryTally (JS port specifics)', () => {
  test('Records is a Map in ordinal id order, and the book is a new record each time', () => {
    const profiles = [Profile('zeta', MasteryKind.Extend), Profile('alpha', MasteryKind.Extend), Profile('Beta', MasteryKind.Extend)];
    const begun = MasteryBook.Begin('alpha', profiles);
    const result = begun.AfterBattle(Tally(['zeta', 3], ['alpha', 2], ['Beta', 8]));
    assert.ok(result instanceof MasteryResult);
    assert.ok(result.Book instanceof MasteryBook);
    assert.ok(result.Book.Records instanceof Map);
    assert.deepEqual([...result.Book.Records.keys()], ['Beta', 'alpha', 'zeta'], 'ordinal: upper case first');
    eq(result.Book.Records.get('alpha'), new CardMastery('alpha', 4, 1));
    eq(result.RankUps, [new RankUp('Beta', 0, 2), new RankUp('alpha', 0, 1), new RankUp('zeta', 0, 1)]);
    assert.equal(begun.Records, null, 'the book it came from is untouched');
    assert.equal(result.Book.Talent, 'alpha');
    assert.equal(result.Book.Profiles, profiles);
  });

  test('the default profiles are the canon\'s, and Begin with no talent has none', () => {
    const book = MasteryBook.Begin(null);
    assert.equal(book.Talent, null);
    assert.equal(book.Profiles, null);
    assert.equal(book.ProfileList, MasteryProfiles.All);
    assert.equal(book.ProfileOf('thrust').Kind, MasteryKind.Extend);
    assert.equal(book.ProfileOf('miasma_blade'), null, 'a card that keeps its name has no profile');
    assert.equal(book.TopRankHolder, null);
  });

  test('MasteredDeck keeps every instance id and grows each card to its step', () => {
    const book = MasteryBook.Begin(null).Train('thrust').Book.Train('thrust').Book.Train('thrust').Book;
    assert.equal(book.RankOf('thrust'), 1);
    const deck = [new CardInstance('thrust-0', CardCatalog.Thrust), new CardInstance('brace-0', CardCatalog.Brace)];
    const grown = book.MasteredDeck(deck);
    assert.deepEqual(grown.map((c) => c.InstanceId), ['thrust-0', 'brace-0']);
    assert.ok(grown[0] instanceof CardInstance);
    assert.equal(grown[0].Def.Name, '鋭い突き');
    assert.equal(grown[0].Def.Column, 4);
    assert.equal(grown[1].Def, CardCatalog.Brace, 'step 0: the card as it is');
    assert.throws(() => book.MasteredDeck(null), (e) => e instanceof ArgumentNullException);
    assert.throws(() => book.Mastered(null), (e) => e instanceof ArgumentNullException);
  });

  test('MasteryTally.Of reads a BattleState or any list of plays (the two C# overloads)', () => {
    const plays = [new MasteryPlay('a', MasteryContext.Gap | MasteryContext.Zone), new MasteryPlay('b', MasteryContext.Omen), new MasteryPlay('a', MasteryContext.Trait)];
    const fromList = MasteryTally.Of(plays);
    assert.deepEqual([...fromList.Ticks.entries()], [['a', 3], ['b', 1]], 'first-seen order, ticks added up');
    const fromSet = MasteryTally.Of(new Set(plays));
    assert.deepEqual([...fromSet.Ticks.entries()], [['a', 3], ['b', 1]]);

    const history = plays.reduce((h, p) => h.WithMasteryPlay(p), new BattleHistory());
    const fromState = MasteryTally.Of({ History: history });
    assert.deepEqual([...fromState.Ticks.entries()], [['a', 3], ['b', 1]]);
    assert.throws(() => MasteryTally.Of(null), (e) => e instanceof ArgumentNullException && e.message === "Value cannot be null. (Parameter 'state')");
    assert.throws(() => fromList.TicksOf(null), (e) => e instanceof ArgumentNullException);
  });

  test('AfterBattle dispatches on its argument: a tally, or a finished battle', () => {
    const book = MasteryBook.Begin(null, [Profile('a', MasteryKind.Extend)]);
    const history = new BattleHistory().WithMasteryPlay(new MasteryPlay('a', 15));
    assert.equal(book.AfterBattle({ Result: GameResult.Won, History: history }).Book.TicksOf('a'), 4);
    assert.equal(book.AfterBattle(Tally(['a', 4])).Book.TicksOf('a'), 4);
    assert.throws(() => book.AfterBattle(null), (e) => e instanceof ArgumentNullException && e.message === "Value cannot be null. (Parameter 'finished')");
  });

  test('records take C# named arguments', () => {
    const p = new MasteryProfile('x', MasteryKind.Reform, { RankNames: ['一', '二', '三'] });
    assert.deepEqual(p.RankNames, ['一', '二', '三']);
    assert.equal(p.RankTraits, null);
    assert.equal(p.GapContext, null);
    const b = new MasteryBook({ Talent: 'x' });
    assert.equal(b.Talent, 'x');
    assert.equal(b.Records, null);
  });

  test('the movement growth tables', () => {
    assert.deepEqual([...Mastery.MovementGuardByRank], [0, 2, 5, 5]);
    assert.deepEqual([...Mastery.MovementExtraByRank], [0, 0, 1, 1]);
    assert.equal(Mastery.TopRank, 3);
  });
});

// ======================================================================== MasteryProfilesTests

const IndexOf = (id) => CardCatalog.All.findIndex((c) => c.Id === id);

function AssertGrowth(id, read, byRank) {
  const card = CardCatalog.ById(id);
  const profile = MasteryProfiles.Find(id);
  const steps = [0, 1, 2, 3].map((rank) => Mastery.Master(card, rank, profile));

  assert.equal(profile.Kind, MasteryKind.Extend);
  assert.deepEqual(steps.map(read), byRank);
  for (const step of steps.slice(1)) {
    // Only the extra grows: the move, the column, the cost and the trait stay.
    assert.equal(step.Face.Move, card.Face.Move, step.Name);
    assert.equal(step.Column, card.Column, step.Name);
    assert.equal(step.Cost, 1, step.Name);
    eq(step.Trait, card.Trait, step.Name);
    assert.equal(step.Face.Power, 0, step.Name);
    eq(FocusStep.Of(step.Attributes, step.Column, step.Face), FocusStep.None, '集中 still does not grow a movement card');
  }
  // The other extras stay where they were.
  const extras = (c) => c.Face.Guard + c.Face.Draw + c.Face.StaminaGain - read(c);
  assert.equal(extras(steps[3]), extras(card));
  assert.deepEqual(steps.slice(1).map((c) => c.Name), [...profile.RankNames]);
}

const KindWord = (kind) => ({ Extend: '伸び', Lighten: '軽さ', Reform: '型替え' })[kind];

function OmenWord(omen) {
  switch (omen) {
    case OmenKind.Attack: return '攻撃';
    case OmenKind.Guard: return '防御';
    case OmenKind.Move: return '移動';
    default: throw new Error(`OmenWord ${omen}`);
  }
}

function AttributeWord(attribute) {
  switch (attribute) {
    case A.Attack: return 'A';
    case A.Guard: return 'G';
    case A.Skill: return 'Sk';
    case A.Stance: return 'St';
    default: throw new Error(`AttributeWord ${attribute}`);
  }
}

/** A condition as §4.6 writes it (§1.2's words). */
function ConditionWord(trait) {
  switch (trait.Condition) {
    case TraitCondition.Broken: return '崩し後';
    case TraitCondition.FirstPlay: return '初手';
    case TraitCondition.Finisher: return '締め';
    case TraitCondition.Chain: return '連打';
    case TraitCondition.Reserve: return `温存(残 ≥ ${trait.Threshold})`;
    case TraitCondition.OmenIs: return `予兆(${OmenWord(trait.Omen)})`;
    case TraitCondition.Combo: return `連動(${AttributeWord(trait.Attribute)})`;
    default: throw new Error(`§4.6 does not loosen to ${trait.Condition}.`);
  }
}

/** The canon beside this repository (read only); null when it is not there. */
const CanonPath = fileURLToPath(new URL('../../../../.claude/docs/card_document/swordsman_cards_v4.md', import.meta.url));
function FindCanon() {
  try {
    return fs.existsSync(CanonPath) ? CanonPath : null;
  } catch {
    return null;
  }
}

/** The rows of §4.6's first table: # / 名前 / 列 / 型 / 段 1 / 段 2 / 段 3 / 型替えの条件. */
function ReadTable(path) {
  const rows = [];
  let inside = false;
  for (const line of fs.readFileSync(path, 'utf8').split(/\r?\n/)) {
    if (line.startsWith('### 4.6')) { inside = true; continue; }
    if (!inside) continue;
    if (line.startsWith('## ') || line.startsWith('### ')) break;
    if (!line.startsWith('|')) continue;
    const cells = line.split('|').map((c) => c.trim());
    if (cells.length < 10 || !/^[+-]?\d+$/.test(cells[1])) continue;
    rows.push({
      Number: Number(cells[1]),
      Name: cells[2],
      Column: Number(cells[3]),
      Kind: cells[4],
      Names: [cells[5], cells[6], cells[7]],
      Conditions: cells[8],
    });
  }
  return rows;
}

describe('MasteryProfilesTests', () => {
  test('TheProfiles_AreSixtyCardsOfTheCatalog_InCanonOrder', () => {
    const ids = MasteryProfiles.All.map((p) => p.CardId);
    const order = ids.map(IndexOf);
    assert.equal(ids.length, 60);
    assert.equal(new Set(ids).size, 60);
    assert.ok(order.every((i) => i >= 0), 'every profile is a catalog card');
    assert.ok(order.every((i, k) => k === 0 || order[k - 1] <= i), 'canon order');
    assert.equal(MasteryProfiles.Find('observe'), MasteryProfiles.Observe);
    assert.equal(MasteryProfiles.All, MasteryProfiles.All, 'built once (static readonly)');
  });

  test('Find: both overloads, and null refused', () => {
    const mine = [Profile('a', MasteryKind.Extend)];
    assert.equal(MasteryProfiles.Find(mine, 'a'), mine[0]);
    assert.equal(MasteryProfiles.Find(mine, 'thrust'), null);
    assert.equal(MasteryProfiles.Find('nothing'), null);
    assert.throws(() => MasteryProfiles.Find(null, 'a'), (e) => e instanceof ArgumentNullException && e.message === "Value cannot be null. (Parameter 'profiles')");
    assert.throws(() => MasteryProfiles.Find(null), (e) => e instanceof ArgumentNullException && e.message === "Value cannot be null. (Parameter 'cardId')");
  });

  for (const [kind, one, two, three] of [
    [MasteryKind.Extend, 10, 12, 8],
    [MasteryKind.Lighten, 0, 3, 11],
    [MasteryKind.Reform, 8, 6, 2],
  ]) {
    test(`TheKinds_ByColumn_MatchTheCanonTally(${kind})`, () => {
      const columns = MasteryProfiles.All.filter((p) => p.Kind === kind).map((p) => CardCatalog.ById(p.CardId).Column);
      assert.equal(columns.filter((c) => c === 1).length, one);
      assert.equal(columns.filter((c) => c === 2).length, two);
      assert.equal(columns.filter((c) => c === 3).length, three);
    });
  }

  test('EveryReformCard_KeepsItsEffect_AndEndsAtFirstPlay', () => {
    // §19.3 S6: the effect stays, only the condition loosens; #504: every path ends at 初手.
    const reforms = MasteryProfiles.All.filter((p) => p.Kind === MasteryKind.Reform);
    assert.equal(reforms.length, 16);
    for (const profile of reforms) {
      const own = CardCatalog.ById(profile.CardId).Trait;
      assert.equal(profile.RankTraits.length, 3, profile.CardId);
      for (const trait of profile.RankTraits) {
        assert.equal(trait.Effect, own.Effect, profile.CardId);
        assert.equal(trait.Amount, own.Amount, profile.CardId);
        eq(trait.Grant, own.Grant, profile.CardId);
        assert.equal(trait.Watch, null, profile.CardId);
      }
      assert.equal(profile.RankTraits[2].Condition, TraitCondition.FirstPlay, profile.CardId);
    }
    for (const profile of MasteryProfiles.All.filter((p) => p.Kind !== MasteryKind.Reform)) {
      assert.equal(profile.RankTraits, null, profile.CardId + ' keeps its trait');
    }
  });

  test('the loosened traits reset the old condition\'s parameter (only the new one is set)', () => {
    const war = MasteryProfiles.Find('war_cry');
    assert.deepEqual(war.RankTraits.map((t) => [t.Condition, t.Threshold, t.Attribute, t.Omen, t.Watch]),
      [[TraitCondition.Combo, 0, A.Skill, null, null], [TraitCondition.Combo, 0, A.Attack, null, null], [TraitCondition.FirstPlay, 0, A.None, null, null]]);
    const brace = MasteryProfiles.Find('brace');
    assert.deepEqual(brace.RankTraits.map((t) => [t.Condition, t.Threshold]),
      [[TraitCondition.Reserve, 4], [TraitCondition.Reserve, 2], [TraitCondition.FirstPlay, 0]]);
    const flat = MasteryProfiles.Find('flat_strike');
    assert.deepEqual(flat.RankTraits.map((t) => [t.Condition, t.Omen]),
      [[TraitCondition.Broken, null], [TraitCondition.OmenIs, OmenKind.Attack], [TraitCondition.FirstPlay, null]]);
  });

  const canon = FindCanon();
  test('TheProfiles_MatchTheTableOfSection46', { skip: canon == null ? 'swordsman_cards_v4.md is not beside this copy of the tests' : false }, () => {
    const rows = ReadTable(canon);
    assert.equal(rows.length, 60, '§4.6 has sixty rows');
    for (const row of rows) {
      const card = CardCatalog.All[row.Number - 1];
      const at = `#${row.Number} ${row.Name}`;
      const profile = MasteryProfiles.Find(card.Id);

      assert.equal(card.Name, row.Name, at);
      assert.equal(card.Column, row.Column, at);
      assert.notEqual(profile, null, at + ' has a profile');
      assert.equal(KindWord(profile.Kind), row.Kind, at);
      assert.deepEqual([...profile.RankNames], row.Names, at);
      const conditions = profile.RankTraits == null ? '—' : profile.RankTraits.map(ConditionWord).join(' → ');
      assert.equal(conditions, row.Conditions, at);
    }

    const listed = new Set(rows.map((r) => CardCatalog.All[r.Number - 1].Id));
    for (const profile of MasteryProfiles.All) {
      assert.ok(listed.has(profile.CardId), `${profile.CardId} has a profile, §4.6 has no row for it`);
    }
  });

  for (const id of ['back_leap', 'slide_step']) {
    test(`Extend_OnAMovementCardWithGuard_Grows4_6_9_9(${id})`, () => AssertGrowth(id, (c) => c.Face.Guard, [4, 6, 9, 9]));
  }

  for (const id of ['footwork', 'leap_back']) {
    test(`Extend_OnAMovementCardWithADraw_DrawsOneMoreFromStep2(${id})`, () => AssertGrowth(id, (c) => c.Face.Draw, [1, 1, 2, 2]));
  }

  test('Extend_OnTheDash_GivesOneMoreStaminaFromStep2', () => AssertGrowth('dash_in', (c) => c.Face.StaminaGain, [1, 1, 2, 2]));

  test('TheFiveMovementCards_AreTheExtendOnesOfTheSix', () => {
    // §2.1: six movement-centred cards; 間合い切り (#28) is 型替え, the other five are 伸び.
    const movement = CardCatalog.All.filter((c) => AttributeRule.IsMovementCard(c.Attributes, c.Face));
    assert.equal(movement.length, 6);
    assert.deepEqual(movement.filter((c) => MasteryProfiles.Find(c.Id).Kind === MasteryKind.Extend).map((c) => c.Id),
      ['footwork', 'back_leap', 'slide_step', 'leap_back', 'dash_in']);
    assert.equal(MasteryProfiles.Find('break_off').Kind, MasteryKind.Reform);
  });
});
