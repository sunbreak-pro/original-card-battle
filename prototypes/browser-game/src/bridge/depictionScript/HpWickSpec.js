// Port of unity-project-kit/Assets/View/Depiction/Script/HpWickSpec.cs
// The wick stripe on an enemy's HP bar while a card is held (battle-visual-v1 §4.5 HP の減る分, §5.2,
// #455). Pure arithmetic with no engine types, like FloorSpec and StatusPanelLayout, so the EditMode
// tests hold it directly. How much HP the card takes off is the script's (CardFace.HpLossFor); this
// only says where on the bar that amount is drawn and what the stripe tile looks like.
//
// The look is the mockup's `repeating-linear-gradient(135deg, #ffd89a 0 3px, transparent 3px 6px)`:
// a 135° gradient runs towards the bottom-right, so its stripes run up to the right (／) on screen,
// 3 px of wick and 3 px of gap.
//
// Port notes:
// - Every C# `float` is a single (Math.fround on every float operation); Sqrt2 and the tile's
//   arithmetic stay double where the C# writes double. `(int)Math.Round(x)` is banker's rounding.
// - Span's bool return and two out parameters are returned as `{ ok, from, to }`.
import { roundEven } from '../../shared/cs.js';

const f = Math.fround;

const Sqrt2 = 1.4142135623730951;

function Clamp01(value) {
  return value < 0 ? f(0) : value > 1 ? f(1) : value;
}

export const HpWickSpec = {
  /** §4.5: the stripe's angle in the CSS sense (the gradient's direction); the stripes run ／. */
  AngleDegrees: f(135),

  /** §4.5: each stripe is 3 px of wick. */
  StripeWidth: f(3),

  /** §4.5 3 px 間隔: 3 px of the bar shows between two stripes. */
  Gap: f(3),

  /** One stripe and one gap, measured across the stripes. */
  Period: f(f(3) + f(3)),

  /** §2.1 灯芯: the stripe's colour. */
  ColorHex: '#ffd89a',

  /**
   * The tile holds two periods, so its side (2 × 6 × √2 = 16.97) rounds to a whole 17 px and
   * the spacing it really draws stays within 0.02 px of Period.
   */
  PeriodsPerTile: 2,

  /** The square tile's side in pixels: 17. */
  get TileSize() {
    // PeriodsPerTile * Period is a C# float constant (12), then times the double Sqrt2.
    return Math.trunc(roundEven(f(HpWickSpec.PeriodsPerTile * HpWickSpec.Period) * Sqrt2));
  },

  /** The period the 17 px tile really draws, across the stripes: 6.01. */
  get TilePeriod() {
    return f(HpWickSpec.TileSize / (HpWickSpec.PeriodsPerTile * Sqrt2));
  },

  /**
   * The tile's alpha at texel (`x`, `y`), x to the right and y upwards as Texture2D counts its rows:
   * 1 inside a stripe, 0 in a gap, and a one-pixel ramp at the edges. Stripes are constant along
   * x − y, so they run ／ and the tile repeats seamlessly.
   */
  Alpha(x, y) {
    const tile = HpWickSpec.TileSize;
    const diagonal = ((((x - y) | 0) % tile) + tile) % tile;
    const period = HpWickSpec.TilePeriod;
    const across = f(f(diagonal / Sqrt2) % period);
    // The stripe fills the first half of each period; measure to its middle, wrapping round.
    const stripe = f(period * 0.5);
    let distance = Math.abs(f(across - f(stripe * 0.5)));
    distance = Math.min(distance, f(period - distance));
    return Clamp01(f(f(f(stripe * 0.5) - distance) + 0.5));
  },

  /**
   * Where the stripe lies on a bar of `hpMax` that is filled to `hp`: from the HP left after `loss`
   * up to the HP now, as fractions of the bar's width. False, and no stripe, when the card takes
   * nothing off (a loss of 0, which the script writes for an enemy out of reach) or the bar is empty.
   * Returns `{ ok, from, to }` (C#: the bool and the two out floats).
   */
  Span(hp, hpMax, loss) {
    let from = f(0);
    let to = f(0);
    if (loss <= 0 || hp <= 0 || hpMax <= 0) return { ok: false, from, to };
    to = Clamp01(f(f(hp) / f(hpMax)));
    from = Clamp01(f(f((hp - loss) | 0) / f(hpMax)));
    return { ok: to > from, from, to };
  },
};
