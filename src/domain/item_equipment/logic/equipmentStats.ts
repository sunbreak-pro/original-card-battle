/**
 * Equipment Stats Calculation
 *
 * AP is derived entirely from defense equipment (armor, helmet, boots, accessory1, accessory2).
 * Weapon does NOT contribute AP.
 * Each equipment's AP contribution is scaled by its durability ratio.
 */

import type { EquipmentSlot, Item, EquipmentStatBonuses } from '@/types/itemTypes';
import type { EquipmentSlots } from '@/types/campTypes';
import { EQUIPMENT_STAT_BONUSES } from '@/constants/data/items/EquipmentData';

/** Defense equipment slots that contribute AP (weapon excluded) */
const DEFENSE_SLOTS: EquipmentSlot[] = [
  'armor', 'helmet', 'boots', 'accessory1', 'accessory2',
] as const;

/** Per-slot AP breakdown */
export interface EquipmentAPResult {
  /** Total current AP (factoring durability) */
  totalAP: number;
  /** Total max AP (all equipment at full durability) */
  maxAP: number;
  /** AP contribution per defense slot */
  perSlot: Partial<Record<EquipmentSlot, number>>;
  /** Max AP per defense slot (at full durability) */
  perSlotMax: Partial<Record<EquipmentSlot, number>>;
}

/**
 * Get the apBonus for an equipment item from the stat bonus table.
 * Falls back to 0 if no apBonus defined for that slot/rarity.
 */
function getApBonus(slot: EquipmentSlot, item: Item): number {
  const bonuses: EquipmentStatBonuses | undefined =
    EQUIPMENT_STAT_BONUSES[slot]?.[item.rarity];
  return bonuses?.apBonus ?? 0;
}

/**
 * Calculate total AP from equipment.
 *
 * For each defense slot:
 * - Look up apBonus from EQUIPMENT_STAT_BONUSES[slot][rarity]
 * - Scale by durability ratio: contribution = apBonus * (durability / maxDurability)
 * - Sum all contributions
 *
 * @param equipmentSlots - Current equipment loadout
 * @returns AP totals and per-slot breakdown
 */
export function calculateEquipmentAP(equipmentSlots: EquipmentSlots): EquipmentAPResult {
  let totalAP = 0;
  let maxAP = 0;
  const perSlot: Partial<Record<EquipmentSlot, number>> = {};
  const perSlotMax: Partial<Record<EquipmentSlot, number>> = {};

  for (const slot of DEFENSE_SLOTS) {
    const item = equipmentSlots[slot];
    if (!item) continue;

    const apBonus = getApBonus(slot, item);
    if (apBonus <= 0) continue;

    const durability = item.durability ?? 0;
    const maxDurability = item.maxDurability ?? 1;
    const durabilityRatio = maxDurability > 0 ? durability / maxDurability : 0;

    const contribution = Math.floor(apBonus * durabilityRatio);
    perSlot[slot] = contribution;
    perSlotMax[slot] = apBonus;

    totalAP += contribution;
    maxAP += apBonus;
  }

  return { totalAP, maxAP, perSlot, perSlotMax };
}

/**
 * Distribute AP damage proportionally across defense equipment.
 *
 * When AP absorbs X damage, distribute that damage across equipped defense items
 * proportional to each item's AP contribution ratio.
 *
 * @param apDamage - Total AP damage to distribute
 * @param equipmentSlots - Current equipment loadout
 * @returns Durability reduction per equipment slot
 */
export function distributeApDamageToEquipment(
  apDamage: number,
  equipmentSlots: EquipmentSlots,
): Partial<Record<EquipmentSlot, number>> {
  if (apDamage <= 0) return {};

  const apResult = calculateEquipmentAP(equipmentSlots);
  if (apResult.totalAP <= 0) return {};

  const durabilityDamage: Partial<Record<EquipmentSlot, number>> = {};
  let distributed = 0;

  const slots = Object.entries(apResult.perSlot) as [EquipmentSlot, number][];

  for (let i = 0; i < slots.length; i++) {
    const [slot, contribution] = slots[i];
    if (contribution <= 0) continue;

    const item = equipmentSlots[slot];
    if (!item) continue;

    const ratio = contribution / apResult.totalAP;

    // Last slot gets the remainder to avoid rounding errors
    let slotDamage: number;
    if (i === slots.length - 1) {
      slotDamage = apDamage - distributed;
    } else {
      slotDamage = Math.floor(apDamage * ratio);
    }

    // Cap at current durability
    const currentDurability = item.durability ?? 0;
    slotDamage = Math.min(slotDamage, currentDurability);

    if (slotDamage > 0) {
      durabilityDamage[slot] = slotDamage;
      distributed += slotDamage;
    }
  }

  return durabilityDamage;
}

/**
 * Check if an equipment item is broken (durability = 0).
 */
export function isEquipmentBroken(item: Item | null): boolean {
  if (!item) return false;
  return (item.durability ?? 0) <= 0;
}

/**
 * Apply durability damage to equipment slots and return updated slots.
 *
 * @param equipmentSlots - Current equipment
 * @param durabilityDamage - Damage per slot from distributeApDamageToEquipment
 * @returns New equipment slots with reduced durability
 */
export function applyDurabilityDamage(
  equipmentSlots: EquipmentSlots,
  durabilityDamage: Partial<Record<EquipmentSlot, number>>,
): EquipmentSlots {
  const updated = { ...equipmentSlots };

  for (const [slot, damage] of Object.entries(durabilityDamage) as [EquipmentSlot, number][]) {
    const item = updated[slot];
    if (!item || damage <= 0) continue;

    const currentDurability = item.durability ?? 0;
    updated[slot] = {
      ...item,
      durability: Math.max(0, currentDurability - damage),
    };
  }

  return updated;
}

/**
 * Get stat bonuses from equipment, excluding broken items (defense equipment only).
 * Weapon effects persist regardless of AP state.
 */
export function getEffectiveEquipmentBonuses(
  equipmentSlots: EquipmentSlots,
): EquipmentStatBonuses {
  const totals: EquipmentStatBonuses = {
    hpBonus: 0,
    apBonus: 0,
    atkPercent: 0,
    defPercent: 0,
    speedBonus: 0,
    energyBonus: 0,
  };

  for (const [slot, item] of Object.entries(equipmentSlots) as [EquipmentSlot, Item | null][]) {
    if (!item) continue;

    // Weapon effects always persist
    const isWeapon = slot === 'weapon';

    // Defense equipment with 0 durability loses its effects
    if (!isWeapon && isEquipmentBroken(item)) continue;

    const bonuses = EQUIPMENT_STAT_BONUSES[slot]?.[item.rarity];
    if (!bonuses) continue;

    totals.hpBonus! += bonuses.hpBonus ?? 0;
    totals.apBonus! += bonuses.apBonus ?? 0;
    totals.atkPercent! += bonuses.atkPercent ?? 0;
    totals.defPercent! += bonuses.defPercent ?? 0;
    totals.speedBonus! += bonuses.speedBonus ?? 0;
    totals.energyBonus! += bonuses.energyBonus ?? 0;
  }

  return totals;
}
