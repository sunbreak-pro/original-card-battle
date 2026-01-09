# Shop Implementation Guide (SHOP_IMPLEMENTATION_GUIDE_V1)

## 0. Prerequisites

### 0.1 Tasks That Must Be Completed

- ✅ BaseCamp Overall Design (Context API Implemented)
- ✅ Item Type System Introduction
- ✅ InventoryContext Implementation

### 0.2 Dependency Relationships

```
GameStateContext (encounterCount, saleTiming, currentSale)
  ↓
PlayerContext (gold, useGold, addGold)
  ↓
InventoryContext (items, addItem, removeItem, getEquippedIds)
  ↓
Shop Components (BuyTab, SellTab, ExchangeTab)

```

---

## Phase 1: Data and Type Preparation (Week 1: Day 1-2)

### Task 1.1: Create ShopTypes.ts

**Priority:** 🔴 Highest

```bash
# Create directory
mkdir -p src/types

```

```typescript
// src/types/ShopTypes.ts (New File)

import type { ItemType, EquipmentSlot } from "./ItemTypes";

/**
 * Shop Item Data
 */
export interface ShopItem {
  id: string;
  targetItemId?: string;
  name: string;
  description: string;
  type: "consumable" | "teleport" | "equipment_pack";
  basePrice: number;
  icon: string;
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
  discountRate: number;
  excludeRarities?: ("epic" | "legendary")[];
}

export type ShopCategory = "consumable" | "teleport" | "equipment_pack";

export interface MagicStoneExchange {
  totalValue: number;
  breakdown: {
    typeId: string;
    count: number;
    unitValue: number;
    totalValue: number;
  }[];
}
```

**✅ Completion Check:**

- [ ] ShopTypes.ts created.
- [ ] No compilation errors.

---

### Task 1.2: Extend GameStateContext

```typescript
// src/contexts/GameStateContext.tsx (Modification)

import type { DailySale } from "../types/ShopTypes";

export interface GameState {
  currentScreen: GameScreen;
  battleMode: BattleMode;
  depth: Depth;
  encounterCount: number; // ✨ New Addition
  battleConfig?: BattleConfig;

  // For Shop
  saleTiming: boolean; // ✨ New Addition
  currentSale: DailySale | null; // ✨ New Addition
}

export const GameStateProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const [gameState, setGameState] = useState<GameState>({
    currentScreen: "camp",
    battleMode: null,
    depth: 1,
    encounterCount: 0, // ✨ Initial Value
    saleTiming: false, // ✨ Initial Value
    currentSale: null, // ✨ Initial Value
  });

  // ... Existing code

  // ✨ New Addition: Increment Encounter Count
  const incrementEncounterCount = () => {
    setGameState((prev) => {
      const newCount = prev.encounterCount + 1;
      return {
        ...prev,
        encounterCount: newCount,
        saleTiming: newCount >= 3, // Sale update flag if 3 or more
      };
    });
  };

  // ✨ New Addition: Update Sale
  const updateSale = (sale: DailySale | null) => {
    setGameState((prev) => ({
      ...prev,
      currentSale: sale,
      saleTiming: false, // Reset flag
    }));
  };

  // ✨ New Addition: Process on Entering Dungeon
  const enterDungeon = () => {
    setGameState((prev) => ({
      ...prev,
      currentScreen: "dungeon",
      saleTiming: false, // Reset sale flag
    }));
  };

  return (
    <GameStateContext.Provider
      value={{
        gameState,
        setGameState,
        navigateTo,
        startBattle,
        returnToCamp,
        incrementEncounterCount, // ✨ Added
        updateSale, // ✨ Added
        enterDungeon, // ✨ Added
      }}
    >
      {children}
    </GameStateContext.Provider>
  );
};
```

**✅ Completion Check:**

- [ ] encounterCount added.
- [ ] saleTiming added.
- [ ] currentSale added.
- [ ] incrementEncounterCount implemented.

---

### Task 1.3: Create MagicStoneData.ts

```bash
mkdir -p src/items/data

```

```typescript
// src/items/data/MagicStoneData.ts (New File)

import type { Item } from "../../types/ItemTypes";

export const MAGIC_STONE_ITEMS: Item[] = [
  {
    id: "magic_stone_small_001",
    typeId: "magic_stone_small",
    name: "Magic Stone (Small)",
    description: "Small stone with faint magic.",
    itemType: "magicStone",
    icon: "💎",
    magicStoneValue: 30,
    rarity: "common",
    sellPrice: 30,
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
    magicStoneValue: 100,
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
    magicStoneValue: 350,
    rarity: "rare",
    sellPrice: 350,
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1,
  },
];

export const MAGIC_STONE_RATES: Record<string, number> = {
  magic_stone_small: 30,
  magic_stone_medium: 100,
  magic_stone_large: 350,
};

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

**✅ Completion Check:**

- [ ] MagicStoneData.ts created.
- [ ] 3 types of Magic Stone data defined.
- [ ] calculateMagicStoneValue implemented.

---

### Task 1.4: Create ShopData.ts

```bash
mkdir -p src/camps/facilities/Shop/data

```

```typescript
// src/camps/facilities/Shop/data/ShopData.ts (New File)

import type { ShopItem, ShopCategory } from "../../../../types/ShopTypes";

/**
 * Consumable Category
 */
export const CONSUMABLE_ITEMS: ShopItem[] = [
  {
    id: "shop_potion_small",
    targetItemId: "potion_small",
    name: "Small Healing Potion",
    description: "Recovers 30 HP",
    type: "consumable",
    basePrice: 50,
    icon: "🧪",
  },
  {
    id: "shop_potion_medium",
    targetItemId: "potion_medium",
    name: "Medium Healing Potion",
    description: "Recovers 70 HP",
    type: "consumable",
    basePrice: 120,
    icon: "🧪",
  },
  {
    id: "shop_potion_large",
    targetItemId: "potion_large",
    name: "Large Healing Potion",
    description: "Recovers 150 HP",
    type: "consumable",
    basePrice: 240,
    icon: "🧪",
  },
];

/**
 * Teleport Stone Category
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
    description: "60% Chance to Return",
    type: "teleport",
    basePrice: 100,
    icon: "⚡",
  },
];

/**
 * Equipment Pack Category
 */
export const EQUIPMENT_PACKS: ShopItem[] = [
  {
    id: "shop_pack_common",
    name: "Common Equipment Pack",
    description: "6 items (Guaranteed Common)",
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
    name: "Rare Equipment Pack",
    description: "6 items (Guaranteed Rare+)",
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
    name: "Epic Equipment Pack",
    description: "6 items (Guaranteed Epic+)",
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

export const ALL_SHOP_ITEMS: ShopItem[] = [
  ...CONSUMABLE_ITEMS,
  ...TELEPORT_ITEMS,
  ...EQUIPMENT_PACKS,
];

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

**✅ Completion Check:**

- [ ] ShopData.ts created.
- [ ] Products for 3 categories defined.
- [ ] getItemsByCategory implemented.

---

## Phase 2: Shop Component Implementation (Week 1-2: Day 3-7)

### Task 2.1: Shop.tsx Skeleton

```bash
mkdir -p src/camps/facilities/Shop

```

```typescript
// src/camps/facilities/Shop/Shop.tsx (New File)

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
  const { player } = usePlayer();
  const { returnToCamp } = useGameState();
  const { items } = useInventory();

  // Calculate total Magic Stone value
  const magicStoneValue = items
    .filter((item) => item.itemType === "magicStone")
    .reduce((sum, item) => {
      const value = item.magicStoneValue || 0;
      const count = item.stackCount || 1;
      return sum + value * count;
    }, 0);

  return (
    <div className="shop-screen">
      <header className="shop-header">
        <h1>🏪 Merchant's Exchange</h1>
        <div className="resources">
          <div className="gold">💰 {player.gold} G</div>
          <div className="magic-stones">💎 {magicStoneValue} G Equivalent</div>
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

**✅ Completion Check:**

- [ ] Shop.tsx created.
- [ ] Tab switching works.
- [ ] Resource display is correct.

---

### Task 2.2: BuyTab Implementation

```typescript
// src/camps/facilities/Shop/BuyTab.tsx (New File)

import { useState } from "react";
import { usePlayer } from "../../../contexts/PlayerContext";
import { useGameState } from "../../../contexts/GameStateContext";
import { useInventory } from "../../../contexts/InventoryContext";
import { getItemsByCategory } from "./data/ShopData";
import { calculateDiscountedPrice } from "./utils/saleCalculator";
import type { ShopCategory, ShopItem } from "../../../types/ShopTypes";
import "./BuyTab.css";

const BuyTab: React.FC = () => {
  const [selectedCategory, setSelectedCategory] =
    useState<ShopCategory>("consumable");
  const { player, useGold } = usePlayer();
  const { gameState } = useGameState();
  const { addItem } = useInventory();

  const items = getItemsByCategory(selectedCategory);
  const { currentSale } = gameState;

  const handleBuy = (shopItem: ShopItem) => {
    const price = calculateDiscountedPrice(
      shopItem.basePrice,
      currentSale,
      shopItem
    );

    if (player.gold < price) {
      alert("Not enough Gold!");
      return;
    }

    if (!confirm(`Buy ${shopItem.name} for ${price}G?`)) {
      return;
    }

    // Pay Gold
    if (!useGold(price)) {
      alert("Purchase failed");
      return;
    }

    // Grant Items (Phase 1: Simple Implementation)
    if (shopItem.type === "equipment_pack") {
      // Phase 1 grants 6 fixed items
      // Phase 2 implements probability lottery
      alert("Equipment pack opened! (Phase 1: Simple Implementation)");
      // TODO: openEquipmentPack(shopItem.packConfig)
    } else {
      // Consumables/Teleport Stones
      // TODO: createItemFromId(shopItem.targetItemId)
      alert(`${shopItem.name} purchased!`);
    }
  };

  return (
    <div className="buy-tab">
      {/* Category Selection */}
      <nav className="category-tabs">
        <button
          className={selectedCategory === "consumable" ? "active" : ""}
          onClick={() => setSelectedCategory("consumable")}
        >
          Consumables
        </button>
        <button
          className={selectedCategory === "teleport" ? "active" : ""}
          onClick={() => setSelectedCategory("teleport")}
        >
          Teleport
        </button>
        <button
          className={selectedCategory === "equipment_pack" ? "active" : ""}
          onClick={() => setSelectedCategory("equipment_pack")}
        >
          Packs
        </button>
      </nav>

      {/* Product Grid */}
      <div className="items-grid">
        {items.map((item) => {
          const price = calculateDiscountedPrice(
            item.basePrice,
            currentSale,
            item
          );
          const isOnSale = price < item.basePrice;

          return (
            <div key={item.id} className="shop-item-card">
              <div className="item-icon">{item.icon}</div>
              <div className="item-name">{item.name}</div>
              <div className="item-description">{item.description}</div>

              <div className="item-price">
                {isOnSale && (
                  <>
                    <span className="sale-badge">SALE!</span>
                    <span className="original-price">{item.basePrice} G</span>
                  </>
                )}
                <span
                  className={isOnSale ? "discounted-price" : "normal-price"}
                >
                  {price} G
                </span>
              </div>

              <button
                className="buy-button"
                onClick={() => handleBuy(item)}
                disabled={player.gold < price}
              >
                {player.gold < price ? "No Gold" : "Buy"}
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default BuyTab;
```

**✅ Completion Check:**

- [ ] BuyTab.tsx created.
- [ ] Category selection works.
- [ ] Product grid displayed.
- [ ] Purchase process works (Simple version).

---

### Task 2.3: Create saleCalculator.ts

```typescript
// src/camps/facilities/Shop/utils/saleCalculator.ts (New File)

import type { DailySale, ShopItem } from "../../../../types/ShopTypes";

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
      excludeRarities: ["epic", "legendary"],
    },
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
```

**✅ Completion Check:**

- [ ] saleCalculator.ts created.
- [ ] calculateDiscountedPrice implemented.
- [ ] generateDailySale implemented.

---

### Task 2.4: SellTab Implementation

```typescript
// src/camps/facilities/Shop/SellTab.tsx (New File)

import { usePlayer } from "../../../contexts/PlayerContext";
import { useInventory } from "../../../contexts/InventoryContext";
import "./SellTab.css";

const SellTab: React.FC = () => {
  const { addGold } = usePlayer();
  const { items, removeItem, getEquippedIds } = useInventory();

  const equippedIds = getEquippedIds();

  // Filter sellable items
  const sellableItems = items.filter((item) => {
    if (!item.canSell) return false;
    if (item.itemType === "equipment" && equippedIds.includes(item.id)) {
      return false; // Exclude if equipped
    }
    return true;
  });

  const handleSell = (item: any) => {
    if (!confirm(`Sell ${item.name} for ${item.sellPrice}G?`)) {
      return;
    }

    // Remove Item
    removeItem(item.id);

    // Add Gold
    addGold(item.sellPrice);

    alert(`${item.name} sold!`);
  };

  return (
    <div className="sell-tab">
      <h2>Items Owned</h2>

      {sellableItems.length === 0 && (
        <p className="no-items">No sellable items.</p>
      )}

      <div className="items-grid">
        {sellableItems.map((item) => {
          const isEquipped = equippedIds.includes(item.id);

          return (
            <div key={item.id} className="sell-item-card">
              <div className="item-icon">{item.icon}</div>
              <div className="item-name">{item.name}</div>
              <div className="item-description">{item.description}</div>

              {isEquipped && <div className="equipped-label">(Equipped)</div>}

              <div className="item-sell-price">{item.sellPrice} G</div>

              <button
                className="sell-button"
                onClick={() => handleSell(item)}
                disabled={isEquipped}
              >
                {isEquipped ? "Equipped" : "Sell"}
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default SellTab;
```

**✅ Completion Check:**

- [ ] SellTab.tsx created.
- [ ] Equipped item filtering works.
- [ ] Sell process works.

---

### Task 2.5: ExchangeTab Implementation

```typescript
// src/camps/facilities/Shop/ExchangeTab.tsx (New File)

import { useState } from "react";
import { usePlayer } from "../../../contexts/PlayerContext";
import { useInventory } from "../../../contexts/InventoryContext";
import "./ExchangeTab.css";

const ExchangeTab: React.FC = () => {
  const [exchangeValue, setExchangeValue] = useState(0);
  const { addGold } = usePlayer();
  const { items, removeItem, updateItemStack } = useInventory();

  // Get Magic Stone List
  const magicStones = items
    .filter((item) => item.itemType === "magicStone")
    .sort((a, b) => (a.magicStoneValue || 0) - (b.magicStoneValue || 0));

  const totalValue = magicStones.reduce((sum, stone) => {
    const value = stone.magicStoneValue || 0;
    const count = stone.stackCount || 1;
    return sum + value * count;
  }, 0);

  const handleExchange = () => {
    if (exchangeValue <= 0 || exchangeValue > totalValue) {
      alert("Invalid exchange amount");
      return;
    }

    if (!confirm(`Exchange Magic Stones for ${exchangeValue}G?`)) {
      return;
    }

    let remaining = exchangeValue;
    const toRemove: string[] = [];

    for (const stone of magicStones) {
      if (remaining <= 0) break;

      const stoneValue = stone.magicStoneValue || 0;
      const count = stone.stackCount || 1;
      const totalStoneValue = stoneValue * count;

      if (totalStoneValue <= remaining) {
        // Consume all of this stone
        remaining -= totalStoneValue;
        toRemove.push(stone.id);
      } else {
        // Partial consume
        const needCount = Math.ceil(remaining / stoneValue);
        remaining = 0;

        // Reduce stack count
        updateItemStack(stone.id, count - needCount);
      }
    }

    // Remove Stones
    toRemove.forEach((id) => removeItem(id));

    // Add Gold
    addGold(exchangeValue);

    // Reset
    setExchangeValue(0);
    alert(`${exchangeValue}G Acquired!`);
  };

  return (
    <div className="exchange-tab">
      <h2>Magic Stones Owned</h2>

      {magicStones.length === 0 && (
        <p className="no-stones">No Magic Stones owned.</p>
      )}

      <div className="magic-stones-list">
        {magicStones.map((stone) => {
          const value = stone.magicStoneValue || 0;
          const count = stone.stackCount || 1;
          const total = value * count;

          return (
            <div key={stone.id} className="magic-stone-item">
              <span className="stone-icon">{stone.icon}</span>
              <span className="stone-name">{stone.name}</span>
              <span className="stone-count">x {count}</span>
              <span className="stone-value">= {total} G</span>
            </div>
          );
        })}
      </div>

      <div className="total-value">
        <strong>Total Value: {totalValue} G</strong>
      </div>

      <div className="exchange-input">
        <label>Exchange Amount:</label>
        <input
          type="number"
          min="0"
          max={totalValue}
          value={exchangeValue}
          onChange={(e) => setExchangeValue(Number(e.target.value))}
        />
        <span>G (Max: {totalValue}G)</span>
      </div>

      <div className="exchange-result">Gold Acquired: {exchangeValue} G</div>

      <button
        className="exchange-button"
        onClick={handleExchange}
        disabled={exchangeValue <= 0 || exchangeValue > totalValue}
      >
        Exchange
      </button>
    </div>
  );
};

export default ExchangeTab;
```

**✅ Completion Check:**

- [ ] ExchangeTab.tsx created.
- [ ] Magic Stone list displayed.
- [ ] Exchange process works.

---

## Phase 3: Sale System Integration (Week 2: Day 1-3)

### Task 3.1: BattleScreen Integration

```typescript
// src/battles/battleUI/BattleScreen.tsx (Modification)

const BattleScreen: React.FC<BattleScreenProps> = (
  {
    // ... props
  }
) => {
  const { incrementEncounterCount } = useGameState(); // ✨ Added

  // Process on Battle End
  useEffect(() => {
    if (battleResult === "victory") {
      // ✨ Increment Battle Count
      incrementEncounterCount();
    }
  }, [battleResult, incrementEncounterCount]);

  // ... rest of code
};
```

**✅ Completion Check:**

- [ ] Encounter count increments in BattleScreen.
- [ ] saleTiming becomes true when encounterCount >= 3.

---

### Task 3.2: BaseCamp Integration

```typescript
// src/camps/campsUI/BaseCamp.tsx (Modification)

import { useEffect } from "react";
import { generateDailySale } from "../facilities/Shop/utils/saleCalculator";

const BaseCamp = () => {
  const { gameState, updateSale, enterDungeon } = useGameState();

  // Check Sale Update on Mount
  useEffect(() => {
    if (gameState.saleTiming) {
      const newSale = generateDailySale();
      updateSale(newSale);
    }
  }, [gameState.saleTiming, updateSale]);

  const facilities: FacilityCardProps[] = [
    {
      type: "dungeon",
      name: "Abyss Entrance",
      description: "Explore Dungeon",
      icon: "🌀",
      isUnlocked: true,
      onEnter: () => {
        enterDungeon(); // ✨ Reset Sale Flag
      },
    },
    // ... Other Facilities
  ];

  // ... Rest of code
};
```

**✅ Completion Check:**

- [ ] Sale updates upon returning to Camp.
- [ ] saleTiming becomes false upon entering dungeon.

---

## Phase 4: Equipment Generation System (Week 2: Day 4-5)

### Task 4.1: Create equipmentGenerator.ts

```bash
mkdir -p src/items/utils

```

```typescript
// src/items/utils/equipmentGenerator.ts (New File)

import type { Item, EquipmentSlot } from "../../types/ItemTypes";
import type { EquipmentPackConfig } from "../../types/ShopTypes";

/**
 * Generate Unique ID
 */
function generateUniqueId(): string {
  return `${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
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

  return "common";
}

/**
 * Generate Random Equipment (Phase 1: Simple)
 */
export function createRandomEquipment(
  slot: EquipmentSlot,
  rarity: "common" | "rare" | "epic" | "legendary"
): Item {
  // Phase 1: Return Fixed Equipment (Temporary)
  // Phase 2: Lottery from EQUIPMENT_AND_ITEMS_DESIGN.md

  const baseNames: Record<EquipmentSlot, string> = {
    weapon: "Sword",
    armor: "Armor",
    helmet: "Helmet",
    boots: "Boots",
    accessory1: "Ring",
    accessory2: "Amulet",
  };

  const rarityNames: Record<string, string> = {
    common: "Common",
    rare: "Rare",
    epic: "Epic",
    legendary: "Legendary",
  };

  return {
    id: generateUniqueId(),
    typeId: `${slot}_${rarity}_template`,
    name: `${rarityNames[rarity]} ${baseNames[slot]}`,
    description: "Temporary equipment",
    itemType: "equipment",
    icon: "⚔️",
    equipmentSlot: slot,
    durability: 100,
    maxDurability: 100,
    effects: [],
    rarity: rarity,
    sellPrice: { common: 50, rare: 150, epic: 400, legendary: 1000 }[rarity],
    canSell: true,
    canDiscard: false,
  };
}

/**
 * Open Equipment Pack
 */
export function openEquipmentPack(config: EquipmentPackConfig): Item[] {
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
    const rarity = rollRarity(config.probabilities);
    const equipment = createRandomEquipment(slot, rarity);
    items.push(equipment);
  }

  return items;
}
```

**✅ Completion Check:**

- [ ] equipmentGenerator.ts created.
- [ ] rollRarity implemented.
- [ ] createRandomEquipment implemented (Phase 1: Simple).
- [ ] openEquipmentPack implemented.

---

### Task 4.2: Integrate Pack Opening into BuyTab

```typescript
// src/camps/facilities/Shop/BuyTab.tsx (Modification)

import { openEquipmentPack } from "../../../items/utils/equipmentGenerator";

const BuyTab: React.FC = () => {
  // ... Existing code

  const handleBuy = (shopItem: ShopItem) => {
    // ... Gold Payment Logic

    if (shopItem.type === "equipment_pack" && shopItem.packConfig) {
      // ✨ Open Equipment Pack
      const newEquipments = openEquipmentPack(shopItem.packConfig);

      newEquipments.forEach((eq) => {
        addItem(eq);
      });

      // TODO: Opening Visuals
      alert(`Pack Opened! Acquired ${newEquipments.length} items!`);
    } else {
      // Consumables/Teleport
      // TODO: createItemFromId(shopItem.targetItemId)
    }
  };

  // ... Rest of code
};
```

**✅ Completion Check:**

- [ ] 6 items generated when purchasing equipment pack.
- [ ] Correctly added to inventory.

---

## Phase 5: CSS and Animations (Week 3)

### Task 5.1: Shop.css

```css
/* src/camps/facilities/Shop/Shop.css */

.shop-screen {
  width: 100vw;
  height: 100vh;
  background: linear-gradient(135deg, #1a1a2e 0%, #2a2a3e 100%);
  display: flex;
  flex-direction: column;
  padding: 2rem;
  color: #e0d0f0;
}

.shop-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.shop-header h1 {
  font-size: 2.5rem;
  text-shadow: 0 0 20px rgba(218, 165, 32, 0.8);
}

.resources {
  display: flex;
  gap: 2rem;
  font-size: 1.5rem;
}

.shop-tabs {
  display: flex;
  gap: 1rem;
  justify-content: center;
  margin-bottom: 2rem;
}

.shop-tabs button {
  padding: 1rem 2rem;
  background: rgba(218, 165, 32, 0.2);
  border: 2px solid rgba(218, 165, 32, 0.5);
  border-radius: 8px;
  color: #e0d0f0;
  font-size: 1.2rem;
  cursor: pointer;
  transition: all 0.3s ease;
}

.shop-tabs button.active {
  background: rgba(218, 165, 32, 0.8);
  border-color: rgba(218, 165, 32, 1);
}

.shop-content {
  flex: 1;
  background: rgba(0, 0, 0, 0.3);
  border: 2px solid rgba(218, 165, 32, 0.3);
  border-radius: 12px;
  padding: 2rem;
  overflow-y: auto;
}

.back-button {
  margin-top: 1rem;
  padding: 1rem 2rem;
  background: rgba(100, 100, 100, 0.3);
  border: 2px solid rgba(150, 150, 150, 0.5);
  border-radius: 8px;
  color: #e0d0f0;
  font-size: 1.1rem;
  cursor: pointer;
}
```

### Task 5.2: BuyTab.css

```css
/* src/camps/facilities/Shop/BuyTab.css */

.buy-tab {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.category-tabs {
  display: flex;
  gap: 1rem;
  margin-bottom: 2rem;
}

.category-tabs button {
  padding: 0.75rem 1.5rem;
  background: rgba(100, 100, 100, 0.3);
  border: 2px solid rgba(150, 150, 150, 0.5);
  border-radius: 6px;
  color: #e0d0f0;
  cursor: pointer;
}

.category-tabs button.active {
  background: rgba(218, 165, 32, 0.5);
  border-color: rgba(218, 165, 32, 0.8);
}

.items-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 1.5rem;
}

.shop-item-card {
  background: rgba(50, 50, 70, 0.6);
  border: 2px solid rgba(100, 100, 120, 0.5);
  border-radius: 8px;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  align-items: center;
  transition: all 0.3s ease;
}

.shop-item-card:hover {
  transform: translateY(-4px);
  border-color: rgba(218, 165, 32, 0.8);
  box-shadow: 0 8px 16px rgba(218, 165, 32, 0.3);
}

.item-icon {
  font-size: 3rem;
  margin-bottom: 0.5rem;
}

.item-name {
  font-size: 1.1rem;
  font-weight: bold;
  margin-bottom: 0.5rem;
}

.item-description {
  font-size: 0.9rem;
  color: #b0b0c0;
  margin-bottom: 1rem;
  text-align: center;
}

.item-price {
  display: flex;
  flex-direction: column;
  align-items: center;
  margin-bottom: 1rem;
}

.sale-badge {
  background: #ef4444;
  color: white;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-size: 0.8rem;
  font-weight: bold;
  margin-bottom: 0.25rem;
}

.original-price {
  text-decoration: line-through;
  color: #888;
  font-size: 0.9rem;
}

.discounted-price {
  font-size: 1.3rem;
  font-weight: bold;
  color: #4ade80;
}

.normal-price {
  font-size: 1.3rem;
  font-weight: bold;
  color: #daa520;
}

.buy-button {
  width: 100%;
  padding: 0.75rem;
  background: linear-gradient(135deg, #daa520 0%, #b8860b 100%);
  border: 2px solid #daa520;
  border-radius: 6px;
  color: white;
  font-weight: bold;
  cursor: pointer;
  transition: all 0.3s ease;
}

.buy-button:hover:not(:disabled) {
  transform: scale(1.05);
  box-shadow: 0 4px 12px rgba(218, 165, 32, 0.6);
}

.buy-button:disabled {
  background: rgba(100, 100, 100, 0.3);
  border-color: rgba(150, 150, 150, 0.5);
  color: #888;
  cursor: not-allowed;
}
```

**✅ Completion Check:**

- [ ] Shop.css created.
- [ ] BuyTab.css created.
- [ ] Styles applied.

---

## Test Procedure

### Basic Operation Test

```
□ Shop Screen Display
  □ Resource display (Gold, Magic Stone value)
  □ Tab switching

□ Buy Function
  □ Product Grid display
  □ Category switching
  □ Buy process
  □ Gold deduction
  □ Item addition

□ Sell Function
  □ Inventory display
  □ Equipped filtering
  □ Sell process
  □ Gold addition

□ Magic Stone Exchange
  □ Magic Stone list display
  □ Exchange process
  □ Correct rate calculation

□ Sale System
  □ encounterCount increase
  □ saleTiming update
  □ Sale price display
  □ Epic exclusion

```

---

## Troubleshooting

### Common Errors

**1. Items not appearing in Shop**

```
Cause: ShopData.ts import error
Solution: Check path

```

**2. Sale not updating**

```
Cause: useEffect not running in BaseCamp
Solution: Check dependency array

```

**3. Equipment pack not opening**

```
Cause: equipmentGenerator.ts import error
Solution: Check import path

```
