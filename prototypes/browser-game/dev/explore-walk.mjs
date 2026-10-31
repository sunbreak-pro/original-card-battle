// Helpers for the screenshot steps: drive the exploration scene through its own UI.
export const sleep = (page, ms) => page.waitForTimeout(ms);

/** Waits until no step, battle or banner is running, and returns which card (if any) is up. */
export async function settle(page, maxMs = 20000) {
  const t0 = Date.now();
  while (Date.now() - t0 < maxMs) {
    const st = await page.evaluate(() => {
      const root = document.querySelector('.ocbg-explore');
      if (!root) return { busy: true, plate: '', confirm: false, interlude: false }; // between scenes (remount)
      const plate = document.querySelector('.ocbg-explore-plate:not(.is-leaving)');
      const confirm = document.querySelector('.ocbg-confirm');
      const battle = document.querySelector('.dev-battle');
      return {
        busy: root.classList.contains('is-busy') || (battle && battle.style.display === 'flex'),
        plate: plate ? plate.className : '',
        confirm: !!confirm,
        interlude: root.classList.contains('is-interlude'),
      };
    });
    if (!st.busy) { await page.waitForTimeout(350); return st; }
    await page.waitForTimeout(150);
  }
  throw new Error('scene did not settle');
}

/** Closes a result card: learns the first offered card when there is one. */
export async function takeReward(page, learn = true) {
  const cards = await page.$$('.ocbg-explore-pick.is-pickable');
  if (cards.length && learn) { await cards[0].click(); await page.waitForTimeout(150); }
  await page.keyboard.press(cards.length && !learn ? 'Escape' : 'Enter');
  await page.waitForTimeout(500);
}

/** The next node to walk to: a reachable one, deepest row first (or of a wanted kind). */
export async function pickNext(page, wantKind) {
  return page.evaluate((want) => {
    const scr = window.__dev.session.Screen();
    const ok = scr.Nodes.filter((n) => n.Standing === 'Reachable');
    if (!ok.length) return null;
    const pick = (want && ok.find((n) => n.Kind === want)) || ok.sort((a, b) => b.Row - a.Row || a.Column - b.Column)[0];
    return pick.Id;
  }, wantKind || null);
}

/** Pins a node with a click and takes its first (or `option`-th) way in. */
export async function stepTo(page, id, option = 0) {
  const pinned = await page.$(`.ocbg-explore-node.is-pinned[data-node="${id}"]`);
  if (!pinned) { await page.click(`.ocbg-explore-node[data-node="${id}"]`); await page.waitForTimeout(250); }
  const btns = await page.$$('.ocbg-explore-go');
  await btns[option].click();
}
