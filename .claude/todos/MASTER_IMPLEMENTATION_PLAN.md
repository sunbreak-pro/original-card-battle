# Master Implementation Plan

> Unified task management document consolidating all previous plans
> Last Updated: 2026-01-26

---

## Table of Contents

1. [Completed Tasks Archive](#1-completed-tasks-archive)
2. [Current Status](#2-current-status)
3. [Priority Task List](#3-priority-task-list)
4. [Phase A: Core Loop Implementation](#4-phase-a-core-loop-implementation)
5. [Phase B: Game Experience Enhancement](#5-phase-b-game-experience-enhancement)
6. [Phase C: Extended Features](#6-phase-c-extended-features)
7. [Architecture Guidelines](#7-architecture-guidelines)
8. [Archived Design Decisions](#8-archived-design-decisions)

---

## 1. Completed Tasks Archive

### 1.1 Buff/Debuff Ownership System (2026-01-26) ✅

**Status:** COMPLETED

**Implementation Summary:**
- Added `BuffOwner` type (`'player' | 'enemy' | 'environment'`) to `baffType.ts`
- Added `appliedBy` field to `BuffDebuffState`
- New `decreaseBuffDebuffDurationForPhase(map, currentActor)` in `buffLogic.ts`
- Fixed critical bug: Enemy debuffs now decrease at enemy phase

**Files Modified:**
- `domain/battles/type/baffType.ts`
- `domain/battles/logic/buffLogic.ts`
- `domain/battles/execution/playerPhaseExecution.ts`
- `domain/battles/execution/enemyPhaseExecution.ts`
- `domain/battles/managements/useCardExecution.ts`

### 1.2 Deck System Integration (2026-01-26) ✅

**Status:** COMPLETED

**Implementation Summary:**
- `INITIAL_DECK_BY_CLASS` replaces hardcoded 20-card deck with 15-card class-specific decks
- `BattleScreen.tsx` uses `playerData.persistent` instead of `Swordman_Status`
- `useBattleOrchestrator.ts` uses `initialPlayerState.deckConfig`

**Files Modified:**
- `domain/battles/data/initialDeckConfig.ts`
- `domain/battles/managements/useBattleState.ts`
- `ui/battleHtml/BattleScreen.tsx`
- `domain/battles/managements/useBattleOrchestrator.ts`
- `domain/characters/player/data/CharacterClassData.ts`

### 1.3 Mage Character (2026-01-26) ✅

**Status:** COMPLETED

- Added `MAGE_CARDS` import and `createMageStarterDeck()` function
- `getCardDataByClass()` returns `MAGE_CARDS` for mage
- Updated mage entry: `isAvailable: true`, `uniqueMechanic: "Elemental Resonance"`
- 15-card starter deck with fire/ice/lightning/light elements

### 1.4 Character Select Unique Card Display (2026-01-26) ✅

**Status:** COMPLETED

- `StarterDeckPreview.tsx` now filters to unique cards by `cardTypeId`
- Displays 7 unique card types instead of 15 cards with duplicates

---

## 2. Current Status

### 2.1 Context Provider Hierarchy

```
GameStateProvider → ResourceProvider → PlayerProvider → InventoryProvider → DungeonRunProvider
```

### 2.2 Implementation Progress

| Category | Status | Notes |
|----------|--------|-------|
| Battle System | 80% | Core complete, needs lives/soul integration |
| Camp Facilities | 40% | Basic UI, missing full functionality |
| Dungeon System | 30% | Basic flow, missing map/events |
| Progression System | 10% | Lives/Souls not implemented |

### 2.3 Known Issues

| Issue | Priority | Location |
|-------|----------|----------|
| FacilityHeader unused props | Low | ESLint errors in Storage, Shop, Blacksmith |
| Summoner class unavailable | Medium | Cards not implemented |
| Sanctuary not connected | High | No battle/dungeon integration |

---

## 3. Priority Task List

### Priority: HIGH (Core Loop - Required for MVP) ✅ COMPLETED

| ID | Task | Status | Implementation |
|----|------|--------|----------------|
| A1 | Lives System | ✅ Complete | `playerTypes.ts`, `PlayerContext.tsx`, `DefeatScreen.tsx` |
| A2 | Soul Remnants System | ✅ Complete | `soulSystem.ts`, `deathHandler.ts` (V3.0: 100% on death) |
| A3 | Sanctuary Skill Tree | ✅ Complete | 17 nodes in `SanctuaryData.ts`, full UI |
| A4 | Return System | ⚠️ Partial | `retreatFromDungeon()` works, teleport stone pending |
| A5 | Dungeon Exploration UI | ✅ Complete | `NodeMap.tsx`, `MapNode.tsx`, `dungeonLogic.ts` |

### Priority: MEDIUM (Game Experience)

| ID | Task | Dependencies | Effort |
|----|------|--------------|--------|
| B1 | Context Separation (Architecture Phase 2) | None | Large |
| B2 | Summoner Class Cards | None | Medium |
| B3 | Shop Full Implementation | None | Medium |
| B4 | Guild Full Implementation | None | Medium |
| B5 | Card Derivation System | None | Large |

### Priority: LOW (Extended Features)

| ID | Task | Dependencies | Effort |
|----|------|--------------|--------|
| C1 | Exploration Prep Screen | A5 | Large |
| C2 | Legacy Interface Deletion | B1 | Medium |
| C3 | Multi-Enemy Battle | B1, C2 | Large |
| C4 | Library Full Implementation | B5 | Medium |
| C5 | Dungeon Events/Rest/Treasure | A5 | Medium |

---

## 4. Phase A: Core Loop Implementation

### 4.1 Overview

The core game loop requires these systems working together:

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

### 4.2 Task A1: Lives System

**Purpose:** Implement the lives (残機) mechanic that adds weight to death

**Design Reference:** `game_design_master.md` Section 2.3

#### 4.2.1 Data Structure

```typescript
// src/domain/characters/player/type/playerTypes.ts

interface LivesState {
  max: number;      // Difficulty-based cap (Hard=2, Normal/Easy=3)
  current: number;  // Current lives remaining
}

// Add to PlayerData.progression
interface PlayerProgression {
  // ... existing fields
  lives: LivesState;
  difficulty: 'easy' | 'normal' | 'hard';
}
```

#### 4.2.2 Implementation Steps

**Step 1: Type Definitions**
```
File: src/domain/characters/player/type/playerTypes.ts
- Add LivesState interface
- Add lives field to PlayerProgression
- Add difficulty field to PlayerProgression
```

**Step 2: PlayerContext Integration**
```
File: src/domain/camps/contexts/PlayerContext.tsx
- Initialize lives based on difficulty
- Add decrementLife() function
- Add isGameOver() computed property
- Add resetOnGameOver() function
```

**Step 3: Death Handler**
```
File: src/domain/battles/managements/useBattleState.ts
- On defeat: call decrementLife()
- Check isGameOver() before returning to camp
- Route to GameOverScreen if lives = 0
```

**Step 4: UI Display**
```
File: src/ui/common/LivesDisplay.tsx (NEW)
- Display heart icons for lives
- Show max and current
- Animate on life loss
```

#### 4.2.3 Files to Modify

| File | Changes |
|------|---------|
| `playerTypes.ts` | Add LivesState, update PlayerProgression |
| `PlayerContext.tsx` | Add lives state, handlers |
| `useBattleState.ts` | Integrate death handling |
| `BattleScreen.tsx` | Display lives, handle game over |
| `App.tsx` | Add GameOverScreen route |

#### 4.2.4 Test Checklist

- [ ] Lives initialized correctly by difficulty
- [ ] Lives decrease on death
- [ ] Game over triggers at lives = 0
- [ ] Lives persist across battles (until death)
- [ ] Lives display updates correctly

---

### 4.3 Task A2: Soul Remnants System

**Purpose:** Implement experience/progression currency gained from battles

**Design Reference:** `sanctuary_design.md` Section 2.1

#### 4.3.1 Data Structure

```typescript
// src/domain/sanctuary/type/sanctuaryTypes.ts

interface SoulState {
  currentRunSouls: number;  // Souls from current exploration (temporary)
  totalSouls: number;       // Accumulated souls (permanent until game over)
  soulGainMultiplier: number; // From Sanctuary upgrades (default 1.0)
}

// Soul acquisition rates
const SOUL_RATES = {
  minion: 5,
  elite: 15,
  boss: 50,
} as const;
```

#### 4.3.2 Implementation Steps

**Step 1: Type Definitions**
```
File: src/domain/sanctuary/type/sanctuaryTypes.ts (NEW)
- Define SoulState interface
- Define SOUL_RATES constants
- Define SoulAcquisitionResult type
```

**Step 2: Soul Logic**
```
File: src/domain/sanctuary/logic/soulLogic.ts (NEW)
- gainSoulFromEnemy(state, enemyType): SoulState
- completeSurvival(state): SoulState  // 100% to total
- handleDeath(state): SoulState       // V3.0: 100% to total!
- handleGameOver(state): SoulState    // Reset to initial
```

**Step 3: Integration with Battle**
```
File: src/domain/battles/managements/useBattleOrchestrator.ts
- Call gainSoulFromEnemy() on enemy defeat
- Pass souls to result screen
```

**Step 4: Integration with PlayerContext**
```
File: src/domain/camps/contexts/PlayerContext.tsx
- Add soulState to player data
- Expose soul-related actions
- Handle game over reset
```

#### 4.3.3 Files to Create/Modify

| File | Type | Changes |
|------|------|---------|
| `sanctuaryTypes.ts` | NEW | Soul type definitions |
| `soulLogic.ts` | NEW | Soul calculation logic |
| `PlayerContext.tsx` | MODIFY | Add soul state |
| `useBattleOrchestrator.ts` | MODIFY | Call soul logic |
| `BattleResultScreen.tsx` | MODIFY | Display souls gained |

#### 4.3.4 Key Design Decisions

**V3.0 Soul Acquisition:**
```
On Survival: currentRunSouls → 100% to totalSouls
On Death:    currentRunSouls → 100% to totalSouls (CHANGE from V2!)
On Game Over: totalSouls → 0 (Full reset)
```

This ensures:
- Death is painful (items lost) but progress continues (souls saved)
- Game Over is the true fail state

---

### 4.4 Task A3: Sanctuary Skill Tree

**Purpose:** Permanent upgrades using accumulated souls

**Design Reference:** `sanctuary_design.md` Sections 2.2-2.3

#### 4.4.1 Data Structure

```typescript
// src/domain/sanctuary/type/sanctuaryTypes.ts

interface SkillNode {
  id: string;
  name: string;
  description: string;
  icon: string;
  cost: number;
  tier: 1 | 2 | 3;
  category: 'hp' | 'gold' | 'utility' | 'class';
  prerequisites: string[];
  effects: NodeEffect[];
  classRestriction?: CharacterClass;
}

interface NodeEffect {
  type: 'stat_boost' | 'special_ability' | 'soul_multiplier';
  target: string;
  value: number;
}

interface SanctuaryProgress {
  unlockedNodes: Set<string>;
  soulState: SoulState;
}
```

#### 4.4.2 Skill Tree Design (15 nodes for MVP)

```
Tier 1 (Cost: 20-30 souls):
├── hp_blessing_1: HP +10
├── gold_blessing_1: Initial Gold +10%
└── [class]_insight_1: Class-specific starting bonus

Tier 2 (Cost: 40-80 souls):
├── hp_blessing_2: HP +20 (requires hp_blessing_1)
├── gold_blessing_2: Gold +20% (requires gold_blessing_1)
├── expanded_bag: Inventory +5
└── soul_resonance_1: Soul gain +10%

Tier 3 (Cost: 100-150 souls):
├── hp_blessing_3: HP +30 (requires hp_blessing_2)
├── indomitable_will: Survive with 1 HP once per run
└── soul_resonance_2: Soul gain +20% (requires soul_resonance_1)
```

#### 4.4.3 Implementation Steps

**Step 1: Skill Data**
```
File: src/domain/sanctuary/data/skillTreeData.ts (NEW)
- Define all skill nodes
- Export SKILL_TREE_NODES array
```

**Step 2: Node Status Logic**
```
File: src/domain/sanctuary/logic/nodeLogic.ts (NEW)
- getNodeStatus(node, progress): 'locked' | 'available' | 'unlocked'
- unlockNode(node, progress): SanctuaryProgress
- canUnlock(node, progress): boolean
```

**Step 3: Effect Application**
```
File: src/domain/sanctuary/logic/effectLogic.ts (NEW)
- calculateTotalEffects(progress): AppliedEffects
- Apply effects to player initial state
```

**Step 4: Sanctuary UI**
```
File: src/ui/sanctuaryHtml/Sanctuary.tsx (MODIFY)
- Display skill tree visually
- Show soul count
- Long-press to unlock (1.5s)
- Node detail panel
```

#### 4.4.4 Files to Create/Modify

| File | Type | Changes |
|------|------|---------|
| `skillTreeData.ts` | NEW | Skill node definitions |
| `nodeLogic.ts` | NEW | Status determination, unlock |
| `effectLogic.ts` | NEW | Calculate applied effects |
| `Sanctuary.tsx` | MODIFY | Full UI implementation |
| `SkillTree.tsx` | NEW | Tree visualization component |
| `SkillNode.tsx` | NEW | Individual node component |

---

### 4.5 Task A4: Return System

**Purpose:** Allow players to escape dungeon and keep rewards

**Design Reference:** `return_system_design.md`

#### 4.5.1 Return Methods

```typescript
type ReturnMethod = 'teleport_stone' | 'return_route';

interface ReturnResult {
  method: ReturnMethod;
  soulMultiplier: 1.0;  // V3.0: Always 100%
  itemsKept: true;
  goldKept: true;
}
```

#### 4.5.2 Implementation Steps

**Step 1: Teleport Stone Item**
```
File: src/domain/item_equipment/data/itemData.ts
- Add TeleportStone item definition
- Consumable, single use
```

**Step 2: Return Route Logic**
```
File: src/domain/dungeon/logic/returnLogic.ts (NEW)
- canUseReturnRoute(dungeonState): boolean
- executeReturn(method, playerState): ReturnResult
```

**Step 3: Dungeon State Integration**
```
File: src/domain/dungeon/context/DungeonRunProvider.tsx
- Track return route availability
- Handle return execution
```

**Step 4: Return UI**
```
File: src/ui/dungeonHtml/ReturnConfirmModal.tsx (NEW)
- Show return options
- Confirm dialog
- Success feedback
```

---

### 4.6 Task A5: Dungeon Exploration UI

**Purpose:** Visual dungeon map with node selection

**Design Reference:** `dungeon_exploration_ui_design_v3.0.md`

#### 4.6.1 Node Types

```typescript
type DungeonNodeType = 'battle' | 'event' | 'rest' | 'treasure' | 'boss';

interface DungeonNode {
  id: string;
  type: DungeonNodeType;
  position: { x: number; y: number };
  connections: string[];  // Connected node IDs
  visited: boolean;
  current: boolean;
}
```

#### 4.6.2 Implementation Steps

**Step 1: Map Generation**
```
File: src/domain/dungeon/logic/mapGenerator.ts (NEW)
- generateDungeonMap(depth): DungeonNode[]
- Branching paths algorithm
- Boss node at end
```

**Step 2: Map State Management**
```
File: src/domain/dungeon/context/DungeonRunProvider.tsx
- Store current map
- Track visited nodes
- Handle node selection
```

**Step 3: Map UI**
```
File: src/ui/dungeonHtml/DungeonMap.tsx (NEW)
- Visual node display
- Path connections
- Current position indicator
- Node type icons
```

**Step 4: Node Interaction**
```
File: src/ui/dungeonHtml/NodeInteraction.tsx (NEW)
- Battle node → Start battle
- Event node → Random event
- Rest node → HP recovery
- Treasure node → Item/equipment
- Boss node → Boss battle
```

---

### 4.7 Phase A Implementation Order

```
Week 1:
├── A1: Lives System (2-3 days)
│   ├── Day 1: Type definitions, PlayerContext integration
│   ├── Day 2: Battle death handling, UI display
│   └── Day 3: Game over flow, testing
│
└── A2: Soul Remnants System (2-3 days)
    ├── Day 1: Type definitions, soul logic
    ├── Day 2: Battle integration, result screen
    └── Day 3: PlayerContext integration, testing

Week 2:
├── A3: Sanctuary Skill Tree (4-5 days)
│   ├── Day 1-2: Skill data, node logic
│   ├── Day 3-4: UI implementation
│   └── Day 5: Effect application, testing
│
└── A4: Return System (2 days)
    ├── Day 1: Teleport stone, return logic
    └── Day 2: UI, integration

Week 3:
└── A5: Dungeon Exploration UI (5 days)
    ├── Day 1-2: Map generation, state management
    ├── Day 3-4: Map UI, node visualization
    └── Day 5: Node interaction, testing
```

---

## 5. Phase B: Game Experience Enhancement

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

**Implementation:**
- Daily sales rotation
- Pack opening animation
- Equipment purchase/sell UI
- Magic stone → Gold exchange

### 5.4 Task B4: Guild Full Implementation

**Implementation:**
- Rumors tab (information purchase)
- Quests tab (quest acceptance/rewards)
- Promotion exam system

### 5.5 Task B5: Card Derivation System

**Implementation:**
- Add `unlockSource`, `unlockMasteryLevel` to Card type
- Mastery level triggers card unlock
- Notification UI on battle end
- Library derivation tree display

---

## 6. Phase C: Extended Features

### 6.1 Task C1: Exploration Prep Screen

Replace Dungeon Gate with comprehensive preparation screen:
- Deck loadout selection
- Equipment selection
- Item preparation (Storage ↔ Inventory)
- Auto depth progression

### 6.2 Task C2: Legacy Interface Deletion

- Delete `Player` interface
- Delete `ExtendedPlayer` interface
- Migrate all usages to `PlayerData`
- Clean up type files

### 6.3 Task C3: Multi-Enemy Battle

- Display multiple enemies
- Target selection UI
- AoE attack support
- Extended phase queue

### 6.4 Task C4: Library Full Implementation

- Encyclopedia (cards/equipment/monsters)
- Card derivation tree UI
- Achievement system
- Save/Load management

### 6.5 Task C5: Dungeon Events

- Random event encounters
- Rest node HP recovery
- Treasure node rewards
- Event choice UI

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
| 2026-01-26 | **Phase A audit completed** - All core systems already implemented |

---

## Next Steps

1. ~~Begin Task A1 (Lives System)~~ ✅ Already implemented
2. ~~Update MEMORY.md with current task status~~ ✅ Done
3. ~~Proceed through Phase A in order~~ ✅ Phase A complete
4. **Begin Phase B: Game Experience Enhancement**
   - B1: Context Separation (Architecture improvement)
   - B2: Summoner Class Cards (40 cards)
   - B3: Shop Full Implementation
   - B4: Guild Full Implementation
   - B5: Card Derivation System
