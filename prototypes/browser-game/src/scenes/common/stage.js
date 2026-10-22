// Shared scene utilities: the 1920×1080 stage, a DOM builder, a frame-clock tween engine,
// a keyboard stack and the confirm card. Owned by the integrator; scenes import, never edit.

export const W = 1920;
export const H = 1080;

/** h('div', {class, style, onclick, ...attrs}, ...children) */
export function h(tag, props, ...kids) {
  const el = document.createElement(tag);
  if (props) {
    for (const k in props) {
      const v = props[k];
      if (v == null || v === false) continue;
      if (k === 'class') el.className = v;
      else if (k === 'text') el.textContent = v;
      else if (k === 'html') el.innerHTML = v;
      else if (k === 'style') {
        if (typeof v === 'string') el.style.cssText = v;
        else for (const s in v) el.style.setProperty(s, v[s]);
      } else if (k.startsWith('on') && typeof v === 'function') el.addEventListener(k.slice(2), v);
      else el.setAttribute(k, v);
    }
  }
  const add = (c) => {
    if (c == null || c === false) return;
    if (Array.isArray(c)) { c.forEach(add); return; }
    el.appendChild(typeof c === 'string' || typeof c === 'number' ? document.createTextNode(String(c)) : c);
  };
  kids.forEach(add);
  return el;
}

/** Place an element at stage coordinates. */
export function place(el, x, y, w, ht) {
  el.style.position = 'absolute';
  el.style.left = x + 'px';
  el.style.top = y + 'px';
  if (w != null) el.style.width = w + 'px';
  if (ht != null) el.style.height = ht + 'px';
  return el;
}

/**
 * The stage: a 1920×1080 box scaled uniformly to its container, letterboxed with --ground.
 * Scenes mount into `stage.layer()`; pointer events convert with `stage.toStage(e)`.
 */
export function createStage(container) {
  const viewport = h('div', { class: 'ocbg-viewport' });
  const root = h('div', { class: 'ocbg-stage ocb-root' });
  viewport.appendChild(root);
  container.appendChild(viewport);
  const st = { root, viewport, scale: 1, offX: 0, offY: 0 };
  function fit() {
    const w = viewport.clientWidth || window.innerWidth;
    const ht = viewport.clientHeight || window.innerHeight;
    st.scale = Math.min(w / W, ht / H);
    st.offX = (w - W * st.scale) / 2;
    st.offY = (ht - H * st.scale) / 2;
    root.style.transform = `translate(${st.offX}px, ${st.offY}px) scale(${st.scale})`;
  }
  if (window.ResizeObserver) new ResizeObserver(fit).observe(viewport);
  window.addEventListener('resize', fit);
  fit();
  st.fit = fit;
  /** Client → stage coordinates. */
  st.toStage = (e) => {
    const r = root.getBoundingClientRect();
    return { x: (e.clientX - r.left) / st.scale, y: (e.clientY - r.top) / st.scale };
  };
  /** A full-stage layer for a scene; returns the element. */
  st.layer = (cls) => {
    const l = h('div', { class: 'ocbg-scene ' + (cls || '') });
    root.appendChild(l);
    return l;
  };
  return st;
}

/** Frame clock + tweens + waits, all in stage time. One clock per scene; call clock.stop() on destroy. */
export const EASE = {
  out: (t) => 1 - Math.pow(1 - t, 3),
  in: (t) => t * t * t,
  inout: (t) => (t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2),
  linear: (t) => t,
  smooth: (t) => t * t * (3 - 2 * t),
};
export function createClock(onFrame) {
  let now = performance.now();
  let tweens = [];
  let timers = [];
  let alive = true;
  let raf = 0;
  const clock = {
    get now() { return now; },
    tween(obj, to, ms, ease, delay) {
      return new Promise((res) => {
        const keys = Object.keys(to);
        tweens = tweens.filter((tw) => {
          if (tw.obj !== obj) return true;
          keys.forEach((k) => delete tw.to[k]);
          if (!Object.keys(tw.to).length) { tw.res(); return false; }
          return true;
        });
        tweens.push({ obj, to: { ...to }, from: null, ms: Math.max(0, ms), ease: typeof ease === 'function' ? ease : EASE[ease] || EASE.out, t0: now + (delay || 0), res });
      });
    },
    wait(ms) { return new Promise((res) => timers.push({ at: now + ms, res })); },
    stop() { alive = false; cancelAnimationFrame(raf); tweens.forEach((t) => t.res()); timers.forEach((t) => t.res()); tweens = []; timers = []; },
  };
  function frame(t) {
    if (!alive) return;
    const dt = Math.min(50, t - now);
    now = t;
    const done = [];
    tweens = tweens.filter((tw) => {
      if (now < tw.t0) return true;
      if (!tw.from) { tw.from = {}; for (const k in tw.to) tw.from[k] = tw.obj[k]; }
      const p = tw.ms ? Math.min(1, (now - tw.t0) / tw.ms) : 1;
      const e = tw.ease(p);
      for (const k in tw.to) tw.obj[k] = tw.from[k] + (tw.to[k] - tw.from[k]) * e;
      if (p >= 1) { done.push(tw.res); return false; }
      return true;
    });
    timers = timers.filter((tm) => (now >= tm.at ? (done.push(tm.res), false) : true));
    done.forEach((r) => r());
    if (onFrame) onFrame(dt, now);
    raf = requestAnimationFrame(frame);
  }
  raf = requestAnimationFrame(frame);
  return clock;
}

/** Keyboard stack: the top handler gets keys first; return true to swallow. */
const keyStack = [];
let keyBound = false;
export function pushKeys(handler) {
  if (!keyBound) {
    keyBound = true;
    window.addEventListener('keydown', (e) => {
      for (let i = keyStack.length - 1; i >= 0; i--) if (keyStack[i](e)) { e.preventDefault(); return; }
    });
  }
  keyStack.push(handler);
  return () => { const i = keyStack.indexOf(handler); if (i >= 0) keyStack.splice(i, 1); };
}

/**
 * A one-line notice at stage coordinates: the app's own word to the player about the game itself (that
 * nothing can be saved here, that a save failed). `ms` > 0 lets it fade out and leave after that long.
 * Returns the element; the caller puts it where it belongs (a scene root, an overlay host, the stage).
 */
export function noticeLine(text, x, y, w, ms = 0) {
  const el = place(h('div', { class: 'ocbg-notice', role: 'status' }, text), x, y, w);
  if (ms > 0) {
    setTimeout(() => { el.classList.add('is-leaving'); }, Math.max(0, ms - 600));
    setTimeout(() => { el.remove(); }, ms);
  }
  return el;
}

/**
 * The element Tab last moved the focus to, or null once the pointer is pressed (a button clicked with the mouse
 * keeps the focus too, but it was not chosen with the keys). Watched from the first confirm card on.
 */
let tabFocus = null;
let lastKeyTab = false;
let focusWatched = false;
function watchTabFocus() {
  if (focusWatched || typeof window === 'undefined') return;
  focusWatched = true;
  window.addEventListener('keydown', (e) => { lastKeyTab = e.key === 'Tab'; }, true);
  document.addEventListener('focusin', (e) => { tabFocus = lastKeyTab ? e.target : null; });
  window.addEventListener('pointerdown', () => { tabFocus = null; lastKeyTab = false; }, true);
}

/**
 * A confirm card (the screens never use window.confirm). Resolves true/false.
 * opts: { title, body, ok = '進む', cancel = 'やめる', danger }
 * Enter confirms and Esc cancels — but a button of the card that Tab moved the focus to takes Enter (and Space)
 * itself: a focused 「やめる」 never confirms (the same rule as the exploration screen's, EXTENSIONS X-8). Only a fresh
 * press answers: a held key repeats, and its repeats must not answer the card (or the next one the screen opens).
 */
export function confirmCard(host, opts) {
  watchTabFocus();
  return new Promise((resolve) => {
    const shade = h('div', { class: 'ocbg-confirm' });
    const cancelBtn = h('button', { class: 'ocbg-btn', type: 'button', onclick: () => done(false) }, opts.cancel || 'やめる');
    const okBtn = h('button', { class: 'ocbg-btn is-primary' + (opts.danger ? ' is-danger' : ''), type: 'button', onclick: () => done(true) }, opts.ok || '進む');
    const card = h('div', { class: 'ocbg-confirm-card ocb-solid' },
      opts.title ? h('h3', null, opts.title) : null,
      h('p', null, opts.body || ''),
      h('div', { class: 'ocbg-confirm-row' }, cancelBtn, okBtn));
    shade.appendChild(card);
    host.appendChild(shade);
    const focused = () => {
      const el = typeof document !== 'undefined' ? document.activeElement : null;
      return el && el === tabFocus && (el === okBtn || el === cancelBtn) ? el : null;
    };
    const pop = pushKeys((e) => {
      if (e.key === 'Escape') { if (!e.repeat) done(false); return true; }
      if (e.key === 'Enter' || e.key === ' ') {
        if (e.repeat || e.ctrlKey || e.metaKey || e.altKey) return true;
        const b = focused();
        if (b) { b.click(); return true; }
        if (e.key === 'Enter') { done(true); return true; }
        return true;
      }
      return false;
    });
    let answered = false;
    function done(v) { if (answered) return; answered = true; pop(); shade.remove(); resolve(v); }
  });
}
