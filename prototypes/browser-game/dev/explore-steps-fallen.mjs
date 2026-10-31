// Seed 1: the auto-play falls at layer 1's boss — the 倒れた end card.
import { settle, takeReward, pickNext, stepTo, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  for (let i = 0; i < 14; i++) {
    const st = await settle(page, 60000);
    if (st.plate.includes('is-end')) { await sleep(page, 600); await shot('fallen-card'); return; }
    if (st.plate.includes('is-result')) { await takeReward(page, false); continue; }
    if (st.plate.includes('is-train')) { await page.keyboard.press('Escape'); continue; }
    const id = await pickNext(page, 'Rest');
    if (id == null) break;
    await stepTo(page, id);
  }
  console.log('no end card');
}
