import type { BuffDebuffMap } from "../type/baffType";

/**
 * Minimal interface for speed calculation
 * Works with both Enemy and EnemyDefinition
 */
interface SpeedSource {
  baseSpeed: number;
}

// Re-export phase calculation types and functions
export {
  applySpeedRandomness,
  generatePhaseQueue,
  calculateConsecutivePhases,
  createInitialSpeedRandomState,
} from "./phaseCalculation";

// Speed bonus type for UI display (kept for backwards compatibility)
export interface SpeedBonus {
  name: string;
  value: number;
}

export function calculatePlayerSpeed(buffs: BuffDebuffMap): number {
  let speed = 50;

  if (buffs.has("haste")) {
    const hasteBuff = buffs.get("haste")!;
    speed += hasteBuff.value;
  }

  if (buffs.has("superFast")) {
    const superFastBuff = buffs.get("superFast")!;
    speed += superFastBuff.value;
  }

  if (buffs.has("slow")) {
    const slowDebuff = buffs.get("slow")!;
    speed -= slowDebuff.value;
  }

  if (buffs.has("stall")) {
    const stallDebuff = buffs.get("stall")!;
    speed -= stallDebuff.value;
  }

  return Math.max(0, speed);
}

export function calculateEnemySpeed(
  enemy: SpeedSource,
  buffs: BuffDebuffMap
): number {
  let speed = enemy.baseSpeed;
  if (buffs.has("haste")) {
    const hasteBuff = buffs.get("haste")!;
    speed += hasteBuff.value;
  } else if (buffs.has("superFast")) {
    const superFastBuff = buffs.get("superFast")!;
    speed += superFastBuff.value;
  } else if (buffs.has("slow")) {
    const slowDebuff = buffs.get("slow")!;
    speed -= slowDebuff.value;
  } else if (buffs.has("stall")) {
    const stallDebuff = buffs.get("stall")!;
    speed -= stallDebuff.value;
  }

  return Math.max(0, speed);
}

// Keep for backwards compatibility
export function determineTurnOrder(
  playerSpeed: number,
  enemySpeed: number
): "player" | "enemy" {
  return playerSpeed >= enemySpeed ? "player" : "enemy";
}
