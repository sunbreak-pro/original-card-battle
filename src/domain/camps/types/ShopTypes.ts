// Shop type definitions for the Merchant's Exchange

import type { ItemRarity } from "../../item_equipment/type/ItemTypes";
import type { EquipmentSlot } from "../../item_equipment/type/EquipmentType";

/**
 * Shop tab types
 */
export type ShopTab = "buy" | "sell" | "exchange";

/**
 * Shop item category
 */
export type ShopCategory = "consumable" | "teleport" | "equipmentPack";

/**
 * Shop item definition (catalog entry, not inventory instance)
 */
export interface ShopItem {
  id: string;
  name: string;
  description: string;
  icon: string;
  category: ShopCategory;
  price: number;
  stock?: number; // undefined = infinite

  // Consumable-specific
  healAmount?: number;

  // Teleport-specific
  returnChance?: number; // 0.6 = 60%

  // Equipment pack-specific
  packType?: EquipmentPackType;
  guaranteedRarity?: ItemRarity;
}

/**
 * Equipment pack types
 */
export type EquipmentPackType = "common" | "rare" | "epic";

/**
 * Equipment pack configuration
 */
export interface EquipmentPackConfig {
  id: string;
  name: string;
  description: string;
  icon: string;
  price: number;
  packType: EquipmentPackType;
  guaranteedRarity: ItemRarity;
  itemCount: number; // Always 6 (one per slot)
  rarityProbabilities: RarityProbability;
}

/**
 * Rarity probability distribution for equipment packs
 */
export interface RarityProbability {
  common: number;
  uncommon: number;
  rare: number;
  epic: number;
  legendary: number;
}

/**
 * Equipment generated from pack
 */
export interface GeneratedEquipment {
  slot: EquipmentSlot;
  rarity: ItemRarity;
  name: string;
  typeId: string;
}

/**
 * Daily sale configuration (deferred - for future implementation)
 */
export interface DailySale {
  itemId: string;
  discountPercent: number; // 10-30
  originalPrice: number;
  salePrice: number;
  expiresAt: number; // timestamp
}

/**
 * Transaction result
 */
export interface TransactionResult {
  success: boolean;
  message: string;
  goldChange?: number;
  itemsReceived?: string[]; // item IDs
}
