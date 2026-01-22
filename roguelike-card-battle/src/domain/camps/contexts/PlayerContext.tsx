// PlayerContext: Manages player state including stats, storage, inventory, and progression
// Resource management (gold, magic stones) has been moved to ResourceContext

import React, {
  createContext,
  useContext,
  useState,
  type ReactNode,
} from "react";
import type { Player } from "../../characters/type/playerTypes";
import type { MagicStones } from "../../item_equipment/type/ItemTypes";
import { Swordman_Status } from "../../characters/player/data/PlayerData";
import {
  STORAGE_TEST_ITEMS,
  INVENTORY_TEST_ITEMS,
  EQUIPPED_TEST_ITEMS,
} from "../../item_equipment/data/TestItemsData";
import type { ExtendedPlayer } from "../../characters/type/playerTypes";
import { useResources } from "./ResourceContext";

/**
 * PlayerContext value
 * Note: Resource-related functions delegate to ResourceContext for actual implementation
 */
interface PlayerContextValue {
  player: ExtendedPlayer;
  updatePlayer: (updates: Partial<ExtendedPlayer>) => void;
  updateClassGrade: (newGrade: string) => void;
  updateHp: (newHp: number) => void;
  updateAp: (newAp: number) => void;
  addSouls: (amount: number) => void;
  transferSouls: (survivalMultiplier: number) => void;
  resetCurrentRunSouls: () => void;

  // Resource operations (delegated to ResourceContext for backward compatibility)
  addGold: (amount: number, toBaseCamp?: boolean) => void;
  useGold: (amount: number) => boolean;
  addMagicStones: (stones: Partial<MagicStones>, toBaseCamp?: boolean) => void;
  useMagicStones: (value: number) => boolean;
  transferExplorationResources: (survivalMultiplier: number) => void;
  resetExplorationResources: () => void;
}

const PlayerContext = createContext<PlayerContextValue | undefined>(undefined);

/**
 * Create initial extended player from base player data
 * Note: Gold and magic stone values come from ResourceContext
 */
function createInitialPlayer(basePlayer: Player): ExtendedPlayer {
  return {
    ...basePlayer,
    // Gold will be synced from ResourceContext
    gold: 0,

    // Storage & Inventory (with test items)
    storage: {
      items: STORAGE_TEST_ITEMS,
      maxCapacity: 100,
      currentCapacity: STORAGE_TEST_ITEMS.length,
    },
    inventory: {
      items: INVENTORY_TEST_ITEMS,
      maxCapacity: 20,
      currentCapacity: INVENTORY_TEST_ITEMS.length,
    },
    equipmentInventory: {
      items: [],
      maxCapacity: 3,
      currentCapacity: 0,
    },
    equipmentSlots: EQUIPPED_TEST_ITEMS,

    // Resources (managed by ResourceContext, kept here for compatibility)
    explorationGold: 0,
    baseCampGold: 0,
    explorationMagicStones: { small: 0, medium: 0, large: 0, huge: 0 },
    baseCampMagicStones: { small: 0, medium: 0, large: 0, huge: 0 },

    // Progression
    explorationLimit: {
      current: 0,
      max: 10,
    },
    sanctuaryProgress: {
      currentRunSouls: 25,
      totalSouls: 150,
      unlockedNodes: [],
      explorationLimitBonus: 0,
    },
  };
}

/**
 * PlayerProvider Component
 */
export const PlayerProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  // Get resource context for delegation
  const resourceContext = useResources();

  // Initialize with Swordsman by default
  const [player, setPlayer] = useState<ExtendedPlayer>(() => {
    const initialPlayer = createInitialPlayer(Swordman_Status);
    // Sync initial values from ResourceContext
    return {
      ...initialPlayer,
      gold: resourceContext.getTotalGold(),
      baseCampGold: resourceContext.resources.gold.baseCamp,
      explorationGold: resourceContext.resources.gold.exploration,
      baseCampMagicStones: resourceContext.resources.magicStones.baseCamp,
      explorationMagicStones: resourceContext.resources.magicStones.exploration,
      explorationLimit: resourceContext.resources.explorationLimit,
    };
  });

  /**
   * Update player with partial updates
   */
  const updatePlayer = (updates: Partial<ExtendedPlayer>) => {
    setPlayer((prev) => ({ ...prev, ...updates }));
  };

  /**
   * Update class grade (for promotion system)
   */
  const updateClassGrade = (newGrade: string) => {
    setPlayer((prev) => ({ ...prev, classGrade: newGrade }));
  };

  /**
   * Update HP (with bounds checking)
   */
  const updateHp = (newHp: number) => {
    setPlayer((prev) => ({
      ...prev,
      hp: Math.max(0, Math.min(newHp, prev.maxHp)),
    }));
  };

  /**
   * Update AP (with bounds checking)
   */
  const updateAp = (newAp: number) => {
    setPlayer((prev) => ({
      ...prev,
      ap: Math.max(0, Math.min(newAp, prev.maxAp)),
    }));
  };

  /**
   * Add souls (current run)
   */
  const addSouls = (amount: number) => {
    setPlayer((prev) => ({
      ...prev,
      sanctuaryProgress: {
        ...prev.sanctuaryProgress,
        currentRunSouls: prev.sanctuaryProgress.currentRunSouls + amount,
      },
    }));
  };

  /**
   * Transfer souls from current run to total (on survival)
   */
  const transferSouls = (survivalMultiplier: number) => {
    setPlayer((prev) => {
      const transferredSouls = Math.floor(
        prev.sanctuaryProgress.currentRunSouls * survivalMultiplier
      );
      return {
        ...prev,
        sanctuaryProgress: {
          ...prev.sanctuaryProgress,
          totalSouls: prev.sanctuaryProgress.totalSouls + transferredSouls,
          currentRunSouls: 0,
        },
      };
    });
  };

  /**
   * Reset current run souls (on death)
   */
  const resetCurrentRunSouls = () => {
    setPlayer((prev) => ({
      ...prev,
      sanctuaryProgress: {
        ...prev.sanctuaryProgress,
        currentRunSouls: 0,
      },
    }));
  };

  // ============================================================
  // Resource operations (delegated to ResourceContext)
  // These are kept for backward compatibility with existing code
  // ============================================================

  /**
   * Add gold (delegated to ResourceContext)
   */
  const addGold = (amount: number, toBaseCamp = false) => {
    resourceContext.addGold(amount, toBaseCamp);
    // Sync player state
    setPlayer((prev) => ({
      ...prev,
      gold: resourceContext.getTotalGold() + amount,
      baseCampGold: toBaseCamp
        ? prev.baseCampGold + amount
        : prev.baseCampGold,
      explorationGold: toBaseCamp
        ? prev.explorationGold
        : prev.explorationGold + amount,
    }));
  };

  /**
   * Use gold (delegated to ResourceContext)
   */
  const useGold = (amount: number): boolean => {
    const success = resourceContext.useGold(amount);
    if (success) {
      // Sync player state - recalculate from ResourceContext
      setPlayer((prev) => {
        let newBaseCampGold = prev.baseCampGold;
        let newExplorationGold = prev.explorationGold;

        if (newBaseCampGold >= amount) {
          newBaseCampGold -= amount;
        } else {
          const remaining = amount - newBaseCampGold;
          newBaseCampGold = 0;
          newExplorationGold -= remaining;
        }

        return {
          ...prev,
          gold: prev.gold - amount,
          baseCampGold: Math.max(0, newBaseCampGold),
          explorationGold: Math.max(0, newExplorationGold),
        };
      });
    }
    return success;
  };

  /**
   * Add magic stones (delegated to ResourceContext)
   */
  const addMagicStones = (stones: Partial<MagicStones>, toBaseCamp = false) => {
    resourceContext.addMagicStones(stones, toBaseCamp);
    // Sync player state
    setPlayer((prev) => {
      if (toBaseCamp) {
        return {
          ...prev,
          baseCampMagicStones: {
            small: prev.baseCampMagicStones.small + (stones.small || 0),
            medium: prev.baseCampMagicStones.medium + (stones.medium || 0),
            large: prev.baseCampMagicStones.large + (stones.large || 0),
            huge: prev.baseCampMagicStones.huge + (stones.huge || 0),
          },
        };
      } else {
        return {
          ...prev,
          explorationMagicStones: {
            small: prev.explorationMagicStones.small + (stones.small || 0),
            medium: prev.explorationMagicStones.medium + (stones.medium || 0),
            large: prev.explorationMagicStones.large + (stones.large || 0),
            huge: prev.explorationMagicStones.huge + (stones.huge || 0),
          },
        };
      }
    });
  };

  /**
   * Use magic stones (delegated to ResourceContext)
   */
  const useMagicStones = (value: number): boolean => {
    return resourceContext.useMagicStones(value);
  };

  /**
   * Transfer exploration resources to BaseCamp (delegated to ResourceContext)
   */
  const transferExplorationResources = (survivalMultiplier: number) => {
    resourceContext.transferExplorationToBaseCamp(survivalMultiplier);
    // Also transfer souls
    transferSouls(survivalMultiplier);
    // Sync player state
    setPlayer((prev) => ({
      ...prev,
      baseCampGold:
        prev.baseCampGold +
        Math.floor(prev.explorationGold * survivalMultiplier),
      explorationGold: 0,
      baseCampMagicStones: {
        small:
          prev.baseCampMagicStones.small +
          Math.floor(prev.explorationMagicStones.small * survivalMultiplier),
        medium:
          prev.baseCampMagicStones.medium +
          Math.floor(prev.explorationMagicStones.medium * survivalMultiplier),
        large:
          prev.baseCampMagicStones.large +
          Math.floor(prev.explorationMagicStones.large * survivalMultiplier),
        huge:
          prev.baseCampMagicStones.huge +
          Math.floor(prev.explorationMagicStones.huge * survivalMultiplier),
      },
      explorationMagicStones: { small: 0, medium: 0, large: 0, huge: 0 },
      gold:
        prev.baseCampGold +
        Math.floor(prev.explorationGold * survivalMultiplier),
    }));
  };

  /**
   * Reset exploration resources (delegated to ResourceContext)
   */
  const resetExplorationResources = () => {
    resourceContext.resetExplorationResources();
    // Also reset current run souls
    resetCurrentRunSouls();
    // Sync player state
    setPlayer((prev) => ({
      ...prev,
      explorationGold: 0,
      gold: prev.baseCampGold,
      explorationMagicStones: { small: 0, medium: 0, large: 0, huge: 0 },
    }));
  };

  return (
    <PlayerContext.Provider
      value={{
        player,
        updatePlayer,
        updateClassGrade,
        updateHp,
        updateAp,
        addSouls,
        transferSouls,
        resetCurrentRunSouls,
        // Resource operations (delegated)
        addGold,
        useGold,
        addMagicStones,
        useMagicStones,
        transferExplorationResources,
        resetExplorationResources,
      }}
    >
      {children}
    </PlayerContext.Provider>
  );
};

/**
 * Hook to use Player context
 */
export const usePlayer = () => {
  const context = useContext(PlayerContext);
  if (!context) {
    throw new Error("usePlayer must be used within PlayerProvider");
  }
  return context;
};
