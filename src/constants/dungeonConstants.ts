/**
 * Dungeon Constants
 *
 * Centralized dungeon-related constants including node configuration,
 * depth display info, and map generation defaults.
 */

import type { Depth } from '@/types/campTypes';
import type { NodeType, NodeTypeConfig, DepthDisplayInfo, MapGenerationConfig } from '@/types/dungeonTypes';

// ============================================================
// Node Type Configuration
// ============================================================

/** Map of node types to their display configuration */
export const NODE_TYPE_CONFIG: Record<NodeType, NodeTypeConfig> = {
  battle: {
    icon: "⚔️",
    label: "Battle",
    description: "Encounter enemies",
  },
  elite: {
    icon: "💀",
    label: "Elite",
    description: "Powerful enemy with better rewards",
  },
  boss: {
    icon: "👑",
    label: "Boss",
    description: "Floor boss - defeat to progress",
  },
  event: {
    icon: "❓",
    label: "Event",
    description: "Unknown encounter",
  },
  rest: {
    icon: "🏕️",
    label: "Rest",
    description: "Restore health",
  },
  treasure: {
    icon: "📦",
    label: "Treasure",
    description: "Find rewards",
  },
};

// ============================================================
// Depth Display Information
// ============================================================

/** Depth display configuration */
export const DEPTH_DISPLAY_INFO: Record<Depth, DepthDisplayInfo> = {
  1: {
    depth: 1,
    name: "Corruption",
    japaneseName: "腐食",
    description: "Where decay begins its embrace",
    recommendedLevel: 1,
  },
  2: {
    depth: 2,
    name: "Frenzy",
    japaneseName: "狂乱",
    description: "Madness lurks in every shadow",
    recommendedLevel: 5,
  },
  3: {
    depth: 3,
    name: "Chaos",
    japaneseName: "混沌",
    description: "Order loses all meaning",
    recommendedLevel: 10,
  },
  4: {
    depth: 4,
    name: "Void",
    japaneseName: "虚無",
    description: "Where existence fades",
    recommendedLevel: 15,
  },
  5: {
    depth: 5,
    name: "Abyss",
    japaneseName: "深淵",
    description: "The endless darkness awaits",
    recommendedLevel: 20,
  },
};

// ============================================================
// Map Generation Configuration
// ============================================================

/** Default map generation configuration */
export const DEFAULT_MAP_CONFIG: MapGenerationConfig = {
  totalRows: 7,
  nodesPerRow: [1, 2, 2, 2, 2, 2, 1], // Start, middle, boss
  eliteChance: 0.15,
  eventChance: 0.1,
  restChance: 0.1,
  treasureChance: 0.05,
};
