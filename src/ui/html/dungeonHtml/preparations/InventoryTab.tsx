// InventoryTab - Read-only display of inventory items

import { useState } from "react";
import type { InventoryState } from "@/types/campTypes";
import type { Item } from "@/types/itemTypes";

interface InventoryTabProps {
  inventory: InventoryState;
  onNavigateToStorage: () => void;
}

const RARITY_COLORS: Record<string, string> = {
  common: "#a0a0a0",
  uncommon: "#4ade80",
  rare: "#60a5fa",
  epic: "#c084fc",
  legendary: "#fbbf24",
};

export function InventoryTab({
  inventory,
  onNavigateToStorage,
}: InventoryTabProps) {
  const [tooltipItem, setTooltipItem] = useState<Item | null>(null);

  return (
    <div className="inventory-tab">
      <div className="inventory-capacity">
        {inventory.currentCapacity} / {inventory.maxCapacity}
      </div>

      <div className="inventory-grid">
        {inventory.items.length === 0 ? (
          <div className="inventory-empty">持ち物はありません</div>
        ) : (
          inventory.items.map((item) => (
            <div
              key={item.id}
              className="inventory-item-card"
              onMouseEnter={() => setTooltipItem(item)}
              onMouseLeave={() => setTooltipItem(null)}
              style={{
                borderColor: RARITY_COLORS[item.rarity] || "#a0a0a0",
              }}
            >
              <div className="inv-item-name">{item.name}</div>
              {item.stackable && item.stackCount && item.stackCount > 1 && (
                <div className="inv-item-stack">x{item.stackCount}</div>
              )}
            </div>
          ))
        )}
      </div>

      {tooltipItem && (
        <div className="inventory-tooltip">
          <div className="tooltip-name">{tooltipItem.name}</div>
          <div className="tooltip-rarity">{tooltipItem.rarity}</div>
          <div className="tooltip-desc">{tooltipItem.description}</div>
        </div>
      )}

      <button
        className="navigate-storage-btn"
        onClick={onNavigateToStorage}
      >
        倉庫へ移動
      </button>
    </div>
  );
}
