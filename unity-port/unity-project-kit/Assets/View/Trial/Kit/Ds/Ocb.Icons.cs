// The DS icons, hatch and swatch (vendor/ds/bundle.js:63-148). The icon paths, labels and the FILLED set live in
// Trial.Geometry's DsIcons (engine-free, parsed by the dotnet tests); the drawing is the kit's PathGraphic (icon) and
// HatchGraphic (hatch). Every icon here is `.ocb-icon`: flex: none in a flex row (NoShrink).
using System.Collections.Generic;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    public static partial class Ocb
    {
        /// <summary>OCB.ICONS (bundle.js:64): name → 24×24 path.</summary>
        public static IReadOnlyDictionary<string, string> Icons => DsIcons.Paths;

        /// <summary>OCB.ICON_LABELS (bundle.js:102): name → what it means.</summary>
        public static IReadOnlyDictionary<string, string> IconLabels => DsIcons.Labels;

        /// <summary>FILLED (bundle.js:110): the icons drawn filled in their colour (heart, pip).</summary>
        public static IReadOnlyCollection<string> FilledIcons => DsIcons.Filled;

        // js: vendor/ds/bundle.js:113
        /// <summary>
        /// icon(name, size = 24, color = currentColor, { fill, fillOpacity, dashed, width }) as a size × size graphic at
        /// (0, 0) of parent: a filled icon (heart, pip) is its path in `color` (the options ignored); any other is
        /// stroked in `color`, `width` viewBox units (2.2) with round caps and joins, filled with `fill` at `fillOpacity`
        /// (only when a fill is given), dashed 2.4 2.4 when asked. An unknown name draws `plus`. `color` null =
        /// currentColor, which in every DS element is --ink (.ocb-root).
        /// </summary>
        public static PathGraphic Icon(RectTransform parent, string name, float size = 24f, Rgba? color = null, Rgba? fill = null,
            float? fillOpacity = null, bool dashed = false, float? width = null)
        {
            Rgba? f = fill;
            if (f.HasValue && fillOpacity.HasValue) f = f.Value.MulAlpha(fillOpacity.Value);   // fill-opacity
            float? sw = width.HasValue && (width.Value > 0f || width.Value < 0f) ? width : null;   // `opts.width || 2.2`
            PathGraphic g = PathGraphic.Icon(parent, name, size, color ?? DsTokens.Ink, f, sw, dashed);
            NoShrink(g);
            return g;
        }

        // js: vendor/ds/bundle.js:132
        /// <summary>
        /// hatch(w, h, angle = 45, gap = 11, color = --reach-red, width = 3): an svg of w × h at (x, y) of parent filled
        /// with a pattern of `width` px bars every `gap` px rotated by `angle` (0, as `angle || 45`, means 45), phase 0 at
        /// its top-left.
        /// </summary>
        public static HatchGraphic Hatch(RectTransform parent, float x, float y, float w, float h, float angle = 45f, float gap = 11f, Rgba? color = null, float width = 3f) =>
            HatchGraphic.Bars(parent, x, y, w, h, angle, gap, color ?? DsTokens.ReachRed, width, "hatch");

        /// <summary>The inline style of the swatch span (bundle.js:143), at a size.</summary>
        public static string SwatchCss(float size) =>
            "display: inline-block; width: " + Num(size) + "px; height: " + Num(size) + "px; border: 2px solid var(--reach-red); border-radius: 3px; overflow: hidden; flex: none;";

        // js: vendor/ds/bundle.js:141
        /// <summary>
        /// swatch(angle, size = 22): a size × size span with a 2px --reach-red border (radius 3, overflow hidden) holding
        /// hatch(size, size, angle, 6, --reach-red, 2) at its padding box, clipped to the padding box ((size − 4)², inner
        /// radius 1). At (0, 0) of parent; flex: none.
        /// </summary>
        public static OcbElement Swatch(RectTransform parent, float angle, float size = 22f)
        {
            if (!(size > 0f || size < 0f)) size = 22f;   // `size || 22`
            var el = Make<OcbElement>(parent, "ocb-swatch", SwatchCss(size));
            BoxStyle s = el.Box.Style;
            Insets bw = s.BorderWidths;
            // the svg (display: block) starts at the padding box and keeps its own size; overflow: hidden clips it there
            HatchGraphic hatch = Hatch(el.Root, bw.Left, bw.Top, size, size, angle, 6f, null, 2f);
            hatch.ClipRect = new RectF(0f, 0f, size - bw.Horizontal, size - bw.Vertical);
            hatch.ClipRadius = 1f;   // 3px radius − 2px border
            el.SetPart("hatch", hatch);
            NoShrink(el.Root);
            return el;
        }
    }
}
