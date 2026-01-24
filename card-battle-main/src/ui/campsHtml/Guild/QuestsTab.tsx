// QuestsTab: Placeholder for future quest system

const QuestsTab = () => {
  return (
    <div className="quests-tab placeholder-tab">
      <div className="placeholder-content">
        <h2 className="placeholder-title">📜 Quests</h2>
        <p className="placeholder-description">
          Accept daily and weekly quests to earn extra rewards and soul remnants.
        </p>
        <div className="coming-soon-badge">Coming Soon in Phase 3</div>
        <div className="feature-preview">
          <h3>Planned Features:</h3>
          <ul>
            <li>Daily quests (refresh every 24 hours)</li>
            <li>Weekly quests (refresh every 7 days)</li>
            <li>Story quests (unlock new content)</li>
            <li>Rewards: Gold, Magic Stones, Soul Remnants, Items</li>
            <li>Progress tracking for objectives</li>
          </ul>
        </div>
      </div>
    </div>
  );
};

export default QuestsTab;
