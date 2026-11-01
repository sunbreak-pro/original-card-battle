// Oracle for InheritanceCore (the C# port of the browser trial's core/inheritance/Inheritance.js): prints what the
// JS gives for a set of calls, and JsParityTests / JsonTests pin the C# to it byte for byte.
// Regenerate (the trial's JS lives on branch origin/claude/nifty-dirac-a99nv7, folder prototypes/browser-game):
//   node js-oracle.mjs <path to prototypes/browser-game> > js-oracle.json
import { pathToFileURL } from 'node:url';
const root = process.argv[2];
const Inh = await import(pathToFileURL(root + '/src/core/inheritance/Inheritance.js').href);
const { RunPhase } = await import(pathToFileURL(root + '/src/core/dungeon/ExplorationState.js').href);
const out = {};

// numbers
const nums = [0.1, 1/3, 2/3, 1e21, 1e20, 123456789012345680000, 1e-7, 1e-6, 0.000001234, 123e-20, 5e-324,
  1.7976931348623157e308, 2**53, 2**53 + 2, 2**60, 1.5, -2.5e-8, 100, -0, 0.3, 1.1, 4.35, 9.95, 1e300, 12.5e10, 3.14159,
  -1e-7, 1.0000000000000002, 299792458, 0.1 + 0.2, 1e16, 1.2e16, 4294967295, 4294967296.5, 2.2250738585072014e-308];
out.numbers = nums.map((x) => [Object.is(x, -0) ? '-0' : String(x), JSON.stringify(x)]);
out.strings = ['plain', 'quote " and \ back', '\b\f\n\r\t', '\u0000\u001f\u007f', '  /', 'あ😀', '\ud800x', 'x\udc00', '\ud83d']
  .map((s) => [Array.from({ length: s.length }, (_, i) => s.charCodeAt(i)), JSON.stringify(s)]);
out.indent = JSON.stringify({ a: 1, b: [1, { c: [] }, {}], '2': 'two', '10': null, '01': true, d: 'x' }, null, 2);
out.order = JSON.stringify(JSON.parse('{"b":1,"10":2,"a":3,"2":4,"4294967295":5,"4294967294":6,"-1":7,"01":8}'));

// names and seeds
out.names42 = Array.from({ length: 81 }, (_, i) => Inh.heirName(42n, i + 1));
out.firstNames = Array.from({ length: 8 }, (_, i) => Inh.heirName(7n + BigInt(i), 1));
out.bigSeedName = Inh.heirName('18446744073709551615', 3);
out.wrapSeedName = [Inh.heirName('18446744073709551616', 1), Inh.heirName(0n, 1)];
out.lifeSeeds = [1, 2, 3].map((g) => String(Inh.lifeSeed(Inh.newLineage(42n), g)));

// lineages
const FULL_DECK = [['kansatsu', 3], ['tsuki', 2], ['harai', 1], ['fumikomi', 0]].map(([CardId, Rank]) => ({ CardId, Rank }));
const FULL_JOURNAL = { Disclosure: { sabiyari: 2, shoga: 1, shokou: 0 }, Pages: 2, Memo: '門竜は押すと崩れる' };
function dieAt(l, layer, { d = FULL_DECK, j = FULL_JOURNAL, ending = RunPhase.Fallen, nodeId = 4, seed = 1n } = {}) {
  const s = Inh.startLife(l, seed);
  return Inh.recordDeath(s.Lineage, s.Life, { Layer: layer, NodeId: nodeId, Ending: ending }, d, j);
}
function surviveAt(l, layer, { j = FULL_JOURNAL, opts = {}, ending = RunPhase.Survived, d = FULL_DECK } = {}) {
  const s = Inh.startLife(l, 1n);
  return Inh.recordSurvival(s.Lineage, s.Life, { Layer: layer, NodeId: 2, Ending: ending }, d, j, opts);
}
const run = () => {
  let l = Inh.newLineage(99n);
  l = dieAt(l, 3, { seed: 5n });
  l = surviveAt(l, 4);
  const s = Inh.startLife(l);
  return Inh.serialize(Inh.takeTrace(s.Lineage, 'trace-1', ['memo']).Lineage);
};
out.r23 = run();
let l = dieAt(Inh.newLineage(42n), 4, { j: { ...FULL_JOURNAL, Pages: [{ Id: 'r-2', Title: '崩れた天井の頁', Text: '大穴の下は陽が届く' }, 'page-x'], Scribbles: [{ Generation: 1, Name: 'X', Text: '古い' }] } });
l = surviveAt(l, 2, { j: { Disclosure: { '10': 1, b: 2, '2': 1 }, Memo: '生存者' } });
const s = Inh.startLife(l, 123456789012345678n);
const r = Inh.takeTrace(s.Lineage, 'trace-1', ['card:tsuki', 'memo', 'page:r-2'], { Layer: 4 });
out.mixed = [Inh.serialize(l), Inh.serialize(s.Lineage), Inh.serialize(r.Lineage), JSON.stringify(r.Taken)];
out.mixedStart = JSON.stringify({ Journal: s.Journal, JournalFrom: s.JournalFrom, heir: Inh.heirJournal(s.Journal, s.JournalFrom), TraceLayers: s.TraceLayers, ToolSlots: s.ToolSlots, DeckMax: s.DeckMax, Seed: String(s.Seed) });
let c = surviveAt(Inh.newLineage(42n), 7, { ending: RunPhase.Completed, opts: { ExtraBossWon: true, RootCards: ['ne_watari', 'ne_shibari', { CardId: 'tsuki', Rank: 0 }] } });
out.cleared = Inh.serialize(c);
out.clearedHall = JSON.stringify(Inh.hallView(c));
out.clearedEnding = JSON.stringify(Inh.endingView(c));
// a long lineage with the hall's every block
let h = Inh.newLineage(42n);
h = dieAt(h, 3);
h = surviveAt(h, 2);
let hs = Inh.startLife(h, 1n);
let ht = Inh.takeTrace(hs.Lineage, 'trace-1', ['card:kansatsu'], { Layer: 3 });
h = Inh.recordDeath(ht.Lineage, null, { Layer: 5, NodeId: 3, NodeKind: 'Boss', Ending: RunPhase.MiasmaDeath }, FULL_DECK, { Memo: 'x'.repeat(25) });
out.hall = JSON.stringify(Inh.hallView(h));
out.ending = JSON.stringify(Inh.endingView(h));
out.hallLineage = Inh.serialize(h);
const hs2 = Inh.startLife(h, 9n);
out.traceNode = JSON.stringify(Inh.traceNodeView(hs2.Lineage, 5));

// the words
const T = Inh.InheritanceText;
const text = {};
for (const k of ['HallTitle','HallLead','HallEmpty','RoleName','StartButton','ToHallButton','TraceHeading','NoTraces','PastLivesHeading',
  'SurvivorHeading','NextHeirHeading','AttireHeading','TraceNodeTitle','TakeButton','LeaveButton','AlreadyTaken','Appears','Waits',
  'ClearedTraceNote','WithheldHeading','DungeonPageDetail','TierFirst','NothingLeft','JournalPasses','SurvivorJournal']) text[k] = T[k];
text.TraceRules = T.TraceRules;
text.KindLabel = T.KindLabel; text.EndingTitle = T.EndingTitle; text.NodeKindLabel = T.NodeKindLabel;
Object.assign(text, {
  GenerationLabel: T.GenerationLabel(3), HeirTitle: T.HeirTitle('ルカ'), HeirLine: T.HeirLine(3, 'ルカ'),
  LayerTitle: T.LayerTitle(2, '■■の秘跡'), RankDetail: T.RankDetail(2), DisclosureDetail: T.DisclosureDetail(1),
  ScribbleName: T.ScribbleName(4, 'ルカ'), RarePageTitle: T.RarePageTitle(7), WithheldReason: T.WithheldReason(3),
  PickLead: T.PickLead(3), PickCount: T.PickCount(1, 3), TraceOf: T.TraceOf(2, 'ルカ'), CandidateCount: T.CandidateCount(1, 2, 3),
  TierReason: T.TierReason(2, 'ルカ', 5), TierCleared: T.TierCleared(2, 'ルカ'),
  EndingSentence: ['MiasmaDeath', 'Fallen', 'Completed', 'Survived'].map((e) => T.EndingSentence(e, 6)),
  TraceLeft: T.TraceLeft('第 1 層　燦光の樹海'), TraceVisit: T.TraceVisit(4, 3), WithheldLine: T.WithheldLine(['a', 'b']),
  ClearedTraceLeft: T.ClearedTraceLeft('X'), SurvivorBonusLine: [T.SurvivorBonusLine(1), T.SurvivorBonusLine(2)],
  SurvivorReplaced: T.SurvivorReplaced(1, 'ルカ'), NextHeirLine: T.NextHeirLine(5, 'ルカ'), SurvivorTools: T.SurvivorTools(3, 4),
  SurvivorDeck: T.SurvivorDeck(40, 45), SurvivorDuration: [T.SurvivorDuration(1), T.SurvivorDuration(3)],
  SurvivorSpent: T.SurvivorSpent(2, 'ルカ'), SurvivorSpentLimits: T.SurvivorSpentLimits(3, 40),
  TraceStatus: [T.TraceStatus.None, T.TraceStatus.Untaken, T.TraceStatus.Taken(2, 'ルカ')],
});
out.text = text;
out.attire = Inh.AttireTiers;
out.heirNames = Inh.HeirNames;
// the survivor's bonus used up (R-14)
let sp = surviveAt(Inh.newLineage(42n), 3);
const sp2 = Inh.startLife(sp, 2n);
sp = Inh.recordDeath(sp2.Lineage, null, { Layer: 1, NodeId: 0, NodeKind: 'Battle', Ending: RunPhase.Fallen }, [], null);
out.hallSpent = JSON.stringify(Inh.hallView(sp));
out.hallSpentLineage = Inh.serialize(sp);
out.hallEmpty = JSON.stringify(Inh.hallView(Inh.newLineage(42n)));
console.log(JSON.stringify(out, null, 1));
