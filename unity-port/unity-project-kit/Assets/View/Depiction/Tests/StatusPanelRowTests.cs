// The number panels of a battle with two enemies (Script/StatusPanelLayout.cs, #589): the player's
// and one per enemy never overlap on any line the floor draws, however the three stand, and each
// stays 24 px inside the screen. A battle with one enemy is laid out exactly as the pair always was.
using System.Collections.Generic;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class StatusPanelRowTests
    {
        /// <summary>The lines §4.2 draws: 5 to 8 cells.</summary>
        private static readonly int[] Lines = { 5, 6, 7, 8 };

        /// <summary>
        /// Every way the player (1 cell) and two enemies of 1 or 2 cells can stand on a line, the
        /// player left of both, nobody on another's cell, at any gap.
        /// </summary>
        private static IEnumerable<(int Cells, int PlayerCell, int[] EnemyCells, int[] EnemySizes)> ThreeStandings()
        {
            foreach (int cells in Lines)
            {
                for (int firstSize = 1; firstSize <= 2; firstSize++)
                for (int secondSize = 1; secondSize <= 2; secondSize++)
                for (int playerCell = 1; playerCell <= cells; playerCell++)
                for (int first = playerCell + 1; first + firstSize - 1 <= cells; first++)
                for (int second = first + firstSize; second + secondSize - 1 <= cells; second++)
                {
                    yield return (cells, playerCell, new[] { first, second }, new[] { firstSize, secondSize });
                }
            }
        }

        [Test]
        public void ThreePanels_NeverOverlap_AndStayInside_WhereverTheThreeStand()
        {
            int checkedCount = 0;
            foreach (var (cells, playerCell, enemyCells, enemySizes) in ThreeStandings())
            {
                ScreenBox[] boxes = StatusPanelLayout.OnFloor(cells, playerCell, 1, enemyCells, enemySizes, StatusPanelLayout.Height(1));
                string where = cells + " cells, player " + playerCell + ", enemies " + enemyCells[0] + "×" + enemySizes[0] + " and " + enemyCells[1] + "×" + enemySizes[1];

                Assert.That(boxes.Length, Is.EqualTo(3), where);
                for (int a = 0; a < boxes.Length; a++)
                {
                    Assert.That(boxes[a].X, Is.GreaterThanOrEqualTo(StatusPanelLayout.EdgeInset - 0.001f), where);
                    Assert.That(boxes[a].Right, Is.LessThanOrEqualTo(CornerLayout.CanvasWidth - StatusPanelLayout.EdgeInset + 0.001f), where);
                    for (int b = a + 1; b < boxes.Length; b++)
                    {
                        Assert.That(boxes[a].Overlaps(boxes[b]), Is.False, where + ": panels " + a + " and " + b);
                    }
                }
                // The player's panel stays on the left, and the enemies' keep the order they stand in.
                Assert.That(boxes[0].Right, Is.LessThan(boxes[1].X), where);
                Assert.That(boxes[1].Right, Is.LessThan(boxes[2].X), where);
                checkedCount++;
            }
            Assert.That(checkedCount, Is.GreaterThan(100), "the standings cover the lines");
        }

        [Test]
        public void AnEnemyPanel_StaysUnderItsFigure_WhenThereIsRoom()
        {
            // 8 cells: the player on 1, the enemies on 4 and 7, far enough apart for each panel to centre.
            ScreenBox[] boxes = StatusPanelLayout.OnFloor(8, 1, 1, new[] { 4, 7 }, new[] { 1, 1 }, 78f);

            Assert.That(boxes[1].X + boxes[1].Width * 0.5f, Is.EqualTo(FloorSpec.FeetX(8, 4, 1)).Within(0.01f));
            Assert.That(boxes[2].X + boxes[2].Width * 0.5f, Is.EqualTo(FloorSpec.FeetX(8, 7, 1)).Within(0.01f));
        }

        [Test]
        public void TheEnemiesBoxes_FollowTheOrderGiven_NotWhereTheyStand()
        {
            // The second enemy given stands nearer: its box is still the third, and it is the left one.
            ScreenBox[] boxes = StatusPanelLayout.OnFloor(8, 1, 1, new[] { 7, 3 }, new[] { 1, 1 }, 78f);

            Assert.That(boxes[2].X, Is.LessThan(boxes[1].X));
            Assert.That(boxes[0].Right, Is.LessThan(boxes[2].X));
        }

        [Test]
        public void OneEnemy_IsLaidOut_AsThePairAlwaysWas([Range(0, 4)] int gap)
        {
            foreach (int cells in Lines)
            {
                for (int enemySize = 1; enemySize <= 2; enemySize++)
                {
                    for (int playerCell = 1; playerCell <= cells; playerCell++)
                    {
                        int enemyCell = playerCell + 1 + gap;
                        if (enemyCell + enemySize - 1 > cells) continue;
                        StatusPanelLayout.OnFloor(cells, playerCell, 1, enemyCell, enemySize, 78f, out ScreenBox player, out ScreenBox enemy);
                        ScreenBox[] row = StatusPanelLayout.OnFloor(cells, playerCell, 1, new[] { enemyCell }, new[] { enemySize }, 78f);

                        Assert.That(row[0].X, Is.EqualTo(player.X), cells + "/" + playerCell);
                        Assert.That(row[1].X, Is.EqualTo(enemy.X), cells + "/" + playerCell);
                        Assert.That(row[1].Width, Is.EqualTo(enemy.Width), cells + "/" + playerCell);
                    }
                }
            }
        }

        [Test]
        public void ARowOfFour_CrowdedAtTheRight_IsPushedBackInside()
        {
            float[] lefts = StatusPanelLayout.PlaceRow(new[] { 1800f, 1850f, 1900f, 1910f }, new[] { 300f, 210f, 210f, 210f });

            for (int i = 1; i < lefts.Length; i++)
            {
                Assert.That(lefts[i], Is.GreaterThanOrEqualTo(lefts[i - 1] + (i == 1 ? 300f : 210f) + StatusPanelLayout.MinGap - 0.001f));
            }
            Assert.That(lefts[3] + 210f, Is.LessThanOrEqualTo(CornerLayout.CanvasWidth - StatusPanelLayout.EdgeInset + 0.001f));
        }
    }
}
