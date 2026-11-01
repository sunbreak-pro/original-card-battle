// A 2-D vector in stage px (y down). Engine-free stand-in for UnityEngine.Vector2.
using System;
using System.Globalization;

namespace Trial.Geometry
{
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X, Y;

        public Vec2(float x, float y) { X = x; Y = y; }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);
        public static readonly Vec2 One = new Vec2(1f, 1f);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, float k) => new Vec2(a.X * k, a.Y * k);
        public static Vec2 operator *(float k, Vec2 a) => new Vec2(a.X * k, a.Y * k);
        public static Vec2 operator /(Vec2 a, float k) => new Vec2(a.X / k, a.Y / k);
        public static bool operator ==(Vec2 a, Vec2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Vec2 a, Vec2 b) => !(a == b);

        public float Dot(Vec2 b) => X * b.X + Y * b.Y;
        /// <summary>z of the 3-D cross product (a.X·b.Y − a.Y·b.X). Positive = b is clockwise of a on a y-down screen.</summary>
        public float Cross(Vec2 b) => X * b.Y - Y * b.X;
        public float Len => (float)Math.Sqrt(X * X + Y * Y);
        public float LenSq => X * X + Y * Y;
        /// <summary>Unit vector; the zero vector stays zero.</summary>
        public Vec2 Norm
        {
            get
            {
                float l = Len;
                return l > 0f ? new Vec2(X / l, Y / l) : Zero;
            }
        }
        /// <summary>(−Y, X): rotated 90° clockwise on a y-down screen (counter-clockwise in y-up maths).</summary>
        public Vec2 Perp => new Vec2(-Y, X);

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
        public static float Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;
        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        public static float Dist(Vec2 a, Vec2 b) => (a - b).Len;
        public static Vec2 Min(Vec2 a, Vec2 b) => new Vec2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y));
        public static Vec2 Max(Vec2 a, Vec2 b) => new Vec2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();
        public override string ToString() =>
            "(" + X.ToString("0.###", CultureInfo.InvariantCulture) + ", " + Y.ToString("0.###", CultureInfo.InvariantCulture) + ")";
    }
}
