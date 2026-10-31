// Dev page for the exploration scene: a real Expedition (seed 1, the prototype deck, the trial
// loadout) whose fights are fought unattended by the ported CoreBattleSource auto-play
// (EncounterBattle.StartNext(true) → run the source to the end → Finish), so the whole map can be walked.
// Query: ?seed=1 &miasma=0 (start gauge) &trace=1 (a dev trace on layer 1) &fast=1 (演出短縮) &noaudio=1
//        &remount=1 (like the app: the scene is destroyed for the battle and a new one is mounted after it,
//        which brings the encounter back with Expedition.Return)
//        &trial=1 (the world extension 「探索の持ち物とイベント」, docs/EXTENSIONS.md): the departure's loadout
//        from &tools=a,b,c &items=x,y,z (catalogue ids; defaults below) through Loadouts.toRunLoadout, the battle
//        tools to the battles, and Expedition options { trial: true }. &hp=50 sets the starting HP.
import '../src/scenes/common/common.css';
import { createStage, h } from '../src/scenes/common/stage.js';
import { createAudio } from '../src/scenes/audio/Audio.js';
import { Expedition } from '../src/bridge/exploration/Expedition.js';
import { RunStart } from '../src/bridge/explorationScript/ExplorationSession.js';
import { DepictionText } from '../src/bridge/depictionScript/DepictionTypes.js';
import { SevenLayers } from '../src/core/dungeon/SevenLayers.js';
import { NodeKind } from '../src/core/dungeon/NodeKind.js';
import { RunPhase } from '../src/core/dungeon/ExplorationState.js';
import * as Inh from '../src/core/inheritance/Inheritance.js';
import { Enemies } from '../src/core/battle/Enemies.js';
import { mountExploreScene } from '../src/scenes/explore/ExploreScene.js';
import { Loadout } from '../src/core/dungeonContent/Loadout.js';
import { ItemCatalogue } from '../src/core/dungeonContent/ItemCatalogue.js';
import { EffectSide } from '../src/core/dungeonContent/ItemEffect.js';
import { toRunLoadout } from '../src/bridge/exploration/Loadouts.js';

const q = new URLSearchParams(location.search);
const seed = BigInt(q.get('seed') || '1');
const startMiasma = Number(q.get('miasma') || '0');
const remount = q.get('remount') === '1';
const settings = { shortFx: q.get('fast') === '1' };

const stage = createStage(document.getElementById('app'));
const audio = q.get('noaudio') === '1' ? null : createAudio(window.OCB, undefined);
if (audio && audio.bindUnlock) audio.bindUnlock(document);
else if (audio) document.addEventListener('pointerdown', () => audio.unlock(), { once: true });

// ---- dev-only: a death trace on layer 1, so the 痕跡 pick can be tried (the app does this through mapSpecFor) ----
let lineage = null;
let inheritance = null;
if (q.get('trace') === '1') {
  const profile = SevenLayers.Of(1);
  profile.MapSpec = profile.MapSpec.With(NodeKind.Trace, 1); // DEV STUB: the integrator draws the map from mapSpecFor
  lineage = Inh.newLineage(7n);
  let started = Inh.startLife(lineage, 11n);
  lineage = started.Lineage;
  lineage = Inh.recordDeath(lineage, null, { Layer: 1, NodeId: 4, NodeKind: NodeKind.Battle, Ending: RunPhase.Fallen },
    [{ CardId: 'thrust', Rank: 1, Name: '突き' }, { CardId: 'kesa_cut', Rank: 1, Name: '袈裟斬り' }, { CardId: 'reach_thrust', Rank: 2, Name: '伸び突き' }],
    { Disclosure: { polearm_warped: 1 }, Pages: 1, Memo: '槍の間合いは 2 まで。' },
    { enemyName: (id) => { try { return Enemies.ById(id).Name; } catch (e) { return id; } } });
  started = Inh.startLife(lineage, 1n);
  lineage = started.Lineage;
  inheritance = {
    traceView: (layer) => Inh.traceNodeView(lineage, layer),
    take: (picks) => {
      const tv = Inh.traceNodeView(lineage, 1);
      const r = Inh.takeTrace(lineage, tv.TraceId, picks, { Layer: 1 });
      lineage = r.Lineage;
      return r.Taken;
    },
  };
}

const trialMode = q.get('trial') === '1';
function makeExpedition() {
  if (!trialMode) return new Expedition(new RunStart({ Seed: seed, Loadout: RunStart.TrialLoadout(), MiasmaPercent: startMiasma }));
  // the departure's choice, as the game hands it in (src/scenes/meta/departure.js → Loadouts.toRunLoadout)
  const tools = (q.get('tools') ?? 'kokugen_no_suna,utsushi_no_tobari,toshin_no_kakera').split(',').filter(Boolean);
  const items = (q.get('items') ?? 'yakuso,shirube_no_ishi,koi_hoshiniku').split(',').filter(Boolean);
  const slots = Math.max(3, tools.length);
  const battleToolIds = tools.filter((id) => ItemCatalogue.Tool(id).Side === EffectSide.Battle);
  const loadout = toRunLoadout(Loadout.Of(tools, items, slots), slots);
  const hp = Number(q.get('hp') || '50');
  return new Expedition(new RunStart({ Seed: seed, Loadout: loadout, MiasmaPercent: startMiasma, Hp: hp }),
    null, battleToolIds, null, null, null, { trial: true });
}
const expedition = makeExpedition();

// ---- the unattended battle (the app mounts the real battle scene here) ----
const veil = h('div', { class: 'dev-battle', style: 'position:absolute;inset:0;z-index:950;display:none;align-items:center;justify-content:center;background:color-mix(in srgb, var(--ground) 88%, transparent);font:700 28px/1 var(--font-display);letter-spacing:.1em;color:var(--ink)' }, '（dev: 自動戦闘）');
stage.root.appendChild(veil);
const wait = (ms) => new Promise((r) => setTimeout(r, ms));
const log = [];

function runUnattended(source, limit = 6000) {
  let n = 0;
  while (!source.Finished && n < limit) {
    n++;
    if (!source.WaitingForPlayer) { source.AdvanceAuto(); continue; }
    const id = source.SuggestedCardId;
    if (!id) { source.EndTurn(); continue; }
    const face = DepictionText.Find(source.Frame.Hand, id);
    source.TryPlay(id, DepictionText.RequiredZone(face.Aim));
  }
  return source.Finished;
}

async function onBattle(exp) {
  const battle = exp.StartEncounter();
  if (!battle) return;
  if (remount) scene.destroy(); // the app's router replaces the scene with the battle scene
  veil.style.display = 'flex';
  try {
    while (!battle.Over) {
      const source = battle.StartNext(true);
      if (!runUnattended(source)) { exp.Abandon(); return; }
      battle.Finish(source);
      log.push({ result: source.State.Result, hp: battle.HpAfter });
      await wait(settings.shortFx ? 80 : 420);
    }
  } catch (e) {
    // A fault in the ported battle core (reported as a blocker): leave the node to the stand-in card.
    console.warn('[dev] the unattended battle failed:', e.message);
    exp.Abandon();
  } finally {
    veil.style.display = 'none';
    if (remount) mount(); // …and mounts a fresh exploration scene once the encounter is over
  }
}

const host = stage.layer('dev-explore');
let scene = null;
const persisted = { count: 0, carvings: 0 };
const mount = () => {
  scene = mountExploreScene(host, sceneCtx);
  if (window.__dev) window.__dev.scene = scene;
};
const sceneCtx = {
  stage,
  audio,
  settings,
  expedition,
  inheritance,
  onBattle,
  onLifeEnd: (ending) => {
    window.__dev.ended = { Ending: ending.Ending, Title: ending.Title, Layer: ending.Layer, NodeId: ending.NodeId, NodeKind: ending.NodeKind };
    console.log('[dev] life ended', JSON.stringify(window.__dev.ended));
  },
  onOverlay: null, // the scene's fallback overlays
  onSaveQuit: () => console.log('[dev] save and quit'),
  onPersist: () => { persisted.count++; },
  onCarving: () => { persisted.carvings++; },
};
mount();

window.__dev = { expedition, session: expedition.Session, scene, stage, log, persisted, get lineage() { return lineage; } };
