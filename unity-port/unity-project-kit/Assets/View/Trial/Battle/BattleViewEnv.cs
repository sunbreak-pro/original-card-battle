// The Unity side of the battle scene's env (docs/BATTLE_SCENE.md §2; scenes/battle/BattleScene.js createBattleEnv
// :54-98): the engine-free BattleEnv (settings, clocks, fx switches, geometry registry, safe sound, layer number, node
// kind, miasma, log) plus what the JS env holds of the DOM — the scene root, its eleven layers, toStage — and the two root
// classes the modules share (ocbg-battle--grab / --grabbing) and the contextmenu's ocbgPinned mark.
//
//   root  (1920×1080, pivot (0,1), RectMask2D = overflow: hidden, background --ground)        .ocbg-battle
//     backdrop · field · fx · panels · marks · hud · shade · hand · held · float · over       .ocbg-battle-layer--*
//
// Each layer is a full-stage RectTransform with a nested Canvas (its own rebuilds, battle-flow §2.2), a CanvasGroup (the
// opening's opacity / visibility) and a UiNode (opacity, the arena's translateX on `field`, the hand's fade on `hand`).
// Layers draw bottom → top in hierarchy order (battle.css:28-38). Every graphic is pointer-transparent by default (the
// layers' `pointer-events: none`); a module turns raycastTarget on for its interactive pieces only (`.ocbg-battle-hit`).
using System;
using System.Collections.Generic;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace Trial.Battle
{
    /// <summary>
    /// env.layer: the scene's eleven layers by name (battle.css:28-38). A layer is a RectTransform under the root, 1920×1080,
    /// placed at (0, 0) in stage px; its UiNode carries the CSS opacity / transform the scene and the modules write.
    /// </summary>
    // js: scenes/battle/BattleScene.js:58 (layer[name])
    public sealed class BattleViewLayers
    {
        private readonly RectTransform[] _byLayer = new RectTransform[BattleLayers.Order.Count];

        internal BattleViewLayers() { }

        internal void Set(BattleLayer layer, RectTransform rt) => _byLayer[(int)layer] = rt;

        /// <summary>layer[name] by enum.</summary>
        public RectTransform this[BattleLayer layer] => _byLayer[(int)layer];

        /// <summary>The layer's UiNode (opacity, translateX, filter).</summary>
        public UiNode Node(BattleLayer layer) => UiNode.Of(this[layer]);

        /// <summary>The layer's CanvasGroup (the opening hides the layers with alpha 0 and no raycasts).</summary>
        public CanvasGroup Group(BattleLayer layer)
        {
            RectTransform rt = this[layer];
            if (!rt) return null;
            CanvasGroup g = rt.GetComponent<CanvasGroup>();
            return g ? g : rt.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>Every layer, bottom → top (LAYERS).</summary>
        public IReadOnlyList<RectTransform> All => _byLayer;

        // The JS names (layer.backdrop, layer.field, …). Never write a bare `Field.` (the View rules boundary): reach
        // the field layer as env.Layer.Field.
        /// <summary>layer.backdrop (z 0).</summary>
        public RectTransform Backdrop => this[BattleLayer.Backdrop];
        /// <summary>layer.field (z 10): floor, figures; the arena's slide and shake move this layer.</summary>
        public RectTransform Field => this[BattleLayer.Field];
        /// <summary>layer.fx (z 15).</summary>
        public RectTransform Fx => this[BattleLayer.Fx];
        /// <summary>layer.panels (z 20).</summary>
        public RectTransform Panels => this[BattleLayer.Panels];
        /// <summary>layer.marks (z 30).</summary>
        public RectTransform Marks => this[BattleLayer.Marks];
        /// <summary>layer.hud (z 40).</summary>
        public RectTransform Hud => this[BattleLayer.Hud];
        /// <summary>layer.shade (z 50).</summary>
        public RectTransform Shade => this[BattleLayer.Shade];
        /// <summary>layer.hand (z 60).</summary>
        public RectTransform Hand => this[BattleLayer.Hand];
        /// <summary>layer.held (z 70).</summary>
        public RectTransform Held => this[BattleLayer.Held];
        /// <summary>layer.float (z 80).</summary>
        public RectTransform Float => this[BattleLayer.Float];
        /// <summary>layer.over (z 90).</summary>
        public RectTransform Over => this[BattleLayer.Over];
    }

    /// <summary>
    /// The scene's env as the Unity modules see it (docs/BATTLE_SCENE.md §2): BattleEnv + root, layers, toStage, the root
    /// classes. Built by <see cref="BattleScene.CreateBattleEnv"/>.
    /// </summary>
    // js: scenes/battle/BattleScene.js:54 (createBattleEnv)
    public sealed class BattleViewEnv : BattleEnv
    {
        /// <summary>The layers that take pointer events (a GraphicRaycaster each): panels, marks, hud, hand, held, over.</summary>
        private static readonly BattleLayer[] RaycastLayers =
        {
            BattleLayer.Panels, BattleLayer.Marks, BattleLayer.Hud, BattleLayer.Hand, BattleLayer.Held, BattleLayer.Over,
        };

        private bool _contextPinned;

        internal BattleViewEnv(IBattleSettings settings, IBattleAudio audio, int layer, BattleNodeKind nodeKind, float? miasmaPercent,
            IBattleLog log, TrialStage stage)
            : base(settings, audio, layer, nodeKind, miasmaPercent, log)
        {
            Stage = stage;
            Layer = new BattleViewLayers();
        }

        /// <summary>env.root: the scene root (.ocbg-battle), 1920×1080 at (0, 0) of the host.</summary>
        public RectTransform Root { get; private set; }

        /// <summary>The root's --ground fill (the root itself as a pointer target: a press there has no layer).</summary>
        public Graphic Ground { get; private set; }

        /// <summary>env.layer.</summary>
        public BattleViewLayers Layer { get; }

        /// <summary>env.stage: the app's stage (ctx.stage), or null (a dev mount: the root's own canvas answers toStage).</summary>
        public TrialStage Stage { get; }

        /// <summary>
        /// The root has class ocbg-battle--grab (a card is hovered and the hand is interactable). The hand writes it; it only
        /// stands for the cursor in the JS.
        /// </summary>
        // js: scenes/battle/hand.js:797
        public bool Grab { get; set; }

        /// <summary>The root has class ocbg-battle--grabbing (a card is held). The hand writes it; the hud's tip reads it (util.js:125).</summary>
        // js: scenes/battle/hand.js:798
        public bool Grabbing { get; set; }

        /// <summary>
        /// e.ocbgPinned of this frame's contextmenu: a status chip's or a stance frame's own right-click handler (EventSystem,
        /// before the scene's pump) calls <see cref="MarkContextPinned"/>; the hud's OnPinContext reads it (hud.js:460). The
        /// driver clears it after it routed the frame's contextmenu.
        /// </summary>
        public bool ContextPinned => _contextPinned;

        /// <summary>e.ocbgPinned = true (status.js:237, stances.js:195).</summary>
        public void MarkContextPinned() => _contextPinned = true;

        internal void ClearContextPinned() => _contextPinned = false;

        /// <summary>The camera pointer events and toStage use (the stage's, else the root canvas's; null for an overlay canvas).</summary>
        public Camera EventCamera
        {
            get
            {
                if (Stage) return Stage.EventCamera;
                Canvas c = Root ? Root.GetComponentInParent<Canvas>() : null;
                if (c) c = c.rootCanvas;
                return c && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
            }
        }

        /// <summary>env.toStage(e): a screen position (Input System, origin bottom left) → stage px (origin top left, y down).</summary>
        // js: scenes/battle/BattleScene.js:85
        public StagePoint ToStage(Vector2 screenPos)
        {
            if (!Root) return new StagePoint(screenPos.x, screenPos.y);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screenPos, EventCamera, out Vector2 local);
            // From the root's top left, y down (whatever its pivot).
            Rect r = Root.rect;
            return new StagePoint(local.x - r.xMin, r.yMax - local.y);
        }

        /// <summary>Stage px → a screen position (origin bottom left).</summary>
        public Vector2 ToScreen(StagePoint p)
        {
            if (!Root) return new Vector2(p.X, p.Y);
            Rect r = Root.rect;
            Vector3 world = Root.TransformPoint(new Vector3(r.xMin + p.X, r.yMax - p.Y, 0f));
            return RectTransformUtility.WorldToScreenPoint(EventCamera, world);
        }

        /// <summary>The stage box an element covers now (getBoundingClientRect in stage px: its rotation and scale included).</summary>
        public StageBox StageBoxOf(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Rect root = Root.rect;
            float l = float.MaxValue, t = float.MaxValue, r = float.MinValue, b = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = Root.InverseTransformPoint(corners[i]);
                float x = p.x - root.xMin, y = root.yMax - p.y;
                if (x < l) l = x;
                if (x > r) r = x;
                if (y < t) t = y;
                if (y > b) b = y;
            }
            return new StageBox(l, t, r - l, b - t);
        }

        /// <summary>The layer a transform lies in (layer.x.contains(t)), or null (the root itself, or outside the scene).</summary>
        public BattleLayer? LayerOf(Transform t)
        {
            if (!t) return null;
            for (int i = 0; i < BattleLayers.Order.Count; i++)
            {
                RectTransform rt = Layer[BattleLayers.Order[i]];
                if (rt && t.IsChildOf(rt)) return BattleLayers.Order[i];
            }
            return null;
        }

        /// <summary>Whether t lies in the scene's root (env.root.contains(t)).</summary>
        public bool Contains(Transform t) => t && Root && t.IsChildOf(Root);

        // ---- building ---------------------------------------------------------------------------------

        /// <summary>
        /// h('div', { class: 'ocbg-battle' }) and its layers, appended to host. The battle.css root: absolute at (0, 0),
        /// 1920×1080, overflow hidden, background --ground, colour --ink.
        /// </summary>
        // js: scenes/battle/BattleScene.js:57
        internal void BuildRoot(RectTransform host)
        {
            RectTransform root = Ui.Div(host, "ocbg-battle", 0f, 0f, L.W, L.H);
            root.gameObject.AddComponent<RectMask2D>();
            Root = root;
            // .ocbg-battle { background: var(--ground) }: the root's own box, under every layer, and a pointer target (a press
            // on the arena with nothing over it lands on the root, as e.target === root).
            BoxGraphic ground = BoxGraphic.Make(root, "background: var(--ground);", 0f, 0f, L.W, L.H, "ground");
            ground.raycastTarget = true;
            Ground = ground;

            Canvas parentCanvas = host ? host.GetComponentInParent<Canvas>() : null;
            for (int i = 0; i < BattleLayers.Order.Count; i++)
            {
                BattleLayer which = BattleLayers.Order[i];
                RectTransform rt = Ui.Div(root, "ocbg-battle-layer--" + NameOf(which), 0f, 0f, L.W, L.H);
                var c = rt.gameObject.AddComponent<Canvas>();
                c.overrideSorting = false;
                if (parentCanvas)
                {
                    c.vertexColorAlwaysGammaSpace = parentCanvas.vertexColorAlwaysGammaSpace;
                    c.additionalShaderChannels = parentCanvas.additionalShaderChannels;
                }
                if (Array.IndexOf(RaycastLayers, which) >= 0) rt.gameObject.AddComponent<GraphicRaycaster>();
                rt.gameObject.AddComponent<CanvasGroup>();
                UiNode.Of(rt);
                Layer.Set(which, rt);
            }
        }

        /// <summary>LAYERS[i]: the layer's JS name (BattleScene.js:26).</summary>
        public static string NameOf(BattleLayer layer)
        {
            switch (layer)
            {
                case BattleLayer.Backdrop: return "backdrop";
                case BattleLayer.Field: return "field";
                case BattleLayer.Fx: return "fx";
                case BattleLayer.Panels: return "panels";
                case BattleLayer.Marks: return "marks";
                case BattleLayer.Hud: return "hud";
                case BattleLayer.Shade: return "shade";
                case BattleLayer.Hand: return "hand";
                case BattleLayer.Held: return "held";
                case BattleLayer.Float: return "float";
                case BattleLayer.Over: return "over";
                default: return "layer";
            }
        }
    }
}
