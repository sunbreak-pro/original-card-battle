/**
 * Equipment Skill Effects Calculation
 *
 * Handles the application of equipment skill effects during battle.
 * Each equipment can have up to 2 skills: one initial (unlockLevel: 0)
 * and one unlocked at max upgrade (unlockLevel: 3).
 */

import type {
  EquipmentSkill,
  EquipmentSkillEffect,
  EquipmentSkillEffectType,
  EnhancedEquipmentData,
  EquipmentUpgradeLevel,
} from '@/types/itemTypes';

/**
 * Aggregated equipment skill effects for battle calculations
 */
export interface AggregatedSkillEffects {
  damageReduction: number;
  damageBonus: number;
  shieldOnTurnStart: number;
  counterAttack: number;
  criticalRateBonus: number;
  criticalDamageBonus: number;
  healingBonus: number;
  energyBonus: number;
  drawBonus: number;
  penetrationBonus: number;
  statusResist: number;
  lifesteal: number;
  swordEnergyBonus: number;
  resonanceBonus: number;
}

/**
 * Create empty aggregated effects
 */
export function createEmptyAggregatedEffects(): AggregatedSkillEffects {
  return {
    damageReduction: 0,
    damageBonus: 0,
    shieldOnTurnStart: 0,
    counterAttack: 0,
    criticalRateBonus: 0,
    criticalDamageBonus: 0,
    healingBonus: 0,
    energyBonus: 0,
    drawBonus: 0,
    penetrationBonus: 0,
    statusResist: 0,
    lifesteal: 0,
    swordEnergyBonus: 0,
    resonanceBonus: 0,
  };
}

/**
 * Check if a skill is unlocked based on equipment upgrade level
 */
export function isSkillUnlocked(
  skill: EquipmentSkill,
  upgradeLevel: EquipmentUpgradeLevel,
): boolean {
  return upgradeLevel >= skill.unlockLevel;
}

/**
 * Get all active skills from an enhanced equipment item
 */
export function getActiveSkills(equipment: EnhancedEquipmentData): EquipmentSkill[] {
  return equipment.skills.filter((skill) =>
    isSkillUnlocked(skill, equipment.upgradeLevel),
  );
}

/**
 * Apply a single skill effect to aggregated effects
 */
function applySkillEffect(
  effect: EquipmentSkillEffect,
  aggregated: AggregatedSkillEffects,
): void {
  switch (effect.type) {
    case 'damageReduction':
      aggregated.damageReduction += effect.value;
      break;
    case 'damageBonus':
      aggregated.damageBonus += effect.value;
      break;
    case 'shieldOnTurnStart':
      aggregated.shieldOnTurnStart += effect.value;
      break;
    case 'counterAttack':
      aggregated.counterAttack += effect.value;
      break;
    case 'criticalBonus':
      // Split value: assume first 50% is rate, rest is damage
      aggregated.criticalRateBonus += effect.value * 0.5;
      aggregated.criticalDamageBonus += effect.value * 0.5;
      break;
    case 'healingBonus':
      aggregated.healingBonus += effect.value;
      break;
    case 'energyBonus':
      aggregated.energyBonus += effect.value;
      break;
    case 'drawBonus':
      aggregated.drawBonus += effect.value;
      break;
    case 'penetrationBonus':
      aggregated.penetrationBonus += effect.value;
      break;
    case 'statusResist':
      aggregated.statusResist += effect.value;
      break;
    case 'lifesteal':
      aggregated.lifesteal += effect.value;
      break;
    case 'classAbilityBonus':
      if (effect.classAbilityTarget === 'swordEnergy') {
        aggregated.swordEnergyBonus += effect.value;
      } else if (effect.classAbilityTarget === 'resonance') {
        aggregated.resonanceBonus += effect.value;
      }
      break;
    case 'bonusPerSwordEnergy':
      aggregated.swordEnergyBonus += effect.value;
      break;
    case 'bonusPerResonance':
      aggregated.resonanceBonus += effect.value;
      break;
    default:
      // Conditional effects (bonusOnLowHp, etc.) handled separately
      break;
  }
}

/**
 * Calculate AP bonus from equipment upgrade level
 * Each level adds 10% of base AP
 */
export function calculateUpgradedAP(
  baseAp: number,
  upgradeLevel: EquipmentUpgradeLevel,
): number {
  const bonusPercent = upgradeLevel * 0.1; // 10% per level
  return Math.floor(baseAp * (1 + bonusPercent));
}

/**
 * Aggregate all equipment skill effects from equipped items
 * Only includes skills that are unlocked based on upgrade level
 */
export function aggregateEquipmentSkillEffects(
  enhancedEquipment: EnhancedEquipmentData[],
): AggregatedSkillEffects {
  const aggregated = createEmptyAggregatedEffects();

  for (const equipment of enhancedEquipment) {
    const activeSkills = getActiveSkills(equipment);
    for (const skill of activeSkills) {
      applySkillEffect(skill.effect, aggregated);
    }
  }

  return aggregated;
}

/**
 * Check if an effect should trigger based on condition
 */
export function checkEffectCondition(
  condition: string | undefined,
  context: {
    currentHp: number;
    maxHp: number;
    activeBuffCount: number;
    enemyDebuffCount: number;
    swordEnergy?: number;
    resonanceLevel?: number;
  },
): boolean {
  if (!condition) return true;

  switch (condition) {
    case 'lowHp':
      return context.currentHp / context.maxHp < 0.3;
    case 'fullHp':
      return context.currentHp >= context.maxHp;
    case 'hasBuff':
      return context.activeBuffCount > 0;
    case 'enemyDebuffed':
      return context.enemyDebuffCount > 0;
    case 'highSwordEnergy':
      return (context.swordEnergy ?? 0) >= 5;
    case 'maxResonance':
      return (context.resonanceLevel ?? 0) >= 2;
    default:
      return true;
  }
}

/**
 * Calculate conditional effect value based on scaling
 */
export function calculateScaledEffectValue(
  effectType: EquipmentSkillEffectType,
  baseValue: number,
  context: {
    activeBuffCount?: number;
    enemyDebuffCount?: number;
    swordEnergy?: number;
    resonanceLevel?: number;
  },
): number {
  switch (effectType) {
    case 'bonusPerBuff':
      return baseValue * (context.activeBuffCount ?? 0);
    case 'bonusPerDebuff':
      return baseValue * (context.enemyDebuffCount ?? 0);
    case 'bonusPerSwordEnergy':
      return baseValue * (context.swordEnergy ?? 0);
    case 'bonusPerResonance':
      return baseValue * (context.resonanceLevel ?? 0);
    default:
      return baseValue;
  }
}
