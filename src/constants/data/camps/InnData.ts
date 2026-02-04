/**
 * Inn Data
 *
 * Defines rest options, meal options, and rumors for the Inn facility.
 * Inn provides status bonuses and buffs for the next exploration.
 */

import type { RestOption, MealOption } from "@/types/campTypes";

// ============================================================
// Rest Options
// ============================================================

export const REST_OPTIONS: readonly RestOption[] = [
  {
    id: "free_rest",
    name: "Simple Rest",
    nameJa: "簡易休憩",
    description: "A quick rest in the common area. No special benefits.",
    descriptionJa: "共用スペースでの簡単な休憩。特別な効果はない。",
    cost: 0,
    effects: [],
    icon: "🪑",
  },
  {
    id: "standard_room",
    name: "Standard Room",
    nameJa: "スタンダードルーム",
    description: "A comfortable room with a proper bed. Grants bonus HP for the next exploration.",
    descriptionJa: "清潔なベッドのある快適な部屋。次の探索で最大HPが増加する。",
    cost: 50,
    effects: [
      { type: "bonusHp", value: 20 },
    ],
    icon: "🛏️",
  },
  {
    id: "deluxe_suite",
    name: "Deluxe Suite",
    nameJa: "デラックススイート",
    description: "The finest room in the inn. Grants significant HP bonus and extra starting energy.",
    descriptionJa: "宿屋最高級の部屋。最大HPが大幅に増加し、初期エネルギーも増加する。",
    cost: 150,
    effects: [
      { type: "bonusHp", value: 40 },
      { type: "energyBonus", value: 1 },
    ],
    icon: "👑",
  },
] as const;

// ============================================================
// Meal Options
// ============================================================

export const MEAL_OPTIONS: readonly MealOption[] = [
  {
    id: "hearty_stew",
    name: "Hearty Stew",
    nameJa: "ハーティシチュー",
    description: "A thick, filling stew that bolsters your defenses.",
    descriptionJa: "具だくさんのシチュー。防御力が上昇する。",
    cost: 40,
    effects: [
      { type: "buff", value: 1, buffType: "defUpMinor", duration: 3 },
    ],
    icon: "🍲",
    duration: 3,
  },
  {
    id: "grilled_meat",
    name: "Meat Platter",
    nameJa: "肉盛りプレート",
    description: "A generous serving of grilled meats. Increases attack power.",
    descriptionJa: "豪快な肉料理の盛り合わせ。攻撃力が上昇する。",
    cost: 40,
    effects: [
      { type: "buff", value: 1, buffType: "atkUpMinor", duration: 3 },
    ],
    icon: "🍖",
    duration: 3,
  },
  {
    id: "energy_drink",
    name: "Adventurer's Tonic",
    nameJa: "冒険者の特製ドリンク",
    description: "A secret recipe that quickens reflexes. Increases speed.",
    descriptionJa: "秘伝のレシピで作られた強壮剤。速度が上昇する。",
    cost: 60,
    effects: [
      { type: "buff", value: 1, buffType: "haste", duration: 2 },
    ],
    icon: "🧃",
    duration: 2,
  },
  {
    id: "full_course",
    name: "Full Course Dinner",
    nameJa: "フルコースディナー",
    description: "A luxurious multi-course meal. Grants HP bonus and regeneration.",
    descriptionJa: "豪華なコース料理。最大HPが増加し、リジェネ効果を得る。",
    cost: 200,
    effects: [
      { type: "bonusHp", value: 30 },
      { type: "buff", value: 1, buffType: "regeneration", duration: 3 },
    ],
    icon: "🍽️",
    duration: 3,
  },
  {
    id: "gold_boost_tea",
    name: "Fortune Tea",
    nameJa: "金運茶",
    description: "A mysterious tea said to bring good fortune. Increases gold acquisition.",
    descriptionJa: "金運を上げると噂のお茶。獲得ゴールドが増加する。",
    cost: 80,
    effects: [
      { type: "goldBonus", value: 15 },
    ],
    icon: "🍵",
    duration: 5,
  },
] as const;

// ============================================================
// Inn Rumors
// ============================================================

export const INN_RUMORS: readonly string[] = [
  "深層には強力な装備が眠っているらしい...",
  "炎属性の敵には氷属性が効果的だ",
  "ギルドで昇進すると新しいカードが手に入るぞ",
  "鍛冶屋で装備を強化すると探索が楽になる",
  "聖域の祝福は永続的な強化をもたらす",
  "魔石は交換所で換金できるが、強化にも使える",
  "ボス戦前には十分な準備をしておけ",
  "毒状態は毎ターンダメージを受けるから注意しろ",
  "ガードは一時的な防御だが、とても有効だ",
  "デッキは20枚前後が扱いやすいらしい",
  "エリートモンスターは強いが、報酬も良い",
  "撤退ルートを使えば安全に帰還できる",
  "スタン状態の敵は行動できない...好機だ",
  "連続攻撃カードは一度に大ダメージを与えられる",
  "回復カードは長期戦で重要になってくる",
  "図書館でカードの情報を確認できるぞ",
  "装備の耐久値が0になると壊れてしまう",
  "倉庫を活用すれば多くのアイテムを保管できる",
] as const;

// ============================================================
// Default Inn Buffs State
// ============================================================

export const DEFAULT_INN_BUFFS_STATE = {
  bonusHp: 0,
  bonusAp: 0,
  bonusEnergy: 0,
  startingBuffs: [],
  hpRegenPercent: 0,
  goldBonusPercent: 0,
  consumed: false,
} as const;
