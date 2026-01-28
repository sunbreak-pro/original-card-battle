// Quest template definitions for the Guild facility
// Daily and weekly quests with procedural generation

import type { Quest, QuestObjective, QuestReward } from "../../../domain/camps/types/GuildTypes";

/**
 * Quest template for procedural generation
 */
interface QuestTemplate {
  titleTemplate: string;
  descriptionTemplate: string;
  type: "daily" | "weekly";
  requiredGrade: string;
  objectiveFactory: () => QuestObjective[];
  rewards: QuestReward;
}

/**
 * Daily quest templates (8 templates)
 */
const DAILY_QUEST_TEMPLATES: QuestTemplate[] = [
  {
    titleTemplate: "Slay 3 Enemies",
    descriptionTemplate: "Defeat 3 enemies in any dungeon depth.",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "defeat",
        target: "any",
        required: 3,
        current: 0,
        description: "Defeat 3 enemies",
      },
    ],
    rewards: { gold: 100 },
  },
  {
    titleTemplate: "Slay 5 Enemies",
    descriptionTemplate: "Defeat 5 enemies in any dungeon depth.",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "defeat",
        target: "any",
        required: 5,
        current: 0,
        description: "Defeat 5 enemies",
      },
    ],
    rewards: { gold: 180, magicStones: 30 },
  },
  {
    titleTemplate: "Explorer's Duty",
    descriptionTemplate: "Complete 3 dungeon nodes in a single run.",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "explore",
        target: "any",
        required: 3,
        current: 0,
        description: "Complete 3 dungeon nodes",
      },
    ],
    rewards: { gold: 120 },
  },
  {
    titleTemplate: "Treasure Seeker",
    descriptionTemplate: "Find and open 1 treasure room.",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "explore",
        target: "treasure",
        required: 1,
        current: 0,
        description: "Find 1 treasure room",
      },
    ],
    rewards: { gold: 150, magicStones: 30 },
  },
  {
    titleTemplate: "Elite Hunter",
    descriptionTemplate: "Defeat 1 elite enemy.",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "defeat",
        target: "elite",
        required: 1,
        current: 0,
        description: "Defeat 1 elite enemy",
      },
    ],
    rewards: { gold: 200, magicStones: 60 },
  },
  {
    titleTemplate: "Survivalist",
    descriptionTemplate: "Complete a dungeon run without losing a life.",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "survive",
        target: "run",
        required: 1,
        current: 0,
        description: "Complete a run without losing a life",
      },
    ],
    rewards: { gold: 250 },
  },
  {
    titleTemplate: "Deep Explorer",
    descriptionTemplate: "Complete 5 dungeon nodes in a single run.",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "explore",
        target: "any",
        required: 5,
        current: 0,
        description: "Complete 5 dungeon nodes",
      },
    ],
    rewards: { gold: 200, magicStones: 30 },
  },
  {
    titleTemplate: "Slayer's Mark",
    descriptionTemplate: "Defeat 8 enemies in any dungeon depth.",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "defeat",
        target: "any",
        required: 8,
        current: 0,
        description: "Defeat 8 enemies",
      },
    ],
    rewards: { gold: 300, magicStones: 60 },
  },
];

/**
 * Weekly quest templates (5 templates)
 */
const WEEKLY_QUEST_TEMPLATES: QuestTemplate[] = [
  {
    titleTemplate: "Weekly Bounty: Mass Slayer",
    descriptionTemplate: "Defeat 20 enemies this week.",
    type: "weekly",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "defeat",
        target: "any",
        required: 20,
        current: 0,
        description: "Defeat 20 enemies",
      },
    ],
    rewards: { gold: 500, magicStones: 100 },
  },
  {
    titleTemplate: "Weekly Bounty: Elite Purge",
    descriptionTemplate: "Defeat 5 elite enemies this week.",
    type: "weekly",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "defeat",
        target: "elite",
        required: 5,
        current: 0,
        description: "Defeat 5 elite enemies",
      },
    ],
    rewards: { gold: 800, magicStones: 200 },
  },
  {
    titleTemplate: "Weekly Bounty: Cartographer",
    descriptionTemplate: "Complete 15 dungeon nodes this week.",
    type: "weekly",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "explore",
        target: "any",
        required: 15,
        current: 0,
        description: "Complete 15 dungeon nodes",
      },
    ],
    rewards: { gold: 600, magicStones: 100 },
  },
  {
    titleTemplate: "Weekly Bounty: Boss Hunter",
    descriptionTemplate: "Defeat 2 boss enemies this week.",
    type: "weekly",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "defeat",
        target: "boss",
        required: 2,
        current: 0,
        description: "Defeat 2 boss enemies",
      },
    ],
    rewards: { gold: 1000, magicStones: 350 },
  },
  {
    titleTemplate: "Weekly Bounty: Iron Will",
    descriptionTemplate: "Complete 3 dungeon runs without losing all lives.",
    type: "weekly",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "survive",
        target: "run",
        required: 3,
        current: 0,
        description: "Complete 3 runs without losing all lives",
      },
    ],
    rewards: { gold: 700, magicStones: 150 },
  },
];

/**
 * Generate daily quests (pick 3 random from templates)
 */
export function generateDailyQuests(): Quest[] {
  const shuffled = [...DAILY_QUEST_TEMPLATES].sort(() => Math.random() - 0.5);
  const selected = shuffled.slice(0, 3);

  const now = new Date();
  const endOfDay = new Date(now);
  endOfDay.setHours(23, 59, 59, 999);

  return selected.map((template, index) => ({
    id: `daily_${Date.now()}_${index}`,
    title: template.titleTemplate,
    description: template.descriptionTemplate,
    type: template.type,
    requiredGrade: template.requiredGrade,
    objectives: template.objectiveFactory(),
    rewards: template.rewards,
    isActive: false,
    isCompleted: false,
    expiresAt: endOfDay,
  }));
}

/**
 * Generate weekly quests (pick 2 random from templates)
 */
export function generateWeeklyQuests(): Quest[] {
  const shuffled = [...WEEKLY_QUEST_TEMPLATES].sort(() => Math.random() - 0.5);
  const selected = shuffled.slice(0, 2);

  const now = new Date();
  const endOfWeek = new Date(now);
  endOfWeek.setDate(endOfWeek.getDate() + (7 - endOfWeek.getDay()));
  endOfWeek.setHours(23, 59, 59, 999);

  return selected.map((template, index) => ({
    id: `weekly_${Date.now()}_${index}`,
    title: template.titleTemplate,
    description: template.descriptionTemplate,
    type: template.type,
    requiredGrade: template.requiredGrade,
    objectives: template.objectiveFactory(),
    rewards: template.rewards,
    isActive: false,
    isCompleted: false,
    expiresAt: endOfWeek,
  }));
}

/**
 * Check if a quest objective matches the given event
 */
export function checkQuestObjective(
  objective: QuestObjective,
  eventType: "defeat" | "collect" | "explore" | "survive",
  targetId: string,
  count: number
): QuestObjective {
  // Check if this objective matches the event
  if (objective.type !== eventType) return objective;

  // Check target match (any matches everything)
  if (objective.target !== "any" && objective.target !== targetId) {
    return objective;
  }

  // Update progress
  const newCurrent = Math.min(objective.current + count, objective.required);
  return { ...objective, current: newCurrent };
}

/**
 * Check if all objectives of a quest are completed
 */
export function isQuestComplete(quest: Quest): boolean {
  return quest.objectives.every((obj) => obj.current >= obj.required);
}

/**
 * Check if a quest has expired
 */
export function isQuestExpired(quest: Quest): boolean {
  if (!quest.expiresAt) return false;
  return new Date() > quest.expiresAt;
}
