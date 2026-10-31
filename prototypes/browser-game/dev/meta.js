// Dev page for the meta screens: a switcher over every screen, with a sample lineage (two past lives —
// the first fell on layer 3 and left a trace, the second closed alive on layer 4 and is the survivor —
// and the third heir about to be named) and real ported data (Inheritance, CardCatalog, Mastery,
// DeckBuilder, DepartureBuilder, ItemCatalogue, JournalBook / JournalTerms, BackdropSpec).
// Build: node tools/devbuild.mjs dev/meta.js → dev/out/meta.html. window.__meta.show(name) switches.
import '../src/scenes/common/common.css';
import { createStage, h } from '../src/scenes/common/stage.js';
import { createAudio } from '../src/scenes/audio/Audio.js';
import { mountTitle, mountHall, mountDeparture, mountEnding, mountSettings, mountOwnedCards, mountJournal } from '../src/scenes/meta/index.js';
import { newLineage, startLife, recordDeath, recordSurvival, mergeMastery, endingView } from '../src/core/inheritance/Inheritance.js';
import { CardCatalog } from '../src/core/battle/CardCatalog.js';
import { MasteryBook, MasteryProfiles, CardMastery } from '../src/core/battle/Mastery.js';
import { JournalBook, JournalContext, JournalPage, JournalRow, JournalTab } from '../src/bridge/journal/JournalBook.js';
import { JournalTerms } from '../src/bridge/journal/JournalTerms.js';
import { Expedition } from '../src/bridge/exploration/Expedition.js';
import { RunStart } from '../src/bridge/explorationScript/ExplorationSession.js';

// ---------------------------------------------------------------- sample data (dev only)
const cardName = (id, rank) => (MasteryProfiles.Find(id) && MasteryProfiles.Find(id).NameAt(rank)) || CardCatalog.ById(id).Name;
const ENEMY_NAMES = { miasma_priest: '大黒蛇 セルク', shadow_hound: '瘴牙の走竜', polearm_warped: '錆槍の竜兵', armored_warden: '鉄壁の門竜' };
const enemyName = (id) => ENEMY_NAMES[id] || id;

function sampleLineage() {
  let L = newLineage(20261020);
  const s1 = startLife(L);
  L = recordDeath(s1.Lineage, null, { Layer: 3, NodeId: 14, NodeKind: 'Boss', Ending: 'Fallen' },
    [{ CardId: 'thrust', Rank: 2 }, { CardId: 'kesa_cut', Rank: 1 }, { CardId: 'brace', Rank: 1 }, { CardId: 'observe', Rank: 1 }, { CardId: 'rock_stance', Rank: 0 }],
    { Disclosure: { miasma_priest: 2, shadow_hound: 1 }, Pages: 1, Memo: '大黒蛇は懐に入ると締め付けてくる。間合い 2 で待つこと。' },
    { cardName, enemyName });
  const afterDeath = L;
  const s2 = startLife(L);
  L = recordSurvival(s2.Lineage, null, { Layer: 4, NodeId: 9, NodeKind: 'Rest', Ending: 'Survived' },
    [{ CardId: 'reach_thrust', Rank: 1 }, { CardId: 'footwork', Rank: 2 }],
    { Disclosure: { polearm_warped: 2, armored_warden: 1 }, Pages: 0, Memo: '' },
    { cardName, enemyName });
  return { lineage: L, afterDeath };
}
const { lineage, afterDeath } = sampleLineage();
// Three deaths: two on layer 2 (the younger trace waits behind the older one), then a miasma death on layer 5.
function crowdedLineage() {
  let L = newLineage(99);
  const die = (layer, kind, ending, deck, journal) => { const s = startLife(L); L = recordDeath(s.Lineage, null, { Layer: layer, NodeKind: kind, Ending: ending }, deck, journal, { cardName, enemyName }); };
  die(2, 'Battle', 'Fallen', [{ CardId: 'thrust', Rank: 1 }, { CardId: 'brace', Rank: 2 }], { Disclosure: { polearm_warped: 1 }, Pages: 0, Memo: '' });
  die(2, 'Event', 'Fallen', [{ CardId: 'observe', Rank: 1 }], { Disclosure: {}, Pages: 0, Memo: '' });
  die(5, 'Elite', 'MiasmaDeath', [{ CardId: 'kesa_cut', Rank: 2 }, { CardId: 'lunge', Rank: 1 }, { CardId: 'focus', Rank: 1 }], { Disclosure: { armored_warden: 2 }, Pages: 2, Memo: '門竜は 3 ターン目に必ず構える。' });
  return L;
}
const crowded = crowdedLineage();
const firstLineage = newLineage(7);
const lifeStart = startLife(lineage);
// The kinds a character owns from the start (#1〜#40), and three learned on the way.
const owned = CardCatalog.All.slice(0, 40).map((d) => d.Id).concat(['dash_in', 'gale_thrust', 'wolf_stance']);
const book = new MasteryBook('thrust', mergeMastery(null, [
  { CardId: 'thrust', Rank: 2, Ticks: 11 }, { CardId: 'kesa_cut', Rank: 1, Ticks: 5 }, { CardId: 'water_stance', Rank: 1, Ticks: 4 },
  { CardId: 'observe', Rank: 3, Ticks: 12 }, { CardId: 'footwork', Rank: 0, Ticks: 2 }, { CardId: 'brace', Rank: 1, Ticks: 3 },
], (id, t, r) => new CardMastery(id, t, r)), null);
const deckIds = ['thrust', 'kesa_cut', 'water_stance', 'observe', 'brace', 'footwork', 'reach_thrust', 'rock_stance', 'lunge', 'back_leap'];

function journalPages() {
  return [
    new JournalPage('enemy-miasma_priest', JournalTab.Enemy, '大黒蛇 セルク', [
      new JournalRow('姿', '壁に巻きついた大蛇。背の結晶が藍紫に光る', 1),
      new JournalRow('間合いの傾向', '間合い 0 では締め付け、1〜2 では尾で払う。3 以上では動かない', 2),
      new JournalRow('得意な手', '足を縛る（鈍足）。毎ターン狙ってくる', 2),
      new JournalRow('弱点', '崩しの後、守りが薄くなる', 2),
    ], 2),
    new JournalPage('enemy-shadow_hound', JournalTab.Enemy, '瘴牙の走竜', [
      new JournalRow('姿', '四つ足で低く走る竜', 1),
      new JournalRow('間合いの傾向', '遠いと駆け寄り、近いと噛みつく', 2),
      new JournalRow('弱点', '身を低くした次のターン', 2),
    ], 1),
    new JournalPage('dungeon-3', JournalTab.Dungeon, '第 3 層　大黒蛇の住処', [
      new JournalRow('瘴気の濃さ', '1 刻限ごとに +4%'),
      new JournalRow('刻限', '10（寄り道なしの踏破は 6）'),
      new JournalRow('ノード', '通常戦闘 4・精鋭 1・階層ボス 1・休息 2・情報収集 1'),
      new JournalRow('情報収集', 'まだ'),
    ]),
    new JournalPage('memo', JournalTab.Memo, 'メモ', [new JournalRow('', '大黒蛇は懐に入ると締め付けてくる。\n間合い 2 で待つこと。')]),
    JournalTerms.Page(),
  ];
}

// A running life the way src/app hands it to the overlays (ctx.expedition only): the prototype deck,
// the journal's 開示度 from the sample lineage.
let sampleExpedition = null;
function expedition() {
  if (!sampleExpedition) sampleExpedition = new Expedition(new RunStart({ Seed: 20261020n }), null, [], 'thrust', null, { armored_warden: 1, polearm_warped: 2 });
  return sampleExpedition;
}
const scribbles = [{ Generation: 1, Name: 'アルマ', Text: '大黒蛇は懐に入ると締め付けてくる。間合い 2 で待つこと。' }];
let memo = '門竜は 3 ターン目に必ず構える。\n構えの前に崩しを入れる。';

// ---------------------------------------------------------------- page
const app = document.getElementById('app');
const stage = createStage(app);
const settings = { speed: 1.25, shortFx: false };
const audio = createAudio(window.OCB, { master: 0.8, effects: 1, ambience: 0.6, muted: false });
audio.bindUnlock(document);

let base = null; // the screen
let over = null; // an overlay
let baseLayer = null;
let overLayer = null;
function clear() {
  if (over) { over.destroy(); over = null; }
  if (overLayer) { overLayer.remove(); overLayer = null; }
  if (base) { base.destroy(); base = null; }
  if (baseLayer) { baseLayer.remove(); baseLayer = null; }
}
const log = (...a) => console.log('[meta]', ...a);
function screen(fn) { baseLayer = stage.layer('dev-base'); base = fn(baseLayer); }
function overlay(fn) {
  overLayer = stage.layer('dev-over');
  over = fn(overLayer, () => { if (over) { over.destroy(); over = null; overLayer.remove(); overLayer = null; } });
}
const common = { stage, audio, settings };

const SCREENS = {
  title: () => screen((host) => mountTitle(host, { ...common, hasSave: true, onNew: () => show('hall'), onContinue: () => show('hall'), onSettings: () => overlay((o, close) => mountSettings(o, { ...common, onChange: (s) => Object.assign(settings, s), onClose: close })) })),
  'title-first': () => screen((host) => mountTitle(host, { ...common, hasSave: false, onNew: () => show('hall-first'), onContinue: () => {}, onSettings: () => {} })),
  hall: () => screen((host) => mountHall(host, { ...common, lineage, onDepart: (ls) => { log('depart', ls.Life.Name, ls.ToolSlots, ls.DeckMax); show('departure'); }, onSettings: () => overlay((o, close) => mountSettings(o, { ...common, onSaveQuit: () => log('save & quit'), onChange: (s) => Object.assign(settings, s), onClose: close })) })),
  'hall-many': () => screen((host) => mountHall(host, { ...common, lineage: crowded, onDepart: () => show('departure'), onSettings: () => {} })),
  'ending-miasma': () => { const L = crowdedLineage(); screen((host) => mountEnding(host, { ...common, ending: endingView(L), lineage: L, onHall: () => show('hall') })); },
  'hall-first': () => screen((host) => mountHall(host, { ...common, lineage: firstLineage, onDepart: () => show('departure'), onSettings: () => {} })),
  departure: () => screen((host) => mountDeparture(host, { ...common, lifeStart, owned, onSetOut: (p) => { log('set out', p.deck.length, p.battleToolIds, p.consumableIds, p.talent, p.loadout.ToolSlotCount); show('ending-death'); }, onBack: () => show('hall') })),
  'ending-death': () => screen((host) => mountEnding(host, { ...common, ending: endingView(afterDeath), lineage: afterDeath, onHall: () => show('hall') })),
  'ending-cleared': () => {
    let L = newLineage(5);
    const s1 = startLife(L);
    L = recordSurvival(s1.Lineage, null, { Layer: 7, NodeId: 30, NodeKind: 'Boss', Ending: 'Completed' }, [{ CardId: 'thrust', Rank: 3 }], { Disclosure: {}, Pages: 0, Memo: '' }, { cardName, enemyName });
    screen((host) => mountEnding(host, { ...common, ending: endingView(L), lineage: L, onHall: () => show('hall') }));
  },
  'ending-alive': () => screen((host) => mountEnding(host, { ...common, ending: endingView(lineage), lineage, onHall: () => show('hall') })),
  settings: () => { SCREENS.hall(); overlay((o, close) => mountSettings(o, { ...common, onSaveQuit: () => log('save & quit'), onChange: (s) => { Object.assign(settings, s); log('settings', JSON.stringify(s)); }, onClose: close })); },
  owned: () => { SCREENS.hall(); overlay((o, close) => mountOwnedCards(o, { ...common, owned, book, deck: deckIds, onClose: close })); },
  'journal-explore': () => { SCREENS.hall(); overlay((o, close) => mountJournal(o, { ...common, mode: 'explore', book: new JournalBook(journalPages(), JournalContext.Exploration), onClose: close })); },
  'journal-enemies': () => { SCREENS.hall(); overlay((o, close) => mountJournal(o, { ...common, mode: 'battle', enemies: [{ Id: 'armored_warden', Name: '鉄壁の門竜' }], disclosureOf: (id) => (id === 'armored_warden' ? 1 : 0), onClose: close })); },
  // the contexts src/app/main.js passes today (overlays get only { expedition, mode, memo, ... })
  'departure-app': () => screen((host) => mountDeparture(host, { ...common, lifeStart: startLife(firstLineage), owned: [], onSetOut: (p) => log('set out', p.deck.length), onBack: () => show('hall-first'),
    onSettings: () => overlay((o, close) => mountSettings(o, { ...common, canSaveQuit: false, onSaveQuit: () => log('save & quit'), onClose: close })),
    onJournal: () => overlay((o, close) => mountJournal(o, { ...common, mode: 'explore', expedition: expedition(), memo, scribbles: [], onMemo: (t) => { memo = t; }, onClose: close })) })),
  'hall-app': () => screen((host) => mountHall(host, { ...common, lineage, onDepart: () => show('departure-app'),
    onSettings: () => overlay((o, close) => mountSettings(o, { ...common, canSaveQuit: false, onSaveQuit: () => log('save & quit'), onClose: close })),
    onJournal: () => overlay((o, close) => mountJournal(o, { ...common, mode: 'explore', expedition: expedition(), memo, scribbles, onMemo: (t) => { memo = t; }, onClose: close })) })),
  'owned-app': () => { SCREENS.hall(); overlay((o, close) => mountOwnedCards(o, { ...common, expedition: expedition(), onClose: close })); },
  'journal-app': () => { SCREENS.hall(); overlay((o, close) => mountJournal(o, { ...common, mode: 'explore', expedition: expedition(), memo, scribbles, onMemo: (t) => { memo = t; log('memo', JSON.stringify(t)); }, pageId: 'memo', onClose: close })); },
  'journal-app-battle': () => { SCREENS.hall(); overlay((o, close) => mountJournal(o, { ...common, mode: 'battle', expedition: expedition(), memo, scribbles, enemyIds: ['armored_warden', 'polearm_warped'], disclosureOf: (id) => expedition().DisclosureOf(id), onClose: close })); },
  'settings-nosave': () => { SCREENS.title(); overlay((o, close) => mountSettings(o, { ...common, canSaveQuit: false, onSaveQuit: () => log('save & quit'), onClose: close })); },
  'journal-battle': () => { SCREENS.hall(); overlay((o, close) => mountJournal(o, { ...common, mode: 'battle', pages: journalPages(), pageId: 'enemy-miasma_priest', onClose: close })); },
};

function show(name) {
  clear();
  (SCREENS[name] || SCREENS.title)();
  bar.querySelectorAll('button').forEach((b) => b.classList.toggle('on', b.dataset.name === name));
  try { history.replaceState(null, '', '#' + name); } catch (e) { /* file:// */ }
}

// the switcher (dev chrome, outside the stage)
const bar = h('div', { id: 'dev-bar', style: 'position:fixed;left:8px;bottom:8px;z-index:9999;display:flex;flex-wrap:wrap;gap:4px;max-width:900px;font:12px/1 sans-serif' });
for (const name of Object.keys(SCREENS)) {
  bar.appendChild(h('button', { 'data-name': name, style: 'padding:4px 8px;background:#222a;color:#ddd;border:1px solid #555;border-radius:4px;cursor:pointer', onclick: () => show(name) }, name));
}
document.body.appendChild(bar);
const style = document.createElement('style');
style.textContent = '#dev-bar button.on{border-color:#e8a84c!important;color:#fff!important}';
document.head.appendChild(style);

window.__meta = { show, hideBar: () => { bar.style.display = 'none'; }, lineage, lifeStart, settings };
show((location.hash || '#title').slice(1));
