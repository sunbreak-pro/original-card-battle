// The target frame (four corner hooks), the prediction tag and the throw line (vendor/ds/bundle.js:504-523,
// bundle.css:144-148).
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>TargetFrame's o: { hook = 26, width = 3, reachable, glow = 10, glowAlpha = .35 }.</summary>
    public sealed class OcbFrameOpts
    {
        /// <summary>The hooks' length (0 = 26, as `o.hook || 26`).</summary>
        public float Hook;
        /// <summary>The stroke width (0 = 3, as `o.width || 3`); an unreachable frame draws 2.</summary>
        public float Width;
        /// <summary>o.reachable: false draws the steel frame without its glow; true or null the wick one.</summary>
        public bool? Reachable;
        /// <summary>The glow's blur (null = 10).</summary>
        public float? Glow;
        /// <summary>The glow's alpha (null = .35).</summary>
        public float? GlowAlpha;
    }

    /// <summary>A built target frame.</summary>
    public sealed class OcbTargetFrame : OcbElement
    {
        public PathGraphic Svg { get; internal set; }
        /// <summary>The keyline stroke under the colour stroke (null when --keyline is transparent: dusk).</summary>
        public PathLayer Keyline { get; internal set; }
        public PathLayer Stroke { get; internal set; }
    }

    public static partial class Ocb
    {
        // js: vendor/ds/bundle.js:511
        /// <summary>The four hooks' path for a w × h frame with hooks of `len` (bundle.js:511, the numbers as JS prints them).</summary>
        public static string TargetFramePath(float w, float h, float len) =>
            "M0 " + Num(len) + "V0H" + Num(len) + "M" + Num(w - len) + " 0H" + Num(w) + "V" + Num(len) + "M" + Num(w) + " " + Num(h - len)
            + "V" + Num(h) + "H" + Num(w - len) + "M" + Num(len) + " " + Num(h) + "H0V" + Num(h - len);

        // js: vendor/ds/bundle.js:507
        /// <summary>
        /// TargetFrame(w, h, o): .ocb-frame w × h (pointer-events: none) holding an svg (overflow visible) of the four corner
        /// hooks, square caps, no fill: the keyline stroke (--keyline, width + 4; it draws nothing in dusk) under the colour
        /// stroke (--wick, `width`; unreachable: --steel, 2). A reachable frame glows: filter drop-shadow(0 0 glow px
        /// rgba(232,168,76, glowAlpha)), σ = glow. At (0, 0) of parent.
        /// </summary>
        public static OcbTargetFrame TargetFrame(RectTransform parent, float w, float h, OcbFrameOpts o = null)
        {
            o = o ?? new OcbFrameOpts();
            float len = o.Hook > 0f || o.Hook < 0f ? o.Hook : 26f;
            bool unreachable = o.Reachable == false;
            float sw = unreachable ? 2f : (o.Width > 0f || o.Width < 0f ? o.Width : 3f);
            Rgba col = unreachable ? DsTokens.Steel : DsTokens.Wick;
            string d = TargetFramePath(w, h, len);

            var el = Make<OcbTargetFrame>(parent, "ocb-frame", FrameCss);
            el.Node.Place(0f, 0f, w, h);
            el.Node.PointerEvents = PointerEvents.None;
            el.Svg = PathGraphic.Make(el.Root, 0f, 0f, w, h, "svg");
            el.Svg.ViewBox = new RectF(0f, 0f, w, h);
            Rgba keyline = DsTokens.Keyline(Theme);
            if (keyline.A > 0f) el.Keyline = el.Svg.AddStroke(d, keyline, sw + 4f, Cap.Square);
            el.Stroke = el.Svg.AddStroke(d, col, sw, Cap.Square);
            if (!unreachable)
            {
                float glow = o.Glow ?? 10f, alpha = o.GlowAlpha ?? 0.35f;
                // the filter acts on the svg's whole picture; the keyline is transparent in dusk, so the colour stroke casts it
                (el.Keyline ?? el.Stroke).WithShadow(0f, 0f, glow, Rgba.Bytes(232, 168, 76, alpha));
                el.Svg.LayersChanged();
            }
            el.SetPart("svg", el.Svg);
            return el;
        }

        // js: vendor/ds/bundle.js:520
        /// <summary>
        /// PredTag(icon, n, iconColor): .ocb-pred, 60 tall, padding 0 14, gap 8, panel-solid, 2px --wick, radius 6, 700 48/1
        /// mono wick, nowrap, translate(−50%, −50%) (place it at its centre): the icon 32 (default --phosphor) and n.
        /// </summary>
        public static OcbElement PredTag(RectTransform parent, string icon, string n, Rgba? iconColor = null)
        {
            OcbElement el = Make<OcbElement>(parent, "ocb-pred", PredCss);
            Icon(el.Root, icon, 32f, iconColor ?? DsTokens.Phosphor);
            el.SetPart("n", Text(el.Root, n ?? "", TextOf(PredCss), "n"));
            Flow(el.Root, PredCss);
            el.Node.SetStyleTransform(pctX: -0.5f, pctY: -0.5f);   // transform: translate(-50%, -50%)
            return el;
        }

        /// <summary>
        /// .ocb-throwline (bundle.css:148): a 3px line of `repeating-linear-gradient(90deg, var(--amber) 0 9px, transparent
        /// 9px 15px)` (9 on, 6 off from its left edge) at (x, y), w wide. Its drop-shadow is the keyline (transparent in dusk).
        /// </summary>
        public static HatchGraphic ThrowLine(RectTransform parent, float x, float y, float w)
        {
            HatchGraphic g = HatchGraphic.Stripes(parent, x, y, w, 3f, 90f, 0f, 9f, 15f, DsTokens.Amber, 0f, "ocb-throwline");
            g.TrySetStripesCss("repeating-linear-gradient(90deg, var(--amber) 0 9px, transparent 9px 15px)");
            return g;
        }
    }
}
