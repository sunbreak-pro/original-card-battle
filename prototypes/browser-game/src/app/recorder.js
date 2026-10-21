// Save by replay. The cores are deterministic for a seed, so a life is saved as its start plus the
// ordered list of state-changing calls made on the Expedition, its ExplorationSession, the
// EncounterBattle and each CoreBattleSource. Loading rebuilds the Expedition and replays the calls.
// A fight in progress is saved with the rest (tier1 R1-16, 2026-10-12 #462: 「戦闘中にゲームを閉じたら、
// 閉じた場面から再開する」): its calls replay onto a source started from the same seed, so the hand, the
// piles, HP, stamina, Guard, statuses, omens, turn, phase and the random state come back as they were,
// and the app hands that source back to the battle scene (replayLife's `open`). A fight counts as
// finished the moment the core decides it (settleWhenDecided), not when the screen has played it out.
import { GameResult } from '../core/battle/Types.js';

// Calls that only read are not logged (logging them would be harmless but wasteful).
const READ_ONLY = new Set([
  'Screen', 'CanOpen', 'DisclosureOf', 'HandIn', 'PreviewFor', 'Inspect', 'RefusalText', 'HasPlayableCard',
  'toJSON', 'equals', 'constructor', 'NextLaunch', 'TryDescend', 'MapOf', 'SyncLife', 'CanUseSlot', 'EventAt', 'CanTeleport', 'TargetFor',
  'CanTakeFreeStep', 'ShownGapGlyph',
]);

/**
 * Wrap an object so calls to its methods are appended to `log` as [tag, method, args].
 * Methods run with `this` = the real object, so the object's own internal calls are not logged.
 * `children` maps a method name to [tag, wrapFn] for methods that return objects whose calls we also log.
 */
function wrap(obj, tag, log, children, onCall) {
  if (!obj || typeof obj !== 'object') return obj;
  return new Proxy(obj, {
    get(target, prop) {
      const v = target[prop];
      if (typeof v !== 'function' || typeof prop !== 'string') {
        if (children && children.props && children.props[prop]) return children.props[prop](v);
        return v;
      }
      return (...args) => {
        // Logged only once the call returns: a call that throws changed nothing and must not replay.
        const r = v.apply(target, args.map(argIn));
        if (!READ_ONLY.has(prop)) {
          push(log, tag, prop, args.map(argOut));
          if (onCall) onCall(tag, prop);
        }
        if (children && children.calls && children.calls[prop]) return children.calls[prop](r);
        return r;
      };
    },
  });
}
// Calls with no arguments repeat a lot (CoreBattleSource.Next per script event): a run of them is one
// entry [tag, method, [], n]. Replay expands it.
function push(log, tag, prop, args) {
  const last = log[log.length - 1];
  if (args.length === 0 && last && last[0] === tag && last[1] === prop && last[2].length === 0) {
    last[3] = (last[3] || 1) + 1;
    return;
  }
  log.push([tag, prop, args]);
}

// A wrapped source passed back as an argument (EncounterBattle.Finish(source)) is logged as a marker.
const RAW = Symbol('raw');
function argOut(a) { return a && a[RAW] ? '$source' : a; }
function argIn(a) { return a && a[RAW] ? a[RAW] : a; }

/**
 * Wrap an Expedition for recording. Returns { expedition, log, wrapSource }.
 * `onCall(tag, method)` runs after each logged call (the app saves then, so a fight in progress is in
 * the save after every move and every script event). `wrapSource(raw)` records a source the replay
 * started (replayLife's open fight) into the same log.
 */
export function recordExpedition(expedition, log = [], onCall = null) {
  const wrapSource = (src) => {
    if (!src) return src;
    const p = wrap(src, 'C', log, null, onCall);
    return new Proxy(p, { get(t, prop) { return prop === RAW ? src : t[prop]; } });
  };
  const wrapBattle = (b) => (b ? wrap(b, 'B', log, { calls: { StartNext: wrapSource } }, onCall) : b);
  const wrapSession = (s) => wrap(s, 'S', log, null, onCall);
  const ex = wrap(expedition, 'E', log, {
    calls: { StartEncounter: wrapBattle },
    props: { Session: wrapSession, Battle: wrapBattle },
  }, onCall);
  return { expedition: ex, log, wrapSource };
}

/**
 * Replay a log onto a fresh Expedition (unwrapped), fights in progress included. Returns
 * { kept, open }: `kept` the log as replayed (a copy), `open` the fight left in progress —
 * { battle, source }, the raw EncounterBattle and CoreBattleSource — or null. The source stands where
 * the last saved call left it (waiting for the player, or with script events still to play).
 */
export function replayLife(expedition, log) {
  const kept = log.map((e) => e.slice());
  let battle = null;
  let source = null;
  let fighting = false;
  for (const [tag, m, args, n] of kept) {
    for (let k = 0; k < (n || 1); k++) {
      const target = tag === 'E' ? expedition : tag === 'S' ? expedition.Session : tag === 'B' ? battle : source;
      if (!target || typeof target[m] !== 'function') throw new Error(`replay: cannot call ${tag}.${m}`);
      const r = target[m](...args.map((a) => (a === '$source' ? source : a)));
      if (tag === 'E' && m === 'StartEncounter') { battle = r; fighting = false; }
      if (tag === 'E' && (m === 'Abandon' || m === 'Return')) fighting = false;
      if (tag === 'B' && m === 'StartNext') { source = r; fighting = true; }
      if (tag === 'B' && m === 'Finish') fighting = false;
    }
  }
  const open = fighting && battle && source && expedition.Battle === battle ? { battle, source } : null;
  return { kept, open };
}

/** Replay a log onto a fresh Expedition (unwrapped); returns the log as replayed (replayLife's `kept`). */
export function replay(expedition, log) {
  return replayLife(expedition, log).kept;
}

/**
 * A fight is over the moment the core decides it (the call that brings HP to 0 returns), well before
 * the screen has played the fall and held the result card. Hand it to the encounter there and then
 * (EncounterBattle.Finish) and let the app save, so a reload during the fall or the result card can
 * neither revive a dead heir nor undo a won fight: tier1 R1-16 「死亡と生の終了は確定と同時に保存し…」,
 * its criterion 「死亡直後にプロセスを落として再開しても、死亡が確定し痕跡が残っている」, INHERITANCE.md R-21.
 *
 * `battle` and `source` are the recorded EncounterBattle and CoreBattleSource (so Finish is logged).
 * Returns { source, finish(), settled }: give the scene `source` (every call goes through to the recorded
 * one; after each, a decided fight is finished once); `finish()` finishes it if that has not happened yet
 * (the scene's onFinished, an unattended fight) and is a no-op after. Calls the scene still makes on the
 * source after the decision (the rest of the script) are logged after B.Finish and replay the same way.
 */
export function settleWhenDecided(battle, source, onSettled) {
  let done = false;
  const decided = () => {
    const st = source.State;
    return !!st && st.Result !== GameResult.Ongoing;
  };
  const finish = () => {
    if (done) return false;
    done = true;
    battle.Finish(source);
    if (onSettled) onSettled();
    return true;
  };
  const view = new Proxy(source, {
    get(target, prop) {
      const v = target[prop];
      if (typeof v !== 'function') return v;
      return (...args) => {
        const r = Reflect.apply(v, target, args);
        if (!done && decided()) finish();
        return r;
      };
    },
  });
  // A fight brought back by a reload that the core had already decided (its Finish never reached the
  // save) is finished at once.
  if (decided()) finish();
  return { source: view, finish, get settled() { return done; } };
}

/**
 * Drop calls after the last completed fight boundary if a fight was left unfinished: the fight starts
 * over. Only for a fight that cannot go on (its calls no longer replay, or the battle scene failed on
 * it); a reload keeps the fight (replayLife).
 */
export function trimUnfinished(log) {
  // Find the last 'B.StartNext' that has no matching 'B.Finish' after it.
  let lastStart = -1;
  let finishedAfter = true;
  for (let i = 0; i < log.length; i++) {
    const [tag, m] = log[i];
    if (tag === 'B' && m === 'StartNext') { lastStart = i; finishedAfter = false; }
    if (tag === 'B' && m === 'Finish') finishedAfter = true;
  }
  const kept = lastStart >= 0 && !finishedAfter ? log.slice(0, lastStart) : log.slice();
  return kept.map((e) => e.slice());
}
