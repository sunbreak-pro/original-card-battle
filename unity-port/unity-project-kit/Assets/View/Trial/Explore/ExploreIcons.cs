// Line icons for the exploration scene (port of src/scenes/explore/icons.js): the design system's style (24 × 24 box,
// 2.2 px stroke, round caps and joins; battle-visual-v1 appendix A). The battle set (OCB.ICONS) has no node kinds, items
// or gear, so they are drawn here. The colour always comes from a role token. The paths (EXPLORE_ICONS, verbatim) and
// ico()'s rules (FILLED, the pouch fallback) live engine-free in Trial.Geometry.ExploreIcons; this draws them with the
// kit's PathGraphic. Owned by EX-LEAF.
using System.Collections.Generic;
using DungeonCore;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Explore
{
    /// <summary>ico's options: { fill, fillOpacity, dashed, width } and { outline } (heart / pip drawn as a line).</summary>
    public sealed class IcoOpts
    {
        /// <summary>opts.outline: heart and pip are stroked like the others instead of filled.</summary>
        public bool Outline;
        /// <summary>opts.fill (null: 'none').</summary>
        public Rgba? Fill;
        /// <summary>opts.fillOpacity: multiplies the fill's alpha (only with a fill).</summary>
        public float? FillOpacity;
        /// <summary>opts.dashed: stroke-dasharray 2.4 2.4.</summary>
        public bool Dashed;
        /// <summary>opts.width: the stroke width in viewBox units (null, 0: 2.2, as `opts.width || 2.2`).</summary>
        public float? Width;

        /// <summary>`{}` (a fresh object each time: the fields are writable, so no instance is shared).</summary>
        public static IcoOpts None => new IcoOpts();
        public static IcoOpts OutlineOnly => new IcoOpts { Outline = true };
    }

    public static class ExploreIcons
    {
        // js: scenes/explore/icons.js:8
        /// <summary>EXPLORE_ICONS: name → path (byte-identical to the JS).</summary>
        public static IReadOnlyDictionary<string, string> Paths => global::Trial.Geometry.ExploreIcons.Paths;

        // js: scenes/explore/icons.js:41
        /// <summary>FILLED: the icons drawn as a fill in the colour unless opts.outline.</summary>
        public static IReadOnlyCollection<string> Filled => global::Trial.Geometry.ExploreIcons.Filled;

        // js: scenes/explore/icons.js:47
        /// <summary>
        /// ico(name, size = 24, color = currentColor, opts): an icon of EXPLORE_ICONS (an unknown name draws the pouch) as
        /// a size × size graphic at (0, 0) of parent (the svg's width / height; viewBox 0 0 24 24). heart and pip are
        /// filled in the colour (stroke none) unless opts.Outline; the others are stroked in the colour, opts.Width (2.2)
        /// wide with round caps and joins, dashed 2.4 2.4 when opts.Dashed, over opts.Fill (× opts.FillOpacity) when
        /// given. color null = currentColor of the scene root (--ink); a caller on a washi plate passes --washi-ink.
        /// </summary>
        public static PathGraphic Ico(RectTransform parent, string name, float size = 24f, Rgba? color = null, IcoOpts opts = null)
        {
            opts = opts ?? IcoOpts.None;
            IconDef def = global::Trial.Geometry.ExploreIcons.Get(name, opts.Outline);
            Rgba? fill = opts.Fill;
            if (fill.HasValue && opts.FillOpacity.HasValue) fill = fill.Value.MulAlpha(opts.FillOpacity.Value);
            PathGraphic g = PathGraphic.Icon(parent, def, size, color ?? DsTokens.Ink, fill, opts.Width, opts.Dashed);
            // class 'ocb-icon' (vendor/ds/bundle.css:8): flex: none in a flex row, as the DS icons (Ocb.Icon)
            Ocb.NoShrink(g);
            return g;
        }

        /// <summary>
        /// ico(name, size, 'var(--ink)', opts) with the colour as the JS writes it (a CSS colour: 'var(--phosphor)',
        /// '#e8a84c', 'rgba(…)'), so a call reads like the JS line it ports.
        /// </summary>
        public static PathGraphic IcoCss(RectTransform parent, string name, float size, string cssColor, IcoOpts opts = null) =>
            Ico(parent, name, size, global::Trial.Geometry.Css.Color(cssColor), opts);

        /// <summary>ico(node.Kind, …): the node kinds use their NodeKind name as the key (Battle, Elite, Boss, Rest, Survey, Event, Trace, Carving).</summary>
        public static PathGraphic Ico(RectTransform parent, NodeKind kind, float size = 24f, Rgba? color = null, IcoOpts opts = null) =>
            Ico(parent, kind.ToString(), size, color, opts);

        // js: scenes/explore/icons.js:73
        /// <summary>itemIcon(id): the icon of a consumable or tool id, falling back to the pouch.</summary>
        public static string ItemIcon(string id) => global::Trial.Geometry.ExploreIcons.ItemIcon(id);
    }
}
