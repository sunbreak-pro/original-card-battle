import { useState } from "react";
import { usePlayer } from "@/contexts/PlayerContext";
import { useResources } from "@/contexts/ResourceContext";
import type { Item } from "@/types/itemTypes";
import { calculateMagicStoneValue } from "@/domain/item_equipment/logic/itemUtils";
import type { EquipmentQuality } from "@/types/itemTypes";
import type { QualityUpOption } from "@/types/campTypes";
import { QUALITY_NAMES, QUALITY_COLORS } from "@/constants/campConstants";
import {
  MAX_EQUIPMENT_LEVEL,
  getNextQuality,
} from "@/domain/camps/logic/blacksmithUtils";
import { QUALITY_UP_OPTIONS } from "@/constants/data/camps/BlacksmithData";
import {
  canLevelUpgrade,
  canQualityUpgrade,
  getLevelUpgradeCost,
  getUpgradePreview,
  performLevelUpgrade,
  getQualityUpgradeCost,
  getQualitySuccessChance,
  attemptQualityUpgrade,
  canAfford,
  getEquippedItems,
} from "@/domain/camps/logic/blacksmithLogic";
import BlacksmithItemCard from "./BlacksmithItemCard";

const UpgradeTab = () => {
  const { playerData, updatePlayerData } = usePlayer();
  const { spendGold: deductGold, spendBaseCampMagicStones } = useResources();
  const [selectedItem, setSelectedItem] = useState<Item | null>(null);
  const [selectedQualityOption, setSelectedQualityOption] =
    useState<QualityUpOption>("normal");
  const [notification, setNotification] = useState<{
    message: string;
    type: "success" | "error";
  } | null>(null);

  // Get equipment items from storage and equipped slots
  const equipmentItems = [
    ...playerData.inventory.storage.items.filter((item) => item.itemType === "equipment"),
    ...getEquippedItems(playerData.inventory.equipmentSlots),
  ];

  const totalMagicStoneValue = calculateMagicStoneValue(
    playerData.resources.baseCampMagicStones,
  );

  const showNotification = (message: string, type: "success" | "error") => {
    setNotification({ message, type });
    setTimeout(() => setNotification(null), 2000);
  };

  // Check if an item is equipped (in equipment slots)
  const isEquippedItem = (itemId: string) => {
    const slots = playerData.inventory.equipmentSlots;
    return (
      slots.weapon?.id === itemId ||
      slots.armor?.id === itemId ||
      slots.helmet?.id === itemId ||
      slots.boots?.id === itemId ||
      slots.accessory1?.id === itemId ||
      slots.accessory2?.id === itemId
    );
  };

  // Update item in storage or equipment slots
  const updateStorageItem = (updatedItem: Item) => {
    if (isEquippedItem(updatedItem.id)) {
      // Update in equipment slots
      const slots = playerData.inventory.equipmentSlots;
      const newSlots = {
        weapon: slots.weapon?.id === updatedItem.id ? updatedItem : slots.weapon,
        armor: slots.armor?.id === updatedItem.id ? updatedItem : slots.armor,
        helmet: slots.helmet?.id === updatedItem.id ? updatedItem : slots.helmet,
        boots: slots.boots?.id === updatedItem.id ? updatedItem : slots.boots,
        accessory1: slots.accessory1?.id === updatedItem.id ? updatedItem : slots.accessory1,
        accessory2: slots.accessory2?.id === updatedItem.id ? updatedItem : slots.accessory2,
      };
      updatePlayerData({
        inventory: {
          ...playerData.inventory,
          equipmentSlots: newSlots,
        },
      });
    } else {
      // Update in storage
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
    }
    setSelectedItem(updatedItem);
  };

  // Handle level upgrade
  const handleLevelUpgrade = () => {
    if (!selectedItem) return;

    const cost = getLevelUpgradeCost(selectedItem);
    if (!cost) {
      showNotification("This item cannot be upgraded further", "error");
      return;
    }

    if (
      !canAfford(playerData.resources.baseCampGold, totalMagicStoneValue, cost)
    ) {
      showNotification("Not enough resources", "error");
      return;
    }

    // Deduct resources
    if (!deductGold(cost.gold)) {
      showNotification("Failed to deduct gold", "error");
      return;
    }
    if (cost.magicStones > 0) {
      spendBaseCampMagicStones(cost.magicStones);
    }

    // Perform upgrade
    const upgradedItem = performLevelUpgrade(selectedItem);
    updateStorageItem(upgradedItem);

    showNotification(
      `Level upgraded! ${selectedItem.name} is now Lv.${upgradedItem.level}`,
      "success",
    );
  };

  // Handle quality upgrade
  const handleQualityUpgrade = () => {
    if (!selectedItem) return;

    const cost = getQualityUpgradeCost(selectedQualityOption);

    if (
      !canAfford(playerData.resources.baseCampGold, totalMagicStoneValue, cost)
    ) {
      showNotification("Not enough resources", "error");
      return;
    }

    // Deduct resources
    if (!deductGold(cost.gold)) {
      showNotification("Failed to deduct gold", "error");
      return;
    }
    if (cost.magicStones > 0) {
      spendBaseCampMagicStones(cost.magicStones);
    }

    // Attempt quality upgrade (includes RNG)
    const result = attemptQualityUpgrade(selectedItem, selectedQualityOption);

    if (result.success && result.item) {
      updateStorageItem(result.item);
      showNotification(result.message, "success");
    } else {
      showNotification(result.message, "error");
    }
  };

  // Get upgrade preview
  const upgradePreview = selectedItem
    ? getUpgradePreview(
        selectedItem,
        playerData.resources.baseCampGold,
        totalMagicStoneValue,
      )
    : null;

  // Get quality upgrade info
  const qualitySuccessChance = selectedItem?.quality
    ? getQualitySuccessChance(
        selectedItem.quality as EquipmentQuality,
        selectedQualityOption,
      )
    : 0;
  const qualityCost = getQualityUpgradeCost(selectedQualityOption);
  const canAffordQuality = canAfford(
    playerData.resources.baseCampGold,
    totalMagicStoneValue,
    qualityCost,
  );

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
        <h3 className="blacksmith-section-title">
          Equipment ({equipmentItems.length})
        </h3>

        {equipmentItems.length === 0 ? (
          <div className="empty-message">No equipment in storage</div>
        ) : (
          <div className="equipment-grid">
            {equipmentItems.map((item) => (
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
          <div className="no-selection">Select equipment to upgrade</div>
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

            {/* Current Stats */}
            <div className="stats-section">
              {selectedItem.effects?.map((effect, index) => (
                <div key={index} className="stat-row">
                  <span className="label">{effect.target.toUpperCase()}</span>
                  <span className="value">+{effect.value}</span>
                </div>
              ))}
              {selectedItem.maxDurability && (
                <div className="stat-row">
                  <span className="label">MAX AP</span>
                  <span className="value">{selectedItem.maxDurability}</span>
                </div>
              )}
            </div>

            {/* Level Upgrade Section */}
            {canLevelUpgrade(selectedItem) && upgradePreview && (
              <div className="action-section">
                <h5 className="blacksmith-section-title">Level Upgrade</h5>

                {/* Preview */}
                <div className="stats-section">
                  {upgradePreview.statChanges.map((change, index) => (
                    <div key={index} className="stat-row">
                      <span className="label">{change.stat.toUpperCase()}</span>
                      <span className="value">
                        {change.before} →{" "}
                        <span className="improved">{change.after}</span>
                      </span>
                    </div>
                  ))}
                </div>

                {/* Cost */}
                <div className="cost-display">
                  <span
                    className={`cost-item gold ${playerData.resources.baseCampGold < upgradePreview.cost.gold ? "insufficient" : ""}`}
                  >
                    💰 {upgradePreview.cost.gold} G
                  </span>
                  <span
                    className={`cost-item stones ${totalMagicStoneValue < upgradePreview.cost.magicStones ? "insufficient" : ""}`}
                  >
                    💎 {upgradePreview.cost.magicStones} G
                  </span>
                </div>

                <button
                  className="action-button"
                  onClick={handleLevelUpgrade}
                  disabled={!upgradePreview.canAfford}
                >
                  Upgrade to Lv.{upgradePreview.nextLevel}
                </button>
              </div>
            )}

            {/* Max Level Message */}
            {!canLevelUpgrade(selectedItem) && (
              <div className="action-section">
                <div
                  className="empty-message"
                  style={{ height: "auto", padding: "1vh 0" }}
                >
                  Maximum level reached (Lv.{MAX_EQUIPMENT_LEVEL})
                </div>
              </div>
            )}

            {/* Quality Upgrade Section */}
            {canQualityUpgrade(selectedItem) && (
              <div className="action-section">
                <h5 className="blacksmith-section-title">Quality Upgrade</h5>

                {/* Current → Target */}
                <div className="stat-row">
                  <span className="label">Quality</span>
                  <span className="value">
                    <span
                      style={{
                        color:
                          QUALITY_COLORS[
                            (selectedItem.quality ??
                              "normal") as EquipmentQuality
                          ],
                      }}
                    >
                      {
                        QUALITY_NAMES[
                          (selectedItem.quality ?? "normal") as EquipmentQuality
                        ]
                      }
                    </span>
                    {" → "}
                    <span
                      style={{
                        color:
                          QUALITY_COLORS[
                            getNextQuality(
                              (selectedItem.quality ??
                                "normal") as EquipmentQuality,
                            ) ?? "normal"
                          ],
                      }}
                    >
                      {
                        QUALITY_NAMES[
                          getNextQuality(
                            (selectedItem.quality ??
                              "normal") as EquipmentQuality,
                          ) ?? "normal"
                        ]
                      }
                    </span>
                  </span>
                </div>

                {/* Quality Options */}
                <div className="quality-options">
                  {(Object.keys(QUALITY_UP_OPTIONS) as QualityUpOption[]).map(
                    (option) => {
                      const config = QUALITY_UP_OPTIONS[option];
                      const optionCost = getQualityUpgradeCost(option);
                      const chance = getQualitySuccessChance(
                        (selectedItem.quality ?? "normal") as EquipmentQuality,
                        option,
                      );
                      const canAffordOption = canAfford(
                        playerData.resources.baseCampGold,
                        totalMagicStoneValue,
                        optionCost,
                      );

                      return (
                        <div
                          key={option}
                          className={`quality-option ${selectedQualityOption === option ? "selected" : ""} ${!canAffordOption ? "disabled" : ""}`}
                          onClick={() =>
                            canAffordOption && setSelectedQualityOption(option)
                          }
                        >
                          <div className="quality-option-header">
                            <span className="quality-option-name">
                              {config.label}
                            </span>
                            <span className="quality-option-chance">
                              {Math.floor(chance * 100)}%
                            </span>
                          </div>
                          <div className="quality-option-desc">
                            {config.description}
                          </div>
                          <div className="quality-option-cost">
                            <span
                              className={`cost-item gold ${playerData.resources.baseCampGold < optionCost.gold ? "insufficient" : ""}`}
                            >
                              💰 {optionCost.gold} G
                            </span>
                            <span
                              className={`cost-item stones ${totalMagicStoneValue < optionCost.magicStones ? "insufficient" : ""}`}
                            >
                              💎 {optionCost.magicStones} G
                            </span>
                          </div>
                        </div>
                      );
                    },
                  )}
                </div>

                <button
                  className="action-button secondary"
                  onClick={handleQualityUpgrade}
                  disabled={!canAffordQuality}
                >
                  Attempt Quality Upgrade (
                  {Math.floor(qualitySuccessChance * 100)}%)
                </button>
              </div>
            )}

            {/* Max Quality Message */}
            {!canQualityUpgrade(selectedItem) && (
              <div className="action-section">
                <div
                  className="empty-message"
                  style={{ height: "auto", padding: "1vh 0" }}
                >
                  Maximum quality reached (Master)
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

export default UpgradeTab;
