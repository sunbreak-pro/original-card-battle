// DeckTab - Deck editor with add/remove card functionality

import { useMemo, useState } from "react";
import type { Card, CardTag, Depth } from "@/types/cardTypes";
import type { CharacterClass } from "@/types/characterTypes";
import { CardComponent } from "@/ui/html/cardHtml/CardComponent";
import { CardAddModal } from "./CardAddModal";
import {
  MIN_DECK_SIZE,
  MAX_DECK_SIZE,
} from "@/constants/uiConstants";
import {
  CARD_TAG_LABEL_MAP,
  CARD_TAG_COLOR_MAP,
} from "@/constants/cardConstants";

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

interface TagGroup {
  tag: CardTag;
  label: string;
  stacks: CardStack[];
  totalCount: number;
}

const TAG_ORDER: CardTag[] = ["attack", "skill", "guard", "stance"];

function groupDeckStacksByTag(stacks: CardStack[]): TagGroup[] {
  const tagMap = new Map<CardTag, CardStack[]>();

  for (const stack of stacks) {
    const tag = stack.card.tags[0] ?? "skill";
    if (!tagMap.has(tag)) {
      tagMap.set(tag, []);
    }
    tagMap.get(tag)!.push(stack);
  }

  return TAG_ORDER.map((tag) => {
    const groupStacks = tagMap.get(tag) ?? [];
    const totalCount = groupStacks.reduce((sum, s) => sum + s.count, 0);
    return {
      tag,
      label: CARD_TAG_LABEL_MAP[tag],
      stacks: groupStacks,
      totalCount,
    };
  });
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

  // Group deck stacks by card tag
  const tagGroups = useMemo(() => groupDeckStacksByTag(deckStacks), [deckStacks]);

  // Build current cardTypeIds list from deck
  const currentCardTypeIds = useMemo(() => {
    return deckCards.map((c) => c.cardTypeId);
  }, [deckCards]);

  const deckSize = deckCards.length;
  const isAtMaxSize = deckSize >= MAX_DECK_SIZE;

  // Card add modal state
  const [addModalTag, setAddModalTag] = useState<CardTag | null>(null);

  const handleAddCards = (newIds: string[]) => {
    onUpdateDeck([...currentCardTypeIds, ...newIds]);
  };

  const [selectedCardIds, setSelectedCardIds] = useState<Map<string, number>>(
    new Map(),
  );

  const totalSelectedForRemove = Array.from(selectedCardIds.values()).reduce(
    (sum, n) => sum + n,
    0,
  );

  const toggleSelect = (cardTypeId: string, maxCount: number) => {
    setSelectedCardIds((prev) => {
      const next = new Map(prev);
      const current = next.get(cardTypeId) ?? 0;
      if (current >= maxCount) {
        next.delete(cardTypeId);
      } else {
        next.set(cardTypeId, current + 1);
      }
      return next;
    });
  };

  const handleRemoveCards = () => {
    if (selectedCardIds.size === 0) return;
    const updated = [...currentCardTypeIds];
    for (const [cardTypeId, count] of selectedCardIds) {
      for (let i = 0; i < count; i++) {
        const idx = updated.indexOf(cardTypeId);
        if (idx !== -1) {
          updated.splice(idx, 1);
        }
      }
    }
    onUpdateDeck(updated);
    setSelectedCardIds(new Map());
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
            {isAtMaxSize && <span className="deck-limit-reached"> (上限)</span>}
          </h3>
          <button
            className="deck-edit-btn deck-remove-btn deck-bulk-remove-btn"
            onClick={handleRemoveCards}
            disabled={totalSelectedForRemove === 0}
          >
            取り除く{totalSelectedForRemove > 0 ? ` (${totalSelectedForRemove}枚)` : ""}
          </button>
        </div>
        {tagGroups.map((group) => (
            <div key={group.tag} className={`deck-type-section deck-${group.tag}-section`}>
              <div className="deck-type-header" style={{ borderLeftColor: CARD_TAG_COLOR_MAP[group.tag] }}>
                <span className="deck-type-label" style={{ color: CARD_TAG_COLOR_MAP[group.tag] }}>
                  {group.label}
                </span>
                <span className="deck-type-count">{group.totalCount}枚</span>
              </div>
              <div className={`deck-card-grid deck-${group.tag}-card-grid`}>
                {group.stacks.map((stack) => {
                  const selectedCount = selectedCardIds.get(stack.cardTypeId) ?? 0;
                  const isSelected = selectedCount > 0;
                  return (
                    <div key={stack.cardTypeId} className="deck-card-slot">
                      <div
                        className={`prep-card-wrapper${isSelected ? " selected" : ""}`}
                        onClick={() => toggleSelect(stack.cardTypeId, stack.count)}
                      >
                        <div className="card-count">x{stack.count}</div>
                        <CardComponent
                          card={stack.card}
                          depth={depth}
                          isPlayable={true}
                        />
                        {isSelected && (
                          <div className="deck-select-count-tag">{selectedCount}</div>
                        )}
                      </div>
                    </div>
                  );
                })}
                <div
                  className="deck-card-slot"
                  key={`add-${group.tag}`}
                >
                  <div
                    className="deck-add-card-placeholder"
                    onClick={() => setAddModalTag(group.tag)}
                  >
                    <span className="deck-add-icon">+</span>
                  </div>
                </div>
              </div>
            </div>
          ))}
      </div>

      {/* Card Add Modal */}
      {addModalTag && (
        <CardAddModal
          isOpen={addModalTag !== null}
          cardTag={addModalTag}
          playerClass={playerClass}
          depth={depth}
          currentDeckCardTypeIds={currentCardTypeIds}
          onAddCards={handleAddCards}
          onClose={() => setAddModalTag(null)}
        />
      )}
    </div>
  );
}
