/**
 * 敵のAIシステム
 * 敵の行動を決定し、実行する
 */

import type { Enemy, EnemyAction } from "../../Character/data/EnemyData";
import { DEPTH1_ENEMIES } from "../../Character/data/EnemyData";
import type { Card } from "../../cards/type/cardType";

/**
 * 敵の行動を決定する（Ver 4.0対応）
 * @param enemy 敵データ
 * @param currentHp 現在のHP
 * @param maxHp 最大HP
 * @param turnNumber 現在のターン番号
 * @param remainingEnergy 残りエナジー（将来的にAIで使用可能）
 * @returns 実行する行動
 */
export function determineEnemyAction(
  enemy: Enemy,
  currentHp: number,
  maxHp: number,
  turnNumber: number,
  remainingEnergy: number
): EnemyAction {
  // ターン番号に一致し、条件を満たすパターンを抽出
  const validPatterns = enemy.aiPatterns.filter((pattern) => {
    // ターン番号チェック（0は全ターン）
    const turnMatch = pattern.turnNumber === 0 || pattern.turnNumber === turnNumber;

    // 条件チェック（条件がない場合はtrue）
    const conditionMatch = !pattern.condition || pattern.condition(currentHp, maxHp);

    return turnMatch && conditionMatch;
  });

  if (validPatterns.length === 0) {
    // パターンが見つからない場合、デフォルト行動
    return {
      name: "基本攻撃",
      type: "attack",
      baseDamage: 5,
    };
  }

  // 確率を考慮してパターンを選択
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

  // フォールバック
  return validPatterns[0].action;
}

/**
 * 敵の行動をCard形式に変換
 * @param action 敵の行動
 * @returns Cardオブジェクト
 */
export function enemyActionToCard(action: EnemyAction): Card {
  return {
    id: `enemy_action_${action.name}`,
    cardTypeId: `enemy_action_${action.name}`,
    name: action.name,
    description: "",
    cost: 0,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: action.baseDamage,
    tags: [],
    rarity: "common",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    applyEnemyDebuff: action.applyDebuffs?.map((debuff) => ({
      type: debuff.type,
      stacks: debuff.stacks,
      duration: debuff.duration,
      value: debuff.value,
    })),
  };
}

/**
 * ランダムに敵を選択
 * @param depth 深度
 * @param encounterType 遭遇タイプ（normal, group, boss）
 * @returns 選択された敵（グループの場合は配列）
 */
export function selectRandomEnemy(
  depth: number,
  encounterType: "normal" | "group" | "boss" = "normal"
): { enemies: Enemy[]; isBoss: boolean } {
  if (depth !== 1) {
    // 現在は深度1のみ実装
    throw new Error(`Depth ${depth} enemies are not implemented yet`);
  }

  switch (encounterType) {
    case "normal": {
      const enemy = DEPTH1_ENEMIES.normal[
        Math.floor(Math.random() * DEPTH1_ENEMIES.normal.length)
      ];
      return { enemies: [enemy], isBoss: false };
    }

    case "group": {
      const group =
        DEPTH1_ENEMIES.groups[
          Math.floor(Math.random() * DEPTH1_ENEMIES.groups.length)
        ];
      const enemies = Array(group.count).fill(group.enemy);
      return { enemies, isBoss: false };
    }

    case "boss": {
      return { enemies: [DEPTH1_ENEMIES.boss], isBoss: true };
    }

    default:
      return { enemies: [DEPTH1_ENEMIES.normal[0]], isBoss: false };
  }
}
