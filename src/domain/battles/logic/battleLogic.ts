export const applyHeal = (
  healAmount: number,
  currentHp: number,
  maxHp: number
): number => {
  return Math.min(maxHp, currentHp + healAmount);
};
