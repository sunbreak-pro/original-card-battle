// A uGUI graphic whose mesh is a Trial.Geometry MeshBuilder (kit.md §4.3, §4.7): every kit shape (boxes, paths,
// hatches, gradients, glows) is a vertex-coloured mesh on the default UI material, so they batch together.
//
// - Geometry: Build(mb, box, feather) fills a MeshBuilder in the graphic's own box px, y down, origin at the box's
//   top-left (CSS coordinates). Subclasses override BuildMesh; a plain VectorGraphic takes a Builder delegate. Shapes
//   may reach outside the box (shadows, glows): only the raycast uses the box.
// - Feather (anti-aliasing): one screen pixel in box units = 1 / (canvas scale factor × the accumulated scale from the
//   root canvas to the graphic), read when the mesh is built. The geometry is built again when the box size, the
//   stage scale (the stage was resized: the root canvas changed its scale factor) or the shape changes
//   (SetGeometryDirty); an animated node scale does not rebuild, not even when a recolour repopulates the mesh
//   (kit.md §4.3).
// - Cheap changes: the geometry is cached. Graphic.color (the tint, a straight sRGB multiplier, as CSS colours are),
//   the inherited CSS filter and the gamma encoding only recolour the cached vertices; Opacity only sets the
//   CanvasRenderer's alpha (no mesh work at all).
// - Colour pipeline: mesh colour × tint × BuildTint → CSS filter of the ancestors (UiNode.Filter) → Materials.Vertex
//   (the gamma target's encoding), or Materials.Raw for the screen-blend material.
// - raycastTarget is off by default (TRIAL-PORT.md convention 8), also for a graphic made under an inactive parent.
using System;
using System.Collections.Generic;
using Trial.Geometry;
using UnityEngine;
using UnityEngine.UI;

namespace Trial.Kit
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class VectorGraphic : MaskableGraphic, IFilterTarget
    {
        /// <summary>The most vertices one graphic sends to uGUI (its 16-bit index limit).</summary>
        public const int MaxVertices = 65000;

        private static readonly HashSet<VectorGraphic> Live = new HashSet<VectorGraphic>();
        private static readonly Dictionary<Canvas, float> RootScales = new Dictionary<Canvas, float>();
        private static readonly List<Canvas> ChangedRoots = new List<Canvas>();
        private static bool _hooked;

        private MeshBuilder _mesh;
        private bool _geometryValid;
        private float _meshFeather = -1f;
        private float _meshRootScale = -1f;
        private float _meshW = -1f, _meshH = -1f;
        private bool _awake, _raycastChosen;
        private ColorFilter _filter = ColorFilter.None;
        private Rgba _buildTint = Rgba.White;
        private float _opacity = 1f;
        private bool _screenBlend;
        private IReadOnlyList<Vec2> _clip;
        private Action<MeshBuilder, RectF, float> _builder;

        /// <summary>A builder for a plain VectorGraphic: (mesh, box in px with the origin at its top-left, feather px).</summary>
        public Action<MeshBuilder, RectF, float> Builder
        {
            get => _builder;
            set { _builder = value; SetGeometryDirty(); }
        }

        /// <summary>
        /// A fixed scale from the root canvas to this graphic to compute the feather with (0 = measure it when the
        /// mesh is built). Set it for a graphic built while an entrance animation scales it.
        /// </summary>
        public float NodeScaleHint;

        /// <summary>A fixed feather in box px (0 = one screen pixel, measured).</summary>
        public float FeatherOverride;

        /// <summary>The feather the cached geometry was built with (−1 before the first build).</summary>
        public float BuiltFeather => _meshFeather;

        /// <summary>The cached geometry (box px, y down), or null before the first build. Do not change it.</summary>
        public MeshBuilder Geometry => _mesh;

        // ---- creation ------------------------------------------------------------------------------------

        /// <summary>A new graphic of type T under parent, placed at (x, y) in the parent's stage px with the size w × h.</summary>
        public static T Make<T>(RectTransform parent, float x, float y, float w, float h, string name = null) where T : VectorGraphic
        {
            RectTransform rt = Ui.Div(parent, string.IsNullOrEmpty(name) ? typeof(T).Name : name);
            Ui.Place(rt, x, y, w, h);
            return rt.gameObject.AddComponent<T>();
        }

        /// <summary>A plain VectorGraphic drawing what `builder` makes (box px, y down; feather px).</summary>
        public static VectorGraphic Draw(RectTransform parent, float x, float y, float w, float h, Action<MeshBuilder, RectF, float> builder, string name = null)
        {
            VectorGraphic g = Make<VectorGraphic>(parent, x, y, w, h, name ?? "vector");
            g.Builder = builder;
            return g;
        }

        // raycastTarget is off unless someone turns it on. Awake waits for the first activation, so a graphic made under
        // an inactive parent (and turned on as a pointer target by ClickArea meanwhile) must keep that choice: Awake only
        // clears the uGUI default, and the getter reports false until then.
        public override bool raycastTarget
        {
            get => (_awake || _raycastChosen) && base.raycastTarget;
            set
            {
                _raycastChosen = true;
                base.raycastTarget = value;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            _awake = true;
            if (!_raycastChosen) base.raycastTarget = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Live.Add(this);
            Hook();
            _filter = UiNode.FilterAt(transform);
            Materials.GammaEncodingChanged += OnEncodingChanged;
            ApplyOpacity();
        }

        protected override void OnDisable()
        {
            Live.Remove(this);
            Materials.GammaEncodingChanged -= OnEncodingChanged;
            base.OnDisable();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            _filter = UiNode.FilterAt(transform);
            SetVerticesDirty();
        }

        private void OnEncodingChanged() => SetVerticesDirty();

        // ---- what changes how ------------------------------------------------------------------------------

        /// <summary>The shape changed: the mesh is built again before the next draw.</summary>
        public void SetGeometryDirty()
        {
            _geometryValid = false;
            SetVerticesDirty();
        }

        /// <summary>Builds the geometry now (it is otherwise built lazily before the canvas draws).</summary>
        public void RebuildNow()
        {
            _geometryValid = false;
            EnsureGeometry(rectTransform.rect);
            SetVerticesDirty();
        }

        /// <summary>Graphic.color as a CSS colour: a straight sRGB multiplier of every vertex (recolours, no rebuild).</summary>
        public Rgba Tint
        {
            get { Color c = color; return new Rgba(c.r, c.g, c.b, c.a); }
            set => color = new Color(value.R, value.G, value.B, value.A);
        }

        /// <summary>
        /// A multiplier the subclass applies on top of the tint without a rebuild: a one-colour shape is built in white
        /// and coloured here, so changing its colour costs a recolour only.
        /// </summary>
        protected Rgba BuildTint
        {
            get => _buildTint;
            set
            {
                if (_buildTint.Equals(value)) return;
                _buildTint = value;
                SetVerticesDirty();
            }
        }

        /// <summary>Opacity of this graphic alone (CanvasRenderer alpha: no mesh work).</summary>
        public float Opacity
        {
            get => _opacity;
            set
            {
                _opacity = Mathf.Clamp01(value);
                ApplyOpacity();
            }
        }

        private void ApplyOpacity()
        {
            if (canvasRenderer != null) canvasRenderer.SetAlpha(_opacity);
        }

        /// <summary>CSS mix-blend-mode: screen (Materials.ScreenBlend; its vertex colours are not encoded). Ignores RectMask2D.</summary>
        public bool ScreenBlend
        {
            get => _screenBlend;
            set
            {
                if (_screenBlend == value) return;
                _screenBlend = value;
                material = value ? Materials.ScreenBlend : null;
                SetVerticesDirty();
            }
        }

        /// <summary>A convex polygon (box px, y down) the built mesh is clipped to; null = no clip.</summary>
        public IReadOnlyList<Vec2> ClipConvex
        {
            get => _clip;
            set { _clip = value; SetGeometryDirty(); }
        }

        /// <summary>The CSS filter in force here (the ancestors' UiNode filters).</summary>
        public ColorFilter InheritedFilter => _filter;

        public void SetInheritedFilter(ColorFilter filter)
        {
            if (_filter.Equals(filter)) return;
            _filter = filter;
            SetVerticesDirty();
        }

        // ---- feather -----------------------------------------------------------------------------------------

        /// <summary>The root canvas's scale factor (the stage scale), registered for resize watching; 1 without a canvas.</summary>
        private float RootScaleFactor()
        {
            Canvas c = canvas;
            if (!c) return 1f;
            Canvas root = c.rootCanvas;
            if (!root) root = c;
            float sf = root.scaleFactor;
            if (!(sf > 0f)) sf = 1f;
            if (!RootScales.ContainsKey(root)) RootScales[root] = sf;
            return sf;
        }

        /// <summary>One screen pixel in this graphic's box units (kit.md §4.3).</summary>
        public float ComputeFeather()
        {
            if (FeatherOverride > 0f) return FeatherOverride;
            float sf = RootScaleFactor();
            Canvas c = canvas;
            Canvas root = c ? (c.rootCanvas ? c.rootCanvas : c) : null;
            if (!root) return 1f / sf;
            float ns = NodeScaleHint;
            if (!(ns > 0f))
            {
                Vector3 g = transform.lossyScale, r = root.transform.lossyScale;
                double gg = Math.Abs((double)g.x * g.y), rr = Math.Abs((double)r.x * r.y);
                ns = rr > 1e-20 ? (float)Math.Sqrt(gg / rr) : 1f;
                if (!(ns > 1e-4f) || float.IsInfinity(ns)) ns = 1f;
            }
            return 1f / (sf * ns);
        }

        private static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            Canvas.willRenderCanvases += WatchScales;
        }

        // A root canvas whose scale factor changed (the stage was resized): its graphics take the new feather.
        private static void WatchScales()
        {
            if (RootScales.Count == 0) return;
            ChangedRoots.Clear();
            var keys = new List<Canvas>(RootScales.Keys);
            foreach (Canvas root in keys)
            {
                if (!root) { RootScales.Remove(root); continue; }
                float sf = root.scaleFactor;
                if (Math.Abs(sf - RootScales[root]) > 1e-5f)
                {
                    RootScales[root] = sf;
                    ChangedRoots.Add(root);
                }
            }
            if (ChangedRoots.Count == 0) return;
            foreach (VectorGraphic g in Live)
            {
                if (!g) continue;
                Canvas c = g.canvas;
                if (c && ChangedRoots.Contains(c.rootCanvas)) g.SetVerticesDirty();
            }
        }

        // ---- the mesh ------------------------------------------------------------------------------------------

        /// <summary>Fills mb with the shape in box px (origin at the box's top-left, y down). feather = one screen px.</summary>
        protected virtual void BuildMesh(MeshBuilder mb, RectF box, float feather)
        {
            _builder?.Invoke(mb, box, feather);
        }

        // The cached geometry stays while the shape, the box size and the stage scale stay (kit.md §4.3: an animated
        // node scale does not rebuild, so a recolour during a scale tween stays a recolour). A fixed feather
        // (FeatherOverride, NodeScaleHint) that changed rebuilds too.
        private void EnsureGeometry(Rect r)
        {
            float w = r.width, h = r.height;
            float rootScale = RootScaleFactor();
            bool same = _geometryValid && _mesh != null
                        && Math.Abs(w - _meshW) < 1e-3f && Math.Abs(h - _meshH) < 1e-3f
                        && Math.Abs(rootScale - _meshRootScale) <= 1e-5f * Math.Max(1f, rootScale);
            if (same && (FeatherOverride > 0f || NodeScaleHint > 0f))
            {
                float fixedFeather = ComputeFeather();
                same = Math.Abs(fixedFeather - _meshFeather) <= 1e-3f * Math.Max(1f, fixedFeather);
            }
            if (same) return;
            float feather = ComputeFeather();
            if (_mesh == null) _mesh = new MeshBuilder();
            else _mesh.Clear();
            try
            {
                BuildMesh(_mesh, new RectF(0f, 0f, w, h), feather);
                if (_clip != null && _mesh.VertexCount > 0) _mesh.ClipConvex(_clip);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                _mesh.Clear();
            }
            _meshW = w;
            _meshH = h;
            _meshFeather = feather;
            _meshRootScale = rootScale;
            _geometryValid = true;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            EnsureGeometry(r);
            MeshBuilder m = _mesh;
            if (m == null || m.Idx.Count == 0) return;
            int n = m.Pos.Count;
            if (n > MaxVertices)
            {
                Debug.LogWarning("[VectorGraphic] " + name + ": " + n + " vertices, more than a UI mesh holds; the shape is cut.", this);
                n = MaxVertices;
            }
            Color tc = color;
            Rgba tint = new Rgba(tc.r * _buildTint.R, tc.g * _buildTint.G, tc.b * _buildTint.B, tc.a * _buildTint.A);
            bool plainTint = tint.R == 1f && tint.G == 1f && tint.B == 1f && tint.A == 1f;
            bool filtered = !_filter.IsNone;
            // the screen-blend shader does not convert vertex colours (Materials.Raw); without it (shader missing) the
            // graphic draws with the default material and takes the usual encoding
            bool raw = _screenBlend && Materials.ScreenBlend;
            float x0 = r.xMin, y0 = r.yMax;
            var v = new UIVertex { normal = Vector3.back, tangent = new Vector4(1f, 0f, 0f, -1f), uv0 = Vector4.zero };
            Rgba lastIn = new Rgba(-1f, -1f, -1f, -1f);
            Color32 lastOut = default;
            for (int i = 0; i < n; i++)
            {
                Rgba c = m.Col[i];
                if (!c.Equals(lastIn))
                {
                    lastIn = c;
                    Rgba k = plainTint ? c : new Rgba(c.R * tint.R, c.G * tint.G, c.B * tint.B, c.A * tint.A);
                    if (filtered) k = _filter.Apply(k);
                    lastOut = raw ? Materials.Raw(k) : Materials.Vertex(k);
                }
                Vec2 p = m.Pos[i];
                v.position = new Vector3(x0 + p.X, y0 - p.Y, 0f);
                v.color = lastOut;
                vh.AddVert(v);
            }
            List<int> idx = m.Idx;
            for (int t = 0; t + 2 < idx.Count; t += 3)
            {
                int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                if (a >= n || b >= n || c >= n) continue;
                vh.AddTriangle(a, b, c);
            }
        }

        /// <summary>Box px (origin top-left, y down) → this graphic's local space.</summary>
        public Vector3 BoxToLocal(Vec2 p)
        {
            Rect r = rectTransform.rect;
            return new Vector3(r.xMin + p.X, r.yMax - p.Y, 0f);
        }

        /// <summary>This graphic's local space → box px (origin top-left, y down).</summary>
        public Vec2 LocalToBox(Vector3 local)
        {
            Rect r = rectTransform.rect;
            return new Vec2(local.x - r.xMin, r.yMax - local.y);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Live.Clear();
            RootScales.Clear();
            ChangedRoots.Clear();
            if (_hooked) Canvas.willRenderCanvases -= WatchScales;
            _hooked = false;
        }
    }
}
