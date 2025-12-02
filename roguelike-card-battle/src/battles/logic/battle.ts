/**
 * 戦闘ロジック関連の処理
 */

/**
 * ダメージをエンティティに適用（シールド優先）
 * @param damage ダメージ量
 * @param currentHp 現在のHP
 * @param currentShield 現在のシールド
 * @returns 新しいHPとシールド
 */
export const applyDamage = (
  damage: number,
  currentHp: number,
  currentShield: number
): { newHp: number; newShield: number } => {
  let newShield = currentShield;
  let newHp = currentHp;

  if (currentShield > 0) {
    // シールドがある場合、シールドから先に減らす
    const remainingDamage = Math.max(0, damage - currentShield);
    newShield = Math.max(0, currentShield - damage);
    newHp = Math.max(0, currentHp - remainingDamage);
  } else {
    // シールドがない場合、直接HPを減らす
    newHp = Math.max(0, currentHp - damage);
  }

  return { newHp, newShield };
};

/**
 * 回復を適用（最大HPを超えない）
 * @param healAmount 回復量
 * @param currentHp 現在のHP
 * @param maxHp 最大HP
 * @returns 新しいHP
 */
export const applyHeal = (
  healAmount: number,
  currentHp: number,
  maxHp: number
): number => {
  return Math.min(maxHp, currentHp + healAmount);
};

/**
 * シールドを付与
 * @param shieldAmount シールド量
 * @param currentShield 現在のシールド
 * @returns 新しいシールド
 */
export const applyShield = (
  shieldAmount: number,
  currentShield: number
): number => {
  return currentShield + shieldAmount;
};

/**
 * 敵の行動を決定（簡易AI）
 * @returns ダメージ量
 */
export const determineEnemyAction = (): number => {
  // 簡易実装：8-12のランダムダメージ
  return Math.floor(Math.random() * 5) + 8;
};

/**
 * 戦闘終了判定
 * @param playerHp プレイヤーのHP
 * @param enemyHp 敵のHP
 * @returns 戦闘結果（"ongoing" | "victory" | "defeat"）
 */
export const checkBattleEnd = (
  playerHp: number,
  enemyHp: number
): "ongoing" | "victory" | "defeat" => {
  if (enemyHp <= 0) return "victory";
  if (playerHp <= 0) return "defeat";
  return "ongoing";
};
