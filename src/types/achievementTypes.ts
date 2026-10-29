/**
 * Achievement Types
 *
 * Type definitions for the achievement system.
 */

// ============================================================================
// Achievement Categories
// ============================================================================

export type AchievementCategory =
  | 'battle'      // 戦闘関連
  | 'exploration' // 探索関連
  | 'collection'  // 収集関連
  | 'progression' // 進行関連
  | 'special';    // 特殊

export type AchievementRarity =
  | 'common'      // 一般
  | 'uncommon'    // 珍しい
  | 'rare'        // レア
  | 'epic'        // エピック
  | 'legendary';  // 伝説

// ============================================================================
// Achievement Definition
// ============================================================================

export interface Achievement {
  id: string;
  name: string;
  description: string;
  icon: string;           // Emoji or icon path
  category: AchievementCategory;
  rarity: AchievementRarity;
  hidden?: boolean;       // Hidden until unlocked
  condition?: string;     // Display condition text
}
