import { useState, useEffect } from "react";
import "../../css/battle/DefeatScreen.css";

interface DefeatScreenProps {
  onRetry: () => void;
  onReturnToCamp: () => void;
  battleStats: {
    turnCount: number;
    damageDealt: number;
    damageTaken: number;
  };
  /** Remaining lives after this death */
  remainingLives?: number;
  /** Max lives */
  maxLives?: number;
  /** Souls transferred to permanent pool */
  soulsTransferred?: number;
  /** Whether this is game over (no lives remaining) */
  isGameOver?: boolean;
}

const DefeatScreen = ({
  onRetry,
  onReturnToCamp,
  battleStats,
  remainingLives = 3,
  maxLives = 3,
  soulsTransferred = 0,
  isGameOver = false,
}: DefeatScreenProps) => {
  const [showOptions, setShowOptions] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => {
      setShowOptions(true);
    }, 1500);

    return () => clearTimeout(timer);
  }, []);

  // Generate lives display (hearts)
  const renderLives = () => {
    const hearts = [];
    for (let i = 0; i < maxLives; i++) {
      const isFilled = i < remainingLives;
      hearts.push(
        <span key={i} className={`life-heart ${isFilled ? "filled" : "empty"}`}>
          {isFilled ? "❤️" : "🖤"}
        </span>,
      );
    }
    return hearts;
  };

  return (
    <div className="defeat-screen">
      <div className="defeat-bg">
        <div className="defeat-particles" />
        <div className="defeat-glow" />
      </div>
      <div className="defeat-content">
        <div className="defeat-title">
          <h1 className="defeat-text">{isGameOver ? "GAME OVER" : "DEFEAT"}</h1>
          <div className="defeat-subtitle">
            {isGameOver
              ? "Your journey ends here..."
              : "The darkness has claimed you..."}
          </div>
        </div>

        {/* Lives Display */}
        <div className={`defeat-lives ${showOptions ? "show" : ""}`}>
          <div className="lives-label">残機 / Lives</div>
          <div className="lives-hearts">{renderLives()}</div>
          {!isGameOver && (
            <div className="lives-remaining">残り {remainingLives} 回</div>
          )}
        </div>

        {/* Souls Transferred Display */}
        {soulsTransferred > 0 && (
          <div className={`defeat-souls ${showOptions ? "show" : ""}`}>
            <div className="souls-icon">✨</div>
            <div className="souls-text">
              <span className="souls-label">魂の残滓を獲得</span>
              <span className="souls-value">+{soulsTransferred}</span>
            </div>
            <div className="souls-description">100% of souls preserved</div>
          </div>
        )}

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

        <div className={`defeat-options ${showOptions ? "show" : ""}`}>
          {!isGameOver && (
            <button className="defeat-button retry" onClick={onRetry}>
              <span className="button-icon">🔄</span>
              <span className="button-text">Retry Battle</span>
            </button>
          )}
          <button className="defeat-button camp" onClick={onReturnToCamp}>
            <span className="button-icon">🏕️</span>
            <span className="button-text">
              {isGameOver ? "New Game" : "Return to Camp"}
            </span>
          </button>
        </div>

        <div className={`defeat-message ${showOptions ? "show" : ""}`}>
          <p>
            {isGameOver
              ? "Start anew with the wisdom you have gained..."
              : "Learn from your mistakes and grow stronger..."}
          </p>
        </div>
      </div>
    </div>
  );
};

export default DefeatScreen;
