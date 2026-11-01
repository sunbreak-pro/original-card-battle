// Section 8: the second heir. 継承の間 → 出立 (the saved deck) → layer 1: the first fight by hand (a card that leaves a
// status on the enemy, for the status chip tooltip), then ?autobattle: an event with a choice, the boss, the descent →
// layer 2: the trace of the first heir (the trace card, picks, the taken card) → この生を終える → survived card →
// ending (alive) → 継承の間 of the third heir. From ckpt-hall2.
import {
  launch, reopen, sleep, shot, startCast, saveFrames, pageNow, marks, clickSel, hoverSel, centerOf, mountedAt,
  setIndexFile, dumpStorage, saveCheckpoint, loadCheckpoint, writeJSON,
} from './lib.mjs';
import { exploreKit, closeResult } from './explorelib.mjs';
import { battleKit } from './battlelib.mjs';

setIndexFile('life2');
const START = process.argv[2] || 'hall2';
const { browser, page, errors } = await launch({ storage: loadCheckpoint(START), query: 'debug' });
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
  await page.waitForSelector('.ocbg-explore, .ocbg-battle, .ocbg-meta-hall', { timeout: 30000 });
  await sleep(2500);
};

if (START === 'hall2') {
  await continueLife(); // the lineage is saved: つづきから comes back to the hall
  await shot(page, '809-hall-gen2-via-continue', 'hall (継承の間)', 'second heir via つづきから (settled)', 'reload, つづきから');
  // ---------------------------------------------------------------- departure of the second heir
  await clickSel(page, '.ocbg-meta-hall .ocbg-meta-primary');
  await page.waitForSelector('.ocbg-meta-departure', { timeout: 15000 });
  await sleep(1500);
  await away();
  await shot(page, '810-deck-gen2-saved-deck', 'deck (デッキを組む)', 'second heir: the deck opens on the saved deck of the last departure', '継承の間 → 出立の支度へ');
  await clickSel(page, '.ocbg-meta-dep-page .ocbg-meta-primary', 0);
  await sleep(900);
  await clickSel(page, '.ocbg-meta-talent', 0);
  for (const name of ['防瘴の面', '写しの帳', '薬草', '浄化の香']) {
    await page.evaluate((name) => {
      const c = [...document.querySelectorAll('.ocbg-meta-supply')].find((x) => x.querySelector('b').textContent === name);
      document.querySelectorAll('[data-ref="s"]').forEach((e) => e.removeAttribute('data-ref'));
      if (c) c.setAttribute('data-ref', 's');
    }, name);
    await clickSel(page, '[data-ref="s"] .ocbg-meta-step[aria-label="＋"]').catch(() => {});
    await sleep(200);
  }
  await away();
  await shot(page, '811-supplies-gen2', 'supplies (出立の支度)', 'second heir supplies (talent, 防瘴の面 + 写しの帳, 薬草 + 浄化の香)', 'chosen with clicks');
  await clickSel(page, '.ocbg-meta-dep-page .ocbg-meta-primary', 1);
  await page.waitForSelector('.ocbg-explore, .ocbg-battle', { timeout: 30000 });
  await sleep(4000);
  saveCheckpoint('life2-start', await dumpStorage(page));
}

// ---------------------------------------------------------------- the first fight by hand: a status on the enemy
if (START === 'hall2' || START === 'life2-start') {
  if (START === 'life2-start') await continueLife();
  await page.waitForFunction(() => window.__scene && window.__scene.player && document.querySelector('.ocbg-battle:not(.ocbg-scene)'), null, { timeout: 40000 }).catch(() => log('no battle at the start'));
  let chipDone = false;
  const STATUS = /鈍足|崩し|出血|毒|麻痺|怯み|威圧|脆弱|弱体/;
  for (let guard = 0; guard < 120; guard++) {
    const s = await B.waitIdle({ fast: chipDone });
    if (s !== 'idle') { log('battle state ' + s); break; }
    if (!chipDone) {
      const chips = await B.ev(() => [...document.querySelectorAll('.ocbg-battle-chip')].map((c) => c.textContent));
      if (chips.length) {
        info.chips = chips;
        await page.mouse.move(960, 200, { steps: 2 });
        await sleep(300);
        await shot(page, '670-battle-status-chips', 'battle', `status chips on the panel (${chips.join(' / ')})`, 'after a card that leaves a status');
        await hoverSel(page, '.ocbg-battle-chip', chips.length - 1);
        await sleep(600);
        await shot(page, '671-battle-status-chip-tooltip', 'battle', `pointer over a status chip (${chips[chips.length - 1]}): tooltip`, 'mouse.move onto the chip');
        const c = await centerOf(page, '.ocbg-battle-chip', chips.length - 1);
        await page.mouse.click(c[0], c[1], { button: 'right' });
        await sleep(500);
        await shot(page, '672-battle-status-chip-pinned', 'battle', 'status chip detail pinned (right click)', 'right click on the chip');
        await page.keyboard.press('Escape');
        await sleep(300);
        await page.mouse.move(960, 200, { steps: 2 });
        chipDone = true;
      }
    }
    const fs = await B.faces();
    const st = fs.find((f) => f.playable && f.aim === 'Single' && (STATUS.test(f.trait) || /体当たり|峰打ち|牽制/.test(f.name)));
    const atk = fs.filter((f) => f.playable && f.aim === 'Single').sort((a, b) => b.cost - a.cost)[0];
    const self = fs.find((f) => f.playable && f.aim !== 'Single');
    const pick = (!chipDone && st) || atk || self;
    if (pick) { await B.dragPlay(pick.id, pick.aim); continue; }
    await B.endTurn();
  }
  if (!chipDone) info.missing.push('status chip: no status appeared in the second heir\'s first fight');
  // the result paper goes by itself after a win; the map comes back with the result card
  await page.waitForSelector('.ocbg-explore:not(.ocbg-scene)', { timeout: 60000 });
  await sleep(3000);
  saveCheckpoint('life2-fought', await dumpStorage(page));
}

// ---------------------------------------------------------------- the rest of layer 1 and the descent, with autobattle
await reopen(page, 'debug&autobattle');
await continueLife();
const done = {};
for (let guard = 0; guard < 60; guard++) {
  let st = await K.settle();
  if (st.scene === 'battle') { await page.waitForSelector('.ocbg-explore:not(.ocbg-scene)', { timeout: 60000 }); continue; }
  if (st.scene !== 'explore') { log('left explore: ' + st.scene); break; }
  const I = await K.info();
  const p = st.plate;
  if (st.confirm) { await page.keyboard.press('Enter'); await sleep(900); continue; }
  if (/is-eventchoice/.test(p)) {
    await sleep(500);
    if (!done.event) {
      await shot(page, '820-explore-event-choice', SC, 'イベント choice plate (受ける / 受けない tiles)', 'arrived on an イベント node with a choice');
      await hoverSel(page, '.ocbg-explore-choice-tile', 0).catch(() => {});
      await sleep(400);
      await shot(page, '821-explore-event-choice-hover', SC, 'イベント choice: pointer over tile 1 (受ける)', 'mouse.move onto the first tile');
    }
    await page.keyboard.press('1');
    await sleep(1600);
    if (!done.event) {
      await away();
      await shot(page, '822-explore-event-choice-result', SC, 'イベント result after 受ける (card pick when the chest gives one)', 'key 1 on the choice');
      done.event = 1;
    }
    continue;
  }
  if (/is-surveychoice/.test(p)) { await page.keyboard.press('1'); await sleep(2500); continue; }
  if (/is-swap/.test(p)) {
    if (!done.swap) { done.swap = 1; await sleep(400); await shot(page, '823-explore-swap', SC, 'found consumable with full slots: swap plate', 'reached during the walk'); }
    await page.keyboard.press('Escape'); await sleep(800); continue;
  }
  if (/is-trace/.test(p)) {
    await sleep(800);
    await away();
    await shot(page, '830-explore-trace-card', SC, 'the trace of the first heir (死亡地点の痕跡): candidates, picks 0', 'stepped onto the trace node');
    await page.keyboard.press('ArrowDown');
    await sleep(300);
    await shot(page, '831-explore-trace-cursor', SC, 'trace card: cursor on the first candidate (↓)', 'ArrowDown');
    await page.keyboard.press(' ');
    await sleep(300);
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press(' ');
    await sleep(500);
    await shot(page, '832-explore-trace-picked', SC, 'trace card: two candidates picked (Space)', 'Space, ↓↓, Space');
    await hoverSel(page, '.ocbg-explore-trace-row', 3).catch(() => {});
    await sleep(300);
    await shot(page, '833-explore-trace-hover-row', SC, 'trace card: pointer over a candidate row', 'mouse.move onto row 4');
    // 何も受け取らない asks first: show the question, then go back
    await page.keyboard.press('Escape');
    await sleep(600);
    await shot(page, '834-explore-trace-leave-confirm', SC, 'trace card: 「何も受け取らない」 asks first (the trace is collected either way)', 'Esc on the trace card');
    await clickSel(page, '.ocbg-confirm .ocbg-btn:not(.is-primary)').catch(() => {});
    await sleep(500);
    const cast = await startCast(page);
    await sleep(150);
    const t0 = Date.now();
    await page.keyboard.press('Enter');
    await sleep(2200);
    const got = await cast.stop();
    saveFrames(got, t0, '835-explore-trace-take', [0, 200, 400, 700, 1000, 1500, 2000], { screen: SC, state: 'taking the picks: the trace card goes, 受け取った遺産 comes', how: 'Enter on the trace card' });
    await away();
    await shot(page, '836-explore-trace-taken', SC, '受け取った遺産 card (what joined this heir)', 'after the take');
    await page.keyboard.press('Enter');
    await sleep(900);
    done.trace = 1;
    continue;
  }
  if (/is-result/.test(p)) { await sleep(800); await closeResult(page, true); continue; }
  if (/is-train/.test(p)) { await page.keyboard.press('ArrowDown'); await sleep(200); await page.keyboard.press('Enter'); await sleep(1200); continue; }
  if (/is-break/.test(p)) { await sleep(500); await page.keyboard.press('Enter'); await sleep(2200); continue; }
  if (/is-end/.test(p)) { log('life ended: ' + p); break; }
  if (st.interlude) { await page.keyboard.press('Enter'); await sleep(6000); continue; }
  if (done.trace) { log('trace taken; idle on layer ' + I.layer); break; }
  // the next node: on layer 1 an event with a choice first; on layer 2 the trace
  const evKinds = await K.ev(() => { const s = window.__ocb.live.raw.Session; return Object.fromEntries(s.Screen().Nodes.filter((n) => n.Kind === 'Event').map((n) => [n.Id, s.EventAt(n.Id)])); });
  const ok = await K.reachable(['Trace', 'Event', 'Rest', 'Battle', 'Survey', 'Elite', 'Boss']);
  if (!ok.length) { log('no way on (layer ' + I.layer + ')'); break; }
  let n = ok[0];
  const trace = I.nodes.find((x) => x.kind === 'Trace');
  const choiceEvent = ok.find((x) => x.kind === 'Event' && evKinds[x.id] && evKinds[x.id] !== 'Spring');
  if (trace) n = ok.find((x) => x.id === trace.id) || ok.slice().sort((a, b) => Math.abs(a.col - trace.col) - Math.abs(b.col - trace.col) || b.row - a.row)[0];
  else if (choiceEvent && !done.event) n = choiceEvent;
  else n = ok.filter((x) => x.kind !== 'Event' || !done.event)[0] || ok[0];
  if (n.kind === 'Trace' && !done.tracePreview) {
    done.tracePreview = 1;
    await K.pin(n.id);
    await away();
    await shot(page, '829-explore-preview-trace', SC, 'trace node pinned on layer 2: preview (the first heir\'s trace)', 'click the trace node');
  }
  if (I.layer === 2 && !done.layer2Map) {
    done.layer2Map = 1;
    await away();
    await shot(page, '828-explore-gen2-layer2-map', SC, 'second heir, layer 2 map with the trace node of the first heir', 'after the descent');
  }
  info.steps.push(I.layer + ':' + n.kind + '#' + n.id);
  log(`layer ${I.layer} step to ${n.kind}#${n.id}`);
  await K.walkTo(n.id, 0);
  await sleep(600);
}
saveCheckpoint('life2-trace', await dumpStorage(page));

// ---------------------------------------------------------------- この生を終える (alive)
await tryCap('end life alive', async () => {
  const st = await K.settle();
  if (st.scene !== 'explore' || st.plate) throw new Error('not on the idle map: ' + JSON.stringify(st));
  await clickSel(page, '.ocbg-explore-endlife');
  await sleep(700);
  await shot(page, '840-explore-confirm-endlife', SC, 'この生を終えますか (confirm)', 'click この生を終える');
  const before = await pageNow(page);
  const cast = await startCast(page);
  await sleep(150);
  const t0 = Date.now();
  await page.keyboard.press('Enter');
  await sleep(2400);
  const got = await cast.stop();
  saveFrames(got, t0, '841-explore-end-card-survived', [0, 150, 300, 600, 1000, 1600], { screen: SC, state: 'end-of-life card: 生きて次へ (Survived)', how: 'Enter on the confirmation' });
  await away();
  await shot(page, '842-explore-end-card-survived', SC, 'end-of-life card (Survived) settled', 'after the card comes in');
  {
    const before2 = await pageNow(page);
    const cast2 = await startCast(page);
    await sleep(100);
    await page.keyboard.press('Enter');
    const tE = await mountedAt(page, 'ocbg-meta-ending', before2, 15000);
    await sleep(9000);
    const got2 = await cast2.stop();
    saveFrames(got2, tE, '850-ending-alive', [0, 300, 700, 1200, 2000, 3000, 4000, 5500, 7000, 8500], { screen: 'ending', state: 'ending screen after a life closed alive (warm light, what passes on)', how: 'Enter on the survived card; time from the ending root attaching' });
  }
  await away();
  await shot(page, '851-ending-alive-settled', 'ending', 'ending screen (alive) settled', 'after ~9 s');
  // a press while the words are still written lays the sheet down at once: shown on the death ending in 801; here, go on
  {
    const before3 = await pageNow(page);
    const cast3 = await startCast(page);
    await sleep(100);
    await page.keyboard.press('Enter');
    const tH = await mountedAt(page, 'ocbg-meta-hall', before3, 15000);
    await sleep(5000);
    const got3 = await cast3.stop();
    saveFrames(got3, tH, '852-hall-gen3-enter', [0, 300, 1000, 2000, 4000], { screen: 'hall (継承の間)', state: 'third heir: entering (two past lives, survivor bonus)', how: 'ending → 継承の間へ; time from the hall root attaching' });
  }
  await away();
  await shot(page, '853-hall-gen3-idle', 'hall (継承の間)', 'third heir settled: two past lives (fallen, survived), the trace collected, survivor bonus', 'after the entrance');
  await hoverSel(page, '.ocbg-meta-hall-life', 0).catch(() => {});
  await sleep(300);
  await shot(page, '854-hall-gen3-hover-life', 'hall (継承の間)', 'pointer over the newest past life', 'mouse.move onto the first life card');
  saveCheckpoint('hall3', await dumpStorage(page));
});
info.done = done;
info.errors = errors;
writeJSON('info-life2.json', info);
console.log(JSON.stringify({ log: info.log, missing: info.missing, done, errors }, null, 1));
await browser.close();
