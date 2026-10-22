// A colour with straight (non-premultiplied) alpha, sRGB channels 0..1: what CSS colour values mean.
using System;
using System.Globalization;

namespace Trial.Geometry
{
    public readonly struct Rgba : IEquatable<Rgba>
    {
        public readonly float R, G, B, A;

        public Rgba(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }

        public static readonly Rgba Transparent = new Rgba(0f, 0f, 0f, 0f);
        public static readonly Rgba White = new Rgba(1f, 1f, 1f, 1f);
        public static readonly Rgba Black = new Rgba(0f, 0f, 0f, 1f);

        /// <summary>0xRRGGBB plus an alpha 0..1.</summary>
        public static Rgba Hex(int rrggbb, float a = 1f) =>
            new Rgba(((rrggbb >> 16) & 0xff) / 255f, ((rrggbb >> 8) & 0xff) / 255f, (rrggbb & 0xff) / 255f, a);

        /// <summary>CSS rgb(r, g, b) / rgba(r, g, b, a): bytes 0..255 and an alpha 0..1.</summary>
        public static Rgba Bytes(int r, int g, int b, float a = 1f) => new Rgba(r / 255f, g / 255f, b / 255f, a);

        public Rgba WithAlpha(float a) => new Rgba(R, G, B, a);
        public Rgba MulAlpha(float k) => new Rgba(R, G, B, A * k);

        /// <summary>
        /// CSS gradient interpolation: in premultiplied sRGB, so a fade to `transparent` (black, alpha 0) keeps the
        /// hue of the opaque end. A result with alpha 0 is transparent black.
        /// </summary>
        public static Rgba LerpPremul(Rgba a, Rgba b, float t)
        {
            float pa = a.A, pb = b.A;
            float alpha = pa + (pb - pa) * t;
            if (alpha <= 0f) return Transparent;
            float r = (a.R * pa + (b.R * pb - a.R * pa) * t) / alpha;
            float g = (a.G * pa + (b.G * pb - a.G * pa) * t) / alpha;
            float bl = (a.B * pa + (b.B * pb - a.B * pa) * t) / alpha;
            return new Rgba(r, g, bl, alpha);
        }

        /// <summary>Plain per-channel lerp of straight values (not what CSS gradients do; for tweens of solid colours).</summary>
        public static Rgba LerpStraight(Rgba a, Rgba b, float t) =>
            new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);

        /// <summary>0..255 byte per channel, rounded like Chrome (round half up).</summary>
        public int R8 => To8(R);
        public int G8 => To8(G);
        public int B8 => To8(B);
        public int A8 => To8(A);
        private static int To8(float v) => (int)Math.Floor(Math.Max(0f, Math.Min(1f, v)) * 255f + 0.5f);

        /// <summary>True when every channel is within eps (0..1 units).</summary>
        public bool Approximately(Rgba o, float eps = 1f / 512f) =>
            Math.Abs(R - o.R) <= eps && Math.Abs(G - o.G) <= eps && Math.Abs(B - o.B) <= eps && Math.Abs(A - o.A) <= eps;

        public bool Equals(Rgba o) => R == o.R && G == o.G && B == o.B && A == o.A;
        public override bool Equals(object obj) => obj is Rgba c && Equals(c);
        public override int GetHashCode() => ((R.GetHashCode() * 397 ^ G.GetHashCode()) * 397 ^ B.GetHashCode()) * 397 ^ A.GetHashCode();
        public static bool operator ==(Rgba a, Rgba b) => a.Equals(b);
        public static bool operator !=(Rgba a, Rgba b) => !a.Equals(b);
        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "rgba({0}, {1}, {2}, {3:0.###})", R8, G8, B8, A);
    }
}
