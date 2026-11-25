// One edge of the exploration map (ExploreScene.js:452-480 buildMap's edge, :522-538 updateMap's classes;
// explore.css:74-78, :82): the base line (.ocbg-explore-edge: dashed steel; walked: ink 55 % solid; lit: amber 4 with
// its drop-shadow), the flow line on a lit edge (.ocbg-explore-edge-flow: dashes 2 22 running from where the player
// stands, @keyframes ocbg-explore-flow 1.2s linear infinite), their end points, and the draw-in of a map that
// assembles (an inline stroke-dasharray / stroke-dashoffset with its own transition). Owned by EX-MAP.
//
// The SVG <g> is G; its class ('is-lit' / 'is-walked' / '') is a state of the base line's CssStates, which transitions
// stroke and stroke-width (300ms); the dash pattern, the caps and the drop-shadow have no transition and are set from
// the same declarations at once (the kit's CssStates leaves them to the element).
//
// On a map drawn as the rock's cross-section (#1050, MapLook.Strata: layer 1) the edge is a tunnel instead
// (MakeTunnel): Exploration.Tunnel's curve stroked thick in the rock's colours, the flow a dark line with the amber
// dashes over it, driven by the same classes.
using System;
using System.Globalization;
using Exploration;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Explore
{
    public sealed class ExploreEdgeView
    {
        // ------------------------------------------------------------------ explore.css (verbatim)

        /// <summary>explore.css:73 .ocbg-explore-edges (the svg, viewBox 0 0 1920 1080).</summary>
        public const string EdgesCss = "position: absolute; left: 0; top: 0; width: 1920px; height: 1080px; overflow: visible;";

        /// <summary>explore.css:74 .ocbg-explore-edge.</summary>
        public const string EdgeCss = "stroke: var(--steel); stroke-width: 2.5; stroke-linecap: round; stroke-dasharray: 3 9; fill: none; transition: stroke 300ms, stroke-width 300ms;";

        /// <summary>explore.css:75 .is-walked .ocbg-explore-edge.</summary>
        public const string WalkedCss = "stroke: color-mix(in srgb, var(--ink) 55%, transparent); stroke-dasharray: none;";

        /// <summary>explore.css:76 .is-lit .ocbg-explore-edge.</summary>
        public const string LitCss = "stroke: var(--amber); stroke-width: 4; stroke-dasharray: none; filter: drop-shadow(0 0 6px color-mix(in srgb, var(--amber) 55%, transparent));";

        /// <summary>explore.css:77 .ocbg-explore-edge-flow.</summary>
        public const string FlowCss = "stroke: color-mix(in srgb, var(--amber) 55%, var(--ink)); stroke-width: 4; stroke-linecap: round; stroke-dasharray: 2 22; opacity: 0; fill: none;";

        /// <summary>explore.css:78 .is-lit .ocbg-explore-edge-flow.</summary>
        public const string FlowLitCss = "opacity: .95; animation: ocbg-explore-flow 1.2s linear infinite;";

        /// <summary>explore.css:78 the flow's animation (explore.css:82 @keyframes ocbg-explore-flow: stroke-dashoffset 24 → 0).</summary>
        public const string FlowAnim = "ocbg-explore-flow 1.2s linear infinite";

        /// <summary>ExploreScene.js:475 the draw-in's inline transition timing (ms = dur(520)).</summary>
        public const string DrawInEase = "cubic-bezier(.22,.61,.36,1)";

        /// <summary>The g's classes (updateMap).</summary>
        public const string ClsLit = "is-lit", ClsWalked = "is-walked", ClsNone = "";

        // ------------------------------------------------------------------ the record buildMap keeps ({ edge, g, base, flow, coords })

        public EdgeLine Edge;

        /// <summary>The edge's group (.is-lit / .is-walked).</summary>
        public RectTransform G;

        /// <summary>.ocbg-explore-edge.</summary>
        public PathGraphic Base;

        /// <summary>.ocbg-explore-edge-flow.</summary>
        public PathGraphic Flow;

        /// <summary>coords: the drawn segment, EDGE_INSET from both node centres.</summary>
        public float X1, Y1, X2, Y2;

        /// <summary>The g's class drawn last (null until updateMap drew it).</summary>
        public string Cls;

        private PathLayer _base;
        private PathLayer _flow;
        private CssStates _states;
        private CssAnimator _flowAnim;
        private bool? _flowFromCurrent;
        private float[] _inlineDash;
        private bool _reduce;

        /// <summary>The flow's lines (one on the plain map; the dark line and the amber dashes over it in a tunnel).</summary>
        private readonly System.Collections.Generic.List<PathLayer> _flows = new System.Collections.Generic.List<PathLayer>();

        /// <summary>The rule of the flow shown (the plain one, or a tunnel's).</summary>
        private string _flowCss = FlowCss;

        /// <summary>The tunnel this edge is drawn as on a strata map (#1050); null on the plain map.</summary>
        public Tunnel Tunnel { get; private set; }

        /// <summary>The edge is still on screen (the map was not rebuilt since).</summary>
        public bool Alive => G;

        /// <summary>The drawn line's length (the straight segment, or the tunnel's curve).</summary>
        public float DrawnLength => Tunnel != null ? Tunnel.Length : (float)Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));

        // ------------------------------------------------------------------ the tunnel (#1050, battle-visual-v1 §2.5)

        /// <summary>A tunnel not walked: the dark rock's colour, thick, round at its ends (hidden under the chambers).</summary>
        private static string TunnelCss(StrataLayout s, StrataPalette p) =>
            "stroke: " + p.TunnelFar + "; stroke-width: " + Num(s.TunnelWidth) + "; stroke-linecap: round; fill: none; transition: stroke 300ms, stroke-width 300ms;";

        /// <summary>A tunnel walked: lit by the place.</summary>
        private static string TunnelWalkedCss(StrataPalette p) => "stroke: " + p.TunnelWalked + ";";

        /// <summary>A tunnel open from where the player stands: brighter still, the amber dashes down its middle.</summary>
        private static string TunnelLitCss(StrataPalette p) => "stroke: " + p.TunnelLit + ";";

        /// <summary>The dark line under the amber dashes (so the amber reads on the lit tunnel).</summary>
        private static readonly string TunnelFlowUnderCss =
            "stroke: " + StrataPalette.Ground + "; stroke-width: " + Num(StrataLayout.FlowUnderWidth) + "; stroke-linecap: round; stroke-dasharray: "
            + Num(StrataLayout.FlowDash) + " " + Num(StrataLayout.FlowGap) + "; opacity: 0; fill: none;";

        /// <summary>The amber dashes (24 a period, so the plain map's ocbg-explore-flow runs them).</summary>
        private static readonly string TunnelFlowCss =
            "stroke: var(--amber); stroke-width: " + Num(StrataLayout.FlowWidth) + "; stroke-linecap: round; stroke-dasharray: "
            + Num(StrataLayout.FlowDash) + " " + Num(StrataLayout.FlowGap) + "; opacity: 0; fill: none;";

        /// <summary>
        /// The edge of a strata map drawn as tunnel <paramref name="t"/>: the thick curve and, over it, the flow (the dark
        /// line and the amber dashes) shown while it is lit. Driven like a plain edge (SetClass / FlowFrom / the draw-in).
        /// </summary>
        public static ExploreEdgeView MakeTunnel(RectTransform parent, EdgeLine e, Tunnel t, StrataLayout s, StrataPalette p, bool reducedMotion = false)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var v = new ExploreEdgeView
            {
                Edge = e, X1 = t.Start.X, Y1 = t.Start.Y, X2 = t.End.X, Y2 = t.End.Y, _reduce = reducedMotion, Tunnel = t, _flowCss = TunnelFlowCss,
            };
            v.G = Ui.Div(parent, "g", 0f, 0f, ExploreStyle.StageW, ExploreStyle.StageH);
            string d = Curve(t, false);
            string baseCss = TunnelCss(s, p);

            v.Base = PathGraphic.Make(v.G, 0f, 0f, ExploreStyle.StageW, ExploreStyle.StageH, "ocbg-explore-tunnel");
            v._base = v.Base.Add(LineLayer(d, baseCss));
            v._states = CssStates.Of(v.Base).Base(baseCss).State("." + ClsWalked, TunnelWalkedCss(p)).State("." + ClsLit, TunnelLitCss(p));
            v._states.Apply(instant: true);

            v.Flow = PathGraphic.Make(v.G, 0f, 0f, ExploreStyle.StageW, ExploreStyle.StageH, "ocbg-explore-tunnel-flow");
            v._flows.Add(v.Flow.Add(LineLayer(d, TunnelFlowUnderCss)));
            v._flow = v.Flow.Add(LineLayer(d, TunnelFlowCss));
            v._flows.Add(v._flow);
            v.Flow.Opacity = OpacityOf(TunnelFlowCss);
            return v;
        }

        /// <summary>The tunnel's curve as an SVG path ("M x y C …"), from its upper end, or from its lower end when reversed.</summary>
        internal static string Curve(Tunnel t, bool reversed)
        {
            MapPoint a = reversed ? t.End : t.Start, b = reversed ? t.C2 : t.C1, c = reversed ? t.C1 : t.C2, d = reversed ? t.Start : t.End;
            return "M" + Num(a.X) + " " + Num(a.Y) + "C" + Num(b.X) + " " + Num(b.Y) + " " + Num(c.X) + " " + Num(c.Y) + " " + Num(d.X) + " " + Num(d.Y);
        }

        // js: scenes/explore/ExploreScene.js:461
        /// <summary>
        /// The edge of buildMap: a group with the base line and the flow line, both from (x1, y1) to (x2, y2) in stage px
        /// (the svg's viewBox is the stage), styled by their rules. reducedMotion: explore.css:401
        /// (@media (prefers-reduced-motion: reduce) .is-lit .ocbg-explore-edge-flow { animation: none }), the lit flow
        /// stands still.
        /// </summary>
        public static ExploreEdgeView Make(RectTransform parent, EdgeLine e, float x1, float y1, float x2, float y2, bool reducedMotion = false)
        {
            var v = new ExploreEdgeView { Edge = e, X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, _reduce = reducedMotion };
            v.G = Ui.Div(parent, "g", 0f, 0f, ExploreStyle.StageW, ExploreStyle.StageH);
            string d = Segment(x1, y1, x2, y2);

            v.Base = PathGraphic.Make(v.G, 0f, 0f, ExploreStyle.StageW, ExploreStyle.StageH, "ocbg-explore-edge");
            v._base = v.Base.Add(LineLayer(d, EdgeCss));
            v._states = CssStates.Of(v.Base).Base(EdgeCss).State("." + ClsWalked, WalkedCss).State("." + ClsLit, LitCss);
            v._states.Apply(instant: true);

            v.Flow = PathGraphic.Make(v.G, 0f, 0f, ExploreStyle.StageW, ExploreStyle.StageH, "ocbg-explore-edge-flow");
            v._flow = v.Flow.Add(LineLayer(d, FlowCss));
            v._flows.Add(v._flow);
            v.Flow.Opacity = OpacityOf(FlowCss);
            return v;
        }

        // js: scenes/explore/ExploreScene.js:530
        /// <summary>
        /// rec.g.setAttribute('class', cls): the base line takes the class's stroke (transitions 300ms; the first class is
        /// set in the task that built the edge, so it does not transition), dashes and drop-shadow; the flow shows and runs
        /// on a lit edge (its animation starts when the edge becomes lit, and keeps running while it stays lit).
        /// </summary>
        public void SetClass(string cls)
        {
            if (!G) return;
            cls = cls ?? ClsNone;
            if (cls == Cls) return;
            bool first = Cls == null;
            Cls = cls;
            _states.SetState("." + ClsWalked, cls == ClsWalked, false);
            _states.SetState("." + ClsLit, cls == ClsLit, false);
            _states.Apply(instant: first);
            ApplyStatic();

            bool lit = cls == ClsLit;
            Flow.Opacity = lit ? OpacityOf(_flowCss + FlowLitCss) : OpacityOf(_flowCss);
            if (lit && !_reduce && (_flowAnim == null || !_flowAnim.Running)) _flowAnim = CssAnimator.Play(Flow, FlowAnim);
            else if (!lit && _flowAnim != null)
            {
                _flowAnim.Stop();
                _flowAnim = null;
            }
        }

        // js: scenes/explore/ExploreScene.js:532
        /// <summary>The flow runs from where the player stands toward where they can go (fromCurrent: the edge's From end).</summary>
        public void FlowFrom(bool fromCurrent)
        {
            if (!G || _flowFromCurrent == fromCurrent) return;
            _flowFromCurrent = fromCurrent;
            string d = Tunnel != null ? Curve(Tunnel, !fromCurrent)
                : fromCurrent ? Segment(X1, Y1, X2, Y2) : Segment(X2, Y2, X1, Y1);
            foreach (PathLayer flow in _flows)
            {
                flow.Paths.Clear();
                flow.Paths.Add(PathData.Cached(d));
            }
            Flow.LayersChanged();
        }

        // ------------------------------------------------------------------ the draw-in (buildMap with enter)

        // js: scenes/explore/ExploreScene.js:471
        /// <summary>
        /// base.style.strokeDasharray = L; strokeDashoffset = L: the line is all gap until it draws on. (The gap is drawn
        /// 1 px longer than the dash so that rounding never leaves a capped dot at the far end.)
        /// </summary>
        public void BeginDrawIn(float length)
        {
            if (!G) return;
            _inlineDash = new[] { length, length + 1f };
            ApplyStatic();
            Base.DashOffset = length;
        }

        // js: scenes/explore/ExploreScene.js:475
        /// <summary>base.style.transition = 'stroke-dashoffset {ms}ms cubic-bezier(.22,.61,.36,1)' (it replaces the rule's).</summary>
        public void DrawTransition(float ms)
        {
            if (!G) return;
            _states.SetTransitions("stroke-dashoffset " + ms.ToString("R", CultureInfo.InvariantCulture) + "ms " + DrawInEase);
        }

        /// <summary>The inline stroke-dashoffset (the draw-in's transition writes it every frame).</summary>
        public void SetDrawOffset(float offset)
        {
            if (G) Base.DashOffset = offset;
        }

        // js: scenes/explore/ExploreScene.js:476
        /// <summary>base.style.strokeDasharray = ''; base.style.transition = '': the rules' dashes and transitions again.</summary>
        public void EndDrawIn()
        {
            if (!G) return;
            _inlineDash = null;
            _states.SetTransitions(null);
            ApplyStatic();
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// The declarations in force that the kit's CssStates does not write: stroke-dasharray (the inline one wins while
        /// the line draws on), stroke-linecap and filter: drop-shadow().
        /// </summary>
        private void ApplyStatic()
        {
            CssBlock b = global::Trial.Geometry.Css.Parse(_states.Effective());
            StrokeStyle st = (_base.StrokeStyle ?? new StrokeStyle(1f)).Clone();
            float[] dash = _inlineDash ?? b.Svg.DashArray;
            st.Dash = dash == null ? null : (float[])dash.Clone();
            if (b.Svg.LineCap.HasValue) st.Cap = b.Svg.LineCap.Value;
            _base.StrokeStyle = st;
            _base.Shadow = ShadowOf(b);
            Base.LayersChanged();
        }

        /// <summary>A stroked line drawn as a rule declares it: stroke, stroke-width, caps, joins, dashes, drop-shadow.</summary>
        internal static PathLayer LineLayer(string d, string css)
        {
            CssBlock b = global::Trial.Geometry.Css.Parse(css ?? "");
            var l = new PathLayer(d);
            float[] dash = b.Svg.DashArray == null ? null : (float[])b.Svg.DashArray.Clone();
            l.WithStroke(b.Svg.Stroke ?? Rgba.Transparent, b.Svg.StrokeWidth ?? 1f, b.Svg.LineCap ?? Cap.Butt, b.Svg.LineJoin ?? Join.Miter, dash);
            l.Shadow = ShadowOf(b);
            return l;
        }

        /// <summary>The first filter: drop-shadow() of a rule, or null.</summary>
        internal static DropShadow? ShadowOf(CssBlock b) =>
            b.Filter != null && b.Filter.DropShadows.Count > 0 ? b.Filter.DropShadows[0] : (DropShadow?)null;

        /// <summary>The opacity a rule declares (1 when it declares none).</summary>
        internal static float OpacityOf(string css) => global::Trial.Geometry.Css.Parse(css ?? "").Opacity ?? 1f;

        /// <summary>An SVG line as a path: "M x1 y1 L x2 y2" (the numbers as JS prints them).</summary>
        internal static string Segment(float x1, float y1, float x2, float y2) =>
            "M" + Num(x1) + " " + Num(y1) + "L" + Num(x2) + " " + Num(y2);

        private static string Num(float v) => v.ToString("R", CultureInfo.InvariantCulture);
    }
}
