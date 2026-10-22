// The stage: one 1920×1080 canvas scaled uniformly to the screen and letterboxed with --ground (port of the canvas
// side of stage.js createStage, kit.md §4.10). Scenes mount into Layer(...) roots; every element is placed in stage px
// with the origin at the top-left and y down (Ui.Place).
//
//   TrialStage (this, on its own GameObject)
//     Canvas  (Screen Space - Camera into the gamma target, or Screen Space - Overlay; CanvasScaler 1920×1080 Expand)
//       Letterbox  (full canvas, --ground)
//       Root       (1920×1080, pivot (0,1), centred, RectMask2D = .ocbg-stage overflow: hidden)
//         <layers> (full stage, nested Canvas each, GraphicRaycaster)
//
// The stage also pumps the key stack once per frame (KeyStack.Pump), turns focus loss into KeyStack.Blur, and owns
// a ClockDriver for the scenes' clocks (Clocks).
using System;
using System.Collections.Generic;
using Trial.Geometry;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Trial.Kit
{
    [DefaultExecutionOrder(-190)]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class TrialStage : MonoBehaviour
    {
        // js: scenes/common/stage.js:4-5
        public const float W = 1920f;
        public const float H = 1080f;

        /// <summary>The UI layer the stage's canvases sit on.</summary>
        public const int UiLayer = 5;

        /// <summary>
        /// Whether a new stage renders through the gamma target (kit.md §4.12). On by default: the colour-space spike
        /// put every probe within 1/255 of the browser with it, and 29–54/255 lighter without it (GammaTarget.cs).
        /// </summary>
        public static bool DefaultUseGammaTarget = true;

        private static TrialStage _current;

        /// <summary>The live stage (the last one created), or null.</summary>
        public static TrialStage Current => _current ? _current : null;

        public Canvas Canvas { get; private set; }
        public CanvasScaler Scaler { get; private set; }
        public RectTransform CanvasRect { get; private set; }

        /// <summary>The full-canvas --ground fill behind the stage (the letterbox).</summary>
        public Image Letterbox { get; private set; }

        /// <summary>The 1920×1080 stage: pivot (0,1), so local (x, −y) is stage (x, y).</summary>
        public RectTransform Root { get; private set; }

        /// <summary>The scenes' clocks: stepped every frame with the unscaled frame time.</summary>
        public ClockDriver Clocks { get; private set; }

        /// <summary>The gamma target while UseGammaTarget is on.</summary>
        public GammaTarget Gamma => _gamma ? _gamma : null;

        /// <summary>The camera pointer events and ToStage use (the gamma target's camera; null for an overlay canvas).</summary>
        public Camera EventCamera => _gamma ? _gamma.TrialCamera : null;

        /// <summary>True: the stage pumps KeyStack in its Update (the boot may pump too; a frame is pumped once).</summary>
        public bool PumpKeys = true;

        /// <summary>Raised after the screen size or the stage scale changed (stage.js fit()).</summary>
        public event Action Resized;

        private GammaTarget _gamma;
        private Camera _clearCamera;
        private bool _useGamma;
        private readonly List<(RectTransform Rt, int Order)> _layers = new List<(RectTransform, int)>();
        private int _lastW = -1, _lastH = -1;
        private float _lastScale = -1f;

        // ---- creation ------------------------------------------------------------------------------------

        // js: scenes/common/stage.js:47
        /// <summary>createStage(container): builds a stage (canvas, scaler, letterbox, root, EventSystem with InputSystemUIInputModule, clocks).</summary>
        public static TrialStage Create(string name = "TrialStage", bool? useGammaTarget = null)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "TrialStage" : name);
            var st = go.AddComponent<TrialStage>();
            st.Build(useGammaTarget ?? DefaultUseGammaTarget);
            return st;
        }

        private void Build(bool useGamma)
        {
            _current = this;
            EnsureEventSystem();

            var cvGo = new GameObject("TrialCanvas", typeof(RectTransform));
            cvGo.layer = UiLayer;
            cvGo.transform.SetParent(transform, false);
            CanvasRect = (RectTransform)cvGo.transform;
            Canvas = cvGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.pixelPerfect = false;
            Canvas.sortingOrder = 0;
            Canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            Scaler = cvGo.AddComponent<CanvasScaler>();
            Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            Scaler.referenceResolution = new Vector2(W, H);
            Scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // scale = min(w/1920, h/1080), stage.js:56
            Scaler.referencePixelsPerUnit = 100f;
            cvGo.AddComponent<GraphicRaycaster>();

            // The letterbox: --ground over the whole canvas (.ocbg-viewport background).
            var lbGo = new GameObject("Letterbox", typeof(RectTransform));
            lbGo.layer = UiLayer;
            lbGo.transform.SetParent(CanvasRect, false);
            var lb = (RectTransform)lbGo.transform;
            lb.anchorMin = Vector2.zero;
            lb.anchorMax = Vector2.one;
            lb.offsetMin = Vector2.zero;
            lb.offsetMax = Vector2.zero;
            Letterbox = lbGo.AddComponent<Image>();
            Letterbox.raycastTarget = false;

            // The stage root: 1920×1080 centred, pivot top-left (.ocbg-stage, overflow hidden).
            var rootGo = new GameObject("Stage", typeof(RectTransform));
            rootGo.layer = UiLayer;
            rootGo.transform.SetParent(CanvasRect, false);
            Root = (RectTransform)rootGo.transform;
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0.5f);
            Root.pivot = new Vector2(0f, 1f);
            Root.sizeDelta = new Vector2(W, H);
            Root.anchoredPosition = new Vector2(-W / 2f, H / 2f);
            rootGo.AddComponent<RectMask2D>();

            Clocks = gameObject.AddComponent<ClockDriver>();

            SetColorMode(useGamma, force: true);
        }

        /// <summary>
        /// Renders through the gamma target (true) or straight to the screen on an overlay canvas (false). Switch it
        /// before scenes are mounted: colours set earlier keep the encoding they were set with (Materials.GammaEncodingChanged).
        /// </summary>
        public bool UseGammaTarget
        {
            get => _useGamma;
            set => SetColorMode(value, force: false);
        }

        private void SetColorMode(bool useGamma, bool force)
        {
            if (!force && useGamma == _useGamma) return;
            _useGamma = useGamma;
            if (useGamma)
            {
                if (_clearCamera) { Destroy(_clearCamera.gameObject); _clearCamera = null; }
                _gamma = GammaTarget.Attach(gameObject, Canvas, DsTokens.Ground);
            }
            else
            {
                if (_gamma) { _gamma.Detach(); _gamma = null; }
                Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Canvas.worldCamera = null;
                Canvas.vertexColorAlwaysGammaSpace = false;
                Materials.SetGammaEncoding(false);
                if (!_clearCamera)
                {
                    // A camera that only clears to --ground, so the screen is defined even with no other camera.
                    var camGo = new GameObject("TrialClearCamera");
                    camGo.transform.SetParent(transform, false);
                    _clearCamera = camGo.AddComponent<Camera>();
                    _clearCamera.clearFlags = CameraClearFlags.SolidColor;
                    _clearCamera.cullingMask = 0;
                    _clearCamera.orthographic = true;
                    _clearCamera.allowHDR = false;
                    _clearCamera.allowMSAA = false;
                    _clearCamera.depth = -10;
                }
                _clearCamera.backgroundColor = Materials.ToUnity(DsTokens.Ground);
            }
            foreach (var (rt, _) in _layers)
            {
                if (!rt) continue;
                var c = rt.GetComponent<Canvas>();
                if (c) c.vertexColorAlwaysGammaSpace = Canvas.vertexColorAlwaysGammaSpace;
            }
            Letterbox.color = Materials.VertexColor(DsTokens.Ground);
        }

        /// <summary>Creates the EventSystem (InputSystemUIInputModule) when there is none; navigation events off everywhere.</summary>
        public static EventSystem EnsureEventSystem()
        {
            EventSystem es = EventSystem.current;
            if (!es) es = FindAnyObjectByType<EventSystem>();
            if (!es)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es = go.GetComponent<EventSystem>();
            }
            else if (!es.GetComponent<BaseInputModule>())
            {
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
            // Enter / Space / Esc / arrows must never click or move uGUI selection behind the key stack's back.
            es.sendNavigationEvents = false;
            return es;
        }

        // ---- layers ------------------------------------------------------------------------------------

        // js: scenes/common/stage.js:71
        /// <summary>
        /// A full-stage layer for a scene or an overlay (stage.js st.layer): a child of Root with its own nested
        /// Canvas (so its rebuilds do not rebatch the others) and a GraphicRaycaster. Layers draw in `order`, then in
        /// creation order; graphics are not raycast targets until a view turns that on.
        /// </summary>
        public RectTransform Layer(string name, int order = 0, bool raycasts = true)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "Layer" : name, typeof(RectTransform));
            go.layer = UiLayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(Root, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var c = go.AddComponent<Canvas>();
            c.vertexColorAlwaysGammaSpace = Canvas.vertexColorAlwaysGammaSpace;
            c.additionalShaderChannels = Canvas.additionalShaderChannels;
            if (raycasts) go.AddComponent<GraphicRaycaster>();

            _layers.RemoveAll(l => !l.Rt);
            int at = _layers.Count;
            for (int i = 0; i < _layers.Count; i++)
                if (_layers[i].Order > order) { at = i; break; }
            _layers.Insert(at, (rt, order));
            // stage.js appends a layer to the root, above everything already there; a higher order stays above it.
            // Only this layer moves, so whatever else the root holds keeps its place.
            if (at + 1 < _layers.Count) rt.SetSiblingIndex(_layers[at + 1].Rt.GetSiblingIndex());
            else rt.SetAsLastSibling();
            return rt;
        }

        // ---- coordinates -------------------------------------------------------------------------------

        // js: scenes/common/stage.js:56
        /// <summary>The canvas scale factor: min(screenW / 1920, screenH / 1080) (stage.js st.scale).</summary>
        public float Scale
        {
            get
            {
                float s = Canvas ? Canvas.scaleFactor : 0f;
                if (s > 0f) return s;
                return Math.Min(Math.Max(1, Screen.width) / W, Math.Max(1, Screen.height) / H);
            }
        }

        /// <summary>Screen px from the left edge to the stage (stage.js st.offX).</summary>
        public float OffX => (Screen.width - W * Scale) / 2f;

        /// <summary>Screen px from the top edge to the stage (stage.js st.offY).</summary>
        public float OffY => (Screen.height - H * Scale) / 2f;

        // js: scenes/common/stage.js:66
        /// <summary>Screen position (Input System / EventSystem: origin bottom-left) → stage px (origin top-left, y down). stage.js st.toStage.</summary>
        public Vector2 ToStage(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screenPos, EventCamera, out Vector2 local);
            return new Vector2(local.x, -local.y);
        }

        /// <summary>Stage px → screen position (origin bottom-left).</summary>
        public Vector2 ToScreen(Vector2 stagePos)
        {
            Vector3 world = Root.TransformPoint(new Vector3(stagePos.x, -stagePos.y, 0f));
            return RectTransformUtility.WorldToScreenPoint(EventCamera, world);
        }

        /// <summary>Whether a screen position falls on the stage (not on the letterbox).</summary>
        public bool OnStage(Vector2 screenPos)
        {
            Vector2 p = ToStage(screenPos);
            return p.x >= 0f && p.y >= 0f && p.x <= W && p.y <= H;
        }

        // ---- the frame ---------------------------------------------------------------------------------

        private void Update()
        {
            if (PumpKeys) KeyStack.Pump();
        }

        private void LateUpdate()
        {
            int w = Screen.width, h = Screen.height;
            float s = Scale;
            if (w == _lastW && h == _lastH && Mathf.Approximately(s, _lastScale)) return;
            bool first = _lastW < 0;
            _lastW = w;
            _lastH = h;
            _lastScale = s;
            if (_gamma) _gamma.EnsureTargets(force: false);
            if (first) return;
            try { Resized?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) KeyStack.Blur();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) KeyStack.Blur();
        }

        private void OnDestroy()
        {
            if (_current == this) _current = null;
            if (_gamma) Materials.SetGammaEncoding(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _current = null;
            DefaultUseGammaTarget = true;
        }
    }
}
