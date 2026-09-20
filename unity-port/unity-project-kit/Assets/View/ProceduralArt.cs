// Every texture the battle screen draws is generated here at runtime, so the
// View ships with no image files. Real art (Live2D figures, brushed banners,
// paper scans) is part of the remaining 20% and replaces these sprite by sprite.
#if UNITY_2021_2_OR_NEWER
using System;
using UnityEngine;

public static class ProceduralArt
{
    public enum IconKind
    {
        Sword,
        Shield,
        Footprint,
        Heal,
    }

    private static Sprite _white;
    private static Sprite _circle;
    private static Sprite _softCircle;
    private static Sprite _glow;
    private static Sprite _shield;
    private static Sprite _figure;
    private static Sprite _figureSpear;
    private static Sprite _floorNoise;
    private static Sprite _mist;
    private static Sprite _paperGrain;
    private static Sprite _triangle;
    private static Sprite _vignette;
    private static readonly Sprite[] Icons = new Sprite[4];

    public static Sprite White => Cached(ref _white, () => Make(4, 4, (x, y) => Color.white));

    public static Sprite Circle => Cached(ref _circle, () => Make(64, 64, (x, y) =>
    {
        float d = Dist(x, y, 32f, 32f) / 30f;
        return Alpha(Mathf.Clamp01((1f - d) * 6f));
    }));

    public static Sprite SoftCircle => Cached(ref _softCircle, () => Make(64, 64, (x, y) =>
    {
        float d = Dist(x, y, 32f, 32f) / 32f;
        return Alpha(Mathf.Clamp01(1f - d * d));
    }));

    /// <summary>Radial lantern glow: alpha = (1 − r)².</summary>
    public static Sprite Glow => Cached(ref _glow, () => Make(128, 128, (x, y) =>
    {
        float d = Mathf.Clamp01(Dist(x, y, 64f, 64f) / 64f);
        float a = (1f - d) * (1f - d);
        return Alpha(a);
    }));

    public static Sprite Shield => Cached(ref _shield, () => Make(64, 72, (x, y) =>
        Alpha(InsideShield(x / 64f, 1f - y / 72f, 0f) ? 1f : 0f)));

    public static Sprite Figure => Cached(ref _figure, () => Make(140, 300, (x, y) => Alpha(InsideFigure(x, y, false) ? 1f : 0f)));

    public static Sprite FigureWithSpear => Cached(ref _figureSpear, () => Make(140, 300, (x, y) => Alpha(InsideFigure(x, y, true) ? 1f : 0f)));

    /// <summary>Wet cobble: value noise with a faint horizontal highlight band.</summary>
    public static Sprite FloorNoise => Cached(ref _floorNoise, () => Make(256, 64, (x, y) =>
    {
        float n = Mathf.PerlinNoise(x * 0.11f, y * 0.23f) * 0.6f + Mathf.PerlinNoise(x * 0.37f + 9f, y * 0.51f) * 0.4f;
        float band = Mathf.Exp(-Mathf.Pow((y - 46f) / 5f, 2f)) * 0.35f;
        float v = Mathf.Clamp01(0.55f + (n - 0.5f) * 0.8f + band);
        return new Color(v, v, v, 1f);
    }));

    /// <summary>Horizontally seamless soft noise for the two mist layers (tiled by UGUI).</summary>
    public static Sprite Mist => Cached(ref _mist, () => Make(512, 128, (x, y) =>
    {
        const float w = 512f;
        float t = x / w;
        float a = Mathf.PerlinNoise(x * 0.012f, y * 0.03f);
        float b = Mathf.PerlinNoise((x - w) * 0.012f, y * 0.03f);
        float n = Mathf.Lerp(a, b, t);
        float edge = Mathf.Sin(y / 128f * Mathf.PI); // fade at top and bottom
        return Alpha(Mathf.Clamp01((n - 0.35f) * 1.6f) * edge);
    }));

    /// <summary>Paper fibres: sparse grey speckle with alpha.</summary>
    public static Sprite PaperGrain => Cached(ref _paperGrain, () => Make(256, 256, (x, y) =>
    {
        float n = Mathf.PerlinNoise(x * 0.9f, y * 0.9f);
        float fibre = Mathf.PerlinNoise(x * 0.05f, y * 0.6f);
        float a = Mathf.Clamp01((n - 0.62f) * 2f) * 0.5f + Mathf.Clamp01((fibre - 0.6f) * 3f) * 0.3f;
        return new Color(0.2f, 0.15f, 0.1f, a * 0.5f);
    }));

    /// <summary>Right triangle filling the top-right half (page tear).</summary>
    public static Sprite Triangle => Cached(ref _triangle, () => Make(64, 64, (x, y) => Alpha(x >= 64 - y ? 1f : 0f)));

    /// <summary>Corner vignette: opaque in the corner, clear toward the centre. Rotate per corner.</summary>
    public static Sprite Vignette => Cached(ref _vignette, () => Make(128, 128, (x, y) =>
    {
        float d = Mathf.Clamp01(Dist(x, y, 0f, 0f) / 128f);
        return Alpha((1f - d) * (1f - d));
    }));

    public static Sprite Icon(IconKind kind)
    {
        return Cached(ref Icons[(int)kind], () => Make(32, 32, (x, y) => Alpha(InsideIcon(kind, x, y) ? 1f : 0f)));
    }

    /// <summary>1×64 vertical gradient, top colour at the top.</summary>
    public static Sprite VerticalGradient(Color top, Color bottom)
    {
        return Make(2, 64, (x, y) => Color.Lerp(bottom, top, y / 63f), FilterMode.Bilinear);
    }

    // ---- shape tests --------------------------------------------------------------

    private static bool InsideShield(float u, float v, float inset)
    {
        // u 0..1 left→right, v 0..1 top→bottom. Straight top, pointed bottom.
        float cx = Mathf.Abs(u - 0.5f) * 2f;
        float width = v < 0.45f ? 1f : 1f - Mathf.Pow((v - 0.45f) / 0.55f, 1.15f);
        width -= inset;
        return v >= 0.02f + inset && v <= 0.98f - inset && cx <= width * 0.96f;
    }

    private static bool InsideFigure(int x, int y, bool spear)
    {
        // 140×300, y from the bottom (feet at y ≈ 0).
        float cx = 70f;
        if (Ellipse(x, y, cx, 262f, 23f, 25f)) return true;                    // head
        if (Rect(x, y, cx - 7f, 226f, cx + 7f, 244f)) return true;              // neck
        float torsoHalf = Mathf.Lerp(34f, 26f, (y - 150f) / 80f);              // shoulders wider than waist
        if (y >= 148f && y <= 232f && Mathf.Abs(x - cx) <= torsoHalf) return true;
        if (Rect(x, y, cx - 30f, 6f, cx - 8f, 152f)) return true;               // left leg
        if (Rect(x, y, cx + 8f, 6f, cx + 30f, 152f)) return true;               // right leg
        if (Rect(x, y, cx - 44f, 0f, cx - 6f, 10f)) return true;                // feet
        if (Rect(x, y, cx + 6f, 0f, cx + 46f, 10f)) return true;
        if (Line(x, y, cx - 32f, 226f, cx - 52f, 150f, 9f)) return true;       // left arm
        if (spear)
        {
            if (Line(x, y, cx + 32f, 226f, cx + 56f, 176f, 9f)) return true;   // right arm holding
            if (Line(x, y, cx + 62f, 20f, cx + 44f, 298f, 3f)) return true;    // spear shaft
            if (Ellipse(x, y, cx + 42f, 292f, 5f, 12f)) return true;           // spear head
        }
        else
        {
            if (Line(x, y, cx + 32f, 226f, cx + 52f, 150f, 9f)) return true;   // right arm
            if (Line(x, y, cx + 52f, 150f, cx + 70f, 118f, 4f)) return true;   // short blade
        }
        return false;
    }

    private static bool InsideIcon(IconKind kind, int x, int y)
    {
        switch (kind)
        {
            case IconKind.Sword:
                return Line(x, y, 6f, 6f, 25f, 25f, 2.2f) || Line(x, y, 9f, 13f, 13f, 9f, 2f) || Line(x, y, 4f, 4f, 8f, 8f, 3f);
            case IconKind.Shield:
                return InsideShield(x / 32f, 1f - y / 32f, 0f) && !InsideShield(x / 32f, 1f - y / 32f, 0.16f);
            case IconKind.Footprint:
                return Ellipse(x, y, 11f, 12f, 4.5f, 7f) || Ellipse(x, y, 21f, 20f, 4.5f, 7f);
            case IconKind.Heal:
                return Rect(x, y, 13f, 5f, 19f, 27f) || Rect(x, y, 5f, 13f, 27f, 19f);
        }
        return false;
    }

    /// <summary>
    /// Domain reload on entering Play Mode is off in this project, so these statics outlive the
    /// sprites (and textures) that stopping Play Mode destroys. Unity's own null check sees
    /// through that; `??=` and `is null` do not.
    /// </summary>
    private static Sprite Cached(ref Sprite slot, Func<Sprite> make)
    {
        if (!slot || !slot.texture) slot = make();
        return slot;
    }

    // ---- primitives -----------------------------------------------------------------

    private static float Dist(float x, float y, float cx, float cy)
    {
        float dx = x - cx, dy = y - cy;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static bool Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return dx * dx + dy * dy <= 1f;
    }

    private static bool Rect(float x, float y, float x0, float y0, float x1, float y1)
    {
        return x >= x0 && x <= x1 && y >= y0 && y <= y1;
    }

    private static bool Line(float px, float py, float x0, float y0, float x1, float y1, float halfWidth)
    {
        float vx = x1 - x0, vy = y1 - y0;
        float len2 = vx * vx + vy * vy;
        float t = len2 <= 0f ? 0f : Mathf.Clamp01(((px - x0) * vx + (py - y0) * vy) / len2);
        return Dist(px, py, x0 + vx * t, y0 + vy * t) <= halfWidth;
    }

    private static Color Alpha(float a) => new Color(1f, 1f, 1f, a);

    private static Sprite Make(int w, int h, Func<int, int, Color> pixel, FilterMode filter = FilterMode.Bilinear)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = filter,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                pixels[y * w + x] = pixel(x, y);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }
}
#endif
