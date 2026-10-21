// Port of unity-port/unity-project-kit/Assets/View/Backdrop/Script/BackdropMotion.cs
//
// How the backdrop moves (#89): the breath of each light, the specks drifting through the haze, and the
// crossfade from one layer to the next. Pure functions of the time, so the same moment always draws the
// same picture (a frame captured twice matches) and `dotnet test` can pin them.
//
// Floats as in BackdropSpec.js: every C# float result goes through Math.fround, double math stays double.
// MoteAt's two `out float` parameters come back as `{ x, y }`.
import { BackdropSpec } from './BackdropSpec.js';

const f = Math.fround;

/** How long a flash of Flicker.Flash stays lit, out of its period. */
const FlashMs = 140;

/** private static double Fraction(double v) */
function Fraction(v) {
  return v - Math.floor(v);
}

/** private static double Wrap(double v, double span) — C# double % is fmod, as JS %. */
function Wrap(v, span) {
  if (span <= 0) return 0;
  const r = v % span;
  return r < 0 ? r + span : r;
}

export const BackdropMotion = Object.freeze({
  FlashMs,

  /**
   * The share of a haze's alpha lit at `ms`: 1 at the top of a breath, 1 − depth
   * at the bottom, on a cosine so it never jumps. A flash is lit only in its first
   * FlashMs, falling from full to nothing; the rest of the period it is 1 − depth.
   */
  FlickerAlpha(flicker, ms) {
    if (!flicker.Moves) return 1;
    const phase = Fraction(ms / flicker.PeriodMs);
    if (flicker.Flash) {
      const lit = phase * flicker.PeriodMs;
      if (lit >= FlashMs) return f(1 - flicker.Depth);
      return f(1 - f(flicker.Depth * f(lit / FlashMs)));
    }
    const dip = 0.5 - 0.5 * Math.cos(phase * 2.0 * Math.PI);
    return f(1 - f(flicker.Depth * f(dip)));
  },

  /**
   * Where speck `index` is at `ms` on a `width` × `height` screen (0 at the top left): a fixed start
   * drawn from the index, moved at the layer's speed with a slow sway, and wrapped round the edges so
   * the count on screen holds.
   *
   * C#: `void MoteAt(Motes motes, int index, double ms, float width, float height, out float x, out float y)`
   * → returns `{ x, y }`.
   */
  MoteAt(motes, index, ms, width, height) {
    width = f(width);
    height = f(height);
    const seconds = ms / 1000.0;
    const sway = Math.sin(seconds * 0.6 + BackdropMotion.Hash(index, 3) * 6.283) * 12.0;
    const x = f(Wrap(BackdropMotion.Hash(index, 1) * width + motes.SpeedX * seconds + sway, width));
    const y = f(Wrap(BackdropMotion.Hash(index, 2) * height + motes.SpeedY * seconds, height));
    return { x, y };
  },

  /** The share of speck `index`'s alpha lit at `ms`, each speck on its own beat. */
  MoteAlpha(motes, index, ms) {
    if (motes.Twinkle <= 0) return 1;
    const beat = 1.5 + BackdropMotion.Hash(index, 4) * 2.5; // seconds per twinkle
    const dip = 0.5 - 0.5 * Math.cos((ms / 1000.0 / beat + BackdropMotion.Hash(index, 5)) * 2.0 * Math.PI);
    return f(1 - f(motes.Twinkle * f(dip)));
  },

  /**
   * The new layer's share of the screen `elapsedMs` into the switch: 0 → 1 over
   * BackdropSpec.CrossfadeMs, eased so it starts and lands softly.
   */
  Crossfade(elapsedMs) {
    if (elapsedMs <= 0) return 0;
    if (elapsedMs >= BackdropSpec.CrossfadeMs) return 1;
    const t = elapsedMs / BackdropSpec.CrossfadeMs;
    return f(t * t * (3.0 - 2.0 * t));
  },

  /** A fixed number in [0, 1) for `index` and `salt`; no Random, so every run draws the same. */
  Hash(index, salt) {
    // unchecked uint arithmetic: (uint)index * 374761393u + (uint)salt * 668265263u, wrapping at 2^32.
    let h = (Math.imul(index | 0, 374761393) + Math.imul(salt | 0, 668265263)) >>> 0;
    h = Math.imul(h ^ (h >>> 13), 1274126177) >>> 0;
    h = (h ^ (h >>> 16)) >>> 0;
    return (h & 0xffffff) / 0x1000000;
  },
});
