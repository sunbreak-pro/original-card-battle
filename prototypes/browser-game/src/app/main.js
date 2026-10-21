// The game shell: one stage, one audio, settings, the lineage, and a router between scenes.
// Scenes never navigate; they call back here. Rules stay in the ported cores and the inheritance module.
import '../scenes/common/common.css';
import { createStage, confirmCard, noticeLine } from '../scenes/common/stage.js';
import { createAudio } from '../scenes/audio/Audio.js';
import * as Meta from '../scenes/meta/index.js';
import { mountExploreScene } from '../scenes/explore/ExploreScene.js';
import { mountBattleScene } from '../scenes/battle/BattleScene.js';
import * as Inh from '../core/inheritance/Inheritance.js';
import { RunPhase } from '../core/dungeon/ExplorationState.js';
import { PrototypeDeck } from '../core/battle/CardCatalog.js';
import { DepictionText } from '../bridge/depictionScript/DepictionTypes.js';
import { Expedition } from '../bridge/exploration/Expedition.js';
import { recordExpedition, replayLife, settleWhenDecided, trimUnfinished } from './recorder.js';
import { openSave, writeSave, loadSettings, writeSettings, storageWorks, keepBrokenSave, storedStamp, sameStamp, SAVE_KEY, DEFAULT_SETTINGS } from './save.js';
import { lifeSetup, makeStart, lifeScribbles, addScribbles } from './life.js';
import { lifeJournal, endOptions } from './names.js';

const OCB = window.OCB;
const app = document.getElementById('app');
const stage = createStage(app);
const settings = Object.assign({}, DEFAULT_SETTINGS, loadSettings());
const audio = createAudio(OCB, settings, { onChange: (s) => { Object.assign(settings, s); writeSettings(settings); } });
if (audio.bindUnlock) audio.bindUnlock(document);

const opened = openSave();
let save = opened.save;            // { lineage, life: { start, log, memo, before } | null }
// A stored save that did not load (another build's version, a write cut off): it stays under its key,
// copied aside (save.openSave), until the player starts anew. While it is there the title says so and
// はじめから asks before writing over it. { error, kept } or null.
let unreadable = opened.unreadable;
// The stored save as this page last read or wrote it (save.js stamps). Another tab of the game that
// writes after it makes this page stale: it writes nothing more and reloads the stored save (markStale).
let stamp = opened.stamp;
const TAB = (Math.random().toString(36).slice(2, 10) || 'tab') + Date.now().toString(36).slice(-4);
let stale = false;
let screen = '';                   // which screen is up: title / hall / departure / explore / battle / ending
let scene = null;                  // the mounted scene { destroy }
let overlay = null;                // the mounted overlay { destroy, host }
let live = null;                   // { expedition (recorded), raw, log, start, life, memo }
let sceneHost = null;              // the mounted scene's host element

// Storage can be blocked (private windows, previews) or full. The game plays on from memory, but the
// player is told that nothing will outlive the page: on the title, in 設定・セーブ, and once when a save fails.
let storageOk = storageWorks();
const NO_STORAGE_LINES = ['この環境では保存できません。', 'ページを閉じると、ここまでの記録は失われます。'];
const SAVE_FAILED_TEXT = '保存できませんでした。ページを閉じると、ここまでの記録は失われます。';
const UNREADABLE_LINES = (kept) => ['保存された記録を読み込めませんでした。',
  kept ? '記録は消さずに控えとして残しています。' : '控えを残せないため、はじめからを選ぶと失われます。'];
const reasonOf = (e) => '（' + String((e && e.message) || e).slice(0, 80) + '）';
const STALE_TEXT = '別のタブでこの記録が先に進みました。最新の記録を読み込み直しました。';

// The app's own notices inside a scene (the title's): they step back while an overlay is open, so the
// panel's notice is the only one (and nothing sits across the panel's frame).
const APP_NOTICE = 'ocbg-app-notice';
function sceneNotices(visible) {
  if (sceneHost) sceneHost.querySelectorAll('.' + APP_NOTICE).forEach((n) => { n.style.visibility = visible ? '' : 'hidden'; });
}

function show(mount, ctx, cls) {
  closeOverlay();
  if (scene && scene.destroy) scene.destroy();
  stage.root.querySelectorAll('.ocbg-scene').forEach((n) => n.remove());
  const host = stage.layer(cls);
  sceneHost = host;
  scene = mount(host, Object.assign({ stage, audio, settings }, ctx));
  return scene;
}

// 設定・セーブ in 継承の間 and 出立: no life is in progress, but the lineage is saved (settings.js 'lineage').
const LINEAGE_SAVE = Object.freeze({ canSaveQuit: true, saveQuitKind: 'lineage' });

function openOverlay(kind, extra) {
  closeOverlay();
  const host = stage.layer('ocbg-overlay-host');
  host.style.zIndex = '500';
  const base = { stage, audio, settings, onClose: closeOverlay };
  sceneNotices(false);
  const ex = live && live.raw;
  if (kind === 'settings') {
    overlay = Meta.mountSettings(host, Object.assign(base, {
      canSaveQuit: !!live,
      onSaveQuit: saveQuit,
      onChange: (s) => { if (s) Object.assign(settings, s); writeSettings(settings); if (audio.applySettings) audio.applySettings(settings); },
    }, extra));
    if (!storageOk && overlay) {
      // Over the panel: 「保存して閉じる」 must not look as if it kept anything. (It says what a passing
      // 「保存できませんでした」 said, so that one goes.)
      stage.root.querySelectorAll(':scope > .ocbg-notice').forEach((n) => n.remove());
      const panel = host.querySelector('.ocbg-meta-settings-panel');
      const y = panel ? Math.max(16, panel.offsetTop - 76) : 40;
      host.appendChild(noticeLine(NO_STORAGE_LINES.join(''), 540, y, 840));
    }
  } else if (kind === 'cards') {
    overlay = Meta.mountOwnedCards(host, Object.assign(base, { expedition: ex }, extra));
  } else if (kind === 'journal') {
    overlay = Meta.mountJournal(host, Object.assign(base, journalCtx('explore'), extra));
  }
  if (overlay) overlay.host = host; else { host.remove(); sceneNotices(true); }
}
function closeOverlay() {
  if (!overlay) return;
  if (overlay.destroy) overlay.destroy();
  if (overlay.host) overlay.host.remove();
  overlay = null;
  sceneNotices(true);
  if (scene && scene.refresh) scene.refresh();
}

function journalCtx(mode) {
  return {
    expedition: live && live.raw,
    lineage: save && save.lineage,
    mode,
    memo: live ? live.memo : '',
    scribbles: live ? live.scribbles : [],
    onMemo: (text) => { if (live) { live.memo = String(text || ''); persist(); } },
  };
}

/**
 * Writes the save, unless another tab has written since this page last read or wrote it: then this
 * page's save is stale and is never written over the newer one (a dead heir would come back, a trace
 * would vanish or be taken twice: tier1 R1-16, INHERITANCE.md R-21); the page reloads the stored save
 * instead (markStale). `force` writes anyway (はじめから: the player chose to replace the record).
 * Returns whether the save was written.
 */
function persist(force = false) {
  if (!save) return false;
  if (!force && !inSync()) return false;
  if (live) save.life = { start: live.start, log: live.log.slice(), memo: live.memo, scribbles: live.scribbles, before: save.life && save.life.before };
  const stored = force ? storedStamp() : null;
  const next = { rev: Math.max(stamp ? stamp.rev : 0, stored ? stored.rev : 0) + 1, tab: TAB };
  const ok = writeSave(save, next);
  if (ok) { stamp = next; storageOk = true; }
  else if (storageOk) {
    // It worked until now (storage full, or taken away): say so once, over whatever is on screen.
    storageOk = false;
    console.warn('the save could not be written');
    stage.root.appendChild(noticeLine(SAVE_FAILED_TEXT, 560, 104, 800, 8000));   // under the top bars
  }
  return ok;
}

// A recorded call (a step, a card, a script event of a fight) is saved once the code that made it has
// run: one write per turn of the event loop, so a fight in progress is in the save after every move
// (tier1 R1-16 「戦闘中にゲームを閉じたら、閉じた場面から再開する」).
let persistQueued = false;
function persistSoon() {
  if (persistQueued) return;
  persistQueued = true;
  queueMicrotask(() => { persistQueued = false; if (live) persist(); });
}

// ------------------------------------------------------------------ one save, several tabs
/** Whether the stored save is still the one this page last read or wrote; marks the page stale when not. */
function inSync() {
  if (stale) return false;
  if (sameStamp(storedStamp(), stamp)) return true;
  markStale();
  return false;
}

/** Another tab wrote the save: this page writes nothing more, and goes back to the title with the stored save. */
function markStale() {
  if (stale) return;
  stale = true;
  console.warn('the save was written by another tab; this page reloads it');
  setTimeout(() => reloadStored(true), 0);
}

/** Reads the stored save into this page and shows the title (with STALE_TEXT when `notice`). */
function reloadStored(notice) {
  // answer the open questions first: they were asked about the save this page held
  stage.root.querySelectorAll('.ocbg-confirm .ocbg-btn:not(.is-primary)').forEach((b) => b.click());
  const o = openSave();
  save = o.save;
  unreadable = o.unreadable;
  stamp = o.stamp;
  stale = false;
  live = null;
  failing = false;
  if (audio.duckAmbience) audio.duckAmbience(1);
  toTitle();
  if (notice) stage.root.appendChild(noticeLine(STALE_TEXT, 560, 104, 800, 10000));
}

/** On the title (nothing of the save is on screen): take up a save another tab wrote, without a word. */
function syncIdle() {
  if (sameStamp(storedStamp(), stamp)) return;
  const o = openSave();
  save = o.save;
  unreadable = o.unreadable;
  stamp = o.stamp;
}

try {
  window.addEventListener('storage', (e) => {
    if (e.key !== null && e.key !== SAVE_KEY) return;
    if (stale || sameStamp(storedStamp(), stamp)) return;
    if (screen === 'title') {
      // The title shows only whether there is a save: draw it again when that changed, else read it on the next click.
      const had = !!(save && save.lineage);
      syncIdle();
      if (had !== !!(save && save.lineage) && !overlay && !stage.root.querySelector('.ocbg-confirm')) toTitle();
      return;
    }
    markStale();
  });
} catch (e) { /* no window events: persist's check still stops a stale write */ }

// ------------------------------------------------------------------ screens
function toTitle() {
  live = null;
  screen = 'title';
  if (audio.setAmbience) audio.setAmbience('title', 1200);
  show(Meta.mountTitle, {
    // A saved lineage is something to continue even before its first life: the hall named the next heir
    // (INHERITANCE.md R-02) and つづきから brings the same heir back.
    hasSave: !!(save && save.lineage),
    // A stored save that did not load: つづきから says so, and はじめから asks over the title as it stands.
    unreadable: !!unreadable,
    onNew: () => {
      syncIdle();
      const fresh = () => { unreadable = null; save = { lineage: Inh.newLineage((Date.now() ^ (Math.random() * 0x7fffffff)) >>> 0), life: null }; persist(true); toHall(); };
      if (save && save.lineage && (save.life || save.lineage.Lives.length)) {
        confirmCard(stage.root, {
          title: '新しい系譜を始める',
          body: 'これまでの生と痕跡の記録は消えます。',
          ok: 'はじめから', cancel: 'やめる', danger: true,
        }).then((ok) => { if (ok) fresh(); });
      } else if (unreadable) {
        // The question comes over the title as it stands (title.js asks first with ctx.unreadable), and
        // 「やめる」 leaves it there with the stored text untouched.
        confirmCard(stage.root, {
          title: '新しい系譜を始める',
          body: '読み込めなかった記録に、新しい系譜を上書きします。' +
            (unreadable.kept ? '控えは残ります。' : '控えを残せなかったため、記録は失われます。') + reasonOf(unreadable.error),
          ok: 'はじめから', cancel: 'やめる', danger: true,
        }).then((ok) => { if (ok) fresh(); });
      } else fresh();
    },
    onContinue: () => { syncIdle(); if (save && save.life) resumeLife(); else if (save && save.lineage) toHall(); else toTitle(); },
    onSettings: () => openOverlay('settings'),
  }, 'ocbg-title');
  const lines = [...(unreadable ? UNREADABLE_LINES(unreadable.kept) : []), ...(storageOk ? [] : NO_STORAGE_LINES)];
  if (lines.length) {
    // Under the menu, inside the title's root and below its fade (z-index under the fade's), so it comes
    // and goes with the title; under any overlay too, and hidden while one is open (sceneNotices).
    const root = sceneHost.querySelector('.ocbg-meta-title');
    const note = noticeLine(lines.join('\n'), 152, 840 - Math.max(0, lines.length - 2) * 25, 560);
    note.classList.add(APP_NOTICE);
    note.style.zIndex = '40';
    const fade = root && root.querySelector('.ocbg-meta-fade');
    if (fade) root.insertBefore(note, fade); else (root || sceneHost).appendChild(note);
  }
}

function toHall() {
  if (stale) return;
  live = null;
  screen = 'hall';
  if (!save || !save.lineage) { unreadable = null; save = { lineage: Inh.newLineage(Date.now() >>> 0), life: null }; persist(); }
  if (audio.setAmbience) audio.setAmbience('hall', 1500);
  show(Meta.mountHall, {
    lineage: save.lineage,
    onDepart: (lifeStart) => toDeparture(lifeStart && lifeStart.Lineage ? lifeStart : Inh.startLife(save.lineage)),
    // 設定・セーブ here offers 「保存してタイトルへ」: the lineage is already saved, so it reads as going back to the title.
    onSettings: () => openOverlay('settings', LINEAGE_SAVE),
    onTitle: toTitle,
  }, 'ocbg-hall');
}

function toDeparture(lifeStart) {
  if (stale) return;
  screen = 'departure';
  show(Meta.mountDeparture, {
    lifeStart,
    // The forty every character owns plus the prototype deck's kinds (Expedition owns its start deck's kinds too).
    owned: [...new Set([...Expedition.InitialKinds, ...PrototypeDeck.Kinds.map((d) => d.Id)])],
    savedDeck: save.deckSave || null,
    onBack: toHall,
    // 設定・セーブ opens here too: the gear at the top right, and Esc (CAMP_FACILITIES_DESIGN.md §3.5). The lineage is
    // saved and the half-built deck is kept as 出立's draft, so 「保存してタイトルへ」 leaves nothing behind.
    onSettings: () => openOverlay('settings', LINEAGE_SAVE),
    onSetOut: (choice) => {
      if (!inSync()) return;              // another tab moved the lineage on: this hall was out of date
      const before = Inh.toJSON(save.lineage);
      save.lineage = lifeStart.Lineage;             // the life is now in progress
      save.life = { before };
      if (choice.deckSave) save.deckSave = choice.deckSave;
      const start = makeStart(lifeStart, choice);
      // the heir's own memo sheet starts empty; a survivor's journal comes in through start.journal
      // (its pages, 開示度 and 走り書き, the survivor's memo among them: Inheritance.heirJournal)
      beginLife(start, [], '', null);
    },
  }, 'ocbg-departure');
}

/**
 * Builds the life from its start and replays its log. A fight the log left in progress comes back where
 * it stood and the battle scene takes it up (tier1 R1-16, #462); only a fight whose calls no longer
 * replay (a save from another build) starts over, with a warning.
 */
function beginLife(start, log, memo, scribbles) {
  if (stale) return;
  let setup = lifeSetup(start);
  let res;
  try { res = replayLife(setup.expedition, log); }
  catch (e) {
    const trimmed = trimUnfinished(log);
    if (trimmed.length === log.length) throw e;
    console.warn('the fight in progress did not replay; it starts over', e);
    setup = lifeSetup(start);
    res = replayLife(setup.expedition, trimmed);
  }
  const { expedition, life } = setup;
  const rec = recordExpedition(expedition, res.kept, persistSoon);
  live = {
    expedition: rec.expedition, raw: expedition, log: rec.log, start, life, memo: memo || '',
    scribbles: lifeScribbles(start, scribbles, save && save.lineage),
  };
  persist();
  if (res.open) resumeFight(rec.wrapSource(res.open.source));
  else toExplore();
}

/** Back into the fight a reload found in progress: the battle scene plays on from the source's frame. */
function resumeFight(source) {
  screen = 'battle';
  if (audio.setAmbience) audio.setAmbience(live.raw.Session.State.Profile.Layer, 1200);
  runEncounter(source).then(() => { if (stale || !live) return; persist(); toExploreBack(); });
}
// A life that does not replay (a save from another build, a bug): nothing is overwritten until the
// player decides. The stored save is copied aside first (save.keepBrokenSave); 「あきらめる」 drops the
// life and the lineage goes back to how it stood before that departure; 「タイトルへ」 leaves the save as it is.
function resumeLife() {
  const l = save.life;
  try { beginLife(l.start, l.log || [], l.memo, l.scribbles); }
  catch (e) {
    console.error('resume failed; the life is kept until the player decides', e);
    live = null;
    const kept = keepBrokenSave();
    toTitle();
    confirmCard(stage.root, {
      title: '生を再開できません',
      body: '保存された生を読み込めませんでした。' + (kept ? '記録は消さずに控えとして残しています。' : '') +
        'あきらめると、この生は無かったことになり、出立の前から出直します。（' + String((e && e.message) || e).slice(0, 80) + '）',
      ok: 'あきらめる', cancel: 'タイトルへ', danger: true,
    }).then((ok) => {
      if (!ok || save.life !== l) return;
      if (l && l.before) { try { save.lineage = Inh.fromJSON(l.before); } catch (e2) { /* keep */ } }
      save.life = null;
      persist();
      toHall();
    });
  }
}

function toExplore() {
  if (stale || !live) return;
  screen = 'explore';
  const raw = live.raw;
  show(mountExploreScene, {
    expedition: live.expedition,
    inheritance: {
      traceView: (layer) => Inh.traceNodeView(save.lineage, layer),
      // The 走り書き the life carries now: the trace card says which of the trace's it already holds.
      scribbles: () => (live ? live.scribbles.slice() : []),
      take: (picks) => {
        if (!inSync()) return null;       // another tab collected or moved on: nothing is taken here
        // A life that is over takes nothing (the 瘴気死 on the trace's own step): the trace stays for a living heir
        // (INHERITANCE R-09 / R-10, EXTENSIONS X-3). The scene never offers it then; this is the second guard.
        if (!live || raw.Session.State.IsOver) return null;
        const layer = raw.Session.State.Profile.Layer;
        const view = Inh.traceNodeView(save.lineage, layer);
        if (!view || !view.CanTake) return null;
        const r = Inh.takeTrace(save.lineage, view.TraceId, picks || [], { Layer: layer });
        save.lineage = r.Lineage;
        const taken = JSON.parse(JSON.stringify(r.Taken));
        // What the take did (the 段 and 開示度 this heir ends with, the copies that joined the deck): the
        // 受け取った遺産 card is written from it (TraceGain), not from the dead heir's numbers.
        const result = live.expedition.Inherit(taken) || {};
        const added = addScribbles(live.scribbles, taken.Journal && taken.Journal.Scribbles);
        live.scribbles = added.list;
        persist();
        return Object.freeze({ ...r.Taken, Result: Object.freeze({ ...result, Scribbles: added.rows }) });
      },
    },
    onBattle: () => runEncounter().then(() => { if (stale || !live) return; persist(); toExploreBack(); }),
    onLifeEnd: (ending) => endLife(ending),
    onOverlay: (kind) => openOverlay(kind),
    onSaveQuit: saveQuit,
    onPersist: persist,
    onCarving: () => { if (!inSync()) return; save.lineage = Inh.noteCarving(save.lineage); persist(); },
  }, 'ocbg-explore');
}
// After the fights the explore scene comes back with the result card (it calls Return itself).
function toExploreBack() { toExplore(); }

// Test switches (never in the shipped URL): ?autobattle fights every battle unattended (the source's AutoCardId:
// its suggestion, also on a fight a reload brought back), so a playtest can walk whole lives through the real
// screens; ?debug exposes window.__ocb.
const QUERY = (() => { try { return window.location.search || ''; } catch (e) { return ''; } })();
const AUTOBATTLE = /[?&]autobattle\b/.test(QUERY);
let fightNow = null;               // the source of the fight on screen (debug only)
if (/[?&]debug\b/.test(QUERY)) window.__ocb = { get save() { return save; }, get live() { return live; }, get fight() { return fightNow; }, settings, audio, stage };

function autoFight(source) {
  for (let guard = 0; guard < 20000 && !source.Finished; guard++) {
    if (source.WaitingForPlayer) {
      // AutoCardId, not SuggestedCardId: a fight a reload brought back was replayed as the player's page built it
      // (StartNext(false): no suggestions), and with no card suggested the run only ended turns and the heir died
      const id = source.AutoCardId;
      const face = id ? DepictionText.Find(source.Frame.Hand, id) : null;
      if (face) source.TryPlay(id, DepictionText.RequiredZone(face.Aim));
      else source.EndTurn();
    } else { try { source.Next(); } catch (e) { source.AdvanceAuto(); } }   // a throwing call is not logged
  }
}

// The ambience's level under a battle (Audio.duckAmbience takes a gain 0..1; 1 is no ducking).
const BATTLE_DUCK = 0.4;

/**
 * Fight every fight of the waiting encounter, one battle scene per fight. `resumed` is the recorded
 * source of a fight a reload found in progress (beginLife): it is fought first, from where it stood.
 */
async function runEncounter(resumed = null) {
  const ex = live.expedition;
  const battle = ex.Battle || ex.StartEncounter();
  if (!battle) return;
  if (audio.duckAmbience) audio.duckAmbience(BATTLE_DUCK);
  while (!battle.Over) {
    // The fight is finished and saved the moment the core decides it, before the fall and the result
    // card play (recorder.settleWhenDecided, tier1 R1-16): a reload there keeps the outcome. Every call
    // before that is saved as it is made (persistSoon), so a reload mid-fight comes back to the fight.
    const fight = settleWhenDecided(battle, resumed || battle.StartNext(AUTOBATTLE), persist);
    resumed = null;
    fightNow = fight.source;
    if (fight.settled) continue;
    if (AUTOBATTLE) { autoFight(fight.source); fight.finish(); continue; }
    screen = 'battle';
    await new Promise((resolve) => {
      show(mountBattleScene, {
        source: fight.source,
        layer: live.raw.Session.State.Profile.Layer,
        layerTitle: live.raw.Session.Screen().LayerTitle,
        miasmaPercent: battle.HandIn.MiasmaPercent,
        nodeKind: battle.HandIn.Kind,
        chain: { index: battle.Index + 1, total: battle.Count },
        disclosureOf: (id) => live.raw.DisclosureOf(id),
        openJournal: (host, opts) => Meta.mountJournal(host, Object.assign({ stage, audio, settings }, journalCtx('battle'), opts)),
        // Esc and the gear open 設定・セーブ in a fight too, with 「保存して閉じる」: a fight is saved move by move
        // (tier1 R1-16, #462), so つづきから comes back to the same moment (settings.js 'battle').
        settingsInBattle: true,
        onSettings: () => openOverlay('settings', { inBattle: true }),
        onSettingsChange: (next) => { Object.assign(settings, next); writeSettings(settings); },
        onError: (err) => battleFailed(err),
        onFinished: resolve,
      }, 'ocbg-battle');
    });
    fight.finish();   // a no-op once the decision finished it
  }
  if (audio.duckAmbience) audio.duckAmbience(1);
}

// The battle scene could not go on (the source threw; the call that threw is not in the log). The fight
// in progress is dropped from the save (recorder.trimUnfinished) and the life is loaded again: the node
// waits on the same fight, which starts over.
let failing = false;
function battleFailed(err) {
  if (failing) return;
  failing = true;
  console.error('battle failed', err);
  if (live) live.log.splice(0, live.log.length, ...trimUnfinished(live.log));
  persist();
  confirmCard(stage.root, {
    title: '戦闘を続けられません',
    body: 'この戦闘をはじめからやり直します。（' + String((err && err.message) || err).slice(0, 80) + '）',
    ok: 'やり直す', cancel: 'タイトルへ',
  }).then((ok) => { failing = false; if (ok) resumeLife(); else toTitle(); });
}

function endLife() {
  if (!live || !inSync()) return;         // another tab holds a newer save: this death or survival is not recorded here
  const raw = live.raw;
  const state = raw.Session.State;
  if (!state.IsOver || state.Phase === RunPhase.Exploring) return;
  const place = Inh.placeFromState(state);
  const journal = lifeJournal(raw, live);
  const opts = endOptions(raw, state);
  save.lineage = Inh.recordEnd(save.lineage, null, place, raw.Mastery, journal, opts);
  save.life = null;
  live = null;
  persist();
  if (audio.setAmbience) audio.setAmbience('hall', 2000);
  screen = 'ending';
  show(Meta.mountEnding, { ending: Inh.endingView(save.lineage), lineage: save.lineage, onHall: toHall }, 'ocbg-ending');
}

// 「保存して閉じる」 / 「保存してタイトルへ」: everything is written, then the title. From a fight the battle scene is
// dropped where it stands (its moves are in the log: つづきから replays them and the fight goes on from there).
function saveQuit() {
  persist();
  closeOverlay();
  if (audio.duckAmbience) audio.duckAmbience(1);
  toTitle();
}

// ------------------------------------------------------------------ boot
function start() {
  if (window.claude && window.claude.hot && window.claude.hot.snapshot) {
    window.claude.hot.snapshot(() => { persist(); return { saved: true }; });
  }
  toTitle();
}
// In the Artifact viewer, an update to the page arrives through window.claude.hot: boot through its ready hook.
function boot() {
  const hot = window.claude && window.claude.hot;
  if (hot && typeof hot.ready === 'function') hot.ready(() => start()); else start();
}
const fontsReady = document.fonts && document.fonts.ready ? document.fonts.ready : Promise.resolve();
Promise.race([fontsReady, new Promise((r) => setTimeout(r, 1500))]).then(boot);
