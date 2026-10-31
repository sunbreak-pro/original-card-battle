// Screenshots of every meta screen (dev only). node tools/shot.mjs dev/out/meta.html dev/shots/meta --steps dev/meta-steps.mjs
export default async function (page, shot) {
  const only = (process.env.META_ONLY || '').split(',').filter(Boolean);
  const show = async (name, wait) => {
    await page.evaluate((n) => { window.__meta.hideBar(); window.__meta.show(n); }, name);
    await page.waitForTimeout(wait);
  };
  const list = [
    ['title', 3800], ['title-first', 3800], ['hall', 4600], ['hall-first', 4200], ['hall-many', 4600], ['departure', 1400],
    ['ending-death', 7500], ['ending-alive', 7500], ['ending-miasma', 7500], ['settings', 1200], ['owned', 1200], ['journal-explore', 1000],
    ['journal-battle', 1000], ['journal-enemies', 1000],
  ];
  for (const [name, wait] of list) {
    if (only.length && !only.includes(name)) continue;
    await show(name, wait);
    await shot(name);
  }
}
