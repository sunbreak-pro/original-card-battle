// SkillTree component - displays radial skill tree layout

import type {
  SkillNode as SkillNodeType,
  NodeStatus,
  CharacterClassForSanctuary as CharacterClass,
  SanctuaryProgress,
} from '@/types/campTypes';
import {
  SKILL_TREE_NODES,
  getNodeById,
} from "@/constants/data/camps/SanctuaryData";
import { getNodeStatus } from "../../../domain/camps/logic/sanctuaryLogic";
import SkillNode from "./SkillNode";

interface SkillTreeProps {
  progress: SanctuaryProgress;
  playerClass?: CharacterClass;
  selectedNodeId: string | null;
  justUnlockedId: string | null;
  onNodeSelect: (node: SkillNodeType) => void;
}

/**
 * Calculate connection line between two nodes
 */
function getConnectionStyle(
  fromNode: SkillNodeType,
  toNode: SkillNodeType,
  treeSize: number,
): React.CSSProperties {
  // Convert angles to radians
  const fromRadians = ((fromNode.position.angle - 90) * Math.PI) / 180;
  const toRadians = ((toNode.position.angle - 90) * Math.PI) / 180;

  // Calculate positions in vh
  const fromRadius = (fromNode.position.radius * 6 + 4) * (treeSize / 50);
  const toRadius = (toNode.position.radius * 6 + 4) * (treeSize / 50);

  const fromX = Math.cos(fromRadians) * fromRadius;
  const fromY = Math.sin(fromRadians) * fromRadius;
  const toX = Math.cos(toRadians) * toRadius;
  const toY = Math.sin(toRadians) * toRadius;

  // Calculate line properties
  const length = Math.sqrt(Math.pow(toX - fromX, 2) + Math.pow(toY - fromY, 2));
  const angle = Math.atan2(toY - fromY, toX - fromX) * (180 / Math.PI);

  return {
    left: `calc(50% + ${fromX}vh)`,
    top: `calc(50% + ${fromY}vh)`,
    width: `${length}vh`,
    transform: `rotate(${angle}deg)`,
  };
}

export const SkillTree: React.FC<SkillTreeProps> = ({
  progress,
  playerClass,
  selectedNodeId,
  justUnlockedId,
  onNodeSelect,
}) => {
  const treeSize = 60; // Size in vh

  // Filter nodes accessible to the player's class
  const accessibleNodes = SKILL_TREE_NODES.filter(
    (node) => !node.classRestriction || node.classRestriction === playerClass,
  );

  // Create connection data
  const connections: {
    from: SkillNodeType;
    to: SkillNodeType;
    fromStatus: NodeStatus;
    toStatus: NodeStatus;
  }[] = [];

  for (const node of accessibleNodes) {
    for (const prereqId of node.prerequisites) {
      const prereqNode = getNodeById(prereqId);
      if (prereqNode) {
        connections.push({
          from: prereqNode,
          to: node,
          fromStatus: getNodeStatus(prereqNode, progress, playerClass),
          toStatus: getNodeStatus(node, progress, playerClass),
        });
      }
    }
  }

  return (
    <div className="skill-tree-container">
      <div
        className="skill-tree"
        style={{ width: `${treeSize}vh`, height: `${treeSize}vh` }}
      >
        {/* Tier Rings */}
        <div className="tier-ring tier-1" />
        <div className="tier-ring tier-2" />
        <div className="tier-ring tier-3" />

        {/* Connection Lines */}
        {connections.map((conn, index) => {
          const isUnlocked =
            conn.fromStatus === "unlocked" && conn.toStatus === "unlocked";
          const isAvailable =
            conn.fromStatus === "unlocked" && conn.toStatus === "available";

          return (
            <div
              key={`conn-${index}`}
              className={`connection-line ${isUnlocked ? "unlocked" : ""} ${isAvailable ? "available" : ""}`}
              style={getConnectionStyle(conn.from, conn.to, treeSize)}
            />
          );
        })}

        {/* Center Orb */}
        <div className="center-orb">
          <span className="center-orb-icon">👻</span>
          <span className="center-orb-value">{progress.totalSouls}</span>
        </div>

        {/* Skill Nodes */}
        {accessibleNodes.map((node) => {
          const status = getNodeStatus(node, progress, playerClass);

          return (
            <SkillNode
              key={node.id}
              node={node}
              status={status}
              isSelected={selectedNodeId === node.id}
              justUnlocked={justUnlockedId === node.id}
              treeSize={treeSize}
              onClick={() => onNodeSelect(node)}
            />
          );
        })}
      </div>
    </div>
  );
};

export default SkillTree;
