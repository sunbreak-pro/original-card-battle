// The battle scene's playtest: plays whole battles in dev/out/battle.html through real mouse and keyboard
// input — a card is held and carried onto its zone (an enemy's receiver, or above the throw line), selected
// with a click or a digit and played with a click or Enter, refused cards are released on purpose, turns are
// ended with X or the plate (battle_ui_ux_v2 §3.4: Enter and Space never end the turn), once with the pile
// list open — at speed ×1.5, holding Space or the pointer to fast-forward the enemy's turn. After every action, once the scene waits again, it checks that what the screen shows (HP,
// Guard, stamina, the hand, what each pile holds, the omens, the turn, the gap, where the figures stand,
// the dark cards) equals the source's state (window.__battle.check(), dev/battle-read.js), and that the
// console has no error.
//
// Which card to play comes from the ported GreedyPlayer (window.__battle.choose()); the test only decides
// how to put it in (drag / click / key), and the scene and the source do the rest.
//
// 俊敏's free step (battle_ui_ux_v2 §3.8; the source's JS extension): some battles open with ?swift=n (dev: 俊敏 on
// the player from turn 1). While the choice waits, a card is tried once (the refusal line, nothing taken), then the
// greedy player's step (window.__battle.chooseStep()) is chosen by a click on its mark or by ← / → and Enter.
// The 3-enemy line (ids=…, 8 cells) runs with the 2-enemy ones.
//
// Build first:  node tools/devbuild.mjs dev/battle.js
// Run:          node dev/battle-autoplay.mjs [--only <enemyId|pair>] [--shots dev/shots/autoplay] [--max-turns 40]
// Output:       a line per battle, dev/shots/autoplay/report.json, a result screenshot per battle.
import { chromium } from '/opt/node22/lib/node_modules/playwright/index.mjs';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const G = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const arg = (k, d) => { const i = process.argv.indexOf(k); return i >= 0 ? process.argv[i + 1] : d; };
const ONLY = arg('--only', '');
const SHOTS = path.resolve(G, arg('--shots', 'dev/shots/autoplay'));
const MAX_TURNS = Number(arg('--max-turns', 40));
const SPEED = arg('--speed', '1.5');
const PAGE = path.join(G, 'dev/out/battle.html');
fs.mkdirSync(SHOTS, { recursive: true });

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const browser = await chromium.launch({ proxy: process.env.HTTPS_PROXY ? { server: process.env.HTTPS_PROXY } : undefined });

// ---- the roster, read from the page itself (Enemies.All, as the picker lists it) ----------------------------
async function roster() {
  const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });
  await page.goto('file://' + PAGE + '?bar=0&mute=1&enemy=__none__');
  await page.waitForFunction(() => window.__battle && window.__battle.roster && window.__battle.roster.length, null, { timeout: 20000 });
  const list = await page.evaluate(() => window.__battle.roster.map((r) => ({ id: r.id, rank: r.rank, size: r.size })));
  await page.close();
  return list;
}

const all = await roster();
const LAYERS = [1, 2, 3, 4, 5, 6, 7];
const CELLS = [6, 7, 5, 8];
// Normal enemies fall to the demo deck in a turn or two; there the greedy player is held to 2 cards a turn
// (cap), so each fight also runs a few enemy turns (omens, moves, pushes, walls).
let battles = all.map((r, i) => ({ name: r.id, enemy: r.id, rank: r.rank, layer: LAYERS[i % LAYERS.length], cells: Math.max(r.size >= 2 ? 6 : 5, CELLS[i % CELLS.length]), seed: 20261020 + i, pair: false, cap: r.rank === 'Normal' ? 2 : 0, swift: i % 3 === 2 ? 3 : 0 }));
// LayerEncounters' pair (錆槍＋灰弩, roster §9): two enemies, 7 cells, on layer 2 (where the exploration hands it in).
if (battles.length) battles[0].chrome = true;
battles.push({ name: 'pair', enemy: 'polearm_warped', rank: 'Elite', layer: 2, cells: 7, seed: 20261099, pair: true, cap: 2, chrome: true });
battles.push({ name: 'pair-8', enemy: 'polearm_warped', rank: 'Elite', layer: 3, cells: 8, seed: 20261100, pair: true, cap: 0, swift: 2 });
// Three enemies (the JS writer's extension, battle_ui_ux_v2 §3.9): 錆槍, 灰弩 and the shadow hound on 8 cells.
battles.push({ name: 'trio', enemy: 'polearm_warped', rank: 'Elite', layer: 5, cells: 8, seed: 20261101, pair: true, ids: 'polearm_warped,crossbow_hunter,shadow_hound', cap: 0, swift: 2 });
if (ONLY) battles = battles.filter((b) => b.name === ONLY || b.enemy === ONLY);

// ---- one battle ---------------------------------------------------------------------------------------
async function play(b, index) {
  const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });
  const errors = [];
  page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
  page.on('console', (m) => { if (m.type() === 'error') errors.push('console: ' + m.text()); });
  const q = new URLSearchParams({ enemy: b.enemy, layer: String(b.layer), cells: String(b.cells), seed: String(b.seed), speed: SPEED, bar: '0', mute: '1' });
  if (b.pair) q.set('pair', '1');
  if (b.ids) q.set('ids', b.ids);
  if (b.swift) q.set('swift', String(b.swift));
  await page.goto('file://' + PAGE + '?' + q.toString());
  await page.waitForFunction(() => window.__battle && window.__battle.scene && window.__battle.scene.player && window.__battle.scene.player.state !== 'opening', null, { timeout: 30000 });

  const S = async (x, y) => page.evaluate(([x, y]) => {
    const st = document.querySelector('.ocbg-stage') || document.querySelector('.ocbg-battle');
    const r = st.getBoundingClientRect();
    const k = r.width / 1920;
    return [r.left + x * k, r.top + y * k];
  }, [x, y]);
  const stateOf = () => page.evaluate(() => window.__battle.scene.player.state);
  const ev = (fn, a) => page.evaluate(fn, a);

  const log = { name: b.name, layer: b.layer, cells: b.cells, actions: 0, plays: 0, refusals: 0, turnsEnded: 0, backTargets: 0, mismatches: [], outcome: null, ok: false, notes: [] };
  const mismatch = (where, list) => { for (const m of list) if (log.mismatches.length < 40) log.mismatches.push(where + ': ' + m); };

  // ---- fast-forward: hold Space, or the pointer on the sky, while the flow runs by itself --------------
  let ff = null;
  const ffMode = index % 2 === 0 ? 'space' : 'pointer';
  async function ffOn() {
    if (ff) return;
    if (ffMode === 'space') { await page.keyboard.down(' '); ff = 'space'; } else {
      const [x, y] = await S(960, 160);
      await page.mouse.move(x, y);
      await page.mouse.down();
      ff = 'pointer';
    }
  }
  async function ffOff() {
    if (!ff) return;
    if (ff === 'space') await page.keyboard.up(' '); else await page.mouse.up();
    ff = null;
  }
  /** Waits (fast-forwarding) until the scene waits for the player again, or the battle is over. */
  async function settle(limitMs = 60000) {
    const t0 = Date.now();
    for (;;) {
      const s = await stateOf();
      if (s === 'idle' || s === 'over' || s === 'halted') { await ffOff(); return s; }
      if (s === 'auto' || s === 'resolving') await ffOn();
      if (Date.now() - t0 > limitMs) { await ffOff(); return 'timeout'; }
      await sleep(40);
    }
  }
  /** The screen against the source once the player waits (the dark cards fade in over a few frames). */
  async function verify(where) {
    await sleep(350);
    const list = await ev(() => window.__battle.check());
    if (list.length) mismatch(where, list);
    return list;
  }

  // ---- input paths ----------------------------------------------------------------------------------
  async function cardPoint(id) {
    return ev((id) => {
      const g = window.__battle.scene.env.geom.handCard(id);
      return g ? [g.x, g.y] : null;
    }, id);
  }
  async function targetPoint(id, unit) {
    return ev(([id, unit]) => {
      const sc = window.__battle.scene;
      const face = sc.modules.hand.faces().find((f) => f.Id === id);
      if (!face) return null;
      if (face.Aim === 'Single') {
        const r = sc.env.geom.receiver(unit >= 0 ? unit : undefined);
        return r ? [r.X + r.Width / 2, r.Y + r.Height / 2, 'Single'] : null;
      }
      return [960, 520, 'Self'];
    }, [id, unit]);
  }
  /** Hold the card, carry it onto its zone in small moves, let go. */
  async function drag(id, unit, to) {
    const at = await cardPoint(id);
    if (!at) return false;
    // The card's lower half: still on it once it has risen to its hover place (top at y 760).
    let [x, y] = await S(at[0], Math.min(at[1] + 10, 1000));
    await page.mouse.move(x, y, { steps: 3 });
    await sleep(120);
    const lifted = (await cardPoint(id)) || at;
    [x, y] = await S(lifted[0], lifted[1]);
    await page.mouse.move(x, y, { steps: 2 });
    await page.mouse.down();
    const dest = to || (await targetPoint(id, unit));
    if (!dest) { await page.mouse.up(); return false; }
    const [tx, ty] = await S(dest[0], dest[1]);
    await page.mouse.move(x + 4, y - 14, { steps: 2 });
    await page.mouse.move(tx, ty, { steps: 12 });
    await sleep(90);
    await page.mouse.up();
    return true;
  }
  /** Click the card (it rises), then click its zone. */
  async function clickPlay(id, unit) {
    const at = await cardPoint(id);
    const dest = await targetPoint(id, unit);
    if (!at || !dest) return false;
    const [x, y] = await S(at[0], Math.min(at[1] + 10, 1000));
    await page.mouse.move(x, y, { steps: 3 });
    await sleep(120);
    await page.mouse.click(x, y);
    await sleep(120);
    const [tx, ty] = await S(dest[0], dest[1]);
    await page.mouse.move(tx, ty, { steps: 6 });
    await sleep(60);
    await page.mouse.click(tx, ty);
    return true;
  }
  /** A digit selects the card (Tab moves it to the named enemy when needed), Enter plays it. */
  async function keyPlay(id, unit) {
    const i = await ev((id) => window.__battle.scene.modules.hand.faces().findIndex((f) => f.Id === id), id);
    if (i < 0 || i > 8) return false;
    // Enter auto-selects the next card after a play (§3.4); Esc drops it so the digit starts clean.
    if (await ev(() => window.__battle.scene.modules.hand.hasSelection())) { await page.keyboard.press('Escape'); await sleep(60); }
    await page.keyboard.press(String(i + 1));
    await sleep(140);
    if (unit != null && unit >= 0) {
      for (let k = 0; k < 3; k++) {
        const framed = await ev(() => window.__battle.scene.modules.hand.framedUnit());
        if (framed === unit) break;
        await page.keyboard.press('Tab');
        await sleep(60);
      }
    }
    await page.keyboard.press('Enter');
    return true;
  }
  async function endTurn(n) {
    const how = ['x', 'plate', 'drawer+plate', 'x'][n % 4];
    // The plate and X are not taken in the turn's first 0.5 s (player.requestEndTurn's guard, review 16).
    await sleep(250);
    if (how === 'drawer+plate') {
      // The pile list does not hold the turn (Unity): open the discard list, end the turn on the plate.
      const pile = await ev(() => window.__battle.scene.env.geom.pileMiddle('Discard'));
      const [px, py] = await S(pile.x, pile.y);
      await page.mouse.move(px, py, { steps: 3 });
      await page.mouse.click(px, py);
      await sleep(260);
      if (!(await ev(() => window.__battle.scene.modules.hud.drawerOpen()))) log.notes.push('the discard pile did not open its list');
      else {
        // Review 16: with the list open a card is not taken, and a press on one says why (the refusal line), as the guide does.
        const card = await ev(() => { const f = window.__battle.scene.modules.hand.faces(); return f.length ? f[f.length - 1].Id : null; });
        const at = card ? await cardPoint(card) : null;
        if (at && at[0] > 640) {
          const [cx, cy] = await S(at[0], Math.min(at[1] + 10, 1000));
          await page.mouse.move(cx, cy, { steps: 3 });
          await sleep(120);
          await page.mouse.down();
          const seen = await ev(() => ({ refusal: window.__battle.read().refusal, guide: window.__battle.read().guide, held: window.__battle.scene.modules.hand.isHolding() }));
          await page.mouse.up();
          if (!/一覧を閉じると/.test(seen.refusal || '')) mismatch('drawer', ['a card pressed with the list open showed ' + JSON.stringify(seen.refusal)]);
          if (seen.held) mismatch('drawer', ['a card was taken with the list open']);
          log.drawerPress = (log.drawerPress || 0) + 1;
        }
      }
    }
    if (how === 'plate' || how === 'drawer+plate') {
      const box = await ev(() => window.__battle.scene.env.geom.endTurnBox());
      const [x, y] = await S(box.x + box.w / 2, box.y + box.h / 2);
      await page.mouse.move(x, y, { steps: 4 });
      await page.mouse.click(x, y);
    } else await page.keyboard.press(how);
    return how;
  }

  /**
   * The chrome through real input, once per battle that asks for it: J opens the journal and Esc closes it,
   * a click on a pile opens its list and Esc closes it, the speed switch cycles ×1.5 → ×1.0 → ×1.25 → ×1.5,
   * and Esc with nothing open does nothing: no settings in a battle (battle-visual-v1 §4.1), so no gear and no
   * ctx.onSettings call unless the app asks for them with ctx.settingsInBattle (dev page ?settingsInBattle=1).
   */
  async function chrome() {
    const fail = [];
    const j0 = await ev(() => window.__battle.journalOpen);
    await page.keyboard.press('j');
    await sleep(200);
    if ((await ev(() => window.__battle.journalOpen)) !== j0 + 1 || !(await ev(() => !!document.querySelector('.ocbg-battle-journal')))) fail.push('J did not open the journal');
    await page.keyboard.press('Escape');
    await sleep(200);
    if (await ev(() => !!document.querySelector('.ocbg-battle-journal'))) fail.push('Esc did not close the journal');
    const pile = await ev(() => window.__battle.scene.env.geom.pileMiddle('Draw'));
    const [px, py] = await S(pile.x, pile.y);
    await page.mouse.move(px, py, { steps: 4 });
    await page.mouse.click(px, py);
    await sleep(400);
    if (!(await ev(() => window.__battle.scene.modules.hud.drawerOpen()))) fail.push('a click on the draw pile did not open its list');
    const rows = await ev(() => document.querySelectorAll('.ocbg-battle-drawer-row').length);
    const kinds = await ev(() => new Set(window.__battle.source.State.DrawPile.map((c) => c.Def.Id)).size);
    if (rows !== kinds) fail.push('the draw list shows ' + rows + ' rows for ' + kinds + ' kinds of card');
    await page.keyboard.press('Escape');
    await sleep(400);
    if (await ev(() => window.__battle.scene.modules.hud.drawerOpen())) fail.push('Esc did not close the pile list');
    const speeds = [];
    for (let k = 0; k < 3; k++) {
      const box = await ev(() => { const r = document.querySelector('.ocbg-battle-speed').getBoundingClientRect(); return [r.left + r.width / 2, r.top + r.height / 2]; });
      await page.mouse.move(box[0], box[1], { steps: 3 });
      await page.mouse.click(box[0], box[1]);
      await sleep(120);
      speeds.push(await ev(() => window.__battle.scene.env.settings.speed));
    }
    if (speeds.join() !== '1,1.25,1.5') fail.push('the speed switch went ' + speeds.join(' → '));
    await page.mouse.move(...(await S(960, 300)), { steps: 3 });
    const s0 = await ev(() => window.__battle.settingsCalls);
    const optIn = await ev(() => new URLSearchParams(location.search).get('settingsInBattle') === '1');
    const gear = await ev(() => !!document.querySelector('.ocbg-battle-settingsbtn'));
    if (gear !== optIn) fail.push(optIn ? 'no settings gear although the app asked for one' : 'a settings gear in the battle (battle-visual-v1 §4.1)');
    await page.keyboard.press('Escape');
    await sleep(120);
    const s1 = await ev(() => window.__battle.settingsCalls);
    if (optIn && s1 !== s0 + 1) fail.push('Esc with nothing open did not reach onSettings (?settingsInBattle=1)');
    if (!optIn && s1 !== s0) fail.push('Esc with nothing open reached onSettings in a battle (battle-visual-v1 §4.1)');
    log.chrome = fail.length ? fail : 'ok';
    if (fail.length) mismatch('chrome', fail);
  }

  // ---- 俊敏's free step (battle_ui_ux_v2 §3.8) ------------------------------------------------------------
  let triedLocked = false;
  async function freeStep(turn) {
    log.freeSteps = (log.freeSteps || 0) + 1;
    if (!triedLocked) {
      // A card tried while the choice waits: refused with the line, nothing taken.
      triedLocked = true;
      const before = await ev(() => JSON.stringify(window.__battle.read().hand));
      const first = await ev(() => { const f = window.__battle.scene.modules.hand.faces()[0]; return f ? f.Id : null; });
      if (first) {
        // Review 16: the line is read right after the press (the scene puts it up on the press); the 3.5 s it stands are
        // real time, and a slow drag on a loaded headless page outlasted them before the old check read it.
        const at = await cardPoint(first);
        let [x, y] = await S(at[0], Math.min(at[1] + 10, 1000));
        await page.mouse.move(x, y, { steps: 3 });
        await sleep(120);
        await page.mouse.down();
        const line = await ev(() => window.__battle.read().refusal);
        await page.mouse.move(x + 4, y - 40, { steps: 3 });
        [x, y] = await S(960, 400);
        await page.mouse.move(x, y, { steps: 4 });
        await page.mouse.up();
        await sleep(150);
        if (!/^先に/.test(line || '')) mismatch('free step', ['a card tried during the choice showed ' + JSON.stringify(line)]);
        if ((await ev(() => JSON.stringify(window.__battle.read().hand))) !== before) mismatch('free step', ['a card tried during the choice changed the hand']);
        if (await ev(() => window.__battle.scene.modules.hand.isHolding())) mismatch('free step', ['a card was taken during the choice']);
        if (!(await ev(() => window.__battle.scene.player.freeStepPending))) mismatch('free step', ['the choice went away when a card was tried']);
      }
    }
    const dir = await ev(() => window.__battle.chooseStep());
    const ways = await ev(() => window.__battle.scene.player.freeStepWays);
    const cellBefore = await ev(() => window.__battle.source.State.Player.Cell);
    const byKeys = dir !== 0 && log.freeSteps % 2 === 0;
    if (byKeys) {
      await page.keyboard.press(dir < 0 ? 'ArrowLeft' : 'ArrowRight');
      await sleep(60);
      const focus = await ev(() => window.__battle.scene.modules.field.freeStepFocus);
      if (focus !== dir) mismatch('free step', ['the key focused ' + focus + ', wanted ' + dir]);
      await page.keyboard.press('Enter');
    } else {
      const c = await ev((d) => window.__battle.scene.modules.field.freeStepCentre(d), dir);
      if (!c) { mismatch('free step', ['no mark for way ' + dir + ' (ways ' + JSON.stringify(ways) + ')']); await page.keyboard.press('x'); return; }
      const [x, y] = await S(c.x, c.y);
      await page.mouse.move(x, y, { steps: 4 });
      await sleep(60);
      await page.mouse.click(x, y);
    }
    await sleep(40);
    const s2 = await settle();
    if (s2 === 'over') return;
    if (await ev(() => window.__battle.scene.player.freeStepPending)) mismatch('free step', ['the choice stayed up after choosing ' + dir]);
    const cellAfter = await ev(() => window.__battle.source.State.Player.Cell);
    log.steps = (log.steps || []).concat([turn + ':' + dir + (dir ? ' ' + cellBefore + '→' + cellAfter : '') + (byKeys ? ' keys' : ' click')]);
    await verify('turn ' + turn + ' after the free step ' + dir);
  }

  // ---- the battle ------------------------------------------------------------------------------------
  const t0 = Date.now();
  let played = 0;
  let lastTurn = -1;
  let n = 0;
  let refusalsTried = 0;
  for (;;) {
    const s = await settle();
    if (s === 'timeout') { log.notes.push('the flow did not settle in 60 s'); break; }
    if (s === 'halted') { log.notes.push('the scene halted (the source threw)'); break; }
    if (s === 'over') break;
    if (Date.now() - t0 > 8 * 60000) { log.notes.push('over 8 minutes'); break; }
    const turn = await ev(() => window.__battle.source.Frame.Corner.Turn);
    if (turn !== lastTurn) { played = 0; lastTurn = turn; }
    await verify('turn ' + turn + ' before action ' + n);
    if (b.chrome && n === 0) { await chrome(); await verify('after the chrome'); }
    n += 1;
    log.actions += 1;

    // 俊敏's choice first: no card is taken until it is made (§3.8).
    if (await ev(() => window.__battle.scene.player.freeStepPending)) {
      await freeStep(turn);
      continue;
    }

    // Now and then a card the source would refuse is released on its zone: the refusal line, the card home, nothing changed.
    const refusable = await ev(() => {
      const src = window.__battle.source;
      const r = window.__battle.read();
      const id = r.dimmed[0];
      return id ? { id, verdict: src.Inspect(id), text: src.RefusalText(id, src.Inspect(id)) } : null;
    });
    if (refusable && refusalsTried < 3 && n % 3 === 1) {
      refusalsTried += 1;
      const before = await ev(() => JSON.stringify(window.__battle.read()));
      await drag(refusable.id, -1, null);
      await sleep(150);
      const line = await ev(() => window.__battle.read().refusal);
      if (refusable.text && line !== refusable.text) mismatch('refusal of ' + refusable.id, ['refusal line ' + JSON.stringify(line) + ', source ' + JSON.stringify(refusable.text)]);
      const s2 = await settle();
      if (s2 !== 'idle') { log.notes.push('after a refusal the scene is ' + s2); if (s2 === 'over') break; }
      const after = JSON.parse(await ev(() => JSON.stringify(window.__battle.read())));
      const b0 = JSON.parse(before);
      if (after.hand.join() !== b0.hand.join() || after.player.hp !== b0.player.hp || after.player.stamina !== b0.player.stamina) mismatch('refusal of ' + refusable.id, ['the screen changed after a refused card']);
      await verify('turn ' + turn + ' after refusal');
      log.refusals += 1;
      continue;
    }

    const choice = turn > MAX_TURNS ? null : await ev(([p, cap]) => window.__battle.choose(p, cap), [played, b.cap || 0]);
    if (choice) {
      const multi = await ev(() => window.__battle.source.State.Enemies.length > 1);
      const defaultTarget = await ev((id) => { const f = window.__battle.source.Frame.Hand.find((x) => x.Id === id); return f ? f.TargetUnit : -1; }, choice.id);
      const way = n % 5;
      let how;
      if (way === 3 || (way === 4 && multi && choice.unit >= 0 && choice.unit !== defaultTarget)) { how = 'key'; await keyPlay(choice.id, choice.unit); }
      else if (way === 1) { how = 'click'; await clickPlay(choice.id, choice.unit); }
      else { how = 'drag'; await drag(choice.id, choice.unit, null); }
      await sleep(30);
      const s2 = await stateOf();
      if (s2 === 'idle') {
        // Not taken: the source refused it, or the input missed. The screen must still be true.
        const r = await ev(() => window.__battle.read());
        log.notes.push('turn ' + turn + ': ' + how + ' of ' + choice.id + ' left the scene idle' + (r.refusal ? ' (refused: ' + r.refusal + ')' : ''));
        await settle();
        await verify('turn ' + turn + ' after a missed play');
        // Fall back to the keyboard path so the battle goes on.
        await keyPlay(choice.id, choice.unit);
      }
      played += 1;
      log.plays += 1;
      if (choice.unit > 0) log.backTargets = (log.backTargets || 0) + 1;
    } else {
      await endTurn(log.turnsEnded);
      log.turnsEnded += 1;
      await sleep(30);
      const s2 = await stateOf();
      if (s2 === 'idle') { log.notes.push('turn ' + turn + ': end turn (' + (['x', 'plate', 'drawer+plate', 'x'][(log.turnsEnded - 1) % 4]) + ') left the scene idle'); await page.keyboard.press('Escape'); await page.keyboard.press('x'); }
      if (await ev(() => window.__battle.scene.modules.hud.drawerOpen())) await page.keyboard.press('Escape');
    }
  }

  // ---- the end: the result card, onFinished once ----------------------------------------------------
  let shotTaken = false;
  const final = await stateOf();
  if (final === 'over') {
    // Review 21: a loss ends on the result screen (battle_ui_ux_v2 §2.5 / §5.19), which waits for 「手記へ」: the test reads it
    // and presses the button as a player would. A win goes on by itself after its paper.
    // The function answers a word, not a boolean: waitForFunction waits for a truthy answer.
    const paper = await page.waitForFunction(() => {
      const p = document.querySelector('.ocbg-battle-result-paper');
      return p && Number(getComputedStyle(document.querySelector('.ocbg-battle-result')).opacity) > 0.95 ? (p.classList.contains('is-lost') ? 'lost' : 'won') : null;
    }, null, { timeout: 20000 }).then((h) => h.jsonValue()).catch(() => { log.notes.push('no result paper'); return null; });
    if (paper === 'won') {
      log.paper = await ev(() => document.querySelector('.ocbg-battle-result-paper').innerText.replace(/\n+/g, ' / '));
      await shotResult();
    }
    if (paper === 'lost') {
      await sleep(1200);
      log.paper = await ev(() => document.querySelector('.ocbg-battle-result-paper').innerText.replace(/\n+/g, ' / '));
      if (await ev(() => window.__battle.finished)) log.notes.push('onFinished came before 手記へ');
      await shotResult();
      const btn = await ev(() => { const r = document.querySelector('.ocbg-battle-result-button').getBoundingClientRect(); return [r.x + r.width / 2, r.y + r.height / 2]; });
      await page.mouse.move(btn[0], btn[1], { steps: 3 });
      await page.mouse.click(btn[0], btn[1]);
    }
    await page.waitForFunction(() => window.__battle.finished, null, { timeout: 15000 }).catch(() => log.notes.push('onFinished was not called'));
    await verify('over');
    const end = await ev(() => ({ outcome: window.__battle.outcome, calls: window.__battle.finishedCalls, result: window.__battle.read().result, turn: window.__battle.source.Frame.Corner.Turn }));
    await sleep(1500);
    const calls = await ev(() => window.__battle.finishedCalls);
    if (calls !== 1) log.notes.push('onFinished called ' + calls + ' times');
    log.outcome = end.outcome;
    log.turns = end.turn;
    log.result = end.result;
  }
  async function shotResult() { await page.screenshot({ path: path.join(SHOTS, String(index + 1).padStart(2, '0') + '-' + b.name + '.png') }); shotTaken = true; }
  if (!shotTaken) await shotResult();
  log.errors = errors.slice(0, 20);
  log.seconds = Math.round((Date.now() - t0) / 1000);
  log.ok = final === 'over' && log.mismatches.length === 0 && errors.length === 0 && !log.notes.some((x) => /onFinished|halted|settle|minutes/.test(x));
  await page.close();
  return log;
}

const results = [];
for (let i = 0; i < battles.length; i++) {
  const b = battles[i];
  let r;
  try { r = await play(b, i); } catch (err) { r = { name: b.name, ok: false, notes: ['the test threw: ' + (err && err.message)], mismatches: [], errors: [] }; }
  results.push(r);
  console.log((r.ok ? 'PASS ' : 'FAIL ') + r.name + ' (layer ' + b.layer + ', ' + b.cells + ' cells' + (b.cap ? ', ≤' + b.cap + ' cards a turn' : '') + '): ' + (r.outcome || '-') + ', turns ' + (r.turns || '-') + ', plays ' + (r.plays || 0) + ', refusals ' + (r.refusals || 0) + (r.backTargets ? ', on the back enemy ' + r.backTargets : '') + (r.chrome ? ', chrome ' + (r.chrome === 'ok' ? 'ok' : 'FAIL') : '') + ', ' + (r.seconds || 0) + ' s'
    + (r.steps ? '\n    free steps: ' + r.steps.join(', ') : '')
    + (r.mismatches.length ? '\n    mismatches: ' + r.mismatches.slice(0, 8).join('\n                ') : '')
    + (r.errors && r.errors.length ? '\n    errors: ' + r.errors.slice(0, 5).join('\n            ') : '')
    + (r.notes && r.notes.length ? '\n    notes: ' + r.notes.slice(0, 6).join('\n           ') : ''));
}
const passed = results.filter((r) => r.ok).length;
fs.writeFileSync(path.join(SHOTS, 'report.json'), JSON.stringify({ played: results.length, passed, results }, null, 2));
console.log('battles played ' + results.length + ', passed ' + passed);
await browser.close();
