// The lower right of the battle screen: ports of BottomRightViews (PlayedAttributesView, ReserveHintView;
// battle-visual-v1 §4.9, battle_ui_ux_v2 §1.1 B6 / §3.6 (f), #455) and of DepictionPlayer's end-turn plate
// (StyleEndTurn, ShowEnemyTurn's plate half, Reserve's 200 ms pop), in the design system's looks.
//
// - Played attributes: four 40×40 frames 6 px apart, 24 px in from the right at y 896, for 攻撃・防御・スキル・
//   スタンス. An attribute played this turn lights its icon in the attribute's colour and carries a 20 px count
//   badge; one not played is steel at 55 %. The attribute of the card played last is underlined. While a 追撃
//   waits, a fifth frame left of them reads "+5" in phosphor (it pops in 150 ms and fades in 150 ms).
// - The 構えの予告 at x 1560, y 984, 56 tall: a Guard-coloured 1 px dashed frame, a 30 px shield and "+3".
// - The end-turn plate at x 1672, y 976, 224×72: amber 2 px frame over amber 16 %, its label at 24 px; 40 %
//   while the enemy has the turn (EnemyTurnShadeLayout.EndTurnAlpha).
import { h } from '../../common/stage.js';
import { PlayedLayout, ReserveHintLayout } from '../../../bridge/depictionScript/BottomRightLayout.js';
import { EnemyTurnShadeLayout } from '../../../bridge/depictionScript/EnemyTurnShadeLayout.js';
import { KIND_LOOK, tok, pop, scaleTo, shake, lineTip } from './util.js';
import { CardFaceText } from '../../../bridge/depiction/CardFaceText.js';

/**
 * Scene addition (review: the +3 beside ターン終了 answered no pointer): the hint's hover line names the rule it
 * shows — the glossary's 構え (CardFaceText.Glossary, the 用語 page's words) — and that ending the turn now gives it.
 */
export const ReserveTip = () => `構え：${CardFaceText.Glossary.get('構え')}。いまターンを終えると付く`;

/** The plate's label (the Unity prefab's Text; DS EndTurnButton). scene text */
export const END_TURN_LABEL = 'ターン終了';

export function createPlayed(env) {
  const { OCB, clock, layer } = env;
  const slots = PlayedLayout.Order.map((kind, i) => {
    const look = KIND_LOOK[kind];
    const iconBox = h('span', { class: 'ocbg-battle-attr-icon' });
    const badge = h('span', { class: 'ocb-count ocbg-battle-attr-count' });
    const underline = h('i', { class: 'ocbg-battle-attr-last' });
    const el = h('div', { class: 'ocb-panel ocb-attr ocbg-battle-attr' }, iconBox, badge, underline);
    el.style.left = PlayedLayout.LeftOf(i) + 'px';
    el.style.top = PlayedLayout.Top + 'px';
    badge.style.borderColor = look.frame;
    underline.style.background = look.frame;
    layer.hud.appendChild(el);
    return { kind, look, el, iconBox, badge, underline, lit: null };
  });
  const followText = h('b');
  const follow = h('div', { class: 'ocb-panel ocbg-battle-followup' }, followText);
  // It hangs from its right edge, so a wider number grows away from the row (PlayedLayout.FollowUpRight).
  follow.style.right = env.L.W - PlayedLayout.FollowUpRight + 'px';
  follow.style.top = PlayedLayout.Top + 'px';
  follow.style.display = 'none';
  layer.hud.appendChild(follow);
  let followShown = 0;
  let followToken = 0;
  let shownAll = false;

  function bindFollowUp(n, animate) {
    const before = followShown;
    followShown = n;
    if (n === before) return;
    const token = ++followToken;
    if (n > 0) {
      followText.textContent = PlayedLayout.FollowUpLabel(n);
      follow.style.display = '';
      follow.style.opacity = '1';
      follow.style.scale = '';
      if (animate && before <= 0) scaleTo(clock, follow, 0.6, 1, PlayedLayout.FollowUpPopMs, 'out');
      return;
    }
    if (!animate || follow.style.display === 'none') { follow.style.display = 'none'; return; }
    clock.run(PlayedLayout.FollowUpVanishMs, 'linear', (t) => { if (token === followToken) follow.style.opacity = (1 - t).toFixed(3); })
      .then(() => { if (token === followToken && followShown <= 0) follow.style.display = 'none'; });
  }

  return {
    /** PlayedAttributesView.Bind. `animate` false on a battle's opening frame. */
    bind(played, animate) {
      shownAll = !!(played && played.Shown);
      slots.forEach((s) => { s.el.style.display = shownAll ? '' : 'none'; });
      bindFollowUp(shownAll ? played.FollowUp | 0 : 0, animate);
      if (!shownAll) return;
      for (const s of slots) {
        const count = played.CountOf(s.kind);
        const lit = count > 0;
        if (s.lit !== lit) {
          s.lit = lit;
          s.iconBox.innerHTML = '';
          s.iconBox.appendChild(OCB.icon(s.look.icon, PlayedLayout.IconSize, lit ? s.look.ink : tok('steel')));
          s.el.classList.toggle('is-on', lit);
        }
        s.badge.style.display = lit ? '' : 'none';
        s.badge.textContent = String(count);
        s.underline.style.display = lit && played.Last === s.kind ? '' : 'none';
      }
    },
    destroy() { slots.forEach((s) => s.el.remove()); follow.remove(); },
  };
}

export function createReserveHint(env, tip = null) {
  const { OCB, layer } = env;
  const text = h('b');
  const el = h('div', { class: 'ocbg-battle-hint' + (tip ? ' ocbg-battle-hit' : '') }, OCB.icon('shield', ReserveHintLayout.ShieldSize, tok('guard'), { fill: tok('guard'), fillOpacity: 0.2 }), text);
  const self = {};
  if (tip) {
    // the line opens above the hint, right edges aligned (the end-turn plate and the screen's edge are below and right)
    el.addEventListener('mouseenter', () => tip.show(lineTip(ReserveTip()), (n) => {
      n.style.left = (ReserveHintLayout.Left + ReserveHintLayout.Width - n.offsetWidth) + 'px';
      n.style.top = (ReserveHintLayout.Top - n.offsetHeight - 8) + 'px';
    }, self));
    el.addEventListener('mouseleave', () => tip.hide(self));
  }
  el.style.left = ReserveHintLayout.Left + 'px';
  el.style.top = ReserveHintLayout.Top + 'px';
  el.style.width = ReserveHintLayout.Width + 'px';
  el.style.height = ReserveHintLayout.Height + 'px';
  el.style.display = 'none';
  layer.hud.appendChild(el);
  return {
    /** ReserveHintView.Show: "+3" with its shield, or nothing for "". */
    show(hint) {
      const on = !!hint;
      el.style.display = on ? '' : 'none';
      if (on) text.textContent = hint;
      else if (tip) tip.hide(self);
    },
    destroy() { if (tip) tip.hide(self); el.remove(); },
  };
}

/** The end-turn plate's one brightening (§3.5): how much brighter at its peak, and its amber light's reach. */
const FLASH_BRIGHT = 0.6;
const FLASH_GLOW = 24;

export function createEndTurn(env, onClick) {
  const { clock, layer, L } = env;
  const el = h('div', { class: 'ocb-endturn ocbg-battle-endturn ocbg-battle-hit', role: 'button' }, END_TURN_LABEL);
  el.style.left = L.END_TURN.x + 'px';
  el.style.top = L.END_TURN.y + 'px';
  el.addEventListener('click', () => { if (onClick && !over) onClick(); });
  layer.hud.appendChild(el);
  let enemyTurn = false;
  let over = false;
  function draw() {
    const off = enemyTurn || over;
    el.style.opacity = off ? String(EnemyTurnShadeLayout.EndTurnAlpha) : '';
    el.classList.toggle('is-off', off);
    el.classList.toggle('is-over', over);
  }
  return {
    el,
    /** ShowEnemyTurn's plate half: 40 % while the enemy acts. */
    setEnemyTurn(on) {
      enemyTurn = !!on;
      draw();
    },
    /** The battle is over (review fix): the plate is off for good, at the enemy turn's 40 %. */
    setOff(on) {
      over = !!on;
      draw();
    },
    /** Reserve: UiTween.Pop(endTurn, ms). */
    pop(ms) { return pop(clock, el, ms); },
    /**
     * battle_ui_ux_v2 §3.5 「出せる札が 1 枚も無い … ターン終了ボタンが 1 回だけ明るくなる」 (review 16; Unity changes only the
     * guide): the plate brightens once and settles over `ms` (out), an amber light around it.
     */
    flash(ms) {
      if (over || enemyTurn) return Promise.resolve();
      return clock.run(ms, 'out', (t) => {
        const k = Math.sin(t * Math.PI);
        el.style.filter = t < 1 ? 'brightness(' + (1 + FLASH_BRIGHT * k).toFixed(3) + ')' : '';
        el.style.boxShadow = t < 1 ? '0 0 ' + Math.round(FLASH_GLOW * k) + 'px color-mix(in srgb, var(--amber) ' + Math.round(70 * k) + '%, transparent)' : '';
      });
    },
    /**
     * Review 21: a press the mash window swallowed (player.requestEndTurn) is seen — the plate shakes 6 px twice over `ms`,
     * as a refused card does (ReturnToHand's shake), and dims for that moment.
     */
    refuse(ms) {
      if (over || enemyTurn) return Promise.resolve();
      el.style.filter = 'brightness(0.75)';
      return shake(clock, el, 6, 2, ms).then(() => { el.style.filter = ''; });
    },
    box() { return { x: L.END_TURN.x, y: L.END_TURN.y, w: L.END_TURN.w, h: L.END_TURN.h }; },
    destroy() { el.remove(); },
  };
}
