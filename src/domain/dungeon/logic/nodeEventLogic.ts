// Node Event Logic (BF.2)
// Handles rest, treasure, and event node interactions

import type { Item } from "../../item_equipment/type/ItemTypes";
import { createItemInstance } from "../../item_equipment/type/ItemTypes";

/**
 * Result of a node event interaction
 */
export interface NodeEventResult {
  type: "rest" | "treasure" | "event";
  title: string;
  description: string;
  rewards: NodeEventReward;
}

/**
 * Rewards from a node event
 */
export interface NodeEventReward {
  hpRestore?: number; // Absolute HP restored
  hpRestorePercent?: number; // Percentage of max HP restored
  gold?: number;
  magicStones?: { small?: number; medium?: number; large?: number };
  items?: Item[];
}

// ========================================================================
// Rest Node
// ========================================================================

/**
 * Process a rest node - restores 20-30% HP
 */
export function processRestNode(currentHp: number, maxHp: number): NodeEventResult {
  const restorePercent = 0.2 + Math.random() * 0.1; // 20-30%
  const hpRestored = Math.floor(maxHp * restorePercent);
  const actualRestore = Math.min(hpRestored, maxHp - currentHp);

  return {
    type: "rest",
    title: "休憩地点",
    description: `安全な場所で体を休めた。HP が ${actualRestore} 回復した。`,
    rewards: {
      hpRestorePercent: restorePercent,
      hpRestore: actualRestore,
    },
  };
}

// ========================================================================
// Treasure Node
// ========================================================================

/**
 * Treasure loot table with weighted probabilities
 */
interface TreasureLoot {
  weight: number;
  generate: () => NodeEventReward;
  description: string;
}

const TREASURE_LOOT_TABLE: TreasureLoot[] = [
  {
    weight: 35,
    description: "ゴールドの山",
    generate: () => ({
      gold: 50 + Math.floor(Math.random() * 150), // 50-200 gold
    }),
  },
  {
    weight: 25,
    description: "小さな魔石の欠片",
    generate: () => ({
      magicStones: { small: 1 + Math.floor(Math.random() * 3) }, // 1-3 small stones
    }),
  },
  {
    weight: 15,
    description: "中型の魔石",
    generate: () => ({
      magicStones: { medium: 1 },
    }),
  },
  {
    weight: 15,
    description: "回復薬の詰め合わせ",
    generate: () => ({
      items: [
        createItemInstance("potion_small", {
          typeId: "potion_small",
          name: "Small Healing Potion",
          description: "Recovers 30 HP.",
          itemType: "consumable",
          type: "🧪",
          rarity: "common",
          sellPrice: 25,
          canSell: true,
          canDiscard: true,
          stackable: true,
          stackCount: 1,
          maxStack: 99,
        }),
      ],
    }),
  },
  {
    weight: 10,
    description: "大きな宝箱",
    generate: () => ({
      gold: 100 + Math.floor(Math.random() * 200), // 100-300 gold
      magicStones: { small: 2 },
    }),
  },
];

/**
 * Process a treasure node - random loot from the loot table
 */
export function processTreasureNode(): NodeEventResult {
  const totalWeight = TREASURE_LOOT_TABLE.reduce((sum, loot) => sum + loot.weight, 0);
  let roll = Math.random() * totalWeight;

  let selectedLoot = TREASURE_LOOT_TABLE[0];
  for (const loot of TREASURE_LOOT_TABLE) {
    roll -= loot.weight;
    if (roll <= 0) {
      selectedLoot = loot;
      break;
    }
  }

  const rewards = selectedLoot.generate();
  const rewardParts: string[] = [];
  if (rewards.gold) rewardParts.push(`${rewards.gold} ゴールド`);
  if (rewards.magicStones?.small) rewardParts.push(`小魔石 x${rewards.magicStones.small}`);
  if (rewards.magicStones?.medium) rewardParts.push(`中魔石 x${rewards.magicStones.medium}`);
  if (rewards.magicStones?.large) rewardParts.push(`大魔石 x${rewards.magicStones.large}`);
  if (rewards.items?.length) rewardParts.push(`アイテム x${rewards.items.length}`);

  return {
    type: "treasure",
    title: "宝箱発見！",
    description: `${selectedLoot.description}を発見した！${rewardParts.join("、")}を獲得！`,
    rewards,
  };
}

// ========================================================================
// Event Node
// ========================================================================

/**
 * Random event definitions
 */
interface RandomEvent {
  weight: number;
  title: string;
  descriptionFactory: () => string;
  rewardFactory: () => NodeEventReward;
}

const RANDOM_EVENTS: RandomEvent[] = [
  {
    weight: 25,
    title: "落とし物",
    descriptionFactory: () => "地面に落ちたポーションを見つけた。",
    rewardFactory: () => ({
      items: [
        createItemInstance("potion_small", {
          typeId: "potion_small",
          name: "Small Healing Potion",
          description: "Recovers 30 HP.",
          itemType: "consumable",
          type: "🧪",
          rarity: "common",
          sellPrice: 25,
          canSell: true,
          canDiscard: true,
          stackable: true,
          stackCount: 1,
          maxStack: 99,
        }),
      ],
    }),
  },
  {
    weight: 20,
    title: "隠された金庫",
    descriptionFactory: () => "壁の隙間に隠された金庫を発見した！",
    rewardFactory: () => ({
      gold: 80 + Math.floor(Math.random() * 120),
    }),
  },
  {
    weight: 20,
    title: "旅の商人",
    descriptionFactory: () => "友好的な旅の商人が少しだけ分けてくれた。",
    rewardFactory: () => ({
      gold: 30 + Math.floor(Math.random() * 50),
      magicStones: { small: 1 },
    }),
  },
  {
    weight: 15,
    title: "罠！",
    descriptionFactory: () => "罠に引っかかった！少しダメージを受けた。",
    rewardFactory: () => ({
      hpRestore: -(10 + Math.floor(Math.random() * 20)), // Negative = damage
    }),
  },
  {
    weight: 10,
    title: "癒しの泉",
    descriptionFactory: () => "不思議な泉を見つけた。体が癒される。",
    rewardFactory: () => ({
      hpRestorePercent: 0.15, // 15% HP restore
    }),
  },
  {
    weight: 10,
    title: "古代の碑文",
    descriptionFactory: () => "古代の碑文に刻まれた知恵を解読した。魔石の欠片を得た。",
    rewardFactory: () => ({
      magicStones: { small: 2 },
    }),
  },
];

/**
 * Process an event node - random event from the event table
 */
export function processEventNode(): NodeEventResult {
  const totalWeight = RANDOM_EVENTS.reduce((sum, event) => sum + event.weight, 0);
  let roll = Math.random() * totalWeight;

  let selectedEvent = RANDOM_EVENTS[0];
  for (const event of RANDOM_EVENTS) {
    roll -= event.weight;
    if (roll <= 0) {
      selectedEvent = event;
      break;
    }
  }

  return {
    type: "event",
    title: selectedEvent.title,
    description: selectedEvent.descriptionFactory(),
    rewards: selectedEvent.rewardFactory(),
  };
}

/**
 * Process any non-battle node type
 */
export function processNodeEvent(
  nodeType: "rest" | "treasure" | "event",
  currentHp: number,
  maxHp: number
): NodeEventResult {
  switch (nodeType) {
    case "rest":
      return processRestNode(currentHp, maxHp);
    case "treasure":
      return processTreasureNode();
    case "event":
      return processEventNode();
  }
}
