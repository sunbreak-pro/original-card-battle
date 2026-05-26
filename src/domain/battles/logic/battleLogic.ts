export const applyHeal = (
  healAmount: number,
  currentHp: number,
  maxHp: number
): number => {
  return Math.min(maxHp, currentHp + healAmount);
};

export const checkBattleEnd = (
  playerHp: number,
  enemyHp: number
): "ongoing" | "victory" | "defeat" => {
  if (enemyHp <= 0) return "victory";
  if (playerHp <= 0) return "defeat";
  return "ongoing";
};
