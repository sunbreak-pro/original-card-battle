// EquipmentTab - Read-only display of equipped items

import { useState, useMemo } from "react";
import type { EquipmentSlots } from "@/types/campTypes";
import type { Item, EquipmentSlot } from "@/types/itemTypes";
import { calculateEquipmentAP } from "@/domain/item_equipment/logic/equipmentStats";

interface EquipmentTabProps {
  equipmentSlots: EquipmentSlots;
  onNavigateToStorage: () => void;
}

const SLOT_LABELS: Record<EquipmentSlot, string> = {
  weapon: "武器",
  armor: "防具",
  helmet: "兜",
  boots: "靴",
  accessory1: "装飾品1",
  accessory2: "装飾品2",
};

const RARITY_COLORS: Record<string, string> = {
  common: "#a0a0a0",
  uncommon: "#4ade80",
  rare: "#60a5fa",
  epic: "#c084fc",
  legendary: "#fbbf24",
};

export function EquipmentTab({
  equipmentSlots,
  onNavigateToStorage,
}: EquipmentTabProps) {
  const [tooltipItem, setTooltipItem] = useState<Item | null>(null);

  const equipmentAP = useMemo(
    () => calculateEquipmentAP(equipmentSlots),
    [equipmentSlots],
  );

  const slots = Object.entries(SLOT_LABELS) as [EquipmentSlot, string][];

  return (
    <div className="equipment-tab">
      <div className="equipment-ap-summary">
        <span className="ap-label">装備AP:</span>
        <span className="ap-value">{equipmentAP.totalAP}/{equipmentAP.maxAP}</span>
      </div>
      <div className="equipment-slots-list">
        {slots.map(([slotKey, slotLabel]) => {
          const item = equipmentSlots[slotKey];
          return (
            <div
              key={slotKey}
              className={`equipment-slot-row ${item ? "equipped" : "empty"}`}
              onMouseEnter={() => item && setTooltipItem(item)}
              onMouseLeave={() => setTooltipItem(null)}
            >
              <span className="slot-label">{slotLabel}</span>
              <span
                className="slot-item-name"
                style={
                  item
                    ? { color: RARITY_COLORS[item.rarity] || "#a0a0a0" }
                    : undefined
                }
              >
                {item ? item.name : "---"}
              </span>
              {slotKey !== "weapon" && equipmentAP.perSlot[slotKey] !== undefined && (
                <span className="slot-ap-badge">
                  AP +{equipmentAP.perSlot[slotKey]}/{equipmentAP.perSlotMax[slotKey]}
                </span>
              )}
            </div>
          );
        })}
      </div>

      {tooltipItem && (
        <div className="equipment-tooltip">
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
