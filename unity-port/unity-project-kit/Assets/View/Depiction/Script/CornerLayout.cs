// Where the small buttons in the top corners of the battle screen sit (battle-visual-v1 §4.1, #389).
// Pure arithmetic with no engine types, like HandFan, so the EditMode tests can hold it directly:
// the View lives in Assembly-CSharp, which a test asmdef cannot reference. Every box is in pixels on
// the 1920 x 1080 reference canvas, measured from the top-left corner with y growing downwards, the
// way battle-visual-v1 §4 writes its coordinates. The View turns a box into an anchoredPosition.
using System;

namespace Depiction
{
    /// <summary>A box on the 1920 x 1080 canvas: left and top edges, width and height, y counted downwards.</summary>
    public readonly struct ScreenBox
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;

        public ScreenBox(float x, float y, float width, float height)
        {
            if (width < 0f || height < 0f) throw new ArgumentOutOfRangeException(nameof(width), "A box has no negative size.");
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float Right => X + Width;
        public float Bottom => Y + Height;

        /// <summary>True when the two boxes share some area. Boxes that only touch along an edge do not overlap.</summary>
        public bool Overlaps(ScreenBox other) =>
            X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

        /// <summary>The offset of the box's top-right corner from the canvas's top-right corner (anchor and pivot (1, 1)).</summary>
        public float FromRight => CornerLayout.CanvasWidth - Right;

        public override string ToString() => "(" + X + ", " + Y + ", " + Width + " x " + Height + ")";
    }

    /// <summary>The top corners of the battle screen (battle-visual-v1 §4.1).</summary>
    public static class CornerLayout
    {
        public const float CanvasWidth = 1920f;
        public const float CanvasHeight = 1080f;

        /// <summary>The corner info's top and height (x 24, y 20, height 44). Its width follows its contents.</summary>
        public const float CornerInfoTop = 20f;
        public const float CornerInfoHeight = 44f;
        public const float CornerInfoBottom = CornerInfoTop + CornerInfoHeight;

        /// <summary>The journal button: 24 from the right, y 20, 56 x 56. The View does not build it yet.</summary>
        public static readonly ScreenBox JournalButton = new ScreenBox(CanvasWidth - 24f - 56f, 20f, 56f, 56f);

        /// <summary>The space between the journal button and the speed switch.</summary>
        public const float JournalGap = 12f;

        /// <summary>The battle-speed switch: 92 from the right (12 left of the journal button), y 20, 120 x 56.</summary>
        public static readonly ScreenBox SpeedButton = new ScreenBox(JournalButton.X - JournalGap - 120f, 20f, 120f, 56f);

        /// <summary>The switch's side padding, the icon's size and the space between the icon and the number.</summary>
        public const float SpeedPadding = 12f;
        public const float SpeedIconSize = 24f;
        public const float SpeedIconGap = 8f;

        /// <summary>The width left for the number ("×1.25") between the icon and the right padding: 64 px.</summary>
        public static float SpeedNumberWidth => SpeedButton.Width - SpeedPadding - SpeedIconSize - SpeedIconGap - SpeedPadding;

        /// <summary>How far under the switch its hover tooltip starts.</summary>
        public const float SpeedTooltipGap = 8f;

        /// <summary>The tooltip's padding: 8 above and below, 12 left and right.</summary>
        public const float SpeedTooltipPadY = 8f;
        public const float SpeedTooltipPadX = 12f;

        /// <summary>
        /// The hover tooltip of the switch for a line <paramref name="textWidth"/> x <paramref name="textHeight"/>
        /// px: 8 under the switch, its right edge on the switch's right edge, padded 8 / 12.
        /// </summary>
        public static ScreenBox SpeedTooltip(float textWidth, float textHeight)
        {
            float width = textWidth + 2f * SpeedTooltipPadX;
            float height = textHeight + 2f * SpeedTooltipPadY;
            return new ScreenBox(SpeedButton.Right - width, SpeedButton.Bottom + SpeedTooltipGap, width, height);
        }

        /// <summary>
        /// The demo's 「降参する」 (#203): top left, 12 under the corner info, 200 x 56. The top right
        /// belongs to the journal button and the speed switch, and the switch's tooltip opens
        /// leftwards under them; the demo's canvas draws above the battle's, so a button there would
        /// hide the tooltip. Far from the switch, a run of presses on the speed cannot end the battle.
        /// </summary>
        public static readonly ScreenBox DemoSurrender = new ScreenBox(24f, CornerInfoBottom + 12f, 200f, 56f);
    }
}
