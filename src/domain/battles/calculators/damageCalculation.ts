import type { Card } from '@/types/cardTypes';
import type { DamageAllocation, DamageResult } from '@/types/battleTypes';
import type { BuffDebuffMap } from '@/types/battleTypes';
import {
  attackBuffDebuff,
  defenseBuffDebuff,
  reflectBuff,
  calculateLifesteal
} from "./buffCalculation";
import type { BattleStats } from '@/types/characterTypes';
import { GUARD_BLEED_THROUGH_MULTIPLIER } from "../../../constants";

// Empty BuffDebuffMap for fallback
const EMPTY_BUFF_MAP: BuffDebuffMap = new Map();

export function calculateDamage(
  attacker: BattleStats,
  defender: BattleStats,
  card: Card,
): DamageResult {
  const baseDmg = card.baseDamage || 0;
  const attackerBuffs = attacker.buffDebuffs ?? EMPTY_BUFF_MAP;
  const defenderBuffs = defender.buffDebuffs ?? EMPTY_BUFF_MAP;

  const atkMultiplier = attackBuffDebuff(attackerBuffs);

  const finalAtk = Math.floor(baseDmg * atkMultiplier);
  const { vulnerabilityMod, damageReductionMod } = defenseBuffDebuff(defenderBuffs);
  const incomingDmg = Math.floor(finalAtk * vulnerabilityMod * damageReductionMod);
  const reflectDamage = reflectBuff(defenderBuffs, incomingDmg);
  const lifestealAmount = calculateLifesteal(attackerBuffs, incomingDmg);
  return {
    finalDamage: incomingDmg,
    isCritical: false,
    reflectDamage,
    lifestealAmount,
  };
}

export function applyDamageAllocation(
  defender: BattleStats,
  damage: number,
  // _isBuffDebuffApplied: boolean = false,
): DamageAllocation {
  let remainingDmg = damage;
  let guardDmg = 0;
  let apDmg = 0;
  let hpDmg = 0;
  const hadGuard = defender.guard > 0;

  // Guard absorption with bleed-through mechanic:
  // When guard fully absorbs damage AND there is no AP backup,
  // a portion of damage bleeds through directly to HP.
  // When AP exists as a secondary layer, guard provides full protection.
  if (hadGuard) {
    if (defender.guard >= remainingDmg && defender.ap <= 0) {
      // Guard absorbs all, but no AP backup → bleed-through to HP
      guardDmg = remainingDmg;
      hpDmg = Math.floor(remainingDmg * GUARD_BLEED_THROUGH_MULTIPLIER);
      remainingDmg = 0;
      return { guardDamage: guardDmg, apDamage: apDmg, hpDamage: hpDmg };
    } else if (defender.guard >= remainingDmg) {
      // Guard absorbs all, AP exists as backup → no bleed-through
      guardDmg = remainingDmg;
      remainingDmg = 0;
      return { guardDamage: guardDmg, apDamage: 0, hpDamage: 0 };
    } else {
      // Guard partially absorbs → overflow continues to AP/HP
      guardDmg = defender.guard;
      remainingDmg -= defender.guard;
    }
  }
  if (defender.ap > 0) {
    if (defender.ap >= remainingDmg) {
      apDmg = remainingDmg;
      remainingDmg = 0;
      return { guardDamage: guardDmg, apDamage: apDmg, hpDamage: 0 };
    } else {
      apDmg = defender.ap;
      remainingDmg -= defender.ap;
    }
  }
  if (remainingDmg > 0) {
    hpDmg = remainingDmg;
  }
  return {
    guardDamage: guardDmg,
    apDamage: apDmg,
    hpDamage: hpDmg,
  };
}

