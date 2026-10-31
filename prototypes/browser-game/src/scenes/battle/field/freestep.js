// 俊敏's choice on the floor (battle_ui_ux_v2 §3.8 / §5.1 順 7 / §5.7 順 10; scene addition with the source's JS
// extension CanTakeFreeStep / TakeFreeStep — Unity offers no free step). While the choice waits, an arrow stands
// over the cell before and the cell behind the player — only where the source says the way is open — and a
// 留まる mark on the player's own cell. A click on a mark chooses it; ← / → pick back / forward and Enter chooses
// (the focus is the amber edge, as every focus in the battle). The marks only show what they are handed: which ways
// are open is the source's, and the step itself plays as the source's event (the walk on the floor).
//
// Review 19 (§1.5: a resting screen's fixed labels are at most 10 characters, each at most 2 — 「← 後ろへ」「→ 前へ」
// 「留まる」 beside ターン終了 made 13; battle-visual-v1 §11 lists 俊敏の矢印 and 留まる as icons): the marks are icons
// only — 付録 A's 前へ / 後ろへ in amber (the spec's own 俊敏 arrows are #92's) and, for 留まる, a stand-in line icon of a
// figure on its own cell (#92 draws the real one) — and the mark's word shows as a tip only while it is hovered or
// focused (a tip is no resting label).
//
// Where they stand (scene choice): the arrows over their cells, their bottom 8 px over the floor's top, so the
// gap number on the cells between the player and the enemy stays readable under them; the 留まる mark on the
// player's own tile, which the gap number never covers — a lower plate (28 tall) on the tile's lower part, its top
// 2 px under the feet (review fix: a taller plate's top edge cut across the hero's feet and foot light; over the
// floor it would cover the legs, under the tile the hero's number panel).
import { h } from '../../common/stage.js';

/** FloorView: the floor's top (y 560; its tiles are 40 tall, to y 600). */
const TOP = 560;
/** The marks: 40 tall; an arrow 64 wide, 留まる 64; the arrows stand this far over the floor. */
const MARK_H = 40;
const ARROW_W = 64;
const STAY_W = 64;
/** 留まる: 28 tall, from 2 px under the feet (FEET_Y 570) to the tile's bottom (600). */
const STAY_H = 28;
const STAY_UNDER_FEET = 2;
const FEET_Y = 570;
const OVER_FLOOR = 8;
const ICON = 30;
const STAY_ICON = 24;
/** The word's tip: 8 px over the mark. */
const TIP_GAP = 8;
/** §5.1 順 7: the marks come up over 150 ms (out); they go in 120. */
const SHOW_MS = 150;
const HIDE_MS = 120;
const RISE = 8;

/** scene text (battle_ui_ux_v2 §3.8): the marks' words, on their tips; 前へ / 後ろへ are the 付録 A icons' own names. */
const WORDS = { [-1]: '後ろへ', 0: '留まる', 1: '前へ' };
const ICONS = { [-1]: 'back', 1: 'forward' };
/**
 * 留まる's stand-in icon (battle-visual-v1 §11: 「演出の印 6 … 俊敏の矢印・留まる」 are to be drawn, #92): 付録 A's line style
 * (24×24, 2.2 px, round ends) — a figure standing on its own cell, the cell's outline under its feet.
 */
const STAY_PATH = 'M12 3.5a2.6 2.6 0 110 5.2a2.6 2.6 0 010-5.2zM12 8.7v6.6M8.6 11.6l3.4-1.6 3.4 1.6M9.4 19.6l2.6-4.3 2.6 4.3M3.5 21h17';
function stayIcon(size) {
  const NS = 'http://www.w3.org/2000/svg';
  const s = document.createElementNS(NS, 'svg');
  s.setAttribute('width', String(size));
  s.setAttribute('height', String(size));
  s.setAttribute('viewBox', '0 0 24 24');
  s.setAttribute('fill', 'none');
  s.setAttribute('stroke', 'var(--amber)');
  s.setAttribute('stroke-width', '2.2');
  s.setAttribute('stroke-linecap', 'round');
  s.setAttribute('stroke-linejoin', 'round');
  s.setAttribute('class', 'ocb-icon');
  const p = document.createElementNS(NS, 'path');
  p.setAttribute('d', STAY_PATH);
  s.appendChild(p);
  return s;
}

/**
 * @param env  the scene env (geom.cellBox follows the arena's slide)
 * @param o    { onPick(dir), onFocus(dir) } — dir −1 back, 0 stay, +1 forward
 */
export function createFreeStep(env, o = {}) {
  const { OCB, layer, clock } = env;
  const root = h('div', { class: 'ocbg-battle-freestep' });
  root.style.display = 'none';
  layer.marks.appendChild(root);

  let marks = [];      // { dir, el, cell, w }
  let shown = false;
  let focus = null;
  let alpha = 0;
  let rise = RISE;
  let token = 0;

  function build(playerCell, ways) {
    root.innerHTML = '';
    marks = [];
    hoverDir = null;
    for (const dir of [-1, 0, 1]) {
      if (dir !== 0 && !ways[dir]) continue;
      const stay = dir === 0;
      const el = h('div', { class: 'ocbg-battle-fs-mark ocbg-battle-hit' + (stay ? ' is-stay' : ''), 'data-dir': String(dir), role: 'button', 'aria-label': WORDS[dir] },
        stay ? stayIcon(STAY_ICON) : OCB.icon(ICONS[dir], ICON, 'var(--amber)'));
      el.addEventListener('pointerenter', () => { hoverDir = dir; drawTip(); if (shown && o.onFocus) o.onFocus(dir); });
      el.addEventListener('pointerleave', () => { if (hoverDir === dir) { hoverDir = null; drawTip(); } });
      el.addEventListener('click', (e) => { e.stopPropagation(); if (shown && o.onPick) o.onPick(dir); });
      root.appendChild(el);
      marks.push({ dir, el, cell: playerCell + dir, w: stay ? STAY_W : ARROW_W });
    }
    drawFocus();
  }

  function drawFocus() {
    marks.forEach((m) => m.el.classList.toggle('is-focus', m.dir === focus));
    drawTip();
  }
  /** The word of the mark under the pointer, else of the focused one, on a tip over it. */
  let hoverDir = null;
  const tip = h('div', { class: 'ocbg-battle-linetip ocbg-battle-fs-tip' }, h('span'));
  tip.style.display = 'none';
  function drawTip() {
    const dir = hoverDir != null ? hoverDir : focus;
    const m = dir != null && shown ? marks.find((x) => x.dir === dir) : null;
    if (!m || m.el.style.display === 'none') { tip.style.display = 'none'; return; }
    tip.firstChild.textContent = WORDS[dir];
    tip.style.display = '';
    const left = parseFloat(m.el.style.left) || 0;
    const top = parseFloat(m.el.style.top) || 0;
    tip.style.left = (left + m.w / 2).toFixed(1) + 'px';
    tip.style.top = (top - TIP_GAP).toFixed(1) + 'px';
  }

  function place() {
    if (!shown && alpha <= 0) return;
    root.style.opacity = alpha >= 1 ? '' : alpha.toFixed(3);
    for (const m of marks) {
      const b = env.geom.cellBox ? env.geom.cellBox(m.cell) : null;
      if (!b) { m.el.style.display = 'none'; continue; }
      m.el.style.display = '';
      const stay = m.dir === 0;
      const top = stay ? FEET_Y + STAY_UNDER_FEET : TOP - OVER_FLOOR - MARK_H + rise;
      m.el.style.left = (b.x + b.w / 2 - m.w / 2).toFixed(1) + 'px';
      m.el.style.top = top.toFixed(1) + 'px';
      m.el.style.width = m.w + 'px';
      m.el.style.height = (stay ? STAY_H : MARK_H) + 'px';
    }
    if (!tip.parentNode) root.appendChild(tip);
    drawTip();
  }
  const offFrame = clock.onFrame(place);

  return {
    get shown() { return shown; },
    get focus() { return focus; },
    /** The marks for a player on `playerCell`; `ways` = { [-1]: bool, [1]: bool } (the source's CanTakeFreeStep(dir)). */
    show(playerCell, ways) {
      const mine = ++token;
      shown = true;
      focus = null;
      build(playerCell, ways || {});
      root.style.display = '';
      alpha = 0;
      rise = RISE;
      place();
      return clock.run(SHOW_MS, 'out', (t) => {
        if (mine !== token) return;
        alpha = t;
        rise = RISE * (1 - t);
        place();
      });
    },
    /** The focus (the amber edge): −1, 0, +1, or null for none. A way that is not shown takes no focus. */
    setFocus(dir) {
      focus = dir != null && marks.some((m) => m.dir === dir) ? dir : null;
      drawFocus();
      return focus;
    },
    /** Which marks stand: [−1?, 0, +1?]. */
    dirs() { return marks.map((m) => m.dir); },
    /**
     * The x span the marks take while the choice is up, from `cellBox(cell)` (the floor's own box, before the arena's
     * slide): { left, right }, or null when none stands. The drawer's slide brings them out with the hero (field.js).
     */
    span(cellBox) {
      if (!shown || !marks.length) return null;
      let left = Infinity;
      let right = -Infinity;
      for (const m of marks) {
        const b = cellBox(m.cell);
        if (!b) continue;
        left = Math.min(left, b.x + b.w / 2 - m.w / 2);
        right = Math.max(right, b.x + b.w / 2 + m.w / 2);
      }
      return left < right ? { left, right } : null;
    },
    /** The marks go (120 ms, or at once with ms 0). */
    hide(ms = HIDE_MS) {
      if (!shown && alpha <= 0) return Promise.resolve();
      const mine = ++token;
      shown = false;
      const from = alpha;
      const done = () => { if (mine !== token) return; alpha = 0; root.style.display = 'none'; root.innerHTML = ''; marks = []; focus = null; };
      if (!(ms > 0)) { done(); return Promise.resolve(); }
      return clock.run(ms, 'in', (t) => {
        if (mine !== token) return;
        alpha = from * (1 - t);
        place();
      }).then(done);
    },
    /** Every mark's box as it stands (stage px, the arena's slide included): the card detail keeps off them. */
    boxes() {
      if (!shown) return [];
      return marks.filter((m) => m.el.style.display !== 'none').map((m) => {
        const left = parseFloat(m.el.style.left);
        const top = parseFloat(m.el.style.top);
        return { x: left, y: top, w: m.w, h: m.dir === 0 ? STAY_H : MARK_H, dir: m.dir };
      }).filter((b) => Number.isFinite(b.x) && Number.isFinite(b.y));
    },
    /** The middle of mark `dir` in stage px (tests and the playtest click it), or null. */
    markCentre(dir) {
      const m = marks.find((x) => x.dir === dir);
      if (!m) return null;
      const left = parseFloat(m.el.style.left);
      const top = parseFloat(m.el.style.top);
      return Number.isFinite(left) && Number.isFinite(top) ? { x: left + m.w / 2, y: top + (m.dir === 0 ? STAY_H : MARK_H) / 2 } : null;
    },
    destroy() {
      token++;
      offFrame();
      root.remove();
    },
  };
}
