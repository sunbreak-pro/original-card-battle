// CSS text-shadow with a small blur (≤ 3 px: `0 1px 2px`, `0 1px 3px`) as a TMP SDF underlay (kit.md §4.9 path 1).
// One material per (font asset, shadow, font size), shared by every label that needs it. Larger blurs (ground halos,
// glows) are TextGlow's (a graphic behind the label), not an underlay.
//
// Calibrated in the Editor (2026-10-22, Zen Kaku Gothic New 400, 64 pt sampling, padding 16, gradient scale g = 17,
// a 200 px glyph): an underlay offset u moves the shadow by u · ratioC · g atlas texels (+x right, +y up); a softness
// s spreads the edge into a linear ramp s · ratioC · 2g texels wide, centred on the glyph edge. ratioC is TMP's
// (g − 1 − range) / (g · t), t = max(1, |offset| + softness); range is 0 with the weights at 0, as set here.
// A Gaussian shadow edge (σ = blur / 2, measured) has the slope of a linear ramp √(2π)·σ wide, so that is the ramp.
// One atlas texel is samplingPointSize / fontSize px. When offset and ramp do not fit the SDF's reach (|offset| +
// ramp/2 ≤ (g − 1) texels) both are scaled down together, as TMP itself would.
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    public static class TextShadows
    {
        /// <summary>The largest blur drawn as an underlay; a larger one is a TextGlow.</summary>
        public const float MaxUnderlayBlur = 3f;

        private const float DefaultRampPerSigma = 2.5066283f;

        /// <summary>Ramp width per σ of a Gaussian edge (√(2π)); the gallery may tune it (reset each Play session).</summary>
        public static float RampPerSigma = DefaultRampPerSigma;

        private static readonly Dictionary<(TMP_FontAsset, string), Material> Cache = new Dictionary<(TMP_FontAsset, string), Material>();

        public static bool IsUnderlay(TextShadow s) => s.Blur <= MaxUnderlayBlur;

        /// <summary>The shadow drawn as the underlay: the small-blur one with the largest reach (TMP has one underlay per material).</summary>
        public static TextShadow? PickUnderlay(IReadOnlyList<TextShadow> shadows)
        {
            if (shadows == null) return null;
            TextShadow? best = null;
            float bestReach = -1f;
            foreach (TextShadow s in shadows)
            {
                if (!IsUnderlay(s) || s.Color.A <= 0f) continue;
                float reach = Math.Max(Math.Abs(s.X), Math.Abs(s.Y)) + s.Blur;
                if (reach > bestReach) { best = s; bestReach = reach; }
            }
            return best;
        }

        /// <summary>The shadows that need a TextGlow (blur above the underlay limit).</summary>
        public static List<TextShadow> GlowShadows(IReadOnlyList<TextShadow> shadows)
        {
            var list = new List<TextShadow>();
            if (shadows == null) return list;
            foreach (TextShadow s in shadows) if (!IsUnderlay(s) && s.Color.A > 0f) list.Add(s);
            return list;
        }

        /// <summary>The underlay parameters (offset x / y, softness) for a shadow at a font size, in TMP's material units.</summary>
        public static Vector3 UnderlayParams(TextShadow s, float fontSize, float samplingPointSize, float gradientScale)
        {
            if (fontSize <= 0f || gradientScale <= 1f) return Vector3.zero;
            float texPerPx = samplingPointSize / fontSize;
            float c0g = gradientScale - 1f; // ratioC · g with t = 1 and range 0
            float offX = s.X * texPerPx;
            float offY = s.Y * texPerPx;
            float ramp = RampPerSigma * s.Sigma * texPerPx;
            float ux = offX / c0g, uy = offY / c0g, us = ramp / (2f * c0g);
            float sum = Math.Max(Math.Abs(ux), Math.Abs(uy)) + us;
            if (sum > 1f) { ux /= sum; uy /= sum; us /= sum; }
            // TMP's +y is up; CSS's is down.
            return new Vector3(ux, -uy, us);
        }

        /// <summary>The material of `asset` with the shadow as its underlay at `fontSize` (cached).</summary>
        public static Material Material(TMP_FontAsset asset, TextShadow shadow, float fontSize)
        {
            if (!asset || !asset.material) return null;
            var key = (asset, shadow.ToCss() + "|" + fontSize.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                         + "|" + (Materials.GammaEncoding ? 1 : 0));
            if (Cache.TryGetValue(key, out Material m) && m) return m;
            m = new Material(asset.material) { name = asset.name + " shadow " + shadow.ToCss() + " @" + fontSize };
            float g = m.HasProperty("_GradientScale") ? m.GetFloat("_GradientScale") : asset.atlasPadding + 1f;
            float sampling = asset.faceInfo.pointSize > 0f ? asset.faceInfo.pointSize : TrialFonts.SamplingPointSize;
            Vector3 p = UnderlayParams(shadow, fontSize, sampling, g);
            m.SetFloat("_WeightNormal", 0f);
            m.SetFloat("_WeightBold", 0f);
            m.SetFloat("_FaceDilate", 0f);
            m.SetFloat("_UnderlayDilate", 0f);
            m.SetFloat("_UnderlayOffsetX", p.x);
            m.SetFloat("_UnderlayOffsetY", p.y);
            m.SetFloat("_UnderlaySoftness", p.z);
            m.SetColor("_UnderlayColor", Materials.Property(shadow.Color));
            m.EnableKeyword("UNDERLAY_ON");
            ShaderUtilities.UpdateShaderRatios(m);
            Cache[key] = m;
            return m;
        }

        /// <summary>
        /// Gives the label its underlay (or the plain face material when no shadow is small enough) and returns the
        /// shadows left for a TextGlow.
        /// </summary>
        public static List<TextShadow> Apply(TMP_Text t, IReadOnlyList<TextShadow> shadows)
        {
            List<TextShadow> rest = GlowShadows(shadows);
            if (!t) return rest;
            TMP_FontAsset asset = t.font;
            TextShadow? under = PickUnderlay(shadows);
            Material mat = under.HasValue && asset ? Material(asset, under.Value, t.fontSize) : (asset ? asset.material : null);
            if (mat && t.fontSharedMaterial != mat)
            {
                t.fontSharedMaterial = mat;
                t.UpdateMeshPadding();
            }
            // A second small shadow is dropped (one underlay per material; kit.md §4.9 keeps the larger).
            return rest;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            RampPerSigma = DefaultRampPerSigma;
        }
    }
}
