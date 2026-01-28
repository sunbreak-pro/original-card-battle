import React from "react";
import type { Item } from '@/types/itemTypes';
import "../css/pages/battle/battle-modals.css";

interface UseItemModalProps {
  isOpen: boolean;
  onClose: () => void;
  items: Item[];
  onUseItem: (item: Item) => void;
  itemUsedThisPhase?: boolean;
}

/**
 * Modal for using items during battle
 * Displays consumable items from player's inventory that are usable in battle
 */
const UseItemModal: React.FC<UseItemModalProps> = ({
  isOpen,
  onClose,
  items,
  onUseItem,
  itemUsedThisPhase = false,
}) => {
  if (!isOpen) return null;

  // Filter consumable items that are usable in battle
  const battleUsableItems = items.filter(
    (item) =>
      item.itemType === "consumable" &&
      (item.usableContext === "battle" || item.usableContext === "anywhere" || !item.usableContext)
  );

  const handleItemClick = (item: Item) => {
    if (itemUsedThisPhase) return;
    onUseItem(item);
    onClose();
  };

  const handleBackdropClick = (e: React.MouseEvent) => {
    if (e.target === e.currentTarget) {
      onClose();
    }
  };

  return (
    <div className="use-item-modal-backdrop" onClick={handleBackdropClick}>
      <div className="use-item-modal-content">
        <div className="use-item-modal-header">
          <h2 className="use-item-modal-title">アイテムを使う</h2>
          <button className="use-item-close-btn" onClick={onClose}>
            ✕
          </button>
        </div>
        {itemUsedThisPhase && (
          <div className="item-used-warning">
            このフェーズでは既にアイテムを使用しました
          </div>
        )}
        <div className="use-item-grid">
          {battleUsableItems.length > 0 ? (
            battleUsableItems.map((item) => (
              <div
                key={item.id}
                className={`use-item-card ${itemUsedThisPhase ? 'disabled' : ''}`}
                onClick={() => handleItemClick(item)}
              >
                <div className={`item-icon-container rarity-${item.rarity}`}>
                  <span className="item-icon">{item.type}</span>
                </div>
                <div className="item-name">{item.name}</div>
                {item.stackCount && item.stackCount > 1 && (
                  <div className="item-quantity">x{item.stackCount}</div>
                )}
              </div>
            ))
          ) : (
            <div className="no-items-message">
              戦闘中に使用可能なアイテムがありません
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default UseItemModal;
