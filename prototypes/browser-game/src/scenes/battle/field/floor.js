// The floor under the figures: a port of unity-project-kit/Assets/View/Depiction/FloorView.cs (battle-visual-v1
// §4.2). The line of cells; the cells the omen aims at in red stripes (6 px apart on those that meet the
// boundary its action reads) with a red frame and an 8 px light; a violet dashed frame on the cell a moving
// omen takes the enemy to; the cells a hovered or held card reaches in amber; and the one gap number
// between the player and the enemy. Every cell comes from the script (DepictionFrame.Floor,
// CardFace.ReachCells); FloorSpec holds the arithmetic. Looks: OCB.FloorTile and the DistanceChip's parts.
import { h } from '../../common/stage.js';
import { FloorSpec } from '../../../bridge/depictionScript/FloorSpec.js';
import { rgba } from './art.js';
import { hatchAngle } from '../layout.js';

/** FloorView: the floor's top (y 560: the feet at 570 less FeetBelowTop). */
const TOP = 560;
/** §4.2 間合いの数字: the tag 3 px below the floor's top, 34 px tall. */
const TAG_TOP = 3;
/** battle_ui_ux_v2 §5.7 順 2: how far the old number rises out and the new one rises in from. */
const RECOUNT_TRAVEL = 14;
/** §4.2: the stripes' direction for the enemy the frame draws (the first enemy: 45°). */
const AIM_ANGLE = 45;
/** battle_ui_ux_v2 §2.2.2: while one enemy's omen badge is hovered, the other enemies' stripes stand at this. */
const UNFOCUSED_ALPHA = 0.2;
/** battle_ui_ux_v2 §5.2: the reach light comes up over 150 ms (out) and goes over 120 ms (linear). */
const REACH_IN_MS = 150;
const REACH_OUT_MS = 120;
/**
 * battle_ui_ux_v2 §5.8 (review 16; Unity redraws the stripes with each frame): as an omen is shown, its aimed cells'
 * stripes are drawn left to right, AIM_DRAW_MS a cell; after the action has run, they fade over AIM_FADE_MS.
 */
const AIM_DRAW_MS = 30;
const AIM_FADE_MS = 120;
/** §5.7 順 7 壁: the lit end of the floor — 10 px wide, from 80 px over the floor to its bottom; it fades over `fade` after its 80 ms. */
const WALL_EDGE = { w: 10, up: 80, fade: 160 };

/**
 * @param env   the scene env
 * @param tile  the layer's floor tile fill (LayerBackdrop.Tile), or null for the DS default
 */
export function createFloor(env, tile) {
  const { OCB, clock, layer } = env;
  const root = h('div', { class: 'ocbg-battle-floor' });
  layer.field.appendChild(root);
  const tileFill = tile ? rgba(tile) : null;

  let cells = 0;
  let tiles = [];
  let reach = [];
  let gapState = null;
  let recount = 0;
  let lastFloor = null;
  /** The enemy whose aimed cells alone stay lit (its omen badge is hovered), or null. */
  let focusUnit = null;
  /** The battle is over: the gap number is put away (review fix: it measured to a fallen enemy's empty cell). */
  let gapHidden = false;
  /**
   * The battle is over (review fix: on a loss with enemies standing, their aimed cells stayed red under the result
   * card while their omen badges faded): the omens' floor half — the aimed, dense and move cells — retires with the
   * badges. `aimAlpha` runs 1 → 0, then every cell is cleared and stays clear (`aimRetired`).
   */
  let aimAlpha = 1;
  let aimRetired = false;
  let aimToken = 0;
  /**
   * battle_ui_ux_v2 §5.15 順 5 (review fix: a fallen enemy's aimed cells stayed striped through its whole fall, a second
   * after its badge had gone): the enemies whose omen went with their fall (retireUnit). Their aimed, dense and move
   * cells leave the floor as the fall starts (the 120 ms fade of §5.8), and stay off whatever a later frame says.
   * `soloRetired`: the same for the one enemy of a frame that carries no FloorFrame.Enemies.
   */
  const retiredUnits = new Set();
  let soloRetired = false;
  /**
   * battle_ui_ux_v2 §7 / §5.8 提示 「斜線…（開示度 1 以上）」 (review 21): which enemies are read to 0 — their omen shows the kind
   * alone, so their aimed, dense and move cells are not drawn. A function (unit → bool; the one enemy of a frame with no
   * FloorFrame.Enemies asks with undefined), kept from the last bind that gave one.
   */
  let unreadOf = () => false;

  // ---- the gap tag: DS DistanceChip's parts, so it can be counted again in place ----
  const gapEl = h('div', { class: 'ocb-dist ocbg-battle-gap' });
  const armL = h('i', { class: 'ocbg-battle-gap-arm is-left' });
  const armR = h('i', { class: 'ocbg-battle-gap-arm is-right' });
  const num = h('span', { class: 'ocbg-battle-gap-num' }, '0');
  const chip = h('div', { class: 'ocb-dist-chip' }, OCB.icon('range', 22, 'var(--ink)'), num);
  gapEl.appendChild(armL);
  gapEl.appendChild(armR);
  gapEl.appendChild(chip);
  gapEl.style.top = TOP + TAG_TOP + 'px';
  root.appendChild(gapEl);

  function build(n) {
    tiles.forEach((t) => t.el.remove());
    tiles = [];
    cells = n;
    const w = n > 0 ? FloorSpec.TileWidth(n) : 0;
    for (let c = 1; c <= n; c++) {
      const el = OCB.FloorTile(w);
      el.classList.add('ocbg-battle-tile');
      el.style.left = FloorSpec.TileLeft(n, c) + 'px';
      el.style.top = TOP + 'px';
      if (tileFill) el.style.background = tileFill;
      root.insertBefore(el, gapEl);
      const t = { el, key: '', lit: false, reachA: 0, reachToken: 0, threat: false, fade: 0 };
      drawReach(t);
      tiles.push(t);
    }
  }

  function drawReach(t) {
    const a = t.reachA > 0.001 ? t.reachA.toFixed(3) : '0';
    t.el.reach.style.opacity = a;
    t.el.reachLine.style.opacity = a;
  }
  /**
   * §4.2 届くマスの光, battle_ui_ux_v2 §5.2 (review 16; Unity switches it at once): a cell's light comes up over
   * REACH_IN_MS (out) with the target frame and goes over REACH_OUT_MS (linear) when the card is let go or put back.
   */
  function applyReach() {
    tiles.forEach((t, i) => {
      const lit = reach.indexOf(i + 1) >= 0;
      if (lit === t.lit) return;
      t.lit = lit;
      const mine = ++t.reachToken;
      const from = t.reachA;
      const to = lit ? 1 : 0;
      clock.run(lit ? REACH_IN_MS : REACH_OUT_MS, lit ? 'out' : 'linear', (k) => {
        if (mine !== t.reachToken) return;
        t.reachA = from + (to - from) * k;
        drawReach(t);
      });
    });
  }

  /**
   * The tag's place for a gap: { left, width, spanW, broken } (FloorView.SetGap with the DS chip's own width).
   * Several enemies (review fix, battle_ui_ux_v2 §2.2 「あいだにある空きマスの上」): when another standing enemy stands
   * between the player and the enemy the number is read to (a far one chosen with Tab or framed by a held card),
   * the cells between are not all empty, and the middle of the whole span fell under that enemy's feet, reading as
   * its own N. The tag then stands over the run of empty cells in the span nearest the chosen enemy (with no bracket:
   * the span is broken). Review 17: only the run right before the chosen enemy was looked at, so with the front enemy
   * on that cell the tag fell to the border between the two enemies' feet and read as the distance between them; the
   * empty cell nearer the hero now carries it. With no empty cell in the span at all, it stands on the chosen enemy's
   * near edge.
   */
  function gapPlace(g) {
    const span = FloorSpec.GapSpan(cells, g.pc, g.ps, g.ec);
    let from = span.from;
    let to = span.to;
    let broken = false;
    const first = g.pc + g.ps;
    const last = g.ec - 1;
    if (last >= first && lastFloor && lastFloor.Enemies && lastFloor.Enemies.length > 1) {
      const taken = new Set();
      for (const e of lastFloor.Enemies) {
        // Review fix: an enemy whose fall is still to play (HP 0, not yet Down) has already left its cells in the core —
        // its Gap is −1 and N is read past it (port notes) — so its cell counts as empty, as N counts it.
        if (!e || e.Down || e.Gap < 0 || e.Cell === g.ec) continue;
        for (let c = e.Cell; c < e.Cell + Math.max(1, e.Size | 0); c++) taken.add(c);
      }
      let blocked = false;
      for (let c = first; c <= last; c++) if (taken.has(c)) blocked = true;
      if (blocked) {
        broken = true;
        const w = FloorSpec.CellWidth(cells);
        // The empty cell of the span nearest the chosen enemy, and the run of empty cells it ends.
        let end = last;
        while (end >= first && taken.has(end)) end--;
        if (end < first) {
          from = to = FloorSpec.Left + (g.ec - 1) * w;
        } else {
          let start = end;
          while (start - 1 >= first && !taken.has(start - 1)) start--;
          from = FloorSpec.Left + (start - 1) * w; // the left edge of cell `start`
          to = FloorSpec.Left + end * w;           // the right edge of cell `end`
        }
      }
    }
    const middle = (from + to) / 2;
    const spanW = Math.max(0, to - from);
    const width = g.gap <= 0 ? FloorSpec.TagAtZero : Math.max(FloorSpec.TagAtZero, spanW);
    if (!broken) return { left: middle - width / 2, width, spanW, broken, chipDx: 0 };
    // Review 19 (a Tab at rest moved the N from the front enemy to the back one, and the tag stood over the same empty
    // cells both times): the brackets run over the whole span the number counts — from the hero's cells to the chosen
    // enemy's, past the enemy standing between — and the plate sits over the empty cells in it (chipDx from the middle).
    const full = { left: Math.min(span.from, middle - width / 2), right: Math.max(span.to, middle + width / 2) };
    return { left: full.left, width: full.right - full.left, spanW: full.right - full.left, broken, chipDx: middle - (full.left + full.right) / 2, run: spanW };
  }

  function placeGap(g, withBrackets = true) {
    if (!cells || !g || gapHidden) { gapEl.style.display = 'none'; return null; }
    gapEl.style.display = '';
    const p = gapPlace(g);
    gapEl.style.left = p.left.toFixed(1) + 'px';
    gapEl.style.width = p.width.toFixed(1) + 'px';
    chip.style.translate = p.chipDx ? p.chipDx.toFixed(1) + 'px 0' : '';
    // FloorView.SetGap (review fix): at gap 0 the plate itself is TagAtZero (100) wide over the two cells' border,
    // not only its transparent box (the DS chip kept its content width, 68).
    chip.style.minWidth = g.gap <= 0 ? FloorSpec.TagAtZero + 'px' : '';
    num.textContent = String(g.gap);
    // FloorView: the brackets only when there is room for them beside the tag (over a broken span, always: they say
    // which enemy the number is read to).
    const room = g.gap > 0 && (p.broken || p.spanW > chip.offsetWidth + 8);
    gapEl.classList.toggle('has-brackets', withBrackets && room);
    gapEl.classList.toggle('is-broken', !!p.broken);
    return p;
  }

  const api = {
    get cells() { return cells; },
    get shown() { return cells > 0; },

    /**
     * FloorView.Bind: the cells, the aimed ones (dense where they meet the boundary), the move frame, the gap.
     * `o.reveal` (an omen shown, §5.8): the cells that start being aimed are drawn left to right, AIM_DRAW_MS a cell
     * (0 ms: at once). Cells that stop being aimed while nothing else marks them fade over AIM_FADE_MS (§5.8 実行の後).
     */
    bind(floor, o) {
      const reveal = o && o.reveal ? o.reveal : null;
      if (o && typeof o.unread === 'function') unreadOf = o.unread;
      // §5.8 提示 (review 21: with two or three enemies the first omen drew every enemy's new stripes, the back enemy's 240 ms
      // before its own badge dropped): an omen shown for enemy `reveal.unit` draws that enemy's new cells only; the others
      // keep the cells they showed until their own omen comes in (or the event's frame lands).
      if (reveal && reveal.unit != null && floor && floor.Enemies && floor.Enemies.length > 1 && lastFloor && lastFloor.Enemies) {
        const was = lastFloor.Enemies;
        floor = Object.assign(Object.create(Object.getPrototypeOf(floor)), floor, {
          Enemies: floor.Enemies.map((e) => {
            if (!e || e.Unit === reveal.unit) return e;
            const p = was.find((x) => x && x.Unit === e.Unit);
            return p ? Object.assign(Object.create(Object.getPrototypeOf(e)), e, { AimCells: p.AimCells, DenseCells: p.DenseCells, MoveCells: p.MoveCells }) : e;
          }),
        });
      }
      lastFloor = floor;
      const n = floor && floor.Cells > 0 ? floor.Cells : 0;
      root.style.display = n > 0 ? '' : 'none';
      if (n !== cells) build(n);
      if (!n) return;
      const drawn = [];
      // battle-visual-v1 §4.2 (several enemies, the JS writer's FloorFrame.Enemies): each standing enemy's
      // aimed cells in its own direction (EnemyHatch), crossing where two overlap; else the one enemy's at 45°.
      // Review 19: a one-enemy frame carries FloorFrame.Enemies too, so the fallen one's cells went with `soloRetired`
      // nowhere and stayed striped through its whole fall.
      const several = !!(floor.Enemies && floor.Enemies.length > 1);
      const unreadFoe = (e) => (e && unreadOf(several ? e.Unit : undefined)
        ? Object.assign({}, e, { AimCells: [], DenseCells: [], MoveCells: [] }) : e);
      const foes = (aimRetired ? [] : floor.Enemies && floor.Enemies.length
        ? floor.Enemies.filter((e) => e && !e.Down && !retiredUnits.has(e.Unit) && !(soloRetired && floor.Enemies.length === 1))
        : soloRetired ? [] : [{ Hatch: 0, AimCells: floor.AimCells || [], DenseCells: floor.DenseCells || [], MoveCells: floor.MoveCells || [] }]).map(unreadFoe);
      tiles.forEach((t, i) => {
        const c = i + 1;
        const threats = [];
        let moveTo = false;
        let moveFaded = true;
        const focusing = focusUnit != null && foes.length > 1;
        for (const e of foes) {
          const faded = focusing && e.Unit !== focusUnit;
          if ((e.AimCells || []).indexOf(c) >= 0) {
            threats.push({ angle: foes.length > 1 || e.Hatch ? hatchAngle(e.Hatch) : AIM_ANGLE, boundary: (e.DenseCells || []).indexOf(c) >= 0, faded });
          }
          if ((e.MoveCells || []).indexOf(c) >= 0) { moveTo = true; if (!faded) moveFaded = false; }
        }
        const key = threats.map((x) => x.angle + (x.boundary ? 'd' : '') + (x.faded ? 'f' : '')).join(',') + (moveTo ? 'm' + (moveFaded && focusing ? 'f' : '') : '');
        if (key === t.key) return;
        const had = t.threat;
        t.key = key;
        t.threat = threats.length > 0;
        const box = t.el.threatBox;
        const mine = ++t.fade;
        if (box) box.style.clipPath = '';
        // §5.8 実行の後: stripes that go with nothing in their place fade out (120, linear), then the cell is cleared.
        if (had && !threats.length && !moveTo && !aimRetired && box && box.firstChild) {
          const from = aimAlpha;
          clock.run(AIM_FADE_MS, 'linear', (k) => { if (t.fade === mine) box.style.opacity = (from * (1 - k)).toFixed(3); })
            .then(() => { if (t.fade === mine) { t.el.setThreat([], false); box.style.opacity = ''; } });
          return;
        }
        t.el.setThreat(threats, moveTo);
        // battle-visual-v1 §4.2 / §5.8 (review fix, two or three enemies): one enemy's move destination on a cell another
        // enemy aims at. The DS tile draws the violet dashed frame first and the red threat frame over it at the same rect,
        // so the destination vanished; it stands in front instead, 4 px inside the red frame, on a dark keyline.
        if (moveTo && threats.length && box) {
          const mv = box.querySelector('.ocb-tile-move');
          if (mv) { mv.classList.add('ocbg-battle-move-over'); box.appendChild(mv); }
        }
        if (reveal && !had && threats.length && box) drawn.push({ t, box, mine });
        // §2.2.2: the hovered badge's enemy keeps its stripes; the others' fade (a cell only theirs fades whole).
        if (box) box.style.opacity = aimAlpha < 1 ? aimAlpha.toFixed(3) : '';
        const threatEl = box ? box.querySelector('.ocb-tile-threat') : null;
        const moveEl = box ? box.querySelector('.ocb-tile-move') : null;
        if (moveEl) moveEl.style.opacity = moveFaded && focusing ? String(UNFOCUSED_ALPHA) : '';
        if (threatEl) {
          const allFaded = threats.every((x) => x.faded);
          threatEl.style.opacity = allFaded ? String(UNFOCUSED_ALPHA) : '';
          if (!allFaded) [...threatEl.children].forEach((svgEl, k) => { if (threats[k] && threats[k].faded) svgEl.style.opacity = String(UNFOCUSED_ALPHA); });
        }
      });
      // §5.8 提示: the new stripes are drawn left to right, one cell after another (clip from the left edge).
      const per = reveal && reveal.ms != null ? reveal.ms : AIM_DRAW_MS;
      drawn.forEach(({ t, box, mine }, k) => {
        if (!(per > 0)) return;
        box.style.clipPath = 'inset(0 100% 0 0)';
        clock.wait(per * k).then(() => clock.run(per, 'linear', (q) => {
          if (t.fade === mine) box.style.clipPath = q >= 1 ? '' : 'inset(0 ' + ((1 - q) * 100).toFixed(1) + '% 0 0)';
        }));
      });
      applyReach();
      recount++;
      gapState = { gap: floor.Gap, pc: floor.PlayerCell, ps: Math.max(1, floor.PlayerSize || 1), ec: floor.EnemyCell };
      placeGap(gapState);
    },

    /**
     * battle_ui_ux_v2 §2.2.2 (two or more enemies): while enemy `unit`'s omen badge is hovered only its aimed
     * cells stay lit and the others' fade; null lights them all again. The gap number is left as it is.
     */
    focusAim(unit) {
      const u = unit != null && unit >= 0 ? unit : null;
      if (u === focusUnit) return;
      focusUnit = u;
      if (!lastFloor || !cells) return;
      const keep = gapState;
      api.bind(lastFloor);
      if (keep) { gapState = keep; placeGap(gapState); }
    },

    /** §4.2 届くマスの光: the cells an enemy-aimed card reaches, while it is hovered or held. */
    showReach(list) {
      reach = (list || []).slice();
      applyReach();
    },
    hideReach() {
      reach = [];
      applyReach();
    },

    setGap(gap, pc, ps, ec) {
      if (!cells) return;
      recount++;
      gapState = { gap, pc, ps: Math.max(1, ps || 1), ec: ec > 0 ? ec : gapState ? gapState.ec : 1 };
      num.style.transform = '';
      num.style.opacity = '';
      placeGap(gapState);
    },

    /**
     * FloorView.RecountGap (§5.7 順 2): the old number rises out of the tag, the new one rises in from
     * below, and the tag slides to the middle of the new empty cells, all in `ms`. The brackets wait for
     * the end.
     */
    recountGap(gap, pc, ps, ec, ms) {
      if (!cells || !gapState) return clock.wait(ms);
      const before = String(num.textContent);
      const from = { left: parseFloat(gapEl.style.left) || 0, width: parseFloat(gapEl.style.width) || FloorSpec.TagAtZero, dx: parseFloat(chip.style.translate) || 0 };
      gapState = { gap, pc, ps: Math.max(1, ps || 1), ec: ec > 0 ? ec : gapState.ec };
      const to = gapPlace(gapState);
      const after = String(gap);
      const mine = ++recount;
      if (!(ms > 0)) { placeGap(gapState); return Promise.resolve(); }
      gapEl.classList.remove('has-brackets');
      return clock.run(ms, 'out', (t) => {
        if (mine !== recount) return;
        gapEl.style.left = (from.left + (to.left - from.left) * t).toFixed(1) + 'px';
        gapEl.style.width = (from.width + (to.width - from.width) * t).toFixed(1) + 'px';
        const dx = from.dx + ((to.chipDx || 0) - from.dx) * t;
        chip.style.translate = Math.abs(dx) > 0.05 ? dx.toFixed(1) + 'px 0' : '';
        const leaving = t < 0.5;
        const half = leaving ? t * 2 : (t - 0.5) * 2;
        num.textContent = leaving ? before : after;
        num.style.transform = 'translateY(' + (leaving ? -RECOUNT_TRAVEL * half : RECOUNT_TRAVEL * (1 - half)).toFixed(1) + 'px)';
        num.style.opacity = (leaving ? 1 - half : half).toFixed(3);
      }).then(() => {
        if (mine !== recount) return;
        num.style.transform = '';
        num.style.opacity = '';
        placeGap(gapState);
      });
    },

    /** The battle is over: the aimed / dense / move cells fade with the omen badges (OmenSpend's ms) and stay clear. */
    retireAim(ms) {
      if (aimRetired) return Promise.resolve();
      const mine = ++aimToken;
      const clear = () => {
        if (mine !== aimToken) return;
        aimRetired = true;
        aimAlpha = 1;
        tiles.forEach((t) => { t.key = ''; t.threat = false; t.fade++; t.el.setThreat([], false); if (t.el.threatBox) { t.el.threatBox.style.opacity = ''; t.el.threatBox.style.clipPath = ''; } });
      };
      if (!(ms > 0)) { clear(); return Promise.resolve(); }
      return clock.run(ms, 'in', (t) => {
        if (mine !== aimToken) return;
        aimAlpha = 1 - t;
        tiles.forEach((tl) => { if (tl.el.threatBox) tl.el.threatBox.style.opacity = aimAlpha.toFixed(3); });
      }).then(clear);
    },
    /**
     * battle_ui_ux_v2 §5.15 順 5 (review fix): enemy `unit` falls (`solo`: the battle's one enemy, whose frame may or may
     * not carry FloorFrame.Enemies). Its aimed, dense and move cells go now, with its omen badge (§5.8's 120 ms fade for cells it
     * alone marked), instead of with the next frame a second later; the gap number is left as it is.
     */
    retireUnit(unit, solo) {
      const known = unit != null && unit >= 0;
      const before = retiredUnits.size + (soloRetired ? 1 : 0);
      if (known) retiredUnits.add(unit);
      if (solo || !known) soloRetired = true;
      if (retiredUnits.size + (soloRetired ? 1 : 0) === before) return;
      if (!lastFloor || !cells) return;
      const keep = gapState;
      api.bind(lastFloor);
      if (keep) { gapState = keep; placeGap(gapState); }
    },
    /** The battle is over (true): the gap number goes; it measured from the hero to an enemy that is no more. */
    setGapHidden(on) {
      on = !!on;
      if (on === gapHidden) return;
      gapHidden = on;
      if (gapState) placeGap(gapState); else gapEl.style.display = on ? 'none' : '';
    },

    /**
     * battle_ui_ux_v2 §5.7 順 7 (review 19): a pushed figure hit the end of the line toward `dir` (−1 left, +1 right) —
     * that end of the floor lights for `ms` (out) and fades. Drawn in the floor (it slides with the arena).
     */
    edgeFlash(dir, ms) {
      if (!cells) return Promise.resolve();
      const x = dir < 0 ? FloorSpec.Left : FloorSpec.Right;
      const el = h('i', { class: 'ocbg-battle-wall-edge' });
      el.style.left = (x - WALL_EDGE.w / 2).toFixed(1) + 'px';
      el.style.top = TOP - WALL_EDGE.up + 'px';
      el.style.width = WALL_EDGE.w + 'px';
      el.style.height = WALL_EDGE.up + FloorSpec.Height + 'px';
      root.appendChild(el);
      return clock.run(Math.max(1, ms), 'out', (t) => { el.style.opacity = (1 - 0.3 * t).toFixed(3); })
        .then(() => clock.run(WALL_EDGE.fade, 'in', (t) => { el.style.opacity = (0.7 * (1 - t)).toFixed(3); }))
        .then(() => el.remove());
    },

    /** FloorView.FeetOf's x: the middle of the cells a figure takes. */
    feetX(cell, size) {
      return cells > 0 ? FloorSpec.FeetX(cells, cell, size) : null;
    },

    cellBox(cell) {
      if (!cells || cell < 1 || cell > cells) return null;
      return { x: FloorSpec.TileLeft(cells, cell), y: TOP, w: FloorSpec.TileWidth(cells), h: FloorSpec.Height };
    },

    destroy() {
      root.remove();
    },
  };
  return api;
}
