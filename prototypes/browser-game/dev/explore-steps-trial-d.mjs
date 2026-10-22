// Trial mode, seed 1: straight down to the boss (events declined, finds given up), the layer-break card
// with 灯芯の欠片's 45%, a consumable used on it, the interlude with 刻限の砂's next 刻限, and layer 2.
// Open: dev/out/explore.html?trial=1&seed=1&noaudio=1&fast=1
import { settle, takeReward, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  for (let i = 0; i < 30; i++) {
    const st = await settle(page, 60000);
    if (st.plate.includes('is-result')) { await takeReward(page); continue; }
    if (st.plate.includes('is-swap')) { await page.keyboard.press('Escape'); await sleep(page, 400); continue; }
    if (st.plate.includes('is-eventchoice')) { await page.keyboard.press('2'); await sleep(page, 400); continue; }
    if (st.plate.includes('is-surveychoice')) { await page.keyboard.press('1'); await sleep(page, 400); continue; }
    if (st.plate.includes('is-train')) { await page.keyboard.press('Escape'); continue; }
    if (st.plate.includes('is-break') || st.plate.includes('is-end')) break;
    const next = await page.evaluate(() => {
      const s = window.__dev.session;
      const scr = s.Screen();
      const ok = scr.Nodes.filter((n) => n.Standing === 'Reachable');
      if (!ok.length) return null;
      return ok.sort((a, b) => b.Row - a.Row || a.Column - b.Column)[0].Id;
    });
    if (next == null) break;
    await page.click(`.ocbg-explore-node[data-node="${next}"]`);
    await sleep(page, 150);
    await page.click('.ocbg-explore-go');
  }
  await settle(page);
  await shot('layer-break');
  await page.keyboard.press('1');
  await sleep(page, 600);
  await shot('break-item-used');
  await page.keyboard.press('Enter');
  await sleep(page, 1400);
  await shot('interlude');
  await page.keyboard.press('Enter');
  await sleep(page, 2400);
  const st = await settle(page, 60000);
  if (st.plate.includes('is-result')) await takeReward(page);
  await settle(page);
  await shot('layer2');
}
