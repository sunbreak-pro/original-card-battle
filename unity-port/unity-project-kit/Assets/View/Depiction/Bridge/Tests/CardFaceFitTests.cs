// #210: the eighty cards' names and trait lines inside the Card prefab's boxes. The measure is a
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
            Assert.That(CardCatalog.All.Count, Is.EqualTo(80));
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
        public void LastStand_PrintsItsTwoTraits_OnePerLine()
        {
            TextFit fit = CardTextFit.Trait(CoreText.TraitLines(CardCatalog.ById("last_stand")), TraitSize, MinSize, TraitWidth, TraitHeight, StandIn);

            Assert.That(fit.Text, Is.EqualTo("間合い2以上 +5\n死力 +3"));
        }

        [Test]
        public void BodyCheck_KeepsItsHeavyBlowOnOneLine()
        {
            TextFit fit = CardTextFit.Trait(CoreText.TraitLines(CardCatalog.ById("body_check")), TraitSize, MinSize, TraitWidth, TraitHeight, StandIn);

            Assert.That(fit.Text, Is.EqualTo("間合い0 重撃"));
            Assert.That(fit.Size, Is.EqualTo(18));
        }
    }
}
