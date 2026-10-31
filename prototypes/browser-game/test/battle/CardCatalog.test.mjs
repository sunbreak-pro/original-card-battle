// Tests for src/core/battle/CardCatalog.js (port of unity-port/BattleCore/CardCatalog.cs, the merged
// partial class). Cases ported from BattleCore.Tests/CardCatalogTests.cs and PrototypeDeckTests.cs.
//
// - The catalog is counted and fifteen cards are pinned field by field. The expected values below
//   are written by hand from CardCatalog.Initial.cs / CardCatalog.Learned.cs (not copied from the JS).
// - Every one of the eighty is then diffed against the C# oracle's dump (oracle/out/catalog.json,
//   written by the C# CardCatalog itself), when that file is present.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { CardCatalog, PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { CardCatalog as InitialPart } from '../../src/core/battle/CardCatalog.Initial.js';
import { CardCatalog as LearnedPart } from '../../src/core/battle/CardCatalog.Learned.js';
import { Cards } from '../../src/core/battle/Cards.js';
import { BattleAttribute, CardDef, CardInstance, GameResult, Reach, StanceDef, StanceHook, StatusGrant, StatusKind, TargetKind, TraitCondition, TraitEffect } from '../../src/core/battle/Types.js';
import { AttributeRule } from '../../src/core/battle/Attributes.js';
import { Statuses } from '../../src/core/battle/Statuses.js';
import { Constants } from '../../src/core/battle/Constants.js';
import { EnemyAi } from '../../src/core/battle/EnemyAi.js';
import { FixedRng, SeededRng } from '../../src/core/battle/IRng.js';
import { errorOf, fixture, late, pin, face, stanceOf, traitOf, card } from './_battle_cards.mjs';

const A = BattleAttribute;

// ---- The catalog, counted ----

test('TheCatalog_HoldsEightyKinds_WithUniqueIds', () => {
  assert.equal(CardCatalog.All.length, Constants.OwnedKindsMax);
  assert.equal(CardCatalog.All.length, 80);
  assert.equal(new Set(CardCatalog.All.map((c) => c.Id)).size, 80);
  assert.ok(CardCatalog.All.every((c) => c instanceof CardDef), 'a field read before it was set');
  for (const c of CardCatalog.All) assert.equal(CardCatalog.ById(c.Id), c, c.Id);
  assert.equal(CardCatalog.All, CardCatalog.All, 'one list, built once');
  assert.ok(Object.isFrozen(CardCatalog.All));
  assert.ok(Object.isFrozen(CardCatalog));
});

test('TheCanonOrder_StartsWithThrust_AndEndsWithLastStand', () => {
  assert.equal(CardCatalog.All[0].Id, 'thrust');
  assert.equal(CardCatalog.All[39].Id, 'twist_away', '#40, the last of the initial forty');
  assert.equal(CardCatalog.All[40].Id, 'miasma_blade', '#41, the first learned card');
  assert.equal(CardCatalog.All[79].Id, 'last_stand');
});

test('The merged catalog holds both parts under their C# names, the same objects', () => {
  const initial = Object.keys(InitialPart);
  const learned = Object.keys(LearnedPart);
  assert.equal(initial.length, 40);
  assert.equal(learned.length, 40);
  assert.deepEqual([...initial, ...learned].map((name) => CardCatalog[name]), CardCatalog.All, 'All is the parts in declaration order');
  for (const name of [...initial, ...learned]) {
    assert.equal(CardCatalog[name], InitialPart[name] ?? LearnedPart[name], name);
  }
  assert.deepEqual(Object.keys(CardCatalog).filter((k) => k !== 'All' && k !== 'ById'), [...initial, ...learned]);
});

test('ById: an unknown id and null throw as in C#', () => {
  assert.deepEqual(errorOf(() => CardCatalog.ById('nobody')), fixture.Errors.ById);
  assert.deepEqual(errorOf(() => CardCatalog.ById(null)), fixture.Errors.ByIdNull);
  assert.deepEqual(errorOf(() => CardCatalog.ById('Thrust')), { $error: 'KeyNotFoundException', Message: 'Unknown card id "Thrust".' }, 'ordinal: case matters');
});

// ---- Fifteen cards, field by field (against the C# source) ----

test('Fifteen cards pinned field by field against CardCatalog.Initial.cs / .Learned.cs', () => {
  const expected = {
    Thrust: card({
      Id: 'thrust', Name: '突き', Attributes: A.Attack, Column: 3, Face: face({ Power: 23, Reach: [0, 2] }),
      Description: 'まっすぐ突く', Cost: 3, Attribute: A.Attack,
    }),
    KesaCut: card({
      Id: 'kesa_cut', Name: '袈裟斬り', Attributes: A.Attack, Column: 2, Face: face({ Power: 15 }),
      Trait: traitOf({ Condition: 'GapAtMost', Effect: 'PowerBonus', Amount: 5, Threshold: 0 }),
      Description: '肩口から斬り下ろす', Cost: 2, Attribute: A.Attack,
    }),
    Brace: card({
      Id: 'brace', Name: '呼吸を整える', Attributes: A.Guard, Column: 2, Face: face({ Guard: 9 }),
      Trait: traitOf({ Condition: 'Reserve', Effect: 'NextTurnRecovery', Amount: 1, Threshold: 6 }),
      Targets: 'Self', Description: '息を整えて受けに備える', Cost: 2, Attribute: A.Guard,
    }),
    WaterStance: card({
      Id: 'water_stance', Name: '水の構え', Attributes: A.Stance, Column: 2,
      Face: face({ Stance: stanceOf({ Hook: 'TurnStart', When: 'GapAtLeast', Threshold: 2, Recovery: 2 }) }),
      Trait: traitOf({ Condition: 'Moved', Effect: 'CostDown', Amount: 1 }),
      Targets: 'Self', Description: '退いて息を戻す型', Cost: 2, Attribute: A.Stance,
    }),
    SpiritRoar: card({
      Id: 'spirit_roar', Name: '気迫', Attributes: A.Skill, Column: 1,
      Face: face({ Reach: [0, 2], Statuses: [['Intimidate', 2, false, 0]] }),
      Trait: traitOf({ Condition: 'FoeHas', Effect: 'Status', Watch: 'Bleed', Grant: ['Fragile', 2, false, 0] }),
      Description: '声で相手の次の一手を鈍らせる', Cost: 1, Attribute: A.Skill,
    }),
    Focus: card({
      Id: 'focus', Name: '集中', Attributes: A.Skill, Column: 3,
      Face: face({ Draw: 1, Statuses: [['Focus', 2, true, 0], ['Empower', 2, true, 0]] }),
      Trait: traitOf({ Condition: 'FirstPlay', Effect: 'Draw', Amount: 1 }),
      Targets: 'Self', Description: '次の一手に力を溜める', Cost: 3, Attribute: A.Skill,
    }),
    Observe: card({
      Id: 'observe', Name: '観察', Attributes: A.Guard | A.Skill, Column: 1, Face: face({ Guard: 3, Draw: 1 }),
      Trait: traitOf({ Condition: 'Thin', Effect: 'Draw', Amount: 1 }),
      Targets: 'Self', Description: '受けを固めて相手をよく見る', Cost: 1, Attribute: A.Guard,
    }),
    BackLeap: card({
      Id: 'back_leap', Name: '後ろ跳び', Attributes: A.None, Column: 1, Face: face({ Move: -2, Guard: 4 }),
      Trait: traitOf({ Condition: 'OmenIs', Effect: 'GuardBonus', Amount: 3, Omen: 'Attack' }),
      Targets: 'Self', Description: '跳んで間合いを外す', Cost: 1, Attribute: A.Guard,
    }),
    GuardThrust: card({
      Id: 'guard_thrust', Name: '柄当て', Attributes: A.Attack | A.Guard, Column: 3, Face: face({ Power: 18, Guard: 10, Reach: [0, 0] }),
      Trait: traitOf({ Condition: 'Combo', Effect: 'PowerBonus', Amount: 3, Attribute: A.Guard }),
      Description: '受けた勢いで柄を突き出す', Cost: 3, Attribute: A.Attack,
    }),
    TwistAway: card({
      Id: 'twist_away', Name: '翻し足', Attributes: A.Skill, Column: 3,
      Face: face({ Move: -1, Statuses: [['Slow', 2, false, 0], ['Fatigue', 2, false, 0], ['Intimidate', 2, false, 0]] }),
      Trait: traitOf({ Condition: 'Combo', Effect: 'StaminaGain', Amount: 1, Attribute: A.Attack }),
      Description: '斬った後に身を翻し足を封じる', Cost: 3, Attribute: A.Skill,
    }),
    PriestPrayer: card({
      Id: 'priest_prayer', Name: '司祭の祈り', Attributes: A.Stance, Column: 2,
      Face: face({ Stance: stanceOf({ Hook: 'OnHit', Guard: 3, Stamina: 1, OncePerTurn: true }) }),
      Trait: traitOf({ Condition: 'SelfHas', Effect: 'CostDown', Amount: 1, Watch: 'Regen' }),
      Targets: 'Self', Description: '痛みを力に変える型', Cost: 2, Attribute: A.Stance,
    }),
    HaulStep: card({
      Id: 'haul_step', Name: '手繰りの歩み', Attributes: A.Attack, Column: 1, Face: face({ Power: 5, Push: -1, Reach: [1, 2] }),
      Trait: traitOf({ Condition: 'Broken', Effect: 'PowerBonus', Amount: 5 }),
      Description: '崩れた相手を手繰り寄せて潰す', Cost: 1, Attribute: A.Attack,
    }),
    SpearWall: card({
      Id: 'spear_wall', Name: '槍衾', Attributes: A.Stance, Column: 1,
      Face: face({ Stance: stanceOf({ Hook: 'OnHit', Status: 'Bleed', StatusStacks: 1 }) }),
      Trait: traitOf({ Condition: 'FoeHas', Effect: 'GuardBonus', Amount: 3, Watch: 'Bleed' }),
      Targets: 'Self', Description: '触れた者を刺す受け', Cost: 1, Attribute: A.Stance,
    }),
    Resolve: card({
      Id: 'resolve', Name: '覚悟', Attributes: A.Guard | A.Skill, Column: 1,
      Face: face({ Guard: 3, Statuses: [['Empower', 2, true, 0]] }),
      Trait: traitOf({ Condition: 'Desperate', Effect: 'Status', Grant: ['Regen', 1, true, 0] }),
      Targets: 'Self', Description: '追い詰められて腹を括り、受け止める', Cost: 1, Attribute: A.Guard,
    }),
    LastStand: card({
      Id: 'last_stand', Name: '背水の陣', Attributes: A.Attack, Column: 1, Face: face({ Power: 8, Reach: [1, 2] }),
      Trait: traitOf({ Condition: 'GapAtLeast', Effect: 'PowerBonus', Amount: 5, Threshold: 2 }),
      Description: '退いて追い詰められてこそ伸びる一撃',
      ExtraTrait: traitOf({ Condition: 'Desperate', Effect: 'PowerBonus', Amount: 3 }),
      Cost: 1, Attribute: A.Attack,
    }),
  };
  for (const [name, want] of Object.entries(expected)) {
    assert.deepEqual(pin(CardCatalog[name]), want, name);
  }
  assert.equal(Object.keys(expected).length, 15);
});

// ---- The canon's tallies (swordsman_cards_v4 §4) ----

test('TheAttributePatterns_MatchTheCanonTally', () => {
  const rows = [
    ['A', 15], ['G', 7], ['St', 14], ['Sk', 6], ['M', 6], ['A+M', 8], ['A+Sk', 8], ['A+G', 5], ['A+St', 0],
    ['G+M', 4], ['G+Sk', 5], ['G+St', 0], ['Sk+M', 2], ['Sk+St', 0], ['St+M', 0],
  ];
  for (const [pattern, count] of rows) {
    let attributes = A.None;
    let movement = false;
    for (const word of pattern.split('+')) {
      if (word === 'M') { movement = true; continue; }
      attributes |= { A: A.Attack, G: A.Guard, St: A.Stance, Sk: A.Skill }[word];
    }
    assert.equal(CardCatalog.All.filter((c) => c.Attributes === attributes && AttributeRule.HasMovement(c.Face) === movement).length, count, pattern);
  }
});

test('TheCosts_AreTwentyFiveThirtyOneTwentyFour_AndNoCardStartsInColumnFour', () => {
  assert.equal(CardCatalog.All.filter((c) => c.Cost === 1).length, 25);
  assert.equal(CardCatalog.All.filter((c) => c.Cost === 2).length, 31);
  assert.equal(CardCatalog.All.filter((c) => c.Cost === 3).length, 24);
  assert.equal(CardCatalog.All.some((c) => c.Column === 4), false);
});

test('TheCostsByAttribute_MatchTheCanonTable', () => {
  for (const [attribute, one, two, three] of [[A.Attack, 11, 17, 8], [A.Guard, 6, 5, 8], [A.Skill, 5, 2, 4], [A.Stance, 3, 7, 4]]) {
    const counted = CardCatalog.All.filter((c) => c.Attribute === attribute);
    assert.deepEqual([1, 2, 3].map((cost) => counted.filter((c) => c.Cost === cost).length), [one, two, three], String(attribute));
  }
});

test('TheCostOneGuardCards_AreTheSixOfV45', () => {
  assert.deepEqual(
    CardCatalog.All.filter((c) => c.Cost === 1 && c.Attribute === A.Guard).map((c) => c.Id).sort(),
    ['observe', 'back_leap', 'slide_step', 'break_off', 'riposte_stance', 'resolve'].sort());
});

test('TheV45Changes_AreInTheCatalog', () => {
  assert.equal(CardCatalog.KesaCut.Face.Power, 15);
  assert.equal(CardCatalog.BoarRush.Face.Power, 11);
  assert.ok(CardCatalog.BoarRush.Face.ReachOrDefault.equals(new Reach(1, 3)));
  assert.equal(CardCatalog.ThrowBlade.Face.Power, 8);
  assert.ok(CardCatalog.ThrowBlade.Face.ReachOrDefault.equals(new Reach(0, 3)));
  assert.ok(CardCatalog.Thrust.Face.ReachOrDefault.equals(new Reach(0, 2)));
  assert.equal(CardCatalog.Thrust.Face.Power, 23);
  assert.equal(CardCatalog.GaleThrust.Face.Power, 14);
  assert.equal(CardCatalog.Observe.Attribute, A.Guard);
  assert.equal(CardCatalog.Observe.Face.Guard, 3);
  assert.equal(CardCatalog.Observe.Face.Draw, 1);
  assert.equal(CardCatalog.BreakOff.Attribute, A.Guard);
  assert.equal(CardCatalog.BreakOff.Face.Guard, 4);
  assert.equal(CardCatalog.BreakOff.Face.StaminaGain, 0);
  assert.equal(CardCatalog.BreakOff.Trait.Effect, TraitEffect.StaminaGain);
  assert.equal(CardCatalog.Resolve.Attribute, A.Guard);
  assert.equal(CardCatalog.Resolve.Face.Guard, 3);
  assert.equal(CardCatalog.Resolve.Face.StatusList.length, 1);
  assert.ok(CardCatalog.Resolve.Face.StatusList[0].equals(new StatusGrant(StatusKind.Empower, 2, { OnSelf: true })));
  assert.equal(CardCatalog.IronWall.Cost, 2);
  assert.ok(CardCatalog.IronWall.Face.Stance.equals(new StanceDef(StanceHook.CostDiscount, { Attribute: A.Guard })));
});

test('TheV46SideSweep_ReachesTwo_AtTwentyThree', () => {
  const c = CardCatalog.SideSweep;
  assert.equal(c.Face.Power, 23);
  assert.ok(c.Face.ReachOrDefault.equals(new Reach(0, 2)));
  assert.equal(c.Cost, 3);
  assert.equal(c.Attributes, A.Attack);
  assert.equal(c.Trait, null, '素直');
  assert.equal(c.Targets, TargetKind.One);
});

test('TheAttackPowers_AddUpToTheCanonTotal', () => {
  const sum = (list) => list.reduce((s, c) => s + c.Face.Power, 0);
  const attacks = CardCatalog.All.filter((c) => c.Attribute === A.Attack);
  assert.equal(attacks.length, 36);
  assert.equal(sum(attacks), 390);
  assert.equal(sum(CardCatalog.All.slice(0, 40).filter((c) => c.Attribute === A.Attack)), 220);
  assert.equal(sum(CardCatalog.All.slice(40).filter((c) => c.Attribute === A.Attack)), 170);
});

test('EveryAttackFace_ReadsTheBandTable', () => {
  const SingleRows = [[8, 17, 27, 36], [7, 15, 24, 33], [6, 13, 21, 30], [5, 10, 17, 26], [4, 8, 13, 22]];
  const DualRows = [[5, 11, 18, 24], [5, 10, 16, 22], [4, 8, 14, 20], [3, 7, 11, 17], [2, 5, 8, 14]];
  const width = (r) => r.Max - r.Min + 1 + (r.Contains(3) ? 1 : 0);
  const off = [];
  for (const c of CardCatalog.All.filter((x) => x.Attribute === A.Attack)) {
    const single = c.Attributes === A.Attack && !AttributeRule.HasMovement(c.Face);
    const w = width(c.Face.ReachOrDefault);
    const band = single ? SingleRows[w - 1][c.Column - 1] : DualRows[w - 1][c.Column - 1];
    const extra = c.Trait == null || c.Id === 'shadow_lunge' ? 2 : 0;
    const want = c.Id === 'last_stand' ? 8 : band + extra;
    if (c.Face.Power !== want) off.push(`${c.Id}: ${c.Face.Power}, the table says ${want}`);
  }
  assert.deepEqual(off, []);
});

test('TheTraits_MatchTheCanonTally', () => {
  assert.equal(CardCatalog.All.filter((c) => c.Trait != null).length, 72);
  assert.equal(CardCatalog.All.filter((c) => c.ExtraTrait != null).length, 1);
  assert.equal(CardCatalog.LastStand.ExtraTrait.Condition, TraitCondition.Desperate);
  assert.equal(CardCatalog.All.some((c) => c.Trait != null && c.Trait.Condition === TraitCondition.Unguarded), false);
});

test('TheConditions_MatchTheCanonTally / TheEffects_MatchTheCanonTally / TheGapConditions_AreSixteen', () => {
  const conditions = { Combo: 8, Moved: 3, OmenIs: 10, Reserve: 2, Desperate: 1, FirstPlay: 4, Finisher: 3, Broken: 4, Chain: 4, Thin: 2, FoeHas: 9, SelfHas: 6 };
  for (const [condition, count] of Object.entries(conditions)) {
    assert.equal(CardCatalog.All.filter((c) => c.Trait != null && c.Trait.Condition === condition).length, count, condition);
  }
  const effects = { StaminaGain: 8, PowerBonus: 14, GuardBonus: 15, Draw: 9, Status: 6, CostDown: 3, NextTurnRecovery: 3, HeavyBlow: 8, Convert: 2, FollowUp: 4 };
  for (const [effect, count] of Object.entries(effects)) {
    assert.equal(CardCatalog.All.filter((c) => c.Trait != null && c.Trait.Effect === effect).length, count, effect);
  }
  assert.equal(CardCatalog.All.filter((c) => c.Trait != null
    && (c.Trait.Condition === TraitCondition.GapAtMost || c.Trait.Condition === TraitCondition.GapAtLeast)).length, Constants.PositionTraitCards);
});

test('NoConditionAndEffectPair_SitsOnMoreThanThreeCards', () => {
  const pairOf = (t) => {
    let bracket = '';
    if (t.Condition === 'GapAtMost' || t.Condition === 'GapAtLeast' || t.Condition === 'Reserve') bracket = String(t.Threshold);
    else if (t.Condition === 'Combo') bracket = { 1: 'Attack', 2: 'Guard', 4: 'Skill', 8: 'Stance' }[t.Attribute];
    else if (t.Condition === 'OmenIs') bracket = t.Omen ?? '';
    else if (t.Condition === 'FoeHas' || t.Condition === 'SelfHas') bracket = t.Watch ?? '';
    return `${t.Condition}(${bracket}) : ${t.Effect}`;
  };
  const pairs = new Map();
  for (const c of CardCatalog.All) {
    for (const t of c.AllTraits) {
      const k = pairOf(t);
      if (!pairs.has(k)) pairs.set(k, []);
      pairs.get(k).push(c.Id);
    }
  }
  const over = [...pairs].filter(([, v]) => v.length > Constants.SameTraitMax);
  const atTheCap = [...pairs].filter(([, v]) => v.length === Constants.SameTraitMax).map(([k]) => k);
  assert.deepEqual(over, []);
  assert.deepEqual(atTheCap.sort(), ['GapAtLeast(2) : PowerBonus', 'OmenIs(Attack) : GuardBonus']);
  assert.deepEqual(pairs.get('GapAtLeast(2) : PowerBonus').sort(), ['boar_rush', 'last_stand', 'reach_thrust']);
  assert.deepEqual(pairs.get('OmenIs(Attack) : GuardBonus').sort(), ['back_leap', 'bulwark', 'rock_stance']);
});

test('TheSixteenGapCards_FillEachCellWithFour_OnColumnsOneTwoTwoThree', () => {
  const attackEffect = (t) => ['PowerBonus', 'HeavyBlow', 'Convert', 'FollowUp'].includes(t.Effect) || (t.Effect === 'Status' && t.Grant != null && !t.Grant.OnSelf);
  const gapCards = CardCatalog.All.filter((c) => c.Trait != null && (c.Trait.Condition === 'GapAtMost' || c.Trait.Condition === 'GapAtLeast'));
  const rows = [
    [true, true, ['body_check', 'kesa_cut', 'fang_rush', 'abyss_stance']],
    [true, false, ['last_stand', 'reach_thrust', 'blood_dance', 'boar_rush']],
    [false, true, ['wrist_cut', 'shield_bash', 'deflect', 'low_guard']],
    [false, false, ['dash_in', 'throw_blade', 'stone_throw', 'gale_thrust']],
  ];
  for (const [attack, close, cards] of rows) {
    const cell = gapCards.filter((c) => attackEffect(c.Trait) === attack && (c.Trait.Condition === 'GapAtMost') === close)
      .map((c, i) => ({ c, i })).sort((x, y) => (x.c.Column - y.c.Column) || (x.i - y.i)).map((p) => p.c);
    assert.equal(gapCards.length, Constants.PositionTraitCards);
    assert.deepEqual(cell.map((c) => c.Id).sort(), [...cards].sort());
    assert.deepEqual(cell.map((c) => c.Column), [1, 2, 2, 3]);
  }
});

test('TheReaches_MatchTheCanonTable', () => {
  const directed = CardCatalog.All.filter((c) => EnemyAi.IsOpponentDirected(c.Attributes, c.Face, c.Targets));
  const count = (min, max) => directed.filter((c) => c.Face.ReachOrDefault.equals(new Reach(min, max))).length;
  const at = (gap) => directed.filter((c) => c.Face.ReachOrDefault.Contains(gap)).length;
  assert.equal(directed.length, 43);
  assert.equal(CardCatalog.All.filter((c) => c.Targets === TargetKind.Self).length, 37);
  assert.deepEqual([count(0, 0), count(0, 1), count(1, 2), count(0, 2), count(2, 3), count(1, 3), count(0, 3)], [4, 18, 5, 11, 1, 1, 3]);
  assert.deepEqual([at(0), at(1), at(2), at(3)], [36, 38, 21, 5]);
  assert.deepEqual(directed.filter((c) => c.Face.ReachOrDefault.Contains(3)).map((c) => c.Id).sort(),
    ['throw_blade', 'boar_rush', 'stone_throw', 'thorn_shot', 'gale_thrust'].sort());
});

test('TheStanceCards_AreFourteen_AndEachSetsAStance', () => {
  const stances = CardCatalog.All.filter((c) => (c.Attributes & A.Stance) === A.Stance);
  assert.equal(stances.length, 14);
  for (const c of stances) assert.notEqual(c.Face.Stance, null, c.Id);
  assert.ok(CardCatalog.All.filter((c) => (c.Attributes & A.Stance) !== A.Stance).every((c) => c.Face.Stance == null));
});

test('TheFaces_KeepToTheCellLimits_AndTheStatusWordsPointTheCanonWay', () => {
  for (const c of CardCatalog.All) {
    assert.ok(Math.abs(c.Face.Move) <= Constants.MoveStepMax, c.Id);
    assert.ok(Math.abs(c.Face.Push) <= Constants.MoveStepMax, c.Id);
    for (const g of c.Face.StatusList) {
      assert.equal(g.OnSelf, Statuses.IsOwn(g.Kind), `${c.Id} ${g.Kind}`);
      assert.ok(g.Stacks >= 1 && g.Stacks <= 4, c.Id);
    }
  }
  assert.deepEqual(CardCatalog.All.filter((c) => c.Face.Push !== 0).map((c) => c.Id).sort(), ['haul_step', 'shield_push']);
});

test('EveryCard_KeepsTheMultiHitRule', () => {
  assert.deepEqual(CardCatalog.All.filter((c) => !Cards.MultiHitCarriesNoFoeStatus(c)).map((c) => c.Id), []);
});

test('ThePrototypeDeck_StillNamesTheTenSliceCards', () => {
  assert.deepEqual(PrototypeDeck.Kinds.map((c) => c.Id), [
    'thrust', 'kesa_cut', 'reach_thrust', 'brace', 'feint', 'boar_rush', 'body_check', 'step_in_guard', 'step_out_guard', 'shield_bash',
  ]);
  assert.equal(PrototypeDeck.Copies, 2);
  assert.equal(PrototypeDeck.Kinds, PrototypeDeck.Kinds, 'one list');
  const deck = PrototypeDeck.Build();
  assert.equal(deck.length, 20);
  assert.deepEqual(deck.slice(0, 3).map((c) => c.InstanceId), ['thrust-0', 'thrust-1', 'kesa_cut-0']);
  assert.equal(deck[0].Def, CardCatalog.Thrust);
  assert.equal(Cards.Validate(deck).Ok, true);
  assert.notEqual(PrototypeDeck.Build(), deck, 'a fresh list each time');
});

test('The catalog data cannot be changed in place (C# readonly)', () => {
  assert.throws(() => { CardCatalog.All.push(CardCatalog.Thrust); }, TypeError);
  assert.throws(() => { CardCatalog.Focus.Face.StatusList.push(null); }, TypeError);
  assert.throws(() => { InitialPart.Thrust = null; }, TypeError);
});

// ---- Every card against the C# oracle's dump ----

const oracleFile = new URL('../../oracle/out/catalog.json', import.meta.url);
const shapesFile = new URL('../../oracle/out/shapes.json', import.meta.url);
const haveOracle = fs.existsSync(oracleFile) && fs.existsSync(shapesFile);

test('C# oracle: all eighty CardDefs, field by field, in canon order', { skip: haveOracle ? false : 'needs oracle/out/catalog.json' }, async () => {
  const { toOracleJson } = await import('../parity/lib/serialize.mjs');
  const shapes = JSON.parse(fs.readFileSync(shapesFile, 'utf8'));
  const cases = JSON.parse(fs.readFileSync(oracleFile, 'utf8')).filter((c) => c.id.startsWith('card:'));
  assert.equal(cases.length, 80);
  assert.deepEqual(cases.map((c) => c.input.Id), CardCatalog.All.map((c) => c.Id));
  for (const c of cases) {
    const def = CardCatalog.ById(c.input.Id);
    assert.deepEqual(toOracleJson(def, shapes), c.output.Def, c.id);
    assert.equal(Cards.IsStanceCard(def), c.output.IsStanceCard, c.id);
    assert.equal(Cards.StanceStandsAlone(def), c.output.StanceStandsAlone, c.id);
    assert.equal(Cards.MultiHitCarriesNoFoeStatus(def), c.output.MultiHitCarriesNoFoeStatus, c.id);
  }
});

// ---- Every card, played (needs TurnLoop.js, which needs Mastery.js and Enemies.js) ----

const loop = await late(import.meta.url, ['../../src/core/battle/TurnLoop.js', '../../src/core/battle/Enemies.js']);

test('EveryCard_CanBePlayedOnSomeBoard_AndNothingThrows', { skip: loop.skip }, () => {
  const { TurnLoop, BattleSetup, PlayRefusal, Enemies } = loop.mods;
  const NoShuffle = new FixedRng(0.9999999);
  const neverPlayed = [];
  for (const c of CardCatalog.All) {
    let played = false;
    for (const gap of [0, 1, 2, 3]) {
      const deck = [new CardInstance(`${c.Id}-0`, c)];
      for (let i = 0; i < 19; i++) deck.push(new CardInstance(`filler-${i}`, CardCatalog.Brace));
      const setup = new BattleSetup(Enemies.PolearmWarped, deck, BattleSetup.SliceFieldCells, { StartGap: gap });
      let state = TurnLoop.Start(setup, NoShuffle).State;
      state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
      if (TurnLoop.CanPlay(state, `${c.Id}-0`) !== PlayRefusal.None) continue;
      played = true;
      assert.doesNotThrow(() => {
        let after = TurnLoop.PlayCard(state, `${c.Id}-0`, NoShuffle).State;
        for (let turn = 0; turn < 3 && after.Result === GameResult.Ongoing; turn++) {
          after = TurnLoop.EndTurn(after, NoShuffle).State;
          if (after.Result !== GameResult.Ongoing) break;
          after = TurnLoop.BeginPlayerTurn(after, NoShuffle).State;
        }
      }, `${c.Id} at gap ${gap}`);
    }
    if (!played) neverPlayed.push(c.Id);
  }
  assert.deepEqual(neverPlayed, [], 'cards no opening gap lets the player release');
});

test('RandomDecksOfTheEighty_FightThePolearmToTheEnd', { skip: loop.skip }, () => {
  const { TurnLoop, BattleSetup, PlayRefusal, Enemies } = loop.mods;
  const RandomDeck = (rng, size) => {
    const counts = new Map();
    const deck = [];
    while (deck.length < size) {
      const def = CardCatalog.All[Math.trunc(rng.NextDouble() * CardCatalog.All.length) % CardCatalog.All.length];
      const held = counts.get(def.Id) ?? 0;
      if (held >= Constants.CopiesMax) continue;
      counts.set(def.Id, held + 1);
      deck.push(new CardInstance(`${def.Id}-${held}`, def));
    }
    return deck;
  };
  for (let seed = 1; seed <= 30; seed++) {
    const rng = new SeededRng(seed);
    const deck = RandomDeck(rng, 20 + (seed % 21));
    assert.equal(Cards.Validate(deck).Ok, true);
    let state = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, BattleSetup.SliceFieldCells), rng).State;
    let turns = 0;
    while (state.Result === GameResult.Ongoing && turns < 60) {
      state = TurnLoop.BeginPlayerTurn(state, rng).State;
      turns++;
      while (state.Result === GameResult.Ongoing) {
        const next = state.Hand.find((c) => TurnLoop.CanPlay(state, c.InstanceId) === PlayRefusal.None);
        if (next == null) break;
        state = TurnLoop.PlayCard(state, next.InstanceId, rng).State;
      }
      if (state.Result === GameResult.Ongoing) state = TurnLoop.EndTurn(state, rng).State;
    }
    assert.notEqual(state.Result, GameResult.Ongoing, `seed ${seed} did not end in 60 turns`);
    assert.equal(state.Hand.length + state.DrawPile.length + state.DiscardPile.length + state.Exiled.length, deck.length, `seed ${seed}`);
  }
});
