/**
 * Card Utility Functions
 *
 * Helper functions for card filtering and classification.
 */

import type { Card } from '@/types/cardTypes';

/**
 * Check if card belongs to a specific class
 */
export function isCardForClass(card: Card, characterClass: "swordsman" | "mage" | "summoner"): boolean {
  return card.characterClass === characterClass || card.characterClass === "common";
}

/**
 * Check if card is class-specific (not common)
 */
export function isClassSpecificCard(card: Card): boolean {
  return card.characterClass !== "common";
}

/**
 * Get cards filtered by class
 */
export function filterCardsByClass(
  cards: Card[],
  characterClass: "swordsman" | "mage" | "summoner"
): Card[] {
  return cards.filter(card => isCardForClass(card, characterClass));
}
