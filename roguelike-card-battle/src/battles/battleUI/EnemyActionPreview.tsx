/**
 * 敵の行動予告UI
 * Battle System Ver 4.0
 *
 * 敵の次のターンの行動を事前に表示
 */

import React from "react";
import type { EnemyAction } from "../../Character/data/EnemyData";
import "./UIcss/EnemyActionPreview.css";

interface EnemyActionPreviewProps {
  actions: EnemyAction[];
  enemyEnergy: number;
}

export const EnemyActionPreview: React.FC<EnemyActionPreviewProps> = ({
  actions,
  enemyEnergy,
}) => {
  if (actions.length === 0) {
    return null;
  }

  return (
    <div className="enemy-action-preview">
      <div className="preview-header">
        <span className="header-text">次の行動</span>
        <span className="energy-badge">{enemyEnergy} Energy</span>
      </div>

      <div className="action-list">
        {actions.map((action, index) => (
          <div key={index} className="action-item">
            <div className="action-icon-wrapper">
              {action.displayIcon && (
                <span className="action-icon">{action.displayIcon}</span>
              )}
            </div>

            <div className="action-details">
              <div className="action-name">{action.name}</div>
              <div className="action-info">
                {action.baseDamage && action.baseDamage > 0 && (
                  <span className="damage-value">
                    {action.baseDamage} DMG
                  </span>
                )}
                {action.hitCount && action.hitCount > 1 && (
                  <span className="hit-count">
                    ×{action.hitCount}
                  </span>
                )}
                {action.guardGain && action.guardGain > 0 && (
                  <span className="guard-value">
                    🛡️ {action.guardGain}
                  </span>
                )}
              </div>
            </div>

            {action.energyCost && action.energyCost > 1 && (
              <div className="action-cost">
                <span className="cost-badge">{action.energyCost}</span>
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
};
