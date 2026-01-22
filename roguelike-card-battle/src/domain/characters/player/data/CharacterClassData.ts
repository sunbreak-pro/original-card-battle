/**
 * Character Class Data
 *
 * Contains information about each playable character class including
 * stats, descriptions, unique mechanics, and starter decks.
 */

import type { CharacterClass } from "../../type/baseTypes";
import type { Card } from "../../../cards/type/cardType";
import { SWORDSMAN_CARDS } from "../../../cards/data/SwordmanCards";

/**
 * Character class information for selection screen
 */
export interface CharacterClassInfo {
  class: CharacterClass;
  name: string;
  japaneseName: string;
  description: string;
  uniqueMechanic: string;
  mechanicDescription: string;
  stats: {
    hp: number;
    ap: number;
    speed: number;
    cardActEnergy: number;
  };
  starterDeck: Card[];
  isAvailable: boolean;
  themeColor: string;
  icon: string;
}

/**
 * Create Swordsman's starter deck (5 basic cards)
 */
function createSwordsmanStarterDeck(): Card[] {
  return [
    { ...SWORDSMAN_CARDS.sw_001, id: "starter_sw_001_1" },
    { ...SWORDSMAN_CARDS.sw_001, id: "starter_sw_001_2" },
    { ...SWORDSMAN_CARDS.sw_002, id: "starter_sw_002_1" },
    { ...SWORDSMAN_CARDS.sw_002, id: "starter_sw_002_2" },
    { ...SWORDSMAN_CARDS.sw_003, id: "starter_sw_003_1" },
  ];
}

/**
 * Character class data for all playable classes
 */
export const CHARACTER_CLASS_DATA: Record<CharacterClass, CharacterClassInfo> =
  {
    swordsman: {
      class: "swordsman",
      name: "Swordsman",
      japaneseName: "剣士",
      description:
        "剣気を操る近接戦闘のスペシャリスト。攻撃を重ねるごとに剣気が蓄積され、強力な必殺技を放つことができる。バランスの取れたステータスと安定した戦闘スタイルが特徴。",
      uniqueMechanic: "Sword Energy",
      mechanicDescription:
        "攻撃カードを使用するたびに剣気が蓄積。剣気を消費して強力な技を発動できる。",
      stats: {
        hp: 100,
        ap: 30,
        speed: 50,
        cardActEnergy: 3,
      },
      starterDeck: createSwordsmanStarterDeck(),
      isAvailable: true,
      themeColor: "#ef4444", // Red
      icon: "sword",
    },
    mage: {
      class: "mage",
      name: "Mage",
      japaneseName: "魔術師",
      description:
        "火・水・雷の三属性を操る魔法使い。属性の連鎖により威力が増幅され、敵を焼き尽くす。HPは低いが、高い火力と豊富な範囲攻撃を持つ。",
      uniqueMechanic: "Elemental Chain",
      mechanicDescription:
        "異なる属性を連続で使用すると連鎖ボーナスが発生。連鎖数に応じてダメージが増加。",
      stats: {
        hp: 60,
        ap: 20,
        speed: 60,
        cardActEnergy: 3,
      },
      starterDeck: [], // Not yet implemented
      isAvailable: false,
      themeColor: "#3b82f6", // Blue
      icon: "staff",
    },
    summoner: {
      class: "summoner",
      name: "Summoner",
      japaneseName: "召喚士",
      description:
        "異界の生物を召喚し戦わせるテクニカルなクラス。召喚獣が戦闘を代行し、本人は後方から支援を行う。召喚獣の組み合わせにより多彩な戦略が可能。",
      uniqueMechanic: "Summon System",
      mechanicDescription:
        "召喚獣を場に出して戦わせる。召喚獣は独自のHPを持ち、自動で攻撃を行う。",
      stats: {
        hp: 80,
        ap: 25,
        speed: 40,
        cardActEnergy: 3,
      },
      starterDeck: [], // Not yet implemented
      isAvailable: false,
      themeColor: "#22c55e", // Green
      icon: "summon",
    },
  };

/**
 * Get character class info by class type
 */
export function getCharacterClassInfo(
  classType: CharacterClass
): CharacterClassInfo {
  return CHARACTER_CLASS_DATA[classType];
}

/**
 * Get all available character classes
 */
export function getAvailableClasses(): CharacterClassInfo[] {
  return Object.values(CHARACTER_CLASS_DATA).filter(
    (classInfo) => classInfo.isAvailable
  );
}

/**
 * Get all character classes (including unavailable)
 */
export function getAllClasses(): CharacterClassInfo[] {
  return Object.values(CHARACTER_CLASS_DATA);
}
