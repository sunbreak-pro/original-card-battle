import type { ItemRarity } from "@/types/itemTypes";
import type { EquipmentSlot } from "@/types/itemTypes";
export const EquipmentData = {
}
export const EQUIPMENT_TEMPLATES: Record<
    EquipmentSlot,
    Record<ItemRarity, { name: string; icon: string }>
> = {
    weapon: {
        common: { name: "Iron Sword", icon: "🗡️" },
        uncommon: { name: "Steel Blade", icon: "⚔️" },
        rare: { name: "Enchanted Sword", icon: "🔮" },
        epic: { name: "Demon Slayer", icon: "⚔️" },
        legendary: { name: "Excalibur", icon: "✨" },
    },
    armor: {
        common: { name: "Leather Armor", icon: "🛡️" },
        uncommon: { name: "Chain Mail", icon: "🔗" },
        rare: { name: "Plate Armor", icon: "🛡️" },
        epic: { name: "Dragon Scale Armor", icon: "🐉" },
        legendary: { name: "Celestial Plate", icon: "✨" },
    },
    helmet: {
        common: { name: "Leather Cap", icon: "🎓" },
        uncommon: { name: "Iron Helm", icon: "⛑️" },
        rare: { name: "Enchanted Helm", icon: "👑" },
        epic: { name: "Crown of Thorns", icon: "👑" },
        legendary: { name: "Crown of the Ancients", icon: "✨" },
    },
    boots: {
        common: { name: "Leather Boots", icon: "👢" },
        uncommon: { name: "Iron Greaves", icon: "🦶" },
        rare: { name: "Swift Boots", icon: "💨" },
        epic: { name: "Shadow Steps", icon: "👣" },
        legendary: { name: "Wings of Mercury", icon: "✨" },
    },
    accessory1: {
        common: { name: "Copper Ring", icon: "💍" },
        uncommon: { name: "Silver Ring", icon: "💎" },
        rare: { name: "Enchanted Ring", icon: "✨" },
        epic: { name: "Ring of Power", icon: "🔥" },
        legendary: { name: "Ring of the Void", icon: "🌀" },
    },
    accessory2: {
        common: { name: "Bone Amulet", icon: "📿" },
        uncommon: { name: "Silver Amulet", icon: "🏵️" },
        rare: { name: "Enchanted Amulet", icon: "💫" },
        epic: { name: "Dragon Heart Amulet", icon: "❤️" },
        legendary: { name: "Amulet of Eternity", icon: "♾️" },
    },
};