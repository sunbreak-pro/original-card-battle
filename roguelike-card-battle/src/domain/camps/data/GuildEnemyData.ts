// Guild-specific enemies for promotion exams
// These enemies only appear in exam battles, not in normal dungeon exploration

import type { Enemy } from "../../characters/type/enemyType";

/**
 * Swordsman Promotion Exam Enemies
 */

// Rank 1: Apprentice Swordsman → Swordsman
export const TRAINING_DUMMY: Enemy = {
  id: "exam_training_dummy",
  name: "Training Dummy",
  nameJa: "訓練用木人",
  description: "Basic training dummy for practicing swordsmanship",
  maxHp: 50,
  hp: 50,
  maxAp: 0,
  ap: 0,
  startingGuard: false,
  guard: 0,
  actEnergy: 2,
  speed: 40,
  aiPatterns: [
    {
      phaseNumber: 0,
      action: {
        name: "Wooden Strike",
        type: "attack",
        baseDamage: 8,
        displayIcon: "⚔️",
        priority: 0,
        energyCost: 1,
      },
    },
  ],
};

// Rank 2: Swordsman → Sword Master
export const GUILD_INSTRUCTOR: Enemy = {
  id: "exam_guild_instructor",
  name: "Guild Instructor",
  nameJa: "ギルド教官",
  description: "Veteran warrior who trains guild members",
  maxHp: 120,
  hp: 120,
  maxAp: 0,
  ap: 0,
  startingGuard: false,
  guard: 0,
  actEnergy: 3,
  speed: 55,
  aiPatterns: [
    {
      phaseNumber: 1,
      action: {
        name: "Instructor's Strike",
        type: "attack",
        baseDamage: 15,
        displayIcon: "⚔️",
        priority: 0,
        energyCost: 2,
      },
    },
    {
      phaseNumber: 2,
      action: {
        name: "Defensive Stance",
        type: "buff",
        baseDamage: 0,
        guardGain: 20,
        displayIcon: "🛡️",
        priority: 1,
        energyCost: 1,
      },
    },
    {
      phaseNumber: 0,
      action: {
        name: "Instructor's Strike",
        type: "attack",
        baseDamage: 15,
        displayIcon: "⚔️",
        priority: 0,
        energyCost: 2,
      },
      probability: 0.7,
    },
    {
      phaseNumber: 0,
      action: {
        name: "Defensive Stance",
        type: "buff",
        baseDamage: 0,
        guardGain: 20,
        displayIcon: "🛡️",
        priority: 1,
        energyCost: 1,
      },
      probability: 0.3,
    },
  ],
};

// Rank 3: Sword Master → Sword Saint
export const VETERAN_WARRIOR: Enemy = {
  id: "exam_veteran_warrior",
  name: "Veteran Warrior",
  nameJa: "歴戦の戦士",
  description: "Battle-hardened warrior with decades of experience",
  maxHp: 200,
  hp: 200,
  maxAp: 0,
  ap: 0,
  startingGuard: false,
  guard: 0,
  actEnergy: 4,
  speed: 60,
  aiPatterns: [
    {
      phaseNumber: 1,
      action: {
        name: "Skilled Slash",
        type: "attack",
        baseDamage: 20,
        displayIcon: "⚔️",
        priority: 0,
        energyCost: 2,
      },
    },
    {
      phaseNumber: 2,
      action: {
        name: "Iron Defense",
        type: "buff",
        baseDamage: 0,
        guardGain: 30,
        displayIcon: "🛡️",
        priority: 1,
        energyCost: 2,
      },
    },
    {
      phaseNumber: 0,
      action: {
        name: "Skilled Slash",
        type: "attack",
        baseDamage: 20,
        displayIcon: "⚔️",
        priority: 0,
        energyCost: 2,
      },
      probability: 0.5,
    },
    {
      phaseNumber: 0,
      action: {
        name: "Combo Strike",
        type: "attack",
        baseDamage: 12,
        hitCount: 2,
        displayIcon: "⚔️⚔️",
        priority: 0,
        energyCost: 3,
        applyDebuffs: [
          {
            name: "bleed",
            stacks: 1,
            duration: 2,
            value: 5,
            isPermanent: false,
          },
        ],
      },
      probability: 0.3,
    },
    {
      phaseNumber: 0,
      action: {
        name: "Iron Defense",
        type: "buff",
        baseDamage: 0,
        guardGain: 30,
        displayIcon: "🛡️",
        priority: 1,
        energyCost: 2,
      },
      probability: 0.2,
    },
  ],
};

// Rank 4: Sword Saint → Sword God
export const SWORD_SAINT_PHANTOM: Enemy = {
  id: "exam_sword_saint_phantom",
  name: "Phantom of the Sword Saint",
  nameJa: "剣聖の幻影",
  description: "Manifestation of a legendary sword saint's spirit",
  maxHp: 350,
  hp: 350,
  maxAp: 0,
  ap: 0,
  startingGuard: false,
  guard: 0,
  actEnergy: 5,
  speed: 70,
  aiPatterns: [
    // Phase 1 (HP > 50%)
    {
      phaseNumber: 1,
      action: {
        name: "Divine Slash",
        type: "attack",
        baseDamage: 25,
        displayIcon: "⚡⚔️",
        priority: 0,
        energyCost: 3,
      },
    },
    {
      phaseNumber: 2,
      action: {
        name: "Sword Spirit Release",
        type: "attack",
        baseDamage: 18,
        displayIcon: "🌊",
        priority: 1,
        energyCost: 2,
        applyDebuffs: [
          {
            name: "defDownMinor",
            stacks: 1,
            duration: 2,
            value: 20,
            isPermanent: false,
          },
        ],
      },
    },
    // Phase 2 (HP <= 50%)
    {
      phaseNumber: 5,
      condition: (hp, maxHp) => hp <= maxHp * 0.5,
      action: {
        name: "Ultimate Art: Void Blade",
        type: "attack",
        baseDamage: 40,
        displayIcon: "💥",
        priority: 0,
        energyCost: 5,
        applyDebuffs: [
          {
            name: "stun",
            stacks: 1,
            duration: 1,
            value: 0,
            isPermanent: false,
          },
        ],
      },
    },
    {
      phaseNumber: 0,
      action: {
        name: "Divine Slash",
        type: "attack",
        baseDamage: 25,
        displayIcon: "⚡⚔️",
        priority: 0,
        energyCost: 3,
      },
      probability: 0.6,
    },
    {
      phaseNumber: 0,
      action: {
        name: "Sword Spirit Release",
        type: "attack",
        baseDamage: 18,
        displayIcon: "🌊",
        priority: 1,
        energyCost: 2,
        applyDebuffs: [
          {
            name: "defDownMinor",
            stacks: 1,
            duration: 2,
            value: 20,
            isPermanent: false,
          },
        ],
      },
      probability: 0.4,
    },
  ],
};

/**
 * Mage Promotion Exam Enemies
 */

export const MAGIC_GOLEM: Enemy = {
  id: "exam_magic_golem",
  name: "Magic Training Golem",
  nameJa: "魔法訓練用ゴーレム",
  description: "Golem designed for mage training",
  maxHp: 45,
  hp: 45,
  maxAp: 0,
  ap: 0,
  startingGuard: true,
  guard: 10,
  actEnergy: 2,
  speed: 35,
  aiPatterns: [
    {
      phaseNumber: 0,
      action: {
        name: "Magic Bullet",
        type: "attack",
        baseDamage: 10,
        displayIcon: "✨",
        priority: 0,
        energyCost: 1,
      },
    },
  ],
};

/**
 * Summoner Promotion Exam Enemies
 */

export const WILD_SPIRIT: Enemy = {
  id: "exam_wild_spirit",
  name: "Wild Spirit",
  nameJa: "野生の精霊",
  description: "Untamed spirit for summoner training",
  maxHp: 40,
  hp: 40,
  maxAp: 0,
  ap: 0,
  startingGuard: false,
  guard: 0,
  actEnergy: 2,
  speed: 50,
  aiPatterns: [
    {
      phaseNumber: 0,
      action: {
        name: "Spirit Blast",
        type: "attack",
        baseDamage: 9,
        displayIcon: "🔮",
        priority: 0,
        energyCost: 1,
      },
    },
  ],
};

/**
 * Export all guild enemies
 */
export const GUILD_ENEMIES: Enemy[] = [
  TRAINING_DUMMY,
  GUILD_INSTRUCTOR,
  VETERAN_WARRIOR,
  SWORD_SAINT_PHANTOM,
  MAGIC_GOLEM,
  WILD_SPIRIT,
];

/**
 * Helper function to get guild enemy by ID
 */
export function getGuildEnemy(id: string): Enemy | undefined {
  return GUILD_ENEMIES.find((enemy) => enemy.id === id);
}
