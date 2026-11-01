// The fibres of washi (port of scenes/meta/util.js:121-171 applyWashi): a 360×360 tile drawn once with --washi-ink at a
// few percent — no image files, no colour of its own — laid over a washi element's --washi fill and repeated. The
// drawing is deterministic: a Park-Miller sequence (seed 48271) places every blot and fibre, in the JS's order of draws
// (per blot cx, cy, rad, a; per fibre x0, y0, len (two draws), ang, bend, a, w), and each shape is drawn wrapped 3×3 so
// the tile repeats without a seam.
//
// Unity has no canvas: Coverage() rasterises the same shapes into an alpha buffer (engine-free maths: a radial blot is
// a(1 − d/rad), a fibre is its quadratic Bézier stroked w wide with round caps, anti-aliased over one pixel, a hairline
// under 1 px drawn 1 px wide at w of its alpha), composited source-over in float (every shape is the same colour, so
// the order does not matter). The tile becomes a Texture2D (wrap Repeat, bilinear) made with
// linear: Materials.ColorTexturesLinear, so under the gamma target its texels are the sRGB values the target blends.
// A RawImage lays it over the element with its phase at the padding box's top-left (background-origin: padding-box).
//
// Deviations: Chrome keeps its canvas as 8-bit premultiplied pixels and rounds after every draw; this buffer rounds once
// at the end, so a pixel may differ by 1/255 where many shapes overlap. Chrome's own anti-aliasing of the thin strokes
// is approximated (coverage by distance), not reproduced.
using System;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace Trial.Meta
{
    public static class WashiTile
    {
        /// <summary>The tile's size in px (util.js:132 size).</summary>
        public const int Size = 360;

        /// <summary>The Park-Miller seed the JS starts from (util.js:137).</summary>
        public const long Seed = 48271;

        /// <summary>--washi-ink (29, 26, 22): the colour of every fibre and blot.</summary>
        public static readonly Rgba Ink = DsTokens.WashiInk;

        private static Texture2D _tile;
        private static bool _tileLinear;
        private static float[] _coverage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (_tile) UnityEngine.Object.DestroyImmediate(_tile);
            _tile = null;
            _tileLinear = false;
            _coverage = null;
        }

        // ---- the drawing (engine-free) -------------------------------------------------------------------------

        /// <summary>The Park-Miller sequence of the JS: seed = seed × 48271 mod 2147483647, value seed / 2147483647.</summary>
        public sealed class ParkMiller
        {
            private long _seed;

            public ParkMiller(long seed = Seed) { _seed = seed; }

            // js: scenes/meta/util.js:138
            public double Next()
            {
                _seed = _seed * 48271L % 2147483647L;
                return _seed / 2147483647.0;
            }
        }

        /// <summary>Number.prototype.toFixed(3) read back as a number.</summary>
        private static double Fixed3(double v) => Math.Round(v, 3, MidpointRounding.AwayFromZero);

        /// <summary>
        /// The tile's alpha (0〜1) per pixel, row 0 at the top, Size × Size, drawn as util.js:138-168 draws it. Cached:
        /// the caller must not change the array.
        /// </summary>
        // js: scenes/meta/util.js:127
        public static float[] Coverage()
        {
            if (_coverage != null) return _coverage;
            const int size = Size;
            var a = new float[size * size];
            var rnd = new ParkMiller();

            // 26 soft blots: a radial gradient from the blot's alpha at its centre to 0 at rad (util.js:140-149)
            for (int i = 0; i < 26; i++)
            {
                double cx = rnd.Next() * size, cy = rnd.Next() * size, rad = 30 + rnd.Next() * 70;
                double alpha = Fixed3(0.012 + rnd.Next() * 0.02);
                // wrapped(): the nine copies never overlap (rad < 180), so each pixel takes the nearest copy
                int x0 = (int)Math.Floor(cx - rad), x1 = (int)Math.Ceiling(cx + rad);
                int y0 = (int)Math.Floor(cy - rad), y1 = (int)Math.Ceiling(cy + rad);
                for (int py = y0; py < y1; py++)
                {
                    int ty = Wrap(py, size);
                    double dy = py + 0.5 - cy;
                    for (int px = x0; px < x1; px++)
                    {
                        double dx = px + 0.5 - cx;
                        double d = Math.Sqrt(dx * dx + dy * dy) / rad;
                        if (d >= 1.0) continue;
                        Over(a, ty * size + Wrap(px, size), (float)(alpha * (1.0 - d)));
                    }
                }
            }

            // 260 fibres: a quadratic curve from (x0, y0) to (x1, y1) bent by `bend`, round caps (util.js:150-167)
            var scratch = new float[0];
            for (int i = 0; i < 260; i++)
            {
                double x0 = rnd.Next() * size, y0 = rnd.Next() * size;
                double len = 6 + rnd.Next() * rnd.Next() * 46;
                double ang = (rnd.Next() - 0.5) * Math.PI * 0.9;
                double bend = (rnd.Next() - 0.5) * len * 0.6;
                double x1 = x0 + Math.Cos(ang) * len, y1 = y0 + Math.Sin(ang) * len;
                double alpha = Fixed3(0.04 + rnd.Next() * 0.07);
                double w = 0.5 + rnd.Next() * 0.9;
                double qx = (x0 + x1) / 2 + bend, qy = (y0 + y1) / 2 - bend;
                StrokeQuad(a, size, x0, y0, qx, qy, x1, y1, w, alpha, ref scratch);
            }
            _coverage = a;
            return a;
        }

        private static int Wrap(int v, int size)
        {
            int m = v % size;
            return m < 0 ? m + size : m;
        }

        /// <summary>Source-over of a same-coloured shape: α = s + d(1 − s).</summary>
        private static void Over(float[] a, int i, float s)
        {
            if (s <= 0f) return;
            a[i] = s + a[i] * (1f - s);
        }

        /// <summary>One stroked quadratic Bézier with round caps, its coverage the union of its segments, wrapped onto the tile.</summary>
        private static void StrokeQuad(float[] a, int size, double x0, double y0, double qx, double qy, double x1, double y1,
            double width, double alpha, ref float[] scratch)
        {
            // flatten: a segment every ~1 px of chord
            double chord = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) + Math.Sqrt((qx - x0) * (qx - x0) + (qy - y0) * (qy - y0));
            int n = Math.Max(8, (int)Math.Ceiling(chord));
            var px = new double[n + 1];
            var py = new double[n + 1];
            for (int k = 0; k <= n; k++)
            {
                double t = (double)k / n, u = 1 - t;
                px[k] = u * u * x0 + 2 * u * t * qx + t * t * x1;
                py[k] = u * u * y0 + 2 * u * t * qy + t * t * y1;
            }
            // a hairline under 1 px is drawn 1 px wide at w of its alpha
            double we = Math.Max(width, 1.0), k0 = Math.Min(width, 1.0);
            double reach = we / 2 + 0.5;
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            for (int k = 0; k <= n; k++)
            {
                minX = Math.Min(minX, px[k]); maxX = Math.Max(maxX, px[k]);
                minY = Math.Min(minY, py[k]); maxY = Math.Max(maxY, py[k]);
            }
            int bx = (int)Math.Floor(minX - reach), by = (int)Math.Floor(minY - reach);
            int bw = (int)Math.Ceiling(maxX + reach) - bx + 1, bh = (int)Math.Ceiling(maxY + reach) - by + 1;
            if (scratch.Length < bw * bh) scratch = new float[bw * bh];
            Array.Clear(scratch, 0, bw * bh);
            for (int k = 0; k < n; k++)
            {
                double ax = px[k], ay = py[k], cx = px[k + 1], cy = py[k + 1];
                int sx0 = (int)Math.Floor(Math.Min(ax, cx) - reach) - bx, sx1 = (int)Math.Ceiling(Math.Max(ax, cx) + reach) - bx;
                int sy0 = (int)Math.Floor(Math.Min(ay, cy) - reach) - by, sy1 = (int)Math.Ceiling(Math.Max(ay, cy) + reach) - by;
                double vx = cx - ax, vy = cy - ay, vv = vx * vx + vy * vy;
                for (int yy = Math.Max(0, sy0); yy <= Math.Min(bh - 1, sy1); yy++)
                {
                    double pcy = by + yy + 0.5;
                    for (int xx = Math.Max(0, sx0); xx <= Math.Min(bw - 1, sx1); xx++)
                    {
                        double pcx = bx + xx + 0.5;
                        double t = vv > 0 ? ((pcx - ax) * vx + (pcy - ay) * vy) / vv : 0;
                        if (t < 0) t = 0; else if (t > 1) t = 1;
                        double dx = pcx - (ax + vx * t), dy = pcy - (ay + vy * t);
                        double d = Math.Sqrt(dx * dx + dy * dy);
                        double cov = reach - d;
                        if (cov <= 0) continue;
                        if (cov > 1) cov = 1;
                        int idx = yy * bw + xx;
                        if (cov > scratch[idx]) scratch[idx] = (float)cov;
                    }
                }
            }
            for (int yy = 0; yy < bh; yy++)
            {
                int ty = Wrap(by + yy, size);
                for (int xx = 0; xx < bw; xx++)
                {
                    float c = scratch[yy * bw + xx];
                    if (c <= 0f) continue;
                    Over(a, ty * size + Wrap(bx + xx, size), (float)(alpha * k0 * c));
                }
            }
        }

        // ---- the texture -----------------------------------------------------------------------------------------

        /// <summary>
        /// The tile as a texture (made once; again when the gamma encoding changed): RGBA32, --washi-ink with the
        /// coverage as alpha, wrap Repeat, bilinear, no mipmaps.
        /// </summary>
        public static Texture2D Texture
        {
            get
            {
                bool linear = Materials.ColorTexturesLinear;
                if (_tile && _tileLinear == linear) return _tile;
                float[] cov = Coverage();
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, linear)
                {
                    name = "ocbg-washi-tex",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                var px = new Color32[Size * Size];
                byte r = (byte)Ink.R8, g = (byte)Ink.G8, b = (byte)Ink.B8;
                for (int y = 0; y < Size; y++)
                {
                    int row = (Size - 1 - y) * Size;   // Texture2D row 0 is the bottom
                    for (int x = 0; x < Size; x++)
                    {
                        float al = cov[y * Size + x];
                        byte ab = (byte)Math.Floor(Math.Max(0f, Math.Min(1f, al)) * 255f + 0.5f);
                        px[row + x] = new Color32(r, g, b, ab);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _tile = tex;
                _tileLinear = linear;
                return tex;
            }
        }

        // js: scenes/meta/util.js:127
        /// <summary>
        /// applyWashi(el): the fibre tile over the element's border box (w × h), repeated every 360 px with its phase at the
        /// padding box's top-left (inside <paramref name="border"/>), as the first background layer of .ocbg-meta-washi
        /// paints it. Add it after the element's background colour and gradients and before its inset shadows, border and
        /// content (MetaStyle.Washi does). Returns the RawImage (no raycasts).
        /// </summary>
        public static RawImage ApplyWashi(RectTransform el, float w, float h, Insets border = default)
        {
            if (el == null) throw new ArgumentNullException(nameof(el));
            RectTransform rt = Ui.Div(el, "washi-tex", 0f, 0f, w, h);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            img.texture = Texture;
            img.color = Materials.VertexColor(Rgba.White);
            SetTile(img, w, h, border);
            return img;
        }

        /// <summary>The uvRect for a w × h border box whose tile phase starts at (border.Left, border.Top).</summary>
        public static void SetTile(RawImage img, float w, float h, Insets border = default)
        {
            if (!img) return;
            float s = Size;
            // u = (x − left) / 360 from the left edge; v runs bottom-up: at the top edge v = 1 + top / 360 (mod 1)
            img.uvRect = new Rect(-border.Left / s, 1f + border.Top / s - h / s, w / s, h / s);
        }
    }
}
