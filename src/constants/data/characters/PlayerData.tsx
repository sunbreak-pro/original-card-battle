import {} from "react";
import type { CharacterClass } from "@/types/characterTypes";
import type { Card } from "@/types/cardTypes";
import {
  getSwordsmanTitle,
  getMageTitle,
  getSummonerTitle,
} from "../../../domain/characters/player/logic/tittle";
import { SWORDSMAN_CARDS } from "../cards/swordmanCards";

/**
 * Base player stats used to initialize a new character.
 * Contains only the core stats needed before PlayerContext adds
 * storage, inventory, and resource tracking.
 */
export interface BasePlayerStats {
  playerClass: CharacterClass;
  classGrade: string;
  level: number;
  hp: number;
  maxHp: number;
  ap: number;
  maxAp: number;
  guard: number;
  speed: number;
  cardActEnergy: number;
  deck: Card[];
}

// Create initial starter deck for testing exams (5 basic cards)
const createStarterDeck = () => {
  return [
    { ...SWORDSMAN_CARDS.sw_001, id: "deck_sw_001_1" },
    { ...SWORDSMAN_CARDS.sw_001, id: "deck_sw_001_2" },
    { ...SWORDSMAN_CARDS.sw_002, id: "deck_sw_002_1" },
    { ...SWORDSMAN_CARDS.sw_002, id: "deck_sw_002_2" },
    { ...SWORDSMAN_CARDS.sw_003, id: "deck_sw_003_1" },
  ];
};

export const Swordman_Status: BasePlayerStats = {
  playerClass: "swordsman",
  classGrade: getSwordsmanTitle(0),
  level: 1,
  hp: 100,
  maxHp: 110,
  ap: 0,
  maxAp: 0,
  guard: 0,
  speed: 50,
  cardActEnergy: 3,
  deck: createStarterDeck(),
};

export const Mage_Status: BasePlayerStats = {
  playerClass: "mage",
  classGrade: getMageTitle(0),
  level: 1,
  hp: 60,
  maxHp: 60,
  ap: 0,
  maxAp: 0,
  guard: 0,
  speed: 50,
  cardActEnergy: 3,
  deck: [],
};

export const Summon_Status: BasePlayerStats = {
  playerClass: "summoner",
  classGrade: getSummonerTitle(0),
  level: 1,
  hp: 100,
  maxHp: 100,
  ap: 0,
  maxAp: 0,
  guard: 0,
  speed: 50,
  cardActEnergy: 3,
  deck: [],
};
