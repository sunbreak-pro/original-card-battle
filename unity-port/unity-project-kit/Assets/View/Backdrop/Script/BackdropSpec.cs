// The battle backdrop of each layer (battle-visual-v1 §2.4, #89): the sky gradient, the haze laid over
// it, the light that breathes in the haze, the motes that drift through it, and the floor tile drawn
// on top. Pure C#, so `dotnet test` holds the values the screen draws.
//
// Only the backdrop changes with the layer; the UI keeps its colours (world-v1 §6). The light source and the
// brightness of each layer come from world-v1 §3-§4. The colours are 2026-11-03's (こうだいさん asked for a colour of
// its own on every layer, layer 1 brighter, a pale-yellow layer and a starry layer 7; #89): lime, lemon cream, teal,
// a white prism, crimson, cobalt and a nebula, chosen from three proposals by their renders. Each layer also stands a
// far view drawn in code (Scenery, BackdropScenery) where the painted art for the layer will go, between the sky and
// the haze, so the haze and the specks stay when the art lands.
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
        /// <summary>
        /// Picks the specks' places and beats. A layer with more than one set gives each its own seed, so the sets
        /// never land on the same places; 0 keeps the places every layer had before there were several.
        /// </summary>
        public int Seed;
        /// <summary>The specks hold their places and only twinkle (a field of stars): no sway. Their speed should be 0.</summary>
        public bool Still;

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
        /// <summary>Further sets of specks, drawn after <see cref="Motes"/> (the stars of layer 7, the pollen of layer 1).</summary>
        public Motes[] MoreMotes = System.Array.Empty<Motes>();
        /// <summary>The far view drawn in code, back to front, between the sky and the haze (BackdropScenery).</summary>
        public Scenery[] Scenery = System.Array.Empty<Scenery>();
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
        private static readonly Rgba Lemon = Rgba.Hex("#f6fbcc");    // the pale lemon sun of layer 1 (hue ~66, never towards amber)
        private static readonly Rgba Cream = Rgba.Hex("#fbf8e2");    // the pale yellow of layer 2's sand and stone
        private static readonly Rgba Phosphor = Rgba.Hex("#b9a3ff"); // 燐光 (world-v1 §6)
        private static readonly Rgba Violet = Rgba.Hex("#8b6fd0");   // 菫 (world-v1 §6)
        private static readonly Rgba Aqua = Rgba.Hex("#bff3ff");     // the prism tint of layer 4's crystals
        private static readonly Rgba Lilac = Rgba.Hex("#d6c0ff");    // the other prism tint of layer 4, bleeding into the ground by its great crystals
        private static readonly Rgba Ultramarine = Rgba.Hex("#3a3ccc"); // the far light down the canyon of layer 6
        private static readonly Rgba Starlight = Rgba.Hex("#eef0ff"); // the cold white of layer 7's stars

        private static readonly LayerBackdrop[] All =
        {
            // 1 燦光の樹海, the brightest of the upper layers: pale lemon sun pours through the hole in the roof onto young lime
            // leaves, in near-upright shafts. Far trunks fade into the green mist, a leafy bank fills the left edge, two near
            // trunks frame the hole (a bough reaches in from the right one) and a leafy fringe hangs from the roof. Lemon and
            // white only, never amber (world-v1 §3 rule 4), and no crystal grows here (rule 5).
            new LayerBackdrop
            {
                Layer = 1,
                Name = "燦光の樹海",
                Sky = new[]
                {
                    new SkyStop(0f, Rgba.Hex("#fafde0")), new SkyStop(0.22f, Rgba.Hex("#ebf8ac")), new SkyStop(0.48f, Rgba.Hex("#c4e878")),
                    new SkyStop(0.75f, Rgba.Hex("#96ce56")), new SkyStop(1f, Rgba.Hex("#62a242")),
                },
                Hazes = new[]
                {
                    new Haze { Shape = HazeShape.Shafts, Color = Lemon.WithAlpha(0.34f), X = 0.46f, Size = 0.055f, Count = 5, Angle = 9f, Flicker = new Flicker(6000, 0.25f) },
                    new Haze { Shape = HazeShape.Radial, Color = White.WithAlpha(0.45f), X = 0.52f, Y = 0f, Size = 0.8f, Flicker = new Flicker(9000, 0.15f) },
                    new Haze { Shape = HazeShape.TopGlow, Color = Lemon.WithAlpha(0.35f), Size = 0.45f },
                    new Haze { Shape = HazeShape.Band, Color = Rgba.Hex("#e2f4b0", 0.22f), Y = 0.47f, Size = 0.2f, Flicker = new Flicker(11000, 0.3f) },
                },
                Motes = new Motes { Count = 26, Color = Lemon.WithAlpha(0.55f), SizePx = 4f, SpeedX = 5f, SpeedY = 9f, Twinkle = 0.5f },
                MoreMotes = new[]
                {
                    new Motes { Count = 18, Color = Rgba.Hex("#eaffb0", 0.6f), SizePx = 3f, SpeedX = 10f, SpeedY = -4f, Twinkle = 0.8f, Seed = 7 },
                },
                Scenery = new[]
                {
                    new Scenery
                    {
                        Kind = SceneryKind.Trunks, Count = 14, Seed = 11, X0 = 0f, X1 = 1f, Top = 0.02f, Base = 0.52f, Size = 0.012f, Vary = 0.6f, Lean = 3f,
                        Color = Rgba.Hex("#a9cc78", 0.45f), Foot = Rgba.Hex("#cfe8a0", 0.3f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Ridge, Count = 3, Seed = 49, X0 = 0f, X1 = 0.24f, Top = 0.27f, Base = 0.53f, Vary = 0.85f, Rough = 0.2f,
                        Color = Rgba.Hex("#94c464", 0.36f), Foot = Rgba.Hex("#72aa4c", 0.5f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Ridge, Count = 12, Seed = 12, X0 = 0f, X1 = 1f, Top = 0.46f, Base = 0.53f, Vary = 0.8f, Rough = 0.7f,
                        Color = Rgba.Hex("#6da844", 0.7f), Foot = Rgba.Hex("#4f8a36", 0.85f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Trunks, Count = 2, Seed = 98, X0 = -0.4f, X1 = 1.4f, Top = -0.1f, Base = 0.53f, Size = 0.05f, Vary = 0.2f, Lean = 2f,
                        Color = Rgba.Hex("#1f3a18", 0.97f), Foot = Rgba.Hex("#2a4a20", 0.97f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Ridge, Count = 4, Seed = 13, X0 = 0f, X1 = 1f, Top = -0.03f, Base = 0.13f, Vary = 1f, Rough = 0.7f, Hang = true,
                        Color = Rgba.Hex("#2f5a24", 0.92f), Foot = Rgba.Hex("#5f9a3c", 0.8f),
                    },
                },
                Tile = Rgba.Bytes(10, 20, 12, 0.88f),
            },
            // 2 ■■の秘跡, bright and the pale yellow layer: cream stone and lemon sand in a slanting light. A far colonnade stands
            // half buried in low dunes, two near columns frame the edges, and the first violet crystals sprout at the pillars'
            // feet, the one colour that floats on the pale screen (world-v1 §4.1).
            new LayerBackdrop
            {
                Layer = 2,
                Name = "■■の秘跡",
                Sky = new[]
                {
                    new SkyStop(0f, Cream), new SkyStop(0.35f, Rgba.Hex("#ebe5bb")),
                    new SkyStop(0.7f, Rgba.Hex("#cfc999")), new SkyStop(1f, Rgba.Hex("#9a9268")),
                },
                Hazes = new[]
                {
                    new Haze { Shape = HazeShape.Shafts, Color = White.WithAlpha(0.38f), X = 0.35f, Size = 0.075f, Count = 3, Angle = 22f, Flicker = new Flicker(7000, 0.2f) },
                    new Haze { Shape = HazeShape.TopGlow, Color = Rgba.Hex("#fffdf0", 0.35f), Size = 0.35f },
                    new Haze { Shape = HazeShape.Band, Color = Rgba.Hex("#efe9c4", 0.3f), Y = 0.48f, Size = 0.2f },
                    new Haze { Shape = HazeShape.Radial, Color = Phosphor.WithAlpha(0.22f), X = 0.08f, Y = 0.47f, Size = 0.16f, Flicker = new Flicker(4200, 0.35f) },
                    new Haze { Shape = HazeShape.Radial, Color = Phosphor.WithAlpha(0.22f), X = 0.92f, Y = 0.47f, Size = 0.16f, Flicker = new Flicker(5300, 0.35f) },
                },
                Motes = new Motes { Count = 26, Color = Rgba.Hex("#f0eac8", 0.4f), SizePx = 3f, SpeedX = 16f, SpeedY = 3f, Twinkle = 0.2f },
                MoreMotes = new[]
                {
                    new Motes { Count = 8, Color = Phosphor.WithAlpha(0.45f), SizePx = 3f, SpeedX = 2f, SpeedY = -8f, Twinkle = 0.8f, Seed = 21 },
                },
                Scenery = new[]
                {
                    new Scenery { Kind = SceneryKind.Pillars, Color = Rgba.Hex("#e3dcb4", 0.6f), Foot = Rgba.Hex("#d2c99a", 0.7f), Light = Rgba.Hex("#fffdf2", 0.7f), Count = 11, Seed = 201, Top = 0.17f, Base = 0.515f, Size = 0.014f, Vary = 0.35f, Rough = 0.35f, Lean = 1.5f },
                    new Scenery { Kind = SceneryKind.Ridge, Color = Rgba.Hex("#d2cb9c", 0.8f), Foot = Rgba.Hex("#b3aa7c", 0.92f), Count = 4, Seed = 13, Top = 0.465f, Base = 0.535f, Vary = 0.9f, Rough = 0.1f },
                    new Scenery { Kind = SceneryKind.Crystals, Color = Rgba.Hex("#7a5cc8", 0.6f), Foot = Rgba.Hex("#6248a8", 0.6f), Light = Rgba.Hex("#c7b5ff", 0.65f), Count = 2, Seed = 48151, X0 = -0.0245f, X1 = 1.0155f, Top = 0.484f, Base = 0.512f, Size = 0.006f, Vary = 0.5f, Lean = 10f },
                    new Scenery { Kind = SceneryKind.Pillars, Color = Rgba.Hex("#8a8670", 0.95f), Foot = Rgba.Hex("#6f6a52", 0.95f), Light = Rgba.Hex("#f4f0da", 0.95f), Count = 2, Seed = 198, X0 = -0.35f, X1 = 1.35f, Top = -0.1f, Base = 0.545f, Size = 0.055f, Vary = 0.15f, Rough = 0.5f },
                    new Scenery { Kind = SceneryKind.Crystals, Color = Rgba.Hex("#6a4fc0", 0.95f), Foot = Rgba.Hex("#4e3a96"), Light = Rgba.Hex("#c7b5ff"), Count = 2, Seed = 464, X0 = -0.35f, X1 = 1.35f, Top = 0.42f, Base = 0.53f, Size = 0.012f, Vary = 0.5f, Lean = 10f },
                },
                Tile = Rgba.Bytes(20, 18, 12, 0.88f),
            },
            // 3 大黒蛇の住処: the one dim upper layer, indigo above and deep teal below. A black serpent coils out of the left wall
            // along the cave roof, and the violet crystals on its back are the only light, so a violet haze hangs round them; a
            // teal glow low in the cave keeps the figures readable. Two ranges of rock stand in the dark behind.
            new LayerBackdrop
            {
                Layer = 3,
                Name = "大黒蛇の住処",
                Sky = new[]
                {
                    new SkyStop(0f, Rgba.Hex("#0e0c28")), new SkyStop(0.3f, Rgba.Hex("#141e48")), new SkyStop(0.5f, Rgba.Hex("#18465e")),
                    new SkyStop(0.7f, Rgba.Hex("#0f2a3e")), new SkyStop(1f, Rgba.Hex("#060c18")),
                },
                Hazes = new[]
                {
                    new Haze { Shape = HazeShape.TopGlow, Color = Violet.WithAlpha(0.12f), Size = 0.38f },
                    new Haze { Shape = HazeShape.Band, Color = Phosphor.WithAlpha(0.08f), Y = 0.1f, Size = 0.2f, Flicker = new Flicker(4600, 0.35f) },
                    new Haze { Shape = HazeShape.Radial, Color = Phosphor.WithAlpha(0.14f), X = 0.6f, Y = 0.14f, Size = 0.5f, Flicker = new Flicker(3200, 0.4f) },
                    new Haze { Shape = HazeShape.Band, Color = Rgba.Hex("#2f8a9a", 0.16f), Y = 0.44f, Size = 0.26f, Flicker = new Flicker(8000, 0.25f) },
                },
                Motes = new Motes { Count = 16, Color = Phosphor.WithAlpha(0.55f), SizePx = 3f, SpeedX = -3f, SpeedY = -6f, Twinkle = 0.8f },
                MoreMotes = new[]
                {
                    new Motes { Count = 14, Color = Rgba.Hex("#7fd0d8", 0.25f), SizePx = 2f, SpeedX = 4f, SpeedY = -2f, Twinkle = 0.5f, Seed = 3 },
                },
                Scenery = new[]
                {
                    new Scenery
                    {
                        Kind = SceneryKind.Ridge, Count = 6, Seed = 301, X0 = 0f, X1 = 1f, Top = 0.2f, Base = 0.53f, Vary = 0.7f, Rough = 0.75f,
                        Color = Rgba.Hex("#1a1c44", 0.6f), Foot = Rgba.Hex("#14223e", 0.7f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Ridge, Count = 9, Seed = 41, X0 = 0f, X1 = 1f, Top = 0.3f, Base = 0.52f, Vary = 0.65f, Rough = 1f,
                        Color = Rgba.Hex("#123048", 0.55f), Foot = Rgba.Hex("#1a4a5e", 0.45f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Wall, Count = 4, Seed = 43, X0 = 0f, X1 = 0.2f, Top = -0.03f, Base = 0.53f, Vary = 0.8f, Rough = 0.7f,
                        Color = Rgba.Hex("#081224", 0.95f), Foot = Rgba.Hex("#0e2232", 0.9f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Coil, Count = 1, Seed = 326, X0 = 0.78f, X1 = 1f, Top = 0.24f, Base = 0.53f, Size = 0.032f, Vary = 0.5f,
                        Color = Rgba.Hex("#04060c", 0.97f), Foot = Rgba.Hex("#060a14", 0.97f), Light = Rgba.Hex("#8c70e8", 0.85f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Coil, Count = 2, Seed = 44, X0 = 0f, X1 = 0.92f, Top = -0.01f, Base = 0.18f, Size = 0.04f, Vary = 0.6f,
                        Color = Rgba.Hex("#04060c", 0.97f), Foot = Rgba.Hex("#060a14", 0.97f), Light = Rgba.Hex("#8c70e8", 0.88f),
                    },
                },
                Tile = Rgba.Bytes(10, 16, 28, 0.80f),
            },
            // 4 輝晶の間, the brightest: white with lavender and aqua prism tints. Crystal pillars stand everywhere, far and near,
            // and hang from the roof; two great lilac clusters stand at the edges and their light bleeds into the ground. Light
            // comes from every side (world-v1 §4), so the figures stand black against it.
            new LayerBackdrop
            {
                Layer = 4,
                Name = "輝晶の間",
                Sky = new[]
                {
                    new SkyStop(0f, Rgba.Hex("#ffffff")), new SkyStop(0.25f, Rgba.Hex("#f1ecff")), new SkyStop(0.5f, Rgba.Hex("#dcd4f6")),
                    new SkyStop(0.7f, Rgba.Hex("#c4e4ee")), new SkyStop(1f, Rgba.Hex("#8d9ccc")),
                },
                Hazes = new[]
                {
                    new Haze { Shape = HazeShape.TopGlow, Color = White.WithAlpha(0.6f), Size = 0.45f },
                    new Haze { Shape = HazeShape.Shafts, Color = White.WithAlpha(0.3f), X = 0.5f, Size = 0.04f, Count = 5, Angle = -14f, Flicker = new Flicker(5000, 0.25f) },
                    new Haze { Shape = HazeShape.Shafts, Color = White.WithAlpha(0.25f), X = 0.5f, Size = 0.035f, Count = 4, Angle = 15f, Flicker = new Flicker(6400, 0.3f) },
                    new Haze { Shape = HazeShape.Radial, Color = Aqua.WithAlpha(0.3f), X = 0.12f, Y = 0.35f, Size = 0.5f, Flicker = new Flicker(5600, 0.3f) },
                    new Haze { Shape = HazeShape.Radial, Color = Lilac.WithAlpha(0.3f), X = 0.93f, Y = 0.46f, Size = 0.58f, Flicker = new Flicker(7200, 0.3f) },
                    new Haze { Shape = HazeShape.Radial, Color = Rgba.Hex("#b9a0ff", 0.26f), X = 0.05f, Y = 0.56f, Size = 0.38f, Flicker = new Flicker(6600, 0.3f) },
                },
                Motes = new Motes { Count = 26, Color = White.WithAlpha(0.7f), SizePx = 3f, SpeedX = 0f, SpeedY = 3f, Twinkle = 0.95f },
                MoreMotes = new[]
                {
                    new Motes { Count = 14, Color = Rgba.Hex("#c8fbff", 0.7f), SizePx = 2.5f, SpeedX = 2f, SpeedY = -2f, Twinkle = 0.9f, Seed = 4 },
                    new Motes { Count = 12, Color = Rgba.Hex("#e6d6ff", 0.7f), SizePx = 2.5f, SpeedX = -2f, SpeedY = 2f, Twinkle = 0.9f, Seed = 9 },
                },
                Scenery = new[]
                {
                    new Scenery
                    {
                        Kind = SceneryKind.Crystals, Count = 11, Seed = 51, X0 = 0f, X1 = 1f, Top = 0.02f, Base = 0.515f, Size = 0.035f, Vary = 0.6f, Lean = 6f,
                        Color = Rgba.Hex("#d6ccf4", 0.55f), Foot = Rgba.Hex("#cde6f0", 0.45f), Light = Rgba.Hex("#ffffff", 0.7f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Ridge, Count = 10, Seed = 52, X0 = 0f, X1 = 1f, Top = -0.03f, Base = 0.1f, Vary = 1f, Rough = 1f, Hang = true,
                        Color = Rgba.Hex("#c8c0ec", 0.7f), Foot = Rgba.Hex("#dcd6f8", 0.6f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Crystals, Count = 8, Seed = 53, X0 = 0.04f, X1 = 0.96f, Top = 0.43f, Base = 0.52f, Size = 0.014f, Vary = 0.5f, Lean = 10f,
                        Color = Rgba.Hex("#a69ed8", 0.85f), Foot = Rgba.Hex("#9aa8d4", 0.85f), Light = Rgba.Hex("#f4fbff", 0.95f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Crystals, Count = 2, Seed = 854, X0 = -0.35f, X1 = 1.35f, Top = -0.08f, Base = 0.51f, Size = 0.07f, Vary = 0.5f, Lean = 5f,
                        Color = Rgba.Hex("#8f80d0", 0.92f), Foot = Rgba.Hex("#7a6cc0", 0.95f), Light = Rgba.Hex("#f2eeff", 0.95f),
                    },
                },
                Tile = Rgba.Bytes(14, 16, 28, 0.84f),
            },
            // 5 血染めの死海, middle brightness: crimson (never orange) scattered light over a dried sea bed. A giant pair of ribs
            // arches over the floor like a gate, faint ribs stand far off in the haze, a soft salt flat lies along the shore and its
            // salt crusts white along the horizon.
            new LayerBackdrop
            {
                Layer = 5,
                Name = "血染めの死海",
                Sky = new[]
                {
                    new SkyStop(0f, Rgba.Hex("#2e0d18")), new SkyStop(0.4f, Rgba.Hex("#6a1f2e")),
                    new SkyStop(0.62f, Rgba.Hex("#8a3040")), new SkyStop(1f, Rgba.Hex("#2a0a12")),
                },
                Hazes = new[]
                {
                    new Haze { Shape = HazeShape.Band, Color = Rgba.Bytes(170, 42, 60, 0.32f), Y = 0.45f, Size = 0.6f, Flicker = new Flicker(8000, 0.25f) },
                    new Haze { Shape = HazeShape.Band, Color = Rgba.Hex("#d84860", 0.16f), Y = 0.5f, Size = 0.24f, Flicker = new Flicker(10000, 0.2f) },
                    new Haze { Shape = HazeShape.Band, Color = Rgba.Hex("#f0dfe2", 0.08f), Y = 0.5f, Size = 0.12f },
                    new Haze { Shape = HazeShape.TopGlow, Color = Rgba.Hex("#12040a", 0.4f), Size = 0.3f },
                },
                Motes = new Motes { Count = 26, Color = Rgba.Hex("#f3ece8", 0.35f), SizePx = 3f, SpeedX = 8f, SpeedY = -4f, Twinkle = 0.3f },
                MoreMotes = new[]
                {
                    new Motes { Count = 12, Color = Rgba.Hex("#c04050", 0.18f), SizePx = 8f, SpeedX = 4f, SpeedY = -2f, Twinkle = 0.4f, Seed = 51 },
                },
                Scenery = new[]
                {
                    new Scenery { Kind = SceneryKind.Ridge, Color = Rgba.Hex("#8a4a56", 0.45f), Foot = Rgba.Hex("#b07c84", 0.55f), Count = 5, Seed = 501, Top = 0.36f, Base = 0.52f, Vary = 0.7f, Rough = 0.3f },
                    new Scenery { Kind = SceneryKind.Ribs, Color = Rgba.Hex("#c8a2a8", 0.18f), Foot = Rgba.Hex("#9a6a72", 0.2f), Count = 2, Seed = 6, X0 = 0.4f, X1 = 0.6f, Top = 0.27f, Base = 0.52f, Size = 0.007f, Vary = 0.6f },
                    new Scenery { Kind = SceneryKind.Ribs, Color = Rgba.Hex("#22080f", 0.95f), Foot = Rgba.Hex("#3a161e", 0.95f), Count = 2, Seed = 131, X0 = -0.1f, X1 = 1.1f, Top = -0.02f, Base = 0.545f, Size = 0.032f, Vary = 0.4f },
                    new Scenery { Kind = SceneryKind.Ridge, Color = Rgba.Hex("#f2e4ea", 0.2f), Foot = Rgba.Hex("#d6b4c0", 0.24f), Count = 4, Seed = 63, Top = 0.465f, Base = 0.535f, Vary = 0.5f, Rough = 0.15f },
                    new Scenery { Kind = SceneryKind.Ridge, Color = Rgba.Hex("#dcc8cc", 0.6f), Foot = Rgba.Hex("#b89ca2", 0.8f), Count = 18, Seed = 504, Top = 0.49f, Base = 0.56f, Vary = 0.6f, Rough = 0.85f },
                },
                Tile = Rgba.Bytes(20, 8, 12, 0.82f),
            },
            // 6 獄雷峡: deep cobalt and violet. Canyon walls rise on both sides with violet and electric-blue rims where the black
            // lightning's edge lights them, and a range of spires stands far down the canyon. The lightning shows only by its violet
            // edge round a black core, as a rare flash, not a glow (world-v1 §4.1).
            new LayerBackdrop
            {
                Layer = 6,
                Name = "獄雷峡",
                Sky = new[]
                {
                    new SkyStop(0f, Rgba.Hex("#04061c")), new SkyStop(0.35f, Rgba.Hex("#0c1450")), new SkyStop(0.5f, Rgba.Hex("#181a70")),
                    new SkyStop(0.7f, Rgba.Hex("#0a0e3a")), new SkyStop(1f, Rgba.Hex("#020310")),
                },
                Hazes = new[]
                {
                    new Haze { Shape = HazeShape.Shafts, Color = Rgba.Hex("#c9b6ff", 0.24f), X = 0.6f, Size = 0.012f, Count = 1, Angle = -12f, Flicker = new Flicker(5200, 1f, flash: true) },
                    new Haze { Shape = HazeShape.Shafts, Color = Rgba.Hex("#050308", 0.85f), X = 0.6f, Size = 0.004f, Count = 1, Angle = -12f, Flicker = new Flicker(5200, 1f, flash: true) },
                    new Haze { Shape = HazeShape.Band, Color = Ultramarine.WithAlpha(0.13f), Y = 0.38f, Size = 0.4f, Flicker = new Flicker(9000, 0.3f) },
                    new Haze { Shape = HazeShape.TopGlow, Color = Violet.WithAlpha(0.06f), Size = 0.5f },
                },
                Motes = new Motes { Count = 12, Color = Rgba.Hex("#b8a8ff", 0.6f), SizePx = 2.5f, SpeedX = 0f, SpeedY = 30f, Twinkle = 0.9f },
                MoreMotes = new[]
                {
                    new Motes { Count = 8, Color = Rgba.Hex("#9ff0ff", 0.5f), SizePx = 2f, SpeedX = -6f, SpeedY = 14f, Twinkle = 1f, Seed = 8 },
                },
                Scenery = new[]
                {
                    new Scenery
                    {
                        Kind = SceneryKind.Ridge, Count = 7, Seed = 601, X0 = 0f, X1 = 1f, Top = 0.14f, Base = 0.53f, Vary = 0.8f, Rough = 0.9f,
                        Color = Rgba.Hex("#0a0e34", 0.6f), Foot = Rgba.Hex("#10164a", 0.6f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Wall, Count = 8, Seed = 71, X0 = 0.005f, X1 = 0.27f, Top = -0.03f, Base = 0.53f, Vary = 0.6f, Rough = 0.6f,
                        Color = Rgba.Hex("#8466f4", 0.55f), Foot = Rgba.Hex("#4a3cb0", 0.4f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Wall, Count = 8, Seed = 71, X0 = 0f, X1 = 0.265f, Top = -0.03f, Base = 0.53f, Vary = 0.6f, Rough = 0.6f,
                        Color = Rgba.Hex("#03040e", 0.96f), Foot = Rgba.Hex("#080b26", 0.94f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Wall, Count = 8, Seed = 72, X0 = 0.73f, X1 = 0.995f, Top = -0.03f, Base = 0.53f, Vary = 0.6f, Rough = 0.6f,
                        Color = Rgba.Hex("#64a6f4", 0.51f), Foot = Rgba.Hex("#3a58b8", 0.4f),
                    },
                    new Scenery
                    {
                        Kind = SceneryKind.Wall, Count = 8, Seed = 72, X0 = 0.735f, X1 = 1f, Top = -0.03f, Base = 0.53f, Vary = 0.6f, Rough = 0.6f,
                        Color = Rgba.Hex("#03040e", 0.96f), Foot = Rgba.Hex("#080b26", 0.94f),
                    },
                },
                Tile = Rgba.Bytes(8, 8, 20, 0.84f),
            },
            // 7 果てなき世界, the darkest: no floor, no roof, no walls, a hollow of miasma under a field of stars. Nebulae of
            // violet, teal and rose glow in it, rock shards float, a few great stars burn white, and the spring glows violet low at
            // the centre.
            new LayerBackdrop
            {
                Layer = 7,
                Name = "果てなき世界",
                Sky = new[] { new SkyStop(0f, Rgba.Hex("#05061a")), new SkyStop(0.5f, Rgba.Hex("#0a0b26")), new SkyStop(1f, Rgba.Hex("#04040f")) },
                Hazes = new[]
                {
                    new Haze { Shape = HazeShape.Radial, Color = Rgba.Hex("#6a4fc8", 0.34f), X = 0.28f, Y = 0.22f, Size = 0.75f, Flicker = new Flicker(11000, 0.2f) },
                    new Haze { Shape = HazeShape.Radial, Color = Rgba.Hex("#2f8fa8", 0.24f), X = 0.76f, Y = 0.3f, Size = 0.6f, Flicker = new Flicker(13000, 0.25f) },
                    new Haze { Shape = HazeShape.Radial, Color = Rgba.Hex("#b0509c", 0.16f), X = 0.55f, Y = 0.08f, Size = 0.45f, Flicker = new Flicker(9000, 0.3f) },
                    new Haze { Shape = HazeShape.Band, Color = Rgba.Hex("#2a2a66", 0.25f), Y = 0.42f, Size = 0.3f },
                    new Haze { Shape = HazeShape.Radial, Color = Rgba.Hex("#9d84f0", 0.32f), X = 0.5f, Y = 0.86f, Size = 0.6f, Flicker = new Flicker(7000, 0.3f) },
                    new Haze { Shape = HazeShape.Radial, Color = Rgba.Hex("#d8ccff", 0.35f), X = 0.5f, Y = 0.85f, Size = 0.22f, Flicker = new Flicker(4000, 0.3f) },
                },
                Motes = new Motes { Count = 24, Color = Violet.WithAlpha(0.35f), SizePx = 4f, SpeedX = 0f, SpeedY = -12f, Twinkle = 0.5f },
                MoreMotes = new[]
                {
                    new Motes { Count = 80, Color = Rgba.Hex("#dfe4ff", 0.7f), SizePx = 2.5f, Twinkle = 0.6f, Seed = 71, Still = true },
                    new Motes { Count = 14, Color = Starlight, SizePx = 5f, Twinkle = 0.4f, Seed = 72, Still = true },
                    new Motes { Count = 12, Color = Rgba.Hex("#9ad8ff", 0.8f), SizePx = 3.5f, Twinkle = 0.7f, Seed = 73, Still = true },
                    new Motes { Count = 10, Color = Rgba.Hex("#e0b0ff", 0.7f), SizePx = 3.5f, Twinkle = 0.8f, Seed = 74, Still = true },
                    new Motes { Count = 6, Color = Starlight, SizePx = 7f, Twinkle = 0.3f, Seed = 75, Still = true },
                    new Motes { Count = 2, Color = White, SizePx = 8f, Twinkle = 0.25f, Seed = 78, Still = true },
                },
                Scenery = new[]
                {
                    new Scenery { Kind = SceneryKind.Shards, Color = Rgba.Hex("#1e2052", 0.65f), Foot = Rgba.Hex("#22244c", 0.65f), Light = Rgba.Hex("#5a52a8", 0.6f), Count = 16, Seed = 701, Top = 0.04f, Base = 0.5f, Size = 0.007f, Lean = 25f },
                    new Scenery { Kind = SceneryKind.Shards, Color = Rgba.Hex("#17173e", 0.92f), Foot = Rgba.Hex("#141436", 0.92f), Light = Rgba.Hex("#8f7ae0", 0.85f), Count = 6, Seed = 702, X0 = 0.05f, X1 = 0.95f, Top = 0.04f, Base = 0.3f, Size = 0.018f, Lean = 18f },
                    new Scenery { Kind = SceneryKind.Shards, Color = Rgba.Hex("#1a1a46", 0.95f), Foot = Rgba.Hex("#101032", 0.95f), Light = Phosphor.WithAlpha(0.9f), Count = 2, Seed = 383, X0 = -0.35f, X1 = 1.35f, Top = 0.05f, Base = 0.52f, Size = 0.06f, Lean = 12f },
                },
                Tile = Rgba.Bytes(6, 6, 16, 0.84f),
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

        /// <summary>Every set of specks <paramref name="backdrop"/> draws, in drawing order: <see cref="LayerBackdrop.Motes"/>, then <see cref="LayerBackdrop.MoreMotes"/>. Empty sets are left out.</summary>
        public static Motes[] Specks(LayerBackdrop backdrop)
        {
            var all = new System.Collections.Generic.List<Motes>();
            if (backdrop == null) return all.ToArray();
            if (backdrop.Motes != null && backdrop.Motes.Count > 0) all.Add(backdrop.Motes);
            foreach (Motes m in backdrop.MoreMotes ?? System.Array.Empty<Motes>())
            {
                if (m != null && m.Count > 0) all.Add(m);
            }
            return all.ToArray();
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
            return BrightestOver(backdrop, SkyAt(backdrop, y), y);
        }

        /// <summary>
        /// The opaque <paramref name="under"/> (the sky, or a far shape laid on it) with every haze of
        /// <paramref name="backdrop"/> laid on at its densest at height <paramref name="y"/>, as the haze lays over
        /// whatever stands under it. A haze that darkens the point is laid on at the bottom of its breath instead: the
        /// black core of layer 6's lightning falls to nothing between flashes, and a shade that never breathes stays.
        /// </summary>
        public static Rgba BrightestOver(LayerBackdrop backdrop, Rgba under, float y)
        {
            foreach (Haze haze in backdrop.Hazes ?? System.Array.Empty<Haze>())
            {
                float densest = HazeAlphaAt(haze, y);
                Rgba lit = Over(haze.Color.WithAlpha(densest), under);
                if (Luminance(lit) >= Luminance(under))
                {
                    under = lit;
                    continue;
                }
                float least = haze.Flicker.Moves ? densest * (1f - haze.Flicker.Depth) : densest;
                under = Over(haze.Color.WithAlpha(least), under);
            }
            return under;
        }

        /// <summary>
        /// The brightest the backdrop gets where a far shape draws <paramref name="point"/>: the point's colour laid on
        /// the sky at its height, every haze laid over that at its densest. The contrast checks read the far view
        /// through this, so a glowing crystal may shine above the sky as long as the text and the red still read on it.
        /// </summary>
        public static Rgba BrightestWith(LayerBackdrop backdrop, SceneryVertex point)
        {
            float y = point.Y < 0f ? 0f : point.Y > 1f ? 1f : point.Y;
            return BrightestOver(backdrop, Over(point.Color, SkyAt(backdrop, y)), y);
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
        /// floor's band from top to bottom, with every haze at its densest (battle-visual-v1 §2.3), over the far
        /// view too where it reaches into the band.
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
            foreach (SceneryVertex p in BackdropScenery.Samples(backdrop))
            {
                if (p.Y < FloorTop || p.Y > FloorBottom) continue;
                lowest = System.Math.Min(lowest, Contrast(ReachRed, Over(backdrop.Tile, BrightestWith(backdrop, p))));
            }
            return lowest;
        }

        private static double Linear(float c)
        {
            return c <= 0.04045 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}
