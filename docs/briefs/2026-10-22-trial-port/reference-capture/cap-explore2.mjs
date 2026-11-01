// Section 5b: layer 2 of the first life → 秘跡の彫り (the carving) → the boss fought by hand and lost on purpose
// (turns ended until the heir falls) → the loss result → the end-of-life card (力尽きた) → the ending screen →
// 継承の間 of the second heir (a past life, an untaken trace on layer 2). From ckpt-layer2; ends with ckpt-hall2.
import {
  launch, reopen, sleep, shot, startCast, saveFrames, pageNow, marks, clickSel, hoverSel, centerOf, mountedAt,
  setIndexFile, dumpStorage, saveCheckpoint, loadCheckpoint, writeJSON,
} from './lib.mjs';
import { exploreKit, closeResult } from './explorelib.mjs';
import { battleKit } from './battlelib.mjs';

setIndexFile('explore2');
const { browser, page, errors } = await launch({ storage: loadCheckpoint('layer2'), query: 'debug&autobattle' });
const K = exploreKit(page);
const B = battleKit(page);
const info = { log: [], missing: [], steps: [] };
const log = (x) => { info.log.push(x); console.log(' ', x); };
const tryCap = async (name, fn) => { try { await fn(); } catch (e) { info.missing.push(name + ': ' + e.message.split('\n')[0]); console.log('  MISSING', name, e.message.split('\n')[0]); } };
const SC = 'exploration';
const away = async () => { await page.mouse.move(1900, 1060, { steps: 2 }); await sleep(250); };
const continueLife = async () => {
  await page.waitForSelector('.ocbg-meta-title');
  await sleep(2700);
  await clickSel(page, '.ocbg-meta-title-item', 1);
  await page.waitForSelector('.ocbg-explore, .ocbg-battle', { timeout: 30000 });
  await sleep(2500);
};
await continueLife();
let st = await K.settle();
let I = await K.info();
info.edges = await K.ev(() => window.__ocb.live.raw.Session.Screen().Edges.map((e) => [e.From, e.To]));
info.events = await K.ev(() => { const s = window.__ocb.live.raw.Session; return s.Screen().Nodes.filter((n) => n.Kind === 'Event').map((n) => [n.Id, s.EventAt(n.Id)]); });
log('layer ' + I.layer + ' nodes ' + I.nodes.map((n) => n.kind + '#' + n.id + '@' + n.row + ',' + n.col).join(' '));
log('events ' + JSON.stringify(info.events) + ' edges ' + JSON.stringify(info.edges));

// the herb first (HP), from its slot: the consumable used on the map
await tryCap('use consumable', async () => {
  const slots = await K.ev(() => [...document.querySelectorAll('.ocbg-explore-slot')].map((s) => s.textContent));
  const i = slots.findIndex((t) => t.includes('薬草'));
  if (i < 0) throw new Error('no herb');
  await hoverSel(page, '.ocbg-explore-slot', i);
  await sleep(300);
  await shot(page, '750-explore-hover-consumable-herb', SC, 'pointer over 薬草 (HP 26 → 36 forecast)', 'mouse.move onto the slot');
  const cast = await startCast(page);
  await sleep(150);
  const t0 = Date.now();
  await clickSel(page, '.ocbg-explore-slot', i);
  await sleep(1600);
  const got = await cast.stop();
  saveFrames(got, t0, '751-explore-use-consumable', [0, 150, 300, 600, 1000, 1500], { screen: SC, state: 'consumable used on the map (薬草: HP rolls up, slot empties)', how: 'click the slot' });
  await away();
});

// ---------------------------------------------------------------- toward the carving
const path = [];
const prefer = ['Battle', 'Survey', 'Carving', 'Boss'];
for (let guard = 0; guard < 30; guard++) {
  st = await K.settle();
  if (st.scene !== 'explore') { log('scene ' + st.scene); break; }
  I = await K.info();
  if (st.confirm) { await shot(page, '752-explore-confirm-step', SC, 'confirmation before a step', 'reached on layer 2'); await page.keyboard.press('Enter'); await sleep(800); continue; }
  if (/is-surveychoice/.test(st.plate)) { await page.keyboard.press('1'); await sleep(2500); continue; }
  if (/is-swap/.test(st.plate)) { await shot(page, '753-explore-swap', SC, 'found consumable with full slots: swap plate', 'reached on layer 2'); await page.keyboard.press('Escape'); await sleep(800); continue; }
  if (/is-result/.test(st.plate)) { await sleep(800); await closeResult(page, true); continue; }
  if (/is-train/.test(st.plate)) { await page.keyboard.press('Escape'); await sleep(600); continue; }
  if (/is-eventchoice/.test(st.plate)) { await page.keyboard.press('1'); await sleep(1200); continue; }
  if (st.plate) { log('plate ' + st.plate); break; }
  const cur = I.nodes.find((n) => n.id === I.current);
  if (cur && cur.kind === 'Carving') break;
  const carving = I.nodes.find((n) => n.kind === 'Carving');
  const ok = await K.reachable(prefer);
  if (!ok.length) { log('no way on'); break; }
  // walk toward the carving's column
  let n = ok[0];
  if (carving) {
    const toward = ok.find((x) => x.id === carving.id) || ok.slice().sort((a, b) => Math.abs(a.col - carving.col) - Math.abs(b.col - carving.col))[0];
    n = toward;
  }
  path.push(n.kind + '#' + n.id);
  log('step to ' + n.kind + '#' + n.id);
  if (n.kind === 'Carving') {
    await K.pin(n.id);
    await away();
    await shot(page, '754-explore-preview-carving', SC, '秘跡の彫り pinned: preview (the hidden name ■■の秘跡)', 'click the carving node');
    const before = await pageNow(page);
    const cast = await startCast(page);
    await sleep(150);
    const t0 = Date.now();
    await K.go(0);
    await sleep(5200);
    const got = await cast.stop();
    saveFrames(got, t0, '755-explore-carving', [0, 400, 800, 1200, 1600, 2000, 2500, 3000, 4000, 5000], { screen: SC, state: 'stepping onto 秘跡の彫り: walk, then the layer banner with the real name, the band renamed', how: 'click 進む on the carving; time from the click' });
    const bn = await marks(page, 'ocbg-explore-banner.is-on', before);
    info.carvingBanners = bn;
    if (bn[0]) saveFrames(got, bn[0].t, '756-explore-carving-banner', [0, 150, 300, 600, 1000, 1500], { screen: SC, state: `carving banner 「${bn[0].text}」`, how: 'time from the banner switching on' });
    st = await K.settle();
    await away();
    await shot(page, '757-explore-carving-read', SC, 'after the carving: the layer\'s real name in the band and on the node', 'settled on the carving node');
    break;
  }
  await K.walkTo(n.id, 0);
  await sleep(600);
}
info.path = path;
saveCheckpoint('carving', await dumpStorage(page));

// ---------------------------------------------------------------- the boss, by hand, lost on purpose
await reopen(page, 'debug');
await continueLife();
st = await K.settle();
I = await K.info();
const boss = I.nodes.find((n) => n.kind === 'Boss');
log('boss reachable: ' + (boss && boss.standing) + ' hp ' + I.hp);
await K.pin(boss.id);
await away();
await K.go(0);
await page.waitForFunction(() => window.__scene && window.__scene.player && document.querySelector('.ocbg-battle:not(.ocbg-scene)'), null, { timeout: 30000 });
log('boss battle: ' + (await B.waitIdle()));
await page.mouse.move(960, 200);
await sleep(600);
await shot(page, '660-battle-boss-idle', 'battle', 'boss fight (layer 2), turn 1 idle', 'stepped onto the boss node without autobattle');
let chipShot = false;
const tryChips = async (tag) => {
  if (chipShot) return;
  const chips = await B.ev(() => [...document.querySelectorAll('.ocbg-battle-chip')].map((c) => c.textContent));
  if (!chips.length) return;
  info.chips = chips;
  await hoverSel(page, '.ocbg-battle-chip', 0);
  await sleep(500);
  await shot(page, '661-battle-status-chip-tooltip', 'battle', `pointer over a status chip (${chips[0]}): tooltip (${tag})`, 'mouse.move onto the first chip');
  // a right click pins it
  const c = await centerOf(page, '.ocbg-battle-chip', 0);
  await page.mouse.click(c[0], c[1], { button: 'right' });
  await sleep(400);
  await shot(page, '662-battle-status-chip-pinned', 'battle', 'status chip detail pinned (right click)', 'right click on the chip');
  await page.keyboard.press('Escape');
  await sleep(300);
  await page.mouse.move(960, 200);
  chipShot = true;
};
let turns = 0;
let lost = false;
for (let guard = 0; guard < 60; guard++) {
  const s = await B.waitIdle({ fast: turns >= 2 });
  if (s === 'over') { lost = true; break; }
  if (s !== 'idle') { log('state ' + s); break; }
  await tryChips('turn ' + (turns + 1));
  if (turns === 0) {
    // the boss's first blows on a heir with no Guard
    await B.noOverlay();
    const before = await pageNow(page);
    const cast = await startCast(page);
    await sleep(200);
    const t0 = Date.now();
    await page.keyboard.press('x');
    await B.leftIdle(3000);
    const s2 = await B.waitIdle({ timeout: 60000 });
    await sleep(800);
    const got = await cast.stop();
    saveFrames(got, t0, '663-battle-boss-enemy-turn', [0, 500, 1000, 1500, 2000, 2500, 3000, 3500, 4000, 5000, 6000, 7000], { screen: 'battle', state: 'end turn → the boss acts (enemy action, hit on the player, omen change)', how: 'X with nothing played; time from the key' });
    const fl = await marks(page, 'ocbg-battle-float', before);
    info.bossFloats = fl;
    if (fl[0]) saveFrames(got, fl[0].t, '664-battle-player-hit', [0, 100, 200, 400, 700, 1000], { screen: 'battle', state: `hit on the player: floating number 「${fl[0].text}」`, how: 'time from the floating number attaching' });
    turns++;
    if (s2 === 'over') { lost = true; break; }
    await page.mouse.move(960, 200);
    await sleep(500);
    await shot(page, '665-battle-boss-turn2', 'battle', 'boss fight turn 2 (after the boss\'s action: new omen, HP lost)', 'after the enemy turn');
    continue;
  }
  await B.endTurn();
  turns++;
}
info.turns = turns;
log('lost: ' + lost + ' after turns ' + turns);
await tryCap('loss result', async () => {
  const before = await pageNow(page) - 15000;
  const cast = await startCast(page);
  await page.waitForFunction(() => document.querySelector('.ocbg-battle-result-paper.is-lost'), null, { timeout: 30000 });
  await sleep(4000);
  const got = await cast.stop();
  const r = await marks(page, 'ocbg-battle-result', before);
  if (r.length) saveFrames(got, r[r.length - 1].t, '666-battle-result-lost', [0, 200, 400, 700, 1000, 1500, 2500, 3500], { screen: 'battle', state: 'loss: ink, 力尽きた, the result screen rising (where, HP, cards by attribute, 手記へ)', how: 'turns ended until the heir fell; time from the result element attaching' });
  await shot(page, '667-battle-result-lost', 'battle', 'loss result screen settled (手記へ)', 'after the rise');
  await hoverSel(page, '.ocbg-battle-result-button');
  await sleep(300);
  await shot(page, '668-battle-result-lost-hover', 'battle', 'loss result: pointer over 手記へ', 'mouse.move onto the button');
  await clickSel(page, '.ocbg-battle-result-button');
});
// the end-of-life card on the map
await page.waitForSelector('.ocbg-explore:not(.ocbg-scene)', { timeout: 30000 });
{
  const before = await pageNow(page);
  const cast = await startCast(page);
  await page.waitForSelector('.ocbg-explore-plate.is-end', { timeout: 30000 }).catch(() => {});
  await sleep(2500);
  const got = await cast.stop();
  const p = await marks(page, 'ocbg-explore-plate', before - 3000);
  info.endPlates = p;
  if (p.length) saveFrames(got, p[p.length - 1].t, '758-explore-end-card-fallen', [0, 150, 300, 600, 1000], { screen: SC, state: 'end-of-life card: 力尽きた (Fallen)', how: 'after 手記へ; time from the card attaching' });
}
await away();
await shot(page, '759-explore-end-card-fallen', SC, 'end-of-life card (Fallen) settled', 'after the card comes in');
saveCheckpoint('fallen', await dumpStorage(page));
// ---------------------------------------------------------------- the ending screen
{
  const before = await pageNow(page);
  const cast = await startCast(page);
  await sleep(150);
  await page.keyboard.press('Enter');
  const t0 = await mountedAt(page, 'ocbg-meta-ending', before, 15000);
  await sleep(9000);
  const got = await cast.stop();
  saveFrames(got, t0, '801-ending-death', [0, 300, 700, 1200, 2000, 3000, 4000, 5500, 7000, 8500], { screen: 'ending', state: 'ending screen after a death (sheet laid down, words written, what stays behind)', how: 'Enter on the end-of-life card; time from the ending root attaching' });
}
await away();
await shot(page, '802-ending-death-settled', 'ending', 'ending screen (death) settled: 継承の間へ', 'after ~9 s');
await hoverSel(page, '.ocbg-meta-washi-btn');
await sleep(300);
await shot(page, '803-ending-death-hover-button', 'ending', 'pointer over 継承の間へ', 'mouse.move onto the button');
// ---------------------------------------------------------------- the hall of the second heir
{
  const before = await pageNow(page);
  const cast = await startCast(page);
  await sleep(100);
  await page.keyboard.press('Enter');
  const t0 = await mountedAt(page, 'ocbg-meta-hall', before, 15000);
  await sleep(5000);
  const got = await cast.stop();
  saveFrames(got, t0, '804-hall-gen2-enter', [0, 300, 1000, 2000, 4000], { screen: 'hall (継承の間)', state: 'second heir: entering (past life, the trace on layer 2)', how: 'ending → 継承の間へ; time from the hall root attaching' });
}
await away();
await shot(page, '805-hall-gen2-idle', 'hall (継承の間)', 'second heir settled: past life card, untaken trace (flame) on layer 2, trace detail', 'after the entrance');
await tryCap('hall gen2 hovers', async () => {
  await hoverSel(page, '.ocbg-meta-stratum.is-live', 0);
  await sleep(400);
  await shot(page, '806-hall-gen2-hover-trace-stratum', 'hall (継承の間)', 'pointer over the layer card holding the trace', 'mouse.move onto the live stratum');
  await hoverSel(page, '.ocbg-meta-mark', 0);
  await sleep(900);
  await shot(page, '807-hall-gen2-hover-mark', 'hall (継承の間)', 'pointer over the life mark on the layer card (native title tooltip is not drawn in headless shots)', 'mouse.move onto the mark');
  await hoverSel(page, '.ocbg-meta-hall-life', 0);
  await sleep(400);
  await shot(page, '808-hall-gen2-hover-life', 'hall (継承の間)', 'pointer over the past life card', 'mouse.move onto the life card');
});
saveCheckpoint('hall2', await dumpStorage(page));
info.errors = errors;
writeJSON('info-explore2.json', info);
console.log(JSON.stringify({ log: info.log, missing: info.missing, errors }, null, 1));
await browser.close();
