// Tests for src/bridge/depictionScript/CornerLayout.js (port of View/Depiction/Script/CornerLayout.cs).
// Ported cases: Assets/View/Depiction/Tests/CornerLayoutTests.cs (all), plus ScreenBox.ToString as
// .NET writes floats (checked against .NET 10).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CornerLayout, ScreenBox } from '../../src/bridge/depictionScript/CornerLayout.js';
import { BattleSpeed } from '../../src/bridge/depictionScript/BattleSpeed.js';

/**
 * A generous bound on the tooltip's line: every character as wide as the 16 px font is tall,
 * so the real line (half-width digits and spaces among the kana) is narrower.
 */
const WidestTooltip = () => CornerLayout.SpeedTooltip(BattleSpeed.Tooltip.length * 16, 16 * 1.5);

test('TheSpeedSwitch_IsBattleVisualSection4_1sBox', () => {
  const speed = CornerLayout.SpeedButton;
  // 120 x 56 at y 20, 92 from the right: x 1708 to 1828 on the 1920 canvas.
  assert.equal(speed.Width, 120);
  assert.equal(speed.Height, 56);
  assert.equal(speed.Y, 20);
  assert.equal(speed.FromRight, 92);
  assert.equal(speed.X, 1708);
  assert.equal(speed.Right, 1828);

  // The journal button: 24 from the right, y 20, 56 x 56, 12 to the right of the switch.
  const journal = CornerLayout.JournalButton;
  assert.equal(journal.FromRight, 24);
  assert.equal(journal.Y, 20);
  assert.equal(journal.Width, 56);
  assert.equal(journal.Height, 56);
  assert.equal(journal.X - speed.Right, 12);
  assert.equal(speed.Overlaps(journal), false);
});

test('TheSwitchsInside_IsPaddingIconGapAndNumber', () => {
  // 12 + icon 24 + 8 + number + 12 = 120, which leaves 64 for "×1.25" at 20 px.
  assert.equal(CornerLayout.SpeedNumberWidth, 64);
});

test('TheTooltip_Opens8UnderTheSwitch_RightAligned', () => {
  const tip = CornerLayout.SpeedTooltip(400, 24);
  assert.equal(tip.Y, CornerLayout.SpeedButton.Bottom + 8);
  assert.equal(tip.Right, CornerLayout.SpeedButton.Right);
  assert.equal(tip.Width, 400 + 24, '12 px each side');
  assert.equal(tip.Height, 24 + 16, '8 px above and below');
});

test('TheDemosSurrender_OverlapsNeitherTheJournalNorTheSwitchNorItsTooltip', () => {
  const surrender = CornerLayout.DemoSurrender;
  assert.equal(surrender.Overlaps(CornerLayout.JournalButton), false, 'journal ' + CornerLayout.JournalButton);
  assert.equal(surrender.Overlaps(CornerLayout.SpeedButton), false, 'speed ' + CornerLayout.SpeedButton);
  assert.equal(surrender.Overlaps(WidestTooltip()), false, 'tooltip ' + WidestTooltip());
  // Under the corner info, and still on the canvas.
  assert.ok(surrender.Y > CornerLayout.CornerInfoBottom);
  assert.ok(surrender.X >= 0);
  assert.ok(surrender.Bottom < CornerLayout.CanvasHeight);
  assert.equal(surrender.Width, 200);
  assert.equal(surrender.Height, 56);
});

test('Overlaps_CountsSharedAreaOnly', () => {
  const a = new ScreenBox(0, 0, 10, 10);
  assert.equal(a.Overlaps(new ScreenBox(5, 5, 10, 10)), true);
  assert.equal(a.Overlaps(new ScreenBox(10, 0, 10, 10)), false, 'touching along an edge');
  assert.equal(a.Overlaps(new ScreenBox(0, 20, 10, 10)), false);
});

// ---- port-specific (checked against the C#) ----

test('Port_ScreenBox_IsAReadonlyStructOfFloats', () => {
  const b = new ScreenBox(0.1, 0.2, 0.3, 0.4);
  assert.equal(b.X, Math.fround(0.1));
  assert.equal(b.Right, Math.fround(Math.fround(0.1) + Math.fround(0.3)));
  assert.ok(Object.isFrozen(b));
  assert.throws(() => new ScreenBox(0, 0, -1, 0), { name: 'ArgumentOutOfRangeException', message: "A box has no negative size. (Parameter 'width')" });
  assert.throws(() => new ScreenBox(0, 0, 1, -1), { name: 'ArgumentOutOfRangeException', message: "A box has no negative size. (Parameter 'width')" });
});

test('Port_ScreenBox_ToString_WritesFloatsAsDotNetDoes', () => {
  assert.equal(CornerLayout.SpeedButton.ToString(), '(1708, 20, 120 x 56)');
  assert.equal(String(CornerLayout.DemoSurrender), '(24, 76, 200 x 56)');
  assert.equal(new ScreenBox(0.1, -0, 1e10, 0.0001234).ToString(), '(0.1, -0, 1E+10 x 0.0001234)');
  assert.equal(new ScreenBox(123456789, 1e-5, 12345.678, NaN).ToString(), '(123456790, 1E-05, 12345.678 x NaN)');
  assert.equal(new ScreenBox(-9999998000, 3.4028235e38, 263.671875, 1e9).ToString(), '(-9.999998E+09, 3.4028235E+38, 263.67188 x 1E+09)');
});
