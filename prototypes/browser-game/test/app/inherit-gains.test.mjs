// What a trace and a survivor's journal give *this* heir, as the trace card, the 受け取った遺産 card, the map and the
// journal show it (docs/EXTENSIONS.md X-11, docs/INHERITANCE.md R-11 / R-13 / R-14 / R-27):
//   - the take (Expedition.prototype.Inherit) returns what it did, and the receipt is written from that: each card at
//     the 段 this life ends with (mergeMastery and the one-card rule on 3 段), each enemy page only above what the
//     journal already knew, a 走り書き the life carries adding nothing;
//   - inherited pages are the life's but not 拾った物 (InheritedPages / FoundPages, the journal's 希少な頁 page);
//   - the hall says when the life that used the survivor's bonus has closed (SurvivorSpent).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { CardMastery } from '../../src/core/battle/Mastery.js';
import { Loadout } from '../../src/core/dungeonContent/Loadout.js';
import { RunPhase } from '../../src/core/dungeon/ExplorationState.js';
import * as Inh from '../../src/core/inheritance/Inheritance.js';
import { recordExpedition, replayLife } from '../../src/app/recorder.js';
import { lifeSetup, makeStart, addScribbles } from '../../src/app/life.js';
import { lifeJournal, cardName, enemyName } from '../../src/app/names.js';
import { traceGains, cardNameAt, TraceGainText } from '../../src/bridge/exploration/TraceGain.js';
import { rarePagesPage } from '../../src/scenes/meta/journal.js';
import { MetaText } from '../../src/scenes/meta/text.js';

const departure = (ls) => ({ deck: PrototypeDeck.Build(), battleToolIds: [], loadout: Loadout.Of([], [], ls.ToolSlots), talent: null });
const clone = (x) => JSON.parse(JSON.stringify(x));
const opts = { cardName, enemyName };
const MEMO = '一代目のメモ';

/**
 * gen 1 dies on layer 3 with feint and thrust at 3 段 (a trace never holds two, but the take must still keep the rule),
 * kesa_cut at 2, brace at 1, three enemies known, 2 rare pages and a memo; gen 2 survives with 4 pages and two enemies
 * known; gen 3 is the heir: the survivor's journal and gen 1's trace on layer 3.
 */
function lineage() {
  let l = Inh.newLineage(77);
  let s = Inh.startLife(l, 1n);
  l = Inh.recordDeath(s.Lineage, s.Life, { Layer: 3, NodeId: 4, NodeKind: 'Battle', Ending: RunPhase.Fallen },
    [{ CardId: 'thrust', Rank: 3 }, { CardId: 'feint', Rank: 3 }, { CardId: 'kesa_cut', Rank: 2 }, { CardId: 'brace', Rank: 1 }],
    { Disclosure: { polearm_warped: 2, shadow_hound: 1, rusted_revenant: 1 }, Pages: 2, Memo: MEMO }, opts);
  s = Inh.startLife(l, 2n);
  l = Inh.recordSurvival(s.Lineage, s.Life, { Layer: 2, NodeId: 3, NodeKind: 'Rest', Ending: RunPhase.Survived }, [],
    { Disclosure: { polearm_warped: 1, rusted_revenant: 2 }, Pages: 4, Memo: '' }, opts);
  return l;
}

/** The heir's life (gen 3), recorded, and the trace node's view as the game shows it on layer 3. */
function heir() {
  const ls = Inh.startLife(lineage(), 3n);
  const start = clone(makeStart(ls, departure(ls)));
  const { expedition: raw } = lifeSetup(start);
  const rec = recordExpedition(raw);
  // the trace's view as the game builds it (the heir stands on layer 3 there; the layer only picks the trace)
  const view = Inh.traceNodeView(ls.Lineage, 3);
  return { ls, start, raw, rec, view };
}

test('R-27 the pages a survivor handed down are the life\'s, inherited, not found', () => {
  const { raw, rec, start } = heir();
  assert.equal(raw.Pages, 4);
  assert.equal(raw.InheritedPages, 4);
  assert.equal(raw.FoundPages, 0, 'nothing found yet: 拾った物 lists no page');
  // a trace's page is inherited too
  rec.expedition.Inherit({ TraceId: 'trace-1', Cards: [], Journal: { Disclosure: {}, Pages: [{ Id: 'page-1', Title: 'p' }], Scribbles: [] } });
  assert.equal(raw.Pages, 5);
  assert.equal(raw.InheritedPages, 5);
  assert.equal(raw.FoundPages, 0);
  // a page found in this life counts as found (the C# adds them to the same count)
  raw._pages += 1;
  assert.equal(raw.FoundPages, 1);
  raw._pages -= 1;
  // the replay holds the same split
  const { expedition: again } = lifeSetup(clone(start));
  replayLife(again, clone(rec.log));
  assert.equal(again.InheritedPages, 5);
  assert.equal(again.FoundPages, 0);
  // the life's journal at its end still holds them all (a trace or a survivor passes every page on)
  assert.equal(lifeJournal(again, { memo: '', scribbles: [] }).Pages, 5);
});

test('R-27 the journal\'s 希少な頁 page says how many were inherited and how many found', () => {
  assert.equal(rarePagesPage(null), null);
  assert.equal(rarePagesPage({ Pages: 0, InheritedPages: 0 }), null, 'no pages: no page');
  const both = rarePagesPage({ Pages: 6, InheritedPages: 4 });
  assert.equal(both.Title, MetaText.RarePagesTitle);
  assert.equal(both.Tab, 'Dungeon');
  assert.deepEqual(both.Rows.map((r) => [r.Label, r.Text]), [
    [MetaText.RarePagesInherited, MetaText.RarePagesCount(4)],
    [MetaText.RarePagesFound, MetaText.RarePagesCount(2)],
    ['', MetaText.RarePagesPending],
    ['', MetaText.RarePagesFate],
  ]);
  const found = rarePagesPage({ Pages: 2 });
  assert.deepEqual(found.Rows.map((r) => r.Label), [MetaText.RarePagesFound, '', ''], 'without InheritedPages every page is found');
  const { raw } = heir();
  assert.deepEqual(rarePagesPage(raw).Rows[0].Text, MetaText.RarePagesCount(4));
});

test('the trace view carries what each row holds as data (the card and 段, the enemy and 開示度, the page, the 走り書き)', () => {
  const { view } = heir();
  const by = (id) => view.Candidates.find((c) => c.Id === id);
  assert.deepEqual([by('card:feint').CardId, by('card:feint').Rank], ['feint', 3]);
  assert.deepEqual([by('enemy:polearm_warped').EnemyId, by('enemy:polearm_warped').Disclosure], ['polearm_warped', 2]);
  assert.equal(by('page:page-1'), undefined, 'an empty rare page is no candidate (試作で決めたこと 9)');
  assert.deepEqual([by('memo').Generation, by('memo').Text], [1, MEMO]);
});

test('X-11 an enemy page gives only what goes above the journal: the row reads this life\'s 開示度', () => {
  const { raw, view } = heir();
  // the journal: polearm 1 (the survivor's), rusted 2, shadow_hound 0; the trace: polearm 2, rusted 1, shadow 1
  const g = traceGains(raw, view.Candidates, []);
  const up = g.get('enemy:polearm_warped');
  assert.equal(up.Detail, Inh.InheritanceText.DisclosureDetail(2));
  assert.deepEqual(up.Notes, [TraceGainText.PageUp(1, 2)]);
  assert.equal(up.Nothing, false);
  const none = g.get('enemy:rusted_revenant');
  assert.equal(none.Nothing, true, 'the journal already reads 2: the page adds nothing');
  assert.equal(none.Detail, Inh.InheritanceText.DisclosureDetail(2), 'the heir\'s 開示度, not the dead heir\'s 1');
  assert.deepEqual(none.Notes, [TraceGainText.PageInTrace(1), TraceGainText.Nothing(TraceGainText.PageNow(2), false)]);
  const fresh = g.get('enemy:shadow_hound');
  assert.equal(fresh.Detail, Inh.InheritanceText.DisclosureDetail(1));
  assert.deepEqual(fresh.Notes, [], 'a new page: the row says it all');
});

test('X-11 the take returns what it did, and the receipt is written from it, not from the dead heir\'s numbers', () => {
  const { raw, rec, view, ls } = heir();
  // the heir already holds a 3 段 card (mastered in this life): an inherited 3 段 stops at 2
  raw.Mastery = raw.Mastery.with({ Records: new Map([['rend', new CardMastery('rend', 15, 3)]]) });
  const picks = ['card:feint', 'enemy:rusted_revenant', 'enemy:polearm_warped'];
  const before = traceGains(raw, view.Candidates, picks, { done: true });
  const r = Inh.takeTrace(ls.Lineage, view.TraceId, picks, { Layer: 3 });
  const result = rec.expedition.Inherit(clone(r.Taken));
  assert.deepEqual(result.Cards.map((c) => [c.CardId, c.Rank, c.From, c.To, c.Joins]), [['feint', 3, 0, 2, true]]);
  assert.deepEqual(result.Enemies.map((e) => [e.EnemyId, e.Disclosure, e.From, e.To]), [['polearm_warped', 2, 1, 2], ['rusted_revenant', 1, 2, 2]]);
  assert.equal(raw.Mastery.RankOf('feint'), 2);
  assert.equal(raw.DisclosureOf('rusted_revenant'), 2, 'the journal keeps the higher 開示度');
  const after = traceGains(raw, view.Candidates, picks, { done: true, result: { ...result, Scribbles: [] } });
  const feint = after.get('card:feint');
  assert.equal(feint.Name, cardNameAt('feint', 2), 'the name at the 段 this heir holds');
  assert.equal(feint.Detail, '2 段目');
  assert.deepEqual(feint.Notes, [
    TraceGainText.InTrace(view.Candidates.find((c) => c.Id === 'card:feint').Name, 3),
    `${TraceGainText.RankUp(0, 2)}（${TraceGainText.Capped(2)}）・${TraceGainText.Joined}`,
  ]);
  assert.deepEqual(after.get('card:feint').Notes, before.get('card:feint').Notes, 'the preview before the take said the same');
  const rusted = after.get('enemy:rusted_revenant');
  assert.equal(rusted.Nothing, true);
  assert.deepEqual(rusted.Notes, [TraceGainText.PageInTrace(1), TraceGainText.Nothing(TraceGainText.PageAlready(2), true)]);
  assert.deepEqual(after.get('enemy:polearm_warped').Notes, [TraceGainText.PageRaised(1, 2)]);
  assert.equal(after.has('card:thrust'), false, 'the receipt lists the picks only');
  // the take is a logged call: a replay makes the same life
  const { expedition: again } = lifeSetup(clone(heir().start));
  again.Mastery = again.Mastery.with({ Records: new Map([['rend', new CardMastery('rend', 15, 3)]]) });
  replayLife(again, clone(rec.log));
  assert.equal(again.Mastery.RankOf('feint'), 2);
});

test('X-11 a 走り書き the life already carries adds nothing, and the router keeps each once', () => {
  const { raw, view } = heir();
  const memo = view.Candidates.find((c) => c.Id === 'memo');
  const held = [{ Generation: 1, Name: memo.From, Text: MEMO }];
  assert.equal(traceGains(raw, view.Candidates, [], { scribbles: [] }).get('memo').Nothing, false);
  const g = traceGains(raw, view.Candidates, [], { scribbles: held }).get('memo');
  assert.equal(g.Nothing, true);
  assert.deepEqual(g.Notes, [TraceGainText.Nothing(TraceGainText.ScribbleHeld, false)]);
  const added = addScribbles(held, [{ Generation: 1, Name: memo.From, Text: MEMO }, { Generation: 2, Name: 'x', Text: 'new' }]);
  assert.deepEqual(added.list.map((s) => s.Text), [MEMO, 'new']);
  assert.deepEqual(added.rows.map((s) => s.Added), [false, true]);
  const done = traceGains(raw, view.Candidates, ['memo'], { done: true, result: { Cards: [], Enemies: [], Scribbles: added.rows } }).get('memo');
  assert.deepEqual(done.Notes, [TraceGainText.Nothing(TraceGainText.ScribbleHeld, true)]);
});

test('R-14 the hall says the survivor\'s bonus was used up once the life that used it has closed', () => {
  const l = lineage();
  assert.equal(Inh.hallView(l).SurvivorSpent, null, 'the survivor stands: its block shows');
  assert.notEqual(Inh.hallView(l).Survivor, null);
  const ls = Inh.startLife(l, 3n);
  const dead = Inh.recordDeath(ls.Lineage, ls.Life, { Layer: 1, NodeId: 1, Ending: RunPhase.Fallen }, [], null);
  const view = Inh.hallView(dead);
  assert.equal(view.Survivor, null);
  assert.deepEqual(view.SurvivorSpent, {
    Generation: 2,
    Line: Inh.InheritanceText.HeirLine(2, dead.Lives[1].Name),
    Lines: [Inh.InheritanceText.SurvivorSpent(3, ls.Life.Name), Inh.InheritanceText.SurvivorSpentLimits(3, 40)],
  });
  // one life later it is no news any more; a new survivor shows its own block instead
  const ls4 = Inh.startLife(dead, 4n);
  assert.equal(Inh.hallView(Inh.recordDeath(ls4.Lineage, ls4.Life, { Layer: 1, Ending: RunPhase.Fallen }, [], null)).SurvivorSpent, null);
  const survived = Inh.recordSurvival(ls.Lineage, ls.Life, { Layer: 2, Ending: RunPhase.Survived }, [], null);
  assert.equal(Inh.hallView(survived).SurvivorSpent, null);
  assert.equal(Inh.hallView(survived).Survivor.Generation, 3);
  assert.equal(Inh.hallView(Inh.newLineage(1)).SurvivorSpent, null);
});
