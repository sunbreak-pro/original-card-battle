# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session Workflow

1. **Read `.claude/MEMORY.md`** - current status, active tasks, recent changes
2. **Check `.claude/docs/`** - game design specs before implementing features
3. **Check `.claude/todos/`** - ongoing refactoring plans

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

**TypeScript strictness:** `noUnusedLocals` and `noUnusedParameters` are enabled — remove unused variables rather than prefixing with `_`. `verbatimModuleSyntax` is enabled — use `import type` for type-only imports.

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
| `DungeonRunProvider` | Persists dungeon state across battle transitions                       |

### Type System

All types consolidated in `src/types/` with `@/types/*` path alias:

```typescript
// Import types
import type { Card } from '@/types/cardTypes';
import type { Player, Enemy } from '@/types/characterTypes';
import type { BuffDebuffState } from '@/types/battleTypes';
// Or barrel import
import type { Card, Player, BuffDebuffState } from '@/types';
```

| File                | Contents                                      |
| ------------------- | --------------------------------------------- |
| `cardTypes.ts`      | Card, Rarity, CardCategory, Depth             |
| `characterTypes.ts` | Player, Enemy, CharacterClass                 |
| `battleTypes.ts`    | BuffDebuffType, DamageResult, BuffOwner       |
| `itemTypes.ts`      | Item, Equipment, ConsumableEffect             |
| `campTypes.ts`      | Shop, Guild, Blacksmith, Sanctuary types      |
| `dungeonTypes.ts`   | DungeonNode, DungeonFloor                     |
| `saveTypes.ts`      | SaveData, SaveResult                          |

### Battle System Flow

```
BattleScreen → useBattleOrchestrator → useBattleState
                    ↓
    getInitialDeckCounts() → getCardDataByClass() → createInitialDeck()
                    ↓
    playerPhaseExecution / enemyPhaseExecution → damageCalculation
```

**Buff ownership:** `appliedBy: 'player' | 'enemy' | 'environment'` - duration decreases only during applier's phase.

### Shop/Item Data Flow

```
ShopListing (typeId) → ConsumableItemData (name/price/effect) → generateConsumableFromData() → Item
```

- `ConsumableItemData.ts` is the single source of truth for consumable items
- `ShopListing` references items by `typeId`, not by duplicating data

### Screen Routing

`character_select` → `camp` → facilities or `dungeon` → `dungeon_map` → `battle`

### Source Structure

```
src/
├── types/             # All type definitions (8 files, use @/types/*)
├── constants/         # Constants only (no type definitions)
├── contexts/          # React context providers
├── domain/            # Core business logic (see below)
├── ui/                # React components by screen area
│   ├── animations/    # Animation hooks and engine
│   ├── battleHtml/    # Battle screen components
│   ├── campsHtml/     # Camp facility screens (Guild, Shop, Blacksmith, Sanctuary, Library, Storage)
│   ├── cardHtml/      # Card display components
│   ├── characterSelectHtml/
│   ├── componentsHtml/    # Shared UI components
│   ├── css/           # Stylesheets
│   └── dungeonHtml/   # Dungeon exploration UI
└── utils/

src/domain/
├── battles/       # Battle logic, calculators, phase execution, contexts
├── camps/         # Camp facilities, shop/guild logic
├── cards/         # Card data, deck management, card state
├── characters/    # Player/enemy data, class abilities
├── dungeon/       # Dungeon map generation, node logic
├── item_equipment/# Items, equipment data and generation
└── save/          # Save/load system
```

## Key Rules

### Immutable Code (DO NOT MODIFY)

- `src/domain/cards/decks/deck.ts`
- `src/domain/cards/decks/deckReducter.ts`

### Adding Character Classes

1. Create card data in `src/domain/cards/data/` (e.g., `mageCards.ts`)
2. Add to `INITIAL_DECK_BY_CLASS` in `initialDeckConfig.ts`
3. Add case to `getCardDataByClass()` in `useBattleOrchestrator.ts`
4. Add `createXxxStarterDeck()` function in `CharacterClassData.ts`
5. Update class entry: `isAvailable: true`

### React 19 Hooks Lint Rules

Two conflicting rules require careful handling:

| Rule                              | Prohibition                               | Fix                                   |
| --------------------------------- | ----------------------------------------- | ------------------------------------- |
| `react-hooks/refs`                | No `ref.current` read/write during render | Use `useState` for render-time values |
| `react-hooks/set-state-in-effect` | No `setState` in useEffect                | Move to render-time setState pattern  |

**Render-time setState pattern:**

```typescript
// Track previous value with useState, NOT useRef
const [prevValue, setPrevValue] = useState(currentValue);
if (currentValue !== prevValue) {
  setPrevValue(currentValue);
  setDerivedState(newValue);
}
```

**Side effects that need setState:** Use useEffect + `eslint-disable-next-line react-hooks/set-state-in-effect` with justification comment.

### CSS

- Use `vh/vw` for sizing, `px` only for borders
- **Scope class names:** `.battle-screen .enemy-card { }` not `.enemy-card { }`

### Naming

- Types: `PascalCase` | Functions: `camelCase` | Constants: `UPPER_SNAKE_CASE`
- UI text: Japanese | Code/comments: English

## Key References

- **`.claude/MEMORY.md`** - Current project status, active tasks, known bugs
- **`.claude/LESSONS_LEARNED.md`** - Critical pitfalls with code examples (CSS collisions, React 19 ref rules, context scope)
- **`.claude/todos/MASTER_IMPLEMENTATION_PLAN.md`** - Phase-based implementation roadmap
- **`.claude/skill/`** - 9 Claude development skills (battle-system, camp-facility, card-creator, etc.)

### Design Docs (in `.claude/docs/`)

| Folder              | Contents                                                 |
| ------------------- | -------------------------------------------------------- |
| `Overall_document/` | Master design, lives system                              |
| `battle_document/`  | Battle logic, buff/debuff system                         |
| `card_document/`    | Cards by class (40 each), character system               |
| `camp_document/`    | Facilities (shop, guild, blacksmith, sanctuary, library) |
| `danjeon_document/` | Dungeon exploration, return system                       |
| `enemy_document/`   | Enemy data by depth (1-5), boss system                   |
| `item_document/`    | Equipment and items design                               |
