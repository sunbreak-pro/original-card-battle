import type { BuffDebuffMap } from "../../cards/type/baffType";
import type { Enemy } from "../../Character/data/EnemyData";

export interface SpeedBonus {
  name: "先制" | "電光石火";
  attackBonus: number;
  criticalBonus: number;
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
  enemy: Enemy,
  buffs: BuffDebuffMap
): number {
  let speed = enemy.speed;
  if (buffs.has("haste")) {
    const hasteBuff = buffs.get("haste")!;
    speed += hasteBuff.value;
  }
  else if (buffs.has("superFast")) {
    const superFastBuff = buffs.get("superFast")!;
    speed += superFastBuff.value;
  }
  else if (buffs.has("slow")) {
    const slowDebuff = buffs.get("slow")!;
    speed -= slowDebuff.value;
  }
  else if (buffs.has("stall")) {
    const stallDebuff = buffs.get("stall")!;
    speed -= stallDebuff.value;
  }

  return Math.max(0, speed);
}

export function determineTurnOrder(
  playerSpeed: number,
  enemySpeed: number
): "player" | "enemy" {
  return playerSpeed >= enemySpeed ? "player" : "enemy";
}

export function calculateSpeedBonus(
  actorSpeed: number,
  targetSpeed: number
): SpeedBonus | null {
  const speedDiff = actorSpeed - targetSpeed;

  if (speedDiff >= 50) {
    return {
      name: "電光石火",
      attackBonus: 0.15,
      criticalBonus: 0.2,
    };
  } else if (speedDiff >= 30) {
    return {
      name: "先制",
      attackBonus: 0.15,
      criticalBonus: 0,
    };
  }
  return null;
}
