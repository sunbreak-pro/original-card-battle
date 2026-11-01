// The stances each side holds, as the screen lists them at its edge (battle-visual-v1 §4.6 スタンス枠,
// battle_core_v4 §4 v4.4, battle_ui_ux_v2 §3.7, #455). Pure C#, so `dotnet test` holds the same order
// and the same places the screen draws.
//
// v4.4 has no single slot: every stance put down joins a list with no cap, and the same stance put
// down again works twice. battle-visual-v1 §4.6 (v1.5, #505) gives each stance one 250×52 frame,
// stacked upward from the slot at y 470. A frame's hover panel carries the stance's effect, which the
// status chips no longer do (#605).
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

        /// <summary>§4.6 / §7.3: the word on the right of a stance frame's hover panel.</summary>
        public const string PanelGroup = "永続";

        /// <summary>§7.3 説明パネル: 340 px wide.</summary>
        public const float PanelWidth = 340f;

        /// <summary>The space between a frame and its panel: 8 px from the §3.2 spacing scale.</summary>
        public const float PanelGap = 8f;

        /// <summary>How close the panel may come to the screen's top and bottom edges (as the status chips' panel keeps).</summary>
        public const float PanelMargin = 16f;

        /// <summary>
        /// §4.6: the left edge of the hover panel of <paramref name="side"/>'s frames — beside the frame,
        /// on the right for the player and on the left for the enemy, so it never covers the list.
        /// </summary>
        public static float PanelLeft(UnitSide side)
        {
            return side == UnitSide.Player
                ? LeftOf(side) + Width + PanelGap
                : LeftOf(side) - PanelGap - PanelWidth;
        }

        /// <summary>
        /// The top of the hover panel of the frame <paramref name="fromBottom"/>: level with the frame's
        /// top, raised when a tall panel (a long "+n" list) would run past the screen's bottom.
        /// </summary>
        public static float PanelTop(int fromBottom, float panelHeight)
        {
            float top = TopOf(fromBottom);
            float lowest = RefHeight - PanelMargin - panelHeight;
            return Math.Max(PanelMargin, Math.Min(top, lowest));
        }

        /// <summary>§4.6: the hover panel's heading beside the flag, "岩の構え ×2" ("岩の構え" for one).</summary>
        public static string PanelTitle(StanceRow row)
        {
            string count = CountText(row.Count);
            return count.Length > 0 ? row.Name + " " + count : row.Name;
        }

        /// <summary>§7.3: the panel's two lines, 効き方 and 減り方, as the bridge wrote them on the row (#605).</summary>
        public static string PanelBody(StanceRow row)
        {
            return "効き方　" + row.Effect + "\n減り方　" + row.Decay;
        }

        /// <summary>
        /// §4.6 畳み方: the panel of the "+n" frame lists the folded stances, the oldest
        /// <paramref name="folded"/> rows, in the order put down, one line each as 「名前 ×数：効き方」.
        /// </summary>
        public static List<string> FoldedLines(IReadOnlyList<StanceRow> rows, int folded)
        {
            var lines = new List<string>();
            if (rows == null) return lines;
            for (int i = 0; i < folded && i < rows.Count; i++) lines.Add(PanelTitle(rows[i]) + "：" + rows[i].Effect);
            return lines;
        }

        private const float RefWidth = 1920f;
        private const float RefHeight = 1080f;
    }
}
