// Where the two number panels sit under the figures (battle-visual-v1 §4.5, #455). Pure arithmetic
// with no engine types, like CornerLayout and FloorSpec, so the EditMode tests hold it directly.
// Boxes are in pixels on the 1920 x 1080 reference canvas, y counted downwards from the top.
//
// §4.5 puts each panel under its own figure, centred on it, 24 px inside the screen's edges. At a
// gap of 0 the two figures stand one cell apart (165 px on an 8-cell line), which is less than half
// of the two panels together, so centring alone would lay them over each other. This spreads the
// pair apart evenly about their middle until there is a gap between them, and keeps both inside
// the edges. The player always stands left of the enemies, so the player's panel is kept on the left.
using System;
using System.Collections.Generic;

namespace Depiction
{
    public static class StatusPanelLayout
    {
        /// <summary>§4.5: the panels' top edge.</summary>
        public const float Top = 614f;

        /// <summary>§4.5: the player's panel is 300 wide.</summary>
        public const float PlayerWidth = 300f;

        /// <summary>§4.5: an enemy's panel is 210 wide, a 2-cell enemy's 272.</summary>
        public const float EnemyWidth = 210f;
        public const float LargeEnemyWidth = 272f;

        /// <summary>§4.5: an enemy's panel is no wider than the cells it stands on, less 10 px.</summary>
        public const float CellMargin = 10f;

        /// <summary>§4.5: kept 24 px inside the screen's left and right edges.</summary>
        public const float EdgeInset = 24f;

        /// <summary>The least space left between the two panels when they are pushed apart (§3.2's 8 px step).</summary>
        public const float MinGap = 8f;

        // §4.5 inside the panel: padding 8 above, 10 at the sides and below, 8 between rows.
        public const float PadTop = 8f;
        public const float PadSide = 10f;
        public const float PadBottom = 10f;
        public const float RowGap = 8f;

        /// <summary>1 行目: the HP number is 24 px, the tallest thing on the row (heart 20, bar 14).</summary>
        public const float HpRowHeight = 24f;

        /// <summary>2 行目: the Guard shield is 28 px, the tallest thing on the row.</summary>
        public const float GuardRowHeight = 28f;

        /// <summary>3 行目: a status chip is 30 px tall (§7.3), and wrapped rows are 5 px apart.</summary>
        public const float ChipHeight = 30f;
        public const float ChipGap = 5f;

        /// <summary>
        /// The width of an enemy's panel: 210, or 272 for an enemy that takes two cells or more, and
        /// never more than its cells' width on a line of <paramref name="cells"/> less 10 px. With no
        /// floor (<paramref name="cells"/> 0) the cell cap does not apply.
        /// </summary>
        public static float EnemyPanelWidth(int cells, int size)
        {
            int taken = Math.Max(1, size);
            float width = taken >= 2 ? LargeEnemyWidth : EnemyWidth;
            if (cells <= 0) return width;
            return Math.Min(width, taken * FloorSpec.CellWidth(cells) - CellMargin);
        }

        /// <summary>The room a panel of <paramref name="width"/> leaves for its rows inside the padding.</summary>
        public static float ContentWidth(float width) => Math.Max(0f, width - 2f * PadSide);

        /// <summary>The y (from the panel's top) of the chip row.</summary>
        public const float ChipRowTop = PadTop + HpRowHeight + RowGap + GuardRowHeight + RowGap;

        /// <summary>
        /// A panel's height with <paramref name="chipRows"/> rows of status chips: the HP row and the
        /// Guard row always, the chip rows when there are any, inside the padding.
        /// </summary>
        public static float Height(int chipRows)
        {
            float height = PadTop + HpRowHeight + RowGap + GuardRowHeight + PadBottom;
            if (chipRows > 0) height += RowGap + chipRows * ChipHeight + (chipRows - 1) * ChipGap;
            return height;
        }

        /// <summary>
        /// The left edges of the two panels, the player's centred on <paramref name="playerX"/> and the
        /// enemy's on <paramref name="enemyX"/> (screen x of each figure's middle) where there is room.
        /// Each is kept 24 px inside the edges; when they would come closer than <see cref="MinGap"/>,
        /// both move apart by the same amount, and one stopped by an edge hands the rest to the other.
        /// The player's panel stays on the left. Whatever the two x are, the panels never overlap as
        /// long as both fit side by side between the edges.
        /// </summary>
        public static void Place(float playerX, float playerWidth, float enemyX, float enemyWidth, out float playerLeft, out float enemyLeft)
        {
            if (playerWidth < 0f || enemyWidth < 0f) throw new ArgumentOutOfRangeException(nameof(playerWidth), "A panel has no negative width.");
            float lowest = EdgeInset;
            float playerHighest = CornerLayout.CanvasWidth - EdgeInset - playerWidth;
            float enemyHighest = CornerLayout.CanvasWidth - EdgeInset - enemyWidth;
            playerLeft = Clamp(playerX - playerWidth * 0.5f, lowest, playerHighest);
            enemyLeft = Clamp(enemyX - enemyWidth * 0.5f, lowest, enemyHighest);

            float shortBy = playerLeft + playerWidth + MinGap - enemyLeft;
            if (shortBy <= 0f) return;
            playerLeft -= shortBy * 0.5f;
            enemyLeft += shortBy * 0.5f;
            if (playerLeft < lowest)
            {
                enemyLeft += lowest - playerLeft;
                playerLeft = lowest;
            }
            if (enemyLeft > enemyHighest)
            {
                playerLeft -= enemyLeft - enemyHighest;
                enemyLeft = enemyHighest;
            }
        }

        /// <summary>
        /// The two panels for figures standing on a line of <paramref name="cells"/> (§4.2): each
        /// figure stands in the middle of its cells (FloorSpec.FeetX), the enemy's panel is as wide as
        /// <see cref="EnemyPanelWidth"/> allows, and both are <paramref name="height"/> tall at y 614.
        /// </summary>
        public static void OnFloor(int cells, int playerCell, int playerSize, int enemyCell, int enemySize, float height,
            out ScreenBox player, out ScreenBox enemy)
        {
            float enemyWidth = EnemyPanelWidth(cells, enemySize);
            Place(FloorSpec.FeetX(cells, playerCell, playerSize), PlayerWidth, FloorSpec.FeetX(cells, enemyCell, enemySize), enemyWidth,
                out float playerLeft, out float enemyLeft);
            player = new ScreenBox(playerLeft, Top, PlayerWidth, height);
            enemy = new ScreenBox(enemyLeft, Top, enemyWidth, height);
        }

        /// <summary>
        /// #589: the left edges of a row of panels in their order from left to right, each centred on its
        /// x in <paramref name="centres"/> where there is room. Two panels are placed by
        /// <see cref="Place"/>. With more, each is first kept 24 px inside the edges; panels that would
        /// come closer than <see cref="MinGap"/> are gathered into a run spaced <see cref="MinGap"/>
        /// apart and set where its members are, on average, as close to their own x as they can be;
        /// then a run that leaves an edge is pushed back in. As long as the row fits between the edges,
        /// no two panels overlap and each keeps its place in the order.
        /// </summary>
        public static float[] PlaceRow(float[] centres, float[] widths)
        {
            if (centres == null) throw new ArgumentNullException(nameof(centres));
            if (widths == null) throw new ArgumentNullException(nameof(widths));
            if (centres.Length != widths.Length) throw new ArgumentException("One width per panel.", nameof(widths));
            int count = centres.Length;
            var lefts = new float[count];
            foreach (float width in widths)
            {
                if (width < 0f) throw new ArgumentOutOfRangeException(nameof(widths), "A panel has no negative width.");
            }
            if (count == 0) return lefts;
            if (count == 2)
            {
                Place(centres[0], widths[0], centres[1], widths[1], out lefts[0], out lefts[1]);
                return lefts;
            }

            for (int i = 0; i < count; i++)
            {
                lefts[i] = Clamp(centres[i] - widths[i] * 0.5f, EdgeInset, CornerLayout.CanvasWidth - EdgeInset - widths[i]);
            }

            // Runs of panels that touch: each run starts at a panel and holds the ones after it.
            var starts = new List<int>();
            var ends = new List<int>();
            var bases = new List<float>();
            for (int i = 0; i < count; i++)
            {
                int start = i;
                float at = lefts[i];
                while (starts.Count > 0)
                {
                    int last = starts.Count - 1;
                    float runEnd = bases[last] + Span(widths, starts[last], ends[last]);
                    if (at >= runEnd + MinGap) break;
                    start = starts[last];
                    starts.RemoveAt(last);
                    ends.RemoveAt(last);
                    bases.RemoveAt(last);
                    at = RunBase(lefts, widths, start, i);
                }
                starts.Add(start);
                ends.Add(i);
                bases.Add(at);
            }
            for (int r = 0; r < starts.Count; r++)
            {
                float x = bases[r];
                for (int i = starts[r]; i <= ends[r]; i++)
                {
                    lefts[i] = x;
                    x += widths[i] + MinGap;
                }
            }

            // Keep the row inside the edges: push right from the left edge, then left from the right one.
            for (int i = 0; i < count; i++)
            {
                float lowest = i == 0 ? EdgeInset : lefts[i - 1] + widths[i - 1] + MinGap;
                if (lefts[i] < lowest) lefts[i] = lowest;
            }
            for (int i = count - 1; i >= 0; i--)
            {
                float highest = i == count - 1 ? CornerLayout.CanvasWidth - EdgeInset - widths[i] : lefts[i + 1] - MinGap - widths[i];
                if (lefts[i] > highest) lefts[i] = highest;
            }
            return lefts;
        }

        /// <summary>
        /// #589: the player's panel and one per enemy for figures standing on a line of
        /// <paramref name="cells"/>, as <see cref="OnFloor(int, int, int, int, int, float, out ScreenBox, out ScreenBox)"/>
        /// places the pair. The boxes come back as the player's first, then the enemies' in the order
        /// given. The panels are laid out by where their figures stand, so an enemy given first but
        /// standing further right still gets the panel further right.
        /// </summary>
        public static ScreenBox[] OnFloor(int cells, int playerCell, int playerSize, int[] enemyCells, int[] enemySizes, float height)
        {
            if (enemyCells == null) throw new ArgumentNullException(nameof(enemyCells));
            if (enemySizes == null) throw new ArgumentNullException(nameof(enemySizes));
            if (enemyCells.Length != enemySizes.Length) throw new ArgumentException("One size per enemy.", nameof(enemySizes));
            int count = enemyCells.Length + 1;
            var centres = new float[count];
            var widths = new float[count];
            centres[0] = FloorSpec.FeetX(cells, playerCell, playerSize);
            widths[0] = PlayerWidth;
            for (int i = 0; i < enemyCells.Length; i++)
            {
                centres[i + 1] = FloorSpec.FeetX(cells, enemyCells[i], enemySizes[i]);
                widths[i + 1] = EnemyPanelWidth(cells, enemySizes[i]);
            }

            // Left to right by where each figure stands; the player first on a tie (it stands left).
            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            Array.Sort(order, (a, b) => centres[a] != centres[b] ? centres[a].CompareTo(centres[b]) : a.CompareTo(b));
            var sortedCentres = new float[count];
            var sortedWidths = new float[count];
            for (int i = 0; i < count; i++)
            {
                sortedCentres[i] = centres[order[i]];
                sortedWidths[i] = widths[order[i]];
            }
            float[] lefts = PlaceRow(sortedCentres, sortedWidths);

            var boxes = new ScreenBox[count];
            for (int i = 0; i < count; i++) boxes[order[i]] = new ScreenBox(lefts[i], Top, sortedWidths[i], height);
            return boxes;
        }

        /// <summary>The width a run of panels from <paramref name="first"/> to <paramref name="last"/> takes, <see cref="MinGap"/> between each.</summary>
        private static float Span(float[] widths, int first, int last)
        {
            float span = 0f;
            for (int i = first; i <= last; i++) span += widths[i] + (i > first ? MinGap : 0f);
            return span;
        }

        /// <summary>Where a run from <paramref name="first"/> to <paramref name="last"/> starts so its panels sit, on average, at their own lefts.</summary>
        private static float RunBase(float[] lefts, float[] widths, int first, int last)
        {
            float sum = 0f;
            float offset = 0f;
            for (int i = first; i <= last; i++)
            {
                sum += lefts[i] - offset;
                offset += widths[i] + MinGap;
            }
            return sum / (last - first + 1);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (max < min) return min;
            return value < min ? min : value > max ? max : value;
        }
    }
}
