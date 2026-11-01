// The trial's eight faces as dynamic SDF TextMeshPro font assets (kit.md §4.9, §4.13):
//   Display 400 / 700 / 800  = Shippori Mincho Regular / Bold / ExtraBold
//   Body    400 / 700        = Zen Kaku Gothic New Regular / Bold
//   Mono    400 / 600 / 700  = IBM Plex Mono Regular / SemiBold / Bold
// Each is made on first use from Resources/Fonts/<file>.ttf with CreateFontAsset(font, 64, 16, SDFAA, 2048, 2048,
// Dynamic, multi-atlas), named "<Family>-<weight>" and registered with MaterialReferenceManager so <font="Mono-600">
// tags resolve.
//
// Padding 16 at 64 pt (gradient scale 17, the SDF reaches ±0.27 em): text-shadow underlays go up to `0 1px 3px` on
// 14 px text (offset + blur ≈ 0.29 em of reach) and need the room; the TMP default 9 (0.16 em) cuts them. The cost
// is atlas space: a CJK cell is about 92 px square, ≈ 450 glyphs per 2048² page (Alpha8, 4 MB); pages are added as
// they fill (multi-atlas).
//
// Fallbacks (glyphs a face lacks): Mono → Body (nearest weight) for Japanese, Display → Body, Body → Display 700. The
// faces of a chain are made together with the face that needs them.
// Domain reload is off in the project: the cache uses Unity's null check, and SubsystemRegistration drops it and
// TMP's MaterialReferenceManager (whose lookup would otherwise keep the previous Play session's destroyed assets).
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using Trial.Geometry;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Trial.Kit
{
    public static class TrialFonts
    {
        public const int SamplingPointSize = 64;
        public const int AtlasPadding = 16;
        public const int AtlasSize = 2048;

        private static readonly Dictionary<int, TMP_FontAsset> Cache = new Dictionary<int, TMP_FontAsset>();
        private static readonly HashSet<int> Missing = new HashSet<int>();
        private static bool _chainsBuilt;

        private static int Key(Family f, int w) => (int)f * 10000 + w;

        /// <summary>The weights each family has as a file.</summary>
        public static IReadOnlyList<int> WeightsOf(Family family)
        {
            switch (family)
            {
                case Family.Display: return DisplayWeights;
                case Family.Mono: return MonoWeights;
                default: return BodyWeights;
            }
        }

        private static readonly int[] DisplayWeights = { 400, 700, 800 };
        private static readonly int[] BodyWeights = { 400, 700 };
        private static readonly int[] MonoWeights = { 400, 600, 700 };

        /// <summary>The Resources/Fonts file of a face (an available weight).</summary>
        public static string FileOf(Family family, int weight)
        {
            switch (family)
            {
                case Family.Display: return weight >= 800 ? "ShipporiMincho-ExtraBold" : weight >= 700 ? "ShipporiMincho-Bold" : "ShipporiMincho-Regular";
                case Family.Mono: return weight >= 700 ? "IBMPlexMono-Bold" : weight >= 600 ? "IBMPlexMono-SemiBold" : "IBMPlexMono-Regular";
                default: return weight >= 700 ? "ZenKakuGothicNew-Bold" : "ZenKakuGothicNew-Regular";
            }
        }

        /// <summary>
        /// CSS font matching for a weight the family lacks (CSS Fonts 4 §5.2): above 500 the next heavier, then
        /// lighter; below 400 the next lighter, then heavier; 400 tries 500 then lighter then heavier; 500 tries 400 first.
        /// </summary>
        public static int ResolveWeight(Family family, int weight)
        {
            IReadOnlyList<int> have = WeightsOf(family);
            foreach (int w in have) if (w == weight) return w;
            var up = new List<int>();
            var down = new List<int>();
            foreach (int w in have) if (w > weight) up.Add(w); else down.Add(w);
            up.Sort();
            down.Sort((a, b) => b.CompareTo(a));
            if (weight >= 400 && weight <= 500)
            {
                foreach (int w in have) if (w > weight && w <= 500) return w;
                if (down.Count > 0) return down[0];
                return up.Count > 0 ? up[0] : have[0];
            }
            if (weight > 500) return up.Count > 0 ? up[0] : down[0];
            return down.Count > 0 ? down[0] : up[0];
        }

        /// <summary>"Display-800", "Body-400", "Mono-600": the asset's name and its &lt;font="…"&gt; tag.</summary>
        public static string Tag(Family family, int weight) => family + "-" + ResolveWeight(family, weight);

        /// <summary>The face for a family and weight (the nearest available weight), or TMP's default font when the file is missing.</summary>
        public static TMP_FontAsset Get(Family family, int weight)
        {
            TMP_FontAsset a = GetOwn(family, weight);
            if (!_chainsBuilt) BuildFallbacks();
            return a ? a : TMP_Settings.defaultFontAsset;
        }

        public static TMP_FontAsset Get(FontSpec font) => Get(font.Family, font.Weight);

        private static TMP_FontAsset GetOwn(Family family, int weight)
        {
            int w = ResolveWeight(family, weight);
            int key = Key(family, w);
            if (Cache.TryGetValue(key, out TMP_FontAsset cached) && cached) return cached;
            if (Missing.Contains(key)) return null;
            string file = FileOf(family, w);
            var font = Resources.Load<Font>("Fonts/" + file);
            if (font == null)
            {
                Missing.Add(key);
                Debug.LogWarning("[TrialFonts] Resources/Fonts/" + file + ".ttf is missing (npm run fonts:fetch, then unity:sync).");
                return null;
            }
            TMP_FontAsset a = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            if (a == null)
            {
                Missing.Add(key);
                Debug.LogWarning("[TrialFonts] could not make a font asset from " + file + " (Include Font Data must be on).");
                return null;
            }
            string tag = family + "-" + w;
            a.name = tag;
            a.hashCode = TMP_TextUtilities.GetHashCode(tag);
            if (a.material)
            {
                a.material.name = tag + " Material";
                a.materialHashCode = TMP_TextUtilities.GetSimpleHashCode(a.material.name);
            }
            a.isMultiAtlasTexturesEnabled = true;
            Cache[key] = a;
            try { MaterialReferenceManager.AddFontAsset(a); }
            catch (Exception e) { Debug.LogException(e); }
            _chainsBuilt = false;
            return a;
        }

        /// <summary>Creates all eight faces now (the boot may call it while the screen is still dark).</summary>
        public static void LoadAll()
        {
            foreach (Family f in Families)
                foreach (int w in WeightsOf(f)) GetOwn(f, w);
            BuildFallbacks();
        }

        private static readonly Family[] Families = { Family.Display, Family.Body, Family.Mono };

        // The fallback faces of a face, in order (kit.md §4.9).
        private static IEnumerable<(Family Family, int Weight)> ChainOf(Family f, int w)
        {
            switch (f)
            {
                case Family.Mono:
                    yield return (Family.Body, ResolveWeight(Family.Body, w));
                    yield return (Family.Display, 700);
                    break;
                case Family.Display:
                    yield return (Family.Body, ResolveWeight(Family.Body, w));
                    break;
                default:
                    yield return (Family.Display, 700);
                    break;
            }
        }

        private static void BuildFallbacks()
        {
            // A chain's faces are made when the face that needs them is made: a Mono label drawn before any Body face
            // was asked for would otherwise show its Japanese as missing glyphs. Loop until no new face appears (a
            // fallback face has fallbacks of its own).
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (Family f in Families)
                    foreach (int w in WeightsOf(f))
                    {
                        if (!Cache.TryGetValue(Key(f, w), out TMP_FontAsset a) || !a) continue;
                        foreach (var (cf, cw) in ChainOf(f, w))
                        {
                            int ck = Key(cf, cw);
                            if (Missing.Contains(ck) || (Cache.TryGetValue(ck, out TMP_FontAsset have) && have)) continue;
                            if (GetOwn(cf, cw)) grew = true;
                        }
                    }
            }
            foreach (Family f in Families)
            {
                foreach (int w in WeightsOf(f))
                {
                    if (!Cache.TryGetValue(Key(f, w), out TMP_FontAsset a) || !a) continue;
                    var chain = new List<TMP_FontAsset>();
                    foreach (var (cf, cw) in ChainOf(f, w))
                        if (Cache.TryGetValue(Key(cf, cw), out TMP_FontAsset fb) && fb && fb != a && !chain.Contains(fb)) chain.Add(fb);
                    a.fallbackFontAssetTable = chain;
                }
            }
            // Last: GetOwn above marks the chains stale for every face it made.
            _chainsBuilt = true;
        }

        /// <summary>Rasterises the characters into every face (and its fallbacks) ahead of use: no first-use hitch.</summary>
        public static void Prewarm(string characters)
        {
            if (string.IsNullOrEmpty(characters)) return;
            LoadAll();
            foreach (TMP_FontAsset a in Cache.Values)
                if (a) a.TryAddCharacters(characters, out string _);
        }

        /// <summary>Rasterises the characters into one face; what it lacks goes to its fallbacks.</summary>
        public static void Prewarm(string characters, Family family, int weight)
        {
            if (string.IsNullOrEmpty(characters)) return;
            TMP_FontAsset a = Get(family, weight);
            if (!a) return;
            a.TryAddCharacters(characters, out string missing);
            if (string.IsNullOrEmpty(missing) || a.fallbackFontAssetTable == null) return;
            foreach (TMP_FontAsset fb in a.fallbackFontAssetTable)
            {
                if (!fb || string.IsNullOrEmpty(missing)) continue;
                fb.TryAddCharacters(missing, out string still);
                missing = still;
            }
        }

        /// <summary>Whether the TTF of a face is present.</summary>
        public static bool Available(Family family, int weight) => GetOwn(family, weight) != null;

        /// <summary>Vertical metrics of a face in em (from the asset's faceInfo; TextMath's table when the asset is missing).</summary>
        public static FontMetrics MetricsOf(Family family, int weight)
        {
            TMP_FontAsset a = GetOwn(family, weight);
            if (!a || a.faceInfo.pointSize <= 0f) return TextMath.Metrics(family, weight);
            var fi = a.faceInfo;
            float ps = fi.pointSize;
            float asc = fi.ascentLine / ps;
            float desc = -fi.descentLine / ps;
            float gap = Math.Max(0f, fi.lineHeight / ps - (asc + desc));
            FontMetrics table = TextMath.Metrics(family, weight);
            return new FontMetrics(asc, desc, gap, table.UnderlinePosEm, table.UnderlineThicknessEm);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            Missing.Clear();
            _chainsBuilt = false;
            // TMP keeps <font> lookups in a static it never clears; with domain reload off it would still hand out
            // the previous session's (destroyed) assets for our names.
            try
            {
                FieldInfo f = typeof(MaterialReferenceManager).GetField("s_Instance", BindingFlags.NonPublic | BindingFlags.Static);
                f?.SetValue(null, null);
            }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
