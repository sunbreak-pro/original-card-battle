// The last review round's fixes that are not covered elsewhere:
//   X-15 MasteryView: a card's name at each 段 and its 刻み toward the next one (the rank-up lines, the training card);
//   X-17 the attribute words the screens print (DeckBuilder.Shown*, the 用語 page's スタンス row);
//   the scene's rank-up line names a renamed card before and after.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { MasteryBook } from '../../src/core/battle/Mastery.js';
import { Constants } from '../../src/core/battle/Constants.js';
import { CardCatalog } from '../../src/core/battle/CardCatalog.js';
import { BattleAttribute } from '../../src/core/battle/Types.js';
import { nameAtRank, masteryProgress } from '../../src/bridge/exploration/MasteryView.js';
import { DeckBuilder } from '../../src/bridge/depiction/DeckBuilder.js';
import { CardFaceText } from '../../src/bridge/depiction/CardFaceText.js';
import { JournalTerms } from '../../src/bridge/journal/JournalTerms.js';
import { withStanceTerm } from '../../src/scenes/meta/journal.js';
import { MetaText } from '../../src/scenes/meta/text.js';
import { SceneText } from '../../src/scenes/explore/text.js';

/** A book after `ticks` training marks on each card id given. */
function trained(marks) {
  let book = MasteryBook.Begin(null);
  for (const [id, n] of marks) for (let i = 0; i < n; i++) book = book.Train(id).Book;
  return book;
}

test('X-15 nameAtRank: the catalogue name at 0, the profile\'s name at each 段', () => {
  const book = MasteryBook.Begin(null);
  assert.equal(nameAtRank(book, 'thrust', 0), CardCatalog.ById('thrust').Name);
  assert.equal(nameAtRank(book, 'thrust', 1), '鋭い突き');
  assert.equal(nameAtRank(book, 'thrust', 2), '穿ち突き');
  assert.equal(nameAtRank(book, 'no_such_card', 1), 'no_such_card');
});

test('X-15 masteryProgress: the 刻み toward the next 段 and the names around it', () => {
  const [t1, t2] = Constants.MasteryThresholds;
  const fresh = masteryProgress(MasteryBook.Begin(null), 'thrust');
  assert.deepEqual([fresh.Rank, fresh.Ticks, fresh.Need, fresh.Grows, fresh.Top, fresh.Capped], [0, 0, t1, true, false, false]);
  assert.equal(fresh.Name, fresh.BaseName);
  assert.equal(fresh.NextName, '鋭い突き');
  const one = masteryProgress(trained([['thrust', t1 + 1]]), 'thrust');
  assert.deepEqual([one.Rank, one.Ticks, one.Need], [1, t1 + 1, t2]);
  assert.equal(one.Name, '鋭い突き');
  assert.equal(one.BaseName, CardCatalog.ById('thrust').Name);
  assert.equal(one.NextName, '穿ち突き');
});

test('X-15 masteryProgress: no next 段 at the top, nor at 2 while another card holds the life\'s 3rd 段', () => {
  const top = Constants.MasteryThresholds[2];
  const book = trained([['thrust', top], ['kesa_cut', top]]);
  const holder = book.TopRankHolder;
  const other = holder === 'thrust' ? 'kesa_cut' : 'thrust';
  const held = masteryProgress(book, holder);
  assert.deepEqual([held.Rank, held.Top, held.Need, held.NextName], [3, true, null, null]);
  const stopped = masteryProgress(book, other);
  assert.deepEqual([stopped.Rank, stopped.Capped, stopped.Need, stopped.NextName], [2, true, null, null]);
});

test('X-15 the rank-up line names a renamed card before and after; an unrenamed one once', () => {
  assert.equal(SceneText.RankUp('鋭い突き', 0, 1, '突き'), '突き → 鋭い突き（0 → 1 段目）');
  assert.equal(SceneText.RankUp('突き', 1, 2, '突き'), '突き　1 → 2 段目');
  assert.equal(SceneText.RankUp('突き', 1, 2), '突き　1 → 2 段目');
});

test('X-17 the deck builder prints the battle\'s attribute words; the C# words stay as the C# writes them', () => {
  assert.deepEqual(DeckBuilder.ShownAttributeOptions.map((o) => o.Key), ['全て', '攻撃', '防御', 'スキル', 'スタンス']);
  assert.deepEqual(DeckBuilder.ShownAttributeOptions.map((o) => o.Value), DeckBuilder.AttributeOptions.map((o) => o.Value));
  assert.deepEqual(DeckBuilder.AttributeOptions.map((o) => o.Key), ['全て', '攻撃', '防御', '技', '構え']);
  for (const flag of [BattleAttribute.Attack, BattleAttribute.Guard, BattleAttribute.Skill, BattleAttribute.Stance]) {
    assert.equal(DeckBuilder.ShownAttributeWords(flag), CardFaceText.AttributeWord(flag), 'the battle\'s word');
  }
  for (const def of CardCatalog.All) {
    const shown = DeckBuilder.ShownDetailOf(def);
    const plain = DeckBuilder.DetailOf(def);
    assert.equal(shown.length, plain.length);
    assert.ok(shown[1].startsWith('コスト ' + def.Cost + '　' + DeckBuilder.ShownAttributeWords(def.Attribute)), def.Id);
    assert.deepEqual(shown.slice(2), plain.slice(2));
    assert.equal(shown[1].replace(DeckBuilder.ShownAttributeWords(def.Attribute), DeckBuilder.AttributeWords(def.Attribute)), plain[1]);
  }
});

test('X-17 the 用語 page shows スタンス under 構え, once; the ported page is the C#\'s', () => {
  const ported = JournalTerms.Page();
  assert.ok(!ported.Rows.some((r) => r.Label === MetaText.StanceTerm));
  const page = withStanceTerm(ported);
  const labels = page.Rows.map((r) => r.Label);
  assert.equal(labels[labels.indexOf('構え') + 1], MetaText.StanceTerm);
  assert.equal(page.Rows.length, ported.Rows.length + 1);
  assert.equal(withStanceTerm(page).Rows.length, page.Rows.length, 'once');
  assert.deepEqual([page.Id, page.Tab, page.Title], [ported.Id, ported.Tab, ported.Title]);
});
