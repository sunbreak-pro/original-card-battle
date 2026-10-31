// Dev page for the battle scene: pick an enemy (every enemy of the ported roster, the two-enemy pair of
// LayerEncounters), a layer backdrop, a line length and a seed; the fight is built the way Unity's
// BattleBootstrap builds it (a BattleLaunch → CoreBattleSource) and mounted with mountBattleScene and a
// stub ctx. "auto" plays every turn with the ported GreedyPlayer through the scene's own input path
// (the hand's drag), so whole battles run unattended.
//
// While the JS core is incomplete (BattleLaunch / Enemies / GreedyPlayer fail to load), the page falls back
// to "replay": the C# CoreBattleSource's own runs from the oracle (dev/battle-replay.js), every frame and
// text Unity's bridge wrote; auto then follows the recorded suggestions. ?replay=1 forces it.
//
// Build: node dev/battle-build.mjs dev/battle.js   (tools/devbuild.mjs works once every port is in)
// URL:   dev/out/battle.html?enemy=<id>&layer=<1..7>&cells=<5..8>&seed=<n>&pair=1&chain=2&auto=1&speed=1.5&short=1
//        &disclosure=<0..2>&loop=1&replay=1&bar=0&mute=0
// window.__battle = { scene, source, mode, finished, outcome, battles, errors } for the screenshot steps, and
// the playtest's read-only hooks (dev only, dev/battle-read.js): read() what the screen shows, check() the
// mismatches between the screen and the source, choose() the ported GreedyPlayer's next play { id, unit } or null.
import '../src/scenes/common/common.css';
import { createStage, h } from '../src/scenes/common/stage.js';
import { createAudio } from '../src/scenes/audio/Audio.js';
import { mountBattleScene } from '../src/scenes/battle/BattleScene.js';
import { createReplaySource } from './battle-replay.js';
import { readScreen, checkScreen } from './battle-read.js';
import replays from './battle-replays/replays.json';
import { Cards } from '../src/core/battle/Cards.js';
import { CardCatalog } from '../src/core/battle/CardCatalog.js';

const OCB = window.OCB;
const params = new URLSearchParams(location.search);
const num = (k, d) => (params.has(k) && params.get(k) !== '' && !Number.isNaN(Number(params.get(k))) ? Number(params.get(k)) : d);
/** LayerEncounters' pair (roster §9): 錆槍 in front, 灰弩 behind. */
const PAIR = ['polearm_warped', 'crossbow_hunter'];

const settings = { speed: num('speed', 1.25), shortFx: params.get('short') === '1', master: 0.8, effects: 0.9, ambience: 0.6, muted: params.get('mute') !== '0' };
const app = document.getElementById('app');
const stage = createStage(app);
const audio = createAudio(OCB, settings);
if (audio.bindUnlock) audio.bindUnlock(document);
if (settings.muted && audio.setMuted) audio.setMuted(true);

const state = window.__battle = { scene: null, source: null, mode: '', finished: false, outcome: null, battles: 0, finishedCalls: 0, errors: [], settingsCalls: 0, journalOpen: 0 };
state.read = () => (state.scene ? readScreen(state.scene) : null);
state.check = () => (state.scene && state.source ? checkScreen(state.scene, state.source) : ['no scene']);
/** The greedy play for the screen's tests: { id, unit } (unit −1 for a card aimed at nobody), or null to end the turn. */
state.choose = (played, cap) => {
  if (!greedy || !state.source || state.mode !== 'core') return null;
  if (cap > 0 && (played || 0) >= cap) return null;
  const c = greedy.Choose(state.source.State, played || 0);
  return c ? { id: c.InstanceId, unit: c.Target } : null;
};
window.addEventListener('error', (e) => state.errors.push(String(e.message)));
/** The app's audio (what reaches the speakers' API, after the scene's own filters), for the sound checks. */
state.audio = audio;

// ---- the real flow (BattleLaunch / Enemies / GreedyPlayer), loaded lazily so a missing port falls back --------
async function loadReal() {
  if (params.get('replay') === '1') return null;
  try {
    const [bl, en, gp] = await Promise.all([
      import('../src/bridge/depiction/BattleLaunch.js'),
      import('../src/core/battle/Enemies.js'),
      import('../src/sim/GreedyPlayer.js'),
    ]);
    const all = en.Enemies.All || [];
    if (!all.length) throw new Error('Enemies.All is empty');
    return { BattleLaunch: bl.BattleLaunch, Enemies: en.Enemies, GreedyPlayer: gp.GreedyPlayer };
  } catch (err) {
    console.warn('[dev/battle] the real flow is not loadable yet; replaying the oracle runs instead:', err && err.message);
    return null;
  }
}

const pick = {
  enemy: params.get('enemy') || 'polearm_warped',
  layer: num('layer', 1),
  cells: num('cells', 6),
  seed: num('seed', 20260921),
  pair: params.get('pair') === '1',
  chain: Math.max(1, num('chain', 1)),
  auto: params.get('auto') === '1',
  /** auto: at most this many cards a turn (0: as many as the greedy player wants), so fights last a few turns. */
  cap: num('cap', 0),
  disclosure: num('disclosure', 2),
  loop: params.get('loop') === '1',
};

let real = null;
let roster = [];

// ---- the dev bar (outside the 1920×1080 stage) ------------------------------------------------------
const bar = h('div', { class: 'ocbg-dev-bar' });
document.body.appendChild(bar);
const style = document.createElement('style');
style.textContent = `.ocbg-dev-bar{position:fixed;left:8px;bottom:8px;z-index:9999;display:flex;gap:8px;align-items:center;flex-wrap:wrap;max-width:calc(100vw - 16px);padding:6px 10px;background:var(--panel-solid);border:1px solid var(--rule);border-radius:var(--radius-panel);font:400 13px/1.4 var(--font-body);color:var(--ink);opacity:.92}
.ocbg-dev-bar select,.ocbg-dev-bar input,.ocbg-dev-bar button{font:inherit;color:var(--ink);background:var(--panel);border:1px solid var(--steel);border-radius:4px;padding:2px 6px}
.ocbg-dev-bar.is-hidden{display:none}`;
document.head.appendChild(style);
if (params.get('bar') === '0') bar.classList.add('is-hidden');

function select(label, value, options, onChange) {
  const s = h('select', null, options.map(([v, t]) => h('option', { value: String(v), selected: String(v) === String(value) ? 'selected' : null }, t)));
  s.addEventListener('change', () => onChange(s.value));
  return h('label', null, label + ' ', s);
}
function check(label, value, onChange) {
  const c = h('input', { type: 'checkbox', checked: value ? 'checked' : null });
  c.addEventListener('change', () => onChange(c.checked));
  return h('label', null, c, ' ' + label);
}
const statusEl = h('span', null, '');
function drawBar() {
  bar.innerHTML = '';
  bar.appendChild(h('b', null, state.mode === 'replay' ? 'replay' : 'core'));
  bar.appendChild(select('敵', pick.enemy, roster.map((r) => [r.id, r.label]), (v) => { pick.enemy = v; }));
  bar.appendChild(select('層', pick.layer, [1, 2, 3, 4, 5, 6, 7].map((n) => [n, String(n)]), (v) => { pick.layer = Number(v); }));
  if (state.mode !== 'replay') {
    bar.appendChild(select('マス', pick.cells, [5, 6, 7, 8].map((n) => [n, String(n)]), (v) => { pick.cells = Number(v); }));
    bar.appendChild(select('開示', pick.disclosure, [0, 1, 2].map((n) => [n, String(n)]), (v) => { pick.disclosure = Number(v); }));
    const seed = h('input', { type: 'number', value: String(pick.seed), style: 'width:110px' });
    seed.addEventListener('change', () => { pick.seed = Number(seed.value) || 1; });
    bar.appendChild(h('label', null, 'seed ', seed));
    bar.appendChild(check('2 体（錆槍＋灰弩）', pick.pair, (v) => { pick.pair = v; }));
  }
  bar.appendChild(select('連戦', pick.chain, [1, 2, 3].map((n) => [n, String(n)]), (v) => { pick.chain = Number(v); }));
  bar.appendChild(check('auto', pick.auto, (v) => { pick.auto = v; }));
  bar.appendChild(check('演出短縮', settings.shortFx, (v) => { settings.shortFx = v; }));
  const go = h('button', { type: 'button' }, '開始');
  go.addEventListener('click', () => startChain());
  bar.appendChild(go);
  bar.appendChild(statusEl);
}

// ---- fights -----------------------------------------------------------------------------------------
function nodeKindOf(rank) {
  return rank === 'Boss' ? 'Boss' : rank === 'Elite' ? 'Elite' : 'Battle';
}
function rankOf(id) {
  const r = roster.find((x) => x.id === id);
  return r ? r.rank : 'Normal';
}
/** BattleBootstrap: the Inspector's fields copied into a BattleLaunch, which builds the source. */
function makeCoreSource(index, total) {
  const launch = new real.BattleLaunch();
  const ids = pick.pair ? PAIR : [pick.enemy];
  launch.EnemyId = ids[0];
  launch.MoreEnemyIds = ids.length > 1 ? ids.slice(1) : null;
  launch.Seed = pick.seed + (index - 1);
  launch.FieldCells = pick.cells;
  launch.Disclosure = pick.disclosure;
  launch.ChainIndex = index;
  launch.ChainTotal = total;
  if (params.get('deck')) { const names = params.get('deck').split(','); launch.Deck = Cards.BuildDeck(names.map((n) => CardCatalog[n]), 2); }
  if (params.get('ids')) { const ids = params.get('ids').split(','); launch.EnemyId = ids[0]; launch.MoreEnemyIds = ids.length > 1 ? ids.slice(1) : null; }
  return launch.CreateSource();
}
function makeReplaySource() {
  const c = replays.cases.find((x) => x.enemies.join('+') === pick.enemy) || replays.cases[0];
  return createReplaySource(c);
}

function stubJournal(host, opts) {
  const panel = h('div', { style: 'position:absolute;right:0;top:0;width:576px;height:1080px;background:var(--washi);color:var(--washi-ink);padding:32px;box-sizing:border-box;font:400 22px/1.6 var(--font-body)' },
    h('h3', { style: 'margin:0 0 16px;font:700 30px/1.4 var(--font-display)' }, '手記（dev の仮）'),
    h('p', null, (opts.enemyIds || []).map((id) => id + '：開示度 ' + (opts.disclosureOf ? opts.disclosureOf(id) : '?')).join(' / ')));
  const close = h('button', { type: 'button', style: 'font:inherit;margin-top:24px' }, '閉じる');
  close.addEventListener('click', () => opts.onClose && opts.onClose());
  panel.appendChild(close);
  host.appendChild(panel);
  return { destroy() { panel.remove(); } };
}

/** As src/app/main.js openOverlay('settings', { inBattle: true }) / closeOverlay(). */
let overlay = null;
async function openRealSettings() {
  const { mountSettings } = await import('../src/scenes/meta/settings.js');
  const host = stage.layer('ocbg-overlay-host');
  host.style.zIndex = '500';
  const close = () => {
    if (!overlay) return;
    overlay.destroy();
    host.remove();
    overlay = null;
    state.overlayOpen = false;
    if (state.scene && state.scene.refresh) state.scene.refresh();
  };
  overlay = mountSettings(host, { stage, audio, settings, inBattle: true, onClose: close, onChange: (next) => { if (next) Object.assign(settings, next); } });
  state.overlayOpen = true;
}

let chainToken = 0;
let greedy = null;

function runOne(index, total, token) {
  return new Promise((resolve) => {
    if (state.scene) state.scene.destroy();
    stage.root.querySelectorAll('.ocbg-scene').forEach((n) => n.remove());
    let source;
    try {
      source = state.mode === 'replay' ? makeReplaySource() : makeCoreSource(index, total);
    } catch (err) {
      console.error(err);
      statusEl.textContent = 'source error: ' + err.message;
      resolve('Error');
      return;
    }
    state.source = source;
    state.finished = false;
    state.outcome = null;
    const host = stage.layer('ocbg-battle-host');
    const front = state.mode === 'replay' ? (source.replay.split('|')[1] || '').split('+')[0] : pick.pair ? PAIR[0] : pick.enemy;
    const ctx = {
      stage, audio, settings, source,
      layer: pick.layer,
      nodeKind: nodeKindOf(rankOf(front)),
      chain: { index, total },
      disclosureOf: () => pick.disclosure,
      openJournal: (host, opts) => { state.journalOpen += 1; return stubJournal(host, opts); },
      // The app opens its settings overlay here and calls scene.refresh() when it closes; the stand-in opens
      // none, so it "closes" at once (?settingsHold=1 keeps the scene held, as behind a real overlay).
      onSettings: () => {
        state.settingsCalls += 1;
        statusEl.textContent = 'onSettings()';
        // ?realSettings=1: the app's own settings overlay, opened and closed the way src/app/main.js does it.
        if (params.get('realSettings') === '1') { openRealSettings(); return; }
        if (params.get('settingsHold') !== '1') setTimeout(() => { if (state.scene && state.scene.refresh) state.scene.refresh(); }, 0);
      },
      onSettingsChange: () => {},
      onFinished: () => {
        state.finishedCalls += 1;
        if (token !== chainToken) return;
        state.finished = true;
        state.outcome = source.Frame.Outcome;
        state.battles += 1;
        statusEl.textContent = '終了: ' + state.outcome + '（' + index + ' / ' + total + '）';
        resolve(state.outcome);
      },
      auto: !pick.auto ? null : state.mode === 'replay' ? { choose: (src) => src.SuggestedCardId } : {
        choose(src, playedThisTurn) {
          if (src.Frame.Corner.Turn > 80) return '';
          if (pick.cap > 0 && playedThisTurn >= pick.cap) return '';
          const choice = greedy.Choose(src.State, playedThisTurn);
          return choice ? { id: choice.InstanceId, unit: choice.Target } : '';
        },
      },
    };
    state.scene = mountBattleScene(host, ctx);
    statusEl.textContent = (state.mode === 'replay' ? source.replay : front + (pick.pair ? '＋' + PAIR[1] : '') + ' / seed ' + (pick.seed + index - 1)) + ' / 層 ' + pick.layer + (pick.auto ? ' / auto' : '');
  });
}

async function startChain() {
  const token = ++chainToken;
  for (let i = 1; i <= pick.chain; i++) {
    const outcome = await runOne(i, pick.chain, token);
    if (token !== chainToken) return;
    if (outcome !== 'Won') break;
  }
  if (pick.loop && token === chainToken) {
    const i = roster.findIndex((r) => r.id === pick.enemy);
    pick.enemy = roster[(i + 1) % roster.length].id;
    pick.seed += 1;
    drawBar();
    startChain();
  }
}

async function boot() {
  real = await loadReal();
  if (real) {
    state.mode = 'core';
    greedy = new real.GreedyPlayer();
    roster = real.Enemies.All.map((d) => ({ id: d.Id, rank: d.Rank, size: d.Size || 1, label: d.Name + '（' + d.Rank + (d.Size > 1 ? '・' + d.Size + 'マス' : '') + '）' }));
  } else {
    state.mode = 'replay';
    roster = replays.cases.map((c) => ({ id: c.enemies.join('+'), rank: rankFromReplay(c), label: c.enemies.join('＋') + '（再生）' }));
  }
  state.roster = roster;
  if (!roster.some((r) => r.id === pick.enemy)) pick.enemy = roster[0].id;
  drawBar();
  startChain();
}
const RANKS = { armored_warden: 'Elite', pack_alpha: 'Elite', polearm_unyielding: 'Elite', miasma_priest: 'Boss', abyss_angler: 'Boss', distortion_root: 'Boss' };
function rankFromReplay(c) { return RANKS[c.enemies[0]] || 'Normal'; }

const fontsReady = document.fonts && document.fonts.ready ? document.fonts.ready : Promise.resolve();
Promise.race([fontsReady, new Promise((r) => setTimeout(r, 1500))]).then(boot);
