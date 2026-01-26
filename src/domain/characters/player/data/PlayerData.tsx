import {} from "react";
import type { Player } from "../../type/playerTypes";
import {
  getSwordsmanTitle,
  getMageTitle,
  getSummonerTitle,
} from "../logic/tittle";
import { SWORDSMAN_CARDS } from "../../../cards/data/SwordmanCards";

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

export const Swordman_Status: Player = {
  playerClass: "swordsman",
  classGrade: getSwordsmanTitle(0),
  level: 1,
  hp: 100,
  maxHp: 110,
  ap: 30,
  maxAp: 30,
  guard: 0,
  speed: 50,
  cardActEnergy: 3,
  gold: 0,
  deck: createStarterDeck(), // Add starter cards for testing
  equipment: [],
  buffDebuffs: new Map(),
  equipmentAtkPercent: 0,
  equipmentDefPercent: 0,
};

export const Mage_Status: Player = {
  playerClass: "mage",
  classGrade: getMageTitle(0),
  level: 1,
  hp: 60,
  maxHp: 60,
  ap: 30,
  maxAp: 30,
  guard: 0,
  speed: 50,
  cardActEnergy: 3,
  gold: 0,
  deck: [],
  equipment: [],
  buffDebuffs: new Map(),
  equipmentAtkPercent: 0,
  equipmentDefPercent: 0,
};

export const Summon_Status: Player = {
  playerClass: "summoner",
  classGrade: getSummonerTitle(0),
  level: 1,
  hp: 100,
  maxHp: 100,
  ap: 30,
  maxAp: 30,
  guard: 0,
  speed: 50,
  cardActEnergy: 3,
  gold: 0,
  deck: [],
  equipment: [],
  equipmentAtkPercent: 0,
  equipmentDefPercent: 0,
  buffDebuffs: new Map(),
};
