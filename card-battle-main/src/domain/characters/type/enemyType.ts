/**
 * Enemy Type Definitions
 *
 * This file contains all enemy-related type definitions.
 * The new architecture separates:
 * - EnemyDefinition: Master data (static, loaded from JSON)
 * - EnemyBattleState: Runtime battle state
 */

import type { BuffDebuffState } from "../../battles/type/baffType";

import type { BattleStats } from "./baseTypes";
import type React from "react";

// ============================================================
// Action Types
// ============================================================

/**
 * Enemy action types
 */
export type EnemyActionType = "attack" | "buff" | "debuff" | "special";

/**
 * Enemy action definition
 */
export interface EnemyAction {
  name: string;
  type: EnemyActionType;
  baseDamage: number;
  applyDebuffs?: BuffDebuffState[];
  applyBuffs?: BuffDebuffState[];
  guardGain?: number;
  hitCount?: number;
  displayIcon?: string;
  priority?: number;
  energyCost?: number;
}

/**
 * Enemy AI pattern definition
 */
export interface EnemyAIPattern {
  phaseNumber: number;
  condition?: (hp: number, maxHp: number) => boolean;
  action: EnemyAction;
  probability?: number;
}

// ============================================================
// NEW ARCHITECTURE - Enemy Types (Phase 1 Refactoring)
// ============================================================

/**
 * Enemy Definition (Master Data)
 *
 * Static data loaded from JSON or defined in code.
 * This represents the "template" for an enemy type.
 */
export interface EnemyDefinition {
  /** Unique enemy type ID */
  id: string;
  /** Display name (English) */
  name: string;
  /** Display name (Japanese) */
  nameJa: string;
  /** Enemy description */
  description: string;

  // Base stats
  /** Base maximum HP */
  baseMaxHp: number;
  /** Base maximum AP */
  baseMaxAp: number;
  /** Base speed */
  baseSpeed: number;
  /** Whether enemy starts with guard */
  startingGuard: boolean;

  // AI
  /** Action energy per turn */
  actEnergy: number;
  /** AI behavior patterns */
  aiPatterns: EnemyAIPattern[];

  // Display
  /** Path to enemy image */
  imagePath?: string;
}

/**
 * Enemy Battle State
 *
 * Runtime state for an enemy during battle.
 * Extends BattleStats with enemy-specific properties.
 */
export interface EnemyBattleState extends BattleStats {
  /** Instance ID (unique per battle instance) */
  instanceId: string;
  /** Reference to EnemyDefinition ID */
  definitionId: string;
  /** Cached EnemyDefinition for quick access */
  definition: EnemyDefinition;

  /** Current action energy */
  energy: number;
  /** AI phase counter */
  phaseCount: number;
  /** Turn counter for this enemy */
  turnCount: number;

  /** Reference to DOM element for animations */
  ref: React.RefObject<HTMLDivElement | null>;
}

// ============================================================
// HELPER FUNCTIONS
// ============================================================

/**
 * Generate unique enemy instance ID
 */
export function generateEnemyInstanceId(definitionId: string): string {
  return `${definitionId}_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
}

/**
 * Check if enemy is alive
 */
export function isEnemyAlive(enemy: EnemyBattleState): boolean {
  return enemy.hp > 0;
}

/**
 * Get enemy HP percentage
 */
export function getEnemyHpPercent(enemy: EnemyBattleState): number {
  return (enemy.hp / enemy.maxHp) * 100;
}
