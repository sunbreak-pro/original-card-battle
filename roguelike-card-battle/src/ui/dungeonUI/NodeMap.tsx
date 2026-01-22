// NodeMap - Dungeon map display component

import { useCallback, useMemo, useEffect } from "react";
import { useGameState } from "../../domain/camps/contexts/GameStateContext";
import { useDungeonRun } from "./DungeonRunContext";
import { neutralTheme } from "../../domain/dungeon/depth/deptManager";
import { DEPTH_DISPLAY_INFO } from "../../domain/dungeon/types/DungeonTypes";
import {
  getNodesByRow,
  getConnectionLines,
  getFloorProgress,
} from "../../domain/dungeon/logic/dungeonLogic";
import MapNode from "./MapNode";
import "./NodeMap.css";

/**
 * NodeMap Component
 * Displays the dungeon floor map with nodes and connections
 */
export function NodeMap() {
  const { returnToCamp, navigateTo, startBattle, gameState } = useGameState();
  const {
    dungeonRun,
    initializeRun,
    selectNodeToVisit,
    completeCurrentNode,
    retreatFromDungeon,
    getCurrentNode,
  } = useDungeonRun();

  // Initialize run if not already active
  useEffect(() => {
    if (!dungeonRun && gameState.depth) {
      initializeRun(gameState.depth);
    }
  }, [dungeonRun, gameState.depth, initializeRun]);

  // Get theme colors (unified neutral theme)
  const theme = useMemo(() => {
    return neutralTheme;
  }, []);

  // Get depth info
  const depthInfo = useMemo(() => {
    const depth = dungeonRun?.selectedDepth || gameState.depth || 1;
    return DEPTH_DISPLAY_INFO[depth];
  }, [dungeonRun, gameState.depth]);

  // Get nodes organized by row
  const nodesByRow = useMemo(() => {
    if (!dungeonRun) return [];
    return getNodesByRow(dungeonRun.currentFloor);
  }, [dungeonRun]);

  // Get connection lines
  const connections = useMemo(() => {
    if (!dungeonRun) return [];
    return getConnectionLines(dungeonRun.currentFloor);
  }, [dungeonRun]);

  // Get progress
  const progress = useMemo(() => {
    if (!dungeonRun) return 0;
    return getFloorProgress(dungeonRun.currentFloor);
  }, [dungeonRun]);

  // Handle node click
  const handleNodeClick = useCallback(
    (nodeId: string) => {
      if (!dungeonRun) return;

      const node = dungeonRun.currentFloor.nodes.find((n) => n.id === nodeId);
      if (!node || node.status !== "available") return;

      // Select the node
      selectNodeToVisit(nodeId);

      // If it's a battle node, start battle
      if (node.type === "battle" || node.type === "elite" || node.type === "boss") {
        // Start battle with callbacks
        startBattle(
          {
            enemyIds: [], // BattleScreen will handle enemy selection
            backgroundType: "dungeon",
            onWin: () => {
              completeCurrentNode("victory");
              navigateTo("dungeon_map");
            },
            onLose: () => {
              completeCurrentNode("defeat");
              returnToCamp();
            },
          },
          "normal"
        );
      } else {
        // For non-battle nodes (event, rest, treasure), complete immediately
        // TODO: Implement event/rest/treasure functionality
        completeCurrentNode("victory");
      }
    },
    [dungeonRun, selectNodeToVisit, startBattle, completeCurrentNode, navigateTo, returnToCamp]
  );

  // Handle retreat
  const handleRetreat = useCallback(() => {
    retreatFromDungeon();
    returnToCamp();
  }, [retreatFromDungeon, returnToCamp]);

  // Handle floor completion
  useEffect(() => {
    if (dungeonRun?.currentFloor.isCompleted) {
      // Floor completed - return to camp for now
      // TODO: Show victory screen or advance to next floor
      returnToCamp();
    }
  }, [dungeonRun?.currentFloor.isCompleted, returnToCamp]);

  // Show loading if run not initialized
  if (!dungeonRun) {
    return (
      <div className="node-map-screen node-map-loading">
        <p>Entering the depths...</p>
      </div>
    );
  }

  const currentNode = getCurrentNode();

  return (
    <div
      className="node-map-screen"
      style={{
        "--map-primary": theme.primary,
        "--map-secondary": theme.secondary,
        "--map-accent": theme.accent,
        "--map-glow": theme.glow,
      } as React.CSSProperties}
    >
      {/* Header */}
      <header className="node-map-header">
        <div className="node-map-title-row">
          <span className="node-map-depth-badge">{depthInfo.japaneseName}</span>
          <h1 className="node-map-title">Floor Map</h1>
        </div>

        <div className="node-map-stats">
          <div className="stat-item">
            <span className="stat-label">Progress</span>
            <span className="stat-value">{progress}%</span>
          </div>
          <div className="stat-item">
            <span className="stat-label">Encounters</span>
            <span className="stat-value">{dungeonRun.encounterCount}</span>
          </div>
        </div>
      </header>

      {/* Map Container */}
      <div className="node-map-container">
        <div className="node-map-grid">
          {/* Connection Lines Layer */}
          <svg className="connection-lines-layer" preserveAspectRatio="none">
            {connections.map((conn) => {
              // Find node positions
              const fromNode = dungeonRun.currentFloor.nodes.find(
                (n) => n.id === conn.fromId
              );
              const toNode = dungeonRun.currentFloor.nodes.find(
                (n) => n.id === conn.toId
              );

              if (!fromNode || !toNode) return null;

              // Calculate positions based on row and column
              const fromX = getNodeX(fromNode.column, nodesByRow[fromNode.row]?.length || 1);
              const fromY = getNodeY(fromNode.row, dungeonRun.currentFloor.totalRows);
              const toX = getNodeX(toNode.column, nodesByRow[toNode.row]?.length || 1);
              const toY = getNodeY(toNode.row, dungeonRun.currentFloor.totalRows);

              return (
                <line
                  key={`${conn.fromId}-${conn.toId}`}
                  className={`connection-line ${conn.isUnlocked ? "unlocked" : ""}`}
                  x1={`${fromX}%`}
                  y1={`${fromY}%`}
                  x2={`${toX}%`}
                  y2={`${toY}%`}
                />
              );
            })}
          </svg>

          {/* Nodes Layer */}
          <div className="nodes-layer">
            {nodesByRow.map((rowNodes, rowIndex) => (
              <div
                key={rowIndex}
                className="node-row"
                style={{
                  top: `${getNodeY(rowIndex, dungeonRun.currentFloor.totalRows)}%`,
                }}
              >
                {rowNodes.map((node) => (
                  <div
                    key={node.id}
                    className="node-wrapper"
                    style={{
                      left: `${getNodeX(node.column, rowNodes.length)}%`,
                    }}
                  >
                    <MapNode
                      node={node}
                      onClick={handleNodeClick}
                      isHighlighted={currentNode?.id === node.id}
                    />
                  </div>
                ))}
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Current Node Info */}
      {currentNode && (
        <div className="current-node-info">
          <span className="current-node-label">Current:</span>
          <span className="current-node-type">
            {currentNode.type.charAt(0).toUpperCase() + currentNode.type.slice(1)}
          </span>
        </div>
      )}

      {/* Action Buttons */}
      <div className="node-map-actions">
        <button className="retreat-button" onClick={handleRetreat}>
          Retreat
        </button>
      </div>

      {/* Back to Camp Button (for testing) */}
      <button className="node-map-back-button" onClick={returnToCamp}>
        ← Back to Camp
      </button>
    </div>
  );
}

/**
 * Calculate X position percentage for a node
 */
function getNodeX(column: number, totalInRow: number): number {
  if (totalInRow === 1) {
    return 50;
  }
  // Spread nodes evenly across the width
  const spacing = 60 / (totalInRow - 1);
  return 20 + column * spacing;
}

/**
 * Calculate Y position percentage for a node
 */
function getNodeY(row: number, totalRows: number): number {
  // Map rows from bottom (start) to top (boss)
  // Row 0 is at the bottom, highest row is at the top
  const padding = 8;
  const availableHeight = 100 - padding * 2;
  const spacing = availableHeight / (totalRows - 1);
  return padding + (totalRows - 1 - row) * spacing;
}

export default NodeMap;
