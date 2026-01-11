// PromotionTab: Displays promotion exam information and start button

import { usePlayer } from "../../../domain/camps/contexts/PlayerContext";
import { useGameState } from "../../../domain/camps/contexts/GameStateContext";
import {
  getNextExam,
  canTakeExam,
} from "../../../domain/camps/data/PromotionData";

const PromotionTab = () => {
  const { player, updateClassGrade } = usePlayer();
  const { startBattle, navigateTo } = useGameState();

  // Get next available exam
  const exam = getNextExam(player.playerClass, player.classGrade);

  // If no exam available (max rank reached)
  if (!exam) {
    return (
      <div className="promotion-unavailable">
        <h2>🏆 Congratulations!</h2>
        <p>You have achieved the highest rank.</p>
        <div className="current-grade-display">
          <span className="grade-label">Current Rank:</span>
          <span className="grade-value">{player.classGrade}</span>
        </div>
        <p className="flavor-text">
          You stand at the pinnacle of your class. There are no more exams to
          take.
        </p>
      </div>
    );
  }

  // Check requirements
  const cardCount = player.deck.length;
  const canTake = canTakeExam(exam, cardCount, player.gold);
  const meetsCardRequirement = cardCount >= exam.requiredCardCount;
  const meetsGoldRequirement = exam.requiredGold
    ? player.gold >= exam.requiredGold
    : true;

  /**
   * Handle exam start
   */
  const handleStartExam = () => {
    if (!canTake) return;

    startBattle(
      {
        enemyIds: [exam.enemyId],
        backgroundType: "arena",
        onWin: handleExamPassed,
        onLose: handleExamFailed,
      },
      "exam"
    );
  };

  /**
   * Handle exam passed
   */
  const handleExamPassed = () => {
    // Promote to next grade
    updateClassGrade(exam.nextGrade);

    // Apply stat bonuses (simplified for Phase 2)
    // TODO Phase 3: Implement actual stat bonus system with specific values
    // For now, show rewards in notification

    // Give reward items (placeholder)
    // TODO Phase 3: Implement actual item reward system
    // For now, show rewards in notification

    // Return to guild first, then show notification
    navigateTo("guild");

    // Show success notification after a brief delay for screen transition
    setTimeout(() => {
      alert(
        `🎉 Promotion Exam Passed!\n\n` +
          `Congratulations! You have been promoted to:\n` +
          `${exam.nextGrade}\n\n` +
          `Rewards:\n` +
          `${exam.rewards.statBonus}\n\n` +
          `Your achievements will be recorded in the guild records.`
      );
    }, 100);
  };

  /**
   * Handle exam failed
   */
  const handleExamFailed = () => {
    // Return to guild first
    navigateTo("guild");

    // Show failure notification after a brief delay for screen transition
    setTimeout(() => {
      alert(
        `❌ Promotion Exam Failed\n\n` +
          `You have been defeated in the exam and returned to the guild.\n\n` +
          `Don't give up! Train harder and try again.\n` +
          `The exam can be retaken at any time when you meet the requirements.\n\n` +
          `Tip: Make sure your HP and AP meet the recommended values before challenging.`
      );
    }, 100);

    // Note: Exam doesn't consume exploration count
    // Player returns with 1 HP (handled by battle system)
  };

  return (
    <div className="promotion-tab">
      <div className="grade-progression">
        <div className="grade-box current">
          <span className="grade-label">Current Rank</span>
          <span className="grade-name">{exam.currentGrade}</span>
        </div>
        <div className="arrow">→</div>
        <div className="grade-box next">
          <span className="grade-label">Next Rank</span>
          <span className="grade-name">{exam.nextGrade}</span>
        </div>
      </div>

      {/* Exam Requirements */}
      <section className="exam-section">
        <h3 className="section-title">📋 Requirements</h3>
        <div className="requirements-list">
          <div
            className={`requirement ${meetsCardRequirement ? "met" : "unmet"}`}
          >
            <span className="requirement-icon">
              {meetsCardRequirement ? "✓" : "✗"}
            </span>
            <span className="requirement-text">
              Cards Owned: {cardCount} / {exam.requiredCardCount}
            </span>
          </div>
          {exam.requiredGold && (
            <div
              className={`requirement ${
                meetsGoldRequirement ? "met" : "unmet"
              }`}
            >
              <span className="requirement-icon">
                {meetsGoldRequirement ? "✓" : "✗"}
              </span>
              <span className="requirement-text">
                Gold: {player.gold} / {exam.requiredGold}G
              </span>
            </div>
          )}
        </div>
      </section>

      {/* Exam Description */}
      <section className="exam-section">
        <h3 className="section-title">📜 Exam Details</h3>
        <p className="exam-description">{exam.description}</p>
      </section>

      {/* Recommendations */}
      <section className="exam-section">
        <h3 className="section-title">💡 Recommendations</h3>
        <div className="recommendations">
          <div className="recommendation-item">
            <span className="stat-label">HP:</span>
            <span className="stat-value">{exam.recommendations.hp}+</span>
          </div>
          <div className="recommendation-item">
            <span className="stat-label">AP:</span>
            <span className="stat-value">{exam.recommendations.ap}+</span>
          </div>
        </div>
        <p className="recommendation-note">
          These are suggested minimum stats. Higher stats increase your chances.
        </p>
      </section>

      {/* Rewards */}
      <section className="exam-section">
        <h3 className="section-title">🎁 Rewards</h3>
        <ul className="rewards-list">
          <li>Promotion to {exam.nextGrade}</li>
          <li>{exam.rewards.statBonus}</li>
        </ul>
      </section>

      {/* Warnings */}
      <div className="exam-warnings">
        <div className="warning-item">
          <span className="warning-icon">⚠️</span>
          <span className="warning-text">
            Starting the exam will begin a battle immediately.
          </span>
        </div>
        <div className="warning-item">
          <span className="warning-icon">ℹ️</span>
          <span className="warning-text">
            Promotion exams do not consume exploration count.
          </span>
        </div>
        <div className="warning-item">
          <span className="warning-icon">💚</span>
          <span className="warning-text">
            You will return to camp with 1 HP if defeated.
          </span>
        </div>
      </div>

      {/* Start Button */}
      <button
        className={`start-exam-button ${canTake ? "enabled" : "disabled"}`}
        onClick={handleStartExam}
        disabled={!canTake}
      >
        {canTake ? "Start Promotion Exam" : "Requirements Not Met"}
      </button>
    </div>
  );
};

export default PromotionTab;
