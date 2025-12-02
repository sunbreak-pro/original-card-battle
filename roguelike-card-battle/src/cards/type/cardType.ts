// ==========================================
// 型定義
// ==========================================
export type Depth = 1 | 2 | 3 | 4 | 5;

export type CardCategory = 'physical' | 'magic' | 'defense' | 'heal';

export type DepthCurveType = 'shallow' | 'neutral' | 'deep' | 'madness' | 'adversity';

export type MasteryLevel = 0 | 1 | 2 | 3;

export type GemLevel = 0 | 1 | 2;

export type Rarity = 'common' | 'rare' | 'epic' | 'legend';

export interface Card {
    id: string;
    name: string;
    description: string;
    cost: number;
    category: CardCategory;
    depthCurveType: DepthCurveType;
    useCount: number;
    masteryLevel: MasteryLevel;
    gemLevel: GemLevel;
    basePower?: number;
    effectivePower?: number;
    talentProgress?: number;
    talentThreshold?: number;
    applyEnemyDebuff?: {
        type: string;
        stacks: number;
        duration: number;
        value: number;
    }[];
    applyPlayerBuff?: {
        type: string;
        stacks: number;
        duration: number;
        value: number;
    }[];
    tags: string[];
    rarity: Rarity;
}

// ==========================================
// 定数
// ==========================================
export const DEPTH_CURVES: Record<DepthCurveType, [number, number, number, number, number]> = {
    shallow: [1.30, 1.10, 0.90, 0.70, 0.50],
    neutral: [1.00, 1.00, 1.00, 1.00, 1.00],
    deep: [0.60, 0.80, 1.10, 1.50, 2.00],
    madness: [0.80, 1.00, 1.20, 1.60, 3.00],
    adversity: [1.50, 1.20, 1.00, 0.80, 1.20],
};

export const MAGIC_MULTIPLIERS: Record<Depth, number> = {
    1: 1,
    2: 2,
    3: 4,
    4: 8,
    5: 16,
};

export const MASTERY_THRESHOLDS = {
    0: 0,
    1: 8,
    2: 16,
    3: 24,
};

export const MASTERY_BONUSES: Record<MasteryLevel, number> = {
    0: 1.0,
    1: 1.2,
    2: 1.4,
    3: 2.0,
};

export const CARD_CATEGORY_NAMES: Record<CardCategory, string> = {
    physical: 'Physical',
    magic: 'Magic',
    defense: 'Defense',
    heal: 'Heal',
};

export const RARITY_COLORS: Record<Rarity, string> = {
    common: '#9ca3af',
    rare: '#3b82f6',
    epic: '#a855f7',
    legend: '#f59e0b',
};