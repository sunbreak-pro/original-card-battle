# Inventory, Equipment & Items System

## Overview

The inventory system manages three storage areas (inventory for consumables, equipmentInventory for equipment, storage as overflow) plus 6 equipment slots. `InventoryContext` provides CRUD operations and item movement between areas. Equipment has an AP (armor point) system with durability, level upgrades (0-3), and quality tiers (poor/normal/good/master). Consumable items are generated from `ConsumableItemData` templates. The entire inventory lives inside `PlayerData.inventory` and is managed through `updatePlayerData()` from PlayerContext.

## File Map

| File | Lines | Role |
|------|-------|------|
| `src/types/itemTypes.ts` | 175 | Item, Equipment, Consumable, MagicStones, EquipmentSlot types |
| `src/domain/item_equipment/logic/generateItem.ts` | 69 | Item factory: consumable and equipment generation |
| `src/domain/item_equipment/logic/itemUtils.ts` | 18 | calculateMagicStoneValue helper |
| `src/domain/item_equipment/logic/equipmentStats.ts` | 205 | AP calculation, stat bonuses from equipped items |
| `src/constants/data/items/ConsumableItemData.ts` | 332 | All consumable item definitions (potions, scrolls, etc.) |
| `src/constants/data/items/EquipmentData.ts` | 103 | Equipment base stats, slot definitions |
| `src/constants/data/items/TestItemsData.ts` | 273 | Test items for development (loaded in initial state) |
| `src/contexts/InventoryContext.tsx` | 774 | Inventory operations context (add/remove/equip/move) |

## Data Structures

### Item (base)

```typescript
interface Item {
  id: string;
  name: string;
  description: string;
  itemType: "consumable" | "equipment" | "material";
  rarity: ItemRarity;        // "common" | "uncommon" | "rare" | "epic" | "legend"
  sellPrice: number;
  // Equipment-specific
  equipmentSlot?: EquipmentSlot;
  level?: number;             // 0-3
  quality?: EquipmentQuality; // "poor" | "normal" | "good" | "master"
  durability?: number;
  maxDurability?: number;
  stats?: EquipmentStats;
  // Consumable-specific
  effect?: ConsumableEffect;
  shopPrice?: number;
}
```

### EquipmentSlot

```typescript
type EquipmentSlot = "weapon" | "armor" | "helmet" | "boots" | "accessory1" | "accessory2";
```

### EquipmentStats

```typescript
interface EquipmentStats {
  hp?: number;
  ap?: number;
  attack?: number;
  defense?: number;
  speed?: number;
}
```

### EquipmentQuality

```typescript
type EquipmentQuality = "poor" | "normal" | "good" | "master";
```

### MagicStones

```typescript
interface MagicStones {
  small: number;   // 30G each
  medium: number;  // 100G each
  large: number;   // 350G each
  huge: number;    // 1000G each
}
```

### Inventory Structure (in PlayerData)

```typescript
interface PlayerData {
  inventory: {
    inventory: InventoryState;           // Consumables (capacity limited)
    storage: StorageState;               // Overflow/general storage
    equipmentSlots: EquipmentSlots;      // 6 equipped items
    equipmentInventory: EquipmentInventoryState;  // Unequipped equipment
  }
}
```

### MoveDirection (for moveItem)

```typescript
type MoveDirection =
  | "storage_to_inventory"
  | "inventory_to_storage"
  | "storage_to_equipment"
  | "equipment_to_storage"
  | "equipSlotItem_to_storage"
  | "storage_to_equipment_inventory"
  | "equipment_inventory_to_storage"
  | "equipment_inventory_to_equipment"
  | "equipment_to_equipment_inventory";
```

## Logic Flow

### Item Generation

```
generateConsumableFromData(typeId)
  ├─ Lookup ConsumableItemData[typeId]
  ├─ Generate unique id: `${typeId}_${Date.now()}_${random}`
  └─ Return Item with all fields from template

generateEquipmentItem(slot, rarity)
  ├─ Lookup base stats from EquipmentData[slot][rarity]
  ├─ Apply quality modifier (random quality assignment)
  ├─ Set durability to max
  └─ Return Item with equipment fields
```

### Equipment AP Calculation

```
calculateEquipmentAP(equipmentSlots)
  ├─ For each of 6 slots:
  │   ├─ item.stats.ap (base AP from equipment)
  │   ├─ × LEVEL_STAT_MODIFIERS[item.level]  (1.0 / 1.1 / 1.2 / 1.3)
  │   ├─ × QUALITY_MODIFIERS[item.quality]    (0.95 / 1.0 / 1.03 / 1.05)
  │   └─ Durability factor: if durability/maxDurability < threshold → reduced
  └─ Sum all slot AP values → total equipmentAP bonus

equipmentAP is added to player's maxAp in battle
```

### Equip Item Flow

```
equipItem(itemId, slot)
  ├─ Find item in storage OR equipmentInventory
  ├─ Validate equipmentSlot matches target slot
  ├─ Get currently equipped item (if any)
  ├─ Remove new item from source (storage)
  ├─ If swapping: add old item back to storage
  └─ Single updatePlayerData call (atomic operation)
      └─ Updates equipmentSlots + storage in one call
```

### Unequip Item Flow

```
unequipItem(slot)
  ├─ Get item from equipmentSlots[slot]
  ├─ Check storage has space
  ├─ Set slot to null
  └─ Add item to storage
```

### Move Item Flow

```
moveItem(itemId, direction)
  ├─ "storage_to_inventory":
  │   ├─ Equipment items → equipmentInventory
  │   └─ Normal items → inventory
  ├─ "inventory_to_storage" → move to storage
  ├─ "storage_to_equipment" → calls equipItem()
  ├─ "equipment_to_storage" → find slot, move to storage
  ├─ "equipSlotItem_to_storage" → same as equipment_to_storage
  ├─ "storage_to_equipment_inventory" → equipment type check
  ├─ "equipment_inventory_to_storage" → move back
  ├─ "equipment_inventory_to_equipment" → equip directly
  └─ "equipment_to_equipment_inventory" → unequip to eq inventory
```

### Consumable Data Structure (ConsumableItemData)

```
ConsumableItemData entries:
  ├─ healing_potion: HP +30, shopPrice 50G
  ├─ greater_healing_potion: HP +60, shopPrice 120G
  ├─ full_elixir: Full HP restore, shopPrice 300G
  ├─ teleport_stone: Escape dungeon, shopPrice 80G
  ├─ guard_scroll: Guard +20, shopPrice 60G
  ├─ energy_potion: Energy +2, shopPrice 75G
  ├─ antidote: Remove poison/burn, shopPrice 40G
  └─ ... (many more consumable types)
```

## Key Details

- InventoryContext delegates all persistence to `PlayerContext.updatePlayerData()`
- Equipment slot types: weapon, armor, helmet, boots, accessory1, accessory2 (6 total)
- Equipment levels 0-3 with stat multipliers: 1.0x, 1.1x, 1.2x, 1.3x
- Quality tiers: poor (0.95x), normal (1.0x), good (1.03x), master (1.05x)
- AP durability affects combat effectiveness — broken equipment provides reduced stats
- `TestItemsData.ts` provides pre-loaded test items for development; these are loaded as initial game state
- Item IDs use `Date.now()` + random for uniqueness
- Storage has a capacity limit (default 100)
- Equipment inventory is separate from general inventory (different capacity)
- All move operations return `MoveResult { success, message, movedItem?, replacedItem? }`

## Dependencies

```
InventoryContext
  └─ PlayerContext.usePlayer() → playerData, updatePlayerData

generateItem.ts
  ├─ ConsumableItemData (template lookup)
  └─ EquipmentData (base stats)

equipmentStats.ts
  ├─ LEVEL_STAT_MODIFIERS, QUALITY_MODIFIERS ← campConstants.ts
  └─ Item type definitions ← itemTypes.ts

Integration points:
  ├─ PlayerContext → reads equipmentSlots for AP calculation
  ├─ Shop → generateConsumableFromData, generateEquipmentItem
  ├─ Blacksmith → level/quality upgrades modify equipment stats
  └─ Battle → equipmentAP feeds into player maxAp
```

## Vulnerability Analysis

### `[BUG-RISK]` Stale playerData in Rapid Operations

**Location:** `InventoryContext.tsx:48`

All inventory operations read `playerData` from closure scope and call `updatePlayerData()`. If multiple operations fire quickly (e.g., opening a 6-item pack), each reads the same stale snapshot, potentially overwriting previous operations' results.

### `[BUG-RISK]` equipItem Searches Storage But Removes Only From Storage

**Location:** `InventoryContext.tsx:211-216`

`equipItem()` searches both `storage` and `equipmentInventory` for the item, but the removal logic (line 234) only filters from `storage.items`. If the item was found in `equipmentInventory`, it won't be removed from there, creating a duplicate.

### `[QUALITY]` Duplicate Move Directions

**Location:** `InventoryContext.tsx:441-483` vs `484-519`

`"equipment_to_storage"` and `"equipSlotItem_to_storage"` perform identical operations. Both find the item in equipment slots and move it to storage. This duplication increases the API surface without adding functionality.

### `[BUG-RISK]` Capacity Tracking Manual and Error-Prone

**Location:** `InventoryContext.tsx:56-73`

`currentCapacity` is tracked as a separate field and manually incremented/decremented alongside the items array. If these get out of sync (e.g., a move operation crashes mid-way), the capacity count won't match `items.length`. Using `items.length` directly would be more reliable.

### `[QUALITY]` Test Items Loaded in Production

**Location:** `TestItemsData.ts` (273 lines)

`STORAGE_TEST_ITEMS`, `INVENTORY_TEST_ITEMS`, and `EQUIPPED_TEST_ITEMS` are imported in `PlayerContext` and used as initial game state. These test items ship in the production build and give new players pre-loaded equipment and consumables.

### `[EXTENSIBILITY]` No Item Stacking

**Location:** Systemic

Each consumable item is a separate object with a unique ID. Buying 5 healing potions creates 5 separate items. There's no stack count mechanism, which means inventory capacity is consumed rapidly by consumables.

### `[BUG-RISK]` Equipment Durability Not Decremented

**Location:** `equipmentStats.ts`

Equipment has `durability` and `maxDurability` fields, but no visible code decrements durability during battle. The durability system exists in the type definitions and stat calculations but the actual degradation mechanism is not implemented.
