// InventoryContext: Manages item operations (add, remove, equip, move)

import React, { createContext, useContext, type ReactNode } from "react";
import { usePlayer } from "./PlayerContext";
import type { Item } from "../../item_equipment/type/ItemTypes";
import type { EquipmentSlot } from "../../item_equipment/type/EquipmentType";
import type { MoveDirection, MoveResult } from "../types/StorageTypes";

/**
 * InventoryContext value
 */
interface InventoryContextValue {
  // Item operations
  addItemToInventory: (item: Item) => boolean;
  addItemToStorage: (item: Item) => boolean;
  addItemToEquipmentInventory: (item: Item) => boolean;
  removeItemFromInventory: (itemId: string) => boolean;
  removeItemFromStorage: (itemId: string) => boolean;
  removeItemFromEquipmentInventory: (itemId: string) => boolean;

  // Equipment operations
  equipItem: (itemId: string, slot: EquipmentSlot) => MoveResult;
  unequipItem: (slot: EquipmentSlot) => MoveResult;

  // Item movement
  moveItem: (itemId: string, direction: MoveDirection) => MoveResult;

  // Getters
  getInventoryItem: (itemId: string) => Item | undefined;
  getStorageItem: (itemId: string) => Item | undefined;
  getEquippedItem: (slot: EquipmentSlot) => Item | null;
  getEquipmentInventoryItem: (itemId: string) => Item | undefined;
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
   * Add item to equipment inventory (equipment only)
   * @returns true if successful, false if full or not equipment type
   */
  const addItemToEquipmentInventory = (item: Item): boolean => {
    if (item.itemType !== "equipment") {
      return false;
    }
    if (
      player.equipmentInventory.currentCapacity >=
      player.equipmentInventory.maxCapacity
    ) {
      return false;
    }

    updatePlayer({
      equipmentInventory: {
        ...player.equipmentInventory,
        items: [...player.equipmentInventory.items, item],
        currentCapacity: player.equipmentInventory.currentCapacity + 1,
      },
    });
    return true;
  };

  /**
   * Remove item from equipment inventory
   */
  const removeItemFromEquipmentInventory = (itemId: string): boolean => {
    const itemIndex = player.equipmentInventory.items.findIndex(
      (i) => i.id === itemId
    );
    if (itemIndex === -1) return false;

    const newItems = [...player.equipmentInventory.items];
    newItems.splice(itemIndex, 1);

    updatePlayer({
      equipmentInventory: {
        ...player.equipmentInventory,
        items: newItems,
        currentCapacity: player.equipmentInventory.currentCapacity - 1,
      },
    });
    return true;
  };

  /**
   * Equip item from inventory or storage
   * Uses a single updatePlayer call to avoid state race conditions
   */
  const equipItem = (itemId: string, slot: EquipmentSlot): MoveResult => {
    // Try to find item in storage first
    const item = player.storage.items.find((i) => i.id === itemId);
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

    // Remove the new equipment from storage
    const newStorageItems = player.storage.items.filter((i) => i.id !== itemId);

    // If swapping, add old equipped item back to storage
    const finalStorageItems = currentlyEquipped
      ? [...newStorageItems, currentlyEquipped]
      : newStorageItems;

    // Single updatePlayer call with all changes to avoid race conditions
    updatePlayer({
      equipmentSlots: { ...player.equipmentSlots, [slot]: item },
      storage: {
        ...player.storage,
        items: finalStorageItems,
        currentCapacity: finalStorageItems.length,
      },
    });

    return {
      success: true,
      message: `Equipped ${item.name}, replaced ${
        currentlyEquipped?.name || "nothing"
      }`,
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
    addItemToStorage(item);

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
        const inventoryItem = player.inventory.items.find(
          (i) => i.id === itemId
        );
        if (!inventoryItem) {
          return { success: false, message: "Item not found in inventory" };
        }
        if (player.storage.currentCapacity >= player.storage.maxCapacity) {
          return { success: false, message: "Storage is full" };
        }
        removeItemFromInventory(itemId);
        addItemToStorage(inventoryItem);
        return {
          success: true,
          message: `Moved ${inventoryItem.name} to storage`,
          movedItem: inventoryItem,
        };
      }

      case "storage_to_equipment": {
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

      case "equipment_to_storage": {
        // Find which slot the item is in
        const slot = Object.entries(player.equipmentSlots).find(
          ([, item]) => item?.id === itemId
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
      case "equipSlotItem_to_storage": {
        const slot = Object.entries(player.equipmentSlots).find(
          ([, item]) => item?.id === itemId
        )?.[0] as EquipmentSlot | undefined;
        const equippedItem = player.equipmentSlots[slot as EquipmentSlot];
        if (!equippedItem) {
          return { success: false, message: "Unequip error" };
        }
        if (player.storage.currentCapacity >= player.storage.maxCapacity) {
          return { success: false, message: "Storage is full" };
        }
        unequipItem(equippedItem.equipmentSlot as EquipmentSlot);
        addItemToStorage(equippedItem);
        return {
          success: true,
          message: `Moved ${equippedItem.name} to storage`,
          movedItem: equippedItem,
        };
      }

      case "storage_to_equipment_inventory": {
        const item = player.storage.items.find((i) => i.id === itemId);
        if (!item) {
          return { success: false, message: "Item not found in storage" };
        }
        if (item.itemType !== "equipment") {
          return {
            success: false,
            message: "Only equipment items can be added to equipment inventory",
          };
        }
        if (
          player.equipmentInventory.currentCapacity >=
          player.equipmentInventory.maxCapacity
        ) {
          return { success: false, message: "Equipment inventory is full" };
        }
        removeItemFromStorage(itemId);
        addItemToEquipmentInventory(item);
        return {
          success: true,
          message: `Moved ${item.name} to equipment inventory`,
          movedItem: item,
        };
      }

      case "equipment_inventory_to_storage": {
        const item = player.equipmentInventory.items.find(
          (i) => i.id === itemId
        );
        if (!item) {
          return {
            success: false,
            message: "Item not found in equipment inventory",
          };
        }
        if (player.storage.currentCapacity >= player.storage.maxCapacity) {
          return { success: false, message: "Storage is full" };
        }
        removeItemFromEquipmentInventory(itemId);
        addItemToStorage(item);
        return {
          success: true,
          message: `Moved ${item.name} to storage`,
          movedItem: item,
        };
      }

      case "equipment_inventory_to_equipment": {
        const item = player.equipmentInventory.items.find(
          (i) => i.id === itemId
        );
        if (!item || !item.equipmentSlot) {
          return {
            success: false,
            message: "Item not found or is not equipment",
          };
        }

        const slot = item.equipmentSlot;
        const currentlyEquipped = player.equipmentSlots[slot];

        // Remove from equipment inventory
        const newEquipmentInventoryItems =
          player.equipmentInventory.items.filter((i) => i.id !== itemId);

        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const updates: Record<string, any> = {
          equipmentSlots: { ...player.equipmentSlots, [slot]: item },
          equipmentInventory: {
            ...player.equipmentInventory,
            items: currentlyEquipped
              ? [...newEquipmentInventoryItems, currentlyEquipped]
              : newEquipmentInventoryItems,
            currentCapacity: currentlyEquipped
              ? player.equipmentInventory.currentCapacity
              : player.equipmentInventory.currentCapacity - 1,
          },
        };

        updatePlayer(updates);

        return {
          success: true,
          message: `Equipped ${item.name}`,
          movedItem: item,
          replacedItem: currentlyEquipped || undefined,
        };
      }

      case "equipment_to_equipment_inventory": {
        // Find which slot the item is in
        const slot = Object.entries(player.equipmentSlots).find(
          ([, item]) => item?.id === itemId
        )?.[0] as EquipmentSlot | undefined;

        if (!slot) {
          return { success: false, message: "Item not found in equipment" };
        }

        const item = player.equipmentSlots[slot];
        if (!item) {
          return { success: false, message: "Item not found" };
        }

        if (
          player.equipmentInventory.currentCapacity >=
          player.equipmentInventory.maxCapacity
        ) {
          return { success: false, message: "Equipment inventory is full" };
        }

        // Unequip and add to equipment inventory
        const newEquipmentSlots = { ...player.equipmentSlots, [slot]: null };
        updatePlayer({ equipmentSlots: newEquipmentSlots });
        addItemToEquipmentInventory(item);

        return {
          success: true,
          message: `Moved ${item.name} to equipment inventory`,
          movedItem: item,
        };
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

  /**
   * Get item from equipment inventory by ID
   */
  const getEquipmentInventoryItem = (itemId: string): Item | undefined => {
    return player.equipmentInventory.items.find((i) => i.id === itemId);
  };

  return (
    <InventoryContext.Provider
      value={{
        addItemToInventory,
        addItemToStorage,
        addItemToEquipmentInventory,
        removeItemFromInventory,
        removeItemFromStorage,
        removeItemFromEquipmentInventory,
        equipItem,
        unequipItem,
        moveItem,
        getInventoryItem,
        getStorageItem,
        getEquippedItem,
        getEquipmentInventoryItem,
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
