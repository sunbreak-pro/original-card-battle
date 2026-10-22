// Icon-style paths the scenes draw on their own (kit.md §2.2 "Other scene-owned icon paths"), strings verbatim.
using System;
using System.Collections.Generic;

namespace Trial.Geometry
{
    public static class ExtraIcons
    {
        /// <summary>留まる's icon (scenes/battle/field/freestep.js:48 STAY_PATH): 24×24, 2.2, round caps and joins, amber.</summary>
        public const string StayPath = "M12 3.5a2.6 2.6 0 110 5.2a2.6 2.6 0 010-5.2zM12 8.7v6.6M8.6 11.6l3.4-1.6 3.4 1.6M9.4 19.6l2.6-4.3 2.6 4.3M3.5 21h17";

        /// <summary>The battle's settings gear (scenes/battle/hud/chrome.js:196 GEAR_PATH), the same string as the explore gear.</summary>
        public const string GearPath = "M12 8.6a3.4 3.4 0 110 6.8a3.4 3.4 0 010-6.8zM12 2.5v3M12 18.5v3M2.5 12h3M18.5 12h3M5.3 5.3l2.1 2.1M16.6 16.6l2.1 2.1M5.3 18.7l2.1-2.1M16.6 7.4l2.1-2.1";

        // The next-recovery cut mark (scenes/battle/hand-marks.js:126-141): a 24×24 svg holding
        //   <circle cx=12 cy=12 r=8 fill=none stroke=amber stroke-width=2 stroke-dasharray="3 3">  (butt caps)
        //   <path d="M4 20L20 4" stroke=ink stroke-width=2.4 stroke-linecap=round>
        public const float CutMarkCx = 12f, CutMarkCy = 12f, CutMarkR = 8f;
        public const float CutMarkCircleWidth = 2f;
        public const float CutMarkDashOn = 3f, CutMarkDashOff = 3f;
        public const string CutMarkSlashPath = "M4 20L20 4";
        public const float CutMarkSlashWidth = 2.4f;
        /// <summary>
        /// The &lt;circle&gt; as the path SVG 2 defines for it (starts at (cx + r, cy), runs clockwise on screen in four
        /// arcs), so its dashes start where the browser's do. Derived, not a JS string.
        /// </summary>
        public const string CutMarkCirclePath = "M20 12A8 8 0 0 1 12 20A8 8 0 0 1 4 12A8 8 0 0 1 12 4A8 8 0 0 1 20 12Z";

        /// <summary>
        /// ProceduralArt.Shield's outline in a 64×72 box (scenes/battle/field/art.js:238 SHIELD_PATH). fx.js:726 fills
        /// it and strokes it 2.5 px non-scaling with round joins; its inner keyline (fx.js:728) is 1 px, miter joins,
        /// under translate(32 36) scale(0.84) translate(−32 −36). The widths stay with the fx port.
        /// </summary>
        public const string ShieldPath = "M3 2 H61 V31 C61 50 46 63 32 70 C18 63 3 50 3 31 Z";
        public static readonly RectF ShieldViewBox = new RectF(0f, 0f, 64f, 72f);

        /// <summary>
        /// A broken shield's crack in the same 64×72 box (scenes/battle/field/fx.js:55 SHIELD_CRACK), stroked 2 px
        /// non-scaling with round joins and butt caps (fx.js:747).
        /// </summary>
        public const string ShieldCrackPath = "M33 3 L29 17 L36 29 L30 43 L35 56 L32 69";

        private static readonly Dictionary<string, IconDef> ByName = new Dictionary<string, IconDef>(StringComparer.Ordinal)
        {
            ["stay"] = new IconDef("stay", StayPath, IconDef.Box24, false),
            ["gear"] = new IconDef("gear", GearPath, IconDef.Box24, false),
            ["cut-circle"] = new IconDef("cut-circle", CutMarkCirclePath, IconDef.Box24, false, CutMarkCircleWidth, Cap.Butt, Join.Miter),
            ["cut-slash"] = new IconDef("cut-slash", CutMarkSlashPath, IconDef.Box24, false, CutMarkSlashWidth, Cap.Round, Join.Miter),
            ["shield"] = new IconDef("shield", ShieldPath, ShieldViewBox, true, 0f, Cap.Butt, Join.Round),
            ["shield-crack"] = new IconDef("shield-crack", ShieldCrackPath, ShieldViewBox, false, 0f, Cap.Butt, Join.Round),
        };

        /// <summary>stay, gear, cut-circle, cut-slash, shield (filled outline), shield-crack; null when unknown.</summary>
        public static IconDef Get(string name) => name != null && ByName.TryGetValue(name, out IconDef d) ? d : null;

        public static IReadOnlyCollection<string> Names => ByName.Keys;

        /// <summary>The cut mark's dashed circle style (viewBox units).</summary>
        public static StrokeStyle CutMarkCircleStyle() =>
            new StrokeStyle(CutMarkCircleWidth, Cap.Butt, Join.Miter, new[] { CutMarkDashOn, CutMarkDashOff });

        /// <summary>The cut mark's slash style (viewBox units).</summary>
        public static StrokeStyle CutMarkSlashStyle() => new StrokeStyle(CutMarkSlashWidth, Cap.Round, Join.Miter);
    }
}
