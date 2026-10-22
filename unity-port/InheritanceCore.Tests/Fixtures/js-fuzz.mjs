// Differential fuzz for InheritanceCore (FuzzParityTests): random op sequences (start, death, survival, end, take,
// carving, reload, heir journal) run through the trial's core/inheritance/Inheritance.js; every op and what the JS gave
// after it (the lineage, hall, ending and trace-node texts, the start and the take, or the refusal) is written out, and
// the C# replays the same ops. The big texts are written as "sha:" + the first 16 hex digits of their SHA-256 (UTF-8);
// --full writes them whole (to see a difference: INHERITANCE_FUZZ=<that file> dotnet test --filter FuzzParityTests).
// Regenerate (the trial's JS lives on branch origin/claude/nifty-dirac-a99nv7, folder prototypes/browser-game):
//   node js-fuzz.mjs <path to prototypes/browser-game> 40 11 > js-fuzz.json
import { pathToFileURL } from 'node:url';
import { createHash } from 'node:crypto';
const root = process.argv[2];
const N = Number(process.argv[3] ?? 40);
let rngState = Number(process.argv[4] ?? 1) >>> 0;
const FULL = process.argv.includes('--full');
const Inh = await import(pathToFileURL(root + '/src/core/inheritance/Inheritance.js').href);
const big = (text) => (FULL || text === null ? text : 'sha:' + createHash('sha256').update(text, 'utf8').digest('hex').slice(0, 16));
const ch = (code) => String.fromCharCode(code);

function rnd() { // mulberry32
  rngState = (rngState + 0x6D2B79F5) >>> 0;
  let t = rngState;
  t = Math.imul(t ^ (t >>> 15), t | 1);
  t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}
const int = (a, b) => a + Math.floor(rnd() * (b - a + 1));
const pick = (xs) => xs[Math.floor(rnd() * xs.length)];
const chance = (p) => rnd() < p;

const CARD_IDS = ['kansatsu', 'tsuki', 'harai', 'fumikomi', 'b', 'A', 'a', 'z', '10', '2', 'ne_watari', 'ne_shibari'];
const ENEMY_IDS = ['sabiyari', 'shoga', 'shokou', '10', '2', '01', 'Z', 'a', 'b'];
const MEMOS = ['', '門竜は押すと崩れる', 'あ'.repeat(30), '  ', ch(0x3000), 'x'.repeat(20), 'x'.repeat(21), 'メモ' + ch(10) + '二行目', ch(0xfeff),
  ch(0x85), ch(0x180e), ch(0x2028) + 'x', (ch(0xd83d) + ch(0xde00)).repeat(15), ('a' + ch(0xd83d) + ch(0xde00)).repeat(10),
  ch(0x2000) + ch(0x200a) + ch(0x205f)];

function genDeck() {
  if (chance(0.1)) return null;
  const ids = CARD_IDS.slice();
  const n = int(0, 6);
  const out = [];
  for (let i = 0; i < n && ids.length; i++) {
    const id = ids.splice(int(0, ids.length - 1), 1)[0];
    const e = { CardId: id, Rank: int(0, 3) };
    if (chance(0.2)) e.Name = pick(['名前', '別名', 'x']);
    if (chance(0.15)) e.Thresholds = pick([[2, 6, 12], [3, 8, 15], [1, 2, 3]]);
    out.push(e);
  }
  if (chance(0.03) && out.length) out.push({ ...out[0] }); // listed twice → error
  if (chance(0.03)) out.push({ CardId: 'bad', Rank: 4 }); // rank out of range → error
  return out;
}

function genJournal() {
  if (chance(0.15)) return null;
  const j = {};
  if (chance(0.8)) {
    const d = {};
    for (const id of ENEMY_IDS) if (chance(0.35)) d[id] = int(0, 2);
    if (chance(0.03)) d.bad = 3;
    j.Disclosure = d;
  }
  if (chance(0.8)) {
    if (chance(0.4)) j.Pages = int(0, 4);
    else {
      const pages = [];
      const n = int(0, 4);
      for (let i = 0; i < n; i++) {
        const r = rnd();
        if (r < 0.3) pages.push(`p${int(1, 9)}`);
        else {
          const p = { Id: `q${int(1, 9)}` };
          if (chance(0.6)) p.Title = pick(['崩れた天井の頁', '題']);
          if (chance(0.6)) p.Text = pick(['大穴の下は陽が届く', '  ', '', '文', ch(0x3000), ch(0x85)]);
          pages.push(p);
        }
      }
      j.Pages = pages;
    }
  }
  if (chance(0.7)) j.Memo = pick(MEMOS);
  if (chance(0.4)) {
    const n = int(0, 3);
    j.Scribbles = Array.from({ length: n }, () => ({ Generation: int(1, 4), Name: pick(['X', 'ルカ', '']), Text: pick(['古い', 'あ'.repeat(25), '']) }));
  }
  if (chance(0.15)) j.CarvingTaken = true;
  return j;
}

function genPlace(kind, lineage) {
  const p = { Layer: chance(0.03) ? pick([0, 8]) : int(1, 7) };
  if (chance(0.7)) p.NodeId = int(0, 12);
  if (chance(0.6)) p.NodeKind = pick(['Battle', 'Elite', 'Boss', 'Rest', 'Survey', 'Event', 'Trace', 'Carving']);
  const deaths = ['Fallen', 'MiasmaDeath'];
  const alive = ['Survived', 'Completed'];
  const r = rnd();
  if (r < 0.05) p.Ending = pick(['Exploring', 'LayerCleared']);
  else if (r < 0.12 && kind !== 'end') { /* absent: defaults */ }
  else if (kind === 'death') p.Ending = chance(0.95) ? pick(deaths) : pick(alive);
  else if (kind === 'survival') p.Ending = chance(0.95) ? pick(alive) : pick(deaths);
  else p.Ending = pick([...deaths, ...alive]);
  if (p.Ending === 'Completed' && chance(0.7)) p.Layer = 7;
  return p;
}

function genOpts() {
  const o = {};
  if (chance(0.4)) o.cardName = pick(['fmt', 'null']);
  if (chance(0.4)) o.enemyName = pick(['fmt', 'null']);
  if (chance(0.3)) o.RootCards = pick([['ne_watari', 'ne_shibari'], ['ne_watari', { CardId: 'tsuki', Rank: 0 }], [{ CardId: 'ne_x', Rank: 2, Name: '根' }]]);
  if (chance(0.3)) o.ExtraBossWon = true;
  return o;
}

function realOpts(o) {
  const r = {};
  if (o.cardName === 'fmt') r.cardName = (id, rank) => `${id}@${rank}`;
  if (o.cardName === 'null') r.cardName = () => null;
  if (o.enemyName === 'fmt') r.enemyName = (id) => `敵 ${id}`;
  if (o.enemyName === 'null') r.enemyName = () => null;
  if (o.RootCards) r.RootCards = o.RootCards;
  if (o.ExtraBossWon) r.ExtraBossWon = true;
  return r;
}

function snapshot(l) {
  const out = { lineage: big(Inh.serialize(l)), hall: big(JSON.stringify(Inh.hallView(l))), layers: Inh.traceLayers(l), heir: Inh.nextHeir(l) };
  try { out.ending = big(JSON.stringify(Inh.endingView(l))); } catch (e) { out.ending = 'ERR ' + e.message; }
  out.nodes = big(JSON.stringify([1, 2, 3, 4, 5, 6, 7].map((layer) => { const v = Inh.traceNodeView(l, layer); return v ? JSON.stringify(v) : null; })));
  return out;
}

const scenarios = [];
for (let n = 0; n < N; n++) {
  const seed = pick(['0', '1', '42', '99', '18446744073709551615', String(int(0, 1e9))]);
  let lineage = Inh.newLineage(seed);
  const steps = [];
  let lastStart = null;
  const len = int(4, 18);
  for (let i = 0; i < len; i++) {
    const cur = lineage.Current;
    let op;
    const r = rnd();
    if (!cur) {
      if (r < 0.8) op = { op: 'start', seed: chance(0.4) ? null : String(int(0, 1e6)) };
      else if (r < 0.85) op = { op: 'reload' };
      else if (r < 0.9) op = { op: 'carving' };
      else op = { op: 'end', kind: pick(['death', 'survival', 'end']), life: null, place: genPlace('end', lineage), deck: [], journal: null, opts: {} };
    } else {
      const tr = Inh.traceForLayer(lineage, int(1, 7)) ?? (lineage.Traces.length && chance(0.5) ? pick(lineage.Traces) : null);
      if (r < 0.3 && tr) {
        const ids = tr.Candidates.map((c) => c.Id);
        const picks = [];
        const k = int(0, 3);
        for (let x = 0; x < k && ids.length; x++) picks.push(ids.splice(int(0, ids.length - 1), 1)[0]);
        if (chance(0.05)) picks.push('nope');
        if (chance(0.03) && picks.length) picks.push(picks[0]);
        op = { op: 'take', traceId: tr.Id, picks: chance(0.05) ? null : picks };
        if (chance(0.5)) op.layer = chance(0.85) ? tr.Layer : int(1, 7);
      } else if (r < 0.35) {
        op = { op: 'carving' };
      } else if (r < 0.4) {
        op = { op: 'reload' };
      } else if (r < 0.45 && lastStart) {
        op = { op: 'heirJournal' };
      } else {
        const kind = pick(['death', 'death', 'survival', 'end', 'end']);
        const lifeR = rnd();
        const life = lifeR < 0.4 ? null : lifeR < 0.7 ? 'record' : lifeR < 0.95 ? cur.Generation : cur.Generation + 1;
        op = { op: 'end', kind, life, place: genPlace(kind, lineage), deck: genDeck(), journal: genJournal(), opts: genOpts() };
      }
    }
    const step = { ...op };
    try {
      switch (op.op) {
        case 'start': {
          const s = Inh.startLife(lineage, op.seed === null ? null : BigInt(op.seed));
          lastStart = s;
          lineage = s.Lineage;
          step.start = JSON.stringify({ Life: s.Life, Seed: String(s.Seed), ToolSlots: s.ToolSlots, ConsumableSlots: s.ConsumableSlots, DeckMin: s.DeckMin, DeckMax: s.DeckMax, Slots: s.Loadout.ToolSlotCount, Journal: s.Journal, JournalFrom: s.JournalFrom, SurvivorBonus: s.SurvivorBonus, TraceLayers: s.TraceLayers, CarvingKnown: s.CarvingKnown, Attire: s.Attire.Tier });
          break;
        }
        case 'reload': lineage = Inh.deserialize(Inh.serialize(lineage)); break;
        case 'carving': lineage = Inh.noteCarving(lineage); break;
        case 'heirJournal': step.result = JSON.stringify(Inh.heirJournal(lastStart.Journal, lastStart.JournalFrom)); break;
        case 'take': {
          const t = Inh.takeTrace(lineage, op.traceId, op.picks, op.layer === undefined ? {} : { Layer: op.layer });
          lineage = t.Lineage;
          step.result = JSON.stringify(t.Taken);
          break;
        }
        case 'end': {
          const life = op.life === 'record' ? lineage.Current : op.life;
          const f = op.kind === 'death' ? Inh.recordDeath : op.kind === 'survival' ? Inh.recordSurvival : Inh.recordEnd;
          lineage = f(lineage, life, op.place, op.deck, op.journal, realOpts(op.opts));
          break;
        }
        default: throw new Error('op');
      }
      Object.assign(step, snapshot(lineage));
    } catch (e) {
      step.error = `${e.name}: ${e.message}`;
    }
    steps.push(step);
  }
  scenarios.push({ seed, steps });
}
// a merge / mastery corner
const merges = [];
for (let n = 0; n < N * 2; n++) {
  const a = genJournal();
  const b = genJournal();
  const m = { a, b };
  try { m.result = JSON.stringify(Inh.mergeJournal(a, b)); } catch (e) { m.error = `${e.name}: ${e.message}`; }
  try { m.normalized = JSON.stringify(Inh.normalizeJournal(a)); } catch (e) { m.normalizedError = `${e.name}: ${e.message}`; }
  const from = chance(0.7) ? { Generation: int(1, 5), Name: pick(['ルカ', 'X']) } : null;
  m.from = from;
  try { m.heir = JSON.stringify(Inh.heirJournal(a, from)); } catch (e) { m.heirError = `${e.name}: ${e.message}`; }
  const recs = Array.from({ length: int(0, 4) }, () => ({ CardId: pick(CARD_IDS), Ticks: int(0, 20), Rank: int(0, 3) }));
  const records = new Map(recs.map((r) => [r.CardId, r]));
  const cards = Array.from({ length: int(0, 4) }, () => ({ CardId: pick(CARD_IDS), Ticks: int(0, 20), Rank: int(0, 3) }));
  m.records = [...records.values()];
  m.cards = cards;
  m.mastery = JSON.stringify([...Inh.mergeMastery(records, cards).values()]);
  merges.push(m);
}
console.log(JSON.stringify({ scenarios, merges }, null, FULL ? 0 : 1));
