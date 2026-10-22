// The figures and the foot light (vendor/ds/bundle.js:575-659, bundle.css:81-83). FIGURES and poly() live in
// Trial.Geometry's DsFigures (engine-free, parsed by the dotnet tests); Figure draws them through PathGraphic (the rim
// group, filled and stroked 4 units with round joins, under the fill group).
using System.Collections.Generic;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    public static partial class Ocb
    {
        /// <summary>OCB.FIGURES (bundle.js:583): swordsman, spearman, hound, serpent, warden.</summary>
        public static IReadOnlyList<FigureDef> Figures => DsFigures.Figures;

        /// <summary>FIGURES[kind] || FIGURES.swordsman.</summary>
        public static FigureDef FigureOf(string kind) => DsFigures.Get(kind);

        // js: vendor/ds/bundle.js:579
        /// <summary>poly(x1, y1, x2, y2, w): a bar of width w from (x1, y1) to (x2, y2), as a closed path.</summary>
        public static string Poly(double x1, double y1, double x2, double y2, double w) => DsFigures.Poly(x1, y1, x2, y2, w);

        // js: vendor/ds/bundle.js:641
        /// <summary>
        /// Figure(kind, { fill, rim, scaleTo }): the svg (overflow visible, display block) of the figure at (0, 0) of parent:
        /// w × h, or scaleTo tall and w·scaleTo/h wide. fill = --figure-enemy for a foe, --figure-self for self; rim =
        /// rgba(139,111,208,.35) / rgba(232,168,76,.28). An unknown kind draws the swordsman.
        /// </summary>
        public static PathGraphic Figure(RectTransform parent, string kind, float scaleTo = 0f, Rgba? fill = null, Rgba? rim = null)
        {
            PathGraphic g = PathGraphic.Figure(parent, kind, scaleTo, fill, rim);
            g.gameObject.name = "ocb-figure-svg " + FigureOf(kind).Name;
            return g;
        }

        /// <summary>The foot light's background (bundle.js:657), for a foe or self.</summary>
        public static string FootLightBackground(bool foe) =>
            "radial-gradient(closest-side, " + (foe ? "rgba(139,111,208,.35)" : "rgba(232,168,76,.55)") + ", rgba(0,0,0,0))";

        // js: vendor/ds/bundle.js:656
        /// <summary>
        /// FootLight(w, foe): .ocb-footlight w × 60 with border-radius 50% (an ellipse) filled with
        /// radial-gradient(closest-side, amber .55 / violet .35 for a foe, transparent). At (0, 0) of parent.
        /// </summary>
        public static GradientGraphic FootLight(RectTransform parent, float w, bool foe)
        {
            float h = Px(Rule(FootLightCss).Layout.Height) ?? 60f;
            return GradientGraphic.Css(parent, FootLightBackground(foe), 0f, 0f, w, h, ellipse: true, name: "ocb-footlight");
        }
    }
}
