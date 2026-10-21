// One unit's number panel: a port of StatusBarView (battle-visual-v1 §4.5, §7.3, #242 / #455) drawn with
// the design system's panel, bar and chip looks. The HP row (heart, number, bar with the 10 HP marks, the
// grey band that follows the bar down, the wick stripes of a held card's HP loss), the Guard row (shield and
// number; an enemy's stamina dot, number and 構えの盾 on the right) and the status chips under them, which
// wrap, fold into "+n" and open their 説明パネル on hover. It shows the values it is handed; the bar's
// width is the only thing derived here (a drawing scale, not a rule).
import { h } from '../../common/stage.js';
import { UnitSide, ChipKind } from '../../../bridge/depictionScript/DepictionTypes.js';
import { StatusPanelLayout as SPL } from '../../../bridge/depictionScript/StatusPanelLayout.js';
import { HpWickSpec } from '../../../bridge/depictionScript/HpWickSpec.js';
import { EffectId } from '../../../bridge/depictionScript/Effects.js';
import { pop, shake, lerp, clamp, mix, tok, statusIconOf, shieldIcon } from './util.js';

// StatusBarView's own numbers (the prefab's, laid out again at run time).
const HP_PER_TICK = 10;
/**
 * battle-visual-v1 §4.5 「10 HP ごとに…目盛り」, battle_ui_ux_v2 §1.1 B1 「目盛は 10 刻み」 and Unity's StatusBarView.RenderTicks
 * (review 19: on the 37 px bars of 8 cells the marks were thinned to every 20 HP, and nothing said so): a mark every 10 HP
 * always. Where they would stand closer than TICK_THIN_GAP px they are drawn 1 px wide (2 px otherwise), so a narrow bar
 * keeps its scale without turning into a barcode.
 */
const TICK_THIN_GAP = 8;
const PLAYER_CHIPS_MAX = 6;
const ENEMY_CHIPS_MAX = 4;
const HEART = 20;
const HP_NUMBER_WIDTH = 44;
const PART_GAP = 6;
const STAMINA_NUMBER_WIDTH = 30;
const BAR_HEIGHT = 14;
const RESERVE_SHIELD = 24;
const STAMINA_DOT = 16;
/** The chips' 説明パネル: 340 wide, its bottom 38 px over the chip's bottom, 16 px inside the screen. */
const TIP_WIDTH = 340;
const TIP_ABOVE = 38;
const TIP_EDGE = 16;
/** The prefix a lasting stance's chip label carries (StatusChipText / StatusBarView.ChipText). */
const LASTING_PREFIX = '構え・';
/** The 説明パネル's two row heads (StatusBarView.ShowPanel). scene text */
const EFFECT_HEAD = '効き方';
const DECAY_HEAD = '減り方';
/** The +n chip's rows join a chip and its effect with this (StatusBarView.ShowPanel). scene text */
const REST_JOIN = '：';

const HP_ROW_MID = SPL.PadTop + SPL.HpRowHeight / 2; // 20
const GUARD_ROW_TOP = SPL.PadTop + SPL.HpRowHeight + SPL.RowGap; // 40
const BAR_LEFT = SPL.PadSide + HEART + PART_GAP + HP_NUMBER_WIDTH + PART_GAP; // 86
/** battle-visual-v1 §4.5 (two or more enemies): a 16 px swatch at the left end of the HP row, before the heart. */
const SWATCH = 16;

function copyChip(c) {
  // StatusBarView.Copy: a source from before #242 marks a stance as a chip with no stacks and no family.
  const label = c.Label == null ? '' : String(c.Label);
  const lasting = c.Kind === ChipKind.Lasting || (c.Stacks <= 0 && label.startsWith(LASTING_PREFIX));
  return { Label: label, Stacks: c.Stacks | 0, Kind: lasting ? ChipKind.Lasting : c.Kind, Title: c.Title || '', Group: c.Group || '', Effect: c.Effect || '', Decay: c.Decay || '' };
}

/** StatusBarView.ChipText: "出血 2", a stance "岩の構え ∞ ×2" (no "×1"), or "+n". */
export function chipText(c) {
  const label = c.Label || '';
  if (c.Kind === ChipKind.Lasting) {
    const name = label.startsWith(LASTING_PREFIX) ? label.slice(LASTING_PREFIX.length) : label;
    return name + ' ∞' + (c.Stacks > 1 ? ' ×' + c.Stacks : '');
  }
  if (label.startsWith('+')) return label;
  return label + ' ' + c.Stacks;
}

/**
 * @param env   the scene env
 * @param side  UnitSide.Player | UnitSide.Enemy
 * @param deps  { tip, onStamina(stamina, max) (the player's: the stamina tag), onStaminaPop(ms), onPip(index, ms) }
 */
export function createStatusPanel(env, side, deps = {}) {
  const { OCB, clock, layer } = env;
  const foe = side === UnitSide.Enemy;
  const tip = deps.tip;

  const st = {
    width: 0, left: 0, hpMax: 1, hp: 0, fill: 0, trail: 0, loss: 0, guard: -1, stamina: 0,
    showStamina: false, reserve: null, elite: false, down: false, chips: [], chipKey: '', rows: 0, shown: true, swatch: null,
  };

  // ---- parts ----------------------------------------------------------------------------------
  const el = h('div', { class: 'ocbg-battle-stat' + (foe ? ' is-foe' : ' is-self') });
  const edge = h('i', { class: 'ocbg-battle-stat-edge' });
  const heart = OCB.icon('heart', HEART, tok('crimson'));
  heart.classList.add('ocbg-battle-stat-heart');
  const hpNum = h('b', { class: 'ocbg-battle-stat-hp' });
  const trailEl = h('i', { class: 'ocbg-battle-bar-trail' });
  const fillEl = h('i', { class: 'ocbg-battle-bar-fill' });
  const wickEl = h('i', { class: 'ocbg-battle-bar-wick' });
  const ticksEl = h('div', { class: 'ocbg-battle-bar-ticks' });
  const bar = h('div', { class: 'ocbg-battle-bar' }, trailEl, fillEl, wickEl, ticksEl);
  // The Guard badge: the shield with its number beside it, one part that pops and shakes (StatusBarView.guardBadge).
  const guardIconBox = h('span', { class: 'ocbg-battle-guard-icon' });
  const guardNum = h('b', { class: 'ocbg-battle-guard-num' });
  const guardBadge = h('span', { class: 'ocbg-battle-guard' }, guardIconBox, guardNum);
  // An enemy's stamina dot, number and 構えの盾, right-aligned on the Guard row.
  const stNum = h('b', { class: 'ocbg-battle-foe-st-num' });
  const reserveBox = h('span', { class: 'ocbg-battle-foe-st-shield' });
  // StatusBarView: the enemy's dot is a full 16×16 circle (battle-visual-v1 §4.5 粒 16 px; review fix: the 付録 A pip
  // icon drew it at 8 px).
  const stDot = h('i', { class: 'ocbg-battle-dot' });
  stDot.style.width = STAMINA_DOT + 'px';
  stDot.style.height = STAMINA_DOT + 'px';
  stDot.style.background = tok('ink-muted');
  const stGroup = h('span', { class: 'ocbg-battle-foe-st' }, stDot, stNum, reserveBox);
  const chipsEl = h('div', { class: 'ocbg-battle-chips' });
  let swatchEl = null;
  el.append(edge, heart, hpNum, bar, guardBadge, stGroup, chipsEl);
  layer.panels.appendChild(el);

  // fixed parts of the layout (StatusBarView.EnsureLayout); the HP row moves right by the swatch when it has one
  /** How far the heart, the number and the bar stand right of their places (the swatch and its gap, or 0). */
  let hpShift = 0;
  function layoutHpRow() {
    heart.style.left = SPL.PadSide + hpShift + 'px';
    hpNum.style.left = SPL.PadSide + hpShift + HEART + PART_GAP + 'px';
    bar.style.left = BAR_LEFT + hpShift + 'px';
    bar.style.width = Math.max(8, st.width - SPL.PadSide - BAR_LEFT - hpShift) + 'px';
    drawTicks();
  }
  heart.style.top = HP_ROW_MID - HEART / 2 + 'px';
  hpNum.style.top = HP_ROW_MID - 15 + 'px';
  hpNum.style.width = HP_NUMBER_WIDTH + 'px';
  bar.style.top = HP_ROW_MID - BAR_HEIGHT / 2 + 'px';
  bar.style.height = BAR_HEIGHT + 'px';
  guardBadge.style.left = SPL.PadSide + 'px';
  guardBadge.style.top = GUARD_ROW_TOP + 'px';
  guardBadge.style.height = SPL.GuardRowHeight + 'px';
  stGroup.style.right = SPL.PadSide + 'px';
  stGroup.style.top = GUARD_ROW_TOP + 'px';
  stGroup.style.height = SPL.GuardRowHeight + 'px';
  stNum.style.minWidth = STAMINA_NUMBER_WIDTH + 'px';
  stGroup.style.display = 'none';
  chipsEl.style.left = SPL.PadSide + 'px';
  chipsEl.style.top = SPL.ChipRowTop + 'px';
  // HpWickSpec: 3 px of wick, 3 px of the bar between, running ／ (the mockup's 135° gradient).
  wickEl.style.background = 'repeating-linear-gradient(' + HpWickSpec.AngleDegrees + 'deg, ' + tok('wick') + ' 0 '
    + HpWickSpec.StripeWidth + 'px, transparent ' + HpWickSpec.StripeWidth + 'px ' + HpWickSpec.Period + 'px)';

  // ---- drawing ----------------------------------------------------------------------------------
  const widthOf = (hp) => clamp(hp / st.hpMax, 0, 1);

  function drawBar() {
    fillEl.style.width = (st.fill * 100).toFixed(3) + '%';
    trailEl.style.width = (st.trail * 100).toFixed(3) + '%';
  }

  /** StatusBarView.RenderWick: the stripes over the part of the bar a held card would empty. */
  function drawWick() {
    const span = foe ? HpWickSpec.Span(st.hp, st.hpMax, st.loss) : { ok: false };
    if (!span.ok) { wickEl.style.display = 'none'; return; }
    wickEl.style.display = 'block';
    wickEl.style.left = (span.from * 100).toFixed(3) + '%';
    wickEl.style.width = ((span.to - span.from) * 100).toFixed(3) + '%';
  }

  let ticksFor = '';
  /** StatusBarView.RenderTicks: a black 55 % mark every 10 HP of the maximum (1 px wide on a narrow bar). */
  function drawTicks() {
    const barW = Math.max(8, st.width - SPL.PadSide - BAR_LEFT - hpShift);
    const key = st.hpMax + '@' + Math.round(barW);
    if (ticksFor === key) return;
    ticksFor = key;
    ticksEl.innerHTML = '';
    const pxPerHp = barW / st.hpMax;
    const thin = HP_PER_TICK * pxPerHp < TICK_THIN_GAP;
    ticksEl.classList.toggle('is-thin', thin);
    const count = Math.floor((st.hpMax - 1) / HP_PER_TICK);
    for (let i = 0; i < count; i++) {
      const at = ((i + 1) * HP_PER_TICK) / st.hpMax;
      const tick = h('i');
      tick.style.left = 'calc(' + (at * 100).toFixed(3) + '% - ' + (thin ? 0.5 : 1) + 'px)';
      ticksEl.appendChild(tick);
    }
  }

  function setWidth(width) {
    width = Math.max(SPL.PadSide * 2, width);
    if (Math.abs(width - st.width) < 0.01) return;
    st.width = width;
    el.style.width = width + 'px';
    // A panel narrower than the 210 of §4.5 (an enemy on a line of 7 or 8 cells: EnemyPanelWidth) packs its
    // Guard row tighter, so two-digit Guard and stamina numbers keep apart at 24 px.
    el.classList.toggle('is-narrow', width < SPL.EnemyWidth);
    layoutHpRow();
    chipsEl.style.width = SPL.ContentWidth(width) + 'px';
    renderChips(true);
  }

  // ---- chips (§4.5 / §7.3) ------------------------------------------------------------------
  let chipViews = [];
  /**
   * battle_ui_ux_v2 §1.6 「並べる順は、最後にスタックが変わった順（新しいものが左）」 and §5.10 順 2 「新規は列の左端に入り」
   * (review 21; StatusBarView.SetStatus appends, and the writer's chips come in StatusKind order): each word keeps the stamp
   * of the last time its stacks changed, and the row reads newest first, so the oldest fold into 「+n」. The opening frame
   * keeps the frame's own order.
   */
  const chipStamps = new Map();
  let chipStampNow = 0;
  function orderChips(list) {
    const fresh = chipStamps.size === 0;
    const seen = new Set();
    list.forEach((c, k) => {
      seen.add(c.Label);
      const was = chipStamps.get(c.Label);
      if (!was) chipStamps.set(c.Label, { stamp: fresh ? -k : ++chipStampNow, stacks: c.Stacks });
      else if (was.stacks !== c.Stacks) chipStamps.set(c.Label, { stamp: ++chipStampNow, stacks: c.Stacks });
    });
    for (const label of [...chipStamps.keys()]) if (!seen.has(label)) chipStamps.delete(label);
    return list.slice().sort((a, b) => chipStamps.get(b.Label).stamp - chipStamps.get(a.Label).stamp);
  }

  function chipEl(c, rest) {
    const self = c.Kind === ChipKind.OnSelf || c.Kind === ChipKind.Lasting;
    const known = statusIconOf(OCB, c.Label);
    const node = h('span', {
      class: 'ocb-chip ocbg-battle-chip' + (self ? ' is-self' : '') + (c.Kind === ChipKind.Boss ? ' is-boss' : ''),
    });
    if (rest) {
      node.appendChild(h('span', { class: 'ocb-mono' }, c.Label));
    } else if (c.Kind === ChipKind.Lasting) {
      const name = c.Label.startsWith(LASTING_PREFIX) ? c.Label.slice(LASTING_PREFIX.length) : c.Label;
      node.appendChild(h('span', null, name));
      node.appendChild(OCB.icon('infinity', 18, tok('hp-text')));
      if (c.Stacks > 1) node.appendChild(h('span', { class: 'ocb-mono' }, '×' + c.Stacks));
    } else {
      if (known) node.appendChild(OCB.icon(known.id, 18, tok('ink')));
      node.appendChild(h('span', null, c.Label));
      node.appendChild(h('span', { class: 'ocb-mono' }, String(c.Stacks)));
    }
    const view = { el: node, chip: c, rest };
    node.addEventListener('mouseenter', () => showChipTip(view));
    node.addEventListener('mouseleave', () => tip && tip.hide(view));
    // §3.13 (review 21): a right click pins the same panel (効き方 / 減り方) until a click, Esc or another right click.
    node.addEventListener('contextmenu', (e) => { e.preventDefault(); e.ocbgPinned = true; showChipTip(view, true); });
    return view;
  }

  /**
   * StatusBarView.RenderChips: the player shows six and an enemy four; the rest gather in one "+n" chip
   * whose panel lists them. The row wraps 5 px apart and the panel grows down to hold its rows.
   */
  function renderChips(force) {
    const key = st.chips.map((c) => c.Kind + '|' + c.Label + '|' + c.Stacks).join(';') + '@' + st.width;
    if (!force && key === st.chipKey) {
      // Review 16: the same chips, but their texts may be new — a cue's setStatus made the chip with empty Group /
      // Effect / Decay, and the event's frame brings them. The views take the frame's chips (their 説明パネル reads them).
      const most = foe ? ENEMY_CHIPS_MAX : PLAYER_CHIPS_MAX;
      const gather = st.chips.length > most;
      const shown = gather ? most - 1 : st.chips.length;
      chipViews.forEach((v, i) => {
        if (v.rest) v.rest = st.chips.slice(shown);
        else if (st.chips[i]) v.chip = st.chips[i];
      });
      return;
    }
    st.chipKey = key;
    if (tip) chipViews.forEach((v) => tip.hide(v));
    chipsEl.innerHTML = '';
    const most = foe ? ENEMY_CHIPS_MAX : PLAYER_CHIPS_MAX;
    const gather = st.chips.length > most;
    const shown = gather ? most - 1 : st.chips.length;
    chipViews = [];
    for (let i = 0; i < shown; i++) chipViews.push(chipEl(st.chips[i], null));
    if (gather) {
      const rest = st.chips.slice(shown);
      chipViews.push(chipEl({ Label: '+' + rest.length, Stacks: 0, Kind: ChipKind.OnSelf }, rest));
    }
    chipViews.forEach((v) => chipsEl.appendChild(v.el));
    // The rows the wrap made (one per distinct top), for the panel's height (StatusPanelLayout.Height).
    const tops = new Set(chipViews.map((v) => v.el.offsetTop));
    st.rows = chipViews.length ? Math.max(1, tops.size) : 0;
    el.style.height = SPL.Height(st.rows) + 'px';
    // How far from the panel's left edge its chips reach (the hover line keeps off them when it must cover the panel).
    st.chipsW = chipViews.reduce((m, v) => Math.max(m, chipsEl.offsetLeft + v.el.offsetLeft + v.el.offsetWidth), 0);
  }

  function chipOf(label) {
    return chipViews.find((v) => !v.rest && v.chip.Label === label) || null;
  }

  /** StatusBarView.ShowPanel: the word's 説明パネル above the chip, kept inside the screen. */
  function showChipTip(view, pin) {
    if (!tip || !st.shown) return;
    const c = view.chip;
    const head = h('div', { class: 'ocb-tip-head' });
    const known = !view.rest ? statusIconOf(OCB, c.Label) : null;
    if (known) head.appendChild(OCB.icon(known.id, 20, tok('ink')));
    else if (c.Kind === ChipKind.Lasting) head.appendChild(OCB.icon('infinity', 20, tok('hp-text')));
    head.appendChild(h('span', null, view.rest ? c.Label : (c.Title || chipText(c))));
    if (!view.rest && c.Group) head.appendChild(h('small', null, c.Group));
    const body = [];
    if (view.rest) {
      view.rest.forEach((o) => body.push(h('p', null, chipText(o) + REST_JOIN + (o.Effect || ''))));
    } else if (c.Effect || c.Decay) {
      body.push(h('p', null, h('span', null, EFFECT_HEAD), c.Effect || ''));
      body.push(h('p', null, h('span', null, DECAY_HEAD), c.Decay || ''));
    }
    const node = h('div', { class: 'ocb-tip ocbg-battle-chiptip' }, head, body);
    node.style.width = TIP_WIDTH + 'px';
    const place = (n) => {
      const chipLeft = st.left + SPL.PadSide + view.el.offsetLeft;
      const chipBottom = SPL.Top + SPL.ChipRowTop + view.el.offsetTop + SPL.ChipHeight;
      n.style.left = clamp(chipLeft, TIP_EDGE, env.L.W - TIP_EDGE - TIP_WIDTH) + 'px';
      n.style.top = chipBottom - TIP_ABOVE - n.offsetHeight + 'px';
    };
    if (pin) tip.pin(node, place, view); else tip.show(node, place, view);
  }

  // ---- the Guard row ------------------------------------------------------------------------
  function setGuard(guard) {
    guardNum.textContent = String(guard);
    const on = guard > 0;
    if ((st.guard > 0) !== on || st.guard < 0) {
      guardIconBox.innerHTML = '';
      guardIconBox.appendChild(shieldIcon(OCB, SPL.GuardRowHeight, on));
    }
    st.guard = guard;
    guardBadge.classList.toggle('is-zero', !on);
  }

  function setReserve(likely) {
    if (st.reserve === likely) return;
    st.reserve = likely;
    reserveBox.innerHTML = '';
    // §4.5 構えの盾: Guard-coloured, solid when 構え is expected, dashed when not.
    reserveBox.appendChild(likely
      ? OCB.icon('shield', RESERVE_SHIELD, tok('guard'), { fill: tok('guard'), fillOpacity: 0.2 })
      : OCB.icon('shield', RESERVE_SHIELD, tok('guard'), { dashed: true }));
  }

  // ---- HP -------------------------------------------------------------------------------------
  function setHpKeepingTrail(hp) {
    st.hp = hp;
    hpNum.textContent = String(hp);
    st.fill = widthOf(hp);
    if (st.trail < st.fill) st.trail = st.fill; // a heal leaves no band behind
    drawBar();
    drawWick();
  }
  function snapTrail() {
    st.trail = st.fill;
    drawBar();
  }

  // ---- api --------------------------------------------------------------------------------------
  const api = {
    el,
    side,
    get width() { return st.width; },
    get left() { return st.left; },
    get height() { return SPL.Height(st.rows); },
    get stamina() { return st.stamina; },
    /** The HP the panel shows now (the bar's number). */
    get hp() { return st.hp; },
    /** Shown and holding its place (a fallen enemy's panel that gave its place up to a standing one's is not). */
    get shown() { return st.shown !== false && !st.yielded; },
    /** The panel's box on the stage as it stands (from its layout numbers: no layout read, it is asked every frame). */
    box() { return { x: st.left, y: SPL.Top, w: st.width, h: SPL.Height(st.rows), chipsW: st.rows ? st.chipsW || 0 : 0 }; },
    setWidth,
    /** PlaceAt: the panel's top-left corner at (left, StatusPanelLayout.Top). */
    placeAt(left) {
      if (st.placed && Math.abs(left - st.left) < 0.05) return;
      st.placed = true;
      st.left = left;
      el.style.left = left.toFixed(1) + 'px';
      el.style.top = SPL.Top + 'px';
    },
    /**
     * battle-visual §4.2 / §4.5 (several enemies): the enemy's hatch swatch at the left end of its HP row,
     * 16 px, before the heart (the heart, the number and the bar move right to make room); null takes it away.
     * With the JS writer's Hatch.
     */
    setSwatch(angle) {
      if (st.swatch === angle) return;
      st.swatch = angle;
      if (swatchEl) { swatchEl.remove(); swatchEl = null; }
      hpShift = angle == null ? 0 : SWATCH + PART_GAP;
      layoutHpRow();
      if (angle == null) return;
      swatchEl = OCB.swatch(angle, SWATCH);
      swatchEl.classList.add('ocbg-battle-stat-swatch');
      swatchEl.style.left = SPL.PadSide + 'px';
      swatchEl.style.top = HP_ROW_MID - SWATCH / 2 + 'px';
      el.appendChild(swatchEl);
    },
    /** Opaque (panel-solid) while another layer could show through (the field's plates behind, several enemies). */
    setSolid(on) { el.classList.toggle('is-solid', !!on); },
    /** Review 19: the battle's focus (the amber edge) — the enemy a Tab at rest chose (the floor's N is read to it). */
    setFocus(on) { el.classList.toggle('is-focus', !!on); },
    show(on) {
      st.shown = !!on;
      el.style.display = on ? '' : 'none';
      if (!on && tip) chipViews.forEach((v) => tip.hide(v));
    },
    /**
     * Several enemies (review fix): a fallen enemy's grey panel gives its place up — hidden — while a standing enemy's
     * panel (or the hero's) stands over it; it comes back when that one moves on.
     */
    setYielded(on) {
      on = !!on;
      if (!!st.yielded === on) return;
      st.yielded = on;
      el.style.visibility = on ? 'hidden' : '';
      el.dataset.yielded = on ? '1' : '';
      if (on && tip) chipViews.forEach((v) => tip.hide(v));
    },
    /** battle_ui_ux_v2 §2.2.6: a fallen enemy's panel stays, grey. */
    setDown(on) {
      on = !!on;
      if (st.greying) {
        // a greyOut under way ends here: its inline look gives way to the class's
        st.greying = false;
        st.greyToken = (st.greyToken || 0) + 1;
        el.style.opacity = '';
        el.style.filter = '';
      }
      if (st.down === on) return;
      st.down = on;
      el.classList.toggle('is-down', on);
      if (on) api.showHpLoss(0);
    },
    /**
     * battle_ui_ux_v2 §5.15 順 5 (scene addition with several enemies; review fix: the panel turned grey at once): the
     * panel turns grey over `ms` (linear) — to the fallen look's opacity and grey — and is then down (setDown).
     */
    greyOut(ms) {
      if (st.down) return Promise.resolve();
      if (!(ms > 0)) { api.setDown(true); return Promise.resolve(); }
      const mine = (st.greyToken = (st.greyToken || 0) + 1);
      st.greying = true;
      const to = parseFloat(getComputedStyle(el).getPropertyValue('--opacity-unplayed')) || 0.55;
      api.showHpLoss(0);
      return clock.run(ms, 'linear', (t) => {
        if (st.greyToken !== mine) return;
        el.style.opacity = lerp(1, to, t).toFixed(3);
        el.style.filter = 'grayscale(' + t.toFixed(3) + ')';
      }).then(() => { if (st.greyToken === mine) api.setDown(true); });
    },
    /** StatusBarView.Bind. */
    bind(unit) {
      if (!unit) return;
      st.hpMax = Math.max(1, unit.HpMax | 0);
      drawTicks();
      api.setHp(unit.Hp);
      setGuard(unit.Guard);
      stGroup.style.display = unit.ShowStamina && foe ? '' : 'none';
      setReserve(!!unit.ReserveLikely);
      st.elite = !!unit.Elite;
      el.classList.toggle('is-elite', st.elite);
      st.showStamina = !!unit.ShowStamina;
      if (unit.ShowStamina) api.setStamina(unit.Stamina, unit.StaminaMax);
      // battle-visual-v1 §4.6 (v1.5, #505): a stance is not a chip here — its frame in the stance list
      // alone carries "×n" and 永続, so stances never push 出血 or 鈍足 into "+n".
      st.chips = orderChips((unit.Statuses || []).map(copyChip).filter((c) => c.Kind !== ChipKind.Lasting));
      renderChips(false);
    },
    setGuard(n) { if (n >= 0) setGuard(n); },
    /** StatusBarView.PopGuard: the badge pops (UiTween.Pop) with the new number. */
    popGuard(n, ms = 260) {
      if (n >= 0) setGuard(n);
      return pop(clock, guardBadge, ms);
    },
    /** StatusBarView.CrackGuard: the shield flashes white and the badge shakes as it breaks. */
    crackGuard(ms) {
      clock.run(ms, 'out', (t) => { guardIconBox.style.filter = t < 1 ? 'brightness(' + lerp(2.4, 1, t).toFixed(3) + ')' : ''; });
      return shake(clock, guardBadge, 7, 2, ms);
    },
    setHp(hp) { setHpKeepingTrail(hp); snapTrail(); },
    /** StatusBarView.AnimateHp: the number lands at once; the bar slides (Ease.Out). */
    animateHp(hp, ms) {
      st.hp = hp;
      hpNum.textContent = String(hp);
      drawWick();
      const from = st.fill;
      const to = widthOf(hp);
      if (st.trail < to) st.trail = to;
      return clock.run(ms, 'out', (t) => { st.fill = lerp(from, to, t); drawBar(); });
    },
    /** StatusBarView.AnimateTrail: the grey band catches up with the bar (Ease.InOut). */
    animateTrail(ms) {
      const from = st.trail;
      const to = st.fill;
      return clock.run(ms, 'inout', (t) => { st.trail = lerp(from, to, t); drawBar(); });
    },
    snapTrail,
    setHpKeepingTrail,
    /** StatusBarView.ShowHpLoss: the wick stripes (HpWickSpec.Span); 0 takes them away. An enemy's panel only. */
    showHpLoss(n) {
      n = n | 0;
      if (n === st.loss) return;
      st.loss = n;
      drawWick();
    },
    /** StatusBarView.SetStatus: one word's chip settled at once — added, restacked, or gone at 0. */
    setStatus(label, stacks) {
      const i = st.chips.findIndex((c) => c.Label === label);
      if (stacks <= 0) {
        if (i >= 0) st.chips.splice(i, 1);
      } else if (i >= 0) {
        st.chips[i] = Object.assign({}, st.chips[i], { Stacks: stacks });
        if (st.chips[i].Kind !== ChipKind.Lasting) st.chips[i].Title = label + ' ' + stacks;
      } else {
        // The cue names the word only; the frame that settles the event brings its panel text. The corner
        // is read off the design system's status table until then (Unity draws the default, square).
        const known = statusIconOf(OCB, label);
        st.chips.push({ Label: label, Stacks: stacks, Kind: known && known.self ? ChipKind.OnSelf : ChipKind.OnFoe, Title: label + ' ' + stacks, Group: '', Effect: '', Decay: '' });
      }
      st.chips = orderChips(st.chips);
      renderChips(false);
    },
    /** StatusBarView.PlayStatus: StatusApply swells in, StatusStack pops, StatusTick shrinks for a moment, StatusVanish fades. */
    playStatus(label, stacks, beat, ms) {
      if (beat === EffectId.StatusVanish) {
        const going = chipOf(label);
        const done = going
          ? clock.run(ms, 'linear', (t) => { going.el.style.opacity = (1 - t).toFixed(3); going.el.style.scale = lerp(1, 0.7, t).toFixed(3); })
          : Promise.resolve();
        return done.then(() => api.setStatus(label, 0));
      }
      api.setStatus(label, stacks);
      const shown = chipOf(label);
      if (!shown) return clock.wait(ms);
      const chip = shown.el;
      let run;
      if (beat === EffectId.StatusApply) {
        run = clock.run(ms, 'out', (t) => {
          const s = t < 0.6 ? lerp(0.3, 1.2, t / 0.6) : lerp(1.2, 1, (t - 0.6) / 0.4);
          chip.style.scale = s.toFixed(3);
        });
      } else if (beat === EffectId.StatusStack) {
        run = pop(clock, chip, ms);
      } else {
        run = clock.run(ms, 'out', (t) => { chip.style.scale = (1 - 0.18 * Math.sin(t * Math.PI)).toFixed(3); });
      }
      return run.then(() => { chip.style.scale = ''; });
    },
    /** StatusBarView.SetStamina (the player's is handed on to the stamina tag: StaminaShown). */
    setStamina(stamina, max) {
      st.stamina = stamina;
      stNum.textContent = String(stamina);
      if (deps.onStamina) deps.onStamina(stamina, max);
    },
    /** StatusBarView.LightPips: the recovered points light one at a time, perPipMs apart. */
    async lightPips(stamina, max, perPipMs) {
      const from = clamp(st.stamina, 0, max);
      if (stamina <= from) { api.setStamina(stamina, max); return; }
      api.setStamina(from, max);
      for (let next = from + 1; next <= stamina; next++) {
        await clock.wait(perPipMs);
        api.setStamina(next, max);
        if (deps.onPip) deps.onPip(next - 1, perPipMs);
        else if (foe) pop(clock, stNum, perPipMs);
      }
    },
    /** A spend pops beside the card's beats (Unity pops the pip row it has hidden; here the number shown). */
    popPips(ms) {
      if (deps.onStaminaPop) return deps.onStaminaPop(ms);
      return pop(clock, stNum, ms);
    },
    /** Hover-out and removal. */
    destroy() {
      if (tip) chipViews.forEach((v) => tip.hide(v));
      el.remove();
    },
  };

  setWidth(foe ? SPL.EnemyWidth : SPL.PlayerWidth);
  api.placeAt(0);
  setGuard(0);
  setReserve(false);
  renderChips(true);
  // a grey band (whiff 85 %) behind the fill: the colour lives in hud.css; mix() keeps it a token
  trailEl.style.background = mix('miss', 85);
  return api;
}
