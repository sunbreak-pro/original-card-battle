# Project Memory

## Operational Rules

**When updating MEMORY.md, always update README.md**

| Timing                                  | Response                                         |
| --------------------------------------- | ------------------------------------------------ |
| When completing a task                  | Add the date and summary of changes to README.md |
| When adding or modifying major features | Record the changes in README.md                  |

---

- Write all design documents and code in English
- Please output only user chat and README.md in Japanese.

## Quick Reference

- **Current Phase:** Phase A Complete (Core Loop) - Ready for Phase B
- **Dev Server:** http://localhost:5173/
- **Last Updated:** 2026-01-28
- **Type System:** `src/types/` に集約済み（`@/types/*` でimport）

---

## Active Tasks

### Phase A: Core Loop (COMPLETED)

| Task                     | Status   | Implementation                                                |
| ------------------------ | -------- | ------------------------------------------------------------- |
| A1: Lives System         | Complete | `playerTypes.ts`, `PlayerContext.tsx`, `DefeatScreen.tsx`     |
| A2: Soul Remnants        | Complete | `soulSystem.ts`, `deathHandler.ts` (100% souls on death V3.0) |
| A3: Sanctuary Skill Tree | Complete | 17 nodes, `Sanctuary.tsx`, `sanctuaryLogic.ts`                |
| A4: Return System        | Partial  | `retreatFromDungeon()` works, teleport stone item not yet     |
| A5: Dungeon Map UI       | Complete | `NodeMap.tsx`, `MapNode.tsx`, `dungeonLogic.ts`               |

### Recently Completed

- **型定義リファクタリング完了 (2026-01-28)**
  - `src/types/` に8ファイル作成、全型定義を集約
  - `@/types/*` パスエイリアス導入（tsconfig + vite）
  - 旧 `domain/*/type(s)/` は削除（1 shimのみ残存: immutable deck用）
  - ~95ファイルのimport更新、ビルド確認済み
  - 詳細: `.claude/todos/REFACTORING_PLAN_TYPES.md`

- **Shop Refactoring & ExchangeTab Bug Fix (2026-01-28)**
  - **Bug Fix:** ExchangeTab magic stone→gold conversion now syncs with FacilityHeader
    - Added `setBaseCampMagicStones()` to ResourceContext
    - Added `updateBaseCampMagicStones()` to PlayerContext (delegates to ResourceContext)
    - ExchangeTab now updates ResourceContext directly instead of only PlayerState
  - **Data Normalization:** Removed ShopItem duplication, single source of truth
    - Created `ConsumableItemData.ts` in `domain/item_equipment/data/` with `shopPrice`
    - Replaced `ShopItem` type with `ShopListing` (references ConsumableItemData by typeId)
    - `generateItem.ts`: New `generateConsumableFromData(typeId)` replaces old shop-dependent functions
    - `nodeEventLogic.ts`: Fixed broken import + hardcoded items → `generateConsumableFromData()`
  - **Architecture:** Shop data flow is now `ShopListing → ConsumableItemData → generateItem`

- **Claude Skills Creation (2026-01-26)** - Created 9 development skills
  - `card-creator`, `enemy-creator`, `character-class-creator`
  - `battle-system`, `camp-facility`, `dungeon-system`
  - `design-research`, `ui-ux-creator`, `debugging-error-prevention`
  - All skills in English, game text examples in Japanese
  - Location: `.claude/skill/`

- **MEMORY.md Reorganization (2026-01-26)** - Reduced context size
  - Created `.claude/LESSONS_LEARNED.md` for detailed documentation
  - Converted Critical Lessons to compact table format (~30 lines → ~10 lines)

- **Phase A Core Loop Audit (2026-01-26)** - Verified all core systems implemented
  - Lives System: Type definitions, runtime state, death handling, UI display
  - Soul System: 100% transfer on death (V3.0), battle integration
  - Sanctuary: 17 skill nodes (Tier 1-3), unlock logic, effect calculation
  - Dungeon: Map generation, node progression, battle integration

- **Phase 1: Buff/Debuff Ownership System (2026-01-26)** - Fixed duration timing bug
  - Added `BuffOwner` type to `baffType.ts`
  - Fixed enemy debuff duration decrease timing

- **Mage Character (2026-01-26)** - Mage class now playable

- **Deck System Integration (2026-01-26)** - PlayerContext deck flows to battle

### Bugs / Issues

- **FacilityHeader unused props** - ESLint errors (Low priority)
- **Dungeon event/rest/treasure nodes** - Complete immediately without UI
- ~~**ExchangeTab magic stone header not updating**~~ - Fixed (2026-01-28)

### Next Phase: B (Game Experience Enhancement)

| Task                          | Priority | Status      |
| ----------------------------- | -------- | ----------- |
| B1: Context Separation        | Medium   | Not Started |
| B2: Summoner Class Cards      | Medium   | Not Started |
| B3: Shop Full Implementation  | Medium   | In Progress (ShopListing refactor done, BuyTab/ExchangeTab working) |
| B4: Guild Full Implementation | Medium   | Not Started |
| B5: Card Derivation System    | Medium   | Not Started |

### Deferred Features (Phase C)

- Multi-Enemy Battle System
- Legacy Interface Deletion
- Exploration Prep Screen
- Library Full Implementation

---

## Critical Lessons Learned

| Issue                  | Rule                                                 |
| ---------------------- | ---------------------------------------------------- | --------------------------- |
| CSS Class Collision    | Scope with parent: `.battle-screen .card {}`         |
| Context Provider Scope | Persist state across screens → provider high in tree |
| React Hooks            | Call at top level, before conditional returns        |
| React 19 Refs          | No `ref.current` during render → use `useState`      |
| Language               | UI: Japanese / Code: English                         | <- **Particular attention** |

**Details:** See `.claude/LESSONS_LEARNED.md`

---

## See Also

- **Game Design:** `.claude/docs/`
- **Battle Logic:** `.claude/docs/battle_document/battle_logic.md`
