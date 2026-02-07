// CardAddModal - Modal for adding cards of a specific type to the deck

import { useMemo, useState } from "react";
import type { CardTag, Depth } from "@/types/cardTypes";
import type { CharacterClass } from "@/types/characterTypes";
import { CardComponent } from "@/ui/html/cardHtml/CardComponent";
import { getCardDataByClass } from "@/constants/data/characters/CharacterClassData";
import {
  MAX_CARD_COPIES,
  MAX_DECK_SIZE,
} from "@/constants/uiConstants";
import {
  CARD_TAG_LABEL_MAP,
  CARD_TAG_COLOR_MAP,
} from "@/constants/cardConstants";

interface CardAddModalProps {
  isOpen: boolean;
  cardTag: CardTag;
  playerClass: CharacterClass;
  depth: Depth;
  currentDeckCardTypeIds: string[];
  onAddCards: (cardTypeIds: string[]) => void;
  onClose: () => void;
}

export function CardAddModal({
  isOpen,
  cardTag,
  playerClass,
  depth,
  currentDeckCardTypeIds,
  onAddCards,
  onClose,
}: CardAddModalProps) {
  const [selectedCards, setSelectedCards] = useState<Map<string, number>>(
    new Map(),
  );

  // Get all cards of this tag type for the player's class
  const tagCards = useMemo(() => {
    const classCards = getCardDataByClass(playerClass);
    return Object.entries(classCards)
      .filter(([, card]) => card.tags[0] === cardTag)
      .map(([cardTypeId, card]) => ({ cardTypeId, card }));
  }, [playerClass, cardTag]);

  // Count current deck copies per cardTypeId
  const deckCounts = useMemo(() => {
    const counts = new Map<string, number>();
    for (const id of currentDeckCardTypeIds) {
      counts.set(id, (counts.get(id) || 0) + 1);
    }
    return counts;
  }, [currentDeckCardTypeIds]);

  const totalSelected = Array.from(selectedCards.values()).reduce(
    (sum, n) => sum + n,
    0,
  );
  const projectedDeckSize = currentDeckCardTypeIds.length + totalSelected;
  const isOverLimit = projectedDeckSize > MAX_DECK_SIZE;

  const toggleCard = (cardTypeId: string) => {
    setSelectedCards((prev) => {
      const next = new Map(prev);
      const currentSelected = next.get(cardTypeId) || 0;
      const inDeck = deckCounts.get(cardTypeId) || 0;

      if (currentSelected > 0) {
        // Deselect
        next.delete(cardTypeId);
      } else {
        // Select: check MAX_CARD_COPIES
        if (inDeck < MAX_CARD_COPIES) {
          next.set(cardTypeId, 1);
        }
      }
      return next;
    });
  };

  const handleAdd = () => {
    const newIds: string[] = [];
    for (const [cardTypeId, count] of selectedCards) {
      for (let i = 0; i < count; i++) {
        newIds.push(cardTypeId);
      }
    }
    onAddCards(newIds);
    setSelectedCards(new Map());
    onClose();
  };

  const handleClose = () => {
    setSelectedCards(new Map());
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="card-add-modal-overlay" onClick={handleClose}>
      <div
        className="card-add-modal"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="card-add-modal-header">
          <div
            className="card-add-modal-title"
            style={{ borderLeftColor: CARD_TAG_COLOR_MAP[cardTag] }}
          >
            <span style={{ color: CARD_TAG_COLOR_MAP[cardTag] }}>
              {CARD_TAG_LABEL_MAP[cardTag]}
            </span>
            カードを追加
            {totalSelected > 0 && (
              <span className="card-add-count">({totalSelected}枚選択中)</span>
            )}
          </div>
          <div className="card-add-modal-actions">
            {isOverLimit && (
              <span className="card-add-over-limit">
                カードの上限を超えています
              </span>
            )}
            <button
              className="card-add-confirm-btn"
              onClick={handleAdd}
              disabled={totalSelected === 0 || isOverLimit}
            >
              追加
            </button>
            <button className="card-add-close-btn" onClick={handleClose}>
              閉じる
            </button>
          </div>
        </div>

        <div className="card-add-modal-grid">
          {tagCards.map(({ cardTypeId, card }) => {
            const inDeck = deckCounts.get(cardTypeId) || 0;
            const isSelected = (selectedCards.get(cardTypeId) || 0) > 0;
            const atCopyLimit = inDeck >= MAX_CARD_COPIES;

            return (
              <div
                key={cardTypeId}
                className={`card-add-selectable${isSelected ? " selected" : ""}${atCopyLimit ? " at-limit" : ""}`}
                onClick={() => !atCopyLimit && toggleCard(cardTypeId)}
              >
                <CardComponent card={card} depth={depth} isPlayable={true} />
                {inDeck > 0 && (
                  <div className="card-add-in-deck">
                    デッキ内: {inDeck}/{MAX_CARD_COPIES}
                  </div>
                )}
                {atCopyLimit && (
                  <div className="card-add-limit-badge">上限</div>
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
