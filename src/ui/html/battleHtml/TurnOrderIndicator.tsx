/**
 * Turn Order Indicator
 * Battle System Ver 5.0 - Horizontal Phase Display
 *
 * Shows 2-4 phases in horizontal layout with icons.
 * Supports multi-enemy display (E1, E2, etc.)
 */

import React from "react";
import type { PhaseEntry } from "@/types/battleTypes";
import "../../css/battle/TurnOrderIndicator.css";
import { MAX_TURN_ORDER_DISPLAY } from "@/constants";

interface TurnOrderIndicatorProps {
  expandedEntries: PhaseEntry[];
  currentPhaseIndex: number;
  enemyCount: number;
}

// Diamond-shaped icons matching .pen design
const PlayerIcon: React.FC<{ isActive: boolean }> = ({ isActive }) => (
  <div className={`phase-icon-diamond player ${isActive ? "active" : ""}`}>
    <span className="icon-letter">P</span>
  </div>
);

const EnemyIcon: React.FC<{ isActive: boolean; label?: string }> = ({
  isActive,
  label,
}) => (
  <div className={`phase-icon-diamond enemy ${isActive ? "active" : ""}`}>
    <span className="icon-letter">{label ?? "E"}</span>
  </div>
);

export const TurnOrderIndicator: React.FC<TurnOrderIndicatorProps> = ({
  expandedEntries,
  currentPhaseIndex,
  enemyCount,
}) => {
  if (expandedEntries.length === 0) {
    return null;
  }

  // Display 2-4 phases from current position, wrapping around
  const displayCount = Math.min(MAX_TURN_ORDER_DISPLAY, expandedEntries.length);
  const phasesToShow: { entry: PhaseEntry; index: number }[] = [];

  for (let i = 0; i < displayCount; i++) {
    const phaseIndex = (currentPhaseIndex + i) % expandedEntries.length;
    phasesToShow.push({
      entry: expandedEntries[phaseIndex],
      index: phaseIndex,
    });
  }

  return (
    <div className="turn-order-indicator">
      {phasesToShow.map((phase, displayIndex) => {
        const entry = phase.entry;
        let enemyLabel: string | undefined;
        if (
          entry.actor === "enemy" &&
          enemyCount > 1 &&
          entry.enemyIndex != null
        ) {
          enemyLabel = `E${entry.enemyIndex + 1}`;
        }

        return (
          <React.Fragment key={`${phase.index}-${displayIndex}`}>
            {entry.actor === "player" ? (
              <PlayerIcon isActive={displayIndex === 0} />
            ) : (
              <EnemyIcon isActive={displayIndex === 0} label={enemyLabel} />
            )}
            {displayIndex < phasesToShow.length - 1 && (
              <span className="phase-arrow">→</span>
            )}
          </React.Fragment>
        );
      })}
    </div>
  );
};

// Legacy export for backward compatibility
export default TurnOrderIndicator;
