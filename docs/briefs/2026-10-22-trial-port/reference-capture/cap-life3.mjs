// Section 9: the second heir fell to the layer-1 boss under autobattle (end of cap-life2). This script takes it from
// there: the end card and the ending of a death on layer 1 → 継承の間 of the third heir (two past lives, two traces) →
// 出立 with 集中 and 石礫 added (cards that leave statuses) → layer 1: the first fight by hand (status chips),
// then ?autobattle: an event with a choice if one is on the way, the second heir's trace on layer 1 (trace card,
// picks, the taken card) → この生を終える → survived card → ending (alive) → 継承の間 of the fourth heir.
// From ckpt-life2-fallen.
import {
  launch, reopen, sleep, shot, startCast, saveFrames, pageNow, marks, clickSel, hoverSel, centerOf, mountedAt,
  setIndexFile, dumpStorage, saveCheckpoint, loadCheckpoint, writeJSON,
} from './lib.mjs';
import { exploreKit, closeResult } from './explorelib.mjs';
import { battleKit } from './battlelib.mjs';

setIndexFile('life3');
const START = process.argv[2] || 'life2-fallen';
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

if (START === 'life2-fallen') {
  await continueLife();
  await page.waitForSelector('.ocbg-explore-plate.is-end', { timeout: 15000 });
  await sleep(1200);
  await away();
  await shot(page, '860-explore-end-card-fallen-layer1', SC, 'end-of-life card of the second heir (fell to the layer-1 boss under autobattle)', 'つづきから brings the card back');
  {
    const before = await pageNow(page);
    const cast = await startCast(page);
    await sleep(100);
    await page.keyboard.press('Enter');
    const t0 = await mountedAt(page, 'ocbg-meta-ending', before, 15000);
    await sleep(2200);
    const got = await cast.stop();
    saveFrames(got, t0, '861-ending-death-layer1', [0, 500, 1000, 2000], { screen: 'ending', state: 'ending of a death on layer 1 (first frames)', how: 'Enter on the end card; time from the ending root attaching' });
  }
  // a press while the words are being written lays the sheet down at once
  await page.mouse.click(300, 950);
  await sleep(600);
  await away();
  await shot(page, '862-ending-death-layer1-skipped', 'ending', 'ending (death on layer 1) after a click: the whole sheet at once (shallow death: high-rank cards stay out)', 'click on the backdrop while the words were being written');
  {
    const before = await pageNow(page);
    const cast = await startCast(page);
    await sleep(100);
    await page.keyboard.press('Enter');
    const t0 = await mountedAt(page, 'ocbg-meta-hall', before, 15000);
    await sleep(5000);
    const got = await cast.stop();
    saveFrames(got, t0, '863-hall-gen3-enter', [0, 1000, 2000, 4000], { screen: 'hall (継承の間)', state: 'third heir entering: two past lives, two traces', how: 'ending → 継承の間へ; time from the hall root attaching' });
  }
  await away();
  await shot(page, '864-hall-gen3-two-traces', 'hall (継承の間)', 'third heir: two past lives (both fallen), traces on layers 1 and 2 (1 / 2 pager)', 'settled');
  await page.keyboard.press('ArrowRight');
  await sleep(600);
  await shot(page, '865-hall-gen3-trace2', 'hall (継承の間)', 'the other trace selected (→): 2 / 2', 'ArrowRight');
  await hoverSel(page, '.ocbg-meta-stratum.is-live', 0).catch(() => {});
  await sleep(300);
  await shot(page, '866-hall-gen3-hover-stratum', 'hall (継承の間)', 'pointer over a layer card holding a trace', 'mouse.move onto the first live stratum');
  saveCheckpoint('hall3', await dumpStorage(page));
  // ---------------------------------------------------------------- departure with status cards
  await clickSel(page, '.ocbg-meta-hall .ocbg-meta-primary');
  await page.waitForSelector('.ocbg-meta-departure', { timeout: 15000 });
  await sleep(1500);
  for (const name of ['集中', '石礫']) {
    for (let k = 0; k < 6; k++) {
      const found = await page.evaluate((name) => {
        document.querySelectorAll('[data-ref="pick"]').forEach((e) => e.removeAttribute('data-ref'));
        const f = [...document.querySelectorAll('.ocbg-meta-cell-face')].find((b) => b.getAttribute('aria-label') === name);
        if (f) f.closest('.ocbg-meta-cell').setAttribute('data-ref', 'pick');
        return !!f;
      }, name);
      if (found) { await clickSel(page, '[data-ref="pick"] .ocbg-meta-step[aria-label="＋"]'); await sleep(300); break; }
      await clickSel(page, '.ocbg-meta-pagenav .ocbg-meta-nav', 1);
      await sleep(500);
    }
  }
  await away();
  await shot(page, '867-deck-gen3', 'deck (デッキを組む)', 'third heir: saved deck + 集中 + 石礫 (the last one added is selected)', '＋ on 集中 and 石礫');
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
  await shot(page, '868-supplies-gen3', 'supplies (出立の支度)', 'third heir supplies', 'chosen with clicks');
  await clickSel(page, '.ocbg-meta-dep-page .ocbg-meta-primary', 1);
  await page.waitForSelector('.ocbg-explore, .ocbg-battle', { timeout: 30000 });
  await sleep(4000);
  saveCheckpoint('life3-start', await dumpStorage(page));
}

// ---------------------------------------------------------------- the first fight by hand: statuses
if (START === 'life2-fallen' || START === 'life3-start') {
  if (START === 'life3-start') await continueLife();
  const inBattle = await page.waitForFunction(() => window.__scene && window.__scene.player && document.querySelector('.ocbg-battle:not(.ocbg-scene)'), null, { timeout: 40000 }).then(() => true, () => false);
  if (!inBattle) log('no battle at the start');
  let chipDone = false;
  for (let guard = 0; inBattle && guard < 150; guard++) {
    const s = await B.waitIdle({ fast: chipDone });
    if (s !== 'idle') { log('battle state ' + s); break; }
    if (!chipDone) {
      const chips = await B.ev(() => [...document.querySelectorAll('.ocbg-battle-chip')].map((c) => c.textContent));
      info.chipsSeen = chips;
      if (chips.length) {
        await page.mouse.move(960, 200, { steps: 2 });
        await sleep(400);
        await shot(page, '670-battle-status-chips', 'battle', `status chips on the panels (${chips.join(' / ')})`, 'after 石礫 / 集中');
        for (let k = 0; k < Math.min(chips.length, 3); k++) {
          await hoverSel(page, '.ocbg-battle-chip', k);
          await sleep(600);
          await shot(page, `671${'abc'[k]}-battle-status-chip-tooltip`, 'battle', `pointer over status chip ${k + 1} (${chips[k]}): tooltip`, 'mouse.move onto the chip');
        }
        const c = await centerOf(page, '.ocbg-battle-chip', 0);
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
    const st = fs.find((f) => f.playable && /石礫|集中|体当たり/.test(f.name));
    const atk = fs.filter((f) => f.playable && f.aim === 'Single').sort((a, b) => b.cost - a.cost)[0];
    const self = fs.find((f) => f.playable && f.aim !== 'Single');
    const pick = (!chipDone && st) || atk || self;
    if (pick) { await B.dragPlay(pick.id, pick.aim); continue; }
    await B.endTurn();
  }
  if (!chipDone) info.missing.push('status chip: no status appeared in the third heir\'s first fight');
  await page.waitForSelector('.ocbg-explore:not(.ocbg-scene)', { timeout: 60000 });
  await sleep(3000);
  saveCheckpoint('life3-fought', await dumpStorage(page));
}

// ---------------------------------------------------------------- layer 1 with autobattle: an event with a choice, the trace
await reopen(page, 'debug&autobattle');
await continueLife();
const done = {};
for (let guard = 0; guard < 60; guard++) {
  const st = await K.settle();
  if (st.scene === 'battle') { await page.waitForSelector('.ocbg-explore:not(.ocbg-scene)', { timeout: 60000 }); continue; }
  if (st.scene !== 'explore') { log('left explore: ' + st.scene); break; }
  const I = await K.info();
  const p = st.plate;
  if (st.confirm) { await page.keyboard.press('Enter'); await sleep(900); continue; }
  if (/is-eventchoice/.test(p)) {
    await sleep(600);
    if (!done.event) {
      await away();
      await shot(page, '820-explore-event-choice', SC, 'イベント choice plate (受ける / 受けない tiles)', 'arrived on an イベント node with a choice');
      await hoverSel(page, '.ocbg-explore-choice-tile', 0).catch(() => {});
      await sleep(400);
      await shot(page, '821-explore-event-choice-hover', SC, 'イベント choice: pointer over tile 1 (受ける)', 'mouse.move onto the first tile');
    }
    await page.keyboard.press('1');
    await sleep(1800);
    if (!done.event) {
      await away();
      await shot(page, '822-explore-event-choice-result', SC, 'イベント result after 受ける (a card pick when the chest gives one)', 'key 1 on the choice');
      done.event = 1;
    }
    continue;
  }
  if (/is-surveychoice/.test(p)) { await page.keyboard.press('1'); await sleep(2500); continue; }
  if (/is-swap/.test(p)) { await page.keyboard.press('Escape'); await sleep(800); continue; }
  if (/is-trace/.test(p)) {
    await sleep(900);
    await away();
    await shot(page, '830-explore-trace-card', SC, 'the trace of the second heir (死亡地点の痕跡): candidates, nothing picked', 'stepped onto the trace node');
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
    await page.keyboard.press('Escape');
    await sleep(700);
    await shot(page, '834-explore-trace-leave-confirm', SC, 'trace card: 「何も受け取らない」 asks first (the trace is collected either way)', 'Esc on the trace card');
    await clickSel(page, '.ocbg-confirm .ocbg-btn:not(.is-primary)').catch(() => {});
    await sleep(600);
    await away();
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
    await sleep(1200);
    done.trace = 1;
    continue;
  }
  if (/is-result/.test(p)) { await sleep(800); await closeResult(page, true); continue; }
  if (/is-train/.test(p)) { await page.keyboard.press('ArrowDown'); await sleep(200); await page.keyboard.press('Enter'); await sleep(1200); continue; }
  if (/is-break/.test(p) || /is-end/.test(p)) { log('stopped at ' + p); break; }
  if (st.interlude) { log('interlude'); break; }
  if (done.trace) { log('trace taken; idle on layer ' + I.layer); break; }
  const evKinds = await K.ev(() => { const s = window.__ocb.live.raw.Session; return Object.fromEntries(s.Screen().Nodes.filter((n) => n.Kind === 'Event').map((n) => [n.Id, s.EventAt(n.Id)])); });
  info.evKinds = evKinds;
  const ok = await K.reachable(['Trace', 'Event', 'Rest', 'Battle', 'Survey', 'Elite']);
  if (!ok.length) { log('no way on (layer ' + I.layer + ')'); break; }
  const trace = I.nodes.find((x) => x.kind === 'Trace');
  const choiceEvent = ok.find((x) => x.kind === 'Event' && evKinds[x.id] && evKinds[x.id] !== 'Spring');
  let n;
  if (choiceEvent && !done.event) n = choiceEvent;
  else if (trace) n = ok.find((x) => x.id === trace.id) || ok.slice().sort((a, b) => Math.abs(a.col - trace.col) - Math.abs(b.col - trace.col) || b.row - a.row)[0];
  else n = ok[0];
  if (n.kind === 'Trace' && !done.tracePreview) {
    done.tracePreview = 1;
    await K.pin(n.id);
    await away();
    await shot(page, '829-explore-preview-trace', SC, 'trace node pinned on layer 1: preview (the second heir\'s trace)', 'click the trace node');
  }
  if (!done.map) {
    done.map = 1;
    await away();
    await shot(page, '828-explore-gen3-layer1-map', SC, 'third heir, layer 1 map with the trace node of the second heir', 'after the first fight');
  }
  info.steps.push(I.layer + ':' + n.kind + '#' + n.id);
  log(`layer ${I.layer} step to ${n.kind}#${n.id}`);
  await K.walkTo(n.id, 0);
  await sleep(600);
}
saveCheckpoint('life3-trace', await dumpStorage(page));

// ---------------------------------------------------------------- この生を終える (alive)
await tryCap('end life alive', async () => {
  const st = await K.settle();
  if (st.scene !== 'explore' || st.plate) throw new Error('not on the idle map: ' + JSON.stringify(st));
  await hoverSel(page, '.ocbg-explore-endlife');
  await sleep(300);
  await clickSel(page, '.ocbg-explore-endlife');
  await page.waitForSelector('.ocbg-confirm', { timeout: 5000 });
  await sleep(500);
  await shot(page, '840-explore-confirm-endlife', SC, 'この生を終えますか (confirm, after the trace)', 'click この生を終える');
  const before = await pageNow(page);
  const cast = await startCast(page);
  await sleep(150);
  const t0 = Date.now();
  await page.keyboard.press('Enter');
  await sleep(2400);
  const got = await cast.stop();
  saveFrames(got, t0, '841-explore-end-card-survived', [0, 150, 300, 600, 1000, 1600], { screen: SC, state: 'end-of-life card: closed alive (Survived)', how: 'Enter on the confirmation' });
  await away();
  await shot(page, '842-explore-end-card-survived', SC, 'end-of-life card (Survived) settled', 'after the card comes in');
  {
    const b2 = await pageNow(page);
    const c2 = await startCast(page);
    await sleep(100);
    await page.keyboard.press('Enter');
    const tE = await mountedAt(page, 'ocbg-meta-ending', b2, 15000);
    await sleep(9000);
    const g2 = await c2.stop();
    saveFrames(g2, tE, '850-ending-alive', [0, 300, 700, 1200, 2000, 3000, 4000, 5500, 7000, 8500], { screen: 'ending', state: 'ending screen after a life closed alive (warm light, what passes on)', how: 'Enter on the survived card; time from the ending root attaching' });
  }
  await away();
  await shot(page, '851-ending-alive-settled', 'ending', 'ending screen (alive) settled', 'after ~9 s');
  {
    const b3 = await pageNow(page);
    const c3 = await startCast(page);
    await sleep(100);
    await page.keyboard.press('Enter');
    const tH = await mountedAt(page, 'ocbg-meta-hall', b3, 15000);
    await sleep(5000);
    const g3 = await c3.stop();
    saveFrames(g3, tH, '852-hall-gen4-enter', [0, 300, 1000, 2000, 4000], { screen: 'hall (継承の間)', state: 'fourth heir entering: three past lives, the survivor\'s bonus', how: 'ending → 継承の間へ; time from the hall root attaching' });
  }
  await away();
  await shot(page, '853-hall-gen4-idle', 'hall (継承の間)', 'fourth heir settled: three past lives (fallen, fallen, survived), survivor bonus block, the remaining trace', 'after the entrance');
  await hoverSel(page, '.ocbg-meta-hall-life', 0).catch(() => {});
  await sleep(300);
  await shot(page, '854-hall-gen4-hover-life', 'hall (継承の間)', 'pointer over the newest past life', 'mouse.move onto the first life card');
  saveCheckpoint('hall4', await dumpStorage(page));
  // the survivor's departure: the raised limits (4 tool slots, deck 45)
  await clickSel(page, '.ocbg-meta-hall .ocbg-meta-primary');
  await page.waitForSelector('.ocbg-meta-departure', { timeout: 15000 });
  await sleep(1500);
  await away();
  await shot(page, '855-deck-gen4-survivor-bonus', 'deck (デッキを組む)', 'fourth heir: deck limits raised by the survivor bonus (bonus chip by the title, cap 45)', '継承の間 → 出立の支度へ');
  await clickSel(page, '.ocbg-meta-dep-page .ocbg-meta-primary', 0);
  await sleep(900);
  await away();
  await shot(page, '856-supplies-gen4-survivor-bonus', 'supplies (出立の支度)', 'fourth heir supplies: the 4th tool slot of the survivor bonus', '準備へ');
});
info.done = done;
info.errors = errors;
writeJSON('info-life3.json', info);
console.log(JSON.stringify({ log: info.log, missing: info.missing, done, errors }, null, 1));
await browser.close();
