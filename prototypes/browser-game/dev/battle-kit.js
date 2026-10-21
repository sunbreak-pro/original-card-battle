// Dev helpers for the battle scene's module pages (dev/battle-hand.js, dev/battle-field.js, dev/battle-hud.js).
// A module page mounts its module alone on the scene's own env (createBattleEnv), with real frames and
// events from a CoreBattleSource built like Unity's BattleBootstrap (BattleLaunch).
//
//   import { devStage, makeSource, stepTo, mountAlone } from './battle-kit.js';
//   const stage = devStage();
//   const source = makeSource({ enemy: 'armored_warden', cells: 7, seed: 3 });
//   const events = stepTo(source, (s) => s.WaitingForPlayer);      // plays the automatic events, returns them
//   const { env, module } = mountAlone(stage, createHud, { chain: { index: 1, total: 2 } }, { layer: 4 });
//   module.applyFrame(source.Frame, { first: true });
import '../src/scenes/common/common.css';
import { createStage } from '../src/scenes/common/stage.js';
import { createBattleEnv } from '../src/scenes/battle/BattleScene.js';
import { BattleLaunch } from '../src/bridge/depiction/BattleLaunch.js';

/** The 1920×1080 stage in #app. */
export function devStage() {
  return createStage(document.getElementById('app'));
}

/**
 * A fight as BattleBootstrap builds it. opts: { enemy, more: [ids], seed, cells (5..8), disclosure (0..2),
 * chainIndex, chainTotal, autoPlay }.
 */
export function makeSource(opts = {}) {
  const launch = new BattleLaunch();
  if (opts.enemy) launch.EnemyId = opts.enemy;
  if (opts.more && opts.more.length) launch.MoreEnemyIds = opts.more.slice();
  if (opts.seed != null) launch.Seed = opts.seed;
  if (opts.cells != null) launch.FieldCells = opts.cells;
  if (opts.disclosure != null) launch.Disclosure = opts.disclosure;
  if (opts.chainIndex != null) launch.ChainIndex = opts.chainIndex;
  if (opts.chainTotal != null) launch.ChainTotal = opts.chainTotal;
  if (opts.autoPlay) launch.AutoPlay = true;
  return launch.CreateSource();
}

/**
 * Advances the source's automatic events until `until(source)` holds (or it waits for the player / is
 * finished), returning the events taken. Dev only: it moves the battle without the screen.
 */
export function stepTo(source, until) {
  const taken = [];
  for (let guard = 0; guard < 500; guard++) {
    if (until && until(source)) break;
    if (source.Finished) break;
    if (source.WaitingForPlayer) {
      if (!until) break;
      taken.push(source.EndTurn());
      continue;
    }
    taken.push(source.AdvanceAuto());
  }
  return taken;
}

/**
 * Mounts one module (createHand / createField / createHud) alone on a fresh scene env.
 * @returns {{ env, module, root, destroy() }}
 */
export function mountAlone(stage, createModule, moduleOpts = {}, ctx = {}) {
  const host = stage.layer('ocbg-battle-host');
  const { root, env, clocks } = createBattleEnv(host, Object.assign({ stage, settings: { speed: 1.25, shortFx: false }, layer: 1, nodeKind: 'Battle' }, ctx));
  // Geometry the module may ask of the others, as stand-ins (the real modules register their own).
  env.geom.figure = env.geom.figure || ((side) => {
    const x = side === 'Player' ? 740 : 1180;
    return { x, homeX: x, feetY: 570, height: 226, width: 100, top: 344, chest: { x, y: 570 - 226 * 0.65 }, head: { x, y: 570 - 226 * 0.84 } };
  });
  env.geom.receiver = env.geom.receiver || (() => ({ X: 1010, Y: 287, Width: 340, Height: 340, Right: 1350, Bottom: 627 }));
  env.geom.pileMiddle = env.geom.pileMiddle || ((kind) => ({ x: kind === 'Draw' ? 64 : kind === 'Discard' ? 150 : 236, y: 990 }));
  env.geom.omenBox = env.geom.omenBox || (() => ({ x: 1080, y: 232, w: 200, h: 98 }));
  const module = createModule(env, moduleOpts);
  return {
    env, module, root,
    destroy() { if (module.destroy) module.destroy(); clocks.destroy(); root.remove(); },
  };
}
