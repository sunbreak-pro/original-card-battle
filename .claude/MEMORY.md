# Project Memory - BaseCamp Implementation

## Current Status

**Last Updated:** 2026-01-11

### ✅ COMPLETED: Storage UI - Compact High-Density Layout

**Bug Fixed:**

- **Issue:** When equipping from inventory, item was not removed (duplicate items appeared)
- **Cause:** `equipItem` function in InventoryContext used multiple separate `updatePlayer` calls, causing React state race conditions
- **Fix:** Consolidated all state updates into a single `updatePlayer` call in `equipItem` function

**Layout Changed (High-Density Redesign):**

- **Items Tab:** Storage and Inventory grids displayed side-by-side
  - Transfer buttons (→ / ←) between grids for easy item movement
  - Compact 4x6 grid (24 slots each) with name-only display
  - Item details panel on right side
- **Equipment Tab:** 3x2 grid of equipment slots only
  - Click to view equipped item details
  - Unequip button in detail panel
- Compact item cards (name only)
- Reduced all padding/margins for higher information density

**Unused Functions Removed:**

- Deleted `getInventorySpace` and `getStorageSpace` from InventoryContext

**Files Modified:**

- `src/domain/camps/contexts/InventoryContext.tsx` - Fixed equipItem, removed unused functions
- `src/ui/campsUI/Storage/Storage.tsx` - Complete layout redesign (Items/Equipment tabs)
- `src/ui/campsUI/Storage/ItemCard.tsx` - Added `compact` prop for name-only mode
- `src/ui/campsUI/Storage/Storage.css` - Full CSS rewrite for compact layout

**Development Server Status:** ✅ Running on http://localhost:5175/

---

### ✅ COMPLETED: Phase 2 - Guild Facility Implementation (FULLY COMPLETE)

**Data Files Created:**

- `src/domain/camps/data/GuildEnemyData.ts` - 6 exam-specific enemies (4 swordsman, 1 mage, 1 summoner)
- `src/domain/camps/data/PromotionData.ts` - 12 promotion exams (3 classes × 4 ranks each)

**UI Components Created:**

- `src/ui/campsUI/Guild/Guild.tsx` - Main Guild component with tab navigation
- `src/ui/campsUI/Guild/PromotionTab.tsx` - Full promotion exam UI with requirements check, rewards display, start button
- `src/ui/campsUI/Guild/RumorsTab.tsx` - Placeholder (Phase 3)
- `src/ui/campsUI/Guild/QuestsTab.tsx` - Placeholder (Phase 3)
- `src/ui/campsUI/Guild/Guild.css` - Complete dark fantasy tavern styling

**Integration:**

- App.tsx updated to import and render Guild component
- Screen routing: BaseCamp → Guild → Tab switching → Back to Camp
- All navigation tested and working

**Exam Battle System:**

- `src/ui/campsUI/Guild/GuildBattleScreen.tsx` - Dedicated exam battle screen (arena theme)
- `src/ui/campsUI/Guild/Exam.tsx` - Exam tab (renamed from PromotionTab.tsx)
- App.tsx routing: Exam battles use GuildBattleScreen, regular battles use BattleScreen
- Battle callbacks (onWin/onLose) properly wired and functional
- Success/failure notifications implemented
- Returns to Guild after exam (not BaseCamp)

**Testing Setup:**

- PlayerData.tsx updated with 5 starter cards for testing
- Exam button now enabled and functional
- Full battle flow tested and working

**Development Server Status:** ✅ Running without errors on http://localhost:5174/

**Critical Bug Fixed:**

- **Issue:** PromotionData used English grade names ("Apprentice Swordsman") but player.classGrade uses Japanese ("見習い剣士")
- **Result:** `getNextExam()` returned null, showing "max rank reached" instead of exam details
- **Fix:** Changed all grade names in PromotionData to Japanese to match player data
- **Lesson:** ALWAYS verify language consistency between data sources and player state

### ✅ COMPLETED: Phase 1 - Foundation (Type Definitions & Context API)

**Type Definitions Created:**

- `src/domain/camps/types/ItemTypes.ts` - Item, EquipmentSlot, EquipmentEffect, MagicStones
- `src/domain/camps/types/GuildTypes.ts` - PromotionExam, Rumor, Quest, GuildState
- `src/domain/camps/types/StorageTypes.ts` - StorageState, InventoryState, EquipmentSlots, MoveResult
- `src/domain/camps/types/CampTypes.ts` - GameScreen, BattleMode, Depth, BattleConfig, ExplorationLimit, SanctuaryProgress

**Context API Implemented:**

- `src/domain/camps/contexts/GameStateContext.tsx` - Screen navigation and battle management
- `src/domain/camps/contexts/PlayerContext.tsx` - Extended player state with storage, inventory, resources, progression
- `src/domain/camps/contexts/InventoryContext.tsx` - Item operations (add, remove, equip, move)

**App.tsx Updated:**

- Integrated all Context Providers (GameStateProvider, PlayerProvider, InventoryProvider)
- Implemented screen routing for all facility types
- Added placeholder screens for facilities (guild, shop, blacksmith, sanctuary, library, storage, dungeon)

**Development Server Status:** ✅ Running without errors (localhost:5173)

BaseCamp UI skeleton exists at `src/ui/campsUI/BaseCamp.tsx` with 6 facility placeholders. Context API is ready for facility implementation.

## Implementation Plan for BaseCamp System

### Phase 1: Foundation (Type Definitions & Context API)

**Priority:** CRITICAL - Must be done first as foundation for all other features

#### Task 1.1: Create Type Definitions

- [ ] Create `src/domain/camps/types/ItemTypes.ts`
  - Item, ItemType, EquipmentSlot, EquipmentEffect interfaces
- [ ] Create `src/domain/camps/types/GuildTypes.ts`
  - PromotionExam, Rumor, Quest, QuestObjective, QuestReward, GuildState interfaces
- [ ] Create `src/domain/camps/types/StorageTypes.ts`
  - StorageState, InventoryState, EquipmentSlots, MoveDirection, MoveResult interfaces

#### Task 1.2: Context API Implementation

- [ ] Create `src/domain/camps/contexts/GameStateContext.tsx`
  - GameScreen, BattleMode, Depth, BattleConfig types
  - navigateTo, startBattle, returnToCamp functions
- [ ] Create `src/domain/camps/contexts/PlayerContext.tsx`
  - Integrate with existing Character/data/PlayerData.tsx
  - Add: storage, inventory, equipment fields
  - Add: explorationGold, baseCampGold separation
  - Add: explorationMagicStones, baseCampMagicStones separation
  - Add: updatePlayer, addGold, useGold, updateHp, updateAp helpers
- [ ] Create `src/domain/camps/contexts/InventoryContext.tsx`
  - items, equipped state management
  - addItem, removeItem, equipItem, unequipItem functions
  - getMagicStones, useMagicStones functions
- [ ] Update `src/App.tsx`
  - Wrap with GameStateProvider, PlayerProvider, InventoryProvider
  - Add screen routing logic (camp, battle, guild, shop, etc.)

#### Task 1.3: Fix Player Type

- [ ] Update `src/domain/characters/data/PlayerData.tsx` (or similar location)
  - Add `equipped: string[]` field to Player interface
  - Add `storage`, `inventory`, `equipment` fields
  - Update Swordsman_status, Mage_status, Summoner_status initial data

### Phase 2: Guild Facility (First Facility Implementation)

**Priority:** HIGH - First complete facility to establish patterns

#### Task 2.1: Create Guild Enemy Data

- [ ] Create `src/domain/characters/data/GuildEnemyData.ts`
  - TRAINING_DUMMY, GUILD_INSTRUCTOR, VETERAN_WARRIOR, SWORD_SAINT_PHANTOM
  - MAGIC_GOLEM, etc. (exam-specific enemies)
  - Export GUILD_ENEMIES array

#### Task 2.2: Create Promotion Data

- [ ] Create `src/domain/camps/data/PromotionData.ts`
  - SWORDSMAN_EXAMS array (4 ranks)
  - MAGE_EXAMS array (4 ranks)
  - SUMMONER_EXAMS array (4 ranks)
  - getExamsForClass, getNextExam helper functions

#### Task 2.3: Guild UI Components

- [ ] Create `src/ui/campsUI/Guild/Guild.tsx`
  - Tab navigation (Rumors, Quests, Promotion)
  - Back to camp button
- [ ] Create `src/ui/campsUI/Guild/PromotionTab.tsx`
  - Display current/next grade
  - Check requirements (card count, gold)
  - Start exam button
  - Handle exam win/loss callbacks
- [ ] Create `src/ui/campsUI/Guild/RumorsTab.tsx` (placeholder)
- [ ] Create `src/ui/campsUI/Guild/QuestsTab.tsx` (placeholder)
- [ ] Create `src/ui/campsUI/Guild/Guild.css`
  - Dark fantasy tavern theme
  - Tab styling, requirement checks, warnings

#### Task 2.4: Battle System Integration

- [ ] Extend `src/domain/battles/battleUI/BattleScreen.tsx` (or similar)
  - Add `battleMode` prop ("normal" | "exam" | "return_route")
  - Add `enemyIds` prop for specific enemy spawning
  - Add `onBattleEnd` callback prop
  - Branch enemy initialization based on battleMode
  - Call onBattleEnd on victory/defeat

#### Task 2.5: BaseCamp to Guild Connection

- [ ] Update `src/ui/campsUI/BaseCamp.tsx`
  - Unlock tavern facility (isUnlocked: true)
  - Use navigateTo("guild") instead of setSelectedFacility

### Phase 3: Storage Facility

**Priority:** HIGH - Critical for item management and death mechanics

#### Task 3.1: Storage Logic

- [ ] Create `src/domain/camps/logic/itemMove.ts`
  - moveItem function
  - moveStorageToInventory, moveInventoryToStorage
  - moveStorageToEquipment, moveEquipmentToStorage
  - Capacity checks, error handling

#### Task 3.2: Death Handler

- [ ] Create `src/domain/battles/logic/deathHandler.ts`
  - handlePlayerDeath function
  - Clear inventory, equipment slots
  - Reset explorationGold, explorationMagicStones, currentRunSouls
  - Keep storage, baseCampGold, baseCampMagicStones, totalSouls
  - Increment explorationLimit.current
  - Return to camp with hp:1, ap:0

#### Task 3.3: Storage UI

- [ ] Create `src/ui/campsUI/Storage/Storage.tsx`
  - Tab switching (Storage, Inventory)
  - Display items with capacity counts
  - Item selection, move buttons
  - Equipment slots display
- [ ] Create `src/ui/campsUI/Storage/ItemCard.tsx`
  - Item name, type, rarity, level display
  - Click to select
  - Color coding by rarity
- [ ] Create `src/ui/campsUI/Storage/ItemDetailPanel.tsx`
  - Show item details, effects, description
  - Move/Equip/Unequip buttons
- [ ] Create `src/ui/campsUI/Storage/Storage.css`
  - Warehouse theme (brown/orange)
  - Grid layout for items
  - Drag-drop styling (Phase 2)

### Phase 4: Shop Facility

**Priority:** MEDIUM - Essential economy feature

#### Task 4.1: Shop Data

- [ ] Create `src/domain/camps/data/ShopData.ts`
  - Equipment packs (gacha)
  - Consumable items
  - Teleport stones
  - Pricing data

#### Task 4.2: Shop Logic

- [ ] Create `src/domain/camps/logic/shopLogic.ts`
  - buyItem, sellItem functions
  - calculateSellPrice
  - gachaRoll (equipment pack)

#### Task 4.3: Shop UI

- [ ] Create `src/ui/campsUI/Shop/Shop.tsx`
  - Tab navigation (Buy, Sell, Packs)
  - Display player gold/magic stones
- [ ] Create `src/ui/campsUI/Shop/BuyTab.tsx`
- [ ] Create `src/ui/campsUI/Shop/SellTab.tsx`
- [ ] Create `src/ui/campsUI/Shop/PacksTab.tsx`
- [ ] Create `src/ui/campsUI/Shop/Shop.css`
  - Gold coin shine theme

### Phase 5: Blacksmith Facility

**Priority:** MEDIUM - Equipment upgrade system

#### Task 5.1: Blacksmith Logic

- [ ] Create `src/domain/camps/logic/blacksmithLogic.ts`
  - upgradeEquipment (Lv0-3)
  - improveQuality (Poor → Normal → Good → Master)
  - repairEquipment (restore AP)
  - dismantleEquipment (return magic stones)

#### Task 5.2: Blacksmith UI

- [ ] Create `src/ui/campsUI/Blacksmith/Blacksmith.tsx`
- [ ] Create `src/ui/campsUI/Blacksmith/UpgradeTab.tsx`
- [ ] Create `src/ui/campsUI/Blacksmith/RepairTab.tsx`
- [ ] Create `src/ui/campsUI/Blacksmith/DismantleTab.tsx`
- [ ] Create `src/ui/campsUI/Blacksmith/Blacksmith.css`
  - Fire and iron red theme

### Phase 6: Sanctuary Facility

**Priority:** MEDIUM-HIGH - Permanent progression system

#### Task 6.1: Sanctuary Data

- [ ] Create `src/domain/camps/data/SanctuaryData.ts`
  - Skill tree nodes (Tier 1, 2, 3)
  - Soul costs
  - Stat bonuses, special effects
  - Extended Exploration skills

#### Task 6.2: Sanctuary Logic

- [ ] Create `src/domain/camps/logic/sanctuaryLogic.ts`
  - unlockNode
  - calculateSoulGain (on monster kill)
  - applySurvivalMultiplier
  - applyNodeEffects to player stats

#### Task 6.3: Sanctuary UI

- [ ] Create `src/ui/campsUI/Sanctuary/Sanctuary.tsx`
- [ ] Create `src/ui/campsUI/Sanctuary/SkillTree.tsx`
- [ ] Create `src/ui/campsUI/Sanctuary/SkillNode.tsx`
- [ ] Create `src/ui/campsUI/Sanctuary/Sanctuary.css`
  - Holy white/gold theme
  - Tree visualization

### Phase 7: Library Facility

**Priority:** LOW-MEDIUM - Meta-progression and information

#### Task 7.1: Library Data

- [ ] Create `src/domain/camps/data/LibraryData.ts`
  - Encyclopedia entries
  - Achievement definitions
  - Title system

#### Task 7.2: Library UI

- [ ] Create `src/ui/campsUI/Library/Library.tsx`
  - 4 bookshelves navigation
- [ ] Create `src/ui/campsUI/Library/DeckBuilder.tsx`
- [ ] Create `src/ui/campsUI/Library/Encyclopedia.tsx`
- [ ] Create `src/ui/campsUI/Library/Records.tsx`
- [ ] Create `src/ui/campsUI/Library/SaveLoad.tsx`
- [ ] Create `src/ui/campsUI/Library/Library.css`
  - Calm blue/purple book theme

### Phase 8: Dungeon Gate

**Priority:** MEDIUM - Exploration entry point

#### Task 8.1: Dungeon Gate UI

- [ ] Create `src/ui/campsUI/DungeonGate/DungeonGate.tsx`
  - Depth selection (1-5)
  - Difficulty display
  - Exploration count warning
  - Confirmation dialog
- [ ] Create `src/ui/campsUI/DungeonGate/DungeonGate.css`
  - Depth-based color changes

### Phase 9: Integration & Testing

**Priority:** CRITICAL - Before production

#### Task 9.1: Full System Integration

- [ ] Connect all facilities to BaseCamp
- [ ] Verify screen transitions
- [ ] Test Context API data flow
- [ ] Verify save/load persistence

#### Task 9.2: Death & Survival Testing

- [ ] Test death penalty (inventory, equipment loss)
- [ ] Test storage retention
- [ ] Test exploration limit increment
- [ ] Test survival rewards (soul accumulation)

#### Task 9.3: Economy Balance

- [ ] Test gold flow
- [ ] Test magic stone economy
- [ ] Test equipment upgrade costs
- [ ] Test shop pricing

## Current Working Directory Structure

```
src/
├── domain/
│   ├── battles/
│   ├── camps/          # To be populated
│   ├── cards/
│   ├── characters/
│   ├── dungeon/
│   └── equipments/
├── ui/
│   ├── battleUI/
│   ├── campsUI/        # Currently has BaseCamp.tsx skeleton
│   ├── cardUI/
│   ├── commonUI/
│   ├── css/
│   ├── dungeonUI/
│   └── enemyUI/
├── assets/
│   ├── images/
│   ├── jasonData/
│   └── sounds/
└── utils/
    └── User/
```

## Next Immediate Actions

**Options for Next Phase:**

1. **Option A: Complete Guild 100% (Rumors & Quests tabs)**

   - Implement RumorsTab functionality
   - Implement QuestsTab functionality
   - Full Guild facility completion

2. **Option B: Move to Storage Facility (RECOMMENDED)**
   - Critical for death mechanics
   - Item management system
   - Higher priority per original plan
   - Files to create:
     - `src/domain/camps/logic/itemMove.ts`
     - `src/domain/battles/logic/deathHandler.ts`
     - `src/ui/campsUI/Storage/Storage.tsx`
     - `src/ui/campsUI/Storage/ItemCard.tsx`
     - `src/ui/campsUI/Storage/Storage.css`

**Recommended:** Proceed with Storage facility (Phase 3 from original plan)

## Important Notes

- Follow DDD-inspired structure strictly
- All game logic in `src/domain/`, UI in `src/ui/`
- Do NOT modify existing deck system in `src/domain/battles/decks/`
- Use TypeScript strict mode, avoid `any`
- Use React functional components only, no class components
- Use Tailwind CSS where possible, create separate CSS files for complex components
- Comments in plain English
- All text content (except code) can be in Japanese for easier communication

## Critical Lessons Learned

### Language Consistency Issues

**Date:** 2026-01-11
**Issue:** Player grades use Japanese ("見習い剣士") but PromotionData initially used English ("Apprentice Swordsman")
**Impact:** `getNextExam()` failed to find matching exams, showing "max rank" message instead
**Solution:** Updated PromotionData to use Japanese grade names matching player.classGrade
**Prevention:** Always verify language consistency across:

- Player data (PlayerData.tsx)
- Game data (PromotionData.ts, etc.)
- Type definitions
- UI display text

## Design Document References

- Camp Facilities: `.claude/docs/camp_document/camp_facilities_design.md`
- Guild: `.claude/docs/camp_document/guild_design.md`
- Guild Implementation: `.claude/docs/camp_document/guild_implementation_guide.md`
- Storage: `.claude/docs/camp_document/storage_design.md`
- Blacksmith: `.claude/docs/camp_document/blacksmith_design.md`
- Sanctuary: `.claude/docs/camp_document/sanctuary_design.md`
- Library: `.claude/docs/camp_document/library_design.md`
- Shop: `.claude/docs/camp_document/shop_design.md`
