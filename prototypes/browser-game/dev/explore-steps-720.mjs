import { settle, takeReward, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  await page.setViewportSize({ width: 1280, height: 720 });
  let st = await settle(page);
  if (st.plate.includes('is-result')) await takeReward(page, false);
  await settle(page);
  // keyboard: move to the right and look
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowRight');
  await sleep(page, 300);
  await shot('kbd-select-720');
}
