// #210: the eighty cards' names and trait lines inside the Card prefab's boxes; #567: their descriptions. The measure is a
// stand-in wider than the prefab's font (Arial with a Japanese fallback): a Japanese letter one em,
// a space 0.3 em, any other letter 0.6 em (Arial's "Guard+3" runs 3.9 em, this 4.2), a line 1.2 em high.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class CardFaceFitTests
    {
        // The Card prefab (DepictionPrefabBuilder): the name box and its size, the trait text's box
        // and its size. The trait text starts 40 px in and CardView.traitVisibleRight cuts it at 150.
        private const int NameSize = 24;
        private const float NameWidth = 100f;
        private const float NameHeight = 40f;
        private const int TraitSize = 22;
        private const float TraitWidth = 150f - 40f;
        private const float TraitHeight = 36f;
        private const int MinSize = 12;

        private static void StandIn(string text, int size, out float width, out float height)
        {
            string[] lines = text.Split('\n');
            float widest = 0f;
            foreach (string line in lines)
            {
                float ems = line.Sum(c => c >= '⺀' ? 1f : c == ' ' ? 0.3f : 0.6f);
                widest = Math.Max(widest, ems * size);
            }
            width = widest;
            height = lines.Length * 1.2f * size;
        }

        [Test]
        public void EveryName_FitsOnOneLine_AboveTheTypeLine()
        {
            Assert.That(CardCatalog.All.Count, Is.EqualTo(73));
            var misfits = new List<string>();
            foreach (CardDef def in CardCatalog.All)
            {
                TextFit fit = CardTextFit.Name(def.Name, NameSize, MinSize, NameWidth, NameHeight, StandIn);
                if (!fit.Fits || fit.Text.Contains("\n") || fit.Size < 16) misfits.Add(def.Name + " at " + fit.Size);
            }
            Assert.That(misfits, Is.Empty);
        }

        [Test]
        public void TheLongestNames_ShrinkButStayReadable()
        {
            int longest = CardCatalog.All.Max(c => c.Name.Length);
            foreach (CardDef def in CardCatalog.All.Where(c => c.Name.Length == longest))
            {
                Assert.That(CardTextFit.Name(def.Name, NameSize, MinSize, NameWidth, NameHeight, StandIn).Size, Is.EqualTo(16), def.Name);
            }
        }

        [Test]
        public void EveryTraitLine_FitsBeforeTheRightNeighbour()
        {
            var misfits = new List<string>();
            foreach (CardDef def in CardCatalog.All)
            {
                string line = CoreText.TraitLines(def);
                if (line.Length == 0) continue;
                TextFit fit = CardTextFit.Trait(line, TraitSize, MinSize, TraitWidth, TraitHeight, StandIn);
                if (!fit.Fits || fit.Size < 15) misfits.Add(def.Name + " 「" + line + "」 at " + fit.Size);
            }
            Assert.That(misfits, Is.Empty);
        }

        [Test]
        public void ACardWithTwoTraits_PrintsThem_OnePerLine()
        {
            // No catalog card carries two traits since v4.10 dropped #80 背水の陣 (#824); the core still can.
            var twoTraits = new CardDef("two_traits", "二つの特性", BattleAttribute.Attack, 1, new Face(Power: 8, Reach: new Reach(1, 2)),
                new BattleCore.Trait(BattleCore.TraitCondition.GapAtLeast, BattleCore.TraitEffect.PowerBonus, 5, Threshold: 2),
                ExtraTrait: new BattleCore.Trait(BattleCore.TraitCondition.Desperate, BattleCore.TraitEffect.PowerBonus, 3));
            TextFit fit = CardTextFit.Trait(CoreText.TraitLines(twoTraits), TraitSize, MinSize, TraitWidth, TraitHeight, StandIn);

            Assert.That(fit.Text, Is.EqualTo("間合い2以上 +5\n死力 +3"));
        }

        [Test]
        public void BodyCheck_KeepsItsHeavyBlowOnOneLine()
        {
            TextFit fit = CardTextFit.Trait(CoreText.TraitLines(CardCatalog.ById("body_check")), TraitSize, MinSize, TraitWidth, TraitHeight, StandIn);

            Assert.That(fit.Text, Is.EqualTo("間合い0 重撃"));
            Assert.That(fit.Size, Is.EqualTo(18));
        }

        // #567: the description box (CardView): 188 px wide, three 22 px lines at 16 px (a lasting
        // card's four). A line is 22 / 16 of the size high, as the Text's lineSpacing makes it.
        private const int TextSize = 16;
        private const float TextWidth = 216f - 16f - 12f;
        private const float TextHeight = 22f * 3f;
        private const float LastingHeight = 22f * 4f;

        private static void DescriptionStandIn(string text, int size, out float width, out float height)
        {
            StandIn(text.Replace(CardTextFit.LastingMark, "　"), size, out width, out _);
            height = text.Split('\n').Length * (22f / 16f) * size;
        }

        private static TextFit DescriptionOf(CardDef def)
        {
            CardFace face = CoreText.FaceOf(def);
            float height = CardTextFit.PrintsLasting(face) ? LastingHeight : TextHeight;
            return CardTextFit.Description(CardTextFit.DescriptionRows(face), TextSize, MinSize, TextWidth, height, DescriptionStandIn);
        }

        [Test]
        public void EveryDescription_FitsItsBox_WithNoLineOpeningOnPunctuation()
        {
            Assert.That(CardCatalog.All.Count, Is.EqualTo(73));
            var misfits = new List<string>();
            foreach (CardDef def in CardCatalog.All)
            {
                TextFit fit = DescriptionOf(def);
                if (!fit.Fits) misfits.Add(def.Id + " " + def.Name + " 「" + fit.Text.Replace("\n", " / ") + "」");
                foreach (string line in fit.Text.Split('\n'))
                {
                    if (line.Length > 0 && CardTextFit.NoLineStart.IndexOf(line[0]) >= 0) misfits.Add(def.Id + " opens a line with 「" + line + "」");
                }
            }
            Assert.That(misfits, Is.Empty);
        }

        [Test]
        public void TheLongDescriptions_Wrap_AsPinned()
        {
            // #769: 深淵の構え now reads on one line at the full size.
            TextFit abyss = DescriptionOf(CardCatalog.ById("abyss_stance"));
            Assert.That(abyss.Text, Is.EqualTo("∞ 間合い 0 で 威力 +5"));
            Assert.That(abyss.Size, Is.EqualTo(16));
            // Too long for one line even at 12: wrapped at 16 inside a lasting card's four lines.
            TextFit mist = DescriptionOf(CardCatalog.ById("mist_step"));
            Assert.That(mist.Text.Split('\n').Length, Is.EqualTo(3));
            Assert.That(mist.Size, Is.EqualTo(16));
        }

        // #722: the five cards the #567 measure printed below 16 px (12, 13, 14, 14 and 14) now hold the
        // full size; three are left since v4.10 dropped 流れの構え and 岩の構え (#824). battle-visual-v1
        // §3.1 keeps the shrinking as a guard, not as these cards' size.
        [TestCase("wolf_stance", "∞ 出血中の敵へ 威力 +3")]
        [TestCase("root_bind", "∞ 敵が自ら動くと崩し 1")]
        [TestCase("twist_away", "敵に 鈍足 2・疲労 2\n敵に 威圧 2")]
        public void TheFiveShortenedDescriptions_FitAt16Px(string id, string printed)
        {
            TextFit fit = DescriptionOf(CardCatalog.ById(id));

            Assert.That(fit.Fits, Is.True);
            Assert.That(fit.Size, Is.EqualTo(16));
            Assert.That(fit.Text, Is.EqualTo(printed));
        }

        // The deck screen prints the catalog with the hand's face (no dealt instance behind it).
        [Test]
        public void EveryCatalogCard_HasAFaceForTheDeckScreen_WithItsOwnIdNameAndPrintedCost()
        {
            foreach (CardDef def in CardCatalog.All)
            {
                CardFace face = CoreText.FaceOf(def);
                Assert.That(face.Id, Is.EqualTo(def.Id), def.Id);
                Assert.That(face.Name, Is.EqualTo(def.Name), def.Id);
                Assert.That(face.Cost, Is.EqualTo(def.Cost), def.Id);
                Assert.That(face.TypeLabel, Is.Not.Empty, def.Id);
                Assert.That(face.TraitLit, Is.False, def.Id);
            }
        }
    }
}
