import type { Card } from "../../cards/type/cardType";
import type { Character } from "./characterType";
export type PlayerClass = "swordsman" | "mage" | "summoner";
export interface Player extends Character {
  name?: string;
  playerClass: PlayerClass;
  classGrade: string;
  level: number;
  hp: number;
  maxHp: number;
  ap: number;
  maxAp: number;
  guard: number;
  speed: number;
  cardActEnergy: number;
  gold: number;
  deck: Card[];
  buffDebuffs: Character["buffDebuffs"];
  equipment?: string[];
  equipmentAtkPercent?: number;
  equipmentDefPercent?: number;
  tittle?: string[];
}