/**
 * Enemy AI Hook
 *
 * Handles enemy AI decision-making and action execution:
 * - Action determination based on HP thresholds and AI patterns
 * - Action execution with damage calculation
 * - Next action preview for UI display
 *
 * This hook wraps the existing enemyAI and enemyActionExecution logic
 * to provide a hook-based interface for React components.
 */

import { useCallback } from "react";
import type { Enemy, EnemyAction } from "../../characters/type/enemyType";
import type { EnemyBattleState } from "../type/battleStateType";
import type { BuffDebuffMap } from "../type/baffType";
import type { Player } from "../../characters/type/playerTypes";

// Enemy AI logic
import { determineEnemyAction } from "../../characters/enemy/logic/enemyAI";
import {
  executeEnemyActions,
  previewEnemyActions,
} from "../../characters/enemy/logic/enemyActionExecution";

// Battle execution
import {
  calculateEnemyAttackDamage,
  applyEnemyDebuffsToPlayer,
} from "../execution/enemyPhaseExecution";

// Bleed damage
import { calculateBleedDamage } from "../logic/bleedDamage";

// ============================================================================
// Types
// ============================================================================

/**
 * Context for enemy action execution
 */
export interface EnemyActionContext {
  enemy: Enemy;
  enemyHp: number;
  enemyMaxHp: number;
  enemyEnergy: number;
  enemyBuffs: BuffDebuffMap;
  enemyTurnCount: number;

  playerHp: number;
  playerBuffs: BuffDebuffMap;
  playerChar: Player;
  enemyChar: Enemy;
}

/**
 * State setters for enemy actions
 */
export interface EnemyActionSetters {
  setEnemyGuard: (updater: number | ((prev: number) => number)) => void;
  setEnemyHp: (updater: number | ((prev: number) => number)) => void;
  setPlayerGuard: (updater: number | ((prev: number) => number)) => void;
  setPlayerAp: (updater: number | ((prev: number) => number)) => void;
  setPlayerHp: (updater: number | ((prev: number) => number)) => void;
  setPlayerBuffs: (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => void;
  setBattleStats: (
    updater: (prev: { damageDealt: number; damageTaken: number }) => {
      damageDealt: number;
      damageTaken: number;
    }
  ) => void;
}

/**
 * Animation handlers for enemy actions
 */
export interface EnemyActionAnimations {
  showDamageEffect: (target: HTMLElement, amount: number, isCritical: boolean) => void;
}

/**
 * Return type for useEnemyAI hook
 */
export interface UseEnemyAIReturn {
  /**
   * Determine the next action for an enemy
   */
  determineAction: (
    enemy: Enemy,
    hp: number,
    maxHp: number,
    turn: number,
    energy: number
  ) => EnemyAction;

  /**
   * Execute all enemy actions for the current turn
   */
  executeAllActions: (
    context: EnemyActionContext,
    setters: EnemyActionSetters,
    animations: EnemyActionAnimations,
    playerRef: HTMLElement | null,
    getTargetEnemyRef: () => HTMLElement | null,
    checkBattleEnd: () => boolean
  ) => Promise<void>;

  /**
   * Get preview of next enemy actions for UI display
   */
  getNextActionsPreview: (
    enemy: Enemy,
    currentHp: number,
    nextTurn: number
  ) => EnemyAction[];

  /**
   * Get the next action preview for a specific enemy state
   */
  getActionPreview: (enemyState: EnemyBattleState) => EnemyAction | null;
}

// ============================================================================
// Hook Implementation
// ============================================================================

/**
 * Enemy AI hook
 *
 * Provides enemy action determination and execution functionality.
 * This hook is stateless and operates on the provided enemy state.
 */
export function useEnemyAI(): UseEnemyAIReturn {
  // ========================================================================
  // Determine Action
  // ========================================================================

  const determineAction = useCallback(
    (
      enemy: Enemy,
      hp: number,
      maxHp: number,
      turn: number,
      energy: number
    ): EnemyAction => {
      return determineEnemyAction(enemy, hp, maxHp, turn, energy);
    },
    []
  );

  // ========================================================================
  // Execute All Actions
  // ========================================================================

  const executeAllActions = useCallback(
    async (
      context: EnemyActionContext,
      setters: EnemyActionSetters,
      animations: EnemyActionAnimations,
      playerRef: HTMLElement | null,
      getTargetEnemyRef: () => HTMLElement | null,
      checkBattleEnd: () => boolean
    ): Promise<void> => {
      const {
        enemy,
        enemyHp,
        enemyMaxHp,
        enemyEnergy,
        enemyBuffs,
        enemyTurnCount,
        playerChar,
        enemyChar,
      } = context;

      const onExecuteAction = async (action: EnemyAction) => {
        // Guard-only action
        if (action.guardGain && action.guardGain > 0 && !action.baseDamage) {
          setters.setEnemyGuard((g) => g + action.guardGain!);
          return;
        }

        // Attack action
        const hitCount = action.hitCount || 1;
        for (let i = 0; i < hitCount; i++) {
          const attackResult = calculateEnemyAttackDamage(
            enemyChar,
            playerChar,
            action
          );

          setters.setPlayerGuard((g) => Math.max(0, g - attackResult.guardDamage));
          setters.setPlayerAp((a) => Math.max(0, a - attackResult.apDamage));
          setters.setPlayerHp((h) => Math.max(0, h - attackResult.hpDamage));

          if (playerRef) {
            animations.showDamageEffect(playerRef, attackResult.totalDamage, false);
          }

          // Reflect damage
          if (attackResult.reflectDamage > 0) {
            setters.setEnemyHp((h) => Math.max(0, h - attackResult.reflectDamage));
            const reflectTarget = getTargetEnemyRef();
            if (reflectTarget) {
              animations.showDamageEffect(
                reflectTarget,
                attackResult.reflectDamage,
                false
              );
            }
          }

          // Update battle stats
          setters.setBattleStats((stats) => ({
            ...stats,
            damageTaken: stats.damageTaken + attackResult.totalDamage,
          }));

          if (i < hitCount - 1) {
            await new Promise((r) => setTimeout(r, 500));
          }
        }

        // Apply debuffs
        if (action.applyDebuffs && action.applyDebuffs.length > 0) {
          const newBuffs = applyEnemyDebuffsToPlayer(
            context.playerBuffs,
            action.applyDebuffs
          );
          setters.setPlayerBuffs(newBuffs);
        }

        // Bleed damage to enemy
        const bleedDamage = calculateBleedDamage(enemyMaxHp, enemyBuffs);
        if (bleedDamage > 0) {
          setters.setEnemyHp((h) => Math.max(0, h - bleedDamage));
          const bleedTarget = getTargetEnemyRef();
          if (bleedTarget) {
            animations.showDamageEffect(bleedTarget, bleedDamage, false);
          }
          await new Promise((r) => setTimeout(r, 300));
        }
      };

      await executeEnemyActions(
        enemy,
        enemyHp,
        enemyMaxHp,
        enemyTurnCount,
        enemyEnergy,
        onExecuteAction,
        checkBattleEnd
      );
    },
    []
  );

  // ========================================================================
  // Get Next Actions Preview
  // ========================================================================

  const getNextActionsPreview = useCallback(
    (enemy: Enemy, currentHp: number, nextTurn: number): EnemyAction[] => {
      return previewEnemyActions(enemy, currentHp, nextTurn);
    },
    []
  );

  // ========================================================================
  // Get Action Preview for Enemy State
  // ========================================================================

  const getActionPreview = useCallback(
    (enemyState: EnemyBattleState): EnemyAction | null => {
      if (enemyState.hp <= 0) {
        return null;
      }

      const actions = previewEnemyActions(
        enemyState.enemy,
        enemyState.hp,
        (enemyState.turnCount ?? 0) + 1
      );

      return actions[0] || null;
    },
    []
  );

  // ========================================================================
  // Return
  // ========================================================================

  return {
    determineAction,
    executeAllActions,
    getNextActionsPreview,
    getActionPreview,
  };
}
