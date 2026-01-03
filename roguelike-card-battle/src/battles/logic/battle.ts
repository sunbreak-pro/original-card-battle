export const applyDamage = (
  damage: number,
  currentHp: number,
  currentShield: number
): { newHp: number; newShield: number } => {
  let newShield = currentShield;
  let newHp = currentHp;

  if (currentShield > 0) {
    const remainingDamage = Math.max(0, damage - currentShield);
    newShield = Math.max(0, currentShield - damage);
    newHp = Math.max(0, currentHp - remainingDamage);
  } else {
    newHp = Math.max(0, currentHp - damage);
  }

  return { newHp, newShield };
};
export const applyHeal = (
  healAmount: number,
  currentHp: number,
  maxHp: number
): number => {
  return Math.min(maxHp, currentHp + healAmount);
};

export const applyShield = (
  shieldAmount: number,
  currentShield: number
): number => {
  return currentShield + shieldAmount;
};

export const checkBattleEnd = (
  playerHp: number,
  enemyHp: number
): "ongoing" | "victory" | "defeat" => {
  if (enemyHp <= 0) return "victory";
  if (playerHp <= 0) return "defeat";
  return "ongoing";
};
