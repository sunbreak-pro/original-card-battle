// Tests for src/bridge/depictionScript/ReceiverDrop.js (port of View/Depiction/Script/ReceiverDrop.cs).
// Ported cases: Assets/View/Depiction/Tests/ReceiverDropTests.cs (all).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ReceiverDrop } from '../../src/bridge/depictionScript/ReceiverDrop.js';
import { ScreenBox } from '../../src/bridge/depictionScript/CornerLayout.js';

// The receiver as the prefab builds it (340 x 340) and a hand card (216 x 304), on the 1920 x 1080 canvas.
const Receiver = new ScreenBox(1190, 170, 340, 340);
const CardAt = (x, y) => new ScreenBox(x, y, 216, 304);

test('ACardWhollyOnTheReceiver_SharesAllOfItsArea', () => {
  assert.equal(ReceiverDrop.Share(CardAt(1200, 180), Receiver), 1);
  assert.equal(ReceiverDrop.Takes(CardAt(1200, 180), Receiver, 1000, 100), true);
});

test('ExactlyHalfTheCard_OnTheReceiver_IsTaken_WithThePointerOutside', () => {
  // 108 of the card's 216 px lie inside, the whole height does.
  const card = CardAt(1190 - 108, 188);
  assert.equal(ReceiverDrop.Share(card, Receiver), 0.5);
  assert.equal(ReceiverDrop.Takes(card, Receiver, 1000, 300), true);
});

test('LessThanHalf_WithThePointerOutside_IsNotTaken', () => {
  const card = CardAt(1190 - 109, 188);
  assert.ok(ReceiverDrop.Share(card, Receiver) < 0.5);
  assert.equal(ReceiverDrop.Takes(card, Receiver, 1000, 300), false);
});

test('ThePointerInside_IsTaken_EvenWithLittleOverlap', () => {
  // Held by its right edge: 16 px of the card lie inside, the pointer does too.
  const card = CardAt(1190 - 200, 188);
  assert.ok(ReceiverDrop.Share(card, Receiver) < 0.1);
  assert.equal(ReceiverDrop.Takes(card, Receiver, 1195, 300), true);
});

test('NoOverlap_WithThePointerOutside_IsNotTaken', () => {
  const card = CardAt(800, 188);
  assert.equal(ReceiverDrop.Share(card, Receiver), 0);
  assert.equal(ReceiverDrop.Takes(card, Receiver, 900, 300), false);
});

test('ACardTouchingTheEdge_SharesNothing_AndThePointerCountsOnTheLeftAndTopEdgesOnly', () => {
  assert.equal(ReceiverDrop.Share(CardAt(1190 - 216, 188), Receiver), 0);
  const away = CardAt(0, 0);
  assert.equal(ReceiverDrop.Takes(away, Receiver, 1190, 170), true);
  assert.equal(ReceiverDrop.Takes(away, Receiver, 1530, 300), false);
  assert.equal(ReceiverDrop.Takes(away, Receiver, 1300, 510), false);
});

// ---- port-specific (checked against the C#) ----

test('Port_ACardWithNoArea_SharesNothing_AndTheShareIsAFloat', () => {
  assert.equal(ReceiverDrop.Share(new ScreenBox(1200, 180, 0, 304), Receiver), 0);
  assert.equal(ReceiverDrop.TakeShare, 0.5);
  const s = ReceiverDrop.Share(new ScreenBox(1100.3, 188, 216, 304), Receiver);
  assert.equal(s, Math.fround(s), 'a float32 value');
});
