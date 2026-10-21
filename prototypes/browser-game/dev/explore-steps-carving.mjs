// Walk layer 1 to its boss, descend, then walk to layer 2's 秘跡の彫り: the hidden name 「■■の秘跡」
// is read, the band and the banner show the real name, the app's onCarving is called.
import { settle, takeReward, pickNext, stepTo, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  for (let i = 0; i < 40; i++) {
    const st = await settle(page, 60000);
    if (st.plate.includes('is-result')) { await takeReward(page); continue; }
    if (st.plate.includes('is-train')) { await page.keyboard.press('Escape'); continue; }
    if (st.plate.includes('is-break')) { await page.keyboard.press('Enter'); await sleep(page, 900); continue; }
    if (st.plate.includes('is-end')) { console.log('life ended'); break; }
    if (st.interlude) { await page.keyboard.press('Enter'); await sleep(page, 2400); continue; }
    const info = await page.evaluate(() => {
      const s = window.__dev.session;
      const scr = s.Screen();
      const layer = s.State.Profile.Layer;
      const carving = scr.Nodes.find((n) => n.Kind === 'Carving');
      const ok = scr.Nodes.filter((n) => n.Standing === 'Reachable');
      if (layer === 2 && carving && carving.Standing === 'Current') return { done: true };
      if (layer === 2 && carving) {
        if (ok.find((n) => n.Id === carving.Id)) return { id: carving.Id, carving: true };
        ok.sort((a, b) => Math.abs(a.Column - carving.Column) - Math.abs(b.Column - carving.Column) || b.Row - a.Row);
        return { id: ok.length ? ok[0].Id : null };
      }
      ok.sort((a, b) => b.Row - a.Row || a.Column - b.Column);
      return { id: ok.length ? ok[0].Id : null };
    });
    if (info.done) break;
    if (info.id == null) { console.log('no way on'); break; }
    if (info.carving) {
      await page.click(`.ocbg-explore-node[data-node="${info.id}"]`);
      await sleep(page, 400);
      await shot('carving-preview');
      await stepTo(page, info.id);
      await sleep(page, 1500);
      await shot('carving-banner');
      await sleep(page, 1800);
      await shot('carving-read');
      console.log('carvings noted:', await page.evaluate(() => window.__dev.persisted.carvings), 'persist calls:', await page.evaluate(() => window.__dev.persisted.count));
      break;
    }
    await stepTo(page, info.id);
  }
}
