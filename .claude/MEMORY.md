# Project Memory - BaseCamp Implementation

## Current Status

**Last Updated:** 2026-01-20

**Development Server:** http://localhost:5173/

---

## Completed Work

### Phase 1: Foundation (Type Definitions & Context API)

Created type definitions (`ItemTypes.ts`, `GuildTypes.ts`, `StorageTypes.ts`, `CampTypes.ts`) and Context API (`GameStateContext`, `PlayerContext`, `InventoryContext`). App.tsx integrated with all providers and screen routing.

### Phase 2: Guild Facility

Implemented promotion exam system with 12 exams (3 classes x 4 ranks). Created `GuildEnemyData.ts` (6 enemies), `PromotionData.ts`, and full UI (`Guild.tsx`, `Exam.tsx`, `GuildBattleScreen.tsx`). Battle callbacks and screen routing functional.

### Phase 3: Storage UI

Implemented compact high-density layout with Items/Equipment tabs. Storage and Inventory grids side-by-side with transfer buttons. Fixed `equipItem` state race condition by consolidating updates.

### Phase 4: Shop Facility

Implemented Merchant's Exchange with 3 tabs: Buy, Sell, Exchange. Created `ShopTypes.ts`, `ShopData.ts`, `shopLogic.ts`. Features: consumables (potions), teleport stones, equipment packs (gacha with rarity rolling), item selling, and magic stone to gold exchange. Gold/merchant themed UI.

### Equipment Inventory Feature (2026-01-14)

Added Equipment Inventory system for dungeon exploration:

- **New Type:** `EquipmentInventoryState` in `StorageTypes.ts` (max 3 equipment slots)
- **Extended Player:** Added `equipmentInventory` to `ExtendedPlayer` type
- **Death Penalty:** Equipment Inventory items are lost on death (same as regular inventory)
- **Storage.tsx Equipment Tab:** Redesigned with:
  - Left: Equipment List (equipment items from Storage only)
  - Right: Equipment Slots (6 slots) + Equipment Inventory (3 slots) + Action buttons
- **Items Tab:** Now filters out equipment items from inventory display
- **New Operations:** `storage_to_equipment_inventory`, `equipment_inventory_to_storage`, `equipment_inventory_to_equipment`, `equipment_to_equipment_inventory`
- **Design Document:** Updated `storage_design.md` with Equipment Inventory concept

### Phase 5: Blacksmith Facility (2026-01-16)

Implemented Blacksmith's Forge with 3 tabs: Upgrade, Repair, Dismantle.

**Files Created:**

- **Types:** `BlacksmithTypes.ts` - Tab types, cost interfaces, quality configs
- **Data:** `BlacksmithData.ts` - Upgrade costs, quality options, repair/dismantle configs
- **Logic:** `blacksmithLogic.ts` - Level/quality upgrade, repair, dismantle functions
- **UI:** `Blacksmith.tsx`, `UpgradeTab.tsx`, `RepairTab.tsx`, `DismantleTab.tsx`, `BlacksmithItemCard.tsx`, `Blacksmith.css`

**Features:**

- **Level Upgrade (Lv0-3):** Stats +10-30%, AP +20-60%, costs scale by rarity
- **Quality Upgrade (3 options):**
  - Normal (1.0x cost, 10-40% success)
  - Quality Focused (1.5x cost, 15-80% success)
  - Max Quality (2.0x cost, 25-100% success)
- **Quality progression:** poor → normal → good → master
- **Repair:** Cost = (maxAP - currentAP) × 0.5 × rarityMultiplier, Repair All button
- **Dismantle:** Gold return (10-25% of sell price), bonus magic stone chance for Rare+
- **Warning system:** Confirmation modal for valuable items (Rare+, Lv1+, Good+)
- **Orange/fire themed UI** matching forge aesthetic

### Phase 6: Sanctuary Facility (2026-01-17)

Implemented Sanctuary with radial skill tree and soul system.

**Files Created:**

- **Types:** `SanctuaryTypes.ts` - Node, effect, progress types and constants
- **Data:** `SanctuaryData.ts` - 17 skill nodes across 3 tiers (HP/Gold/Utility/Class/Exploration branches)
- **Logic:** `sanctuaryLogic.ts` - Node status, unlock, effect calculation
- **Logic:** `soulSystem.ts` - Soul acquisition, survival/death handling
- **UI:** `Sanctuary.tsx`, `SkillTree.tsx`, `SkillNode.tsx`, `NodeDetailPanel.tsx`, `Sanctuary.css`

**Implemented Features:**

- Radial skill tree UI with tier rings and prerequisite connections
- 17 nodes: HP boost (Lv1-3), Gold multiplier, Soul multiplier, Appraisal abilities, Class-specific bonuses, Exploration limit increase
- Hold-to-unlock interaction (1.5s)
- Soul display (totalSouls, currentRunSouls)
- Progress statistics
- Purple/mystical themed styling
- App.tsx routing and PlayerContext integration

**Not Yet Implemented:**

- playerClass hardcoded to "swordsman" (needs actual player class data)
- Sanctuary effects not applied to game mechanics (battle HP, gold acquisition, etc.)
- Soul acquisition not connected to battle outcomes
- Effect application in dungeon/battle systems

### Phase 8: Dungeon Gate (2026-01-20)

Implemented dungeon gate with node map and battle integration.

**Files Created:**

- **Types:** `domain/dungeon/types/DungeonTypes.ts` - Node, floor, run types and configs
- **Logic:** `domain/dungeon/logic/dungeonLogic.ts` - Map generation, node progression
- **Context:** `ui/dungeonUI/DungeonRunContext.tsx` - Dungeon run state management
- **UI:** `DungeonGate.tsx`, `DungeonGate.css` - Depth selection screen
- **UI:** `NodeMap.tsx`, `NodeMap.css` - Node map display
- **UI:** `MapNode.tsx` - Individual node component

**Files Modified:**

- `CampTypes.ts` - Added `"dungeon_map"` to GameScreen
- `App.tsx` - Added routing, lifted DungeonRunProvider to app level
- `BattleScreen.tsx` - Added `onWin`/`onLose` props for dungeon callbacks

**Implemented Features:**

- Depth selection (5 depths with themed colors)
- 7-row node map with connections
- Node types: battle, elite, boss, event, rest, treasure
- Battle integration: victory returns to NodeMap, defeat returns to BaseCamp
- Progress tracking and encounter counting

**Flow:** BaseCamp → DungeonGate → NodeMap → BattleScreen → NodeMap (loop) → BaseCamp

---

## Remaining Phases

| Phase | Facility    | Priority   | Key Components                                 |
| ----- | ----------- | ---------- | ---------------------------------------------- |
| 7     | Library     | Low-Medium | Deck builder, encyclopedia, records, save/load |
| 9     | Integration | Critical   | Full testing, death mechanics, economy balance |

---

## Next Actions

**Recommended:** Phase 9 (Integration) - Connect all systems and test full game loop

**Phase 8 follow-up tasks:**

- Implement event/rest/treasure node functionality (currently complete immediately)
- Add floor completion reward screen
- Connect soul acquisition to battle outcomes

**Sanctuary follow-up tasks:**

- Connect playerClass to actual player data (currently hardcoded to "swordsman")
- Apply Sanctuary effects to battle/dungeon systems (HP boost, gold multiplier, etc.)
- Integrate soul acquisition with battle outcomes

**Pending items:**

- Guild: RumorsTab and QuestsTab are placeholders
- Shop: Daily sales system (deferred), pack opening animation (deferred)
- Implement the item and equipment data as soon as the design document is completed.

---

## Critical Lessons Learned

**Language Consistency (2026-01-11):**
Player grades use Japanese ("見習い剣士") but `PromotionData` initially used English. This caused `getNextExam()` to fail. Always verify language consistency across PlayerData, game data files, and type definitions.

**Context Provider Scope and Screen Navigation (2026-01-20):**
When state needs to persist across screen transitions (e.g., dungeon run state during battles), the Context Provider must be placed at a high enough level in the component tree. Issues encountered:

1. **Problem:** `DungeonRunProvider` wrapped only `NodeMap` - when navigating to battle screen, NodeMap unmounted and dungeon run state was lost
2. **Problem:** `BattleScreen` didn't receive `battleConfig.onWin/onLose` callbacks - VictoryScreen called `handleContinueToNextBattle()` instead of returning to dungeon map
3. **Solution:**
   - Lift `DungeonRunProvider` to wrap the entire `AppContent` in `App.tsx`
   - Pass `onWin`/`onLose` props from `battleConfig` to `BattleScreen`
   - Use these callbacks in `VictoryScreen` and `DefeatScreen`

**Rule:** When implementing features that span multiple screens (dungeon exploration, multi-stage battles), always consider:
- Where the Context Provider should be placed to maintain state
- Whether all necessary callbacks are passed through the component chain
- Test the full navigation flow (A → B → A) before marking complete

**React Hooks Rules of Hooks (2026-01-20):**
All React Hooks (useState, useEffect, useCallback, useMemo, custom hooks) MUST be called:
- At the top level of the component
- Before any conditional returns (if/return)
- In the same order every render

Common mistake pattern:
```tsx
// ❌ BAD - hook after early return
if (condition) return <Early />;
useEffect(() => {...});  // This breaks!

// ✅ GOOD - hook before early return
useEffect(() => {...});  // All hooks first
if (condition) return <Early />;
```

Additional note: Use `useRef` instead of `useState` for flags that track whether an effect has run, to avoid ESLint `set-state-in-effect` warnings.

