// The hand module of the battle scene (docs/BATTLE_SCENE.md §6a): the cards in hand, the fan, the hover
// and its panels, hold and release, the drop zones, the target hooks, the throw line, the prediction tag,
// the refusal line, and the keyboard / click path. A port of the hand parts of
// unity-port/unity-project-kit/Assets/View/Depiction/DepictionPlayer.cs (LayoutHand, HomeOf, HoverPlace,
// UpdateHover, UpdateHoverPanels, CardUnderPointer, BeginHold, ShowTargetMark, SetZoneHot, ZoneAt,
// ReceiverTakes, UpdateHot, DragPosition, UpdateSnap, LetGo, Release, FlyDrawn, FlyDiscarded, FlyCard,
// SettleHand, ReturnToHand, RefreshPlayableLook, Refusal, AutoDrag) and its views (CardView, CardPanels,
// ReceiverView, ThrowLineView, TargetMarkView, RefusalLineView), drawn with the design system.
//
// It computes no rule: whether a card plays is the source's (the player's onRelease → TryPlay), what the
// cards say is the CardFace's, and where they stand is the ported HandSpec / HandFan / ReceiverDrop /
// RefusalLineLayout. Every length runs on env.clock (battle speed, 演出短縮, fast-forward); input timings
// (the drag threshold, the long press, the refusal line's 3.5 s) on env.real.
//
// Feel: Unity's numbers, and the design system's prototype (build/src/50-game.js beginHold / updateHold /
// zoneTest / accept / refuse / backToHand, FEEL_DEFS) where Unity does not say otherwise.
//
// Several enemies (scene addition, docs/port-notes/depiction-multi-enemy.md): a single-target card has one
// receiver per standing enemy (env.geom.receivers); the enemy whose receiver takes the card is the one it is
// framed over and released on (opts.onRelease(cardId, zone, unit), opts.preview(cardId, unit),
// CardFace.LitFor(unit)); with no enemy named the source sends it to CardFace.TargetUnit.
import './hand.css';
import { h, EASE } from '../common/stage.js';
import { UnitSide, CardAim, CardKind, DropZone, DepictionText } from '../../bridge/depictionScript/DepictionTypes.js';
import { HandFan, HandSpec } from '../../bridge/depictionScript/HandFan.js';
import { ReceiverDrop } from '../../bridge/depictionScript/ReceiverDrop.js';
import { ScreenBox } from '../../bridge/depictionScript/CornerLayout.js';
import { EffectPlan } from '../../bridge/depictionScript/Effects.js';
import { buildCard, faceKey, modsOf, CARD_W, CARD_H } from './hand-card.js';
import { createMarks } from './hand-marks.js';
import { createPanels } from './hand-panels.js';

const W2 = CARD_W / 2;
const H2 = CARD_H / 2;
/** DepictionPlayer inspector: cardSpacing (five or fewer) and fanDegreesPerCard (battle-visual-v1 §4.8). */
const CARD_SPACING = 160;
const FAN_DEGREES = 2.5;
/** The release sound by the card's attribute (sound.md 「動作」). */
const RELEASE_SOUND = { [CardKind.Attack]: 'releaseAttack', [CardKind.Guard]: 'releaseDefense', [CardKind.Stance]: 'releaseStance' };
/** 50-game.js: a drawn card comes out of the 64×84 pile small and grows (0.3 → 1); a discarded one shrinks into it. */
const PILE_SCALE = 0.3;
/** §5.11 順 1: a stance card shrinks into the stance list's slot to its ghost's width (76 of 216, field/fx.js ghost). */
const STANCE_CARD_SCALE = 76 / 216;
/** §5.9 順 1: the lamp swells to 1.4 and back over this (nominal ms, out). */
const LAMP_SWELL = { scale: 1.4, ms: 150 };
/** sound.md: the hover sound is not repeated within 50 ms. */
const HOVER_SOUND_GAP_MS = 50;
const FAR = { x: -99999, y: -99999 };
/** battle_ui_ux_v2 §3.2 / §3.13: a long-pressed card goes back to its rest place over 200 ms before the detail opens. */
const LONG_PRESS_RETURN_MS = 200;
/** The battle is over (review fix): the hand fades to this under the result card's scrim. */
const END_HAND_ALPHA = 0.35;

function dimAlpha() {
  try {
    const v = parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--opacity-unpayable'));
    return Number.isFinite(v) ? v : 0.6;
  } catch (err) { return 0.6; }
}

function lerp(a, b, t) { return a + (b - a) * t; }

/** Mathf.MoveTowards: steps `cur` toward `target` by at most `step` and stops on it (no overshoot, no swing). */
function moveTowards(cur, target, step) {
  if (Math.abs(target - cur) <= step) return target;
  return cur + Math.sign(target - cur) * step;
}

/** The axis-aligned box a w×h card centred at (x, y) and turned `rot`° covers (DepictionPlayer.BoxOn: a tilted card counts its whole outline). */
function aabb(x, y, rot) {
  const a = rot * Math.PI / 180;
  const c = Math.abs(Math.cos(a));
  const s = Math.abs(Math.sin(a));
  const hw = W2 * c + H2 * s;
  const hh = W2 * s + H2 * c;
  return { x: x - hw, y: y - hh, w: 2 * hw, h: 2 * hh };
}

export function createHand(env, opts) {
  const { OCB, layer, clock, real, L } = env;
  opts = opts || {};
  const DIM = dimAlpha();

  let cards = [];               // in the fan, left to right
  const leaving = new Map();    // released cards on their way out, by id
  const returning = new Set();  // cards sliding home (ReturnToHand / a cancel): the hand is busy meanwhile (Unity's _busy)
  let interactable = false;
  let hovered = null;
  let held = null;              // a drag: { card, offX, offY, hot, snapW, auto, zone }
  let sel = null;               // the click / key path's selection: { card, hot }
  let down = null;              // a press on a card that is not yet a drag: { card, x, y, token, pointerId }
  let bgDown = null;            // a press anywhere else in the scene: { x, y }
  let ptr = { x: FAR.x, y: FAR.y };
  let ptrClient = null;
  let ptrForeign = false;       // the pointer is over another module's panel or an overlay
  let dimOf = () => false;
  let lastHoverSound = -1e9;
  let pressToken = 0;
  let destroyed = false;
  /** An overlay (the journal, the settings) is up: the hand takes no press, hover or grab (review fix). */
  let blocked = false;
  /** The overlay that blocks is the journal: the cards can still be hovered (battle_ui_ux_v2 §2.4). */
  let hoverWhileBlocked = false;
  /** While blocked by the pile list: what a press on a card (or its digit) calls — the reason, on the refusal line. */
  let blockedTry = null;
  /**
   * battle_ui_ux_v2 §3.8: while 俊敏's choice waits, no card is taken (a press, a digit); the cards still lift on
   * hover so the hand can be read. A function: what to do when a card is tried meanwhile (the player's refusal).
   */
  let grabLock = null;

  const marks = createMarks(env);
  const panels = createPanels(env);

  // ---- places (HandSpec / HandFan, battle-visual-v1 §4.8) ----------------------------------------------
  const spacingOf = (n) => HandFan.Spacing(n, CARD_SPACING);
  /** HomeOf: the resting centre and tilt (CSS degrees, clockwise) of card i of n. */
  function rest(i, n) { const p = HandSpec.Rest(n, i, spacingOf(n), FAN_DEGREES); return { x: p.X, y: p.Y, rot: -p.Degrees }; }
  /** HoverPlace: upright, raised until its top is at y 760. */
  function hoverPlace(i, n) { const p = HandSpec.Hover(n, i, spacingOf(n)); return { x: p.X, y: p.Y, rot: 0 }; }

  // ---- cards ------------------------------------------------------------------------------------------
  function make(face) {
    const wrap = h('div', { class: 'ocbg-battle-card ocbg-battle-hit', 'data-card': face.Id });
    // A card made mid-flow (the turn start's draw) comes in under the sheet the hand wears now (refreshPlayable's
    // dimOf: the whole hand is dark while the flow runs), with no fade.
    const dark = !!dimOf(face.Id);
    const c = { id: face.Id, face, key: '', wrap, view: null, mode: 'fan', pose: { x: 960, y: 1300, rot: 0, s: 1, a: 1 }, hw: 0, shake: 0, dim: dark, dimA: dark ? DIM : 0, dimToken: 0, litFor: null, costFor: null, motion: 0 };
    draw(c);
    wrap.addEventListener('contextmenu', (e) => e.preventDefault());
    layer.hand.appendChild(wrap);
    return c;
  }
  /** CardView.Bind: a new face only when something it prints changed. The lamp and the dark sheet carry over. */
  function draw(c) {
    const key = faceKey(c.face);
    if (key === c.key && c.view) { applyLamp(c); applyMods(c); return; }
    c.key = key;
    c.view = buildCard(OCB, c.face);
    c.wrap.innerHTML = '';
    c.wrap.appendChild(c.view.el);
    c.view.setDim(c.dimA);
    applyLamp(c);
    applyMods(c);
  }
  /** §3.5 / §5.10 (review 21): the player's 集中 / 強化 / 威圧 as marks beside the values of the cards they act on. */
  let statusMarks = null;
  function applyMods(c) {
    if (c.view && c.view.setMods) c.view.setMods(modsOf(c.face, statusMarks));
  }
  /** §3.13 (review 21): what the detail says the card does now — its lamp, the source's prediction, the marks on it. */
  function detailExtra(c) {
    let preview = '';
    try { preview = opts.preview ? opts.preview(c.id) || '' : ''; } catch (err) { preview = ''; }
    return { lit: !!(c.view && c.view.lit), preview, mods: modsOf(c.face, statusMarks) };
  }
  /** ShowLamp: the face's lamp, or the framed enemy's verdict (CardFace.LitFor(unit)) while a single-target card is up. */
  function applyLamp(c) {
    if (!c.view) return;
    c.view.setLit(c.litFor != null ? c.litFor : !!c.face.TraitLit);
    if (c.view.setCost) {
      const u = c.costFor;
      if (u != null) c.view.setCost(c.face.CostFor(u), c.face.CostDropFor(u));
      else c.view.setCost(c.face.Cost, c.face.CostDrop || 0);
    }
  }
  /**
   * While a single-target card is up (held or selected) its lamp and its cost circle are read on the enemy it frames
   * (CardFace.LitFor(k), CostFor(k) / CostDropFor(k): port notes 「Holding a card」, review fix — the circle kept the
   * face's own cost over a dearer enemy); off, the face's own.
   */
  function setLitFor(c, on, unit) {
    const u = unit != null && unit >= 0 ? unit : defaultUnit(c.face);
    const framed = on && c.face.Aim === CardAim.Single;
    c.litFor = framed && typeof c.face.LitFor === 'function' ? !!c.face.LitFor(u) : null;
    c.costFor = framed && typeof c.face.CostFor === 'function' ? u : null;
    applyLamp(c);
  }
  /** The enemy a single-target card goes to when no receiver names one (CardFace.TargetUnit; 0 with one enemy). */
  function defaultUnit(face) {
    return face && face.TargetUnit >= 0 ? face.TargetUnit : 0;
  }
  /**
   * The enemy a lifted card is framed on before the pointer or Tab names one (review fix, battle_ui_ux_v2 §1.4
   * 「選んでいる相手との N（静止時はいちばん近い敵。Tab か対象の枠で移る）」): the enemy a Tab at rest chose
   * (opts.restingUnit, the floor's N), when it still stands; else the card's own target (CardFace.TargetUnit).
   */
  function baseUnit(face) {
    if (!face || face.Aim !== CardAim.Single) return defaultUnit(face);
    const u = opts.restingUnit ? opts.restingUnit() : -1;
    if (u != null && u >= 0 && receivers().some((r) => r.unit === u)) return u;
    return defaultUnit(face);
  }
  /** The enemy a held / selected card is framed on now: the one the pointer or Tab named, else its base. */
  function framedOf(st) {
    return st.unit >= 0 ? st.unit : st.base != null ? st.base : defaultUnit(st.card.face);
  }

  /** Where a card in the fan stands now: its rest, eased toward its hover place by the hover weight (smoothstep, Unity). */
  function fanPose(c, i, n) {
    const r = rest(i, n);
    const hv = hoverPlace(i, n);
    const w = EASE.smooth(Math.max(0, Math.min(1, c.hw)));
    return { x: lerp(r.x, hv.x, w), y: lerp(r.y, hv.y, w), rot: r.rot * (1 - w), s: 1, a: 1 };
  }

  function render() {
    const n = cards.length;
    cards.forEach((c, i) => {
      if (c.mode === 'fan') Object.assign(c.pose, fanPose(c, i, n));
    });
    const paint = (c, z) => {
      const p = c.pose;
      c.wrap.style.transform = 'translate(' + (p.x - W2 + c.shake).toFixed(2) + 'px,' + (p.y - H2).toFixed(2) + 'px) rotate(' + p.rot.toFixed(3) + 'deg) scale(' + p.s.toFixed(4) + ')';
      c.wrap.style.opacity = Math.max(0, Math.min(1, p.a)).toFixed(3);
      c.wrap.style.zIndex = String(z);
    };
    cards.forEach((c, i) => {
      // Cards draw left to right; the hovered one in front, and one still settling from a hover stays in front of its neighbours (50-game.js).
      let z = 10 + i;
      if (sel && sel.card === c) z = 300;
      else if (c === hovered) z = 250;
      else if (c.mode === 'fan' && c.hw > 0.01) z = 200 + Math.round(c.hw * 10);
      if (returning.has(c)) z = 400;
      paint(c, z);
    });
    leaving.forEach((c) => paint(c, 450));
  }

  // ---- hover (UpdateHover / CardUnderPointer) ------------------------------------------------------------
  function inRect(p, place) {
    const a = -place.rot * Math.PI / 180;
    const dx = p.x - place.x;
    const dy = p.y - place.y;
    const lx = dx * Math.cos(a) - dy * Math.sin(a);
    const ly = dx * Math.sin(a) + dy * Math.cos(a);
    return Math.abs(lx) <= W2 && Math.abs(ly) <= H2;
  }
  /**
   * CardUnderPointer: the hovered card is drawn in front, so it wins where two overlap, and it is caught on
   * its raised part too (§4.8); the others by their resting places, the right one over the left. A card
   * under the open pile list, or under any other panel, is not hovered through it.
   */
  function cardUnder(p) {
    if (ptrForeign) return null;
    if (env.geom.coversPointer && env.geom.coversPointer(p)) return null;
    const n = cards.length;
    const raised = (c) => {
      const i = cards.indexOf(c);
      return i >= 0 && c.mode === 'fan' && (inRect(p, rest(i, n)) || inRect(p, hoverPlace(i, n)));
    };
    if (sel && raised(sel.card)) return sel.card;
    if (hovered && raised(hovered)) return hovered;
    for (let i = n - 1; i >= 0; i--) {
      const c = cards[i];
      if (c.mode === 'fan' && inRect(p, rest(i, n))) return c;
    }
    return null;
  }
  function busy() { return returning.size > 0; }
  /**
   * battle_ui_ux_v2 §3.13 / §3.5 (review fix): the detail a right click or a long press opened stays up, over its card,
   * until Esc, a click or another right click closes it, and while it is up the hand is locked — the cards lift on
   * hover but none is taken. (Unity's UpdateHover closed it as the hover moved, so a long press showed nothing.) The
   * selected card's own detail (I) is a reading aid of the selection and locks nothing.
   */
  function detailLocked() { return panels.detailShown && !(sel && panels.detailFor === sel.card.id); }
  // While a card is selected (a click or a digit), the others do not lift on hover (review fix: two raised
  // cards made it unclear which one Enter or a click on the enemy would play). Clicking another card, or its
  // digit, still moves the selection.
  // An overlay (blocked) takes no press and no grab; the journal still lets the cards be hovered (§2.4): its veil
  // leaves the pointer to the hand outside its drawer (isForeign), the settings' modal never does.
  function canHover() { return interactable && (!blocked || hoverWhileBlocked) && !held && !down && !sel && !busy() && !destroyed; }
  function canGrab() { return interactable && !blocked && !grabLock && !held && !busy() && !destroyed && !detailLocked(); }
  /** A card was tried while the pile list blocks the hand: its answer (a refusal line), once per press or digit. */
  function triedBlocked(c) {
    if (!c || !blocked || !blockedTry || !interactable || held || busy() || destroyed) return false;
    blockedTry();
    return true;
  }
  /** A card was tried while the grab lock holds: the lock's own answer (a refusal line), once per press. */
  function triedLocked() {
    if (grabLock && interactable && !blocked && !held && !busy() && !destroyed) { grabLock(); return true; }
    return false;
  }

  function setHovered(c) {
    if (c === hovered) return;
    hovered = c;
    // The detail belongs to the card it was opened on and stays with it while the hover moves (§3.13, detailLocked).
    if (c) {
      const now = real.now;
      if (now - lastHoverSound >= HOVER_SOUND_GAP_MS) { env.audio.play('hover'); lastHoverSound = now; }
    }
    if (opts.onHover) opts.onHover(c ? c.face : null);
  }

  /** The upright box of a card as it stands now (CardPanels.Bounds). */
  function boxOf(c) { return aabb(c.pose.x, c.pose.y, c.pose.rot); }

  /** UpdateHoverPanels: the hover line, the reach hint and the detail follow the hovered card; a term under the pointer shows its meaning. */
  function updatePanels() {
    // §3.4 I: the selected card's detail stands over it while it is selected.
    if (sel && panels.detailShown && panels.detailFor === sel.card.id) { panels.hideHover(); panels.placeDetail(boxOf(sel.card)); return; }
    // The detail stands over its own card (where the hover line was); with it open, the hover panels are down.
    if (panels.detailShown) {
      const dc = cards.find((x) => x.id === panels.detailFor);
      panels.hideHover();
      if (dc && !held) panels.placeDetail(boxOf(dc)); else panels.hideDetail();
      return;
    }
    // Review 21 (§3.4 「同じ結果と同じ判断材料」: a card raised by a click or a digit hid its hover line and reach panel): the
    // selected card shows the panels a hovered one does.
    const c = held ? null : hovered || (sel ? sel.card : null);
    if (!c || c === longRest) { panels.hideHover(); return; }
    const box = boxOf(c);
    panels.showHover(c.face, box, !!(c.view && c.view.lit));
    let shown = false;
    if (ptrClient && c.view && c.view.terms.length) {
      for (const t of c.view.terms) {
        const r = t.getBoundingClientRect();
        if (ptrClient.x >= r.left && ptrClient.x <= r.right && ptrClient.y >= r.top && ptrClient.y <= r.bottom) {
          const a = env.toStage({ clientX: r.left, clientY: r.top });
          const b = env.toStage({ clientX: r.right, clientY: r.bottom });
          panels.showTerm({ x: a.x, y: a.y, w: b.x - a.x, h: b.y - a.y }, t.__mark);
          shown = true;
          break;
        }
      }
    }
    if (!shown) panels.hideTerm();
  }

  // ---- the zone (ZoneAt / ReceiverTakes / SetZoneHot) -----------------------------------------------
  /** Every standing enemy's receiver, { unit, box } (one with a single enemy). */
  function receivers() {
    if (env.geom.receivers) return env.geom.receivers().map((r) => ({ unit: r.unit, box: new ScreenBox(r.box.X, r.box.Y, r.box.Width, r.box.Height) }));
    const r = env.geom.receiver ? env.geom.receiver() : null;
    return r ? [{ unit: -1, box: new ScreenBox(r.X, r.Y, r.Width, r.Height) }] : [];
  }
  function receiverBox(unit) {
    const list = receivers();
    if (!list.length) return null;
    const want = unit != null && unit >= 0 ? unit : null;
    const hit = want != null ? list.find((r) => r.unit === want) : null;
    const st = held || sel;
    const base = st ? framedOf(Object.assign({}, st, { unit: -1 })) : defaultUnit(null);
    return (hit || list.find((r) => r.unit === base) || list[0]).box;
  }
  const inBox = (r, p) => p.x >= r.X && p.x < r.Right && p.y >= r.Y && p.y < r.Bottom;
  /**
   * ZoneAt: a single-target card is taken by a receiver when half of the card lies on it or the pointer
   * is inside it (ReceiverDrop.Takes, #568), measured where the pointer carries the card before the snap;
   * a self card when the pointer is above the throw line (ThrowLineView.IsAbove). Returns the zone and the
   * enemy whose receiver took the card (−1 for none / a self card); the one under the pointer wins.
   */
  function zoneAt(face, cx, cy, rot, p) {
    if (face.Aim === CardAim.Single) {
      const list = receivers();
      if (!list.length) return { zone: DropZone.None, unit: -1 };
      const b = aabb(cx, cy, rot);
      const card = new ScreenBox(b.x, b.y, b.w, b.h);
      const under = list.find((r) => inBox(r.box, p));
      const took = under || list.find((r) => ReceiverDrop.Takes(card, r.box, p.x, p.y));
      return took ? { zone: DropZone.Receiver, unit: took.unit } : { zone: DropZone.None, unit: -1 };
    }
    return { zone: p.y < L.THROW.y ? DropZone.AboveThrowLine : DropZone.None, unit: -1 };
  }
  /** The framed enemy changed: the hooks, the tag, the lamp and the player's HP wick follow it. */
  function setFramed(state, unit) {
    if (!state || state.unit === unit) return;
    state.unit = unit;
    const c = state.card;
    const u = framedOf(state);
    marks.setTarget(u);
    setLitFor(c, true, u);
    if (opts.onTarget) opts.onTarget(c.face, u);
  }
  /** SetZoneHot (+ the design system's frame sounds: in, out, or the dull one over an enemy out of reach). */
  function setHot(state, on, face) {
    if (!state || state.hot === on) return;
    state.hot = on;
    marks.setHot(on);
    if (on) env.audio.play(face.Aim === CardAim.Single && face.ReachHint ? 'noReach' : 'frameIn');
    else env.audio.play('frameOut');
  }

  // ---- hold (BeginHold … Release) ---------------------------------------------------------------------
  /** The zone and the hooks, the lamp, and the player's pay preview (BeginHold's shared half). */
  function lift(c, base) {
    setLitFor(c, true, base);
    marks.show(c.face, (unit) => (opts.preview ? opts.preview(c.id, unit) || '' : ''), base);
    if (opts.onHoldStart) opts.onHoldStart(c.face, c.face.Aim === CardAim.Single ? (base != null && base >= 0 ? base : defaultUnit(c.face)) : -1);
  }
  function drop(c) {
    marks.hide();
    setLitFor(c, false);
    if (opts.onHoldEnd) opts.onHoldEnd(c.face);
  }

  function beginHold(c, auto) {
    if (held || cards.indexOf(c) < 0) return;
    if (sel) endSelection(false);
    setHovered(null);
    panels.hideAll();
    // The hover stops while a card is held: settle any card it left half lifted (BeginHold).
    cards.forEach((o) => { if (o !== c && o.mode === 'fan') o.hw = 0; });
    c.mode = 'held';
    held = { card: c, offX: c.pose.x - ptr.x, offY: c.pose.y - ptr.y, hot: false, snapW: 0, auto: !!auto, zone: DropZone.None, unit: -1, base: baseUnit(c.face) };
    if (auto) { held.offX = 0; held.offY = 0; }
    c.pose.a = L.HELD_ALPHA;
    c.pose.s = 1;
    layer.held.appendChild(c.wrap);
    env.audio.play('grab');
    // EffectId.CardGrab: the held card stands upright (out). §0.1 decision 7: its size and shadow stay.
    const tilt = c.pose.rot;
    const ms = env.fx.ms('CardGrab');
    const mine = held;
    if (ms > 0) clock.run(ms, 'out', (t) => { if (held === mine) c.pose.rot = tilt * (1 - t); });
    else c.pose.rot = 0;
    lift(c, held.base);
  }

  /** OnDragMoved + UpdateSnap: under the pointer (the grabbed point kept), drawn toward the receiver while over it. */
  function updateHold(dt) {
    const c = held.card;
    const fx = ptr.x + held.offX;
    const fy = ptr.y + held.offY;
    const at = zoneAt(c.face, fx, fy, c.pose.rot, ptr);
    const zone = at.zone;
    held.zone = zone;
    setFramed(held, at.unit);
    setHot(held, zone !== DropZone.None, c.face);
    const snapMs = env.fx.ms('ReceiverSnap');
    const target = c.face.Aim === CardAim.Single && held.hot ? 1 : 0;
    held.snapW = snapMs > 0 ? moveTowards(held.snapW, target, dt / snapMs) : target;
    const r = receiverBox(held.unit);
    const pull = r ? L.SNAP_PULL * EASE.smooth(held.snapW) : 0;
    c.pose.x = r ? lerp(fx, r.X + r.Width / 2, pull) : fx;
    c.pose.y = r ? lerp(fy, r.Y + r.Height / 2, pull) : fy;
  }

  /** The hold's decorations go; the card stays where it was let go, for the player to fly or send home. */
  function endHold() {
    const c = held.card;
    held = null;
    c.mode = 'free';
    drop(c);
    return c;
  }

  /** Release: the source decides through the player (onRelease → TryPlay); the hand shows the answer. */
  function releaseHeld(zone, unit) {
    const c = endHold();
    handOver(c, zone, unit);
  }
  /** The card being handed to the player right now: the dark sheet the flow puts on the hand skips it (it is played). */
  let handingOver = null;
  function handOver(c, zone, unit) {
    handingOver = c;
    try {
      if (opts.onRelease) opts.onRelease(c.id, zone, zone === DropZone.Receiver && unit != null && unit >= 0 ? unit : undefined);
    } finally { handingOver = null; }
    // Nobody took the card (no player): it slides home.
    if (cards.indexOf(c) >= 0 && c.mode === 'free' && !returning.has(c)) slideHome(c, false, false);
  }

  /** ReturnToHand's motion: home over CardReturn (out), upright → its tilt, then the refusal shake (UiTween.Shake 8 px × 2). */
  function slideHome(c, shake, sound) {
    if (sound) {
      if (shake) { env.audio.play('refusal'); env.audio.play('refusal', 70); } else env.audio.play('cancel');
    }
    returning.add(c);
    c.mode = 'free';
    c.hw = 0;
    const my = ++c.motion;
    layer.held.appendChild(c.wrap);
    const i = cards.indexOf(c);
    const r = rest(Math.max(0, i), cards.length);
    return clock.tween(c.pose, { x: r.x, y: r.y, rot: r.rot, s: 1 }, env.fx.ms('CardReturn'), 'out').then(() => {
      if (c.motion !== my) return undefined;
      c.pose.a = 1;
      setLitFor(c, false);
      const shakeMs = env.fx.ms('RefusalShake');
      if (!shake || shakeMs <= 0) return undefined;
      return clock.run(shakeMs, 'linear', (t) => { if (c.motion === my) c.shake = Math.sin(t * Math.PI * 2 * L.SHAKE.cycles) * L.SHAKE.px * (1 - t); });
    }).then(() => {
      c.shake = 0;
      returning.delete(c);
      if (c.motion !== my || cards.indexOf(c) < 0) return;
      // LayoutHand: every card home, the hover dropped.
      layer.hand.appendChild(c.wrap);
      c.mode = 'fan';
      c.hw = 0;
      render();
    });
  }

  /**
   * §5.9 順 1 「カードのランプが 1.4 倍に膨らんで戻り、条件名 1 語がカードと一緒に消える」 (review 19): on the released card, the
   * lamp swells and back over LAMP_SWELL.ms (out) and the trait's condition word stands over its trait box — a child of the
   * card, so it shrinks and fades away with it.
   */
  function traitSignal(c, word) {
    const lamp = c.wrap.querySelector('.ocb-lamp');
    const box = c.wrap.querySelector('.ocb-trait');
    if (lamp) clock.run(LAMP_SWELL.ms, 'out', (t) => { lamp.style.scale = (1 + (LAMP_SWELL.scale - 1) * Math.sin(t * Math.PI)).toFixed(3); }).then(() => { lamp.style.scale = ''; });
    if (box && word) box.appendChild(h('span', { class: 'ocbg-battle-card-trait' }, word));
  }

  // ---- the keyboard / click path (battle_ui_ux_v2 §3.4) -------------------------------------------------
  /** A card is lifted to its hover place with the same hooks / line and prediction a drag shows; it does not follow the pointer. */
  function selectCard(c, auto) {
    // §3.4 one key, one role: a digit selects (the selected card's own digit keeps it); Enter plays.
    if (sel && sel.card === c) { if (!auto) sel.auto = false; return true; }
    if (sel) endSelection(false);
    panels.hideAll();
    // `auto` (review 19): the scene picked it after an Enter play (§3.4 確定後は扇の同じ位置の札を自動で選択する), not the player.
    sel = { card: c, hot: false, unit: -1, tabbed: false, tabAt: null, base: baseUnit(c.face), auto: !!auto };
    env.audio.play('grab');
    lift(c, sel.base);
    return true;
  }
  function endSelection(sound) {
    if (!sel) return;
    const c = sel.card;
    sel = null;
    if (panels.detailFor === c.id) panels.hideDetail();
    drop(c);
    if (sound) env.audio.play('cancel');
  }
  const inRectBox = (b, p) => !!b && p.x >= b.x && p.x <= b.x + b.w && p.y >= b.y && p.y <= b.y + b.h;
  /**
   * The enemy whose omen badge or number panel stands under `p`, or null (review fix: they move with the enemy
   * and read as part of it, so a click there on the click path, §3.4, is a click on that enemy — it dropped the
   * selection). A fallen enemy's grey panel counts for nothing.
   */
  function partOfEnemyAt(p) {
    for (const r of receivers()) {
      const u = r.unit >= 0 ? r.unit : undefined;
      const badge = env.geom.omenBox ? env.geom.omenBox(u) : null;
      const shownBadge = badge && env.geom.omenShown ? env.geom.omenShown(u) : !!badge;
      if (shownBadge && inRectBox(badge, p)) return r.unit;
      if (env.geom.statusBox && inRectBox(env.geom.statusBox(UnitSide.Enemy, u), p)) return r.unit;
    }
    return null;
  }
  /** Where a click would play the selected card: inside an enemy's receiver, hooks, omen badge or panel, or above the throw line. */
  function selectionZone(p) {
    const c = sel.card;
    if (c.face.Aim === CardAim.Single) {
      for (const r of receivers()) {
        const b = marks.boxOf(UnitSide.Enemy, r.unit >= 0 ? r.unit : undefined);
        if (inBox(r.box, p) || inRectBox(b, p)) return { zone: DropZone.Receiver, unit: r.unit };
      }
      const part = partOfEnemyAt(p);
      if (part != null) return { zone: DropZone.Receiver, unit: part };
      return { zone: DropZone.None, unit: -1 };
    }
    return { zone: p.y < L.THROW.y ? DropZone.AboveThrowLine : DropZone.None, unit: -1 };
  }

  /** The detail on the card under the pointer (§7.4: right click, or a 400 ms press; `open`: never closes it). */
  function toggleDetailOn(c, open) {
    if (!c || !c.face.Detail) { panels.hideDetail(); return; }
    if (sel && sel.card !== c) endSelection(false); // the detail locks the hand: no selection stands under it
    if (hovered !== c) setHovered(c);
    if (open && panels.detailShown && panels.detailFor === c.id) return;
    panels.toggleDetail(c.face, boxOf(c), detailExtra(c));
  }
  /** A press that closed the locked detail: its release does nothing more (no selection, no drop). */
  let swallowUp = false;
  /** The card a long press sent back to its rest place (§3.13): it is not raised again until the pointer leaves it. */
  let longRest = null;
  /** The card raised when the refusal line came up (its own press was refused): the line does not step aside for it. */
  let refusalOwner = null;

  // ---- pointer ---------------------------------------------------------------------------------------
  function isMine(target) {
    return cards.some((c) => c.wrap.contains(target));
  }
  /**
   * Another module's panel, a button or an overlay (the pile list, a drawer's own panel): the hand leaves it alone.
   * `p` (stage px): an enemy's number panel (its chips take the pointer for their tooltips) is the enemy's while a
   * card is selected (partOfEnemyAt), and the journal's veil outside its drawer lets the hand be read (§2.4).
   */
  function isForeign(target, p) {
    if (!target || !env.root.contains(target)) return true;
    if (target === env.root || isMine(target)) return false;
    if (p && sel && layer.panels.contains(target) && partOfEnemyAt(p) != null) return false;
    if (p && layer.over.contains(target) && underJournalVeil(target, p)) return false;
    return layer.hud.contains(target) || layer.over.contains(target) || layer.panels.contains(target);
  }
  /**
   * battle_ui_ux_v2 §2.4 (review fix: no card could be hovered with the journal open): 「開いている間はカードをホバー
   * できるがプレイできません」. The journal's host covers the screen (its veil closes it on a click); over the veil,
   * outside the drawer (env.geom.journalCovers), the cards lift and show their panels, and nothing is taken.
   */
  function underJournalVeil(target, p) {
    const host = target.closest ? target.closest('.ocbg-battle-journal') : null;
    if (!host) return false;
    return !(env.geom.journalCovers && env.geom.journalCovers(p));
  }
  function onDown(e) {
    if (destroyed) return;
    ptr = env.toStage(e);
    ptrClient = { x: e.clientX, y: e.clientY };
    if (e.button !== 0 || held) return;
    const foreign = isForeign(e.target, ptr);
    // §3.13: a click closes the locked detail (on the scene's own ground it does nothing more).
    if (detailLocked() && env.root.contains(e.target)) {
      panels.hideDetail();
      if (!foreign) { swallowUp = true; return; }
    }
    if (foreign) { ptrForeign = true; return; }
    if (triedBlocked(cardUnder(ptr))) return;
    if (grabLock && cardUnder(ptr) && triedLocked()) return;
    const c = canGrab() ? cardUnder(ptr) : null;
    if (c) {
      const token = ++pressToken;
      down = { card: c, x: ptr.x, y: ptr.y, token, long: false };
      try { env.root.setPointerCapture(e.pointerId); } catch (err) { /* ignore */ }
      // §3.13: a press held 400 ms without moving opens the detail, and the release then plays nothing. Review fix:
      // a press that moves 8 px after that still becomes a drag (the detail closes) — a player who pauses before
      // dragging gets the drag, and a drag whose moves came late (a busy frame) is not lost to the timer.
      real.wait(L.LONG_PRESS_MS).then(() => {
        if (!down || down.token !== token || held) return;
        down.long = true;
        // §3.2 / §3.13 (review 16): the press is spent, the card goes back to its rest place over LONG_PRESS_RETURN_MS,
        // and then the detail opens (it stayed raised under the detail). It stays down until the pointer leaves it.
        const pressed = down.card;
        longRest = pressed;
        panels.hideHover();
        clock.wait(LONG_PRESS_RETURN_MS).then(() => {
          if (pressToken !== token || held || cards.indexOf(pressed) < 0 || longRest !== pressed) return;
          toggleDetailOn(pressed, true);
        });
      });
      return;
    }
    bgDown = { x: ptr.x, y: ptr.y };
  }
  /** The press turns into a drag: the card is held as if the pointer had moved off it from the press point. */
  function dragFromPress(d) {
    if (d.long) { panels.hideDetail(); longRest = null; }
    if (!canGrab()) return false;
    const now = ptr;
    ptr = { x: d.x, y: d.y };
    beginHold(d.card, false);
    ptr = now;
    return !!held;
  }
  /**
   * Review 16 (after a window blur a held card stayed glued to the pointer and played on the next click): the window
   * lost focus or the page was hidden, so the release will not come here — a held card slides home unplayed and a
   * press under way is dropped, as Esc does. A mouse move with no button down (the release was lost) does the same.
   */
  function dropLostHold() {
    if (destroyed) return;
    if (down) { down = null; pressToken++; }
    bgDown = null;
    if (held && !held.auto) api.cancel();
  }
  function onBlur() { dropLostHold(); }
  function onVisibility() { if (document.hidden) dropLostHold(); }
  function onMove(e) {
    if (destroyed) return;
    if ((held && !held.auto) || down) {
      if (e.pointerType === 'mouse' && e.buttons === 0) { dropLostHold(); return; }
    }
    ptr = env.toStage(e);
    ptrClient = { x: e.clientX, y: e.clientY };
    ptrForeign = !held && !down && isForeign(e.target, ptr);
    if (down && !held) {
      // §3.2: 8 px from the press the card leaves the fan (stage px, not scaled by speed).
      if (Math.hypot(ptr.x - down.x, ptr.y - down.y) >= L.DRAG_THRESHOLD) {
        const d = down;
        down = null;
        dragFromPress(d);
      }
    }
  }
  function onUp(e) {
    if (destroyed) return;
    ptr = env.toStage(e);
    ptrClient = { x: e.clientX, y: e.clientY };
    try { if (env.root.hasPointerCapture && env.root.hasPointerCapture(e.pointerId)) env.root.releasePointerCapture(e.pointerId); } catch (err) { /* ignore */ }
    if (swallowUp) { swallowUp = false; bgDown = null; return; }
    // A press that left its card by 8 px with no move reported in between (a busy frame): it was a drag all along.
    if (down && !held && e.button === 0 && Math.hypot(ptr.x - down.x, ptr.y - down.y) >= L.DRAG_THRESHOLD && cards.indexOf(down.card) >= 0) {
      const d = down;
      down = null;
      dragFromPress(d);
    }
    if (held && !held.auto) {
      const c = held.card;
      // OnDragEnded: the zone where the pointer lets go, measured with the card where the pointer carries it.
      const at = zoneAt(c.face, ptr.x + held.offX, ptr.y + held.offY, c.pose.rot, ptr);
      releaseHeld(at.zone, at.unit);
      return;
    }
    if (down) {
      const c = down.card;
      const long = down.long;
      down = null;
      if (long) return; // the long press opened the detail; letting go leaves it up (§3.13)
      if (e.button !== 0 || !canGrab() || cards.indexOf(c) < 0) return;
      // A click on a card selects it; on the selected card again, lets it go back (§3.4). Review 19 (after an Enter play the
      // card the scene picked was dropped by the player's click on it, and the next click on the enemy did nothing): a click
      // on a card the scene picked takes it as the player's own selection — the next click on the enemy plays it.
      if (sel && sel.card === c && sel.auto) { sel.auto = false; env.audio.play('grab'); }
      else if (sel && sel.card === c) endSelection(true);
      else selectCard(c);
      return;
    }
    if (bgDown) {
      const moved = Math.hypot(ptr.x - bgDown.x, ptr.y - bgDown.y);
      bgDown = null;
      if (moved >= L.DRAG_THRESHOLD || e.button !== 0) return;
      panels.hideDetail();
      if (sel && interactable && !busy()) {
        // §3.4: a click on the enemy's frame (or above the line for a self card) plays; anywhere else drops the selection.
        const at = selectionZone(ptr);
        if (at.zone !== DropZone.None) confirmOn(at.unit);
        else endSelection(true);
      }
    }
  }
  function onCancel() {
    down = null;
    bgDown = null;
    if (held && !held.auto) api.cancel();
  }
  function onLeave() {
    if (held) return;
    ptr = { x: FAR.x, y: FAR.y };
    ptrClient = null;
  }
  /** §7.4: a right click on the hovered card toggles its detail; elsewhere it closes it. The browser menu never opens in battle. */
  function onContext(e) {
    e.preventDefault();
    if (destroyed || held) return;
    ptr = env.toStage(e);
    const c = interactable && !busy() ? (cardUnder(ptr) || null) : null;
    if (c) toggleDetailOn(c);
    else panels.hideDetail();
  }
  window.addEventListener('pointerdown', onDown);
  window.addEventListener('pointermove', onMove);
  window.addEventListener('pointerup', onUp);
  window.addEventListener('pointercancel', onCancel);
  window.addEventListener('blur', onBlur);
  document.addEventListener('visibilitychange', onVisibility);
  env.root.addEventListener('pointerleave', onLeave);
  env.root.addEventListener('contextmenu', onContext);

  // ---- the frame loop ------------------------------------------------------------------------------
  const offFrame = clock.onFrame((dtScene, dtReal) => {
    if (destroyed) return;
    // While an overlay holds the scene clock still (the journal), the hover runs on real time (§2.4).
    const dt = clock.paused ? dtReal || 0 : dtScene;
    if (canHover()) setHovered(cardUnder(ptr));
    else if (!down) setHovered(null);
    // EffectId.CardHover: the weight moves linearly over the effect's length (smoothstep on the place).
    const hoverMs = env.fx.ms('CardHover');
    const step = hoverMs > 0 ? dt / hoverMs : 1;
    // A long-pressed card stays at rest until the pointer has left it (§3.13).
    if (longRest && hovered !== longRest && !(down && down.card === longRest)) longRest = null;
    cards.forEach((c) => {
      if (c.mode !== 'fan') return;
      if (c === longRest) {
        // §3.2 詳細を見る: back to its rest place over LONG_PRESS_RETURN_MS.
        c.hw = moveTowards(c.hw, 0, LONG_PRESS_RETURN_MS > 0 ? dt / LONG_PRESS_RETURN_MS : 1);
        return;
      }
      const up = c === hovered || (sel && sel.card === c) || (down && down.card === c) || (panels.detailShown && panels.detailFor === c.id);
      const target = up ? 1 : 0;
      c.hw = moveTowards(c.hw, target, step);
    });
    if (held && !held.auto) updateHold(dt);
    if (sel) {
      const at = interactable && !ptrForeign ? selectionZone(ptr) : { zone: DropZone.None, unit: -1 };
      // A Tab focus holds until the pointer moves (§3.4: Tab moves which frame Enter plays on).
      if (sel.tabbed && sel.tabAt && Math.hypot(ptr.x - sel.tabAt.x, ptr.y - sel.tabAt.y) >= L.DRAG_THRESHOLD) sel.tabbed = false;
      if (!sel.tabbed && at.zone !== DropZone.None) setFramed(sel, at.unit);
      setHot(sel, sel.tabbed || at.zone !== DropZone.None, sel.card.face);
    }
    render();
    marks.update();
    updatePanels();
    // The guide steps aside while a hovered card's panels or the detail stand in its band (review fix: it showed
    // through them in torn pieces), and while a card is selected (review 16: the selected card stands at the hover
    // place, top y 760, across the guide's band, and the guide's drag wording is not the click path's). It stays up
    // under a held card, which goes where the pointer takes it.
    panels.stepGuide(dt, panels.panelsUp || !!sel);
    panels.stepRefusal(dtReal || 0, raisedOverRefusal());
    env.root.classList.toggle('ocbg-battle--grab', !!(hovered && interactable && !held));
    env.root.classList.toggle('ocbg-battle--grabbing', !!held);
  });

  /**
   * A card raised out of the fan (hovered, pressed or selected) stands over the refusal line (review 16: after a
   * refusal, the line covered the name and the values of the next card hovered, for up to 3.5 s).
   */
  function raisedOverRefusal() {
    const r = panels.refusalBox();
    if (!r) return false;
    // The card whose own press brought the line up (a locked press) keeps it: the reason is about that card.
    if (refusalOwner && (refusalOwner.hw <= 0.3 || refusalOwner.mode !== 'fan' || cards.indexOf(refusalOwner) < 0)) refusalOwner = null;
    return cards.some((c) => {
      if (c === refusalOwner || c.mode !== 'fan' || c.hw <= 0.3) return false;
      const b = boxOf(c);
      return b.x < r.x + r.w && b.x + b.w > r.x && b.y < r.y + r.h && b.y + b.h > r.y;
    });
  }

  /** Plays the selection on its own zone; on enemy `unit` when a click named one, else where the source sends it. */
  function confirmOn(unit) {
    if (!sel || !interactable || busy()) return false;
    const c = sel.card;
    sel = null;
    drop(c);
    c.mode = 'free';
    handOver(c, DepictionText.RequiredZone(c.face.Aim), unit);
    return true;
  }

  // ---- the dim (RefreshPlayableLook / CardView.FadeDimmed) --------------------------------------------
  function setDim(c, dim) {
    if (dim === c.dim) return;
    c.dim = dim;
    const token = ++c.dimToken;
    const from = c.dimA;
    const to = dim ? DIM : 0;
    const ms = env.fx.ms('UnpayableDim');
    if (ms <= 0) { c.dimA = to; if (c.view) c.view.setDim(to); return; }
    clock.run(ms, 'linear', (t) => {
      if (token !== c.dimToken) return;
      c.dimA = lerp(from, to, t);
      if (c.view) c.view.setDim(c.dimA);
    });
  }

  const api = {
    // ---- frame ----
    sameHand(faces) {
      faces = faces || [];
      if (faces.length !== cards.length) return false;
      for (let i = 0; i < faces.length; i++) if (cards[i].id !== faces[i].Id) return false;
      return true;
    },
    /** BuildHand: new card elements at their fan places, undimmed. */
    build(faces) {
      api.clear();
      cards = (faces || []).map(make);
      api.layout();
    },
    /** Same ids: each face is bound again in place (cost, lamp, values, hover line). */
    bind(faces) {
      (faces || []).forEach((f, i) => {
        const c = cards[i];
        if (!c) return;
        c.face = f;
        draw(c);
      });
    },
    /** LayoutHand: every card to its place at once, the hover dropped. */
    layout() {
      setHovered(null);
      cards.forEach((c) => {
        if ((held && held.card === c) || (sel && sel.card === c) || returning.has(c)) return;
        c.mode = 'fan';
        c.motion++;
        c.hw = 0;
        c.shake = 0;
        if (c.wrap.parentNode !== layer.hand) layer.hand.appendChild(c.wrap);
      });
      render();
    },
    clear() {
      if (held) endHold();
      if (sel) endSelection(false);
      down = null;
      cards.forEach((c) => c.wrap.remove());
      cards = [];
      returning.clear();
      hovered = null;
      longRest = null;
      panels.hideAll();
    },
    count() { return cards.length; },
    faces() { return cards.map((c) => c.face); },

    // ---- state set by the player ----
    /** SetHandInteractable: off, no hover and no grab; a held card is let go and the refusal line goes. */
    setInteractable(on) {
      interactable = !!on;
      if (!interactable) {
        if (held && !held.auto) api.letGo();
        if (sel) endSelection(false);
        down = null;
        bgDown = null;
        setHovered(null);
        panels.hideAll();
        panels.hideRefusal();
      }
    },
    /** RefreshPlayableLook: the cards the source would refuse now darken (UnpayableDim), and the guide line prints. */
    refreshPlayable(fn, guide) {
      dimOf = fn || (() => false);
      cards.forEach((c) => {
        if ((held && held.card === c) || c === handingOver) return;
        setDim(c, !!dimOf(c.id));
      });
      panels.setGuide(guide || '');
    },
    /**
     * Review 21 (battle_ui_ux_v2 §3.5 / §5.10): the player holds 集中 / 強化 / 威圧 — `marks` = { focus, empower, intimidate };
     * the cards they act on carry the mark beside their values.
     */
    setStatusMarks(marks) {
      const key = marks ? [marks.focus, marks.empower, marks.intimidate].map(Number).join('') : '';
      const was = statusMarks ? [statusMarks.focus, statusMarks.empower, statusMarks.intimidate].map(Number).join('') : '';
      statusMarks = marks || null;
      if (key !== was) cards.forEach(applyMods);
    },
    isHolding() { return !!held; },
    heldFace() { return held ? held.card.face : sel ? sel.card.face : null; },
    hoveredFace() { return hovered ? hovered.face : null; },
    /** Dev / tests: what decides whether a press takes a card now (the playtest logs it when a drag misses). */
    debugState() {
      return { interactable, blocked, grabLock: !!grabLock, held: !!held, sel: !!sel, down: !!down, returning: returning.size, ptrForeign,
        hovered: hovered ? hovered.id : null, modes: cards.map((c) => c.id + ':' + c.mode), ptr: { x: Math.round(ptr.x), y: Math.round(ptr.y) } };
    },
    /** Addition: a card is selected by a click or a digit (not held). */
    hasSelection() { return !!sel; },
    /** Addition: the right-click detail is open. */
    detailOpen() { return panels.detailShown; },
    /** Addition (several enemies): the enemy the held / selected card is framed over now, else its default target; −1 for none. */
    framedUnit() {
      const st = held || sel;
      if (!st || st.card.face.Aim !== CardAim.Single) return -1;
      return framedOf(st);
    },

    // ---- beats ----
    /**
     * FlyDrawn: cards [first..] fly in from `from` (the draw pile) one stagger apart (EffectPlan.DrawCardStartMs),
     * out, growing from the pile's size; a draw that ran the pile out calls onRebuild at RebuildStartMs.
     */
    flyDrawn(first, rebuildAfter, o) {
      o = o || {};
      const from = o.from || { x: 64, y: 990 };
      const flying = cards.slice(Math.max(0, first));
      flying.forEach((c) => { c.mode = 'free'; c.hw = 0; c.motion++; Object.assign(c.pose, { x: from.x, y: from.y, rot: 0, s: PILE_SCALE, a: 0 }); });
      render();
      const ms = env.fx.ms('CardDraw');
      const lands = flying.map((c, k) => {
        const my = c.motion;
        return clock.wait(EffectPlan.DrawCardStartMs(k, rebuildAfter, env.fx.switches)).then(() => {
          env.audio.play('draw');
          const i = cards.indexOf(c);
          if (i < 0 || c.motion !== my) return undefined;
          const r = rest(i, cards.length);
          return clock.tween(c.pose, { x: r.x, y: r.y, rot: r.rot, s: 1, a: 1 }, ms, 'out');
        }).then(() => { if (c.motion === my && cards.indexOf(c) >= 0 && c.mode === 'free') c.mode = 'fan'; });
      });
      if (rebuildAfter >= 0 && rebuildAfter < flying.length && o.onRebuild) {
        clock.wait(EffectPlan.RebuildStartMs(rebuildAfter, env.fx.switches)).then(() => o.onRebuild());
      }
      return Promise.all(lands).then(() => {});
    },
    /** FlyDiscarded: the whole hand to the discard pile one stagger apart (HandDiscard, in), then cleared. */
    discardAll(to) {
      to = to || { x: 150, y: 990 };
      const ms = env.fx.ms('HandDiscard');
      const stagger = env.fx.stagger('HandDiscard');
      if (sel) endSelection(false);
      setHovered(null);
      const list = cards.slice();
      const flights = list.map((c, i) => clock.wait(stagger * i).then(() => {
        c.mode = 'free';
        c.motion++;
        returning.delete(c);
        env.audio.play('discard');
        return clock.tween(c.pose, { x: to.x, y: to.y, rot: 0, s: PILE_SCALE, a: 0 }, ms, 'in');
      }));
      return Promise.all(flights).then(() => api.clear());
    },
    /**
     * PlayAccepted's CardRelease: the card leaves the hand list at once and flies to `to` (a single-target
     * card: the receiver's centre) or 140 px up (a self card), shrinking to half and fading, ease in.
     * The rest of the fan stays where it is until settle().
     */
    release(cardId, ev, to, o) {
      const i = cards.findIndex((c) => c.id === cardId);
      if (i < 0) return { done: Promise.resolve(), vanishedAt: () => to || { x: 960, y: 700 } };
      if (sel && sel.card.id === cardId) endSelection(false);
      if (held && held.card.id === cardId) endHold();
      const c = cards.splice(i, 1)[0];
      if (c === hovered) setHovered(null);
      returning.delete(c);
      // The others keep their places (Unity sets them only in LayoutHand / SettleHand).
      cards.forEach((o) => { if (o.mode === 'fan') { o.mode = 'free'; o.hw = 0; } });
      leaving.set(c.id, c);
      c.mode = 'free';
      c.motion++;
      c.shake = 0;
      layer.held.appendChild(c.wrap);
      env.audio.play(RELEASE_SOUND[c.face.Kind] || 'releaseSkill');
      // battle_ui_ux_v2 §5.9 順 1 (review 19): a trait the card fires — its lamp swells 1.4× and back, and its condition
      // word stands on the card and leaves with it.
      if (o && o.traitWord) traitSignal(c, o.traitWord);
      c.pose.a = 1;
      // §5.11 順 1 / 順 4 (review 19; Unity tosses every self card up and fades it): a stance card flies to the end of the
      // stance list and shrinks there to its ghost's size; it stands in the slot (`keep`) until its ghost leaves for the
      // exile pile (player.js released → ghostToPile). The flow holds CardRelease's wait only; the card flies on beside it.
      const slot = c.face.Kind === CardKind.Stance && env.geom.stanceSlot ? env.geom.stanceSlot(UnitSide.Player) : null;
      if (slot) {
        clock.tween(c.pose, { x: slot.x + slot.w / 2, y: slot.y + slot.h / 2, rot: 0, s: STANCE_CARD_SCALE }, L.STANCE_FLY_MS, 'out');
        return { done: clock.wait(env.fx.ms('CardRelease')), vanishedAt: () => ({ x: c.pose.x, y: c.pose.y }), face: c.face, keep: true };
      }
      const target = ev && ev.Aim === CardAim.Single && to ? to : { x: c.pose.x, y: c.pose.y - L.RELEASE_RISE };
      const done = clock.tween(c.pose, { x: target.x, y: target.y, s: L.RELEASE_SCALE, a: 0 }, env.fx.ms('CardRelease'), 'in');
      return { done, vanishedAt: () => ({ x: c.pose.x, y: c.pose.y }), face: c.face };
    },
    /**
     * The played card's face for its ghost on the way to its pile (review fix: Unity's FlyGhostTo flies a plain pale
     * sheet, which read as a missing texture): a fresh element of the design system's card for `face`, full size;
     * the field shrinks it to the ghost's 76×104.
     */
    ghostCard(face) {
      if (!face) return null;
      try { return buildCard(OCB, face).el; } catch (err) { return null; }
    },
    /** Released: the card's element goes. */
    dropCard(cardId) {
      const c = leaving.get(cardId);
      if (c) { c.wrap.remove(); leaving.delete(cardId); }
    },
    /** SettleHand: the rest slide from where they are to their new fan places (out). */
    settle(ms) {
      const n = cards.length;
      return Promise.all(cards.map((c, i) => {
        if (returning.has(c)) return Promise.resolve();
        c.mode = 'free';
        const my = ++c.motion;
        const r = rest(i, n);
        return clock.tween(c.pose, { x: r.x, y: r.y, rot: r.rot, s: 1, a: 1 }, ms, 'out').then(() => {
          if (c.motion === my && cards.indexOf(c) >= 0 && c.mode === 'free') { c.mode = 'fan'; c.hw = 0; }
        });
      })).then(() => {});
    },
    /** ReturnToHand: CardReturn home, then the RefusalShake; 'cancel', or 'refusal' twice when it shakes. */
    returnToHand(cardId, shake) {
      const c = cards.find((x) => x.id === cardId);
      if (!c) return Promise.resolve();
      if (held && held.card === c) endHold();
      if (sel && sel.card === c) endSelection(false);
      return slideHome(c, !!shake, true);
    },
    /**
     * LetGo (Halt): a held card goes back to its place at once, unplayed, and its decorations go. A press
     * that has not yet become a drag (and a press on the background) is dropped too (review fix: a card
     * pressed just before J could still be dragged and played under the journal).
     */
    letGo() {
      if (sel) endSelection(false);
      if (down) { down = null; pressToken++; }
      bgDown = null;
      if (!held) return;
      const c = endHold();
      c.pose.a = 1;
      c.mode = 'fan';
      c.hw = 0;
      layer.hand.appendChild(c.wrap);
      api.layout();
    },
    /**
     * An overlay is up (true) or gone (false): while up, no press or grab reaches the hand, and no hover unless
     * `hover` (the journal alone: its cards can be read but not played, battle_ui_ux_v2 §2.4).
     */
    setBlocked(on, hover, onTry) {
      blocked = !!on;
      hoverWhileBlocked = !!on && !!hover;
      // Review 16 (the pile list): a press on a card or its digit while blocked calls onTry (the reason on the refusal
      // line) instead of doing nothing.
      blockedTry = blocked && typeof onTry === 'function' ? onTry : null;
      if (blocked) {
        if (down) { down = null; pressToken++; }
        bgDown = null;
        setHovered(null);
      }
    },
    /**
     * battle_ui_ux_v2 §3.8 (俊敏's choice): `onTry` set, no card can be taken (hover still lifts it); a press on a
     * card or its digit calls onTry instead. null lifts the lock. A selection or a press under way is dropped.
     */
    setGrabLock(onTry) {
      grabLock = typeof onTry === 'function' ? onTry : null;
      if (grabLock) {
        if (sel) endSelection(false);
        if (down) { down = null; pressToken++; }
      }
    },
    /** The enemies whose frame shows the held / selected card's prediction tag (hand-marks.taggedUnits). */
    taggedUnits() { return marks.shown ? marks.taggedUnits() : []; },
    /** RefusalLineView: the reason on its own line above the hand for 3.5 s (real time); the guide steps aside. */
    showRefusal(text) {
      refusalOwner = (down && down.card) || hovered || (sel && sel.card) || null;
      panels.showRefusal(text);
    },
    hideRefusal() { panels.hideRefusal(); },

    // ---- input paths (battle_ui_ux_v2 §3.4) ----
    /**
     * Key 1–9: selects card `index` (§3.4: another digit moves the selection; Enter plays it). `o.auto`: the scene's own pick
     * after an Enter play (a click on it then keeps it, review 19).
     */
    select(index, o) {
      const c = cards[index];
      if (c && c.mode === 'fan' && triedBlocked(c)) return false;
      if (c && c.mode === 'fan' && triedLocked()) return false;
      if (!c || !canGrab() || c.mode !== 'fan') return false;
      return selectCard(c, !!(o && o.auto));
    },
    /**
     * Enter: plays the selected card on its own zone (DepictionText.RequiredZone), on the frame the screen shows as
     * focused — the one Tab or the pointer last framed, which stays framed (its light, the HP wick, the floor's N) once
     * the pointer moves off (review fix: Enter then went to CardFace.TargetUnit while another enemy stood framed).
     */
    confirm() {
      if (!sel) return false;
      return confirmOn(sel.card.face.Aim === CardAim.Single ? framedOf(sel) : -1);
    },
    /** The fan position of the selected card (−1 for none): §3.4, the card then at that place is selected next. */
    selectedIndex() { return sel ? cards.indexOf(sel.card) : -1; },
    /**
     * Tab (§3.4, two or more enemies): the selected single-target card's focus moves to the next standing
     * enemy's frame, left to right (`dir` −1 goes back); Enter then plays it there. Returns whether it moved.
     */
    cycleTarget(dir) {
      if (!sel || held || sel.card.face.Aim !== CardAim.Single) return false;
      const list = receivers().slice().sort((a, b) => (a.box.X + a.box.Width / 2) - (b.box.X + b.box.Width / 2));
      if (list.length < 2) return false;
      const now = framedOf(sel);
      const i = Math.max(0, list.findIndex((r) => r.unit === now));
      const next = list[(i + (dir < 0 ? list.length - 1 : 1)) % list.length].unit;
      sel.tabbed = true;
      sel.tabAt = { x: ptr.x, y: ptr.y };
      setFramed(sel, next);
      setHot(sel, true, sel.card.face);
      return true;
    },
    /** I (§3.4 / §3.13): the selected card's detail opens over it, or closes. */
    toggleSelectedDetail() {
      if (!sel || !sel.card.face.Detail) return false;
      panels.toggleDetail(sel.card.face, boxOf(sel.card), detailExtra(sel.card));
      return true;
    },
    /** The battle is over (review fix): the hand fades under the result card; 0 ms sets it at once. */
    fadeAll(ms) {
      const from = parseFloat(layer.hand.style.opacity || '1') || 1;
      if (!(ms > 0)) { layer.hand.style.opacity = String(END_HAND_ALPHA); return Promise.resolve(); }
      return clock.run(ms, 'out', (t) => { layer.hand.style.opacity = lerp(from, END_HAND_ALPHA, t).toFixed(3); });
    },
    /** Esc: a held card slides home unplayed, or the selection drops, or the detail closes. */
    cancel() {
      if (held && !held.auto) {
        const c = endHold();
        slideHome(c, false, true);
        return true;
      }
      if (sel) { endSelection(true); return true; }
      if (panels.detailShown) { panels.hideDetail(); return true; }
      return false;
    },
    /** AutoDrag: the suggested card is held, carried 450 ms (inout) onto its zone (enemy `unit`'s receiver), made hot and released there. */
    autoDrag(cardId, zone, unit) {
      const c = cards.find((x) => x.id === cardId);
      if (!c || held || !interactable || busy()) return Promise.resolve();
      beginHold(c, true);
      const from = { x: c.pose.x, y: c.pose.y };
      let to;
      if (c.face.Aim === CardAim.Single) {
        const named = unit != null && unit >= 0 ? unit : defaultUnit(c.face);
        if (held) setFramed(held, named);
        const r = receiverBox(named);
        to = r ? { x: r.X + r.Width / 2, y: r.Y + r.Height / 2 } : from;
      } else to = { x: from.x, y: L.THROW.y - (from.y - L.THROW.y) * 0.35 };
      const mine = held;
      return clock.run(L.AUTO_DRAG_MS, 'inout', (t) => {
        if (held !== mine) return;
        c.pose.x = lerp(from.x, to.x, t);
        c.pose.y = lerp(from.y, to.y, t);
      }).then(() => {
        if (held !== mine) return;
        setHot(held, true, c.face);
        releaseHeld(zone, zone === DropZone.Receiver ? held.unit : -1);
      });
    },
    destroy() {
      destroyed = true;
      offFrame();
      window.removeEventListener('pointerdown', onDown);
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
      window.removeEventListener('pointercancel', onCancel);
      window.removeEventListener('blur', onBlur);
      document.removeEventListener('visibilitychange', onVisibility);
      env.root.removeEventListener('pointerleave', onLeave);
      env.root.removeEventListener('contextmenu', onContext);
      env.root.classList.remove('ocbg-battle--grab', 'ocbg-battle--grabbing');
      marks.destroy();
      panels.destroy();
      cards.forEach((c) => c.wrap.remove());
      leaving.forEach((c) => c.wrap.remove());
      cards = [];
      leaving.clear();
    },
  };

  env.geom.handCard = (cardId) => {
    const c = cards.find((x) => x.id === cardId) || leaving.get(cardId);
    return c ? { x: c.pose.x, y: c.pose.y } : null;
  };

  return api;
}
