import React from "react";
import type { Card, Depth } from "../../domain/cards/type/cardType";
import { CardComponent } from "./CardComponent";
import "../css/others/BattleScreen.css";

interface CardPileModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  cards: Card[];
  depth: Depth;
}

export const BattlingCardPileModal: React.FC<CardPileModalProps> = ({
  isOpen,
  onClose,
  title,
  cards,
  depth,
}) => {
  if (!isOpen) return null;

  return (
    <div className="battle-modal-overlay" onClick={onClose}>
      <div className="battle-modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="battle-modal-header">
          <h2>
            {title} ({cards.length})
          </h2>
          <button className="battle-modal-close-btn" onClick={onClose}>
            ×
          </button>
        </div>

        <div className="battle-modal-body">
          {cards.length === 0 ? (
            <div className="battle-empty-message">捨て札は空です</div>
          ) : (
            <div className="card-grid">
              {cards.map((card, index) => (
                <div key={`${card.id}-${index}`} className="modal-card-wrapper">
                  <CardComponent card={card} depth={depth} isPlayable={false} />
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
