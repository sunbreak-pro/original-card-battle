# Master Implementation Plan

> Unified task management document consolidating all previous plans
> Last Updated: 2026-01-29

---

## Table of Contents

1. [Completed Tasks Archive](#1-completed-tasks-archive)
2. [Current Status](#2-current-status)
3. [Priority Task List](#3-priority-task-list)
4. [Phase A: Core Loop (Completed)](#4-phase-a-core-loop-completed)
5. [Phase B: Game Experience Enhancement](#5-phase-b-game-experience-enhancement)
6. [Phase C: Extended Features](#6-phase-c-extended-features)
7. [Architecture Guidelines](#7-architecture-guidelines)
8. [Archived Design Decisions](#8-archived-design-decisions)

---

## 1. Completed Tasks Archive

### 1.1 Phase A: Core Loop (2026-01-26) ✅

All core loop systems implemented and verified:

| Task | Summary |
|------|---------|
| A1: Lives System | `LivesState` in `playerTypes.ts`, death handling in `PlayerContext.tsx`, `DefeatScreen.tsx` |
| A2: Soul Remnants | `soulSystem.ts`, 100% soul transfer on death (V3.0), battle integration |
| A3: Sanctuary Skill Tree | 17 nodes (Tier 1-3), `SanctuaryData.ts`, unlock logic, effect calculation |
| A4: Return System | `retreatFromDungeon()` works; teleport stone item not yet implemented |
| A5: Dungeon Map UI | `NodeMap.tsx`, `MapNode.tsx`, `dungeonLogic.ts`, map generation |

### 1.2 Buff/Debuff Ownership System (2026-01-26) ✅

- Added `BuffOwner` type and `appliedBy` field to `BuffDebuffState`
- Duration decreases only during applier's phase

### 1.3 Deck System Integration (2026-01-26) ✅

- `INITIAL_DECK_BY_CLASS` replaces hardcoded deck with 15-card class-specific decks
- `BattleScreen.tsx` uses `playerData.persistent`

### 1.4 Mage Character (2026-01-26) ✅

- `MAGE_CARDS`, `createMageStarterDeck()`, 15-card starter deck
- `isAvailable: true`, `uniqueMechanic: "Elemental Resonance"`

### 1.5 Character Select Unique Card Display (2026-01-26) ✅

- `StarterDeckPreview.tsx` filters to unique cards by `cardTypeId`

### 1.6 Type Definition Refactoring (2026-01-28) ✅

- Consolidated all types into `src/types/` (8 files)
- `@/types/*` path alias (tsconfig + vite)
- ~95 files updated, old `domain/*/type(s)/` removed
- Details: `.claude/todos/REFACTORING_PLAN_TYPES.md`

### 1.7 Shop Refactoring & ExchangeTab Bug Fix (2026-01-28) ✅

- Fixed ExchangeTab magic stone→gold conversion sync with FacilityHeader
- Created `ConsumableItemData.ts` as single source of truth
- Replaced `ShopItem` with `ShopListing` (references by `typeId`)

---

## 2. Current Status

### 2.1 Context Provider Hierarchy

```
GameStateProvider → ResourceProvider → PlayerProvider → InventoryProvider → DungeonRunProvider
```

### 2.2 Implementation Progress

| Category | Status | Notes |
|----------|--------|-------|
| Battle System | 90% | Core complete, elemental chain, escape, element system. Multi-enemy pending |
| Camp Facilities | 85% | Shop full, Guild full (Exam/Quests/Rumors), Library full (Card+Enemy encyclopedias) |
| Dungeon System | 75% | Map generation, battle/event/rest/treasure nodes, 5-floor progression |
| Progression System | 85% | Lives + Souls + Sanctuary + equipment stat bonuses + card derivation |

### 2.3 Known Bugs

All previous critical/high bugs resolved (BUG-1~4, MISS-1~6, FIX-1~3).

### 2.4 Known Quality Issues

| Issue | Location |
|-------|----------|
| FacilityHeader unused props | ESLint errors in Storage, Shop, Blacksmith |
| EnemyFrame emoji icons (should be SVG) | `src/ui/battleHtml/EnemyFrame.tsx:31` |
| Storage.tsx has commented-out code | `src/ui/campsHtml/Storage/Storage.tsx:53` |

---

## 3. Priority Task List

### Priority: CRITICAL (Bug Fixes)

| ID | Task | File | Details |
|----|------|------|---------|
| FIX-2 | Dungeon non-battle node UI | `src/ui/dungeonHtml/NodeMap.tsx` + `nodeEventLogic.ts` | Connect existing event/rest/treasure logic to UI instead of instant completion |
| FIX-3 | Huge stone calculation | `src/domain/camps/logic/shopLogic.ts` | Add huge stone (1000G) to `calculateMagicStoneTotal()` |

### Priority: HIGH (Phase B - Core Game Experience)

| ID | Task | Dependencies | File(s) |
|----|------|--------------|---------|
| B2 | Summoner Class (40 cards) | None | `src/domain/cards/data/summonerCards.ts` |
| B3 | Shop Full Implementation | None | Remaining: daily rotation, inventory limits |
| B4 | Guild Full Implementation | None | Quests/Rumors tabs |
| B5 | Card Derivation System | None | Card type extension, mastery unlock |
| B6 | Mage Elemental Chain | None | `src/domain/battles/managements/useClassAbility.ts` |
| B7 | Dungeon Floor Progression | FIX-2 | `src/ui/dungeonHtml/NodeMap.tsx` |
| B8 | Escape System | None | `src/ui/battleHtml/BattleScreen.tsx` |
| B9 | Exam Reward Application | None | `src/ui/campsHtml/Guild/Exam.tsx` |

### Priority: MEDIUM (Phase B - Architecture & Data)

| ID | Task | Dependencies | File(s) |
|----|------|--------------|---------|
| B1 | Context Separation | None | New: `PlayerBattleContext`, `EnemyBattleContext`, `BattleSessionContext` |
| B10 | Equipment Stat Bonus Definitions | None | `src/constants/data/items/EquipmentData.ts` |
| B11 | Magic Stone Value Constants | None | `src/domain/item_equipment/logic/itemUtils.ts` → extract to constants |

### Priority: LOW (Code Quality / Dead Code Cleanup)

| ID | Task | File | Details |
|----|------|------|---------|
| FIX-1 | Remove or implement `useMagicStones()` dead code | `src/contexts/ResourceContext.tsx` | Unused stub function with zero call sites; actual consumption handled by `ExchangeTab.tsx` |

### Priority: LOW (Phase C - Extended Features)

| ID | Task | Dependencies |
|----|------|--------------|
| C1 | Exploration Prep Screen | A5 |
| C2 | Legacy Interface Deletion | B1 |
| C3 | Multi-Enemy Battle | B1, C2 |
| C4 | Library Full Implementation | B5 |
| C5 | Dungeon Events/Rest/Treasure | FIX-2 |

---

## 4. Phase A: Core Loop (Completed)

Phase A is fully implemented. See [Section 1.1](#11-phase-a-core-loop-2026-01-26-) for summary.

**Core Game Loop:**
```
BaseCamp (Safe Zone)
    ↓ Start Exploration
Dungeon (Danger Zone)
    ↓ Battle → Gain Souls/Items
    ↓ Decision: Continue or Return
    ├── Survive → Keep Everything + Souls
    └── Death → Lose Items + Souls Saved + Lives -1
         ↓
    Lives > 0 → Return to BaseCamp
    Lives = 0 → Game Over (Full Reset)
```

---

## 5. Phase B: Game Experience Enhancement

### 5.0 Bug Fixes (CRITICAL)

#### ~~FIX-1: Magic Stone Consumption~~ → Reclassified as LOW (Dead Code Cleanup)

**Analysis Result:** `useMagicStones()` in `ResourceContext.tsx` is a stub with a TODO comment, but it has **zero call sites** in the entire codebase. Actual magic stone consumption is correctly handled by `ExchangeTab.tsx` using `calculateStonesToConsume()` + `setBaseCampMagicStones()`. No gameplay impact.

**Recommendation:** Delete the unused `useMagicStones()` function, or implement it as a reusable consumption API if needed in the future.

**File:** `src/contexts/ResourceContext.tsx:200-216`

#### FIX-2: Dungeon Non-Battle Node UI

**Problem:** When player reaches event/rest/treasure nodes, `NodeMap.tsx` calls `completeCurrentNode("victory")` immediately without showing any UI. The logic for these events exists in `nodeEventLogic.ts` but is not connected.

**Files:**
- `src/ui/dungeonHtml/NodeMap.tsx:105`
- `src/domain/dungeon/logic/nodeEventLogic.ts`

**Fix:** Create UI components for each node type and connect to existing logic.

#### FIX-3: Huge Stone Calculation

**Problem:** `calculateMagicStoneTotal()` in `shopLogic.ts` only accounts for small/medium/large stones, missing huge stone (1000G value).

**File:** `src/domain/camps/logic/shopLogic.ts:72`

**Fix:** Add huge stone case to the calculation function.

### 5.1 Task B1: Context Separation

**Purpose:** Improve architecture by separating battle-time state

**Implementation:**
- Create `PlayerBattleContext` (battle-only state)
- Create `EnemyBattleContext` (multi-enemy ready)
- Create `BattleSessionContext` (flow control)

**Files:**
- `src/domain/battles/contexts/PlayerBattleContext.tsx` (NEW)
- `src/domain/battles/contexts/EnemyBattleContext.tsx` (NEW)
- `src/domain/battles/contexts/BattleSessionContext.tsx` (NEW)

### 5.2 Task B2: Summoner Class

**Purpose:** Third playable character with summon mechanics

**Implementation:**
- Implement 40 Summoner cards from `SUMMONER_CARDS_40.md`
- Add to `CharacterClassData.ts`
- Create `summonerCards.ts`

### 5.3 Task B3: Shop Full Implementation

**Status:** In Progress (BuyTab/SellTab/ExchangeTab/PackTab working)

**Remaining:**
- Daily sales rotation
- Equipment purchase/sell UI
- Inventory capacity limits

### 5.4 Task B4: Guild Full Implementation

**Status:** Partial (Exam works)

**Remaining:**
- Rumors tab (information purchase)
- Quests tab (quest acceptance/rewards)

### 5.5 Task B5: Card Derivation System

**Implementation:**
- Add `unlockSource`, `unlockMasteryLevel` to Card type
- Mastery level triggers card unlock
- Notification UI on battle end
- Library derivation tree display

### 5.6 Task B6: Mage Elemental Chain

**Problem:** `useClassAbility.ts` has mage case as stub falling back to swordsman behavior.

**File:** `src/domain/battles/managements/useClassAbility.ts:260`

**Implementation:** Implement Elemental Resonance mechanic - consecutive same-element cards trigger chain bonuses.

### 5.7 Task B7: Dungeon Floor Progression

**Problem:** After boss defeat, game returns to camp instead of progressing to next floor.

**File:** `src/ui/dungeonHtml/NodeMap.tsx:129`

**Dependencies:** FIX-2

**Implementation:** Add floor transition logic after boss defeat, generate next floor map.

### 5.8 Task B8: Escape System

**Problem:** Escape button in battle UI exists but has no functionality.

**File:** `src/ui/battleHtml/BattleScreen.tsx:507`

**Implementation:** Connect button to retreat logic, add escape success/failure calculation.

### 5.9 Task B9: Exam Reward Application

**Problem:** Exam completion shows notification but doesn't apply stat bonuses or item rewards.

**File:** `src/ui/campsHtml/Guild/Exam.tsx:77-81`

**Implementation:** Apply stat bonuses to PlayerContext and add item rewards to Inventory.

### 5.10 Task B10: Equipment Stat Bonus Definitions

**Problem:** `EquipmentData` is an empty object `{}`. Equipment items have names and icons but no stat effects.

**File:** `src/constants/data/items/EquipmentData.ts`

**Implementation:** Define stat bonuses for all equipment types per design docs.

### 5.11 Task B11: Magic Stone Value Constants

**Problem:** Magic stone gold values are hardcoded in `itemUtils.ts`.

**File:** `src/domain/item_equipment/logic/itemUtils.ts:13`

**Implementation:** Extract to `src/constants/itemConstants.ts` for reuse.

---

## 6. Phase C: Extended Features

### 6.1 Task C1: Exploration Prep Screen ✅

`DungeonGate.tsx` with depth selection. Functional.

### 6.2 Task C2: Legacy Interface Deletion ✅ (2026-01-29)

- Deleted `Player` and `ExtendedPlayer` interfaces from `characterTypes.ts`
- Created `BasePlayerStats` interface in `PlayerData.tsx`
- Created `InternalPlayerState` in `PlayerContext.tsx` (replaces ExtendedPlayer)
- Removed legacy converter functions from `typeConverters.ts`

### 6.3 Task C3: Multi-Enemy Battle (PENDING)

- Display multiple enemies in battle UI
- Target selection UI (click to select)
- AoE attack support (damage all enemies)
- Enemy phase: each alive enemy acts in sequence
- Group encounter generation in dungeon nodes
- Victory condition: all enemies defeated

### 6.4 Task C4: Library Full Implementation ✅ (2026-01-29)

- Card encyclopedia: all 3 classes (Swordsman/Mage/Summoner), class filter
- Card derivation tree: `CardDerivationTree.tsx` with chain visualization
- Enemy encyclopedia: depth 1-5 enemies (40 total), depth filter, boss filter
- Enemy data files: `enemyDepth2.ts` through `enemyDepth5.ts` created
- `EnemyEncyclopediaData.ts` consolidated (constants re-exports from domain)

### 6.5 Task C5: Dungeon Events ✅

- 6 random events in `nodeEventLogic.ts`
- Rest/treasure nodes with modal UI
- Event choice UI integrated

### 6.6 Task B4: Guild Quests/Rumors ✅ (2026-01-29)

- QuestsTab: daily/weekly quests, accept/track/claim flow, progress bars
- RumorsTab: rumor purchase with gold, active rumor tracking, typed effects
- Data: `QuestData.ts` (template-based generation), `RumorData.ts` (5 rumors)
- CSS styles added to `Guild.css`

---

## 7. Architecture Guidelines

### 7.1 SOLID Principles

**Single Responsibility:**
- Each context manages one concern
- Logic files separate from components

**Open/Closed:**
- Use composition over inheritance
- Extend via new hooks, not modification

**Liskov Substitution:**
- Do NOT create unified `Character` interface
- Player and Enemy have different behaviors

**Interface Segregation:**
- Small, focused interfaces
- `BattleStats` for shared battle properties only

**Dependency Inversion:**
- Contexts depend on abstractions (types)
- Components depend on context hooks

### 7.2 State Management

```
Permanent State (survives battles):
├── PlayerContext: persistent data, resources, progression
├── InventoryContext: items, equipment
└── ResourceContext: gold, magic stones

Battle State (reset each battle):
├── PlayerBattleContext: HP, AP, buffs
├── EnemyBattleContext: enemy state
└── BattleSessionContext: flow control

Run State (reset each exploration):
└── DungeonRunProvider: map, progress, temp items
```

### 7.3 File Organization

```
src/domain/
├── battles/           # Battle-time only
│   ├── contexts/      # Battle contexts
│   ├── logic/         # Pure battle logic
│   ├── execution/     # Phase execution
│   └── managements/   # Hooks for battle management
├── camps/             # Camp facilities
│   ├── contexts/      # Camp contexts (Player, Resource)
│   └── facilities/    # Facility-specific code
├── dungeon/           # Dungeon exploration
│   ├── context/       # Dungeon run state
│   └── logic/         # Map generation, etc.
└── sanctuary/         # Sanctuary system
    ├── type/          # Soul, skill types
    ├── data/          # Skill tree data
    └── logic/         # Soul/node logic
```

---

## 8. Archived Design Decisions

### 8.1 Why No Character Interface

**Reasons:**
- Player and Enemy are not interchangeable (violates LSP)
- Common properties handled by `BattleStats`
- Avoids `type: 'player' | 'enemy'` branching

**Correct Structure:**
```
BattleStats (common interface)
├── PlayerBattleState extends BattleStats
└── EnemyBattleState extends BattleStats
```

### 8.2 Deck System Decisions

- Initial deck: 15 cards (not 20 or 40)
- `initialDeckConfig.ts` is source of truth
- `CharacterClassData.ts` references, not duplicates

### 8.3 Soul System V3.0 Change

**Key Change:** Souls are saved 100% on BOTH survival AND death

**Rationale:**
- Death penalty is item loss + life loss (already significant)
- Losing souls too would be too punishing
- Progress should continue even through failure
- Game Over is the true reset point

---

## Revision History

| Date | Changes |
|------|---------|
| 2026-01-26 | Initial unified plan created |
| 2026-01-26 | Consolidated from: architecture-roadmap.md, phase1-buff-debuff-ownership.md, deck-system-integration-v2.md |
| 2026-01-26 | Added Phase A detailed implementation plan |
| 2026-01-26 | Phase A audit completed - All core systems already implemented |
| 2026-01-29 | **Full revision:** Compressed Phase A archive, added BUG-1~4 & MISS-1~6, added FIX-1~3 critical bugs, added B6-B11 new tasks, updated progress percentages, removed week-based schedules |
| 2026-01-29 | **FIX-1 reclassification:** `useMagicStones()` analyzed as unused dead code (zero call sites), downgraded CRITICAL→LOW. Actual stone consumption works correctly via `ExchangeTab.tsx` |
| 2026-01-29 | **Phase C progress:** Build fix (86 TS errors → 0), C2 Legacy Deletion complete, C4 Library complete (3 classes + depth 1-5 enemies), B4 Guild Quests/Rumors complete. C3 Multi-Enemy Battle remains pending. |
