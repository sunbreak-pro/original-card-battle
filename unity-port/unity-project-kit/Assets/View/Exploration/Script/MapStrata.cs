// The map drawn as a cross-section of the rock the layer is cut into (#1050, battle-visual-v1 §2.5): every node a
// chamber, a hollow open onto the layer's own sky, and every link a thick tunnel through the rock. Layer 1 draws it
// first; the other layers keep the JS's squares and lines (MapLook), so the look switches by layer.
//
// The chambers stand where the plain map puts its nodes (ExplorationScreenBuilder's fractions over the area) and keep
// one screen with no scrolling (2026-11-25 Koudai-san): the design canvas draws a five-row map with 170 × 104 chambers,
// and a map of more rows draws them smaller so that each row's chamber, the walker standing on the one below and the
// tunnel between them still fit one row's height. The label stands beside its chamber instead of under it, so the
// rows give their height to the chambers. Every size, curve and colour the view draws is here; the view computes none.
using System;
using System.Collections.Generic;
using Backdrop;
using DungeonCore;

namespace Exploration
{
    /// <summary>How a layer's map is drawn.</summary>
    public enum MapLook
    {
        /// <summary>The JS's look: square nodes and straight lines (MapLayout).</summary>
        Plain,

        /// <summary>Chambers in the rock joined by tunnels (StrataLayout).</summary>
        Strata,
    }

    public static class MapLooks
    {
        /// <summary>The layers drawn as chambers and tunnels. Layer 1 first, so the feel is tried on one layer (#1050).</summary>
        public static MapLook ForLayer(int layer) => layer == 1 ? MapLook.Strata : MapLook.Plain;
    }

    /// <summary>A point in stage px (or in px from the map area's centre: the geometry does not care which).</summary>
    public readonly struct MapPoint
    {
        public MapPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
    }

    /// <summary>
    /// The chamber's corners as CSS border-radius percentages, a little different on every chamber. Each side's two
    /// radii add up to 100 %, so no corner is scaled down and the outline is a smooth, slightly lopsided oval.
    /// </summary>
    public readonly struct ChamberCorners
    {
        public ChamberCorners(int topLeftX, int bottomRightX, int topLeftY, int topRightY)
        {
            TopLeftX = topLeftX;
            BottomRightX = bottomRightX;
            TopLeftY = topLeftY;
            TopRightY = topRightY;
        }

        public int TopLeftX { get; }
        public int TopRightX => 100 - TopLeftX;
        public int BottomRightX { get; }
        public int BottomLeftX => 100 - BottomRightX;
        public int TopLeftY { get; }
        public int BottomLeftY => 100 - TopLeftY;
        public int TopRightY { get; }
        public int BottomRightY => 100 - TopRightY;

        /// <summary>"58% 42% 55% 45% / 50% 60% 40% 50%": horizontal radii TL TR BR BL, then the vertical ones.</summary>
        public string Css =>
            $"{TopLeftX}% {TopRightX}% {BottomRightX}% {BottomLeftX}% / {TopLeftY}% {TopRightY}% {BottomRightY}% {BottomLeftY}%";
    }

    /// <summary>
    /// One tunnel: a cubic curve from inside the upper chamber's floor to inside the lower chamber's ceiling. It leaves
    /// and enters straight down, and turns in the middle of the rock between the two rows, so it never runs past
    /// another chamber or a label (every chamber of a row stands on the same height).
    /// </summary>
    public sealed class Tunnel
    {
        private const int Samples = 48;
        private readonly float[] _xs = new float[Samples + 1];
        private readonly float[] _ys = new float[Samples + 1];
        private readonly float[] _along = new float[Samples + 1];

        public Tunnel(MapPoint from, MapPoint start, MapPoint c1, MapPoint c2, MapPoint end, MapPoint to)
        {
            From = from;
            Start = start;
            C1 = c1;
            C2 = c2;
            End = end;
            To = to;
            float total = 0f;
            for (int i = 0; i <= Samples; i++)
            {
                MapPoint p = CurveAt(i / (float)Samples);
                _xs[i] = p.X;
                _ys[i] = p.Y;
                if (i > 0) total += Distance(_xs[i - 1], _ys[i - 1], p.X, p.Y);
                _along[i] = total;
            }
            Length = total;
            LeadIn = Distance(from.X, from.Y, start.X, start.Y);
            LeadOut = Distance(end.X, end.Y, to.X, to.Y);
        }

        /// <summary>The upper chamber's centre.</summary>
        public MapPoint From { get; }

        /// <summary>Where the curve starts, inside the upper chamber.</summary>
        public MapPoint Start { get; }

        public MapPoint C1 { get; }
        public MapPoint C2 { get; }

        /// <summary>Where the curve ends, inside the lower chamber.</summary>
        public MapPoint End { get; }

        /// <summary>The lower chamber's centre.</summary>
        public MapPoint To { get; }

        /// <summary>The curve's length (what its draw-in and its walked trail dash over).</summary>
        public float Length { get; }

        /// <summary>From the upper chamber's centre to the curve's start.</summary>
        public float LeadIn { get; }

        /// <summary>From the curve's end to the lower chamber's centre.</summary>
        public float LeadOut { get; }

        /// <summary>The walk: centre → curve → centre.</summary>
        public float RouteLength => LeadIn + Length + LeadOut;

        /// <summary>The curve at parameter t (0 start … 1 end).</summary>
        public MapPoint CurveAt(float t)
        {
            float u = 1f - t;
            float a = u * u * u, b = 3f * u * u * t, c = 3f * u * t * t, d = t * t * t;
            return new MapPoint(a * Start.X + b * C1.X + c * C2.X + d * End.X, a * Start.Y + b * C1.Y + c * C2.Y + d * End.Y);
        }

        /// <summary>The point <paramref name="distance"/> px along the curve from its start (held to its ends).</summary>
        public MapPoint AlongCurve(float distance)
        {
            if (distance <= 0f) return Start;
            if (distance >= Length) return End;
            int i = 1;
            while (i < Samples && _along[i] < distance) i++;
            float seg = _along[i] - _along[i - 1];
            float k = seg > 0f ? (distance - _along[i - 1]) / seg : 0f;
            return new MapPoint(_xs[i - 1] + (_xs[i] - _xs[i - 1]) * k, _ys[i - 1] + (_ys[i] - _ys[i - 1]) * k);
        }

        /// <summary>
        /// The walker at share <paramref name="f"/> of the walk from the upper chamber's centre to the lower one's
        /// (<paramref name="downward"/>), or back up the same tunnel; equal steps cover equal ground.
        /// </summary>
        public MapPoint Route(float f, bool downward = true)
        {
            float d = Clamp01(downward ? f : 1f - f) * RouteLength;
            if (d <= LeadIn) return Lerp(From, Start, LeadIn > 0f ? d / LeadIn : 1f);
            d -= LeadIn;
            if (d <= Length) return AlongCurve(d);
            d -= Length;
            return Lerp(End, To, LeadOut > 0f ? d / LeadOut : 1f);
        }

        /// <summary>How much of the curve the walker has passed at share <paramref name="f"/> of the walk (0 … 1).</summary>
        public float CurveShare(float f, bool downward = true)
        {
            float d = Clamp01(f) * RouteLength - (downward ? LeadIn : LeadOut);
            return Length > 0f ? Clamp01(d / Length) : 1f;
        }

        private static MapPoint Lerp(MapPoint a, MapPoint b, float t) => new MapPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        private static float Distance(float x1, float y1, float x2, float y2)
        {
            float dx = x2 - x1, dy = y2 - y1;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        internal static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>The sizes of a map drawn as chambers and tunnels, for a map of a given row count (stage px).</summary>
    public sealed class StrataLayout
    {
        /// <summary>The design canvas's chamber at five rows (MapStrata.dc.html: 170 × 104).</summary>
        public const float FullChamberW = 170f, FullChamberH = 104f;

        /// <summary>The layer boss's chamber at five rows: larger, at the bottom (220 × 124).</summary>
        public const float FullBossW = 220f, FullBossH = 124f;

        /// <summary>Maps up to this many rows keep the canvas's sizes.</summary>
        public const int FullSizeRows = 5;

        /// <summary>
        /// The least rock left between a row's chambers and the row below, after the walker standing on the lower
        /// chamber: the walker and the tunnel's turn take it.
        /// </summary>
        public const float MinRock = 12f;

        /// <summary>The least room between any two things drawn on the map (chambers, labels, the walker).</summary>
        public const float MinClearance = 4f;

        /// <summary>The label at full size (the canvas's 18 px) and on a smaller map (16 px, never less).</summary>
        public const float FullLabelSize = 18f, SmallLabelSize = 16f;

        /// <summary>A label's line takes its size + 4.</summary>
        public const float LabelLeading = 4f;

        /// <summary>A label holds at most this many letters on a line; a longer one breaks into even lines.</summary>
        public const int LabelLetters = 4;

        /// <summary>The border of the chamber the player stands in, one they can walk to, any other.</summary>
        public const float CurrentBorder = 4f, ReachableBorder = 3f, OtherBorder = 2f;

        /// <summary>The amber dashes down a lit tunnel and the dark line under them (px, the canvas's at every size).</summary>
        public const float FlowWidth = 4f, FlowUnderWidth = 7f;

        /// <summary>The dash and gap of the lit tunnel's flow: 24 a period, the same run as the plain map's flow.</summary>
        public const float FlowDash = 2f, FlowGap = 22f;

        // The canvas's values at full size, scaled with the chamber.
        private const float BaseIcon = 50f, BaseBossIcon = 60f, BaseLabelGap = 16f, BaseTunnel = 22f, BaseWalkerLift = 6f,
            BaseWalkerBox = 52f, BaseWalkerGlowInset = -24f, BaseWalkerDiscInset = 6f, BaseWalkerIcon = 24f, BaseSelSide = 21f,
            BaseSelAbove = 20f, BaseSelBelow = 20f, BaseSelHook = 30f;

        /// <summary>How far the walker's disc rises above the chamber's top, per px of scale (6 lift + 26 half box − 6 inset).</summary>
        private const float DiscReach = BaseWalkerLift + BaseWalkerBox / 2f - BaseWalkerDiscInset;

        /// <summary>The share of a chamber's half width a tunnel may lean out from its centre, and how deep inside it starts.</summary>
        private const float TunnelLean = 0.4f, TunnelInside = 0.6f;

        public static readonly StrataLayout Full = new StrataLayout(FullChamberH, FullLabelSize);

        private StrataLayout(float chamberH, float labelSize)
        {
            Scale = chamberH / FullChamberH;
            ChamberH = chamberH;
            ChamberW = Even(FullChamberW * Scale);
            BossH = Even(FullBossH * Scale);
            BossW = Even(FullBossW * Scale);
            LabelSize = labelSize;
        }

        /// <summary>The sizes for a map of <paramref name="rowCount"/> rows.</summary>
        public static StrataLayout ForRows(int rowCount)
        {
            if (rowCount <= FullSizeRows) return Full;
            float spacing = MapLayout.AreaHeight / (rowCount - 1);
            // The tightest pair is the last row over the boss with the walker standing on the boss: half a chamber, half
            // the boss, the walker's disc over the boss and the rock left between them share one row's height.
            float scale = (spacing - MinRock) / (FullChamberH / 2f + FullBossH / 2f + DiscReach);
            float h = Math.Min(FullChamberH, 2f * (float)Math.Floor(scale * FullChamberH / 2f + 1e-4f));
            return h >= FullChamberH ? Full : new StrataLayout(h, SmallLabelSize);
        }

        /// <summary>Chamber height / 104: what the sizes below are scaled by.</summary>
        public float Scale { get; }

        public float ChamberW { get; }
        public float ChamberH { get; }
        public float BossW { get; }
        public float BossH { get; }

        public float Width(NodeKind kind) => kind == NodeKind.Boss ? BossW : ChamberW;
        public float Height(NodeKind kind) => kind == NodeKind.Boss ? BossH : ChamberH;

        /// <summary>The kind's line icon in the chamber.</summary>
        public float Icon(NodeKind kind) => (kind == NodeKind.Boss ? BaseBossIcon : BaseIcon) * Scale;

        /// <summary>The label's text size (16 px or more, under 24) and its line.</summary>
        public float LabelSize { get; }

        public float LabelLine => LabelSize + LabelLeading;

        /// <summary>The room between a chamber's right side and its label.</summary>
        public float LabelGap => BaseLabelGap * Scale;

        /// <summary>The label's lines: up to four letters on a line, a longer label broken into lines as even as can be.</summary>
        public static IReadOnlyList<string> LabelLines(string label)
        {
            label = label ?? "";
            if (label.Length <= LabelLetters) return new[] { label };
            int lines = (label.Length + LabelLetters - 1) / LabelLetters;
            int per = (label.Length + lines - 1) / lines;
            var result = new List<string>();
            for (int at = 0; at < label.Length; at += per) result.Add(label.Substring(at, Math.Min(per, label.Length - at)));
            return result;
        }

        /// <summary>The label's box: as wide as its longest line (one full-width letter a letter), its lines tall.</summary>
        public (float W, float H) LabelBox(string label)
        {
            IReadOnlyList<string> lines = LabelLines(label);
            int longest = 0;
            foreach (string l in lines) longest = Math.Max(longest, l.Length);
            return (longest * LabelSize, lines.Count * LabelLine);
        }

        /// <summary>The tunnel's width (a thick line through the rock).</summary>
        public float TunnelWidth => BaseTunnel * Scale;

        /// <summary>The walker's box, its glow and disc insets, its icon (the plain map's walker, scaled).</summary>
        public float WalkerBox => BaseWalkerBox * Scale;

        public float WalkerGlowInset => BaseWalkerGlowInset * Scale;
        public float WalkerDiscInset => BaseWalkerDiscInset * Scale;
        public float WalkerIcon => BaseWalkerIcon * Scale;

        /// <summary>The walker stands on the chamber's top, this far above the chamber's centre (on the boss, a little inside its top).</summary>
        public float WalkerUp => ChamberH / 2f + BaseWalkerLift * Scale;

        /// <summary>How far the walker's disc rises above the centre of the chamber it stands in.</summary>
        public float WalkerDiscTop => WalkerUp + WalkerBox / 2f - WalkerDiscInset;

        /// <summary>The selection frame's four hooks round the chamber (the label stands outside it).</summary>
        public float SelW(NodeKind kind) => Width(kind) + 2f * BaseSelSide * Scale;

        public float SelH(NodeKind kind) => Height(kind) + (BaseSelAbove + BaseSelBelow) * Scale;
        public float SelAbove => BaseSelAbove * Scale;
        public float SelHook => BaseSelHook * Scale;

        /// <summary>The corners of node <paramref name="nodeId"/>'s chamber: each radius 42〜58 %, the same on every visit.</summary>
        public static ChamberCorners Corners(int nodeId)
        {
            uint h = unchecked((uint)nodeId * 2654435761u + 0x9e3779b9u);
            int Next()
            {
                h ^= h >> 15;
                h = unchecked(h * 2246822519u);
                h ^= h >> 13;
                return 42 + (int)(h % 17u);
            }
            int a = Next(), b = Next(), c = Next(), d = Next();
            return new ChamberCorners(a, b, c, d);
        }

        /// <summary>
        /// The tunnel from the chamber at <paramref name="from"/> (upper row) to the one at <paramref name="to"/> (the
        /// row below). It starts and ends inside the chambers, leaning toward the other end by up to 0.4 of the half width
        /// (inside the rounded outline), so its round ends hide under the chambers drawn over it.
        /// </summary>
        public Tunnel TunnelBetween(MapPoint from, NodeKind fromKind, MapPoint to, NodeKind toKind)
        {
            float lean = (to.X - from.X) / 2f;
            float leanFrom = Hold(lean, Width(fromKind) / 2f * TunnelLean);
            float leanTo = Hold(lean, Width(toKind) / 2f * TunnelLean);
            var start = new MapPoint(from.X + leanFrom, from.Y + Height(fromKind) / 2f * TunnelInside);
            var end = new MapPoint(to.X - leanTo, to.Y - Height(toKind) / 2f * TunnelInside);
            float mid = (start.Y + end.Y) / 2f;
            return new Tunnel(from, start, new MapPoint(start.X, mid), new MapPoint(end.X, mid), end, to);
        }

        private static float Hold(float v, float limit) => v < -limit ? -limit : v > limit ? limit : v;

        private static float Even(float v) => 2f * (float)Math.Round(v / 2f);

        // ------------------------------------------------------------------ the rest of the screen around the map

        /// <summary>The band's bottom and the foot's top (the hint, the legend): the map stays between them.</summary>
        public const float StageTop = 96f, StageBottom = 976f;

        /// <summary>The detail panel's left edge (ExploreScene.js:54 DETAIL).</summary>
        public const float DetailLeft = 1304f;

        /// <summary>
        /// The depth column on the left (the canvas's box, cells and gaps): its heading, the current layer's taller cell
        /// and the others. Its words are 本文 under 24 px, so the current cell shows the layer's light as a strip beside
        /// the number instead of filling the cell with it.
        /// </summary>
        public const float DepthX = 24f, DepthY = 128f, DepthW = 124f, DepthHeadingSize = 16f, DepthHeadingLine = 20f,
            DepthCurrentH = 104f, DepthCellH = 84f, DepthGap = 6f, DepthNumberSize = 22f, DepthNoteSize = 16f, DepthLightW = 10f;

        /// <summary>The depth column's height for <paramref name="layers"/> layers.</summary>
        public static float DepthH(int layers) =>
            DepthHeadingLine + DepthGap + DepthCurrentH + (layers - 1) * (DepthGap + DepthCellH);

        /// <summary>The window over the detail panel: the chosen chamber against the layer's light.</summary>
        public const float WindowH = 150f, WindowRadius = 6f, WindowFigureH = 110f, WindowFigureGap = 40f, WindowUnknownW = 80f,
            WindowUnknownH = 120f, WindowUnknownBorder = 3f, WindowMarkSize = 40f, WindowIcon = 72f;
    }

    /// <summary>A chamber's look: its fill (top to bottom), its border, its icon's colour.</summary>
    public sealed class ChamberPaint
    {
        public ChamberPaint(string top, string bottom, string border, bool dashed, float borderWidth, string icon)
        {
            Top = top;
            Bottom = bottom;
            Border = border;
            Dashed = dashed;
            BorderWidth = borderWidth;
            Icon = icon;
        }

        /// <summary>"#rrggbb": the fill at the chamber's top and bottom (the same for a flat fill).</summary>
        public string Top { get; }
        public string Bottom { get; }

        /// <summary>The border's colour ("#rrggbb" or "rgba(…)").</summary>
        public string Border { get; }

        public bool Dashed { get; }
        public float BorderWidth { get; }

        /// <summary>The kind's icon on this fill.</summary>
        public string Icon { get; }
    }

    /// <summary>
    /// The colours of a layer drawn as chambers: the chambers' insides are the layer's sky (BackdropSpec, the same light
    /// the battle stands in), the rock the layer's veil (ExplorationBackdropSpec). Amber stays the player's: the frame of
    /// where they stand and where they can go, and the dashes down the tunnels open to them.
    /// </summary>
    public sealed class StrataPalette
    {
        /// <summary>琥珀 (§2.1).</summary>
        public const string Amber = "#e8a84c";

        /// <summary>鋼 (§2.1): the dashed frame of a far chamber.</summary>
        public const string Steel = "#6d7787";

        /// <summary>The boss's own colour (its double frame and icon on a far chamber).</summary>
        public const string Boss = "#d08cf0";

        /// <summary>地 (§2.1): the dark line under a lit tunnel's amber dashes.</summary>
        public const string Ground = "#10141d";

        /// <summary>The glow round the chamber the player stands in.</summary>
        public const string CurrentGlow = "rgba(232,168,76,.55)";

        /// <summary>The frame of a chamber walked back to (本文 at 60 %).</summary>
        public const string RevisitBorder = "rgba(236,235,242,.6)";

        /// <summary>The frame of a resolved chamber (罫 the rule colour, a hairline).</summary>
        public const string ResolvedBorder = "rgba(157,166,186,.4)";

        /// <summary>The icon on a far chamber, and the silhouette that stands against the window's light.</summary>
        public const string FarIcon = "#8d96a6";

        public const string Silhouette = "#1b1722";

        public static StrataPalette For(int layer)
        {
            LayerBackdrop place = BackdropSpec.For(layer);
            Rgba veil = ExplorationBackdropSpec.For(layer).Veil;
            Rgba rock = BackdropSpec.Over(veil, BackdropSpec.SkyAt(place, 1f));
            Rgba ground = Backdrop.Rgba.Hex(Ground);
            Rgba skyTop = BackdropSpec.SkyAt(place, 0f), skyMid = BackdropSpec.SkyAt(place, 0.48f),
                skyHigh = BackdropSpec.SkyAt(place, 0.22f), skyLow = BackdropSpec.SkyAt(place, 0.75f), skyFoot = BackdropSpec.SkyAt(place, 1f);
            // the chamber's ink: the sky's foot taken most of the way to black, so the icon keeps the layer's hue
            Rgba ink = BackdropSpec.Mix(skyFoot, Backdrop.Rgba.Hex("#000000"), 0.82f);
            return new StrataPalette
            {
                Layer = BackdropSpec.Clamp(layer),
                LitTop = Hex(skyHigh),
                LitBottom = Hex(skyLow),
                WalkedTop = Hex(BackdropSpec.Mix(skyHigh, rock, 0.22f)),
                WalkedBottom = Hex(BackdropSpec.Mix(skyLow, rock, 0.22f)),
                FarFill = Hex(BackdropSpec.Mix(skyFoot, ground, 0.72f)),
                ChamberInk = Hex(ink),
                TunnelFar = Hex(BackdropSpec.Mix(skyFoot, ground, 0.76f)),
                TunnelWalked = Hex(BackdropSpec.Mix(skyLow, ground, 0.12f)),
                TunnelLit = Hex(skyMid),
                WindowTop = Hex(skyTop),
                WindowMid = Hex(skyMid),
                WindowBottom = Hex(skyFoot),
            };
        }

        public int Layer { get; private set; }

        /// <summary>A chamber the player stands in or can walk to: the sky's light.</summary>
        public string LitTop { get; private set; }
        public string LitBottom { get; private set; }

        /// <summary>A chamber already walked (resolved, or one to walk back to): the same light, a little dimmed by the rock.</summary>
        public string WalkedTop { get; private set; }
        public string WalkedBottom { get; private set; }

        /// <summary>A far chamber: dark, its light out of reach.</summary>
        public string FarFill { get; private set; }

        /// <summary>The icon on a lit chamber.</summary>
        public string ChamberInk { get; private set; }

        /// <summary>A tunnel not yet walked, one walked, one open from where the player stands.</summary>
        public string TunnelFar { get; private set; }
        public string TunnelWalked { get; private set; }
        public string TunnelLit { get; private set; }

        /// <summary>The window's light, top / 52 % / bottom (the canvas's 0 / 52 / 100 %).</summary>
        public string WindowTop { get; private set; }
        public string WindowMid { get; private set; }
        public string WindowBottom { get; private set; }

        /// <summary>The look of a chamber that stands so for the player.</summary>
        public ChamberPaint Chamber(NodeStanding standing, NodeKind kind)
        {
            switch (standing)
            {
                case NodeStanding.Current:
                    return new ChamberPaint(LitTop, LitBottom, Amber, false, StrataLayout.CurrentBorder, ChamberInk);
                case NodeStanding.Reachable:
                    return new ChamberPaint(LitTop, LitBottom, Amber, false, StrataLayout.ReachableBorder, ChamberInk);
                case NodeStanding.Revisit:
                    // walked already, so its light is the walked one; the brighter frame says the player can go back
                    return new ChamberPaint(WalkedTop, WalkedBottom, RevisitBorder, false, StrataLayout.OtherBorder, ChamberInk);
                case NodeStanding.Resolved:
                    return new ChamberPaint(WalkedTop, WalkedBottom, ResolvedBorder, false, StrataLayout.OtherBorder, ChamberInk);
                default:
                    return new ChamberPaint(FarFill, FarFill, Steel, true, StrataLayout.OtherBorder, kind == NodeKind.Boss ? Boss : FarIcon);
            }
        }

        /// <summary>本文 (§2.1): the labels beside the chambers, on the veiled backdrop.</summary>
        public const string Ink = "#ecebf2";

        /// <summary>The layer's sky at height <paramref name="y"/> (0 top, 1 bottom), as "#rrggbb".</summary>
        public static string SkyHex(int layer, float y) => Hex(BackdropSpec.SkyAt(BackdropSpec.For(layer), y));

        /// <summary>The WCAG contrast of two opaque "#rrggbb" colours.</summary>
        public static double Contrast(string a, string b) => BackdropSpec.Contrast(Backdrop.Rgba.Hex(a), Backdrop.Rgba.Hex(b));

        /// <summary>The relative luminance of an opaque "#rrggbb" colour.</summary>
        public static double Luminance(string hex) => BackdropSpec.Luminance(Backdrop.Rgba.Hex(hex));

        /// <summary>The lowest contrast <paramref name="hex"/> gets anywhere on the layer's veiled backdrop (ExplorationBackdropSpec).</summary>
        public static double VeilContrast(int layer, string hex) =>
            ExplorationBackdropSpec.LowestContrast(ExplorationBackdropSpec.For(layer), Backdrop.Rgba.Hex(hex));

        /// <summary>"#rrggbb" of an opaque colour.</summary>
        public static string Hex(Rgba c)
        {
            int r = Byte(c.R), g = Byte(c.G), b = Byte(c.B);
            return "#" + r.ToString("x2") + g.ToString("x2") + b.ToString("x2");
        }

        private static int Byte(float v) => (int)Math.Round(Tunnel.Clamp01(v) * 255f);
    }

    /// <summary>One layer in the depth column on the map's left.</summary>
    public enum DepthMark
    {
        /// <summary>A layer above this one.</summary>
        Passed,

        /// <summary>The layer the player is on.</summary>
        Current,

        /// <summary>A layer below, not yet reached.</summary>
        Below,
    }

    public sealed class DepthCell
    {
        public DepthCell(int layer, DepthMark mark, string note, bool king)
        {
            Layer = layer;
            Mark = mark;
            Note = note ?? "";
            King = king;
        }

        public int Layer { get; }
        public DepthMark Mark { get; }

        /// <summary>The words under the number ("" for none).</summary>
        public string Note { get; }

        /// <summary>The king room (its cell takes the boss's double frame).</summary>
        public bool King { get; }

        public string Number => Layer.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The calendar as one bar: what is left, the share the next new node takes (dashed), a tick every ten days.</summary>
    public sealed class CalendarBar
    {
        /// <summary>The bar's box in the band.</summary>
        public const float Width = 400f, Height = 20f, TickWidth = 2f;

        /// <summary>A tick every this many days.</summary>
        public const int TickDays = 10;

        public CalendarBar(int daysLeft, int nextDaysLeft, bool frozen, int totalDays = Calendar.TotalDays)
        {
            int total = Math.Max(1, totalDays);
            int left = Math.Max(0, Math.Min(total, daysLeft));
            int next = frozen ? left : Math.Max(0, Math.Min(left, nextDaysLeft));
            Left = left / (float)total;
            Next = next / (float)total;
            Frozen = frozen;
            var ticks = new List<float>();
            for (int d = TickDays; d < total; d += TickDays) ticks.Add(d / (float)total);
            Ticks = ticks;
        }

        /// <summary>The share of the bar still filled: the days left over the whole calendar.</summary>
        public float Left { get; }

        /// <summary>The share left after the next new node; Next … Left is drawn dashed (none while the calendar stands still).</summary>
        public float Next { get; }

        public bool Frozen { get; }

        /// <summary>The ticks' places along the bar (0 … 1).</summary>
        public IReadOnlyList<float> Ticks { get; }
    }

    /// <summary>The body in the band drawn as the battle draws it: a heart and a bar for HP, a pip per point of stamina.</summary>
    public static class BodyMarks
    {
        /// <summary>The HP bar's height and its least width.</summary>
        public const float HpBarH = 12f, HpBarMinW = 40f;

        /// <summary>A stamina pip and the room between two, at most; a long row shrinks them to its width.</summary>
        public const float PipSize = 12f, PipGap = 4f;

        /// <summary>The share of the HP bar filled.</summary>
        public static float HpFill(int hp, int maxHp) => maxHp <= 0 ? 0f : Tunnel.Clamp01(hp / (float)maxHp);

        /// <summary>
        /// The pip's size and pitch that fit <paramref name="count"/> pips into <paramref name="width"/> px: 12 px pips 16
        /// apart while they fit, all shrunk together (the pip three quarters of its pitch) when they do not.
        /// </summary>
        public static (float Size, float Pitch) Pips(int count, float width)
        {
            const float pitch0 = PipSize + PipGap, share = PipSize / pitch0;
            if (count <= 0) return (PipSize, pitch0);
            float pitch = Math.Min(pitch0, width / (count - 1f + share));
            return (pitch * share, pitch);
        }
    }

    /// <summary>What stands in the detail panel's window: an enemy whose name the journal knows, or one it does not.</summary>
    public enum WindowFigure
    {
        Known,
        Unknown,
    }
}
