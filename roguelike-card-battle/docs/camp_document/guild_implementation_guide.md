Here is the English translation of the Implementation Guide.

# BaseCamp & Guild Implementation Procedures (IMPLEMENTATION_GUIDE_V1)

## 0. Preparation

### 0.1 Required Understanding

- Basics of React Context API
- TypeScript Type Definitions
- Structure of the existing `BattleScreen`
- Structure of the existing `PlayerData.tsx`

### 0.2 Environment Check

```bash
# Check at project root
npm run dev  # Ensure the development server starts

```

---

## Phase 1: Foundation (Week 1: Day 1-3)

### Task 1.1: Creating Type Definitions

**Priority:** 🔴 Highest (Foundation for other implementations)

**1.1.1 Create ItemTypes.ts**

```bash
# Create directory
mkdir -p src/types

```

```typescript
// src/types/ItemTypes.ts

export type ItemType =
  | "equipment"
  | "consumable"
  | "magicStone"
  | "material"
  | "quest"
  | "key";

export type EquipmentSlot =
  | "weapon"
  | "armor"
  | "helmet"
  | "boots"
  | "accessory1"
  | "accessory2";

export interface Item {
  id: string;
  typeId: string;
  name: string;
  description: string;
  itemType: ItemType;
  icon: string;

  // Specific to Equipment
  equipmentSlot?: EquipmentSlot;
  durability?: number;
  maxDurability?: number;
  effects?: EquipmentEffect[];

  // Specific to Consumables
  stackable?: boolean;
  stackCount?: number;
  maxStack?: number;

  // Specific to Magic Stones
  magicStoneValue?: number;

  // Common
  rarity: "common" | "uncommon" | "rare" | "epic" | "legendary";
  sellPrice: number;
  canSell: boolean;
  canDiscard: boolean;
}

export interface EquipmentEffect {
  type: "stat" | "skill" | "passive";
  target: string;
  value: number | string;
  description: string;
}
```

**1.1.2 Create GuildTypes.ts**

```typescript
// src/types/GuildTypes.ts

export interface PromotionExam {
  currentGrade: string;
  nextGrade: string;
  requiredCardCount: number;
  requiredGold?: number;
  enemyId: string;
  description: string;
  recommendations: {
    hp: number;
    ap: number;
  };
  rewards: {
    statBonus: string;
    items?: string[];
  };
}

export interface Rumor {
  id: string;
  name: string;
  description: string;
  cost: number;
  effect: RumorEffect;
  rarity: "common" | "rare" | "epic";
  icon: string;
}

export type RumorEffect =
  | { type: "elite_rate"; value: number }
  | { type: "shop_discount"; value: number }
  | { type: "treasure_rate"; value: number }
  | { type: "start_bonus"; bonus: string };

export interface Quest {
  id: string;
  title: string;
  description: string;
  type: "daily" | "weekly";
  requiredGrade: string;
  objectives: QuestObjective[];
  rewards: QuestReward;
  isActive: boolean;
  isCompleted: boolean;
  expiresAt?: Date;
}

export interface QuestObjective {
  type: "defeat" | "collect" | "explore";
  target: string;
  required: number;
  current: number;
  description: string;
}

export interface QuestReward {
  gold?: number;
  magicStones?: number;
  items?: string[];
  experience?: number;
}

export interface GuildState {
  activeRumors: string[];
  acceptedQuests: string[];
  completedQuests: string[];
  availableExam: PromotionExam | null;
}
```

**✅ Completion Check:**

- [ ] `ItemTypes.ts` created, no compilation errors.
- [ ] `GuildTypes.ts` created, no compilation errors.

---

### Task 1.2: Context API Implementation

**Priority:** 🔴 Highest

**1.2.1 Directory Structure**

```bash
mkdir -p src/contexts

```

**1.2.2 GameStateContext.tsx**

```typescript
// src/contexts/GameStateContext.tsx

import React, { createContext, useContext, useState, ReactNode } from "react";

export type GameScreen =
  | "camp"
  | "battle"
  | "shop"
  | "blacksmith"
  | "guild"
  | "dungeon";
export type BattleMode = "normal" | "exam" | "return_route" | null;
export type Depth = 1 | 2 | 3 | 4 | 5;

export interface BattleConfig {
  enemyIds: string[];
  backgroundType: "dungeon" | "arena" | "guild";
  onWin?: () => void;
  onLose?: () => void;
}

export interface GameState {
  currentScreen: GameScreen;
  battleMode: BattleMode;
  depth: Depth;
  encounterCount: number;
  battleConfig?: BattleConfig;
}

interface GameStateContextValue {
  gameState: GameState;
  setGameState: React.Dispatch<React.SetStateAction<GameState>>;
  navigateTo: (screen: GameScreen) => void;
  startBattle: (config: BattleConfig, mode?: BattleMode) => void;
  returnToCamp: () => void;
}

const GameStateContext = createContext<GameStateContextValue | undefined>(
  undefined
);

export const GameStateProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const [gameState, setGameState] = useState<GameState>({
    currentScreen: "camp",
    battleMode: null,
    depth: 1,
    encounterCount: 0,
  });

  const navigateTo = (screen: GameScreen) => {
    setGameState((prev) => ({ ...prev, currentScreen: screen }));
  };

  const startBattle = (config: BattleConfig, mode: BattleMode = "normal") => {
    setGameState((prev) => ({
      ...prev,
      currentScreen: "battle",
      battleMode: mode,
      battleConfig: config,
    }));
  };

  const returnToCamp = () => {
    setGameState((prev) => ({
      ...prev,
      currentScreen: "camp",
      battleMode: null,
      battleConfig: undefined,
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
      }}
    >
      {children}
    </GameStateContext.Provider>
  );
};

export const useGameState = () => {
  const context = useContext(GameStateContext);
  if (!context) {
    throw new Error("useGameState must be used within GameStateProvider");
  }
  return context;
};
```

**1.2.3 PlayerContext.tsx**

```typescript
// src/contexts/PlayerContext.tsx

import React, { createContext, useContext, useState, ReactNode } from "react";
import { Player, Swordman_Status } from "../Character/data/PlayerData";

interface PlayerContextValue {
  player: Player;
  updatePlayer: (updates: Partial<Player>) => void;
  updateClassGrade: (newGrade: string) => void;
  addGold: (amount: number) => void;
  useGold: (amount: number) => boolean;
  updateHp: (newHp: number) => void;
  updateAp: (newAp: number) => void;
}

const PlayerContext = createContext<PlayerContextValue | undefined>(undefined);

export const PlayerProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  // Initial Player Data (To be loaded from save data later)
  const [player, setPlayer] = useState<Player>(Swordman_Status);

  const updatePlayer = (updates: Partial<Player>) => {
    setPlayer((prev) => ({ ...prev, ...updates }));
  };

  const updateClassGrade = (newGrade: string) => {
    setPlayer((prev) => ({ ...prev, classGrade: newGrade }));
  };

  const addGold = (amount: number) => {
    setPlayer((prev) => ({ ...prev, gold: prev.gold + amount }));
  };

  const useGold = (amount: number): boolean => {
    if (player.gold < amount) return false;
    setPlayer((prev) => ({ ...prev, gold: prev.gold - amount }));
    return true;
  };

  const updateHp = (newHp: number) => {
    setPlayer((prev) => ({
      ...prev,
      hp: Math.max(0, Math.min(newHp, prev.maxHp)),
    }));
  };

  const updateAp = (newAp: number) => {
    setPlayer((prev) => ({
      ...prev,
      ap: Math.max(0, Math.min(newAp, prev.maxAp)),
    }));
  };

  return (
    <PlayerContext.Provider
      value={{
        player,
        updatePlayer,
        updateClassGrade,
        addGold,
        useGold,
        updateHp,
        updateAp,
      }}
    >
      {children}
    </PlayerContext.Provider>
  );
};

export const usePlayer = () => {
  const context = useContext(PlayerContext);
  if (!context) {
    throw new Error("usePlayer must be used within PlayerProvider");
  }
  return context;
};
```

**1.2.4 InventoryContext.tsx**

```typescript
// src/contexts/InventoryContext.tsx

import React, { createContext, useContext, useState, ReactNode } from "react";
import type { Item, EquipmentSlot } from "../types/ItemTypes";

interface EquippedItems {
  weapon?: string;
  armor?: string;
  helmet?: string;
  boots?: string;
  accessory1?: string;
  accessory2?: string;
}

interface InventoryContextValue {
  items: Item[];
  equipped: EquippedItems;
  addItem: (item: Item) => void;
  removeItem: (itemId: string) => void;
  equipItem: (itemId: string, slot: EquipmentSlot) => void;
  unequipItem: (slot: EquipmentSlot) => void;
  getMagicStones: () => number;
  useMagicStones: (amount: number) => boolean;
  getEquippedIds: () => string[];
}

const InventoryContext = createContext<InventoryContextValue | undefined>(
  undefined
);

export const InventoryProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const [items, setItems] = useState<Item[]>([]);
  const [equipped, setEquipped] = useState<EquippedItems>({});

  const addItem = (item: Item) => {
    setItems((prev) => {
      // If item is stackable, add to existing item
      if (item.stackable) {
        const existingIndex = prev.findIndex((i) => i.typeId === item.typeId);
        if (existingIndex !== -1) {
          const newItems = [...prev];
          const existing = newItems[existingIndex];
          newItems[existingIndex] = {
            ...existing,
            stackCount: (existing.stackCount || 1) + (item.stackCount || 1),
          };
          return newItems;
        }
      }
      return [...prev, item];
    });
  };

  const removeItem = (itemId: string) => {
    setItems((prev) => prev.filter((item) => item.id !== itemId));
  };

  const equipItem = (itemId: string, slot: EquipmentSlot) => {
    const item = items.find((i) => i.id === itemId);
    if (!item || item.itemType !== "equipment") return;

    setEquipped((prev) => ({ ...prev, [slot]: itemId }));
  };

  const unequipItem = (slot: EquipmentSlot) => {
    setEquipped((prev) => {
      const newEquipped = { ...prev };
      delete newEquipped[slot];
      return newEquipped;
    });
  };

  const getMagicStones = (): number => {
    return items
      .filter((item) => item.itemType === "magicStone")
      .reduce((sum, item) => {
        const value = item.magicStoneValue || 0;
        const count = item.stackCount || 1;
        return sum + value * count;
      }, 0);
  };

  const useMagicStones = (amount: number): boolean => {
    const total = getMagicStones();
    if (total < amount) return false;

    // Consume Magic Stones (Start from lowest value)
    let remaining = amount;
    const magicStones = items
      .filter((item) => item.itemType === "magicStone")
      .sort((a, b) => (a.magicStoneValue || 0) - (b.magicStoneValue || 0));

    const newItems = [...items];
    for (const stone of magicStones) {
      if (remaining <= 0) break;

      const stoneValue = stone.magicStoneValue || 0;
      const stoneCount = stone.stackCount || 1;
      const totalValue = stoneValue * stoneCount;

      if (totalValue <= remaining) {
        // Consume all of this stone stack
        remaining -= totalValue;
        const index = newItems.findIndex((i) => i.id === stone.id);
        if (index !== -1) newItems.splice(index, 1);
      } else {
        // Consume partial stack
        const needCount = Math.ceil(remaining / stoneValue);
        remaining = 0;
        const index = newItems.findIndex((i) => i.id === stone.id);
        if (index !== -1) {
          newItems[index] = {
            ...newItems[index],
            stackCount: stoneCount - needCount,
          };
        }
      }
    }

    setItems(newItems);
    return true;
  };

  const getEquippedIds = (): string[] => {
    return Object.values(equipped).filter(
      (id): id is string => id !== undefined
    );
  };

  return (
    <InventoryContext.Provider
      value={{
        items,
        equipped,
        addItem,
        removeItem,
        equipItem,
        unequipItem,
        getMagicStones,
        useMagicStones,
        getEquippedIds,
      }}
    >
      {children}
    </InventoryContext.Provider>
  );
};

export const useInventory = () => {
  const context = useContext(InventoryContext);
  if (!context) {
    throw new Error("useInventory must be used within InventoryProvider");
  }
  return context;
};
```

**1.2.5 Update App.tsx**

```typescript
// src/App.tsx

import { GameStateProvider } from "./contexts/GameStateContext";
import { PlayerProvider } from "./contexts/PlayerContext";
import { InventoryProvider } from "./contexts/InventoryContext";
import BattleScreen from "./battles/battleUI/BattleScreen";
import BaseCamp from "./camps/campsUI/BaseCamp";
import { useGameState } from "./contexts/GameStateContext";

function AppContent() {
  const { gameState, setGameState } = useGameState();
  const { currentScreen, depth, battleMode, battleConfig } = gameState;

  return (
    <div style={{ width: "100%", height: "100%" }}>
      {currentScreen === "camp" && <BaseCamp />}
      {currentScreen === "battle" && (
        <BattleScreen
          depth={depth}
          onDepthChange={(newDepth) =>
            setGameState((prev) => ({ ...prev, depth: newDepth }))
          }
          battleMode={battleMode || "normal"}
          enemyIds={battleConfig?.enemyIds}
          onBattleEnd={(result) => {
            if (result === "victory" && battleConfig?.onWin) {
              battleConfig.onWin();
            } else if (result === "defeat" && battleConfig?.onLose) {
              battleConfig.onLose();
            }
          }}
        />
      )}
    </div>
  );
}

function App() {
  return (
    <GameStateProvider>
      <PlayerProvider>
        <InventoryProvider>
          <AppContent />
        </InventoryProvider>
      </PlayerProvider>
    </GameStateProvider>
  );
}

export default App;
```

**✅ Completion Check:**

- [ ] 3 Contexts created.
- [ ] `App.tsx` is wrapped in Providers.
- [ ] No compilation errors.
- [ ] Development server starts successfully.

---

### Task 1.3: Fix Player Type

**Priority:** 🟡 Medium

```typescript
// src/Character/data/PlayerData.tsx (Fix)

export interface Player {
  characterClass: CharacterClass;
  classGrade: string; // ✅ Maintain existing string type
  level: number;
  hp: number;
  maxHp: number;
  ap: number;
  maxAp: number;
  guard: number;
  speed: number;
  initialEnergy: number;
  gold: number;

  deck: string[];
  equipped: string[]; // ✨ Newly added

  statusEffects: Record<string, number>;
  title?: string[];
}

// ✅ Fix existing initial data
export const Swordman_Status: Player = {
  characterClass: "swordsman",
  classGrade: getSwordsmanTitle(0),
  level: 1,
  hp: 100,
  maxHp: 110,
  ap: 30,
  maxAp: 30,
  guard: 0,
  speed: 50,
  initialEnergy: 3,
  gold: 0,
  deck: [],
  equipped: [], // ✨ Newly added
  statusEffects: {},
};

// Fix Mage_Status and Summon_Status similarly
```

**✅ Completion Check:**

- [ ] `equipped: string[]` added to Player type.
- [ ] Initial data updated.
- [ ] Confirmed no type errors in existing code.

---

## Phase 2: Create Test Data (Week 1: Day 4-5)

### Task 2.1: Create GuildEnemyData.ts

**Priority:** 🔴 Highest

```bash
# Check directory
ls src/domain/characters/enemy/data/

```

```typescript
// src/domain/characters/enemy/data/GuildEnemyData.ts

import type { Enemy } from "../../../../Character/data/EnemyData";

/**
 * Enemy data specific to Promotion Exams
 * Will not appear in normal dungeons
 */

// Swordsman Exam Enemies
export const TRAINING_DUMMY: Enemy = {
  id: "exam_training_dummy",
  name: "Training Dummy",
  displayName: "Training Dummy",
  maxHp: 50,
  maxAp: 30,
  speed: 40,
  initialEnergy: 2,
  depth: 1,
  type: "normal",
  pattern: [
    {
      turn: 1,
      actions: [
        {
          probability: 1.0,
          action: {
            name: "Wooden Sword Attack",
            type: "attack",
            baseDamage: 8,
            displayIcon: "⚔️",
            priority: 0,
            energyCost: 1,
          },
        },
      ],
    },
  ],
  rewards: {
    gold: { min: 0, max: 0 },
    magicStones: [],
  },
};

export const GUILD_INSTRUCTOR: Enemy = {
  id: "exam_guild_instructor",
  name: "Guild Instructor",
  displayName: "Guild Instructor",
  maxHp: 120,
  maxAp: 60,
  speed: 55,
  initialEnergy: 3,
  depth: 2,
  type: "elite",
  pattern: [
    {
      turn: 1,
      actions: [
        {
          probability: 0.7,
          action: {
            name: "Instructor Strike",
            type: "attack",
            baseDamage: 15,
            displayIcon: "⚔️",
            priority: 0,
            energyCost: 2,
          },
        },
        {
          probability: 0.3,
          action: {
            name: "Defensive Stance",
            type: "buff",
            baseDamage: 0,
            guardGain: 20,
            displayIcon: "🛡️",
            priority: 1,
            energyCost: 1,
          },
        },
      ],
    },
  ],
  rewards: {
    gold: { min: 0, max: 0 },
    magicStones: [],
  },
};

export const VETERAN_WARRIOR: Enemy = {
  id: "exam_veteran_warrior",
  name: "Veteran Warrior",
  displayName: "Veteran Warrior",
  maxHp: 200,
  maxAp: 90,
  speed: 60,
  initialEnergy: 4,
  depth: 3,
  type: "elite",
  pattern: [
    {
      turn: 1,
      actions: [
        {
          probability: 0.5,
          action: {
            name: "Skilled Swordsmanship",
            type: "attack",
            baseDamage: 20,
            displayIcon: "⚔️",
            priority: 0,
            energyCost: 2,
          },
        },
        {
          probability: 0.3,
          action: {
            name: "Combo Strike",
            type: "attack",
            baseDamage: 12,
            displayIcon: "⚔️⚔️",
            priority: 0,
            energyCost: 3,
            applyDebuffs: [{ type: "bleed", stacks: 1, duration: 2 }],
          },
        },
        {
          probability: 0.2,
          action: {
            name: "Iron Defense",
            type: "buff",
            baseDamage: 0,
            guardGain: 30,
            displayIcon: "🛡️",
            priority: 1,
            energyCost: 2,
          },
        },
      ],
    },
  ],
  rewards: {
    gold: { min: 0, max: 0 },
    magicStones: [],
  },
};

export const SWORD_SAINT_PHANTOM: Enemy = {
  id: "exam_sword_saint_phantom",
  name: "Phantom of the Sword Saint",
  displayName: "Phantom of the Sword Saint",
  maxHp: 350,
  maxAp: 120,
  speed: 70,
  initialEnergy: 5,
  depth: 4,
  type: "boss",
  pattern: [
    // Phase 1 (HP 100%)
    {
      turn: 1,
      actions: [
        {
          probability: 0.6,
          action: {
            name: "Godspeed Slash",
            type: "attack",
            baseDamage: 25,
            displayIcon: "⚡⚔️",
            priority: 0,
            energyCost: 3,
          },
        },
        {
          probability: 0.4,
          action: {
            name: "Sword Spirit Release",
            type: "attack",
            baseDamage: 18,
            displayIcon: "🌊",
            priority: 1,
            energyCost: 2,
            applyDebuffs: [{ type: "weakened", stacks: 1, duration: 2 }],
          },
        },
      ],
    },
    // Phase 2 (HP < 50%)
    {
      turn: 5,
      actions: [
        {
          probability: 1.0,
          action: {
            name: "Ultimate Art: Void Blade",
            type: "attack",
            baseDamage: 40,
            displayIcon: "💥",
            priority: 0,
            energyCost: 5,
            applyDebuffs: [{ type: "stunned", stacks: 1, duration: 1 }],
          },
        },
      ],
    },
  ],
  rewards: {
    gold: { min: 0, max: 0 },
    magicStones: [],
  },
};

// Mage Exam Enemy (Simplified)
export const MAGIC_GOLEM: Enemy = {
  id: "exam_magic_golem",
  name: "Magic Golem",
  displayName: "Magic Golem",
  maxHp: 45,
  maxAp: 25,
  speed: 35,
  initialEnergy: 2,
  depth: 1,
  type: "normal",
  pattern: [
    {
      turn: 1,
      actions: [
        {
          probability: 1.0,
          action: {
            name: "Magic Bullet",
            type: "attack",
            baseDamage: 10,
            displayIcon: "✨",
            priority: 0,
            energyCost: 1,
          },
        },
      ],
    },
  ],
  rewards: {
    gold: { min: 0, max: 0 },
    magicStones: [],
  },
};

// Export
export const GUILD_ENEMIES: Enemy[] = [
  TRAINING_DUMMY,
  GUILD_INSTRUCTOR,
  VETERAN_WARRIOR,
  SWORD_SAINT_PHANTOM,
  MAGIC_GOLEM,
  // ... Add other class enemies
];
```

**✅ Completion Check:**

- [ ] `GuildEnemyData.ts` created.
- [ ] At least 4 enemy data defined.
- [ ] Compatible with existing `Enemy` type.

---

### Task 2.2: Create PromotionData.ts

```bash
mkdir -p src/camps/facilities/Guild/data

```

```typescript
// src/camps/facilities/Guild/data/PromotionData.ts

import type { PromotionExam } from "../../../../types/GuildTypes";

/**
 * Swordsman Promotion Exam Data
 */
export const SWORDSMAN_EXAMS: PromotionExam[] = [
  {
    currentGrade: "Apprentice Swordsman",
    nextGrade: "Swordsman",
    requiredCardCount: 5,
    enemyId: "exam_training_dummy",
    description: "Defeat the Training Dummy to prove your basic swordsmanship.",
    recommendations: {
      hp: 60,
      ap: 40,
    },
    rewards: {
      statBonus: "maxHP+10, Quest Slot+1",
      items: ["weapon_iron_sword"],
    },
  },
  {
    currentGrade: "Swordsman",
    nextGrade: "Sword Master",
    requiredCardCount: 15,
    enemyId: "exam_guild_instructor",
    description:
      "Demonstrate your ability in a mock battle with the Guild Instructor.",
    recommendations: {
      hp: 80,
      ap: 60,
    },
    rewards: {
      statBonus: "ATK+5%, Reward Bonus",
      items: ["weapon_steel_sword", "armor_steel_plate"],
    },
  },
  {
    currentGrade: "Sword Master",
    nextGrade: "Sword Saint",
    requiredCardCount: 30,
    requiredGold: 500,
    enemyId: "exam_veteran_warrior",
    description: "Defeat the Veteran Warrior to master the path of the sword.",
    recommendations: {
      hp: 120,
      ap: 80,
    },
    rewards: {
      statBonus: "All Stats+5%",
      items: ["weapon_mythril_sword", "armor_mythril_plate"],
    },
  },
  {
    currentGrade: "Sword Saint",
    nextGrade: "Sword God",
    requiredCardCount: 50,
    requiredGold: 1000,
    enemyId: "exam_sword_saint_phantom",
    description:
      "Win the death match against the Phantom of the Sword Saint to reach the realm of gods.",
    recommendations: {
      hp: 150,
      ap: 100,
    },
    rewards: {
      statBonus: "Unlock Unique Legend Skill",
      items: ["weapon_legendary_excalibur"],
    },
  },
];

/**
 * Mage Promotion Exam Data
 */
export const MAGE_EXAMS: PromotionExam[] = [
  {
    currentGrade: "Apprentice Mage",
    nextGrade: "Mage",
    requiredCardCount: 5,
    enemyId: "exam_magic_golem",
    description: "Control the Magic Golem to prove your handling of mana.",
    recommendations: {
      hp: 50,
      ap: 35,
    },
    rewards: {
      statBonus: "maxHP+8, maxAP+5",
      items: ["weapon_apprentice_staff"],
    },
  },
  // ... Other ranks
];

/**
 * Summoner Promotion Exam Data
 */
export const SUMMONER_EXAMS: PromotionExam[] = [
  // ... Definitions
];

/**
 * Get exam data based on class
 */
export function getExamsForClass(characterClass: string): PromotionExam[] {
  switch (characterClass) {
    case "swordsman":
      return SWORDSMAN_EXAMS;
    case "mage":
      return MAGE_EXAMS;
    case "summoner":
      return SUMMONER_EXAMS;
    default:
      return [];
  }
}

/**
 * Get next exam from current grade
 */
export function getNextExam(
  characterClass: string,
  currentGrade: string
): PromotionExam | null {
  const exams = getExamsForClass(characterClass);
  return exams.find((exam) => exam.currentGrade === currentGrade) || null;
}
```

**✅ Completion Check:**

- [ ] `PromotionData.ts` created.
- [ ] 12 exam data defined (3 classes x 4 ranks).
- [ ] Helper functions implemented.

---

## Phase 3: Guild UI Implementation (Week 2: Day 1-5)

### Task 3.1: Guild Component Skeleton

```bash
mkdir -p src/camps/facilities/Guild

```

```typescript
// src/camps/facilities/Guild/Guild.tsx

import { useState } from "react";
import { usePlayer } from "../../../contexts/PlayerContext";
import { useGameState } from "../../../contexts/GameStateContext";
import PromotionTab from "./PromotionTab";
import RumorsTab from "./RumorsTab";
import QuestsTab from "./QuestsTab";
import "./Guild.css";

type GuildTab = "rumors" | "quests" | "promotion";

const Guild: React.FC = () => {
  const [selectedTab, setSelectedTab] = useState<GuildTab>("promotion");
  const { returnToCamp } = useGameState();

  return (
    <div className="guild-screen">
      <header className="guild-header">
        <h1>🍺 Guild - Tavern</h1>
      </header>

      <nav className="guild-tabs">
        <button
          className={selectedTab === "rumors" ? "active" : ""}
          onClick={() => setSelectedTab("rumors")}
        >
          Rumors
        </button>
        <button
          className={selectedTab === "quests" ? "active" : ""}
          onClick={() => setSelectedTab("quests")}
        >
          Quests
        </button>
        <button
          className={selectedTab === "promotion" ? "active" : ""}
          onClick={() => setSelectedTab("promotion")}
        >
          Promotion Exams
        </button>
      </nav>

      <div className="guild-content">
        {selectedTab === "rumors" && <RumorsTab />}
        {selectedTab === "quests" && <QuestsTab />}
        {selectedTab === "promotion" && <PromotionTab />}
      </div>

      <button className="back-button" onClick={returnToCamp}>
        Back to Camp
      </button>
    </div>
  );
};

export default Guild;
```

**✅ Completion Check:**

- [ ] `Guild.tsx` created.
- [ ] Tab switching works.
- [ ] Back button works.

---

### Task 3.2: PromotionTab Implementation

```typescript
// src/camps/facilities/Guild/PromotionTab.tsx

import { usePlayer } from "../../../contexts/PlayerContext";
import { useGameState } from "../../../contexts/GameStateContext";
import { useInventory } from "../../../contexts/InventoryContext";
import { getNextExam } from "./data/PromotionData";
import "./PromotionTab.css";

const PromotionTab: React.FC = () => {
  const { player, updateClassGrade, updatePlayer } = usePlayer();
  const { startBattle, returnToCamp } = useGameState();
  const { addItem } = useInventory();

  const exam = getNextExam(player.characterClass, player.classGrade);

  if (!exam) {
    return (
      <div className="promotion-unavailable">
        <h2>Congratulations!</h2>
        <p>You have already obtained the highest rank.</p>
        <p className="current-grade">{player.classGrade}</p>
      </div>
    );
  }

  // Check Requirements
  const cardCount = player.deck.length;
  const meetsCardRequirement = cardCount >= exam.requiredCardCount;
  const meetsGoldRequirement = exam.requiredGold
    ? player.gold >= exam.requiredGold
    : true;

  const canTakeExam = meetsCardRequirement && meetsGoldRequirement;

  const handleStartExam = () => {
    if (!canTakeExam) return;

    // Start Exam Battle
    startBattle(
      {
        enemyIds: [exam.enemyId],
        backgroundType: "arena",
        onWin: () => handleExamPassed(),
        onLose: () => handleExamFailed(),
      },
      "exam"
    );
  };

  const handleExamPassed = () => {
    // Promotion Process
    updateClassGrade(exam.nextGrade);

    // Apply Stat Bonus (Simplified)
    // TODO: Parse and apply statBonus

    // Reward Items
    if (exam.rewards.items) {
      exam.rewards.items.forEach((itemId) => {
        // TODO: Create Item object from itemId
        // addItem(createItemFromId(itemId));
      });
    }

    // Return to Camp
    returnToCamp();
  };

  const handleExamFailed = () => {
    // Return to Camp with 1 HP
    updatePlayer({ hp: 1 });
    returnToCamp();
  };

  return (
    <div className="promotion-tab">
      {/* Current Grade and Next Grade */}
      <div className="grade-display">
        <div className="current-grade-box">
          <span className="grade-label">Current</span>
          <span className="grade-name">{exam.currentGrade}</span>
        </div>
        <div className="arrow">→</div>
        <div className="next-grade-box">
          <span className="grade-label">Next</span>
          <span className="grade-name">{exam.nextGrade}</span>
        </div>
      </div>

      {/* Exam Requirements */}
      <section className="exam-requirements">
        <h3>◆ Requirements</h3>
        <div
          className={`requirement ${meetsCardRequirement ? "met" : "unmet"}`}
        >
          [{meetsCardRequirement ? "✓" : "✗"}] Cards Owned: {cardCount}/
          {exam.requiredCardCount}
        </div>
        {exam.requiredGold && (
          <div
            className={`requirement ${meetsGoldRequirement ? "met" : "unmet"}`}
          >
            [{meetsGoldRequirement ? "✓" : "✗"}] Gold Owned: {player.gold}/
            {exam.requiredGold}G
          </div>
        )}
      </section>

      {/* Exam Details */}
      <section className="exam-details">
        <h3>◆ Details</h3>
        <p>{exam.description}</p>
        <div className="recommendations">
          <p>Recommended HP: {exam.recommendations.hp}+</p>
          <p>Recommended AP: {exam.recommendations.ap}+</p>
        </div>
      </section>

      {/* Rewards */}
      <section className="exam-rewards">
        <h3>◆ Rewards</h3>
        <ul>
          <li>Title: {exam.nextGrade}</li>
          <li>{exam.rewards.statBonus}</li>
          {exam.rewards.items && (
            <li>Rare Equipment x{exam.rewards.items.length}</li>
          )}
        </ul>
      </section>

      {/* Warning */}
      <div className="exam-warning">
        ⚠️ Starting the exam will initiate a battle. Please prepare your
        equipment!
      </div>

      {/* Start Button */}
      <button
        className="start-exam-button"
        disabled={!canTakeExam}
        onClick={handleStartExam}
      >
        {canTakeExam ? "Start Exam" : "Requirements Not Met"}
      </button>
    </div>
  );
};

export default PromotionTab;
```

**✅ Completion Check:**

- [ ] `PromotionTab.tsx` created.
- [ ] Requirement checks work.
- [ ] Button enables/disables correctly.

---

### Task 3.3: Creating Placeholder Tabs

```typescript
// src/camps/facilities/Guild/RumorsTab.tsx
const RumorsTab: React.FC = () => {
  return (
    <div className="rumors-tab">
      <h2>Rumors</h2>
      <p className="coming-soon">Coming Soon...</p>
      <p>Use magic stones to grant buffs for exploration.</p>
    </div>
  );
};

export default RumorsTab;
```

```typescript
// src/camps/facilities/Guild/QuestsTab.tsx
const QuestsTab: React.FC = () => {
  return (
    <div className="quests-tab">
      <h2>Quests</h2>
      <p className="coming-soon">Coming Soon...</p>
      <p>Accept daily/weekly quests.</p>
    </div>
  );
};

export default QuestsTab;
```

---

### Task 3.4: BaseCamp to Guild Transition

```typescript
// src/camps/campsUI/BaseCamp.tsx (Fix)

import { useGameState } from "../../contexts/GameStateContext";

const BaseCamp = () => {
  const { navigateTo } = useGameState();
  const [selectedFacility, setSelectedFacility] = useState<FacilityType | null>(
    null
  );

  const facilities: FacilityCardProps[] = [
    // ... Existing facilities
    {
      type: "tavern",
      name: "Tavern",
      description: "Rumors, Quests, Promotion Exams",
      icon: "🍺",
      isUnlocked: true, // ✅ Unlock
      onEnter: () => navigateTo("guild"), // ✅ Transition to Guild
    },
  ];

  // ... Remaining code
};
```

**✅ Completion Check:**

- [ ] Can click Tavern from BaseCamp.
- [ ] Guild component displays.
- [ ] Back button returns to BaseCamp.

---

## Phase 4: Battle System Integration (Week 3-4)

### Task 4.1: Extend BattleScreen

```typescript
// src/battles/battleUI/BattleScreen.tsx (Fix)

interface BattleScreenProps {
  depth: Depth;
  onDepthChange: (depth: Depth) => void;
  battleMode?: "normal" | "exam" | "return_route";
  enemyIds?: string[];
  onBattleEnd?: (result: "victory" | "defeat") => void;
}

const BattleScreen: React.FC<BattleScreenProps> = ({
  depth,
  onDepthChange,
  battleMode = "normal",
  enemyIds,
  onBattleEnd,
}) => {
  // ... Existing logic

  // Branch enemy initialization
  useEffect(() => {
    if (battleMode === "exam" && enemyIds) {
      // Exam Mode: Spawn specific enemies
      const examEnemies = enemyIds.map((id) => {
        const enemyData = GUILD_ENEMIES.find((e) => e.id === id);
        return createEnemyState(enemyData);
      });
      setEnemies(examEnemies);
    } else {
      // Normal Mode: Existing logic
      const { enemies: randomEnemies } = selectRandomEnemy(depth, "normal");
      setEnemies(randomEnemies);
    }
  }, [battleMode, enemyIds, depth]);

  // Branch win/loss logic
  useEffect(() => {
    if (aliveEnemies.length === 0 && !battleResult) {
      setBattleResult("victory");
      if (onBattleEnd) onBattleEnd("victory");
    }

    if (playerHp <= 0 && !battleResult) {
      setBattleResult("defeat");
      if (onBattleEnd) onBattleEnd("defeat");
    }
  }, [aliveEnemies, playerHp, battleResult, onBattleEnd]);

  // ... Remaining code
};
```

**✅ Completion Check:**

- [ ] `battleMode` prop added.
- [ ] Specific enemies spawn in `exam` mode.
- [ ] `onBattleEnd` called on win/loss.

---

## Testing Procedures

### Basic Operation Test

```
□ Context Integration Test
  □ GameStateContext state transitions
  □ PlayerContext updates
  □ InventoryContext operations

□ Screen Transition Test
  □ BaseCamp → Guild
  □ Guild → Tab Switching
  □ Guild → Exam → Battle → BaseCamp

□ Promotion Exam Test
  □ Button disabled when requirements not met
  □ Start Exam
  □ Battle with Enemy
  □ Promotion Process on Victory
  □ Return with 1 HP on Defeat

```

---

## Troubleshooting

### Common Errors

**1. Context is undefined**

```
Cause: Hook is used outside of Provider.
Solution: Check if Provider is correctly placed in App.tsx.

```

**2. Type error on Player.equipped**

```
Cause: Existing code assumes equipment array structure.
Solution: Add compatibility layer or fix corresponding code.

```

**3. Exam Battle does not start**

```
Cause: GameStateContext update not reflected.
Solution: Check if startBattle function is called correctly.

```

---

## Next Steps

After completing Phase 4:

1. Implement Rumor System
2. Implement Quest System
3. Extend Equipment Quality System
4. Integrate Save/Load features
