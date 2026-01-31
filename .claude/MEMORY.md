# Project Memory

## Operational Rules

- Write all design documents and code in English
- Please output only user chat in Japanese.

---

## Quick Reference

- **Current Phase:** Phase C (Extended Features) — NEARLY COMPLETE
- **Dev Server:** http://localhost:5173/
- **Last Updated:** 2026-01-31
- **Type System:** `src/types/` with barrel export (`@/types/*`)

---

## Implementation Status

| Category | Status | Notes |
|----------|--------|-------|
| Battle System | 95% | Core, elemental chain, escape, element system, multi-enemy all complete. AoE cards pending |
| Camp Facilities | 95% | Shop, Guild (Exam/Quests/Rumors), Library (Card+Enemy encyclopedias), Blacksmith, Storage all complete |
| Dungeon System | 90% | Map generation, battle/event/rest/treasure nodes, 5-floor progression, depth 1-5 enemies |
| Progression System | 95% | Lives + Souls + Sanctuary + equipment stat bonuses + card derivation + mastery all complete |
| Save System | Implemented | `src/domain/save/logic/saveManager.ts` |

---

## Completed Phases

### Phase A: Core Loop — COMPLETED
Lives system, Soul remnants, Sanctuary skill tree, Return system, Dungeon map UI

### Phase B: Game Experience Enhancement — COMPLETED
- B1: Context Separation (PlayerBattleContext, EnemyBattleContext, BattleSessionContext)
- B2: Summoner Class (40 cards)
- B3: Shop Full Implementation (daily rotation, equipment)
- B4: Guild Full Implementation (Exam, Quests, Rumors)
- B5: Card Derivation System
- B6: Mage Elemental Chain
- B7: Dungeon Floor Progression (5 floors per depth)
- B8: Escape System
- B9: Exam Reward Application
- B10: Equipment Stat Bonuses
- B11: Magic Stone Constants
- FIX-1~3: Dead code cleanup, dungeon node UI, huge stone calc

### Phase C: Extended Features — NEARLY COMPLETE
- C1: Exploration Prep Screen (DungeonGate.tsx + preparations/) — COMPLETE
- C2: Legacy Interface Deletion — COMPLETE
- C3: Multi-Enemy Battle — COMPLETE (target selection, per-enemy phase, auto-retarget)
- C4: Library Full Implementation — COMPLETE
- C5: Dungeon Events — COMPLETE
- Element Refactor: slash/impact → physics unified — COMPLETE
- Card data migration: `src/domain/cards/data/` → `src/constants/data/cards/` — COMPLETE
- Teleport stone item — COMPLETE

---

## Remaining Work

### Features Not Yet Implemented

| Feature | Priority | Notes |
|---------|----------|-------|
| AoE card support | LOW | Cards that damage all enemies — no logic exists yet |
| EnemyFrame SVG icons | LOW | Currently uses emoji placeholders (TODO in code) |
| FacilityHeader unused `variant` prop | LOW | Prop accepted but not used |
| Build errors (pre-existing) | MEDIUM | `useDeckManage.ts:12` wrong import path; `NodeMap.tsx:252` missing `.remaining` on LivesSystem |

### Class Ability Hooks

All class hooks implemented and wired via `useClassAbility.ts`:
- Swordsman: `useSwordEnergy()` (inside useClassAbility.ts)
- Mage: `useElementalChain.ts`
- Summoner: `useSummonSystem.ts`

---

## Asset Structure

| Directory | Contents |
|-----------|----------|
| `public/assets/images/facility-backgrounds/` | Camp facility backgrounds |
| `public/assets/images/depth-backgrounds/` | Dungeon depth backgrounds |
| `public/assets/images/elements/` | Element icons |
| `public/assets/images/enemies/` | Enemy sprites |
| `public/assets/images/icons/` | UI icons |

---

## Critical Lessons Learned

| Issue | Rule |
|-------|------|
| CSS Class Collision | Scope with parent: `.battle-screen .card {}` |
| Context Provider Scope | Persist state across screens -> provider high in tree |
| React Hooks | Call at top level, before conditional returns |
| React 19 Refs | No `ref.current` during render -> use `useState` |
| Language | UI: Japanese / Code: English |

**Details:** See `.claude/LESSONS_LEARNED.md`

---

## See Also

- **Game Design:** `.claude/docs/`
- **Battle Logic:** `.claude/docs/battle_document/battle_logic.md`
- **Implementation Plan:** `.claude/todos/MASTER_IMPLEMENTATION_PLAN.md`
