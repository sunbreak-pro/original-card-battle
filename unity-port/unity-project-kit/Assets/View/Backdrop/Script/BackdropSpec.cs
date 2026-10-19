// The battle backdrop of each layer (battle-visual-v1 §2.4, #89): the sky gradient, the haze laid over
// it, the light that breathes in the haze, the motes that drift through it, and the floor tile drawn
// on top. Pure C#, so `dotnet test` holds the values the screen draws.
//
// Only the backdrop changes with the layer; the UI keeps its colours (world-v1 §6). Layers 4 and 5
// are the values §2.4 fixed before #89; 2, 3, 6 and 7 are #89's and 1 is #609's, read off world-v1 §4
// (the light source, the brightness and the main colours of each layer). The art for a layer (a far view and a
// ground, #89's second half) goes between the sky and the haze, so everything here stays when it lands.
namespace Backdrop
{
    /// <summary>A colour in sRGB, 0〜1 per channel, with its alpha. The mockups blend in sRGB, so this does too.</summary>
    public readonly struct Rgba
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly float A;

        public Rgba(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        /// <summary>"#rrggbb" with the alpha given apart (the CSS rgba(...) of §2.4).</summary>
        public static Rgba Hex(string hex, float alpha = 1f)
        {
            string h = hex.TrimStart('#');
            return new Rgba(Channel(h, 0), Channel(h, 2), Channel(h, 4), alpha);
        }

        /// <summary>The CSS rgba(r, g, b, a) form §2.4 writes the floor tiles in.</summary>
        public static Rgba Bytes(int r, int g, int b, float alpha = 1f)
        {
            return new Rgba(r / 255f, g / 255f, b / 255f, alpha);
        }

        public Rgba WithAlpha(float alpha)
        {
            return new Rgba(R, G, B, alpha);
        }

        private static float Channel(string hex, int at)
        {
            return System.Convert.ToInt32(hex.Substring(at, 2), 16) / 255f;
        }
    }

    /// <summary>One stop of the sky gradient. <see cref="At"/> runs 0 at the top of the screen to 1 at the bottom.</summary>
    public readonly struct SkyStop
    {
        public readonly float At;
        public readonly Rgba Color;

        public SkyStop(float at, Rgba color)
        {
            At = at;
            Color = color;
        }
    }

    public enum HazeShape
    {
        /// <summary>Light from above: <see cref="Haze.Color"/> at the top, gone at <see cref="Haze.Size"/> of the height.</summary>
        TopGlow,
        /// <summary>A level band centred on <see cref="Haze.Y"/>, <see cref="Haze.Size"/> of the height across, soft at both edges.</summary>
        Band,
        /// <summary><see cref="Haze.Count"/> shafts of light, <see cref="Haze.Size"/> of the width each, leaning <see cref="Haze.Angle"/> degrees from upright.</summary>
        Shafts,
        /// <summary>A soft round glow at (<see cref="Haze.X"/>, <see cref="Haze.Y"/>), <see cref="Haze.Size"/> of the height across.</summary>
        Radial,
    }

    /// <summary>
    /// How a light breathes: its alpha falls by up to <see cref="Depth"/> of itself and comes back once
    /// every <see cref="PeriodMs"/>. <see cref="Flash"/> makes it a short flash at the start of the
    /// period instead (the lightning of layer 6), dark the rest of the time.
    /// </summary>
    public readonly struct Flicker
    {
        public readonly int PeriodMs;
        public readonly float Depth;
        public readonly bool Flash;

        public Flicker(int periodMs, float depth, bool flash = false)
        {
            PeriodMs = periodMs;
            Depth = depth;
            Flash = flash;
        }

        public static readonly Flicker Still = new Flicker(0, 0f);

        public bool Moves => PeriodMs > 0 && Depth > 0f;
    }

    /// <summary>One layer of haze over the sky. Positions are fractions of the screen, 0 at the top left.</summary>
    public sealed class Haze
    {
        public HazeShape Shape;
        public Rgba Color;
        public float X = 0.5f;
        public float Y = 0.5f;
        public float Size = 1f;
        public int Count = 1;
        public float Angle;
        public Flicker Flicker = Flicker.Still;
    }

    /// <summary>
    /// The specks that drift through the haze: dust in the light, sand, crystal sparks, salt, miasma.
    /// Speeds are in reference pixels (1920×1080) per second, + to the right and downward.
    /// </summary>
    public sealed class Motes
    {
        public int Count;
        public Rgba Color;
        public float SizePx = 4f;
        public float SpeedX;
        public float SpeedY;
        /// <summary>How far each speck's alpha dips as it twinkles, 0 (steady) to 1.</summary>
        public float Twinkle;

        public static readonly Motes None = new Motes();
    }

    /// <summary>Everything one layer draws behind the battle.</summary>
    public sealed class LayerBackdrop
    {
        public int Layer;
        /// <summary>The layer's name (world-v1 §4). Layer 2 keeps its masked name.</summary>
        public string Name;
        public SkyStop[] Sky;
        public Haze[] Hazes;
        public Motes Motes = Motes.None;
        /// <summary>床のタイル: the fill of a floor cell, drawn by the floor over the backdrop.</summary>
        public Rgba Tile;
    }

    public static class BackdropSpec
    {
        /// <summary>The layers of the dragon's lair (world-v1 §4).</summary>
        public const int Layers = 7;

        /// <summary>battle_ui_ux_v2 §4.3: a new layer fades in over the old one in the 400 ms of the switch banner.</summary>
        public const int CrossfadeMs = 400;

        /// <summary>The floor's band on the 1920×1080 screen (battle-visual-v1 §4.2: top y 560, cells 40 px tall).</summary>
        public const float FloorTop = 560f / 1080f;
        public const float FloorBottom = 600f / 1080f;

        /// <summary>
        /// The red of the cells an enemy's action reaches (battle-visual-v1 §2.1). It is drawn at 100%
        /// over the floor tile, and §2.3 holds it to a contrast of 3 there on every layer.
        /// </summary>
        public static readonly Rgba ReachRed = Rgba.Hex("#ff1f4f");
        public const float ReachRedMinContrast = 3f;

        private static readonly Rgba White = new Rgba(1f, 1f, 1f);
        private static readonly Rgba Sunlight = Rgba.Hex("#f4f8dc"); // the pale yellow-green sun of layer 1
        private static readonly Rgba Phosphor = Rgba.Hex("#b9a3ff"); // 燐光 (world-v1 §6)
        private static readonly Rgba Violet = Rgba.Hex("#8b6fd0");   // 菫 (world-v1 §6)

        private static readonly LayerBackdrop[] All =
        {
            // 1 燦光の樹海, bright (#609): young grass under the sun falling through the hole in the roof, in
            // near-upright shafts. The sun is pale yellow-green and white, never amber (world-v1 §3 rule 4), and no crystal grows here (rule 5).
            new LayerBackdrop
            {
                Layer = 1,
                Name = "燦光の樹海",
                Sky = new[] { new SkyStop(0f, Rgba.Hex("#d6e8a4")), new SkyStop(0.4f, Rgba.Hex("#7cab4a")), new SkyStop(1f, Rgba.Hex("#22391c")) },
                Hazes = new[]
                {
                    new Haze { Shape = HazeShape.Shafts, Color = White.WithAlpha(0.28f), X = 0.42f, Size = 0.05f, Count = 4, Angle = 8f, Flicker = new Flicker(6000, 0.25f) },
                    new Haze { Shape = HazeShape.TopGlow, Color = Sunlight.WithAlpha(0.30f), Size = 0.5f },
                },
                Motes = new Motes { Count = 24, Color = Sunlight.WithAlpha(0.35f), SizePx = 4f, SpeedX = 6f, SpeedY = 10f, Twinkle = 0.5f },
                Tile = Rgba.Bytes(10, 18, 12, 0.82f),
            },
            // 2 ■■の秘跡: sand and white stone in a slanting light; the first violet crystals sprout at the foot of the pillars.
            new LayerBackdrop
            {
                Layer = 2,
                Name = "■■の秘跡",
                Sky = new[] { new SkyStop(0f, Rgba.Hex("#b8a684")), new SkyStop(0.5f, Rgba.Hex("#8a7a5e")), new SkyStop(1f, Rgba.Hex("#4a4034")) },
                Hazes = new[]
                {
                    new Haze { Shape = HazeShape.Shafts, Color = White.WithAlpha(0.25f), X = 0.3f, Size = 0.06f, Count = 3, Angle = 18f, Flicker = new Flicker(7000, 0.2f) },
                    new Haze { Shape = HazeShape.Radial, Color = Phosphor.WithAlpha(0.12f), X = 0.5f, Y = 0.95f, Size = 0.5f, Flicker = new Flicker(4200, 0.3f) },
                },
                Motes = new Motes { Count = 20, Color = Rgba.Hex("#e8dcc0", 0.25f), SizePx = 3f, SpeedX = 14f, SpeedY = 4f, Twinkle = 0.2f },
                Tile = Rgba.Bytes(16, 13, 10, 0.82f),
            },
            // 3 大黒蛇の住処: the one dark upper layer; the crystals on the serpent's back are its only light.
            new LayerBackdrop
            {
                Layer = 3,
                Name = "大黒蛇の住処",
                Sky = new[] { new SkyStop(0f, Rgba.Hex("#1e1a3a")), new SkyStop(1f, Rgba.Hex("#0b0a18")) },
                Hazes = new[]
                {
                    new Haze { Shape = HazeShape.Radial, Color = Phosphor.WithAlpha(0.10f), X = 0.78f, Y = 0.25f, Size = 0.6f, Flicker = new Flicker(3200, 0.4f) },
                    new Haze { Shape = HazeShape.TopGlow, Color = Violet.WithAlpha(0.07f), Size = 0.6f },
                },
                Motes = new Motes { Count = 12, Color = Phosphor.WithAlpha(0.35f), SizePx = 3f, SpeedX = -3f, SpeedY = -6f, Twinkle = 0.7f },
                Tile = Rgba.Bytes(12, 14, 28, 0.78f),
            },
            // 4 輝晶の間, the brightest: #e4e6f2 → #b7bbd6 → #7d82a8, upright light streaks (white 35%), white 60% from above.
            new LayerBackdrop
            {
                Layer = 4,
                Name = "輝晶の間",
                Sky = new[] { new SkyStop(0f, Rgba.Hex("#e4e6f2")), new SkyStop(0.5f, Rgba.Hex("#b7bbd6")), new SkyStop(1f, Rgba.Hex("#7d82a8")) },
                Hazes = new[]
                {
                    new Haze { Shape = HazeShape.Shafts, Color = White.WithAlpha(0.35f), X = 0.5f, Size = 0.05f, Count = 5, Angle = 0f, Flicker = new Flicker(5000, 0.2f) },
                    new Haze { Shape = HazeShape.TopGlow, Color = White.WithAlpha(0.60f), Size = 0.45f },
                },
                Motes = new Motes { Count = 20, Color = White.WithAlpha(0.5f), SizePx = 3f, SpeedX = 0f, SpeedY = 3f, Twinkle = 0.9f },
                Tile = Rgba.Bytes(14, 16, 28, 0.82f),
            },
            // 5 血染めの死海: the red haze (rgba(140,30,40,.28)) over #1a0e12 → #070507; salt drifts white off the sea bed.
            new LayerBackdrop
            {
                Layer = 5,
                Name = "血染めの死海",
                Sky = new[] { new SkyStop(0f, Rgba.Hex("#1a0e12")), new SkyStop(1f, Rgba.Hex("#070507")) },
                Hazes = new[]
                {
                    new Haze { Shape = HazeShape.Band, Color = Rgba.Bytes(140, 30, 40, 0.28f), Y = 0.4f, Size = 0.7f, Flicker = new Flicker(8000, 0.25f) },
                },
                Motes = new Motes { Count = 18, Color = Rgba.Hex("#efe6dc", 0.2f), SizePx = 3f, SpeedX = 8f, SpeedY = -4f, Twinkle = 0.3f },
                Tile = Rgba.Bytes(12, 8, 10, 0.80f),
            },
            // 6 獄雷峡: dark; the black lightning shows only by its violet edge, so the light is a rare flash, not a glow (world-v1 §4.1).
            new LayerBackdrop
            {
                Layer = 6,
                Name = "獄雷峡",
                Sky = new[] { new SkyStop(0f, Rgba.Hex("#120b1c")), new SkyStop(1f, Rgba.Hex("#040208")) },
                Hazes = new[]
                {
                    new Haze { Shape = HazeShape.Shafts, Color = Violet.WithAlpha(0.22f), X = 0.62f, Size = 0.012f, Count = 1, Angle = -12f, Flicker = new Flicker(5200, 1f, flash: true) },
                    new Haze { Shape = HazeShape.TopGlow, Color = Violet.WithAlpha(0.05f), Size = 0.5f },
                },
                Motes = Motes.None,
                Tile = Rgba.Bytes(8, 6, 12, 0.84f),
            },
            // 7 果てなき世界, the darkest: a hollow made of miasma, lit by the miasma itself; the spring at its centre glows violet.
            new LayerBackdrop
            {
                Layer = 7,
                Name = "果てなき世界",
                Sky = new[] { new SkyStop(0f, Rgba.Hex("#080a1a")), new SkyStop(1f, Rgba.Hex("#020309")) },
                Hazes = new[]
                {
                    new Haze { Shape = HazeShape.Radial, Color = Violet.WithAlpha(0.22f), X = 0.5f, Y = 0.9f, Size = 0.9f, Flicker = new Flicker(7000, 0.3f) },
                },
                Motes = new Motes { Count = 30, Color = Violet.WithAlpha(0.3f), SizePx = 4f, SpeedX = 0f, SpeedY = -12f, Twinkle = 0.5f },
                Tile = Rgba.Bytes(6, 6, 14, 0.84f),
            },
        };

        /// <summary>The backdrop of <paramref name="layer"/>; anything outside 1〜7 is held to the nearest end.</summary>
        public static LayerBackdrop For(int layer)
        {
            return All[Clamp(layer) - 1];
        }

        public static int Clamp(int layer)
        {
            if (layer < 1) return 1;
            return layer > Layers ? Layers : layer;
        }

        /// <summary>The sky's colour at <paramref name="y"/> (0 top, 1 bottom), straight between its stops.</summary>
        public static Rgba SkyAt(LayerBackdrop backdrop, float y)
        {
            SkyStop[] sky = backdrop.Sky;
            if (y <= sky[0].At) return sky[0].Color;
            for (int i = 1; i < sky.Length; i++)
            {
                if (y > sky[i].At) continue;
                float t = (y - sky[i - 1].At) / (sky[i].At - sky[i - 1].At);
                return Mix(sky[i - 1].Color, sky[i].Color, t);
            }
            return sky[sky.Length - 1].Color;
        }

        /// <summary>
        /// The most a haze can lay on the point at height <paramref name="y"/> (0 top, 1 bottom) in any
        /// column, at the top of its breath: a shaft or a radial glow is taken where it is densest across.
        /// </summary>
        public static float HazeAlphaAt(Haze haze, float y)
        {
            float a = haze.Color.A;
            switch (haze.Shape)
            {
                case HazeShape.TopGlow:
                    return y >= haze.Size ? 0f : a * (1f - y / haze.Size);
                case HazeShape.Band:
                {
                    float half = haze.Size / 2f;
                    float d = System.Math.Abs(y - haze.Y);
                    return d >= half ? 0f : a * (1f - d / half);
                }
                case HazeShape.Shafts:
                    return a;
                case HazeShape.Radial:
                {
                    float r = haze.Size / 2f;
                    float d = System.Math.Abs(y - haze.Y);
                    return d >= r ? 0f : a * (1f - d / r);
                }
                default:
                    return 0f;
            }
        }

        /// <summary>The brightest the backdrop gets at height <paramref name="y"/>: the sky with every haze laid on at its densest.</summary>
        public static Rgba BrightestAt(LayerBackdrop backdrop, float y)
        {
            Rgba under = SkyAt(backdrop, y);
            foreach (Haze haze in backdrop.Hazes) under = Over(haze.Color.WithAlpha(HazeAlphaAt(haze, y)), under);
            return under;
        }

        /// <summary><paramref name="top"/> laid over the opaque <paramref name="under"/> at the top's alpha (sRGB, as the mockups do).</summary>
        public static Rgba Over(Rgba top, Rgba under)
        {
            float a = top.A;
            return new Rgba(top.R * a + under.R * (1f - a), top.G * a + under.G * (1f - a), top.B * a + under.B * (1f - a));
        }

        public static Rgba Mix(Rgba a, Rgba b, float t)
        {
            return new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }

        /// <summary>WCAG 2 relative luminance.</summary>
        public static double Luminance(Rgba c)
        {
            return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
        }

        /// <summary>WCAG 2 contrast ratio, 1〜21.</summary>
        public static double Contrast(Rgba a, Rgba b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);
            return (System.Math.Max(la, lb) + 0.05) / (System.Math.Min(la, lb) + 0.05);
        }

        /// <summary>
        /// The lowest contrast the reach red gets on <paramref name="backdrop"/>'s floor tile, over the
        /// floor's band from top to bottom, with every haze at its densest (battle-visual-v1 §2.3).
        /// </summary>
        public static double ReachRedContrast(LayerBackdrop backdrop)
        {
            double lowest = double.MaxValue;
            for (int step = 0; step <= 8; step++)
            {
                float y = FloorTop + (FloorBottom - FloorTop) * step / 8f;
                Rgba tile = Over(backdrop.Tile, BrightestAt(backdrop, y));
                lowest = System.Math.Min(lowest, Contrast(ReachRed, tile));
            }
            return lowest;
        }

        private static double Linear(float c)
        {
            return c <= 0.04045 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}
