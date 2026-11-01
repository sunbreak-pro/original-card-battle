// The exploration scene's backdrop (port of src/scenes/explore/backdrop.js; explore.css:14-23; explore.md §5.2): the
// layer's sky, haze and drifting motes, crossfaded over 400 ms when the layer changes, a veil of the ground colour over
// it, the interlude's warm lull, and the miasma shade closing in from the corners; plus skyStrip, the interlude's look at
// the next layer. Every value and motion is the ported bridge's (BackdropSpec / BackdropMotion / BackdropSwitch /
// ExplorationBackdropSpec / MiasmaLook); this file only lays them out. Owned by EX-LEAF.
//
// How the two canvases of the JS map onto uGUI:
// - The place canvas (960 × 540, scaled up 2×) is redrawn every frame. Here each layer is a "slot": a full-stage element
//   holding the sky, the hazes and the motes as vertex-coloured meshes built once. A frame only writes what the canvas
//   frame changes: the slot's alpha (canvas globalAlpha: a CanvasGroup, which like globalAlpha multiplies every draw on
//   its own), each haze's breath (BackdropMotion.FlickerAlpha: the haze graphic's opacity, since every stop of a haze is
//   scaled by the same `lit`), and each mote's place and alpha. Sizes are the JS's canvas sizes divided by RES (0.5), so
//   in stage px: radial radius Size·H/2, shafts Size·W wide and 1.6·H tall, motes SizePx × 2.2 across.
// - The shade canvas (240 × 135, scaled to the stage) is a Texture2D of the same size on a RawImage, filtered bilinear,
//   redrawn only when (Strength, InnerRadius) changes, as the JS does.
// - The veil and the lull are the explore.css rules, with their CSS transitions run by the kit's CssStates.
// The meshes: gradients along one axis are split at their stops (exact), radial ones are fans of 128 segments whose
// colour runs linearly along each radius (the canvas gradient's), so no mesh refinement is needed for the large hazes.
using System;
using System.Collections.Generic;
using System.Globalization;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;
using UnityEngine.UI;
using BdRgba = global::Backdrop.Rgba;
using BackdropMotion = global::Backdrop.BackdropMotion;
using BackdropSpec = global::Backdrop.BackdropSpec;
using BackdropSwitch = global::Backdrop.BackdropSwitch;
using ExplorationBackdropSpec = global::Backdrop.ExplorationBackdropSpec;
using Haze = global::Backdrop.Haze;
using HazeShape = global::Backdrop.HazeShape;
using LayerBackdrop = global::Backdrop.LayerBackdrop;
using MiasmaLook = global::Backdrop.MiasmaLook;
using MiasmaShade = global::Backdrop.MiasmaShade;
using Motes = global::Backdrop.Motes;
using SkyStop = global::Backdrop.SkyStop;

namespace Trial.Explore
{
    public sealed class ExploreBackdrop
    {
        // js: scenes/explore/backdrop.js:13
        private const float W = 1920f;
        private const float H = 1080f;

        /// <summary>
        /// js: scenes/explore/backdrop.js:16 RES: the JS draws the place at half resolution and scales it up. The meshes
        /// here are resolution-free, so every canvas size below is given in stage px (canvas px ÷ RES).
        /// </summary>
        public const float Res = 0.5f;

        /// <summary>js: scenes/explore/backdrop.js:18 SHAFT_SPACING (Unity BackdropLayers.ShaftSpacing): shafts of one haze stand 0.18 of the width apart.</summary>
        public const float ShaftSpacing = 0.18f;

        /// <summary>js: scenes/explore/backdrop.js:19 SHADE_W / SHADE_H.</summary>
        public const int ShadeW = 240;
        public const int ShadeH = 135;

        /// <summary>js: scenes/explore/backdrop.js:136: a mote is SizePx × 2.2 stage px across here (the battle's are SizePx).</summary>
        public const float MoteScale = 2.2f;

        /// <summary>Segments of a radial fan (the colour error at a chord's middle is 1 − cos(π/128) ≈ 0.03 % of the ramp).</summary>
        private const int Segments = 128;

        // ---- explore.css, verbatim ------------------------------------------------------------------------------------

        /// <summary>explore.css:16 .ocbg-explore .ocbg-explore-bd (the place canvas).</summary>
        public const string BdCss = "position: absolute; left: 0; top: 0; width: 1920px; height: 1080px;";

        /// <summary>explore.css:17 .ocbg-explore .ocbg-explore-veil (its colour is set inline by show()).</summary>
        public const string VeilCss = "position: absolute; inset: 0; transition: background-color 400ms ease-in-out;";

        /// <summary>explore.css:18 .ocbg-explore .ocbg-explore-shade.</summary>
        public const string ShadeCss = "position: absolute; left: 0; top: 0; width: 1920px; height: 1080px;";

        /// <summary>explore.css:19-22 .ocbg-explore .ocbg-explore-lull.</summary>
        public const string LullCss =
            "position: absolute; inset: 0; opacity: 0; transition: opacity 900ms ease-in-out; " +
            "background: " +
            "radial-gradient(60% 55% at 34% 88%, color-mix(in srgb, var(--amber) 14%, transparent), transparent 70%), " +
            "linear-gradient(color-mix(in srgb, var(--ground) 62%, transparent), color-mix(in srgb, var(--ground) 40%, transparent));";

        /// <summary>explore.css:23 .ocbg-explore .ocbg-explore-lull.is-on.</summary>
        public const string LullOnCss = "opacity: 1;";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---- colours --------------------------------------------------------------------------------------------------

        // js: scenes/explore/backdrop.js:23
        /// <summary>rgbaCss(c, mul): a bridge Rgba (0〜1 channels) as a CSS colour, its alpha scaled by mul (clamped, 4 places).</summary>
        public static string RgbaCss(BdRgba c, float mul = 1f)
        {
            double a = Math.Max(0.0, Math.Min(1.0, (double)c.A * mul));
            return "rgba(" + Round255(c.R).ToString(Inv) + "," + Round255(c.G).ToString(Inv) + "," + Round255(c.B).ToString(Inv) + "," +
                   a.ToString("F4", Inv) + ")";
        }

        /// <summary>The colour RgbaCss(c, mul) names, as a kit Rgba (the bytes rounded and the alpha to 4 places, as the CSS string is).</summary>
        public static Rgba RgbaOf(BdRgba c, float mul = 1f)
        {
            double a = Math.Max(0.0, Math.Min(1.0, (double)c.A * mul));
            return new Rgba(Round255(c.R) / 255f, Round255(c.G) / 255f, Round255(c.B) / 255f,
                (float)Math.Round(a, 4, MidpointRounding.AwayFromZero));
        }

        /// <summary>Math.round(v * 255) (half up, as the JS).</summary>
        private static int Round255(float v) => (int)Math.Floor(v * 255.0 + 0.5);

        // ---- the elements -----------------------------------------------------------------------------------------------

        /// <summary>One layer's place (the JS's slot(layer): its spec and, here, the graphics that draw it).</summary>
        private sealed class Slot
        {
            public LayerBackdrop Spec;
            public RectTransform Root;
            public CanvasGroup Group;
            public float Alpha = -1f;
            public readonly List<HazeParts> Hazes = new List<HazeParts>();
            public readonly List<RectTransform> Dots = new List<RectTransform>();
            public readonly List<VectorGraphic> DotGraphics = new List<VectorGraphic>();
            public float DotSize;
        }

        private sealed class HazeParts
        {
            public Haze Haze;
            public readonly List<VectorGraphic> Parts = new List<VectorGraphic>();
            public float Lit = -1f;
        }

        private RectTransform _host;
        private RectTransform _canvas, _veil, _lull, _shade;
        private CssStates _veilStates, _lullStates;
        private RawImage _shadeImage;
        private Texture2D _shadeTex;
        private bool _shadeLinear;
        private readonly Color32[] _shadePx = new Color32[ShadeW * ShadeH];
        private readonly float[] _dist = new float[ShadeW * ShadeH];

        private readonly BackdropSwitch _sw = new BackdropSwitch();
        private Slot _shown;
        private Slot _leaving;
        private int _miasmaTarget;
        private float _miasmaFrom;
        private float _miasmaAt;
        private string _lastShadeKey = "";
        private bool _destroyed;

        private ExploreBackdrop() { }

        // js: scenes/explore/backdrop.js:44
        /// <summary>
        /// createBackdrop(host): the place, the veil, the lull and the shade, in that order, into host (a full-stage
        /// element; the backdrop fills it from the bottom of the scene's stack).
        /// </summary>
        public static ExploreBackdrop Create(RectTransform host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            var b = new ExploreBackdrop { _host = host };
            b.Build();
            return b;
        }

        // js: scenes/explore/backdrop.js:45-58
        private void Build()
        {
            // the place canvas: the layers' slots go in here, the newest on top
            _canvas = Ui.Div(_host, "ocbg-explore-bd", 0f, 0f, W, H);

            // the veil: background-color set by show(), with its 400 ms transition
            _veil = Ui.Div(_host, "ocbg-explore-veil", 0f, 0f, W, H);
            BoxGraphic.On(_veil);
            _veilStates = CssStates.Of(_veil).Base(VeilCss);
            _veilStates.Apply(instant: true);

            // the lull: its two background layers (the first on top), faded by .is-on over 900 ms
            _lull = Ui.Div(_host, "ocbg-explore-lull", 0f, 0f, W, H);
            BuildBackgrounds(_lull, LullCss, W, H);
            _lullStates = CssStates.Of(_lull).Base(LullCss).State(".is-on", LullOnCss);
            _lullStates.Apply(instant: true);

            // the shade canvas, and the distance of every shade pixel from the centre (MiasmaLook.CornerDistance)
            _shade = Ui.Div(_host, "ocbg-explore-shade", 0f, 0f, W, H);
            _shadeImage = _shade.gameObject.AddComponent<RawImage>();
            _shadeImage.raycastTarget = false;
            _shadeImage.color = Materials.VertexColor(Rgba.White);
            _shadeImage.enabled = false;
            for (int y = 0; y < ShadeH; y++)
            {
                for (int x = 0; x < ShadeW; x++)
                    _dist[TexIndex(x, y)] = MiasmaLook.CornerDistance((x + 0.5f) / ShadeW, (y + 0.5f) / ShadeH, (float)(1920.0 / 1080.0));
            }
            MakeShadeTexture();
            Materials.GammaEncodingChanged += OnEncodingChanged;
        }

        /// <summary>Pixel (x, y) of the canvas (y down) in the texture's rows (row 0 at the bottom).</summary>
        private static int TexIndex(int x, int yDown) => (ShadeH - 1 - yDown) * ShadeW + x;

        // js: scenes/explore/backdrop.js:68
        /// <summary>slot(layer): the layer's place, built under the canvas (on top of the slots there), shown at 0 until a frame draws it.</summary>
        private Slot MakeSlot(int layer)
        {
            LayerBackdrop spec = BackdropSpec.For(layer);
            var s = new Slot { Spec = spec };
            s.Root = Ui.Div(_canvas, "slot " + layer.ToString(Inv), 0f, 0f, W, H);
            s.Group = s.Root.gameObject.AddComponent<CanvasGroup>();
            s.Group.blocksRaycasts = false;
            s.Group.interactable = false;
            s.Group.alpha = 0f;
            s.Alpha = 0f;

            // sky
            AddSky(s.Root, spec, W, H);

            // haze (each graphic is built at the top of its breath; the breath is its opacity)
            foreach (Haze haze in spec.Hazes ?? Array.Empty<Haze>())
            {
                var hp = new HazeParts { Haze = haze };
                if (haze.Shape == HazeShape.TopGlow || haze.Shape == HazeShape.Band)
                {
                    hp.Parts.Add(VerticalHaze(s.Root, haze, W, H, 20));
                }
                else if (haze.Shape == HazeShape.Radial)
                {
                    float r = haze.Size * H / 2f;
                    hp.Parts.Add(RadialDot(s.Root, haze.X * W, haze.Y * H, r, RgbaOf(haze.Color), RgbaOf(haze.Color, 0f), "haze radial"));
                }
                else if (haze.Shape == HazeShape.Shafts)
                {
                    float width = haze.Size * W;
                    float spacing = W * ShaftSpacing;
                    float tall = H * 1.6f;
                    for (int i = 0; i < haze.Count; i++)
                    {
                        float x = haze.X * W + (i - (haze.Count - 1) / 2f) * spacing;
                        hp.Parts.Add(Shaft(s.Root, x, H / 2f, width, tall, haze.Angle,
                            new[] { 0f, 0.25f, 0.75f, 1f },
                            new[] { RgbaOf(haze.Color, 0f), RgbaOf(haze.Color), RgbaOf(haze.Color), RgbaOf(haze.Color, 0f) }));
                    }
                }
                s.Hazes.Add(hp);
            }

            // motes (BackdropMotion.MoteAt / MoteAlpha), SizePx × 2.2 stage px across
            Motes motes = spec.Motes ?? Motes.None;
            s.DotSize = motes.SizePx * MoteScale;
            for (int i = 0; i < motes.Count; i++)
            {
                VectorGraphic dot = SoftDot(s.Root, motes.Color, s.DotSize);
                dot.Opacity = 0f;
                s.Dots.Add(dot.rectTransform);
                s.DotGraphics.Add(dot);
            }
            return s;
        }

        private static void DropSlot(Slot s)
        {
            if (s != null && s.Root) Ui.Remove(s.Root);
        }

        // js: scenes/explore/backdrop.js:73
        /// <summary>
        /// show(layer, now): turns the place to a layer (BackdropSwitch: the first is drawn at once, a later one fades in over
        /// 400 ms, a third one mid-fade drops the oldest) and sets the veil's colour (its own 400 ms transition). False when
        /// the layer is already shown.
        /// </summary>
        public bool Show(int layer, float now)
        {
            if (_destroyed) return false;
            BackdropSwitch.Change change = _sw.Show(layer, now);
            if (change == BackdropSwitch.Change.None) return false;
            if (change == BackdropSwitch.Change.FadeDroppingOldest)
            {
                DropSlot(_leaving);
                _leaving = null;
            }
            // a leaving slot whose fade ended before a frame dropped it goes now (the JS overwrites it)
            if (_leaving != null) DropSlot(_leaving);
            _leaving = _shown;
            _shown = MakeSlot(_sw.Shown);
            // The veil follows the layer (ExplorationBackdropSpec.For(layer).Veil) with the same 400 ms crossfade.
            _veilStates.Base(VeilCss + " background-color: " + RgbaCss(ExplorationBackdropSpec.For(_sw.Shown).Veil) + ";");
            return true;
        }

        /// <summary>get layer(): the layer shown (sw.Shown; 0 before the first).</summary>
        public int Layer => _sw.Shown;

        // js: scenes/explore/backdrop.js:84
        /// <summary>drawSlot(s, alpha, now): the slot at globalAlpha `alpha`; each haze at its breath; the motes where they are now.</summary>
        private static void DrawSlot(Slot s, float alpha, float now)
        {
            if (!s.Root) return;
            if (s.Alpha != alpha)
            {
                s.Alpha = alpha;
                s.Group.alpha = Mathf.Clamp01(alpha);
            }
            // haze, each at its breath (BackdropMotion.FlickerAlpha)
            foreach (HazeParts hp in s.Hazes)
            {
                float lit = BackdropMotion.FlickerAlpha(hp.Haze.Flicker, now);
                if (lit == hp.Lit) continue;
                hp.Lit = lit;
                foreach (VectorGraphic g in hp.Parts) g.Opacity = lit;
            }
            // motes (BackdropMotion.MoteAt / MoteAlpha), in reference pixels
            Motes motes = s.Spec.Motes ?? Motes.None;
            float half = s.DotSize / 2f;
            for (int i = 0; i < motes.Count && i < s.Dots.Count; i++)
            {
                BackdropMotion.MoteAt(motes, i, now, W, H, out float x, out float y);
                s.Dots[i].anchoredPosition = new Vector2(x - half, -(y - half));
                s.DotGraphics[i].Opacity = motes.Color.A * BackdropMotion.MoteAlpha(motes, i, now);
            }
        }

        // js: scenes/explore/backdrop.js:145
        /// <summary>drawShade(percent, now): the shade at the reading, redrawn only when its strength or inner edge moved.</summary>
        private void DrawShade(float percent, float now)
        {
            MiasmaShade sh = MiasmaLook.ShadeAt(percent, now);
            string key = sh.Strength.ToString("F4", Inv) + "/" + sh.InnerRadius.ToString("F4", Inv);
            if (key == _lastShadeKey) return;
            _lastShadeKey = key;
            if (!_shadeTex) return;
            if (sh.Strength <= 0f)
            {
                Array.Clear(_shadePx, 0, _shadePx.Length);
                _shadeTex.SetPixels32(_shadePx);
                _shadeTex.Apply(false, false);
                _shadeImage.enabled = false;
                return;
            }
            BdRgba sc = MiasmaLook.ShadeColor;
            BdRgba se = MiasmaLook.SeepColor;
            for (int i = 0; i < _dist.Length; i++)
            {
                double a1 = MiasmaLook.ShadeAlphaAt(sh, _dist[i]);
                double a2 = sh.SeepAlpha * (double)MiasmaLook.EdgeFalloff(sh, _dist[i]);
                double o = a2 + a1 * (1.0 - a2);
                if (o <= 0.0)
                {
                    _shadePx[i] = new Color32(0, 0, 0, 0);
                    continue;
                }
                _shadePx[i] = new Color32(
                    Byte((se.R * a2 + sc.R * a1 * (1.0 - a2)) / o),
                    Byte((se.G * a2 + sc.G * a1 * (1.0 - a2)) / o),
                    Byte((se.B * a2 + sc.B * a1 * (1.0 - a2)) / o),
                    Byte(o));
            }
            _shadeTex.SetPixels32(_shadePx);
            _shadeTex.Apply(false, false);
            _shadeImage.enabled = true;
        }

        /// <summary>Math.round(v * 255) into a Uint8ClampedArray.</summary>
        private static byte Byte(double v)
        {
            double r = Math.Floor(v * 255.0 + 0.5);
            return (byte)(r < 0 ? 0 : r > 255 ? 255 : r);
        }

        // js: scenes/explore/backdrop.js:175
        /// <summary>setMiasma(percent, now, instant): the gauge's reading the shade follows; it eases there with MiasmaLook.Rise like the gauge.</summary>
        public void SetMiasma(int percent, float now, bool instant = false)
        {
            if (percent == _miasmaTarget && !instant) return;
            _miasmaFrom = instant ? percent : MiasmaNow(now);
            _miasmaTarget = percent;
            _miasmaAt = now;
        }

        // js: scenes/explore/backdrop.js:181
        /// <summary>
        /// miasmaNow(now): MiasmaLook.Rise(miasmaFrom, miasmaTarget, now − miasmaAt) as the JS evaluates it on a start
        /// that may fall between whole numbers (a rise interrupted mid-way): the JS port of Rise (bridge/backdrop/
        /// MiasmaLook.js:184) gives `from` before the rise, `to` once RiseMs has passed, and in between
        /// from + ((to − from) | 0) × smoothstep — the span is cut to a whole number, so an interrupted rise steps the last
        /// fraction at the end, exactly as the gauge does (ExploreScene.Band GaugeRise). The C# Rise takes whole readings.
        /// </summary>
        public float MiasmaNow(float now)
        {
            double elapsedMs = (double)now - _miasmaAt;
            if (elapsedMs <= 0) return _miasmaFrom;
            if (elapsedMs >= MiasmaLook.RiseMs) return _miasmaTarget;
            double t = elapsedMs / MiasmaLook.RiseMs;
            int span = (int)((double)_miasmaTarget - _miasmaFrom);   // (to - from) | 0: truncated toward zero
            return (float)(_miasmaFrom + span * (t * t * (3.0 - 2.0 * t)));
        }

        // js: scenes/explore/backdrop.js:184
        /// <summary>setLull(on): the interlude's warm lull (lull.classList.toggle('is-on'): opacity, 900 ms ease-in-out).</summary>
        public void SetLull(bool on)
        {
            if (_destroyed || !_lullStates) return;
            _lullStates.SetState(".is-on", on);
        }

        // js: scenes/explore/backdrop.js:185
        /// <summary>frame(now): one frame of the place (the leaving layer at 1, the shown one fading in over it) and the shade.</summary>
        public void Frame(float now)
        {
            if (_destroyed || _shown == null) return;
            float a = _sw.Tick(now);
            if (_leaving != null && _sw.Leaving == 0)
            {
                DropSlot(_leaving);
                _leaving = null;
            }
            if (_leaving != null) DrawSlot(_leaving, 1f, now);
            DrawSlot(_shown, _leaving != null ? a : 1f, now);
            DrawShade(MiasmaNow(now), now);
        }

        // js: scenes/explore/backdrop.js:193
        /// <summary>destroy(): removes the canvas, the veil, the lull and the shade.</summary>
        public void Destroy()
        {
            if (_destroyed) return;
            _destroyed = true;
            Materials.GammaEncodingChanged -= OnEncodingChanged;
            if (_canvas) Ui.Remove(_canvas);
            if (_veil) Ui.Remove(_veil);
            if (_lull) Ui.Remove(_lull);
            if (_shade) Ui.Remove(_shade);
            Kill(_shadeTex);
            _shadeTex = null;
            _shown = null;
            _leaving = null;
        }

        // ---- the shade texture ------------------------------------------------------------------------------------------

        // A colour texture under the gamma target is read without the sRGB decode (Materials.ColorTexturesLinear).
        private void MakeShadeTexture()
        {
            Kill(_shadeTex);
            _shadeLinear = Materials.ColorTexturesLinear;
            _shadeTex = new Texture2D(ShadeW, ShadeH, TextureFormat.RGBA32, false, _shadeLinear)
            {
                name = "ocbg-explore-shade",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            Array.Clear(_shadePx, 0, _shadePx.Length);
            _shadeTex.SetPixels32(_shadePx);
            _shadeTex.Apply(false, false);
            _shadeImage.texture = _shadeTex;
            _shadeImage.enabled = false;
            _lastShadeKey = "";
        }

        private void OnEncodingChanged()
        {
            if (_destroyed || !_shadeImage)
            {
                Materials.GammaEncodingChanged -= OnEncodingChanged;
                return;
            }
            if (_shadeLinear != Materials.ColorTexturesLinear) MakeShadeTexture();
        }

        private static void Kill(UnityEngine.Object o)
        {
            if (!o) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        // js: scenes/explore/backdrop.js:198
        /// <summary>
        /// skyStrip(layer, w, ht): a small strip of a layer's sky and haze (the interlude's look ahead at the next layer), as
        /// a w × ht element at (0, 0) of parent, clipped to itself as the canvas is. No motes and no breath; radial hazes are
        /// Size·ht/2 across, shafts max(4, Size·w) wide and 2·ht tall with a 0 → full → 0 ramp, the top glow and band in 11
        /// stops; then the layer's veil at 0.6× its alpha. The window's drift (explore.css:359 ocbg-explore-window) is the
        /// caller's, on the element returned.
        /// </summary>
        public static RectTransform SkyStrip(RectTransform parent, int layer, float w, float ht)
        {
            RectTransform c = Ui.Div(parent, "ocbg-explore-strip", 0f, 0f, w, ht);
            c.gameObject.AddComponent<RectMask2D>();
            LayerBackdrop spec = BackdropSpec.For(layer);
            AddSky(c, spec, w, ht);
            foreach (Haze haze in spec.Hazes ?? Array.Empty<Haze>())
            {
                if (haze.Shape == HazeShape.Radial)
                {
                    float r = haze.Size * ht / 2f;
                    RadialDot(c, haze.X * w, haze.Y * ht, r, RgbaOf(haze.Color), RgbaOf(haze.Color, 0f), "haze radial");
                }
                else if (haze.Shape == HazeShape.Shafts)
                {
                    for (int i = 0; i < haze.Count; i++)
                    {
                        float x = haze.X * w + (i - (haze.Count - 1) / 2f) * w * ShaftSpacing;
                        float sw = Math.Max(4f, haze.Size * w);
                        Shaft(c, x, ht / 2f, sw, ht * 2f, haze.Angle,
                            new[] { 0f, 0.5f, 1f },
                            new[] { RgbaOf(haze.Color, 0f), RgbaOf(haze.Color), RgbaOf(haze.Color, 0f) });
                    }
                }
                else
                {
                    VerticalHaze(c, haze, w, ht, 10);
                }
            }
            // the same ground veil the map would have, so the strip reads as "that place, seen from here"
            Rgba veil = RgbaOf(ExplorationBackdropSpec.For(layer).Veil, 0.6f);
            VectorGraphic.Draw(c, 0f, 0f, w, ht, (mb, box, f) => Bands(mb, box, true, new[] { 0f }, new[] { veil }), "veil");
            return c;
        }

        // ---- the pieces -------------------------------------------------------------------------------------------------

        /// <summary>The sky: a vertical gradient of the layer's stops (each At held to 0〜1) over w × ht.</summary>
        private static VectorGraphic AddSky(RectTransform parent, LayerBackdrop spec, float w, float ht)
        {
            SkyStop[] sky = spec.Sky ?? Array.Empty<SkyStop>();
            var ts = new float[sky.Length];
            var cols = new Rgba[sky.Length];
            for (int i = 0; i < sky.Length; i++)
            {
                ts[i] = Math.Max(0f, Math.Min(1f, sky[i].At));
                cols[i] = RgbaOf(sky[i].Color);
            }
            return VectorGraphic.Draw(parent, 0f, 0f, w, ht, (mb, box, f) => Bands(mb, box, true, ts, cols), "sky");
        }

        /// <summary>A top glow or a band: a vertical gradient of `steps` + 1 stops of BackdropSpec.HazeAlphaAt on the haze's colour at alpha 1.</summary>
        private static VectorGraphic VerticalHaze(RectTransform parent, Haze haze, float w, float ht, int steps)
        {
            BdRgba b = haze.Color.WithAlpha(1f);
            var ts = new float[steps + 1];
            var cols = new Rgba[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float y = i / (float)steps;
                ts[i] = y;
                cols[i] = RgbaOf(b, BackdropSpec.HazeAlphaAt(haze, y));
            }
            return VectorGraphic.Draw(parent, 0f, 0f, w, ht, (mb, box, f) => Bands(mb, box, true, ts, cols),
                haze.Shape == HazeShape.Band ? "haze band" : "haze topglow");
        }

        /// <summary>A radial glow of radius r centred at (cx, cy): `inner` at the centre to `outer` at r (createRadialGradient(c, 0, c, r)).</summary>
        private static VectorGraphic RadialDot(RectTransform parent, float cx, float cy, float r, Rgba inner, Rgba outer, string name)
        {
            var ts = new[] { 0f, 1f };
            var cols = new[] { inner, outer };
            return VectorGraphic.Draw(parent, cx - r, cy - r, r * 2f, r * 2f,
                (mb, box, f) => Rings(mb, box.Center, box.W / 2f, box.H / 2f, inner, ts, cols, Segments), name);
        }

        // js: scenes/explore/backdrop.js:28
        /// <summary>
        /// softDot(color): a speck in the colour at full strength fading to nothing at its rim (the JS stamps a 32 × 32
        /// canvas of radialGradient(16, 16, 0, 16) with rgbaCss(color, 1 / max(.001, color.A)) → rgbaCss(color, 0)), d across.
        /// </summary>
        private static VectorGraphic SoftDot(RectTransform parent, BdRgba color, float d)
        {
            Rgba inner = RgbaOf(color, 1f / Math.Max(0.001f, color.A));
            Rgba outer = RgbaOf(color, 0f);
            var ts = new[] { 0f, 1f };
            var cols = new[] { inner, outer };
            return VectorGraphic.Draw(parent, 0f, 0f, d, d,
                (mb, box, f) => Rings(mb, box.Center, box.W / 2f, box.H / 2f, inner, ts, cols, 24), "mote");
        }

        /// <summary>
        /// A shaft: a width × tall rect centred on (cx, cy), turned `angle` degrees clockwise about its centre (the canvas's
        /// translate + rotate), with a horizontal gradient across it.
        /// </summary>
        private static VectorGraphic Shaft(RectTransform parent, float cx, float cy, float width, float tall, float angle, float[] ts, Rgba[] cols)
        {
            VectorGraphic g = VectorGraphic.Draw(parent, cx - width / 2f, cy - tall / 2f, width, tall,
                (mb, box, f) => Bands(mb, box, false, ts, cols), "haze shaft");
            UiNode.Of(g.rectTransform).Rotate = angle;
            return g;
        }

        /// <summary>
        /// The CSS backgrounds of a declaration block as graphics under el, the bottom layer first: a radial gradient as a
        /// fan of rings (its stops, plus 8 rings where a premultiplied ramp bends), any other image through GradientGraphic.
        /// </summary>
        private static void BuildBackgrounds(RectTransform el, string css, float w, float h)
        {
            List<BackgroundLayer> layers = global::Trial.Geometry.Css.Box(css).Backgrounds;
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                BackgroundImage image = layers[i].Image;
                if (image == null) continue;
                if (image is RadialGradientSpec radial)
                {
                    VectorGraphic.Draw(el, 0f, 0f, w, h, (mb, box, f) =>
                    {
                        if (radial.Resolve(box) is RadialGradient rg) RadialPaint(mb, box, rg);
                    }, "background radial");
                }
                else
                {
                    GradientGraphic g = GradientGraphic.Make(el, 0f, 0f, w, h, "background linear");
                    g.SetPaint(box => image.Resolve(box));
                    g.Fringe = false;
                }
            }
        }

        /// <summary>A kit RadialGradient (CSS: premultiplied, its stops resolved) as rings; the last colour, when it shows, reaches the box's far corner.</summary>
        private static void RadialPaint(MeshBuilder mb, RectF box, RadialGradient g)
        {
            IReadOnlyList<float> T = g.StopPositions;
            IReadOnlyList<Rgba> C = g.StopColors;
            if (T == null || T.Count == 0) return;
            var ts = new List<float>();
            var cols = new List<Rgba>();
            for (int i = 0; i < T.Count; i++)
            {
                if (i > 0 && g.Premultiplied && Bends(C[i - 1], C[i]) && T[i] > T[i - 1])
                {
                    for (int k = 1; k < 8; k++)
                    {
                        float u = T[i - 1] + (T[i] - T[i - 1]) * k / 8f;
                        ts.Add(u);
                        cols.Add(g.ColorAtT(u));
                    }
                }
                ts.Add(T[i]);
                cols.Add(C[i]);
            }
            Rgba last = C[C.Count - 1];
            if (last.A > 0f)
            {
                float far = 0f;
                foreach (Vec2 q in new[] { new Vec2(box.X, box.Y), new Vec2(box.Right, box.Y), new Vec2(box.Right, box.Bottom), new Vec2(box.X, box.Bottom) })
                {
                    float dx = g.Rx > 0f ? (q.X - g.Center.X) / g.Rx : 0f, dy = g.Ry > 0f ? (q.Y - g.Center.Y) / g.Ry : 0f;
                    far = Math.Max(far, (float)Math.Sqrt(dx * dx + dy * dy));
                }
                far *= 1.01f;
                if (far > ts[ts.Count - 1])
                {
                    ts.Add(far);
                    cols.Add(last);
                }
            }
            Rings(mb, g.Center, g.Rx, g.Ry, g.ColorAtT(0f), ts, cols, Segments);
        }

        /// <summary>A premultiplied segment whose straight colours do not interpolate linearly (both alpha and RGB differ).</summary>
        private static bool Bends(Rgba a, Rgba b)
        {
            if (Math.Abs(a.A - b.A) <= 1f / 255f) return false;
            return Math.Abs(a.R - b.R) > 1f / 255f || Math.Abs(a.G - b.G) > 1f / 255f || Math.Abs(a.B - b.B) > 1f / 255f;
        }

        // ---- meshes -----------------------------------------------------------------------------------------------------

        /// <summary>
        /// A gradient along one axis of the box, split at its stops (t of 0〜1, ascending; equal ones make a hard stop): the
        /// first colour before the first stop and the last after the last, as a canvas gradient pads.
        /// </summary>
        private static void Bands(MeshBuilder mb, RectF box, bool vertical, IReadOnlyList<float> ts, IReadOnlyList<Rgba> cols)
        {
            int n = Math.Min(ts.Count, cols.Count);
            if (n == 0 || box.W <= 0f || box.H <= 0f) return;
            var at = new List<float>(n + 2);
            var col = new List<Rgba>(n + 2);
            float first = Math.Max(0f, Math.Min(1f, ts[0]));
            float lastT = Math.Max(0f, Math.Min(1f, ts[n - 1]));
            if (first > 0f)
            {
                at.Add(0f);
                col.Add(cols[0]);
            }
            for (int i = 0; i < n; i++)
            {
                at.Add(Math.Max(0f, Math.Min(1f, ts[i])));
                col.Add(cols[i]);
            }
            if (lastT < 1f || n == 1)
            {
                at.Add(1f);
                col.Add(cols[n - 1]);
            }
            float len = vertical ? box.H : box.W;
            int prevA = -1, prevB = -1;
            float prevPos = 0f;
            for (int i = 0; i < at.Count; i++)
            {
                float p = at[i] * len;
                Vec2 a = vertical ? new Vec2(box.X, box.Y + p) : new Vec2(box.X + p, box.Y);
                Vec2 b = vertical ? new Vec2(box.Right, box.Y + p) : new Vec2(box.X + p, box.Bottom);
                int ia = mb.AddVertex(a, col[i]);
                int ib = mb.AddVertex(b, col[i]);
                if (prevA >= 0 && p > prevPos)
                {
                    mb.AddTri(prevA, prevB, ib);
                    mb.AddTri(prevA, ib, ia);
                }
                prevA = ia;
                prevB = ib;
                prevPos = p;
            }
        }

        /// <summary>
        /// Concentric elliptic rings around c (radii rx·t, ry·t for each stop t &gt; 0, ascending), the colour of each stop on
        /// its ring and `centre` at c: a fan of n segments whose colour runs linearly along every radius.
        /// </summary>
        private static void Rings(MeshBuilder mb, Vec2 c, float rx, float ry, Rgba centre, IReadOnlyList<float> ts, IReadOnlyList<Rgba> cols, int n)
        {
            int count = Math.Min(ts.Count, cols.Count);
            if (count == 0 || !(rx > 0f) || !(ry > 0f) || n < 3) return;
            int mid = mb.AddVertex(c, centre);
            int prev = -1;
            for (int k = 0; k < count; k++)
            {
                float t = ts[k];
                if (!(t > 0f)) continue;
                int start = mb.VertexCount;
                for (int j = 0; j < n; j++)
                {
                    double ang = 2.0 * Math.PI * j / n;
                    mb.AddVertex(new Vec2(c.X + (float)(Math.Cos(ang) * rx * t), c.Y + (float)(Math.Sin(ang) * ry * t)), cols[k]);
                }
                for (int j = 0; j < n; j++)
                {
                    int j2 = (j + 1) % n;
                    if (prev < 0)
                    {
                        mb.AddTri(mid, start + j, start + j2);
                    }
                    else
                    {
                        mb.AddTri(prev + j, prev + j2, start + j2);
                        mb.AddTri(prev + j, start + j2, start + j);
                    }
                }
                prev = start;
            }
        }
    }
}
