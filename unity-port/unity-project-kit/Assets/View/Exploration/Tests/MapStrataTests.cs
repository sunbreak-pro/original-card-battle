// The map drawn as chambers and tunnels (#1050): layer 1 only, the canvas's 170 × 104 chambers on a map of five rows or
// fewer and smaller ones on the 7〜9 row maps of #1049, so that no chamber, label or walker touches another, no tunnel
// runs over a chamber or a label, and the whole map stays on one screen between the band, the depth column, the detail
// panel and the foot. Runs in Unity's EditMode runner and under `dotnet test` (unity-port/Exploration.Script.Tests).
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;
using Exploration;
using NUnit.Framework;

namespace Exploration.Tests
{
    public class MapStrataTests
    {
        /// <summary>The least of a tunnel that must show between its two chambers.</summary>
        private const float MinShownTunnel = 10f;

        // ------------------------------------------------------------------ which layers

        [Test]
        public void OnlyLayerOneDrawsChambers()
        {
            Assert.That(MapLooks.ForLayer(1), Is.EqualTo(MapLook.Strata));
            for (int layer = 2; layer <= Lair.LayerCount; layer++)
                Assert.That(MapLooks.ForLayer(layer), Is.EqualTo(MapLook.Plain), $"layer {layer}");
        }

        [Test]
        public void ALayerOneScreenCarriesTheChambersSizesAndColours()
        {
            var screen = new ExplorationSession(new RunStart { Seed = 7 }).Screen();
            int rows = screen.Nodes.Max(n => n.Row) + 1;
            Assert.That(screen.Look, Is.EqualTo(MapLook.Strata));
            Assert.That(screen.Strata.ChamberH, Is.EqualTo(StrataLayout.ForRows(rows).ChamberH));
            Assert.That(screen.Palette.Layer, Is.EqualTo(1));
            // the plain sizes stay on the screen for the layers that draw them
            Assert.That(screen.Layout.Node, Is.EqualTo(MapLayout.ForRows(rows).Node));
        }

        [Test]
        public void AScreenBuiltWithoutALookDrawsThePlainMap()
        {
            var screen = new ExplorationScreen();
            Assert.That(screen.Look, Is.EqualTo(MapLook.Plain));
            Assert.That(screen.Strata, Is.Null);
            Assert.That(screen.Depths, Is.Empty);
        }

        // ------------------------------------------------------------------ sizes by row count

        [Test]
        public void MapsOfFiveRowsOrFewerKeepTheCanvasSizes()
        {
            for (int rows = 1; rows <= 5; rows++)
            {
                var s = StrataLayout.ForRows(rows);
                Assert.That(s, Is.SameAs(StrataLayout.Full), $"{rows} rows");
                Assert.That((s.ChamberW, s.ChamberH), Is.EqualTo((170f, 104f)), $"{rows} rows");
                Assert.That((s.BossW, s.BossH), Is.EqualTo((220f, 124f)), $"{rows} rows");
                Assert.That(s.TunnelWidth, Is.EqualTo(22f), $"{rows} rows");
                Assert.That(s.LabelSize, Is.EqualTo(18f), $"{rows} rows");
                Assert.That(s.Icon(NodeKind.Battle), Is.EqualTo(50f), $"{rows} rows");
            }
        }

        [TestCase(7, 120f, 74f, 156f, 88f)]
        [TestCase(8, 102f, 62f, 132f, 74f)]
        [TestCase(9, 88f, 54f, 114f, 64f)]
        public void LongerMapsDrawSmallerChambers(int rows, float w, float h, float bossW, float bossH)
        {
            var s = StrataLayout.ForRows(rows);
            Assert.That((s.ChamberW, s.ChamberH), Is.EqualTo((w, h)));
            Assert.That((s.BossW, s.BossH), Is.EqualTo((bossW, bossH)));
            Assert.That(s.TunnelWidth, Is.EqualTo(22f * h / 104f).Within(1e-4f), "the tunnel follows the chamber");
            Assert.That(s.LabelSize, Is.EqualTo(16f), "the label never drops below 16 px");
        }

        [Test]
        public void TextStaysBetweenSixteenAndTwentyFourPx()
        {
            for (int rows = 1; rows <= 12; rows++)
            {
                float size = StrataLayout.ForRows(rows).LabelSize;
                Assert.That(size, Is.GreaterThanOrEqualTo(16f).And.LessThan(24f), $"{rows} rows");
            }
            Assert.That(StrataLayout.DepthNumberSize, Is.GreaterThanOrEqualTo(16f).And.LessThan(24f));
            Assert.That(StrataLayout.DepthNoteSize, Is.GreaterThanOrEqualTo(16f).And.LessThan(24f));
            Assert.That(StrataLayout.DepthHeadingSize, Is.GreaterThanOrEqualTo(16f).And.LessThan(24f));
        }

        [Test]
        public void MoreRowsNeverDrawBiggerChambers()
        {
            for (int rows = 2; rows <= 12; rows++)
                Assert.That(StrataLayout.ForRows(rows).ChamberH, Is.LessThanOrEqualTo(StrataLayout.ForRows(rows - 1).ChamberH), $"{rows} rows");
        }

        [Test]
        public void ALongLabelBreaksIntoEvenLines()
        {
            Assert.That(StrataLayout.LabelLines("通常戦闘"), Is.EqualTo(new[] { "通常戦闘" }));
            Assert.That(StrataLayout.LabelLines("死亡地点の痕跡"), Is.EqualTo(new[] { "死亡地点", "の痕跡" }));
            Assert.That(StrataLayout.LabelLines("秘跡の彫り"), Is.EqualTo(new[] { "秘跡の", "彫り" }));
            var box = StrataLayout.Full.LabelBox("死亡地点の痕跡");
            Assert.That(box, Is.EqualTo((4 * 18f, 2 * 22f)));
        }

        // ------------------------------------------------------------------ the chambers' outlines

        [Test]
        public void EveryChamberIsRoundedALittleDifferently()
        {
            var seen = new HashSet<string>();
            for (int id = 0; id < 60; id++)
            {
                ChamberCorners c = StrataLayout.Corners(id);
                foreach (int r in new[] { c.TopLeftX, c.TopRightX, c.BottomRightX, c.BottomLeftX, c.TopLeftY, c.TopRightY, c.BottomRightY, c.BottomLeftY })
                    Assert.That(r, Is.InRange(42, 58), $"node {id}");
                Assert.That(c.TopLeftX + c.TopRightX, Is.EqualTo(100));
                Assert.That(c.TopRightY + c.BottomRightY, Is.EqualTo(100));
                Assert.That(StrataLayout.Corners(id).Css, Is.EqualTo(c.Css), "the same node keeps its outline");
                seen.Add(c.Css);
            }
            Assert.That(seen.Count, Is.GreaterThan(50), "neighbouring chambers differ");
        }

        // ------------------------------------------------------------------ the tunnels

        [Test]
        public void ATunnelLeavesAndEntersStraightDownAndTurnsInTheRock()
        {
            var s = StrataLayout.ForRows(8);
            var from = new MapPoint(100f, 0f);
            var to = new MapPoint(400f, 97f);
            Tunnel t = s.TunnelBetween(from, NodeKind.Battle, to, NodeKind.Battle);
            Assert.That(t.Start.Y, Is.GreaterThan(from.Y).And.LessThan(from.Y + s.ChamberH / 2f), "starts inside the upper chamber");
            Assert.That(t.End.Y, Is.LessThan(to.Y).And.GreaterThan(to.Y - s.ChamberH / 2f), "ends inside the lower chamber");
            Assert.That(t.C1.X, Is.EqualTo(t.Start.X), "leaves straight down");
            Assert.That(t.C2.X, Is.EqualTo(t.End.X), "enters straight down");
            float y = float.MinValue;
            for (int i = 0; i <= 100; i++)
            {
                float at = t.CurveAt(i / 100f).Y;
                Assert.That(at, Is.GreaterThanOrEqualTo(y - 1e-4f), "never climbs back");
                y = at;
            }
            Assert.That(Math.Abs(t.Start.X - from.X), Is.LessThanOrEqualTo(s.ChamberW / 2f * 0.4f + 1e-4f), "inside the outline");
        }

        [Test]
        public void TheWalkFollowsTheTunnelBothWays()
        {
            var s = StrataLayout.Full;
            Tunnel t = s.TunnelBetween(new MapPoint(0f, 0f), NodeKind.Battle, new MapPoint(265f, 170f), NodeKind.Rest);
            Assert.That(t.Route(0f).X, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(t.Route(1f).Y, Is.EqualTo(170f).Within(1e-3f));
            Assert.That(t.Route(0f, downward: false).Y, Is.EqualTo(170f).Within(1e-3f), "back up starts below");
            Assert.That(t.Route(1f, downward: false).Y, Is.EqualTo(0f).Within(1e-3f));
            MapPoint mid = t.Route(0.5f);
            MapPoint curveMid = t.AlongCurve(t.Length / 2f + (t.LeadOut - t.LeadIn) / 2f);
            Assert.That(mid.X, Is.EqualTo(curveMid.X).Within(0.5f), "equal steps cover equal ground");
            Assert.That(t.CurveShare(0f), Is.EqualTo(0f));
            Assert.That(t.CurveShare(1f), Is.EqualTo(1f));
            Assert.That(t.Length, Is.GreaterThan(170f - s.ChamberH * 0.6f));
        }

        // The widest rows the generator draws on layers 1〜3 (#1049): the row under the entry at most 3 wide, every other
        // middle row at most 4. Five rows is the canvas's own map.
        [TestCase(new[] { 1, 3, 4, 4, 1 })]
        [TestCase(new[] { 1, 3, 4, 4, 4, 4, 1 })]
        [TestCase(new[] { 1, 3, 4, 4, 4, 4, 4, 1 })]
        [TestCase(new[] { 1, 3, 4, 4, 4, 4, 4, 4, 1 })]
        public void TheWidestMapsLeaveEveryChamberAndTunnelClear(int[] widths)
        {
            var quotas = new Dictionary<NodeKind, int> { [NodeKind.Battle] = widths.Sum() - 2 };
            var spec = new LayerMapSpec(1, widths, quotas);
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var map = MapGenerator.Generate(spec, seed);
                var rooms = map.Nodes.Select(n => Room.Make(n.Id, n.Kind, ExplorationText.KindLabel(n.Kind),
                    (n.Column + 0.5f) / map.Row(n.Row).Count, map.RowCount == 1 ? 0.5f : 1f - (float)n.Row / (map.RowCount - 1))).ToList();
                var links = map.Nodes.SelectMany(n => map.Successors(n.Id).Select(to => (n.Id, to))).ToList();
                AssertClear(StrataLayout.ForRows(map.RowCount), rooms, links, $"{string.Join(",", widths)} seed {seed}");
            }
        }

        [Test]
        public void TheLayerOneMapsLeaveEveryChamberAndTunnelClear()
        {
            // the real maps of layer 1 as the screen lays them out, so the builder's fractions and sizes are the ones checked
            var rowCounts = new HashSet<int>();
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var screen = new ExplorationSession(new RunStart { Seed = seed }).Screen();
                rowCounts.Add(screen.Nodes.Max(n => n.Row) + 1);
                var rooms = screen.Nodes.Select(n => Room.Make(n.Id, n.Kind, n.KindLabel, n.X, n.Y)).ToList();
                var links = screen.Edges.Select(e => (e.From, e.To)).ToList();
                AssertClear(screen.Strata, rooms, links, $"seed {seed}");
            }
            Assert.That(rowCounts, Is.EquivalentTo(new[] { 7, 8, 9 }));
        }

        // ------------------------------------------------------------------ colours

        [Test]
        public void TheChambersOpenOntoTheLayersSky()
        {
            var palette = StrataPalette.For(1);
            Assert.That(palette.LitTop, Is.EqualTo(StrataPalette.SkyHex(1, 0.22f)));
            Assert.That(palette.LitBottom, Is.EqualTo(StrataPalette.SkyHex(1, 0.75f)));
            Assert.That(palette.WindowTop, Is.EqualTo(StrataPalette.SkyHex(1, 0f)));
            Assert.That(palette.WindowBottom, Is.EqualTo(StrataPalette.SkyHex(1, 1f)));
            // the canvas's layer-1 light (若草と陽)
            Assert.That((palette.LitTop, palette.LitBottom), Is.EqualTo(("#ebf8ac", "#96ce56")));
        }

        [Test]
        public void TheMarksReadOnTheirChambersAndTheVeil()
        {
            var palette = StrataPalette.For(1);
            foreach (NodeStanding standing in Enum.GetValues(typeof(NodeStanding)))
            {
                foreach (NodeKind kind in new[] { NodeKind.Battle, NodeKind.Boss })
                {
                    ChamberPaint p = palette.Chamber(standing, kind);
                    double floor = standing == NodeStanding.Far ? 3.0 : 4.5;
                    Assert.That(Contrast(p.Icon, p.Top), Is.GreaterThanOrEqualTo(floor), $"{standing} {kind} icon on the top");
                    Assert.That(Contrast(p.Icon, p.Bottom), Is.GreaterThanOrEqualTo(floor), $"{standing} {kind} icon on the bottom");
                }
            }
            // amber is the player's only: the frames of where they stand and can go, and the lit tunnel's dashes
            Assert.That(palette.Chamber(NodeStanding.Current, NodeKind.Battle).Border, Is.EqualTo(StrataPalette.Amber));
            Assert.That(palette.Chamber(NodeStanding.Reachable, NodeKind.Battle).Border, Is.EqualTo(StrataPalette.Amber));
            foreach (NodeStanding other in new[] { NodeStanding.Revisit, NodeStanding.Resolved, NodeStanding.Far })
                Assert.That(palette.Chamber(other, NodeKind.Battle).Border, Is.Not.EqualTo(StrataPalette.Amber), other.ToString());
            Assert.That(Contrast(StrataPalette.Amber, StrataPalette.Ground), Is.GreaterThanOrEqualTo(3.0), "the dashes over their dark line");

            // the labels and the frames stand on layer 1's veiled backdrop (ExplorationBackdropSpec's rule)
            Assert.That(StrataPalette.VeilContrast(1, StrataPalette.Ink), Is.GreaterThanOrEqualTo(4.5));
            Assert.That(StrataPalette.VeilContrast(1, StrataPalette.Amber), Is.GreaterThanOrEqualTo(3.0));
        }

        [Test]
        public void TheTunnelsStepFromDarkToLit()
        {
            var palette = StrataPalette.For(1);
            double far = StrataPalette.Luminance(palette.TunnelFar);
            double walked = StrataPalette.Luminance(palette.TunnelWalked);
            double lit = StrataPalette.Luminance(palette.TunnelLit);
            Assert.That(far, Is.LessThan(walked));
            Assert.That(walked, Is.LessThan(lit));
        }

        // ------------------------------------------------------------------ the band, the depth column, the window

        [Test]
        public void TheCalendarBarShowsWhatIsLeftAndTheNextStep()
        {
            var bar = new CalendarBar(87, 86, frozen: false, totalDays: 100);
            Assert.That(bar.Left, Is.EqualTo(0.87f).Within(1e-5f));
            Assert.That(bar.Next, Is.EqualTo(0.86f).Within(1e-5f));
            Assert.That(bar.Ticks.Count, Is.EqualTo(9));
            for (int i = 0; i < 9; i++) Assert.That(bar.Ticks[i], Is.EqualTo((i + 1) / 10f).Within(1e-5f), $"tick {i + 1}");

            var still = new CalendarBar(41, 40, frozen: true, totalDays: 100);
            Assert.That(still.Next, Is.EqualTo(still.Left), "no day passes while a king is down");

            var held = new CalendarBar(140, -3, frozen: false, totalDays: 100);
            Assert.That((held.Left, held.Next), Is.EqualTo((1f, 0f)));
        }

        [Test]
        public void TheBodyMarksFitTheBand()
        {
            Assert.That(BodyMarks.HpFill(38, 50), Is.EqualTo(0.76f).Within(1e-5f));
            Assert.That(BodyMarks.HpFill(5, 0), Is.EqualTo(0f));
            Assert.That(BodyMarks.HpFill(60, 50), Is.EqualTo(1f));
            Assert.That(BodyMarks.Pips(10, 200f), Is.EqualTo((12f, 16f)), "ten pips keep their size");
            for (int count = 1; count <= 30; count++)
            {
                var (size, pitch) = BodyMarks.Pips(count, 160f);
                Assert.That(pitch * (count - 1) + size, Is.LessThanOrEqualTo(160f + 1e-3f), $"{count} pips");
                Assert.That(size, Is.LessThan(pitch), $"{count} pips stay apart");
            }
        }

        [Test]
        public void TheDepthColumnMarksTheLayerAndNamesTheKingRoom()
        {
            var screen = new ExplorationSession(new RunStart { Seed = 3 }).Screen();
            Assert.That(screen.Depths.Select(d => d.Layer), Is.EqualTo(Enumerable.Range(1, Lair.LayerCount)));
            Assert.That(screen.Depths[0].Mark, Is.EqualTo(DepthMark.Current));
            Assert.That(screen.Depths[0].Note, Is.EqualTo(ExplorationText.DepthHere));
            Assert.That(screen.Depths.Skip(1).All(d => d.Mark == DepthMark.Below), Is.True);
            Assert.That(screen.Depths[Lair.LayerCount - 1].King, Is.True);
            Assert.That(screen.Depths[Lair.LayerCount - 1].Note, Is.EqualTo(ExplorationText.KingRoom));
            Assert.That(StrataLayout.DepthY + StrataLayout.DepthH(Lair.LayerCount), Is.LessThanOrEqualTo(StrataLayout.StageBottom));
        }

        [Test]
        public void TheWindowShowsWhoWaitsInACombatChamber()
        {
            // a session with no journal knows no enemy: every combat chamber shows one 「？」, the others no one
            var screen = new ExplorationSession(new RunStart { Seed = 11 }).Screen();
            foreach (NodePanel n in screen.Nodes)
            {
                if (n.Kind.IsCombat()) Assert.That(n.Figures, Is.EqualTo(new[] { WindowFigure.Unknown }), $"node {n.Id}");
                else Assert.That(n.Figures, Is.Empty, $"node {n.Id}");
            }
            Assert.That(screen.HpValueLabel, Is.EqualTo($"{screen.Hp} / {screen.MaxHp}"));
            Assert.That(screen.StaminaValueLabel, Is.EqualTo($"{screen.Stamina} / {screen.MaxStamina}"));
        }

        // ------------------------------------------------------------------ geometry

        /// <summary>A chamber: its id and kind, its label, and its centre in stage px.</summary>
        private sealed class Room
        {
            public int Id;
            public NodeKind Kind;
            public string Label;
            public MapPoint At;

            public static Room Make(int id, NodeKind kind, string label, float x, float y) => new Room
            {
                Id = id,
                Kind = kind,
                Label = label,
                At = new MapPoint(MapLayout.AreaCentreX + MapLayout.OffsetX(x), MapLayout.AreaCentreY + MapLayout.OffsetY(y)),
            };
        }

        private readonly struct Box
        {
            public Box(float left, float top, float right, float bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public float Left { get; }
            public float Top { get; }
            public float Right { get; }
            public float Bottom { get; }

            public bool Contains(float x, float y) => x > Left && x < Right && y > Top && y < Bottom;

            /// <summary>The room between two boxes: negative when they overlap.</summary>
            public float GapTo(Box o) => Math.Max(Math.Max(o.Left - Right, Left - o.Right), Math.Max(o.Top - Bottom, Top - o.Bottom));

            /// <summary>How far a point is from the box (0 inside).</summary>
            public float DistanceTo(float x, float y)
            {
                float dx = Math.Max(0f, Math.Max(Left - x, x - Right));
                float dy = Math.Max(0f, Math.Max(Top - y, y - Bottom));
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }

        private static Box Chamber(StrataLayout s, Room r)
        {
            float w = s.Width(r.Kind) / 2f, h = s.Height(r.Kind) / 2f;
            return new Box(r.At.X - w, r.At.Y - h, r.At.X + w, r.At.Y + h);
        }

        /// <summary>The label beside the chamber's right side, its lines centred on the chamber's middle.</summary>
        private static Box Label(StrataLayout s, Room r)
        {
            var (w, h) = s.LabelBox(r.Label);
            float left = r.At.X + s.Width(r.Kind) / 2f + s.LabelGap;
            return new Box(left, r.At.Y - h / 2f, left + w, r.At.Y + h / 2f);
        }

        /// <summary>The walker's disc while it stands on the chamber (its glow is light, not a shape, and may spill).</summary>
        private static Box WalkerDisc(StrataLayout s, Room r)
        {
            float half = s.WalkerBox / 2f - s.WalkerDiscInset;
            float top = r.At.Y - s.WalkerDiscTop;
            return new Box(r.At.X - half, top, r.At.X + half, top + 2f * half);
        }

        private static void AssertClear(StrataLayout s, IReadOnlyList<Room> rooms, IReadOnlyList<(int From, int To)> links, string what)
        {
            const float eps = 1e-3f;
            var chambers = rooms.Select(r => Chamber(s, r)).ToList();
            var labels = rooms.Select(r => Label(s, r)).ToList();

            for (int i = 0; i < rooms.Count; i++)
            {
                // the whole map on one screen: under the band, over the foot, right of the depth column, left of the detail panel
                var disc = WalkerDisc(s, rooms[i]);
                Assert.That(disc.Top, Is.GreaterThanOrEqualTo(StrataLayout.StageTop), $"{what}: the walker on {rooms[i].Id} runs under the band");
                Assert.That(chambers[i].Bottom, Is.LessThanOrEqualTo(StrataLayout.StageBottom), $"{what}: chamber {rooms[i].Id} runs into the foot");
                Assert.That(chambers[i].Left, Is.GreaterThanOrEqualTo(StrataLayout.DepthX + StrataLayout.DepthW + StrataLayout.MinClearance),
                    $"{what}: chamber {rooms[i].Id} runs into the depth column");
                Assert.That(labels[i].Right, Is.LessThanOrEqualTo(StrataLayout.DetailLeft - StrataLayout.MinClearance),
                    $"{what}: the label of {rooms[i].Id} runs into the detail panel");
                Assert.That(chambers[i].GapTo(labels[i]), Is.GreaterThan(0f), $"{what}: chamber {rooms[i].Id} covers its own label");

                for (int j = 0; j < rooms.Count; j++)
                {
                    if (i == j) continue;
                    string pair = $"{what}: {rooms[i].Id} and {rooms[j].Id}";
                    Assert.That(chambers[i].GapTo(chambers[j]), Is.GreaterThanOrEqualTo(StrataLayout.MinClearance - eps), pair + " (chambers)");
                    Assert.That(chambers[i].GapTo(labels[j]), Is.GreaterThanOrEqualTo(StrataLayout.MinClearance - eps), pair + " (chamber, label)");
                    Assert.That(labels[i].GapTo(labels[j]), Is.GreaterThanOrEqualTo(StrataLayout.MinClearance - eps), pair + " (labels)");
                    Assert.That(disc.GapTo(chambers[j]), Is.GreaterThanOrEqualTo(StrataLayout.MinClearance - eps), pair + " (walker, chamber)");
                    Assert.That(disc.GapTo(labels[j]), Is.GreaterThanOrEqualTo(StrataLayout.MinClearance - eps), pair + " (walker, label)");
                }
            }

            var index = rooms.Select((r, i) => (r.Id, i)).ToDictionary(p => p.Id, p => p.i);
            float half = s.TunnelWidth / 2f;
            foreach (var (from, to) in links)
            {
                int a = index[from], b = index[to];
                Tunnel t = s.TunnelBetween(rooms[a].At, rooms[a].Kind, rooms[b].At, rooms[b].Kind);
                const int steps = 400;
                int shown = 0;
                for (int k = 0; k <= steps; k++)
                {
                    MapPoint p = t.CurveAt(k / (float)steps);
                    for (int j = 0; j < rooms.Count; j++)
                    {
                        Assert.That(labels[j].DistanceTo(p.X, p.Y), Is.GreaterThanOrEqualTo(half - eps),
                            $"{what}: tunnel {from}-{to} runs over the label of {rooms[j].Id}");
                        if (j == a || j == b) continue;
                        Assert.That(chambers[j].DistanceTo(p.X, p.Y), Is.GreaterThanOrEqualTo(half - eps),
                            $"{what}: tunnel {from}-{to} runs over chamber {rooms[j].Id}");
                    }
                    if (!chambers[a].Contains(p.X, p.Y) && !chambers[b].Contains(p.X, p.Y)) shown++;
                }
                float visible = t.Length * shown / (steps + 1);
                Assert.That(visible, Is.GreaterThanOrEqualTo(MinShownTunnel), $"{what}: tunnel {from}-{to} shows {visible} px");
            }
        }

        private static double Contrast(string a, string b) => StrataPalette.Contrast(a, b);
    }
}
