// 設定・セーブ (CAMP_FACILITIES_DESIGN.md §3.5, dungeon_exploration_v4.md §6.2): the battle speed
// (×1.0 / ×1.25 / ×1.5, BattleSpeed's labels and chevrons), 演出短縮, the volumes and mute (the game's
// audio, which persists itself), 「保存して閉じる」 and 「閉じる」. An overlay over whatever screen
// opened it; Esc closes it. The speed is one value shared with the battle screen's switch.
import { h, place, pushKeys } from '../common/stage.js';
import { BattleSpeed } from '../../bridge/depictionScript/BattleSpeed.js';
import { MetaText } from './text.js';
import { sceneRoot, soundOf, sceneClock, styler, icon, modalSwallow } from './util.js';

function stepOf(speed) {
  if (BattleSpeed.Steps.includes(speed)) return speed;
  const n = Number(speed);
  for (const s of BattleSpeed.Steps) if (Math.abs(BattleSpeed.Multiplier(s) - n) < 1e-6) return s;
  return BattleSpeed.Default;
}

/**
 * mountSettings(host, ctx) → { destroy(), close() }
 * ctx: { stage, audio, settings: { speed (1 | 1.25 | 1.5 or a BattleSpeedStep), shortFx }, onChange(next),
 *        onSaveQuit?(), canSaveQuit? (false hides 保存して閉じる), inBattle?, saveQuitKind? ('life' | 'battle' |
 *        'lineage'; default 'battle' with inBattle, else 'life'), onClose() }
 * onChange gets { ...settings, speed: multiplier, speedStep, shortFx } on every change.
 */
export function mountSettings(host, ctx) {
  const sound = soundOf(ctx);
  const audio = sound.audio;
  const root = sceneRoot(host, 'settings');
  const clock = sceneClock();
  let alive = true;
  let closing = false;

  const game = { ...(ctx.settings || {}) };
  let speed = stepOf(game.speed ?? game.speedStep);
  let shortFx = !!game.shortFx;
  const vol = audio && audio.settings ? { ...audio.settings }
    : { master: game.master ?? 0.8, effects: game.effects ?? 1, ambience: game.ambience ?? 0.6, muted: !!game.muted };

  const shade = place(h('div', { class: 'ocbg-meta-shade', onclick: () => close() }), 0, 0, 1920, 1080);
  root.appendChild(shade);
  const panel = place(h('section', { class: 'ocbg-meta-panel ocbg-meta-settings-panel', role: 'dialog', 'aria-label': MetaText.SettingsTitle }), 540, 150, 840);
  root.appendChild(panel);
  panel.appendChild(h('header', null, h('h2', null, MetaText.SettingsTitle),
    h('button', { class: 'ocbg-meta-x', type: 'button', onclick: () => close(), 'aria-label': MetaText.Close }, icon('exile', 22, 'var(--ink)'))));

  const rows = [];
  // Every change hands on the whole panel as it stands now: the volumes and mute as they are at this
  // moment (vol follows the sliders and audio.onChange), not as they were when the panel opened. The
  // router writes the object back into the audio, so a stale copy would undo a volume change.
  function emit() {
    const live = ctx.settings || game;
    const next = { ...live, master: vol.master, effects: vol.effects, ambience: vol.ambience, muted: !!vol.muted,
      speed: BattleSpeed.Multiplier(speed), speedStep: speed, shortFx };
    if (vol.reverb != null) next.reverb = vol.reverb;
    if (ctx.onChange) ctx.onChange(next);
  }

  // ---- segmented rows ----
  function segRow(label, options, get, set, note) {
    const row = h('div', { class: 'ocbg-meta-set-row' }, h('span', { class: 'ocbg-meta-set-label' }, label));
    const seg = h('div', { class: 'ocbg-meta-seg' });
    const btns = options.map((o) => {
      const b = h('button', { type: 'button', onclick: () => { if (get() !== o.value) { set(o.value); sound.play('uiClick'); paint(); } } }, o.icon || null, h('span', null, o.label));
      b.optionValue = o.value;
      seg.appendChild(b);
      return b;
    });
    row.appendChild(seg);
    const paint = () => btns.forEach((b) => b.classList.toggle('is-on', b.optionValue === get()));
    paint();
    panel.appendChild(row);
    if (note) panel.appendChild(h('p', { class: 'ocbg-meta-set-note' }, note));
    const step = (by) => {
      const i = options.findIndex((o) => o.value === get());
      const j = Math.max(0, Math.min(options.length - 1, i + by));
      if (j !== i) { set(options[j].value); sound.play('uiClick'); paint(); }
    };
    rows.push({ el: row, left: () => step(-1), right: () => step(1), paint });
  }

  segRow(MetaText.Speed, BattleSpeed.Steps.map((s) => ({ value: s, label: BattleSpeed.Label(s), icon: icon('speed' + BattleSpeed.Chevrons(s), 22, 'var(--ink)') })),
    () => speed, (v) => { speed = v; emit(); });
  segRow(MetaText.ShortFx, [{ value: true, label: MetaText.ShortFxOn }, { value: false, label: MetaText.ShortFxOff }],
    () => shortFx, (v) => { shortFx = v; emit(); }, MetaText.ShortFxNote);

  // ---- volumes ----
  panel.appendChild(h('h3', { class: 'ocbg-meta-set-h3' }, MetaText.Volume));
  function slider(label, key, apply) {
    const fill = h('i', { class: 'ocbg-meta-slider-fill' });
    const knob = h('i', { class: 'ocbg-meta-slider-knob' });
    const track = h('div', { class: 'ocbg-meta-slider', role: 'slider', 'aria-label': label }, fill, knob);
    const num = h('b', { class: 'ocbg-meta-slider-num' });
    const row = h('div', { class: 'ocbg-meta-set-row' }, h('span', { class: 'ocbg-meta-set-label' }, label), track, num);
    panel.appendChild(row);
    const paint = () => {
      const v = vol[key];
      fill.style.width = `${v * 100}%`;
      knob.style.left = `${v * 100}%`;
      num.textContent = String(Math.round(v * 100));
      track.setAttribute('aria-valuenow', String(Math.round(v * 100)));
    };
    const setV = (v, quiet) => {
      v = Math.max(0, Math.min(1, Math.round(v * 20) / 20));
      if (v === vol[key]) return;
      vol[key] = v;
      try { apply(v); } catch (e) { /* no audio */ }
      paint();
      if (!quiet) sound.play('uiClick');
    };
    const fromPointer = (e) => {
      const r = track.getBoundingClientRect();
      setV((e.clientX - r.left) / r.width);
    };
    track.addEventListener('pointerdown', (e) => {
      fromPointer(e);
      track.setPointerCapture(e.pointerId);
      const move = (ev) => fromPointer(ev);
      const up = () => { track.removeEventListener('pointermove', move); track.removeEventListener('pointerup', up); };
      track.addEventListener('pointermove', move);
      track.addEventListener('pointerup', up);
    });
    paint();
    rows.push({ el: row, left: () => setV(vol[key] - 0.05), right: () => setV(vol[key] + 0.05), paint });
  }
  slider(MetaText.Master, 'master', (v) => audio && audio.setMaster(v));
  slider(MetaText.Effects, 'effects', (v) => audio && audio.setEffects(v));
  slider(MetaText.Ambience, 'ambience', (v) => audio && audio.setAmbienceVolume(v));
  segRow(MetaText.Mute, [{ value: true, label: MetaText.MuteOn }, { value: false, label: MetaText.MuteOff }],
    () => vol.muted, (v) => { vol.muted = v; if (audio) audio.setMuted(v); });

  // ---- save and close ----
  const foot = h('footer', null);
  // 「保存して閉じる」 wherever there is something to save; ctx.canSaveQuit === false hides it. A fight is in the save
  // too: tier1 R1-16 (2026-10-12, #462) 「戦闘中にゲームを閉じたら、閉じた場面から再開する」 — every move is saved as it
  // is made, so つづきから comes back to the same moment of the fight (docs/ARCH.md 「Save」). In 継承の間 and 出立
  // ('lineage') there is no life yet: the lineage (and 出立's draft) is already kept, so the button says it goes back
  // to the title with everything saved.
  if (ctx.onSaveQuit && ctx.canSaveQuit !== false) {
    const kind = ctx.saveQuitKind || (ctx.inBattle ? 'battle' : 'life');
    foot.appendChild(h('p', { class: 'ocbg-meta-set-note' }, MetaText.SaveNoteOf(kind)));
    const saveBtn = h('button', { class: 'ocbg-meta-btn', type: 'button', onclick: () => { sound.play('uiConfirm'); ctx.onSaveQuit(); } }, MetaText.SaveQuitOf(kind));
    foot.appendChild(saveBtn);
    rows.push({ el: saveBtn, enter: () => saveBtn.click() });
  }
  const closeBtn = h('button', { class: 'ocbg-meta-primary is-compact', type: 'button', onclick: () => close() }, h('span', null, MetaText.Close), h('small', null, 'Esc'));
  foot.appendChild(closeBtn);
  rows.push({ el: closeBtn, enter: () => close() });
  panel.appendChild(foot);
  panel.appendChild(h('p', { class: 'ocbg-meta-keys is-in-panel' }, MetaText.SettingsKeys));

  panel.style.top = `${Math.max(24, Math.round((1080 - panel.offsetHeight) / 2))}px`;

  let focus = 0;
  function paintFocus() { rows.forEach((r, i) => r.el.classList.toggle('is-focus', i === focus)); }
  paintFocus();

  // keep the panel in step with a change made elsewhere (the battle's switch, a mute key)
  const unsub = audio && audio.onChange ? audio.onChange((s) => { Object.assign(vol, s); rows.forEach((r) => r.paint && r.paint()); }) : null;

  // ---- open / close ----
  const ps = styler(panel, { o: 0, s: 0.97, y: 10 });
  const pd = styler(shade, { o: 0 });
  clock.tween(pd, { o: 1 }, 200, 'out');
  clock.tween(ps, { o: 1, s: 1, y: 0 }, 220, 'out');
  sound.play('uiClick');

  async function close() {
    if (closing) return;
    closing = true;
    sound.play('uiCancel');
    clock.tween(pd, { o: 0 }, 160, 'in');
    await clock.tween(ps, { o: 0, s: 0.98, y: 6 }, 160, 'in');
    if (alive && ctx.onClose) ctx.onClose();
  }

  const popKeys = pushKeys((e) => {
    if (closing) return true;
    // A held key repeats: only a fresh press closes, or the screen under it would open the panel again on the next
    // repeat, and close and open it over and over (its own openers ignore repeats too).
    if (e.key === 'Escape') { if (!e.repeat) close(); return true; }
    if (e.key === 'ArrowDown') { focus = (focus + 1) % rows.length; paintFocus(); sound.play('nodeHover'); return true; }
    if (e.key === 'ArrowUp') { focus = (focus - 1 + rows.length) % rows.length; paintFocus(); sound.play('nodeHover'); return true; }
    if (e.key === 'ArrowLeft') { rows[focus].left && rows[focus].left(); return true; }
    if (e.key === 'ArrowRight') { rows[focus].right && rows[focus].right(); return true; }
    if (e.key === 'Enter' || e.key === ' ') { if (rows[focus].enter) rows[focus].enter(); else if (rows[focus].right) rows[focus].right(); return true; }
    return modalSwallow(e); // modal: the screen under it gets no keys
  });

  return {
    close,
    destroy() {
      alive = false;
      popKeys();
      if (unsub) unsub();
      clock.stop();
      root.remove();
    },
  };
}
