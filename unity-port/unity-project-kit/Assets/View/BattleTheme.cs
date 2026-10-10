// Visual tokens for the battle screen: battle-visual-v1.md §2 (案 1a, #242). Every colour in the View
// comes from here; nothing writes a literal colour elsewhere. The names from the A+ skin (Ground,
// Ink, Omen, Warm, Accent...) are kept so every screen built on them follows the new palette; each
// one now holds the 1a colour of the role it played (Omen is the violet of the enemy and its omen,
// Warm and Accent the amber of the player's side).
#if UNITY_2021_2_OR_NEWER
using UnityEngine;

public static class BattleTheme
{
    // ---- §2.1 tokens --------------------------------------------------------------------
    /// <summary>地: the outer frame and the result screens. The battle backdrop is the layer's (§2.4).</summary>
    public static readonly Color Ground = Hex("#10141d");
    /// <summary>パネル (88%): number panels, the corner info, the stance frames. No blur.</summary>
    public static readonly Color Panel = Hex("#10141e", 0.88f);
    /// <summary>パネル（不透明）: the omen badge, tooltips, the detail, the prediction tag.</summary>
    public static readonly Color PanelOpaque = Hex("#141925");
    /// <summary>カードの地: the card face. No pattern on it.</summary>
    public static readonly Color CardGround = Hex("#171c28");
    /// <summary>本文: every text under 24 px is written in it.</summary>
    public static readonly Color Ink = Hex("#ecebf2");
    /// <summary>補足: labels and notes, never a number.</summary>
    public static readonly Color Ink2 = Hex("#a9adbd");
    /// <summary>罫線: panel frames and table rules.</summary>
    public static readonly Color Line = Hex("#9da6ba", 0.26f);
    /// <summary>鈍鋼: lines only, never text.</summary>
    public static readonly Color Steel = Hex("#6d7787");
    /// <summary>琥珀: the player's side — stamina, the cost circle, the lit reach, the throw line, the end-turn frame, the skill attribute.</summary>
    public static readonly Color Amber = Hex("#e8a84c");
    /// <summary>灯芯: the held card's prediction and a lit lamp only.</summary>
    public static readonly Color Wick = Hex("#ffd89a");
    /// <summary>緋: the HP bar and the stance attribute's frame. Never text.</summary>
    public static readonly Color Scarlet = Hex("#c2483f");
    /// <summary>HP の文字: HP numbers of 24 px and over, and heals.</summary>
    public static readonly Color HpInk = Hex("#e36a5e");
    /// <summary>菫: the enemy's light, the omen badge's frame, the attack attribute's frame. Never text.</summary>
    public static readonly Color Violet = Hex("#8b6fd0");
    /// <summary>赤: the cells an enemy's action reaches, and their swatches only. Never text or a bar (§2.1, #350).</summary>
    public static readonly Color AimRed = Hex("#ff1f4f");
    /// <summary>燐光: power numbers (omen and card alike), the miasma icon.</summary>
    public static readonly Color Phosphor = Hex("#b9a3ff");
    /// <summary>Guard: the Guard number and icon, always with the shield shape.</summary>
    public static readonly Color Guard = Hex("#8fc4d8");
    /// <summary>外れ: does not land or reach; always with a dashed line or a strike.</summary>
    public static readonly Color Whiff = Hex("#6d7787");
    /// <summary>注意: worse than expected; always with △.</summary>
    public static readonly Color Caution = Hex("#e6c35c");
    /// <summary>ボス: the bosses' own statuses and adaptations; always with a double frame.</summary>
    public static readonly Color Boss = Hex("#d08cf0");
    /// <summary>和紙: the journal, the glossary, the rest and the result screens.</summary>
    public static readonly Color Paper = Hex("#f0e6d0");
    public static readonly Color PaperInk = Hex("#1d1a16");
    public static readonly Color InkBlack = Hex("#06090c");
    public static readonly Color White = Color.white;

    // ---- the A+ names, now holding the 1a colour of their role ----------------------------
    /// <summary>The enemy and its omen (A+ red → 1a violet).</summary>
    public static readonly Color Omen = Violet;
    /// <summary>The player's warm accent (A+ gold → 1a amber).</summary>
    public static readonly Color Warm = Amber;
    /// <summary>The player's own colour (A+ teal → 1a amber: warm colours belong to the player's side, §2.1).</summary>
    public static readonly Color Accent = Amber;

    // ---- §2.2 attribute colours ------------------------------------------------------------

    /// <summary>§2.2: the frame and band of a card of this attribute.</summary>
    public static Color KindFrame(Depiction.CardKind kind)
    {
        switch (kind)
        {
            case Depiction.CardKind.Attack: return Violet;
            case Depiction.CardKind.Guard: return Guard;
            case Depiction.CardKind.Skill: return Amber;
            case Depiction.CardKind.Stance: return Scarlet;
            default: return Steel;
        }
    }

    /// <summary>§2.2: the icon and value colour of a card of this attribute.</summary>
    public static Color KindInk(Depiction.CardKind kind)
    {
        switch (kind)
        {
            case Depiction.CardKind.Attack: return Phosphor;
            case Depiction.CardKind.Guard: return Guard;
            case Depiction.CardKind.Skill: return Amber;
            case Depiction.CardKind.Stance: return HpInk;
            default: return Ink;
        }
    }

    // ---- §2.4 the layer's backdrop ------------------------------------------------------------
    public struct FloorPalette
    {
        public Color Top;
        public Color Bottom;
        public Color Mist;
        /// <summary>床のタイル: the fill of a floor cell (§4.2).</summary>
        public Color Tile;
        public bool Embers;
    }

    private static readonly FloorPalette Floor1 = new FloorPalette
    {
        Top = Hex("#1d3340"), Bottom = Hex("#0f1a22"), Mist = new Color(1f, 1f, 1f, 0.10f), Tile = Hex("#0a1016", 0.72f), Embers = false,
    };

    // Layers 2, 3, 6 and 7 are #89's to settle (§2.4); 3 keeps the A+ value until then.
    private static readonly FloorPalette Floor3 = new FloorPalette
    {
        Top = Hex("#1a1f3a"), Bottom = Hex("#0b0d1a"), Mist = Hex("#8a6fd6", 0.07f), Tile = Hex("#0c0e1c", 0.78f), Embers = false,
    };

    /// <summary>The brightest layer: #e4e6f2 → #b7bbd6 → #7d82a8 (the middle stop is folded into the gradient's ends).</summary>
    private static readonly FloorPalette Floor4 = new FloorPalette
    {
        Top = Hex("#e4e6f2"), Bottom = Hex("#7d82a8"), Mist = new Color(1f, 1f, 1f, 0.35f), Tile = Hex("#0e101c", 0.82f), Embers = false,
    };

    private static readonly FloorPalette Floor5 = new FloorPalette
    {
        Top = Hex("#1a0e12"), Bottom = Hex("#070507"), Mist = Hex("#8c1e28", 0.28f), Tile = Hex("#0c080a", 0.80f), Embers = true,
    };

    public static FloorPalette Floor(int floor)
    {
        if (floor <= 1) return Floor1;
        if (floor == 2) return Lerp(Floor1, Floor3, 0.5f, false);
        if (floor == 3) return Floor3;
        if (floor == 4) return Floor4;
        return Floor5;
    }

    private static FloorPalette Lerp(FloorPalette a, FloorPalette b, float t, bool embers)
    {
        return new FloorPalette
        {
            Top = Color.Lerp(a.Top, b.Top, t),
            Bottom = Color.Lerp(a.Bottom, b.Bottom, t),
            Mist = Color.Lerp(a.Mist, b.Mist, t),
            Tile = Color.Lerp(a.Tile, b.Tile, t),
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
