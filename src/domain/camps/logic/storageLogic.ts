/**
 * Storage Logic Functions
 *
 * Helper functions for storage and inventory management.
 */

import type { StorageState, InventoryState, EquipmentInventoryState, EquipmentSlots } from '@/types/campTypes';
import type { Item } from '@/types/itemTypes';
import type { EquipmentSlot } from '@/types/itemTypes';

/**
 * Check if storage has space
 */
export function hasStorageSpace(storage: StorageState, count = 1): boolean {
  return storage.currentCapacity + count <= storage.maxCapacity;
}

/**
 * Check if inventory has space
 */
export function hasInventorySpace(inventory: InventoryState, count = 1): boolean {
  return inventory.currentCapacity + count <= inventory.maxCapacity;
}

/**
 * Check if equipment inventory has space
 */
export function hasEquipmentInventorySpace(equipmentInventory: EquipmentInventoryState, count = 1): boolean {
  return equipmentInventory.currentCapacity + count <= equipmentInventory.maxCapacity;
}

/**
 * Get equipped item by slot
 */
export function getEquippedItem(
  equipment: EquipmentSlots,
  slot: EquipmentSlot
): Item | null {
  return equipment[slot];
}

/**
 * Check if a slot is occupied
 */
export function isSlotOccupied(
  equipment: EquipmentSlots,
  slot: EquipmentSlot
): boolean {
  return equipment[slot] !== null;
}
