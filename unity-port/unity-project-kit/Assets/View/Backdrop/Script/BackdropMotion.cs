// How the backdrop moves (#89): the breath of each light, the specks drifting through the haze, and the
// crossfade from one layer to the next. Pure functions of the time, so the same moment always draws the
// same picture (a frame captured twice matches) and `dotnet test` can pin them.
namespace Backdrop
{
    public static class BackdropMotion
    {
        /// <summary>How long a flash of <see cref="Flicker.Flash"/> stays lit, out of its period.</summary>
        public const int FlashMs = 140;

        /// <summary>
        /// The share of a haze's alpha lit at <paramref name="ms"/>: 1 at the top of a breath, 1 − depth
        /// at the bottom, on a cosine so it never jumps. A flash is lit only in its first
        /// <see cref="FlashMs"/>, falling from full to nothing; the rest of the period it is 1 − depth.
        /// </summary>
        public static float FlickerAlpha(Flicker flicker, double ms)
        {
            if (!flicker.Moves) return 1f;
            double phase = Fraction(ms / flicker.PeriodMs);
            if (flicker.Flash)
            {
                double lit = phase * flicker.PeriodMs;
                if (lit >= FlashMs) return 1f - flicker.Depth;
                return 1f - flicker.Depth * (float)(lit / FlashMs);
            }
            double dip = 0.5 - 0.5 * System.Math.Cos(phase * 2.0 * System.Math.PI);
            return 1f - flicker.Depth * (float)dip;
        }

        /// <summary>
        /// Where speck <paramref name="index"/> is at <paramref name="ms"/> on a <paramref name="width"/> ×
        /// <paramref name="height"/> screen (0 at the top left): a fixed start drawn from the index, moved
        /// at the layer's speed with a slow sway, and wrapped round the edges so the count on screen holds.
        /// </summary>
        public static void MoteAt(Motes motes, int index, double ms, float width, float height, out float x, out float y)
        {
            double seconds = ms / 1000.0;
            double sway = System.Math.Sin(seconds * 0.6 + Hash(index, 3) * 6.283) * 12.0;
            x = (float)Wrap(Hash(index, 1) * width + motes.SpeedX * seconds + sway, width);
            y = (float)Wrap(Hash(index, 2) * height + motes.SpeedY * seconds, height);
        }

        /// <summary>The share of speck <paramref name="index"/>'s alpha lit at <paramref name="ms"/>, each speck on its own beat.</summary>
        public static float MoteAlpha(Motes motes, int index, double ms)
        {
            if (motes.Twinkle <= 0f) return 1f;
            double beat = 1.5 + Hash(index, 4) * 2.5; // seconds per twinkle
            double dip = 0.5 - 0.5 * System.Math.Cos((ms / 1000.0 / beat + Hash(index, 5)) * 2.0 * System.Math.PI);
            return 1f - motes.Twinkle * (float)dip;
        }

        /// <summary>
        /// The new layer's share of the screen <paramref name="elapsedMs"/> into the switch: 0 → 1 over
        /// <see cref="BackdropSpec.CrossfadeMs"/>, eased so it starts and lands softly.
        /// </summary>
        public static float Crossfade(double elapsedMs)
        {
            if (elapsedMs <= 0) return 0f;
            if (elapsedMs >= BackdropSpec.CrossfadeMs) return 1f;
            double t = elapsedMs / BackdropSpec.CrossfadeMs;
            return (float)(t * t * (3.0 - 2.0 * t));
        }

        /// <summary>A fixed number in [0, 1) for <paramref name="index"/> and <paramref name="salt"/>; no Random, so every run draws the same.</summary>
        public static double Hash(int index, int salt)
        {
            unchecked
            {
                uint h = (uint)index * 374761393u + (uint)salt * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (double)0x1000000;
            }
        }

        private static double Fraction(double v)
        {
            return v - System.Math.Floor(v);
        }

        private static double Wrap(double v, double span)
        {
            if (span <= 0) return 0;
            double r = v % span;
            return r < 0 ? r + span : r;
        }
    }
}
