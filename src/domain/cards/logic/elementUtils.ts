/**
 * Element Utility Functions
 *
 * Helpers for element-based card classification.
 */

import type { Card } from '@/types/cardTypes';
import type { ElementType, CharacterClass } from '@/types/characterTypes';
import { FUNCTIONAL_ELEMENTS, MAGIC_ELEMENTS } from '@/constants/cardConstants';

/**
 * Check if a card has a specific element
 */
export function hasElement(card: Card, element: ElementType): boolean {
  return card.element.includes(element);
}

/**
 * Get the primary functional element from a card's element array.
 * Returns the first matching element from FUNCTIONAL_ELEMENTS.
 */
export function getFunctionalElement(card: Card): ElementType | null {
  for (const elem of card.element) {
    if (FUNCTIONAL_ELEMENTS.has(elem)) {
      return elem;
    }
  }
  return null;
}

/**
 * For Mage class cards with magic elements, automatically include "chain" element.
 * Returns a new card with "chain" added if applicable.
 */
export function applyMageChainElement(card: Card, playerClass: CharacterClass): Card {
  if (playerClass !== "mage") return card;

  const hasMagicElement = card.element.some(e => MAGIC_ELEMENTS.has(e));
  if (!hasMagicElement) return card;

  if (card.element.includes("chain")) return card;

  return {
    ...card,
    element: [...card.element, "chain"],
  };
}
