import { settle, takeReward, pickNext, stepTo, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  let st = await settle(page);
  if (st.plate.includes('is-result')) await takeReward(page, false);
  st = await settle(page);
  await page.keyboard.press('j');
  await sleep(page, 500);
  await shot('journal');
  await page.keyboard.press('j');
  await sleep(page, 400);
  // walk toward the trace
  for (let i = 0; i < 6; i++) {
    st = await settle(page);
    if (st.plate.includes('is-trace')) break;
    if (st.plate.includes('is-result')) { await takeReward(page, false); continue; }
    const has = await page.evaluate(() => window.__dev.session.Screen().Nodes.some((n) => n.Kind === 'Trace'));
    if (!has) { console.log('no trace on this map'); break; }
    const id = await page.evaluate(() => {
      const scr = window.__dev.session.Screen();
      const trace = scr.Nodes.find((n) => n.Kind === 'Trace');
      const ok = scr.Nodes.filter((n) => n.Options.length && n.Standing === 'Reachable');
      if (ok.find((n) => n.Id === trace.Id)) return trace.Id;
      // walk toward its column
      ok.sort((a, b) => Math.abs(a.Column - trace.Column) - Math.abs(b.Column - trace.Column) || b.Row - a.Row);
      return ok.length ? ok[0].Id : null;
    });
    if (id == null) break;
    await stepTo(page, id);
  }
  st = await settle(page);
  // the keyboard: ↓ to a row, Space to mark it, twice
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Space');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Space');
  await sleep(page, 250);
  await shot('trace');
  await page.keyboard.press('Enter');
  await sleep(page, 700);
  await shot('trace-taken');
}
