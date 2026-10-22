// Keyboard focus checks on the departure (dev only): Tab reaches a button and Enter presses it
// instead of the screen's own Enter; Esc opens the settings when the router gives them.
export default async function (page, shot) {
  const go = async (name, wait) => { await page.evaluate((n) => { window.__meta.hideBar(); window.__meta.show(n); }, name); await page.waitForTimeout(wait); };
  await go('departure-app', 1400);
  const total = () => page.evaluate(() => document.querySelector('.ocbg-meta-total').textContent);
  console.log('total before', await total());
  // Tab until the focused element is a ＋ button
  for (let i = 0; i < 40; i++) {
    await page.keyboard.press('Tab');
    const label = await page.evaluate(() => document.activeElement && document.activeElement.getAttribute('aria-label'));
    if (label === '＋') break;
  }
  await page.keyboard.press('Enter');
  await page.waitForTimeout(500);
  console.log('total after Tab+Enter', await total(), 'step still deck:', await page.evaluate(() => getComputedStyle(document.querySelectorAll('.ocbg-meta-dep-page')[1]).display === 'none'));
  await shot('tab-enter');
  await page.keyboard.press('Escape');
  await page.waitForTimeout(500);
  console.log('settings open:', await page.evaluate(() => !!document.querySelector('.ocbg-meta-settings')));
  await shot('esc-settings');
  await page.keyboard.press('Escape');
  await page.waitForTimeout(400);
  await page.keyboard.press('j');
  await page.waitForTimeout(500);
  console.log('journal open:', await page.evaluate(() => !!document.querySelector('.ocbg-meta-journal')));
  await shot('j-journal');
}
