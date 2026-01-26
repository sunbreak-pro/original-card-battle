import React from "react";
import type { Item } from "../../domain/item_equipment/type/ItemTypes";
import "../css/pages/battle/battle-modals.css";

interface UseItemModalProps {
  isOpen: boolean;
  onClose: () => void;
  items: Item[];
  onUseItem: (item: Item) => void;
}

/**
 * Modal for using items during battle
 * Displays consumable items from player's inventory
 */
const UseItemModal: React.FC<UseItemModalProps> = ({
  isOpen,
  onClose,
  items,
  onUseItem,
}) => {
  if (!isOpen) return null;

  // Filter consumable items only
  const consumableItems = items.filter(
    (item) => item.itemType === "consumable"
  );

  const handleItemClick = (item: Item) => {
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
        <div className="use-item-grid">
          {consumableItems.length > 0 ? (
            consumableItems.map((item) => (
              <div
                key={item.id}
                className="use-item-card"
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
              使用可能なアイテムがありません
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default UseItemModal;
