// Test Items Data - For testing Storage UI
// These items will be removed or replaced with real items later

import type { Item } from "../type/ItemTypes";
import { createItemInstance } from "../type/ItemTypes";

// Storage Test Items (5 items)
export const STORAGE_TEST_ITEMS: Item[] = [
  createItemInstance("steel_sword", {
    typeId: "steel_sword",
    name: "Steel Sword",
    description: "A sturdy sword made of steel. Reliable for basic combat.",
    itemType: "equipment",
    type: "SWORD",
    equipmentSlot: "weapon",
    durability: 45,
    maxDurability: 50,
    level: 1,
    quality: "normal",
    effects: [
      {
        type: "stat",
        target: "attack",
        value: 15,
        description: "ATK +15",
      },
    ],
    rarity: "common",
    sellPrice: 50,
    canSell: true,
    canDiscard: true,
  }),

  createItemInstance("iron_armor", {
    typeId: "iron_armor",
    name: "Iron Armor",
    description: "Basic iron plating. Provides decent protection.",
    itemType: "equipment",
    type: "ARMOR",
    equipmentSlot: "armor",
    durability: 60,
    maxDurability: 60,
    level: 0,
    quality: "normal",
    effects: [
      {
        type: "stat",
        target: "defense",
        value: 10,
        description: "DEF +10",
      },
    ],
    rarity: "common",
    sellPrice: 40,
    canSell: true,
    canDiscard: true,
  }),

  createItemInstance("healing_potion", {
    typeId: "healing_potion",
    name: "Healing Potion",
    description: "Restores HP when consumed.",
    itemType: "consumable",
    type: "POTION",
    stackable: true,
    stackCount: 3,
    maxStack: 10,
    usableContext: "battle",
    rarity: "common",
    sellPrice: 20,
    canSell: true,
    canDiscard: true,
  }),

  createItemInstance("magic_scroll", {
    typeId: "magic_scroll",
    name: "Magic Scroll",
    description: "A scroll containing ancient magic. Single use.",
    itemType: "consumable",
    type: "SCROLL",
    stackable: false,
    usableContext: "battle",
    rarity: "rare",
    sellPrice: 100,
    canSell: true,
    canDiscard: true,
  }),

  createItemInstance("dragon_shield", {
    typeId: "dragon_shield",
    name: "Dragon Shield",
    description: "A legendary shield crafted from dragon scales.",
    itemType: "equipment",
    type: "SHIELD",
    equipmentSlot: "armor",
    durability: 95,
    maxDurability: 100,
    level: 2,
    quality: "good",
    effects: [
      {
        type: "stat",
        target: "defense",
        value: 35,
        description: "DEF +35",
      },
      {
        type: "passive",
        target: "fire_resistance",
        value: 20,
        description: "Fire Resistance +20%",
      },
    ],
    rarity: "epic",
    sellPrice: 500,
    canSell: true,
    canDiscard: false,
  }),
];

// Inventory Test Items (3 items)
export const INVENTORY_TEST_ITEMS: Item[] = [
  createItemInstance("bronze_sword", {
    typeId: "bronze_sword",
    name: "Bronze Sword",
    description: "An old bronze sword. Better than nothing.",
    itemType: "equipment",
    type: "SWORD",
    equipmentSlot: "weapon",
    durability: 25,
    maxDurability: 30,
    level: 0,
    quality: "poor",
    effects: [
      {
        type: "stat",
        target: "attack",
        value: 8,
        description: "ATK +8",
      },
    ],
    rarity: "common",
    sellPrice: 15,
    canSell: true,
    canDiscard: true,
  }),

  createItemInstance("lesser_potion", {
    typeId: "lesser_potion",
    name: "Lesser Potion",
    description: "A weak healing potion. Restores minimal HP.",
    itemType: "consumable",
    type: "POTION",
    stackable: true,
    stackCount: 2,
    maxStack: 10,
    usableContext: "anywhere",
    rarity: "common",
    sellPrice: 10,
    canSell: true,
    canDiscard: true,
  }),

  createItemInstance("teleport_stone", {
    typeId: "teleport_stone",
    name: "Teleport Stone",
    description: "Instantly return to base camp. Single use.",
    itemType: "consumable",
    type: "STONE",
    stackable: false,
    usableContext: "map",
    rarity: "rare",
    sellPrice: 150,
    canSell: true,
    canDiscard: false,
  }),
];

// Equipment Test Items (3 equipped items)
export const EQUIPPED_TEST_ITEMS: {
  weapon: Item | null;
  armor: Item | null;
  helmet: Item | null;
  boots: Item | null;
  accessory1: Item | null;
  accessory2: Item | null;
} = {
  weapon: createItemInstance("beginner_sword", {
    typeId: "beginner_sword",
    name: "Beginner Sword",
    description: "A sword given to all new adventurers.",
    itemType: "equipment",
    type: "SWORD",
    equipmentSlot: "weapon",
    durability: 40,
    maxDurability: 40,
    level: 0,
    quality: "normal",
    effects: [
      {
        type: "stat",
        target: "attack",
        value: 10,
        description: "ATK +10",
      },
    ],
    rarity: "common",
    sellPrice: 25,
    canSell: true,
    canDiscard: true,
  }),

  armor: createItemInstance("leather_armor", {
    typeId: "leather_armor",
    name: "Leather Armor",
    description: "Basic leather protection for beginners.",
    itemType: "equipment",
    type: "ARMOR",
    equipmentSlot: "armor",
    durability: 30,
    maxDurability: 30,
    level: 0,
    quality: "normal",
    effects: [
      {
        type: "stat",
        target: "defense",
        value: 5,
        description: "DEF +5",
      },
    ],
    rarity: "common",
    sellPrice: 20,
    canSell: true,
    canDiscard: true,
  }),

  helmet: null,
  boots: null,

  accessory1: createItemInstance("power_ring", {
    typeId: "power_ring",
    name: "Power Ring",
    description: "A ring that slightly enhances physical strength.",
    itemType: "equipment",
    type: "RING",
    equipmentSlot: "accessory1",
    durability: 999,
    maxDurability: 999,
    level: 1,
    quality: "normal",
    effects: [
      {
        type: "stat",
        target: "attack",
        value: 5,
        description: "ATK +5",
      },
      {
        type: "stat",
        target: "hp",
        value: 10,
        description: "HP +10",
      },
    ],
    rarity: "uncommon",
    sellPrice: 80,
    canSell: true,
    canDiscard: true,
  }),

  accessory2: null,
};
