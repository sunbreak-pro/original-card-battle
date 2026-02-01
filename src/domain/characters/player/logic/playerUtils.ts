/**
 * Player Utility Functions
 *
 * Factory and helper functions for player state management.
 */

import type {
  Difficulty,
  LivesSystem,
  PlayerResources,
} from '@/types/characterTypes';
import type { MagicStones } from '@/types/itemTypes';
import type { ExplorationLimit, SanctuaryProgress } from '@/types/campTypes';

/** Maximum lives by difficulty level */
export const LIVES_BY_DIFFICULTY: Record<Difficulty, number> = {
  easy: 3,
  normal: 3,
  hard: 2,
};

/**
 * Create initial lives system based on difficulty
 */
export function createLivesSystem(difficulty: Difficulty): LivesSystem {
  const maxLives = LIVES_BY_DIFFICULTY[difficulty];
  return {
    maxLives,
    currentLives: maxLives,
  };
}

/**
 * Decrease lives by 1 (on death)
 */
export function decreaseLives(lives: LivesSystem): LivesSystem {
  return {
    ...lives,
    currentLives: Math.max(0, lives.currentLives - 1),
  };
}

/**
 * Check if game is over (no lives remaining)
 */
export function isGameOver(lives: LivesSystem): boolean {
  return lives.currentLives <= 0;
}

/**
 * Create empty magic stones object
 */
export function createEmptyMagicStones(): MagicStones {
  return {
    small: 0,
    medium: 0,
    large: 0,
    huge: 0,
  };
}

/**
 * Create default exploration limit
 */
export function createDefaultExplorationLimit(): ExplorationLimit {
  return {
    current: 0,
    max: 10,
  };
}

/**
 * Create default sanctuary progress
 */
export function createDefaultSanctuaryProgress(): SanctuaryProgress {
  return {
    currentRunSouls: 0,
    totalSouls: 0,
    unlockedNodes: [],
    explorationLimitBonus: 0,
  };
}

/**
 * Calculate total gold (baseCamp + exploration)
 */
export function calculateTotalGold(resources: PlayerResources): number {
  return resources.baseCampGold + resources.explorationGold;
}

/**
 * Check if player can afford a cost
 */
export function canAfford(resources: PlayerResources, cost: number): boolean {
  return calculateTotalGold(resources) >= cost;
}
