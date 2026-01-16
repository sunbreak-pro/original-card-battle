// NodeDetailPanel component - shows details of selected node

import { useState, useRef, useCallback } from "react";
import type {
  SkillNode,
  NodeStatus,
  CharacterClass,
} from "../../../domain/camps/types/SanctuaryTypes";
import { SANCTUARY_CONSTANTS } from "../../../domain/camps/types/SanctuaryTypes";
import { CATEGORY_DISPLAY, getNodeById } from "../../../domain/camps/data/SanctuaryData";

interface NodeDetailPanelProps {
  node: SkillNode | null;
  status: NodeStatus;
  totalSouls: number;
  unlockedNodes: string[];
  playerClass?: CharacterClass;
  onUnlock: (node: SkillNode) => void;
}

const STATUS_LABELS: Record<NodeStatus, string> = {
  unlocked: "Unlocked",
  available: "Available",
  locked: "Locked",
};

export const NodeDetailPanel: React.FC<NodeDetailPanelProps> = ({
  node,
  status,
  totalSouls,
  unlockedNodes,
  playerClass,
  onUnlock,
}) => {
  const [unlockProgress, setUnlockProgress] = useState(0);
  const [isUnlocking, setIsUnlocking] = useState(false);
  const holdTimerRef = useRef<number | null>(null);
  const progressIntervalRef = useRef<number | null>(null);

  const canAfford = node ? totalSouls >= node.cost : false;
  const canUnlock = status === "available" && canAfford;

  const clearTimers = useCallback(() => {
    if (holdTimerRef.current) {
      clearTimeout(holdTimerRef.current);
      holdTimerRef.current = null;
    }
    if (progressIntervalRef.current) {
      clearInterval(progressIntervalRef.current);
      progressIntervalRef.current = null;
    }
    setIsUnlocking(false);
    setUnlockProgress(0);
  }, []);

  const startUnlock = useCallback(() => {
    if (!node || !canUnlock) return;

    setIsUnlocking(true);
    const startTime = Date.now();
    const duration = SANCTUARY_CONSTANTS.UNLOCK_HOLD_DURATION;

    progressIntervalRef.current = window.setInterval(() => {
      const elapsed = Date.now() - startTime;
      const progress = Math.min((elapsed / duration) * 100, 100);
      setUnlockProgress(progress);

      if (progress >= 100) {
        clearTimers();
        onUnlock(node);
      }
    }, 16); // ~60fps
  }, [node, canUnlock, clearTimers, onUnlock]);

  const handleMouseDown = useCallback(() => {
    if (canUnlock) {
      startUnlock();
    }
  }, [canUnlock, startUnlock]);

  const handleMouseUp = useCallback(() => {
    clearTimers();
  }, [clearTimers]);

  const handleMouseLeave = useCallback(() => {
    clearTimers();
  }, [clearTimers]);

  // No selection state
  if (!node) {
    return (
      <div className="detail-panel">
        <div className="no-selection">
          <span className="no-selection-icon">✨</span>
          <span className="no-selection-text">
            Select a node to view details
          </span>
        </div>
      </div>
    );
  }

  const categoryInfo = CATEGORY_DISPLAY[node.category];

  // Check prerequisites
  const prerequisites = node.prerequisites.map((prereqId) => {
    const prereqNode = getNodeById(prereqId);
    const isMet = unlockedNodes.includes(prereqId);
    return {
      id: prereqId,
      name: prereqNode?.name || prereqId,
      isMet,
    };
  });

  // Check class restriction
  const isClassRestricted =
    node.classRestriction && node.classRestriction !== playerClass;

  return (
    <div className="detail-panel">
      <div className="node-detail">
        {/* Header */}
        <div className="detail-header">
          <span className="detail-icon">{node.icon}</span>
          <div className="detail-title">
            <h3 className="detail-name">{node.name}</h3>
            <div className="detail-badges">
              <span className="badge tier">Tier {node.tier}</span>
              <span
                className={`badge category ${node.category}`}
                style={{ backgroundColor: categoryInfo.bgColor }}
              >
                {categoryInfo.name}
              </span>
              <span className={`badge status ${status}`}>
                {STATUS_LABELS[status]}
              </span>
            </div>
          </div>
        </div>

        {/* Class Restriction */}
        {node.classRestriction && (
          <div className={`class-restriction ${node.classRestriction}`}>
            <span>
              {node.classRestriction === "swordsman" && "⚔️"}
              {node.classRestriction === "mage" && "✨"}
              {node.classRestriction === "summoner" && "👻"}
            </span>
            <span>
              {node.classRestriction.charAt(0).toUpperCase() +
                node.classRestriction.slice(1)}{" "}
              Only
            </span>
          </div>
        )}

        {/* Description */}
        <p className="detail-description">{node.description}</p>

        {/* Effects */}
        <div className="effects-section">
          <div className="effects-title">Effects</div>
          {node.effects.map((effect, index) => (
            <div key={index} className="effect-item">
              <span className="effect-bullet" />
              <span className="effect-text">{effect.description}</span>
            </div>
          ))}
        </div>

        {/* Prerequisites */}
        {prerequisites.length > 0 && (
          <div className="prerequisites-section">
            <div className="prerequisites-title">Prerequisites</div>
            {prerequisites.map((prereq) => (
              <div key={prereq.id} className="prerequisite-item">
                <span className="prerequisite-status">
                  {prereq.isMet ? "✅" : "❌"}
                </span>
                <span
                  className={`prerequisite-name ${prereq.isMet ? "met" : "unmet"}`}
                >
                  {prereq.name}
                </span>
              </div>
            ))}
          </div>
        )}

        {/* Cost Section */}
        {status !== "unlocked" && (
          <div className={`cost-section ${canAfford ? "affordable" : "insufficient"}`}>
            <span className="cost-icon">👻</span>
            <span className="cost-amount">{node.cost}</span>
            <span className="cost-label">
              Souls ({canAfford ? "Affordable" : `Need ${node.cost - totalSouls} more`})
            </span>
          </div>
        )}

        {/* Action Button */}
        <div className="unlock-button-container">
          {status === "unlocked" ? (
            <div className="unlocked-message">
              <span className="unlocked-icon">✨</span>
              <span>Already Unlocked</span>
            </div>
          ) : status === "available" ? (
            <button
              className={`unlock-button ${isUnlocking ? "unlocking" : ""}`}
              disabled={!canUnlock}
              onMouseDown={handleMouseDown}
              onMouseUp={handleMouseUp}
              onMouseLeave={handleMouseLeave}
              onTouchStart={handleMouseDown}
              onTouchEnd={handleMouseUp}
            >
              <div
                className="unlock-progress"
                style={{ width: `${unlockProgress}%` }}
              />
              <span className="button-text">
                {isUnlocking
                  ? "Hold to Unlock..."
                  : canAfford
                    ? "Hold to Unlock"
                    : "Insufficient Souls"}
              </span>
            </button>
          ) : (
            <div className="locked-message">
              {isClassRestricted
                ? `Requires ${node.classRestriction} class`
                : "Complete prerequisites to unlock"}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default NodeDetailPanel;
