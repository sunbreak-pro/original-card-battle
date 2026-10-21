// The hold decorations of the hand module: ports of TargetMarkView.cs (the four corner hooks around the
// figure a held card lands on, with the one predicted value on the top edge, battle-visual-v1 §5.2 / §5.3)
// and ThrowLineView.cs (the amber dashed line at y 790 a self card is released above), plus the zone fade
// of DepictionPlayer.BeginHold (EffectId.ReceiverShow / ThrowLineShow). ReceiverView draws nothing since
// #242: the receiver is only the box ReceiverDrop tests (the field's geom.receiver()).
//
// Which figure is framed and the value come from the script (CardFace.Aim / Affects / ReachHint and the
// source's PreviewFor); this file only places and lights them. Several enemies (scene addition,
// battle-visual-v1 §5.2): a single-target card frames every standing enemy, amber where CardFace.ReachFor(k)
// and steel where not; each frame the card reaches carries its own predicted value on its top edge
// (battle_ui_ux_v2 §2.2.5 「届く相手の枠ごとに、その相手への予測値を上辺に出します」; review fix: the tag stood on
// the framed enemy only). The framed one (setTarget) is lit full, the others at the idle 75 %. Looks: OCB.TargetFrame, OCB.PredTag,
// .ocb-throwline. The hooks are drawn in layer.fx with the figures' effects, under the omen badge and the panels
// (Unity: the mark is the figure's); the tag in layer.float (layout.md 重なりの順), on the frame's top edge — the badge over it
// rises clear of it (review 19, DS tag.lift).
//
// TargetMarkView's light (review fix): a frame the card reaches carries ProceduralArt.Glow in amber at 35 %,
// stretched over the frame plus 10 px and drawn over the figure under the hooks (the mark is a child of the
// figure, its light the first sibling), so the framed enemy stands in warm light. With several enemies only
// the frame the card would go to now (setTarget) is lit; the other reached frames keep their amber hooks.
import { h } from '../common/stage.js';
import { tok, glowGradient } from './field/art.js';
import { UnitSide, CardAim } from '../../bridge/depictionScript/DepictionTypes.js';
import { previewKind, valueIcon, valueColor } from './hand-card.js';

/** TargetMarkView §5.2: the frame 46 px out on the left and right, 22 above, 12 below; hooks 26 long, 3 thick (steel 2). */
const OUT = { x: 46, top: 22, bottom: 12 };
/**
 * Review 21 (two or three enemies on 7–8 cells: the 46 px sides of neighbouring frames overlapped by 13–37 px and their hooks
 * met as one 「TT」): a frame's side toward a standing neighbour comes in to keep this far from the neighbour's frame — each
 * takes half of the room between the two bodies, less half this — and never closer to its body than OUT_MIN.
 */
const FRAME_APART = 12;
const OUT_MIN = 2;
/** CoreText.EffectWord of TraitEffect.HeavyBlow, as the lamp line prints it. */
const HEAVY_WORD = /(^|[\s／→])重撃(\s|$)/;
const HOOK = 26;
const HOOK_W = 3;
/** TargetMarkView: the amber light 10 px past the frame at 35%. */
const GLOW = 10;
const GLOW_ALPHA = 0.35;
/** TargetMarkView.SetHot: 1 when releasing now would play it, 0.75 otherwise (--opacity-idle-hooks). */
const IDLE = 0.75;
/** ThrowLineView.SetHot: the dashes at full amber over the line, 60% under it. */
const LINE_IDLE = 0.6;
/**
 * Several enemies (review fix: a Tab or the pointer that focused an enemy the card cannot reach left only the floor's
 * N to say so — the reached frames kept their amber hooks and tags, and the focused steel hooks barely showed): the
 * focused frame stands full (its steel hooks brighter and 3 px, `.is-focus`), the others step back — their hooks at
 * OTHER_HOOKS, their prediction tags at OTHER_TAG — so the eye goes to the one Enter or a release would play on.
 */
const FOCUS_IDLE = 0.9;
const OTHER_HOOKS = 0.6;
/**
 * battle-visual-v1 §5.2 / DS TargetFrame 「予測値の札は枠の上辺の中央」. Review 19 (pushed down under the omen badge — review
 * 17's fix — the tag stood on the enemy's head and hid the badge's ▼): the tag stands on the middle of the frame's top
 * edge again, and the badge over that frame rises just clear of it (DS tag.lift, hud.js liftOmen / L.TAG_LIFT_GAP), so the
 * omen's kind word and hit mark (A1) stay readable.
 */

export function createMarks(env) {
  const { OCB, layer, clock, L } = env;
  let frames = [];           // [{ side, unit, el, w, h, x, y, tag }]
  let current = null;        // { face, preview(unit), target }
  let line = null;
  let shown = false;
  let hot = false;
  let fade = { a: 0 };       // the zone fade (ReceiverShow / ThrowLineShow)
  let token = 0;

  /** The frame's box around `side`'s figure now (the hooks are a child of the figure in Unity, so they follow it). */
  function boxOf(side, unit) {
    const f = env.geom.figure ? env.geom.figure(side, unit) : null;
    if (!f || f.down) return null;
    let left = OUT.x;
    let right = OUT.x;
    if (side === UnitSide.Enemy && env.geom.receivers) {
      // The standing neighbours' bodies (review 21): each side gives way to keep FRAME_APART from the next frame.
      for (const r of env.geom.receivers()) {
        if (r.unit === unit || r.unit < 0) continue;
        const o = env.geom.figure(UnitSide.Enemy, r.unit);
        if (!o || o.down) continue;
        if (o.x > f.x) {
          const room = (o.x - o.width / 2) - (f.x + f.width / 2);
          right = Math.min(right, Math.max(OUT_MIN, room / 2 - FRAME_APART / 2));
        } else if (o.x < f.x) {
          const room = (f.x - f.width / 2) - (o.x + o.width / 2);
          left = Math.min(left, Math.max(OUT_MIN, room / 2 - FRAME_APART / 2));
        }
      }
    }
    const x = f.x - f.width / 2 - left;
    const w = f.width + left + right;
    const top = f.top - OUT.top;
    return { x, y: top, w, h: f.feetY + OUT.bottom - top, cx: f.x };
  }

  function makeFrame(side, reaches, preview, face, unit) {
    const box = boxOf(side, unit);
    if (!box) return null;
    const fr = { side, unit, reaches, w: 0, h: 0, el: null, light: null, tag: null };
    build(fr, box);
    setTag(fr, reaches ? preview : '', face);
    return fr;
  }
  /** §5.2 予測値: one tag on the middle of the top edge, only when the card reaches and the source gave one. */
  function setTag(fr, preview, face) {
    if (fr.tag) { fr.tag.remove(); fr.tag = null; }
    fr.tagW = 0;
    if (!preview) return;
    const kind = previewKind(face);
    fr.tag = OCB.PredTag(valueIcon(kind), preview, valueColor(kind));
    fr.tag.classList.add('ocbg-battle-pred');
    // battle_ui_ux_v2 §3.1 / §3.6 (c) (review 21): a 重撃 card whose lamp is lit on this enemy carries, beside the value (which
    // holds the +6), 「次の回復 −1」 as its mark — one next-recovery pip, struck through. No words (§3.2: one value, one word).
    if (heavyLitOn(face, fr.unit)) fr.tag.appendChild(recoveryCutMark());
    layer.float.appendChild(fr.tag);
  }
  /** The face's trait is 重撃 (CoreText.TraitLine's effect word on the lamp line) and its lamp is lit on enemy `unit`. */
  function heavyLitOn(face, unit) {
    if (!face || face.Plain || !HEAVY_WORD.test(String(face.TraitText || '') + ' ' + String(face.TraitBottom || ''))) return false;
    if (face.Aim === CardAim.Single && typeof face.LitFor === 'function') return !!face.LitFor(unit != null && unit >= 0 ? unit : face.TargetUnit >= 0 ? face.TargetUnit : 0);
    return !!face.TraitLit;
  }
  /** One next-recovery pip (StaminaTagView's dashed amber dot, 20 px) with a slash across it. */
  function recoveryCutMark() {
    const NS = 'http://www.w3.org/2000/svg';
    const svg = document.createElementNS(NS, 'svg');
    svg.setAttribute('width', '24');
    svg.setAttribute('height', '24');
    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('class', 'ocbg-battle-pred-cut');
    const dot = document.createElementNS(NS, 'circle');
    dot.setAttribute('cx', '12'); dot.setAttribute('cy', '12'); dot.setAttribute('r', '8');
    dot.setAttribute('fill', 'none'); dot.setAttribute('stroke', 'var(--amber)'); dot.setAttribute('stroke-width', '2'); dot.setAttribute('stroke-dasharray', '3 3');
    const cut = document.createElementNS(NS, 'path');
    cut.setAttribute('d', 'M4 20L20 4'); cut.setAttribute('stroke', 'var(--ink)'); cut.setAttribute('stroke-width', '2.4'); cut.setAttribute('stroke-linecap', 'round');
    const title = document.createElementNS(NS, 'title');
    title.textContent = '次の回復 −1'; // scene text (§3.1)
    svg.append(title, dot, cut);
    return svg;
  }

  function build(fr, box) {
    if (fr.el) fr.el.remove();
    fr.w = Math.round(box.w);
    fr.h = Math.round(box.h);
    fr.el = OCB.TargetFrame(fr.w, fr.h, { reachable: fr.reaches, hook: HOOK, width: HOOK_W, glow: GLOW, glowAlpha: GLOW_ALPHA });
    fr.el.classList.add('ocbg-battle-hooks');
    if (!fr.reaches) fr.el.classList.add('is-steel');
    // TargetMarkView.BuildParts / Show(reaches): the light, only on a frame the card reaches (steel: none).
    fr.light = null;
    if (fr.reaches) {
      fr.light = h('div', { class: 'ocbg-battle-hooks-light' });
      fr.light.style.left = fr.light.style.top = -GLOW + 'px';
      fr.light.style.width = fr.w + 2 * GLOW + 'px';
      fr.light.style.height = fr.h + 2 * GLOW + 'px';
      fr.light.style.background = glowGradient((a) => tok('amber', a), GLOW_ALPHA);
      fr.el.insertBefore(fr.light, fr.el.firstChild);
    }
    // Review fix: the hooks belong to the figure (TargetMarkView is the figure's child in Unity), so they are drawn
    // with the figure's effects (layer.fx, over the body), under the panels' layer: the omen badge stands over
    // them, as in Unity's 02-grab-attack-in-frame. Only the prediction tag stands in front (layer.float, DS rule).
    layer.fx.appendChild(fr.el);
  }

  /** The tag's middle y on frame `fr` (box = its box now): the frame's top edge (the badge over it rises for it). */
  function tagY(fr, box) {
    return box.y;
  }

  function place() {
    const a = fade.a;
    for (const fr of frames) {
      const box = boxOf(fr.side, fr.unit);
      if (!box) { fr.el.style.opacity = '0'; if (fr.tag) fr.tag.style.opacity = '0'; continue; }
      if (Math.round(box.w) !== fr.w || Math.round(box.h) !== fr.h) build(fr, box);
      fr.el.style.transform = 'translate(' + box.x.toFixed(1) + 'px,' + box.y.toFixed(1) + 'px)';
      const several = frames.length >= 2;
      const framed = !several || fr.unit === (current && current.target);
      // One frame: TargetMarkView.SetHot (1 / 0.75). Several: the focus stands out (FOCUS_IDLE … OTHER_TAG).
      const op = !several ? a * (hot ? 1 : IDLE) : a * (framed ? (hot ? 1 : FOCUS_IDLE) : OTHER_HOOKS);
      fr.el.style.opacity = op.toFixed(3);
      fr.el.classList.toggle('is-focus', several && framed);
      if (fr.light) fr.light.style.display = framed ? '' : 'none';
      if (fr.tag) {
        fr.tag.style.left = box.cx.toFixed(1) + 'px';
        fr.tag.style.top = tagY(fr, box).toFixed(1) + 'px';
        fr.tag.style.opacity = (several && !framed ? a : op).toFixed(3);
        fr.tag.classList.toggle('is-other', several && !framed);
      }
    }
    if (line) line.style.opacity = (a * (hot ? 1 : LINE_IDLE)).toFixed(3);
  }

  const api = {
    get shown() { return shown; },
    get hot() { return hot; },
    /**
     * BeginHold's zone + ShowTargetMark: a card aimed at one enemy frames the enemy (steel and no number
     * when CardFace.ReachHint says nobody is in reach); a self card shows the throw line and frames the
     * figure CardFace.Affects names. The zone fades in over ReceiverShow / ThrowLineShow (out).
     */
    show(face, preview, target) {
      api.hide();
      shown = true;
      hot = false;
      const previewOf = typeof preview === 'function' ? preview : () => preview || '';
      current = { face, preview: previewOf, target: target != null ? target : 0 };
      const single = face.Aim === CardAim.Single;
      if (single) {
        const units = env.geom.receivers ? env.geom.receivers().map((r) => r.unit) : [];
        if (units.length > 1) {
          // Several enemies: every standing one is framed (ReachFor), each one reached with its own tag.
          for (const u of units) {
            const reaches = face.ReachOn && face.ReachOn.length ? !!face.ReachFor(u) : !face.ReachHint;
            const fr = makeFrame(UnitSide.Enemy, reaches, previewOf(u), face, u);
            if (fr) frames.push(fr);
          }
        } else {
          const u = units.length ? units[0] : undefined;
          const fr = makeFrame(UnitSide.Enemy, !face.ReachHint, previewOf(u), face, u);
          if (fr) frames.push(fr);
        }
      } else {
        line = h('div', { class: 'ocb-throwline ocbg-battle-throw' });
        line.style.left = L.THROW.x0 + 'px';
        line.style.top = (L.THROW.y - 1.5) + 'px';
        line.style.width = (L.THROW.x1 - L.THROW.x0) + 'px';
        layer.marks.appendChild(line);
        if (face.Affects == null) {
          console.error('[Depiction] card ' + face.Id + ' is a throw-line card but the script gave no Affects');
        } else {
          const fr = makeFrame(face.Affects === UnitSide.Player ? UnitSide.Player : UnitSide.Enemy, true, previewOf(undefined), face);
          if (fr) frames.push(fr);
        }
      }
      const ms = env.fx.ms(single ? 'ReceiverShow' : 'ThrowLineShow');
      const my = ++token;
      fade = { a: 0 };
      if (ms > 0) clock.run(ms, 'out', (t) => { if (my === token) fade.a = t; });
      else fade.a = 1;
      place();
    },
    /** Several enemies: the card is framed over enemy `unit` now (its frame and tag are lit full). */
    setTarget(unit) {
      if (!current || current.target === unit) return;
      current.target = unit;
      if (frames.length < 2) return;
      place();
    },
    /**
     * The enemies whose frame carries a prediction tag now (their omen badges stand higher for it): units, or
     * −1 for the one enemy of a battle that names none. A frame on the player is not listed (no badge there).
     */
    taggedUnits() {
      return frames.filter((fr) => fr.tag && fr.side === UnitSide.Enemy).map((fr) => (fr.unit != null && fr.unit >= 0 ? fr.unit : -1));
    },
    /** SetZoneHot: hot = releasing the card now would play it. */
    setHot(on) {
      hot = !!on;
      place();
    },
    /** Release / LetGo: the zone and the hooks go at once. */
    hide() {
      token++;
      shown = false;
      current = null;
      hot = false;
      frames.forEach((fr) => { if (fr.el) fr.el.remove(); if (fr.tag) fr.tag.remove(); });
      frames = [];
      if (line) { line.remove(); line = null; }
    },
    /** The hooks' box around a side's figure (the click path tests a click against it). */
    boxOf,
    /** Per frame: follow the figures, and the fade. */
    update() { if (shown) place(); },
    destroy() { api.hide(); },
  };
  return api;
}

