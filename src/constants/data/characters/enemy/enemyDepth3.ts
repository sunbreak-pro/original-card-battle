import type { EnemyDefinition, EncounterPattern, DepthEnemyData } from "@/types/characterTypes";

export const BLIGHTED_TREANT: EnemyDefinition = {
  id: "depth3_treant",
  imagePath: "/assets/images/enemies/depth3_treant.png",
  name: "Blighted Treant",
  nameJa: "穢れた樹人",
  description: "腐食に侵された巨大な樹木の魔物",
  baseMaxHp: 120,
  baseMaxAp: 0,
  baseSpeed: 20,
  startingGuard: true,
  actEnergy: 1,
  displayWidth: 22,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "枝叩き", type: "attack", baseDamage: 15, displayIcon: "🌳", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "胞子散布", type: "debuff", baseDamage: 10, applyDebuffs: [{ name: "poison", stacks: 2, duration: 3, value: 5, isPermanent: false }], displayIcon: "🍄", priority: 1, energyCost: 1 } },
    { phaseNumber: 3, action: { name: "根絡め", type: "debuff", baseDamage: 12, applyDebuffs: [{ name: "slow", stacks: 1, duration: 2, value: 20, isPermanent: false }], displayIcon: "🌿", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "枝叩き", type: "attack", baseDamage: 15, displayIcon: "🌳", priority: 0, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, action: { name: "胞子散布", type: "debuff", baseDamage: 10, applyDebuffs: [{ name: "poison", stacks: 2, duration: 3, value: 5, isPermanent: false }], displayIcon: "🍄", priority: 1, energyCost: 1 }, probability: 0.5 },
  ],
};

export const DIRE_WOLF: EnemyDefinition = {
  id: "depth3_dire_wolf",
  imagePath: "/assets/images/enemies/depth3_dire_wolf.png",
  name: "Dire Wolf",
  nameJa: "凶暴な大狼",
  description: "闇に染まった巨大な灰色の狼",
  baseMaxHp: 85,
  baseMaxAp: 0,
  baseSpeed: 60,
  startingGuard: false,
  actEnergy: 1,
  displayWidth: 20,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "牙撃", type: "attack", baseDamage: 14, displayIcon: "🐺", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "引き裂き", type: "debuff", baseDamage: 12, applyDebuffs: [{ name: "bleed", stacks: 1, duration: 3, value: 5, isPermanent: false }], displayIcon: "🦷", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.5, action: { name: "牙撃", type: "attack", baseDamage: 14, displayIcon: "🐺", priority: 0, energyCost: 1 } },
    { phaseNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.5, action: { name: "狂乱の噛みつき", type: "attack", baseDamage: 18, displayIcon: "💢", priority: 1, energyCost: 1 } },
  ],
};

export const VENOMOUS_VINE: EnemyDefinition = {
  id: "depth3_venomous_vine",
  imagePath: "/assets/images/enemies/depth3_venomous_vine.png",
  name: "Venomous Vine",
  nameJa: "猛毒の蔦",
  description: "紫色の液体を滴らせる巨大な食虫植物",
  baseMaxHp: 70,
  baseMaxAp: 0,
  baseSpeed: 35,
  startingGuard: false,
  actEnergy: 1,
  displayWidth: 18,
  aiPatterns: [
    { phaseNumber: 0, action: { name: "毒蔦の鞭", type: "debuff", baseDamage: 12, applyDebuffs: [{ name: "poison", stacks: 1, duration: 3, value: 6, isPermanent: false }], displayIcon: "🌱", priority: 1, energyCost: 1 }, probability: 0.6 },
    { phaseNumber: 0, action: { name: "絞殺", type: "debuff", baseDamage: 15, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "🌿", priority: 2, energyCost: 1 }, probability: 0.4 },
  ],
};

export const CORRUPTED_STAG: EnemyDefinition = {
  id: "depth3_corrupted_stag",
  imagePath: "/assets/images/enemies/depth3_corrupted_stag.png",
  name: "Corrupted Stag",
  nameJa: "穢れし鹿",
  description: "闇の瘴気に蝕まれた巨大な角鹿",
  baseMaxHp: 95,
  baseMaxAp: 0,
  baseSpeed: 50,
  startingGuard: false,
  actEnergy: 1,
  displayWidth: 20,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "突進", type: "attack", baseDamage: 16, displayIcon: "🦌", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "角薙ぎ", type: "attack", baseDamage: 13, hitCount: 2, displayIcon: "⚡", priority: 0, energyCost: 1 } },
    { phaseNumber: 3, action: { name: "瘴気の息", type: "debuff", baseDamage: 10, applyDebuffs: [{ name: "atkDownMinor", stacks: 1, duration: 3, value: 20, isPermanent: false }], displayIcon: "🌫️", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "突進", type: "attack", baseDamage: 16, displayIcon: "🦌", priority: 0, energyCost: 1 }, probability: 0.6 },
    { phaseNumber: 0, action: { name: "瘴気の息", type: "debuff", baseDamage: 10, applyDebuffs: [{ name: "atkDownMinor", stacks: 1, duration: 3, value: 20, isPermanent: false }], displayIcon: "🌫️", priority: 1, energyCost: 1 }, probability: 0.4 },
  ],
};

export const FUNGAL_SPORE: EnemyDefinition = {
  id: "depth3_fungal_spore",
  imagePath: "/assets/images/enemies/depth3_fungal_spore.png",
  name: "Fungal Spore",
  nameJa: "菌糸胞子体",
  description: "有毒な胞子を撒き散らす浮遊する菌類",
  baseMaxHp: 40,
  baseMaxAp: 0,
  baseSpeed: 45,
  startingGuard: false,
  actEnergy: 1,
  displayWidth: 14,
  aiPatterns: [
    { phaseNumber: 0, action: { name: "胞子爆発", type: "debuff", baseDamage: 8, applyDebuffs: [{ name: "poison", stacks: 1, duration: 2, value: 5, isPermanent: false }], displayIcon: "🍄", priority: 1, energyCost: 1 }, probability: 0.7 },
    { phaseNumber: 0, action: { name: "麻痺粉", type: "debuff", baseDamage: 5, applyDebuffs: [{ name: "slow", stacks: 1, duration: 2, value: 15, isPermanent: false }], displayIcon: "💫", priority: 1, energyCost: 1 }, probability: 0.3 },
  ],
};

export const THORN_LIZARD: EnemyDefinition = {
  id: "depth3_thorn_lizard",
  imagePath: "/assets/images/enemies/depth3_thorn_lizard.png",
  name: "Thorn Lizard",
  nameJa: "棘蜥蜴",
  description: "全身を鋭い棘で覆われた大型の爬虫類",
  baseMaxHp: 60,
  baseMaxAp: 0,
  baseSpeed: 40,
  startingGuard: true,
  actEnergy: 1,
  displayWidth: 18,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "棘尾撃", type: "attack", baseDamage: 14, displayIcon: "🦎", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "棘飛ばし", type: "debuff", baseDamage: 10, applyDebuffs: [{ name: "bleed", stacks: 1, duration: 2, value: 4, isPermanent: false }], displayIcon: "🌵", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "棘尾撃", type: "attack", baseDamage: 14, displayIcon: "🦎", priority: 0, energyCost: 1 }, probability: 0.6 },
    { phaseNumber: 0, action: { name: "棘飛ばし", type: "debuff", baseDamage: 10, applyDebuffs: [{ name: "bleed", stacks: 1, duration: 2, value: 4, isPermanent: false }], displayIcon: "🌵", priority: 1, energyCost: 1 }, probability: 0.4 },
  ],
};

export const SWARM_HORNET: EnemyDefinition = {
  id: "depth3_swarm_hornet",
  imagePath: "/assets/images/enemies/depth3_swarm_hornet.png",
  name: "Swarm Hornet",
  nameJa: "群れ蜂",
  description: "猛毒の針を持つ巨大な蜂",
  baseMaxHp: 30,
  baseMaxAp: 0,
  baseSpeed: 75,
  startingGuard: false,
  actEnergy: 1,
  displayWidth: 14,
  aiPatterns: [
    { phaseNumber: 0, action: { name: "毒針", type: "debuff", baseDamage: 10, applyDebuffs: [{ name: "poison", stacks: 1, duration: 2, value: 4, isPermanent: false }], displayIcon: "🐝", priority: 1, energyCost: 1 }, probability: 0.6 },
    { phaseNumber: 0, action: { name: "連続刺し", type: "attack", baseDamage: 6, hitCount: 3, displayIcon: "⚡", priority: 0, energyCost: 1 }, probability: 0.4 },
  ],
};

export const ELDER_BASILISK: EnemyDefinition = {
  id: "depth3_boss",
  imagePath: "/assets/images/enemies/depth3_boss.png",
  name: "Elder Basilisk",
  nameJa: "太古のバジリスク",
  description: "穢れた森の最深部に棲む伝説の蛇竜、その眼は全てを石に変える",
  baseMaxHp: 300,
  baseMaxAp: 0,
  baseSpeed: 40,
  startingGuard: true,
  actEnergy: 1,
  displayWidth: 32,
  aiPatterns: [
    // Phase 1: HP > 66% - Physical dominance
    { phaseNumber: 1, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "牙撃", type: "attack", baseDamage: 18, displayIcon: "🐍", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "尾薙ぎ", type: "attack", baseDamage: 15, hitCount: 2, displayIcon: "🌀", priority: 0, energyCost: 1 } },
    { phaseNumber: 3, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "毒霧", type: "debuff", baseDamage: 12, applyDebuffs: [{ name: "poison", stacks: 2, duration: 3, value: 6, isPermanent: false }], displayIcon: "☁️", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "牙撃", type: "attack", baseDamage: 18, displayIcon: "🐍", priority: 0, energyCost: 1 } },
    // Phase 2: HP 34-66% - Petrification phase
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.34 && hp <= maxHp * 0.66, action: { name: "石化の視線", type: "debuff", baseDamage: 14, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "👁️", priority: 2, energyCost: 1 }, probability: 0.4 },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.34 && hp <= maxHp * 0.66, action: { name: "猛毒の噛みつき", type: "debuff", baseDamage: 16, applyDebuffs: [{ name: "poison", stacks: 1, duration: 3, value: 8, isPermanent: false }], displayIcon: "🦷", priority: 1, energyCost: 1 }, probability: 0.6 },
    // Phase 3: HP < 33% - Berserk
    { phaseNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.33, action: { name: "太古の咆哮", type: "debuff", baseDamage: 20, applyDebuffs: [{ name: "defDownMajor", stacks: 1, duration: 3, value: 30, isPermanent: false }], displayIcon: "🔥", priority: 2, energyCost: 1 }, probability: 0.4 },
    { phaseNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.33, action: { name: "絶滅の牙", type: "attack", baseDamage: 22, displayIcon: "💀", priority: 2, energyCost: 1 }, probability: 0.6 },
  ],
};

const SINGLE_PATTERNS: EncounterPattern[] = [
  { id: "d3_s_treant", nameJa: "穢れた樹人",     enemies: [BLIGHTED_TREANT] },
  { id: "d3_s_wolf",   nameJa: "凶暴な大狼",     enemies: [DIRE_WOLF] },
  { id: "d3_s_vine",   nameJa: "猛毒の蔦",       enemies: [VENOMOUS_VINE] },
  { id: "d3_s_stag",   nameJa: "穢れし鹿",       enemies: [CORRUPTED_STAG] },
];

const DOUBLE_PATTERNS: EncounterPattern[] = [
  { id: "d3_d_lizards",     nameJa: "棘蜥蜴の組",     enemies: [THORN_LIZARD, THORN_LIZARD] },
  { id: "d3_d_wolf_vine",   nameJa: "大狼と毒蔦",     enemies: [DIRE_WOLF, VENOMOUS_VINE] },
  { id: "d3_d_spore_lizard", nameJa: "胞子と棘蜥蜴",  enemies: [FUNGAL_SPORE, THORN_LIZARD] },
  { id: "d3_d_stag_hornet", nameJa: "鹿と群れ蜂",     enemies: [CORRUPTED_STAG, SWARM_HORNET] },
];

const THREE_PATTERNS: EncounterPattern[] = [
  { id: "d3_t_spores",  nameJa: "菌糸胞子の群",   enemies: [FUNGAL_SPORE, FUNGAL_SPORE, FUNGAL_SPORE] },
  { id: "d3_t_hornets", nameJa: "群れ蜂の巣",     enemies: [SWARM_HORNET, SWARM_HORNET, SWARM_HORNET] },
  { id: "d3_t_mixed",   nameJa: "森の混成群",     enemies: [FUNGAL_SPORE, THORN_LIZARD, SWARM_HORNET] },
  { id: "d3_t_hornet_sp", nameJa: "蜂と胞子の群", enemies: [SWARM_HORNET, SWARM_HORNET, FUNGAL_SPORE] },
];

export const DEPTH3_ENEMIES: DepthEnemyData = {
  single: SINGLE_PATTERNS,
  double: DOUBLE_PATTERNS,
  three: THREE_PATTERNS,
  boss: ELDER_BASILISK,
};

export const enemyList: EnemyDefinition[] = [
  BLIGHTED_TREANT,
  DIRE_WOLF,
  VENOMOUS_VINE,
  CORRUPTED_STAG,
  FUNGAL_SPORE,
  THORN_LIZARD,
  SWARM_HORNET,
  ELDER_BASILISK,
];
