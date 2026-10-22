// Tests for src/bridge/depictionScript/DemoDeck.js (port of View/Depiction/Script/DemoDeck.cs).
// Ported cases: the DemoDeck parts of Assets/View/Depiction/Tests/DepictionScriptTests.cs (every hand
// card prints its type and what it does, in a box three sentences tall; every throw-line card says whose
// figures it lands on), plus DefIdOf and the faces DemoDeck prints (checked against the C#).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { DemoCard, DemoDeck, TraitCondition } from '../../src/bridge/depictionScript/DemoDeck.js';
import { CardAim, CardFace, CardKind, RangeSide, UnitSide } from '../../src/bridge/depictionScript/DepictionTypes.js';

function Width(text) {
  let width = 0;
  for (const c of text) width += c.charCodeAt(0) < 0x2000 ? 0.5 : 1.0;
  return width;
}

test('TheFiveKinds_AreTheFilmedSlices', () => {
  assert.deepEqual(DemoDeck.All.map((c) => c.DefId), ['kesagiri', 'daijodan', 'ushirotobi', 'tetsu-no-uke', 'kansatsu']);
  assert.deepEqual(DemoDeck.All.map((c) => c.Name), ['袈裟斬り', '大上段', '後ろ跳び', '鉄の受け', '観察']);
  assert.deepEqual(DemoDeck.All.map((c) => c.Cost), [1, 2, 1, 2, 1]);
  assert.deepEqual(DemoDeck.All.map((c) => c.Kind), [CardKind.Attack, CardKind.Attack, CardKind.Move, CardKind.Guard, CardKind.Skill]);
  assert.deepEqual(DemoDeck.All.map((c) => c.Trait), [TraitCondition.FirstCardOfTurn, TraitCondition.None, TraitCondition.AgainstAttackOmen, TraitCondition.None, TraitCondition.None]);
  assert.deepEqual(DemoDeck.All.map((c) => c.RequiredRange), [RangeSide.Near, RangeSide.Near, RangeSide.Near, null, null]);
  assert.strictEqual(DemoDeck.All, DemoDeck.All, 'one list, as the static readonly field');
});

test('EveryHandCardPrintsItsTypeAndWhatItDoes', () => {
  for (const def of DemoDeck.All) {
    const face = DemoDeck.Face(def, def.DefId, true);
    assert.notEqual(face.TypeLabel, '', face.Id + ' has no TypeLabel');
    assert.notEqual(face.Description, '', face.Id + ' has no Description');
    // One sentence per line in a box three lines tall and about nine full-width characters wide.
    const sentences = face.Description.replace(/。+$/, '').split('。');
    assert.ok(sentences.length <= 3, face.Id + ' has more than three sentences');
    for (const sentence of sentences) assert.ok(Width(sentence + '。') <= 9.5, face.Id + ': ' + sentence);
    if (face.ValueText) assert.ok(face.Description.includes(face.ValueText), face.Id + ' describes a number other than its value');
  }
});

test('EveryThrowLineCardSaysWhoseFiguresItLandsOn', () => {
  let throwLine = 0;
  for (const def of DemoDeck.All) {
    const face = DemoDeck.Face(def, def.DefId, false);
    if (face.Aim !== CardAim.Self) continue;
    throwLine++;
    assert.equal(face.Affects, UnitSide.Player, face.Id);
  }
  assert.equal(throwLine, 2);
});

test('DefIdOf_TakesTheKindOffAnInstanceId', () => {
  assert.equal(DemoDeck.DefIdOf('kesagiri#1'), 'kesagiri');
  assert.equal(DemoDeck.DefIdOf('kesagiri'), 'kesagiri');
  assert.equal(DemoDeck.DefIdOf('a#b#c'), 'a');
  assert.equal(DemoDeck.DefIdOf('#x'), '');
});

test('AFace_LightsItsTraitOnlyWhenItHasOne_AndPrintsItsRangeGlyph', () => {
  const kesa = DemoDeck.Face(DemoDeck.All[0], 'kesagiri#2', true);
  assert.ok(kesa instanceof CardFace);
  assert.equal(kesa.Id, 'kesagiri#2');
  assert.equal(kesa.TraitLit, true);
  assert.equal(kesa.RequiredRangeGlyph, '近');
  assert.equal(kesa.TraitText, '初手 +3');
  assert.equal(DemoDeck.Face(DemoDeck.All[0], 'k', false).TraitLit, false);
  const dai = DemoDeck.Face(DemoDeck.All[1], 'daijodan', true);
  assert.equal(dai.TraitLit, false, 'no trait, no lamp');
  const tetsu = DemoDeck.Face(DemoDeck.All[3], 'tetsu-no-uke', true);
  assert.equal(tetsu.RequiredRange, null);
  assert.equal(tetsu.RequiredRangeGlyph, '');
  assert.equal(DemoDeck.Glyph(RangeSide.Near), '近');
  assert.equal(DemoDeck.Glyph(RangeSide.Far), '遠');
});

test('ADemoCard_StartsAtTheCsInitializers', () => {
  const c = new DemoCard();
  assert.deepEqual({ ...c }, {
    DefId: '', Name: '', Cost: 0, Kind: CardKind.Attack, Aim: CardAim.Single, Affects: null, TypeLabel: '', Description: '',
    ValueText: '', TraitText: '', Trait: TraitCondition.None, RequiredRange: null,
  });
  assert.equal(new DemoCard({ Name: 'x' }).Name, 'x');
});
