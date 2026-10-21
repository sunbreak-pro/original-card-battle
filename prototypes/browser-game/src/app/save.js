// Browser storage for the lineage, the life in progress and the settings. Every access is guarded:
// storage can be missing or throw (private windows, previews), and the game must still run without it.
import * as Inh from '../core/inheritance/Inheritance.js';

export const SAVE_KEY = 'ocb.save.v1';
const SETTINGS_KEY = 'ocb.settings.v1';
const BROKEN_KEY = 'ocb.save.v1.broken';   // a save whose life would not replay, kept before it is dropped
const PROBE_KEY = 'ocb.probe';

export const DEFAULT_SETTINGS = Object.freeze({
  speed: 1.25,       // battle speed ×1.0 / ×1.25 / ×1.5; ×1.25 is Unity's default (BattleSpeed.Default)
  shortFx: false,    // 演出短縮
  master: 0.8,
  effects: 0.9,
  ambience: 0.6,
  muted: false,
});

function read(key) {
  try { return window.localStorage.getItem(key); } catch (e) { return null; }
}
function write(key, text) {
  try { window.localStorage.setItem(key, text); return true; } catch (e) { return false; }
}
function remove(key) {
  try { window.localStorage.removeItem(key); } catch (e) { /* storage unavailable */ }
}

// BigInt seeds and Map journals do not survive JSON as they are; tag them.
function replacer(_k, v) {
  if (typeof v === 'bigint') return { $big: v.toString() };
  if (v instanceof Map) return { $map: [...v.entries()] };
  if (v instanceof Set) return { $set: [...v] };
  return v;
}
function reviver(_k, v) {
  if (v && typeof v === 'object' && !Array.isArray(v)) {
    if ('$big' in v) return BigInt(v.$big);
    if ('$map' in v) return new Map(v.$map);
    if ('$set' in v) return new Set(v.$set);
  }
  return v;
}

// Every write is stamped { rev, tab }: a counter and the id of the page that wrote it. A page keeps the
// stamp of the save it last read or wrote, and before it writes it checks that the stored stamp is
// still that one; if another tab of the game has written since, this page's save is stale and must not
// be written over the newer one (it would bring back a recorded death, wipe a trace, or let a taken
// trace be taken again: tier1 R1-16, INHERITANCE.md R-21). The stamp leads the stored text, so it is
// read without parsing the whole save.
const STAMP_RE = /^\{"v":1,"rev":(\d+),"tab":"([^"\\]*)"/;

/** The stamp of a stored text: null when nothing is stored, { rev: 0, tab: '' } for a save written without one. */
export function stampOf(text) {
  if (!text) return null;
  const m = STAMP_RE.exec(text);
  return m ? { rev: Number(m[1]), tab: m[2] } : { rev: 0, tab: '' };
}

/** The stamp of the save as it is stored now (see stampOf). */
export function storedStamp() { return stampOf(read(SAVE_KEY)); }

/** Whether two stamps name the same write (null = nothing stored). */
export function sameStamp(a, b) {
  if (!a || !b) return !a && !b;
  return a.rev === b.rev && a.tab === b.tab;
}

/**
 * Reads the stored save: { save, unreadable, stamp }. `stamp` is the stored text's (stampOf).
 * save: { lineage, life: { start, log, memo } | null, deckSave } or null when nothing loads.
 * unreadable: null, or { error, kept } when a save is stored but does not load (another build's version,
 * a write cut off half way). Nothing is dropped then: the text stays under its key until the player
 * starts anew, and is first copied aside (keepBrokenSave; kept says whether the copy was written), so a
 * lineage the player cannot reload any other way (no load slots, CAMP_FACILITIES_DESIGN.md §3.5) is
 * never lost without a word.
 */
export function openSave() {
  const text = read(SAVE_KEY);
  if (!text) return { save: null, unreadable: null, stamp: null };
  const stamp = stampOf(text);
  try {
    const data = JSON.parse(text, reviver);
    // writeSave never stores a save without its lineage: one without it is broken, not empty.
    if (!data || typeof data !== 'object' || !data.lineage) throw new Error('bad save: no lineage');
    const lineage = Inh.fromJSON(data.lineage);
    return { save: { lineage, life: data.life || null, deckSave: typeof data.deckSave === 'string' ? data.deckSave : null }, unreadable: null, stamp };
  } catch (e) {
    const kept = keepBrokenSave();
    console.warn('save could not be read; it is ' + (kept ? 'kept under ' + BROKEN_KEY : 'not copied (storage refused)') + ' until the player starts anew', e);
    return { save: null, unreadable: { error: e, kept }, stamp };
  }
}

/** { lineage, life, deckSave } or null when nothing is saved or it does not load (openSave says which). */
export function loadSave() { return openSave().save; }

/** Stores the save, stamped with `stamp` ({ rev, tab }) when given. Returns whether storage took it. */
export function writeSave(save, stamp = null) {
  if (!save || !save.lineage) return false;
  const head = stamp ? { v: 1, rev: stamp.rev | 0, tab: String(stamp.tab).replace(/["\\]/g, '') } : { v: 1 };
  return write(SAVE_KEY, JSON.stringify({ ...head, lineage: Inh.toJSON(save.lineage), life: save.life || null, deckSave: save.deckSave || null }, replacer));
}

/** Whether this browser keeps what the game writes (a probe written and removed). False in a window
 *  whose storage is blocked or missing: the game still plays, but nothing outlives the page. */
export function storageWorks() {
  if (!write(PROBE_KEY, '1')) return false;
  remove(PROBE_KEY);
  return true;
}

/** Copies the save as it is stored now to a second key, so a life that no longer replays can be dropped
 *  without losing its record (a later build may read it again). Returns whether a copy was written. */
export function keepBrokenSave() {
  const text = read(SAVE_KEY);
  return text ? write(BROKEN_KEY, text) : false;
}

/** The copy keepBrokenSave made, as stored text, or null. */
export function readBrokenSave() { return read(BROKEN_KEY); }

export function clearLife(save) {
  if (!save) return save;
  save.life = null;
  writeSave(save);
  return save;
}

export function clearAll() { remove(SAVE_KEY); }

export function loadSettings() {
  const text = read(SETTINGS_KEY);
  if (!text) return {};
  try {
    const s = JSON.parse(text);
    const out = {};
    for (const k of Object.keys(DEFAULT_SETTINGS)) if (k in s && typeof s[k] === typeof DEFAULT_SETTINGS[k]) out[k] = s[k];
    return out;
  } catch (e) { return {}; }
}

export function writeSettings(settings) {
  const out = {};
  for (const k of Object.keys(DEFAULT_SETTINGS)) out[k] = settings[k];
  return write(SETTINGS_KEY, JSON.stringify(out));
}

export { replacer as saveReplacer, reviver as saveReviver };
