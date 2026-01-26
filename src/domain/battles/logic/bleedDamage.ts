import type { BuffDebuffMap } from "../type/baffType";

export function calculateBleedDamage(
  maxHp: number,
  buffDebuffs: BuffDebuffMap
): number {
  if (!buffDebuffs.has("bleed")) {
    return 0;
  }
  const bleedDamage = Math.floor(maxHp * 0.05);
  return bleedDamage;
}

export function hasBleed(buffDebuffs: BuffDebuffMap): boolean {
  return buffDebuffs.has("bleed");
}
