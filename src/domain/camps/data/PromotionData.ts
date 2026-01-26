// Promotion exam definitions for all character classes
import type { CharacterClass } from "../../characters/type/baseTypes";
import type { PromotionExam } from "../types/GuildTypes";

/**
 * Swordsman Promotion Exams
 */
export const SWORDSMAN_EXAMS: PromotionExam[] = [
  {
    currentGrade: "見習い剣士", // Apprentice Swordsman
    nextGrade: "剣士", // Swordsman
    requiredCardCount: 5,
    enemyId: "exam_training_dummy",
    description:
      "Defeat the Training Dummy to prove your basic swordsmanship skills.",
    recommendations: {
      hp: 60,
      ap: 40,
    },
    rewards: {
      statBonus: "maxHP+10, Quest Slot+1",
    },
  },
  {
    currentGrade: "剣士", // Swordsman
    nextGrade: "剣豪", // Sword Master
    requiredCardCount: 15,
    requiredGold: 100,
    enemyId: "exam_guild_instructor",
    description:
      "Demonstrate your martial prowess in a mock battle with the Guild Instructor.",
    recommendations: {
      hp: 80,
      ap: 60,
    },
    rewards: {
      statBonus: "ATK+5%, Reward Bonus",
    },
  },
  {
    currentGrade: "剣豪", // Sword Master
    nextGrade: "剣聖", // Sword Saint
    requiredCardCount: 30,
    requiredGold: 500,
    enemyId: "exam_veteran_warrior",
    description:
      "Defeat the Veteran Warrior to master the ultimate path of the sword.",
    recommendations: {
      hp: 120,
      ap: 80,
    },
    rewards: {
      statBonus: "All Stats+5%, Critical Rate+10%",
    },
  },
  {
    currentGrade: "剣聖", // Sword Saint
    nextGrade: "剣神", // Sword God
    requiredCardCount: 50,
    requiredGold: 1000,
    enemyId: "exam_sword_saint_phantom",
    description:
      "Win the death match against the Phantom of the Sword Saint to reach the realm of gods.",
    recommendations: {
      hp: 150,
      ap: 100,
    },
    rewards: {
      statBonus: "Unlock Unique Legend Skill, All Stats+10%",
    },
  },
];

/**
 * Mage Promotion Exams
 */
export const MAGE_EXAMS: PromotionExam[] = [
  {
    currentGrade: "Apprentice Mage",
    nextGrade: "Mage",
    requiredCardCount: 5,
    enemyId: "exam_magic_golem",
    description:
      "Control the Magic Golem to prove your mastery of mana manipulation.",
    recommendations: {
      hp: 50,
      ap: 35,
    },
    rewards: {
      statBonus: "maxHP+8, maxAP+5",
    },
  },
  {
    currentGrade: "Mage",
    nextGrade: "Archmage",
    requiredCardCount: 15,
    requiredGold: 100,
    enemyId: "exam_guild_instructor",
    description: "Demonstrate your magical prowess to the Guild Instructor.",
    recommendations: {
      hp: 70,
      ap: 55,
    },
    rewards: {
      statBonus: "Magic Power+10%, Spell Cost-5%",
    },
  },
  {
    currentGrade: "Archmage",
    nextGrade: "Sage",
    requiredCardCount: 30,
    requiredGold: 500,
    enemyId: "exam_veteran_warrior",
    description: "Overcome the Veteran Warrior with your magical might.",
    recommendations: {
      hp: 100,
      ap: 75,
    },
    rewards: {
      statBonus: "All Stats+5%, Mana Regeneration+1",
    },
  },
  {
    currentGrade: "Sage",
    nextGrade: "Grand Magus",
    requiredCardCount: 50,
    requiredGold: 1000,
    enemyId: "exam_sword_saint_phantom",
    description:
      "Face the ultimate test to become a legendary master of arcane arts.",
    recommendations: {
      hp: 130,
      ap: 95,
    },
    rewards: {
      statBonus: "Unlock Unique Legend Spell, All Stats+10%",
    },
  },
];

/**
 * Summoner Promotion Exams
 */
export const SUMMONER_EXAMS: PromotionExam[] = [
  {
    currentGrade: "Apprentice Summoner",
    nextGrade: "Summoner",
    requiredCardCount: 5,
    enemyId: "exam_wild_spirit",
    description:
      "Tame the Wild Spirit to prove your ability to command creatures.",
    recommendations: {
      hp: 55,
      ap: 38,
    },
    rewards: {
      statBonus: "maxHP+9, Summon Slot+1",
    },
  },
  {
    currentGrade: "Summoner",
    nextGrade: "Beast Master",
    requiredCardCount: 15,
    requiredGold: 100,
    enemyId: "exam_guild_instructor",
    description:
      "Show the Guild Instructor your mastery over summoned creatures.",
    recommendations: {
      hp: 75,
      ap: 58,
    },
    rewards: {
      statBonus: "Summon Power+8%, Beast Bond+1",
    },
  },
  {
    currentGrade: "Beast Master",
    nextGrade: "Spirit Caller",
    requiredCardCount: 30,
    requiredGold: 500,
    enemyId: "exam_veteran_warrior",
    description:
      "Defeat the Veteran Warrior with your loyal summons by your side.",
    recommendations: {
      hp: 110,
      ap: 78,
    },
    rewards: {
      statBonus: "All Stats+5%, Summon Duration+2",
    },
  },
  {
    currentGrade: "Spirit Caller",
    nextGrade: "Mythic Summoner",
    requiredCardCount: 50,
    requiredGold: 1000,
    enemyId: "exam_sword_saint_phantom",
    description:
      "Prove you can command even the mightiest of legendary creatures.",
    recommendations: {
      hp: 140,
      ap: 98,
    },
    rewards: {
      statBonus: "Unlock Unique Legend Summon, All Stats+10%",
    },
  },
];

/**
 * Get exam data based on character class
 */
export function getExamsForClass(
  characterClass: CharacterClass
): PromotionExam[] {
  switch (characterClass) {
    case "swordsman":
      return SWORDSMAN_EXAMS;
    case "mage":
      return MAGE_EXAMS;
    case "summoner":
      return SUMMONER_EXAMS;
    default:
      return [];
  }
}

/**
 * Get next exam based on current grade
 */
export function getNextExam(
  characterClass: CharacterClass,
  currentGrade: string
): PromotionExam | null {
  const exams = getExamsForClass(characterClass);
  return exams.find((exam) => exam.currentGrade === currentGrade) || null;
}

/**
 * Check if player can take the exam
 */
export function canTakeExam(
  exam: PromotionExam,
  cardCount: number,
  gold: number
): boolean {
  const meetsCardRequirement = cardCount >= exam.requiredCardCount;
  const meetsGoldRequirement = exam.requiredGold
    ? gold >= exam.requiredGold
    : true;
  return meetsCardRequirement && meetsGoldRequirement;
}
