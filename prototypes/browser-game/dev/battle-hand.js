// Dev page for the battle scene's hand module (src/scenes/battle/hand.js).
//
//   ?mode=scene (default): the whole scene on a fight built like BattleBootstrap (BattleLaunch), with a deck and
//     a start gap of your choice, so every state the hand owns can be reached by hand or by a steps script.
//     &enemy=<id>&layer=<1..7>&cells=<5..8>&gap=<n>&seed=<n>&deck=<id,id,…>&copies=<1..3>&speed=<1|1.25|1.5>&short=1&auto=1
//   ?mode=gallery: every card of CardCatalog as the hand draws it (CardFace from a fight's own writer when the card
//     is in hand, else bound through CoreBattleSource on a deck of that card) — checks the face, the text fit, the terms.
//     &page=<0..> (24 a page)
//
// Build: node dev/battle-build.mjs dev/battle-hand.js → dev/out/battle-hand.html
// window.__battle = { scene, source, finished, outcome, errors } (as dev/battle.js) for the steps in dev/battle-hand-steps/.
import '../src/scenes/common/common.css';
import { createStage, h } from '../src/scenes/common/stage.js';
import { createAudio } from '../src/scenes/audio/Audio.js';
import { mountBattleScene } from '../src/scenes/battle/BattleScene.js';
import { BattleLaunch } from '../src/bridge/depiction/BattleLaunch.js';
import { CardCatalog } from '../src/core/battle/CardCatalog.js';
import { Cards } from '../src/core/battle/Cards.js';
import { GreedyPlayer } from '../src/sim/GreedyPlayer.js';
import { buildCard } from '../src/scenes/battle/hand-card.js';
import { createHand } from '../src/scenes/battle/hand.js';
import { mountAlone } from './battle-kit.js';

const OCB = window.OCB;
const params = new URLSearchParams(location.search);
const num = (k, d) => (params.has(k) && params.get(k) !== '' && !Number.isNaN(Number(params.get(k))) ? Number(params.get(k)) : d);
const mode = params.get('mode') || 'scene';

const settings = { speed: num('speed', 1.25), shortFx: params.get('short') === '1', master: 0.8, effects: 0.9, ambience: 0.6, muted: params.get('mute') !== '0' };
const app = document.getElementById('app');
const stage = createStage(app);
const state = window.__battle = { scene: null, source: null, finished: false, outcome: null, errors: [], settings };
window.addEventListener('error', (e) => state.errors.push(String(e.message)));

/** A deck of the given card ids (copies each), topped up to the §8 floor with the prototype deck's cards. */
function deckOf(ids, copies) {
  const defs = ids.map((id) => CardCatalog.ById(id));
  let deck = Cards.BuildDeck(defs, copies);
  if (deck.length < 20) {
    const fill = ['thrust', 'kesa_cut', 'reach_thrust', 'brace', 'feint', 'boar_rush', 'body_check', 'step_in_guard', 'step_out_guard', 'shield_bash']
      .filter((id) => !ids.includes(id)).map((id) => CardCatalog.ById(id));
    deck = deck.concat(Cards.BuildDeck(fill, 2)).slice(0, 20);
  }
  return deck;
}

function makeSource(over = {}) {
  const launch = new BattleLaunch();
  launch.EnemyId = over.enemy || params.get('enemy') || 'polearm_warped';
  launch.Seed = over.seed != null ? over.seed : num('seed', 20260921);
  launch.FieldCells = num('cells', 6);
  if (params.has('gap')) launch.StartGap = num('gap', 3);
  launch.Disclosure = num('disclosure', 2);
  const ids = (over.deck || params.get('deck') || '').split(',').map((s) => s.trim()).filter(Boolean);
  if (ids.length) launch.Deck = deckOf(ids, Math.max(1, Math.min(3, over.copies || num('copies', 2))));
  return launch.CreateSource();
}

function stubJournal(host, opts) {
  const panel = h('div', { style: 'position:absolute;right:0;top:0;width:576px;height:1080px;background:var(--washi);color:var(--washi-ink);padding:32px;box-sizing:border-box;font:400 22px/1.6 var(--font-body)' },
    h('h3', { style: 'margin:0 0 16px;font:700 30px/1.4 var(--font-display)' }, '手記（dev の仮）'));
  host.appendChild(panel);
  return { destroy() { panel.remove(); } };
}

function mountScene() {
  const audio = createAudio(OCB, settings);
  if (audio.bindUnlock) audio.bindUnlock(document);
  if (settings.muted && audio.setMuted) audio.setMuted(true);
  const host = stage.layer('ocbg-battle-host');
  const source = makeSource();
  state.source = source;
  const ctx = {
    stage, audio, settings, source,
    layer: num('layer', 1), nodeKind: 'Battle', chain: { index: 1, total: 1 },
    disclosureOf: () => 2,
    openJournal: stubJournal,
    onSettings() { state.settingsOpened = (state.settingsOpened || 0) + 1; },
    onFinished() { state.finished = true; state.outcome = source.Frame.Outcome; },
    onError(err) { state.errors.push(String(err && err.message)); },
  };
  if (params.get('auto') === '1') {
    const greedy = new GreedyPlayer();
    ctx.auto = {
      choose(src, played) {
        if (src.Frame.Corner.Turn > 80) return '';
        const choice = greedy.Choose(src.State, played);
        return choice ? choice.InstanceId : '';
      },
    };
  }
  state.scene = mountBattleScene(host, ctx);
}

/** Every catalog card as the hand draws it: each face comes from the writer, through a fight whose deck holds that card. */
function mountGallery() {
  const page = num('page', 0);
  const per = 24;
  const defs = CardCatalog.All.slice(page * per, page * per + per);
  const root = stage.layer('ocbg-battle ocbg-battle-gallery');
  root.style.background = 'var(--ground)';
  const faces = [];
  for (const def of defs) {
    let face = null;
    try {
      // A deck of this card × 3 and fillers; the first hand that holds it gives its face.
      for (let seed = 1; seed < 30 && !face; seed++) {
        const s = makeSource({ deck: def.Id, copies: 3, seed, enemy: 'polearm_warped' });
        for (let g = 0; g < 40 && !s.Finished && !face; g++) {
          if (s.WaitingForPlayer) {
            face = s.Frame.Hand.find((f) => f.Id.startsWith(def.Id + '-')) || null;
            if (!face) s.EndTurn();
          } else s.AdvanceAuto();
        }
      }
    } catch (err) { console.warn(def.Id, err.message); }
    faces.push({ def, face });
  }
  faces.forEach((f, i) => {
    const col = i % 8;
    const row = Math.floor(i / 8);
    const cell = h('div', { style: `position:absolute;left:${16 + col * 238}px;top:${12 + row * 356}px;width:216px;height:340px` });
    if (f.face) {
      const card = buildCard(OCB, f.face);
      cell.appendChild(card.el);
    }
    cell.appendChild(h('div', { style: 'position:absolute;left:0;top:310px;font:400 16px/22px var(--font-body);color:var(--ink-muted)' }, f.def.Id + (f.face ? '' : ' (no face)')));
    root.appendChild(cell);
  });
  state.gallery = faces.map((f) => ({ id: f.def.Id, ok: !!f.face }));
}

/**
 * The hand alone on the scene's env (dev/battle-kit.js mountAlone, stand-in figures), with n real faces (3..8) taken
 * from a fight's own frames: the fan at every size, hover, hold, the draw with a pile rebuild, the discard.
 * window.__hand = { hand, env, faces, draw(rebuildAfter), discard(), rebuild() }.
 */
function mountHandAlone() {
  const n = Math.max(1, Math.min(8, num('n', 8)));
  const faces = [];
  const seen = new Set();
  const deck = params.get('deck') || 'water_stance,first_aid,observe,stone_throw,boar_rush,kesa_cut,brace,focus,leg_sweep,feint';
  for (let seed = 1; seed < 40 && faces.length < n; seed++) {
    const s = makeSource({ deck, copies: 2, seed });
    for (let g = 0; g < 60 && !s.Finished && faces.length < n; g++) {
      if (s.WaitingForPlayer) {
        for (const f of s.Frame.Hand) {
          if (faces.length < n && !seen.has(f.Name)) { seen.add(f.Name); faces.push(f); }
        }
        s.EndTurn();
      } else s.AdvanceAuto();
    }
  }
  const kit = mountAlone(stage, createHand, {
    onRelease(id, zone) { state.released = { id, zone }; kit.module.returnToHand(id, zone !== 'None'); kit.module.showRefusal('dev: 「' + id + '」は戻しました'); },
    onHover(face) { state.hovered = face ? face.Name : null; },
    onHoldStart(face) { state.holding = face.Name; },
    onHoldEnd() { state.holding = null; },
    preview: () => '12',
  }, { layer: num('layer', 2), settings });
  const hand = kit.module;
  hand.build(faces);
  hand.setInteractable(true);
  hand.refreshPlayable((id) => faces.findIndex((f) => f.Id === id) % 3 === 2, '出したい札を離してください。手が済んだら「ターン終了」');
  window.__hand = {
    hand, env: kit.env, faces,
    draw(rebuildAfter = -1) { hand.build(faces); return hand.flyDrawn(0, rebuildAfter, { from: kit.env.geom.pileMiddle('Draw'), onRebuild() { state.rebuilt = (state.rebuilt || 0) + 1; } }); },
    discard() { return hand.discardAll(kit.env.geom.pileMiddle('Discard')); },
  };
  state.alone = true;
}

const start = () => (mode === 'gallery' ? mountGallery() : mode === 'alone' ? mountHandAlone() : mountScene());
const fontsReady = document.fonts && document.fonts.ready ? document.fonts.ready : Promise.resolve();
Promise.race([fontsReady, new Promise((r) => setTimeout(r, 1500))]).then(start);
