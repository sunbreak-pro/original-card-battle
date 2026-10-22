// CSS text-shadow with a large blur (the `0 0 6–8px ground` halos, the `0 0 12–34px` glows; kit.md §4.9 path 2):
// TMP's SDF underlay reaches only a few px, so these are drawn as a graphic behind the label: one soft rectangle per
// visible glyph (its ink box from textInfo), the exact Gaussian blur of that rectangle (σ = blur / 2, measured) times
// InkFactor, because a glyph covers only part of its box and neighbouring blobs overlap.
//
// The glow is the label's previous sibling (uGUI draws siblings in order, so it lies under the text) and mirrors the
// label's RectTransform and its own opacity every frame; it rebuilds when the label's text is laid out again. Shadows
// are animatable (SetShadows: the explore roll keyframe, the title menu's `text-shadow .2s`); any blur is drawn here,
// so an animation may pass through small blurs too.
//
//   TextGlow.For(label)                                   // the label's own text-shadows above the underlay limit
//   TextGlow.For(label, new[] { new TextShadow(0, 0, 12, color) })
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;
using UnityEngine.UI;

namespace Trial.Kit
{
    [AddComponentMenu("")]
    public sealed class TextGlow : VectorGraphic
    {
        private const float DefaultInkFactor = 0.6f;

        /// <summary>The share of a glyph's box its ink covers, for the glow's strength (kit.md §4.9: ~.6; the gallery calibrates it).</summary>
        public static float InkFactor = DefaultInkFactor;

        /// <summary>Blur profile stations around each edge, in σ (the blurred edge is sampled there and interpolated linearly).</summary>
        private static readonly float[] Stations = { -3f, -2f, -1.25f, -0.6f, 0f, 0.6f, 1.25f, 2f, 3f };

        private TMP_Text _label;
        private readonly List<TextShadow> _shadows = new List<TextShadow>();
        private bool _textDirty;

        /// <summary>The label the glow is behind.</summary>
        public TMP_Text Label => _label;

        /// <summary>The text-shadow layers drawn (CSS order: the first on top).</summary>
        public IReadOnlyList<TextShadow> Shadows => _shadows;

        /// <summary>
        /// The glow of a label (made when missing, as the label's previous sibling). shadows null = the label's own
        /// text-shadows that the underlay leaves (Label.GlowShadows).
        /// </summary>
        public static TextGlow For(TMP_Text label, IReadOnlyList<TextShadow> shadows = null)
        {
            if (!label) throw new ArgumentNullException(nameof(label));
            GlowLink link = label.GetComponent<GlowLink>();
            TextGlow g = link ? link.Glow : null;
            if (!g)
            {
                var go = new GameObject("glow " + label.name, typeof(RectTransform));
                go.layer = label.gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(label.transform.parent, false);
                rt.SetSiblingIndex(label.transform.GetSiblingIndex());
                g = go.AddComponent<TextGlow>();
                g._label = label;
                if (!link) link = label.gameObject.AddComponent<GlowLink>();
                link.Glow = g;
                g.Mirror();
            }
            g.SetShadows(shadows ?? global::Trial.Kit.Label.GlowShadows(label));
            return g;
        }

        /// <summary>The glow of a label, or null.</summary>
        public static TextGlow Of(TMP_Text label)
        {
            GlowLink link = label ? label.GetComponent<GlowLink>() : null;
            return link && link.Glow ? link.Glow : null;
        }

        /// <summary>text-shadow layers (CSS order); empty or null = none.</summary>
        public void SetShadows(IReadOnlyList<TextShadow> shadows)
        {
            _shadows.Clear();
            if (shadows != null) _shadows.AddRange(shadows);
            SetGeometryDirty();
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
            // TMP lays text out inside the canvas rebuild, where a graphic may not ask for a rebuild: next frame then.
            if (CanvasUpdateRegistry.IsRebuildingGraphics() || CanvasUpdateRegistry.IsRebuildingLayout()) _textDirty = true;
            else SetGeometryDirty();
        }

        private void LateUpdate()
        {
            if (!_label)
            {
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }
            Mirror();
            if (_textDirty)
            {
                _textDirty = false;
                SetGeometryDirty();
            }
        }

        /// <summary>Takes the label's place: its RectTransform, its own opacity, whether it shows.</summary>
        private void Mirror()
        {
            RectTransform a = _label.rectTransform, b = rectTransform;
            if (b.parent != a.parent) b.SetParent(a.parent, false);
            int li = a.GetSiblingIndex(), gi = b.GetSiblingIndex();
            if (gi != li - 1) b.SetSiblingIndex(gi < li ? li - 1 : li);
            if (b.anchorMin != a.anchorMin) b.anchorMin = a.anchorMin;
            if (b.anchorMax != a.anchorMax) b.anchorMax = a.anchorMax;
            if (b.pivot != a.pivot) b.pivot = a.pivot;
            if (b.sizeDelta != a.sizeDelta) b.sizeDelta = a.sizeDelta;
            if (b.anchoredPosition != a.anchoredPosition) b.anchoredPosition = a.anchoredPosition;
            if (b.localRotation != a.localRotation) b.localRotation = a.localRotation;
            if (b.localScale != a.localScale) b.localScale = a.localScale;
            CanvasGroup cg = _label.GetComponent<CanvasGroup>();
            bool shown = _label.gameObject.activeSelf && _label.enabled;
            float alpha = !shown ? 0f : cg ? cg.alpha : 1f;
            if (Math.Abs(alpha - Opacity) > 1e-4f) Opacity = alpha;
        }

        protected override void BuildMesh(MeshBuilder mb, RectF box, float feather)
        {
            if (!_label || _shadows.Count == 0) return;
            TMP_TextInfo info = _label.textInfo;
            if ((info == null || info.characterCount == 0) && !string.IsNullOrEmpty(_label.text))
            {
                _label.ForceMeshUpdate();
                info = _label.textInfo;
            }
            if (info == null || info.characterCount == 0) return;
            Transform from = _label.transform, to = transform;
            for (int s = _shadows.Count - 1; s >= 0; s--)
            {
                TextShadow sh = _shadows[s];
                if (sh.Color.A <= 0f) continue;
                Rgba col = sh.Color.MulAlpha(InkFactor);
                for (int i = 0; i < info.characterCount && i < info.characterInfo.Length; i++)
                {
                    TMP_CharacterInfo ci = info.characterInfo[i];
                    if (!ci.isVisible) continue;
                    InkBox(ci, out Vector3 inkBl, out Vector3 inkTr);
                    Vec2 bl = LocalToBox(to.InverseTransformPoint(from.TransformPoint(inkBl)));
                    Vec2 tr = LocalToBox(to.InverseTransformPoint(from.TransformPoint(inkTr)));
                    RectF g = RectF.FromLtrb(Math.Min(bl.X, tr.X) + sh.X, Math.Min(bl.Y, tr.Y) + sh.Y, Math.Max(bl.X, tr.X) + sh.X, Math.Max(bl.Y, tr.Y) + sh.Y);
                    SoftRect(mb, g, sh.Sigma, col, feather);
                }
            }
        }

        /// <summary>
        /// A glyph's ink box in the label's local space: from its glyph metrics (the quad TMP stores in bottomLeft /
        /// topRight is grown by the SDF padding, 5 px a side on 20 px text at the kit's padding 16).
        /// </summary>
        public static void InkBox(TMP_CharacterInfo ci, out Vector3 bottomLeft, out Vector3 topRight)
        {
            bottomLeft = ci.bottomLeft;
            topRight = ci.topRight;
            if (ci.textElement == null || ci.textElement.glyph == null || !(ci.scale > 0f)) return;
            UnityEngine.TextCore.GlyphMetrics m = ci.textElement.glyph.metrics;
            if (!(m.width > 0f) || !(m.height > 0f)) return;
            float left = ci.origin + m.horizontalBearingX * ci.scale;
            float top = ci.baseLine + m.horizontalBearingY * ci.scale;
            bottomLeft = new Vector3(left, top - m.height * ci.scale, 0f);
            topRight = new Vector3(left + m.width * ci.scale, top, 0f);
        }

        /// <summary>
        /// The Gaussian blur (σ) of a filled rectangle as a vertex grid: alpha = c.A · Bx(x) · By(y), with
        /// B(x) = Φ((x − left)/σ) − Φ((x − right)/σ) sampled at fixed stations around each edge. σ = 0: the sharp rectangle.
        /// </summary>
        public static void SoftRect(MeshBuilder mb, RectF r, float sigma, Rgba c, float feather = 0f)
        {
            if (r.W <= 0f || r.H <= 0f || c.A <= 0f) return;
            if (!(sigma > 0f))
            {
                var outline = new Polyline(new List<Vec2> { r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft }, true);
                Shapes.FillConvexAA(outline, new SolidPaint(c), feather, mb);
                return;
            }
            List<float> xs = Axis(r.Left, r.Right, sigma), ys = Axis(r.Top, r.Bottom, sigma);
            var ax = new float[xs.Count];
            var ay = new float[ys.Count];
            for (int i = 0; i < xs.Count; i++) ax[i] = Blur.Phi((xs[i] - r.Left) / sigma) - Blur.Phi((xs[i] - r.Right) / sigma);
            for (int j = 0; j < ys.Count; j++) ay[j] = Blur.Phi((ys[j] - r.Top) / sigma) - Blur.Phi((ys[j] - r.Bottom) / sigma);
            int nx = xs.Count, b0 = mb.VertexCount;
            for (int j = 0; j < ys.Count; j++)
                for (int i = 0; i < nx; i++)
                    mb.AddVertex(new Vec2(xs[i], ys[j]), c.MulAlpha(Math.Max(0f, ax[i] * ay[j])));
            for (int j = 0; j + 1 < ys.Count; j++)
                for (int i = 0; i + 1 < nx; i++)
                {
                    int a = b0 + j * nx + i, b = a + 1, d = a + nx, e = d + 1;
                    mb.AddTri(a, b, e);
                    mb.AddTri(a, e, d);
                }
        }

        private static List<float> Axis(float lo, float hi, float sigma)
        {
            var v = new List<float>(Stations.Length * 2);
            foreach (float k in Stations) v.Add(lo + k * sigma);
            foreach (float k in Stations) v.Add(hi + k * sigma);
            v.Sort();
            var o = new List<float>(v.Count);
            float min = 0.2f * sigma;
            foreach (float x in v)
                if (o.Count == 0 || x - o[o.Count - 1] > min) o.Add(x);
            // the outermost stations stay (3σ out on both sides)
            if (o[o.Count - 1] < hi + 3f * sigma - 1e-3f) o.Add(hi + 3f * sigma);
            return o;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            InkFactor = DefaultInkFactor;
        }
    }

    /// <summary>On a label: its TextGlow (so the glow is found again).</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class GlowLink : MonoBehaviour
    {
        public TextGlow Glow;

        private void OnDestroy()
        {
            if (Glow) Destroy(Glow.gameObject);
        }
    }
}
