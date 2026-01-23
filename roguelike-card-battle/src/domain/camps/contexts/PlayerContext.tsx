// PlayerContext: Manages player state including stats, storage, inventory, and progression
// Resource management (gold, magic stones) has been moved to ResourceContext
//
// NEW ARCHITECTURE: Internally uses PlayerData, exposes both PlayerData and legacy ExtendedPlayer

import React, {
  createContext,
  useContext,
  useState,
  useMemo,
  type ReactNode,
} from "react";
import type {
  Player,
  ExtendedPlayer,
  PlayerData,
  Difficulty,
  LivesSystem,
} from "../../characters/type/playerTypes";
import {
  createLivesSystem,
  decreaseLives as decreaseLivesHelper,
  isGameOver as isGameOverHelper,
} from "../../characters/type/playerTypes";
import type { CharacterClass } from "../../characters/type/baseTypes";
import type { MagicStones } from "../../item_equipment/type/ItemTypes";
import {
  Swordman_Status,
  Mage_Status,
  Summon_Status,
} from "../../characters/player/data/PlayerData";
import { getCharacterClassInfo } from "../../characters/player/data/CharacterClassData";
import {
  STORAGE_TEST_ITEMS,
  INVENTORY_TEST_ITEMS,
  EQUIPPED_TEST_ITEMS,
} from "../../item_equipment/data/TestItemsData";
import { useResources } from "./ResourceContext";

/**
 * Runtime Battle State
 * Persisted between battles within a single exploration run.
 * Reset when starting a new exploration.
 */
export interface RuntimeBattleState {
  /** Current HP (persists between battles) */
  currentHp: number;
  /** Current AP (persists between battles) */
  currentAp: number;
  /** Card mastery store - cardTypeId -> useCount */
  cardMasteryStore: Map<string, number>;
  /** Lives system (remaining lives) */
  lives: LivesSystem;
  /** Current game difficulty */
  difficulty: Difficulty;
}

/**
 * PlayerContext value
 *
 * Uses PlayerData as the primary interface.
 * Resource-related functions delegate to ResourceContext for actual implementation.
 */
interface PlayerContextValue {
  // ============================================================
  // PlayerData-based interface
  // ============================================================

  /** Player data */
  playerData: PlayerData;

  /** Update player data with partial updates */
  updatePlayerData: (updates: Partial<PlayerData>) => void;

  // ============================================================
  // Runtime battle state (persists between battles)
  // ============================================================

  /** Runtime state that persists between battles */
  runtimeState: RuntimeBattleState;

  /** Update runtime state after battle */
  updateRuntimeState: (updates: Partial<RuntimeBattleState>) => void;

  /** Update card mastery for a card type */
  updateCardMastery: (cardTypeId: string, useCount: number) => void;

  /** Reset runtime state for new exploration */
  resetRuntimeState: () => void;

  // ============================================================
  // Lives System
  // ============================================================

  /** Decrease lives by 1 (on death) */
  decreaseLives: () => void;

  /** Check if game is over (no lives remaining) */
  isGameOver: () => boolean;

  /** Set game difficulty (affects max lives) */
  setDifficulty: (difficulty: Difficulty) => void;

  // ============================================================
  // Common operations
  // ============================================================

  updateClassGrade: (newGrade: string) => void;
  updateHp: (newHp: number) => void;
  updateAp: (newAp: number) => void;
  addSouls: (amount: number) => void;
  transferSouls: (survivalMultiplier: number) => void;
  resetCurrentRunSouls: () => void;
  initializeWithClass: (classType: CharacterClass) => void;

  // Resource operations (delegated to ResourceContext)
  addGold: (amount: number, toBaseCamp?: boolean) => void;
  useGold: (amount: number) => boolean;
  addMagicStones: (stones: Partial<MagicStones>, toBaseCamp?: boolean) => void;
  useMagicStones: (value: number) => boolean;
  transferExplorationResources: (survivalMultiplier: number) => void;
  resetExplorationResources: () => void;
}

const PlayerContext = createContext<PlayerContextValue | undefined>(undefined);

/**
 * Get base player data by character class
 */
function getBasePlayerByClass(classType: CharacterClass): Player {
  switch (classType) {
    case "swordsman":
      return Swordman_Status;
    case "mage":
      return Mage_Status;
    case "summoner":
      return Summon_Status;
    default:
      return Swordman_Status;
  }
}

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
 * Create initial runtime battle state
 */
function createInitialRuntimeState(
  basePlayer: Player,
  difficulty: Difficulty = 'normal'
): RuntimeBattleState {
  return {
    currentHp: basePlayer.hp,
    currentAp: basePlayer.ap,
    cardMasteryStore: new Map(),
    lives: createLivesSystem(difficulty),
    difficulty,
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

  // Internal state using ExtendedPlayer for backward compatibility
  // This is converted to PlayerData for external use
  const [playerState, setPlayerState] = useState<ExtendedPlayer>(() => {
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

  // Runtime battle state - persists between battles within exploration
  const [runtimeState, setRuntimeState] = useState<RuntimeBattleState>(() =>
    createInitialRuntimeState(Swordman_Status)
  );

  /**
   * Update class grade (for promotion system)
   */
  const updateClassGrade = (newGrade: string) => {
    setPlayerState((prev) => ({ ...prev, classGrade: newGrade }));
  };

  /**
   * Update HP (with bounds checking)
   */
  const updateHp = (newHp: number) => {
    setPlayerState((prev) => ({
      ...prev,
      hp: Math.max(0, Math.min(newHp, prev.maxHp)),
    }));
  };

  /**
   * Update AP (with bounds checking)
   */
  const updateAp = (newAp: number) => {
    setPlayerState((prev) => ({
      ...prev,
      ap: Math.max(0, Math.min(newAp, prev.maxAp)),
    }));
  };

  /**
   * Add souls (current run)
   */
  const addSouls = (amount: number) => {
    setPlayerState((prev) => ({
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
    setPlayerState((prev) => {
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
    setPlayerState((prev) => ({
      ...prev,
      sanctuaryProgress: {
        ...prev.sanctuaryProgress,
        currentRunSouls: 0,
      },
    }));
  };

  /**
   * Initialize player with a specific character class
   * Used when starting a new game from character selection
   */
  const initializeWithClass = (classType: CharacterClass) => {
    const basePlayer = getBasePlayerByClass(classType);
    const classInfo = getCharacterClassInfo(classType);

    // Create player with starter deck from class data
    const playerWithStarterDeck: Player = {
      ...basePlayer,
      deck: classInfo.starterDeck,
    };

    const newPlayer = createInitialPlayer(playerWithStarterDeck);

    // Sync with ResourceContext
    setPlayerState({
      ...newPlayer,
      gold: resourceContext.getTotalGold(),
      baseCampGold: resourceContext.resources.gold.baseCamp,
      explorationGold: resourceContext.resources.gold.exploration,
      baseCampMagicStones: resourceContext.resources.magicStones.baseCamp,
      explorationMagicStones: resourceContext.resources.magicStones.exploration,
      explorationLimit: resourceContext.resources.explorationLimit,
      // Reset souls for new game
      sanctuaryProgress: {
        currentRunSouls: 0,
        totalSouls: 0,
        unlockedNodes: [],
        explorationLimitBonus: 0,
      },
    });
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
    setPlayerState((prev) => ({
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
      setPlayerState((prev) => {
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
    setPlayerState((prev) => {
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
    setPlayerState((prev) => ({
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
    setPlayerState((prev) => ({
      ...prev,
      explorationGold: 0,
      gold: prev.baseCampGold,
      explorationMagicStones: { small: 0, medium: 0, large: 0, huge: 0 },
    }));
  };

  // ============================================================
  // Runtime Battle State Management
  // ============================================================

  /**
   * Update runtime state (HP/AP/mastery between battles)
   */
  const updateRuntimeState = (updates: Partial<RuntimeBattleState>) => {
    setRuntimeState((prev) => ({
      ...prev,
      ...updates,
    }));
  };

  /**
   * Update card mastery for a specific card type
   */
  const updateCardMastery = (cardTypeId: string, useCount: number) => {
    setRuntimeState((prev) => {
      const newStore = new Map(prev.cardMasteryStore);
      const currentCount = newStore.get(cardTypeId) || 0;
      newStore.set(cardTypeId, currentCount + useCount);
      return {
        ...prev,
        cardMasteryStore: newStore,
      };
    });
  };

  /**
   * Reset runtime state for new exploration (return to camp)
   * Note: This resets HP/AP/mastery but preserves lives
   */
  const resetRuntimeState = () => {
    setRuntimeState(prev => ({
      ...createInitialRuntimeState(playerState, prev.difficulty),
      // Preserve lives when returning to camp (not resetting completely)
      lives: prev.lives,
      difficulty: prev.difficulty,
    }));
  };

  // ============================================================
  // Lives System Management
  // ============================================================

  /**
   * Decrease lives by 1 (on death)
   */
  const decreaseLives = () => {
    setRuntimeState(prev => ({
      ...prev,
      lives: decreaseLivesHelper(prev.lives),
    }));
  };

  /**
   * Check if game is over (no lives remaining)
   */
  const isGameOver = () => {
    return isGameOverHelper(runtimeState.lives);
  };

  /**
   * Set game difficulty (resets lives to match new difficulty)
   */
  const setDifficulty = (difficulty: Difficulty) => {
    setRuntimeState(prev => ({
      ...prev,
      difficulty,
      lives: createLivesSystem(difficulty),
    }));
  };

  // ============================================================
  // NEW: PlayerData-based interface
  // ============================================================

  /**
   * Computed PlayerData from internal ExtendedPlayer state
   */
  const playerData = useMemo<PlayerData>(() => ({
    persistent: {
      id: `player_${Date.now()}`,
      name: playerState.name ?? "Adventurer",
      playerClass: playerState.playerClass,
      classGrade: playerState.classGrade,
      level: playerState.level,
      baseMaxHp: playerState.maxHp,
      baseMaxAp: playerState.maxAp,
      baseSpeed: playerState.speed,
      deckCardIds: playerState.deck.map(card => card.id),
      titles: playerState.tittle ?? [],
    },
    resources: {
      baseCampGold: playerState.baseCampGold,
      explorationGold: playerState.explorationGold,
      baseCampMagicStones: playerState.baseCampMagicStones,
      explorationMagicStones: playerState.explorationMagicStones,
      explorationLimit: playerState.explorationLimit,
    },
    inventory: {
      storage: playerState.storage,
      inventory: playerState.inventory,
      equipmentInventory: playerState.equipmentInventory,
      equipmentSlots: playerState.equipmentSlots,
    },
    progression: {
      sanctuaryProgress: playerState.sanctuaryProgress,
      unlockedDepths: [1], // Default: only depth 1 unlocked
      completedAchievements: [],
    },
  }), [playerState]);

  /**
   * Update player data using new PlayerData interface
   * Internally converts to ExtendedPlayer updates for backward compatibility.
   */
  const updatePlayerData = (updates: Partial<PlayerData>) => {
    setPlayerState((prev) => {
      let updated = { ...prev };

      // Update persistent data
      if (updates.persistent) {
        if (updates.persistent.classGrade !== undefined) {
          updated.classGrade = updates.persistent.classGrade;
        }
        if (updates.persistent.level !== undefined) {
          updated.level = updates.persistent.level;
        }
        if (updates.persistent.name !== undefined) {
          updated.name = updates.persistent.name;
        }
        if (updates.persistent.titles !== undefined) {
          updated.tittle = updates.persistent.titles;
        }
      }

      // Update resources
      if (updates.resources) {
        if (updates.resources.baseCampGold !== undefined) {
          updated.baseCampGold = updates.resources.baseCampGold;
          updated.gold = updates.resources.baseCampGold + (updates.resources.explorationGold ?? prev.explorationGold);
        }
        if (updates.resources.explorationGold !== undefined) {
          updated.explorationGold = updates.resources.explorationGold;
          updated.gold = (updates.resources.baseCampGold ?? prev.baseCampGold) + updates.resources.explorationGold;
        }
        if (updates.resources.baseCampMagicStones !== undefined) {
          updated.baseCampMagicStones = updates.resources.baseCampMagicStones;
        }
        if (updates.resources.explorationMagicStones !== undefined) {
          updated.explorationMagicStones = updates.resources.explorationMagicStones;
        }
        if (updates.resources.explorationLimit !== undefined) {
          updated.explorationLimit = updates.resources.explorationLimit;
        }
      }

      // Update inventory
      if (updates.inventory) {
        if (updates.inventory.storage !== undefined) {
          updated.storage = updates.inventory.storage;
        }
        if (updates.inventory.inventory !== undefined) {
          updated.inventory = updates.inventory.inventory;
        }
        if (updates.inventory.equipmentInventory !== undefined) {
          updated.equipmentInventory = updates.inventory.equipmentInventory;
        }
        if (updates.inventory.equipmentSlots !== undefined) {
          updated.equipmentSlots = updates.inventory.equipmentSlots;
        }
      }

      // Update progression
      if (updates.progression) {
        if (updates.progression.sanctuaryProgress !== undefined) {
          updated.sanctuaryProgress = updates.progression.sanctuaryProgress;
        }
      }

      return updated;
    });
  };

  return (
    <PlayerContext.Provider
      value={{
        // PlayerData-based interface
        playerData,
        updatePlayerData,

        // Runtime battle state
        runtimeState,
        updateRuntimeState,
        updateCardMastery,
        resetRuntimeState,

        // Lives system
        decreaseLives,
        isGameOver,
        setDifficulty,

        // Common operations
        updateClassGrade,
        updateHp,
        updateAp,
        addSouls,
        transferSouls,
        resetCurrentRunSouls,
        initializeWithClass,

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
