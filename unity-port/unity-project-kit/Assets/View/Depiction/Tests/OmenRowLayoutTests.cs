// The omen badges of a battle with several enemies (Script/OmenRowLayout.cs, #589): battle_ui_ux_v2 §2.2
// の 2 and §5.8. On any line the floor draws, however the enemies stand, no badge and no plan lies on
// another, the order on the screen is the order of the figures, and every badge stays inside the screen.
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class OmenRowLayoutTests
    {
        private static readonly int[] Lines = { 5, 6, 7, 8 };

        /// <summary>Every way two enemies of 1 or 2 cells can stand on a line, the first given on the left or the right.</summary>
        private static IEnumerable<(int Cells, int[] EnemyCells, int[] EnemySizes)> Pairs()
        {
            foreach (int cells in Lines)
            {
                for (int firstSize = 1; firstSize <= 2; firstSize++)
                for (int secondSize = 1; secondSize <= 2; secondSize++)
                for (int first = 2; first + firstSize - 1 <= cells; first++)
                for (int second = first + firstSize; second + secondSize - 1 <= cells; second++)
                {
                    yield return (cells, new[] { first, second }, new[] { firstSize, secondSize });
                    yield return (cells, new[] { second, first }, new[] { secondSize, firstSize });
                }
            }
        }

        /// <summary>Every way three enemies of 1 or 2 cells can stand on a line (§2.2: three at most).</summary>
        private static IEnumerable<(int Cells, int[] EnemyCells, int[] EnemySizes)> Trios()
        {
            foreach (int cells in Lines)
            {
                for (int a = 1; a <= 2; a++)
                for (int b = 1; b <= 2; b++)
                for (int c = 1; c <= 2; c++)
                for (int first = 2; first + a - 1 <= cells; first++)
                for (int second = first + a; second + b - 1 <= cells; second++)
                for (int third = second + b; third + c - 1 <= cells; third++)
                {
                    yield return (cells, new[] { first, second, third }, new[] { a, b, c });
                    yield return (cells, new[] { third, first, second }, new[] { c, a, b });
                }
            }
        }

        private static IEnumerable<(int Cells, int[] EnemyCells, int[] EnemySizes)> AllStandings()
        {
            foreach (var stand in Pairs()) yield return stand;
            foreach (var stand in Trios()) yield return stand;
        }

        private static float[] Centres(int cells, int[] enemyCells, int[] enemySizes)
        {
            var centres = new float[enemyCells.Length];
            for (int i = 0; i < centres.Length; i++) centres[i] = FloorSpec.FeetX(cells, enemyCells[i], enemySizes[i]);
            return centres;
        }

        private static string Say((int Cells, int[] EnemyCells, int[] EnemySizes) stand)
        {
            return stand.Cells + " cells, enemies on " + string.Join(",", stand.EnemyCells) + " taking " + string.Join(",", stand.EnemySizes);
        }

        [Test]
        public void NoBadgeLiesOnAnother_OnAnyStand()
        {
            foreach (var stand in AllStandings())
            {
                float[] centres = Centres(stand.Cells, stand.EnemyCells, stand.EnemySizes);
                float[] lefts = OmenRowLayout.Lefts(centres);
                for (int a = 0; a < lefts.Length; a++)
                for (int b = a + 1; b < lefts.Length; b++)
                {
                    float apart = Math.Abs(lefts[a] - lefts[b]);
                    Assert.That(apart, Is.GreaterThanOrEqualTo(OmenRowLayout.BadgeWidth + StatusPanelLayout.MinGap - 0.01f), Say(stand));
                }
            }
        }

        [Test]
        public void ThePlanAboveABadge_ClearsTheNeighbours_OnAnyStand()
        {
            foreach (var stand in AllStandings())
            {
                float[] lefts = OmenRowLayout.Lefts(Centres(stand.Cells, stand.EnemyCells, stand.EnemySizes));
                for (int a = 0; a < lefts.Length; a++)
                {
                    float planLeft = OmenRowLayout.PlanLeft(lefts[a]);
                    Assert.That(planLeft, Is.GreaterThanOrEqualTo(lefts[a]), "the plan stays over its own badge: " + Say(stand));
                    Assert.That(planLeft + OmenRowLayout.PlanWidth, Is.LessThanOrEqualTo(lefts[a] + OmenRowLayout.BadgeWidth), Say(stand));
                    for (int b = 0; b < lefts.Length; b++)
                    {
                        if (a == b) continue;
                        // Against the other's badge: the rows are the same, so only the x can part them.
                        bool clearOfBadge = planLeft + OmenRowLayout.PlanWidth <= lefts[b] || lefts[b] + OmenRowLayout.BadgeWidth <= planLeft;
                        Assert.That(clearOfBadge, Is.True, Say(stand));
                        float otherPlan = OmenRowLayout.PlanLeft(lefts[b]);
                        bool clearOfPlan = planLeft + OmenRowLayout.PlanWidth <= otherPlan || otherPlan + OmenRowLayout.PlanWidth <= planLeft;
                        Assert.That(clearOfPlan, Is.True, Say(stand));
                    }
                }
            }
        }

        [Test]
        public void TheOrderOnTheScreen_IsTheOrderOfTheFigures_OnAnyStand()
        {
            foreach (var stand in AllStandings())
            {
                float[] centres = Centres(stand.Cells, stand.EnemyCells, stand.EnemySizes);
                float[] lefts = OmenRowLayout.Lefts(centres);
                for (int a = 0; a < lefts.Length; a++)
                for (int b = 0; b < lefts.Length; b++)
                {
                    if (centres[a] < centres[b]) Assert.That(lefts[a], Is.LessThan(lefts[b]), Say(stand));
                }
            }
        }

        [Test]
        public void EveryBadge_StaysInsideTheScreen_OnAnyStand()
        {
            foreach (var stand in AllStandings())
            {
                foreach (float left in OmenRowLayout.Lefts(Centres(stand.Cells, stand.EnemyCells, stand.EnemySizes)))
                {
                    Assert.That(left, Is.GreaterThanOrEqualTo(StatusPanelLayout.EdgeInset - 0.01f), Say(stand));
                    Assert.That(left + OmenRowLayout.BadgeWidth, Is.LessThanOrEqualTo(CornerLayout.CanvasWidth - StatusPanelLayout.EdgeInset + 0.01f), Say(stand));
                }
            }
        }

        [Test]
        public void NoBadgeSlidesPast75Px_OnAnyStand()
        {
            foreach (var stand in AllStandings())
            {
                float[] centres = Centres(stand.Cells, stand.EnemyCells, stand.EnemySizes);
                float[] lefts = OmenRowLayout.Lefts(centres);
                for (int i = 0; i < lefts.Length; i++)
                {
                    Assert.That(Math.Abs(OmenRowLayout.Slide(lefts[i], centres[i])), Is.LessThanOrEqualTo(OmenRowLayout.SlideMax), Say(stand));
                }
            }
        }

        [Test]
        public void BadgesWithRoom_StayOnTheirHeads()
        {
            float[] centres = { 600f, 1200f };
            float[] lefts = OmenRowLayout.Lefts(centres);
            Assert.That(OmenRowLayout.Slide(lefts[0], centres[0]), Is.EqualTo(0f));
            Assert.That(OmenRowLayout.Slide(lefts[1], centres[1]), Is.EqualTo(0f));
        }

        [Test]
        public void TwoBadgesThatWouldOverlap_SlideApartEvenly()
        {
            // Neighbours on 8 cells stand 165 apart; a badge is 200 wide with 8 kept between: 43 short.
            float[] centres = { 900f, 1065f };
            float[] lefts = OmenRowLayout.Lefts(centres);
            Assert.That(OmenRowLayout.Slide(lefts[0], centres[0]), Is.EqualTo(-21.5f).Within(0.01f));
            Assert.That(OmenRowLayout.Slide(lefts[1], centres[1]), Is.EqualTo(21.5f).Within(0.01f));
        }

        [Test]
        public void EveryBadge_SharesOneRow_OverTheHighestHead()
        {
            // §5.16: a 264 px elite beside a 226 px enemy; 14 px over the elite's head, feet on y 570.
            Assert.That(OmenRowLayout.RowBottom(570f, new[] { FigureFrame.StandardHeight, FigureFrame.LargeHeight }), Is.EqualTo(570f - 264f - 14f));
            Assert.That(OmenRowLayout.RowBottom(570f, new[] { FigureFrame.StandardHeight }), Is.EqualTo(570f - 226f - 14f));
        }

        [Test]
        public void OneBadge_StandsOnItsHead_KeptInsideTheEdges()
        {
            Assert.That(OmenRowLayout.Lefts(new[] { 900f })[0], Is.EqualTo(800f));
            Assert.That(OmenRowLayout.Lefts(new[] { 50f })[0], Is.EqualTo(StatusPanelLayout.EdgeInset));
            Assert.That(OmenRowLayout.Lefts(new float[0]), Is.Empty);
        }
    }
}
