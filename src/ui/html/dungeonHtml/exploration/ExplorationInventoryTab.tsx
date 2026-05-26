// ExplorationInventoryTab - Item display and usage during dungeon exploration

import { useState } from "react";
import { usePlayer } from "@/contexts/PlayerContext";
import { useExplorationItemUsage } from "@/domain/dungeon/hooks/useExplorationItemUsage";
import type { Item } from "@/types/itemTypes";

export function ExplorationInventoryTab() {
  const { playerData } = usePlayer();
  const { canUseOnMap, useItem: applyItem } = useExplorationItemUsage();
  const [confirmItem, setConfirmItem] = useState<Item | null>(null);

  const items = playerData.inventory.inventory.items;

  const handleUseConfirm = () => {
    if (confirmItem) {
      applyItem(confirmItem);
      setConfirmItem(null);
    }
  };

  if (items.length === 0) {
    return (
      <div className="exploration-inventory-tab">
        <div className="exploration-inventory-empty">
          アイテムがありません
        </div>
      </div>
    );
  }

  return (
    <div className="exploration-inventory-tab">
      <div className="exploration-inventory-grid">
        {items.map((item) => {
          const usable = canUseOnMap(item);
          const isBattleOnly = item.usableContext === "battle";

          return (
            <div key={item.id} className="exploration-item-card">
              <div className="exploration-item-header">
                <span className="exploration-item-name">{item.name}</span>
                {item.stackable && item.stackCount && item.stackCount > 1 && (
                  <span className="exploration-item-stack">
                    x{item.stackCount}
                  </span>
                )}
              </div>
              <div className="exploration-item-desc">{item.description}</div>
              <div className="exploration-item-actions">
                {usable ? (
                  <button
                    className="exploration-item-use-btn"
                    onClick={() => setConfirmItem(item)}
                  >
                    使う
                  </button>
                ) : isBattleOnly ? (
                  <span className="exploration-item-battle-only" title="戦闘中のみ使用可">
                    戦闘中のみ
                  </span>
                ) : null}
              </div>
            </div>
          );
        })}
      </div>

      {/* Use Confirm */}
      {confirmItem && (
        <div
          className="exploration-item-confirm-overlay"
          onClick={() => setConfirmItem(null)}
        >
          <div
            className="exploration-item-confirm-modal"
            onClick={(e) => e.stopPropagation()}
          >
            <p className="exploration-item-confirm-text">
              {confirmItem.name}を使用しますか？
            </p>
            <div className="exploration-item-confirm-actions">
              <button
                className="exploration-item-confirm-yes"
                onClick={handleUseConfirm}
              >
                使用
              </button>
              <button
                className="exploration-item-confirm-no"
                onClick={() => setConfirmItem(null)}
              >
                キャンセル
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
