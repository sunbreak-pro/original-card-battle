// src/ui/cardUI/CardComponent.tsx
import React from "react";
import type { Card, Depth } from "@/types/cardTypes";
import type { ElementType } from "@/types/characterTypes";
import { calculateEffectivePower } from "../../domain/cards/state/card";
import { getElementIcon } from "../../constants/uiConstants";
import { MASTERY_THRESHOLDS } from "../../constants/cardConstants";

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
  const primaryTag = card.tags[0] ?? "skill";
  const damage = card.baseDamage ? calculateEffectivePower(card) : null;

  // Build CSS class list for unified styling
  const cardClasses = [
    "card",
    isPlayable ? "playable" : "unplayable",
    `tag-${primaryTag}`,
    `mastery-${card.masteryLevel}`,
  ].join(" ");

  const elementIcons = card.element
    .map((e) => ({ element: e, path: getElementIcon(e) }))
    .filter(
      (entry): entry is { element: ElementType; path: string } =>
        entry.path !== undefined,
    );
  const multiElement = elementIcons.length > 1;

  return (
    <div className={cardClasses}>
      <div className={`card-cost ${primaryTag}-badge`}>{card.cost}</div>
      <div className="card-header">
        {elementIcons.length > 0 && (
          <div className={`card-element-icons${multiElement ? "multi" : ""}`}>
            {elementIcons.map(({ element, path }) => (
              <img
                key={element}
                className="card-element-icon"
                src={path}
                alt={element}
              />
            ))}
          </div>
        )}
        <div className={`card-badge ${primaryTag}-badge`}>{primaryTag}</div>
      </div>
      <div className="card-body">
        <div className={`card-name mastery-${card.masteryLevel}`}>
          {card.name}
        </div>
        <div className="card-desc-box">
          <div className="card-desc-text">{card.description}</div>
          {damage && <div className="card-power">Damage: {damage}</div>}
        </div>
      </div>
      <div className="mastery-info">
        <div className="mastery-labels">
          <span>熟練度: Lv.{card.masteryLevel}</span>
          <span>{card.useCount}</span>
        </div>
        <div className="mastery-bar-bg">
          <div
            className="mastery-bar-fill"
            style={{
              width: `${(() => {
                const currentThreshold = MASTERY_THRESHOLDS[card.masteryLevel];
                const nextLevel = Math.min(card.masteryLevel + 1, 4) as
                  | 0
                  | 1
                  | 2
                  | 3
                  | 4;
                const nextThreshold = MASTERY_THRESHOLDS[nextLevel];
                if (currentThreshold === nextThreshold) return 100;
                return (
                  ((card.useCount - currentThreshold) /
                    (nextThreshold - currentThreshold)) *
                  100
                );
              })()}%`,
            }}
          />
        </div>
      </div>
    </div>
  );
};
