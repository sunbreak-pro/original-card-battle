// Storage and Inventory type definitions

import type { Item } from "../../item_equipment/type/ItemTypes";
import type { EquipmentSlot } from "../../item_equipment/type/EquipmentType";

/**
 * Storage state
 * Items stored at BaseCamp (safe from death penalty)
 */
export interface StorageState {
  items: Item[]; // List of items in storage
  maxCapacity: number; // Max capacity (Phase 1: 100)
  currentCapacity: number; // Current usage (items.length)
}

/**
 * Inventory state
 * Items carried during exploration (lost on death)
 */
export interface InventoryState {
  items: Item[]; // List of items in inventory
  maxCapacity: number; // Max capacity (Phase 1: 20)
  currentCapacity: number; // Current usage (items.length)
}

/**
 * Equipment Inventory state
 * Specialized inventory for equipment items only (lost on death)
 * Used during dungeon exploration for quick equipment swaps
 */
export interface EquipmentInventoryState {
  items: Item[]; // List of equipment items only
  maxCapacity: number; // Max capacity (Phase 1: 3)
  currentCapacity: number; // Current usage (items.length)
}

/**
 * Equipment slots
 * Currently equipped items (lost on death)
 */
export interface EquipmentSlots {
  weapon: Item | null;
  armor: Item | null;
  helmet: Item | null;
  boots: Item | null;
  accessory1: Item | null;
  accessory2: Item | null;
}

/**
 * Direction of item movement
 */
export type MoveDirection =
  | "storage_to_inventory"
  | "inventory_to_storage"
  | "storage_to_equipment"
  | "equipment_to_storage"
  | "equipSlotItem_to_storage"
  | "storage_to_equipment_inventory"
  | "equipment_inventory_to_storage"
  | "equipment_inventory_to_equipment"
  | "equipment_to_equipment_inventory";

/**
 * Result of an item move operation
 */
export interface MoveResult {
  success: boolean;
  message: string;
  movedItem?: Item;
  replacedItem?: Item; // Old item when swapping equipment
}

/**
 * Equipment set (for save/load functionality)
 */
export interface EquipmentSet {
  id: string;
  name: string;
  equipment: {
    weapon?: string; // Item ID
    armor?: string;
    helmet?: string;
    boots?: string;
    accessory1?: string;
    accessory2?: string;
  };
}

/**
 * Item filter options
 */
export interface ItemFilter {
  itemType?: "equipment" | "consumable" | "magicStone" | "material";
  rarity?: "common" | "uncommon" | "rare" | "epic" | "legendary";
  equipmentSlot?: EquipmentSlot;
  searchTerm?: string;
}

/**
 * Item sort options
 */
export type ItemSortCriteria =
  | "rarity" // Legendary → Common
  | "level" // High → Low
  | "name" // Alphabetical
  | "type" // weapon → armor → helmet → ...
  | "recent"; // Newest → Oldest

/**
 * Helper function to check if storage has space
 */
export function hasStorageSpace(storage: StorageState, count = 1): boolean {
  return storage.currentCapacity + count <= storage.maxCapacity;
}

/**
 * Helper function to check if inventory has space
 */
export function hasInventorySpace(inventory: InventoryState, count = 1): boolean {
  return inventory.currentCapacity + count <= inventory.maxCapacity;
}

/**
 * Helper function to check if equipment inventory has space
 */
export function hasEquipmentInventorySpace(equipmentInventory: EquipmentInventoryState, count = 1): boolean {
  return equipmentInventory.currentCapacity + count <= equipmentInventory.maxCapacity;
}

/**
 * Helper function to get equipped item by slot
 */
export function getEquippedItem(
  equipment: EquipmentSlots,
  slot: EquipmentSlot
): Item | null {
  return equipment[slot];
}

/**
 * Helper function to check if a slot is occupied
 */
export function isSlotOccupied(
  equipment: EquipmentSlots,
  slot: EquipmentSlot
): boolean {
  return equipment[slot] !== null;
}
