// Sprites the battle-visual-v1 parts need (#242), drawn in code like ProceduralArt: rounded panels
// (sliced, so one sprite fits any size), dashed edges (tiled), and the line icons of 付録 A that the
// card face and the panels use. They are placeholders until the UI art (#92) replaces them.
#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    /// <summary>The line icons of battle-visual-v1 付録 A that no other sprite draws yet.</summary>
    public enum VisualIcon
    {
        Range,
        Self,
        Forward,
        Back,
        Heal,
        Draw,
        Seal,
    }

    /// <summary>
    /// battle-visual-v1 §4 gives every place on the 1920×1080 screen, x from the left and y from the top
    /// (#242). This puts such a point on the canvas the way the hand does (DepictionPlayer.HandPoint):
    /// x kept from the screen's centre and y from its bottom, so a screen of another shape keeps the
    /// lower parts on its bottom edge.
    /// </summary>
    public static class RefScreen
    {
        public static RectTransform Root(Transform t)
        {
            Canvas canvas = t ? t.GetComponentInParent<Canvas>() : null;
            return canvas ? (RectTransform)canvas.rootCanvas.transform : null;
        }

        /// <summary>The world point of the screen point <paramref name="screen"/> on <paramref name="root"/>.</summary>
        public static Vector3 World(RectTransform root, Vector2 screen)
        {
            Rect area = root.rect;
            var local = new Vector2(area.center.x + (screen.x - BattleTheme.RefWidth * 0.5f), area.yMin + (BattleTheme.RefHeight - screen.y));
            return root.TransformPoint(local);
        }

        /// <summary>The screen point (x from the left, y from the top) a world point stands on.</summary>
        public static Vector2 Of(RectTransform root, Vector3 world)
        {
            Vector3 local = root.InverseTransformPoint(world);
            Rect area = root.rect;
            return new Vector2(local.x - area.center.x + BattleTheme.RefWidth * 0.5f, BattleTheme.RefHeight - (local.y - area.yMin));
        }

        /// <summary>Sizes <paramref name="rt"/> to <paramref name="size"/> px and puts its top-left on the screen point <paramref name="topLeft"/>.</summary>
        public static void Place(RectTransform rt, RectTransform root, Vector2 topLeft, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.position = World(root, topLeft);
        }
    }

    public static class VisualArt
    {
        private const int IconSize = 48; // the 24-grid of 付録 A at twice its size
        private static readonly Dictionary<int, Sprite> RoundedFills = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> RoundedRings = new Dictionary<int, Sprite>();
        private static readonly Sprite[] Icons = new Sprite[Enum.GetValues(typeof(VisualIcon)).Length];
        private static Sprite _dashH;
        private static Sprite _dashV;

        // ---- panels -----------------------------------------------------------------------------

        /// <summary>A filled rounded rectangle with corners of <paramref name="radius"/> px, sliced to any size.</summary>
        public static Sprite Rounded(int radius)
        {
            radius = Mathf.Max(1, radius);
            int key = radius;
            if (RoundedFills.TryGetValue(key, out Sprite cached) && cached && cached.texture) return cached;
            Sprite made = RoundedSprite(radius, 0);
            RoundedFills[key] = made;
            return made;
        }

        /// <summary>The outline of a rounded rectangle, <paramref name="thickness"/> px wide, sliced to any size.</summary>
        public static Sprite RoundedRing(int radius, int thickness)
        {
            radius = Mathf.Max(1, radius);
            thickness = Mathf.Clamp(thickness, 1, radius + 1);
            int key = radius * 100 + thickness;
            if (RoundedRings.TryGetValue(key, out Sprite cached) && cached && cached.texture) return cached;
            Sprite made = RoundedSprite(radius, thickness);
            RoundedRings[key] = made;
            return made;
        }

        private static Sprite RoundedSprite(int radius, int ring)
        {
            int border = Mathf.Max(radius, ring) + 1;
            int size = border * 2 + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float outside = RoundedDistance(x + 0.5f, y + 0.5f, size, radius);
                    float a = Mathf.Clamp01(0.5f - outside);
                    if (ring > 0) a *= Mathf.Clamp01(outside + ring + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        /// <summary>Signed distance from (x, y) to the edge of a rounded square of side <paramref name="size"/>; negative inside.</summary>
        private static float RoundedDistance(float x, float y, int size, int radius)
        {
            float half = size * 0.5f;
            float qx = Mathf.Abs(x - half) - (half - radius);
            float qy = Mathf.Abs(y - half) - (half - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        /// <summary>A sliced rounded image filling <paramref name="parent"/>.</summary>
        public static Image Panel(RectTransform parent, string name, Color color, int radius)
        {
            Image image = UiKit.Image(parent, name, Rounded(radius), color, Vector2.zero, Vector2.one);
            image.type = Image.Type.Sliced;
            return image;
        }

        /// <summary>A sliced rounded outline filling <paramref name="parent"/>, grown by <paramref name="outset"/> px on every side.</summary>
        public static Image Ring(RectTransform parent, string name, Color color, int radius, int thickness, float outset = 0f)
        {
            Image image = UiKit.Image(parent, name, RoundedRing(radius, thickness), color, Vector2.zero, Vector2.one);
            image.type = Image.Type.Sliced;
            image.rectTransform.offsetMin = new Vector2(-outset, -outset);
            image.rectTransform.offsetMax = new Vector2(outset, outset);
            return image;
        }

        // ---- dashed edges (§3.2: 3 px dashes, 3 px apart) ------------------------------------------

        private static Sprite DashSprite(bool horizontal)
        {
            int w = horizontal ? 6 : 1;
            int h = horizontal ? 1 : 6;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[w * h];
            for (int i = 0; i < 6; i++) pixels[i] = new Color(1f, 1f, 1f, i < 3 ? 1f : 0f);
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        public static Sprite DashH => _dashH && _dashH.texture ? _dashH : (_dashH = DashSprite(true));
        public static Sprite DashV => _dashV && _dashV.texture ? _dashV : (_dashV = DashSprite(false));

        /// <summary>Four dashed edges inside <paramref name="parent"/>, as UiKit.Frame draws solid ones.</summary>
        public static Image[] DashedFrame(RectTransform parent, Color color, float thickness)
        {
            Image[] edges = UiKit.Frame(parent, color, thickness);
            for (int i = 0; i < edges.Length; i++)
            {
                edges[i].sprite = i < 2 ? DashH : DashV;
                edges[i].type = Image.Type.Tiled;
                edges[i].gameObject.name = "Dashed" + edges[i].gameObject.name;
            }
            return edges;
        }

        // ---- §3.2 斜線 / §4.2 floor --------------------------------------------------------------------

        private static readonly Dictionary<int, Sprite> StripeSprites = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> RingSprites = new Dictionary<int, Sprite>();
        private static Sprite _fade;

        /// <summary>
        /// §3.2 斜線: white stripes 3 px wide, tiled (Image.Type.Tiled). <paramref name="direction"/> 0 is
        /// 45° (／), 1 is −45° (＼), 2 is 90° (｜) — one per enemy (§4.2). Normal stripes are 11 px apart;
        /// <paramref name="dense"/> ones (a cell that meets the omen's threshold) are 6 px apart. The tile
        /// is a whole number of pixels, so a slanted spacing comes out within half a pixel of the value.
        /// </summary>
        public static Sprite Stripes(int direction, bool dense)
        {
            direction = Mathf.Clamp(direction, 0, 2);
            int key = direction * 2 + (dense ? 1 : 0);
            if (StripeSprites.TryGetValue(key, out Sprite cached) && cached && cached.texture) return cached;
            float spacing = dense ? 6f : 11f;
            const float width = 3f;
            bool slanted = direction < 2;
            // A slanted tile repeats every spacing × √2 along x and y.
            int tile = Mathf.Max(4, Mathf.RoundToInt(slanted ? spacing * 1.41421356f : spacing));
            float period = slanted ? tile / 1.41421356f : tile; // the spacing the tile really draws
            var tex = new Texture2D(tile, tile, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[tile * tile];
            for (int y = 0; y < tile; y++)
            {
                for (int x = 0; x < tile; x++)
                {
                    float along;
                    if (direction == 0) along = ((x - y) % tile + tile) % tile / 1.41421356f;
                    else if (direction == 1) along = ((x + y) % tile) / 1.41421356f;
                    else along = x;
                    // One stripe down the middle of each period.
                    float distance = Mathf.Abs(along - period * 0.5f);
                    float a = Mathf.Clamp01(width * 0.5f - distance + 0.5f);
                    pixels[y * tile + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            Sprite made = Sprite.Create(tex, new Rect(0, 0, tile, tile), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            StripeSprites[key] = made;
            return made;
        }

        /// <summary>
        /// A white fill whose alpha runs from 1 at the top down to 18 / 55 at the bottom: tinted amber at
        /// 55%, it is §4.2's 届くマスの光 (琥珀の塗り、上 55% → 下 18%).
        /// </summary>
        public static Sprite Fade
        {
            get
            {
                if (_fade && _fade.texture) return _fade;
                const int h = 32;
                var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < h; y++) tex.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.Lerp(18f / 55f, 1f, y / (h - 1f))));
                tex.Apply();
                _fade = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                return _fade;
            }
        }

        /// <summary>
        /// A circle outline <paramref name="thickness"/> px wide on a 16 px dot (§4.7 粒: 3 px for the stamina
        /// about to be paid, 2 px for an empty one). Drawn at twice the size; use Image.Type.Simple.
        /// </summary>
        public static Sprite CircleRing(float thickness)
        {
            int key = Mathf.RoundToInt(thickness * 10f);
            if (RingSprites.TryGetValue(key, out Sprite cached) && cached && cached.texture) return cached;
            const int size = 32;
            float outer = size * 0.5f;
            float inner = outer - thickness * 2f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - outer, y + 0.5f - outer).magnitude;
                    float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            Sprite made = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            RingSprites[key] = made;
            return made;
        }

        // ---- 付録 A line icons ---------------------------------------------------------------------

        /// <summary>The sprite of a 付録 A icon, white so the Image tints it.</summary>
        public static Sprite Icon(VisualIcon icon)
        {
            int slot = (int)icon;
            if (!Icons[slot] || !Icons[slot].texture) Icons[slot] = DrawIcon(Segments(icon), Rings(icon));
            return Icons[slot];
        }

        /// <summary>The segments of each icon in 付録 A's 24 grid (x right, y down).</summary>
        private static float[][] Segments(VisualIcon icon)
        {
            switch (icon)
            {
                case VisualIcon.Range: return new[] { S(3, 12, 21, 12), S(3, 7, 3, 17), S(21, 7, 21, 17) };
                case VisualIcon.Forward: return new[] { S(4, 12, 18, 12), S(13, 6, 19, 12), S(19, 12, 13, 18) };
                case VisualIcon.Back: return new[] { S(20, 12, 6, 12), S(11, 6, 5, 12), S(5, 12, 11, 18) };
                case VisualIcon.Heal: return new[] { S(12, 6, 12, 18), S(6, 12, 18, 12) };
                case VisualIcon.Draw:
                    return new[] { S(8, 3, 17, 3), S(17, 3, 17, 17), S(17, 17, 8, 17), S(8, 17, 8, 3), S(5, 7, 5, 21), S(5, 21, 14, 21) };
                case VisualIcon.Seal:
                    return new[]
                    {
                        S(9, 3, 15, 3), S(15, 3, 15, 9), S(15, 9, 9, 9), S(9, 9, 9, 3),
                        S(9, 9, 6, 13), S(6, 13, 18, 13), S(18, 13, 15, 9),
                        S(4, 13, 20, 13), S(20, 13, 20, 17), S(20, 17, 4, 17), S(4, 17, 4, 13),
                        S(6, 21, 18, 21),
                    };
                case VisualIcon.Self: return new float[0][];
                default: return new float[0][];
            }
        }

        /// <summary>Ring arcs: centre x, centre y, radius, and whether only the upper half is drawn.</summary>
        private static float[][] Rings(VisualIcon icon)
        {
            if (icon == VisualIcon.Self) return new[] { new[] { 12f, 7.5f, 3.5f, 0f }, new[] { 12f, 21f, 7f, 1f } };
            return new float[0][];
        }

        private static float[] S(float x0, float y0, float x1, float y1)
        {
            return new[] { x0, y0, x1, y1 };
        }

        private static Sprite DrawIcon(float[][] segments, float[][] rings)
        {
            const float scale = IconSize / 24f;
            const float halfWidth = 1.1f; // 付録 A: 2.2 wide lines, in grid units
            var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[IconSize * IconSize];
            for (int py = 0; py < IconSize; py++)
            {
                for (int px = 0; px < IconSize; px++)
                {
                    // Texture rows count from the bottom; the grid counts from the top.
                    float gx = (px + 0.5f) / scale;
                    float gy = (IconSize - py - 0.5f) / scale;
                    float best = float.MaxValue;
                    foreach (float[] s in segments) best = Mathf.Min(best, SegmentDistance(gx, gy, s[0], s[1], s[2], s[3]));
                    foreach (float[] r in rings)
                    {
                        if (r[3] > 0f && gy > r[1]) continue;
                        best = Mathf.Min(best, Mathf.Abs(new Vector2(gx - r[0], gy - r[1]).magnitude - r[2]));
                    }
                    float a = Mathf.Clamp01((halfWidth - best) * scale + 0.5f);
                    pixels[py * IconSize + px] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, IconSize, IconSize), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float SegmentDistance(float px, float py, float x0, float y0, float x1, float y1)
        {
            float vx = x1 - x0, vy = y1 - y0;
            float len2 = vx * vx + vy * vy;
            float t = len2 <= 0f ? 0f : Mathf.Clamp01(((px - x0) * vx + (py - y0) * vy) / len2);
            float dx = px - (x0 + vx * t), dy = py - (y0 + vy * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // ---- card values (§6.1 / §8) -----------------------------------------------------------------

        /// <summary>§8: the icon of a value column.</summary>
        public static Sprite ValueIcon(CardValueKind kind)
        {
            switch (kind)
            {
                case CardValueKind.Power: return ProceduralArt.Icon(ProceduralArt.IconKind.Sword);
                case CardValueKind.Guard: return ProceduralArt.Icon(ProceduralArt.IconKind.Shield);
                case CardValueKind.Heal: return Icon(VisualIcon.Heal);
                case CardValueKind.Close:
                case CardValueKind.Push:
                    return Icon(VisualIcon.Forward);
                case CardValueKind.Back:
                case CardValueKind.Pull:
                    return Icon(VisualIcon.Back);
                default: return Icon(VisualIcon.Draw);
            }
        }

        /// <summary>§8: the colour of a value column's icon and number.</summary>
        public static Color ValueColor(CardValueKind kind)
        {
            switch (kind)
            {
                case CardValueKind.Power: return BattleTheme.Phosphor;
                case CardValueKind.Guard: return BattleTheme.Guard;
                case CardValueKind.Heal: return BattleTheme.HpInk;
                default: return BattleTheme.Ink;
            }
        }

        /// <summary>§6.1 属性アイコン: sword, shield, star, flag.</summary>
        public static Sprite KindIcon(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Attack: return ProceduralArt.Icon(ProceduralArt.IconKind.Sword);
                case CardKind.Guard: return ProceduralArt.Icon(ProceduralArt.IconKind.Shield);
                case CardKind.Stance: return OmenIconArt.Of(OmenIcon.Stance);
                default: return OmenIconArt.Of(OmenIcon.Status);
            }
        }
    }
}
#endif
