// Dev page for the JS extension 「手記の敵の頁と地図の敵の情報」 (docs/EXTENSIONS.md): a life whose Expedition
// carries a bound JournalIntel (as src/app/life.js builds it) and an inherited journal, shown the way the
// router shows it — the exploration scene with the journal drawer (explore mode, the memo writable), and
// the battle scene of the entry fight with the drawer opened by J (battle mode, the memo read only).
// Build: node tools/devbuild.mjs dev/journal-ext.js → dev/out/journal-ext.html
// Query: ?seed=5 &journal={"polearm_warped":1,...} (the inherited 開示度) &surveyed=1 (walk to the survey node)
//        &layer=N (clear layers straight down first, the stand-in winning) &node=elite|boss (#battle: fight that
//        node of the layer instead of its entry) &trial=1 (RunStart.Trial) &foes=a,b,c (#battle: the drawer's foes)
// Hash: #explore (drawer on an enemy page) · #explore-map (the map only) · #battle (battle scene; press J)
import '../src/scenes/common/common.css';
import { createStage } from '../src/scenes/common/stage.js';
import { createAudio } from '../src/scenes/audio/Audio.js';
import { Expedition } from '../src/bridge/exploration/Expedition.js';
import { RunStart } from '../src/bridge/explorationScript/ExplorationSession.js';
import { ScreenAction } from '../src/bridge/explorationScript/ExplorationScreen.js';
import { NodeKind } from '../src/core/dungeon/NodeKind.js';
import { mountExploreScene } from '../src/scenes/explore/ExploreScene.js';
import { mountBattleScene } from '../src/scenes/battle/BattleScene.js';
import { mountJournal } from '../src/scenes/meta/index.js';
import { JournalIntel } from '../src/app/intel.js';

const q = new URLSearchParams(location.search);
const seed = BigInt(q.get('seed') || '5');
const journal = JSON.parse(q.get('journal') || '{"polearm_warped":1,"shadow_hound":1,"miasma_priest":2,"armored_warden":2}');
const mode = (location.hash || '#explore').slice(1);

const stage = createStage(document.getElementById('app'));
const audio = createAudio(window.OCB, { master: 0, effects: 0, ambience: 0, muted: true });
const settings = { shortFx: q.get('short') === '1', speed: 1.25 };

const intel = new JournalIntel();
const expedition = new Expedition(new RunStart({ Seed: seed, Trial: q.get('trial') === '1' }), null, [], null, intel, journal);
intel.bind(expedition);

/** A breadth-first route to a node (not through the boss unless it is the goal), the stand-in winning on the way. */
function walkTo(goal, winLast) {
  const session = expedition.Session;
  if (session.HasPendingBattle) session.Act(ScreenAction.WinBattle);
  const map = session.State.Map;
  const prev = new Map();
  const queue = [session.State.CurrentNodeId];
  const seen = new Set(queue);
  while (queue.length) {
    const at = queue.shift();
    for (const next of map.Neighbours(at)) {
      if (seen.has(next) || (map.Node(next).Kind === NodeKind.Boss && next !== goal)) continue;
      seen.add(next); prev.set(next, at); queue.push(next);
    }
  }
  const route = [];
  for (let at = goal; at !== session.State.CurrentNodeId; at = prev.get(at)) route.unshift(at);
  for (const id of route) {
    session.Step(id);
    if (session.HasPendingEvent) session.Act('DeclineEvent');
    if (session.HasPendingFound) session.Act('LeaveFound');
    if (session.HasPendingBattle && (id !== goal || winLast)) session.Act(ScreenAction.WinBattle);
  }
}
// Clear layers straight down to ?layer (each layer's boss beaten by the stand-in).
for (let target = Number(q.get('layer') || '1'); expedition.Session.State.Profile.Layer < target;) {
  const session = expedition.Session;
  walkTo(session.State.Map.Nodes.find((n) => n.Kind === NodeKind.Boss).Id, true);
  session.Act(ScreenAction.GoToInterlude);
  session.Act(ScreenAction.Descend);
}
let memo = '門竜は鉄壁を置く前に叩く。\n槍の竜兵は間合い 0 で押し返してくる。';
const scribbles = [{ Generation: 1, Name: 'アルマ', Text: '大黒蛇は懐に入ると締め付けてくる。間合い 2 で待つこと。' }];
const journalCtx = (m) => ({ stage, audio, settings, expedition, mode: m, memo, scribbles, onMemo: (t) => { memo = t; } });

// Walk to the layer's 情報収集 node when asked (the stand-in battle reported won on the way).
function walkToSurvey() {
  const survey = expedition.Session.State.Map.Nodes.find((n) => n.Kind === NodeKind.Survey);
  if (survey) walkTo(survey.Id, true);
}

let overlay = null;
const host = stage.layer('dev-scene');
if (mode === 'battle') {
  if (q.get('surveyed') === '1') walkToSurvey();
  const kind = q.get('node') === 'elite' ? NodeKind.Elite : q.get('node') === 'boss' ? NodeKind.Boss : null;
  if (kind) walkTo(expedition.Session.State.Map.Nodes.find((n) => n.Kind === kind).Id, false);
  const battle = expedition.StartEncounter();
  const source = battle.StartNext(false);
  window.__src = source;
  window.__qa = { source: null, scene: null, finished: 0 };
  window.__qa.scene = mountBattleScene(host, {
    stage, audio, settings, source,
    layer: expedition.Session.State.Profile.Layer,
    layerTitle: expedition.Session.Screen().LayerTitle,
    miasmaPercent: battle.HandIn.MiasmaPercent,
    nodeKind: battle.HandIn.Kind,
    chain: { index: battle.Index + 1, total: battle.Count },
    disclosureOf: (id) => expedition.DisclosureOf(id),
    // As src/app/main.js: journalCtx('battle') plus what the battle scene hands (enemyIds, disclosureOf, onClose).
    // ?foes=a,b,c (dev only) hands the drawer other foes than the fight's, to see a 3 体戦's six tabs.
    openJournal: (h, opts) => mountJournal(h, Object.assign(journalCtx('battle'), opts, q.get('foes') ? { enemyIds: q.get('foes').split(',') } : {})),
    onSettings: () => {},
    onFinished: () => { window.__qa.finished++; },
  });
} else {
  if (q.get('surveyed') === '1') walkToSurvey();
  else if (expedition.Session.HasPendingBattle) expedition.Session.Act(ScreenAction.WinBattle);
  const openJournal = (pageId) => {
    if (overlay) { overlay.destroy(); overlay.host.remove(); overlay = null; return; }
    const oh = stage.layer('dev-over');
    oh.style.zIndex = '500';
    overlay = mountJournal(oh, Object.assign(journalCtx('explore'), {
      pageId,
      onClose: () => { if (overlay) { overlay.destroy(); oh.remove(); overlay = null; } },
    }));
    overlay.host = oh;
  };
  mountExploreScene(host, {
    stage, audio, settings, expedition, inheritance: null,
    onBattle: async () => {}, onLifeEnd: () => {}, onSaveQuit: () => {}, onPersist: () => {}, onCarving: () => {},
    onOverlay: (kind) => { if (kind === 'journal') openJournal(); },
  });
  if (mode === 'explore') openJournal(q.get('page') || 'enemy-miasma_priest');
}

window.__dev = { expedition, intel, get memo() { return memo; } };
