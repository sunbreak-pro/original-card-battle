// One element that moves (kit.md §4.10): its layout box in stage px and the CSS channels the scenes animate, composed
// into the RectTransform.
//
// - Box: X, Y (left / top in the parent's stage px), W, H. Anchor = (0,1) of the parent; the pivot follows Origin.
// - Transform channels: the CSS `transform` (TranslateX/Y px, TranslatePctX/Y of the own box as in translate(-50%),
//   ScaleX/Y, Rotate in CSS degrees, clockwise) and the separate CSS properties `translate`, `scale`, `rotate`
//   (PropTranslateX/Y, PropScaleX/Y, PropRotate; hud util.js pop / shake / scaleTo write those). They compose into one
//   TRS: translations add, scales multiply, rotations add, all about Origin (transform-origin, default 50% 50%).
// - Opacity: a CanvasGroup added on first use (CSS opacity; kit.md §6 risk 2: per graphic, not a composited group).
// - Filter: a ColorFilter pushed down to every IFilterTarget below (kit graphics, labels), composed with ancestors'.
// - Visible: display: none → SetActive(false).
// - PointerEvents: CSS pointer-events with inheritance: None turns raycastTarget off on every graphic below (an Auto
//   node under it keeps its own), Auto / Inherit give back the targets None turned off.
// - Z: CSS z-index among siblings; siblings re-sort stably by (Z, DOM order).
using System;
using System.Collections.Generic;
using Trial.Geometry;
using UnityEngine;
using UnityEngine.UI;

namespace Trial.Kit
{
    /// <summary>A graphic or label that takes the CSS filter of its ancestors (UiNode.Filter pushes it down).</summary>
    public interface IFilterTarget
    {
        void SetInheritedFilter(ColorFilter filter);
    }

    public enum PointerEvents { Inherit, Auto, None }

    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class UiNode : MonoBehaviour, ITweenable
    {
        private static double _domCounter;
        private static readonly HashSet<Graphic> Suppressed = new HashSet<Graphic>();

        private bool _captured;
        private float _x, _y, _w, _h;
        private float _tx, _ty, _tpx, _tpy, _sx = 1f, _sy = 1f, _rot;
        private float _ptx, _pty, _psx = 1f, _psy = 1f, _prot;
        private float _btx, _bty, _btpx, _btpy, _bsx = 1f, _bsy = 1f, _brot;
        private float _ox = 0.5f, _oy = 0.5f;
        private float _opacity = 1f;
        private CanvasGroup _group;
        private ColorFilter _filter = ColorFilter.None;
        private PointerEvents _pe = PointerEvents.Inherit;
        private int _z;
        private double _dom = double.NaN;

        // ---- access ------------------------------------------------------------------------------------

        /// <summary>The UiNode of rt, added when missing (the box is read from the RectTransform as it stands).</summary>
        public static UiNode Of(RectTransform rt)
        {
            if (rt == null) throw new ArgumentNullException(nameof(rt));
            UiNode n = rt.GetComponent<UiNode>();
            if (!n) n = rt.gameObject.AddComponent<UiNode>();
            n.Capture();
            return n;
        }

        public static UiNode Of(Component c) => Of((RectTransform)c.transform);
        public static UiNode Of(GameObject go) => Of((RectTransform)go.transform);

        public RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            if (double.IsNaN(_dom)) _dom = ++_domCounter;
        }

        /// <summary>Reads the box from the RectTransform once (a stretched rect becomes a fixed box of its current size).</summary>
        private void Capture()
        {
            if (_captured) return;
            _captured = true;
            if (double.IsNaN(_dom)) _dom = ++_domCounter;
            RectTransform rt = Rect;
            Vector2 size = rt.rect.size;
            bool point = rt.anchorMin == rt.anchorMax;
            if (point && rt.anchorMin == new Vector2(0f, 1f))
            {
                _w = rt.sizeDelta.x;
                _h = rt.sizeDelta.y;
                Vector2 p = rt.pivot;
                _x = rt.anchoredPosition.x - p.x * _w;
                _y = -rt.anchoredPosition.y - (1f - p.y) * _h;
            }
            else
            {
                // Its current box in the parent's top-left space.
                var parent = rt.parent as RectTransform;
                Vector3[] c = new Vector3[4];
                rt.GetWorldCorners(c); // bl, tl, tr, br
                if (parent)
                {
                    Vector3 tl = parent.InverseTransformPoint(c[1]);
                    Rect pr = parent.rect;
                    _x = tl.x - pr.xMin;
                    _y = pr.yMax - tl.y;
                }
                _w = size.x;
                _h = size.y;
            }
            // The pivot (0,1) of Ui.Place is a placement convention, not a transform-origin: CSS's default is 50% 50%.
            _ox = 0.5f;
            _oy = 0.5f;
            Apply();
        }

        // ---- box ---------------------------------------------------------------------------------------

        /// <summary>Left in stage px (CSS left).</summary>
        public float X { get { Capture(); return _x; } set { Capture(); _x = value; Apply(); } }

        /// <summary>Top in stage px (CSS top).</summary>
        public float Y { get { Capture(); return _y; } set { Capture(); _y = value; Apply(); } }

        public float W { get { Capture(); return _w; } set { Capture(); _w = value; Apply(); } }
        public float H { get { Capture(); return _h; } set { Capture(); _h = value; Apply(); } }

        /// <summary>stage.js place(el, x, y, w, h): left / top, and the size when given.</summary>
        public UiNode Place(float x, float y, float? w = null, float? h = null)
        {
            Capture();
            _x = x;
            _y = y;
            if (w.HasValue) _w = w.Value;
            if (h.HasValue) _h = h.Value;
            Apply();
            return this;
        }

        // ---- transform channels ------------------------------------------------------------------------

        /// <summary>CSS transform translate(x, ·) in px.</summary>
        public float TranslateX { get => _tx; set { Capture(); _tx = value; Apply(); } }
        /// <summary>CSS transform translate(·, y) in px, y down.</summary>
        public float TranslateY { get => _ty; set { Capture(); _ty = value; Apply(); } }
        /// <summary>CSS transform translate(p%, ·) as a fraction of the own width (−0.5 = translate(−50%)).</summary>
        public float TranslatePctX { get => _tpx; set { Capture(); _tpx = value; Apply(); } }
        /// <summary>CSS transform translate(·, p%) as a fraction of the own height.</summary>
        public float TranslatePctY { get => _tpy; set { Capture(); _tpy = value; Apply(); } }
        public float ScaleX { get => _sx; set { Capture(); _sx = value; Apply(); } }
        public float ScaleY { get => _sy; set { Capture(); _sy = value; Apply(); } }
        /// <summary>CSS transform scale(s): both axes.</summary>
        public float Scale { get => _sx; set { Capture(); _sx = value; _sy = value; Apply(); } }
        /// <summary>CSS transform rotate(deg): clockwise on screen.</summary>
        public float Rotate { get => _rot; set { Capture(); _rot = value; Apply(); } }

        /// <summary>The CSS `translate` property, x px.</summary>
        public float PropTranslateX { get => _ptx; set { Capture(); _ptx = value; Apply(); } }
        /// <summary>The CSS `translate` property, y px.</summary>
        public float PropTranslateY { get => _pty; set { Capture(); _pty = value; Apply(); } }
        /// <summary>The CSS `scale` property (both axes).</summary>
        public float PropScale { get => _psx; set { Capture(); _psx = value; _psy = value; Apply(); } }
        public float PropScaleX { get => _psx; set { Capture(); _psx = value; Apply(); } }
        public float PropScaleY { get => _psy; set { Capture(); _psy = value; Apply(); } }
        /// <summary>The CSS `rotate` property, degrees clockwise.</summary>
        public float PropRotate { get => _prot; set { Capture(); _prot = value; Apply(); } }

        /// <summary>CSS transform-origin as fractions of the box from the top-left (default 0.5, 0.5).</summary>
        public Vector2 Origin => new Vector2(_ox, _oy);

        /// <summary>transform-origin: the pivot moves, the layout box stays where it is.</summary>
        public UiNode SetOrigin(float ox, float oy)
        {
            Capture();
            _ox = ox;
            _oy = oy;
            Apply();
            return this;
        }

        /// <summary>The whole CSS transform at once: translate(tx px + pctX·W, ty px + pctY·H) scale(sx, sy) rotate(rot).</summary>
        public void SetTransform(float tx, float ty, float sx, float sy, float rot, float pctX = 0f, float pctY = 0f)
        {
            Capture();
            _tx = tx;
            _ty = ty;
            _sx = sx;
            _sy = sy;
            _rot = rot;
            _tpx = pctX;
            _tpy = pctY;
            Apply();
        }

        /// <summary>
        /// The stylesheet's transform (the CSS rule's `transform`, e.g. translate(-50%, -50%)). Inline writes (Styler,
        /// tweens, the Translate*/Scale/Rotate setters) replace it, as an inline style does; ResetTransform brings it back.
        /// </summary>
        public UiNode SetStyleTransform(float tx = 0f, float ty = 0f, float sx = 1f, float sy = 1f, float rot = 0f, float pctX = 0f, float pctY = 0f)
        {
            Capture();
            _btx = tx; _bty = ty; _bsx = sx; _bsy = sy; _brot = rot; _btpx = pctX; _btpy = pctY;
            _tx = tx; _ty = ty; _sx = sx; _sy = sy; _rot = rot; _tpx = pctX; _tpy = pctY;
            Apply();
            return this;
        }

        /// <summary>el.style.transform = '' : the inline transform goes, the stylesheet's (SetStyleTransform, else none) applies again; the CSS properties stay.</summary>
        public void ResetTransform()
        {
            Capture();
            _tx = _btx; _ty = _bty; _tpx = _btpx; _tpy = _btpy; _rot = _brot;
            _sx = _bsx; _sy = _bsy;
            Apply();
        }

        /// <summary>The separate CSS translate / scale / rotate properties back to none.</summary>
        public void ResetProps()
        {
            Capture();
            _ptx = _pty = _prot = 0f;
            _psx = _psy = 1f;
            Apply();
        }

        /// <summary>Writes the box and the channels into the RectTransform.</summary>
        public void Apply()
        {
            RectTransform rt = Rect;
            var tl = new Vector2(0f, 1f);
            if (rt.anchorMin != tl) rt.anchorMin = tl;
            if (rt.anchorMax != tl) rt.anchorMax = tl;
            rt.pivot = new Vector2(_ox, 1f - _oy);
            rt.sizeDelta = new Vector2(_w, _h);
            float tx = _tx + _ptx + _tpx * _w;
            float ty = _ty + _pty + _tpy * _h;
            rt.anchoredPosition = new Vector2(_x + _ox * _w + tx, -(_y + _oy * _h + ty));
            rt.localScale = new Vector3(_sx * _psx, _sy * _psy, 1f);
            rt.localEulerAngles = new Vector3(0f, 0f, -(_rot + _prot));
        }

        // ---- opacity, filter, visibility ---------------------------------------------------------------

        /// <summary>CSS opacity (a CanvasGroup on first use).</summary>
        public float Opacity
        {
            get => _opacity;
            set
            {
                _opacity = value;
                if (!_group)
                {
                    if (value >= 1f) return;
                    _group = GetComponent<CanvasGroup>();
                    if (!_group) _group = gameObject.AddComponent<CanvasGroup>();
                }
                _group.alpha = Mathf.Clamp01(value);
            }
        }

        /// <summary>CSS filter (brightness, grayscale) of this element; pushed down to every IFilterTarget below.</summary>
        public ColorFilter Filter
        {
            get => _filter;
            set
            {
                _filter = value;
                PushFilter(transform, FilterAbove(transform).Then(value), true);
            }
        }

        /// <summary>The filter in force at t: every ancestor UiNode's filter (and t's own), outermost first.</summary>
        public static ColorFilter FilterAt(Transform t)
        {
            if (t == null) return ColorFilter.None;
            UiNode n = t.GetComponent<UiNode>();
            ColorFilter above = FilterAbove(t);
            return n ? above.Then(n._filter) : above;
        }

        private static ColorFilter FilterAbove(Transform t)
        {
            var chain = new List<UiNode>();
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                UiNode n = p.GetComponent<UiNode>();
                if (n && !n._filter.IsNone) chain.Add(n);
            }
            ColorFilter f = ColorFilter.None;
            for (int i = chain.Count - 1; i >= 0; i--) f = f.Then(chain[i]._filter);
            return f;
        }

        private static readonly List<IFilterTarget> FilterTargets = new List<IFilterTarget>();

        private static void PushFilter(Transform t, ColorFilter effective, bool isStart)
        {
            if (!isStart)
            {
                UiNode n = t.GetComponent<UiNode>();
                if (n && !n._filter.IsNone) effective = effective.Then(n._filter);
            }
            t.GetComponents(FilterTargets);
            if (FilterTargets.Count > 0)
            {
                IFilterTarget[] targets = FilterTargets.ToArray();
                foreach (IFilterTarget ft in targets)
                {
                    try { ft.SetInheritedFilter(effective); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
            for (int i = 0; i < t.childCount; i++) PushFilter(t.GetChild(i), effective, false);
        }

        /// <summary>Pushes the filter in force here down to everything below again (after children were added).</summary>
        public void RefreshFilter() => PushFilter(transform, FilterAt(transform), true);

        /// <summary>display: none ↔ shown (SetActive).</summary>
        public bool Visible
        {
            get => gameObject.activeSelf;
            set { if (gameObject.activeSelf != value) gameObject.SetActive(value); }
        }

        // ---- pointer events ----------------------------------------------------------------------------

        /// <summary>CSS pointer-events of this element (Inherit = not set).</summary>
        public PointerEvents PointerEvents
        {
            get => _pe;
            set
            {
                _pe = value;
                RefreshPointerEvents();
            }
        }

        /// <summary>Whether pointer events reach t (the nearest explicit setting at or above t is not None).</summary>
        public static bool PointerEventsAt(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                UiNode n = p.GetComponent<UiNode>();
                if (n && n._pe != PointerEvents.Inherit) return n._pe == PointerEvents.Auto;
            }
            return true;
        }

        /// <summary>Re-applies pointer-events to every graphic below (call after adding raycast targets under a None node).</summary>
        public void RefreshPointerEvents()
        {
            bool parentNone = transform.parent != null && !PointerEventsAt(transform.parent);
            Suppressed.RemoveWhere(g => !g);
            ApplyPointer(transform, parentNone);
        }

        private static readonly List<Graphic> GraphicsBuffer = new List<Graphic>();

        private static void ApplyPointer(Transform t, bool parentNone)
        {
            UiNode n = t.GetComponent<UiNode>();
            bool none = n && n._pe != PointerEvents.Inherit ? n._pe == PointerEvents.None : parentNone;
            t.GetComponents(GraphicsBuffer);
            for (int i = 0; i < GraphicsBuffer.Count; i++)
            {
                Graphic g = GraphicsBuffer[i];
                if (none)
                {
                    if (g.raycastTarget) { g.raycastTarget = false; Suppressed.Add(g); }
                }
                else if (Suppressed.Remove(g))
                {
                    g.raycastTarget = true;
                }
            }
            for (int i = 0; i < t.childCount; i++) ApplyPointer(t.GetChild(i), none);
        }

        private void OnTransformChildrenChanged()
        {
            if (_pe == PointerEvents.None) RefreshPointerEvents();
            if (!_filter.IsNone) RefreshFilter();
        }

        // ---- z-index -----------------------------------------------------------------------------------

        /// <summary>The DOM order the z sort falls back on (creation order unless set).</summary>
        public double DomOrder
        {
            get { if (double.IsNaN(_dom)) _dom = ++_domCounter; return _dom; }
            set => _dom = value;
        }

        /// <summary>CSS z-index among siblings: siblings re-sort stably by (Z, DomOrder).</summary>
        public int Z
        {
            get => _z;
            set
            {
                if (_z == value) return;
                _z = value;
                ResortSiblings(transform.parent);
            }
        }

        /// <summary>Re-sorts the children of parent by (Z, DomOrder); children without a UiNode get one, ordered where they stand.</summary>
        public static void ResortSiblings(Transform parent)
        {
            if (parent == null) return;
            int count = parent.childCount;
            var items = new List<(Transform T, int Z, double Dom)>(count);
            double prev = double.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                Transform c = parent.GetChild(i);
                UiNode n = c.GetComponent<UiNode>();
                if (!n)
                {
                    n = c.gameObject.AddComponent<UiNode>();
                    // Where it stands in the hierarchy is its DOM place: just after the child before it, or (at the head)
                    // just before the first later child that has an order already.
                    if (!double.IsNegativeInfinity(prev)) n._dom = prev + 1e-6;
                    else
                    {
                        double next = double.NaN;
                        for (int j = i + 1; j < count && double.IsNaN(next); j++)
                        {
                            UiNode later = parent.GetChild(j).GetComponent<UiNode>();
                            if (later) next = later.DomOrder;
                        }
                        n._dom = double.IsNaN(next) ? ++_domCounter : next - 1.0;
                    }
                }
                double d = n.DomOrder;
                if (d > prev) prev = d;
                items.Add((c, n._z, d));
            }
            items.Sort((a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.Dom.CompareTo(b.Dom));
            for (int i = 0; i < items.Count; i++)
                if (items[i].T.GetSiblingIndex() != i) items[i].T.SetSiblingIndex(i);
        }

        // ---- ITweenable --------------------------------------------------------------------------------

        /// <summary>Keys: x, y (transform translate px), s (scale), sx, sy, r (rotate), o / opacity, left, top, width, height, tx, ty, scale (the CSS properties).</summary>
        public float GetTween(string key)
        {
            switch (key)
            {
                case "x": return _tx;
                case "y": return _ty;
                case "s": return _sx;
                case "sx": return _sx;
                case "sy": return _sy;
                case "r": return _rot;
                case "o":
                case "opacity": return _opacity;
                case "left": return X;
                case "top": return Y;
                case "width": return W;
                case "height": return H;
                case "tx": return _ptx;
                case "ty": return _pty;
                case "scale": return _psx;
                default: return 0f;
            }
        }

        public void SetTween(string key, float value)
        {
            switch (key)
            {
                case "x": TranslateX = value; break;
                case "y": TranslateY = value; break;
                case "s": Scale = value; break;
                case "sx": ScaleX = value; break;
                case "sy": ScaleY = value; break;
                case "r": Rotate = value; break;
                case "o":
                case "opacity": Opacity = value; break;
                case "left": X = value; break;
                case "top": Y = value; break;
                case "width": W = value; break;
                case "height": H = value; break;
                case "tx": PropTranslateX = value; break;
                case "ty": PropTranslateY = value; break;
                case "scale": PropScale = value; break;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _domCounter = 0;
            Suppressed.Clear();
            FilterTargets.Clear();
            GraphicsBuffer.Clear();
        }
    }
}
