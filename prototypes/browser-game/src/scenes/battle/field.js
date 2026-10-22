// The field module of the battle scene (docs/BATTLE_SCENE.md §6b): the layer's backdrop, the floor of
// cells, the figures and their motion, every DepictionFx effect, the floating numbers and the enemy-turn
// shade. A port of Unity's FigureView, FloorView, DepictionFx, EnemyTurnShadeView and BackdropLayers, and
// of the figure / floor parts of DepictionPlayer (ApplyFloor, FollowFigures' positions, Lunge, ReturnHome,
// SnapHome, EnemyMotion, Recoil, Heave, ShakeArena, MoveOnFloor, SnapOnFloor, FlyGhostTo,
// SlideDiscardOntoDraw, the stance-glow parts).
//
// It computes no rule: where a figure stands, what the floor marks, which shape a blow draws and how
// strong it is all come from the frame and the cues the player hands over; the layout numbers from the
// ported FloorSpec / FigureFrame / EnemyMotionShape / FigureFacing / DefeatShape / StanceGlow /
// EnemyTurnShadeLayout / BackdropSpec. Every length runs on env.clock with the nominal ms it is given.
// The field plays no sound (§4.4).
//
// Coordinates are stage px with y down: Unity's y-up offsets are negated, and Unity's z rotations
// (counter-clockwise positive) are written as CSS rotate(−deg).
//
// Several enemies (scene addition with the JS writer's extension, docs/port-notes/depiction-multi-enemy.md;
// Unity draws Enemies[0] only): every enemy of DepictionFrame.Enemies has its own figure, standing on its own
// Cell. Every method that takes a side takes an optional enemy `unit` last (Cue.Unit / UnitFrame.Unit); without
// one, 'Enemy' is the frame's focus enemy (Frame.Enemy, the first standing in the line), as before.
import './field.css';
import { h } from '../common/stage.js';
import { UnitSide, StrikeSystem, EnemyHatch, BattleOutcome } from '../../bridge/depictionScript/DepictionTypes.js';
import { FloorSpec } from '../../bridge/depictionScript/FloorSpec.js';
import { FigureFrame, EnemyMotionShape, FigureFacing, DefeatShape, FigurePose } from '../../bridge/depictionScript/FigureMotion.js';
import { EnemyTurnShadeLayout } from '../../bridge/depictionScript/EnemyTurnShadeLayout.js';
import { StanceGlow } from '../../bridge/depictionScript/StanceGlow.js';
import { ScreenBox } from '../../bridge/depictionScript/CornerLayout.js';
import { createBackdrop } from './field/backdrop.js';
import { createFloor } from './field/floor.js';
import { createFx } from './field/fx.js';
import { createFreeStep } from './field/freestep.js';
import { tok, glowGradient, shapeToken, silhouetteOf, figureDef, drawFigure } from './field/art.js';

const PLAYER = UnitSide.Player;
const ENEMY = UnitSide.Enemy;
/** Where the figures stand when the script draws no floor (the prefab's slots; the core always draws one). */
const NO_FLOOR_X = { Player: 660, Enemy: 1260 };
/** DepictionPlayer.KeepX: the parts tied to the enemy keep 24 px inside the screen's edges. */
const EDGE = 24;
const lerp = (a, b, t) => a + (b - a) * t;
/**
 * battle_ui_ux_v2 §2.3 / §2.4: while the journal drawer (right 30 %) is open the figures slide 15 % of the
 * screen to the left, and while the pile list (left) is open 15 % to the right, over 220 ms (out); both open,
 * they stay. The floor slides with them (the figures stand on its cells), and so does everything placed
 * from the figures (panels, badges, hooks, receivers, effects) through env.geom.
 *
 * Review 19 (the trio on 8 cells: the journal cut off the third enemy's badge, body and panel, and the slide was
 * 62–203 px where the spec sets 288): the slide is the spec's 15 %. The stance lists no longer hold it back — their
 * frames are drawn over the figures (layer.panels), so a figure that slides under one stays readable behind it — and
 * the floor may come within 12 px of the screen's edge (the 15 % centres the 1320 px floor in the 70 % beside the
 * drawer). One limit stays (scene choice): no standing figure's art leaves the screen (SCREEN_KEEP), so a wide
 * silhouette on the last cell (the shadow hound) holds the pile list's slide a little short. The badges and the
 * panels keep inside the part of the screen the drawer leaves (hud.js). The room is read every frame, so a figure
 * walking toward that edge while the drawer is open pulls the slide back.
 */
const ARENA_SHIFT_SHARE = 0.15;
const ARENA_SHIFT_MS = 220;
/** battle_ui_ux_v2 §2.4: either drawer takes 30 % of the screen; a figure the slide brings out of it keeps this far inside. */
const DRAWER_SHARE = 0.3;
const DRAWER_KEEP = 12;
/** Scene choice: while a drawer slides the arena, every figure's art keeps this far inside the screen. */
const SCREEN_KEEP = 0;
/** An enemy's lunge stops this far short of an ally's body in its way (roomToward; scene choice, Unity has one enemy). */
const ALLY_CLEAR = 8;
/** Review 19: a blow's shape stops this far short of another standing figure's body, and reaches at least this far. */
const BLOW_CLEAR = 8;
const BLOW_MIN = 40;
/** How far from the chest a blocked blow's shield stands, toward the blow (scene choice; Unity: on the chest). */
const SHIELD_TOWARD = 40;
/** battle_ui_ux_v2 §5.7 順 5: how far a figure leans the way it meant to move when the move went nowhere (scene choice). */
const BUMP_PX = 12;
/**
 * Paint order of the figures (battle_ui_ux_v2 §2.2.1 「人型の前後の重なりは、自分に近い敵を前面に描きます」, §5.7
 * 「敵どうしのすれ違いは、動く敵の人型を後ろ（奥）の層に描いて重なりを見せます」; review fix: they painted in creation
 * order, so the farthest enemy covered the nearer ones). The hero stands in front; then the enemies, the one nearest
 * the hero in front of the next; an enemy walking on the floor goes behind every other enemy until it arrives. The
 * gap number (field.css, z 20) stays over them all.
 */
const Z_HERO = 9;
const Z_FRONT_ENEMY = 8;
const Z_WALKING_ENEMY = 2;
/**
 * Depth separation with two or three enemies (scene choice, review fix: three enemies on adjacent cells, all in the
 * same dark fill with a faint violet rim, fused into one clump): the nearest enemy wears a sharper violet edge, so its
 * outline cuts across the body behind it, and each enemy further back is drawn a step lighter (opaque: a see-through
 * body turned into a ghost over the bright layers), so where two overlap the darker one reads in front and the bodies
 * can be counted at a glance. One enemy is drawn as Unity draws it.
 */
const FRONT_EDGE = 'drop-shadow(0 0 1.5px ' + 'rgba(139, 111, 208, 0.85))';
const BACK_LIFT_STEP = 0.5;
/** battle_ui_ux_v2 §5.6 順 5: the low-HP vignette rests at 0.25 and a landed blow pulses it from 0.5 over 200 ms. */
const VIGNETTE_REST = 0.25;
const VIGNETTE_PEAK = 0.5;
const VIGNETTE_PULSE_MS = 200;
/**
 * Review 21 (on layer 1 a crimson veil at α 0.25 over the pale meadow read as a beige stain, not as danger): over a bright
 * sky (BackdropSpec's top sky stop above BRIGHT_SKY relative luminance — layers 1, 2 and 4) the vignette is the deep form
 * (field.css .is-bright: a narrower band, crimson darkened toward the flood at the edge) at BRIGHT_BOOST times the strength
 * (the peak held at 1), so it reads as a red rim against the light as the plain form does against the dark layers. A look call; §5.6 順 5 gives 0.25 / 0.5.
 */
const BRIGHT_SKY = 0.4;
const BRIGHT_BOOST = 2.6;
/**
 * §5.11 順 2 「身体に薄い光」 (review 21: the silhouette filled flat crimson at α 0.25 read as an enraged tint, and drowned the
 * hero's amber edge): the body's light is a radial light inside the silhouette — centred on the chest (65 % up), full at its
 * middle and gone at 46 % of the figure's height, screened over the art (field.css), so the middle of the body glows and its
 * edge keeps the figure's own rim. Its strength is StanceGlow.Alpha (renderFigure).
 */
const BODY_LIGHT = { color: 'var(--crimson)', at: 0.6, r: 0.46, stops: [[0, 1], [0.35, 0.7], [0.7, 0.22], [1, 0]] };

export function createField(env, opts = {}) {
  const { OCB, layer, clock, L } = env;
  const layerNo = opts.layer || env.layerNo || 1;
  const FEET = L.FEET_Y;

  const backdrop = createBackdrop(env, layerNo);
  const floor = createFloor(env, backdrop.tile);
  const fx = createFx(env);
  /** 俊敏's choice on the floor (scene addition with the source's free step; field/freestep.js). */
  const freeStep = createFreeStep(env, {
    onPick: (dir) => { if (opts.onFreeStepPick) opts.onFreeStepPick(dir); },
    onFocus: (dir) => { if (opts.onFreeStepFocus) opts.onFreeStepFocus(dir); },
  });

  // ---- figures (FigureView) ------------------------------------------------------------------

  function makeFigure(side, unit) {
    const el = h('div', { class: 'ocbg-battle-figure ' + (side === PLAYER ? 'is-player' : 'is-enemy') });
    if (side !== PLAYER) el.dataset.unit = String(unit);
    const halo = h('div', { class: 'ocbg-battle-halo' });
    const glow = h('div', { class: 'ocbg-battle-stance-glow' });
    // §5.11 順 2 StanceGlow: the stance attribute's colour (crimson) at α 0.25, a soft light over the body.
    glow.style.background = glowGradient((a) => tok('crimson', a), StanceGlow.Alpha);
    const body = h('div', { class: 'ocbg-battle-body' });
    const art = h('div', { class: 'ocbg-battle-art' });
    // Review 19 (the stance light sat behind the opaque silhouette, so only a red fringe showed): the body's own light
    // (§5.11 順 2 「身体に薄い光」) is the silhouette again in the stance colour at StanceGlow.Alpha, laid over the art;
    // the soft glow behind stays as its halo.
    const glowBody = h('div', { class: 'ocbg-battle-glowbody' });
    const flashEl = h('div', { class: 'ocbg-battle-flash' });
    body.appendChild(art);
    body.appendChild(glowBody);
    body.appendChild(flashEl);
    el.appendChild(halo);
    el.appendChild(glow);
    el.appendChild(body);
    layer.field.appendChild(el);
    const x = NO_FLOOR_X[side];
    return {
      side, unit: side === PLAYER ? -1 : unit, size: 1, el, halo, glow, body, art, glowBody, flashEl, foot: null,
      kind: '', rim: '', height: FigureFrame.StandardHeight, width: 100, svgW: 140, cx: 62,
      x, homeX: x, nudge: 0, y: 0, shake: 0,
      rot: 0, sx: 1, sy: 1, hv: 1, rootRot: 0, alpha: 1, bright: 1, flashA: 0,
      glowA: 0, glowShown: false, down: false, falling: false, walking: false, pose: FigurePose.Idle, footA: 1, footS: 1, footKeep: false,
      t: { pos: 0, body: 0, shake: 0, flash: 0, glow: 0, heave: 0, fall: 0 },
      drawn: {},
    };
  }
  const hero = makeFigure(PLAYER);
  /** unit → the enemy's figure (scene addition: one per DepictionFrame.Enemies entry). */
  const foes = new Map();
  /** The frame's focus enemy (Frame.Enemy.Unit): the one 'Enemy' means when no unit is named. */
  let focus = 0;
  function foeFig(unit) {
    let f = foes.get(unit);
    if (!f) {
      f = makeFigure(ENEMY, unit);
      foes.set(unit, f);
    }
    return f;
  }
  foeFig(0);
  /** The figure of `side` (and of enemy `unit` when given); null for an enemy the frame never had. */
  function fig(side, unit) {
    if (side === PLAYER) return hero;
    if (side !== ENEMY) return null;
    if (unit != null && unit >= 0) return foes.get(unit) || null;
    return foes.get(focus) || foes.values().next().value || null;
  }
  /** Which way a side steps toward the other: the player faces right, the enemy left. */
  const toward = (side) => (side === PLAYER ? 1 : -1);

  /**
   * FigureView.ShowArt / StandTall: the DS silhouette for the art id, `height` px tall (FigureFrame.HeightFor),
   * with its edge light (§4.3: amber 28% for the player, violet 35% for every enemy, elites and bosses
   * included — review fix: the boss colour and phosphor belong to boss statuses and power numbers) and the
   * foot light under it (2 × the body, 60 tall).
   */
  function dress(f, kind, height, rim) {
    if (f.kind === kind && f.height === height && f.rim === rim) return;
    const def = figureDef(OCB, kind);
    const foe = f.side !== PLAYER;
    const k = height / def.h;
    f.kind = kind;
    f.height = height;
    f.rim = rim;
    f.width = def.body * k;
    f.svgW = def.w * k;
    f.cx = def.cx * k;
    f.el.style.width = f.svgW.toFixed(1) + 'px';
    f.el.style.height = height + 'px';
    f.el.style.transformOrigin = f.cx.toFixed(1) + 'px ' + height + 'px';
    f.body.style.width = f.svgW.toFixed(1) + 'px';
    f.body.style.height = height + 'px';
    f.body.style.transformOrigin = f.cx.toFixed(1) + 'px ' + height + 'px';
    f.art.innerHTML = '';
    f.art.appendChild(drawFigure(def, rim ? { scaleTo: height, rim, foe } : { scaleTo: height, foe }));
    f.flashEl.innerHTML = '';
    f.flashEl.appendChild(drawFigure(def, { scaleTo: height, fill: 'currentColor', rim: 'transparent', foe }));
    f.glowBody.innerHTML = '';
    f.glowBody.appendChild(drawFigure(def, { scaleTo: height, rim: 'transparent', foe, light: BODY_LIGHT }));
    f.halo.style.display = 'none';
    // §4.3 足元の灯り: a radial light in an ellipse under the feet, behind the body.
    if (f.foot) f.foot.remove();
    f.foot = OCB.FootLight(f.width * FigureFrame.FootLightWidthRatio, f.side !== PLAYER);
    f.foot.classList.add('ocbg-battle-foot');
    f.foot.style.left = (f.cx - f.width).toFixed(1) + 'px';
    f.foot.style.top = (height - FigureFrame.FootLightHeight / 2) + 'px';
    f.foot.style.height = FigureFrame.FootLightHeight + 'px';
    f.el.insertBefore(f.foot, f.el.firstChild);
    // StanceGlow's rect: anchors (−0.2, −0.02)〜(1.2, 1.04) of the frame, the frame as wide as the body.
    f.glow.style.left = (f.cx - 0.7 * f.width).toFixed(1) + 'px';
    f.glow.style.width = (1.4 * f.width).toFixed(1) + 'px';
    f.glow.style.top = (-0.04 * height).toFixed(1) + 'px';
    f.glow.style.height = (1.06 * height).toFixed(1) + 'px';
    f.drawn = {};
  }

  function setStyle(f, el, key, prop, value) {
    if (f.drawn[key] === value) return;
    f.drawn[key] = value;
    el.style[prop] = value;
  }

  /** §5.18 順 7 (review 21): the next enemy of a chain comes out of the haze — every enemy figure's opacity (1 at rest). */
  let enemiesAppear = 1;
  function renderFigure(f) {
    setStyle(f, f.el, 'down', 'display', f.down ? 'none' : '');
    if (f.down) return;
    const appear = f.side === PLAYER ? 1 : enemiesAppear;
    setStyle(f, f.el, 'appear', 'opacity', appear >= 1 ? '' : appear.toFixed(3));
    const tx = f.x + f.shake - f.cx;
    const ty = FEET - f.height + f.y;
    setStyle(f, f.el, 'tf', 'transform', 'translate(' + tx.toFixed(1) + 'px,' + ty.toFixed(1) + 'px)' + (f.rootRot ? ' rotate(' + f.rootRot.toFixed(2) + 'deg)' : ''));
    // DefeatShape's fade is the body's (and its stance light's); the foot light fades with it, unless a fall among
    // standing enemies keeps it for its own 300 ms after (battle_ui_ux_v2 §5.15 順 5, fadeFoot).
    setStyle(f, f.body, 'alpha', 'opacity', f.alpha >= 1 ? '' : f.alpha.toFixed(3));
    const footA = (f.footKeep ? 1 : f.alpha) * f.footA;
    if (f.foot) setStyle(f, f.foot, 'footA', 'opacity', footA >= 1 ? '' : footA.toFixed(3));
    if (f.foot) setStyle(f, f.foot, 'footS', 'transform', f.footS >= 1 ? '' : 'scaleX(' + f.footS.toFixed(3) + ')');
    const sy = f.sy * f.hv;
    setStyle(f, f.body, 'btf', 'transform', (f.rot ? 'rotate(' + f.rot.toFixed(2) + 'deg) ' : '') + (f.sx !== 1 || sy !== 1 ? 'scale(' + f.sx.toFixed(3) + ',' + sy.toFixed(3) + ')' : ''));
    setStyle(f, f.body, 'bright', 'filter', f.bright < 1 ? 'brightness(' + f.bright.toFixed(3) + ')' : '');
    setStyle(f, f.flashEl, 'flash', 'opacity', f.flashA > 0 ? f.flashA.toFixed(3) : '0');
    const glowA = f.glowA * f.alpha;
    setStyle(f, f.glow, 'glow', 'opacity', glowA > 0 ? glowA.toFixed(3) : '0');
    const bodyLight = glowA * StanceGlow.Alpha;
    setStyle(f, f.glowBody, 'glowBody', 'opacity', bodyLight > 0 ? bodyLight.toFixed(3) : '0');
  }

  let arenaShake = 0;
  let arenaDrawn = '';
  /** The arena's slide beside an open drawer (stage px; animated on real time, the journal pauses the scene). */
  const shift = { x: 0, dir: 0, to: 0, token: 0 };
  /** The paint order and the depth look of every figure (Z_HERO … FRONT_EDGE). */
  function renderDepth() {
    setStyle(hero, hero.el, 'z', 'zIndex', String(Z_HERO));
    const standing = [...foes.values()].filter((f) => !f.down);
    const several = standing.length > 1;
    standing.sort((a, b) => Math.abs(a.x - hero.x) - Math.abs(b.x - hero.x));
    // Review fix (a back enemy's lunge merged with the front enemy's body): the enemy acting now stands in front of
    // the others until it is home again; the rest keep the nearest-first order behind it.
    const acting = several ? standing.find((f) => f.acting && !f.walking) : null;
    let rank = 0;
    let zRank = acting ? 1 : 0;
    for (const f of standing) {
      const z = f.walking && several ? Z_WALKING_ENEMY : f === acting ? Z_FRONT_ENEMY : Z_FRONT_ENEMY - zRank++;
      setStyle(f, f.el, 'z', 'zIndex', String(z));
      const r = several ? rank : -1;
      setStyle(f, f.art, 'edge', 'filter', r === 0 ? FRONT_EDGE : r > 0 ? 'brightness(' + (1 + BACK_LIFT_STEP * r).toFixed(2) + ')' : '');
      rank += 1;
    }
  }

  function render() {
    renderDepth();
    renderFigure(hero);
    foes.forEach(renderFigure);
    const dx = arenaShake + shift.x;
    const a = Math.abs(dx) > 0.05 ? 'translateX(' + dx.toFixed(1) + 'px)' : '';
    if (a !== arenaDrawn) { arenaDrawn = a; layer.field.style.transform = a; }
  }
  /**
   * How far the arena slides toward `dir` now: ARENA_SHIFT_SHARE — or further (review 21: the trio's third enemy stayed cut by
   * the journal's edge), as far as brings every standing figure's art out from under the drawer (DRAWER_KEEP inside what it
   * leaves) — held short only where a figure's art would leave the screen on the other side.
   */
  function shiftRoom(dir) {
    if (!dir) return 0;
    let want = ARENA_SHIFT_SHARE * L.W;
    let room = Infinity;
    const lo = L.W * DRAWER_SHARE + DRAWER_KEEP;
    const hi = L.W * (1 - DRAWER_SHARE) - DRAWER_KEEP;
    for (const f of [hero, ...foes.values()]) {
      if (f.down) continue;
      const left = f.homeX - f.cx;
      const right = left + f.svgW;
      want = Math.max(want, dir > 0 ? lo - left : right - hi);
      room = Math.min(room, dir > 0 ? L.W - SCREEN_KEEP - right : left - SCREEN_KEEP);
    }
    return Math.max(0, Math.min(want, room));
  }
  /** The slide toward `to` (stage px) over ARENA_SHIFT_MS (out), on real time. */
  function slideTo(to) {
    if (Math.abs(to - shift.to) < 0.5) return;
    shift.to = to;
    const mine = ++shift.token;
    const from = shift.x;
    const t0 = env.real.now;
    const step = () => {
      if (mine !== shift.token || !alive) return;
      const t = Math.min(1, (env.real.now - t0) / ARENA_SHIFT_MS);
      const e = 1 - (1 - t) * (1 - t);
      shift.x = from + (to - from) * e;
      render();
      if (t < 1) env.real.wait(16).then(step);
    };
    step();
  }
  const offFrame = clock.onFrame(() => {
    if (shift.dir) slideTo(shift.dir * shiftRoom(shift.dir));
    render();
  });

  /** A run that stops touching the figure once a newer one on the same channel starts. */
  function own(f, channel, ms, ease, fn) {
    const mine = ++f.t[channel];
    return clock.run(ms, ease, (t, raw) => { if (f.t[channel] === mine) fn(t, raw); }).then(() => f.t[channel] === mine);
  }

  /** FigureView.RestBody: no tilt, no stretch, its colour (the flash gone). */
  function restBody(f) {
    f.t.body++;
    f.t.heave++;
    f.t.flash++;
    f.rot = 0;
    f.sx = 1;
    f.sy = 1;
    f.hv = 1;
    f.flashA = 0;
    f.bright = 1;
  }

  /** FigureView.Settle: whatever a stopped effect left on the figure (position is the caller's). */
  function settle(f) {
    f.falling = false;
    f.acting = false;
    f.t.fall++;
    f.t.shake++;
    f.shake = 0;
    f.rootRot = 0;
    f.alpha = 1;
    f.footA = 1;
    f.footS = 1;
    f.footKeep = false;
    f.y = 0;
    restBody(f);
    f.pose = FigurePose.Idle;
    f.el.dataset.pose = f.pose;
  }

  // ---- the enemy-turn shade (EnemyTurnShadeView, §5.5) -------------------------------------------
  const shade = h('div', { class: 'ocbg-battle-shade' });
  shade.style.top = EnemyTurnShadeLayout.Top + 'px';
  shade.style.height = EnemyTurnShadeLayout.Height + 'px';
  // Black from TopAlpha to BottomAlpha (EnemyTurnShadeLayout's values; Unity's gradient is black).
  shade.style.background = 'linear-gradient(180deg, rgba(0,0,0,' + EnemyTurnShadeLayout.TopAlpha + '), rgba(0,0,0,' + EnemyTurnShadeLayout.BottomAlpha.toFixed(2) + '))';
  layer.shade.appendChild(shade);
  let shadeOn = false;

  // ---- the low-HP vignette (battle_ui_ux_v2 §5.6 順 5; scene addition, Unity has none) ---------------------------
  // With the player's HP under 15 a crimson vignette lines the screen's edge at α 0.25, and each blow that lands
  // pulses it 0.5 → 0.25 over 200 ms (out). In the shade's layer: over the field and the hud, under the hand.
  const vignette = h('div', { class: 'ocbg-battle-vignette' });
  layer.shade.appendChild(vignette);
  const skyTop = backdrop.spec && backdrop.spec.Sky && backdrop.spec.Sky[0] ? backdrop.spec.Sky[0].Color : null;
  const lin = (c) => (c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4));
  const brightSky = !!skyTop && 0.2126 * lin(skyTop.R) + 0.7152 * lin(skyTop.G) + 0.0722 * lin(skyTop.B) > BRIGHT_SKY;
  vignette.classList.toggle('is-bright', brightSky);
  const vigK = brightSky ? BRIGHT_BOOST : 1;
  const vig = { a: 0, rest: 0, token: 0 };
  const drawVignette = () => { vignette.style.opacity = vig.a > 0.001 ? vig.a.toFixed(3) : '0'; };
  drawVignette();

  // ---- frame state --------------------------------------------------------------------------------
  let sizes = { player: 1, enemy: 1 };
  let multi = false;
  let lastFrame = null;
  let gapUnit = null;

  function feetX(side, cell, size) {
    const x = floor.feetX(cell, size);
    return x == null ? NO_FLOOR_X[side] : x;
  }
  /**
   * Where figure `f` stands on `cell`: its feet in the middle of its cells (FloorSpec.FeetX; battle_ui_ux_v2 §2.1
   * 「人型の中心 x は、立っているマスの中心」). Review 19: a silhouette wider than its cell is no longer moved in off the
   * stance lists (the hound on the last of 8 cells stood 24 px off its cell, into the enemy beside it); its art may
   * reach behind the stance list's frame, which is drawn over it. f.nudge stays 0 (dev/battle-read.js reads it).
   */
  function standX(f, cell, size) {
    f.nudge = 0;
    return feetX(f.side, cell, size);
  }

  /**
   * How far enemy figure `f` may move toward `dir` before its body meets another standing enemy's (review fix, several
   * enemies: a back enemy's attack lunge — EnemyMotionShape.Reach, 90 px for a thrust — carried it onto the enemy in
   * front, and the two bodies read as one double silhouette while it acted). The lunge stops ALLY_CLEAR short of the
   * ally's body; with no ally in the way (one enemy, or the front enemy) it is not limited.
   */
  function roomToward(f, dir) {
    let room = Infinity;
    if (!multi) return room;
    const edge = f.x + dir * f.width / 2;
    for (const o of foes.values()) {
      if (o === f || o.down || o.falling) continue;
      if ((o.x - f.x) * dir <= 0) continue; // behind it
      const near = o.x - dir * o.width / 2;
      room = Math.min(room, Math.max(0, (near - edge) * dir - ALLY_CLEAR));
    }
    return room;
  }

  /**
   * How far a blow's shape may reach left and right of figure `f`'s chest before it would cross another standing figure
   * (its striker apart): BLOW_CLEAR short of that figure's body, never under BLOW_MIN (review 19, several enemies).
   */
  function blowRoom(f, striker) {
    const room = { left: Infinity, right: Infinity };
    for (const o of [hero, ...foes.values()]) {
      if (o === f || o === striker || o.down || o.falling) continue;
      if (o.x > f.x) room.right = Math.min(room.right, Math.max(BLOW_MIN, o.x - o.width / 2 - f.x - BLOW_CLEAR));
      else room.left = Math.min(room.left, Math.max(BLOW_MIN, f.x - (o.x + o.width / 2) - BLOW_CLEAR));
    }
    return room;
  }

  /** A point of the figure on the screen (the arena's slide included: the effects' layers do not slide). */
  function point(f, frac) {
    return { x: f.x + shift.x, y: FEET + f.y - f.height * frac };
  }
  const chestOf = (f) => point(f, L.CHEST);
  const headOf = (f) => point(f, L.HEAD);
  const feetOf = (f) => ({ x: f.x + shift.x, y: FEET + f.y });
  const NO_POINT = { x: 960, y: 400 };

  /**
   * battle_ui_ux_v2 §7 (review 21; the writer's OmenDisclosure blanks the numbers and the plan, not FloorFrame.AimCells): the
   * enemies read to 開示度 0 — their omen's Disclosure — whose aimed cells the floor does not draw. unit → bool; undefined asks
   * for the frame's one omen.
   */
  function unreadOf(frame) {
    const omens = frame && frame.Omens && frame.Omens.length ? frame.Omens : frame && frame.Omen ? [frame.Omen] : [];
    const low = (o) => !!o && o.Disclosure != null && o.Disclosure < 1;
    return (unit) => {
      if (unit == null) return low(frame.Omen);
      const o = omens.find((x) => x && x.Unit === unit);
      return low(o || (omens.length === 1 ? omens[0] : null));
    };
  }
  /** The enemies a frame describes, with their cells: every one when it carries the list, else its one enemy. */
  function enemiesOf(frame, fl) {
    const list = frame.Enemies && frame.Enemies.length ? frame.Enemies : [frame.Enemy || {}];
    return list.map((u, k) => {
      const unit = u && u.Unit >= 0 ? u.Unit : k;
      const isFocus = u === frame.Enemy || list.length === 1;
      const size = Math.max(1, (u && u.Size) || (isFocus && fl ? fl.EnemySize : 1) || 1);
      const cell = u && u.Cell > 0 ? u.Cell : isFocus && fl ? fl.EnemyCell : 0;
      return { unit, u: u || {}, size, cell };
    });
  }

  /**
   * The enemy a RangeSwitch cue moves, and its cell after (MoveOnFloor / SnapOnFloor). Several enemies (review fix:
   * a card that killed the front enemy and then moved — 牽制, 猪突猛進, 踏み込み斬り — slid the dead enemy onto the
   * next one): only Cue.Unit with its UnitCellAfter moves an enemy (port notes: −1 when the player moved). The C#'s
   * EnemyCellAfter is the writer's focus enemy's cell, and after a kill that is the next enemy's, not the figure the
   * field still calls the focus. One enemy: the frame's enemy and EnemyCellAfter, as Unity.
   */
  function movedEnemyOf(cue) {
    const named = cue.Unit != null && cue.Unit >= 0 && foes.has(cue.Unit);
    if (named) {
      const cell = cue.UnitCellAfter > 0 ? cue.UnitCellAfter : multi ? 0 : cue.EnemyCellAfter;
      return { e: foes.get(cue.Unit), eCell: cell };
    }
    if (multi) return { e: null, eCell: 0 };
    return { e: fig(ENEMY), eCell: cue.EnemyCellAfter };
  }

  // ---- the api ------------------------------------------------------------------------------------
  const api = {
    /**
     * ApplyFrame's field half: art and Down, heights (FigureFrame.HeightFor(size)), the floor (FloorView.Bind),
     * the player on its cells at once, the enemy's HOME on its cells (moved there now only when first),
     * the stance lights snapped when first, the enemy-turn shade.
     */
    applyFrame(frame, o) {
      if (!frame) return;
      const first = !!(o && o.first);
      const fl = frame.Floor || null;
      lastFrame = frame;
      floor.bind(fl, { unread: unreadOf(frame) });
      if (gapUnit != null) api.showGapTo(gapUnit);
      sizes = { player: fl ? Math.max(1, fl.PlayerSize || 1) : 1, enemy: fl ? Math.max(1, fl.EnemySize || 1) : 1 };
      const p = hero;
      dress(p, silhouetteOf(PLAYER, frame.Player && frame.Player.ArtId, sizes.player), FigureFrame.HeightFor(sizes.player), '');
      api.setDown(PLAYER, !!(frame.Player && frame.Player.Down));
      // ApplyFloor: the player stands on its cells now; each enemy's home is its cells (the next return or snap moves it).
      p.homeX = fl && floor.shown ? standX(p, fl.PlayerCell, sizes.player) : NO_FLOOR_X[PLAYER];
      p.t.pos++;
      p.x = p.homeX;
      const list = enemiesOf(frame, fl);
      multi = list.length > 1;
      focus = frame.Enemy && frame.Enemy.Unit >= 0 ? frame.Enemy.Unit : list[0].unit;
      for (const e of list) {
        const f = foeFig(e.unit);
        f.size = e.size;
        dress(f, silhouetteOf(ENEMY, e.u.ArtId, e.size), FigureFrame.HeightFor(e.size), '');
        api.setDown(ENEMY, !!e.u.Down, e.unit);
        f.homeX = fl && floor.shown && e.cell > 0 ? standX(f, e.cell, e.size) : NO_FLOOR_X[ENEMY];
        if (first) {
          f.t.pos++;
          f.x = f.homeX;
          f.y = 0;
          api.setStanceGlow(ENEMY, StanceGlow.IsOn(e.u), e.unit);
        }
      }
      if (first) api.setStanceGlow(PLAYER, StanceGlow.IsOn(frame.Player));
      // Review fix: once the battle is decided the gap number goes (it measured to the fallen enemy's empty cell).
      floor.setGapHidden(frame.Outcome != null && frame.Outcome !== BattleOutcome.Ongoing);
      api.setEnemyTurnShade(!!frame.EnemyTurn);
      render();
    },

    /**
     * battle-visual-v1 §4.2 間合いの数字 with several enemies (port notes, "The one N"): while a held card frames
     * enemy `unit`, the floor's one number is that enemy's N (UnitFrame.Gap) over the empty cells up to its Cell;
     * null puts the frame's own back (Floor.Gap, the nearest standing enemy). With one enemy nothing changes.
     */
    showGapTo(unit) {
      gapUnit = unit != null && unit >= 0 ? unit : null;
      const fr = lastFrame;
      if (!fr || !fr.Floor || !floor.shown || !multi) return;
      const fl = fr.Floor;
      const u = gapUnit != null ? (fr.Enemies || []).find((e) => e && e.Unit === gapUnit) : null;
      if (u && !u.Down && u.Gap >= 0) floor.setGap(u.Gap, fl.PlayerCell, Math.max(1, fl.PlayerSize || 1), u.Cell);
      else floor.setGap(fl.Gap, fl.PlayerCell, Math.max(1, fl.PlayerSize || 1), fl.EnemyCell);
    },

    /** The enemies the field draws (standing or not), by unit. */
    enemyUnits() { return [...foes.keys()]; },

    /**
     * Kept for the contract (docs/BATTLE_SCENE.md §6b): the back enemies used to be drawn from source.State as
     * static extras. Every enemy of DepictionFrame.Enemies now has its own figure (applyFrame), so this draws nothing.
     */
    setExtras() {},

    /**
     * battle_ui_ux_v2 §5.8 提示 (scene addition; Unity redraws the floor with the event's frame): an omen is shown —
     * the floor takes `frame`'s cells now (the event's After), the newly aimed ones drawn left to right, 30 ms a cell
     * (`now`: at once).
     */
    drawAim(frame, unit, now) {
      if (!frame || !frame.Floor) return Promise.resolve();
      floor.bind(frame.Floor, { reveal: { unit, ms: now ? 0 : undefined }, unread: unreadOf(frame) });
      if (gapUnit != null) api.showGapTo(gapUnit);
      return Promise.resolve();
    },
    showReach(cells) { floor.showReach(cells); },
    /** The battle is over: the enemies' aimed / dense / move cells fade over `ms` with their omen badges, and stay clear. */
    retireAim(ms) { return floor.retireAim(ms); },
    /**
     * battle_ui_ux_v2 §5.15 順 5 (review fix): enemy `unit` falls — its aimed, dense and move cells go now, with its omen
     * badge, instead of with the frame after its fall (they outlived the badge and the figure by a second).
     */
    retireAimOf(unit) { floor.retireUnit(unit != null && unit >= 0 ? unit : undefined, !multi); },
    /** battle_ui_ux_v2 §2.2.2: enemy `unit`'s aimed cells alone stay lit (its omen badge is hovered); null for all. */
    focusAim(unit) { floor.focusAim(unit); },
    hideReach() { floor.hideReach(); },

    /**
     * battle_ui_ux_v2 §2.4: the arena slides `dir` × 15 % of the screen (−1 left for the journal, +1 right for
     * the pile list, 0 back) over 220 ms (out), on real time so it runs while the journal holds the scene.
     */
    setShift(dir) {
      shift.dir = Math.max(-1, Math.min(1, dir | 0));
      slideTo(shift.dir * shiftRoom(shift.dir));
    },
    get shiftX() { return shift.x; },
    /** How far figure `side` (enemy `unit`) stands moved in from its cell's middle (standX; 0 for most). */
    nudgeOf(side, unit) { const f = fig(side, unit); return f ? f.nudge || 0 : 0; },

    /** §5.6 順 5: the vignette rests at VIGNETTE_REST while `on` (HP under 15), at 0 otherwise (a 200 ms fade). */
    setLowHp(on) {
      const rest = on ? VIGNETTE_REST * vigK : 0;
      if (rest === vig.rest) return Promise.resolve();
      vig.rest = rest;
      const mine = ++vig.token;
      const from = vig.a;
      return clock.run(VIGNETTE_PULSE_MS, 'out', (t) => { if (mine === vig.token) { vig.a = lerp(from, rest, t); drawVignette(); } });
    },
    /** §5.6 順 5: a blow landed with HP under 15: the vignette pulses VIGNETTE_PEAK → its rest over `ms` (out). */
    pulseLowHp(ms) {
      if (vig.rest <= 0) return Promise.resolve();
      const mine = ++vig.token;
      const peak = Math.min(1, VIGNETTE_PEAK * vigK);
      vig.a = peak;
      drawVignette();
      return clock.run(ms, 'out', (t) => { if (mine === vig.token) { vig.a = lerp(peak, vig.rest, t); drawVignette(); } });
    },

    /**
     * battle_ui_ux_v2 §5.18 順 7 (review 21): a chain's next enemy comes out of the haze — the enemy figures from `from` to 1
     * over `ms` (out). `ms` 0 puts them at `from` (0: hidden until the next call).
     */
    appearEnemies(ms, from = 0) {
      enemiesAppear = from;
      render();
      if (!(ms > 0)) return Promise.resolve();
      return clock.run(ms, 'out', (t) => { enemiesAppear = from + (1 - from) * t; }).then(() => { enemiesAppear = 1; render(); });
    },
    /** EnemyTurnShadeView.Show: the black below y 790 is on or off (SetActive, no fade). */
    setEnemyTurnShade(on) {
      on = !!on;
      if (on === shadeOn) return;
      shadeOn = on;
      shade.classList.toggle('is-on', on);
    },

    // ---- figure state ----
    /** FigureView.SetPose: the silhouette has one picture; the pose is kept for art. A falling figure stays down. */
    setPose(side, pose, unit) {
      const f = fig(side, unit);
      if (!f) return;
      if (f.falling && pose !== FigurePose.Down) return;
      f.pose = pose;
      f.el.dataset.pose = pose;
    },
    /** FigureView.SetDown: gone (hidden whole) or standing, at rest either way. */
    setDown(side, down, unit) {
      const f = fig(side, unit);
      if (!f) return;
      down = !!down;
      if (down === f.down) return;
      f.down = down;
      settle(f);
      render();
    },
    enemyAway(unit) {
      const e = fig(ENEMY, unit);
      if (!e || e.down) return false;
      return Math.abs(e.x - e.homeX) > 0.5 || Math.abs(e.y) > 0.5;
    },
    setEnemyHomeCell(cell, size, unit) {
      const e = fig(ENEMY, unit);
      if (!floor.shown || !e || !(cell > 0)) return;
      e.homeX = standX(e, cell, Math.max(1, size || e.size || sizes.enemy));
    },
    /** SnapHome: onto its home at once, standing. */
    snapHome(side, unit) {
      const f = fig(side, unit);
      if (!f) return;
      f.t.pos++;
      f.x = f.homeX;
      f.y = 0;
      f.walking = false;
      f.acting = false;
      api.setPose(side, FigurePose.Idle, f.unit);
      render();
    },
    stanceGlowShown(side, unit) { const f = fig(side, unit); return !!(f && f.glowShown); },
    /** FigureView.SetStanceGlow: on or off at once; a fade still running stops touching the light. */
    setStanceGlow(side, on, unit) {
      const f = fig(side, unit);
      if (!f) return;
      f.glowShown = !!on;
      f.t.glow++;
      f.glowA = on ? 1 : 0;
      render();
    },
    /** FigureView.FadeStanceGlow: in or out over ms (ease out); a newer call takes the light over. */
    fadeStanceGlow(side, on, ms, unit) {
      const f = fig(side, unit);
      if (!f) return Promise.resolve();
      f.glowShown = !!on;
      const from = f.glowA;
      const to = on ? 1 : 0;
      return own(f, 'glow', ms, 'out', (t) => { f.glowA = lerp(from, to, t); });
    },

    // ---- beats ----
    /** Lunge: Act pose, `px` toward the other side (ease out). The player's home is unchanged. */
    lunge(side, px, ms, unit) {
      const f = fig(side, unit);
      if (!f) return Promise.resolve();
      api.setPose(side, FigurePose.Act, f.unit);
      const from = f.x;
      // An enemy's step in stops short of an ally standing in its way, as its motion does (roomToward).
      const to = from + (side === PLAYER ? px : Math.min(px, roomToward(f, toward(side)))) * toward(side);
      if (side !== PLAYER) f.acting = true;
      return own(f, 'pos', ms, 'out', (t) => { f.x = lerp(from, to, t); });
    },
    /** ReturnHome: back to its home (inout), then standing. */
    returnHome(side, ms, unit) {
      const f = fig(side, unit);
      if (!f) return Promise.resolve();
      const fromX = f.x;
      const fromY = f.y;
      return own(f, 'pos', ms, 'inout', (t) => {
        f.x = lerp(fromX, f.homeX, t);
        f.y = lerp(fromY, 0, t);
      }).then((still) => { if (still) { f.acting = false; api.setPose(side, FigurePose.Idle, f.unit); } });
    },
    /**
     * EnemyMotion: the enemy moves into its action, shaped by its system (EnemyMotionShape): 払 swings
     * across, 突 drives straight in, 打 shoves with its weight, 盾 raises the haft; a step just steps. The
     * stretch is taken on the body's scale at rest (FigureFacing.During; the DS silhouettes are drawn
     * facing the way they face, as art is). The figure stays where the move took it until it returns.
     */
    enemyMotion(system, ms, unit) {
      const f = fig(ENEMY, unit);
      if (!f) return clock.wait(ms);
      const dir = toward(ENEMY);
      const fromX = f.x;
      const reach = Math.min(EnemyMotionShape.Reach(system), roomToward(f, dir));
      const homeScaleX = FigureFacing.HomeScaleX(ENEMY, true);
      api.setPose(ENEMY, FigurePose.Act, f.unit);
      const bodyMine = ++f.t.body;
      // Several enemies: the one acting is drawn in front of the others until it is home again (renderDepth).
      f.acting = true;
      return own(f, 'pos', ms, 'out', (t) => {
        const bell = Math.sin(t * Math.PI);
        f.x = fromX + reach * dir * t;
        f.y = system === StrikeSystem.Shield ? -16 * bell : 0;
        if (f.t.body !== bodyMine) return;
        const s = FigureFacing.During(homeScaleX, system, bell);
        f.sx = s.X;
        f.sy = s.Y;
        if (system === StrikeSystem.Sweep) f.rot = dir * lerp(-18, 14, t) * bell; // wound back, then across
        else if (system === StrikeSystem.Strike) f.rot = dir * 10 * bell; // leaning its weight into the push
        else f.rot = 0;
      }).then(() => {
        if (f.t.body === bodyMine) { f.rot = 0; f.sx = homeScaleX; f.sy = 1; }
      });
    },
    /**
     * Recoil: the struck figure leans away from the blow (7°, 12° when strong) and shakes (UiTween.Shake:
     * 10 px, 20 when strong, 3 round trips) in its Hit pose.
     */
    recoil(side, strong, ms, unit) {
      const f = fig(side, unit);
      if (!f) return Promise.resolve();
      const lean = (strong ? 12 : 7) * (side === PLAYER ? -1 : 1);
      const amp = strong ? 20 : 10;
      api.setPose(side, FigurePose.Hit, f.unit);
      own(f, 'shake', ms, 'linear', (t) => { f.shake = Math.sin(t * Math.PI * 2 * 3) * amp * (1 - t); }).then((still) => { if (still) f.shake = 0; });
      return own(f, 'body', ms, 'out', (t) => { f.rot = lean * Math.sin(t * Math.PI); }).then((still) => {
        if (still) f.rot = 0;
        if (!f.falling) api.setPose(side, FigurePose.Idle, f.unit);
      });
    },
    /** FigureView.Flash: the body blinks the role's colour ('white' the ink, 'omen' the enemy's violet) and back. */
    flash(side, role, ms, unit) {
      const f = fig(side, unit);
      if (!f || f.falling) return Promise.resolve();
      f.flashEl.style.color = tok(shapeToken(role));
      f.flashA = 1;
      return own(f, 'flash', ms, 'out', (t) => { f.flashA = 1 - t; }).then((still) => { if (still) f.flashA = 0; });
    },
    /** Heave (§5.3 息 on a heal): the body rises a little and settles once (y-scale 1 → 1.05 → 1, inout). */
    heave(side, ms, unit) {
      const f = fig(side, unit);
      if (!f) return Promise.resolve();
      return own(f, 'heave', ms, 'inout', (t) => { f.hv = 1 + 0.05 * Math.sin(t * Math.PI); }).then((still) => { if (still) f.hv = 1; });
    },
    /**
     * FigureView.Fall (DefeatShape): from the body at rest, the figure tilts about its feet away from the
     * other side, sinks 40 and darkens to 0.35, fades from 60%, and is gone (its foot light with it).
     */
    fall(side, ms, unit, o) {
      const f = fig(side, unit);
      if (!f) return Promise.resolve();
      restBody(f);
      f.t.shake++;
      f.shake = 0;
      f.falling = true;
      // o.keepFoot (battle_ui_ux_v2 §5.15 順 5, an enemy falling while others stand): the foot light stays lit through
      // the fall, and the figure is not put down at its end — fadeFoot then takes the light out and empties the cell.
      const keepFoot = !!(o && o.keepFoot);
      f.footKeep = keepFoot;
      f.footA = 1;
      f.pose = FigurePose.Down;
      f.el.dataset.pose = f.pose;
      const fromY = f.y;
      const mine = ++f.t.fall;
      return clock.run(ms, 'linear', (t) => {
        if (f.t.fall !== mine) return;
        f.rootRot = -DefeatShape.Tilt(side, t);
        f.y = fromY - DefeatShape.Sink(t);
        f.alpha = DefeatShape.Alpha(t);
        f.bright = DefeatShape.Brightness(t);
      }).then(() => {
        if (f.t.fall !== mine) return;
        f.y = fromY;
        if (keepFoot) { f.alpha = 0; render(); return; }
        api.setDown(side, true, f.unit);
      });
    },
    /**
     * battle_ui_ux_v2 §5.15 順 5 (scene addition with several enemies; review fix): after a fall made with keepFoot, the
     * foot light goes out over `ms` (linear) and the cell is empty — the figure is put down. 0 ms: at once.
     */
    fadeFoot(side, ms, unit) {
      const f = fig(side, unit);
      if (!f) return Promise.resolve();
      const mine = ++f.t.fall;
      f.footKeep = true;
      f.alpha = 0;
      const done = () => { if (f.t.fall === mine) api.setDown(side, true, f.unit); };
      if (!(ms > 0)) { done(); return Promise.resolve(); }
      return clock.run(ms, 'linear', (t) => { if (f.t.fall === mine) f.footA = 1 - t; }).then(done);
    },
    /**
     * battle_ui_ux_v2 §5.19 順 1 (review 19; scene addition): a lost battle — every standing figure's foot light shrinks
     * and goes out over `ms` (in). 0 ms: at once.
     */
    dimFeet(ms) {
      const list = [hero, ...foes.values()].filter((f) => !f.down && f.foot);
      const set = (k) => { list.forEach((f) => { f.footA = k; f.footS = k; }); render(); };
      if (!(ms > 0)) { set(0); return Promise.resolve(); }
      return clock.run(ms, 'in', (t) => set(1 - t));
    },
    /**
     * battle_ui_ux_v2 §3.14 / §5.7 順 5 (scene addition; Unity and the script have no beat for it): a move that went
     * nowhere (the edge of the line, or someone in the way) — the figure leans BUMP_PX the way it meant to go over half
     * of `ms` and comes back over the other half (inout).
     */
    bump(side, dir, ms, unit) {
      const f = fig(side, unit);
      if (!f || f.down || !(ms > 0)) return Promise.resolve();
      const d = dir < 0 ? -1 : 1;
      const mine = ++f.t.shake;
      return clock.run(ms, 'inout', (t) => { if (f.t.shake === mine) f.shake = d * BUMP_PX * Math.sin(t * Math.PI); })
        .then(() => { if (f.t.shake === mine) f.shake = 0; });
    },
    /** ShakeArena: a strong blow shakes the figures' parent (the floor with them) 6 px, 3 round trips. */
    shakeArena(ms) {
      const mine = ++arenaToken;
      return clock.run(ms, 'linear', (t) => {
        if (mine === arenaToken) arenaShake = Math.sin(t * Math.PI * 2 * 3) * 6 * (1 - t);
      }).then(() => { if (mine === arenaToken) arenaShake = 0; });
    },
    /**
     * MoveOnFloor (§4.2 / §5.7 順 1): the player walks to its new cell, an enemy that moved (a push, a pull,
     * its own step) slides to its own, over `ms` (160 a cell, the player's wait); a push starts fast and settles.
     */
    moveOnFloor(cue, cells, ms) {
      const p = hero;
      const { e, eCell } = movedEnemyOf(cue);
      const fromX = p.x;
      const toX = standX(p, cue.PlayerCellAfter, sizes.player);
      const eFrom = e ? e.x : 0;
      const eTo = e && eCell > 0 ? standX(e, eCell, e.size || sizes.enemy) : eFrom;
      const enemyMoves = !!e && (eTo - e.homeX) * (eTo - e.homeX) > 0.25;
      if (enemyMoves) e.homeX = eTo;
      p.homeX = toX; // the panels set off for the new cells as the figures do
      const ease = cue.Pushed ? 'out' : 'inout';
      // Walking (review fix): the number panels and the badge ride with the figure until it arrives (geom.figure's
      // `walking`), and an enemy that walks goes behind the others (renderDepth).
      if (enemyMoves) {
        e.walking = true;
        own(e, 'pos', ms, ease, (t) => { e.x = lerp(eFrom, eTo, t); }).then((still) => { if (still) e.walking = false; });
      }
      const heroMoves = Math.abs(toX - fromX) > 0.5;
      if (heroMoves) p.walking = true;
      return own(p, 'pos', ms, ease, (t) => { p.x = lerp(fromX, toX, t); }).then((still) => { if (still) p.walking = false; return still; });
    },
    /** SnapOnFloor: the walk's end state at once, both figures on their cells. */
    snapOnFloor(cue) {
      const p = hero;
      p.homeX = standX(p, cue.PlayerCellAfter, sizes.player);
      p.t.pos++;
      p.x = p.homeX;
      p.walking = false;
      const { e, eCell } = movedEnemyOf(cue);
      if (e && eCell > 0) {
        e.homeX = standX(e, eCell, e.size || sizes.enemy);
        api.snapHome(ENEMY, e.unit);
      }
      render();
    },
    // ---- 俊敏's choice (battle_ui_ux_v2 §3.8; field/freestep.js) ----
    /** The arrows over the open ways (`ways` = { [-1]: bool, [1]: bool }, the source's) and 留まる on `playerCell`. */
    showFreeStep(playerCell, ways) { return freeStep.show(playerCell, ways); },
    /** The amber edge on mark `dir` (−1, 0, +1; null for none); returns the focus it took. */
    focusFreeStep(dir) { return freeStep.setFocus(dir); },
    hideFreeStep(ms) { return freeStep.hide(ms); },
    get freeStepShown() { return freeStep.shown; },
    get freeStepFocus() { return freeStep.focus; },
    freeStepDirs() { return freeStep.dirs(); },
    freeStepCentre(dir) { return freeStep.markCentre(dir); },
    recountGap(gap, playerCell, playerSize, enemyCell, ms) { return floor.recountGap(gap, playerCell, playerSize, enemyCell, ms); },
    setGap(gap, playerCell, playerSize, enemyCell) { floor.setGap(gap, playerCell, playerSize, enemyCell); },

    // ---- fx (DepictionFx) ----
    /**
     * DepictionFx.Strike across `side`'s chest: the player's blow in ink, the enemy's in its violet. Review 19 (with two or
     * three enemies a 突 on the front enemy ran through the next one's chest to the third one's head, so a single-target blow
     * read as a hit on them all; §5.3 keeps the width of two enemies for an all 払): the shape reaches no further than
     * BLOW_CLEAR short of any other standing figure but its striker (`blowRoom`), and past the struck body only a little
     * (fx.js BLOW_PAST).
     */
    strike(side, o = {}) {
      const f = fig(side, o.unit);
      const striker = o.byPlayer ? hero : (o.source != null && o.source >= 0 ? foes.get(o.source) : null) || [...foes.values()].find((e) => e.acting) || null;
      return fx.strike(f ? chestOf(f) : NO_POINT, {
        colorName: o.byPlayer ? 'ink' : 'violet', tier: o.intensity, towardLeft: !o.byPlayer, system: o.system, ms: o.ms,
        room: f ? blowRoom(f, striker) : null, half: f ? f.width / 2 : 50,
      });
    },
    /**
     * battle_ui_ux_v2 §5.7 順 7 (review 19; scene addition — the writer marks the wall by its event, Unity draws none): `side`
     * (enemy `unit`) was pushed toward `dir` (−1 left, +1 right) into what stopped it. The edge it hit lights for `ms` — the
     * floor's end when it stands on the last cell that way, else the figure in its way flashes white — and the figure
     * takes 打's ripple from that side.
     */
    wallHit(side, dir, ms, unit) {
      const f = fig(side, unit);
      if (!f || f.down) return Promise.resolve();
      const d = dir < 0 ? -1 : 1;
      const fl = lastFrame && lastFrame.Floor;
      let other = null;
      for (const o of [hero, ...foes.values()]) {
        if (o === f || o.down || o.falling || (o.x - f.x) * d <= 0) continue;
        if (!other || Math.abs(o.x - f.x) < Math.abs(other.x - f.x)) other = o;
      }
      const cell = side === PLAYER ? (fl ? fl.PlayerCell : 0) : ((lastFrame && (lastFrame.Enemies || []).find((e) => e && e.Unit === f.unit)) || {}).Cell || (fl ? fl.EnemyCell : 0);
      const size = Math.max(1, f.size || 1);
      const atEnd = !fl || !(cell > 0) ? !other : d < 0 ? cell <= 1 : cell + size - 1 >= fl.Cells;
      const lit = atEnd || !other
        ? floor.edgeFlash(d, ms, shift.x)
        : api.flash(other.side, 'white', ms, other.side === PLAYER ? undefined : other.unit);
      const ripple = fx.strike(chestOf(f), { colorName: 'ink', tier: 2, towardLeft: d > 0, system: StrikeSystem.Strike, ms: Math.max(ms, 200) });
      return Promise.all([lit, ripple]).then(() => {});
    },
    /** DepictionFx.TraitAccent at the point of the blow on `side`; warm when the trait is the player's. */
    traitAccent(side, ms, unit) {
      const f = fig(side, unit);
      return fx.traitAccent(f ? chestOf(f) : NO_POINT, side === ENEMY, ms, side === PLAYER);
    },
    /** DepictionFx.Mark: 盾 ring, 型 crest at the feet (+ body light when crestLight), 気 mark; 息 and blows draw nothing. */
    mark(side, o = {}) {
      const f = fig(side, o.unit);
      if (!f) return Promise.resolve();
      // Review 17 (an elite's 構え: its action step took it 70 px toward the hero, and the crest stamped there stayed off
      // its feet once it stepped back): the crest and the ring ride with the figure for as long as they show.
      return fx.mark({
        chest: chestOf(f), head: headOf(f), feet: feetOf(f), facing: toward(side),
        fx: o.fx, tier: o.intensity, ms: o.ms, crestLight: o.crestLight !== false, warm: side === PLAYER,
        trackX: () => (f.down ? null : f.x + shift.x),
      });
    },
    burst(at, role, size) { if (at) fx.burst(at, shapeToken(role), size); },
    floatText(at, text, role, fontSize, o) { return at ? fx.floatText(at, text, role, fontSize, o) : Promise.resolve(); },
    /**
     * DepictionFx.ShieldBlock, stood 40 px toward the blow from the chest (review fix: square on the chest it hid
     * the whole figure and its number sat under the HP float; the figure's back now shows beside it).
     */
    shieldBlock(side, amountText, ms, unit, o) {
      const f = fig(side, unit);
      return fx.shieldBlock(f ? api.shieldAt(side, unit) : NO_POINT, amountText, ms, { broken: !!(o && o.broken), towardLeft: side === ENEMY });
    },
    /** Where the shield of a blocked blow stands (the Guard float rises from it). */
    shieldAt(side, unit) {
      const f = fig(side, unit);
      if (!f) return NO_POINT;
      const c = chestOf(f);
      return { x: c.x + toward(side) * SHIELD_TOWARD, y: c.y };
    },
    ghost(from, to, o) { return fx.ghost(from, to, o); },

    destroy() {
      alive = false;
      offFrame();
      fx.clear();
      freeStep.destroy();
      floor.destroy();
      backdrop.destroy();
      hero.el.remove();
      foes.forEach((f) => f.el.remove());
      foes.clear();
      shade.remove();
      vignette.remove();
      layer.field.style.transform = '';
    },
  };
  let arenaToken = 0;
  let alive = true;

  // ---- geometry (docs/BATTLE_SCENE.md §2.4) ---------------------------------------------------
  env.geom.figure = (side, unit) => {
    const f = fig(side, unit);
    if (!f) return null;
    return {
      x: f.x + shift.x, feetY: FEET + f.y, height: f.height, width: f.width, homeX: f.homeX + shift.x, top: FEET + f.y - f.height,
      /** The art's own box across (the silhouette's svg, wider than the body for a tail or a spear; review 21: the stance lists keep clear of it). */
      artX0: f.x + shift.x - f.cx, artX1: f.x + shift.x - f.cx + f.svgW,
      chest: point(f, L.CHEST), head: point(f, L.HEAD), unit: f.unit, down: f.down,
      /** It is walking on the floor (MoveOnFloor): what follows it rides with `x`, not `homeX` (already the new cell). */
      walking: !!f.walking,
    };
  };
  /** ReceiverView (#568): the invisible 340×340 square over an enemy's home, its middle at the body's middle. */
  function receiverOf(e) {
    const w = L.RECEIVER.w;
    const ht = L.RECEIVER.h;
    const x = Math.max(EDGE + w / 2, Math.min(L.W - EDGE - w / 2, e.homeX + shift.x));
    const cy = FEET - e.height / 2;
    return new ScreenBox(x - w / 2, cy - ht / 2, w, ht);
  }
  env.geom.receiver = (unit) => {
    const e = fig(ENEMY, unit);
    return e ? receiverOf(e) : null;
  };
  /**
   * Scene addition (several enemies): one receiver per standing enemy, { unit, box }, left to right. Two
   * neighbours' squares would overlap, so each is cut at the middle between their homes.
   */
  env.geom.receivers = () => {
    const list = [...foes.values()].filter((f) => !f.down).sort((a, b) => a.homeX - b.homeX);
    return list.map((f, i) => {
      const box = receiverOf(f);
      let left = box.X;
      let right = box.X + box.Width;
      if (i > 0) left = Math.max(left, (list[i - 1].homeX + f.homeX) / 2 + shift.x);
      if (i < list.length - 1) right = Math.min(right, (f.homeX + list[i + 1].homeX) / 2 + shift.x);
      return { unit: f.unit, box: new ScreenBox(left, box.Y, Math.max(1, right - left), box.Height) };
    });
  };
  /** 俊敏's choice marks as they stand (stage px), [] while none shows (the card detail keeps off them, §3.13). */
  env.geom.freeStepBoxes = () => freeStep.boxes();
  env.geom.cellBox = (cell) => {
    const b = floor.cellBox(cell);
    return b && shift.x ? Object.assign({}, b, { x: b.x + shift.x }) : b;
  };

  render();
  return api;
}
