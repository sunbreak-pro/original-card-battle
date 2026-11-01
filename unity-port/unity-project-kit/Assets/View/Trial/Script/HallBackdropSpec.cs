// The lamp-lit room of 継承の間, built from the same backdrop types as a layer (warm: it is the player's room),
// and the seven strata's skies on the hall's layer column. Port of scenes/meta/hall.js hallBackdropSpec and
// skyCss. 出立 paints the same room at half strength (departure.js: hallBackdropSpec(root, 0.5)).
//
// The JS reads the colours off the CSS tokens with the DS values as fall-backs (--ground #10141d, --amber
// #e8a84c, --wick #ffd89a); the tokens are those values, so they are written here as bytes, as the JS's
// Rgba.Bytes(r, g, b, a) gets them.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Backdrop;
using InheritanceCore;

namespace Trial.Script
{
    /// <summary>One stop of a stratum's sky as hall.js writes it into CSS: the colour at a whole percent.</summary>
    public readonly struct StratumStop
    {
        public readonly int Percent;
        public readonly Rgba Color;

        public StratumStop(int percent, Rgba color)
        {
            Percent = percent;
            Color = color;
        }
    }

    public static class HallBackdropSpec
    {
        /// <summary>--ground #10141d.</summary>
        public static readonly Rgba Ground = Rgba.Bytes(16, 20, 29, 1f);

        /// <summary>--amber #e8a84c.</summary>
        public static readonly Rgba Amber = Rgba.Bytes(232, 168, 76, 1f);

        /// <summary>--wick #ffd89a.</summary>
        public static readonly Rgba Wick = Rgba.Bytes(255, 216, 154, 1f);

        /// <summary>
        /// The hall's room: Layer 0, named 継承の間; a sky from a breath of amber to the ground and on to near black;
        /// a wide amber glow at the top centre and a small wick glow at the right, each breathing; 26 wick motes
        /// rising slowly. <paramref name="strength"/> scales the glows' and the motes' alpha (1 in the hall, 0.5 on
        /// 出立), as the JS multiplies before Rgba rounds to float.
        /// </summary>
        // js: scenes/meta/hall.js:17
        public static LayerBackdrop For(double strength = 1)
        {
            return new LayerBackdrop
            {
                Layer = 0,
                Name = InheritanceText.HallTitle,
                Sky = new[]
                {
                    new SkyStop(0f, BackdropSpec.Mix(Ground, Amber, 0.06f)),
                    new SkyStop(0.55f, Ground),
                    new SkyStop(1f, BackdropSpec.Mix(Ground, new Rgba(0f, 0f, 0f, 1f), 0.45f)),
                },
                Hazes = new[]
                {
                    new Haze
                    {
                        Shape = HazeShape.Radial, Color = Amber.WithAlpha((float)(0.16 * strength)),
                        X = 0.5f, Y = 0.02f, Size = 1.25f, Flicker = new Flicker(4200, 0.28f),
                    },
                    new Haze
                    {
                        Shape = HazeShape.Radial, Color = Wick.WithAlpha((float)(0.07 * strength)),
                        X = 0.86f, Y = 0.30f, Size = 0.7f, Flicker = new Flicker(5300, 0.35f),
                    },
                },
                Motes = new Motes
                {
                    Count = 26, Color = Wick.WithAlpha((float)(0.22 * strength)), SizePx = 3f, SpeedX = 3f, SpeedY = -5f, Twinkle = 0.6f,
                },
            };
        }

        /// <summary>
        /// A stratum's sky on the hall's layer column, read off the bridge's per-layer stops: each stop's colour at
        /// Math.round(At × 100) percent, top to bottom (the band's CSS linear-gradient(180deg, …)).
        /// </summary>
        // js: scenes/meta/hall.js:51
        public static IReadOnlyList<StratumStop> StratumSky(int layer)
        {
            LayerBackdrop spec = BackdropSpec.For(layer);
            var stops = new StratumStop[spec.Sky.Length];
            for (int i = 0; i < stops.Length; i++) stops[i] = new StratumStop(JsRound(spec.Sky[i].At * 100.0), spec.Sky[i].Color);
            return new ReadOnlyCollection<StratumStop>(stops);
        }

        // Math.round: halves go up (toward +∞), unlike C#'s banker's rounding.
        private static int JsRound(double v) => (int)Math.Floor(v + 0.5);
    }
}
