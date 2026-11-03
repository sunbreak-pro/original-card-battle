// The pips of the player's stamina tag (battle-visual-v1 §4.7, #822). Pure arithmetic with no engine
// types, like StatusPanelLayout, so both stamina tags (the trial's HudStaminaTag and the depiction's
// StaminaTagView) and the EditMode tests read the same rows.
//
// One pip per point of the maximum, ten to a row (決定 21): a maximum of 11〜20 puts the 11th pip and
// after on a second row, and the tag grows 20 px upward to hold it. The stamina above the maximum (息継ぎ
// の笛, battle_core_v4 §9.1) gets a pip of its own in another colour after the maximum's, on the same
// rows (#508, 2026-11-03). The core caps both at 20, so two rows always hold every pip.
using System;

namespace Depiction
{
    /// <summary>The look of one pip (battle-visual-v1 §4.7).</summary>
    public enum PipShape
    {
        /// <summary>Stamina there now: an amber fill.</summary>
        Have,

        /// <summary>Stamina a held card would pay: an amber 3 px ring.</summary>
        Pay,

        /// <summary>Stamina the next turn start brings back: an amber 2 px dashed ring.</summary>
        Next,

        /// <summary>A point of the maximum not there: a steel 2 px ring.</summary>
        Empty,

        /// <summary>Stamina above the maximum (息継ぎの笛): a fill in another colour (#508).</summary>
        Over,
    }

    public static class StaminaPips
    {
        /// <summary>§4.7 決定 21: ten pips to a row.</summary>
        public const int PerRow = 10;

        /// <summary>battle_core_v4 §1: the maximum stops at 20, so two rows hold every pip.</summary>
        public const int Rows = 2;

        /// <summary>§4.7: a pip is 16 px, 4 px from the next; the rows stand 4 px apart.</summary>
        public const float Dot = 16f;
        public const float Gap = 4f;
        public const float RowGap = 4f;

        /// <summary>From one row's top to the next one's.</summary>
        public const float RowPitch = Dot + RowGap;

        /// <summary>§4.7: the tag is 84 tall, and 104 (20 more, grown upward) when the pips take two rows.</summary>
        public const float TagHeight = 84f;
        public const float TagGrowth = 20f;

        /// <summary>How many pips show: one per point of the maximum, and one per point above it.</summary>
        public static int Count(int stamina, int max)
        {
            int count = Math.Max(Math.Max(0, max), Math.Max(0, stamina));
            return Math.Min(PerRow * Rows, count);
        }

        /// <summary>Whether the pips take a second row (a maximum or a stamina of 11 or more).</summary>
        public static bool TwoRows(int stamina, int max) => Count(stamina, max) > PerRow;

        /// <summary>The tag's height: 84, or 104 with a second row.</summary>
        public static float TagHeightFor(int stamina, int max) => TwoRows(stamina, max) ? TagHeight + TagGrowth : TagHeight;

        /// <summary>How far the tag's top moves up from its one-row place: 0, or 20 with a second row.</summary>
        public static float TagRaise(int stamina, int max) => TwoRows(stamina, max) ? TagGrowth : 0f;

        /// <summary>The row of pip <paramref name="index"/> (0 first).</summary>
        public static int RowOf(int index) => index / PerRow;

        /// <summary>
        /// The look of pip <paramref name="index"/>: the stamina counts from the first pip on, the points
        /// a held card pays come off the top of it, and the next turn's recovery follows it. A pip past the
        /// maximum is stamina above it, paid first when a card is held.
        /// </summary>
        public static PipShape ShapeAt(int index, int stamina, int max, int cost, int next)
        {
            int have = Math.Max(0, stamina);
            int paying = Math.Min(Math.Max(0, cost), have);
            if (index >= Math.Max(0, max)) return index < have - paying ? PipShape.Over : index < have ? PipShape.Pay : PipShape.Empty;
            if (index < have - paying) return PipShape.Have;
            if (index < have) return PipShape.Pay;
            if (index < have + Math.Max(0, next)) return PipShape.Next;
            return PipShape.Empty;
        }

        /// <summary>
        /// Whether the 構え divider stands after pip <paramref name="dividerAfter"/> − 1: only on the first
        /// row, and only when a pip follows it there (§4.7).
        /// </summary>
        public static bool DividerShown(int dividerAfter, int stamina, int max) =>
            dividerAfter > 0 && dividerAfter < PerRow && Math.Min(PerRow, Count(stamina, max)) > dividerAfter;
    }
}
