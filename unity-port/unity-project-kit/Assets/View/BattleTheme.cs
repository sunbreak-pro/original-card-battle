// Visual tokens for the battle screen (battle_ui_ux_v1.md §4, skin A+ "霧と灯り").
// Every colour in the View comes from here; nothing writes a literal colour elsewhere.
#if UNITY_2021_2_OR_NEWER
using UnityEngine;

public static class BattleTheme
{
    // ---- tokens -------------------------------------------------------------
    public static readonly Color Ground = Hex("#0e1c26");
    public static readonly Color Panel = Hex("#10202a", 0.84f);
    public static readonly Color Ink = Hex("#eef3f0");
    public static readonly Color Ink2 = Hex("#a3b5b3");
    public static readonly Color Line = Hex("#bed7d2", 0.16f);
    public static readonly Color Accent = Hex("#63e0c9");
    public static readonly Color Warm = Hex("#ffc46b");
    public static readonly Color Omen = Hex("#ff5d47");
    public static readonly Color Amber = Hex("#f0a848");
    public static readonly Color Guard = Hex("#9cc7ff");
    public static readonly Color Whiff = Hex("#7d8a8c");
    public static readonly Color InkBlack = Hex("#06090c");
    public static readonly Color Paper = Hex("#f0e6d0");
    public static readonly Color PaperInk = Hex("#2a2118");
    public static readonly Color White = Color.white;

    // ---- floor palettes (1..5; 2 and 4 are interpolated) ----------------------
    public struct FloorPalette
    {
        public Color Top;
        public Color Bottom;
        public Color Mist;
        public bool Embers;
    }

    private static readonly FloorPalette Floor1 = new FloorPalette
    {
        Top = Hex("#1d3340"), Bottom = Hex("#0f1a22"), Mist = new Color(1f, 1f, 1f, 0.05f), Embers = false,
    };

    private static readonly FloorPalette Floor3 = new FloorPalette
    {
        Top = Hex("#1a1f3a"), Bottom = Hex("#0b0d1a"), Mist = Hex("#8a6fd6", 0.07f), Embers = false,
    };

    private static readonly FloorPalette Floor5 = new FloorPalette
    {
        Top = Hex("#1a0e12"), Bottom = Hex("#070507"), Mist = Hex("#7a1e1e", 0.10f), Embers = true,
    };

    public static FloorPalette Floor(int floor)
    {
        if (floor <= 1) return Floor1;
        if (floor == 2) return Lerp(Floor1, Floor3, 0.5f, false);
        if (floor == 3) return Floor3;
        if (floor == 4) return Lerp(Floor3, Floor5, 0.5f, false);
        return Floor5;
    }

    private static FloorPalette Lerp(FloorPalette a, FloorPalette b, float t, bool embers)
    {
        return new FloorPalette
        {
            Top = Color.Lerp(a.Top, b.Top, t),
            Bottom = Color.Lerp(a.Bottom, b.Bottom, t),
            Mist = Color.Lerp(a.Mist, b.Mist, t),
            Embers = embers,
        };
    }

    // ---- layout (reference 1920×1080, anchors as fractions of the canvas) ---------
    public const float RefWidth = 1920f;
    public const float RefHeight = 1080f;

    // Bands, given as (yMin, yMax) from the bottom. Spec §2.1 lists them from the top.
    public static readonly Vector2 TopBand = new Vector2(0.93f, 1.00f);
    public static readonly Vector2 Arena = new Vector2(0.42f, 0.93f);
    public static readonly Vector2 StatusBand = new Vector2(0.32f, 0.42f);
    public static readonly Vector2 StaminaBand = new Vector2(0.24f, 0.32f);
    public static readonly Vector2 HandBand = new Vector2(0.00f, 0.24f);
    public static readonly Vector2 HandX = new Vector2(0.24f, 0.76f);
    public static readonly Vector2 LeftBottomX = new Vector2(0.02f, 0.22f);
    public static readonly Vector2 RightBottomX = new Vector2(0.78f, 0.98f);
    public const float FloorFraction = 0.08f; // floor line above the arena's bottom edge

    // ---- helpers -------------------------------------------------------------
    public static Color Hex(string hex, float alpha = 1f)
    {
        Color c;
        if (!ColorUtility.TryParseHtmlString(hex, out c)) c = Color.magenta;
        c.a = alpha;
        return c;
    }

    public static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
#endif
