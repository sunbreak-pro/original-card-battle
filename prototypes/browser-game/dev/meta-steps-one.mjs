// One screen by name (dev only): META_SHOW=name META_WAIT=ms
export default async function (page, shot) {
  const name = process.env.META_SHOW || 'title';
  await page.evaluate((n) => { window.__meta.hideBar(); window.__meta.show(n); }, name);
  await page.waitForTimeout(Number(process.env.META_WAIT || 2000));
  await shot(name);
}
