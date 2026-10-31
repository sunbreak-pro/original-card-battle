// The battle scene: mounts the DOM renderer of one fight's depiction script (Unity's battle screen,
// unity-port/unity-project-kit/Assets/View/Depiction/DepictionPlayer.cs and its views) into the stage.
//
//   mountBattleScene(host, ctx) → { destroy }
//   ctx = { stage, audio, settings, source, layer (1..7), nodeKind ('Battle' | 'Elite' | 'Boss'),
//           chain: { index, total }, disclosureOf(enemyId), openJournal(host, opts) → { destroy },
//           onSettings(), onFinished(), [onSettingsChange(settings)], [auto: { choose(source, played) }] }
//
// The scene computes no rule: player.js plays ctx.source's frames / events / cues and sends input back
// to ctx.source (docs/BATTLE_SCENE.md). This file builds the root, the layers, the shared env, the three
// view modules (hand / field / hud) and the keys, and tears it all down.
import './battle.css';
import { h, pushKeys } from '../common/stage.js';
import { EffectSwitches } from '../../bridge/depictionScript/Effects.js';
import { createBattleClock } from './clock.js';
import { L } from './layout.js';
import { createPlayer } from './player.js';
import { createHand } from './hand.js';
import { createField } from './field.js';
import { createHud } from './hud.js';

/** battle_ui_ux_v2 §2.4: the journal drawer takes the right 30 % of the screen. */
const JOURNAL_SHARE = 0.3;

/** docs/BATTLE_SCENE.md §2.1, bottom → top. */
export const LAYERS = ['backdrop', 'field', 'fx', 'panels', 'marks', 'hud', 'shade', 'hand', 'held', 'float', 'over'];

/**
 * The scene's sound: never throws. While fast-forward is held (`clocks.clock.fastForward`), a sound of the same
 * name asked for again within L.FF_SOUND_GAP_MS of real time is dropped (review fix: with every stagger
 * collapsed, a turn's 6 discards and 6 draws fired in the same instant and rang louder than a hit).
 */
function safeAudio(audio, clocks) {
  const last = new Map();
  return {
    play(name, delayMs, mult) {
      if (!audio || typeof audio.play !== 'function' || !name) return;
      if (clocks && clocks.clock.fastForward) {
        const at = clocks.real.now + (delayMs || 0);
        const prev = last.get(name);
        if (prev != null && Math.abs(at - prev) < L.FF_SOUND_GAP_MS) return;
        last.set(name, at);
      } else if (clocks) last.set(name, clocks.real.now + (delayMs || 0));
      try { audio.play(name, delayMs || 0, mult); } catch (err) { /* sound never stops the battle */ }
    },
  };
}

/**
 * The scene root, its layers and the shared env (docs/BATTLE_SCENE.md §2), without any module. Exported
 * so a module's own dev page can mount that module alone on the same env.
 * @returns {{ root, layer, env, clocks }}
 */
export function createBattleEnv(host, ctx) {
  const OCB = window.OCB;
  const settings = ctx.settings || {};
  const root = h('div', { class: 'ocbg-battle', 'data-layer': String(ctx.layer || 1), 'data-node': ctx.nodeKind || 'Battle' });
  const layer = {};
  for (const name of LAYERS) {
    layer[name] = h('div', { class: 'ocbg-battle-layer ocbg-battle-layer--' + name });
    root.appendChild(layer[name]);
  }
  host.appendChild(root);

  const clocks = createBattleClock(settings);
  const switches = EffectSwitches.AllOn();
  const stage = ctx.stage || null;
  const env = {
    OCB,
    root,
    layer,
    clock: clocks.clock,
    real: clocks.real,
    fx: {
      switches,
      on: (id) => switches.IsOn(id),
      ms: (id) => switches.Ms(id),
      stagger: (id) => switches.StaggerMs(id),
      wait: (id, count) => switches.WaitMs(id, count == null ? 1 : count),
    },
    geom: {},
    audio: safeAudio(ctx.audio, clocks),
    settings,
    stage,
    toStage(e) {
      if (stage && stage.toStage) return stage.toStage(e);
      const r = root.getBoundingClientRect();
      const k = r.width / L.W || 1;
      return { x: (e.clientX - r.left) / k, y: (e.clientY - r.top) / k };
    },
    layerNo: Math.max(1, Math.min(7, ctx.layer | 0 || 1)),
    nodeKind: ctx.nodeKind || 'Battle',
    /** The exploration's miasma %, when the app hands it (the writer always writes 0). */
    miasmaPercent: typeof ctx.miasmaPercent === 'number' ? ctx.miasmaPercent : null,
    L,
  };
  return { root, layer, env, clocks };
}

/**
 * The web fonts the cards' text is measured in (review fix). The Japanese faces come in unicode-range subsets,
 * and a subset is fetched only once its glyphs are laid out, so document.fonts.ready alone resolved before the
 * cards were built and their names were fitted with the fallback's widths. The opening frame's hand is empty
 * (the turn start draws it), but its piles list every card by name: FontFaceSet.load(font, text) asks for the
 * subsets those names need in the display face hand-card.js fits them in (and for any hand text in the body
 * face), then waits for anything else already loading (the HUD's text, laid out under the hidden layers).
 */
function fontsFor(source) {
  const set = document.fonts;
  if (!set || typeof set.load !== 'function') return Promise.resolve();
  const names = new Set();
  const rest = new Set();
  try {
    const frame = source.Frame;
    JSON.stringify(frame.Piles, (key, value) => { if (key === 'Name' && typeof value === 'string') names.add(value); return value; });
    JSON.stringify(frame.Hand, (key, value) => {
      if (typeof value === 'string') (key === 'Name' ? names : rest).add(value);
      return value;
    });
  } catch (err) { /* nothing to read: only the fonts already loading are waited for */ }
  const css = getComputedStyle(document.documentElement);
  const family = (name, fallback) => { const v = css.getPropertyValue(name); return v && v.trim() ? v.trim() : fallback; };
  const loads = [];
  const ask = (font, text) => { if (text) loads.push(set.load(font, text).catch(() => [])); };
  ask('700 22px ' + family('--font-display', 'serif'), [...names].join(''));
  ask('400 16px ' + family('--font-body', 'sans-serif'), [...rest].join(''));
  return Promise.all(loads).then(() => set.ready);
}

export function mountBattleScene(host, ctx) {
  if (!ctx || !ctx.source) throw new Error('mountBattleScene: ctx.source is required');
  const { root, layer, env, clocks } = createBattleEnv(host, ctx);
  /**
   * battle-visual-v1 §4.1 「その画面（設定・セーブ）は戦闘中に開けないので、戦闘中に速さを替えられるのはこのボタンだけです」 and
   * battle_ui_ux_v2 §3.4 (Esc backs out and closes, nothing more). Review 19 (a repeat finding): neither Esc nor a gear opens
   * the settings in a battle any more. The app can still ask for them with ctx.settingsInBattle (and ctx.onSettings).
   */
  function settingsInBattle() { return !!ctx.settingsInBattle && typeof ctx.onSettings === 'function'; }

  // The modules raise input through callbacks; the player is made last, so they are bound late.
  let player = null;
  const late = (name) => (...args) => (player ? player.handCallbacks[name](...args) : undefined);

  const field = createField(env, {
    layer: env.layerNo,
    nodeKind: env.nodeKind,
    // 俊敏's choice on the floor (battle_ui_ux_v2 §3.8): a click on a mark, the pointer over one.
    onFreeStepPick: (dir) => { if (player) player.pickFreeStep(dir); },
    onFreeStepFocus: (dir) => { if (player) player.focusFreeStep(dir); },
  });
  const hud = createHud(env, {
    chain: ctx.chain || { index: 1, total: 1 },
    nodeKind: env.nodeKind,
    // The plate, like X, is not lost to a refused card's slide home (it waits for it); a press while the flow runs, or
    // mashed right after it, does not end the next turn (player.requestEndTurn's mash window).
    onEndTurn: () => { if (player) player.requestEndTurn({ queue: true, press: true }); },
    onSpeed: () => { if (player) player.cycleSpeed(); },
    onJournal: () => toggleJournal(),
    onSettings: () => openSettings(),
    // battle-visual-v1 §4.1 (review 19): the settings and save screen cannot be opened in a battle — no gear, unless the app
    // asks for one (ctx.settingsInBattle).
    settingsInBattle: settingsInBattle(),
    onDrawer: (open) => {
      // battle_ui_ux_v2 §3.5 (review fix; Unity lets the cards be taken under the list): while the pile list is open
      // the cards lift on hover but are not taken; its close button says 「閉じるとプレイできます」.
      if (open) hand.letGo();
      syncPause();
      updateShift();
    },
    // §2.2.2: hovering an enemy's omen badge keeps only that enemy's aimed cells lit.
    onOmenHover: (unit) => { if (field.focusAim) field.focusAim(unit); },
  });
  const hand = createHand(env, {
    onRelease: late('onRelease'),
    onHover: late('onHover'),
    onHoldStart: late('onHoldStart'),
    onHoldEnd: late('onHoldEnd'),
    onTarget: late('onTarget'),
    preview: (id, unit) => (player ? player.handCallbacks.preview(id, unit) : ''),
    // §1.4 / §3.4: the enemy a Tab at rest chose (the floor's N) is where a selection starts (−1: the card's own target).
    restingUnit: () => (player ? player.restingUnit : -1),
  });
  player = createPlayer(env, { hand, field, hud }, ctx);

  // ---- overlays: the journal (J / 手記) and the settings (Esc) ---------------------------------
  // While either is up the scene holds still (review fix: the enemy's turn and the result played on unseen
  // behind them, though the journal says 「閉じるとプレイできます」): the scene clock stops, the flow waits
  // with it, and the result's hold counts only the time no overlay covered it.
  let journal = null;
  /** The app's settings overlay is up (ctx.onSettings was called and the scene has had no input since). */
  let settingsOpen = false;
  function overlayUp() { return !!journal || settingsOpen; }
  function syncPause() {
    clocks.clock.setPaused(overlayUp());
    // §2.4 / §3.5: behind the journal or the pile list the cards can still be hovered (not taken); the settings' modal
    // holds all. The pile list does not hold the scene clock (it is no overlay of the turn).
    const drawer = hud.drawerOpen();
    // Review 16: with the pile list open (and nothing else over the scene), a card pressed or picked by its digit says
    // why nothing happens, on the refusal line; the guide line says it too (player.refreshPlayable). Review 17: the
    // journal the same way (a pressed card did nothing, and the guide still asked for a release).
    const onTry = journal && !settingsOpen ? () => player.refuseWhileJournal()
      : drawer && !overlayUp() ? () => player.refuseWhileDrawer() : null;
    hand.setBlocked(overlayUp() || drawer, !settingsOpen && (!!journal || drawer), onTry);
    // Review 21: a refusal line that asked to close the list or the journal goes with it.
    if (player.overlaysChanged) player.overlaysChanged({ drawer, journal: !!journal });
    player.refreshPlayable();
    updateFF();
  }
  /** The journal's drawer (the right 30 %, as the pile list's left 30 %) covers `p` while it is open. */
  env.geom.journalCovers = (p) => !!journal && !!p && p.x >= L.W * (1 - JOURNAL_SHARE);
  /** The journal drawer's left edge (stage px) while it is open, else null: the panels and badges keep left of it. */
  env.geom.journalEdge = () => (journal ? L.W * (1 - JOURNAL_SHARE) : null);
  /**
   * battle_ui_ux_v2 §2.3 / §2.4: the figures slide 15 % left while the journal drawer (right) is open, 15 %
   * right while the pile list (left) is open, and stay when both are.
   */
  function updateShift() {
    if (destroyed || !field.setShift) return;
    field.setShift((hud.drawerOpen() ? 1 : 0) - (journal ? 1 : 0));
  }
  function closeJournal() {
    if (!journal) return;
    const j = journal;
    journal = null;
    try { if (j.destroy) j.destroy(); } catch (err) { console.error(err); }
    if (j.host && j.host.parentNode) j.host.remove();
    // Review fix: no journalOpen / journalClose here — the journal plays its own (docs/AUDIO.md: 「戦闘でも」), and
    // the scene's copy made each one ring twice.
    syncPause();
    updateShift();
  }
  function toggleJournal() {
    if (journal) { closeJournal(); return; }
    if (typeof ctx.openJournal !== 'function' || player.state === 'over') return;
    // A held card goes home and a click / key selection drops (letGo clears both).
    hand.letGo();
    const jhost = h('div', { class: 'ocbg-battle-journal' });
    layer.over.appendChild(jhost);
    let opened = null;
    try {
      opened = ctx.openJournal(jhost, { enemyIds: player.enemyIds(), disclosureOf: ctx.disclosureOf, onClose: closeJournal });
    } catch (err) {
      console.error(err);
    }
    journal = { destroy: opened && opened.destroy ? () => opened.destroy() : null, host: jhost };
    syncPause();
    updateShift();
  }
  function openSettings() {
    if (!settingsInBattle()) return;
    hand.letGo();
    ctx.onSettings();
    // The app's overlay is modal: it holds the scene until it closes (main.js closeOverlay calls refresh()).
    // Review fix: a modal passes on the keys it has no use for (Shift, Ctrl, Alt, F-keys …), so a key that
    // reaches the scene proves nothing. Only one of the scene's own keys (sceneKey) or a click on the scene
    // means no overlay is up (an app that opened none, or one that never calls refresh()).
    settingsOpen = true;
    syncPause();
  }
  function settingsClosed() {
    if (!settingsOpen) return;
    settingsOpen = false;
    syncPause();
  }
  // The pile list does not hold the turn: as in Unity (DepictionPlayer.UpdateEndTurn never looks at it), the
  // plate and X end the turn with the list open, and the list stays open and follows the new piles. Only the cards
  // wait for it to close (§3.5, syncPause).
  player.setOverlayCheck(overlayUp);
  player.setDrawerCheck(() => hud.drawerOpen());
  player.setJournalCheck(() => !!journal);

  // ---- keys (battle_ui_ux_v2 §3.4; docs/BATTLE_SCENE.md §4.5) -----------------------------------------
  // One key, one role (§3.4): 1–9 select a card, Enter plays the selection (and nothing without one, so a
  // confirm can never end the turn), Tab moves the target among two or more enemies, I opens the selected
  // card's detail, X ends the turn, Space held fast-forwards the enemy's turn and a play, J the journal,
  // Esc backs out one step and then opens the settings.
  const FF_KEYS = new Set([' ']);
  const ffHeld = new Set();
  let pointerFF = false;
  /**
   * battle_ui_ux_v2 §3.4 「Space: 敵フェーズの早送り（次の 1 手の終わりまで）」 / §3.11 「Space かクリックで次の手へ早送り」
   * (review fix: only a held key or button sped the clock, so a tap advanced one frame's worth and the rest of the
   * action played at speed): a press arms the fast-forward until the screen event playing now has played out
   * (player.onEventEnd); holding the key or the button keeps it on as before.
   */
  let ffArmed = false;
  /**
   * Review 19 (a tap in the 350 ms pause after an enemy action was spent on the pause, and the next action played at full
   * length): the one-press fast-forward runs to the end of the next action — the event playing now, or, pressed in an
   * event's after-wait (player.afterWaiting), the event after it. `ffUntil` is the count of event ends (player.eventEnds)
   * at which it ends.
   */
  let ffUntil = 0;
  /** When (real ms) the one-press fast-forward was last armed: a press that would run under L.FF_MIN_RUN_MS carries on. */
  let ffArmedAt = -Infinity;
  function updateFF() {
    clocks.clock.setFastForward((ffHeld.size > 0 || pointerFF || ffArmed) && !overlayUp() && player.canFastForward());
  }
  function armFF() {
    if (!player.canFastForward()) return;
    if (!ffArmed) ffArmedAt = clocks.real.now;
    ffArmed = true;
    ffUntil = Math.max(ffUntil, player.eventEnds + (player.afterWaiting ? 2 : 1));
    updateFF();
  }
  function clearFF() {
    ffHeld.clear();
    pointerFF = false;
    ffArmed = false;
    updateFF();
  }
  const offEventEnd = player.onEventEnd(() => {
    if (!ffArmed || player.eventEnds < ffUntil) return;
    // A press in an action's last moments would hurry next to nothing: it runs on to the end of the action after it.
    if (clocks.real.now - ffArmedAt < L.FF_MIN_RUN_MS) { ffUntil = player.eventEnds + 1; return; }
    ffArmed = false;
    updateFF();
  });
  /**
   * §3.4: after Enter plays a card (確定後: the source accepted it), the card then at the same fan place is
   * selected, in the same turn. Review fix: a refused Enter selects nothing again, and a card the source
   * would refuse is not picked — the nearest playable one is, and none when nothing is playable (the guide
   * 「出せる札がありません…」 then shows).
   */
  let autoSelect = null;
  const turnNow = () => { try { return ctx.source.Frame.Corner.Turn; } catch (err) { return -1; } };
  /**
   * The keys the scene acts on (§3.4). A modal overlay takes every one of them (the app's settings and the
   * confirm card: Esc, Enter, Space, Tab and the printable keys), so one of them arriving here means none is up.
   */
  function sceneKey(e) {
    if (e.ctrlKey || e.metaKey || e.altKey) return false;
    const k = e.key;
    // ← / → are the scene's only while 俊敏's choice waits (§3.4: 「それ以外で ← / → は使いません」).
    if ((k === 'ArrowLeft' || k === 'ArrowRight') && player && player.freeStepPending) return true;
    return k === 'Escape' || k === 'Enter' || k === 'Tab' || FF_KEYS.has(k) || /^[1-9]$/.test(k) || /^[jJiIxX]$/.test(k);
  }
  /**
   * Esc with nothing left to back out of opens the settings when the key is let go (review fix): a held Esc
   * repeats, and the overlay that opened on the press took the repeats and closed itself again, over and over.
   * The press is remembered here; the repeats are swallowed; the release opens the overlay once.
   */
  let escToSettings = false;
  const popKeys = pushKeys((e) => {
    if (!player) return false;
    if (!sceneKey(e)) return false; // not the scene's: Shift, Ctrl, Alt, F-keys … pass on and prove nothing
    settingsClosed(); // one of the scene's keys reached it: no modal overlay is up
    const k = e.key;
    if (player.state === 'over') {
      // The pile list (finish() closes it; a list opened since) closes before Esc moves past the result.
      if (k === 'Escape' && hud.drawerOpen()) { hud.closeDrawer(); return true; }
      // Only a fresh press moves the result on: a key held to fast-forward the last blow repeats, and its
      // repeats must not skip the card (review fix).
      const mine = k === 'Enter' || k === ' ' || k === 'Escape';
      if (mine && !e.repeat) player.skipResult();
      return mine;
    }
    if (k === 'Escape') {
      // Review fix: only a fresh press backs out one step; its repeats do nothing (held, it reopened the settings).
      if (e.repeat) return true;
      escToSettings = false;
      if (hand.cancel()) return true;
      // Review 21: a status chip's or a stance's detail opened by a right click (§3.13) closes like the card's.
      if (hud.closePinned && hud.closePinned()) return true;
      if (hud.drawerOpen()) { hud.closeDrawer(); return true; }
      if (journal) { closeJournal(); return true; }
      // 俊敏's choice (review 21): with ← / → always picking their way, Esc takes the focus back to 留まる.
      if (player.freeStepPending && player.stayFreeStepFocus()) return true;
      escToSettings = true;
      return true;
    }
    if (k === 'j' || k === 'J') { if (!e.repeat) toggleJournal(); return true; }
    if (journal) return false; // the journal has the keys while it is open
    // battle_ui_ux_v2 §3.8: while 俊敏's choice waits, ← / → move its focus and Enter chooses (a held arrow repeats).
    if (player.freeStepPending && (k === 'ArrowLeft' || k === 'ArrowRight')) { player.moveFreeStepFocus(k === 'ArrowLeft' ? -1 : 1); return true; }
    if (player.freeStepPending && k === 'Enter') { if (!e.repeat) player.confirmFreeStep(); return true; }
    if (FF_KEYS.has(k)) {
      if (!e.repeat) armFF();
      ffHeld.add(k);
      updateFF();
      return true;
    }
    if (e.repeat) return k === 'Enter' || k === 'Tab';
    if (/^[1-9]$/.test(k)) { autoSelect = null; hand.select(Number(k) - 1); return true; }
    if (k === 'Enter') {
      // battle_ui_ux_v2 §3 入力の端 「選択中に Enter を 2 度押す」: 「1 度目で確定、2 度目は自動選択された次の札の確定」 (review 21:
      // the second Enter came while the first play resolved, found no selection and was lost). An Enter while the play it
      // confirmed resolves is kept, and confirms the card the scene picks next once the player is asked again.
      if (autoSelect && player.state !== 'idle') { autoSelect.confirm = true; return true; }
      const at = hand.selectedIndex();
      const plays = player.acceptedPlays;
      if (hand.confirm() && at >= 0) autoSelect = { index: at, turn: turnNow(), plays, confirm: false };
      return true;
    }
    if (k === 'Tab') {
      if (!hand.cycleTarget(e.shiftKey ? -1 : 1) && !hand.isHolding() && !hand.hasSelection() && player.state === 'idle') player.cycleGapFocus(e.shiftKey ? -1 : 1);
      return true;
    }
    if (k === 'i' || k === 'I') { hand.toggleSelectedDetail(); return true; }
    if (k === 'x' || k === 'X') {
      autoSelect = null;
      player.requestEndTurn({ queue: true, press: true });
      return true;
    }
    return false;
  });
  function onKeyUp(e) {
    if (ffHeld.delete(e.key)) updateFF();
    if (e.key === 'Escape' && escToSettings) {
      escToSettings = false;
      if (!destroyed && player && player.state !== 'over') openSettings();
    }
  }
  window.addEventListener('keyup', onKeyUp);

  // ---- pointer: holding the button on the arena fast-forwards; a click moves the result on -----------
  /**
   * The buttons, the panels, the hud and the overlays take their own clicks: they never fast-forward. The end-turn
   * plate is the exception (review fix, §3.5 「敵フェーズ中…早送りだけ受ける」): it has nothing to do while the flow
   * runs, so a click there hurries the enemy's turn like a click anywhere else on the arena.
   */
  function isControl(target) {
    if (!target || target === root) return false;
    if (target.closest && target.closest('.ocbg-battle-endturn')) return false;
    if (target.closest && target.closest('.ocbg-battle-hit')) return true;
    return layer.hud.contains(target) || layer.over.contains(target) || layer.panels.contains(target);
  }
  /**
   * Review 16 (double-clicking the target to play a selected card fast-forwarded the player's own attack: the second
   * click landed while the play resolved and armed the one-press fast-forward, so the strike, the hit-stop and the
   * number all vanished): a press within L.DOUBLE_CLICK_MS of a press or release made while the player was asked (the
   * click that played the card) does not fast-forward.
   */
  let lastAskedPointer = -Infinity;
  let downWhileAsked = false;
  function onPointerDown(e) {
    if (!player) return;
    settingsClosed(); // a click reached the scene: no modal overlay is up
    if (player.state === 'over') { player.skipResult(); return; }
    downWhileAsked = player.state === 'idle';
    if (downWhileAsked) lastAskedPointer = clocks.real.now;
    if (e.button === 0 && player.canFastForward() && !isControl(e.target)) {
      if (clocks.real.now - lastAskedPointer < L.DOUBLE_CLICK_MS) return;
      pointerFF = true;
      armFF();
    }
  }
  function onPointerUp() {
    if (downWhileAsked) { downWhileAsked = false; lastAskedPointer = clocks.real.now; }
    if (pointerFF) { pointerFF = false; updateFF(); }
  }
  /** The window lost focus: a held key or button will not send its release here (review fix). */
  function onBlur() { clearFF(); escToSettings = false; }
  function onVisibility() { if (document.hidden) clearFF(); }
  root.addEventListener('pointerdown', onPointerDown);
  window.addEventListener('pointerup', onPointerUp);
  window.addEventListener('blur', onBlur);
  document.addEventListener('visibilitychange', onVisibility);
  const offState = player.onState((s) => {
    // The one-press fast-forward belongs to the flow that was running: it ends when the player is asked again.
    if (s === 'idle' || s === 'over' || s === 'halted') { ffArmed = false; ffUntil = 0; }
    updateFF();
    if (s === 'idle' && autoSelect) {
      const a = autoSelect;
      autoSelect = null;
      // After the player has made the hand take input again (it does so right after it goes idle).
      Promise.resolve().then(() => {
        if (destroyed || player.state !== 'idle' || a.turn !== turnNow() || hand.isHolding() || hand.hasSelection()) return;
        if (player.acceptedPlays <= a.plays) return; // refused: the card slid home, nothing is selected again
        const faces = hand.faces();
        const from = Math.min(a.index, faces.length - 1);
        let pick = -1;
        for (let d = 0; d < faces.length && pick < 0; d++) {
          for (const i of [from + d, from - d]) {
            if (i >= 0 && i < faces.length && player.isPlayable(faces[i].Id)) { pick = i; break; }
          }
        }
        if (pick < 0) return;
        hand.select(pick, { auto: true });
        // The second Enter of the pair (above): the picked card is confirmed as the first one was, and the pair ends there.
        if (a.confirm) {
          const plays2 = player.acceptedPlays;
          if (hand.confirm()) autoSelect = { index: pick, turn: turnNow(), plays: plays2, confirm: false };
        }
      });
    } else if (s === 'over' || s === 'halted') autoSelect = null;
  });

  // Fonts first, so the card text measures right; then the battle. Review fix: until the opening frame is
  // applied, only the layer's backdrop shows (the HUD and the field are built empty: icons with no numbers, the
  // gap chip at x 0, a lit ターン終了), and then everything but the backdrop fades in together (L.OPEN_FADE_MS).
  // Hidden, not just transparent, so no button takes a click it does not show; laid out all the same, so the
  // fonts their text needs are fetched meanwhile. The over layer (journal, result, pile list) stays as it is:
  // what opens there opens over the backdrop.
  const opening = LAYERS.filter((name) => name !== 'backdrop' && name !== 'over').map((name) => layer[name]);
  opening.forEach((el) => { el.style.opacity = '0'; el.style.visibility = 'hidden'; });
  let started = false;
  function go() {
    if (started || destroyed) return;
    started = true;
    player.start();
    opening.forEach((el) => { el.style.visibility = ''; });
    clocks.clock.run(L.OPEN_FADE_MS, 'out', (t) => {
      if (destroyed) return;
      opening.forEach((el) => { el.style.opacity = t >= 1 ? '' : t.toFixed(3); });
    });
  }
  let destroyed = false;
  Promise.race([fontsFor(ctx.source), clocks.real.wait(L.OPEN_FONT_WAIT_MS)]).then(go, go);

  return {
    root,
    env,
    player,
    modules: { hand, field, hud },
    /**
     * The app closed an overlay it opened over the battle (main.js closeOverlay): the scene runs on, and the
     * speed switch shows the speed the settings may have changed (演出短縮 is read live by the clock).
     */
    refresh() {
      if (destroyed) return;
      settingsClosed();
      player.showSpeed();
    },
    /** For the app: hold the scene still (true) or let it run (false) while something of its own covers it. */
    pause(on) {
      settingsOpen = !!on;
      syncPause();
    },
    destroy() {
      if (destroyed) return;
      destroyed = true;
      offState();
      offEventEnd();
      popKeys();
      window.removeEventListener('keyup', onKeyUp);
      window.removeEventListener('pointerup', onPointerUp);
      root.removeEventListener('pointerdown', onPointerDown);
      window.removeEventListener('blur', onBlur);
      document.removeEventListener('visibilitychange', onVisibility);
      closeJournal();
      player.destroy();
      for (const m of [hand, hud, field]) {
        try { if (m && m.destroy) m.destroy(); } catch (err) { console.error(err); }
      }
      clocks.destroy();
      root.remove();
    },
  };
}
