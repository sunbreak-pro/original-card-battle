/**
 * Game Tips Data
 *
 * Collection of game tips and tutorials organized by category.
 */

import type { GameTip } from "../types/LibraryTypes";

/**
 * All game tips
 */
export const GAME_TIPS: GameTip[] = [
  // Battle tips
  {
    id: "tip_battle_001",
    category: "battle",
    title: "Energy Management",
    content:
      "Each turn you receive 3 energy to play cards. Plan your card plays carefully to maximize damage while maintaining defense. Unused energy does not carry over to the next turn.",
    isUnlocked: true,
  },
  {
    id: "tip_battle_002",
    category: "battle",
    title: "Guard Mechanics",
    content:
      "Guard blocks incoming damage before HP. Guard does not stack between turns - any remaining guard is lost at the start of your turn. Prioritize guard when expecting heavy enemy attacks.",
    isUnlocked: true,
  },
  {
    id: "tip_battle_003",
    category: "battle",
    title: "Speed and Turn Order",
    content:
      "Higher speed means you act first. If your speed is higher than the enemy, you can attack before they do. Use preemptive cards to attack even earlier in the turn.",
    isUnlocked: true,
  },
  {
    id: "tip_battle_004",
    category: "battle",
    title: "Armor Points (AP)",
    content:
      "AP provides permanent damage reduction. When HP damage occurs, AP absorbs some damage. Unlike guard, AP persists across turns but is harder to recover.",
    isUnlocked: true,
  },

  // Card tips
  {
    id: "tip_cards_001",
    category: "cards",
    title: "Card Mastery System",
    content:
      "Using a card increases its mastery level. Higher mastery provides damage bonuses: Lv1 = +20%, Lv2 = +40%, Lv3 = +100%. Build your deck around cards you use frequently.",
    isUnlocked: true,
  },
  {
    id: "tip_cards_002",
    category: "cards",
    title: "Card Cost Reduction",
    content:
      "Some cards reduce the cost of the next card played. Chaining these cards together allows you to play more cards per turn. Look for cards with 'cost reduction' effects.",
    isUnlocked: true,
  },
  {
    id: "tip_cards_003",
    category: "cards",
    title: "Multi-Hit Attacks",
    content:
      "Multi-hit cards deal damage multiple times. Each hit can trigger on-hit effects and is calculated separately against enemy defense. Great for breaking through guard.",
    isUnlocked: true,
  },

  // Exploration tips
  {
    id: "tip_explore_001",
    category: "exploration",
    title: "Dungeon Node Types",
    content:
      "The dungeon map contains different node types: Battle nodes for combat, Rest nodes for healing, Event nodes for random encounters, and Treasure nodes for loot.",
    isUnlocked: true,
  },
  {
    id: "tip_explore_002",
    category: "exploration",
    title: "Exploration Limit",
    content:
      "You have a limited number of explorations before needing to rest. Plan your dungeon runs carefully and return to camp before running out of explorations.",
    isUnlocked: true,
  },
  {
    id: "tip_explore_003",
    category: "exploration",
    title: "Resource Management",
    content:
      "Gold and magic stones gained during exploration are temporary until you return to camp. If you die, you lose all exploration resources. Return safely to keep your loot.",
    isUnlocked: true,
  },

  // Class tips
  {
    id: "tip_class_001",
    category: "class",
    title: "Sword Energy (Swordsman)",
    content:
      "The Swordsman builds Sword Energy by playing attack cards. Spend accumulated energy to unleash powerful finisher moves. Balance energy building and spending for optimal damage.",
    isUnlocked: true,
  },
  {
    id: "tip_class_002",
    category: "class",
    title: "Guild Promotions",
    content:
      "Complete guild exams to increase your rank. Higher ranks unlock better rewards, new cards, and access to deeper dungeon levels. Check the guild for available promotions.",
    isUnlocked: true,
  },

  // General tips
  {
    id: "tip_general_001",
    category: "general",
    title: "Sanctuary Upgrades",
    content:
      "The Sanctuary offers permanent upgrades using souls. Souls are gained from defeating enemies and are transferred to your total when you return safely. Unlock nodes to grow stronger.",
    isUnlocked: true,
  },
  {
    id: "tip_general_002",
    category: "general",
    title: "Blacksmith Services",
    content:
      "The Blacksmith can upgrade, repair, and dismantle equipment. Upgrading improves stats, repairing restores durability, and dismantling recovers materials from unwanted items.",
    isUnlocked: true,
  },
  {
    id: "tip_general_003",
    category: "general",
    title: "Shop Inventory",
    content:
      "The shop sells consumables, equipment, and cards. Inventory refreshes periodically. Check back regularly for new items and stock up on healing items before difficult runs.",
    isUnlocked: true,
  },
];

/**
 * Get all game tips
 */
export function getAllTips(): GameTip[] {
  return GAME_TIPS;
}

/**
 * Get tips filtered by category
 */
export function getTipsByCategory(category: string | null): GameTip[] {
  if (!category) {
    return GAME_TIPS;
  }

  return GAME_TIPS.filter((tip) => tip.category === category);
}

/**
 * Search tips by title or content
 */
export function searchTips(searchText: string): GameTip[] {
  const lowerSearch = searchText.toLowerCase();

  if (!searchText) {
    return GAME_TIPS;
  }

  return GAME_TIPS.filter(
    (tip) =>
      tip.title.toLowerCase().includes(lowerSearch) ||
      tip.content.toLowerCase().includes(lowerSearch)
  );
}

/**
 * Get tip statistics
 */
export function getTipStats(): {
  total: number;
  byCategory: Record<string, number>;
} {
  const byCategory: Record<string, number> = {};

  GAME_TIPS.forEach((tip) => {
    byCategory[tip.category] = (byCategory[tip.category] || 0) + 1;
  });

  return {
    total: GAME_TIPS.length,
    byCategory,
  };
}

/**
 * Category display names
 */
export const CATEGORY_NAMES: Record<string, string> = {
  battle: "Battle",
  cards: "Cards",
  exploration: "Exploration",
  class: "Class",
  general: "General",
};
