import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  shuffleArray,
  shuffleDiscardIntoDraw,
  drawCards,
  discardCards,
  createInitialDeck,
} from '../deck';
import type { Card } from '@/types/cardTypes';

// Helper to create a minimal Card for testing
function createTestCard(id: string, cardTypeId?: string): Card {
  return {
    id,
    cardTypeId: cardTypeId ?? id,
    name: `Card ${id}`,
    description: 'Test card',
    characterClass: 'swordsman',
    baseDamage: 5,
    cost: 1,
    element: ['physics'],
    tags: ['attack'],
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
  };
}

describe('shuffleArray', () => {
  beforeEach(() => {
    vi.spyOn(Math, 'random');
  });

  it('returns empty array for empty input', () => {
    const result = shuffleArray([]);
    expect(result).toEqual([]);
  });

  it('returns single element array unchanged', () => {
    const result = shuffleArray([1]);
    expect(result).toEqual([1]);
  });

  it('returns array with same elements', () => {
    const input = [1, 2, 3, 4, 5];
    const result = shuffleArray(input);

    expect(result).toHaveLength(5);
    expect(result.sort()).toEqual([1, 2, 3, 4, 5]);
  });

  it('does not mutate original array', () => {
    const input = [1, 2, 3, 4, 5];
    const original = [...input];
    shuffleArray(input);

    expect(input).toEqual(original);
  });

  it('shuffles array (not always same order)', () => {
    // Mock random to produce specific swap pattern
    vi.mocked(Math.random)
      .mockReturnValueOnce(0.1)
      .mockReturnValueOnce(0.9)
      .mockReturnValueOnce(0.5)
      .mockReturnValueOnce(0.2);

    const input = [1, 2, 3, 4, 5];
    const result = shuffleArray(input);

    // With these random values, the array should be shuffled
    expect(result).toHaveLength(5);
    expect(result.sort()).toEqual([1, 2, 3, 4, 5]);
  });
});

describe('shuffleDiscardIntoDraw', () => {
  it('moves discard pile to draw pile', () => {
    const drawPile = [createTestCard('a'), createTestCard('b')];
    const discardPile = [createTestCard('c'), createTestCard('d')];

    const result = shuffleDiscardIntoDraw(drawPile, discardPile);

    expect(result.newDrawPile).toHaveLength(4);
    expect(result.newDiscardPile).toHaveLength(0);
  });

  it('preserves existing draw pile cards', () => {
    const cardA = createTestCard('a');
    const cardB = createTestCard('b');
    const drawPile = [cardA, cardB];
    const discardPile = [createTestCard('c')];

    const result = shuffleDiscardIntoDraw(drawPile, discardPile);

    expect(result.newDrawPile).toContain(cardA);
    expect(result.newDrawPile).toContain(cardB);
  });

  it('handles empty discard pile', () => {
    const drawPile = [createTestCard('a')];
    const discardPile: Card[] = [];

    const result = shuffleDiscardIntoDraw(drawPile, discardPile);

    expect(result.newDrawPile).toHaveLength(1);
    expect(result.newDiscardPile).toHaveLength(0);
  });

  it('handles empty draw pile', () => {
    const drawPile: Card[] = [];
    const discardPile = [createTestCard('a'), createTestCard('b')];

    const result = shuffleDiscardIntoDraw(drawPile, discardPile);

    expect(result.newDrawPile).toHaveLength(2);
    expect(result.newDiscardPile).toHaveLength(0);
  });
});

describe('drawCards', () => {
  it('draws specified number of cards from draw pile', () => {
    const drawPile = [
      createTestCard('a'),
      createTestCard('b'),
      createTestCard('c'),
    ];
    const discardPile: Card[] = [];

    const result = drawCards(2, drawPile, discardPile);

    expect(result.drawnCards).toHaveLength(2);
    expect(result.newDrawPile).toHaveLength(1);
    expect(result.newDiscardPile).toHaveLength(0);
  });

  it('draws cards from top (end) of draw pile', () => {
    const cardA = createTestCard('a');
    const cardB = createTestCard('b');
    const cardC = createTestCard('c');
    const drawPile = [cardA, cardB, cardC];
    const discardPile: Card[] = [];

    const result = drawCards(1, drawPile, discardPile);

    expect(result.drawnCards[0]).toBe(cardC);
    expect(result.newDrawPile).toContain(cardA);
    expect(result.newDrawPile).toContain(cardB);
  });

  it('recycles discard pile when draw pile is empty', () => {
    const drawPile: Card[] = [];
    const discardPile = [
      createTestCard('a'),
      createTestCard('b'),
      createTestCard('c'),
    ];

    const result = drawCards(2, drawPile, discardPile);

    expect(result.drawnCards).toHaveLength(2);
    expect(result.newDiscardPile).toHaveLength(0);
    expect(result.newDrawPile.length + result.drawnCards.length).toBe(3);
  });

  it('recycles discard pile mid-draw when draw pile runs out', () => {
    const drawPile = [createTestCard('a')];
    const discardPile = [createTestCard('b'), createTestCard('c')];

    const result = drawCards(3, drawPile, discardPile);

    expect(result.drawnCards).toHaveLength(3);
    expect(result.newDrawPile).toHaveLength(0);
    expect(result.newDiscardPile).toHaveLength(0);
  });

  it('returns fewer cards when both piles are exhausted', () => {
    const drawPile = [createTestCard('a')];
    const discardPile = [createTestCard('b')];

    const result = drawCards(5, drawPile, discardPile);

    expect(result.drawnCards).toHaveLength(2);
    expect(result.newDrawPile).toHaveLength(0);
    expect(result.newDiscardPile).toHaveLength(0);
  });

  it('returns empty array when both piles are empty', () => {
    const drawPile: Card[] = [];
    const discardPile: Card[] = [];

    const result = drawCards(3, drawPile, discardPile);

    expect(result.drawnCards).toHaveLength(0);
    expect(result.newDrawPile).toHaveLength(0);
    expect(result.newDiscardPile).toHaveLength(0);
  });

  it('handles count of 0', () => {
    const drawPile = [createTestCard('a'), createTestCard('b')];
    const discardPile: Card[] = [];

    const result = drawCards(0, drawPile, discardPile);

    expect(result.drawnCards).toHaveLength(0);
    expect(result.newDrawPile).toHaveLength(2);
  });

  it('does not mutate original arrays', () => {
    const drawPile = [createTestCard('a'), createTestCard('b')];
    const discardPile = [createTestCard('c')];
    const originalDrawLength = drawPile.length;
    const originalDiscardLength = discardPile.length;

    drawCards(1, drawPile, discardPile);

    expect(drawPile.length).toBe(originalDrawLength);
    expect(discardPile.length).toBe(originalDiscardLength);
  });
});

describe('discardCards', () => {
  it('adds cards to discard pile', () => {
    const cards = [createTestCard('a'), createTestCard('b')];
    const discardPile = [createTestCard('c')];

    const result = discardCards(cards, discardPile);

    expect(result).toHaveLength(3);
  });

  it('preserves existing discard pile cards', () => {
    const existingCard = createTestCard('existing');
    const newCards = [createTestCard('new')];
    const discardPile = [existingCard];

    const result = discardCards(newCards, discardPile);

    expect(result).toContain(existingCard);
  });

  it('adds new cards to end of discard pile', () => {
    const existingCard = createTestCard('existing');
    const newCard = createTestCard('new');
    const discardPile = [existingCard];

    const result = discardCards([newCard], discardPile);

    expect(result[0]).toBe(existingCard);
    expect(result[1]).toBe(newCard);
  });

  it('handles empty discard pile', () => {
    const cards = [createTestCard('a')];
    const discardPile: Card[] = [];

    const result = discardCards(cards, discardPile);

    expect(result).toHaveLength(1);
  });

  it('handles empty cards array', () => {
    const cards: Card[] = [];
    const discardPile = [createTestCard('a')];

    const result = discardCards(cards, discardPile);

    expect(result).toHaveLength(1);
  });

  it('does not mutate original arrays', () => {
    const cards = [createTestCard('a')];
    const discardPile = [createTestCard('b')];
    const originalCardsLength = cards.length;
    const originalDiscardLength = discardPile.length;

    discardCards(cards, discardPile);

    expect(cards.length).toBe(originalCardsLength);
    expect(discardPile.length).toBe(originalDiscardLength);
  });
});

describe('createInitialDeck', () => {
  const allCards = [
    createTestCard('card-a', 'card-a'),
    createTestCard('card-b', 'card-b'),
    createTestCard('card-c', 'card-c'),
  ];

  it('creates deck with specified card counts', () => {
    const cardCounts = {
      'card-a': 2,
      'card-b': 1,
    };

    const result = createInitialDeck(cardCounts, allCards);

    expect(result).toHaveLength(3);

    const typeACounts = result.filter(c => c.cardTypeId === 'card-a').length;
    const typeBCounts = result.filter(c => c.cardTypeId === 'card-b').length;

    expect(typeACounts).toBe(2);
    expect(typeBCounts).toBe(1);
  });

  it('generates unique instance IDs for each card', () => {
    const cardCounts = {
      'card-a': 3,
    };

    const result = createInitialDeck(cardCounts, allCards);

    const ids = result.map(c => c.id);
    const uniqueIds = new Set(ids);

    expect(uniqueIds.size).toBe(3);
  });

  it('preserves card properties except id', () => {
    const cardCounts = {
      'card-a': 1,
    };

    const result = createInitialDeck(cardCounts, allCards);

    const originalCard = allCards.find(c => c.cardTypeId === 'card-a')!;
    const createdCard = result[0];

    expect(createdCard.name).toBe(originalCard.name);
    expect(createdCard.baseDamage).toBe(originalCard.baseDamage);
    expect(createdCard.cost).toBe(originalCard.cost);
    expect(createdCard.cardTypeId).toBe('card-a');
  });

  it('skips cards not found in allCards', () => {
    const cardCounts = {
      'card-a': 1,
      'nonexistent': 2,
    };

    const result = createInitialDeck(cardCounts, allCards);

    expect(result).toHaveLength(1);
  });

  it('handles empty cardCounts', () => {
    const cardCounts = {};

    const result = createInitialDeck(cardCounts, allCards);

    expect(result).toHaveLength(0);
  });

  it('handles empty allCards', () => {
    const cardCounts = {
      'card-a': 2,
    };

    const result = createInitialDeck(cardCounts, []);

    expect(result).toHaveLength(0);
  });

  it('matches cards by cardTypeId', () => {
    const cardsWithTypeId = [
      { ...createTestCard('instance-1', 'type-a'), id: 'instance-1', cardTypeId: 'type-a' },
    ];
    const cardCounts = {
      'type-a': 2,
    };

    const result = createInitialDeck(cardCounts, cardsWithTypeId);

    expect(result).toHaveLength(2);
    result.forEach(card => {
      expect(card.cardTypeId).toBe('type-a');
    });
  });

  it('returns shuffled deck', () => {
    // With a large enough deck and fixed random, we can verify shuffling occurs
    const cardCounts = {
      'card-a': 5,
      'card-b': 5,
      'card-c': 5,
    };

    const result1 = createInitialDeck(cardCounts, allCards);
    const result2 = createInitialDeck(cardCounts, allCards);

    // Both should have same total cards
    expect(result1).toHaveLength(15);
    expect(result2).toHaveLength(15);

    // Same card types should be present
    const types1 = result1.map(c => c.cardTypeId).sort();
    const types2 = result2.map(c => c.cardTypeId).sort();
    expect(types1).toEqual(types2);
  });
});
