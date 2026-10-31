// Pointer checks on the departure (dev only): the back button and the step buttons take clicks.
export default async function (page, shot, S) {
  const go = async (name, wait) => { await page.evaluate((n) => { window.__meta.hideBar(); window.__meta.show(n); }, name); await page.waitForTimeout(wait); };
  const click = async (x, y) => { const [cx, cy] = await S(x, y); await page.mouse.click(cx, cy); };
  await go('departure', 1200);
  await click(64 + 4 * 190 + 13 + 156 - 20, 214 + 244);
  await click(64 + 4 * 190 + 13 + 156 - 20, 214 + 244);
  await click(1236 + 318, 984 + 36); // 出立の支度へ
  await page.waitForTimeout(800);
  await click(64 + 130, 24 + 26); // デッキに戻る (header)
  await page.waitForTimeout(800);
  await shot('back-to-deck');
  await click(64 + 130, 24 + 26); // 継承の間へ (header)
  await page.waitForTimeout(900);
  console.log('after back', await page.evaluate(() => location.hash));
}
