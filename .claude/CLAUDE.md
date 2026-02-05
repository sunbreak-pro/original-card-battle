# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

```bash
npm run dev          # Vite dev server at localhost:5173
npm run build        # TypeScript check + production build
npm run lint -- --fix
npm run test         # Vitest watch mode
npm run test:run     # Single run
```

**Stack:** React 19.2, TypeScript 5.9, Vite 7

**Path alias:** `@/*` → `src/*` (configured in both `vite.config.ts` and `tsconfig.app.json`)

**TypeScript strictness:** `noUnusedLocals`, `noUnusedParameters` — remove unused variables. `verbatimModuleSyntax` — use `import type`. `erasableSyntaxOnly` — use `as const` objects instead of `enum`.

## Key Rules

### Immutable Code (DO NOT MODIFY)

- `src/domain/cards/decks/deck.ts`
- `src/domain/cards/decks/deckReducer.ts`

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
| State ownership | One context owns each piece of state; others read via hooks |

### React 19 Patterns

**Ref vs State:** Never access `ref.current` during render — use `useState` for values displayed in UI.

**Render-time derived state:**
```typescript
const [prevValue, setPrevValue] = useState(currentValue);
if (currentValue !== prevValue) {
  setPrevValue(currentValue);
  setDerivedState(newValue);
}
```

**Side effects with setState:**
```typescript
const guardRef = useRef(false);
useEffect(() => {
  if (!guardRef.current) {
    doSideEffect();
    setSomeState(value);
    guardRef.current = true;
  }
}, [deps]);
```

**Mutable result pattern:**
```typescript
const result = { success: false };
setResources(prev => {
  if (prev.gold < cost) return prev;
  result.success = true;
  return { ...prev, gold: prev.gold - cost };
});
return result.success;
```

## Architecture Overview

### Context Provider Hierarchy

```
GameStateProvider (screen routing, battle config, depth)
  → SettingsProvider → ToastProvider
    → ResourceProvider (gold dual-pool, magic stones)
      → PlayerProvider (player stats, deck, equipment)
        → InventoryProvider (items, storage)
          → DungeonRunProvider (dungeon exploration state)
```

Battle state is managed by `useBattleOrchestrator` hook (transient, not Context).

### Core Systems

| System | Key Files | Purpose |
|--------|-----------|---------|
| **Battle** | `domain/battles/managements/useBattleOrchestrator.ts` | Turn-based card combat with phase queue |
| **Cards** | `domain/cards/decks/deck.ts`, `deckReducer.ts` | Deck shuffle/draw/discard (IMMUTABLE) |
| **Mastery** | `domain/cards/state/masteryManager.ts` | Card use tracking, derived card unlocks |
| **Class Abilities** | `domain/characters/player/` | Sword Energy (swordsman), Elemental Resonance (mage) |
| **Enemy AI** | `domain/characters/enemy/enemyAI.ts` | Energy-based action selection |
| **Dungeon** | `domain/dungeon/logic/dungeonLogic.ts` | Procedural map generation (5 depths × 5 floors) |
| **Camps** | `domain/camps/logic/` | Shop, Blacksmith, Sanctuary, Guild |

### Game Loop Flow

```
Character Select → Base Camp → Dungeon Entry → Node Navigation → Battle
       ↑                                                          ↓
       ←─────────── Resources & Progression ←── Rewards ──────────┘
```

- **Survive:** Keep all souls + items
- **Death:** Lose exploration resources, -1 life (souls saved)
- **Life = 0:** Game over (full reset)

## Task Completion Rule

When completing a task, update the Development History in `README.md`:
1. Add the date, work, and progress to the table.
2. Store the implementation plan in `.claude/current_plans/`.

README.md serves as the single source of truth for work history.

## References

| Resource | Contents |
|----------|----------|
| `README.md` | Work history, project overview, implementation status |
| `.claude/docs/` | Game design specifications (battle, cards, camps, dungeon, enemies, items) |
| `.claude/code_overview/` | Code analysis, cross-system vulnerabilities, debugging requirements |
| `.claude/feature_plans/` | Future features (quests, titles, NPC, dark market) |
| `.claude/current_plans/` | Current implementation plans and session tracking |
| `.claude/memories/` | Lessons learned and completed refactoring guides |
| `.claude/skills/` | 11 development skills (card-creator, enemy-creator, battle-system, etc.) |
