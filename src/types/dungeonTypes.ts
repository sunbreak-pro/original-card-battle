/**
 * Dungeon Type Definitions
 *
 * Centralized dungeon-related types including:
 * - Node and map types
 * - Dungeon run state
 * - Map generation configuration
 */

import type { Depth } from './cardTypes';
import type { NodeStatus } from './campTypes';

// Re-export for convenience
export type { Depth } from './cardTypes';
export type { NodeStatus } from './campTypes';

// ============================================================
// Node Types
// ============================================================

export type NodeType =
  | "battle"
  | "elite"
  | "boss"
  | "event"
  | "rest"
  | "treasure";

export interface DungeonNode {
  id: string;
  type: NodeType;
  row: number;
  column: number;
  connections: string[];
  status: NodeStatus;
}

export interface DungeonFloor {
  depth: Depth;
  nodes: DungeonNode[];
  currentNodeId: string | null;
  isCompleted: boolean;
  totalRows: number;
}

export interface DungeonRun {
  runId: string;
  selectedDepth: Depth;
  currentFloor: DungeonFloor;
  encounterCount: number;
  isActive: boolean;
  startedAt: number;
}

// ============================================================
// Display Types
// ============================================================

export interface NodeTypeConfig {
  icon: string;
  label: string;
  description: string;
}

export interface DepthDisplayInfo {
  depth: Depth;
  name: string;
  japaneseName: string;
  description: string;
  recommendedLevel: number;
}

export type NodeCompletionResult = "victory" | "defeat" | "retreat";

// ============================================================
// Map Generation Types
// ============================================================

export interface MapGenerationConfig {
  totalRows: number;
  nodesPerRow: number[];
  eliteChance: number;
  eventChance: number;
  restChance: number;
  treasureChance: number;
}
