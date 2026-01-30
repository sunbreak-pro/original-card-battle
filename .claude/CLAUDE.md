# CLAUDE.md

## Development Commands

```bash
npm run dev          # Vite dev server at localhost:5173
npm run build        # TypeScript check + production build
npm run lint -- --fix
npm run preview      # Preview production build
```

No test framework configured - verify manually in browser.

**Stack:** React 19.2, TypeScript 5.9, Vite 7

**Path alias:** `@/*` → `src/*` (configured in both `vite.config.ts` and `tsconfig.app.json`). Use `@/domain/...`, `@/ui/...`, etc.

**TypeScript strictness:** `noUnusedLocals` and `noUnusedParameters` are enabled — remove unused variables rather than prefixing with `_`. `verbatimModuleSyntax` is enabled — use `import type` for type-only imports. `erasableSyntaxOnly` is enabled — use `as const` objects instead of `enum`, no `namespace` or `module` declarations.

## Architecture

### Context Provider Hierarchy (App.tsx)

```
GameStateProvider → ResourceProvider → PlayerProvider → InventoryProvider → DungeonRunProvider
```

| Context              | Responsibility                                                         |
| -------------------- | ---------------------------------------------------------------------- |
| `GameStateContext`   | Screen routing via `currentScreen`, depth, battleMode                  |
| `ResourceContext`    | Gold, magic stones                                                     |
| `PlayerContext`      | `PlayerData` (persistent) + `RuntimeBattleState` (HP/AP/lives/mastery) |
| `InventoryContext`   | Items, equipment, cards in storage                                     |
| `DungeonRunProvider` | Persists dungeon state across battle transitions (lives in `src/ui/dungeonHtml/`, not `src/contexts/`) |

### Type System

All types in `src/types/` with barrel export. Use `@/types/*` or `@/types`:

```typescript
import type { Card, Player, BuffDebuffState } from '@/types';
```

### Battle System Flow

```
BattleScreen → useBattleOrchestrator → useBattleState
                    ↓
    getInitialDeckCounts() → getCardDataByClass() → createInitialDeck()
                    ↓
    playerPhaseExecution / enemyPhaseExecution → damageCalculation
```

**Buff ownership:** `appliedBy: 'player' | 'enemy' | 'environment'` - duration decreases only during applier's phase.

**Class ability hooks:** Each character class has a dedicated battle hook composed into `useBattleOrchestrator`:
- Swordsman: `useSwordEnergy()` — energy gauge for special attacks
- Mage: `useElementalChain()` — elemental resonance combos across card plays
- Summoner: `useSummonSystem()` — summon spawning, decay, and expiry

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
- `src/ui/` — React components organized by screen area (battleHtml, campsHtml, dungeonHtml, etc.)
- `src/types/` — all type definitions; `src/constants/` — constants only (no types)
- `src/contexts/` — React context providers

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
| React 19 hooks | See `.claude/LESSONS_LEARNED.md` sections 4 & 6 for ref vs setState rules and render-time pattern |
| Adding classes | Use `character-class-creator` skill |

## References

- **`.claude/MEMORY.md`** — current status, active tasks, known bugs (read at session start)
- **`.claude/LESSONS_LEARNED.md`** — critical pitfalls: CSS collisions, React 19 ref rules, context scope
- **`.claude/todos/`** — ongoing refactoring plans; `MASTER_IMPLEMENTATION_PLAN.md` for roadmap
- **`.claude/docs/`** — game design specs by area (battle, card, camp, dungeon, enemy, item)
- **`.claude/skill/`** — 10 development skills (battle-system, camp-facility, card-creator, debugging-active, etc.)
