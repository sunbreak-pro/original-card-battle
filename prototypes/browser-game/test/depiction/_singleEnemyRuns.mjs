// The one-enemy battles MultiEnemy.test.mjs replays to prove the multi-enemy writer leaves them alone.
//
// The snapshot (MultiEnemy.single.golden.json) was written from the code as it stood before the
// multi-enemy extension (docs/port-notes/depiction-multi-enemy.md), by running these scenarios on it:
//
//   node test/depiction/_singleEnemyRuns.mjs --write [srcRoot]
//
// Every screen event, every frame and every answer the source gives at a wait (Inspect, PreviewFor,
// RefusalText for each verdict, GuideText, SuggestedCardId, CanEndTurn) is serialized with every
// own member, keys sorted, and hashed. The golden keeps the hashes and the member names each type had
// then; the test serializes the new code's output, keeps only those members (the new ones are
// additions, checked on their own) and compares the hashes. `srcRoot` defaults to this game's src/.
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
export const GOLDEN = path.join(HERE, 'MultiEnemy.single.golden.json');

/** The port's modules this file drives, imported from `srcRoot` (so an older copy of src/ can be run too). */
export async function loadPort(srcRoot = path.resolve(HERE, '..', '..', 'src')) {
  const imp = (rel) => import(pathToFileURL(path.join(srcRoot, rel)).href);
  const [launch, types, script, turnLoop, rng, enemies, greedy, decks, writer, disclosure] = await Promise.all([
    imp('bridge/depiction/BattleLaunch.js'),
    imp('core/battle/Types.js'),
    imp('bridge/depictionScript/DepictionTypes.js'),
    imp('core/battle/TurnLoop.js'),
    imp('core/battle/IRng.js'),
    imp('core/battle/Enemies.js'),
    imp('sim/GreedyPlayer.js'),
    imp('sim/Decks.js'),
    imp('bridge/depiction/CoreScriptWriter.js'),
    imp('bridge/depictionScript/OmenDisclosure.js'),
  ]);
  return {
    BattleLaunch: launch.BattleLaunch,
    GameResult: types.GameResult,
    BattlePhase: types.BattlePhase,
    DepictionText: script.DepictionText,
    PlayVerdict: script.PlayVerdict,
    DropZone: script.DropZone,
    TurnLoop: turnLoop.TurnLoop,
    BattleSetup: turnLoop.BattleSetup,
    PlayRefusal: turnLoop.PlayRefusal,
    SeededRng: rng.SeededRng,
    Enemies: enemies.Enemies,
    GreedyPlayer: greedy.GreedyPlayer,
    Positioning: greedy.Positioning,
    Decks: decks.Decks,
    CoreScriptWriter: writer.CoreScriptWriter,
    OmenDisclosure: disclosure.OmenDisclosure,
  };
}

// ---- canonical JSON ---------------------------------------------------------------------------

/**
 * A value as plain JSON: every own enumerable member (no functions, no `_private`), keys sorted,
 * class instances tagged with `$t` (their class name). Getters are derived from the members and are
 * left out. Maps become sorted objects.
 */
export function canon(v, depth = 0) {
  if (depth > 60) throw new Error('canon: nested too deep');
  if (v === undefined || v === null) return null;
  if (typeof v === 'number') return Number.isFinite(v) ? v : String(v);
  if (typeof v === 'string' || typeof v === 'boolean') return v;
  if (typeof v === 'bigint') return v.toString();
  if (typeof v === 'function' || typeof v === 'symbol') return null;
  if (v instanceof Error) return { $error: v.name || (v.constructor && v.constructor.name), Message: String(v.message) };
  if (Array.isArray(v)) return v.map((x) => canon(x, depth + 1));
  if (v instanceof Map) {
    const o = {};
    for (const k of [...v.keys()].map(String).sort()) o[k] = canon(v.get(k), depth + 1);
    return o;
  }
  if (v instanceof Set) return [...v].map((x) => canon(x, depth + 1));
  const C = v.constructor;
  const name = C && C !== Object ? (C.$name || C.name) : null;
  const o = {};
  if (name) o.$t = name;
  for (const k of Object.keys(v).sort()) {
    if (k.startsWith('_') || typeof v[k] === 'function') continue;
    o[k] = canon(v[k], depth + 1);
  }
  return o;
}

/** Records the member names of every tagged object in `c` (a canon value) into `keys` ($t → Set). */
export function collectKeys(c, keys) {
  if (Array.isArray(c)) {
    for (const x of c) collectKeys(x, keys);
    return;
  }
  if (c === null || typeof c !== 'object') return;
  if (c.$t) {
    if (!keys.has(c.$t)) keys.set(c.$t, new Set());
    const set = keys.get(c.$t);
    for (const k of Object.keys(c)) if (k !== '$t') set.add(k);
  }
  for (const k of Object.keys(c)) collectKeys(c[k], keys);
}

/**
 * `c` with only the members the old types had (`keys`: $t → Set). A tagged object of a type the old
 * code did not have is a bug here (it can only sit under a new member, which is dropped first).
 */
export function project(c, keys, extra) {
  if (Array.isArray(c)) return c.map((x) => project(x, keys, extra));
  if (c === null || typeof c !== 'object') return c;
  const o = {};
  const known = c.$t ? keys.get(c.$t) : null;
  if (c.$t && !known) throw new Error(`project: type ${c.$t} did not exist in the one-enemy snapshot`);
  for (const k of Object.keys(c)) {
    if (k === '$t') {
      o.$t = c.$t;
      continue;
    }
    if (known && !known.has(k)) {
      if (extra) extra.add(`${c.$t}.${k}`);
      continue;
    }
    o[k] = project(c[k], keys, extra);
  }
  return o;
}

export function hash(c) {
  return crypto.createHash('sha1').update(JSON.stringify(c)).digest('hex').slice(0, 10);
}

// ---- the scenarios ----------------------------------------------------------------------------

/** Every one-enemy scenario: a list of { id, kind, ... } the runner below knows. */
export function scenarios(P) {
  const ids = P.Enemies.All.map((d) => d.Id);
  const list = [];
  // The unattended source, as EncounterBattle.StartNext(true) builds it, for every enemy.
  for (const id of ids) {
    for (const seed of [1, 2, 3, 4]) list.push({ id: `source:${id}|${seed}`, kind: 'source', launch: { EnemyId: id, Seed: seed, FieldCells: 7 } });
  }
  // Journal levels below 2, the slice's line, carried HP and stamina, the tools, a chain position.
  const variants = [
    { tag: 'd0', launch: { Disclosure: 0 } },
    { tag: 'd1', launch: { Disclosure: 1 } },
    { tag: 'narrow', launch: { FieldCells: 5 } },
    { tag: 'wide', launch: { FieldCells: 8, PlayerStartCell: 1, StartGap: 4 } },
    { tag: 'carried', launch: { StartHp: 23, StartStamina: 2, MaxStamina: 8, ChainIndex: 2, ChainTotal: 2 } },
    { tag: 'tools', launch: { StartStaminaBonus: 3, FirstHitGuard: 5, BossWordWard: 1, PlayerMaxHp: 70 } },
    { tag: 'limited', launch: { StopAfterTurns: 4 } },
  ];
  for (const id of ['polearm_warped', 'crossbow_hunter', 'armored_warden', 'pack_alpha', 'miasma_priest', 'abyss_angler', 'distortion_root']) {
    for (const v of variants) list.push({ id: `source:${id}|${v.tag}`, kind: 'source', launch: { EnemyId: id, Seed: 5, FieldCells: 7, ...v.launch } });
  }
  // The writer fed the greedy player's battles (the parity suite's writer:* cases, more seeds and decks).
  for (const id of ids) {
    for (const seed of [2, 3]) list.push({ id: `writer:${id}|${seed}`, kind: 'writer', enemy: id, seed, deck: seed === 2 ? 'Initial' : 'InitialForty' });
  }
  return list;
}

const SOURCE_MAX_MOVES = 4000;

/** Runs one scenario and hands back its records (canon values), in order. */
export function run(P, spec) {
  return spec.kind === 'source' ? runSource(P, spec) : runWriter(P, spec);
}

function runSource(P, spec) {
  const out = [];
  const launch = new P.BattleLaunch({ AutoPlay: true, ...spec.launch });
  const source = launch.CreateSource();
  out.push(canon({ open: source.Frame }));
  const verdicts = Object.values(P.PlayVerdict).filter((v) => typeof v === 'string');
  let guard = 0;
  while (!source.Finished && guard++ < SOURCE_MAX_MOVES) {
    const at = {
      Waiting: source.WaitingForPlayer, Finished: source.Finished, CanEndTurn: source.CanEndTurn,
      Guide: source.GuideText, Suggested: source.SuggestedCardId,
    };
    let ev;
    if (source.WaitingForPlayer) {
      at.Hand = source.Frame.Hand.map((face) => {
        const verdict = source.Inspect(face.Id);
        const refusals = {};
        for (const v of verdicts) refusals[v] = source.RefusalText(face.Id, v);
        return { Id: face.Id, Inspect: verdict, PreviewFor: source.PreviewFor(face.Id), Refusals: refusals };
      });
      const id = source.SuggestedCardId;
      if (id.length > 0) {
        const face = P.DepictionText.Find(source.Frame.Hand, id);
        const wrong = face.Aim === 'Single' ? P.DropZone.AboveThrowLine : P.DropZone.Receiver;
        at.Wrong = source.TryPlay(id, wrong).verdict;
        const r = source.TryPlay(id, P.DepictionText.RequiredZone(face.Aim));
        at.Verdict = r.verdict;
        ev = r.played;
      } else {
        ev = source.EndTurn();
      }
    } else {
      ev = source.AdvanceAuto();
    }
    out.push(canon({ at, ev }));
  }
  out.push(canon({ end: { Finished: source.Finished, Result: source.State.Result, Turn: source.State.Turn, History: source.History.length, Guard: guard } }));
  return out;
}

function runWriter(P, spec) {
  const out = [];
  const T = P;
  const deck = P.Decks[spec.deck]();
  const setup = new P.BattleSetup(P.Enemies.ById(spec.enemy), deck.Build(), 7);
  const writer = new P.CoreScriptWriter(setup.Enemy);
  const rng = new P.SeededRng(spec.seed);
  const player = new P.GreedyPlayer(P.Positioning.Neutral, 0);
  const write = (r) => {
    try {
      out.push(canon({ events: writer.Write(r.Events, r.State) }));
    } catch (e) {
      out.push(canon({ error: e }));
    }
  };
  let r = T.TurnLoop.Start(setup, rng);
  out.push(canon({ open: writer.Opening(r.State) }));
  let state = r.State;
  const ongoing = T.GameResult.Ongoing;
  while (state.Result === ongoing && state.Turn < 40) {
    r = T.TurnLoop.BeginPlayerTurn(state, rng);
    write(r);
    state = r.State;
    if (state.Result !== ongoing) break;
    const step = player.ChooseFreeStep(state);
    if (step !== 0) {
      r = T.TurnLoop.TakeFreeStep(state, step);
      write(r);
      state = r.State;
    }
    let played = 0;
    while (state.Result === ongoing) {
      const choice = player.Choose(state, played);
      if (choice == null) break;
      r = T.TurnLoop.PlayCard(state, choice.InstanceId, rng, choice.Target);
      write(r);
      state = r.State;
      played++;
    }
    if (state.Result !== ongoing) break;
    r = T.TurnLoop.EndTurn(state, rng);
    write(r);
    state = r.State;
  }
  out.push(canon({ end: { Result: state.Result, Turn: state.Turn } }));
  return out;
}

// ---- writing the golden -----------------------------------------------------------------------

async function main() {
  const args = process.argv.slice(2);
  if (!args.includes('--write')) {
    console.error('usage: node test/depiction/_singleEnemyRuns.mjs --write [srcRoot]');
    process.exit(2);
  }
  const root = args.find((a) => a !== '--write');
  const P = await loadPort(root ? path.resolve(root) : undefined);
  const keys = new Map();
  const golden = { note: 'One-enemy runs of the code before the multi-enemy extension (see _singleEnemyRuns.mjs).', keys: {}, scenarios: [] };
  const started = Date.now();
  for (const spec of scenarios(P)) {
    const records = run(P, spec);
    for (const c of records) collectKeys(c, keys);
    golden.scenarios.push({ id: spec.id, count: records.length, hashes: records.map(hash).join(' ') });
  }
  for (const [t, set] of [...keys.entries()].sort((a, b) => (a[0] < b[0] ? -1 : 1))) golden.keys[t] = [...set].sort();
  fs.writeFileSync(GOLDEN, JSON.stringify(golden, null, 1) + '\n');
  const n = golden.scenarios.reduce((s, x) => s + x.count, 0);
  console.log(`wrote ${path.relative(process.cwd(), GOLDEN)}: ${golden.scenarios.length} scenarios, ${n} records, ${((Date.now() - started) / 1000).toFixed(1)} s`);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((e) => {
    console.error(e);
    process.exit(1);
  });
}
