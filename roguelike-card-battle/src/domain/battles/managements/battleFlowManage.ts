/**
 * Battle Flow Management Hook
 *
 * This file now re-exports the refactored useBattleOrchestrator as useBattle
 * for backward compatibility with existing consumers.
 *
 * The battle logic has been split into modular hooks:
 * - useBattleState: Player/Enemy state management
 * - useCardExecution: Card effect execution
 * - useEnemyAI: Enemy AI and action execution
 * - useClassAbility: Class-specific abilities (Sword Energy, etc.)
 * - useBattlePhase: Phase queue and turn management
 *
 * @see useBattleOrchestrator for the main implementation
 */

// Re-export the orchestrator as useBattle for backward compatibility
export { useBattleOrchestrator as useBattle } from "./useBattleOrchestrator";

// Re-export for backward compatibility
export { createEnemyState } from "../logic/enemyStateLogic";

// Re-export individual hooks for advanced usage
export { useBattleState, type UseBattleStateReturn, type PlayerState } from "./useBattleState";
export { useBattlePhase, type UseBattlePhaseReturn, type PhaseState } from "./useBattlePhase";
export { useCardExecution, type UseCardExecutionReturn } from "./useCardExecution";
export { useEnemyAI, type UseEnemyAIReturn } from "./useEnemyAI";
export {
  useClassAbility,
  useSwordEnergy,
  type UseClassAbilityReturn,
  type ClassAbilityUI,
} from "./useClassAbility";
