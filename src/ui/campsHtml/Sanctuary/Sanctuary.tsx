// Sanctuary main component

import { useState, useCallback } from "react";
import { useGameState } from "../../../contexts/GameStateContext";
import { usePlayer } from "../../../contexts/PlayerContext";
import type {
  SkillNode,
  NodeStatus,
  CharacterClassForSanctuary as CharacterClass,
} from '@/types/campTypes';
import {
  getNodeStatus,
  unlockNode,
  getProgressStats,
  calculateTotalEffects,
} from "../../../domain/camps/logic/sanctuaryLogic";
import SkillTree from "./SkillTree";
import NodeDetailPanel from "./NodeDetailPanel";
import FacilityHeader from "../../componentsHtml/FacilityHeader";
import "../../css/camps/Sanctuary.css";

export const Sanctuary = () => {
  const { returnToCamp } = useGameState();
  const { playerData, updatePlayerData } = usePlayer();

  const [selectedNode, setSelectedNode] = useState<SkillNode | null>(null);
  const [justUnlockedId, setJustUnlockedId] = useState<string | null>(null);
  const [notification, setNotification] = useState<{
    message: string;
    type: "success" | "error";
  } | null>(null);

  // Get player class from player data
  const playerClass: CharacterClass = playerData.persistent.playerClass;

  // Get sanctuary progress
  const { progression } = playerData;
  const sanctuaryProgress = progression.sanctuaryProgress;

  // Calculate effects
  const effects = calculateTotalEffects(sanctuaryProgress);

  // Get progress statistics
  const stats = getProgressStats(sanctuaryProgress, playerClass);

  // Handle node selection
  const handleNodeSelect = useCallback((node: SkillNode) => {
    setSelectedNode(node);
    setJustUnlockedId(null);
  }, []);

  // Handle node unlock
  const handleUnlock = useCallback(
    (node: SkillNode) => {
      const result = unlockNode(node, sanctuaryProgress, playerClass);

      if (result.success && result.newProgress) {
        // Update player state
        updatePlayerData({
          progression: {
            ...progression,
            sanctuaryProgress: result.newProgress,
          },
        });

        // Show success notification
        setNotification({
          message: result.message,
          type: "success",
        });

        // Mark as just unlocked for animation
        setJustUnlockedId(node.id);

        // Clear notification after animation
        setTimeout(() => {
          setNotification(null);
        }, 3000);
      } else {
        // Show error notification
        setNotification({
          message: result.message,
          type: "error",
        });
        setTimeout(() => {
          setNotification(null);
        }, 3000);
      }
    },
    [sanctuaryProgress, progression, playerClass, updatePlayerData],
  );

  // Get selected node status
  const selectedNodeStatus: NodeStatus = selectedNode
    ? getNodeStatus(selectedNode, sanctuaryProgress, playerClass)
    : "locked";

  return (
    <>
      <img
        className="sanctuary-background"
        alt="Sanctuary Background"
        src="/assets/images/Sanctuary-background.png"
      ></img>
      <div className="sanctuary-screen">
        {/* Notification Toast */}
        {notification && (
          <div className={`sanctuary-notification ${notification.type}`}>
            {notification.message}
          </div>
        )}

        {/* Standard Resources Header */}
        <FacilityHeader title="聖域" />

        {/* Sanctuary-specific Soul & Progress Display */}
        <div className="sanctuary-soul-section">
          {/* Soul Resources Display */}
          <div className="sanctuary-resources">
            <div className="soul-display total">
              <span className="soul-icon">👻</span>
              <div className="soul-info">
                <span className="soul-label">総ソウル</span>
                <span className="soul-value">
                  {sanctuaryProgress.totalSouls}
                </span>
              </div>
            </div>

            <div className="soul-display current-run">
              <span className="soul-icon">✨</span>
              <div className="soul-info">
                <span className="soul-label">今回のラン</span>
                <span className="soul-value">
                  {sanctuaryProgress.currentRunSouls}
                </span>
              </div>
            </div>

            <div className="exploration-display">
              <span className="exploration-icon">🗺️</span>
              <span className="exploration-value">
                {playerData.resources.explorationLimit.current} /{" "}
                {playerData.resources.explorationLimit.max +
                  effects.explorationLimitBonus}
              </span>
            </div>
          </div>

          {/* Progress Stats */}
          <div className="progress-stats">
            <div className="stat-item">
              <span className="stat-value">{stats.unlockedCount}</span>
              <span className="stat-label">解放済</span>
            </div>
            <div className="stat-item">
              <span className="stat-value">{stats.availableCount}</span>
              <span className="stat-label">解放可能</span>
            </div>
            <div className="stat-item">
              <span className="stat-value">{stats.completionPercent}%</span>
              <span className="stat-label">完了率</span>
            </div>
            <div className="stat-item">
              <span className="stat-value">{stats.soulsSpent}</span>
              <span className="stat-label">消費済</span>
            </div>
          </div>
        </div>

        {/* Main Content */}
        <div className="sanctuary-content">
          {/* Skill Tree */}
          <SkillTree
            progress={sanctuaryProgress}
            playerClass={playerClass}
            selectedNodeId={selectedNode?.id || null}
            justUnlockedId={justUnlockedId}
            onNodeSelect={handleNodeSelect}
          />

          {/* Detail Panel */}
          <NodeDetailPanel
            node={selectedNode}
            status={selectedNodeStatus}
            totalSouls={sanctuaryProgress.totalSouls}
            unlockedNodes={sanctuaryProgress.unlockedNodes}
            playerClass={playerClass}
            onUnlock={handleUnlock}
          />
        </div>

        {/* Back Button */}
        <button className="sanctuary-back-button" onClick={returnToCamp}>
          ← キャンプに戻る
        </button>
      </div>
    </>
  );
};

export default Sanctuary;
