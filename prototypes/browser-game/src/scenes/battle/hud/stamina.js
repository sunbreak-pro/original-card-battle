// The player's stamina tag above the piles: a port of StaminaTagView (battle-visual-v1 §4.7, #242 / #455) in
// the design system's plate look. x 32, y 846, 236×84: the amber dot and the number (34) on the first row,
// with "▸ n" (wick, 24) on its right while a card is held; one dot per point of the maximum on the second, a
// divider after the stamina 構え needs (DepictionFrame.StanceDivider), the stamina there now in amber, the
// points a held card would pay as amber rings, the stamina the next turn start brings back as amber dashed
// rings (UnitFrame.NextRecovery), the rest as steel rings. It shows the numbers it is handed.
import { h } from '../../common/stage.js';
import { tok, mix, lerp, pop } from './util.js';

// StaminaTagView: At (32, 846), Size (236, 84). (layout.js L.STAMINA holds the same box.)
/** "▸ n": the points a held card would pay (StaminaTagView.Render). scene text */
const PAY_MARK = '▸ ';
/** StaminaTagView: the amber dot before the number, 18×18. */
const DOT = 18;
/** DS 試作 (b): a point lighting swells 1.35 → 1 with an 8 px wick ring, over 320 ms. */
const LIGHT_MS = 320;

export function createStaminaTag(env) {
  const { OCB, clock, layer, L } = env;
  const num = h('b');
  const pay = h('span', { class: 'ocb-st-pay' });
  // StaminaTagView: the dot is ProceduralArt.Circle at a full 18×18 (battle-visual-v1 §4.7 粒 18 px). Review fix:
  // the 付録 A pip icon is a radius-6 circle in its 24 box, so an 18 px icon drew a 9 px dot.
  const dot = h('i', { class: 'ocbg-battle-dot' });
  dot.style.width = DOT + 'px';
  dot.style.height = DOT + 'px';
  dot.style.background = tok('amber');
  const row1 = h('div', { class: 'ocb-st-row1' }, dot, num, pay);
  const pips = h('div', { class: 'ocb-pips' });
  const el = h('div', { class: 'ocb-stamina ocbg-battle-stamina' }, row1, pips);
  el.style.left = L.STAMINA.x + 'px';
  el.style.top = L.STAMINA.y + 'px';
  layer.hud.appendChild(el);

  const st = { stamina: 0, max: 0, cost: 0, next: 0, divider: 0 };
  const dots = [];
  const divider = h('i', { class: 'ocb-pip-div' });

  /** StaminaTagView.Render. */
  function render() {
    num.textContent = String(st.stamina);
    pay.textContent = PAY_MARK + st.cost;
    pay.classList.toggle('is-on', st.cost > 0);
    while (dots.length < st.max) dots.push(h('i', { class: 'ocb-pip' }));
    pips.innerHTML = '';
    const paying = Math.min(st.cost, st.stamina);
    for (let i = 0; i < st.max; i++) {
      // The divider stands in the gap after the stamina 構え needs, only when there are dots beyond it.
      if (st.divider > 0 && i === st.divider) pips.appendChild(divider);
      const dot = dots[i];
      const cls = i < st.stamina - paying ? 'is-have' : i < st.stamina ? 'is-pay' : i < st.stamina + st.next ? 'is-next' : 'is-empty';
      dot.className = 'ocb-pip ' + cls;
      pips.appendChild(dot);
    }
  }

  return {
    el,
    setStamina(stamina, max) {
      st.stamina = Math.max(0, stamina | 0);
      st.max = Math.max(0, max | 0);
      render();
    },
    setDivider(after) {
      const next = Math.max(0, after | 0);
      if (next === st.divider) return;
      st.divider = next;
      render();
    },
    setNextRecovery(next) {
      const value = Math.max(0, next | 0);
      if (value === st.next) return;
      st.next = value;
      render();
    },
    /** §4.7 / §5.2: while a card is held, the points it would pay turn to rings and "▸ n" shows. 0 puts both away. */
    showCost(cost) {
      st.cost = Math.max(0, cost | 0);
      render();
    },
    /**
     * §5.13 順 1 (#501): the dots up to the divider (構え's 息) glow once over `ms` (Ease.Out), swelling 25 %.
     * With no divider nothing glows.
     */
    glowReserve(ms) {
      const count = Math.min(st.divider, st.max);
      const lit = dots.slice(0, count);
      return clock.run(ms, 'out', (t) => {
        const a = 0.9 * Math.sin(t * Math.PI);
        const s = 1 + 0.25 * Math.sin(t * Math.PI);
        const glow = '0 0 ' + Math.round(10 + 6 * a) + 'px ' + Math.round(4 * a) + 'px ' + mix('amber', Math.round(a * 100));
        lit.forEach((d) => { d.style.scale = s.toFixed(3); d.style.boxShadow = glow; });
      }).then(() => lit.forEach((d) => { d.style.scale = ''; d.style.boxShadow = ''; }));
    },
    /** One recovered point lights (DS: a small swell and a wick ring), beside the recovery's own wait. */
    lightPip(index) {
      const dot = dots[index];
      if (!dot) return Promise.resolve();
      return clock.run(LIGHT_MS, 'out', (t) => {
        dot.style.scale = lerp(1.35, 1, t).toFixed(3);
        dot.style.boxShadow = '0 0 0 ' + (8 * t).toFixed(1) + 'px ' + mix('wick', Math.round(90 * (1 - t)));
      }).then(() => { dot.style.scale = ''; dot.style.boxShadow = ''; });
    },
    /** A spend: the number pops (StatusBarView pops the pip row Unity hides; this is the tag's). */
    popNumber(ms) { return pop(clock, num, ms); },
    destroy() { el.remove(); },
  };
}
