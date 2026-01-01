import { useState, useEffect } from "react";
import type { Card } from "../../cards/type/cardType";
import "./UIcss/VictoryScreen.css";

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
    // アニメーション用のディレイ
    const timer = setTimeout(() => {
      setShowRewards(true);
    }, 1000);

    return () => clearTimeout(timer);
  }, []);

  return (
    <div className="victory-screen">
      {/* 背景エフェクト */}
      <div className="victory-bg">
        <div className="victory-particles" />
        <div className="victory-glow" />
      </div>

      {/* メインコンテンツ */}
      <div className="victory-content">
        {/* タイトル */}
        <div className="victory-title">
          <h1 className="victory-text">VICTORY!</h1>
          <div className="victory-subtitle">You have conquered the depths</div>
        </div>

        {/* 戦闘統計 */}
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

        {/* 報酬セクション */}
        <div className={`rewards-section ${showRewards ? "show" : ""}`}>
          <h2 className="rewards-title">Rewards</h2>

          <div className="rewards-grid">
            {/* ゴールド */}
            <div className="reward-item gold">
              <div className="reward-icon">💰</div>
              <div className="reward-amount">+{rewards.gold} Gold</div>
            </div>

            {/* 経験値 */}
            <div className="reward-item experience">
              <div className="reward-icon">⭐</div>
              <div className="reward-amount">+{rewards.experience} EXP</div>
            </div>
          </div>

          {/* カード報酬 */}
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

        {/* 続けるボタン */}
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
