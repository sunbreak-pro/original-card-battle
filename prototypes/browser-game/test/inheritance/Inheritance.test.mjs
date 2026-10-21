// Tests for src/core/inheritance/Inheritance.js. There is no C# to port tests from: every rule of
// docs/INHERITANCE.md (R-01 …) has at least one test here, named after it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  Inheritance, InheritanceRules, InheritanceText, InheritanceRuleException, LifeEnding, CandidateKind,
  HeirNames, AttireTiers, newLineage, heirName, nextHeir, lifeSeed, startLife, recordDeath, recordSurvival,
  recordEnd, takeTrace, traceForLayer, traceLayers, mapSpecFor, noteCarving, mergeJournal, mergeMastery,
  normalizeJournal, hallView, traceNodeView, pickState, endingView, placeFromState, tierAfter,
  toJSON, fromJSON, serialize, deserialize,
} from '../../src/core/inheritance/Inheritance.js';
import { RunLoadout, ReliefItem } from '../../src/core/dungeon/RunLoadout.js';
import { NodeKind } from '../../src/core/dungeon/NodeKind.js';
import { RunPhase } from '../../src/core/dungeon/ExplorationState.js';
import { SevenLayers } from '../../src/core/dungeon/SevenLayers.js';
import { MapGenerator } from '../../src/core/dungeon/MapGenerator.js';
import { ExplorationReducer } from '../../src/core/dungeon/ExplorationReducer.js';
import { Constants } from '../../src/core/battle/Constants.js';

// ---- fixtures -----------------------------------------------------------------------------------

const SEED = 42n;

/** A deck as a list of mastery entries: [cardId, rank] pairs. */
function deck(...pairs) {
  return pairs.map(([CardId, Rank]) => ({ CardId, Rank }));
}

const FULL_DECK = deck(['kansatsu', 3], ['tsuki', 2], ['harai', 1], ['fumikomi', 0]);
const FULL_JOURNAL = { Disclosure: { sabiyari: 2, shoga: 1, shokou: 0 }, Pages: 2, Memo: '門竜は押すと崩れる' };

function throwsRule(fn, pattern) {
  assert.throws(fn, (e) => e instanceof InheritanceRuleException && (!pattern || pattern.test(e.message)));
}

/** Starts a life and kills it at `layer` with the given deck and journal. */
function dieAt(lineage, layer, { d = FULL_DECK, j = FULL_JOURNAL, ending = RunPhase.Fallen, nodeId = 4, seed = 1n } = {}) {
  const s = startLife(lineage, seed);
  return recordDeath(s.Lineage, s.Life, { Layer: layer, NodeId: nodeId, Ending: ending }, d, j);
}

function surviveAt(lineage, layer, { j = FULL_JOURNAL, opts = {}, ending = RunPhase.Survived, d = FULL_DECK } = {}) {
  const s = startLife(lineage, 1n);
  return recordSurvival(s.Lineage, s.Life, { Layer: layer, NodeId: 2, Ending: ending }, d, j, opts);
}

// ---- R-01 / R-02: lineage, heirs, names ------------------------------------------------------------

test('R-01 a new lineage holds no life, no trace, no survivor', () => {
  const l = newLineage(SEED);
  assert.equal(l.Version, 1);
  assert.equal(l.Seed, '42');
  assert.deepEqual(l.Lives, []);
  assert.equal(l.Current, null);
  assert.deepEqual(l.Traces, []);
  assert.equal(l.Survivor, null);
  assert.deepEqual(l.Player, { CarvingTaken: false, RootDefeated: false });
});

test('R-02 every heir has a personal name, deterministic from the lineage seed', () => {
  assert.equal(heirName(SEED, 1), heirName(42, 1));
  assert.equal(heirName(SEED, 1), heirName('42', 1));
  const first = Array.from({ length: HeirNames.length }, (_, i) => heirName(SEED, i + 1));
  assert.equal(new Set(first).size, HeirNames.length, 'one pass over the pool repeats no name');
  for (const n of first) assert.ok(HeirNames.includes(n));
  assert.notEqual(heirName(SEED, HeirNames.length + 1), heirName(SEED, HeirNames.length), 'a new pass does not repeat the last name');
  assert.ok(!HeirNames.includes('ガルド') && !HeirNames.includes('セルク'), 'the bosses keep their names');
  const others = Array.from({ length: 8 }, (_, i) => heirName(7n + BigInt(i), 1));
  assert.ok(new Set(others).size > 1, 'different lineages start with different heirs');
});

test('R-02 the hall names the next heir before the life starts, and startLife gives the same heir', () => {
  const l = newLineage(SEED);
  const heir = nextHeir(l);
  const s = startLife(l, 5n);
  assert.equal(s.Life.Generation, heir.Generation);
  assert.equal(s.Life.Name, heir.Name);
  assert.equal(s.Life.Tier, heir.Tier);
  assert.equal(hallView(l).NextHeir.Name, heir.Name);
  assert.deepEqual(nextHeir(s.Lineage), { Generation: 1, Name: heir.Name, Tier: 0 });
});

// ---- R-03: starting a life ------------------------------------------------------------------------

test('R-03 the first life: generation 1, tier 0, base slots, empty journal, an empty RunLoadout', () => {
  const s = startLife(newLineage(SEED), 9n);
  assert.equal(s.Life.Generation, 1);
  assert.equal(s.Life.Tier, 0);
  assert.equal(s.Seed, 9n);
  assert.equal(s.Life.Seed, '9');
  assert.equal(s.ToolSlots, RunLoadout.ToolSlots);
  assert.equal(s.ConsumableSlots, RunLoadout.ConsumableSlots);
  assert.equal(s.DeckMin, Constants.DeckMin);
  assert.equal(s.DeckMax, Constants.DeckMax);
  assert.ok(s.Loadout instanceof RunLoadout);
  assert.equal(s.Loadout.ToolSlotCount, 3);
  assert.deepEqual(s.Journal, { Disclosure: {}, Pages: [], Memo: '', Scribbles: [] });
  assert.equal(s.SurvivorBonus, false);
  assert.deepEqual(s.TraceLayers, []);
  assert.equal(s.Attire.Name, AttireTiers[0].Name);
});

test('R-03 a life without a seed takes one drawn from the lineage seed', () => {
  const l = newLineage(SEED);
  const a = startLife(l);
  const b = startLife(l);
  assert.equal(a.Seed, b.Seed);
  assert.equal(a.Seed, lifeSeed(l, 1));
  assert.equal(typeof a.Seed, 'bigint');
});

test('R-03 a second life cannot start while one is in progress', () => {
  const s = startLife(newLineage(SEED), 1n);
  throwsRule(() => startLife(s.Lineage, 2n), /in progress/);
});

// ---- R-04 / R-05: death leaves a trace with the candidates --------------------------------------------

test('R-04 a death closes the life and leaves a trace at the place of death', () => {
  const l = dieAt(newLineage(SEED), 4, { nodeId: 7 });
  assert.equal(l.Current, null);
  assert.equal(l.Lives.length, 1);
  const life = l.Lives[0];
  assert.equal(life.Ending, LifeEnding.Fallen);
  assert.equal(life.Layer, 4);
  assert.equal(life.NodeId, 7);
  assert.equal(life.TraceId, 'trace-1');
  assert.equal(l.Traces.length, 1);
  const t = l.Traces[0];
  assert.equal(t.Id, 'trace-1');
  assert.equal(t.Layer, 4);
  assert.equal(t.NodeId, 7);
  assert.equal(t.Generation, 1);
  assert.equal(t.Name, life.Name);
});

test('R-04 candidates: every card at 習熟 1 段 or more and every filled page; nothing else', () => {
  const l = dieAt(newLineage(SEED), 4, { ending: RunPhase.MiasmaDeath });
  const ids = l.Traces[0].Candidates.map((c) => c.Id);
  assert.deepEqual(ids, [
    'card:kansatsu', 'card:tsuki', 'card:harai', // rank 3, 2, 1 — rank 0 is not a candidate
    'enemy:sabiyari', 'enemy:shoga', // disclosure 0 is not a filled page
    // the 2 rare pages hold nothing yet (#107): an empty page is not a filled page (試作で決めたこと 9)
    'memo',
  ]);
  assert.equal(l.Lives[0].Ending, LifeEnding.MiasmaDeath);
  const memo = l.Traces[0].Candidates.find((c) => c.Id === 'memo');
  assert.equal(memo.Kind, CandidateKind.Scribble);
  assert.equal(memo.Text, '門竜は押すと崩れる');
});

test('R-04 a rare page is a candidate only once something is written on it (試作で決めたこと 9)', () => {
  const j = { Disclosure: {}, Pages: [{ Id: 'page-1', Title: '希少な頁 1' }, { Id: 'r-2', Title: '崩れた天井の頁', Text: '大穴の下は陽が届く' }], Memo: '' };
  const l = dieAt(newLineage(SEED), 4, { d: [], j });
  assert.deepEqual(l.Traces[0].Candidates.map((c) => [c.Id, c.Kind, c.Name, c.Text]),
    [['page:r-2', CandidateKind.DungeonPage, '崩れた天井の頁', '大穴の下は陽が届く']], 'the empty page is left out');
  const s = startLife(l, 1n);
  const r = takeTrace(s.Lineage, 'trace-1', ['page:r-2']);
  assert.deepEqual(r.Taken.Journal.Pages, [{ Id: 'r-2', Title: '崩れた天井の頁', Text: '大穴の下は陽が届く' }], 'the page keeps what is written on it');
  assert.equal(hallView(l).Traces[0].CountLine, InheritanceText.CandidateCount(0, 1, 0));
});

test('R-04 a trace saved while empty pages were candidates hides them, and an old take of one still replays', () => {
  const l = dieAt(newLineage(SEED), 4);
  const page = { Id: 'page:page-1', Kind: CandidateKind.DungeonPage, PageId: 'page-1', Name: '希少な頁 1' };
  const old = fromJSON({ ...toJSON(l), Traces: [{ ...toJSON(l).Traces[0], Candidates: [...toJSON(l).Traces[0].Candidates, page] }] });
  const t = hallView(old).Traces[0];
  assert.ok(!t.Candidates.some((c) => c.Kind === CandidateKind.DungeonPage), 'not offered');
  assert.equal(t.CountLine, '遺産の候補：技術 3 ／ 頁 2 ／ 走り書き 1', 'not counted');
  const s = startLife(old, 1n);
  assert.ok(!traceNodeView(s.Lineage, 4).Candidates.some((c) => c.Id === 'page:page-1'));
  const r = takeTrace(s.Lineage, 'trace-1', ['card:tsuki', 'page:page-1']);
  assert.deepEqual(r.Taken.Journal.Pages, [{ Id: 'page-1', Title: '希少な頁 1' }], 'a logged take from an old save loads');
});

test('R-04 the page of terms is never a candidate (tier1 R1-10)', () => {
  const j = { ...FULL_JOURNAL, Terms: ['間合い', '構え'], Disclosure: {} , Pages: 0, Memo: '' };
  const l = dieAt(newLineage(SEED), 4, { d: [], j });
  assert.deepEqual(l.Traces[0].Candidates, []);
  assert.deepEqual(normalizeJournal(j), { Disclosure: {}, Pages: [], Memo: '', Scribbles: [] });
});

test('R-04 an inherited card keeps its rank; its ticks start at that rank (thresholds 3 / 8 / 15 or the profile\'s)', () => {
  const l = dieAt(newLineage(SEED), 4, { d: [
    { CardId: 'a', Rank: 1 }, { CardId: 'b', Rank: 2 }, { CardId: 'c', Rank: 3 },
    { CardId: 'r', Rank: 2, Thresholds: Constants.MasteryThresholdsReform },
  ], j: null });
  const ticks = Object.fromEntries(l.Traces[0].Candidates.map((c) => [c.CardId, [c.Rank, c.Ticks]]));
  assert.deepEqual(ticks, { c: [3, 15], b: [2, 8], r: [2, 6], a: [1, 3] });
});

test('R-04 a MasteryBook-like deck: Records and ProfileOf give ranks, thresholds and names', () => {
  const book = {
    Records: new Map([
      ['kansatsu', { CardId: 'kansatsu', Ticks: 9, Rank: 2 }],
      ['zero', { CardId: 'zero', Ticks: 1, Rank: 0 }],
    ]),
    ProfileOf: (id) => (id === 'kansatsu' ? { Thresholds: [2, 6, 12], NameAt: (r) => ['精査', '洞察', '看破'][r - 1] } : null),
  };
  const l = dieAt(newLineage(SEED), 3, { d: book, j: null });
  assert.deepEqual(l.Traces[0].Candidates.map((c) => [c.CardId, c.Rank, c.Ticks, c.Name]), [['kansatsu', 2, 6, '洞察']]);
  const s = startLife(newLineage(SEED), 1n);
  const named = recordDeath(s.Lineage, null, { Layer: 3, Ending: RunPhase.Fallen }, deck(['kansatsu', 1]), null,
    { cardName: (id, rank) => `${id}@${rank}`, enemyName: (id) => `敵 ${id}` });
  assert.equal(named.Traces[0].Candidates[0].Name, 'kansatsu@1');
  const s2 = startLife(newLineage(SEED), 1n);
  const enemies = recordDeath(s2.Lineage, null, { Layer: 3 }, [], { Disclosure: { x: 1 } }, { enemyName: (id) => `敵 ${id}` });
  assert.equal(enemies.Traces[0].Candidates[0].Name, '敵 x');
});

test('R-05 shallow deaths keep cards at 2 段 or more out of the legacy (layers 1 and 2)', () => {
  for (const layer of [1, 2]) {
    const l = dieAt(newLineage(SEED), layer);
    const t = l.Traces[0];
    assert.deepEqual(t.Candidates.filter((c) => c.Kind === CandidateKind.Card).map((c) => c.CardId), ['harai'], `layer ${layer}`);
    assert.deepEqual(t.Withheld.map((w) => [w.CardId, w.Rank]), [['kansatsu', 3], ['tsuki', 2]]);
    assert.ok(t.Candidates.some((c) => c.Kind === CandidateKind.EnemyPage), 'pages stay from any layer');
  }
});

test('R-05 from layer 3 down every card at 1 段 or more stays', () => {
  for (const layer of [3, 5, 7]) {
    const t = dieAt(newLineage(SEED), layer).Traces[0];
    assert.deepEqual(t.Candidates.filter((c) => c.Kind === CandidateKind.Card).map((c) => c.CardId), ['kansatsu', 'tsuki', 'harai']);
    assert.deepEqual(t.Withheld, []);
  }
});

test('R-04 a death needs a life in progress, its generation, a death ending and a layer 1..7', () => {
  const l = newLineage(SEED);
  throwsRule(() => recordDeath(l, null, { Layer: 1 }, [], null), /no life/);
  const s = startLife(l, 1n);
  throwsRule(() => recordDeath(s.Lineage, 2, { Layer: 1 }, [], null), /not the life in progress/);
  throwsRule(() => recordDeath(s.Lineage, s.Life, { Layer: 1, Ending: RunPhase.Survived }, [], null), /Survived is not one of/);
  throwsRule(() => recordDeath(s.Lineage, s.Life, { Layer: 1, Ending: RunPhase.Exploring }, [], null), /Exploring/);
  throwsRule(() => recordDeath(s.Lineage, s.Life, { Layer: 1, Ending: 'toString' }, [], null), /not a RunPhase/);
  throwsRule(() => recordDeath(s.Lineage, s.Life, { Layer: 8 }, [], null), /layer/);
  throwsRule(() => recordDeath(s.Lineage, s.Life, { Layer: 1 }, [], { Disclosure: { a: 3 } }), /disclosure/);
  throwsRule(() => recordDeath(s.Lineage, s.Life, { Layer: 1 }, deck(['a', 1], ['a', 2]), null), /twice/);
  const closed = recordDeath(s.Lineage, s.Life, { Layer: 1 }, [], null);
  throwsRule(() => recordDeath(closed, s.Life, { Layer: 1 }, [], null), /no life/);
});

// ---- R-06 / R-07: traces stay until collected, one per layer ----------------------------------------

test('R-06 traces are kept until collected: a later death does not overwrite them', () => {
  let l = dieAt(newLineage(SEED), 3);
  l = dieAt(l, 5);
  l = dieAt(l, 3);
  assert.deepEqual(l.Traces.map((t) => [t.Id, t.Layer]), [['trace-1', 3], ['trace-2', 5], ['trace-3', 3]]);
  assert.equal(hallView(l).Traces.length, 3, 'the hall shows every untaken trace');
});

test('R-07 a layer holds at most one trace node: the oldest untaken one; the others wait', () => {
  let l = dieAt(newLineage(SEED), 3);
  l = dieAt(l, 5);
  l = dieAt(l, 3);
  assert.equal(traceForLayer(l, 3).Id, 'trace-1');
  assert.equal(traceForLayer(l, 5).Id, 'trace-2');
  assert.equal(traceForLayer(l, 4), null);
  assert.deepEqual(traceLayers(l), [3, 5]);
  const views = hallView(l).Traces;
  assert.deepEqual(views.map((v) => [v.Id, v.Visible, v.AppearsLine]), [
    ['trace-1', true, InheritanceText.Appears],
    ['trace-2', true, InheritanceText.Appears],
    ['trace-3', false, InheritanceText.Waits],
  ]);
  const s = startLife(l, 1n);
  assert.deepEqual(s.TraceLayers, [3, 5]);
  throwsRule(() => takeTrace(s.Lineage, 'trace-3', [], { Layer: 3 }), /waits behind/);
});

// ---- R-08 .. R-11: taking a trace -------------------------------------------------------------------

test('R-08 at the trace node: up to 3 picks, no duplicates, only its candidates', () => {
  const l = dieAt(newLineage(SEED), 2); // kansatsu and tsuki are withheld
  const s = startLife(l, 1n).Lineage;
  throwsRule(() => takeTrace(s, 'trace-1', ['card:harai', 'enemy:sabiyari', 'page:page-1', 'memo']), /at most 3/);
  throwsRule(() => takeTrace(s, 'trace-1', ['memo', 'memo']), /twice/);
  throwsRule(() => takeTrace(s, 'trace-1', ['card:nothing']), /not a candidate/);
  throwsRule(() => takeTrace(s, 'trace-1', ['card:kansatsu']), /not a candidate/);
  throwsRule(() => takeTrace(s, 'trace-9', []), /no untaken trace/);
  const r = takeTrace(s, 'trace-1', ['card:harai', 'enemy:sabiyari', 'memo'], { Layer: 2 });
  assert.equal(r.Taken.TraceId, 'trace-1');
  assert.equal(r.Taken.Lost, l.Traces[0].Candidates.length - 3);
});

test('R-08 taking nothing is allowed and still collects the trace', () => {
  const s = startLife(dieAt(newLineage(SEED), 3), 1n).Lineage;
  const r = takeTrace(s, 'trace-1', []);
  assert.deepEqual(r.Lineage.Traces, []);
  assert.equal(r.Lineage.Current.TakenTraceId, 'trace-1');
  assert.deepEqual(r.Taken.Cards, []);
});

test('R-09 a life takes one trace only: the first visited; later traces stay for later lives', () => {
  let l = dieAt(newLineage(SEED), 3);
  l = dieAt(l, 5);
  const s = startLife(l, 1n).Lineage;
  const r = takeTrace(s, 'trace-1', ['card:harai']);
  throwsRule(() => takeTrace(r.Lineage, 'trace-2', []), /already took trace-1/);
  assert.equal(traceForLayer(r.Lineage, 5), null, 'no trace node appears for the rest of this life');
  assert.deepEqual(traceLayers(r.Lineage), []);
  const node = traceNodeView(r.Lineage, 5);
  assert.equal(node.CanTake, false);
  assert.equal(node.Refusal, InheritanceText.AlreadyTaken);
  const after = recordDeath(r.Lineage, null, { Layer: 1 }, [], null);
  assert.deepEqual(startLife(after, 1n).TraceLayers, [1, 5], 'trace-2 is still there for the next life');
});

test('R-10 the taken trace is collected: it leaves the list, the dead life records who took it', () => {
  const l = dieAt(newLineage(SEED), 3);
  const s = startLife(l, 1n).Lineage;
  const r = takeTrace(s, 'trace-1', ['enemy:shoga']);
  assert.deepEqual(r.Lineage.Traces, []);
  assert.equal(r.Lineage.Lives[0].CollectedBy, 2);
  const done = recordDeath(r.Lineage, null, { Layer: 1 }, [], null);
  const past = hallView(done).PastLives.find((p) => p.Generation === 1);
  assert.equal(past.TraceLine, InheritanceText.TraceStatus.Taken(2, done.Lives[1].Name));
});

test('R-11 the taken cards keep their rank; pages and 走り書き arrive as journal additions', () => {
  const l = dieAt(newLineage(SEED), 4);
  const s = startLife(l, 1n);
  const r = takeTrace(s.Lineage, 'trace-1', ['card:kansatsu', 'enemy:sabiyari', 'memo'], { Layer: 4 });
  assert.deepEqual(r.Taken.Cards, [{ CardId: 'kansatsu', Rank: 3, Ticks: 15, Name: 'kansatsu' }]);
  assert.deepEqual(r.Taken.Journal.Disclosure, { sabiyari: 2 });
  assert.deepEqual(r.Taken.Journal.Scribbles, [{ Generation: 1, Name: l.Lives[0].Name, Text: '門竜は押すと崩れる' }]);
  const merged = mergeJournal({ Disclosure: { sabiyari: 1, other: 1 }, Pages: ['p'], Memo: '自分のメモ' }, r.Taken.Journal);
  assert.deepEqual(merged.Disclosure, { other: 1, sabiyari: 2 });
  assert.equal(merged.Memo, '自分のメモ', 'the memo stays the heir\'s own');
  assert.equal(merged.Scribbles.length, 1);
  assert.deepEqual(mergeJournal(merged, r.Taken.Journal), merged, 'merging twice changes nothing');
});

test('R-11 taking a trace needs a life in progress and the heir on its layer', () => {
  const l = dieAt(newLineage(SEED), 3);
  throwsRule(() => takeTrace(l, 'trace-1', []), /no life/);
  const s = startLife(l, 1n).Lineage;
  throwsRule(() => takeTrace(s, 'trace-1', [], { Layer: 4 }), /on layer 3, not 4/);
});

test('R-11 mergeMastery: ranks kept, ticks the higher, one card at step 3', () => {
  const inherited = [{ CardId: 'b', Rank: 3, Ticks: 15 }, { CardId: 'a', Rank: 1, Ticks: 3 }];
  const fresh = mergeMastery(null, inherited);
  assert.deepEqual([...fresh.values()], [{ CardId: 'a', Ticks: 3, Rank: 1 }, { CardId: 'b', Ticks: 15, Rank: 3 }]);
  const held = mergeMastery(new Map([['z', { CardId: 'z', Ticks: 16, Rank: 3 }], ['a', { CardId: 'a', Ticks: 7, Rank: 1 }]]), inherited);
  assert.deepEqual(held.get('b'), { CardId: 'b', Ticks: 15, Rank: 2 }, 'the life already holds step 3 on z');
  assert.deepEqual(held.get('a'), { CardId: 'a', Ticks: 7, Rank: 1 });
  assert.deepEqual([...held.keys()], ['a', 'b', 'z']);
  const made = mergeMastery({}, inherited, (id, t, r) => `${id}:${t}:${r}`);
  assert.deepEqual([...made.values()], ['a:3:1', 'b:15:3']);
});

// ---- R-12: the hall only shows ----------------------------------------------------------------------

test('R-12 the hall shows the candidates of every trace and offers no way to take one', () => {
  const l = dieAt(newLineage(SEED), 2);
  const h = hallView(l);
  const t = h.Traces[0];
  assert.equal(t.PlaceLine, InheritanceText.LayerTitle(2, '■■の秘跡'));
  assert.deepEqual(t.Candidates.map((c) => [c.KindLabel, c.Detail]), [
    ['技術', '1 段目'], ['敵の頁', '開示度 2 / 2'], ['敵の頁', '開示度 1 / 2'],
    ['走り書き', '門竜は押すと崩れる'],
  ]);
  assert.deepEqual(t.Withheld.map((w) => w.Detail), ['3 段目。第 3 層より浅い死では残らない', '2 段目。第 3 層より浅い死では残らない']);
  assert.equal(t.CountLine, '遺産の候補：技術 1 ／ 頁 2 ／ 走り書き 1');
  assert.deepEqual(h.TraceRules, InheritanceText.TraceRules);
  assert.ok(Object.values(h).every((v) => typeof v !== 'function'), 'a view-model holds data only');
});

// ---- R-13 .. R-17: survival ----------------------------------------------------------------------------

test('R-13 the survivor\'s whole journal passes to the next life; after a death the journal starts empty', () => {
  const l = surviveAt(newLineage(SEED), 3);
  const s = startLife(l, 1n);
  assert.deepEqual(s.Journal, normalizeJournal(FULL_JOURNAL));
  assert.equal(s.Life.InheritedJournal, true);
  const died = recordDeath(s.Lineage, null, { Layer: 1 }, [], null);
  assert.deepEqual(startLife(died, 1n).Journal, normalizeJournal(null), 'only the life right after the survivor reads its journal');
});

test('R-14 the survivor bonus: tool slots 4 and deck cap 45 for the next life only', () => {
  const l = surviveAt(newLineage(SEED), 2);
  const s = startLife(l, 1n);
  assert.equal(s.SurvivorBonus, true);
  assert.equal(s.ToolSlots, RunLoadout.SurvivorBonusToolSlots);
  assert.equal(s.DeckMax, Constants.DeckMax + 5);
  assert.equal(s.Loadout.ToolSlotCount, 4);
  const four = Array.from({ length: 4 }, () => ReliefItem.Density('bosho_no_men', 1));
  assert.doesNotThrow(() => new RunLoadout(four, null, s.ToolSlots));
  assert.equal(s.Life.SurvivorFrom, 1);
  const next = startLife(recordDeath(s.Lineage, null, { Layer: 1 }, [], null), 1n);
  assert.equal(next.SurvivorBonus, false);
  assert.equal(next.ToolSlots, 3);
  assert.equal(next.DeckMax, 40);
  assert.equal(next.Lineage.Survivor, null);
});

test('R-15 one survivor at most: a new survivor replaces the previous one', () => {
  let l = surviveAt(newLineage(SEED), 2, { opts: { ExtraBossWon: false } });
  l = surviveAt(l, 3, { j: { Memo: '二人目' } });
  assert.equal(l.Survivor.Generation, 2);
  assert.equal(l.Survivor.BonusLivesLeft, 1);
  const s = startLife(l, 1n);
  assert.equal(s.Life.SurvivorFrom, 2);
  assert.equal(s.Journal.Memo, '二人目');
  assert.equal(l.Lives[1].ReplacedSurvivor, null, 'generation 2 already used generation 1\'s one life of bonus');
  assert.ok(!endingView(l).Lines.some((x) => x.startsWith('生存者は一人まで')));
});

test('R-15 the previous survivor\'s remaining lives are dropped when a new one survives', () => {
  let l = surviveAt(newLineage(SEED), 7, { ending: RunPhase.Completed, opts: { ExtraBossWon: true } });
  assert.equal(l.Survivor.BonusLivesLeft, 2);
  l = surviveAt(l, 1);
  assert.equal(l.Survivor.Generation, 2);
  assert.equal(l.Survivor.BonusLivesLeft, 1);
  assert.equal(l.Lives[1].ReplacedSurvivor, 1);
  assert.ok(endingView(l).Lines.includes(InheritanceText.SurvivorReplaced(1, l.Lives[0].Name)));
  const s = startLife(l, 1n);
  const after = startLife(recordDeath(s.Lineage, null, { Layer: 1 }, [], null), 1n);
  assert.equal(after.SurvivorBonus, false);
});

test('R-16 the dead heir\'s legacy and the survivor\'s bonus apply together', () => {
  let l = dieAt(newLineage(SEED), 3);
  l = surviveAt(l, 2, { j: { Memo: '生存者の手記' } });
  const s = startLife(l, 1n);
  assert.equal(s.SurvivorBonus, true);
  assert.deepEqual(s.TraceLayers, [3]);
  assert.equal(s.Journal.Memo, '生存者の手記');
  const r = takeTrace(s.Lineage, 'trace-1', ['card:kansatsu', 'enemy:sabiyari'], { Layer: 3 });
  assert.equal(r.Taken.Cards[0].Rank, 3);
  const h = hallView(l);
  assert.equal(h.Survivor.Generation, 2);
  assert.deepEqual(h.Survivor.Lines, ['手記を丸ごと受け継ぐ', 'ツール枠 +1（3 → 4）', 'デッキ上限 +5（40 → 45）']);
  assert.equal(h.Survivor.DurationLine, '次の 1 生だけ');
  assert.equal(h.Traces.length, 1);
});

test('R-17 a won extra boss stretches the survivor bonus to two lives; the journal still passes once', () => {
  const l = surviveAt(newLineage(SEED), 7, { ending: RunPhase.Completed, opts: { ExtraBossWon: true } });
  assert.equal(hallView(l).Survivor.DurationLine, 'あと 2 生');
  const a = startLife(l, 1n);
  assert.equal(a.SurvivorBonus, true);
  assert.equal(a.Lineage.Survivor.BonusLivesLeft, 1);
  assert.equal(hallView(recordDeath(a.Lineage, null, { Layer: 1 }, [], null)).Survivor.Lines.includes('手記を丸ごと受け継ぐ'), false);
  const b = startLife(recordDeath(a.Lineage, null, { Layer: 1 }, [], null), 1n);
  assert.equal(b.SurvivorBonus, true);
  assert.equal(b.ToolSlots, 4);
  assert.deepEqual(b.Journal, normalizeJournal(null));
  const c = startLife(recordDeath(b.Lineage, null, { Layer: 1 }, [], null), 1n);
  assert.equal(c.SurvivorBonus, false);
  assert.ok(endingView(l).Lines.includes('生存者ボーナス：ツール枠 +1 ／ デッキ上限 +5（次の 2 生）'));
});

test('R-13 survival needs a survival ending', () => {
  const s = startLife(newLineage(SEED), 1n);
  throwsRule(() => recordSurvival(s.Lineage, s.Life, { Layer: 2, Ending: RunPhase.Fallen }, [], null), /not one of/);
});

// ---- R-18: 踏破 ---------------------------------------------------------------------------------------

test('R-18 踏破: the heir survives, the root is recorded, and a card trace stays on layer 7 with the root\'s cards', () => {
  const l = surviveAt(newLineage(SEED), 7, { ending: RunPhase.Completed, opts: { RootCards: ['ne_watari', 'ne_shibari', { CardId: 'tsuki', Rank: 0 }] } });
  assert.equal(l.Player.RootDefeated, true);
  assert.equal(l.Survivor.Generation, 1);
  assert.equal(l.Lives[0].Ending, LifeEnding.Completed);
  const t = l.Traces[0];
  assert.equal(t.Cleared, true);
  assert.equal(t.Layer, 7);
  assert.deepEqual(t.Candidates.map((c) => [c.CardId, c.Rank]), [
    ['kansatsu', 3], ['tsuki', 2], ['harai', 1], ['ne_shibari', 0], ['ne_watari', 0],
  ], 'mastered cards, then the root\'s cards at any rank; a card already listed is not repeated');
  assert.ok(t.Candidates.every((c) => c.Kind === CandidateKind.Card), 'the pages went with the survivor');
  const s = startLife(l, 1n);
  assert.equal(s.Life.Tier, InheritanceRules.ClearedTier);
  assert.deepEqual(s.TraceLayers, [7]);
  const e = endingView(l);
  assert.equal(e.Title, '踏破');
  assert.equal(e.Sentence, '歪みの根を倒した。');
  assert.ok(e.Lines.includes(InheritanceText.ClearedTraceLeft(InheritanceText.LayerTitle(7, '果てなき世界'))));
});

// ---- R-19: 装束 tier ------------------------------------------------------------------------------------

test('R-19 the heir\'s 装束 tier comes from the previous life\'s reached layer, not from the best ever', () => {
  const expected = { 1: 0, 2: 1, 3: 1, 4: 2, 5: 2, 6: 3, 7: 3 };
  for (const [layer, tier] of Object.entries(expected)) {
    const l = dieAt(newLineage(SEED), Number(layer));
    assert.equal(startLife(l, 1n).Life.Tier, tier, `layer ${layer}`);
    assert.equal(tierAfter(l.Lives[0]), tier);
  }
  let l = dieAt(newLineage(SEED), 6);
  l = dieAt(l, 1);
  assert.equal(nextHeir(l).Tier, 0, 'a shallow life after a deep one sends the next heir back to tier 0');
  const h = hallView(dieAt(newLineage(SEED), 5));
  assert.equal(h.NextHeir.TierName, '銀の縁');
  assert.match(h.NextHeir.TierReason, /が第 5 層まで届いたため$/);
  assert.deepEqual(h.NextHeir.Attire.map((a) => a.Part), ['面の縁飾り', 'マントの裏地', '留め具']);
  assert.equal(hallView(newLineage(SEED)).NextHeir.TierReason, InheritanceText.TierFirst);
  assert.equal(AttireTiers.length, 5);
});

// ---- R-20: player knowledge, map specs ------------------------------------------------------------------

test('R-20 the carving is the player\'s knowledge: it survives the death and names layer 2', () => {
  const l = dieAt(newLineage(SEED), 2, { j: { ...FULL_JOURNAL, CarvingTaken: true } });
  assert.equal(l.Player.CarvingTaken, true);
  assert.equal(hallView(l).Traces[0].PlaceLine, '第 2 層　竜神の秘跡');
  assert.equal(startLife(l, 1n).CarvingKnown, true);
  const noted = noteCarving(newLineage(SEED));
  assert.equal(noted.Player.CarvingTaken, true);
  assert.equal(noteCarving(noted), noted);
});

test('R-20 mapSpecFor: a trace layer trades one battle for a trace node; a read carving is not offered again', () => {
  let l = newLineage(SEED);
  for (const layer of [1, 2, 3, 4, 5, 6, 7]) l = dieAt(l, layer);
  l = startLife(l, 1n).Lineage;
  for (const profile of SevenLayers.All) {
    const spec = mapSpecFor(l, profile);
    assert.equal(spec.Quotas.get(NodeKind.Trace), 1, `layer ${profile.Layer}`);
    assert.equal((spec.Quotas.get(NodeKind.Battle) ?? 0), (profile.MapSpec.Quotas.get(NodeKind.Battle) ?? 0) - 1);
    for (const seed of [1n, 2n, 3n]) {
      const map = MapGenerator.Generate(spec, seed);
      assert.equal(map.Nodes.filter((n) => n.Kind === NodeKind.Trace).length, 1);
    }
  }
  const carving = noteCarving(newLineage(SEED));
  const layer2 = mapSpecFor(carving, SevenLayers.Of(2));
  assert.equal(layer2.Quotas.has(NodeKind.Carving), false);
  assert.equal(layer2.Quotas.get(NodeKind.Battle), SevenLayers.Of(2).MapSpec.Quotas.get(NodeKind.Battle) + 1);
  assert.equal(mapSpecFor(newLineage(SEED), SevenLayers.Of(1)), SevenLayers.Of(1).MapSpec, 'nothing to change, the same spec');
});

// ---- R-21: saving -------------------------------------------------------------------------------------

test('R-21 a lineage survives the JSON round trip unchanged', () => {
  let l = dieAt(newLineage(SEED), 4);
  l = surviveAt(l, 2);
  const s = startLife(l, 123456789012345678n);
  const r = takeTrace(s.Lineage, 'trace-1', ['card:tsuki', 'memo']);
  for (const x of [l, s.Lineage, r.Lineage]) {
    assert.deepEqual(deserialize(serialize(x)), x);
    assert.deepEqual(fromJSON(toJSON(x)), x);
  }
  assert.equal(deserialize(serialize(s.Lineage)).Current.Seed, '123456789012345678');
});

test('R-21 a death saved at once is still there after a reload, and a taken trace cannot be taken again', () => {
  const dead = deserialize(serialize(dieAt(newLineage(SEED), 3)));
  assert.equal(dead.Traces.length, 1);
  assert.equal(dead.Current, null);
  const s = startLife(dead, 1n);
  const took = deserialize(serialize(takeTrace(s.Lineage, 'trace-1', ['card:harai']).Lineage));
  throwsRule(() => takeTrace(took, 'trace-1', ['card:kansatsu']), /already took/);
});

test('R-21 a save that does not read is refused', () => {
  const good = toJSON(dieAt(newLineage(SEED), 3));
  throwsRule(() => fromJSON(null), /not an object/);
  throwsRule(() => fromJSON({ ...good, Version: 2 }), /version/);
  throwsRule(() => fromJSON({ ...good, Seed: 'x' }), /seed/);
  throwsRule(() => fromJSON({ ...good, Lives: [{ ...good.Lives[0], Generation: 2 }] }), /out of order/);
  throwsRule(() => fromJSON({ ...good, Lives: [{ ...good.Lives[0], Ending: 'Exploring' }] }), /ending/);
  throwsRule(() => fromJSON({ ...good, Traces: [{ ...good.Traces[0], Candidates: [{ Id: 'x', Kind: 'Gold', Name: 'x' }] }] }), /candidate/);
});

// ---- R-22 / R-23: purity --------------------------------------------------------------------------------

test('R-22 no function changes its input; every lineage is deep-frozen', () => {
  const l = dieAt(newLineage(SEED), 3);
  const before = serialize(l);
  const s = startLife(l, 1n);
  takeTrace(s.Lineage, 'trace-1', ['card:harai']);
  hallView(l);
  endingView(l);
  assert.equal(serialize(l), before);
  assert.ok(Object.isFrozen(l) && Object.isFrozen(l.Traces) && Object.isFrozen(l.Traces[0].Candidates[0]));
  assert.throws(() => { l.Traces.push({}); }, TypeError);
});

test('R-23 the same calls give the same lineage, byte for byte', () => {
  const run = () => {
    let l = newLineage(99n);
    l = dieAt(l, 3, { seed: 5n });
    l = surviveAt(l, 4);
    const s = startLife(l);
    return serialize(takeTrace(s.Lineage, 'trace-1', ['memo']).Lineage);
  };
  assert.equal(run(), run());
});

// ---- R-24 / R-25: the screens' text -------------------------------------------------------------------

test('R-24 ending card: the end sentence, what stays, and the next heir', () => {
  const miasma = endingView(dieAt(newLineage(SEED), 4, { ending: RunPhase.MiasmaDeath }));
  assert.equal(miasma.Title, '瘴気死');
  assert.equal(miasma.Sentence, '瘴気が 100% に達し、第 4 層でこの生が終わった。');
  assert.deepEqual(miasma.Lines, [
    '第 4 層　輝晶の間に痕跡と手記が残った',
    '遺産の候補：技術 3 ／ 頁 2 ／ 走り書き 1',
    '次の踏破者が第 4 層を訪れると、3 つまで選んで受け取れる',
  ]);
  const shallow = dieAt(newLineage(SEED), 1, { d: deck(['kansatsu', 2]), j: null });
  const fallen = endingView(shallow);
  assert.equal(fallen.Sentence, '第 1 層の戦いで倒れた。');
  assert.deepEqual(fallen.Lines, ['第 1 層　燦光の樹海に痕跡と手記が残った', InheritanceText.NothingLeft, '浅い層の死では残らない札：kansatsu']);
  assert.equal(fallen.NextLine, `次の踏破者：第 2 代 ${heirName(SEED, 2)}`);
  assert.equal(fallen.Button, '継承の間へ');
  const survived = endingView(surviveAt(newLineage(SEED), 3));
  assert.equal(survived.Sentence, '生きたまま、第 3 層でこの生を閉じた。');
  assert.deepEqual(survived.Lines, ['手記は丸ごと次の生へ渡る', '生存者ボーナス：ツール枠 +1 ／ デッキ上限 +5（次の 1 生）']);
  throwsRule(() => endingView(newLineage(SEED)), /no closed life/);
});

test('R-25 hall: first visit, past lives newest first, the next heir line', () => {
  const empty = hallView(newLineage(SEED));
  assert.equal(empty.Title, '継承の間');
  assert.equal(empty.Empty, InheritanceText.HallEmpty);
  assert.equal(empty.NoTraces, InheritanceText.NoTraces);
  assert.equal(empty.Survivor, null);
  assert.equal(empty.StartButton, '出立の支度へ');
  assert.ok([...empty.StartButton].length <= 10, 'fixed labels stay within 10 characters');
  let l = dieAt(newLineage(SEED), 3);
  l = surviveAt(l, 2);
  const h = hallView(l);
  assert.equal(h.Empty, '');
  assert.deepEqual(h.PastLives.map((p) => [p.Generation, p.EndingLabel, p.TraceLine]), [
    [2, '生存ルート', InheritanceText.TraceStatus.None],
    [1, '倒れた', InheritanceText.TraceStatus.Untaken],
  ]);
  assert.equal(h.PastLives[1].PlaceLine, '第 3 層　大黒蛇の住処');
  assert.equal(h.NextHeir.Line, `第 3 代 踏破者 ${heirName(SEED, 3)}`);
});

test('R-25 trace node card: candidates, at most 3, the pick counter', () => {
  const l = startLife(dieAt(newLineage(SEED), 2), 1n).Lineage;
  assert.equal(traceNodeView(l, 3), null);
  const v = traceNodeView(l, 2);
  assert.equal(v.Title, '死亡地点の痕跡');
  assert.equal(v.TraceId, 'trace-1');
  assert.equal(v.MaxPicks, 3);
  assert.equal(v.CanTake, true);
  assert.equal(v.Lead, '遺産を 3 つまで選んで受け取る。選ばなかったものはここで失われる。');
  assert.equal(v.WithheldHeading, InheritanceText.WithheldHeading);
  assert.deepEqual(pickState(['a', 'b']), { Line: '2 / 3', CanPickMore: true });
  assert.deepEqual(pickState(['a', 'b', 'c']), { Line: '3 / 3', CanPickMore: false });
  const scribble = traceNodeView(startLife(dieAt(newLineage(SEED), 3, { d: [], j: { Memo: 'あ'.repeat(30) } }), 1n).Lineage, 3);
  assert.equal(scribble.Candidates[0].Detail, `${'あ'.repeat(20)}…`);
});

// ---- R-26: wiring to the exploration core -----------------------------------------------------------------

test('R-26 placeFromState + recordEnd close a life straight from a finished ExplorationState', () => {
  const profile = SevenLayers.Of(1);
  const entered = ExplorationReducer.Enter(profile, profile.Map(3n), RunLoadout.Empty, 50, 50, 10);
  const fallen = ExplorationReducer.ReportBattle(entered, 0, 0);
  assert.equal(fallen.Phase, RunPhase.Fallen);
  const s = startLife(newLineage(SEED), 3n);
  const place = placeFromState(fallen);
  assert.deepEqual(place, { Layer: 1, NodeId: entered.Map.EntryId, NodeKind: NodeKind.Battle, Ending: RunPhase.Fallen });
  const l = recordEnd(s.Lineage, s.Life, place, deck(['a', 1]), null);
  assert.equal(l.Traces[0].NodeKind, NodeKind.Battle);
  assert.equal(hallView(l).Traces[0].PlaceLine, '第 1 層　燦光の樹海（通常戦闘）');
  const alive = recordEnd(startLife(l, 1n).Lineage, null, placeFromState(ExplorationReducer.EndLife(entered)), [], null);
  assert.equal(alive.Survivor.Generation, 2);
  throwsRule(() => recordEnd(startLife(alive, 1n).Lineage, null, placeFromState(entered), [], null), /does not end a life/);
});

test('the Inheritance namespace carries the same functions and the rule numbers', () => {
  assert.equal(Inheritance.startLife, startLife);
  assert.equal(Inheritance.takeTrace, takeTrace);
  assert.equal(Inheritance.Rules.LegacyPicks, 3);
  assert.equal(Inheritance.Rules.DeepRank, 2);
  assert.equal(Inheritance.Rules.DeepLayer, 3);
  assert.equal(Inheritance.Rules.SurvivorToolSlots, 4);
  assert.equal(Inheritance.Rules.SurvivorDeckBonus, 5);
});
