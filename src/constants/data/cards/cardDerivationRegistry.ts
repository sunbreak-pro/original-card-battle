/**
 * Card Derivation Registry
 *
 * Central registry defining parent-child derivation relationships between cards.
 * Each entry specifies which card derives from which parent and the required mastery level.
 *
 * To add a new derivation:
 * 1. Create the derived card in the appropriate class card file (e.g., SwordmanCards.ts)
 * 2. Add a DerivationEntry here linking parent -> derived with required mastery
 * 3. The hydration system will automatically set derivesInto/derivedFrom/unlockMasteryLevel
 */

import type { MasteryLevel } from "@/types/cardTypes";
import type { CharacterClass } from "@/types/characterTypes";

export interface DerivationEntry {
  /** cardTypeId of the parent card */
  parentCardTypeId: string;
  /** cardTypeId of the derived (child) card */
  derivedCardTypeId: string;
  /** Mastery level required on parent card to unlock the derived card */
  requiredMastery: MasteryLevel;
}

// ============================================================
// Swordsman Derivations
// ============================================================

/** Swordsman card derivation entries. Add entries here as cards are created. */
export const SWORDSMAN_DERIVATIONS: DerivationEntry[] = [
  // Attack card derivations (Lv1 unlocks for basic cards)
  { parentCardTypeId: "sw_001", derivedCardTypeId: "sw_001d", requiredMastery: 1 },
  { parentCardTypeId: "sw_007", derivedCardTypeId: "sw_007d", requiredMastery: 1 },
  { parentCardTypeId: "sw_009", derivedCardTypeId: "sw_009d", requiredMastery: 1 },
  { parentCardTypeId: "sw_005", derivedCardTypeId: "sw_005d", requiredMastery: 1 },
  { parentCardTypeId: "sw_004", derivedCardTypeId: "sw_004d", requiredMastery: 1 },
  // Attack card derivations (higher mastery requirements)
  { parentCardTypeId: "sw_017", derivedCardTypeId: "sw_017d", requiredMastery: 2 },
  { parentCardTypeId: "sw_019", derivedCardTypeId: "sw_019d", requiredMastery: 2 },
  { parentCardTypeId: "sw_023", derivedCardTypeId: "sw_023d", requiredMastery: 2 },
  { parentCardTypeId: "sw_025", derivedCardTypeId: "sw_025d", requiredMastery: 2 },
  { parentCardTypeId: "sw_034", derivedCardTypeId: "sw_034d", requiredMastery: 3 },
  // Skill card derivations
  { parentCardTypeId: "sw_013", derivedCardTypeId: "sw_013d", requiredMastery: 1 },
  { parentCardTypeId: "sw_016", derivedCardTypeId: "sw_016d", requiredMastery: 1 },
  { parentCardTypeId: "sw_033", derivedCardTypeId: "sw_033d", requiredMastery: 2 },
  // Guard card derivations
  { parentCardTypeId: "sw_037", derivedCardTypeId: "sw_037d", requiredMastery: 1 },
  { parentCardTypeId: "sw_042", derivedCardTypeId: "sw_042d", requiredMastery: 1 },
];

// ============================================================
// Mage Derivations
// ============================================================

/** Mage card derivation entries. Add entries here as cards are created. */
export const MAGE_DERIVATIONS: DerivationEntry[] = [
  // Example (uncomment when derived cards exist):
  // { parentCardTypeId: "mg_fireball", derivedCardTypeId: "mg_inferno", requiredMastery: 3 },
];

// ============================================================
// Registry Access
// ============================================================

const DERIVATION_REGISTRY: Record<CharacterClass, DerivationEntry[]> = {
  swordsman: SWORDSMAN_DERIVATIONS,
  mage: MAGE_DERIVATIONS,
};

/**
 * Get all derivation entries for a specific character class
 */
export function getDerivationsForClass(
  classType: CharacterClass,
): DerivationEntry[] {
  return DERIVATION_REGISTRY[classType] ?? [];
}

/**
 * Get all derivation entries across all classes
 */
export function getAllDerivations(): DerivationEntry[] {
  return [
    ...SWORDSMAN_DERIVATIONS,
    ...MAGE_DERIVATIONS,
  ];
}
