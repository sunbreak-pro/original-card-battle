// Loads the port's modules by their TYPE_MAP paths. A suite declares what it imports; a module that is
// not there yet (or whose own imports are not there yet) makes the cases that need it "skipped".
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

export class Missing extends Error {
  constructor(what, reason) {
    super(`missing ${what}`);
    this.name = 'Missing';
    this.what = what;
    this.reason = reason || '';
  }
}

export function createLoader(root) {
  const loaded = new Map(); // rel → { ns } | { missing: Missing }

  function relOf(abs) {
    return path.relative(root, abs).split(path.sep).join('/');
  }

  async function load(rel) {
    if (loaded.has(rel)) return loaded.get(rel);
    const abs = path.join(root, rel);
    let entry;
    if (!fs.existsSync(abs)) {
      entry = { missing: new Missing(rel, 'file not written yet') };
    } else {
      try {
        entry = { ns: await import(pathToFileURL(abs).href) };
      } catch (e) {
        const m = /Cannot find module '([^']+)'/.exec(String(e && e.message));
        if (e && e.code === 'ERR_MODULE_NOT_FOUND' && m) {
          entry = { missing: new Missing(relOf(m[1]), `imported (directly or not) by ${rel}`) };
        } else {
          const where = e && e.stack ? String(e.stack).split('\n').slice(0, 2).join(' | ') : String(e);
          entry = { missing: new Missing(`${rel} (fails to load)`, where) };
        }
      }
    }
    loaded.set(rel, entry);
    return entry;
  }

  async function preload(rels) {
    for (const rel of rels) await load(rel);
  }

  /** The named exports of an already preloaded module; throws Missing when the module or an export is absent. */
  function use(rel, ...names) {
    const entry = loaded.get(rel);
    if (!entry) throw new Error(`harness bug: ${rel} was not preloaded`);
    if (entry.missing) throw entry.missing;
    const out = {};
    for (const n of names) {
      if (!(n in entry.ns) || entry.ns[n] === undefined) throw new Missing(`${rel} export ${n}`, 'module loads but does not export it');
      out[n] = entry.ns[n];
    }
    return out;
  }

  function status() {
    const rows = [];
    for (const [rel, e] of loaded) rows.push({ module: rel, ok: !e.missing, why: e.missing ? `${e.missing.what}${e.missing.reason ? ' — ' + e.missing.reason : ''}` : '' });
    return rows;
  }

  return { preload, use, status };
}
