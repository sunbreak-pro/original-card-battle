// Dev page for the battle scene's hud module (docs/BATTLE_SCENE.md §8): the hud mounted on the scene's own env
// with the field under it (no hand, no player), fed real frames from a CoreBattleSource built like Unity's
// BattleBootstrap (BattleLaunch). The screenshot steps in dev/battle-hud-steps/ drive it through window.__hud:
// they apply real frames, or real frames with a few members changed (chips, plan, statuses), and call the
// hud's beat methods (Playwright's page.clock freezes the frame loop mid-beat).
//
// Build: node dev/battle-build.mjs dev/battle-hud.js
// URL:   dev/out/battle-hud.html?enemy=<id>&layer=<1..7>&cells=<5..8>&seed=<n>&node=Battle|Elite|Boss&more=<id,...>
//        &disclosure=<0..2>&chain=<i>/<n>&miasma=<n>
import '../src/scenes/common/common.css';
import { createStage } from '../src/scenes/common/stage.js';
import { createBattleEnv } from '../src/scenes/battle/BattleScene.js';
import { createField } from '../src/scenes/battle/field.js';
import { createHud } from '../src/scenes/battle/hud.js';
import { L } from '../src/scenes/battle/layout.js';
import { makeSource, stepTo } from './battle-kit.js';
import { StatusChipText } from '../src/bridge/depiction/StatusChipText.js';
import { StatusKind } from '../src/core/battle/Types.js';
import * as T from '../src/bridge/depictionScript/DepictionTypes.js';

const params = new URLSearchParams(location.search);
const num = (k, d) => (params.has(k) && params.get(k) !== '' ? Number(params.get(k)) : d);
const errors = [];
window.addEventListener('error', (e) => errors.push(String(e.message)));

const stage = createStage(document.getElementById('app'));
const layerNo = num('layer', 1);
const nodeKind = params.get('node') || 'Battle';
const chainParam = (params.get('chain') || '1/1').split('/').map(Number);
const chain = { index: chainParam[0] || 1, total: chainParam[1] || 1 };
const settings = { speed: num('speed', 1.25), shortFx: false };
const host = stage.layer('ocbg-battle-host');
const ctx = { stage, settings, layer: layerNo, nodeKind, chain, miasmaPercent: params.has('miasma') ? num('miasma', 0) : undefined };
const { env } = createBattleEnv(host, ctx);
const field = createField(env, { layer: layerNo, nodeKind });
const calls = [];
const hud = createHud(env, {
  chain,
  onEndTurn: () => calls.push('endTurn'),
  onSpeed: () => calls.push('speed'),
  onJournal: () => calls.push('journal'),
  onSettings: () => calls.push('settings'),
});

let source = null;
try {
  source = makeSource({
    enemy: params.get('enemy') || 'polearm_warped',
    more: params.get('more') ? params.get('more').split(',') : null,
    cells: num('cells', 6),
    seed: num('seed', 20260921),
    disclosure: num('disclosure', 2),
    chainIndex: chain.index,
    chainTotal: chain.total,
  });
} catch (err) {
  console.error(err);
}

let shownFrame = null;
function apply(f, first) {
  shownFrame = f;
  field.applyFrame(f, { first: !!first });
  hud.applyFrame(f, { first: !!first });
  hud.reserveHint(f.StanceHint || '');
}
// the player's per-frame call (DepictionPlayer.FollowFigures)
env.clock.onFrame(() => {
  if (!shownFrame) return;
  const p = env.geom.figure('Player');
  const e = env.geom.figure('Enemy');
  if (!p || !e) return;
  hud.follow({ playerX: p.homeX, enemyX: e.homeX, enemyTop: L.FEET_Y - e.height, cells: shownFrame.Floor ? shownFrame.Floor.Cells : 0, enemySize: shownFrame.Floor ? Math.max(1, shownFrame.Floor.EnemySize) : 1 });
});

/**
 * A deep copy that keeps every class (getters like OmenFrame.PlanTag, PileFrame.CardsOf) and every shared
 * reference (Frame.Enemy is the object Frame.Enemies holds, Frame.Omen the one Frame.Omens holds).
 */
function clone(o, seen = new Map()) {
  if (o === null || typeof o !== 'object') return o;
  if (seen.has(o)) return seen.get(o);
  if (Array.isArray(o)) {
    const a = [];
    seen.set(o, a);
    o.forEach((x) => a.push(clone(x, seen)));
    return a;
  }
  const c = Object.create(Object.getPrototypeOf(o));
  seen.set(o, c);
  for (const k of Object.keys(o)) c[k] = clone(o[k], seen);
  return c;
}

if (source) {
  apply(source.Frame, true);
  field.snapHome('Enemy');
}
const speedOf = { 1: ['×1.0', 1], 1.25: ['×1.25', 2], 1.5: ['×1.5', 3] }[settings.speed] || ['×1.25', 2];
hud.speed(speedOf[0], speedOf[1]);

window.__hud = {
  hud, field, env, source, errors, calls, T, StatusChipText, StatusKind,
  frame: () => shownFrame,
  clone: (f) => clone(f),
  apply,
  /** Takes automatic events until the source waits (or ends), then shows its frame. */
  settle() { const evs = stepTo(source, (s) => s.WaitingForPlayer || s.Finished); apply(source.Frame); return evs; },
  /** Ends the turn and takes events until the player's turn again. */
  nextTurn() { if (source.WaitingForPlayer) source.EndTurn(); return this.settle(); },
  /** Events up to (but not past) the first that satisfies `pick(ev)`; returns it with the frame before it. */
  untilEvent(pick) {
    for (let guard = 0; guard < 400 && !source.Finished; guard++) {
      const before = source.Frame;
      const ev = source.WaitingForPlayer ? source.EndTurn() : source.AdvanceAuto();
      if (pick(ev)) return { ev, before };
    }
    return null;
  },
};
