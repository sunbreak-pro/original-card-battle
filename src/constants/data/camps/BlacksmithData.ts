// Blacksmith facility data constants
import type { ItemRarity } from "@/types/itemTypes";
import type { UpgradeCost, QualityUpConfig, QualityUpOption } from "@/types/campTypes";

/**
 * Upgrade costs by rarity and target level
 * Index 0 = Lv0→Lv1, Index 1 = Lv1→Lv2, Index 2 = Lv2→Lv3
 *
 * Magic stone values (gold equivalent):
 * - Small: 30G, Medium: 100G, Large: 350G
 */
export const UPGRADE_COSTS: Record<ItemRarity, UpgradeCost[]> = {
  common: [
    { gold: 200, magicStones: 150 },   // ~5 small stones
    { gold: 400, magicStones: 300 },   // ~10 small stones
    { gold: 800, magicStones: 600 },   // ~20 small stones
  ],
  uncommon: [
    { gold: 200, magicStones: 150 },
    { gold: 400, magicStones: 300 },
    { gold: 800, magicStones: 600 },
  ],
  rare: [
    { gold: 400, magicStones: 300 },
    { gold: 800, magicStones: 600 },
    { gold: 1600, magicStones: 1200 },
  ],
  epic: [
    { gold: 800, magicStones: 600 },
    { gold: 1600, magicStones: 1200 },
    { gold: 3200, magicStones: 2400 },
  ],
  legendary: [
    { gold: 1600, magicStones: 1200 },
    { gold: 3200, magicStones: 2400 },
    { gold: 6400, magicStones: 4800 },
  ],
};

/**
 * Base cost for quality upgrades (before multiplier)
 */
export const QUALITY_UP_BASE_COST: UpgradeCost = {
  gold: 500,
  magicStones: 300,
};

/**
 * Quality upgrade option configurations
 */
export const QUALITY_UP_OPTIONS: Record<QualityUpOption, QualityUpConfig> = {
  normal: {
    option: "normal",
    label: "通常強化",
    description: "標準的な品質強化。成功率は低めだがコストも控えめ",
    costMultiplier: 1.0,
    successRates: {
      poorToNormal: 0.40,
      normalToGood: 0.20,
      goodToMaster: 0.10,
    },
  },
  qualityFocused: {
    option: "qualityFocused",
    label: "品質重視",
    description: "コスト1.5倍で成功率アップ。確実性を求める方に",
    costMultiplier: 1.5,
    successRates: {
      poorToNormal: 0.80,
      normalToGood: 0.40,
      goodToMaster: 0.15,
    },
  },
  maxQuality: {
    option: "maxQuality",
    label: "最高品質狙い",
    description: "コスト2倍で最高の成功率。傑作を目指すならこれ",
    costMultiplier: 2.0,
    successRates: {
      poorToNormal: 1.00,
      normalToGood: 0.60,
      goodToMaster: 0.25,
    },
  },
};

/**
 * Repair cost per durability point
 */
export const REPAIR_COST_PER_DURABILITY = 0.5;

/**
 * Repair cost multipliers by rarity
 */
export const REPAIR_RARITY_MULTIPLIERS: Record<ItemRarity, number> = {
  common: 1.0,
  uncommon: 1.2,
  rare: 1.5,
  epic: 2.0,
  legendary: 3.0,
};

/**
 * Dismantle configuration
 */
export const DISMANTLE_CONFIG = {
  // Return percentage of sell price (10-25%)
  sellPriceReturnRate: {
    common: 0.10,
    uncommon: 0.12,
    rare: 0.15,
    epic: 0.20,
    legendary: 0.25,
  } as Record<ItemRarity, number>,

  // Bonus magic stone chance for Epic+ items
  bonusChances: {
    common: 0,
    uncommon: 0,
    rare: 0.10,    // 10%
    epic: 0.30,    // 30%
    legendary: 0.50, // 50%
  } as Record<ItemRarity, number>,

  // Bonus from equipment level (adds to bonus chance)
  levelBonusChance: {
    0: 0,
    1: 0.05,   // +5%
    2: 0.10,   // +10%
    3: 0.15,   // +15%
  } as Record<number, number>,

  // Bonus magic stone value (medium stone equivalent)
  bonusMagicStoneValue: 100,
};

/**
 * Helper: Get upgrade cost for specific rarity and current level
 */
export function getUpgradeCostByRarityAndLevel(
  rarity: ItemRarity,
  currentLevel: number
): UpgradeCost | null {
  const costs = UPGRADE_COSTS[rarity];
  if (currentLevel < 0 || currentLevel >= costs.length) {
    return null;
  }
  return costs[currentLevel];
}

/**
 * Helper: Get quality upgrade cost with multiplier
 */
export function getQualityUpgradeCost(option: QualityUpOption): UpgradeCost {
  const config = QUALITY_UP_OPTIONS[option];
  return {
    gold: Math.floor(QUALITY_UP_BASE_COST.gold * config.costMultiplier),
    magicStones: Math.floor(QUALITY_UP_BASE_COST.magicStones * config.costMultiplier),
  };
}

/**
 * Helper: Get repair cost for an item
 */
export function calculateRepairCost(
  durability: number,
  maxDurability: number,
  rarity: ItemRarity
): number {
  const durabilityToRestore = maxDurability - durability;
  const multiplier = REPAIR_RARITY_MULTIPLIERS[rarity];
  return Math.ceil(durabilityToRestore * REPAIR_COST_PER_DURABILITY * multiplier);
}

/**
 * Helper: Calculate dismantle gold return
 */
export function calculateDismantleGold(sellPrice: number, rarity: ItemRarity): number {
  const returnRate = DISMANTLE_CONFIG.sellPriceReturnRate[rarity];
  return Math.floor(sellPrice * returnRate);
}

/**
 * Helper: Calculate bonus magic stone chance
 */
export function calculateBonusStoneChance(rarity: ItemRarity, level: number): number {
  const baseChance = DISMANTLE_CONFIG.bonusChances[rarity];
  const levelBonus = DISMANTLE_CONFIG.levelBonusChance[level] ?? 0;
  return baseChance + levelBonus;
}
