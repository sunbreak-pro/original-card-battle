import type { BuffDebuffMap } from '@/types/battleTypes';
import { BLEED_DAMAGE_RATE } from "../../../constants";

export function calculateBleedDamage(
  maxHp: number,
  buffDebuffs: BuffDebuffMap
): number {
  if (!buffDebuffs.has("bleed")) {
    return 0;
  }
  const stacks = buffDebuffs.get("bleed")!.stacks;
  const bleedDamage = Math.floor(maxHp * BLEED_DAMAGE_RATE) * stacks;
  return bleedDamage;
}
