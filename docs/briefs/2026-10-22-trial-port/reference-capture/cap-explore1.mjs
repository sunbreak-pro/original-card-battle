// Section 5a: layer 1 after the first fight, with ?autobattle (every later fight fought unattended):
// result card, node hover / pin / preview, overlays, walking, 情報収集 (写しの帳's choice), イベント, 休息 → 訓練,
// boss, layer-break card, interlude, descent, layer 2 map. From ckpt-battle1; ends with ckpt-layer2.
import {
  launch, sleep, shot, startCast, saveFrames, pageNow, marks, clickSel, hoverSel, centerOf,
  setIndexFile, dumpStorage, saveCheckpoint, loadCheckpoint, writeJSON,
} from './lib.mjs';
import { exploreKit, closeResult } from './explorelib.mjs';

setIndexFile('explore1');
const { browser, page, errors } = await launch({ storage: loadCheckpoint('battle1'), query: 'debug&autobattle' });
const K = exploreKit(page);
const info = { log: [], missing: [], steps: [] };
const log = (x) => { info.log.push(x); console.log(' ', x); };
const tryCap = async (name, fn) => { try { await fn(); } catch (e) { info.missing.push(name + ': ' + e.message.split('\n')[0]); console.log('  MISSING', name, e.message.split('\n')[0]); } };
const SC = 'exploration';
const away = async () => { await page.mouse.move(1900, 1060, { steps: 2 }); await sleep(250); };

await page.waitForSelector('.ocbg-meta-title');
await sleep(2700);
await clickSel(page, '.ocbg-meta-title-item', 1);
await page.waitForSelector('.ocbg-explore', { timeout: 30000 });
// the layer banner plays first, then the result card the reload brought back
await page.waitForSelector('.ocbg-explore-plate', { timeout: 8000 }).catch(() => {});
let st = await K.settle();
log('first status ' + JSON.stringify(st));

// ---------------------------------------------------------------- the result card (brought back by the reload)
await tryCap('result card picks', async () => {
  if (!/is-result/.test(st.plate)) throw new Error('no result card: ' + st.plate);
  await sleep(800);
  await hoverSel(page, '.ocbg-explore-pick.is-pickable', 1);
  await sleep(400);
  await shot(page, '702-explore-result-hover-pick', SC, 'battle result card: pointer over reward card 2', 'mouse.move onto the 2nd card');
  await clickSel(page, '.ocbg-explore-pick.is-pickable', 1);
  await sleep(400);
  await shot(page, '703-explore-result-card-chosen', SC, 'battle result card: card 2 chosen (frame, この札を覚える enabled)', 'click the 2nd card');
  const before = await pageNow(page);
  const cast = await startCast(page);
  await sleep(150);
  const t0 = Date.now();
  await page.keyboard.press('Enter');
  await sleep(1200);
  const got = await cast.stop();
  saveFrames(got, t0, '704-explore-result-close', [0, 100, 200, 350, 600], { screen: SC, state: 'result card closing after この札を覚える', how: 'Enter with a card chosen' });
});
st = await K.settle();
await away();
await shot(page, '705-explore-map-idle-start', SC, 'layer 1 map idle on the start node (after the first fight)', 'after closing the result card');
let I = await K.info();
info.map1 = I;
const byKind = (k) => I.nodes.filter((n) => n.kind === k);
const reach = I.nodes.filter((n) => n.standing === 'Reachable');
log('reachable: ' + reach.map((n) => n.kind + '#' + n.id).join(', '));

// ---------------------------------------------------------------- hover / pin / preview / keys
await tryCap('hover node', async () => {
  const n = reach.find((x) => x.kind === 'Elite') || reach[reach.length - 1];
  await K.hoverNode(n.id);
  await shot(page, '706-explore-hover-node', SC, `pointer over a reachable node (${n.kind}) without pinning: the detail follows the hover, no buttons`, 'mouse.move onto the node');
  const far = I.nodes.find((x) => x.standing === 'Far');
  if (far) {
    await K.hoverNode(far.id);
    await shot(page, '707-explore-hover-far-node', SC, `pointer over a far node (${far.kind})`, 'mouse.move onto a node not reachable yet');
  }
});
for (const kind of ['Survey', 'Elite', 'Battle']) {
  await tryCap('pin ' + kind, async () => {
    const n = reach.find((x) => x.kind === kind);
    if (!n) throw new Error('no reachable ' + kind);
    await K.pin(n.id);
    await away();
    await shot(page, `708-explore-pinned-${kind.toLowerCase()}`, SC, `node pinned (${kind}): selection frame and the preview panel with its way in and forecast`, 'click the node');
  });
}
await tryCap('keyboard select', async () => {
  await page.keyboard.press('ArrowLeft');
  await sleep(400);
  await shot(page, '709-explore-key-select', SC, 'selection moved with ← (keyboard)', 'ArrowLeft');
});
// overlays from the band
await tryCap('journal overlay', async () => {
  await page.keyboard.press('j');
  await sleep(900);
  await shot(page, '710-explore-journal', 'journal (手記)', 'journal drawer over the map (J)', 'J on the map');
  const tabs = await page.$$('.ocbg-meta-journal .ocbg-meta-drawer-tab');
  info.journalTabs = tabs.length;
  if (tabs.length > 1) {
    for (let k = 1; k < Math.min(tabs.length, 4); k++) {
      const b = await tabs[k].boundingBox();
      if (!b) continue;
      await page.mouse.click(b.x + b.width / 2, b.y + b.height / 2);
      await sleep(500);
      await shot(page, `710${String.fromCharCode(97 + k)}-explore-journal-tab${k + 1}`, 'journal (手記)', `journal tab ${k + 1}`, 'click the tab');
    }
  }
  await page.keyboard.press('j');
  await sleep(600);
});
await tryCap('owned cards overlay', async () => {
  await page.keyboard.press('c');
  await sleep(900);
  await shot(page, '711-explore-owned-cards', 'owned cards (所持カード)', 'owned cards overlay (C)', 'C on the map');
  await hoverSel(page, '.ocbg-meta-owned .ocbg-meta-cell', 2).catch(() => {});
  await sleep(400);
  await shot(page, '711b-explore-owned-cards-hover', 'owned cards (所持カード)', 'owned cards: pointer over a card', 'mouse.move onto a card');
  await page.keyboard.press('ArrowRight');
  await sleep(400);
  await shot(page, '711c-explore-owned-cards-key', 'owned cards (所持カード)', 'owned cards: cursor moved with →', 'ArrowRight');
  await page.keyboard.press('c');
  await sleep(600);
});
await tryCap('settings overlay', async () => {
  await page.keyboard.press('Escape');
  await sleep(900);
  await shot(page, '712-explore-settings', 'settings', 'settings over the map (保存して閉じる)', 'Esc on the map');
  await page.keyboard.press('Escape');
  await sleep(600);
});
await tryCap('end life confirm', async () => {
  await hoverSel(page, '.ocbg-explore-endlife');
  await sleep(300);
  await shot(page, '713-explore-hover-endlife', SC, 'pointer over この生を終える', 'mouse.move onto the button');
  await clickSel(page, '.ocbg-explore-endlife');
  await sleep(700);
  await shot(page, '714-explore-confirm-endlife', SC, 'confirmation: この生を終える? (やめる / 終える)', 'click この生を終える');
  await clickSel(page, '.ocbg-confirm .ocbg-btn:not(.is-primary)');
  await sleep(600);
});
await tryCap('consumable hover', async () => {
  await hoverSel(page, '.ocbg-explore-slot', 0);
  await sleep(400);
  await shot(page, '715-explore-hover-consumable', SC, 'pointer over consumable slot 1', 'mouse.move onto the slot');
  await away();
});

// ---------------------------------------------------------------- the walk through layer 1
const done = {};
let firstWalk = true;
let wantOrder = ['Survey', 'Event', 'Rest', 'Battle', 'Elite', 'Boss'];
for (let guard = 0; guard < 40; guard++) {
  st = await K.settle();
  if (st.scene === 'battle') { await page.waitForSelector('.ocbg-explore', { timeout: 60000 }); continue; }
  if (st.scene !== 'explore') { log('left explore: ' + st.scene); break; }
  I = await K.info();
  if (I.layer >= 2 && !st.plate && !st.interlude) { log('on layer 2'); break; }
  const p = st.plate;
  if (st.confirm) {
    if (!done.confirm) { done.confirm = 1; await shot(page, '716-explore-confirm', SC, 'a confirmation card over the map', 'reached during the walk'); }
    await page.keyboard.press('Enter');
    await sleep(800);
    continue;
  }
  if (/is-surveychoice/.test(p)) {
    await sleep(500);
    await shot(page, '720-explore-survey-choice', SC, '情報収集 with 写しの帳: which enemy to read up on (choice plate)', 'arrived on the 情報収集 node');
    await hoverSel(page, '.ocbg-explore-survey-row', 0).catch(() => {});
    await sleep(300);
    await shot(page, '721-explore-survey-choice-hover', SC, '情報収集 choice: pointer over option 1', 'mouse.move onto the first row');
    const before = await pageNow(page);
    const cast = await startCast(page);
    await sleep(150);
    const t0 = Date.now();
    await page.keyboard.press('1');
    await sleep(3400);
    const got = await cast.stop();
    saveFrames(got, t0, '722-explore-survey-banner', [0, 200, 500, 1000, 2000, 3000], { screen: SC, state: '情報収集 chosen: the note banner says what was raised', how: 'key 1 on the choice' });
    continue;
  }
  if (/is-eventchoice/.test(p)) {
    await sleep(500);
    await shot(page, '724-explore-event-choice', SC, 'イベント choice plate (受ける / 受けない tiles)', 'arrived on the イベント node');
    await hoverSel(page, '.ocbg-explore-choice-tile', 0).catch(() => {});
    await sleep(300);
    await shot(page, '725-explore-event-choice-hover', SC, 'イベント choice: pointer over tile 1', 'mouse.move onto the first tile');
    await page.keyboard.press('1');
    await sleep(1200);
    continue;
  }
  if (/is-swap/.test(p)) {
    await sleep(400);
    await shot(page, '728-explore-swap', SC, 'found consumable with full slots: swap plate', 'reached during the walk');
    await page.keyboard.press('Escape');
    await sleep(800);
    continue;
  }
  if (/is-result/.test(p)) {
    await sleep(900);
    const ev = /is-event/.test(p);
    const boss = I.nodes.find((n) => n.id === I.current && n.kind === 'Boss');
    const key = ev ? 'eventResult' : boss ? 'bossResult' : 'battleResult' + I.current;
    if (!done[key]) {
      done[key] = 1;
      const file = ev ? '726-explore-event-result' : boss ? '740-explore-boss-result' : `730-explore-battle-result-node${I.current}`;
      await shot(page, file, SC, ev ? 'イベント result card (what the event did)' : boss ? 'boss fight result card (autobattle)' : `fight result card (autobattle) on node ${I.current}`, 'arrived; the result card');
    }
    await closeResult(page, true);
    continue;
  }
  if (/is-train/.test(p)) {
    await sleep(500);
    const descend = /is-descend/.test(p);
    await shot(page, descend ? '745-explore-train-before-descend' : '732-explore-training', SC, descend ? 'training card before the descent (marks left)' : 'training pick card (訓練: one mark to spend)', 'reached during the walk');
    await page.keyboard.press('ArrowDown');
    await sleep(300);
    await page.keyboard.press('ArrowDown');
    await sleep(400);
    await shot(page, descend ? '746-explore-train-before-descend-chosen' : '733-explore-training-chosen', SC, 'training card: a card chosen (preview on the right)', '↓ twice');
    const before = await pageNow(page);
    const cast = await startCast(page);
    await sleep(150);
    const t0 = Date.now();
    await page.keyboard.press('Enter');
    await sleep(3000);
    const got = await cast.stop();
    if (!descend) saveFrames(got, t0, '734-explore-trained-banner', [0, 200, 500, 1000, 2000], { screen: SC, state: 'trained: the note banner says what the mark did', how: 'Enter on the training card' });
    continue;
  }
  if (/is-break/.test(p)) {
    await sleep(600);
    await shot(page, '741-explore-layer-break', SC, 'layer-break card (階層を抜けた)', 'after the boss result');
    await hoverSel(page, '.ocbg-explore-plate .ocbg-btn', 0).catch(() => {});
    await sleep(300);
    await shot(page, '741b-explore-layer-break-hover', SC, 'layer-break card: pointer over its button', 'mouse.move onto the first button');
    const cast = await startCast(page);
    await sleep(150);
    const t0 = Date.now();
    await page.keyboard.press('Enter');
    await sleep(2600);
    const got = await cast.stop();
    saveFrames(got, t0, '742-explore-to-interlude', [0, 200, 400, 700, 1000, 1500, 2400], { screen: SC, state: 'layer-break card → interlude (rest on washi, the next layer on the right)', how: 'Enter on the layer-break card' });
    continue;
  }
  if (/is-end/.test(p)) { log('life ended on layer 1: ' + p); break; }
  if (st.interlude) {
    await sleep(500);
    await away();
    await shot(page, '743-explore-interlude', SC, 'interlude (階層間の休憩): rest plate, next layer window', 'after the layer-break card');
    await hoverSel(page, '.ocbg-explore-lull-buttons .ocbg-btn', 0).catch(() => {});
    await sleep(300);
    await shot(page, '744-explore-interlude-hover-descend', SC, 'interlude: pointer over 次の層へ降りる', 'mouse.move onto the first button');
    const before = await pageNow(page);
    const cast = await startCast(page);
    await sleep(150);
    const t0 = Date.now();
    await page.keyboard.press('Enter');
    // a training card may ask first (marks left): spend them, then the descent plays
    for (let k = 0; k < 6; k++) {
      await sleep(700);
      const s2 = await K.status();
      if (/is-train/.test(s2.plate)) {
        if (!done.trainDescend) {
          done.trainDescend = 1;
          await shot(page, '745-explore-train-before-descend', SC, 'training card before the descent (marks left)', 'Enter at the interlude with marks left');
        }
        await page.keyboard.press('ArrowDown');
        await sleep(200);
        await page.keyboard.press('Enter');
        continue;
      }
      if (s2.confirm) { await page.keyboard.press('Enter'); continue; }
      if (!s2.interlude) break;
    }
    await sleep(5200);
    const got = await cast.stop();
    saveFrames(got, t0, '747-explore-descend', [0, 300, 600, 1000, 1500, 2000, 3000, 4000, 5000], { screen: SC, state: 'descent: interlude → layer 2 (descend banner, map assembling)', how: 'Enter at the interlude; time from the key' });
    const bn = await marks(page, 'ocbg-explore-banner.is-on', before);
    info.descendBanners = bn;
    const lb = bn.find((b) => /2/.test(b.text) && /層/.test(b.text));
    if (lb) saveFrames(got, lb.t, '748-explore-layer2-banner', [0, 150, 300, 600, 1000, 1500], { screen: SC, state: `descend banner 「${lb.text}」`, how: 'time from the layer banner switching on' });
    continue;
  }
  // ---- the map is idle: choose the next node
  const ok = await K.reachable(wantOrder);
  if (!ok.length) { log('no way on'); break; }
  const n = ok[0];
  info.steps.push(n.kind + '#' + n.id);
  log(`layer ${I.layer} step to ${n.kind}#${n.id} (opts ${n.opts.join('/')})`);
  const kindKey = 'pin' + n.kind;
  if (!done[kindKey]) {
    done[kindKey] = 1;
    await K.pin(n.id);
    await away();
    await shot(page, `7${{ Survey: 19, Event: 23, Rest: 27, Battle: 29, Elite: 31, Boss: 36 }[n.kind] || 39}-explore-preview-${n.kind.toLowerCase()}`, SC, `${n.kind} node pinned: preview panel (${n.opts.join(' / ')})`, 'click the node before stepping');
  }
  const option = n.kind === 'Rest' && n.opts.length > 1 ? 1 : 0; // 訓練する on the rest node
  if (firstWalk) {
    firstWalk = false;
    await K.walkTo(n.id, option, {
      base: '718-explore-walk', ms: 2400,
      meta: { screen: SC, state: `walking animation to ${n.kind}#${n.id} (walker steps along the lit edge)`, how: 'click the way-in button; time from the click' },
      after: async (got, before) => {
        const w = await marks(page, 'ocbg-explore-walker.is-walking', before);
        info.walkMarks = w;
        if (w[0]) saveFrames(got, w[0].t, '718b-explore-walk-from-start', [0, 100, 200, 300, 400, 600, 800], { screen: SC, state: 'walking frames from the walker starting', how: 'time from the walker\'s is-walking class' });
      },
    });
  } else if (n.kind === 'Boss') {
    await K.walkTo(n.id, option, {
      base: '737-explore-to-boss', ms: 4200, at: [0, 300, 600, 900, 1200, 1500, 2000, 2500, 3000, 4000],
      meta: { screen: SC, state: 'stepping onto the boss node: walk, encounter banner (autobattle fights it unattended)', how: 'click the way-in button; time from the click' },
      after: async (got, before) => {
        const bn = await marks(page, 'ocbg-explore-banner.is-on', before);
        const enc = bn.find((b) => !/層/.test(b.text));
        if (enc) saveFrames(got, enc.t, '738-explore-boss-encounter-banner', [0, 150, 300, 600, 1000, 1500], { screen: SC, state: `encounter banner 「${enc.text}」 (boss)`, how: 'time from the banner switching on' });
      },
    });
  } else {
    await K.walkTo(n.id, option);
  }
  await sleep(600);
}

// ---------------------------------------------------------------- layer 2 map
st = await K.settle();
await away();
await sleep(500);
I = await K.info();
info.map2 = I;
await shot(page, '749-explore-layer2-map', SC, `layer ${I.layer} map, idle after the descent`, 'after the descent settles');
saveCheckpoint('layer2', await dumpStorage(page));
info.errors = errors;
info.done = done;
writeJSON('info-explore1.json', info);
console.log(JSON.stringify({ log: info.log, missing: info.missing, steps: info.steps, errors }, null, 1));
await browser.close();
