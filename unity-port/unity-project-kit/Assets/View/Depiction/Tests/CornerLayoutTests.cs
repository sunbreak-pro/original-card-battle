// The top corners of the battle screen (Script/CornerLayout.cs, #389): the speed switch where
// battle-visual-v1 §4.1 puts it, and the demo's 「降参する」 clear of it, of its tooltip and of the
// journal button.
using NUnit.Framework;

namespace Depiction.Tests
{
    public class CornerLayoutTests
    {
        /// <summary>
        /// A generous bound on the tooltip's line: every character as wide as the 16 px font is tall,
        /// so the real line (half-width digits and spaces among the kana) is narrower.
        /// </summary>
        private static ScreenBox WidestTooltip() =>
            CornerLayout.SpeedTooltip(BattleSpeed.Tooltip.Length * 16f, 16f * 1.5f);

        [Test]
        public void TheSpeedSwitch_IsBattleVisualSection4_1sBox()
        {
            ScreenBox speed = CornerLayout.SpeedButton;
            // 120 x 56 at y 20, 92 from the right: x 1708 to 1828 on the 1920 canvas.
            Assert.That(speed.Width, Is.EqualTo(120f));
            Assert.That(speed.Height, Is.EqualTo(56f));
            Assert.That(speed.Y, Is.EqualTo(20f));
            Assert.That(speed.FromRight, Is.EqualTo(92f));
            Assert.That(speed.X, Is.EqualTo(1708f));
            Assert.That(speed.Right, Is.EqualTo(1828f));

            // The journal button: 24 from the right, y 20, 56 x 56, 12 to the right of the switch.
            ScreenBox journal = CornerLayout.JournalButton;
            Assert.That(journal.FromRight, Is.EqualTo(24f));
            Assert.That(journal.Y, Is.EqualTo(20f));
            Assert.That(journal.Width, Is.EqualTo(56f));
            Assert.That(journal.Height, Is.EqualTo(56f));
            Assert.That(journal.X - speed.Right, Is.EqualTo(12f));
            Assert.That(speed.Overlaps(journal), Is.False);
        }

        [Test]
        public void TheSwitchsInside_IsPaddingIconGapAndNumber()
        {
            // 12 + icon 24 + 8 + number + 12 = 120, which leaves 64 for "×1.25" at 20 px.
            Assert.That(CornerLayout.SpeedNumberWidth, Is.EqualTo(64f));
        }

        [Test]
        public void TheTooltip_Opens8UnderTheSwitch_RightAligned()
        {
            ScreenBox tip = CornerLayout.SpeedTooltip(400f, 24f);
            Assert.That(tip.Y, Is.EqualTo(CornerLayout.SpeedButton.Bottom + 8f));
            Assert.That(tip.Right, Is.EqualTo(CornerLayout.SpeedButton.Right));
            Assert.That(tip.Width, Is.EqualTo(400f + 24f), "12 px each side");
            Assert.That(tip.Height, Is.EqualTo(24f + 16f), "8 px above and below");
        }

        [Test]
        public void TheDemosSurrender_OverlapsNeitherTheJournalNorTheSwitchNorItsTooltip()
        {
            ScreenBox surrender = CornerLayout.DemoSurrender;
            Assert.That(surrender.Overlaps(CornerLayout.JournalButton), Is.False, "journal " + CornerLayout.JournalButton);
            Assert.That(surrender.Overlaps(CornerLayout.SpeedButton), Is.False, "speed " + CornerLayout.SpeedButton);
            Assert.That(surrender.Overlaps(WidestTooltip()), Is.False, "tooltip " + WidestTooltip());
            // Under the corner info, and still on the canvas.
            Assert.That(surrender.Y, Is.GreaterThan(CornerLayout.CornerInfoBottom));
            Assert.That(surrender.X, Is.GreaterThanOrEqualTo(0f));
            Assert.That(surrender.Bottom, Is.LessThan(CornerLayout.CanvasHeight));
            Assert.That(surrender.Width, Is.EqualTo(200f));
            Assert.That(surrender.Height, Is.EqualTo(56f));
        }

        [Test]
        public void Overlaps_CountsSharedAreaOnly()
        {
            var a = new ScreenBox(0f, 0f, 10f, 10f);
            Assert.That(a.Overlaps(new ScreenBox(5f, 5f, 10f, 10f)), Is.True);
            Assert.That(a.Overlaps(new ScreenBox(10f, 0f, 10f, 10f)), Is.False, "touching along an edge");
            Assert.That(a.Overlaps(new ScreenBox(0f, 20f, 10f, 10f)), Is.False);
        }
    }
}
