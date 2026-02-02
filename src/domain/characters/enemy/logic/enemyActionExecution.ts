/**
 * enemy action execution logic
 * Battle System Ver 4.0
 */

import type { EnemyAction, EnemyAIPattern } from '@/types/characterTypes';
import { determineEnemyAction } from "./enemyAI";

/**
 * Minimal interface for enemy action preview
 * Works with both Enemy and EnemyDefinition
 */
interface EnemyActionSource {
  actEnergy: number;
  aiPatterns: EnemyAIPattern[];
}

export async function executeEnemyActions(
  enemy: EnemyActionSource,
  enemyHp: number,
  enemyMaxHp: number,
  turn: number,
  enemyEnergy: number,
  onExecuteAction: (action: EnemyAction) => Promise<void>,
  checkBattleEnd: () => boolean
): Promise<void> {
  let remainingEnergy = enemyEnergy;
  const actionsToExecute: EnemyAction[] = [];

  while (remainingEnergy > 0) {
    const action = determineEnemyAction(
      enemy,
      enemyHp,
      enemyMaxHp,
      turn,
    );

    const actionCost = action.energyCost ?? 1;

    if (actionCost > remainingEnergy) {
      const fallbackAction = getFallbackAction(remainingEnergy, enemy);
      if (fallbackAction) {
        actionsToExecute.push(fallbackAction);
      }
      break;
    }

    actionsToExecute.push(action);
    remainingEnergy -= actionCost;
  }
  for (let i = 0; i < actionsToExecute.length; i++) {
    if (checkBattleEnd()) {
      break;
    }
    await onExecuteAction(actionsToExecute[i]);
    if (i < actionsToExecute.length - 1) {
      await delay(800);
    }
    if (checkBattleEnd()) {
      break;
    }
  }
}
function getFallbackAction(remainingEnergy: number, enemy: EnemyActionSource): EnemyAction | null {
  if (remainingEnergy >= 1) {
    // Scale fallback damage from the enemy's attack patterns
    const attackDamages = enemy.aiPatterns
      .filter(p => p.action.type === 'attack' && p.action.baseDamage > 0)
      .map(p => p.action.baseDamage);
    const avgDamage = attackDamages.length > 0
      ? attackDamages.reduce((a, b) => a + b, 0) / attackDamages.length
      : 5;

    return {
      name: "基本攻撃",
      type: "attack",
      baseDamage: Math.max(3, Math.floor(avgDamage * 0.5)),
      displayIcon: "⚔️",
      priority: 0,
      energyCost: 1,
    };
  }
  return null;
}
function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
export function previewEnemyActions(
  enemy: EnemyActionSource,
  currentHp: number,
  maxHp: number,
  nextTurn: number
): EnemyAction[] {
  const totalEnergy = enemy.actEnergy;
  const actions: EnemyAction[] = [];
  let remainingEnergy = totalEnergy;

  while (remainingEnergy > 0) {
    const action = determineEnemyAction(
      enemy,
      currentHp,
      maxHp,
      nextTurn,
    );

    const actionCost = action.energyCost ?? 1;

    if (actionCost > remainingEnergy) {
      const fallbackAction = getFallbackAction(remainingEnergy, enemy);
      if (fallbackAction) {
        actions.push(fallbackAction);
      }
      break;
    }

    actions.push(action);
    remainingEnergy -= actionCost;
  }

  return actions;
}
