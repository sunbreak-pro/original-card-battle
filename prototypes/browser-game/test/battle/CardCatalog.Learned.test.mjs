// Tests for src/core/battle/CardCatalog.Learned.js (port of unity-port/BattleCore/CardCatalog.Learned.cs):
// the learned forty (#41-#80), the part of the partial class this file declares. Expected values are
// written by hand from the C# source.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CardCatalog as LearnedPart } from '../../src/core/battle/CardCatalog.Learned.js';
import { CardCatalog } from '../../src/core/battle/CardCatalog.js';
import { BattleAttribute, CardDef } from '../../src/core/battle/Types.js';
import { Constants } from '../../src/core/battle/Constants.js';
import { pin, face, stanceOf, traitOf, card } from './_battle_cards.mjs';

const A = BattleAttribute;

const CANON = [
  'miasma_blade', 'priest_prayer', 'purge_flash', 'line_lash', 'abyss_stance', 'haul_step', 'root_stride', 'thorn_shot', 'root_bind', 'bulwark',
  'shield_bash', 'pack_howl', 'fang_rush', 'whirlwind', 'riposte_stance', 'hunter_mark', 'spear_wall', 'shield_push', 'snap_guard', 'leap_back',
  'rising_cut', 'leg_sweep', 'dash_in', 'guard_walk', 'thorn_cut', 'calm_guard', 'deflect', 'crescent_cut', 'pommel_strike', 'mist_step',
  'keen_eye', 'resolve', 'gale_thrust', 'anchor_stance', 'vital_thrust', 'evade_cut', 'blood_dance', 'wolf_stance', 'shadow_lunge', 'last_stand',
];

test('The part holds the learned forty, #41-#80, in canon order, frozen', () => {
  const defs = Object.values(LearnedPart);
  assert.equal(defs.length, 40);
  assert.ok(defs.every((d) => d instanceof CardDef));
  assert.deepEqual(defs.map((d) => d.Id), CANON);
  assert.deepEqual(CardCatalog.All.slice(40), defs, 'the merged catalog ends with this part');
  assert.ok(Object.isFrozen(LearnedPart));
});

test('§3: none is 素直, and #80 背水の陣 is the one card with two traits', () => {
  assert.ok(Object.values(LearnedPart).every((d) => d.Trait != null));
  assert.deepEqual(Object.values(LearnedPart).filter((d) => d.ExtraTrait != null).map((d) => d.Id), ['last_stand']);
  assert.equal(LearnedPart.LastStand.AllTraits.length, 2);
  assert.equal(LearnedPart.LastStand.AllTraits[0], LearnedPart.LastStand.Trait, 'Trait is judged first');
});

test('The 追撃 traits carry FOLLOW_UP_POWER', () => {
  const followUps = Object.values(LearnedPart).filter((d) => d.Trait.Effect === 'FollowUp');
  assert.deepEqual(followUps.map((d) => d.Id), ['purge_flash', 'abyss_stance', 'pack_howl', 'wolf_stance']);
  assert.ok(followUps.every((d) => d.Trait.Amount === Constants.FollowUpPower && d.Trait.Amount === 5));
});

test('Fifteen learned cards pinned field by field against CardCatalog.Learned.cs', () => {
  const expected = {
    MiasmaBlade: card({
      Id: 'miasma_blade', Name: '瘴気の刃', Attributes: A.Attack | A.Skill, Column: 1, Face: face({ Power: 5, Statuses: [['Fatigue', 2, false, 0]] }),
      Trait: traitOf({ Condition: 'FoeHas', Effect: 'PowerBonus', Amount: 5, Watch: 'Fatigue' }),
      Description: '疲れた相手を瘴気の刃で削る', Cost: 1, Attribute: A.Attack,
    }),
    PurgeFlash: card({
      Id: 'purge_flash', Name: '浄化の一閃', Attributes: A.Stance, Column: 2,
      Face: face({ Stance: stanceOf({ Hook: 'TurnStart', When: 'GapAtLeast', Threshold: 2, Guard: 3 }) }),
      Trait: traitOf({ Condition: 'OmenIs', Effect: 'FollowUp', Amount: 5, Omen: 'Attack' }),
      Targets: 'Self', Description: '離れた場所で光を纏う', Cost: 2, Attribute: A.Stance,
    }),
    AbyssStance: card({
      Id: 'abyss_stance', Name: '深淵の構え', Attributes: A.Stance, Column: 3,
      Face: face({ Stance: stanceOf({ Hook: 'AttackBonus', When: 'GapAtMost', Threshold: 0, Power: 5 }) }),
      Trait: traitOf({ Condition: 'GapAtMost', Effect: 'FollowUp', Amount: 5, Threshold: 0 }),
      Targets: 'Self', Description: '引きずり込んだ間合いで押し切る型', Cost: 3, Attribute: A.Stance,
    }),
    RootStride: card({
      Id: 'root_stride', Name: '根渡り', Attributes: A.Stance, Column: 3,
      Face: face({ Stance: stanceOf({ Hook: 'TurnEnd', When: 'GapAtLeast', Threshold: 2, Guard: 5 }) }),
      Trait: traitOf({ Condition: 'Combo', Effect: 'GuardBonus', Amount: 3, Attribute: A.Skill }),
      Targets: 'Self', Description: '伸びる根の上に立ち、離れて守る', Cost: 3, Attribute: A.Stance,
    }),
    RootBind: card({
      Id: 'root_bind', Name: '根縛り', Attributes: A.Stance, Column: 1, Face: face({ Stance: stanceOf({ Hook: 'BreakOnFoeMove', Break: 1 }) }),
      Trait: traitOf({ Condition: 'FoeHas', Effect: 'Draw', Amount: 1, Watch: 'Slow' }),
      Targets: 'Self', Description: '根を張って相手の足を縫う', Cost: 1, Attribute: A.Stance,
    }),
    ShieldPush: card({
      Id: 'shield_push', Name: '盾押し', Attributes: A.Guard, Column: 2, Face: face({ Guard: 6, Push: 1, Reach: [0, 0] }),
      Trait: traitOf({ Condition: 'Finisher', Effect: 'StaminaGain', Amount: 1 }),
      Description: '盾で押して間合いを空ける', Cost: 2, Attribute: A.Guard,
    }),
    DashIn: card({
      Id: 'dash_in', Name: '駆け込み', Attributes: A.None, Column: 1, Face: face({ Move: 2, StaminaGain: 1 }),
      Trait: traitOf({ Condition: 'GapAtLeast', Effect: 'Draw', Amount: 1, Threshold: 2 }),
      Targets: 'Self', Description: '遠くから駆けて視野を広げる', Cost: 1, Attribute: A.Skill,
    }),
    CalmGuard: card({
      Id: 'calm_guard', Name: '静の受け', Attributes: A.Guard | A.Skill, Column: 3,
      Face: face({ Guard: 10, Statuses: [['Focus', 2, true, 0], ['Regen', 3, true, 0]] }),
      Trait: traitOf({ Condition: 'SelfHas', Effect: 'NextTurnRecovery', Amount: 1, Watch: 'Focus' }),
      Targets: 'Self', Description: '受けながら次の一手を溜める', Cost: 3, Attribute: A.Guard,
    }),
    CrescentCut: card({
      Id: 'crescent_cut', Name: '三日月斬り', Attributes: A.Attack | A.Guard, Column: 2, Face: face({ Power: 10, Guard: 6 }),
      Trait: traitOf({ Condition: 'Finisher', Effect: 'Convert' }),
      Description: '手を尽くした後、受けを刃に変える', Cost: 2, Attribute: A.Attack,
    }),
    MistStep: card({
      Id: 'mist_step', Name: '霞み足', Attributes: A.Stance, Column: 3,
      Face: face({ Stance: stanceOf({ Hook: 'TurnEnd', When: 'GapAtLeast', Threshold: 2, Guard: 3, NextRecovery: 2 }) }),
      Trait: traitOf({ Condition: 'Combo', Effect: 'NextTurnRecovery', Amount: 1, Attribute: A.Skill }),
      Targets: 'Self', Description: '霞のように離れて守る型', Cost: 3, Attribute: A.Stance,
    }),
    KeenEye: card({
      Id: 'keen_eye', Name: '見切りの目', Attributes: A.Stance, Column: 2,
      Face: face({ Stance: stanceOf({ Hook: 'TurnStart', When: 'OmenAttack', Guard: 3 }) }),
      Trait: traitOf({ Condition: 'OmenIs', Effect: 'Draw', Amount: 1, Omen: 'Attack' }),
      Targets: 'Self', Description: '来ると分かっている一撃を見切る', Cost: 2, Attribute: A.Stance,
    }),
    AnchorStance: card({
      Id: 'anchor_stance', Name: '錨の構え', Attributes: A.Stance, Column: 1, Face: face({ Stance: stanceOf({ Hook: 'PushImmune' }) }),
      Trait: traitOf({ Condition: 'FoeHas', Effect: 'GuardBonus', Amount: 3, Watch: 'Intimidate' }),
      Targets: 'Self', Description: '怯んだ相手に押されない型', Cost: 1, Attribute: A.Stance,
    }),
    BloodDance: card({
      Id: 'blood_dance', Name: '血の舞', Attributes: A.Attack | A.Skill, Column: 2,
      Face: face({ Power: 8, Reach: [0, 2], Statuses: [['Bleed', 2, false, 0]] }),
      Trait: traitOf({ Condition: 'GapAtLeast', Effect: 'Status', Threshold: 2, Grant: ['Bleed', 1, false, 0] }),
      Description: '流れた血の分だけ舞う', Cost: 2, Attribute: A.Attack,
    }),
    WolfStance: card({
      Id: 'wolf_stance', Name: '狼の構え', Attributes: A.Stance, Column: 2,
      Face: face({ Stance: stanceOf({ Hook: 'AttackBonus', When: 'TargetHasStatus', Status: 'Bleed', Power: 3 }) }),
      Trait: traitOf({ Condition: 'SelfHas', Effect: 'FollowUp', Amount: 5, Watch: 'Empower' }),
      Targets: 'Self', Description: '傷ついた獲物を追う型', Cost: 2, Attribute: A.Stance,
    }),
    ShadowLunge: card({
      Id: 'shadow_lunge', Name: '影踏み', Attributes: A.Attack, Column: 2, Face: face({ Power: 12, Move: 1, Reach: [1, 2] }),
      Trait: traitOf({ Condition: 'OmenIs', Effect: 'PowerBonus', Amount: 5, Omen: 'Move' }),
      Description: '動こうとした影を踏んで斬る', Cost: 2, Attribute: A.Attack,
    }),
  };
  for (const [name, want] of Object.entries(expected)) {
    assert.deepEqual(pin(LearnedPart[name]), want, name);
    assert.equal(CardCatalog[name], LearnedPart[name], name);
  }
});
