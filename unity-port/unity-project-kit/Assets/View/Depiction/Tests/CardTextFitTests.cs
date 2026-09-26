using NUnit.Framework;

namespace Depiction.Tests
{
    public class CardTextFitTests
    {
        // Every character one em wide and every line one em high: the arithmetic, not a font.
        private static void Ems(string text, int size, out float width, out float height)
        {
            string[] lines = text.Split('\n');
            int widest = 0;
            foreach (string line in lines) widest = System.Math.Max(widest, line.Length);
            width = widest * size;
            height = lines.Length * size;
        }

        [Test]
        public void AName_TakesTheLargestSizeThatStaysInItsBox()
        {
            TextFit fit = CardTextFit.Name("呼吸を整える", 24, 12, 100f, 40f, Ems);

            Assert.That(fit.Text, Is.EqualTo("呼吸を整える"), "a name never breaks");
            Assert.That(fit.Size, Is.EqualTo(16), "6 × 16 = 96 fits 100, 6 × 17 does not");
            Assert.That(fit.Fits, Is.True);
        }

        [Test]
        public void AShortName_KeepsThePrefabSize()
        {
            Assert.That(CardTextFit.Name("突き", 24, 12, 100f, 40f, Ems).Size, Is.EqualTo(24));
        }

        [Test]
        public void ANameTooLongForTheSmallestSize_SaysSo_AtTheSmallestSize()
        {
            TextFit fit = CardTextFit.Name("あいうえおかきくけこ", 24, 12, 100f, 40f, Ems);

            Assert.That(fit.Fits, Is.False);
            Assert.That(fit.Size, Is.EqualTo(12));
        }

        [Test]
        public void TwoTraits_BreakBetweenThem_AndOneTrait_BetweenItsConditionAndEffect()
        {
            Assert.That(CardTextFit.TwoLines("間合い2以上 +5 ／ 死力 +3"), Is.EqualTo("間合い2以上 +5\n死力 +3"));
            Assert.That(CardTextFit.TwoLines("間合い0 スタミナ+1"), Is.EqualTo("間合い0\nスタミナ+1"));
            Assert.That(CardTextFit.TwoLines("転換"), Is.Null);
        }

        [Test]
        public void ATraitLine_GoesOnTwoLines_OnlyWhenTheyPrintLarger()
        {
            // One line of 11 fits 110 at 10; two lines of 5 fit 110 × 36 at 18.
            TextFit two = CardTextFit.Trait("あいうえお かきくけこ", 22, 8, 110f, 36f, Ems);
            Assert.That(two.Text, Is.EqualTo("あいうえお\nかきくけこ"));
            Assert.That(two.Size, Is.EqualTo(18));

            // One line of 5 fits at 22 already.
            TextFit one = CardTextFit.Trait("あい うえ", 22, 8, 110f, 36f, Ems);
            Assert.That(one.Text, Is.EqualTo("あい うえ"));
            Assert.That(one.Size, Is.EqualTo(22));
        }

        [Test]
        public void ATie_KeepsOneLine()
        {
            // One line of 6 fits 108 at 18; two lines of 3 and 2 fit 108 × 36 at 18 as well.
            TextFit fit = CardTextFit.Trait("あいう えお", 22, 8, 108f, 36f, Ems);

            Assert.That(fit.Text, Is.EqualTo("あいう えお"));
            Assert.That(fit.Size, Is.EqualTo(18));
        }
    }
}
