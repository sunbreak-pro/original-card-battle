/**
 * Card Classification System
 *
 * Utility functions to classify cards into three categories:
 * - Base cards: Original cards (derivedFrom: null, not talent cards)
 * - Derived cards: Cards derived from base cards (derivedFrom !== null)
 * - Talent cards: Special cards unlocked via high mastery (isTalentCard: true)
 */

import type { Card } from "@/types/cardTypes";

export interface ClassifiedCards {
  baseCards: Card[];
  derivedCards: Card[];
  talentCards: Card[];
}

/**
 * Classify cards into three categories: base, derived, and talent
 * @param cards - Array of cards to classify
 * @returns Object with three arrays: baseCards, derivedCards, talentCards
 */
export function classifyCards(cards: Card[]): ClassifiedCards {
  const baseCards: Card[] = [];
  const derivedCards: Card[] = [];
  const talentCards: Card[] = [];

  for (const card of cards) {
    if (card.isTalentCard) {
      talentCards.push(card);
    } else if (card.derivedFrom) {
      derivedCards.push(card);
    } else {
      baseCards.push(card);
    }
  }

  return { baseCards, derivedCards, talentCards };
}

/**
 * Check if a card is a talent card
 * @param card - Card to check
 * @returns true if it's a talent card
 */
export function isTalentCard(card: Card): boolean {
  return !!card.isTalentCard;
}
