import type { Card } from "../type/cardType";
/*
 * サンプルカードデータ（設計書準拠）
 * blueprint/card_system_complete.md より抜粋
 */

export const SAMPLE_CARDS: Card[] = [
  {
    id: "phy_001",
    name: "Slash",
    description: "Deal 8 physical damage",
    cost: 1,
    category: "physical",
    depthCurveType: "shallow", // 130% / 110% / 90% / 70% / 50%
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 8,
    tags: ["slash"],
    rarity: "common",
  },

  {
    id: "phy_002",
    name: "Thrust",
    description: "Deal 6 physical damage with 20% penetration",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral", // 100% all depths
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 6,
    tags: ["pierce", "penetration"],
    rarity: "common",
  },

  {
    id: "phy_003",
    name: "Heavy Strike",
    description: "Deal 10 physical damage. -1 energy recovery next turn",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 10,
    tags: ["heavy"],
    rarity: "common",
  },

  // ==========================================
  // 魔法攻撃カード
  // ==========================================
  {
    id: "mag_001",
    name: "Fireball",
    description: "Deal 10 magic damage (scales with depth)",
    cost: 2,
    category: "magic",
    depthCurveType: "deep", // 60% / 80% / 110% / 150% / 200%
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 10,
    tags: ["fire", "projectile"],
    rarity: "rare",
  },

  {
    id: "mag_002",
    name: "Ice Shard",
    description: "Deal 8 magic damage. Freeze enemy for 1 turn",
    cost: 2,
    category: "magic",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 8,
    tags: ["ice", "freeze"],
    rarity: "rare",
  },

  // ==========================================
  // 守りカード
  // ==========================================
  {
    id: "def_001",
    name: "Iron Wall",
    description: "Gain 9 shield",
    cost: 1,
    category: "defense",
    depthCurveType: "shallow",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 9,
    tags: ["shield"],
    rarity: "common",
  },

  {
    id: "def_002",
    name: "Magic Barrier",
    description: "Gain 12 shield",
    cost: 1,
    category: "defense",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 12,
    tags: ["shield", "magic"],
    rarity: "common",
  },

  // ==========================================
  // ヒールカード
  // ==========================================
  {
    id: "heal_001",
    name: "Minor Heal",
    description: "Restore 5 HP",
    cost: 1,
    category: "heal",
    depthCurveType: "shallow",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 5,
    tags: ["heal"],
    rarity: "common",
  },

  {
    id: "heal_002",
    name: "Regeneration",
    description: "Restore 8 HP. Gain regen buff for 3 turns (+3 HP/turn)",
    cost: 2,
    category: "heal",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 8,
    tags: ["heal", "buff"],
    rarity: "rare",
    applyPlayerBuff: [
      {
        type: "regeneration",
        stacks: 1,
        duration: 3,
        value: 3,
      },
    ],
  },

  // ==========================================
  // 火傷カード（Burn）
  // ==========================================
  {
    id: "mag_003",
    name: "Flame Strike",
    description: "Deal 6 magic damage. Apply 3 stacks of Burn for 3 turns",
    cost: 2,
    category: "magic",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 6,
    tags: ["fire", "burn"],
    rarity: "rare",
    applyEnemyDebuff: [
      {
        type: "burn",
        stacks: 3,
        duration: 3,
        value: 3, // Each stack deals 3 damage per turn
      },
    ],
  },

  {
    id: "mag_004",
    name: "Inferno",
    description: "Deal 4 magic damage. Apply 5 stacks of Burn for 2 turns",
    cost: 2,
    category: "magic",
    depthCurveType: "deep",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 4,
    tags: ["fire", "burn"],
    rarity: "epic",
    applyEnemyDebuff: [
      {
        type: "burn",
        stacks: 5,
        duration: 2,
        value: 3,
      },
    ],
  },

  // ==========================================
  // 出血カード（Bleed）
  // ==========================================
  {
    id: "phy_004",
    name: "Lacerate",
    description: "Deal 5 physical damage. Apply 4 stacks of Bleed for 3 turns",
    cost: 1,
    category: "physical",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 5,
    tags: ["slash", "bleed"],
    rarity: "rare",
    applyEnemyDebuff: [
      {
        type: "bleed",
        stacks: 4,
        duration: 3,
        value: 2, // Each stack deals 2 damage per turn
      },
    ],
  },

  {
    id: "phy_005",
    name: "Deep Cut",
    description: "Deal 3 physical damage. Apply 6 stacks of Bleed for 4 turns",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 3,
    tags: ["slash", "bleed"],
    rarity: "epic",
    applyEnemyDebuff: [
      {
        type: "bleed",
        stacks: 6,
        duration: 4,
        value: 2,
      },
    ],
  },

  // ==========================================
  // 毒カード（Poison）
  // ==========================================
  {
    id: "mag_005",
    name: "Poison Dart",
    description: "Deal 3 magic damage. Apply 5 stacks of Poison for 4 turns",
    cost: 1,
    category: "magic",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 3,
    tags: ["poison"],
    rarity: "rare",
    applyEnemyDebuff: [
      {
        type: "poison",
        stacks: 5,
        duration: 4,
        value: 2, // Each stack deals 2 damage per turn, ignores defense
      },
    ],
  },

  {
    id: "mag_006",
    name: "Toxic Cloud",
    description: "Deal 2 magic damage. Apply 8 stacks of Poison for 3 turns",
    cost: 2,
    category: "magic",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 2,
    tags: ["poison"],
    rarity: "epic",
    applyEnemyDebuff: [
      {
        type: "poison",
        stacks: 8,
        duration: 3,
        value: 2,
      },
    ],
  },

  // ==========================================
  // 凍結カード（Freeze）
  // ==========================================
  {
    id: "mag_007",
    name: "Frost Nova",
    description: "Deal 10 magic damage. Freeze enemy for 1 turn",
    cost: 3,
    category: "magic",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 10,
    tags: ["ice", "freeze"],
    rarity: "epic",
    applyEnemyDebuff: [
      {
        type: "freeze",
        stacks: 1,
        duration: 1,
        value: 0,
      },
    ],
  },

  // ==========================================
  // 弱体化カード（Weak）
  // ==========================================
  {
    id: "mag_008",
    name: "Enfeeble",
    description: "Deal 4 magic damage. Reduce enemy attack by 30% for 2 turns",
    cost: 1,
    category: "magic",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 4,
    tags: ["curse", "weak"],
    rarity: "rare",
    applyEnemyDebuff: [
      {
        type: "weak",
        stacks: 1,
        duration: 2,
        value: 30,
      },
    ],
  },

  // ==========================================
  // 攻撃力上昇カード（Attack Up）
  // ==========================================
  {
    id: "phy_006",
    name: "Battle Cry",
    description: "Gain 5 shield. Increase attack by 50% for 2 turns",
    cost: 2,
    category: "defense",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 5,
    tags: ["buff", "attack"],
    rarity: "rare",
    applyPlayerBuff: [
      {
        type: "atkUp",
        stacks: 1,
        duration: 2,
        value: 50,
      },
    ],
  },

  // ==========================================
  // シールド再生カード（Shield Regen）
  // ==========================================
  {
    id: "def_003",
    name: "Fortify",
    description: "Gain 8 shield. Regenerate 3 shield per turn for 3 turns",
    cost: 2,
    category: "defense",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 8,
    tags: ["shield", "buff"],
    rarity: "rare",
    applyPlayerBuff: [
      {
        type: "shieldRegen",
        stacks: 1,
        duration: 3,
        value: 3,
      },
    ],
  },

  // ==========================================
  // 防御力低下カード（Defense Down）
  // ==========================================
  {
    id: "phy_007",
    name: "Armor Break",
    description:
      "Deal 7 physical damage. Reduce enemy defense by 40% for 2 turns",
    cost: 2,
    category: "physical",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 7,
    tags: ["physical", "debuff"],
    rarity: "rare",
    applyEnemyDebuff: [
      {
        type: "defDown",
        stacks: 1,
        duration: 2,
        value: 40,
      },
    ],
  },
];

/**
 * カテゴリ別にカードを取得
 */
export function getCardsByCategory(category: Card["category"]): Card[] {
  return SAMPLE_CARDS.filter((card) => card.category === category);
}

/**
 * レアリティ別にカードを取得
 */
export function getCardsByRarity(rarity: Card["rarity"]): Card[] {
  return SAMPLE_CARDS.filter((card) => card.rarity === rarity);
}

/**
 * コスト別にカードを取得
 */
export function getCardsByCost(cost: number): Card[] {
  return SAMPLE_CARDS.filter((card) => card.cost === cost);
}

/**
 * IDでカードを取得
 */
export function getCardById(id: string): Card | undefined {
  return SAMPLE_CARDS.find((card) => card.id === id);
}

/**
 * ランダムにカードを取得
 */
export function getRandomCards(count: number): Card[] {
  const shuffled = [...SAMPLE_CARDS].sort(() => Math.random() - 0.5);
  return shuffled.slice(0, count);
}
