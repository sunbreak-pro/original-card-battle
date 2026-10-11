// #455: the terms the bridge marks in a card's trait box for battle-visual-v1 §7.1's dotted underline
// and hover definition — which characters of which row, and the §7.5 meaning. Runs under `dotnet test`
// (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class TermMarkTests
    {
        private static CardFace FaceOf(CardDef def) => CoreText.FaceOf(def);

        private static string Printed(CardFace face, TermMark mark)
        {
            string row = mark.Row == TermRow.TraitTop ? face.TraitTop : face.TraitBottom;
            return row.Substring(mark.Start, mark.Length);
        }

        [Test]
        public void BodyCheck_UnderlinesTheGapInItsCondition_AndTheHeavyBlowInItsEffect()
        {
            CardFace face = FaceOf(CardCatalog.BodyCheck);
            Assert.That(face.TraitTop, Is.EqualTo("間合い 0"));
            Assert.That(face.TraitBottom, Is.EqualTo("→ 重撃"));
            Assert.That(face.Terms.Select(m => m.Row + ":" + Printed(face, m)), Is.EqualTo(new[] { "TraitTop:間合い", "TraitBottom:重撃" }));
            TermMark heavy = face.Terms.Single(m => m.Word == "重撃");
            Assert.That(heavy.Start, Is.EqualTo(2));
            Assert.That(heavy.Meaning, Is.EqualTo(CardFaceText.Glossary["重撃"]));
        }

        [Test]
        public void Brace_UnderlinesReserve()
        {
            CardFace face = FaceOf(CardCatalog.Brace);
            TermMark reserve = face.Terms.Single();
            Assert.That(reserve.Word, Is.EqualTo("温存"));
            Assert.That(reserve.Row, Is.EqualTo(TermRow.TraitTop));
            Assert.That(reserve.Start, Is.EqualTo(0));
            Assert.That(Printed(face, reserve), Is.EqualTo("温存"));
        }

        [Test]
        public void Lunge_UnderlinesCombo_UnderItsGlossaryWord()
        {
            CardFace face = FaceOf(CardCatalog.Lunge);
            TermMark combo = face.Terms.Single(m => m.Word == "連動 X");
            Assert.That(Printed(face, combo), Is.EqualTo("連動"), "§7.5 連動 X is printed 連動 スタンス");
            Assert.That(combo.Meaning, Does.Contain("属性 X"));
        }

        [Test]
        public void APlainCard_UnderlinesItsName_InTheBox()
        {
            CardFace face = FaceOf(CardCatalog.Thrust);
            Assert.That(face.Plain, Is.True);
            TermMark plain = face.Terms.Single();
            Assert.That(plain.Word, Is.EqualTo("素直な札"));
            Assert.That(Printed(face, plain), Is.EqualTo("素直な札"));
        }

        [Test]
        public void AStanceCard_WithNoTermInItsBox_MarksNothingThere()
        {
            CardFace face = FaceOf(CardCatalog.RockStance);
            foreach (TermMark mark in face.Terms)
            {
                Assert.That(Printed(face, mark), Is.Not.Empty);
                Assert.That(CardFaceText.Glossary.ContainsKey(mark.Word), Is.True);
            }
        }

        [Test]
        public void EveryMark_InTheCatalog_PointsAtItsPrintedWord_AndNoTwoOverlap()
        {
            foreach (CardDef def in CardCatalog.All)
            {
                CardFace face = FaceOf(def);
                foreach (TermMark mark in face.Terms)
                {
                    string printed = Printed(face, mark);
                    Assert.That(CardFaceText.PrintedForms(mark.Word), Does.Contain(printed), def.Name + ": " + mark.Word);
                    Assert.That(mark.Meaning, Is.Not.Empty, def.Name);
                }
                foreach (TermMark a in face.Terms)
                {
                    foreach (TermMark b in face.Terms)
                    {
                        if (ReferenceEquals(a, b) || a.Row != b.Row) continue;
                        bool overlap = a.Start < b.Start + b.Length && b.Start < a.Start + a.Length;
                        Assert.That(overlap, Is.False, def.Name + ": " + a.Word + " / " + b.Word);
                    }
                }
            }
        }
    }
}
