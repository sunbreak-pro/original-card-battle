// The reward pick with cards on offer (seed 1's first fight), by keyboard: 2 picks the second card.
import { settle, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  await settle(page, 60000);
  await sleep(page, 700);
  await shot('offer');
  await page.keyboard.press('2');
  await sleep(page, 300);
  await shot('offer-picked');
  await page.keyboard.press('Enter');
  await sleep(page, 900);
  await shot('after-learn');
  const n = await page.evaluate(() => window.__dev.persisted.count);
  console.log('persist calls so far:', n);
}
