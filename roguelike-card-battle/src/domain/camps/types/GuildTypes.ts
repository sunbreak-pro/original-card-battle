// Guild facility type definitions

/**
 * Promotion Exam definition
 * Defines the requirements and rewards for class promotion
 */
export interface PromotionExam {
  currentGrade: string; // Current class grade (e.g., "Apprentice Swordsman")
  nextGrade: string; // Next class grade (e.g., "Swordsman")
  requiredCardCount: number; // Minimum number of cards owned
  requiredGold?: number; // Gold cost to attempt exam (optional)
  enemyId: string; // ID of the enemy to fight in the exam
  description: string; // Exam description
  recommendations: {
    hp: number; // Recommended HP
    ap: number; // Recommended AP
  };
  rewards: {
    statBonus: string; // Description of stat bonuses (e.g., "maxHP+10, ATK+5%")
    items?: string[]; // Item IDs to receive on pass
  };
}

/**
 * Rumor effect types
 * Buffs purchased with magic stones that affect exploration
 */
export type RumorEffect =
  | { type: "elite_rate"; value: number } // Increase elite enemy spawn rate
  | { type: "shop_discount"; value: number } // Discount percentage at shop
  | { type: "treasure_rate"; value: number } // Increase treasure find rate
  | { type: "start_bonus"; bonus: string }; // Starting bonus (e.g., "potion_x3")

/**
 * Rumor definition
 * Temporary buffs purchased at the Guild
 */
export interface Rumor {
  id: string;
  name: string;
  description: string;
  cost: number; // Cost in magic stone value
  effect: RumorEffect;
  rarity: "common" | "rare" | "epic";
  icon: string;
  duration?: number; // Number of explorations (if not permanent)
}

/**
 * Quest objective types
 */
export interface QuestObjective {
  type: "defeat" | "collect" | "explore" | "survive";
  target: string; // Target ID (enemy, item, depth)
  required: number; // Required count
  current: number; // Current progress
  description: string; // Human-readable description
}

/**
 * Quest reward definition
 */
export interface QuestReward {
  gold?: number;
  magicStones?: number; // Total magic stone value
  items?: string[]; // Item type IDs
  experience?: number; // Soul Remnants
}

/**
 * Quest definition
 */
export interface Quest {
  id: string;
  title: string;
  description: string;
  type: "daily" | "weekly" | "story";
  requiredGrade: string; // Minimum class grade required
  objectives: QuestObjective[];
  rewards: QuestReward;
  isActive: boolean; // Currently accepted
  isCompleted: boolean; // Completed
  expiresAt?: Date; // Expiration time (for daily/weekly)
}

/**
 * Guild state
 * Tracks active rumors, quests, and exam availability
 */
export interface GuildState {
  activeRumors: string[]; // IDs of active rumor effects
  acceptedQuests: string[]; // IDs of accepted quests
  completedQuests: string[]; // IDs of completed quests
  availableExam: PromotionExam | null; // Current available exam
  lastQuestRefresh?: Date; // Last time quests were refreshed
}

/**
 * Character class types
 */