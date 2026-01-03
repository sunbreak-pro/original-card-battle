/**
 * 剣士固有アビリティ: 剣気システム
 *
 * 【剣気ゲージ】
 * 最大値: 10
 * 初期値: 0
 *
 * 【剣気の蓄積】
 * 物理攻撃カード使用時、剣気が蓄積
 * - 0コスト: +1剣気
 * - 1コスト: +1剣気
 * - 2コスト: +2 or +3剣気
 * - 3コスト以上: +3剣気
 * - 剣気蓄積専用カード: +4剣気
 *
 * 【剣気の効果】（ナーフ後）
 * - 物理攻撃ダメージ = 基本威力 + (剣気×2)
 * - 剣気5以上: クリティカル率+20%
 * - 剣気8以上: 物理攻撃に貫通+30%
 * - 剣気10(最大): 次の物理攻撃が確定クリティカル+貫通50%
 */

export interface SwordEnergyState {
  current: number;
  max: number;
}

export const SWORD_ENERGY_MAX = 10;


export function createInitialSwordEnergy(): SwordEnergyState {
  return {
    current: 0,
    max: SWORD_ENERGY_MAX,
  };
}
export function calculateSwordEnergyGain(
  cost: number,
  isSwordEnergyCard: boolean = false,
  customGain?: number
): number {
  if (customGain !== undefined) {
    return customGain;
  }

  if (isSwordEnergyCard) {
    return 4;
  }

  if (cost === 0) return 1;
  if (cost === 1) return 1;
  if (cost === 2) return 2; 
  return 3; 
}

export function addSwordEnergy(
  state: SwordEnergyState,
  amount: number
): SwordEnergyState {
  return {
    ...state,
    current: Math.min(state.max, state.current + amount),
  };
}

export function consumeSwordEnergy(
  state: SwordEnergyState,
  amount: number
): { newState: SwordEnergyState; consumed: number } {
  const consumed = Math.min(state.current, amount);
  return {
    newState: {
      ...state,
      current: state.current - consumed,
    },
    consumed,
  };
}

export function consumeAllSwordEnergy(
  state: SwordEnergyState
): { newState: SwordEnergyState; consumed: number } {
  const consumed = state.current;
  return {
    newState: {
      ...state,
      current: 0,
    },
    consumed,
  };
}

export function calculateSwordEnergyCritBonus(swordEnergy: number): number {
  if (swordEnergy >= 10) return 1.0; 
  if (swordEnergy >= 5) return 0.2; 
  return 0;
}

export function calculateSwordEnergyPenetration(swordEnergy: number): number {
  if (swordEnergy >= 10) return 0.5;
  if (swordEnergy >= 8) return 0.3; 
  return 0;
}

export interface SwordEnergyEffects {
  critBonus: number;
  penetration: number;
  isMaxEnergy: boolean;
}

export function getSwordEnergyEffects(swordEnergy: number): SwordEnergyEffects {
  return {
    critBonus: calculateSwordEnergyCritBonus(swordEnergy),
    penetration: calculateSwordEnergyPenetration(swordEnergy),
    isMaxEnergy: swordEnergy >= SWORD_ENERGY_MAX,
  };
}
export function calculateSwordEnergyConsumeDamage(
  baseDamage: number,
  consumedEnergy: number,
  multiplier: number = 10
): number {
  return baseDamage + (consumedEnergy * multiplier);
}
