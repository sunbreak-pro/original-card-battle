// The end of a life (?miasma=97): walk until a step would reach 100%, which asks first (the lethal
// forecast and its confirmation), then the 瘴気死 card.
import { settle, takeReward, stepTo, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  for (let i = 0; i < 6; i++) {
    const st = await settle(page, 60000);
    if (st.plate.includes('is-result')) { await takeReward(page, false); continue; }
    const lethal = await page.evaluate(() => {
      const scr = window.__dev.session.Screen();
      const n = scr.Nodes.find((x) => x.Standing === 'Reachable' && x.Options.some((o) => o.NeedsConfirm));
      return n ? n.Id : null;
    });
    if (lethal != null) {
      await page.click(`.ocbg-explore-node[data-node="${lethal}"]`);
      await sleep(page, 400);
      await shot('lethal-forecast');
      await stepTo(page, lethal);
      await sleep(page, 500);
      await shot('confirm-lethal');
      await page.keyboard.press('Enter');
      await sleep(page, 1800);
      await shot('end-card');
      return;
    }
    const next = await page.evaluate(() => {
      const ok = window.__dev.session.Screen().Nodes.filter((n) => n.Standing === 'Reachable');
      return ok.length ? ok.sort((a, b) => b.Row - a.Row || a.Column - b.Column)[0].Id : null;
    });
    if (next == null) break;
    await stepTo(page, next);
  }
  console.log('no lethal step found');
}
