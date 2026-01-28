// InventoryContext: Manages item operations (add, remove, equip, move)

import React, { createContext, useContext, type ReactNode } from "react";
import { usePlayer } from "./PlayerContext";
import type { Item } from '@/types/itemTypes';
import type { EquipmentSlot } from '@/types/itemTypes';
import type {
  MoveDirection,
  MoveResult,
} from '@/types/campTypes';

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
  undefined,
);

/**
 * InventoryProvider Component
 */
export const InventoryProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const { playerData, updatePlayerData } = usePlayer();

  /**
   * Add item to inventory
   * @returns true if successful, false if inventory full
   */
  const addItemToInventory = (item: Item): boolean => {
    if (
      playerData.inventory.inventory.currentCapacity >=
      playerData.inventory.inventory.maxCapacity
    ) {
      return false;
    }

    updatePlayerData({
      inventory: {
        ...playerData.inventory,
        inventory: {
          ...playerData.inventory.inventory,
          items: [...playerData.inventory.inventory.items, item],
          currentCapacity: playerData.inventory.inventory.currentCapacity + 1,
        },
      },
    });
    return true;
  };

  /**
   * Add item to storage
   * @returns true if successful, false if storage full
   */
  const addItemToStorage = (item: Item): boolean => {
    if (
      playerData.inventory.storage.currentCapacity >=
      playerData.inventory.storage.maxCapacity
    ) {
      return false;
    }

    updatePlayerData({
      inventory: {
        ...playerData.inventory,
        storage: {
          ...playerData.inventory.storage,
          items: [...playerData.inventory.storage.items, item],
          currentCapacity: playerData.inventory.storage.currentCapacity + 1,
        },
      },
    });
    return true;
  };

  /**
   * Remove item from inventory
   */
  const removeItemFromInventory = (itemId: string): boolean => {
    const itemIndex = playerData.inventory.inventory.items.findIndex(
      (i) => i.id === itemId,
    );
    if (itemIndex === -1) return false;

    const newItems = [...playerData.inventory.inventory.items];
    newItems.splice(itemIndex, 1);

    updatePlayerData({
      inventory: {
        ...playerData.inventory,
        inventory: {
          ...playerData.inventory.inventory,
          items: newItems,
          currentCapacity: playerData.inventory.inventory.currentCapacity - 1,
        },
      },
    });
    return true;
  };

  /**
   * Remove item from storage
   */
  const removeItemFromStorage = (itemId: string): boolean => {
    const itemIndex = playerData.inventory.storage.items.findIndex(
      (i) => i.id === itemId,
    );
    if (itemIndex === -1) return false;

    const newItems = [...playerData.inventory.storage.items];
    newItems.splice(itemIndex, 1);

    updatePlayerData({
      inventory: {
        ...playerData.inventory,
        storage: {
          ...playerData.inventory.storage,
          items: newItems,
          currentCapacity: playerData.inventory.storage.currentCapacity - 1,
        },
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
      playerData.inventory.equipmentInventory.currentCapacity >=
      playerData.inventory.equipmentInventory.maxCapacity
    ) {
      return false;
    }

    updatePlayerData({
      inventory: {
        ...playerData.inventory,
        equipmentInventory: {
          ...playerData.inventory.equipmentInventory,
          items: [...playerData.inventory.equipmentInventory.items, item],
          currentCapacity:
            playerData.inventory.equipmentInventory.currentCapacity + 1,
        },
      },
    });
    return true;
  };

  /**
   * Remove item from equipment inventory
   */
  const removeItemFromEquipmentInventory = (itemId: string): boolean => {
    const itemIndex = playerData.inventory.equipmentInventory.items.findIndex(
      (i) => i.id === itemId,
    );
    if (itemIndex === -1) return false;

    const newItems = [...playerData.inventory.equipmentInventory.items];
    newItems.splice(itemIndex, 1);

    updatePlayerData({
      inventory: {
        ...playerData.inventory,
        equipmentInventory: {
          ...playerData.inventory.equipmentInventory,
          items: newItems,
          currentCapacity:
            playerData.inventory.equipmentInventory.currentCapacity - 1,
        },
      },
    });
    return true;
  };

  /**
   * Equip item from inventory or storage
   * Uses a single updatePlayerData call to avoid state race conditions
   */
  const equipItem = (itemId: string, slot: EquipmentSlot): MoveResult => {
    // Try to find item in storage first
    const equipmentItem =
      playerData.inventory.storage.items.find((i) => i.id === itemId) ||
      playerData.inventory.equipmentInventory.items.find(
        (i) => i.id === itemId,
      );
    if (!equipmentItem) {
      return {
        success: false,
        message: "Item not found in Equipment's Inventory or storage",
      };
    }
    // Check if item can be equipped in this slot
    if (equipmentItem.equipmentSlot !== slot) {
      return {
        success: false,
        message: `Cannot equip ${equipmentItem.name} in ${slot} slot`,
      };
    }

    // Get currently equipped item (if any)
    const currentlyEquipped = playerData.inventory.equipmentSlots[slot];

    // Remove the new equipment from storage
    const newStorageItems = playerData.inventory.storage.items.filter(
      (i) => i.id !== itemId,
    );

    // If swapping, add old equipped item back to storage
    const finalStorageItems = currentlyEquipped
      ? [...newStorageItems, currentlyEquipped]
      : newStorageItems;

    // Single updatePlayerData call with all changes to avoid race conditions
    updatePlayerData({
      inventory: {
        ...playerData.inventory,
        equipmentSlots: {
          ...playerData.inventory.equipmentSlots,
          [slot]: equipmentItem,
        },
        storage: {
          ...playerData.inventory.storage,
          items: finalStorageItems,
          currentCapacity: finalStorageItems.length,
        },
      },
    });

    return {
      success: true,
      message: `Equipped ${equipmentItem.name}, replaced ${
        currentlyEquipped?.name || "nothing"
      }`,
      movedItem: equipmentItem,
      replacedItem: currentlyEquipped || undefined,
    };
  };

  /**
   * Unequip item and move to inventory
   */
  const unequipItem = (slot: EquipmentSlot): MoveResult => {
    const item = playerData.inventory.equipmentSlots[slot];

    if (!item) {
      return {
        success: false,
        message: `No item equipped in ${slot} slot`,
      };
    }

    // Check if inventory has space
    if (
      playerData.inventory.inventory.currentCapacity >=
      playerData.inventory.inventory.maxCapacity
    ) {
      return {
        success: false,
        message: "Inventory is full",
      };
    }

    // Remove from equipment slot
    const newEquipmentSlots = {
      ...playerData.inventory.equipmentSlots,
      [slot]: null,
    };

    updatePlayerData({
      inventory: {
        ...playerData.inventory,
        equipmentSlots: newEquipmentSlots,
      },
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
        const item = playerData.inventory.storage.items.find(
          (i) => i.id === itemId,
        );
        if (!item) {
          return { success: false, message: "Item not found in storage" };
        }
        if (
          playerData.inventory.inventory.currentCapacity >=
          playerData.inventory.inventory.maxCapacity
        ) {
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
        const inventoryItem = playerData.inventory.inventory.items.find(
          (i) => i.id === itemId,
        );
        if (!inventoryItem) {
          return { success: false, message: "Item not found in inventory" };
        }
        if (
          playerData.inventory.storage.currentCapacity >=
          playerData.inventory.storage.maxCapacity
        ) {
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
            ? playerData.inventory.storage.items.find((i) => i.id === itemId)
            : playerData.inventory.inventory.items.find((i) => i.id === itemId);

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
        const slot = Object.entries(playerData.inventory.equipmentSlots).find(
          ([, item]) => item?.id === itemId,
        )?.[0] as EquipmentSlot | undefined;

        if (!slot) {
          return { success: false, message: "Item not found in equipment" };
        }

        const item = playerData.inventory.equipmentSlots[slot];
        if (!item) {
          return { success: false, message: "Item not found" };
        }

        // Unequip first
        const newEquipmentSlots = {
          ...playerData.inventory.equipmentSlots,
          [slot]: null,
        };
        updatePlayerData({
          inventory: {
            ...playerData.inventory,
            equipmentSlots: newEquipmentSlots,
          },
        });

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
        const slot = Object.entries(playerData.inventory.equipmentSlots).find(
          ([, item]) => item?.id === itemId,
        )?.[0] as EquipmentSlot | undefined;
        const equippedItem =
          playerData.inventory.equipmentSlots[slot as EquipmentSlot];
        if (!equippedItem) {
          return { success: false, message: "Unequip error" };
        }
        if (
          playerData.inventory.storage.currentCapacity >=
          playerData.inventory.storage.maxCapacity
        ) {
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
        const item = playerData.inventory.storage.items.find(
          (i) => i.id === itemId,
        );
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
          playerData.inventory.equipmentInventory.currentCapacity >=
          playerData.inventory.equipmentInventory.maxCapacity
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
        const item = playerData.inventory.equipmentInventory.items.find(
          (i) => i.id === itemId,
        );
        if (!item) {
          return {
            success: false,
            message: "Item not found in equipment inventory",
          };
        }
        if (
          playerData.inventory.storage.currentCapacity >=
          playerData.inventory.storage.maxCapacity
        ) {
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
        const item = playerData.inventory.equipmentInventory.items.find(
          (i) => i.id === itemId,
        );
        if (!item || !item.equipmentSlot) {
          return {
            success: false,
            message: "Item not found or is not equipment",
          };
        }

        const slot = item.equipmentSlot;
        const currentlyEquipped = playerData.inventory.equipmentSlots[slot];

        // Remove from equipment inventory
        const newEquipmentInventoryItems =
          playerData.inventory.equipmentInventory.items.filter(
            (i) => i.id !== itemId,
          );

        updatePlayerData({
          inventory: {
            ...playerData.inventory,
            equipmentSlots: {
              ...playerData.inventory.equipmentSlots,
              [slot]: item,
            },
            equipmentInventory: {
              ...playerData.inventory.equipmentInventory,
              items: currentlyEquipped
                ? [...newEquipmentInventoryItems, currentlyEquipped]
                : newEquipmentInventoryItems,
              currentCapacity: currentlyEquipped
                ? playerData.inventory.equipmentInventory.currentCapacity
                : playerData.inventory.equipmentInventory.currentCapacity - 1,
            },
          },
        });

        return {
          success: true,
          message: `Equipped ${item.name}`,
          movedItem: item,
          replacedItem: currentlyEquipped || undefined,
        };
      }

      case "equipment_to_equipment_inventory": {
        // Find which slot the item is in
        const slot = Object.entries(playerData.inventory.equipmentSlots).find(
          ([, item]) => item?.id === itemId,
        )?.[0] as EquipmentSlot | undefined;

        if (!slot) {
          return { success: false, message: "Item not found in equipment" };
        }

        const item = playerData.inventory.equipmentSlots[slot];
        if (!item) {
          return { success: false, message: "Item not found" };
        }

        if (
          playerData.inventory.equipmentInventory.currentCapacity >=
          playerData.inventory.equipmentInventory.maxCapacity
        ) {
          return { success: false, message: "Equipment inventory is full" };
        }

        // Unequip and add to equipment inventory
        const newEquipmentSlots = {
          ...playerData.inventory.equipmentSlots,
          [slot]: null,
        };
        updatePlayerData({
          inventory: {
            ...playerData.inventory,
            equipmentSlots: newEquipmentSlots,
          },
        });
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
    return playerData.inventory.inventory.items.find((i) => i.id === itemId);
  };

  /**
   * Get item from storage by ID
   */
  const getStorageItem = (itemId: string): Item | undefined => {
    return playerData.inventory.storage.items.find((i) => i.id === itemId);
  };

  /**
   * Get equipped item by slot
   */
  const getEquippedItem = (slot: EquipmentSlot): Item | null => {
    return playerData.inventory.equipmentSlots[slot];
  };

  /**
   * Get item from equipment inventory by ID
   */
  const getEquipmentInventoryItem = (itemId: string): Item | undefined => {
    return playerData.inventory.equipmentInventory.items.find(
      (i) => i.id === itemId,
    );
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
