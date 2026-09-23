using System;

namespace BattleCore
{
    /// <summary>Where a shift left its owner and how many of the asked-for cells it could not take.</summary>
    public sealed record Shift(int From, int To, int Blocked)
    {
        public int Moved => Math.Abs(To - From);
    }

    /// <summary>
    /// §7: the line of cells. The player stands on the left and the enemy on the right, so "toward"
    /// is +1 for the player and −1 for the enemy. Nothing here holds state; the turn loop does.
    ///
    /// One enemy for now: with several (#47) the toward-limit becomes the nearest enemy, and a cell
    /// holds one enemy at most (`CELL_CAPACITY` = 1, 2026-09-23).
    /// </summary>
    public static class Field
    {
        /// <summary>§7.2: N = the opponent's near edge − own cell − 1. Never negative: nobody overtakes (§7.1).</summary>
        public static int GapBetween(CombatantState player, CombatantState enemy)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            return enemy.Cell - player.FarCell - 1;
        }

        /// <summary>
        /// §7.3: move one side by <paramref name="cells"/> toward the other (positive) or away
        /// (negative), stopping short at the opponent's near edge or at the line's own end. The
        /// caller has already taken 鈍足 off the count.
        /// </summary>
        public static Shift Move(BattleState state, Actor who, int cells)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var self = who == Actor.Player ? state.Player : state.Enemy;
            var other = who == Actor.Player ? state.Enemy : state.Player;
            if (cells == 0) return new Shift(self.Cell, self.Cell, 0);

            int from = self.Cell;
            int to;
            if (who == Actor.Player)
            {
                // Toward = right, capped one cell short of the enemy; away = left, floor 1.
                to = cells > 0
                    ? Math.Min(from + cells, other.Cell - self.Size)
                    : Math.Max(from + cells, 1);
            }
            else
            {
                // Toward = left, floor one cell past the player; away = right, cap at the line's end.
                to = cells > 0
                    ? Math.Max(from - cells, other.FarCell + 1)
                    : Math.Min(from - cells, state.FieldCells - self.Size + 1);
            }
            return new Shift(from, to, Math.Abs(cells) - Math.Abs(to - from));
        }

        /// <summary>
        /// §7.3 開始のマス: the enemy's near edge starts <paramref name="startGap"/> empty cells to the
        /// right of the player. When the line is too short to hold that — a 5-cell line, or a large
        /// enemy on a narrow one — the enemy stands at the right end instead and the gap shrinks;
        /// the player keeps its cell and the room behind it (2026-09-23, #169). Whether the result
        /// still fits at all is <see cref="Validate"/>'s to say.
        /// </summary>
        public static int EnemyStartCell(int fieldCells, int playerCell, int playerSize, int enemySize, int startGap)
        {
            if (startGap < 0) throw new ArgumentOutOfRangeException(nameof(startGap), startGap, "A gap is never negative.");
            int wanted = playerCell + playerSize + startGap;
            return Math.Min(wanted, fieldCells - enemySize + 1);
        }

        /// <summary>§7.3: the damage for the cells a pushed side could not take, before Guard.</summary>
        public static int WallDamage(int blockedCells) =>
            Math.Max(0, blockedCells) * Constants.WallDamage;

        /// <summary>
        /// Refuses a setup the line cannot hold: the width outside 5〜8, a side off the line, the
        /// player not to the left of the enemy, or an enemy wider than `ENEMY_SIZE_MAX`.
        /// </summary>
        public static void Validate(int fieldCells, int playerCell, int playerSize, int enemyCell, int enemySize)
        {
            if (fieldCells < Constants.FieldCellsMin || fieldCells > Constants.FieldCellsMax)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fieldCells), fieldCells, $"The line is {Constants.FieldCellsMin}..{Constants.FieldCellsMax} cells.");
            }
            if (enemySize < 1 || enemySize > Constants.EnemySizeMax)
            {
                throw new ArgumentOutOfRangeException(nameof(enemySize), enemySize, $"An enemy uses 1..{Constants.EnemySizeMax} cells.");
            }
            if (playerSize < 1) throw new ArgumentOutOfRangeException(nameof(playerSize), playerSize, "At least one cell.");
            if (playerCell < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCell), playerCell, "Cells start at 1.");
            }
            if (enemyCell + enemySize - 1 > fieldCells)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyCell), enemyCell, "The enemy does not fit on the line.");
            }
            if (playerCell + playerSize - 1 >= enemyCell)
            {
                throw new ArgumentException("The player must stand to the left of the enemy (§7.1).");
            }
        }
    }
}
