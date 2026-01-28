import React from "react";
import type { BuffDebuffMap } from '@/types/battleTypes';
import type { SwordEnergyState } from '@/types/characterTypes';
import StatusEffectDisplay from "../componentsHtml/BuffEffect";
import { GUARD_BAR_DISPLAY_MAX } from "../../constants";

interface PlayerFrameProps {
  playerName: string;
  playerClass: string;
  playerRef: React.RefObject<HTMLDivElement | null>;
  playerHp: number;
  playerMaxHp: number;
  playerAp: number;
  playerMaxAp: number;
  playerGuard: number;
  playerBuffs: BuffDebuffMap;
  cardEnergy: number;
  maxEnergy: number;
  swordEnergy: SwordEnergyState;
  theme: {
    primary: string;
    secondary: string;
    accent: string;
    bg: string;
    glow: string;
    hover: string;
  };
}

const PlayerFrame: React.FC<PlayerFrameProps> = ({
  playerName,
  playerClass,
  playerRef,
  playerHp,
  playerMaxHp,
  playerAp,
  playerMaxAp,
  playerGuard,
  playerBuffs,
  cardEnergy,
  maxEnergy,
  swordEnergy,
  theme,
}) => {
  return (
    <div className="player-section">
      <div className="player-field">
        <div className="character-name">
          {playerName} [{playerClass}]
        </div>
        <div className="character-visual player" ref={playerRef}>
          ⚔️
        </div>
        <div className="status-container player-status-container">
          {/* Guard bar - value badge on left */}
          {playerGuard > 0 && (
            <div className="status-bar-row guard-row">
              <div className="value-badge guard-badge">{playerGuard}</div>
              <div className="unified-bar-container guard-bar">
                <div
                  className="bar-fill guard-fill"
                  style={{
                    width: `${Math.min(100, (playerGuard / GUARD_BAR_DISPLAY_MAX) * 100)}%`,
                  }}
                />
              </div>
            </div>
          )}

          <div className="status-bar-row hp-row">
            <div
              {...(playerAp === 0
                ? { className: "break-badge" }
                : { className: "value-badge ap-badge" })}
            >
              {" "}
              {playerAp > 0 && `${playerAp}/${playerMaxAp}`}
              {playerAp === 0 && `break!`}
            </div>

            <div
              {...(playerAp > 0
                ? { className: "Armor-border" }
                : { className: "unified-bar-container hp-bar" })}
            >
              {/* AP overlay */}
              {playerAp > 0 && (
                <div
                  className="bar-fill ap-overlay"
                  style={{ width: `${(playerAp / playerMaxAp) * 100}%` }}
                />
              )}
              {/* HP bar */}
              <div
                className="bar-fill hp-fill"
                style={{ width: `${(playerHp / playerMaxHp) * 100}%` }}
              />
              <span className="hp-value">
                {playerHp}/{playerMaxHp}
              </span>
            </div>
          </div>

          {/* Energy bar - value badge on left */}
          <div className="status-bar-row energy-row">
            <div className="value-badge energy-badge">
              {cardEnergy}/{maxEnergy}
            </div>
            <div className="unified-bar-container energy-bar">
              <div
                className="bar-fill energy-fill"
                style={{ width: `${(cardEnergy / maxEnergy) * 100}%` }}
              />
            </div>
          </div>
          <StatusEffectDisplay buffsDebuffs={playerBuffs} theme={theme} />
        </div>
      </div>
      <div className="energy-and-ability">
        <div className="sword-energy-display">
          <div className="sword-energy-label">剣気:</div>

          <div className="sword-energy-bar-container">
            <div className="sword-energy-bar">
              <div
                className={`sword-energy-fill ${
                  swordEnergy.current >= 10
                    ? "level-max"
                    : swordEnergy.current >= 8
                      ? "level-high"
                      : swordEnergy.current >= 5
                        ? "level-mid"
                        : ""
                }`}
                style={{
                  width: `${(swordEnergy.current / swordEnergy.max) * 100}%`,
                }}
              />
              <span className="sword-energy-text">
                {swordEnergy.current}/{swordEnergy.max}
              </span>
            </div>
          </div>
          <div className="sword-energy-effects">
            <span
              className={`effect-badge crit ${
                swordEnergy.current >= 5 ? "active" : "inactive"
              }`}
            >
              {swordEnergy.current >= 5 ? "✓" : "○"} Crit+20%
            </span>
            <span
              className={`effect-badge pierce ${
                swordEnergy.current >= 8 ? "active" : "inactive"
              }`}
            >
              {swordEnergy.current >= 8 ? "✓" : "○"} 貫通+30%
            </span>
            <span
              className={`effect-badge max ${
                swordEnergy.current >= 10 ? "active" : "inactive"
              }`}
            >
              {swordEnergy.current >= 10 ? "✓" : "○"} MAX
            </span>
          </div>
        </div>
      </div>
    </div>
  );
};

export default PlayerFrame;
