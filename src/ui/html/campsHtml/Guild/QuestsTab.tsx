/**
 * QuestsTab: Quest acceptance and tracking system
 *
 * Shows daily and weekly quests with objectives and rewards.
 * Players can accept quests, track progress, and claim rewards.
 * Uses GuildContext for persistent quest state across screens.
 */

import { useState, useEffect, useRef } from "react";
import { useGuild } from "@/contexts/GuildContext";
import { useResources } from "@/contexts/ResourceContext";
import { useToast } from "@/contexts/ToastContext";
import { isQuestComplete, isQuestExpired } from "@/constants/data/camps/QuestData";

type QuestFilter = "all" | "daily" | "weekly" | "active";

const FILTER_LABELS: Record<QuestFilter, string> = {
  all: "すべて",
  daily: "デイリー",
  weekly: "ウィークリー",
  active: "受注中",
};

const QuestsTab = () => {
  const {
    availableQuests,
    acceptedQuests,
    completedQuests,
    acceptQuest,
    claimQuestReward,
    refreshQuests,
  } = useGuild();
  const { addGold, addMagicStones } = useResources();
  const { addToast } = useToast();

  const [filter, setFilter] = useState<QuestFilter>("all");

  // Refresh expired quests on tab mount
  const refreshedRef = useRef(false);
  useEffect(() => {
    if (!refreshedRef.current) {
      refreshQuests();
      refreshedRef.current = true;
    }
  }, [refreshQuests]);

  // Combine available and accepted quests for display
  const displayQuests = [...availableQuests, ...acceptedQuests];

  const filteredQuests = (() => {
    switch (filter) {
      case "daily":
        return displayQuests.filter((q) => q.type === "daily");
      case "weekly":
        return displayQuests.filter((q) => q.type === "weekly");
      case "active":
        return acceptedQuests.filter((q) => !q.isCompleted);
      default:
        return displayQuests;
    }
  })();

  const handleAcceptQuest = (questId: string) => {
    const success = acceptQuest(questId);
    if (!success) {
      addToast({
        message: "依頼を受注できません（最大5件まで）",
        type: "alert",
        duration: 3000,
      });
    } else {
      addToast({
        message: "依頼を受注しました",
        type: "success",
        duration: 2000,
      });
    }
  };

  const handleClaimReward = (questId: string) => {
    const quest = claimQuestReward(questId);
    if (!quest) return;

    // Grant rewards via ResourceContext
    if (quest.rewards.gold) {
      addGold(quest.rewards.gold, true);
    }
    if (quest.rewards.magicStones) {
      addMagicStones({ small: quest.rewards.magicStones }, true);
    }

    addToast({
      message: `報酬を受け取りました${quest.rewards.gold ? ` ${quest.rewards.gold}G` : ""}${quest.rewards.magicStones ? ` 魔石×${quest.rewards.magicStones}` : ""}`,
      type: "success",
      duration: 3000,
    });
  };

  const activeCount = acceptedQuests.filter((q) => !q.isCompleted).length;
  const completableCount = acceptedQuests.filter(
    (q) => isQuestComplete(q) && !isQuestExpired(q),
  ).length;

  return (
    <div className="quests-tab">
      {/* Stats */}
      <div className="guild-quest-stats">
        <div className="guild-quest-stat">
          <span className="guild-quest-stat-value">
            {availableQuests.length}
          </span>
          <span className="guild-quest-stat-label">受注可能</span>
        </div>
        <div className="guild-quest-stat">
          <span className="guild-quest-stat-value">{activeCount}</span>
          <span className="guild-quest-stat-label">進行中</span>
        </div>
        <div className="guild-quest-stat">
          <span className="guild-quest-stat-value">{completableCount}</span>
          <span className="guild-quest-stat-label">完了</span>
        </div>
        <div className="guild-quest-stat">
          <span className="guild-quest-stat-value">
            {completedQuests.length}
          </span>
          <span className="guild-quest-stat-label">達成済</span>
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
            {FILTER_LABELS[f]}
          </button>
        ))}
      </div>

      {/* Quest List */}
      <div className="guild-quest-list">
        {filteredQuests.length === 0 ? (
          <div className="guild-quest-empty">
            {filter === "active"
              ? "受注中の依頼はありません"
              : "依頼がありません"}
          </div>
        ) : (
          filteredQuests.map((quest) => {
            const isAccepted = quest.isActive;
            const complete = isQuestComplete(quest);
            const expired = isQuestExpired(quest);

            return (
              <div
                key={quest.id}
                className={`guild-quest-card ${quest.isCompleted ? "completed" : ""} ${isAccepted ? "active" : ""} ${expired ? "expired" : ""}`}
              >
                <div className="guild-quest-card-header">
                  <div>
                    <h3 className="guild-quest-title">{quest.title}</h3>
                    <span className={`guild-quest-type-badge ${quest.type}`}>
                      {quest.type === "daily" ? "デイリー" : "ウィークリー"}
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
                  <span className="guild-quest-rewards-label">報酬:</span>
                  {quest.rewards.gold && (
                    <span className="guild-quest-reward-item gold">
                      {quest.rewards.gold}G
                    </span>
                  )}
                  {quest.rewards.magicStones && (
                    <span className="guild-quest-reward-item stones">
                      魔石×{quest.rewards.magicStones}
                    </span>
                  )}
                </div>

                {/* Action Button */}
                <div className="guild-quest-actions">
                  {expired && (
                    <span className="guild-quest-expired">期限切れ</span>
                  )}
                  {!expired && !isAccepted && !quest.isCompleted && (
                    <button
                      className="guild-quest-accept-btn"
                      onClick={() => handleAcceptQuest(quest.id)}
                    >
                      受注する
                    </button>
                  )}
                  {!expired && isAccepted && complete && (
                    <button
                      className="guild-quest-claim-btn"
                      onClick={() => handleClaimReward(quest.id)}
                    >
                      報酬を受け取る
                    </button>
                  )}
                  {!expired && isAccepted && !complete && !quest.isCompleted && (
                    <span className="guild-quest-in-progress">進行中</span>
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
