# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session Workflow

1. **Read `.claude/MEMORY.md`** - current status, active tasks, recent changes
2. **Check `.claude/docs/`** - game design specs before implementing features
3. **Check `.claude/todos/`** - ongoing refactoring plans

## Development Commands

```bash
cd card-battle-main
npm run dev          # Vite dev server at localhost:5173
npm run build        # TypeScript check + production build
npm run lint -- --fix
```

No test framework configured - verify manually in browser.

**Stack:** React 19, TypeScript 5.9, Vite 7

## Architecture

### Context Provider Hierarchy (App.tsx)

```
GameStateProvider → ResourceProvider → PlayerProvider → InventoryProvider → DungeonRunProvider
```

| Context | Responsibility |
|---------|---------------|
| `GameStateContext` | Screen routing via `currentScreen` |
| `ResourceContext` | Gold, magic stones |
| `PlayerContext` | `PlayerData` (persistent) + `RuntimeBattleState` (HP/AP/lives/mastery) |

### Battle System Flow

```
BattleScreen → useBattleOrchestrator → useBattleState
                    ↓
    getInitialDeckCounts() → getCardDataByClass() → createInitialDeck()
                    ↓
    playerPhaseExecution / enemyPhaseExecution → damageCalculation
```

**Buff ownership:** `appliedBy: 'player' | 'enemy' | 'environment'` - duration decreases only during applier's phase.

### Screen Routing

`character_select` → `camp` → facilities or `dungeon` → `dungeon_map` → `battle`

## Key Rules

### Immutable Code (DO NOT MODIFY)

- `src/domain/cards/decks/deck.ts`
- `src/domain/cards/decks/deckReducter.ts`

### Adding Character Classes

1. Create card data in `src/domain/cards/data/`
2. Add to `INITIAL_DECK_BY_CLASS` in `initialDeckConfig.ts`
3. Add case to `getCardDataByClass()` in `useBattleOrchestrator.ts`
4. Update `CharacterClassData.ts`: `isAvailable: true`

### CSS

- Use `vh/vw` for sizing, `px` only for borders
- **Scope class names:** `.battle-screen .enemy-card { }` not `.enemy-card { }`

### Naming

- Types: `PascalCase` | Functions: `camelCase` | Constants: `UPPER_SNAKE_CASE`
- UI text: Japanese | Code/comments: English

## Docs Reference

| Folder | Contents |
|--------|----------|
| `Overall_document/` | Master design, lives system |
| `battle_document/` | Battle logic, buff/debuff |
| `card_document/` | Cards, character classes |
| `camp_document/` | Facilities (shop, guild, etc.) |
| `danjeon_document/` | Dungeon exploration |
| `enemy_document/` | Enemy data by depth |
| `item_document/` | Equipment and items |
