/**
 * enemy action execution logic
 * Battle System Ver 4.0
 */

import type { Enemy, EnemyAction } from "../../Character/data/EnemyData";
import { determineEnemyAction } from "./enemyAI";

/**
 * @param enemy 
 * @param enemyHp
 * @param enemyMaxHp
 * @param turn 
 * @param enemyEnergy 
 * @param onExecuteAction
 * @param checkBattleEnd
 */
export async function executeEnemyActions(
  enemy: Enemy,
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
      remainingEnergy
    );

    const actionCost = action.energyCost ?? 1;

    if (actionCost > remainingEnergy) {
      const fallbackAction = getFallbackAction(remainingEnergy);
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
function getFallbackAction(remainingEnergy: number): EnemyAction | null {
  if (remainingEnergy >= 1) {
    return {
      name: "基本攻撃",
      type: "attack",
      baseDamage: 5,
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
  enemy: Enemy,
  currentHp: number,
  nextTurn: number
): EnemyAction[] {
  const totalEnergy = enemy.actEnergy;
  const actions: EnemyAction[] = [];
  let remainingEnergy = totalEnergy;

  while (remainingEnergy > 0) {
    const action = determineEnemyAction(
      enemy,
      currentHp,
      enemy.maxHp,
      nextTurn,
      remainingEnergy
    );

    const actionCost = action.energyCost ?? 1;

    if (actionCost > remainingEnergy) {
      const fallbackAction = getFallbackAction(remainingEnergy);
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
