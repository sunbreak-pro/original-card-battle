// Review pass of the meta screens (dev only): every screen, the contexts src/app passes today, the
// keyboard paths and the mid-animation moments. META_ONLY=a,b limits the run to some groups.
// node tools/shot.mjs dev/out/meta.html dev/shots/rv/f --steps dev/meta-review-steps.mjs
export default async function (page, shot, S) {
  const only = (process.env.META_ONLY || '').split(',').filter(Boolean);
  const want = (g) => !only.length || only.includes(g);
  const go = async (name, wait) => { await page.evaluate((n) => { window.__meta.hideBar(); window.__meta.show(n); }, name); await page.waitForTimeout(wait); };
  const click = async (x, y) => { const [cx, cy] = await S(x, y); await page.mouse.click(cx, cy); };
  const hover = async (x, y) => { const [cx, cy] = await S(x, y); await page.mouse.move(cx, cy); };
  const key = async (k, n = 1) => { for (let i = 0; i < n; i++) { await page.keyboard.press(k); await page.waitForTimeout(90); } };

  if (want('screens')) {
    for (const [name, wait] of [['title', 3800], ['title-first', 3800], ['hall', 4600], ['hall-first', 4200], ['hall-many', 4600],
      ['departure', 1400], ['ending-death', 7500], ['ending-alive', 7500], ['ending-miasma', 7500], ['settings', 1200], ['owned', 1200],
      ['journal-explore', 1000], ['journal-battle', 1000], ['journal-enemies', 1000]]) {
      await go(name, wait);
      await shot(name);
    }
  }
  if (want('app')) {
    await go('hall-app', 4600); await shot('hall-app');
    await go('departure-app', 1400); await shot('departure-app');
    await go('owned-app', 1200); await shot('owned-app');
    await go('journal-app', 1000); await shot('journal-app-memo');
    // type into the memo: the journal's keys (J closes) must not fire while writing
    await click(1920 - 576 + 200, 600);
    await page.keyboard.type(' Jを押しても閉じない');
    await page.waitForTimeout(700);
    await shot('journal-app-typed');
    await key('Escape');
    await page.waitForTimeout(400);
    await go('journal-app-battle', 1000); await shot('journal-app-battle');
    await go('settings-nosave', 1000); await shot('settings-nosave');
  }
  if (want('keys')) {
    // deck: arrows walk the cursor, + adds, PageDown pages, Enter goes on once valid
    await go('departure', 1400);
    await key('ArrowRight', 4); await key('+'); await key('+');
    await key('ArrowDown'); await key('+');
    await page.waitForTimeout(400);
    await shot('deck-keys');
    await key('ArrowRight', 3);
    await page.waitForTimeout(400);
    await shot('deck-keys-page2');
    await key('Enter');
    await page.waitForTimeout(900);
    await shot('supplies-keys');
    // owned: the cursor crosses a page
    await go('owned', 1000);
    await key('ArrowDown', 2); await key('ArrowRight', 6);
    await page.waitForTimeout(500);
    await shot('owned-keys');
  }
  if (want('moments')) {
    await go('title', 3800);
    await hover(300, 500);
    await page.waitForTimeout(400);
    await shot('title-hover-new');
    await go('hall', 1900); await shot('hall-naming');
    await go('ending-death', 2600); await shot('ending-writing');
    await click(200, 900); await page.waitForTimeout(300); await shot('ending-skipped');
    await go('departure', 1400);
    await hover(64 + 2 * 190 + 90, 214 + 100);
    await page.waitForTimeout(300);
    await shot('deck-hover');
  }
  if (want('small')) {
    await page.setViewportSize({ width: 1280, height: 720 });
    for (const [name, wait] of [['title', 3800], ['hall-many', 4600], ['departure-app', 1400], ['owned', 1200], ['journal-app', 1000], ['ending-miasma', 7500], ['settings', 1200]]) {
      await go(name, wait);
      await shot('720-' + name);
    }
  }
}
