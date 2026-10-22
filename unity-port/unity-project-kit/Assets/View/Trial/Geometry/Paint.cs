// What colours a mesh vertex: the abstract Paint, the isolines a linear paint is split along, and a solid colour.
// LinearGradient / RadialGradient live in Gradients.cs.
using System.Collections.Generic;

namespace Trial.Geometry
{
    /// <summary>
    /// The straight line { p : Dot(N, p) = C } (N need not be unit length). Linear gradients report one per stop
    /// (and per repeat) so <see cref="MeshBuilder.SplitByLines"/> can cut a mesh where the colour has a kink; between
    /// two isolines a linear gradient is linear, so vertex colours interpolate it exactly.
    /// </summary>
    public readonly struct IsoLine
    {
        public readonly Vec2 N;
        public readonly float C;

        public IsoLine(Vec2 n, float c) { N = n; C = c; }

        /// <summary>The line through <paramref name="point"/> perpendicular to <paramref name="normal"/>.</summary>
        public static IsoLine Through(Vec2 point, Vec2 normal) => new IsoLine(normal, Vec2.Dot(normal, point));

        /// <summary>Signed side: Dot(N, p) − C.</summary>
        public float Side(Vec2 p) => N.X * p.X + N.Y * p.Y - C;
    }

    public abstract class Paint
    {
        /// <summary>The colour at a point (in the coordinates the mesh is built in).</summary>
        public abstract Rgba At(Vec2 p);

        /// <summary>Where the colour has kinks inside <paramref name="bounds"/> (linear gradients); null = none / not linear.</summary>
        public virtual IEnumerable<IsoLine> Bands(RectF bounds) => null;

        /// <summary>True when At returns the same colour everywhere (no splitting or refining needed).</summary>
        public virtual bool IsSolid => false;

        /// <summary>
        /// True when the colour is not piecewise linear between <see cref="Bands"/> (radial gradients): the mesh has
        /// to be refined (<see cref="MeshBuilder.Refine"/>) before vertex colours approximate it.
        /// </summary>
        public virtual bool NeedsRefine => false;

        public static SolidPaint Solid(Rgba c) => new SolidPaint(c);
    }

    public sealed class SolidPaint : Paint
    {
        public readonly Rgba Color;

        public SolidPaint(Rgba color) { Color = color; }

        public override Rgba At(Vec2 p) => Color;
        public override bool IsSolid => true;
    }
}
