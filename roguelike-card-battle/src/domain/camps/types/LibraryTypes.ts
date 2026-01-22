/**
 * Library Types
 *
 * Type definitions for the Library facility including
 * card encyclopedia, enemy bestiary, and game tips.
 */

import type { Card } from "../../cards/type/cardType";
import type { Enemy } from "../../characters/type/enemyType";

/**
 * Library tab types
 */
export type LibraryTab = "cards" | "enemies" | "tips";

/**
 * Card encyclopedia entry
 */
export interface CardEncyclopediaEntry {
  card: Card;
  isUnlocked: boolean;
  timesUsed: number;
  firstObtainedDate?: string;
}

/**
 * Enemy encyclopedia entry
 */
export interface EnemyEncyclopediaEntry {
  enemy: Enemy;
  isEncountered: boolean;
  timesDefeated: number;
  firstEncounteredDate?: string;
}

/**
 * Game tip categories
 */
export type TipCategory = "battle" | "cards" | "exploration" | "class" | "general";

/**
 * Game tip entry
 */
export interface GameTip {
  id: string;
  category: TipCategory;
  title: string;
  content: string;
  isUnlocked: boolean;
}

/**
 * Filter options for card encyclopedia
 */
export interface CardFilterOptions {
  rarity: string | null;
  category: string | null;
  characterClass: string | null;
  searchText: string;
}

/**
 * Filter options for enemy encyclopedia
 */
export interface EnemyFilterOptions {
  depth: number | null;
  isBoss: boolean | null;
  searchText: string;
}
