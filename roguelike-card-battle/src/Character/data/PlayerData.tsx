import {} from "react";

export interface Player {
  id: string;
  classGrade: string[];
  level: number;
  experience: number;
  health: number;
  maxHealth: number;
  mana: number;
  maxMana: number;
  gold: number;
  deck: string[]; // Card IDs
  equipment: string[]; // Equipment IDs
  statusEffects: Record<string, number>; // StatusEffect ID to duration
}

export const swordMaster: Player = {
  id: "sword_master",
  classGrade: ["見習い剣士", "剣士", "剣聖"],
  level: 1,
  experience: 0,
  health: 100,
  maxHealth: 100,
  mana: 30,
  maxMana: 30,
  gold: 50,
  deck: [],
  equipment: [],
  statusEffects: {},
};
