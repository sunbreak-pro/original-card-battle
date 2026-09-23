using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>Where a shift left its owner and how many of the asked-for cells it could not take.</summary>
    public sealed record Shift(int From, int To, int Blocked)
    {
        public int Moved => Math.Abs(To - From);
    }

    /// <summary>
    /// §7: the line of cells. The player stands on the left and the enemies on the right, so "toward"
    /// is +1 for the player and −1 for an enemy. Nothing here holds state; the turn loop does.
    ///
    /// Several enemies (§7.1 / §7.4, #47): one enemy per cell (`CELL_CAPACITY` = 1) and a large enemy
    /// keeps its cells to itself. Nobody overtakes: the player stops short of the nearest standing
    /// enemy, and an enemy stops short of the player. Enemies pass each other — only the cells an
    /// enemy lands on must be free, not the ones it crosses. A fallen enemy holds no cells.
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
        /// (negative), as far as the line allows. <paramref name="unit"/> names the enemy when
        /// <paramref name="who"/> is <see cref="Actor.Enemy"/>. The caller has already taken 鈍足 off
        /// the count.
        ///
        /// The player stops one cell short of the nearest standing enemy and at cell 1. An enemy
        /// stops one cell past the player and at the line's end, and lands on the furthest cell it
        /// asked for whose span no other standing enemy uses; it crosses occupied cells freely.
        /// </summary>
        public static Shift Move(BattleState state, Actor who, int cells, int unit = 0)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return who == Actor.Player ? MovePlayer(state, cells) : MoveEnemy(state, unit, cells);
        }

        private static Shift MovePlayer(BattleState state, int cells)
        {
            var self = state.Player;
            int from = self.Cell;
            if (cells == 0) return new Shift(from, from, 0);

            int to;
            if (cells > 0)
            {
                // Toward = right, capped one cell short of the nearest standing enemy.
                int nearest = state.Nearest;
                int cap = nearest < 0 ? state.FieldCells - self.Size + 1 : state.Enemies[nearest].Body.Cell - self.Size;
                to = Math.Min(from + cells, cap);
            }
            else
            {
                to = Math.Max(from + cells, 1);
            }
            return new Shift(from, to, Math.Abs(cells) - Math.Abs(to - from));
        }

        private static Shift MoveEnemy(BattleState state, int unit, int cells)
        {
            var self = state.Enemies[unit].Body;
            int from = self.Cell;
            if (cells == 0) return new Shift(from, from, 0);

            // Toward = left, floor one cell past the player; away = right, cap at the line's end.
            int floor = state.Player.FarCell + 1;
            int cap = state.FieldCells - self.Size + 1;
            int step = cells > 0 ? -1 : 1;
            int wanted = from + step * Math.Abs(cells);
            int limit = Math.Max(floor, Math.Min(cap, wanted));

            // Walk back from the furthest cell asked for until the whole body fits.
            int to = from;
            for (int cell = limit; cell != from; cell -= step)
            {
                if (IsFree(state, unit, cell, self.Size))
                {
                    to = cell;
                    break;
                }
            }
            return new Shift(from, to, Math.Abs(cells) - Math.Abs(to - from));
        }

        /// <summary>Whether cells [cell, cell + size − 1] hold no standing enemy other than <paramref name="unit"/>.</summary>
        private static bool IsFree(BattleState state, int unit, int cell, int size)
        {
            int far = cell + size - 1;
            for (int i = 0; i < state.Enemies.Count; i++)
            {
                if (i == unit || !state.Enemies[i].Alive) continue;
                var other = state.Enemies[i].Body;
                if (cell <= other.FarCell && other.Cell <= far) return false;
            }
            return true;
        }

        /// <summary>
        /// §7.3 開始のマス: the enemy's near edge starts <paramref name="startGap"/> empty cells to the
        /// right of the player. When the line is too short to hold that — a 5-cell line, or a large
        /// enemy on a narrow one — the enemy stands at the right end instead and the gap shrinks;
        /// the player keeps its cell and the room behind it (2026-09-23, #169). Whether the result
        /// still fits at all is Validate's to say.
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
            Validate(fieldCells, playerCell, playerSize, new[] { (enemyCell, enemySize) });
        }

        /// <summary>
        /// §7.1 / §7.4: the same for one to three enemies (`ENEMIES_MAX`), which must also each keep
        /// their own cells (`CELL_CAPACITY` = 1).
        /// </summary>
        public static void Validate(int fieldCells, int playerCell, int playerSize, IReadOnlyList<(int Cell, int Size)> enemies)
        {
            if (enemies == null) throw new ArgumentNullException(nameof(enemies));
            if (fieldCells < Constants.FieldCellsMin || fieldCells > Constants.FieldCellsMax)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fieldCells), fieldCells, $"The line is {Constants.FieldCellsMin}..{Constants.FieldCellsMax} cells.");
            }
            if (enemies.Count < 1 || enemies.Count > Constants.EnemiesMax)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemies), enemies.Count, $"A battle has 1..{Constants.EnemiesMax} enemies (§7.4).");
            }
            if (playerSize < 1) throw new ArgumentOutOfRangeException(nameof(playerSize), playerSize, "At least one cell.");
            if (playerCell < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCell), playerCell, "Cells start at 1.");
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                var (cell, size) = enemies[i];
                if (size < 1 || size > Constants.EnemySizeMax)
                {
                    throw new ArgumentOutOfRangeException(nameof(enemies), size, $"An enemy uses 1..{Constants.EnemySizeMax} cells.");
                }
                if (cell + size - 1 > fieldCells)
                {
                    throw new ArgumentOutOfRangeException(nameof(enemies), cell, $"Enemy {i} does not fit on the line.");
                }
                if (playerCell + playerSize - 1 >= cell)
                {
                    throw new ArgumentException("The player must stand to the left of every enemy (§7.1).");
                }
                for (int j = 0; j < i; j++)
                {
                    var (otherCell, otherSize) = enemies[j];
                    if (cell <= otherCell + otherSize - 1 && otherCell <= cell + size - 1)
                    {
                        throw new ArgumentException($"Enemies {j} and {i} share a cell; a cell holds one enemy (§7.1).");
                    }
                }
            }
        }
    }
}
