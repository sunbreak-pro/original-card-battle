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
import type { Depth } from "../../cards/type/cardType";
import type { BuffDebuffMap } from "../type/baffType";
import type { Enemy } from "../../characters/type/enemyType";
import type { EnemyBattleState } from "../type/battleStateType";
import type { Player } from "../../characters/type/playerTypes";
import { Swordman_Status } from "../../characters/player/data/PlayerData";
import { selectRandomEnemy } from "../../characters/enemy/logic/enemyAI";
import { createEnemyState } from "../logic/enemyStateLogic";

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
  playerClass: Player["playerClass"];
  classGrade: Player["classGrade"];
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
  currentEnemy: Enemy | undefined;
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

  // Legacy setters for backward compatibility with battleFlowManage
  setEnemyHp: (updater: number | ((prev: number) => number)) => void;
  setEnemyAp: (updater: number | ((prev: number) => number)) => void;
  setEnemyGuard: (updater: number | ((prev: number) => number)) => void;
  setEnemyBuffs: (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => void;

  // Derived values
  aliveEnemies: EnemyBattleState[];
  isPlayerAlive: boolean;
  areAllEnemiesDead: boolean;

  // Legacy derived values for backward compatibility
  enemyHp: number;
  enemyMaxHp: number;
  enemyAp: number;
  enemyMaxAp: number;
  enemyGuard: number;
  enemyBuffs: BuffDebuffMap;
  enemyEnergy: number;

  // Enemy energy state (separate for backward compatibility)
  setEnemyEnergy: (value: number) => void;

  // Memoized character objects for calculations
  playerChar: Player;
  enemyChar: Enemy;

  // Reset function for next battle
  resetForNextEnemy: (nextEnemies: Enemy | Enemy[]) => void;
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
 */
export function useBattleState(
  depth: Depth,
  initialEnemies?: Enemy[],
  playerSpeed: number = Swordman_Status.speed
): UseBattleStateReturn {
  // ========================================================================
  // Player State
  // ========================================================================

  const [playerName] = useState<Player["name"]>("エイレス");
  const [playerClass] = useState<Player["playerClass"]>(Swordman_Status.playerClass);
  const [classGrade] = useState<Player["classGrade"]>(Swordman_Status.classGrade);
  const [playerHp, setPlayerHpInternal] = useState(Swordman_Status.hp);
  const [playerMaxHp] = useState(Swordman_Status.maxHp);
  const [playerAp, setPlayerApInternal] = useState(Swordman_Status.ap);
  const [playerMaxAp] = useState(Swordman_Status.maxAp);
  const [playerGuard, setPlayerGuardInternal] = useState(Swordman_Status.guard);
  const [playerEnergy, setPlayerEnergyInternal] = useState(Swordman_Status.cardActEnergy);
  const [maxEnergy] = useState(Swordman_Status.cardActEnergy);
  const [playerBuffs, setPlayerBuffsInternal] = useState<BuffDebuffMap>(new Map());

  // ========================================================================
  // Enemy State
  // ========================================================================

  const [enemies, setEnemies] = useState<EnemyBattleState[]>(() => {
    if (initialEnemies && initialEnemies.length > 0) {
      return initialEnemies.map(createEnemyState);
    }
    const { enemies: selectedEnemies } = selectRandomEnemy(depth, "normal");
    return selectedEnemies.map(createEnemyState);
  });

  // Separate enemy energy state for backward compatibility
  const [enemyEnergy, setEnemyEnergy] = useState(enemies[0]?.energy ?? 1);

  // ========================================================================
  // Derived Enemy Values (for backward compatibility)
  // ========================================================================

  const currentEnemy = enemies[0]?.enemy;
  const enemyHp = enemies[0]?.hp ?? 0;
  const enemyMaxHp = enemies[0]?.enemy.maxHp ?? 0;
  const enemyAp = enemies[0]?.ap ?? 0;
  const enemyMaxAp = enemies[0]?.enemy.maxAp ?? 0;
  const enemyGuard = enemies[0]?.guard ?? 0;
  const enemyBuffs = useMemo(() => enemies[0]?.buffs ?? new Map(), [enemies]);

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
  // Legacy Enemy Setters (for backward compatibility with battleFlowManage)
  // ========================================================================

  const setEnemyHp = useCallback(
    (updater: number | ((prev: number) => number)) => {
      updateEnemyByUpdater(0, (e) => ({
        hp: typeof updater === "function" ? updater(e.hp) : updater,
      }));
    },
    [updateEnemyByUpdater]
  );

  const setEnemyAp = useCallback(
    (updater: number | ((prev: number) => number)) => {
      updateEnemyByUpdater(0, (e) => ({
        ap: typeof updater === "function" ? updater(e.ap) : updater,
      }));
    },
    [updateEnemyByUpdater]
  );

  const setEnemyGuard = useCallback(
    (updater: number | ((prev: number) => number)) => {
      updateEnemyByUpdater(0, (e) => ({
        guard: typeof updater === "function" ? updater(e.guard) : updater,
      }));
    },
    [updateEnemyByUpdater]
  );

  const setEnemyBuffs = useCallback(
    (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => {
      updateEnemyByUpdater(0, (e) => ({
        buffs: typeof updater === "function" ? updater(e.buffs) : updater,
      }));
    },
    [updateEnemyByUpdater]
  );

  // ========================================================================
  // Character Objects for Calculations (memoized)
  // ========================================================================

  const playerChar = useMemo<Player>(
    () => ({
      playerClass,
      classGrade,
      name: playerName,
      hp: playerHp,
      maxHp: playerMaxHp,
      ap: playerAp,
      maxAp: playerMaxAp,
      guard: playerGuard,
      buffDebuffs: playerBuffs,
      level: 1,
      speed: playerSpeed,
      cardActEnergy: maxEnergy,
      gold: 0,
      deck: [],
    }),
    [
      playerClass,
      classGrade,
      playerName,
      playerHp,
      playerMaxHp,
      playerAp,
      playerMaxAp,
      playerGuard,
      playerBuffs,
      playerSpeed,
      maxEnergy,
    ]
  );

  const enemyChar = useMemo<Enemy>(
    () =>
      currentEnemy
        ? {
            id: currentEnemy.id,
            name: currentEnemy.name,
            description: currentEnemy.description,
            speed: currentEnemy.speed,
            actEnergy: currentEnemy.actEnergy,
            startingGuard: currentEnemy.startingGuard,
            aiPatterns: currentEnemy.aiPatterns,
            hp: enemyHp,
            maxHp: enemyMaxHp,
            ap: enemyAp,
            maxAp: enemyMaxAp,
            guard: enemyGuard,
            buffDebuffs: enemyBuffs,
          }
        : ({} as Enemy),
    [
      currentEnemy,
      enemyHp,
      enemyMaxHp,
      enemyAp,
      enemyMaxAp,
      enemyGuard,
      enemyBuffs,
    ]
  );

  // ========================================================================
  // Reset for Next Enemy
  // ========================================================================

  const resetForNextEnemy = useCallback(
    (nextEnemies: Enemy | Enemy[]) => {
      const enemies = Array.isArray(nextEnemies) ? nextEnemies : [nextEnemies];
      setEnemies(enemies.map(createEnemyState));
      setEnemyEnergy(enemies[0]?.actEnergy ?? 1);

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

    // Legacy setters
    setEnemyHp,
    setEnemyAp,
    setEnemyGuard,
    setEnemyBuffs,

    // Derived values
    aliveEnemies,
    isPlayerAlive,
    areAllEnemiesDead,

    // Legacy derived values
    enemyHp,
    enemyMaxHp,
    enemyAp,
    enemyMaxAp,
    enemyGuard,
    enemyBuffs,
    enemyEnergy,
    setEnemyEnergy,

    // Character objects
    playerChar,
    enemyChar,

    // Reset
    resetForNextEnemy,
  };
}
