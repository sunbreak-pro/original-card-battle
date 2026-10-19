// The exploration screen's backdrop for each layer (battle-visual-v1 §2.5, #90): the same place the battle
// stands in (BackdropSpec's sky, haze and motes), with a veil of the ground colour laid over it so the
// map, its links and its labels — drawn straight on the backdrop, not on panels — read on every layer.
// Pure C#, so `dotnet test` holds the values the screen draws.
//
// The veil is thin on the dark layers and thick on the three bright ones (1, 2 and 4), and each alpha is the
// least that keeps the body text and the amber accent readable at the backdrop's brightest point, with
// a little to spare. The hand-drawn map ground, when it lands, goes between the place and the veil.
namespace Backdrop
{
    /// <summary>Everything the exploration screen draws behind its map on one layer.</summary>
    public sealed class ExplorationBackdrop
    {
        public int Layer;
        /// <summary>The place itself: the battle's backdrop of the same layer (§2.4).</summary>
        public LayerBackdrop Place;
        /// <summary>The ground colour laid over the whole place, under the map and the panels.</summary>
        public Rgba Veil;
    }

    public static class ExplorationBackdropSpec
    {
        /// <summary>地 (battle-visual-v1 §2.1): the veil's colour.</summary>
        public static readonly Rgba Ground = Rgba.Hex("#10141d");

        /// <summary>本文 (§2.1): the map's labels and the node letters on the ground.</summary>
        public static readonly Rgba Ink = Rgba.Hex("#ecebf2");
        public const float InkMinContrast = 4.5f;

        /// <summary>琥珀 (§2.1): the frame of what can be done now, the player's one warm colour.</summary>
        public static readonly Rgba Amber = Rgba.Hex("#e8a84c");
        public const float AmberMinContrast = 3f;

        // The veil's alpha by layer (index 0 is layer 1). 1, 2 and 4 are the bright layers of world-v1 §3
        // rule 3; layer 4 stays the brightest after the veil.
        private static readonly float[] VeilAlpha = { 0.72f, 0.68f, 0.35f, 0.70f, 0.35f, 0.35f, 0.35f };

        /// <summary>The exploration backdrop of <paramref name="layer"/>; anything outside 1〜7 is held to the nearest end.</summary>
        public static ExplorationBackdrop For(int layer)
        {
            int at = BackdropSpec.Clamp(layer);
            return new ExplorationBackdrop
            {
                Layer = at,
                Place = BackdropSpec.For(at),
                Veil = Ground.WithAlpha(VeilAlpha[at - 1]),
            };
        }

        /// <summary>The brightest the veiled backdrop gets at height <paramref name="y"/> (0 top, 1 bottom).</summary>
        public static Rgba BrightestAt(ExplorationBackdrop backdrop, float y)
        {
            return BackdropSpec.Over(backdrop.Veil, BackdropSpec.BrightestAt(backdrop.Place, y));
        }

        /// <summary>The lowest contrast <paramref name="ink"/> gets on the veiled backdrop, top to bottom, every haze at its densest.</summary>
        public static double LowestContrast(ExplorationBackdrop backdrop, Rgba ink)
        {
            double lowest = double.MaxValue;
            for (int step = 0; step <= Steps; step++)
            {
                Rgba under = BrightestAt(backdrop, step / (float)Steps);
                lowest = System.Math.Min(lowest, BackdropSpec.Contrast(ink, under));
            }
            return lowest;
        }

        /// <summary>The brightest luminance the veiled backdrop reaches anywhere, for comparing layers.</summary>
        public static double PeakLuminance(ExplorationBackdrop backdrop)
        {
            double peak = 0;
            for (int step = 0; step <= Steps; step++)
            {
                peak = System.Math.Max(peak, BackdropSpec.Luminance(BrightestAt(backdrop, step / (float)Steps)));
            }
            return peak;
        }

        private const int Steps = 40;
    }
}
