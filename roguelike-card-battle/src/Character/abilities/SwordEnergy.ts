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
 * 【剣気の効果】
 * - 物理攻撃ダメージ = 基本威力 + (剣気×5)
 * - 剣気5以上: クリティカル率+20%
 * - 剣気8以上: 物理攻撃に貫通+30%
 * - 剣気10(最大): 次の物理攻撃が確定クリティカル+貫通50%
 */

export interface SwordEnergyState {
  current: number;
  max: number;
}

export const SWORD_ENERGY_MAX = 10;
export const SWORD_ENERGY_DAMAGE_MULTIPLIER = 5; // 剣気×5のダメージ加算

/**
 * 剣気システムの初期状態
 */
export function createInitialSwordEnergy(): SwordEnergyState {
  return {
    current: 0,
    max: SWORD_ENERGY_MAX,
  };
}

/**
 * カードコストに基づく剣気蓄積量を計算
 * @param cost カードコスト
 * @param isSwordEnergyCard 剣気蓄積専用カードかどうか
 * @param customGain カード固有の剣気蓄積量（設定されている場合）
 */
export function calculateSwordEnergyGain(
  cost: number,
  isSwordEnergyCard: boolean = false,
  customGain?: number
): number {
  // カード固有の剣気蓄積量が設定されている場合はそれを使用
  if (customGain !== undefined) {
    return customGain;
  }

  // 剣気蓄積専用カード
  if (isSwordEnergyCard) {
    return 4;
  }

  // コストに応じた蓄積量
  if (cost === 0) return 1;
  if (cost === 1) return 1;
  if (cost === 2) return 2; // 2 or 3、カードごとに設定
  return 3; // 3コスト以上
}

/**
 * 剣気を加算（最大値を超えない）
 */
export function addSwordEnergy(
  state: SwordEnergyState,
  amount: number
): SwordEnergyState {
  return {
    ...state,
    current: Math.min(state.max, state.current + amount),
  };
}

/**
 * 剣気を消費
 */
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

/**
 * 剣気を全消費
 */
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

/**
 * 剣気によるダメージボーナスを計算
 */
export function calculateSwordEnergyDamageBonus(swordEnergy: number): number {
  return swordEnergy * SWORD_ENERGY_DAMAGE_MULTIPLIER;
}

/**
 * 剣気によるクリティカル率ボーナスを計算
 */
export function calculateSwordEnergyCritBonus(swordEnergy: number): number {
  if (swordEnergy >= 10) return 1.0; // 確定クリティカル
  if (swordEnergy >= 5) return 0.2; // +20%
  return 0;
}

/**
 * 剣気による貫通率ボーナスを計算
 */
export function calculateSwordEnergyPenetration(swordEnergy: number): number {
  if (swordEnergy >= 10) return 0.5; // +50%
  if (swordEnergy >= 8) return 0.3; // +30%
  return 0;
}

/**
 * 剣気の効果をまとめて取得
 */
export interface SwordEnergyEffects {
  damageBonus: number;
  critBonus: number;
  penetration: number;
  isMaxEnergy: boolean;
}

export function getSwordEnergyEffects(swordEnergy: number): SwordEnergyEffects {
  return {
    damageBonus: calculateSwordEnergyDamageBonus(swordEnergy),
    critBonus: calculateSwordEnergyCritBonus(swordEnergy),
    penetration: calculateSwordEnergyPenetration(swordEnergy),
    isMaxEnergy: swordEnergy >= SWORD_ENERGY_MAX,
  };
}

/**
 * 剣気消費技のダメージ計算
 * @param baseDamage 基本威力
 * @param consumedEnergy 消費した剣気
 * @param multiplier 剣気ダメージ倍率（カードごとに異なる）
 */
export function calculateSwordEnergyConsumeDamage(
  baseDamage: number,
  consumedEnergy: number,
  multiplier: number = 10
): number {
  return baseDamage + (consumedEnergy * multiplier);
}
