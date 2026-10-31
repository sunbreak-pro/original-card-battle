// Trial mode (docs/EXTENSIONS.md 「探索の持ち物とイベント」), seed 11, HP 30, tools 刻限の砂・写しの帳・灯芯の欠片,
// slots 薬草・標の石・濃い干し肉: the entry battle drops a 薬草 into full slots (swap card), the band and the
// inventory, a consumable used, 写しの帳's survey pick, a 泉, and 標の石 aimed and jumped.
// Open: dev/out/explore.html?trial=1&seed=11&hp=30&noaudio=1&fast=1
import { settle, takeReward, sleep } from './explore-walk.mjs';
export default async function (page, shot) {
  let st = await settle(page, 60000);
  if (st.plate.includes('is-result')) { await sleep(page, 400); await shot('entry-result'); await takeReward(page); }
  st = await settle(page);
  if (st.plate.includes('is-swap')) {
    await sleep(page, 400);
    await shot('swap-card');
    await page.keyboard.press('3'); // the 濃い干し肉 gives its slot to the 薬草 found
    await sleep(page, 600);
  }
  st = await settle(page);
  await shot('band-and-slots');
  // a consumable: hover shows its note and forecast, the key uses it
  const slot = await page.$('.ocbg-explore-slot.is-usable');
  if (slot) { await slot.hover(); await sleep(page, 300); await shot('slot-hover'); }
  await page.keyboard.press('1');
  await sleep(page, 700);
  await shot('slot-used');
  // 写しの帳: the survey node next door asks which enemy
  const survey = await page.evaluate(() => {
    const scr = window.__dev.session.Screen();
    const n = scr.Nodes.find((x) => x.Kind === 'Survey' && x.Standing === 'Reachable');
    return n ? n.Id : null;
  });
  if (survey != null) {
    await page.click(`.ocbg-explore-node[data-node="${survey}"]`);
    await sleep(page, 300);
    await shot('survey-detail');
    await page.click('.ocbg-explore-go');
    st = await settle(page);
    await sleep(page, 300);
    await shot('survey-choice');
    await page.keyboard.press('2');
    st = await settle(page);
    await shot('survey-done');
  }
  // back to the entry (free) and on to the spring next to it
  const spring = await page.evaluate(() => {
    const s = window.__dev.session;
    const map = s.State.Map;
    const entry = map.EntryId;
    return { entry, spring: map.Neighbours(entry).find((id) => map.Node(id).Kind === 'Event' && s.EventAt(id) === 'Spring') };
  });
  if (spring.spring != null) {
    await page.click(`.ocbg-explore-node[data-node="${spring.entry}"]`);
    await sleep(page, 200);
    await page.click('.ocbg-explore-go');
    await settle(page);
    await page.click(`.ocbg-explore-node[data-node="${spring.spring}"]`);
    await sleep(page, 300);
    await shot('spring-detail');
    await page.click('.ocbg-explore-go');
    st = await settle(page);
    await sleep(page, 300);
    await shot('spring-result');
    await page.keyboard.press('Enter');
    await settle(page);
  }
  // 標の石: the slot aims, the map marks where it can land, the detail offers 「跳ぶ」
  const stone = await page.evaluate(() => window.__dev.session.State.Loadout.Consumables.findIndex((c) => c.Id === 'shirube_no_ishi'));
  if (stone >= 0) {
    await page.keyboard.press(String(stone + 1));
    await sleep(page, 500);
    await shot('stone-aiming');
    const target = await page.evaluate(() => {
      const scr = window.__dev.session.Screen();
      const n = scr.Nodes.filter((x) => x.Options.some((o) => o.Jump) && x.Kind !== 'Boss').sort((a, b) => b.Row - a.Row)[0];
      return n ? n.Id : null;
    });
    await page.click(`.ocbg-explore-node[data-node="${target}"]`);
    await sleep(page, 300);
    await shot('stone-target');
    await page.click('.ocbg-explore-go');
    await sleep(page, 250);
    await shot('stone-leap');
    st = await settle(page, 60000);
    await sleep(page, 300);
    await shot('stone-landed');
  }
}
