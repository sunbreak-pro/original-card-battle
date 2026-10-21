// Tests for src/bridge/depictionScript/EffectAudit.js (port of View/Depiction/Script/EffectAudit.cs).
// Ported cases: Assets/View/Depiction/Tests/EffectAuditTests.cs (every EffectId has a family, what it
// carries and a judgement) and the EffectFlow lines of BattleSpeedTests.cs, plus the table's rows
// pinned as the C# writes them.
//
// The cases run on the real module when Effects / BattleSpeed have been ported, else on the module
// isolated from them (EffectId's 60 members and BattleSpeed.WallMs stubbed from the C#).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { enumValues } from '../../src/shared/cs.js';
import { BATTLE_SPEED_STUB, EFFECTS_STUB, EFFECT_IDS, importIsolated, tryImport } from './_demoIsolate.mjs';

const real = await tryImport('bridge/depictionScript/EffectAudit.js');
const effects = real.module ? await tryImport('bridge/depictionScript/Effects.js') : { module: null };
const a = real.module ?? (await importIsolated('bridge/depictionScript/EffectAudit.js', {
  './Effects.js': EFFECTS_STUB,
  './BattleSpeed.js': BATTLE_SPEED_STUB,
})).module;
const layer = real.module ? 'real' : 'isolated';
const { EffectAudit, EffectAuditEntry, EffectFlow, Carries, Verdict, Basis } = a;
/** Every EffectId, in declaration order: the real enum's when it is there, else the C#'s list. */
const ids = effects.module ? enumValues(effects.module.EffectId) : EFFECT_IDS;

test(`EveryEffect_HasAJudgement_WithAReason [${layer}]`, () => {
  for (const id of ids) {
    assert.equal(EffectAudit.Has(id), true, id + ' has no row in EffectAudit (Script/EffectAudit.cs)');
    const entry = EffectAudit.Of(id);
    assert.ok(entry.Family.length > 0, id);
    assert.ok(entry.Reason.length > 10, id + ' needs a reason');
  }
  assert.equal(EffectAudit.All.length, ids.length, 'a row for a name that is not an effect');
});

test(`ADroppedEffect_CarriesNothingTheFrameLacks_AndNeededOnesCarrySomething [${layer}]`, () => {
  for (const entry of EffectAudit.All) {
    if (entry.Verdict === Verdict.Needed) {
      assert.notEqual(entry.Carries, Carries.None, entry.Id + ' is judged needed but carries nothing');
    }
    if (entry.Carries === Carries.None) {
      assert.equal(entry.Verdict, Verdict.Drop, entry.Id + ' carries nothing, so it cannot be kept as it is');
    }
  }
});

test(`ADecisionThatRestsOnHowItFeels_IsHandedTo79 [${layer}]`, () => {
  // A "drop" judged from the data alone would be a claim nobody looked at: every drop is provisional.
  for (const entry of EffectAudit.All.filter((e) => e.Verdict === Verdict.Drop)) {
    assert.equal(entry.Basis, Basis.HandsOn, entry.Id + ' is dropped without a hands-on check');
  }
});

test(`TheFlowGaps_AreTheOnesTheScreenWaits [${layer}]`, () => {
  assert.equal(EffectFlow.AfterPlayerEventMs, 250);
  assert.equal(EffectFlow.AfterAutoEventMs, 350);
  // 1000f / 60f: the float32 of the quotient.
  assert.equal(EffectFlow.FrameMs, Math.fround(1000 / 60));
  assert.equal(EffectFlow.FrameMs, 16.666666030883789);
});

// BattleSpeedTests.AWait_LastsTodaysLengthTimesOnePointTwoFiveOverTheMultiplier (the EffectFlow lines).
for (const [step, scale] of [['Slow', 1.25], ['Normal', 1], ['Fast', Math.fround(0.8333333)]]) {
  test(`AWait_LastsTodaysLengthTimesOnePointTwoFiveOverTheMultiplier(${step}) — EffectFlow [${layer}]`, () => {
    assert.ok(Math.abs(EffectFlow.AfterPlayerEventMsAt(step) - Math.fround(250 * scale)) <= 0.01, String(EffectFlow.AfterPlayerEventMsAt(step)));
    assert.ok(Math.abs(EffectFlow.AfterAutoEventMsAt(step) - Math.fround(350 * scale)) <= 0.01, String(EffectFlow.AfterAutoEventMsAt(step)));
  });
}

// ---- the table as the C# writes it ----

test(`TheRows_AreInTheEnumsOrder_InFourFamilies [${layer}]`, () => {
  assert.deepEqual(EffectAudit.All.map((e) => e.Id), ids);
  const families = new Map();
  for (const e of EffectAudit.All) families.set(e.Family, (families.get(e.Family) ?? 0) + 1);
  assert.deepEqual([...families], [['札の動き', 15], ['攻撃と防御', 19], ['そのほかの拍', 13], ['状態とターン', 13]]);
  assert.equal(EffectAudit.Of('StrikeShape').Family, '攻撃と防御');
  assert.equal(EffectAudit.Of('GrammarMark').Family, 'そのほかの拍');
  assert.equal(EffectAudit.Of('PushMark').Family, '状態とターン');
});

test(`TheVerdicts_AndBases_CountAsWritten [${layer}]`, () => {
  const count = (f) => EffectAudit.All.filter(f).length;
  assert.equal(count((e) => e.Verdict === Verdict.Needed), 38);
  assert.equal(count((e) => e.Verdict === Verdict.Shorten), 11);
  assert.equal(count((e) => e.Verdict === Verdict.Drop), 11);
  assert.equal(count((e) => e.Basis === Basis.HandsOn), 24);
  assert.equal(count((e) => e.Carries === Carries.None), 6);
});

test(`TheRows_HoldTheirFlags_AndWords [${layer}]`, () => {
  const row = (id) => {
    const e = EffectAudit.Of(id);
    return [e.Carries, e.Verdict, e.Basis];
  };
  assert.deepEqual({ ...Carries }, {
    None: 0, Amount: 1, WhoHitWhom: 2, Status: 4, Position: 8, Intent: 16, Hand: 32, TurnOrResult: 64, Input: 128,
  });
  assert.deepEqual(row('CardDraw'), [Carries.Hand, Verdict.Shorten, Basis.HandsOn]);
  assert.deepEqual(row('ReceiverShow'), [Carries.Input | Carries.WhoHitWhom, Verdict.Needed, Basis.Script]);
  assert.equal(EffectAudit.Of('ReceiverShow').Carries, 130);
  assert.deepEqual(row('CardReturn'), [Carries.Hand | Carries.Input, Verdict.Needed, Basis.Script]);
  assert.deepEqual(row('EnemyMotion'), [Carries.WhoHitWhom | Carries.Intent, Verdict.Needed, Basis.Script]);
  assert.deepEqual(row('GuardBlock'), [Carries.Amount | Carries.WhoHitWhom, Verdict.Shorten, Basis.Script]);
  assert.deepEqual(row('HitStop'), [Carries.None, Verdict.Drop, Basis.HandsOn]);
  assert.deepEqual(row('ReservePips'), [Carries.Hand, Verdict.Needed, Basis.HandsOn]);
  assert.deepEqual(row('Defeat'), [Carries.TurnOrResult, Verdict.Needed, Basis.Script]);
  assert.equal(EffectAudit.Of('CardDraw').Reason,
    '新しい手札はターン開始の枠が先に持っているので、飛ぶ動きが運ぶのは「どれが来たか」だけです。6 枚で 560 ms 入力を止めるため、間隔を詰めます。');
  assert.equal(EffectAudit.Of('ReservePips').Reason,
    '構えの Guard が、残したスタミナ 3 から来たことを、ピップ 1〜3 の光で示します（§5.13 順 1）。盾と +3 だけでは、何と引き換えだったかが分かりません。構えのある毎ターン 250 ms 待つので、長さが妥当かは #79 の試運転で確かめます。');
  assert.equal(EffectAudit.Of('Defeat').Reason,
    '敵が倒れてマスが空いたことを示します。切ると人型が一瞬で消え、何が起きたか読めません。勝利の札はこの後に出ます（§2.2 の 6）。');
});

test(`AnEffectWithNoRow_Throws_AndAnEntryIsFixed [${layer}]`, () => {
  assert.equal(EffectAudit.Has('Nothing'), false);
  assert.throws(() => EffectAudit.Of('Nothing'),
    (e) => e.name === 'KeyNotFoundException' && e.message === 'No judgement is written for effect Nothing.');
  const entry = new EffectAuditEntry('CardDraw', 'f', Carries.Hand, Verdict.Needed, Basis.Script, 'r');
  assert.deepEqual({ ...entry }, { Id: 'CardDraw', Family: 'f', Carries: 32, Verdict: 'Needed', Basis: 'Script', Reason: 'r' });
  assert.throws(() => { 'use strict'; entry.Reason = 'x'; }, TypeError, 'get-only');
  assert.equal(EffectAudit.Of('CardDraw'), EffectAudit.Of('CardDraw'), 'one table, built once');
});
