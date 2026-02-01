# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

```bash
npm run dev          # Vite dev server at localhost:5173
npm run build        # TypeScript check + production build
npm run lint -- --fix
npm run preview      # Preview production build
```

No test framework configured — verify manually in browser.

**Stack:** React 19.2, TypeScript 5.9, Vite 7

**Path alias:** `@/*` → `src/*` (configured in both `vite.config.ts` and `tsconfig.app.json`). Use `@/domain/...`, `@/ui/...`, etc.

**TypeScript strictness:** `noUnusedLocals` and `noUnusedParameters` are enabled — remove unused variables rather than prefixing with `_`. `verbatimModuleSyntax` is enabled — use `import type` for type-only imports. `erasableSyntaxOnly` is enabled — use `as const` objects instead of `enum`, no `namespace` or `module` declarations.

**Known build error:** None. Previous `NodeMap.tsx:252` error was resolved during Session 1 state refactoring.

## Architecture

### Context Provider Hierarchy (App.tsx)

```
GameStateProvider → ResourceProvider → PlayerProvider → InventoryProvider → DungeonRunProvider
```

| Context              | Responsibility                                                         |
| -------------------- | ---------------------------------------------------------------------- |
| `GameStateContext`   | Screen routing via `currentScreen`, depth, battleMode                  |
| `ResourceContext`    | Gold, magic stones                                                     |
| `PlayerContext`      | `PlayerData` (persistent) + `RuntimeBattleState` (HP/AP/lives/mastery) + `deckCards` (custom deck). Resource ops (gold/stones) delegated to `ResourceContext`. |
| `InventoryContext`   | Items, equipment, cards in storage                                     |
| `DungeonRunProvider` | Persists dungeon state across battle transitions (lives in `src/ui/dungeonHtml/`, not `src/contexts/`) |

Battle state is managed entirely by `useBattleOrchestrator` hook — no separate battle contexts.

### Type System

All types in `src/types/` with barrel export. Use `@/types/*` or `@/types`:

```typescript
import type { Card, Player, BuffDebuffState } from '@/types';
```

### Data Location

All static data lives in `src/constants/data/`, NOT in `src/domain/`:

| Data | Location |
|------|----------|
| Card definitions | `src/constants/data/cards/` (SwordmanCards, mageCards, summonerCards) |
| Enemy definitions | `src/constants/data/characters/enemy/` (enemyDepth1-5.ts) |
| Camp facility data | `src/constants/data/camps/` (ShopData, SanctuaryData, etc.) |
| Item/equipment data | `src/constants/data/items/` |
| Battle constants | `src/constants/data/battles/` |

`src/domain/` contains only logic (functions, hooks), not data definitions.

### Battle System Flow

```
BattleScreen → useBattleOrchestrator → useBattleState
                    ↓
    getInitialDeckCounts() → getCardDataByClass() → createInitialDeck()
                    ↓
    playerPhaseExecution / enemyPhaseExecution → damageCalculation
```

**Buff ownership:** `appliedBy: 'player' | 'enemy' | 'environment'` — duration decreases only during applier's phase.

**Multi-hit cards:** `useCardExecution.ts` loops per-hit for `hitCount > 1`, with independent damage calculation and 500ms delay per hit.

**Class ability hooks** (all called unconditionally in `useBattleOrchestrator` per React rules):
- Swordsman: `useSwordEnergy()` in `useClassAbility.ts` — flat damage bonus + bleed chance
- Mage: `useElementalChain()` in `useElementalChain.ts` — resonance `percentMultiplier` applied to base damage via `getElementalDamageModifier`
- Summoner: `useSummonSystem()` in `useSummonSystem.ts` — summon spawning, decay, and expiry

### Shop/Item Data Flow

```
ShopListing (typeId) → ConsumableItemData (name/price/effect) → generateConsumableFromData() → Item
```

- `ConsumableItemData.ts` is the single source of truth for consumable items
- `ShopListing` references items by `typeId`, not by duplicating data

### Screen Routing

`character_select` → `camp` → facilities or `dungeon` → `dungeon_map` → `battle`

### Source Structure

- `src/domain/` — pure business logic (battles, camps, cards, characters, dungeon, item_equipment, save)
- `src/constants/` — constants and all static data (`constants/data/`)
- `src/ui/` — React components organized by screen area (battleHtml, campsHtml, dungeonHtml, etc.)
- `src/types/` — all type definitions
- `src/contexts/` — React context providers

### Asset Paths

Player images: `PLAYER_CHARACTER_IMAGES` in `src/constants/uiConstants.ts` maps `CharacterClass` → image path.
Enemy images: each `EnemyDefinition` has an `imagePath` field (images mostly not yet created; fallback shown).
All asset path constants are centralized in `src/constants/uiConstants.ts`.

## Key Rules

### Immutable Code (DO NOT MODIFY)

- `src/domain/cards/decks/deck.ts`
- `src/domain/cards/decks/deckReducter.ts`

### Conventions

| Area | Rule |
|------|------|
| Types | `PascalCase` |
| Functions | `camelCase` |
| Constants | `UPPER_SNAKE_CASE` |
| UI text | Japanese |
| Code/comments | English |
| CSS sizing | `vh/vw` (use `px` only for borders) |
| CSS selectors | Scope with parent: `.battle-screen .card { }` |
| Adding classes | Use `character-class-creator` skill |
| Chat language | Japanese (ユーザーへの応答は日本語で行う) |

### React 19 Patterns

**Ref vs State:** Never access `ref.current` during render — use `useState` for values displayed in UI or passed to child props.

**Render-time derived state (no side effects):**
```typescript
const [prevValue, setPrevValue] = useState(currentValue);
if (currentValue !== prevValue) {
  setPrevValue(currentValue);
  setDerivedState(newValue);
}
```

**Side effects that need setState:**
```typescript
const guardRef = useRef(false);
useEffect(() => {
  if (!guardRef.current) {
    doSideEffect();
    // eslint-disable-next-line react-hooks/set-state-in-effect -- one-time init guarded by ref
    setSomeState(value);
    guardRef.current = true;
  }
}, [deps]);
```

**Details:** See `.claude/LESSONS_LEARNED.md`

## References

- **`.claude/MEMORY.md`** — current status, active tasks, known bugs (read at session start)
- **`.claude/LESSONS_LEARNED.md`** — critical pitfalls: CSS collisions, React 19 ref rules, context scope
- **`.claude/todos/`** — ongoing refactoring plans; `MASTER_IMPLEMENTATION_PLAN.md` for roadmap
- **`.claude/docs/`** — game design specs by area (battle, card, camp, dungeon, enemy, item)
- **`.claude/code_overview/`** — static analysis docs + AI reference + vulnerability remediation guide
- **`.claude/skill/`** — 10 development skills (battle-system, camp-facility, card-creator, debugging-active, etc.)
