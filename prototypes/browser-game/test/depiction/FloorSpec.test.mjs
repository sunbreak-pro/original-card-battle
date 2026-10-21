// Tests for src/bridge/depictionScript/FloorSpec.js (port of View/Depiction/Script/FloorSpec.cs).
// Ported cases: Assets/View/Depiction/Tests/FloorSpecTests.cs (all).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { FloorSpec } from '../../src/bridge/depictionScript/FloorSpec.js';

const near = (a, b, tol, msg) => assert.ok(Math.abs(a - b) <= tol, `${msg || ''} expected ${b} ± ${tol}, got ${a}`);

for (const [cells, width] of [[6, 220], [8, 165], [5, 264]]) {
  test(`TheBand_IsSplitEvenlyByTheCells(${cells})`, () => {
    near(FloorSpec.CellWidth(cells), width, 1e-3);
    near(FloorSpec.TileWidth(cells), width - 6, 1e-3);
  });
}

test('TheCells_RunFromX300ToX1620', () => {
  near(FloorSpec.TileLeft(6, 1), 303, 1e-3);
  near(FloorSpec.TileLeft(6, 6) + FloorSpec.TileWidth(6), 1617, 1e-3);
});

test('AFigure_StandsInTheMiddleOfItsCells', () => {
  near(FloorSpec.FeetX(6, 2, 1), 630, 1e-3);
  near(FloorSpec.FeetX(6, 6, 1), 1510, 1e-3);
  near(FloorSpec.FeetX(6, 5, 2), 1400, 1e-3, 'a large elite on cells 5 and 6');
});

test('TheGapNumber_SpansTheEmptyCells', () => {
  // Player on 2, enemy on 6: cells 3〜5 are empty, gap 3.
  let { from, to } = FloorSpec.GapSpan(6, 2, 1, 6);
  near(from, 740, 1e-3);
  near(to, 1400, 1e-3);

  // Gap 0: both ends are the border the two cells share.
  ({ from, to } = FloorSpec.GapSpan(6, 2, 1, 3));
  near(from, to, 1e-3);
});

// ---- port-specific (checked against the C#) ----

test('Port_AnEightCellLine_IsInFloats', () => {
  assert.equal(FloorSpec.CellWidth(8), 165);
  assert.equal(FloorSpec.CellWidth(7), Math.fround(1320 / 7));
  assert.equal(FloorSpec.CellWidth(0), 1320, 'no cells reads as one');
  assert.equal(FloorSpec.FeetX(8, 1, 1), 382.5);
  assert.equal(FloorSpec.FeetX(8, 1, 0), 382.5, 'a size under 1 reads as 1');
});
