# Warehouse System Detailed Design Document (STORAGE_DESIGN_V1.0)

## Revision History

- V1.0: Initial creation - Clear distinction between Storage and Inventory, item loss specifications upon death.

---

## 1. Overview

The Warehouse (Storage) is an item storage facility accessible only at the BaseCamp.

**Important Specifications in V1.0:**

```
Purpose: Risk management upon death
Principle: Accessible only at BaseCamp
Guarantee: Items in Storage are retained even upon death

```

### 1.1 Design Background

**Core Elements of an Extraction Dungeon RPG:**

- **Clarification of Death Penalty:** Items in Inventory (on hand) and Equipment Slots are completely lost.
- **Strategic Risk Management:** Deciding whether to keep valuable equipment in Storage or carry it in Inventory.
- **Recovery after Death:** Ability to retry using spare equipment stored in Storage.

### 1.2 Key Roles

1. **Long-term Storage:** Keeping equipment used less frequently or spare items.
2. **Risk Management:** A safety net to prevent loss upon death.
3. **Organization:** Managing the limited capacity of the Inventory.

---

## 2. Detailed Functional Specifications

### 2.1 Distinction between Storage and Inventory

#### 2.1.1 Comparison Table

| Item                | Storage (Warehouse)                         | Inventory (On Hand)                   |
| ------------------- | ------------------------------------------- | ------------------------------------- |
| **Access Location** | BaseCamp only                               | Anywhere (Combat, Exploration)        |
| **Capacity**        | Large (100 slots) \*Phase 1                 | Small (20-30 slots)                   |
| **Upon Death**      | **Retained**                                | **All Lost**                          |
| **Usage**           | Long-term storage, spare gear, collectibles | Use during exploration, carrying gear |
| **Item Types**      | Equipment, Consumables, Materials           | Equipment, Consumables, Materials     |
| **Magic Stones**    | Cannot store (Currency)                     | Cannot store (Currency)               |
| **Gold**            | Cannot store (Currency)                     | Cannot store (Currency)               |

#### 2.1.2 Relationship with Equipment Slots

**Equipment Slots:**

- weapon, armor, helmet, boots, accessory1, accessory2
- Equipped items are treated **separately from Inventory**.
- However, **items in Equipment Slots are also completely lost upon death**.

**Relationship Diagram:**

```
【Player Possessions】

Storage (Warehouse)      Inventory (On Hand)      Equipment Slots
BaseCamp Only            Anywhere                 Equipped
─────────────────        ─────────────────        ─────────────────
⚔️ Spare Sword           ⚔️ Steel Sword           weapon: ⚔️ Fire Sword
🛡️ Spare Armor           🧪 Potion               armor:  🛡️ Dragon Armor
🧪 Potion x10            📜 Scroll               helmet: 👑 Crown
...                      ...                      boots:  👢 Boots
                                                  accessory1: 💍 Ring
Death: Retained          Death: All Lost          Death: All Lost

```

---

### 2.2 Death Processing Flow

#### 2.2.1 Loss Targets vs. Retention Targets

**[Items Lost]**

1. **All items in Inventory**

- Consumables (Potions, Scrolls, etc.)
- Equipment (Weapons, Armor, etc.)
- Materials (Future implementation)

2. **All items in Equipment Slots**

- weapon, armor, helmet, boots, accessory1, accessory2

3. **Resources gained during exploration**

- Gold (Gained during current run)
- Magic Stones (Gained during current run, currency type)
- Soul Remnants (Gained during current run, `currentRunSouls`)

**[Items Retained]**

1. **All items in Storage**

- All items are safe.

2. **Resources stored at BaseCamp**

- Gold stored at BaseCamp (if implemented)
- Magic Stones stored at BaseCamp (currency type)

3. **Permanent Progression Data**

- Accumulated Soul Remnants (`totalSouls`)
- Unlocked Sanctuary Nodes
- Card Mastery
- Encyclopedia Data (Cards, Equipment, Monsters)
- Achievements / Titles
- Class Grade (Promoted status)

#### 2.2.2 Death Processing Pseudo-Code

```typescript
// Death Processing (Pseudo-code)
function handlePlayerDeath() {
  // Loss Processing
  player.inventory = []; // Delete all Inventory
  player.equipment = {
    // Delete all Equipment Slots
    weapon: null,
    armor: null,
    helmet: null,
    boots: null,
    accessory1: null,
    accessory2: null,
  };

  // Reset resources gained during exploration
  player.explorationGold = 0; // Zero out exploration Gold
  player.explorationMagicStones = { small: 0, medium: 0, large: 0 }; // Zero out exploration Stones
  player.sanctuaryProgress.currentRunSouls = 0; // Zero out exploration Souls

  // BaseCamp resources are retained (If implemented)
  // player.baseCampGold remains as is
  // player.baseCampMagicStones remains as is

  // Storage is strictly untouched
  // player.storage remains as is

  // Permanent data is retained
  // player.sanctuaryProgress.totalSouls remains as is
  // player.sanctuaryProgress.unlockedNodes remains as is
  // player.cardMastery remains as is
  // player.encyclopedia remains as is

  // Increment Exploration Count
  player.explorationLimit.current += 1;

  // Return to camp with 1 HP
  player.hp = 1;
  player.ap = 0;

  // Transition to BaseCamp
  gameState.currentScreen = "camp";
}
```

---

### 2.3 Storage Basic Specifications

#### 2.3.1 Capacity

**Phase 1 (MVP):**

- **Capacity:** 100 slots (Fixed)
- **Expansion:** None

**Phase 2 (Expansion):**

- **Initial Capacity:** 50 slots
- **Expansion Methods:**
- Sanctuary "Expanded Bag" Skill: +10, +20, +30
- Shop "Storage Expansion Ticket": +10 (Purchased with Gold or Magic Stones)

- **Max Capacity:** 150 slots

#### 2.3.2 Storable Items

**Storable:**

- ✅ Equipment
- ✅ Consumables
- ✅ Materials (Future)
- ✅ Teleport Stones

**Not Storable:**

- ❌ Gold (Managed separately as currency)
- ❌ Magic Stones (Managed separately as currency)
- ❌ Soul Remnants (Managed by Sanctuary)

#### 2.3.3 Item Movement

**Allowed Operations:**

| Operation                | Description                   |
| ------------------------ | ----------------------------- |
| Storage → Inventory      | Retrieve from warehouse       |
| Inventory → Storage      | Deposit into warehouse        |
| Equipment Slot → Storage | Unequip and move to warehouse |
| Storage → Equipment Slot | Equip directly from warehouse |

**Movement Restrictions:**

- **Inventory Full:** Cannot move Storage → Inventory.
- **Storage Full:** Cannot move Inventory → Storage.
- **Equipped:** Items in Equipment Slots must be unequipped before moving (or moved directly via swap).

---

## 3. UI/UX Design

### 3.1 Screen Layout

```
┌────────────────────────────────────────────────────────┐
│  📦 Warehouse                                          │
├────────────────────────────────────────────────────────┤
│  Gold: 1,250 G  Magic Stone Value: 450                 │
│                                                        │
│  [Storage] [Inventory]  ← Tab Switch                   │
│  ═════════  ─────────────                              │
│                                                        │
│  ┌─────────────── Storage (45/100) ───────────────┐   │
│  │                                                 │   │
│  │  ┌──┐ ┌──┐ ┌──┐ ┌──┐ ┌──┐                     │   │
│  │  │⚔️│ │🛡️│ │👑│ │🧪│ │📜│ ...                │   │
│  │  │Sw│ │Ar│ │Cr│ │Po│ │Sc│                     │   │
│  │  └──┘ └──┘ └──┘ └──┘ └──┘                     │   │
│  │                                                 │   │
│  │  ┌──┐ ┌──┐ ┌──┐                                │   │
│  │  │⚔️│ │⚔️│ │🧪│ ...                           │   │
│  │  └──┘ └──┘ └──┘                                │   │
│  │                                                 │   │
│  └─────────────────────────────────────────────────┘   │
│                                                        │
│  ↕ Move Items ↕                                        │
│                                                        │
│  ┌───────────── Inventory (15/30) ────────────────┐   │
│  │                                                 │   │
│  │  ┌──┐ ┌──┐ ┌──┐ ┌──┐                           │   │
│  │  │⚔️│ │🧪│ │🧪│ │📜│ ...                       │   │
│  │  │Sw│ │Po│ │Po│ │Sc│                           │   │
│  │  └──┘ └──┘ └──┘ └──┘                           │   │
│  │                                                 │   │
│  └─────────────────────────────────────────────────┘   │
│                                                        │
│  [Move Selected] [Move All] [Change Equip]            │
│                                                        │
│  [Return to Camp]                                      │
└────────────────────────────────────────────────────────┘

```

### 3.2 Tab Switching

**Two Tabs:**

1. **Storage Tab:**

- Displays items inside Storage.
- Click to select → Shows "Move to Inventory" button.

2. **Inventory Tab:**

- Displays items inside Inventory.
- Click to select → Shows "Move to Storage" button.

### 3.3 Item Movement Operations

#### 3.3.1 Individual Move

**Operation 1: Click Select + Button**

```
1. Click item in Storage → Selected state (Highlight border)
2. Click "Move to Inventory" button
3. Item moves to Inventory

(Reverse applies for Inventory → Storage)

```

**Operation 2: Drag & Drop (Phase 2)**

```
1. Start dragging item in Storage
2. Drop into Inventory area
3. Item moves to Inventory

```

#### 3.3.2 Batch Move

**Batch Buttons:**

| Button                       | Function                                                    |
| ---------------------------- | ----------------------------------------------------------- |
| [All Equip to Storage]       | Move all `equipment` from Inventory to Storage.             |
| [All Consumables to Storage] | Move all `consumable` from Inventory to Storage.            |
| [Store All Items]            | Move everything from Inventory to Storage (up to capacity). |
| [Loadout]                    | Retrieve a saved equipment set from Storage (Phase 2).      |

**Confirmation Dialog:**

```
┌─────────────────────────────────────┐
│  Move 15 items to Storage?          │
│                                     │
│  [Yes]  [No]                        │
└─────────────────────────────────────┘

```

---

### 3.4 Item Display

#### 3.4.1 Item Card

```
┌────────┐
│  ⚔️    │  ← Icon
│ FireSw │  ← Name
│ Epic   │  ← Rarity
│ Lv2    │  ← Level
└────────┘

```

**Color Coding:**

- Common: White
- Rare: Blue
- Epic: Purple
- Legendary: Gold

#### 3.4.2 Detail Information Panel

Clicking an item displays details on the right:

```
┌───────────────────────────────────┐
│  ⚔️ Sword of Fire                  │
│                                   │
│  Rarity: Epic                     │
│  Level: 2                         │
│  Durability: 45/50                │
│                                   │
│  Effects:                         │
│  - ATK +25                        │
│  - Fire Dmg +10                   │
│                                   │
│  Description:                     │
│  A sword inhabited by a fire      │
│  spirit.                          │
│                                   │
│  [Move to Inventory]              │
│  [Equip]                          │
│  [Close Details]                  │
└───────────────────────────────────┘

```

---

### 3.5 Equipment Change Function

**Direct Equip from Storage Screen:**

```
Click weapon in Storage
  ↓
Click [Equip] button
  ↓
Swap with currently equipped weapon
  - Old weapon → Moves to Storage
  - New weapon → Moves to Equipment Slot

```

**Equipment Slot Display:**

Display equipment slots at the top of the Storage screen:

```
┌───────────────── Current Loadout ─────────────────┐
│  weapon: ⚔️ Sword of Fire                        │
│  armor:  🛡️ Dragon Armor                         │
│  helmet: 👑 Crown                               │
│  boots:  👢 Boots of Speed                      │
│  accessory1: 💍 Ring of Power                   │
│  accessory2: 📿 Amulet of Protection            │
│                                                 │
│  [Unequip All]  [Save Loadout]                  │
└─────────────────────────────────────────────────┘

```

---

## 4. Data Structure Definition

### 4.1 StorageTypes.ts

```typescript
// src/types/StorageTypes.ts (New)

import type { Item } from "./ItemTypes";

/**
 * Storage State
 */
export interface StorageState {
  items: Item[]; // List of items in Storage
  maxCapacity: number; // Max capacity (Phase 1: 100)
  currentCapacity: number; // Current usage (items.length)
}

/**
 * Inventory State
 */
export interface InventoryState {
  items: Item[]; // List of items in Inventory
  maxCapacity: number; // Max capacity (Phase 1: 30)
  currentCapacity: number; // Current usage (items.length)
}

/**
 * Equipment Slots
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
  | "inventory_to_equipment"
  | "equipment_to_inventory";

/**
 * Move Result
 */
export interface MoveResult {
  success: boolean;
  message: string;
  movedItem?: Item;
  replacedItem?: Item; // Old item when swapping equipment
}
```

---

### 4.2 Integration into PlayerContext

```typescript
// src/contexts/PlayerContext.tsx (Modified)

import type {
  StorageState,
  InventoryState,
  EquipmentSlots,
} from "../types/StorageTypes";

export interface Player {
  // ... existing fields

  // New: Storage
  storage: StorageState;

  // New: Inventory
  inventory: InventoryState;

  // New: Equipment Slots
  equipment: EquipmentSlots;

  // Resources (Currency type)
  gold: number; // Current Gold (Exploration + BaseCamp)
  explorationGold: number; // Gold gained during exploration (Lost on death)
  baseCampGold: number; // Gold stored at BaseCamp (Kept on death)

  magicStones: {
    // Current Stones (Exploration + BaseCamp)
    small: number;
    medium: number;
    large: number;
  };
  explorationMagicStones: {
    // Stones gained during exploration (Lost on death)
    small: number;
    medium: number;
    large: number;
  };
  baseCampMagicStones: {
    // Stones stored at BaseCamp (Kept on death)
    small: number;
    medium: number;
    large: number;
  };

  // ... others
}

// Initial Values
const initialPlayer: Player = {
  // ...

  storage: {
    items: [],
    maxCapacity: 100,
    currentCapacity: 0,
  },

  inventory: {
    items: [],
    maxCapacity: 30,
    currentCapacity: 0,
  },

  equipment: {
    weapon: null,
    armor: null,
    helmet: null,
    boots: null,
    accessory1: null,
    accessory2: null,
  },

  gold: 0,
  explorationGold: 0,
  baseCampGold: 0,

  magicStones: { small: 0, medium: 0, large: 0 },
  explorationMagicStones: { small: 0, medium: 0, large: 0 },
  baseCampMagicStones: { small: 0, medium: 0, large: 0 },

  // ...
};
```

---

## 5. Logic Implementation

### 5.1 Item Move Logic

```typescript
// src/domain/camps/storage/logic/itemMove.ts (New)

import type { Item } from "../../../../types/ItemTypes";
import type {
  StorageState,
  InventoryState,
  MoveDirection,
  MoveResult,
} from "../../../../types/StorageTypes";

/**
 * Move Item
 */
export function moveItem(
  item: Item,
  direction: MoveDirection,
  storage: StorageState,
  inventory: InventoryState
): MoveResult {
  switch (direction) {
    case "storage_to_inventory":
      return moveStorageToInventory(item, storage, inventory);

    case "inventory_to_storage":
      return moveInventoryToStorage(item, storage, inventory);

    // ... other directions

    default:
      return { success: false, message: "Unknown move direction" };
  }
}

/**
 * Storage → Inventory
 */
function moveStorageToInventory(
  item: Item,
  storage: StorageState,
  inventory: InventoryState
): MoveResult {
  // Check Inventory Capacity
  if (inventory.currentCapacity >= inventory.maxCapacity) {
    return {
      success: false,
      message: "Inventory is full",
    };
  }

  // Remove from Storage
  const itemIndex = storage.items.findIndex((i) => i.id === item.id);
  if (itemIndex === -1) {
    return {
      success: false,
      message: "Item not found in Storage",
    };
  }

  storage.items.splice(itemIndex, 1);
  storage.currentCapacity--;

  // Add to Inventory
  inventory.items.push(item);
  inventory.currentCapacity++;

  return {
    success: true,
    message: `Moved ${item.name} to Inventory`,
    movedItem: item,
  };
}

/**
 * Inventory → Storage
 */
function moveInventoryToStorage(
  item: Item,
  storage: StorageState,
  inventory: InventoryState
): MoveResult {
  // Check Storage Capacity
  if (storage.currentCapacity >= storage.maxCapacity) {
    return {
      success: false,
      message: "Storage is full",
    };
  }

  // Remove from Inventory
  const itemIndex = inventory.items.findIndex((i) => i.id === item.id);
  if (itemIndex === -1) {
    return {
      success: false,
      message: "Item not found in Inventory",
    };
  }

  inventory.items.splice(itemIndex, 1);
  inventory.currentCapacity--;

  // Add to Storage
  storage.items.push(item);
  storage.currentCapacity++;

  return {
    success: true,
    message: `Moved ${item.name} to Storage`,
    movedItem: item,
  };
}
```

---

### 5.2 Death Handling Logic

```typescript
// src/domain/battles/logic/deathHandler.ts (New or Modified)

import type { Player } from "../../../contexts/PlayerContext";

/**
 * Handle Player Death
 */
export function handlePlayerDeath(player: Player): Player {
  return {
    ...player,

    // Delete all Inventory
    inventory: {
      ...player.inventory,
      items: [],
      currentCapacity: 0,
    },

    // Delete all Equipment Slots
    equipment: {
      weapon: null,
      armor: null,
      helmet: null,
      boots: null,
      accessory1: null,
      accessory2: null,
    },

    // Zero out exploration resources
    explorationGold: 0,
    gold: player.baseCampGold, // Keep only BaseCamp funds

    explorationMagicStones: { small: 0, medium: 0, large: 0 },
    magicStones: { ...player.baseCampMagicStones }, // Keep only BaseCamp stones

    // Zero out exploration Souls
    sanctuaryProgress: {
      ...player.sanctuaryProgress,
      currentRunSouls: 0,
      // totalSouls is retained
    },

    // Storage is strictly untouched
    // storage: kept as is

    // Exploration Count +1
    explorationLimit: {
      ...player.explorationLimit,
      current: player.explorationLimit.current + 1,
    },

    // Return to Camp with 1 HP
    hp: 1,
    ap: 0,
  };
}
```

---

## 6. Strategic Significance

### 6.1 Importance of Risk Management

**Core Strategy:**

```
High Risk Exploration (Deep levels):
→ Store valuable equipment in Storage
→ Carry only essential gear in Inventory

Safe Exploration (Shallow levels):
→ Use good equipment in Inventory
→ Proceed efficiently

```

### 6.2 Player Choices

**Pre-Exploration Preparation:**

1. **Equipment Selection:**

- "Do I need Epic gear for this run? Or is Common enough?"
- "The Abyss (Depth 5) is dangerous, so I'll store my Epic gear."

2. **Consumable Prep:**

- "I'll bring extra potions."
- "Teleport Stone is mandatory."

3. **Securing Spares:**

- "If I die, I can retry using the spare gear in Storage."

### 6.3 Recovery After Death

**If Death Occurs:**

```
1. Inventory and Equipment Slots are completely lost.
   → Lost the Epic gear...

2. Access Storage.
   → I have spare Rare gear!

3. Retrieve gear from Storage.
   → Ready to try again.

4. Buy new gear at the Shop.
   → Using Gold stored at BaseCamp.

5. Proceed to next exploration.

```

---

## 7. Phase Implementation Plan

### Phase 1 (MVP): Basic Functions

```
□ Implementation of Storage and Inventory
  □ Data Structures (StorageState, InventoryState)
  □ Integration into PlayerContext

□ Item Movement Features
  □ Storage ⇄ Inventory
  □ Storage ⇄ Equipment
  □ Capacity Checks

□ Death Processing
  □ Delete all Inventory
  □ Delete all Equipment Slots
  □ Retain Storage

□ Storage UI
  □ Basic Layout
  □ Tab Switching
  □ Item Display
  □ Click Select + Move Button

```

### Phase 2 (Extensions): Usability Improvements

```
□ Drag & Drop
□ Batch Move Functions
□ Save/Load Equipment Sets
□ Storage Capacity Expansion
□ Filtering / Sorting Functions

```

---

## 8. Test Cases

### 8.1 Item Movement Tests

```
□ Storage → Inventory
  □ Normal Move
  □ Error on Inventory Full

□ Inventory → Storage
  □ Normal Move
  □ Error on Storage Full

□ Storage → Equipment
  □ Equip Swap (Old gear goes to Storage)
  □ Equip to Empty Slot

□ Equipment → Storage
  □ Unequip to Storage

```

### 8.2 Death Processing Tests

```
□ Loss Verification
  □ Inventory items deleted
  □ Equipment slots deleted
  □ explorationGold → 0
  □ explorationMagicStones → 0
  □ currentRunSouls → 0

□ Retention Verification
  □ Storage items retained
  □ baseCampGold retained
  □ baseCampMagicStones retained
  □ totalSouls retained
  □ Unlocked Sanctuary Nodes retained

```

### 8.3 Capacity Management Tests

```
□ Max Capacity Check
  □ Cannot add at Storage 100/100
  □ Cannot add at Inventory 30/30

□ Capacity Count
  □ Add item: +1
  □ Remove item: -1

```

---

## 9. Detailed UI/UX Specifications

### 9.1 Item Sorting

**Sort Options:**

| Sort Criteria | Description                      |
| ------------- | -------------------------------- |
| Rarity        | Legendary → Epic → Rare → Common |
| Level         | High Level → Low Level           |
| Name          | Alphabetical                     |
| Type          | weapon → armor → helmet → ...    |
| Recent        | Newest → Oldest                  |

### 9.2 Filtering

**Filter Options:**

| Filter Criteria  | Description                      |
| ---------------- | -------------------------------- |
| Equipment Only   | `equipment` only                 |
| Consumables Only | `consumable` only                |
| By Rarity        | Common / Rare / Epic / Legendary |
| By Type          | weapon / armor / helmet / ...    |

### 9.3 Search Function (Phase 2)

**Search Box:**

```
┌────────────────────────────────────┐
│  🔍 Search Item: [_________]      │
└────────────────────────────────────┘

Example: Search "Fire"
→ Displays "Sword of Fire", "Fire Armor", etc.

```

---

## 10. Notes

### 10.1 Implementation Notes

**Data Integrity:**

- Item `id` must be unique.
- Ensure the same item does not exist in both Storage and Inventory.

**Save Data:**

- Storage state must be persistent.
- Save to localStorage or Cloud.

**Performance:**

- Use virtual scrolling if item count is large.
- Properly memoize sorting and filtering.

### 10.2 Future Extensions

**Phase 3+:**

- Storage Tabs (Equipment Tab, Consumable Tab, Material Tab).
- Favorites function.
- Item Lock (Prevent accidental deletion).
- Dialogue with Warehouse Keeper (NPC).
- Storage-specific keyboard shortcuts.

---

## 11. Reference Documents

```
GAME_DESIGN_MASTER_V2
├── CAMP_FACILITIES_DESIGN.md (Role of warehouse facility)
├── SANCTUARY_DESIGN_V2.md (Processing upon death)
└── STORAGE_DESIGN_V1.0 [This Document]
    ├── StorageTypes.ts
    ├── itemMove.ts
    └── deathHandler.ts

```
