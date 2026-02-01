/**
 * QuestsTab: Quest acceptance and tracking system
 *
 * Shows daily and weekly quests with objectives and rewards.
 * Players can accept quests and track progress.
 */

import { useState, useMemo } from "react";
import type { Quest } from "@/types/campTypes";
import {
  generateDailyQuests,
  generateWeeklyQuests,
  isQuestComplete,
} from "@/constants/data/camps/QuestData";
import { useResources } from "@/contexts/ResourceContext";

type QuestFilter = "all" | "daily" | "weekly" | "active";

const QuestsTab = () => {
  const { addGold, addMagicStones } = useResources();

  // Generate quests (in real implementation, these would persist in context)
  const [dailyQuests, setDailyQuests] = useState<Quest[]>(() =>
    generateDailyQuests(),
  );
  const [weeklyQuests, setWeeklyQuests] = useState<Quest[]>(() =>
    generateWeeklyQuests(),
  );
  const [filter, setFilter] = useState<QuestFilter>("all");
  const [claimedIds, setClaimedIds] = useState<Set<string>>(new Set());

  const allQuests = useMemo(
    () => [...dailyQuests, ...weeklyQuests],
    [dailyQuests, weeklyQuests],
  );

  const filteredQuests = useMemo(() => {
    switch (filter) {
      case "daily":
        return dailyQuests;
      case "weekly":
        return weeklyQuests;
      case "active":
        return allQuests.filter((q) => q.isActive && !q.isCompleted);
      default:
        return allQuests;
    }
  }, [filter, dailyQuests, weeklyQuests, allQuests]);

  const acceptQuest = (questId: string) => {
    setDailyQuests((prev) =>
      prev.map((q) => (q.id === questId ? { ...q, isActive: true } : q)),
    );
    setWeeklyQuests((prev) =>
      prev.map((q) => (q.id === questId ? { ...q, isActive: true } : q)),
    );
  };

  const claimReward = (quest: Quest) => {
    if (!isQuestComplete(quest) || claimedIds.has(quest.id)) return;

    // Grant rewards
    if (quest.rewards.gold) {
      addGold(quest.rewards.gold, true);
    }
    if (quest.rewards.magicStones) {
      addMagicStones({ small: quest.rewards.magicStones }, true);
    }

    setClaimedIds((prev) => new Set([...prev, quest.id]));

    // Mark as completed
    setDailyQuests((prev) =>
      prev.map((q) => (q.id === quest.id ? { ...q, isCompleted: true } : q)),
    );
    setWeeklyQuests((prev) =>
      prev.map((q) => (q.id === quest.id ? { ...q, isCompleted: true } : q)),
    );
  };

  const activeCount = allQuests.filter(
    (q) => q.isActive && !q.isCompleted,
  ).length;
  const completedCount = allQuests.filter((q) => q.isCompleted).length;

  return (
    <div className="quests-tab">
      {/* Stats */}
      <div className="guild-quest-stats">
        <div className="guild-quest-stat">
          <span className="guild-quest-stat-value">{allQuests.length}</span>
          <span className="guild-quest-stat-label">Available</span>
        </div>
        <div className="guild-quest-stat">
          <span className="guild-quest-stat-value">{activeCount}</span>
          <span className="guild-quest-stat-label">Active</span>
        </div>
        <div className="guild-quest-stat">
          <span className="guild-quest-stat-value">{completedCount}</span>
          <span className="guild-quest-stat-label">Completed</span>
        </div>
      </div>

      {/* Filters */}
      <div className="guild-quest-filters">
        {(["all", "daily", "weekly", "active"] as QuestFilter[]).map((f) => (
          <button
            key={f}
            className={`guild-quest-filter-btn ${filter === f ? "active" : ""}`}
            onClick={() => setFilter(f)}
          >
            {f === "all"
              ? "All"
              : f === "daily"
                ? "Daily"
                : f === "weekly"
                  ? "Weekly"
                  : "Active"}
          </button>
        ))}
      </div>

      {/* Quest List */}
      <div className="guild-quest-list">
        {filteredQuests.length === 0 ? (
          <div className="guild-quest-empty">No quests available</div>
        ) : (
          filteredQuests.map((quest) => {
            const complete = isQuestComplete(quest);
            const claimed = claimedIds.has(quest.id);

            return (
              <div
                key={quest.id}
                className={`guild-quest-card ${quest.isCompleted ? "completed" : ""} ${quest.isActive ? "active" : ""}`}
              >
                <div className="guild-quest-card-header">
                  <div>
                    <h3 className="guild-quest-title">{quest.title}</h3>
                    <span className={`guild-quest-type-badge ${quest.type}`}>
                      {quest.type === "daily" ? "Daily" : "Weekly"}
                    </span>
                  </div>
                </div>

                <p className="guild-quest-description">{quest.description}</p>

                {/* Objectives */}
                <div className="guild-quest-objectives">
                  {quest.objectives.map((obj, idx) => (
                    <div key={idx} className="guild-quest-objective">
                      <span className="guild-quest-obj-desc">
                        {obj.description}
                      </span>
                      <span className="guild-quest-obj-progress">
                        {obj.current}/{obj.required}
                      </span>
                      <div className="guild-quest-progress-bar">
                        <div
                          className="guild-quest-progress-fill"
                          style={{
                            width: `${Math.min(100, (obj.current / obj.required) * 100)}%`,
                          }}
                        />
                      </div>
                    </div>
                  ))}
                </div>

                {/* Rewards */}
                <div className="guild-quest-rewards">
                  <span className="guild-quest-rewards-label">Rewards:</span>
                  {quest.rewards.gold && (
                    <span className="guild-quest-reward-item gold">
                      {quest.rewards.gold}G
                    </span>
                  )}
                  {quest.rewards.magicStones && (
                    <span className="guild-quest-reward-item stones">
                      {quest.rewards.magicStones} Stones
                    </span>
                  )}
                </div>

                {/* Action Button */}
                <div className="guild-quest-actions">
                  {!quest.isActive && !quest.isCompleted && (
                    <button
                      className="guild-quest-accept-btn"
                      onClick={() => acceptQuest(quest.id)}
                    >
                      Accept Quest
                    </button>
                  )}
                  {quest.isActive && complete && !claimed && (
                    <button
                      className="guild-quest-claim-btn"
                      onClick={() => claimReward(quest)}
                    >
                      Claim Reward
                    </button>
                  )}
                  {quest.isActive && !complete && !quest.isCompleted && (
                    <span className="guild-quest-in-progress">In Progress</span>
                  )}
                  {claimed && (
                    <span className="guild-quest-claimed">Claimed</span>
                  )}
                </div>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
};

export default QuestsTab;
