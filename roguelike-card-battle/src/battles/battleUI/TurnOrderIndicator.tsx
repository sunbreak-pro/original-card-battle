/**
 * ターン順序インジケーター
 * Battle System Ver 4.0
 *
 * プレイヤーと敵の速度を表示し、ターン順序と速度ボーナスを可視化
 */

import React from "react";
import type { SpeedBonus } from "../logic/speedCalculation";
import "./UIcss/TurnOrderIndicator.css";

interface TurnOrderIndicatorProps {
  playerSpeed: number;
  enemySpeed: number;
  turnOrder: "player" | "enemy";
  playerBonus: SpeedBonus | null;
  enemyBonus: SpeedBonus | null;
}

export const TurnOrderIndicator: React.FC<TurnOrderIndicatorProps> = ({
  playerSpeed,
  enemySpeed,
  turnOrder,
  playerBonus,
  enemyBonus,
}) => {
  return (
    <div className="turn-order-indicator">
      <div className="indicator-header">行動順序</div>

      <div className="speed-comparison">
        {/* プレイヤー側 */}
        <div className={`actor-info ${turnOrder === "player" ? "first-actor" : "second-actor"}`}>
          <div className="actor-name">Player</div>
          <div className="speed-value">{playerSpeed}</div>
          {playerBonus && (
            <div className="speed-bonus player-bonus">
              <span className="bonus-icon">⚡</span>
              <span className="bonus-name">{playerBonus.name}</span>
            </div>
          )}
          {turnOrder === "player" && (
            <div className="first-indicator">先攻</div>
          )}
        </div>

        {/* VS表示 */}
        <div className="vs-divider">
          <span className="vs-icon">⚔️</span>
        </div>

        {/* 敵側 */}
        <div className={`actor-info ${turnOrder === "enemy" ? "first-actor" : "second-actor"}`}>
          <div className="actor-name">Enemy</div>
          <div className="speed-value">{enemySpeed}</div>
          {enemyBonus && (
            <div className="speed-bonus enemy-bonus">
              <span className="bonus-icon">⚡</span>
              <span className="bonus-name">{enemyBonus.name}</span>
            </div>
          )}
          {turnOrder === "enemy" && (
            <div className="first-indicator">先攻</div>
          )}
        </div>
      </div>
    </div>
  );
};
