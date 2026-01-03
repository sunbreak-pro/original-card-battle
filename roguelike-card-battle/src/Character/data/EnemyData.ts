import type { BuffDebuffState } from "../../cards/type/baffType";

export type EnemyActionType = "attack" | "buff" | "debuff" | "special";

export interface EnemyAction {
  name: string;
  type: EnemyActionType;
  baseDamage: number;
  applyDebuffs?: BuffDebuffState[];
  applyBuffs?: BuffDebuffState[];
  guardGain?: number;
  hitCount?: number;
  displayIcon?: string;
  priority?: number;
  energyCost?: number;
}

export interface EnemyAIPattern {
  turnNumber: number;
  condition?: (hp: number, maxHp: number) => boolean;
  action: EnemyAction;
  probability?: number;
}

export interface Enemy {
  id: string;
  name: string;
  nameJa: string;
  description: string;
  maxHp: number;
  maxAp: number;
  speed: number;
  actEnergy: number;
  startingGuard: number;
  evasionRate: number;
  immunities: string[];
  aiPatterns: EnemyAIPattern[];
  imagePath?: string;
}

export const CORRUPTED_HOUND: Enemy = {
  id: "depth1_hound",
  name: "Corrupted Hound",
  nameJa: "腐敗の野犬",
  description: "腐肉が露出した痩せこけた黒い野犬",
  maxHp: 40,
  maxAp: 0,
  startingGuard: 0,
  evasionRate: 0,
  immunities: [],
  actEnergy: 1,
  speed: 40,
  aiPatterns: [
    { turnNumber: 1, action: { name: "噛みつき", type: "attack", baseDamage: 7, displayIcon: "⚔️", priority: 0, energyCost: 1 } },
    { turnNumber: 2, action: { name: "腐肉の牙", type: "debuff", baseDamage: 7, applyDebuffs: [{ name: "poison", stacks: 1, duration: 2, value: 3, isPermanent: false }], displayIcon: "🦷", priority: 1, energyCost: 1 } },
    { turnNumber: 0, action: { name: "噛みつき", type: "attack", baseDamage: 7, displayIcon: "⚔️", priority: 0, energyCost: 1 }, probability: 0.5 },
    { turnNumber: 0, action: { name: "腐肉の牙", type: "debuff", baseDamage: 7, applyDebuffs: [{ name: "poison", stacks: 1, duration: 2, value: 3, isPermanent: false }], displayIcon: "🦷", priority: 1, energyCost: 1 }, probability: 0.5 },
  ],
};

export const MUTATED_CROW: Enemy = {
  id: "depth1_crow",
  name: "Mutated Carrion Crow",
  nameJa: "変異した腐食鴉",
  description: "羽が抜け落ちた灰色の鴉",
  maxHp: 35,
  maxAp: 0,
  startingGuard: 0,
  evasionRate: 0,
  immunities: [],
  actEnergy: 1,
  speed: 55,
  aiPatterns: [
    { turnNumber: 0, action: { name: "連続啄み", type: "attack", baseDamage: 5, hitCount: 2, displayIcon: "🦅", priority: 0, energyCost: 1 }, probability: 0.8 },
    { turnNumber: 0, action: { name: "酸の唾液", type: "debuff", baseDamage: 3, applyDebuffs: [{ name: "defDownMajor", stacks: 1, duration: 3, value: 30, isPermanent: false }], displayIcon: "💧", priority: 1, energyCost: 1 }, probability: 0.2 },
  ],
};

export const BONE_WANDERER: Enemy = {
  id: "depth1_skeleton",
  name: "Bone Wanderer",
  nameJa: "徘徊する骨人",
  description: "白骨化した人型の骸骨",
  maxHp: 38,
  maxAp: 0,
  startingGuard: 0,
  evasionRate: 0,
  immunities: ["bleed"],
  actEnergy: 1,
  speed: 35,
  aiPatterns: [
    { turnNumber: 1, action: { name: "骨の剣", type: "attack", baseDamage: 6, displayIcon: "🗡️", priority: 0, energyCost: 1 } },
    { turnNumber: 2, action: { name: "骨の剣", type: "attack", baseDamage: 6, displayIcon: "🗡️", priority: 0, energyCost: 1 } },
    { turnNumber: 3, action: { name: "骨砕き", type: "debuff", baseDamage: 10, applyDebuffs: [{ name: "slow", stacks: 1, duration: 1, value: 10, isPermanent: false }], displayIcon: "💀", priority: 1, energyCost: 1 } },
    { turnNumber: 0, action: { name: "骨の剣", type: "attack", baseDamage: 6, displayIcon: "🗡️", priority: 0, energyCost: 1 } },
  ],
};

export const SHADOW_CRAWLER: Enemy = {
  id: "depth1_shadow",
  name: "Shadow Crawler",
  nameJa: "這いずる影",
  description: "黒い霧状の人型生物",
  maxHp: 25,
  maxAp: 0,
  startingGuard: 0,
  evasionRate: 0.15,
  immunities: [],
  actEnergy: 1,
  speed: 60,
  aiPatterns: [
    { turnNumber: 1, action: { name: "影の触手", type: "attack", baseDamage: 8, displayIcon: "🌑", priority: 0, energyCost: 1 } },
    { turnNumber: 2, action: { name: "闇の侵食", type: "debuff", baseDamage: 6, applyDebuffs: [{ name: "atkDownMinor", stacks: 1, duration: 3, value: 15, isPermanent: false }], displayIcon: "🌫️", priority: 1, energyCost: 1 } },
    { turnNumber: 0, action: { name: "影の触手", type: "attack", baseDamage: 8, displayIcon: "🌑", priority: 0, energyCost: 1 }, probability: 0.5 },
    { turnNumber: 0, action: { name: "闇の侵食", type: "debuff", baseDamage: 6, applyDebuffs: [{ name: "atkDownMinor", stacks: 1, duration: 3, value: 15, isPermanent: false }], displayIcon: "🌫️", priority: 1, energyCost: 1 }, probability: 0.5 },
  ],
};

export const FLESH_EATER: Enemy = {
  id: "depth1_flesh_eater",
  name: "Flesh Eater",
  nameJa: "腐肉喰らい",
  description: "腐敗した肉塊から無数の触手が生えた小型の生物",
  maxHp: 18,
  maxAp: 0,
  startingGuard: 0,
  evasionRate: 0,
  immunities: [],
  actEnergy: 1,
  speed: 45,
  aiPatterns: [
    { turnNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.5, action: { name: "触手攻撃", type: "attack", baseDamage: 5, displayIcon: "🦑", priority: 0, energyCost: 1 } },
    { turnNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.5, action: { name: "狂乱", type: "attack", baseDamage: 7, displayIcon: "💢", priority: 1, energyCost: 1 } },
  ],
};

export const RUSTY_SWORDSMAN: Enemy = {
  id: "depth1_rusty_swordsman",
  name: "Rusty Swordsman",
  nameJa: "錆びた剣士",
  description: "錆びた鎧を纏った亡霊騎士",
  maxHp: 30,
  maxAp: 0,
  startingGuard: 0,
  evasionRate: 0,
  immunities: [],
  actEnergy: 1,
  speed: 50,
  aiPatterns: [
    { turnNumber: 1, action: { name: "斬撃", type: "attack", baseDamage: 8, displayIcon: "⚔️", priority: 0, energyCost: 1 } },
    { turnNumber: 2, action: { name: "斬撃", type: "attack", baseDamage: 8, displayIcon: "⚔️", priority: 0, energyCost: 1 } },
    { turnNumber: 3, action: { name: "二段斬り", type: "attack", baseDamage: 6, hitCount: 2, displayIcon: "⚡", priority: 1, energyCost: 1 } },
    { turnNumber: 0, action: { name: "斬撃", type: "attack", baseDamage: 8, displayIcon: "⚔️", priority: 0, energyCost: 1 } },
  ],
};

export const POISON_SPIDER: Enemy = {
  id: "depth1_poison_spider",
  name: "Poison Spider",
  nameJa: "毒蜘蛛",
  description: "人の頭ほどの大きさの紫色の蜘蛛",
  maxHp: 28,
  maxAp: 0,
  startingGuard: 0,
  evasionRate: 0,
  immunities: [],
  actEnergy: 1,
  speed: 48,
  aiPatterns: [
    { turnNumber: 0, action: { name: "毒牙", type: "debuff", baseDamage: 4, applyDebuffs: [{ name: "poison", stacks: 1, duration: 1, value: 3, isPermanent: false }], displayIcon: "🕷️", priority: 1, energyCost: 1 }, probability: 0.8 },
    { turnNumber: 0, action: { name: "糸縛り", type: "debuff", baseDamage: 2, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "🕸️", priority: 2, energyCost: 1 }, probability: 0.2 },
  ],
};

export const FALLEN_GUARDIAN: Enemy = {
  id: "depth1_boss",
  name: "Fallen Guardian",
  nameJa: "堕ちた番人",
  description: "かつて上層を守護していた重装の騎士",
  maxHp: 120,
  maxAp: 0,
  startingGuard: 15,
  evasionRate: 0,
  immunities: [],
  actEnergy: 1,
  speed: 55,
  aiPatterns: [
    { turnNumber: 1, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "重斬撃", type: "attack", baseDamage: 12, displayIcon: "🔨", priority: 0, energyCost: 1 } },
    { turnNumber: 2, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "防御固め", type: "buff", baseDamage: 0, guardGain: 20, displayIcon: "🛡️", priority: 1, energyCost: 1 } },
    { turnNumber: 3, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "戦斧の一振り", type: "attack", baseDamage: 15, displayIcon: "🪓", priority: 2, energyCost: 1 } },
    { turnNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "重斬撃", type: "attack", baseDamage: 12, displayIcon: "🔨", priority: 0, energyCost: 1 } },
    { turnNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.34 && hp <= maxHp * 0.65, action: { name: "腐敗の斬撃", type: "debuff", baseDamage: 12, applyDebuffs: [{ name: "bleed", stacks: 1, duration: 2, value: 5, isPermanent: false }], displayIcon: "⚔️", priority: 1, energyCost: 1 } },
    { turnNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.33, action: { name: "狂乱の斬撃", type: "attack", baseDamage: 18, displayIcon: "💥", priority: 2, energyCost: 1 }, probability: 0.5 },
  ],
};

export const DEPTH1_ENEMIES = {
  normal: [CORRUPTED_HOUND, MUTATED_CROW, BONE_WANDERER, SHADOW_CRAWLER],
  groups: [
    { enemy: FLESH_EATER, count: 3 },
    { enemy: RUSTY_SWORDSMAN, count: 2 },
    { enemy: POISON_SPIDER, count: 4 },
  ],
  boss: FALLEN_GUARDIAN,
};
