// Tests for src/bridge/depictionScript/StatusPanelLayout.js (port of View/Depiction/Script/StatusPanelLayout.cs).
// Ported cases: Assets/View/Depiction/Tests/StatusPanelLayoutTests.cs (all).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { StatusPanelLayout } from '../../src/bridge/depictionScript/StatusPanelLayout.js';
import { CornerLayout } from '../../src/bridge/depictionScript/CornerLayout.js';

const near = (a, b, tol, msg) => assert.ok(Math.abs(a - b) <= tol, `${msg || ''} expected ${b} ± ${tol}, got ${a}`);

/** The lines §4.2 draws: 5 to 8 cells. */
const Lines = [5, 6, 7, 8];

/** Every way the two can stand at `gap`: the player (1 cell) on each cell that leaves room, an enemy of 1 or 2 cells right after the gap. */
function* Standings(gap) {
  for (const cells of Lines) {
    for (let enemySize = 1; enemySize <= 2; enemySize++) {
      for (let playerCell = 1; playerCell <= cells; playerCell++) {
        const enemyCell = playerCell + 1 + gap;
        if (enemyCell + enemySize - 1 > cells) continue;
        yield [cells, playerCell, enemyCell, enemySize];
      }
    }
  }
}

test('TheWidths_AreSection4_5s', () => {
  assert.equal(StatusPanelLayout.PlayerWidth, 300);
  // 6 cells of 220: 210 is exactly a cell less 10.
  assert.equal(StatusPanelLayout.EnemyPanelWidth(6, 1), 210);
  assert.equal(StatusPanelLayout.EnemyPanelWidth(5, 1), 210, '264 - 10 leaves room for 210');
  assert.equal(StatusPanelLayout.EnemyPanelWidth(6, 2), 272);
  // 8 cells of 165: no wider than the cell less 10.
  assert.equal(StatusPanelLayout.EnemyPanelWidth(8, 1), 155);
  assert.equal(StatusPanelLayout.EnemyPanelWidth(8, 2), 272, 'two cells of 165 leave 320');
  assert.equal(StatusPanelLayout.EnemyPanelWidth(0, 1), 210, 'no floor, no cap');
});

test('TheHeight_IsTwoRowsAndTheChipsInsideThePadding', () => {
  // 8 + 24 (HP) + 8 + 28 (Guard) + 10.
  assert.equal(StatusPanelLayout.Height(0), 78);
  // + 8 + 30 for one row of chips, + 5 + 30 for each row after.
  assert.equal(StatusPanelLayout.Height(1), 116);
  assert.equal(StatusPanelLayout.Height(2), 151);
  assert.equal(StatusPanelLayout.ChipRowTop, 76);
});

for (let gap = 0; gap <= 5; gap++) {
  test(`ThePanels_NeverOverlap_AtAnyGapFromZeroToFive(${gap})`, () => {
    let checkedStandings = 0;
    for (const [cells, playerCell, enemyCell, enemySize] of Standings(gap)) {
      const { player, enemy } = StatusPanelLayout.OnFloor(cells, playerCell, 1, enemyCell, enemySize, StatusPanelLayout.Height(2));
      const at = `${cells} cells, player on ${playerCell}, enemy (${enemySize}) on ${enemyCell}: player ${player}, enemy ${enemy}`;
      assert.equal(player.Overlaps(enemy), false, at);
      assert.ok(enemy.X - player.Right >= StatusPanelLayout.MinGap - 0.001, at);
      assert.ok(player.X >= StatusPanelLayout.EdgeInset, at);
      assert.ok(enemy.Right <= CornerLayout.CanvasWidth - StatusPanelLayout.EdgeInset, at);
      assert.equal(player.Y, 614, at);
      assert.equal(enemy.Y, 614, at);
      checkedStandings++;
    }
    assert.ok(checkedStandings > 0, 'gap ' + gap + ' fits on the 8-cell line at least');
  });
}

test('WithRoom_EachPanelIsCentredOnItsFigure', () => {
  // 6 cells, player on 1 (x 410), enemy on 6 (x 1510): far apart, nothing moves them.
  const { player, enemy } = StatusPanelLayout.OnFloor(6, 1, 1, 6, 1, 78);
  assert.equal(player.X + player.Width * 0.5, 410);
  assert.equal(enemy.X + enemy.Width * 0.5, 1510);
  assert.equal(player.Width, 300);
  assert.equal(enemy.Width, 210);
});

test('AtGapZero_BothMoveApartByTheSameAmount', () => {
  // 6 cells, player on 3 (x 850), enemy on 4 (x 1070): centred, they would share 35 px.
  const { player, enemy } = StatusPanelLayout.OnFloor(6, 3, 1, 4, 1, 78);
  // 150 + 105 + 8 - 220 = 43 px short: each moves 21.5 px.
  const playerShift = 850 - (player.X + player.Width * 0.5);
  const enemyShift = enemy.X + enemy.Width * 0.5 - 1070;
  near(playerShift, 21.5, 0.001);
  near(enemyShift, 21.5, 0.001);
  near(enemy.X - player.Right, StatusPanelLayout.MinGap, 0.001);
});

test('AtTheEdge_ThePanelStopsAndTheOtherTakesTheRest', () => {
  // 8 cells, player on 1 (x 382.5), enemy on 2 (x 547.5): the player's panel would go past
  // x 24 if it took half the push, so it stops at 24 and the enemy's moves the rest.
  let { playerLeft, enemyLeft } = StatusPanelLayout.Place(100, 300, 200, 155);
  assert.equal(playerLeft, 24);
  assert.equal(enemyLeft, 24 + 300 + StatusPanelLayout.MinGap);

  ({ playerLeft, enemyLeft } = StatusPanelLayout.Place(1800, 300, 1880, 210));
  assert.equal(enemyLeft + 210, 1920 - 24);
  near(enemyLeft - (playerLeft + 300), StatusPanelLayout.MinGap, 0.001);
});

test('EvenWhenTheFiguresCross_ThePlayersPanelStaysLeftAndClear', () => {
  // A lunge can carry a figure past the other for a moment: the panels still keep apart.
  const { playerLeft, enemyLeft } = StatusPanelLayout.Place(1000, 300, 900, 210);
  assert.ok(enemyLeft - (playerLeft + 300) >= StatusPanelLayout.MinGap - 0.001);
});

// ---- port-specific (checked against the C#) ----

test('Port_ANegativeWidth_ThrowsWithDotNetsMessage_AndTheContentWidth', () => {
  assert.throws(() => StatusPanelLayout.Place(0, -1, 0, 10), { name: 'ArgumentOutOfRangeException', message: "A panel has no negative width. (Parameter 'playerWidth')" });
  assert.equal(StatusPanelLayout.ContentWidth(300), 280);
  assert.equal(StatusPanelLayout.ContentWidth(10), 0);
  assert.equal(StatusPanelLayout.Height(-1), 78);
  assert.equal(StatusPanelLayout.EnemyPanelWidth(7, 1), Math.fround(Math.fround(1320 / 7) - 10));
});
