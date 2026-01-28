// MapNode - Individual node component for the dungeon map

import { useMemo } from "react";
import type { DungeonNode } from '@/types/dungeonTypes';
import { NODE_TYPE_CONFIG } from "../../constants/dungeonConstants";

interface MapNodeProps {
  node: DungeonNode;
  onClick: (nodeId: string) => void;
  isHighlighted?: boolean;
}

/**
 * MapNode Component
 * Displays a single node on the dungeon map
 */
export function MapNode({ node, onClick, isHighlighted = false }: MapNodeProps) {
  const { id, type, status } = node;
  const config = NODE_TYPE_CONFIG[type];

  // Determine if clickable
  const isClickable = status === "available";

  // Build class names
  const classNames = useMemo(() => {
    const classes = ["map-node", `map-node-${status}`, `map-node-type-${type}`];

    if (isClickable) {
      classes.push("map-node-clickable");
    }

    if (isHighlighted) {
      classes.push("map-node-highlighted");
    }

    return classes.join(" ");
  }, [status, type, isClickable, isHighlighted]);

  // Handle click
  const handleClick = () => {
    if (isClickable) {
      onClick(id);
    }
  };

  // Handle keyboard interaction
  const handleKeyDown = (e: React.KeyboardEvent) => {
    if ((e.key === "Enter" || e.key === " ") && isClickable) {
      e.preventDefault();
      onClick(id);
    }
  };

  return (
    <div
      className={classNames}
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      role={isClickable ? "button" : "presentation"}
      tabIndex={isClickable ? 0 : -1}
      aria-label={`${config.label} node - ${status}`}
      aria-disabled={!isClickable}
    >
      <div className="map-node-inner">
        <span className="map-node-icon">{config.icon}</span>
        {status === "current" && (
          <div className="map-node-current-indicator" />
        )}
      </div>
      {status === "available" && (
        <div className="map-node-available-pulse" />
      )}
    </div>
  );
}

export default MapNode;
