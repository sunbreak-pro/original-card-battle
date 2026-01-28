// ConsumableItemData.ts - Registry of all consumable items and their effects

import type { ConsumableItemData } from "@/types/itemTypes";

/**
 * Registry of all consumable items with their effects
 * Organized by usableContext
 */
export const CONSUMABLE_ITEMS: Record<string, ConsumableItemData> = {
  // ============================================================================
  // BATTLE-USABLE ITEMS
  // ============================================================================
  healing_small_potion: {
    typeId: "poor_healing_potion",
    name: "Poor Healing Potion",
    nameJa: "下級回復薬",
    description: "Recovers 30 HP. A basic remedy for minor wounds.",
    descriptionJa: "HPを30回復する。軽傷用の基本的な治療薬。",
    effects: [{ type: "heal", value: 30 }],
    usableContext: "battle",
    icon: "🧪",
    rarity: "common",
    sellPrice: 25,
    maxStack: 99,
    shopPrice: 50,
  },
  healing_potion: {
    typeId: "intermediate_healing_potion",
    name: "Intermediate Healing Potion",
    nameJa: "中級回復薬",
    description: "Restores 30 HP when consumed.",
    descriptionJa: "使用時にHPを30回復する。",
    effects: [{ type: 'heal', value: 60 }],
    usableContext: 'battle',
    icon: "🧪",
    rarity: 'common',
    sellPrice: 20,
    maxStack: 10,
  },

  greater_healing_potion: {
    typeId: "greater_healing_potion",
    name: "Greater Healing Potion",
    nameJa: "上級回復薬",
    description: "Restores 60 HP when consumed.",
    descriptionJa: "使用時にHPを60回復する。",
    effects: [{ type: 'heal', value: 100 }],
    usableContext: 'battle',
    icon: "🧪",
    rarity: 'uncommon',
    sellPrice: 50,
    maxStack: 5,
  },

  full_elixir: {
    typeId: "full_elixir",
    name: "Full Elixir",
    nameJa: "完全回復薬",
    description: "Fully restores HP to maximum.",
    descriptionJa: "HPを最大まで完全回復する。",
    effects: [{ type: 'fullHeal' }],
    usableContext: 'battle',
    icon: "✨",
    rarity: 'rare',
    sellPrice: 200,
    maxStack: 3,
  },

  energy_potion: {
    typeId: "energy_potion",
    name: "Energy Potion",
    nameJa: "エナジーポーション",
    description: "Restores 2 card energy.",
    descriptionJa: "カードエネルギーを2回復する。",
    effects: [{ type: 'energy', value: 2 }],
    usableContext: 'battle',
    icon: "💧",
    rarity: 'common',
    sellPrice: 25,
    maxStack: 10,
  },

  shield_potion: {
    typeId: "shield_potion",
    name: "Shield Potion",
    nameJa: "シールドポーション",
    description: "Grants 15 guard points.",
    descriptionJa: "ガードを15獲得する。",
    effects: [{ type: 'shield', value: 15 }],
    usableContext: 'battle',
    icon: "🛡️",
    rarity: 'common',
    sellPrice: 30,
    maxStack: 10,
  },

  antidote: {
    typeId: "antidote",
    name: "Antidote",
    nameJa: "解毒薬",
    description: "Removes all debuffs.",
    descriptionJa: "すべてのデバフを解除する。",
    effects: [{ type: 'debuffClear' }],
    usableContext: 'battle',
    icon: "💊",
    rarity: 'uncommon',
    sellPrice: 40,
    maxStack: 5,
  },

  strength_elixir: {
    typeId: "strength_elixir",
    name: "Strength Elixir",
    nameJa: "力のエリクサー",
    description: "Grants Attack Up (Major) for 3 turns.",
    descriptionJa: "3ターンの間、攻撃力が大幅に上昇する。",
    effects: [{ type: 'buff', buffType: 'atkUpMajor', duration: 3 }],
    usableContext: 'battle',
    icon: "💪",
    rarity: 'rare',
    sellPrice: 100,
    maxStack: 3,
  },

  iron_skin_elixir: {
    typeId: "iron_skin_elixir",
    name: "Iron Skin Elixir",
    nameJa: "鉄壁のエリクサー",
    description: "Grants Defense Up (Major) for 3 turns.",
    descriptionJa: "3ターンの間、防御力が大幅に上昇する。",
    effects: [{ type: 'buff', buffType: 'defUpMajor', duration: 3 }],
    usableContext: 'battle',
    icon: "🏰",
    rarity: 'rare',
    sellPrice: 100,
    maxStack: 3,
  },

  haste_potion: {
    typeId: "haste_potion",
    name: "Haste Potion",
    nameJa: "ヘイストポーション",
    description: "Grants Haste for 2 turns.",
    descriptionJa: "2ターンの間、速度が上昇する。",
    effects: [{ type: 'buff', buffType: 'haste', duration: 2 }],
    usableContext: 'battle',
    icon: "⚡",
    rarity: 'uncommon',
    sellPrice: 60,
    maxStack: 5,
  },

  magic_burst_crystal: {
    typeId: "magic_burst_crystal",
    name: "Magic Burst Crystal",
    nameJa: "魔力爆発の結晶",
    description: "Deals 40 damage to all enemies.",
    descriptionJa: "全ての敵に40ダメージを与える。",
    effects: [{ type: 'damage', value: 40, targetAll: true }],
    usableContext: 'battle',
    icon: "💎",
    rarity: 'rare',
    sellPrice: 150,
    maxStack: 3,
  },

  time_stop_hourglass: {
    typeId: "time_stop_hourglass",
    name: "Time Stop Hourglass",
    nameJa: "時止めの砂時計",
    description: "Skips the next enemy turn.",
    descriptionJa: "次の敵のターンをスキップする。",
    effects: [{ type: 'skipEnemyTurn' }],
    usableContext: 'battle',
    icon: "⏳",
    rarity: 'epic',
    sellPrice: 300,
    maxStack: 1,
  },

  draw_scroll: {
    typeId: "draw_scroll",
    name: "Draw Scroll",
    nameJa: "ドローの巻物",
    description: "Draw 2 additional cards.",
    descriptionJa: "カードを2枚追加でドローする。",
    effects: [{ type: 'draw', value: 2 }],
    usableContext: 'battle',
    icon: "📜",
    rarity: 'uncommon',
    sellPrice: 45,
    maxStack: 5,
  },

  combo_elixir: {
    typeId: "combo_elixir",
    name: "Combo Elixir",
    nameJa: "コンボエリクサー",
    description: "Restores 30 HP, 2 energy, and grants 10 guard.",
    descriptionJa: "HPを30回復し、エネルギーを2回復し、ガードを10獲得する。",
    effects: [
      { type: 'heal', value: 30 },
      { type: 'energy', value: 2 },
      { type: 'shield', value: 10 }
    ],
    usableContext: 'battle',
    icon: "🌟",
    rarity: 'epic',
    sellPrice: 250,
    maxStack: 2,
  },

  // ============================================================================
  // MAP-ONLY ITEMS
  // ============================================================================

  teleport_stone: {
    typeId: "teleport_stone",
    name: "Teleport Stone",
    nameJa: "帰還石",
    description: "Instantly return to base camp.",
    descriptionJa: "ベースキャンプに即座に帰還する。",
    effects: [], // Map effect handled separately
    usableContext: 'map',
    icon: "💫",
    rarity: 'rare',
    sellPrice: 150,
    maxStack: 3,
  },

  treasure_map: {
    typeId: "treasure_map",
    name: "Treasure Map",
    nameJa: "宝の地図",
    description: "Reveals hidden treasure on the current floor.",
    descriptionJa: "現在の階層の隠し宝を表示する。",
    effects: [], // Map effect handled separately
    usableContext: 'map',
    icon: "🗺️",
    rarity: 'uncommon',
    sellPrice: 80,
    maxStack: 3,
  },

  repair_kit: {
    typeId: "repair_kit",
    name: "Repair Kit",
    nameJa: "修理キット",
    description: "Restores 20 durability to equipped weapon.",
    descriptionJa: "装備中の武器の耐久度を20回復する。",
    effects: [], // Map effect handled separately
    usableContext: 'map',
    icon: "🔧",
    rarity: 'common',
    sellPrice: 30,
    maxStack: 5,
  },

  lucky_charm: {
    typeId: "lucky_charm",
    name: "Lucky Charm",
    nameJa: "幸運のお守り",
    description: "Increases rare drop rate for next battle.",
    descriptionJa: "次の戦闘でレアドロップ率が上昇する。",
    effects: [], // Map effect handled separately
    usableContext: 'map',
    icon: "🍀",
    rarity: 'uncommon',
    sellPrice: 60,
    maxStack: 5,
  },

  // ============================================================================
  // CAMP-ONLY ITEMS
  // ============================================================================

  merchant_ticket: {
    typeId: "merchant_ticket",
    name: "Merchant Ticket",
    nameJa: "商人チケット",
    description: "Refreshes the shop inventory.",
    descriptionJa: "ショップの品揃えを更新する。",
    effects: [], // Camp effect handled separately
    usableContext: 'camp',
    icon: "🎟️",
    rarity: 'rare',
    sellPrice: 200,
    maxStack: 3,
  },

  // ============================================================================
  // ANYWHERE ITEMS
  // ============================================================================

  lesser_potion: {
    typeId: "lesser_potion",
    name: "Lesser Potion",
    nameJa: "小さな回復薬",
    description: "Restores 15 HP when consumed.",
    descriptionJa: "使用時にHPを15回復する。",
    effects: [{ type: 'heal', value: 15 }],
    usableContext: 'anywhere',
    icon: "🧴",
    rarity: 'common',
    sellPrice: 10,
    maxStack: 10,
  },
};

/**
 * Get consumable data by typeId
 */
export function getConsumableData(typeId: string): ConsumableItemData | undefined {
  return CONSUMABLE_ITEMS[typeId];
}

/**
 * Get all consumables that are usable in a specific context
 */
export function getConsumablesByContext(context: 'battle' | 'map' | 'camp' | 'anywhere'): ConsumableItemData[] {
  return Object.values(CONSUMABLE_ITEMS).filter(
    item => item.usableContext === context || item.usableContext === 'anywhere'
  );
}

/**
 * Check if an item is usable in battle
 */
export function isUsableInBattle(typeId: string): boolean {
  const data = CONSUMABLE_ITEMS[typeId];
  return data ? (data.usableContext === 'battle' || data.usableContext === 'anywhere') : false;
}
