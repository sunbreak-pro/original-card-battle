/**
 * Data Barrel Export
 *
 * Re-exports all domain entity data files.
 */

// Battle data
export * from "./battles/initialDeckConfig";
export * from "./battles/buffData";

// Camp data
export * from "./camps/ShopData";
export * from "./camps/SanctuaryData";
export * from "./camps/BlacksmithData";
export * from "./camps/QuestData";
export * from "./camps/RumorData";
export * from "./camps/PromotionData";
export * from "./camps/GuildEnemyData";
export * from "./camps/GameTipsData";
export * from "./camps/CardEncyclopediaData";
export * from "./camps/EnemyEncyclopediaData";

// Character data
export * from "./characters/CharacterClassData";

// Enemy data
export * from "./characters/enemy/enemyDepth1";

// Item data
// ConsumableItemData uses named re-export to avoid conflict with ShopData.CONSUMABLE_ITEMS
export { CONSUMABLE_ITEMS as CONSUMABLE_ITEM_REGISTRY, getConsumableData, getConsumablesByContext, isUsableInBattle } from "./items/ConsumableItemData";
export * from "./items/EquipmentData";

