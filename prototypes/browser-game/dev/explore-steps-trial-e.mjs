// Trial mode, the review's fixes. Seed 2, HP 30, slots 標の石・薬草・濃い干し肉 (tools: the dev default).
// 1. 標の石 aimed at a far 泉: its 【跳ぶ】 forecast is a bare step (no HP line), the same as a far 特殊な食事's
//    (encounters_and_rewards_v4.md §5 「中身は隣接したときに見えます」); a 泉 next door, if any, still counts.
// 2. A refused slot key while aiming: 薬草 at full HP (HP staged to the maximum through window.__dev) cannot be
//    used, its key does nothing, and the stone stays aimed with its landing targets.
// 3. 濃い干し肉 on the floor of 3 (temporary −4 and 瘴気 81%, staged): not lit, 「いま使っても何も変わらない」.
// 4. Ending the life with the stone aimed: the confirmation stops the aim; the end card shows no lit 「やめる」.
// Open: dev/out/explore.html?trial=1&seed=2&hp=30&items=shirube_no_ishi,yakuso,koi_hoshiniku&noaudio=1&fast=1
import { settle, takeReward, sleep } from './explore-walk.mjs';

/** Redraws the scene after a staged state: the journal opens and closes (each calls render). */
async function redraw(page) {
  await page.keyboard.press('j');
  await sleep(page, 500);
  await page.keyboard.press('j');
  await sleep(page, 500);
}

export default async function (page, shot) {
  let st = await settle(page, 60000);
  if (st.plate.includes('is-result')) await takeReward(page);
  st = await settle(page);
  while (st.plate.includes('is-swap')) { await page.keyboard.press('Escape'); await sleep(page, 500); st = await settle(page); }

  // 1. aim, then the far 泉 and a far 特殊な食事 / 重い宝箱
  await page.keyboard.press('1');
  await sleep(page, 400);
  const targets = await page.evaluate(() => {
    const s = window.__dev.session;
    const scr = s.Screen();
    const events = scr.Nodes.filter((n) => n.Kind === 'Event' && n.Options.some((o) => o.Jump));
    const farSpring = events.find((n) => n.Standing === 'Far' && s.EventAt(n.Id) === 'Spring');
    const farOther = events.find((n) => n.Standing === 'Far' && s.EventAt(n.Id) !== 'Spring');
    const nearSpring = events.find((n) => n.Standing !== 'Far' && s.EventAt(n.Id) === 'Spring');
    return { hp: s.State.Hp, max: s.State.MaxHp, farSpring: farSpring?.Id ?? null, farOther: farOther?.Id ?? null, nearSpring: nearSpring?.Id ?? null,
      detail: farSpring ? farSpring.Detail : null };
  });
  console.log('[steps] targets', JSON.stringify(targets));
  for (const [label, id] of [['far-spring-jump', targets.farSpring], ['far-other-jump', targets.farOther], ['near-spring-jump', targets.nearSpring]]) {
    if (id == null) continue;
    await page.click(`.ocbg-explore-node[data-node="${id}"]`);
    await sleep(page, 400);
    await shot(label);
  }

  // 2. 薬草 at full HP: its key is refused and the aim stays
  await page.evaluate(() => { const s = window.__dev.session; s.State = s.State.With({ hp: s.State.MaxHp }); });
  await redraw(page);
  await page.keyboard.press('2');
  await sleep(page, 400);
  const kept = await page.evaluate(() => ({ aiming: window.__dev.session.IsAiming, lit: document.querySelectorAll('.ocbg-explore-slot.is-aiming').length }));
  console.log('[steps] after the refused key', JSON.stringify(kept));
  await shot('refused-key-aim-kept');

  // 3. 濃い干し肉 on the floor of 3
  await page.keyboard.press('Escape'); // stops the aim
  await sleep(page, 400);
  await page.evaluate(() => { const s = window.__dev.session; s.State = s.State.With({ miasmaPercent: 81, tempMaxStaminaMod: -4 }); });
  await redraw(page);
  const meat = (await page.$$('.ocbg-explore-slot'))[2];
  if (meat) { await meat.hover(); await sleep(page, 300); }
  await shot('meat-on-the-floor');
  await page.evaluate(() => { const s = window.__dev.session; s.State = s.State.With({ miasmaPercent: 1, tempMaxStaminaMod: 0 }); });
  await redraw(page);

  // 4. aim, then end the life
  await page.keyboard.press('1');
  await sleep(page, 400);
  await page.click('.ocbg-explore-endlife');
  await sleep(page, 600);
  const asked = await page.evaluate(() => ({ aiming: window.__dev.session.IsAiming }));
  console.log('[steps] end-life asked', JSON.stringify(asked));
  await shot('endlife-confirm');
  await page.keyboard.press('Enter');
  await sleep(page, 1800);
  const ended = await page.evaluate(() => {
    const s = window.__dev.session;
    const scr = s.Screen();
    return { phase: s.State.Phase, aiming: s.IsAiming, screenAiming: scr.Aiming, slot0: { CanUse: scr.Consumables[0].CanUse, Aiming: scr.Consumables[0].Aiming },
      litSlots: document.querySelectorAll('.ocbg-explore-slot:not([disabled])').length };
  });
  console.log('[steps] ended', JSON.stringify(ended));
  await shot('ended');
}
