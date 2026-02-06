// DeckReadOnlyView - Read-only deck display grouped by card tag

import { useMemo } from "react";
import type { Card, CardTag, Depth } from "@/types/cardTypes";
import { CardComponent } from "@/ui/html/cardHtml/CardComponent";
import { CARD_TAG_LABEL_MAP } from "@/constants/cardConstants";
import { useJournal } from "@/contexts/JournalContext";

interface DeckReadOnlyViewProps {
  deckCards: Card[];
  depth: Depth;
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

function groupByTag(cards: Card[]): TagGroup[] {
  const tagMap = new Map<CardTag, Map<string, { card: Card; count: number }>>();

  for (const card of cards) {
    const tag = card.tags[0] ?? "skill";
    if (!tagMap.has(tag)) {
      tagMap.set(tag, new Map());
    }
    const typeMap = tagMap.get(tag)!;
    const existing = typeMap.get(card.cardTypeId);
    if (existing) {
      existing.count++;
    } else {
      typeMap.set(card.cardTypeId, { card, count: 1 });
    }
  }

  return TAG_ORDER.filter((tag) => tagMap.has(tag)).map((tag) => {
    const typeMap = tagMap.get(tag)!;
    const stacks = Array.from(typeMap.entries()).map(
      ([cardTypeId, { card, count }]) => ({ card, cardTypeId, count }),
    );
    const totalCount = stacks.reduce((sum, s) => sum + s.count, 0);
    return {
      tag,
      label: CARD_TAG_LABEL_MAP[tag],
      stacks,
      totalCount,
    };
  });
}

export function DeckReadOnlyView({ deckCards, depth }: DeckReadOnlyViewProps) {
  const { openJournal } = useJournal();

  const tagGroups = useMemo(() => groupByTag(deckCards), [deckCards]);

  const handleOpenJournal = () => {
    openJournal("tactics");
  };

  if (deckCards.length === 0) {
    return (
      <div className="deck-readonly">
        <div className="deck-readonly-header">
          <h3 className="deck-readonly-title">デッキ概要 (0枚)</h3>
          <button
            className="deck-readonly-edit-btn"
            onClick={handleOpenJournal}
          >
            Journalでデッキを編集
          </button>
        </div>
        <div className="deck-readonly-empty">
          デッキにカードがありません
        </div>
      </div>
    );
  }

  return (
    <div className="deck-readonly">
      <div className="deck-readonly-header">
        <h3 className="deck-readonly-title">
          デッキ概要 ({deckCards.length}枚)
        </h3>
        <button
          className="deck-readonly-edit-btn"
          onClick={handleOpenJournal}
        >
          Journalでデッキを編集
        </button>
      </div>

      {tagGroups.map((group) => (
        <div key={group.tag} className="deck-readonly-tag-row">
          <div className={`deck-readonly-tag-label tag-color-${group.tag}`}>
            {group.label} ({group.totalCount})
          </div>
          <div className="deck-readonly-card-grid">
            {group.stacks.map((stack) => (
              <div key={stack.cardTypeId} className="deck-readonly-card-wrapper">
                {stack.count > 1 && (
                  <div className="deck-readonly-count-badge">
                    x{stack.count}
                  </div>
                )}
                <CardComponent
                  card={stack.card}
                  depth={depth}
                  isPlayable={true}
                />
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
