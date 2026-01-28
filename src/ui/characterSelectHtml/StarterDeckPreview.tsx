/**
 * StarterDeckPreview Component
 *
 * Displays the starter deck cards for the selected character class.
 */

import React, { useMemo } from "react";
import type { Card } from '@/types/cardTypes';
import { CardComponent } from "../cardHtml/CardComponent";

interface StarterDeckPreviewProps {
  cards: Card[];
  className?: string;
}

export const StarterDeckPreview: React.FC<StarterDeckPreviewProps> = ({
  cards,
  className = "",
}) => {
  // Get unique cards by cardTypeId (show one of each type)
  const uniqueCards = useMemo(() => {
    const seen = new Set<string>();
    return cards.filter((card) => {
      if (seen.has(card.cardTypeId)) return false;
      seen.add(card.cardTypeId);
      return true;
    });
  }, [cards]);

  if (cards.length === 0) {
    return (
      <div className={`starter-deck-preview ${className}`}>
        <div className="starter-deck-header">
          <h3 className="starter-deck-title">Starter Deck</h3>
          <span className="starter-deck-count">0 cards</span>
        </div>
        <div className="no-deck-message">
          Starter deck is not yet available for this class.
        </div>
      </div>
    );
  }

  return (
    <div className={`starter-deck-preview ${className}`}>
      <div className="starter-deck-header">
        <h3 className="starter-deck-title">Starter Deck</h3>
        <span className="starter-deck-count">{cards.length} cards</span>
      </div>
      <div className="starter-deck-cards">
        {uniqueCards.map((card) => (
          <div key={card.cardTypeId} className="starter-deck-card">
            <CardComponent card={card} depth={1} isPlayable={true} />
          </div>
        ))}
      </div>
    </div>
  );
};
