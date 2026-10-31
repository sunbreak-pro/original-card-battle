// A layer's backdrop on a canvas, for the meta screens: the sky gradient, the haze with its breathing
// light and the drifting specks, laid out the way Unity's Backdrop.View.BackdropLayers lays them out
// (shafts 18% of the width apart and 1.6 screens tall, a radial glow Size × height across, specks as
// soft circles). Every value and every motion comes from the ported bridge (BackdropSpec /
// BackdropMotion); this file only paints them.
import { h } from '../common/stage.js';
import { BackdropSpec, HazeShape, Haze, Rgba } from '../../bridge/backdrop/BackdropSpec.js';
import { BackdropMotion } from '../../bridge/backdrop/BackdropMotion.js';
import { rgbaCss, token, hexRgb } from './util.js';

const W = 1920;
const H = 1080;
/** BackdropLayers.ShaftSpacing: how far apart the shafts of one haze stand, as a share of the width. */
const ShaftSpacing = 0.18;
/** Samples of a vertical haze's alpha (BackdropLayers draws a 128-px tall sprite). */
const Samples = 32;

function softDot(color, size) {
  const px = Math.max(4, Math.ceil(size * 2));
  const c = document.createElement('canvas');
  c.width = px; c.height = px;
  const g = c.getContext('2d');
  const grad = g.createRadialGradient(px / 2, px / 2, 0, px / 2, px / 2, px / 2);
  const solid = rgbaCss(color.WithAlpha(1));
  grad.addColorStop(0, solid);
  grad.addColorStop(0.45, solid);
  grad.addColorStop(1, rgbaCss(color.WithAlpha(0)));
  g.fillStyle = grad;
  g.fillRect(0, 0, px, px);
  return c;
}

/**
 * Paints `spec` (a LayerBackdrop from BackdropSpec.For, or one the scene builds from the same
 * types) into a 1920×1080 canvas. opts: { veil: Rgba, vignette: 0..1, moteSpeed: time factor for
 * the specks, className }.
 */
export function createBackdrop(host, spec, opts = {}) {
  const canvas = h('canvas', { class: 'ocbg-meta-backdrop ' + (opts.className || ''), width: W, height: H });
  host.appendChild(canvas);
  const g = canvas.getContext('2d');
  let current = spec;
  let sky = null;
  let dot = null;
  let alpha = 1;
  // The vignette darkens towards --ground, read from the tokens (the canvas cannot read var()).
  const [gr, gg, gb] = hexRgb(token(host, '--ground', '#10141d'));
  const groundRgba = opts.vignetteColor || Rgba.Bytes(gr, gg, gb, 1);

  function build() {
    sky = document.createElement('canvas');
    sky.width = W; sky.height = H;
    const s = sky.getContext('2d');
    const grad = s.createLinearGradient(0, 0, 0, H);
    for (const stop of current.Sky) grad.addColorStop(Math.max(0, Math.min(1, stop.At)), rgbaCss(stop.Color));
    s.fillStyle = grad;
    s.fillRect(0, 0, W, H);
    dot = current.Motes && current.Motes.Count > 0 ? softDot(current.Motes.Color, current.Motes.SizePx) : null;
  }
  build();

  function vertical(haze, lit) {
    const unit = new Haze({ Shape: haze.Shape, Color: new Rgba(1, 1, 1, 1), Y: haze.Y, Size: haze.Size });
    const grad = g.createLinearGradient(0, 0, 0, H);
    for (let i = 0; i <= Samples; i++) {
      const y = i / Samples;
      grad.addColorStop(y, rgbaCss(haze.Color, lit * BackdropSpec.HazeAlphaAt(unit, y)));
    }
    g.fillStyle = grad;
    g.fillRect(0, 0, W, H);
  }

  function radial(haze, lit) {
    const across = haze.Size * H;
    const cx = haze.X * W, cy = haze.Y * H, r = across / 2;
    const grad = g.createRadialGradient(cx, cy, 0, cx, cy, r);
    grad.addColorStop(0, rgbaCss(haze.Color, lit));
    grad.addColorStop(1, rgbaCss(haze.Color, 0));
    g.fillStyle = grad;
    g.fillRect(cx - r, cy - r, across, across);
  }

  function shafts(haze, lit) {
    const width = haze.Size * W;
    const spacing = W * ShaftSpacing;
    for (let i = 0; i < haze.Count; i++) {
      const x = haze.X * W + (i - (haze.Count - 1) / 2) * spacing;
      g.save();
      g.translate(x, H / 2);
      g.rotate((haze.Angle * Math.PI) / 180);
      const grad = g.createLinearGradient(-width / 2, 0, width / 2, 0);
      grad.addColorStop(0, rgbaCss(haze.Color, 0));
      grad.addColorStop(0.25, rgbaCss(haze.Color, lit));
      grad.addColorStop(0.75, rgbaCss(haze.Color, lit));
      grad.addColorStop(1, rgbaCss(haze.Color, 0));
      g.fillStyle = grad;
      g.fillRect(-width / 2, -H * 0.8, width, H * 1.6);
      g.restore();
    }
  }

  function paint(ms) {
    g.globalAlpha = 1;
    g.clearRect(0, 0, W, H);
    g.globalAlpha = alpha;
    g.drawImage(sky, 0, 0);
    for (const haze of current.Hazes) {
      const lit = BackdropMotion.FlickerAlpha(haze.Flicker, ms);
      if (haze.Shape === HazeShape.TopGlow || haze.Shape === HazeShape.Band) vertical(haze, lit);
      else if (haze.Shape === HazeShape.Radial) radial(haze, lit);
      else if (haze.Shape === HazeShape.Shafts) shafts(haze, lit);
    }
    if (dot) {
      const m = current.Motes;
      const t = ms * (opts.moteSpeed ?? 1);
      const half = dot.width / 2;
      for (let i = 0; i < m.Count; i++) {
        const p = BackdropMotion.MoteAt(m, i, t, W, H);
        g.globalAlpha = alpha * m.Color.A * BackdropMotion.MoteAlpha(m, i, t);
        g.drawImage(dot, p.x - half, p.y - half);
      }
      g.globalAlpha = alpha;
    }
    if (opts.veil) {
      g.fillStyle = rgbaCss(opts.veil);
      g.fillRect(0, 0, W, H);
    }
    if (opts.vignette) {
      const grad = g.createRadialGradient(W / 2, H / 2, H * 0.35, W / 2, H / 2, W * 0.62);
      grad.addColorStop(0, rgbaCss(groundRgba, 0));
      grad.addColorStop(1, rgbaCss(groundRgba, opts.vignette));
      g.fillStyle = grad;
      g.fillRect(0, 0, W, H);
    }
    g.globalAlpha = 1;
  }

  return {
    canvas,
    paint,
    get spec() { return current; },
    /** Swaps the painted backdrop (no crossfade: the meta screens change place only between scenes). */
    use(next) { current = next; build(); },
    set alpha(v) { alpha = v; },
    get alpha() { return alpha; },
    destroy() { canvas.remove(); },
  };
}

/** The backdrop of `layer` (1〜7) as the bridge gives it. */
export function layerBackdrop(layer) {
  return BackdropSpec.For(layer);
}
