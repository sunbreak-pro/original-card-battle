import { settle, takeReward, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  let st = await settle(page, 60000);
  if (st.plate.includes('is-result')) await takeReward(page, false);
  await settle(page);
  await page.click('.ocbg-explore-endlife');
  await sleep(page, 400);
  await shot('endlife-confirm');
  await page.keyboard.press('Enter');
  await sleep(page, 800);
  await shot('survived-card');
}
