// The journal drawer's page contract (Assets/View/Journal/Script/), shared with the battle screen:
// hidden rows read 「？」, in battle only the enemy pages open (battle_ui_ux_v2.md, 手記ドロワー).
using System;
using System.Linq;
using Journal;
using NUnit.Framework;

namespace Exploration.Tests
{
    public class JournalBookTests
    {
        private static JournalPage Enemy(int disclosure) =>
            new JournalPage("warped", JournalTab.Enemy, "歪み兵",
                new[]
                {
                    new JournalRow("名前", "長柄の歪み兵"),
                    new JournalRow("傾向", "間合い 1〜2 で突く", needsDisclosure: 1),
                    new JournalRow("弱点", "引きに弱い", needsDisclosure: 2),
                },
                disclosure);

        private static JournalPage Dungeon() =>
            new JournalPage("dungeon-1", JournalTab.Dungeon, "第 1 層", new[] { new JournalRow("刻限", "10") });

        private static JournalPage Memo() =>
            new JournalPage("memo", JournalTab.Memo, "メモ", new[] { new JournalRow("", "") });

        [Test]
        public void RowsBeyondTheDisclosureReadAsAQuestionMark()
        {
            var page = Enemy(1);

            Assert.That(page.TextOf(page.Rows[0]), Is.EqualTo("長柄の歪み兵"));
            Assert.That(page.TextOf(page.Rows[1]), Is.EqualTo("間合い 1〜2 で突く"));
            Assert.That(page.TextOf(page.Rows[2]), Is.EqualTo(JournalRow.Hidden));
            Assert.That(Enemy(2).Rows.Select(Enemy(2).TextOf), Does.Not.Contain(JournalRow.Hidden));
        }

        [Test]
        public void OnlyEnemyPagesCarryADisclosureLabel()
        {
            Assert.That(Enemy(1).DisclosureLabel, Is.EqualTo("開示度 1 / 2"));
            Assert.That(Dungeon().DisclosureLabel, Is.Empty);
            Assert.That(Memo().DisclosureLabel, Is.Empty);
            Assert.That(Enemy(0).DisclosureLabel, Is.Empty, "a page at 0 is a stand-in: real pages start at the encounter");
        }

        [Test]
        public void InBattleOnlyTheEnemyPagesOpen()
        {
            var pages = new[] { Enemy(1), Dungeon(), Memo() };
            var battle = new JournalBook(pages, JournalContext.Battle);
            var exploring = new JournalBook(pages, JournalContext.Exploration);

            Assert.That(battle.Openable.Select(p => p.Tab), Is.EqualTo(new[] { JournalTab.Enemy }));
            Assert.That(exploring.Openable.Count, Is.EqualTo(3));
            Assert.That(battle.ClosingHint, Is.EqualTo("閉じるとプレイできます"));
            Assert.That(exploring.ClosingHint, Is.Empty);
        }

        [Test]
        public void FindFallsBackToThePageThatMayOpen()
        {
            var battle = new JournalBook(new[] { Dungeon(), Enemy(1) }, JournalContext.Battle);

            Assert.That(battle.First.Id, Is.EqualTo("warped"));
            Assert.That(battle.Find("dungeon-1").Id, Is.EqualTo("warped"), "the dungeon page does not open in battle");
            Assert.That(new JournalBook(new JournalPage[0], JournalContext.Battle).First, Is.Null);
        }

        [Test]
        public void ThePageBodyPutsEachLabelOverItsText()
        {
            Assert.That(Enemy(1).Body, Is.EqualTo("名前\n長柄の歪み兵\n\n傾向\n間合い 1〜2 で突く\n\n弱点\n？"));
            var memo = new JournalPage("memo", JournalTab.Memo, "メモ", new[] { new JournalRow("", "メモはまだありません。") });
            Assert.That(memo.Body, Is.EqualTo("メモはまだありません。"));
        }

        [Test]
        public void EveryPageGetsATabNamedForIt()
        {
            Assert.That(JournalBook.TabText(Enemy(1)), Is.EqualTo("歪み兵"));
            Assert.That(JournalBook.TabText(Dungeon()), Is.EqualTo("ダンジョン"));
            Assert.That(JournalBook.TabText(Memo()), Is.EqualTo("メモ"));
        }

        [Test]
        public void TheTabsHaveJapaneseLabels()
        {
            Assert.That(JournalBook.TabLabel(JournalTab.Enemy), Is.EqualTo("敵"));
            Assert.That(JournalBook.TabLabel(JournalTab.Dungeon), Is.EqualTo("ダンジョン"));
            Assert.That(JournalBook.TabLabel(JournalTab.Memo), Is.EqualTo("メモ"));
        }

        [Test]
        public void DisclosureOutsideZeroToTwoIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Enemy(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new JournalRow("x", "y", needsDisclosure: -1));
        }
    }
}
