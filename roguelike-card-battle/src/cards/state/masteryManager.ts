import type { Card, MasteryLevel } from "../type/cardType";
import { MASTERY_THRESHOLDS } from "../type/cardType";
export type MasteryStore = Map<string, number>;

export function createMasteryStore(): MasteryStore {
  return new Map();
}

export function calculateMasteryLevel(useCount: number): MasteryLevel {
  if (useCount >= MASTERY_THRESHOLDS[3]) return 3;
  if (useCount >= MASTERY_THRESHOLDS[2]) return 2;
  if (useCount >= MASTERY_THRESHOLDS[1]) return 1;
  return 0;
}

export function incrementCardMastery(
  store: MasteryStore,
  cardTypeId: string
): { newStore: MasteryStore; newUseCount: number; newMasteryLevel: MasteryLevel } {
  const newStore = new Map(store);
  const currentCount = newStore.get(cardTypeId) ?? 0;
  const newCount = currentCount + 1;
  newStore.set(cardTypeId, newCount);

  return {
    newStore,
    newUseCount: newCount,
    newMasteryLevel: calculateMasteryLevel(newCount),
  };
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

export function syncDeckMastery(
  hand: Card[],
  drawPile: Card[],
  discardPile: Card[],
  store: MasteryStore
): { hand: Card[]; drawPile: Card[]; discardPile: Card[] } {
  return {
    hand: applyMasteryToCards(hand, store),
    drawPile: applyMasteryToCards(drawPile, store),
    discardPile: applyMasteryToCards(discardPile, store),
  };
}
