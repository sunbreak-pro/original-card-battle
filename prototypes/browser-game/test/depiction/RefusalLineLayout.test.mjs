// Tests for src/bridge/depictionScript/RefusalLineLayout.js (port of View/Depiction/Script/RefusalLineLayout.cs).
// Ported cases: Assets/View/Depiction/Tests/RefusalLineLayoutTests.cs (all).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { RefusalLineLayout } from '../../src/bridge/depictionScript/RefusalLineLayout.js';

test('TheLine_Is48Tall_WithA16Dot_And20Text_For3Point5Seconds', () => {
  assert.equal(RefusalLineLayout.Height, 48);
  assert.equal(RefusalLineLayout.DotSize, 16);
  assert.equal(RefusalLineLayout.TextSize, 20);
  assert.equal(RefusalLineLayout.Seconds, 3.5);
});

test('TheLine_StandsJustAboveTheRestingHand', () => {
  assert.ok(RefusalLineLayout.Bottom < 896, "§4.8: the middle card's top is at y 896");
  assert.ok(RefusalLineLayout.Top > 790, 'below the throw line (§5.3, y 790)');
});

for (const text of [0, 420]) {
  test(`TheLine_IsCentredOnTheHand_AndFitsItsText(${text})`, () => {
    const width = RefusalLineLayout.WidthFor(text);
    assert.equal(width, 16 + 16 + 10 + text + 16);
    assert.equal(RefusalLineLayout.LeftFor(text) + width * 0.5, 960);
  });
}

// ---- port-specific (checked against the C#) ----

test('Port_ANegativeWidthCountsAsNone_AndTheTopIs836', () => {
  assert.equal(RefusalLineLayout.WidthFor(-20), 58);
  assert.equal(RefusalLineLayout.Top, 836);
  assert.equal(RefusalLineLayout.WidthFor(33.3), Math.fround(Math.fround(42 + Math.fround(33.3)) + 16));
});
