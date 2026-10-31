// States the walk does not reach: the opening banners, a far node read by hover, the fallback
// cards / settings pages, the training pick after 「訓練する」, and the band after it.
import { settle, takeReward, pickNext, stepTo, sleep } from './explore-walk.mjs';
export default async function (page, shot, S) {
  // load again and catch the opening banners as they come (the first load may finish after them)
  await page.goto(page.url(), { waitUntil: 'commit' });
  await page.waitForFunction(() => document.querySelector('.ocbg-explore-banner.is-on:not(.is-encounter)'), null, { timeout: 20000, polling: 'raf' });
  await sleep(page, 400);
  await shot('layer-banner');
  await page.waitForFunction(() => document.querySelector('.ocbg-explore-banner.is-encounter.is-on'), null, { timeout: 20000, polling: 'raf' });
  await sleep(page, 350);
  await shot('encounter-banner');
  let st = await settle(page, 60000);
  if (st.plate.includes('is-result')) { await takeReward(page, false); }
  await settle(page);
  await shot('layer1-map');
  // hover a far node
  const far = await page.evaluate(() => window.__dev.session.Screen().Nodes.find((n) => n.Standing === 'Far').Id);
  await page.hover(`.ocbg-explore-node[data-node="${far}"]`);
  await sleep(page, 300);
  await shot('hover-far');
  await page.mouse.move(10, 10);
  // the fallback pages
  await page.keyboard.press('c');
  await sleep(page, 400);
  await shot('cards-page');
  await page.keyboard.press('c');
  await sleep(page, 300);
  await page.keyboard.press('Escape');
  await sleep(page, 400);
  await shot('settings-page');
  await page.keyboard.press('Escape');
  await sleep(page, 300);
  // walk to a rest node and train
  for (let i = 0; i < 5; i++) {
    st = await settle(page, 60000);
    if (st.plate.includes('is-result')) { await takeReward(page, false); continue; }
    if (st.plate.includes('is-train')) break;
    const id = await pickNext(page, 'Rest');
    const isRest = await page.evaluate((id) => window.__dev.session.Screen().Node(id).Kind === 'Rest', id);
    if (isRest) {
      await page.click(`.ocbg-explore-node[data-node="${id}"]`);
      await sleep(page, 300);
      await page.keyboard.down('Shift');
      await page.keyboard.press('Enter');
      await page.keyboard.up('Shift');
      await sleep(page, 1600);
      continue;
    }
    await stepTo(page, id);
  }
  st = await settle(page);
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await sleep(page, 300);
  await shot('training');
  await page.keyboard.press('Enter');
  await sleep(page, 900);
  await shot('after-training');
}
