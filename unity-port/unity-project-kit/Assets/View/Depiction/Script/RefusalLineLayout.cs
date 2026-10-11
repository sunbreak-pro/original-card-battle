// Where the reason a card went back stands, and how big it is (battle-visual-v1 §5.4, #455). Pure C#,
// so `dotnet test` holds the same numbers the screen draws.
using System;

namespace Depiction
{
    /// <summary>
    /// battle-visual-v1 §5.4 出せなかった理由: the one line above the hand that says why a released card
    /// went back, for 3.5 s. 48 px tall, centred on the hand's middle (x 960), its bottom 12 px above the
    /// resting hand's top (y 896, §4.8).
    /// </summary>
    public static class RefusalLineLayout
    {
        public const float Height = 48f;
        public const float CenterX = 960f;
        public const float Bottom = 884f;
        public static float Top => Bottom - Height;

        /// <summary>§5.4: an amber dot at 16 px on the left and the text at 20 px, 16 px in from either end, 10 px apart.</summary>
        public const float DotSize = 16f;
        public const int TextSize = 20;
        public const float Pad = 16f;
        public const float DotGap = 10f;

        /// <summary>§5.4: how long the line stays.</summary>
        public const float Seconds = 3.5f;

        /// <summary>The line's width for a text <paramref name="textWidth"/> px wide.</summary>
        public static float WidthFor(float textWidth)
        {
            return Pad + DotSize + DotGap + Math.Max(0f, textWidth) + Pad;
        }

        /// <summary>The line's left edge on the 1920×1080 screen, centred on the hand.</summary>
        public static float LeftFor(float textWidth)
        {
            return CenterX - WidthFor(textWidth) * 0.5f;
        }
    }
}
