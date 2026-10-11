// The stances each side holds, as the screen lists them at its edge (battle-visual-v1 §4.6 スタンス枠,
// battle_core_v4 §4 v4.4, battle_ui_ux_v2 §3.7, #455). Pure C#, so `dotnet test` holds the same order
// and the same places the screen draws.
//
// v4.4 has no single slot: every stance put down joins a list with no cap, and the same stance put
// down again works twice. battle-visual-v1 §4.6 still draws one 250×52 frame per side, so until
// design settles the list's shape (the conflict handed off as 2026-10-11-battle-conflict-stance-list-
// shape) each stance takes one such frame, stacked upward from the slot §4.6 gives.
using System;
using System.Collections.Generic;

namespace Depiction
{
    public static class StanceList
    {
        /// <summary>§4.6: each frame is 250×52, 24 px in from its side's edge, its top at y 470.</summary>
        public const float Width = 250f;
        public const float RowHeight = 52f;
        public const float Edge = 24f;
        public const float SlotTop = 470f;

        /// <summary>The gap between two stacked frames: 6 px from the §3.2 spacing scale.</summary>
        public const float RowGap = 6f;

        /// <summary>
        /// At most this many frames per side (the top one at y 354). A longer list folds its oldest
        /// stances into one "+n" frame on top, as the status chips fold theirs (§7.3), so the newest
        /// stance always shows.
        /// </summary>
        public const int MaxRows = 3;

        /// <summary>
        /// The rows of a side's list from the stance names in the order they were put down, a repeat
        /// listed again (CombatantState.StanceList). battle_ui_ux_v2 §3.7: in the order put down, the
        /// newest last; a stance put down again is not listed twice but moves to the end with its
        /// count raised, so the newest stance is always the last row.
        /// </summary>
        public static List<StanceRow> Rows(IReadOnlyList<string> placed)
        {
            var rows = new List<StanceRow>();
            if (placed == null) return rows;
            foreach (string name in placed)
            {
                if (string.IsNullOrEmpty(name)) continue;
                int at = rows.FindIndex(r => r.Name == name);
                int count = 1;
                if (at >= 0)
                {
                    count = rows[at].Count + 1;
                    rows.RemoveAt(at);
                }
                rows.Add(new StanceRow { Name = name, Count = count });
            }
            return rows;
        }

        /// <summary>How many of the oldest rows fold into the "+n" frame: none while the list fits.</summary>
        public static int Folded(int rows)
        {
            return rows > MaxRows ? rows - (MaxRows - 1) : 0;
        }

        /// <summary>How many frames a list of <paramref name="rows"/> takes: one empty frame for none.</summary>
        public static int Frames(int rows)
        {
            return Math.Max(1, Math.Min(rows, MaxRows));
        }

        /// <summary>The left edge of <paramref name="side"/>'s frames on the 1920×1080 screen.</summary>
        public static float LeftOf(UnitSide side)
        {
            return side == UnitSide.Player ? Edge : RefWidth - Edge - Width;
        }

        /// <summary>The top of the frame <paramref name="fromBottom"/> places above the §4.6 slot (0 is the slot itself).</summary>
        public static float TopOf(int fromBottom)
        {
            return SlotTop - fromBottom * (RowHeight + RowGap);
        }

        /// <summary>The "×2" beside a stance put down more than once; nothing for one (§7.3 永続).</summary>
        public static string CountText(int count)
        {
            return count > 1 ? "×" + count : "";
        }

        private const float RefWidth = 1920f;
    }
}
