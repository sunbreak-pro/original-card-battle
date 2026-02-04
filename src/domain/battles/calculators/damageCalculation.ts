import type { Card } from '@/types/cardTypes';
import type { DamageAllocation, DamageResult, DamageType } from '@/types/battleTypes';
import type { BuffDebuffMap } from '@/types/battleTypes';
import {
  attackBuffDebuff,
  defenseBuffDebuff,
  reflectBuff,
  calculateLifesteal
} from "./buffCalculation";
import type { BattleStats, ElementType } from '@/types/characterTypes';
import { GUARD_BLEED_THROUGH_MULTIPLIER } from "../../../constants";

// Empty BuffDebuffMap for fallback
const EMPTY_BUFF_MAP: BuffDebuffMap = new Map();

/** Magical element types that resolve to "magical" damage */
const MAGICAL_ELEMENTS: ReadonlySet<ElementType> = new Set([
  "fire", "ice", "lightning", "dark", "light",
]);

/** Elements that resolve to "true" damage (bypasses defense in future) */
// Note: Currently no elements trigger true damage. Was "sacrifice" for summoner class.
const TRUE_DAMAGE_ELEMENTS: ReadonlySet<ElementType> = new Set([]);

/**
 * Resolve damage type from card elements.
 * Priority: true > magical > physical (default)
 */
export function resolveDamageType(elements: ElementType[]): DamageType {
  for (const el of elements) {
    if (TRUE_DAMAGE_ELEMENTS.has(el)) return "true";
  }
  for (const el of elements) {
    if (MAGICAL_ELEMENTS.has(el)) return "magical";
  }
  return "physical";
}

export function calculateDamage(
  attacker: BattleStats,
  defender: BattleStats,
  card: Card,
  critBonus: number = 0,
  penetration: number = 0,
): DamageResult {
  const baseDmg = card.baseDamage || 0;
  const attackerBuffs = attacker.buffDebuffs ?? EMPTY_BUFF_MAP;
  const defenderBuffs = defender.buffDebuffs ?? EMPTY_BUFF_MAP;

  // Resolve damage type from card elements
  const damageType = resolveDamageType(card.element);

  const atkMultiplier = attackBuffDebuff(attackerBuffs);

  const finalAtk = Math.floor(baseDmg * atkMultiplier);

  let incomingDmg: number;

  if (damageType === "true") {
    // True damage bypasses all defense calculations
    incomingDmg = finalAtk;
  } else {
    // Physical and magical damage use standard defense pipeline
    const { vulnerabilityMod, damageReductionMod } = defenseBuffDebuff(defenderBuffs);

    // Apply penetration: reduce defense effectiveness
    const effectiveReduction = penetration > 0
      ? 1 - (1 - damageReductionMod) * (1 - penetration)
      : damageReductionMod;

    incomingDmg = Math.floor(finalAtk * vulnerabilityMod * effectiveReduction);
  }

  // Critical hit check
  const isCritical = critBonus > 0 && Math.random() < critBonus;
  if (isCritical) {
    incomingDmg = Math.floor(incomingDmg * 1.5);
  }

  const reflectDamage = reflectBuff(defenderBuffs, incomingDmg);
  const lifestealAmount = calculateLifesteal(attackerBuffs, incomingDmg);
  return {
    finalDamage: incomingDmg,
    damageType,
    isCritical,
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

