// SkillNode component - individual node in skill tree

import { useCallback } from "react";
import type { SkillNode as SkillNodeType, NodeStatus } from '@/types/campTypes';

interface SkillNodeProps {
  node: SkillNodeType;
  status: NodeStatus;
  isSelected: boolean;
  justUnlocked: boolean;
  treeSize: number; // Size of the tree container in vh
  onClick: () => void;
}

/**
 * Calculate node position based on angle and radius
 */
function calculatePosition(
  angle: number,
  radius: number,
  treeSize: number
): { left: string; top: string } {
  // Convert angle to radians (0 degrees = top, clockwise)
  const radians = ((angle - 90) * Math.PI) / 180;

  // Calculate radius in vh units
  // Each tier has a different radius multiplier
  const radiusVh = (radius * 6 + 4) * (treeSize / 50);

  // Calculate x and y offsets from center
  const x = Math.cos(radians) * radiusVh;
  const y = Math.sin(radians) * radiusVh;

  // Convert to percentages (center is at 50%, 50%)
  const left = `calc(50% + ${x}vh)`;
  const top = `calc(50% + ${y}vh)`;

  return { left, top };
}

export const SkillNode: React.FC<SkillNodeProps> = ({
  node,
  status,
  isSelected,
  justUnlocked,
  treeSize,
  onClick,
}) => {
  const position = calculatePosition(node.position.angle, node.position.radius, treeSize);

  const classNames = [
    "skill-node",
    status,
    isSelected ? "selected" : "",
    justUnlocked ? "just-unlocked" : "",
  ]
    .filter(Boolean)
    .join(" ");

  // Handle keyboard navigation
  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onClick();
    }
  }, [onClick]);

  // Generate accessible label
  const getStatusLabel = () => {
    switch (status) {
      case 'unlocked': return '解放済み';
      case 'available': return '解放可能';
      case 'locked': return 'ロック中';
    }
  };

  const ariaLabel = `${node.name} - ${getStatusLabel()}${status !== 'unlocked' ? ` (${node.cost}ソウル)` : ''}`;

  return (
    <div
      className={classNames}
      style={{ left: position.left, top: position.top }}
      onClick={onClick}
      onKeyDown={handleKeyDown}
      title={node.name}
      tabIndex={0}
      role="button"
      aria-label={ariaLabel}
      aria-pressed={isSelected}
    >
      <div className="skill-node-inner">
        <span className="node-icon">{node.icon}</span>
        {status !== "unlocked" && (
          <span className="node-cost">{node.cost}</span>
        )}
      </div>
    </div>
  );
};

export default SkillNode;
