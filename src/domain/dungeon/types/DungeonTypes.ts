// Dungeon node and map type definitions

import type { Depth } from "../../camps/types/CampTypes";

/**
 * Node types in the dungeon map
 */
export type NodeType =
  | "battle" // Normal enemy encounter
  | "elite" // Elite/mini-boss encounter
  | "boss" // Boss encounter (end of floor)
  | "event" // Random event
  | "rest" // Rest/heal point
  | "treasure"; // Treasure room

/**
 * Node status for progression tracking
 */
export type NodeStatus =
  | "locked" // Cannot be selected (path not unlocked)
  | "available" // Can be selected (on current path)
  | "current" // Currently at this node
  | "completed" // Already cleared
  | "skipped"; // Passed but not visited

/**
 * Individual node in the dungeon map
 */
export interface DungeonNode {
  id: string;
  type: NodeType;
  row: number; // 0 = start, higher = deeper
  column: number; // Horizontal position
  connections: string[]; // IDs of nodes this connects to
  status: NodeStatus;
}

/**
 * A single floor of the dungeon
 */
export interface DungeonFloor {
  depth: Depth;
  nodes: DungeonNode[];
  currentNodeId: string | null;
  isCompleted: boolean;
  totalRows: number;
}

/**
 * Complete dungeon run state
 */
export interface DungeonRun {
  runId: string;
  selectedDepth: Depth;
  currentFloor: DungeonFloor;
  encounterCount: number;
  isActive: boolean;
  startedAt: number; // Timestamp
}

/**
 * Node type configuration for display
 */
export interface NodeTypeConfig {
  icon: string;
  label: string;
  description: string;
}

/**
 * Map of node types to their display configuration
 */
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

/**
 * Depth information for display
 */
export interface DepthDisplayInfo {
  depth: Depth;
  name: string;
  japaneseName: string;
  description: string;
  recommendedLevel: number;
}

/**
 * Depth display configuration
 */
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

/**
 * Result of completing a dungeon node
 */
export type NodeCompletionResult = "victory" | "defeat" | "retreat";

/**
 * Map generation configuration
 */
export interface MapGenerationConfig {
  totalRows: number;
  nodesPerRow: number[];
  eliteChance: number;
  eventChance: number;
  restChance: number;
  treasureChance: number;
}

/**
 * Default map generation configuration
 */
export const DEFAULT_MAP_CONFIG: MapGenerationConfig = {
  totalRows: 7,
  nodesPerRow: [1, 2, 2, 2, 2, 2, 1], // Start, middle, boss
  eliteChance: 0.15,
  eventChance: 0.1,
  restChance: 0.1,
  treasureChance: 0.05,
};
