// Tests for src/bridge/depictionScript/BottomRightLayout.js (port of View/Depiction/Script/BottomRightLayout.cs).
// Ported cases: Assets/View/Depiction/Tests/BottomRightLayoutTests.cs (all; the PlayedFrame tally is
// DepictionTypes' type and runs on that module).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { PlayedLayout, ReserveHintLayout } from '../../src/bridge/depictionScript/BottomRightLayout.js';
import { CardKind, PlayedFrame } from '../../src/bridge/depictionScript/DepictionTypes.js';

test('ThePlayedFrames_Are40Square_6Apart_24FromTheRight_AtY896', () => {
  assert.deepEqual(PlayedLayout.Order, [CardKind.Attack, CardKind.Guard, CardKind.Skill, CardKind.Stance]);
  assert.equal(PlayedLayout.LeftOf(3) + PlayedLayout.Size, 1920 - 24);
  assert.equal(PlayedLayout.LeftOf(1) - PlayedLayout.LeftOf(0), 46);
  assert.equal(PlayedLayout.LeftOf(0), 1920 - 24 - 178);
  assert.equal(PlayedLayout.Top, 896);
  assert.equal(PlayedLayout.BadgeSize, 20);
  assert.equal(PlayedLayout.BadgeText, 14);
  assert.equal(PlayedLayout.UnplayedAlpha, Math.fround(0.55));
});

test('TheFollowUpBadge_StandsAtTheRowsLeftEnd_OneGapBeforeAttack', () => {
  // battle_ui_ux_v2 §1.1 B6: 追撃の予約中は「+5」のバッジを左端に出す (#455).
  assert.equal(PlayedLayout.FollowUpRight, PlayedLayout.LeftOf(0) - 6);
  assert.equal(PlayedLayout.FollowUpRight - PlayedLayout.Size, 1672, "a 40 px frame from x 1672, over the end-turn plate's left edge");
  assert.equal(PlayedLayout.FollowUpLabel(5), '+5');
  assert.equal(PlayedLayout.FollowUpLabel(10), '+10');
  assert.equal(PlayedLayout.FollowUpLabel(0), '');
  assert.equal(PlayedLayout.FollowUpPopMs, 150);
  assert.equal(PlayedLayout.FollowUpVanishMs, 150);
  assert.equal(new PlayedFrame().FollowUp, 0);
});

test('ThePlayedFrames_EndAboveTheEndTurnPlate', () => {
  // §4.9 ターン終了: right 24, y 976, 224×72. The frames end at y 936 and the underline 6 px below.
  assert.ok(PlayedLayout.Top + PlayedLayout.Size + 6 < 976);
});

test('TheReserveHint_StandsAtX1560Y984_ClearOfTheEndTurnPlate', () => {
  assert.equal(ReserveHintLayout.Left, 1560);
  assert.equal(ReserveHintLayout.Top, 984);
  assert.equal(ReserveHintLayout.Height, 56);
  assert.equal(ReserveHintLayout.ShieldSize, 30);
  assert.equal(ReserveHintLayout.TextSize, 24);
  assert.ok(ReserveHintLayout.Left + ReserveHintLayout.Width < 1920 - 24 - 224, 'the plate starts at x 1672');
});

test('ThePlayedTally_CountsEachAttribute_AndKeepsTheLast', () => {
  const played = new PlayedFrame({ Shown: true });
  played.Add(CardKind.Attack);
  played.Add(CardKind.Attack);
  played.Add(CardKind.Guard);
  played.Add(CardKind.Move); // movement is no attribute (battle_core_v4 §2.1): not counted, not last
  assert.equal(played.CountOf(CardKind.Attack), 2);
  assert.equal(played.CountOf(CardKind.Guard), 1);
  assert.equal(played.CountOf(CardKind.Skill), 0);
  assert.equal(played.CountOf(CardKind.Move), 0);
  assert.equal(played.Last, CardKind.Guard);
});

// ---- port-specific (checked against the C#) ----

test('Port_TheSpanAndTheWidths', () => {
  assert.equal(PlayedLayout.SpanWidth, 178);
  assert.equal(PlayedLayout.FollowUpRight, 1712);
  assert.deepEqual([0, 1, 2, 3].map((i) => PlayedLayout.LeftOf(i)), [1718, 1764, 1810, 1856]);
  assert.equal(ReserveHintLayout.Width, 98);
  assert.equal(PlayedLayout.FollowUpLabel(-1), '');
});

test('Port_AFrameOutsideTheRow_ThrowsWithDotNetsMessage', () => {
  for (const i of [-1, 4]) {
    assert.throws(() => PlayedLayout.LeftOf(i), { name: 'ArgumentOutOfRangeException', message: "Specified argument was out of the range of valid values. (Parameter 'index')" });
  }
});
