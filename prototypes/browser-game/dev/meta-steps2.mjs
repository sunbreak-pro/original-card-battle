// Interactions on the meta dev page (dev only): the departure's supplies step, the journal's terms tab,
// the owned cards' detail of a stance card, the title's leave.
export default async function (page, shot, S) {
  const go = async (name, wait) => { await page.evaluate((n) => { window.__meta.hideBar(); window.__meta.show(n); }, name); await page.waitForTimeout(wait); };
  const click = async (x, y) => { const [cx, cy] = await S(x, y); await page.mouse.click(cx, cy); };
  // deck: add three cards so the deck passes, filter attack, then go on to the supplies
  await go('departure', 1200);
  for (let i = 0; i < 2; i++) await click(64 + 4 * 190 + 13 + 156 - 20, 214 + 0 * 266 + 244); // + on 横薙ぎ
  await page.waitForTimeout(400);
  await shot('deck-valid');
  await page.keyboard.press('Enter');
  await page.waitForTimeout(900);
  await click(64 + 0 * 580 + 560 - 24, 262 + 2 * 90 + 41); // + on a tool (row 2, left)
  await click(64 + 1 * 580 + 560 - 24, 262 + 3 * 90 + 41); // + on a tool (row 3, right)
  await click(64 + 0 * 580 + 560 - 24, 752 + 0 * 90 + 41); // + on a consumable
  await click(1236 + 100, 212 + 21); // the first talent
  await page.waitForTimeout(600);
  await shot('supplies');
  // journal: the terms tab
  await go('journal-explore', 900);
  await click(1920 - 576 + 28 + 3 * 132 + 60, 108);
  await page.waitForTimeout(500);
  await shot('journal-terms');
  // owned: a stance card's lasting rows
  await go('owned', 900);
  await click(40 + 28 + 3 * 184 + 88, 36 + 92 + 2 * 274 + 100);
  await page.waitForTimeout(500);
  await shot('owned-stance');
}
