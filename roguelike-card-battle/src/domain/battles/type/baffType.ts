import { BUFF_EFFECTS } from "../data/buffData";

export type BuffDebuffType =
  | "bleed"
  | "poison"
  | "burn"
  | "curse"
  | "overCurse"
  | "stagger"
  | "stun"
  | "atkDownMinor"
  | "atkDownMajor"
  | "defDownMinor"
  | "defDownMajor"
  | "weakness"
  | "prostoration"
  | "slow"
  | "stall"
  | "atkUpMinor"
  | "atkUpMajor"
  | "defUpMinor"
  | "defUpMajor"
  | "penetrationUp"
  | "hitRateUp"
  | "criticalUp"
  | "haste"
  | "superFast"
  | "regeneration"
  | "shieldRegen"
  | "reflect"
  | "immunity"
  | "energyRegen"
  | "drawPower"
  | "costReduction"
  | "lifesteal"
  | "doubleStrike"
  | "swordEnergyGain"
  | "elementalMastery"
  | "fireField"
  | "electroField"
  | "summonPower"
  | "sacrificeBonus"
  | "focus"
  | "momentum"
  | "cleanse"
  | "tenacity"
  | "lastStand";

export interface BuffEffectDefinition {
  name: string;
  nameJa: string;
  value: number;
  isDebuff: boolean;
  isPercentage: boolean;
  stackable: boolean;
  description(): string;
}


export function getBuffValue(type: BuffDebuffType): number {
  return BUFF_EFFECTS[type].value;
}

export function isStackable(type: BuffDebuffType): boolean {
  return BUFF_EFFECTS[type].stackable;
}

export interface CardBuffSpec {
  name: BuffDebuffType;
  duration: number;
  stacks: number;
  isPermanent?: boolean;
}
export interface BuffDebuffState {
  name: BuffDebuffType;
  stacks: number;
  duration: number;
  value: number;
  isPermanent: boolean;
  source?: string;
}

export type BuffDebuffMap = Map<BuffDebuffType, BuffDebuffState>;

export function createBuffState(buff: CardBuffSpec, source?: string): BuffDebuffState {
  const effectDef = BUFF_EFFECTS[buff.name];
  return {
    name: buff.name,
    stacks: effectDef.stackable ? buff.stacks : 1,
    duration: buff.duration,
    value: effectDef.value,
    isPermanent: buff.isPermanent ?? false,
    source,
  };
}