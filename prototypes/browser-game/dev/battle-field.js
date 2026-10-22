// Dev page for the battle scene's field module alone (docs/BATTLE_SCENE.md §8): the field mounted on the
// scene's own env (dev/battle-kit.js mountAlone) with real frames from a CoreBattleSource built like Unity's
// BattleBootstrap. The screenshot steps in dev/battle-field-steps/ drive its beats through window.__field
// (Playwright's page.clock freezes the frame loop mid-effect).
//
// Build: node dev/battle-build.mjs dev/battle-field.js
// URL:   dev/out/battle-field.html?enemy=<id>&layer=<1..7>&cells=<5..8>&seed=<n>&node=Battle|Elite|Boss&more=<id,...>
import { devStage, makeSource, stepTo, mountAlone } from './battle-kit.js';
import { createField } from '../src/scenes/battle/field.js';

const params = new URLSearchParams(location.search);
const num = (k, d) => (params.has(k) && params.get(k) !== '' ? Number(params.get(k)) : d);

const stage = devStage();
const opts = {
  enemy: params.get('enemy') || 'polearm_warped',
  cells: num('cells', 6),
  seed: num('seed', 20260921),
  more: params.get('more') ? params.get('more').split(',') : null,
};
const layerNo = num('layer', 1);
const nodeKind = params.get('node') || 'Battle';
const errors = [];
window.addEventListener('error', (e) => errors.push(String(e.message)));

let source = null;
try {
  source = makeSource(opts);
} catch (err) {
  console.error(err);
}
const mounted = mountAlone(stage, createField, { layer: layerNo, nodeKind }, { layer: layerNo, nodeKind });
const field = mounted.module;
const env = mounted.env;
if (source) field.applyFrame(source.Frame, { first: true });

/** Takes automatic events until the source waits for the player (or ends); applies their settled frames. */
function settle() {
  const evs = stepTo(source, (s) => s.WaitingForPlayer || s.Finished);
  if (source) field.applyFrame(source.Frame, {});
  return evs;
}

window.__field = { field, env, source, errors, settle, stepTo: (until) => stepTo(source, until), frame: () => source && source.Frame };
