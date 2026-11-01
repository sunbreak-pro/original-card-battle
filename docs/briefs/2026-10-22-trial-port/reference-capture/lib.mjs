// Shared helpers for the reference captures (Playwright, 1920x1080, deviceScaleFactor 1).
import { chromium } from 'file:///C:/Users/user/AppData/Roaming/npm/node_modules/@playwright/mcp/node_modules/playwright/index.mjs';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const REF = path.dirname(fileURLToPath(import.meta.url));
export const SHOTS = path.join(REF, 'shots');
// A copy of proto/.../dist with one line added to game.js: window.__scene = the mounted scene (read-only use:
// battle geometry and player state). Everything else is the build as shipped.
export const GAME = pathToFileURL(path.join(REF, 'game', 'dev.html')).href;
fs.mkdirSync(SHOTS, { recursive: true });

export const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** The index lines this run adds (file | screen | state | how). Appended to shots/_index-<script>.tsv. */
const rows = [];
let indexFile = null;
export function setIndexFile(name) { indexFile = path.join(SHOTS, '_index-' + name + '.tsv'); fs.writeFileSync(indexFile, ''); }
export function note(file, screen, state, how) {
  rows.push([file, screen, state, how]);
  if (indexFile) fs.appendFileSync(indexFile, [file, screen, state, how].join('\t') + '\n');
  console.log('  shot', file, '|', state);
}

export async function launch({ query = 'debug', storage = null, navigate = true } = {}) {
  // The bundled playwright asks for build 1247; build 1243 is what is installed (same Chromium line).
  const exe = path.join(process.env.LOCALAPPDATA, 'ms-playwright', 'chromium_headless_shell-1243', 'chrome-headless-shell-win64', 'chrome-headless-shell.exe');
  const browser = await chromium.launch({ headless: true, executablePath: fs.existsSync(exe) ? exe : undefined, args: ['--autoplay-policy=no-user-gesture-required'] });
  const context = await browser.newContext({ viewport: { width: 1920, height: 1080 }, deviceScaleFactor: 1, reducedMotion: 'no-preference' });
  if (storage) {
    await context.addInitScript((s) => {
      if (sessionStorage.getItem('__seeded')) return;
      sessionStorage.setItem('__seeded', '1');
      for (const [k, v] of Object.entries(s)) localStorage.setItem(k, v);
    }, storage);
  }
  await context.addInitScript(MOUNT_WATCH);
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
  page.on('console', (m) => { if (m.type() === 'error') errors.push('console: ' + m.text()); });
  if (navigate) await page.goto(GAME + (query ? '?' + query : ''));
  return { browser, context, page, errors };
}

/** Reload the same origin with another query (the save stays in localStorage). */
export async function reopen(page, query) {
  await page.goto(GAME + (query ? '?' + query : ''));
}

export async function fontsReady(page) {
  await page.evaluate(() => (document.fonts && document.fonts.ready ? document.fonts.ready.then(() => true) : true));
}

/** Screenshot to shots/<file>.png after the fonts are ready. */
export async function shot(page, file, screen, state, how, { fonts = true, clip = null } = {}) {
  if (fonts) await fontsReady(page);
  const p = path.join(SHOTS, file + '.png');
  await page.screenshot(clip ? { path: p, clip } : { path: p });
  note(file + '.png', screen, state, how);
  return p;
}

/** Stage px → client px (the stage is 1:1 at 1920x1080, but read it anyway). */
export async function S(page, x, y) {
  return page.evaluate(([x, y]) => {
    const st = document.querySelector('.ocbg-stage');
    const r = st.getBoundingClientRect();
    const k = r.width / 1920;
    return [r.left + x * k, r.top + y * k];
  }, [x, y]);
}

/** The centre (client px) of the first element matching `sel` (optionally the n-th). */
export async function centerOf(page, sel, n = 0) {
  return page.evaluate(([sel, n]) => {
    const el = document.querySelectorAll(sel)[n];
    if (!el) return null;
    const r = el.getBoundingClientRect();
    return [r.left + r.width / 2, r.top + r.height / 2];
  }, [sel, n]);
}
export async function hoverSel(page, sel, n = 0, steps = 4) {
  const c = await centerOf(page, sel, n);
  if (!c) throw new Error('no element ' + sel);
  await page.mouse.move(c[0], c[1], { steps });
  return c;
}
export async function clickSel(page, sel, n = 0) {
  const c = await hoverSel(page, sel, n);
  await page.mouse.click(c[0], c[1]);
  return c;
}

export async function waitSel(page, sel, timeout = 20000) {
  await page.waitForSelector(sel, { timeout, state: 'attached' });
}

/**
 * Frame sequence through the DevTools screencast: starts streaming, runs `action`, keeps every frame for
 * `durationMs`, then saves the frames nearest each of `atMs` (ms after `action` started; the frame at or just
 * after the target) as <base>-<t>ms.png. Returns the saved list with the real offset of each frame.
 */
export async function frames(page, base, atMs, durationMs, action, meta) {
  const cast = await startCast(page);
  await sleep(250);
  const t0 = Date.now();
  if (action) await action();
  const tAct = Date.now();
  await sleep(Math.max(0, durationMs - (tAct - t0)));
  const got = await cast.stop();
  return saveFrames(got, t0, base, atMs, meta);
}

/** Starts the DevTools screencast; stop() returns the frames [{ t (epoch ms), data (png base64) }]. */
export async function startCast(page) {
  const cdp = await page.context().newCDPSession(page);
  const got = [];
  cdp.on('Page.screencastFrame', (f) => {
    got.push({ t: f.metadata.timestamp * 1000, data: f.data });
    cdp.send('Page.screencastFrameAck', { sessionId: f.sessionId }).catch(() => {});
  });
  await cdp.send('Page.startScreencast', { format: 'png', maxWidth: 1920, maxHeight: 1080, everyNthFrame: 1 });
  return {
    got,
    async stop() {
      await cdp.send('Page.stopScreencast').catch(() => {});
      await cdp.detach().catch(() => {});
      return got;
    },
  };
}

/** Saves the frame the screen showed at t0 + each of atMs (the last frame at or before it). */
export function saveFrames(got, t0, base, atMs, meta) {
  const saved = [];
  for (const at of atMs) {
    const target = t0 + at;
    let best = null;
    for (const f of got) if (f.t <= target + 8 && (!best || f.t > best.t)) best = f;
    if (!best) best = got.find((f) => f.t >= target) || null;
    if (!best) continue;
    const file = `${base}-${String(at).padStart(4, '0')}ms`;
    fs.writeFileSync(path.join(SHOTS, file + '.png'), Buffer.from(best.data, 'base64'));
    const real = Math.round(best.t - t0);
    note(file + '.png', meta.screen, `${meta.state} (target ${at} ms, frame shown at ${real} ms)`, meta.how);
    saved.push({ file, at, real });
  }
  console.log('  screencast frames:', got.length, 'saved', saved.length);
  return saved;
}

/** Init script: every scene root attached is logged as { cls, t } (epoch ms) in window.__mounts. */
export const MOUNT_WATCH = () => {
  // scene roots, and the battle's transient pieces (banner, floating number, refusal line, result card)
  const ROOTS = ['ocbg-meta-title', 'ocbg-meta-hall', 'ocbg-meta-departure', 'ocbg-explore', 'ocbg-battle', 'ocbg-meta-ending',
    'ocbg-battle-banner', 'ocbg-battle-float', 'ocbg-battle-refusal', 'ocbg-battle-result', 'ocbg-battle-chainbanner', 'ocbg-explore-plate', 'ocbg-confirm'];
  // class switches: [element class, state class]
  const ONS = [['ocbg-explore-banner', 'is-on'], ['ocbg-explore-walker', 'is-walking'], ['ocbg-explore-walker', 'is-leaping']];
  window.__mounts = [];
  const txt = (el) => (el.textContent || '').replace(/\s+/g, ' ').trim().slice(0, 40);
  const mo = new MutationObserver((list) => {
    const t = performance.timeOrigin + performance.now();
    for (const m of list) {
      if (m.type === 'attributes') {
        const el = m.target;
        if (!el.classList) continue;
        const was = String(m.oldValue || '').split(/\s+/);
        for (const [c, on] of ONS) if (el.classList.contains(c) && el.classList.contains(on) && !was.includes(on)) window.__mounts.push({ cls: c + '.' + on, t, text: txt(el) });
        continue;
      }
      for (const n of m.addedNodes) {
        if (!n.classList) continue;
        for (const c of ROOTS) {
          if (n.classList.contains(c)) window.__mounts.push({ cls: c, t, text: txt(n) });
          else if (n.querySelector && n.querySelector('.' + c)) window.__mounts.push({ cls: c, t, text: txt(n.querySelector('.' + c)) });
        }
      }
    }
  });
  mo.observe(document, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'], attributeOldValue: true });
};
/** Marks of `cls` logged after `after` (epoch ms): [{ cls, t, text }]. */
export async function marks(page, cls, after = 0) {
  const list = await page.evaluate(([cls, after]) => (window.__mounts || []).filter((x) => x.cls === cls && x.t > after), [cls, after]);
  return list.filter((m, i) => i === 0 || !(Math.abs(m.t - list[i - 1].t) < 30 && m.text === list[i - 1].text));
}
/** Epoch ms of the first mount of scene root `cls` after `after` (epoch ms). */
export async function mountedAt(page, cls, after = 0, timeout = 30000) {
  const h = await page.waitForFunction(([cls, after]) => {
    const m = (window.__mounts || []).find((x) => x.cls === cls && x.t > after);
    return m ? m.t : null;
  }, [cls, after], { timeout });
  return h.jsonValue();
}
export async function pageNow(page) { return page.evaluate(() => performance.timeOrigin + performance.now()); }

/** localStorage as a plain object (a checkpoint of the save). */
export async function dumpStorage(page) {
  return page.evaluate(() => { const o = {}; for (let i = 0; i < localStorage.length; i++) { const k = localStorage.key(i); o[k] = localStorage.getItem(k); } return o; });
}
export function saveCheckpoint(name, storage) { fs.writeFileSync(path.join(REF, 'ckpt-' + name + '.json'), JSON.stringify(storage)); }
export function loadCheckpoint(name) { return JSON.parse(fs.readFileSync(path.join(REF, 'ckpt-' + name + '.json'), 'utf8')); }

// ------------------------------------------------------------------ exploration helpers
/** Waits until the explore scene is up and idle; returns which card (plate) is up. */
export async function settleExplore(page, maxMs = 30000) {
  const t0 = Date.now();
  while (Date.now() - t0 < maxMs) {
    const st = await page.evaluate(() => {
      const root = document.querySelector('.ocbg-explore');
      const battle = document.querySelector('.ocbg-battle');
      if (battle) return { battle: true };
      const ending = document.querySelector('.ocbg-meta-ending');
      if (ending) return { ending: true };
      if (!root) return { busy: true };
      const plate = document.querySelector('.ocbg-explore-plate:not(.is-leaving)');
      const confirm = document.querySelector('.ocbg-confirm');
      return {
        busy: root.classList.contains('is-busy'),
        plate: plate ? plate.className : '',
        confirm: !!confirm,
        interlude: root.classList.contains('is-interlude'),
      };
    });
    if (st.battle || st.ending) return st;
    if (!st.busy) { await sleep(350); return st; }
    await sleep(120);
  }
  throw new Error('explore did not settle');
}

/** The session screen's nodes, read from the debug handle. */
export async function exploreInfo(page) {
  return page.evaluate(() => {
    const ex = window.__ocb.live && window.__ocb.live.raw;
    if (!ex) return null;
    const s = ex.Session;
    const scr = s.Screen();
    return {
      layer: s.State.Profile.Layer,
      phase: s.State.Phase,
      hp: s.State.Hp,
      current: scr.CurrentNodeId,
      nodes: scr.Nodes.map((n) => ({ id: n.Id, kind: n.Kind, standing: n.Standing, row: n.Row, col: n.Column, opts: n.Options.length, labels: n.Options.map((o) => o.Label) })),
    };
  });
}

export async function stepTo(page, id, option = 0) {
  const pinned = await page.$(`.ocbg-explore-node.is-pinned[data-node="${id}"]`);
  if (!pinned) { await clickSel(page, `.ocbg-explore-node[data-node="${id}"]`); await sleep(300); }
  const btns = await page.$$('.ocbg-explore-go');
  if (!btns[option]) throw new Error('no go button ' + option + ' for node ' + id);
  const box = await btns[option].boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2, { steps: 3 });
  await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
}

export function writeJSON(name, obj) { fs.writeFileSync(path.join(REF, name), JSON.stringify(obj, null, 2)); }
