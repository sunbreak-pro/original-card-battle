/**
 * Journal Encyclopedia Data Index
 *
 * Barrel export for all journal encyclopedia data modules.
 */

// Card Encyclopedia
export {
  getAllCards,
  createCardEncyclopediaEntries,
  getCardStats,
  getAllTags,
} from "./CardEncyclopediaData";

// Enemy Encyclopedia
export {
  getAllEnemies,
  getEnemiesByDepth,
  createEnemyEncyclopediaEntries,
  isBossEnemy,
  getEnemiesByBossStatus,
  searchEnemies,
  getEnemyStats,
} from "./EnemyEncyclopediaData";

// Game Tips
export {
  GAME_TIPS,
  getAllTips,
  getTipsByCategory,
  searchTips,
  getTipStats,
  CATEGORY_NAMES,
} from "./GameTipsData";

// Equipment Encyclopedia
export {
  getAllEquipment,
  getEquipmentByClass,
  getEquipmentBySlot,
  createEquipmentEncyclopediaEntries,
  getEquipmentStats,
  searchEquipment,
  SLOT_NAMES,
  RARITY_NAMES,
  CLASS_NAMES,
} from "./EquipmentEncyclopediaData";
export type {
  EquipmentEncyclopediaEntry,
  EquipmentFilterOptions,
} from "./EquipmentEncyclopediaData";

// Event Encyclopedia
export {
  GAME_EVENTS,
  getAllEvents,
  getEventsByCategory,
  createEventEncyclopediaEntries,
  getEventStats,
  searchEvents,
  EVENT_CATEGORY_NAMES,
} from "./EventEncyclopediaData";
export type {
  EventCategory,
  EventEntry,
  EventEncyclopediaEntry,
} from "./EventEncyclopediaData";
