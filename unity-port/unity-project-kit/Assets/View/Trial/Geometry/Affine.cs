// A 2×3 affine transform in y-down stage px, laid out like SVG matrix(a b c d e f):
//   x' = A·x + C·y + E,   y' = B·x + D·y + F.
using System;
using System.Globalization;

namespace Trial.Geometry
{
    public readonly struct Affine : IEquatable<Affine>
    {
        public readonly float A, B, C, D, E, F;

        public Affine(float a, float b, float c, float d, float e, float f) { A = a; B = b; C = c; D = d; E = e; F = f; }

        public static readonly Affine Identity = new Affine(1f, 0f, 0f, 1f, 0f, 0f);

        public static Affine Translate(float x, float y) => new Affine(1f, 0f, 0f, 1f, x, y);
        public static Affine Translate(Vec2 d) => new Affine(1f, 0f, 0f, 1f, d.X, d.Y);
        public static Affine Scale(float sx, float sy) => new Affine(sx, 0f, 0f, sy, 0f, 0f);
        public static Affine Scale(float s) => new Affine(s, 0f, 0f, s, 0f, 0f);

        /// <summary>CSS / SVG rotate(deg): clockwise on a y-down screen (matrix(cos, sin, −sin, cos, 0, 0)).</summary>
        public static Affine RotateCss(float deg)
        {
            double r = deg * Math.PI / 180.0;
            float c = (float)Math.Cos(r), s = (float)Math.Sin(r);
            return new Affine(c, s, -s, c, 0f, 0f);
        }

        /// <summary>Scale about a centre (SVG "translate(cx cy) scale(s) translate(−cx −cy)").</summary>
        public static Affine ScaleAbout(float s, Vec2 centre) =>
            Translate(-centre.X, -centre.Y).Then(Scale(s)).Then(Translate(centre.X, centre.Y));

        /// <summary>
        /// The viewBox → element box map for preserveAspectRatio="xMidYMid meet" (the SVG default): uniform scale
        /// min(w / vb.W, h / vb.H), centred in the w×h box whose top-left is the origin.
        /// </summary>
        public static Affine ViewBox(RectF viewBox, float w, float h)
        {
            if (viewBox.W <= 0f || viewBox.H <= 0f) return Identity;
            float s = Math.Min(w / viewBox.W, h / viewBox.H);
            float tx = (w - viewBox.W * s) * 0.5f - viewBox.X * s;
            float ty = (h - viewBox.H * s) * 0.5f - viewBox.Y * s;
            return new Affine(s, 0f, 0f, s, tx, ty);
        }

        /// <summary>This transform first, then <paramref name="next"/> (result = next ∘ this).</summary>
        public Affine Then(Affine next) => new Affine(
            next.A * A + next.C * B,
            next.B * A + next.D * B,
            next.A * C + next.C * D,
            next.B * C + next.D * D,
            next.A * E + next.C * F + next.E,
            next.B * E + next.D * F + next.F);

        public Vec2 Apply(Vec2 p) => new Vec2(A * p.X + C * p.Y + E, B * p.X + D * p.Y + F);
        public Vec2 Apply(float x, float y) => new Vec2(A * x + C * y + E, B * x + D * y + F);
        /// <summary>The linear part only (no translation): for directions and offsets.</summary>
        public Vec2 ApplyVector(Vec2 v) => new Vec2(A * v.X + C * v.Y, B * v.X + D * v.Y);

        public float Determinant => A * D - B * C;
        /// <summary>sqrt(|det|): the length scale a uniform stroke width or dash length goes through.</summary>
        public float MeanScale => (float)Math.Sqrt(Math.Abs(A * D - B * C));
        public bool IsIdentity => A == 1f && B == 0f && C == 0f && D == 1f && E == 0f && F == 0f;

        public Affine Inverse()
        {
            float det = A * D - B * C;
            if (det == 0f) return Identity;
            float id = 1f / det;
            float a = D * id, b = -B * id, c = -C * id, d = A * id;
            return new Affine(a, b, c, d, -(a * E + c * F), -(b * E + d * F));
        }

        public bool Equals(Affine o) => A == o.A && B == o.B && C == o.C && D == o.D && E == o.E && F == o.F;
        public override bool Equals(object obj) => obj is Affine m && Equals(m);
        public override int GetHashCode() => (((A.GetHashCode() * 397 ^ B.GetHashCode()) * 397 ^ C.GetHashCode()) * 397 ^ D.GetHashCode()) * 397 ^ E.GetHashCode() ^ F.GetHashCode() * 31;
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "matrix({0:0.####} {1:0.####} {2:0.####} {3:0.####} {4:0.###} {5:0.###})", A, B, C, D, E, F);
    }
}
