# Project Memory - BaseCamp Implementation

## Current Status

**Last Updated:** 2026-01-13

**Development Server:** Running on http://localhost:5174/

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

---

## Remaining Phases

| Phase | Facility     | Priority    | Key Components                                      |
| ----- | ------------ | ----------- | --------------------------------------------------- |
| 5     | Blacksmith   | Medium      | Upgrade (Lv0-3), quality improve, repair, dismantle |
| 6     | Sanctuary    | Medium-High | Skill tree, soul system, permanent progression      |
| 7     | Library      | Low-Medium  | Deck builder, encyclopedia, records, save/load      |
| 8     | Dungeon Gate | Medium      | Depth selection (1-5), exploration entry            |
| 9     | Integration  | Critical    | Full testing, death mechanics, economy balance      |

---

## Next Actions

**Recommended:** Phase 5 (Blacksmith) or Phase 6 (Sanctuary)

**Pending items:**

- Guild: RumorsTab and QuestsTab are placeholders
- Shop: Daily sales system (deferred), pack opening animation (deferred)
- Implement the item and equipment data as soon as the design document is completed.

---

## Critical Lessons Learned

**Language Consistency (2026-01-11):**
Player grades use Japanese ("見習い剣士") but `PromotionData` initially used English. This caused `getNextExam()` to fail. Always verify language consistency across PlayerData, game data files, and type definitions.
