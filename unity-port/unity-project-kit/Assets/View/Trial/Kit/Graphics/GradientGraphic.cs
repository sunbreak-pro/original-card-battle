// A box filled with a gradient (or any Paint): CSS linear-gradient / radial-gradient backgrounds, canvas and SVG
// gradients, and the glow sprites (kit.md §4.4). The shape is the box, a rounded box or the ellipse inscribed in it
// (border-radius: 50%). Linear paints are meshed exactly (split at their stop lines), radial ones refined.
//
//   GradientGraphic.Css(parent, "radial-gradient(closest-side, rgba(232,168,76,.55), rgba(0,0,0,0))", x, y, w, h, ellipse: true)
//   GradientGraphic.Glow(parent, x, y, w, h, DsTokens.Amber, .5f)        // field/art.js glowGradient()
using System;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    [AddComponentMenu("")]
    public sealed class GradientGraphic : VectorGraphic
    {
        public enum ShapeKind { Rect, RoundRect, Ellipse }

        private ShapeKind _shape = ShapeKind.Rect;
        private float _radius;
        private BackgroundImage _image;
        private Func<RectF, Paint> _paintOf;
        private bool _fringe = true;
        private Rgba _glowColor = Rgba.White;
        private bool _glow;
        private float _glowAlpha = 1f;

        /// <summary>Rect, rounded (Radius), or the inscribed ellipse.</summary>
        public ShapeKind Shape { get => _shape; set { _shape = value; SetGeometryDirty(); } }

        /// <summary>The corner radius of a RoundRect (px).</summary>
        public float Radius { get => _radius; set { _radius = value; SetGeometryDirty(); } }

        /// <summary>Anti-alias the edge (off for a fill that ends under something, or one that runs off the stage).</summary>
        public bool Fringe { get => _fringe; set { _fringe = value; SetGeometryDirty(); } }

        /// <summary>A new graphic placed at (x, y), sized w × h.</summary>
        public static GradientGraphic Make(RectTransform parent, float x, float y, float w, float h, string name = null) =>
            Make<GradientGraphic>(parent, x, y, w, h, name ?? "gradient");

        /// <summary>A CSS image value ("linear-gradient(...)", "radial-gradient(...)", "repeating-…") over the box.</summary>
        public static GradientGraphic Css(RectTransform parent, string image, float x, float y, float w, float h, bool ellipse = false, string name = null)
        {
            GradientGraphic g = Make(parent, x, y, w, h, name);
            g.SetCss(image);
            if (ellipse) g.Shape = ShapeKind.Ellipse;
            return g;
        }

        /// <summary>The image of a CSS background value (one gradient); the paint is resolved against the box when meshed.</summary>
        public GradientGraphic SetCss(string image, DsTheme theme = DsTheme.Dusk)
        {
            _image = global::Trial.Geometry.Css.Image(image, theme);
            _paintOf = null;
            _glow = false;
            BuildTint = Rgba.White;
            SetGeometryDirty();
            return this;
        }

        /// <summary>A paint made for the box (box px, origin top-left): canvas gradients, SVG userSpaceOnUse ones.</summary>
        public GradientGraphic SetPaint(Func<RectF, Paint> paintOfBox)
        {
            _paintOf = paintOfBox;
            _image = null;
            _glow = false;
            BuildTint = Rgba.White;
            SetGeometryDirty();
            return this;
        }

        /// <summary>A fixed paint (box px).</summary>
        public GradientGraphic SetPaint(Paint paint) => SetPaint(_ => paint);

        // js: scenes/battle/field/art.js:25
        /// <summary>
        /// glowGradient(color, alpha): radial-gradient(closest-side, color α·(1−r)² at r = 0, 25, 50, 75 %, transparent
        /// 100%) over the box (ProceduralArt.Glow). The colour is a tint (recolour only); Alpha rebuilds.
        /// </summary>
        public static GradientGraphic Glow(RectTransform parent, float x, float y, float w, float h, Rgba color, float alpha, string name = null)
        {
            GradientGraphic g = Make(parent, x, y, w, h, name ?? "glow");
            g.SetGlow(color, alpha);
            return g;
        }

        // js: scenes/battle/field/art.js:25
        public GradientGraphic SetGlow(Rgba color, float alpha)
        {
            _glow = true;
            _glowAlpha = alpha;
            _image = null;
            _paintOf = null;
            GlowColor = color;
            SetGeometryDirty();
            return this;
        }

        /// <summary>The glow's colour (its alpha multiplies the glow's): a tint.</summary>
        public Rgba GlowColor
        {
            get => _glowColor;
            set
            {
                _glowColor = value;
                if (_glow) BuildTint = new Rgba(value.R, value.G, value.B, value.A);
            }
        }

        /// <summary>The glow's alpha (the stops' α).</summary>
        public float GlowAlpha
        {
            get => _glowAlpha;
            set { _glowAlpha = value; if (_glow) SetGeometryDirty(); }
        }

        /// <summary>The stops of glowGradient(color, alpha) in white (the colour is the tint).</summary>
        public static GradientStop[] GlowStops(float alpha)
        {
            (float Pos, float Alpha)[] s = DsFigures.GlowStops(alpha);
            var o = new GradientStop[s.Length];
            for (int i = 0; i < s.Length; i++)
                o[i] = GradientStop.At(i == s.Length - 1 ? Rgba.Transparent : new Rgba(1f, 1f, 1f, s[i].Alpha), s[i].Pos);
            return o;
        }

        private Paint PaintFor(RectF box)
        {
            if (_glow) return RadialGradient.Css("closest-side", box, GlowStops(_glowAlpha));
            if (_paintOf != null) return _paintOf(box);
            return _image?.Resolve(box);
        }

        protected override void BuildMesh(MeshBuilder mb, RectF box, float feather)
        {
            if (box.W <= 0f || box.H <= 0f) return;
            Paint p = PaintFor(box);
            if (p == null) return;
            Polyline outline;
            switch (_shape)
            {
                case ShapeKind.Ellipse: outline = Shapes.Ellipse(box); break;
                case ShapeKind.RoundRect: outline = Shapes.RoundRect(box, CornerSet.All(Math.Max(0f, _radius))); break;
                default: outline = new Polyline(new System.Collections.Generic.List<Vec2> { box.TopLeft, box.TopRight, box.BottomRight, box.BottomLeft }, true); break;
            }
            if (_fringe) Shapes.FillConvexAA(outline, p, feather, mb);
            else Shapes.FillConvex(outline, p, mb);
        }
    }
}
