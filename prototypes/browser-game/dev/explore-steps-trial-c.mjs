// Trial mode, seed 21 with tools 防瘴の面・息継ぎの笛・刻限の砂 and slots 濃い干し肉・標の石・忘れ水: the battle
// tool's chip, 防瘴の面 under the gauge, the 濃い干し肉's forecast, 標の石 aimed and stopped with Esc, the
// 特殊な食事 next door declined (受けない) — no card, the journal keeps it.
// Open: dev/out/explore.html?trial=1&seed=21&tools=bosho_no_men,ikitsugi_no_fue,kokugen_no_suna&items=koi_hoshiniku,shirube_no_ishi,wasuremizu&noaudio=1&fast=1
import { settle, takeReward, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  let st = await settle(page, 60000);
  if (st.plate.includes('is-result')) await takeReward(page);
  st = await settle(page);
  if (st.plate.includes('is-swap')) { await page.keyboard.press('Escape'); await sleep(page, 500); }
  st = await settle(page);
  const slot = await page.$('.ocbg-explore-slot.is-usable');
  if (slot) { await slot.hover(); await sleep(page, 300); }
  await shot('meat-forecast');
  await page.keyboard.press('2');
  await sleep(page, 400);
  await shot('aim');
  await page.keyboard.press('Escape');
  await sleep(page, 400);
  await shot('aim-stopped');
  const meal = await page.evaluate(() => {
    const s = window.__dev.session;
    const map = s.State.Map;
    return map.Neighbours(s.State.CurrentNodeId).find((id) => map.Node(id).Kind === 'Event' && s.EventAt(id) === 'StrangeMeal');
  });
  await page.click(`.ocbg-explore-node[data-node="${meal}"]`);
  await sleep(page, 200);
  await page.click('.ocbg-explore-go');
  st = await settle(page);
  await sleep(page, 300);
  await page.keyboard.press('2'); // 受けない
  st = await settle(page);
  await sleep(page, 300);
  await shot('meal-declined');
}
