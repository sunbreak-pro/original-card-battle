// Port of unity-port/unity-project-kit/Assets/View/Backdrop/Script/BackdropSwitch.cs
//
// Which layers are on screen while the backdrop switches (#607): the one shown, the one fading out under
// it, and how far the 400 ms crossfade of battle_ui_ux_v2 §4.3 has gone. Pure C#, so `dotnet test` pins
// the switching that BackdropLayers draws.
//
// The nested enum `BackdropSwitch.Change` is exported as `Change` and also hung on the class
// (`BackdropSwitch.Change.Fade`). `Shown` / `Leaving` are `{ get; private set; }`: read-only getters
// over private fields.
import { defineEnum } from '../../shared/cs.js';
import { BackdropSpec } from './BackdropSpec.js';
import { BackdropMotion } from './BackdropMotion.js';

/** What a call to Show asks the drawing side to do. */
export const Change = defineEnum([
  /** The layer is already shown: draw nothing new. */
  'None',
  /** The first layer: draw it at full, with nothing to fade from. */
  'First',
  /** Draw the new layer at 0 over the shown one and fade it in. */
  'Fade',
  /** A third layer mid-fade: drop the one fading out, then fade as Fade. */
  'FadeDroppingOldest',
]);

export class BackdropSwitch {
  constructor() {
    this._shown = 0;
    this._leaving = 0;
    this._fadeStartMs = 0;
  }

  /** The layer drawn on top (1〜7), or 0 before the first. */
  get Shown() {
    return this._shown;
  }

  /** The layer fading out under Shown, or 0 when none is. */
  get Leaving() {
    return this._leaving;
  }

  /** Turns to `layer` (held to 1〜7) at `nowMs`. */
  Show(layer, nowMs) {
    const next = BackdropSpec.Clamp(layer);
    if (next === this._shown) return Change.None;
    const change = this._shown === 0 ? Change.First : this._leaving !== 0 ? Change.FadeDroppingOldest : Change.Fade;
    this._leaving = this._shown;
    this._shown = next;
    this._fadeStartMs = nowMs;
    return change;
  }

  /**
   * The shown layer's alpha at `nowMs`. When it reaches 1 the fade is over and
   * Leaving goes back to 0, so the drawing side can drop the layer under it.
   */
  Tick(nowMs) {
    if (this._leaving === 0) return 1;
    const t = BackdropMotion.Crossfade(nowMs - this._fadeStartMs);
    if (t >= 1) this._leaving = 0;
    return t;
  }
}
BackdropSwitch.Change = Change;
