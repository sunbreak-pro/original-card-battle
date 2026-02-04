// PlayerContext: Manages player state including stats, storage, inventory, and progression
// Resource management (gold, magic stones) has been moved to ResourceContext

import React, {
  createContext,
  useContext,
  useState,
  useMemo,
  useCallback,
  type ReactNode,
} from "react";
import type {
  PlayerData,
  Difficulty,
  LivesSystem,
  CharacterClass,
} from "@/types/characterTypes";
import type { Card } from "@/types/cardTypes";
import type {
  StorageState,
  InventoryState,
  EquipmentInventoryState,
  EquipmentSlots,
  SanctuaryProgress,
  ShopStockState,
  InnBuffsState,
} from "@/types/campTypes";
import { createLivesSystem } from "../domain/characters/player/logic/playerUtils";
import {
  Swordman_Status,
  Mage_Status,
} from "../constants/data/characters/PlayerData";
import type { BasePlayerStats } from "../constants/data/characters/PlayerData";
import { getCharacterClassInfo } from "@/constants/data/characters/CharacterClassData";
import { useResources } from "./ResourceContext";
import {
  STORAGE_MAX_CAPACITY,
  INVENTORY_MAX_CAPACITY,
  EQUIPMENT_INVENTORY_MAX,
} from "../constants";
import { calculateEquipmentAP } from "../domain/item_equipment/logic/equipmentStats";
import { generateId } from "@/utils/idGenerator";

// Extracted hooks
import { usePlayerBattle } from "@/domain/characters/player/hooks/usePlayerBattle";
import { usePlayerProgression } from "@/domain/characters/player/hooks/usePlayerProgression";
import { usePlayerDeck } from "@/domain/characters/player/hooks/usePlayerDeck";
import { useEquipmentAP } from "@/domain/characters/player/hooks/useEquipmentAP";

/**
 * Internal player state used by PlayerContext.
 * Combines base stats with storage, inventory, and resource tracking.
 * Exported for use by extracted hooks.
 */
export interface InternalPlayerState {
  // Base stats
  name?: string;
  playerClass: CharacterClass;
  classGrade: string;
  level: number;
  hp: number;
  maxHp: number;
  ap: number;
  maxAp: number;
  guard: number;
  speed: number;
  cardActEnergy: number;
  deck: Card[];
  title?: string[];

  // Storage & Inventory
  storage: StorageState;
  inventory: InventoryState;
  equipmentInventory: EquipmentInventoryState;
  equipmentSlots: EquipmentSlots;

  // Progression
  sanctuaryProgress: SanctuaryProgress;
  shopRotationDay?: number;
  shopStockState?: ShopStockState;
  innBuffsState?: InnBuffsState;
}

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
  /** Player data */
  playerData: PlayerData;

  /** Update player data with partial updates (object or function form) */
  updatePlayerData: (
    updates: Partial<PlayerData> | ((prev: PlayerData) => Partial<PlayerData>),
  ) => void;

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
  // Card Derivation / Unlock System
  // ============================================================

  /** Set of cardTypeIds that have been unlocked through derivation */
  unlockedCardTypeIds: Set<string>;

  /** Add newly unlocked card type IDs (from derivation checks) */
  addUnlockedCards: (cardTypeIds: string[]) => void;

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
  // Deck
  // ============================================================

  /** Current deck cards */
  deckCards: Card[];

  /** Update deck by replacing card IDs and internal deck */
  updateDeck: (cardIds: string[]) => void;

  // Common operations
  // ============================================================

  updateClassGrade: (newGrade: string) => void;
  updateBaseMaxHp: (delta: number) => void;
  updateBaseMaxAp: (delta: number) => void;
  updateHp: (newHp: number) => void;
  updateAp: (newAp: number) => void;
  addSouls: (amount: number) => void;
  transferSouls: (survivalMultiplier: number) => void;
  resetCurrentRunSouls: () => void;
  initializeWithClass: (classType: CharacterClass) => void;

  // Equipment AP system
  /** Apply durability damage to equipment and update AP. Returns new AP values. */
  applyEquipmentDurabilityDamage: (apDamage: number) => {
    currentAp: number;
    maxAp: number;
  };
}

const PlayerContext = createContext<PlayerContextValue | undefined>(undefined);

/**
 * Get base player data by character class
 */
function getBasePlayerByClass(classType: CharacterClass): BasePlayerStats {
  switch (classType) {
    case "swordsman":
      return Swordman_Status;
    case "mage":
      return Mage_Status;
    default:
      return Swordman_Status;
  }
}

/**
 * Create initial internal player state from base stats
 * Note: Gold and magic stone values come from ResourceContext
 */
function createInitialPlayerState(
  basePlayer: BasePlayerStats,
): InternalPlayerState {
  return {
    ...basePlayer,

    // Storage & Inventory (with test items)
    storage: {
      items: [],
      maxCapacity: STORAGE_MAX_CAPACITY,
      currentCapacity: 0,
    },
    inventory: {
      items: [],
      maxCapacity: INVENTORY_MAX_CAPACITY,
      currentCapacity: 0,
    },
    equipmentInventory: {
      items: [],
      maxCapacity: EQUIPMENT_INVENTORY_MAX,
      currentCapacity: 0,
    },
    equipmentSlots: {
      weapon: null,
      armor: null,
      helmet: null,
      boots: null,
      accessory1: null,
      accessory2: null,
    },

    // Progression
    sanctuaryProgress: {
      currentRunSouls: 0,
      totalSouls: 0,
      unlockedNodes: [],
      explorationLimitBonus: 0,
    },
  };
}

/**
 * Create initial runtime battle state
 */
function createInitialRuntimeState(
  basePlayer: { hp: number; ap: number },
  difficulty: Difficulty = "normal",
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

  // Stable player ID — generated once on mount
  const [playerId] = useState(() => generateId('player'));

  // Internal state
  const [playerState, setPlayerState] = useState<InternalPlayerState>(() =>
    createInitialPlayerState(Swordman_Status),
  );

  // Runtime battle state - persists between battles within exploration
  const [runtimeState, setRuntimeState] = useState<RuntimeBattleState>(() => {
    const initialPlayer = createInitialPlayerState(Swordman_Status);
    const equipAP = calculateEquipmentAP(initialPlayer.equipmentSlots);
    return createInitialRuntimeState({
      hp: Swordman_Status.hp,
      ap: equipAP.totalAP,
    });
  });

  // ============================================================
  // Extracted hooks
  // ============================================================

  const battle = usePlayerBattle(runtimeState, setRuntimeState);
  const progression = usePlayerProgression(setPlayerState);
  const deck = usePlayerDeck(playerState.playerClass, setPlayerState);
  const { equipmentAP, applyEquipmentDurabilityDamage } = useEquipmentAP(
    playerState.equipmentSlots,
    setPlayerState,
    setRuntimeState,
  );

  // ============================================================
  // Card Derivation / Unlock System
  // ============================================================

  const [unlockedCardTypeIds, setUnlockedCardTypeIds] = useState<Set<string>>(
    () => {
      // Initialize with starter deck card type IDs
      const initialIds = new Set<string>();
      for (const card of Swordman_Status.deck) {
        initialIds.add(card.cardTypeId);
      }
      return initialIds;
    },
  );

  const addUnlockedCards = useCallback((cardTypeIds: string[]) => {
    if (cardTypeIds.length === 0) return;
    setUnlockedCardTypeIds((prev) => {
      const next = new Set(prev);
      for (const id of cardTypeIds) {
        next.add(id);
      }
      return next;
    });
  }, []);

  // ============================================================
  // Cross-state operations (stay in PlayerContext)
  // ============================================================

  /**
   * Initialize player with a specific character class
   * Used when starting a new game from character selection
   */
  const initializeWithClass = (classType: CharacterClass) => {
    const basePlayer = getBasePlayerByClass(classType);
    const classInfo = getCharacterClassInfo(classType);

    // Create player with starter deck from class data
    const playerWithStarterDeck: BasePlayerStats = {
      ...basePlayer,
      deck: classInfo.starterDeck,
    };

    const newPlayer = createInitialPlayerState(playerWithStarterDeck);

    setPlayerState({
      ...newPlayer,
      // Reset souls for new game
      sanctuaryProgress: {
        currentRunSouls: 0,
        totalSouls: 0,
        unlockedNodes: [],
        explorationLimitBonus: 0,
      },
    });

    // Reset runtime state with equipment-derived AP
    const equipAP2 = calculateEquipmentAP(newPlayer.equipmentSlots);
    setRuntimeState((prev) => ({
      ...createInitialRuntimeState(
        { hp: newPlayer.maxHp, ap: equipAP2.totalAP },
        prev.difficulty,
      ),
    }));

    // Reset unlocked cards to starter deck card types
    const starterIds = new Set<string>();
    for (const card of classInfo.starterDeck) {
      starterIds.add(card.cardTypeId);
    }
    setUnlockedCardTypeIds(starterIds);
  };

  /**
   * Reset runtime state for new exploration (return to camp)
   * Note: This resets HP/AP/mastery but preserves lives
   */
  const resetRuntimeState = () => {
    // AP is derived from equipment durability — not reset to a base value
    const equipAP2 = calculateEquipmentAP(playerState.equipmentSlots);
    setRuntimeState((prev) => ({
      ...createInitialRuntimeState(
        { hp: playerState.maxHp, ap: equipAP2.totalAP },
        prev.difficulty,
      ),
      // Preserve lives when returning to camp (not resetting completely)
      lives: prev.lives,
      difficulty: prev.difficulty,
    }));
  };

  // ============================================================
  // PlayerData interface
  // ============================================================

  const playerData = useMemo<PlayerData>(
    () => ({
      persistent: {
        id: playerId,
        name: playerState.name ?? "Adventurer",
        playerClass: playerState.playerClass,
        classGrade: playerState.classGrade,
        level: playerState.level,
        baseMaxHp: playerState.maxHp,
        baseMaxAp: equipmentAP.maxAP,
        baseSpeed: playerState.speed,
        cardActEnergy: playerState.cardActEnergy,
        deckCardIds: playerState.deck.map((card) => card.id),
        titles: playerState.title ?? [],
      },
      resources: {
        baseCampGold: resourceContext.resources.gold.baseCamp,
        explorationGold: resourceContext.resources.gold.exploration,
        baseCampMagicStones: resourceContext.resources.magicStones.baseCamp,
        explorationMagicStones:
          resourceContext.resources.magicStones.exploration,
        explorationLimit: resourceContext.resources.explorationLimit,
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
        shopRotationDay: playerState.shopRotationDay,
        shopStockState: playerState.shopStockState,
        innBuffsState: playerState.innBuffsState,
      },
    }),
    [playerState, playerId, equipmentAP, resourceContext.resources],
  );

  /**
   * Build PlayerData from InternalPlayerState (for functional updater form)
   */
  const buildPlayerData = (state: InternalPlayerState): PlayerData => {
    const eqAP = calculateEquipmentAP(state.equipmentSlots);
    return {
      persistent: {
        id: playerId,
        name: state.name ?? "Adventurer",
        playerClass: state.playerClass,
        classGrade: state.classGrade,
        level: state.level,
        baseMaxHp: state.maxHp,
        baseMaxAp: eqAP.maxAP,
        baseSpeed: state.speed,
        cardActEnergy: state.cardActEnergy,
        deckCardIds: state.deck.map((card) => card.id),
        titles: state.title ?? [],
      },
      resources: {
        baseCampGold: resourceContext.resources.gold.baseCamp,
        explorationGold: resourceContext.resources.gold.exploration,
        baseCampMagicStones: resourceContext.resources.magicStones.baseCamp,
        explorationMagicStones:
          resourceContext.resources.magicStones.exploration,
        explorationLimit: resourceContext.resources.explorationLimit,
      },
      inventory: {
        storage: state.storage,
        inventory: state.inventory,
        equipmentInventory: state.equipmentInventory,
        equipmentSlots: state.equipmentSlots,
      },
      progression: {
        sanctuaryProgress: state.sanctuaryProgress,
        unlockedDepths: [1],
        completedAchievements: [],
        shopRotationDay: state.shopRotationDay,
        shopStockState: state.shopStockState,
        innBuffsState: state.innBuffsState,
      },
    };
  };

  /**
   * Apply Partial<PlayerData> updates to InternalPlayerState
   */
  const applyUpdates = (
    prev: InternalPlayerState,
    updates: Partial<PlayerData>,
  ): InternalPlayerState => {
    const updated = { ...prev };

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
        updated.title = updates.persistent.titles;
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
      if (updates.progression.shopRotationDay !== undefined) {
        updated.shopRotationDay = updates.progression.shopRotationDay;
      }
      if (updates.progression.shopStockState !== undefined) {
        updated.shopStockState = updates.progression.shopStockState;
      }
      if (updates.progression.innBuffsState !== undefined) {
        updated.innBuffsState = updates.progression.innBuffsState;
      }
    }

    return updated;
  };

  /**
   * Update player data using PlayerData interface
   * Accepts either a partial update object or a function that receives current PlayerData
   */
  const updatePlayerData = (
    updatesOrFn:
      | Partial<PlayerData>
      | ((prev: PlayerData) => Partial<PlayerData>),
  ) => {
    setPlayerState((prev) => {
      const updates =
        typeof updatesOrFn === "function"
          ? updatesOrFn(buildPlayerData(prev))
          : updatesOrFn;

      return applyUpdates(prev, updates);
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
        resetRuntimeState,

        // Card Derivation / Unlock
        unlockedCardTypeIds,
        addUnlockedCards,

        // Deck
        deckCards: playerState.deck,

        // Cross-state operations
        initializeWithClass,
        applyEquipmentDurabilityDamage,

        // Spread extracted hooks
        ...battle,
        ...progression,
        ...deck,
      }}
    >
      {children}
    </PlayerContext.Provider>
  );
};

/**
 * Hook to use Player context
 */
// eslint-disable-next-line react-refresh/only-export-components
export const usePlayer = () => {
  const context = useContext(PlayerContext);
  if (!context) {
    throw new Error("usePlayer must be used within PlayerProvider");
  }
  return context;
};
