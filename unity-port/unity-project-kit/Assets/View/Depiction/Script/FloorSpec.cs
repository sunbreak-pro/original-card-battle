// Where the floor's cells stand on the 1920×1080 screen (battle-visual-v1 §4.2, #242). Pure
// arithmetic with no engine types, so the EditMode tests hold it directly, as they hold HandSpec.
// This is layout, not a rule of the game: which cells exist and who stands where is the script's.
using System;

namespace Depiction
{
    public static class FloorSpec
    {
        /// <summary>§4.2: the band runs x 300〜1620 (1320 px) and is 40 px tall.</summary>
        public const float Left = 300f;
        public const float Right = 1620f;
        public const float Height = 40f;

        /// <summary>§4.2: each cell leaves 3 px free on its left and right.</summary>
        public const float Inset = 3f;

        /// <summary>§4.2 / §4.3: the floor's top is at y 560 and the feet at y 570.</summary>
        public const float FeetBelowTop = 10f;

        /// <summary>§4.2 間合いの数字: at gap 0 the tag is 100 px wide over the border of the two cells.</summary>
        public const float TagAtZero = 100f;

        /// <summary>The width the band gives one cell: 220 px for 6, 165 for 8, 264 for 5.</summary>
        public static float CellWidth(int cells)
        {
            return (Right - Left) / Math.Max(1, cells);
        }

        /// <summary>The left edge of cell <paramref name="cell"/> (1-based) as drawn, its 3 px inset taken off.</summary>
        public static float TileLeft(int cells, int cell)
        {
            return Left + (cell - 1) * CellWidth(cells) + Inset;
        }

        /// <summary>The width of a cell as drawn: its share of the band less 3 px on each side.</summary>
        public static float TileWidth(int cells)
        {
            return CellWidth(cells) - Inset * 2f;
        }

        /// <summary>§4.3: a figure stands in the middle of the cells it takes.</summary>
        public static float FeetX(int cells, int cell, int size)
        {
            return Left + (cell - 1 + Math.Max(1, size) * 0.5f) * CellWidth(cells);
        }

        /// <summary>
        /// §4.2 間合いの数字: the empty cells between the player's front edge and the enemy's near edge,
        /// as screen x. At gap 0 both are the border the two share.
        /// </summary>
        public static void GapSpan(int cells, int playerCell, int playerSize, int enemyCell, out float from, out float to)
        {
            float width = CellWidth(cells);
            from = Left + (playerCell + Math.Max(1, playerSize) - 1) * width;
            to = Left + (enemyCell - 1) * width;
        }
    }
}
