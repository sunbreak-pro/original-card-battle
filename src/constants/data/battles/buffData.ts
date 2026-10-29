import type { BuffDebuffType, BuffEffectDefinition } from "@/types/battleTypes";

// ============================================================
// Buff Category Maps for Data-Driven Calculation
// ============================================================

/** Calculation mode for buff effects */
type BuffCalcMode = "additive" | "multiplicative" | "stackScaled";

interface AttackBuffEntry {
  type: BuffDebuffType;
  mode: BuffCalcMode;
}

/** Buffs that increase attack multiplier (additive to base 1.0) */
export const ATTACK_BUFF_TYPES: AttackBuffEntry[] = [
  { type: "atkUpMinor", mode: "additive" },
  { type: "atkUpMajor", mode: "additive" },
  { type: "momentum", mode: "stackScaled" },
];

/** Debuffs that reduce attack multiplier (multiplicative) */
export const ATTACK_DEBUFF_TYPES: BuffDebuffType[] = [
  "atkDownMinor",
  "atkDownMajor",
];

/** Buffs that reduce incoming damage (multiplicative on damageReductionMod) */
export const DAMAGE_REDUCTION_BUFF_TYPES: BuffDebuffType[] = [
  "defUpMinor",
  "defUpMajor",
];

/** Debuffs that increase incoming damage (multiplicative on vulnerabilityMod) */
export const VULNERABILITY_DEBUFF_TYPES: BuffDebuffType[] = [
  "defDownMinor",
  "defDownMajor",
];

// ============================================================
// Phase-based Effect Category Maps
// ============================================================

/** Buffs that heal HP at start of phase */
export const START_PHASE_HEALING_BUFFS: BuffDebuffType[] = ["regeneration"];

/** Buffs that grant shield at start of phase */
export const START_PHASE_SHIELD_BUFFS: BuffDebuffType[] = ["shieldRegen"];

/** Debuffs that deal damage at end of phase */
export const END_PHASE_DAMAGE_BUFFS: BuffDebuffType[] = ["burn", "poison"];

/** Buffs that reflect damage back to attacker */
export const REFLECT_BUFFS: BuffDebuffType[] = ["reflect"];

/** Buffs that heal based on damage dealt */
export const LIFESTEAL_BUFFS: BuffDebuffType[] = ["lifesteal"];

/** Buffs that modify card draw count */
export const DRAW_MODIFIER_BUFFS: BuffDebuffType[] = ["drawPower"];

/** Buffs that mitigate vulnerability effects (tenacity reduces excess vulnerability) */
export const VULNERABILITY_MITIGATION_BUFFS: BuffDebuffType[] = ["tenacity"];

export const BUFF_EFFECTS: Record<BuffDebuffType, BuffEffectDefinition> = {
    bleed: { name: "Bleed", nameJa: "出血", value: 3, isDebuff: true, isPercentage: true, stackable: true, description: () => `${BUFF_EFFECTS.bleed.value}% maxHP damage per card use` },
    poison: { name: "Poison", nameJa: "毒", value: 5, isDebuff: true, isPercentage: false, stackable: true, description: () => `${BUFF_EFFECTS.poison.value} damage at turn end` },
    burn: { name: "Burn", nameJa: "火傷", value: 3, isDebuff: true, isPercentage: false, stackable: true, description: () => `${BUFF_EFFECTS.burn.value} damage at turn end` },
    curse: { name: "Curse", nameJa: "呪い", value: 20, isDebuff: true, isPercentage: true, stackable: true, description: () => `Healing -${BUFF_EFFECTS.curse.value}%` },
    overCurse: { name: "Heavy Curse", nameJa: "重呪い", value: 50, isDebuff: true, isPercentage: true, stackable: true, description: () => `Healing -${BUFF_EFFECTS.overCurse.value}%` },
    stagger: { name: "stagger", nameJa: "よろめき", value: 100, isDebuff: true, isPercentage: true, stackable: true, description: () => "Cannot act" },
    stun: { name: "Stun", nameJa: "気絶", value: 100, isDebuff: true, isPercentage: true, stackable: true, description: () => "Skip next turn" },
    freeze: { name: "Freeze", nameJa: "凍結", value: 100, isDebuff: true, isPercentage: true, stackable: false, description: () => "Cannot act (ice)" },
    atkDownMinor: { name: "Minor Atk Down", nameJa: "脱力", value: 15, isDebuff: true, isPercentage: true, stackable: false, description: () => `Attack -${BUFF_EFFECTS.atkDownMinor.value}%` },
    atkDownMajor: { name: "Major Atk Down", nameJa: "無力", value: 30, isDebuff: true, isPercentage: true, stackable: false, description: () => `Attack -${BUFF_EFFECTS.atkDownMajor.value}%` },
    defDownMinor: { name: "Minor Def Down", nameJa: "軟弱", value: 15, isDebuff: true, isPercentage: true, stackable: false, description: () => `Defense -${BUFF_EFFECTS.defDownMinor.value}%` },
    defDownMajor: { name: "Major Def Down", nameJa: "無防備", value: 30, isDebuff: true, isPercentage: true, stackable: false, description: () => `Defense -${BUFF_EFFECTS.defDownMajor.value}%` },
    weakness: { name: "Weakness", nameJa: "衰弱", value: 20, isDebuff: true, isPercentage: true, stackable: true, description: () => `All stats -${BUFF_EFFECTS.weakness.value}%` },
    prostoration: { name: "Prostoration", nameJa: "虚弱", value: 50, isDebuff: true, isPercentage: true, stackable: true, description: () => `All stats -${BUFF_EFFECTS.prostoration.value}%` },
    slow: { name: "Slow", nameJa: "鈍足", value: 10, isDebuff: true, isPercentage: false, stackable: true, description: () => `Speed -${BUFF_EFFECTS.slow.value}` },
    stall: { name: "Stall", nameJa: "失速", value: 15, isDebuff: true, isPercentage: true, stackable: true, description: () => `Speed -${BUFF_EFFECTS.stall.value}` },

    atkUpMinor: { name: "Minor Atk Up", nameJa: "剛力", value: 15, isDebuff: false, isPercentage: true, stackable: false, description: () => `Attack +${BUFF_EFFECTS.atkUpMinor.value}%` },
    atkUpMajor: { name: "Major Atk Up", nameJa: "豪力", value: 30, isDebuff: false, isPercentage: true, stackable: false, description: () => `Attack +${BUFF_EFFECTS.atkUpMajor.value}%` },
    defUpMinor: { name: "Minor Def Up", nameJa: "堅牢", value: 15, isDebuff: false, isPercentage: true, stackable: false, description: () => `Defense +${BUFF_EFFECTS.defUpMinor.value}%` },
    defUpMajor: { name: "Major Def Up", nameJa: "金剛", value: 30, isDebuff: false, isPercentage: true, stackable: false, description: () => `Defense +${BUFF_EFFECTS.defUpMajor.value}%` },
    penetrationUp: { name: "Penetration Up", nameJa: "衝撃力アップ", value: 30, isDebuff: false, isPercentage: true, stackable: true, description: () => `Penetration +${BUFF_EFFECTS.penetrationUp.value}%` },
    hitRateUp: { name: "Hit Rate Up", nameJa: "会心率アップ", value: 15, isDebuff: false, isPercentage: true, stackable: true, description: () => `Hit rate +${BUFF_EFFECTS.hitRateUp.value}%` },
    criticalUp: { name: "Critical Up", nameJa: "会心力アップ", value: 15, isDebuff: false, isPercentage: true, stackable: true, description: () => `Critical damage +${BUFF_EFFECTS.criticalUp.value}%` },
    haste: { name: "Haste", nameJa: "加速", value: 15, isDebuff: false, isPercentage: true, stackable: true, description: () => `Speed +${BUFF_EFFECTS.haste.value}` },
    superFast: { name: "Super Fast", nameJa: "高速", value: 30, isDebuff: false, isPercentage: true, stackable: true, description: () => `Speed +${BUFF_EFFECTS.superFast.value}` },

    regeneration: { name: "Regeneration", nameJa: "再生", value: 5, isDebuff: false, isPercentage: false, stackable: true, description: () => `Heal ${BUFF_EFFECTS.regeneration.value} HP per turn` },
    shieldRegen: { name: "Iron Stance", nameJa: "鉄の構え", value: 5, isDebuff: false, isPercentage: false, stackable: true, description: () => `Guard +${BUFF_EFFECTS.shieldRegen.value} per turn` },
    reflect: { name: "Reflect", nameJa: "流転の構え", value: 30, isDebuff: false, isPercentage: true, stackable: true, description: () => `Reflect ${BUFF_EFFECTS.reflect.value}% damage` },
    immunity: { name: "Immunity", nameJa: "免疫", value: 1, isDebuff: false, isPercentage: false, stackable: true, description: () => "Immune to damage" },

    energyRegen: { name: "Energy Regen", nameJa: "完璧な呼吸", value: 1, isDebuff: false, isPercentage: false, stackable: true, description: () => `Energy +${BUFF_EFFECTS.energyRegen.value}` },
    drawPower: { name: "Draw Power", nameJa: "冷静な俯瞰", value: 1, isDebuff: false, isPercentage: false, stackable: true, description: () => "Draw +1 card" },
    costReduction: { name: "Cost Reduction", nameJa: "体力温存", value: 1, isDebuff: false, isPercentage: false, stackable: true, description: () => "Cost -1" },

    lifesteal: { name: "Lifesteal", nameJa: "吸血", value: 30, isDebuff: false, isPercentage: true, stackable: true, description: () => `Heal ${BUFF_EFFECTS.lifesteal.value}% of damage dealt` },
    doubleStrike: { name: "Double Strike", nameJa: "はやぶさの構え", value: 50, isDebuff: false, isPercentage: true, stackable: true, description: () => "Next ATK card triggers twice" },

    swordEnergyGain: { name: "Sword Energy Amp", nameJa: "剣気精錬", value: 3, isDebuff: false, isPercentage: true, stackable: true, description: () => `Sword energy gain +${BUFF_EFFECTS.swordEnergyGain.value}%` },

    elementalMastery: { name: "Elemental Mastery", nameJa: "元素熟達", value: 30, isDebuff: false, isPercentage: true, stackable: true, description: () => `Elemental damage +${BUFF_EFFECTS.elementalMastery.value}%` },
    fireField: { name: "Fire Field", nameJa: "爆焔界", value: 50, isDebuff: false, isPercentage: true, stackable: true, description: () => `Fire card effects +${BUFF_EFFECTS.fireField.value}%` },
    electroField: { name: "Electric Field", nameJa: "雷鳴界", value: 10, isDebuff: false, isPercentage: true, stackable: true, description: () => `+${BUFF_EFFECTS.electroField.value} damage per lightning card` },
    iceField: { name: "Ice Field", nameJa: "冷静界", value: 50, isDebuff: false, isPercentage: true, stackable: false, description: () => `Ice damage +${BUFF_EFFECTS.iceField.value}%` },
    darkField: { name: "Dark Field", nameJa: "闇界", value: 50, isDebuff: false, isPercentage: true, stackable: false, description: () => `Dark damage +${BUFF_EFFECTS.darkField.value}%` },
    lightField: { name: "Light Field", nameJa: "光界", value: 50, isDebuff: false, isPercentage: true, stackable: false, description: () => `Light damage +${BUFF_EFFECTS.lightField.value}%` },

    focus: { name: "Focus", nameJa: "集中", value: 50, isDebuff: false, isPercentage: true, stackable: true, description: () => `Next card effect +${BUFF_EFFECTS.focus.value}%` },
    momentum: { name: "Momentum", nameJa: "勢い", value: 5, isDebuff: false, isPercentage: true, stackable: true, description: () => `Attack +${BUFF_EFFECTS.momentum.value}% per card used` },
    cleanse: { name: "Cleanse", nameJa: "浄化", value: 1, isDebuff: false, isPercentage: false, stackable: true, description: () => "Remove 1 debuff at turn end" },
    tenacity: { name: "Tenacity", nameJa: "不屈", value: 30, isDebuff: false, isPercentage: true, stackable: true, description: () => `All stats +${BUFF_EFFECTS.tenacity.value}% when HP < 30%` },
    lastStand: { name: "Last Stand", nameJa: "再誕", value: 1, isDebuff: false, isPercentage: false, stackable: true, description: () => "Survive lethal damage once" },
};

export const BUFF_DEBUFF_ICONS: Record<BuffDebuffType, string> = {
    burn: "🔥",
    bleed: "🩸",
    poison: "☠️",
    curse: "👿",
    overCurse: "💀",
    stagger: "💫",
    stun: "💥",
    freeze: "🧊",
    atkDownMinor: "⚔️↓",
    atkDownMajor: "⚔️⬇️",
    defDownMinor: "🛡️↓",
    defDownMajor: "🛡️⬇️",
    weakness: "🥀",
    prostoration: "😵",
    slow: "🐌",
    stall: "🐢",
    atkUpMinor: "⚔️↑",
    atkUpMajor: "⚔️⬆️",
    defUpMinor: "🛡️↑",
    defUpMajor: "🛡️⬆️",
    penetrationUp: "🎯↑",
    hitRateUp: "🎯",
    criticalUp: "⭐",
    haste: "⚡",
    superFast: "⚡⚡",
    regeneration: "💚",
    shieldRegen: "🛡️",
    reflect: "🔄",
    immunity: "✨",
    energyRegen: "⚡🔄",
    drawPower: "🃏",
    costReduction: "💰↓",
    lifesteal: "🩸💚",
    doubleStrike: "⚔️⚔️",
    swordEnergyGain: "⚔️⚡",
    elementalMastery: "🔮✨",
    fireField: "🔥🌐",
    electroField: "⚡🌐",
    iceField: "❄️🌐",
    darkField: "🌑🌐",
    lightField: "✨🌐",
    focus: "🎯",
    momentum: "🔥↑",
    cleanse: "✨💧",
    tenacity: "💪",
    lastStand: "⚔️🔥",
};
