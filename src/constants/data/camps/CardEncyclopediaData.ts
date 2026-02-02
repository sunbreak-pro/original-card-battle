/**
 * Card Encyclopedia Data
 *
 * Provides all cards for the encyclopedia display.
 * Includes Swordsman, Mage, and Summoner cards.
 */

import { SWORDSMAN_CARDS } from "@/constants/data/cards/swordmanCards";
import { MAGE_CARDS } from "@/constants/data/cards/mageCards";
import { SUMMONER_CARDS } from "@/constants/data/cards/summonerCards";
import type { Card } from '@/types/cardTypes';
import type { CardEncyclopediaEntry } from '@/types/campTypes';
import type { ElementType } from '@/types/characterTypes';

/**
 * Get all available cards for encyclopedia
 * Sorted by cost, then by name
 */
export function getAllCards(): Card[] {
  const cards = [
    ...Object.values(SWORDSMAN_CARDS),
    ...Object.values(MAGE_CARDS),
    ...Object.values(SUMMONER_CARDS),
  ];

  return cards.sort((a, b) => {
    if (a.cost !== b.cost) {
      return a.cost - b.cost;
    }
    return a.name.localeCompare(b.name);
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
 * Get cards filtered by element
 */
export function getCardsByElement(
  element: ElementType | null
): CardEncyclopediaEntry[] {
  const entries = createCardEncyclopediaEntries();

  if (!element) {
    return entries;
  }

  return entries.filter((entry) => entry.card.element.includes(element));
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
  byElement: Record<string, number>;
  byClass: Record<string, number>;
} {
  const cards = getAllCards();

  const byElement: Record<string, number> = {};
  const byClass: Record<string, number> = {};

  cards.forEach((card) => {
    for (const elem of card.element) {
      byElement[elem] = (byElement[elem] || 0) + 1;
    }
    byClass[card.characterClass] = (byClass[card.characterClass] || 0) + 1;
  });

  return {
    total: cards.length,
    byElement,
    byClass,
  };
}
