import { BUFF_EFFECTS } from "../data/buffData";

/**
 * Represents who applied a buff/debuff.
 * Used to determine when duration should decrease.
 */
export type BuffOwner = 'player' | 'enemy' | 'environment';

export type BuffDebuffType =
  | "bleed"
  | "poison"
  | "burn"
  | "curse"
  | "overCurse"
  | "stagger"
  | "stun"
  | "freeze"
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
  | "iceField"
  | "darkField"
  | "lightField"
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
  /**
   * Who applied this buff/debuff. Duration decreases only at the applier's phase start.
   * Optional for backward compatibility - defaults to 'environment' when not specified.
   */
  appliedBy?: BuffOwner;
}

export type BuffDebuffMap = Map<BuffDebuffType, BuffDebuffState>;

export function createBuffState(
  buff: CardBuffSpec,
  source?: string,
  appliedBy: BuffOwner = 'environment'
): BuffDebuffState {
  const effectDef = BUFF_EFFECTS[buff.name];
  return {
    name: buff.name,
    stacks: effectDef.stackable ? buff.stacks : 1,
    duration: buff.duration,
    value: effectDef.value,
    isPermanent: buff.isPermanent ?? false,
    source,
    appliedBy,
  };
}