/**
 * Game Tips Data for Journal
 *
 * Re-exports game tips data from camps.
 * This module provides a stable import path for the journal system.
 */

export {
  GAME_TIPS,
  getAllTips,
  getTipsByCategory,
  searchTips,
  getTipStats,
  CATEGORY_NAMES,
} from "../camps/GameTipsData";
