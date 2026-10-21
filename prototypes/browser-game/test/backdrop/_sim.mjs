// Shared by the Sim tests (Decks / GreedyPlayer): loads the port's modules dynamically so a module
// another agent has not written yet makes the tests skip ("needs <module>") instead of crashing, and
// holds the bits of BattleCore.Sim the C# tests use that are not ours to port (Runner.Setup /
// FieldCellsFor / SeedFor / StableHash, Bench.MainDecks), restated from Runner.cs / Bench.cs.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');

/** Imports the given src-relative modules; returns { mods, missing } with missing = 'needs <module>' or false. */
export async function loadModules(rels) {
  const mods = {};
  for (const rel of rels) {
    const abs = path.join(root, 'src', rel);
    try {
      mods[rel] = await import(pathToFileURL(abs).href);
    } catch (e) {
      const m = /Cannot find module '([^']+)'/.exec(String(e && e.message));
      const what = m ? path.relative(root, m[1]).split(path.sep).join('/') : `src/${rel} (${String(e && e.message).split('\n')[0]})`;
      return { mods, missing: `needs ${what}` };
    }
  }
  return { mods, missing: false };
}

/** A module that loaded but lacks an export the test needs. */
export function needExports(mods, rel, names) {
  const ns = mods[rel];
  for (const n of names) if (!ns || ns[n] === undefined) return `needs src/${rel} export ${n}`;
  return false;
}

/** Runner.StableHash: a 32-bit FNV-1a over the UTF-16 code units, masked to a non-negative int. */
export function StableHash(text) {
  let hash = 2166136261 >>> 0;
  for (let i = 0; i < text.length; i++) {
    hash = (hash ^ text.charCodeAt(i)) >>> 0;
    hash = Math.imul(hash, 16777619) >>> 0;
  }
  return hash & 0x7fffffff;
}

/** Runner.SeedFor(baseSeed, kind, enemyId, run). */
export function SeedFor(baseSeed, kind, enemyId, run) {
  return StableHash(`${baseSeed}|${kind}|${enemyId}|${run}`);
}

/** Runner.FieldCellsFor(enemies). */
export function FieldCellsFor(BattleSetup, Constants, enemies) {
  return Math.max(BattleSetup.SliceFieldCells, Constants.PlayerStartCell + Constants.StartGap + enemies.reduce((s, e) => s + e.Size, 0));
}

/** Runner.Setup(deck, enemy): PlayerMaxStamina = BaseMaxStamina, no carried HP / stamina. */
export function Setup(BattleSetup, Constants, deck, enemy) {
  return new BattleSetup(enemy, deck.Build(), FieldCellsFor(BattleSetup, Constants, [enemy]), {
    PlayerMaxStamina: Constants.BaseMaxStamina,
    PlayerStartHp: null,
    PlayerStartStamina: null,
  });
}

/** Bench.MainDecks(): the archetypes, cost-one, the no-stance twins, initial, initial forty, plain. */
export function MainDecks(Decks) {
  return [...Decks.Archetypes, Decks.CostOneOnly(), ...Decks.NoStanceTwins(), Decks.Initial(), Decks.InitialForty(), Decks.Plain()];
}

/** Reads the given case ids (or the first `limit` cases matching `filter`) of an oracle suite file, line by line. */
export function oracleCases(suite, filter, limit = Infinity) {
  const file = path.join(root, 'oracle', 'out', `${suite}.json`);
  if (!fs.existsSync(file)) return null;
  const text = fs.readFileSync(file, 'utf8');
  const out = [];
  let start = 0;
  while (start < text.length && out.length < limit) {
    let end = text.indexOf('\n', start);
    if (end < 0) end = text.length;
    let line = text.slice(start, end).trim();
    start = end + 1;
    if (!line.startsWith('{')) continue;
    if (line.endsWith(',')) line = line.slice(0, -1);
    const idMatch = /^\{"id":"([^"]*)"/.exec(line);
    if (idMatch && !filter(idMatch[1])) continue;
    out.push(JSON.parse(line));
  }
  return out;
}

export function oracleShapes() {
  const file = path.join(root, 'oracle', 'out', 'shapes.json');
  return fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : null;
}
