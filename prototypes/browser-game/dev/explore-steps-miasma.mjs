// High miasma (?miasma=78): the gauge, the shade from the corners, a 20% line warning, incense.
import { settle, takeReward, pickNext, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  let st = await settle(page, 60000);
  if (st.plate.includes('is-result')) await takeReward(page, false);
  await settle(page);
  const id = await pickNext(page);
  await page.click(`.ocbg-explore-node[data-node="${id}"]`);
  await sleep(page, 400);
  await shot('high-miasma');
  await page.keyboard.press('1');
  await sleep(page, 250);
  await shot('incense-easing');
  await sleep(page, 900);
  await shot('incense-after');
}
