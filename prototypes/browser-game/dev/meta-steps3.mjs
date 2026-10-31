// The meta screens at 1280×720 and the flow between them by keyboard (dev only).
export default async function (page, shot) {
  await page.setViewportSize({ width: 1280, height: 720 });
  const go = async (name, wait) => { await page.evaluate((n) => { window.__meta.hideBar(); window.__meta.show(n); }, name); await page.waitForTimeout(wait); };
  await go('hall', 4600);
  await shot('720-hall');
  await go('departure', 1200);
  await shot('720-departure');
  await go('journal-battle', 900);
  await shot('720-journal');
  // keyboard flow: title → Enter (つづきから) → hall → Esc opens settings → Esc closes → Enter departs → departure → Esc back
  await go('title', 3000);
  await page.keyboard.press('ArrowUp');
  await page.keyboard.press('Enter');
  await page.waitForTimeout(1600);
  const afterTitle = await page.evaluate(() => location.hash);
  await page.waitForTimeout(3600);
  await page.keyboard.press('Escape');
  await page.waitForTimeout(500);
  await shot('720-hall-settings');
  await page.keyboard.press('Escape');
  await page.waitForTimeout(400);
  await page.keyboard.press('Enter');
  await page.waitForTimeout(1400);
  const afterHall = await page.evaluate(() => location.hash);
  await page.keyboard.press('Escape');
  await page.waitForTimeout(900);
  const afterBack = await page.evaluate(() => location.hash);
  console.log('flow', afterTitle, afterHall, afterBack);
}
