import type { Card, Depth } from "../type/cardType.ts";
import { calculateEffectivePower } from "./card";
import type { BuffDebuffType } from "../type/baffType.ts";

/**
 * バフ/デバフ効果データ
 */
export interface BuffDebuffEffect {
  type: BuffDebuffType;
  stacks: number;
  duration: number;
  value: number;
}

/**
 * カード効果の適用結果
 */
export interface CardEffectResult {
  damageToEnemy?: number;
  shieldGain?: number;
  hpGain?: number;
  // バフ/デバフ効果
  enemyDebuffs?: BuffDebuffEffect[]; // 敵に付与するデバフ
  playerBuffs?: BuffDebuffEffect[]; // プレイヤーに付与するバフ
}

/**
 * カードの効果を計算
 * @param card プレイするカード
 * @param currentDepth 現在の深度
 * @returns カード効果の結果
 */
export const calculateCardEffect = (
  card: Card,
  currentDepth: Depth
): CardEffectResult => {
  const effectivePower = calculateEffectivePower(card, currentDepth);
  const result: CardEffectResult = {};

  // 基本効果
  switch (card.category) {
    case "physical":
    case "magic":
      // 攻撃カード：敵にダメージ
      result.damageToEnemy = effectivePower;
      break;

    case "defense":
      // 防御カード:シールド付与
      result.shieldGain = effectivePower;
      break;

    case "heal":
      // 回復カード：HP回復
      result.hpGain = effectivePower;
      break;
  }

  // バフ/デバフ効果を追加
  if (card.applyEnemyDebuff && card.applyEnemyDebuff.length > 0) {
    result.enemyDebuffs = card.applyEnemyDebuff.map((debuff) => ({
      type: debuff.type as BuffDebuffType,
      stacks: debuff.stacks,
      duration: debuff.duration,
      value: debuff.value,
    }));
  }

  if (card.applyPlayerBuff && card.applyPlayerBuff.length > 0) {
    result.playerBuffs = card.applyPlayerBuff.map((buff) => ({
      type: buff.type as BuffDebuffType,
      stacks: buff.stacks,
      duration: buff.duration,
      value: buff.value,
    }));
  }

  return result;
};

/**
 * カードプレイが可能かどうかを判定
 * @param card プレイしようとするカード
 * @param currentEnergy 現在のエナジー
 * @param isPlayerTurn プレイヤーのターンかどうか
 * @returns プレイ可能かどうか
 */
export const canPlayCard = (
  card: Card,
  currentEnergy: number,
  isPlayerTurn: boolean
): boolean => {
  return isPlayerTurn && card.cost <= currentEnergy;
};
