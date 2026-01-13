// PlayerContext: Manages player state including stats, resources, and progression

import React, {
  createContext,
  useContext,
  useState,
  type ReactNode,
} from "react";
import type { Player } from "../../characters/type/playerTypes";
import type { MagicStones } from "../types/ItemTypes";
import { Swordman_Status } from "../../characters/player/data/PlayerData";
import {
  STORAGE_TEST_ITEMS,
  INVENTORY_TEST_ITEMS,
  EQUIPPED_TEST_ITEMS,
} from "../data/TestItemsData";
import type { ExtendedPlayer } from "../../characters/type/playerTypes";
/**
 * PlayerContext value
 */
interface PlayerContextValue {
  player: ExtendedPlayer;
  updatePlayer: (updates: Partial<ExtendedPlayer>) => void;
  updateClassGrade: (newGrade: string) => void;
  addGold: (amount: number, toBaseCamp?: boolean) => void;
  useGold: (amount: number) => boolean;
  updateHp: (newHp: number) => void;
  updateAp: (newAp: number) => void;
  addMagicStones: (stones: Partial<MagicStones>, toBaseCamp?: boolean) => void;
  useMagicStones: (value: number) => boolean;
  addSouls: (amount: number) => void;
  transferExplorationResources: (survivalMultiplier: number) => void;
  resetExplorationResources: () => void;
}

const PlayerContext = createContext<PlayerContextValue | undefined>(undefined);

/**
 * Create initial extended player from base player data
 */
function createInitialPlayer(basePlayer: Player): ExtendedPlayer {
  return {
    ...basePlayer,
    // Storage & Inventory (with test items)
    storage: {
      items: STORAGE_TEST_ITEMS, // Test items for Phase 3
      maxCapacity: 100,
      currentCapacity: STORAGE_TEST_ITEMS.length,
    },
    inventory: {
      items: INVENTORY_TEST_ITEMS, // Test items for Phase 3
      maxCapacity: 20,
      currentCapacity: INVENTORY_TEST_ITEMS.length,
    },
    equipmentSlots: EQUIPPED_TEST_ITEMS, // Test equipped items for Phase 3

    // Resources (with test values)
    explorationGold: 0,
    baseCampGold: 1250, // Test value for UI display
    explorationMagicStones: { small: 0, medium: 0, large: 0 },
    baseCampMagicStones: { small: 5, medium: 3, large: 1 }, // Test values (450 total)

    // Progression
    explorationLimit: {
      current: 0,
      max: 10,
    },
    sanctuaryProgress: {
      currentRunSouls: 0,
      totalSouls: 0,
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
  // Initialize with Swordsman by default
  const [player, setPlayer] = useState<ExtendedPlayer>(
    createInitialPlayer(Swordman_Status) //Selected character by PlayUser
  );

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
   * Add gold to player
   * @param amount - Amount of gold to add
   * @param toBaseCamp - If true, add to baseCampGold; otherwise to explorationGold
   */
  const addGold = (amount: number, toBaseCamp = false) => {
    setPlayer((prev) => {
      if (toBaseCamp) {
        return {
          ...prev,
          baseCampGold: prev.baseCampGold + amount,
          gold: prev.gold + amount,
        };
      } else {
        return {
          ...prev,
          explorationGold: prev.explorationGold + amount,
          gold: prev.gold + amount,
        };
      }
    });
  };

  /**
   * Use gold (deduct from total)
   * @returns true if successful, false if insufficient gold
   */
  const useGold = (amount: number): boolean => {
    if (player.gold < amount) return false;

    setPlayer((prev) => {
      const newGold = prev.gold - amount;
      // Deduct from baseCamp first, then exploration
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
        gold: newGold,
        baseCampGold: Math.max(0, newBaseCampGold),
        explorationGold: Math.max(0, newExplorationGold),
      };
    });
    return true;
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
   * Add magic stones
   */
  const addMagicStones = (stones: Partial<MagicStones>, toBaseCamp = false) => {
    setPlayer((prev) => {
      if (toBaseCamp) {
        return {
          ...prev,
          baseCampMagicStones: {
            small: prev.baseCampMagicStones.small + (stones.small || 0),
            medium: prev.baseCampMagicStones.medium + (stones.medium || 0),
            large: prev.baseCampMagicStones.large + (stones.large || 0),
          },
        };
      } else {
        return {
          ...prev,
          explorationMagicStones: {
            small: prev.explorationMagicStones.small + (stones.small || 0),
            medium: prev.explorationMagicStones.medium + (stones.medium || 0),
            large: prev.explorationMagicStones.large + (stones.large || 0),
          },
        };
      }
    });
  };

  /**
   * Use magic stones (deduct from total value)
   * @returns true if successful, false if insufficient
   */
  const useMagicStones = (value: number): boolean => {
    const baseCampValue =
      player.baseCampMagicStones.small * 30 +
      player.baseCampMagicStones.medium * 100 +
      player.baseCampMagicStones.large * 350;

    const explorationValue =
      player.explorationMagicStones.small * 30 +
      player.explorationMagicStones.medium * 100 +
      player.explorationMagicStones.large * 350;

    const totalValue = baseCampValue + explorationValue;
    if (totalValue < value) return false;

    // Deduct from baseCamp first, then exploration
    // TODO: Implement proper stone deduction logic
    // For now, just check if enough value exists
    return true;
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
   * Transfer exploration resources to BaseCamp (on survival)
   * @param survivalMultiplier - Multiplier based on survival method (0.6-1.0)
   */
  const transferExplorationResources = (survivalMultiplier: number) => {
    setPlayer((prev) => {
      const transferredGold = Math.floor(
        prev.explorationGold * survivalMultiplier
      );
      const transferredSouls = Math.floor(
        prev.sanctuaryProgress.currentRunSouls * survivalMultiplier
      );

      return {
        ...prev,
        baseCampGold: prev.baseCampGold + transferredGold,
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
        },
        explorationMagicStones: { small: 0, medium: 0, large: 0 },
        sanctuaryProgress: {
          ...prev.sanctuaryProgress,
          totalSouls: prev.sanctuaryProgress.totalSouls + transferredSouls,
          currentRunSouls: 0,
        },
      };
    });
  };

  /**
   * Reset exploration resources (on death)
   */
  const resetExplorationResources = () => {
    setPlayer((prev) => ({
      ...prev,
      explorationGold: 0,
      gold: prev.baseCampGold,
      explorationMagicStones: { small: 0, medium: 0, large: 0 },
      sanctuaryProgress: {
        ...prev.sanctuaryProgress,
        currentRunSouls: 0,
      },
    }));
  };

  return (
    <PlayerContext.Provider
      value={{
        player,
        updatePlayer,
        updateClassGrade,
        addGold,
        useGold,
        updateHp,
        updateAp,
        addMagicStones,
        useMagicStones,
        addSouls,
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
