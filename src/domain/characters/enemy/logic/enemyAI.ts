import type { EnemyDefinition, EnemyAction, EnemyAIPattern } from '@/types/characterTypes';
import { DEPTH1_ENEMIES } from "../data/enemyDepth1";
import { DEPTH2_ENEMIES } from "../data/enemyDepth2";
import { DEPTH3_ENEMIES } from "../data/enemyDepth3";
import { DEPTH4_ENEMIES } from "../data/enemyDepth4";
import { DEPTH5_ENEMIES } from "../data/enemyDepth5";
import type { Card, CardTag } from '@/types/cardTypes';

/**
 * Minimal interface for determining enemy actions
 * Works with both Enemy and EnemyDefinition
 */
interface EnemyAISource {
  aiPatterns: EnemyAIPattern[];
}

export function determineEnemyAction(
  enemy: EnemyAISource,
  currentHp: number,
  maxHp: number,
  turnNumber: number,
  _remainingEnergy?: number
): EnemyAction {
  const validPatterns = enemy.aiPatterns?.filter((pattern) => {
    const turnMatch = pattern.phaseNumber === 0 || pattern.phaseNumber === turnNumber;
    const conditionMatch = !pattern.condition || pattern.condition(currentHp, maxHp);

    return turnMatch && conditionMatch;
  });

  if (validPatterns.length === 0) {
    return {
      name: "基本攻撃",
      type: "attack",
      baseDamage: 5,
    };
  }

  let totalProbability = 0;
  const patternsWithProb = validPatterns.map((pattern) => {
    const prob = pattern.probability ?? 1.0;
    totalProbability += prob;
    return { pattern, probability: prob };
  });

  const random = Math.random() * totalProbability;
  let cumulative = 0;

  for (const { pattern, probability } of patternsWithProb) {
    cumulative += probability;
    if (random <= cumulative) {
      return pattern.action;
    }
  }
  return validPatterns[0].action;
}
export function enemyAction(action: EnemyAction): Card {
  // Determine tag based on action type
  const tags: CardTag[] = action.baseDamage && action.baseDamage > 0 ? ["attack"] : ["skill"];

  return {
    id: `enemy_action_${action.name}`,
    cardTypeId: `enemy_action_${action.name}`,
    name: action.name,
    description: "",
    characterClass: "common",
    cost: 0,
    category: "atk",
    baseDamage: action.baseDamage,
    tags,
    element: "slash",
    rarity: "common",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    applyEnemyDebuff: action.applyDebuffs?.map((debuff) => ({
      name: debuff.name,
      stacks: debuff.stacks,
      duration: debuff.duration,
      value: debuff.value,
      isPermanent: debuff.isPermanent,
    })),
  };
}

export function selectRandomEnemy(
  depth: number,
  encounterType: "normal" | "group" | "boss" = "normal"
): { enemies: EnemyDefinition[]; isBoss: boolean } {
  const depthEnemies: Record<number, typeof DEPTH1_ENEMIES> = {
    1: DEPTH1_ENEMIES,
    2: DEPTH2_ENEMIES,
    3: DEPTH3_ENEMIES,
    4: DEPTH4_ENEMIES,
    5: DEPTH5_ENEMIES,
  };

  const enemies = depthEnemies[depth];
  if (!enemies) {
    throw new Error(`Depth ${depth} enemies are not defined`);
  }

  switch (encounterType) {
    case "normal": {
      const enemy = enemies.normal[
        Math.floor(Math.random() * enemies.normal.length)
      ];
      return { enemies: [enemy], isBoss: false };
    }

    case "group": {
      const group =
        enemies.groups[
        Math.floor(Math.random() * enemies.groups.length)
        ];
      const groupEnemies = Array(group.count).fill(group.enemy);
      return { enemies: groupEnemies, isBoss: false };
    }

    case "boss": {
      return { enemies: [enemies.boss], isBoss: true };
    }

    default:
      return { enemies: [enemies.normal[0]], isBoss: false };
  }
}
