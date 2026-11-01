// A rectangle in y-down stage px: (X, Y) is the top-left corner.
using System;
using System.Globalization;

namespace Trial.Geometry
{
    public readonly struct RectF : IEquatable<RectF>
    {
        public readonly float X, Y, W, H;

        public RectF(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }

        public static readonly RectF Empty = new RectF(0f, 0f, 0f, 0f);

        public static RectF FromLtrb(float left, float top, float right, float bottom) =>
            new RectF(left, top, right - left, bottom - top);

        public float Left => X;
        public float Top => Y;
        public float Right => X + W;
        public float Bottom => Y + H;
        public Vec2 Min => new Vec2(X, Y);
        public Vec2 Max => new Vec2(X + W, Y + H);
        public Vec2 Center => new Vec2(X + W * 0.5f, Y + H * 0.5f);
        public Vec2 Size => new Vec2(W, H);
        public Vec2 TopLeft => new Vec2(X, Y);
        public Vec2 TopRight => new Vec2(X + W, Y);
        public Vec2 BottomRight => new Vec2(X + W, Y + H);
        public Vec2 BottomLeft => new Vec2(X, Y + H);
        public bool IsEmpty => W <= 0f || H <= 0f;

        public bool Contains(Vec2 p) => p.X >= X && p.X <= X + W && p.Y >= Y && p.Y <= Y + H;

        /// <summary>Grown by d on every side (negative shrinks).</summary>
        public RectF Inflate(float d) => new RectF(X - d, Y - d, W + 2f * d, H + 2f * d);
        public RectF Inflate(float dx, float dy) => new RectF(X - dx, Y - dy, W + 2f * dx, H + 2f * dy);
        /// <summary>CSS inset order: top, right, bottom, left (positive shrinks).</summary>
        public RectF Inset(float top, float right, float bottom, float left) =>
            new RectF(X + left, Y + top, W - left - right, H - top - bottom);
        public RectF Offset(float dx, float dy) => new RectF(X + dx, Y + dy, W, H);
        public RectF Offset(Vec2 d) => new RectF(X + d.X, Y + d.Y, W, H);

        public static RectF Union(RectF a, RectF b) =>
            FromLtrb(Math.Min(a.Left, b.Left), Math.Min(a.Top, b.Top), Math.Max(a.Right, b.Right), Math.Max(a.Bottom, b.Bottom));

        public static RectF Intersect(RectF a, RectF b)
        {
            float l = Math.Max(a.Left, b.Left), t = Math.Max(a.Top, b.Top);
            float r = Math.Min(a.Right, b.Right), bt = Math.Min(a.Bottom, b.Bottom);
            return r <= l || bt <= t ? new RectF(l, t, 0f, 0f) : FromLtrb(l, t, r, bt);
        }

        /// <summary>Bounds of a point set; Empty when there are none.</summary>
        public static RectF Bounds(System.Collections.Generic.IReadOnlyList<Vec2> pts)
        {
            if (pts == null || pts.Count == 0) return Empty;
            float l = pts[0].X, t = pts[0].Y, r = l, b = t;
            for (int i = 1; i < pts.Count; i++)
            {
                Vec2 p = pts[i];
                if (p.X < l) l = p.X; if (p.X > r) r = p.X;
                if (p.Y < t) t = p.Y; if (p.Y > b) b = p.Y;
            }
            return FromLtrb(l, t, r, b);
        }

        public bool Equals(RectF o) => X == o.X && Y == o.Y && W == o.W && H == o.H;
        public override bool Equals(object obj) => obj is RectF r && Equals(r);
        public override int GetHashCode() => ((X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ W.GetHashCode()) * 397 ^ H.GetHashCode();
        public static bool operator ==(RectF a, RectF b) => a.Equals(b);
        public static bool operator !=(RectF a, RectF b) => !a.Equals(b);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "[{0:0.###}, {1:0.###}, {2:0.###}×{3:0.###}]", X, Y, W, H);
    }
}
