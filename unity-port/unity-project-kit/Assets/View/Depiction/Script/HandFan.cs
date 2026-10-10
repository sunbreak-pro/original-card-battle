// Where each card of the hand rests. Pure arithmetic with no engine types, so the EditMode
// tests can hold it directly: the View lives in Assembly-CSharp, which a test asmdef cannot
// reference. This is layout, not a rule of the game; the numbers come from the View's inspector.
using System;

namespace Depiction
{
    /// <summary>A resting place in the hand area: offset from its centre in pixels, and tilt in degrees.</summary>
    public readonly struct FanPlace
    {
        public readonly float X;
        public readonly float Y;
        /// <summary>Counter-clockwise tilt. Cards left of the middle lean left (positive).</summary>
        public readonly float Degrees;

        public FanPlace(float x, float y, float degrees)
        {
            X = x;
            Y = y;
            Degrees = degrees;
        }
    }

    public static class HandFan
    {
        /// <summary>The largest hand the spacing still closes in for (HAND_LIMIT, battle_core_v4 §8).</summary>
        public const int MostCards = 8;

        /// <summary>
        /// battle-visual-v1.md §4.8: neighbouring cards sit 160 px apart for five cards or fewer, then
        /// 150 / 140 / 126 for six / seven / eight, so eight still fit the 1100 px hand band.
        /// <paramref name="upToFive"/> is the inspector's spacing for five or fewer; six to eight scale it
        /// by the same shares, so 160 gives the table exactly. Past eight the spacing stays at eight's.
        /// </summary>
        public static float Spacing(int count, float upToFive)
        {
            if (count <= 5) return upToFive;
            if (count == 6) return upToFive * (150f / 160f);
            if (count == 7) return upToFive * (140f / 160f);
            return upToFive * (126f / 160f);
        }

        /// <summary>
        /// Resting place of card <paramref name="index"/> of <paramref name="count"/> in a shallow fan.
        /// The centre of the middle card rises above the outermost cards' by <paramref name="dropPixels"/>
        /// times the step squared. A tilted card's lower corner dips by half its width times sin(tilt);
        /// the whole hand is raised by the outermost card's dip, so that corner stays on or just above
        /// the hand's base line (the screen edge is just below it; the lift left over is
        /// half the height times 1 - cos(tilt), under 2 px for shallow fans). The raise is the same for
        /// every card: raising each card by its own dip would lift the cards beside the middle above it
        /// once the tilt outgrows the drop. Assumes the outermost tilt stays within 90 degrees.
        /// </summary>
        public static FanPlace Place(int count, int index, float spacing, float degreesPerCard, float dropPixels, float cardWidth)
        {
            float edge = (count - 1) * 0.5f;
            float step = index - edge;
            float degrees = -step * degreesPerCard;
            float edgeCornerDip = (float)Math.Abs(Math.Sin(edge * degreesPerCard * Math.PI / 180.0)) * cardWidth * 0.5f;
            return new FanPlace(step * spacing, (edge * edge - step * step) * dropPixels + edgeCornerDip, degrees);
        }
    }

    /// <summary>
    /// battle-visual-v1 §4.8 (#242): where the hand's cards rest and rise on the 1920×1080 screen.
    /// Coordinates are screen pixels, x from the left and y from the top, as the document gives them.
    /// </summary>
    public static class HandSpec
    {
        public const float CardWidth = 216f;
        public const float CardHeight = 304f;
        public const float CentreX = 960f;

        /// <summary>The middle card's top: only its upper 184 px show above the screen's bottom.</summary>
        public const float MiddleTop = 896f;

        /// <summary>The k-th card from the middle sits k² times this much lower.</summary>
        public const float DropPerStep = 3.2f;

        /// <summary>A hovered card's top: its bottom then sits 16 px above the screen's bottom.</summary>
        public const float HoverTop = 760f;

        /// <summary>
        /// The resting place of card <paramref name="index"/> of <paramref name="count"/>: its centre on
        /// the screen (X, Y from the top) and its tilt (counter-clockwise, cards left of the middle lean
        /// left). The tilt turns the card about the middle of its bottom edge, as §4.8 says.
        /// </summary>
        public static FanPlace Rest(int count, int index, float spacing, float degreesPerCard)
        {
            float step = index - (count - 1) * 0.5f;
            float x = CentreX + step * spacing;
            float top = MiddleTop + step * step * DropPerStep;
            float degrees = -step * degreesPerCard;
            double radians = degrees * Math.PI / 180.0;
            float half = CardHeight * 0.5f;
            return new FanPlace(x - half * (float)Math.Sin(radians), top + CardHeight - half * (float)Math.Cos(radians), degrees);
        }

        /// <summary>A hovered card's centre: upright, above its own place in the fan, top at <see cref="HoverTop"/>.</summary>
        public static FanPlace Hover(int count, int index, float spacing)
        {
            float step = index - (count - 1) * 0.5f;
            return new FanPlace(CentreX + step * spacing, HoverTop + CardHeight * 0.5f, 0f);
        }

        /// <summary>The width the hand spans at rest, ignoring the tilt: (count − 1) × spacing + one card.</summary>
        public static float Width(int count, float spacing)
        {
            return count <= 0 ? 0f : (count - 1) * spacing + CardWidth;
        }
    }
}
