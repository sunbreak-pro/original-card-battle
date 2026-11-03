// The far view of each layer drawn in code (#89, 2026-11-03): the trunks of the sunken forest, the colonnade of the
// sanctuary, the serpent's coils, the crystal pillars, the ribs on the dead sea, the canyon walls, the shards floating
// in the endless world. They stand between the sky and the haze, where the painted art for a layer will go
// (battle-visual-v1 §2.4), so the haze's light and the specks still pass over them, and the art replaces them when it lands.
//
// Pure C#: each group is a seeded recipe that BackdropScenery.Mesh turns into coloured triangles in fractions of the
// stage (0 at the top left, y down). The views only scale the triangles to the stage and draw them; every shape and
// colour comes from here, so `dotnet test` holds what the screen draws.
namespace Backdrop
{
    /// <summary>The kinds of far shapes a layer can stand in its view.</summary>
    public enum SceneryKind
    {
        /// <summary>Upright tree trunks, tapering towards the top, with a flare of roots at the foot.</summary>
        Trunks,
        /// <summary>Stone columns: a plinth, a shaft and a capital. <see cref="Scenery.Rough"/> of them are broken off.</summary>
        Pillars,
        /// <summary>Pointed crystal prisms in two facets: the shaded side in Color / Foot, the lit side in <see cref="Scenery.Light"/>.</summary>
        Crystals,
        /// <summary>A band along the bottom of the group with a wavy (Rough 0) to jagged (Rough 1) top edge: hills, dunes, rock, undergrowth. <see cref="Scenery.Hang"/> turns it into a roof hanging from the top.</summary>
        Ridge,
        /// <summary>A rock wall standing in from the left edge (X1 ≤ 0.5) or the right edge (X0 ≥ 0.5) with a jagged inner edge: a canyon side.</summary>
        Wall,
        /// <summary>Giant curved ribs rising from the ground in facing pairs, like a gate of bone.</summary>
        Ribs,
        /// <summary>A thick body winding across the group (a serpent's coils), with spikes of <see cref="Scenery.Light"/> along its back.</summary>
        Coil,
        /// <summary>Fragments of rock floating in the group's band, flat on top and pointed below, their top rim in <see cref="Scenery.Light"/>.</summary>
        Shards,
    }

    /// <summary>
    /// One group of far shapes. Positions are fractions of the stage (0 at the top left, y down); widths are shares
    /// of the width. Colours are sRGB with alpha, laid straight over what is under them; a vertex at height y takes
    /// <see cref="Color"/> at <see cref="Top"/> and <see cref="Foot"/> at <see cref="Base"/>, straight between.
    /// </summary>
    public sealed class Scenery
    {
        public SceneryKind Kind;
        /// <summary>The fill at the top of the group (<see cref="Top"/>).</summary>
        public Rgba Color;
        /// <summary>The fill at the foot of the group (<see cref="Base"/>): the haze thickening towards the ground.</summary>
        public Rgba Foot;
        /// <summary>The lit side: a crystal's facet, the spikes on a coil, a shard's rim, a pillar's lit edge.</summary>
        public Rgba Light;
        /// <summary>How many shapes the group stands (a Ridge or a Wall is one band whatever this is).</summary>
        public int Count = 1;
        /// <summary>Picks the places and sizes; the same seed always draws the same group.</summary>
        public int Seed;
        /// <summary>The horizontal band the shapes stand in.</summary>
        public float X0;
        public float X1 = 1f;
        /// <summary>The highest a shape reaches (a shape may run off the top when this is below 0).</summary>
        public float Top = 0.2f;
        /// <summary>Where the shapes stand: the horizon is 556 / 1080 ≈ 0.515.</summary>
        public float Base = 0.515f;
        /// <summary>A shape's width at its widest, as a share of the stage's width.</summary>
        public float Size = 0.03f;
        /// <summary>0: every shape alike; 1: heights and widths spread widely.</summary>
        public float Vary = 0.5f;
        /// <summary>Ridge / Wall: 0 smooth hills to 1 jagged rock. Pillars: the share broken off. Others: unused.</summary>
        public float Rough;
        /// <summary>The most a shape leans either way, in degrees.</summary>
        public float Lean;
        /// <summary>Ridge only: hang from <see cref="Top"/> down to an edge near <see cref="Base"/> (a canopy, a cave roof).</summary>
        public bool Hang;
    }

    /// <summary>One corner of a far shape: where it is (fractions of the stage, y down) and its colour.</summary>
    public readonly struct SceneryVertex
    {
        public readonly float X;
        public readonly float Y;
        public readonly Rgba Color;

        public SceneryVertex(float x, float y, Rgba color)
        {
            X = x;
            Y = y;
            Color = color;
        }
    }

    /// <summary>A group of far shapes as triangles: three indices into <see cref="Vertices"/> per triangle, drawn in order.</summary>
    public sealed class SceneryMesh
    {
        public SceneryVertex[] Vertices = System.Array.Empty<SceneryVertex>();
        public int[] Triangles = System.Array.Empty<int>();
        /// <summary>
        /// Which of <see cref="Vertices"/> are the outer corners of the fringe: a band one stage pixel wide round every
        /// outer edge of a shape, fading to nothing, because uGUI draws no anti-aliasing of its own and a hard slanted
        /// edge would show its steps. The rest are the shapes' own corners.
        /// </summary>
        public bool[] Fringe = System.Array.Empty<bool>();
    }

    public static class BackdropScenery
    {
        // The shapes are built in the pixels of the 1920×1080 stage, so a crystal or a rib keeps its proportions on
        // a wide screen, and handed back as fractions of it.
        private const double W = 1920.0;
        private const double H = 1080.0;

        // A triangle smaller than this (square pixels) draws nothing on the stage, so it is left out rather than
        // handed to a mesh as a sliver.
        private const double MinArea = 0.01;

        // The fringe round the outer edges: one stage pixel, as wide as the kit's own feather at 1080p.
        private const double FeatherPx = 1.0;
        private const double MinFringeEdgePx = 0.05;

        /// <summary>
        /// The triangles of <paramref name="scenery"/>, in fractions of the stage. The same scenery always gives the
        /// same mesh (no Random: every choice is <see cref="BackdropMotion.Hash"/> of the seed). Every triangle has an
        /// area and turns the same way, clockwise as seen on the stage (y down). A count of 0 or less draws nothing.
        /// </summary>
        public static SceneryMesh Mesh(Scenery scenery)
        {
            if (scenery == null || scenery.Count <= 0) return new SceneryMesh();
            var b = new Builder(scenery);
            switch (scenery.Kind)
            {
                case SceneryKind.Trunks: Trunks(b); break;
                case SceneryKind.Pillars: Pillars(b); break;
                case SceneryKind.Crystals: Crystals(b); break;
                case SceneryKind.Ridge: Ridge(b); break;
                case SceneryKind.Wall: Wall(b); break;
                case SceneryKind.Ribs: Ribs(b); break;
                case SceneryKind.Coil: Coil(b); break;
                case SceneryKind.Shards: Shards(b); break;
            }
            return b.ToMesh();
        }

        private static void Trunks(Builder b)
        {
            Scenery s = b.S;
            // Foot to top: the flare of roots takes the lowest 6%, then the trunk tapers straight to its top.
            double[] levels = { 0.0, 0.025, 0.06, 0.3, 0.55, 0.8, 1.0 };
            for (int i = 0; i < s.Count; i++)
            {
                b.EndShape();
                // One draw sets both the height and the width, so the tall trunks are the wide (near) ones.
                double h = b.R(i, 21);
                double w = s.Size * W * (1 - s.Vary / 2 + s.Vary * h);
                double foot = b.BasePx;
                double len = foot - (b.TopPx + b.SpanPx * s.Vary * 0.5 * (1 - h));
                if (!(len > 0) || !(w > 0)) continue;
                double cx = b.SlotX(i, 22, 0.7);
                double lean = System.Math.Tan(Radians((b.R(i, 23) * 2 - 1) * s.Lean));
                double bow = (b.R(i, 24) - 0.5) * 0.5 * w;

                double Half(double f)
                {
                    double shaft = w / 2 * (1 - 0.45 * f);
                    if (f >= 0.06) return shaft;
                    double r = 1 - f / 0.06;
                    return shaft * (1 + 0.8 * r * r);
                }
                double Cx(double f) => cx + len * f * lean + bow * System.Math.Sin(System.Math.PI * f);
                double Y(double f) => foot - len * f;

                for (int k = 0; k < levels.Length - 1; k++)
                {
                    double f0 = levels[k], f1 = levels[k + 1];
                    b.Quad(new Pt(Cx(f0) - Half(f0), Y(f0)), new Pt(Cx(f0) + Half(f0), Y(f0)),
                        new Pt(Cx(f1) + Half(f1), Y(f1)), new Pt(Cx(f1) - Half(f1), Y(f1)));
                }

                if (h <= 0.55 || len <= 4 * w) continue;
                int stubs = b.R(i, 25) < 0.5 ? 1 : 2;
                double side = b.R(i, 26) < 0.5 ? -1 : 1;
                for (int k = 0; k < stubs; k++, side = -side)
                {
                    // A stub grows from just inside the trunk's edge: a translucent mesh lays its alpha twice where two of its
                    // triangles overlap, so the stub must not reach across the trunk.
                    double f = 0.45 + 0.4 * b.R(i, 27 + k);
                    double thick = Half(f) * 0.7;
                    double reach = w * (1.6 + 1.6 * b.R(i, 29 + k));
                    double up = Radians(35 + 25 * b.R(i, 31 + k));
                    double x = Cx(f) + side * Half(f) * 0.8, y = Y(f);
                    b.Tri(new Pt(x, y - thick / 2), new Pt(x, y + thick / 2),
                        new Pt(x + side * reach * System.Math.Cos(up), y - reach * System.Math.Sin(up)));
                }
            }
        }

        private static void Pillars(Builder b)
        {
            Scenery s = b.S;
            double litSide = b.R(0, 40) < 0.5 ? -1 : 1; // one light for the whole colonnade
            for (int i = 0; i < s.Count; i++)
            {
                b.EndShape();
                double h = b.R(i, 41);
                double w = s.Size * W * (1 - s.Vary / 2 + s.Vary * h);
                double foot = b.BasePx;
                double top = b.TopPx + b.SpanPx * s.Vary * 0.5 * (1 - h);
                double len = foot - top;
                if (!(len > 0) || !(w > 0)) continue;
                double cx = b.SlotX(i, 42, 0.5);
                double lean = System.Math.Tan(Radians((b.R(i, 43) * 2 - 1) * s.Lean));
                bool broken = b.R(i, 44) < s.Rough;

                double plinth = System.Math.Min(Clamp(0.05 * len, 0.2 * w, 0.6 * w), 0.25 * len);
                double cap = System.Math.Min(Clamp(0.045 * len, 0.2 * w, 0.5 * w), 0.2 * len);
                double shaftFoot = foot - plinth;
                double shaftTop = top + cap; // where a whole shaft meets its capital; a broken one tapers the same way

                double Cx(double y) => cx + (foot - y) * lean;
                double ShaftHalf(double y) => w / 2 * (1 - 0.12 * (shaftFoot - y) / (shaftFoot - shaftTop));
                Pt Shaft(double y, double u, bool lit = false) => new Pt(Cx(y) + u * ShaftHalf(y), y, lit);
                void Slab(double y0, double half0, double y1, double half1)
                {
                    b.Quad(new Pt(Cx(y0) - half0, y0), new Pt(Cx(y0) + half0, y0), new Pt(Cx(y1) + half1, y1), new Pt(Cx(y1) - half1, y1));
                }

                double step = foot - plinth * 0.55;
                Slab(foot, 0.675 * w, step, 0.675 * w);
                Slab(step, 0.6 * w, shaftFoot, 0.6 * w);

                double litTop; // how far up the lit strip runs
                if (broken)
                {
                    double stub = foot - len * (0.35 + 0.35 * b.R(i, 45));
                    double amp = System.Math.Min(0.75 * w, 0.5 * (shaftFoot - stub));
                    double cut = stub + amp * 1.05; // below every point of the jag, so each piece of it has a height
                    b.Quad(Shaft(shaftFoot, -1), Shaft(shaftFoot, 1), Shaft(cut, 1), Shaft(cut, -1));

                    int points = (b.R(i, 46) < 0.5 ? 3 : 4) + 2; // 3 or 4 points between the two corners
                    var across = new double[points];
                    var height = new double[points];
                    for (int k = 0; k < points; k++)
                    {
                        across[k] = -1 + 2.0 * k / (points - 1);
                        if (k > 0 && k < points - 1) across[k] += (b.R(i, 47 + k) - 0.5) * 0.6 / (points - 1);
                        double dip = k % 2 == 0 ? 0.35 + 0.65 * b.R(i, 53 + k) : 0.2 * b.R(i, 53 + k);
                        height[k] = stub + amp * dip;
                    }
                    for (int k = 0; k < points - 1; k++)
                    {
                        b.Quad(Shaft(height[k], across[k]), Shaft(height[k + 1], across[k + 1]), Shaft(cut, across[k + 1]), Shaft(cut, across[k]));
                    }
                    litTop = cut;
                }
                else
                {
                    b.Quad(Shaft(shaftFoot, -1), Shaft(shaftFoot, 1), Shaft(shaftTop, 1), Shaft(shaftTop, -1));
                    double neck = top + cap * 0.45;
                    Slab(shaftTop, 0.475 * w, neck, 0.65 * w);
                    Slab(neck, 0.725 * w, top, 0.725 * w);
                    litTop = shaftTop;
                }

                // 18% of the shaft's width (0.36 of its −1..1 span), set in from the lit edge.
                double inner = litSide * 0.52, outer = litSide * 0.88;
                b.Quad(Shaft(shaftFoot, inner, true), Shaft(shaftFoot, outer, true), Shaft(litTop, outer, true), Shaft(litTop, inner, true));
            }
        }

        private static void Crystals(Builder b)
        {
            Scenery s = b.S;
            bool litLeft = b.R(0, 60) < 0.5; // one light for the whole group
            for (int i = 0; i < s.Count; i++)
            {
                double h = b.R(i, 61);
                double w = s.Size * W * (1 - s.Vary / 2 + s.Vary * h);
                double tall = b.SpanPx * (1 - s.Vary * 0.5 * (1 - h));
                if (!(tall > 0) || !(w > 0)) continue;
                double cx = b.SlotX(i, 62, 0.7);
                double foot = b.BasePx + (b.R(i, 63) * 2 - 1) * 0.01 * H;
                double lean = (b.R(i, 64) * 2 - 1) * s.Lean;

                // The companions go first so the main prism stands in front of them; they lean outward, as a cluster grows.
                if (s.Vary > 0.3f)
                {
                    int companions = b.R(i, 66) < 0.5 ? 1 : 2;
                    double side = b.R(i, 67) < 0.5 ? -1 : 1;
                    for (int k = 0; k < companions; k++, side = -side)
                    {
                        double scale = k == 0 ? 0.6 : 0.4;
                        double cw = w * scale;
                        Crystal(b, cx + side * (0.42 * w + 0.3 * cw), foot + 0.004 * H * b.R(i, 68 + k), cw, tall * scale,
                            lean + side * (6 + 0.5 * s.Lean), (b.R(i, 70 + k) - 0.5) * 0.3 * cw, litLeft);
                    }
                }
                Crystal(b, cx, foot, w, tall, lean, (b.R(i, 65) - 0.5) * 0.3 * w, litLeft);
            }
        }

        /// <summary>One prism standing on (cx, foot): split along the line from its foot to its tip into a shaded and a lit facet.</summary>
        private static void Crystal(Builder b, double cx, double foot, double w, double tall, double leanDeg, double tipOff, bool litLeft)
        {
            b.EndShape();
            double a = Radians(leanDeg), cos = System.Math.Cos(a), sin = System.Math.Sin(a);
            Pt P(double dx, double dy, bool lit) => new Pt(cx + dx * cos - dy * sin, foot + dx * sin + dy * cos, lit);
            double half = w / 2;

            Pt footLeft = P(-half, 0, litLeft), footMid = P(0, 0, litLeft), tip = P(tipOff, -tall, litLeft), shoulder = P(-half, -0.75 * tall, litLeft);
            b.Tri(footLeft, footMid, tip);
            b.Tri(footLeft, tip, shoulder);

            footMid = P(0, 0, !litLeft);
            tip = P(tipOff, -tall, !litLeft);
            Pt footRight = P(half, 0, !litLeft);
            shoulder = P(half, -0.8 * tall, !litLeft);
            b.Tri(footMid, footRight, shoulder);
            b.Tri(footMid, shoulder, tip);
        }

        private static void Ridge(Builder b)
        {
            Scenery s = b.S;
            // A band that starts at an edge of the stage runs just past it, so no sliver of sky shows at the side.
            double xa = (s.X0 <= 0f ? -0.01 : s.X0) * W;
            double xb = (s.X1 >= 1f ? 1.01 : s.X1) * W;
            int n = System.Math.Max(24, s.Count * 8);
            double reach = b.SpanPx * s.Vary;
            for (int k = 0; k < n; k++)
            {
                double x0 = xa + (xb - xa) * k / n, x1 = xa + (xb - xa) * (k + 1) / n;
                if (s.Hang)
                {
                    double e0 = b.BasePx - reach * Edge(b, k, n), e1 = b.BasePx - reach * Edge(b, k + 1, n);
                    b.Quad(new Pt(x0, b.TopPx), new Pt(x1, b.TopPx), new Pt(x1, e1), new Pt(x0, e0));
                }
                else
                {
                    double e0 = b.TopPx + reach * Edge(b, k, n), e1 = b.TopPx + reach * Edge(b, k + 1, n);
                    b.Quad(new Pt(x0, e0), new Pt(x1, e1), new Pt(x1, b.BasePx), new Pt(x0, b.BasePx));
                }
            }
        }

        private static void Wall(Builder b)
        {
            Scenery s = b.S;
            bool left = s.X0 + s.X1 < 1f; // the side of the stage the band sits on
            double outer = (left ? -0.01 : 1.01) * W;
            double x0 = s.X0 * W, x1 = s.X1 * W;
            int n = System.Math.Max(24, s.Count * 8);
            double EdgeX(int k)
            {
                double e = Edge(b, k, n) * s.Vary * (x1 - x0);
                return left ? x1 - e : x0 + e;
            }
            for (int k = 0; k < n; k++)
            {
                double y0 = b.TopPx + b.SpanPx * k / n, y1 = b.TopPx + b.SpanPx * (k + 1) / n;
                b.Quad(new Pt(outer, y0), new Pt(EdgeX(k), y0), new Pt(EdgeX(k + 1), y1), new Pt(outer, y1));
            }
        }

        /// <summary>
        /// How far a ridge's or a wall's edge falls back at point k of n: 0 where it reaches furthest, 1 the most.
        /// Count hills (the odd knots of a lattice of 2 × Count) on a smooth curve; Rough turns the curve angular and
        /// lays a sharp up-and-down over it point by point, so 1 reads as broken rock.
        /// </summary>
        private static double Edge(Builder b, int k, int n)
        {
            Scenery s = b.S;
            int hills = System.Math.Max(1, s.Count);
            double rough = Clamp01(s.Rough);
            double p = (double)k / n * 2 * hills;
            int j = System.Math.Min((int)System.Math.Floor(p), 2 * hills - 1);
            double t = p - j;
            double eased = t * t * (3 - 2 * t);
            t = eased + (t - eased) * rough;
            double hill = Knot(b, j) + (Knot(b, j + 1) - Knot(b, j)) * t;
            double jag = (k % 2 == 0 ? -1 : 1) * b.R(k, 72);
            return Clamp01(1 - hill + rough * 0.22 * jag);
        }

        private static double Knot(Builder b, int j)
        {
            return j % 2 == 1 ? 0.6 + 0.4 * b.R(j, 71) : 0.4 * b.R(j, 71);
        }

        private static void Ribs(Builder b)
        {
            Scenery s = b.S;
            const int segments = 16;
            int pairs = (s.Count + 1) / 2;
            double slot = (b.X1Px - b.X0Px) / pairs;
            double thick = s.Size * W;
            if (!(thick > 0)) return;
            for (int i = 0; i < s.Count; i++)
            {
                b.EndShape();
                int pair = i / 2;
                double dir = i % 2 == 0 ? 1 : -1; // even ribs curve right, odd ones left, so a pair arches together
                double centre = b.X0Px + slot * (pair + 0.5 + (b.R(pair, 81) - 0.5) * 0.3);
                double gate = slot * (0.28 + 0.12 * b.R(pair, 82));
                double fx = centre - dir * gate + (b.R(i, 83) - 0.5) * 0.1 * gate, fy = b.BasePx;
                double ty = b.TopPx + b.SpanPx * s.Vary * 0.4 * b.R(i, 84);
                if (!(fy - ty > 0)) continue;
                double tx = centre - dir * gate * (0.1 + 0.15 * b.R(i, 85));
                // The control point sits above the foot at the tip's height: the rib rises nearly upright and arrives at
                // its tip level, pointing across the gate, never higher than the tip.
                double qx = fx - dir * gate * 0.15, qy = ty;

                Pt lastLeft = default, lastRight = default;
                for (int k = 0; k <= segments; k++)
                {
                    double t = (double)k / segments, u = 1 - t;
                    double px = u * u * fx + 2 * u * t * qx + t * t * tx;
                    double py = u * u * fy + 2 * u * t * qy + t * t * ty;
                    double dx = 2 * u * (qx - fx) + 2 * t * (tx - qx);
                    double dy = 2 * u * (qy - fy) + 2 * t * (ty - qy);
                    double len = System.Math.Sqrt(dx * dx + dy * dy);
                    if (!(len > 0)) continue;
                    dx /= len;
                    dy /= len;
                    double half = thick * (1 - 0.75 * t) / 2;
                    var left = new Pt(px - dy * half, py + dx * half);
                    var right = new Pt(px + dy * half, py - dx * half);
                    if (k > 0) b.Quad(lastLeft, lastRight, right, left);
                    lastLeft = left;
                    lastRight = right;
                    if (k == segments) b.Tri(left, right, new Pt(px + dx * thick * 0.15, py + dy * thick * 0.15));
                }
            }
        }

        private static void Coil(Builder b)
        {
            Scenery s = b.S;
            int humps = System.Math.Max(1, System.Math.Min(s.Count, 16));
            int segments = 48 * humps;
            double xa = (s.X0 <= 0f ? -0.02 : s.X0) * W;
            double xb = (s.X1 >= 1f ? 1.02 : s.X1) * W;
            bool tail = s.X0 > 0f, head = s.X1 < 1f; // an end inside the stage tapers off; one at the edge runs out of sight
            double phase = 2 * System.Math.PI * b.R(0, 91);
            double thick = s.Size * W;
            double swell = s.Vary / 3.0;
            // The body's inner edge folds over itself where its half-width passes the centreline's radius of curvature, which
            // is smallest at a crest: 1 / (A k²) for y = A sin(k x). Every half-width stays under 0.9 of it.
            double amp = b.SpanPx * 0.4, wave = 2 * System.Math.PI * humps / (xb - xa);
            double maxHalf = amp * wave * wave > 0 ? 0.9 / (amp * wave * wave) : double.MaxValue;

            var upper = new Pt[segments + 1];
            var lower = new Pt[segments + 1];
            var nx = new double[segments + 1];
            var ny = new double[segments + 1];
            var half = new double[segments + 1];
            for (int k = 0; k <= segments; k++)
            {
                double u = (double)k / segments;
                double angle = 2 * System.Math.PI * humps * u + phase;
                double x = xa + (xb - xa) * u;
                double y = b.TopPx + b.SpanPx * (0.5 + 0.4 * System.Math.Sin(angle));
                double dx = xb - xa, dy = b.SpanPx * 0.4 * 2 * System.Math.PI * humps * System.Math.Cos(angle);
                double len = System.Math.Sqrt(dx * dx + dy * dy);
                // The normal on the back's side (up the stage while the body runs left to right).
                nx[k] = len > 0 ? dy / len : 0;
                ny[k] = len > 0 ? -dx / len : -1;
                half[k] = System.Math.Min(thick * (1 + swell * (2 * Wave(b, u, humps) - 1)) * Taper(u, tail, head) / 2, maxHalf);
                upper[k] = new Pt(x + nx[k] * half[k], y + ny[k] * half[k]);
                lower[k] = new Pt(x - nx[k] * half[k], y - ny[k] * half[k]);
            }
            for (int k = 0; k < segments; k++) b.Quad(upper[k], upper[k + 1], lower[k + 1], lower[k]);

            for (int k = 1; k + 2 <= segments; k += 3)
            {
                // A spike's foot is set into the body so no sky shows under it, and it rakes back towards the tail.
                double tall = half[k + 1];
                double inset = 0.15 * tall;
                Pt a = upper[k], c = upper[k + 2], m = upper[k + 1];
                double tx = -ny[k + 1], ty = nx[k + 1]; // along the body, towards the head
                b.Tri(new Pt(a.X - nx[k] * inset, a.Y - ny[k] * inset, true),
                    new Pt(c.X - nx[k + 2] * inset, c.Y - ny[k + 2] * inset, true),
                    new Pt(m.X + nx[k + 1] * tall - tx * 0.25 * tall, m.Y + ny[k + 1] * tall - ty * 0.25 * tall, true));
            }
        }

        /// <summary>A smooth wobble in [0, 1] along the coil, two knots a hump, so its thickness swells and thins.</summary>
        private static double Wave(Builder b, double u, int humps)
        {
            double p = u * 2 * humps;
            int j = System.Math.Min((int)System.Math.Floor(p), 2 * humps - 1);
            double t = p - j;
            t = t * t * (3 - 2 * t);
            double v0 = b.R(j, 92), v1 = b.R(j + 1, 92);
            return v0 + (v1 - v0) * t;
        }

        private static double Taper(double u, bool tail, bool head)
        {
            double t = 1;
            if (tail && u < 0.1) t = System.Math.Min(t, Ease(u / 0.1));
            if (head && u > 0.9) t = System.Math.Min(t, Ease((1 - u) / 0.1));
            return 0.15 + 0.85 * t;
        }

        private static void Shards(Builder b)
        {
            Scenery s = b.S;
            for (int i = 0; i < s.Count; i++)
            {
                b.EndShape();
                double w = s.Size * W * (0.5 + b.R(i, 101));
                double tall = w * (1.2 + 0.8 * b.R(i, 102));
                if (!(w > 0)) continue;
                double cx = b.SlotX(i, 103, 0.6);
                // The shard runs 0.35 of its height above its centre and 0.65 below; it floats wholly inside the band when it fits.
                double lo = b.TopPx + 0.35 * tall, hi = b.BasePx - 0.65 * tall;
                double cy = hi > lo ? lo + (hi - lo) * b.R(i, 104) : (b.TopPx + b.BasePx) / 2 - 0.15 * tall;
                double a = Radians((b.R(i, 105) * 2 - 1) * s.Lean), cos = System.Math.Cos(a), sin = System.Math.Sin(a);
                double slope = (b.R(i, 106) - 0.5) * 0.12 * tall;
                double tip = (b.R(i, 107) - 0.5) * 0.4 * w;
                Pt P(double dx, double dy, bool lit = false) => new Pt(cx + dx * cos - dy * sin, cy + dx * sin + dy * cos, lit);

                // Top left, top right, right shoulder (the widest), right knee, bottom tip, left knee, left shoulder: a slab of
                // rock under the flat top, then a taper to the point, so it reads as torn ground rather than a cone.
                double kneeR = 0.22 + 0.1 * b.R(i, 110), kneeL = 0.22 + 0.1 * b.R(i, 111);
                double[] ox = { -0.44 * w, 0.44 * w, 0.5 * w, kneeR * w, tip, -kneeL * w, -0.5 * w };
                double[] oy =
                {
                    -0.35 * tall + slope, -0.35 * tall - slope, (-0.22 + 0.08 * b.R(i, 108)) * tall, (0.12 + 0.12 * b.R(i, 112)) * tall,
                    0.65 * tall, (0.1 + 0.12 * b.R(i, 113)) * tall, (-0.2 + 0.08 * b.R(i, 109)) * tall,
                };
                double mx = 0, my = 0;
                for (int k = 0; k < ox.Length; k++)
                {
                    mx += ox[k] / ox.Length;
                    my += oy[k] / oy.Length;
                }
                for (int k = 0; k < ox.Length; k++)
                {
                    int next = (k + 1) % ox.Length;
                    b.Tri(P(mx, my), P(ox[k], oy[k]), P(ox[next], oy[next]));
                }

                double rim = System.Math.Max(2.5, 0.07 * tall);
                b.Quad(P(ox[0], oy[0], true), P(ox[1], oy[1], true), P(ox[1] * 0.97, oy[1] + rim, true), P(ox[0] * 0.97, oy[0] + rim, true));
            }
        }

        private static double Radians(double degrees)
        {
            return degrees * System.Math.PI / 180.0;
        }

        private static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        private static double Clamp01(double v)
        {
            return Clamp(v, 0, 1);
        }

        private static double Ease(double t)
        {
            t = Clamp01(t);
            return t * t * (3 - 2 * t);
        }

        /// <summary>A corner in the stage's pixels, and whether it takes the lit colour.</summary>
        private readonly struct Pt
        {
            public readonly double X;
            public readonly double Y;
            public readonly bool Lit;

            public Pt(double x, double y, bool lit = false)
            {
                X = x;
                Y = y;
                Lit = lit;
            }
        }

        /// <summary>Gathers the triangles of one group: clamps them to the stage's margin, drops the slivers, shares the corners.</summary>
        private sealed class Builder
        {
            public readonly Scenery S;
            public readonly double TopPx;
            public readonly double BasePx;
            public readonly double SpanPx;
            public readonly double X0Px;
            public readonly double X1Px;

            private readonly float _yMin;
            private readonly System.Collections.Generic.List<SceneryVertex> _vertices = new System.Collections.Generic.List<SceneryVertex>();
            private readonly System.Collections.Generic.List<int> _triangles = new System.Collections.Generic.List<int>();
            private readonly System.Collections.Generic.Dictionary<(float, float, bool), int> _seen = new System.Collections.Generic.Dictionary<(float, float, bool), int>();
            private readonly System.Collections.Generic.List<bool> _fringe = new System.Collections.Generic.List<bool>();
            private int _shapeStart; // the first triangle of the shape being built

            public Builder(Scenery s)
            {
                S = s;
                TopPx = s.Top * H;
                BasePx = s.Base * H;
                SpanPx = BasePx - TopPx;
                X0Px = s.X0 * W;
                X1Px = s.X1 * W;
                _yMin = System.Math.Min(s.Top, 0f) - 0.05f;
            }

            /// <summary>A fixed number in [0, 1) for shape <paramref name="index"/> of this group, one <paramref name="salt"/> per choice.</summary>
            public double R(int index, int salt)
            {
                return BackdropMotion.Hash(unchecked(S.Seed * 100003 + index), salt);
            }

            /// <summary>Where shape <paramref name="i"/> stands: its own slot of X0..X1, moved inside it by up to half of <paramref name="jitter"/>, so a group never bunches up.</summary>
            public double SlotX(int i, int salt, double jitter)
            {
                double slot = (X1Px - X0Px) / S.Count;
                return X0Px + slot * (i + 0.5 + (R(i, salt) - 0.5) * jitter);
            }

            /// <summary>The quad a → b → c → d, corners in order round it.</summary>
            public void Quad(Pt a, Pt b, Pt c, Pt d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            public void Tri(Pt a, Pt b, Pt c)
            {
                float ax = Fx(a), ay = Fy(a), bx = Fx(b), by = Fy(b), cx = Fx(c), cy = Fy(c);
                if (!Finite(ax) || !Finite(ay) || !Finite(bx) || !Finite(by) || !Finite(cx) || !Finite(cy)) return;
                double cross = ((double)(bx - ax) * (cy - ay) - (double)(cx - ax) * (by - ay)) * W * H;
                if (!(System.Math.Abs(cross) / 2 > MinArea)) return;
                int ia = Index(ax, ay, a.Lit), ib = Index(bx, by, b.Lit), ic = Index(cx, cy, c.Lit);
                _triangles.Add(ia);
                _triangles.Add(cross > 0 ? ib : ic);
                _triangles.Add(cross > 0 ? ic : ib);
            }

            public SceneryMesh ToMesh()
            {
                EndShape();
                return new SceneryMesh { Vertices = _vertices.ToArray(), Triangles = _triangles.ToArray(), Fringe = _fringe.ToArray() };
            }

            /// <summary>
            /// Closes the shape built since the last call: its fringe goes straight after it, so a shape drawn later (nearer)
            /// covers the fringe of the one behind it as it covers its body.
            /// </summary>
            public void EndShape()
            {
                AddFringe(_shapeStart, _triangles.Count);
                _shapeStart = _triangles.Count;
            }

            /// <summary>
            /// The fringe of one shape (its triangles from <paramref name="start"/> to <paramref name="end"/>): every edge only
            /// one of them uses gets a band <see cref="FeatherPx"/> wide on its outside, from the edge's colours to the same
            /// colours at alpha 0. A seam between a shaded and a lit facet shares its corners' places, so it is used twice; an
            /// edge whose outside lies under another part of the same shape (a capital on its shaft, a spike set into the body,
            /// the lit strip on a pillar) is a seam too, and gets none.
            /// </summary>
            private void AddFringe(int start, int end)
            {
                if (end <= start) return;
                var uses = new System.Collections.Generic.Dictionary<(float, float, float, float), int>();
                for (int t = start; t < end; t += 3)
                {
                    for (int e = 0; e < 3; e++)
                    {
                        var key = EdgeKey(_triangles[t + e], _triangles[t + (e + 1) % 3]);
                        uses[key] = uses.TryGetValue(key, out int c) ? c + 1 : 1;
                    }
                }
                for (int t = start; t < end; t += 3)
                {
                    for (int e = 0; e < 3; e++)
                    {
                        int ia = _triangles[t + e], ib = _triangles[t + (e + 1) % 3];
                        if (uses[EdgeKey(ia, ib)] != 1) continue;
                        SceneryVertex a = _vertices[ia], b = _vertices[ib];
                        double dx = (b.X - a.X) * W, dy = (b.Y - a.Y) * H;
                        double len = System.Math.Sqrt(dx * dx + dy * dy);
                        if (len < MinFringeEdgePx) continue;
                        // Clockwise on the y-down stage, the inside of a → b lies to its right, so the outside is (dy, −dx).
                        double ox = dy / len * FeatherPx / W, oy = -dx / len * FeatherPx / H;
                        double mx = (a.X + b.X) / 2.0 + ox / 2, my = (a.Y + b.Y) / 2.0 + oy / 2;
                        if (Covered(mx, my, start, end, t)) continue;
                        int oa = Raw(a.X + ox, a.Y + oy, a.Color.WithAlpha(0f));
                        int ob = Raw(b.X + ox, b.Y + oy, b.Color.WithAlpha(0f));
                        Wound(ia, ib, ob);
                        Wound(ia, ob, oa);
                    }
                }
            }

            /// <summary>Whether (x, y) lies inside a triangle of the shape other than <paramref name="skip"/>.</summary>
            private bool Covered(double x, double y, int start, int end, int skip)
            {
                for (int t = start; t < end; t += 3)
                {
                    if (t == skip) continue;
                    if (Inside(_vertices[_triangles[t]], _vertices[_triangles[t + 1]], _vertices[_triangles[t + 2]], x, y, out _, out _, out _)) return true;
                }
                return false;
            }

            private (float, float, float, float) EdgeKey(int i, int j)
            {
                SceneryVertex a = _vertices[i], b = _vertices[j];
                bool aFirst = a.X < b.X || (a.X == b.X && a.Y <= b.Y);
                return aFirst ? (a.X, a.Y, b.X, b.Y) : (b.X, b.Y, a.X, a.Y);
            }

            private int Raw(double x, double y, Rgba color)
            {
                int at = _vertices.Count;
                _vertices.Add(new SceneryVertex((float)Clamp(x, -0.05, 1.05), (float)Clamp(y, _yMin, 1.05), color));
                _fringe.Add(true);
                return at;
            }

            /// <summary>The triangle i, j, k turned clockwise on the stage like every other; dropped when it has no area.</summary>
            private void Wound(int i, int j, int k)
            {
                SceneryVertex a = _vertices[i], b = _vertices[j], c = _vertices[k];
                double cross = ((double)(b.X - a.X) * (c.Y - a.Y) - (double)(c.X - a.X) * (b.Y - a.Y)) * W * H;
                if (!(System.Math.Abs(cross) / 2 > MinArea)) return;
                _triangles.Add(i);
                _triangles.Add(cross > 0 ? j : k);
                _triangles.Add(cross > 0 ? k : j);
            }

            private float Fx(Pt p)
            {
                return (float)Clamp(p.X / W, -0.05, 1.05);
            }

            private float Fy(Pt p)
            {
                return (float)Clamp(p.Y / H, _yMin, 1.05);
            }

            private int Index(float x, float y, bool lit)
            {
                if (_seen.TryGetValue((x, y, lit), out int at)) return at;
                at = _vertices.Count;
                _vertices.Add(new SceneryVertex(x, y, ColorAt(y, lit)));
                _fringe.Add(false);
                _seen.Add((x, y, lit), at);
                return at;
            }

            /// <summary>The colour at height <paramref name="y"/>: Color (or Light) at Top, straight down to Foot at Base.</summary>
            private Rgba ColorAt(float y, bool lit)
            {
                double t = S.Base > S.Top ? (y - S.Top) / (double)(S.Base - S.Top) : (y >= S.Base ? 1 : 0);
                return BackdropSpec.Mix(lit ? S.Light : S.Color, S.Foot, (float)Clamp01(t));
            }

            private static bool Finite(float v)
            {
                return !float.IsNaN(v) && !float.IsInfinity(v);
            }
        }

        /// <summary>The meshes of every group of <paramref name="backdrop"/>, back to front (the order of <see cref="LayerBackdrop.Scenery"/>).</summary>
        public static SceneryMesh[] Meshes(LayerBackdrop backdrop)
        {
            Scenery[] all = backdrop?.Scenery ?? System.Array.Empty<Scenery>();
            var meshes = new SceneryMesh[all.Length];
            for (int i = 0; i < all.Length; i++) meshes[i] = all[i] == null ? new SceneryMesh() : Mesh(all[i]);
            return meshes;
        }

        /// <summary>The heights the contrast checks read the far view at: every 1/80 of the screen, and nine through the floor's band.</summary>
        private static readonly float[] SampleHeights = BuildSampleHeights();

        private static float[] BuildSampleHeights()
        {
            var ys = new System.Collections.Generic.List<float>();
            for (int i = 0; i <= 80; i++) ys.Add(i / 80f);
            for (int i = 0; i <= 8; i++) ys.Add(BackdropSpec.FloorTop + (BackdropSpec.FloorBottom - BackdropSpec.FloorTop) * i / 8f);
            return ys.ToArray();
        }

        /// <summary>
        /// Points of the far view for the contrast checks, each with the opaque colour the stage shows there before the
        /// haze: the sky at its height with every group laid over it back to front, every triangle that covers the point
        /// in drawing order, so far shapes seen through nearer translucent ones count as drawn. The points sit just inside
        /// each triangle: near its corners, and where it crosses the sample heights (both sides and the middle). A
        /// triangle's colour runs straight between its corners, so its brightest point is near one of these.
        /// </summary>
        public static System.Collections.Generic.List<SceneryVertex> Samples(LayerBackdrop backdrop)
        {
            var points = new System.Collections.Generic.List<SceneryVertex>();
            SceneryMesh[] meshes = Meshes(backdrop);
            var bins = new Bins[meshes.Length];
            for (int g = 0; g < meshes.Length; g++) bins[g] = new Bins(meshes[g]);
            var probe = new System.Collections.Generic.List<(double X, double Y)>();
            foreach (SceneryMesh mesh in meshes)
            {
                SceneryVertex[] v = mesh.Vertices;
                int[] t = mesh.Triangles;
                for (int k = 0; k + 2 < t.Length; k += 3)
                {
                    SceneryVertex a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                    probe.Clear();
                    probe.Add(Blend(a, b, c, 0.9, 0.05));
                    probe.Add(Blend(b, c, a, 0.9, 0.05));
                    probe.Add(Blend(c, a, b, 0.9, 0.05));
                    float lo = System.Math.Min(a.Y, System.Math.Min(b.Y, c.Y)), hi = System.Math.Max(a.Y, System.Math.Max(b.Y, c.Y));
                    foreach (float y in SampleHeights)
                    {
                        if (y <= lo || y >= hi) continue;
                        var cut = new System.Collections.Generic.List<(double X, double Y)>(3);
                        Cross(a, b, y, cut);
                        Cross(b, c, y, cut);
                        Cross(c, a, y, cut);
                        if (cut.Count < 2) continue;
                        double x0 = System.Math.Min(cut[0].X, cut[1].X), x1 = System.Math.Max(cut[0].X, cut[1].X), span = x1 - x0;
                        probe.Add((x0 + span * 0.02, y));
                        probe.Add((x0 + span * 0.5, y));
                        probe.Add((x1 - span * 0.02, y));
                    }
                    foreach ((double x, double y) in probe) points.Add(new SceneryVertex((float)x, (float)y, Composite(backdrop, meshes, bins, x, y)));
                }
            }
            return points;
        }

        /// <summary>The point weighted <paramref name="wa"/> towards a and <paramref name="wb"/> towards b (the rest towards c).</summary>
        private static (double X, double Y) Blend(SceneryVertex a, SceneryVertex b, SceneryVertex c, double wa, double wb)
        {
            double wc = 1 - wa - wb;
            return (a.X * wa + b.X * wb + c.X * wc, a.Y * wa + b.Y * wb + c.Y * wc);
        }

        /// <summary>The opaque colour at (x, y): the sky, then every covering triangle of every group in drawing order.</summary>
        private static Rgba Composite(LayerBackdrop backdrop, SceneryMesh[] meshes, Bins[] bins, double x, double y)
        {
            float sy = y < 0 ? 0f : y > 1 ? 1f : (float)y;
            Rgba under = backdrop.Sky != null && backdrop.Sky.Length > 0 ? BackdropSpec.SkyAt(backdrop, sy) : new Rgba(0f, 0f, 0f);
            for (int g = 0; g < meshes.Length; g++)
            {
                SceneryVertex[] v = meshes[g].Vertices;
                int[] t = meshes[g].Triangles;
                foreach (int k in bins[g].At(x, y))
                {
                    SceneryVertex a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                    if (!Inside(a, b, c, x, y, out double la, out double lb, out double lc)) continue;
                    var col = new Rgba((float)(a.Color.R * la + b.Color.R * lb + c.Color.R * lc), (float)(a.Color.G * la + b.Color.G * lb + c.Color.G * lc),
                        (float)(a.Color.B * la + b.Color.B * lb + c.Color.B * lc), (float)(a.Color.A * la + b.Color.A * lb + c.Color.A * lc));
                    under = BackdropSpec.Over(col, under);
                }
            }
            return new Rgba(under.R, under.G, under.B, 1f);
        }

        /// <summary>
        /// Whether (x, y) lies strictly inside the triangle a, b, c (in the stage's pixels, so a thin triangle is judged
        /// at its true shape), with its weights towards each corner.
        /// </summary>
        private static bool Inside(SceneryVertex a, SceneryVertex b, SceneryVertex c, double x, double y, out double la, out double lb, out double lc)
        {
            double ax = a.X * W, ay = a.Y * H, bx = b.X * W, by = b.Y * H, cx = c.X * W, cy = c.Y * H, px = x * W, py = y * H;
            double d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
            la = lb = lc = 0;
            if (System.Math.Abs(d) < 1e-12) return false;
            la = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / d;
            lb = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / d;
            lc = 1 - la - lb;
            const double e = 1e-9;
            return la > e && lb > e && lc > e;
        }

        /// <summary>The triangles of one mesh sorted into a coarse grid of the stage, so a point is tested only against its own cell's.</summary>
        private sealed class Bins
        {
            private const int Cols = 64, Rows = 36;
            private const double X0 = -0.05, X1 = 1.05, Y0 = -0.25, Y1 = 1.05;
            private readonly System.Collections.Generic.List<int>[] _cells = new System.Collections.Generic.List<int>[Cols * Rows];
            private static readonly int[] None = System.Array.Empty<int>();

            public Bins(SceneryMesh mesh)
            {
                SceneryVertex[] v = mesh.Vertices;
                int[] t = mesh.Triangles;
                for (int k = 0; k + 2 < t.Length; k += 3)
                {
                    SceneryVertex a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                    int c0 = Col(System.Math.Min(a.X, System.Math.Min(b.X, c.X))), c1 = Col(System.Math.Max(a.X, System.Math.Max(b.X, c.X)));
                    int r0 = Row(System.Math.Min(a.Y, System.Math.Min(b.Y, c.Y))), r1 = Row(System.Math.Max(a.Y, System.Math.Max(b.Y, c.Y)));
                    for (int r = r0; r <= r1; r++)
                    {
                        for (int col = c0; col <= c1; col++)
                        {
                            int i = r * Cols + col;
                            (_cells[i] ??= new System.Collections.Generic.List<int>()).Add(k);
                        }
                    }
                }
            }

            /// <summary>The triangles (their first index) whose box covers (x, y)'s cell, in drawing order.</summary>
            public System.Collections.Generic.IEnumerable<int> At(double x, double y)
            {
                return (System.Collections.Generic.IEnumerable<int>)_cells[Row(y) * Cols + Col(x)] ?? None;
            }

            private static int Col(double x) => (int)Clamp(System.Math.Floor((x - X0) / (X1 - X0) * Cols), 0, Cols - 1);
            private static int Row(double y) => (int)Clamp(System.Math.Floor((y - Y0) / (Y1 - Y0) * Rows), 0, Rows - 1);
        }

        /// <summary>Where the side p → q crosses height y.</summary>
        private static void Cross(SceneryVertex p, SceneryVertex q, float y, System.Collections.Generic.List<(double X, double Y)> cut)
        {
            float lo = System.Math.Min(p.Y, q.Y), hi = System.Math.Max(p.Y, q.Y);
            if (y < lo || y > hi || hi <= lo || cut.Count >= 2) return;
            double t = (y - p.Y) / (double)(q.Y - p.Y);
            cut.Add((p.X + (q.X - p.X) * t, y));
        }
    }
}
