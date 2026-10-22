// A walked neighbour reads 「戻れる」 with 「移る」 (no 刻限); stepping back walks the edge for free. Also the
// interlude at 1280×720 is checked by the walk at that size elsewhere.
import { settle, takeReward, pickNext, stepTo, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  let st = await settle(page, 60000);
  if (st.plate.includes('is-result')) await takeReward(page, false);
  await settle(page);
  const first = await pickNext(page);
  await stepTo(page, first);
  st = await settle(page, 60000);
  if (st.plate.includes('is-result')) await takeReward(page, false);
  await settle(page);
  const back = await page.evaluate(() => window.__dev.session.Screen().Nodes.find((n) => n.Standing === 'Revisit').Id);
  await page.click(`.ocbg-explore-node[data-node="${back}"]`);
  await sleep(page, 400);
  await shot('revisit');
  await stepTo(page, back);
  await sleep(page, 330);
  await shot('walking-back');
}
