// ExploreScene — the map, the walker and the steps (ExploreScene.js "DOM" :252-269 (the map layer's elements),
// "helpers" :408-443, "the map" :445-595, "steps" :1240-1383; explore.css:67-112, :205-207; explore.md §5.4). Owned
// by EX-MAP.
//
// The map layer holds, back to front: the lamp's pool of light under the walker, the edges (one group per edge, the
// trail of the edge being walked on top of them), the nodes (ExploreNodeView), the selection frame (OCB.TargetFrame
// over the pinned node) and the walker (z-index 3: glow that breathes, the disc with the person). The map is built once
// per layer (BuildMap; assembling row by row when `enter`) and restyled by UpdateMap. A step walks the edge (Walk:
// the walker steps down off its perch, strides along the line while the trail lights up behind it, steps up onto the
// new node, the band's numbers roll as it lands) or, with 標の石, leaps (Leap); the arrival ripples on the node.
//
// Clocks: the JS timers and the JS-driven transitions (the walk, the enter animation, dur(…)) run on the scene clock
// (Delay / Timeout / Clock); the stylesheet's transitions and keyframes run on the document clock (CssStates,
// CssAnimator), untouched by 演出短縮 as in the JS. The map's engaging dim (.is-engaging: brightness .55) and its
// interlude fade are the map layer's own rules (the scene core registers them on MapLayer).
//
// The node, the walker and the selection frame take their sizes from the map's MapLayout (ExplorationScreen.Layout):
// the JS's on a map of five rows or fewer, smaller on a longer one so that its rows fit the area (#1049).
//
// A layer whose ExplorationScreen.Look is Strata (layer 1, #1050) draws its nodes as chambers and its edges as tunnels
// (StrataLayout's sizes, StrataPalette's colours), walks the walker through the tunnels, and shows the depth column on
// the map's left. Every other layer draws as above.
using System;
using System.Collections.Generic;
using System.Globalization;
using DungeonCore;
using Exploration;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Explore
{
    public sealed partial class ExploreScene
    {
        // ------------------------------------------------------------------ explore.css (verbatim)

        /// <summary>explore.css:71 .ocbg-explore-pool.</summary>
        private const string PoolCss = "position: absolute; left: 0; top: 0; width: 520px; height: 400px; border-radius: 50%; pointer-events: none; will-change: transform; background: radial-gradient(closest-side, color-mix(in srgb, var(--amber) 16%, transparent), color-mix(in srgb, var(--amber) 5%, transparent) 55%, transparent);";

        /// <summary>The pool's box (explore.css:71) and its offset from the node centre (setWalker: translate(x − 260, y − 200)).</summary>
        private const float PoolW = 520f, PoolH = 400f;

        /// <summary>explore.css:80 .ocbg-explore-trail.</summary>
        private const string TrailCss = "stroke: var(--amber); stroke-width: 5; stroke-linecap: round; fill: none; opacity: 1; filter: drop-shadow(0 0 7px color-mix(in srgb, var(--amber) 60%, transparent)); transition: opacity 480ms ease-out;";

        /// <summary>explore.css:81 .ocbg-explore-trail.is-fading.</summary>
        private const string TrailFadingCss = "opacity: 0;";

        /// <summary>explore.css:103 .ocbg-explore-sel.</summary>
        private const string SelCss = "position: absolute; left: 0; top: 0; opacity: 0; transition: transform 160ms cubic-bezier(.22,.61,.36,1), opacity 160ms; pointer-events: none;";

        /// <summary>explore.css:104 .ocbg-explore-sel.is-on.</summary>
        private const string SelOnCss = "opacity: 1;";

        /// <summary>explore.css:105 .ocbg-explore.is-busy .ocbg-explore-sel.</summary>
        private const string SelBusyCss = "opacity: 0;";

        /// <summary>
        /// ExploreScene.js:259 OCB.TargetFrame(SEL.w, SEL.h, { hook: 20, width: 3, glow: 12 }): the stroke and the glow; the
        /// hook is MapLayout.SelHook.
        /// </summary>
        private const float SelStroke = 3f, SelGlow = 12f;

        /// <summary>
        /// explore.css:107 .ocbg-explore-walker; its 52px box is MapLayout.WalkerBox (the state machine writes the box back
        /// on every change, so the rule must name the map's size).
        /// </summary>
        private static string WalkerCss(float box) =>
            "position: absolute; left: 0; top: 0; width: " + Px(box) + "; height: " + Px(box) + "; z-index: 3; pointer-events: none; will-change: transform;";

        /// <summary>explore.css:206 .ocbg-explore-walker (trial: the leap fades it).</summary>
        private const string WalkerTransitionCss = "transition: opacity 240ms ease-in-out;";

        /// <summary>explore.css:207 .ocbg-explore-walker.is-leaping.</summary>
        private const string WalkerLeapingCss = "opacity: 0;";

        /// <summary>explore.css:107 z-index: 3 (above the nodes and the frame).</summary>
        private const int WalkerZ = 3;

        /// <summary>explore.css:108 .ocbg-explore-walker-glow (inset −24px: 100 × 100).</summary>
        private const string WalkerGlowCss = "position: absolute; inset: -24px; border-radius: 50%; background: radial-gradient(closest-side, color-mix(in srgb, var(--amber) 42%, transparent), transparent); animation: ocbg-explore-breathe 2.6s ease-in-out infinite;";

        /// <summary>explore.css:108 the glow's animation (explore.css:109 @keyframes ocbg-explore-breathe).</summary>
        private const string BreatheAnim = "ocbg-explore-breathe 2.6s ease-in-out infinite";

        /// <summary>explore.css:110 .ocbg-explore-walker-disc (inset 6px: 40 × 40).</summary>
        private const string WalkerDiscCss = "position: absolute; inset: 6px; border-radius: 50%; display: flex; align-items: center; justify-content: center; background: var(--panel-solid); border: 3px solid var(--amber); box-shadow: 0 2px 6px color-mix(in srgb, var(--ground) 80%, transparent), var(--shadow-reach);";

        /// <summary>explore.css:111 .ocbg-explore-walker.is-landed .ocbg-explore-walker-disc (explore.css:112 @keyframes ocbg-explore-land).</summary>
        private const string LandAnim = "ocbg-explore-land 320ms cubic-bezier(.22,.61,.36,1)";

        // ------------------------------------------------------------------ the map layer's elements (ExploreScene.js:253-269)

        /// <summary>.ocbg-explore-pool: the lamp's pool of light under the walker.</summary>
        private RectTransform Pool;

        /// <summary>The svg of the edges (.ocbg-explore-edges, viewBox 0 0 1920 1080).</summary>
        private RectTransform EdgesLayer;

        /// <summary>.ocbg-explore-nodes.</summary>
        private RectTransform NodesLayer;

        /// <summary>.ocbg-explore-sel: OCB.TargetFrame(SEL.w, SEL.h, { hook: 20, width: 3, glow: 12 }).</summary>
        private RectTransform SelFrame;

        /// <summary>.ocbg-explore-walker (glow + disc with the person icon).</summary>
        private RectTransform Walker;

        /// <summary>.ocbg-explore-trail: the edge being walked, lit behind the walker (made on the first walk; buildMap's
        /// clearing of the edges removes it, as textContent = '' does, and the next walk makes it again).</summary>
        private PathGraphic Trail;

        private PathLayer TrailLine;
        private CssStates TrailStates;
        private UiNode PoolNode;
        private UiNode WalkerNode;
        private CssStates WalkerStates;
        private BoxGraphic WalkerDisc;
        private CssStates SelStates;

        /// <summary>The selection frame has not been drawn yet (its first place and look come without a transition).</summary>
        private bool SelFresh = true;

        /// <summary>The sizes the nodes, the walker and the selection frame are drawn at (the map's ExplorationScreen.Layout).</summary>
        private MapLayout MapSize = MapLayout.Full;

        /// <summary>How the map on screen is drawn (#1050): the plain squares and lines, or chambers and tunnels.</summary>
        private MapLook ShownLook = MapLook.Plain;

        /// <summary>The chambers' sizes and colours while ShownLook is Strata (null otherwise).</summary>
        private StrataLayout StrataSize;

        private StrataPalette StrataColours;

        /// <summary>The walker's and the selection frame's sizes for the map on screen.</summary>
        private MarkSize Marks = MarkSize.Of(MapLayout.Full);

        /// <summary>The selection frame's box as drawn last (a strata map's boss frame is larger than the others).</summary>
        private float SelDrawnW, SelDrawnH;

        /// <summary>The depth column on the map's left (strata maps only), and what it shows.</summary>
        private RectTransform DepthLayer;

        private string DepthKey;

        /// <summary>The sizes the walker and the selection frame's hooks take on a map (the plain one's, or the chambers').</summary>
        private readonly struct MarkSize
        {
            public readonly float WalkerBox, WalkerUp, GlowInset, DiscInset, WalkerIcon, SelAbove, SelHook;

            private MarkSize(float walkerBox, float walkerUp, float glowInset, float discInset, float walkerIcon, float selAbove, float selHook)
            {
                WalkerBox = walkerBox;
                WalkerUp = walkerUp;
                GlowInset = glowInset;
                DiscInset = discInset;
                WalkerIcon = walkerIcon;
                SelAbove = selAbove;
                SelHook = selHook;
            }

            public static MarkSize Of(MapLayout l) =>
                new MarkSize(l.WalkerBox, l.WalkerUp, l.WalkerGlowInset, l.WalkerDiscInset, l.WalkerIcon, l.SelAbove, l.SelHook);

            public static MarkSize Of(StrataLayout s) =>
                new MarkSize(s.WalkerBox, s.WalkerUp, s.WalkerGlowInset, s.WalkerDiscInset, s.WalkerIcon, s.SelAbove, s.SelHook);

            public bool Same(MarkSize o) =>
                WalkerBox == o.WalkerBox && WalkerUp == o.WalkerUp && GlowInset == o.GlowInset && DiscInset == o.DiscInset &&
                WalkerIcon == o.WalkerIcon && SelAbove == o.SelAbove && SelHook == o.SelHook;
        }

        /// <summary>The selection frame's box round node n, and how far its face's top stands above its centre.</summary>
        private float SelWOf(NodePanel n) => StrataSize != null ? StrataSize.SelW(n.Kind) : MapSize.SelW;

        private float SelHOf(NodePanel n) => StrataSize != null ? StrataSize.SelH(n.Kind) : MapSize.SelH;

        private float HalfHOf(NodePanel n) => StrataSize != null ? StrataSize.Height(n.Kind) / 2f : MapSize.Node / 2f;

        // js: scenes/explore/ExploreScene.js:252
        /// <summary>
        /// Builds the map layer's content into MapLayer (ExploreScene.js:253-269): pool, edges, nodes, selection frame,
        /// walker. Called once from the core's BuildDom, after it made MapLayer.
        /// </summary>
        private void BuildMapLayer()
        {
            // the lamp's pool of light on the ground where the player stands (world-v1 §3 rule 4: amber is where the player is)
            BoxGraphic pool = BoxGraphic.Make(MapLayer, PoolCss, 0f, 0f, PoolW, PoolH, "ocbg-explore-pool");
            Pool = pool.rectTransform;
            PoolNode = UiNode.Of(Pool);

            EdgesLayer = Ui.Div(MapLayer, "ocbg-explore-edges", 0f, 0f, ExploreStyle.StageW, ExploreStyle.StageH);
            NodesLayer = Ui.Div(MapLayer, "ocbg-explore-nodes", 0f, 0f, ExploreStyle.StageW, ExploreStyle.StageH);

            // the depth column (a strata map's; empty until one is built), under the marks
            DepthLayer = Ui.Div(MapLayer, "ocbg-explore-depth", StrataLayout.DepthX, StrataLayout.DepthY, StrataLayout.DepthW,
                StrataLayout.DepthH(Lair.LayerCount));
            UiNode.Of(DepthLayer).PointerEvents = PointerEvents.None;

            // the selection frame (opacity 0 until a node is pinned; hidden while a step runs)
            SelDrawnW = MapSize.SelW;
            SelDrawnH = MapSize.SelH;
            SelFrame = Ui.Div(MapLayer, "ocbg-explore-sel", 0f, 0f, SelDrawnW, SelDrawnH);
            DrawSelFrame();
            SelStates = CssStates.Of(SelFrame).Base(SelCss).State(".is-on", SelOnCss);
            SelStates.Apply(instant: true);
            WatchRootClass(".is-busy", SelStates, SelBusyCss);

            // the walker: glow (breathing) and the disc with the person
            Walker = Ui.Div(MapLayer, "ocbg-explore-walker", 0f, 0f, Marks.WalkerBox, Marks.WalkerBox);
            WalkerNode = UiNode.Of(Walker);
            WalkerNode.Z = WalkerZ;
            DrawWalker();
            WalkerStates = CssStates.Of(Walker).Base(WalkerCss(Marks.WalkerBox) + WalkerTransitionCss).State(".is-leaping", WalkerLeapingCss);
            WalkerStates.Apply(instant: true);

            // explore.css:371 .ocbg-explore.is-interlude .ocbg-explore-node { pointer-events: none; }
            WatchRootClass(".is-interlude", on =>
            {
                foreach (ExploreNodeView rec in NodeEls.Values) rec.SetPointerEvents(!on);
            });
        }

        /// <summary>OCB.TargetFrame(SEL.w, SEL.h, …) in the selection frame, at the box drawn last.</summary>
        private void DrawSelFrame()
        {
            Ocb.TargetFrame(SelFrame, SelDrawnW, SelDrawnH, new OcbFrameOpts { Hook = Marks.SelHook, Width = SelStroke, Glow = SelGlow });
        }

        /// <summary>The selection frame drawn again at another box (a strata map's boss, or a map of another size).</summary>
        private void RedrawSelFrame(float w, float h)
        {
            SelDrawnW = w;
            SelDrawnH = h;
            if (!SelFrame) return;
            Ui.Clear(SelFrame);
            Ui.Size(SelFrame, w, h);
            DrawSelFrame();
        }

        /// <summary>The walker's glow (breathing) and its disc with the person, at the map's size.</summary>
        private void DrawWalker()
        {
            float box = Marks.WalkerBox, glowInset = Marks.GlowInset, discInset = Marks.DiscInset, icon = Marks.WalkerIcon;
            float glowSize = box - 2f * glowInset;
            BoxGraphic glow = BoxGraphic.Make(Walker, WalkerGlowCss, glowInset, glowInset, glowSize, glowSize, "ocbg-explore-walker-glow");
            // explore.css:401 @media (prefers-reduced-motion: reduce) .ocbg-explore-walker-glow { animation: none } (the same
            // rule's `.ocbg-explore-node-face` loses to the reachable / jump rules' specificity: the beckon keeps running)
            if (!Reduce) CssAnimator.Play(glow, BreatheAnim);
            float discSize = box - 2f * discInset;
            WalkerDisc = BoxGraphic.Make(Walker, WalkerDiscCss, discInset, discInset, discSize, discSize, "ocbg-explore-walker-disc");
            PathGraphic person = ExploreIcons.Ico(WalkerDisc.rectTransform, "person", icon, ExploreStyle.Ink);
            Ui.Place(person.rectTransform, (discSize - icon) / 2f, (discSize - icon) / 2f);
        }

        /// <summary>
        /// A map of another size (#1049) or another look (#1050): the selection frame and the walker are drawn again at
        /// it. Their states (on, busy, leaping) stay: only their boxes and what is inside change.
        /// </summary>
        private void ResizeMapMarks(ExplorationScreen scr)
        {
            bool strata = scr.Look == MapLook.Strata && scr.Strata != null && scr.Palette != null;
            ShownLook = strata ? MapLook.Strata : MapLook.Plain;
            StrataSize = strata ? scr.Strata : null;
            StrataColours = strata ? scr.Palette : null;
            MapSize = scr.Layout ?? MapLayout.Full;
            MarkSize next = strata ? MarkSize.Of(scr.Strata) : MarkSize.Of(MapSize);
            // every size follows the face, so the same marks are the same look
            if (next.Same(Marks)) return;
            Marks = next;
            RedrawSelFrame(SelDrawnW, SelDrawnH);
            if (Walker)
            {
                Ui.Clear(Walker);
                Ui.Size(Walker, next.WalkerBox, next.WalkerBox);
                DrawWalker();
                if (WalkerStates)
                {
                    WalkerStates.Base(WalkerCss(next.WalkerBox) + WalkerTransitionCss);
                    WalkerStates.Apply(instant: true);
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        // js: scenes/explore/ExploreScene.js:409
        /// <summary>nodePos(n): the node's centre in stage px.</summary>
        private Vector2 NodePos(NodePanel n) => new Vector2(ExploreStyle.NodeX(n.X), ExploreStyle.NodeY(n.Y));

        // js: scenes/explore/ExploreScene.js:417
        /// <summary>
        /// The walker at node centre (x, y). `perch` 1 stands it on the node's top edge, 0 puts it on the edge line itself
        /// (mid-step); `hop` lifts it for the stride. The pool follows the node centre.
        /// </summary>
        private void SetWalker(float x, float y, float perch = 1f, float hop = 0f)
        {
            if (WalkerNode)
            {
                WalkerNode.SetTransform(x - Marks.WalkerBox / 2f,
                    y - Marks.WalkerUp * perch - Marks.WalkerBox / 2f - hop, 1f, 1f, 0f);
            }
            if (PoolNode) PoolNode.SetTransform(x - PoolW / 2f, y - PoolH / 2f, 1f, 1f, 0f);
        }

        // js: scenes/explore/ExploreScene.js:433
        /// <summary>
        /// The node icon's colour by role: an elite is the enemy's violet (phosphor is the miasma's icon), the boss its own
        /// colour with the double frame, a trace of the previous heir the player's amber, others ink.
        /// </summary>
        private static Rgba KindColor(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Elite: return ExploreStyle.Violet;
                case NodeKind.Boss: return ExploreStyle.Boss;
                case NodeKind.Trace: return ExploreStyle.Amber;
                default: return ExploreStyle.Ink;
            }
        }

        // js: scenes/explore/ExploreScene.js:441
        private static Rgba NodeIconColor(NodePanel n) => n.Standing == NodeStanding.Far ? ExploreStyle.Steel : KindColor(n.Kind);

        /// <summary>Math.hypot(dx, dy).</summary>
        private static float Hypot(float dx, float dy) => (float)Math.Sqrt((double)dx * dx + (double)dy * dy);

        // ------------------------------------------------------------------ the map

        // js: scenes/explore/ExploreScene.js:446
        /// <summary>
        /// buildMap(scr, enter): the edges and the nodes of a layer. `enter` assembles them row by row: each edge draws on
        /// (its dash offset L → 0 over dur(520), decel) after dur(260 + row·90), each node rises into place (opacity 0 →
        /// 1 over dur(420) ease-out, translateY(14) → 0 over dur(420) decel) after dur(200 + row·90).
        /// </summary>
        private void BuildMap(ExplorationScreen scr, bool enter)
        {
            ResizeMapMarks(scr);
            bool strata = ShownLook == MapLook.Strata;
            Ui.Clear(NodesLayer);
            Ui.Clear(EdgesLayer);
            NodeEls.Clear();
            EdgeEls.Clear();
            Trail = null; // went with the edges (textContent = '')
            TrailLine = null;
            TrailStates = null;
            DrawDepth(scr);

            foreach (EdgeLine e in scr.Edges)
            {
                NodePanel na = scr.Node(e.From);
                NodePanel nb = scr.Node(e.To);
                if (na == null || nb == null) continue;
                Vector2 a = NodePos(na);
                Vector2 b = NodePos(nb);
                ExploreEdgeView rec;
                if (strata)
                {
                    // a tunnel runs from the upper chamber's floor down to the lower one's ceiling
                    NodePanel up = na.Row <= nb.Row ? na : nb, down = ReferenceEquals(up, na) ? nb : na;
                    Vector2 pu = NodePos(up), pd = NodePos(down);
                    Tunnel t = StrataSize.TunnelBetween(new MapPoint(pu.x, pu.y), up.Kind, new MapPoint(pd.x, pd.y), down.Kind);
                    rec = ExploreEdgeView.MakeTunnel(EdgesLayer, e, t, StrataSize, StrataColours, Reduce);
                }
                else
                {
                    float len = Hypot(b.x - a.x, b.y - a.y);
                    float k = MapSize.EdgeInset / len;
                    float x1 = a.x + (b.x - a.x) * k;
                    float y1 = a.y + (b.y - a.y) * k;
                    float x2 = b.x - (b.x - a.x) * k;
                    float y2 = b.y - (b.y - a.y) * k;
                    rec = ExploreEdgeView.Make(EdgesLayer, e, x1, y1, x2, y2, Reduce);
                }
                if (enter)
                {
                    float L = rec.DrawnLength;
                    rec.BeginDrawIn(L);
                    int row = na.Row;
                    Timeout(Dur(260f + row * 90f), () =>
                    {
                        if (!rec.Alive) return;
                        // base.style.transition = `stroke-dashoffset ${dur(520)}ms cubic-bezier(.22,.61,.36,1)`; strokeDashoffset = '0'
                        float drawMs = Dur(520f);
                        rec.DrawTransition(drawMs);
                        Clock.Run(drawMs, Ease.Linear, (_, raw) =>
                        {
                            if (rec.Alive) rec.SetDrawOffset(L * (1f - Ease.Decel(raw)));
                        });
                    });
                    Timeout(Dur(260f + row * 90f + 560f), () =>
                    {
                        if (rec.Alive) rec.EndDrawIn();
                    });
                }
                EdgeEls.Add(rec);
            }

            bool interlude = HasRootClass(".is-interlude");
            foreach (NodePanel n in scr.Nodes)
            {
                Vector2 p = NodePos(n);
                ExploreNodeView rec = strata
                    ? ExploreNodeView.MakeChamber(NodesLayer, n, StrataSize, StrataColours, p.x, p.y)
                    : ExploreNodeView.Make(NodesLayer, n, MapSize, p.x - MapSize.Node / 2f, p.y - MapSize.Node / 2f);
                int id = n.Id;
                rec.Hit.Enter += _ => OnHover(id, true);
                rec.Hit.Exit += _ => OnHover(id, false);
                rec.Hit.Click += _ => OnNodeClick(id);
                if (interlude) rec.SetPointerEvents(false);
                if (enter)
                {
                    UiNode el = UiNode.Of(rec.El);
                    el.Opacity = 0f;
                    el.TranslateY = 14f;
                    // (the JS clears the inline transition again after dur(200 + row·90 + 460): nothing to undo here)
                    Timeout(Dur(200f + n.Row * 90f), () =>
                    {
                        if (!el) return;
                        // transition: opacity dur(420)ms ease-out, transform dur(420)ms cubic-bezier(.22,.61,.36,1); opacity / transform → ''
                        float riseMs = Dur(420f);
                        float o0 = el.Opacity, y0 = el.TranslateY;
                        Clock.Run(riseMs, Ease.Linear, (_, raw) =>
                        {
                            if (!el) return;
                            el.Opacity = o0 + (1f - o0) * Ease.CssOut(raw);
                            el.TranslateY = y0 * (1f - Ease.Decel(raw));
                        });
                    });
                }
                NodeEls[n.Id] = rec;
            }
        }

        // js: scenes/explore/ExploreScene.js:506
        /// <summary>updateMap(scr): standings, live / pinned / jump marks, walked and lit edges, the selection frame.</summary>
        private void UpdateMap(ExplorationScreen scr)
        {
            if (scr == null) return;
            foreach (NodePanel n in scr.Nodes)
            {
                if (!NodeEls.TryGetValue(n.Id, out ExploreNodeView rec) || !rec.Alive) continue;
                if (rec.Standing != n.Standing) rec.SetStanding(n, NodeIconColor(n));
                rec.SetLive(scr.MapIsLive && !Busy);
                rec.SetPinned(n.Id == Pinned);
                // trial: while a 標の石 is aimed, the nodes it can land on are marked
                rec.SetJump(scr.Aiming && scr.MapIsLive && !Busy && HasJump(n));
                rec.Commit();
            }

            var byKey = new Dictionary<string, ExploreEdgeView>(StringComparer.Ordinal);
            foreach (ExploreEdgeView r in EdgeEls) byKey[EdgeKey(r.Edge.From, r.Edge.To)] = r;
            foreach (EdgeLine edge in scr.Edges)
            {
                if (!byKey.TryGetValue(EdgeKey(edge.From, edge.To), out ExploreEdgeView rec) || !rec.Alive) continue;
                NodePanel a = scr.Node(edge.From);
                NodePanel b = scr.Node(edge.To);
                if (a == null || b == null) continue;
                bool lit = edge.Lit && scr.MapIsLive && !Busy && !scr.Aiming;
                rec.SetClass(lit ? ExploreEdgeView.ClsLit : Walked(a.Standing) && Walked(b.Standing) ? ExploreEdgeView.ClsWalked : ExploreEdgeView.ClsNone);
                // the flow runs from where the player stands toward where they can go
                if (lit) rec.FlowFrom(a.Standing == NodeStanding.Current);
            }

            NodePanel pn = scr.Node(Pinned);
            bool fresh = SelFresh;
            SelFresh = false;
            if (pn != null)
            {
                Vector2 p = NodePos(pn);
                float sw = SelWOf(pn), sh = SelHOf(pn);
                if (sw != SelDrawnW || sh != SelDrawnH) RedrawSelFrame(sw, sh);
                // the frame sits SelAbove (18 px in the JS) over the node's top: it frames the node and its label (a
                // chamber's label stands beside it, outside the frame)
                float tx = p.x - sw / 2f, ty = p.y - HalfHOf(pn) - Marks.SelAbove;
                SelStates.SetState(".is-on", true, false);
                // selFrame.style.transform = translate(…): the inline transform (transition: transform 160ms decel)
                SelStates.State("style", "transform: translate(" + Px(tx) + ", " + Px(ty) + ");", true);
            }
            else SelStates.SetState(".is-on", false, false);
            // the frame drawn in the task that built it takes its first place and look at once
            SelStates.Apply(instant: fresh);
        }

        /// <summary>Walked: both ends were stood on (Current / Revisit / Resolved).</summary>
        private static bool Walked(NodeStanding s) => s == NodeStanding.Current || s == NodeStanding.Revisit || s == NodeStanding.Resolved;

        private static bool HasJump(NodePanel n)
        {
            foreach (StepOption o in n.Options) if (o.Jump) return true;
            return false;
        }

        private static string EdgeKey(int from, int to) =>
            from.ToString(CultureInfo.InvariantCulture) + "-" + to.ToString(CultureInfo.InvariantCulture);

        private static string Px(float v) => v.ToString("R", CultureInfo.InvariantCulture) + "px";

        // js: scenes/explore/ExploreScene.js:547
        private void OnHover(int id, bool enter)
        {
            if (Screen == null) return;
            if (enter)
            {
                if (Hovered != id) Sfx("nodeHover");
                Hovered = id;
            }
            else if (Hovered == id) Hovered = -1;
            DrawDetail();
        }

        // js: scenes/explore/ExploreScene.js:556
        private void OnNodeClick(int id)
        {
            if (Screen == null || !Screen.MapIsLive || Busy || Plate != null) return;
            NodePanel node = Screen.Node(id);
            if (node == null) return;
            if (Pinned == id && node.Options.Count > 0)
            {
                TakeOption(node, 0);
                return;
            }
            Pinned = id;
            Sfx("uiClick");
            UpdateMap(Screen);
            DrawDetail();
        }

        // js: scenes/explore/ExploreScene.js:571
        /// <summary>
        /// Arrow keys: the nearest node in that direction, preferring the ones the player can walk to: among the nodes
        /// more than 4 px along the direction, the lowest along + 2.2·|across| (× 0.6 for a node with a way in).
        /// </summary>
        private void MoveSelection(int dx, int dy)
        {
            if (Screen == null) return;
            NodePanel from = Screen.Node(Pinned) ?? Screen.Node(Screen.CurrentNodeId);
            if (from == null) return;
            Vector2 p0 = NodePos(from);
            NodePanel best = null;
            float bestScore = float.PositiveInfinity;
            foreach (NodePanel n in Screen.Nodes)
            {
                if (n.Id == from.Id) continue;
                Vector2 p = NodePos(n);
                float vx = p.x - p0.x;
                float vy = p.y - p0.y;
                float along = vx * dx + vy * dy;
                if (along <= 4f) continue;
                float across = Math.Abs(vx * dy - vy * dx);
                float score = along + across * 2.2f;
                if (n.Options.Count > 0) score *= 0.6f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = n;
                }
            }
            if (best == null) return;
            Pinned = best.Id;
            Hovered = -1;
            Sfx("nodeHover");
            UpdateMap(Screen);
            DrawDetail();
        }

        // ------------------------------------------------------------------ steps

        // js: scenes/explore/ExploreScene.js:1241
        private void TakeOption(NodePanel node, int index)
        {
            if (Screen == null || !Screen.MapIsLive || Busy || Plate != null || ConfirmOpen) return;
            if (node == null || index < 0 || index >= node.Options.Count) return;
            StepOption opt = node.Options[index];
            if (opt == null) return;
            ExplorationScreen before = Screen;
            bool jump = opt.Jump; // trial: 標の石's jump (ExplorationSession.Jump)
            if (!(jump ? Session.Jump(node.Id, opt.Choice) : Session.Step(node.Id, opt.Choice))) return;
            if (Session.HasPendingStep)
            {
                StepInfo = new ExploreStepInfo { Before = before, Node = node, Choice = opt.Choice, Jump = jump };
                Render();
                return;
            }
            AfterMove(before, node, opt.Choice, jump).Forget();
        }

        // js: scenes/explore/ExploreScene.js:1252
        /// <summary>afterMove(before, node, choice, jump): the walk (or leap), its sounds, the arrival, then proceed().</summary>
        private async Promise AfterMove(ExplorationScreen before, NodePanel node, RestChoice choice, bool jump = false)
        {
            if (node == null)
            {
                Render();
                return;
            }
            // The step is in the session already: save it now, so a reload during the walk keeps it (tier1 R1-16).
            Persist();
            NodePanel fromNode = (before != null ? before.Node(before.CurrentNodeId) : null) ?? node;
            StepOption opt = null;
            foreach (StepOption o in node.Options)
                if (o.Choice == choice && o.Jump == jump) { opt = o; break; }
            if (opt == null)
                foreach (StepOption o in node.Options)
                    if (o.Choice == choice) { opt = o; break; }
            bool fresh = node.Standing == NodeStanding.Reachable || jump;
            StepForecast fc = opt?.Forecast;
            if (fresh && node.Kind == NodeKind.Carving)
            {
                // the carving names the layer: the lineage keeps it for the heirs after (the app's onCarving), saved with
                // the step so a reload during the walk cannot leave the session's carving read and the lineage's not
                try { Ctx.OnCarving?.Invoke(); }
                catch (Exception e) { Debug.LogError("[explore] carving " + e); }
            }
            Busy = true;
            SetRootClass(".is-busy", true);
            UpdateMap(Screen);
            DrawDetail();
            // what the step sounds like (docs/AUDIO.md): the step, then at most one result
            // A step that lowers the max stamina still swells low (no miasma behind it now, #888): the old line sound by
            // how many points it falls, 20 a point, up to the deepest line (80).
            int line = 0;
            bool expiry = fc != null && fc.IsExpiry;
            if (fc != null && fc.MaxStaminaAfter < fc.MaxStaminaBefore && !expiry) line = Math.Min(80, 20 * (fc.MaxStaminaBefore - fc.MaxStaminaAfter));
            // at most two sounds: the step, then one result (the calendar running out outranks the node's own; the
            // sound keeps the miasma death's name)
            string result = !fresh ? null
                : node.Kind == NodeKind.Rest ? (choice == RestChoice.Train ? "train" : "rest")
                : ExploreStyle.StepResult(node.Kind);
            try
            {
                if (Audio != null) Audio.StepTo(fresh, line, expiry, result);
            }
            catch (Exception) { /* optional */ }
            Hovered = -1;
            if (jump) Sfx("consumableUse");
            await (jump ? Leap(fromNode, node) : Walk(fromNode, node));
            if (NodeEls.TryGetValue(node.Id, out ExploreNodeView arrived) && arrived.Alive)
            {
                arrived.Arrive();
                Timeout(900f, () =>
                {
                    if (arrived.Alive) arrived.ClearArrived();
                });
            }
            Busy = false;
            // A trace opens its card on arrival; walked back onto with its legacy still undecided (a reload left it
            // open), it opens again — the choice is made at the place of death (concept-v3 §8.3, R-08 / R-11). Never when
            // the step itself ended the life (the calendar running out on arrival, §6.4): the heir takes nothing, and the
            // trace stays (R-09 / R-10).
            if (node.Kind == NodeKind.Trace && !Session.State.IsOver && (fresh || TraceWaiting() != null))
                PendingTrace = new ExploreTraceRef(Session.State.Profile.Layer, node.Id);
            if (fresh && node.Kind == NodeKind.Rest && choice == RestChoice.Train && !Session.Trial) PendingTraining = true; // trial: TrainingWaiting
            if (fresh && node.Kind == NodeKind.Carving) Timeout(Dur(300f), () => LayerBanner().Forget());
            // the survey says what it raised (X-16); with 写しの帳 the line waits for the pick (onCardAction)
            if (fresh && node.Kind == NodeKind.Survey && !Session.State.IsOver)
                NoteBanner(ExploreText.SurveyKicker, ExplorationScreenBuilder.SurveyLine(Session.Raw), "Survey");
            Persist();
            Render();
            Proceed().Forget();
        }

        // js: scenes/explore/ExploreScene.js:1306
        /// <summary>
        /// The trace the heir stands on whose legacy is still to be decided ({ layer, nodeId }), or null: the current node
        /// is this layer's 死亡地点の痕跡 and the lineage still lets this life take it (traceNodeView.CanTake).
        /// </summary>
        private ExploreTraceRef TraceWaiting()
        {
            Func<int, int, InheritanceCore.TraceNodeView> traceView = Ctx.Inheritance?.TraceView;
            if (traceView == null) return null;
            ExplorationState state = Session.State;
            if (state.IsOver || state.Phase != RunPhase.Exploring) return null;
            MapNode node = state.Map.Node(state.CurrentNodeId);
            if (node == null || node.Kind != NodeKind.Trace || !state.HasResolved(node.Id)) return null;
            InheritanceCore.TraceNodeView view;
            try { view = traceView(state.Profile.Layer, node.Id); }
            catch (Exception) { return null; }
            return view != null && view.CanTake ? new ExploreTraceRef(state.Profile.Layer, node.Id) : null;
        }

        // js: scenes/explore/ExploreScene.js:1322
        /// <summary>
        /// walk(fromNode, toNode): the walker steps down off its perch onto the edge, walks the edge line from one node to
        /// the next while the edge lights up behind it, and steps up onto the new node; the band's numbers roll as it
        /// lands (render at t ≥ .7). ms = dur(min(820, 420 + len·0.9)), EASE.inout; perch: smooth over the first and last
        /// 24 %; hop sin(πt)·10.
        /// </summary>
        private Promise Walk(NodePanel fromNode, NodePanel toNode)
        {
            // on a strata map the walker goes through the tunnel between the two chambers, when there is one
            ExploreEdgeView tunnel = TunnelBetween(fromNode, toNode);
            if (tunnel != null) return WalkTunnel(tunnel.Tunnel, fromNode.Row < toNode.Row);
            var done = new PromiseSource();
            Vector2 a = NodePos(fromNode);
            Vector2 b = NodePos(toNode);
            float len = Hypot(b.x - a.x, b.y - a.y);
            if (!(len > 0f)) len = 1f;
            float ms = Dur(Math.Min(820f, 420f + len * 0.9f));
            float t0 = Now;
            // the trail runs along the drawn edge segment, from the node left toward the node reached
            float k = Math.Min(0.45f, MapSize.EdgeInset / len);
            float sx = a.x + (b.x - a.x) * k;
            float sy = a.y + (b.y - a.y) * k;
            float ex = b.x - (b.x - a.x) * k;
            float ey = b.y - (b.y - a.y) * k;
            float segLen = Hypot(ex - sx, ey - sy);
            PathGraphic trail = TrailFor(ExploreEdgeView.Segment(sx, sy, ex, ey), segLen);
            // walker.classList.add('is-walking') (no rule draws it)
            const float StepOff = 0.24f; // share of the stride spent stepping off the perch (and back up at the end)
            bool landed = false;
            WalkerAnim = now =>
            {
                float t = Math.Min(1f, (now - t0) / ms);
                float e = Ease.InOut(t);
                float perch = t < StepOff ? Ease.Smooth(1f - t / StepOff) : t > 1f - StepOff ? Ease.Smooth((t - (1f - StepOff)) / StepOff) : 0f;
                SetWalker(a.x + (b.x - a.x) * e, a.y + (b.y - a.y) * e, perch, (float)Math.Sin(Math.PI * t) * 10f);
                float along = Math.Max(0f, Math.Min(1f, (e * len - k * len) / Math.Max(1f, len - 2f * k * len)));
                if (trail) trail.DashOffset = segLen * (1f - along);
                if (!landed && t >= 0.7f)
                {
                    landed = true;
                    Render();
                }
                if (t >= 1f)
                {
                    WalkerAnim = null;
                    Landed();
                    CssStates fading = trail ? TrailStates : null;
                    if (fading) fading.SetState(".is-fading", true);
                    Timeout(Dur(520f), () =>
                    {
                        if (WalkerAnim == null && trail && fading && fading.Has(".is-fading")) RemoveTrail(trail);
                    });
                    done.TryResolve();
                }
            };
            if (!(ms > 0f)) WalkerAnim(t0 + 1f);
            return done.Promise;
        }

        /// <summary>The tunnel drawn between two chambers of a strata map, or null (a plain map, or no link between them).</summary>
        private ExploreEdgeView TunnelBetween(NodePanel a, NodePanel b)
        {
            if (ShownLook != MapLook.Strata || a == null || b == null) return null;
            foreach (ExploreEdgeView e in EdgeEls)
            {
                if (!e.Alive || e.Tunnel == null) continue;
                if ((e.Edge.From == a.Id && e.Edge.To == b.Id) || (e.Edge.From == b.Id && e.Edge.To == a.Id)) return e;
            }
            return null;
        }

        /// <summary>
        /// walk() through a tunnel (#1050): the walker steps off its perch, follows the tunnel from one chamber's centre to
        /// the other's (down it, or back up it), and steps up again; the trail lights the curve behind it. The timing,
        /// the perch, the hop and the landing are the plain walk's.
        /// </summary>
        private Promise WalkTunnel(Tunnel t, bool downward)
        {
            var done = new PromiseSource();
            float len = Math.Max(1f, t.RouteLength);
            float ms = Dur(Math.Min(820f, 420f + len * 0.9f));
            float t0 = Now;
            PathGraphic trail = TrailFor(ExploreEdgeView.Curve(t, !downward), t.Length);
            const float StepOff = 0.24f;
            bool landed = false;
            WalkerAnim = now =>
            {
                float f = Math.Min(1f, (now - t0) / ms);
                float e = Ease.InOut(f);
                float perch = f < StepOff ? Ease.Smooth(1f - f / StepOff) : f > 1f - StepOff ? Ease.Smooth((f - (1f - StepOff)) / StepOff) : 0f;
                MapPoint at = t.Route(e, downward);
                SetWalker(at.X, at.Y, perch, (float)Math.Sin(Math.PI * f) * 10f);
                if (trail) trail.DashOffset = t.Length * (1f - t.CurveShare(e, downward));
                if (!landed && f >= 0.7f)
                {
                    landed = true;
                    Render();
                }
                if (f >= 1f)
                {
                    WalkerAnim = null;
                    Landed();
                    CssStates fading = trail ? TrailStates : null;
                    if (fading) fading.SetState(".is-fading", true);
                    Timeout(Dur(520f), () =>
                    {
                        if (WalkerAnim == null && trail && fading && fading.Has(".is-fading")) RemoveTrail(trail);
                    });
                    done.TryResolve();
                }
            };
            if (!(ms > 0f)) WalkerAnim(t0 + 1f);
            return done.Promise;
        }

        /// <summary>
        /// The trail line of a walk (ExploreScene.js:1336-1341): the path walked (`d`), strokeDasharray `segLen segLen+10`,
        /// strokeDashoffset segLen (all gap), is-fading removed, appended last to the edges. A trail still fading from the
        /// walk before comes back (its opacity transitions up again); one that was removed is made anew (no transition).
        /// </summary>
        private PathGraphic TrailFor(string d, float segLen)
        {
            if (!Trail)
            {
                Trail = PathGraphic.Make(EdgesLayer, 0f, 0f, ExploreStyle.StageW, ExploreStyle.StageH, "ocbg-explore-trail");
                TrailLine = Trail.Add(ExploreEdgeView.LineLayer(d, TrailCss));
                TrailStates = CssStates.Of(Trail).Base(TrailCss).State(".is-fading", TrailFadingCss);
                TrailStates.Apply(instant: true);
            }
            else
            {
                TrailLine.Paths.Clear();
                TrailLine.Paths.Add(PathData.Cached(d));
                TrailStates.SetState(".is-fading", false);
            }
            StrokeStyle st = (TrailLine.StrokeStyle ?? new StrokeStyle(1f)).Clone();
            st.Dash = new[] { segLen, segLen + 10f };
            TrailLine.StrokeStyle = st;
            Trail.LayersChanged();
            Trail.DashOffset = segLen;
            Trail.transform.SetAsLastSibling();
            return Trail;
        }

        /// <summary>trail.remove().</summary>
        private void RemoveTrail(PathGraphic trail)
        {
            if (trail) Ui.Remove(trail);
            if (ReferenceEquals(trail, Trail))
            {
                Trail = null;
                TrailLine = null;
                TrailStates = null;
            }
        }

        /// <summary>walker.classList.remove('is-landed'); reflow; add('is-landed'): the disc's landing squash runs again.</summary>
        private void Landed()
        {
            if (WalkerDisc) CssAnimator.Set(WalkerDisc, LandAnim);
        }

        // ------------------------------------------------------------------ the depth column (#1050)

        /// <summary>A layer's cell: the panel, and how it stands (this one amber, a passed one solid, a deeper one dashed).</summary>
        private const string DepthCellCss = "border-radius: var(--radius-panel); background: var(--panel-solid);";

        private const string DepthCurrentCss = "border: 3px solid var(--amber);";
        private const string DepthPassedCss = "border: 1px solid var(--rule);";
        private const string DepthBelowCss = "border: 1px dashed var(--steel);";

        /// <summary>The king room keeps the boss's double frame.</summary>
        private const string DepthKingCss = "border: 2px solid var(--boss); outline: 2px solid var(--boss); outline-offset: 3px;";

        /// <summary>
        /// The depth column on a strata map: 「深さ」 over the seven layers top down, the one the player is on taller, framed
        /// in amber and lit by a strip of its own light; nothing on a plain map. Rebuilt only when the layer changes.
        /// </summary>
        private void DrawDepth(ExplorationScreen scr)
        {
            if (!DepthLayer) return;
            bool on = ShownLook == MapLook.Strata && scr.Depths.Count > 0;
            string key = on ? scr.Layer.ToString(CultureInfo.InvariantCulture) : "-";
            if (key == DepthKey) return;
            DepthKey = key;
            Ui.Clear(DepthLayer);
            if (!on) return;

            TextStyle heading = TextStyle.FromCss("font: 700 " + Px(StrataLayout.DepthHeadingSize) + "/" + Px(StrataLayout.DepthHeadingLine)
                                                  + " var(--font-body); color: var(--ink); white-space: nowrap; text-shadow: 0 0 6px var(--ground);");
            TextStyle number = TextStyle.FromCss("font: 700 " + Px(StrataLayout.DepthNumberSize) + "/" + Px(StrataLayout.DepthNumberSize + 6f)
                                                 + " var(--font-mono); color: var(--ink); white-space: nowrap;");
            TextStyle note = TextStyle.FromCss("font: 700 " + Px(StrataLayout.DepthNoteSize) + "/" + Px(StrataLayout.DepthNoteSize + 4f)
                                               + " var(--font-body); color: var(--ink); white-space: nowrap;");
            TMP_Text head = Label.Make(DepthLayer, ExplorationText.DepthHeading, heading, 0f, 0f, null, StrataLayout.DepthHeadingLine, "heading");
            TextGlow.For(head);

            float y = StrataLayout.DepthHeadingLine + StrataLayout.DepthGap;
            const float pad = 10f;
            foreach (DepthCell cell in scr.Depths)
            {
                bool current = cell.Mark == DepthMark.Current;
                float h = current ? StrataLayout.DepthCurrentH : StrataLayout.DepthCellH;
                string css = DepthCellCss + (cell.King ? DepthKingCss : current ? DepthCurrentCss : cell.Mark == DepthMark.Passed ? DepthPassedCss : DepthBelowCss);
                if (current && cell.King) css += " " + DepthCurrentCss; // amber wins on the layer the player is on
                BoxGraphic box = BoxGraphic.Make(DepthLayer, css, 0f, y, StrataLayout.DepthW, h, "ocbg-explore-depth-cell");
                float x = pad;
                if (current && StrataColours != null)
                {
                    // the layer's own light, as its chambers show it
                    BoxGraphic.Make(box.rectTransform, "border-radius: 3px; background: linear-gradient(180deg, " + StrataColours.LitTop + ", "
                                                       + StrataColours.LitBottom + ");", pad, pad, StrataLayout.DepthLightW, h - 2f * pad, "light");
                    x += StrataLayout.DepthLightW + 8f;
                }
                Label.Make(box.rectTransform, cell.Number, number, x, pad - 2f, null, StrataLayout.DepthNumberSize + 6f, "number");
                if (cell.Note.Length > 0)
                    Label.Make(box.rectTransform, cell.Note, note, x, h - pad - (StrataLayout.DepthNoteSize + 4f), null, StrataLayout.DepthNoteSize + 4f, "note");
                y += h + StrataLayout.DepthGap;
            }
        }

        // js: scenes/explore/ExploreScene.js:1369
        /// <summary>
        /// Trial: 標の石 — the walker fades where it stood (is-leaping: opacity 240ms) and, after dur(320), appears on the node
        /// it jumped to (render, fade back in, the landing squash); resolves dur(260) later.
        /// </summary>
        private Promise Leap(NodePanel fromNode, NodePanel toNode)
        {
            var done = new PromiseSource();
            Vector2 b = NodePos(toNode);
            if (WalkerStates) WalkerStates.SetState(".is-leaping", true);
            Timeout(Dur(320f), () =>
            {
                SetWalker(b.x, b.y);
                Render();
                if (WalkerStates) WalkerStates.SetState(".is-leaping", false);
                Landed();
                Timeout(Dur(260f), () => done.TryResolve());
            });
            return done.Promise;
        }
    }
}
