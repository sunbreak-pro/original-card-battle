import type { EnemyDefinition, EncounterPattern, DepthEnemyData } from "@/types/characterTypes";

export const IRON_AUTOMATON: EnemyDefinition = {
  id: "depth2_iron_automaton",
  name: "Iron Automaton",
  nameJa: "鉄の自動人形",
  description: "古代遺跡で稼働し続ける錆びた機械兵士",
  baseMaxHp: 65,
  baseMaxAp: 0,
  baseSpeed: 30,
  startingGuard: true,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "鉄拳", type: "attack", baseDamage: 10, displayIcon: "🤖", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "蒸気噴射", type: "debuff", baseDamage: 8, applyDebuffs: [{ name: "burn", stacks: 1, duration: 2, value: 4, isPermanent: false }], displayIcon: "💨", priority: 1, energyCost: 1 } },
    { phaseNumber: 3, action: { name: "重鉄拳", type: "attack", baseDamage: 14, displayIcon: "🔨", priority: 0, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "鉄拳", type: "attack", baseDamage: 10, displayIcon: "🤖", priority: 0, energyCost: 1 }, probability: 0.6 },
    { phaseNumber: 0, action: { name: "蒸気噴射", type: "debuff", baseDamage: 8, applyDebuffs: [{ name: "burn", stacks: 1, duration: 2, value: 4, isPermanent: false }], displayIcon: "💨", priority: 1, energyCost: 1 }, probability: 0.4 },
  ],
};

export const GHOUL: EnemyDefinition = {
  id: "depth2_ghoul",
  name: "Ghoul",
  nameJa: "食屍鬼",
  description: "地下墓地を彷徨う腐敗した亡者",
  baseMaxHp: 55,
  baseMaxAp: 0,
  baseSpeed: 40,
  startingGuard: false,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "腐爪", type: "debuff", baseDamage: 9, applyDebuffs: [{ name: "poison", stacks: 1, duration: 3, value: 4, isPermanent: false }], displayIcon: "🦴", priority: 1, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "噛みつき", type: "attack", baseDamage: 11, displayIcon: "🦷", priority: 0, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "腐爪", type: "debuff", baseDamage: 9, applyDebuffs: [{ name: "poison", stacks: 1, duration: 3, value: 4, isPermanent: false }], displayIcon: "🦴", priority: 1, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, action: { name: "噛みつき", type: "attack", baseDamage: 11, displayIcon: "🦷", priority: 0, energyCost: 1 }, probability: 0.5 },
  ],
};

export const CAVE_BAT_SWARM: EnemyDefinition = {
  id: "depth2_cave_bat",
  name: "Cave Bat Swarm",
  nameJa: "洞窟蝙蝠の群れ",
  description: "暗闇から襲いかかる大量の蝙蝠",
  baseMaxHp: 50,
  baseMaxAp: 0,
  baseSpeed: 65,
  startingGuard: false,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 0, action: { name: "群れの襲撃", type: "attack", baseDamage: 5, hitCount: 3, displayIcon: "🦇", priority: 0, energyCost: 1 }, probability: 0.7 },
    { phaseNumber: 0, action: { name: "超音波", type: "debuff", baseDamage: 4, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "🔊", priority: 1, energyCost: 1 }, probability: 0.3 },
  ],
};

export const RUNIC_GOLEM: EnemyDefinition = {
  id: "depth2_runic_golem",
  name: "Runic Golem",
  nameJa: "ルーンの石像兵",
  description: "古代文字が刻まれた巨大な石の巨人",
  baseMaxHp: 80,
  baseMaxAp: 0,
  baseSpeed: 20,
  startingGuard: true,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "岩砕き", type: "attack", baseDamage: 15, displayIcon: "🪨", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "ルーン防壁", type: "buff", baseDamage: 0, guardGain: 15, displayIcon: "🛡️", priority: 1, energyCost: 1 } },
    { phaseNumber: 3, action: { name: "地震", type: "debuff", baseDamage: 12, applyDebuffs: [{ name: "slow", stacks: 1, duration: 2, value: 15, isPermanent: false }], displayIcon: "🌍", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "岩砕き", type: "attack", baseDamage: 15, displayIcon: "🪨", priority: 0, energyCost: 1 }, probability: 0.6 },
    { phaseNumber: 0, action: { name: "ルーン防壁", type: "buff", baseDamage: 0, guardGain: 15, displayIcon: "🛡️", priority: 1, energyCost: 1 }, probability: 0.4 },
  ],
};

export const SKELETON_ARCHER: EnemyDefinition = {
  id: "depth2_skeleton_archer",
  name: "Skeleton Archer",
  nameJa: "骸骨射手",
  description: "朽ちた弓を構えるアンデッドの弓兵",
  baseMaxHp: 30,
  baseMaxAp: 0,
  baseSpeed: 50,
  startingGuard: false,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 0, action: { name: "骨矢", type: "attack", baseDamage: 8, displayIcon: "🏹", priority: 0, energyCost: 1 }, probability: 0.7 },
    { phaseNumber: 0, action: { name: "毒矢", type: "debuff", baseDamage: 6, applyDebuffs: [{ name: "poison", stacks: 1, duration: 2, value: 3, isPermanent: false }], displayIcon: "☠️", priority: 1, energyCost: 1 }, probability: 0.3 },
  ],
};

export const CLOCKWORK_SPIDER: EnemyDefinition = {
  id: "depth2_clockwork_spider",
  name: "Clockwork Spider",
  nameJa: "機械蜘蛛",
  description: "歯車で動く金属製の蜘蛛型機械",
  baseMaxHp: 45,
  baseMaxAp: 0,
  baseSpeed: 55,
  startingGuard: false,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "鋼糸", type: "debuff", baseDamage: 8, applyDebuffs: [{ name: "slow", stacks: 1, duration: 2, value: 10, isPermanent: false }], displayIcon: "🕸️", priority: 1, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "電撃噛みつき", type: "debuff", baseDamage: 10, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "⚡", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "鋼糸", type: "debuff", baseDamage: 8, applyDebuffs: [{ name: "slow", stacks: 1, duration: 2, value: 10, isPermanent: false }], displayIcon: "🕸️", priority: 1, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, action: { name: "電撃噛みつき", type: "debuff", baseDamage: 10, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "⚡", priority: 1, energyCost: 1 }, probability: 0.5 },
  ],
};

export const GHOST_WISP: EnemyDefinition = {
  id: "depth2_ghost_wisp",
  name: "Ghost Wisp",
  nameJa: "幽霊灯火",
  description: "青白く揺らめく不気味な霊体の炎",
  baseMaxHp: 22,
  baseMaxAp: 0,
  baseSpeed: 70,
  startingGuard: false,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 0, action: { name: "霊火", type: "attack", baseDamage: 9, displayIcon: "👻", priority: 0, energyCost: 1 }, probability: 0.6 },
    { phaseNumber: 0, action: { name: "呪縛の光", type: "debuff", baseDamage: 5, applyDebuffs: [{ name: "atkDownMinor", stacks: 1, duration: 2, value: 15, isPermanent: false }], displayIcon: "🔮", priority: 1, energyCost: 1 }, probability: 0.4 },
  ],
};

export const IRON_REVENANT: EnemyDefinition = {
  id: "depth2_boss",
  name: "Iron Revenant",
  nameJa: "鉄の亡霊",
  description: "古代の鎧に宿りし強大な亡霊、遺跡の最深部を守護する",
  baseMaxHp: 200,
  baseMaxAp: 0,
  baseSpeed: 45,
  startingGuard: true,
  actEnergy: 1,
  aiPatterns: [
    // Phase 1: HP > 66% - Heavy physical attacks
    { phaseNumber: 1, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "鉄槌", type: "attack", baseDamage: 12, displayIcon: "🔨", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "鋼鉄の守り", type: "buff", baseDamage: 0, guardGain: 25, displayIcon: "🛡️", priority: 1, energyCost: 1 } },
    { phaseNumber: 3, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "粉砕撃", type: "attack", baseDamage: 15, displayIcon: "💥", priority: 0, energyCost: 1 } },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "鉄槌", type: "attack", baseDamage: 12, displayIcon: "🔨", priority: 0, energyCost: 1 } },
    // Phase 2: HP 34-66% - Debuff-heavy
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.34 && hp <= maxHp * 0.66, action: { name: "呪われし鉄鎖", type: "debuff", baseDamage: 10, applyDebuffs: [{ name: "slow", stacks: 1, duration: 2, value: 15, isPermanent: false }], displayIcon: "⛓️", priority: 1, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.34 && hp <= maxHp * 0.66, action: { name: "亡霊の嘆き", type: "debuff", baseDamage: 12, applyDebuffs: [{ name: "defDownMajor", stacks: 1, duration: 3, value: 25, isPermanent: false }], displayIcon: "💀", priority: 1, energyCost: 1 }, probability: 0.5 },
    // Phase 3: HP < 33% - Enraged
    { phaseNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.33, action: { name: "鉄の暴走", type: "attack", baseDamage: 15, hitCount: 2, displayIcon: "🌀", priority: 2, energyCost: 1 }, probability: 0.6 },
    { phaseNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.33, action: { name: "崩壊の一撃", type: "debuff", baseDamage: 14, applyDebuffs: [{ name: "bleed", stacks: 1, duration: 3, value: 6, isPermanent: false }], displayIcon: "🩸", priority: 2, energyCost: 1 }, probability: 0.4 },
  ],
};

const SINGLE_PATTERNS: EncounterPattern[] = [
  { id: "d2_s_automaton", nameJa: "鉄の自動人形",     enemies: [IRON_AUTOMATON] },
  { id: "d2_s_ghoul",     nameJa: "食屍鬼",           enemies: [GHOUL] },
  { id: "d2_s_bat",       nameJa: "洞窟蝙蝠の群れ",   enemies: [CAVE_BAT_SWARM] },
  { id: "d2_s_golem",     nameJa: "ルーンの石像兵",   enemies: [RUNIC_GOLEM] },
];

const DOUBLE_PATTERNS: EncounterPattern[] = [
  { id: "d2_d_spiders",       nameJa: "機械蜘蛛の組",     enemies: [CLOCKWORK_SPIDER, CLOCKWORK_SPIDER] },
  { id: "d2_d_ghoul_bat",     nameJa: "食屍鬼と蝙蝠",     enemies: [GHOUL, CAVE_BAT_SWARM] },
  { id: "d2_d_automaton_wisp", nameJa: "自動人形と幽霊灯", enemies: [IRON_AUTOMATON, GHOST_WISP] },
  { id: "d2_d_archer_spider", nameJa: "射手と機械蜘蛛",   enemies: [SKELETON_ARCHER, CLOCKWORK_SPIDER] },
];

const THREE_PATTERNS: EncounterPattern[] = [
  { id: "d2_t_archers",  nameJa: "骸骨射手の隊",     enemies: [SKELETON_ARCHER, SKELETON_ARCHER, SKELETON_ARCHER] },
  { id: "d2_t_wisps",    nameJa: "幽霊灯火の群",     enemies: [GHOST_WISP, GHOST_WISP, GHOST_WISP] },
  { id: "d2_t_mixed",    nameJa: "遺跡の混成群",     enemies: [SKELETON_ARCHER, CLOCKWORK_SPIDER, GHOST_WISP] },
  { id: "d2_t_wisp_sp",  nameJa: "幽霊と機械の群",   enemies: [GHOST_WISP, GHOST_WISP, CLOCKWORK_SPIDER] },
];

export const DEPTH2_ENEMIES: DepthEnemyData = {
  single: SINGLE_PATTERNS,
  double: DOUBLE_PATTERNS,
  three: THREE_PATTERNS,
  boss: IRON_REVENANT,
};

export const enemyList: EnemyDefinition[] = [
  IRON_AUTOMATON,
  GHOUL,
  CAVE_BAT_SWARM,
  RUNIC_GOLEM,
  SKELETON_ARCHER,
  CLOCKWORK_SPIDER,
  GHOST_WISP,
  IRON_REVENANT,
];
