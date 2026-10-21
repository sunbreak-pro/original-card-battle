// Shared helpers of the battle scene's hud module (docs/BATTLE_SCENE.md §6c): token colours, Unity's
// UiTween shapes (Pop, Shake) on the scene clock, the icon look-ups the hud's views share, and the one
// tooltip the hud shows at a time. Presentation only: no rule, no number of the game.
import { h } from '../../common/stage.js';
import { CardKind, OmenIcon, StatusGlyph } from '../../../bridge/depictionScript/DepictionTypes.js';
import { PileKind } from '../../../bridge/depictionScript/PileLayout.js';

/** `var(--name)`. */
export const tok = (name) => 'var(--' + name + ')';
/** A token colour at `pct` % (the battle-visual §2.1 alphas: amber 16 %, black 55 % …). */
export const mix = (name, pct) => 'color-mix(in srgb, var(--' + name + ') ' + pct + '%, transparent)';
export const lerp = (a, b, t) => a + (b - a) * t;
export const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);

/** UiTween.Pop's curve: scale 1.2 → 0.8 → 1 over the (eased) t. */
export function popScale(t) {
  return t < 0.5 ? lerp(1.2, 0.8, t * 2) : lerp(0.8, 1, (t - 0.5) * 2);
}

/** UiTween.Pop(rt, ms) on the scene clock (Ease.Out). Uses the CSS `scale` property, so `left/top` stay. */
export function pop(clock, el, ms) {
  if (!el) return clock.wait(ms);
  return clock.run(ms, 'out', (t) => { el.style.scale = popScale(t).toFixed(4); }).then(() => { el.style.scale = ''; });
}

/** UiTween.Shake(rt, amplitude, cycles, ms): a horizontal shake that dies out (Ease.Linear). */
export function shake(clock, el, amplitude, cycles, ms) {
  if (!el) return clock.wait(ms);
  return clock.run(ms, 'linear', (t) => {
    const x = Math.sin(t * Math.PI * 2 * cycles) * amplitude * (1 - t);
    el.style.translate = x.toFixed(2) + 'px 0';
  }).then(() => { el.style.translate = ''; });
}

/** UiTween.Scale(rt, from, to, ms, ease) on the CSS `scale` property. */
export function scaleTo(clock, el, from, to, ms, ease) {
  if (!el) return clock.wait(ms);
  return clock.run(ms, ease || 'out', (t) => { el.style.scale = lerp(from, to, t).toFixed(4); }).then(() => {
    el.style.scale = to === 1 ? '' : String(to);
  });
}

// ---- icon look-ups (the design system's 付録 A icons; colour by role) --------------------------------

/**
 * OmenIconArt.Of + OmenBadgeView.RoleColor: the omen kind's icon and its colour. Attack in phosphor and
 * Guard in the Guard colour (as their numbers), 休み in steel (it is drawn as 空き, DS OMEN_KIND), the rest
 * in body ink. None draws no icon.
 */
export const OMEN_ICON = {
  [OmenIcon.Attack]: { icon: 'sword', color: tok('phosphor') },
  [OmenIcon.Guard]: { icon: 'shield', color: tok('guard') },
  [OmenIcon.Status]: { icon: 'star', color: tok('ink') },
  [OmenIcon.Move]: { icon: 'back', color: tok('ink') },
  [OmenIcon.Stance]: { icon: 'flag', color: tok('ink') },
  [OmenIcon.Rest]: { icon: 'rest', color: tok('steel') },
  [OmenIcon.Seal]: { icon: 'seal', color: tok('ink') },
};

/** OmenBadgeView.RoleColor for the omen's number: attack phosphor, guard Guard, the rest body ink. */
export function omenValueColor(icon) {
  return icon === OmenIcon.Attack ? tok('phosphor') : icon === OmenIcon.Guard ? tok('guard') : tok('ink');
}

/** VisualArt.StatusIcon: the 付録 A icon of a status a 状態付与 omen puts on; None (a boss's word) has none. */
export const GLYPH_ICON = {
  [StatusGlyph.Bleed]: 'bleed',
  [StatusGlyph.Fragile]: 'brittle',
  [StatusGlyph.Slow]: 'slow',
  [StatusGlyph.Intimidate]: 'intimidate',
  [StatusGlyph.Fatigue]: 'fatigue',
};

/** VisualArt.KindIcon / BattleTheme.KindInk / KindFrame: a card attribute's icon, ink and frame colour. */
export const KIND_LOOK = {
  [CardKind.Attack]: { icon: 'sword', ink: tok('phosphor'), frame: tok('violet') },
  [CardKind.Guard]: { icon: 'shield', ink: tok('guard'), frame: tok('guard') },
  [CardKind.Skill]: { icon: 'star', ink: tok('amber'), frame: tok('amber') },
  [CardKind.Stance]: { icon: 'flag', ink: tok('hp-text'), frame: tok('crimson') },
};

/** PilesView.IconOf. */
export const PILE_ICON = { [PileKind.Draw]: 'draw', [PileKind.Discard]: 'discard', [PileKind.Exile]: 'exile' };

let statusIds = null;
/**
 * A status word's 付録 A icon id, looked up by its name in the design system's STATUS table ("出血" →
 * bleed). Presentation only: the word itself is the script's. Null for a word the table does not draw
 * (a boss's own statuses, a stance).
 */
export function statusIconOf(OCB, label) {
  if (!statusIds) {
    statusIds = new Map();
    const table = (OCB && OCB.STATUS) || {};
    for (const id of Object.keys(table)) statusIds.set(table[id].name, { id, self: !!table[id].self });
  }
  return statusIds.get(label) || null;
}

/** The solid / dashed shield of battle-visual §4.5 (Guard > 0 / Guard 0, 構えの盾). */
export function shieldIcon(OCB, size, solid) {
  return solid
    ? OCB.icon('shield', size, tok('guard'), { fill: tok('guard'), fillOpacity: 0.2 })
    : OCB.icon('shield', size, tok('steel'), { dashed: true });
}

// ---- the one tooltip --------------------------------------------------------------------------------

/**
 * The hud's tooltip (the status chips' 説明パネル, the stance frames, the corner buttons): one at a time,
 * in layer.over, never taking the pointer (BattleSpeedButton: raycastTarget false).
 */
export function createTip(env) {
  let el = null;
  let owner = null;
  /**
   * battle_ui_ux_v2 §3.13 (review 21): 「スタンスの一覧（自分 / 敵）の名前と状態チップも右クリックで同じ詳細を出します」 — a right
   * click pins the panel: it stays (a hover elsewhere shows nothing over it) until a click, Esc or another right click.
   */
  let pinned = false;
  const api = {
    /** Shows `node`, then `place(node)` puts it (it is measured in the layer first). */
    show(node, place, by) {
      // Not while a card is held (the hand marks the root): the pointer is carrying the card, not reading.
      if (env.root && env.root.classList.contains('ocbg-battle--grabbing')) return;
      if (pinned) return;
      if (el) el.remove();
      el = node;
      owner = by || null;
      el.classList.add('ocbg-battle-tip');
      env.layer.over.appendChild(el);
      if (place) place(el);
    },
    hide(by) {
      if (!el || pinned) return;
      if (by && owner && by !== owner) return;
      el.remove();
      el = null;
      owner = null;
    },
    /** The right click's detail: `node` stays until unpin(); the same owner's right click again closes it (toggle). */
    pin(node, place, by) {
      if (pinned && owner === (by || null)) { api.unpin(); return false; }
      pinned = false;
      api.show(node, place, by);
      if (!el) return false;
      pinned = true;
      el.classList.add('is-pinned');
      return true;
    },
    /** Closes a pinned detail; returns whether one was open. */
    unpin() {
      if (!pinned) return false;
      pinned = false;
      if (el) el.remove();
      el = null;
      owner = null;
      return true;
    },
    get pinned() { return pinned; },
    get shown() { return !!el; },
  };
  return api;
}

/** A plain one-line tooltip (BattleSpeedButton.BuildTooltip: opaque panel, dull-steel frame, 16 px ink). */
export function lineTip(text) {
  return h('div', { class: 'ocbg-battle-linetip' }, h('span', null, text));
}
