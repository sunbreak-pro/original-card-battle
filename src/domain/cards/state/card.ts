import type { Card, MasteryLevel } from "../type/cardType";
import { MASTERY_THRESHOLDS } from "../type/cardType";

export function calculateEffectivePower(card: Card): number {
  if (!card.baseDamage) return 0;
  let damage = card.baseDamage;
  const masteryBonus = 1 + card.masteryLevel * 0.1;
  damage *= (masteryBonus + card.gemLevel * 0.5);
  return Math.round(damage);
}

export function calculateMasteryLevel(useCount: number): MasteryLevel {
  if (useCount >= MASTERY_THRESHOLDS[3]) return 3;
  if (useCount >= MASTERY_THRESHOLDS[2]) return 2;
  if (useCount >= MASTERY_THRESHOLDS[1]) return 1;
  return 0;
}

export function canBecomeTalent(card: Card): boolean {
  return card.useCount >= MASTERY_THRESHOLDS[3] && card.masteryLevel < 3;
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
  isPlayerTurn: boolean
): boolean {
  return isPlayerTurn && card.cost <= currentEnergy;
}

import type { BuffDebuffState } from "../../battles/type/baffType";
import { createBuffState } from "../../battles/type/baffType";

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
}
