// The CSS box of an element (kit.md §4.7): background, border (solid / dashed / dotted / double, per side), radius,
// box-shadow (outer and inset) and outline, meshed by Trial.Geometry's BoxMesh in CSS paint order. The box is the
// element's border box; children are separate GameObjects drawn after it (the element's content).
//
//   BoxGraphic.Make(parent, "background: var(--panel-solid); border: 1px solid var(--steel); border-radius: 6px", x, y, w, h)
//   box.SetCss(CommonStyles.Btn(primary: true, …));   box.SetShadows(frame.BoxShadows);   // a keyframe's box-shadow
using System;
using System.Collections.Generic;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    [AddComponentMenu("")]
    public sealed class BoxGraphic : VectorGraphic
    {
        private BoxStyle _style = new BoxStyle();
        private string _css;

        /// <summary>The style meshed (owned by the graphic: set a new one or call SetGeometryDirty after changing it).</summary>
        public BoxStyle Style
        {
            get => _style;
            set
            {
                _style = value ?? new BoxStyle();
                _css = null;
                SetGeometryDirty();
            }
        }

        /// <summary>The CSS declarations last given to SetCss (null when the style was set directly).</summary>
        public string Css => _css;

        /// <summary>A new box under parent, placed at (x, y), sized w × h, styled with CSS declarations.</summary>
        public static BoxGraphic Make(RectTransform parent, string css, float x, float y, float w, float h, string name = null)
        {
            BoxGraphic g = Make<BoxGraphic>(parent, x, y, w, h, name ?? "box");
            g.SetCss(css);
            return g;
        }

        /// <summary>A new box under parent with a parsed style.</summary>
        public static BoxGraphic Make(RectTransform parent, BoxStyle style, float x, float y, float w, float h, string name = null)
        {
            BoxGraphic g = Make<BoxGraphic>(parent, x, y, w, h, name ?? "box");
            g.Style = style?.Clone();
            return g;
        }

        /// <summary>The BoxGraphic of an element (added when missing; the element must not hold another graphic).</summary>
        public static BoxGraphic On(RectTransform rt, string css = null)
        {
            BoxGraphic g = rt.GetComponent<BoxGraphic>();
            if (!g) g = rt.gameObject.AddComponent<BoxGraphic>();
            if (css != null) g.SetCss(css);
            return g;
        }

        /// <summary>The box properties of a CSS declaration block (Trial.Geometry Css.Box: var(), color-mix(), gradients…).</summary>
        public BoxGraphic SetCss(string declarations, DsTheme theme = DsTheme.Dusk)
        {
            _style = global::Trial.Geometry.Css.Box(declarations ?? "", theme);
            _css = declarations;
            SetGeometryDirty();
            return this;
        }

        /// <summary>box-shadow layers in CSS order (a keyframe or a transition writes these); null = none.</summary>
        public void SetShadows(IReadOnlyList<BoxShadow> shadows)
        {
            _style.Shadows = shadows == null ? new List<BoxShadow>() : new List<BoxShadow>(shadows);
            SetGeometryDirty();
        }

        /// <summary>background-color.</summary>
        public Rgba BackgroundColor
        {
            get => _style.BackgroundColor;
            set
            {
                if (_style.BackgroundColor.Equals(value)) return;
                _style.BackgroundColor = value;
                SetGeometryDirty();
            }
        }

        /// <summary>border-color of every side (widths and styles kept).</summary>
        public void SetBorderColor(Rgba c)
        {
            _style.Top = _style.Top.WithColor(c);
            _style.Right = _style.Right.WithColor(c);
            _style.Bottom = _style.Bottom.WithColor(c);
            _style.Left = _style.Left.WithColor(c);
            SetGeometryDirty();
        }

        /// <summary>outline (the Tab focus ring of a button: 2px solid --wick, offset 2px).</summary>
        public Outline Outline
        {
            get => _style.Outline;
            set
            {
                if (_style.Outline.Equals(value)) return;
                _style.Outline = value;
                SetGeometryDirty();
            }
        }

        /// <summary>The border box in its own px (origin top-left, y down).</summary>
        public RectF BorderBox
        {
            get { Rect r = rectTransform.rect; return new RectF(0f, 0f, r.width, r.height); }
        }

        /// <summary>The padding box (inside the border).</summary>
        public RectF PaddingBox => _style.PaddingBox(BorderBox);

        /// <summary>The content box (inside the border and the padding).</summary>
        public RectF ContentBox => _style.ContentBox(BorderBox);

        /// <summary>The border box (or the padding box) as a convex polygon in box px: overflow: hidden with a radius (VectorGraphic.ClipConvex of a child).</summary>
        public List<Vec2> ClipPolygon(bool paddingBox = false) => BoxMesh.ClipPolygon(BorderBox, _style, paddingBox);

        protected override void BuildMesh(MeshBuilder mb, RectF box, float feather)
        {
            if (_style == null || box.W <= 0f || box.H <= 0f) return;
            BoxMesh.Build(box, _style, feather, mb);
        }
    }
}
