// Tests for src/core/battle/EnemyAi.js — ported from unity-port/BattleCore.Tests:
// PolearmTests (the EnemyAi half), SwiftAndFocusTests (FreeStepDirection), FloorReadTests
// (BoundaryOf / BoundaryCells), OmenPlanTests (BranchFor), PriestAwayTests (Barred),
// EnemyTreeReachTests (the whole tree walk), BossSwitchTests (ActiveSwitch / BranchFor of every boss),
// plus fixture-only cases of each member.

import { describe as nodeDescribe, test } from 'node:test';
import assert from 'node:assert/strict';

import { ArgumentOutOfRangeException, KeyNotFoundException, compareOrdinal } from '../../src/shared/cs.js';
import { loadDeps, fixturesSkip, EnemyAction, Combatant, SS, eq } from './_turnloop_fixtures.mjs';

// The battle modules load dynamically: while a sibling module is not written yet, the suites that
// need it report as skipped with the missing module named (see _turnloop_fixtures.mjs).
const B = '../../src/core/battle/';
const base = await loadDeps(import.meta.url, [B + 'Types.js', B + 'Combat.js', B + 'Statuses.js', B + 'EnemyAi.js']);
const roster = base.skip ? base : await loadDeps(import.meta.url, [B + 'Traits.js', B + 'Enemies.js']);
const BASE_SKIP = base.skip || fixturesSkip;
const ROSTER_SKIP = BASE_SKIP || roster.skip;
/** describe, skipped with the reason while a module it needs is missing. */
const describeWith = (skip) => (name, fn) => (skip ? nodeDescribe(name, { skip }, fn) : nodeDescribe(name, fn));
const describe = describeWith(BASE_SKIP);
const describeRoster = describeWith(ROSTER_SKIP);

const {
  BattleAttribute, TargetKind, OmenKind, StatusKind, TraitCondition, TraitEffect, EnemyRank, Face, Reach, Trait,
  StatusGrant, StanceDef, StanceHook, OmenLabel, Omen, GapBoundary, EnemyActionDef, EnemyDef, TreeSwitch,
  AdaptationView, BattleHistory, PlayerTurnEnd, CombatantState, Combat, StatusSet, EnemyAi,
} = base.mods ?? {};
const { Traits, TraitContext, Enemies } = roster.mods ?? {};

const A = BattleAttribute;

// ======================================================================== fixture-only cases

describe('EnemyAi (fixtures)', () => {
  const hit = EnemyAction('hit', { column: 2, face: new Face({ Power: 6, Reach: new Reach(0, 1) }) });
  const jab = EnemyAction('jab', { face: new Face({ Power: 3, Reach: new Reach(0, 2) }) });
  const shell = EnemyAction('shell', { face: new Face({ Guard: 3 }), attributes: A.Guard, targets: TargetKind.Self });
  const step = EnemyAction('step', { face: new Face({ Move: 1 }), attributes: A.None, targets: TargetKind.Self });
  const rite = EnemyAction('rite', { face: new Face({ Reach: new Reach(0, 4), Statuses: [new StatusGrant(StatusKind.MiasmaShroud, 1, false, 1)] }), attributes: A.Skill });
  const bind = new EnemyActionDef('bind', 'bind', A.Skill, 1, new Face({ Reach: new Reach(0, 4), Statuses: [new StatusGrant(StatusKind.Binding, 2)] }),
    null, TargetKind.One, '', null, StatusKind.Binding);
  const capped = new EnemyActionDef('capped', 'capped', A.Skill, 1, rite.Face, null, TargetKind.One, '', null, null, null, true);
  const actions = new Map([[hit.Id, hit], [jab.Id, jab], [shell.Id, shell], [step.Id, step], [bind.Id, bind], [capped.Id, capped]]);
  const def = new EnemyDef('fx', 'fx', 60, 10, 2, 1, ['hit', 'jab', 'shell'], ['bind', 'jab'], ['step', 'shell'], actions, EnemyRank.Elite, 2);

  test('ChooseAction walks the branch top-down and takes the first it can pay for', () => {
    assert.equal(EnemyAi.ChooseAction(def, 0, 10).Id, 'hit');
    assert.equal(EnemyAi.ChooseAction(def, 0, 1).Id, 'jab');
    assert.equal(EnemyAi.ChooseAction(def, 0, 0), null);
    assert.equal(EnemyAi.ChooseAction(def, 0, 10, ['hit']).Id, 'jab', 'skip');
    assert.equal(EnemyAi.ChooseAction(def, 2, 10).Id, 'bind');
    assert.equal(EnemyAi.ChooseAction(def, 7, 10).Id, 'step');
  });

  test('BranchFor: the band, a switch branch, and a negative gap', () => {
    const sw = new TreeSwitch('s', () => true, { BranchAtGapZero: ['shell'] });
    assert.deepEqual([...EnemyAi.BranchFor(def, 0)], ['hit', 'jab', 'shell']);
    assert.deepEqual([...EnemyAi.BranchFor(def, 0, sw)], ['shell']);
    assert.deepEqual([...EnemyAi.BranchFor(def, 1, sw)], ['bind', 'jab'], 'a band the switch leaves null keeps the base');
    assert.throws(() => EnemyAi.BranchFor(def, -1), ArgumentOutOfRangeException);
    assert.throws(() => EnemyAi.BranchFor(null, 0), { name: 'ArgumentNullException', message: "Value cannot be null. (Parameter 'def')" });
  });

  test('ActiveSwitch: the last that holds wins; SwitchById', () => {
    const a = new TreeSwitch('a', (v) => v.Hp < 50);
    const b = new TreeSwitch('b', (v) => v.Hp < 30);
    const boss = def.with({ Switches: [a, b] });
    const view = (hp) => new AdaptationView(1, hp, 60, [], new BattleHistory());
    assert.equal(EnemyAi.ActiveSwitch(boss, view(55)), null);
    assert.equal(EnemyAi.ActiveSwitch(boss, view(40)).Id, 'a');
    assert.equal(EnemyAi.ActiveSwitch(boss, view(20)).Id, 'b');
    assert.equal(EnemyAi.ActiveSwitch(def, view(20)), null);
    assert.equal(EnemyAi.SwitchById(boss, 'b'), b);
    assert.equal(EnemyAi.SwitchById(boss, null), null);
    assert.equal(EnemyAi.SwitchById(boss, 'z'), null);
    assert.equal(EnemyAi.SwitchById(def, 'a'), null);
  });

  test('ActionOf: the override under the switch, else the own; an unknown id throws KeyNotFoundException', () => {
    const strong = EnemyAction('hit', { column: 2, face: new Face({ Power: 9 }) });
    const sw = new TreeSwitch('s', () => true, { Overrides: [strong] });
    assert.equal(EnemyAi.ActionOf(def, 'hit'), hit);
    assert.equal(EnemyAi.ActionOf(def, 'hit', sw), strong);
    assert.equal(EnemyAi.ActionOf(def, 'jab', sw), jab);
    assert.throws(() => EnemyAi.ActionOf(def, 'nothing'), KeyNotFoundException);
  });

  test('StaminaAtAction: now plus the recovery, 疲労 and the bonus counted', () => {
    const body = (stamina, statuses = StatusSet.Empty, bonus = 0) => new CombatantState(60, 60, stamina, 10, 0, 6, 1, statuses, bonus);
    assert.equal(EnemyAi.StaminaAtAction(def, body(0)), 2);
    assert.equal(EnemyAi.StaminaAtAction(def, body(0, SS([StatusKind.Fatigue, 1]))), 1);
    assert.equal(EnemyAi.StaminaAtAction(def, body(0, StatusSet.Empty, 1)), 3);
    assert.equal(EnemyAi.StaminaAtAction(def, body(9)), 10);
    assert.equal(EnemyAi.StaminaAtAction(def, body(10)), 10);
  });

  test('Barred: NotWhileFoeHas reads the word as it stands after the next tick; NotAtFoeCap reads the cap', () => {
    assert.deepEqual([...EnemyAi.Barred(def, StatusSet.Empty, true)], []);
    assert.deepEqual([...EnemyAi.Barred(def, SS([StatusKind.Binding, 1]), true)], [], 'one stack is gone by then');
    assert.deepEqual([...EnemyAi.Barred(def, SS([StatusKind.Binding, 1]), false)], ['bind']);
    assert.deepEqual([...EnemyAi.Barred(def, SS([StatusKind.Binding, 2]), true)], ['bind']);
    assert.deepEqual([...EnemyAi.Barred(def, SS([StatusKind.MiasmaShroud, 1]), true)], ['capped']);
    assert.deepEqual([...EnemyAi.Barred(def, StatusSet.Empty, true, null, SS([StatusKind.MiasmaShroud, 1]))], ['capped'], 'warded counts as held');
  });

  test('DecideOmen / OmenOf / DecidePlan', () => {
    eq(EnemyAi.DecideOmen(def, 0, 10), new Omen('hit', new OmenLabel(OmenKind.Attack, new Reach(0, 1))));
    eq(EnemyAi.DecideOmen(def, 0, 0), new Omen('rest', new OmenLabel(OmenKind.Rest)));
    assert.equal(EnemyAi.OmenOf(null).Label, EnemyAi.RestLabel, 'the one static RestLabel');
    const first = EnemyAi.DecideOmen(def, 0, 10);
    eq(EnemyAi.DecidePlan(def, 0, 10, first), new Omen('jab', new OmenLabel(OmenKind.Attack, new Reach(0, 2))));
    eq(EnemyAi.DecidePlan(def, 0, 2, first), EnemyAi.OmenOf(null), 'the first cost 2 of 2');
    assert.equal(EnemyAi.DecidePlan(def, 0, 10, EnemyAi.OmenOf(null)), null, 'nothing follows a rest');
    assert.equal(EnemyAi.DecidePlan(def.with({ ActionsPerPhase: 1 }), 0, 10, first), null);
  });

  test('ActionToExecute: the omen as shown, or a rest', () => {
    const omen = EnemyAi.DecideOmen(def, 0, 10);
    assert.equal(EnemyAi.ActionToExecute(def, omen, 2).Id, 'hit');
    assert.equal(EnemyAi.ActionToExecute(def, omen, 1), null);
    assert.equal(EnemyAi.ActionToExecute(def, EnemyAi.OmenOf(null), 10), null);
    assert.throws(() => EnemyAi.ActionToExecute(def, null, 10), { name: 'ArgumentNullException', message: "Value cannot be null. (Parameter 'omen')" });
  });

  test('FaceAt: PullBeyond turns a push into a pull beyond the gap', () => {
    const creeping = new EnemyActionDef('creep', 'creep', A.Attack, 1, new Face({ Power: 2, Push: 1, Reach: new Reach(0, 4) }),
      null, TargetKind.One, '', null, null, 1);
    assert.equal(EnemyAi.FaceAt(creeping, 0).Push, 1);
    assert.equal(EnemyAi.FaceAt(creeping, 1).Push, 1);
    assert.equal(EnemyAi.FaceAt(creeping, 2).Push, -1);
    assert.equal(EnemyAi.FaceAt(hit, 3), hit.Face, 'every other action keeps its own face');
  });

  for (const [gap, min, max, expected] of [[3, 0, 1, 1], [0, 2, 3, -1], [2, 1, 2, 0]]) {
    test(`FreeStepDirection_ServesTheOmensReach(${gap},${min},${max})`, () => {
      const action = EnemyAction('blow', { face: new Face({ Power: 5, Reach: new Reach(min, max) }) });
      assert.equal(EnemyAi.FreeStepDirection(action, gap), expected);
    });
  }

  test('FreeStepDirection_Stays_ForAnOmenAimedAtNobody_AndForARest', () => {
    const Wait = EnemyAction('wait', { face: new Face(), attributes: A.Guard, targets: TargetKind.Self });
    assert.equal(EnemyAi.FreeStepDirection(Wait, 5), 0);
    assert.equal(EnemyAi.FreeStepDirection(null, 5), 0);
  });

  test('IsOpponentDirected', () => {
    assert.equal(EnemyAi.IsOpponentDirected(A.Attack, new Face(), TargetKind.One), true);
    assert.equal(EnemyAi.IsOpponentDirected(A.Attack, new Face(), TargetKind.Self), false);
    assert.equal(EnemyAi.IsOpponentDirected(A.None, new Face({ Push: -1 }), TargetKind.One), true);
    assert.equal(EnemyAi.IsOpponentDirected(A.None, new Face({ Break: 1 }), TargetKind.All), true);
    assert.equal(EnemyAi.IsOpponentDirected(A.Skill, new Face({ Statuses: [new StatusGrant(StatusKind.Slow, 2)] }), TargetKind.One), true);
    assert.equal(EnemyAi.IsOpponentDirected(A.Skill, new Face({ Statuses: [new StatusGrant(StatusKind.Regen, 2, true)] }), TargetKind.One), false);
    assert.equal(EnemyAi.IsOpponentDirected(A.Guard, new Face({ Guard: 3 }), TargetKind.One), false);
  });

  test('LabelOf: Attack, Move, Guard, Skill, Stance; the roster column wins; the reach only when aimed', () => {
    eq(EnemyAi.LabelOf(hit), new OmenLabel(OmenKind.Attack, new Reach(0, 1)));
    eq(EnemyAi.LabelOf(step), new OmenLabel(OmenKind.Move));
    eq(EnemyAi.LabelOf(shell), new OmenLabel(OmenKind.Guard));
    eq(EnemyAi.LabelOf(EnemyAction('mend', { face: new Face({ Heal: 3 }), attributes: A.Skill, targets: TargetKind.Self })), new OmenLabel(OmenKind.Skill));
    eq(EnemyAi.LabelOf(EnemyAction('st', { face: new Face({ Stance: new StanceDef(StanceHook.TurnStart, { Guard: 1 }) }), attributes: A.Stance, targets: TargetKind.Self })),
      new OmenLabel(OmenKind.Stance));
    const advanceGuard = new EnemyActionDef('ag', 'ag', A.Guard, 1, new Face({ Move: 1, Guard: 2 }), null, TargetKind.Self, '', OmenKind.Guard);
    eq(EnemyAi.LabelOf(advanceGuard), new OmenLabel(OmenKind.Guard));
    eq(EnemyAi.LabelOf(EnemyAction('push', { face: new Face({ Push: 2 }), attributes: A.None })), new OmenLabel(OmenKind.Move, Reach.Default));
  });

  test('TargetCells and BoundaryCells count from the near edge', () => {
    const sweep = EnemyAction('sweep', { column: 2, face: new Face({ Power: 9, Reach: new Reach(1, 2) }),
      trait: new Trait(TraitCondition.GapAtLeast, TraitEffect.PowerBonus, 3, { Threshold: 2 }) });
    const enemy = Combatant({ hp: 60, cell: 5 });
    assert.deepEqual([...EnemyAi.TargetCells(EnemyAi.LabelOf(sweep), enemy)], [2, 3]);
    assert.deepEqual([...EnemyAi.TargetCells(EnemyAi.LabelOf(sweep), enemy.with({ Cell: 3 }))], [1]);
    assert.deepEqual([...EnemyAi.TargetCells(EnemyAi.LabelOf(shell), enemy)], []);
    eq(EnemyAi.BoundaryOf(sweep), new GapBoundary(true, 2));
    assert.equal(EnemyAi.BoundaryOf(hit), null);
    assert.equal(EnemyAi.BoundaryOf(EnemyAction('u', { trait: new Trait(TraitCondition.Unguarded, TraitEffect.PowerBonus, 3) })), null);
    assert.deepEqual([...EnemyAi.BoundaryCells(sweep, Combatant({ cell: 6 }))], [3]);
    assert.deepEqual([...EnemyAi.BoundaryCells(sweep, Combatant({ cell: 4 }))], [1]);
    assert.deepEqual([...EnemyAi.BoundaryCells(sweep, Combatant({ cell: 3 }))], []);
  });

  test('ToText: the screen words, byte for byte', () => {
    assert.equal(EnemyAi.ToText(new OmenLabel(OmenKind.Attack, new Reach(1, 2))), '攻撃・1〜2');
    assert.equal(EnemyAi.ToText(new OmenLabel(OmenKind.Attack, Reach.Only(0))), '攻撃・0');
    assert.equal(EnemyAi.ToText(new OmenLabel(OmenKind.Guard)), '防御');
    assert.equal(EnemyAi.ToText(new OmenLabel(OmenKind.Move)), '移動');
    assert.equal(EnemyAi.ToText(new OmenLabel(OmenKind.Skill, new Reach(0, 4))), '技・0〜4');
    assert.equal(EnemyAi.ToText(new OmenLabel(OmenKind.Stance)), '構え');
    assert.equal(EnemyAi.ToText(EnemyAi.RestLabel), '休み');
    assert.throws(() => EnemyAi.ToText(new OmenLabel('Nope')), ArgumentOutOfRangeException);
  });
});

// ======================================================================== PolearmTests (EnemyAi)

describeRoster('PolearmTests', () => {
  const Polearm = () => Enemies.PolearmWarped;
  const act = (id) => Polearm().Actions.get(id);

  for (const [id, column, power, guard, push, move, reach, omen] of [
    ['sweep', 2, 9, 0, 0, 0, '1〜2', '攻撃・1〜2'],
    ['shove', 2, 6, 0, 2, 0, '0', '攻撃・0'],
    ['reach_thrust', 1, 5, 0, 0, 0, '0〜2', '攻撃・0〜2'],
    ['guard_up', 1, 0, 3, 0, 0, '', '防御'],
    ['step_forward', 1, 0, 2, 0, 1, '', '移動'],
  ]) {
    test(`Actions_MatchTheRosterTable(${id})`, () => {
      const action = act(id);
      const label = EnemyAi.LabelOf(action);
      assert.equal(action.Column, column);
      assert.equal(action.Cost, column);
      assert.equal(action.Face.Power, power);
      assert.equal(action.Face.Guard, guard);
      assert.equal(action.Face.Push, push);
      assert.equal(action.Face.Move, move);
      assert.equal(label.Reach == null ? '' : label.Reach.ToText(), reach);
      assert.equal(EnemyAi.ToText(label), omen);
    });
  }

  test('TheSelfActions_ReadNoReach', () => {
    assert.equal(EnemyAi.IsOpponentDirected(act('step_forward').Attributes, act('step_forward').Face, TargetKind.Self), false);
  });

  for (const [gap, first] of [[0, 'shove'], [1, 'sweep'], [2, 'sweep'], [3, 'step_forward'], [5, 'step_forward']]) {
    test(`TheBranch_FollowsTheGapBand(${gap})`, () => {
      assert.equal(EnemyAi.ChooseAction(Polearm(), gap, 10).Id, first);
    });
  }

  test('Adjacent_WithOneStamina_FallsToTheGuard', () => {
    assert.equal(EnemyAi.ChooseAction(Polearm(), 0, 1).Id, 'guard_up');
  });

  test('AtGapTwo_WithOneStamina_FallsToTheThrust', () => {
    assert.equal(EnemyAi.ChooseAction(Polearm(), 2, 1).Id, 'reach_thrust');
  });

  for (const gap of [0, 2, 4]) {
    test(`WithNoStamina_NothingFits_AndTheOmenIsARest(${gap})`, () => {
      assert.equal(EnemyAi.ChooseAction(Polearm(), gap, 0), null);
      const omen = EnemyAi.DecideOmen(Polearm(), gap, 0);
      assert.equal(omen.ActionId, EnemyAi.RestActionId);
      assert.equal(EnemyAi.ToText(omen.Label), '休み');
    });
  }

  test('ExactlyTwoStamina_StillPaysForTheColumnTwoAction', () => {
    assert.equal(EnemyAi.ChooseAction(Polearm(), 2, 2).Id, 'sweep');
  });

  test('TheOmen_IsTheNextActionsLabel', () => {
    eq(EnemyAi.DecideOmen(Polearm(), 2, 10), new Omen('sweep', new OmenLabel(OmenKind.Attack, new Reach(1, 2))));
    eq(EnemyAi.DecideOmen(Polearm(), 0, 10), new Omen('shove', new OmenLabel(OmenKind.Attack, Reach.Only(0))));
    assert.equal(EnemyAi.ToText(EnemyAi.DecideOmen(Polearm(), 2, 1).Label), '攻撃・0〜2');
    assert.equal(EnemyAi.ToText(EnemyAi.DecideOmen(Polearm(), 0, 1).Label), '防御');
    assert.equal(EnemyAi.ToText(EnemyAi.DecideOmen(Polearm(), 3, 10).Label), '移動');
  });

  test('TheAimedCells_AreCountedFromTheEnemysNearEdge', () => {
    const enemy = Combatant({ hp: 60, cell: 5 });
    assert.deepEqual([...EnemyAi.TargetCells(EnemyAi.LabelOf(act('sweep')), enemy)], [2, 3]);
    assert.deepEqual([...EnemyAi.TargetCells(EnemyAi.LabelOf(act('shove')), enemy)], [4]);
    assert.deepEqual([...EnemyAi.TargetCells(EnemyAi.LabelOf(act('guard_up')), enemy)], []);
    assert.deepEqual([...EnemyAi.TargetCells(EnemyAi.LabelOf(act('sweep')), enemy.with({ Cell: 3 }))], [1]);
  });

  test('TheOmenIsACommitment_AnUnpayableOmenBecomesARest_NotACheaperAction', () => {
    const omen = EnemyAi.DecideOmen(Polearm(), 2, 10);
    assert.equal(EnemyAi.ActionToExecute(Polearm(), omen, 2).Id, 'sweep');
    assert.equal(EnemyAi.ActionToExecute(Polearm(), omen, 1), null);
  });

  test('TheOmenIsACommitment_ItDoesNotFollowThePlayerAcrossTheLine', () => {
    const omen = EnemyAi.DecideOmen(Polearm(), 2, 10);
    assert.equal(EnemyAi.ActionToExecute(Polearm(), omen, 10).Id, 'sweep');
  });

  test('ANegativeGap_IsRefused', () => {
    assert.throws(() => EnemyAi.BranchFor(Polearm(), -1), ArgumentOutOfRangeException);
  });

  const RawPower = (action, gap, guard, staminaBefore) => {
    const outcome = Traits.Evaluate(action.Trait, new TraitContext({ Gap: gap, OpponentGuard: guard, StaminaAfterUse: staminaBefore - action.Cost }));
    return Combat.ComputeRawPower(action.Face.Power, outcome.PowerBonus);
  };

  test('StayingAtGapTwo_EatsTheSweepAtTwelve_EveryTurn', () => {
    for (let turn = 0; turn < 3; turn++) {
      const action = EnemyAi.ChooseAction(Polearm(), 2, 10);
      assert.equal(action.Id, 'sweep');
      assert.equal(RawPower(action, 2, 0, 10), 12);
    }
  });

  test('SteppingAdjacent_GetsShovedBackTwo_AndTheNextOmenIsTheSweep', () => {
    const shove = EnemyAi.ChooseAction(Polearm(), 0, 10);
    assert.equal(shove.Id, 'shove');
    assert.equal(RawPower(shove, 0, 0, 10), 9);
    const next = EnemyAi.DecideOmen(Polearm(), 2, 10);
    assert.equal(next.ActionId, 'sweep');
    assert.equal(EnemyAi.ToText(next.Label), '攻撃・1〜2');
  });
});

// ======================================================================== FloorReadTests (EnemyAi)

describeRoster('FloorReadTests (boundaries)', () => {
  test('TheBoundary_IsTheGapTraitOfTheAction', () => {
    eq(EnemyAi.BoundaryOf(Enemies.PolearmWarped.Actions.get('sweep')), new GapBoundary(true, 2));
    eq(EnemyAi.BoundaryOf(Enemies.ArmoredWarden.Actions.get('helm_splitter')), new GapBoundary(false, 0));
    assert.equal(EnemyAi.BoundaryOf(Enemies.PolearmWarped.Actions.get('shove')), null);
    assert.equal(EnemyAi.BoundaryOf(Enemies.PolearmWarped.Actions.get('reach_thrust')), null);
  });

  test('TheBoundaryCells_FollowTheEnemyCell', () => {
    const sweep = Enemies.PolearmWarped.Actions.get('sweep');
    const body = Combatant({ cell: 6 });
    assert.deepEqual([...EnemyAi.BoundaryCells(sweep, body)], [3]);
    assert.deepEqual([...EnemyAi.BoundaryCells(sweep, body.with({ Cell: 4 }))], [1]);
    assert.deepEqual([...EnemyAi.BoundaryCells(sweep, body.with({ Cell: 3 }))], []);
  });

  test('AnAtMostBoundary_ThickensTheAdjacentCell', () => {
    assert.deepEqual([...EnemyAi.BoundaryCells(Enemies.MiasmaPriest.Actions.get('staff_strike'), Combatant({ cell: 6 }))], [5]);
  });
});

// ======================================================================== PriestAwayTests (Barred)

describeRoster('PriestAwayTests (Barred)', () => {
  const Priest = () => Enemies.MiasmaPriest;
  const Switch = (id) => Priest().Switches.find((s) => s.Id === id);
  const Shroud = (n) => SS([StatusKind.MiasmaShroud, n]);
  const has = (list, id) => [...list].includes(id);

  test('TheRite_IsBarred_WhileTheShroudIsAtTheCapOfTheSwitch', () => {
    const stage = Switch('second_stage');
    const stageBreaker = Switch('second_stage+breaker');
    assert.equal(has(EnemyAi.Barred(Priest(), StatusSet.Empty, true), 'miasma_rite'), false);
    assert.equal(has(EnemyAi.Barred(Priest(), Shroud(1), true), 'miasma_rite'), true);
    assert.equal(has(EnemyAi.Barred(Priest(), Shroud(1), false), 'miasma_rite'), true);
    assert.equal(has(EnemyAi.Barred(Priest(), Shroud(1), true, stage), 'miasma_rite'), false);
    assert.equal(has(EnemyAi.Barred(Priest(), Shroud(2), true, stage), 'miasma_rite'), true);
    assert.equal(has(EnemyAi.Barred(Priest(), Shroud(1), false, stageBreaker), 'miasma_rite'), false);
    assert.equal(has(EnemyAi.Barred(Priest(), Shroud(2), false, stageBreaker), 'miasma_rite'), true);
  });

  test('TheRite_ReadsTheShroudAsItWouldSitWithoutTheCharm', () => {
    const warded = Shroud(1);
    assert.equal(has(EnemyAi.Barred(Priest(), StatusSet.Empty, true, null, warded), 'miasma_rite'), true);
    assert.equal(has(EnemyAi.Barred(Priest(), StatusSet.Empty, true, Switch('second_stage'), warded), 'miasma_rite'), false);
    assert.equal(has(EnemyAi.Barred(Priest(), Shroud(1), true, Switch('second_stage'), warded), 'miasma_rite'), true);
  });

  test('TheCapBar_IsOnlyWhereTheRosterSaysIt', () => {
    const angler = Enemies.AbyssAngler;
    assert.equal(angler.Actions.get('fathom_call').NotAtFoeCap, false);
    assert.deepEqual([...EnemyAi.Barred(angler, SS([StatusKind.Depths, 2]), true)], []);
    assert.deepEqual([...Priest().Actions.values()].filter((a) => a.NotAtFoeCap).map((a) => a.Id), ['miasma_rite']);
  });
});

// ======================================================================== BossSwitchTests (EnemyAi)

describeRoster('BossSwitchTests (EnemyAi)', () => {
  function History(...turns) {
    let history = new BattleHistory();
    for (const t of turns) {
      for (const card of t.cards ?? []) history = history.WithCard(card);
      history = history.WithTurnEnd(new PlayerTurnEnd(t.guard ?? 0, t.gap ?? 3, t.moved ?? false, t.cell ?? 2, (t.cards ?? []).length, t.lastMove ?? 0));
    }
    return history;
  }
  const View = (boss, history, { hp = null, gap = 3, turn = null, held = [] } = {}) =>
    new AdaptationView(turn ?? history.TurnEnds.length, hp ?? boss.MaxHp, boss.MaxHp, [], history, gap, held);
  const Active = (boss, view) => EnemyAi.ActiveSwitch(boss, view)?.Id ?? null;

  test('Angler_Bait_AfterTwoTurnsAtTwoOrMore_HooksAndReels_UntilAdjacent', () => {
    const angler = Enemies.AbyssAngler;
    const far = History({ gap: 2 }, { gap: 4 });
    assert.equal(Active(angler, View(angler, far, { gap: 3 })), 'bait');
    assert.equal(Active(angler, View(angler, far, { gap: 0 })), null);
  });

  test('Angler_Snap_EverySecondHookTheyShake_SlacksThenCalls_ForOnePhase', () => {
    const angler = Enemies.AbyssAngler;
    const After = (...snags) => {
      let history = History({ gap: 1 }, { gap: 1 }, { gap: 1 }, { gap: 1 });
      for (const turn of snags) history = history.WithHookSnag(turn);
      return View(angler, history, { turn: 4, gap: 1 });
    };
    assert.equal(Active(angler, After(3, 4)), 'snap');
    assert.equal(Active(angler, After(4)), null);
    assert.equal(Active(angler, After(2, 3)), null);
  });

  test('Angler_Pin_AfterTwoTurnsAdjacent_DrownsThenWhips', () => {
    const angler = Enemies.AbyssAngler;
    assert.equal(Active(angler, View(angler, History({ gap: 0 }, { gap: 0 }), { gap: 0 })), 'pin');
  });

  test('EveryBossSwitch_NamesOnlyTheBossesOwnActions_AndEndsEachBranchOnSomethingCheap', () => {
    for (const boss of Enemies.All.filter((e) => e.Rank === EnemyRank.Boss)) {
      assert.ok(boss.Switches != null && boss.Switches.length > 0, boss.Id);
      for (const each of boss.Switches) {
        for (const gap of [0, 1, 3]) {
          const branch = EnemyAi.BranchFor(boss, gap, each);
          assert.ok(branch.length > 0, `${boss.Id}/${each.Id}`);
          assert.ok(branch.every((id) => boss.Actions.has(id)), `${boss.Id}/${each.Id}`);
          assert.ok(EnemyAi.ActionOf(boss, branch[branch.length - 1], each).Column <= 2, `${boss.Id}/${each.Id} ${gap}`);
        }
      }
    }
    for (const other of Enemies.All.filter((e) => e.Rank !== EnemyRank.Boss)) assert.equal(other.Switches, null, other.Id);
  });
});

// ======================================================================== EnemyTreeReachTests

describeRoster('EnemyTreeReachTests', () => {
  const GapMax = 8;
  const NeverChosen = new Map();

  const Body = (def, stamina, fatigue, bonus) => new CombatantState(
    def.MaxHp, def.MaxHp, stamina, def.MaxStamina, 0, 6, def.Size,
    fatigue ? SS([StatusKind.Fatigue, 1]) : StatusSet.Empty, bonus);

  function StaminasAtAction(def) {
    const staminas = new Set();
    for (let now = 0; now <= def.MaxStamina; now++) {
      for (const fatigue of [false, true]) {
        for (const bonus of [0, 1]) staminas.add(EnemyAi.StaminaAtAction(def, Body(def, now, fatigue, bonus)));
      }
    }
    return [...staminas].sort((a, b) => a - b);
  }

  function* SpentStanceSets(def) {
    const stances = [...def.Actions.values()].filter((a) => a.Face.Stance != null).map((a) => a.Id).sort(compareOrdinal);
    for (let mask = 0; mask < 1 << stances.length; mask++) yield stances.filter((_, i) => (mask & (1 << i)) !== 0);
  }

  function* FoeWords(def) {
    yield StatusSet.Empty;
    const waited = [...def.Actions.values()].filter((a) => a.NotWhileFoeHas != null).map((a) => a.NotWhileFoeHas);
    const capped = [...def.Actions.values()].filter((a) => a.NotAtFoeCap)
      .flatMap((a) => a.Face.StatusList.filter((g) => !g.OnSelf && g.Cap > 0).map((g) => g.Kind));
    for (const word of [...new Set([...waited, ...capped])]) {
      yield SS([word, 1]);
      yield SS([word, 2]);
    }
  }

  function ChosenSomewhere(def) {
    const chosen = new Map();
    const Saw = (action, scene) => { if (action != null && !chosen.has(action.Id)) chosen.set(action.Id, scene); };
    const switches = [null, ...(def.Switches ?? [])];
    for (const active of switches) {
      for (const spent of SpentStanceSets(def)) {
        for (const foe of FoeWords(def)) {
          for (let gap = 0; gap <= GapMax; gap++) {
            for (const stamina of StaminasAtAction(def)) {
              const scene = `switch ${active?.Id ?? 'base'}, gap ${gap}, stamina ${stamina}`;
              const skip = [...spent, ...EnemyAi.Barred(def, foe, true, active)];
              const omen = EnemyAi.DecideOmen(def, gap, stamina, skip, active);
              if (omen.ActionId === EnemyAi.RestActionId) continue;
              const first = EnemyAi.ActionOf(def, omen.ActionId, active);
              Saw(first, scene + ', first');
              if (def.ActionsPerPhase < 2) continue;
              const plan = EnemyAi.DecidePlan(def, gap, stamina, omen, skip, active);
              if (plan != null && plan.ActionId !== EnemyAi.RestActionId) Saw(EnemyAi.ActionOf(def, plan.ActionId, active), scene + ', plan');
              const again = [...spent, first.Id, ...EnemyAi.Barred(def, foe, false, active)];
              Saw(EnemyAi.ChooseAction(def, gap, stamina - first.Cost, again, active), scene + ', second');
            }
          }
        }
      }
    }
    return chosen;
  }

  test('EveryAction_IsChosenInSomeScene (every enemy of the roster)', () => {
    for (const def of Enemies.All) {
      const chosen = ChosenSomewhere(def);
      const missing = [...def.Actions.keys()].filter((id) => !chosen.has(id) && !NeverChosen.has(def.Id + '.' + id)).sort(compareOrdinal);
      assert.deepEqual(missing, [], `${def.Id}: no scene chooses these actions`);
    }
  });

  test('TheWalk_SeesTheFatiguedStaminaOfOne', () => {
    const def = Enemies.PolearmWarped;
    assert.equal(EnemyAi.StaminaAtAction(def, Body(def, 0, true, 0)), 1);
    assert.equal(EnemyAi.StaminaAtAction(def, Body(def, 0, false, 0)), 2);
    assert.ok(ChosenSomewhere(def).has('guard_up'));
  });

  test('TheTwoActionsV415MovedUp_AreChosenWhereTheRosterSays', () => {
    const alpha = Enemies.PackAlpha;
    const alphaSecond = EnemyAi.ChooseAction(alpha, 1, alpha.MaxStamina - 2 - 2, ['hunt_stance', 'pounce']);
    const angler = Enemies.AbyssAngler;
    const anglerSecond = EnemyAi.ChooseAction(angler, 0, 4 - 2, ['drown']);
    assert.equal(alphaSecond?.Id, 'crouch');
    assert.equal(anglerSecond?.Id, 'deep_water');
  });
});
