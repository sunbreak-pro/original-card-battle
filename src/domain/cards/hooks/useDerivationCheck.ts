/**
 * useDerivationCheck Hook
 *
 * Checks for newly unlockable card derivations based on current deck mastery.
 * Used after battle victory to detect and report new card unlocks.
 */

import { useMemo } from "react";
import type { Card } from "@/types/cardTypes";
import type { CharacterClass } from "@/types/characterTypes";
import {
  checkAllDerivationUnlocks,
  type DerivationUnlock,
} from "@/domain/cards/logic/cardDerivation";
import { getCardDataByClass } from "@/constants/data/characters/CharacterClassData";

/**
 * Check for derivation unlocks given current deck cards and unlocked set.
 *
 * @param deckCards - Current deck cards with updated mastery/useCount
 * @param playerClass - Player's character class
 * @param unlockedCardTypeIds - Set of already-unlocked card type IDs
 * @returns Array of DerivationUnlock results (empty if no new unlocks)
 */
export function useDerivationCheck(
  deckCards: Card[],
  playerClass: CharacterClass,
  unlockedCardTypeIds: Set<string>,
): DerivationUnlock[] {
  const allCards = useMemo(
    () => getCardDataByClass(playerClass),
    [playerClass],
  );

  const unlocks = useMemo(
    () => checkAllDerivationUnlocks(deckCards, allCards, unlockedCardTypeIds),
    [deckCards, allCards, unlockedCardTypeIds],
  );

  return unlocks;
}
