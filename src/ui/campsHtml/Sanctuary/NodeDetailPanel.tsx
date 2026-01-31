// NodeDetailPanel component - shows details of selected node

import { useState, useRef, useCallback } from "react";
import type {
  SkillNode,
  NodeStatus,
  CharacterClassForSanctuary as CharacterClass,
} from '@/types/campTypes';
import { SANCTUARY_CONSTANTS } from "../../../constants/campConstants";
import {
  CATEGORY_DISPLAY,
  getNodeById,
} from "../../../domain/camps/data/SanctuaryData";
interface NodeDetailPanelProps {
  node: SkillNode | null;
  status: NodeStatus;
  totalSouls: number;
  unlockedNodes: string[];
  playerClass?: CharacterClass;
  onUnlock: (node: SkillNode) => void;
}

const STATUS_LABELS: Record<NodeStatus, string> = {
  unlocked: "解放済み",
  available: "解放可能",
  locked: "ロック中",
  current: "現在地",
  completed: "完了",
  skipped: "スキップ",
};

export const NodeDetailPanel: React.FC<NodeDetailPanelProps> = ({
  node,
  status,
  totalSouls,
  unlockedNodes,
  playerClass,
  onUnlock,
}) => {
  const [isUnlocking, setIsUnlocking] = useState(false);
  const completionTimerRef = useRef<number | null>(null);

  const canAfford = node ? totalSouls >= node.cost : false;
  const canUnlock = status === "available" && canAfford;

  const clearTimers = useCallback(() => {
    if (completionTimerRef.current) {
      clearTimeout(completionTimerRef.current);
      completionTimerRef.current = null;
    }
    setIsUnlocking(false);
  }, []);

  const startUnlock = useCallback(() => {
    if (!node || !canUnlock) return;

    setIsUnlocking(true);

    // CSS transition handles the visual progress; setTimeout detects completion
    completionTimerRef.current = window.setTimeout(() => {
      clearTimers();
      onUnlock(node);
    }, SANCTUARY_CONSTANTS.UNLOCK_HOLD_DURATION);
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
            ノードを選択して詳細を表示
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
              {node.classRestriction === "swordsman" && "剣士"}
              {node.classRestriction === "mage" && "魔法使い"}
              {node.classRestriction === "summoner" && "召喚師"}
              専用
            </span>
          </div>
        )}

        {/* Description */}
        <p className="detail-description">{node.description}</p>

        {/* Effects */}
        <div className="effects-section">
          <div className="effects-title">効果</div>
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
            <div className="prerequisites-title">前提条件</div>
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
          <div
            className={`cost-section ${canAfford ? "affordable" : "insufficient"}`}
          >
            <span className="cost-icon">👻</span>
            <span className="cost-amount">{node.cost}</span>
            <span className="cost-label">
              ソウル (
              {canAfford ? "購入可能" : `あと ${node.cost - totalSouls} 必要`}
              )
            </span>
          </div>
        )}

        {/* Action Button */}
        <div className="unlock-button-container">
          {status === "unlocked" ? (
            <div className="unlocked-message">
              <span className="unlocked-icon">✨</span>
              <span>解放済み</span>
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
              <div className="unlock-progress" />
              <span className="button-text">
                {isUnlocking
                  ? "長押しで解放中..."
                  : canAfford
                    ? "長押しで解放"
                    : "ソウル不足"}
              </span>
            </button>
          ) : (
            <div className="locked-message">
              {isClassRestricted
                ? `${node.classRestriction === "swordsman" ? "剣士" : node.classRestriction === "mage" ? "魔法使い" : "召喚師"}クラスが必要`
                : "前提条件を満たす必要があります"}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default NodeDetailPanel;
