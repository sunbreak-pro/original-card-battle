// The full walk on the real dev page (dev/explore.js): layer 1 start → reward → preview → rest → boss →
// layer break → interlude → layer 2.
import { settle, takeReward, pickNext, stepTo, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  await sleep(page, 200);
  await shot('layer1-start');
  let st = await settle(page, 60000);
  if (st.plate.includes('is-result')) { await sleep(page, 500); await shot('after-battle-reward'); await takeReward(page); }
  st = await settle(page);
  // a node selected with its preview
  const first = await pickNext(page);
  await page.click(`.ocbg-explore-node[data-node="${first}"]`);
  await sleep(page, 400);
  await shot('node-preview');
  // walk down, preferring a rest node, until the layer breaks
  let restShot = false;
  for (let i = 0; i < 12; i++) {
    st = await settle(page, 60000);
    if (st.plate.includes('is-result')) {
      await sleep(page, 300);
      const boss = await page.evaluate(() => { const s = window.__dev.session.State; return s.Map.Node(s.CurrentNodeId).Kind === 'Boss'; });
      if (boss) { await sleep(page, 500); await shot('boss-reward'); }
      await takeReward(page);
      continue;
    }
    if (st.plate.includes('is-train')) { await page.keyboard.press('Escape'); continue; }
    if (st.plate.includes('is-break') || st.plate.includes('is-end')) break;
    const rest = await pickNext(page, 'Rest');
    const isRest = rest != null && (await page.evaluate((id) => window.__dev.session.Screen().Node(id).Kind === 'Rest', rest));
    if (isRest && !restShot) {
      await page.click(`.ocbg-explore-node[data-node="${rest}"]`);
      await sleep(page, 400);
      await shot('rest-choice');
      restShot = true;
      await stepTo(page, rest, 0);
      await sleep(page, 380);
      await shot('walking');
      continue;
    }
    const id = await pickNext(page);
    if (id == null) break;
    await stepTo(page, id);
  }
  st = await settle(page);
  await shot('layer-break');
  // the consumables stay usable on the layer-break card (key 1)
  await page.keyboard.press('1');
  await sleep(page, 700);
  await shot('layer-break-item');
  await page.keyboard.press('Enter');
  await sleep(page, 1400);
  await shot('interlude');
  // Enter descends from the interlude
  await page.keyboard.press('Enter');
  await sleep(page, 900);
  await shot('descend-banner');
  await sleep(page, 1800);
  st = await settle(page, 60000);
  if (st.plate.includes('is-result')) await takeReward(page);
  await settle(page);
  await shot('layer2');
}
