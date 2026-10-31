// SVG paths as a graphic (kit.md §4.6, §2.2, §2.15): an <svg width height viewBox> holding groups of <path>s, each
// group with its own transform, fill (a colour or a gradient paint), stroke (width, caps, joins, dashes, dash offset,
// non-scaling) and an optional filter: drop-shadow(). Icons (the DS icon(), explore's ico()) and figures (the DS
// Figure()) are presets of it.
//
// Coordinates: a layer's path is in user units; its Transform (the SVG group transform) maps it into the viewBox, and
// the viewBox maps into the graphic's box (preserveAspectRatio xMidYMid meet). No viewBox = user units are box px.
// Drop-shadow blur b is σ = b in box px (measured, kit.md §3.2), drawn under the layer.
//
//   PathGraphic.Icon(parent, "sword", 24, DsTokens.Ink)                 // an icon at (0, 0), placed by the caller
//   var g = PathGraphic.Make(parent, x, y, w, h); g.ViewBox = new RectF(0, 0, 1920, 1080);
//   g.AddStroke("M10 10L400 300", DsTokens.Amber, 4, Cap.Round, dash: new[] { 2f, 22f });  g.DashOffset = 24;
using System;
using System.Collections.Generic;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>One SVG group of paths: shapes, a transform, a fill, a stroke, an optional drop-shadow.</summary>
    public sealed class PathLayer
    {
        /// <summary>The paths of the group (each a `d`), parsed once.</summary>
        public List<PathData> Paths = new List<PathData>();

        /// <summary>The group's transform (user units → viewBox units), e.g. translate(dx dy).</summary>
        public Affine Transform = Affine.Identity;

        /// <summary>fill (in user units for a gradient: userSpaceOnUse); null = fill none.</summary>
        public Paint Fill;

        /// <summary>stroke; null = stroke none.</summary>
        public Paint Stroke;

        /// <summary>stroke-width, caps, joins, dasharray, dashoffset, non-scaling (user units unless NonScaling).</summary>
        public StrokeStyle StrokeStyle = new StrokeStyle(1f);

        /// <summary>filter: drop-shadow(x y blur colour) of the group (σ = blur, box px), or null.</summary>
        public DropShadow? Shadow;

        /// <summary>Anti-alias the fill's outline (off where a stroke covers the edge anyway).</summary>
        public bool FillFringe = true;

        /// <summary>The layer is built (display).</summary>
        public bool Visible = true;

        public PathLayer() { }

        public PathLayer(params string[] d)
        {
            if (d != null) foreach (string s in d) if (!string.IsNullOrEmpty(s)) Paths.Add(PathData.Cached(s));
        }

        public PathLayer WithFill(Rgba c) { Fill = new SolidPaint(c); return this; }
        public PathLayer WithFill(Paint p) { Fill = p; return this; }

        public PathLayer WithStroke(Rgba c, float width, Cap cap = Cap.Butt, Join join = Join.Miter, float[] dash = null, float dashOffset = 0f)
        {
            Stroke = new SolidPaint(c);
            StrokeStyle = new StrokeStyle(width, cap, join, dash, dashOffset);
            return this;
        }

        public PathLayer WithTransform(Affine m) { Transform = m; return this; }
        public PathLayer WithShadow(float x, float y, float blur, Rgba color) { Shadow = new DropShadow(x, y, blur, color); return this; }
    }

    [AddComponentMenu("")]
    public sealed class PathGraphic : VectorGraphic
    {
        private enum Mode { Layers, Icon, Figure }

        private Mode _mode = Mode.Layers;
        private RectF? _viewBox;
        private float _dashOffset = float.NaN;

        // icon
        private IconDef _icon;
        private Rgba _iconColor = Rgba.White;
        private Rgba? _iconFill;
        private float? _iconWidth;
        private bool _iconDashed;
        private DropShadow? _iconShadow;

        // figure
        private FigureDef _figure;
        private Rgba _figureFill, _figureRim;

        /// <summary>The groups, painted in order (the first lowest).</summary>
        public List<PathLayer> Layers { get; } = new List<PathLayer>();

        /// <summary>The SVG viewBox (null = none: user units are box px).</summary>
        public RectF? ViewBox
        {
            get => _viewBox;
            set { _viewBox = value; SetGeometryDirty(); }
        }

        /// <summary>
        /// stroke-dashoffset of every dashed stroke, overriding the layers' own (NaN = each layer's own). The
        /// explore edge flow (@keyframes ocbg-explore-flow) and the edge draw-in animate it.
        /// </summary>
        public float DashOffset
        {
            get => _dashOffset;
            set
            {
                if (_dashOffset.Equals(value)) return;
                _dashOffset = value;
                SetGeometryDirty();
            }
        }

        /// <summary>A new, empty path graphic placed at (x, y) with the box w × h.</summary>
        public static PathGraphic Make(RectTransform parent, float x, float y, float w, float h, string name = null) =>
            Make<PathGraphic>(parent, x, y, w, h, name ?? "path");

        /// <summary>The user units → box px map of a layer (its transform, then the viewBox).</summary>
        public Affine MapOf(PathLayer layer, RectF box)
        {
            Affine vb = _viewBox.HasValue ? Affine.ViewBox(_viewBox.Value, box.W, box.H) : Affine.Identity;
            return (layer?.Transform ?? Affine.Identity).Then(vb);
        }

        /// <summary>Adds a group and rebuilds.</summary>
        public PathLayer Add(PathLayer layer)
        {
            if (layer == null) throw new ArgumentNullException(nameof(layer));
            _mode = Mode.Layers;
            Layers.Add(layer);
            BuildTint = Rgba.White;
            SetGeometryDirty();
            return layer;
        }

        /// <summary>A filled path (fill-opacity folded into the colour's alpha).</summary>
        public PathLayer AddFill(string d, Rgba fill) => Add(new PathLayer(d).WithFill(fill));

        /// <summary>A stroked path.</summary>
        public PathLayer AddStroke(string d, Rgba stroke, float width, Cap cap = Cap.Butt, Join join = Join.Miter, float[] dash = null, float dashOffset = 0f) =>
            Add(new PathLayer(d).WithStroke(stroke, width, cap, join, dash, dashOffset));

        /// <summary>Removes every group (and the icon / figure).</summary>
        public void Clear()
        {
            Layers.Clear();
            _mode = Mode.Layers;
            _icon = null;
            _figure = null;
            BuildTint = Rgba.White;
            SetGeometryDirty();
        }

        /// <summary>Call after changing a layer in place.</summary>
        public void LayersChanged() => SetGeometryDirty();

        // ---- icons ---------------------------------------------------------------------------------------------

        // js: vendor/ds/bundle.js:113
        /// <summary>
        /// icon(name, size, color, { fill, fillOpacity, dashed, width }) of the DS (an unknown name draws `plus`), as a
        /// size × size graphic at (0, 0) of parent (size 0 = 24, as `size || 24`). fillOpacity is the alpha of `fill`.
        /// </summary>
        public static PathGraphic Icon(RectTransform parent, string name, float size, Rgba color, Rgba? fill = null, float? width = null, bool dashed = false)
        {
            size = IconSize(size);
            PathGraphic g = Make(parent, 0f, 0f, size, size, "icon " + name);
            g.SetIcon(DsIcons.Get(name), color, fill, width, dashed);
            return g;
        }

        /// <summary>An icon from any table (DsIcons.Get, ExploreIcons.Get, ExtraIcons.Get) at (0, 0) of parent (size 0 = 24).</summary>
        public static PathGraphic Icon(RectTransform parent, IconDef icon, float size, Rgba color, Rgba? fill = null, float? width = null, bool dashed = false)
        {
            size = IconSize(size);
            PathGraphic g = Make(parent, 0f, 0f, size, size, "icon " + (icon?.Name ?? "?"));
            g.SetIcon(icon, color, fill, width, dashed);
            return g;
        }

        // js: vendor/ds/bundle.js:115 (`size = size || 24`: 0, NaN and a missing size draw 24 px)
        private static float IconSize(float size) => size > 0f || size < 0f ? size : 24f;

        /// <summary>
        /// Draws an icon in the box (its size = the box's smaller side, centred: the svg's viewBox meet). Without a fill
        /// the shape is built in white and the colour is a tint, so IconColor changes cost a recolour only.
        /// </summary>
        public void SetIcon(IconDef icon, Rgba color, Rgba? fill = null, float? width = null, bool dashed = false, DropShadow? shadow = null)
        {
            _mode = Mode.Icon;
            _icon = icon;
            _iconColor = color;
            _iconFill = fill;
            _iconWidth = width;
            _iconDashed = dashed;
            _iconShadow = shadow;
            BuildTint = Monochrome ? color : Rgba.White;
            SetGeometryDirty();
        }

        private bool Monochrome => _mode == Mode.Icon && (_icon == null || _icon.Filled || !_iconFill.HasValue) && !_iconShadow.HasValue;

        /// <summary>The icon's colour (stroke, or the fill of a filled icon).</summary>
        public Rgba IconColor
        {
            get => _iconColor;
            set
            {
                if (_iconColor.Equals(value)) return;
                _iconColor = value;
                if (_mode == Mode.Icon && Monochrome) BuildTint = value;
                else SetGeometryDirty();
            }
        }

        // ---- figures -------------------------------------------------------------------------------------------

        // js: vendor/ds/bundle.js:641
        /// <summary>
        /// The DS Figure(kind, { fill, rim, scaleTo }): the rim group (filled and stroked 4 user units, round joins) under
        /// the fill group, fitted to the box (the box is the svg: w × h of the figure, or scaleTo tall).
        /// </summary>
        public void SetFigure(FigureDef figure, Rgba fill, Rgba rim)
        {
            _mode = Mode.Figure;
            _figure = figure;
            _figureFill = fill;
            _figureRim = rim;
            BuildTint = Rgba.White;
            SetGeometryDirty();
        }

        /// <summary>A DS figure graphic at (0, 0) of parent, scaleTo px tall (0 = its natural size).</summary>
        public static PathGraphic Figure(RectTransform parent, string kind, float scaleTo = 0f, Rgba? fill = null, Rgba? rim = null)
        {
            FigureDef f = DsFigures.Get(kind);
            float h = scaleTo > 0f ? scaleTo : f.H;
            (float w, float hh) = f.SizeFor(h);
            PathGraphic g = Make(parent, 0f, 0f, w, hh, "figure " + f.Name);
            g.SetFigure(f, fill ?? (f.IsFoe ? DsTokens.FigureEnemy : DsTokens.FigureSelf), rim ?? f.DefaultRim);
            return g;
        }

        // ---- building ------------------------------------------------------------------------------------------

        protected override void BuildMesh(MeshBuilder mb, RectF box, float feather)
        {
            switch (_mode)
            {
                case Mode.Icon:
                    BuildIcon(mb, box, feather);
                    break;
                case Mode.Figure:
                    if (_figure != null) IconMesh.Figure(_figure, box.H, _figureFill, _figureRim, feather, mb);
                    break;
                default:
                    foreach (PathLayer l in Layers)
                        if (l != null && l.Visible) BuildLayer(l, MapOf(l, box), feather, mb, _dashOffset);
                    break;
            }
        }

        private void BuildIcon(MeshBuilder mb, RectF box, float feather)
        {
            if (_icon == null) return;
            float size = Math.Min(box.W, box.H);
            if (!(size > 0f)) return;
            var tmp = new MeshBuilder();
            bool mono = Monochrome;
            if (_iconShadow.HasValue && !_icon.Filled)
            {
                // the shadow of the stroke, under the icon
                Affine m = _icon.ToBox(size);
                StrokeStyle st = _icon.Style(_iconWidth, _iconDashed);
                foreach (Polyline line in Flatten.Run(_icon.Path, m))
                    StrokeShadow(line, st, m, _iconShadow.Value, tmp);
            }
            IconMesh.Build(_icon, size, mono ? Rgba.White : _iconColor, feather, tmp, mono ? null : _iconFill, _iconWidth, _iconDashed);
            if (box.W != size || box.H != size) tmp.Transform(Affine.Translate((box.W - size) * 0.5f, (box.H - size) * 0.5f));
            mb.Append(tmp);
        }

        /// <summary>One group into mb: drop-shadow, then fill, then stroke (SVG paint order).</summary>
        public static void BuildLayer(PathLayer l, Affine m, float feather, MeshBuilder mb, float dashOffsetOverride = float.NaN)
        {
            if (l.Paths.Count == 0) return;
            var lines = new List<Polyline>();
            foreach (PathData p in l.Paths)
                if (p != null) lines.AddRange(Flatten.Run(p, m));
            StrokeStyle st = l.StrokeStyle;
            if (st != null && !float.IsNaN(dashOffsetOverride) && st.Dash != null && st.Dash.Length > 0)
            {
                st = st.Clone();
                st.DashOffset = dashOffsetOverride;
            }

            if (l.Shadow.HasValue)
            {
                DropShadow ds = l.Shadow.Value;
                if (l.Fill != null)
                    foreach (Polyline line in lines) FillShadow(line, ds, mb);
                if (l.Stroke != null && st != null)
                    foreach (Polyline line in lines) StrokeShadow(line, st, m, ds, mb);
            }
            if (l.Fill != null)
            {
                Paint fill = Transformed(l.Fill, m);
                foreach (Polyline line in lines)
                {
                    if (line.Count < 3) continue;
                    var ring = new Polyline(line.Points, true);
                    if (l.FillFringe) Triangulate.FillAA(ring, mb, fill, feather);
                    else Triangulate.Fill(ring, mb, fill);
                }
            }
            if (l.Stroke != null && st != null)
            {
                Paint stroke = Transformed(l.Stroke, m);
                foreach (Polyline line in lines) Stroker.Stroke(line, st, m, feather, mb, stroke);
            }
        }

        /// <summary>filter: drop-shadow(x y b c) of a stroke: a soft band under it, per dash (σ = b, box px).</summary>
        private static void StrokeShadow(Polyline line, StrokeStyle st, Affine m, DropShadow ds, MeshBuilder mb)
        {
            if (ds.Color.A <= 0f) return;
            float k = st.NonScaling ? 1f : m.MeanScale;
            float w = st.Width * k;
            float sigma = Math.Max(0f, ds.Blur);
            Polyline moved = Offset(line, ds.X, ds.Y);
            var pieces = new List<Polyline>();
            if (st.Dash != null && st.Dash.Length > 0)
            {
                var pat = new float[st.Dash.Length];
                for (int i = 0; i < pat.Length; i++) pat[i] = st.Dash[i] * k;
                pieces.AddRange(Stroker.Dash(moved, pat, st.DashOffset * k));
            }
            else pieces.Add(moved);
            if (sigma <= 0f)
            {
                var hard = new StrokeStyle(w, st.Cap, st.Join) { NonScaling = true };
                foreach (Polyline p in pieces) Stroker.Stroke(p, hard, Affine.Identity, 0f, mb, new SolidPaint(ds.Color));
                return;
            }
            Func<float, float> profile = Blur.StrokeShadowProfile(w, sigma, 1f);
            float reach = Blur.StrokeShadowReach(w, sigma);
            foreach (Polyline p in pieces) Stroker.Soft(p, profile, reach, Affine.Identity, mb, ds.Color);
        }

        /// <summary>
        /// filter: drop-shadow of a filled shape: the outward half of the blurred edge as a soft band along the outline
        /// (kit.md §4.4), under the fill.
        /// </summary>
        private static void FillShadow(Polyline line, DropShadow ds, MeshBuilder mb)
        {
            if (ds.Color.A <= 0f || line.Count < 3) return;
            float sigma = Math.Max(0f, ds.Blur);
            if (sigma <= 0f) return;
            var ring = Offset(new Polyline(line.Points, true), ds.X, ds.Y);
            // Stroker.Soft's d > 0 is inside a ring of positive signed area (clockwise on the screen, y down).
            float inward = ring.SignedArea >= 0f ? 1f : -1f;
            Stroker.Soft(ring, d => { float o = -d * inward; return o < 0f ? 0f : Blur.EdgeAlpha(o, sigma); }, 3f * sigma, Affine.Identity, mb, ds.Color);
        }

        private static Polyline Offset(Polyline l, float dx, float dy)
        {
            if (dx == 0f && dy == 0f) return l;
            var pts = new List<Vec2>(l.Points.Count);
            foreach (Vec2 p in l.Points) pts.Add(new Vec2(p.X + dx, p.Y + dy));
            return new Polyline(pts, l.Closed);
        }

        /// <summary>A paint given in user units, evaluated in box px (userSpaceOnUse gradients under the viewBox map).</summary>
        public static Paint Transformed(Paint p, Affine userToBox)
        {
            if (p == null || p.IsSolid || userToBox.IsIdentity) return p;
            return new TransformedPaint(p, userToBox);
        }
    }

    /// <summary>A paint defined in another coordinate space: At and the isolines go through the inverse map.</summary>
    public sealed class TransformedPaint : Paint
    {
        private readonly Paint _inner;
        private readonly Affine _toInner;

        /// <param name="inner">The paint in its own space.</param>
        /// <param name="innerToOuter">Its space → the mesh's space.</param>
        public TransformedPaint(Paint inner, Affine innerToOuter)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _toInner = innerToOuter.Inverse();
        }

        public override Rgba At(Vec2 p) => _inner.At(_toInner.Apply(p));
        public override bool IsSolid => _inner.IsSolid;
        public override bool NeedsRefine => _inner.NeedsRefine;

        public override IEnumerable<IsoLine> Bands(RectF bounds)
        {
            Vec2 a = _toInner.Apply(bounds.TopLeft), b = _toInner.Apply(bounds.TopRight);
            Vec2 c = _toInner.Apply(bounds.BottomRight), d = _toInner.Apply(bounds.BottomLeft);
            RectF inner = RectF.Bounds(new[] { a, b, c, d });
            IEnumerable<IsoLine> lines = _inner.Bands(inner);
            if (lines == null) return null;
            // N·u = C with u = L·p + t (the inverse map) ⇔ (Lᵀ N)·p = C − N·t
            Affine m = _toInner;
            var o = new List<IsoLine>();
            foreach (IsoLine l in lines)
            {
                var n = new Vec2(m.A * l.N.X + m.B * l.N.Y, m.C * l.N.X + m.D * l.N.Y);
                o.Add(new IsoLine(n, l.C - (l.N.X * m.E + l.N.Y * m.F)));
            }
            return o;
        }
    }
}
