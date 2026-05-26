# Design Change Proposal: Introduction of Life System (Retries)

## Revision History

* 2026-01-23: Initial version created
* 2026-01-23: **Phase 1-3 Implementation Completed**

---

## Implementation Status

### Completed (Phase 1-3)

* [x] **LivesSystem Type Definitions** (`playerTypes.ts`)
* `Difficulty` type, `LivesSystem` interface, `LIVES_BY_DIFFICULTY` constant
* Helper functions: `createLivesSystem()`, `decreaseLives()`, `isGameOver()`


* [x] **PlayerContext Update** (`PlayerContext.tsx`)
* Added `lives` and `difficulty` to `RuntimeBattleState`
* Added `decreaseLives()`, `isGameOver()`, and `setDifficulty()` methods


* [x] **Death Handling Logic** (`deathHandler.ts`)
* 100% Soul Remnant acquisition (all transferred even on death)
* `handlePlayerDeathWithDetails()` now returns the amount of transferred souls


* [x] **DefeatScreen UI** (`DefeatScreen.tsx`)
* Lives display (Heart icons)
* Soul acquisition display
* Game Over state display


* [x] **HP/AP Persistence Bug Fix**
* Added `InitialPlayerState` interface to `useBattleState.ts`
* Pass initial values from PlayerContext in `useBattleOrchestrator.ts`
* Save HP/AP upon victory in `BattleScreen.tsx`


* [x] **Mastery Persistence Bug Fix**
* Added `cardMasteryStore` to `RuntimeBattleState`
* Aggregates and saves mastery based on `cardTypeId` from the deck upon victory
* Applies mastery from `masteryStore` to the deck at the start of the next battle



### Pending (Phase 4-5)

* [ ] Unification of Teleport Stone system (currently unimplemented feature)
* [ ] Implementation of the Abyss escape route
* [ ] Game Over screen (Full Reset process)
* [ ] Detailed update of design documents (remaining documentation)
* [ ] Testing and verification

---

## 1. Plan Overview

### 1.1 Objective

Change the "Exploration Limit" system to a "Life System (Retries)" to achieve more rigorous and clear risk management.

### 1.2 Summary of Major Changes

| Item | Old Design | New Design |
| --- | --- | --- |
| Progression Management | Exploration Count Limit (10) | **Life System (Retries)** |
| Max Lives | - | **Hard: 2 / Normal & Easy: 3** |
| Life Decrease Timing | - | **Only upon death** |
| Life Recovery | - | **None** |
| Successful Return | Exploration count +1 | **No change to Lives** |
| Death at 0 Lives | - | **Game Over (Hard Reset)** |
| Teleport Stone Types | 3 types (70%/80%/60% returns) | **Unified into 1 type (100% return)** |
| Death Penalty | Loss of gear, items, gold, & souls | **Loss of all items/gear + 100% Soul Gain** |
| Abyss Clear | Auto-return after boss defeat | **Escape Route appears after boss defeat** |
| After Game Over | Partial Reset | **Full Reset (Achievements only persist)** |

---

## 2. Detailed Design

### 2.1 Life System

#### 2.1.1 Basic Specifications

```typescript
interface LivesSystem {
  // Max lives by difficulty
  maxLives: {
    easy: 3,
    normal: 3,
    hard: 2
  };

  // Current lives
  currentLives: number;

  // Decrease trigger
  decreaseOn: 'death_only';  // Only upon death

  // Recovery
  recovery: 'none';  // No means of recovery
}

```

#### 2.1.2 Life Decrease Conditions

| Situation | Life Change | Remarks |
| --- | --- | --- |
| Death during normal exploration | **-1** | Death in Depths 1-4 |
| Death in The Abyss (Depth 5) | **-1** | Includes boss battles |
| Return via Return Route | **No Change** | Successful survival |
| Return via Teleport Stone | **No Change** | Successful survival |
| Escape after Abyss Boss defeat | **No Change** | Treated as Game Clear |

#### 2.1.3 Game Over Condition

```
Death while currentLives = 0 → Game Over → Full Reset

```

---

### 2.2 Death Penalty (Revised)

#### 2.2.1 Old Design

```
Upon Death:
- Gear in Dungeon: All lost
- Items in Dungeon: All lost
- Acquired Gold: All lost
- Acquired Soul Remnants: All lost (for that run only)
- Exploration Count: +1
- BaseCamp Storage: Retained

```

#### 2.2.2 New Design

```
Upon Death:
- Carried Items: **Total Loss (including equipment)**
- Owned Gold (earned in-run): **Total Loss**
- Soul Remnants: **100% Acquisition** (guaranteed even on death)
- Lives: **-1**
- BaseCamp Storage: Retained (Gear, Cards, Gold, Cumulative Souls)

```

#### 2.2.3 Design Intent

| Item | Reason |
| --- | --- |
| Total Item Loss | Clarifies the risk of death. Encourages cautious play as "bring-in" gear is also lost. |
| 100% Soul Gain | Ensures growth persists even after death. Souls are guaranteed in exchange for losing a Life. |
| BaseCamp Retention | A full reset only occurs at 0 Lives. Base resources are safe during normal deaths. |

---

### 2.3 Teleport Stone System (Unified)

#### 2.3.1 Old Design

```
3 Types of Teleport Stones:
- Standard: 70% reward, cannot use in battle
- Blessed: 80% reward, cannot use in battle
- Emergency: 60% reward, can use in battle

```

#### 2.3.2 New Design

```
1 Type of Teleport Stone:
- Teleport Stone (Unified Version)
  - Reward: 100% (All resources brought back)
  - Usage: Cannot use in battle; Map screen only
  - Abyss (Depth 5): Cannot be used (No change)

```

#### 2.3.3 Differentiation of Return Methods

| Return Method | Reward | Risk | Characteristics |
| --- | --- | --- | --- |
| Return Route | 100% | Battles present | Can earn additional souls; takes time |
| Teleport Stone | 100% | None | Immediate safe return; consumes item |

**Points of Differentiation:**

* Return Route: Risk of battle, but additional rewards (Souls/Mastery).
* Teleport Stone: Completely safe, but costs an item.

---

### 2.4 Abyss (Depth 5) Special Rules (Revised)

#### 2.4.1 Old Design

```
- Teleport Stone / Return Route: Unavailable
- Boss Defeat: Automatic Return (Clear)
- Death: Exploration Count +1 + Total Item Loss

```

#### 2.4.2 New Design

```
- Teleport Stone / Return Route: Unavailable (No change)
- Boss Defeat: **Escape Route appears** → Return safely
- Death: **Life -1** + Total Item Loss + **100% Soul Gain**

```

#### 2.4.3 Escape Route Specifications

```typescript
interface EscapeRoute {
  // Trigger condition
  trigger: 'boss_defeated';  // Appears after defeating the boss

  // Route content
  encounters: 'none';  // No battles

  // Rewards
  rewardMultiplier: 1.0;  // 100% extraction

  // UI message
  display: 'An escape route from the Abyss has opened...'
}

```

---

### 2.5 Game Over Handling (Revised)

#### 2.5.1 Old Design

```
When exploration limit is exceeded:
- Partial Reset
- Some progression is retained

```

#### 2.5.2 New Design

```
Upon death at 0 Lives:
- Full Reset
  - Gold: Returns to initial value (500G)
  - Equipment: Initial equipment only
  - Soul Remnants (Cumulative): Resets to 0
  - Sanctuary Unlocks: Reset
  - Card Deck: Returns to initial deck
  - Difficulty Settings: Maintained
  - Encyclopedia: Reset
  - Known Event Info: Reset

- Persistence
  - **Achievement Unlocks Only**

```

---

### 2.6 Survival Handling (Revised)

#### 2.6.1 Old Design

```
Upon Survival (Return Route / Teleport Stone):
- All items brought back (Teleport stone applies multiplier)
- Soul Remnants: Added to total based on return method multiplier
- Exploration Count: +1

```

#### 2.6.2 New Design

```
Upon Survival (Return Route / Teleport Stone):
- 100% of all items brought back (Unified)
- Soul Remnants: 100% added to total
- Lives: **No Change**

```

---

## 3. Impacted Documents and Change Locations

### 3.1 game_design_master.md

| Section | Content Change |
| --- | --- |
| 2.1 Core Loop | Exploration Limit → Life System |
| 2.2 Risk/Reward | Update Death Penalty |
| 2.3 Exploration Limit System | Complete rewrite into Life System |
| 3.2 Overall Game Flow | Rewrite phase descriptions based on Lives |
| 3.3 Ending Conditions | Rewrite failure conditions based on Lives |
| 5.3 Limit Balancing | Delete or change to Life balancing |

### 3.2 return_system_design.md

| Section | Content Change |
| --- | --- |
| 1.2 Survival vs Death | Update Lives and Soul gain rules |
| 3. Teleport Stone System | Unify to 1 type, 100% reward |
| 5. Abyss Special Rules | Add Escape Route |
| 6. Reward Calculation | Delete Teleport Stone multipliers |
| 7. Relation to Limit | Update to relation with Life system |

### 3.3 dungeon_exploration_ui_design_v2.1.md

| Section | Content Change |
| --- | --- |
| 1.3 Exploration System | Change to Life System |
| 4.1 Screen Layout | Change to Lives display |
| 5. Return System UI | Unify Teleport Stones, update reward display |
| 6.2 Dynamic Effects | Change to Life warning effects |

### 3.4 sanctuary_design.md

| Section | Content Change |
| --- | --- |
| 2.1 Soul Remnants | 100% gain on death rule |
| 2.2 Skill Tree | Delete exploration limit expansion skill |
| 5.3 Soul Remnant System | Change handling upon death |
| 6. PlayerContext Integration | Connect with Life System |
| 8.1 Balancing | Account for Soul gain upon death |

### 3.5 inventory_design.md

| Section | Content Change |
| --- | --- |
| 6.3 Loss Rule on Death | Update to "Loss of all owned items" |
| 6.4 Survival Rule | Update to unified 100% return |

### 3.6 guild_design.md (Minor)

| Section | Content Change |
| --- | --- |
| Exploration Count Refs | Change to Life references (where applicable) |

---

## 4. Data Structure Changes

### 4.1 New: LivesSystem

```typescript
// src/types/GameTypes.ts

export type Difficulty = 'easy' | 'normal' | 'hard';

export interface LivesSystem {
  maxLives: number;  // 2 or 3 depending on difficulty
  currentLives: number;
}

export const LIVES_BY_DIFFICULTY: Record<Difficulty, number> = {
  easy: 3,
  normal: 3,
  hard: 2,
};

```

### 4.2 Changed: PlayerContext

```typescript
// Old
interface Player {
  explorationLimit: {
    max: number;
    current: number;
  };
}

// New
interface Player {
  lives: {
    max: number;      // Max based on difficulty (2 or 3)
    current: number;  // Current lives
  };
  difficulty: Difficulty;
}

```

### 4.3 Changed: TeleportStone

```typescript
// Old
interface TeleportStone {
  type: 'normal' | 'blessed' | 'emergency';
  rewardMultiplier: number;  // 0.7 / 0.8 / 0.6
  canUseInBattle: boolean;
}

// New
interface TeleportStone {
  type: 'standard';  // Unified
  rewardMultiplier: 1.0;  // Fixed 100%
  canUseInBattle: false;  // Cannot be used in battle
}

```

### 4.4 Changed: SanctuaryProgress

```typescript
// Old
interface SanctuaryProgress {
  currentRunSouls: number;
  totalSouls: number;
  explorationLimitBonus: number;  // To be removed
}

// New
interface SanctuaryProgress {
  currentRunSouls: number;
  totalSouls: number;
  // explorationLimitBonus removed (no life expansion skill)
}

```

### 4.5 Changed: Death Handling

```typescript
// Old
function handleDeath(player: Player): void {
  // Reset run souls
  currentRunSouls = 0;
  // Exploration count +1
  explorationLimit.current++;
}

// New
function handleDeath(player: Player): void {
  // 100% Soul acquisition
  player.totalSouls += currentRunSouls;
  currentRunSouls = 0;

  // Life -1
  player.lives.current--;

  // Loss of all carried items
  player.inventory = getEmptyInventory();
  player.equipment = {};
  player.gold = 0;  // Only in-run Gold

  // Check Game Over
  if (player.lives.current <= 0) {
    triggerGameOver(player);
  }
}

```

---

## 5. UI Changes

### 5.1 Header Display

```
Old: Exploration: 7/10
New: Lives: ❤️❤️❤️ (3 left) or ❤️❤️ (2 left)

```

### 5.2 Return Menu

```
Old:
┌─────────────────────────────────────┐
│  1. Teleport (Normal)  Reward: 70%   │
│  2. Teleport (Blessed) Reward: 80%   │
│  3. Teleport (Emergency) Reward: 60% │
│  4. Return Route      Reward: 100%  │
└─────────────────────────────────────┘

New:
┌─────────────────────────────────────┐
│  1. Teleport Stone    Reward: 100%  │
│     Owned: 2          Return Instantly│
│                                     │
│  2. Return Route      Reward: 100%  │
│     Includes Battles  Extra Souls    │
└─────────────────────────────────────┘

```

### 5.3 Death Display

```
Old:
"Exploration Failed... You lost everything."
Exploration: 7 → 8

New:
"Exploration Failed... You lost everything."
"However, you secured the Soul Remnants..."
Souls: +85 → Total 650
Lives: ❤️❤️❤️ → ❤️❤️

```

### 5.4 Game Over Display

```
┌─────────────────────────────────────┐
│            GAME OVER                │
│                                     │
│  Your lives have run out...         │
│                                     │
│  Final Depth: Depth 4               │
│  Total Souls: 1,250                 │
│  Enemies Defeated: 87               │
│                                     │
│  [Unlocked achievements are kept]    │
│                                     │
│  [Start Over From Beginning]        │
└─────────────────────────────────────┘

```

---

## 6. Implementation Order

### Phase 1: Data Structure Changes (Day 1)

1. Define `LivesSystem` type
2. Update `PlayerContext` (`explorationLimit` → `lives`)
3. Unify `TeleportStone` type
4. Update `SanctuaryProgress`

### Phase 2: Logic Changes (Day 2)

1. Death handling (100% Soul gain + Life decrease)
2. Survival handling (No life change)
3. Game Over handling (Full Reset)
4. Abyss Escape Route logic

### Phase 3: UI Changes (Day 3)

1. Header lives display
2. Update Return Menu
3. Death sequence/presentation
4. Game Over screen

### Phase 4: Document Updates (Day 4)

1. game_design_master.md
2. return_system_design.md
3. dungeon_exploration_ui_design_v2.1.md
4. sanctuary_design.md
5. inventory_design.md

### Phase 5: Testing & Verification (Day 5)

1. Death → Life decrease flow
2. Survival → Life maintenance flow
3. Game Over → Full Reset flow
4. Abyss Escape Route
5. Max lives by difficulty check

---

## 7. Consistency Checklist

### 7.1 System Consistency

* [ ] Consistency between Life system and death penalties
* [ ] Differentiation between unified Teleport Stones and Return Route
* [ ] Consistency between Abyss special rules and Escape Route
* [ ] Consistency between Sanctuary Soul gain and death handling
* [ ] Consistency between Game Over reset and persistent data

### 7.2 UI Consistency

* [ ] Header display matches Life system
* [ ] Return Menu matches unified Teleport Stones
* [ ] Death sequence properly conveys 100% Soul gain
* [ ] Game Over screen properly explains Full Reset

### 7.3 Balance Consistency

* [ ] Validity of Max Lives by difficulty
* [ ] Balance between "Total Loss" and "Soul Gain" upon death
* [ ] Value balance between 100% Teleport Stone and Return Route

---

## 8. Impact Analysis

### 8.1 Impact on Game Experience

| Aspect | Old | New | Impact |
| --- | --- | --- | --- |
| Tension | Moderate (Limit consumption) | High (Life loss) | **UP** |
| Safety Net | Limit expansion skills | 100% Soul gain | Changed Form |
| Strategy | Limit management | Life preservation + Gear risk | **UP** |
| Replayability | Partial Reset | Full Reset | **DOWN** (Intentional) |

### 8.2 Removed Elements

1. **Exploration Limit System** → Replaced by Lives
2. **Limit Expansion Skills** → Deleted
3. **Teleport Stone Variations** → Unified

### 8.3 Added Elements

1. **Life System**
2. **Max Lives by Difficulty**
3. **Abyss Escape Route**
4. **100% Soul Gain on Death**

---

## 9. Approvals

Based on this plan, the following design documents will be updated:

1. game_design_master.md
2. return_system_design.md
3. dungeon_exploration_ui_design_v2.1.md
4. sanctuary_design.md
5. inventory_design.md
