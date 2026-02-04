import { useState, useCallback } from 'react';
import type { Item } from '@/types/itemTypes';
import type { BuffDebuffMap } from '@/types/battleTypes';
import type { PlayerData } from '@/types/characterTypes';
import { executeItemEffect } from '@/domain/battles/logic/itemEffectExecutor';
import {
  applyBuffsToMap,
  clearDebuffsFromMap,
} from '@/domain/battles/logic/buffLogic';
import { logger } from '@/utils/logger';

interface UseItemUsageParams {
  playerHp: number;
  playerMaxHp: number;
  playerBuffs: BuffDebuffMap;
  playerEnergy: number;
  maxEnergy: number;
  playerData: PlayerData;
  updateRuntimeState: (updates: { currentHp?: number }) => void;
  updatePlayerData: (updates: Partial<PlayerData>) => void;
  setPlayerBuffs: React.Dispatch<React.SetStateAction<BuffDebuffMap>>;
}

interface UseItemUsageReturn {
  itemUsedThisPhase: boolean;
  resetItemUsage: () => void;
  handleUseItem: (item: Item) => void;
}

/**
 * Hook for managing item usage in battle
 */
export function useItemUsage({
  playerHp,
  playerMaxHp,
  playerBuffs,
  playerEnergy,
  maxEnergy,
  playerData,
  updateRuntimeState,
  updatePlayerData,
  setPlayerBuffs,
}: UseItemUsageParams): UseItemUsageReturn {
  const [itemUsedThisPhase, setItemUsedThisPhase] = useState(false);

  const resetItemUsage = useCallback(() => {
    setItemUsedThisPhase(false);
  }, []);

  const handleUseItem = useCallback(
    (item: Item) => {
      // Prevent using multiple items per phase
      if (itemUsedThisPhase) {
        logger.debug('Item already used this phase');
        return;
      }

      // Execute item effect
      const result = executeItemEffect(
        item,
        playerHp,
        playerMaxHp,
        playerBuffs,
        playerEnergy,
        maxEnergy,
      );

      if (!result.success) {
        logger.debug(`Item use failed: ${result.message}`);
        return;
      }

      // Apply effects to player state
      if (result.hpChange && result.hpChange > 0) {
        const newHp = Math.min(playerHp + result.hpChange, playerMaxHp);
        updateRuntimeState({ currentHp: newHp });
      }

      // Apply buffs if any
      if (result.buffsApplied && result.buffsApplied.length > 0) {
        setPlayerBuffs((prev) => applyBuffsToMap(prev, result.buffsApplied!));
      }

      // Clear debuffs if requested
      if (result.debuffsCleared) {
        setPlayerBuffs((prev) => clearDebuffsFromMap(prev));
      }

      // Remove or decrement item from inventory
      const itemIndex = playerData.inventory.inventory.items.findIndex(
        (i) => i.id === item.id,
      );

      if (itemIndex !== -1) {
        const items = [...playerData.inventory.inventory.items];
        const targetItem = items[itemIndex];

        if (
          targetItem.stackable &&
          targetItem.stackCount &&
          targetItem.stackCount > 1
        ) {
          // Decrement stack count
          items[itemIndex] = {
            ...targetItem,
            stackCount: targetItem.stackCount - 1,
          };
        } else {
          // Remove item entirely
          items.splice(itemIndex, 1);
        }

        // Update player inventory
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

      // Mark item as used this phase
      setItemUsedThisPhase(true);

      logger.debug(`Used item: ${item.name} - ${result.message}`);
    },
    [
      itemUsedThisPhase,
      playerHp,
      playerMaxHp,
      playerBuffs,
      playerEnergy,
      maxEnergy,
      playerData.inventory,
      updateRuntimeState,
      updatePlayerData,
      setPlayerBuffs,
    ],
  );

  return {
    itemUsedThisPhase,
    resetItemUsage,
    handleUseItem,
  };
}
