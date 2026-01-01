import { useState, useEffect } from "react";
import "./UIcss/DefeatScreen.css";

interface DefeatScreenProps {
  onRetry: () => void;
  onReturnToCamp: () => void;
  battleStats: {
    turnCount: number;
    damageDealt: number;
    damageTaken: number;
  };
}

const DefeatScreen = ({
  onRetry,
  onReturnToCamp,
  battleStats,
}: DefeatScreenProps) => {
  const [showOptions, setShowOptions] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => {
      setShowOptions(true);
    }, 1500);

    return () => clearTimeout(timer);
  }, []);

  return (
    <div className="defeat-screen">
      {/* 背景エフェクト */}
      <div className="defeat-bg">
        <div className="defeat-particles" />
        <div className="defeat-glow" />
      </div>

      {/* メインコンテンツ */}
      <div className="defeat-content">
        {/* タイトル */}
        <div className="defeat-title">
          <h1 className="defeat-text">DEFEAT</h1>
          <div className="defeat-subtitle">The darkness has claimed you...</div>
        </div>

        {/* 戦闘統計 */}
        <div className={`defeat-stats ${showOptions ? "show" : ""}`}>
          <div className="defeat-stat-card">
            <div className="defeat-stat-icon">⚔️</div>
            <div className="defeat-stat-value">{battleStats.turnCount}</div>
            <div className="defeat-stat-label">Turns Survived</div>
          </div>
          <div className="defeat-stat-card">
            <div className="defeat-stat-icon">💥</div>
            <div className="defeat-stat-value">{battleStats.damageDealt}</div>
            <div className="defeat-stat-label">Damage Dealt</div>
          </div>
          <div className="defeat-stat-card">
            <div className="defeat-stat-icon">💔</div>
            <div className="defeat-stat-value">{battleStats.damageTaken}</div>
            <div className="defeat-stat-label">Damage Taken</div>
          </div>
        </div>

        {/* オプションボタン */}
        <div className={`defeat-options ${showOptions ? "show" : ""}`}>
          <button className="defeat-button retry" onClick={onRetry}>
            <span className="button-icon">🔄</span>
            <span className="button-text">Retry Battle</span>
          </button>
          <button className="defeat-button camp" onClick={onReturnToCamp}>
            <span className="button-icon">🏕️</span>
            <span className="button-text">Return to Camp</span>
          </button>
        </div>

        {/* メッセージ */}
        <div className={`defeat-message ${showOptions ? "show" : ""}`}>
          <p>Learn from your mistakes and grow stronger...</p>
        </div>
      </div>
    </div>
  );
};

export default DefeatScreen;
