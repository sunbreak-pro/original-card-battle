// CSS text-decoration drawn from a label's laid-out glyphs (kit.md §4.9, §3.2): TMP's own underline and strikethrough
// have the font's thickness and cannot be dotted, so the lines are vector graphics.
// - underline: top = baseline + |underline position| × size + text-underline-offset (kit.md §4.9), `thickness` tall;
//   dotted = thickness × thickness squares every 2 × thickness (Chrome: 2 px dots every 4 px, measured);
// - line-through: centred two thirds of the ascent below the top of the text box, as Blink paints it (2 · ascent / 3 −
//   thickness / 2 from the box top, the ascent in whole px), i.e. round(ascent px) / 3 above the baseline: 9.33 px on
//   Zen Kaku Gothic New 24 px. The kit.md §3.2 probe (the browser's default sans-serif, 24/32) has its 2 px line on rows
//   16–17 below the line box top, which this rule gives for that face's metrics. LineThroughOverrideEm replaces it.
// One run per line of the decorated range, from the first glyph's pen position to the last glyph's advance. The decor is
// a child of the label (drawn over the text), coloured with the label's colour unless given one (a tint: no rebuild).
//
//   TextDecor.Underline(label, dotted: true, color: DsTokens.InkMuted)    // the card's term: underline dotted 2px, offset 4px
//   TextDecor.LineThrough(label, thickness: 3, color: DsTokens.Miss)       // the miss float (field.css:108)
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;
using UnityEngine.UI;

namespace Trial.Kit
{
    [AddComponentMenu("")]
    public sealed class TextDecor : VectorGraphic
    {
        /// <summary>
        /// A calibration override: the line-through's centre above the baseline in em, for every face. NaN (the default)
        /// = Blink's rule, round(ascent px) / 3 (LineThroughAbove).
        /// </summary>
        public static float LineThroughOverrideEm = float.NaN;

        private TMP_Text _label;
        private DecorationLine _line = DecorationLine.Underline;
        private BorderStyle _style = BorderStyle.Solid;
        private float _thickness = 2f;
        private float _offset;
        private Rgba? _color;
        private int _start;
        private int _length = -1;
        private bool _textDirty;

        public TMP_Text Label => _label;

        // ---- making ---------------------------------------------------------------------------------------

        /// <summary>
        /// text-decoration: underline [dotted] on chars [start, start + length) (length −1 = to the end): thickness px,
        /// text-underline-offset px; colour null = the label's colour.
        /// </summary>
        public static TextDecor Underline(TMP_Text label, int start = 0, int length = -1, float thickness = 2f, float offset = 4f,
            bool dotted = false, Rgba? color = null)
        {
            TextDecor d = Make(label, "underline");
            d.Set(DecorationLine.Underline, dotted ? BorderStyle.Dotted : BorderStyle.Solid, thickness, offset, color, start, length);
            return d;
        }

        /// <summary>text-decoration: line-through thickness px on chars [start, start + length).</summary>
        public static TextDecor LineThrough(TMP_Text label, int start = 0, int length = -1, float thickness = 2f, Rgba? color = null)
        {
            TextDecor d = Make(label, "line-through");
            d.Set(DecorationLine.LineThrough, BorderStyle.Solid, thickness, 0f, color, start, length);
            return d;
        }

        /// <summary>The decoration the label's TextStyle declares (text-decoration line, style, colour, thickness, offset); null when none.</summary>
        public static TextDecor FromStyle(TMP_Text label)
        {
            TextStyle s = global::Trial.Kit.Label.StyleOf(label);
            if (s == null || s.Decoration == DecorationLine.None) return null;
            TextDecor d = Make(label, s.Decoration == DecorationLine.LineThrough ? "line-through" : "underline");
            d.Set(s.Decoration, s.DecorationStyle, s.DecorationThickness ?? 2f, s.UnderlineOffset ?? 0f, s.DecorationColor, 0, -1);
            return d;
        }

        private static TextDecor Make(TMP_Text label, string name)
        {
            if (!label) throw new ArgumentNullException(nameof(label));
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = label.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(label.rectTransform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = label.rectTransform.pivot;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            TextDecor d = go.AddComponent<TextDecor>();
            d._label = label;
            return d;
        }

        /// <summary>Changes the decoration (rebuilds).</summary>
        public void Set(DecorationLine line, BorderStyle style, float thickness, float offset, Rgba? color, int start = 0, int length = -1)
        {
            _line = line;
            _style = style;
            _thickness = thickness > 0f ? thickness : 2f;
            _offset = offset;
            _color = color;
            _start = Math.Max(0, start);
            _length = length;
            ApplyColor();
            SetGeometryDirty();
        }

        /// <summary>text-decoration-color (null = currentColor, the label's colour): a tint.</summary>
        public Rgba? DecorationColor
        {
            get => _color;
            set { _color = value; ApplyColor(); }
        }

        private void ApplyColor()
        {
            Rgba c = _color ?? (global::Trial.Kit.Label.StyleOf(_label)?.Color ?? DsTokens.Ink);
            BuildTint = c;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        }

        protected override void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            base.OnDisable();
        }

        private void OnTextChanged(UnityEngine.Object o)
        {
            if (!_label || o != _label) return;
            if (CanvasUpdateRegistry.IsRebuildingGraphics() || CanvasUpdateRegistry.IsRebuildingLayout()) _textDirty = true;
            else SetGeometryDirty();
        }

        private void LateUpdate()
        {
            if (!_label) return;
            if (_color == null) ApplyColor();   // currentColor follows the label
            if (_textDirty)
            {
                _textDirty = false;
                SetGeometryDirty();
            }
        }

        // ---- the lines -------------------------------------------------------------------------------------

        protected override void BuildMesh(MeshBuilder mb, RectF box, float feather)
        {
            if (!_label || _line == DecorationLine.None) return;
            TMP_TextInfo info = _label.textInfo;
            if ((info == null || info.characterCount == 0) && !string.IsNullOrEmpty(_label.text))
            {
                _label.ForceMeshUpdate();
                info = _label.textInfo;
            }
            if (info == null || info.characterCount == 0) return;
            TextStyle style = global::Trial.Kit.Label.StyleOf(_label) ?? new TextStyle();
            FontMetrics m = TrialFonts.MetricsOf(style.Family, style.Weight);
            int end = _length < 0 ? info.characterCount : Math.Min(info.characterCount, _start + _length);
            Transform from = _label.transform, to = transform;

            int i = _start;
            while (i < end)
            {
                int line = info.characterInfo[i].lineNumber;
                int first = -1, last = -1;
                int j = i;
                for (; j < end && info.characterInfo[j].lineNumber == line; j++)
                {
                    char ch = info.characterInfo[j].character;
                    if (ch == '\n' || ch == '\r') continue;
                    if (first < 0) first = j;
                    last = j;
                }
                i = j;
                if (first < 0) continue;
                TMP_CharacterInfo a = info.characterInfo[first], b = info.characterInfo[last];
                float size = a.pointSize > 0f ? a.pointSize : style.Size;
                float x0 = a.origin;
                float x1 = b.xAdvance > b.origin ? b.xAdvance : b.topRight.x;
                float baseline = a.baseLine;
                float top, bottom;   // local y, up
                if (_line == DecorationLine.LineThrough)
                {
                    float c = baseline + LineThroughAbove(m, size);
                    top = c + _thickness * 0.5f;
                    bottom = c - _thickness * 0.5f;
                }
                else if (_line == DecorationLine.Overline)
                {
                    top = baseline + m.AscentEm * size;
                    bottom = top - _thickness;
                }
                else
                {
                    top = baseline - (Math.Abs(m.UnderlinePosEm) * size + _offset);
                    bottom = top - _thickness;
                }
                Vec2 p0 = LocalToBox(to.InverseTransformPoint(from.TransformPoint(new Vector3(x0, top, 0f))));
                Vec2 p1 = LocalToBox(to.InverseTransformPoint(from.TransformPoint(new Vector3(x1, bottom, 0f))));
                Run(mb, RectF.FromLtrb(Math.Min(p0.X, p1.X), Math.Min(p0.Y, p1.Y), Math.Max(p0.X, p1.X), Math.Max(p0.Y, p1.Y)), feather);
            }
        }

        /// <summary>
        /// The line-through's centre above the baseline in px: Blink's 2 · ascent / 3 below the text box top, the ascent
        /// rounded to whole px as Blink rounds it (so round(ascent px) / 3 above the baseline); or the override.
        /// </summary>
        public static float LineThroughAbove(FontMetrics m, float size)
        {
            if (!float.IsNaN(LineThroughOverrideEm)) return LineThroughOverrideEm * size;
            float ascent = (float)Math.Floor(m.AscentEm * size + 0.5f);
            return ascent / 3f;
        }

        /// <summary>One line's run in white (the colour is the tint).</summary>
        private void Run(MeshBuilder mb, RectF r, float feather)
        {
            if (r.W <= 0f || r.H <= 0f) return;
            var white = new SolidPaint(Rgba.White);
            switch (_style)
            {
                case BorderStyle.Dotted:
                case BorderStyle.Dashed:
                {
                    float on = _style == BorderStyle.Dotted ? r.H : 3f * r.H;
                    float off = _style == BorderStyle.Dotted ? r.H : 2f * r.H;
                    for (float x = r.X; x < r.Right - 1e-3f; x += on + off)
                        Quad(mb, RectF.FromLtrb(x, r.Y, Math.Min(r.Right, x + on), r.Bottom), white, feather);
                    break;
                }
                case BorderStyle.Double:
                {
                    float h = r.H;
                    Quad(mb, new RectF(r.X, r.Y, r.W, h), white, feather);
                    Quad(mb, new RectF(r.X, r.Y + 2f * h, r.W, h), white, feather);
                    break;
                }
                default:
                    Quad(mb, r, white, feather);
                    break;
            }
        }

        private static void Quad(MeshBuilder mb, RectF r, Paint p, float feather)
        {
            var poly = new Polyline(new List<Vec2> { r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft }, true);
            Shapes.FillConvexAA(poly, p, feather, mb);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            LineThroughOverrideEm = float.NaN;
        }
    }
}
