// Where the omen badges stand when a battle has several enemies (battle_ui_ux_v2 §2.2 の 2 / §5.8 /
// §5.16, battle-visual-v1 §4.4, #589). Pure arithmetic with no engine types, like StatusPanelLayout, so
// the EditMode tests hold it directly. Boxes are in pixels on the 1920 x 1080 reference canvas, y
// counted downwards from the top.
//
// A badge stands over its own enemy's head. Two that would overlap slide apart, each at most 75 px and
// by the same rule as the number panels (StatusPanelLayout.PlaceByX). An elite's or a boss's plan (its
// second action) stacks above the badge and is narrower, so it stays over its own badge and clears the
// next one's whenever the badges clear each other. §5.16 puts every first action on one row, 14 px
// over the highest head, so an elite's two rows stand beside the others' one.
//
// Every standing enemy takes a place whether or not its omen shows now, as the trial's hud does: a
// spent omen does not send its neighbour sliding back for the moment until the next one drops in.
using System;
using System.Collections.Generic;

namespace Depiction
{
    public static class OmenRowLayout
    {
        /// <summary>battle-visual-v1 §4.4 予兆の札: 200 × 98.</summary>
        public const float BadgeWidth = 200f;
        public const float BadgeHeight = 98f;

        /// <summary>battle-visual-v1 §4.4 控えの予兆: 170 × 66, 8 px above the badge.</summary>
        public const float PlanWidth = 170f;
        public const float PlanHeight = 66f;
        public const float PlanGap = 8f;

        /// <summary>battle_ui_ux_v2 §5.8: a badge slides at most 75 px off its head.</summary>
        public const float SlideMax = 75f;

        /// <summary>battle_ui_ux_v2 §5.8: the slide takes 200 ms, easing out.</summary>
        public const float SlideMs = 200f;

        /// <summary>
        /// Each badge's left edge, in the order given, for enemies whose figures' middles stand at
        /// <paramref name="centres"/>: over its own head where there is room, kept 24 px inside the screen,
        /// pushed apart evenly where two would come closer than 8 px. The badge further right stays right.
        /// </summary>
        public static float[] Lefts(float[] centres)
        {
            if (centres == null) throw new ArgumentNullException(nameof(centres));
            var widths = new float[centres.Length];
            for (int i = 0; i < widths.Length; i++) widths[i] = BadgeWidth;
            return StatusPanelLayout.PlaceByX(centres, widths);
        }

        /// <summary>How far the badge at <paramref name="left"/> stands off the head at <paramref name="centre"/>.</summary>
        public static float Slide(float left, float centre) => left + BadgeWidth * 0.5f - centre;

        /// <summary>The left edge of the plan over the badge at <paramref name="left"/>: on the badge's middle.</summary>
        public static float PlanLeft(float left) => left + (BadgeWidth - PlanWidth) * 0.5f;

        /// <summary>
        /// §5.16: the y of the row's bottom edge, 14 px over the highest of the heads, for figures
        /// <paramref name="heights"/> tall with their feet on <paramref name="feetY"/>. A tall enemy in the
        /// row puts the whole row at its fixed place (<see cref="BadgeBottom"/>), so it stays one row.
        /// </summary>
        public static float RowBottom(float feetY, IReadOnlyList<float> heights)
        {
            if (heights == null) throw new ArgumentNullException(nameof(heights));
            float tallest = 0f;
            foreach (float height in heights) tallest = Math.Max(tallest, height);
            return BadgeBottom(feetY, tallest);
        }

        /// <summary>
        /// battle-visual-v1 §4.3 大型の敵 (#843): the first action's badge over an enemy taller than
        /// <see cref="FigureFrame.TallHeight"/> has its top fixed on y 162, wherever the head is; the plan
        /// stacks 8 px above it (y 88〜154), clear of the corner info and the buttons that end at y 76.
        /// </summary>
        public const float TallBadgeTop = 162f;

        /// <summary>
        /// The y of the bottom edge of a badge <paramref name="badgeHeight"/> tall over a figure
        /// <paramref name="height"/> px tall with its feet on <paramref name="feetY"/>: 14 px over the head
        /// (§4.4), or, for a tall enemy, under its fixed top (<see cref="TallBadgeTop"/>).
        /// </summary>
        public static float BadgeBottom(float feetY, float height, float badgeHeight = BadgeHeight)
        {
            return FigureFrame.IsTall(height) ? TallBadgeTop + badgeHeight : feetY - FigureFrame.OmenBottomAbove(height);
        }
    }
}
