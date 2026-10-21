// The exploration screen's backdrop (battle-visual-v1 §2.4 / §2.5, Unity BackdropLayers.cs): the
// layer's sky, haze and drifting motes, crossfaded over 400 ms when the layer changes, a veil of the
// ground colour over it, and the miasma shade closing in from the corners. Every value and motion is
// the ported bridge's (BackdropSpec / BackdropMotion / BackdropSwitch / ExplorationBackdropSpec /
// MiasmaLook); this file only lays them out on two canvases.
import { h } from '../common/stage.js';
import { BackdropSpec, HazeShape } from '../../bridge/backdrop/BackdropSpec.js';
import { BackdropMotion } from '../../bridge/backdrop/BackdropMotion.js';
import { BackdropSwitch, Change } from '../../bridge/backdrop/BackdropSwitch.js';
import { ExplorationBackdropSpec } from '../../bridge/backdrop/ExplorationBackdrop.js';
import { MiasmaLook } from '../../bridge/backdrop/MiasmaLook.js';

const W = 1920;
const H = 1080;
/** The place is soft light only, so it is drawn at half resolution and scaled up. */
const RES = 0.5;
/** Unity BackdropLayers.ShaftSpacing: how far apart the shafts of one haze stand, as a share of the width. */
const SHAFT_SPACING = 0.18;
const SHADE_W = 240;
const SHADE_H = 135;

/** A bridge Rgba (0〜1 channels) as a CSS colour, its alpha scaled by `mul`. */
export function rgbaCss(c, mul = 1) {
  const a = Math.max(0, Math.min(1, c.A * mul));
  return `rgba(${Math.round(c.R * 255)},${Math.round(c.G * 255)},${Math.round(c.B * 255)},${a.toFixed(4)})`;
}

function softDot(color) {
  const c = document.createElement('canvas');
  c.width = c.height = 32;
  const g = c.getContext('2d');
  const grad = g.createRadialGradient(16, 16, 0, 16, 16, 16);
  grad.addColorStop(0, rgbaCss(color, 1 / Math.max(0.001, color.A)));
  grad.addColorStop(1, rgbaCss(color, 0));
  g.fillStyle = grad;
  g.fillRect(0, 0, 32, 32);
  return c;
}

/**
 * createBackdrop(host) → { show(layer), setMiasma(percent), setLull(on), frame(now), destroy() }.
 * `host` is a full-stage element; the backdrop fills it from the bottom of the scene's stack.
 */
export function createBackdrop(host) {
  const canvas = h('canvas', { class: 'ocbg-explore-bd', width: W * RES, height: H * RES });
  const veil = h('div', { class: 'ocbg-explore-veil' });
  const lull = h('div', { class: 'ocbg-explore-lull' });
  const shade = h('canvas', { class: 'ocbg-explore-shade', width: SHADE_W, height: SHADE_H });
  host.append(canvas, veil, lull, shade);
  const g = canvas.getContext('2d');
  const sg = shade.getContext('2d');

  // Distance of every shade pixel from the centre, as a share of the half diagonal (MiasmaLook.CornerDistance).
  const dist = new Float32Array(SHADE_W * SHADE_H);
  for (let y = 0; y < SHADE_H; y++) {
    for (let x = 0; x < SHADE_W; x++) dist[y * SHADE_W + x] = MiasmaLook.CornerDistance((x + 0.5) / SHADE_W, (y + 0.5) / SHADE_H, W / H);
  }
  const shadeImg = sg.createImageData(SHADE_W, SHADE_H);

  const sw = new BackdropSwitch();
  let shown = null;
  let leaving = null;
  let miasmaTarget = 0;
  let miasmaFrom = 0;
  let miasmaAt = 0;
  let lastShadeKey = '';

  function slot(layer) {
    const spec = BackdropSpec.For(layer);
    return { spec, dot: softDot(spec.Motes.Color) };
  }

  function show(layer, now = performance.now()) {
    const change = sw.Show(layer, now);
    if (change === Change.None) return false;
    if (change === Change.FadeDroppingOldest) leaving = null;
    leaving = shown;
    shown = slot(sw.Shown);
    // The veil follows the layer (ExplorationBackdropSpec.For(layer).Veil) with the same 400 ms crossfade.
    veil.style.backgroundColor = rgbaCss(ExplorationBackdropSpec.For(sw.Shown).Veil);
    return true;
  }

  function drawSlot(s, alpha, now) {
    const spec = s.spec;
    g.globalAlpha = alpha;
    // sky
    const sky = g.createLinearGradient(0, 0, 0, H * RES);
    for (const stop of spec.Sky) sky.addColorStop(Math.max(0, Math.min(1, stop.At)), rgbaCss(stop.Color));
    g.fillStyle = sky;
    g.fillRect(0, 0, W * RES, H * RES);
    // haze, each at its breath (BackdropMotion.FlickerAlpha)
    for (const haze of spec.Hazes) {
      const lit = BackdropMotion.FlickerAlpha(haze.Flicker, now);
      if (haze.Shape === HazeShape.TopGlow || haze.Shape === HazeShape.Band) {
        const grad = g.createLinearGradient(0, 0, 0, H * RES);
        const base = haze.Color.WithAlpha(1);
        for (let i = 0; i <= 20; i++) {
          const y = i / 20;
          grad.addColorStop(y, rgbaCss(base, BackdropSpec.HazeAlphaAt(haze, y) * lit));
        }
        g.fillStyle = grad;
        g.fillRect(0, 0, W * RES, H * RES);
      } else if (haze.Shape === HazeShape.Radial) {
        const r = (haze.Size * H * RES) / 2;
        const cx = haze.X * W * RES;
        const cy = haze.Y * H * RES;
        const grad = g.createRadialGradient(cx, cy, 0, cx, cy, r);
        grad.addColorStop(0, rgbaCss(haze.Color, lit));
        grad.addColorStop(1, rgbaCss(haze.Color, 0));
        g.fillStyle = grad;
        g.fillRect(cx - r, cy - r, r * 2, r * 2);
      } else if (haze.Shape === HazeShape.Shafts) {
        const width = haze.Size * W * RES;
        const spacing = W * RES * SHAFT_SPACING;
        const tall = H * RES * 1.6;
        for (let i = 0; i < haze.Count; i++) {
          const x = haze.X * W * RES + (i - (haze.Count - 1) / 2) * spacing;
          g.save();
          g.translate(x, (H * RES) / 2);
          // Unity turns the shaft by Euler z = −Angle (y up): clockwise on a y-down canvas.
          g.rotate((haze.Angle * Math.PI) / 180);
          const grad = g.createLinearGradient(-width / 2, 0, width / 2, 0);
          grad.addColorStop(0, rgbaCss(haze.Color, 0));
          grad.addColorStop(0.25, rgbaCss(haze.Color, lit));
          grad.addColorStop(0.75, rgbaCss(haze.Color, lit));
          grad.addColorStop(1, rgbaCss(haze.Color, 0));
          g.fillStyle = grad;
          g.fillRect(-width / 2, -tall / 2, width, tall);
          g.restore();
        }
      }
    }
    // motes (BackdropMotion.MoteAt / MoteAlpha), in reference pixels
    const motes = spec.Motes;
    const size = motes.SizePx * RES * 2.2;
    for (let i = 0; i < motes.Count; i++) {
      const p = BackdropMotion.MoteAt(motes, i, now, W, H);
      g.globalAlpha = alpha * motes.Color.A * BackdropMotion.MoteAlpha(motes, i, now);
      g.drawImage(s.dot, p.x * RES - size / 2, p.y * RES - size / 2, size, size);
    }
    g.globalAlpha = 1;
  }

  function drawShade(percent, now) {
    const sh = MiasmaLook.ShadeAt(percent, now);
    const key = sh.Strength.toFixed(4) + '/' + sh.InnerRadius.toFixed(4);
    if (key === lastShadeKey) return;
    lastShadeKey = key;
    if (sh.Strength <= 0) {
      sg.clearRect(0, 0, SHADE_W, SHADE_H);
      return;
    }
    const sc = MiasmaLook.ShadeColor;
    const se = MiasmaLook.SeepColor;
    const d = shadeImg.data;
    for (let i = 0; i < dist.length; i++) {
      const a1 = MiasmaLook.ShadeAlphaAt(sh, dist[i]);
      const a2 = sh.SeepAlpha * MiasmaLook.EdgeFalloff(sh, dist[i]);
      const out = a2 + a1 * (1 - a2);
      const k = i * 4;
      if (out <= 0) { d[k + 3] = 0; continue; }
      d[k] = Math.round(((se.R * a2 + sc.R * a1 * (1 - a2)) / out) * 255);
      d[k + 1] = Math.round(((se.G * a2 + sc.G * a1 * (1 - a2)) / out) * 255);
      d[k + 2] = Math.round(((se.B * a2 + sc.B * a1 * (1 - a2)) / out) * 255);
      d[k + 3] = Math.round(out * 255);
    }
    sg.putImageData(shadeImg, 0, 0);
  }

  return {
    show,
    get layer() { return sw.Shown; },
    /** The gauge's reading the shade follows; it eases there with MiasmaLook.Rise like the gauge. */
    setMiasma(percent, now = performance.now(), instant = false) {
      if (percent === miasmaTarget && !instant) return;
      miasmaFrom = instant ? percent : this.miasmaNow(now);
      miasmaTarget = percent;
      miasmaAt = now;
    },
    miasmaNow(now = performance.now()) {
      return MiasmaLook.Rise(miasmaFrom, miasmaTarget, now - miasmaAt);
    },
    setLull(on) { lull.classList.toggle('is-on', !!on); },
    frame(now) {
      if (!shown) return;
      const a = sw.Tick(now);
      if (leaving && sw.Leaving === 0) leaving = null;
      if (leaving) drawSlot(leaving, 1, now);
      drawSlot(shown, leaving ? a : 1, now);
      drawShade(this.miasmaNow(now), now);
    },
    destroy() { canvas.remove(); veil.remove(); lull.remove(); shade.remove(); },
  };
}

/** A small strip of a layer's sky and haze (the interlude's look ahead at the next layer). */
export function skyStrip(layer, w, ht) {
  const c = h('canvas', { width: w, height: ht, class: 'ocbg-explore-strip' });
  const g = c.getContext('2d');
  const spec = BackdropSpec.For(layer);
  const sky = g.createLinearGradient(0, 0, 0, ht);
  for (const stop of spec.Sky) sky.addColorStop(Math.max(0, Math.min(1, stop.At)), rgbaCss(stop.Color));
  g.fillStyle = sky;
  g.fillRect(0, 0, w, ht);
  for (const haze of spec.Hazes) {
    if (haze.Shape === HazeShape.Radial) {
      const r = (haze.Size * ht) / 2;
      const grad = g.createRadialGradient(haze.X * w, haze.Y * ht, 0, haze.X * w, haze.Y * ht, r);
      grad.addColorStop(0, rgbaCss(haze.Color));
      grad.addColorStop(1, rgbaCss(haze.Color, 0));
      g.fillStyle = grad;
      g.fillRect(0, 0, w, ht);
    } else if (haze.Shape === HazeShape.Shafts) {
      for (let i = 0; i < haze.Count; i++) {
        const x = haze.X * w + (i - (haze.Count - 1) / 2) * w * SHAFT_SPACING;
        g.save();
        g.translate(x, ht / 2);
        g.rotate((haze.Angle * Math.PI) / 180);
        const sw = Math.max(4, haze.Size * w);
        const grad = g.createLinearGradient(-sw / 2, 0, sw / 2, 0);
        grad.addColorStop(0, rgbaCss(haze.Color, 0));
        grad.addColorStop(0.5, rgbaCss(haze.Color));
        grad.addColorStop(1, rgbaCss(haze.Color, 0));
        g.fillStyle = grad;
        g.fillRect(-sw / 2, -ht, sw, ht * 2);
        g.restore();
      }
    } else {
      const grad = g.createLinearGradient(0, 0, 0, ht);
      const base = haze.Color.WithAlpha(1);
      for (let i = 0; i <= 10; i++) grad.addColorStop(i / 10, rgbaCss(base, BackdropSpec.HazeAlphaAt(haze, i / 10)));
      g.fillStyle = grad;
      g.fillRect(0, 0, w, ht);
    }
  }
  // the same ground veil the map would have, so the strip reads as "that place, seen from here"
  g.fillStyle = rgbaCss(ExplorationBackdropSpec.For(layer).Veil, 0.6);
  g.fillRect(0, 0, w, ht);
  return c;
}
