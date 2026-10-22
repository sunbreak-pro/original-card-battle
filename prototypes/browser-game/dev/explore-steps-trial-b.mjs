// Trial mode, seed 4: the entry's find is given up (Esc on the swap card), the 重い宝箱 next door is taken
// (受ける): its result card lays out the pile's cards to pick one, then its consumable meets full slots
// (the swap card again), and the journal's dungeon page records the event.
// Open: dev/out/explore.html?trial=1&seed=4&noaudio=1&fast=1
import { settle, takeReward, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  let st = await settle(page, 60000);
  if (st.plate.includes('is-result')) await takeReward(page);
  st = await settle(page);
  if (st.plate.includes('is-swap')) { await page.keyboard.press('Escape'); await sleep(page, 500); }
  st = await settle(page);
  const chest = await page.evaluate(() => {
    const s = window.__dev.session;
    const map = s.State.Map;
    return map.Neighbours(s.State.CurrentNodeId).find((id) => map.Node(id).Kind === 'Event' && s.EventAt(id) === 'HeavyChest');
  });
  await page.click(`.ocbg-explore-node[data-node="${chest}"]`);
  await sleep(page, 300);
  await shot('chest-detail');
  await page.click('.ocbg-explore-go');
  st = await settle(page);
  await sleep(page, 300);
  await shot('chest-choice');
  await page.keyboard.press('1'); // 受ける
  st = await settle(page);
  await sleep(page, 500);
  await shot('chest-result');
  await page.keyboard.press('2');
  await sleep(page, 300);
  await shot('chest-card-picked');
  await page.keyboard.press('Enter');
  st = await settle(page);
  await sleep(page, 400);
  if (st.plate.includes('is-swap')) {
    await shot('chest-swap');
    await page.keyboard.press('1');
    st = await settle(page);
  }
  await shot('after-chest');
  await page.keyboard.press('j');
  await sleep(page, 500);
  const tab = await page.$('.ocbg-explore-jtabs button[data-id="dungeon-1"]');
  if (tab) { await tab.click(); await sleep(page, 300); }
  await shot('journal-dungeon');
}
