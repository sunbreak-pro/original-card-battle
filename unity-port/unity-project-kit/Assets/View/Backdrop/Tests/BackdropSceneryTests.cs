// #89 (2026-11-03): the far view each layer draws in code until its painted art lands (BackdropScenery), and the
// further sets of specks (Motes.Seed / Still, BackdropSpec.Specks). The same recipe always draws the same shapes, every
// mesh is well formed and stays on the stage, every outer edge fades out over one pixel, and the contrast checks of
// BackdropSpecTests and ExplorationBackdropTests read the far view too.
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Backdrop.Tests
{
    public class BackdropSceneryTests
    {
        private static readonly Rgba Dark = new Rgba(0.10f, 0.20f, 0.30f, 0.9f);
        private static readonly Rgba Mist = new Rgba(0.50f, 0.45f, 0.40f, 0.6f);
        private static readonly Rgba Glow = new Rgba(0.95f, 0.90f, 0.70f, 0.8f);

        private static readonly SceneryKind[] Kinds = (SceneryKind[])Enum.GetValues(typeof(SceneryKind));

        [Test]
        public void TheSameScenery_AlwaysDrawsTheSameMesh()
        {
            foreach (SceneryKind kind in Kinds)
            {
                SceneryMesh first = BackdropScenery.Mesh(Sample(kind, 7));
                SceneryMesh again = BackdropScenery.Mesh(Sample(kind, 7));
                Assert.That(again.Triangles, Is.EqualTo(first.Triangles), kind.ToString());
                Assert.That(again.Vertices.Length, Is.EqualTo(first.Vertices.Length), kind.ToString());
                for (int i = 0; i < first.Vertices.Length; i++)
                {
                    Assert.That(again.Vertices[i].X, Is.EqualTo(first.Vertices[i].X), kind + " x " + i);
                    Assert.That(again.Vertices[i].Y, Is.EqualTo(first.Vertices[i].Y), kind + " y " + i);
                    Assert.That(Same(again.Vertices[i].Color, first.Vertices[i].Color), Is.True, kind + " colour " + i);
                }
            }
        }

        [Test]
        public void ADifferentSeed_DrawsADifferentGroup()
        {
            foreach (SceneryKind kind in Kinds)
            {
                SceneryMesh one = BackdropScenery.Mesh(Sample(kind, 1));
                SceneryMesh two = BackdropScenery.Mesh(Sample(kind, 2));
                bool differs = one.Vertices.Length != two.Vertices.Length;
                for (int i = 0; !differs && i < one.Vertices.Length; i++)
                {
                    differs = one.Vertices[i].X != two.Vertices[i].X || one.Vertices[i].Y != two.Vertices[i].Y;
                }
                Assert.That(differs, Is.True, kind.ToString());
            }
        }

        [Test]
        public void EveryKind_StandsTriangles()
        {
            foreach (SceneryKind kind in Kinds)
            {
                for (int count = 1; count <= 4; count++)
                {
                    Scenery s = Sample(kind, 3);
                    s.Count = count;
                    Assert.That(BackdropScenery.Mesh(s).Triangles, Is.Not.Empty, kind + " × " + count);
                }
            }
        }

        [TestCase(0)]
        [TestCase(-2)]
        public void ACountOfNone_DrawsNothing(int count)
        {
            foreach (SceneryKind kind in Kinds)
            {
                Scenery s = Sample(kind, 3);
                s.Count = count;
                SceneryMesh mesh = BackdropScenery.Mesh(s);
                Assert.That(mesh.Vertices, Is.Empty, kind.ToString());
                Assert.That(mesh.Triangles, Is.Empty, kind.ToString());
            }
            Assert.That(BackdropScenery.Mesh(null).Triangles, Is.Empty);
        }

        [Test]
        public void EveryMesh_IsWellFormedAndStaysOnTheStage()
        {
            // A sweep over the knobs, the extremes included, so no recipe a layer may write folds into slivers or runs off.
            float[] tops = { -0.15f, 0f, 0.25f };
            float[] varies = { 0f, 0.5f, 1f };
            float[] roughs = { 0f, 0.6f, 1f };
            float[][] bands = { new[] { 0f, 1f }, new[] { 0.1f, 0.45f }, new[] { 0.55f, 0.95f } };
            int checkedMeshes = 0;
            foreach (SceneryKind kind in Kinds)
            {
                foreach (float top in tops)
                {
                    foreach (float vary in varies)
                    {
                        foreach (float rough in roughs)
                        {
                            foreach (float[] band in bands)
                            {
                                for (int seed = 0; seed < 4; seed++)
                                {
                                    var s = new Scenery
                                    {
                                        Kind = kind, Color = Dark, Foot = Mist, Light = Glow, Count = 1 + seed * 2, Seed = seed,
                                        X0 = band[0], X1 = band[1], Top = top, Base = 0.52f, Size = 0.02f + 0.02f * seed,
                                        Vary = vary, Rough = rough, Lean = seed * 6f, Hang = seed % 2 == 1,
                                    };
                                    AssertWellFormed(s, BackdropScenery.Mesh(s));
                                    checkedMeshes++;
                                }
                            }
                        }
                    }
                }
            }
            Assert.That(checkedMeshes, Is.GreaterThan(0));
        }

        [Test]
        public void TheColour_RunsFromColorAtTheTopToFootAtTheBase()
        {
            // A flat band (Vary 0) stands from Top to Base exactly, so both ends are drawn.
            var band = new Scenery { Kind = SceneryKind.Ridge, Color = Dark, Foot = Mist, Light = Glow, Count = 3, Top = 0.3f, Base = 0.5f, Vary = 0f };
            SceneryMesh mesh = BackdropScenery.Mesh(band);
            bool sawTop = false, sawBase = false;
            foreach (SceneryVertex v in Corners(mesh))
            {
                if (Math.Abs(v.Y - band.Top) < 1e-5f)
                {
                    sawTop = true;
                    Assert.That(Same(v.Color, Dark), Is.True, "Color at Top");
                }
                if (Math.Abs(v.Y - band.Base) < 1e-5f)
                {
                    sawBase = true;
                    Assert.That(Same(v.Color, Mist), Is.True, "Foot at Base");
                }
            }
            Assert.That(sawTop, Is.True);
            Assert.That(sawBase, Is.True);
        }

        [Test]
        public void EveryCorner_TakesItsColourFromItsHeight()
        {
            foreach (SceneryKind kind in Kinds)
            {
                Scenery s = Sample(kind, 5);
                foreach (SceneryVertex v in Corners(BackdropScenery.Mesh(s)))
                {
                    float t = Clamp01((v.Y - s.Top) / (s.Base - s.Top));
                    bool shaded = Same(v.Color, BackdropSpec.Mix(s.Color, s.Foot, t));
                    bool lit = Same(v.Color, BackdropSpec.Mix(s.Light, s.Foot, t));
                    Assert.That(shaded || lit, Is.True, kind + " at y " + v.Y);
                }
            }
        }

        [Test]
        public void TheLitKinds_UseTheLight_AndTheRestDoNot()
        {
            var lit = new HashSet<SceneryKind> { SceneryKind.Pillars, SceneryKind.Crystals, SceneryKind.Coil, SceneryKind.Shards };
            foreach (SceneryKind kind in Kinds)
            {
                Scenery s = Sample(kind, 5);
                bool any = false;
                foreach (SceneryVertex v in Corners(BackdropScenery.Mesh(s)))
                {
                    float t = Clamp01((v.Y - s.Top) / (s.Base - s.Top));
                    if (!Same(v.Color, BackdropSpec.Mix(s.Color, s.Foot, t))) any = true;
                }
                Assert.That(any, Is.EqualTo(lit.Contains(kind)), kind.ToString());
            }
        }

        [Test]
        public void ARidge_SpansItsBand()
        {
            var inner = new Scenery { Kind = SceneryKind.Ridge, Color = Dark, Foot = Mist, Count = 4, Seed = 2, X0 = 0.2f, X1 = 0.7f, Top = 0.3f, Base = 0.52f, Vary = 0.6f, Rough = 0.5f };
            Bounds(BackdropScenery.Mesh(inner), out float left, out float right, out _, out _);
            Assert.That(left, Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(right, Is.EqualTo(0.7f).Within(1e-4f));

            // A band that starts at the stage's edge runs just past it, so no sliver of sky shows at the side.
            var whole = new Scenery { Kind = SceneryKind.Ridge, Color = Dark, Foot = Mist, Count = 4, Seed = 2, X0 = 0f, X1 = 1f, Top = 0.3f, Base = 0.52f, Vary = 0.6f };
            Bounds(BackdropScenery.Mesh(whole), out left, out right, out _, out _);
            Assert.That(left, Is.EqualTo(-0.01f).Within(1e-4f));
            Assert.That(right, Is.EqualTo(1.01f).Within(1e-4f));
        }

        [Test]
        public void ARidge_RisesNoHigherThanTop_AndStandsOnBase()
        {
            var s = new Scenery { Kind = SceneryKind.Ridge, Color = Dark, Foot = Mist, Count = 5, Seed = 4, Top = 0.32f, Base = 0.52f, Vary = 0.8f, Rough = 1f };
            Bounds(BackdropScenery.Mesh(s), out _, out _, out float high, out float low);
            Assert.That(high, Is.GreaterThanOrEqualTo(s.Top - 1e-5f));
            Assert.That(low, Is.EqualTo(s.Base).Within(1e-5f));
        }

        [Test]
        public void AHangingRidge_ReachesTop()
        {
            var roof = new Scenery { Kind = SceneryKind.Ridge, Color = Dark, Foot = Mist, Count = 6, Seed = 9, Top = -0.02f, Base = 0.2f, Vary = 0.7f, Rough = 0.4f, Hang = true };
            Bounds(BackdropScenery.Mesh(roof), out _, out _, out float high, out float low);
            Assert.That(high, Is.EqualTo(roof.Top).Within(1e-5f));
            Assert.That(low, Is.LessThanOrEqualTo(roof.Base + 1e-5f));
        }

        [Test]
        public void ALeftWall_StaysLeftOfX1_AndARightWallRightOfX0()
        {
            var left = new Scenery { Kind = SceneryKind.Wall, Color = Dark, Foot = Mist, Count = 4, Seed = 3, X0 = 0.05f, X1 = 0.2f, Top = 0.1f, Base = 0.6f, Vary = 0.9f, Rough = 0.8f };
            Bounds(BackdropScenery.Mesh(left), out float l0, out float l1, out _, out _);
            Assert.That(l1, Is.LessThanOrEqualTo(left.X1 + 1e-5f));
            Assert.That(l0, Is.EqualTo(-0.01f).Within(1e-4f));

            var right = new Scenery { Kind = SceneryKind.Wall, Color = Dark, Foot = Mist, Count = 4, Seed = 3, X0 = 0.8f, X1 = 0.95f, Top = 0.1f, Base = 0.6f, Vary = 0.9f, Rough = 0.8f };
            Bounds(BackdropScenery.Mesh(right), out float r0, out float r1, out _, out _);
            Assert.That(r0, Is.GreaterThanOrEqualTo(right.X0 - 1e-5f));
            Assert.That(r1, Is.EqualTo(1.01f).Within(1e-4f));
        }

        [Test]
        public void BrokenPillars_StandLowerThanWholeOnes()
        {
            var whole = new Scenery { Kind = SceneryKind.Pillars, Color = Dark, Foot = Mist, Light = Glow, Count = 5, Seed = 6, Top = 0.15f, Base = 0.52f, Size = 0.03f, Vary = 0.4f, Rough = 0f };
            var broken = new Scenery { Kind = SceneryKind.Pillars, Color = Dark, Foot = Mist, Light = Glow, Count = 5, Seed = 6, Top = 0.15f, Base = 0.52f, Size = 0.03f, Vary = 0.4f, Rough = 1f };
            Bounds(BackdropScenery.Mesh(whole), out _, out _, out float wholeTop, out _);
            Bounds(BackdropScenery.Mesh(broken), out _, out _, out float brokenTop, out _);
            Assert.That(brokenTop, Is.GreaterThan(wholeTop));
        }

        [Test]
        public void Meshes_KeepsTheOrderOfTheGroups()
        {
            Scenery ridge = Sample(SceneryKind.Ridge, 1), trunks = Sample(SceneryKind.Trunks, 2), shards = Sample(SceneryKind.Shards, 3);
            var backdrop = new LayerBackdrop { Scenery = new[] { ridge, trunks, null, shards } };
            SceneryMesh[] meshes = BackdropScenery.Meshes(backdrop);
            Assert.That(meshes, Has.Length.EqualTo(4));
            AssertSameMesh(meshes[0], BackdropScenery.Mesh(ridge));
            AssertSameMesh(meshes[1], BackdropScenery.Mesh(trunks));
            Assert.That(meshes[2].Triangles, Is.Empty);
            AssertSameMesh(meshes[3], BackdropScenery.Mesh(shards));
            Assert.That(BackdropScenery.Meshes(new LayerBackdrop()), Is.Empty);
            Assert.That(BackdropScenery.Meshes(null), Is.Empty);
        }

        [Test]
        public void Specks_ListsEverySetInDrawingOrder_LeavingOutTheEmptyOnes()
        {
            var first = new Motes { Count = 5 };
            var pollen = new Motes { Count = 3, Seed = 1 };
            var stars = new Motes { Count = 9, Seed = 2, Still = true };
            var backdrop = new LayerBackdrop { Motes = first, MoreMotes = new[] { pollen, null, new Motes { Count = 0 }, stars } };
            Assert.That(BackdropSpec.Specks(backdrop), Is.EqualTo(new[] { first, pollen, stars }));

            var noFirst = new LayerBackdrop { Motes = Motes.None, MoreMotes = new[] { stars } };
            Assert.That(BackdropSpec.Specks(noFirst), Is.EqualTo(new[] { stars }));
            Assert.That(BackdropSpec.Specks(new LayerBackdrop()), Is.Empty);
            Assert.That(BackdropSpec.Specks(null), Is.Empty);
        }

        [Test]
        public void ASpeckOfSeed0_StandsWhereItAlwaysDid()
        {
            // The formula before there were several sets, written out again: seed 0 keeps every layer's specks in place.
            var motes = new Motes { Count = 12, SpeedX = 6f, SpeedY = -9f, Twinkle = 0.6f };
            for (int i = 0; i < motes.Count; i++)
            {
                for (int ms = 0; ms <= 9000; ms += 1500)
                {
                    double seconds = ms / 1000.0;
                    double sway = Math.Sin(seconds * 0.6 + BackdropMotion.Hash(i, 3) * 6.283) * 12.0;
                    float oldX = (float)Wrap(BackdropMotion.Hash(i, 1) * 1920f + motes.SpeedX * seconds + sway, 1920f);
                    float oldY = (float)Wrap(BackdropMotion.Hash(i, 2) * 1080f + motes.SpeedY * seconds, 1080f);
                    double beat = 1.5 + BackdropMotion.Hash(i, 4) * 2.5;
                    float oldAlpha = 1f - motes.Twinkle * (float)BackdropMotion.CosineDip(ms / 1000.0 / beat + BackdropMotion.Hash(i, 5));

                    BackdropMotion.MoteAt(motes, i, ms, 1920f, 1080f, out float x, out float y);
                    Assert.That(x, Is.EqualTo(oldX), "x of " + i + " at " + ms);
                    Assert.That(y, Is.EqualTo(oldY), "y of " + i + " at " + ms);
                    Assert.That(BackdropMotion.MoteAlpha(motes, i, ms), Is.EqualTo(oldAlpha), "alpha of " + i + " at " + ms);
                }
            }
        }

        [Test]
        public void AStillSpeck_KeepsItsPlace_AndStillTwinkles()
        {
            var stars = new Motes { Count = 20, Seed = 3, Still = true, Twinkle = 0.8f };
            bool twinkled = false;
            for (int i = 0; i < stars.Count; i++)
            {
                BackdropMotion.MoteAt(stars, i, 0, 1920f, 1080f, out float x0, out float y0);
                for (int ms = 700; ms <= 60000; ms += 3700)
                {
                    BackdropMotion.MoteAt(stars, i, ms, 1920f, 1080f, out float x, out float y);
                    Assert.That(x, Is.EqualTo(x0), "star " + i + " at " + ms);
                    Assert.That(y, Is.EqualTo(y0), "star " + i + " at " + ms);
                    if (Math.Abs(BackdropMotion.MoteAlpha(stars, i, ms) - BackdropMotion.MoteAlpha(stars, i, 0)) > 0.05f) twinkled = true;
                }
            }
            Assert.That(twinkled, Is.True);
        }

        [Test]
        public void DifferentSeeds_PutTheSpecksInDifferentPlaces()
        {
            var a = new Motes { Count = 16, Seed = 0 };
            var b = new Motes { Count = 16, Seed = 1 };
            var c = new Motes { Count = 16, Seed = 2 };
            int same = 0;
            for (int i = 0; i < a.Count; i++)
            {
                BackdropMotion.MoteAt(a, i, 0, 1920f, 1080f, out float ax, out float ay);
                BackdropMotion.MoteAt(b, i, 0, 1920f, 1080f, out float bx, out float by);
                BackdropMotion.MoteAt(c, i, 0, 1920f, 1080f, out float cx, out float cy);
                if (Math.Abs(ax - bx) < 1f && Math.Abs(ay - by) < 1f) same++;
                if (Math.Abs(bx - cx) < 1f && Math.Abs(by - cy) < 1f) same++;
            }
            Assert.That(same, Is.EqualTo(0));
        }

        [Test]
        public void BrightestOverTheSky_IsBrightestAt()
        {
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++)
            {
                LayerBackdrop backdrop = BackdropSpec.For(layer);
                for (int step = 0; step <= 20; step++)
                {
                    float y = step / 20f;
                    Assert.That(Same(BackdropSpec.BrightestOver(backdrop, BackdropSpec.SkyAt(backdrop, y), y), BackdropSpec.BrightestAt(backdrop, y)),
                        Is.True, "layer " + layer + " at " + y);
                }
            }
        }

        [Test]
        public void EveryOuterEdge_FadesOutOverOnePixel()
        {
            // uGUI draws no anti-aliasing, so the generator rings every shape with a band that runs to alpha 0.
            foreach (SceneryKind kind in Kinds)
            {
                SceneryMesh mesh = BackdropScenery.Mesh(Sample(kind, 4));
                Assert.That(mesh.Fringe, Has.Length.EqualTo(mesh.Vertices.Length), kind.ToString());
                Assert.That(mesh.Fringe, Has.Some.True, kind.ToString());
                Assert.That(mesh.Fringe, Has.Some.False, kind.ToString());
                for (int i = 0; i < mesh.Vertices.Length; i++)
                {
                    if (!mesh.Fringe[i]) continue;
                    SceneryVertex f = mesh.Vertices[i];
                    Assert.That(f.Color.A, Is.EqualTo(0f), kind + " fringe " + i);
                    double nearest = double.MaxValue;
                    foreach (SceneryVertex c in Corners(mesh))
                    {
                        double dx = (f.X - c.X) * 1920.0, dy = (f.Y - c.Y) * 1080.0;
                        nearest = Math.Min(nearest, Math.Sqrt(dx * dx + dy * dy));
                    }
                    Assert.That(nearest, Is.LessThanOrEqualTo(1.01), kind + " fringe " + i + " stands off its edge");
                }
            }
        }

        [Test]
        public void TheContrastChecks_ReadTheFarView()
        {
            // A pale band across the dark layer 7, from the top of the screen into the floor's band: the checks that keep
            // the red and the text readable must see it, or a far shape could hide under them.
            LayerBackdrop seven = BackdropSpec.For(7);
            LayerBackdrop pale = Copy(seven);
            pale.Scenery = new[]
            {
                new Scenery { Kind = SceneryKind.Ridge, Color = new Rgba(0.92f, 0.9f, 0.86f, 0.9f), Foot = new Rgba(0.92f, 0.9f, 0.86f, 0.9f), Count = 3, X0 = 0f, X1 = 1f, Top = 0.05f, Base = 0.6f, Vary = 0f },
            };
            Assert.That(BackdropSpec.ReachRedContrast(pale), Is.LessThan(BackdropSpec.ReachRedContrast(seven) - 0.5));
            var veil = ExplorationBackdropSpec.For(7).Veil;
            var bare = new ExplorationBackdrop { Layer = 7, Place = seven, Veil = veil };
            var lit = new ExplorationBackdrop { Layer = 7, Place = pale, Veil = veil };
            Assert.That(ExplorationBackdropSpec.LowestContrast(lit, ExplorationBackdropSpec.Ink),
                Is.LessThan(ExplorationBackdropSpec.LowestContrast(bare, ExplorationBackdropSpec.Ink) - 1.0));
            Assert.That(ExplorationBackdropSpec.PeakLuminance(lit), Is.GreaterThan(ExplorationBackdropSpec.PeakLuminance(bare)));
        }

        [Test]
        public void AThickCoil_NeverFoldsOverItself()
        {
            // Many humps and a thick body: the inner edge would cross itself at the crests if the half-width passed the
            // centreline's radius of curvature. No body triangle may then sit on top of another.
            var s = new Scenery { Kind = SceneryKind.Coil, Color = Dark, Foot = Dark, Light = Glow, Count = 8, Seed = 2, X0 = 0f, X1 = 1f, Top = 0.15f, Base = 0.5f, Size = 0.08f, Vary = 0f };
            SceneryMesh mesh = BackdropScenery.Mesh(s);
            var body = new List<SceneryVertex[]>();
            for (int t = 0; t < mesh.Triangles.Length; t += 3)
            {
                SceneryVertex a = mesh.Vertices[mesh.Triangles[t]], b = mesh.Vertices[mesh.Triangles[t + 1]], c = mesh.Vertices[mesh.Triangles[t + 2]];
                if (Same(a.Color, Dark) && Same(b.Color, Dark) && Same(c.Color, Dark)) body.Add(new[] { a, b, c });
            }
            Assert.That(body.Count, Is.GreaterThan(100));
            int stacked = 0;
            foreach (SceneryVertex[] one in body)
            {
                float cx = (one[0].X + one[1].X + one[2].X) / 3f, cy = (one[0].Y + one[1].Y + one[2].Y) / 3f;
                foreach (SceneryVertex[] other in body)
                {
                    if (ReferenceEquals(one, other)) continue;
                    if (StrictlyInside(other, cx, cy)) stacked++;
                }
            }
            Assert.That(stacked, Is.EqualTo(0));
        }

        [Test]
        public void ThePillarsLitStrip_GetsNoFringeOfItsOwn()
        {
            // The strip lies on its shaft: its edges are seams, not outer edges, so no faint line runs round it.
            var s = new Scenery { Kind = SceneryKind.Pillars, Color = Dark, Foot = Dark, Light = Glow, Count = 3, Seed = 1, Top = 0.15f, Base = 0.52f, Size = 0.04f, Vary = 0.3f };
            SceneryMesh mesh = BackdropScenery.Mesh(s);
            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                if (!mesh.Fringe[i]) continue;
                Rgba c = mesh.Vertices[i].Color;
                Assert.That(Same(new Rgba(c.R, c.G, c.B, 1f), new Rgba(Dark.R, Dark.G, Dark.B, 1f)), Is.True, "fringe " + i + " carries the light");
            }
        }

        [Test]
        public void TheChecks_SeeFarShapesThroughNearerOnes()
        {
            // Two translucent pale groups on the same places: the stage shows them stacked, and the checks must too.
            var pale = new Rgba(0.9f, 0.88f, 0.8f, 0.5f);
            Scenery Group() => new Scenery { Kind = SceneryKind.Crystals, Color = pale, Foot = pale, Light = pale, Count = 5, Seed = 3, X0 = 0.1f, X1 = 0.9f, Top = 0.1f, Base = 0.5f, Size = 0.05f, Vary = 0.2f };
            LayerBackdrop one = Copy(BackdropSpec.For(7));
            one.Scenery = new[] { Group() };
            LayerBackdrop two = Copy(BackdropSpec.For(7));
            two.Scenery = new[] { Group(), Group() };
            var veil = ExplorationBackdropSpec.For(7).Veil;
            double peakOne = ExplorationBackdropSpec.PeakLuminance(new ExplorationBackdrop { Layer = 7, Place = one, Veil = veil });
            double peakTwo = ExplorationBackdropSpec.PeakLuminance(new ExplorationBackdrop { Layer = 7, Place = two, Veil = veil });
            Assert.That(peakTwo, Is.GreaterThan(peakOne * 1.15));
        }

        private static bool StrictlyInside(SceneryVertex[] t, float x, float y)
        {
            double ax = t[0].X * 1920.0, ay = t[0].Y * 1080.0, bx = t[1].X * 1920.0, by = t[1].Y * 1080.0, cx = t[2].X * 1920.0, cy = t[2].Y * 1080.0;
            double px = x * 1920.0, py = y * 1080.0;
            double d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
            if (Math.Abs(d) < 1e-9) return false;
            double la = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / d, lb = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / d, lc = 1 - la - lb;
            return la > 1e-6 && lb > 1e-6 && lc > 1e-6;
        }

        private static LayerBackdrop Copy(LayerBackdrop b)
        {
            return new LayerBackdrop { Layer = b.Layer, Name = b.Name, Sky = b.Sky, Hazes = b.Hazes, Motes = b.Motes, MoreMotes = b.MoreMotes, Scenery = b.Scenery, Tile = b.Tile };
        }

        /// <summary>The shapes' own corners, without the fringe's outer ones.</summary>
        private static IEnumerable<SceneryVertex> Corners(SceneryMesh mesh)
        {
            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                if (i >= mesh.Fringe.Length || !mesh.Fringe[i]) yield return mesh.Vertices[i];
            }
        }

        private static Scenery Sample(SceneryKind kind, int seed)
        {
            return new Scenery
            {
                Kind = kind, Color = Dark, Foot = Mist, Light = Glow, Count = 4, Seed = seed,
                X0 = 0.05f, X1 = 0.95f, Top = 0.18f, Base = 0.515f, Size = 0.03f, Vary = 0.6f, Rough = 0.5f, Lean = 8f,
            };
        }

        private static void AssertWellFormed(Scenery s, SceneryMesh mesh)
        {
            string at = s.Kind + " seed " + s.Seed + " top " + s.Top + " vary " + s.Vary + " rough " + s.Rough + " band " + s.X0 + ".." + s.X1;
            Assert.That(mesh.Triangles.Length % 3, Is.EqualTo(0), at);
            float yMin = Math.Min(s.Top, 0f) - 0.05f;
            foreach (SceneryVertex v in mesh.Vertices)
            {
                Assert.That(float.IsNaN(v.X) || float.IsInfinity(v.X) || float.IsNaN(v.Y) || float.IsInfinity(v.Y), Is.False, at);
                Assert.That(v.X, Is.InRange(-0.05f, 1.05f), at);
                Assert.That(v.Y, Is.InRange(yMin, 1.05f), at);
            }
            for (int t = 0; t < mesh.Triangles.Length; t += 3)
            {
                int a = mesh.Triangles[t], b = mesh.Triangles[t + 1], c = mesh.Triangles[t + 2];
                Assert.That(a, Is.InRange(0, mesh.Vertices.Length - 1), at);
                Assert.That(b, Is.InRange(0, mesh.Vertices.Length - 1), at);
                Assert.That(c, Is.InRange(0, mesh.Vertices.Length - 1), at);
                SceneryVertex va = mesh.Vertices[a], vb = mesh.Vertices[b], vc = mesh.Vertices[c];
                // In the stage's pixels; every triangle turns the same way (clockwise on the stage, y down).
                double cross = ((double)(vb.X - va.X) * (vc.Y - va.Y) - (double)(vc.X - va.X) * (vb.Y - va.Y)) * 1920.0 * 1080.0;
                Assert.That(cross / 2, Is.GreaterThan(1e-3), at + " triangle " + t / 3);
            }
        }

        private static void AssertSameMesh(SceneryMesh actual, SceneryMesh expected)
        {
            Assert.That(actual.Triangles, Is.EqualTo(expected.Triangles));
            Assert.That(actual.Vertices.Length, Is.EqualTo(expected.Vertices.Length));
            for (int i = 0; i < expected.Vertices.Length; i++)
            {
                Assert.That(actual.Vertices[i].X, Is.EqualTo(expected.Vertices[i].X));
                Assert.That(actual.Vertices[i].Y, Is.EqualTo(expected.Vertices[i].Y));
            }
        }

        private static void Bounds(SceneryMesh mesh, out float left, out float right, out float high, out float low)
        {
            Assert.That(mesh.Vertices, Is.Not.Empty);
            left = float.MaxValue;
            right = float.MinValue;
            high = float.MaxValue;
            low = float.MinValue;
            foreach (SceneryVertex v in Corners(mesh))
            {
                left = Math.Min(left, v.X);
                right = Math.Max(right, v.X);
                high = Math.Min(high, v.Y);
                low = Math.Max(low, v.Y);
            }
        }

        private static bool Same(Rgba a, Rgba b)
        {
            const float e = 1e-5f;
            return Math.Abs(a.R - b.R) < e && Math.Abs(a.G - b.G) < e && Math.Abs(a.B - b.B) < e && Math.Abs(a.A - b.A) < e;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }

        private static double Wrap(double v, double span)
        {
            double r = v % span;
            return r < 0 ? r + span : r;
        }
    }
}
