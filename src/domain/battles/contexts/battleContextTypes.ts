// Battle Context Type Definitions (B1.1)
// Defines the interfaces for the three split battle contexts

import type { RefObject } from "react";
import type { Card } from '@/types/cardTypes';
import type { SwordEnergyState } from '@/types/characterTypes';
import type { BuffDebuffMap } from '@/types/battleTypes';
import type { EnemyBattleState, EnemyDefinition, EnemyAction } from '@/types/characterTypes';
import type { CharacterClass } from '@/types/characterTypes';
import type { PhaseQueue } from '@/types/battleTypes';

// ========================================================================
// Immutable Snapshot Types (for cross-context communication)
// ========================================================================

/**
 * Read-only snapshot of player battle stats for damage calculation
 * Passed to EnemyBattleContext for enemy phase resolution
 */
export interface PlayerBattleStats {
  readonly hp: number;
  readonly maxHp: number;
  readonly ap: number;
  readonly maxAp: number;
  readonly guard: number;
  readonly buffs: BuffDebuffMap;
  readonly energy: number;
  readonly maxEnergy: number;
  readonly playerClass: CharacterClass;
}

/**
 * Read-only snapshot of enemy battle stats for damage calculation
 * Passed to PlayerBattleContext for player phase resolution
 */
export interface EnemyBattleStats {
  readonly hp: number;
  readonly maxHp: number;
  readonly ap: number;
  readonly maxAp: number;
  readonly guard: number;
  readonly buffs: BuffDebuffMap;
  readonly energy: number;
}

// ========================================================================
// PlayerBattleContext
// ========================================================================

/**
 * PlayerBattleContext value
 * Manages player state, deck, sword energy, and card execution
 */
export interface PlayerBattleContextValue {
  // Player identity
  playerName: string;
  playerClass: CharacterClass;
  playerRef: RefObject<HTMLDivElement | null>;

  // Player combat stats
  playerHp: number;
  playerMaxHp: number;
  playerAp: number;
  playerMaxAp: number;
  playerGuard: number;
  playerBuffs: BuffDebuffMap;
  playerEnergy: number;
  maxEnergy: number;

  // Sword energy (class ability)
  swordEnergy: SwordEnergyState;

  // Deck state
  hand: Card[];
  drawPile: Card[];
  discardPile: Card[];

  // Card animation state
  isNewCard: (cardId: string) => boolean;
  getDiscardingCards: () => Card[];

  // Card play action
  handleCardPlay: (card: Card, cardElement?: HTMLElement) => Promise<void>;

  // Modal controls for pile viewing
  openedPileType: "draw" | "discard" | null;
  openDrawPile: () => void;
  openDiscardPile: () => void;
  closePileModal: () => void;

  // Stats snapshot for cross-context communication
  playerBattleStats: PlayerBattleStats;
}

// ========================================================================
// EnemyBattleContext
// ========================================================================

/**
 * EnemyBattleContext value
 * Manages enemy state, AI, and enemy actions
 */
export interface EnemyBattleContextValue {
  // Enemy state
  currentEnemy: EnemyBattleState | null;
  enemies: EnemyBattleState[];
  aliveEnemies: EnemyBattleState[];

  // Enemy combat stats (derived from currentEnemy)
  enemyHp: number;
  enemyMaxHp: number;
  enemyAp: number;
  enemyMaxAp: number;
  enemyGuard: number;
  enemyBuffs: BuffDebuffMap;
  enemyEnergy: number;

  // Enemy actions preview
  nextEnemyActions: EnemyAction[];

  // Enemy ref accessor
  getTargetEnemyRef: () => HTMLDivElement | null;

  // Enemy state mutation
  updateEnemy: (
    index: number,
    updater: (state: EnemyBattleState) => Partial<EnemyBattleState>
  ) => void;
  updateAllEnemies: (
    updater: (state: EnemyBattleState) => Partial<EnemyBattleState>
  ) => void;

  // Stats snapshot for cross-context communication
  enemyBattleStats: EnemyBattleStats;
}

// ========================================================================
// BattleSessionContext
// ========================================================================

/**
 * BattleSessionContext value
 * Manages phase flow, turn order, battle result, and orchestration
 */
export interface BattleSessionContextValue {
  // Phase state
  phaseCount: number;
  isPlayerPhase: boolean;
  playerNowSpeed: number;
  enemyNowSpeed: number;
  phaseQueue: PhaseQueue | null;
  currentPhaseIndex: number;

  // Turn transition
  turnMessage: string;
  showTurnMessage: boolean;

  // Battle flow actions
  handleEndPhase: () => void;
  initializeBattle: () => Promise<void>;
  resetForNextEnemy: (nextEnemy: EnemyDefinition | EnemyDefinition[]) => void;

  // Battle result
  battleResult: "ongoing" | "victory" | "defeat";
  battleStats: {
    damageDealt: number;
    damageTaken: number;
  };
}
