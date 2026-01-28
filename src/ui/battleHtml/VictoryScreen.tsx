import { useState, useEffect } from "react";
import type { Card } from '@/types/cardTypes';
import type { MagicStones } from '@/types/itemTypes';
import type { EnemyType } from "../../domain/camps/logic/soulSystem";
import { formatMagicStoneDrops } from "../../domain/camps/logic/soulSystem";
import "../css/battle/VictoryScreen.css";

interface VictoryScreenProps {
  onContinue: () => void;
  rewards: {
    soulRemnants: number;           // Soul remnants from defeated enemies
    magicStones: MagicStones;       // Magic stone drops
    cards: Card[];
  };
  battleStats: {
    phaseCount: number;             // Number of phases (was turnCount)
    damageDealt: number;
    damageTaken: number;
  };
  enemyType: EnemyType;
}

const VictoryScreen = ({
  onContinue,
  rewards,
  battleStats,
  enemyType,
}: VictoryScreenProps) => {
  const [showRewards, setShowRewards] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => {
      setShowRewards(true);
    }, 1000);

    return () => clearTimeout(timer);
  }, []);

  return (
    <div className="victory-screen">
      <div className="victory-bg">
        <div className="victory-particles" />
        <div className="victory-glow" />
      </div>

      <div className="victory-content">
        <div className="victory-title">
          <h1 className="victory-text">VICTORY!</h1>
          <div className="victory-subtitle">You have conquered the depths</div>
        </div>

        <div className={`battle-stats ${showRewards ? "show" : ""}`}>
          <div className="stat-card">
            <div className="stat-icon">⚔️</div>
            <div className="stat-value">{battleStats.phaseCount}</div>
            <div className="stat-label">Phases</div>
          </div>
          <div className="stat-card">
            <div className="stat-icon">💥</div>
            <div className="stat-value">{battleStats.damageDealt}</div>
            <div className="stat-label">Damage Dealt</div>
          </div>
          <div className="stat-card">
            <div className="stat-icon">🛡️</div>
            <div className="stat-value">{battleStats.damageTaken}</div>
            <div className="stat-label">Damage Taken</div>
          </div>
        </div>

        <div className={`rewards-section ${showRewards ? "show" : ""}`}>
          <h2 className="rewards-title">Rewards</h2>

          <div className="rewards-grid">
            <div className="reward-item souls">
              <div className="reward-icon">✨</div>
              <div className="reward-amount">+{rewards.soulRemnants} 魂の残滓</div>
              <div className="reward-detail">
                {enemyType === "boss" ? "ボス" : enemyType === "elite" ? "エリート" : "通常"}敵
              </div>
            </div>
            <div className="reward-item magic-stones">
              <div className="reward-icon">💎</div>
              <div className="reward-amount">魔石獲得</div>
              <div className="reward-detail">{formatMagicStoneDrops(rewards.magicStones)}</div>
            </div>
          </div>

          {rewards.cards.length > 0 && (
            <div className="card-rewards">
              <h3 className="card-rewards-title">Card Rewards</h3>
              <div className="card-rewards-list">
                {rewards.cards.map((card, index) => (
                  <div
                    key={index}
                    className="reward-card"
                    style={{ animationDelay: `${index * 0.1}s` }}
                  >
                    <div className="reward-card-name">{card.name}</div>
                    <div className="reward-card-rarity">{card.rarity}</div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        <button
          className={`continue-button ${showRewards ? "show" : ""}`}
          onClick={onContinue}
        >
          Continue
        </button>
      </div>
    </div>
  );
};

export default VictoryScreen;
