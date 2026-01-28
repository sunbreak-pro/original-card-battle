import type { Card } from '@/types/cardTypes';
import { calculateEffectivePower } from "./card";
import type { BuffDebuffState } from '@/types/battleTypes';
import { createBuffState } from '../../battles/logic/buffLogic';

export interface CardEffectResult {
  damageToEnemy?: number;
  shieldGain?: number;
  hpGain?: number;
  enemyDebuffs?: BuffDebuffState[];
  playerBuffs?: BuffDebuffState[];
}

export const calculateCardEffect = (card: Card): CardEffectResult => {
  const effectivePower = calculateEffectivePower(card);
  const result: CardEffectResult = {};

  switch (card.category) {
    case "atk":
      result.damageToEnemy = effectivePower;
      break;
    case "def":
      result.shieldGain = effectivePower;
      break;
    case "heal":
      result.hpGain = effectivePower;
      break;
  }

  if (card.applyEnemyDebuff && card.applyEnemyDebuff.length > 0) {
    result.enemyDebuffs = card.applyEnemyDebuff.map((spec) =>
      createBuffState(spec, card.id)
    );
  }

  if (card.applyPlayerBuff && card.applyPlayerBuff.length > 0) {
    result.playerBuffs = card.applyPlayerBuff.map((spec) =>
      createBuffState(spec, card.id)
    );
  }

  return result;
};

/**
 * @param card
 * @param currentEnergy
 * @param isPlayerTurn
 * @returns
 */
export const canPlayCard = (
  card: Card,
  currentEnergy: number,
  isPlayerTurn: boolean
): boolean => {
  return isPlayerTurn && card.cost <= currentEnergy;
};
