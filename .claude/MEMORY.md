# Project Memory

## Operational Rules

- Write all design documents and code in English
- When updating MEMORY.md, always update `README.md` (project root) in the same operation
- Key project files: `README.md` (project root), `.claude/MEMORY.md`, `.claude/LESSONS_LEARNED.md`
- When searching for project-root files, use `ls` or specify `path` parameter in Glob to avoid `node_modules` noise

---

## Quick Reference

- **Current Phase:** Phase D (Shop & Encyclopedia Overhaul) — COMPLETE
- **Dev Server:** http://localhost:5173/
- **Last Updated:** 2026-02-03
- **Type System:** `src/types/` with barrel export (`@/types/*`)

---

## Implementation Status

| Category | Status | Notes |
|----------|--------|-------|
| Battle System | 98% | Core complete. AoE cards pending |
| Camp Facilities | 100% | Shop, Guild, Library, Blacksmith, Sanctuary, Storage, **Inn** (rest/dining/rumors) |
| Dungeon System | 90% | Map generation, 5-floor progression, depth 1-5 enemies |
| Progression System | 98% | Lives + Souls + Sanctuary + equipment + card derivation + mastery + custom deck |
| State Management | Fixed | 57 vulnerability fixes across Sessions 1-11 |
| Save System | Implemented | `src/domain/save/logic/saveManager.ts` |
| Character Images | 90% | Player images displayed. 50 enemies have `imagePath` set (PNGs not yet created) |

---

## Completed Phases (Summary)

- **Phase A:** Core Loop — Lives, Souls, Sanctuary, Dungeon map UI
- **Phase B:** Game Experience — Summoner class, Shop, Guild, Card Derivation, Elemental Chain, Escape, Equipment stats
- **Phase C:** Extended Features — DungeonGate prep screen, Multi-enemy battle, Library, Dungeon events, Element refactor, Data migrations, Multi-hit cards, Equipment durability, Custom deck, Player images
- **Phase D:** Shop & Encyclopedia Overhaul — Shop inventory/stock system (`ShopStockState`, lazy init, battle-count restock, merchant ticket), Card encyclopedia filters (tag/cost/unknown toggle, unlock stats)
- **Phase E:** Inn Facility — Rest/dining options with exploration bonuses, rumor bubble system, `InnBuffsState` in `PlayerProgression`
- **Vulnerability Remediation (Sessions 1-9):** 52 fixes total (Critical 5, High 22, cleanup 17). Details in `.claude/code_overview/`
- **Session 8:** Dead code & duplicate sweep (11 fixes — duplicate functions, legacy aliases, identical constants consolidated)
- **Session 9:** Naming & file organization (6 fixes — DungeonRunContext moved to `src/contexts/`, file renames: deptManager→depthManager, tittle→title, swordmanCards→swordsmanCards, test data removed, default values zeroed)
- **Session 10:** Hardcoded → Data-Driven Conversion (4 fixes — V-DMG-07: buff calculation uses category maps, V-DMG-08: true damage bypasses defense, V-CARD-08: explicit stance tag handling, V-ENM-03: Depth 1 enemies element types) — COMPLETE
- **Session 11:** Large File Refactoring (3 items — V-CS05: PlayerContext already refactored with 4 hooks, V-ORCH-05: helper functions extracted to phaseQueueHelpers.ts, Q2: variant prop in use) — COMPLETE

---

## Remaining Work

| Feature | Priority | Notes |
|---------|----------|-------|
| AoE card support | LOW | Cards that damage all enemies — no logic exists yet |
| EnemyFrame SVG icons | LOW | Currently uses emoji placeholders |
| Enemy image assets | MEDIUM | 50 enemies have `imagePath` but no actual PNGs |
| Summoner character image | LOW | Summoner.png is provisional |

---

## Recent Architecture Changes

### State & Context
- **Resource single source of truth:** ResourceContext owns gold/magicStones. PlayerContext delegates. All facilities use `useResources()`.
- **Inventory functional updaters:** All mutations use `setPlayerData(prev => ...)` to prevent stale closures.
- **Battle context simplified:** Removed `BattleProviderStack`, `PlayerBattleContext`, `EnemyBattleContext`, `BattleSessionContext`. `useBattleOrchestrator` returns all battle state directly.

### Battle Logic (Sessions 3-6, 10)
- `canAct()` checks freeze/stagger via `DISABLING_DEBUFFS` array
- `removeNDebuffs` helper for cleanse/purge operations
- Guard: single source of truth for shield gain
- Elemental resonance effects (burn/freeze/stun) trigger after damage calculation
- Fallback attack: 50% of avg `baseDamage` from enemy `aiPatterns` (min 3)
- **Buff calculation data-driven:** Category maps in `buffData.ts` (START_PHASE_HEALING_BUFFS, END_PHASE_DAMAGE_BUFFS, etc.)
- **True damage bypasses defense:** `sacrifice` element cards skip vulnerability/reduction calculations

### Shop & Economy (Sessions 7 + Phase D)
- `ShopStockState` in `PlayerContext.progression` (lazy init, undefined until first visit)
- Permanent items (7) + daily specials (3 slots, weighted random) + epic roll (depth-scaled 5-20%)
- Battle count restock (7-10 battles) with `hasNewStock` flag
- `shopRotationDay` persisted in `PlayerProgression`
- Blacksmith fix: `spendBaseCampMagicStones()` deducts both gold and stones

### Card System (Session 5)
- Category/rarity fields removed from cards, ElementType expanded

### UI Components
- `BackToCampButton`, `FacilityTabNav`, `FacilityHeader` — shared camp navigation
- Card encyclopedia: tag/cost/unknown filters, colored tag badges, unlock ratio stats

### Inn Facility (Phase E)
- `InnBuffsState` in `PlayerProgression` tracks rest/meal bonuses
- Rest options: Simple (free), Standard (+20 HP), Deluxe (+40 HP, +1 energy)
- Meal options: 5 types providing buffs (attack/defense/haste/regen/gold) for N battles
- `RumorBubble` component shows random tips/rumors for atmosphere
- Integration point: bonuses applied at dungeon start (not yet implemented)

---

## Code Analysis

Static analysis of all contexts and domain logic completed in Session 7.
- **Docs:** 14 files in `.claude/code_overview/` (overall summary, AI reference, per-domain analysis)
- **Vulnerabilities:** 77 identified (Critical 5, High 22, Medium 30, Low 20) — 57 fixed in Sessions 1-11
- **Details:** `.claude/code_overview/README.md`

---

## Critical Lessons Learned

| Issue | Rule |
|-------|------|
| CSS Class Collision | Scope with parent: `.battle-screen .card {}` |
| Context Provider Scope | Persist state across screens → provider high in tree |
| React Hooks | Call at top level, before conditional returns |
| React 19 Refs | No `ref.current` during render → use `useState` |
| Language | UI: Japanese / Code: English |

**Details:** See `.claude/LESSONS_LEARNED.md`

---

## See Also

- **Game Design:** `.claude/docs/`
- **Battle Logic:** `.claude/docs/battle_document/battle_logic.md`
- **Implementation Plan:** `.claude/todos/MASTER_IMPLEMENTATION_PLAN.md`
- **Code Analysis:** `.claude/code_overview/`
