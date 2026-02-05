/**
 * Talent Card Registry
 *
 * Defines unlock conditions for talent cards.
 * Talent cards are special cards unlocked by reaching high mastery levels on specific cards.
 */

import type { MasteryLevel } from "@/types/cardTypes";
import type { CharacterClass } from "@/types/characterTypes";

export interface TalentUnlockEntry {
  /** Card type ID of the talent card */
  talentCardTypeId: string;
  /** Card type ID of the card required to unlock (condition) */
  requiredCardTypeId: string;
  /** Mastery level required on the condition card (3 or 4) */
  requiredMastery: MasteryLevel;
}

// ============================================================
// Swordsman Talent Unlocks
// ============================================================

export const SWORDSMAN_TALENT_UNLOCKS: TalentUnlockEntry[] = [
  // Lv3 unlocks
  { talentCardTypeId: "sw_027", requiredCardTypeId: "sw_001", requiredMastery: 3 },
  { talentCardTypeId: "sw_028", requiredCardTypeId: "sw_007", requiredMastery: 3 },
  { talentCardTypeId: "sw_029", requiredCardTypeId: "sw_003", requiredMastery: 3 },
  { talentCardTypeId: "sw_031", requiredCardTypeId: "sw_010", requiredMastery: 3 },
  // Lv4 unlocks
  { talentCardTypeId: "sw_030", requiredCardTypeId: "sw_009", requiredMastery: 4 },
  { talentCardTypeId: "sw_032", requiredCardTypeId: "sw_017", requiredMastery: 4 },
  { talentCardTypeId: "sw_033", requiredCardTypeId: "sw_013", requiredMastery: 4 },
  { talentCardTypeId: "sw_034", requiredCardTypeId: "sw_025", requiredMastery: 4 },
];

// ============================================================
// Mage Talent Unlocks (Future)
// ============================================================

export const MAGE_TALENT_UNLOCKS: TalentUnlockEntry[] = [];

// ============================================================
// Registry Access
// ============================================================

const TALENT_REGISTRY: Record<CharacterClass, TalentUnlockEntry[]> = {
  swordsman: SWORDSMAN_TALENT_UNLOCKS,
  mage: MAGE_TALENT_UNLOCKS,
};

/**
 * Get all talent unlock entries for a specific character class
 */
export function getTalentUnlocksForClass(
  classType: CharacterClass,
): TalentUnlockEntry[] {
  return TALENT_REGISTRY[classType] ?? [];
}

/**
 * Get all talent unlock entries across all classes
 */
export function getAllTalentUnlocks(): TalentUnlockEntry[] {
  return [
    ...SWORDSMAN_TALENT_UNLOCKS,
    ...MAGE_TALENT_UNLOCKS,
  ];
}
