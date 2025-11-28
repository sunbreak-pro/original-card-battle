// ==========================================
// カードシステム型定義
// 設計書: blueprint/FINAL_INTEGRATED_SYSTEM_DESIGN.md
// ==========================================

// 基本型定義
export type Depth = 1 | 2 | 3 | 4 | 5;

export type CardCategory = 'physical' | 'magic' | 'defense' | 'heal';

export type DepthCurveType = 'shallow' | 'neutral' | 'deep' | 'madness' | 'adversity';

export type MasteryLevel = 0 | 1 | 2 | 3; // 0:初期 1:熟練 2:達人 3:才能

export type GemLevel = 0 | 1 | 2; // 0:通常 1:魔石 2:超魔石

export type Rarity = 'common' | 'rare' | 'epic' | 'legend';

// ==========================================
// カードインターフェース
// ==========================================
export interface Card {
  // 基本情報
  id: string;
  name: string;
  description: string;
  
  // コストとカテゴリ
  cost: number; // 0-5
  category: CardCategory;
  
  // 深度適正
  depthCurveType: DepthCurveType;
  
  // 熟練度システム
  useCount: number; // 使用回数
  masteryLevel: MasteryLevel;
  
  // 魔石強化
  gemLevel: GemLevel;
  
  // 効果値
  basePower?: number; // 基本威力
  effectivePower?: number; // 現在の実効威力（計算後）
  
  // メタ情報
  tags: string[]; // ['斬撃', '貫通', '連続', etc.]
  rarity: Rarity;
}

// ==========================================
// 深度適正カーブ定義
// ==========================================
export const DEPTH_CURVES: Record<DepthCurveType, [number, number, number, number, number]> = {
  shallow: [1.30, 1.10, 0.90, 0.70, 0.50], // 浅層型: 序盤強い
  neutral: [1.00, 1.00, 1.00, 1.00, 1.00], // 中立型: 安定
  deep: [0.60, 0.80, 1.10, 1.50, 2.00],    // 深層型: 後半爆発
  madness: [0.80, 1.00, 1.20, 1.60, 3.00], // 狂気型: 深淵で超強力
  adversity: [1.50, 1.20, 1.00, 0.80, 1.20], // 逆境型: 特殊
};

// ==========================================
// 魔力倍率定義
// ==========================================
export const MAGIC_MULTIPLIERS: Record<Depth, number> = {
  1: 1,
  2: 2,
  3: 4,
  4: 8,
  5: 16,
};

// ==========================================
// 熟練度閾値定義
// ==========================================
export const MASTERY_THRESHOLDS = {
  0: 0,   // 初期: 0-7回
  1: 8,   // 熟練: 8-15回 → 効果+20%
  2: 16,  // 達人: 16-23回 → 効果+40%
  3: 24,  // 才能: 24回以上 → 才能カード化
};

export const MASTERY_BONUSES: Record<MasteryLevel, number> = {
  0: 1.0,  // +0%
  1: 1.2,  // +20%
  2: 1.4,  // +40%
  3: 2.0,  // +100% (才能カード)
};

// ==========================================
// ヘルパー関数
// ==========================================

/**
 * 深度適正カーブから特定深度の倍率を取得
 */
export function getDepthEfficiency(curveType: DepthCurveType, depth: Depth): number {
  return DEPTH_CURVES[curveType][depth - 1];
}

/**
 * カードの実効威力を計算
 * @param card カード
 * @param currentDepth 現在の深度
 * @returns 実効威力
 */
export function calculateEffectivePower(card: Card, currentDepth: Depth): number {
  if (!card.basePower) return 0;
  
  // 基本威力
  let power = card.basePower;
  
  // 深度適正カーブを適用
  const depthEfficiency = getDepthEfficiency(card.depthCurveType, currentDepth);
  power *= depthEfficiency;
  
  // 魔法カードの場合は魔力倍率を適用
  if (card.category === 'magic') {
    power *= MAGIC_MULTIPLIERS[currentDepth];
  }
  
  // 熟練度ボーナスを適用
  const masteryBonus = MASTERY_BONUSES[card.masteryLevel];
  power *= masteryBonus;
  
  // 魔石強化ボーナス（+50%/段階）
  power *= (1 + card.gemLevel * 0.5);
  
  return Math.round(power);
}

/**
 * 使用回数から熟練度レベルを計算
 */
export function calculateMasteryLevel(useCount: number): MasteryLevel {
  if (useCount >= MASTERY_THRESHOLDS[3]) return 3;
  if (useCount >= MASTERY_THRESHOLDS[2]) return 2;
  if (useCount >= MASTERY_THRESHOLDS[1]) return 1;
  return 0;
}

/**
 * カードが才能化可能かチェック
 */
export function canBecomeTalent(card: Card): boolean {
  return card.useCount >= MASTERY_THRESHOLDS[3] && card.masteryLevel < 3;
}

/**
 * カードの使用回数をインクリメント
 */
export function incrementUseCount(card: Card): Card {
  const newUseCount = card.useCount + 1;
  const newMasteryLevel = calculateMasteryLevel(newUseCount);
  
  return {
    ...card,
    useCount: newUseCount,
    masteryLevel: newMasteryLevel,
  };
}

// ==========================================
// カードカテゴリ表示名
// ==========================================
export const CARD_CATEGORY_NAMES: Record<CardCategory, string> = {
  physical: 'Physical',
  magic: 'Magic',
  defense: 'Defense',
  heal: 'Heal',
};

// ==========================================
// レアリティカラー
// ==========================================
export const RARITY_COLORS: Record<Rarity, string> = {
  common: '#9ca3af',   // グレー
  rare: '#3b82f6',     // ブルー
  epic: '#a855f7',     // パープル
  legend: '#f59e0b',   // ゴールド
};

// ==========================================
// コスト分布（設計書より）
// ==========================================
export const COST_DISTRIBUTION = {
  0: '才能カードのみ',
  1: '42枚 (最多)',
  2: '37枚',
  3: '11枚',
  4: '3枚 (レジェンド級)',
  5: '希少',
} as const;
