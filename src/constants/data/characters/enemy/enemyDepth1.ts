import type { EnemyDefinition, EncounterPattern, DepthEnemyData } from '@/types/characterTypes';

export const CORRUPTED_HOUND: EnemyDefinition = {
  id: "depth1_hound",
  imagePath: "/assets/images/enemies/depth1_hound.png",
  name: "Corrupted Hound",
  nameJa: "赤狼",
  description: "腐肉が露出した痩せこけた黒い野犬",
  baseMaxHp: 40,
  baseMaxAp: 0,
  baseSpeed: 40,
  startingGuard: false,
  actEnergy: 1,
  displayWidth: 18,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "噛みつき", type: "attack", baseDamage: 7, displayIcon: "⚔️", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "腐肉の牙", type: "debuff", baseDamage: 7, applyDebuffs: [{ name: "poison", stacks: 1, duration: 2, value: 3, isPermanent: false }], displayIcon: "🦷", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "噛みつき", type: "attack", baseDamage: 7, displayIcon: "⚔️", priority: 0, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, action: { name: "腐肉の牙", type: "debuff", baseDamage: 7, applyDebuffs: [{ name: "poison", stacks: 1, duration: 2, value: 3, isPermanent: false }], displayIcon: "🦷", priority: 1, energyCost: 1 }, probability: 0.5 },
  ],
};

export const MUTATED_CROW: EnemyDefinition = {
  id: "depth1_crow",
  imagePath: "/assets/images/enemies/depth1_crow.png",
  name: "Mutated Carrion Crow",
  nameJa: "瘴気鷹",
  description: "羽が抜け落ちた灰色の鴉",
  baseMaxHp: 35,
  baseMaxAp: 0,
  baseSpeed: 55,
  startingGuard: false,
  actEnergy: 1,
  displayWidth: 16,
  aiPatterns: [
    { phaseNumber: 0, action: { name: "連続啄み", type: "attack", baseDamage: 5, hitCount: 2, displayIcon: "🦅", priority: 0, energyCost: 1 }, probability: 0.8 },
    { phaseNumber: 0, action: { name: "酸の唾液", type: "debuff", baseDamage: 3, applyDebuffs: [{ name: "defDownMajor", stacks: 1, duration: 3, value: 30, isPermanent: false }], displayIcon: "💧", priority: 1, energyCost: 1 }, probability: 0.2 },
  ],
};

export const BONE_WANDERER: EnemyDefinition = {
  id: "depth1_skeleton",
  imagePath: "/assets/images/enemies/depth1_skeleton.png",
  name: "Bone Wanderer",
  nameJa: "ワンダーボーン",
  description: "白骨化した人型の骸骨",
  baseMaxHp: 38,
  baseMaxAp: 0,
  baseSpeed: 35,
  startingGuard: false,
  actEnergy: 1,
  displayWidth: 18,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "骨の剣", type: "attack", baseDamage: 6, displayIcon: "🗡️", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "骨の剣", type: "attack", baseDamage: 6, displayIcon: "🗡️", priority: 0, energyCost: 1 } },
    { phaseNumber: 3, action: { name: "骨砕き", type: "debuff", baseDamage: 10, applyDebuffs: [{ name: "slow", stacks: 1, duration: 1, value: 10, isPermanent: false }], displayIcon: "💀", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "骨の剣", type: "attack", baseDamage: 6, displayIcon: "🗡️", priority: 0, energyCost: 1 } },
  ],
};

export const SHADOW_CRAWLER: EnemyDefinition = {
  id: "depth1_shadow",
  imagePath: "/assets/images/enemies/depth1_shadow.png",
  name: "Shadow Crawler",
  nameJa: "クローラー",
  description: "黒い霧状の人型生物",
  baseMaxHp: 25,
  baseMaxAp: 0,
  baseSpeed: 60,
  startingGuard: false,
  actEnergy: 1,
  displayWidth: 15,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "影の触手", type: "attack", baseDamage: 8, displayIcon: "🌑", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "闇の侵食", type: "debuff", baseDamage: 6, applyDebuffs: [{ name: "atkDownMinor", stacks: 1, duration: 3, value: 15, isPermanent: false }], displayIcon: "🌫️", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "影の触手", type: "attack", baseDamage: 8, displayIcon: "🌑", priority: 0, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, action: { name: "闇の侵食", type: "debuff", baseDamage: 6, applyDebuffs: [{ name: "atkDownMinor", stacks: 1, duration: 3, value: 15, isPermanent: false }], displayIcon: "🌫️", priority: 1, energyCost: 1 }, probability: 0.5 },
  ],
};

export const FLESH_EATER: EnemyDefinition = {
  id: "depth1_flesh_eater",
  imagePath: "/assets/images/enemies/depth1_flesh_eater.png",
  name: "Flesh Eater",
  nameJa: "腐肉喰らい",
  description: "腐敗した肉塊から無数の触手が生えた小型の生物",
  baseMaxHp: 18,
  baseMaxAp: 0,
  baseSpeed: 45,
  startingGuard: false,
  actEnergy: 1,
  displayWidth: 14,
  aiPatterns: [
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.5, action: { name: "触手攻撃", type: "attack", baseDamage: 5, displayIcon: "🦑", priority: 0, energyCost: 1 } },
    { phaseNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.5, action: { name: "狂乱", type: "attack", baseDamage: 7, displayIcon: "💢", priority: 1, energyCost: 1 } },
  ],
};

export const RUSTY_SWORDSMAN: EnemyDefinition = {
  id: "depth1_rusty_swordsman",
  imagePath: "/assets/images/enemies/depth1_rusty_swordsman.png",
  name: "Rusty Swordsman",
  nameJa: "錆びた剣士",
  description: "錆びた鎧を纏った亡霊騎士",
  baseMaxHp: 30,
  baseMaxAp: 0,
  baseSpeed: 50,
  startingGuard: true,
  actEnergy: 1,
  displayWidth: 18,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "斬撃", type: "attack", baseDamage: 8, displayIcon: "⚔️", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "斬撃", type: "attack", baseDamage: 8, displayIcon: "⚔️", priority: 0, energyCost: 1 } },
    { phaseNumber: 3, action: { name: "二段斬り", type: "attack", baseDamage: 6, hitCount: 2, displayIcon: "⚡", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "斬撃", type: "attack", baseDamage: 8, displayIcon: "⚔️", priority: 0, energyCost: 1 } },
  ],
};

export const POISON_SPIDER: EnemyDefinition = {
  id: "depth1_poison_spider",
  imagePath: "/assets/images/enemies/depth1_poison_spider.png",
  name: "Poison Spider",
  nameJa: "毒蜘蛛",
  description: "人の頭ほどの大きさの紫色の蜘蛛",
  baseMaxHp: 28,
  baseMaxAp: 0,
  baseSpeed: 48,
  startingGuard: false,
  actEnergy: 1,
  displayWidth: 15,
  aiPatterns: [
    { phaseNumber: 0, action: { name: "毒牙", type: "debuff", baseDamage: 4, applyDebuffs: [{ name: "poison", stacks: 1, duration: 1, value: 3, isPermanent: false }], displayIcon: "🕷️", priority: 1, energyCost: 1 }, probability: 0.8 },
    { phaseNumber: 0, action: { name: "糸縛り", type: "debuff", baseDamage: 2, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "🕸️", priority: 2, energyCost: 1 }, probability: 0.2 },
  ],
};

export const FALLEN_GUARDIAN: EnemyDefinition = {
  id: "depth1_boss",
  imagePath: "/assets/images/enemies/depth1_boss.png",
  name: "Fallen Guardian",
  nameJa: "堕ちた番人",
  description: "かつて上層を守護していた重装の騎士",
  baseMaxHp: 120,
  baseMaxAp: 0,
  baseSpeed: 55,
  startingGuard: true,
  actEnergy: 1,
  displayWidth: 28,
  aiPatterns: [
    { phaseNumber: 1, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "重斬撃", type: "attack", baseDamage: 12, displayIcon: "🔨", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "防御固め", type: "buff", baseDamage: 0, guardGain: 20, displayIcon: "🛡️", priority: 1, energyCost: 1 } },
    { phaseNumber: 3, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "戦斧の一振り", type: "attack", baseDamage: 15, displayIcon: "🪓", priority: 2, energyCost: 1 } },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "重斬撃", type: "attack", baseDamage: 12, displayIcon: "🔨", priority: 0, energyCost: 1 } },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.34 && hp <= maxHp * 0.65, action: { name: "腐敗の斬撃", type: "debuff", baseDamage: 12, applyDebuffs: [{ name: "bleed", stacks: 1, duration: 2, value: 5, isPermanent: false }], displayIcon: "⚔️", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.33, action: { name: "狂乱の斬撃", type: "attack", baseDamage: 18, displayIcon: "💥", priority: 2, energyCost: 1 }, probability: 0.5 },
  ],
};

const SINGLE_PATTERNS: EncounterPattern[] = [
  { id: "d1_s_hound", nameJa: "腐敗の野犬", enemies: [CORRUPTED_HOUND] },
  { id: "d1_s_crow", nameJa: "変異した腐食鴉", enemies: [MUTATED_CROW] },
  { id: "d1_s_skeleton", nameJa: "徘徊する骨人", enemies: [BONE_WANDERER] },
  { id: "d1_s_shadow", nameJa: "這いずる影", enemies: [SHADOW_CRAWLER] },
];

const DOUBLE_PATTERNS: EncounterPattern[] = [
  { id: "d1_d_swords", nameJa: "錆びた剣士の組", enemies: [RUSTY_SWORDSMAN, RUSTY_SWORDSMAN] },
  { id: "d1_d_hound_crow", nameJa: "野犬と腐食鴉", enemies: [CORRUPTED_HOUND, MUTATED_CROW] },
  { id: "d1_d_shadow_spider", nameJa: "影と毒蜘蛛", enemies: [SHADOW_CRAWLER, POISON_SPIDER] },
  { id: "d1_d_bone_eater", nameJa: "骨人と腐肉喰", enemies: [BONE_WANDERER, FLESH_EATER] },
];

const THREE_PATTERNS: EncounterPattern[] = [
  { id: "d1_t_flesh", nameJa: "腐肉喰らいの群", enemies: [FLESH_EATER, FLESH_EATER, FLESH_EATER] },
  { id: "d1_t_spiders", nameJa: "毒蜘蛛の巣", enemies: [POISON_SPIDER, POISON_SPIDER, POISON_SPIDER] },
  { id: "d1_t_mixed", nameJa: "混成の群れ", enemies: [CORRUPTED_HOUND, FLESH_EATER, POISON_SPIDER] },
  { id: "d1_t_shadow_sp", nameJa: "影と蜘蛛の群", enemies: [SHADOW_CRAWLER, POISON_SPIDER, POISON_SPIDER] },
];

export const DEPTH1_ENEMIES: DepthEnemyData = {
  single: SINGLE_PATTERNS,
  double: DOUBLE_PATTERNS,
  three: THREE_PATTERNS,
  boss: FALLEN_GUARDIAN,
};

export const enemyList: EnemyDefinition[] = [
  CORRUPTED_HOUND,
  MUTATED_CROW,
  BONE_WANDERER,
  SHADOW_CRAWLER,
  FLESH_EATER,
  RUSTY_SWORDSMAN,
  POISON_SPIDER,
  FALLEN_GUARDIAN,
];
