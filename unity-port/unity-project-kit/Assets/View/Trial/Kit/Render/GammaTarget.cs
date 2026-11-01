// The gamma render target (kit.md §4.12): the Trial canvas renders into an 8-bit UNorm texture so every blend
// happens on sRGB values, as in the browser; the frame is then shown on the screen unchanged.
//
//   TrialCamera (depth -10) ── renders the Screen Space - Camera Trial canvas ──▶ Target (R8G8B8A8_UNorm, screen size)
//   endCameraRendering: Graphics.CopyTexture(Target → View)   (View is R8G8B8A8_SRGB: same bytes, read with sRGB decode)
//   PresentCamera (depth -9, clears) + an overlay canvas with one RawImage(View) ──▶ the sRGB back buffer (re-encodes)
//
// The copy is a raw byte copy, the sampler decodes sRGB exactly and the back buffer encodes it exactly, so the
// present is lossless. Raycasts and ToStage keep working because the target has the screen's size (it is recreated
// when the screen changes) and the canvas's event camera is the TrialCamera.
//
// Spike (2026-10-22, Unity 6000.5.5f1, URP 17, D3D12, Game view 2560×1440, real frame loop) — probe value: browser
// (sRGB blend, by formula) / Linear default / this target:
//   rgba(5,6,10,.62) over #808080        52,52,55  / 81,81,82   / 52,53,55
//   rgba(157,166,186,.26) over #141925   56,62,76  / 86,91,105  / 56,62,75
//   rgba(0,0,0,.45) over #808080         70        / 97         / 70
//   rgba(0,0,0,.225) over #141925        16,19,29  / 18,18,33   / 16,19,29
//   rgba(255,216,154,.5) over #10141d    136,118,92 / 189,159,114 / 135,118,91  (TMP glyph: 135,118,91)
//   #e8a84c opaque                        232,168,76 / 232,168,75 / 232,167,76  (TMP glyph: 232,167,76)
//   screen: crimson .7 over #808080       196,153,150 / —         / 195,153,150
// Vertex colours must be encoded (Materials.Vertex): the stock UI/TMP shaders convert them with UIGammaToLinear.
using System;
using Trial.Geometry;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Trial.Kit
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class GammaTarget : MonoBehaviour
    {
        public Camera TrialCamera { get; private set; }
        public Camera PresentCamera { get; private set; }
        public Canvas PresentCanvas { get; private set; }
        public RawImage PresentImage { get; private set; }

        /// <summary>What the Trial canvas renders into (UNorm: the bytes are sRGB values).</summary>
        public RenderTexture Target { get; private set; }

        /// <summary>The same bytes as an sRGB texture, which the present samples.</summary>
        public RenderTexture View { get; private set; }

        /// <summary>Raised after the targets were recreated for a new screen size.</summary>
        public event Action TargetsResized;

        private Canvas _canvas;
        private Rgba _clear;
        private int _w, _h;
        private bool _hooked;
        private bool _ownsEncoding;
        private int _copies;

        /// <summary>Frames copied so far (a check that the present runs).</summary>
        public int CopyCount => _copies;

        /// <summary>
        /// Builds the cameras, the targets and the present under `host`, and points `canvas` (the Trial canvas) at the
        /// target: render mode Screen Space - Camera, vertexColorAlwaysGammaSpace on.
        /// </summary>
        public static GammaTarget Attach(GameObject host, Canvas canvas, Rgba clear)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            GammaTarget g = host.GetComponent<GammaTarget>();
            if (!g) g = host.AddComponent<GammaTarget>();
            g.Build(canvas, clear);
            return g;
        }

        private void Build(Canvas canvas, Rgba clear)
        {
            _canvas = canvas;
            _clear = clear;

            if (!TrialCamera)
            {
                var camGo = new GameObject("TrialCamera");
                camGo.transform.SetParent(transform, false);
                TrialCamera = camGo.AddComponent<Camera>();
            }
            Camera cam = TrialCamera;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Materials.ClearColor(clear);
            cam.orthographic = true;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.allowDynamicResolution = false;
            cam.useOcclusionCulling = false;
            cam.depth = -10;
            // Every layer: a nested canvas on another layer would otherwise not be drawn (measured); the Trial scene
            // has no world objects of its own.
            cam.cullingMask = ~0;

            if (!PresentCamera)
            {
                var pcGo = new GameObject("TrialPresentCamera");
                pcGo.transform.SetParent(transform, false);
                PresentCamera = pcGo.AddComponent<Camera>();
            }
            Camera pc = PresentCamera;
            pc.clearFlags = CameraClearFlags.SolidColor;
            pc.backgroundColor = Materials.ToUnity(clear);
            pc.cullingMask = 0;
            pc.orthographic = true;
            pc.allowHDR = false;
            pc.allowMSAA = false;
            pc.useOcclusionCulling = false;
            pc.depth = -9;

            if (!PresentCanvas)
            {
                var ovGo = new GameObject("TrialPresent", typeof(RectTransform));
                ovGo.transform.SetParent(transform, false);
                PresentCanvas = ovGo.AddComponent<Canvas>();
                var rawGo = new GameObject("Frame", typeof(RectTransform));
                rawGo.transform.SetParent(ovGo.transform, false);
                var rr = (RectTransform)rawGo.transform;
                rr.anchorMin = Vector2.zero;
                rr.anchorMax = Vector2.one;
                rr.offsetMin = Vector2.zero;
                rr.offsetMax = Vector2.zero;
                PresentImage = rawGo.AddComponent<RawImage>();
                PresentImage.raycastTarget = false;
            }
            PresentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            PresentCanvas.sortingOrder = -32000;
            PresentCanvas.vertexColorAlwaysGammaSpace = false;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 100f;
            canvas.vertexColorAlwaysGammaSpace = true;

            EnsureTargets(force: true);
            Hook(true);
            Materials.SetGammaEncoding(true);
            _ownsEncoding = true;
        }

        /// <summary>Removes the target: the canvas goes back to Screen Space - Overlay and colours stop being encoded.</summary>
        public void Detach()
        {
            Hook(false);
            if (_canvas)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.worldCamera = null;
                _canvas.vertexColorAlwaysGammaSpace = false;
            }
            if (TrialCamera) Destroy(TrialCamera.gameObject);
            if (PresentCanvas) Destroy(PresentCanvas.gameObject);
            // The present camera stays out of it: TrialStage keeps a clearing camera of its own in overlay mode.
            if (PresentCamera) Destroy(PresentCamera.gameObject);
            ReleaseTargets();
            Materials.SetGammaEncoding(false);
            _ownsEncoding = false;
            Destroy(this);
        }

        private void LateUpdate() => EnsureTargets(force: false);

        /// <summary>Recreates the targets when the screen size changed (or when forced).</summary>
        public void EnsureTargets(bool force)
        {
            int w = Math.Max(1, Screen.width), h = Math.Max(1, Screen.height);
            if (!force && Target && View && w == _w && h == _h && Target.IsCreated() && View.IsCreated()) return;
            ReleaseTargets();
            _w = w;
            _h = h;
            Target = new RenderTexture(new RenderTextureDescriptor(w, h, GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormat.None))
            {
                name = "TrialGammaTarget",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point,
            };
            Target.Create();
            View = new RenderTexture(new RenderTextureDescriptor(w, h, GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.None))
            {
                name = "TrialGammaView",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point,
            };
            View.Create();
            if (TrialCamera) TrialCamera.targetTexture = Target;
            if (PresentImage) PresentImage.texture = View;
            try { TargetsResized?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void ReleaseTargets()
        {
            if (TrialCamera) TrialCamera.targetTexture = null;
            if (PresentImage) PresentImage.texture = null;
            if (Target) { Target.Release(); Destroy(Target); }
            if (View) { View.Release(); Destroy(View); }
            Target = null;
            View = null;
        }

        private void Hook(bool on)
        {
            if (on == _hooked) return;
            _hooked = on;
            if (on)
            {
                RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
                Camera.onPostRender += OnPostRenderBuiltin;
            }
            else
            {
                RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
                Camera.onPostRender -= OnPostRenderBuiltin;
            }
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam) => CopyAfter(cam);

        // The built-in pipeline (no SRP asset): the same copy after the camera rendered.
        private void OnPostRenderBuiltin(Camera cam)
        {
            if (GraphicsSettings.currentRenderPipeline != null) return;
            CopyAfter(cam);
        }

        private void CopyAfter(Camera cam)
        {
            if (cam == null || cam != TrialCamera) return;
            if (!Target || !View) return;
            Graphics.CopyTexture(Target, View);
            _copies++;
        }

        private void OnEnable()
        {
            if (TrialCamera) Hook(true);
        }

        private void OnDisable() => Hook(false);

        private void OnDestroy()
        {
            Hook(false);
            ReleaseTargets();
            if (_ownsEncoding) Materials.SetGammaEncoding(false);
            _ownsEncoding = false;
        }
    }
}
