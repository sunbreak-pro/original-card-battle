# CLAUDE.md

> 現状の実装規約・設計判断の SSOT。**「変わらない事実」だけを持ち、手順はスキル/エージェントへ委譲**する。抽象構想・設計原則は `docs/vision/core.md`。Claude Code 起動時に auto-load。

---

## 0. Meta

- **役割**: 現状の実装規約 / アーキテクチャ / 規約の参照点（400 行以下目標）。抽象構想は `docs/vision/core.md`（ADR は作らない）
- **更新規則**: 実装変更はコードと同一コミットで本ファイルを更新。新機能の要件は `docs/requirements/`、設計原則は `docs/vision/core.md` へ
- **タスク運用**: 進行中 / 予定は `memory/chat-<self>.md`、変更履歴は `history/chat-<self>.md`（いずれも task-tracker スキル経由で更新、手動編集しない）。**課題追跡の正は GitHub Issues**。`README.md` の Development History は完了履歴の要約
- **関連**: `memory/`(タスク) / `history/`(履歴) / `docs/vision/core.md`(設計原則) / `docs/requirements/`(要件) / `docs/known-issues/`([INDEX](./docs/known-issues/INDEX.md)) / `docs/code-explanation/`(コード解析・脆弱性) / `archive/`(完了プラン)

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
- **メイン（`C:\Users\user\orca\original-card-battle`）は `main` 専有**。feature 作業は worktree から行う。メインで `git checkout <feature>` をしない
- **レーンのブランチは `<prefix>/<slug>-<issue>`**（例: `feat/battle-36`, `docs/cards-38`）。待機ブランチは `chore/lane-<slug>`、tracker 専用は `chore/tracker-<self>-YYYYMMDD`

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

> 設計上の正本は `docs/vision/concept-v3.md`（2026-09-12「生と継承」）。下のループは設計の目標形。Web 版コード（`src/`）は旧ループ（キャンプ 5 施設 / ライフ制 / AP）のまま残っており、Unity 側で新ループを実装する。

```
継承の間（遺産候補の確認 / 生存者ボーナス）→ 出立（ツール / 戦闘デッキ）→ ダンジョン（刻限 × 瘴気）→ 戦闘（間合い × スタミナ）
       ↑                                                                                    ↓
       ←──── 生存ルート（生存者ボーナス） / 死亡（死亡地点に遺産と手記を残す） ←────────────┘
```

- **生存ルート:** 生きたまま次のキャラクターへ。生存者は一人まで
- **死亡:** 成長は消え、手記は死亡地点に残る。次のキャラクターが死亡地点を訪れ、遺産（技術 / 手記の頁）から数個だけ選ぶ。痕跡は回収まで残り、1 つの生で 1 件。浅層の意図的な死では高習熟カードは残らない
- **実装の正:** 戦闘コアは `unity-port/BattleCore/`（C#）が正（2026-09-12）。TS の `src/ui/battle-lab/core/` は凍結
- **戦闘描写（2026-09-19）:** 固定台本を再生する画面は `unity-port/unity-project-kit/Assets/View/Depiction/`（`Script/` は純 C# の台本と型、View は名前空間 `Depiction.View`）。C# はこのリポが正で `npm run unity:sync` で写す。プレハブ / シーン / `.meta` は Unity リポが正（`Assets/Prefabs/Depiction/`、`Assets/Scenes/BattleDepiction.unity`）。View は計算せず、値は全て台本から取る
- **凍結（2026-09-12）:** アーマー（AP / 装備耐久 / 修理）、装備・Gold・ソウル経済、ショップ / 鍛冶屋 / サンクチュアリ。防御は Guard のみ

## Testing

Tests live in `__tests__/` subdirectories adjacent to source files (e.g., `src/domain/cards/decks/__tests__/deck.test.ts`).

**Pattern:** Pure function unit tests with `describe/it/expect`. Use `vi.spyOn(Math, 'random')` for deterministic shuffles. Create minimal fixtures via helper functions (e.g., `createTestCard()`).

## Skills Quick Reference

| Task                                   | Skill                        |
| -------------------------------------- | ---------------------------- |
| Add new card                           | `card-creator`               |
| Add new enemy                          | `enemy-creator`              |
| Add character class                    | `character-class-creator`    |
| Battle system changes                  | `battle-system`              |
| Camp facility work                     | `camp-facility`              |
| Dungeon system                         | `dungeon-system`             |
| UI/UX work                             | `ui-ux-creator`              |
| Character art / UI production pipeline | `visual-production-pipeline` |
| 前のセッションの続きを引き継ぐ         | `session-successor`          |
| セッション開始 / `/clear` の後         | `session-loader`             |
| worktree / レーン / ブランチ切替       | `worktree-policy`            |
| 課題を Issue として起票                | `issue-dispatch`             |
| 次に着手する Issue を決める            | `issue-prompter`             |
| open Issue を仕分ける                  | `/loop-triage`               |
| Issue 1 件を commit まで実装           | `/loop-implement`            |
| 検証ゲートを通して原因を切り分ける     | `/loop-verify`               |
| 失敗から再発防止の 1 行を回収          | `/loop-postmortem`           |
| Find design docs                       | `design-research`            |
| Bug investigation                      | `debugging-active`           |
| Error prevention                       | `debugging-error-prevention` |

## Development Workflows

> 2026-09-20 に life-editor の開発環境を移植。手順はスキルへ委譲し、本節は本リポの規約だけを持つ。

### 課題追跡は GitHub Issues が正

- **起票先**: `gh issue create -R sunbreak-pro/original-card-battle`。テンプレートは `.github/ISSUE_TEMPLATE/` の 3 種（Known Issue / Roadmap Item / Human Task）
- **ラベル**: `type:` (bug / feature / task / human) × `sev:` (blocking / important / minor) × `area:` (battle / cards / enemy / dungeon / ui / art / unity / docs / tooling) × `status:` (monitoring / workaround / frozen)
- **スコープの境界**: Issue は**プロダクトの課題専用**。Claude Code 環境やハーネス起因の問題は `docs/known-issues/` に置き、Issue にしない
- **着手前に必ず open を見る**: `gh issue list --label type:bug`。重複起票を避ける
- **1 Issue = 1 ブランチ = 1 PR**。PR 本文に `Closes #<n>` を書く。**merge は常に人**（`gh pr merge` は settings.json の `ask` で止まる）

### 判断の控えは life-editor

GitHub は在庫棚（課題の正確な台帳）、life-editor は献立表（何を作るか決めて記録する場所）。**Issue の代替にはしない**（2026-09-20 こうだいさん決定）。

- **置くもの**: 判断の控えと調査結果（Note）／次にやること（Todo）。プロジェクトを跨いで見たいものだけを上げる
- **置かないもの**: 課題の状態・担当・PR との結線。ステータスが 2 値しかなく、コメントが無く、後勝ち同期で記録が消えるため、正本は GitHub Issue のまま
- **ローカルの `.claude/memory/` は残す**。per-chat の進捗は従来どおり task-tracker が書く
- **手順は `life-editor-bridge` スキル**。セッション頭に `node ~/.claude/skills/life-editor-bridge/scripts/le.mjs pull`、判断が出たら `note`、次にやることは `todo`
- **タグ**: `proj/original-card-battle` と `開発` が自動で付く。プロジェクト名の正本は `.claude/life-editor.json`
- **落ちても止まらない**。life-editor は移行中で、失敗したらスクリプトが止まるだけ。作業は先へ進める

### タスクの進捗は per-chat ファイル

並行チャットが 1 つのファイルの同じ行を書き換えると必ず衝突するため、チャットごとにファイルを分ける。

- **自分の名前**: `.claude/comm/.session-name`（git 非追跡）。`chat-` 接頭辞は付けない（例: `main`）
- **進捗**: `.claude/memory/chat-<self>.md` / **履歴**: `.claude/history/chat-<self>.md`
- **横断ビュー**: `.claude/memory/INDEX.md` / `.claude/history/INDEX.md` は **git 非追跡の生成物**。`.claude/hooks/regen-index.sh` が SessionStart と task-tracker から再生成する。手編集も `git add` もしない
- **更新は task-tracker スキル経由**（手動編集しない）。**開始時**: 「進行中」へ起票（プランがあれば `docs/vision/plans/YYYY-MM-DD-<slug>.md` を作成しリンク）。**完了時**: (1) 進行中 → 直近の完了へ移動、(2) history 先頭に概要+変更点、(3) `README.md` Development History に 1 行要約、(4) 完了プランは `archive/` へ移動
- **tracker の更新を実装コミットに混ぜない**。`pre-commit-tracker-guard.sh` が止める。意図的に同梱するときだけコマンドに `[tracker-ok]` を含める
- `.claude/MEMORY.md` / `.claude/HISTORY.md` / `TODO.md` は移転前の後方互換ポインタ。更新しない

### hooks（`.claude/settings.json`）

| タイミング       | スクリプト                    | 役割                                                             |
| ---------------- | ----------------------------- | ---------------------------------------------------------------- |
| SessionStart     | `regen-index.sh`              | `memory/INDEX.md` と `history/INDEX.md` を再生成                 |
| SessionStart     | `session-start-check.sh`      | `.session-name` / `.session-branch` の宣言整合を検査（警告のみ） |
| PreToolUse(Bash) | `pre-commit-mcp-check.sh`     | `.mcp.json` のトークン平文化を commit 前に検出                   |
| PreToolUse(Bash) | `pre-commit-index-guard.sh`   | 生成物 `INDEX.md` の commit 混入を自動除外                       |
| PreToolUse(Bash) | `pre-commit-tracker-guard.sh` | tracker と実装の同梱コミットをブロック                           |

実体は `$HOME/dev/Claude/hooks-lib/` を優先し、無ければ `.claude/scripts/hooks-lib/` の同梱版に落ちる。

### 禁止コマンド（`.claude/settings.json` の `deny`）

main / master への直接 push、force push、`git reset --hard`、`git branch -D`、`rm -rf .git`。**`gh pr merge` は `ask`** で必ず人に聞く。

### 並行作業は worktree のレーン

> 規約の本体は `worktree-policy` スキル。本節は一覧と禁止事項だけを持つ。

worktree はリポジトリの外、`C:\Users\user\orca\workspaces\original-card-battle\<slug>\` に置く（絶対パスで作る）。**1 レーン = 1 worktree = 1 チャット**で、ブランチは Issue ごとに切り替える。

| slug      | 担当                                       | 既定の `area:`   |
| --------- | ------------------------------------------ | ---------------- |
| `cards`   | カード設計・デッキ・習熟・敵ロースター     | `cards` `enemy`  |
| `design`  | 画面と絵。UI / UX 設計・演出・立ち絵・素材 | `ui` `art`       |
| `battle`  | 戦闘プログラム（C# の BattleCore が正）    | `battle` `unity` |
| `dungeon` | 探索プログラム。刻限 / 瘴気 / ノード       | `dungeon`        |
| `audit`   | 監査。設計書と実装の整合、既知課題の棚卸し | `docs` `tooling` |

- **宛先ラベルは `lane:<slug>`**。付けなければ `issue-prompter` が上の `area:` から既定のレーンへ振る
- **`audit` は読み取り専用**。整合監査の結果を Issue に起票し、修正は担当レーンへ回す
- **試運転はメインだけ**。`npm run dev`・実ブラウザ検証・Unity Editor での手触り確認はメインで行い、各レーンは `npm run build` / `npm run lint` / `npm run test:run` / `dotnet test` の静的検証まで
- **one writer per artifact**。同じファイルを 2 レーンに触らせない
- **`.claude/comm/.session-name` と `.session-branch` を必ず書く**。ブランチを切り替えるたびに `.session-branch` を更新する（省略すると hook が無音スキップする）

## Document System

- **フロー**: Vision（`docs/vision/core.md`、ADR 不使用）→ 実装プラン（`docs/vision/plans/YYYY-MM-DD-<slug>.md`）→ 完了で `archive/` 移動・規約は本ファイルへ統合。進捗 / 履歴は per-chat（`memory/` `history/`、task-tracker 経由）
- **Known Issue**: `docs/known-issues/` に Root Cause + 再発防止を蓄積。発見時 `NNN-<slug>.md` 作成 + `INDEX.md` 更新、解決時 Status=Fixed。**類似バグはまず `INDEX.md` を grep**
- **設計書 vs 実装**: ゲーム数値は `docs/*_document/` の設計書を正とし、差分は設計書側か実装側へ寄せて解消（`design-research` スキル）

## References

| Resource                         | Contents                                                                   |
| -------------------------------- | -------------------------------------------------------------------------- |
| `memory/chat-<self>.md`          | タスクトラッカー — 進行中 / 直近の完了 / 予定（per-chat）                  |
| `history/chat-<self>.md`         | 変更履歴（降順、概要+変更点。per-chat）                                    |
| `.github/ISSUE_TEMPLATE/`        | Issue テンプレート 3 種（Known Issue / Roadmap Item / Human Task）         |
| `.claude/hooks/`                 | SessionStart と PreToolUse の hook 5 本                                    |
| `README.md`                      | プロジェクト概要・Development History（完了履歴の要約）                    |
| `.claude/docs/INDEX.md`          | ドキュメント索引（標準構造 + ゲーム設計書）                                |
| `.claude/docs/vision/core.md`    | Vision・設計原則                                                           |
| `.claude/docs/*_document/`       | Game design specs (battle, cards, camps, dungeon, enemies, items, journal) |
| `.claude/docs/code-explanation/` | Code analysis, vulnerability tracker, testing analysis                     |
| `.claude/docs/vision/plans/`     | Active plans + future features (quest/title/NPC/dark market/PixiJS)        |
| `.claude/docs/known-issues/`     | Root Cause + 再発防止知見（INDEX + LESSONS_LEARNED）                       |
| `.claude/skills/`                | プロジェクト固有スキル（Skills Quick Reference 参照）                      |
| `.claude/agents/`                | プロジェクト固有エージェント（リンク実体は agents-lib）                    |
