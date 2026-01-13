import { useState, useEffect } from "react";
import type { Card } from "../../domain/cards/type/cardType";
import "../css/VictoryScreen.css";

interface VictoryScreenProps {
  onContinue: () => void;
  rewards: {
    gold: number;
    experience: number;
    cards: Card[];
  };
  battleStats: {
    turnCount: number;
    damageDealt: number;
    damageTaken: number;
  };
}

const VictoryScreen = ({
  onContinue,
  rewards,
  battleStats,
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
            <div className="stat-value">{battleStats.turnCount}</div>
            <div className="stat-label">Turns</div>
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
            <div className="reward-item gold">
              <div className="reward-icon">💰</div>
              <div className="reward-amount">+{rewards.gold} Gold</div>
            </div>
            <div className="reward-item experience">
              <div className="reward-icon">⭐</div>
              <div className="reward-amount">+{rewards.experience} EXP</div>
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
