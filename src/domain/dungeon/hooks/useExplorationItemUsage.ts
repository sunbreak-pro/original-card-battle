// useExplorationItemUsage - Item usage logic for map/exploration context

import { useCallback } from "react";
import type { Item } from "@/types/itemTypes";
import { getConsumableData } from "@/constants/data/items/ConsumableItemData";
import { usePlayer } from "@/contexts/PlayerContext";
import { useToast } from "@/contexts/ToastContext";
import { logger } from "@/utils/logger";

/**
 * Hook for using items during dungeon exploration (map context).
 * Supports heal and fullHeal effects from ConsumableItemData.
 */
export function useExplorationItemUsage() {
  const { playerData, runtimeState, updateHp, updatePlayerData } = usePlayer();
  const { addToast } = useToast();

  const canUseOnMap = useCallback((item: Item): boolean => {
    return (
      item.usableContext === "map" ||
      item.usableContext === "anywhere"
    );
  }, []);

  const useItem = useCallback(
    (item: Item) => {
      if (!canUseOnMap(item)) {
        logger.debug(`Item ${item.name} cannot be used on map`);
        return;
      }

      // Look up consumable data for proper effects
      const consumableData = getConsumableData(item.typeId);
      if (!consumableData || consumableData.effects.length === 0) {
        logger.debug(`Item ${item.name} has no consumable effects`);
        return;
      }

      const maxHp = playerData.persistent.baseMaxHp;
      const currentHp = runtimeState.currentHp;
      let healAmount = 0;

      // Apply supported map effects
      for (const effect of consumableData.effects) {
        if (effect.type === "heal" && effect.value) {
          healAmount += effect.value;
        } else if (effect.type === "fullHeal") {
          healAmount = maxHp - currentHp;
        }
      }

      if (healAmount > 0) {
        const newHp = Math.min(currentHp + healAmount, maxHp);
        updateHp(newHp);
      }

      // Consume item from inventory
      const items = [...playerData.inventory.inventory.items];
      const itemIndex = items.findIndex((i) => i.id === item.id);

      if (itemIndex !== -1) {
        const targetItem = items[itemIndex];
        if (
          targetItem.stackable &&
          targetItem.stackCount &&
          targetItem.stackCount > 1
        ) {
          items[itemIndex] = {
            ...targetItem,
            stackCount: targetItem.stackCount - 1,
          };
        } else {
          items.splice(itemIndex, 1);
        }

        updatePlayerData({
          inventory: {
            ...playerData.inventory,
            inventory: {
              ...playerData.inventory.inventory,
              items,
              currentCapacity: items.length,
            },
          },
        });
      }

      // Show toast
      addToast({
        type: "success",
        title: `${item.name}を使用`,
        message: healAmount > 0 ? `HP +${healAmount}` : "効果を適用しました",
        duration: 2000,
      });

      logger.debug(`Used item on map: ${item.name}`);
    },
    [
      canUseOnMap,
      playerData.persistent.baseMaxHp,
      playerData.inventory,
      runtimeState.currentHp,
      updateHp,
      updatePlayerData,
      addToast,
    ],
  );

  return { canUseOnMap, useItem };
}
