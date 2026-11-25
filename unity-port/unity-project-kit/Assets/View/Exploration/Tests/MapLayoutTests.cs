// The exploration map's sizes by row count (#1049): every row of a 7〜9 row map fits the one screen without a node or
// its label touching another, the edges between them stay visible, and the maps of five rows or fewer keep the JS's
// sizes. Runs in Unity's EditMode runner and under `dotnet test` (unity-port/Exploration.Script.Tests).
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;
using Exploration;
using NUnit.Framework;

namespace Exploration.Tests
{
    public class MapLayoutTests
    {
        /// <summary>Shorter than this, a drawn edge reads as a dot rather than a way.</summary>
        private const float MinDrawnEdge = 24f;

        /// <summary>The least of an edge that must show outside every node face and label.</summary>
        private const float MinShownEdge = 10f;

        /// <summary>A label's width can be no wider than one full-width character per letter.</summary>
        private static readonly float LongestLabel = Enum.GetValues(typeof(NodeKind)).Cast<NodeKind>()
            .Max(k => ExplorationText.KindLabel(k).Length) * MapLayout.LabelFontSize;

        [Test]
        public void MapsOfFiveRowsOrFewerKeepTheJsSizes()
        {
            for (int rows = 1; rows <= 5; rows++)
            {
                var size = MapLayout.ForRows(rows);
                Assert.That(size.Node, Is.EqualTo(88f), $"{rows} rows");
                Assert.That(size.Scale, Is.EqualTo(1f), $"{rows} rows");
                Assert.That(size.LabelTop, Is.EqualTo(96f), $"{rows} rows");
                Assert.That(size.EdgeInset, Is.EqualTo(52f), $"{rows} rows");
                Assert.That(size.Icon, Is.EqualTo(44f), $"{rows} rows");
                Assert.That(size.WalkerUp, Is.EqualTo(50f), $"{rows} rows");
                Assert.That(size.WalkerBox, Is.EqualTo(52f), $"{rows} rows");
                Assert.That(size.WalkerGlowInset, Is.EqualTo(-24f), $"{rows} rows");
                Assert.That(size.WalkerDiscInset, Is.EqualTo(6f), $"{rows} rows");
                Assert.That(size.WalkerIcon, Is.EqualTo(24f), $"{rows} rows");
                Assert.That(size.SelW, Is.EqualTo(132f), $"{rows} rows");
                Assert.That(size.SelH, Is.EqualTo(146f), $"{rows} rows");
                Assert.That(size.SelAbove, Is.EqualTo(18f), $"{rows} rows");
                Assert.That(size.SelHook, Is.EqualTo(20f), $"{rows} rows");
            }
        }

        [TestCase(7, 64f)]
        [TestCase(8, 52f)]
        [TestCase(9, 44f)]
        public void LongerMapsDrawSmallerNodes(int rows, float node)
        {
            var size = MapLayout.ForRows(rows);
            Assert.That(size.Node, Is.EqualTo(node));
            Assert.That(size.EdgeInset, Is.EqualTo(node / 2f + 8f * node / 88f).Within(1e-4f), "the inset follows the face");
            Assert.That(size.WalkerBox, Is.EqualTo(52f * node / 88f).Within(1e-4f), "the walker follows the face");
            Assert.That(size.Icon, Is.EqualTo(44f * node / 88f).Within(1e-4f), "the icon follows the face");
            Assert.That(size.LabelBottom - size.LabelTop, Is.EqualTo(MapLayout.LabelLine), "the label keeps its line");
            Assert.That(MapLayout.LabelFontSize, Is.GreaterThanOrEqualTo(16f), "text stays 16 px or larger");
        }

        [Test]
        public void MoreRowsNeverDrawBiggerNodes()
        {
            for (int rows = 2; rows <= 12; rows++)
                Assert.That(MapLayout.ForRows(rows).Node, Is.LessThanOrEqualTo(MapLayout.ForRows(rows - 1).Node), $"{rows} rows");
        }

        // The widest rows the generator draws: the row under the entry is at most 3 wide, every other middle row of
        // layers 1〜3 at most 4. The king room is 1,4,1.
        [TestCase(new[] { 1, 4, 1 })]
        [TestCase(new[] { 1, 3, 4, 4, 1 })]
        [TestCase(new[] { 1, 3, 4, 4, 4, 4, 1 })]
        [TestCase(new[] { 1, 3, 4, 4, 4, 4, 4, 1 })]
        [TestCase(new[] { 1, 3, 4, 4, 4, 4, 4, 4, 1 })]
        public void TheWidestMapsLeaveEveryNodeAndEdgeClear(int[] widths)
        {
            var quotas = new Dictionary<NodeKind, int> { [NodeKind.Battle] = widths.Sum() - 2 };
            var spec = new LayerMapSpec(1, widths, quotas);
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var map = MapGenerator.Generate(spec, seed);
                var nodes = map.Nodes.Select(n => Place(n.Id, n.Row, n.Column, map.Row(n.Row).Count, map.RowCount)).ToList();
                var edges = map.Nodes.SelectMany(n => map.Successors(n.Id).Select(to => (n.Id, to))).ToList();
                AssertClear(MapLayout.ForRows(map.RowCount), nodes, edges, $"{string.Join(",", widths)} seed {seed}");
            }
        }

        [Test]
        public void TheLairsMapsLeaveEveryNodeAndEdgeClear()
        {
            // The real maps of layer 1 as the screen lays them out, so the builder's fractions and layout are the ones checked.
            var rowCounts = new HashSet<int>();
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var screen = new ExplorationSession(new RunStart { Seed = seed }).Screen();
                int rows = screen.Nodes.Max(n => n.Row) + 1;
                rowCounts.Add(rows);
                Assert.That(screen.Layout.Node, Is.EqualTo(MapLayout.ForRows(rows).Node), $"seed {seed}");
                var nodes = screen.Nodes.Select(n => new Spot(n.Id, MapLayout.OffsetX(n.X), MapLayout.OffsetY(n.Y))).ToList();
                var edges = screen.Edges.Select(e => (e.From, e.To)).ToList();
                AssertClear(screen.Layout, nodes, edges, $"seed {seed}");
            }
            Assert.That(rowCounts, Is.EquivalentTo(new[] { 7, 8, 9 }));
        }

        [Test]
        public void AScreenBuiltWithoutALayoutDrawsTheJsSizes()
        {
            Assert.That(new ExplorationScreen().Layout, Is.SameAs(MapLayout.Full));
            Assert.That(new ExplorationScreen { Layout = null }.Layout, Is.SameAs(MapLayout.Full));
        }

        // ------------------------------------------------------------------ geometry

        private sealed class Spot
        {
            public Spot(int id, float x, float y)
            {
                Id = id;
                X = x;
                Y = y;
            }

            public int Id { get; }

            /// <summary>The node's centre from the area's centre, stage px, y down.</summary>
            public float X { get; }

            public float Y { get; }
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

            public Box Union(Box o) =>
                new Box(Math.Min(Left, o.Left), Math.Min(Top, o.Top), Math.Max(Right, o.Right), Math.Max(Bottom, o.Bottom));
        }

        /// <summary>Where ExplorationScreenBuilder puts a node (its X by column over the row, Y by row over the rows).</summary>
        private static Spot Place(int id, int row, int column, int rowWidth, int rowCount) => new Spot(id,
            MapLayout.OffsetX((column + 0.5f) / rowWidth),
            MapLayout.OffsetY(rowCount == 1 ? 0.5f : 1f - (float)row / (rowCount - 1)));

        private static Box Face(MapLayout size, Spot n) =>
            new Box(n.X - size.Node / 2f, n.Y - size.Node / 2f, n.X + size.Node / 2f, n.Y + size.Node / 2f);

        private static Box Label(MapLayout size, Spot n)
        {
            float top = n.Y - size.Node / 2f;
            return new Box(n.X - LongestLabel / 2f, top + size.LabelTop, n.X + LongestLabel / 2f, top + size.LabelBottom);
        }

        /// <summary>The walker's disc while it stands on the node (its glow is light, not a shape, and may spill).</summary>
        private static Box WalkerDisc(MapLayout size, Spot n)
        {
            float half = size.WalkerBox / 2f - size.WalkerDiscInset;
            float top = n.Y - size.WalkerDiscTop;
            return new Box(n.X - half, top, n.X + half, top + 2f * half);
        }

        private static void AssertClear(MapLayout size, IReadOnlyList<Spot> nodes, IReadOnlyList<(int From, int To)> edges, string what)
        {
            const float eps = 1e-3f;
            var faces = nodes.Select(n => Face(size, n)).ToList();
            var labels = nodes.Select(n => Label(size, n)).ToList();
            var boxes = faces.Zip(labels, (f, l) => f.Union(l)).ToList();

            for (int i = 0; i < nodes.Count; i++)
            {
                var disc = WalkerDisc(size, nodes[i]);
                for (int j = 0; j < nodes.Count; j++)
                {
                    if (i == j) continue;
                    Assert.That(boxes[i].GapTo(boxes[j]), Is.GreaterThanOrEqualTo(MapLayout.MinClearance - eps),
                        $"{what}: node {nodes[i].Id} and its label touch node {nodes[j].Id} or its label");
                    Assert.That(disc.GapTo(boxes[j]), Is.GreaterThanOrEqualTo(MapLayout.MinClearance - eps),
                        $"{what}: the walker on node {nodes[i].Id} covers node {nodes[j].Id} or its label");
                }
            }

            var byId = nodes.ToDictionary(n => n.Id);
            foreach (var (from, to) in edges)
            {
                Spot a = byId[from], b = byId[to];
                float dx = b.X - a.X, dy = b.Y - a.Y;
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                // as ExploreScene.buildMap draws it: EDGE_INSET in from both centres
                float k = size.EdgeInset / len;
                float x1 = a.X + dx * k, y1 = a.Y + dy * k, x2 = b.X - dx * k, y2 = b.Y - dy * k;
                float drawn = len - 2f * size.EdgeInset;
                Assert.That(drawn, Is.GreaterThanOrEqualTo(MinDrawnEdge), $"{what}: edge {from}-{to} is drawn {drawn} px long");

                const int steps = 400;
                int shown = 0;
                for (int s = 0; s < steps; s++)
                {
                    float t = (s + 0.5f) / steps;
                    float x = x1 + (x2 - x1) * t, y = y1 + (y2 - y1) * t;
                    if (!faces.Any(f => f.Contains(x, y)) && !labels.Any(l => l.Contains(x, y))) shown++;
                }
                float visible = drawn * shown / steps;
                Assert.That(visible, Is.GreaterThanOrEqualTo(MinShownEdge), $"{what}: edge {from}-{to} shows {visible} px");
            }
        }
    }
}
