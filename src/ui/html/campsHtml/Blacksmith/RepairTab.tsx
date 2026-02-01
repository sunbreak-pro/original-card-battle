import { useState } from "react";
import { usePlayer } from "@/contexts/PlayerContext";
import { useResources } from "@/contexts/ResourceContext";
import type { Item } from "@/types/itemTypes";
import type { EquipmentQuality } from "@/types/itemTypes";
import { QUALITY_NAMES, QUALITY_COLORS } from "@/constants/campConstants";
import {
  getRepairCost,
  performRepair,
  canAffordRepair,
  getRepairableItems,
  getTotalRepairCost,
} from "@/domain/camps/logic/blacksmithLogic";
import BlacksmithItemCard from "./BlacksmithItemCard";

const RepairTab = () => {
  const { playerData, updatePlayerData } = usePlayer();
  const { spendGold: deductGold } = useResources();
  const [selectedItem, setSelectedItem] = useState<Item | null>(null);
  const [notification, setNotification] = useState<{
    message: string;
    type: "success" | "error";
  } | null>(null);

  // Get equipment items that need repair from storage
  const repairableItems = getRepairableItems(
    playerData.inventory.storage.items,
  );
  const totalRepairCost = getTotalRepairCost(repairableItems);

  const showNotification = (message: string, type: "success" | "error") => {
    setNotification({ message, type });
    setTimeout(() => setNotification(null), 2000);
  };

  // Update item in storage
  const updateStorageItem = (updatedItem: Item) => {
    const newItems = playerData.inventory.storage.items.map((item) =>
      item.id === updatedItem.id ? updatedItem : item,
    );
    updatePlayerData({
      inventory: {
        ...playerData.inventory,
        storage: {
          ...playerData.inventory.storage,
          items: newItems,
        },
      },
    });
    setSelectedItem(updatedItem);
  };

  // Update multiple items in storage
  const updateMultipleStorageItems = (updatedItems: Item[]) => {
    const updatedIds = new Set(updatedItems.map((item) => item.id));
    const newItems = playerData.inventory.storage.items.map((item) => {
      if (updatedIds.has(item.id)) {
        return updatedItems.find((u) => u.id === item.id) ?? item;
      }
      return item;
    });
    updatePlayerData({
      inventory: {
        ...playerData.inventory,
        storage: {
          ...playerData.inventory.storage,
          items: newItems,
        },
      },
    });
  };

  // Handle single item repair
  const handleRepair = () => {
    if (!selectedItem) return;

    const cost = getRepairCost(selectedItem);
    if (!cost) {
      showNotification("This item doesn't need repair", "error");
      return;
    }

    if (!canAffordRepair(playerData.resources.baseCampGold, cost)) {
      showNotification("Not enough gold", "error");
      return;
    }

    // Deduct gold
    if (!deductGold(cost.gold)) {
      showNotification("Failed to deduct gold", "error");
      return;
    }

    // Perform repair
    const repairedItem = performRepair(selectedItem);
    updateStorageItem(repairedItem);

    showNotification(
      `${selectedItem.name} has been fully repaired!`,
      "success",
    );
  };

  // Handle repair all
  const handleRepairAll = () => {
    if (repairableItems.length === 0) {
      showNotification("No items need repair", "error");
      return;
    }

    if (playerData.resources.baseCampGold < totalRepairCost) {
      showNotification("Not enough gold for all repairs", "error");
      return;
    }

    // Deduct gold
    if (!deductGold(totalRepairCost)) {
      showNotification("Failed to deduct gold", "error");
      return;
    }

    // Repair all items
    const repairedItems = repairableItems.map((item) => performRepair(item));
    updateMultipleStorageItems(repairedItems);

    // Clear selection if the selected item was repaired
    if (
      selectedItem &&
      repairableItems.some((item) => item.id === selectedItem.id)
    ) {
      const repairedSelected = repairedItems.find(
        (item) => item.id === selectedItem.id,
      );
      if (repairedSelected) {
        setSelectedItem(repairedSelected);
      }
    }

    showNotification(`Repaired ${repairableItems.length} items!`, "success");
  };

  // Get repair cost for selected item
  const repairCost = selectedItem ? getRepairCost(selectedItem) : null;
  const canAffordSelectedRepair = repairCost
    ? canAffordRepair(playerData.resources.baseCampGold, repairCost)
    : false;

  return (
    <div className="tab-layout">
      {/* Notification */}
      {notification && (
        <div className={`blacksmith-notification ${notification.type}`}>
          {notification.message}
        </div>
      )}

      {/* Equipment List */}
      <div className="equipment-list-section">
        {/* Repair All Section */}
        {repairableItems.length > 0 && (
          <div className="repair-all-section">
            <div className="repair-all-info">
              <span className="repair-all-title">
                Repair All ({repairableItems.length} items)
              </span>
              <span className="repair-all-cost">
                Total Cost: {totalRepairCost} G
              </span>
            </div>
            <button
              className="repair-all-button"
              onClick={handleRepairAll}
              disabled={playerData.resources.baseCampGold < totalRepairCost}
            >
              Repair All
            </button>
          </div>
        )}

        <h3 className="blacksmith-section-title">
          Damaged Equipment ({repairableItems.length})
        </h3>

        {repairableItems.length === 0 ? (
          <div className="empty-message">All equipment is fully repaired</div>
        ) : (
          <div className="equipment-grid">
            {repairableItems.map((item) => (
              <BlacksmithItemCard
                key={item.id}
                item={item}
                isSelected={selectedItem?.id === item.id}
                onClick={() => setSelectedItem(item)}
                showDurability={true}
              />
            ))}
          </div>
        )}
      </div>

      {/* Detail Panel */}
      <div className="detail-panel">
        {!selectedItem ? (
          <div className="no-selection">Select equipment to repair</div>
        ) : (
          <div className="item-detail">
            {/* Header */}
            <div className="detail-header">
              <span className="detail-icon">{selectedItem.type}</span>
              <div className="detail-title">
                <h4 className="detail-name">{selectedItem.name}</h4>
                <div className="detail-badges">
                  <span className="badge level">
                    Lv.{selectedItem.level ?? 0}
                  </span>
                  <span
                    className={`badge quality-${selectedItem.quality ?? "normal"}`}
                    style={{
                      color:
                        QUALITY_COLORS[
                          (selectedItem.quality ?? "normal") as EquipmentQuality
                        ],
                    }}
                  >
                    {
                      QUALITY_NAMES[
                        (selectedItem.quality ?? "normal") as EquipmentQuality
                      ]
                    }
                  </span>
                </div>
              </div>
            </div>

            {/* Durability Display */}
            <div className="durability-display">
              <span className="label">Durability</span>
              <div className="bar">
                <div
                  className="fill"
                  style={{
                    width: `${((selectedItem.durability ?? 0) / (selectedItem.maxDurability ?? 100)) * 100}%`,
                  }}
                />
              </div>
              <span className="text">
                {selectedItem.durability ?? 0} /{" "}
                {selectedItem.maxDurability ?? 100}
              </span>
            </div>

            {/* Repair Info */}
            {repairCost && (
              <div className="action-section">
                <h5 className="blacksmith-section-title">Repair</h5>

                <div className="stats-section">
                  <div className="stat-row">
                    <span className="label">Durability to Restore</span>
                    <span className="value improved">
                      +{repairCost.durabilityRestored}
                    </span>
                  </div>
                  <div className="stat-row">
                    <span className="label">After Repair</span>
                    <span className="value">
                      {selectedItem.maxDurability ?? 100} /{" "}
                      {selectedItem.maxDurability ?? 100}
                    </span>
                  </div>
                </div>

                {/* Cost */}
                <div className="cost-display">
                  <span
                    className={`cost-item gold ${!canAffordSelectedRepair ? "insufficient" : ""}`}
                  >
                    💰 {repairCost.gold} G
                  </span>
                </div>

                <button
                  className="action-button"
                  onClick={handleRepair}
                  disabled={!canAffordSelectedRepair}
                >
                  Repair Equipment
                </button>
              </div>
            )}

            {/* Already Repaired */}
            {!repairCost && (
              <div className="action-section">
                <div
                  className="empty-message"
                  style={{ height: "auto", padding: "1vh 0" }}
                >
                  This equipment is fully repaired
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

export default RepairTab;
