import type { Card, MasteryLevel } from '@/types/cardTypes';
import { MASTERY_THRESHOLDS, MASTERY_BONUSES } from '@/constants/cardConstants';

export function calculateEffectivePower(card: Card): number {
  if (!card.baseDamage) return 0;
  let damage = card.baseDamage;
  const masteryBonus = MASTERY_BONUSES[card.masteryLevel] ?? 1.0;
  damage *= (masteryBonus + card.gemLevel * 0.5);
  return Math.round(damage);
}

export function calculateMasteryLevel(useCount: number): MasteryLevel {
  if (useCount >= MASTERY_THRESHOLDS[4]) return 4;
  if (useCount >= MASTERY_THRESHOLDS[3]) return 3;
  if (useCount >= MASTERY_THRESHOLDS[2]) return 2;
  if (useCount >= MASTERY_THRESHOLDS[1]) return 1;
  return 0;
}

export function canBecomeTalent(card: Card): boolean {
  return card.useCount >= MASTERY_THRESHOLDS[4] && card.masteryLevel < 4;
}

export function incrementUseCount(card: Card): Card {
  const newUseCount = card.useCount + 1;
  const newMasteryLevel = calculateMasteryLevel(newUseCount);

  return {
    ...card,
    useCount: newUseCount,
    masteryLevel: newMasteryLevel,
  };
}
export function canPlayCard(
  card: Card,
  currentEnergy: number,
  isPlayerTurn: boolean,
  hasActiveSummon?: boolean
): boolean {
  if (card.requiresSummon && !hasActiveSummon) return false;
  return isPlayerTurn && card.cost <= currentEnergy;
}

import type { BuffDebuffState } from '@/types/battleTypes';
import { createBuffState } from '../../battles/logic/buffLogic';

export interface CardEffectResult {
  damageToEnemy?: number;
  shieldGain?: number;
  hpGain?: number;
  enemyDebuffs?: BuffDebuffState[];
  playerBuffs?: BuffDebuffState[];
}

export function calculateCardEffect(
  card: Card,
): CardEffectResult {
  const effectivePower = calculateEffectivePower(card);
  const result: CardEffectResult = {};

  if (card.element.includes("attack")) {
    result.damageToEnemy = effectivePower;
  } else if (card.element.includes("guard")) {
    result.shieldGain = effectivePower;
  } else if (card.element.includes("heal")) {
    result.hpGain = effectivePower;
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
}
