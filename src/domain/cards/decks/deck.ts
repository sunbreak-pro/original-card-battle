import type { Card } from "../type/cardType";

let cardInstanceCounter = 0;
const generateCardInstanceId = (baseId: string): string => {
  cardInstanceCounter++;
  return `${baseId}_instance_${cardInstanceCounter}`;
};

export interface DeckState {
  drawPile: Card[];
  hand: Card[];
  discardPile: Card[];
}
export const shuffleArray = <T,>(array: T[]): T[] => {
  const shuffled = [...array];
  for (let i = shuffled.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
  }
  return shuffled;
};

export const shuffleDiscardIntoDraw = (
  drawPile: Card[],
  discardPile: Card[]
): { newDrawPile: Card[]; newDiscardPile: Card[] } => {
  const shuffledDiscard = shuffleArray(discardPile);
  return {
    newDrawPile: [...drawPile, ...shuffledDiscard],
    newDiscardPile: [],
  };
};

/**
 * 山札からカードを引く（足りない場合は捨て札をシャッフル）
 * @param count 引く枚数
 * @param drawPile 現在の山札
 * @param discardPile 現在の捨て札
 * @returns 引いたカードと新しい山札・捨て札の状態
 */
export const drawCards = (
  count: number,
  drawPile: Card[],
  discardPile: Card[]
): {
  drawnCards: Card[];
  newDrawPile: Card[];
  newDiscardPile: Card[];
} => {
  let currentDrawPile = [...drawPile];
  let currentDiscardPile = [...discardPile];
  const drawnCards: Card[] = [];

  for (let i = 0; i < count; i++) {
    if (currentDrawPile.length === 0) {
      if (currentDiscardPile.length === 0) {
        break;
      }
      const shuffled = shuffleDiscardIntoDraw(
        currentDrawPile,
        currentDiscardPile
      );
      currentDrawPile = shuffled.newDrawPile;
      currentDiscardPile = shuffled.newDiscardPile;
    }

    const card = currentDrawPile.pop();
    if (card) {
      drawnCards.push(card);
    }
  }

  return {
    drawnCards,
    newDrawPile: currentDrawPile,
    newDiscardPile: currentDiscardPile,
  };
};

export const discardCards = (
  cards: Card[],
  discardPile: Card[]
): Card[] => {
  return [...discardPile, ...cards];
};

export const createInitialDeck = (
  cardCounts: Record<string, number>,
  allCards: Card[]
): Card[] => {
  const deck: Card[] = [];
  Object.entries(cardCounts).forEach(([cardId, count]) => {
    const card = allCards.find((c) => c.id === cardId || c.cardTypeId === cardId);
    if (card) {
      for (let i = 0; i < count; i++) {
        deck.push({
          ...card,
          id: generateCardInstanceId(card.cardTypeId || card.id),
          cardTypeId: card.cardTypeId || card.id,
        });
      }
    }
  });

  return shuffleArray(deck);
};
