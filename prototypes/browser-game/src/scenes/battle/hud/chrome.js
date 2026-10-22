// The battle screen's chrome: the corner tag (CornerInfoView, battle_ui_ux_v2 §10.2), the speed switch
// (BattleSpeedButton, #348 / #389), the journal button (battle-visual-v1 §4.1), the turn banners
// (DepictionPlayer.Banner) and the result card (DepictionPlayer.ShowResult / BuildResultCard), in the design
// system's looks. Boxes come from CornerLayout; the words from BattleSpeed, DepictionText and the frame.
import { h } from '../../common/stage.js';
import { EnemyTurnShadeLayout } from '../../../bridge/depictionScript/EnemyTurnShadeLayout.js';
import { CornerLayout } from '../../../bridge/depictionScript/CornerLayout.js';
import { BattleSpeed } from '../../../bridge/depictionScript/BattleSpeed.js';
import { BattleOutcome, DepictionText } from '../../../bridge/depictionScript/DepictionTypes.js';
import { ExplorationText } from '../../../bridge/explorationScript/ExplorationText.js';
import { Miasma } from '../../../core/dungeon/Miasma.js';
import { tok, lineTip } from './util.js';

/**
 * Scene addition (review: the corner tag answered no pointer): what each of the corner's items is, on hover, in a
 * line under the tag. The numbers are the frame's and the app's; the miasma's effect is Miasma's (concept-v3 §6).
 */
export const CornerTips = Object.freeze({
  Turn: (n) => `ターン ${n}`,
  Layer: (n) => `第 ${n} 層`,
  Chain: (index, total) => `連戦：${total} 戦のうち ${index} 戦目`,
  Miasma: (pct) => `瘴気 ${pct}%：探索で溜まった瘴気。${Miasma.StepPercent}% ごとに最大スタミナ −1（−${Miasma.MaxPenalty} まで）`,
});

/** The journal button's tooltip: the DS JournalButton's title and its key (battle_ui_ux_v2 §3.4 J). scene text */
export const JOURNAL_TIP = '手記（J）';

/**
 * CornerInfoView: turn, floor (the exploration's layer: the writer always writes 1), the chain dots and the miasma %.
 * One small line at x 24, y 20, 44 tall. The chain is one 12 px circle per fight of the chain (battle-visual-v1 §4.1),
 * unboxed like the other items. Review 19: 「済んだ戦いを塗る」 — only the fights already won are filled (i < the index of
 * the one being fought, 1-based), so the first of three shows ○○○ and a dot fills as the next fight comes in (§5.18 順 7
 * 「連戦の点が 1 つ進む」); Unity's CornerInfoView fills the one being fought too (i ≤ ChainIndex).
 */
const CHAIN_DOT = 12;
export function createCorner(env, chainOpt, tip = null) {
  const { OCB, layer } = env;
  const turn = h('b', { class: 'ocb-mono' });
  const floor = h('b', { class: 'ocb-mono' });
  const miasma = h('b', { class: 'ocb-mono' });
  const chainItem = h('span', { class: 'ocbg-battle-corner-chain' });
  const turnItem = h('span', null, OCB.icon('hourglass', 22, tok('ink')), turn);
  const floorItem = h('span', null, OCB.icon('stairs', 22, tok('ink')), floor);
  const miasmaItem = h('span', null, OCB.icon('miasma', 22, tok('phosphor')), miasma);
  const el = h('div', { class: 'ocb-panel ocb-corner ocbg-battle-corner' }, turnItem, floorItem, chainItem, miasmaItem);
  // each item says what it is on hover (CornerTips), in a line under the tag, left edge on the item's
  const words = { turn: '', floor: '', chain: '', miasma: '' };
  const self = {};
  const hover = (item, key) => {
    if (!tip) return;
    item.classList.add('ocbg-battle-hit', 'ocbg-battle-corner-item');
    item.addEventListener('mouseenter', () => {
      if (!words[key]) return;
      tip.show(lineTip(words[key]), (n) => {
        n.style.left = (el.offsetLeft + item.offsetLeft) + 'px';
        n.style.top = (el.offsetTop + el.offsetHeight + CornerLayout.SpeedTooltipGap) + 'px';
      }, self);
    });
    item.addEventListener('mouseleave', () => tip.hide(self));
  };
  hover(turnItem, 'turn');
  hover(floorItem, 'floor');
  hover(chainItem, 'chain');
  hover(miasmaItem, 'miasma');
  el.style.left = '24px';
  el.style.top = CornerLayout.CornerInfoTop + 'px';
  el.style.height = CornerLayout.CornerInfoHeight + 'px';
  layer.hud.appendChild(el);
  let chainKey = '';
  return {
    el,
    bind(corner) {
      const c = corner || {};
      turn.textContent = String(c.Turn != null ? c.Turn : 0);
      floor.textContent = String(env.layerNo);
      // The chain: the app's (ctx.chain), else the frame's. ● for the fights won (i < the one being fought), ○ for the rest.
      const index = chainOpt && chainOpt.total ? chainOpt.index | 0 : c.ChainIndex | 0;
      const total = chainOpt && chainOpt.total ? chainOpt.total | 0 : c.ChainTotal | 0;
      chainItem.style.display = total > 0 ? '' : 'none';
      const key = index + '/' + total;
      if (key !== chainKey) {
        chainKey = key;
        chainItem.innerHTML = '';
        for (let i = 1; i <= total; i++) {
          const dot = h('i', { class: 'ocbg-battle-chain-dot' + (i < index ? ' is-done' : '') });
          dot.style.width = dot.style.height = CHAIN_DOT + 'px';
          chainItem.appendChild(dot);
        }
        chainItem.dataset.chain = key;
      }
      const pct = env.miasmaPercent != null ? env.miasmaPercent : c.MiasmaPercent || 0;
      miasma.textContent = pct + '%';
      words.turn = CornerTips.Turn(turn.textContent);
      words.floor = CornerTips.Layer(env.layerNo);
      words.chain = total > 0 ? CornerTips.Chain(index, total) : '';
      words.miasma = CornerTips.Miasma(pct);
    },
    destroy() { if (tip) tip.hide(self); el.remove(); },
  };
}

/** A tooltip 8 px under a corner button, right edges aligned (CornerLayout.SpeedTooltip). */
function placeUnder(node, box) {
  const textW = Math.ceil(node.firstChild ? node.firstChild.offsetWidth || node.scrollWidth : 0);
  const textH = Math.ceil(node.firstChild ? node.firstChild.offsetHeight || 22 : 22);
  const tipBox = CornerLayout.SpeedTooltip(textW, textH);
  const right = box.X + box.Width;
  node.style.left = right - tipBox.Width + 'px';
  node.style.top = box.Y + box.Height + CornerLayout.SpeedTooltipGap + 'px';
  node.style.width = tipBox.Width + 'px';
  node.style.height = tipBox.Height + 'px';
}

function cornerTip(text) {
  return lineTip(text);
}

/**
 * BattleSpeedButton: top right, 12 left of the journal button, 120×56. The chevrons (1–3) and the number
 * ("×1.25"), no word. Hovering lights the frame, washes the face and opens the tooltip. Never dimmed: it
 * stays pressable on the enemy's turn.
 */
export function createSpeedButton(env, onSpeed, tip) {
  const { OCB, layer } = env;
  const box = CornerLayout.SpeedButton;
  const iconBox = h('span', { class: 'ocbg-battle-speed-icon' });
  const label = h('span', { class: 'ocbg-battle-speed-label' });
  const el = h('div', { class: 'ocb-panel ocbg-battle-speed ocbg-battle-hit', role: 'button' }, iconBox, label);
  el.style.left = box.X + 'px';
  el.style.top = box.Y + 'px';
  el.style.width = box.Width + 'px';
  el.style.height = box.Height + 'px';
  el.style.paddingLeft = CornerLayout.SpeedPadding + 'px';
  el.style.gap = CornerLayout.SpeedIconGap + 'px';
  el.addEventListener('click', () => { if (onSpeed) onSpeed(); });
  const self = {};
  el.addEventListener('mouseenter', () => tip.show(cornerTip(BattleSpeed.Tooltip), (n) => placeUnder(n, box), self));
  el.addEventListener('mouseleave', () => tip.hide(self));
  layer.hud.appendChild(el);
  return {
    /** BattleSpeedButton.Show: the number (BattleSpeed.Label) and the chevrons (BattleSpeed.Chevrons). */
    show(text, chevrons) {
      const n = Math.max(1, Math.min(3, chevrons | 0 || 2));
      iconBox.innerHTML = '';
      iconBox.appendChild(OCB.icon('speed' + n, CornerLayout.SpeedIconSize, tok('ink')));
      label.textContent = text || '';
    },
    /** Hidden mid-hover must not come back lit. */
    setVisible(on) {
      if (!on) tip.hide(self);
      el.style.display = on ? '' : 'none';
    },
    destroy() { tip.hide(self); el.remove(); },
  };
}

/** The journal button (battle-visual-v1 §4.1): 24 from the right, y 20, 56×56, the book at 30 px. */
export function createJournalButton(env, onJournal, tip) {
  const { OCB, layer } = env;
  const box = CornerLayout.JournalButton;
  const el = OCB.JournalButton();
  el.removeAttribute('title');
  el.classList.add('ocbg-battle-journalbtn', 'ocbg-battle-hit');
  el.style.position = 'absolute';
  el.style.left = box.X + 'px';
  el.style.top = box.Y + 'px';
  let off = false;
  el.addEventListener('click', () => { if (onJournal && !off) onJournal(); });
  const self = {};
  el.addEventListener('mouseenter', () => { if (!off) tip.show(cornerTip(JOURNAL_TIP), (n) => placeUnder(n, box), self); });
  el.addEventListener('mouseleave', () => tip.hide(self));
  layer.hud.appendChild(el);
  return {
    /**
     * The battle is over (review fix: the book stayed lit over the result, and a click on it moved past the result
     * instead of opening the journal): off with the end-turn plate, at its 40 % (EnemyTurnShadeLayout.EndTurnAlpha),
     * taking no click of its own (a click there moves the result on, as anywhere on the screen).
     */
    setOff(on) {
      off = !!on;
      if (off) tip.hide(self);
      el.style.opacity = off ? String(EnemyTurnShadeLayout.EndTurnAlpha) : '';
      el.style.pointerEvents = off ? 'none' : '';
      el.classList.toggle('is-off', off);
    },
    destroy() { tip.hide(self); el.remove(); },
  };
}

/**
 * Scene addition (review fix: the brief's 「Esc / gear」; Unity's battle has no gear, so a mouse or touch player
 * could not reach 演出短縮, the volume or 保存 in a fight): a 56×56 button in the journal button's look, 12 px left
 * of the speed switch, with a gear in 付録 A's line style (the exploration screen's gear, src/scenes/explore/icons.js)
 * and the exploration's own words for it (ExplorationText.SettingsButton, 設定（Esc）) as its tooltip.
 */
const GEAR_PATH = 'M12 8.6a3.4 3.4 0 110 6.8a3.4 3.4 0 010-6.8zM12 2.5v3M12 18.5v3M2.5 12h3M18.5 12h3M5.3 5.3l2.1 2.1M16.6 16.6l2.1 2.1M5.3 18.7l2.1-2.1M16.6 7.4l2.1-2.1';
export const SETTINGS_TIP = ExplorationText.SettingsButton;
export const SETTINGS_BOX = Object.freeze({
  X: CornerLayout.SpeedButton.X - CornerLayout.JournalGap - CornerLayout.JournalButton.Width,
  Y: CornerLayout.JournalButton.Y,
  Width: CornerLayout.JournalButton.Width,
  Height: CornerLayout.JournalButton.Height,
});

function gearIcon(size) {
  const NS = 'http://www.w3.org/2000/svg';
  const s = document.createElementNS(NS, 'svg');
  s.setAttribute('width', String(size));
  s.setAttribute('height', String(size));
  s.setAttribute('viewBox', '0 0 24 24');
  s.setAttribute('fill', 'none');
  s.setAttribute('stroke', tok('ink'));
  s.setAttribute('stroke-width', '2.2');
  s.setAttribute('stroke-linecap', 'round');
  s.setAttribute('stroke-linejoin', 'round');
  s.setAttribute('class', 'ocb-icon');
  const p = document.createElementNS(NS, 'path');
  p.setAttribute('d', GEAR_PATH);
  s.appendChild(p);
  return s;
}

export function createSettingsButton(env, onSettings, tip) {
  const { layer } = env;
  const box = SETTINGS_BOX;
  const el = h('div', { class: 'ocb-panel ocb-btn-sq ocbg-battle-journalbtn ocbg-battle-settingsbtn ocbg-battle-hit', role: 'button' }, gearIcon(30));
  el.style.position = 'absolute';
  el.style.left = box.X + 'px';
  el.style.top = box.Y + 'px';
  el.addEventListener('click', () => { env.audio.play('uiClick'); if (onSettings) onSettings(); });
  const self = {};
  el.addEventListener('mouseenter', () => tip.show(cornerTip(SETTINGS_TIP), (n) => placeUnder(n, box), self));
  el.addEventListener('mouseleave', () => tip.hide(self));
  layer.hud.appendChild(el);
  return {
    el,
    setVisible(on) { if (!on) tip.hide(self); el.style.display = on ? '' : 'none'; },
    destroy() { tip.hide(self); el.remove(); },
  };
}

/**
 * DepictionPlayer.Banner: a band across the arena with one line on it, faded in over the first quarter and
 * out over the last (Ease.Linear) of `ms`. The DS banner: y 400, 132 tall, the display face at 64 px, amber
 * rules for the player's turn and violet for the enemy's.
 */
export function playBanner(env, text, side, ms) {
  const { OCB, clock, layer, L } = env;
  const el = OCB.Banner(text, side === 'enemy');
  el.classList.add('ocbg-battle-banner');
  el.style.top = L.BANNER.y + 'px';
  el.style.opacity = '0';
  layer.float.appendChild(el);
  return clock.run(ms, 'linear', (t) => {
    el.style.opacity = (t < 0.25 ? t / 0.25 : t > 0.75 ? (1 - t) / 0.25 : 1).toFixed(3);
  }).then(() => el.remove());
}

/**
 * The end of the battle (review 21: a 520×220 box holding one word read as a debug overlay, and a loss put 「負け」 and
 * 「力尽きた」 on the screen together). In the design system's end-screen look (bundle.css .ocb-end / .ocb-end-paper: the
 * board under a scrim of the ground at 62 %, washi paper, washi ink):
 * - won: the paper (720 wide) comes in scaling 0.94 → 1 (battle_ui_ux_v2 §5.18 順 2, out) with the outcome's word
 *   (DepictionText.OutcomeText, 勝ち) and what the battle left: whom it beat, the turns and the HP kept (the DS prototype's
 *   end card), the chain's fight when there is one;
 * - lost: §2.5 / §5.19 順 5〜7's result screen, 922×626 at x 26〜74 %, y 21〜79 %, rising from below (400, out) over the
 *   ink and 「力尽きた」 (which fades as it comes): where the hero fell, 残 HP, the cards played by attribute as four bars
 *   (the attribute colours; the values at once, the bars growing over 400), the traits fired, the turns, and 「手記へ」.
 *   Its items come in from the top, 120 apart.
 * `info` = { title, lines[], bars[{ label, value, color }], stats[], button } — scene text built by the player from the frame
 * and its own count of the battle. Nominal ms on the scene clock.
 */
const RESULT_PAPER = Object.freeze({
  won: { w: 720 },
  lost: { w: 922, h: 626 },
  rise: 80, itemStagger: 120, barMs: 400, itemMs: 200, scaleFrom: 0.94,
});
export function createResultCard(env) {
  const { layer, clock } = env;
  let el = null;
  let onButton = null;
  function build(outcome, info) {
    if (el) el.remove();
    const won = outcome === BattleOutcome.Won;
    const i = info || {};
    const items = [];
    const paper = h('div', { class: 'ocb-end-paper ocbg-battle-result-paper ' + (won ? 'is-won' : 'is-lost') });
    if (won) {
      paper.style.width = RESULT_PAPER.won.w + 'px';
      paper.appendChild(h('h2', null, DepictionText.OutcomeText(outcome)));
      (i.lines || []).forEach((t) => paper.appendChild(h('p', null, t)));
    } else {
      paper.style.width = RESULT_PAPER.lost.w + 'px';
      paper.style.minHeight = RESULT_PAPER.lost.h + 'px';
      const title = h('h2', { class: 'ocbg-battle-result-title' }, i.title || DepictionText.OutcomeText(outcome));
      paper.appendChild(title);
      items.push(title);
      (i.lines || []).forEach((t) => { const p = h('p', { class: 'ocbg-battle-result-line' }, t); paper.appendChild(p); items.push(p); });
      if (i.bars && i.bars.length) {
        const most = Math.max(1, ...i.bars.map((b) => b.value | 0));
        const box = h('div', { class: 'ocbg-battle-result-bars' });
        if (i.barsLabel) box.appendChild(h('div', { class: 'ocbg-battle-result-bars-head' }, i.barsLabel));
        i.bars.forEach((b) => {
          const fill = h('i', { class: 'ocbg-battle-result-bar-fill', style: { background: b.color } });
          fill.dataset.to = String(((b.value | 0) / most) * 100);
          box.appendChild(h('div', { class: 'ocbg-battle-result-bar' },
            h('span', null, b.label), h('div', { class: 'ocbg-battle-result-bar-track' }, fill), h('b', { class: 'ocb-mono' }, String(b.value | 0))));
        });
        paper.appendChild(box);
        items.push(box);
      }
      (i.stats || []).forEach((t) => { const p = h('p', { class: 'ocbg-battle-result-line' }, t); paper.appendChild(p); items.push(p); });
      if (i.button) {
        const btn = h('button', { type: 'button', class: 'ocbg-battle-result-button ocbg-battle-hit' }, i.button);
        btn.addEventListener('click', () => { if (onButton) onButton(); });
        paper.appendChild(btn);
        items.push(btn);
      }
    }
    el = h('div', { class: 'ocb-end ocbg-battle-result ' + (won ? 'is-won' : 'is-lost') }, paper);
    el.style.opacity = '0';
    layer.over.appendChild(el);
    return { el, paper, items, won };
  }
  function settleItems(b) {
    b.items.forEach((x) => { x.style.opacity = ''; });
    b.paper.querySelectorAll('.ocbg-battle-result-bar-fill').forEach((f) => { f.style.width = f.dataset.to + '%'; });
  }
  return {
    /** The paper comes in over `ms` (§5.18 順 2's 300 for a win; §5.19 順 5's 400 for a loss, then its items). */
    show(outcome, ms, info, o) {
      onButton = o && o.onButton ? o.onButton : null;
      const b = build(outcome, info);
      if (!(ms > 0)) { el.style.opacity = '1'; settleItems(b); return Promise.resolve(); }
      b.items.forEach((x) => { x.style.opacity = '0'; });
      b.paper.querySelectorAll('.ocbg-battle-result-bar-fill').forEach((f) => { f.style.width = '0%'; });
      const enter = clock.run(ms, 'out', (t) => {
        el.style.opacity = Math.min(1, t * 2).toFixed(3);
        b.paper.style.transform = b.won
          ? 'scale(' + (RESULT_PAPER.scaleFrom + (1 - RESULT_PAPER.scaleFrom) * t).toFixed(4) + ')'
          : 'translateY(' + (RESULT_PAPER.rise * (1 - t)).toFixed(1) + 'px)';
      }).then(() => { b.paper.style.transform = ''; el.style.opacity = '1'; });
      if (b.won) return enter.then(() => settleItems(b));
      // §5.19 順 6: the items from the top, 120 apart; the bars grow over 400 (their values stand at once).
      const runs = b.items.map((x, k) => clock.wait(ms * 0.5 + k * RESULT_PAPER.itemStagger).then(() => clock.run(RESULT_PAPER.itemMs, 'out', (t) => { x.style.opacity = t.toFixed(3); })));
      const bars = clock.wait(ms * 0.5 + RESULT_PAPER.itemStagger * 2).then(() => clock.run(RESULT_PAPER.barMs, 'out', (t) => {
        b.paper.querySelectorAll('.ocbg-battle-result-bar-fill').forEach((f) => { f.style.width = (Number(f.dataset.to) * t).toFixed(2) + '%'; });
      }));
      return Promise.all([enter, bars, ...runs]).then(() => settleItems(b));
    },
    /** At once. */
    now(outcome, info, o) {
      onButton = o && o.onButton ? o.onButton : null;
      const b = build(outcome, info);
      el.style.opacity = '1';
      settleItems(b);
    },
    destroy() { if (el) el.remove(); el = null; onButton = null; },
  };
}

/**
 * battle_ui_ux_v2 §2.5 切替バナー / §5.18 順 6〜7 (review 21: in a chain the next fight cut straight to the full board): the
 * board stands dark (the end of the fight before left it so), 「n 戦目」 and the next enemy's name come in at the middle
 * (300, out), stand, and go as the board comes up again (400). Scene text from ctx.chain and the roster's names. Nominal ms.
 */
const CHAIN_BANNER = Object.freeze({ inMs: 300, holdMs: 700, outMs: 400 });
export function createChainBanner(env) {
  const { layer, clock } = env;
  let el = null;
  return {
    /** Plays the switch: resolves once the board is up again. `ms` 0 (演出短縮 / effects off) shows nothing. */
    play(title, name, scale = 1, onOut) {
      if (el) el.remove();
      if (!(scale > 0)) { if (onOut) onOut(); return Promise.resolve(); }
      const panel = h('div', { class: 'ocbg-battle-chainbanner-band' },
        h('b', null, title), name ? h('span', null, name) : null);
      el = h('div', { class: 'ocbg-battle-chainbanner' }, panel);
      layer.over.appendChild(el);
      el.style.opacity = '1';
      panel.style.opacity = '0';
      return clock.run(CHAIN_BANNER.inMs, 'out', (t) => { panel.style.opacity = t.toFixed(3); panel.style.translate = '0 ' + (12 * (1 - t)).toFixed(1) + 'px'; })
        .then(() => clock.wait(CHAIN_BANNER.holdMs))
        .then(() => { if (onOut) onOut(CHAIN_BANNER.outMs); return clock.run(CHAIN_BANNER.outMs, 'in', (t) => { if (el) el.style.opacity = (1 - t).toFixed(3); }); })
        .then(() => { if (el) el.remove(); el = null; });
    },
    destroy() { if (el) el.remove(); el = null; },
  };
}

/**
 * battle_ui_ux_v2 §5.19 順 2〜3 (review 19; scene addition — Unity goes from the hero's fall to 負け): a lost battle's ink
 * (flood) rises from the bottom of the screen over `inkMs` (in), then 「力尽きた」 comes in over `wordMs` (out) in the display
 * face at 64 px, its letters drawing apart. In layer.over, under the result card that follows.
 */
export function createLossInk(env) {
  const { layer, clock } = env;
  let el = null;
  return {
    show(word, inkMs, wordMs) {
      if (el) el.remove();
      const fill = h('div', { class: 'ocbg-battle-ink-fill' });
      const text = h('div', { class: 'ocbg-battle-ink-word' }, word || '');
      el = h('div', { class: 'ocbg-battle-ink' }, fill, text);
      layer.over.appendChild(el);
      const travel = 1240;
      const place = (t) => { fill.style.top = (1080 - travel * t).toFixed(1) + 'px'; };
      const spell = (t) => {
        text.style.opacity = t.toFixed(3);
        text.style.letterSpacing = (0.06 + 0.34 * t).toFixed(3) + 'em';
      };
      place(0);
      spell(0);
      const rise = inkMs > 0 ? clock.run(inkMs, 'in', place) : Promise.resolve(place(1));
      return rise.then(() => (wordMs > 0 ? clock.run(wordMs, 'out', spell) : spell(1)));
    },
    /** Review 21: 「力尽きた」 gives way as the result screen comes up over it (one headline at a time). */
    fadeWord(ms) {
      const text = el ? el.querySelector('.ocbg-battle-ink-word') : null;
      if (!text) return Promise.resolve();
      if (!(ms > 0)) { text.style.opacity = '0'; return Promise.resolve(); }
      const from = parseFloat(text.style.opacity) || 1;
      return clock.run(ms, 'in', (t) => { text.style.opacity = (from * (1 - t)).toFixed(3); });
    },
    destroy() { if (el) el.remove(); el = null; },
  };
}
