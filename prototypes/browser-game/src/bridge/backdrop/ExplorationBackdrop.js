// Port of unity-port/unity-project-kit/Assets/View/Backdrop/Script/ExplorationBackdrop.cs
//
// The exploration screen's backdrop for each layer (battle-visual-v1 §2.5, #90): the same place the battle
// stands in (BackdropSpec's sky, haze and motes), with a veil of the ground colour laid over it so the
// map, its links and its labels — drawn straight on the backdrop, not on panels — read on every layer.
// Pure C#, so `dotnet test` holds the values the screen draws.
//
// The veil is thin on the dark layers and thick on the three bright ones (1, 2 and 4), and each alpha is the
// least that keeps the body text and the amber accent readable at the backdrop's brightest point, with
// a little to spare. The hand-drawn map ground, when it lands, goes between the place and the veil.
//
// Floats as in BackdropSpec.js (Math.fround after every C# float operation).
import { BackdropSpec, Rgba } from './BackdropSpec.js';

const f = Math.fround;

/** Everything the exploration screen draws behind its map on one layer. */
export class ExplorationBackdrop {
  /** C# object initializer: `new ExplorationBackdrop { Layer = l, Place = p, Veil = v }` → `new ExplorationBackdrop({ Layer: l, Place: p, Veil: v })`. */
  constructor(init) {
    this.Layer = 0;
    /** The place itself: the battle's backdrop of the same layer (§2.4). */
    this.Place = null;
    /** The ground colour laid over the whole place, under the map and the panels. */
    this.Veil = new Rgba(0, 0, 0, 0); // default(Rgba)
    if (init != null) {
      for (const key of Object.keys(init)) {
        if (!Object.prototype.hasOwnProperty.call(this, key)) throw new Error(`ExplorationBackdrop has no member '${key}'`);
        if (init[key] !== undefined) this[key] = init[key];
      }
    }
  }
}

// The veil's alpha by layer (index 0 is layer 1). 1, 2 and 4 are the bright layers of world-v1 §3
// rule 3; layer 4 stays the brightest after the veil.
const VeilAlpha = [0.72, 0.68, 0.35, 0.70, 0.35, 0.35, 0.35].map(f);

const Steps = 40;

/** Static class: public members in C# declaration order; VeilAlpha and Steps (private) are module-level. */
export const ExplorationBackdropSpec = Object.freeze({
  /** 地 (battle-visual-v1 §2.1): the veil's colour. */
  Ground: Rgba.Hex('#10141d'),

  /** 本文 (§2.1): the map's labels and the node letters on the ground. */
  Ink: Rgba.Hex('#ecebf2'),
  InkMinContrast: 4.5,

  /** 琥珀 (§2.1): the frame of what can be done now, the player's one warm colour. */
  Amber: Rgba.Hex('#e8a84c'),
  AmberMinContrast: 3,

  /** The exploration backdrop of `layer`; anything outside 1〜7 is held to the nearest end. */
  For(layer) {
    const at = BackdropSpec.Clamp(layer);
    return new ExplorationBackdrop({
      Layer: at,
      Place: BackdropSpec.For(at),
      Veil: ExplorationBackdropSpec.Ground.WithAlpha(VeilAlpha[at - 1]),
    });
  },

  /** The brightest the veiled backdrop gets at height `y` (0 top, 1 bottom). */
  BrightestAt(backdrop, y) {
    return BackdropSpec.Over(backdrop.Veil, BackdropSpec.BrightestAt(backdrop.Place, f(y)));
  },

  /** The lowest contrast `ink` gets on the veiled backdrop, top to bottom, every haze at its densest. */
  LowestContrast(backdrop, ink) {
    let lowest = Number.MAX_VALUE; // double.MaxValue
    for (let step = 0; step <= Steps; step++) {
      const under = ExplorationBackdropSpec.BrightestAt(backdrop, f(step / Steps));
      lowest = Math.min(lowest, BackdropSpec.Contrast(ink, under));
    }
    return lowest;
  },

  /** The brightest luminance the veiled backdrop reaches anywhere, for comparing layers. */
  PeakLuminance(backdrop) {
    let peak = 0;
    for (let step = 0; step <= Steps; step++) {
      peak = Math.max(peak, BackdropSpec.Luminance(ExplorationBackdropSpec.BrightestAt(backdrop, f(step / Steps))));
    }
    return peak;
  },
});
