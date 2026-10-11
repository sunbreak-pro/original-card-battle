// The two number panels under the figures (Script/StatusPanelLayout.cs, battle-visual-v1 §4.5,
// #455): their widths, and that they never overlap at any gap from 0 to 5 on any line the floor
// draws, while each stays under its figure where there is room and 24 px inside the screen.
using System.Collections.Generic;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class StatusPanelLayoutTests
    {
        /// <summary>The lines §4.2 draws: 5 to 8 cells.</summary>
        private static readonly int[] Lines = { 5, 6, 7, 8 };

        /// <summary>
        /// Every way the two can stand at gap <paramref name="gap"/>: the player (1 cell) on each cell
        /// that leaves room, an enemy of 1 or 2 cells right after the gap.
        /// </summary>
        private static IEnumerable<(int Cells, int PlayerCell, int EnemyCell, int EnemySize)> Standings(int gap)
        {
            foreach (int cells in Lines)
            {
                for (int enemySize = 1; enemySize <= 2; enemySize++)
                {
                    for (int playerCell = 1; playerCell <= cells; playerCell++)
                    {
                        int enemyCell = playerCell + 1 + gap;
                        if (enemyCell + enemySize - 1 > cells) continue;
                        yield return (cells, playerCell, enemyCell, enemySize);
                    }
                }
            }
        }

        [Test]
        public void TheWidths_AreSection4_5s()
        {
            Assert.That(StatusPanelLayout.PlayerWidth, Is.EqualTo(300f));
            // 6 cells of 220: 210 is exactly a cell less 10.
            Assert.That(StatusPanelLayout.EnemyPanelWidth(6, 1), Is.EqualTo(210f));
            Assert.That(StatusPanelLayout.EnemyPanelWidth(5, 1), Is.EqualTo(210f), "264 - 10 leaves room for 210");
            Assert.That(StatusPanelLayout.EnemyPanelWidth(6, 2), Is.EqualTo(272f));
            // 8 cells of 165: no wider than the cell less 10.
            Assert.That(StatusPanelLayout.EnemyPanelWidth(8, 1), Is.EqualTo(155f));
            Assert.That(StatusPanelLayout.EnemyPanelWidth(8, 2), Is.EqualTo(272f), "two cells of 165 leave 320");
            Assert.That(StatusPanelLayout.EnemyPanelWidth(0, 1), Is.EqualTo(210f), "no floor, no cap");
        }

        [Test]
        public void TheHeight_IsTwoRowsAndTheChipsInsideThePadding()
        {
            // 8 + 24 (HP) + 8 + 28 (Guard) + 10.
            Assert.That(StatusPanelLayout.Height(0), Is.EqualTo(78f));
            // + 8 + 30 for one row of chips, + 5 + 30 for each row after.
            Assert.That(StatusPanelLayout.Height(1), Is.EqualTo(116f));
            Assert.That(StatusPanelLayout.Height(2), Is.EqualTo(151f));
            Assert.That(StatusPanelLayout.ChipRowTop, Is.EqualTo(76f));
        }

        [Test]
        public void ThePanels_NeverOverlap_AtAnyGapFromZeroToFive([Range(0, 5)] int gap)
        {
            int checkedStandings = 0;
            foreach (var (cells, playerCell, enemyCell, enemySize) in Standings(gap))
            {
                StatusPanelLayout.OnFloor(cells, playerCell, 1, enemyCell, enemySize, StatusPanelLayout.Height(2),
                    out ScreenBox player, out ScreenBox enemy);
                string at = cells + " cells, player on " + playerCell + ", enemy (" + enemySize + ") on " + enemyCell
                    + ": player " + player + ", enemy " + enemy;
                Assert.That(player.Overlaps(enemy), Is.False, at);
                Assert.That(enemy.X - player.Right, Is.GreaterThanOrEqualTo(StatusPanelLayout.MinGap - 0.001f), at);
                Assert.That(player.X, Is.GreaterThanOrEqualTo(StatusPanelLayout.EdgeInset), at);
                Assert.That(enemy.Right, Is.LessThanOrEqualTo(CornerLayout.CanvasWidth - StatusPanelLayout.EdgeInset), at);
                Assert.That(player.Y, Is.EqualTo(614f), at);
                Assert.That(enemy.Y, Is.EqualTo(614f), at);
                checkedStandings++;
            }
            Assert.That(checkedStandings, Is.GreaterThan(0), "gap " + gap + " fits on the 8-cell line at least");
        }

        [Test]
        public void WithRoom_EachPanelIsCentredOnItsFigure()
        {
            // 6 cells, player on 1 (x 410), enemy on 6 (x 1510): far apart, nothing moves them.
            StatusPanelLayout.OnFloor(6, 1, 1, 6, 1, 78f, out ScreenBox player, out ScreenBox enemy);
            Assert.That(player.X + player.Width * 0.5f, Is.EqualTo(410f));
            Assert.That(enemy.X + enemy.Width * 0.5f, Is.EqualTo(1510f));
            Assert.That(player.Width, Is.EqualTo(300f));
            Assert.That(enemy.Width, Is.EqualTo(210f));
        }

        [Test]
        public void AtGapZero_BothMoveApartByTheSameAmount()
        {
            // 6 cells, player on 3 (x 850), enemy on 4 (x 1070): centred, they would share 35 px.
            StatusPanelLayout.OnFloor(6, 3, 1, 4, 1, 78f, out ScreenBox player, out ScreenBox enemy);
            // 150 + 105 + 8 - 220 = 43 px short: each moves 21.5 px.
            float playerShift = 850f - (player.X + player.Width * 0.5f);
            float enemyShift = (enemy.X + enemy.Width * 0.5f) - 1070f;
            Assert.That(playerShift, Is.EqualTo(21.5f).Within(0.001f));
            Assert.That(enemyShift, Is.EqualTo(21.5f).Within(0.001f));
            Assert.That(enemy.X - player.Right, Is.EqualTo(StatusPanelLayout.MinGap).Within(0.001f));
        }

        [Test]
        public void AtTheEdge_ThePanelStopsAndTheOtherTakesTheRest()
        {
            // 8 cells, player on 1 (x 382.5), enemy on 2 (x 547.5): the player's panel would go past
            // x 24 if it took half the push, so it stops at 24 and the enemy's moves the rest.
            StatusPanelLayout.Place(100f, 300f, 200f, 155f, out float playerLeft, out float enemyLeft);
            Assert.That(playerLeft, Is.EqualTo(24f));
            Assert.That(enemyLeft, Is.EqualTo(24f + 300f + StatusPanelLayout.MinGap));

            StatusPanelLayout.Place(1800f, 300f, 1880f, 210f, out playerLeft, out enemyLeft);
            Assert.That(enemyLeft + 210f, Is.EqualTo(1920f - 24f));
            Assert.That(enemyLeft - (playerLeft + 300f), Is.EqualTo(StatusPanelLayout.MinGap).Within(0.001f));
        }

        [Test]
        public void EvenWhenTheFiguresCross_ThePlayersPanelStaysLeftAndClear()
        {
            // A lunge can carry a figure past the other for a moment: the panels still keep apart.
            StatusPanelLayout.Place(1000f, 300f, 900f, 210f, out float playerLeft, out float enemyLeft);
            Assert.That(enemyLeft - (playerLeft + 300f), Is.GreaterThanOrEqualTo(StatusPanelLayout.MinGap - 0.001f));
        }
    }
}
