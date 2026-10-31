// Tests for src/bridge/depictionScript/StanceList.js (port of View/Depiction/Script/StanceList.cs).
// Ported cases: Assets/View/Depiction/Tests/StanceListTests.cs (all).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { StanceList } from '../../src/bridge/depictionScript/StanceList.js';
import { StanceRow, UnitSide } from '../../src/bridge/depictionScript/DepictionTypes.js';

const Shown = (placed) => StanceList.Rows(placed).map((r) => r.Name + StanceList.CountText(r.Count));

test('Rows_KeepTheOrderPutDown_TheNewestLast', () => {
  assert.deepEqual(Shown(['岩の構え', '水の構え', '流れの構え']), ['岩の構え', '水の構え', '流れの構え']);
});

test('ARepeat_IsOneRowWithItsCount_MovedToTheEnd', () => {
  assert.deepEqual(Shown(['岩の構え', '水の構え', '岩の構え']), ['水の構え', '岩の構え×2']);
  assert.deepEqual(Shown(['岩の構え', '岩の構え', '岩の構え']), ['岩の構え×3']);
});

test('NothingHeld_IsNoRow_AndOneEmptyFrame', () => {
  assert.deepEqual(StanceList.Rows([]), []);
  assert.deepEqual(StanceList.Rows(null), []);
  assert.deepEqual(StanceList.Rows(['', null]), [], 'a source with no name is left out');
  assert.equal(StanceList.Frames(0), 1, '§4.6 空きの枠');
});

for (const [rows, frames, folded] of [[1, 1, 0], [3, 3, 0], [4, 3, 2], [7, 3, 5]]) {
  test(`ALongList_FoldsItsOldestIntoOneFrameOnTop(${rows}, ${frames}, ${folded})`, () => {
    assert.equal(StanceList.Frames(rows), frames);
    assert.equal(StanceList.Folded(rows), folded);
    if (folded > 0) assert.equal(folded + (frames - 1), rows, 'the frames below the fold show the newest');
  });
}

test('TheFrames_StandOnTheSection46Slot_AndStackUpward', () => {
  assert.equal(StanceList.LeftOf(UnitSide.Player), 24);
  assert.equal(StanceList.LeftOf(UnitSide.Enemy) + StanceList.Width, 1920 - 24, '右から 24');
  assert.equal(StanceList.TopOf(0), 470);
  assert.equal(StanceList.Width, 250);
  assert.equal(StanceList.RowHeight, 52);
  assert.equal(StanceList.TopOf(1), 470 - 58);
  // The lowest frame ends at y 522, clear of the floor's 40 px cells from y 560 (§4.2).
  assert.ok(StanceList.TopOf(0) + StanceList.RowHeight < 560);
});

test('TheCount_IsWrittenFromTwoOn', () => {
  assert.equal(StanceList.CountText(1), '');
  assert.equal(StanceList.CountText(2), '×2');
});

// ---- port-specific (checked against the C#) ----

test('Port_RowsAreStanceRows_InTheOrderTheCsLeavesThem', () => {
  const rows = StanceList.Rows(['', null, 'a', '', 'b', 'a', 'c', 'b', 'a']);
  assert.ok(rows.every((r) => r instanceof StanceRow));
  assert.deepEqual(rows.map((r) => [r.Name, r.Count]), [['c', 1], ['b', 2], ['a', 3]]);
  assert.equal(StanceList.TopOf(-1), 528);
  assert.equal(StanceList.Frames(-2), 1);
});
