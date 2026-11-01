// #455: the order and the places of battle-visual-v1 §4.6's stance lists (StanceList). Runs under
// `dotnet test` (Depiction.Script.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class StanceListTests
    {
        private static string[] Shown(IReadOnlyList<string> placed) =>
            StanceList.Rows(placed).Select(r => r.Name + StanceList.CountText(r.Count)).ToArray();

        [Test]
        public void Rows_KeepTheOrderPutDown_TheNewestLast()
        {
            Assert.That(Shown(new[] { "岩の構え", "水の構え", "流れの構え" }), Is.EqualTo(new[] { "岩の構え", "水の構え", "流れの構え" }));
        }

        [Test]
        public void ARepeat_IsOneRowWithItsCount_MovedToTheEnd()
        {
            Assert.That(Shown(new[] { "岩の構え", "水の構え", "岩の構え" }), Is.EqualTo(new[] { "水の構え", "岩の構え×2" }));
            Assert.That(Shown(new[] { "岩の構え", "岩の構え", "岩の構え" }), Is.EqualTo(new[] { "岩の構え×3" }));
        }

        [Test]
        public void NothingHeld_IsNoRow_AndOneEmptyFrame()
        {
            Assert.That(StanceList.Rows(new string[0]), Is.Empty);
            Assert.That(StanceList.Rows(null), Is.Empty);
            Assert.That(StanceList.Rows(new[] { "", null }), Is.Empty, "a source with no name is left out");
            Assert.That(StanceList.Frames(0), Is.EqualTo(1), "§4.6 空きの枠");
        }

        [TestCase(1, 1, 0)]
        [TestCase(3, 3, 0)]
        [TestCase(4, 3, 2)]
        [TestCase(7, 3, 5)]
        public void ALongList_FoldsItsOldestIntoOneFrameOnTop(int rows, int frames, int folded)
        {
            Assert.That(StanceList.Frames(rows), Is.EqualTo(frames));
            Assert.That(StanceList.Folded(rows), Is.EqualTo(folded));
            if (folded > 0) Assert.That(folded + (frames - 1), Is.EqualTo(rows), "the frames below the fold show the newest");
        }

        [Test]
        public void TheFrames_StandOnTheSection46Slot_AndStackUpward()
        {
            Assert.That(StanceList.LeftOf(UnitSide.Player), Is.EqualTo(24f));
            Assert.That(StanceList.LeftOf(UnitSide.Enemy) + StanceList.Width, Is.EqualTo(1920f - 24f), "右から 24");
            Assert.That(StanceList.TopOf(0), Is.EqualTo(470f));
            Assert.That(StanceList.Width, Is.EqualTo(250f));
            Assert.That(StanceList.RowHeight, Is.EqualTo(52f));
            Assert.That(StanceList.TopOf(1), Is.EqualTo(470f - 58f));
            // The lowest frame ends at y 522, clear of the floor's 40 px cells from y 560 (§4.2).
            Assert.That(StanceList.TopOf(0) + StanceList.RowHeight, Is.LessThan(560f));
        }

        [Test]
        public void TheCount_IsWrittenFromTwoOn()
        {
            Assert.That(StanceList.CountText(1), Is.EqualTo(""));
            Assert.That(StanceList.CountText(2), Is.EqualTo("×2"));
        }

        // ---- the hover panel (§4.6 / §7.3 永続の状態, #605) ----

        private static StanceRow Row(string name, int count, string effect) =>
            new StanceRow { Name = name, Count = count, Effect = effect, Decay = "減らない（永続）" };

        [Test]
        public void ThePanel_HeadsWithTheNameAndCount_AndWritesTheRowsText()
        {
            Assert.That(StanceList.PanelTitle(Row("岩の構え", 2, "")), Is.EqualTo("岩の構え ×2"));
            Assert.That(StanceList.PanelTitle(Row("岩の構え", 1, "")), Is.EqualTo("岩の構え"), "no ×1");
            Assert.That(StanceList.PanelGroup, Is.EqualTo("永続"));
            Assert.That(StanceList.PanelBody(Row("岩の構え", 1, "毎ターン開始に Guard +3")),
                Is.EqualTo("効き方　毎ターン開始に Guard +3\n減り方　減らない（永続）"));
        }

        [Test]
        public void TheFoldedPanel_ListsTheFoldedStances_InTheOrderPutDown()
        {
            var rows = new List<StanceRow>
            {
                Row("岩の構え", 2, "A"), Row("水の構え", 1, "B"), Row("流れの構え", 1, "C"), Row("霞み足", 1, "D"),
            };
            int folded = StanceList.Folded(rows.Count);
            Assert.That(StanceList.FoldedLines(rows, folded), Is.EqualTo(new[] { "岩の構え ×2：A", "水の構え：B" }));
            Assert.That(StanceList.FoldedLines(rows, 0), Is.Empty);
            Assert.That(StanceList.FoldedLines(null, 2), Is.Empty);
        }

        [Test]
        public void ThePanel_StandsBesideTheFrame_InsideTheScreen()
        {
            // The player's to the right of its frames, the enemy's to the left, 8 px apart.
            Assert.That(StanceList.PanelLeft(UnitSide.Player), Is.EqualTo(24f + 250f + 8f));
            Assert.That(StanceList.PanelLeft(UnitSide.Enemy) + StanceList.PanelWidth, Is.EqualTo(StanceList.LeftOf(UnitSide.Enemy) - 8f));
            Assert.That(StanceList.PanelLeft(UnitSide.Enemy), Is.GreaterThanOrEqualTo(0f));
            // Level with the frame's top while it fits ...
            for (int i = 0; i < StanceList.MaxRows; i++) Assert.That(StanceList.PanelTop(i, 120f), Is.EqualTo(StanceList.TopOf(i)));
            // ... and raised so a long "+n" list still ends 16 px above the screen's bottom.
            Assert.That(StanceList.PanelTop(0, 800f) + 800f, Is.EqualTo(1080f - 16f));
            Assert.That(StanceList.PanelTop(0, 2000f), Is.EqualTo(16f), "never above the top");
        }
    }
}
