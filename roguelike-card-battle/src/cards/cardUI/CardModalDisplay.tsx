// src/components/CardPileModal.tsx
import React from "react";
import type { Card, Depth } from "../type/cardType";
import { CardComponent } from "./CardComponent"; // さっき作ったファイルをインポート
import "../../battles/battleUI/UIcss/BattleScreen.css"; // CSSは既存のファイルに追記する形でOK

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
    <div className="modal-overlay" onClick={onClose}>
      {/* e.stopPropagation() で、中身をクリックしても閉じないようにする */}
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>
            {title} ({cards.length})
          </h2>
          <button className="modal-close-btn" onClick={onClose}>
            ×
          </button>
        </div>

        <div className="modal-body">
          {cards.length === 0 ? (
            <div className="empty-message">捨て札は空です</div>
          ) : (
            <div className="card-grid">
              {cards.map((card, index) => (
                <div key={`${card.id}-${index}`} className="modal-card-wrapper">
                  {/* モーダル内のカードはプレイ不可なので isPlayable={false} */}
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
