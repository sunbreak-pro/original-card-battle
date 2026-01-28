// Sanctuary skill tree data

import type { SkillNode } from "../../../domain/camps/types/SanctuaryTypes";

/**
 * Complete skill tree data
 * Arranged in radial layout:
 * - HP nodes: Top (0°)
 * - Gold nodes: Right (90°)
 * - Utility nodes: Bottom (180°)
 * - Class nodes: Left (270°)
 * - Exploration nodes: Diagonal positions
 */
export const SKILL_TREE_NODES: SkillNode[] = [
  // ==================== TIER 1 (20-30 Souls) ====================

  // HP Branch - Top
  {
    id: "blessing_life_1",
    name: "Blessing of Life I",
    description:
      "The light of the sanctuary strengthens your vitality. Gain +10 max HP at the start of each run.",
    icon: "❤️",
    cost: 20,
    category: "hp",
    tier: 1,
    prerequisites: [],
    effects: [
      {
        type: "stat_boost",
        target: "initial_hp",
        value: 10,
        description: "+10 Max HP",
      },
    ],
    position: { angle: 0, radius: 1 },
  },

  // Gold Branch - Right
  {
    id: "blessing_wealth_1",
    name: "Blessing of Wealth I",
    description:
      "Fortune favors the blessed. Gain +10% gold from all sources during exploration.",
    icon: "💰",
    cost: 25,
    category: "gold",
    tier: 1,
    prerequisites: [],
    effects: [
      {
        type: "gold_multiplier",
        target: "gold_acquisition",
        value: 0.1,
        description: "+10% Gold",
      },
    ],
    position: { angle: 90, radius: 1 },
  },

  // Class Nodes - Left
  {
    id: "swordsman_insight",
    name: "Swordsman's Insight",
    description:
      "Ancient warriors guide your blade. Start each battle with +1 Sword Energy.",
    icon: "⚔️",
    cost: 30,
    category: "class",
    tier: 1,
    prerequisites: [],
    effects: [
      {
        type: "stat_boost",
        target: "sword_energy",
        value: 1,
        description: "+1 Sword Energy",
      },
    ],
    classRestriction: "swordsman",
    position: { angle: 240, radius: 1 },
  },
  {
    id: "mage_insight",
    name: "Mage's Insight",
    description:
      "Arcane echoes resonate within. Start each battle with +1 Resonance.",
    icon: "✨",
    cost: 30,
    category: "class",
    tier: 1,
    prerequisites: [],
    effects: [
      {
        type: "stat_boost",
        target: "resonance",
        value: 1,
        description: "+1 Resonance",
      },
    ],
    classRestriction: "mage",
    position: { angle: 270, radius: 1 },
  },
  {
    id: "summoner_insight",
    name: "Summoner's Insight",
    description:
      "Spirits heed your call more readily. Start each battle with +1 Summon Slot.",
    icon: "👻",
    cost: 30,
    category: "class",
    tier: 1,
    prerequisites: [],
    effects: [
      {
        type: "stat_boost",
        target: "summon_slot",
        value: 1,
        description: "+1 Summon Slot",
      },
    ],
    classRestriction: "summoner",
    position: { angle: 300, radius: 1 },
  },

  // Utility - Bottom
  {
    id: "keen_eye",
    name: "Keen Eye",
    description:
      "Your perception sharpens. Identify basic item properties without appraisal.",
    icon: "👁️",
    cost: 25,
    category: "utility",
    tier: 1,
    prerequisites: [],
    effects: [
      {
        type: "special_ability",
        target: "basic_appraisal",
        value: 1,
        description: "Basic Appraisal",
      },
    ],
    position: { angle: 180, radius: 1 },
  },

  // ==================== TIER 2 (40-80 Souls) ====================

  // HP Branch - Top
  {
    id: "blessing_life_2",
    name: "Blessing of Life II",
    description:
      "The sanctuary's protection deepens. Gain +20 max HP at the start of each run.",
    icon: "❤️",
    cost: 50,
    category: "hp",
    tier: 2,
    prerequisites: ["blessing_life_1"],
    effects: [
      {
        type: "stat_boost",
        target: "initial_hp",
        value: 20,
        description: "+20 Max HP",
      },
    ],
    position: { angle: 0, radius: 2 },
  },
  {
    id: "boon_recovery",
    name: "Boon of Recovery",
    description:
      "Healing energies linger upon you. Recover 5% HP when resting at camp.",
    icon: "💚",
    cost: 40,
    category: "hp",
    tier: 2,
    prerequisites: ["blessing_life_1"],
    effects: [
      {
        type: "stat_boost",
        target: "hp_recovery",
        value: 0.05,
        description: "+5% HP Recovery",
      },
    ],
    position: { angle: 30, radius: 2 },
  },

  // Gold Branch - Right
  {
    id: "blessing_wealth_2",
    name: "Blessing of Wealth II",
    description:
      "Riches flow to you naturally. Gain +20% gold from all sources (cumulative).",
    icon: "💰",
    cost: 60,
    category: "gold",
    tier: 2,
    prerequisites: ["blessing_wealth_1"],
    effects: [
      {
        type: "gold_multiplier",
        target: "gold_acquisition",
        value: 0.2,
        description: "+20% Gold",
      },
    ],
    position: { angle: 90, radius: 2 },
  },

  // Utility - Bottom
  {
    id: "eye_appraisal",
    name: "Eye of Appraisal",
    description:
      "See beyond the surface. View detailed equipment stats and effects.",
    icon: "🔍",
    cost: 45,
    category: "utility",
    tier: 2,
    prerequisites: ["keen_eye"],
    effects: [
      {
        type: "special_ability",
        target: "appraisal",
        value: 1,
        description: "Equipment Appraisal",
      },
    ],
    position: { angle: 180, radius: 2 },
  },
  {
    id: "expanded_bag",
    name: "Expanded Bag",
    description:
      "Your bag seems to hold more than before. +5 inventory capacity.",
    icon: "🎒",
    cost: 50,
    category: "utility",
    tier: 2,
    prerequisites: ["keen_eye"],
    effects: [
      {
        type: "resource_increase",
        target: "inventory_capacity",
        value: 5,
        description: "+5 Inventory",
      },
    ],
    position: { angle: 150, radius: 2 },
  },

  // Exploration - Diagonal
  {
    id: "extended_exploration_1",
    name: "Extended Exploration I",
    description:
      "Your endurance grows. +1 maximum exploration per day.",
    icon: "🗺️",
    cost: 80,
    category: "exploration",
    tier: 2,
    prerequisites: [],
    effects: [
      {
        type: "exploration_limit",
        target: "exploration_limit",
        value: 1,
        description: "+1 Exploration",
      },
    ],
    position: { angle: 45, radius: 2 },
  },

  // ==================== TIER 3 (100-150 Souls) ====================

  // HP Branch - Top
  {
    id: "blessing_life_3",
    name: "Blessing of Life III",
    description:
      "You are one with the sanctuary's power. Gain +30 max HP at the start of each run.",
    icon: "❤️",
    cost: 100,
    category: "hp",
    tier: 3,
    prerequisites: ["blessing_life_2"],
    effects: [
      {
        type: "stat_boost",
        target: "initial_hp",
        value: 30,
        description: "+30 Max HP",
      },
    ],
    position: { angle: 0, radius: 3 },
  },
  {
    id: "indomitable_will",
    name: "Indomitable Will",
    description:
      "Death cannot claim you so easily. Once per run, survive a killing blow with 1 HP.",
    icon: "🛡️",
    cost: 120,
    category: "hp",
    tier: 3,
    prerequisites: ["blessing_life_2", "boon_recovery"],
    effects: [
      {
        type: "special_ability",
        target: "death_defiance",
        value: 1,
        description: "Survive with 1 HP (once)",
      },
    ],
    position: { angle: 15, radius: 3 },
  },

  // Gold Branch - Right
  {
    id: "blessing_wealth_3",
    name: "Blessing of Wealth III",
    description:
      "Fortune itself bends to your will. Gain +30% gold from all sources (cumulative).",
    icon: "💰",
    cost: 100,
    category: "gold",
    tier: 3,
    prerequisites: ["blessing_wealth_2"],
    effects: [
      {
        type: "gold_multiplier",
        target: "gold_acquisition",
        value: 0.3,
        description: "+30% Gold",
      },
    ],
    position: { angle: 90, radius: 3 },
  },
  {
    id: "soul_resonance",
    name: "Soul Resonance",
    description:
      "Souls are drawn to you like moths to flame. +20% soul acquisition from all sources.",
    icon: "👻",
    cost: 110,
    category: "gold",
    tier: 3,
    prerequisites: ["blessing_wealth_2"],
    effects: [
      {
        type: "soul_multiplier",
        target: "soul_acquisition",
        value: 0.2,
        description: "+20% Souls",
      },
    ],
    position: { angle: 75, radius: 3 },
  },

  // Utility - Bottom
  {
    id: "true_appraisal",
    name: "True Appraisal",
    description:
      "Nothing is hidden from your sight. Reveal hidden effects and true values of items.",
    icon: "💎",
    cost: 100,
    category: "utility",
    tier: 3,
    prerequisites: ["eye_appraisal"],
    effects: [
      {
        type: "special_ability",
        target: "true_appraisal",
        value: 1,
        description: "True Appraisal",
      },
    ],
    position: { angle: 180, radius: 3 },
  },

  // Exploration - Diagonal
  {
    id: "extended_exploration_2",
    name: "Extended Exploration II",
    description:
      "Your stamina seems limitless. +2 maximum exploration per day.",
    icon: "🗺️",
    cost: 150,
    category: "exploration",
    tier: 3,
    prerequisites: ["extended_exploration_1"],
    effects: [
      {
        type: "exploration_limit",
        target: "exploration_limit",
        value: 2,
        description: "+2 Exploration",
      },
    ],
    position: { angle: 45, radius: 3 },
  },
];

/**
 * Get node by ID
 */
export function getNodeById(id: string): SkillNode | undefined {
  return SKILL_TREE_NODES.find((node) => node.id === id);
}

/**
 * Get all nodes of a specific tier
 */
export function getNodesByTier(tier: 1 | 2 | 3): SkillNode[] {
  return SKILL_TREE_NODES.filter((node) => node.tier === tier);
}

/**
 * Get all nodes of a specific category
 */
export function getNodesByCategory(
  category: "hp" | "gold" | "combat" | "utility" | "class" | "exploration"
): SkillNode[] {
  return SKILL_TREE_NODES.filter((node) => node.category === category);
}

/**
 * Get class-specific nodes
 */
export function getClassNodes(
  characterClass: "swordsman" | "mage" | "summoner"
): SkillNode[] {
  return SKILL_TREE_NODES.filter(
    (node) =>
      node.classRestriction === characterClass || !node.classRestriction
  );
}

/**
 * Category display names and colors
 */
export const CATEGORY_DISPLAY = {
  hp: {
    name: "Life",
    color: "#ef4444", // Red
    bgColor: "rgba(239, 68, 68, 0.2)",
  },
  gold: {
    name: "Wealth",
    color: "#eab308", // Gold
    bgColor: "rgba(234, 179, 8, 0.2)",
  },
  combat: {
    name: "Combat",
    color: "#f97316", // Orange
    bgColor: "rgba(249, 115, 22, 0.2)",
  },
  utility: {
    name: "Utility",
    color: "#22c55e", // Green
    bgColor: "rgba(34, 197, 94, 0.2)",
  },
  class: {
    name: "Class",
    color: "#a855f7", // Purple
    bgColor: "rgba(168, 85, 247, 0.2)",
  },
  exploration: {
    name: "Exploration",
    color: "#3b82f6", // Blue
    bgColor: "rgba(59, 130, 246, 0.2)",
  },
} as const;

/**
 * Tier display configuration
 */
export const TIER_DISPLAY = {
  1: {
    name: "Foundation",
    costRange: "20-30 Souls",
    description: "Basic sanctuary blessings",
  },
  2: {
    name: "Advancement",
    costRange: "40-80 Souls",
    description: "Enhanced powers and abilities",
  },
  3: {
    name: "Mastery",
    costRange: "100-150 Souls",
    description: "Ultimate sanctuary blessings",
  },
} as const;
