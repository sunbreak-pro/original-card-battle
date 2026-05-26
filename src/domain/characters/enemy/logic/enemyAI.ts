import type {
  EnemyDefinition,
  EnemyAction,
  EnemyAIPattern,
  EncounterSize,
  DepthEnemyData,
  ElementType,
  EnemyActionType,
} from "@/types/characterTypes";
import { DEPTH1_ENEMIES } from "@/constants/data/characters/enemy/enemyDepth1";
import { DEPTH2_ENEMIES } from "@/constants/data/characters/enemy/enemyDepth2";
import { DEPTH3_ENEMIES } from "@/constants/data/characters/enemy/enemyDepth3";
import { DEPTH4_ENEMIES } from "@/constants/data/characters/enemy/enemyDepth4";
import { DEPTH5_ENEMIES } from "@/constants/data/characters/enemy/enemyDepth5";
import type { Card, CardTag } from "@/types/cardTypes";

/** Default element mapping based on action type */
const ACTION_TYPE_DEFAULT_ELEMENTS: Record<EnemyActionType, ElementType[]> = {
  attack: ["physics", "attack"],
  buff: ["buff"],
  debuff: ["debuff"],
  special: ["physics", "attack"],
};

/**
 * Minimal interface for determining enemy actions
 * Works with both Enemy and EnemyDefinition
 */
interface EnemyAISource {
  aiPatterns: EnemyAIPattern[];
}

/**
 * Enemy source that also carries a stable identity, required so the
 * resolved-action cache can key per enemy definition.
 */
interface IdentifiedEnemyAISource extends EnemyAISource {
  id: string;
}

// ============================================================
// V-ENM-02 fix: single-resolution shared action cache
// ============================================================
//
// Background: `determineEnemyAction` uses unseeded `Math.random()`. The
// preview path (NextEnemyActions / EnemyFrame) and the execute path each
// called it independently, so an enemy with multiple probabilistic
// patterns could show one action in the preview and perform another.
//
// Fix: resolve each (enemy id, hp, maxHp, turn, callIndex) exactly once and
// memoize it. Preview and execute then read the same resolved action. The
// probability distribution is preserved (the RNG still runs, just once per
// distinct slot). `callIndex` distinguishes the successive actions an enemy
// takes within the same phase (energy spend loop), so a multi-action enemy
// still gets a varied sequence while preview == execute.

const resolvedActionCache = new Map<string, EnemyAction>();

function actionCacheKey(
  enemyId: string,
  currentHp: number,
  maxHp: number,
  turnNumber: number,
  callIndex: number,
): string {
  return `${enemyId}|${currentHp}|${maxHp}|${turnNumber}|${callIndex}`;
}

/**
 * Resolve an enemy action deterministically across preview and execute.
 *
 * The first call for a given slot runs the RNG via `determineEnemyAction`
 * and caches the result; subsequent calls (e.g. the preview after the
 * execute already resolved it, or a re-render of EnemyFrame) return the
 * cached action so preview always matches execution.
 */
export function resolveEnemyAction(
  enemy: IdentifiedEnemyAISource,
  currentHp: number,
  maxHp: number,
  turnNumber: number,
  callIndex: number,
): EnemyAction {
  const key = actionCacheKey(enemy.id, currentHp, maxHp, turnNumber, callIndex);
  const cached = resolvedActionCache.get(key);
  if (cached) {
    return cached;
  }
  const action = determineEnemyAction(enemy, currentHp, maxHp, turnNumber);
  resolvedActionCache.set(key, action);
  return action;
}

/**
 * Clear the resolved-action cache. Call when a battle ends / resets so
 * stale slots from a previous battle cannot leak into a new one.
 */
export function clearResolvedActionCache(): void {
  resolvedActionCache.clear();
}

export function determineEnemyAction(
  enemy: EnemyAISource,
  currentHp: number,
  maxHp: number,
  turnNumber: number,
): EnemyAction {
  const validPatterns = enemy.aiPatterns?.filter((pattern) => {
    const turnMatch =
      pattern.phaseNumber === 0 || pattern.phaseNumber === turnNumber;
    const conditionMatch =
      !pattern.condition || pattern.condition(currentHp, maxHp);

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
  const tags: CardTag[] =
    action.baseDamage && action.baseDamage > 0 ? ["attack"] : ["skill"];
  // Use action-specific element if defined, otherwise fall back to action type defaults
  const element = action.element ?? ACTION_TYPE_DEFAULT_ELEMENTS[action.type];

  return {
    id: `enemy_action_${action.name}`,
    cardTypeId: `enemy_action_${action.name}`,
    name: action.name,
    description: "",
    characterClass: "swordsman", // Enemy actions use swordsman as default (no gameplay impact)
    cost: 0,
    baseDamage: action.baseDamage,
    tags,
    element,
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
  encounterSize: EncounterSize = "single",
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
      const pattern =
        data.single[Math.floor(Math.random() * data.single.length)];
      return { enemies: pattern.enemies, isBoss: false };
    }

    case "double": {
      const pattern =
        data.double[Math.floor(Math.random() * data.double.length)];
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
