import type { Card } from "../types/cardType";

/**
 * サンプルカードデータ（設計書準拠）
 * blueprint/card_system_complete.md より抜粋
 */

export const SAMPLE_CARDS: Card[] = [
  // ==========================================
  // 物理攻撃カード
  // ==========================================
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
    description: "Restore 8 HP. Gain regen buff for 3 turns",
    cost: 2,
    category: "heal",
    depthCurveType: "neutral",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    basePower: 8,
    tags: ["heal", "buff"],
    rarity: "rare",
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
