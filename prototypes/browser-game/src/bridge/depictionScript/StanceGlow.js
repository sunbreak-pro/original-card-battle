// Port of unity-project-kit/Assets/View/Depiction/Script/StanceGlow.cs
// The light a unit's body keeps while it holds a stance (battle_ui_ux_v2 §5.11 順 2 / §5.3 型 StanceGlow,
// #501). Pure C#, so `dotnet test` holds when the light is on with the same rule the screen follows.
//
// The light is read off the frame alone: on while the unit's stance list (UnitFrame.Stances) holds at
// least one stance, off while it is empty. Since battle_core_v4 v4.4 (#454) a stance joins a list and
// is never replaced, so a second stance adds no second light (§5.11 順 2: 2 つ目からは光を重ねず) and
// there is no old light to fade out under a new one: the 型 crest drawn for it comes without the crest's
// own body light (CrestCarriesLight). The light goes out only when the list empties. Stances last the
// battle (battle_core_v4 §4), so no frame within a battle empties it; a new battle snaps the light off
// with no fade, and §5.18 順 3's fade on the carry-over panel belongs to the run screen, not built yet.
import { defineEnum } from '../../shared/cs.js';

/** What the screen does to a unit's stance light when a frame lands. */
export const StanceGlowChange = defineEnum([
  /** The light stays as it is (on and still held, or off and still empty). */
  'Keep',
  /** The first stance was put down: the light fades in. */
  'Light',
  /** The last stance is gone: the light fades out. */
  'Fade',
]);

export const StanceGlow = {
  /** §5.11 順 2: the light is the stance attribute's colour at α 0.25. */
  Alpha: Math.fround(0.25),

  /** The light is on while `unit` (UnitFrame) holds at least one stance. */
  IsOn(unit) {
    return unit != null && unit.Stances != null && unit.Stances.length > 0;
  },

  /** What a light now `shown` (or not) does when `unit`'s frame lands. */
  Change(shown, unit) {
    const on = StanceGlow.IsOn(unit);
    if (on === shown) return StanceGlowChange.Keep;
    return on ? StanceGlowChange.Light : StanceGlowChange.Fade;
  },

  /**
   * §5.11 順 2: whether a 型 crest put down now brings the crest's own body light (§5.3 型 「身体に薄い光」).
   * Only on a unit with no light yet; from the second stance on the crest is drawn alone
   * (2 つ目からは光を重ねず、紋だけを出し直す). `shown` is the light before the stance lands.
   */
  CrestCarriesLight(shown) {
    return !shown;
  },
};
