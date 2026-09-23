using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// §3 and §3.1: the column a card sits in, and the scale each column is authored from.
    ///
    /// The old tier table used to be a range the player picked at play time. It is now a design-time
    /// ruler: a card is written into one column, the column number is its cost, and that column's
    /// value is its effect. Column 4 is the exception — it still costs 3, so that 集中 and 習熟 have
    /// a rung above a cost-3 card to climb to.
    ///
    /// The heal scales came in with the eighty cards (#188): 集中 reads them to find the column to
    /// the right. Bleed / regen stacks and the stance step-up stay on the cards themselves.
    /// </summary>
    public static class Columns
    {
        public const int Min = 1;
        public const int Max = Constants.ColumnCount;

        /// <summary>§2.1: a single-attribute move card is always cost 1, whatever it carries.</summary>
        public const int MoveCardColumn = 1;

        /// <summary>§3.1: an enemy face is about 6 tenths of the player ruler. Authoring guidance only —
        /// the polearm's own numbers come from enemy_roster_v4.md §2.1 and are not derived here.</summary>
        public const double EnemyFaceRatio = 0.6;

        /// <summary>§2.1: each face of a two-attribute card is about 65% of the single-attribute column.</summary>
        public const double DualFaceRatio = 0.65;

        public static readonly IReadOnlyList<int> SingleAttackPower = new[] { 6, 13, 21, 30 };
        public static readonly IReadOnlyList<int> SingleGuard = new[] { 4, 9, 15, 22 };
        public static readonly IReadOnlyList<int> DualAttackPower = new[] { 4, 8, 14, 20 };
        public static readonly IReadOnlyList<int> DualGuard = new[] { 3, 6, 10, 14 };
        public static readonly IReadOnlyList<int> SingleHeal = new[] { 3, 8, 13, 19 };
        public static readonly IReadOnlyList<int> DualHeal = new[] { 2, 5, 8, 12 };

        /// <summary>
        /// §5 集中 (demo approximation, #188): what one column to the right adds on a scale — the
        /// step from <paramref name="column"/> to the next. 0 at column 4, which 集中 never passes.
        /// </summary>
        public static int StepRight(IReadOnlyList<int> scale, int column)
        {
            if (scale == null) throw new ArgumentNullException(nameof(scale));
            Validate(column);
            return column >= Max ? 0 : scale[column] - scale[column - 1];
        }

        /// <summary>§3.1: how many status kinds a single-attribute skill face applies. Column 4 also draws 1.</summary>
        public static readonly IReadOnlyList<int> SingleSkillKinds = new[] { 1, 2, 3, 3 };

        /// <summary>§3: the column number is the cost, except that column 4 still costs 3.</summary>
        public static int CostOf(int column)
        {
            Validate(column);
            return Math.Min(column, Constants.CostMax);
        }

        /// <summary>Reads one column off a scale. Columns are 1-based; the lists are not.</summary>
        public static int Value(IReadOnlyList<int> scale, int column)
        {
            if (scale == null) throw new ArgumentNullException(nameof(scale));
            Validate(column);
            return scale[column - 1];
        }

        public static bool IsValid(int column) => column >= Min && column <= Max;

        public static void Validate(int column)
        {
            if (!IsValid(column))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(column), column, $"Column must be {Min}..{Max}.");
            }
        }
    }
}
