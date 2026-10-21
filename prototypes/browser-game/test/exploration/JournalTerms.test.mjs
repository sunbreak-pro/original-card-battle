// Port of the JournalTerms cases in unity-project-kit/Assets/View/Exploration/Tests/JournalBookTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { JournalRow, JournalTab } from '../../src/bridge/journal/JournalBook.js';
import { JournalTerms } from '../../src/bridge/journal/JournalTerms.js';

test('ThePageOfTermsHasTermsAboveStatusesAndHidesNothing', () => {
  // battle-visual-v1.md §7.5 has 16 terms and §7.3 ten statuses; every row reads from the first life.
  const page = JournalTerms.Page();
  assert.equal(page.Id, 'terms');
  assert.equal(page.Tab, JournalTab.Terms);
  assert.equal(page.Title, '用語と状態');
  assert.equal(page.Rows[0].Label, JournalTerms.TermsHeading);
  assert.equal(page.Rows.map((r) => r.Label).indexOf(JournalTerms.StatusesHeading), 1 + 16);
  assert.equal(page.Rows.length, 2 + 16 + 10);
  assert.ok(!page.Rows.map((r) => page.TextOf(r)).includes(JournalRow.Hidden));
  assert.ok(page.Rows.every((r) => r.NeedsDisclosure === 0));
  assert.equal(page.DisclosureLabel, '');
});

test('EachStatusSaysWhoCarriesItHowItActsAndHowItWearsOff', () => {
  const bleed = JournalTerms.Statuses[0];
  assert.equal(bleed.Label, '出血（相手に付ける）');
  assert.equal(bleed.Text, '効き方: 相手のターン開始に HP −2×スタック\n減り方: ターンごとに 1 減る');
  assert.equal(JournalTerms.Statuses.filter((s) => s.Label.endsWith('（自分に付ける）')).length, 5);
});

test('TheWordsAreTheCanonsByteForByte', () => {
  assert.equal(JournalTerms.TermsHeading, '【用語】');
  assert.equal(JournalTerms.StatusesHeading, '【状態】');
  assert.deepEqual(JournalTerms.Terms.map((r) => r.Label), [
    '間合い', '構え', '崩し n', '素直な札', '初手', '温存', '連打', '連動 X', '手薄', '重撃', '転換', '追撃', '移動後', '永続',
    '詰める / 離れる', '押す / 引く',
  ]);
  assert.equal(JournalTerms.Terms[1].Text, 'ターン終了のときスタミナが 3 以上残っていれば Guard +3');
  const sight = JournalTerms.Statuses.find((s) => s.Label.startsWith('見切り'));
  assert.equal(sight.Label, '見切り（自分に付ける）');
  assert.equal(sight.Text, '効き方: 次に攻撃を受けたとき、Guard で防いだ分の半分を相手に返す\n減り方: 効くと 1 減る（Guard 0 で受けたときは減らない）');
  // The body: headings print alone, the others label over text.
  const body = JournalTerms.Page().Body;
  assert.ok(body.startsWith('【用語】\n\n間合い\n自分と相手のあいだの空きマスの数。0 は隣り合っている\n\n'));
  assert.ok(body.endsWith('再生（自分に付ける）\n効き方: ターン開始に HP +2×スタック\n減り方: ターンごとに 1 減る'));
});
