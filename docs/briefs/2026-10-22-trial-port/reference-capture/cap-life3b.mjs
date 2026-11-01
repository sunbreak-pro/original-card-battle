// Section 9b: the third heir again from 継承の間 (ckpt-hall3; the same life seed, so the same map), this time with
// 標の石: 休息 → 休息 → the 特殊な食事 event (a choice) → 標の石 jumps over the elite onto the second heir's trace on
// layer 1 → trace card, picks, taken card → この生を終える → survived card → ending (alive) → 継承の間 of the fourth heir
// (survivor bonus) → its departure (raised limits). cap-life3.mjs died at the elite under autobattle.
import {
  launch, reopen, sleep, shot, startCast, saveFrames, pageNow, marks, clickSel, hoverSel, centerOf, mountedAt,
  setIndexFile, dumpStorage, saveCheckpoint, loadCheckpoint, writeJSON,
} from './lib.mjs';
import { exploreKit, closeResult } from './explorelib.mjs';
import { battleKit } from './battlelib.mjs';

setIndexFile('life3b');
const { browser, page, errors } = await launch({ storage: loadCheckpoint('hall3'), query: 'debug' });
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

await continueLife();
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
await clickSel(page, '.ocbg-meta-dep-page .ocbg-meta-primary', 0);
await sleep(900);
await clickSel(page, '.ocbg-meta-talent', 0);
for (const name of ['防瘴の面', '写しの帳', '薬草', '標の石', '浄化の香']) {
  await page.evaluate((name) => {
    const c = [...document.querySelectorAll('.ocbg-meta-supply')].find((x) => x.querySelector('b').textContent === name);
    document.querySelectorAll('[data-ref="s"]').forEach((e) => e.removeAttribute('data-ref'));
    if (c) c.setAttribute('data-ref', 's');
  }, name);
  await clickSel(page, '[data-ref="s"] .ocbg-meta-step[aria-label="＋"]').catch(() => {});
  await sleep(200);
}
await away();
await shot(page, '869-supplies-gen3-jumpstone', 'supplies (出立の支度)', 'third heir (second try): consumables 薬草 + 標の石 + 浄化の香', 'chosen with clicks');
await clickSel(page, '.ocbg-meta-dep-page .ocbg-meta-primary', 1);
await page.waitForSelector('.ocbg-explore, .ocbg-battle', { timeout: 30000 });
// the first fight, by hand (greedy, fast-forwarded): the same fight as cap-life3.mjs, no shots
const inBattle = await page.waitForFunction(() => window.__scene && window.__scene.player && document.querySelector('.ocbg-battle:not(.ocbg-scene)'), null, { timeout: 40000 }).then(() => true, () => false);
for (let guard = 0; inBattle && guard < 150; guard++) {
  const s = await B.waitIdle({ fast: true });
  if (s !== 'idle') { log('first fight ' + s); break; }
  const r = await B.greedyStep();
  if (r === 'end') await B.waitIdle({ fast: true });
}
await page.waitForSelector('.ocbg-explore:not(.ocbg-scene)', { timeout: 60000 });
await sleep(3500);
saveCheckpoint('life3b-fought', await dumpStorage(page));

// ---------------------------------------------------------------- 休息 → 休息 → 特殊な食事 → 標の石 → the trace
const done = {};
for (let guard = 0; guard < 40; guard++) {
  const st = await K.settle();
  if (st.scene !== 'explore') { log('scene ' + st.scene); break; }
  const I = await K.info();
  const p = st.plate;
  if (st.confirm) { await page.keyboard.press('Enter'); await sleep(900); continue; }
  if (/is-eventchoice/.test(p)) {
    await sleep(600);
    await away();
    await shot(page, '820-explore-event-choice', SC, 'イベント choice plate: 特殊な食事 (受ける / 受けない tiles)', 'arrived on the 特殊な食事 node');
    await hoverSel(page, '.ocbg-explore-choice-tile', 0).catch(() => {});
    await sleep(400);
    await shot(page, '821-explore-event-choice-hover', SC, 'イベント choice: pointer over tile 1 (受ける)', 'mouse.move onto the first tile');
    await page.keyboard.press('1');
    await sleep(1800);
    await away();
    await shot(page, '822-explore-event-choice-result', SC, 'イベント result after 受ける (max stamina +1 for HP)', 'key 1 on the choice');
    done.event = 1;
    continue;
  }
  if (/is-trace/.test(p)) {
    await sleep(900);
    await away();
    await shot(page, '830-explore-trace-card', SC, 'the trace of the second heir (死亡地点の痕跡): candidates, nothing picked', 'landed on the trace node');
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
  if (/is-surveychoice/.test(p)) { await page.keyboard.press('1'); await sleep(2500); continue; }
  if (/is-swap/.test(p)) { await page.keyboard.press('Escape'); await sleep(800); continue; }
  if (/is-train/.test(p)) { await page.keyboard.press('Escape'); await sleep(800); continue; }
  if (p) { log('stopped at ' + p); break; }
  if (done.trace) { log('trace taken'); break; }
  const trace = I.nodes.find((x) => x.kind === 'Trace');
  if (done.event && trace) {
    // 標の石: aim it from its slot, then the trace node
    const slots = await K.ev(() => [...document.querySelectorAll('.ocbg-explore-slot')].map((s) => s.textContent));
    const i = slots.findIndex((t) => t.includes('標の石'));
    if (i < 0) { log('no 標の石'); break; }
    await clickSel(page, '.ocbg-explore-slot', i);
    await sleep(700);
    await away();
    await shot(page, '824-explore-jumpstone-aiming', SC, '標の石 being aimed: the nodes it can land on are marked', 'click the 標の石 slot');
    await K.pin(trace.id);
    await away();
    await shot(page, '825-explore-jumpstone-target', SC, '標の石: the trace node pinned with its jump option', 'click the trace node while aiming');
    const before = await pageNow(page);
    const cast = await startCast(page);
    await sleep(150);
    const t0 = Date.now();
    await K.go(0);
    await sleep(2400);
    const got = await cast.stop();
    saveFrames(got, t0, '826-explore-jump-leap', [0, 100, 200, 350, 500, 700, 1000, 1500], { screen: SC, state: 'the 標の石 leap (walker jumps instead of walking)', how: 'click the jump option; time from the click' });
    const lp = await marks(page, 'ocbg-explore-walker.is-leaping', before);
    if (lp[0]) saveFrames(got, lp[0].t, '827-explore-jump-leap-from-start', [0, 100, 200, 300, 450, 600], { screen: SC, state: 'leap frames from the walker leaving', how: 'time from the walker\'s is-leaping class' });
    continue;
  }
  // the walk: 休息 (休む) → 休息 (休む) → the event with a choice
  const evKinds = await K.ev(() => { const s = window.__ocb.live.raw.Session; return Object.fromEntries(s.Screen().Nodes.filter((n) => n.Kind === 'Event').map((n) => [n.Id, s.EventAt(n.Id)])); });
  const ok = await K.reachable(['Event', 'Rest', 'Survey', 'Battle', 'Elite']);
  const ev = ok.find((x) => x.kind === 'Event' && evKinds[x.id] && evKinds[x.id] !== 'Spring');
  const rest = ok.find((x) => x.kind === 'Rest');
  const n = ev || rest || ok[0];
  if (!n) { log('no way on'); break; }
  info.steps.push(n.kind + '#' + n.id);
  log('step to ' + n.kind + '#' + n.id);
  await K.walkTo(n.id, 0);
  await sleep(600);
}
saveCheckpoint('life3b-trace', await dumpStorage(page));

// ---------------------------------------------------------------- この生を終える (alive) → ending → hall of the fourth heir
await tryCap('end life alive', async () => {
  const st = await K.settle();
  if (st.scene !== 'explore' || st.plate) throw new Error('not on the idle map: ' + JSON.stringify(st));
  await hoverSel(page, '.ocbg-explore-endlife');
  await sleep(300);
  await clickSel(page, '.ocbg-explore-endlife');
  await page.waitForSelector('.ocbg-confirm', { timeout: 5000 });
  await sleep(500);
  await shot(page, '840-explore-confirm-endlife', SC, 'この生を終えますか (confirm, after the trace)', 'click この生を終える');
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
  await clickSel(page, '.ocbg-meta-hall .ocbg-meta-primary');
  await page.waitForSelector('.ocbg-meta-departure', { timeout: 15000 });
  await sleep(1500);
  await away();
  await shot(page, '855-deck-gen4-survivor-bonus', 'deck (デッキを組む)', 'fourth heir: deck limits raised by the survivor bonus (bonus chip by the title)', '継承の間 → 出立の支度へ');
  await clickSel(page, '.ocbg-meta-dep-page .ocbg-meta-primary', 0);
  await sleep(900);
  await away();
  await shot(page, '856-supplies-gen4-survivor-bonus', 'supplies (出立の支度)', 'fourth heir supplies: the survivor bonus slot', '準備へ');
});
info.done = done;
info.errors = errors;
writeJSON('info-life3b.json', info);
console.log(JSON.stringify({ log: info.log, missing: info.missing, done, errors }, null, 1));
await browser.close();
