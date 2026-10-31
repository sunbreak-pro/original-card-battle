// Battle-scene helpers: read the mounted scene (window.__scene, read only) and drive it with real input.
import { sleep } from './lib.mjs';

export function battleKit(page) {
  const ev = (fn, a) => page.evaluate(fn, a);
  const state = () => ev(() => (window.__scene && window.__scene.player ? window.__scene.player.state : null));
  let ff = false;
  async function ffOn() { if (!ff) { await page.keyboard.down(' '); ff = true; } }
  async function ffOff() { if (ff) { await page.keyboard.up(' '); ff = false; } }
  /** Waits until the scene asks the player (idle) or the fight is over. `fast`: hold Space meanwhile. */
  async function waitIdle({ fast = false, timeout = 90000 } = {}) {
    const t0 = Date.now();
    for (;;) {
      const s = await state();
      if (s === 'idle' || s === 'over' || s === 'halted') { await ffOff(); return s; }
      if (s === null) { await ffOff(); return null; }
      if (fast && (s === 'auto' || s === 'resolving')) await ffOn();
      if (Date.now() - t0 > timeout) { await ffOff(); return 'timeout'; }
      await sleep(40);
    }
  }
  const faces = () => ev(() => {
    const sc = window.__scene;
    return sc.modules.hand.faces().map((f) => ({ id: f.Id, name: f.Name, aim: f.Aim, kind: f.Kind, cost: f.Cost, playable: sc.player.isPlayable(f.Id), reach: f.ReachHint || '', range: f.RequiredRangeGlyph, trait: f.TraitText || '', hover: f.HoverLine || '' }));
  });
  const cardPos = (id) => ev((id) => window.__scene.env.geom.handCard(id), id);
  async function hoverCard(id) {
    const p = await cardPos(id);
    if (!p) return null;
    await page.mouse.move(p.x, Math.min(p.y + 10, 1000), { steps: 4 });
    await sleep(320);
    return cardPos(id);
  }
  async function holdCard(id) {
    const lifted = await hoverCard(id);
    if (!lifted) return false;
    await page.mouse.move(lifted.x, lifted.y, { steps: 2 });
    await page.mouse.down();
    await page.mouse.move(lifted.x + 4, lifted.y - 16, { steps: 2 });
    return true;
  }
  const receiver = (unit = 0) => ev((u) => { const r = window.__scene.env.geom.receivers().find((x) => x.unit === u) || window.__scene.env.geom.receivers()[0]; return r ? { x: r.box.X + r.box.Width / 2, y: r.box.Y + r.box.Height / 2 } : null; }, unit);
  async function carry(x, y, steps = 14) { await page.mouse.move(x, y, { steps }); await sleep(120); }
  /** Plays a card by drag (onto the enemy's receiver, or above the throw line for a self card). */
  async function dragPlay(id, aim) {
    if (!(await holdCard(id))) return false;
    const dest = aim === 'Single' ? await receiver(0) : { x: 960, y: 470 };
    await carry(dest.x, dest.y);
    await page.mouse.up();
    return leftIdle(1500);
  }
  /** A settings overlay left open by a stray Esc: close it (its own Esc). */
  async function noOverlay() {
    for (let k = 0; k < 3; k++) {
      const open = await ev(() => !!document.querySelector('.ocbg-meta-settings-panel'));
      if (!open) return;
      await page.keyboard.press('Escape');
      await sleep(500);
    }
  }
  /** After an input: wait (≤ ms) for the scene to leave idle. */
  const leftIdle = (ms = 2500) => page.waitForFunction(() => !window.__scene || !window.__scene.player || window.__scene.player.state !== 'idle', null, { timeout: ms }).then(() => true, () => false);
  async function endTurn() { await noOverlay(); await sleep(300); await page.keyboard.press('x'); return leftIdle(); }
  const frame = () => ev(() => { const f = window.__ocb.fight.Frame; return { turn: f.Corner.Turn, hp: f.Player.Hp, st: f.Player.Stamina, enemies: (f.Enemies || []).map((e) => ({ hp: e.Hp, down: e.Down })), gap: f.Floor ? f.Floor.Gap : null, outcome: f.Outcome }; });
  /** One greedy action: a playable attack, else a playable self card, else end the turn. Returns what it did. */
  async function greedyStep({ attacksOnly = false, noEnd = false } = {}) {
    const fs = await faces();
    const atk = fs.filter((f) => f.playable && f.aim === 'Single').sort((a, b) => b.cost - a.cost)[0];
    const self = attacksOnly ? null : fs.filter((f) => f.playable && f.aim !== 'Single')[0];
    const pick = atk || self;
    if (pick) { await dragPlay(pick.id, pick.aim); return 'play ' + pick.name; }
    if (noEnd) return null;
    await endTurn();
    return 'end';
  }
  return { ev, state, noOverlay, leftIdle, waitIdle, faces, cardPos, hoverCard, holdCard, receiver, carry, dragPlay, endTurn, frame, greedyStep, ffOn, ffOff };
}
