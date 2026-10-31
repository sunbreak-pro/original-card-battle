// The layer's backdrop behind the battle: a port of unity-project-kit/Assets/View/Backdrop/BackdropLayers.cs
// (the sky gradient, the haze with its breathing light, the drifting specks) drawn in the DOM. Every value
// and every motion comes from the ported BackdropSpec / BackdropMotion; this file only lays them out.
//
// One addition from the design system (README 「背景のテーマ」, layout.md 地平線): from the horizon at y 556
// down the ground is dark, so the floor, the hand and the numbers always stand on dark ground. The earth
// is the ground token laid over the layer's own sky (field.css: 80% at the horizon, 92% at the bottom
// edge), so each layer's ground keeps its hue in shadow, and the light shafts fade out by the horizon
// (DS wakakusa-b: 光の筋は上から 560 px で消える). Motion runs on real time (BackdropMotion is a function of the clock);
// the battle speed and fast-forward do not touch it, as Unity's BackdropLayers reads unscaled time. 演出短縮
// holds it still (battle_ui_ux_v2 §6.2; Unity's BackdropLayers has no 演出短縮 yet).
import { h } from '../../common/stage.js';
import { BackdropSpec, HazeShape } from '../../../bridge/backdrop/BackdropSpec.js';
import { BackdropMotion } from '../../../bridge/backdrop/BackdropMotion.js';
import { rgba } from './art.js';

const W = 1920;
const H = 1080;
/** BackdropLayers.ShaftSpacing: how far apart the shafts of one haze stand, as a share of the width. */
const SHAFT_SPACING = 0.18;
/** DS wakakusa-b 「光の筋は上から 560 px で消える」: the shafts fade out by this depth from the top. */
const SHAFT_FADE_PX = 560;
/** A haze's or a speck's opacity is written again only when it moved this much (review 16: fewer style writes). */
const ALPHA_STEP = 0.004;

/**
 * @param env    the scene env (layer.backdrop, real clock, clock.onFrame)
 * @param layerNo 1..7
 * @returns {{ spec, tile, destroy() }}
 */
export function createBackdrop(env, layerNo) {
  let spec = null;
  try { spec = BackdropSpec.For(layerNo); } catch (err) { console.error(err); }
  const root = h('div', { class: 'ocbg-battle-backdrop' });
  env.layer.backdrop.appendChild(root);
  if (!spec) {
    root.appendChild(h('div', { class: 'ocbg-battle-earth', style: 'top:0;height:1080px' }));
    return { spec: null, tile: null, destroy() { root.remove(); } };
  }

  // ---- the sky: the stops over the whole height, as BackdropLayers' SkySprite (At 0 top → 1 bottom) ----
  const sky = h('div', { class: 'ocbg-battle-sky' });
  sky.style.background = 'linear-gradient(180deg, ' + spec.Sky.map((s) => rgba(s.Color) + ' ' + (s.At * 100).toFixed(2) + '%').join(', ') + ')';
  root.appendChild(sky);

  // ---- the earth (DS addition): dark from the horizon down ----
  root.appendChild(h('div', { class: 'ocbg-battle-earth' }));

  // ---- the hazes, each with its own breath (BackdropMotion.FlickerAlpha) ----
  const hazes = [];
  for (const haze of spec.Hazes || []) {
    for (const el of hazeElements(haze)) {
      root.appendChild(el);
      hazes.push({ el, haze });
    }
  }

  // ---- the specks (BackdropMotion.MoteAt / MoteAlpha) ----
  const motes = [];
  const m = spec.Motes;
  if (m && m.Count > 0) {
    const size = m.SizePx;
    for (let i = 0; i < m.Count; i++) {
      const el = h('i', { class: 'ocbg-battle-mote' });
      el.style.width = size + 'px';
      el.style.height = size + 'px';
      el.style.marginLeft = -size / 2 + 'px';
      el.style.marginTop = -size / 2 + 'px';
      el.style.background = 'radial-gradient(closest-side, ' + rgba(m.Color) + ', ' + rgba(m.Color, 0.6) + ' 60%, transparent)';
      root.appendChild(el);
      motes.push(el);
    }
  }

  // ---- the horizon (layout.md: y 556) ----
  root.appendChild(h('div', { class: 'ocbg-battle-horizon' }));

  /**
   * battle_ui_ux_v2 §6.2 (常時の揺れ): with 演出短縮 on, the flickers and the drift hold still. Each haze
   * stands at the light it keeps between its beats (a breath at its middle, a flash at its rest) and each
   * speck where and as lit as it is at time 0 — the motion functions read at fixed moments.
   */
  const stillAt = (flicker) => (!flicker || !flicker.Moves ? 0 : flicker.Flash ? BackdropMotion.FlashMs : flicker.PeriodMs / 4);
  let drawnStill = false;
  function animate() {
    const still = !!(env.settings && env.settings.shortFx);
    if (still && drawnStill) return;
    drawnStill = still;
    const now = env.real.now;
    for (const hz of hazes) {
      const a = BackdropMotion.FlickerAlpha(hz.haze.Flicker, still ? stillAt(hz.haze.Flicker) : now);
      if (hz.drawn != null && Math.abs(a - hz.drawn) < ALPHA_STEP) continue;
      hz.drawn = a;
      hz.el.style.opacity = a.toFixed(3);
    }
    for (let i = 0; i < motes.length; i++) {
      const t = still ? 0 : now;
      const at = BackdropMotion.MoteAt(m, i, t, W, H);
      motes[i].style.transform = 'translate(' + at.x.toFixed(1) + 'px,' + at.y.toFixed(1) + 'px)';
      motes[i].style.opacity = BackdropMotion.MoteAlpha(m, i, t).toFixed(3);
    }
  }
  animate();
  const off = env.clock.onFrame(animate);

  return {
    spec,
    /** 床のタイル: the fill of a floor cell on this layer (LayerBackdrop.Tile). */
    tile: spec.Tile,
    destroy() {
      off();
      root.remove();
    },
  };
}

/** BackdropLayers.HazeImages: the elements one haze draws, each carrying the haze's colour at its own alpha. */
function hazeElements(haze) {
  const c = haze.Color;
  const out = [];
  switch (haze.Shape) {
    case HazeShape.TopGlow: {
      // Light from above: the colour at the top, gone at Size of the height.
      const el = h('div', { class: 'ocbg-battle-haze' });
      el.style.background = 'linear-gradient(180deg, ' + rgba(c) + ' 0%, ' + rgba(c, 0) + ' ' + (haze.Size * 100).toFixed(2) + '%)';
      out.push(el);
      break;
    }
    case HazeShape.Band: {
      // A level band centred on Y, Size of the height across, soft at both edges.
      const half = haze.Size / 2;
      const el = h('div', { class: 'ocbg-battle-haze' });
      el.style.background = 'linear-gradient(180deg, ' + rgba(c, 0) + ' ' + ((haze.Y - half) * 100).toFixed(2) + '%, '
        + rgba(c) + ' ' + (haze.Y * 100).toFixed(2) + '%, ' + rgba(c, 0) + ' ' + ((haze.Y + half) * 100).toFixed(2) + '%)';
      out.push(el);
      break;
    }
    case HazeShape.Radial: {
      // A soft round glow at (X, Y), Size of the height across, alpha 1 − d.
      const across = haze.Size * H;
      const el = h('div', { class: 'ocbg-battle-haze is-radial' });
      el.style.left = (haze.X * W - across / 2).toFixed(1) + 'px';
      el.style.top = (haze.Y * H - across / 2).toFixed(1) + 'px';
      el.style.width = across.toFixed(1) + 'px';
      el.style.height = across.toFixed(1) + 'px';
      el.style.background = 'radial-gradient(closest-side, ' + rgba(c) + ', ' + rgba(c, 0) + ')';
      out.push(el);
      break;
    }
    case HazeShape.Shafts: {
      // Count shafts of light, Size of the width each, leaning Angle degrees from upright (Unity rotates
      // by −Angle about z, clockwise, which CSS and the canvas write as +Angle), 1.6 × the height long.
      // Review 16 (layers 1, 2 and 4 ran at 15–20 fps): the shafts are painted once into a canvas, the horizon's fade
      // (DS wakakusa-b: 光の筋は上から 560 px で消える) baked in, and only the canvas's opacity moves each frame — a
      // full-screen mask over rotated gradient boxes, its opacity rewritten every frame, was raster work per frame.
      const width = haze.Size * W;
      const spacing = W * SHAFT_SPACING;
      const canvas = h('canvas', { class: 'ocbg-battle-shafts' });
      canvas.width = W;
      canvas.height = H;
      const g = canvas.getContext ? canvas.getContext('2d') : null;
      if (g) {
        for (let i = 0; i < haze.Count; i++) {
          const x = haze.X * W + (i - (haze.Count - 1) / 2) * spacing;
          g.save();
          g.translate(x, H / 2);
          g.rotate(haze.Angle * Math.PI / 180);
          // ShaftSprite: full in the middle half, falling to nothing at the sides.
          const grad = g.createLinearGradient(-width / 2, 0, width / 2, 0);
          grad.addColorStop(0, rgba(c, 0));
          grad.addColorStop(0.25, rgba(c));
          grad.addColorStop(0.75, rgba(c));
          grad.addColorStop(1, rgba(c, 0));
          g.fillStyle = grad;
          g.fillRect(-width / 2, -H * 0.8, width, H * 1.6);
          g.restore();
        }
        g.globalCompositeOperation = 'destination-in';
        const fade = g.createLinearGradient(0, 0, 0, SHAFT_FADE_PX);
        fade.addColorStop(0, 'rgba(0,0,0,1)');
        fade.addColorStop(1, 'rgba(0,0,0,0)');
        g.fillStyle = fade;
        g.fillRect(0, 0, W, H);
        g.globalCompositeOperation = 'source-over';
      }
      out.push(canvas);
      break;
    }
    default:
      break;
  }
  return out;
}
