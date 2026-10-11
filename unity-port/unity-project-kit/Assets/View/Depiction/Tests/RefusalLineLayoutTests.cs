// #455: battle-visual-v1 §5.4's line for a card that went back (Script/RefusalLineLayout.cs). Runs
// under `dotnet test` (Depiction.Script.Tests.csproj) and in Unity's EditMode runner from the same file.
using NUnit.Framework;

namespace Depiction.Tests
{
    public class RefusalLineLayoutTests
    {
        [Test]
        public void TheLine_Is48Tall_WithA16Dot_And20Text_For3Point5Seconds()
        {
            Assert.That(RefusalLineLayout.Height, Is.EqualTo(48f));
            Assert.That(RefusalLineLayout.DotSize, Is.EqualTo(16f));
            Assert.That(RefusalLineLayout.TextSize, Is.EqualTo(20));
            Assert.That(RefusalLineLayout.Seconds, Is.EqualTo(3.5f));
        }

        [Test]
        public void TheLine_StandsJustAboveTheRestingHand()
        {
            Assert.That(RefusalLineLayout.Bottom, Is.LessThan(896f), "§4.8: the middle card's top is at y 896");
            Assert.That(RefusalLineLayout.Top, Is.GreaterThan(790f), "below the throw line (§5.3, y 790)");
        }

        [TestCase(0f)]
        [TestCase(420f)]
        public void TheLine_IsCentredOnTheHand_AndFitsItsText(float text)
        {
            float width = RefusalLineLayout.WidthFor(text);
            Assert.That(width, Is.EqualTo(16f + 16f + 10f + text + 16f));
            Assert.That(RefusalLineLayout.LeftFor(text) + width * 0.5f, Is.EqualTo(960f));
        }
    }
}
