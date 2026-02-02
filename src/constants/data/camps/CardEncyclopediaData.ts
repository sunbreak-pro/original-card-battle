/**
 * Card Encyclopedia Data
 *
 * Provides all cards for the encyclopedia display.
 * Includes Swordsman, Mage, and Summoner cards.
 * Supports unlocked/unknown card awareness.
 */

import { SWORDSMAN_CARDS } from "@/constants/data/cards/swordsmanCards";
import { MAGE_CARDS } from "@/constants/data/cards/mageCards";
import { SUMMONER_CARDS } from "@/constants/data/cards/summonerCards";
import type { Card, CardTag } from '@/types/cardTypes';
import type { CardEncyclopediaEntry } from '@/types/campTypes';

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
 * Create encyclopedia entries from cards with unlock awareness
 * @param unlockedCardTypeIds - set of unlocked card type IDs. If undefined, all cards are treated as unlocked.
 */
export function createCardEncyclopediaEntries(
  unlockedCardTypeIds?: Set<string>,
): CardEncyclopediaEntry[] {
  const cards = getAllCards();

  return cards.map((card) => ({
    card,
    isUnlocked: !unlockedCardTypeIds || unlockedCardTypeIds.has(card.cardTypeId),
    timesUsed: 0,
    firstObtainedDate: undefined,
  }));
}

/**
 * Get card statistics including unlock ratio and tag breakdown
 */
export function getCardStats(unlockedCardTypeIds?: Set<string>): {
  total: number;
  unlocked: number;
  byElement: Record<string, number>;
  byClass: Record<string, number>;
  byTag: Record<string, number>;
} {
  const cards = getAllCards();

  const byElement: Record<string, number> = {};
  const byClass: Record<string, number> = {};
  const byTag: Record<string, number> = {};
  let unlocked = 0;

  cards.forEach((card) => {
    if (!unlockedCardTypeIds || unlockedCardTypeIds.has(card.cardTypeId)) {
      unlocked++;
    }
    for (const elem of card.element) {
      byElement[elem] = (byElement[elem] || 0) + 1;
    }
    byClass[card.characterClass] = (byClass[card.characterClass] || 0) + 1;
    for (const tag of card.tags) {
      byTag[tag] = (byTag[tag] || 0) + 1;
    }
  });

  return {
    total: cards.length,
    unlocked,
    byElement,
    byClass,
    byTag,
  };
}

/**
 * Get all unique tags present across all cards
 */
export function getAllTags(): CardTag[] {
  const tags = new Set<CardTag>();
  const cards = getAllCards();
  for (const card of cards) {
    for (const tag of card.tags) {
      tags.add(tag);
    }
  }
  return Array.from(tags);
}
