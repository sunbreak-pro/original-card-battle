// The enemy's omen badge: a port of OmenBadgeView and OmenIconArt (battle-visual-v1 §4.4, #349 / #455), drawn
// with the design system's omen plate and plan badge looks. 200×98, an opaque panel with a 2 px violet frame:
// the kind's icon (34) and its number (48) on the first row — or, for a 状態付与 omen, the statuses it puts on
// at 28 px (a boss's word written by name in the boss colour's double frame) — with the enemy's hatch swatch
// at the right; the kind word (16) and the hit mark on the second. 休み draws a steel dashed frame. While the
// enemy carries the omen out it wears a phosphor edge and a violet light (§4.4 敵の番). An elite's or a boss's
// plan stacks above it, 170×66 at 62 %, dashed, with 予定 / 予定変更. What the journal has not read is already
// off the frame (OmenDisclosure): this draws what is there.
import { h } from '../../common/stage.js';
import { OmenIcon, OmenHit, StatusGlyph } from '../../../bridge/depictionScript/DepictionTypes.js';
import { hatchAngle } from '../layout.js';
import { OMEN_ICON, GLYPH_ICON, omenValueColor, tok, lerp, shake } from './util.js';

// OmenBadgeView's own numbers.
const BADGE = { w: 200, h: 98 };
const PLAN = { w: 170, h: 66, gap: 8, alpha: 0.62 };
const ICON = 34;
const PLAN_ICON = 24;
const STATUS_ICON = 28;
const PLAN_STATUS_ICON = 20;
const SWATCH = 22;
const HIT_ICON = 18;
/** The ▼ that ties a badge to its enemy's head: 16 wide, 9 tall, in the 14 px between badge and head. */
const TAIL = { w: 16, h: 9 };
/** battle_ui_ux_v2 §3.10: the plan drops into the lower slot and turns solid over this before the second action. */
export const PLAN_DROP_MS = 150;
/** §5.8 提示 「頭上へ降りる」: how far over its place an omen starts its drop (scene choice). */
const DROP_PX = 24;
/** §5.9 順 12: an enemy's fired trait's word stands this long after swelling in, then fades over the next (scene choice). */
const TRAIT_WORD_HOLD_MS = 900;
const TRAIT_WORD_FADE_MS = 200;

/** OmenBadgeView.StatusRow: at most two statuses, icons in body ink, a word with no icon by name in the boss double frame. */
function statusRow(OCB, statuses, size) {
  const row = h('span', { class: 'ocbg-battle-omen-statuses' });
  (statuses || []).slice(0, 2).forEach((s) => {
    const icon = GLYPH_ICON[s.Glyph];
    if (icon && s.Glyph !== StatusGlyph.None) row.appendChild(OCB.icon(icon, size, tok('ink')));
    else row.appendChild(h('span', { class: 'ocbg-battle-bossword', style: { height: size + 'px' } }, s.Label || ''));
  });
  return row;
}

export function createOmenBadge(env) {
  const { OCB, clock, layer } = env;
  // The wrap carries the badge's place and scale; the plan is its child (it scales with the badge, as in
  // Unity) but keeps its own alpha (its CanvasGroup ignores the badge's).
  const wrap = h('div', { class: 'ocbg-battle-omenwrap' });
  const badge = h('div', { class: 'ocb-omen ocbg-battle-omen' });
  const plan = h('div', { class: 'ocbg-battle-plan' });
  wrap.append(plan, badge);
  layer.panels.appendChild(wrap);

  const st = { frame: null, shown: false, badgeAlpha: 0, planAlpha: 0, x: 0, y: 0, acting: false, struck: false, blink: 1, moveDir: 0, planDrop: 0, swatches: false, aimed: false, tail: null, drop: 0 };
  /**
   * battle_ui_ux_v2 §2.2.2 / §5.8 (review fix): 「▼ は頭に残ります」 — a small ▼ under the badge, on its enemy's head x,
   * so a badge that slid apart from its neighbour still points at whose omen it is. Review 17: every badge carries it,
   * one enemy's too (§5.8 「札は敵の頭上に置き、▼ で敵に結びます」, §5.16 「1 手目（▼）が下」); Unity draws none.
   */
  const tail = h('i', { class: 'ocbg-battle-omen-tail' });
  let kindEl = null;
  let sideEl = null;
  let hitEl = null;

  /** The badge's place on the stage, with the drop it is coming in from (popIn). */
  function placeNow() {
    wrap.style.left = st.x.toFixed(1) + 'px';
    wrap.style.top = (st.y + st.drop).toFixed(1) + 'px';
  }

  function drawAlpha() {
    badge.style.opacity = (st.badgeAlpha * st.blink).toFixed(3);
    // The plan reads at 62 % (OmenBadgeView.PlanAlpha) through its contents and frame; its panel stays opaque,
    // so the backdrop of a bright layer does not wash it out (DS: numbers and words stand on an opaque panel).
    plan.style.opacity = st.planAlpha.toFixed(3);
    plan.style.setProperty('--plan-alpha', String(PLAN.alpha));
    wrap.style.visibility = st.badgeAlpha <= 0.001 && st.planAlpha <= 0.001 ? 'hidden' : '';
  }

  /**
   * The kind's icon. A move points the way the enemy moves on screen (battle-visual-v1 §4.4; review fix: every
   * move drew ←, and Unity always draws 前へ →): the frame's own destination cells (FloorFrame.MoveCells) against
   * the enemy's cell give the way (setMoveDir); with none known it keeps the design system's ← (an enemy's step in).
   */
  function iconOf(icon, moveDir) {
    const look = OMEN_ICON[icon] || null;
    if (look && icon === OmenIcon.Move && moveDir > 0) return { icon: 'forward', color: look.color };
    return look;
  }

  function drawBadge(o) {
    badge.innerHTML = '';
    const look = iconOf(o.Icon, st.moveDir);
    const rests = o.Icon === OmenIcon.Rest;
    badge.classList.toggle('is-rest', rests);
    badge.classList.toggle('is-active', st.acting);
    const row1 = h('div', { class: 'ocb-omen-row1' });
    if (look && o.Icon !== OmenIcon.None) row1.appendChild(OCB.icon(look.icon, ICON, look.color));
    else row1.appendChild(h('span', { class: 'ocbg-battle-omen-noicon' }));
    if (o.Statuses && o.Statuses.length) row1.appendChild(statusRow(OCB, o.Statuses, STATUS_ICON));
    else if (o.ValueText) row1.appendChild(h('b', { style: { color: omenValueColor(o.Icon) } }, o.ValueText));
    const marked = o.Hit !== OmenHit.None && o.Hit != null;
    badge.appendChild(row1);
    // §4.4: the swatch at the top right ties the badge to this enemy's red stripes on the floor; it stands
    // with the hit mark (OmenBadgeView.BuildHitParts: 22×22, 12 px from the right, 14 from the top). With several
    // enemies it stands whenever this enemy's omen hatches cells on the floor (FloorEnemy.AimCells). Review 16: the DS
    // OmenPlate's 「移動の行動に赤の色見本を付けないでください」 (angle null for an action that aims at no cell) and
    // battle-visual-v1 §2.3 keep the red to the cells an action reaches, so a move omen (its cells are the violet dashed
    // frame) and a rest carry none; the badge's ▼ and the panels' swatches say whose it is.
    // battle_ui_ux_v2 §7 (review 21): an enemy read to 0 shows the kind alone — no aimed cells on the floor, so no swatch
    // for them; the hit mark stays (§7 「当たり外れの印はどの開示度でも出します」).
    const unread = o.Disclosure != null && o.Disclosure < 1;
    if (!unread && (marked || (st.swatches && st.aimed && !rests))) {
      const sw = OCB.swatch(hatchAngle(o.Hatch), SWATCH);
      sw.classList.add('ocbg-battle-omen-swatch');
      badge.appendChild(sw);
    }
    const row2 = h('div', { class: 'ocb-omen-row2' });
    kindEl = h('span', { class: 'ocbg-battle-omen-kind' }, o.KindLabel || '');
    row2.appendChild(kindEl);
    sideEl = null;
    hitEl = null;
    if (marked) {
      const lands = o.Hit === OmenHit.Lands;
      hitEl = h('span', { class: 'ocb-hitmark ' + (lands ? 'is-hit' : 'is-miss') }, OCB.icon('person', HIT_ICON, lands ? tok('phosphor') : tok('miss')));
      row2.appendChild(hitEl);
    } else if (o.SideGlyph) {
      // The v4.2 sources' reach glyph; the hit mark takes its place once the floor shows the aimed cells.
      sideEl = h('span', { class: 'ocbg-battle-omen-side' + (st.struck ? ' is-struck' : '') }, o.SideGlyph);
      row2.appendChild(sideEl);
    }
    badge.appendChild(row2);
    badge.appendChild(tail);
  }

  function drawPlan(o) {
    plan.innerHTML = '';
    plan.style.translate = '';
    if (!o.PlanVisible) return;
    const look = OMEN_ICON[o.PlanIcon] || null;
    const row1 = h('div', { class: 'ocbg-battle-plan-row1' });
    if (look && o.PlanIcon !== OmenIcon.None) row1.appendChild(OCB.icon(look.icon, PLAN_ICON, look.color));
    if (o.PlanStatuses && o.PlanStatuses.length) row1.appendChild(statusRow(OCB, o.PlanStatuses, PLAN_STATUS_ICON));
    else if (o.PlanValueText) row1.appendChild(h('b', { style: { color: omenValueColor(o.PlanIcon) } }, o.PlanValueText));
    const row2 = h('div', { class: 'ocbg-battle-plan-row2' },
      h('span', null, o.PlanKindLabel || ''),
      h('span', { class: 'ocb-plan ocbg-battle-plan-tag' + (o.PlanChanged ? ' is-changed' : '') }, o.PlanTag));
    plan.append(row1, row2);
  }

  const api = {
    el: wrap,
    /** OmenBadgeView.Bind: the badge as the frame has it (alpha 1 when Visible), the plan at 62 % when PlanVisible. */
    bind(o) {
      if (!o) return;
      st.frame = o;
      st.shown = !!o.Visible;
      st.struck = false;
      st.badgeAlpha = st.shown ? 1 : 0;
      st.planAlpha = o.PlanVisible ? 1 : 0;
      drawBadge(o);
      drawPlan(o);
      drawAlpha();
    },
    /**
     * Several enemies (SwatchesShown): the swatch stands on the badge while `aimed` — this enemy's omen hatches cells
     * on the floor (FloorEnemy.AimCells); never on a move (DS OmenPlate) or a rest.
     */
    setSwatchShown(on, aimed) {
      on = !!on;
      aimed = !!aimed;
      if (on === st.swatches && aimed === st.aimed) return;
      st.swatches = on;
      st.aimed = aimed;
      if (st.frame) drawBadge(st.frame);
    },
    /**
     * The ▼ under the badge at `dx` px from its left edge (its enemy's head x), or null for none. It stays inside the
     * badge's rounded corners.
     */
    setTail(dx) {
      const want = dx == null ? null : Math.max(TAIL.w / 2 + 6, Math.min(BADGE.w - TAIL.w / 2 - 6, dx));
      if (want === st.tail || (want != null && st.tail != null && Math.abs(want - st.tail) < 0.25)) return;
      st.tail = want;
      tail.style.display = want == null ? 'none' : '';
      // left from the badge's padding box (inside its 2 px frame)
      if (want != null) tail.style.left = (want - TAIL.w / 2 - 2).toFixed(1) + 'px';
    },
    /** A walking enemy's badge stands behind the other badges until it arrives (hud.follow; z-order only). */
    setBehind(on) {
      on = !!on;
      if (on === !!st.behind) return;
      st.behind = on;
      wrap.classList.toggle('is-behind', on);
    },
    /** The way a move omen's enemy goes on screen: −1 left, +1 right, 0 unknown (the icon keeps ←). */
    setMoveDir(dir) {
      const d = dir > 0 ? 1 : dir < 0 ? -1 : 0;
      if (d === st.moveDir) return;
      st.moveDir = d;
      if (st.frame) drawBadge(st.frame);
    },
    /**
     * battle_ui_ux_v2 §3.10 (review fix): an elite's or a boss's second action. Its first omen is spent (the
     * badge hidden) and the plan still stands over the empty slot: the plan drops into the lower slot over `ms`
     * (Ease.Out), turns solid (the full badge, as the plan's own words and number) and is the omen the enemy acts
     * on, so the acting light and a struck bonus have their badge. The next frame binds what the script says.
     * Resolves true when it promoted the plan, false when there was nothing to promote.
     */
    promotePlan(ms) {
      const o = st.frame;
      if (!o || !o.PlanVisible || st.badgeAlpha > 0.001 || st.planAlpha <= 0.001) return Promise.resolve(false);
      // Review 16: no PlanSideGlyph (the v4.2 reach text, which no other badge shows) where the hit mark goes. The frame
      // carries no hit mark and no aimed cells for the plan (OmenFrame has no PlanHit, the floor no plan cells), so the
      // promoted badge shows neither the mark nor the swatch, and the floor no stripes, until the writer writes them.
      const promoted = Object.assign(Object.create(Object.getPrototypeOf(o)), o, {
        Visible: true, Icon: o.PlanIcon, KindLabel: o.PlanKindLabel, ValueText: o.PlanValueText,
        Statuses: o.PlanStatuses || [], SideGlyph: '', Hit: OmenHit.None, PlanVisible: false,
      });
      const drop = PLAN.h + PLAN.gap;
      st.frame = promoted;
      st.shown = true;
      drawBadge(promoted);
      const finish = () => {
        st.badgeAlpha = 1;
        st.planAlpha = 0;
        plan.style.translate = '';
        plan.innerHTML = '';
        drawAlpha();
      };
      if (!(ms > 0)) { finish(); return Promise.resolve(true); }
      return clock.run(ms, 'out', (t) => {
        plan.style.translate = '0 ' + (drop * t).toFixed(1) + 'px';
        st.planAlpha = 1 - t;
        st.badgeAlpha = t;
        drawAlpha();
      }).then(() => { finish(); return true; });
    },
    /**
     * The omen comes in (battle_ui_ux_v2 §5.8 提示 「敵ごとの札が頭上へ降りる」 260 out, §5.16 順 1 the two rows together;
     * review 16 — Unity's PopIn scales 0.6 → 1): the badge (or the plan alone, a 予定変更 after the first omen is spent)
     * drops DROP_PX into place and fades in over `ms` (Ease.Out).
     */
    popIn(ms) {
      const to = st.shown ? 1 : 0;
      const planTo = st.planAlpha;
      if (!(ms > 0)) { st.badgeAlpha = to; st.drop = 0; drawAlpha(); placeNow(); return Promise.resolve(); }
      // From the first frame on: over its place and clear (the bind just before put it up whole).
      st.drop = -DROP_PX;
      st.badgeAlpha = 0;
      st.planAlpha = 0;
      drawAlpha();
      placeNow();
      return clock.run(ms, 'out', (t) => {
        st.drop = -DROP_PX * (1 - t);
        st.badgeAlpha = to * t;
        st.planAlpha = planTo * t;
        drawAlpha();
        placeNow();
      }).then(() => { st.drop = 0; st.badgeAlpha = to; st.planAlpha = planTo; drawAlpha(); placeNow(); });
    },
    /** Blink: the standing omen dips to 25 % and back once (Ease.InOut). */
    blink(ms) {
      return clock.run(ms, 'inout', (t) => { st.blink = lerp(1, 0.25, Math.sin(t * Math.PI)); drawAlpha(); })
        .then(() => { st.blink = 1; drawAlpha(); });
    },
    /** Flare: the omen swells 18 % and back as the enemy carries it out, its kind word flashing white (Ease.Out). */
    flare(ms) {
      return clock.run(ms, 'out', (t) => {
        wrap.style.scale = (1 + 0.18 * Math.sin(t * Math.PI)).toFixed(4);
        if (kindEl) kindEl.style.filter = t < 1 ? 'brightness(' + lerp(1.6, 1, t).toFixed(3) + ')' : '';
      }).then(() => { wrap.style.scale = ''; if (kindEl) kindEl.style.filter = ''; });
    },
    /**
     * Scene addition: the badge was read further (OmenFrame.Disclosure rose between two frames, the journal's
     * 開示度 handed in mid-battle): it swells once as its numbers and plan come in. Unity has no such beat.
     */
    reveal(ms) {
      return clock.run(ms, 'out', (t) => {
        wrap.style.scale = (1 + 0.12 * Math.sin(t * Math.PI)).toFixed(4);
        badge.style.filter = t < 1 ? 'brightness(' + lerp(1.5, 1, t).toFixed(3) + ')' : '';
      }).then(() => { wrap.style.scale = ''; badge.style.filter = ''; });
    },
    /**
     * battle_ui_ux_v2 §5.9 順 12 (review 19; Unity floats the line over the enemy's head): the enemy's action fires a trait —
     * its condition word swells in at the right end of the badge over `ms` (out), stands while the action plays and
     * fades. 16 px body ink on an opaque tag with the enemy's phosphor edge.
     */
    traitWord(word, ms) {
      if (!word) return Promise.resolve();
      // Review 21 (the tag hung half outside the badge's right edge, on its border, and with three enemies the next badge
      // painted over it): it stands inside the badge's right end (§5.9 順 12 「1 手目の札の右端」), over the swatch and the hit
      // mark for its moment, and the badge stands over its neighbours while it shows.
      const tag = h('span', { class: 'ocbg-battle-omen-trait' }, word);
      badge.appendChild(tag);
      wrap.classList.add('is-trait');
      const grow = ms > 0 ? clock.run(ms, 'out', (t) => { tag.style.scale = (0.6 + 0.4 * t).toFixed(3); tag.style.opacity = t.toFixed(3); }) : Promise.resolve();
      return grow.then(() => clock.wait(TRAIT_WORD_HOLD_MS)).then(() => clock.run(TRAIT_WORD_FADE_MS, 'in', (t) => { tag.style.opacity = (1 - t).toFixed(3); }))
        .then(() => { tag.remove(); if (!badge.querySelector('.ocbg-battle-omen-trait')) wrap.classList.remove('is-trait'); });
    },
    /** §4.4 敵の番: the phosphor edge and the violet light while the enemy acts on this omen. */
    setActing(on) {
      st.acting = !!on;
      badge.classList.toggle('is-active', st.acting);
    },
    /** FadeOut: the badge fades (Ease.In); the plan stays (a spent first omen keeps it up). */
    fadeOut(ms) {
      const from = st.badgeAlpha;
      return clock.run(ms, 'in', (t) => { st.badgeAlpha = lerp(from, 0, t); drawAlpha(); });
    },
    hideNow() { st.badgeAlpha = 0; drawAlpha(); },
    /** FadeOutAll: a fall takes the badge and its plan together. */
    fadeOutAll(ms) {
      const b = st.badgeAlpha;
      const p = st.planAlpha;
      return clock.run(ms, 'in', (t) => { st.badgeAlpha = lerp(b, 0, t); st.planAlpha = lerp(p, 0, t); drawAlpha(); });
    },
    hideAllNow() { st.badgeAlpha = 0; st.planAlpha = 0; drawAlpha(); },
    /** StrikeSide: the punished side did not apply — the reach glyph greys out, is struck and shakes. */
    strikeSide(ms) {
      st.struck = true;
      if (!sideEl) return clock.wait(ms);
      sideEl.classList.add('is-struck');
      return shake(clock, sideEl, 6, 2, ms);
    },
    strikeSideNow() {
      st.struck = true;
      if (sideEl) sideEl.classList.add('is-struck');
    },
    /** KeepX / KeepBottom: the badge's middle on `x` (24 px inside the edges, the caller's clamp) and its bottom on `bottom`. */
    place(left, top) {
      if (Math.abs(left - st.x) < 0.05 && Math.abs(top - st.y) < 0.05 && wrap.style.left) return;
      st.x = left;
      st.y = top;
      placeNow();
    },
    box() { return { x: st.x, y: st.y, w: BADGE.w, h: BADGE.h }; },
    show(on) { wrap.style.display = on ? '' : 'none'; },
    get visible() { return st.badgeAlpha > 0.001; },
    destroy() { wrap.remove(); },
  };
  tail.style.display = 'none';
  tail.style.top = BADGE.h - 2 + 'px'; // from under the 2 px frame (the badge's box is border-box)
  plan.style.left = (BADGE.w - PLAN.w) / 2 + 'px';
  plan.style.top = -(PLAN.h + PLAN.gap) + 'px';
  drawAlpha();
  return api;
}

export const OMEN_BADGE = BADGE;
