#!/usr/bin/env node
// Writes TraceGain.parity.txt: what the browser trial's TraceGain (src/bridge/exploration/TraceGain.js, with the take of
// src/app/life.js Expedition.prototype.Inherit) gives over a grid of heirs, trace rows, picks and modes. TraceGainTests'
// TraceGains_AgreeWithTheJs_OverTheGrid builds the same grid in C# and compares every row as JSON. The grid below and
// the one in TraceGainTests.cs (ParityGrid) must stay the same.
//
// Usage (the JS source is the branch origin/claude/nifty-dirac-a99nv7, folder prototypes/browser-game):
//   node TraceGain.parity.mjs <path to prototypes/browser-game> > TraceGain.parity.txt
import { pathToFileURL } from 'node:url';
import { join } from 'node:path';

const root = process.argv[2];
if (!root) throw new Error('usage: node TraceGain.parity.mjs <prototypes/browser-game>');
const load = (p) => import(pathToFileURL(join(root, p)).href);
const { Expedition } = await load('src/bridge/exploration/Expedition.js');
await load('src/app/life.js'); // Expedition.prototype.Inherit
const { toRunLoadout } = await load('src/bridge/exploration/Loadouts.js');
const { RunStart } = await load('src/bridge/explorationScript/ExplorationSession.js');
const { Loadout } = await load('src/core/dungeonContent/Loadout.js');
const { DeckLimits } = await load('src/core/battle/DeckRules.js');
const { CardMastery } = await load('src/core/battle/Mastery.js');
const { traceGains } = await load('src/bridge/exploration/TraceGain.js');

// ---- the grid (TraceGainTests.cs ParityGrid) ----
const CAPS = [null, 20, 21];
const RANKS = [[], [['thrust', 3], ['brace', 2]], [['feint', 2], ['kesa_cut', 1], ['rend', 3]]];
const PICKS = [
  [],
  ['card:feint'],
  ['card:thrust', 'card:feint', 'enemy:shadow_hound'],
  ['card:no_such_card', 'card:boar_rush', 'scribble:0'],
  ['card:feint', 'card:feint', 'memo', 'page:page-1', 'enemy:polearm_warped'],
];
const MODES = ['preview', 'done', 'take'];
const CARDS = ['thrust', 'feint', 'brace', 'kesa_cut', 'boar_rush', 'rend', 'no_such_card'];
const ENEMIES = [['shadow_hound', 1], ['polearm_warped', 2], ['rusted_revenant', 1]];

function candidates() {
  const rows = [];
  CARDS.forEach((id, i) => {
    const rank = (i % 3) + 1;
    const row = { Id: `card:${id}`, Kind: 'Card', KindLabel: '技術', Name: `trace-${id}`, Detail: `${rank} 段目` };
    if (i % 2 === 0) Object.assign(row, { CardId: id, Rank: rank });
    rows.push(row);
  });
  ENEMIES.forEach(([id, level], i) => {
    const row = { Id: `enemy:${id}`, Kind: 'EnemyPage', KindLabel: '敵の頁', Name: `page-${id}`, Detail: `開示度 ${level} / 2` };
    if (i % 2 === 0) Object.assign(row, { EnemyId: id, Disclosure: level });
    rows.push(row);
  });
  rows.push({ Id: 'page:page-1', Kind: 'DungeonPage', KindLabel: '頁', Name: '希少な頁 1', Detail: '手記の頁', PageId: 'page-1' });
  rows.push({ Id: 'scribble:0', Kind: 'Scribble', KindLabel: '走り書き', Name: '第 1 代', Detail: '', Generation: 1, From: 'A', Text: 'memo' });
  rows.push({ Id: 'memo', Kind: 'Scribble', KindLabel: '走り書き', Name: '第 2 代', Detail: null, Generation: 2, From: 'B', Text: 'mine' });
  return rows;
}

let index = 0;
for (const cap of CAPS) {
  for (const ranks of RANKS) {
    for (const picks of PICKS) {
      for (const mode of MODES) {
        const some = index % 2 === 1; // every other scenario: a journal and a 走り書き the life holds already
        const journal = some ? { shadow_hound: 1, polearm_warped: 2 } : null;
        const held = some ? [{ Generation: 1, Name: 'A', Text: 'memo' }] : [];
        const limits = cap == null ? null : new DeckLimits({ DeckMax: cap });
        const start = new RunStart({ Seed: 3n, Loadout: toRunLoadout(Loadout.Of([], [], 3), 3) });
        const ex = new Expedition(start, null, null, null, null, journal, { trial: true, limits });
        ex.Mastery = ex.Mastery.with({ Records: new Map(ranks.map(([id, r]) => [id, new CardMastery(id, [0, 3, 8, 15][r], r)])) });
        const rows = candidates();
        let gains;
        if (mode === 'preview') gains = traceGains(ex, rows, picks, { scribbles: held });
        else if (mode === 'done') gains = traceGains(ex, rows, picks, { done: true, scribbles: held });
        else {
          const unique = [...new Set(picks)];
          const picked = unique.map((id) => rows.find((c) => c.Id === id));
          const cards = picked.filter((c) => c.Kind === 'Card' && c.Id !== 'card:no_such_card')
            .map((c) => ({ CardId: c.Id.slice(5), Rank: Number(c.Detail[0]), Ticks: 0, Name: c.Id.slice(5) }));
          const disclosure = Object.fromEntries(picked.filter((c) => c.Kind === 'EnemyPage').map((c) => [c.Id.slice(6), Number(c.Detail.split(' ')[1])]));
          const pages = picked.filter((c) => c.Kind === 'DungeonPage').map((c) => ({ Id: c.PageId, Title: c.Name }));
          const result = ex.Inherit({ TraceId: 'trace-1', Cards: cards, Journal: { Disclosure: disclosure, Pages: pages, Memo: '', Scribbles: [] }, Lost: 0 });
          const list = held.slice();
          const scribbleRows = [];
          for (const c of picked.filter((x) => x.Kind === 'Scribble')) {
            const dup = list.some((t) => t.Generation === c.Generation && t.Text === c.Text);
            if (!dup) list.push({ Generation: c.Generation, Name: c.From, Text: c.Text });
            scribbleRows.push({ Generation: c.Generation, Name: c.From, Text: c.Text, Added: !dup });
          }
          gains = traceGains(ex, rows, picks, { done: true, result: { ...result, Scribbles: scribbleRows }, scribbles: held });
        }
        const out = [...gains].map(([id, g]) => [id, g.Name, g.Detail, [...g.Notes], g.Nothing,
          g.Result ? g.Result.From : null, g.Result ? g.Result.To : null]);
        console.log(JSON.stringify([index, out]));
        index++;
      }
    }
  }
}
