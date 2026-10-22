// 1280×720: the reward pick with an offer, the map with a rest choice, then the interlude.
import { settle, takeReward, pickNext, stepTo, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  await page.setViewportSize({ width: 1280, height: 720 });
  let st = await settle(page, 60000);
  await sleep(page, 600);
  if (st.plate.includes('is-result')) { await shot('reward-720'); await takeReward(page); }
  await settle(page);
  const rest = await pickNext(page, 'Rest');
  await page.click(`.ocbg-explore-node[data-node="${rest}"]`);
  await sleep(page, 400);
  await shot('select-720');
}
