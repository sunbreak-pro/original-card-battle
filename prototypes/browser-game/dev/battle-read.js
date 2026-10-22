// Dev only: reads what the battle scene shows on screen (its DOM), and compares it with the source's state.
// Never imported from src/. dev/battle.js puts these on window.__battle for the playtest
// (dev/battle-autoplay.mjs): read() → the screen, check() → the mismatches, [] when the screen tells the truth.
//
// What is read is what a player sees: the numbers printed in the panels and the stamina tag, the cards in the
// fan, what each pile stands for (data-count, the count its thickness and hover line come from), the omen
// badges' words and numbers, the corner's turn, the floor's gap number, and where each figure stands.
// What it is compared with: the core's BattleState (source.State: HP, Guard, stamina, hand, piles, cells) and
// the depiction's settled frame (source.Frame: omens, turn, gap — the writer's view of the same state).
import { FloorSpec } from '../src/bridge/depictionScript/FloorSpec.js';
import { OmenHit } from '../src/bridge/depictionScript/DepictionTypes.js';

const num = (el) => (el ? Number(String(el.textContent).trim()) : null);
const text = (el) => (el ? String(el.textContent).trim() : null);
const visible = (el) => {
  if (!el || !el.isConnected) return false;
  for (let n = el; n && n.nodeType === 1; n = n.parentElement) {
    const cs = getComputedStyle(n);
    if (cs.display === 'none' || cs.visibility === 'hidden') return false;
    if (Number(cs.opacity) < 0.05) return false;
  }
  return true;
};

function readPanel(el) {
  if (!el) return null;
  const st = el.querySelector('.ocbg-battle-foe-st');
  return {
    shown: visible(el),
    down: el.classList.contains('is-down'),
    // a fallen enemy's grey panel that gave its place up to a standing enemy's (hud.follow, review 15)
    yielded: el.dataset.yielded === '1',
    hp: num(el.querySelector('.ocbg-battle-stat-hp')),
    guard: num(el.querySelector('.ocbg-battle-guard-num')),
    stamina: st && getComputedStyle(st).display !== 'none' ? num(el.querySelector('.ocbg-battle-foe-st-num')) : null,
    chips: [...el.querySelectorAll('.ocbg-battle-chip')].map((c) => text(c)),
  };
}

function readOmen(wrap) {
  if (!wrap) return null;
  const badge = wrap.querySelector('.ocbg-battle-omen');
  const plan = wrap.querySelector('.ocbg-battle-plan');
  const value = badge ? badge.querySelector('.ocb-omen-row1 > b') : null;
  return {
    shown: visible(badge),
    value: value ? text(value) : '',
    kind: text(badge && badge.querySelector('.ocbg-battle-omen-kind')) || '',
    hit: badge && badge.querySelector('.ocb-hitmark') ? (badge.querySelector('.ocb-hitmark.is-hit') ? 'Lands' : 'Misses') : 'None',
    acting: !!(badge && badge.classList.contains('is-active')),
    plan: visible(plan) && plan.childElementCount > 0,
  };
}

/** What the scene shows now. */
export function readScreen(scene) {
  const root = scene.root;
  const env = scene.env;
  const layer = (n) => root.querySelector('.ocbg-battle-layer--' + n);
  const corner = root.querySelector('.ocbg-battle-corner');
  const enemies = {};
  root.querySelectorAll('.ocbg-battle-stat.is-foe[data-unit]').forEach((el) => { enemies[el.dataset.unit] = readPanel(el); });
  const omens = {};
  root.querySelectorAll('.ocbg-battle-omenwrap[data-unit]').forEach((el) => { omens[el.dataset.unit] = readOmen(el); });
  // Review fix: where the figures stand on the floor, without the arena's drawer slide (field.shiftX, the intended
  // 15 % at most) — the check reported every figure 288 px off its cell while a drawer was open.
  const fieldMod = scene.modules && scene.modules.field;
  const slide = fieldMod && fieldMod.shiftX ? fieldMod.shiftX : 0;
  const nudge = (side, u) => (fieldMod && fieldMod.nudgeOf ? fieldMod.nudgeOf(side, u) : 0);
  const figures = {};
  root.querySelectorAll('.ocbg-battle-figure.is-enemy[data-unit]').forEach((el) => {
    const u = Number(el.dataset.unit);
    const g = env.geom.figure('Enemy', u);
    figures[u] = { shown: getComputedStyle(el).display !== 'none', x: g ? g.x - slide : null, homeX: g ? g.homeX - slide : null, nudge: nudge('Enemy', u) };
  });
  const hero = env.geom.figure('Player');
  const piles = {};
  root.querySelectorAll('.ocbg-battle-pile[data-pile]').forEach((el) => { piles[el.dataset.pile] = el.dataset.count != null ? Number(el.dataset.count) : null; });
  // The fan left to right as it stands on screen (a card that came home is appended last in the DOM).
  const fan = [...layer('hand').querySelectorAll('.ocbg-battle-card[data-card]')]
    .map((c) => ({ c, x: (env.geom.handCard(c.dataset.card) || { x: 0 }).x }))
    .sort((a, b) => a.x - b.x)
    .map((o) => o.c);
  const gap = root.querySelector('.ocbg-battle-gap');
  const refusal = root.querySelector('.ocbg-battle-refusal');
  const result = root.querySelector('.ocbg-battle-result');
  return {
    state: scene.player.state,
    turn: corner ? num(corner.querySelector('b')) : null,
    player: Object.assign(readPanel(root.querySelector('.ocbg-battle-stat.is-self')) || {}, {
      stamina: num(root.querySelector('.ocbg-battle-stamina .ocb-st-row1 > b')),
      x: hero ? hero.x - slide : null,
      nudge: nudge('Player'),
    }),
    slide,
    // 俊敏's choice (battle_ui_ux_v2 §3.8): the marks on the floor, by way (−1, 0, +1)
    freeStep: fieldMod && fieldMod.freeStepShown ? { dirs: fieldMod.freeStepDirs(), focus: fieldMod.freeStepFocus } : null,
    enemies,
    omens,
    figures,
    piles,
    hand: fan.map((c) => c.dataset.card),
    dimmed: fan.filter((c) => { const d = c.querySelector('.ocb-card-dim'); return d && Number(d.style.opacity || 0) > 0.3; }).map((c) => c.dataset.card),
    gap: gap && getComputedStyle(gap).display !== 'none' ? num(gap.querySelector('.ocbg-battle-gap-num')) : null,
    refusal: refusal ? text(refusal) : '',
    guide: text(root.querySelector('.ocbg-battle-guide')) || '',
    result: result ? text(result) : '',
    shade: !!root.querySelector('.ocbg-battle-shade.is-on'),
  };
}

/**
 * The mismatches between the screen and the source, settled (the player waits, or the battle is over).
 * Each is a short line: what, shown, truth.
 */
export function checkScreen(scene, source) {
  const out = [];
  const s = readScreen(scene);
  const st = source.State;
  const fr = source.Frame;
  const eq = (what, shown, truth) => { if (shown !== truth) out.push(what + ': shown ' + JSON.stringify(shown) + ', source ' + JSON.stringify(truth)); };
  if (!st || !fr) return ['the source has no state'];

  // the player
  eq('player HP', s.player.hp, st.Player.Hp);
  eq('player Guard', s.player.guard, st.Player.Guard);
  eq('player stamina', s.player.stamina, st.Player.Stamina);
  // the enemies: every standing one (and the last one, whose panel stays up with HP 0 once the battle is won)
  const multi = st.Enemies.length > 1;
  st.Enemies.forEach((u, k) => {
    const p = s.enemies[k];
    const alive = u.Alive;
    if (!p) { out.push('enemy ' + k + ': no panel'); return; }
    // battle_ui_ux_v2 §2.2.6: a fallen enemy's panel stays, grey, with its last numbers; once the battle is
    // over the fallen ones fade under the result card (the scene's end settle).
    const over = s.state === 'over';
    // A fallen enemy's panel may give its place up (hidden) while a standing enemy's panel stands over it (review 15).
    if (!(over && !alive) && !(multi && !alive && p.yielded)) eq('enemy ' + k + ' panel shown', p.shown, true);
    eq('enemy ' + k + ' panel grey', p.down, !alive);
    eq('enemy ' + k + ' HP', p.hp, u.Body.Hp);
    if (alive || !multi) eq('enemy ' + k + ' Guard', p.guard, u.Body.Guard);
    const uf = fr.Enemies && fr.Enemies[k] ? fr.Enemies[k] : fr.Enemy;
    if (alive && uf && uf.ShowStamina) eq('enemy ' + k + ' stamina', p.stamina, u.Body.Stamina);
    const f = s.figures[k];
    if (!f) { out.push('enemy ' + k + ': no figure'); return; }
    eq('enemy ' + k + ' figure standing', f.shown, alive);
    if (alive && fr.Floor && fr.Floor.Cells > 0) {
      // Its cell's middle, moved in by the field's nudge (a silhouette kept off the stance lists, at most 40 px).
      const x = FloorSpec.FeetX(fr.Floor.Cells, u.Body.Cell, Math.max(1, u.Body.Size));
      if (Math.abs(f.nudge) > 40) out.push('enemy ' + k + ' is moved ' + f.nudge.toFixed(1) + ' px off its cell');
      if (Math.abs(f.x - f.nudge - x) > 1) out.push('enemy ' + k + ' stands at x ' + (f.x - f.nudge).toFixed(1) + ', its cell ' + u.Body.Cell + ' is at ' + x.toFixed(1));
    }
  });
  if (fr.Floor && fr.Floor.Cells > 0) {
    const x = FloorSpec.FeetX(fr.Floor.Cells, st.Player.Cell, Math.max(1, st.Player.Size));
    if (Math.abs(s.player.x - s.player.nudge - x) > 1) out.push('player stands at x ' + (s.player.x - s.player.nudge).toFixed(1) + ', its cell ' + st.Player.Cell + ' is at ' + x.toFixed(1));
    // Once the battle is decided the gap number is put away (it measured to a fallen enemy).
    // The floor's one N (battle_ui_ux_v2 §2.2): the nearest enemy's at rest; while a card is selected or held in a fight
    // of several, the framed enemy's (UnitFrame.Gap); none once the battle is decided.
    const h = scene.modules.hand;
    // At rest a Tab moves the N to the enemy it chose (battle_ui_ux_v2 §1.4 / §3.4: player.restingUnit, review 15).
    const resting = multi && scene.player.restingUnit != null ? scene.player.restingUnit : -1;
    // Review 17: a selected or held card that frames nobody (a self card: framedUnit() −1) keeps the Tab's N (the player's
    // showHpLoss reads restingGapUnit then), so the check falls back to restingUnit, not to the frame's own N.
    const handFramed = (h.hasSelection() || h.isHolding()) && multi ? h.framedUnit() : -1;
    const framed = handFramed >= 0 ? handFramed : resting;
    const framedFrame = framed >= 0 ? (fr.Enemies || []).find((u) => u.Unit === framed && !u.Down && u.Gap >= 0) : null;
    const gapWant = fr.Outcome !== 'Ongoing' ? null : framedFrame ? framedFrame.Gap : fr.Floor.Gap;
    if (s.gap !== gapWant) {
      out.push('gap number: shown ' + JSON.stringify(s.gap) + ', source ' + JSON.stringify(gapWant) + ' (selected ' + h.hasSelection() + ', held ' + h.isHolding()
        + ', framed ' + h.framedUnit() + ', enemies ' + (fr.Enemies || []).map((u) => u.Unit + '@' + u.Cell + ' N' + u.Gap + (u.Down ? ' down' : '')).join(' ') + ', player ' + fr.Floor.PlayerCell + ')');
    }
  }
  // the hand and the piles
  eq('hand', s.hand.join(','), st.Hand.map((c) => c.InstanceId).join(','));
  eq('draw pile', s.piles.Draw, st.DrawPile.length);
  eq('discard pile', s.piles.Discard, st.DiscardPile.length);
  eq('exile pile', s.piles.Exile, st.Exiled.length);
  // the omens, as the depiction writes them (OmenDisclosure has already blanked what the journal has not read)
  const omens = fr.Omens && fr.Omens.length ? fr.Omens : [fr.Omen];
  omens.forEach((o, k) => {
    const u = o && o.Unit >= 0 ? o.Unit : k;
    const b = s.omens[u];
    if (!o) return;
    if (!b) { out.push('omen ' + u + ': no badge'); return; }
    const standing = !st.Enemies[u] || st.Enemies[u].Alive || !multi;
    if (!standing) return;
    // Review 9: once the result is up, every standing enemy's badge and plan have faded (no enemy acts again;
    // hud.retireOmens), whatever the last frame's omen says.
    if (s.state === 'over' && fr.Outcome !== 'Ongoing') {
      eq('omen ' + u + ' shown (retired at the end)', b.shown, false);
      eq('omen ' + u + ' plan shown (retired at the end)', b.plan, false);
      return;
    }
    eq('omen ' + u + ' shown', b.shown, !!o.Visible);
    if (o.Visible) {
      if (!(o.Statuses && o.Statuses.length)) eq('omen ' + u + ' value', b.value, o.ValueText || '');
      eq('omen ' + u + ' kind', b.kind, o.KindLabel || '');
      eq('omen ' + u + ' hit mark', b.hit, o.Hit == null ? OmenHit.None : o.Hit);
    }
    eq('omen ' + u + ' plan shown', b.plan, !!o.PlanVisible);
  });
  eq('turn', s.turn, fr.Corner.Turn);
  // while the player waits: the cards the source would refuse for their cost or 呪縛 are dark — not for reach
  // (battle-visual-v1 §5.1 / §6.2, review 15) — and the screen is up
  if (s.state === 'idle') {
    eq('dimmed cards', s.dimmed.join(','), st.Hand.map((c) => c.InstanceId).filter((id) => { const v = source.Inspect(id); return v !== 'Accepted' && v !== 'OutOfRange'; }).join(','));
    eq('enemy-turn shade', s.shade, false);
    // 俊敏's choice: up only while the source holds the step open, with an arrow on every way it calls open.
    const pending = !!scene.player.freeStepPending;
    eq('free step marks shown', !!s.freeStep, pending);
    if (pending) {
      if (typeof source.CanTakeFreeStep === 'function' && !source.CanTakeFreeStep()) out.push('free step marks up while the source has no step open');
      const want = [-1, 0, 1].filter((d) => d === 0 || source.CanTakeFreeStep(d)).join(',');
      eq('free step ways', s.freeStep ? s.freeStep.dirs.join(',') : '', want);
    }
  }
  if (s.state === 'over') {
    eq('result card', !!s.result, true);
  }
  return out;
}
