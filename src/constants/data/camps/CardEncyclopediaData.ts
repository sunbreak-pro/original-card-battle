/**
 * Card Encyclopedia Data
 *
 * Provides all cards for the encyclopedia display.
 * Includes Swordsman and Mage cards.
 * Supports unlocked/unknown card awareness.
 */

import { SWORDSMAN_CARDS } from "@/constants/data/cards/swordsmanCards";
import { MAGE_CARDS } from "@/constants/data/cards/mageCards";
import type { Card } from '@/types/cardTypes';
import type { CharacterClass } from '@/types/characterTypes';

/**
 * Get all cards for a specific character class
 * Sorted by cost, then by name
 */
export function getCardsByClass(characterClass: CharacterClass): Card[] {
  const cards = characterClass === "swordsman"
    ? Object.values(SWORDSMAN_CARDS)
    : Object.values(MAGE_CARDS);

  return cards.sort((a, b) => {
    if (a.cost !== b.cost) {
      return a.cost - b.cost;
    }
    return a.name.localeCompare(b.name);
  });
}
