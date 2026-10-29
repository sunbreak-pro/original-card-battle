import type { Card, MasteryLevel } from '@/types/cardTypes';
import { MASTERY_THRESHOLDS } from '@/constants/cardConstants';
export type MasteryStore = Map<string, number>;

export function calculateMasteryLevel(useCount: number): MasteryLevel {
  if (useCount >= MASTERY_THRESHOLDS[4]) return 4;
  if (useCount >= MASTERY_THRESHOLDS[3]) return 3;
  if (useCount >= MASTERY_THRESHOLDS[2]) return 2;
  if (useCount >= MASTERY_THRESHOLDS[1]) return 1;
  return 0;
}

export function applyMasteryToCard(card: Card, store: MasteryStore): Card {
  const useCount = store.get(card.cardTypeId) ?? card.useCount;
  const masteryLevel = calculateMasteryLevel(useCount);

  return {
    ...card,
    useCount,
    masteryLevel,
  };
}

export function applyMasteryToCards(cards: Card[], store: MasteryStore): Card[] {
  return cards.map(card => applyMasteryToCard(card, store));
}
