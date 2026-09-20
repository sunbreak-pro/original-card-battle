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
}
