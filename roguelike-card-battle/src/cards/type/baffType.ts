export type BuffDebuffType =
  | "bleed"
  | "poison"
  | "burn"
  | "curse"
  | "overCurse"
  | "stun"
  | "atkDownMinor"
  | "atkDownMajor"
  | "defDownMinor"
  | "defDownMajor"
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
  value: number;
  isDebuff: boolean;
  isPercentage: boolean;
  stackable: boolean;
  description(): string;
}

export const BUFF_EFFECTS: Record<BuffDebuffType, BuffEffectDefinition> = {
  bleed: { name: "Bleed", value: 3, isDebuff: true, isPercentage: true, stackable: true, description: () => `${BUFF_EFFECTS.bleed.value}% maxHP damage per card use` },
  poison: { name: "Poison", value: 5, isDebuff: true, isPercentage: false, stackable: true, description: () => `${BUFF_EFFECTS.poison.value} damage at turn end` },
  burn: { name: "Burn", value: 3, isDebuff: true, isPercentage: false, stackable: true, description: () => `${BUFF_EFFECTS.burn.value} damage at turn end` },
  curse: { name: "Curse", value: 20, isDebuff: true, isPercentage: true, stackable: true, description: () => `Healing -${BUFF_EFFECTS.curse.value}%` },
  overCurse: { name: "Heavy Curse", value: 50, isDebuff: true, isPercentage: true, stackable: true, description: () => `Healing -${BUFF_EFFECTS.overCurse.value}%` },
  stun: { name: "Stun", value: 100, isDebuff: true, isPercentage: true, stackable: true, description: () => "Cannot act" },

  atkDownMinor: { name: "Minor Atk Down", value: 15, isDebuff: true, isPercentage: true, stackable: false, description: () => `Attack -${BUFF_EFFECTS.atkDownMinor.value}%` },
  atkDownMajor: { name: "Major Atk Down", value: 30, isDebuff: true, isPercentage: true, stackable: false, description: () => `Attack -${BUFF_EFFECTS.atkDownMajor.value}%` },
  defDownMinor: { name: "Minor Def Down", value: 15, isDebuff: true, isPercentage: true, stackable: false, description: () => `Defense -${BUFF_EFFECTS.defDownMinor.value}%` },
  defDownMajor: { name: "Major Def Down", value: 30, isDebuff: true, isPercentage: true, stackable: false, description: () => `Defense -${BUFF_EFFECTS.defDownMajor.value}%` },
  slow: { name: "Slow", value: 10, isDebuff: true, isPercentage: false, stackable: true, description: () => `Speed -${BUFF_EFFECTS.slow.value}` },
  stall: { name: "Stall", value: 15, isDebuff: true, isPercentage: true, stackable: true, description: () => `Speed -${BUFF_EFFECTS.stall.value}` },

  atkUpMinor: { name: "Minor Atk Up", value: 15, isDebuff: false, isPercentage: true, stackable: false, description: () => `Attack +${BUFF_EFFECTS.atkUpMinor.value}%` },
  atkUpMajor: { name: "Major Atk Up", value: 30, isDebuff: false, isPercentage: true, stackable: false, description: () => `Attack +${BUFF_EFFECTS.atkUpMajor.value}%` },
  defUpMinor: { name: "Minor Def Up", value: 15, isDebuff: false, isPercentage: true, stackable: false, description: () => `Defense +${BUFF_EFFECTS.defUpMinor.value}%` },
  defUpMajor: { name: "Major Def Up", value: 30, isDebuff: false, isPercentage: true, stackable: false, description: () => `Defense +${BUFF_EFFECTS.defUpMajor.value}%` },
  penetrationUp: { name: "Penetration Up", value: 30, isDebuff: false, isPercentage: true, stackable: true, description: () => `Penetration +${BUFF_EFFECTS.penetrationUp.value}%` },
  hitRateUp: { name: "Hit Rate Up", value: 15, isDebuff: false, isPercentage: true, stackable: true, description: () => `Hit rate +${BUFF_EFFECTS.hitRateUp.value}%` },
  criticalUp: { name: "Critical Up", value: 15, isDebuff: false, isPercentage: true, stackable: true, description: () => `Critical damage +${BUFF_EFFECTS.criticalUp.value}%` },
  haste: { name: "Haste", value: 15, isDebuff: false, isPercentage: true, stackable: true, description: () => `Speed +${BUFF_EFFECTS.haste.value}` },
  superFast: { name: "Super Fast", value: 30, isDebuff: false, isPercentage: true, stackable: true, description: () => `Speed +${BUFF_EFFECTS.superFast.value}` },

  regeneration: { name: "Regeneration", value: 5, isDebuff: false, isPercentage: false, stackable: true, description: () => `Heal ${BUFF_EFFECTS.regeneration.value} HP per turn` },
  shieldRegen: { name: "Iron Stance", value: 5, isDebuff: false, isPercentage: false, stackable: true, description: () => `Guard +${BUFF_EFFECTS.shieldRegen.value} per turn` },
  reflect: { name: "Reflect", value: 30, isDebuff: false, isPercentage: true, stackable: true, description: () => `Reflect ${BUFF_EFFECTS.reflect.value}% damage` },
  immunity: { name: "Immunity", value: 1, isDebuff: false, isPercentage: false, stackable: true, description: () => "Immune to damage" },

  energyRegen: { name: "Energy Regen", value: 1, isDebuff: false, isPercentage: false, stackable: true, description: () => `Energy +${BUFF_EFFECTS.energyRegen.value}` },
  drawPower: { name: "Draw Power", value: 1, isDebuff: false, isPercentage: false, stackable: true, description: () => "Draw +1 card" },
  costReduction: { name: "Cost Reduction", value: 1, isDebuff: false, isPercentage: false, stackable: true, description: () => "Cost -1" },

  lifesteal: { name: "Lifesteal", value: 30, isDebuff: false, isPercentage: true, stackable: true, description: () => `Heal ${BUFF_EFFECTS.lifesteal.value}% of damage dealt` },
  doubleStrike: { name: "Double Strike", value: 50, isDebuff: false, isPercentage: true, stackable: true, description: () => "Next ATK card triggers twice" },

  swordEnergyGain: { name: "Sword Energy Amp", value: 3, isDebuff: false, isPercentage: true, stackable: true, description: () => `Sword energy gain +${BUFF_EFFECTS.swordEnergyGain.value}%` },

  elementalMastery: { name: "Elemental Mastery", value: 30, isDebuff: false, isPercentage: true, stackable: true, description: () => `Elemental damage +${BUFF_EFFECTS.elementalMastery.value}%` },
  fireField: { name: "Fire Field", value: 50, isDebuff: false, isPercentage: true, stackable: true, description: () => `Fire card effects +${BUFF_EFFECTS.fireField.value}%` },
  electroField: { name: "Electric Field", value: 10, isDebuff: false, isPercentage: true, stackable: true, description: () => `+${BUFF_EFFECTS.electroField.value} damage per lightning card` },

  summonPower: { name: "Summon Power", value: 30, isDebuff: false, isPercentage: true, stackable: true, description: () => `Summon power +${BUFF_EFFECTS.summonPower.value}%` },
  sacrificeBonus: { name: "Sacrifice Bonus", value: 30, isDebuff: false, isPercentage: true, stackable: true, description: () => `Sacrifice effect +${BUFF_EFFECTS.sacrificeBonus.value}%` },

  focus: { name: "Focus", value: 50, isDebuff: false, isPercentage: true, stackable: true, description: () => `Next card effect +${BUFF_EFFECTS.focus.value}%` },
  momentum: { name: "Momentum", value: 5, isDebuff: false, isPercentage: true, stackable: true, description: () => `Attack +${BUFF_EFFECTS.momentum.value}% per card used` },
  cleanse: { name: "Cleanse", value: 1, isDebuff: false, isPercentage: false, stackable: true, description: () => "Remove 1 debuff at turn end" },
  tenacity: { name: "Tenacity", value: 30, isDebuff: false, isPercentage: true, stackable: true, description: () => `All stats +${BUFF_EFFECTS.tenacity.value}% when HP < 30%` },
  lastStand: { name: "Last Stand", value: 1, isDebuff: false, isPercentage: false, stackable: true, description: () => "Survive lethal damage once" },
};

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