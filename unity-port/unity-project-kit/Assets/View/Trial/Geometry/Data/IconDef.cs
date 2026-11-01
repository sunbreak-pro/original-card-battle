// One line icon of the design system's style (battle-visual-v1 appendix A): a path in a 24×24 viewBox, stroked
// 2.2 viewBox units with round caps and joins, or (heart, pip) filled in the colour with no stroke.
namespace Trial.Geometry
{
    public sealed class IconDef
    {
        /// <summary>The DS icon stroke width in viewBox units (bundle.js:122 `opts.width || 2.2`).</summary>
        public const float DefaultStrokeWidth = 2.2f;
        /// <summary>stroke-dasharray of a dashed icon, viewBox units (bundle.js:123 `'2.4 2.4'`), round caps on every dash.</summary>
        public const float DashOn = 2.4f, DashOff = 2.4f;

        public readonly string Name;
        /// <summary>The path data, byte-identical to the JS string.</summary>
        public readonly string D;
        public readonly RectF ViewBox;
        /// <summary>Drawn as a fill in the icon colour with stroke none (FILLED); the stroke options are ignored.</summary>
        public readonly bool Filled;
        /// <summary>Stroke width in viewBox units.</summary>
        public readonly float StrokeWidth;
        /// <summary>stroke-linecap / stroke-linejoin of the &lt;path&gt;.</summary>
        public readonly Cap LineCap;
        public readonly Join LineJoin;
        /// <summary>ICON_LABELS entry (DS icons), or null.</summary>
        public readonly string Label;

        public IconDef(string name, string d, RectF viewBox, bool filled, float strokeWidth = DefaultStrokeWidth,
            Cap lineCap = Cap.Round, Join lineJoin = Join.Round, string label = null)
        {
            Name = name; D = d; ViewBox = viewBox; Filled = filled; StrokeWidth = strokeWidth;
            LineCap = lineCap; LineJoin = lineJoin; Label = label;
        }

        public static readonly RectF Box24 = new RectF(0f, 0f, 24f, 24f);

        /// <summary>The parsed path (cached per string).</summary>
        public PathData Path => PathData.Cached(D);

        /// <summary>viewBox → a size×size box at the origin (meet, centred).</summary>
        public Affine ToBox(float width, float height) => Affine.ViewBox(ViewBox, width, height);
        public Affine ToBox(float size) => Affine.ViewBox(ViewBox, size, size);

        /// <summary>
        /// The stroke of a stroked icon in viewBox units (scale it with the ToBox transform): `width` overrides
        /// 2.2 (opts.width), `dashed` gives 2.4 2.4 with the icon's round caps on every dash. A width of 0 (or NaN)
        /// falls back like the JS `opts.width || 2.2`.
        /// </summary>
        public StrokeStyle Style(float? width = null, bool dashed = false) => new StrokeStyle
        {
            Width = width is float w && w != 0f && !float.IsNaN(w) ? w : StrokeWidth,
            Cap = LineCap,
            Join = LineJoin,
            Dash = dashed ? new[] { DashOn, DashOff } : null,
        };
    }
}
