// DeckTab - Deck editor with add/remove card functionality

import { useMemo, useState } from "react";
import type { Card, Depth } from "@/types/cardTypes";
import type { CharacterClass } from "@/types/characterTypes";
import { CardComponent } from "@/ui/cardHtml/CardComponent";
import { getCardDataByClass } from "@/constants/data/characters/CharacterClassData";
import { MAX_CARD_COPIES, MIN_DECK_SIZE, MAX_DECK_SIZE } from "@/constants/uiConstants";

interface DeckTabProps {
  deckCards: Card[];
  playerClass: CharacterClass;
  depth: Depth;
  onUpdateDeck: (cardTypeIds: string[]) => void;
}

interface CardStack {
  card: Card;
  cardTypeId: string;
  count: number;
}

export function DeckTab({
  deckCards,
  playerClass,
  depth,
  onUpdateDeck,
}: DeckTabProps) {
  // Group deck cards by cardTypeId
  const deckStacks = useMemo<CardStack[]>(() => {
    const grouped = new Map<string, { card: Card; count: number }>();
    for (const card of deckCards) {
      const existing = grouped.get(card.cardTypeId);
      if (existing) {
        existing.count++;
      } else {
        grouped.set(card.cardTypeId, { card, count: 1 });
      }
    }
    return Array.from(grouped.entries()).map(
      ([cardTypeId, { card, count }]) => ({
        card,
        cardTypeId,
        count,
      }),
    );
  }, [deckCards]);

  // Get all available cards for this class
  const availableCards = useMemo<CardStack[]>(() => {
    const classCards = getCardDataByClass(playerClass);
    const deckCounts = new Map<string, number>();
    for (const card of deckCards) {
      deckCounts.set(
        card.cardTypeId,
        (deckCounts.get(card.cardTypeId) || 0) + 1,
      );
    }

    return Object.entries(classCards).map(([cardTypeId, card]) => ({
      card,
      cardTypeId,
      count: deckCounts.get(cardTypeId) || 0,
    }));
  }, [playerClass, deckCards]);

  // Build current cardTypeIds list from deck
  const currentCardTypeIds = useMemo(() => {
    return deckCards.map((c) => c.cardTypeId);
  }, [deckCards]);

  const deckSize = deckCards.length;
  const isAtMaxSize = deckSize >= MAX_DECK_SIZE;

  const getCardCount = (cardTypeId: string): number => {
    return currentCardTypeIds.filter((id) => id === cardTypeId).length;
  };

  const handleAddCard = (cardTypeId: string) => {
    if (isAtMaxSize) return;
    if (getCardCount(cardTypeId) >= MAX_CARD_COPIES) return;
    onUpdateDeck([...currentCardTypeIds, cardTypeId]);
  };

  const handleRemoveCard = (cardTypeId: string) => {
    const idx = currentCardTypeIds.indexOf(cardTypeId);
    if (idx !== -1) {
      const updated = [...currentCardTypeIds];
      updated.splice(idx, 1);
      onUpdateDeck(updated);
    }
  };

  const [selectedCardId, setSelectedCardId] = useState<string | null>(null);

  const toggleSelect = (id: string) => {
    setSelectedCardId((prev) => (prev === id ? null : id));
  };

  return (
    <div className="deck-tab">
      {/* Current Deck Section */}
      <div className="deck-section">
        <div className="deck-section-header">
          <h3 className="deck-section-title">
            現在のデッキ ({deckSize}/{MIN_DECK_SIZE}~{MAX_DECK_SIZE}枚)
            {deckSize < MIN_DECK_SIZE && (
              <span className="deck-warning"> ⚠ 最低{MIN_DECK_SIZE}枚必要</span>
            )}
            {isAtMaxSize && (
              <span className="deck-limit-reached"> (上限)</span>
            )}
          </h3>
        </div>
        {deckStacks.length === 0 ? (
          <div className="deck-empty">デッキにカードがありません</div>
        ) : (
          <div className="deck-card-grid">
            {deckStacks.map((stack) => {
              const cardId = `deck-${stack.cardTypeId}`;
              const isSelected = selectedCardId === cardId;
              return (
                <div key={stack.cardTypeId} className="deck-card-slot">
                  <div
                    className={`prep-card-wrapper${isSelected ? " selected" : ""}`}
                    onClick={() => toggleSelect(cardId)}
                  >
                    <div className="card-count">x{stack.count}</div>
                    <CardComponent
                      card={stack.card}
                      depth={depth}
                      isPlayable={true}
                    />
                  </div>
                  {isSelected && (
                    <div className="deck-edit-btn-container">
                      <button
                        className="deck-edit-btn deck-remove-btn"
                        onClick={() => handleRemoveCard(stack.cardTypeId)}
                      >
                        − 外す
                      </button>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Divider */}
      <div className="deck-divider" />

      {/* Available Cards Section */}
      <div className="deck-section">
        <div className="deck-section-header">
          <h3 className="deck-section-title">追加可能なカード</h3>
        </div>
        <div className="deck-card-grid">
          {availableCards.map((stack) => {
            const cardId = `available-${stack.cardTypeId}`;
            const isSelected = selectedCardId === cardId;
            return (
              <div key={stack.cardTypeId} className="deck-card-slot">
                <div
                  className={`prep-card-wrapper${isSelected ? " selected" : ""}`}
                  onClick={() => toggleSelect(cardId)}
                >
                  <CardComponent
                    card={stack.card}
                    depth={depth}
                    isPlayable={true}
                  />
                  {stack.count > 0 && (
                    <div className="deck-in-deck-badge">
                      デッキ内: {stack.count}
                    </div>
                  )}
                </div>
                {isSelected && (() => {
                  const atCopyLimit = getCardCount(stack.cardTypeId) >= MAX_CARD_COPIES;
                  const disabled = isAtMaxSize || atCopyLimit;
                  return (
                    <div className="deck-edit-btn-container">
                      <button
                        className="deck-edit-btn deck-add-btn"
                        onClick={() => handleAddCard(stack.cardTypeId)}
                        disabled={disabled}
                      >
                        {atCopyLimit ? `上限 (${MAX_CARD_COPIES}枚)` : "+ 追加"}
                      </button>
                    </div>
                  );
                })()}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
