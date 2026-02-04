> ⚠️ **DEPRECATED**: This design document has been superseded by the Journal system.
>
> The Library facility is being integrated into the **Journal (手記)** system, which provides a unified UI accessible from the header.
>
> **Please refer to:** `.claude/docs/journal_document/journal_system_implementation_plan.md`
>
> **Migration mapping:**
> - Book of Builds (デッキ編成) → Journal Chapter 1「戦術」
> - Book of Encyclopedia (図鑑) → Journal Chapter 2「記憶」
> - Book of Records (アチーブメント) → Not yet migrated
> - Chamber of Memories (セーブ/ロード) → Journal「設定」
>
> The content below is preserved for historical reference.

---

Here is the English translation of the Library Detailed Design Document.

# Library Detailed Design Document (LIBRARY_DESIGN_V1)

## Update History

- V1.0: Initial Draft (Deck Building, Encyclopedia, Achievements, Save/Load)

---

## 1. Overview

The Grand Library is the **Hall of Knowledge and Records**.

It is the facility that supports the player's "Meta-game" by managing deck building, the encyclopedia, achievement records, and save data.

### Primary Roles

1. **Build Management**: Combination of decks, equipment, and items.
2. **Knowledge Accumulation (Encyclopedia)**: Records of cards, equipment, and monsters.
3. **Progression Records (Achievements)**: Titles and completion rates.
4. **Memory Storage (Save/Load)**: Save data management.

---

## 2. Detailed Functional Specifications

### 2.1 The 4 Bookshelves (Main Menu)

```
┌────────────────────────────────────────────┐
│  📚 The Grand Library                     │
├────────────────────────────────────────────┤
│                                            │
│      [Please select a book from the shelf] │
│                                            │
│  ┌──────────┐  ┌──────────┐              │
│  │  📖      │  │  📕      │              │
│  │ Book of  │  │ Book of  │              │
│  │ Builds   │  │ Encyclo- │              │
│  │          │  │ pedia    │              │
│  │Deck Edit │  │Cards     │              │
│  │Equip Sel │  │Equipment │              │
│  │          │  │Monsters  │              │
│  └──────────┘  └──────────┘              │
│                                            │
│  ┌──────────┐  ┌──────────┐              │
│  │  📘      │  │  📗      │              │
│  │ Book of  │  │ Chamber  │              │
│  │ Records  │  │ of Memory│              │
│  │          │  │          │              │
│  │Titles    │  │Save      │              │
│  │Achieve-  │  │Load      │              │
│  │ments     │  │          │              │
│  └──────────┘  └──────────┘              │
│                                            │
│  [Return to Camp]                          │
└────────────────────────────────────────────┘

```

---

## 3. Book of Builds (Build Management)

### 3.1 Deck Building Screen

```
┌────────────────────────────────────────────────────────┐
│  📖 Book of Builds - Deck Editor                       │
├────────────────────────────────────────────────────────┤
│  Current Character: Swordsman                          │
│                                                        │
│  ┌──────────────────┐  ┌────────────────────────────┐ │
│  │ Current Deck (40) │  │ Card Pool                  │ │
│  │                  │  │ [All][Atk][Def][Special]   │ │
│  │ [Attack Cards]   │  │                            │ │
│  │ ⚔️ Slash x4      │  │ ⚔️ Slash (Owned: 4/4)      │ │
│  │ 🔥 Flame Cut x2  │  │ 🔥 Flame Cut (Owned: 2/3)  │ │
│  │                  │  │ ⚡ Lightning (Owned: 0/2)🔒│ │
│  │ [Defense Cards]  │  │                            │ │
│  │ 🛡️ Defend x3     │  │ 🛡️ Defend (Owned: 3/4)     │ │
│  │                  │  │ ...                        │ │
│  │ [Special Cards]  │  │                            │ │
│  │ 💊 Heal x2       │  │                            │ │
│  │                  │  │                            │ │
│  └──────────────────┘  └────────────────────────────┘ │
│                                                        │
│  Mana Curve: [0|1███|2████|3██|4█|5]                  │
│                                                        │
│  [Save Loadout]  [Set 1] [Set 2] [Set 3]               │
│  [Reset]  [Back]                                       │
└────────────────────────────────────────────────────────┘

```

### 3.2 Deck Building Rules

**Basic Rules:**

- Deck Size: 40 cards (Fixed).
- Max Copies: Up to 4 of the same card.
- Only class-specific cards can be used.

**Card States:**

- **Owned**: Can be added to the deck.
- **Unowned**: Grayed out, marked with 🔒.
- **Insufficient Mastery**: Some cards are unlocked via Mastery.

### 3.3 Equipment & Item Setup

```
┌────────────────────────────────────────────┐
│  Equipment Selection                       │
├────────────────────────────────────────────┤
│  Equipment Slots:                          │
│  [Weapon]  🗡️ Iron Sword (Lv2, normal)     │
│  [Armor]   🛡️ Knight Armor (Lv1, good)     │
│  [Helmet]  👑 (Unequipped)                 │
│  [Boots]   👢 Leather Boots (Lv0, poor)    │
│  [Accessory1] 💍 Power Ring (Lv0, master)  │
│  [Accessory2] (Unequipped)                 │
│                                            │
│  Initial Items (Max 3):                    │
│  [1] 🧪 Small Potion                       │
│  [2] 🔮 Teleport Stone (Normal)            │
│  [3] (Not Selected)                        │
└────────────────────────────────────────────┘

```

### 3.4 Save Loadout

**3 Sets Available:**

- Set 1: "Balanced"
- Set 2: "Attack Focused"
- Set 3: "Defense Focused"

**Functions:**

- Save current configuration.
- One-click switching.
- Renamable.

---

## 4. Book of Encyclopedia

### 4.1 Card Encyclopedia

```
┌────────────────────────────────────────────────────────┐
│  📕 Book of Encyclopedia - Card Index                  │
├────────────────────────────────────────────────────────┤
│  [All][Swordsman][Mage][Summoner][Common]              │
│  Discovered: 45 / 140 (32%)                            │
│                                                        │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐          │
│  │ ⚔️       │  │ 🔥       │  │ ⚡       │          │
│  │ Slash    │  │ Flame Cut│  │ ???      │          │
│  │          │  │          │  │          │          │
│  │Mastery: 3│  │Mastery: 1│  │ Unknown  │          │
│  └──────────┘  └──────────┘  └──────────┘          │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ [Selected Card Details]                         │   │
│  │ ⚔️ Slash                                        │   │
│  │                                                │   │
│  │ Cost: 1                                        │   │
│  │ Type: Attack                                   │   │
│  │ Effect: Deal 8 damage to an enemy.             │   │
│  │                                                │   │
│  │ Mastery: Lv3 (Uses: 245 / 250)                 │   │
│  │ Next Level: Lv4 (Evolution Unlock)             │   │
│  │                                                │   │
│  │ Evolution Paths:                               │   │
│  │ - [Power] Flame Cut: Dmg 12, inflicts Burn     │   │
│  │ - [Tech] Twin Slash: Dmg 6x2                   │   │
│  │                                                │   │
│  └────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────┘

```

### 4.2 Equipment Encyclopedia

```
┌────────────────────────────────────────────────────────┐
│  📕 Book of Encyclopedia - Equipment Index             │
├────────────────────────────────────────────────────────┤
│  [All][Weapon][Armor][Helm][Boots][Accessory]          │
│  Discovered: 28 / 73 (38%)                             │
│                                                        │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐          │
│  │ 🗡️       │  │ ⚔️       │  │ 🔱       │          │
│  │ Iron Swd │  │ Steel Swd│  │ ???      │          │
│  │          │  │          │  │          │          │
│  │Common    │  │Rare      │  │ Unknown  │          │
│  └──────────┘  └──────────┘  └──────────┘          │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ [Selected Equipment Details]                    │   │
│  │ 🗡️ Iron Sword                                   │   │
│  │                                                │   │
│  │ Rarity: Common                                 │   │
│  │ Slot: Weapon                                   │   │
│  │                                                │   │
│  │ Base Stats:                                    │   │
│  │ ATK: +10                                       │   │
│  │ AP: 50                                         │   │
│  │                                                │   │
│  │ Acquisition:                                   │   │
│  │ - Shop: Common Equipment Pack                  │   │
│  │ - Drop: Depth 1-2 Enemies                      │   │
│  │                                                │   │
│  └────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────┘

```

### 4.3 Monster Encyclopedia

```
┌────────────────────────────────────────────────────────┐
│  📕 Book of Encyclopedia - Monster Index               │
├────────────────────────────────────────────────────────┤
│  [All][Depth1][Depth2][Depth3][Depth4][Depth5][Boss]   │
│  Discovered: 18 / 45 (40%)                             │
│                                                        │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐          │
│  │ 👻       │  │ 🧟       │  │ 🐺       │          │
│  │ Goblin   │  │ Skeleton │  │ ???      │          │
│  │          │  │          │  │          │          │
│  │Killed: 32│  │Killed: 18│  │ Unknown  │          │
│  └──────────┘  └──────────┘  └──────────┘          │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ [Selected Monster Details]                      │   │
│  │ 👻 Goblin                                      │   │
│  │                                                │   │
│  │ HP: 30                                         │   │
│  │ Habitat: Depth 1-2                             │   │
│  │                                                │   │
│  │ Behavior Pattern:                              │   │
│  │ - Attack: 5 Damage                             │   │
│  │ - Defend: Guard +3                             │   │
│  │                                                │   │
│  │ Drops:                                         │   │
│  │ - Gold: 10-15                                  │   │
│  │ - Magic Stone (S): 10%                         │   │
│  │ - Equipment: 5% (Common)                       │   │
│  │                                                │   │
│  │ Defeat Count: 32 Times                         │   │
│  └────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────┘

```

---

## 5. Book of Records (Achievements)

### 5.1 Title System

```
┌────────────────────────────────────────────────────────┐
│  📘 Book of Records - Titles                           │
├────────────────────────────────────────────────────────┤
│  Current Title: 🏆 Goblin Slayer                       │
│                                                        │
│  Unlocked Titles: 12 / 50                              │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ 🏆 Goblin Slayer                                │   │
│  │ Unlocked: 2026/01/05                            │   │
│  │ Condition: Defeat 100 Goblins                   │   │
│  │ Effect: Gold from Goblins +10%                  │   │
│  └────────────────────────────────────────────────┘   │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ 🔥 Flame Wielder                                │   │
│  │ Unlocked: 2026/01/08                            │   │
│  │ Condition: Use Fire cards 100 times             │   │
│  │ Effect: None (Cosmetic)                         │   │
│  └────────────────────────────────────────────────┘   │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ ??? Locked                                      │   │
│  │ Condition: ???                                  │   │
│  │ Hint: Reach Depth 5                             │   │
│  └────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────┘

```

### 5.2 Achievements & Statistics

```
┌────────────────────────────────────────────┐
│  📘 Book of Records - Achievements         │
├────────────────────────────────────────────┤
│  Total Play Time: 45h 32m                  │
│  Total Runs: 87                            │
│                                            │
│  【Exploration Records】                   │
│  Deepest Depth: Depth 4 (Pre-Boss)         │
│  Clears: 0                                 │
│  Deaths: 87                                │
│                                            │
│  【Combat Records】                        │
│  Total Kills: 1,234                        │
│  Max Damage: 156                           │
│  Longest Combo: Survived 8 Turns           │
│                                            │
│  【Economy Records】                       │
│  Total Gold Earned: 45,600                 │
│  Total Souls Earned: 850                   │
│  Max Gold Held: 3,200                      │
│                                            │
│  【Collection】                            │
│  Card Discovery: 45/140 (32%)              │
│  Equip Discovery: 28/73 (38%)              │
│  Monster Encounter: 18/45 (40%)            │
└────────────────────────────────────────────┘

```

---

## 6. Chamber of Memories (Save/Load)

### 6.1 Save Data Management

```
┌────────────────────────────────────────────────────────┐
│  📗 Chamber of Memories - Save/Load                    │
├────────────────────────────────────────────────────────┤
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ Slot 1: [In Use]                                │   │
│  │ Character: Swordsman (Lv.15)                    │   │
│  │ Soul Remnants: 150                              │   │
│  │ Current Depth: Depth 3                          │   │
│  │ Play Time: 12h 45m                              │   │
│  │ Last Save: 2026/01/09 14:30                     │   │
│  │                                                │   │
│  │ [Load] [Overwrite] [Delete]                     │   │
│  └────────────────────────────────────────────────┘   │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ Slot 2: [In Use]                                │   │
│  │ Character: Mage (Lv.8)                          │   │
│  │ Soul Remnants: 80                               │   │
│  │ Current Depth: Depth 2                          │   │
│  │ Play Time: 5h 20m                               │   │
│  │ Last Save: 2026/01/08 20:15                     │   │
│  │                                                │   │
│  │ [Load] [Overwrite] [Delete]                     │   │
│  └────────────────────────────────────────────────┘   │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ Slot 3: [Empty]                                 │   │
│  │                                                │   │
│  │ [Create New]                                    │   │
│  └────────────────────────────────────────────────┘   │
│                                                        │
│  [Export] [Import] [Back]                              │
└────────────────────────────────────────────────────────┘

```

### 6.2 Auto-Save

**Auto-Save Triggers:**

- Returning to BaseCamp
- After using a facility
- Before starting exploration
- Every 5 minutes (Background)

**Save Content:**

```typescript
{
  player: {
    character: 'swordsman',
    gold: 1250,
    soulRemnants: 150,
    hp: 100,
    maxHp: 100,
    sanctuaryProgress: {...},
    // ...
  },
  inventory: [...],
  deck: [...],
  equipment: {...},
  library: {
    encyclopedia: {...},
    achievements: [...],
    statistics: {...}
  },
  timestamp: '2026-01-09T14:30:00Z'
}

```

### 6.3 Export/Import

**Export:**

- Download as JSON format.
- Filename: `roguelike_save_20260109_143000.json`.
- Used for backups.

**Import:**

- Upload JSON file.
- Data validation.
- Overwrite confirmation.

---

## 7. Data Structure Definition

### 7.1 LibraryTypes.ts

```typescript
// src/types/LibraryTypes.ts (New File)

/**
 * Loadout (Deck Configuration Set)
 */
export interface Loadout {
  id: string;
  name: string;
  deck: string[]; // Array of Card IDs
  equipment: {
    weapon?: string;
    armor?: string;
    helmet?: string;
    boots?: string;
    accessory1?: string;
    accessory2?: string;
  };
  initialItems: string[]; // Item IDs (Max 3)
}

/**
 * Encyclopedia Data
 */
export interface Encyclopedia {
  cards: {
    discovered: Set<string>;
    mastery: Map<string, number>; // cardId -> Mastery Level
    useCount: Map<string, number>; // cardId -> Use Count
  };
  equipment: {
    discovered: Set<string>;
  };
  monsters: {
    encountered: Set<string>;
    defeatCount: Map<string, number>; // monsterId -> Defeat Count
  };
}

/**
 * Title (Achievement)
 */
export interface Title {
  id: string;
  name: string;
  description: string;
  icon: string;
  condition: string;
  effect?: string;
  unlockedAt?: Date;
}

/**
 * Statistics
 */
export interface Statistics {
  totalPlayTime: number; // Seconds
  totalRuns: number;
  deepestDepth: number;
  clearCount: number;
  deathCount: number;
  totalDefeats: number;
  maxDamage: number;
  longestCombo: number;
  totalGoldEarned: number;
  totalSoulEarned: number;
  maxGoldHeld: number;
}

/**
 * Library State
 */
export interface LibraryState {
  loadouts: Loadout[];
  currentLoadout: string; // loadout id
  encyclopedia: Encyclopedia;
  unlockedTitles: Set<string>;
  currentTitle: string | null;
  statistics: Statistics;
}
```

---

## 8. Encyclopedia Update Logic

### 8.1 Card Encyclopedia Update

```typescript
// src/camps/facilities/Library/logic/updateEncyclopedia.ts

import type { Encyclopedia } from "../../../../types/LibraryTypes";

/**
 * Update encyclopedia when a card is used
 */
export function recordCardUse(
  encyclopedia: Encyclopedia,
  cardId: string
): Encyclopedia {
  const updated = { ...encyclopedia };

  // Add to Discovered
  updated.cards.discovered.add(cardId);

  // Count Usage
  const currentCount = updated.cards.useCount.get(cardId) || 0;
  updated.cards.useCount.set(cardId, currentCount + 1);

  // Check Mastery Level Up
  const newCount = currentCount + 1;
  const currentMastery = updated.cards.mastery.get(cardId) || 0;

  // Mastery Thresholds: 50, 150, 250, 400, 600...
  const thresholds = [50, 150, 250, 400, 600];
  const newMastery = thresholds.findIndex((t) => newCount < t) + 1;

  if (newMastery > currentMastery) {
    updated.cards.mastery.set(cardId, newMastery);
    // Fire level up event
  }

  return updated;
}

/**
 * Update encyclopedia when equipment is acquired
 */
export function recordEquipmentAcquired(
  encyclopedia: Encyclopedia,
  equipmentTypeId: string
): Encyclopedia {
  const updated = { ...encyclopedia };
  updated.equipment.discovered.add(equipmentTypeId);
  return updated;
}

/**
 * Update encyclopedia when encountering a monster
 */
export function recordMonsterEncounter(
  encyclopedia: Encyclopedia,
  monsterId: string,
  defeated: boolean
): Encyclopedia {
  const updated = { ...encyclopedia };

  updated.monsters.encountered.add(monsterId);

  if (defeated) {
    const currentCount = updated.monsters.defeatCount.get(monsterId) || 0;
    updated.monsters.defeatCount.set(monsterId, currentCount + 1);
  }

  return updated;
}
```

---

## 9. Context API Integration

### 9.1 Creating LibraryContext

```typescript
// src/contexts/LibraryContext.tsx (New File)

import { createContext, useContext, useState, ReactNode } from "react";
import type {
  LibraryState,
  Loadout,
  Encyclopedia,
  Statistics,
} from "../types/LibraryTypes";

interface LibraryContextValue {
  libraryState: LibraryState;

  // Loadouts
  saveLoadout: (loadout: Loadout) => void;
  loadLoadout: (loadoutId: string) => void;
  deleteLoadout: (loadoutId: string) => void;

  // Encyclopedia
  updateEncyclopedia: (update: Partial<Encyclopedia>) => void;

  // Titles
  unlockTitle: (titleId: string) => void;
  setCurrentTitle: (titleId: string | null) => void;

  // Statistics
  updateStatistics: (update: Partial<Statistics>) => void;
}

const LibraryContext = createContext<LibraryContextValue | undefined>(
  undefined
);

export const LibraryProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const [libraryState, setLibraryState] = useState<LibraryState>(() => {
    // Load from localStorage
    const saved = localStorage.getItem("library");
    if (saved) {
      return JSON.parse(saved);
    }

    return {
      loadouts: [],
      currentLoadout: "",
      encyclopedia: {
        cards: {
          discovered: new Set(),
          mastery: new Map(),
          useCount: new Map(),
        },
        equipment: {
          discovered: new Set(),
        },
        monsters: {
          encountered: new Set(),
          defeatCount: new Map(),
        },
      },
      unlockedTitles: new Set(),
      currentTitle: null,
      statistics: {
        totalPlayTime: 0,
        totalRuns: 0,
        deepestDepth: 0,
        clearCount: 0,
        deathCount: 0,
        totalDefeats: 0,
        maxDamage: 0,
        longestCombo: 0,
        totalGoldEarned: 0,
        totalSoulEarned: 0,
        maxGoldHeld: 0,
      },
    };
  });

  // Save Loadout
  const saveLoadout = (loadout: Loadout) => {
    setLibraryState((prev) => ({
      ...prev,
      loadouts: [...prev.loadouts.filter((l) => l.id !== loadout.id), loadout],
    }));
  };

  // ... Other methods

  return (
    <LibraryContext.Provider
      value={{
        libraryState,
        saveLoadout,
        loadLoadout,
        deleteLoadout,
        updateEncyclopedia,
        unlockTitle,
        setCurrentTitle,
        updateStatistics,
      }}
    >
      {children}
    </LibraryContext.Provider>
  );
};

export const useLibrary = () => {
  const context = useContext(LibraryContext);
  if (!context) {
    throw new Error("useLibrary must be used within LibraryProvider");
  }
  return context;
};
```

---

## 10. Implementation Procedure (Overview)

### Phase 1: Data Structures (Week 1: Day 1-2)

```
□ Create LibraryTypes.ts
□ Create LibraryContext
□ Initialize Encyclopedia Data

```

### Phase 2: Book of Builds (Week 1: Day 3 - Week 2: Day 1)

```
□ DeckBuilder.tsx (Deck Edit UI)
□ EquipmentSelector.tsx (Equipment Selection)
□ LoadoutManager.tsx (Loadout Management)

```

### Phase 3: Book of Encyclopedia (Week 2: Day 2-3)

```
□ Encyclopedia.tsx (Main Index)
□ CardEncyclopedia.tsx (Cards)
□ EquipmentEncyclopedia.tsx (Equipment)
□ MonsterEncyclopedia.tsx (Monsters)

```

### Phase 4: Book of Records (Week 2: Day 4)

```
□ Achievements.tsx (Titles UI)
□ Statistics.tsx (Statistics UI)

```

### Phase 5: Chamber of Memories (Week 3: Day 1-2)

```
□ SaveLoad.tsx (Save/Load UI)
□ Save Data Validation
□ Export/Import Functionality

```

---

## 11. Notes

### 11.1 Data Persistence

**Important:** `LibraryState` requires complete persistence.

- Save to `localStorage`.
- Regular auto-saves.
- Backup in case of data corruption.

### 11.2 Encyclopedia Update Timing

**During Battle:**

- Card Use → `recordCardUse`
- Encounter Monster → `recordMonsterEncounter`

**Upon Acquiring Items:**

- Obtain Equipment → `recordEquipmentAcquired`

**Statistics Update:**

- Batch update at the end of a Run.

---

## 12. Reference Documents

```
BASE_CAMP_DESIGN_V2
└── LIBRARY_DESIGN_V1 [This Document]
    ├── LibraryContext.tsx
    ├── updateEncyclopedia.ts
    └── SaveLoadManager.ts

```
