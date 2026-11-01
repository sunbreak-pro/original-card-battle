// The colour rules of the Trial canvas and the few shared materials the kit needs.
//
// The project renders in Linear colour space, the browser blends in sRGB. The stage therefore draws the Trial canvas
// into a gamma render target (GammaTarget): the canvas sends vertex colours as they are (Canvas.vertexColorAlwaysGammaSpace)
// and the target stores what the shaders write without an sRGB conversion, so every blend happens on sRGB values as in
// CSS. The stock UI and TMP shaders still convert the vertex colour with UnityUI.cginc's UIGammaToLinear before they
// write it; that conversion cannot be switched off per material (the canvas sets _UIVertexColorAlwaysGammaSpace per draw,
// measured in the colour-space spike). So a colour handed to a UI/TMP vertex goes through Vertex(): the 8-bit code whose
// UIGammaToLinear is nearest to the wanted sRGB value. Measured on the real frame loop: every probe within 1/255 of the
// browser's value (pure white comes out 254: UIGammaToLinear(1) = 0.99586).
//
// When the stage runs without the gamma target (TrialStage.UseGammaTarget = false), every helper here is the identity.
//
// Who calls what:
// - a vertex colour of a UI/TMP shader (Graphic.color, mesh colours, TMP_Text.color)   -> Vertex(rgba)
// - a TMP <color> tag inside a label's text                                           -> ColorTag(rgba)
// - a material Color property (TMP _UnderlayColor, _FaceColor, a UI material's _Color) -> Property(rgba)
// - a vertex colour of ScreenBlend (no conversion in that shader)                      -> Raw(rgba)
// - a Texture2D that holds colours                                                      -> new Texture2D(..., linear: ColorTexturesLinear)
using System;
using System.Globalization;
using Trial.Geometry;
using UnityEngine;
using UnityEngine.UI;

namespace Trial.Kit
{
    public static class Materials
    {
        /// <summary>True while the live stage draws through the gamma target, so colours are encoded for it.</summary>
        public static bool GammaEncoding { get; private set; }

        /// <summary>Raised when GammaEncoding changes (the stage switched its target): colours set earlier must be set again.</summary>
        public static event Action GammaEncodingChanged;

        internal static void SetGammaEncoding(bool on)
        {
            if (GammaEncoding == on) return;
            GammaEncoding = on;
            try { GammaEncodingChanged?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>UnityUI.cginc UIGammaToLinear: what the UI and TMP shaders do to a vertex colour channel sent in gamma space.</summary>
        public static float UiGammaToLinear(float value)
        {
            float low = 0.0849710f * value - 0.000163029f;
            float high = value * (value * (value * 0.265885f + 0.736584f) - 0.00980184f) + 0.00319697f;
            return value < 0.0725490f ? low : high;
        }

        private static float[] _decoded;

        private static float[] Decoded
        {
            get
            {
                if (_decoded != null) return _decoded;
                var t = new float[256];
                for (int k = 0; k < 256; k++) t[k] = UiGammaToLinear(k / 255f);
                _decoded = t;
                return t;
            }
        }

        /// <summary>The 8-bit vertex code whose UIGammaToLinear is nearest to the wanted channel value c (0..1, sRGB).</summary>
        public static byte EncodeChannel(float c)
        {
            if (!(c > 0f)) c = 0f;
            if (c > 1f) c = 1f;
            float[] t = Decoded;
            int lo = 0, hi = 255;
            while (lo < hi)
            {
                int m = (lo + hi) >> 1;
                if (t[m] < c) lo = m + 1; else hi = m;
            }
            if (lo > 0 && Math.Abs(t[lo - 1] - c) <= Math.Abs(t[lo] - c)) lo--;
            return (byte)lo;
        }

        private static byte Byte(float v) => (byte)Math.Floor(Math.Max(0f, Math.Min(1f, v)) * 255f + 0.5f);

        /// <summary>A vertex colour for a UI/TMP shader on the Trial canvas (Graphic.color, mesh colours, TMP_Text.color).</summary>
        public static Color32 Vertex(Rgba c)
        {
            if (!GammaEncoding) return new Color32(Byte(c.R), Byte(c.G), Byte(c.B), Byte(c.A));
            return new Color32(EncodeChannel(c.R), EncodeChannel(c.G), EncodeChannel(c.B), Byte(c.A));
        }

        /// <summary>Vertex(rgba) for a Unity colour given in sRGB (as the CSS value).</summary>
        public static Color32 Vertex(Color c) => Vertex(new Rgba(c.r, c.g, c.b, c.a));

        /// <summary>Vertex(rgba) as a float Color (for Graphic.color / TMP_Text.color).</summary>
        public static Color VertexColor(Rgba c) => Vertex(c);

        /// <summary>A material Color property (TMP _UnderlayColor / _FaceColor, a UI material's _Color): Unity converts those exactly to linear, so the gamma target gets the sRGB curve's inverse.</summary>
        public static Color Property(Rgba c)
        {
            var u = new Color(c.R, c.G, c.B, c.A);
            if (!GammaEncoding) return u;
            Color g = u.gamma;
            return new Color(g.r, g.g, g.b, c.A);
        }

        /// <summary>A vertex colour for a shader that does not convert vertex colours (ScreenBlend): the colour as it is.</summary>
        public static Color32 Raw(Rgba c) => new Color32(Byte(c.R), Byte(c.G), Byte(c.B), Byte(c.A));

        /// <summary>A TMP rich-text colour tag ("&lt;color=#rrggbbaa&gt;") with the colour encoded like Vertex().</summary>
        public static string ColorTag(Rgba c)
        {
            Color32 v = Vertex(c);
            return string.Format(CultureInfo.InvariantCulture, "<color=#{0:x2}{1:x2}{2:x2}{3:x2}>", v.r, v.g, v.b, v.a);
        }

        /// <summary>
        /// Create colour textures with `linear: ColorTexturesLinear`: under the gamma target a texture must not be
        /// decoded from sRGB when sampled (its texels are already the sRGB values the target blends).
        /// </summary>
        public static bool ColorTexturesLinear => GammaEncoding;

        /// <summary>The camera clear colour of the gamma target (Unity linearises clear colours exactly).</summary>
        public static Color ClearColor(Rgba c) => Property(c);

        /// <summary>Rgba as a Unity Color (no encoding).</summary>
        public static Color ToUnity(Rgba c) => new Color(c.R, c.G, c.B, c.A);

        // ---- shared materials --------------------------------------------------------------------------

        private static Material _screenBlend;

        /// <summary>
        /// CSS mix-blend-mode: screen (field.css:55, the stance body light): Blend One OneMinusSrcColor with the colour
        /// premultiplied by alpha, i.e. dst + a·src·(1 − dst). Builtin "Legacy Shaders/Particles/Additive (Soft)";
        /// measured 195,153,150 for crimson .7 over #808080 (browser 196,153,150). Its vertex colours take Raw(), and it
        /// ignores RectMask2D clipping. Null when the shader is not in the build (add it to Always Included Shaders).
        /// </summary>
        public static Material ScreenBlend
        {
            get
            {
                if (_screenBlend) return _screenBlend;
                Shader sh = Shader.Find("Legacy Shaders/Particles/Additive (Soft)");
                if (sh == null) return null;
                _screenBlend = new Material(sh) { name = "Trial ScreenBlend", hideFlags = HideFlags.DontSave };
                return _screenBlend;
            }
        }

        /// <summary>The default UI material (UI/Default): clip rect, softness and stencil support; vertex colours take Vertex().</summary>
        public static Material Ui => Canvas.GetDefaultCanvasMaterial();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GammaEncoding = false;
            GammaEncodingChanged = null;
            _screenBlend = null;
        }
    }
}
