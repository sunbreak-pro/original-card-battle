// src/components/CardComponent.tsx
import React from "react";
import type { Card, Depth } from "../../cards/type/cardType";
import { calculateEffectivePower } from "../../cards/state/card";

interface CardComponentProps {
  card: Card;
  depth: Depth;
  isPlayable?: boolean; // モーダル表示用などで省略可能にするため ? を付与
}

export const CardComponent: React.FC<CardComponentProps> = ({
  card,
  isPlayable = false,
}) => {
  const categoryColors: Record<string, string> = {
    physical: "#d94a4a",
    defense: "#4a8ed9",
    magic: "#9a4ad9",
    heal: "#4ade80",
  };
  const catColor = categoryColors[card.category] || "#ccc";
  const damage = card.baseDamage ? calculateEffectivePower(card) : null;

  return (
    <div
      className={`card ${isPlayable ? "playable" : "unplayable"}`}
      style={
        {
          "--category-bg": `radial-gradient(circle, #fff 0%, ${catColor} 100%)`,
          "--category-bg-alpha": `linear-gradient(135deg, ${catColor} 0%, ${catColor}dd 100%)`,
        } as React.CSSProperties
      }
    >
      <div className="card-cost">{card.cost}</div>
      <div className="card-badge">{card.category}</div>
      <div className="card-name">{card.name}</div>
      <div className="card-desc-box">
        <div className="card-desc-text">{card.description}</div>
        {damage && <div className="card-power">Damage: {damage}</div>}
      </div>
      <div className="mastery-info">
        <div className="mastery-labels">
          <span>Lv.{card.masteryLevel}</span>
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
