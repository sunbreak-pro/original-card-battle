// src/ui/cardUI/CardComponent.tsx
import React from "react";
import type { Card, Depth } from "../../domain/cards/type/cardType";
import { calculateEffectivePower } from "../../domain/cards/state/card";

interface CardComponentProps {
  card: Card;
  depth: Depth;
  isPlayable?: boolean;
  size?: "sm" | "md" | "lg";
}

// Tag colors for UI display (cardCategory remains for battle logic)

export const CardComponent: React.FC<CardComponentProps> = ({
  card,
  isPlayable = false,
}) => {
  // Use primary tag for UI display (tags[0]), fallback to "skill"
  const primaryTag = card.tags;
  const damage = card.baseDamage ? calculateEffectivePower(card) : null;

  // Build CSS class list for unified styling
  const cardClasses = [
    "card",
    isPlayable ? "playable" : "unplayable",
    `tag-${primaryTag}`,
    `mastery-${card.masteryLevel}`,
  ].join(" ");

  return (
    <div className={cardClasses}>
      <div className={`card-cost ${primaryTag}-badge`}>{card.cost}</div>
      <div className={`card-badge ${primaryTag}-badge`}>{primaryTag}</div>
      <div className={`card-name rarity-${card.rarity}`}>{card.name}</div>
      <div className="card-desc-box">
        <div className="card-desc-text">{card.description}</div>
        {damage && <div className="card-power">Damage: {damage}</div>}
      </div>
      <div className="mastery-info">
        <div className="mastery-labels">
          <span>熟練度: Lv.{card.masteryLevel}</span>
          <span>{card.useCount}</span>
        </div>
        <div className="mastery-bar-bg">
          <div
            className="mastery-bar-fill"
            style={{ width: `${(card.useCount / 8) * 100}%` }}
          />
        </div>
      </div>
    </div>
  );
};
