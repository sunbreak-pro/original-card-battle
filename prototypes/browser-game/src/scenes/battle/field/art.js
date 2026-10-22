// The field's colours and silhouettes (docs/BATTLE_SCENE.md §6b). Every colour is a design-system token
// (tokens.css) or, for the layer's backdrop, a BackdropSpec value handed over as data; this file only
// turns Unity's BattleTheme roles into the tokens and the sprite shapes of ProceduralArt into CSS.
import { FigureFrame } from '../../../bridge/depictionScript/FigureMotion.js';

/** A token, optionally at an alpha (Unity's BattleTheme.WithAlpha), as a CSS colour. */
export function tok(name, alpha) {
  const v = 'var(--' + name + ')';
  if (alpha == null || alpha >= 1) return v;
  const pct = Math.max(0, Math.min(100, alpha * 100));
  return 'color-mix(in srgb, ' + v + ' ' + pct.toFixed(2) + '%, transparent)';
}

/** A BackdropSpec Rgba (0..1 channels) as a CSS colour, with its alpha times `mult`. */
export function rgba(c, mult = 1) {
  if (!c) return 'transparent';
  const a = (c.A == null ? 1 : c.A) * mult;
  return 'rgba(' + Math.round(c.R * 255) + ',' + Math.round(c.G * 255) + ',' + Math.round(c.B * 255) + ',' + a.toFixed(4) + ')';
}

/**
 * ProceduralArt.Glow: a radial light whose alpha falls as (1 − r)², stretched to its box. `color(a)`
 * returns the CSS colour at alpha a.
 */
export function glowGradient(color, alpha) {
  const stops = [0, 0.25, 0.5, 0.75].map((r) => color(alpha * (1 - r) * (1 - r)) + ' ' + r * 100 + '%');
  return 'radial-gradient(closest-side, ' + stops.join(', ') + ', transparent 100%)';
}

/**
 * Unity's BattleTheme role names (as the player hands them to the field) → the token of a shape drawn in
 * that role. BattleTheme.Omen is violet, Warm / Accent amber, White and Ink the near-white ink.
 */
const SHAPE = {
  ink: 'ink', white: 'ink', omen: 'violet', guard: 'guard', warm: 'amber', heal: 'hp-text', whiff: 'miss', ink2: 'ink-muted',
};
export function shapeToken(role) {
  return SHAPE[role] || 'ink';
}

/**
 * The silhouette the figure wears until standing art exists (FigureView wears ProceduralArt's when the
 * shelf has no art for the id). The design system draws five (OCB.FIGURES); which one an enemy wears, and
 * what the scene adds to or takes from it, is a scene choice by the roster (review fix: six enemies wore the
 * same spearman, the elite hound the normal hound's shape and the two last bosses shapes of others):
 * the crossbowman, the archer and the twin-blade soldier put the spear down for their own weapons, the
 * crystal spear carries a crystal head, the armoured soldier its plates, the unyielding elite a shield, the
 * pack leader a mane, 獄竜 ガルド an angler's lure, and 歪みの根 a root of its own. Coordinates are the
 * DS figure's own box (facing left, the feet on its bottom edge); every addition stays inside the box, so the
 * badge 14 px over the head and the hooks round the body stand where they did.
 */

/** A bar `w` wide from (x1, y1) to (x2, y2), as a closed path (the DS poly()). */
function bar(x1, y1, x2, y2, w) {
  const dx = x2 - x1;
  const dy = y2 - y1;
  const len = Math.hypot(dx, dy) || 1;
  const nx = (-dy / len) * (w / 2);
  const ny = (dx / len) * (w / 2);
  const p = (x, y) => x.toFixed(1) + ' ' + y.toFixed(1);
  return 'M' + p(x1 + nx, y1 + ny) + 'L' + p(x2 + nx, y2 + ny) + ' ' + p(x2 - nx, y2 - ny) + ' ' + p(x1 - nx, y1 - ny) + 'Z';
}
/** A round part of radius r at (x, y). */
function disc(x, y, r) {
  return 'M' + (x - r) + ' ' + y + 'A' + r + ' ' + r + ' 0 1 0 ' + (x + r) + ' ' + y + 'A' + r + ' ' + r + ' 0 1 0 ' + (x - r) + ' ' + y + 'Z';
}

/** The spearman's spear (its 8th and 9th parts: the shaft and the head). */
const SPEAR = [7, 8];
/**
 * The bosses' serpent (review 21: the DS serpent's neck met its coil with a notch and a spike, its coil was a thin ring,
 * and the boss carried less mass than the hero): the DS head and tongue on a thick neck that runs into two solid coils
 * stacked on the ground, a hood behind the head and a curled tail, the coils parted by a line in the edge light. A scene
 * shape until the boss art lands (#92); the head stays where the DS has it, so 獄竜's lure still hangs before the jaws.
 */
const SERPENT_DEF = { w: 220, h: 234, body: 184, cx: 124 };
const SERPENT_PATHS = [
  'M58 8C72 0 92 4 98 18L102 34 84 42 62 38 42 30C40 20 46 12 58 8Z',
  'M44 22L18 26 22 34 44 32Z',
  'M84 10C104 2 124 14 126 36C127 54 116 66 102 72L94 42Z',
  'M66 34C56 70 82 96 86 120C90 138 84 150 90 162L138 158C130 140 128 118 122 96C114 66 112 44 100 28Z',
  'M60 168A72 25 0 1 0 204 168A72 25 0 1 0 60 168Z',
  'M30 206A94 28 0 1 0 218 206A94 28 0 1 0 30 206Z',
  'M198 194C216 180 222 158 212 140C206 130 194 128 188 133C200 142 204 158 192 176Z',
];
const SERPENT_DETAILS = ['M66 182C96 198 160 199 186 188', 'M92 152C106 162 124 162 136 154', 'M70 60C80 62 92 60 100 54'];
const VARIANTS = {
  polearm_warped: { kind: 'spearman' },
  // 瘴甲: plates standing off its back and shoulder, a crest on the helm
  rusted_revenant: { kind: 'spearman', add: [
    'M132 54L152 46 141 66Z', 'M137 76L156 72 141 88Z', 'M134 98L152 100 136 110Z', 'M112 8L118 0 124 10Z', 'M86 52L94 42 104 52Z',
  ] },
  // 灰弩: a crossbow held level at the front hand
  crossbow_hunter: { kind: 'spearman', drop: SPEAR, add: [
    bar(30, 101, 110, 101, 8), 'M44 78Q28 101 44 124L38 126Q20 101 38 76Z', bar(42, 79, 42, 123, 2), 'M30 96L16 101 30 106Z',
  ] },
  // 燐弓: a tall bow at the front hand, an arrow on the string
  mist_archer: { kind: 'spearman', drop: SPEAR, add: [
    'M62 36Q16 104 62 172L55 175Q6 104 55 33Z', bar(60, 37, 60, 171, 2), bar(28, 104, 104, 104, 3), 'M30 98L14 104 30 110Z',
  ] },
  // 燐刃: a blade in each hand, one low and forward, one raised
  twin_blade_warped: { kind: 'spearman', drop: SPEAR, add: [
    bar(64, 107, 14, 136, 6), 'M17 130L2 146 12 140Z', bar(104, 100, 54, 44, 6), 'M57 47L46 30 51 49Z',
  ] },
  // 晶槍: the spear's head is a crystal
  polearm_crystal: { kind: 'spearman', drop: [8], add: ['M34 146L6 160 0 190 22 178Z', 'M42 142L30 130 24 146Z', 'M26 170L12 168 14 182Z'] },
  shadow_hound: { kind: 'hound' },
  // 統牙 (elite): a mane of spines down its back
  pack_alpha: { kind: 'hound', add: ['M92 62L100 40 112 62Z', 'M116 60L128 34 138 60Z', 'M142 61L156 38 164 64Z', 'M168 66L182 48 188 74Z', 'M66 52L74 30 82 52Z'] },
  // 不退 (elite): a heater shield on the front arm, the spear behind it (review fix: a round disc on the shaft
  // read as a mace). Its rim is drawn inside the silhouette in the edge light's colour (`details`).
  polearm_unyielding: { kind: 'spearman', add: ['M28 78H82V106C82 128 69 141 55 150C41 141 28 128 28 106Z'],
    details: ['M34 84H76V106C76 124 66 134 55 142C44 134 34 124 34 106Z', 'M55 86V138'] },
  armored_warden: { kind: 'warden' },
  miasma_priest: { def: SERPENT_DEF, paths: SERPENT_PATHS, details: SERPENT_DETAILS },
  // 獄竜 ガルド: an angler's lure hanging before the jaws, spines under them
  abyss_angler: { def: SERPENT_DEF, details: SERPENT_DETAILS, paths: SERPENT_PATHS.concat([
    'M72 8C60 0 40 0 26 10L29 15C42 7 58 7 68 14Z', disc(21, 21, 8), 'M28 34L20 50 34 40Z', 'M40 38L36 54 46 42Z', 'M54 40L52 56 60 42Z',
  ]) },
  // 歪みの根 (the last boss; scene-drawn, no DS figure is a root). Review 21 (a bare sapling of square-ended bars and a
  // 30 px trunk, lighter than the hero beside it): a gnarled trunk 60–90 px thick flaring into roots, branches that taper
  // to points, a crown of twisted shoots, and a knot like an eye in the edge light.
  distortion_root: { def: { w: 220, h: 264, body: 170, cx: 110 }, details: [
    'M92 166C92 154 116 152 118 164C120 178 94 180 92 166Z', 'M100 166C102 162 108 162 110 166', 'M80 214C88 200 92 188 90 176', 'M134 216C128 202 126 190 128 178',
  ], paths: [
    'M70 264C62 238 76 214 74 190C72 160 88 138 84 112C82 90 92 66 104 46L126 50C120 70 124 92 130 112C138 140 124 164 132 192C138 218 156 240 150 264Z',
    'M24 264C40 248 58 238 76 232L90 248C70 252 50 258 40 264Z',
    'M196 264C182 250 162 240 142 236L130 250C150 254 168 258 178 264Z',
    'M52 264C70 240 88 228 110 228C134 228 152 242 168 264Z',
    'M88 122C66 116 48 98 36 72C32 62 24 54 14 50C28 48 40 56 48 66C60 88 78 102 100 108Z',
    'M84 156C62 154 42 162 24 178C36 158 56 146 82 142Z',
    'M120 96C142 84 162 66 174 40C178 30 188 22 200 20C190 30 186 40 182 50C172 76 152 96 126 110Z',
    'M128 134C152 130 176 138 196 154C176 146 152 144 130 148Z',
    'M104 50C96 34 94 18 100 2C106 18 112 32 116 46Z',
    'M118 52C124 34 136 20 152 10C144 24 134 38 126 56Z',
    'M100 58C86 44 70 38 54 38C70 30 90 34 106 48Z',
    'M66 90C58 84 50 82 40 84C48 76 60 76 70 82Z',
    'M150 74C160 70 170 72 178 78C168 80 160 80 152 82Z',
  ] },
};

/** The DS silhouette for a figure: the player is the swordsman; an enemy by its id, else by its size. */
export function silhouetteOf(side, artId, size) {
  if (side === 'Player') return 'swordsman';
  if (artId && VARIANTS[artId]) return artId;
  return size >= 2 ? 'warden' : 'spearman';
}

/**
 * The figure a silhouette key draws: { w, h, body, cx, paths } in its own box. A key is a DS figure
 * (OCB.FIGURES) or a roster id of VARIANTS (a DS figure with parts taken off and added, or a shape of its own).
 */
export function figureDef(OCB, key) {
  const v = VARIANTS[key];
  if (v && v.def) return Object.assign({}, v.def, { paths: v.paths.slice(), details: (v.details || []).slice() });
  const base = OCB.FIGURES[(v && v.kind) || key] || OCB.FIGURES.swordsman;
  const drop = new Set((v && v.drop) || []);
  const paths = base.paths.filter((d, i) => !drop.has(i)).concat((v && v.add) || []);
  return { w: base.w, h: base.h, body: base.body, cx: base.cx, paths, details: (v && v.details) || [] };
}

let lightIds = 0;
/** Unity's Outline (FigureView edge light): four copies of the shape offset by (±d, ±d) screen px. */
const EDGE_OFFSETS = [[1, 1], [1, -1], [-1, 1], [-1, -1]];

/**
 * OCB.Figure for a figure def, scaled to `scaleTo` tall, with its edge light drawn as Unity's Outline
 * component draws it (FigureView.BuildEdgeLight / FitEdgeLight): four copies of the silhouette in the edge
 * colour, offset by (±2, ±2) screen px (FigureFrame.EdgeLightWidth), under the fill. Where the copies
 * overlap their alphas compound, so the 2 px edge reads at about 50–65 % of the colour, as in the build
 * (review fix: a single 4 px stroke at one alpha left about 2 px at 28 %, and the figures sank into the
 * dark layers' skies). `details` (a variant's inner lines, e.g. a shield's rim) are stroked in the edge
 * colour over the fill. o = { fill, rim, scaleTo, foe }; rim 'transparent' draws the fill alone.
 */
export function drawFigure(def, o = {}) {
  const NS = 'http://www.w3.org/2000/svg';
  const fill = o.fill || (o.foe ? 'var(--figure-enemy)' : 'var(--figure-self)');
  const rim = o.rim || (o.foe ? tok('violet', 0.35) : tok('amber', 0.28));
  const s = document.createElementNS(NS, 'svg');
  s.setAttribute('width', String(o.scaleTo ? def.w * o.scaleTo / def.h : def.w));
  s.setAttribute('height', String(o.scaleTo || def.h));
  s.setAttribute('viewBox', '0 0 ' + def.w + ' ' + def.h);
  s.setAttribute('class', 'ocb-figure-svg');
  s.style.overflow = 'visible';
  s.style.display = 'block';
  // Screen px → the figure's own units (the edge stays 2 px on the screen at every height).
  const unit = o.scaleTo ? def.h / o.scaleTo : 1;
  const d = FigureFrame.EdgeLightWidth * unit;
  const shape = (g) => { for (const p of def.paths) { const a = document.createElementNS(NS, 'path'); a.setAttribute('d', p); g.appendChild(a); } };
  if (rim !== 'transparent') {
    for (const [sx, sy] of EDGE_OFFSETS) {
      const gr = document.createElementNS(NS, 'g');
      gr.setAttribute('fill', rim);
      gr.setAttribute('transform', 'translate(' + (sx * d).toFixed(3) + ' ' + (sy * d).toFixed(3) + ')');
      shape(gr);
      s.appendChild(gr);
    }
  }
  const gf = document.createElementNS(NS, 'g');
  if (o.light) {
    // Review 21 (the stance's body light, §5.11 順 2 「身体に薄い光」, read as a flat maroon cut-out): a light, not a fill —
    // the stance colour strongest at the chest and gone before the silhouette's edge, so the figure's own edge light
    // stays (Unity's FigureView.BuildStanceGlow is a radial ProceduralArt.Glow). In the figure's own units.
    const id = 'ocbg-light-' + (++lightIds);
    const defs = document.createElementNS(NS, 'defs');
    const grad = document.createElementNS(NS, 'radialGradient');
    grad.setAttribute('id', id);
    grad.setAttribute('gradientUnits', 'userSpaceOnUse');
    grad.setAttribute('cx', String(def.cx));
    grad.setAttribute('cy', String(def.h * (1 - o.light.at)));
    grad.setAttribute('r', String(def.h * o.light.r));
    for (const [at, a] of o.light.stops) {
      const st = document.createElementNS(NS, 'stop');
      st.setAttribute('offset', String(at));
      st.setAttribute('stop-color', o.light.color);
      st.setAttribute('stop-opacity', String(a));
      grad.appendChild(st);
    }
    defs.appendChild(grad);
    s.appendChild(defs);
    gf.setAttribute('fill', 'url(#' + id + ')');
  } else gf.setAttribute('fill', fill);
  shape(gf);
  s.appendChild(gf);
  if (rim !== 'transparent' && def.details && def.details.length) {
    const gd = document.createElementNS(NS, 'g');
    gd.setAttribute('fill', 'none');
    gd.setAttribute('stroke', rim);
    gd.setAttribute('stroke-width', (2 * unit).toFixed(3));
    gd.setAttribute('stroke-linejoin', 'round');
    for (const p of def.details) { const a = document.createElementNS(NS, 'path'); a.setAttribute('d', p); gd.appendChild(a); }
    s.appendChild(gd);
  }
  return s;
}

/** ProceduralArt.Shield's outline (straight top, pointed bottom) in a 64×72 box, as an SVG path. */
export const SHIELD_PATH = 'M3 2 H61 V31 C61 50 46 63 32 70 C18 63 3 50 3 31 Z';
