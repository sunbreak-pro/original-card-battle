// Quest template definitions for the Guild facility
// Daily and weekly quests with procedural generation

import type { Quest, QuestObjective, QuestReward } from "@/types/campTypes";

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
    titleTemplate: "討伐依頼：敵3体",
    descriptionTemplate: "ダンジョンで敵を3体倒せ。",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "defeat",
        target: "any",
        required: 3,
        current: 0,
        description: "敵を3体倒す",
      },
    ],
    rewards: { gold: 100 },
  },
  {
    titleTemplate: "討伐依頼：敵5体",
    descriptionTemplate: "ダンジョンで敵を5体倒せ。",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "defeat",
        target: "any",
        required: 5,
        current: 0,
        description: "敵を5体倒す",
      },
    ],
    rewards: { gold: 180, magicStones: 30 },
  },
  {
    titleTemplate: "探索任務",
    descriptionTemplate: "ダンジョンのノードを3つ踏破せよ。",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "explore",
        target: "any",
        required: 3,
        current: 0,
        description: "ノードを3つ踏破",
      },
    ],
    rewards: { gold: 120 },
  },
  {
    titleTemplate: "宝探し",
    descriptionTemplate: "宝箱部屋を1つ発見せよ。",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "explore",
        target: "treasure",
        required: 1,
        current: 0,
        description: "宝箱部屋を1つ発見",
      },
    ],
    rewards: { gold: 150, magicStones: 30 },
  },
  {
    titleTemplate: "精鋭狩り",
    descriptionTemplate: "エリート敵を1体倒せ。",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "defeat",
        target: "elite",
        required: 1,
        current: 0,
        description: "エリート敵を1体倒す",
      },
    ],
    rewards: { gold: 200, magicStones: 60 },
  },
  {
    titleTemplate: "生存者",
    descriptionTemplate: "ライフを失わずにダンジョンを踏破せよ。",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "survive",
        target: "run",
        required: 1,
        current: 0,
        description: "ライフを失わず踏破",
      },
    ],
    rewards: { gold: 250 },
  },
  {
    titleTemplate: "深層探索",
    descriptionTemplate: "ダンジョンのノードを5つ踏破せよ。",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "explore",
        target: "any",
        required: 5,
        current: 0,
        description: "ノードを5つ踏破",
      },
    ],
    rewards: { gold: 200, magicStones: 30 },
  },
  {
    titleTemplate: "殲滅の刻印",
    descriptionTemplate: "ダンジョンで敵を8体倒せ。",
    type: "daily",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "defeat",
        target: "any",
        required: 8,
        current: 0,
        description: "敵を8体倒す",
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
    titleTemplate: "週間討伐：大量殲滅",
    descriptionTemplate: "今週中に敵を20体倒せ。",
    type: "weekly",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "defeat",
        target: "any",
        required: 20,
        current: 0,
        description: "敵を20体倒す",
      },
    ],
    rewards: { gold: 500, magicStones: 100 },
  },
  {
    titleTemplate: "週間討伐：精鋭掃討",
    descriptionTemplate: "今週中にエリート敵を5体倒せ。",
    type: "weekly",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "defeat",
        target: "elite",
        required: 5,
        current: 0,
        description: "エリート敵を5体倒す",
      },
    ],
    rewards: { gold: 800, magicStones: 200 },
  },
  {
    titleTemplate: "週間探索：地図製作",
    descriptionTemplate: "今週中にノードを15個踏破せよ。",
    type: "weekly",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "explore",
        target: "any",
        required: 15,
        current: 0,
        description: "ノードを15個踏破",
      },
    ],
    rewards: { gold: 600, magicStones: 100 },
  },
  {
    titleTemplate: "週間討伐：ボスハンター",
    descriptionTemplate: "今週中にボスを2体倒せ。",
    type: "weekly",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "defeat",
        target: "boss",
        required: 2,
        current: 0,
        description: "ボスを2体倒す",
      },
    ],
    rewards: { gold: 1000, magicStones: 350 },
  },
  {
    titleTemplate: "週間生存：鉄の意志",
    descriptionTemplate: "今週中にダンジョンを3回踏破せよ。",
    type: "weekly",
    requiredGrade: "Apprentice",
    objectiveFactory: () => [
      {
        type: "survive",
        target: "run",
        required: 3,
        current: 0,
        description: "ダンジョンを3回踏破",
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
