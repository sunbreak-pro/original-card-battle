/**
 * Card Encyclopedia Data
 *
 * Provides all cards for the encyclopedia display.
 * Currently only Swordsman cards are available.
 */

import { SWORDSMAN_CARDS } from "../../cards/data/SwordmanCards";
import type { Card } from "../../cards/type/cardType";
import type { CardEncyclopediaEntry } from "../types/LibraryTypes";

/**
 * Get all available cards for encyclopedia
 * Sorted by cost, then by rarity
 */
export function getAllCards(): Card[] {
  const cards = Object.values(SWORDSMAN_CARDS);

  // Sort by cost (ascending), then by rarity
  const rarityOrder = { common: 0, rare: 1, epic: 2, legend: 3 };

  return cards.sort((a, b) => {
    if (a.cost !== b.cost) {
      return a.cost - b.cost;
    }
    return rarityOrder[a.rarity] - rarityOrder[b.rarity];
  });
}

/**
 * Create encyclopedia entries from cards
 * All cards are unlocked by default for now
 */
export function createCardEncyclopediaEntries(): CardEncyclopediaEntry[] {
  const cards = getAllCards();

  return cards.map((card) => ({
    card,
    isUnlocked: true, // All cards are visible in encyclopedia
    timesUsed: 0,
    firstObtainedDate: undefined,
  }));
}

/**
 * Get cards filtered by character class
 */
export function getCardsByClass(
  characterClass: string | null
): CardEncyclopediaEntry[] {
  const entries = createCardEncyclopediaEntries();

  if (!characterClass) {
    return entries;
  }

  return entries.filter(
    (entry) =>
      entry.card.characterClass === characterClass ||
      entry.card.characterClass === "common"
  );
}

/**
 * Get cards filtered by rarity
 */
export function getCardsByRarity(rarity: string | null): CardEncyclopediaEntry[] {
  const entries = createCardEncyclopediaEntries();

  if (!rarity) {
    return entries;
  }

  return entries.filter((entry) => entry.card.rarity === rarity);
}

/**
 * Get cards filtered by category
 */
export function getCardsByCategory(
  category: string | null
): CardEncyclopediaEntry[] {
  const entries = createCardEncyclopediaEntries();

  if (!category) {
    return entries;
  }

  return entries.filter((entry) => entry.card.category === category);
}

/**
 * Search cards by name or description
 */
export function searchCards(searchText: string): CardEncyclopediaEntry[] {
  const entries = createCardEncyclopediaEntries();
  const lowerSearch = searchText.toLowerCase();

  if (!searchText) {
    return entries;
  }

  return entries.filter(
    (entry) =>
      entry.card.name.toLowerCase().includes(lowerSearch) ||
      entry.card.description.toLowerCase().includes(lowerSearch)
  );
}

/**
 * Get card statistics
 */
export function getCardStats(): {
  total: number;
  byRarity: Record<string, number>;
  byCategory: Record<string, number>;
} {
  const cards = getAllCards();

  const byRarity: Record<string, number> = {};
  const byCategory: Record<string, number> = {};

  cards.forEach((card) => {
    byRarity[card.rarity] = (byRarity[card.rarity] || 0) + 1;
    byCategory[card.category] = (byCategory[card.category] || 0) + 1;
  });

  return {
    total: cards.length,
    byRarity,
    byCategory,
  };
}
