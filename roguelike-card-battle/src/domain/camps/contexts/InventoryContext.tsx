// InventoryContext: Manages item operations (add, remove, equip, move)

import React, { createContext, useContext, type ReactNode } from "react";
import { usePlayer } from "./PlayerContext";
import type { Item, EquipmentSlot } from "../types/ItemTypes";
import type { MoveDirection, MoveResult } from "../types/StorageTypes";

/**
 * InventoryContext value
 */
interface InventoryContextValue {
  // Item operations
  addItemToInventory: (item: Item) => boolean;
  addItemToStorage: (item: Item) => boolean;
  removeItemFromInventory: (itemId: string) => boolean;
  removeItemFromStorage: (itemId: string) => boolean;

  // Equipment operations
  equipItem: (itemId: string, slot: EquipmentSlot) => MoveResult;
  unequipItem: (slot: EquipmentSlot) => MoveResult;

  // Item movement
  moveItem: (itemId: string, direction: MoveDirection) => MoveResult;

  // Getters
  getInventoryItem: (itemId: string) => Item | undefined;
  getStorageItem: (itemId: string) => Item | undefined;
  getEquippedItem: (slot: EquipmentSlot) => Item | null;
}

const InventoryContext = createContext<InventoryContextValue | undefined>(
  undefined
);

/**
 * InventoryProvider Component
 */
export const InventoryProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const { player, updatePlayer } = usePlayer();

  /**
   * Add item to inventory
   * @returns true if successful, false if inventory full
   */
  const addItemToInventory = (item: Item): boolean => {
    if (player.inventory.currentCapacity >= player.inventory.maxCapacity) {
      return false;
    }

    updatePlayer({
      inventory: {
        ...player.inventory,
        items: [...player.inventory.items, item],
        currentCapacity: player.inventory.currentCapacity + 1,
      },
    });
    return true;
  };

  /**
   * Add item to storage
   * @returns true if successful, false if storage full
   */
  const addItemToStorage = (item: Item): boolean => {
    if (player.storage.currentCapacity >= player.storage.maxCapacity) {
      return false;
    }

    updatePlayer({
      storage: {
        ...player.storage,
        items: [...player.storage.items, item],
        currentCapacity: player.storage.currentCapacity + 1,
      },
    });
    return true;
  };

  /**
   * Remove item from inventory
   */
  const removeItemFromInventory = (itemId: string): boolean => {
    const itemIndex = player.inventory.items.findIndex((i) => i.id === itemId);
    if (itemIndex === -1) return false;

    const newItems = [...player.inventory.items];
    newItems.splice(itemIndex, 1);

    updatePlayer({
      inventory: {
        ...player.inventory,
        items: newItems,
        currentCapacity: player.inventory.currentCapacity - 1,
      },
    });
    return true;
  };

  /**
   * Remove item from storage
   */
  const removeItemFromStorage = (itemId: string): boolean => {
    const itemIndex = player.storage.items.findIndex((i) => i.id === itemId);
    if (itemIndex === -1) return false;

    const newItems = [...player.storage.items];
    newItems.splice(itemIndex, 1);

    updatePlayer({
      storage: {
        ...player.storage,
        items: newItems,
        currentCapacity: player.storage.currentCapacity - 1,
      },
    });
    return true;
  };

  /**
   * Equip item from inventory or storage
   * Uses a single updatePlayer call to avoid state race conditions
   */
  const equipItem = (itemId: string, slot: EquipmentSlot): MoveResult => {
    // Try to find item in inventory first
    let item = player.inventory.items.find((i) => i.id === itemId);
    let fromInventory = true;

    // If not in inventory, try storage
    if (!item) {
      item = player.storage.items.find((i) => i.id === itemId);
      fromInventory = false;
    }

    if (!item) {
      return {
        success: false,
        message: "Item not found in inventory or storage",
      };
    }

    // Check if item can be equipped in this slot
    if (item.equipmentSlot !== slot) {
      return {
        success: false,
        message: `Cannot equip ${item.name} in ${slot} slot`,
      };
    }

    // Get currently equipped item (if any)
    const currentlyEquipped = player.equipmentSlots[slot];

    // Build all updates in a single object to avoid race conditions
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const updates: Record<string, any> = {
      equipmentSlots: { ...player.equipmentSlots, [slot]: item },
    };

    if (fromInventory) {
      // Remove item from inventory
      const newInventoryItems = player.inventory.items.filter(
        (i) => i.id !== itemId
      );

      // If swapping, add old equipped item to inventory
      if (currentlyEquipped) {
        newInventoryItems.push(currentlyEquipped);
      }

      updates.inventory = {
        ...player.inventory,
        items: newInventoryItems,
        currentCapacity: newInventoryItems.length,
      };
    } else {
      // Remove item from storage
      const newStorageItems = player.storage.items.filter(
        (i) => i.id !== itemId
      );
      updates.storage = {
        ...player.storage,
        items: newStorageItems,
        currentCapacity: newStorageItems.length,
      };

      // If swapping, add old equipped item to inventory
      if (currentlyEquipped) {
        updates.inventory = {
          ...player.inventory,
          items: [...player.inventory.items, currentlyEquipped],
          currentCapacity: player.inventory.currentCapacity + 1,
        };
      }
    }

    // Single updatePlayer call with all changes
    updatePlayer(updates);

    return {
      success: true,
      message: `Equipped ${item.name}`,
      movedItem: item,
      replacedItem: currentlyEquipped || undefined,
    };
  };

  /**
   * Unequip item and move to inventory
   */
  const unequipItem = (slot: EquipmentSlot): MoveResult => {
    const item = player.equipmentSlots[slot];

    if (!item) {
      return {
        success: false,
        message: `No item equipped in ${slot} slot`,
      };
    }

    // Check if inventory has space
    if (player.inventory.currentCapacity >= player.inventory.maxCapacity) {
      return {
        success: false,
        message: "Inventory is full",
      };
    }

    // Remove from equipment slot
    const newEquipmentSlots = { ...player.equipmentSlots, [slot]: null };

    updatePlayer({
      equipmentSlots: newEquipmentSlots,
    });

    // Add to inventory
    addItemToInventory(item);

    return {
      success: true,
      message: `Unequipped ${item.name}`,
      movedItem: item,
    };
  };

  /**
   * Move item between storage/inventory/equipment
   */
  const moveItem = (itemId: string, direction: MoveDirection): MoveResult => {
    switch (direction) {
      case "storage_to_inventory": {
        const item = player.storage.items.find((i) => i.id === itemId);
        if (!item) {
          return { success: false, message: "Item not found in storage" };
        }
        if (player.inventory.currentCapacity >= player.inventory.maxCapacity) {
          return { success: false, message: "Inventory is full" };
        }
        removeItemFromStorage(itemId);
        addItemToInventory(item);
        return {
          success: true,
          message: `Moved ${item.name} to inventory`,
          movedItem: item,
        };
      }

      case "inventory_to_storage": {
        const item = player.inventory.items.find((i) => i.id === itemId);
        if (!item) {
          return { success: false, message: "Item not found in inventory" };
        }
        if (player.storage.currentCapacity >= player.storage.maxCapacity) {
          return { success: false, message: "Storage is full" };
        }
        removeItemFromInventory(itemId);
        addItemToStorage(item);
        return {
          success: true,
          message: `Moved ${item.name} to storage`,
          movedItem: item,
        };
      }

      case "storage_to_equipment":
      case "inventory_to_equipment": {
        // Get item
        const item =
          direction === "storage_to_equipment"
            ? player.storage.items.find((i) => i.id === itemId)
            : player.inventory.items.find((i) => i.id === itemId);

        if (!item || !item.equipmentSlot) {
          return {
            success: false,
            message: "Item not found or is not equipment",
          };
        }

        return equipItem(itemId, item.equipmentSlot);
      }

      case "equipment_to_storage":
      case "equipment_to_inventory": {
        // Find which slot the item is in
        const slot = Object.entries(player.equipmentSlots).find(
          ([_, item]) => item?.id === itemId
        )?.[0] as EquipmentSlot | undefined;

        if (!slot) {
          return { success: false, message: "Item not found in equipment" };
        }

        const item = player.equipmentSlots[slot];
        if (!item) {
          return { success: false, message: "Item not found" };
        }

        // Unequip first
        const newEquipmentSlots = { ...player.equipmentSlots, [slot]: null };
        updatePlayer({ equipmentSlots: newEquipmentSlots });

        // Then add to destination
        if (direction === "equipment_to_storage") {
          const success = addItemToStorage(item);
          return success
            ? {
                success: true,
                message: `Moved ${item.name} to storage`,
                movedItem: item,
              }
            : { success: false, message: "Storage is full" };
        } else {
          const success = addItemToInventory(item);
          return success
            ? {
                success: true,
                message: `Moved ${item.name} to inventory`,
                movedItem: item,
              }
            : { success: false, message: "Inventory is full" };
        }
      }

      default:
        return { success: false, message: "Unknown move direction" };
    }
  };

  /**
   * Get item from inventory by ID
   */
  const getInventoryItem = (itemId: string): Item | undefined => {
    return player.inventory.items.find((i) => i.id === itemId);
  };

  /**
   * Get item from storage by ID
   */
  const getStorageItem = (itemId: string): Item | undefined => {
    return player.storage.items.find((i) => i.id === itemId);
  };

  /**
   * Get equipped item by slot
   */
  const getEquippedItem = (slot: EquipmentSlot): Item | null => {
    return player.equipmentSlots[slot];
  };

  return (
    <InventoryContext.Provider
      value={{
        addItemToInventory,
        addItemToStorage,
        removeItemFromInventory,
        removeItemFromStorage,
        equipItem,
        unequipItem,
        moveItem,
        getInventoryItem,
        getStorageItem,
        getEquippedItem,
      }}
    >
      {children}
    </InventoryContext.Provider>
  );
};

/**
 * Hook to use Inventory context
 */
export const useInventory = () => {
  const context = useContext(InventoryContext);
  if (!context) {
    throw new Error("useInventory must be used within InventoryProvider");
  }
  return context;
};
