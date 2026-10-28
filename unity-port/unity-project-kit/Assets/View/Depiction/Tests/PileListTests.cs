// #654: the pile lists' tab words (PileList.TabLabel). A pile kind with no word throws rather than
// reading 山札. Runs under `dotnet test` (Depiction.Script.Tests.csproj) and in Unity's EditMode runner.
using System;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class PileListTests
    {
        [TestCase(PileKind.Draw, "山札")]
        [TestCase(PileKind.Discard, "捨札")]
        [TestCase(PileKind.Exile, "除外")]
        public void TabLabel_NamesEachPile(PileKind pile, string expected)
        {
            Assert.That(PileList.TabLabel(pile), Is.EqualTo(expected));
        }

        [Test]
        public void TabLabel_StopsOnAnUnknownPile()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PileList.TabLabel((PileKind)99));
        }
    }
}
