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
  // Example (uncomment when derived cards exist):
  // { parentCardTypeId: "sw_slash", derivedCardTypeId: "sw_heavy_slash", requiredMastery: 2 },
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
// Summoner Derivations
// ============================================================

/** Summoner card derivation entries. Add entries here as cards are created. */
export const SUMMONER_DERIVATIONS: DerivationEntry[] = [
  // Example (uncomment when derived cards exist):
  // { parentCardTypeId: "sm_summon_wolf", derivedCardTypeId: "sm_summon_dire_wolf", requiredMastery: 2 },
];

// ============================================================
// Registry Access
// ============================================================

const DERIVATION_REGISTRY: Record<CharacterClass, DerivationEntry[]> = {
  swordsman: SWORDSMAN_DERIVATIONS,
  mage: MAGE_DERIVATIONS,
  summoner: SUMMONER_DERIVATIONS,
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
    ...SUMMONER_DERIVATIONS,
  ];
}
