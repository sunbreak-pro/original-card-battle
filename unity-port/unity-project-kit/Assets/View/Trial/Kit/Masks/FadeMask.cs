// CSS mask-image edge fades (`linear-gradient(180deg, var(--ground) 82%, transparent)` and the like on the scroll
// lists, `90deg/270deg, transparent 0%, #000 22%` on an fx streak) as per-vertex alpha ramps.
//
// Why ramps and not RectMask2D softness: softness fades every edge of the clip rect by the same px, so a one-sided
// fade (is-more-below: only the bottom 18 %) or an uneven one (the deck list's 24 % top / 14 % bottom) cannot be
// drawn with it, and nesting a second RectMask2D does not help (the clip rect is the intersection, the softness the
// inner one's). The ramp is exact for any stops on either axis: every graphic below the mask has its triangles split
// at the ramp's kinks and its vertex alpha multiplied by the ramp (FadeVertex); TMP labels get the same in
// OnPreRenderText (FadeText). The price is a re-mesh of what is below on every scroll step (the lists are short).
// Hard clipping of the box (overflow: hidden) stays RectMask2D's job (ScrollArea puts one on its viewport).
// Like CanvasGroup opacity, the alpha applies per graphic, not to the composited subtree (kit.md §6 risk 2).
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using Trial.Geometry;
using UnityEngine;
using UnityEngine.UI;

namespace Trial.Kit
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class FadeMask : MonoBehaviour
    {
        private readonly List<(float At, float Alpha)> _stops = new List<(float, float)>();
        private bool _vertical = true;
        private bool _dirty = true;
        private static readonly List<Graphic> GraphicBuffer = new List<Graphic>();

        public RectTransform Rect => (RectTransform)transform;

        /// <summary>Top → bottom (true) or left → right (false).</summary>
        public bool Vertical => _vertical;

        /// <summary>The ramp: (position 0..1 along the axis, alpha 0..1), ascending. Empty = no mask.</summary>
        public IReadOnlyList<(float At, float Alpha)> Stops => _stops;

        /// <summary>Whether the ramp changes anything (some stop below alpha 1).</summary>
        public bool IsActive
        {
            get
            {
                foreach (var s in _stops) if (s.Alpha < 0.9999f) return true;
                return false;
            }
        }

        /// <summary>The FadeMask on rt (added when missing).</summary>
        public static FadeMask Of(RectTransform rt)
        {
            FadeMask m = rt.GetComponent<FadeMask>();
            return m ? m : rt.gameObject.AddComponent<FadeMask>();
        }

        /// <summary>Sets the ramp directly; positions are fractions of the box along the axis.</summary>
        public void SetStops(bool vertical, params (float At, float Alpha)[] stops)
        {
            _vertical = vertical;
            _stops.Clear();
            if (stops != null) _stops.AddRange(stops);
            _stops.Sort((a, b) => a.At.CompareTo(b.At));
            MarkDirty();
        }

        /// <summary>No mask (mask-image: none).</summary>
        public void Clear()
        {
            if (_stops.Count == 0) return;
            _stops.Clear();
            MarkDirty();
        }

        /// <summary>
        /// The value of a CSS mask-image linear-gradient (with or without "linear-gradient(...)"): an angle of 0, 90, 180
        /// or 270 deg, then colour stops whose alpha is the mask (transparent 0, a colour its alpha). Positions in % (or
        /// none: spread evenly, as CSS does). "none" clears.
        /// </summary>
        public void SetCss(string maskImage)
        {
            if (string.IsNullOrWhiteSpace(maskImage) || maskImage.Trim() == "none") { Clear(); return; }
            if (!TryParseCss(maskImage, out bool vertical, out List<(float, float)> stops))
            {
                Debug.LogWarning("[FadeMask] not a mask gradient: " + maskImage);
                Clear();
                return;
            }
            SetStops(vertical, stops.ToArray());
        }

        /// <summary>Parses a mask linear-gradient into top→bottom / left→right alpha stops.</summary>
        public static bool TryParseCss(string css, out bool vertical, out List<(float At, float Alpha)> stops)
        {
            vertical = true;
            stops = new List<(float, float)>();
            string s = css.Trim();
            int open = s.IndexOf('(');
            if (s.StartsWith("linear-gradient", StringComparison.Ordinal) && open >= 0 && s.EndsWith(")", StringComparison.Ordinal))
                s = s.Substring(open + 1, s.Length - open - 2);
            List<string> parts = SplitTopLevel(s);
            if (parts.Count == 0) return false;
            float angle = 180f;
            int first = 0;
            string p0 = parts[0].Trim();
            if (p0.EndsWith("deg", StringComparison.Ordinal) &&
                float.TryParse(p0.Substring(0, p0.Length - 3), NumberStyles.Float, CultureInfo.InvariantCulture, out float a))
            {
                angle = a;
                first = 1;
            }
            else if (p0.StartsWith("to ", StringComparison.Ordinal))
            {
                string dir = p0.Substring(3).Trim();
                angle = dir == "top" ? 0f : dir == "right" ? 90f : dir == "left" ? 270f : 180f;
                first = 1;
            }
            angle = ((angle % 360f) + 360f) % 360f;
            bool reverse;
            if (Math.Abs(angle - 180f) < 0.01f) { vertical = true; reverse = false; }
            else if (angle < 0.01f || Math.Abs(angle - 360f) < 0.01f) { vertical = true; reverse = true; }
            else if (Math.Abs(angle - 90f) < 0.01f) { vertical = false; reverse = false; }
            else if (Math.Abs(angle - 270f) < 0.01f) { vertical = false; reverse = true; }
            else return false;

            var raw = new List<(float? At, float Alpha)>();
            for (int i = first; i < parts.Count; i++)
            {
                string part = parts[i].Trim();
                if (part.Length == 0) continue;
                float? pos = null;
                string colour = part;
                int sp = LastSpaceOutsideParens(part);
                if (sp > 0)
                {
                    string tail = part.Substring(sp + 1).Trim();
                    if (tail.EndsWith("%", StringComparison.Ordinal) &&
                        float.TryParse(tail.Substring(0, tail.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float pc))
                    {
                        pos = pc / 100f;
                        colour = part.Substring(0, sp).Trim();
                    }
                }
                if (!CssColor.TryParse(colour, out Rgba col)) return false;
                raw.Add((pos, col.A));
            }
            if (raw.Count < 2) return false;
            // CSS: a missing first / last position is 0 % / 100 %; missing ones in between are spread evenly.
            if (!raw[0].At.HasValue) raw[0] = (0f, raw[0].Alpha);
            if (!raw[raw.Count - 1].At.HasValue) raw[raw.Count - 1] = (1f, raw[raw.Count - 1].Alpha);
            for (int i = 1; i < raw.Count; i++)
            {
                if (raw[i].At.HasValue) continue;
                int j = i;
                while (!raw[j].At.HasValue) j++;
                float from = raw[i - 1].At.Value, to = raw[j].At.Value;
                for (int k = i; k < j; k++) raw[k] = (from + (to - from) * (k - i + 1) / (j - i + 1), raw[k].Alpha);
            }
            float last = 0f;
            foreach (var r in raw)
            {
                float at = Math.Max(last, r.At.Value); // a position below an earlier one is clamped to it
                last = at;
                stops.Add((reverse ? 1f - at : at, r.Alpha));
            }
            stops.Sort((x, y) => x.Item1.CompareTo(y.Item1));
            return true;
        }

        private static List<string> SplitTopLevel(string s)
        {
            var list = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0) { list.Add(s.Substring(start, i - start)); start = i + 1; }
            }
            list.Add(s.Substring(start));
            return list;
        }

        private static int LastSpaceOutsideParens(string s)
        {
            int depth = 0;
            for (int i = s.Length - 1; i >= 0; i--)
            {
                char c = s[i];
                if (c == ')') depth++;
                else if (c == '(') depth--;
                else if (c == ' ' && depth == 0) return i;
            }
            return -1;
        }

        /// <summary>The mask's alpha at a fraction along the axis (clamped to the end stops, as CSS does).</summary>
        public float AlphaAt(float frac)
        {
            int n = _stops.Count;
            if (n == 0) return 1f;
            if (frac <= _stops[0].At) return _stops[0].Alpha;
            if (frac >= _stops[n - 1].At) return _stops[n - 1].Alpha;
            for (int i = 1; i < n; i++)
            {
                if (frac > _stops[i].At) continue;
                var a = _stops[i - 1];
                var b = _stops[i];
                float span = b.At - a.At;
                return span <= 0f ? b.Alpha : a.Alpha + (b.Alpha - a.Alpha) * (frac - a.At) / span;
            }
            return _stops[n - 1].Alpha;
        }

        /// <summary>The fraction along the axis of a world point.</summary>
        public float FractionOf(Vector3 world)
        {
            Vector3 l = Rect.InverseTransformPoint(world);
            Rect r = Rect.rect;
            if (_vertical) return r.height > 0f ? (r.yMax - l.y) / r.height : 0f;
            return r.width > 0f ? (l.x - r.xMin) / r.width : 0f;
        }

        /// <summary>Re-applies the ramp to everything below at the end of the frame (call after scrolling or changing the content).</summary>
        public void MarkDirty() => _dirty = true;

        private void LateUpdate()
        {
            if (_dirty) Refresh();
        }

        private void OnEnable() => _dirty = true;

        private void OnTransformChildrenChanged() => _dirty = true;

        /// <summary>Re-applies now: every graphic below gets its modifier and is re-meshed.</summary>
        public void Refresh()
        {
            _dirty = false;
            GetComponentsInChildren(true, GraphicBuffer);
            bool active = IsActive;
            foreach (Graphic g in GraphicBuffer)
            {
                if (!g) continue;
                if (g is TMP_Text t)
                {
                    FadeText ft = t.GetComponent<FadeText>();
                    if (!ft)
                    {
                        if (!active) continue;
                        ft = t.gameObject.AddComponent<FadeText>();
                    }
                    ft.Mask = this;
                    t.havePropertiesChanged = true;
                    t.SetVerticesDirty();
                }
                else
                {
                    FadeVertex fv = g.GetComponent<FadeVertex>();
                    if (!fv)
                    {
                        if (!active) continue;
                        fv = g.gameObject.AddComponent<FadeVertex>();
                    }
                    fv.Mask = this;
                    g.SetVerticesDirty();
                }
            }
            GraphicBuffer.Clear();
        }

        // ---- triangle splitting (for FadeVertex) -------------------------------------------------------------

        private static readonly List<UIVertex> Stream = new List<UIVertex>();
        private static readonly List<UIVertex> Out = new List<UIVertex>();
        private static readonly List<UIVertex> PolyA = new List<UIVertex>();

        /// <summary>Splits a graphic's triangles at the ramp's kinks and multiplies their alpha by the ramp.</summary>
        internal void ApplyTo(Graphic g, VertexHelper vh)
        {
            if (!IsActive || vh.currentVertCount == 0) return;
            Transform gt = g.transform;
            // fraction = fx·x + fy·y + f0 in the graphic's local space (exact for affine transforms)
            float f0 = FractionOf(gt.TransformPoint(Vector3.zero));
            float fx = FractionOf(gt.TransformPoint(Vector3.right)) - f0;
            float fy = FractionOf(gt.TransformPoint(Vector3.up)) - f0;
            Stream.Clear();
            vh.GetUIVertexStream(Stream);
            Out.Clear();
            for (int i = 0; i + 2 < Stream.Count; i += 3)
            {
                PolyA.Clear();
                PolyA.Add(Stream[i]);
                PolyA.Add(Stream[i + 1]);
                PolyA.Add(Stream[i + 2]);
                SplitAndEmit(PolyA, 0, f0, fx, fy);
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(Out);
            Stream.Clear();
            Out.Clear();
        }

        private float Frac(UIVertex v, float f0, float fx, float fy) => f0 + fx * v.position.x + fy * v.position.y;

        // Cuts the convex polygon at the kinks from index k on; emits a fan of the pieces with the ramp applied.
        private void SplitAndEmit(List<UIVertex> poly, int k, float f0, float fx, float fy)
        {
            for (; k < _stops.Count; k++)
            {
                float cut = _stops[k].At;
                bool below = false, above = false;
                foreach (UIVertex v in poly)
                {
                    float f = Frac(v, f0, fx, fy);
                    if (f < cut - 1e-5f) below = true;
                    else if (f > cut + 1e-5f) above = true;
                }
                if (!(below && above)) continue;
                var lo = new List<UIVertex>();
                var hi = new List<UIVertex>();
                for (int i = 0; i < poly.Count; i++)
                {
                    UIVertex a = poly[i], b = poly[(i + 1) % poly.Count];
                    float fa = Frac(a, f0, fx, fy) - cut, fb = Frac(b, f0, fx, fy) - cut;
                    if (fa <= 0f) lo.Add(a);
                    if (fa >= 0f) hi.Add(a);
                    if ((fa < 0f && fb > 0f) || (fa > 0f && fb < 0f))
                    {
                        UIVertex m = Lerp(a, b, fa / (fa - fb));
                        lo.Add(m);
                        hi.Add(m);
                    }
                }
                if (lo.Count >= 3) SplitAndEmit(lo, k + 1, f0, fx, fy);
                if (hi.Count >= 3) SplitAndEmit(hi, k + 1, f0, fx, fy);
                return;
            }
            for (int i = 1; i + 1 < poly.Count; i++)
            {
                Out.Add(Faded(poly[0], f0, fx, fy));
                Out.Add(Faded(poly[i], f0, fx, fy));
                Out.Add(Faded(poly[i + 1], f0, fx, fy));
            }
        }

        private UIVertex Faded(UIVertex v, float f0, float fx, float fy)
        {
            float a = AlphaAt(Frac(v, f0, fx, fy));
            Color32 c = v.color;
            c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * a), 0, 255);
            v.color = c;
            return v;
        }

        private static UIVertex Lerp(UIVertex a, UIVertex b, float t)
        {
            var v = new UIVertex
            {
                position = Vector3.LerpUnclamped(a.position, b.position, t),
                normal = Vector3.LerpUnclamped(a.normal, b.normal, t),
                tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, t),
                color = Color32.LerpUnclamped(a.color, b.color, t),
                uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t),
                uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, t),
                uv2 = Vector4.LerpUnclamped(a.uv2, b.uv2, t),
                uv3 = Vector4.LerpUnclamped(a.uv3, b.uv3, t),
            };
            return v;
        }

        // ---- TMP (for FadeText) -------------------------------------------------------------------------------

        internal void ApplyTo(TMP_Text t, TMP_TextInfo info)
        {
            if (!IsActive || info == null || info.meshInfo == null) return;
            Transform tt = t.transform;
            float f0 = FractionOf(tt.TransformPoint(Vector3.zero));
            float fx = FractionOf(tt.TransformPoint(Vector3.right)) - f0;
            float fy = FractionOf(tt.TransformPoint(Vector3.up)) - f0;
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                TMP_MeshInfo mi = info.meshInfo[m];
                if (mi.vertices == null || mi.colors32 == null) continue;
                int n = Math.Min(mi.vertexCount, Math.Min(mi.vertices.Length, mi.colors32.Length));
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = mi.vertices[i];
                    float a = AlphaAt(f0 + fx * p.x + fy * p.y);
                    if (a >= 0.9999f) continue;
                    Color32 c = mi.colors32[i];
                    c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * a), 0, 255);
                    mi.colors32[i] = c;
                }
            }
        }

        // Domain reload is off: the shared buffers must not hold the previous Play session's graphics.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GraphicBuffer.Clear();
            Stream.Clear();
            Out.Clear();
            PolyA.Clear();
        }

        private void OnDisable()
        {
            // Give the graphics below their full alpha back.
            GetComponentsInChildren(true, GraphicBuffer);
            foreach (Graphic g in GraphicBuffer)
            {
                if (!g) continue;
                if (g is TMP_Text t) { t.havePropertiesChanged = true; t.SetVerticesDirty(); }
                else g.SetVerticesDirty();
            }
            GraphicBuffer.Clear();
        }
    }

    /// <summary>The FadeMask ramp on a uGUI graphic's mesh (added by FadeMask).</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class FadeVertex : BaseMeshEffect
    {
        public FadeMask Mask;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || !Mask || !Mask.isActiveAndEnabled) return;
            if (!transform.IsChildOf(Mask.transform)) return;
            Mask.ApplyTo(graphic, vh);
        }
    }

    /// <summary>The FadeMask ramp on a TMP label's vertex colours (added by FadeMask).</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class FadeText : MonoBehaviour
    {
        public FadeMask Mask;
        private TMP_Text _t;

        private void OnEnable()
        {
            _t = GetComponent<TMP_Text>();
            if (_t) _t.OnPreRenderText += OnPreRender;
        }

        private void OnDisable()
        {
            if (_t) _t.OnPreRenderText -= OnPreRender;
        }

        private void OnPreRender(TMP_TextInfo info)
        {
            if (!Mask || !Mask.isActiveAndEnabled || !transform.IsChildOf(Mask.transform)) return;
            Mask.ApplyTo(_t, info);
        }
    }
}
