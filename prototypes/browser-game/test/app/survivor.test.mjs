// 生存ルートでは手記を丸ごと受け継ぐ (concept-v3 §8.2, §16.2; tier1 R1-13 「生存ルート後の次の生で、前の手記が
// 全頁そのまま読める」; INHERITANCE.md R-13): the survivor's 開示度, rare pages, 走り書き and memo reach the
// heir's life through the app (makeStart → lifeSetup → the router's memo and 走り書き), and they are in the
// journal that life leaves at its own death, so they are not lost for the heirs after it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { Loadout } from '../../src/core/dungeonContent/Loadout.js';
import { RunPhase } from '../../src/core/dungeon/ExplorationState.js';
import * as Inh from '../../src/core/inheritance/Inheritance.js';
import { recordExpedition, replayLife } from '../../src/app/recorder.js';
import { lifeSetup, makeStart, lifeScribbles, startPages } from '../../src/app/life.js';
import { lifeJournal } from '../../src/app/names.js';

const departure = (ls) => ({ deck: PrototypeDeck.Build(), battleToolIds: [], loadout: Loadout.Of([], [], ls.ToolSlots), talent: null });
const clone = (x) => JSON.parse(JSON.stringify(x));
const MEMO = '八代目のメモ。生きて戻る。';
const OLD = { Generation: 1, Name: 'ミナト', Text: '一代目の走り書き' };

/** gen 1 dies (no memo), gen 2 survives on layer 4 with a memo, 4 rare pages, 2 enemies known and an inherited 走り書き. */
function survivorLineage() {
  let l = Inh.newLineage(21);
  let s = Inh.startLife(l, 1n);
  l = Inh.recordDeath(s.Lineage, s.Life, { Layer: 1, NodeId: 3, Ending: RunPhase.Fallen }, [], { Disclosure: {}, Pages: 0, Memo: '' });
  s = Inh.startLife(l, 2n);
  const journal = { Disclosure: { shadow_hound: 2, rust_spear: 1 }, Pages: 4, Memo: MEMO, Scribbles: [OLD] };
  l = Inh.recordSurvival(s.Lineage, s.Life, { Layer: 4, NodeId: 2, Ending: RunPhase.Survived }, [], journal);
  return { lineage: l, survivor: s.Life };
}

test('heirJournal: the memo becomes the writer\'s 走り書き, the rest passes as it is', () => {
  const j = Inh.heirJournal({ Disclosure: { a: 1 }, Pages: 2, Memo: MEMO, Scribbles: [OLD] }, { Generation: 8, Name: 'ハル' });
  assert.equal(j.Memo, '', 'the heir starts on a sheet of their own');
  assert.deepEqual(j.Scribbles, [OLD, { Generation: 8, Name: 'ハル', Text: MEMO }]);
  assert.deepEqual(j.Disclosure, { a: 1 });
  assert.equal(j.Pages.length, 2);
  assert.ok(Object.isFrozen(j));
  // nothing to move: as it is
  assert.deepEqual(Inh.heirJournal({ Disclosure: {}, Pages: 0, Memo: '  ' }, { Generation: 2, Name: 'x' }).Scribbles, []);
  assert.equal(Inh.heirJournal({ Disclosure: {}, Pages: 0, Memo: 'm' }, null).Memo, 'm', 'no writer to name: unchanged');
  // the same memo twice is one 走り書き
  assert.equal(Inh.heirJournal(j.Scribbles.length ? { ...j, Memo: MEMO } : j, { Generation: 8, Name: 'ハル' }).Scribbles.length, 2);
});

test('a survivor\'s heir sets out with the whole journal: 開示度, rare pages, 走り書き and the memo', () => {
  const { lineage, survivor } = survivorLineage();
  const ls = Inh.startLife(lineage, 3n);
  assert.deepEqual(ls.JournalFrom, { Generation: survivor.Generation, Name: survivor.Name });
  assert.equal(ls.Journal.Memo, MEMO, 'startLife still hands the survivor\'s journal as it was (R-13)');
  const start = clone(makeStart(ls, departure(ls)));
  assert.equal(start.journal.Memo, '', 'the heir\'s own memo starts empty');
  assert.deepEqual(start.journal.Scribbles, [OLD, { Generation: 2, Name: survivor.Name, Text: MEMO }]);
  assert.equal(startPages(start), 4);

  const { expedition: raw } = lifeSetup(start);
  assert.equal(raw.Pages, 4, 'the rare pages are the life\'s from the start (the map\'s chip, the end journal)');
  assert.equal(raw.DisclosureOf('shadow_hound'), 2);
  assert.equal(raw.DisclosureOf('rust_spear'), 1);
  // what the router keeps for the メモ tab: the 走り書き handed down, the memo of the survivor among them
  const scribbles = lifeScribbles(start, null, ls.Lineage);
  assert.deepEqual(scribbles, start.journal.Scribbles);

  // the heir dies at once: the journal left at the death holds every page and every 走り書き
  const live = { memo: '九代目のメモ', scribbles };
  const journal = lifeJournal(raw, live);
  assert.equal(journal.Pages, 4);
  assert.deepEqual(journal.Scribbles, scribbles);
  const dead = Inh.recordDeath(ls.Lineage, null, { Layer: 1, NodeId: 0, Ending: RunPhase.Fallen }, raw.Mastery, journal);
  const trace = dead.Traces[dead.Traces.length - 1];
  const kinds = (k) => trace.Candidates.filter((c) => c.Kind === k);
  assert.equal(kinds('DungeonPage').length, 0, 'the 4 rare pages hold nothing yet (#107): no pick is spent on them');
  assert.equal(kinds('EnemyPage').length, 2);
  assert.deepEqual(kinds('Scribble').map((c) => c.Name), [
    Inh.InheritanceText.ScribbleName(1, OLD.Name),
    Inh.InheritanceText.ScribbleName(2, survivor.Name),
    Inh.InheritanceText.ScribbleName(ls.Life.Generation, ls.Life.Name),
  ], 'the survivor\'s memo stays theirs; the heir\'s own memo is the heir\'s');
});

test('the start pages stay through a replay and add to the pages a trace gives', () => {
  const { lineage } = survivorLineage();
  const ls = Inh.startLife(lineage, 3n);
  const start = clone(makeStart(ls, departure(ls)));
  const { expedition: raw } = lifeSetup(start);
  const rec = recordExpedition(raw);
  rec.expedition.Inherit({ TraceId: 'trace-x', Cards: [], Journal: { Disclosure: {}, Pages: [{ Id: 'page-1', Title: 'p' }], Scribbles: [] } });
  assert.equal(raw.Pages, 5);
  const { expedition: again } = lifeSetup(clone(start));
  replayLife(again, clone(rec.log));
  assert.equal(again.Pages, 5, 'the replay counts the same pages');
});

test('a start saved before the memo moved still gives the survivor\'s memo back as a 走り書き on resume', () => {
  const { lineage, survivor } = survivorLineage();
  const ls = Inh.startLife(lineage, 3n);
  // the older makeStart: the survivor's journal as it was, memo included
  const old = clone({ ...makeStart(ls, departure(ls)), journal: clone(ls.Journal) });
  const got = lifeScribbles(old, [], ls.Lineage);
  assert.deepEqual(got, [OLD, { Generation: 2, Name: survivor.Name, Text: MEMO }]);
  // united with what the life holds already (a trace's 走り書き), each once
  const taken = { Generation: 1, Name: 'ミナト', Text: '別の走り書き' };
  assert.deepEqual(lifeScribbles(old, [OLD, taken], ls.Lineage), [OLD, { Generation: 2, Name: survivor.Name, Text: MEMO }, taken]);
  assert.equal(startPages(old), 4, 'its pages are counted too');
});
