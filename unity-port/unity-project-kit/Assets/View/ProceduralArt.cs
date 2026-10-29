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
    private static readonly Sprite[] Icons = new Sprite[4];

    public static Sprite White => Cached(ref _white, () => MakeSprite(4, 4, (x, y) => Color.white));

    public static Sprite Circle => Cached(ref _circle, () => MakeSprite(64, 64, (x, y) =>
    {
        float d = Distance(x, y, 32f, 32f) / 30f;
        return Alpha(Mathf.Clamp01((1f - d) * 6f));
    }));

    public static Sprite SoftCircle => Cached(ref _softCircle, () => MakeSprite(64, 64, (x, y) =>
    {
        float d = Distance(x, y, 32f, 32f) / 32f;
        return Alpha(Mathf.Clamp01(1f - d * d));
    }));

    /// <summary>Radial lantern glow: alpha = (1 − r)².</summary>
    public static Sprite Glow => Cached(ref _glow, () => MakeSprite(128, 128, (x, y) =>
    {
        float d = Mathf.Clamp01(Distance(x, y, 64f, 64f) / 64f);
        float a = (1f - d) * (1f - d);
        return Alpha(a);
    }));

    public static Sprite Shield => Cached(ref _shield, () => MakeSprite(64, 72, (x, y) =>
        Alpha(InsideShield(x / 64f, 1f - y / 72f, 0f) ? 1f : 0f)));

    public static Sprite Figure => Cached(ref _figure, () => MakeSprite(140, 300, (x, y) => Alpha(InsideFigure(x, y, false) ? 1f : 0f)));

    public static Sprite FigureWithSpear => Cached(ref _figureSpear, () => MakeSprite(140, 300, (x, y) => Alpha(InsideFigure(x, y, true) ? 1f : 0f)));

    public static Sprite Icon(IconKind kind)
    {
        return Cached(ref Icons[(int)kind], () => MakeSprite(32, 32, (x, y) => Alpha(InsideIcon(kind, x, y) ? 1f : 0f)));
    }

    /// <summary>1×64 vertical gradient, top colour at the top.</summary>
    public static Sprite VerticalGradient(Color top, Color bottom)
    {
        return MakeSprite(2, 64, (x, y) => Color.Lerp(bottom, top, y / 63f), FilterMode.Bilinear);
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
        if (InsideEllipse(x, y, cx, 262f, 23f, 25f)) return true;                    // head
        if (InsideRect(x, y, cx - 7f, 226f, cx + 7f, 244f)) return true;              // neck
        float torsoHalf = Mathf.Lerp(34f, 26f, (y - 150f) / 80f);              // shoulders wider than waist
        if (y >= 148f && y <= 232f && Mathf.Abs(x - cx) <= torsoHalf) return true;
        if (InsideRect(x, y, cx - 30f, 6f, cx - 8f, 152f)) return true;               // left leg
        if (InsideRect(x, y, cx + 8f, 6f, cx + 30f, 152f)) return true;               // right leg
        if (InsideRect(x, y, cx - 44f, 0f, cx - 6f, 10f)) return true;                // feet
        if (InsideRect(x, y, cx + 6f, 0f, cx + 46f, 10f)) return true;
        if (InsideSegment(x, y, cx - 32f, 226f, cx - 52f, 150f, 9f)) return true;       // left arm
        if (spear)
        {
            if (InsideSegment(x, y, cx + 32f, 226f, cx + 56f, 176f, 9f)) return true;   // right arm holding
            if (InsideSegment(x, y, cx + 62f, 20f, cx + 44f, 298f, 3f)) return true;    // spear shaft
            if (InsideEllipse(x, y, cx + 42f, 292f, 5f, 12f)) return true;           // spear head
        }
        else
        {
            if (InsideSegment(x, y, cx + 32f, 226f, cx + 52f, 150f, 9f)) return true;   // right arm
            if (InsideSegment(x, y, cx + 52f, 150f, cx + 70f, 118f, 4f)) return true;   // short blade
        }
        return false;
    }

    private static bool InsideIcon(IconKind kind, int x, int y)
    {
        switch (kind)
        {
            case IconKind.Sword:
                return InsideSegment(x, y, 6f, 6f, 25f, 25f, 2.2f) || InsideSegment(x, y, 9f, 13f, 13f, 9f, 2f) || InsideSegment(x, y, 4f, 4f, 8f, 8f, 3f);
            case IconKind.Shield:
                return InsideShield(x / 32f, 1f - y / 32f, 0f) && !InsideShield(x / 32f, 1f - y / 32f, 0.16f);
            case IconKind.Footprint:
                return InsideEllipse(x, y, 11f, 12f, 4.5f, 7f) || InsideEllipse(x, y, 21f, 20f, 4.5f, 7f);
            case IconKind.Heal:
                return InsideRect(x, y, 13f, 5f, 19f, 27f) || InsideRect(x, y, 5f, 13f, 27f, 19f);
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
    // Public so every screen draws with the same shape tests and texture builder instead of a private copy (#618).

    public static float Distance(float x, float y, float cx, float cy)
    {
        float dx = x - cx, dy = y - cy;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The distance from the point (<paramref name="px"/>, <paramref name="py"/>) to the nearest point of the segment (x0, y0)–(x1, y1).</summary>
    public static float DistanceToSegment(float px, float py, float x0, float y0, float x1, float y1)
    {
        float vx = x1 - x0, vy = y1 - y0;
        float len2 = vx * vx + vy * vy;
        float t = len2 <= 0f ? 0f : Mathf.Clamp01(((px - x0) * vx + (py - y0) * vy) / len2);
        return Distance(px, py, x0 + vx * t, y0 + vy * t);
    }

    public static bool InsideEllipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return dx * dx + dy * dy <= 1f;
    }

    public static bool InsideRect(float x, float y, float x0, float y0, float x1, float y1)
    {
        return x >= x0 && x <= x1 && y >= y0 && y <= y1;
    }

    /// <summary>Whether the point is within <paramref name="halfWidth"/> of the segment (x0, y0)–(x1, y1).</summary>
    public static bool InsideSegment(float px, float py, float x0, float y0, float x1, float y1, float halfWidth)
    {
        return DistanceToSegment(px, py, x0, y0, x1, y1) <= halfWidth;
    }

    private static Color Alpha(float a) => new Color(1f, 1f, 1f, a);

    /// <summary>A w×h sprite whose pixel at (x, y) is <paramref name="pixel"/>(x, y); y runs from the bottom.</summary>
    public static Sprite MakeSprite(int w, int h, Func<int, int, Color> pixel, FilterMode filter = FilterMode.Bilinear)
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
