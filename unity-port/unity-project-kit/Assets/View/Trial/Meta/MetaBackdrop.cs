// A layer's backdrop for the meta screens (port of scenes/meta/backdrop.js): the sky gradient, the haze with its
// breathing light, the drifting specks, then the veil and the vignette, all × the backdrop's alpha (the hall's lamp).
// Every value and every motion comes from the ported bridge (Backdrop.Script's BackdropSpec / BackdropMotion); this
// file only paints them, laid out the way the JS canvas lays them out (shafts 18% of the width apart and 1.6 screens
// tall, a radial glow Size × height across, specks as soft dots solid to 45% of their radius, diameter
// max(4, ceil(2 × SizePx))).
//
// The JS repaints a 1920×1080 canvas every frame with globalAlpha = alpha. Here each piece is a kit graphic built once
// per spec (Use rebuilds them); Paint(ms) only sets what moves — each haze's breath as its graphic's opacity (the
// haze's colours are linear in it), each speck's place and twinkle — and the root's CanvasGroup carries the alpha
// (uGUI multiplies it into every graphic, as globalAlpha multiplies every draw). The backdrop sits in a canvas of its
// own, so the specks moving every frame rebatch only it. It takes no pointer events (.ocbg-meta-backdrop).
using System;
using System.Collections.Generic;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;
using BRgba = global::Backdrop.Rgba;
using Haze = global::Backdrop.Haze;
using HazeShape = global::Backdrop.HazeShape;
using LayerSpec = global::Backdrop.LayerBackdrop;
using Motes = global::Backdrop.Motes;

namespace Trial.Meta
{
    public sealed class MetaBackdrop : ITweenable
    {
        /// <summary>createBackdrop's opts: { veil: Rgba, vignette: 0..1, moteSpeed: time factor for the specks, className, vignetteColor }.</summary>
        public sealed class Options
        {
            /// <summary>The colour laid over the whole place (ExplorationBackdropSpec.For(layer).Veil); null = none.</summary>
            public BRgba? Veil;

            /// <summary>The vignette's alpha at its outer radius (0 = none).</summary>
            public float Vignette;

            /// <summary>The specks' time factor (1 = BackdropMotion's speed).</summary>
            public float MoteSpeed = 1f;

            /// <summary>An extra class name (the element's name here).</summary>
            public string ClassName;

            /// <summary>The colour the vignette darkens to (null = --ground).</summary>
            public BRgba? VignetteColor;
        }

        // js: scenes/meta/backdrop.js:11
        private const float W = 1920f;
        private const float H = 1080f;

        // js: scenes/meta/backdrop.js:14
        /// <summary>BackdropLayers.ShaftSpacing: how far apart the shafts of one haze stand, as a share of the width.</summary>
        public const float ShaftSpacing = 0.18f;

        // js: scenes/meta/backdrop.js:16
        /// <summary>Samples of a vertical haze's alpha.</summary>
        public const int Samples = 32;

        private readonly Options _opts;
        private readonly BRgba _ground;
        private LayerSpec _current;
        private float _alpha = 1f;
        private readonly List<(Haze Haze, List<VectorGraphic> Parts)> _hazes = new List<(Haze, List<VectorGraphic>)>();
        private readonly List<GradientGraphic> _motes = new List<GradientGraphic>();
        private float _half;

        /// <summary>The backdrop element (backdrop.canvas): 1920×1080 at (0, 0) of its host.</summary>
        public RectTransform Canvas { get; }

        /// <summary>Its node (the alpha is its opacity).</summary>
        public UiNode Node { get; }

        private MetaBackdrop(RectTransform host, LayerSpec spec, Options opts)
        {
            _opts = opts ?? new Options();
            _current = spec ?? throw new ArgumentNullException(nameof(spec));
            // The vignette darkens towards --ground, read from the tokens (backdrop.js:51-54).
            int[] g = MetaUtil.HexRgb(MetaUtil.Token("--ground", "#10141d"));
            _ground = _opts.VignetteColor ?? new BRgba(g[0] / 255f, g[1] / 255f, g[2] / 255f, 1f);

            Canvas = Ui.Div(host, ("ocbg-meta-backdrop " + (_opts.ClassName ?? "")).Trim(), 0f, 0f, W, H);
            Node = UiNode.Of(Canvas);
            Node.PointerEvents = PointerEvents.None;
            // a canvas of its own: the specks move every frame
            Canvas.gameObject.AddComponent<UnityEngine.Canvas>();
            SyncCanvas();
            Materials.GammaEncodingChanged += SyncCanvas;
            Build();
        }

        // The nested canvas follows its layer's colour mode (the stage sets it on the layers it made).
        private void SyncCanvas()
        {
            if (!Canvas) { Materials.GammaEncodingChanged -= SyncCanvas; return; }
            var c = Canvas.GetComponent<UnityEngine.Canvas>();
            Transform up = Canvas.parent;
            UnityEngine.Canvas parentCanvas = up ? up.GetComponentInParent<UnityEngine.Canvas>() : null;
            if (!c || !parentCanvas) return;
            c.vertexColorAlwaysGammaSpace = parentCanvas.vertexColorAlwaysGammaSpace;
            c.additionalShaderChannels = parentCanvas.additionalShaderChannels;
        }

        // js: scenes/meta/backdrop.js:44
        /// <summary>
        /// createBackdrop(host, spec, opts): paints `spec` (a LayerBackdrop from BackdropSpec.For, or one a scene builds
        /// from the same types) into a 1920×1080 element under host. Call Paint(now) every frame (the scene clock's onFrame).
        /// </summary>
        public static MetaBackdrop Create(RectTransform host, LayerSpec spec, Options opts = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            var b = new MetaBackdrop(host, spec, opts);
            b.Paint(MetaUtil.PageNow);
            return b;
        }

        // js: scenes/meta/backdrop.js:57
        /// <summary>build(): the sky, the hazes, the specks, the veil and the vignette, in that order.</summary>
        private void Build()
        {
            Ui.Clear(Canvas);
            _hazes.Clear();
            _motes.Clear();

            // the sky: a vertical gradient through the spec's stops (each At clamped to 0..1)
            var sky = new List<GradientStop>();
            foreach (global::Backdrop.SkyStop stop in _current.Sky ?? Array.Empty<global::Backdrop.SkyStop>())
                sky.Add(GradientStop.At(MetaUtil.RgbaOf(stop.Color), Math.Max(0f, Math.Min(1f, stop.At))));
            if (sky.Count > 0) Fill("sky", LinearGradient.Points(new Vec2(0f, 0f), new Vec2(0f, H), sky.ToArray()));

            foreach (Haze haze in _current.Hazes ?? Array.Empty<Haze>())
            {
                var parts = new List<VectorGraphic>();
                if (haze.Shape == HazeShape.TopGlow || haze.Shape == HazeShape.Band) parts.Add(Vertical(haze));
                else if (haze.Shape == HazeShape.Radial) parts.Add(Radial(haze));
                else if (haze.Shape == HazeShape.Shafts) parts.AddRange(Shafts(haze));
                _hazes.Add((haze, parts));
            }

            Motes m = _current.Motes;
            if (m != null && m.Count > 0)
            {
                // js: scenes/meta/backdrop.js:18 softDot(color, size)
                float px = Math.Max(4f, (float)Math.Ceiling(m.SizePx * 2f));
                _half = px / 2f;
                Rgba solid = MetaUtil.RgbaOf(m.Color.WithAlpha(1f));
                Rgba clear = MetaUtil.RgbaOf(m.Color.WithAlpha(0f));
                RadialGradient dot = RadialGradient.Points(new Vec2(px / 2f, px / 2f), 0f, px / 2f,
                    GradientStop.At(solid, 0f), GradientStop.At(solid, 0.45f), GradientStop.At(clear, 1f));
                for (int i = 0; i < m.Count; i++)
                {
                    GradientGraphic g = GradientGraphic.Make(Canvas, 0f, 0f, px, px, "mote");
                    g.Shape = GradientGraphic.ShapeKind.Ellipse;
                    g.Fringe = false;
                    g.SetPaint(dot);
                    _motes.Add(g);
                }
            }

            if (_opts.Veil.HasValue)
                BoxGraphic.Make(Canvas, new BoxStyle { BackgroundColor = MetaUtil.RgbaOf(_opts.Veil.Value) }, 0f, 0f, W, H, "veil");

            if (_opts.Vignette > 0f)
            {
                Fill("vignette", RadialGradient.Points(new Vec2(W / 2f, H / 2f), H * 0.35f, W * 0.62f,
                    GradientStop.At(MetaUtil.RgbaOf(_ground, 0f), 0f), GradientStop.At(MetaUtil.RgbaOf(_ground, _opts.Vignette), 1f)));
            }
        }

        private GradientGraphic Fill(string name, Paint paint)
        {
            GradientGraphic g = GradientGraphic.Make(Canvas, 0f, 0f, W, H, name);
            g.Fringe = false;
            g.SetPaint(paint);
            return g;
        }

        // js: scenes/meta/backdrop.js:69
        /// <summary>vertical(haze, lit): a TopGlow or Band over the whole screen, Samples + 1 stops of HazeAlphaAt (lit = the graphic's opacity).</summary>
        private VectorGraphic Vertical(Haze haze)
        {
            var unit = new Haze { Shape = haze.Shape, Color = new BRgba(1f, 1f, 1f, 1f), Y = haze.Y, Size = haze.Size };
            var stops = new GradientStop[Samples + 1];
            for (int i = 0; i <= Samples; i++)
            {
                float y = (float)i / Samples;
                stops[i] = GradientStop.At(MetaUtil.RgbaOf(haze.Color, global::Backdrop.BackdropSpec.HazeAlphaAt(unit, y)), y);
            }
            return Fill("haze " + haze.Shape, LinearGradient.Points(new Vec2(0f, 0f), new Vec2(0f, H), stops));
        }

        // js: scenes/meta/backdrop.js:81
        /// <summary>radial(haze, lit): a glow Size × height across at (X × width, Y × height), its colour at the centre to nothing at the rim.</summary>
        private VectorGraphic Radial(Haze haze)
        {
            float across = haze.Size * H;
            float cx = haze.X * W, cy = haze.Y * H, r = across / 2f;
            GradientGraphic g = GradientGraphic.Make(Canvas, cx - r, cy - r, across, across, "haze Radial");
            g.Shape = GradientGraphic.ShapeKind.Ellipse;
            g.Fringe = false;
            g.SetPaint(RadialGradient.Points(new Vec2(r, r), 0f, r,
                GradientStop.At(MetaUtil.RgbaOf(haze.Color, 1f), 0f), GradientStop.At(MetaUtil.RgbaOf(haze.Color, 0f), 1f)));
            return g;
        }

        // js: scenes/meta/backdrop.js:92
        /// <summary>shafts(haze, lit): Count shafts ShaftSpacing apart, Size × width wide, 1.6 screens tall, turned Angle° clockwise about (x, 540).</summary>
        private IEnumerable<VectorGraphic> Shafts(Haze haze)
        {
            float width = haze.Size * W;
            float spacing = W * ShaftSpacing;
            Rgba on = MetaUtil.RgbaOf(haze.Color, 1f), off = MetaUtil.RgbaOf(haze.Color, 0f);
            LinearGradient across = LinearGradient.Points(new Vec2(0f, 0f), new Vec2(width, 0f),
                GradientStop.At(off, 0f), GradientStop.At(on, 0.25f), GradientStop.At(on, 0.75f), GradientStop.At(off, 1f));
            var list = new List<VectorGraphic>();
            for (int i = 0; i < haze.Count; i++)
            {
                float x = haze.X * W + (i - (haze.Count - 1) / 2f) * spacing;
                GradientGraphic g = GradientGraphic.Make(Canvas, x - width / 2f, H / 2f - H * 0.8f, width, H * 1.6f, "haze Shaft");
                g.Fringe = false;
                g.SetPaint(across);
                UiNode n = UiNode.Of(g.rectTransform);
                n.SetOrigin(0.5f, 0.5f);
                n.SetStyleTransform(rot: haze.Angle);
                list.Add(g);
            }
            return list;
        }

        // js: scenes/meta/backdrop.js:111
        /// <summary>
        /// paint(ms): each haze's breath (BackdropMotion.FlickerAlpha at the page time ms), each speck's place and twinkle
        /// at ms × moteSpeed (BackdropMotion.MoteAt / MoteAlpha; its opacity m.Color.A × the twinkle).
        /// </summary>
        public void Paint(double ms)
        {
            if (!Canvas) return;
            foreach ((Haze haze, List<VectorGraphic> parts) in _hazes)
            {
                float lit = global::Backdrop.BackdropMotion.FlickerAlpha(haze.Flicker, ms);
                foreach (VectorGraphic p in parts) if (p) p.Opacity = lit;
            }
            Motes m = _current.Motes;
            if (_motes.Count > 0 && m != null)
            {
                double t = ms * _opts.MoteSpeed;
                for (int i = 0; i < _motes.Count; i++)
                {
                    GradientGraphic g = _motes[i];
                    if (!g) continue;
                    global::Backdrop.BackdropMotion.MoteAt(m, i, t, W, H, out float x, out float y);
                    Ui.Place(g.rectTransform, x - _half, y - _half);
                    g.Opacity = m.Color.A * global::Backdrop.BackdropMotion.MoteAlpha(m, i, t);
                }
            }
        }

        /// <summary>backdrop.spec: the painted spec.</summary>
        public LayerSpec Spec => _current;

        // js: scenes/meta/backdrop.js:141
        /// <summary>use(next): swaps the painted backdrop (no crossfade: the meta screens change place only between scenes).</summary>
        public void Use(LayerSpec next)
        {
            _current = next ?? throw new ArgumentNullException(nameof(next));
            Build();
            Paint(MetaUtil.PageNow);
        }

        /// <summary>backdrop.alpha: the whole backdrop's opacity (globalAlpha of every draw; the hall's lamp).</summary>
        public float Alpha
        {
            get => _alpha;
            set
            {
                _alpha = value;
                if (Node) Node.Opacity = value;
            }
        }

        public float GetTween(string key) => key == "alpha" || key == "v" ? _alpha : 0f;

        public void SetTween(string key, float value)
        {
            if (key == "alpha" || key == "v") Alpha = value;
        }

        // js: scenes/meta/backdrop.js:146
        public void Destroy()
        {
            Materials.GammaEncodingChanged -= SyncCanvas;
            if (Canvas) Ui.Remove(Canvas);
        }

        // js: scenes/meta/backdrop.js:151
        /// <summary>layerBackdrop(layer): the backdrop of `layer` (1〜7) as the bridge gives it.</summary>
        public static LayerSpec LayerBackdrop(int layer) => global::Backdrop.BackdropSpec.For(layer);
    }
}
