// Transient effects of the battle: a port of unity-project-kit/Assets/View/Depiction/DepictionFx.cs (and
// DepictionPlayer's FlyGhostTo / SlideDiscardOntoDraw) on the scene's clock. Strength comes in as the 1..4
// tier the script decided; the shape as the system the script wrote. Every length is the nominal ms
// (EffectCatalog at ×1.25), run by env.clock, so the battle speed, 演出短縮 and fast-forward stretch it.
//
// Looks: the shapes are Unity's (ProceduralArt sprites as CSS); the floating numbers wear the design
// system's FloatNum (battle-visual-v1 §5.5: a black 80% tag, an icon with every number, HP in the HP ink,
// Guard in the Guard colour) at the size DepictionFx.NumberFont gives the tier.
import { h } from '../../common/stage.js';
import { StrikeSystem } from '../../../bridge/depictionScript/DepictionTypes.js';
import { EffectGrammar, FxForm } from '../../../bridge/depictionScript/EffectGrammar.js';
import { PileLayout } from '../../../bridge/depictionScript/PileLayout.js';
import { tok, glowGradient, shapeToken, SHIELD_PATH } from './art.js';

/** DepictionFx: the blow's length and thickness by tier 1..4. */
const SLASH_LENGTH = [300, 380, 460, 560];
const SLASH_THICKNESS = [8, 12, 16, 22];
/** Review 19: a blow's shape runs this far past the struck body's far edge, and reaches at least BLOW_MIN either side. */
const BLOW_PAST = 48;
const BLOW_MIN = 40;
/** DepictionFx.MarkScale (§5.3 大きさ): 0.6 / 1.0 / 1.3 / 1.7 by tier. */
const MARK_SCALE = [0.6, 1, 1.3, 1.7];
/**
 * battle_ui_ux_v2 §5.0 / the DS Notices (浮かぶ数字; review 19): a floating number rises from the chest to the head in
 * 600 ms (ease-out). Unity's DepictionFx.FloatRoutine runs 1000 ms and rises 96 px from 40 px over the chest, which on
 * the hero and on a 2-cell elite carried the number past the head into the badge. The rise is the caller's (the figure's
 * chest-to-head span); DEFAULT_RISE is the 226 px figure's (0.19 × 226).
 */
const FLOAT_MS = 600;
const DEFAULT_RISE = 43;
/** DepictionFx.FloatRoutine: a float starts at 1.35 × its size and settles to 1 in its first 18 %. */
const POP = 1.35;
/** DepictionFx.BurstRoutine. */
const BURST_MS = 320;
/**
 * Scene addition (review fix: one enemy action's 「空振り」, trait line and Guard "+3" piled up as one muddy stack
 * of black tags on its head): a float never crosses the path of a float still on screen. It stands this far under
 * the lowest one in its way (and its widths count this much wider), so the floats of one action read top to
 * bottom in the order they came. Unity draws bare shadowed text at the same anchors; the design system's 80%
 * black tag is what makes an overlap unreadable.
 */
const FLOAT_STACK_GAP = 6;
const FLOAT_STACK_MARGIN_X = 4;
/** A float this faded (or more) no longer holds its place: what comes next may stand on it. */
const FLOAT_STACK_MIN_ALPHA = 0.15;
/** A float set beside the others (its column would reach under o.lowest) keeps this far from the stage's edges. */
const FLOAT_STAGE_W = 1920;
const FLOAT_STAGE_EDGE = 16;

/**
 * The blocked blow's shield (review 17): 100×114, about two thirds of Unity's 150×170, its face see-through; a broken
 * one's crack (in the 64×72 box of SHIELD_PATH) and how its halves part.
 */
const SHIELD = { w: 100, h: 114 };
const SHIELD_CRACK = 'M33 3 L29 17 L36 29 L30 43 L35 56 L32 69';
const SHIELD_PART = { dx: 16, dy: 12, deg: 14 };

/** The played card's ghost (o.card): the card is 216 wide (battle-visual-v1 §6), shrunk to the ghost; it starts at 90 %. */
const GHOST_CARD_W = 216;
const GHOST_CARD_ALPHA = 0.9;

const idx = (tier) => Math.max(1, Math.min(4, tier | 0 || 2)) - 1;
const lerp = (a, b, t) => a + (b - a) * t;
const SVGNS = 'http://www.w3.org/2000/svg';

function svg(tag, attrs) {
  const el = document.createElementNS(SVGNS, tag);
  for (const k in attrs) el.setAttribute(k, attrs[k]);
  return el;
}

/**
 * VisualArt.CircleRing as an ellipse drawn at its real size: w×h with a `stroke` px line all round. Review 17 (a look
 * call): Unity stretches one 32 px ring sprite, and the port did the same with an SVG stretched by
 * preserveAspectRatio='none', so the 型 crest's 280×72 ring was 26 px thick at its ends and 6 at its top, the 盾 ring a
 * heavy lozenge and the trait accent a target. `o.glow` (px) adds a soft light of `o.glowColor` (the line's colour by
 * default) around the line; the timing is untouched.
 */
function ringSvg(w, ht, stroke, color, o = {}) {
  const s = svg('svg', { width: w, height: ht, viewBox: '0 0 ' + w + ' ' + ht, class: 'ocbg-battle-fx-svg' });
  const rx = Math.max(0.5, w / 2 - stroke / 2);
  const ry = Math.max(0.5, ht / 2 - stroke / 2);
  s.appendChild(svg('ellipse', { cx: w / 2, cy: ht / 2, rx, ry, fill: 'none', stroke: color, 'stroke-width': stroke }));
  if (o.glow) s.style.filter = 'drop-shadow(0 0 ' + o.glow + 'px ' + (o.glowColor || color) + ')';
  return s;
}
/** The 打 ring's line as a share of its size (scene choice, review 17; Unity's sprite gave a tenth). */
const RIPPLE_LINE = 0.04;
/** Review 17: the lines of the rings that read as light, not as prototype sprites (px at tier 2, MARK_SCALE 1). */
const RING_LINE = { stand: 4, crestOuter: 3, crestInner: 2, accentOuter: 2.5, accentInner: 2 };

export function createFx(env) {
  const { clock, layer, OCB } = env;
  const live = new Set();
  /** The floats on screen, oldest first: where each stands and where it is going (FLOAT_STACK_GAP). */
  const floats = [];

  /** A centre-anchored element in `host` at stage point `at`, w×h. */
  function piece(host, cls, at, w, ht) {
    const el = h('div', { class: 'ocbg-battle-fx ' + cls });
    el.style.left = at.x - w / 2 + 'px';
    el.style.top = at.y - ht / 2 + 'px';
    el.style.width = w + 'px';
    el.style.height = ht + 'px';
    host.appendChild(el);
    live.add(el);
    return el;
  }
  function drop(el) {
    live.delete(el);
    el.remove();
  }
  /** DepictionFx.FadeAndDestroy: alpha → 0 over ms (ease in), then gone. Beside the flow. */
  function fadeAndDrop(el, ms, from = 1) {
    return clock.run(ms, 'in', (t) => { el.style.opacity = String(from * (1 - t)); }).then(() => drop(el));
  }
  /**
   * Review 17 (scene addition): `els` ride along with a figure for as long as they show — `trackX()` gives its x now
   * (null: leave them), `x0` its x when they were placed. Each frame they move by however far it has gone since.
   */
  function follow(els, trackX, x0) {
    if (typeof trackX !== 'function' || x0 == null) return;
    const placed = els.filter(Boolean).map((el) => ({ el, left: parseFloat(el.style.left) || 0 }));
    const off = clock.onFrame(() => {
      if (!placed.some((p) => live.has(p.el))) { off(); return; }
      const x = trackX();
      if (x == null) return;
      for (const p of placed) if (live.has(p.el)) p.el.style.left = (p.left + x - x0).toFixed(1) + 'px';
    });
  }

  // ---- blows (DepictionFx.Strike) ------------------------------------------------------------

  /**
   * The blow's horizontal reach (review 19): `near` px toward the striker and `far` px past the chest. The far side stops
   * BLOW_PAST beyond the struck body's edge (a shape running 250 px past the hero read as a prop, not a hit); with two or
   * three enemies both sides stop short of the other figures (field.js blowRoom: `room.left` / `room.right`).
   */
  function reachOf(towardLeft, natural, room, half) {
    const r = room || { left: Infinity, right: Infinity };
    const nearRoom = towardLeft ? r.right : r.left;
    const farRoom = towardLeft ? r.left : r.right;
    return { near: Math.max(BLOW_MIN, Math.min(nearRoom, natural)), far: Math.max(BLOW_MIN, Math.min(farRoom, (half || 50) + BLOW_PAST)) };
  }

  /**
   * 突 (§5.3 「一直線の閃きと点の衝撃」): one straight line of light arriving from the striker's side — a bright core in a soft
   * glow, tapered at its tail — drawn out to `far` past the chest over the first part of `ms` (linear), and a point of
   * impact flaring where it meets the body (the chest's edge toward the striker) as the line lands.
   */
  function thrust(at, colorName, towardLeft, reach, thickness, ms) {
    const dir = towardLeft ? -1 : 1;
    const x0 = at.x - dir * reach.near;
    const x1 = at.x + dir * reach.far;
    const len = Math.abs(x1 - x0);
    const el = piece(layer.fx, 'ocbg-battle-streak is-thrust', { x: (x0 + x1) / 2, y: at.y }, len, thickness * 3);
    const tail = towardLeft ? '90deg' : '270deg';
    el.style.background = 'linear-gradient(' + tail + ', ' + tok(colorName, 1) + ' 0%, ' + tok(colorName, 0.95) + ' 70%, transparent 100%) center / 100% ' + thickness + 'px no-repeat, '
      + 'linear-gradient(' + tail + ', ' + tok(colorName, 0.35) + ' 0%, ' + tok(colorName, 0.2) + ' 60%, transparent 100%) center / 100% ' + (thickness * 3) + 'px no-repeat';
    el.style.filter = 'drop-shadow(0 0 6px ' + tok(colorName, 0.8) + ')';
    el.style.transformOrigin = towardLeft ? '100% 50%' : '0 50%';
    el.style.transform = 'scaleX(0)';
    const lineMs = Math.max(1, ms * 0.6);
    const hit = { x: at.x - dir * IMPACT_IN, y: at.y };
    return clock.run(lineMs, 'linear', (t) => { el.style.transform = 'scaleX(' + t.toFixed(3) + ')'; }).then(() => {
      impact(hit, colorName, 70, Math.max(1, ms - lineMs) + 80);
      fadeAndDrop(el, 180);
    });
  }

  /** The point of impact: a small hard flash that grows and fades (beside the flow). */
  function impact(at, colorName, size, ms) {
    const el = piece(layer.fx, 'ocbg-battle-impact', at, size, size);
    el.style.background = 'radial-gradient(closest-side, ' + tok('ink', 1) + ' 0%, ' + tok(colorName, 0.9) + ' 35%, ' + tok(colorName, 0) + ' 100%)';
    el.style.transform = 'scale(0.4)';
    return clock.run(ms, 'out', (t) => {
      el.style.transform = 'scale(' + lerp(0.4, 1.2, t).toFixed(3) + ')';
      el.style.opacity = (1 - t * t).toFixed(3);
    }).then(() => drop(el));
  }

  /**
   * 斬 (§5.3 「弧を描く刃の光。相手の胸を横切る」; review 19: Unity's straight bar at −38° read as a placeholder): an arc of
   * blade light swept across the chest from high on the striker's side to low past it — a soft glow under a bright core,
   * revealed along the arc over `ms` (out), its ends thin. The arc's span keeps to the blow's reach.
   */
  let arcSeq = 0;
  function slash(at, colorName, towardLeft, reach, thickness, ms) {
    const dir = towardLeft ? -1 : 1;
    const R = Math.max(60, Math.min(reach.natural * 0.42, reach.near / 0.85, reach.far / 0.85));
    const w = R * 2 + thickness * 4;
    const ht = R * 1.6 + thickness * 4;
    const el = piece(layer.fx, 'ocbg-battle-arc', at, w, ht);
    const cx = w / 2;
    const cy = ht / 2;
    // From upper-near to lower-far, the chord through the chest, bowing a little up and away from the striker (the
    // swing's path): a radius of 1.8 × the half chord keeps the bow to about a sixth of it.
    const sx = cx - dir * R * 0.85;
    const sy = cy - R * 0.38;
    const ex = cx + dir * R * 0.85;
    const ey = cy + R * 0.62;
    const sweep = dir > 0 ? 1 : 0;
    const rr = Math.hypot(ex - sx, ey - sy) * 0.9;
    const d = 'M' + sx.toFixed(1) + ' ' + sy.toFixed(1) + ' A' + rr.toFixed(1) + ' ' + rr.toFixed(1) + ' 0 0 ' + sweep + ' ' + ex.toFixed(1) + ' ' + ey.toFixed(1);
    const id = 'ocbgArc' + (++arcSeq);
    const s2 = svg('svg', { width: w, height: ht, viewBox: '0 0 ' + w + ' ' + ht, class: 'ocbg-battle-fx-svg' });
    const defs = svg('defs', {});
    const grad = svg('linearGradient', { id, gradientUnits: 'userSpaceOnUse', x1: sx, y1: sy, x2: ex, y2: ey });
    grad.appendChild(svg('stop', { offset: '0', 'stop-color': tok(colorName, 0) }));
    grad.appendChild(svg('stop', { offset: '0.35', 'stop-color': tok(colorName, 0.95) }));
    grad.appendChild(svg('stop', { offset: '0.8', 'stop-color': tok(colorName, 1) }));
    grad.appendChild(svg('stop', { offset: '1', 'stop-color': tok(colorName, 0.2) }));
    defs.appendChild(grad);
    s2.appendChild(defs);
    const glow = svg('path', { d, fill: 'none', stroke: tok(colorName, 0.35), 'stroke-width': thickness * 2.6, 'stroke-linecap': 'round' });
    const core = svg('path', { d, fill: 'none', stroke: 'url(#' + id + ')', 'stroke-width': Math.max(3, thickness * 0.9), 'stroke-linecap': 'round' });
    const edge = svg('path', { d, fill: 'none', stroke: tok('ink', 0.9), 'stroke-width': Math.max(1.5, thickness * 0.3), 'stroke-linecap': 'round' });
    s2.append(glow, core, edge);
    s2.style.filter = 'drop-shadow(0 0 8px ' + tok(colorName, 0.7) + ')';
    s2.style.overflow = 'visible';
    el.appendChild(s2);
    const total = 1000;
    for (const p of [glow, core, edge]) { p.setAttribute('pathLength', String(total)); p.setAttribute('stroke-dasharray', total + ' ' + total); p.setAttribute('stroke-dashoffset', String(total)); }
    return clock.run(ms, 'out', (t) => {
      const off = (total * (1 - t)).toFixed(1);
      glow.setAttribute('stroke-dashoffset', off);
      core.setAttribute('stroke-dashoffset', off);
      edge.setAttribute('stroke-dashoffset', off);
    }).then(() => { fadeAndDrop(el, 200); });
  }

  /**
   * 払 (§5.3 「横に広い帯が薙ぐ」, 飾りの変数「粒子の量」; review 19: a flat 75 % violet plank with no core or light that ran
   * 250 px past the hero): a wide band of light swept across the chest from the striker's side — soft edges, a bright core
   * line, a lit leading edge — that stops a little past the body, throwing a few motes behind its front as it goes.
   */
  function sweep(at, colorName, coreName, towardLeft, reach, thickness, ms) {
    const dir = towardLeft ? -1 : 1;
    const x0 = at.x - dir * reach.near;
    const x1 = at.x + dir * reach.far;
    const len = Math.abs(x1 - x0);
    const tilt = towardLeft ? 6 : -6;
    const el = piece(layer.fx, 'ocbg-battle-band', { x: (x0 + x1) / 2, y: at.y }, len, thickness);
    el.style.background = 'linear-gradient(180deg, transparent 0%, ' + tok(colorName, 0.55) + ' 30%, ' + tok(coreName, 0.95) + ' 47%, ' + tok('ink', 0.95) + ' 50%, ' + tok(coreName, 0.95) + ' 53%, ' + tok(colorName, 0.55) + ' 70%, transparent 100%)';
    el.style.maskImage = el.style.webkitMaskImage = 'linear-gradient(' + (towardLeft ? '270deg' : '90deg') + ', transparent 0%, #000 22%, #000 100%)';
    el.style.filter = 'drop-shadow(0 0 8px ' + tok(colorName, 0.75) + ')';
    el.style.transformOrigin = towardLeft ? '100% 50%' : '0 50%';
    const front = piece(layer.fx, 'ocbg-battle-band-front', { x: x0, y: at.y }, 14, thickness * 1.6);
    front.style.background = 'radial-gradient(closest-side, ' + tok('ink', 1) + ', ' + tok(coreName, 0.8) + ' 45%, ' + tok(colorName, 0) + ')';
    const motes = [];
    const MOTES = 7;
    el.style.transform = 'rotate(' + tilt + 'deg) scaleX(0)';
    return clock.run(ms, 'out', (t) => {
      el.style.transform = 'rotate(' + tilt + 'deg) scaleX(' + t.toFixed(3) + ')';
      const fx0 = x0 + (x1 - x0) * t;
      front.style.left = (fx0 - 7).toFixed(1) + 'px';
      front.style.top = (at.y + Math.tan(tilt * Math.PI / 180) * (fx0 - (x0 + x1) / 2) * -1 - thickness * 0.8).toFixed(1) + 'px';
      while (motes.length < Math.floor(t * MOTES)) {
        const k = motes.length;
        const m = piece(layer.fx, 'ocbg-battle-mote', { x: fx0, y: at.y + ((k % 3) - 1) * thickness * 0.35 }, 5, 5);
        m.style.background = tok(k % 2 ? coreName : 'ink', 0.95);
        m.style.borderRadius = '50%';
        motes.push(m);
        const mx = fx0;
        const my = at.y + ((k % 3) - 1) * thickness * 0.35;
        const vx = -dir * (18 + 6 * (k % 3));
        const vy = -10 - 4 * (k % 2);
        clock.run(320, 'out', (q) => {
          m.style.left = (mx + vx * q - 2.5).toFixed(1) + 'px';
          m.style.top = (my + vy * q - 2.5).toFixed(1) + 'px';
          m.style.opacity = (1 - q).toFixed(3);
        }).then(() => drop(m));
      }
    }).then(() => {
      fadeAndDrop(front, 120);
      fadeAndDrop(el, 200);
    });
  }

  /**
   * Ripple (打). Unity grows a solid disc (ProceduralArt.Circle at 90%) 0.3 → 1.2 over the chest. Review fix (a look
   * call): drawn that way, or as a rim-lit shock disc, the struck figure turned milky inside a translucent sphere
   * with no point of contact. Here the blow lands where it meets the body — the chest's edge toward the striker,
   * IMPACT_IN px in — as a thin ring snapping open there (0.3 → 1, its inside clear) and five short spikes thrown
   * out the way the blow travels (±50°), so it reads as a weight arriving from one side. The sizes stay Unity's
   * (the ring is half the disc, the spikes a third of it).
   */
  const IMPACT_IN = 36;
  const SPIKES = [-50, -25, 0, 25, 50];
  function ripple(at, colorName, size, ms, towardLeft) {
    const dir = towardLeft ? -1 : 1;
    const hit = { x: at.x - dir * IMPACT_IN, y: at.y };
    const ringSize = size * 0.5;
    const ring = piece(layer.fx, 'ocbg-battle-ripple', hit, ringSize, ringSize);
    // §6b 「a thin ring」 (review 17: at Unity's sprite line it was 13 px, flat): an even line with a soft light.
    ring.appendChild(ringSvg(ringSize, ringSize, Math.max(3, ringSize * RIPPLE_LINE), tok(colorName, 0.9), { glow: 6 }));
    ring.style.transform = 'scale(0.3)';
    const len = size / 3;
    const spikes = SPIKES.map((deg) => {
      const a = (towardLeft ? 180 - deg : deg);
      const el = piece(layer.fx, 'ocbg-battle-spike', hit, len, 5);
      el.style.left = hit.x + 'px';
      el.style.top = hit.y - 2.5 + 'px';
      el.style.background = 'linear-gradient(90deg, ' + tok(colorName, 0.95) + ', transparent)';
      el.style.borderRadius = '3px';
      el.style.transformOrigin = '0 50%';
      el.style.transform = 'rotate(' + a + 'deg) translateX(' + (ringSize * 0.15).toFixed(1) + 'px) scaleX(0)';
      return { el, a };
    });
    return clock.run(ms, 'out', (t) => {
      ring.style.transform = 'scale(' + lerp(0.3, 1, t).toFixed(3) + ')';
      for (const sp of spikes) {
        sp.el.style.transform = 'rotate(' + sp.a + 'deg) translateX(' + (ringSize * (0.15 + 0.3 * t)).toFixed(1) + 'px) scaleX(' + t.toFixed(3) + ')';
      }
    }).then(() => {
      fadeAndDrop(ring, 180);
      spikes.forEach((sp) => fadeAndDrop(sp.el, 140));
    });
  }

  /**
   * DepictionFx.Strike: the blow's shape by system, all drawn in `ms`. 斬 an arc, 突 one straight line and its point of
   * impact, 払 a wide band, 打 a ripple where it lands. `towardLeft` when the blow travels right to left (the enemy's).
   * `room` / `half` (review 19): how far it may reach beside the struck figure (field.js blowRoom) and that body's half
   * width, so it reads as a hit on that figure alone.
   */
  function strike(at, { colorName, tier, towardLeft, system, ms, room, half }) {
    const length = SLASH_LENGTH[idx(tier)];
    const thickness = SLASH_THICKNESS[idx(tier)];
    switch (system) {
      case StrikeSystem.Thrust:
        return thrust(at, colorName, towardLeft, reachOf(towardLeft, length * 0.85, room, half), Math.max(4, thickness * 0.45), ms);
      case StrikeSystem.Sweep:
        return sweep(at, colorName, colorName === 'violet' ? 'phosphor' : colorName, towardLeft, reachOf(towardLeft, length * 0.7, room, half), thickness * 2.4, ms);
      case StrikeSystem.Strike:
        return ripple(at, colorName, length * 0.7, ms, towardLeft);
      default:
        return slash(at, colorName, towardLeft, Object.assign({ natural: length }, reachOf(towardLeft, length, room, half)), thickness, ms);
    }
  }

  // ---- the shapes of the other systems (DepictionFx.Mark, §5.3) ------------------------------

  /**
   * `p` = { chest, head, feet, facing (+1 faces right), fx (FxSystem), tier, ms, crestLight, warm }.
   * 盾 a ring standing in front of the figure, 型 a crest at its feet (with the body light when crestLight),
   * 気 a mark falling from above the head onto the chest. Blows and 息 draw nothing here.
   * `warm` = the unit is the player's: the crest is amber / wick there and violet / phosphor on the enemy
   * (README 色: warm colours belong to the player's side only; Unity draws amber for both).
   */
  function mark(p) {
    const s = MARK_SCALE[idx(p.tier)];
    let form = null;
    try { form = EffectGrammar.FormOf(p.fx); } catch (err) { form = null; }
    const x0 = typeof p.trackX === 'function' ? p.trackX() : null;
    switch (form) {
      case FxForm.Ring:
        return standingRing({ x: p.chest.x + 70 * p.facing, y: p.chest.y }, s, p.ms, p.trackX, x0);
      case FxForm.Crest:
        return crest(p.feet, s, p.ms, p.crestLight, p.warm, p.trackX, x0);
      case FxForm.Sigil:
        return sigil({ x: p.head.x, y: p.head.y - 60 }, p.chest, s, p.ms);
      default:
        return Promise.resolve();
    }
  }

  /** 盾: a ring seen edge-on, rising to full height in front of the figure (and riding with it). */
  function standingRing(at, s, ms, trackX, x0) {
    const el = piece(layer.fx, 'ocbg-battle-ring', at, 110 * s, 230 * s);
    el.appendChild(ringSvg(110 * s, 230 * s, Math.max(2, RING_LINE.stand * s), tok('guard', 0.95), { glow: 7 * s, glowColor: tok('guard', 0.75) }));
    follow([el], trackX, x0);
    burst(at, 'guard', 160 * s);
    el.style.transform = 'scale(0.6, 0)';
    return clock.run(ms, 'out', (t) => {
      el.style.transform = 'scale(' + lerp(0.6, 1, t).toFixed(3) + ',' + t.toFixed(3) + ')';
    }).then(() => { fadeAndDrop(el, 220); });
  }

  /**
   * 型: two flat rings open under the feet; the crest's light lingers 600 ms before it fades. Review 17: the rings are
   * even lines with a soft light, the ground inside the outer one lit faintly, and they ride with the figure's feet (an
   * enemy's action step took it 70 px off the crest it had stamped).
   */
  function crest(feet, s, ms, withLight, warm, trackX, x0) {
    const outerColor = warm ? tok('amber', 0.95) : tok('violet', 0.95);
    const innerColor = warm ? tok('wick', 0.85) : tok('phosphor', 0.85);
    const outer = piece(layer.fx, 'ocbg-battle-crest', feet, 280 * s, 72 * s);
    outer.style.borderRadius = '50%';
    outer.style.background = 'radial-gradient(closest-side, ' + tok(warm ? 'amber' : 'violet', 0.22) + ', ' + tok(warm ? 'amber' : 'violet', 0) + ')';
    outer.appendChild(ringSvg(280 * s, 72 * s, Math.max(1.5, RING_LINE.crestOuter * s), outerColor, { glow: 6 * s }));
    const inner = piece(layer.fx, 'ocbg-battle-crest', feet, 170 * s, 44 * s);
    inner.appendChild(ringSvg(170 * s, 44 * s, Math.max(1.25, RING_LINE.crestInner * s), innerColor, { glow: 4 * s }));
    let glow = null;
    if (withLight) {
      glow = piece(layer.fx, 'ocbg-battle-crest-glow', { x: feet.x, y: feet.y - 140 * s }, 200 * s, 320 * s);
      glow.style.background = glowGradient((a) => tok(warm ? 'amber' : 'violet', a), 0.35);
    }
    follow([outer, inner, glow], trackX, x0);
    outer.style.transform = 'scale(0.3)';
    inner.style.transform = 'scale(0.6)';
    return clock.run(ms, 'out', (t) => {
      outer.style.transform = 'scale(' + lerp(0.3, 1, t).toFixed(3) + ')';
      inner.style.transform = 'scale(' + lerp(0.6, 1, t).toFixed(3) + ')';
    }).then(() => {
      fadeAndDrop(outer, 600);
      fadeAndDrop(inner, 600);
      if (glow) fadeAndDrop(glow, 600);
    });
  }

  /**
   * 気: a diamond drops from above the head onto the chest and bursts there. Review fix (a look call; Unity's
   * DepictionFx.Sigil is a flat White sprite, which read as a debug marker): the diamond is lit — a phosphor-to-violet
   * gradient with a bright edge — carries the crest's soft Glow behind it, and leaves a short trail of fading copies
   * along its fall, so it reads as a curse landing.
   */
  const SIGIL_TRAIL = [0.07, 0.14, 0.21];
  function sigil(from, to, s, ms) {
    const size = 40 * s;
    const at = (t) => ({ x: lerp(from.x, to.x, t), y: lerp(from.y, to.y, t) });
    const ease = (t) => t * t * t; // the fall's 'in'
    const glow = piece(layer.fx, 'ocbg-battle-sigil-glow', from, size * 3.2, size * 3.2);
    glow.style.background = glowGradient((a) => tok('phosphor', a), 0.55);
    const trail = SIGIL_TRAIL.map((lag, i) => {
      const el = piece(layer.fx, 'ocbg-battle-sigil is-trail', from, size * (0.9 - 0.12 * i), size * (0.9 - 0.12 * i));
      el.style.background = tok('phosphor', 0.5 - 0.14 * i);
      el.style.transform = 'rotate(45deg)';
      el.style.opacity = '0';
      return { el, lag, w: size * (0.9 - 0.12 * i) };
    });
    const el = piece(layer.fx, 'ocbg-battle-sigil', from, size, size);
    el.style.background = 'linear-gradient(135deg, ' + tok('ink', 0.95) + ' 0%, ' + tok('phosphor', 0.98) + ' 38%, ' + tok('violet', 0.95) + ' 100%)';
    el.style.boxShadow = '0 0 ' + (10 * s).toFixed(1) + 'px ' + tok('phosphor', 0.85) + ', inset 0 0 0 2px ' + tok('ink', 0.55);
    el.style.transform = 'rotate(45deg)';
    const place = (node, p, w) => { node.style.left = p.x - w / 2 + 'px'; node.style.top = p.y - w / 2 + 'px'; };
    return clock.run(ms, 'linear', (raw) => {
      const p = at(ease(raw));
      place(el, p, size);
      place(glow, p, size * 3.2);
      for (const tr of trail) {
        const lt = Math.max(0, raw - tr.lag);
        place(tr.el, at(ease(lt)), tr.w);
        tr.el.style.opacity = raw > tr.lag ? '1' : '0';
      }
    }).then(() => {
      burst(to, 'phosphor', 140 * s);
      fadeAndDrop(el, 150);
      fadeAndDrop(glow, 220);
      trail.forEach((tr) => fadeAndDrop(tr.el, 120));
    });
  }

  /**
   * DepictionFx.TraitAccent (§5.9 J2): a short flare laid over the blow where it lands. `warm`: the trait is the player's
   * (wick and amber); an enemy's is drawn in phosphor and violet. Review 19 (with the 突 line through it, Unity's two
   * concentric rings read as a gun sight on the hero): a burst of light — a soft core and eight short rays thrown out
   * from the point of contact (the chest's edge toward the striker) — grows 0.5 → 1.35 and fades; no rings.
   */
  const ACCENT_RAYS = 8;
  function traitAccent(at, warm, ms, towardLeft) {
    const dir = towardLeft ? -1 : 1;
    const hit = { x: at.x - dir * IMPACT_IN, y: at.y };
    const coreColor = warm ? 'amber' : 'phosphor';
    const rayColor = warm ? 'wick' : 'violet';
    const core = piece(layer.fx, 'ocbg-battle-accent', hit, 96, 96);
    core.style.background = 'radial-gradient(closest-side, ' + tok('ink', 0.9) + ' 0%, ' + tok(coreColor, 0.75) + ' 30%, ' + tok(coreColor, 0) + ' 100%)';
    const rays = [];
    for (let k = 0; k < ACCENT_RAYS; k++) {
      const el = piece(layer.fx, 'ocbg-battle-accent-ray', hit, 46, 4);
      el.style.left = hit.x + 'px';
      el.style.top = hit.y - 2 + 'px';
      el.style.transformOrigin = '0 50%';
      el.style.borderRadius = '2px';
      el.style.background = 'linear-gradient(90deg, ' + tok(rayColor, 0) + ', ' + tok(rayColor, 0.95) + ' 45%, ' + tok('ink', 0.9) + ' 70%, ' + tok(rayColor, 0) + ')';
      rays.push({ el, deg: k * (360 / ACCENT_RAYS) + 22.5 });
    }
    return clock.run(ms, 'out', (t) => {
      const s2 = lerp(0.5, 1.35, t);
      core.style.transform = 'scale(' + s2.toFixed(3) + ')';
      for (const r of rays) r.el.style.transform = 'rotate(' + r.deg + 'deg) translateX(' + (10 + 26 * t).toFixed(1) + 'px) scaleX(' + s2.toFixed(3) + ')';
    }).then(() => {
      fadeAndDrop(core, 140);
      rays.forEach((r) => fadeAndDrop(r.el, 120));
    });
  }

  /** DepictionFx.Burst: a soft glow (ProceduralArt.Glow at 80%) that grows 0.4 → 1.5 and fades. Fire and forget. */
  function burst(at, colorName, size) {
    const el = piece(layer.fx, 'ocbg-battle-burst', at, size, size);
    el.style.background = glowGradient((a) => tok(colorName, a), 0.8);
    el.style.transform = 'scale(0.4)';
    return clock.run(BURST_MS, 'out', (t) => {
      el.style.transform = 'scale(' + lerp(0.4, 1.5, t).toFixed(3) + ')';
      el.style.opacity = String(1 - t);
    }).then(() => drop(el));
  }

  // ---- floating text (DepictionFx.FloatText, dressed as the DS FloatNum) ------------------------

  /**
   * What a float says and how it is dressed. A number alone, or a cause and a number ("出血 -3",
   * "Guard −2"), wears an icon by its role; anything else is a word on the tag. `plain` keeps the text
   * whole as a word, with no icon: a fired trait's line ("間合い2以上 +3") is a cue, not an HP or Guard
   * number (DepictionPlayer.FireTrait writes it as plain text).
   */
  function dress(text, role, fontSize, struck, plain, side) {
    const m = plain ? null : /^(.*\S)\s+([+\-−]?\d+)$/.exec(text);
    const numberOnly = !plain && /^[+\-−]?\d+$/.test(text);
    const isNumber = numberOnly || !!m;
    const main = numberOnly ? text : m ? m[2] : text;
    const label = !numberOnly && m ? m[1] : '';
    let kind = 'word';
    let ink = 'ink';
    let iconName = null;
    if (struck || role === 'whiff') {
      kind = 'miss';
    } else if (isNumber && (role === 'ink' || role === 'omen' || role === 'white')) {
      kind = 'hp'; ink = 'hp-text'; iconName = 'heart';
    } else if (isNumber && role === 'heal') {
      kind = 'hp'; ink = 'hp-text'; iconName = 'heal';
    } else if (role === 'guard') {
      kind = 'guard'; ink = 'guard'; iconName = isNumber ? 'shield' : null;
    } else if (role === 'warm') {
      ink = 'amber';
    } else if (role === 'ink2') {
      ink = 'ink-muted';
    } else if (role === 'omen') {
      ink = 'phosphor';
    }
    const el = h('div', { class: 'ocb-float ocbg-battle-float is-' + kind });
    if (iconName) {
      const k = Math.round(fontSize * 0.62);
      const opts = iconName === 'shield' ? { fill: 'var(--guard)', fillOpacity: 0.2 } : undefined;
      el.appendChild(OCB.icon(iconName, k, tok(ink), opts));
    }
    const b = h('b', null, main);
    b.style.fontSize = fontSize + 'px';
    b.style.lineHeight = Math.round(fontSize * 1.1) + 'px';
    b.style.color = tok(ink);
    if (!isNumber && !struck) b.classList.add('is-word');
    // The text's own order (review fix: "Guard −3" read "−3 Guard"): a cause written before its number stays
    // before it — "Guard −3", "出血 −3" (DepictionPlayer writes them so; battle_ui_ux_v2 §5.0) — as a small
    // word between the icon and the number.
    if (label) el.appendChild(h('small', { class: 'ocbg-battle-float-cause' }, label));
    el.appendChild(b);
    // §5.0 「主数字 + 脇ラベル」 (review 19): a side label after the number in 16 px body ink — a fired trait's 「+5 間合い」,
    // the wall's 「壁」.
    if (side) el.appendChild(h('small', { class: 'ocbg-battle-float-side' }, side));
    return el;
  }

  /**
   * DepictionFx.FloatText: the text rises `rise` px over FLOAT_MS (600, ease out; §5.0), pops 1.35 → 1 in its first
   * 18% and fades over its last 40%. Fire and forget; returns its Promise.
   */
  function floatText(at, text, role, fontSize, o = {}) {
    const el = dress(String(text), role, fontSize, !!o.struck, !!o.plain, o.side || '');
    // o.anchor (scene addition): 'left' / 'right' puts that edge of the tag on at.x (it grows away from it),
    // so a tag can stand beside something without reaching over it; the middle otherwise.
    const ax = o.anchor === 'left' ? '0%' : o.anchor === 'right' ? '-100%' : '-50%';
    if (o.anchor === 'left' || o.anchor === 'right') el.style.transformOrigin = (o.anchor === 'left' ? '0' : '100%') + ' 50%';
    el.style.left = at.x + 'px';
    el.style.top = at.y + 'px';
    el.style.transform = 'translate(' + ax + ', -50%) scale(1.35)';
    layer.float.appendChild(el);
    live.add(el);
    const pl = placeFloat(el, at, fontSize, o);
    const rise = pl.rise;
    el.style.left = pl.x + 'px';
    el.style.top = pl.y0 + 'px';
    // o.onPlaced (scene addition): where it stands, settled — { x0, x1 } its span, y its start, yNow() its middle now
    // (the HP number lines up beside a "Guard −n" tag and rides with it).
    if (typeof o.onPlaced === 'function') { try { o.onPlaced({ x0: pl.sx0, x1: pl.sx1, y: pl.y0, rise: pl.rise, yNow: () => pl.y0 - rise * me.t }); } catch (err) { /* geometry only */ } }
    // o.followY (scene addition, review 16): a function giving the middle y of the float it stands beside; it rides at
    // that height (its own rise is then 0), so a pair that started apart in time still reads side by side.
    const followY = typeof o.followY === 'function' ? o.followY : null;
    const me = {
      x0: pl.sx0, x1: pl.sx1, t: 0, pop: POP, alpha: 1,
      /** The box it covers from now to its end. */
      swept() {
        const hNow = pl.halfS * me.pop;
        if (followY) {
          // It rides with the float beside it: from where it is now up to that float's end (its rise at most).
          const c = followY();
          return { top: c - hNow - (o.followRise || 0) * (1 - me.t), bottom: c + hNow };
        }
        const cNow = pl.y0 - rise * me.t;
        const cEnd = pl.y0 - rise;
        return { top: Math.min(cNow - hNow, cEnd - pl.halfS), bottom: Math.max(cNow + hNow, cEnd + pl.halfS) };
      },
    };
    floats.push(me);
    const forget = () => { const i = floats.indexOf(me); if (i >= 0) floats.splice(i, 1); };
    // o.track (scene addition, review fix): a function giving the x of the figure the float is about; the float rides
    // along by however far that figure has moved since it rose (a shove carries its number and 「押し出し」 with it).
    const track = typeof o.track === 'function' ? o.track : null;
    const tx0 = track ? track() : null;
    let dxNow = 0;
    // Paced (review 16): fast-forward speeds the floats up less than the motion, so its numbers stay readable.
    const run = clock.runPaced ? clock.runPaced.bind(clock) : clock.run.bind(clock);
    return run(FLOAT_MS, 'out', (t) => {
      const pop = t < 0.18 ? lerp(POP, 1, t / 0.18) : 1;
      const alpha = t < 0.6 ? 1 : 1 - (t - 0.6) / 0.4;
      me.t = t;
      me.pop = pop;
      me.alpha = alpha;
      if (track && tx0 != null) {
        const x = track();
        const dx = x == null ? dxNow : x - tx0;
        if (dx !== dxNow) {
          me.x0 += dx - dxNow;
          me.x1 += dx - dxNow;
          dxNow = dx;
          el.style.left = (pl.x + dx).toFixed(1) + 'px';
        }
      }
      const up = followY ? pl.y0 - followY() : rise * t;
      el.style.transform = 'translate(' + ax + ', calc(-50% - ' + up.toFixed(1) + 'px)) scale(' + pop.toFixed(3) + ')';
      el.style.opacity = String(alpha);
    }).then(() => { forget(); drop(el); });
  }

  /**
   * Where a float starts and how far it rises (scene additions over DepictionFx's anchor and rise):
   * - o.avoid: boxes ({ x, y, w, h }, the omen badges) it must stay under. Of those over its path (its width at
   *   the first pop), the lowest bottom + o.avoidGap is its ceiling: it starts low enough that its first pop
   *   stays under it, and rises only as far as the room allows.
   * - the floats still on screen (FLOAT_STACK_GAP): it stands under every one whose path its own would cross.
   * - o.lowest: the stage y its path must not reach under (the floor line). When standing under the others
   *   would take it there, it stands beside them instead, at its own height — on the side of the figure's back
   *   (away from the stage's middle) when there is room, else the other — and only when neither side has room
   *   does it go on down.
   * Returns { x, y0, rise, sx0, sx1 (its settled width's span), halfS }. Layout sizes: the transform does not count.
   */
  function placeFloat(el, at, fontSize, o) {
    const rise0 = o.rise != null ? o.rise : DEFAULT_RISE;
    const wS = el.offsetWidth || fontSize;
    const halfS = (el.offsetHeight || fontSize * 1.2) / 2;
    const halfP = halfS * POP;
    const spanAt = (x, w) => (o.anchor === 'left' ? [x, x + w] : o.anchor === 'right' ? [x - w, x] : [x - w / 2, x + w / 2]);
    const badges = rise0 > 0 && typeof o.avoid === 'function' ? o.avoid() || [] : [];
    const lowest = typeof o.lowest === 'function' ? o.lowest() : o.lowest;
    const pathOf = (pl) => ({
      top: Math.min(pl.y0 - halfP, pl.y0 - Math.max(pl.rise, 0) - halfS),
      bottom: Math.max(pl.y0 + halfP, pl.y0 - Math.min(pl.rise, 0) + halfS),
    });
    const under = (pl, ceiling) => {
      const start = Math.max(pl.y0, ceiling + halfP);
      if (pl.rise > 0) pl.rise = Math.max(0, start - Math.max(start - pl.rise, ceiling + halfS));
      pl.y0 = start;
    };
    /**
     * The floats on screen whose swept box its path would cross: side by side by its settled width (one under
     * another: the first pop's few px of overreach at an edge is let be), or (wide) by its width at the first
     * pop (one beside another: the pop would reach over the neighbour).
     */
    const inTheWay = (pl, wide) => {
      const path = pathOf(pl);
      const x0 = wide ? pl.px0 : pl.sx0;
      const x1 = wide ? pl.px1 : pl.sx1;
      const hits = [];
      for (const f of floats) {
        if (f.alpha <= FLOAT_STACK_MIN_ALPHA) continue;
        if (f.x1 + FLOAT_STACK_MARGIN_X <= x0 || f.x0 >= x1 + FLOAT_STACK_MARGIN_X) continue;
        const b = f.swept();
        if (b.bottom + FLOAT_STACK_GAP <= path.top || b.top >= path.bottom + FLOAT_STACK_GAP) continue;
        hits.push({ f, b });
      }
      return hits;
    };
    /** At x: under the badges, and (stack) under every float in its way. */
    const placeAt = (x, stack) => {
      const [px0, px1] = spanAt(x, wS * POP);
      const [sx0, sx1] = spanAt(x, wS);
      const pl = { x, y0: at.y, rise: rise0, sx0, sx1, px0, px1, halfS };
      let ceiling = -Infinity;
      for (const b of badges) if (b && px1 > b.x && px0 < b.x + b.w) ceiling = Math.max(ceiling, b.y + b.h + (o.avoidGap || 0));
      if (ceiling > -Infinity) under(pl, ceiling);
      for (let pass = 0; stack && pass < 8; pass++) {
        const hits = inTheWay(pl);
        if (!hits.length) break;
        under(pl, Math.max(...hits.map((k) => k.b.bottom)) + FLOAT_STACK_GAP);
      }
      return pl;
    };
    const fits = (pl) => lowest == null || pathOf(pl).bottom <= lowest;
    /** Beside the floats in its way, toward dir (±1), at its own height; null when the stage has no room. */
    const besideAt = (dir) => {
      let x = at.x;
      for (let pass = 0; pass < 6; pass++) {
        const pl = placeAt(x, false);
        const hits = inTheWay(pl, true);
        if (!hits.length) return pl.px0 >= FLOAT_STAGE_EDGE && pl.px1 <= FLOAT_STAGE_W - FLOAT_STAGE_EDGE && fits(pl) ? pl : null;
        x += dir > 0
          ? Math.max(...hits.map((k) => k.f.x1)) + FLOAT_STACK_GAP - pl.px0
          : Math.min(...hits.map((k) => k.f.x0)) - FLOAT_STACK_GAP - pl.px1;
      }
      return null;
    };
    const stacked = placeAt(at.x, true);
    if (fits(stacked)) return stacked;
    const back = at.x >= FLOAT_STAGE_W / 2 ? 1 : -1;
    return besideAt(back) || besideAt(-back) || stacked;
  }

  // ---- the shield (DepictionFx.ShieldBlock) -----------------------------------------------------

  /**
   * DepictionFx.ShieldBlock: a shield snaps up in front of the target as Guard takes the blow — pop 240 (1.2 → 0.8 → 1),
   * 80 more, then it fades over 220 beside the flow. Review 17 (a look call; Unity's prototype draws a flat 150×170 slab
   * at 92 % with the number on it, and it hid the struck figure — on a blow Guard stopped whole the hero could not be
   * seen at all): a smaller shield of light — a Guard-coloured rim with a soft glow over a faint see-through face, so the
   * figure shows through it — with no number of its own (the "Guard −n" tag beside it says how much). `o.broken`: the
   * blow took the Guard to 0 — a crack runs down it and its halves part as it fades (battle_ui_ux_v2 §5.6 順 1 割れる).
   */
  let shieldSeq = 0;
  function shieldArt(id) {
    const s = svg('svg', { width: SHIELD.w, height: SHIELD.h, viewBox: '0 0 64 72', class: 'ocbg-battle-fx-svg' });
    const defs = svg('defs', {});
    const grad = svg('linearGradient', { id, x1: '0', y1: '0', x2: '0', y2: '1' });
    grad.appendChild(svg('stop', { offset: '0', 'stop-color': tok('guard', 0.34) }));
    grad.appendChild(svg('stop', { offset: '1', 'stop-color': tok('guard', 0.06) }));
    defs.appendChild(grad);
    s.appendChild(defs);
    s.appendChild(svg('path', { d: SHIELD_PATH, fill: 'url(#' + id + ')', stroke: tok('guard', 0.95), 'stroke-width': 2.5, 'vector-effect': 'non-scaling-stroke', 'stroke-linejoin': 'round' }));
    // an inner keyline, as light caught on the rim
    s.appendChild(svg('path', { d: SHIELD_PATH, fill: 'none', stroke: tok('ink', 0.35), 'stroke-width': 1, 'vector-effect': 'non-scaling-stroke', transform: 'translate(32 36) scale(0.84) translate(-32 -36)' }));
    s.style.filter = 'drop-shadow(0 0 8px ' + tok('guard', 0.7) + ')';
    return s;
  }
  function shieldBlock(at, amountText, ms, o = {}) {
    if (!(ms > 0)) return Promise.resolve();
    const el = piece(layer.fx, 'ocbg-battle-shield', at, SHIELD.w, SHIELD.h);
    const broken = !!o.broken;
    // Two halves (one piece unless it breaks): each clipped to its side, so a break can part them.
    const halves = (broken ? ['inset(0 50% 0 0)', 'inset(0 0 0 50%)'] : ['']).map((clip) => {
      const half = h('div', { class: 'ocbg-battle-shield-half' });
      half.style.clipPath = clip;
      half.appendChild(shieldArt('ocbgShield' + (++shieldSeq)));
      el.appendChild(half);
      return half;
    });
    let crack = null;
    if (broken) {
      crack = svg('svg', { width: SHIELD.w, height: SHIELD.h, viewBox: '0 0 64 72', class: 'ocbg-battle-fx-svg' });
      crack.appendChild(svg('path', { d: SHIELD_CRACK, fill: 'none', stroke: tok('ink', 0.9), 'stroke-width': 2, 'vector-effect': 'non-scaling-stroke', 'stroke-linejoin': 'round' }));
      crack.style.opacity = '0';
      el.appendChild(crack);
    }
    burst(at, 'guard', 220);
    const pop = Math.min(240, ms);
    const rest = Math.max(0, ms - pop);
    return clock.run(pop, 'out', (t) => {
      const k = t < 0.5 ? lerp(1.2, 0.8, t * 2) : lerp(0.8, 1, (t - 0.5) * 2);
      el.style.transform = 'scale(' + k.toFixed(3) + ')';
      if (crack) crack.style.opacity = t < 0.5 ? '0' : ((t - 0.5) * 2).toFixed(3);
    }).then(() => clock.wait(rest)).then(() => {
      if (!broken) { fadeAndDrop(el, 220); return; }
      // The halves part: each turns and drifts out from the crack and down as the whole fades.
      clock.run(220, 'in', (t) => {
        halves.forEach((half, i) => {
          const d = i === 0 ? -1 : 1;
          half.style.transform = 'translate(' + (d * SHIELD_PART.dx * t).toFixed(1) + 'px,' + (SHIELD_PART.dy * t).toFixed(1) + 'px) rotate(' + (d * SHIELD_PART.deg * t).toFixed(1) + 'deg)';
        });
        el.style.opacity = String(1 - t);
      }).then(() => drop(el));
    });
  }

  // ---- cards in flight (FlyGhostTo / SlideDiscardOntoDraw) ---------------------------------------

  /**
   * kind 'discard' | 'exile': the played card's ghost, 76×104 ink at 55%, fading as it flies (FlyGhostTo).
   * kind 'sheet': a card back of the piles' 64×84 crossing from the discard pile to the draw pile (§3.14).
   */
  function ghost(from, to, o = {}) {
    if (!from || !to) return Promise.resolve();
    const sheet = o.kind === 'sheet';
    const w = sheet ? PileLayout.Width : 76;
    const ht = sheet ? PileLayout.Height : 104;
    // o.card (review fix: the plain 55 % sheet read as a missing texture): the played card's own face (the design
    // system's card, its attribute band and frame in its colour), shrunk to the ghost's 76 × 104, fading out late so
    // it reads as the card going to its pile.
    const card = !sheet && o.card ? o.card : null;
    const el = piece(layer.float, sheet ? 'ocbg-battle-sheet' : card ? 'ocbg-battle-ghost is-card' : 'ocbg-battle-ghost', from, w, ht);
    if (card) {
      const k = w / GHOST_CARD_W;
      card.style.transform = 'scale(' + k.toFixed(4) + ')';
      card.style.transformOrigin = '0 0';
      el.appendChild(card);
    }
    return clock.run(o.ms || 0, o.ease || (sheet ? 'out' : 'inout'), (t) => {
      el.style.left = lerp(from.x, to.x, t) - w / 2 + 'px';
      el.style.top = lerp(from.y, to.y, t) - ht / 2 + 'px';
      if (card) el.style.opacity = (GHOST_CARD_ALPHA * (1 - t * t)).toFixed(3);
      else if (!sheet) el.style.opacity = String(1 - t);
    }).then(() => drop(el));
  }

  return {
    strike, mark, traitAccent, burst, floatText, shieldBlock, ghost,
    clear() {
      live.forEach((el) => el.remove());
      live.clear();
      floats.length = 0;
    },
  };
}

export { shapeToken };
