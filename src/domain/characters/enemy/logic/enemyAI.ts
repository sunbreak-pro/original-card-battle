import type { EnemyDefinition, EnemyAction, EnemyAIPattern, EncounterSize, DepthEnemyData } from '@/types/characterTypes';
import { DEPTH1_ENEMIES } from "@/constants/data/characters/enemy/enemyDepth1";
import { DEPTH2_ENEMIES } from "@/constants/data/characters/enemy/enemyDepth2";
import { DEPTH3_ENEMIES } from "@/constants/data/characters/enemy/enemyDepth3";
import { DEPTH4_ENEMIES } from "@/constants/data/characters/enemy/enemyDepth4";
import { DEPTH5_ENEMIES } from "@/constants/data/characters/enemy/enemyDepth5";
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
    baseDamage: action.baseDamage,
    tags,
    element: ["physics", "attack"],
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
  encounterSize: EncounterSize = "single"
): { enemies: EnemyDefinition[]; isBoss: boolean } {
  const depthEnemies: Record<number, DepthEnemyData> = {
    1: DEPTH1_ENEMIES,
    2: DEPTH2_ENEMIES,
    3: DEPTH3_ENEMIES,
    4: DEPTH4_ENEMIES,
    5: DEPTH5_ENEMIES,
  };

  const data = depthEnemies[depth];
  if (!data) {
    throw new Error(`Depth ${depth} enemies are not defined`);
  }

  switch (encounterSize) {
    case "single": {
      const pattern = data.single[Math.floor(Math.random() * data.single.length)];
      return { enemies: pattern.enemies, isBoss: false };
    }

    case "double": {
      const pattern = data.double[Math.floor(Math.random() * data.double.length)];
      return { enemies: pattern.enemies, isBoss: false };
    }

    case "three": {
      const pattern = data.three[Math.floor(Math.random() * data.three.length)];
      return { enemies: pattern.enemies, isBoss: false };
    }

    case "boss": {
      return { enemies: [data.boss], isBoss: true };
    }

    default:
      return { enemies: data.single[0].enemies, isBoss: false };
  }
}
