/**
 * Types Barrel Export
 *
 * Re-exports all centralized type definitions.
 * Note: Some types are intentionally exported from their canonical source only
 * to avoid duplicate export conflicts.
 */

// Card types (canonical source for Depth, Rarity, CardCategory, Card, etc.)
export * from './cardTypes';

// Battle types (BuffDebuffType, DamageResult, PhaseActor, CardExecutionResult, etc.)
export * from './battleTypes';

// Character types (CharacterClass, CardCharacterClass, BattleStats, Player, Enemy, ClassAbility types)
export * from './characterTypes';

// Item types (Item, EquipmentSlot, MagicStones, ConsumableEffect, etc.)
export * from './itemTypes';

// Camp types - exclude Depth (already from cardTypes)
export {
  type FacilityType,
  type GameScreen,
  type BattleMode,
  type BattleConfig,
  type ExplorationLimit,
  type SanctuaryProgress,
  type ResourceTracking,
  type PlayerStatistics,
  type FacilityUnlockState,
  type NodeStatus,
  type ShopTab,
  type ShopCategory,
  type ShopListing,
  type PromotionExam,
  type RumorEffect,
  type Rumor,
  type QuestObjective,
  type QuestReward,
  type Quest,
  type GuildState,
  type BlacksmithTab,
  type QualityUpOption,
  type UpgradeCost,
  type QualityUpConfig,
  type RepairCost,
  type DismantleResult,
  type BlacksmithResult,
  type UpgradePreview,
  type QualityUpgradePreview,
  type SkillCategory,
  type SkillTier,
  type NodeEffectType,
  type NodeEffect,
  type SkillNode,
  type CharacterClassForSanctuary,
  type UnlockResult,
  type SanctuaryEffects,
  type EnemySoulValues,
  type SurvivalMultipliers,
  type LibraryTab,
  type CardEncyclopediaEntry,
  type EnemyEncyclopediaEntry,
  type TipCategory,
  type GameTip,
  type CardFilterOptions,
  type EnemyFilterOptions,
  type StorageState,
  type InventoryState,
  type EquipmentInventoryState,
  type EquipmentSlots,
  type MoveDirection,
  type MoveResult,
  type EquipmentSet,
  type ItemFilter,
  type ItemSortCriteria,
} from './campTypes';

// Dungeon types - exclude Depth, NodeStatus (already exported)
export {
  type NodeType,
  type DungeonNode,
  type DungeonFloor,
  type DungeonRun,
  type NodeTypeConfig,
  type DepthDisplayInfo,
  type NodeCompletionResult,
  type MapGenerationConfig,
} from './dungeonTypes';

// Save types
export * from './saveTypes';
