// PromotionTab: Displays promotion exam information and start button

import { usePlayer } from "../../../contexts/PlayerContext";
import { useGameState } from "../../../contexts/GameStateContext";
import {
  getNextExam,
  canTakeExam,
} from "@/constants/data/camps/PromotionData";

const PromotionTab = () => {
  const { playerData, updateClassGrade, updateBaseMaxHp, updateBaseMaxAp } = usePlayer();
  const { startBattle, navigateTo } = useGameState();

  // Calculate total gold
  const totalGold =
    playerData.resources.baseCampGold + playerData.resources.explorationGold;

  // Get next available exam
  const exam = getNextExam(
    playerData.persistent.playerClass,
    playerData.persistent.classGrade,
  );

  // If no exam available (max rank reached)
  if (!exam) {
    return (
      <div className="promotion-unavailable">
        <h2>🏆 Congratulations!</h2>
        <p>You have achieved the highest rank.</p>
        <div className="current-grade-display">
          <span className="grade-label">Current Rank:</span>
          <span className="grade-value">
            {playerData.persistent.classGrade}
          </span>
        </div>
        <p className="flavor-text">
          You stand at the pinnacle of your class. There are no more exams to
          take.
        </p>
      </div>
    );
  }

  // Check requirements
  const cardCount = playerData.persistent.deckCardIds.length;
  const canTake = canTakeExam(exam, cardCount, totalGold);
  const meetsCardRequirement = cardCount >= exam.requiredCardCount;
  const meetsGoldRequirement = exam.requiredGold
    ? totalGold >= exam.requiredGold
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
      "exam",
    );
  };

  /**
   * Handle exam passed
   */
  const handleExamPassed = () => {
    // Promote to next grade
    updateClassGrade(exam.nextGrade);

    // Apply stat bonuses
    if (exam.rewards.maxHpBonus) {
      updateBaseMaxHp(exam.rewards.maxHpBonus);
    }
    if (exam.rewards.maxApBonus) {
      updateBaseMaxAp(exam.rewards.maxApBonus);
    }

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
          `Your achievements will be recorded in the guild records.`,
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
          `Tip: Make sure your HP and AP meet the recommended values before challenging.`,
      );
    }, 100);

    // Note: Exam doesn't consume exploration count
    // Player returns with 1 HP (handled by battle system)
  };

  return (
    <div className="promotion-tab">
      {/* Left Column */}
      <div className="promotion-tab-left">
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
          <h3 className="guild-section-title">📋 Requirements</h3>
          <div className="requirements-list">
            <div
              className={`requirement ${
                meetsCardRequirement ? "met" : "unmet"
              }`}
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
                  Gold: {totalGold} / {exam.requiredGold}G
                </span>
              </div>
            )}
          </div>
        </section>

        {/* Exam Description */}
        <section className="exam-section">
          <h3 className="guild-section-title">📜 Exam Details</h3>
          <p className="exam-description">{exam.description}</p>
        </section>
      </div>

      {/* Right Column */}
      <div className="promotion-tab-right">
        {/* Recommendations */}
        <section className="exam-section">
          <h3 className="guild-section-title">💡 Recommendations</h3>
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
            These are suggested minimum stats. Higher stats increase your
            chances.
          </p>
        </section>

        {/* Rewards */}
        <section className="exam-section">
          <h3 className="guild-section-title">🎁 Rewards</h3>
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
          {/* Start Button */}
          <button
            className={`start-exam-button ${canTake ? "enabled" : "disabled"}`}
            onClick={handleStartExam}
            disabled={!canTake}
          >
            {canTake ? "Start Promotion Exam" : "Requirements Not Met"}
          </button>
        </div>
      </div>
    </div>
  );
};

export default PromotionTab;
