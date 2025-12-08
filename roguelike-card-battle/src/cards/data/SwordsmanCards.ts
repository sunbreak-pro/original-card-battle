/**
 * 剣士専用カード 40枚
 */

import type { Card } from "../type/cardType";

// ==========================================
// カテゴリ1: 剣気蓄積型（0-1コスト高速攻撃）12枚
// ==========================================

export const SWORDSMAN_CARDS: Record<string, Card> = {
  // 01. 迅雷斬（コスト0）
  sw_001: {
    id: "sw_001",
    name: "Swift Slash",
    description: "素早い一撃。剣気+1",
    cost: 0,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 5,
    swordEnergyGain: 1,
    tags: ["swordsman", "quick"],
    rarity: "common",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 02. 疾風斬（コスト0）
  sw_002: {
    id: "sw_002",
    name: "Gale Slash",
    description: "風のような一撃。剣気+1、次のカードコスト-1",
    cost: 0,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 3,
    swordEnergyGain: 1,
    nextCardCostReduction: 1,
    tags: ["swordsman", "quick", "combo"],
    rarity: "common",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 03. 連撃（コスト1）
  sw_003: {
    id: "sw_003",
    name: "Combo Strike",
    description: "3連続攻撃。剣気+1",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 6,
    hitCount: 3,
    swordEnergyGain: 1,
    tags: ["swordsman", "multi-hit"],
    rarity: "common",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 04. 三段突き（コスト1）
  sw_004: {
    id: "sw_004",
    name: "Triple Thrust",
    description: "貫通力のある3連突き。剣気+1、貫通20%",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 8,
    hitCount: 3,
    swordEnergyGain: 1,
    penetration: 0.2,
    tags: ["swordsman", "multi-hit", "pierce"],
    rarity: "common",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 05. 居合斬り（コスト1）
  sw_005: {
    id: "sw_005",
    name: "Iaido Slash",
    description: "先制の一撃。剣気+1",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 12,
    swordEnergyGain: 1,
    isPreemptive: true,
    tags: ["swordsman", "preemptive"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 06. 双剣乱舞（コスト1）
  sw_006: {
    id: "sw_006",
    name: "Twin Blade Dance",
    description: "6連続のランダム攻撃。剣気+1",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 4,
    hitCount: 6,
    swordEnergyGain: 1,
    tags: ["swordsman", "multi-hit", "random"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 07. 斬撃（コスト1）
  sw_007: {
    id: "sw_007",
    name: "Slash",
    description: "基本の斬撃。剣気+1",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 10,
    swordEnergyGain: 1,
    tags: ["swordsman", "basic"],
    rarity: "common",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 08. 袈裟斬り（コスト1）
  sw_008: {
    id: "sw_008",
    name: "Diagonal Slash",
    description: "出血を与える斬撃。剣気+1、出血1",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 11,
    swordEnergyGain: 1,
    applyEnemyDebuff: [{ type: "bleed", stacks: 1, duration: 3, value: 5 }],
    tags: ["swordsman", "bleed"],
    rarity: "common",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 09. 回転斬り（コスト1）
  sw_009: {
    id: "sw_009",
    name: "Spinning Slash",
    description: "全体攻撃。剣気+1",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 9,
    swordEnergyGain: 1,
    tags: ["swordsman", "aoe"],
    rarity: "common",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 10. 牙突（コスト1）
  sw_010: {
    id: "sw_010",
    name: "Fang Thrust",
    description: "高貫通の突き。剣気+1、貫通30%",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 13,
    swordEnergyGain: 1,
    penetration: 0.3,
    tags: ["swordsman", "pierce"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 11. 龍牙（コスト1）
  sw_011: {
    id: "sw_011",
    name: "Dragon Fang",
    description: "次の攻撃威力+50%。剣気+1",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 8,
    swordEnergyGain: 1,
    applyPlayerBuff: [{ type: "atkUp", stacks: 1, duration: 1, value: 50 }],
    tags: ["swordsman", "buff"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 12. 流水剣（コスト1）
  sw_012: {
    id: "sw_012",
    name: "Flowing Water Blade",
    description: "回避+30%。剣気+1",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 10,
    swordEnergyGain: 1,
    applyPlayerBuff: [{ type: "evasion", stacks: 1, duration: 1, value: 30 }],
    tags: ["swordsman", "evasion"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // ==========================================
  // カテゴリ2: 剣気蓄積専用カード 4枚
  // ==========================================

  // 13. 剣気集中（コスト1）
  sw_013: {
    id: "sw_013",
    name: "Sword Energy Focus",
    description: "剣気+4、手札+1",
    cost: 1,
    category: "swordEnergy",
    depthCurveType: "neutral",
    swordEnergyGain: 4,
    tags: ["swordsman", "energy", "draw"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 14. 瞑想（コスト1）
  sw_014: {
    id: "sw_014",
    name: "Meditation",
    description: "剣気+3、HP+10",
    cost: 1,
    category: "swordEnergy",
    depthCurveType: "neutral",
    swordEnergyGain: 3,
    healAmount: 10,
    tags: ["swordsman", "energy", "heal"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 15. 闘気解放（コスト2）
  sw_015: {
    id: "sw_015",
    name: "Fighting Spirit Release",
    description: "剣気+7、次ターン物理攻撃+30%",
    cost: 2,
    category: "swordEnergy",
    depthCurveType: "neutral",
    swordEnergyGain: 7,
    applyPlayerBuff: [{ type: "physicalUp", stacks: 1, duration: 2, value: 30 }],
    tags: ["swordsman", "energy", "buff"],
    rarity: "epic",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 16. 気合（コスト0）
  sw_016: {
    id: "sw_016",
    name: "Fighting Spirit",
    description: "剣気+2、エナジー+1",
    cost: 0,
    category: "swordEnergy",
    depthCurveType: "neutral",
    swordEnergyGain: 2,
    tags: ["swordsman", "energy", "resource"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // ==========================================
  // カテゴリ3: 中火力（2コスト）10枚
  // ==========================================

  // 17. 断空斬（コスト2）
  sw_017: {
    id: "sw_017",
    name: "Sky Cleaver",
    description: "貫通30%の強撃。剣気+2",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 25,
    swordEnergyGain: 2,
    penetration: 0.3,
    tags: ["swordsman", "pierce"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 18. 鋼鉄斬（コスト2）
  sw_018: {
    id: "sw_018",
    name: "Steel Slash",
    description: "攻防一体。剣気+2、Guard+15",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 22,
    swordEnergyGain: 2,
    guardAmount: 15,
    tags: ["swordsman", "defense"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 19. 旋風剣（コスト2）
  sw_019: {
    id: "sw_019",
    name: "Whirlwind Blade",
    description: "全体攻撃。剣気+2",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 20,
    swordEnergyGain: 2,
    tags: ["swordsman", "aoe"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 20. 閃光剣（コスト2）
  sw_020: {
    id: "sw_020",
    name: "Flash Blade",
    description: "先制攻撃。剣気+3",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 24,
    swordEnergyGain: 3,
    isPreemptive: true,
    tags: ["swordsman", "preemptive"],
    rarity: "epic",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 21. 爆砕剣（コスト2）
  sw_021: {
    id: "sw_021",
    name: "Crushing Blade",
    description: "敵Guard-20。剣気+2",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 28,
    swordEnergyGain: 2,
    applyEnemyDebuff: [{ type: "defDown", stacks: 1, duration: 2, value: 20 }],
    tags: ["swordsman", "guard-break"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 22. 影斬り（コスト2）
  sw_022: {
    id: "sw_022",
    name: "Shadow Slash",
    description: "回避+40%。剣気+2",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 23,
    swordEnergyGain: 2,
    applyPlayerBuff: [{ type: "evasion", stacks: 1, duration: 1, value: 40 }],
    tags: ["swordsman", "evasion"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 23. 裂空斬（コスト2）
  sw_023: {
    id: "sw_023",
    name: "Sky Rend",
    description: "出血2スタック。剣気+3",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 26,
    swordEnergyGain: 3,
    applyEnemyDebuff: [{ type: "bleed", stacks: 2, duration: 3, value: 5 }],
    tags: ["swordsman", "bleed"],
    rarity: "epic",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 24. 烈火剣（コスト2）
  sw_024: {
    id: "sw_024",
    name: "Blazing Blade",
    description: "火傷1スタック。剣気+2",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 27,
    swordEnergyGain: 2,
    applyEnemyDebuff: [{ type: "burn", stacks: 1, duration: 2, value: 3 }],
    tags: ["swordsman", "burn"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 25. 大地斬（コスト2）
  sw_025: {
    id: "sw_025",
    name: "Earth Cleaver",
    description: "よろめき2ターン。剣気+2",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 30,
    swordEnergyGain: 2,
    applyEnemyDebuff: [{ type: "stagger", stacks: 1, duration: 2, value: 50 }],
    tags: ["swordsman", "stagger"],
    rarity: "epic",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 26. 獅子奮迅（コスト2）
  sw_026: {
    id: "sw_026",
    name: "Lion's Fury",
    description: "2連撃、各攻撃に剣気ダメージ。剣気+3",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 15,
    hitCount: 2,
    swordEnergyGain: 3,
    tags: ["swordsman", "multi-hit"],
    rarity: "epic",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // ==========================================
  // カテゴリ4: 剣気消費技（中火力）6枚
  // ==========================================

  // 27. 剣気解放（コスト1）
  sw_027: {
    id: "sw_027",
    name: "Sword Energy Release",
    description: "剣気3消費。威力15+(消費×10)",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 15,
    swordEnergyConsume: 3,
    swordEnergyMultiplier: 10,
    tags: ["swordsman", "consume"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 28. 剣気刃（コスト1）
  sw_028: {
    id: "sw_028",
    name: "Sword Energy Blade",
    description: "剣気3消費、貫通40%。威力12+(消費×8)",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 12,
    swordEnergyConsume: 3,
    swordEnergyMultiplier: 8,
    penetration: 0.4,
    tags: ["swordsman", "consume", "pierce"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 29. 剣気連斬（コスト2）
  sw_029: {
    id: "sw_029",
    name: "Sword Energy Chain",
    description: "剣気5消費、3連撃。威力10+(消費×6)×3",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 10,
    hitCount: 3,
    swordEnergyConsume: 5,
    swordEnergyMultiplier: 6,
    tags: ["swordsman", "consume", "multi-hit"],
    rarity: "epic",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 30. 剣気爆発（コスト2）
  sw_030: {
    id: "sw_030",
    name: "Sword Energy Burst",
    description: "剣気5消費、全体攻撃。威力20+(消費×12)",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 20,
    swordEnergyConsume: 5,
    swordEnergyMultiplier: 12,
    tags: ["swordsman", "consume", "aoe"],
    rarity: "epic",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 31. 剣気貫通（コスト1）
  sw_031: {
    id: "sw_031",
    name: "Sword Energy Pierce",
    description: "剣気3消費、貫通50%。威力18+(消費×9)",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 18,
    swordEnergyConsume: 3,
    swordEnergyMultiplier: 9,
    penetration: 0.5,
    tags: ["swordsman", "consume", "pierce"],
    rarity: "epic",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 32. 剣気衝撃波（コスト2）
  sw_032: {
    id: "sw_032",
    name: "Sword Energy Shockwave",
    description: "剣気3消費、よろめき2ターン。威力16+(消費×10)",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 16,
    swordEnergyConsume: 3,
    swordEnergyMultiplier: 10,
    applyEnemyDebuff: [{ type: "stagger", stacks: 1, duration: 2, value: 50 }],
    tags: ["swordsman", "consume", "stagger"],
    rarity: "epic",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // ==========================================
  // カテゴリ5: 剣気全消費技（超火力）4枚
  // ==========================================

  // 33. 剣神降臨（コスト3）
  sw_033: {
    id: "sw_033",
    name: "Sword God Descent",
    description: "剣気全消費、全体攻撃、出血3。威力30+(消費×20)",
    cost: 3,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 30,
    swordEnergyConsume: 0, // 0 = 全消費
    swordEnergyMultiplier: 20,
    applyEnemyDebuff: [{ type: "bleed", stacks: 3, duration: 3, value: 5 }],
    tags: ["swordsman", "ultimate", "aoe", "bleed"],
    rarity: "legend",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 34. 究極奥義（コスト4）
  sw_034: {
    id: "sw_034",
    name: "Ultimate Secret Art",
    description: "剣気全消費、貫通60%、出血3。威力40+(消費×25)",
    cost: 4,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 40,
    swordEnergyConsume: 0,
    swordEnergyMultiplier: 25,
    penetration: 0.6,
    applyEnemyDebuff: [{ type: "bleed", stacks: 3, duration: 3, value: 5 }],
    tags: ["swordsman", "ultimate", "pierce", "bleed"],
    rarity: "legend",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 35. 剣気天昇（コスト3）
  sw_035: {
    id: "sw_035",
    name: "Sword Energy Ascension",
    description: "剣気全消費、出血3、次ターン剣気+5。威力35+(消費×22)",
    cost: 3,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 35,
    swordEnergyConsume: 0,
    swordEnergyMultiplier: 22,
    applyEnemyDebuff: [{ type: "bleed", stacks: 3, duration: 3, value: 5 }],
    tags: ["swordsman", "ultimate", "bleed", "recovery"],
    rarity: "legend",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 36. 剣気殲滅（コスト3）
  sw_036: {
    id: "sw_036",
    name: "Sword Energy Annihilation",
    description: "剣気全消費、3連撃、出血3。威力25+(消費×18)×3",
    cost: 3,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 25,
    hitCount: 3,
    swordEnergyConsume: 0,
    swordEnergyMultiplier: 18,
    applyEnemyDebuff: [{ type: "bleed", stacks: 3, duration: 3, value: 5 }],
    tags: ["swordsman", "ultimate", "multi-hit", "bleed"],
    rarity: "legend",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // ==========================================
  // カテゴリ6: 防御・補助系 4枚
  // ==========================================

  // 37. 剣気結界（コスト1）
  sw_037: {
    id: "sw_037",
    name: "Sword Energy Barrier",
    description: "Guard = 剣気×8（剣気消費なし）",
    cost: 1,
    category: "defense",
    depthCurveType: "neutral",
    tags: ["swordsman", "defense", "barrier"],
    rarity: "epic",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 38. 反撃の構え（コスト1）
  sw_038: {
    id: "sw_038",
    name: "Counter Stance",
    description: "Guard+15、剣気+2、被ダメージ時剣気+3",
    cost: 1,
    category: "defense",
    depthCurveType: "neutral",
    guardAmount: 15,
    swordEnergyGain: 2,
    applyPlayerBuff: [{ type: "thorns", stacks: 1, duration: 1, value: 3 }],
    tags: ["swordsman", "defense", "counter"],
    rarity: "rare",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 39. 不屈の闘志（コスト2）
  sw_039: {
    id: "sw_039",
    name: "Indomitable Spirit",
    description: "HP+20、デバフ全解除、Guard = 剣気×2",
    cost: 2,
    category: "defense",
    depthCurveType: "neutral",
    healAmount: 20,
    tags: ["swordsman", "defense", "heal", "cleanse"],
    rarity: "epic",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },

  // 40. 剣気吸収（コスト2）
  sw_040: {
    id: "sw_040",
    name: "Sword Energy Drain",
    description: "HP吸収50%、Guard = 剣気×3。剣気+3",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    baseDamage: 18,
    swordEnergyGain: 3,
    applyPlayerBuff: [{ type: "lifesteal", stacks: 1, duration: 1, value: 50 }],
    tags: ["swordsman", "lifesteal", "defense"],
    rarity: "epic",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    characterClass: "swordsman",
  },
};

// カード配列としてエクスポート
export const SWORDSMAN_CARDS_ARRAY: Card[] = Object.values(SWORDSMAN_CARDS);

// IDでカードを取得
export function getSwordsmanCard(id: string): Card | undefined {
  return SWORDSMAN_CARDS[id];
}
