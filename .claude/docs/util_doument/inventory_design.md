Here is the English translation of the design document.

# Inventory System Design Document V3.0

## Revision History

| Version | Date       | Changes                                                                |
| ------- | ---------- | ---------------------------------------------------------------------- |
| V1.0    | -          | Initial creation                                                       |
| V3.0    | 2026-01-23 | Lives system integration, death penalty update, teleport stone unification |

## Overview

### Concept

**A resource management system for an "Extraction Dungeon RPG"**

- Manage items and equipment acquired during exploration.
- Strategic decision-making on "what to bring back" within limited capacity.
- V3.0: Death means complete item/equipment loss (including brought items), but souls are 100% saved.

### Related Documents

| Document                           | Related Content                                       |
| ---------------------------------- | ----------------------------------------------------- |
| `EQUIPMENT_AND_ITEMS_DESIGN.md`    | Detailed specifications for equipment and consumables |
| `return_system_design.md` (V3.0)   | Item handling upon survival/death, lives system       |
| `dungeon_exploration_ui_design.md` (V3.0) | UI layout, Inventory modal, lives display       |
| `game_design_master.md` (V3.0)     | Core game loop with lives system                      |

---

## 1. Inventory Structure

### 1.1 Overall Composition

```
[Inventory Overall Structure]
├─ Equipped Slots (6 Fixed Slots)
│   ├─ Head
│   ├─ Body
│   ├─ Right Hand
│   ├─ Left Hand
│   ├─ Boots
│   └─ Accessory
│
├─ Spare Equipment Slots (10 Slots)
│   └─ Stores unequipped gear
│   └─ Equipment acquired during exploration is stored here
│
├─ Consumable Item Slots (20 Slots)
│   └─ Potions, Teleport Stones, Buff items, etc.
│   └─ Same items cannot stack (1 item per slot)
│
└─ Magic Stone Slots (Managed separately, 5 types)
    ├─ Tiny: Max 99
    ├─ Small: Max 99
    ├─ Medium: Max 99
    ├─ Large: Max 99
    └─ Huge: Max 99

```

### 1.2 Capacity Limit Summary

| Category        | Limit           | Stack | Notes                      |
| --------------- | --------------- | ----- | -------------------------- |
| Equipped        | 6 Slots (Fixed) | -     | 1 per slot                 |
| Spare Equipment | 10 Slots        | No    | Acquired gear goes here    |
| Consumables     | 20 Slots        | No    | 1 item per slot            |
| Magic Stones    | 99 each         | Yes   | 5 types managed separately |
| Gold            | No Limit        | -     | Managed as a number only   |

---

## 2. Item Category Definitions

### 2.1 Equipment

```typescript
interface Equipment {
  id: string;
  name: string;
  slot: "head" | "body" | "rightHand" | "leftHand" | "boots" | "accessory";
  rarity: "common" | "rare" | "epic" | "legendary";
  level: 0 | 1 | 2 | 3; // Upgrade level
  currentAP: number; // Current durability
  maxAP: number; // Max durability
  stats: EquipmentStats;
  skills: EquipmentSkill[];
  isEquipped: boolean;
}

interface EquipmentStats {
  hpBonus?: number;
  atkBonus?: number; // % notation
  defBonus?: number; // % notation
  magicBonus?: number; // % notation
  energyBonus?: number;
}

interface EquipmentSkill {
  id: string;
  name: string;
  description: string;
  isUnlocked: boolean; // For skills unlocked at Lv3
}
```

### 2.2 Consumable Items

```typescript
interface ConsumableItem {
  id: string;
  name: string;
  category: "recovery" | "buff" | "special" | "utility";
  effect: ItemEffect;
  usableIn: ("map" | "battle")[]; // Usable contexts
  price: number; // Shop price
}

interface ItemEffect {
  type: string;
  value: number;
  duration?: number; // For buffs
  additionalEffects?: string[];
}
```

### 2.3 Magic Stones

```typescript
interface MagicStone {
  size: "tiny" | "small" | "medium" | "large" | "huge";
  count: number; // 0-99
  sellPrice: number; // Selling price
}

// Value by Magic Stone size
const STONE_VALUES = {
  tiny: 10,
  small: 30,
  medium: 80,
  large: 200,
  huge: 500,
};
```

---

## 3. Possession Limit System

### 3.1 Overflow Handling

**When Equipment (10 Spare Slots) reaches capacity**:

```
┌─────────────────────────────────────┐
│      Equipment Slots Full           │
├─────────────────────────────────────┤
│                                     │
│ New Equipment Acquired:             │
│ [Warrior's Sword (Rare)]            │
│                                     │
│ To bring this back, you must        │
│ discard an existing item.           │
│                                     │
│ ┌─────────────────────────────┐    │
│ │ Current Spare Equipment:     │    │
│ │ 1. Old Shield (Common)       │    │
│ │ 2. Wizard's Hat (Rare)       │    │
│ │ 3. ...                       │    │
│ └─────────────────────────────┘    │
│                                     │
│ [Discard Selected] [Give Up Item]   │
└─────────────────────────────────────┘

```

**Process Flow**:

1. Check capacity when acquiring a new item.
2. If full, immediately show the selection screen.
3. Player selects an item to discard.
4. After selection, the new item is added to the inventory.

**When Consumables (20 Slots) reach capacity**:

- Show a similar selection screen.
- Select an item to discard or give up acquiring the new one.

**When Magic Stones reach capacity (99)**:

- Cannot acquire more of that size.
- Display "Cannot carry more Magic Stones (Small)".
- Other sizes can still be acquired normally.

### 3.2 Stacking Specifications

| Item Type            | Stack | Limit      |
| -------------------- | ----- | ---------- |
| Equipment            | No    | 1 per slot |
| Consumable           | No    | 1 per slot |
| Magic Stone (Tiny)   | Yes   | 99         |
| Magic Stone (Small)  | Yes   | 99         |
| Magic Stone (Medium) | Yes   | 99         |
| Magic Stone (Large)  | Yes   | 99         |
| Magic Stone (Huge)   | Yes   | 99         |
| Gold                 | -     | No Limit   |

---

## 4. Item Management UI

### 4.1 Exploration Inventory (Modal Display)

**Display Trigger**: Click "🎒 Inventory" button on the left panel.

**Modal Layout**:

```
┌─────────────────────────────────────────────────────┐
│  Inventory                                   [×]   │
├─────────────────────────────────────────────────────┤
│                                                     │
│  [Equipped]                                         │
│  ┌────┬────┬────┬────┬────┬────┐                  │
│  │Head│Body│ R  │ L  │Boot│Acc │                  │
│  └────┴────┴────┴────┴────┴────┘                  │
│                                                     │
│  [Spare Equipment] (7/10)                           │
│  ┌────┬────┬────┬────┬────┐                       │
│  │ 🗡️ │ 🛡️ │ 👒 │ ... │    │                       │
│  └────┴────┴────┴────┴────┘                       │
│                                                     │
│  [Consumables] (12/20)                              │
│  ┌────┬────┬────┬────┬────┐                       │
│  │ 💊 │ 💎 │ 📜 │ ... │    │                       │
│  └────┴────┴────┴────┴────┘                       │
│                                                     │
│  [Magic Stones]                                     │
│  Tiny: 45  Small: 23  Med: 8  Lrg: 2  Huge: 0       │
│                                                     │
│  [Gold] 1,500 G                                     │
│                                                     │
└─────────────────────────────────────────────────────┘

```

**Operations**:

- Click Item → Show details + Action menu.
- Equipment → "Equip", "Discard".
- Consumable → "Use" (if usable), "Discard".
- Drag & Drop → Swap equipment (Possible during exploration).

### 4.2 Base Inventory

**Additional Base Functions**:

- Upgrade Equipment (Blacksmith).
- Repair Equipment AP (Blacksmith).
- Sell Magic Stones (Merchant).
- Buy Consumables (Merchant).

> **Note**: A Warehouse system (extra storage at base) is not planned at this time. Considered for future expansion.

### 4.3 Combat Quick Slots

**Display on Battle Screen**:

```
┌─────────────────────────────────────┐
│  [Hand Area]                        │
│                                     │
│  ┌────┬────┬────┐                  │
│  │ Q1 │ Q2 │ Q3 │ ← Quick Slots     │
│  │ 💊 │ 💎 │ 📜 │    (3 Slots)      │
│  └────┴────┴────┘                  │
│                                     │
│  [End Turn]                         │
└─────────────────────────────────────┘

```

**Quick Slot Specs**:

- Slots: **3 Slots**.
- Setup Timing: Before battle or from exploration inventory.
- Change During Battle: Impossible (Pre-set only).
- Usage: Click or Shortcut keys (1, 2, 3).

**Items Set to Quick Slots**:

- Only consumables with the "Usable in Battle" flag.
- Examples: Potions, Buffs, Emergency Teleport Stone, Hourglass of Time Stop, etc.

---

## 5. Item Usage Context

### 5.1 Definition of Usage Scenarios

| Item Example               | Map | Battle | Notes                   |
| -------------------------- | --- | ------ | ----------------------- |
| Small Potion               | ○   | ○      |                         |
| Medium Potion              | ○   | ○      |                         |
| Large Potion               | ○   | ○      |                         |
| Full Potion                | ○   | ○      |                         |
| Resurrection Stone         | -   | Auto   | Auto-activates on death |
| Elixir of Attack           | ×   | ○      | Battle-only buff        |
| Elixir of Defense          | ×   | ○      | Battle-only buff        |
| Elixir of Speed            | ×   | ○      | Battle-only buff        |
| Elixir of Omnipotence      | ×   | ○      | Battle-only buff        |
| Elixir of Critical         | ×   | ○      | Battle-only buff        |
| Teleport Stone (Unified)   | ○   | ×      | V3.0: Single type, 100% reward |
| Hourglass of Time Stop     | ×   | ○      | Battle only             |
| Crystal of Magic Explosion | ×   | ○      | Battle only             |
| Treasure Map               | ○   | ×      | Map only                |
| Merchant Discount Ticket   | ○   | ×      | Map only                |
| Equipment Repair Kit       | ○   | ×      | Map only                |
| XP Boost                   | ○   | ×      | Map only                |
| Lucky Charm                | ○   | ×      | Map only                |

### 5.2 Auto-Activation Items

**Resurrection Stone**:

- Trigger: The moment HP reaches 0.
- Effect: Revive with 50% HP.
- Consumption: Automatically consumes 1.
- Priority: If holding multiple, only 1 activates.

---

## 6. Item Acquisition/Loss Flow

### 6.1 Combat Drop Process

```
Battle End
    │
    ▼
Drop Check
    │
    ▼
┌─────────────────────┐
│  Battle Rewards     │
│                     │
│  Magic Stone (S) x3 │
│  Gold +120          │
│  Card Acquired!     │
│  Equipment!         │ ← If dropped
│                     │
│  [Confirm]          │
└─────────────────────┘
    │
    ▼
Inventory Limit Check
    │
    ├─ Within Limit → Auto Add
    │
    └─ Over Limit → Overflow Process (Selection Screen)

```

### 6.2 Event Reward Process

- Chest: Same process as drops.
- Choice Event: Grant reward after selection.
- Hidden Room: Guaranteed reward + Additional reward.

### 6.3 Loss Rules Upon Death (V3.0 - Major Change)

| Item Type             | Process on Death     | Notes                                   |
| --------------------- | -------------------- | --------------------------------------- |
| Equipped Gear         | **ALL Lost**         | V3.0: No exceptions, even Legendary     |
| Spare Equipment       | **ALL Lost**         | V3.0: No exceptions, even Legendary     |
| Items Brought to Dungeon | **ALL Lost**      | V3.0: Items brought from base are lost too |
| Consumables           | **ALL Lost**         |                                         |
| Magic Stones          | **Becomes 0**        |                                         |
| Gold                  | **Becomes 0**        | V3.0: Gold is also lost on death        |
| Soul Remnants         | **100% Saved**       | V3.0: Souls are always saved to total   |

**V3.0 Important Changes**:

- **No Safe Items**: Even Legendary equipment is lost on death.
- **Brought Items Lost**: Items you bring into the dungeon are also lost on death.
- **Gold Lost**: Gold acquired during exploration is lost (base gold was already 0 at start).
- **Souls Always Saved**: The only thing guaranteed to persist is soul remnants (100%).

**Strategic Implications**:

- Players must carefully consider what equipment to bring into dangerous areas.
- Losing valuable equipment is a real consequence of death.
- Soul collection becomes the primary form of permanent progression (until game over).
- High-risk/high-reward gameplay is encouraged since souls are always saved.

**Lives System Connection**:

- Death decreases lives by 1.
- When lives reach 0 → Game Over → Complete inventory reset + sanctuary reset.
- Lives cap: Hard = 2, Normal/Easy = 3.

### 6.4 Carry-Back Rules Upon Survival (V3.0 - Simplified)

**Return on Foot (Return Route)**:
| Item Type | Carry Back | Notes |
| :--- | :--- | :--- |
| Equipment | 100% | Bring back all |
| Consumables | 100% | Bring back all |
| Magic Stones | 100% | Bring back all |
| Soul Remnants | 100% | Always saved |

**Teleport Stone Use (V3.0 - Unified)**:
| Item Type | Carry Back | Notes |
| :--- | :--- | :--- |
| Equipment | 100% | V3.0: No reduction |
| Consumables | 100% | V3.0: No reduction |
| Magic Stones | 100% | V3.0: No reduction |
| Soul Remnants | 100% | Always saved |

**V3.0 Change**: Teleport stones are now unified into a single type with 100% reward. The multiple stone types (Normal/Blessed/Emergency) have been removed.

> **Note**: Refer to `return_system_design.md` (V3.0) for details.

---

## 7. Sort & Filter Functions

### 7.1 Sort Conditions

**Equipment**:

- Rarity (Desc/Asc)
- By Slot
- Upgrade Level
- Remaining AP
- Acquisition Order

**Consumables**:

- By Category (Recovery/Buff/Special/Utility)
- Acquisition Order
- Price

### 7.2 Filter Conditions

**Equipment**:

- Slot (Head/Body/Right/Left/Boots/Acc)
- Rarity (Common/Rare/Epic/Legendary)
- Equippable Class (Swordsman/Mage/Common)

**Consumables**:

- Category
- Usage Scenario (Map/Battle/Both)

---

## 8. Data Structure

### 8.1 TypeScript Interface Definition

```typescript
interface PlayerInventory {
  // Equipped
  equippedItems: {
    head: Equipment | null;
    body: Equipment | null;
    rightHand: Equipment | null;
    leftHand: Equipment | null;
    boots: Equipment | null;
    accessory: Equipment | null;
  };

  // Spare Equipment (Max 10)
  reserveEquipment: Equipment[];

  // Consumables (Max 20)
  consumableItems: ConsumableItem[];

  // Quick Slots (3)
  quickSlots: (ConsumableItem | null)[];

  // Magic Stones
  magicStones: {
    tiny: number; // 0-99
    small: number; // 0-99
    medium: number; // 0-99
    large: number; // 0-99
    huge: number; // 0-99
  };

  // Gold
  gold: number;
}

// Capacity Constants
const INVENTORY_LIMITS = {
  RESERVE_EQUIPMENT_MAX: 10,
  CONSUMABLE_ITEMS_MAX: 20,
  QUICK_SLOTS: 3,
  MAGIC_STONE_STACK_MAX: 99,
} as const;
```

### 8.2 Inventory Operation Functions

```typescript
// Add Item (With Overflow Check)
function addItem(
  inventory: PlayerInventory,
  item: Equipment | ConsumableItem
): { success: boolean; requiresSelection: boolean } {
  if (isEquipment(item)) {
    if (
      inventory.reserveEquipment.length >=
      INVENTORY_LIMITS.RESERVE_EQUIPMENT_MAX
    ) {
      return { success: false, requiresSelection: true };
    }
    inventory.reserveEquipment.push(item);
    return { success: true, requiresSelection: false };
  }

  if (isConsumable(item)) {
    if (
      inventory.consumableItems.length >= INVENTORY_LIMITS.CONSUMABLE_ITEMS_MAX
    ) {
      return { success: false, requiresSelection: true };
    }
    inventory.consumableItems.push(item);
    return { success: true, requiresSelection: false };
  }

  return { success: false, requiresSelection: false };
}

// Add Magic Stone
function addMagicStone(
  inventory: PlayerInventory,
  size: keyof PlayerInventory["magicStones"],
  count: number
): { added: number; overflow: number } {
  const current = inventory.magicStones[size];
  const space = INVENTORY_LIMITS.MAGIC_STONE_STACK_MAX - current;
  const added = Math.min(count, space);
  const overflow = count - added;

  inventory.magicStones[size] = current + added;

  return { added, overflow };
}

// Swap Equipment
function equipItem(inventory: PlayerInventory, item: Equipment): boolean {
  const slot = item.slot;
  const currentEquipped = inventory.equippedItems[slot];

  // Move currently equipped item to spare
  if (currentEquipped) {
    if (
      inventory.reserveEquipment.length >=
      INVENTORY_LIMITS.RESERVE_EQUIPMENT_MAX
    ) {
      return false; // No space in spare slots
    }
    inventory.reserveEquipment.push(currentEquipped);
  }

  // Equip new item
  inventory.equippedItems[slot] = item;

  // Remove from spare
  const index = inventory.reserveEquipment.findIndex((e) => e.id === item.id);
  if (index !== -1) {
    inventory.reserveEquipment.splice(index, 1);
  }

  return true;
}
```

### 8.3 Persistence Specs

**Save Timing**:

- End of Exploration (Survival/Death).
- Operations at Base (Change Equip, Buy/Sell, etc.).
- App Close.

**Save Targets**:

- Entire PlayerInventory.
- Known Event List (Managed separately).
- Library Records (Managed separately).

**Carry-Over on Game Over (V3.0 - Major Change)**:

- Inventory: Reset (Initial state).
- Known Events: **Reset** (V3.0 change).
- Library Records: **Reset** (V3.0 change).
- Sanctuary Progress: **Reset** (V3.0 change).
- Achievements: **Retained** (Only achievements persist).

---

## 9. Implementation Priority

### Phase 1: Core Features

- [ ] Inventory data structure implementation.
- [ ] Management of Equipped/Spare equipment.
- [ ] Management of Consumables.
- [ ] Management of Magic Stones.
- [ ] Overflow processing (Selection Screen).
- [ ] Inventory Modal UI.

### Phase 2: Combat Integration

- [ ] Quick Slot System.
- [ ] Item usage during combat.
- [ ] Auto-activation items (Resurrection Stone).

### Phase 3: UX Improvements

- [ ] Sort function.
- [ ] Filter function.
- [ ] Drag & Drop operations.
- [ ] Item comparison display.
