// Blacksmith facility business logic
import type { Item } from '@/types/itemTypes';
import type { EquipmentQuality } from '@/types/itemTypes';
import type {
  UpgradeCost,
  RepairCost,
  DismantleResult,
  BlacksmithResult,
  UpgradePreview,
  QualityUpgradePreview,
  QualityUpOption,
} from '@/types/campTypes';
import {
  LEVEL_STAT_MODIFIERS,
  LEVEL_AP_MODIFIERS,
  QUALITY_MODIFIERS,
} from '@/constants/campConstants';
import {
  getNextQuality,
  canUpgradeLevel,
  canUpgradeQuality,
  needsRepair as needsRepairCheck,
  canDismantle as canDismantleCheck,
} from '../logic/blacksmithUtils';
import {
  QUALITY_UP_OPTIONS,
  DISMANTLE_CONFIG,
  getUpgradeCostByRarityAndLevel,
  getQualityUpgradeCost as getQualityUpgradeCostData,
  calculateRepairCost,
  calculateDismantleGold,
  calculateBonusStoneChance,
} from "../data/BlacksmithData";

// ==================== LEVEL UPGRADE ====================

/**
 * Check if item can be level upgraded
 */
export function canLevelUpgrade(item: Item): boolean {
  return canUpgradeLevel(item);
}

/**
 * Get level upgrade cost for item
 */
export function getLevelUpgradeCost(item: Item): UpgradeCost | null {
  if (!canLevelUpgrade(item)) return null;
  const currentLevel = item.level ?? 0;
  return getUpgradeCostByRarityAndLevel(item.rarity, currentLevel);
}

/**
 * Get upgrade preview showing stat changes
 */
export function getUpgradePreview(
  item: Item,
  playerGold: number,
  magicStoneValue: number
): UpgradePreview | null {
  if (!canLevelUpgrade(item)) return null;

  const currentLevel = item.level ?? 0;
  const nextLevel = currentLevel + 1;
  const cost = getLevelUpgradeCost(item);

  if (!cost) return null;

  // Calculate stat changes based on effects
  const statChanges: { stat: string; before: number; after: number }[] = [];

  if (item.effects) {
    for (const effect of item.effects) {
      if (effect.type === "stat" && typeof effect.value === "number") {
        const currentModifier = LEVEL_STAT_MODIFIERS[currentLevel];
        const nextModifier = LEVEL_STAT_MODIFIERS[nextLevel];
        const baseValue = effect.value / currentModifier;

        statChanges.push({
          stat: effect.target,
          before: Math.floor(effect.value),
          after: Math.floor(baseValue * nextModifier),
        });
      }
    }
  }

  // Add AP change
  if (item.maxDurability) {
    const currentApMod = LEVEL_AP_MODIFIERS[currentLevel];
    const nextApMod = LEVEL_AP_MODIFIERS[nextLevel];
    const baseAp = item.maxDurability / currentApMod;

    statChanges.push({
      stat: "maxAP",
      before: Math.floor(item.maxDurability),
      after: Math.floor(baseAp * nextApMod),
    });
  }

  return {
    currentLevel,
    nextLevel,
    statChanges,
    cost,
    canAfford: playerGold >= cost.gold && magicStoneValue >= cost.magicStones,
  };
}

/**
 * Perform level upgrade on item (returns new item, does not mutate)
 */
export function performLevelUpgrade(item: Item): Item {
  if (!canLevelUpgrade(item)) return item;

  const currentLevel = item.level ?? 0;
  const nextLevel = currentLevel + 1;

  // Calculate new stats
  const currentStatMod = LEVEL_STAT_MODIFIERS[currentLevel];
  const nextStatMod = LEVEL_STAT_MODIFIERS[nextLevel];
  const currentApMod = LEVEL_AP_MODIFIERS[currentLevel];
  const nextApMod = LEVEL_AP_MODIFIERS[nextLevel];

  // Update effects
  const newEffects = item.effects?.map((effect) => {
    if (effect.type === "stat" && typeof effect.value === "number") {
      const baseValue = effect.value / currentStatMod;
      return {
        ...effect,
        value: Math.floor(baseValue * nextStatMod),
      };
    }
    return effect;
  });

  // Update AP
  let newMaxDurability = item.maxDurability;
  let newDurability = item.durability;
  if (item.maxDurability) {
    const baseAp = item.maxDurability / currentApMod;
    newMaxDurability = Math.floor(baseAp * nextApMod);
    // Increase current durability proportionally
    if (item.durability) {
      const ratio = item.durability / item.maxDurability;
      newDurability = Math.floor(newMaxDurability * ratio);
    }
  }

  return {
    ...item,
    level: nextLevel,
    effects: newEffects,
    maxDurability: newMaxDurability,
    durability: newDurability,
  };
}

// ==================== QUALITY UPGRADE ====================

/**
 * Check if item quality can be upgraded
 */
export function canQualityUpgrade(item: Item): boolean {
  return canUpgradeQuality(item);
}

/**
 * Get quality upgrade cost for specific option
 */
export function getQualityUpgradeCost(option: QualityUpOption): UpgradeCost {
  return getQualityUpgradeCostData(option);
}

/**
 * Get success chance for quality upgrade
 */
export function getQualitySuccessChance(
  currentQuality: EquipmentQuality,
  option: QualityUpOption
): number {
  const config = QUALITY_UP_OPTIONS[option];
  switch (currentQuality) {
    case "poor":
      return config.successRates.poorToNormal;
    case "normal":
      return config.successRates.normalToGood;
    case "good":
      return config.successRates.goodToMaster;
    default:
      return 0;
  }
}

/**
 * Get quality upgrade preview
 */
export function getQualityUpgradePreview(
  item: Item,
  option: QualityUpOption,
  playerGold: number,
  magicStoneValue: number
): QualityUpgradePreview | null {
  if (!canQualityUpgrade(item) || !item.quality) return null;

  const targetQuality = getNextQuality(item.quality);
  if (!targetQuality) return null;

  const cost = getQualityUpgradeCost(option);
  const successChance = getQualitySuccessChance(item.quality, option);

  return {
    currentQuality: item.quality,
    targetQuality,
    successChance,
    cost,
    canAfford: playerGold >= cost.gold && magicStoneValue >= cost.magicStones,
  };
}

/**
 * Attempt quality upgrade (includes RNG)
 */
export function attemptQualityUpgrade(
  item: Item,
  option: QualityUpOption
): BlacksmithResult {
  if (!canQualityUpgrade(item) || !item.quality) {
    return {
      success: false,
      message: "この装備の品質は強化できません",
      qualityChanged: false,
    };
  }

  const nextQuality = getNextQuality(item.quality);
  if (!nextQuality) {
    return {
      success: false,
      message: "既に最高品質です",
      qualityChanged: false,
    };
  }

  const successChance = getQualitySuccessChance(item.quality, option);
  const roll = Math.random();
  const succeeded = roll < successChance;

  if (succeeded) {
    // Apply quality modifier to stats
    const currentQualityMod = QUALITY_MODIFIERS[item.quality];
    const nextQualityMod = QUALITY_MODIFIERS[nextQuality];

    const newEffects = item.effects?.map((effect) => {
      if (effect.type === "stat" && typeof effect.value === "number") {
        const baseValue = effect.value / currentQualityMod;
        return {
          ...effect,
          value: Math.floor(baseValue * nextQualityMod),
        };
      }
      return effect;
    });

    const upgradedItem: Item = {
      ...item,
      quality: nextQuality,
      effects: newEffects,
    };

    return {
      success: true,
      message: `品質強化成功! ${item.quality} → ${nextQuality}`,
      item: upgradedItem,
      qualityChanged: true,
    };
  } else {
    return {
      success: false,
      message: `品質強化失敗... (${Math.floor(successChance * 100)}%の確率でした)`,
      item: item,
      qualityChanged: false,
    };
  }
}

// ==================== REPAIR ====================

/**
 * Check if item needs repair
 */
export function needsRepair(item: Item): boolean {
  return needsRepairCheck(item);
}

/**
 * Get repair cost for item
 */
export function getRepairCost(item: Item): RepairCost | null {
  if (!needsRepair(item)) return null;

  const durability = item.durability ?? 0;
  const maxDurability = item.maxDurability ?? 100;
  const durabilityToRestore = maxDurability - durability;
  const gold = calculateRepairCost(durability, maxDurability, item.rarity);

  return {
    gold,
    durabilityRestored: durabilityToRestore,
  };
}

/**
 * Perform repair on item
 */
export function performRepair(item: Item): Item {
  if (!needsRepair(item)) return item;

  return {
    ...item,
    durability: item.maxDurability,
  };
}

// ==================== DISMANTLE ====================

/**
 * Check if item can be dismantled
 */
export function canDismantle(item: Item): boolean {
  return canDismantleCheck(item);
}

/**
 * Get dismantle preview (deterministic part)
 */
export function getDismantlePreview(item: Item): DismantleResult | null {
  if (!canDismantle(item)) return null;

  const goldReturn = calculateDismantleGold(item.sellPrice, item.rarity);
  const level = item.level ?? 0;
  const magicStoneChance = calculateBonusStoneChance(item.rarity, level);
  const bonusMagicStoneValue = DISMANTLE_CONFIG.bonusMagicStoneValue;

  let bonusDescription = "";
  if (magicStoneChance > 0) {
    bonusDescription = `${Math.floor(magicStoneChance * 100)}%の確率で魔石を獲得`;
  } else {
    bonusDescription = "ボーナスなし";
  }

  return {
    goldReturn,
    magicStoneChance,
    magicStoneReturn: bonusMagicStoneValue,
    bonusDescription,
  };
}

/**
 * Perform dismantle (includes RNG for bonus)
 */
export function performDismantle(item: Item): BlacksmithResult {
  if (!canDismantle(item)) {
    return {
      success: false,
      message: "この装備は分解できません",
    };
  }

  const preview = getDismantlePreview(item);
  if (!preview) {
    return {
      success: false,
      message: "分解プレビューの取得に失敗しました",
    };
  }

  // Roll for bonus magic stone
  const gotBonus = Math.random() < preview.magicStoneChance;
  const magicStoneGain = gotBonus ? preview.magicStoneReturn : 0;

  let message = `${item.name}を分解しました。${preview.goldReturn}G を獲得`;
  if (gotBonus) {
    message += `、さらにボーナスで魔石${magicStoneGain}相当を獲得!`;
  }

  return {
    success: true,
    message,
    goldChange: preview.goldReturn,
    magicStoneChange: magicStoneGain,
  };
}

// ==================== VALIDATION HELPERS ====================

/**
 * Check if player can afford a cost
 */
export function canAfford(
  playerGold: number,
  magicStoneValue: number,
  cost: UpgradeCost
): boolean {
  return playerGold >= cost.gold && magicStoneValue >= cost.magicStones;
}

/**
 * Check if player can afford repair
 */
export function canAffordRepair(playerGold: number, cost: RepairCost): boolean {
  return playerGold >= cost.gold;
}

/**
 * Check if dismantle should show warning (valuable item)
 */
export function shouldWarnOnDismantle(item: Item): boolean {
  const isHighRarity = item.rarity === "rare" || item.rarity === "epic" || item.rarity === "legendary";
  const isHighLevel = (item.level ?? 0) >= 1;
  const isHighQuality = item.quality === "good" || item.quality === "master";

  return isHighRarity && isHighLevel && isHighQuality;
}

// ==================== BATCH OPERATIONS ====================

/**
 * Get total repair cost for multiple items
 */
export function getTotalRepairCost(items: Item[]): number {
  return items.reduce((total, item) => {
    const cost = getRepairCost(item);
    return total + (cost?.gold ?? 0);
  }, 0);
}

/**
 * Get items that need repair
 */
export function getRepairableItems(items: Item[]): Item[] {
  return items.filter((item) => needsRepair(item));
}

/**
 * Get items that can be upgraded
 */
export function getUpgradeableItems(items: Item[]): Item[] {
  return items.filter((item) => canLevelUpgrade(item));
}

/**
 * Get items that can have quality upgraded
 */
export function getQualityUpgradeableItems(items: Item[]): Item[] {
  return items.filter((item) => canQualityUpgrade(item));
}

/**
 * Get items that can be dismantled
 */
export function getDismantleableItems(items: Item[]): Item[] {
  return items.filter((item) => canDismantle(item));
}
