// Tests for src/bridge/depictionScript/PileLayout.js (port of View/Depiction/Script/PileLayout.cs).
// Ported cases: Assets/View/Depiction/Tests/PileLayoutTests.cs (all).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { PileKind, PileLayout } from '../../src/bridge/depictionScript/PileLayout.js';
import { PileFrame } from '../../src/bridge/depictionScript/DepictionTypes.js';

test('ThePiles_StandFromX32_64By84_22Apart', () => {
  assert.equal(PileLayout.LeftOf(PileKind.Draw), 32);
  assert.equal(PileLayout.LeftOf(PileKind.Discard), 118);
  assert.equal(PileLayout.LeftOf(PileKind.Exile), 204);
  assert.equal(PileLayout.Top, 948);
  assert.equal(PileLayout.Width, 64);
  assert.equal(PileLayout.Height, 84);
});

test('TheThree_SpanTheStaminaTagsWidth', () => {
  assert.equal(PileLayout.SpanWidth, 236, '§4.7: the stamina tag is 236 wide, the same as the three piles');
});

for (const [count, behind] of [[0, 0], [1, 0], [2, 1], [6, 1], [7, 2], [11, 2], [12, 3], [40, 3]]) {
  test(`APile_ThickensBySheets_AsItsCountGrows(${count}, ${behind})`, () => {
    assert.equal(PileLayout.SheetsBehind(count), behind);
  });
}

test('TheThickestPile_StaysClearOfTheStaminaTag', () => {
  // The tag: y 846, 84 tall, so it ends at 930.
  const top = PileLayout.Top - PileLayout.MaxSheetsBehind * PileLayout.SheetStep;
  assert.ok(top > 846 + 84);
});

test('AnEmptyPile_IsAnEmptySlot', () => {
  assert.equal(PileLayout.IsEmpty(0), true);
  assert.equal(PileLayout.IsEmpty(1), false);
});

test('TheCount_IsReadOffTheFrame', () => {
  const piles = new PileFrame({ Shown: true, Draw: 14, Discard: 5, Exile: 1 });
  assert.equal(PileLayout.CountOf(piles, PileKind.Draw), 14);
  assert.equal(PileLayout.CountOf(piles, PileKind.Discard), 5);
  assert.equal(PileLayout.CountOf(piles, PileKind.Exile), 1);
  assert.equal(PileLayout.CountOf(null, PileKind.Draw), 0);
});

// ---- port-specific (checked against the C#) ----

test('Port_AnUndefinedPile_AsCsCastsAnInt', () => {
  assert.equal(PileLayout.LeftOf(5), 32 + 5 * 86, '(PileKind)5 is still an int to the C#');
  assert.equal(PileLayout.LeftOf('5'), 462, '.NET writes an undefined value as its number');
  assert.throws(() => PileLayout.CountOf(new PileFrame(), 5), {
    name: 'ArgumentOutOfRangeException',
    message: "Specified argument was out of the range of valid values. (Parameter 'pile')\nActual value was 5.",
  });
  assert.equal(PileLayout.CountOf(null, 5), 0, 'a null frame answers 0 before the pile is looked at');
  assert.equal(PileLayout.SheetsBehind(-3), 0);
});
