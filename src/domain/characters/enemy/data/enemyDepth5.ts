import type { EnemyDefinition, EncounterPattern, DepthEnemyData } from "@/types/characterTypes";

export const STORM_ELEMENTAL: EnemyDefinition = {
  id: "depth5_storm_elemental",
  name: "Storm Elemental",
  nameJa: "嵐の精霊",
  description: "雷雲から生まれた純粋な嵐のエネルギー体",
  baseMaxHp: 170,
  baseMaxAp: 0,
  baseSpeed: 70,
  startingGuard: false,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "雷撃", type: "attack", baseDamage: 30, displayIcon: "⚡", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "稲妻の連鎖", type: "attack", baseDamage: 15, hitCount: 3, displayIcon: "🌩️", priority: 0, energyCost: 1 } },
    { phaseNumber: 3, action: { name: "麻痺の稲光", type: "debuff", baseDamage: 25, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "💫", priority: 2, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "雷撃", type: "attack", baseDamage: 30, displayIcon: "⚡", priority: 0, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, action: { name: "麻痺の稲光", type: "debuff", baseDamage: 25, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "💫", priority: 2, energyCost: 1 }, probability: 0.5 },
  ],
};

export const MAGMA_TITAN: EnemyDefinition = {
  id: "depth5_magma_titan",
  name: "Magma Titan",
  nameJa: "溶岩の巨人",
  description: "溶岩で構成された巨大な人型の存在",
  baseMaxHp: 220,
  baseMaxAp: 0,
  baseSpeed: 25,
  startingGuard: true,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "溶岩拳", type: "debuff", baseDamage: 30, applyDebuffs: [{ name: "burn", stacks: 2, duration: 3, value: 8, isPermanent: false }], displayIcon: "🌋", priority: 1, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "地殻粉砕", type: "attack", baseDamage: 35, displayIcon: "💥", priority: 0, energyCost: 1 } },
    { phaseNumber: 3, action: { name: "溶岩の鎧", type: "buff", baseDamage: 0, guardGain: 40, displayIcon: "🛡️", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "溶岩拳", type: "debuff", baseDamage: 30, applyDebuffs: [{ name: "burn", stacks: 2, duration: 3, value: 8, isPermanent: false }], displayIcon: "🌋", priority: 1, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, action: { name: "地殻粉砕", type: "attack", baseDamage: 35, displayIcon: "💥", priority: 0, energyCost: 1 }, probability: 0.5 },
  ],
};

export const FROST_WRAITH: EnemyDefinition = {
  id: "depth5_frost_wraith",
  name: "Frost Wraith",
  nameJa: "霜の亡霊",
  description: "絶対零度の冷気を纏う古代の怨霊",
  baseMaxHp: 150,
  baseMaxAp: 0,
  baseSpeed: 60,
  startingGuard: false,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "凍結の手", type: "debuff", baseDamage: 25, applyDebuffs: [{ name: "slow", stacks: 2, duration: 3, value: 25, isPermanent: false }], displayIcon: "❄️", priority: 1, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "氷柱の嵐", type: "attack", baseDamage: 18, hitCount: 2, displayIcon: "🌨️", priority: 0, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "凍結の手", type: "debuff", baseDamage: 25, applyDebuffs: [{ name: "slow", stacks: 2, duration: 3, value: 25, isPermanent: false }], displayIcon: "❄️", priority: 1, energyCost: 1 }, probability: 0.4 },
    { phaseNumber: 0, action: { name: "氷柱の嵐", type: "attack", baseDamage: 18, hitCount: 2, displayIcon: "🌨️", priority: 0, energyCost: 1 }, probability: 0.3 },
    { phaseNumber: 0, action: { name: "絶対零度", type: "debuff", baseDamage: 28, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "🧊", priority: 2, energyCost: 1 }, probability: 0.3 },
  ],
};

export const DIVINE_SENTINEL: EnemyDefinition = {
  id: "depth5_divine_sentinel",
  name: "Divine Sentinel",
  nameJa: "神聖なる守護者",
  description: "古代神殿を護り続ける光の戦士",
  baseMaxHp: 200,
  baseMaxAp: 0,
  baseSpeed: 45,
  startingGuard: true,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "聖剣", type: "attack", baseDamage: 32, displayIcon: "✨", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "聖なる障壁", type: "buff", baseDamage: 0, guardGain: 35, displayIcon: "🛡️", priority: 1, energyCost: 1 } },
    { phaseNumber: 3, action: { name: "審判の光", type: "debuff", baseDamage: 28, applyDebuffs: [{ name: "defDownMajor", stacks: 1, duration: 3, value: 35, isPermanent: false }], displayIcon: "🌟", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "聖剣", type: "attack", baseDamage: 32, displayIcon: "✨", priority: 0, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, action: { name: "審判の光", type: "debuff", baseDamage: 28, applyDebuffs: [{ name: "defDownMajor", stacks: 1, duration: 3, value: 35, isPermanent: false }], displayIcon: "🌟", priority: 1, energyCost: 1 }, probability: 0.5 },
  ],
};

export const CRYSTAL_SPRITE: EnemyDefinition = {
  id: "depth5_crystal_sprite",
  name: "Crystal Sprite",
  nameJa: "結晶の精霊",
  description: "光り輝く水晶から生まれた小さな精霊",
  baseMaxHp: 70,
  baseMaxAp: 0,
  baseSpeed: 80,
  startingGuard: false,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 0, action: { name: "結晶弾", type: "attack", baseDamage: 25, displayIcon: "💎", priority: 0, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, action: { name: "光の屈折", type: "debuff", baseDamage: 18, applyDebuffs: [{ name: "atkDownMinor", stacks: 1, duration: 3, value: 25, isPermanent: false }], displayIcon: "🔮", priority: 1, energyCost: 1 }, probability: 0.3 },
    { phaseNumber: 0, action: { name: "結晶化", type: "debuff", baseDamage: 20, applyDebuffs: [{ name: "slow", stacks: 1, duration: 2, value: 20, isPermanent: false }], displayIcon: "✨", priority: 1, energyCost: 1 }, probability: 0.2 },
  ],
};

export const ANCIENT_GOLEM: EnemyDefinition = {
  id: "depth5_ancient_golem",
  name: "Ancient Golem",
  nameJa: "太古のゴーレム",
  description: "神殿建設時に作られた古代の巨大石像兵",
  baseMaxHp: 120,
  baseMaxAp: 0,
  baseSpeed: 30,
  startingGuard: true,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "巨腕撃", type: "attack", baseDamage: 35, displayIcon: "🗿", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "古代の防壁", type: "buff", baseDamage: 0, guardGain: 40, displayIcon: "🛡️", priority: 1, energyCost: 1 } },
    { phaseNumber: 3, action: { name: "地震撃", type: "debuff", baseDamage: 28, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "🌍", priority: 2, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "巨腕撃", type: "attack", baseDamage: 35, displayIcon: "🗿", priority: 0, energyCost: 1 }, probability: 0.6 },
    { phaseNumber: 0, action: { name: "古代の防壁", type: "buff", baseDamage: 0, guardGain: 40, displayIcon: "🛡️", priority: 1, energyCost: 1 }, probability: 0.4 },
  ],
};

export const PHOENIX_HATCHLING: EnemyDefinition = {
  id: "depth5_phoenix",
  name: "Phoenix Hatchling",
  nameJa: "不死鳥の雛",
  description: "永遠の炎から生まれた幼い不死鳥",
  baseMaxHp: 55,
  baseMaxAp: 0,
  baseSpeed: 85,
  startingGuard: false,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 0, action: { name: "焔の翼", type: "debuff", baseDamage: 25, applyDebuffs: [{ name: "burn", stacks: 1, duration: 3, value: 8, isPermanent: false }], displayIcon: "🔥", priority: 1, energyCost: 1 }, probability: 0.6 },
    { phaseNumber: 0, action: { name: "炎の突進", type: "attack", baseDamage: 30, displayIcon: "🐦", priority: 0, energyCost: 1 }, probability: 0.4 },
  ],
};

export const CHRONOS_GUARDIAN: EnemyDefinition = {
  id: "depth5_boss",
  name: "Chronos Guardian",
  nameJa: "時の守護神クロノス",
  description: "時間そのものを操る古代神殿の最終守護者、万物の終焉と始まりを司る",
  baseMaxHp: 500,
  baseMaxAp: 0,
  baseSpeed: 55,
  startingGuard: true,
  actEnergy: 1,
  aiPatterns: [
    // Phase 1: HP > 66% - Time manipulation offense
    { phaseNumber: 1, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "時の剣", type: "attack", baseDamage: 30, displayIcon: "⏳", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "時空障壁", type: "buff", baseDamage: 0, guardGain: 45, displayIcon: "🛡️", priority: 1, energyCost: 1 } },
    { phaseNumber: 3, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "加速斬", type: "attack", baseDamage: 20, hitCount: 2, displayIcon: "⚡", priority: 0, energyCost: 1 } },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "時の剣", type: "attack", baseDamage: 30, displayIcon: "⏳", priority: 0, energyCost: 1 } },
    // Phase 2: HP 34-66% - Time decay and debuffs
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.34 && hp <= maxHp * 0.66, action: { name: "時の侵食", type: "debuff", baseDamage: 28, applyDebuffs: [{ name: "slow", stacks: 2, duration: 3, value: 30, isPermanent: false }], displayIcon: "🕰️", priority: 1, energyCost: 1 }, probability: 0.4 },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.34 && hp <= maxHp * 0.66, action: { name: "老化の呪い", type: "debuff", baseDamage: 25, applyDebuffs: [{ name: "atkDownMinor", stacks: 2, duration: 3, value: 30, isPermanent: false }], displayIcon: "💀", priority: 1, energyCost: 1 }, probability: 0.3 },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.34 && hp <= maxHp * 0.66, action: { name: "時間停止", type: "debuff", baseDamage: 30, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "⏸️", priority: 2, energyCost: 1 }, probability: 0.3 },
    // Phase 3: HP < 33% - Apocalyptic time powers
    { phaseNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.33, action: { name: "時の終焉", type: "attack", baseDamage: 40, displayIcon: "🌌", priority: 2, energyCost: 1 }, probability: 0.4 },
    { phaseNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.33, action: { name: "永劫回帰", type: "debuff", baseDamage: 30, applyDebuffs: [{ name: "bleed", stacks: 2, duration: 3, value: 10, isPermanent: false }], displayIcon: "♾️", priority: 2, energyCost: 1 }, probability: 0.3 },
    { phaseNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.33, action: { name: "次元崩壊", type: "attack", baseDamage: 22, hitCount: 3, displayIcon: "💥", priority: 2, energyCost: 1 }, probability: 0.3 },
  ],
};

const SINGLE_PATTERNS: EncounterPattern[] = [
  { id: "d5_s_storm",    nameJa: "嵐の精霊",       enemies: [STORM_ELEMENTAL] },
  { id: "d5_s_magma",    nameJa: "溶岩の巨人",     enemies: [MAGMA_TITAN] },
  { id: "d5_s_frost",    nameJa: "霜の亡霊",       enemies: [FROST_WRAITH] },
  { id: "d5_s_sentinel", nameJa: "神聖なる守護者", enemies: [DIVINE_SENTINEL] },
];

const DOUBLE_PATTERNS: EncounterPattern[] = [
  { id: "d5_d_golems",        nameJa: "太古のゴーレムの組", enemies: [ANCIENT_GOLEM, ANCIENT_GOLEM] },
  { id: "d5_d_storm_frost",   nameJa: "嵐と霜の精霊",       enemies: [STORM_ELEMENTAL, FROST_WRAITH] },
  { id: "d5_d_sprite_phoenix", nameJa: "結晶と不死鳥",      enemies: [CRYSTAL_SPRITE, PHOENIX_HATCHLING] },
  { id: "d5_d_magma_sentinel", nameJa: "溶岩と守護者",      enemies: [MAGMA_TITAN, DIVINE_SENTINEL] },
];

const THREE_PATTERNS: EncounterPattern[] = [
  { id: "d5_t_sprites",  nameJa: "結晶精霊の群",   enemies: [CRYSTAL_SPRITE, CRYSTAL_SPRITE, CRYSTAL_SPRITE] },
  { id: "d5_t_phoenix",  nameJa: "不死鳥の群",     enemies: [PHOENIX_HATCHLING, PHOENIX_HATCHLING, PHOENIX_HATCHLING] },
  { id: "d5_t_mixed",    nameJa: "神殿の混成群",   enemies: [CRYSTAL_SPRITE, ANCIENT_GOLEM, PHOENIX_HATCHLING] },
  { id: "d5_t_sprite_ph", nameJa: "結晶と不死鳥群", enemies: [CRYSTAL_SPRITE, CRYSTAL_SPRITE, PHOENIX_HATCHLING] },
];

export const DEPTH5_ENEMIES: DepthEnemyData = {
  single: SINGLE_PATTERNS,
  double: DOUBLE_PATTERNS,
  three: THREE_PATTERNS,
  boss: CHRONOS_GUARDIAN,
};

export const enemyList: EnemyDefinition[] = [
  STORM_ELEMENTAL,
  MAGMA_TITAN,
  FROST_WRAITH,
  DIVINE_SENTINEL,
  CRYSTAL_SPRITE,
  ANCIENT_GOLEM,
  PHOENIX_HATCHLING,
  CHRONOS_GUARDIAN,
];
