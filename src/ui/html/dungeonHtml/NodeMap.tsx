// NodeMap - Dungeon map display component

import { useCallback, useMemo, useEffect, useState } from "react";
import { useGameState } from "@/contexts/GameStateContext";
import { usePlayer } from "@/contexts/PlayerContext";
import { useResources } from "@/contexts/ResourceContext";
import { useInventory } from "@/contexts/InventoryContext";
import { useDungeonRun } from "@/contexts/DungeonRunContext";
import { useGuild } from "@/contexts/GuildContext";
import { incrementBattleCount } from "@/domain/camps/logic/shopStockLogic";
import { neutralTheme } from "@/domain/dungeon/depth/depthManager";
import { DEPTH_DISPLAY_INFO } from "@/constants/dungeonConstants";
import {
  getNodesByRow,
  getConnectionLines,
  getFloorProgress,
} from "@/domain/dungeon/logic/dungeonLogic";
import { processNodeEvent } from "@/domain/dungeon/logic/nodeEventLogic";
import type { NodeEventResult } from "@/domain/dungeon/logic/nodeEventLogic";
import MapNode from "./MapNode";
import "./NodeMap.css";

/**
 * NodeMap Component
 * Displays the dungeon floor map with nodes and connections
 */
export function NodeMap() {
  const { returnToCamp, navigateTo, startBattle, gameState } = useGameState();
  const { playerData, runtimeState, updateHp, updatePlayerData } = usePlayer();
  const { addGold, addMagicStones } = useResources();
  const { addItemToInventory } = useInventory();
  const [eventResult, setEventResult] = useState<NodeEventResult | null>(null);
  const {
    dungeonRun,
    initializeRun,
    selectNodeToVisit,
    completeCurrentNode,
    advanceToNextFloor,
    retreatFromDungeon,
    getCurrentNode,
  } = useDungeonRun();
  const { updateQuestProgress } = useGuild();
  const [floorClearModal, setFloorClearModal] = useState<
    "floor" | "depth" | null
  >(null);

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
      if (
        node.type === "battle" ||
        node.type === "elite" ||
        node.type === "boss"
      ) {
        // Determine encounter size from node type
        const enemyType =
          node.type === "battle"
            ? "double"
            : node.type === "elite"
              ? "three"
              : node.type; // "boss" passes through

        // Start battle with callbacks
        startBattle(
          {
            enemyIds: [], // BattleScreen will handle enemy selection
            backgroundType: "dungeon",
            enemyType: enemyType as "single" | "double" | "three" | "boss",
            onWin: () => {
              completeCurrentNode("victory");
              // Track quest progress: node exploration
              updateQuestProgress("explore", "any", 1);
              // Increment shop stock battle counter using functional updater
              updatePlayerData((prev) => {
                const currentStock = prev.progression.shopStockState;
                if (!currentStock) return {};
                return {
                  progression: {
                    ...prev.progression,
                    shopStockState: incrementBattleCount(currentStock),
                  },
                };
              });
              navigateTo("dungeon_map");
            },
            onLose: () => {
              completeCurrentNode("defeat");
              returnToCamp();
            },
          },
          "normal",
        );
      } else {
        // Non-battle node: process event and show modal
        const nodeType = node.type as "rest" | "treasure" | "event";
        const currentHp = runtimeState.currentHp;
        const maxHp = playerData.persistent.baseMaxHp;
        const result = processNodeEvent(nodeType, currentHp, maxHp);
        setEventResult(result);
      }
    },
    [
      dungeonRun,
      selectNodeToVisit,
      startBattle,
      completeCurrentNode,
      navigateTo,
      returnToCamp,
      runtimeState.currentHp,
      playerData.persistent.baseMaxHp,
      updatePlayerData,
      updateQuestProgress,
    ],
  );

  // Handle event modal confirm - apply rewards and complete node
  const handleEventConfirm = useCallback(() => {
    if (!eventResult) return;

    const { rewards } = eventResult;

    // Apply HP restore (can be negative for traps)
    if (rewards.hpRestore) {
      const currentHp = runtimeState.currentHp;
      const maxHp = playerData.persistent.baseMaxHp;
      const newHp = Math.max(0, Math.min(currentHp + rewards.hpRestore, maxHp));
      updateHp(newHp);
    }
    if (rewards.hpRestorePercent && !rewards.hpRestore) {
      const maxHp = playerData.persistent.baseMaxHp;
      const restoreAmount = Math.floor(maxHp * rewards.hpRestorePercent);
      const currentHp = runtimeState.currentHp;
      const newHp = Math.min(currentHp + restoreAmount, maxHp);
      updateHp(newHp);
    }

    // Apply gold reward
    if (rewards.gold) {
      addGold(rewards.gold, false); // exploration gold
    }

    // Apply magic stones reward
    if (rewards.magicStones) {
      addMagicStones(rewards.magicStones, false); // exploration stones
    }

    // Apply item rewards
    if (rewards.items) {
      for (const item of rewards.items) {
        addItemToInventory(item);
      }
    }

    completeCurrentNode("victory");

    // Track quest progress: node exploration
    updateQuestProgress("explore", "any", 1);
    if (eventResult.type === "treasure") {
      updateQuestProgress("explore", "treasure", 1);
    }

    setEventResult(null);
  }, [
    eventResult,
    runtimeState.currentHp,
    playerData.persistent.baseMaxHp,
    updateHp,
    addGold,
    addMagicStones,
    addItemToInventory,
    completeCurrentNode,
    updateQuestProgress,
  ]);

  // Handle floor completion
  useEffect(() => {
    if (dungeonRun?.currentFloor.isCompleted) {
      if (dungeonRun.floorNumber >= 5) {
        // eslint-disable-next-line react-hooks/set-state-in-effect -- one-time modal show on floor completion event
        setFloorClearModal("depth");
      } else {
        setFloorClearModal("floor");
      }
    }
  }, [dungeonRun?.currentFloor.isCompleted, dungeonRun?.floorNumber]);

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
      style={
        {
          "--map-primary": theme.primary,
          "--map-secondary": theme.secondary,
          "--map-accent": theme.accent,
          "--map-glow": theme.glow,
        } as React.CSSProperties
      }
    >
      {/* Header */}
      <header className="node-map-header">
        <div className="node-map-title-row">
          <span className="node-map-depth-badge">{depthInfo.japaneseName}</span>
          <h1 className="node-map-title">Floor {dungeonRun.floorNumber} / 5</h1>
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
                (n) => n.id === conn.fromId,
              );
              const toNode = dungeonRun.currentFloor.nodes.find(
                (n) => n.id === conn.toId,
              );

              if (!fromNode || !toNode) return null;

              // Calculate positions based on row and column
              const fromX = getNodeX(
                fromNode.column,
                nodesByRow[fromNode.row]?.length || 1,
              );
              const fromY = getNodeY(
                fromNode.row,
                dungeonRun.currentFloor.totalRows,
              );
              const toX = getNodeX(
                toNode.column,
                nodesByRow[toNode.row]?.length || 1,
              );
              const toY = getNodeY(
                toNode.row,
                dungeonRun.currentFloor.totalRows,
              );

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
            {currentNode.type.charAt(0).toUpperCase() +
              currentNode.type.slice(1)}
          </span>
        </div>
      )}

      {/* Floor Clear Modal */}
      {floorClearModal && (
        <div className="node-event-overlay">
          <div className="node-event-modal">
            {floorClearModal === "floor" ? (
              <>
                <h2 className="node-event-title">
                  Floor {dungeonRun.floorNumber} Clear!
                </h2>
                <p className="node-event-description">
                  Next floor awaits. Prepare for stronger enemies.
                </p>
                <button
                  className="node-event-confirm"
                  onClick={() => {
                    setFloorClearModal(null);
                    advanceToNextFloor();
                  }}
                >
                  Next Floor
                </button>
              </>
            ) : (
              <>
                <h2 className="node-event-title">Depth Clear!</h2>
                <p className="node-event-description">
                  All 5 floors conquered. Returning to camp with your spoils.
                </p>
                <button
                  className="node-event-confirm"
                  onClick={() => {
                    setFloorClearModal(null);
                    // Track quest progress: survived a full dungeon run
                    updateQuestProgress("survive", "run", 1);
                    retreatFromDungeon();
                    returnToCamp();
                  }}
                >
                  Return to Camp
                </button>
              </>
            )}
          </div>
        </div>
      )}

      {/* Node Event Modal */}
      {eventResult && (
        <div className="node-event-overlay">
          <div className="node-event-modal">
            <h2 className="node-event-title">{eventResult.title}</h2>
            <p className="node-event-description">{eventResult.description}</p>
            <ul className="node-event-rewards">
              {eventResult.rewards.hpRestore != null &&
                eventResult.rewards.hpRestore > 0 && (
                  <li className="reward-item reward-positive">
                    HP +{eventResult.rewards.hpRestore}
                  </li>
                )}
              {eventResult.rewards.hpRestore != null &&
                eventResult.rewards.hpRestore < 0 && (
                  <li className="reward-item reward-negative">
                    HP {eventResult.rewards.hpRestore}
                  </li>
                )}
              {eventResult.rewards.hpRestorePercent != null &&
                !eventResult.rewards.hpRestore && (
                  <li className="reward-item reward-positive">
                    HP +{Math.floor(eventResult.rewards.hpRestorePercent * 100)}
                    %
                  </li>
                )}
              {eventResult.rewards.gold != null &&
                eventResult.rewards.gold > 0 && (
                  <li className="reward-item reward-gold">
                    {eventResult.rewards.gold} ゴールド
                  </li>
                )}
              {eventResult.rewards.magicStones?.small != null &&
                eventResult.rewards.magicStones.small > 0 && (
                  <li className="reward-item reward-stone">
                    小魔石 x{eventResult.rewards.magicStones.small}
                  </li>
                )}
              {eventResult.rewards.magicStones?.medium != null &&
                eventResult.rewards.magicStones.medium > 0 && (
                  <li className="reward-item reward-stone">
                    中魔石 x{eventResult.rewards.magicStones.medium}
                  </li>
                )}
              {eventResult.rewards.magicStones?.large != null &&
                eventResult.rewards.magicStones.large > 0 && (
                  <li className="reward-item reward-stone">
                    大魔石 x{eventResult.rewards.magicStones.large}
                  </li>
                )}
              {eventResult.rewards.items != null &&
                eventResult.rewards.items.length > 0 && (
                  <li className="reward-item reward-item-drop">
                    アイテム x{eventResult.rewards.items.length}
                  </li>
                )}
            </ul>
            <button className="node-event-confirm" onClick={handleEventConfirm}>
              続ける
            </button>
          </div>
        </div>
      )}
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
