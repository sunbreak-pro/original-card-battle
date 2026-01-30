/**
 * Battle State Management Hook
 *
 * Manages the core battle state for player and enemies:
 * - Player HP, AP, Guard, Energy, Buffs
 * - Enemy states (array of EnemyBattleState)
 * - Derived values (aliveEnemies, isPlayerAlive, areAllEnemiesDead)
 *
 * This hook is designed to be composed with other battle hooks
 * (useCardExecution, useEnemyAI, etc.) via useBattleOrchestrator.
 */

import { useState, useCallback, useMemo } from "react";
import type { Depth } from '@/types/cardTypes';
import type { BuffDebuffMap } from '@/types/battleTypes';
import type { EnemyDefinition, EnemyBattleState } from '@/types/characterTypes';
import type { BattleStats, CharacterClass } from '@/types/characterTypes';
import { Swordman_Status } from "../../../constants/data/characters/PlayerData";
import { selectRandomEnemy } from "../../characters/enemy/logic/enemyAI";
import { createEnemyStateFromDefinition } from "../logic/enemyStateLogic";

// ============================================================================
// Types
// ============================================================================

/**
 * Player state managed by this hook
 */
export interface PlayerState {
  hp: number;
  maxHp: number;
  ap: number;
  maxAp: number;
  guard: number;
  energy: number;
  maxEnergy: number;
  buffs: BuffDebuffMap;
  name: string;
  playerClass: CharacterClass;
  classGrade: string;
}

/**
 * Return type for useBattleState hook
 */
export interface UseBattleStateReturn {
  // Player state
  playerState: PlayerState;
  updatePlayerHp: (delta: number) => void;
  setPlayerHp: (updater: number | ((prev: number) => number)) => void;
  updatePlayerAp: (delta: number) => void;
  setPlayerAp: (updater: number | ((prev: number) => number)) => void;
  setPlayerGuard: (updater: number | ((prev: number) => number)) => void;
  setPlayerEnergy: (updater: number | ((prev: number) => number)) => void;
  setPlayerBuffs: (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => void;
  resetPlayerGuard: () => void;
  resetPlayerEnergy: () => void;

  // Enemy state
  enemies: EnemyBattleState[];
  currentEnemy: EnemyDefinition | undefined;
  updateEnemyState: (index: number, updates: Partial<EnemyBattleState>) => void;
  updateEnemyByUpdater: (
    index: number,
    updater: (state: EnemyBattleState) => Partial<EnemyBattleState>
  ) => void;
  updateAllEnemies: (
    updater: (state: EnemyBattleState) => Partial<EnemyBattleState>
  ) => void;
  setEnemies: React.Dispatch<React.SetStateAction<EnemyBattleState[]>>;
  removeEnemy: (index: number) => void;

  // Target selection for multi-enemy battles
  selectedTargetIndex: number;
  setSelectedTargetIndex: (index: number) => void;
  selectedEnemy: EnemyBattleState | undefined;

  // Targeted enemy setters (use selectedTargetIndex)
  setTargetedEnemyHp: (updater: number | ((prev: number) => number)) => void;
  setTargetedEnemyAp: (updater: number | ((prev: number) => number)) => void;
  setTargetedEnemyGuard: (updater: number | ((prev: number) => number)) => void;
  setTargetedEnemyEnergy: (updater: number | ((prev: number) => number)) => void;
  setTargetedEnemyBuffs: (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => void;

  // Legacy setters for backward compatibility (index 0 / selected target)
  setEnemyHp: (updater: number | ((prev: number) => number)) => void;
  setEnemyAp: (updater: number | ((prev: number) => number)) => void;
  setEnemyGuard: (updater: number | ((prev: number) => number)) => void;
  setEnemyBuffs: (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => void;

  // Derived values
  aliveEnemies: EnemyBattleState[];
  isPlayerAlive: boolean;
  areAllEnemiesDead: boolean;

  // Legacy derived values for backward compatibility (selected target)
  enemyHp: number;
  enemyMaxHp: number;
  enemyAp: number;
  enemyMaxAp: number;
  enemyGuard: number;
  enemyBuffs: BuffDebuffMap;
  enemyEnergy: number;

  // Enemy energy state (separate for backward compatibility)
  setEnemyEnergy: (value: number) => void;

  // BattleStats-compatible objects for damage calculations
  playerBattleStats: BattleStats;
  enemyBattleStats: BattleStats;

  // Reset function for next battle
  resetForNextEnemy: (nextEnemies: EnemyDefinition | EnemyDefinition[]) => void;
}

// ============================================================================
// Types for initial player state
// ============================================================================

/**
 * Initial player state passed from PlayerContext
 */
export interface InitialPlayerState {
  /** Current HP from previous battle or exploration start */
  currentHp: number;
  /** Current AP from previous battle or exploration start */
  currentAp: number;
  /** Max HP */
  maxHp: number;
  /** Max AP */
  maxAp: number;
  /** Player name */
  name?: string;
  /** Player class */
  playerClass?: CharacterClass;
  /** Class grade */
  classGrade?: string;
  /** Speed */
  speed?: number;
  /** Card energy */
  cardActEnergy?: number;
  /** Card mastery store - cardTypeId -> useCount */
  cardMasteryStore?: Map<string, number>;
  /** Deck configuration - maps card type IDs to counts */
  deckConfig?: Record<string, number>;
}

// ============================================================================
// Hook Implementation
// ============================================================================

/**
 * Battle state management hook
 *
 * @param depth - Current dungeon depth for enemy selection
 * @param initialEnemies - Optional initial enemies (overrides random selection)
 * @param playerSpeed - Player speed from phase state
 * @param initialPlayerState - Optional initial player state from PlayerContext
 */
export function useBattleState(
  depth: Depth,
  initialEnemies?: EnemyDefinition[],
  playerSpeed: number = Swordman_Status.speed,
  initialPlayerState?: InitialPlayerState,
  encounterType: "normal" | "group" | "boss" = "normal"
): UseBattleStateReturn {
  // ========================================================================
  // Player State
  // Use initialPlayerState if provided, otherwise fall back to defaults
  // ========================================================================

  const [playerName] = useState<string>(initialPlayerState?.name ?? "エイレス");
  const [playerClass] = useState<CharacterClass>(
    initialPlayerState?.playerClass ?? Swordman_Status.playerClass
  );
  const [classGrade] = useState<string>(
    initialPlayerState?.classGrade ?? Swordman_Status.classGrade
  );
  const [playerHp, setPlayerHpInternal] = useState(
    initialPlayerState?.currentHp ?? Swordman_Status.hp
  );
  const [playerMaxHp] = useState(
    initialPlayerState?.maxHp ?? Swordman_Status.maxHp
  );
  const [playerAp, setPlayerApInternal] = useState(
    initialPlayerState?.currentAp ?? Swordman_Status.ap
  );
  const [playerMaxAp] = useState(
    initialPlayerState?.maxAp ?? Swordman_Status.maxAp
  );
  const [playerGuard, setPlayerGuardInternal] = useState(Swordman_Status.guard);
  const [playerEnergy, setPlayerEnergyInternal] = useState(
    initialPlayerState?.cardActEnergy ?? Swordman_Status.cardActEnergy
  );
  const [maxEnergy] = useState(
    initialPlayerState?.cardActEnergy ?? Swordman_Status.cardActEnergy
  );
  const [playerBuffs, setPlayerBuffsInternal] = useState<BuffDebuffMap>(new Map());

  // ========================================================================
  // Enemy State
  // ========================================================================

  const [enemies, setEnemies] = useState<EnemyBattleState[]>(() => {
    if (initialEnemies && initialEnemies.length > 0) {
      return initialEnemies.map((def) => createEnemyStateFromDefinition(def));
    }
    const { enemies: selectedEnemies } = selectRandomEnemy(depth, encounterType);
    return selectedEnemies.map((def) => createEnemyStateFromDefinition(def));
  });

  // Separate enemy energy state for backward compatibility
  const [enemyEnergy, setEnemyEnergy] = useState(enemies[0]?.energy ?? 1);

  // ========================================================================
  // Target Selection State
  // ========================================================================

  const [selectedTargetIndex, setSelectedTargetIndexInternal] = useState(0);

  // ========================================================================
  // Derived Enemy Values (use selectedTargetIndex for legacy compatibility)
  // ========================================================================

  // Find the actual index in the enemies array for the selected alive enemy
  const aliveEnemiesForTarget = useMemo(
    () => enemies.map((e, i) => ({ state: e, originalIndex: i })).filter(item => item.state.hp > 0),
    [enemies]
  );

  // Clamp selectedTargetIndex to valid range
  const clampedTargetIndex = useMemo(() => {
    if (aliveEnemiesForTarget.length === 0) return 0;
    return Math.min(selectedTargetIndex, aliveEnemiesForTarget.length - 1);
  }, [selectedTargetIndex, aliveEnemiesForTarget]);

  // The actual enemy array index of the selected target
  const selectedEnemyArrayIndex = aliveEnemiesForTarget[clampedTargetIndex]?.originalIndex ?? 0;

  const selectedEnemy = enemies[selectedEnemyArrayIndex];

  const setSelectedTargetIndex = useCallback((index: number) => {
    setSelectedTargetIndexInternal(index);
  }, []);

  const currentEnemy = selectedEnemy?.definition ?? enemies[0]?.definition;
  const enemyHp = selectedEnemy?.hp ?? 0;
  const enemyMaxHp = selectedEnemy?.maxHp ?? 0;
  const enemyAp = selectedEnemy?.ap ?? 0;
  const enemyMaxAp = selectedEnemy?.maxAp ?? 0;
  const enemyGuard = selectedEnemy?.guard ?? 0;
  const enemyBuffs = useMemo(() => selectedEnemy?.buffDebuffs ?? new Map(), [selectedEnemy]);

  // ========================================================================
  // Derived Values
  // ========================================================================

  const aliveEnemies = useMemo(
    () => enemies.filter((e) => e.hp > 0),
    [enemies]
  );

  const isPlayerAlive = playerHp > 0;
  const areAllEnemiesDead = useMemo(
    () => enemies.every((e) => e.hp <= 0),
    [enemies]
  );

  // ========================================================================
  // Player State Object (memoized)
  // ========================================================================

  const playerState: PlayerState = useMemo(
    () => ({
      hp: playerHp,
      maxHp: playerMaxHp,
      ap: playerAp,
      maxAp: playerMaxAp,
      guard: playerGuard,
      energy: playerEnergy,
      maxEnergy,
      buffs: playerBuffs,
      name: playerName ?? "",
      playerClass,
      classGrade,
    }),
    [
      playerHp,
      playerMaxHp,
      playerAp,
      playerMaxAp,
      playerGuard,
      playerEnergy,
      maxEnergy,
      playerBuffs,
      playerName,
      playerClass,
      classGrade,
    ]
  );

  // ========================================================================
  // Player Update Functions
  // ========================================================================

  const updatePlayerHp = useCallback((delta: number) => {
    setPlayerHpInternal((prev) => Math.max(0, Math.min(prev + delta, playerMaxHp)));
  }, [playerMaxHp]);

  const setPlayerHp = useCallback(
    (updater: number | ((prev: number) => number)) => {
      setPlayerHpInternal((prev) => {
        const newValue = typeof updater === "function" ? updater(prev) : updater;
        return Math.max(0, Math.min(newValue, playerMaxHp));
      });
    },
    [playerMaxHp]
  );

  const updatePlayerAp = useCallback((delta: number) => {
    setPlayerApInternal((prev) => Math.max(0, Math.min(prev + delta, playerMaxAp)));
  }, [playerMaxAp]);

  const setPlayerAp = useCallback(
    (updater: number | ((prev: number) => number)) => {
      setPlayerApInternal((prev) => {
        const newValue = typeof updater === "function" ? updater(prev) : updater;
        return Math.max(0, newValue);
      });
    },
    []
  );

  const setPlayerGuard = useCallback(
    (updater: number | ((prev: number) => number)) => {
      setPlayerGuardInternal((prev) => {
        const newValue = typeof updater === "function" ? updater(prev) : updater;
        return Math.max(0, newValue);
      });
    },
    []
  );

  const setPlayerEnergy = useCallback(
    (updater: number | ((prev: number) => number)) => {
      setPlayerEnergyInternal((prev) => {
        const newValue = typeof updater === "function" ? updater(prev) : updater;
        return Math.max(0, Math.min(newValue, maxEnergy));
      });
    },
    [maxEnergy]
  );

  const setPlayerBuffs = useCallback(
    (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => {
      setPlayerBuffsInternal((prev) =>
        typeof updater === "function" ? updater(prev) : updater
      );
    },
    []
  );

  const resetPlayerGuard = useCallback(() => {
    setPlayerGuardInternal(0);
  }, []);

  const resetPlayerEnergy = useCallback(() => {
    setPlayerEnergyInternal(maxEnergy);
  }, [maxEnergy]);

  // ========================================================================
  // Enemy Update Functions
  // ========================================================================

  const updateEnemyState = useCallback(
    (index: number, updates: Partial<EnemyBattleState>) => {
      setEnemies((prev) =>
        prev.map((e, i) => (i === index ? { ...e, ...updates } : e))
      );
    },
    []
  );

  const updateEnemyByUpdater = useCallback(
    (
      index: number,
      updater: (state: EnemyBattleState) => Partial<EnemyBattleState>
    ) => {
      setEnemies((prev) =>
        prev.map((e, i) => (i === index ? { ...e, ...updater(e) } : e))
      );
    },
    []
  );

  const updateAllEnemies = useCallback(
    (updater: (state: EnemyBattleState) => Partial<EnemyBattleState>) => {
      setEnemies((prev) => prev.map((e) => ({ ...e, ...updater(e) })));
    },
    []
  );

  const removeEnemy = useCallback((index: number) => {
    setEnemies((prev) => prev.filter((_, i) => i !== index));
  }, []);

  // ========================================================================
  // Targeted Enemy Setters (use selectedTargetIndex)
  // ========================================================================

  const setTargetedEnemyHp = useCallback(
    (updater: number | ((prev: number) => number)) => {
      updateEnemyByUpdater(selectedEnemyArrayIndex, (e) => ({
        hp: typeof updater === "function" ? updater(e.hp) : updater,
      }));
    },
    [updateEnemyByUpdater, selectedEnemyArrayIndex]
  );

  const setTargetedEnemyAp = useCallback(
    (updater: number | ((prev: number) => number)) => {
      updateEnemyByUpdater(selectedEnemyArrayIndex, (e) => ({
        ap: typeof updater === "function" ? updater(e.ap) : updater,
      }));
    },
    [updateEnemyByUpdater, selectedEnemyArrayIndex]
  );

  const setTargetedEnemyGuard = useCallback(
    (updater: number | ((prev: number) => number)) => {
      updateEnemyByUpdater(selectedEnemyArrayIndex, (e) => ({
        guard: typeof updater === "function" ? updater(e.guard) : updater,
      }));
    },
    [updateEnemyByUpdater, selectedEnemyArrayIndex]
  );

  const setTargetedEnemyEnergy = useCallback(
    (updater: number | ((prev: number) => number)) => {
      updateEnemyByUpdater(selectedEnemyArrayIndex, (e) => ({
        energy: typeof updater === "function" ? updater(e.energy) : updater,
      }));
    },
    [updateEnemyByUpdater, selectedEnemyArrayIndex]
  );

  const setTargetedEnemyBuffs = useCallback(
    (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => {
      updateEnemyByUpdater(selectedEnemyArrayIndex, (e) => ({
        buffDebuffs: typeof updater === "function" ? updater(e.buffDebuffs) : updater,
      }));
    },
    [updateEnemyByUpdater, selectedEnemyArrayIndex]
  );

  // ========================================================================
  // Legacy Enemy Setters (route through selected target for card damage)
  // ========================================================================

  const setEnemyHp = useCallback(
    (updater: number | ((prev: number) => number)) => {
      updateEnemyByUpdater(selectedEnemyArrayIndex, (e) => ({
        hp: typeof updater === "function" ? updater(e.hp) : updater,
      }));
    },
    [updateEnemyByUpdater, selectedEnemyArrayIndex]
  );

  const setEnemyAp = useCallback(
    (updater: number | ((prev: number) => number)) => {
      updateEnemyByUpdater(selectedEnemyArrayIndex, (e) => ({
        ap: typeof updater === "function" ? updater(e.ap) : updater,
      }));
    },
    [updateEnemyByUpdater, selectedEnemyArrayIndex]
  );

  const setEnemyGuard = useCallback(
    (updater: number | ((prev: number) => number)) => {
      updateEnemyByUpdater(selectedEnemyArrayIndex, (e) => ({
        guard: typeof updater === "function" ? updater(e.guard) : updater,
      }));
    },
    [updateEnemyByUpdater, selectedEnemyArrayIndex]
  );

  const setEnemyBuffs = useCallback(
    (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => {
      updateEnemyByUpdater(selectedEnemyArrayIndex, (e) => ({
        buffDebuffs: typeof updater === "function" ? updater(e.buffDebuffs) : updater,
      }));
    },
    [updateEnemyByUpdater, selectedEnemyArrayIndex]
  );

  // ========================================================================
  // Character Objects for Calculations (memoized)
  // ========================================================================

  /**
   * NEW: BattleStats-compatible player object for damage calculations
   * Use this for new code that expects BattleStats interface.
   */
  const playerBattleStats = useMemo<BattleStats>(
    () => ({
      hp: playerHp,
      maxHp: playerMaxHp,
      ap: playerAp,
      maxAp: playerMaxAp,
      guard: playerGuard,
      speed: playerSpeed,
      buffDebuffs: playerBuffs,
    }),
    [playerHp, playerMaxHp, playerAp, playerMaxAp, playerGuard, playerSpeed, playerBuffs]
  );

  /**
   * BattleStats-compatible enemy object for damage calculations
   * Uses the currently selected target enemy
   */
  const enemyBattleStats = useMemo<BattleStats>(
    () => ({
      hp: enemyHp,
      maxHp: enemyMaxHp,
      ap: enemyAp,
      maxAp: enemyMaxAp,
      guard: enemyGuard,
      speed: currentEnemy?.baseSpeed ?? 0,
      buffDebuffs: enemyBuffs,
    }),
    [enemyHp, enemyMaxHp, enemyAp, enemyMaxAp, enemyGuard, enemyBuffs, currentEnemy?.baseSpeed]
  );

  // ========================================================================
  // Reset for Next Enemy
  // ========================================================================

  const resetForNextEnemy = useCallback(
    (nextEnemies: EnemyDefinition | EnemyDefinition[]) => {
      const enemyDefs = Array.isArray(nextEnemies) ? nextEnemies : [nextEnemies];
      setEnemies(enemyDefs.map((def) => createEnemyStateFromDefinition(def)));
      setEnemyEnergy(enemyDefs[0]?.actEnergy ?? 1);
      setSelectedTargetIndexInternal(0);

      // Reset player temporary state
      setPlayerGuardInternal(0);
      setPlayerBuffsInternal(new Map());
      setPlayerEnergyInternal(maxEnergy);
    },
    [maxEnergy]
  );

  // ========================================================================
  // Return
  // ========================================================================

  return {
    // Player state
    playerState,
    updatePlayerHp,
    setPlayerHp,
    updatePlayerAp,
    setPlayerAp,
    setPlayerGuard,
    setPlayerEnergy,
    setPlayerBuffs,
    resetPlayerGuard,
    resetPlayerEnergy,

    // Enemy state
    enemies,
    currentEnemy,
    updateEnemyState,
    updateEnemyByUpdater,
    updateAllEnemies,
    setEnemies,
    removeEnemy,

    // Target selection
    selectedTargetIndex: clampedTargetIndex,
    setSelectedTargetIndex,
    selectedEnemy,

    // Targeted enemy setters
    setTargetedEnemyHp,
    setTargetedEnemyAp,
    setTargetedEnemyGuard,
    setTargetedEnemyEnergy,
    setTargetedEnemyBuffs,

    // Legacy setters (routed through selected target)
    setEnemyHp,
    setEnemyAp,
    setEnemyGuard,
    setEnemyBuffs,

    // Derived values
    aliveEnemies,
    isPlayerAlive,
    areAllEnemiesDead,

    // Legacy derived values (selected target)
    enemyHp,
    enemyMaxHp,
    enemyAp,
    enemyMaxAp,
    enemyGuard,
    enemyBuffs,
    enemyEnergy,
    setEnemyEnergy,

    // BattleStats-compatible objects
    playerBattleStats,
    enemyBattleStats,

    // Reset
    resetForNextEnemy,
  };
}
