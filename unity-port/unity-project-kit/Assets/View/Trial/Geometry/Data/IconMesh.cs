// Whole icons and figures as meshes, painted the way the JS builds their SVG (fill, then stroke; the rim group
// under the fill group). Coordinates: the svg box, origin at its top-left, in output px.
using System.Collections.Generic;

namespace Trial.Geometry
{
    public static class IconMesh
    {
        /// <summary>
        /// js: vendor/ds/bundle.js:113 icon() / scenes/explore/icons.js:47 ico(): a size×size icon. A filled icon is
        /// its path filled in `color` (the options are ignored); otherwise the optional `fill` (fill-opacity folded
        /// into its alpha) and then the stroke in `color`, `width` viewBox units (2.2) with round caps and joins,
        /// dashed 2.4 2.4 when asked. Feather = one screen pixel in output px.
        /// </summary>
        public static void Build(IconDef icon, float size, Rgba color, float feather, MeshBuilder mb,
            Rgba? fill = null, float? width = null, bool dashed = false, float tolerancePx = Flatten.DefaultTolerancePx)
        {
            if (icon == null || mb == null) return;
            Affine m = icon.ToBox(size);
            List<Polyline> lines = Flatten.Run(icon.Path, m, tolerancePx);
            if (icon.Filled)
            {
                var paint = new SolidPaint(color);
                foreach (Polyline l in lines) Triangulate.FillAA(l, mb, paint, feather);
                return;
            }
            if (fill.HasValue)
            {
                var fp = new SolidPaint(fill.Value);
                foreach (Polyline l in lines) Triangulate.FillAA(l, mb, fp, feather);
            }
            StrokeStyle style = icon.Style(width, dashed);
            var sp = new SolidPaint(color);
            foreach (Polyline l in lines) Stroker.Stroke(l, style, m, feather, mb, sp);
        }

        /// <summary>
        /// js: vendor/ds/bundle.js:641 Figure(): the rim group (each part filled and stroked 4 user units with round
        /// joins in `rim`) under the fill group (each part filled in `fill`). `height` = the svg height (scaleTo;
        /// 0 or NaN gives the natural size f.W × f.H, as the JS `if (o.scaleTo)`); the width follows w·height/h.
        /// </summary>
        public static void Figure(FigureDef f, float height, Rgba fill, Rgba rim, float feather, MeshBuilder mb,
            float tolerancePx = Flatten.DefaultTolerancePx)
        {
            if (f == null || mb == null) return;
            if (height == 0f || float.IsNaN(height)) height = f.H;
            (float w, float h) = f.SizeFor(height);
            Affine m = Affine.ViewBox(f.ViewBox, w, h);
            var parts = new List<List<Polyline>>(f.Paths.Count);
            foreach (string d in f.Paths) parts.Add(Flatten.Run(PathData.Cached(d), m, tolerancePx));
            if (rim.A > 0f)
            {
                var rp = new SolidPaint(rim);
                var rs = new StrokeStyle(DsFigures.RimStrokeWidth, Cap.Butt, Join.Round);
                foreach (List<Polyline> part in parts)
                    foreach (Polyline l in part)
                    {
                        Triangulate.FillAA(l, mb, rp, feather);
                        Stroker.Stroke(l, rs, m, feather, mb, rp);
                    }
            }
            var fp = new SolidPaint(fill);
            foreach (List<Polyline> part in parts)
                foreach (Polyline l in part) Triangulate.FillAA(l, mb, fp, feather);
        }
    }
}
