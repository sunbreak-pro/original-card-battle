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

- **Current Phase:** Phase C (Extended Features) — IN PROGRESS
- **Dev Server:** http://localhost:5173/
- **Last Updated:** 2026-01-29
- **Type System:** `src/types/` に集約済み（`@/types/*` でimport）

---

## Active Tasks

### Phase C: Extended Features (IN PROGRESS)

| Task | Status | Summary |
|------|--------|---------|
| C1: Exploration Prep Screen | Complete | `DungeonGate.tsx` with depth selection |
| C2: Legacy Interface Deletion | Complete | Deleted `Player`/`ExtendedPlayer`, created `BasePlayerStats`, refactored `PlayerContext.tsx` |
| C4: Library Full Implementation | Complete | All 3 class cards in encyclopedia, CardDerivationTree component, enemy depth 1-5 data, depth filter |
| B4: Guild Quests/Rumors | Complete | QuestsTab (daily/weekly, accept/track/claim), RumorsTab (purchase buffs), CSS styles |
| C5: Dungeon Events | Complete | `nodeEventLogic.ts` has 6 events, rest/treasure nodes, modal UI |
| C3: Multi-Enemy Battle | Complete | Target selection, per-enemy phase turns, auto-retarget, multi-enemy phase queue |

### Build Fix (2026-01-29) ✅

| Fix | Summary |
|-----|---------|
| Element backfill | Added `element` field to all 42 swordsman + 40 summoner cards |
| Element system | Added 6 new elements (slash, shock, guard, summon, enhance, sacrifice) to resonance system |
| Enemy AI fallback | Added `element: "slash"` to fallback card in `enemyAI.ts` |

### Phase B: Game Experience Enhancement (COMPLETED 2026-01-29)

| Task | Status | Summary |
|------|--------|---------|
| B2: Summoner Class | Complete | 40 cards, summon system hook, starter deck, class available |
| B3: Shop Full Implementation | Complete | Daily equipment rotation, individual equipment purchase |
| B5: Card Derivation System | Complete | `derivedFrom`, `unlockMasteryLevel`, `derivesInto` on Card type, `cardDerivation.ts` |
| B6: Mage Elemental Chain | Complete | `useElementalChain.ts` hook, wired to factory |
| B7: Dungeon Floor Progression | Complete | 5 floors per depth, floor/depth clear modals |
| B8: Escape System | Complete | `escapeLogic.ts`, BattleScreen escape button, boss-disabled |
| B9: Exam Reward Application | Complete | `updateBaseMaxHp`/`updateBaseMaxAp` in PlayerContext |
| B10: Equipment Stat Bonuses | Complete | `EQUIPMENT_STAT_BONUSES` data (6 slots × 5 rarities) |
| B11: Magic Stone Constants | Complete | `MAGIC_STONE_VALUES` in itemConstants.ts |
| FIX-1~3 | Complete | Dead code cleanup, dungeon node UI, huge stone calc |

### Bugs / Issues

#### LOW

| Issue | File |
|-------|------|
| FacilityHeader unused props | ESLint errors in Storage, Shop, Blacksmith |
| EnemyFrame emoji icons | `EnemyFrame.tsx:31` |
| Storage.tsx commented-out code | `Storage.tsx:53` |

### Remaining Features (Phase C)

- AoE card support (cards that hit all enemies) — future enhancement
- Encyclopedia data moved from `domain/camps/data/` → `constants/data/camps/` (canonical source)

### Asset Reorganization (2026-01-29)

Image assets restructured from flat layout to subdirectory structure:

| Old Path | New Path |
|----------|----------|
| `public/assets/images/Blacksmith-background.png` etc. | `public/assets/images/facility-backgrounds/` |
| `public/assets/images/depth_1_background.png` etc. | `public/assets/images/depth-backgrounds/` |
| (new) | `public/assets/images/elements/` — element icons |
| (new) | `public/assets/images/enemies/` — enemy sprites |

### New Files Added (Phase B-C)

| File | Purpose |
|------|---------|
| `src/domain/battles/logic/escapeLogic.ts` | Escape logic (disabled for boss battles) |
| `src/domain/battles/managements/useElementalChain.ts` | Mage elemental chain hook |
| `src/domain/battles/managements/useSummonSystem.ts` | Summoner summon system hook |
| `src/domain/cards/logic/cardDerivation.ts` | Card derivation system |
| `src/domain/characters/enemy/data/enemyDepth2.ts` ~ `enemyDepth5.ts` | Enemy data for depths 2-5 |
| `src/ui/campsHtml/Library/CardDerivationTree.tsx` | Card derivation tree UI component |
| `public/assets/pencil/cardComponent.pen` | Card UI design template |

---

## Critical Lessons Learned

| Issue                  | Rule                                                           |
| ---------------------- | -------------------------------------------------------------- |
| CSS Class Collision    | Scope with parent: `.battle-screen .card {}`                   |
| Context Provider Scope | Persist state across screens → provider high in tree           |
| React Hooks            | Call at top level, before conditional returns                   |
| React 19 Refs          | No `ref.current` during render → use `useState`                |
| Language               | UI: Japanese / Code: English ← **Particular attention**        |

**Details:** See `.claude/LESSONS_LEARNED.md`

---

## See Also

- **Game Design:** `.claude/docs/`
- **Battle Logic:** `.claude/docs/battle_document/battle_logic.md`
- **Implementation Plan:** `.claude/todos/MASTER_IMPLEMENTATION_PLAN.md`
