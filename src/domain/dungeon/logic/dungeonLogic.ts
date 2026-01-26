// Dungeon map generation and progression logic

import type { Depth } from "../../camps/types/CampTypes";
import type {
  DungeonNode,
  DungeonFloor,
  DungeonRun,
  NodeType,
  NodeStatus,
  NodeCompletionResult,
  MapGenerationConfig,
} from "../types/DungeonTypes";
import { DEFAULT_MAP_CONFIG } from "../types/DungeonTypes";

/**
 * Generate a unique ID for nodes
 */
function generateNodeId(row: number, column: number): string {
  return `node-${row}-${column}`;
}

/**
 * Generate a unique ID for dungeon runs
 */
export function generateRunId(): string {
  return `run-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`;
}

/**
 * Determine node type based on row position and randomness
 */
export function getNodeTypeForRow(
  row: number,
  totalRows: number,
  config: MapGenerationConfig = DEFAULT_MAP_CONFIG
): NodeType {
  // First row is always a battle (entry point)
  if (row === 0) {
    return "battle";
  }

  // Last row is always the boss
  if (row === totalRows - 1) {
    return "boss";
  }

  // Middle rows have varied types
  const random = Math.random();
  let cumulative = 0;

  cumulative += config.eliteChance;
  if (random < cumulative) {
    return "elite";
  }

  cumulative += config.eventChance;
  if (random < cumulative) {
    return "event";
  }

  cumulative += config.restChance;
  if (random < cumulative) {
    return "rest";
  }

  cumulative += config.treasureChance;
  if (random < cumulative) {
    return "treasure";
  }

  // Default to battle
  return "battle";
}

/**
 * Generate connections from one row to the next
 * Creates a web-like structure with forward connections
 */
function generateConnections(
  currentRowNodes: DungeonNode[],
  nextRowNodes: DungeonNode[]
): void {
  // Each node connects to at least one node in the next row
  currentRowNodes.forEach((node, nodeIndex) => {
    const nextRowSize = nextRowNodes.length;

    if (nextRowSize === 1) {
      // All nodes connect to the single next node (boss)
      node.connections.push(nextRowNodes[0].id);
    } else if (currentRowNodes.length === 1) {
      // Single node connects to all next nodes
      nextRowNodes.forEach((nextNode) => {
        node.connections.push(nextNode.id);
      });
    } else {
      // Map node index to next row indices
      // Each node connects to the node directly ahead and optionally adjacent
      const directIndex = Math.min(
        nodeIndex,
        nextRowSize - 1
      );
      node.connections.push(nextRowNodes[directIndex].id);

      // Add diagonal connection with 50% chance
      if (Math.random() > 0.5) {
        const adjacentIndex =
          nodeIndex < currentRowNodes.length / 2
            ? directIndex + 1
            : directIndex - 1;

        if (adjacentIndex >= 0 && adjacentIndex < nextRowSize) {
          const adjacentId = nextRowNodes[adjacentIndex].id;
          if (!node.connections.includes(adjacentId)) {
            node.connections.push(adjacentId);
          }
        }
      }
    }
  });
}

/**
 * Generate a floor map with nodes and connections
 */
export function generateFloorMap(
  depth: Depth,
  config: MapGenerationConfig = DEFAULT_MAP_CONFIG
): DungeonFloor {
  const { totalRows, nodesPerRow } = config;
  const allNodes: DungeonNode[] = [];
  const rowArrays: DungeonNode[][] = [];

  // Generate nodes for each row
  for (let row = 0; row < totalRows; row++) {
    const nodeCount = nodesPerRow[row] || 1;
    const rowNodes: DungeonNode[] = [];

    for (let col = 0; col < nodeCount; col++) {
      const node: DungeonNode = {
        id: generateNodeId(row, col),
        type: getNodeTypeForRow(row, totalRows, config),
        row,
        column: col,
        connections: [],
        status: row === 0 ? "available" : "locked",
      };
      rowNodes.push(node);
      allNodes.push(node);
    }

    rowArrays.push(rowNodes);
  }

  // Generate connections between rows
  for (let row = 0; row < totalRows - 1; row++) {
    generateConnections(rowArrays[row], rowArrays[row + 1]);
  }

  return {
    depth,
    nodes: allNodes,
    currentNodeId: null,
    isCompleted: false,
    totalRows,
  };
}

/**
 * Get all available nodes that can be selected
 */
export function getAvailableNodes(floor: DungeonFloor): DungeonNode[] {
  return floor.nodes.filter((node) => node.status === "available");
}

/**
 * Get a specific node by ID
 */
export function getNodeById(
  floor: DungeonFloor,
  nodeId: string
): DungeonNode | undefined {
  return floor.nodes.find((node) => node.id === nodeId);
}

/**
 * Update node status (immutable)
 */
function updateNodeStatus(
  nodes: DungeonNode[],
  nodeId: string,
  newStatus: NodeStatus
): DungeonNode[] {
  return nodes.map((node) =>
    node.id === nodeId ? { ...node, status: newStatus } : node
  );
}

/**
 * Select a node and update the floor state
 * Marks the node as current and updates availability
 */
export function selectNode(
  floor: DungeonFloor,
  nodeId: string
): DungeonFloor {
  const node = getNodeById(floor, nodeId);
  if (!node || node.status !== "available") {
    return floor;
  }

  // Mark all available nodes that weren't selected as skipped
  let updatedNodes = floor.nodes.map((n) => {
    if (n.status === "available" && n.id !== nodeId) {
      return { ...n, status: "skipped" as NodeStatus };
    }
    return n;
  });

  // Mark the selected node as current
  updatedNodes = updateNodeStatus(updatedNodes, nodeId, "current");

  return {
    ...floor,
    nodes: updatedNodes,
    currentNodeId: nodeId,
  };
}

/**
 * Complete the current node and unlock next nodes
 */
export function completeNode(
  floor: DungeonFloor,
  nodeId: string,
  result: NodeCompletionResult
): DungeonFloor {
  const node = getNodeById(floor, nodeId);
  if (!node || node.status !== "current") {
    return floor;
  }

  // If defeated or retreated, don't progress
  if (result === "defeat" || result === "retreat") {
    return {
      ...floor,
      isCompleted: false,
    };
  }

  // Mark current node as completed
  let updatedNodes = updateNodeStatus(floor.nodes, nodeId, "completed");

  // Check if this was the boss (last row)
  const isBoss = node.row === floor.totalRows - 1;
  if (isBoss) {
    return {
      ...floor,
      nodes: updatedNodes,
      currentNodeId: null,
      isCompleted: true,
    };
  }

  // Unlock connected nodes
  node.connections.forEach((connectedId) => {
    const connectedNode = floor.nodes.find((n) => n.id === connectedId);
    if (connectedNode && connectedNode.status === "locked") {
      updatedNodes = updateNodeStatus(updatedNodes, connectedId, "available");
    }
  });

  return {
    ...floor,
    nodes: updatedNodes,
    currentNodeId: null,
  };
}

/**
 * Initialize a new dungeon run
 */
export function initializeDungeonRun(depth: Depth): DungeonRun {
  return {
    runId: generateRunId(),
    selectedDepth: depth,
    currentFloor: generateFloorMap(depth),
    encounterCount: 0,
    isActive: true,
    startedAt: Date.now(),
  };
}

/**
 * Get progress percentage for the current floor
 */
export function getFloorProgress(floor: DungeonFloor): number {
  const completedNodes = floor.nodes.filter(
    (node) => node.status === "completed"
  ).length;
  const totalNodes = floor.nodes.length;
  return Math.round((completedNodes / totalNodes) * 100);
}

/**
 * Check if the floor can be continued (has available nodes or current node)
 */
export function canContinueFloor(floor: DungeonFloor): boolean {
  if (floor.isCompleted) return false;
  const hasAvailable = floor.nodes.some((node) => node.status === "available");
  const hasCurrent = floor.nodes.some((node) => node.status === "current");
  return hasAvailable || hasCurrent;
}

/**
 * Get nodes organized by row for rendering
 */
export function getNodesByRow(floor: DungeonFloor): DungeonNode[][] {
  const rowMap = new Map<number, DungeonNode[]>();

  floor.nodes.forEach((node) => {
    const row = rowMap.get(node.row) || [];
    row.push(node);
    rowMap.set(node.row, row);
  });

  // Convert to array sorted by row
  const rows: DungeonNode[][] = [];
  for (let i = 0; i < floor.totalRows; i++) {
    rows.push(rowMap.get(i) || []);
  }

  return rows;
}

/**
 * Get all connection lines for rendering
 */
export interface ConnectionLine {
  fromId: string;
  toId: string;
  isUnlocked: boolean;
}

export function getConnectionLines(floor: DungeonFloor): ConnectionLine[] {
  const lines: ConnectionLine[] = [];
  const completedIds = new Set(
    floor.nodes.filter((n) => n.status === "completed").map((n) => n.id)
  );

  floor.nodes.forEach((node) => {
    node.connections.forEach((toId) => {
      lines.push({
        fromId: node.id,
        toId,
        isUnlocked: completedIds.has(node.id),
      });
    });
  });

  return lines;
}
