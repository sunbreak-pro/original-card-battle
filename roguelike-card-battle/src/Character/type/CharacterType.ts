/**
 * キャラクタータイプ定義
 */

export type CharacterClass = "swordsman" | "mage" | "summoner";

export interface CharacterStats {
  hp: number;
  maxHp: number;
  ap: number;
  maxAp: number;
  atkBonus: number;      // 攻撃力ボーナス（%）
  defBonus: number;      // 防御力ボーナス（%）
  magicBonus: number;    // 魔法補正（%）
  initialEnergy: number; // 初期エナジー
}

export interface Character {
  id: string;
  name: string;
  nameJa: string;
  class: CharacterClass;
  stats: CharacterStats;
  title: string;         // 称号
  cardCount: number;     // 獲得カード枚数
}

/**
 * 剣士の基本ステータス
 */
export const SWORDSMAN_BASE_STATS: CharacterStats = {
  hp: 100,
  maxHp: 100,
  ap: 30,
  maxAp: 30,
  atkBonus: 30,      // +30%
  defBonus: 10,      // +10%
  magicBonus: 0,     // +0%
  initialEnergy: 3,
};

/**
 * 魔術士の基本ステータス
 */
export const MAGE_BASE_STATS: CharacterStats = {
  hp: 80,
  maxHp: 80,
  ap: 20,
  maxAp: 20,
  atkBonus: 0,       // +0%
  defBonus: 5,       // +5%
  magicBonus: 40,    // +40%
  initialEnergy: 4,
};

/**
 * 召喚士の基本ステータス
 */
export const SUMMONER_BASE_STATS: CharacterStats = {
  hp: 90,
  maxHp: 90,
  ap: 25,
  maxAp: 25,
  atkBonus: 10,      // +10%
  defBonus: 15,      // +15%
  magicBonus: 20,    // +20%
  initialEnergy: 4,
};

/**
 * 剣士の称号取得
 */
export function getSwordsmanTitle(cardCount: number): string {
  if (cardCount >= 50) return "剣神";
  if (cardCount >= 30) return "剣聖";
  if (cardCount >= 15) return "剣豪";
  if (cardCount >= 5) return "剣士";
  return "見習い剣士";
}

/**
 * 魔術士の称号取得
 */
export function getMageTitle(cardCount: number): string {
  if (cardCount >= 50) return "魔神";
  if (cardCount >= 30) return "大魔導師";
  if (cardCount >= 15) return "魔導師";
  if (cardCount >= 5) return "魔術士";
  return "見習い魔術士";
}

/**
 * 召喚士の称号取得
 */
export function getSummonerTitle(cardCount: number): string {
  if (cardCount >= 50) return "召喚神";
  if (cardCount >= 30) return "召喚師";
  if (cardCount >= 15) return "上級召喚士";
  if (cardCount >= 5) return "召喚士";
  return "見習い召喚士";
}
