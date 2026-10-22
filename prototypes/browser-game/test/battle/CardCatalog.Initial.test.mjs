// Tests for src/core/battle/CardCatalog.Initial.js (port of unity-port/BattleCore/CardCatalog.Initial.cs):
// the initial forty (#1-#40), the part of the partial class this file declares. Expected values are
// written by hand from the C# source.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CardCatalog as InitialPart } from '../../src/core/battle/CardCatalog.Initial.js';
import { CardCatalog } from '../../src/core/battle/CardCatalog.js';
import { BattleAttribute, CardDef } from '../../src/core/battle/Types.js';
import { pin, face, stanceOf, traitOf, card } from './_battle_cards.mjs';

const A = BattleAttribute;

const CANON = [
  'thrust', 'kesa_cut', 'overhead', 'wrist_cut', 'side_sweep', 'flat_strike', 'probe_thrust', 'pierce', 'throw_blade', 'reach_thrust',
  'brace', 'iron_block', 'low_guard', 'riposte_guard', 'deep_breath', 'water_stance', 'rock_stance', 'flow_stance', 'first_aid', 'spirit_roar',
  'focus', 'observe', 'war_cry', 'second_wind', 'footwork', 'back_leap', 'slide_step', 'break_off', 'lunge', 'feint',
  'boar_rush', 'rend', 'body_check', 'stone_throw', 'parry_cut', 'guard_thrust', 'step_in_guard', 'step_out_guard', 'iron_wall', 'twist_away',
];

test('The part holds the initial forty, #1-#40, in canon order, frozen', () => {
  const defs = Object.values(InitialPart);
  assert.equal(defs.length, 40);
  assert.ok(defs.every((d) => d instanceof CardDef));
  assert.deepEqual(defs.map((d) => d.Id), CANON);
  assert.deepEqual(CardCatalog.All.slice(0, 40), defs, 'the merged catalog starts with this part');
  assert.ok(Object.isFrozen(InitialPart));
});

test('§2: eight of the forty are 素直 (no trait), and none carries a second trait', () => {
  const plain = Object.values(InitialPart).filter((d) => d.Trait == null).map((d) => d.Id);
  assert.deepEqual(plain, ['thrust', 'side_sweep', 'iron_block', 'deep_breath', 'first_aid', 'parry_cut', 'step_in_guard', 'step_out_guard']);
  assert.ok(Object.values(InitialPart).every((d) => d.ExtraTrait == null && d.FixedCost == null));
});

test('Twelve initial cards pinned field by field against CardCatalog.Initial.cs', () => {
  const expected = {
    FlatStrike: card({
      Id: 'flat_strike', Name: '峰打ち', Attributes: A.Attack, Column: 1, Face: face({ Power: 7, Break: 1 }),
      Trait: traitOf({ Condition: 'FoeHas', Effect: 'PowerBonus', Amount: 5, Watch: 'Slow' }),
      Description: '足の止まった相手の体力を削る', Cost: 1, Attribute: A.Attack,
    }),
    RockStance: card({
      Id: 'rock_stance', Name: '岩の構え', Attributes: A.Stance, Column: 2, Face: face({ Stance: stanceOf({ Hook: 'TurnStart', Guard: 3 }) }),
      Trait: traitOf({ Condition: 'OmenIs', Effect: 'GuardBonus', Amount: 3, Omen: 'Attack' }),
      Targets: 'Self', Description: '動かず受ける型', Cost: 2, Attribute: A.Stance,
    }),
    FlowStance: card({
      Id: 'flow_stance', Name: '流れの構え', Attributes: A.Stance, Column: 3,
      Face: face({ Stance: stanceOf({ Hook: 'AttackBonus', When: 'MovedThisTurn', Power: 5 }) }),
      Trait: traitOf({ Condition: 'Moved', Effect: 'Draw', Amount: 1 }),
      Targets: 'Self', Description: '動いてから斬る型', Cost: 3, Attribute: A.Stance,
    }),
    FirstAid: card({
      Id: 'first_aid', Name: '応急処置', Attributes: A.Skill, Column: 3, Face: face({ Heal: 15 }),
      Targets: 'Self', Description: '傷口を縛る', Cost: 3, Attribute: A.Skill,
    }),
    WarCry: card({
      Id: 'war_cry', Name: '鬨の声', Attributes: A.Skill, Column: 2,
      Face: face({ Reach: [0, 2], Statuses: [['Empower', 2, true, 0], ['Intimidate', 2, false, 0]] }),
      Trait: traitOf({ Condition: 'Chain', Effect: 'Status', Grant: ['Fragile', 2, false, 0] }),
      Description: '叫んで己を奮わせ相手を怯ませる', Cost: 2, Attribute: A.Skill,
    }),
    SecondWind: card({
      Id: 'second_wind', Name: '息継ぎ', Attributes: A.Skill, Column: 3, Face: face({ StaminaGain: 1, Statuses: [['Regen', 3, true, 0]] }),
      Trait: traitOf({ Condition: 'SelfHas', Effect: 'StaminaGain', Amount: 1, Watch: 'Regen' }),
      Targets: 'Self', Description: '少しずつ傷を癒す', Cost: 3, Attribute: A.Skill,
    }),
    Footwork: card({
      Id: 'footwork', Name: '足運び', Attributes: A.None, Column: 1, Face: face({ Move: 1, Draw: 1 }),
      Trait: traitOf({ Condition: 'FirstPlay', Effect: 'StaminaGain', Amount: 1 }),
      Targets: 'Self', Description: '先に足を運ぶ', Cost: 1, Attribute: A.Skill,
    }),
    Feint: card({
      Id: 'feint', Name: '牽制', Attributes: A.Attack, Column: 2, Face: face({ Power: 10, Move: -1 }),
      Trait: traitOf({ Condition: 'Reserve', Effect: 'GuardBonus', Amount: 3, Threshold: 4 }),
      Description: '牽制して退く', Cost: 2, Attribute: A.Attack,
    }),
    BoarRush: card({
      Id: 'boar_rush', Name: '猪突猛進', Attributes: A.Attack, Column: 3, Face: face({ Power: 11, Move: 2, Reach: [1, 3] }),
      Trait: traitOf({ Condition: 'GapAtLeast', Effect: 'PowerBonus', Amount: 6, Threshold: 2 }),
      Description: '遠くから一気に駆けて斬る', Cost: 3, Attribute: A.Attack,
    }),
    Rend: card({
      Id: 'rend', Name: '裂き斬り', Attributes: A.Attack | A.Skill, Column: 1, Face: face({ Power: 5, Statuses: [['Bleed', 1, false, 0]] }),
      Trait: traitOf({ Condition: 'FoeHas', Effect: 'Status', Watch: 'Bleed', Grant: ['Bleed', 1, false, 0] }),
      Description: '傷口をさらに裂く', Cost: 1, Attribute: A.Attack,
    }),
    StoneThrow: card({
      Id: 'stone_throw', Name: '石礫', Attributes: A.Attack | A.Skill, Column: 2,
      Face: face({ Power: 5, Reach: [0, 3], Statuses: [['Fragile', 2, false, 0], ['Intimidate', 2, false, 0]] }),
      Trait: traitOf({ Condition: 'GapAtLeast', Effect: 'CostDown', Amount: 1, Threshold: 2 }),
      Description: '離れていても石は投げられる', Cost: 2, Attribute: A.Attack,
    }),
    IronWall: card({
      Id: 'iron_wall', Name: '鉄壁の構え', Attributes: A.Stance, Column: 2,
      Face: face({ Stance: stanceOf({ Hook: 'CostDiscount', Attribute: A.Guard }) }),
      Trait: traitOf({ Condition: 'SelfHas', Effect: 'GuardBonus', Amount: 3, Watch: 'Parry' }),
      Targets: 'Self', Description: '見切った上で壁になる', Cost: 2, Attribute: A.Stance,
    }),
  };
  for (const [name, want] of Object.entries(expected)) {
    assert.deepEqual(pin(InitialPart[name]), want, name);
    assert.equal(CardCatalog[name], InitialPart[name], name);
  }
});

test('The shorthands build the C# records: the grant-only call passes amount 0', () => {
  // C# `FoeHas(StatusKind.Bleed, TraitEffect.Status, grant: Foe(StatusKind.Fragile, 2))`.
  const t = InitialPart.SpiritRoar.Trait;
  assert.equal(t.Amount, 0);
  assert.equal(t.Grant.OnSelf, false);
  assert.equal(t.Grant.Stacks, 2);
  // The Self helper lands on the one playing; Foe on the opponent.
  assert.deepEqual(InitialPart.Focus.Face.StatusList.map((g) => g.OnSelf), [true, true]);
  assert.deepEqual(InitialPart.TwistAway.Face.StatusList.map((g) => g.OnSelf), [false, false, false]);
});
