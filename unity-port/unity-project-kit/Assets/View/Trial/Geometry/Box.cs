// The CSS box: background, border, border-radius, box-shadow and outline, meshed in CSS paint order (kit.md §4.4,
// §4.7). Measured facts used here (kit.md §3.2, Chrome 155):
//  - dashed borders: dash 3w, gap 2w below 3 px (Blink's StrokeData ratios: 2w / w from 3 px), gaps stretched so a
//    straight side starts and ends on a dash (Blink SelectBestDashGap, open path); a rounded uniform border is one
//    closed path (a 16 px circle with a 2 px dashed border shows 4 dashes);
//  - dotted: 1 px → 1 on 1 off squares; from 3 px round dots, one width apart;
//  - double 3 px: 1 px line, 1 px gap, 1 px line (below 3 px it paints solid);
//  - mixed side widths / colours split along the line from the outer to the inner corner;
//  - outline follows the border radius: radius + offset (+ width outside);
//  - box-shadow blur b → Gaussian σ = b / 2; the shadow is painted only outside the border box (outer) or inside
//    the padding box (inset); the spread changes the corner radius by the CSS spread rule.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Trial.Geometry
{
    public enum BorderStyle { None, Solid, Dashed, Dotted, Double }

    public readonly struct BorderSide : IEquatable<BorderSide>
    {
        public readonly float Width;
        public readonly BorderStyle Style;
        public readonly Rgba Color;

        public BorderSide(float width, BorderStyle style, Rgba color) { Width = width; Style = style; Color = color; }

        public static readonly BorderSide None = new BorderSide(0f, BorderStyle.None, Rgba.Transparent);

        /// <summary>The width the box model uses (0 when the style is none).</summary>
        public float Used => Style == BorderStyle.None ? 0f : Math.Max(0f, Width);
        public bool Visible => Style != BorderStyle.None && Width > 0f && Color.A > 0f;

        public BorderSide WithWidth(float w) => new BorderSide(w, Style, Color);
        public BorderSide WithStyle(BorderStyle s) => new BorderSide(Width, s, Color);
        public BorderSide WithColor(Rgba c) => new BorderSide(Width, Style, c);

        public bool Equals(BorderSide o) => Width == o.Width && Style == o.Style && Color.Equals(o.Color);
        public override bool Equals(object obj) => obj is BorderSide b && Equals(b);
        public override int GetHashCode() => Width.GetHashCode() ^ (int)Style * 31 ^ Color.GetHashCode();
        public override string ToString() =>
            Width.ToString("0.###", CultureInfo.InvariantCulture) + "px " + Style.ToString().ToLowerInvariant() + " " + CssColor.ToCss(Color);
    }

    /// <summary>Top, right, bottom, left in px (CSS order).</summary>
    public readonly struct Insets : IEquatable<Insets>
    {
        public readonly float Top, Right, Bottom, Left;

        public Insets(float top, float right, float bottom, float left) { Top = top; Right = right; Bottom = bottom; Left = left; }

        public static readonly Insets Zero = new Insets(0f, 0f, 0f, 0f);
        public static Insets All(float v) => new Insets(v, v, v, v);
        public static Insets Of(float vertical, float horizontal) => new Insets(vertical, horizontal, vertical, horizontal);

        public float Horizontal => Left + Right;
        public float Vertical => Top + Bottom;

        public static Insets operator +(Insets a, Insets b) => new Insets(a.Top + b.Top, a.Right + b.Right, a.Bottom + b.Bottom, a.Left + b.Left);

        public RectF Shrink(RectF r) => r.Inset(Top, Right, Bottom, Left);

        public bool Equals(Insets o) => Top == o.Top && Right == o.Right && Bottom == o.Bottom && Left == o.Left;
        public override bool Equals(object obj) => obj is Insets i && Equals(i);
        public override int GetHashCode() => Top.GetHashCode() ^ Right.GetHashCode() * 3 ^ Bottom.GetHashCode() * 7 ^ Left.GetHashCode() * 11;
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", Top, Right, Bottom, Left);
    }

    /// <summary>One box-shadow layer: offset, blur radius, spread, colour, inset.</summary>
    public readonly struct BoxShadow : IEquatable<BoxShadow>
    {
        public readonly float X, Y, Blur, Spread;
        public readonly Rgba Color;
        public readonly bool Inset;

        public BoxShadow(float x, float y, float blur, float spread, Rgba color, bool inset = false)
        {
            X = x; Y = y; Blur = Math.Max(0f, blur); Spread = spread; Color = color; Inset = inset;
        }

        /// <summary>The Gaussian σ (measured: blur / 2).</summary>
        public float Sigma => Blur * 0.5f;

        public BoxShadow WithColor(Rgba c) => new BoxShadow(X, Y, Blur, Spread, c, Inset);

        /// <summary>CSS interpolation of one layer: lengths linearly, the colour premultiplied (inset must match).</summary>
        public static BoxShadow Lerp(BoxShadow a, BoxShadow b, float t) => new BoxShadow(
            a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Blur + (b.Blur - a.Blur) * t, a.Spread + (b.Spread - a.Spread) * t,
            GradientPaint.LerpPremulKeep(a.Color, b.Color, t), a.Inset);

        /// <summary>The "none" partner a list is padded with when two shadow lists of different lengths interpolate.</summary>
        public static BoxShadow Zero(bool inset) => new BoxShadow(0f, 0f, 0f, 0f, Rgba.Transparent, inset);

        public string ToCss() =>
            (Inset ? "inset " : "") + F(X) + "px " + F(Y) + "px " + F(Blur) + "px " + F(Spread) + "px " + CssColor.ToCss(Color);

        public static string ToCss(IReadOnlyList<BoxShadow> list)
        {
            if (list == null || list.Count == 0) return "none";
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(list[i].ToCss());
            }
            return sb.ToString();
        }

        private static string F(float v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);

        public bool Equals(BoxShadow o) =>
            Math.Abs(X - o.X) < 1e-4f && Math.Abs(Y - o.Y) < 1e-4f && Math.Abs(Blur - o.Blur) < 1e-4f && Math.Abs(Spread - o.Spread) < 1e-4f &&
            Color.Approximately(o.Color, 1f / 1024f) && Inset == o.Inset;
        public override bool Equals(object obj) => obj is BoxShadow s && Equals(s);
        public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode() * 3 ^ Blur.GetHashCode() * 7 ^ Spread.GetHashCode() * 11 ^ Inset.GetHashCode();
        public override string ToString() => ToCss();
    }

    /// <summary>outline: width, style, colour, offset.</summary>
    public readonly struct Outline : IEquatable<Outline>
    {
        public readonly float Width;
        public readonly BorderStyle Style;
        public readonly Rgba Color;
        public readonly float Offset;

        public Outline(float width, BorderStyle style, Rgba color, float offset = 0f) { Width = width; Style = style; Color = color; Offset = offset; }

        public static readonly Outline None = new Outline(0f, BorderStyle.None, Rgba.Transparent);

        public bool Visible => Style != BorderStyle.None && Width > 0f && Color.A > 0f;

        public Outline WithWidth(float w) => new Outline(w, Style, Color, Offset);
        public Outline WithStyle(BorderStyle s) => new Outline(Width, s, Color, Offset);
        public Outline WithColor(Rgba c) => new Outline(Width, Style, c, Offset);
        public Outline WithOffset(float o) => new Outline(Width, Style, Color, o);

        public bool Equals(Outline o) => Width == o.Width && Style == o.Style && Color.Equals(o.Color) && Offset == o.Offset;
        public override bool Equals(object obj) => obj is Outline o && Equals(o);
        public override int GetHashCode() => Width.GetHashCode() ^ (int)Style * 31 ^ Color.GetHashCode() ^ Offset.GetHashCode() * 7;
    }

    /// <summary>What the CSS box paints. Lists are in CSS order (the first background / shadow is on top).</summary>
    public sealed class BoxStyle
    {
        /// <summary>background-color (painted under every background layer).</summary>
        public Rgba BackgroundColor = Rgba.Transparent;
        /// <summary>background-image layers with their size / position / repeat; the first is on top.</summary>
        public List<BackgroundLayer> Backgrounds = new List<BackgroundLayer>();
        public BorderSide Top = BorderSide.None, Right = BorderSide.None, Bottom = BorderSide.None, Left = BorderSide.None;
        public Radii Radius = Radii.Zero;
        public List<BoxShadow> Shadows = new List<BoxShadow>();
        public Outline Outline = Outline.None;
        /// <summary>padding (px), for the content box; not painted.</summary>
        public Insets Padding = Insets.Zero;

        public void SetBorder(BorderSide side) { Top = Right = Bottom = Left = side; }

        public Insets BorderWidths => new Insets(Top.Used, Right.Used, Bottom.Used, Left.Used);

        public RectF PaddingBox(RectF borderBox) => BorderWidths.Shrink(borderBox);
        public RectF ContentBox(RectF borderBox) => Padding.Shrink(BorderWidths.Shrink(borderBox));

        public bool HasBorder => Top.Visible || Right.Visible || Bottom.Visible || Left.Visible;

        public bool IsEmpty
        {
            get
            {
                if (BackgroundColor.A > 0f || HasBorder || Outline.Visible) return false;
                foreach (BackgroundLayer l in Backgrounds) if (l.Image != null) return false;
                foreach (BoxShadow s in Shadows) if (s.Color.A > 0f) return false;
                return true;
            }
        }

        /// <summary>The background as one paint over the border box (colour + layers that cover their area); null if none.</summary>
        public Paint BackgroundPaint(RectF borderBox)
        {
            var layers = new List<Paint>();
            RectF area = PaddingBox(borderBox);
            foreach (BackgroundLayer l in Backgrounds)
            {
                Paint p = l.Image?.Resolve(l.Tile(area));
                if (p != null) layers.Add(p);
            }
            if (BackgroundColor.A > 0f) layers.Add(new SolidPaint(BackgroundColor));
            if (layers.Count == 0) return null;
            return layers.Count == 1 ? layers[0] : new LayeredPaint(layers);
        }

        public BoxStyle Clone()
        {
            var c = (BoxStyle)MemberwiseClone();
            c.Backgrounds = new List<BackgroundLayer>(Backgrounds.Count);
            foreach (BackgroundLayer l in Backgrounds) c.Backgrounds.Add(l.Clone());
            c.Shadows = new List<BoxShadow>(Shadows);
            return c;
        }
    }

    public static class BoxMesh
    {
        /// <summary>Contour offsets of a blurred shadow edge, in σ.</summary>
        private static readonly float[] BlurRings = { -3f, -2.25f, -1.5f, -1f, -0.6f, -0.3f, 0f, 0.3f, 0.6f, 1f, 1.5f, 2.25f, 3f };

        /// <summary>
        /// Paint order (CSS 2 Appendix E): outer shadows (the last listed lowest), background colour and layers (the
        /// last listed lowest; clipped to the border box), inset shadows (clipped to the padding box), border, outline.
        /// feather = one screen pixel in these units (kit.md §4.3).
        /// </summary>
        public static void Build(RectF borderBox, BoxStyle s, float feather, MeshBuilder mb)
        {
            if (s == null || borderBox.W <= 0f || borderBox.H <= 0f) return;
            CornerSet radii = s.Radius.Resolve(borderBox);
            List<Vec2> outline = Outline(borderBox, radii);
            for (int i = s.Shadows.Count - 1; i >= 0; i--)
                if (!s.Shadows[i].Inset) OuterShadow(borderBox, radii, s.Shadows[i], feather, outline, mb);
            Backgrounds(borderBox, radii, s, feather, outline, mb);
            Insets bw = s.BorderWidths;
            for (int i = s.Shadows.Count - 1; i >= 0; i--)
                if (s.Shadows[i].Inset) InsetShadow(borderBox, radii, bw, s.Shadows[i], feather, mb);
            Border(borderBox, radii, s.Top, s.Right, s.Bottom, s.Left, feather, mb);
            if (s.Outline.Visible) OutlineRing(borderBox, radii, s.Outline, feather, mb);
        }

        /// <summary>The border box (or the padding box) as a convex polygon, for clipping children (overflow: hidden with a radius).</summary>
        public static List<Vec2> ClipPolygon(RectF borderBox, BoxStyle s, bool paddingBox = false, float tolPx = 0.2f)
        {
            CornerSet r = s.Radius.Resolve(borderBox);
            if (!paddingBox) return Shapes.RoundRect(borderBox, r, tolPx).Points;
            Insets bw = s.BorderWidths;
            return Shapes.RoundRect(bw.Shrink(borderBox), r.Inset(bw.Top, bw.Right, bw.Bottom, bw.Left), tolPx).Points;
        }

        private static List<Vec2> Outline(RectF r, CornerSet c) => Pieces.CleanConvex(Shapes.RoundRect(r, c).Points);

        // ---- backgrounds ---------------------------------------------------------------------------------------

        private static void Backgrounds(RectF bb, CornerSet radii, BoxStyle s, float feather, List<Vec2> outline, MeshBuilder mb)
        {
            RectF bounds = bb.Inflate(feather);
            if (s.BackgroundColor.A > 0f)
                foreach (List<CV> piece in Pieces.ConvexAA(outline, feather))
                    Pieces.EmitPainted(piece, new SolidPaint(s.BackgroundColor), bounds, mb);
            RectF area = s.PaddingBox(bb);
            if (area.W <= 0f || area.H <= 0f) area = bb;
            for (int i = s.Backgrounds.Count - 1; i >= 0; i--)
            {
                BackgroundLayer layer = s.Backgrounds[i];
                if (layer.Image == null) continue;
                RectF tile = layer.Tile(area);
                if (tile.W <= 0f || tile.H <= 0f) continue;
                if (layer.CoversArea(area))
                {
                    // the image extends to the border box (clip border-box), the gradient padded beyond its area
                    Paint p = layer.Image.Resolve(tile);
                    if (p == null) continue;
                    foreach (List<CV> piece in Pieces.ConvexAA(outline, feather)) Pieces.EmitPainted(piece, p, bounds, mb);
                    continue;
                }
                foreach (RectF t in Tiles(layer, tile, bb))
                {
                    Paint p = layer.Image.Resolve(t);
                    if (p == null) break;
                    var poly = new List<CV>
                    {
                        new CV(t.TopLeft, 1f), new CV(t.TopRight, 1f), new CV(t.BottomRight, 1f), new CV(t.BottomLeft, 1f),
                    };
                    poly = Pieces.ClipConvex(poly, outline);
                    if (poly.Count >= 3) Pieces.EmitPainted(poly, p, t, mb);
                }
            }
        }

        private static IEnumerable<RectF> Tiles(BackgroundLayer layer, RectF tile, RectF clip)
        {
            bool rx = layer.Repeat == BackgroundRepeat.Repeat || layer.Repeat == BackgroundRepeat.RepeatX;
            bool ry = layer.Repeat == BackgroundRepeat.Repeat || layer.Repeat == BackgroundRepeat.RepeatY;
            int i0 = 0, i1 = 0, j0 = 0, j1 = 0;
            if (rx) { i0 = (int)Math.Floor((clip.X - tile.X) / tile.W); i1 = (int)Math.Ceiling((clip.Right - tile.X) / tile.W) - 1; }
            if (ry) { j0 = (int)Math.Floor((clip.Y - tile.Y) / tile.H); j1 = (int)Math.Ceiling((clip.Bottom - tile.Y) / tile.H) - 1; }
            int count = 0;
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    if (++count > 4096) yield break;
                    yield return tile.Offset(i * tile.W, j * tile.H);
                }
        }

        // ---- shadows -------------------------------------------------------------------------------------------

        /// <summary>Clamps radii to a box like CSS does (one factor for all).</summary>
        private static CornerSet Fit(CornerSet c, float w, float h)
        {
            float f = 1f;
            float Fx(float side, float sum) => sum > side && sum > 0f ? Math.Min(f, Math.Max(0f, side) / sum) : f;
            f = Fx(w, c.TL.X + c.TR.X);
            f = Fx(w, c.BL.X + c.BR.X);
            f = Fx(h, c.TL.Y + c.BL.Y);
            f = Fx(h, c.TR.Y + c.BR.Y);
            if (f >= 1f) return c;
            return new CornerSet(c.TL * f, c.TR * f, c.BR * f, c.BL * f);
        }

        private static float Contour(RectF r, CornerSet c, float d, out RectF grown, out CornerSet gc)
        {
            float lim = Math.Min(r.W, r.H) * 0.5f - 0.01f;
            if (d < -lim) d = -lim;
            grown = r.Inflate(d);
            gc = Fit(c.Grow(d), grown.W, grown.H);
            return d;
        }

        private static void OuterShadow(RectF bb, CornerSet radii, BoxShadow sh, float feather, List<Vec2> borderOutline, MeshBuilder mb)
        {
            if (sh.Color.A <= 0f) return;
            // a sharp shadow no bigger than the box and not moved lies wholly behind it (`0 0 0 0` in ocb-pip-lit)
            if (sh.Blur <= 0f && sh.Spread <= 0f && sh.X == 0f && sh.Y == 0f) return;
            RectF shape = bb.Inflate(sh.Spread).Offset(sh.X, sh.Y);
            if (shape.W <= 0f || shape.H <= 0f) return;
            CornerSet sr = Fit(radii.Spread(sh.Spread), shape.W, shape.H);
            float sigma = sh.Sigma;
            var pieces = new List<List<CV>>();
            if (sigma <= 0f)
            {
                List<Vec2> pts = Pieces.CleanConvex(Shapes.RoundRect(shape, sr).Points);
                foreach (List<CV> p in Pieces.ConvexAA(pts, feather)) pieces.Add(Tint(p, sh.Color));
            }
            else
            {
                int segs = Shapes.CornerSegments(sr.Max + 3f * sigma);
                Vec2 centre = shape.Center;
                float ha = shape.W * 0.5f, hb = shape.H * 0.5f;
                // points along the straight edges too, where a corner's falloff meets the edge's plateau
                float[] edgeOffsets = { 0.5f * sigma, sigma, 2f * sigma, 3f * sigma };
                var contours = new List<CV[]>();
                foreach (float k in BlurRings)
                {
                    float d = Contour(shape, sr, k * sigma, out RectF g, out CornerSet gc);
                    List<Vec2> pts = ShadowContour(g, gc, segs, edgeOffsets);
                    var ring = new CV[pts.Count];
                    float edge = Blur.EdgeAlpha(d, sigma);
                    for (int i = 0; i < pts.Count; i++)
                    {
                        Vec2 p = pts[i];
                        float sep = Separable(p.X - centre.X, ha, sigma) * Separable(p.Y - centre.Y, hb, sigma);
                        ring[i] = new CV(p, sh.Color.MulAlpha(Math.Min(edge, sep)));
                    }
                    contours.Add(ring);
                }
                pieces.Add(new List<CV>(contours[0]));
                for (int k = 0; k + 1 < contours.Count; k++)
                {
                    CV[] a = contours[k], b = contours[k + 1];
                    for (int i = 0, n = a.Length; i < n; i++)
                    {
                        int j = (i + 1) % n;
                        pieces.Add(new List<CV> { a[i], a[j], b[j] });
                        pieces.Add(new List<CV> { a[i], b[j], b[i] });
                    }
                }
            }
            var box = new ConvexRegion(borderOutline);
            foreach (List<CV> p in pieces)
            {
                if (p.Count < 3 || Math.Abs(Pieces.SignedArea(p)) < 1e-7f) continue;
                foreach (List<CV> outside in Pieces.SubtractConvex(p, box)) Pieces.EmitColored(outside, mb);
            }
        }

        /// <summary>
        /// A rounded-rect contour with extra points on each straight edge at the given distances from both of its ends
        /// (clamped at the middle), so every contour of one shadow has the same number of points.
        /// </summary>
        private static List<Vec2> ShadowContour(RectF r, CornerSet c, int segs, float[] offsets)
        {
            List<Vec2> arcs = Shapes.RoundRectPoints(r, c, segs);
            int block = segs + 1;
            var o = new List<Vec2>(4 * (block + 2 * offsets.Length));
            for (int k = 0; k < 4; k++)
            {
                for (int i = 0; i < block; i++) o.Add(arcs[k * block + i]);
                Vec2 a = arcs[k * block + segs], b = arcs[((k + 1) % 4) * block];
                float len = Vec2.Dist(a, b);
                Vec2 dir = len > 0f ? (b - a) / len : Vec2.Zero;
                float half = len * 0.5f;
                foreach (float d in offsets) o.Add(a + dir * Math.Min(d, half));
                for (int j = offsets.Length - 1; j >= 0; j--) o.Add(b - dir * Math.Min(offsets[j], half));
            }
            return o;
        }

        /// <summary>The blurred 1-D box of half width a at offset x: Φ((a − x)/σ) − Φ((−a − x)/σ).</summary>
        private static float Separable(float x, float a, float sigma) => Blur.Phi((a - x) / sigma) - Blur.Phi((-a - x) / sigma);

        private static List<CV> Tint(List<CV> coverage, Rgba c)
        {
            var o = new List<CV>(coverage.Count);
            foreach (CV v in coverage) o.Add(new CV(v.P, c.MulAlpha(v.C.A)));
            return o;
        }

        private static void InsetShadow(RectF bb, CornerSet radii, Insets bw, BoxShadow sh, float feather, MeshBuilder mb)
        {
            if (sh.Color.A <= 0f) return;
            RectF pad = bw.Shrink(bb);
            if (pad.W <= 0f || pad.H <= 0f) return;
            CornerSet pr = radii.Inset(bw.Top, bw.Right, bw.Bottom, bw.Left);
            List<Vec2> clip = Pieces.CleanConvex(Shapes.RoundRect(pad, pr).Points);
            RectF hole = pad.Inflate(-sh.Spread).Offset(sh.X, sh.Y);
            float sigma = sh.Sigma;
            if (sigma <= 0f && hole.W > 0f && hole.H > 0f)
            {
                // a hard hole edge on or past the padding edge paints nothing on that side: push it far out so its
                // AA fringe does not leave a faint line along the box (`inset 3px 0 0` is a bar on the left only)
                const float Far = 1e4f, Eps = 1e-3f;
                float l = hole.X <= pad.X + Eps ? -Far : hole.X;
                float t = hole.Y <= pad.Y + Eps ? -Far : hole.Y;
                float r = hole.Right >= pad.Right - Eps ? Far : hole.Right;
                float b = hole.Bottom >= pad.Bottom - Eps ? Far : hole.Bottom;
                hole = RectF.FromLtrb(l, t, r, b);
            }
            var pieces = new List<List<CV>>();
            if (hole.W <= 0f || hole.H <= 0f)
            {
                var all = new List<CV>();
                foreach (Vec2 p in clip) all.Add(new CV(p, sh.Color));
                pieces.Add(all);
            }
            else
            {
                CornerSet hr = Fit(pr.Spread(-sh.Spread), hole.W, hole.H);
                float far = (float)Math.Sqrt(pad.W * pad.W + pad.H * pad.H) + Math.Abs(sh.X) + Math.Abs(sh.Y) + 3f * sigma + 8f;
                float[] offsets;
                if (sigma > 0f)
                {
                    offsets = new float[BlurRings.Length];
                    for (int i = 0; i < BlurRings.Length; i++) offsets[i] = BlurRings[i] * sigma;
                }
                else
                {
                    float h = Math.Max(1e-3f, feather * 0.5f);
                    offsets = new[] { -h, h };
                }
                int segs = Shapes.CornerSegments(hr.Max + far);
                var contours = new List<CV[]>();
                foreach (float off in offsets)
                {
                    float d = Contour(hole, hr, off, out RectF g, out CornerSet gc);
                    float a = sigma > 0f ? 1f - Blur.EdgeAlpha(d, sigma) : (off > 0f ? 1f : 0f);
                    List<Vec2> pts = Shapes.RoundRectPoints(g, gc, segs);
                    var ring = new CV[pts.Count];
                    for (int i = 0; i < pts.Count; i++) ring[i] = new CV(pts[i], sh.Color.MulAlpha(a));
                    contours.Add(ring);
                }
                {
                    Contour(hole, hr, far, out RectF g, out CornerSet gc);
                    List<Vec2> pts = Shapes.RoundRectPoints(g, gc, segs);
                    var ring = new CV[pts.Count];
                    float a = contours[contours.Count - 1][0].C.A / Math.Max(1e-6f, sh.Color.A);
                    for (int i = 0; i < pts.Count; i++) ring[i] = new CV(pts[i], sh.Color.MulAlpha(a));
                    contours.Add(ring);
                }
                for (int k = 0; k + 1 < contours.Count; k++)
                {
                    CV[] a = contours[k], b = contours[k + 1];
                    for (int i = 0, n = a.Length; i < n; i++)
                    {
                        int j = (i + 1) % n;
                        pieces.Add(new List<CV> { a[i], a[j], b[j] });
                        pieces.Add(new List<CV> { a[i], b[j], b[i] });
                    }
                }
            }
            var padRegion = new ConvexRegion(clip);
            foreach (List<CV> p in pieces)
            {
                if (p.Count < 3 || Math.Abs(Pieces.SignedArea(p)) < 1e-7f) continue;
                List<CV> inside = Pieces.ClipConvex(p, padRegion);
                if (inside.Count >= 3) Pieces.EmitColored(inside, mb);
            }
        }


        // ---- borders -------------------------------------------------------------------------------------------

        /// <summary>The border of a box (also used for the outline, as a uniform border of the outline's rect).</summary>
        private static void Border(RectF bb, CornerSet r, BorderSide top, BorderSide right, BorderSide bottom, BorderSide left,
            float feather, MeshBuilder mb)
        {
            if (!top.Visible && !right.Visible && !bottom.Visible && !left.Visible) return;
            float wT = top.Used, wR = right.Used, wB = bottom.Used, wL = left.Used;
            RectF inner = bb.Inset(wT, wR, wB, wL);
            if (inner.W < 0f || inner.H < 0f) inner = new RectF(bb.Center.X, bb.Center.Y, 0f, 0f);
            CornerSet ir = r.Inset(wT, wR, wB, wL);
            int segs = Shapes.CornerSegments(r.Max);
            List<Vec2> outer = Shapes.RoundRectPoints(bb, r, segs);
            List<Vec2> innerPts = Shapes.RoundRectPoints(inner, ir, segs);
            BorderSide[] sides = { top, right, bottom, left };
            bool uniform = top.Equals(right) && top.Equals(bottom) && top.Equals(left);
            if (uniform && (top.Style == BorderStyle.Dashed || top.Style == BorderStyle.Dotted) && !r.IsZero)
            {
                ClosedDashes(bb, r, top, feather, mb);
                return;
            }
            if (uniform && (top.Style == BorderStyle.Solid || (top.Style == BorderStyle.Double && top.Width < 3f)))
            {
                foreach (List<CV> t in Band(outer, innerPts, 0f, 1f, feather)) Pieces.EmitColored(Tint(t, top.Color), mb);
                return;
            }
            if (uniform && top.Style == BorderStyle.Double)
            {
                foreach (List<CV> t in Band(outer, innerPts, 0f, 1f / 3f, feather)) Pieces.EmitColored(Tint(t, top.Color), mb);
                foreach (List<CV> t in Band(outer, innerPts, 2f / 3f, 1f, feather)) Pieces.EmitColored(Tint(t, top.Color), mb);
                return;
            }
            (Vec2 n, float c)[] corners = CornerLines(bb, wT, wR, wB, wL);
            float[] widths = { wT, wR, wB, wL };
            List<List<CV>> full = null, outerThird = null, innerThird = null;
            for (int s = 0; s < 4; s++)
            {
                BorderSide side = sides[s];
                if (!side.Visible) continue;
                switch (side.Style)
                {
                    case BorderStyle.Solid:
                        full = full ?? Band(outer, innerPts, 0f, 1f, feather);
                        EmitSide(full, segs, s, corners, side.Color, mb);
                        break;
                    case BorderStyle.Double:
                        if (side.Width < 3f)
                        {
                            full = full ?? Band(outer, innerPts, 0f, 1f, feather);
                            EmitSide(full, segs, s, corners, side.Color, mb);
                        }
                        else
                        {
                            outerThird = outerThird ?? Band(outer, innerPts, 0f, 1f / 3f, feather);
                            innerThird = innerThird ?? Band(outer, innerPts, 2f / 3f, 1f, feather);
                            EmitSide(outerThird, segs, s, corners, side.Color, mb);
                            EmitSide(innerThird, segs, s, corners, side.Color, mb);
                        }
                        break;
                    case BorderStyle.Dashed:
                    case BorderStyle.Dotted:
                        SideDashes(r, outer, innerPts, segs, s, side, widths, corners, feather, mb);
                        break;
                }
            }
        }

        /// <summary>The ring triangles between the fractions f0..f1 of the way from the outer to the inner outline (6 per point pair).</summary>
        private static List<List<CV>> Band(List<Vec2> outer, List<Vec2> inner, float f0, float f1, float feather)
        {
            int n = outer.Count;
            var o = new List<Vec2>(n);
            var q = new List<Vec2>(n);
            for (int i = 0; i < n; i++)
            {
                o.Add(Vec2.Lerp(outer[i], inner[i], f0));
                q.Add(Vec2.Lerp(outer[i], inner[i], f1));
            }
            return Pieces.RingTriangles(o, q, feather);
        }

        /// <summary>
        /// Per corner (TL, TR, BR, BL): the line from the outer corner to the inner corner of the border, as (n, c)
        /// with Dot(n, p) − c &gt; 0 on the side that follows the corner clockwise (TL → top, TR → right, ...).
        /// </summary>
        private static (Vec2 n, float c)[] CornerLines(RectF bb, float wT, float wR, float wB, float wL)
        {
            Vec2[] o = { bb.TopLeft, bb.TopRight, bb.BottomRight, bb.BottomLeft };
            Vec2[] i =
            {
                new Vec2(bb.X + wL, bb.Y + wT), new Vec2(bb.Right - wR, bb.Y + wT),
                new Vec2(bb.Right - wR, bb.Bottom - wB), new Vec2(bb.X + wL, bb.Bottom - wB),
            };
            Vec2[] diag = { new Vec2(1f, 1f), new Vec2(-1f, 1f), new Vec2(-1f, -1f), new Vec2(1f, -1f) };
            Vec2[] next = { bb.TopRight, bb.BottomRight, bb.BottomLeft, bb.TopLeft };
            var lines = new (Vec2, float)[4];
            for (int k = 0; k < 4; k++)
            {
                Vec2 d = i[k] - o[k];
                if (d.LenSq < 1e-10f) d = diag[k];
                Vec2 n = d.Perp;
                float c = Vec2.Dot(n, o[k]);
                if (Vec2.Dot(n, next[k]) - c < 0f) { n = -n; c = -c; }
                lines[k] = (n, c);
            }
            return lines;
        }

        /// <summary>
        /// Emits the triangles of a band that belong to side s (0 top, 1 right, 2 bottom, 3 left): the straight edge
        /// after corner s, and the halves of the corner arcs on its side of the corner lines.
        /// </summary>
        private static void EmitSide(List<List<CV>> band, int segs, int s, (Vec2 n, float c)[] corners, Rgba color, MeshBuilder mb)
        {
            int pairs = band.Count / 6, block = segs + 1;
            for (int i = 0; i < pairs; i++)
            {
                int j = (i + 1) % pairs;
                int ci = i / block, cj = j / block;
                bool arc = ci == cj;
                int before = (ci + 3) % 4, after = ci;
                if (arc ? (s != before && s != after) : ci != s) continue;
                for (int k = 0; k < 6; k++)
                {
                    List<CV> piece = band[i * 6 + k];
                    if (arc)
                    {
                        (Vec2 ln, float lc) = corners[ci];
                        piece = s == after ? Pieces.ClipHalf(piece, ln, lc) : Pieces.ClipHalf(piece, -ln, -lc);
                    }
                    if (piece.Count >= 3) Pieces.EmitColored(Tint(piece, color), mb);
                }
            }
        }

        // ---- dashed and dotted ---------------------------------------------------------------------------------

        /// <summary>Blink's dash and gap for a stroke width (StrokeData::DashLengthRatio / DashGapRatio; dotted = width / width).</summary>
        public static void DashPattern(BorderStyle style, float width, out float dash, out float gap)
        {
            if (style == BorderStyle.Dotted)
            {
                dash = width;
                gap = width;
                return;
            }
            dash = width * (width >= 3f ? 2f : 3f);
            gap = width * (width >= 3f ? 1f : 2f);
        }

        /// <summary>
        /// Blink's SelectBestDashGap: the gap that fits whole dashes into the length (an open path starts and ends with
        /// a dash; a closed one has as many gaps as dashes), whichever is nearer the nominal gap.
        /// </summary>
        public static float BestDashGap(float length, float dash, float gap, bool closed)
        {
            float available = closed ? length : length + gap;
            float minN = (float)Math.Floor(available / (dash + gap));
            float maxN = minN + 1f;
            float minGaps = closed ? minN : minN - 1f;
            float maxGaps = closed ? maxN : maxN - 1f;
            float minGap = minGaps > 0f ? (length - minN * dash) / minGaps : float.PositiveInfinity;
            float maxGap = maxGaps > 0f ? (length - maxN * dash) / maxGaps : float.PositiveInfinity;
            return maxGap <= 0f || Math.Abs(minGap - gap) < Math.Abs(maxGap - gap) ? minGap : maxGap;
        }

        /// <summary>
        /// The dash intervals [from, to] along a path of the given length. An open path no longer than two dashes
        /// paints solid (Blink drops the dash effect there); a closed path too short for one gap paints one dash.
        /// </summary>
        public static List<(float from, float to)> DashIntervals(float length, float dash, float gap, bool closed)
        {
            var list = new List<(float, float)>();
            if (length <= 0f) return list;
            if (!closed && length <= dash * 2f)
            {
                list.Add((0f, length));
                return list;
            }
            float g = BestDashGap(length, dash, gap, closed);
            if (float.IsInfinity(g) || float.IsNaN(g) || g < 0f)
            {
                list.Add((0f, closed ? Math.Min(dash, length) : length));
                return list;
            }
            float period = dash + g;
            for (float s = 0f; s < length - 1e-3f; s += period)
                list.Add((s, Math.Min(length, s + dash)));
            return list;
        }

        /// <summary>A uniform dashed / dotted border with rounded corners: one closed path along the middle of the band.</summary>
        private static void ClosedDashes(RectF bb, CornerSet r, BorderSide side, float feather, MeshBuilder mb)
        {
            float w = side.Width;
            RectF mid = bb.Inflate(-w * 0.5f);
            if (mid.W <= 0f || mid.H <= 0f) return;
            CornerSet mr = r.Inset(w * 0.5f, w * 0.5f, w * 0.5f, w * 0.5f);
            int segs = Shapes.CornerSegments(Math.Max(mr.Max, 1f), 0.1f);
            List<Vec2> pts = Shapes.Dedupe(Shapes.RoundRectPoints(mid, mr, segs));
            // start where the top edge starts (after the TL arc), as Skia's rounded-rect path does
            int start = 0;
            float best = float.MaxValue;
            var target = new Vec2(mid.X + mr.TL.X, mid.Y);
            for (int i = 0; i < pts.Count; i++)
            {
                float d = (pts[i] - target).LenSq;
                if (d < best) { best = d; start = i; }
            }
            var path = new List<Vec2>(pts.Count);
            for (int i = 0; i < pts.Count; i++) path.Add(pts[(start + i) % pts.Count]);
            DashPath(path, true, side, feather, null, mb);
        }

        /// <summary>One dashed / dotted side: an open path along the middle of the band between its corner lines.</summary>
        private static void SideDashes(CornerSet r, List<Vec2> outer, List<Vec2> inner, int segs, int s, BorderSide b, float[] widths,
            (Vec2 n, float c)[] corners, float feather, MeshBuilder mb)
        {
            int n = outer.Count, block = segs + 1;
            var mid = new List<Vec2>(n);
            for (int i = 0; i < n; i++) mid.Add(Vec2.Lerp(outer[i], inner[i], 0.5f));
            // the side runs from corner s's arc to corner s+1's arc
            var run = new List<Vec2>();
            for (int i = s * block; i < s * block + 2 * block; i++) run.Add(mid[i % n]);
            (Vec2 n0, float c0) = corners[s];
            (Vec2 n1, float c1) = corners[(s + 1) % 4];
            List<Vec2> path = Shapes.Dedupe(ClipOpen(ClipOpen(run, n0, c0), -n1, -c1));
            if (path.Count < 2) return;
            // a sharp corner: run the end dash out to the outer edge; the corner lines then split the corner square
            if (IsSharp(r, s)) path[0] = path[0] - (path[1] - path[0]).Norm * (widths[(s + 3) % 4] * 0.5f);
            if (IsSharp(r, (s + 1) % 4))
            {
                int k = path.Count - 1;
                path[k] = path[k] + (path[k] - path[k - 1]).Norm * (widths[(s + 1) % 4] * 0.5f);
            }
            var planes = new List<(Vec2, float)> { (n0, c0), (-n1, -c1) };
            DashPath(path, false, b, feather, planes, mb);
        }

        private static bool IsSharp(CornerSet r, int corner)
        {
            Vec2 c = corner == 0 ? r.TL : corner == 1 ? r.TR : corner == 2 ? r.BR : r.BL;
            return c.X <= 0f || c.Y <= 0f;
        }

        /// <summary>The part of an open polyline with Dot(n, p) − c ≥ 0 (one contiguous run assumed).</summary>
        private static List<Vec2> ClipOpen(List<Vec2> pts, Vec2 n, float c)
        {
            var o = new List<Vec2>();
            for (int i = 0; i < pts.Count; i++)
            {
                float si = Vec2.Dot(n, pts[i]) - c;
                if (i > 0)
                {
                    float sp = Vec2.Dot(n, pts[i - 1]) - c;
                    if ((sp < 0f && si > 0f) || (sp > 0f && si < 0f)) o.Add(Vec2.Lerp(pts[i - 1], pts[i], sp / (sp - si)));
                }
                if (si >= 0f) o.Add(pts[i]);
            }
            return o;
        }

        /// <summary>Dashes (or dots) of one border style along a path; planes (optional) clip every triangle.</summary>
        private static void DashPath(List<Vec2> path, bool closed, BorderSide b, float feather, List<(Vec2 n, float c)> planes, MeshBuilder mb)
        {
            float w = b.Width;
            var pts = new List<Vec2>(path);
            if (closed) pts.Add(path[0]);
            var cum = new List<float> { 0f };
            for (int i = 1; i < pts.Count; i++) cum.Add(cum[i - 1] + Vec2.Dist(pts[i - 1], pts[i]));
            float length = cum[cum.Count - 1];
            if (length <= 0f) return;
            var walk = new PathWalk(pts, cum, closed);
            if (b.Style == BorderStyle.Dotted && w >= 3f)
            {
                foreach ((float from, float to) in DashIntervals(length, w, w, closed))
                {
                    Vec2 c = walk.At((from + to) * 0.5f, out _);
                    List<Vec2> disc = Pieces.CleanConvex(Shapes.Ellipse(new RectF(c.X - w / 2f, c.Y - w / 2f, w, w)).Points);
                    foreach (List<CV> p in Pieces.ConvexAA(disc, feather)) Emit(Tint(p, b.Color), planes, mb);
                }
                return;
            }
            DashPattern(b.Style, w, out float dash, out float gap);
            foreach ((float from, float to) in DashIntervals(length, dash, gap, closed))
                Strip(walk, from, to, w, b.Color, feather, planes, mb);
        }

        private static void Emit(List<CV> piece, List<(Vec2 n, float c)> planes, MeshBuilder mb)
        {
            if (planes != null)
                foreach ((Vec2 n, float c) in planes)
                {
                    piece = Pieces.ClipHalf(piece, n, c);
                    if (piece.Count < 3) return;
                }
            Pieces.EmitColored(piece, mb);
        }

        /// <summary>Arc-length access to a polyline (closed: wraps; open: continues straight past the ends).</summary>
        private sealed class PathWalk
        {
            private readonly List<Vec2> _pts;
            private readonly List<float> _cum;
            private readonly bool _closed;
            public readonly float Length;

            public PathWalk(List<Vec2> pts, List<float> cum, bool closed)
            {
                _pts = pts;
                _cum = cum;
                _closed = closed;
                Length = cum[cum.Count - 1];
            }

            /// <summary>Interior vertex positions along the path (for stations inside a dash).</summary>
            public IEnumerable<float> Vertices()
            {
                for (int i = 1; i < _cum.Count - 1; i++) yield return _cum[i];
                if (_closed) yield return 0f;
            }

            private float Wrap(float s) => _closed ? ((s % Length) + Length) % Length : s;

            /// <summary>The point at s and the unit direction of travel there.</summary>
            public Vec2 At(float s, out Vec2 dir)
            {
                if (!_closed && s <= 0f)
                {
                    dir = (_pts[1] - _pts[0]).Norm;
                    return _pts[0] + dir * s;
                }
                if (!_closed && s >= Length)
                {
                    int k = _pts.Count - 1;
                    dir = (_pts[k] - _pts[k - 1]).Norm;
                    return _pts[k] + dir * (s - Length);
                }
                s = Wrap(s);
                int i = 1;
                while (i < _cum.Count - 1 && _cum[i] < s) i++;
                Vec2 a = _pts[i - 1], b = _pts[i];
                float seg = _cum[i] - _cum[i - 1];
                dir = (b - a).Norm;
                return Vec2.Lerp(a, b, seg > 0f ? (s - _cum[i - 1]) / seg : 0f);
            }

            /// <summary>The offset direction at s: the segment's left normal, or the miter at a vertex (scaled by 1/cos).</summary>
            public Vec2 Normal(float s)
            {
                At(s, out Vec2 dir);
                float sm = Wrap(s);
                int count = _cum.Count;
                for (int i = 0; i < count; i++)
                {
                    bool interior = i > 0 && i < count - 1;
                    bool seam = _closed && (i == 0 || i == count - 1);
                    if (!interior && !seam) continue;
                    if (Math.Abs(_cum[i] - sm) > 1e-4f) continue;
                    Vec2 d0, d1;
                    if (interior)
                    {
                        d0 = (_pts[i] - _pts[i - 1]).Norm;
                        d1 = (_pts[i + 1] - _pts[i]).Norm;
                    }
                    else
                    {
                        d0 = (_pts[count - 1] - _pts[count - 2]).Norm;
                        d1 = (_pts[1] - _pts[0]).Norm;
                    }
                    Vec2 m = (d0 + d1).Norm;
                    if (m != Vec2.Zero)
                    {
                        float cos = Math.Max(0.25f, Vec2.Dot(m, d0));
                        dir = m * (1f / cos);
                    }
                    break;
                }
                return new Vec2(dir.Y, -dir.X); // left of the travel direction = outward on a clockwise loop
            }
        }

        /// <summary>One dash: a strip along the path from s0 to s1, AA across (as a ring) and at both ends.</summary>
        private static void Strip(PathWalk walk, float s0, float s1, float w, Rgba color, float feather,
            List<(Vec2 n, float c)> planes, MeshBuilder mb)
        {
            float h = feather * 0.5f;
            var ends = new List<(float s, float a)>();
            float len = s1 - s0;
            if (feather <= 0f) { ends.Add((s0, 1f)); ends.Add((s1, 1f)); }
            else if (len >= feather)
            {
                ends.Add((s0 - h, 0f));
                ends.Add((s0 + h, 1f));
                ends.Add((s1 - h, 1f));
                ends.Add((s1 + h, 0f));
            }
            else
            {
                ends.Add((s0 - h, 0f));
                ends.Add(((s0 + s1) * 0.5f, len / feather));
                ends.Add((s1 + h, 0f));
            }
            var st = new List<(float s, float a)>(ends);
            float lo = ends[0].s, hi = ends[ends.Count - 1].s;
            float L = walk.Length;
            foreach (float v in walk.Vertices())
                for (int lap = -1; lap <= 1; lap++)
                {
                    float cs = v + lap * L;
                    if (cs > lo + 1e-4f && cs < hi - 1e-4f) st.Add((cs, Along(ends, cs)));
                }
            st.Sort((x, y) => x.s.CompareTo(y.s));
            float[] off, acr;
            if (feather <= 0f) { off = new[] { w / 2f, -w / 2f }; acr = new[] { 1f, 1f }; }
            else if (w >= feather) { off = new[] { w / 2f + h, w / 2f - h, -w / 2f + h, -w / 2f - h }; acr = new[] { 0f, 1f, 1f, 0f }; }
            else { off = new[] { feather, 0f, -feather }; acr = new[] { 0f, w / feather, 0f }; }
            var rows = new CV[st.Count][];
            for (int k = 0; k < st.Count; k++)
            {
                Vec2 p = walk.At(st[k].s, out _);
                Vec2 nrm = walk.Normal(st[k].s);
                rows[k] = new CV[off.Length];
                for (int j = 0; j < off.Length; j++) rows[k][j] = new CV(p + nrm * off[j], color.MulAlpha(acr[j] * st[k].a));
            }
            for (int k = 0; k + 1 < rows.Length; k++)
                for (int j = 0; j + 1 < off.Length; j++)
                {
                    Emit(new List<CV> { rows[k][j], rows[k + 1][j], rows[k + 1][j + 1] }, planes, mb);
                    Emit(new List<CV> { rows[k][j], rows[k + 1][j + 1], rows[k][j + 1] }, planes, mb);
                }
        }

        private static float Along(List<(float s, float a)> ends, float s)
        {
            for (int i = 0; i + 1 < ends.Count; i++)
                if (s >= ends[i].s && s <= ends[i + 1].s)
                {
                    float span = ends[i + 1].s - ends[i].s;
                    return span > 0f ? ends[i].a + (ends[i + 1].a - ends[i].a) * (s - ends[i].s) / span : ends[i].a;
                }
            return 1f;
        }

        // ---- outline -------------------------------------------------------------------------------------------

        /// <summary>The outline as a uniform border of the box grown by offset + width; corners keep the radius (+ offset), sharp stays sharp.</summary>
        private static void OutlineRing(RectF bb, CornerSet r, Outline o, float feather, MeshBuilder mb)
        {
            float grow = o.Offset + o.Width;
            RectF outer = bb.Inflate(grow);
            if (outer.W <= 0f || outer.H <= 0f) return;
            CornerSet orr = Fit(r.Grow(grow, keepSharp: true), outer.W, outer.H);
            var side = new BorderSide(o.Width, o.Style, o.Color);
            Border(outer, orr, side, side, side, side, feather, mb);
        }
    }
}
