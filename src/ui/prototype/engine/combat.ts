// Pure combat math — no React, no randomness, fully unit-testable.

import type { RangeBand } from "./types";
import {
  FATIGUE_FLOOR_MULT,
  FATIGUE_THRESHOLD,
  RANGE_MULT,
  RANGE_ORDER,
  STAMINA_RECOVERY,
} from "./constants";

export function rangeToIndex(band: RangeBand): number {
  return RANGE_ORDER.indexOf(band);
}

export function clampDistance(index: number): number {
  const max = RANGE_ORDER.length - 1;
  if (index < 0) return 0;
  return index > max ? max : index;
}

export function indexToRange(index: number): RangeBand {
  return RANGE_ORDER[clampDistance(index)];
}

export function shiftDistance(currentIndex: number, shift: number): number {
  return clampDistance(currentIndex + shift);
}

export function staminaRecovery(band: RangeBand): number {
  return STAMINA_RECOVERY[band];
}

/**
 * Fatigue as a deterministic damage multiplier (never a random miss).
 * stamina >= 8 -> 1.0; otherwise max(0.4, stamina / 8).
 */
export function staminaDamageMultiplier(stamina: number): number {
  if (stamina >= FATIGUE_THRESHOLD) return 1;
  return Math.max(FATIGUE_FLOOR_MULT, stamina / FATIGUE_THRESHOLD);
}

/** Range compatibility: diff 0 -> 1.0, diff 1 -> 0.5, diff 2 -> 0.15. */
export function rangeMultiplier(
  distanceIndex: number,
  effRange: RangeBand,
): number {
  const diff = Math.abs(clampDistance(distanceIndex) - rangeToIndex(effRange));
  const idx = Math.min(diff, RANGE_MULT.length - 1);
  return RANGE_MULT[idx];
}

/** Final attack damage before guard subtraction. */
export function computeAttackDamage(
  basePower: number,
  effRange: RangeBand,
  attackerStamina: number,
  distanceIndex: number,
): number {
  const raw =
    basePower *
    rangeMultiplier(distanceIndex, effRange) *
    staminaDamageMultiplier(attackerStamina);
  return Math.max(0, Math.round(raw));
}
