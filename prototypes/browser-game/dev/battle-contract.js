// Contract check for the battle scene's modules (docs/BATTLE_SCENE.md §6): mounts hand / field / hud alone and
// checks that every method the player calls exists. A module agent runs it after replacing a module:
//   node dev/battle-build.mjs dev/battle-contract.js && node tools/shot.mjs dev/out/battle-contract.html dev/shots/contract
// It prints "contract ok" or the missing members as a console error (shot.mjs lists console errors).
import { devStage, mountAlone } from './battle-kit.js';
import { createHand } from '../src/scenes/battle/hand.js';
import { createField } from '../src/scenes/battle/field.js';
import { createHud } from '../src/scenes/battle/hud.js';

const CONTRACT = {
  hand: ['sameHand', 'build', 'bind', 'layout', 'clear', 'count', 'faces', 'setInteractable', 'refreshPlayable', 'isHolding', 'heldFace',
    'hoveredFace', 'flyDrawn', 'discardAll', 'release', 'dropCard', 'settle', 'returnToHand', 'letGo', 'showRefusal', 'hideRefusal', 'select',
    'confirm', 'cancel', 'autoDrag', 'setBlocked', 'taggedUnits', 'setStatusMarks', 'destroy'],
  field: ['applyFrame', 'setExtras', 'showReach', 'hideReach', 'setEnemyTurnShade', 'setPose', 'setDown', 'enemyAway', 'setEnemyHomeCell',
    'snapHome', 'stanceGlowShown', 'setStanceGlow', 'fadeStanceGlow', 'lunge', 'returnHome', 'enemyMotion', 'recoil', 'flash', 'heave', 'fall',
    'shakeArena', 'moveOnFloor', 'snapOnFloor', 'recountGap', 'setGap', 'strike', 'traitAccent', 'mark', 'burst', 'floatText', 'shieldBlock',
    'ghost', 'shieldAt', 'setShift', 'appearEnemies', 'destroy'],
  hud: ['applyFrame', 'follow', 'status', 'reserveHint', 'setEnemyTurn', 'banner', 'resultCard', 'resultCardNow',
    'speed', 'setSpeedVisible', 'drawerOpen', 'closeDrawer', 'liftOmen', 'settleEnd', 'settleEndNow', 'retireOmens',
    'chainBanner', 'lossWordOut', 'closePinned', 'destroy'],
  'hud.status()': ['setGuard', 'popGuard', 'crackGuard', 'setHp', 'animateHp', 'animateTrail', 'snapTrail', 'setHpKeepingTrail', 'showHpLoss',
    'setStatus', 'playStatus', 'setStamina', 'lightPips', 'popPips'],
  'hud.omen': ['bind', 'popIn', 'blink', 'flare', 'setActing', 'fadeOut', 'hideNow', 'fadeOutAll', 'hideAllNow', 'strikeSide', 'strikeSideNow'],
  'hud.stamina': ['showCost', 'glowReserve'],
  'hud.endTurn': ['pop', 'flash', 'refuse'],
  geom: ['figure', 'receiver', 'cellBox', 'pileMiddle', 'omenBox', 'endTurnBox', 'handCard', 'coversPointer'],
};

const stage = devStage();
const missing = [];
const check = (name, obj, list) => list.forEach((m) => { if (!obj || typeof obj[m] !== 'function') missing.push(name + '.' + m); });
const hand = mountAlone(stage, createHand, { onRelease() {}, onHover() {}, onHoldStart() {}, onHoldEnd() {}, preview: () => '' });
const field = mountAlone(stage, createField, { layer: 1, nodeKind: 'Battle' });
const hud = mountAlone(stage, createHud, { chain: { index: 1, total: 1 }, onEndTurn() {}, onSpeed() {}, onJournal() {}, onSettings() {} });
check('hand', hand.module, CONTRACT.hand);
check('field', field.module, CONTRACT.field);
check('hud', hud.module, CONTRACT.hud);
check('hud.status()', hud.module.status && hud.module.status('Enemy'), CONTRACT['hud.status()']);
check('hud.omen', hud.module.omen, CONTRACT['hud.omen']);
check('hud.stamina', hud.module.stamina, CONTRACT['hud.stamina']);
check('hud.endTurn', hud.module.endTurn, CONTRACT['hud.endTurn']);
const geom = Object.assign({}, hand.env.geom, field.env.geom, hud.env.geom);
check('geom', geom, CONTRACT.geom);
window.__contract = { missing };
if (missing.length) console.error('contract: missing ' + missing.join(', '));
else console.log('contract ok');
