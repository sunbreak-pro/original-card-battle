# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

```bash
npm run dev          # Vite dev server at localhost:5173
npm run build        # TypeScript check + production build
npm run lint -- --fix
npm run test         # Vitest watch mode
npm run test:run     # Single run
npx vitest run src/domain/cards/decks/__tests__/deck.test.ts  # Single file
```

**Stack:** React 19.2, TypeScript 5.9, Vite 7, Vitest 4.0

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

### Context Provider Hierarchy (as nested in App.tsx)

```
ErrorBoundary
  → GameStateProvider (screen routing, battle config, depth)
    → SettingsProvider
      → JournalProvider (overlay UI state)
        → ToastProvider
          → ResourceProvider (gold dual-pool, magic stones)
            → PlayerProvider (player stats, deck, equipment)
              → InventoryProvider (items, storage)
                → DungeonRunProvider (dungeon exploration state)
```

Battle state is managed by `useBattleOrchestrator` hook (transient, not Context). `GuildContext` is local to Guild screen only.

### Screen Routing (GameStateContext)

`gameState.screen` drives rendering in `AppContent`:
- `character_select` → `camp` → facility screens (`guild`, `shop`, `blacksmith`, `sanctuary`)
- `camp` → `dungeon` (DungeonGate) → `dungeon_map` (ExplorationScreen with NodeMap)
- `camp`/`dungeon_map` → `battle` (BattleScreen or GuildBattleScreen for exams)

Global overlays: `BrightnessOverlay`, `ToastContainer`, `JournalOverlay`

### Battle Orchestrator Hook Composition

```
useBattleOrchestrator (main orchestrator, ~877 lines)
  ├── useBattleState       — HP, AP, Guard, Buffs
  ├── useBattlePhase       — Phase queue, turn management
  ├── useCardExecution     — Card effect execution
  ├── useCharacterPhaseExecution — Player/enemy phase logic
  ├── useSwordEnergy       — Swordsman class ability
  ├── useElementalChain    — Mage class ability
  ├── useCardAnimation     — Draw/discard/damage effects
  └── useTurnTransition    — Turn messages
```

Battle flow: Init phase queue (speed-sorted) → Player phase (draw, buffs, wait for input) → Enemy phase (AI select, auto-advance) → Phase end (DoT, discard) → Next phase.

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

### Data vs Logic Separation

- **Card data:** `src/constants/data/cards/` (swordsman, mage card definitions)
- **Card logic:** `src/domain/cards/` (deck operations, mastery)
- **Enemy data:** `src/constants/data/characters/enemies/` (per-depth enemy stats)
- **Enemy logic:** `src/domain/characters/enemy/` (AI, behavior)
- **Camp data:** `src/constants/data/camps/` (shop items, prices)
- **Camp logic:** `src/domain/camps/logic/` (stock, pricing, crafting)
- **Type definitions:** `src/types/` (8 files + barrel export via `index.ts`)

### Game Loop Flow

```
Character Select → Base Camp → Dungeon Entry → Node Navigation → Battle
       ↑                                                          ↓
       ←─────────── Resources & Progression ←── Rewards ──────────┘
```

- **Survive:** Keep all souls + items
- **Death:** Lose exploration resources, -1 life (souls saved)
- **Life = 0:** Game over (full reset)

## Testing

Tests live in `__tests__/` subdirectories adjacent to source files (e.g., `src/domain/cards/decks/__tests__/deck.test.ts`).

**Pattern:** Pure function unit tests with `describe/it/expect`. Use `vi.spyOn(Math, 'random')` for deterministic shuffles. Create minimal fixtures via helper functions (e.g., `createTestCard()`).

## Skills Quick Reference

| Task | Skill |
|------|-------|
| Add new card | `card-creator` |
| Add new enemy | `enemy-creator` |
| Add character class | `character-class-creator` |
| Battle system changes | `battle-system` |
| Camp facility work | `camp-facility` |
| Dungeon system | `dungeon-system` |
| UI/UX work | `ui-ux-creator` |
| Find design docs | `design-research` |
| Bug investigation | `debugging-active` |
| Error prevention | `debugging-error-prevention` |

## Task Completion Rule

タスク開始時:
1. `TODO.md` のバックログから進行中へ行を移動。

タスク完了時:
1. `TODO.md` の進行中から行を削除。
2. `README.md` の Development History に日付・作業・進捗を追記。
3. 実装計画書がある場合は `.claude/archive/` へ保存。

`TODO.md` = 未完了タスクの情報源、`README.md` = 完了履歴の情報源。

## References

| Resource | Contents |
|----------|----------|
| `TODO.md` | タスク一覧 — 優先度、ステータス、仕様リンク |
| `README.md` | Work history, project overview, implementation status |
| `.claude/docs/` | Game design specifications (battle, cards, camps, dungeon, enemies, items) |
| `.claude/code_overview/` | Code analysis, cross-system vulnerabilities, debugging requirements |
| `.claude/feature_plans/` | Future features (quests, titles, NPC, dark market) |
| `.claude/memories/` | Lessons learned and completed refactoring guides |
| `.claude/skills/` | 11 development skills (see Skills Quick Reference above) |
