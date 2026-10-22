// The hud module of the battle scene (docs/BATTLE_SCENE.md §6c): everything on the screen that is neither a
// card in hand nor the arena. A port of Unity's StatusBarView, OmenBadgeView / OmenIconArt, StanceListView,
// StaminaTagView, PilesView, PileListView, BottomRightViews (PlayedAttributesView, ReserveHintView),
// CornerInfoView and BattleSpeedButton, and of the hud parts of DepictionPlayer: the end-turn plate
// (StyleEndTurn, ShowEnemyTurn), Banner, ShowResult / BuildResultCard and the panel half of FollowFigures /
// FollowPanels / KeepX / KeepBottom. Drawn with the design system's components (window.OCB) and bundle.css.
//
// It computes no rule. Every number and word comes from the frame and the cues the player hands over and from
// the ported texts (BattleSpeed, PileList, DepictionText, PlayedLayout …); every box from the ported layout
// scripts (StatusPanelLayout, StanceList, PileLayout, PlayedLayout, ReserveHintLayout, CornerLayout,
// EnemyTurnShadeLayout, HpWickSpec) or layout.js. Every length runs on env.clock with the nominal ms handed in.
//
// Several enemies (scene addition; Unity draws Enemies[0] only): when the frame carries more than one enemy
// (DepictionFrame.Enemies / Omens, the JS writer's extension), each standing enemy gets its own number panel
// and omen badge, under and over its cells. `status('Enemy')` and `omen` are the frame's enemy (Frame.Enemy,
// the first standing in the line); `status('Enemy', unit)` and `omenOf(unit)` reach any one of them.
import './hud.css';
import { UnitSide, BattleOutcome } from '../../bridge/depictionScript/DepictionTypes.js';
import { hatchAngle } from './layout.js';
import { StatusPanelLayout as SPL } from '../../bridge/depictionScript/StatusPanelLayout.js';
import { FloorSpec } from '../../bridge/depictionScript/FloorSpec.js';
import { FigureFrame } from '../../bridge/depictionScript/FigureMotion.js';
import { BattleSpeed } from '../../bridge/depictionScript/BattleSpeed.js';
import { EffectId } from '../../bridge/depictionScript/Effects.js';
import { stepOfSpeed } from './clock.js';
import { createTip, clamp } from './hud/util.js';
import { h } from '../common/stage.js';
import { createStatusPanel } from './hud/status.js';
import { createOmenBadge, OMEN_BADGE } from './hud/omen.js';
import { createStaminaTag } from './hud/stamina.js';
import { createPiles } from './hud/piles.js';
import { createStanceLists } from './hud/stances.js';
import { createPlayed, createReserveHint, createEndTurn } from './hud/bottom.js';
import { createCorner, createSpeedButton, createJournalButton, createSettingsButton, playBanner, createResultCard, createLossInk, createChainBanner, SETTINGS_BOX } from './hud/chrome.js';
import { StanceList } from '../../bridge/depictionScript/StanceList.js';
import { CornerLayout } from '../../bridge/depictionScript/CornerLayout.js';

/** DepictionPlayer.KeepX: the parts tied to the enemy keep 24 px inside the screen's edges. */
const EDGE = 24;
/** StatusPanelLayout.Place pushes two panels apart to this gap; several badges keep the same. */
const MIN_GAP = SPL.MinGap;
/** Above this scene-clock factor (fast-forward, 演出短縮) the panels are put on their places at once. */
const SNAP_SCALE = 50;
/** The badge's lift for a prediction tag (DS tag.lift) eases in and out over this (nominal ms on the scene clock; ReceiverShow's length). */
const OMEN_LIFT_MS = 120;
/**
 * battle_ui_ux_v2 §5.8 (several enemies): badges that would overlap slide apart, 「最大 75 px ずつ、200 ms、out」, and
 * their ▼ stays on the head. The slide is eased toward its place with a 200 ms out-like curve (exponential, time
 * constant 200 / 3), on the scene's speed.
 */
const BADGE_SLIDE_MAX = 75;
const BADGE_SLIDE_MS = 200;
/**
 * Review fix (the pile list's drawer covered the left 36 px of the hero's panel): while the drawer is open, the
 * panels keep this far right of its edge (DS README 余白 24).
 */
const DRAWER_CLEAR = 24;
/** Review 21: a stance list's high place stands this far from the corner chrome beside it. */
const STANCE_HIGH_GAP = 12;


/**
 * Lays several boxes on one row (scene addition, for two or three enemies): each centred on its x, kept
 * inside the edges, then pushed apart evenly where two would come closer than MIN_GAP — the
 * StatusPanelLayout.Place rule widened to any count. `items` are { x, w }, sorted left to right.
 */
function spreadRow(items, lo, hi) {
  const left = items.map((it) => clamp(it.x - it.w / 2, lo, hi - it.w));
  for (let pass = 0; pass < 24; pass++) {
    let moved = false;
    for (let i = 1; i < items.length; i++) {
      const short = left[i - 1] + items[i - 1].w + MIN_GAP - left[i];
      if (short <= 0.01) continue;
      left[i - 1] -= short / 2;
      left[i] += short / 2;
      moved = true;
    }
    for (let i = 0; i < items.length; i++) left[i] = clamp(left[i], lo, hi - items[i].w);
    if (!moved) break;
  }
  return left;
}

/**
 * Where a fallen enemy's grey panel stands (review fix: with the MIN_GAP padding a neighbour that merely touched it
 * hid it, so on adjacent cells a defeated enemy left no trace): under its emptied cell at `left`, or slid at most
 * FALLEN_SLIDE toward the free side to keep FALLEN_GAP off the panels already placed (`taken`); null — it gives its
 * place up — only when a panel actually stands over its cell.
 */
const FALLEN_GAP = 4;
const FALLEN_SLIDE = 24;
/** A standing panel counts as under its figure while the figure's x stays this far inside its edges (a quarter of a narrow one). */
const FALLEN_KEEP_UNDER = 24;
function fallenPlace(left, w, taken, lo, hi) {
  const clear = (l) => l >= lo - 0.5 && l + w <= hi + 0.5 && !taken.some((t) => l < t.right + FALLEN_GAP && l + w > t.left - FALLEN_GAP);
  if (clear(left)) return left;
  const options = [];
  for (const t of taken) {
    options.push(t.right + FALLEN_GAP, t.left - FALLEN_GAP - w);
  }
  const best = options.filter((l) => Math.abs(l - left) <= FALLEN_SLIDE && clear(l)).sort((a, b) => Math.abs(a - left) - Math.abs(b - left));
  return best.length ? best[0] : null;
}

export function createHud(env, opts) {
  opts = opts || {};
  const { clock, L, layer } = env;
  const tip = createTip(env);

  // ---- the parts, bottom → top inside their layers ---------------------------------------------
  // Review 21: the stance lists' high place (stances.js keepClear) — in the top bar, beside each side's corner chrome.
  let cornerRight = 24 + 232;
  const stances = createStanceLists(env, tip, {
    highTop: () => CornerLayout.CornerInfoTop,
    highLeft: (side) => (side === UnitSide.Player
      ? cornerRight + STANCE_HIGH_GAP
      : (settingsBtn ? SETTINGS_BOX.X : CornerLayout.SpeedButton.X) - STANCE_HIGH_GAP - StanceList.Width),
  });
  const stamina = createStaminaTag(env);
  const player = createStatusPanel(env, UnitSide.Player, {
    tip,
    onStamina: (s, max) => stamina.setStamina(s, max),
    onPip: (index) => stamina.lightPip(index),
    onStaminaPop: (ms) => stamina.popNumber(ms),
  });
  /** unit → { unit, status, omen, frame, omenFrame, shown } */
  const enemies = new Map();
  let primary = 0;
  let multi = false;

  function enemyView(unit) {
    let v = enemies.get(unit);
    if (!v) {
      v = { unit, status: createStatusPanel(env, UnitSide.Enemy, { tip }), omen: createOmenBadge(env), frame: null, shown: true, disclosure: null, lift: { y: 0 }, tagged: false };
      v.status.el.dataset.unit = String(unit);
      v.omen.el.dataset.unit = String(unit);
      // The boss colour's double frame belongs to the bosses' own words and statuses (battle-visual-v1 §2.1);
      // the panel keeps the rule frame, or the elite's phosphor one (StatusBarView.SetElite).
      enemies.set(unit, v);
    }
    return v;
  }
  enemyView(0);
  const primaryView = () => enemies.get(primary) || enemyView(primary);

  const piles = createPiles(env, {
    tip,
    onToggle(kind) {
      tip.hide();
      env.audio.play('uiClick');
      piles.toggle(kind);
    },
    /** The list opened or closed (battle_ui_ux_v2 §2.4: the figures slide right while it is open). */
    onDrawer(open) { if (opts.onDrawer) opts.onDrawer(open); },
  });
  const played = createPlayed(env);
  const hint = createReserveHint(env, tip);
  const endTurn = createEndTurn(env, () => { if (opts.onEndTurn) opts.onEndTurn(); });
  const corner = createCorner(env, opts.chain || null, tip);
  const speedBtn = createSpeedButton(env, () => { if (opts.onSpeed) opts.onSpeed(); }, tip);
  const journalBtn = createJournalButton(env, () => { if (opts.onJournal) opts.onJournal(); }, tip);
  // battle-visual-v1 §4.1 (review 19, a repeat finding: 「設定・セーブの画面は戦闘中に開けない」 — the speed switch is the one
  // setting in a battle): no gear, unless the app asks for one (opts.settingsInBattle; the brief's 「Esc / gear」).
  const settingsBtn = opts.settingsInBattle ? createSettingsButton(env, () => { if (opts.onSettings) opts.onSettings(); }, tip) : null;
  const result = createResultCard(env);
  const lossInk = createLossInk(env);
  const chainBanner = createChainBanner(env);
  {
    const step = stepOfSpeed(env.settings && env.settings.speed);
    speedBtn.show(BattleSpeed.Label(step), BattleSpeed.Chevrons(step));
  }

  // ---- frame → hud ----------------------------------------------------------------------------

  /** The enemies the frame describes: every one when it carries the list, else its one enemy. */
  function enemiesOf(f) {
    const list = f.Enemies && f.Enemies.length ? f.Enemies : [f.Enemy];
    const omens = f.Omens && f.Omens.length === list.length ? f.Omens : null;
    return list.map((u, k) => ({
      unit: u && u.Unit >= 0 ? u.Unit : k,
      unitFrame: u,
      omenFrame: omens ? omens[k] : u === f.Enemy ? f.Omen : null,
    }));
  }

  /**
   * The way enemy `e`'s move omen goes on screen (battle-visual-v1 §4.4): its destination cells as the frame's
   * floor gives them (FloorFrame.MoveCells, the violet dashed cells) against the cell it stands on. 0 when the
   * frame shows none.
   */
  function moveDirOf(f, e) {
    const fl = f.Floor;
    if (!fl) return 0;
    let cells = fl.MoveCells;
    if (fl.Enemies && fl.Enemies.length) {
      const fe = fl.Enemies.find((x) => x && x.Unit === e.unit);
      cells = fe ? fe.MoveCells : null;
    }
    const cell = e.unitFrame && e.unitFrame.Cell > 0 ? e.unitFrame.Cell : fl.EnemyCell;
    if (!cells || !cells.length || !(cell > 0)) return 0;
    return Math.min(...cells) < cell ? -1 : 1;
  }

  /** Whether enemy `e`'s shown omen hatches cells on the frame's floor (FloorFrame.Enemies[k].AimCells / AimCells). */
  function aimsOf(f, e) {
    const fl = f.Floor;
    if (!fl) return false;
    if (fl.Enemies && fl.Enemies.length) {
      const fe = fl.Enemies.find((x) => x && x.Unit === e.unit);
      return !!(fe && !fe.Down && fe.AimCells && fe.AimCells.length);
    }
    return !!(fl.AimCells && fl.AimCells.length);
  }

  function applyFrame(f, o) {
    if (!f) return;
    const first = !!(o && o.first);
    corner.bind(f.Corner);
    if (corner.el && corner.el.offsetWidth) cornerRight = corner.el.offsetLeft + corner.el.offsetWidth;
    // StaminaTagView: the divider and the dashed dots before the bind below shows the stamina.
    stamina.setDivider(f.StanceDivider | 0);
    stamina.setNextRecovery(f.Player ? f.Player.NextRecovery | 0 : 0);
    player.bind(f.Player);
    const list = enemiesOf(f);
    multi = list.length > 1;
    primary = f.Enemy && f.Enemy.Unit >= 0 ? f.Enemy.Unit : list[0] ? list[0].unit : 0;
    const listed = new Set();
    for (const e of list) {
      const v = enemyView(e.unit);
      listed.add(e.unit);
      v.frame = e.unitFrame;
      if (e.unitFrame) v.status.bind(e.unitFrame);
      v.omen.setMoveDir(moveDirOf(f, e));
      // §4.4 (several enemies): the badge's swatch ties it to its enemy's red cells on the floor, while it has some.
      v.omen.setSwatchShown(!!f.SwatchesShown, aimsOf(f, e));
      // Review 21: the badge of an action held across its events keeps what it shows (player.applyFrame's keepOmen).
      const keep = o && o.keepOmen != null && o.keepOmen === e.unit && v.omen.visible;
      if (e.omenFrame && !keep) {
        v.omen.bind(e.omenFrame);
        // scene addition: a 開示度 that rose mid-battle shows itself once (the writer re-blanks the frame)
        const d = e.omenFrame.Disclosure;
        if (!first && v.disclosure != null && d != null && d > v.disclosure) v.omen.reveal(env.fx.ms(EffectId.OmenShow));
        v.disclosure = d;
      }
      // battle_ui_ux_v2 §2.2.6: a fallen enemy's panel stays under its emptied cell, grey.
      v.shown = true;
      v.status.setDown(!!(e.unitFrame && e.unitFrame.Down));
      v.status.setSolid(multi);
      v.status.setSwatch(f.SwatchesShown && e.unitFrame ? hatchAngle(e.unitFrame.Hatch) : null);
    }
    for (const v of enemies.values()) {
      if (!listed.has(v.unit)) v.shown = false;
      v.status.show(v.shown);
      v.omen.show(v.shown);
    }
    stances.bind(f, first);
    lastFrame = f;
    piles.bind(f);
    played.bind(f.Played, !first);
    endTurn.setEnemyTurn(!!f.EnemyTurn);
    if (first) snapNext = true;
  }

  // ---- follow the figures (FollowFigures / FollowPanels / KeepX / KeepBottom) --------------------
  let dtReal = 16;
  let snapNext = true;
  let lastFrame = null;
  const offFrame = clock.onFrame((dtScene, real) => { dtReal = real || 16; });
  let lastWidths = '';
  /** A walking figure's panel: the offset it had from its figure as the walk began, closing as it walks (follow). */
  const carriedOff = new Map();

  function follow(o) {
    if (!o) return;
    const cells = o.cells | 0;
    const shown = [...enemies.values()].filter((v) => v.shown);
    // o.units (the player's, from each enemy's figure): [{ unit, x, top, size }]; else the frame's cells.
    const byUnit = new Map((o.units || []).map((u) => [u.unit, u]));
    const rows = shown.map((v) => {
      const isPrimary = v.unit === primary;
      const fig = byUnit.get(v.unit);
      const down = !!(v.frame && v.frame.Down);
      if (fig) return { v, x: fig.x, top: fig.top, w: SPL.EnemyPanelWidth(cells, Math.max(1, fig.size | 0)), down, walking: !!fig.walking };
      const size = isPrimary ? Math.max(1, o.enemySize | 0) : Math.max(1, (v.frame && v.frame.Size) | 0);
      const x = isPrimary || cells <= 0 || !v.frame ? o.enemyX : FloorSpec.FeetX(cells, v.frame.Cell, size);
      const top = isPrimary ? o.enemyTop : L.FEET_Y - FigureFrame.HeightFor(size);
      return { v, x, top, w: SPL.EnemyPanelWidth(cells, size), down };
    }).sort((a, b) => a.x - b.x);
    // battle-visual-v1 §4.5 (review fix): the panels say whose number it is by position alone. A fallen enemy's
    // grey panel (§2.2.6) takes no part in the row's spread, so it never pushes a standing enemy's panel off its
    // figure; it stands under its emptied cell, and gives the place up (hidden) while a standing enemy's panel or the
    // hero's would overlap it — an enemy that stepped into the fallen one's cell. One enemy: as Unity.
    const standing = rows.length > 1 ? rows.filter((r) => !r.down) : rows;
    const fallen = rows.length > 1 ? rows.filter((r) => r.down) : [];

    // A new line (other widths) puts the panels straight on their places, as a battle's first frame does.
    const widths = rows.map((r) => r.v.unit + ':' + r.w).join(',');
    let snap = !!o.snap || snapNext || widths !== lastWidths || clock.scale > SNAP_SCALE;
    lastWidths = widths;
    snapNext = false;
    rows.forEach((r) => r.v.status.setWidth(r.w));
    player.setWidth(SPL.PlayerWidth);

    // The pile list's drawer (left) covers its share of the screen: the row keeps DRAWER_CLEAR right of it. Review 19: the
    // journal's (right) the same — the row and the badges keep DRAWER_CLEAR left of its edge (with the trio on 8 cells
    // the third enemy's panel and badge stood under it).
    const journalEdge = env.geom.journalEdge ? env.geom.journalEdge() : null;
    const lo = Math.max(SPL.EdgeInset, piles.open ? piles.drawerRight() + DRAWER_CLEAR : 0);
    const hi = journalEdge != null ? Math.min(L.W - SPL.EdgeInset, journalEdge - DRAWER_CLEAR) : L.W - SPL.EdgeInset;
    const badgeLo = Math.max(EDGE, piles.open ? piles.drawerRight() + DRAWER_CLEAR : 0);
    const badgeHi = journalEdge != null ? Math.min(L.W - EDGE, journalEdge - DRAWER_CLEAR) : L.W - EDGE;

    // StatusPanelLayout.Place for one enemy; the same rule spread over the row for several.
    const targets = new Map();
    if (standing.length === 1) {
      const p = SPL.Place(o.playerX, SPL.PlayerWidth, standing[0].x, standing[0].w);
      const playerLeft = Math.max(p.playerLeft, lo);
      targets.set(player, playerLeft);
      targets.set(standing[0].v.status, playerLeft === p.playerLeft ? p.enemyLeft : Math.max(p.enemyLeft, playerLeft + SPL.PlayerWidth + MIN_GAP));
    } else if (standing.length > 1) {
      const items = [{ x: o.playerX, w: SPL.PlayerWidth, panel: player }].concat(standing.map((r) => ({ x: r.x, w: r.w, panel: r.v.status })));
      items.sort((a, b) => a.x - b.x);
      const lefts = spreadRow(items, lo, hi);
      items.forEach((it, i) => targets.set(it.panel, lefts[i]));
    } else {
      targets.set(player, clamp(o.playerX - SPL.PlayerWidth / 2, lo, hi - SPL.PlayerWidth));
    }
    // Review 17 (one enemy fell while another stood: the hero's 300 px panel slid back under the hero, into the fallen
    // hound's place, and the grey panel gave its place up for the rest of the fight though nobody stood on its cell): the
    // row is first laid out with the grey panels in it, as it stood before the fall. That layout is kept when every
    // standing panel still stands under its own figure (FALLEN_KEEP_UNDER inside its edges); only when one would be
    // pushed off its figure (an enemy that stepped into the fallen one's cell) does the grey panel slide or give way.
    let fallenDone = false;
    if (fallen.length && standing.length) {
      const items = [{ x: o.playerX, w: SPL.PlayerWidth, panel: player, keep: true }]
        .concat(standing.map((r) => ({ x: r.x, w: r.w, panel: r.v.status, keep: true })))
        .concat(fallen.map((r) => ({ x: r.x, w: r.w, panel: r.v.status, keep: false })));
      items.sort((a, b) => a.x - b.x);
      const lefts = spreadRow(items, lo, hi);
      const under = items.every((it, i) => !it.keep || (it.x >= lefts[i] + Math.min(FALLEN_KEEP_UNDER, it.w / 4) && it.x <= lefts[i] + it.w - Math.min(FALLEN_KEEP_UNDER, it.w / 4)));
      if (under) {
        items.forEach((it, i) => targets.set(it.panel, lefts[i]));
        fallen.forEach((r) => r.v.status.setYielded(false));
        fallenDone = true;
      }
    }
    const taken = [...targets.entries()].filter(([panel]) => !fallenDone || !fallen.some((r) => r.v.status === panel)).map(([panel, left]) => ({ left, right: left + panel.width }));
    for (const r of fallenDone ? [] : fallen) {
      const left = fallenPlace(clamp(r.x - r.w / 2, lo, hi - r.w), r.w, taken, lo, hi);
      r.v.status.setYielded(left == null);
      if (left != null) { targets.set(r.v.status, left); taken.push({ left, right: left + r.w }); }
    }
    // They ease toward their places by the same share each frame (DepictionPlayer.PanelFollowRate, at the
    // battle speed's frame-motion factor, on real time).
    // A figure walking on the floor (MoveOnFloor) carries its panel along with it (review fix: the eased follow ran
    // ahead of the walk's slow start, or trailed its fast middle by 120 px): the panel moves with the figure, and only
    // the offset it had as the walk began (a lunge's 46 px) closes at the follow rate; once it stands, the ease is back.
    const speed = BattleSpeed.TweenSpeed(stepOfSpeed(env.settings && env.settings.speed));
    const share = 1 - Math.exp(-L.PANEL_FOLLOW_RATE * speed * (dtReal / 1000));
    const carried = new Set(rows.filter((r) => r.walking).map((r) => r.v.status));
    if (o.playerWalking) carried.add(player);
    for (const [panel, to] of targets) {
      const now = panel.left;
      let left;
      if (snap) left = to;
      else if (carried.has(panel)) {
        const off = carriedOff.has(panel) ? carriedOff.get(panel) * (1 - share) : now - to;
        carriedOff.set(panel, Math.abs(off) < 0.25 ? 0 : off);
        left = to + carriedOff.get(panel);
      } else left = Math.abs(to - now) < 0.25 ? to : now + (to - now) * share;
      if (!carried.has(panel)) carriedOff.delete(panel);
      panel.placeAt(left);
    }

    // KeepX / KeepBottom: each badge's middle on its enemy's home x, 24 px inside the edges; its bottom 14 px
    // over the head (OmenOverHead). Several badges keep apart on one row (scene addition, §5.8): each slides at most
    // BADGE_SLIDE_MAX off its head, eased over BADGE_SLIDE_MS, and its ▼ stays on the head.
    const badgeRows = rows.length > 1 ? standing : rows;
    const spread = badgeRows.length > 1
      ? spreadRow(badgeRows.map((r) => ({ x: r.x, w: OMEN_BADGE.w })), badgeLo, badgeHi)
      : badgeRows.map((r) => clamp(r.x - OMEN_BADGE.w / 2, badgeLo, badgeHi - OMEN_BADGE.w));
    const slideShare = 1 - Math.exp(-3 * speed * dtReal / BADGE_SLIDE_MS);
    // battle_ui_ux_v2 §5.16 「2 体以上の戦いでは、精鋭の札 2 段とほかの敵の札 1 段（下段の高さ）を並べます」 (review 19: each badge
    // stood 14 px over its own head, so a 264 px elite's stood 38 px over a 226 px enemy's): with two or more enemies every
    // badge's bottom is on one row, 14 px over the highest head; the ▼ still points down at its own enemy.
    const rowTop = badgeRows.length > 1 ? Math.min(...badgeRows.map((r) => r.top)) : null;
    // Review 19: with a drawer open the row has less room (the trio beside the journal); its badges may slide twice as far
    // apart, so none covers its neighbour's swatch and hit mark.
    const slideMax = journalEdge != null || piles.open ? BADGE_SLIDE_MAX * 2 : BADGE_SLIDE_MAX;
    const liftShare = 1 - Math.exp(-3 * speed * dtReal / OMEN_LIFT_MS);
    badgeRows.forEach((r, i) => {
      const home = r.x - OMEN_BADGE.w / 2;
      // KeepX's own shift at the screen's (or an open drawer's) edge, and the row's slide apart (at most 75 px) from there.
      const edgeFree = clamp(home, badgeLo, badgeHi - OMEN_BADGE.w) - home;
      const base = home + edgeFree;
      const off = (multi ? clamp(spread[i], base - slideMax, base + slideMax) : spread[i]) - base;
      const v = r.v;
      v.badgeOff = snap || v.badgeOff == null || Math.abs(off - v.badgeOff) < 0.25 ? off : v.badgeOff + (off - v.badgeOff) * slideShare;
      const left = home + edgeFree + v.badgeOff;
      const bottom = (rowTop != null ? rowTop : r.top) - L.OMEN.overHead;
      // DS tag.lift (review 19): a badge over a frame showing the held card's prediction tag (on the frame's top edge) rises
      // just clear of the tag, eased over OMEN_LIFT_MS; every other badge rests on the row.
      const tagTop = r.top - L.MARK_PAD.top - L.PRED_TAG_H / 2;
      const need = v.tagged ? clamp(bottom - (tagTop - L.TAG_LIFT_GAP), 0, L.TAG_LIFT) : 0;
      v.lift.y = snap || Math.abs(need - v.lift.y) < 0.25 ? need : v.lift.y + (need - v.lift.y) * liftShare;
      v.omen.place(left, bottom - OMEN_BADGE.h - v.lift.y);
      // Review 17: the ▼ ties every badge to its enemy's head, one enemy too (battle_ui_ux_v2 §5.8 「▼ で敵に結びます」,
      // §5.16 「1 手目（▼）が下」); Unity draws none.
      v.omen.setTail(r.x - left);
      // Review 17 (two enemies passing: the walking one's badge rode over its neighbour's swatch and hit mark): a walking
      // enemy's badge goes behind the others until it arrives, as its figure does (battle_ui_ux_v2 §5.7).
      v.omen.setBehind(!!r.walking && badgeRows.length > 1);
    });
    // Review 21: a stance list a standing figure's art comes under (a slid arena, a wide silhouette on the last cell) moves to
    // its high place in the top bar, and back once the figures leave it.
    if (env.geom.figure) {
      const arts = [];
      const add = (g) => { if (g && !g.down) arts.push({ x0: g.artX0 != null ? g.artX0 : g.x - g.width / 2, x1: g.artX1 != null ? g.artX1 : g.x + g.width / 2, y0: g.top, y1: g.feetY }); };
      add(env.geom.figure(UnitSide.Player));
      for (const r of rows) if (!r.down) add(env.geom.figure(UnitSide.Enemy, multi ? r.v.unit : undefined));
      stances.keepClear(arts);
    }
  }

  // ---- the badge and the held card's prediction tag (DS motion.md `tag.lift`) ----------------------------
  /**
   * `units`: the enemies whose frame shows the held / selected card's prediction tag (the hand's taggedUnits; −1 or
   * undefined for the one enemy). Review 19 (the tag pushed under the badge stood on the enemy's head and hid the ▼; the DS
   * TargetFrame puts it on the frame's top edge and lifts the badge for it): follow() raises their badges just clear of the
   * tag (L.TAG_LIFT_GAP, at most L.TAG_LIFT); the others stay on the row.
   */
  function liftOmen(units) {
    const list = Array.isArray(units) ? units : units == null ? [] : [units];
    const lifted = new Set(list.map((u) => (multi && u != null && u >= 0 && enemies.has(u) ? u : primary)));
    for (const v of enemies.values()) v.tagged = v.shown && !(v.frame && v.frame.Down) && lifted.has(v.unit);
  }

  // ---- hovering an omen badge keeps only that enemy's aimed cells (battle_ui_ux_v2 §2.2.2) -----------
  let hoverUnit = null;
  function onPointerMove(e) {
    let found = null;
    if (multi && e.target && env.root.contains(e.target)) {
      const pt = env.toStage(e);
      for (const v of enemies.values()) {
        if (!v.shown || !v.omen.visible) continue;
        const b = v.omen.box();
        if (pt.x >= b.x && pt.x <= b.x + b.w && pt.y >= b.y && pt.y <= b.y + b.h) { found = v.unit; break; }
      }
    }
    if (found === hoverUnit) return;
    hoverUnit = found;
    if (opts.onOmenHover) opts.onOmenHover(found);
  }
  window.addEventListener('pointermove', onPointerMove);
  /**
   * §3.13 (review 21): a pinned chip / stance detail closes on a click anywhere and on a right click elsewhere (a right click
   * on a chip or a frame pins that one instead: the event is marked there).
   */
  function onPinDown(e) { if (e.button === 0 && tip.pinned) tip.unpin(); }
  function onPinContext(e) { if (!e.ocbgPinned && tip.pinned) tip.unpin(); }
  window.addEventListener('pointerdown', onPinDown, true);
  env.root.addEventListener('contextmenu', onPinContext);

  // ---- the end of the battle: the field settles under the result card (review fix) ---------------------
  const scrim = h('div', { class: 'ocbg-battle-endscrim' });
  scrim.style.opacity = '0';
  scrim.style.display = 'none';
  layer.over.insertBefore(scrim, layer.over.firstChild);
  /** Every fallen unit's panel and stance list (and badge), as elements to fade. */
  function fallenParts(outcome) {
    const parts = [];
    for (const v of enemies.values()) {
      if (v.frame && v.frame.Down) parts.push(v.status.el, v.omen.el);
      // Review fix: the battle is over, so no enemy acts again: a standing enemy's omen badge and plan go too
      // (on a loss they stood in the result card's band, over the surviving enemy's head, and stuck out of it).
      else if (outcome !== BattleOutcome.Ongoing) parts.push(v.omen.el);
    }
    // The enemies' stance list fades once every enemy has fallen (it keeps the stances of the standing ones).
    if (outcome === BattleOutcome.Won) parts.push(stances.elOf(UnitSide.Enemy));
    // The script never marks the player Down: a lost battle is the player's fall.
    if (outcome === BattleOutcome.Lost || (lastFrame && lastFrame.Player && lastFrame.Player.Down)) parts.push(player.el, stances.elOf(UnitSide.Player));
    return parts.filter(Boolean);
  }
  function settleEnd(ms, outcome) {
    endTurn.setOff(true);
    journalBtn.setOff(true);
    if (tip) tip.hide();
    scrim.style.display = '';
    const parts = fallenParts(outcome);
    // A part already faded (a standing enemy's omen, retireOmens) stays at 0 (review fix: `|| 1` lit it again), and a
    // fallen enemy's grey panel fades from its class's 55 % (review 17: the inline 1 lit it up for the first frame).
    const from = parts.map((el) => {
      const v = parseFloat(el.style.opacity);
      if (Number.isFinite(v)) return v;
      const c = parseFloat(getComputedStyle(el).opacity);
      return Number.isFinite(c) ? c : 1;
    });
    return clock.run(ms, 'out', (t) => {
      scrim.style.opacity = t.toFixed(3);
      parts.forEach((el, i) => { el.style.opacity = (from[i] * (1 - t)).toFixed(3); });
    });
  }

  /**
   * The battle is over (review fix): every standing enemy's omen badge and plan fade (Ease.In, OmenSpend's
   * length), since no enemy acts again. On a loss this runs beside the hero's fall, so the badge over the
   * surviving enemy's head is gone before the result card rises into that band.
   */
  function retireOmens(ms) {
    const els = [...enemies.values()].filter((v) => !(v.frame && v.frame.Down)).map((v) => v.omen.el).filter(Boolean);
    const from = els.map((el) => { const v = parseFloat(el.style.opacity); return Number.isFinite(v) ? v : 1; });
    return clock.run(ms, 'in', (t) => { els.forEach((el, i) => { el.style.opacity = (from[i] * (1 - t)).toFixed(3); }); });
  }

  // ---- the omen badge of the frame's enemy (a forwarder, so the player's calls reach whoever leads) ---
  const omen = {
    bind(o) {
      if (!o) return;
      const target = multi && o.Unit >= 0 && enemies.has(o.Unit) ? enemies.get(o.Unit) : primaryView();
      target.omen.bind(o);
    },
    popIn: (ms) => primaryView().omen.popIn(ms),
    blink: (ms) => primaryView().omen.blink(ms),
    flare: (ms) => primaryView().omen.flare(ms),
    setActing: (on) => {
      // The light goes out on every badge (an event other than the enemy's action), and comes on the leader's.
      if (!on) for (const v of enemies.values()) v.omen.setActing(false);
      else primaryView().omen.setActing(true);
    },
    fadeOut: (ms) => primaryView().omen.fadeOut(ms),
    hideNow: () => primaryView().omen.hideNow(),
    fadeOutAll: (ms) => primaryView().omen.fadeOutAll(ms),
    hideAllNow: () => primaryView().omen.hideAllNow(),
    strikeSide: (ms) => primaryView().omen.strikeSide(ms),
    strikeSideNow: () => primaryView().omen.strikeSideNow(),
  };

  /** The stance list's slot a stance just put down goes to (StanceList.TopOf(0)), or null while the lists are hidden. */
  function stanceSlotBox(side) {
    return stances.slotOf(side);
  }
  /** Badges in flight to the stance list (removed on destroy). */
  const flights = new Set();

  // ---- api ------------------------------------------------------------------------------------------
  const api = {
    applyFrame,
    follow,
    /** 'Player' | 'Enemy'; `unit` (optional) names one enemy of several (Cue.Unit). */
    status(side, unit) {
      if (side === UnitSide.Player) return player;
      if (unit != null && unit >= 0 && enemies.has(unit)) return enemies.get(unit).status;
      return primaryView().status;
    },
    omen,
    /**
     * An omen shown mid-event (OmenShow, before the event's frame is applied): enemy `unit`'s badge takes its swatch and
     * its move arrow from `frame` (the event's After), so the badge pops in as that frame will leave it.
     */
    syncOmenLook(frame, unit) {
      if (!frame) return;
      const e = enemiesOf(frame).find((x) => x.unit === unit) || enemiesOf(frame)[0];
      if (!e) return;
      const v = enemies.get(e.unit);
      if (!v) return;
      v.omen.setMoveDir(moveDirOf(frame, e));
      v.omen.setSwatchShown(!!frame.SwatchesShown, aimsOf(frame, e));
    },
    /** The badge of enemy `unit` (Cue.Unit / OmenFrame.Unit); the leader's when unknown. */
    omenOf(unit) {
      return unit != null && enemies.has(unit) ? enemies.get(unit).omen : primaryView().omen;
    },
    /** The enemies the hud draws now, by unit (the frame's order). */
    enemyUnits() { return [...enemies.values()].filter((v) => v.shown).map((v) => v.unit); },
    stamina: {
      showCost(n) { stamina.showCost(n || 0); },
      glowReserve(ms) { return stamina.glowReserve(ms); },
    },
    reserveHint(text) { hint.show(text || ''); },
    setEnemyTurn(on) { endTurn.setEnemyTurn(on); },
    endTurn: { pop(ms) { return endTurn.pop(ms); }, flash(ms) { return endTurn.flash(ms); }, refuse(ms) { return endTurn.refuse(ms); } },
    banner(text, side, ms) { return playBanner(env, text, side, ms); },
    /**
     * The end of the battle (review 21): the result paper — `info` { title, lines, bars, stats, button } (the player's scene
     * text), `o.onButton` its button's click.
     */
    resultCard(outcome, ms, info, o) { return result.show(outcome, ms, info, o); },
    resultCardNow(outcome, info, o) { result.now(outcome, info, o); },
    /** §2.5 / §5.18 順 6 (review 21): a chain's next fight opens with 「n 戦目」 and its enemy's name over the dark board. */
    chainBanner(title, name, scale, onOut) { return chainBanner.play(title, name, scale, onOut); },
    /** §5.19 (review 21): 「力尽きた」 goes as the result screen comes up. */
    lossWordOut(ms) { return lossInk.fadeWord(ms); },
    /** The battle is over: the scrim comes up, the plate goes off, the fallen units' panels and stance lists fade. */
    settleEnd(ms, outcome) { return settleEnd(ms, outcome); },
    settleEndNow(outcome) { settleEnd(0, outcome); },
    /** The battle is over: the standing enemies' omen badges and plans fade (no enemy acts again). */
    retireOmens(ms) { return retireOmens(ms); },
    /** The enemies whose frame shows the held card's prediction tag (none: []): their badges stand higher. */
    liftOmen(units) { liftOmen(units); },
    /**
     * Review 19 (a Tab at rest chose an enemy and nothing on it said so): enemy `unit`'s number panel wears the battle's
     * focus (the amber edge) — null for none.
     */
    setRestFocus(unit) {
      for (const v of enemies.values()) v.status.setFocus(unit != null && v.unit === unit && v.shown && !(v.frame && v.frame.Down));
    },
    /** The pile list's log tab (battle_ui_ux_v2 §1.1 C4): the lines so far. */
    setLog(lines) { piles.setLog(lines); },
    /**
     * battle_ui_ux_v2 §5.11 順 8 (review 19; Unity lets the badge vanish with the frame): an enemy's stance action — its omen
     * badge flies to the end of the enemy's stance list and shrinks into the slot there over `ms` (out), fading as it lands.
     */
    flyOmenToStances(unit, ms) {
      const v = unit != null && enemies.has(unit) ? enemies.get(unit) : primaryView();
      const from = v.omen.box();
      const slot = stanceSlotBox(UnitSide.Enemy);
      if (!slot || !v.omen.visible) return Promise.resolve();
      // The badge alone (an elite's plan stays for its second action).
      const face = v.omen.el.querySelector('.ocbg-battle-omen') || v.omen.el;
      const ghost = face.cloneNode(true);
      ghost.classList.add('ocbg-battle-omenfly');
      ghost.style.width = from.w + 'px';
      ghost.style.height = from.h + 'px';
      ghost.style.left = from.x + 'px';
      ghost.style.top = from.y + 'px';
      ghost.style.transformOrigin = '0 0';
      layer.float.appendChild(ghost);
      flights.add(ghost);
      v.omen.hideNow();
      const k = Math.min(slot.w / from.w, slot.h / from.h);
      const to = { x: slot.x + slot.w / 2 - (from.w * k) / 2, y: slot.y + slot.h / 2 - (from.h * k) / 2 };
      if (!(ms > 0)) { ghost.remove(); flights.delete(ghost); return Promise.resolve(); }
      return clock.run(ms, 'out', (t) => {
        ghost.style.left = (from.x + (to.x - from.x) * t).toFixed(1) + 'px';
        ghost.style.top = (from.y + (to.y - from.y) * t).toFixed(1) + 'px';
        ghost.style.scale = (1 + (k - 1) * t).toFixed(3);
        ghost.style.opacity = (t < 0.7 ? 1 : 1 - (t - 0.7) / 0.3).toFixed(3);
      }).then(() => { ghost.remove(); flights.delete(ghost); });
    },
    /** battle_ui_ux_v2 §5.19 順 2〜3: the ink floods up from the bottom over `inkMs`, then `word` comes in over `wordMs`. */
    lossInk(word, inkMs, wordMs) { return lossInk.show(word, inkMs, wordMs); },
    speed(label, chevrons) { speedBtn.show(label, chevrons); },
    /** The speed switch (and the settings gear beside it) shown or not: the battle's own switches, gone once it is over. */
    setSpeedVisible(on) { speedBtn.setVisible(!!on); if (settingsBtn) settingsBtn.setVisible(!!on); },
    /** Esc (review 21): closes a chip's or a stance's detail pinned by a right click; returns whether one was open. */
    closePinned() { return tip.unpin(); },
    drawerOpen() { return piles.open; },
    closeDrawer() { piles.close(); },
    destroy() {
      offFrame();
      window.removeEventListener('pointermove', onPointerMove);
      window.removeEventListener('pointerdown', onPinDown, true);
      env.root.removeEventListener('contextmenu', onPinContext);
      tip.unpin();
      scrim.remove();
      tip.hide();
      piles.destroy();
      [stances, stamina, player, played, hint, endTurn, corner, speedBtn, journalBtn, settingsBtn, result, lossInk, chainBanner].forEach((p) => { if (p) p.destroy(); });
      flights.forEach((el) => el.remove());
      for (const v of enemies.values()) { v.status.destroy(); v.omen.destroy(); }
      enemies.clear();
    },
  };

  // ---- geometry (docs/BATTLE_SCENE.md §2.4) --------------------------------------------------------
  env.geom.pileMiddle = (kind) => piles.middle(kind);
  env.geom.omenBox = (unit) => api.omenOf(unit).box();
  /** Whether enemy `unit`'s badge shows now (a spent or hidden badge is no part of the enemy to click). */
  env.geom.omenShown = (unit) => api.omenOf(unit).visible;
  /**
   * The omen badge's place over every standing enemy (stage px), shown or between two omens: the floats keep
   * under them (a badge that pops in while a number rises must not land on it).
   */
  env.geom.omenBoxes = () => [...enemies.values()].filter((v) => v.shown && !(v.frame && v.frame.Down)).map((v) => v.omen.box());
  env.geom.endTurnBox = () => endTurn.box();
  /** The number panel of `side` (an enemy's by `unit`) as it stands: { x, y, w, h }, or null when not shown. */
  env.geom.statusBox = (side, unit) => {
    const p = api.status(side, unit);
    return p && p.shown ? p.box() : null;
  };
  /** Every number panel shown, { side, unit, box } (the hand keeps its hover line off them). */
  env.geom.statusBoxes = () => {
    const out = [];
    if (player.shown) out.push({ side: UnitSide.Player, unit: -1, box: player.box() });
    for (const v of enemies.values()) if (v.shown && v.status.shown) out.push({ side: UnitSide.Enemy, unit: v.unit, box: v.status.box() });
    return out;
  };
  /** The stance list of `side` as it stands (its frames, the empty one included), or null when the lists are hidden. */
  env.geom.stanceBox = (side) => stances.boxOf(side);
  /** The slot at the end of `side`'s stance list (y 470, where a stance just put down goes; §5.11 順 1), or null when hidden. */
  env.geom.stanceSlot = (side) => stanceSlotBox(side);
  env.geom.coversPointer = (pt) => piles.covers(pt);
  /** The pile list drawer's right edge (stage px) as it slides in; 0 while closed (the guide line keeps right of it). */
  env.geom.drawerRight = () => piles.drawerRight();

  return api;
}
