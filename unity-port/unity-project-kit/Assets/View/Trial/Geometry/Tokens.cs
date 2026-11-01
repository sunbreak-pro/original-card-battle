// The design system's tokens (vendor/ds/tokens.css) as typed constants, the three themes, and OCB.LAYOUT
// (vendor/ds/bundle.js:1310-1320). The game always runs the dusk theme (build.mjs: data-theme="dusk"); the
// wakakusa values are kept for completeness. `DsTokens.TryResolve` serves var(--name) to the CSS parser with the
// token's text exactly as tokens.css writes it, so a value reads the same in C# as in the stylesheet.
using System;
using System.Collections.Generic;

namespace Trial.Geometry
{
    public enum DsTheme { Dusk, WakakusaA, WakakusaB }

    public static class DsTokens
    {
        // ---- colours (tokens.css:2, dusk = :root) ---------------------------------------------------------
        public static readonly Rgba Ground = Rgba.Hex(0x10141d);
        public static readonly Rgba Panel = Rgba.Bytes(16, 20, 30, 0.88f);
        public static readonly Rgba PanelSolid = Rgba.Hex(0x141925);
        public static readonly Rgba Card = Rgba.Hex(0x171c28);
        public static readonly Rgba Ink = Rgba.Hex(0xecebf2);
        public static readonly Rgba InkMuted = Rgba.Hex(0xa9adbd);
        public static readonly Rgba Rule = Rgba.Bytes(157, 166, 186, 0.26f);
        public static readonly Rgba Steel = Rgba.Hex(0x6d7787);
        public static readonly Rgba Amber = Rgba.Hex(0xe8a84c);
        public static readonly Rgba Wick = Rgba.Hex(0xffd89a);
        public static readonly Rgba Crimson = Rgba.Hex(0xc2483f);
        public static readonly Rgba HpText = Rgba.Hex(0xe36a5e);
        public static readonly Rgba Violet = Rgba.Hex(0x8b6fd0);
        public static readonly Rgba ReachRed = Rgba.Hex(0xff1f4f);
        public static readonly Rgba Phosphor = Rgba.Hex(0xb9a3ff);
        public static readonly Rgba Guard = Rgba.Hex(0x8fc4d8);
        /// <summary>--miss: var(--steel).</summary>
        public static readonly Rgba Miss = Steel;
        public static readonly Rgba Caution = Rgba.Hex(0xe6c35c);
        public static readonly Rgba Boss = Rgba.Hex(0xd08cf0);
        public static readonly Rgba Washi = Rgba.Hex(0xf0e6d0);
        public static readonly Rgba WashiInk = Rgba.Hex(0x1d1a16);
        public static readonly Rgba FigureSelf = Rgba.Hex(0x2b2f3a);
        public static readonly Rgba FigureEnemy = Rgba.Hex(0x1b1722);
        /// <summary>Not a token: the scenes write var(--flood, #06090c) and never set --flood (field.css:80, hud.css:160/186).</summary>
        public static readonly Rgba Flood = Rgba.Hex(0x06090c);

        // ---- the per-theme colours (tokens.css:2-4) -----------------------------------------------------------
        public static readonly Rgba SkyTopDusk = Rgba.Hex(0x1d3340);
        public static readonly Rgba SkyBottomDusk = Rgba.Hex(0x182b37);
        public static readonly Rgba MeadowTopDusk = Rgba.Hex(0x182b37);
        public static readonly Rgba MeadowBottomDusk = Rgba.Hex(0x152530);
        public static readonly Rgba EarthTopDusk = Rgba.Hex(0x13212b);
        public static readonly Rgba EarthBottomDusk = Rgba.Hex(0x0f1a22);
        public static readonly Rgba FloorTileDusk = Rgba.Bytes(10, 16, 22, 0.72f);
        public static readonly Rgba SunDusk = Rgba.Bytes(255, 255, 255, 0.10f);
        public static readonly Rgba HazeDusk = Rgba.Bytes(255, 255, 255, 0.05f);
        public static readonly Rgba KeylineDusk = Rgba.Bytes(16, 20, 30, 0f);

        public static Rgba SkyTop(DsTheme t = DsTheme.Dusk) => Pick(t, SkyTopDusk, Rgba.Hex(0xf4f2df), Rgba.Hex(0xe3ebd3));
        public static Rgba SkyBottom(DsTheme t = DsTheme.Dusk) => Pick(t, SkyBottomDusk, Rgba.Hex(0xdde9cb), Rgba.Hex(0xc3d6ad));
        public static Rgba MeadowTop(DsTheme t = DsTheme.Dusk) => Pick(t, MeadowTopDusk, Rgba.Hex(0xb9d394), Rgba.Hex(0x97b878));
        public static Rgba MeadowBottom(DsTheme t = DsTheme.Dusk) => Pick(t, MeadowBottomDusk, Rgba.Hex(0x86a862), Rgba.Hex(0x64844c));
        public static Rgba EarthTop(DsTheme t = DsTheme.Dusk) => Pick(t, EarthTopDusk, Rgba.Hex(0x5f7d45), Rgba.Hex(0x4a663a));
        public static Rgba EarthBottom(DsTheme t = DsTheme.Dusk) => Pick(t, EarthBottomDusk, Rgba.Hex(0x10170d), Rgba.Hex(0x0c130b));
        public static Rgba FloorTile(DsTheme t = DsTheme.Dusk) =>
            Pick(t, FloorTileDusk, Rgba.Bytes(14, 20, 12, 0.82f), Rgba.Bytes(12, 18, 12, 0.80f));
        public static Rgba Sun(DsTheme t = DsTheme.Dusk) =>
            Pick(t, SunDusk, Rgba.Bytes(255, 255, 255, 0.55f), Rgba.Bytes(255, 255, 255, 0.35f));
        public static Rgba Haze(DsTheme t = DsTheme.Dusk) =>
            Pick(t, HazeDusk, Rgba.Bytes(255, 255, 255, 0.14f), Rgba.Bytes(255, 255, 255, 0.10f));
        /// <summary>Fully transparent in dusk: the keyline strokes exist but draw nothing (kit.md §1.2).</summary>
        public static Rgba Keyline(DsTheme t = DsTheme.Dusk) =>
            Pick(t, KeylineDusk, Rgba.Bytes(16, 20, 30, 0.85f), Rgba.Bytes(16, 20, 30, 0.85f));

        private static Rgba Pick(DsTheme t, Rgba dusk, Rgba a, Rgba b) =>
            t == DsTheme.WakakusaA ? a : t == DsTheme.WakakusaB ? b : dusk;

        // ---- sizes (tokens.css:5) ---------------------------------------------------------------------------
        public const float Space4 = 4f, Space6 = 6f, Space8 = 8f, Space12 = 12f, Space16 = 16f, Space24 = 24f, Space32 = 32f, Space48 = 48f;
        public const float RadiusPanel = 6f, RadiusPlate = 5f, RadiusTag = 4f, RadiusChipFoe = 3f, RadiusChipSelf = 15f;
        /// <summary>--radius-pip: 50% (of the box: a circle on a square).</summary>
        public const float RadiusPipPercent = 50f;
        public const float StrokeRule = 1f, StrokeFrame = 2f, StrokeIcon = 2.2f, StrokeBold = 3f, Dash = 3f, HatchGap = 11f;
        public const float OpacityUnpayable = 0.6f, OpacityHeld = 0.92f, OpacityIdleHooks = 0.75f, OpacityUnplayed = 0.55f,
            OpacityEnemyTurn = 0.4f, OpacityGuardZero = 0.7f;
        public const float ScreenW = 1920f, ScreenH = 1080f, CardW = 216f, CardH = 304f, CellH = 40f, FigureH = 226f;

        // ---- fonts (tokens.css:5; the system fallbacks after the first family do not exist in Unity) ---------
        public const string FontDisplay = "Shippori Mincho";
        public const string FontBody = "Zen Kaku Gothic New";
        public const string FontMono = "IBM Plex Mono";

        // ---- shadows (tokens.css:2, parsed: x, y, blur, spread, colour) ----------------------------------------
        public static readonly IReadOnlyList<BoxShadow> ShadowCard = Shadows(new BoxShadow(0f, 6f, 14f, 0f, Rgba.Bytes(0, 0, 0, 0.45f)));
        public static readonly IReadOnlyList<BoxShadow> ShadowLamp = Shadows(
            new BoxShadow(0f, 0f, 0f, 2f, Rgba.Hex(0xffd89a)),
            new BoxShadow(0f, 0f, 20f, 5f, Rgba.Bytes(255, 216, 154, 0.5f)));
        public static readonly IReadOnlyList<BoxShadow> ShadowThreat = Shadows(new BoxShadow(0f, 0f, 8f, 0f, Rgba.Bytes(255, 31, 79, 0.3f)));
        public static readonly IReadOnlyList<BoxShadow> ShadowReach = Shadows(new BoxShadow(0f, 0f, 22f, 0f, Rgba.Bytes(232, 168, 76, 0.5f)));
        public static readonly IReadOnlyList<BoxShadow> ShadowOmenActive = Shadows(new BoxShadow(0f, 0f, 30f, 0f, Rgba.Bytes(139, 111, 208, 0.6f)));
        public static readonly IReadOnlyList<BoxShadow> ShadowTip = Shadows(new BoxShadow(0f, 8f, 18f, 0f, Rgba.Bytes(0, 0, 0, 0.45f)));

        private static IReadOnlyList<BoxShadow> Shadows(params BoxShadow[] s) => Array.AsReadOnly(s);

        // ---- var(--name) for the CSS parser: the token text verbatim ------------------------------------------
        // tokens.css:2 (:root, [data-theme="dusk"]) and :5 (:root sizes and fonts).
        private static readonly Dictionary<string, string> RootText = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["--ground"] = "#10141d", ["--panel"] = "rgba(16,20,30,.88)", ["--panel-solid"] = "#141925", ["--card"] = "#171c28",
            ["--ink"] = "#ecebf2", ["--ink-muted"] = "#a9adbd", ["--rule"] = "rgba(157,166,186,.26)", ["--steel"] = "#6d7787",
            ["--amber"] = "#e8a84c", ["--wick"] = "#ffd89a", ["--crimson"] = "#c2483f", ["--hp-text"] = "#e36a5e",
            ["--violet"] = "#8b6fd0", ["--reach-red"] = "#ff1f4f", ["--phosphor"] = "#b9a3ff", ["--guard"] = "#8fc4d8",
            ["--miss"] = "var(--steel)", ["--caution"] = "#e6c35c", ["--boss"] = "#d08cf0", ["--washi"] = "#f0e6d0",
            ["--washi-ink"] = "#1d1a16", ["--figure-self"] = "#2b2f3a", ["--figure-enemy"] = "#1b1722",
            ["--sky-top"] = "#1d3340", ["--sky-bottom"] = "#182b37", ["--meadow-top"] = "#182b37", ["--meadow-bottom"] = "#152530",
            ["--earth-top"] = "#13212b", ["--earth-bottom"] = "#0f1a22", ["--floor-tile"] = "rgba(10,16,22,.72)",
            ["--sun"] = "rgba(255,255,255,.10)", ["--haze"] = "rgba(255,255,255,.05)", ["--keyline"] = "rgba(16,20,30,0)",
            ["--shadow-card"] = "0 6px 14px rgba(0,0,0,.45)",
            ["--shadow-lamp"] = "0 0 0 2px #ffd89a, 0 0 20px 5px rgba(255,216,154,.5)",
            ["--shadow-threat"] = "0 0 8px rgba(255,31,79,.3)",
            ["--shadow-reach"] = "0 0 22px rgba(232,168,76,.5)",
            ["--shadow-omen-active"] = "0 0 30px rgba(139,111,208,.6)",
            ["--shadow-tip"] = "0 8px 18px rgba(0,0,0,.45)",
            ["--space-4"] = "4px", ["--space-6"] = "6px", ["--space-8"] = "8px", ["--space-12"] = "12px", ["--space-16"] = "16px",
            ["--space-24"] = "24px", ["--space-32"] = "32px", ["--space-48"] = "48px",
            ["--radius-panel"] = "6px", ["--radius-plate"] = "5px", ["--radius-tag"] = "4px", ["--radius-chip-foe"] = "3px",
            ["--radius-chip-self"] = "15px", ["--radius-pip"] = "50%",
            ["--stroke-rule"] = "1px", ["--stroke-frame"] = "2px", ["--stroke-icon"] = "2.2px", ["--stroke-bold"] = "3px",
            ["--dash"] = "3px", ["--hatch-gap"] = "11px",
            ["--opacity-unpayable"] = "0.6", ["--opacity-held"] = "0.92", ["--opacity-idle-hooks"] = "0.75",
            ["--opacity-unplayed"] = "0.55", ["--opacity-enemy-turn"] = "0.4", ["--opacity-guard-zero"] = "0.7",
            ["--screen-w"] = "1920px", ["--screen-h"] = "1080px", ["--card-w"] = "216px", ["--card-h"] = "304px",
            ["--cell-h"] = "40px", ["--figure-h"] = "226px",
            ["--font-display"] = "\"Shippori Mincho\", \"Hiragino Mincho ProN\", \"Yu Mincho\", serif",
            ["--font-body"] = "\"Zen Kaku Gothic New\", \"Hiragino Sans\", \"Yu Gothic\", sans-serif",
            ["--font-mono"] = "\"IBM Plex Mono\", ui-monospace, Menlo, monospace",
        };

        // tokens.css:3
        private static readonly Dictionary<string, string> WakakusaAText = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["--sky-top"] = "#f4f2df", ["--sky-bottom"] = "#dde9cb", ["--meadow-top"] = "#b9d394", ["--meadow-bottom"] = "#86a862",
            ["--earth-top"] = "#5f7d45", ["--earth-bottom"] = "#10170d", ["--floor-tile"] = "rgba(14,20,12,.82)",
            ["--sun"] = "rgba(255,255,255,.55)", ["--haze"] = "rgba(255,255,255,.14)", ["--keyline"] = "rgba(16,20,30,.85)",
        };

        // tokens.css:4
        private static readonly Dictionary<string, string> WakakusaBText = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["--sky-top"] = "#e3ebd3", ["--sky-bottom"] = "#c3d6ad", ["--meadow-top"] = "#97b878", ["--meadow-bottom"] = "#64844c",
            ["--earth-top"] = "#4a663a", ["--earth-bottom"] = "#0c130b", ["--floor-tile"] = "rgba(12,18,12,.80)",
            ["--sun"] = "rgba(255,255,255,.35)", ["--haze"] = "rgba(255,255,255,.10)", ["--keyline"] = "rgba(16,20,30,.85)",
        };

        /// <summary>
        /// The token's CSS text as tokens.css writes it (nested var() left in place), for the theme. Accepts the
        /// name with or without the leading "--". False for a name tokens.css does not define.
        /// </summary>
        public static bool TryResolve(string name, DsTheme theme, out string css)
        {
            css = null;
            if (string.IsNullOrEmpty(name)) return false;
            string key = name.StartsWith("--", StringComparison.Ordinal) ? name : "--" + name;
            if (theme == DsTheme.WakakusaA && WakakusaAText.TryGetValue(key, out css)) return true;
            if (theme == DsTheme.WakakusaB && WakakusaBText.TryGetValue(key, out css)) return true;
            return RootText.TryGetValue(key, out css);
        }

        /// <summary>Every token name tokens.css defines (for tests and the gallery).</summary>
        public static IEnumerable<string> Names => RootText.Keys;
    }

    /// <summary>OCB.LAYOUT (bundle.js:1310-1320): the battle screen's fixed places, 1920×1080, origin top-left, px.</summary>
    // js: vendor/ds/bundle.js:1310 (var LAYOUT)
    public static class DsLayout
    {
        public const float W = 1920f, H = 1080f;
        public const float FloorX0 = 300f, FloorX1 = 1620f, FloorY = 560f, CellH = 40f, CellGap = 3f;
        public const float FootY = 570f, OmenGap = 14f, OmenW = 200f, OmenH = 98f;
        public const float PanelY = 614f, PanelSelfW = 300f, PanelFoeW = 210f, Edge = 24f;
        public const float StanceY = 470f, StanceW = 250f, StanceH = 52f, StanceGap = 6f;
        public const int StanceMax = 3;
        public const float DistY = 563f, ThrowX0 = 400f, ThrowX1 = 1520f;
        public const float RefusalBottom = 884f, BannerY = 400f;

        public static class Stamina { public const float X = 32f, Y = 846f; }
        public static class Piles { public const float X = 32f, Y = 948f, W = 64f, H = 84f, Gap = 22f; }
        public static class Attrs { public const float X = 1718f, Y = 896f; }
        public static class GuardPre { public const float X = 1560f, Y = 984f; }
        public static class EndTurn { public const float X = 1672f, Y = 976f; }
        public static class Corner { public const float X = 24f, Y = 20f; }
        public static class Journal { public const float X = 1840f, Y = 20f; }
        public static class Speed { public const float X = 1708f, Y = 20f; }
        public static class Hand { public const float Cx = 960f; }
    }
}
