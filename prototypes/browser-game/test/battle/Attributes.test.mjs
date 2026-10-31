// Tests for src/core/battle/Attributes.js (port of unity-port/BattleCore/Attributes.cs).
// Ports CardsTests.FoldingCountsACardAsOneAttribute_AttackThenGuardThenSkill and BossSwitchTests' move-only fold.
import { test as nodeTest } from 'node:test';
import assert from 'node:assert/strict';
import { loadDeps } from './_foundation_deps.mjs';

// Types.js imports Statuses.js; skip with a reason while that module is missing.
const { mods, skip: SKIP } = await loadDeps(import.meta.url, ['../../src/core/battle/Attributes.js', '../../src/core/battle/Types.js']);
const test = (name, fn) => nodeTest(name, { skip: SKIP }, fn);
const { AttributeRule, BattleAttribute, Face, StatusGrant, StatusKind, StanceDef, StanceHook } = mods ?? {};

const A = BattleAttribute;

test('FoldingCountsACardAsOneAttribute_AttackThenGuardThenSkill', () => {
  const dual = new Face({ Power: 6, Guard: 4 });
  const guardSkill = new Face({ Guard: 4, Statuses: [new StatusGrant(StatusKind.Regen, 1, { OnSelf: true })] });

  assert.equal(AttributeRule.Fold(A.Attack | A.Guard, dual), A.Attack);
  assert.equal(AttributeRule.Fold(A.Attack | A.Skill, dual), A.Attack);
  assert.equal(AttributeRule.Fold(A.Guard | A.Skill, guardSkill), A.Guard);
  assert.equal(AttributeRule.Fold(A.Skill, guardSkill), A.Skill);
  assert.equal(AttributeRule.Fold(A.Attack | A.Guard | A.Skill, dual), A.Attack);
  assert.equal(AttributeRule.Fold(A.Stance, new Face()), A.Stance);
  // Movement only: a Guard makes it 防御, anything else makes it スキル (2026-09-28 の割り振り).
  assert.equal(AttributeRule.Fold(A.None, new Face({ Move: -2, Guard: 4 })), A.Guard);
  assert.equal(AttributeRule.Fold(A.None, new Face({ Move: 1, Draw: 1 })), A.Skill);
  assert.equal(AttributeRule.Fold(A.None, new Face({ Move: 2, StaminaGain: 1 })), A.Skill);
  assert.equal(AttributeRule.Fold(A.None, new Face()), A.None);
});

test('Fold: a move-only face counts as スキル; heal / status-only too; stance wins over everything', () => {
  assert.equal(AttributeRule.Fold(A.None, new Face({ Move: 1 })), A.Skill);
  assert.equal(AttributeRule.Fold(A.None, new Face({ Push: -1 })), A.Skill);
  assert.equal(AttributeRule.Fold(A.None, new Face({ Heal: 3 })), A.Skill);
  assert.equal(AttributeRule.Fold(A.None, new Face({ Statuses: [new StatusGrant(StatusKind.Bleed)] })), A.Skill);
  assert.equal(AttributeRule.Fold(A.Stance | A.Attack, new Face({ Power: 3 })), A.Stance);
});

test('HasMovement reads Move and Push', () => {
  assert.equal(AttributeRule.HasMovement(new Face()), false);
  assert.equal(AttributeRule.HasMovement(new Face({ Move: -1 })), true);
  assert.equal(AttributeRule.HasMovement(new Face({ Push: 2 })), true);
  assert.equal(AttributeRule.HasMovement(new Face({ Power: 5, Guard: 3 })), false);
});

test('IsMovementCard: declares nothing and moves', () => {
  assert.equal(AttributeRule.IsMovementCard(A.None, new Face({ Move: 1 })), true);
  assert.equal(AttributeRule.IsMovementCard(A.None, new Face({ Move: 1, Draw: 1 })), true);
  assert.equal(AttributeRule.IsMovementCard(A.Attack, new Face({ Power: 5, Move: 1 })), false);
  assert.equal(AttributeRule.IsMovementCard(A.None, new Face({ Guard: 2 })), false);
});

test('StanceStandsAlone: a stance declares nothing else and does not move; a stance face declares スタンス', () => {
  const stance = new StanceDef(StanceHook.TurnStart, { Guard: 2 });
  assert.equal(AttributeRule.StanceStandsAlone(A.Stance, new Face({ Stance: stance })), true);
  assert.equal(AttributeRule.StanceStandsAlone(A.Stance | A.Guard, new Face({ Stance: stance })), false);
  assert.equal(AttributeRule.StanceStandsAlone(A.Stance, new Face({ Stance: stance, Move: 1 })), false);
  assert.equal(AttributeRule.StanceStandsAlone(A.Stance, new Face({ Stance: stance, Power: 3 })), false);
  assert.equal(AttributeRule.StanceStandsAlone(A.Stance, new Face({ Stance: stance, Guard: 3 })), false);
  assert.equal(AttributeRule.StanceStandsAlone(A.Attack, new Face({ Power: 3 })), true);
  assert.equal(AttributeRule.StanceStandsAlone(A.Guard, new Face({ Guard: 3, Stance: stance })), false);
});

test('null faces are refused like C# (ArgumentNullException, same message)', () => {
  for (const f of [
    () => AttributeRule.HasMovement(null),
    () => AttributeRule.Fold(A.Attack, null),
    () => AttributeRule.IsMovementCard(A.None, undefined),
    () => AttributeRule.StanceStandsAlone(A.Stance, null),
  ]) {
    assert.throws(f, (e) => e.name === 'ArgumentNullException' && e.message === "Value cannot be null. (Parameter 'face')");
  }
});

// 1500 random faces folded by the C# code under dotnet 10.
import { readFileSync } from 'node:fs';
const FIXTURE = JSON.parse(readFileSync(new URL('./Attributes.fixture.json', import.meta.url), 'utf8'));

test('Fold / HasMovement / IsMovementCard / StanceStandsAlone / GivesFoeStatus: 1500 random faces match C#', () => {
  const stance = new StanceDef(StanceHook.TurnStart, { Guard: 2 });
  for (const [declared, Power, Guard, Move, Push, Draw, StaminaGain, Heal, statuses, hasStance, ...expected] of FIXTURE.rows) {
    const face = new Face({
      Power, Guard, Move, Push, Draw, StaminaGain, Heal,
      Statuses: statuses === null ? null : statuses.map((self) => new StatusGrant(StatusKind.Bleed, 1, self === 1)),
      Stance: hasStance ? stance : null,
    });
    const got = [
      AttributeRule.Fold(declared, face),
      +AttributeRule.HasMovement(face),
      +AttributeRule.IsMovementCard(declared, face),
      +AttributeRule.StanceStandsAlone(declared, face),
      +face.GivesFoeStatus,
    ];
    assert.deepEqual(got, expected, JSON.stringify(face));
  }
});
