/**
 * Turn Order Indicator
 * Battle System Ver 5.0 - Horizontal Phase Display
 *
 * Shows 2-4 phases in horizontal layout with icons
 */

import React from "react";
import type { PhaseQueue, PhaseActor } from "../../domain/battles/type/phaseType";
import "../css/TurnOrderIndicator.css";

interface TurnOrderIndicatorProps {
  phaseQueue: PhaseQueue | null;
  currentPhaseIndex: number;
}

// Placeholder icons - replace with SVG imports when ready
const PlayerIcon: React.FC<{ isActive: boolean }> = ({ isActive }) => (
  <div className={`phase-icon player-icon ${isActive ? "active" : ""}`}>
    {/* TODO: Replace with <img src={playerIconSvg} /> */}
    <span className="icon-placeholder">P</span>
  </div>
);

const EnemyIcon: React.FC<{ isActive: boolean }> = ({ isActive }) => (
  <div className={`phase-icon enemy-icon ${isActive ? "active" : ""}`}>
    {/* TODO: Replace with <img src={enemyIconSvg} /> */}
    <span className="icon-placeholder">E</span>
  </div>
);

export const TurnOrderIndicator: React.FC<TurnOrderIndicatorProps> = ({
  phaseQueue,
  currentPhaseIndex,
}) => {
  if (!phaseQueue || phaseQueue.phases.length === 0) {
    return null;
  }

  // Display 2-4 phases from current position
  const displayCount = Math.min(4, phaseQueue.phases.length);
  const phasesToShow: { actor: PhaseActor; index: number }[] = [];

  for (let i = 0; i < displayCount; i++) {
    const phaseIndex = (currentPhaseIndex + i) % phaseQueue.phases.length;
    phasesToShow.push({
      actor: phaseQueue.phases[phaseIndex],
      index: phaseIndex,
    });
  }

  return (
    <div className="turn-order-indicator-v2">
      {phasesToShow.map((phase, displayIndex) => (
        <React.Fragment key={`${phase.index}-${displayIndex}`}>
          {phase.actor === "player" ? (
            <PlayerIcon isActive={displayIndex === 0} />
          ) : (
            <EnemyIcon isActive={displayIndex === 0} />
          )}
          {displayIndex < phasesToShow.length - 1 && (
            <span className="phase-arrow">→</span>
          )}
        </React.Fragment>
      ))}
    </div>
  );
};

// Legacy export for backward compatibility
export default TurnOrderIndicator;
