// Section 6a: the first battle of layer 1, played by hand (no autobattle) for the interactive states.
// From ckpt-departed (the life just set out, the start node's fight waiting). Ends with ckpt-battle1 (back on the map).
import {
  launch, sleep, shot, startCast, saveFrames, pageNow, marks, clickSel, hoverSel, centerOf,
  setIndexFile, dumpStorage, saveCheckpoint, loadCheckpoint, writeJSON,
} from './lib.mjs';
import { battleKit } from './battlelib.mjs';

setIndexFile('battle1');
const { browser, page, errors } = await launch({ storage: loadCheckpoint('departed') });
const B = battleKit(page);
const info = { log: [], missing: [] };
const log = (x) => { info.log.push(x); console.log(' ', x); };
const tryCap = async (name, fn) => { try { await fn(); } catch (e) { info.missing.push(name + ': ' + e.message.split('\n')[0]); console.log('  MISSING', name, e.message.split('\n')[0]); } };
const SC = 'battle';

await page.waitForSelector('.ocbg-meta-title');
await sleep(2700);
await shot(page, '109-title-with-save', 'title', 'title with a life in progress (つづきから enabled and focused)', 'reload with the save of a life just set out');
await clickSel(page, '.ocbg-meta-title-item', 1);
await page.waitForFunction(() => window.__scene && window.__scene.player, null, { timeout: 30000 });
log('resumed: ' + (await B.waitIdle()));
await page.mouse.move(960, 200);
await sleep(600);

// ---------------------------------------------------------------- turn 1: looking without playing
await shot(page, '603-battle-idle-hand', SC, 'turn 1, idle: hand fanned, guide line, enemy omen (移動)', 'つづきから into the waiting fight; no input');
let fs = await B.faces();
info.hand1 = fs;
const outOfReach = fs.find((f) => f.aim === 'Single' && f.reach);
const selfGuard = fs.find((f) => f.aim !== 'Single' && f.kind === 'Guard' && f.playable);
const selfStance = fs.find((f) => f.kind === 'Stance' && f.playable);
await tryCap('hover card', async () => {
  const c = selfGuard || fs[0];
  await B.hoverCard(c.id);
  await sleep(250);
  await shot(page, '604-battle-hover-card', SC, `pointer over 「${c.name}」: lifted, hover line (trait) above it`, 'mouse.move onto the card');
});
await tryCap('hover out of reach', async () => {
  if (!outOfReach) throw new Error('no out-of-reach card in the hand');
  await B.hoverCard(outOfReach.id);
  await sleep(250);
  await shot(page, '605-battle-hover-out-of-reach', SC, `pointer over 「${outOfReach.name}」 (range ${outOfReach.range}, gap 3): reason panel 「${outOfReach.reach}」`, 'mouse.move onto an attack that does not reach');
});
await tryCap('card detail', async () => {
  const c = outOfReach || fs[0];
  const p = await B.cardPos(c.id);
  await page.mouse.click(p.x, Math.min(p.y + 10, 1000), { button: 'right' });
  await sleep(500);
  await shot(page, '606-battle-card-detail', SC, `card detail of 「${c.name}」 (right click)`, 'right click on the card');
  await page.keyboard.press('Escape');
  await sleep(300);
});
await tryCap('held out of reach over enemy', async () => {
  if (!outOfReach) throw new Error('no out-of-reach card');
  await B.holdCard(outOfReach.id);
  const r = await B.receiver(0);
  await B.carry(r.x, r.y);
  await sleep(250);
  await shot(page, '607-battle-held-out-of-reach-over-enemy', SC, `held 「${outOfReach.name}」 over the enemy while out of reach`, 'mouse down on the card, carried onto the enemy receiver');
  const t0 = Date.now();
  const cast = await startCast(page);
  await sleep(150);
  const tUp = Date.now();
  await page.mouse.up();
  await sleep(1400);
  const got = await cast.stop();
  saveFrames(got, tUp, '608-battle-refusal', [0, 80, 160, 300, 600, 1000], { screen: SC, state: 'refused release: refusal line, card slides home', how: 'mouse up over the enemy with the out-of-reach card' });
  await sleep(200);
  await shot(page, '609-battle-refusal-line', SC, 'refusal line standing (3.5 s)', 'right after the refused release');
});
await page.mouse.move(960, 200, { steps: 3 });
await sleep(400);
// pile drawer
await tryCap('pile drawer', async () => {
  const p = await B.ev(() => window.__scene.env.geom.pileMiddle('Draw'));
  await page.mouse.move(p.x, p.y, { steps: 4 });
  await sleep(300);
  await shot(page, '610-battle-hover-draw-pile', SC, 'pointer over the draw pile', 'mouse.move onto the draw pile');
  await page.mouse.click(p.x, p.y);
  await sleep(700);
  await shot(page, '611-battle-pile-drawer-draw', SC, 'pile drawer open: draw pile list (figures shift right)', 'click on the draw pile');
  await page.keyboard.press('Escape');
  await sleep(600);
});
// speed toggle
await tryCap('speed toggle', async () => {
  for (let k = 0; k < 3; k++) {
    const c = await centerOf(page, '.ocbg-battle-speed');
    await page.mouse.move(c[0], c[1], { steps: 3 });
    await page.mouse.click(c[0], c[1]);
    await sleep(350);
    const sp = await B.ev(() => window.__scene.env.settings.speed);
    await shot(page, `612-battle-speed-${k + 1}`, SC, `speed switch clicked → ×${sp} (pointer on it)`, 'click the speed switch');
  }
  await page.mouse.move(960, 200, { steps: 3 });
});
// journal in battle
await tryCap('journal', async () => {
  await page.keyboard.press('j');
  await sleep(900);
  await shot(page, '613-battle-journal', SC, 'journal drawer over the battle (right 30 %, figures shift left)', 'J');
  await page.keyboard.press('j');
  await sleep(600);
});
// settings in battle (Esc is taken on its release)
await tryCap('settings', async () => {
  await page.keyboard.press('Escape');
  await sleep(900);
  await shot(page, '614-battle-settings', 'settings', 'settings over the battle (保存して閉じる)', 'Esc (released) in the fight with nothing open');
  await page.keyboard.press('Escape');
  await sleep(700);
});
// stance slot (empty) hover
await tryCap('stance slot empty', async () => {
  const b = await B.ev(() => window.__scene.env.geom.stanceSlot('Player'));
  await page.mouse.move(b.x + b.w / 2, b.y + b.h / 2, { steps: 4 });
  await sleep(400);
  await shot(page, '615-battle-stance-slot-empty-hover', SC, 'pointer over the empty stance slot (player)', 'mouse.move onto the stance slot');
});

// ---------------------------------------------------------------- turn 1: playing self cards
await tryCap('held self card over throw line', async () => {
  const c = selfStance || selfGuard;
  if (!c) throw new Error('no self card');
  await B.holdCard(c.id);
  await B.carry(960, 720);
  await sleep(200);
  await shot(page, '616-battle-held-self-below-line', SC, `held 「${c.name}」 (self) still below the throw line`, 'mouse down on the card, carried to y 720');
  await B.carry(960, 470);
  await sleep(250);
  await shot(page, '617-battle-held-self-over-line', SC, `held 「${c.name}」 (self) above the throw line (zone lit)`, 'carried on to y 470');
  const cast = await startCast(page);
  await sleep(150);
  const tUp = Date.now();
  await page.mouse.up();
  await sleep(2200);
  const got = await cast.stop();
  saveFrames(got, tUp, `618-battle-release-${c.kind.toLowerCase()}`, [0, 80, 160, 300, 500, 800, 1200, 1800], { screen: SC, state: `release of 「${c.name}」 above the throw line (card flies, effect lands)`, how: 'mouse up above the throw line' });
  log('after self play: ' + (await B.waitIdle()));
});
await page.mouse.move(960, 200, { steps: 3 });
await sleep(500);
await shot(page, '619-battle-after-stance', SC, 'after the stance card: stance list shows it', 'idle after the release');
await tryCap('stance tooltip', async () => {
  const b = await B.ev(() => window.__scene.env.geom.stanceBox('Player'));
  if (!b) throw new Error('no stance box');
  await page.mouse.move(b.x + 40, b.y + 26, { steps: 4 });
  await sleep(500);
  await shot(page, '620-battle-stance-tooltip', SC, 'pointer over the player\'s stance: tooltip', 'mouse.move onto the stance frame');
  info.stanceBox = b;
});
await tryCap('status chip (player)', async () => {
  const n = await B.ev(() => document.querySelectorAll('.ocbg-battle-chip').length);
  if (!n) throw new Error('no chip yet');
  await hoverSel(page, '.ocbg-battle-chip', 0);
  await sleep(500);
  await shot(page, '621-battle-status-chip-tooltip', SC, 'pointer over a status chip: tooltip', 'mouse.move onto the first chip');
});
await page.mouse.move(960, 200, { steps: 3 });
await sleep(300);
// play the guard card too (by keyboard: digit + Enter)
await tryCap('keyboard select', async () => {
  fs = await B.faces();
  const g = fs.find((f) => f.aim !== 'Single' && f.playable);
  if (!g) throw new Error('no playable self card left');
  const i = fs.findIndex((f) => f.id === g.id);
  await page.keyboard.press(String(i + 1));
  await sleep(400);
  await shot(page, '622-battle-key-selected', SC, `card ${i + 1} 「${g.name}」 selected with its digit key`, `key ${i + 1}`);
  await page.keyboard.press('Enter');
  log('after key play: ' + (await B.waitIdle()));
  await sleep(300);
  if (await B.ev(() => window.__scene.modules.hand.hasSelection())) await page.keyboard.press('Escape');
});
await page.mouse.move(960, 200, { steps: 3 });
await sleep(400);
await shot(page, '623-battle-turn1-before-end', SC, 'turn 1 before ending the turn (Guard up, stance set)', 'idle');

// ---------------------------------------------------------------- the enemy's turn
const endTurnCapture = async (base, label) => {
  await B.noOverlay();
  await sleep(300);
  const before = await pageNow(page);
  const cast = await startCast(page);
  await sleep(200);
  const tX = Date.now();
  await page.keyboard.press('x');
  if (!(await B.leftIdle(3000))) log('X did not start the enemy turn');
  const s = await B.waitIdle({ timeout: 60000 });
  await sleep(1300);
  const got = await cast.stop();
  saveFrames(got, tX, base + '-from-x', [0, 300, 600, 1000, 1500, 2000, 2500, 3000, 3500, 4000, 5000, 6000], { screen: SC, state: `${label}: end turn → enemy turn → next turn`, how: 'X; time from the key press' });
  const bn = await marks(page, 'ocbg-battle-banner', before);
  info[base + '-banners'] = bn;
  const enemy = bn.find((b) => /敵/.test(b.text));
  const mine = bn.find((b) => !/敵/.test(b.text));
  if (enemy) saveFrames(got, enemy.t, base + '-enemy-banner', [0, 150, 300, 600, 1000, 1400, 2000], { screen: SC, state: `enemy turn shade + banner 「${enemy.text}」, then the enemy's action`, how: 'time from the 敵の番 banner attaching' });
  if (mine) saveFrames(got, mine.t, base + '-turn-banner', [0, 150, 300, 600, 1000], { screen: SC, state: `turn start banner 「${mine.text}」 (next turn)`, how: 'time from the あなたの番 banner attaching' });
  return s;
};
await tryCap('enemy turn 1', async () => { log('after turn 1: ' + (await endTurnCapture('624-battle-turn1-end', 'turn 1'))); });
await page.mouse.move(960, 200, { steps: 3 });
await sleep(500);
await shot(page, '625-battle-turn2-idle', SC, 'turn 2 idle (new omen, the enemy has moved)', 'after the enemy turn');
info.frame2 = await B.frame();

// ---------------------------------------------------------------- attacks: find one that reaches
let attacked = false;
for (let guard = 0; guard < 8 && !attacked; guard++) {
  const s = await B.state();
  if (s === 'over') break;
  fs = await B.faces();
  const atk = fs.find((f) => f.playable && f.aim === 'Single');
  if (!atk) {
    // a self card if any, then end the turn
    const self = fs.find((f) => f.playable && f.aim !== 'Single');
    if (self) { await B.dragPlay(self.id, self.aim); await B.waitIdle(); continue; }
    log('turn end (no attack in reach)');
    await B.endTurn();
    await B.waitIdle({ fast: true });
    continue;
  }
  await tryCap('attack', async () => {
    await B.hoverCard(atk.id);
    await sleep(200);
    await shot(page, '626-battle-hover-attack-in-reach', SC, `pointer over 「${atk.name}」 in reach (hover line, reach cells lit on the floor)`, 'mouse.move onto the card');
    await B.holdCard(atk.id);
    const r = await B.receiver(0);
    await B.carry(r.x - 60, r.y + 40, 10);
    await B.carry(r.x, r.y, 6);
    await sleep(300);
    await shot(page, '627-battle-held-over-enemy', SC, `held 「${atk.name}」 over the enemy: target hooks, prediction tag, reach light on the floor`, 'mouse down on the card, carried onto the enemy receiver');
    await B.carry(r.x - 110, r.y + 150, 6);
    await sleep(300);
    await shot(page, '627b-battle-held-over-enemy-low', SC, `held 「${atk.name}」 low on the enemy receiver (figure, hooks and floor visible)`, 'carried to the lower left part of the receiver box');
    await B.carry(r.x, r.y, 6);
    await sleep(200);
    const before = await pageNow(page);
    const cast = await startCast(page);
    await sleep(150);
    const tUp = Date.now();
    await page.mouse.up();
    await sleep(2600);
    const got = await cast.stop();
    saveFrames(got, tUp, '628-battle-release-attack', [0, 80, 160, 300, 500, 700, 1000, 1500, 2000], { screen: SC, state: `release of 「${atk.name}」 on the enemy (card flies, strike, hit)`, how: 'mouse up over the enemy receiver' });
    const fl = await marks(page, 'ocbg-battle-float', before);
    info.floats = fl;
    fl.slice(0, 3).forEach((f, k) => saveFrames(got, f.t, '629' + ['', 'b', 'c'][k] + '-battle-hit-number', [0, 100, 200, 400, 700, 1000], { screen: SC, state: `hit: floating number ${k + 1} (「${f.text}」)`, how: 'time from that floating number attaching' }));
    attacked = true;
    log('after attack: ' + (await B.waitIdle()));
  });
  if (!attacked) break;
}
await page.mouse.move(960, 200, { steps: 3 });
await sleep(500);
await tryCap('status chip (enemy)', async () => {
  const n = await B.ev(() => document.querySelectorAll('.ocbg-battle-chip').length);
  info.chips = await B.ev(() => [...document.querySelectorAll('.ocbg-battle-chip')].map((c) => c.textContent));
  if (!n) throw new Error('no chip');
  await hoverSel(page, '.ocbg-battle-chip', n - 1);
  await sleep(500);
  await shot(page, '630-battle-status-chip-tooltip', SC, `pointer over a status chip (${info.chips[n - 1]}): tooltip`, 'mouse.move onto the last chip');
  await page.mouse.move(960, 200, { steps: 3 });
});
await tryCap('omen hover', async () => {
  const o = await B.ev(() => window.__scene.env.geom.omenBoxes()[0]);
  if (!o) throw new Error('no omen');
  await page.mouse.move(o.x + o.w / 2, o.y + o.h / 2, { steps: 4 });
  await sleep(500);
  await shot(page, '631-battle-omen-hover', SC, 'pointer over the enemy omen badge (its aimed cells kept lit)', 'mouse.move onto the omen badge');
  await page.mouse.move(960, 200, { steps: 3 });
});

// ---------------------------------------------------------------- play on to the win (attacks first); second enemy turn captured too
let enemyTurns = 1;
let result = null;
for (let guard = 0; guard < 200; guard++) {
  const s = await B.state();
  if (s === 'over' || s === null) break;
  if (s !== 'idle') { await B.waitIdle({ fast: true }); continue; }
  fs = await B.faces();
  const atk = fs.filter((f) => f.playable && f.aim === 'Single').sort((a, b) => b.cost - a.cost)[0];
  const self = fs.find((f) => f.playable && f.aim !== 'Single');
  const pick = atk || self;
  if (pick) {
    const before = await pageNow(page);
    await B.dragPlay(pick.id, pick.aim);
    const st = await B.waitIdle({ fast: false });
    if (st === 'over') {
      // the winning blow: the result card
      const cast = await startCast(page);
      await page.waitForFunction(() => document.querySelector('.ocbg-battle-result-paper'), null, { timeout: 15000 }).catch(() => {});
      await sleep(3200);
      const got = await cast.stop();
      const r = await marks(page, 'ocbg-battle-result', before);
      if (r[0]) saveFrames(got, r[0].t, '633-battle-result-won', [0, 150, 300, 600, 1000, 1500, 2500], { screen: SC, state: 'win: result card (勝ち) coming in', how: 'the winning card released; time from the result element attaching' });
      result = 'won';
      break;
    }
    continue;
  }
  if (enemyTurns === 1) {
    enemyTurns++;
    await tryCap('enemy turn 2', async () => { await endTurnCapture('632-battle-turn2-end', 'turn 2'); });
    continue;
  }
  await B.endTurn();
  await B.waitIdle({ fast: true });
}
info.result = result;
log('battle result: ' + result);
// back on the map: the result / reward card
await page.waitForFunction(() => document.querySelector('.ocbg-explore'), null, { timeout: 30000 }).catch(() => {});
await sleep(2500);
info.plate = await B.ev(() => { const p = document.querySelector('.ocbg-explore-plate'); return p ? p.className : null; });
await shot(page, '701-explore-battle-result-reward', 'exploration', 'battle result / reward card after the first fight (勝利, learned, journal, finds)', 'the fight won; the map comes back with the result card');
saveCheckpoint('battle1', await dumpStorage(page));
info.errors = errors;
writeJSON('info-battle1.json', info);
console.log(JSON.stringify(info, null, 1).slice(0, 4000));
await browser.close();
