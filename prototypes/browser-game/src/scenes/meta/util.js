// Small presentation helpers shared by the meta screens: the scene root, a style proxy the frame
// clock can tween, sound calls that never throw, number roll-ups and token reads for the canvas.
import { h, createClock, EASE } from '../common/stage.js';

/** The design system bundle (window.OCB), read when a screen mounts, never at module load. */
export function ocb() {
  const o = globalThis.OCB;
  if (!o) throw new Error('meta scenes need window.OCB (ds/project/components/bundle.js)');
  return o;
}

/** ctx.audio (the game's createAudio) or ctx.sound; every call is guarded so a silent page still runs. */
export function soundOf(ctx) {
  const a = (ctx && (ctx.audio || ctx.sound)) || null;
  return {
    audio: a,
    play(name, delayMs, mult) {
      try { if (a && typeof a.play === 'function') a.play(name, delayMs || 0, mult); } catch (e) { /* silent */ }
    },
  };
}

/** The scene's root element (class prefix ocbg-meta-), filling the 1920×1080 host. */
export function sceneRoot(host, name) {
  const el = h('div', { class: `ocbg-meta ocbg-meta-${name}` });
  host.appendChild(el);
  return el;
}

/**
 * A tweenable view of an element's opacity and transform: clock.tween(p, { o: 1, y: 0 }, 300, 'out').
 * Keys: o (opacity), x, y (px), s (scale), r (deg).
 */
export function styler(el, init) {
  const st = { o: 1, x: 0, y: 0, s: 1, r: 0, ...(init || {}) };
  const apply = () => {
    el.style.opacity = String(st.o);
    el.style.transform = `translate(${st.x}px, ${st.y}px) scale(${st.s}) rotate(${st.r}deg)`;
  };
  const p = {};
  for (const k of Object.keys(st)) {
    Object.defineProperty(p, k, { get: () => st[k], set: (v) => { st[k] = v; apply(); }, enumerable: true });
  }
  p.set = (vals) => { Object.assign(st, vals); apply(); return p; };
  p.el = el;
  apply();
  return p;
}

/** A number that rolls to its new value (mono digits keep their width). */
export function rollNumber(clock, el, initial) {
  const n = { v: initial || 0 };
  const show = () => { el.textContent = String(Math.round(n.v)); };
  const proxy = {};
  Object.defineProperty(proxy, 'v', { get: () => n.v, set: (v) => { n.v = v; show(); } });
  show();
  return {
    to(v, ms = 260) {
      if (Math.round(n.v) === v) { n.v = v; show(); return Promise.resolve(); }
      return clock.tween(proxy, { v }, ms, 'out');
    },
    set(v) { n.v = v; show(); },
  };
}

/** A token's computed value (for the canvas, which cannot read var()). */
export function token(el, name, fallback) {
  try {
    const v = getComputedStyle(el).getPropertyValue(name).trim();
    return v || fallback;
  } catch (e) { return fallback; }
}

/** '#rrggbb' → [r, g, b] (0〜255). */
export function hexRgb(hex) {
  const s = String(hex || '').replace('#', '');
  if (s.length !== 6) return [255, 255, 255];
  return [parseInt(s.slice(0, 2), 16), parseInt(s.slice(2, 4), 16), parseInt(s.slice(4, 6), 16)];
}

/** A bridge Rgba (0〜1 channels) as a CSS colour, its alpha multiplied by `mul`. */
export function rgbaCss(c, mul = 1) {
  const r = Math.round(c.R * 255), g = Math.round(c.G * 255), b = Math.round(c.B * 255);
  return `rgba(${r},${g},${b},${Math.max(0, Math.min(1, c.A * mul)).toFixed(3)})`;
}

/** Reduced motion asked for by the system. */
export function reducedMotion() {
  try { return !!(globalThis.matchMedia && globalThis.matchMedia('(prefers-reduced-motion: reduce)').matches); } catch (e) { return false; }
}

/** A clock whose tweens finish at once when motion is reduced. */
export function sceneClock(onFrame) {
  const clock = createClock(onFrame);
  if (!reducedMotion()) return clock;
  const tween = clock.tween.bind(clock);
  clock.tween = (obj, to, ms, ease, delay) => tween(obj, to, Math.min(ms, 1), ease, 0);
  return clock;
}

export { EASE };

/** Enter / leave helpers: a staggered rise-in for a list of elements. */
export function riseIn(clock, els, { delay = 0, step = 70, dy = 18, ms = 420 } = {}) {
  return Promise.all(els.map((el, i) => {
    const p = styler(el, { o: 0, y: dy });
    return clock.tween(p, { o: 1, y: 0 }, ms, 'out', delay + i * step).then(() => { el.style.transform = ''; });
  }));
}

/** An icon from the design system, sized and coloured by role. */
export function icon(name, size, color, opts) {
  return ocb().icon(name, size, color, opts);
}

/** What a modal overlay keeps from the screen under it: characters, arrows, Enter and Tab (not F5 / F12). */
export function modalSwallow(e) {
  return e.key.length === 1 || e.key.startsWith('Arrow') || e.key === 'Enter' || e.key === 'Tab' || e.key === 'PageUp' || e.key === 'PageDown';
}

/**
 * The fibres of washi as a tile drawn once with --washi-ink at a few percent (no image files, no
 * colour of its own). Deterministic: a Park-Miller sequence places every fibre. Set on an element as
 * --ocbg-washi-tex; meta.css lays it over the --washi fill.
 */
let washiUrl = null;
export function applyWashi(el) {
  try {
    if (!washiUrl) {
      const [r, g, b] = hexRgb(token(el, '--washi-ink', '#1d1a16'));
      const size = 360;
      const c = document.createElement('canvas');
      c.width = size; c.height = size;
      const x = c.getContext('2d');
      let seed = 48271;
      const rnd = () => { seed = (seed * 48271) % 2147483647; return seed / 2147483647; };
      const wrapped = (draw) => { for (const dx of [-size, 0, size]) for (const dy of [-size, 0, size]) { x.save(); x.translate(dx, dy); draw(); x.restore(); } };
      for (let i = 0; i < 26; i++) {
        const cx = rnd() * size, cy = rnd() * size, rad = 30 + rnd() * 70;
        const a = (0.012 + rnd() * 0.02).toFixed(3);
        wrapped(() => {
          const grad = x.createRadialGradient(cx, cy, 0, cx, cy, rad);
          grad.addColorStop(0, `rgba(${r},${g},${b},${a})`);
          grad.addColorStop(1, `rgba(${r},${g},${b},0)`);
          x.fillStyle = grad;
          x.fillRect(cx - rad, cy - rad, rad * 2, rad * 2);
        });
      }
      x.lineCap = 'round';
      for (let i = 0; i < 260; i++) {
        const x0 = rnd() * size, y0 = rnd() * size;
        const len = 6 + rnd() * rnd() * 46;
        const ang = (rnd() - 0.5) * Math.PI * 0.9;
        const bend = (rnd() - 0.5) * len * 0.6;
        const x1 = x0 + Math.cos(ang) * len, y1 = y0 + Math.sin(ang) * len;
        const a = (0.04 + rnd() * 0.07).toFixed(3);
        const w = 0.5 + rnd() * 0.9;
        wrapped(() => {
          x.strokeStyle = `rgba(${r},${g},${b},${a})`;
          x.lineWidth = w;
          x.beginPath();
          x.moveTo(x0, y0);
          x.quadraticCurveTo((x0 + x1) / 2 + bend, (y0 + y1) / 2 - bend, x1, y1);
          x.stroke();
        });
      }
      washiUrl = c.toDataURL('image/png');
    }
    el.style.setProperty('--ocbg-washi-tex', `url(${washiUrl})`);
  } catch (e) { /* plain washi */ }
}
