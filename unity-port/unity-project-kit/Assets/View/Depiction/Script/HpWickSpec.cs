// The wick stripe on an enemy's HP bar while a card is held (battle-visual-v1 §4.5 HP の減る分, §5.2,
// #455). Pure arithmetic with no engine types, like FloorSpec and StatusPanelLayout, so the EditMode
// tests hold it directly. How much HP the card takes off is the script's (CardFace.HpLossFor); this
// only says where on the bar that amount is drawn and what the stripe tile looks like.
//
// The look is the mockup's `repeating-linear-gradient(135deg, #ffd89a 0 3px, transparent 3px 6px)`:
// a 135° gradient runs towards the bottom-right, so its stripes run up to the right (／) on screen,
// 3 px of wick and 3 px of gap.
using System;

namespace Depiction
{
    public static class HpWickSpec
    {
        /// <summary>§4.5: the stripe's angle in the CSS sense (the gradient's direction); the stripes run ／.</summary>
        public const float AngleDegrees = 135f;

        /// <summary>§4.5: each stripe is 3 px of wick.</summary>
        public const float StripeWidth = 3f;

        /// <summary>§4.5 3 px 間隔: 3 px of the bar shows between two stripes.</summary>
        public const float Gap = 3f;

        /// <summary>One stripe and one gap, measured across the stripes.</summary>
        public const float Period = StripeWidth + Gap;

        /// <summary>§2.1 灯芯: the stripe's colour.</summary>
        public const string ColorHex = "#ffd89a";

        /// <summary>
        /// The tile holds two periods, so its side (2 × 6 × √2 = 16.97) rounds to a whole 17 px and
        /// the spacing it really draws stays within 0.02 px of <see cref="Period"/>.
        /// </summary>
        public const int PeriodsPerTile = 2;

        private const double Sqrt2 = 1.4142135623730951;

        /// <summary>The square tile's side in pixels: 17.</summary>
        public static int TileSize => (int)Math.Round(PeriodsPerTile * Period * Sqrt2);

        /// <summary>The period the 17 px tile really draws, across the stripes: 6.01.</summary>
        public static float TilePeriod => (float)(TileSize / (PeriodsPerTile * Sqrt2));

        /// <summary>
        /// The tile's alpha at texel (<paramref name="x"/>, <paramref name="y"/>), x to the right and y
        /// upwards as Texture2D counts its rows: 1 inside a stripe, 0 in a gap, and a one-pixel ramp at
        /// the edges. Stripes are constant along x − y, so they run ／ and the tile repeats seamlessly.
        /// </summary>
        public static float Alpha(int x, int y)
        {
            int tile = TileSize;
            int diagonal = ((x - y) % tile + tile) % tile;
            float period = TilePeriod;
            float across = (float)(diagonal / Sqrt2) % period;
            // The stripe fills the first half of each period; measure to its middle, wrapping round.
            float stripe = period * 0.5f;
            float distance = Math.Abs(across - stripe * 0.5f);
            distance = Math.Min(distance, period - distance);
            return Clamp01(stripe * 0.5f - distance + 0.5f);
        }

        /// <summary>
        /// Where the stripe lies on a bar of <paramref name="hpMax"/> that is filled to
        /// <paramref name="hp"/>: from the HP left after <paramref name="loss"/> up to the HP now, as
        /// fractions of the bar's width. False, and no stripe, when the card takes nothing off (a loss
        /// of 0, which the script writes for an enemy out of reach) or the bar is empty.
        /// </summary>
        public static bool Span(int hp, int hpMax, int loss, out float from, out float to)
        {
            from = to = 0f;
            if (loss <= 0 || hp <= 0 || hpMax <= 0) return false;
            to = Clamp01(hp / (float)hpMax);
            from = Clamp01((hp - loss) / (float)hpMax);
            return to > from;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
