import type { Card } from "../../cards/type/cardType";
import type { DamageAllocation, DamageResult } from "../type/damageType";
import type { BuffDebuffMap } from "../type/baffType";
import {
  attackBuffDebuff,
  criticalRateBuff,
  defenseBuffDebuff,
  reflectBuff,
  calculateLifesteal
} from "./buffCalculation";
import type { BattleStats } from "../../characters/type/baseTypes";
import { CRIT_DAMAGE_MULTIPLIER, GUARD_BLEED_THROUGH_MULTIPLIER } from "../../../constants";

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

  let critMod = 1.0;
  let isCritical = false;

  if (attackerBuffs.has("criticalUp")) {
    const critRate = criticalRateBuff(attackerBuffs);
    isCritical = Math.random() < critRate;

    if (isCritical) {
      critMod = CRIT_DAMAGE_MULTIPLIER;
      const critBuff = attackerBuffs.get("criticalUp")!;
      critMod += critBuff.value / 100;
    }
  }
  const finalAtk = Math.floor(baseDmg * atkMultiplier * critMod);
  const { vulnerabilityMod, damageReductionMod } = defenseBuffDebuff(defenderBuffs);
  const incomingDmg = Math.floor(finalAtk * vulnerabilityMod * damageReductionMod);
  const reflectDamage = reflectBuff(defenderBuffs, incomingDmg);
  const lifestealAmount = calculateLifesteal(attackerBuffs, incomingDmg);
  return {
    finalDamage: incomingDmg,
    isCritical,
    reflectDamage,
    lifestealAmount,
  };
}

export function applyDamageAllocation(
  defender: BattleStats,
  damage: number,
  _isBuffDebuffApplied: boolean = false,
): DamageAllocation {
  let remainingDmg = damage;
  let guardDmg = 0;
  let apDmg = 0;
  let hpDmg = 0;
  const hadGuard = defender.guard > 0;

  // Step 2: Damage to Guard (damage as is)
  if (hadGuard) {
    if (defender.guard >= remainingDmg && defender.ap <= 0) {
      guardDmg = remainingDmg;
      hpDmg = Math.floor(remainingDmg * GUARD_BLEED_THROUGH_MULTIPLIER);
      remainingDmg = 0;
      return { guardDamage: guardDmg, apDamage: apDmg, hpDamage: hpDmg };
    } else if (defender.guard >= remainingDmg) {
      guardDmg = remainingDmg;
      remainingDmg = 0;
      return { guardDamage: guardDmg, apDamage: 0, hpDamage: 0 };
    }
    else {
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

