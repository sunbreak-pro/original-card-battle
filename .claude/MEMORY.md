# Project Memory

## Operational Rules

- Write all design documents and code in English

---

## Quick Reference

- **Current Phase:** Phase C (Extended Features) — COMPLETE
- **Dev Server:** http://localhost:5173/
- **Last Updated:** 2026-02-01
- **Type System:** `src/types/` with barrel export (`@/types/*`)

---

## Implementation Status

| Category | Status | Notes |
|----------|--------|-------|
| Battle System | 98% | Core, elemental chain + resonance display, escape, element system, multi-enemy, multi-hit loop all complete. AoE cards pending |
| Camp Facilities | 95% | Shop, Guild (Exam/Quests/Rumors), Library (Card+Enemy encyclopedias), Blacksmith, Storage all complete |
| Dungeon System | 90% | Map generation, battle/event/rest/treasure nodes, 5-floor progression, depth 1-5 enemies |
| Progression System | 98% | Lives + Souls + Sanctuary + equipment stat bonuses + equipment durability + card derivation + mastery + custom deck all complete |
| Save System | Implemented | `src/domain/save/logic/saveManager.ts` |
| Character Images | 90% | Player images (Swordsman/Mage) displayed in battle. Summoner uses placeholder. All 40 enemies have imagePath set (images not yet created) |

---

## Completed Phases

### Phase A: Core Loop — COMPLETED
Lives system, Soul remnants, Sanctuary skill tree, Return system, Dungeon map UI

### Phase B: Game Experience Enhancement — COMPLETED
- B1: Context Separation (later removed — see Phase C cleanup)
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

### Phase C: Extended Features — COMPLETE
- C1: Exploration Prep Screen (DungeonGate.tsx + preparations/) — COMPLETE
- C2: Legacy Interface Deletion — COMPLETE
- C3: Multi-Enemy Battle — COMPLETE (target selection, per-enemy phase, auto-retarget)
- C4: Library Full Implementation — COMPLETE
- C5: Dungeon Events — COMPLETE
- Element Refactor: slash/impact → physics unified — COMPLETE
- Card data migration: `src/domain/cards/data/` → `src/constants/data/cards/` — COMPLETE
- Teleport stone item — COMPLETE
- FIX: Sword energy flat damage bonus in `useCardExecution.ts` — COMPLETE
- Battle context cleanup: Removed `BattleProviderStack`, `PlayerBattleContext`, `EnemyBattleContext`, `BattleSessionContext` — COMPLETE
- Camp data migration: `src/domain/camps/data/` → `src/constants/data/camps/` — COMPLETE
- Enemy data migration: `src/domain/characters/enemy/data/` → `src/constants/data/characters/enemy/` — COMPLETE
- Deleted `src/domain/characters/utils/typeConverters.ts` (unused) — COMPLETE
- Elemental chain damage modifier integrated into card execution — COMPLETE
- ElementalResonanceDisplay UI for Mage class — COMPLETE
- Multi-hit card execution loop (per-hit damage calculation with animation delays) — COMPLETE
- Equipment durability system (`equipmentStats.ts`, `applyEquipmentDurabilityDamage`) — COMPLETE
- Custom deck support from PlayerContext (`deckCards`) — COMPLETE
- Player character images in battle (PlayerFrame `<img>` tag) — COMPLETE
- Enemy `imagePath` field added to all 40 enemies + 5 bosses — COMPLETE
- Dead code removal from `soulSystem.ts` — COMPLETE

---

## Remaining Work

### Features Not Yet Implemented

| Feature | Priority | Notes |
|---------|----------|-------|
| AoE card support | LOW | Cards that damage all enemies — no logic exists yet |
| EnemyFrame SVG icons | LOW | Currently uses emoji placeholders (TODO in code) |
| Enemy image assets | MEDIUM | All 40 enemies have `imagePath` set but no actual PNG files exist yet (fallback image shown) |
| Summoner character image | LOW | Currently uses Mage.png as placeholder |
| FacilityHeader unused `variant` prop | LOW | Prop accepted but not used |
| Build error (pre-existing) | MEDIUM | `NodeMap.tsx:252` — `Property 'remaining' does not exist on type 'LivesSystem'` |

### Class Ability Hooks

All class hooks implemented and wired via `useBattleOrchestrator`:
- Swordsman: `useSwordEnergy()` (inside `useClassAbility.ts`)
- Mage: `useElementalChain()` (in `useElementalChain.ts`, integrated into orchestrator + `ElementalResonanceDisplay` UI)
- Summoner: `useSummonSystem()` (in `useSummonSystem.ts`)

---

## Recent Architecture Changes

### Battle Context Simplification
The separate battle contexts (`BattleProviderStack`, `PlayerBattleContext`, `EnemyBattleContext`, `BattleSessionContext`) have been **removed**. `useBattleOrchestrator` directly returns all battle state — no intermediate contexts needed.

### Data Location Migration
- Camp data: `src/domain/camps/data/` → `src/constants/data/camps/`
- Enemy data: `src/domain/characters/enemy/data/` → `src/constants/data/characters/enemy/`
- Card data was already migrated to `src/constants/data/cards/`

### Card Execution Multi-Hit
`useCardExecution.ts` now executes a per-hit loop for cards with `hitCount > 1`. Each hit independently calculates damage, allocation, lifesteal, and reflect, with 500ms delays between hits.

### Elemental Resonance Integration
`useElementalChain` hook is called unconditionally in `useBattleOrchestrator`. `getElementalDamageModifier` is passed into `CardExecutionSetters` and applied as a `percentMultiplier` to base damage during both preview and execution.

---

## Asset Structure

| Directory | Contents |
|-----------|----------|
| `public/assets/images/facility-backgrounds/` | Camp facility backgrounds |
| `public/assets/images/depth-backgrounds/` | Dungeon depth backgrounds |
| `public/assets/images/elements/` | Element icons |
| `public/assets/images/enemies/` | Enemy sprites (most pending creation) |
| `public/assets/images/icons/` | UI icons |
| `public/assets/images/player-character/` | Player class images (Swordman.png, Mage.png, Summoner.png placeholder) |

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

## Code Analysis

Session 7で全コンテキストシステムおよび主要ドメインロジックの静的分析を実施。

- **分析ドキュメント:** 14ファイル（`.claude/code/`）
  - `overall-summary.md` — 全体サマリー + 77件の脆弱性一覧
  - `ai-reference/ai-reference.md` — AI向けクイックリファレンス
  - `battle/` — orchestration, card-execution, damage-and-buff, class-abilities
  - `state/` — context-system (5 Context Providers)
  - `character/` — player, enemy
  - `cards/` — deck-and-mastery
  - `dungeon/` — dungeon-system
  - `inventory/` — equipment-and-items
  - `resource/` — economy
- **特定された脆弱性:** 77件（Critical 5, High 22, Medium 30, Low 20）
- **詳細:** `.claude/code/README.md` を参照

---

## See Also

- **Game Design:** `.claude/docs/`
- **Battle Logic:** `.claude/docs/battle_document/battle_logic.md`
- **Implementation Plan:** `.claude/todos/MASTER_IMPLEMENTATION_PLAN.md`
- **Code Analysis:** `.claude/code/` (静的分析ドキュメント + AI参照)
