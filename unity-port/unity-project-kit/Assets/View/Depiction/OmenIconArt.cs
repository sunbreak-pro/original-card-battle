// Placeholder icons for the omen badge (#349). Drawn in code until the UI art (#92); the status
// icon look is #350 to settle. The sword and the shield are ProceduralArt's own; the other four are
// drawn here after battle-visual-v1 付録 A, in white, so the badge tints them.
#if UNITY_2021_2_OR_NEWER
using System;
using UnityEngine;

namespace Depiction.View
{
    public static class OmenIconArt
    {
        private const int Size = 32;
        private static readonly Sprite[] Shapes = new Sprite[Enum.GetValues(typeof(OmenIcon)).Length];

        /// <summary>The sprite for an omen icon, or null for <see cref="OmenIcon.None"/>.</summary>
        public static Sprite Of(OmenIcon icon)
        {
            switch (icon)
            {
                case OmenIcon.None: return null;
                case OmenIcon.Attack: return ProceduralArt.Icon(ProceduralArt.IconKind.Sword);
                case OmenIcon.Guard: return ProceduralArt.Icon(ProceduralArt.IconKind.Shield);
            }
            int slot = (int)icon;
            // Domain reload on entering Play Mode is off, so the statics outlive the sprites that
            // stopping Play Mode destroys; Unity's own null check sees through that (ProceduralArt.Cached).
            if (!Shapes[slot] || !Shapes[slot].texture) Shapes[slot] = Make((x, y) => Inside(icon, x, y));
            return Shapes[slot];
        }

        // ---- shapes (32×32, y counted from the bottom) ----------------------------------

        private static bool Inside(OmenIcon icon, int x, int y)
        {
            switch (icon)
            {
                case OmenIcon.Status:
                    // 付録 A スキル: the four-pointed star, filled.
                    return Mathf.Sqrt(Mathf.Abs(x - 16f)) + Mathf.Sqrt(Mathf.Abs(y - 16f)) <= Mathf.Sqrt(13f);
                case OmenIcon.Move:
                    // 付録 A 前へ: an arrow to the right.
                    return Line(x, y, 5f, 16f, 25f, 16f, 2.2f) || Line(x, y, 17f, 8f, 25f, 16f, 2.2f) || Line(x, y, 17f, 24f, 25f, 16f, 2.2f);
                case OmenIcon.Stance:
                    // 付録 A スタンス: a flag, the pole and its cloth.
                    return Line(x, y, 8f, 4f, 8f, 28f, 2f) || InBox(x, y, 9f, 17f, 24f, 27f);
                case OmenIcon.Rest:
                    // §4.4 休み: one flat line.
                    return Mathf.Abs(y - 16f) <= 2f && x >= 6 && x <= 26;
                default:
                    return false;
            }
        }

        // ---- primitives (copied from ProceduralArt, whose own are private) ----------------

        private static float Dist(float x, float y, float cx, float cy)
        {
            float dx = x - cx, dy = y - cy;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static bool InBox(float x, float y, float x0, float y0, float x1, float y1)
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

        private static Sprite Make(Func<int, int, bool> inside)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    pixels[y * Size + x] = new Color(1f, 1f, 1f, inside(x, y) ? 1f : 0f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
#endif
