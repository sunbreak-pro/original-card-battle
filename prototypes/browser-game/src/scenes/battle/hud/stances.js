// The stance lists at the two edges of the screen: a port of StanceListView (battle-visual-v1 §4.6 スタンス枠,
// battle_core_v4 §4 v4.4, #455) in the design system's StanceSlot look. Each stance takes one 250×52 frame,
// stacked upward from the slot at y 470 (StanceList), the newest in the slot. The player's frames stand 24 px
// from the left edge in the stance colour; the enemy's 24 px from the right in violet with a phosphor flag. A
// stance held twice shows "×2". A side holding none shows the empty frame (steel dashed, steel flag). A list
// longer than three folds its oldest into a "+n" frame on top. Hovering a stance shows its chip's 説明 (the
// lasting chip the same unit carries: UnitFrame.Statuses), as the design system's StanceTip does.
//
// Two or more enemies (battle-visual-v1 §4.6 敵の枠: 「敵が 2 体以上なら左端に色見本を置きます」; review fix:
// only Frame.Enemy's stances were drawn): the enemy list holds every standing enemy's stances, each frame with
// its own enemy's hatch swatch on the left. Unity draws one enemy, so the order across enemies is the
// scene's: a stance goes to the slot when it is put down (or stacked again, §3.7), and the frames of one
// enemy keep the order its UnitFrame.Stances gives.
import { h } from '../../common/stage.js';
import { UnitSide, ChipKind } from '../../../bridge/depictionScript/DepictionTypes.js';
import { hatchAngle } from '../layout.js';
import { StanceList } from '../../../bridge/depictionScript/StanceList.js';
import { tok, mix, clamp } from './util.js';

const LASTING_PREFIX = '構え・';
/** The 説明 beside a frame: 340 wide (the chips' panel), 12 px off the frame (DS renderStances). */
const TIP_WIDTH = 340;
const TIP_GAP = 12;
/** The 説明's row heads, as the chips' panel (StatusBarView.ShowPanel). scene text */
const EFFECT_HEAD = '効き方';
const DECAY_HEAD = '減り方';
/**
 * DS 試作 (b) スタンス枠が立つときの光: a 4 px ring round the frame a stance was just put in, fading over
 * 300 ms — it shows where the played card went. In the stance colour on the player's side, violet on the enemy's.
 */
const SET_RING = { px: 4, ms: 300 };
/**
 * Review 21 (the frames at the screen's edges were drawn across the figures — a slid arena beside an open drawer, the
 * hound's tail on the last of 8 cells, the hero beside the journal): a list that a standing figure's art would come under
 * moves to its high place, in the top bar beside its side's corner chrome — the player's 12 px right of the corner
 * information, the enemy's 12 px left of the speed switch (or the gear) — one frame there, the newest, with 「+n」 on it
 * for the rest (its hover lists them all). It comes back to y 470 once the figures leave the place. A look call: Unity's
 * StanceListView has one place, and battle-visual-v1 §4.6 none for a slid arena.
 */
const HIGH_GAP = 12;
const SWITCH_FADE_MS = 150;

export function createStanceLists(env, tip, place) {
  const { OCB, layer, L } = env;
  const host = h('div', { class: 'ocbg-battle-stances' });
  layer.panels.appendChild(host);
  const sides = {
    [UnitSide.Player]: { el: h('div'), key: null, frames: [], high: false, rows: [], swatch: null, merged: null },
    [UnitSide.Enemy]: { el: h('div'), key: null, frames: [], high: false, rows: [], swatch: null, merged: null },
  };
  /** `place.highLeft(side)`: the high place's left edge (the hud measures the corner and the speed switch). */
  const highTop = () => (place && place.highTop ? place.highTop() : 20);
  const highLeft = (side) => (place && place.highLeft ? place.highLeft(side) : StanceList.LeftOf(side));
  /** Where frame i of `side`'s list stands now: { left, top }. */
  function frameAt(side, i) {
    return sides[side].high ? { left: highLeft(side), top: highTop() } : { left: StanceList.LeftOf(side), top: StanceList.TopOf(i) };
  }
  host.append(sides[UnitSide.Player].el, sides[UnitSide.Enemy].el);

  function chipFor(unit, name) {
    return (unit.Statuses || []).find((c) => c.Kind === ChipKind.Lasting && (c.Label === LASTING_PREFIX + name || c.Label === name)) || null;
  }

  function showTip(side, i, unit, row, folded, pin) {
    if (!tip) return;
    const foe = side === UnitSide.Enemy;
    let node;
    if (folded) {
      node = h('div', { class: 'ocb-tip ocbg-battle-chiptip' },
        folded.map((r) => {
          const c = chipFor(r.unitFrame || unit, r.Name);
          return h('p', null, r.Name + (StanceList.CountText(r.Count) ? ' ' + StanceList.CountText(r.Count) : '') + (c && c.Effect ? '：' + c.Effect : ''));
        }));
    } else {
      // Review 16: an enemy's stance has no lasting chip in UnitFrame.Statuses (the writer writes the player's only), so
      // its hover showed nothing; the panel then says what the frame has — the name and ×n (StanceList.CountText).
      const c = chipFor(unit, row.Name);
      const count = StanceList.CountText(row.Count);
      const title = c && c.Title ? c.Title : row.Name + (count ? ' ' + count : '');
      node = h('div', { class: 'ocb-tip ocbg-battle-chiptip' },
        h('div', { class: 'ocb-tip-head' }, OCB.icon('flag', 20, foe ? tok('phosphor') : tok('hp-text')), h('span', null, title), c && c.Group ? h('small', null, c.Group) : null),
        c && c.Effect ? h('p', null, h('span', null, EFFECT_HEAD), c.Effect) : null,
        c && c.Decay ? h('p', null, h('span', null, DECAY_HEAD), c.Decay) : null);
    }
    node.style.width = TIP_WIDTH + 'px';
    const at = frameAt(side, i);
    const high = sides[side].high;
    const putTip = (n) => {
      // Beside the frame (the player's on its right, the enemy's on its left); under it in the high place.
      if (high) {
        n.style.left = clamp(at.left + StanceList.Width / 2 - TIP_WIDTH / 2, 16, L.W - 16 - TIP_WIDTH) + 'px';
        n.style.top = at.top + StanceList.RowHeight + TIP_GAP + 'px';
        return;
      }
      n.style.left = (foe ? at.left - TIP_GAP - TIP_WIDTH : at.left + StanceList.Width + TIP_GAP) + 'px';
      n.style.top = clamp(at.top - 10, 16, L.H - 16 - n.offsetHeight) + 'px';
    };
    // §3.13 (review 21): a right click pins it (the same detail) until a click, Esc or another right click.
    if (pin) tip.pin(node, putTip, sides[side]); else tip.show(node, putTip, sides[side]);
  }

  /** The ring round the newest frame (slot 0) as a stance is put down. */
  function ringNewest(side) {
    const el = sides[side].el.firstChild;
    if (!el) return;
    const color = side === UnitSide.Player ? 'hp-text' : 'violet';
    env.clock.run(SET_RING.ms, 'linear', (t) => {
      el.style.boxShadow = t < 1 ? '0 0 0 ' + SET_RING.px + 'px ' + mix(color, Math.round(90 * (1 - t))) : '';
    });
  }

  /**
   * The enemy list's rows (several enemies): every standing enemy's stances, oldest first, each with its
   * owner's UnitFrame and swatch angle. A stance new to the list, or stacked again, takes a newer stamp
   * (it goes to the slot); the rest keep theirs.
   */
  const stamps = new Map();
  let stampNow = 0;
  function mergedRows(frame, first) {
    const list = frame.Enemies && frame.Enemies.length ? frame.Enemies : frame.Enemy ? [frame.Enemy] : [];
    const standing = list.filter((u) => u && !u.Down);
    const from = standing.length ? standing : list; // all fallen (a won battle): the list fades as it stood
    const rows = [];
    const seen = new Set();
    from.forEach((u, k) => {
      const unit = u.Unit >= 0 ? u.Unit : k;
      const angle = frame.SwatchesShown ? hatchAngle(u.Hatch) : null;
      (u.Stances || []).forEach((r) => {
        const id = unit + ':' + r.Name;
        seen.add(id);
        const was = stamps.get(id);
        if (!was || (r.Count | 0) > was.count) stamps.set(id, { stamp: first && !was ? 0 : ++stampNow, count: r.Count | 0 });
        else was.count = r.Count | 0;
        rows.push({ Name: r.Name, Count: r.Count, unitFrame: u, angle, stamp: stamps.get(id).stamp, order: rows.length });
      });
    });
    for (const id of [...stamps.keys()]) if (!seen.has(id)) stamps.delete(id);
    return rows.sort((a, b) => a.stamp - b.stamp || a.order - b.order);
  }

  function bindSide(side, unit, swatchAngle, first, merged) {
    const s = sides[side];
    // rows: { Name, Count, [unitFrame], [angle] } oldest first; `merged` carries the enemies' own.
    const rows = merged || (unit && unit.Stances ? unit.Stances : []);
    const angleOf = (r) => (r && r.angle !== undefined ? r.angle : swatchAngle);
    const key = rows.map((r) => r.Name + '×' + r.Count + '#' + angleOf(r)).join('|') + '@' + swatchAngle + (s.high ? '@high' + highLeft(side) : '');
    s.unit = unit;
    s.args = [unit, swatchAngle, merged];
    s.rowsCount = rows.length;
    if (key === s.key) return;
    // A stance was put down (the list holds more than it did, or a newer stamp leads it): the newest frame lights once.
    const held = rows.reduce((n, r) => n + (r.Count | 0), 0);
    const who = merged ? 'merged' : unit ? unit.Unit : null;
    const newest = merged && rows.length ? rows[rows.length - 1].stamp : null;
    const grew = !first && s.held != null && s.who === who && (held > s.held || (newest != null && newest > (s.newest || 0)));
    s.held = held;
    s.who = who;
    s.newest = newest;
    s.key = key;
    if (tip) tip.hide(s);
    s.el.innerHTML = '';
    const foe = side === UnitSide.Enemy;
    // The high place (review 21) holds one frame: the newest, carrying 「+n」 for the others.
    const frames = s.high ? 1 : StanceList.Frames(rows.length);
    const folded = s.high ? 0 : StanceList.Folded(rows.length);
    for (let i = 0; i < frames; i++) {
      let d;
      let row = null;
      let rest = null;
      if (rows.length === 0) d = { empty: true, foe };
      else if (folded > 0 && i === frames - 1) {
        d = { more: folded, foe };
        rest = rows.slice(0, folded).reverse();
      } else {
        row = rows[rows.length - 1 - i];
        d = { name: row.Name, count: row.Count, foe };
      }
      const angle = row ? angleOf(row) : swatchAngle;
      if (foe && angle != null && !d.empty) d.swatchAngle = angle;
      const el = OCB.StanceSlot(d);
      el.classList.add('ocbg-battle-stance');
      el.style.position = 'absolute';
      const at = frameAt(side, i);
      el.style.left = at.left + 'px';
      el.style.top = at.top + 'px';
      if (s.high && rows.length > 1) {
        // The others, folded on the newest's frame; its hover lists every one (oldest first) after the newest's own panel.
        el.appendChild(h('b', { class: 'ocbg-battle-stance-more ocb-mono' }, '+' + (rows.length - 1)));
        rest = rows.slice().reverse();
      }
      if (!d.empty) {
        el.classList.add('ocbg-battle-hit');
        el.addEventListener('mouseenter', () => { el.classList.add('is-hover'); showTip(side, i, (row && row.unitFrame) || s.unit, row, rest); });
        el.addEventListener('mouseleave', () => { el.classList.remove('is-hover'); if (tip) tip.hide(s); });
        el.addEventListener('contextmenu', (e) => { e.preventDefault(); e.ocbgPinned = true; showTip(side, i, (row && row.unitFrame) || s.unit, row, rest, true); });
      }
      s.el.appendChild(el);
    }
    if (grew) ringNewest(side);
  }

  /** The list's frames as they would stand at rest (y 470 up), { x, y, w, h } — what the figures are tested against. */
  function restBox(side) {
    const s = sides[side];
    const n = Math.max(1, StanceList.Frames(s.rowsCount || 0));
    const top = StanceList.TopOf(n - 1);
    return { x: StanceList.LeftOf(side), y: top, w: StanceList.Width, h: StanceList.SlotTop + StanceList.RowHeight - top };
  }
  /** Moves `side`'s list to its high place or back (a short fade in at the new place, on the scene clock). */
  function setHigh(side, high) {
    const s = sides[side];
    high = !!high;
    if (high === s.high) return;
    s.high = high;
    if (tip) tip.hide(s);
    s.key = null;
    if (s.args) bindSide(side, s.args[0], s.args[1], true, s.args[2]);
    // On real time, as the drawers' slide that moves the figures (the journal holds the scene clock still).
    s.el.style.opacity = '0';
    const mine = (s.fadeToken = (s.fadeToken || 0) + 1);
    const t0 = env.real.now;
    const step = () => {
      if (mine !== s.fadeToken) return;
      const t = Math.min(1, (env.real.now - t0) / SWITCH_FADE_MS);
      s.el.style.opacity = t >= 1 ? '' : (1 - (1 - t) * (1 - t)).toFixed(3);
      if (t < 1) env.real.wait(16).then(step);
    };
    step();
  }

  return {
    /** StanceListView.Bind: both sides' lists of the frame; a source that keeps none hides them. */
    bind(frame, first) {
      const shown = !!(frame && frame.StanceListsShown);
      host.style.display = shown ? '' : 'none';
      if (!shown) { if (tip) { tip.hide(sides[UnitSide.Player]); tip.hide(sides[UnitSide.Enemy]); } return; }
      bindSide(UnitSide.Player, frame.Player, null, first);
      const swatch = frame.SwatchesShown && frame.Enemy ? hatchAngle(frame.Enemy.Hatch) : null;
      const several = frame.Enemies && frame.Enemies.length > 1;
      bindSide(UnitSide.Enemy, frame.Enemy, swatch, first, several ? mergedRows(frame, first) : null);
    },
    /** `side`'s list on the stage, { x, y, w, h } over every frame it shows (the empty one too); null when hidden. */
    boxOf(side) {
      const s = sides[side];
      if (!s || host.style.display === 'none') return null;
      if (s.high) return { x: highLeft(side), y: highTop(), w: StanceList.Width, h: StanceList.RowHeight };
      const n = Math.max(1, s.el.children.length);
      const top = StanceList.TopOf(n - 1);
      return { x: StanceList.LeftOf(side), y: top, w: StanceList.Width, h: StanceList.SlotTop + StanceList.RowHeight - top };
    },
    /** The frame a stance just put down goes to (§5.11 順 1): the slot at y 470, or the high place's one frame. */
    slotOf(side) {
      const s = sides[side];
      if (!s || host.style.display === 'none') return null;
      const at = frameAt(side, 0);
      return { x: at.left, y: at.top, w: StanceList.Width, h: StanceList.RowHeight };
    },
    /**
     * Review 21: `arts` = the standing figures' art boxes ({ x0, x1, y0, y1 }, stage px). A list any of them comes under (4 px
     * kept) goes to its high place; it comes back when none does.
     */
    keepClear(arts) {
      if (host.style.display === 'none') return;
      for (const side of [UnitSide.Player, UnitSide.Enemy]) {
        const b = restBox(side);
        const under = (arts || []).some((a) => a.x0 < b.x + b.w + 4 && a.x1 > b.x - 4 && a.y0 < b.y + b.h + 4 && a.y1 > b.y - 4);
        setHigh(side, under);
      }
    },
    isHigh(side) { return !!(sides[side] && sides[side].high); },
    /** The element of `side`'s list. */
    elOf(side) {
      const s = sides[side];
      return s ? s.el : null;
    },
    destroy() { host.remove(); },
  };
}
