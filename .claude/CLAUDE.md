# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session Workflow

**Read `.claude/MEMORY.md` first** - contains current project status, active tasks, and recent changes.

Check `.claude/docs/` for game design specs before implementing features.

## Development Commands

```bash
# All commands run from card-battle-main/ (React project root)
cd card-battle-main

npm run dev          # Start dev server at http://localhost:5173/
npm run build        # TypeScript check + production build
npm run lint -- --fix # ESLint with auto-fix
```

## Architecture Overview

### Context Provider Hierarchy
Providers in `App.tsx` wrap the entire app:
```
GameStateProvider → ResourceProvider → PlayerProvider → InventoryProvider → DungeonRunProvider
```
- `GameStateContext` controls screen routing via `currentScreen`
- State that persists across screens (like dungeon runs) must be placed high enough in the tree

### Domain-Driven Structure
- **Domain layer** (`src/domain/`) - pure game logic, UI-independent
- **UI layer** (`src/ui/`) - React components consuming domain logic
- Type definitions in `src/domain/*/type/` directories

### Battle System Flow
1. `BattleScreen.tsx` → `useBattleOrchestrator.ts` → `useBattleState.ts`
2. Phase execution: `playerPhaseExecution.ts`, `enemyPhaseExecution.ts`
3. Damage calculation: `damageCalculation.ts`, `battleLogic.ts`

### CSS Architecture
Modular CSS in `src/ui/css/`:
- `core/` - variables.css (CSS custom properties), reset.css
- `components/` - reusable (buttons, tabs, modals, bars)
- `animations/` - shared @keyframes (single source of truth)
- `pages/battle/` - battle screen modules
- `camps/` - facility-specific styles

## Key Conventions

### Immutable Code Zones
**DO NOT MODIFY** without explicit approval:
- `src/domain/cards/decks/deck.ts` - Card shuffling logic
- `src/domain/cards/decks/deckReducter.ts` - Deck state management

### CSS Rules
- Use viewport units (vh, vw) for sizing; px only for borders
- **Scope generic class names** with parent element to prevent collisions:
  ```css
  .battle-screen .enemy-card { ... }  /* Good */
  .enemy-card { ... }                  /* Bad - can conflict */
  ```

### Naming
- Types/Interfaces: `PascalCase`
- Functions: `camelCase` (verb-first)
- Constants: `UPPER_SNAKE_CASE`

## Game Design References

Located in `.claude/docs/`:
- `battle_document/` - Battle logic, buff/debuff system
- `card_document/` - Card mechanics, character system
- `camp_document/` - Facility designs (shop, guild, blacksmith, etc.)
- `enemy_document/` - Enemy data by depth
