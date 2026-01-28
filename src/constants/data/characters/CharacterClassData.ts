/**
 * Character Class Data
 *
 * Contains information about each playable character class including
 * stats, descriptions, unique mechanics, and starter decks.
 */

import type { CharacterClass } from "@/types/characterTypes";
import type { Card } from "@/types/cardTypes";
import { SWORDSMAN_CARDS } from "../../../domain/cards/data/SwordmanCards";
import { MAGE_CARDS } from "../../../domain/cards/data/mageCards";
import { INITIAL_DECK_BY_CLASS } from "../battles/initialDeckConfig";

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
 * Create starter deck from deck counts configuration
 * Used for character selection display (shows unique card types as stacks)
 */
function createStarterDeckFromCounts(
  deckCounts: Record<string, number>,
  cardData: Record<string, Card>
): Card[] {
  const deck: Card[] = [];
  let instanceCounter = 1;

  for (const [cardTypeId, count] of Object.entries(deckCounts)) {
    const cardTemplate = cardData[cardTypeId];
    if (!cardTemplate) continue;

    for (let i = 0; i < count; i++) {
      deck.push({
        ...cardTemplate,
        id: `starter_${cardTypeId}_${instanceCounter++}`,
      });
    }
  }

  return deck;
}

/**
 * Create Swordsman's starter deck from initial deck config (15 cards)
 */
function createSwordsmanStarterDeck(): Card[] {
  return createStarterDeckFromCounts(
    INITIAL_DECK_BY_CLASS.swordsman,
    SWORDSMAN_CARDS
  );
}

/**
 * Create Mage's starter deck from initial deck config (15 cards)
 */
function createMageStarterDeck(): Card[] {
  return createStarterDeckFromCounts(
    INITIAL_DECK_BY_CLASS.mage,
    MAGE_CARDS
  );
}

/**
 * Get card data by character class
 * Returns the card definitions for a specific class
 */
function getCardDataByClass(classType: CharacterClass): Record<string, Card> {
  switch (classType) {
    case "swordsman":
      return SWORDSMAN_CARDS;
    case "mage":
      return MAGE_CARDS;
    case "summoner":
      // TODO: Return SUMMONER_CARDS when implemented
      return {};
    default:
      return SWORDSMAN_CARDS;
  }
}

/**
 * Get starter deck card stacks for display in character selection
 * Groups cards by type for a cleaner display
 */
export function getStarterDeckStacks(
  classType: CharacterClass
): { card: Card; count: number }[] {
  const deckCounts = INITIAL_DECK_BY_CLASS[classType];
  if (!deckCounts || Object.keys(deckCounts).length === 0) {
    return [];
  }

  const stacks: { card: Card; count: number }[] = [];
  const cardData = getCardDataByClass(classType);

  for (const [cardTypeId, count] of Object.entries(deckCounts)) {
    const cardTemplate = cardData[cardTypeId];
    if (cardTemplate) {
      stacks.push({ card: cardTemplate, count });
    }
  }

  return stacks;
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
      uniqueMechanic: "Elemental Resonance",
      mechanicDescription:
        "同属性カードを連続使用すると共鳴レベルが上昇。最大Lv2でダメージ+30%と属性効果発動。",
      stats: {
        hp: 60,
        ap: 20,
        speed: 60,
        cardActEnergy: 3,
      },
      starterDeck: createMageStarterDeck(),
      isAvailable: true,
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
