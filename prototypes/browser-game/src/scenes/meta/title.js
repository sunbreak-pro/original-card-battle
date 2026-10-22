// The title: the lair at its deepest (layer 7's backdrop values from the bridge: the violet spring,
// the miasma rising slowly), one heir standing at its edge, the line from concept-v3 §1, and
// はじめから / つづきから / 設定. The router mounts it; it only calls back.
import { h, place, pushKeys } from '../common/stage.js';
import { MetaText } from './text.js';
import { sceneRoot, soundOf, sceneClock, styler, ocb } from './util.js';
import { createBackdrop, layerBackdrop } from './backdrop.js';

/**
 * mountTitle(host, ctx) → { destroy() }
 * ctx: { stage, audio, settings, hasSave, onNew(), onContinue(), onSettings(), unreadable? }
 * `unreadable`: a save is stored but could not be read (the router keeps it and says so under the menu): つづきから
 * says that instead of 「保存された生は無い」, and はじめから asks first, over the title as it stands, like a save.
 */
export function mountTitle(host, ctx) {
  const O = ocb();
  const sound = soundOf(ctx);
  const root = sceneRoot(host, 'title');
  const backdrop = createBackdrop(root, layerBackdrop(7), { vignette: 0.55, moteSpeed: 0.7 });
  const clock = sceneClock((dt, now) => { backdrop.paint(now); breathe(now); });

  // ---- the heir at the edge of the spring ----
  const figureWrap = place(h('div', { class: 'ocbg-meta-title-figure' }), 960 - 70, 600, 140, 226);
  const foot = O.FootLight(200, false);
  place(foot, -30, 196);
  figureWrap.appendChild(foot);
  figureWrap.appendChild(O.Figure('swordsman'));
  const ground = place(h('div', { class: 'ocbg-meta-title-ground' }), 0, 826, 1920, 254);
  root.appendChild(ground);
  root.appendChild(figureWrap);

  // ---- the line, written top to bottom on the right ----
  const tagline = place(h('div', { class: 'ocbg-meta-title-tagline' }), 1440, 104, 280, 880);
  const charEls = [];
  MetaText.TaglineLines.forEach((line) => {
    const col = h('div', { class: 'ocbg-meta-title-col' });
    for (const ch of line) {
      const c = h('span', null, ch);
      charEls.push(c);
      col.appendChild(c);
    }
    tagline.appendChild(col);
  });
  root.appendChild(tagline);

  // ---- name and menu on the left ----
  const name = place(h('div', { class: 'ocbg-meta-title-name' }, MetaText.GameName), 152, 236);
  const rule = place(h('i', { class: 'ocbg-meta-title-rule' }), 152, 300, 360, 2);
  root.appendChild(name);
  root.appendChild(rule);

  // はじめから over a saved lineage: the router first asks whether to wipe it (a confirm card). The
  // question is asked over the title as it stands, not after the walk into the dark, so 「やめる」
  // leaves a working title behind; 「はじめから」 there moves on to the hall. Without a save nothing is
  // asked and the title walks out as usual.
  const startNew = () => {
    if (!ctx.hasSave && !ctx.unreadable) { leave(ctx.onNew, true); return; }
    sound.play('uiClick');
    if (ctx.onNew) ctx.onNew();
  };
  const items = [
    { key: 'new', label: MetaText.NewGame, enabled: true, run: startNew },
    { key: 'continue', label: MetaText.Continue, enabled: !!ctx.hasSave, run: () => leave(ctx.onContinue, true) },
    { key: 'settings', label: MetaText.Settings, enabled: !!ctx.onSettings, run: () => { sound.play('uiClick'); ctx.onSettings && ctx.onSettings(); } },
  ];
  const menu = place(h('div', { class: 'ocbg-meta-title-menu', role: 'menu' }), 152, 560, 520);
  const rows = items.map((it, i) => {
    const row = h('button', {
      class: 'ocbg-meta-title-item' + (it.enabled ? '' : ' is-off'),
      type: 'button',
      role: 'menuitem',
      'aria-disabled': it.enabled ? 'false' : 'true',
      onmouseenter: () => focus(i, true),
      onclick: () => choose(i),
    }, h('i', { class: 'ocbg-meta-title-mark' }), h('span', null, it.label));
    if (!it.enabled && it.key === 'continue') row.appendChild(h('small', null, ctx.unreadable ? MetaText.Unreadable : MetaText.NoSave));
    menu.appendChild(row);
    return row;
  });
  root.appendChild(menu);
  const keys = place(h('div', { class: 'ocbg-meta-keys' }, MetaText.TitleKeys), 152, 1004);
  root.appendChild(keys);

  // ---- focus and choice ----
  let cur = items.findIndex((it) => it.key === (ctx.hasSave ? 'continue' : 'new'));
  let busy = false;
  function focus(i, fromPointer) {
    if (i === cur || !items[i].enabled) return;
    cur = i;
    rows.forEach((r, k) => r.classList.toggle('is-on', k === cur));
    if (fromPointer) sound.play('nodeHover'); else sound.play('uiClick');
  }
  rows.forEach((r, k) => r.classList.toggle('is-on', k === cur));
  function move(by) {
    let i = cur;
    for (let n = 0; n < items.length; n++) {
      i = (i + by + items.length) % items.length;
      if (items[i].enabled) { focus(i, false); return; }
    }
  }
  function choose(i) {
    if (busy || !items[i].enabled) return;
    cur = i;
    items[i].run();
  }

  // Leaving into the den: the menu lets go, the heir walks into the dark, then the router moves on.
  async function leave(cb, enter) {
    if (busy) return;
    busy = true;
    if (enter) sound.play('titleEnter');
    sound.play('uiConfirm');
    const pm = styler(menu), pt = styler(tagline), pf = styler(figureWrap), pn = styler(name);
    clock.tween(pm, { o: 0, x: -24 }, 360, 'in');
    clock.tween(pn, { o: 0 }, 420, 'in');
    clock.tween(pt, { o: 0 }, 700, 'in');
    clock.tween(pf, { o: 0, y: 26, s: 0.92 }, 900, 'in');
    await clock.tween(fade, { o: 1 }, 900, 'inout', 200);
    if (cb) cb();
  }

  // ---- entrance ----
  const fade = styler(place(h('div', { class: 'ocbg-meta-fade' }), 0, 0, 1920, 1080), { o: 1 });
  root.appendChild(fade.el);
  clock.tween(fade, { o: 0 }, 1600, 'inout');
  const pf0 = styler(figureWrap, { o: 0, y: 12 });
  clock.tween(pf0, { o: 1, y: 0 }, 1400, 'out', 500);
  const pn0 = styler(name, { o: 0, y: 8 });
  clock.tween(pn0, { o: 1, y: 0 }, 900, 'out', 900);
  const pr0 = styler(rule, { o: 0, s: 0.4 });
  rule.style.transformOrigin = '0 50%';
  clock.tween(pr0, { o: 1, s: 1 }, 1100, 'out', 1000);
  charEls.forEach((c, i) => {
    const p = styler(c, { o: 0, y: -10 });
    clock.tween(p, { o: 1, y: 0 }, 700, 'out', 900 + i * 85);
  });
  const menuIn = rows.map((r, i) => {
    const p = styler(r, { o: 0, x: -16 });
    return clock.tween(p, { o: 1, x: 0 }, 500, 'out', 1800 + i * 110);
  });
  Promise.all(menuIn).then(() => rows.forEach((r) => { r.style.transform = ''; r.style.opacity = ''; }));
  const pk = styler(keys, { o: 0 });
  clock.tween(pk, { o: 1 }, 700, 'out', 2400);

  // The heir's foot light breathes with the spring (the same cosine breath as the backdrop's light).
  function breathe(now) {
    const k = 0.5 - 0.5 * Math.cos((now / 4200) * 2 * Math.PI);
    foot.style.opacity = String(0.55 + 0.35 * k);
  }

  const popKeys = pushKeys((e) => {
    if (busy) return false;
    if (document.querySelector('.ocbg-confirm')) return false;   // the router's question is open over the title
    if (e.key === 'ArrowUp' || e.key === 'w') { move(-1); return true; }
    if (e.key === 'ArrowDown' || e.key === 's') { move(1); return true; }
    if (e.key === 'Enter' || e.key === ' ') { choose(cur); return true; }
    // a held Esc repeats: only a fresh press opens the settings (the panel closes on a fresh press only, too)
    if (e.key === 'Escape' && ctx.onSettings) { if (!e.repeat) { sound.play('uiClick'); ctx.onSettings(); } return true; }
    return false;
  });

  return {
    destroy() {
      popKeys();
      clock.stop();
      root.remove();
    },
  };
}

