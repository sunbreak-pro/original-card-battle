Here is the English translation of the Shop (Merchant's Exchange) Detailed Design Document.

I have included a few visual aids to help visualize the atmosphere and logic.

---

# Merchant's Exchange Detailed Design Document (SHOP_DESIGN_V3)

## Update History

- V1.0: Initial Draft (Magic Stone rate adjustment, Sale system, Equipment Pack specifications finalized)
- V1.1: Updated to match implementation (Restock timing, Seeded RNG, Depth-dependent drops)
- V3.0: Unified Teleport Stone (1 type, 100% return rate) to align with Lives System and return_system V3.0

---

## 1. Overview

The Merchant's Exchange is the center of economic activity where adventurers convert loot obtained from dungeon exploration into funds (Gold) and procure supplies and equipment for their next adventure.

### Primary Roles

1. **Buy**: Purchase consumables, Teleport Stones, and Equipment Packs (Gacha element) using Gold or Magic Stones.
2. **Sell**: Convert unnecessary equipment and collection items into money.
3. **Daily Sales**: Specific items are discounted.

**Changes in V1.1 (Magic Stones as Currency):**

- Magic Stones are treated as currency, so they do not need to be converted to Gold before purchasing.
- Payments can be made using Gold or Magic Stones (via exchange) during purchase.

---

## 2. Detailed Functional Specifications

### 2.1 Buying System

Items are displayed in the following 3 categories.

#### 2.1.1 Consumables

**Basic Specs:**

- Potions, Status Recovery, Buff Items.
- Stock: Infinite (Phase 1).
- Sale Slots: Two items are selected for sales per exploration run (Item count increases with Growth Tree).

**Key Product Examples:**

```typescript
// Potion (Small)
{
  id: "shop_potion_small",
  name: "Small Healing Potion",
  type: "consumable",
  basePrice: 50,
  effect: "Recover HP+30"
}

// Potion (Medium)
{
  id: "shop_potion_medium",
  name: "Medium Healing Potion",
  type: "consumable",
  basePrice: 120,
  effect: "Recover HP+70"
}

```

#### 2.1.2 Teleport Stones (V3.0 Update)

**Basic Specs:**

- Stock is always secured based on `return_system.md`.
- **V3.0 Change:** Unified to 1 type (100% return rate).
- Eligible for Daily Sales.

**Product Definition:**

```typescript
{
  id: "teleport_stone",
  name: "Teleport Stone",
  type: "teleport",
  basePrice: 150,
  effect: "100% Chance to Return"
}

```

> **V3.0 Note:** Previously there were 3 types (Normal 70%, Blessed 80%, Emergency 60%). These have been unified into a single Teleport Stone with 100% return rate to simplify the return system and reduce player frustration from RNG.

#### 2.1.3 Equipment Packs

**Basic Specs:**

- A "Bag" with random contents.
- Opened immediately upon purchase and items are added to inventory.
- **One pack yields items for all 6 equipment slots** (weapon, armor, helmet, boots, accessory1, accessory2).
- You get a total of 6 items, one from each slot.

**Pack Types and Probabilities:**

| Pack Name   | Price | Guaranteed Rarity | Common | Rare | Epic | Legendary |
| ----------- | ----- | ----------------- | ------ | ---- | ---- | --------- |
| Common Pack | 300G  | Common            | 100%   | 0%   | 0%   | 0%        |
| Rare Pack   | 500G  | Rare or higher    | 60%    | 35%  | 5%   | 0%        |
| Epic Pack   | 1000G | Epic or higher    | 30%    | 45%  | 20%  | 5%        |

**Lottery Logic:**

```typescript
interface EquipmentPack {
  id: string;
  name: string;
  basePrice: number;
  guaranteedRarity: 'common' | 'rare' | 'epic';
  probabilities: {
    common: number;
    rare: number;
    epic: number;
    legendary: number;
  };
}

// Example: Rare Pack
{
  id: "shop_pack_rare",
  name: "Rare Equipment Pack",
  basePrice: 500,
  guaranteedRarity: "rare",
  probabilities: {
    common: 0.60,
    rare: 0.35,
    epic: 0.05,
    legendary: 0.00
  }
}

// Processing on Open
function openEquipmentPack(pack: EquipmentPack): Item[] {
  const slots: EquipmentSlot[] = ['weapon', 'armor', 'helmet', 'boots', 'accessory1', 'accessory2'];
  const items: Item[] = [];

  for (const slot of slots) {
    const rarity = rollRarity(pack.probabilities);
    const equipment = createRandomEquipment(slot, rarity);

    // IMPORTANT: Initial Quality and Level specification
    equipment.quality = 'normal';  // Always generated as normal quality
    equipment.level = 0;           // Always generated as Lv0

    items.push(equipment);
  }

  return items; // Returns 6 items
}

/**
 * Equipment Pack Generation Rules:
 * - All equipment starts at quality: 'normal' (not poor/good/master)
 * - All equipment starts at level: 0 (not upgraded)
 * - Rarity (common/rare/epic/legendary) is determined by pack probabilities
 * - Players must use the Blacksmith to upgrade quality and level
 * - This ensures consistent starting conditions for all equipment
 */

```

---

### 2.2 Selling System

**Basic Specs:**

- Sell items currently in the player's inventory.
- **Equipped items cannot be sold** (Excluded from the list).
- Selling price uses `Item.sellPrice` directly.

**Sellable Item Filtering:**

```typescript
// List of Equipped Item IDs
const equippedIds = getEquippedIds(); // ["weapon_001", "armor_003", ...]

// Sellable Items
const sellableItems = items.filter((item) => {
  if (!item.canSell) return false; // Not sellable flag
  if (item.itemType === "equipment" && equippedIds.includes(item.id)) {
    return false; // Exclude if equipped
  }
  return true;
});
```

**Selling Process:**

```typescript
const handleSell = (item: Item) => {
  // Show Confirmation Dialog
  if (!confirm(`Sell ${item.name} for ${item.sellPrice}G?`)) {
    return;
  }

  // Remove Item
  removeItem(item.id);

  // Add Gold
  addGold(item.sellPrice);

  // Sell Effect
  playSellAnimation();
};
```

**Bulk Selling (Future Expansion):**

```typescript
// Not implemented in Phase 1
// Phase 2+: Add filters like "Select All Common Equipment"
```

---

### 2.3 Magic Stone Exchange

**Basic Specs:**

- Convert Magic Stone items into Gold.
- Different rates for the 3 types of Magic Stones.

**Magic Stone Rates:**

```typescript
const MAGIC_STONE_RATES = {
  magic_stone_small: 30, // Magic Stone (Small): 30G
  magic_stone_medium: 100, // Magic Stone (Medium): 100G
  magic_stone_large: 350, // Magic Stone (Large): 350G
};
```

**UI Design:**

- Display total value of owned Magic Stones.
- Specify the amount to exchange via slider or input box.
- Consumes stones starting from the lowest value.

**Exchange Process:**

```typescript
const handleExchangeMagicStones = (targetValue: number) => {
  const magicStones = items
    .filter((item) => item.itemType === "magicStone")
    .sort((a, b) => (a.magicStoneValue || 0) - (b.magicStoneValue || 0)); // Ascending Order of Value

  let remaining = targetValue;
  const toRemove: string[] = [];

  for (const stone of magicStones) {
    if (remaining <= 0) break;

    const stoneValue = stone.magicStoneValue || 0;
    const count = stone.stackCount || 1;
    const totalValue = stoneValue * count;

    if (totalValue <= remaining) {
      // Consume all of this stone stack
      remaining -= totalValue;
      toRemove.push(stone.id);
    } else {
      // Consume partially
      const needCount = Math.ceil(remaining / stoneValue);
      remaining = 0;

      // Reduce stack count
      updateItemStack(stone.id, count - needCount);
    }
  }

  // Remove fully consumed stones
  toRemove.forEach((id) => removeItem(id));

  // Add Gold
  addGold(targetValue);

  // Effect
  playExchangeAnimation();
};
```

---

### 2.4 Stock Restock System (V1.1 - Updated)

> **Implementation Note (V1.1):** The actual implementation uses a battle-count based restock system rather than the "3 battles" system originally described. See details below.

**Restock Trigger Conditions:**

- Restock occurs after **7-10 battles** (randomized threshold per restock cycle)
- Implemented via `RESTOCK_BATTLE_RANGE = { min: 7, max: 10 }`
- Shop tracks `battlesSinceLastRestock` to determine when to refresh stock

**Seeded RNG System:**

- Shop uses seeded random number generation for deterministic stock selection
- Seed is based on run ID and restock count, ensuring consistent results if game is reloaded
- Prevents save-scumming for better shop inventory

**Depth-Dependent Epic Drops:**

Epic consumables appear at increasing rates based on current dungeon depth:

| Depth | Epic Appearance Rate |
|-------|---------------------|
| 1     | 5%                  |
| 2     | 7%                  |
| 3     | 10%                 |
| 4     | 15%                 |
| 5     | 20%                 |

### 2.5 Daily Sales (Original Design)

**Trigger Conditions:**

- Update sale contents upon **Return** (Returning to BaseCamp).
- Reset `saleTiming = false` when **Entering a Dungeon**.

**Sale Targets:**

- Random Category (Consumables / Teleport Stones / Equipment Packs).
- Or Specific Items.
- **Epic or higher Equipment Packs are excluded from sales.**

**Discount Rate:**

- 10% ~ 30% OFF (Random).

**Data Structure:**

```typescript
interface DailySale {
  targetCategory?: "consumable" | "teleport" | "equipment_pack";
  targetItemId?: string; // If specifying a specific item
  discountRate: number; // 0.1 = 10% OFF
  excludeRarities?: string[]; // ['epic', 'legendary'] = Exclude Epic+
}

// Add to GameStateContext
interface GameState {
  // ... existing fields
  encounterCount: number; // ✨ New
  saleTiming: boolean; // ✨ New
  currentSale: DailySale | null; // ✨ New
}
```

**Sale Generation Logic:**

```typescript
function generateDailySale(): DailySale {
  const patterns = [
    // Pattern 1: Whole Category
    { targetCategory: "consumable", discountRate: 0.2 },
    { targetCategory: "teleport", discountRate: 0.15 },
    {
      targetCategory: "equipment_pack",
      discountRate: 0.1,
      excludeRarities: ["epic", "legendary"], // ✅ Exclude Epic+
    },

    // Pattern 2: Specific Item
    { targetItemId: "shop_potion_large", discountRate: 0.3 },
    { targetItemId: "shop_teleport_blessed", discountRate: 0.25 },
  ];

  return patterns[Math.floor(Math.random() * patterns.length)];
}
```

**Price Calculation:**

```typescript
function calculatePrice(
  basePrice: number,
  sale: DailySale | null,
  item: ShopItem
): number {
  if (!sale) return basePrice;

  // Exclude Epic+ Equipment Packs
  if (
    item.type === "equipment_pack" &&
    ["epic", "legendary"].includes(item.guaranteedRarity)
  ) {
    return basePrice;
  }

  // Category Sale
  if (sale.targetCategory && sale.targetCategory === item.type) {
    return Math.floor(basePrice * (1 - sale.discountRate));
  }

  // Specific Item Sale
  if (sale.targetItemId && sale.targetItemId === item.id) {
    return Math.floor(basePrice * (1 - sale.discountRate));
  }

  return basePrice;
}
```

---

## 3. UI/UX Design

### 3.1 Screen Layout

```
┌────────────────────────────────────────────────┐
│  🏪 Merchant's Exchange                        │
├────────────────────────────────────────────────┤
│                                                │
│  Gold: 1,250 G    Magic Stone Value: 450 G     │
│                                                │
│  [Buy] [Sell] [Exchange]                       │
│  ═════  ────  ──────────                       │
│                                                │
│  ┌──────────────────────────────────────────┐ │
│  │                                          │ │
│  │    Content of Selected Tab               │ │
│  │                                          │ │
│  │    [Consumables] [Teleport] [Packs]      │ │
│  │                                          │ │
│  │    ┌──────┐ ┌──────┐ ┌──────┐            │ │
│  │    │Item 1│ │Item 2│ │Item 3│            │ │
│  │    │SALE! │ │      │ │      │            │ │
│  │    │100G  │ │150G  │ │300G  │            │ │
│  │    └──────┘ └──────┘ └──────┘            │ │
│  │                                          │ │
│  └──────────────────────────────────────────┘ │
│                                                │
│  [Return to Camp]                              │
│                                                │
└────────────────────────────────────────────────┘

```

### 3.2 Buy Tab

**Category Selection:**

```
┌─────────────────────────────────────────────┐
│ [Consumables] [Teleport] [Equipment Packs]  │
│ ═══════════   ────────   ─────────────────  │
└─────────────────────────────────────────────┘

```

**Product Grid:**

```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  🧪          │  │  🧪          │  │  🧪          │
│ Small Potion │  │ Med Potion   │  │ Large Potion │
│              │  │              │  │  SALE! 20%   │
│   50 G       │  │  120 G       │  │  192 G       │
│  [Buy]       │  │  [Buy]       │  │  [Buy]       │
└──────────────┘  └──────────────┘  └──────────────┘

```

**Sale Display:**

- SALE Badge (Red background).
- Strike-through on original price.
- Discounted price displayed prominently.

### 3.3 Sell Tab

**Inventory Grid:**

```
Items Owned:

┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  ⚔️          │  │  🛡️          │  │  👑          │
│ Sword        │  │ Armor        │  │ Hat          │
│ (Equipped)   │  │              │  │              │
│   - G        │  │   80 G       │  │   120 G      │
│  [---]       │  │  [Sell]      │  │  [Sell]      │
└──────────────┘  └──────────────┘  └──────────────┘
         ↑ Cannot sell equipped items

```

**Sell Confirmation Popup:**

```
┌─────────────────────────────────┐
│  Sell Armor for 80G?            │
│                                 │
│  [Yes]  [No]                    │
└─────────────────────────────────┘

```

### 3.4 Exchange Tab

**Magic Stone List:**

```
Owned Magic Stones:

Stone (Small)  x 10  = 300 G
Stone (Medium) x  3  = 300 G
Stone (Large)  x  1  = 350 G
───────────────────────────
Total Value          = 950 G

Exchange Value: [______] G  (Max: 950G)
          OR
[Small ▼▼] [Med ▼▼] [Large ▼▼]  Specify Count

Gold Acquired: 450 G

[Exchange]  [Cancel]

```

### 3.5 Visuals (Feedback)

**Buy Success:**

```
Sound: Cha-ching! (Coin sound)
Animation:
  1. Product card zooms up.
  2. Enters a bag.
  3. Flies towards the player.

```

**Opening Equipment Pack:**

```
Flow:
1. Buy button pressed.
   ↓
2. A bag appears in the center of the screen.
   ↓
3. Shakes violently (1 second).
   ↓
4. 6 Equipment icons burst out with light.
   (Light color corresponds to rarity)
   - Common: White
   - Rare: Blue
   - Epic: Purple
   - Legendary: Gold
   ↓
5. Display each equipment in order (0.5s interval).
   ↓
6. Added to inventory.

```

**Magic Stone Exchange:**

```
Sound: Shatter! (Glass breaking sound)
Animation:
  1. Magic Stone shatters.
  2. Turns into Gold coins.
  3. Gold counter ticks up.

```

---

## 4. Data Structure Definition

### 4.1 ShopTypes.ts

```typescript
// src/types/ShopTypes.ts (New File)

import type { ItemType, EquipmentSlot } from "./ItemTypes";

/**
 * Shop Item Data
 */
export interface ShopItem {
  id: string; // Shop Item ID
  targetItemId?: string; // Actual Item ID (Non-Pack)
  name: string;
  description: string;
  type: "consumable" | "teleport" | "equipment_pack";
  basePrice: number;
  icon: string;

  // Equipment Pack Configuration
  packConfig?: EquipmentPackConfig;
}

export interface EquipmentPackConfig {
  guaranteedRarity: "common" | "rare" | "epic";
  probabilities: {
    common: number;
    rare: number;
    epic: number;
    legendary: number;
  };
}

/**
 * Sale Information
 */
export interface DailySale {
  targetCategory?: "consumable" | "teleport" | "equipment_pack";
  targetItemId?: string;
  discountRate: number; // 0.1 = 10% OFF
  excludeRarities?: ("epic" | "legendary")[]; // Excluded Rarities
}

/**
 * Shop Categories
 */
export type ShopCategory = "consumable" | "teleport" | "equipment_pack";

/**
 * Magic Stone Exchange Info
 */
export interface MagicStoneExchange {
  totalValue: number; // Total value of owned stones
  breakdown: {
    typeId: string;
    count: number;
    unitValue: number;
    totalValue: number;
  }[];
}
```

### 4.2 GameStateContext Extension

```typescript
// src/contexts/GameStateContext.tsx (Modification)

export interface GameState {
  currentScreen: GameScreen;
  battleMode: BattleMode;
  depth: Depth;
  encounterCount: number; // ✨ New: Combat Count
  battleConfig?: BattleConfig;

  // For Shop
  saleTiming: boolean; // ✨ New: Sale update flag
  currentSale: DailySale | null; // ✨ New: Current Sale
}

// Initial Values
const initialGameState: GameState = {
  currentScreen: "camp",
  battleMode: null,
  depth: 1,
  encounterCount: 0, // ✨ Starts at 0
  saleTiming: false, // ✨ Initially false
  currentSale: null, // ✨ No sale
};
```

---

## 5. Data File Definition

### 5.1 ShopData.ts

```typescript
// src/camps/facilities/Shop/data/ShopData.ts (New File)

import type { ShopItem } from "../../../../types/ShopTypes";

/**
 * Consumable Category
 */
export const CONSUMABLE_ITEMS: ShopItem[] = [
  {
    id: "shop_potion_small",
    targetItemId: "potion_small",
    name: "Small Potion",
    description: "Recovers 30 HP",
    type: "consumable",
    basePrice: 50,
    icon: "🧪",
  },
  {
    id: "shop_potion_medium",
    targetItemId: "potion_medium",
    name: "Medium Potion",
    description: "Recovers 70 HP",
    type: "consumable",
    basePrice: 120,
    icon: "🧪",
  },
  {
    id: "shop_potion_large",
    targetItemId: "potion_large",
    name: "Large Potion",
    description: "Recovers 150 HP",
    type: "consumable",
    basePrice: 240,
    icon: "🧪",
  },
  // ... Other consumables
];

/**
 * Teleport Items Category
 */
export const TELEPORT_ITEMS: ShopItem[] = [
  {
    id: "shop_teleport_normal",
    targetItemId: "teleport_normal",
    name: "Teleport Stone (Normal)",
    description: "70% Chance to Return",
    type: "teleport",
    basePrice: 150,
    icon: "🔮",
  },
  {
    id: "shop_teleport_blessed",
    targetItemId: "teleport_blessed",
    name: "Teleport Stone (Blessed)",
    description: "80% Chance to Return",
    type: "teleport",
    basePrice: 300,
    icon: "✨",
  },
  {
    id: "shop_teleport_emergency",
    targetItemId: "teleport_emergency",
    name: "Teleport Stone (Emergency)",
    description: "60% Chance (Low Cost)",
    type: "teleport",
    basePrice: 100,
    icon: "⚡",
  },
];

/**
 * Equipment Packs Category
 */
export const EQUIPMENT_PACKS: ShopItem[] = [
  {
    id: "shop_pack_common",
    name: "Common Pack",
    description: "6 Items (Guaranteed Common)",
    type: "equipment_pack",
    basePrice: 300,
    icon: "📦",
    packConfig: {
      guaranteedRarity: "common",
      probabilities: {
        common: 1.0,
        rare: 0.0,
        epic: 0.0,
        legendary: 0.0,
      },
    },
  },
  {
    id: "shop_pack_rare",
    name: "Rare Pack",
    description: "6 Items (Guaranteed Rare+)",
    type: "equipment_pack",
    basePrice: 500,
    icon: "📦",
    packConfig: {
      guaranteedRarity: "rare",
      probabilities: {
        common: 0.6,
        rare: 0.35,
        epic: 0.05,
        legendary: 0.0,
      },
    },
  },
  {
    id: "shop_pack_epic",
    name: "Epic Pack",
    description: "6 Items (Guaranteed Epic+)",
    type: "equipment_pack",
    basePrice: 1000,
    icon: "📦",
    packConfig: {
      guaranteedRarity: "epic",
      probabilities: {
        common: 0.3,
        rare: 0.45,
        epic: 0.2,
        legendary: 0.05,
      },
    },
  },
];

/**
 * All Shop Items List
 */
export const ALL_SHOP_ITEMS: ShopItem[] = [
  ...CONSUMABLE_ITEMS,
  ...TELEPORT_ITEMS,
  ...EQUIPMENT_PACKS,
];

/**
 * Get Items By Category
 */
export function getItemsByCategory(category: ShopCategory): ShopItem[] {
  switch (category) {
    case "consumable":
      return CONSUMABLE_ITEMS;
    case "teleport":
      return TELEPORT_ITEMS;
    case "equipment_pack":
      return EQUIPMENT_PACKS;
    default:
      return [];
  }
}
```

### 5.2 MagicStoneData.ts

```typescript
// src/items/data/MagicStoneData.ts (New File)

import type { Item } from "../../types/ItemTypes";

/**
 * Magic Stone Item Data
 */
export const MAGIC_STONE_ITEMS: Item[] = [
  {
    id: "magic_stone_small_001", // Actual ID (Instance)
    typeId: "magic_stone_small", // Type ID (Identifier)
    name: "Magic Stone (Small)",
    description: "Small stone with faint magic.",
    itemType: "magicStone",
    icon: "💎",
    magicStoneValue: 30, // ✅ 30G
    rarity: "common",
    sellPrice: 30, // Sell Price = Magic Stone Value
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1,
  },
  {
    id: "magic_stone_medium_001",
    typeId: "magic_stone_medium",
    name: "Magic Stone (Medium)",
    description: "Stone glowing dimly.",
    itemType: "magicStone",
    icon: "💎",
    magicStoneValue: 100, // ✅ 100G
    rarity: "uncommon",
    sellPrice: 100,
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1,
  },
  {
    id: "magic_stone_large_001",
    typeId: "magic_stone_large",
    name: "Magic Stone (Large)",
    description: "Precious stone emitting strong magic.",
    itemType: "magicStone",
    icon: "💎",
    magicStoneValue: 350, // ✅ 350G
    rarity: "rare",
    sellPrice: 350,
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1,
  },
];

/**
 * Exchange Rate Definition
 */
export const MAGIC_STONE_RATES: Record<string, number> = {
  magic_stone_small: 30,
  magic_stone_medium: 100,
  magic_stone_large: 350,
};

/**
 * Calculate Total Value of Magic Stones
 */
export function calculateMagicStoneValue(items: Item[]): number {
  return items
    .filter((item) => item.itemType === "magicStone")
    .reduce((sum, item) => {
      const value = item.magicStoneValue || 0;
      const count = item.stackCount || 1;
      return sum + value * count;
    }, 0);
}
```

---

## 6. Implementation Procedure

### Phase 1: Data Preparation (Week 1: Day 1-2)

**Task 1.1: Type Definition Creation**

```
□ Create src/types/ShopTypes.ts
  □ ShopItem Type
  □ DailySale Type
  □ MagicStoneExchange Type

```

**Task 1.2: GameStateContext Extension**

```
□ Modify src/contexts/GameStateContext.tsx
  □ Add encounterCount
  □ Add saleTiming
  □ Add currentSale
  □ Add incrementEncounterCount function
  □ Add updateSale function

```

**Task 1.3: Product Data Creation**

```
□ Create src/camps/facilities/Shop/data/ShopData.ts
  □ Define CONSUMABLE_ITEMS
  □ Define TELEPORT_ITEMS
  □ Define EQUIPMENT_PACKS
  □ getItemsByCategory function

```

**Task 1.4: Magic Stone Data Creation**

```
□ Create src/items/data/MagicStoneData.ts
  □ Define MAGIC_STONE_ITEMS
  □ Define MAGIC_STONE_RATES
  □ calculateMagicStoneValue function

```

---

### Phase 2: Shop Component Implementation (Week 1-2: Day 3-7)

**Task 2.1: Shop.tsx Skeleton**

```typescript
// src/camps/facilities/Shop/Shop.tsx

import { useState } from "react";
import { usePlayer } from "../../../contexts/PlayerContext";
import { useGameState } from "../../../contexts/GameStateContext";
import { useInventory } from "../../../contexts/InventoryContext";
import BuyTab from "./BuyTab";
import SellTab from "./SellTab";
import ExchangeTab from "./ExchangeTab";
import "./Shop.css";

type ShopTab = "buy" | "sell" | "exchange";

const Shop: React.FC = () => {
  const [activeTab, setActiveTab] = useState<ShopTab>("buy");
  const { returnToCamp } = useGameState();

  return (
    <div className="shop-screen">
      <header className="shop-header">
        <h1>🏪 Merchant's Exchange</h1>
        <div className="resources">
          <PlayerResources />
        </div>
      </header>

      <nav className="shop-tabs">
        <button
          className={activeTab === "buy" ? "active" : ""}
          onClick={() => setActiveTab("buy")}
        >
          Buy
        </button>
        <button
          className={activeTab === "sell" ? "active" : ""}
          onClick={() => setActiveTab("sell")}
        >
          Sell
        </button>
        <button
          className={activeTab === "exchange" ? "active" : ""}
          onClick={() => setActiveTab("exchange")}
        >
          Exchange
        </button>
      </nav>

      <div className="shop-content">
        {activeTab === "buy" && <BuyTab />}
        {activeTab === "sell" && <SellTab />}
        {activeTab === "exchange" && <ExchangeTab />}
      </div>

      <button className="back-button" onClick={returnToCamp}>
        Return to Camp
      </button>
    </div>
  );
};

export default Shop;
```

**Task 2.2: BuyTab Implementation**

```
□ Create src/camps/facilities/Shop/BuyTab.tsx
  □ Category selection UI
  □ Product Grid display
  □ Sale price calculation
  □ Purchase processing
  □ Equipment Pack opening (Fixed equipment for Phase 1)

```

**Task 2.3: SellTab Implementation**

```
□ Create src/camps/facilities/Shop/SellTab.tsx
  □ Inventory display
  □ Equipped filtering
  □ Sell confirmation dialog
  □ Sell processing

```

**Task 2.4: ExchangeTab Implementation**

```
□ Create src/camps/facilities/Shop/ExchangeTab.tsx
  □ Magic Stone list display
  □ Exchange amount input UI
  □ Exchange processing

```

---

### Phase 3: Equipment Generation System (Week 2: Day 1-3)

**Task 3.1: equipmentGenerator.ts Creation**

```typescript
// src/items/utils/equipmentGenerator.ts (New File)

import type { Item, EquipmentSlot } from "../../types/ItemTypes";

/**
 * Generate Random Equipment
 */
export function createRandomEquipment(
  slot: EquipmentSlot,
  rarity: "common" | "rare" | "epic" | "legendary"
): Item {
  // Phase 1: Temporary implementation (Returns fixed equipment)
  // Phase 2: Lottery from EQUIPMENT_AND_ITEMS_DESIGN.md

  const equipmentPool = getEquipmentPoolBySlotAndRarity(slot, rarity);
  const template = selectRandom(equipmentPool);

  return {
    id: generateUniqueId(),
    typeId: template.id,
    name: template.name,
    description: template.description,
    itemType: "equipment",
    icon: template.icon,
    equipmentSlot: slot,
    durability: template.maxDurability,
    maxDurability: template.maxDurability,
    effects: template.effects,
    rarity: rarity,
    sellPrice: template.sellPrice,
    canSell: true,
    canDiscard: false,
  };
}

/**
 * Roll Rarity
 */
export function rollRarity(probabilities: {
  common: number;
  rare: number;
  epic: number;
  legendary: number;
}): "common" | "rare" | "epic" | "legendary" {
  const roll = Math.random();
  let cumulative = 0;

  for (const [rarity, prob] of Object.entries(probabilities)) {
    cumulative += prob;
    if (roll < cumulative) {
      return rarity as "common" | "rare" | "epic" | "legendary";
    }
  }

  return "common"; // Fallback
}

/**
 * Open Equipment Pack
 */
export function openEquipmentPack(pack: EquipmentPackConfig): Item[] {
  const slots: EquipmentSlot[] = [
    "weapon",
    "armor",
    "helmet",
    "boots",
    "accessory1",
    "accessory2",
  ];

  const items: Item[] = [];

  for (const slot of slots) {
    const rarity = rollRarity(pack.probabilities);
    const equipment = createRandomEquipment(slot, rarity);
    items.push(equipment);
  }

  return items; // 6 Items
}
```

**Task 3.2: EquipmentData.ts Creation (Phase 1: Simplified)**

```
□ Create src/items/data/EquipmentData.ts
  □ Basic equipment definitions for each slot x rarity
  □ Extract from EQUIPMENT_AND_ITEMS_DESIGN.md
  □ Incomplete data can be postponed

```

---

### Phase 4: Sale System Integration (Week 2: Day 4-5)

**Task 4.1: Sale Generation Logic**

```typescript
// src/camps/facilities/Shop/utils/saleGenerator.ts

import type { DailySale } from "../../../../types/ShopTypes";

export function generateDailySale(): DailySale {
  const patterns: DailySale[] = [
    {
      targetCategory: "consumable",
      discountRate: 0.2,
    },
    {
      targetCategory: "teleport",
      discountRate: 0.15,
    },
    {
      targetCategory: "equipment_pack",
      discountRate: 0.1,
      excludeRarities: ["epic", "legendary"], // ✅ Epic+ Excluded
    },
    // Specific Items
    {
      targetItemId: "shop_potion_large",
      discountRate: 0.3,
    },
    {
      targetItemId: "shop_teleport_blessed",
      discountRate: 0.25,
    },
  ];

  return patterns[Math.floor(Math.random() * patterns.length)];
}

export function calculateDiscountedPrice(
  basePrice: number,
  sale: DailySale | null,
  item: ShopItem
): number {
  if (!sale) return basePrice;

  // Exclude Epic+ Equipment Packs
  if (item.type === "equipment_pack" && item.packConfig) {
    if (["epic", "legendary"].includes(item.packConfig.guaranteedRarity)) {
      return basePrice;
    }
  }

  // Category Sale
  if (sale.targetCategory === item.type) {
    return Math.floor(basePrice * (1 - sale.discountRate));
  }

  // Specific Item Sale
  if (sale.targetItemId === item.id) {
    return Math.floor(basePrice * (1 - sale.discountRate));
  }

  return basePrice;
}
```

**Task 4.2: BattleScreen Integration**

```
□ Modify BattleScreen.tsx
  □ Increment encounterCount upon battle end
  □ Set saleTiming = true if encounterCount >= 3

```

**Task 4.3: BaseCamp Integration**

```
□ Modify BaseCamp.tsx
  □ Check saleTiming on mount
  □ If true, update Sale
  □ Set saleTiming = false upon clicking Dungeon facility

```

---

### Phase 5: UI/Animations (Week 3: Day 1-3)

**Task 5.1: CSS Implementation**

```
□ Create Shop.css
  □ Product Grid Layout
  □ SALE Badge Style
  □ Hover Effects

```

**Task 5.2: Animation Implementation**

```
□ Purchase Effects
  □ Coin animation
  □ Item acquired visual

□ Equipment Pack Opening Visual
  □ Shaking bag animation
  □ Light effects
  □ Equipment icon appearance

□ Magic Stone Exchange Effect
  □ Stone shattering animation
  □ Gold count up

```

---

## 7. Integration with Context API

### 7.1 PlayerContext

```typescript
// Functions used
const { player, addGold, useGold } = usePlayer();

// On Buy
if (useGold(price)) {
  // Purchase Success
} else {
  // Insufficient Gold
}

// On Sell
addGold(item.sellPrice);
```

### 7.2 InventoryContext

```typescript
// Functions used
const {
  items,
  addItem,
  removeItem,
  getEquippedIds,
  getMagicStones,
  useMagicStones,
} = useInventory();

// On Buy
addItem(newItem);

// On Sell
removeItem(itemId);

// Check Equipped
const equippedIds = getEquippedIds();
const isEquipped = equippedIds.includes(item.id);

// Magic Stone Exchange
const totalMagicStoneValue = getMagicStones();
useMagicStones(350); // Consume 350G worth of stones
```

### 7.3 GameStateContext

```typescript
// Functions used
const { gameState, setGameState, returnToCamp } = useGameState();

// Check Sale
const { currentSale, saleTiming, encounterCount } = gameState;

// Increment Encounter Count (Executed in BattleScreen)
setGameState((prev) => ({
  ...prev,
  encounterCount: prev.encounterCount + 1,
  saleTiming: prev.encounterCount + 1 >= 3,
}));

// Update Sale (Executed in BaseCamp)
if (saleTiming) {
  const newSale = generateDailySale();
  setGameState((prev) => ({
    ...prev,
    currentSale: newSale,
    saleTiming: false,
  }));
}

// On Dungeon Entry (Executed in BaseCamp)
setGameState((prev) => ({
  ...prev,
  saleTiming: false,
}));
```

---

## 8. Test Items

### 8.1 Buy System Test

```
□ Consumable Purchase
  □ Gold deduction
  □ Item addition
  □ Insufficient Gold error

□ Teleport Stone Purchase
  □ Normal purchase
  □ Inventory addition verification

□ Equipment Pack Purchase
  □ Pack opening
  □ Acquisition of 6 items
  □ Rarity probability verification

```

### 8.2 Sell System Test

```
□ Equipment Sale
  □ Gold addition
  □ Item removal

□ Equipped Filtering
  □ Hidden while equipped
  □ Shown after unequip

```

### 8.3 Magic Stone Exchange Test

```
□ Magic Stone Exchange
  □ Correct rate calculation
  □ Consumed in order of lowest value
  □ Gold addition

```

### 8.4 Sale System Test

```
□ Sale Trigger
  □ encounterCount >= 3
  □ Update on Return
  □ Reset on Dungeon Entry

□ Sale Application
  □ Category Sale
  □ Specific Item Sale
  □ Epic+ Exclusion

```

---

## 9. Notes

### 9.1 Implementation vs Design Differences (V1.1)

The following differences exist between the original design and actual implementation:

| Aspect | Original Design | Implementation |
|--------|-----------------|----------------|
| Restock Timing | 3 battles after return | 7-10 battles (randomized) |
| RNG | Standard random | Seeded RNG (deterministic per run) |
| Epic Items | Not specified | Depth-dependent probability |
| Stock Structure | Single inventory | Permanent + Daily Special separation |

**Stock System Structure:**

```typescript
interface ShopStockState {
  permanentStock: Map<string, number>;     // Always available items
  dailySpecialStock: Map<string, number>;  // Rotating special items
  epicSlot: { key: string; stock: number } | null;  // Depth-scaled epic item
  seed: number;                             // For deterministic RNG
  restockThreshold: number;                 // 7-10 battles until next restock
  battlesSinceLastRestock: number;          // Current battle count
}
```

### 9.2 Data Incompleteness

- Refer to `EQUIPMENT_AND_ITEMS_DESIGN.md` for equipment data.
- Buff/Debuff details postponed.
- Consumable effects are simple implementations.

### 9.2 Future Expansion

- Stock limit system.
- Bulk sell function.
- Quality System.
- Conversations with Shop NPC.

### 9.3 Implementation Priority

```
Phase 1 (Top Priority):
- Basic Buy/Sell
- Fixed Equipment Pack
- Simple Sales

Phase 2 (Medium Priority):
- Probability Lottery System
- Equipment Pack Visuals
- Magic Stone Exchange

Phase 3 (Low Priority):
- Advanced Animations
- Stock Limits
- Bulk Sell

```

---

## 10. Reference Documents

```
BASE_CAMP_DESIGN_V1
├── GUILD_DESIGN_V2.1
└── SHOP_DESIGN_V1 [This Document]
    ├── ShopData.ts [Product Data]
    ├── MagicStoneData.ts [Magic Stone Data]
    ├── equipmentGenerator.ts [Equipment Generation]
    └── return_system.md [Teleport System]

```
