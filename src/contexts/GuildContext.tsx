// GuildContext - Manages rumor and quest state (B4.3)

import React, {
  createContext,
  useContext,
  useState,
  useCallback,
  type ReactNode,
} from "react";
import type {
  Rumor,
  Quest,
  QuestObjective,
} from '@/types/campTypes';
import { getRumorById, RUMORS } from "../constants/data/camps/RumorData";
import {
  generateDailyQuests,
  generateWeeklyQuests,
  checkQuestObjective,
  isQuestComplete,
  isQuestExpired,
} from "../constants/data/camps/QuestData";

// ========================================================================
// Active Rumor State
// ========================================================================

interface ActiveRumor {
  rumor: Rumor;
  remainingDuration: number; // Remaining exploration count
  activatedAt: number; // Timestamp
}

// ========================================================================
// Guild Context Value
// ========================================================================

interface GuildContextValue {
  // Rumors
  availableRumors: Rumor[];
  activeRumors: ActiveRumor[];
  activateRumor: (rumorId: string) => boolean;
  clearExpiredRumors: () => void;
  decrementRumorDurations: () => void;

  // Quests
  availableQuests: Quest[];
  acceptedQuests: Quest[];
  completedQuests: Quest[];
  acceptQuest: (questId: string) => boolean;
  updateQuestProgress: (
    eventType: "defeat" | "collect" | "explore" | "survive",
    targetId: string,
    count: number,
  ) => void;
  claimQuestReward: (questId: string) => Quest | null;
  refreshQuests: () => void;
}

const GuildContext = createContext<GuildContextValue | undefined>(undefined);

// ========================================================================
// Guild Provider
// ========================================================================

export const GuildProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  // Rumor state
  const [activeRumors, setActiveRumors] = useState<ActiveRumor[]>([]);

  // Quest state
  const [availableQuests, setAvailableQuests] = useState<Quest[]>(() => [
    ...generateDailyQuests(),
    ...generateWeeklyQuests(),
  ]);
  const [acceptedQuests, setAcceptedQuests] = useState<Quest[]>([]);
  const [completedQuests, setCompletedQuests] = useState<Quest[]>([]);

  // ========================================================================
  // Rumor Operations
  // ========================================================================

  const availableRumors = RUMORS.filter(
    (r) => !activeRumors.some((ar) => ar.rumor.id === r.id),
  );

  const activateRumor = useCallback(
    (rumorId: string): boolean => {
      const rumor = getRumorById(rumorId);
      if (!rumor) return false;

      // Check if already active
      if (activeRumors.some((ar) => ar.rumor.id === rumorId)) return false;

      const newActiveRumor: ActiveRumor = {
        rumor,
        remainingDuration: rumor.duration ?? 1,
        activatedAt: Date.now(),
      };

      setActiveRumors((prev) => [...prev, newActiveRumor]);
      return true;
    },
    [activeRumors],
  );

  const clearExpiredRumors = useCallback(() => {
    setActiveRumors((prev) => prev.filter((ar) => ar.remainingDuration > 0));
  }, []);

  const decrementRumorDurations = useCallback(() => {
    setActiveRumors((prev) =>
      prev
        .map((ar) => ({
          ...ar,
          remainingDuration: ar.remainingDuration - 1,
        }))
        .filter((ar) => ar.remainingDuration > 0),
    );
  }, []);

  // ========================================================================
  // Quest Operations
  // ========================================================================

  const acceptQuest = useCallback(
    (questId: string): boolean => {
      const quest = availableQuests.find((q) => q.id === questId);
      if (!quest) return false;

      // Check if already at max accepted (5 total)
      if (acceptedQuests.length >= 5) return false;

      const activatedQuest: Quest = { ...quest, isActive: true };
      setAcceptedQuests((prev) => [...prev, activatedQuest]);
      setAvailableQuests((prev) => prev.filter((q) => q.id !== questId));
      return true;
    },
    [availableQuests, acceptedQuests.length],
  );

  const updateQuestProgress = useCallback(
    (
      eventType: "defeat" | "collect" | "explore" | "survive",
      targetId: string,
      count: number,
    ) => {
      setAcceptedQuests((prev) =>
        prev.map((quest) => {
          if (quest.isCompleted) return quest;
          if (isQuestExpired(quest)) return quest;

          const newObjectives = quest.objectives.map((obj: QuestObjective) =>
            checkQuestObjective(obj, eventType, targetId, count),
          );

          const updatedQuest = { ...quest, objectives: newObjectives };
          if (isQuestComplete(updatedQuest)) {
            return { ...updatedQuest, isCompleted: true };
          }
          return updatedQuest;
        }),
      );
    },
    [],
  );

  const claimQuestReward = useCallback(
    (questId: string): Quest | null => {
      const quest = acceptedQuests.find(
        (q) => q.id === questId && q.isCompleted,
      );
      if (!quest) return null;

      // Move to completed
      setAcceptedQuests((prev) => prev.filter((q) => q.id !== questId));
      setCompletedQuests((prev) => [...prev, quest]);

      return quest; // Caller should apply rewards via ResourceContext
    },
    [acceptedQuests],
  );

  const refreshQuests = useCallback(() => {
    // Remove expired available quests and generate new ones
    const nonExpiredAvailable = availableQuests.filter(
      (q) => !isQuestExpired(q),
    );

    const hasDailies = nonExpiredAvailable.some((q) => q.type === "daily");
    const hasWeeklies = nonExpiredAvailable.some((q) => q.type === "weekly");

    let newQuests = [...nonExpiredAvailable];
    if (!hasDailies) {
      newQuests = [...newQuests, ...generateDailyQuests()];
    }
    if (!hasWeeklies) {
      newQuests = [...newQuests, ...generateWeeklyQuests()];
    }

    setAvailableQuests(newQuests);

    // Also clean up expired accepted quests
    setAcceptedQuests((prev) => prev.filter((q) => !isQuestExpired(q)));
  }, [availableQuests]);

  return (
    <GuildContext.Provider
      value={{
        availableRumors,
        activeRumors,
        activateRumor,
        clearExpiredRumors,
        decrementRumorDurations,
        availableQuests,
        acceptedQuests,
        completedQuests,
        acceptQuest,
        updateQuestProgress,
        claimQuestReward,
        refreshQuests,
      }}
    >
      {children}
    </GuildContext.Provider>
  );
};

/**
 * Hook to use Guild context
 */
export const useGuild = () => {
  const context = useContext(GuildContext);
  if (!context) {
    throw new Error("useGuild must be used within GuildProvider");
  }
  return context;
};
