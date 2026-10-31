// Exploration helpers: read the session (window.__ocb, read only) and drive the map with real input.
import { sleep, clickSel, hoverSel, centerOf, startCast, saveFrames, marks, pageNow, shot } from './lib.mjs';

export function exploreKit(page) {
  const ev = (fn, a) => page.evaluate(fn, a);
  /** What is on screen: { scene, busy, plate (class), confirm, interlude, battle } */
  const status = () => ev(() => {
    const root = document.querySelector('.ocbg-explore:not(.ocbg-scene)');
    const battle = !!document.querySelector('.ocbg-battle:not(.ocbg-scene)');
    const ending = !!document.querySelector('.ocbg-meta-ending');
    const hall = !!document.querySelector('.ocbg-meta-hall');
    const plate = document.querySelector('.ocbg-explore-plate:not(.is-leaving)');
    const overlay = !!document.querySelector('.ocbg-overlay-host');
    return {
      scene: root ? 'explore' : battle ? 'battle' : ending ? 'ending' : hall ? 'hall' : 'other',
      busy: root ? root.classList.contains('is-busy') : true,
      plate: plate ? plate.className.replace('ocbg-explore-plate', '').trim() : '',
      confirm: !!document.querySelector('.ocbg-confirm'),
      interlude: root ? root.classList.contains('is-interlude') : false,
      overlay,
    };
  });
  /** Waits until the explore scene is idle (or another scene is up). */
  async function settle(maxMs = 60000) {
    const t0 = Date.now();
    let last = null;
    while (Date.now() - t0 < maxMs) {
      const st = await status();
      last = st;
      if (st.scene !== 'explore' && st.scene !== 'other') return st;
      if (st.scene === 'explore' && (!st.busy || st.plate || st.confirm)) {
        await sleep(450);
        const again = await status();
        if (again.scene !== 'explore' || !again.busy || again.plate || again.confirm) return again;
      }
      await sleep(120);
    }
    return last;
  }
  const info = () => ev(() => {
    const ex = window.__ocb.live && window.__ocb.live.raw;
    if (!ex) return null;
    const s = ex.Session;
    const scr = s.Screen();
    return {
      layer: s.State.Profile.Layer, phase: s.State.Phase, hp: s.State.Hp, marks: s.State.TrainingMarks,
      current: scr.CurrentNodeId, timeLeft: scr.TimeLeft,
      nodes: scr.Nodes.map((n) => ({ id: n.Id, kind: n.Kind, standing: n.Standing, row: n.Row, col: n.Column, opts: n.Options.map((o) => o.Label) })),
    };
  });
  /** Pins a node with a click (a click on the pinned node would step onto it, so a pinned node is left as it is). */
  async function pin(id) {
    const already = await ev((id) => !!document.querySelector(`.ocbg-explore-node.is-pinned[data-node="${id}"]`), id);
    if (already) { await hoverNode(id); return; }
    await clickSel(page, `.ocbg-explore-node[data-node="${id}"]`);
    await sleep(350);
  }
  async function hoverNode(id) {
    await hoverSel(page, `.ocbg-explore-node[data-node="${id}"]`);
    await sleep(350);
  }
  /** Presses the n-th way-in button of the pinned node. */
  async function go(option = 0) {
    const c = await centerOf(page, '.ocbg-explore-go', option);
    if (!c) throw new Error('no way-in button ' + option);
    await page.mouse.move(c[0], c[1], { steps: 3 });
    await page.mouse.click(c[0], c[1]);
  }
  /** Reachable node ids, best first by kind preference (then deepest row). */
  async function reachable(prefer = []) {
    const i = await info();
    const ok = i.nodes.filter((n) => n.standing === 'Reachable' && n.opts.length);
    ok.sort((a, b) => {
      const pa = prefer.indexOf(a.kind); const pb = prefer.indexOf(b.kind);
      const ra = pa < 0 ? 99 : pa; const rb = pb < 0 ? 99 : pb;
      return ra - rb || b.row - a.row || a.col - b.col;
    });
    return ok;
  }
  /** Walks to `id` (pinned first) with option `option`; when `cast` is given, records the walk. */
  async function walkTo(id, option = 0, capture = null) {
    const pinned = await ev((id) => !!document.querySelector(`.ocbg-explore-node.is-pinned[data-node="${id}"]`), id);
    if (!pinned) await pin(id);
    if (!capture) { await go(option); return; }
    const before = await pageNow(page);
    const cast = await startCast(page);
    await sleep(200);
    const t0 = Date.now();
    await go(option);
    await sleep(capture.ms || 2200);
    const got = await cast.stop();
    saveFrames(got, t0, capture.base, capture.at || [0, 100, 200, 300, 450, 600, 800, 1000, 1300, 1700], capture.meta);
    if (capture.after) await capture.after(got, before);
  }
  return { ev, status, settle, info, pin, hoverNode, go, reachable, walkTo };
}

/** Closes a result card: learns the first offered card when `learn`. */
export async function closeResult(page, learn = true) {
  const picks = await page.$$('.ocbg-explore-pick.is-pickable');
  if (picks.length && learn) {
    const b = await picks[0].boundingBox();
    await page.mouse.click(b.x + b.width / 2, b.y + b.height / 2);
    await sleep(250);
    await page.keyboard.press('Enter');
  } else await page.keyboard.press(picks.length ? 'Escape' : 'Enter');
  await sleep(700);
}
