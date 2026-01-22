// ItemDetailPanel Component - Displays detailed information about selected item

import React from "react";
import type { Item } from "../../../domain/item_equipment/type/ItemTypes";
import "../../css/camps/Storage.css";
interface ItemDetailPanelProps {
  item: Item | null;
  source: "storage" | "inventory" | "equipment" | "equipmentInventory";
  onMoveToStorage?: () => void;
  onMoveToInventory?: () => void;
  onMoveToEquipmentInventory?: () => void;
  onUnequipToEquipmentInventory?: () => void;
  onEquip?: () => void;
  onUnequip?: () => void;
  onDelete?: () => void;
}
interface EquipmentDetailPanelProps {
  item: Item | null;
  source: "storage" | "inventory" | "equipment" | "equipmentInventory";
  onMoveToStorage?: () => void;
  onMoveToEquipmentInventory?: () => void;
  onEquip?: () => void;
  onUnequipToEquipmentInventory?: () => void;
  onDelete?: () => void;
}

/**
 * ItemDetailPanel Component
 * Shows detailed item information and action buttons
 */
export const ItemDetailPanel: React.FC<ItemDetailPanelProps> = ({
  item,
  source,
  onMoveToStorage,
  onMoveToInventory,
  onMoveToEquipmentInventory,
  onEquip,
  onUnequip,
  onDelete,
}) => {
  // If no item selected, show placeholder
  if (!item) {
    return (
      <div className="item-detail-panel">
        <div className="item-detail-placeholder">
          Select an item to view details
        </div>
      </div>
    );
  }

  // Get rarity color for display
  const getRarityColor = () => {
    switch (item.rarity) {
      case "common":
        return "#9e9e9e";
      case "uncommon":
        return "#4caf50";
      case "rare":
        return "#2196f3";
      case "epic":
        return "#9c27b0";
      case "legendary":
        return "#ffc107";
      default:
        return "#ffffff";
    }
  };

  // Get quality color for equipment
  const getQualityColor = () => {
    if (item.itemType !== "equipment" || !item.quality) return "#ffffff";
    switch (item.quality) {
      case "poor":
        return "#8f8e8e";
      case "normal":
        return "#82e779";
      case "good":
        return "#f1f664";
      case "master":
        return "#ff2d2d";
      default:
        return "#ffffff";
    }
  };

  // Check if item is equipment
  const isEquipment = item.itemType === "equipment";

  return (
    <div className="item-detail-panel">
      {/* Item Name with Rarity Color */}
      <h3 className="item-detail-name" style={{ color: getRarityColor() }}>
        {item.name}
      </h3>

      {/* Item Icon (Large) */}
      <div className="item-detail-icon">{item.type}</div>

      {/* Rarity and Quality */}
      <div className="item-detail-rarity">
        Rarity:
        <span style={{ color: getRarityColor() }}>
          {" " + item.rarity.charAt(0).toUpperCase() + item.rarity.slice(1)}
        </span>
        {isEquipment && item.quality && (
          <span style={{ color: getQualityColor(), marginLeft: "1vw" }}>
            {" | "}
            {item.quality.charAt(0).toUpperCase() + item.quality.slice(1)}
          </span>
        )}
      </div>

      {/* Equipment-specific info */}
      {isEquipment && (
        <div className="item-detail-stats">
          {item.level !== undefined && (
            <div className="item-detail-stat">Level: {item.level}</div>
          )}
          {item.durability !== undefined &&
            item.maxDurability !== undefined && (
              <div className="item-detail-stat">
                Durability: {item.durability}/{item.maxDurability}
              </div>
            )}
          {item.equipmentSlot && (
            <div className="item-detail-stat">Slot: {item.equipmentSlot}</div>
          )}
        </div>
      )}

      {/* Equipment Effects */}
      {isEquipment && item.effects && item.effects.length > 0 && (
        <div className="item-detail-effects">
          <h4>Effects:</h4>
          {item.effects.map((effect, index) => (
            <div key={index} className="item-detail-effect">
              - {effect.description}
            </div>
          ))}
        </div>
      )}

      {/* Consumable Info */}
      {item.stackable && (
        <div className="item-detail-stats">
          <div className="item-detail-stat">
            Stack: {item.stackCount || 1}/{item.maxStack || 99}
          </div>
        </div>
      )}

      {/* Description */}
      <div className="item-detail-description">{item.description}</div>

      {/* Item Type */}
      <div className="item-detail-type">
        Type: {item.itemType.charAt(0).toUpperCase() + item.itemType.slice(1)}
      </div>

      {/* Sell Price */}
      {item.canSell && (
        <div className="item-detail-sell-price">
          Sell Price: {item.sellPrice} G
        </div>
      )}

      {/* Action Buttons */}
      <div className="item-detail-actions">
        {/* Move to Storage */}
        {source === "inventory" && (
          <button className="item-action-button" onClick={onMoveToStorage}>
            Move to Storage
          </button>
        )}

        {/* Move to Inventory */}
        {source === "storage" && !isEquipment && (
          <button className="item-action-button" onClick={onMoveToInventory}>
            Move to Inventory
          </button>
        )}

        {/* Equip (if equipment from storage or inventory) */}
        {isEquipment && source === "storage" && item.equipmentSlot && (
          <button className="item-action-button equip" onClick={onEquip}>
            Equip ({item.equipmentSlot})
          </button>
        )}
        {isEquipment && source === "storage" && (
          <button
            className="item-action-button equip"
            onClick={onMoveToEquipmentInventory}
          >
            Move to Equipment inventory
          </button>
        )}

        {/* Unequip (if from equipment) */}
        {source === "equipment" && (
          <>
            <button
              className="item-action-button"
              onClick={() => {
                onUnequip?.();
                // Will unequip to inventory, then user can move to storage
              }}
            >
              Unequip to Storage
            </button>
          </>
        )}

        {/* Delete (only from storage or inventory, not equipment) */}
        {(source === "storage" || source === "inventory") && (
          <button className="item-action-button delete" onClick={onDelete}>
            Delete
          </button>
        )}
      </div>
    </div>
  );
};

export const EquipmentDetailPanel: React.FC<EquipmentDetailPanelProps> = ({
  item,
  source,
  onMoveToStorage,
  onMoveToEquipmentInventory,
  onEquip,
  onDelete,
}) => {
  // If no item selected, show placeholder
  if (!item) {
    return (
      <div className="item-detail-panel">
        <div className="item-detail-placeholder">
          Select an equipment to view details
        </div>
      </div>
    );
  }

  // Get rarity color for display
  const getRarityColor = () => {
    switch (item.rarity) {
      case "common":
        return "#9e9e9e";
      case "uncommon":
        return "#4caf50";
      case "rare":
        return "#2196f3";
      case "epic":
        return "#9c27b0";
      case "legendary":
        return "#ffc107";
      default:
        return "#ffffff";
    }
  };

  // Get quality color for equipment
  const getQualityColor = () => {
    if (item.itemType !== "equipment" || !item.quality) return "#ffffff";
    switch (item.quality) {
      case "poor":
        return "#8f8e8e";
      case "normal":
        return "#82e779";
      case "good":
        return "#f1f664";
      case "master":
        return "#ff2d2d";
      default:
        return "#ffffff";
    }
  };

  // Check if item is equipment
  const isEquipment = item.itemType === "equipment";

  return (
    <div className="item-detail-panel">
      {/* Item Name with Rarity Color */}
      <h3 className="item-detail-name" style={{ color: getRarityColor() }}>
        {item.name}
      </h3>

      {/* Item Icon (Large) */}
      <div className="item-detail-icon">{item.type}</div>

      {/* Rarity and Quality */}
      <div className="item-detail-rarity">
        <span style={{ color: getRarityColor() }}>
          {item.rarity.charAt(0).toUpperCase() + item.rarity.slice(1)}
        </span>
        {isEquipment && item.quality && (
          <span style={{ color: getQualityColor(), marginLeft: "1vw" }}>
            {" | "}
            {item.quality.charAt(0).toUpperCase() + item.quality.slice(1)}
          </span>
        )}
      </div>

      {/* Equipment-specific info */}
      {isEquipment && (
        <div className="item-detail-stats">
          {item.level !== undefined && (
            <div className="item-detail-stat">Level: {item.level}</div>
          )}
          {item.durability !== undefined &&
            item.maxDurability !== undefined && (
              <div className="item-detail-stat">
                Durability: {item.durability}/{item.maxDurability}
              </div>
            )}
          {item.equipmentSlot && (
            <div className="item-detail-stat">Slot: {item.equipmentSlot}</div>
          )}
        </div>
      )}

      {/* Equipment Effects */}
      {isEquipment && item.effects && item.effects.length > 0 && (
        <div className="item-detail-effects">
          <h4>Effects:</h4>
          {item.effects.map((effect, index) => (
            <div key={index} className="item-detail-effect">
              - {effect.description}
            </div>
          ))}
        </div>
      )}

      {/* Consumable Info */}
      {item.stackable && (
        <div className="item-detail-stats">
          <div className="item-detail-stat">
            Stack: {item.stackCount || 1}/{item.maxStack || 99}
          </div>
        </div>
      )}

      {/* Description */}
      <div className="item-detail-description">{item.description}</div>

      {/* Item Type */}
      <div className="item-detail-type">
        Type: {item.itemType.charAt(0).toUpperCase() + item.itemType.slice(1)}
      </div>

      {/* Sell Price */}
      {item.canSell && (
        <div className="item-detail-sell-price">
          Sell Price: {item.sellPrice} G
        </div>
      )}

      {/* Action Buttons */}
      <div className="item-detail-actions">
        {isEquipment && source === "storage" && (
          <>
            <button className="item-action-button equip" onClick={onEquip}>
              Equip
            </button>
            <button
              className="item-action-button"
              onClick={onMoveToEquipmentInventory}
            >
              To Equipment Inventory
            </button>
            <button className="item-action-button delete" onClick={onDelete}>
              Delete
            </button>
          </>
        )}
        {source === "equipmentInventory" && (
          <>
            <button className="item-action-button equip" onClick={onEquip}>
              Equip
            </button>
            <button className="item-action-button" onClick={onMoveToStorage}>
              To Storage
            </button>
            <button className="item-action-button delete" onClick={onDelete}>
              Delete
            </button>
          </>
        )}
      </div>
    </div>
  );
};

export default { ItemDetailPanel, EquipmentDetailPanel };
