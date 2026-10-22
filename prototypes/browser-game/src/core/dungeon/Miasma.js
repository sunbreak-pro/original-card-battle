// Port of unity-port/DungeonCore/Miasma.cs
import { idiv } from '../../shared/cs.js';
import { argOutOfRange } from './_errors.js';

/**
 * 瘴気: how far one life can be pushed. Every 刻限 spent inside a layer adds that
 * layer's density to the gauge; every full 20% costs one point of max stamina; 100%
 * ends the life (concept-v3.md §6).
 *
 * This is the exploration side of the gauge only. The boss state 瘴気纏い lowers max
 * stamina inside a battle and is restored when the battle ends — it never touches this
 * gauge (battle_core_v4.md §6.2).
 */
export const Miasma = {
  /** The gauge runs 0..100. Reaching 100 is 瘴気死. */
  DeathAt: 100,

  /** One point of max stamina per full 20% (concept-v3.md §6). */
  StepPercent: 20,

  /** The penalty stops at −4, which is max stamina 6 at 80% (battle_core_v4.md §13). */
  MaxPenalty: 4,

  /** Relief can thin the air but never clear it: a 刻限 always costs at least 1%. */
  DensityFloor: 1,

  /** Base max stamina before any modifier (battle_core_v4.md §1). */
  BaseMaxStamina: 10,

  MaxStaminaFloor: 3,
  MaxStaminaCeil: 14,

  /** Temporary modifiers from exploration clamp at ±4 (battle_core_v3.md §3.1). */
  TempModClamp: 4,

  /** Stamina recovered outside battle, once per 刻限 spent (concept-v3.md §12-16). */
  FieldRecovery: 3,

  /** A layer's density after the run's relief is applied, floored at 1. */
  EffectiveDensity(layerDensity, densityRelief) {
    if (layerDensity < 0) throw argOutOfRange('layerDensity');
    return Math.max(Miasma.DensityFloor, layerDensity - Math.max(0, densityRelief));
  },

  /** Adds one 刻限 worth of miasma, clamped to the lethal ceiling. */
  Accumulate(percent, effectiveDensity) {
    return Math.min(Miasma.DeathAt, Math.max(0, percent) + Math.max(0, effectiveDensity));
  },

  /** Consumables and the like pull the gauge back down, never below zero. */
  Relieve(percent, amount) {
    return Math.max(0, Math.max(0, percent) - Math.max(0, amount));
  },

  IsLethal(percent) {
    return percent >= Miasma.DeathAt;
  },

  /**
   * −1 max stamina per full 20%, capped at −4. 100% never reaches a battle because the
   * life has already ended, so the reading is clamped to 99 the way the battle core does.
   */
  MaxStaminaPenalty(percent) {
    const clamped = Math.max(0, Math.min(Miasma.DeathAt - 1, percent));
    return Math.min(Miasma.MaxPenalty, idiv(clamped, Miasma.StepPercent));
  },

  /** max = clamp(10 + clamp(temp, ±4) − miasma penalty, 3..14). */
  MaxStamina(tempModifierSum, percent) {
    const temp = Math.max(-Miasma.TempModClamp, Math.min(Miasma.TempModClamp, tempModifierSum));
    const raw = Miasma.BaseMaxStamina + temp - Miasma.MaxStaminaPenalty(percent);
    return Math.max(Miasma.MaxStaminaFloor, Math.min(Miasma.MaxStaminaCeil, raw));
  },
};
