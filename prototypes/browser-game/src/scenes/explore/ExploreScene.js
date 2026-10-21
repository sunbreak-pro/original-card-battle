// The exploration scene (dungeon_exploration_v4 §6; Unity ExplorationScreenView.cs + ExplorationBootstrap.cs).
// It renders ExplorationScreenBuilder's view-model and forwards the player's input to the ported
// ExplorationSession / Expedition. It computes no rule and no number: every value and line comes from
// the bridge (or the small scene-text table in ./text.js). What it adds is presentation: the per-layer
// backdrop under its veil and the miasma shade (battle-visual-v1 §2.4 / §2.5), the map drawn on it, a
// walker that travels each step, number roll-ups, the cards over the map, the battle hand-off and the
// reward pick, the trace pick, the interlude, and keyboard support.
// Trial mode (docs/EXTENSIONS.md 「探索の持ち物とイベント」, on when the expedition's session is Trial): the tools
// with their notes, consumable slots that act (標の石 aims on the map), the swap card after a find, the event
// cards (受ける / 受けない) and their result, 写しの帳's survey pick, and 刻限の砂 / 防瘴の面 in the band.
//
// mountExploreScene(host, ctx) → { destroy(), refresh() }
//   ctx.stage        the shared Stage (createStage)
//   ctx.audio        createAudio(...) from src/scenes/audio (optional; ctx.sound is accepted too)
//   ctx.settings     { shortFx?: boolean, speed?: number } (演出短縮 and battle speed; optional)
//   ctx.expedition   a ported Expedition; its Session is the exploration being shown
//   ctx.inheritance  { traceView(layer, nodeId) → traceNodeView | null, take(picks) → Taken (+ Result: what the take did) | null,
//                      scribbles() → the 走り書き the life carries } | null
//   ctx.onBattle(expedition) → Promise   runs the fights of expedition.StartEncounter(); resolves when over
//   ctx.onLifeEnd(ending)                 the life is over (see LifeEnd below)
//   ctx.onOverlay(kind, info) → Promise?  'journal' | 'cards' | 'settings'; info = { book, page, screen }
//   ctx.onSaveQuit()                      only used by the fallback settings page (when onOverlay is absent)
import './explore.css';
import { h, place, createClock, EASE, pushKeys, confirmCard } from '../common/stage.js';
import { NodeStanding, ScreenAction, CardKind, TrialAction, TrialCardKind } from '../../bridge/explorationScript/ExplorationScreen.js';
import { ExplorationText } from '../../bridge/explorationScript/ExplorationText.js';
import { ExplorationScreenBuilder } from '../../bridge/explorationScript/ExplorationScreenBuilder.js';
import { RestChoice, RunPhase } from '../../core/dungeon/ExplorationState.js';
import { NodeKind } from '../../core/dungeon/NodeKind.js';
import { Miasma } from '../../core/dungeon/Miasma.js';
import { MiasmaLook, MiasmaWarning } from '../../bridge/backdrop/MiasmaLook.js';
import { JournalPage, JournalBook } from '../../bridge/journal/JournalBook.js';
import { Enemies } from '../../core/battle/Enemies.js';
import { pickState } from '../../core/inheritance/Inheritance.js';
import { traceGains, deckIsFull, TraceGainText } from '../../bridge/exploration/TraceGain.js';
import { EnemyPages } from '../../bridge/journal/EnemyPages.js';
import { createBackdrop, skyStrip, rgbaCss } from './backdrop.js';
import { ico, itemIcon } from './icons.js';
import { SceneText as T } from './text.js';
import { cardDefById, cardName } from './cardFace.js';
import { nameAtRank, masteryProgress } from '../../bridge/exploration/MasteryView.js';

// ---------------------------------------------------------------------------------------------
// Layout (1920×1080 stage px). The Unity screen's proportions: map left ~2/3, detail right top,
// carried items right bottom, 「この生を終える」 bottom left, the band across the top.
// ---------------------------------------------------------------------------------------------
const AREA = { cx: 652, cy: 548, w: 1060, h: 680 }; // ExplorationScreenView MapWidth × MapHeight, centred in the map
const NODE = 88;
/** The walker stands on the top edge of its node: clear of the icon below and of the label of the node above. */
const WALKER_UP = NODE / 2 + 6;
const WALKER_BOX = 52;
/** Where an edge line starts and ends, measured from a node's centre. */
const EDGE_INSET = NODE / 2 + 8;
const DETAIL = { x: 1304, y: 112, w: 592, h: 604 };
const CARRIED = { x: 1304, y: 728, w: 592, h: 232 };
const MAP_CENTER = { x: 652, y: 540 };

const KIND_CLASS = {
  [NodeKind.Battle]: 'battle', [NodeKind.Elite]: 'elite', [NodeKind.Boss]: 'boss', [NodeKind.Rest]: 'rest',
  [NodeKind.Survey]: 'survey', [NodeKind.Event]: 'event', [NodeKind.Trace]: 'trace', [NodeKind.Carving]: 'carving',
};
const STANDING_CLASS = {
  [NodeStanding.Current]: 'is-current', [NodeStanding.Reachable]: 'is-reachable', [NodeStanding.Revisit]: 'is-revisit',
  [NodeStanding.Resolved]: 'is-resolved', [NodeStanding.Far]: 'is-far',
};
/**
 * The sound a fresh node of this kind adds after the step (docs/AUDIO.md, 「A step and its result」).
 * A trace sounds 'traceTake' when the legacy is actually taken, not on arrival.
 */
const STEP_RESULT = { [NodeKind.Survey]: 'infoGained', [NodeKind.Event]: 'eventResolve', [NodeKind.Carving]: 'infoGained' };
/** Trial mode's card buttons (TrialAction): answered on the session, then the flow shows what they did. */
const TRIAL_ACTIONS = new Set(Object.values(TrialAction));

/** Which icon heads a forecast line, by the word it starts with (the line itself is the bridge's). */
function forecastIcon(line) {
  if (line.startsWith('刻限')) return ['hourglass', 'var(--ink)'];
  if (line.startsWith('瘴気')) return ['miasma', 'var(--phosphor)'];
  if (line.startsWith('最大スタミナ')) return ['pip', 'var(--amber)', { outline: true }];
  if (line.startsWith('スタミナ')) return ['pip', 'var(--amber)'];
  if (line.startsWith('HP')) return ['heart', 'var(--crimson)'];
  return null;
}

/**
 * A bridge line with its digit runs set in the number face (IBM Plex Mono) and its hidden-name runs
 * (「■■の秘跡」 until the carving is read, world-v1) set as sealed blocks instead of white squares.
 */
function rich(text, cls) {
  const el = h('span', { class: cls || null });
  String(text).split(/(\d+|■+)/).forEach((part, i) => {
    if (!part) return;
    if (i % 2 === 0) el.appendChild(document.createTextNode(part));
    else if (part[0] === '■') el.appendChild(h('span', { class: 'ocbg-explore-sealed', 'aria-label': part }, part));
    else el.appendChild(h('span', { class: 'ocbg-explore-num' }, part));
  });
  return el;
}

/** Element.append without the nulls and falses (append would print them). */
function put(el, ...kids) {
  for (const k of kids.flat()) if (k != null && k !== false) el.append(k);
  return el;
}

/** "key: value" rows (脅威度: 通常, 休む: HP +8…) split at the first ": " for a two-column read. */
function splitRow(line) {
  const at = line.indexOf(': ');
  return at > 0 && at <= 8 ? [line.slice(0, at), line.slice(at + 2)] : [null, line];
}

/** A label whose numbers roll to their new value when only the numbers change. */
class RollLabel {
  constructor(el, ms = 520) {
    this.el = el;
    this.ms = ms;
    this.text = null;
    this.spans = [];
    this.anim = null;
  }

  set(text, now, animate) {
    if (text === this.text) return;
    const old = this.text;
    this.text = text;
    const parts = String(text).split(/(\d+)/);
    let from = null;
    if (animate && old != null) {
      const op = String(old).split(/(\d+)/);
      if (op.length === parts.length && op.every((p, i) => i % 2 === 1 || p === parts[i])) from = op.filter((_, i) => i % 2).map(Number);
    }
    this.el.textContent = '';
    this.spans = [];
    parts.forEach((part, i) => {
      if (i % 2) {
        const s = h('span', { class: 'ocbg-explore-num' }, part);
        this.spans.push(s);
        this.el.appendChild(s);
      } else if (part) this.el.appendChild(document.createTextNode(part));
    });
    const to = parts.filter((_, i) => i % 2).map(Number);
    if (from && from.some((v, i) => v !== to[i])) {
      this.anim = { from, to, t0: now };
      this.paint(from);
      this.el.classList.remove('is-rolled');
      void this.el.offsetWidth;
      this.el.classList.add('is-rolled');
    } else this.anim = null;
  }

  paint(values) { values.forEach((v, i) => { if (this.spans[i]) this.spans[i].textContent = String(v); }); }

  tick(now) {
    if (!this.anim) return;
    const t = Math.min(1, (now - this.anim.t0) / this.ms);
    const e = EASE.out(t);
    this.paint(this.anim.from.map((f, i) => Math.round(f + (this.anim.to[i] - f) * e)));
    if (t >= 1) this.anim = null;
  }
}

/**
 * What ctx.onLifeEnd receives: the phase the life ended in (RunPhase: MiasmaDeath / Fallen /
 * Completed / Survived), the end card ExplorationScreenBuilder wrote, and where it happened.
 */
function lifeEnd(session, screen) {
  const state = session.State;
  const node = state.Map.Node(state.CurrentNodeId);
  return {
    Ending: state.Phase,
    Card: screen.Card,
    Title: screen.Card.Title,
    Lines: screen.Card.Lines,
    Layer: state.Profile.Layer,
    LayerTitle: screen.LayerTitle,
    NodeId: node.Id,
    NodeKind: node.Kind,
    State: state,
  };
}

export function mountExploreScene(host, ctx) {
  const expedition = ctx.expedition;
  const session = expedition.Session;
  const audio = ctx.audio || ctx.sound || null;
  const settings = ctx.settings || {};
  const reduce = typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
  const fast = () => !!(settings.shortFx || settings.shortEffects || reduce);
  const dur = (ms) => (fast() ? Math.round(ms * 0.35) : ms);

  const sfx = (name, delay, mult, opts) => {
    try { if (audio && audio.play) audio.play(name, delay || 0, mult, opts); } catch (e) { /* sound is optional */ }
  };
  const ambience = (id) => { try { if (audio && audio.setAmbience) audio.setAmbience(id); } catch (e) { /* optional */ } };
  /** The app saves the life after every change (save by replay; ARCH 「Save」). */
  const persist = () => { try { if (ctx.onPersist) ctx.onPersist(); } catch (e) { console.error('[explore] persist', e); } };

  // ---- a button chosen with Tab acts on Enter / Space (出立 teaches it: 「Tab 項目を移る　Space 押す」) ----
  // The screen's own keys (Enter steps onto the pinned node, takes the card's first button, confirms) come after
  // it: a focused 「やめる」 never confirms a 瘴気死 (dungeon_exploration_v4 §6.4 / §6.5 / §6.6 add those cards only
  // to stop an unwanted end). Only focus that Tab put there counts — a button clicked with the mouse keeps the
  // focus too, and Enter must then still step onto the pinned node.
  let tabFocus = null;
  let lastKeyTab = false;
  const onKeyCapture = (e) => { lastKeyTab = e.key === 'Tab'; };
  const onFocusIn = (e) => { const was = tabFocus; tabFocus = lastKeyTab ? e.target : null; if (was !== tabFocus) redrawLegend(); };
  const onFocusOut = () => { if (tabFocus) setTimeout(() => { if (tabFocus && document.activeElement !== tabFocus) { tabFocus = null; redrawLegend(); } }, 0); };
  const onPointerCapture = () => { const was = tabFocus; tabFocus = null; lastKeyTab = false; if (was) redrawLegend(); };
  /** The legend again, as it stands (a Tab-focused button names its Enter there). */
  function redrawLegend() { try { if (alive && legend) drawLegend(legendNode); } catch (e) { /* not built yet */ } }
  /** A button's own words (its first label span, else its text): what Enter presses. */
  function buttonLabel(b) {
    if (b.classList.contains('ocbg-explore-trace-row')) return T.KeyToggle; // a trace candidate: Enter picks or drops it
    const sp = b.querySelector(':scope > span');
    return ((sp ? sp.textContent : b.textContent) || '').trim().split('\n')[0];
  }
  window.addEventListener('keydown', onKeyCapture, true);
  document.addEventListener('focusin', onFocusIn);
  document.addEventListener('focusout', onFocusOut);
  window.addEventListener('pointerdown', onPointerCapture, true);
  /** The enabled button inside `scope` that Tab moved the focus to, or null. */
  function focusedButton(scope) {
    const el = document.activeElement;
    if (!el || el !== tabFocus || !scope || !scope.contains(el)) return null;
    // under a confirmation only its own buttons answer (what is behind it waits)
    const confirm = root.querySelector('.ocbg-confirm');
    if (confirm && !confirm.contains(el)) return null;
    return el.tagName === 'BUTTON' && !el.disabled ? el : null;
  }
  /** Enter / Space on a focused button presses it (and nothing else); true when it did. */
  function pressFocused(e, scope) {
    if ((e.key !== 'Enter' && e.key !== ' ') || e.ctrlKey || e.metaKey || e.altKey) return false;
    const b = focusedButton(scope);
    if (!b) return false;
    b.click();
    return true;
  }
  /** pushKeys, with a focused button's Enter / Space first. */
  const keysWithFocus = (handler) => pushKeys((e) => pressFocused(e, root) || handler(e));
  /** confirmCard (stage.js), whose own Enter confirms: a focused button of the card is pressed instead. */
  function ask(opts) {
    const answer = confirmCard(root, opts);
    const shades = root.querySelectorAll('.ocbg-confirm');
    const shade = shades[shades.length - 1] || null;
    const pop = pushKeys((e) => pressFocused(e, shade));
    return answer.then((v) => { pop(); return v; });
  }

  // ------------------------------------------------------------------ DOM
  const root = h('div', { class: 'ocbg-explore' });
  host.appendChild(root);
  const bdHost = h('div', { class: 'ocbg-explore-bdhost' });
  const mapLayer = h('div', { class: 'ocbg-explore-map' });
  const edgesSvg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  edgesSvg.setAttribute('class', 'ocbg-explore-edges');
  edgesSvg.setAttribute('viewBox', '0 0 1920 1080');
  const nodesEl = h('div', { class: 'ocbg-explore-nodes' });
  const selFrame = h('div', { class: 'ocbg-explore-sel' });
  const SEL = { w: NODE + 44, h: NODE + 58 };
  selFrame.appendChild(window.OCB.TargetFrame(SEL.w, SEL.h, { hook: 20, width: 3, glow: 12 }));
  const walker = h('div', { class: 'ocbg-explore-walker' },
    h('i', { class: 'ocbg-explore-walker-glow' }),
    h('span', { class: 'ocbg-explore-walker-disc' }, ico('person', 24, 'var(--ink)')));
  // the edge being walked lights up behind the walker (drawn into the edge layer while a step runs)
  const trail = document.createElementNS('http://www.w3.org/2000/svg', 'line');
  trail.setAttribute('class', 'ocbg-explore-trail');
  const banner = h('div', { class: 'ocbg-explore-banner' });
  // the lamp's pool of light on the ground where the player stands (world-v1 §3 rule 4: amber is where the player is)
  const pool = h('div', { class: 'ocbg-explore-pool' });
  mapLayer.append(pool, edgesSvg, nodesEl, selFrame, walker);

  const detail = place(h('div', { class: 'ocbg-explore-detail' }), DETAIL.x, DETAIL.y, DETAIL.w, DETAIL.h);
  const lull = h('div', { class: 'ocbg-explore-lullview' });
  const cardLayer = h('div', { class: 'ocbg-explore-cards' });
  const carried = place(h('div', { class: 'ocbg-explore-carried' }), CARRIED.x, CARRIED.y, CARRIED.w, CARRIED.h);
  const band = h('div', { class: 'ocbg-explore-band' });
  const endLifeBtn = place(h('button', { class: 'ocbg-btn ocbg-explore-endlife', type: 'button' }, ExplorationText.EndLifeButton), 24, 980, 288, 68);
  const hint = place(h('div', { class: 'ocbg-explore-hint' }), 336, 980, 724, 68);
  // the key legend runs right-aligned from the right edge; a card's longer key list may reach left past the panels
  const legend = place(h('div', { class: 'ocbg-explore-legend' }), 1068, 976, 828, 76);
  const overlayLayer = h('div', { class: 'ocbg-explore-overlays' });
  root.append(bdHost, mapLayer, banner, detail, lull, cardLayer, carried, endLifeBtn, hint, legend, band, overlayLayer);

  const backdrop = createBackdrop(bdHost);

  // ---- the band ----
  const layerTitle = h('span', { class: 'ocbg-explore-layer-title' });
  let shownLayerTitle = null;
  const timeLabel = h('span', { class: 'ocbg-explore-band-label' });
  const timeBonus = h('span', { class: 'ocbg-explore-bonus' });
  const timePips = h('div', { class: 'ocbg-explore-pips' });
  const reliefLabel = h('div', { class: 'ocbg-explore-relief' });
  const miasmaLabel = h('span', { class: 'ocbg-explore-band-label' });
  const miasmaForecast = h('span', { class: 'ocbg-explore-forecast' });
  const gaugeAhead = h('i', { class: 'ocbg-explore-gauge-ahead' });
  const gaugeFill = h('i', { class: 'ocbg-explore-gauge-fill' });
  const gaugeTicks = h('div', { class: 'ocbg-explore-gauge-ticks' });
  for (const tick of MiasmaLook.Ticks()) {
    gaugeTicks.appendChild(h('i', { class: tick.IsStepLine ? 'is-line' : null, style: { left: tick.Percent + '%', height: tick.Height * 100 + '%', background: rgbaCss(MiasmaLook.Tick) } }));
  }
  const gauge = h('div', { class: 'ocbg-explore-gauge', style: { background: rgbaCss(MiasmaLook.Track), 'border-color': rgbaCss(MiasmaLook.Frame) } }, gaugeAhead, gaugeFill, gaugeTicks);
  const hpLabel = h('span', { class: 'ocbg-explore-band-label' });
  const stLabel = h('span', { class: 'ocbg-explore-band-label' });
  const maxLabel = h('span', { class: 'ocbg-explore-small' });
  const marksBtn = h('button', { class: 'ocbg-explore-marks', type: 'button' });
  const journalBtn = h('button', { class: 'ocbg-explore-bandbtn', type: 'button' }, ico('journal', 28, 'var(--ink)'), h('span', null, ExplorationText.JournalButton));
  const cardsBtn = h('button', { class: 'ocbg-explore-bandbtn', type: 'button' }, ico('cards', 28, 'var(--ink)'), h('span', null, ExplorationText.CardsButton));
  const settingsBtn = h('button', { class: 'ocbg-explore-bandbtn', type: 'button' }, ico('gear', 28, 'var(--ink)'), h('span', null, ExplorationText.SettingsButton));
  band.append(
    h('div', { class: 'ocbg-explore-band-layer' }, ico('stairs', 28, 'var(--ink)'), layerTitle),
    h('div', { class: 'ocbg-explore-band-time' }, h('div', { class: 'ocbg-explore-row' }, ico('hourglass', 22, 'var(--ink)'), timeLabel), timePips, timeBonus),
    h('div', { class: 'ocbg-explore-band-miasma' }, h('div', { class: 'ocbg-explore-row' }, ico('miasma', 24, 'var(--phosphor)'), miasmaLabel, miasmaForecast), gauge, reliefLabel),
    h('div', { class: 'ocbg-explore-band-body' },
      h('div', { class: 'ocbg-explore-row' }, ico('heart', 22, 'var(--crimson)'), hpLabel),
      h('div', { class: 'ocbg-explore-row' }, ico('pip', 20, 'var(--amber)'), stLabel),
      marksBtn, maxLabel),
    h('div', { class: 'ocbg-explore-band-buttons' }, journalBtn, cardsBtn, settingsBtn));
  const timeRoll = new RollLabel(timeLabel);
  const miasmaRoll = new RollLabel(miasmaLabel);
  const hpRoll = new RollLabel(hpLabel);
  const stRoll = new RollLabel(stLabel);
  const maxRoll = new RollLabel(maxLabel);
  const marksRoll = new RollLabel(marksBtn);
  const rollers = [timeRoll, miasmaRoll, hpRoll, stRoll, maxRoll, marksRoll];

  // ---- the key legend: the keys of what can be done now (labels from the bridge where it has them) ----
  let legendNode = null;
  function drawLegend(node) {
    legendNode = node;
    legend.textContent = '';
    const key = (keys, label) => legend.appendChild(h('span', null, [].concat(keys).map((k) => h('kbd', null, k)), label));
    // A button Tab moved the focus to takes Enter and Space first (X-8): the legend names that button for Enter, and
    // leaves out the screen's own Enter / Space, which would not act.
    const focused = focusedButton(root);
    if (focused) key(T.KeyEnter, buttonLabel(focused));
    const pressKeys = (keys) => [].concat(keys).some((k) => k === T.KeyEnter || k === 'Space');
    if (plate && plate.legend) {
      // a card over the map lists its own keys
      for (const [keys, label] of plate.legend) if (!focused || !pressKeys(keys)) key(keys, label);
    } else if (focused) {
      const live = screen && screen.MapIsLive && !busy && !plate;
      if (live) key('←→↑↓', T.KeySelect);
      if (live && screen.Aiming) key('Esc', T.StopAim);
    } else {
      const live = screen && screen.MapIsLive && !busy && !plate;
      if (live) key('←→↑↓', T.KeySelect);
      if (live && node) node.Options.forEach((opt, i) => key(i === 0 ? T.KeyEnter : [T.KeyShift, T.KeyEnter], opt.Label));
      if (live && screen.Aiming) key('Esc', T.StopAim);
      if (screen && !plate && !busy && session.State.Phase === RunPhase.Interlude && screen.Card.Buttons[0]) key(T.KeyEnter, screen.Card.Buttons[0].Label);
    }
    if (screen && screen.Consumables.some((c) => c.CanUse) && (!plate || plate.kind === 'screen')) key('1–3', T.KeyItems);
    fitHint();
  }

  /**
   * The hint runs from 「この生を終える」 up to where the key legend's keys start (the legend is right-aligned and its
   * width follows what can be done now), so a long hint — 標の石's aim — keeps to one line where there is room.
   */
  const HINT = { x: 336, w: 724, gap: 28 };
  const LEGEND_RIGHT = 1068 + 828;
  function fitHint() {
    const kids = [...legend.children];
    const used = kids.reduce((sum, k) => sum + k.offsetWidth, 0) + Math.max(0, kids.length - 1) * 20;
    const w = Math.max(HINT.w, LEGEND_RIGHT - used - HINT.gap - HINT.x);
    hint.style.width = w + 'px';
  }

  // ------------------------------------------------------------------ state
  let alive = true;
  let screen = null;
  let pinned = -1;
  let hovered = -1;
  let lastCurrent = -1;
  let mapRef = null;
  let lastLayer = 0;
  let lastAmbience = null;
  let stepInfo = null; // the screen and node of a step waiting on its confirmation
  let busy = false; // a step, battle or plate is in progress: the map takes no input
  let plate = null; // the open card over the map (ours)
  let confirmOpen = false;
  let overlayOpen = null;
  let flowRunning = false;
  let pendingTrace = null;
  let pendingTraining = false;
  let battleFallback = false;
  let walkerAnim = null;
  let gaugeFrom = 0;
  let gaugeTo = 0;
  let gaugeAt = 0;
  let gaugeNext = 0;
  let firstRender = true;
  let hintOverride = null;
  let inBattle = false; // the app's battle scene is up: the backdrop rests
  let shownResult = null; // trial: the pending result (Expedition.PendingResult) whose card this mount opened last
  let traceOffered = ''; // 'layer:node' of the trace card this mount opened (a reload opens it again: R1-16)
  let shownTaken = null; // trial: the pending 受け取った遺産 card (Expedition.PendingTaken) this mount opened last
  let overlayClose = null; // closes the fallback overlay (destroy)
  const nodeEls = new Map();
  const edgeEls = [];

  const clock = createClock((dt, now) => {
    if (inBattle) return;
    backdrop.frame(now);
    for (const r of rollers) r.tick(now);
    paintGauge(now);
    if (walkerAnim) walkerAnim(now);
  });

  // ------------------------------------------------------------------ helpers
  function nodePos(n) {
    return { x: AREA.cx + (n.X - 0.5) * AREA.w, y: AREA.cy + (0.5 - n.Y) * AREA.h };
  }

  /**
   * The walker at node centre (x, y). `perch` 1 stands it on the node's top edge, 0 puts it on the
   * edge line itself (mid-step); `hop` lifts it for the stride.
   */
  function setWalker(x, y, perch = 1, hop = 0) {
    walker.style.transform = `translate(${x - WALKER_BOX / 2}px, ${y - WALKER_UP * perch - WALKER_BOX / 2 - hop}px)`;
    pool.style.transform = `translate(${x - 260}px, ${y - 200}px)`;
  }

  function paintGauge(now) {
    const shown = MiasmaLook.Rise(gaugeFrom, gaugeTo, now - gaugeAt);
    gaugeFill.style.width = Math.max(0, Math.min(100, shown)) + '%';
    gaugeFill.style.background = rgbaCss(MiasmaLook.FillAt(shown), MiasmaLook.FillAlpha(gaugeTo, now));
    gaugeAhead.style.width = Math.max(0, Math.min(100, gaugeNext)) + '%';
  }

  /**
   * The node icon's colour by role: an elite is the enemy's violet (phosphor is the miasma's icon),
   * the boss its own colour with the double frame, a trace of the previous heir the player's amber.
   */
  function kindColor(kind) {
    switch (kind) {
      case NodeKind.Elite: return 'var(--violet)';
      case NodeKind.Boss: return 'var(--boss)';
      case NodeKind.Trace: return 'var(--amber)';
      default: return 'var(--ink)';
    }
  }
  function nodeIconColor(n) {
    return n.Standing === NodeStanding.Far ? 'var(--steel)' : kindColor(n.Kind);
  }

  // ------------------------------------------------------------------ the map
  function buildMap(scr, enter) {
    nodesEl.textContent = '';
    edgesSvg.textContent = '';
    nodeEls.clear();
    edgeEls.length = 0;
    const SVG = 'http://www.w3.org/2000/svg';
    for (const e of scr.Edges) {
      const a = nodePos(scr.Node(e.From));
      const b = nodePos(scr.Node(e.To));
      const len = Math.hypot(b.x - a.x, b.y - a.y);
      const k = EDGE_INSET / len;
      const x1 = a.x + (b.x - a.x) * k;
      const y1 = a.y + (b.y - a.y) * k;
      const x2 = b.x - (b.x - a.x) * k;
      const y2 = b.y - (b.y - a.y) * k;
      const g = document.createElementNS(SVG, 'g');
      const base = document.createElementNS(SVG, 'line');
      const flow = document.createElementNS(SVG, 'line');
      for (const ln of [base, flow]) {
        ln.setAttribute('x1', x1); ln.setAttribute('y1', y1); ln.setAttribute('x2', x2); ln.setAttribute('y2', y2);
      }
      base.setAttribute('class', 'ocbg-explore-edge');
      flow.setAttribute('class', 'ocbg-explore-edge-flow');
      g.append(base, flow);
      if (enter) {
        const L = Math.hypot(x2 - x1, y2 - y1);
        base.style.strokeDasharray = `${L}`;
        base.style.strokeDashoffset = `${L}`;
        const row = scr.Node(e.From).Row;
        setTimeout(() => { base.style.transition = `stroke-dashoffset ${dur(520)}ms cubic-bezier(.22,.61,.36,1)`; base.style.strokeDashoffset = '0'; }, dur(260 + row * 90));
        setTimeout(() => { base.style.strokeDasharray = ''; base.style.transition = ''; }, dur(260 + row * 90 + 560));
      }
      edgesSvg.appendChild(g);
      edgeEls.push({ edge: e, g, base, flow, coords: { x1, y1, x2, y2 } });
    }
    for (const n of scr.Nodes) {
      const p = nodePos(n);
      const face = h('span', { class: 'ocbg-explore-node-face' });
      const label = h('span', { class: 'ocbg-explore-node-label' }, n.KindLabel);
      const el = h('div', { class: 'ocbg-explore-node kind-' + KIND_CLASS[n.Kind], role: 'button', 'data-node': String(n.Id), 'aria-label': n.Title },
        h('span', { class: 'ocbg-explore-node-lift' }, face, label));
      place(el, p.x - NODE / 2, p.y - NODE / 2, NODE, NODE);
      el.addEventListener('mouseenter', () => onHover(n.Id, true));
      el.addEventListener('mouseleave', () => onHover(n.Id, false));
      el.addEventListener('click', () => onNodeClick(n.Id));
      if (enter) {
        el.style.opacity = '0';
        el.style.transform = 'translateY(14px)';
        setTimeout(() => {
          el.style.transition = `opacity ${dur(420)}ms ease-out, transform ${dur(420)}ms cubic-bezier(.22,.61,.36,1)`;
          el.style.opacity = '';
          el.style.transform = '';
        }, dur(200 + n.Row * 90));
        setTimeout(() => { el.style.transition = ''; }, dur(200 + n.Row * 90 + 460));
      }
      nodesEl.appendChild(el);
      nodeEls.set(n.Id, { el, face, label, kind: n.Kind, standing: null });
    }
  }

  function updateMap(scr) {
    for (const n of scr.Nodes) {
      const rec = nodeEls.get(n.Id);
      if (!rec) continue;
      if (rec.standing !== n.Standing) {
        rec.standing = n.Standing;
        rec.el.className = 'ocbg-explore-node kind-' + KIND_CLASS[n.Kind] + ' ' + STANDING_CLASS[n.Standing];
        rec.face.textContent = '';
        rec.face.appendChild(ico(n.Kind, 44, nodeIconColor(n)));
        rec.el.setAttribute('aria-label', n.Title + '　' + T.Standing[n.Standing]);
      }
      rec.el.classList.toggle('is-live', scr.MapIsLive && !busy);
      rec.el.classList.toggle('is-pinned', n.Id === pinned);
      // trial: while a 標の石 is aimed, the nodes it can land on are marked
      rec.el.classList.toggle('is-jump', !!scr.Aiming && scr.MapIsLive && !busy && n.Options.some((o) => o.Jump));
    }
    const walked = (s) => s === NodeStanding.Current || s === NodeStanding.Revisit || s === NodeStanding.Resolved;
    const byKey = new Map(edgeEls.map((r) => [r.edge.From + '-' + r.edge.To, r]));
    for (const edge of scr.Edges) {
      const rec = byKey.get(edge.From + '-' + edge.To);
      if (!rec) continue;
      const a = scr.Node(edge.From);
      const b = scr.Node(edge.To);
      const lit = edge.Lit && scr.MapIsLive && !busy && !scr.Aiming;
      rec.g.setAttribute('class', lit ? 'is-lit' : walked(a.Standing) && walked(b.Standing) ? 'is-walked' : '');
      if (lit) {
        // the flow runs from where the player stands toward where they can go
        const c = rec.coords;
        const fromCurrent = a.Standing === NodeStanding.Current;
        rec.flow.setAttribute('x1', fromCurrent ? c.x1 : c.x2); rec.flow.setAttribute('y1', fromCurrent ? c.y1 : c.y2);
        rec.flow.setAttribute('x2', fromCurrent ? c.x2 : c.x1); rec.flow.setAttribute('y2', fromCurrent ? c.y2 : c.y1);
      }
    }
    const pn = scr.Node(pinned);
    if (pn) {
      const p = nodePos(pn);
      selFrame.style.transform = `translate(${p.x - SEL.w / 2}px, ${p.y - NODE / 2 - 18}px)`;
      selFrame.classList.add('is-on');
    } else selFrame.classList.remove('is-on');
  }

  function onHover(id, enter) {
    if (!screen) return;
    if (enter) {
      if (hovered !== id) sfx('nodeHover');
      hovered = id;
    } else if (hovered === id) hovered = -1;
    drawDetail();
  }

  function onNodeClick(id) {
    if (!screen || !screen.MapIsLive || busy || plate) return;
    const node = screen.Node(id);
    if (!node) return;
    if (pinned === id && node.Options.length > 0) {
      takeOption(node, 0);
      return;
    }
    pinned = id;
    sfx('uiClick');
    updateMap(screen);
    drawDetail();
  }

  /** Arrow keys: the nearest node in that direction, preferring the ones the player can walk to. */
  function moveSelection(dx, dy) {
    if (!screen) return;
    const from = screen.Node(pinned) || screen.Node(screen.CurrentNodeId);
    const p0 = nodePos(from);
    let best = null;
    let bestScore = Infinity;
    for (const n of screen.Nodes) {
      if (n.Id === from.Id) continue;
      const p = nodePos(n);
      const vx = p.x - p0.x;
      const vy = p.y - p0.y;
      const along = vx * dx + vy * dy;
      if (along <= 4) continue;
      const across = Math.abs(vx * dy - vy * dx);
      let score = along + across * 2.2;
      if (n.Options.length > 0) score *= 0.6;
      if (score < bestScore) { bestScore = score; best = n; }
    }
    if (!best) return;
    pinned = best.Id;
    hovered = -1;
    sfx('nodeHover');
    updateMap(screen);
    drawDetail();
  }

  // ------------------------------------------------------------------ the node detail
  let detailKey = '';
  let detailScreen = null;
  function drawDetail() {
    if (!screen) return;
    const hovering = hovered >= 0 && hovered !== pinned;
    const node = screen.Node(hovering ? hovered : pinned);
    const key = (node ? node.Id : -1) + '|' + hovering + '|' + busy + '|' + !!plate;
    if (screen === detailScreen && key === detailKey) return;
    drawLegend(hovering ? screen.Node(pinned) : node);
    detailScreen = screen;
    detailKey = key;
    detail.textContent = '';
    if (!node) return;
    const head = h('div', { class: 'ocbg-explore-detail-head' },
      h('span', { class: 'ocbg-explore-detail-token kind-' + KIND_CLASS[node.Kind] + ' ' + STANDING_CLASS[node.Standing] }, ico(node.Kind, 40, nodeIconColor(node))),
      h('div', { class: 'ocbg-explore-detail-titles' },
        h('h2', null, node.KindLabel),
        h('p', null, node.Note)),
      h('span', { class: 'ocbg-explore-pill ' + STANDING_CLASS[node.Standing] }, T.Standing[node.Standing]));
    detail.appendChild(h('span', { class: 'ocbg-explore-detail-mark' }, ico(node.Kind, 260, 'var(--ink)', { width: 1.2 })));
    detail.appendChild(head);
    if (node.Lines.length) {
      const rows = h('div', { class: 'ocbg-explore-detail-lines' });
      for (const line of node.Lines) {
        const [k, v] = splitRow(line);
        rows.appendChild(h('div', { class: 'ocbg-explore-kv' + (k ? '' : ' is-wide') }, k ? h('span', { class: 'ocbg-explore-k' }, k) : null, rich(v, 'ocbg-explore-v')));
      }
      detail.appendChild(rows);
    }
    const options = hovering || busy ? [] : node.Options;
    if (options.length) {
      const cols = h('div', { class: 'ocbg-explore-options n' + options.length });
      options.forEach((opt, i) => {
        // a way in without a forecast (「移る」 to a walked node) is just its button, not an empty box
        const col = h('div', { class: 'ocbg-explore-option' + (opt.NeedsConfirm ? ' is-lethal' : '') + (opt.Forecast ? '' : ' is-bare') });
        if (opt.Forecast) {
          col.appendChild(h('div', { class: 'ocbg-explore-option-label' }, '【' + opt.Label + '】'));
          for (const line of opt.Forecast.Lines) {
            const icon = forecastIcon(line);
            col.appendChild(h('div', { class: 'ocbg-explore-fc' }, icon ? ico(icon[0], 20, icon[1], icon[2]) : h('i', { class: 'ocbg-explore-fc-dot' }), rich(line)));
          }
          for (const w of opt.Forecast.Warnings) {
            col.appendChild(h('div', { class: 'ocbg-explore-warn' }, ico('caution', 22, 'var(--caution)'), h('span', null, w)));
          }
        }
        const btn = h('button', { class: 'ocbg-btn is-primary ocbg-explore-go' + (opt.NeedsConfirm ? ' is-danger' : ''), type: 'button', onclick: () => takeOption(node, i) },
          h('span', null, opt.Label));
        col.appendChild(h('div', { class: 'ocbg-explore-option-foot' }, btn));
        cols.appendChild(col);
      });
      detail.appendChild(cols);
    } else if (!hovering && screen.MapIsLive && node.Standing !== NodeStanding.Current && node.Lines.length === 0) {
      detail.appendChild(h('p', { class: 'ocbg-explore-detail-far' }, ExplorationText.FarNode));
    }
    // A rest node with both forecasts and their warnings can outgrow the panel: set it tighter, step
    // by step, so the buttons always stay inside (never clipped away).
    detail.classList.remove('is-tight', 'is-tighter');
    for (const cls of ['is-tight', 'is-tighter']) {
      if (detail.scrollHeight <= detail.clientHeight + 1) break;
      detail.classList.add(cls);
    }
  }

  // ------------------------------------------------------------------ carried items
  function drawCarried() {
    carried.textContent = '';
    const trial = session.Trial;
    carried.classList.toggle('is-trial', trial);
    if (trial && screen.ToolCards.length) {
      // trial: each tool as a chip with what it does; its note on hover (tools_and_consumables_v4 §2)
      const tools = h('div', { class: 'ocbg-explore-tools is-chips' }, ico('tool', 22, 'var(--ink)'));
      for (const t of screen.ToolCards) {
        const battle = t.Side === ExplorationText.SideBattle;
        const chip = h('span', { class: 'ocbg-explore-tool' + (battle ? ' is-battle' : '') },
          h('span', { class: 'ocbg-explore-tool-name' }, t.Name),
          h('span', { class: 'ocbg-explore-tool-effect' }, rich(t.Effect || t.Side)));
        chip.addEventListener('mouseenter', () => { hintOverride = T.ToolNote(t.Name, t.Side, t.Note); drawHint(); });
        chip.addEventListener('mouseleave', () => { hintOverride = null; drawHint(); });
        tools.appendChild(chip);
      }
      carried.appendChild(tools);
    } else {
      carried.appendChild(h('div', { class: 'ocbg-explore-tools' }, ico('tool', 22, 'var(--ink)'), rich(screen.ToolsLabel)));
    }
    const slots = h('div', { class: 'ocbg-explore-slots' });
    for (const slot of screen.Consumables) {
      const empty = slot.Name === ExplorationText.EmptySlot;
      const id = empty ? null : (slot.Id || session.State.Loadout.Consumables[slot.Index]?.Id);
      // trial: what using it changes (or why not), under its name — one line per change, so a forecast of two
      // changes (浄化の香 across a 20% line) wraps inside its slot instead of widening the row
      const extra = trial && !empty ? (slot.Forecast ? slot.Forecast.join('　') : slot.Reason) : '';
      const extraEl = !extra ? null : slot.Forecast
        ? h('span', { class: 'ocbg-explore-slot-fc' }, slot.Forecast.map((l) => rich(l, 'ocbg-explore-slot-fcl')))
        : h('span', { class: 'ocbg-explore-slot-fc is-reason' }, rich(extra));
      const el = h('button', { class: 'ocbg-explore-slot' + (empty ? ' is-empty' : '') + (slot.CanUse ? ' is-usable' : '') + (slot.Aiming ? ' is-aiming' : ''), type: 'button', disabled: slot.CanUse || slot.Aiming ? null : 'disabled' },
        h('kbd', null, String(slot.Index + 1)),
        empty ? null : ico(itemIcon(id), trial ? 34 : 40, 'var(--ink)'),
        h('span', { class: 'ocbg-explore-slot-name' }, slot.Name),
        extraEl,
        slot.CanUse || slot.Aiming ? h('span', { class: 'ocbg-explore-slot-use' }, slot.Aiming ? T.StopAim : T.Use) : null);
      el.addEventListener('mouseenter', () => { hintOverride = slot.Note ? T.SlotHint(slot.Note, extra) : null; drawHint(); });
      el.addEventListener('mouseleave', () => { hintOverride = null; drawHint(); });
      el.addEventListener('click', () => useConsumable(slot.Index));
      slots.appendChild(el);
    }
    carried.appendChild(slots);
    // trial: the found consumables are in the slots (or were swapped away), so only the pages are listed
    const found = trial ? [] : expedition.Consumables;
    // The pages a survivor's journal or a trace handed down are the life's too, but not found here (INHERITANCE.md
    // R-27): they get their own row, 受け継いだ物, and 拾った物 counts only what this life picked up.
    const inherited = Math.max(0, Math.min(expedition.Pages, expedition.InheritedPages || 0));
    const pages = expedition.Pages - inherited;
    if (found.length || pages) {
      const row = h('div', { class: 'ocbg-explore-found', title: trial ? T.FoundNoteTrial : T.FoundNote }, h('span', { class: 'ocbg-explore-k' }, T.Found));
      const counts = new Map();
      for (const id of found) counts.set(id, (counts.get(id) || 0) + 1);
      for (const [id, n] of counts) {
        row.appendChild(h('span', { class: 'ocbg-explore-chip' }, ico(itemIcon(id), 18, 'var(--ink)'), ExplorationText.ItemName(id), n > 1 ? rich('×' + n) : null));
      }
      if (pages) row.appendChild(h('span', { class: 'ocbg-explore-chip', title: T.PagesNote }, ico('page', 18, 'var(--ink)'), rich(T.Pages(pages))));
      carried.appendChild(row);
    }
    if (inherited) {
      carried.appendChild(h('div', { class: 'ocbg-explore-found is-inherited', title: T.InheritedNote },
        h('span', { class: 'ocbg-explore-k' }, T.Inherited),
        h('span', { class: 'ocbg-explore-chip', title: T.PagesNote }, ico('page', 18, 'var(--ink)'), rich(T.Pages(inherited)))));
    }
    fitCarried();
  }

  /**
   * The carried panel keeps its bottom edge and grows upward when what it lists needs more room (the
   * survivor's 4th tool wraps the chips onto a second row: every tool stays listed, W-2 / L-7); the node
   * detail above gives up the same height and sets itself tighter if it must (drawDetail).
   */
  function fitCarried() {
    carried.style.height = 'auto';
    const need = Math.max(CARRIED.h, Math.ceil(carried.offsetHeight));
    const top = CARRIED.y + CARRIED.h - need;
    carried.style.top = top + 'px';
    carried.style.height = need + 'px';
    const dh = top - 12 - DETAIL.y;
    if (detail.style.height !== dh + 'px') { detail.style.height = dh + 'px'; detailKey = ''; }
  }

  function drawHint() {
    hint.textContent = '';
    // the map's hint speaks of the map: under a card that waits on the player it steps back (the card has its keys)
    const text = hintOverride || (screen && !(plate && plate.kind !== 'screen') ? screen.Hint : '');
    if (text) hint.appendChild(rich(text));
    fitHint();
  }

  // ------------------------------------------------------------------ the band
  function drawBand(now, animate) {
    if (screen.LayerTitle !== shownLayerTitle) {
      shownLayerTitle = screen.LayerTitle;
      layerTitle.textContent = '';
      layerTitle.appendChild(rich(screen.LayerTitle));
    }
    timeRoll.set(screen.TimeLabel, now, animate);
    timeBonus.textContent = screen.TimeBonusLabel || '';
    timeBonus.style.display = screen.TimeBonusLabel ? '' : 'none';
    reliefLabel.textContent = '';
    if (screen.ReliefLabel) reliefLabel.appendChild(rich(screen.ReliefLabel));
    reliefLabel.style.display = screen.ReliefLabel ? '' : 'none';
    timePips.classList.toggle('is-long', screen.TimeLimit > 10);
    timePips.textContent = '';
    for (let i = 0; i < screen.TimeLimit; i++) timePips.appendChild(h('i', { class: i < screen.TimeLeft ? 'is-left' : '' }));
    const g = screen.Miasma;
    miasmaRoll.set(g.Label, now, animate);
    miasmaForecast.textContent = '';
    if (g.ForecastLabel) {
      const mark = MiasmaLook.ForecastMark(g.Percent, g.NextPercent);
      if (mark) miasmaForecast.appendChild(ico('caution', 18, 'var(--caution)'));
      miasmaForecast.appendChild(rich(g.ForecastLabel));
    }
    if (g.Percent !== gaugeTo || firstRender) {
      gaugeFrom = firstRender || !animate ? g.Percent : MiasmaLook.Rise(gaugeFrom, gaugeTo, now - gaugeAt);
      gaugeTo = g.Percent;
      gaugeAt = now;
    }
    gaugeNext = g.NextPercent;
    gaugeAhead.style.background = rgbaCss(MiasmaLook.AheadColor(g.Percent, g.NextPercent));
    gauge.classList.toggle('is-warn', MiasmaLook.Warning(g.Percent, g.NextPercent) !== MiasmaWarning.None);
    backdrop.setMiasma(g.Percent, now, firstRender || !animate);
    hpRoll.set(screen.HpLabel, now, animate);
    stRoll.set(screen.StaminaLabel, now, animate);
    maxRoll.set(screen.MaxStaminaLabel, now, animate);
    marksRoll.set(screen.MarksLabel, now, animate);
    syncMarks();
    cardsBtn.disabled = !screen.CanOpenCards;
    settingsBtn.disabled = !screen.CanOpenSettings;
  }

  // ------------------------------------------------------------------ render
  function render(opts = {}) {
    if (!alive) return;
    const now = performance.now();
    const animate = !firstRender && opts.animate !== false;
    screen = session.Screen();
    const state = session.State;
    const layer = state.Profile.Layer;

    if (state.CurrentNodeId !== lastCurrent) {
      lastCurrent = state.CurrentNodeId;
      pinned = state.CurrentNodeId;
    }
    if (!screen.Node(pinned)) pinned = screen.CurrentNodeId;

    if (layer !== lastLayer) backdrop.show(layer, now);
    const mapChanged = state.Map !== mapRef;
    if (mapChanged) {
      mapRef = state.Map;
      buildMap(screen, !firstRender || opts.enter);
      if (!walkerAnim) {
        const p = nodePos(screen.Node(screen.CurrentNodeId));
        setWalker(p.x, p.y);
      }
    }
    lastLayer = layer;

    drawBand(now, animate);
    updateMap(screen);
    if (!walkerAnim && !mapChanged) {
      const p = nodePos(screen.Node(screen.CurrentNodeId));
      setWalker(p.x, p.y);
    }
    drawCarried(); // first: it may take height from the detail panel (fitCarried)
    drawDetail();
    drawHint();
    endLifeBtn.style.display = screen.ShowsEndLife ? '' : 'none';
    syncEndLife();

    // the interlude replaces the map (§6.5); the backdrop rests under a warm lull
    const interlude = state.Phase === RunPhase.Interlude;
    root.classList.toggle('is-interlude', interlude);
    backdrop.setLull(interlude);
    const amb = interlude ? 'interlude' : layer;
    if (amb !== lastAmbience) { lastAmbience = amb; ambience(amb); }
    drawInterlude(interlude);

    root.classList.toggle('is-busy', busy);
    drawScreenCard();
    firstRender = false;
  }

  // ------------------------------------------------------------------ cards over the map
  function closePlate() {
    if (!plate) return;
    const p = plate;
    plate = null;
    carried.classList.remove('is-held');
    syncMarks();
    syncEndLife();
    if (p.pop) p.pop();
    p.el.classList.add('is-leaving');
    p.dim.classList.add('is-leaving');
    setTimeout(() => { p.el.remove(); p.dim.remove(); }, dur(220));
    detailKey = '';
    drawDetail();
    drawHint();
  }

  /**
   * A card over the map: dims the map and the detail, leaves the band and the carried items above it.
   * `ht` null lets the card take the height of what it holds; call fitPlate() once it is filled.
   * `legend` lists the card's keys in the key legend ([[keys, label], …]).
   */
  function openPlate({ cls, w, ht, keys, kind, x, y, legend }) {
    closePlate();
    const dim = h('div', { class: 'ocbg-explore-dim' });
    const el = h('div', { class: 'ocbg-explore-plate ' + (cls || '') });
    place(el, x != null ? x : Math.round(MAP_CENTER.x - w / 2), y != null ? y : Math.round(MAP_CENTER.y - (ht || 0) / 2), w, ht);
    cardLayer.append(dim, el);
    plate = { el, dim, kind, legend: legend || null, pop: keys ? keysWithFocus(keys) : null };
    carried.classList.toggle('is-held', kind !== 'screen'); // the slots wait under this card (W-8)
    syncMarks();
    syncEndLife();
    detailKey = '';
    drawDetail();
    drawHint();
    return el;
  }

  /** Centres an auto-height card on the map once its content is in (it never rises under the band). */
  function fitPlate(el) {
    const ht = el.offsetHeight;
    el.style.top = Math.max(120, Math.round(MAP_CENTER.y - ht / 2)) + 'px';
  }

  /**
   * The training mark can be spent now: a mark is left and nothing waits, on the map, under the layer-break card or
   * at the interlude (the slots' rule, W-8) — the button is disabled exactly where a click would do nothing.
   */
  function canTrainNow() {
    return session.State.TrainingMarks > 0 && session.IsIdle && !session.State.IsOver && !busy && !confirmOpen &&
      !flowRunning && (!plate || plate.kind === 'screen');
  }
  function syncMarks() { marksBtn.disabled = !canTrainNow(); }
  /**
   * 「この生を終える」 can be pressed at any time while exploring (§6.6, §5-7) — except under a card that waits on the
   * player (a result, the trace, training, a choice) or a confirmation: there it is held, and looks held (disabled),
   * like the layer-break and interlude buttons beside it, instead of glowing while a click does nothing.
   */
  function syncEndLife() {
    if (!screen) return;
    endLifeBtn.disabled = !screen.CanEndLife || busy || confirmOpen || !!(plate && plate.kind !== 'screen');
  }

  function cardButtons(card, onAct) {
    const row = h('div', { class: 'ocbg-explore-plate-buttons' });
    card.Buttons.forEach((b, i) => {
      row.appendChild(h('button', { class: 'ocbg-btn' + (i === 0 ? ' is-primary' : ''), type: 'button', onclick: () => onAct(b.Action) }, b.Label));
    });
    return row;
  }

  function drawScreenCard() {
    const card = screen.Card;
    if (busy || flowRunning) return;
    if (!card.IsShown || card.Kind === CardKind.Interlude) {
      if (plate && plate.kind === 'screen') closePlate();
      return;
    }
    if (card.IsConfirmation) {
      if (plate && plate.kind === 'screen') closePlate();
      if (!confirmOpen) openConfirm(card);
      return;
    }
    if (card.Kind === CardKind.Battle && !battleFallback) return; // the battle flow handles it
    if (card.IsChoice) { drawChoiceCard(card); return; } // trial mode's event / swap / survey cards
    const key = card.Kind + '|' + card.Title + '|' + card.Lines.join('|');
    if (plate && plate.kind === 'screen' && plate.key === key) return;
    const end = card.Kind === CardKind.MiasmaDeath || card.Kind === CardKind.Fallen || card.Kind === CardKind.Completed || card.Kind === CardKind.Survived;
    const brk = card.Kind === CardKind.LayerCleared || card.Kind === CardKind.PushedOut;
    const act = (action) => onCardAction(action);
    const el = openPlate({
      cls: end ? 'is-end is-' + card.Kind.toLowerCase() : brk ? 'is-washi is-break' : 'is-washi',
      w: end ? 980 : 900,
      ht: null,
      kind: 'screen',
      legend: card.Buttons.length ? [[T.KeyEnter, card.Buttons[0].Label]] : null,
      keys: (e) => {
        if (e.key === 'Enter') { act(card.Buttons[0].Action); return true; }
        return false;
      },
    });
    plate.key = key;
    const lines = h('div', { class: 'ocbg-explore-plate-lines' });
    for (const line of card.Lines) if (line) lines.appendChild(h('p', null, rich(line)));
    const mark = card.Kind === CardKind.MiasmaDeath ? ['miasma', 'var(--phosphor)']
      : card.Kind === CardKind.Fallen ? ['heart', 'var(--crimson)', { outline: true }]
        : ['stairs', 'var(--amber)']; // survived / cleared: the player's own way out
    put(el,
      end ? h('div', { class: 'ocbg-explore-end-mark' }, ico(mark[0], 40, mark[1], mark[2])) : null,
      brk ? h('div', { class: 'ocbg-explore-plate-kicker' }, ico(card.Kind === CardKind.LayerCleared ? 'Boss' : 'hourglass', 28, 'var(--washi-ink)'), rich(screen.LayerTitle)) : null,
      h('h2', null, card.Title),
      lines,
      cardButtons(card, act));
    fitPlate(el);
  }

  // ---- trial mode: the cards that wait on a choice (ExplorationScreenBuilder's TrialCardKind) ----
  /** A line's icon on the trial cards, by the word it starts with (the line itself is the bridge's). */
  function lineIcon(line, ink) {
    if (line.startsWith('HP')) return ico('heart', 20, 'var(--crimson)');
    if (line.startsWith('最大スタミナ')) return ico('pip', 18, 'var(--amber)', { outline: true });
    if (line.startsWith('スタミナ')) return ico('pip', 18, 'var(--amber)');
    if (line.startsWith('瘴気')) return ico('miasma', 20, 'var(--phosphor)');
    if (line.startsWith('カード')) return ico('seal', 20, ink);
    if (line.startsWith('希少な頁') || line.startsWith('頁')) return ico('page', 20, ink);
    if (line.startsWith('消耗品')) return ico('pouch', 20, ink);
    return h('i', { class: 'ocbg-explore-fc-dot' });
  }

  function drawChoiceCard(card) {
    const key = card.Kind + '|' + card.Title + '|' + card.Lines.join('|') + '|' + card.Buttons.map((b) => b.Label + b.Arg).join('|');
    if (plate && plate.kind === 'choice' && plate.key === key) return;
    const act = (b) => onCardAction(b.Action, b.Arg);
    const leave = card.Buttons.find((b) => b.Action === TrialAction.LeaveFound) || null;
    const picks = card.Buttons.filter((b) => b !== leave);
    const legend = picks.map((b, i) => [String(i + 1), b.Label]);
    if (leave) legend.push(['Esc', leave.Label]);
    const kind = card.Kind;
    const el = openPlate({
      cls: 'is-washi is-choice is-' + kind.toLowerCase(),
      w: kind === TrialCardKind.Swap ? 1180 : kind === TrialCardKind.EventChoice ? 1000 : 900,
      ht: null,
      kind: 'choice',
      legend,
      keys: (e) => {
        const n = Number(e.key);
        if (n >= 1 && n <= picks.length) { act(picks[n - 1]); return true; }
        if (e.key === 'Escape' && leave) { act(leave); return true; }
        return false;
      },
    });
    plate.key = key;
    const ink = 'var(--washi-ink)';
    if (kind === TrialCardKind.EventChoice) {
      const node = session.PendingEvent ? session.State.Map.Node(session.PendingEvent.NodeId) : null;
      const [lead, ...rest] = card.Lines;
      const tiles = h('div', { class: 'ocbg-explore-choice-tiles' });
      picks.forEach((b, i) => {
        const tile = h('button', { class: 'ocbg-explore-choice-tile' + (i === 0 ? ' is-primary' : ''), type: 'button', onclick: () => act(b) },
          h('kbd', null, String(i + 1)),
          h('strong', null, b.Label),
          h('span', { class: 'ocbg-explore-choice-note' }, b.Note ? b.Note.split('　').map((l) => h('span', l.startsWith('希少な頁') ? { title: T.PagesNote } : null, lineIcon(l, 'var(--ink)'), rich(l))) : null));
        tiles.appendChild(tile);
      });
      put(el,
        h('div', { class: 'ocbg-explore-plate-kicker' }, ico('Event', 28, ink), T.EventKicker, node ? h('span', { class: 'ocbg-explore-kicker-sub' }, rich(screen.LayerTitle)) : null),
        h('h2', null, card.Title),
        h('p', { class: 'ocbg-explore-choice-lead' }, lead),
        h('div', { class: 'ocbg-explore-plate-lines is-list' }, rest.map((l) => h('p', null, rich(l)))),
        tiles);
    } else if (kind === TrialCardKind.Swap) {
      const foundId = session.PendingFound[0];
      const [lead] = card.Lines;
      const carriedIds = session.State.Loadout.Consumables.map((c) => c.Id);
      const foundTile = h('div', { class: 'ocbg-explore-swap-found' },
        h('span', { class: 'ocbg-explore-swap-label' }, T.FoundItem),
        ico(itemIcon(foundId), 56, ink),
        h('strong', null, ExplorationText.ItemName(foundId)),
        h('span', { class: 'ocbg-explore-choice-note' }, ExplorationText.PlayerNote(foundId)));
      const slotsRow = h('div', { class: 'ocbg-explore-swap-slots' });
      picks.forEach((b, i) => {
        const id = carriedIds[b.Arg] || null;
        slotsRow.appendChild(h('button', { class: 'ocbg-explore-swap-slot', type: 'button', onclick: () => act(b) },
          h('kbd', null, String(i + 1)),
          id ? ico(itemIcon(id), 40, 'var(--ink)') : null,
          h('strong', null, id ? ExplorationText.ItemName(id) : b.Label),
          h('span', { class: 'ocbg-explore-choice-note' }, b.Note),
          h('span', { class: 'ocbg-explore-swap-go' }, T.Swap)));
      });
      put(el,
        h('div', { class: 'ocbg-explore-plate-kicker' }, ico('pouch', 28, ink), T.FoundItem),
        h('h2', null, card.Title),
        h('p', { class: 'ocbg-explore-choice-lead' }, lead),
        h('div', { class: 'ocbg-explore-swap-body' }, foundTile,
          h('div', { class: 'ocbg-explore-swap-side' }, h('span', { class: 'ocbg-explore-swap-label' }, T.Carried), slotsRow)),
        leave ? h('div', { class: 'ocbg-explore-plate-buttons' }, h('button', { class: 'ocbg-btn', type: 'button', onclick: () => act(leave) }, leave.Label)) : null);
    } else {
      const list = h('div', { class: 'ocbg-explore-survey-list' });
      picks.forEach((b, i) => {
        list.appendChild(h('button', { class: 'ocbg-explore-survey-row', type: 'button', onclick: () => act(b) },
          h('kbd', null, String(i + 1)), ico('Battle', 22, 'var(--violet)'), h('strong', null, rich(b.Label))));
      });
      put(el,
        h('div', { class: 'ocbg-explore-plate-kicker' }, ico('Survey', 28, ink), T.SurveyKicker),
        h('h2', null, card.Title),
        h('div', { class: 'ocbg-explore-plate-lines' }, card.Lines.map((l) => h('p', null, rich(l)))),
        list);
    }
    fitPlate(el);
    sfx(kind === TrialCardKind.Swap ? 'rewardReveal' : 'uiClick');
  }

  /** Trial: what an event did (EventOutcome), and a chest's card pick (Expedition.Offer / Learn). */
  function eventResultFlow(outcome) {
    return new Promise((resolve) => {
      const book = expedition.Mastery;
      const offer = outcome.Cards.length && expedition.Offer.length && outcome.Cards.every((id) => expedition.Offer.includes(id)) ? [...expedition.Offer] : [];
      const fits = offer.length ? expedition.OfferFits || [] : [];
      let chosen = -1;
      let closed = false;
      const finish = (learn) => {
        if (closed) return;
        closed = true;
        if (learn && chosen >= 0) { if (expedition.Learn(offer[chosen])) sfx('cardAdded'); } else if (offer.length) sfx('uiCancel');
        if (expedition.CloseResult) expedition.CloseResult();
        persist();
        closePlate();
        render();
        setTimeout(resolve, dur(240));
      };
      const select = (i) => {
        chosen = i;
        cardEls.forEach((c, k) => c.classList.toggle('is-chosen', k === i));
        learnBtn.disabled = chosen < 0;
        sfx('rewardPick');
      };
      const el = openPlate({
        cls: 'is-washi is-result is-event' + (offer.length ? '' : ' is-compact'),
        w: offer.length ? 1180 : 760,
        ht: null,
        kind: 'result',
        legend: offer.length ? [['←→', T.KeySelect], [T.KeyEnter, T.LearnButton], ['Esc', T.SkipButton]] : [[T.KeyEnter, T.BackToMap]],
        keys: (e) => {
          if (offer.length && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) {
            const d = e.key === 'ArrowLeft' ? -1 : 1;
            select(chosen < 0 ? (d > 0 ? 0 : offer.length - 1) : (chosen + d + offer.length) % offer.length);
            return true;
          }
          const n = Number(e.key);
          if (offer.length && n >= 1 && n <= offer.length) { select(n - 1); return true; }
          if (e.key === 'Enter') { if (!offer.length) finish(false); else if (chosen >= 0) finish(true); return true; }
          if (e.key === 'Escape') { finish(false); return true; }
          return false;
        },
      });
      const ink = 'var(--washi-ink)';
      const side = h('div', { class: 'ocbg-explore-result-side' },
        h('div', { class: 'ocbg-explore-result-group' }, outcome.Lines.flatMap((l) => [h('p', null, lineIcon(l, ink), rich(l)),
          // the pages have no content yet (#107): the reward says so where it is given
          l.startsWith('希少な頁') ? h('p', { class: 'ocbg-explore-result-note' }, T.PagesNote) : null])));
      const cardsRow = h('div', { class: 'ocbg-explore-result-cards' });
      const cardEls = [];
      const learnBtn = h('button', { class: 'ocbg-btn is-primary', type: 'button', disabled: 'disabled', onclick: () => finish(true) }, T.LearnButton);
      offer.forEach((id, i) => {
        let face;
        try { face = window.OCB.Card(cardDefById(id, book), { fit: 'b' }); } catch (e) { face = h('div', { class: 'ocbg-explore-cardstub' }, cardName(id, book)); }
        const wrap = h('div', { class: 'ocbg-explore-pick is-pickable' }, face,
          h('div', { class: 'ocbg-explore-pick-frame' }, window.OCB.TargetFrame(236, 324, { hook: 24, width: 3 })),
          h('kbd', { class: 'ocbg-explore-pick-key' }, String(i + 1)));
        wrap.addEventListener('click', () => select(i));
        wrap.addEventListener('dblclick', () => { select(i); finish(true); });
        wrap.style.animationDelay = dur(120 + i * 110) + 'ms';
        cardEls.push(wrap);
        cardsRow.appendChild(wrap);
      });
      const buttons = offer.length
        ? h('div', { class: 'ocbg-explore-plate-buttons' }, h('button', { class: 'ocbg-btn', type: 'button', onclick: () => finish(false) }, T.SkipButton), learnBtn)
        : h('div', { class: 'ocbg-explore-plate-buttons' }, h('button', { class: 'ocbg-btn is-primary', type: 'button', onclick: () => finish(false) }, T.BackToMap));
      el.append(
        h('div', { class: 'ocbg-explore-plate-kicker' }, ico('Event', 26, ink), T.EventKicker, h('span', { class: 'ocbg-explore-kicker-sub' }, rich(screen.LayerTitle))),
        h('div', { class: 'ocbg-explore-result-head' }, h('h2', null, outcome.Name),
          h('div', { class: 'ocbg-explore-result-stats' },
            h('span', null, ico('heart', 22, 'var(--crimson)'), rich(screen.HpLabel)),
            h('span', null, ico('pip', 20, 'var(--amber)'), rich(screen.StaminaLabel)))),
        offer.length ? h('div', { class: 'ocbg-explore-result-body' }, side, h('div', { class: 'ocbg-explore-result-main' }, h('h3', null, T.PickOne), cardsRow,
          fits.some((f) => !f) ? h('p', { class: 'ocbg-explore-result-note' }, rich(T.OfferBenched(expedition.DeckMax))) : null)) : side,
        buttons);
      fitPlate(el);
      sfx(offer.length ? 'rewardReveal' : 'eventResolve', 80);
    });
  }

  function openConfirm(card) {
    confirmOpen = true;
    syncMarks();
    syncEndLife();
    sfx('uiClick');
    const lethal = card.Kind !== CardKind.ConfirmEndLife;
    ask({
      title: card.Title,
      body: card.Lines.join('\n'),
      ok: card.Buttons[0].Label,
      cancel: card.Buttons[1] ? card.Buttons[1].Label : 'やめる',
      danger: lethal,
    }).then((yes) => {
      confirmOpen = false;
      if (!alive) return;
      syncEndLife();
      if (!yes) {
        stepInfo = null;
        session.Act(ScreenAction.Cancel);
        persist();
        sfx('uiCancel');
        render();
        return;
      }
      const action = card.Buttons[0].Action;
      onCardAction(action);
    });
  }

  async function onCardAction(action, arg = null) {
    if (!alive) return;
    if (TRIAL_ACTIONS.has(action)) {
      // trial mode: an event answered, a find swapped or left, the survey's target picked
      if (!session.Act(action, arg)) return;
      persist();
      closePlate();
      if (action === TrialAction.TakeFound) sfx('consumableUse');
      else if (action === TrialAction.ChooseSurvey) sfx('infoGained');
      else if (action === TrialAction.AcceptEvent) sfx('eventResolve');
      else sfx('uiCancel');
      render();
      if (action === TrialAction.ChooseSurvey) noteBanner(T.SurveyKicker, ExplorationScreenBuilder.SurveyLine(session), 'Survey');
      proceed();
      return;
    }
    const waiting = stepInfo;
    if (action === ScreenAction.ConfirmStep || action === ScreenAction.Cancel) stepInfo = null;
    if (action === ScreenAction.NewLife) {
      const end = lifeEnd(session, screen);
      // The app takes the life's end from here (the ending card, 継承の間); the card stays until it does.
      if (ctx.onLifeEnd) { ctx.onLifeEnd(end); return; }
      closePlate();
      session.Act(ScreenAction.NewLife);
      battleFallback = false;
      mapRef = null;
      render({ enter: true });
      layerBanner();
      proceed();
      return;
    }
    if (action === ScreenAction.AskEndLife) {
      if (session.Act(action)) { persist(); render(); }
      return;
    }
    // 次の層へ降りる with training marks left: the marks are this layer's, so the card asks for them first
    if (action === ScreenAction.Descend && session.State.TrainingMarks > 0 && session.IsIdle) {
      if (busy || plate || confirmOpen) return;
      const go = await trainBeforeDescend();
      if (!alive) return;
      if (!go) { render(); return; }
    }
    if (!session.Act(action)) return;
    persist();
    closePlate();
    switch (action) {
      case ScreenAction.GoToInterlude:
        sfx('interlude');
        render();
        break;
      case ScreenAction.Descend:
      case ScreenAction.ConfirmDescend:
        if (session.ConfirmingDescend) { render(); break; }
        await afterDescend();
        break;
      case ScreenAction.ConfirmStep:
        if (waiting) await afterMove(waiting.before, waiting.node, waiting.choice, waiting.jump);
        else render();
        break;
      case ScreenAction.ConfirmEndLife:
        sfx('uiConfirm');
        render();
        break;
      case ScreenAction.WinBattle:
      case ScreenAction.FallInBattle:
        battleFallback = false;
        render();
        proceed();
        break;
      default:
        render();
    }
  }

  // ------------------------------------------------------------------ steps
  function takeOption(node, index) {
    if (!screen || !screen.MapIsLive || busy || plate || confirmOpen) return;
    const opt = node.Options[index];
    if (!opt) return;
    const before = screen;
    const jump = !!opt.Jump; // trial: 標の石's jump (ExplorationSession.Jump)
    if (!(jump ? session.Jump(node.Id, opt.Choice) : session.Step(node.Id, opt.Choice))) return;
    if (session.HasPendingStep) { stepInfo = { before, node, choice: opt.Choice, jump }; render(); return; }
    afterMove(before, node, opt.Choice, jump);
  }

  async function afterMove(before, node, choice, jump = false) {
    if (!node) { render(); return; }
    // The step is in the session already: save it now, so a reload during the walk keeps it (tier1 R1-16).
    persist();
    const fromNode = before.Node(before.CurrentNodeId);
    const opt = node.Options.find((o) => o.Choice === choice && !!o.Jump === !!jump) || node.Options.find((o) => o.Choice === choice) || null;
    const fresh = node.Standing === NodeStanding.Reachable || jump;
    const fc = opt ? opt.Forecast : null;
    if (fresh && node.Kind === NodeKind.Carving) {
      // the carving names the layer: the lineage keeps it for the heirs after (the app's onCarving), saved with
      // the step so a reload during the walk cannot leave the session's carving read and the lineage's not
      try { if (ctx.onCarving) ctx.onCarving(); } catch (e) { console.error('[explore] carving', e); }
    }
    busy = true;
    root.classList.add('is-busy');
    updateMap(screen);
    drawDetail();
    // what the step sounds like (docs/AUDIO.md): the step, then at most one result
    let line = 0;
    if (fc && fc.MaxStaminaAfter < fc.MaxStaminaBefore && !fc.IsLethal) line = Miasma.MaxStaminaPenalty(fc.MiasmaAfter) * Miasma.StepPercent;
    // at most two sounds: the step, then one result (a 20% line or death outranks the node's own)
    const result = !fresh ? null : node.Kind === NodeKind.Rest ? (choice === RestChoice.Train ? 'train' : 'rest') : STEP_RESULT[node.Kind] || null;
    try {
      if (audio && audio.stepTo) audio.stepTo({ fresh, line, miasmaDeath: !!(fc && fc.IsLethal), result });
    } catch (e) { /* optional */ }
    hovered = -1;
    if (jump) sfx('consumableUse');
    await (jump ? leap(fromNode, node) : walk(fromNode, node));
    const arrived = nodeEls.get(node.Id);
    if (arrived) {
      arrived.el.classList.remove('is-arrived');
      void arrived.el.offsetWidth;
      arrived.el.classList.add('is-arrived');
      setTimeout(() => arrived.el.classList.remove('is-arrived'), 900);
    }
    busy = false;
    // A trace opens its card on arrival; walked back onto with its legacy still undecided (a reload left it
    // open), it opens again — the choice is made at the place of death (concept-v3 §8.3, R-08 / R-11). Never when
    // the step itself ended the life (瘴気死 on arrival, §6.4): the dead heir takes nothing, and the trace stays for
    // a living heir (R-09 / R-10).
    if (node.Kind === NodeKind.Trace && !session.State.IsOver && (fresh || traceWaiting())) pendingTrace = { layer: session.State.Profile.Layer, nodeId: node.Id };
    if (fresh && node.Kind === NodeKind.Rest && choice === RestChoice.Train && !session.Trial) pendingTraining = true; // trial: TrainingWaiting
    if (fresh && node.Kind === NodeKind.Carving) setTimeout(() => layerBanner(), dur(300));
    // the survey says what it raised (X-16); with 写しの帳 the line waits for the pick (onCardAction)
    if (fresh && node.Kind === NodeKind.Survey && !session.State.IsOver) noteBanner(T.SurveyKicker, ExplorationScreenBuilder.SurveyLine(session), 'Survey');
    persist();
    render();
    proceed();
  }

  /**
   * The trace the heir stands on whose legacy is still to be decided ({ layer, nodeId }), or null: the current
   * node is this layer's 死亡地点の痕跡 and the lineage still lets this life take it (traceNodeView.CanTake).
   */
  function traceWaiting() {
    if (!ctx.inheritance || !ctx.inheritance.traceView) return null;
    const state = session.State;
    if (state.IsOver || state.Phase !== RunPhase.Exploring) return null;
    const node = state.Map.Node(state.CurrentNodeId);
    if (!node || node.Kind !== NodeKind.Trace || !state.HasResolved(node.Id)) return null;
    let view = null;
    try { view = ctx.inheritance.traceView(state.Profile.Layer, node.Id); } catch (e) { return null; }
    return view && view.CanTake ? { layer: state.Profile.Layer, nodeId: node.Id } : null;
  }

  /**
   * The walker steps down off its perch onto the edge, walks the edge line from one node to the
   * next while the edge lights up behind it, and steps up onto the new node; the band's numbers
   * roll as it lands.
   */
  function walk(fromNode, toNode) {
    return new Promise((resolve) => {
      const a = nodePos(fromNode);
      const b = nodePos(toNode);
      const len = Math.hypot(b.x - a.x, b.y - a.y) || 1;
      const ms = dur(Math.min(820, 420 + len * 0.9));
      const t0 = performance.now();
      // the trail runs along the drawn edge segment, from the node left toward the node reached
      const k = Math.min(0.45, EDGE_INSET / len);
      const sx = a.x + (b.x - a.x) * k;
      const sy = a.y + (b.y - a.y) * k;
      const ex = b.x - (b.x - a.x) * k;
      const ey = b.y - (b.y - a.y) * k;
      const segLen = Math.hypot(ex - sx, ey - sy);
      trail.setAttribute('x1', sx); trail.setAttribute('y1', sy);
      trail.setAttribute('x2', ex); trail.setAttribute('y2', ey);
      trail.style.strokeDasharray = `${segLen} ${segLen + 10}`;
      trail.style.strokeDashoffset = `${segLen}`;
      trail.classList.remove('is-fading');
      edgesSvg.appendChild(trail);
      walker.classList.add('is-walking');
      const STEP_OFF = 0.24; // share of the stride spent stepping off the perch (and back up at the end)
      let landed = false;
      walkerAnim = (now) => {
        const t = Math.min(1, (now - t0) / ms);
        const e = EASE.inout(t);
        const perch = t < STEP_OFF ? EASE.smooth(1 - t / STEP_OFF) : t > 1 - STEP_OFF ? EASE.smooth((t - (1 - STEP_OFF)) / STEP_OFF) : 0;
        setWalker(a.x + (b.x - a.x) * e, a.y + (b.y - a.y) * e, perch, Math.sin(Math.PI * t) * 10);
        const along = Math.max(0, Math.min(1, (e * len - k * len) / Math.max(1, len - 2 * k * len)));
        trail.style.strokeDashoffset = `${segLen * (1 - along)}`;
        if (!landed && t >= 0.7) { landed = true; render(); }
        if (t >= 1) {
          walkerAnim = null;
          walker.classList.remove('is-walking');
          walker.classList.remove('is-landed');
          void walker.offsetWidth;
          walker.classList.add('is-landed');
          trail.classList.add('is-fading');
          setTimeout(() => { if (!walkerAnim && trail.classList.contains('is-fading')) trail.remove(); }, dur(520));
          resolve();
        }
      };
      if (!ms) { walkerAnim(t0 + 1); }
    });
  }

  /** Trial: 標の石 — the walker fades where it stood and appears on the node it jumped to. */
  function leap(fromNode, toNode) {
    return new Promise((resolve) => {
      const b = nodePos(toNode);
      walker.classList.add('is-leaping');
      setTimeout(() => {
        setWalker(b.x, b.y);
        render();
        walker.classList.remove('is-leaping');
        walker.classList.remove('is-landed');
        void walker.offsetWidth;
        walker.classList.add('is-landed');
        setTimeout(resolve, dur(260));
      }, dur(320));
    });
  }

  function useConsumable(index) {
    // the same rule for the mouse as for the keys 1–3: only on the map, the layer-break card and the interlude
    // (W-8) — never under a result, trace, training or choice card, nor while a step or a confirmation runs
    if (busy || confirmOpen || (plate && plate.kind !== 'screen')) return;
    if (!session.CanUseConsumables) return;
    const aiming = session.IsAiming;
    if (!session.UseConsumable(index)) {
      // a refused call changes nothing; redraw anyway if the aim moved, so the map never shows a stale aim
      if (session.IsAiming !== aiming) render();
      return;
    }
    // trial: a 標の石 starts (or stops) aiming instead of being spent
    if (session.IsAiming || aiming) sfx(session.IsAiming ? 'uiClick' : 'uiCancel');
    else sfx('consumableUse');
    hintOverride = null;
    persist();
    render();
  }

  async function afterDescend() {
    sfx('descend');
    battleFallback = false;
    render({ enter: true });
    const st = session.State;
    if (st.Phase === RunPhase.MiasmaDeath) {
      sfx('miasmaDeath', 600);
      return;
    }
    await layerBanner();
    proceed();
  }

  // ------------------------------------------------------------------ banners
  // Each banner owns the banner element until the next one starts: an older banner's timers must not
  // take down a newer one (the layer banner's fade used to cut the encounter banner short).
  let bannerSeq = 0;
  function layerBanner() {
    return new Promise((resolve) => {
      if (!screen) { resolve(); return; }
      const seq = ++bannerSeq;
      const title = screen.LayerTitle;
      const cut = title.indexOf('　');
      banner.textContent = '';
      banner.append(
        h('small', null, rich(cut > 0 ? title.slice(0, cut) : '')),
        h('strong', null, rich(cut > 0 ? title.slice(cut + 1) : title)),
        h('i', { class: 'ocbg-explore-banner-rule' }));
      banner.classList.remove('is-on', 'is-encounter', 'is-note');
      void banner.offsetWidth;
      banner.classList.add('is-on');
      setTimeout(() => { if (seq === bannerSeq) banner.classList.remove('is-on'); }, dur(1700));
      setTimeout(resolve, dur(1500));
    });
  }

  /**
   * What a step or a card just did, said once over the map (X-15 / X-16): the training's 刻み, the survey's enemy.
   * The banner's look at a reading size; it fades by itself and never takes a key or a click.
   */
  function noteBanner(kicker, line, icon = null) {
    if (!line) return;
    const seq = ++bannerSeq;
    banner.textContent = '';
    banner.append(
      h('small', null, icon ? ico(icon, 26, 'var(--ink)') : null, rich(kicker)),
      h('strong', null, rich(line)),
      h('i', { class: 'ocbg-explore-banner-rule' }));
    banner.classList.remove('is-on', 'is-encounter');
    banner.classList.add('is-note');
    void banner.offsetWidth;
    banner.classList.add('is-on');
    setTimeout(() => { if (seq === bannerSeq) banner.classList.remove('is-on'); }, dur(2800));
  }

  function encounterBanner() {
    return new Promise((resolve) => {
      const node = session.State.Map.Node(session.PendingBattleNode);
      const enc = session.PendingEncounter;
      const seq = ++bannerSeq;
      banner.textContent = '';
      // A name shows from 開示度 1 (§6.3), the same rule the node detail follows; an enemy not yet met stays 「？」.
      const nameOf = (id) => {
        if (!(expedition.DisclosureOf(id) >= 1)) return ExplorationText.UnknownEnemy;
        try { return Enemies.ById(id).Name; } catch (e) { return ExplorationText.UnknownEnemy; }
      };
      // the map's marks (J-9, enemy_roster_v4 §9): 「 + 」 between the enemies of one fight, 「 → 」 between fights
      const names = enc ? EnemyPages.EncounterName(enc.Fights.map((fight) => fight.EnemyIds.map(nameOf))) : '';
      const known = enc ? enc.EnemyIds.some((id) => expedition.DisclosureOf(id) >= 1) : false;
      put(banner,
        h('span', { class: 'ocbg-explore-banner-icon kind-' + KIND_CLASS[node.Kind] }, ico(node.Kind, 56, kindColor(node.Kind))),
        h('strong', null, ExplorationText.KindLabel(node.Kind)),
        known ? h('em', null, names) : null,
        enc && enc.Fights.length > 1 ? h('small', null, rich(T.FightCount(enc.Fights.length))) : null,
        h('i', { class: 'ocbg-explore-banner-rule' }));
      banner.classList.add('is-encounter');
      banner.classList.remove('is-on', 'is-note');
      void banner.offsetWidth;
      banner.classList.add('is-on');
      root.classList.add('is-engaging');
      setTimeout(() => {
        if (seq === bannerSeq) {
          banner.classList.remove('is-on');
          setTimeout(() => { if (seq === bannerSeq) banner.classList.remove('is-encounter'); }, dur(300));
        }
        resolve();
      }, dur(1200));
    });
  }

  // ------------------------------------------------------------------ the flow after a step
  async function proceed() {
    if (flowRunning || !alive) return;
    flowRunning = true;
    try {
      for (let guard = 0; guard < 8 && alive; guard++) {
        // The encounter is over (here, or in the app's battle scene before this scene was mounted
        // again): bring it back and show what it gave.
        const fought = expedition.Battle;
        if (fought && fought.Over) {
          await returnFlow();
          continue;
        }
        // A battle waits, or an encounter of several fights is part-fought (a reload after its first fight:
        // the save keeps every finished fight, and the next one starts over — ARCH 「Save by replay」).
        if (session.HasPendingBattle && (fought == null || !fought.Over) && !battleFallback) {
          await battleFlow();
          continue;
        }
        // trial: a result card the player has not closed — just shown by returnFlow or an event, or brought
        // back by a reload (Expedition.PendingResult, closed with CloseResult), its pick included
        const pending = expedition.PendingResult;
        const pendingKey = pending ? (pending.Kind === 'battle' ? pending : pending.Outcome) : null;
        if (pending && pendingKey !== shownResult) {
          shownResult = pendingKey;
          if (pending.Kind === 'battle') await resultFlow(pending);
          else await eventResultFlow(pending.Outcome);
          continue;
        }
        // trial: the 受け取った遺産 card the player has not closed (a reload while it was up: X-3)
        const taken = expedition.PendingTaken || null;
        if (taken && taken !== shownTaken) {
          shownTaken = taken;
          await takenFlow(taken);
          continue;
        }
        // The heir stands on a trace not yet decided (a reload while its card was open): the card again.
        if (!pendingTrace) {
          const t = traceWaiting();
          if (t && t.layer + ':' + t.nodeId !== traceOffered) pendingTrace = t;
        }
        if (pendingTrace) {
          const t = pendingTrace;
          pendingTrace = null;
          await traceFlow(t);
          continue;
        }
        // a mark waits on its card: just earned at a rest, or brought back by a reload while the card was up
        // (trial: Session.TrainingWaiting, put off with 「あとで」)
        if (pendingTraining || (session.TrainingWaiting && session.State.TrainingMarks > 0)) {
          pendingTraining = false;
          await trainingFlow();
          continue;
        }
        break;
      }
    } finally {
      flowRunning = false;
    }
    if (alive) render();
  }

  async function battleFlow() {
    if (!ctx.onBattle) {
      // No battle scene to play it in: the stand-in card's buttons (dungeon_exploration_v4 §6.8).
      battleFallback = true;
      render();
      return;
    }
    busy = true;
    render();
    await encounterBanner();
    inBattle = true;
    try {
      await ctx.onBattle(expedition);
    } catch (e) {
      console.error('[explore] the battle failed', e);
    }
    inBattle = false;
    // The app may have replaced this scene with the battle scene and mounted a new one after it;
    // that one brings the encounter back (proceed → returnFlow).
    if (!alive) return;
    root.classList.remove('is-engaging');
    const battle = expedition.Battle;
    if (!battle || !battle.Over) {
      // No battle scene played it (Unity: the stand-in card's buttons stay).
      if (battle) expedition.Abandon();
      battleFallback = true;
      busy = false;
      render();
    }
  }

  /** Expedition.Return: the encounter's HP, stamina, mastery and journal go home; then its reward. */
  async function returnFlow() {
    busy = true;
    const back = expedition.Return();
    persist();
    busy = false;
    root.classList.remove('is-engaging');
    render();
    // trial: the result waits on the expedition (PendingResult) and proceed shows it, the same way as after a
    // reload; without trial the card is shown from here
    if (!back.Died && !expedition.PendingResult) await resultFlow({ Back: back });
  }

  // ---- after a battle: the result and the reward pick (Expedition.Return / Offer / Learn) ----
  // `res` is Expedition.PendingResult (trial: Back plus Raised / PileSpent / Benched / DeckMax), or { Back }.
  function resultFlow(res) {
    return new Promise((resolve) => {
      const back = res.Back;
      const book = expedition.Mastery;
      const offer = [...expedition.Offer];
      const fits = offer.length ? expedition.OfferFits || [] : [];
      let chosen = offer.length ? -1 : null;
      let closed = false;
      const finish = (learn) => {
        if (closed) return;
        closed = true;
        if (learn && chosen != null && chosen >= 0) {
          if (expedition.Learn(offer[chosen])) sfx('cardAdded');
        } else sfx('uiCancel');
        if (expedition.CloseResult) expedition.CloseResult();
        persist();
        closePlate();
        render();
        if (session.State.Phase === RunPhase.LayerCleared) sfx('layerCleared', 200);
        setTimeout(resolve, dur(240));
      };
      const keys = (e) => {
        if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
          if (!offer.length) return true;
          const d = e.key === 'ArrowLeft' ? -1 : 1;
          select(chosen < 0 ? (d > 0 ? 0 : offer.length - 1) : (chosen + d + offer.length) % offer.length);
          return true;
        }
        const n = Number(e.key);
        if (offer.length && n >= 1 && n <= offer.length) { select(n - 1); return true; }
        if (e.key === 'Enter') {
          if (!offer.length) finish(false);
          else if (chosen >= 0) finish(true);
          return true;
        }
        if (e.key === 'Escape') { finish(false); return true; }
        return false;
      };
      const showCards = offer.length ? offer : back.Learned;
      const compact = showCards.length === 0;
      const el = openPlate({
        cls: 'is-washi is-result' + (compact ? ' is-compact' : ''),
        w: compact ? 840 : 1180,
        ht: null,
        kind: 'result',
        keys,
        legend: offer.length
          ? [['←→', T.KeySelect], [T.KeyEnter, T.LearnButton], ['Esc', T.SkipButton]]
          : [[T.KeyEnter, T.BackToMap]],
      });

      // what the encounter brought back, one group per kind
      const side = h('div', { class: 'ocbg-explore-result-side' });
      const section = (title, rows) => {
        if (!rows.length) return;
        side.appendChild(h('div', { class: 'ocbg-explore-result-group' }, h('h4', null, title), rows));
      };
      // learned cards are listed here unless they are the cards shown on the right
      if (offer.length) section(T.Learned, back.Learned.map((id) => h('p', null, ico('seal', 18, 'var(--washi-ink)'), cardName(id, book))));
      // trial: a learned card a full deck kept out is owned only (encounters_and_rewards_v4 §4.3 「デッキの上限の中で」)
      const benched = res.Benched || [];
      if (benched.length) side.appendChild(h('p', { class: 'ocbg-explore-result-note' }, rich(T.Benched(benched.map((id) => cardName(id, book)).join('・'), res.DeckMax))));
      // a 段 can rename the card (突き → 鋭い突き): the line names it as it was in the hand too (X-15)
      // (the names and the 段 are two runs: the narrow column breaks between them, never inside 「0 → 1 段目」)
      section(T.RankUps, back.RankUps.map((r) => h('p', { class: 'ocbg-explore-rankup' }, ico('pip', 16, 'var(--washi-ink)', { outline: true }),
        h('span', null, rich(T.RankUpNames(nameAtRank(book, r.CardId, r.To), nameAtRank(book, r.CardId, r.From))),
          h('span', { class: 'ocbg-explore-rankup-steps' }, rich(T.RankUpSteps(r.From, r.To)))))));
      const journalRows = [];
      // trial: only the enemies whose 開示度 rose (Raised); without it every enemy fought is listed
      for (const [id, level] of res.Raised || back.Disclosure) {
        let name = id;
        try { name = Enemies.ById(id).Name; } catch (e) { /* the id stands in */ }
        journalRows.push(h('p', null, ico('journal', 18, 'var(--washi-ink)'), rich(T.JournalLine(name, level, JournalPage.MaxDisclosure))));
      }
      section(T.Journal, journalRows);
      const foundRows = back.Reward.Consumables.map((id) => h('p', null, ico(itemIcon(id), 18, 'var(--washi-ink)'), ExplorationText.ItemName(id)));
      if (back.Reward.Pages) {
        foundRows.push(h('p', null, ico('page', 18, 'var(--washi-ink)'), rich(T.Pages(back.Reward.Pages))));
        // the pages have no content yet (#107): say so where they are given, so the reward does not read as a gain
        foundRows.push(h('p', { class: 'ocbg-explore-result-note' }, T.PagesNote));
      }
      // trial: the card the reward wanted came as a consumable (§4.3 「すべての山が尽きたときは、札の代わりに消耗品」)
      if (res.PileSpent) foundRows.push(h('p', { class: 'ocbg-explore-result-note' }, T.PileSpentBattle));
      section(T.Found, foundRows.length ? foundRows : [h('p', { class: 'is-quiet' }, T.NothingFound)]);

      const stats = h('div', { class: 'ocbg-explore-result-stats' },
        h('span', null, ico('heart', 22, 'var(--crimson)'), rich(screen.HpLabel)),
        h('span', null, ico('pip', 20, 'var(--amber)'), rich(screen.StaminaLabel)));
      const main = h('div', { class: 'ocbg-explore-result-main' });
      const cardsRow = h('div', { class: 'ocbg-explore-result-cards' });
      const cardEls = [];
      const learnBtn = h('button', { class: 'ocbg-btn is-primary', type: 'button', disabled: 'disabled', onclick: () => finish(true) }, T.LearnButton);
      const select = (i) => {
        chosen = i;
        cardEls.forEach((c, k) => c.classList.toggle('is-chosen', k === i));
        learnBtn.disabled = chosen < 0;
        sfx('rewardPick');
      };
      showCards.forEach((id, i) => {
        let card;
        try { card = window.OCB.Card(cardDefById(id, book), { fit: 'b' }); } catch (e) { card = h('div', { class: 'ocbg-explore-cardstub' }, cardName(id, book)); }
        const wrap = h('div', { class: 'ocbg-explore-pick' + (offer.length ? ' is-pickable' : ' is-learned') }, card);
        wrap.appendChild(h('div', { class: 'ocbg-explore-pick-frame' }, window.OCB.TargetFrame(236, 324, { hook: 24, width: 3 })));
        if (offer.length) {
          wrap.appendChild(h('kbd', { class: 'ocbg-explore-pick-key' }, String(i + 1)));
          wrap.addEventListener('click', () => select(i));
          wrap.addEventListener('dblclick', () => { select(i); finish(true); });
        }
        wrap.style.animationDelay = dur(120 + i * 110) + 'ms';
        cardEls.push(wrap);
        cardsRow.appendChild(wrap);
      });
      const buttons = offer.length
        ? h('div', { class: 'ocbg-explore-plate-buttons' }, h('button', { class: 'ocbg-btn', type: 'button', onclick: () => finish(false) }, T.SkipButton), learnBtn)
        : h('div', { class: 'ocbg-explore-plate-buttons' }, h('button', { class: 'ocbg-btn is-primary', type: 'button', onclick: () => finish(false) }, T.BackToMap));
      if (offer.length) main.append(h('h3', null, T.PickOne), cardsRow);
      else if (back.Learned.length) main.append(h('h3', null, T.Learned), cardsRow);
      if (offer.length && fits.some((f) => !f)) main.append(h('p', { class: 'ocbg-explore-result-note' }, rich(T.OfferBenched(expedition.DeckMax))));
      el.append(
        h('div', { class: 'ocbg-explore-result-head' }, h('h2', null, back.Won ? T.BattleWon : T.BattleLost), stats),
        compact ? side : h('div', { class: 'ocbg-explore-result-body' }, side, main),
        buttons);
      fitPlate(el);
      if (showCards.length) sfx('rewardReveal', 120);
    });
  }

  // ---- 死亡地点の痕跡 (Inheritance through ctx.inheritance) ----
  /** The 走り書き the life carries now (the app's list), so a trace's copy of one reads as adding nothing. */
  function heldScribbles() {
    try { return ctx.inheritance && ctx.inheritance.scribbles ? ctx.inheritance.scribbles() || [] : []; } catch (e) { return []; }
  }
  function traceFlow(t) {
    return new Promise((resolve) => {
      if (!ctx.inheritance || !ctx.inheritance.traceView) { resolve(); return; }
      // a life that is over takes no legacy (the 瘴気死 on the trace's own step: R-09 / R-10 keep it for the next heir)
      if (session.State.IsOver) { resolve(); return; }
      let view = null;
      try { view = ctx.inheritance.traceView(t.layer, t.nodeId); } catch (e) { console.error('[explore] traceView', e); }
      if (!view) { resolve(); return; }
      traceOffered = t.layer + ':' + t.nodeId;
      const picks = [];
      let cursor = -1;
      let closed = false;
      let asking = false;
      const done = (take) => {
        if (closed) return;
        closed = true;
        let taken = null;
        let receipt = null;
        const chosen = take ? picks.slice() : [];
        if (view.CanTake && !session.State.IsOver) {
          // what the picks give this heir: written from what the take did (its Result: the 段 and 開示度 this life ends
          // with), else from the same steps tried before the take (X-11)
          const before = heldScribbles();
          const preview = chosen.length ? traceGains(expedition, view.Candidates, chosen, { done: true, scribbles: before }) : null;
          try { taken = ctx.inheritance.take(chosen); } catch (e) { console.error('[explore] take', e); }
          sfx(take && picks.length ? 'traceTake' : 'uiCancel');
          if (taken && chosen.length) {
            const gains = taken.Result ? traceGains(expedition, view.Candidates, chosen, { done: true, result: taken.Result, scribbles: before }) : preview;
            receipt = takenCard(view, chosen, taken, gains);
            // the card waits on the expedition until it is closed, so a reload shows it again (X-3; trial mode)
            if (expedition.ShowTaken && expedition.ShowTaken(receipt)) persist();
          }
        }
        closePlate();
        render();
        // what was received, row by row as the card listed it (nothing to show for 「何も受け取らない」)
        if (receipt) setTimeout(() => takenFlow(receipt).then(resolve), dur(240));
        else setTimeout(resolve, dur(240));
      };
      // Leaving takes nothing and still collects the trace (R-10, 試作で決めたこと 5): it asks first, since Esc
      // is the close key everywhere else and what is not chosen is lost (concept-v3 §5).
      const leave = () => {
        if (closed || asking) return;
        if (!view.CanTake) { done(false); return; }
        asking = true;
        sfx('uiClick');
        ask({ title: T.TraceLeaveTitle, body: T.TraceLeaveBody(picks.length), ok: view.LeaveButton, cancel: T.TraceLeaveCancel, danger: true })
          .then((yes) => { asking = false; if (!alive) return; if (yes) done(false); else sfx('uiCancel'); });
      };
      const toggle = (id) => {
        const at = picks.indexOf(id);
        if (at >= 0) picks.splice(at, 1);
        else if (pickState(picks).CanPickMore) picks.push(id);
        else return;
        sfx('rewardPick');
        refresh();
      };
      // The cursor walks the candidates, then the withheld cards under their heading (they only show why they
      // stayed: R-05), so the keys reach every row of the list, as the mouse wheel does.
      const placeCursor = (i, quiet = false) => {
        cursor = i;
        stops.forEach((r, k) => r.row.classList.toggle('is-cursor', k === cursor));
        const at = stops[cursor];
        if (at.heading) keepInView(list, at.heading);
        keepInView(list, at.row);
        if (cursor === rows.length - 1 && !stops[cursor + 1]) list.scrollTop = list.scrollHeight; // the end of the list
        if (!quiet) sfx('nodeHover');
      };
      const moveCursor = (d) => {
        if (!stops.length) return;
        placeCursor(cursor < 0 ? (d > 0 ? 0 : stops.length - 1) : (cursor + d + stops.length) % stops.length);
      };
      const page = (d) => { list.scrollTop += d * Math.round(list.clientHeight * 0.8); };
      const el = openPlate({
        cls: 'is-washi is-trace', w: 1100, ht: 760, kind: 'trace',
        legend: view.CanTake
          ? [['↑↓', T.KeySelect], ['Space', T.KeyToggle], [T.KeyEnter, view.TakeButton], ['Esc', view.LeaveButton]]
          : [['↑↓', T.KeyScroll], ['Esc', T.Close]],
        keys: (e) => {
          if (asking) return true; // the leave confirmation answers its own keys; nothing else moves under it
          if (e.key === 'ArrowUp' || e.key === 'ArrowDown') { moveCursor(e.key === 'ArrowUp' ? -1 : 1); return true; }
          if (e.key === 'PageUp' || e.key === 'PageDown') { page(e.key === 'PageUp' ? -1 : 1); return true; }
          if (e.key === 'Home' || e.key === 'End') { list.scrollTop = e.key === 'Home' ? 0 : list.scrollHeight; return true; }
          if (view.CanTake && e.key === ' ' && cursor >= 0) { if (stops[cursor].id != null) toggle(stops[cursor].id); return true; }
          if (e.key === 'Enter') { if (!view.CanTake) done(false); else if (picks.length) done(true); return true; }
          if (e.key === 'Escape') { leave(); return true; }
          return false;
        },
      });
      const count = h('span', { class: 'ocbg-explore-trace-count' });
      const takeBtn = h('button', { class: 'ocbg-btn is-primary', type: 'button', onclick: () => done(true) }, view.TakeButton);
      const list = h('div', { class: 'ocbg-explore-trace-list' });
      const rows = [];
      const stops = []; // the cursor's rows: the candidates, then the withheld cards ({ row, id: null, heading? })
      // What each row gives *this* heir (X-11): the row is printed as this life will hold it — a card's name and 段
      // after the take (capped by the one-card rule on 3 段, or the higher 段 the life holds), an enemy page's 開示度
      // after the journal keeps the higher one — and the notes under it say what the trace held when that differs and
      // when a pick adds nothing (a 段 or a page held already, a 走り書き already carried, a deck with no room). The
      // picks are tried together (they share the deck's room and the 3 段), the others as the next pick.
      const gainNote = (at, notes) => {
        if (!at || !at.gain) return;
        at.gain.textContent = '';
        at.gain.style.display = notes && notes.length ? '' : 'none';
        for (const n of notes || []) at.gain.appendChild(rich(n));
      };
      const refresh = () => {
        const ps = pickState(picks);
        count.textContent = '';
        count.appendChild(rich(ps.Line));
        const gains = view.CanTake ? traceGains(expedition, view.Candidates, picks, { scribbles: heldScribbles() }) : new Map();
        rows.forEach((at) => {
          const { row, id, c } = at;
          const on = picks.includes(id);
          row.classList.toggle('is-on', on);
          row.classList.toggle('is-locked', !on && !ps.CanPickMore);
          const g = gains.get(id) || null;
          row.classList.toggle('is-nothing', !!(g && g.Nothing));
          at.name.textContent = g && g.Name ? g.Name : c.Name;
          at.detail.textContent = '';
          at.detail.appendChild(rich(g && g.Detail ? g.Detail : (c.Detail || '')));
          gainNote(at, g ? g.Notes : null);
        });
        takeBtn.disabled = picks.length === 0;
        fitTrace();
      };
      for (const c of view.Candidates) {
        const gain = h('span', { class: 'ocbg-explore-trace-gain', style: { display: 'none' } });
        const name = h('strong', null, c.Name);
        const detail = h('span', { class: 'ocbg-explore-trace-detail' }, rich(c.Detail || ''));
        const row = h('button', { class: 'ocbg-explore-trace-row', type: 'button', disabled: view.CanTake ? null : 'disabled' },
          h('i', { class: 'ocbg-explore-check' }),
          h('span', { class: 'ocbg-explore-trace-kind' }, c.KindLabel),
          name, detail, gain);
        // a click also moves the cursor there, so Space goes on from the row the mouse last used
        row.addEventListener('click', () => { placeCursor(stops.findIndex((x) => x.row === row), true); toggle(c.Id); });
        rows.push({ row, id: c.Id, gain, name, detail, c });
        stops.push({ row, id: c.Id });
        list.appendChild(row);
      }
      if (view.Withheld && view.Withheld.length) {
        const heading = h('h4', null, view.WithheldHeading);
        list.appendChild(heading);
        view.Withheld.forEach((w, i) => {
          const row = h('div', { class: 'ocbg-explore-trace-row is-withheld' }, h('i', { class: 'ocbg-explore-check' }), h('strong', null, w.Name), h('span', { class: 'ocbg-explore-trace-detail' }, rich(w.Detail)));
          stops.push({ row, id: null, heading: i === 0 ? heading : null });
          list.appendChild(row);
        });
      }
      // the list fades at the edge it runs on past, like the hall's (a scroll cue: nothing below reads as empty)
      list.addEventListener('scroll', () => moreCue(list));
      put(el,
        h('div', { class: 'ocbg-explore-plate-kicker' }, ico('Trace', 28, 'var(--washi-ink)'), view.PlaceLine),
        h('h2', null, view.Title),
        h('p', { class: 'ocbg-explore-trace-heir' }, view.HeirLine),
        view.Note ? h('p', { class: 'ocbg-explore-trace-note' }, view.Note) : null,
        h('p', { class: 'ocbg-explore-trace-lead' }, view.CanTake ? view.Lead : view.Refusal),
        // the deck is at its cap: every card taken here is owned only (X-11, §4.3 「デッキの上限の中で」)
        view.CanTake && deckIsFull(expedition) && view.Candidates.some((c) => c.Kind === 'Card')
          ? h('p', { class: 'ocbg-explore-trace-lead is-warn' }, ico('caution', 20, 'var(--caution)'), rich(TraceGainText.DeckFullLead(expedition.DeckMax))) : null,
        list,
        h('div', { class: 'ocbg-explore-plate-buttons' },
          view.CanTake ? count : null,
          h('button', { class: 'ocbg-btn', type: 'button', onclick: () => leave() }, view.CanTake ? view.LeaveButton : T.Close),
          view.CanTake ? takeBtn : null));
      // the list keeps its scroll cue right as the notes under the card rows come and go
      function fitTrace() { moreCue(list); }
      refresh();
      // the legend offers Space at once: the cursor starts on the first candidate (a reload brings the card back the same)
      if (view.CanTake && rows.length) placeCursor(0, true);
      moreCue(list);
    });
  }

  /** A scrolling list fades at the edge it continues past (the hall's lists do the same: is-more-above / is-more-below). */
  function moreCue(list) {
    const more = list.scrollHeight > list.clientHeight + 2;
    list.classList.toggle('is-more-below', more && list.scrollTop + list.clientHeight < list.scrollHeight - 2);
    list.classList.toggle('is-more-above', more && list.scrollTop > 2);
  }

  /**
   * Scrolls a list so its row stays in view (the stage is scaled: measure in screen px, scroll in layout px). A list
   * that fades at its edges (moreCue) keeps the row clear of the fade, so the row the keys are on is never dimmed.
   */
  function keepInView(list, row) {
    const lr = list.getBoundingClientRect();
    const rr = row.getBoundingClientRect();
    const k = list.offsetHeight ? lr.height / list.offsetHeight : 1;
    const fades = list.scrollHeight > list.clientHeight + 2;
    const m = (fades ? Math.round(list.clientHeight * 0.18) : 8) * k; // the fade's depth (is-more-* masks: 82%)
    if (rr.top < lr.top + m) list.scrollTop -= (lr.top + m - rr.top) / k;
    else if (rr.bottom > lr.bottom - m) list.scrollTop += (rr.bottom - (lr.bottom - m)) / k;
    moreCue(list);
  }

  /**
   * The 受け取った遺産 card's content as plain data (the expedition keeps it until it is closed: X-3): the rows the trace
   * card listed for the picks (Inheritance's own rows), each card with what it gave this heir (X-11), and how many
   * candidates were lost (concept-v3 §5 「選ばなかった頁と資源は失われる」).
   */
  function takenCard(view, chosen, taken, gains) {
    const rows = view.Candidates.filter((c) => chosen.includes(c.Id)).map((c) => {
      const g = gains ? gains.get(c.Id) || null : null;
      return {
        Kind: c.Kind, KindLabel: c.KindLabel, Name: g && g.Name ? g.Name : c.Name, Detail: g && g.Detail ? g.Detail : (c.Detail || ''),
        Notes: g ? [...g.Notes] : [],
        Nothing: !!(g && g.Nothing),
      };
    });
    return { PlaceLine: view.PlaceLine, HeirLine: view.HeirLine, Rows: rows, Lost: taken && typeof taken.Lost === 'number' ? taken.Lost : 0 };
  }

  /**
   * After 受け取る: what the heir took from the trace (takenCard), as the trace card listed it. Only shows; nothing is
   * decided. Closing it closes the expedition's pending card (CloseTaken), so a reload no longer brings it back.
   */
  function takenFlow(card) {
    return new Promise((resolve) => {
      let closed = false;
      const finish = () => {
        if (closed) return;
        closed = true;
        if (expedition.CloseTaken && expedition.PendingTaken) { expedition.CloseTaken(); persist(); }
        closePlate();
        render();
        setTimeout(resolve, dur(240));
      };
      const el = openPlate({
        cls: 'is-washi is-result is-taken is-compact', w: 840, ht: null, kind: 'result',
        legend: [[T.KeyEnter, T.BackToMap]],
        keys: (e) => { if (e.key === 'Enter' || e.key === 'Escape') { finish(); return true; } return false; },
      });
      const ink = 'var(--washi-ink)';
      const rows = (card.Rows || []).map((c) => h('div', { class: 'ocbg-explore-taken-item' + (c.Nothing ? ' is-nothing' : '') },
        h('p', { class: 'ocbg-explore-taken-row' },
          ico(c.Kind === 'Card' ? 'seal' : c.Kind === 'EnemyPage' ? 'journal' : 'page', 18, ink),
          h('span', { class: 'ocbg-explore-trace-kind' }, c.KindLabel), h('strong', null, c.Name), c.Detail ? rich(c.Detail, 'ocbg-explore-trace-detail') : null),
        c.Notes && c.Notes.length ? h('p', { class: 'ocbg-explore-taken-gain' }, c.Notes.map((n) => rich(n))) : null));
      const lost = typeof card.Lost === 'number' ? card.Lost : 0;
      put(el,
        h('div', { class: 'ocbg-explore-plate-kicker' }, ico('Trace', 26, ink), card.PlaceLine),
        h('div', { class: 'ocbg-explore-result-head' }, h('h2', null, T.TakenTitle)),
        h('div', { class: 'ocbg-explore-result-side' },
          h('div', { class: 'ocbg-explore-result-group' }, h('h4', null, card.HeirLine), rows),
          lost > 0 ? h('p', { class: 'ocbg-explore-result-note' }, rich(T.TraceLost(lost))) : null),
        h('div', { class: 'ocbg-explore-plate-buttons' }, h('button', { class: 'ocbg-btn is-primary', type: 'button', onclick: () => finish() }, T.BackToMap)));
      fitPlate(el);
    });
  }

  // ---- 習熟訓練: spend a mark on a card (Expedition.Train) ----
  // Resolves with what the player did: 'trained', 'later' (「あとで」 / 「やめる」) or 'descend' (「刻みを捨てて降りる」).
  // `descend` is the card 次の層へ降りる opens while marks are left: a mark is earned on this layer and the next
  // layer starts with none (ExplorationReducer.Enter), so the heir chooses before going down instead of losing a
  // paid tick without a word (dungeon_exploration_v4 §2.1 「指定した 1 枚に刻み 1」, §3.2).
  function trainingFlow({ descend = false } = {}) {
    return new Promise((resolve) => {
      if (!(session.State.TrainingMarks > 0)) { resolve('later'); return; }
      const book = expedition.Mastery;
      const ids = [];
      const copies = new Map();
      for (const c of expedition.Deck) {
        if (!ids.includes(c.Def.Id)) ids.push(c.Def.Id);
        copies.set(c.Def.Id, (copies.get(c.Def.Id) || 0) + 1);
      }
      let chosen = -1;
      let closed = false;
      const rows = [];
      const preview = h('div', { class: 'ocbg-explore-train-preview' });
      const lastMark = session.State.TrainingMarks === 1;
      const done = (how) => {
        if (closed) return;
        closed = true;
        let result = how;
        let said = null;
        if (how === 'train' && chosen >= 0 && expedition.Train(ids[chosen])) {
          sfx('uiConfirm');
          result = 'trained';
          // what the mark did, said once the card is gone (X-15): the 刻み toward the next 段, or the 段 it bought
          const id = ids[chosen];
          const was = progress[chosen];
          const now = masteryProgress(expedition.Mastery, id);
          said = now.Rank > was.Rank ? T.RankUp(now.Name, was.Rank, now.Rank, was.Name) : T.Trained(now.Name, now.Ticks, now.Need);
        } else {
          sfx('uiCancel');
          if (how === 'train') result = 'later';
          // 「あとで」: the card is put off, so a reload does not bring it back; the band keeps the marks
          if (session.TrainingWaiting && session.PutOffTraining) session.PutOffTraining();
        }
        persist();
        closePlate();
        render();
        if (said) noteBanner(T.TrainedKicker, said, 'Rest');
        setTimeout(() => resolve(result), dur(240));
      };
      // each card's 習熟 now (MasteryView): the rows' 刻み, the preview's lines, and the line after the training
      const progress = ids.map((id) => masteryProgress(book, id));
      const holderName = book.TopRankHolder ? nameAtRank(book, book.TopRankHolder, 3) : '';
      let shownFace = -1;
      const showFace = (i) => {
        shownFace = i;
        preview.textContent = '';
        if (i < 0) return;
        let card;
        try { card = window.OCB.Card(cardDefById(ids[i], book), { fit: 'b' }); } catch (e) { card = h('div', { class: 'ocbg-explore-cardstub' }, cardName(ids[i], book)); }
        preview.appendChild(card);
        // under the card: the name it had, and what one more 刻み does (X-15)
        const p = progress[i];
        const lines = [];
        if (p.Name !== p.BaseName) lines.push(T.TrainBaseName(p.BaseName));
        if (p.Need) {
          lines.push(T.TrainNext(p.Ticks + 1, p.Need, p.Rank + 1));
          if (p.NextName) lines.push(T.TrainNextName(p.NextName));
        } else if (p.Capped) lines.push(T.TrainCappedNote(holderName));
        else lines.push(p.Top ? T.TrainTicksTop : T.TrainTicksFlat);
        preview.appendChild(h('div', { class: 'ocbg-explore-train-about' }, lines.map((l) => h('p', null, rich(l)))));
      };
      const select = (i) => {
        chosen = i;
        rows.forEach((r, k) => r.classList.toggle('is-chosen', k === i));
        trainBtn.disabled = chosen < 0;
        showFace(i);
        // the chosen row stays in sight (the keys move it; the preview alone must not be the only sign of it)
        if (rows[i]) keepInView(list, rows[i]);
        sfx('rewardPick');
      };
      const laterLabel = descend ? T.TrainStay : T.TrainLater;
      const legend = [['↑↓', T.KeySelect], [T.KeyEnter, descend && lastMark ? T.TrainAndDescend : T.TrainButton]];
      if (descend) legend.push([[T.KeyShift, T.KeyEnter], T.TrainDiscard]);
      legend.push(['Esc', laterLabel]);
      const el = openPlate({
        cls: 'is-solid is-train' + (descend ? ' is-descend' : ''), w: 1060, ht: 780, kind: 'train',
        legend,
        keys: (e) => {
          const moves = { ArrowUp: -1, ArrowDown: 1, ArrowLeft: -1, ArrowRight: 1 };
          if (moves[e.key] != null) {
            select(chosen < 0 ? 0 : (chosen + moves[e.key] + ids.length) % ids.length);
            return true;
          }
          if (e.key === 'Enter' && descend && e.shiftKey) { done('descend'); return true; }
          if (e.key === 'Enter') { if (chosen >= 0) done('train'); return true; }
          if (e.key === 'Escape') { done('later'); return true; }
          return false;
        },
      });
      const trainBtn = h('button', { class: 'ocbg-btn is-primary', type: 'button', disabled: 'disabled', onclick: () => done('train') }, descend && lastMark ? T.TrainAndDescend : T.TrainButton);
      const list = h('div', { class: 'ocbg-explore-train-list' });
      list.addEventListener('scroll', () => moreCue(list));
      list.addEventListener('mouseleave', () => showFace(chosen < 0 ? 0 : chosen));
      ids.forEach((id, i) => {
        let def = null;
        try { def = cardDefById(id, book); } catch (e) { /* the name still shows */ }
        const attr = def ? window.OCB.ATTR[def.attr] : null;
        const p = progress[i];
        const rank = p.Rank;
        const ticks = p.Need ? T.TrainTicks(p.Ticks, p.Need) : p.Capped ? T.TrainTicksCapped : p.Top ? T.TrainTicksTop : T.TrainTicksFlat;
        const row = h('button', { class: 'ocbg-explore-train-row', type: 'button' },
          attr ? window.OCB.icon(attr.icon, 24, attr.ink) : h('i'),
          h('strong', null, def ? def.name : cardName(id, book)),
          h('span', { class: 'ocbg-explore-train-copies' }, rich('×' + copies.get(id))),
          h('span', { class: 'ocbg-explore-train-ticks' + (p.Need ? '' : ' is-quiet') }, rich(ticks)),
          h('span', { class: 'ocbg-explore-rank' + (rank > 0 ? '' : ' is-zero') }, rich(T.Rank(rank))));
        row.addEventListener('click', () => select(i));
        // The mouse previews the card it moves over. Only a real move counts: when the keys scroll the list under a
        // resting pointer, a different row slides under it (and gets mouseenter), but the preview must stay on the
        // row the keys chose — the one 訓練する / Enter trains (X-8).
        row.addEventListener('mousemove', (e) => { if ((e.movementX || e.movementY) && shownFace !== i) showFace(i); });
        rows.push(row);
        list.appendChild(row);
      });
      el.append(
        h('div', { class: 'ocbg-explore-plate-kicker' }, ico('Rest', 26, 'var(--ink)'), rich(screen.MarksLabel)),
        h('h2', null, descend ? T.TrainBeforeDescend : T.TrainTitle),
        h('p', { class: 'ocbg-explore-train-lead' }, descend ? T.TrainLeadDescend : T.TrainLead),
        h('div', { class: 'ocbg-explore-train-body' }, list, preview),
        h('div', { class: 'ocbg-explore-plate-buttons' },
          h('button', { class: 'ocbg-btn', type: 'button', onclick: () => done('later') }, laterLabel),
          descend ? h('button', { class: 'ocbg-btn is-danger', type: 'button', onclick: () => done('descend') }, T.TrainDiscard) : null,
          trainBtn));
      showFace(0);
      moreCue(list);
    });
  }

  /**
   * 次の層へ降りる with marks left: the training card asks first, one mark at a time. True when the descent goes on
   * (every mark spent, or 「刻みを捨てて降りる」); false when the heir stays at the interlude (「やめる」).
   */
  async function trainBeforeDescend() {
    if (flowRunning) return false;
    flowRunning = true;
    try {
      for (let guard = 0; guard < 8 && alive && session.State.TrainingMarks > 0; guard++) {
        const how = await trainingFlow({ descend: true });
        if (how === 'descend') return true;
        if (how !== 'trained') return false;
      }
      return alive;
    } finally {
      flowRunning = false;
    }
  }

  // ------------------------------------------------------------------ the interlude (§6.5)
  // The rest on washi where the map was; the next layer (a window onto its backdrop, its line and any
  // warning) in the right column where the node detail was; the carried items stay usable below it.
  let lullKey = '';
  function drawInterlude(on) {
    if (!on) {
      if (lullKey) { lull.textContent = ''; lullKey = ''; }
      return;
    }
    // a confirmation (この生を終える, or a descent into 瘴気死) is up over the rest: its own modal asks, and the
    // rest's card stays under it (one confirmation, §6.6) — drawn from the interlude card itself when this mount has
    // not drawn it yet (a reload with the confirmation open: X-8)
    if (screen.Card.Kind !== CardKind.Interlude && lullKey) return;
    const card = screen.Card.Kind === CardKind.Interlude ? screen.Card : ExplorationScreenBuilder.InterludeCard(session);
    if (!card) return;
    const key = card.Lines.join('|') + card.Buttons.map((b) => b.Label).join('|');
    if (key === lullKey) return;
    lullKey = key;
    lull.textContent = '';
    const below = session.TryDescend();
    const icons = [['heart', 'var(--crimson)'], ['pip', 'var(--amber)'], ['miasma', 'var(--phosphor)']];
    const rest = h('div', { class: 'ocbg-explore-lull-rest' });
    const ahead = [];
    card.Lines.forEach((line, i) => {
      if (i < 3) rest.appendChild(h('p', null, ico(icons[i][0], 28, icons[i][1]), rich(line)));
      else ahead.push(line);
    });
    // the next layer: a window onto its backdrop under the same veil, then the bridge's line and warning
    const next = place(h('div', { class: 'ocbg-explore-lull-next' }), DETAIL.x, DETAIL.y, DETAIL.w, null);
    next.appendChild(h('div', { class: 'ocbg-explore-plate-kicker' }, ico('stairs', 26, 'var(--ink)'), T.NextLayer));
    if (below) next.appendChild(h('div', { class: 'ocbg-explore-lull-window' }, skyStrip(below.Profile.Layer, 534, 300)));
    for (const line of ahead) {
      if (line.startsWith('！ ')) next.appendChild(h('p', { class: 'ocbg-explore-warn' }, ico('caution', 22, 'var(--caution)'), h('span', null, line.slice(2))));
      else {
        // 「次は第 2 層　■■の秘跡: 1 歩ごとに瘴気 +1%、刻限 10」: the layer as a heading, its numbers under it
        const at = line.indexOf(': ');
        if (below && at > 0) {
          next.appendChild(h('h3', { class: 'ocbg-explore-lull-name' }, rich(line.slice(0, at))));
          next.appendChild(h('p', { class: 'ocbg-explore-lull-ahead' }, rich(line.slice(at + 2))));
        } else next.appendChild(h('p', null, rich(line)));
      }
    }
    const buttons = h('div', { class: 'ocbg-explore-lull-buttons' });
    card.Buttons.forEach((b, i) => {
      buttons.appendChild(h('button', { class: 'ocbg-btn' + (i === 0 && b.Action === ScreenAction.Descend ? ' is-primary' : ''), type: 'button', onclick: () => onCardAction(b.Action) },
        b.Action === ScreenAction.Descend ? ico('stairs', 24, 'var(--ink)') : null, h('span', null, b.Label)));
    });
    const restPlate = h('div', { class: 'ocbg-explore-lull-plate' },
      h('div', { class: 'ocbg-explore-plate-kicker' }, ico('Rest', 28, 'var(--washi-ink)'), rich(screen.LayerTitle)),
      h('h2', null, card.Title),
      rest,
      buttons);
    lull.append(restPlate, next);
    restPlate.style.top = Math.max(120, Math.round(MAP_CENTER.y - restPlate.offsetHeight / 2)) + 'px';
  }

  // ------------------------------------------------------------------ overlays (the app mounts them)
  async function openOverlay(kind) {
    if (!screen || overlayOpen) return;
    if (kind === 'cards' && !screen.CanOpenCards) return;
    if (kind === 'settings' && !screen.CanOpenSettings) return;
    if (kind === 'journal') sfx('journalOpen'); else sfx('uiClick');
    overlayOpen = kind;
    try {
      if (ctx.onOverlay) {
        const r = ctx.onOverlay(kind, { book: screen.Journal, page: kind === 'cards' ? screen.Cards : kind === 'settings' ? screen.Settings : null, screen });
        if (r && typeof r.then === 'function') {
          await r;
          if (kind === 'journal') sfx('journalClose');
        }
      } else {
        await fallbackOverlay(kind);
        if (kind === 'journal') sfx('journalClose');
      }
    } catch (e) {
      console.error('[explore] overlay', e);
    } finally {
      overlayOpen = null;
    }
    if (alive) render();
  }

  /** Stand-ins for the app's overlays: the builder's OverlayPage, and a plain journal drawer. */
  function fallbackOverlay(kind) {
    return new Promise((resolve) => {
      const shade = h('div', { class: 'ocbg-explore-ovshade' });
      let pop = null;
      const close = () => { overlayClose = null; pop(); el.classList.add('is-leaving'); shade.remove(); setTimeout(() => el.remove(), 220); resolve(); };
      overlayClose = close;
      let el;
      if (kind === 'journal') {
        const book = screen.Journal;
        let page = book.First;
        const tabs = h('div', { class: 'ocbg-explore-jtabs' });
        const body = h('div', { class: 'ocbg-explore-jbody' });
        const show = (p) => {
          page = p;
          tabs.querySelectorAll('button').forEach((b) => b.classList.toggle('is-on', b.dataset.id === p.Id));
          body.textContent = '';
          body.append(h('h3', null, p.Title, p.DisclosureLabel ? h('small', null, p.DisclosureLabel) : null));
          for (const row of p.Rows) {
            if (row.Label) body.appendChild(h('h4', null, row.Label));
            if (row.Text) body.appendChild(h('p', null, p.TextOf(row)));
          }
        };
        for (const p of book.Pages) {
          const b = h('button', { type: 'button', 'data-id': p.Id, disabled: book.CanOpen(p) ? null : 'disabled' }, JournalBook.TabText(p));
          b.addEventListener('click', () => show(p));
          tabs.appendChild(b);
        }
        el = h('div', { class: 'ocbg-explore-journal' },
          h('div', { class: 'ocbg-explore-jhead' }, h('h2', null, JournalBook.Heading), h('button', { class: 'ocbg-btn', type: 'button', onclick: () => close() }, JournalBook.CloseLabel)),
          tabs, body);
        if (page) show(page);
        overlayLayer.append(el);
      } else {
        const page = kind === 'cards' ? screen.Cards : screen.Settings;
        el = h('div', { class: 'ocbg-explore-ovpage' },
          h('div', { class: 'ocbg-explore-jhead' }, h('h2', null, page.Title), h('button', { class: 'ocbg-btn', type: 'button', onclick: () => close() }, page.CloseLabel)),
          h('div', { class: 'ocbg-explore-ovlines' }, page.Lines.map((l) => h('p', null, l || ' '))),
          kind === 'settings' && ctx.onSaveQuit ? h('div', { class: 'ocbg-explore-plate-buttons' }, h('button', { class: 'ocbg-btn', type: 'button', onclick: () => { close(); ctx.onSaveQuit(); } }, T.SaveQuit)) : null);
        overlayLayer.append(shade, el);
      }
      const keyOf = { journal: 'j', cards: 'c', settings: 'Escape' };
      pop = pushKeys((e) => {
        const k = e.key.length === 1 ? e.key.toLowerCase() : e.key;
        if (k === keyOf[kind] || e.key === 'Escape') { close(); return true; }
        return kind !== 'journal';
      });
    });
  }

  // ------------------------------------------------------------------ input
  journalBtn.addEventListener('click', () => openOverlay('journal'));
  cardsBtn.addEventListener('click', () => openOverlay('cards'));
  settingsBtn.addEventListener('click', () => openOverlay('settings'));
  marksBtn.addEventListener('click', () => {
    if (!canTrainNow()) return;
    // trial: the session keeps the card open (OpenTraining), so a reload brings it back
    if (session.Trial && session.OpenTraining) { if (!session.OpenTraining()) return; persist(); } else pendingTraining = true;
    proceed();
  });
  endLifeBtn.addEventListener('click', () => {
    if (!screen || !screen.CanEndLife || busy) return;
    if (plate && plate.kind !== 'screen') return;
    onCardAction(ScreenAction.AskEndLife);
  });

  const popKeys = keysWithFocus((e) => {
    if (!alive || !screen) return false;
    if (e.ctrlKey || e.metaKey || e.altKey) return false;
    const k = e.key.length === 1 ? e.key.toLowerCase() : e.key;
    // A held key repeats: only a fresh press opens an overlay (the app's overlays close on a fresh press only, so a held
    // J, C or Esc no longer opens and closes them over and over).
    if (e.repeat && (k === 'j' || k === 'c' || k === 'Escape')) return true;
    if (k === 'j') { if (!session.HasPendingBattle || !busy) openOverlay('journal'); return true; }
    if (busy) return false;
    if (k === 'c') { openOverlay('cards'); return true; }
    if (k === 'Escape' && session.IsAiming && !plate && !confirmOpen) {
      // trial: Esc stops aiming a 標の石 before it opens anything (like a confirmation, §6.2)
      if (session.Escape()) { sfx('uiCancel'); persist(); render(); }
      return true;
    }
    if (k === 'Escape') { openOverlay('settings'); return true; }
    if (confirmOpen) return false;
    // the consumables stay usable while the layer-break card is up and at the interlude (§6.2)
    if ((k === '1' || k === '2' || k === '3') && (!plate || plate.kind === 'screen')) {
      // a key presses the slot's button, so it does nothing where the button is disabled
      const slot = screen.Consumables[Number(k) - 1];
      if (slot && (slot.CanUse || slot.Aiming)) useConsumable(slot.Index);
      return true;
    }
    if (plate) return false;
    if (session.State.Phase === RunPhase.Interlude && (k === 'Enter' || k === ' ')) {
      const first = screen.Card.Buttons[0];
      if (first) onCardAction(first.Action);
      return true;
    }
    if (!screen.MapIsLive) return false;
    const dirs = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] };
    if (dirs[k]) { moveSelection(dirs[k][0], dirs[k][1]); return true; }
    if (k === 'Enter' || k === ' ') {
      const node = screen.Node(pinned);
      if (node && node.Options.length) takeOption(node, e.shiftKey && node.Options.length > 1 ? 1 : 0);
      return true;
    }
    return false;
  });

  // ------------------------------------------------------------------ start
  // Back from the app's battle scene (the encounter waits on Return): the map fades back in where it
  // was, with no layer banner. Otherwise the layer assembles row by row under its banner.
  const returning = !!(expedition.Battle && expedition.Battle.Over);
  if (returning) root.classList.add('is-returning');
  render({ enter: !returning });
  (async () => {
    if (!returning && (session.State.Phase === RunPhase.Exploring || session.HasPendingBattle)) await layerBanner();
    proceed();
  })();

  return {
    destroy() {
      // answer an open confirmation and close a fallback overlay so their keys leave the stack
      const cancel = root.querySelector('.ocbg-confirm .ocbg-btn:not(.is-primary)');
      alive = false;
      if (cancel) cancel.click();
      if (overlayClose) overlayClose();
      popKeys();
      window.removeEventListener('keydown', onKeyCapture, true);
      document.removeEventListener('focusin', onFocusIn);
      document.removeEventListener('focusout', onFocusOut);
      window.removeEventListener('pointerdown', onPointerCapture, true);
      if (plate && plate.pop) plate.pop();
      clock.stop();
      backdrop.destroy();
      root.remove();
    },
    refresh() { render(); proceed(); },
  };
}
