# CLAUDE.md

> 現状の実装規約・設計判断の SSOT。**「変わらない事実」だけを持ち、手順はスキル/エージェントへ委譲**する。抽象構想・設計原則は `docs/vision/core.md`。Claude Code 起動時に auto-load。

---

## 0. Meta

- **役割**: 現状の実装規約 / アーキテクチャ / 規約の参照点（400 行以下目標）。抽象構想は `docs/vision/core.md`（ADR は作らない）
- **更新規則**: 実装変更はコードと同一コミットで本ファイルを更新。新機能の要件は `docs/requirements/`、設計原則は `docs/vision/core.md` へ
- **タスク運用**: 進行中 / 予定は `MEMORY.md`、変更履歴は `HISTORY.md`（いずれも task-tracker スキル経由で更新、手動編集しない）。`README.md` の Development History は完了履歴の要約
- **関連**: `MEMORY.md`(タスク) / `HISTORY.md`(履歴) / `docs/vision/core.md`(設計原則) / `docs/requirements/`(要件) / `docs/known-issues/`([INDEX](./docs/known-issues/INDEX.md)) / `docs/code-explanation/`(コード解析・脆弱性) / `archive/`(完了プラン)

---

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

## Git / Branch Workflow

ブランチ運用は GitHub Flow。詳細手順はグローバル `git-branch-flow` スキルに委譲し、本節は本リポの規約のみを持つ。

- **main + 短命 feature ブランチ**。長期作業ブランチを作らない（溜めると統合が重くなる。`docs/realism-concept-v2` が40コミット化した反省）
- **命名**: `feat/` `fix/` `docs/` `chore/` + kebab-case（例: `feat/stamina-system`）。snake_case 禁止（旧 `battle_logic` 等）
- **1 機能 = 1 ブランチ = 1 PR**。マージは PR 経由で履歴を残す
- **PR マージ後はブランチ削除**（ローカル・リモート両方）
- **main は常に origin/main 追従**（作業開始前に pull）
- **不要な未マージ作業は `archive/<name>` タグで保全してから削除**（`git tag archive/<name> <branch>` → push → ブランチ削除。一覧を汚さず復元可能に）

## Key Rules

### Immutable Code (DO NOT MODIFY)

- `src/domain/cards/decks/deck.ts`
- `src/domain/cards/decks/deckReducer.ts`

### Conventions

| Area            | Rule                                                        |
| --------------- | ----------------------------------------------------------- |
| Types           | `PascalCase`                                                |
| Functions       | `camelCase`                                                 |
| Constants       | `UPPER_SNAKE_CASE`                                          |
| UI text         | Japanese                                                    |
| Code/comments   | English                                                     |
| CSS sizing      | `vh/vw` (use `px` only for borders)                         |
| CSS selectors   | Scope with parent: `.battle-screen .card { }`               |
| Adding classes  | Use `character-class-creator` skill                         |
| Chat language   | Japanese (ユーザーへの応答は日本語で行う)                   |
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
setResources((prev) => {
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
                  → GuildProvider (quests, rumors)
```

Battle state is managed by `useBattleOrchestrator` hook (transient, not Context).

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

| System              | Key Files                                             | Purpose                                              |
| ------------------- | ----------------------------------------------------- | ---------------------------------------------------- |
| **Battle**          | `domain/battles/managements/useBattleOrchestrator.ts` | Turn-based card combat with phase queue              |
| **Cards**           | `domain/cards/decks/deck.ts`, `deckReducer.ts`        | Deck shuffle/draw/discard (IMMUTABLE)                |
| **Mastery**         | `domain/cards/state/masteryManager.ts`                | Card use tracking, derived card unlocks              |
| **Class Abilities** | `domain/characters/player/`                           | Sword Energy (swordsman), Elemental Resonance (mage) |
| **Enemy AI**        | `domain/characters/enemy/enemyAI.ts`                  | Energy-based action selection                        |
| **Dungeon**         | `domain/dungeon/logic/dungeonLogic.ts`                | Procedural map generation (5 depths × 5 floors)      |
| **Camps**           | `domain/camps/logic/`                                 | Shop, Blacksmith, Sanctuary, Guild                   |

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

| Task                  | Skill                        |
| --------------------- | ---------------------------- |
| Add new card          | `card-creator`               |
| Add new enemy         | `enemy-creator`              |
| Add character class   | `character-class-creator`    |
| Battle system changes | `battle-system`              |
| Camp facility work    | `camp-facility`              |
| Dungeon system        | `dungeon-system`             |
| UI/UX work            | `ui-ux-creator`              |
| Find design docs      | `design-research`            |
| Bug investigation     | `debugging-active`           |
| Error prevention      | `debugging-error-prevention` |

## Task Completion Rule

タスク管理は `MEMORY.md` / `HISTORY.md`（task-tracker スキル経由、手動編集しない）。

**開始時**: task-tracker で `MEMORY.md` の「進行中」へ起票（プランがあれば `docs/vision/plans/YYYY-MM-DD-<slug>.md` を作成しリンク）。

**完了時**: task-tracker で (1) `MEMORY.md` 進行中 → 直近の完了へ移動、(2) `HISTORY.md` 先頭に概要+変更点を追記、(3) `README.md` Development History に 1 行要約、(4) 完了プランは `archive/` へ移動。

> `MEMORY.md` = 未完了タスクの SSOT、`HISTORY.md` = 変更履歴、`README.md` = 完了履歴の要約。`TODO.md` は `MEMORY.md` への薄いポインタ（後方互換）。

## Document System

- **フロー**: Vision（`docs/vision/core.md`、ADR 不使用）→ 実装プラン（`docs/vision/plans/YYYY-MM-DD-<slug>.md`）→ 完了で `archive/` 移動・規約は本ファイルへ統合。MEMORY/HISTORY はセッション単位（task-tracker 経由）
- **Known Issue**: `docs/known-issues/` に Root Cause + 再発防止を蓄積。発見時 `NNN-<slug>.md` 作成 + `INDEX.md` 更新、解決時 Status=Fixed。**類似バグはまず `INDEX.md` を grep**
- **設計書 vs 実装**: ゲーム数値は `docs/*_document/` の設計書を正とし、差分は設計書側か実装側へ寄せて解消（`design-research` スキル）

## References

| Resource                         | Contents                                                                   |
| -------------------------------- | -------------------------------------------------------------------------- |
| `MEMORY.md`                      | タスクトラッカー — 進行中 / 直近の完了 / 予定                              |
| `HISTORY.md`                     | セッション単位の変更履歴（降順、概要+変更点）                              |
| `README.md`                      | プロジェクト概要・Development History（完了履歴の要約）                    |
| `.claude/docs/INDEX.md`          | ドキュメント索引（標準構造 + ゲーム設計書）                                |
| `.claude/docs/vision/core.md`    | Vision・設計原則                                                           |
| `.claude/docs/*_document/`       | Game design specs (battle, cards, camps, dungeon, enemies, items, journal) |
| `.claude/docs/code-explanation/` | Code analysis, vulnerability tracker, testing analysis                     |
| `.claude/docs/vision/plans/`     | Active plans + future features (quest/title/NPC/dark market/PixiJS)        |
| `.claude/docs/known-issues/`     | Root Cause + 再発防止知見（INDEX + LESSONS_LEARNED）                       |
| `.claude/skills/`                | プロジェクト固有スキル（Skills Quick Reference 参照）                      |
| `.claude/agents/`                | プロジェクト固有エージェント（リンク実体は agents-lib）                    |
