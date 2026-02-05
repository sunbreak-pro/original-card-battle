/**
 * CardCategoryRow Component
 *
 * Displays a horizontal row of cards for a single category.
 * Shows locked/unlocked state with appropriate visual indicators.
 */

import React from "react";
import type { Card } from "@/types/cardTypes";
import { CardComponent } from "../../cardHtml/CardComponent";

interface CardCategoryRowProps {
  /** Section title (e.g., "基本カード") */
  title: string;
  /** Cards to display in this category */
  cards: Card[];
  /** Set of unlocked card type IDs */
  unlockedIds: Set<string>;
  /** Callback when a card is clicked */
  onCardClick?: (card: Card) => void;
}

export const CardCategoryRow: React.FC<CardCategoryRowProps> = ({
  title,
  cards,
  unlockedIds,
  onCardClick,
}) => {
  const unlockedCount = cards.filter(c => unlockedIds.has(c.cardTypeId)).length;

  return (
    <div className="card-category-row">
      <div className="category-header">
        <h3 className="category-title">{title}</h3>
        <span className="category-count">
          {unlockedCount}/{cards.length}
        </span>
      </div>
      <div className="category-cards-container">
        {cards.map((card) => {
          const isUnlocked = unlockedIds.has(card.cardTypeId);
          return (
            <div
              key={card.cardTypeId}
              className={`encyclopedia-card-wrapper${!isUnlocked ? " card-locked" : ""}`}
              onClick={() => isUnlocked && onCardClick?.(card)}
            >
              {isUnlocked ? (
                <CardComponent
                  card={card}
                  depth={1}
                  isPlayable={true}
                />
              ) : (
                <div className="locked-card-placeholder">
                  <div className="locked-card-icon">🔒</div>
                  <div className="locked-card-name">{card.name}</div>
                </div>
              )}
            </div>
          );
        })}
        {cards.length === 0 && (
          <div className="category-empty">
            カードがありません
          </div>
        )}
      </div>
    </div>
  );
};
