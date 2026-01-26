import { useState } from "react";
import { usePlayer } from "../../../domain/camps/contexts/PlayerContext";
import { useInventory } from "../../../domain/camps/contexts/InventoryContext";
import type { Item } from "../../../domain/item_equipment/type/ItemTypes";
import type { EquipmentQuality } from "../../../domain/item_equipment/type/EquipmentType";
import { QUALITY_NAMES, QUALITY_COLORS } from "../../../domain/camps/types/BlacksmithTypes";
import {
  getDismantlePreview,
  performDismantle,
  shouldWarnOnDismantle,
  getDismantleableItems,
} from "../../../domain/camps/logic/blacksmithLogic";
import BlacksmithItemCard from "./BlacksmithItemCard";

const DismantleTab = () => {
  const { playerData, addGold, addMagicStones } = usePlayer();
  const { removeItemFromStorage } = useInventory();
  const [selectedItem, setSelectedItem] = useState<Item | null>(null);
  const [showConfirmModal, setShowConfirmModal] = useState(false);
  const [notification, setNotification] = useState<{ message: string; type: "success" | "error" } | null>(null);

  // Get dismantleable equipment items from storage
  const dismantleableItems = getDismantleableItems(playerData.inventory.storage.items);

  const showNotification = (message: string, type: "success" | "error") => {
    setNotification({ message, type });
    setTimeout(() => setNotification(null), 2500);
  };

  // Handle dismantle
  const handleDismantle = () => {
    if (!selectedItem) return;

    // Check if warning should be shown
    if (shouldWarnOnDismantle(selectedItem) && !showConfirmModal) {
      setShowConfirmModal(true);
      return;
    }

    // Perform dismantle
    const result = performDismantle(selectedItem);

    if (result.success) {
      // Remove item from storage
      removeItemFromStorage(selectedItem.id);

      // Add gold return
      if (result.goldChange) {
        addGold(result.goldChange, true);
      }

      // Add magic stone bonus (if any)
      if (result.magicStoneChange && result.magicStoneChange > 0) {
        // Add as medium stones (value: 100)
        addMagicStones({ medium: 1 }, true);
      }

      setSelectedItem(null);
      setShowConfirmModal(false);
      showNotification(result.message, "success");
    } else {
      showNotification(result.message, "error");
    }
  };

  // Get dismantle preview
  const dismantlePreview = selectedItem ? getDismantlePreview(selectedItem) : null;

  return (
    <div className="tab-layout">
      {/* Notification */}
      {notification && (
        <div className={`blacksmith-notification ${notification.type}`}>
          {notification.message}
        </div>
      )}

      {/* Confirm Modal */}
      {showConfirmModal && selectedItem && (
        <div className="confirm-modal-overlay" onClick={() => setShowConfirmModal(false)}>
          <div className="confirm-modal" onClick={(e) => e.stopPropagation()}>
            <h3>Dismantle Valuable Item?</h3>
            <p>
              "{selectedItem.name}" is a valuable item (
              {selectedItem.rarity} / Lv.{selectedItem.level ?? 0} /{" "}
              {QUALITY_NAMES[(selectedItem.quality ?? "normal") as EquipmentQuality]}
              ). This action cannot be undone.
            </p>
            <div className="confirm-buttons">
              <button
                className="confirm-button cancel"
                onClick={() => setShowConfirmModal(false)}
              >
                Cancel
              </button>
              <button className="confirm-button confirm" onClick={handleDismantle}>
                Dismantle
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Equipment List */}
      <div className="equipment-list-section">
        <h3 className="blacksmith-section-title">
          Dismantleable Equipment ({dismantleableItems.length})
        </h3>

        {dismantleableItems.length === 0 ? (
          <div className="empty-message">No equipment available for dismantle</div>
        ) : (
          <div className="equipment-grid">
            {dismantleableItems.map((item) => (
              <BlacksmithItemCard
                key={item.id}
                item={item}
                isSelected={selectedItem?.id === item.id}
                onClick={() => setSelectedItem(item)}
              />
            ))}
          </div>
        )}
      </div>

      {/* Detail Panel */}
      <div className="detail-panel">
        {!selectedItem ? (
          <div className="no-selection">Select equipment to dismantle</div>
        ) : (
          <div className="item-detail">
            {/* Header */}
            <div className="detail-header">
              <span className="detail-icon">{selectedItem.type}</span>
              <div className="detail-title">
                <h4 className="detail-name">{selectedItem.name}</h4>
                <div className="detail-badges">
                  <span className="badge level">Lv.{selectedItem.level ?? 0}</span>
                  <span
                    className={`badge quality-${selectedItem.quality ?? "normal"}`}
                    style={{ color: QUALITY_COLORS[(selectedItem.quality ?? "normal") as EquipmentQuality] }}
                  >
                    {QUALITY_NAMES[(selectedItem.quality ?? "normal") as EquipmentQuality]}
                  </span>
                </div>
              </div>
            </div>

            {/* Warning for valuable items */}
            {shouldWarnOnDismantle(selectedItem) && (
              <div className="dismantle-warning">
                <span className="warning-icon">⚠️</span>
                <span className="warning-text">
                  This is a valuable item! Consider carefully before dismantling.
                </span>
              </div>
            )}

            {/* Current Stats */}
            <div className="stats-section">
              <div className="stat-row">
                <span className="label">Rarity</span>
                <span className={`value rarity-${selectedItem.rarity}`}>
                  {selectedItem.rarity.charAt(0).toUpperCase() + selectedItem.rarity.slice(1)}
                </span>
              </div>
              <div className="stat-row">
                <span className="label">Sell Price</span>
                <span className="value">{selectedItem.sellPrice} G</span>
              </div>
              {selectedItem.effects?.map((effect, index) => (
                <div key={index} className="stat-row">
                  <span className="label">{effect.target.toUpperCase()}</span>
                  <span className="value">+{effect.value}</span>
                </div>
              ))}
            </div>

            {/* Dismantle Preview */}
            {dismantlePreview && (
              <div className="action-section">
                <h5 className="blacksmith-section-title">Dismantle Result</h5>

                <div className="dismantle-preview">
                  <div className="preview-row">
                    <span className="label">Gold Return</span>
                    <span className="value gold">+{dismantlePreview.goldReturn} G</span>
                  </div>
                  {dismantlePreview.magicStoneChance > 0 && (
                    <>
                      <div className="preview-row">
                        <span className="label">Bonus Chance</span>
                        <span className="value bonus">
                          {Math.floor(dismantlePreview.magicStoneChance * 100)}%
                        </span>
                      </div>
                      <div className="preview-row">
                        <span className="label">Bonus Reward</span>
                        <span className="value">Magic Stone (Medium)</span>
                      </div>
                    </>
                  )}
                  {dismantlePreview.magicStoneChance === 0 && (
                    <div className="preview-row">
                      <span className="label">Bonus</span>
                      <span className="value">None (Rare+ items only)</span>
                    </div>
                  )}
                </div>

                <button
                  className="action-button danger"
                  onClick={handleDismantle}
                >
                  Dismantle Equipment
                </button>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

export default DismantleTab;
