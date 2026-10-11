// The lower right of the battle screen (battle-visual-v1 §4.9, #455): the four frames of the attributes
// played this turn, and the 構えの予告 left of the end-turn plate. Pure C#, so `dotnet test` holds the
// same numbers the screen draws.
using System;

namespace Depiction
{
    public static class PlayedLayout
    {
        /// <summary>§4.9 出した属性: 攻撃・防御・スキル・スタンス, left to right.</summary>
        public static readonly CardKind[] Order = { CardKind.Attack, CardKind.Guard, CardKind.Skill, CardKind.Stance };

        /// <summary>§4.9: 40×40 frames, 6 px apart, the last one 24 px in from the right, their tops at y 896.</summary>
        public const float Size = 40f;
        public const float Gap = 6f;
        public const float Right = 24f;
        public const float Top = 896f;

        /// <summary>§4.9: the icon in a frame (§6.1's 属性アイコン size).</summary>
        public const float IconSize = 24f;

        /// <summary>§4.9 回数の札: a 20 px circle at the frame's top right, its number at 14 px.</summary>
        public const float BadgeSize = 20f;
        public const int BadgeText = 14;

        /// <summary>§4.9: an attribute not played this turn is steel at 55%.</summary>
        public const float UnplayedAlpha = 0.55f;

        /// <summary>The span of the four frames.</summary>
        public static float SpanWidth => Order.Length * Size + (Order.Length - 1) * Gap;

        /// <summary>The left edge of the <paramref name="index"/>-th frame on the 1920×1080 screen.</summary>
        public static float LeftOf(int index)
        {
            if (index < 0 || index >= Order.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return RefWidth - Right - SpanWidth + index * (Size + Gap);
        }

        private const float RefWidth = 1920f;
    }

    public static class ReserveHintLayout
    {
        /// <summary>§4.9 構えの予告: from x 1560, y 984, 56 tall.</summary>
        public const float Left = 1560f;
        public const float Top = 984f;
        public const float Height = 56f;

        /// <summary>The padding either side: 12 px from the §3.2 spacing scale.</summary>
        public const float Pad = 12f;

        /// <summary>§4.9: the shield at 30 px and "+3" at 24 px, 8 px apart.</summary>
        public const float ShieldSize = 30f;
        public const float TextGap = 8f;
        public const int TextSize = 24;
        public const float TextWidth = 36f;

        /// <summary>Wide enough for the shield and a two-character "+3"; it ends 12 px short of the end-turn plate (x 1672).</summary>
        public static float Width => Pad + ShieldSize + TextGap + TextWidth + Pad;
    }
}
