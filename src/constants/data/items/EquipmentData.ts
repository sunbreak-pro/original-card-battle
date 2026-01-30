import type { ItemRarity, EquipmentStatBonuses } from "@/types/itemTypes";
import type { EquipmentSlot } from "@/types/itemTypes";
export const EquipmentData = {
}
export const EQUIPMENT_TEMPLATES: Record<
    EquipmentSlot,
    Record<ItemRarity, { name: string; icon: string }>
> = {
    weapon: {
        common: { name: "Iron Sword", icon: "\u{1F5E1}\u{FE0F}" },
        uncommon: { name: "Steel Blade", icon: "\u{2694}\u{FE0F}" },
        rare: { name: "Enchanted Sword", icon: "\u{1F52E}" },
        epic: { name: "Demon Slayer", icon: "\u{2694}\u{FE0F}" },
        legendary: { name: "Excalibur", icon: "\u{2728}" },
    },
    armor: {
        common: { name: "Leather Armor", icon: "\u{1F6E1}\u{FE0F}" },
        uncommon: { name: "Chain Mail", icon: "\u{1F517}" },
        rare: { name: "Plate Armor", icon: "\u{1F6E1}\u{FE0F}" },
        epic: { name: "Dragon Scale Armor", icon: "\u{1F409}" },
        legendary: { name: "Celestial Plate", icon: "\u{2728}" },
    },
    helmet: {
        common: { name: "Leather Cap", icon: "\u{1F393}" },
        uncommon: { name: "Iron Helm", icon: "\u{26D1}\u{FE0F}" },
        rare: { name: "Enchanted Helm", icon: "\u{1F451}" },
        epic: { name: "Crown of Thorns", icon: "\u{1F451}" },
        legendary: { name: "Crown of the Ancients", icon: "\u{2728}" },
    },
    boots: {
        common: { name: "Leather Boots", icon: "\u{1F462}" },
        uncommon: { name: "Iron Greaves", icon: "\u{1F9B6}" },
        rare: { name: "Swift Boots", icon: "\u{1F4A8}" },
        epic: { name: "Shadow Steps", icon: "\u{1F463}" },
        legendary: { name: "Wings of Mercury", icon: "\u{2728}" },
    },
    accessory1: {
        common: { name: "Copper Ring", icon: "\u{1F48D}" },
        uncommon: { name: "Silver Ring", icon: "\u{1F48E}" },
        rare: { name: "Enchanted Ring", icon: "\u{2728}" },
        epic: { name: "Ring of Power", icon: "\u{1F525}" },
        legendary: { name: "Ring of the Void", icon: "\u{1F300}" },
    },
    accessory2: {
        common: { name: "Bone Amulet", icon: "\u{1F4FF}" },
        uncommon: { name: "Silver Amulet", icon: "\u{1F3F5}\u{FE0F}" },
        rare: { name: "Enchanted Amulet", icon: "\u{1F4AB}" },
        epic: { name: "Dragon Heart Amulet", icon: "\u{2764}\u{FE0F}" },
        legendary: { name: "Amulet of Eternity", icon: "\u{267E}\u{FE0F}" },
    },
};

/**
 * Equipment stat bonuses by slot and rarity.
 * Based on design doc: HP+10~50, ATK+5%~30%, DEF+5%~30%, Speed, Energy.
 */
export const EQUIPMENT_STAT_BONUSES: Record<
    EquipmentSlot,
    Record<ItemRarity, EquipmentStatBonuses>
> = {
    weapon: {
        common:    { atkPercent: 5 },
        uncommon:  { atkPercent: 10 },
        rare:      { atkPercent: 15, speedBonus: 1 },
        epic:      { atkPercent: 22, speedBonus: 2 },
        legendary: { atkPercent: 30, speedBonus: 3, energyBonus: 1 },
    },
    armor: {
        common:    { hpBonus: 10, defPercent: 5 },
        uncommon:  { hpBonus: 15, defPercent: 8 },
        rare:      { hpBonus: 22, defPercent: 12 },
        epic:      { hpBonus: 32, defPercent: 18 },
        legendary: { hpBonus: 50, defPercent: 25 },
    },
    helmet: {
        common:    { hpBonus: 8, defPercent: 3 },
        uncommon:  { hpBonus: 12, defPercent: 5 },
        rare:      { hpBonus: 18, defPercent: 8 },
        epic:      { hpBonus: 25, defPercent: 12, apBonus: 5 },
        legendary: { hpBonus: 35, defPercent: 18, apBonus: 10 },
    },
    boots: {
        common:    { speedBonus: 1 },
        uncommon:  { speedBonus: 2, defPercent: 3 },
        rare:      { speedBonus: 3, defPercent: 5 },
        epic:      { speedBonus: 5, defPercent: 8, hpBonus: 10 },
        legendary: { speedBonus: 8, defPercent: 10, hpBonus: 15 },
    },
    accessory1: {
        common:    { hpBonus: 5 },
        uncommon:  { hpBonus: 8, atkPercent: 3 },
        rare:      { hpBonus: 12, atkPercent: 5, apBonus: 3 },
        epic:      { hpBonus: 18, atkPercent: 8, apBonus: 5, energyBonus: 1 },
        legendary: { hpBonus: 25, atkPercent: 12, apBonus: 8, energyBonus: 1 },
    },
    accessory2: {
        common:    { apBonus: 3 },
        uncommon:  { apBonus: 5, defPercent: 3 },
        rare:      { apBonus: 8, defPercent: 5, hpBonus: 8 },
        epic:      { apBonus: 12, defPercent: 8, hpBonus: 15 },
        legendary: { apBonus: 18, defPercent: 12, hpBonus: 22, energyBonus: 1 },
    },
};
