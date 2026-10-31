// Port of the JournalBook cases in unity-project-kit/Assets/View/Exploration/Tests/JournalBookTests.cs:
// hidden rows read 「？」, in battle only the enemy pages and the page of terms open.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ArgumentOutOfRangeException, ArgumentException } from '../../src/shared/cs.js';
import { JournalBook, JournalContext, JournalPage, JournalRow, JournalTab } from '../../src/bridge/journal/JournalBook.js';
import { JournalTerms } from '../../src/bridge/journal/JournalTerms.js';

const Enemy = (disclosure) =>
  new JournalPage('warped', JournalTab.Enemy, '歪み兵',
    [
      new JournalRow('名前', '長柄の歪み兵'),
      new JournalRow('傾向', '間合い 1〜2 で突く', 1),
      new JournalRow('弱点', '引きに弱い', 2),
    ],
    disclosure);

const Dungeon = () => new JournalPage('dungeon-1', JournalTab.Dungeon, '第 1 層', [new JournalRow('刻限', '10')]);

const Memo = () => new JournalPage('memo', JournalTab.Memo, 'メモ', [new JournalRow('', '')]);

test('RowsBeyondTheDisclosureReadAsAQuestionMark', () => {
  const page = Enemy(1);
  assert.equal(page.TextOf(page.Rows[0]), '長柄の歪み兵');
  assert.equal(page.TextOf(page.Rows[1]), '間合い 1〜2 で突く');
  assert.equal(page.TextOf(page.Rows[2]), JournalRow.Hidden);
  assert.equal(JournalRow.Hidden, '？');
  const two = Enemy(2);
  assert.ok(!two.Rows.map((r) => two.TextOf(r)).includes(JournalRow.Hidden));
});

test('OnlyEnemyPagesCarryADisclosureLabel', () => {
  assert.equal(Enemy(1).DisclosureLabel, '開示度 1 / 2');
  assert.equal(Dungeon().DisclosureLabel, '');
  assert.equal(Memo().DisclosureLabel, '');
  assert.equal(Enemy(0).DisclosureLabel, '', 'a page at 0 is a stand-in: real pages start at the encounter');
});

test('InBattleOnlyTheEnemyPagesAndTheTermsOpen', () => {
  const pages = [Enemy(1), Dungeon(), Memo(), JournalTerms.Page()];
  const battle = new JournalBook(pages, JournalContext.Battle);
  const exploring = new JournalBook(pages, JournalContext.Exploration);

  assert.deepEqual(pages.filter((p) => battle.CanOpen(p)).map((p) => p.Tab), [JournalTab.Enemy, JournalTab.Terms]);
  assert.ok(pages.every((p) => exploring.CanOpen(p)));
  assert.equal(battle.ClosingHint, '閉じるとプレイできます');
  assert.equal(exploring.ClosingHint, '');
  assert.equal(battle.CanOpen(null), false);
});

test('FindFallsBackToThePageThatMayOpen', () => {
  const battle = new JournalBook([Dungeon(), Enemy(1)], JournalContext.Battle);
  assert.equal(battle.First.Id, 'warped');
  assert.equal(battle.Find('dungeon-1').Id, 'warped', 'the dungeon page does not open in battle');
  assert.equal(new JournalBook([], JournalContext.Battle).First, null);
  const exploring = new JournalBook([Dungeon(), Enemy(1)], JournalContext.Exploration);
  assert.equal(exploring.Find('warped').Id, 'warped');
  assert.equal(exploring.Find('nope').Id, 'dungeon-1');
});

test('ThePageBodyPutsEachLabelOverItsText', () => {
  assert.equal(Enemy(1).Body, '名前\n長柄の歪み兵\n\n傾向\n間合い 1〜2 で突く\n\n弱点\n？');
  const memo = new JournalPage('memo', JournalTab.Memo, 'メモ', [new JournalRow('', 'メモはまだありません。')]);
  assert.equal(memo.Body, 'メモはまだありません。');
});

test('EveryPageGetsATabNamedForIt', () => {
  assert.equal(JournalBook.TabText(Enemy(1)), '歪み兵');
  assert.equal(JournalBook.TabText(Dungeon()), 'ダンジョン');
  assert.equal(JournalBook.TabText(Memo()), 'メモ');
  assert.equal(JournalBook.TabText(JournalTerms.Page()), '用語');
});

test('TheTabsHaveJapaneseLabels', () => {
  assert.equal(JournalBook.TabLabel(JournalTab.Enemy), '敵');
  assert.equal(JournalBook.TabLabel(JournalTab.Dungeon), 'ダンジョン');
  assert.equal(JournalBook.TabLabel(JournalTab.Memo), 'メモ');
  assert.equal(JournalBook.TabLabel(JournalTab.Terms), '用語');
  assert.throws(() => JournalBook.TabLabel('Nope'), ArgumentOutOfRangeException);
});

test('AHeadingPrintsItsLabelAlone', () => {
  const page = new JournalPage('p', JournalTab.Terms, '用語と状態',
    [new JournalRow('【用語】', ''), new JournalRow('間合い', '空きマスの数')]);
  assert.equal(page.Body, '【用語】\n\n間合い\n空きマスの数');
});

test('DisclosureOutsideZeroToTwoIsRejected', () => {
  assert.throws(() => Enemy(3), ArgumentOutOfRangeException);
  assert.throws(() => new JournalRow('x', 'y', -1), ArgumentOutOfRangeException);
  assert.throws(() => new JournalPage('', JournalTab.Memo, 'x', []), ArgumentException);
  assert.throws(() => new JournalPage(null, JournalTab.Memo, 'x', []), ArgumentException);
});

test('NullsBecomeEmpty_AndTheDefaultsMatchTheCSharp', () => {
  const row = new JournalRow(null, null);
  assert.equal(row.Label, '');
  assert.equal(row.Text, '');
  assert.equal(row.NeedsDisclosure, 0);
  const page = new JournalPage('x', JournalTab.Memo, null, null);
  assert.equal(page.Title, '');
  assert.deepEqual(page.Rows, []);
  assert.equal(page.Disclosure, JournalPage.MaxDisclosure);
  assert.equal(JournalPage.MaxDisclosure, 2);
  assert.deepEqual(new JournalBook(null, JournalContext.Battle).Pages, []);
  assert.equal(JournalBook.Heading, '手記');
  assert.equal(JournalBook.CloseLabel, '閉じる（J）');
  assert.equal(page.TextOf(null), '');
  assert.equal(page.IsHidden(null), false);
});
