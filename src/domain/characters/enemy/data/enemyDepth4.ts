import type { EnemyDefinition } from "@/types/characterTypes";

export const HELL_HOUND: EnemyDefinition = {
  id: "depth4_hell_hound",
  name: "Hell Hound",
  nameJa: "地獄の番犬",
  description: "灼熱の炎を纏う三つ首の魔犬",
  baseMaxHp: 120,
  baseMaxAp: 0,
  baseSpeed: 55,
  startingGuard: false,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "炎の咆哮", type: "debuff", baseDamage: 20, applyDebuffs: [{ name: "burn", stacks: 1, duration: 3, value: 7, isPermanent: false }], displayIcon: "🔥", priority: 1, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "三連噛みつき", type: "attack", baseDamage: 10, hitCount: 3, displayIcon: "🐕", priority: 0, energyCost: 1 } },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.5, action: { name: "炎の咆哮", type: "debuff", baseDamage: 20, applyDebuffs: [{ name: "burn", stacks: 1, duration: 3, value: 7, isPermanent: false }], displayIcon: "🔥", priority: 1, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.5, action: { name: "三連噛みつき", type: "attack", baseDamage: 10, hitCount: 3, displayIcon: "🐕", priority: 0, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.5, action: { name: "業火の牙", type: "attack", baseDamage: 28, displayIcon: "💢", priority: 2, energyCost: 1 } },
  ],
};

export const SHADOW_DEMON: EnemyDefinition = {
  id: "depth4_shadow_demon",
  name: "Shadow Demon",
  nameJa: "影の悪魔",
  description: "闇から実体化する不定形の悪魔",
  baseMaxHp: 100,
  baseMaxAp: 0,
  baseSpeed: 65,
  startingGuard: false,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "影の爪", type: "attack", baseDamage: 22, displayIcon: "🌑", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "暗黒の呪い", type: "debuff", baseDamage: 15, applyDebuffs: [{ name: "atkDownMinor", stacks: 2, duration: 3, value: 20, isPermanent: false }], displayIcon: "🌫️", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "影の爪", type: "attack", baseDamage: 22, displayIcon: "🌑", priority: 0, energyCost: 1 }, probability: 0.6 },
    { phaseNumber: 0, action: { name: "暗黒の呪い", type: "debuff", baseDamage: 15, applyDebuffs: [{ name: "atkDownMinor", stacks: 2, duration: 3, value: 20, isPermanent: false }], displayIcon: "🌫️", priority: 1, energyCost: 1 }, probability: 0.4 },
  ],
};

export const BLOOD_KNIGHT: EnemyDefinition = {
  id: "depth4_blood_knight",
  name: "Blood Knight",
  nameJa: "血の騎士",
  description: "血に染まった漆黒の鎧を纏う堕落した騎士",
  baseMaxHp: 140,
  baseMaxAp: 0,
  baseSpeed: 40,
  startingGuard: true,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "血刃斬", type: "debuff", baseDamage: 22, applyDebuffs: [{ name: "bleed", stacks: 1, duration: 3, value: 7, isPermanent: false }], displayIcon: "🗡️", priority: 1, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "血の障壁", type: "buff", baseDamage: 0, guardGain: 30, displayIcon: "🛡️", priority: 1, energyCost: 1 } },
    { phaseNumber: 3, action: { name: "断罪の一撃", type: "attack", baseDamage: 28, displayIcon: "⚔️", priority: 0, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "血刃斬", type: "debuff", baseDamage: 22, applyDebuffs: [{ name: "bleed", stacks: 1, duration: 3, value: 7, isPermanent: false }], displayIcon: "🗡️", priority: 1, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, action: { name: "断罪の一撃", type: "attack", baseDamage: 28, displayIcon: "⚔️", priority: 0, energyCost: 1 }, probability: 0.5 },
  ],
};

export const DARK_SORCERER: EnemyDefinition = {
  id: "depth4_dark_sorcerer",
  name: "Dark Sorcerer",
  nameJa: "闇の魔術師",
  description: "禁忌の魔術を操る魔界の術師",
  baseMaxHp: 110,
  baseMaxAp: 0,
  baseSpeed: 50,
  startingGuard: false,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "闇の弾丸", type: "attack", baseDamage: 20, displayIcon: "🔮", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "呪縛の鎖", type: "debuff", baseDamage: 15, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "⛓️", priority: 2, energyCost: 1 } },
    { phaseNumber: 3, action: { name: "腐食の魔弾", type: "debuff", baseDamage: 18, applyDebuffs: [{ name: "defDownMajor", stacks: 1, duration: 3, value: 30, isPermanent: false }], displayIcon: "💀", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "闇の弾丸", type: "attack", baseDamage: 20, displayIcon: "🔮", priority: 0, energyCost: 1 }, probability: 0.4 },
    { phaseNumber: 0, action: { name: "呪縛の鎖", type: "debuff", baseDamage: 15, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "⛓️", priority: 2, energyCost: 1 }, probability: 0.3 },
    { phaseNumber: 0, action: { name: "腐食の魔弾", type: "debuff", baseDamage: 18, applyDebuffs: [{ name: "defDownMajor", stacks: 1, duration: 3, value: 30, isPermanent: false }], displayIcon: "💀", priority: 1, energyCost: 1 }, probability: 0.3 },
  ],
};

export const IMP_TRICKSTER: EnemyDefinition = {
  id: "depth4_imp",
  name: "Imp Trickster",
  nameJa: "悪戯小悪魔",
  description: "素早く飛び回る小型の悪魔",
  baseMaxHp: 55,
  baseMaxAp: 0,
  baseSpeed: 75,
  startingGuard: false,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 0, action: { name: "悪魔の爪", type: "attack", baseDamage: 18, displayIcon: "😈", priority: 0, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, action: { name: "混乱の呪い", type: "debuff", baseDamage: 12, applyDebuffs: [{ name: "atkDownMinor", stacks: 1, duration: 2, value: 20, isPermanent: false }], displayIcon: "🎭", priority: 1, energyCost: 1 }, probability: 0.3 },
    { phaseNumber: 0, action: { name: "炎弾", type: "debuff", baseDamage: 15, applyDebuffs: [{ name: "burn", stacks: 1, duration: 2, value: 5, isPermanent: false }], displayIcon: "🔥", priority: 1, energyCost: 1 }, probability: 0.2 },
  ],
};

export const CURSED_GARGOYLE: EnemyDefinition = {
  id: "depth4_gargoyle",
  name: "Cursed Gargoyle",
  nameJa: "呪われたガーゴイル",
  description: "禁忌の呪いで動き出した石像の魔物",
  baseMaxHp: 80,
  baseMaxAp: 0,
  baseSpeed: 35,
  startingGuard: true,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 1, action: { name: "石拳", type: "attack", baseDamage: 24, displayIcon: "🗿", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, action: { name: "呪いの咆哮", type: "debuff", baseDamage: 18, applyDebuffs: [{ name: "defDownMajor", stacks: 1, duration: 2, value: 25, isPermanent: false }], displayIcon: "💀", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, action: { name: "石拳", type: "attack", baseDamage: 24, displayIcon: "🗿", priority: 0, energyCost: 1 }, probability: 0.6 },
    { phaseNumber: 0, action: { name: "呪いの咆哮", type: "debuff", baseDamage: 18, applyDebuffs: [{ name: "defDownMajor", stacks: 1, duration: 2, value: 25, isPermanent: false }], displayIcon: "💀", priority: 1, energyCost: 1 }, probability: 0.4 },
  ],
};

export const HELLFIRE_WISP: EnemyDefinition = {
  id: "depth4_wisp",
  name: "Hellfire Wisp",
  nameJa: "地獄の鬼火",
  description: "魔界の炎が凝縮した浮遊する火球",
  baseMaxHp: 45,
  baseMaxAp: 0,
  baseSpeed: 80,
  startingGuard: false,
  actEnergy: 1,
  aiPatterns: [
    { phaseNumber: 0, action: { name: "業火", type: "debuff", baseDamage: 18, applyDebuffs: [{ name: "burn", stacks: 1, duration: 3, value: 6, isPermanent: false }], displayIcon: "🔥", priority: 1, energyCost: 1 }, probability: 0.7 },
    { phaseNumber: 0, action: { name: "爆発", type: "attack", baseDamage: 25, displayIcon: "💥", priority: 0, energyCost: 1 }, probability: 0.3 },
  ],
};

export const DEMON_LORD_VARGATH: EnemyDefinition = {
  id: "depth4_boss",
  name: "Demon Lord Vargath",
  nameJa: "魔王ヴァルガス",
  description: "魔界を統べる強大な悪魔の王、その力は次元を歪ませる",
  baseMaxHp: 400,
  baseMaxAp: 0,
  baseSpeed: 50,
  startingGuard: true,
  actEnergy: 1,
  aiPatterns: [
    // Phase 1: HP > 66% - Dominant offense
    { phaseNumber: 1, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "魔王の剣", type: "attack", baseDamage: 25, displayIcon: "⚔️", priority: 0, energyCost: 1 } },
    { phaseNumber: 2, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "暗黒障壁", type: "buff", baseDamage: 0, guardGain: 35, displayIcon: "🛡️", priority: 1, energyCost: 1 } },
    { phaseNumber: 3, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "地獄の業火", type: "debuff", baseDamage: 22, applyDebuffs: [{ name: "burn", stacks: 2, duration: 3, value: 8, isPermanent: false }], displayIcon: "🔥", priority: 1, energyCost: 1 } },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.66, action: { name: "魔王の剣", type: "attack", baseDamage: 25, displayIcon: "⚔️", priority: 0, energyCost: 1 } },
    // Phase 2: HP 34-66% - Curse and weaken
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.34 && hp <= maxHp * 0.66, action: { name: "絶望の呪い", type: "debuff", baseDamage: 20, applyDebuffs: [{ name: "atkDownMinor", stacks: 2, duration: 3, value: 25, isPermanent: false }], displayIcon: "🌑", priority: 1, energyCost: 1 }, probability: 0.4 },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.34 && hp <= maxHp * 0.66, action: { name: "魔界の鎖", type: "debuff", baseDamage: 22, applyDebuffs: [{ name: "stun", stacks: 1, duration: 1, value: 0, isPermanent: false }], displayIcon: "⛓️", priority: 2, energyCost: 1 }, probability: 0.3 },
    { phaseNumber: 0, condition: (hp, maxHp) => hp > maxHp * 0.34 && hp <= maxHp * 0.66, action: { name: "血の収穫", type: "debuff", baseDamage: 24, applyDebuffs: [{ name: "bleed", stacks: 1, duration: 3, value: 8, isPermanent: false }], displayIcon: "🩸", priority: 1, energyCost: 1 }, probability: 0.3 },
    // Phase 3: HP < 33% - Apocalyptic
    { phaseNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.33, action: { name: "終焉の一撃", type: "attack", baseDamage: 30, displayIcon: "💥", priority: 2, energyCost: 1 }, probability: 0.5 },
    { phaseNumber: 0, condition: (hp, maxHp) => hp <= maxHp * 0.33, action: { name: "魔王の怒り", type: "attack", baseDamage: 18, hitCount: 3, displayIcon: "😈", priority: 2, energyCost: 1 }, probability: 0.5 },
  ],
};

export const DEPTH4_ENEMIES = {
  normal: [HELL_HOUND, SHADOW_DEMON, BLOOD_KNIGHT, DARK_SORCERER],
  groups: [
    { enemy: IMP_TRICKSTER, count: 3 },
    { enemy: CURSED_GARGOYLE, count: 2 },
    { enemy: HELLFIRE_WISP, count: 4 },
  ],
  boss: DEMON_LORD_VARGATH,
};

export const enemyList: EnemyDefinition[] = [
  HELL_HOUND,
  SHADOW_DEMON,
  BLOOD_KNIGHT,
  DARK_SORCERER,
  IMP_TRICKSTER,
  CURSED_GARGOYLE,
  HELLFIRE_WISP,
  DEMON_LORD_VARGATH,
];
