import type { Card } from "../../cards/type/cardType";

/**
 * デッキ管理システム
 */

// カード インスタンスIDのカウンター
let cardInstanceCounter = 0;

/**
 * ユニークなカードインスタンスIDを生成
 */
const generateCardInstanceId = (baseId: string): string => {
  cardInstanceCounter++;
  return `${baseId}_instance_${cardInstanceCounter}`;
};

/**
 * デッキの状態
 */
export interface DeckState {
  drawPile: Card[];
  hand: Card[];
  discardPile: Card[];
}

/**
 * 配列をシャッフル（Fisher-Yates）
 */
export const shuffleArray = <T,>(array: T[]): T[] => {
  const shuffled = [...array];
  for (let i = shuffled.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
  }
  return shuffled;
};

/**
 * 捨て札をシャッフルして山札に戻す
 */
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
    // 山札が空の場合、捨て札をシャッフルして山札に戻す
    if (currentDrawPile.length === 0) {
      if (currentDiscardPile.length === 0) {
        // 山札も捨て札も空の場合、これ以上引けない
        break;
      }
      const shuffled = shuffleDiscardIntoDraw(
        currentDrawPile,
        currentDiscardPile
      );
      currentDrawPile = shuffled.newDrawPile;
      currentDiscardPile = shuffled.newDiscardPile;
    }

    // 山札の一番上からカードを引く
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

/**
 * カードを捨て札に移動
 */
export const discardCards = (
  cards: Card[],
  discardPile: Card[]
): Card[] => {
  return [...discardPile, ...cards];
};

/**
 * 初期デッキを構成（各カードを指定枚数ずつ含む）
 * @param cardCounts カードIDと枚数のマップ
 * @param allCards 全カードデータ
 * @returns 初期デッキ（シャッフル済み）
 */
export const createInitialDeck = (
  cardCounts: Record<string, number>,
  allCards: Card[]
): Card[] => {
  const deck: Card[] = [];
  Object.entries(cardCounts).forEach(([cardId, count]) => {
    const card = allCards.find((c) => c.id === cardId);
    if (card) {
      for (let i = 0; i < count; i++) {
        // 各カードインスタンスにユニークなIDを付与
        deck.push({
          ...card,
          id: generateCardInstanceId(card.id),
        });
      }
    }
  });

  return shuffleArray(deck);
};
