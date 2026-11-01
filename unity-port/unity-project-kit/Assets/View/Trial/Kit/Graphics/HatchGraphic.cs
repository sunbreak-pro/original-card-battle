// Parallel bars clipped to a convex window (kit.md §2.3, §4.5):
// - Bars: the DS hatch(w, h, angle, gap, color, width), an SVG <pattern> of `width` px bars every `gap` px rotated by
//   `angle` (clockwise), phase 0 at the svg's top-left (Origin). The floor's threat tiles and the swatches.
// - Stripes: a CSS repeating-linear-gradient with hard stops (`var(--wick) 0 2px, transparent 2px 5px` at 135deg, the
//   throw line's `amber 0 9px, transparent 9px 15px` at 90deg): bands [From, To) every Period px along the CSS angle,
//   phase 0 at the gradient line's start of the box.
// The window is the box (or Clip) with a corner radius; RevealRight cuts it from the right (clip-path: inset(0 x% 0 0)).
// One colour: the bars are built in white and tinted, so a colour change is a recolour.
using System;
using System.Collections.Generic;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    [AddComponentMenu("")]
    public sealed class HatchGraphic : VectorGraphic
    {
        public enum HatchMode { Bars, Stripes }

        private HatchMode _mode = HatchMode.Bars;
        private float _angle = 45f, _gap = 11f, _width = 3f;
        private float _from, _to = 2f, _period = 5f;
        private Rgba _color = DsTokens.ReachRed;
        private Vec2 _origin = Vec2.Zero;
        private RectF? _clipRect;
        private float _radius;
        private float _revealRight;

        public HatchMode Mode { get => _mode; set { _mode = value; SetGeometryDirty(); } }

        /// <summary>Bars: the pattern's rotation (deg, clockwise). Stripes: the CSS gradient angle.</summary>
        public float AngleDeg { get => _angle; set { _angle = value; SetGeometryDirty(); } }

        /// <summary>Bars: the pattern size (bar spacing).</summary>
        public float Gap { get => _gap; set { _gap = value; SetGeometryDirty(); } }

        /// <summary>Bars: the bar width.</summary>
        public float BarWidth { get => _width; set { _width = value; SetGeometryDirty(); } }

        /// <summary>Stripes: the band [From, To) inside each period, px along the gradient line.</summary>
        public float From { get => _from; set { _from = value; SetGeometryDirty(); } }
        public float To { get => _to; set { _to = value; SetGeometryDirty(); } }
        public float Period { get => _period; set { _period = value; SetGeometryDirty(); } }

        /// <summary>Bars: the pattern origin (box px; the svg's top-left = (0, 0)).</summary>
        public Vec2 Origin { get => _origin; set { _origin = value; SetGeometryDirty(); } }

        /// <summary>The window (box px); null = the whole box.</summary>
        public RectF? ClipRect { get => _clipRect; set { _clipRect = value; SetGeometryDirty(); } }

        /// <summary>The window's corner radius (px).</summary>
        public float ClipRadius { get => _radius; set { _radius = value; SetGeometryDirty(); } }

        /// <summary>The part of the window hidden from the right, 0..1 (clip-path: inset(0 x% 0 0) as a fraction of the box width).</summary>
        public float RevealRight
        {
            get => _revealRight;
            set
            {
                float v = Mathf.Clamp01(value);
                if (Math.Abs(v - _revealRight) < 1e-6f) return;
                _revealRight = v;
                SetGeometryDirty();
            }
        }

        /// <summary>The bars' colour (a tint: no rebuild).</summary>
        public Rgba BarColor
        {
            get => _color;
            set { _color = value; BuildTint = value; }
        }

        // js: vendor/ds/bundle.js:132
        /// <summary>hatch(w, h, angle = 45, gap = 11, color = --reach-red, width = 3) placed at (x, y) of parent.</summary>
        public static HatchGraphic Bars(RectTransform parent, float x, float y, float w, float h, float angle = 45f, float gap = 11f,
            Rgba? color = null, float width = 3f, string name = null)
        {
            HatchGraphic g = Make<HatchGraphic>(parent, x, y, w, h, name ?? "hatch");
            g._mode = HatchMode.Bars;
            g._angle = Or(angle, 45f);   // `angle || 45`
            g._gap = Or(gap, 11f);       // `gap || 11`
            g._width = Or(width, 3f);    // `width || 3`
            g.BarColor = color ?? DsTokens.ReachRed;
            g.SetGeometryDirty();
            return g;
        }

        // JS `v || fallback` for a number: 0 and NaN take the fallback.
        private static float Or(float v, float fallback) => v > 0f || v < 0f ? v : fallback;

        /// <summary>A repeating-linear-gradient(angle, color from to, transparent to period) of the box, placed at (x, y).</summary>
        public static HatchGraphic Stripes(RectTransform parent, float x, float y, float w, float h, float cssAngle, float from, float to, float period,
            Rgba color, float radius = 0f, string name = null)
        {
            HatchGraphic g = Make<HatchGraphic>(parent, x, y, w, h, name ?? "stripes");
            g._mode = HatchMode.Stripes;
            g._angle = cssAngle;
            g._from = from;
            g._to = to;
            g._period = period;
            g._radius = radius;
            g.BarColor = color;
            g.SetGeometryDirty();
            return g;
        }

        /// <summary>
        /// Stripes from a CSS value ("repeating-linear-gradient(135deg, var(--wick) 0 2px, transparent 2px 5px)"): false
        /// when it is not one coloured band over transparency.
        /// </summary>
        public bool TrySetStripesCss(string css)
        {
            BackgroundImage img = Css.Image(css);
            if (!(img is LinearGradientSpec spec) || !spec.Repeating) return false;
            Rect r = rectTransform.rect;
            var box = new RectF(0f, 0f, Math.Max(1f, r.width), Math.Max(1f, r.height));
            if (!(spec.Resolve(box) is LinearGradient lg) || !lg.TryAsStripes(out float from, out float to, out float period, out Rgba color)) return false;
            _mode = HatchMode.Stripes;
            _angle = spec.AngleFor(box);
            _from = from;
            _to = to;
            _period = period;
            BarColor = color;
            SetGeometryDirty();
            return true;
        }

        /// <summary>The window polygon (box px).</summary>
        public List<Vec2> Window(RectF box)
        {
            RectF w = _clipRect ?? box;
            List<Vec2> poly = _radius > 0f ? Shapes.RoundRect(w, CornerSet.All(_radius)).Points
                : new List<Vec2> { w.TopLeft, w.TopRight, w.BottomRight, w.BottomLeft };
            if (_revealRight > 0f) poly = Hatch.RevealTo(poly, box.X + (1f - _revealRight) * box.W);
            return poly;
        }

        protected override void BuildMesh(MeshBuilder mb, RectF box, float feather)
        {
            List<Vec2> clip = Window(box);
            if (clip.Count < 3) return;
            if (_mode == HatchMode.Bars) Hatch.Bars(clip, _origin, _angle, _gap, _width, Rgba.White, feather, mb);
            else Hatch.Stripes(box, clip, _angle, _from, _to, _period, Rgba.White, feather, mb);
        }
    }
}
