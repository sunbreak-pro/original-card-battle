// ItemCard Component - Displays a single item in storage/inventory grid

import React from "react";
import type { Item } from "@/types/itemTypes";
import "../../../css/camps/Storage.css";

interface ItemCardProps {
  item: Item;
  isSelected: boolean;
  onClick: () => void;
  compact?: boolean;
}

/**
 * ItemCard Component
 * Displays item with icon, name, rarity, and level
 * Supports selection highlighting and hover effects
 */
export const ItemCard: React.FC<ItemCardProps> = ({
  item,
  isSelected,
  onClick,
  compact = false,
}) => {
  // Get rarity class for border color
  const getRarityClass = () => {
    switch (item.rarity) {
      case "common":
        return "rarity-common";
      case "uncommon":
        return "rarity-uncommon";
      case "rare":
        return "rarity-rare";
      case "epic":
        return "rarity-epic";
      case "legendary":
        return "rarity-legendary";
      default:
        return "rarity-common";
    }
  };

  // Get quality class for equipment (if applicable)
  const getQualityClass = () => {
    if (item.itemType !== "equipment" || !item.quality) return "";
    return `quality-${item.quality}`;
  };

  // Truncate item name if too long
  const truncateName = (name: string, maxLength: number = 15) => {
    if (name.length <= maxLength) return name;
    return (
      <>
        {name.split(" ").map((word, index) => (
          <span key={index}>{" " + word}</span>
        ))}
      </>
    );
  };

  const cardClass = compact ? "item-card-compact" : "item-card";

  return (
    <div
      className={`${cardClass} ${getRarityClass()} ${getQualityClass()} ${
        isSelected ? "item-card-selected" : ""
      }`}
      onClick={onClick}
      title={item.name}
    >
      {/* Item Name (hidden in compact mode) */}
      {compact && (
        <div className="item-card-name">{truncateName(item.name)}</div>
      )}
      {/* Item Icon
      <div className={compact ? "item-icon-compact" : "item-card-icon"}>
        {item.type}
      </div> */}

      {/* Item Level Badge (for equipment only) */}
      {item.itemType === "equipment" &&
        item.level !== undefined &&
        item.level > 0 && (
          <div className={compact ? "item-level-compact" : "item-card-level"}>
            {compact ? item.level : `Lv${item.level}`}
          </div>
        )}

      {/* Stack Count Badge (for stackable consumables) */}
      {item.stackable &&
        item.stackCount !== undefined &&
        item.stackCount > 1 && (
          <div className={compact ? "item-stack-compact" : "item-card-stack"}>
            {compact ? item.stackCount : `x${item.stackCount}`}
          </div>
        )}
    </div>
  );
};

export default ItemCard;
