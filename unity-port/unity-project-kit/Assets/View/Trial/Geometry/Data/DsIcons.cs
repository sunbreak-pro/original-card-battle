// The design system's icon table: ICONS, ICON_LABELS and FILLED (vendor/ds/bundle.js:63-110), strings verbatim.
using System;
using System.Collections.Generic;

namespace Trial.Geometry
{
    public static class DsIcons
    {
        // js: vendor/ds/bundle.js:64 ICONS (in the JS order)
        private static readonly string[,] Table =
        {
            { "sword", "M20 4l-1.5 5.5L9 19l-4-4 9.5-9.5zM6.5 13.5l4 4M3 21l3.5-3.5" },
            { "shield", "M12 3l7 3v5c0 5-3 8.5-7 10-4-1.5-7-5-7-10V6z" },
            { "heart", "M12 21s-8-5-8-11a4.5 4.5 0 018-2.8A4.5 4.5 0 0120 10c0 6-8 11-8 11z" },
            { "heal", "M12 6v12M6 12h12" },
            { "forward", "M4 12h14M13 6l6 6-6 6" },
            { "back", "M20 12H6M11 6l-6 6 6 6" },
            { "range", "M3 12h18M3 7v10M21 7v10" },
            { "infinity", "M18.2 8c5.1 0 5.1 8 0 8-5.1 0-7.1-8-12.7-8-4.6 0-4.6 8 0 8 5.6 0 7.6-8 12.7-8z" },
            { "pip", "M12 6a6 6 0 110 12a6 6 0 010-12z" },
            { "draw", "M8 3h9v14H8zM5 7v14h9" },
            { "discard", "M12 4v10M7 9l5 5 5-5M4 20h16" },
            { "exile", "M6 6l12 12M18 6L6 18" },
            { "flag", "M6 21V3M6 4h12l-3 4.5L18 13H6" },
            { "star", "M12 3l2 7 7 2-7 2-2 7-2-7-7-2 7-2z" },
            { "hourglass", "M7 3h10M7 21h10M8 3c0 5 8 6 8 9s-8 4-8 9M16 3c0 5-8 6-8 9s8 4 8 9" },
            { "stairs", "M3 20h5v-5h5v-5h5V5h3" },
            { "miasma", "M3 9c3-3 6 3 9 0s6 3 9 0M3 15c3-3 6 3 9 0s6 3 9 0" },
            { "journal", "M4 5c3-1 6-1 8 1v14c-2-2-5-2-8-1zM20 5c-3-1-6-1-8 1v14c2-2 5-2 8-1z" },
            { "person", "M12 4a3.5 3.5 0 110 7a3.5 3.5 0 010-7zM5 21c0-4 3-7 7-7s7 3 7 7" },
            { "target", "M4 20L20 4M4 12l8-8M12 20l8-8" },
            { "seal", "M9 3h6v6H9zM9 9l-3 4h12l-3-4M4 13h16v4H4zM6 21h12" },
            { "bleed", "M12 3c4 5 6 8 6 11a6 6 0 01-12 0c0-3 2-6 6-11z" },
            { "brittle", "M12 3l-2 6 4 3-3 4 2 5M5 5l4 4M19 6l-4 3" },
            { "slow", "M6 9l6 5 6-5M6 4l6 5 6-5M4 20h16" },
            { "intimidate", "M2 12s4-6 10-6 10 6 10 6-4 6-10 6S2 12 2 12zM12 9v6" },
            { "fatigue", "M3 8h15v8H3zM18 11h3v2M6 11h3v2H6z" },
            { "empower", "M6 13l6-6 6 6M6 19l6-6 6 6" },
            { "focus", "M12 4a8 8 0 110 16a8 8 0 010-16zM12 10a2 2 0 110 4a2 2 0 010-4z" },
            { "insight", "M3 12h9M8 7l5 5-5 5M17 4v16" },
            { "agile", "M4 8h9a3 3 0 10-3-3M4 16h12a3 3 0 11-3 3M4 12h6" },
            { "regen", "M12 21V11M12 11c0-4 3-7 8-7 0 5-3 7-8 7zM12 15c0-3-2-5-6-5 0 3 2 5 6 5z" },
            { "plus", "M5 12h14M12 5v14" },
            { "rest", "M5 12h14" },
            { "speed1", "M9 6l6 6-6 6" },
            { "speed2", "M6 6l6 6-6 6M12 6l6 6-6 6" },
            { "speed3", "M4 6l5 6-5 6M10 6l5 6-5 6M16 6l5 6-5 6" },
        };

        // js: vendor/ds/bundle.js:102 ICON_LABELS
        private static readonly string[,] LabelTable =
        {
            { "sword", "威力（剣）" }, { "shield", "Guard（盾）" }, { "heart", "HP（ハート）" }, { "heal", "回復" },
            { "forward", "前へ" }, { "back", "後ろへ" }, { "range", "間合い" }, { "infinity", "永続（∞）" },
            { "pip", "スタミナの粒" }, { "draw", "ドロー / 山札" }, { "discard", "捨札" }, { "exile", "除外" },
            { "flag", "スタンス（旗）" }, { "star", "スキル" }, { "hourglass", "ターン" }, { "stairs", "階層" },
            { "miasma", "瘴気" }, { "journal", "手記" }, { "person", "自分" }, { "target", "狙うマス" },
            { "seal", "状態付与（印）" }, { "bleed", "出血" }, { "brittle", "脆化" }, { "slow", "鈍足" },
            { "intimidate", "威圧" }, { "fatigue", "疲労" }, { "empower", "強化" }, { "focus", "集中" },
            { "insight", "見切り" }, { "agile", "俊敏" }, { "regen", "再生" }, { "plus", "「+n」" }, { "rest", "休み" },
            { "speed1", "速さ ×1.0" }, { "speed2", "速さ ×1.25" }, { "speed3", "速さ ×1.5" },
        };

        // js: vendor/ds/bundle.js:110 FILLED
        private static readonly HashSet<string> FilledSet = new HashSet<string>(StringComparer.Ordinal) { "heart", "pip" };

        private static readonly Dictionary<string, string> PathMap = ToMap(Table);
        private static readonly Dictionary<string, string> LabelMap = ToMap(LabelTable);
        private static readonly string[] NameList = Column(Table);

        /// <summary>The fallback of an unknown name (bundle.js:117 `ICONS[name] || ICONS.plus`).</summary>
        public const string Fallback = "plus";

        public static IReadOnlyList<string> Names => NameList;
        public static IReadOnlyDictionary<string, string> Paths => PathMap;
        public static IReadOnlyDictionary<string, string> Labels => LabelMap;
        public static IReadOnlyCollection<string> Filled => FilledSet;

        public static bool Has(string name) => name != null && PathMap.ContainsKey(name);
        public static bool IsFilled(string name) => name != null && FilledSet.Contains(name);

        /// <summary>
        /// js: vendor/ds/bundle.js:113 icon(): the path of `name` (plus when unknown) with the FILLED flag of the
        /// requested name, the 24×24 viewBox and the 2.2 stroke.
        /// </summary>
        public static IconDef Get(string name)
        {
            string d = name != null && PathMap.TryGetValue(name, out string p) ? p : PathMap[Fallback];
            string label = name != null && LabelMap.TryGetValue(name, out string l) ? l : null;
            return new IconDef(name, d, IconDef.Box24, IsFilled(name), label: label);
        }

        internal static Dictionary<string, string> ToMap(string[,] t)
        {
            var m = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < t.GetLength(0); i++) m[t[i, 0]] = t[i, 1];
            return m;
        }

        internal static string[] Column(string[,] t)
        {
            var o = new string[t.GetLength(0)];
            for (int i = 0; i < o.Length; i++) o[i] = t[i, 0];
            return o;
        }
    }
}
