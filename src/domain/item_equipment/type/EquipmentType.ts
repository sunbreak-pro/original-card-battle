import type { ItemRarity } from "./ItemTypes";
export type EquipmentSlot =
    | "weapon"
    | "armor"
    | "helmet"
    | "boots"
    | "accessory1"
    | "accessory2";

export type EquipmentQuality = "poor" | "normal" | "good" | "master";

export interface EquipmentEffect {
    type: "stat" | "skill" | "passive";
    target: string; // e.g., "hp", "ap", "attack", "defense"
    value: number | string;
    description: string;
}

export interface EquipmentData {
    id: string; // Unique instance ID
    typeId: string; // Item type ID (for same items)
    name: string;
    description: string;
    itemType: "equipment";
    type: string; // Emoji or image path

    equipmentSlot: EquipmentSlot;
    durability: number; // Current durability (AP in equipment system)
    maxDurability: number; // Max durability
    rarity: ItemRarity;
    quality: EquipmentQuality;
    effects: EquipmentEffect[];
}