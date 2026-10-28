/**
 * Card Utility Functions
 *
 * Helper functions for card filtering and classification.
 */

import type { Card, MasteryLevel } from '@/types/cardTypes';

/**
 * Check if card belongs to a specific class
 */
export function isCardForClass(card: Card, characterClass: "swordsman" | "mage"): boolean {
  return card.characterClass === characterClass;
}

/**
 * Check if card is a base card (not derived from another card)
 */
export function isBaseCard(card: Card): boolean {
  return card.derivedFrom === null || card.derivedFrom === undefined;
}

/**
 * Check if a derived card is unlocked based on mastery store.
 * Base cards are always unlocked.
 */
export function isCardUnlocked(
  card: Card,
  masteryStore: Record<string, MasteryLevel>,
): boolean {
  if (isBaseCard(card)) return true;
  if (!card.derivedFrom || card.unlockMasteryLevel === undefined) return true;
  const parentMastery = masteryStore[card.derivedFrom] ?? 0;
  return parentMastery >= card.unlockMasteryLevel;
}
