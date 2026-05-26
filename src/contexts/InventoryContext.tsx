// InventoryContext: Manages item operations (add, remove, equip, move)
// All mutations use functional updater to avoid stale closure issues

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
    const result = { success: false };
    updatePlayerData((prev) => {
      if (
        prev.inventory.inventory.currentCapacity >=
        prev.inventory.inventory.maxCapacity
      ) {
        return {};
      }
      result.success = true;
      return {
        inventory: {
          ...prev.inventory,
          inventory: {
            ...prev.inventory.inventory,
            items: [...prev.inventory.inventory.items, item],
            currentCapacity: prev.inventory.inventory.currentCapacity + 1,
          },
        },
      };
    });
    return result.success;
  };

  /**
   * Add item to storage
   * @returns true if successful, false if storage full
   */
  const addItemToStorage = (item: Item): boolean => {
    const result = { success: false };
    updatePlayerData((prev) => {
      if (
        prev.inventory.storage.currentCapacity >=
        prev.inventory.storage.maxCapacity
      ) {
        return {};
      }
      result.success = true;
      return {
        inventory: {
          ...prev.inventory,
          storage: {
            ...prev.inventory.storage,
            items: [...prev.inventory.storage.items, item],
            currentCapacity: prev.inventory.storage.currentCapacity + 1,
          },
        },
      };
    });
    return result.success;
  };

  /**
   * Remove item from inventory
   */
  const removeItemFromInventory = (itemId: string): boolean => {
    const result = { success: false };
    updatePlayerData((prev) => {
      const itemIndex = prev.inventory.inventory.items.findIndex(
        (i) => i.id === itemId,
      );
      if (itemIndex === -1) return {};

      const newItems = [...prev.inventory.inventory.items];
      newItems.splice(itemIndex, 1);

      result.success = true;
      return {
        inventory: {
          ...prev.inventory,
          inventory: {
            ...prev.inventory.inventory,
            items: newItems,
            currentCapacity: prev.inventory.inventory.currentCapacity - 1,
          },
        },
      };
    });
    return result.success;
  };

  /**
   * Remove item from storage
   */
  const removeItemFromStorage = (itemId: string): boolean => {
    const result = { success: false };
    updatePlayerData((prev) => {
      const itemIndex = prev.inventory.storage.items.findIndex(
        (i) => i.id === itemId,
      );
      if (itemIndex === -1) return {};

      const newItems = [...prev.inventory.storage.items];
      newItems.splice(itemIndex, 1);

      result.success = true;
      return {
        inventory: {
          ...prev.inventory,
          storage: {
            ...prev.inventory.storage,
            items: newItems,
            currentCapacity: prev.inventory.storage.currentCapacity - 1,
          },
        },
      };
    });
    return result.success;
  };

  /**
   * Add item to equipment inventory (equipment only)
   * @returns true if successful, false if full or not equipment type
   */
  const addItemToEquipmentInventory = (item: Item): boolean => {
    if (item.itemType !== "equipment") {
      return false;
    }
    const result = { success: false };
    updatePlayerData((prev) => {
      if (
        prev.inventory.equipmentInventory.currentCapacity >=
        prev.inventory.equipmentInventory.maxCapacity
      ) {
        return {};
      }
      result.success = true;
      return {
        inventory: {
          ...prev.inventory,
          equipmentInventory: {
            ...prev.inventory.equipmentInventory,
            items: [...prev.inventory.equipmentInventory.items, item],
            currentCapacity:
              prev.inventory.equipmentInventory.currentCapacity + 1,
          },
        },
      };
    });
    return result.success;
  };

  /**
   * Remove item from equipment inventory
   */
  const removeItemFromEquipmentInventory = (itemId: string): boolean => {
    const result = { success: false };
    updatePlayerData((prev) => {
      const itemIndex = prev.inventory.equipmentInventory.items.findIndex(
        (i) => i.id === itemId,
      );
      if (itemIndex === -1) return {};

      const newItems = [...prev.inventory.equipmentInventory.items];
      newItems.splice(itemIndex, 1);

      result.success = true;
      return {
        inventory: {
          ...prev.inventory,
          equipmentInventory: {
            ...prev.inventory.equipmentInventory,
            items: newItems,
            currentCapacity:
              prev.inventory.equipmentInventory.currentCapacity - 1,
          },
        },
      };
    });
    return result.success;
  };

  /**
   * Equip item from inventory or storage
   * Uses a single updatePlayerData call to avoid state race conditions
   */
  const equipItem = (itemId: string, slot: EquipmentSlot): MoveResult => {
    const result: MoveResult = {
      success: false,
      message: "Item not found in Equipment's Inventory or storage",
    };
    updatePlayerData((prev) => {
      // Determine the source of the item
      const fromStorage = prev.inventory.storage.items.find((i) => i.id === itemId);
      const fromEquipmentInventory = !fromStorage
        ? prev.inventory.equipmentInventory.items.find((i) => i.id === itemId)
        : undefined;
      const equipmentItem = fromStorage || fromEquipmentInventory;
      if (!equipmentItem) return {};

      if (equipmentItem.equipmentSlot !== slot) {
        result.message = `Cannot equip ${equipmentItem.name} in ${slot} slot`;
        return {};
      }

      const currentlyEquipped = prev.inventory.equipmentSlots[slot];

      // Remove the item from its source
      const newStorageItems = fromStorage
        ? prev.inventory.storage.items.filter((i) => i.id !== itemId)
        : prev.inventory.storage.items;

      const newEquipmentInventoryItems = fromEquipmentInventory
        ? prev.inventory.equipmentInventory.items.filter((i) => i.id !== itemId)
        : prev.inventory.equipmentInventory.items;

      // If swapping, add old equipped item back to storage
      const finalStorageItems = currentlyEquipped
        ? [...newStorageItems, currentlyEquipped]
        : newStorageItems;

      result.success = true;
      result.message = `Equipped ${equipmentItem.name}, replaced ${
        currentlyEquipped?.name || "nothing"
      }`;
      result.movedItem = equipmentItem;
      result.replacedItem = currentlyEquipped || undefined;

      return {
        inventory: {
          ...prev.inventory,
          equipmentSlots: {
            ...prev.inventory.equipmentSlots,
            [slot]: equipmentItem,
          },
          storage: {
            ...prev.inventory.storage,
            items: finalStorageItems,
            currentCapacity: finalStorageItems.length,
          },
          equipmentInventory: {
            ...prev.inventory.equipmentInventory,
            items: newEquipmentInventoryItems,
            currentCapacity: newEquipmentInventoryItems.length,
          },
        },
      };
    });
    return result;
  };

  /**
   * Unequip item and move to inventory
   */
  const unequipItem = (slot: EquipmentSlot): MoveResult => {
    const result: MoveResult = {
      success: false,
      message: `No item equipped in ${slot} slot`,
    };
    updatePlayerData((prev) => {
      const item = prev.inventory.equipmentSlots[slot];
      if (!item) return {};

      if (
        prev.inventory.storage.currentCapacity >=
        prev.inventory.storage.maxCapacity
      ) {
        result.message = "Storage is full";
        return {};
      }

      result.success = true;
      result.message = `Unequipped ${item.name}`;
      result.movedItem = item;

      return {
        inventory: {
          ...prev.inventory,
          equipmentSlots: {
            ...prev.inventory.equipmentSlots,
            [slot]: null,
          },
          storage: {
            ...prev.inventory.storage,
            items: [...prev.inventory.storage.items, item],
            currentCapacity: prev.inventory.storage.currentCapacity + 1,
          },
        },
      };
    });
    return result;
  };

  /**
   * Move item between storage/inventory/equipment
   */
  const moveItem = (itemId: string, direction: MoveDirection): MoveResult => {
    const result: MoveResult = {
      success: false,
      message: "Unknown error",
    };

    switch (direction) {
      case "storage_to_inventory": {
        updatePlayerData((prev) => {
          const item = prev.inventory.storage.items.find(
            (i) => i.id === itemId,
          );
          if (!item) {
            result.message = "Item not found in storage";
            return {};
          }

          const newStorageItems = prev.inventory.storage.items.filter(
            (i) => i.id !== itemId,
          );
          const newStorage = {
            ...prev.inventory.storage,
            items: newStorageItems,
            currentCapacity: prev.inventory.storage.currentCapacity - 1,
          };

          // Equipment items go to equipmentInventory
          if (item.itemType === "equipment") {
            const eqInv = prev.inventory.equipmentInventory;
            if (eqInv.currentCapacity >= eqInv.maxCapacity) {
              result.message = "Equipment inventory is full";
              return {};
            }
            result.success = true;
            result.message = `Moved ${item.name} to equipment inventory`;
            result.movedItem = item;
            return {
              inventory: {
                ...prev.inventory,
                storage: newStorage,
                equipmentInventory: {
                  ...eqInv,
                  items: [...eqInv.items, item],
                  currentCapacity: eqInv.currentCapacity + 1,
                },
              },
            };
          }

          // Normal items go to inventory
          const inv = prev.inventory.inventory;
          if (inv.currentCapacity >= inv.maxCapacity) {
            result.message = "Inventory is full";
            return {};
          }
          result.success = true;
          result.message = `Moved ${item.name} to inventory`;
          result.movedItem = item;
          return {
            inventory: {
              ...prev.inventory,
              storage: newStorage,
              inventory: {
                ...inv,
                items: [...inv.items, item],
                currentCapacity: inv.currentCapacity + 1,
              },
            },
          };
        });
        return result;
      }

      case "inventory_to_storage": {
        updatePlayerData((prev) => {
          const inventoryItem = prev.inventory.inventory.items.find(
            (i) => i.id === itemId,
          );
          if (!inventoryItem) {
            result.message = "Item not found in inventory";
            return {};
          }
          if (
            prev.inventory.storage.currentCapacity >=
            prev.inventory.storage.maxCapacity
          ) {
            result.message = "Storage is full";
            return {};
          }
          result.success = true;
          result.message = `Moved ${inventoryItem.name} to storage`;
          result.movedItem = inventoryItem;
          return {
            inventory: {
              ...prev.inventory,
              inventory: {
                ...prev.inventory.inventory,
                items: prev.inventory.inventory.items.filter(
                  (i) => i.id !== itemId,
                ),
                currentCapacity:
                  prev.inventory.inventory.currentCapacity - 1,
              },
              storage: {
                ...prev.inventory.storage,
                items: [...prev.inventory.storage.items, inventoryItem],
                currentCapacity:
                  prev.inventory.storage.currentCapacity + 1,
              },
            },
          };
        });
        return result;
      }

      case "storage_to_equipment": {
        // Delegate to equipItem which already uses functional updater
        // But we need to find the item first from fresh state
        updatePlayerData((prev) => {
          const item = prev.inventory.storage.items.find(
            (i) => i.id === itemId,
          );
          if (!item || !item.equipmentSlot) {
            result.message = "Item not found or is not equipment";
            return {};
          }

          const slot = item.equipmentSlot;
          const currentlyEquipped = prev.inventory.equipmentSlots[slot];
          const newStorageItems = prev.inventory.storage.items.filter(
            (i) => i.id !== itemId,
          );
          const finalStorageItems = currentlyEquipped
            ? [...newStorageItems, currentlyEquipped]
            : newStorageItems;

          result.success = true;
          result.message = `Equipped ${item.name}`;
          result.movedItem = item;
          result.replacedItem = currentlyEquipped || undefined;

          return {
            inventory: {
              ...prev.inventory,
              equipmentSlots: {
                ...prev.inventory.equipmentSlots,
                [slot]: item,
              },
              storage: {
                ...prev.inventory.storage,
                items: finalStorageItems,
                currentCapacity: finalStorageItems.length,
              },
            },
          };
        });
        return result;
      }

      case "equipment_to_storage": {
        updatePlayerData((prev) => {
          const slot = Object.entries(prev.inventory.equipmentSlots).find(
            ([, eqItem]) => eqItem?.id === itemId,
          )?.[0] as EquipmentSlot | undefined;
          if (!slot) {
            result.message = "Item not found in equipment";
            return {};
          }
          const item = prev.inventory.equipmentSlots[slot];
          if (!item) {
            result.message = "Item not found";
            return {};
          }
          if (
            prev.inventory.storage.currentCapacity >=
            prev.inventory.storage.maxCapacity
          ) {
            result.message = "Storage is full";
            return {};
          }
          result.success = true;
          result.message = `Moved ${item.name} to storage`;
          result.movedItem = item;
          return {
            inventory: {
              ...prev.inventory,
              equipmentSlots: {
                ...prev.inventory.equipmentSlots,
                [slot]: null,
              },
              storage: {
                ...prev.inventory.storage,
                items: [...prev.inventory.storage.items, item],
                currentCapacity:
                  prev.inventory.storage.currentCapacity + 1,
              },
            },
          };
        });
        return result;
      }

      case "storage_to_equipment_inventory": {
        updatePlayerData((prev) => {
          const item = prev.inventory.storage.items.find(
            (i) => i.id === itemId,
          );
          if (!item) {
            result.message = "Item not found in storage";
            return {};
          }
          if (item.itemType !== "equipment") {
            result.message =
              "Only equipment items can be added to equipment inventory";
            return {};
          }
          if (
            prev.inventory.equipmentInventory.currentCapacity >=
            prev.inventory.equipmentInventory.maxCapacity
          ) {
            result.message = "Equipment inventory is full";
            return {};
          }
          result.success = true;
          result.message = `Moved ${item.name} to equipment inventory`;
          result.movedItem = item;
          return {
            inventory: {
              ...prev.inventory,
              storage: {
                ...prev.inventory.storage,
                items: prev.inventory.storage.items.filter(
                  (i) => i.id !== itemId,
                ),
                currentCapacity:
                  prev.inventory.storage.currentCapacity - 1,
              },
              equipmentInventory: {
                ...prev.inventory.equipmentInventory,
                items: [
                  ...prev.inventory.equipmentInventory.items,
                  item,
                ],
                currentCapacity:
                  prev.inventory.equipmentInventory.currentCapacity + 1,
              },
            },
          };
        });
        return result;
      }

      case "equipment_inventory_to_storage": {
        updatePlayerData((prev) => {
          const item = prev.inventory.equipmentInventory.items.find(
            (i) => i.id === itemId,
          );
          if (!item) {
            result.message = "Item not found in equipment inventory";
            return {};
          }
          if (
            prev.inventory.storage.currentCapacity >=
            prev.inventory.storage.maxCapacity
          ) {
            result.message = "Storage is full";
            return {};
          }
          result.success = true;
          result.message = `Moved ${item.name} to storage`;
          result.movedItem = item;
          return {
            inventory: {
              ...prev.inventory,
              equipmentInventory: {
                ...prev.inventory.equipmentInventory,
                items: prev.inventory.equipmentInventory.items.filter(
                  (i) => i.id !== itemId,
                ),
                currentCapacity:
                  prev.inventory.equipmentInventory.currentCapacity - 1,
              },
              storage: {
                ...prev.inventory.storage,
                items: [...prev.inventory.storage.items, item],
                currentCapacity:
                  prev.inventory.storage.currentCapacity + 1,
              },
            },
          };
        });
        return result;
      }

      case "equipment_inventory_to_equipment": {
        updatePlayerData((prev) => {
          const item = prev.inventory.equipmentInventory.items.find(
            (i) => i.id === itemId,
          );
          if (!item || !item.equipmentSlot) {
            result.message = "Item not found or is not equipment";
            return {};
          }

          const slot = item.equipmentSlot;
          const currentlyEquipped = prev.inventory.equipmentSlots[slot];

          const newEquipmentInventoryItems =
            prev.inventory.equipmentInventory.items.filter(
              (i) => i.id !== itemId,
            );

          result.success = true;
          result.message = `Equipped ${item.name}`;
          result.movedItem = item;
          result.replacedItem = currentlyEquipped || undefined;

          return {
            inventory: {
              ...prev.inventory,
              equipmentSlots: {
                ...prev.inventory.equipmentSlots,
                [slot]: item,
              },
              equipmentInventory: {
                ...prev.inventory.equipmentInventory,
                items: currentlyEquipped
                  ? [...newEquipmentInventoryItems, currentlyEquipped]
                  : newEquipmentInventoryItems,
                currentCapacity: currentlyEquipped
                  ? prev.inventory.equipmentInventory.currentCapacity
                  : prev.inventory.equipmentInventory.currentCapacity - 1,
              },
            },
          };
        });
        return result;
      }

      case "equipment_to_equipment_inventory": {
        updatePlayerData((prev) => {
          const slot = Object.entries(prev.inventory.equipmentSlots).find(
            ([, eqItem]) => eqItem?.id === itemId,
          )?.[0] as EquipmentSlot | undefined;

          if (!slot) {
            result.message = "Item not found in equipment";
            return {};
          }

          const item = prev.inventory.equipmentSlots[slot];
          if (!item) {
            result.message = "Item not found";
            return {};
          }

          if (
            prev.inventory.equipmentInventory.currentCapacity >=
            prev.inventory.equipmentInventory.maxCapacity
          ) {
            result.message = "Equipment inventory is full";
            return {};
          }

          result.success = true;
          result.message = `Moved ${item.name} to equipment inventory`;
          result.movedItem = item;

          return {
            inventory: {
              ...prev.inventory,
              equipmentSlots: {
                ...prev.inventory.equipmentSlots,
                [slot]: null,
              },
              equipmentInventory: {
                ...prev.inventory.equipmentInventory,
                items: [
                  ...prev.inventory.equipmentInventory.items,
                  item,
                ],
                currentCapacity:
                  prev.inventory.equipmentInventory.currentCapacity + 1,
              },
            },
          };
        });
        return result;
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
// eslint-disable-next-line react-refresh/only-export-components
export const useInventory = () => {
  const context = useContext(InventoryContext);
  if (!context) {
    throw new Error("useInventory must be used within InventoryProvider");
  }
  return context;
};
