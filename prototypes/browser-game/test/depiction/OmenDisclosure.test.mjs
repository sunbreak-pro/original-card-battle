// Tests for src/bridge/depictionScript/OmenDisclosure.js (port of View/Depiction/Script/OmenDisclosure.cs).
// Ported cases: Assets/View/Depiction/Tests/OmenDisclosureTests.cs (all).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { OmenDisclosure } from '../../src/bridge/depictionScript/OmenDisclosure.js';
import { OmenFrame, OmenHit, OmenIcon, OmenStatus, StatusGlyph } from '../../src/bridge/depictionScript/DepictionTypes.js';

function Attack() {
  return new OmenFrame({
    Visible: true, Icon: OmenIcon.Attack, KindLabel: '攻撃', ValueText: '13', Hit: OmenHit.Lands,
    PlanVisible: true, PlanIcon: OmenIcon.Guard, PlanKindLabel: '防御', PlanValueText: '4',
  });
}

/** A 状態付与 omen (鈍足) with a 状態付与 plan (出血). */
function Seal() {
  return new OmenFrame({
    Visible: true, Icon: OmenIcon.Seal, KindLabel: '技', Hit: OmenHit.Lands,
    Statuses: [new OmenStatus({ Label: '鈍足', Glyph: StatusGlyph.Slow })],
    PlanVisible: true, PlanIcon: OmenIcon.Seal, PlanKindLabel: '技',
    PlanStatuses: [new OmenStatus({ Label: '出血', Glyph: StatusGlyph.Bleed })],
  });
}

test('AFrame_IsFullyDisclosed_UntilAWriterSaysOtherwise', () => {
  assert.equal(new OmenFrame().Disclosure, OmenDisclosure.Full);
  assert.equal(OmenDisclosure.Full, 2);
});

test('ReadToTwo_TheNumbersAndTheStatusesStay', () => {
  const omen = OmenDisclosure.Apply(Attack(), 2);
  assert.equal(omen.ValueText, '13');
  assert.equal(omen.PlanValueText, '4');
  assert.equal(omen.Disclosure, 2);

  const seal = OmenDisclosure.Apply(Seal(), 2);
  assert.equal(seal.Statuses.length, 1);
  assert.equal(seal.PlanStatuses.length, 1);
});

for (const level of [1, 0]) {
  test(`BelowTwo_TheNumbersGo_AndTheKindStays(${level})`, () => {
    const omen = OmenDisclosure.Apply(Attack(), level);
    assert.equal(omen.ValueText, '');
    assert.equal(omen.PlanValueText, '');
    assert.equal(omen.Disclosure, level);
    assert.equal(omen.Icon, OmenIcon.Attack, "the kind's icon stays");
    assert.equal(omen.KindLabel, '攻撃', 'the kind word stays');
    assert.equal(omen.Hit, OmenHit.Lands);
  });

  test(`BelowTwo_AStatusApplyingOmen_KeepsItsStamp_AndLosesItsStatuses(${level})`, () => {
    const omen = OmenDisclosure.Apply(Seal(), level);
    assert.equal(omen.Icon, OmenIcon.Seal, '§4.4: the icon and the kind word show at every level');
    assert.deepEqual(omen.Statuses, []);
    assert.deepEqual(omen.PlanStatuses, []);
  });
}

test('ReadToOne_ThePlanStays', () => {
  const omen = OmenDisclosure.Apply(Attack(), 1);
  assert.equal(omen.PlanVisible, true);
  assert.equal(omen.PlanKindLabel, '防御');
  assert.equal(OmenDisclosure.ShowsPlan(1), true);
});

test('ReadToZero_ThePlanGoes_AndTheFirstOmensKindStays', () => {
  // battle_ui_ux_v2 §7 / §5.16 順 2: 開示度 0 は 1 手目の種別だけで、2 手目を出さない.
  const omen = OmenDisclosure.Apply(Attack(), 0);
  assert.equal(omen.PlanVisible, false);
  assert.equal(omen.Visible, true);
  assert.equal(omen.KindLabel, '攻撃');
  assert.equal(OmenDisclosure.ShowsPlan(0), false);
});

test('ALevelOutsideZeroToTwo_IsHeldInside', () => {
  assert.equal(OmenDisclosure.Apply(Attack(), -1).Disclosure, 0, '−1 has no parts yet and reads as 0');
  assert.equal(OmenDisclosure.Apply(Attack(), 5).Disclosure, 2);
  assert.equal(OmenDisclosure.ShowsNumbers(5), true);
  assert.equal(OmenDisclosure.ShowsNumbers(-1), false);
  assert.equal(OmenDisclosure.ShowsPlan(-1), false);
});

// ---- port-specific (checked against the C#) ----

test('Port_ApplyReturnsTheSameFrame_AndEmptiesItsListsInPlace', () => {
  const omen = Seal();
  const statuses = omen.Statuses;
  assert.strictEqual(OmenDisclosure.Apply(omen, 0), omen);
  assert.strictEqual(omen.Statuses, statuses, 'List.Clear() keeps the list');
  assert.equal(statuses.length, 0);
});

test('Port_ANullFrame_ThrowsWithDotNetsMessage', () => {
  assert.throws(() => OmenDisclosure.Apply(null, 1), { name: 'ArgumentNullException', message: "Value cannot be null. (Parameter 'omen')" });
});
