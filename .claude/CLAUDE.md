# CLAUDE.md

> 現状の実装規約・設計判断の SSOT。**「変わらない事実」だけを持ち、手順はスキル/エージェントへ委譲**する。抽象構想・設計原則は `docs/vision/core.md`。Claude Code 起動時に auto-load。

---

## 0. Meta

- **役割**: 現状の実装規約 / アーキテクチャ / 規約の参照点（400 行以下目標）。抽象構想は `docs/vision/core.md`
- **決定の置き場**: ゲームの設計の決定は、設計書の本文に入れます。進め方の決定だけを、決定の記録（`.claude/decisions/`）に置きます。本ファイルには今の決まりと理由だけを書き、いつ誰が決めたかは決定の記録に書きます
- **更新規則**: 実装変更はコードと同一コミットで本ファイルを更新。新機能の要件は `docs/requirements/`、設計原則は `docs/vision/core.md` へ
- **タスク運用**: 進行中 / 予定は `memory/chat-<self>.md`、変更履歴は `history/chat-<self>.md`（いずれも task-tracker スキル経由で更新、手動編集しない）。**課題追跡の正は GitHub Issues**
- **関連**: `memory/`(タスク) / `history/`(履歴) / `decisions/`(進め方の決定の記録) / `docs/vision/core.md`(設計原則) / `docs/requirements/`(要件) / `docs/known-issues/`([INDEX](./docs/known-issues/INDEX.md)) / `docs/code-explanation/`(コード解析・脆弱性) / `archive/`(完了プラン)

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

ブランチの運用と、commit から merge までの手順は、共通の `git-workflow` スキルに従います。この節は、このリポジトリだけの決まりを持ちます。

- **不要な未マージ作業は `archive/<name>` タグで保全してから削除**（`git tag archive/<name> <branch>` → push → ブランチ削除。一覧を汚さず復元可能に）
- **メイン（`C:\Users\user\orca\original-card-battle`）は `main` 専有**。feature 作業は worktree から行う。メインで `git checkout <feature>` をしない
- **担当のブランチは `<prefix>/<slug>-<issue>`**（例: `feat/battle-36`, `docs/cards-38`）。待機ブランチは `chore/lane-<slug>`、tracker 専用は `chore/tracker-<self>-YYYYMMDD`

## Key Rules

### Immutable Code (DO NOT MODIFY)

- `src/domain/cards/decks/deck.ts`
- `src/domain/cards/decks/deckReducer.ts`

### Conventions

| Area | Rule |
| --- | --- |
| Types | `PascalCase` |
| Functions | `camelCase` |
| Constants | `UPPER_SNAKE_CASE` |
| UI text | Japanese |
| Code/comments | English |
| CSS sizing | `vh/vw` (use `px` only for borders) |
| CSS selectors | Scope with parent: `.battle-screen .card { }` |
| Adding classes | Use `character-class-creator` skill |
| Chat language | Japanese。書き方は下の「こうだいさん向けの文章」に従う |
| State ownership | One context owns each piece of state; others read via hooks |

### こうだいさん向けの文章

会話・報告・`docs/reports/` の HTML は、グローバルの `rules/japanese.md` と output style `tone-persona` に従う。本ファイルとスキルの文体（体言止め・内輪の語）を読み手向けの文章に持ち込まない。内輪の語は、グローバルの `rules/japanese.md` の「内輪の語の言い換え」の表のとおりに言い換える。その表に無い、このリポジトリだけの語は、次のとおりに言い換える。

| 内輪の語 | こうだいさん向けの書き方 |
| --- | --- |
| 台帳 | 元になる文書の一覧（`docs/SOURCES.md`） |
| 宛先を付ける | どの担当に回すかのラベルを付ける |
| 起票する / 退役させる | Issue を作る / 使わなくする |

「こうだいさんの作業」と書くときは、何をするかまで書く。

スキル名・ラベル名・ファイル名は、何をするものかを先に言ってから添える。

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

| System | Key Files | Purpose |
| --- | --- | --- |
| **Battle** | `domain/battles/managements/useBattleOrchestrator.ts` | Turn-based card combat with phase queue |
| **Cards** | `domain/cards/decks/deck.ts`, `deckReducer.ts` | Deck shuffle/draw/discard (IMMUTABLE) |
| **Mastery** | `unity-port/BattleCore/Mastery.cs` | Mastery tally and rank (`MasteryTally` `RankFor`)。戦闘コアの正は C#。Web 版 `src/` は旧ループで凍結 |
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

> 設計の元になる文書は `docs/vision/concept-v3.md`（「生と継承」）。下のループは設計の目標形。Web 版コード（`src/`）は旧ループ（キャンプ 5 施設 / ライフ制 / AP）のまま残っており、Unity 側で新ループを実装する。

```
継承の間（遺産候補の確認 / 生存者ボーナス）→ 出立（ツール / 戦闘デッキ）→ ダンジョン（崩壊の暦 × 3 つの領域）→ 戦闘（間合い × スタミナ）
       ↑                                                                                    ↓
       ←──── 生存ルート（生存者ボーナス） / 死亡（死亡地点に遺産と手記を残す） ←────────────┘
```

- **生存ルート:** 生きたまま次のキャラクターへ。生存者は一人まで
- **死亡:** 成長は消え、手記は死亡地点に残る。次のキャラクターが死亡地点を訪れ、遺産（技術 / 手記の頁）から数個だけ選ぶ。痕跡は回収まで残り、1 つの生で 1 件。浅層の意図的な死では高習熟カードは残らない
- **実装の正:** 戦闘コアは `unity-port/BattleCore/`（C#）が正（経緯は `.claude/decisions/2026-09-12-battle-core-in-csharp.md`）。TS の `src/ui/battle-lab/core/` は凍結。戦闘の規則は `docs/battle_document/battle_core_v4.md` v4.3（横 1 列のマスの間合い N）で、コアの追従は #162
- **戦闘描写（経緯は `.claude/decisions/2026-09-20-battle-depiction.md`）:** 画面は `unity-port/unity-project-kit/Assets/View/Depiction/`（`Script/` は純 C# の台本と型、View は名前空間 `Depiction.View`）。C# はこのリポが正で `npm run unity:sync` で写す。プレハブ / シーン / `.meta` は Unity リポが正（`Assets/Prefabs/Depiction/`、撮影用の `Assets/Scenes/BattleDepiction.unity`、縦切りの実戦用 `Assets/Scenes/Battle.unity`）。**View は計算せず、値は全て台本から取る**。敵が 2 体以上のとき、`DepictionPlayer` は敵ごとに人型・数値パネル・予兆の札を持ちます。1 体目はシーンの部品で、2 体目以降は実行時の複製です。部品は `DepictionFrame.Enemies` の添字と揃い、配置と振り分けの計算は `Script/` の `EnemyScreen` `OmenRowLayout` `FloorStripes` `EnemySwatch` が持ちます（#589）。台本を出すのは `IDepictionSource` の実装で、実戦は v4.3 のコアで戦う `Bridge/` の `CoreBattleSource`（`BattleCore.TurnLoop` のイベント列を `CoreScriptWriter` が台本へ写す。`Battle.unity` では `BattleBootstrap` が Inspector の敵 id と乱数の種を `BattleLaunch` へ写し、`DepictionPlayer.UseSource` へ渡す）、撮影用は固定台本の `DepictionRunner`（`DepictionPlayer.scriptedPlayback`）です。手書きの規則を持つ旧い `LiveTurn` は、撮影用のシーンが今も使っていますが、使わなくします（#571）。戦闘の規則は BattleCore だけが持ちます。**View が BattleCore の型を持ってよいのは、計算せず受け渡すときだけ**で、View から `TurnLoop` などの規則を呼びません（検査は #572。経緯は `.claude/decisions/2026-10-18-view-does-not-compute.md`）。探索から入るときは、`ExplorationBootstrap` が `Exploration.Bridge` の `Expedition` で戦闘を組んで `BattleBootstrap.HandIn` に渡し、`Battle.unity` を探索のシーンに重ねて読み込みます（#99）。受け渡しの純 C# は `Assets/View/Exploration/Bridge/`（`unity-port/Exploration.Bridge*.csproj`）です。`Script/` `Bridge/` と EditMode テストは `unity-port/Depiction.Script*.csproj` `Depiction.Bridge*.csproj` 経由で `dotnet test` が回す
- **試遊版の Unity 版:** ブラウザの試遊版（Artifact「Original Card Battle 試遊版」、元は `origin/claude/nifty-dirac-a99nv7` の `prototypes/browser-game/`）を、見た目・動き・音ごと `unity-port/unity-project-kit/Assets/View/Trial/` に写した。これを今後のゲームの基本の形とする（経緯は `.claude/decisions/2026-10-22-trial-unity-port-as-base.md`）。タイトル → 継承の間 → 出立 → 探索 ↔ 戦闘 → 終わり → 次の探索者まで、Unity リポの `Assets/Scenes/Trial.unity`（`TrialBoot` を 1 つ置くだけ）で遊べる。写し方の決まり・アセンブリの表・確かめ方は `unity-port/TRIAL-PORT.md` が持つ。見た目と手触りは試遊版の JS が正、規則は C# のコアが正。ただし人型の絵だけは、立ち絵ができた敵と主人公に限り立ち絵が勝ちます（#772。主人公は 2026-11-25 のこうだいさんの決定で加えました。絵のない人型はベクターの影絵のままです。絵は `Resources/Trial/FigureArtShelf` の棚から読み、頭の頂から靴底までを人型の高さに合わせます。経緯は `.claude/decisions/2026-11-01-trial-standing-portraits.md`）。戦闘の型・守り・移動の動きも、Unity では JS より 0.2〜0.4 s 遅くします（長さは `View/Trial/Battle/Flow/BattlePacing.cs` だけが持ちます。経緯は `.claude/decisions/2026-11-02-trial-battle-pacing.md`）。階層の背景も JS と変えています。層ごとに色を分け、コードで描く遠景（`Backdrop/Script/BackdropScenery.cs`）と星などの粒の組を足しました（#89。値は `BackdropSpec.cs` が持ちます。経緯は `.claude/decisions/2026-11-03-trial-layer-backdrops.md`）。探索の地図と出立の画面も JS と変えています（2026-11-25 のこうだいさんの決定、#1044）。第 1〜6 層の地図は 7〜9 行で、行の数に合わせてノードを縮めます（#1049。大きさは `View/Exploration/Script/MapLayout.cs`）。第 1 層の地図だけは、部屋を空洞、道を通路として描く「層の断面」の形です（#1050。値は `View/Exploration/Script/MapStrata.cs`）。出立は 2 段をやめて「支度の卓」の 1 画面にしました（#1051。配置は `View/Trial/Bridge/DepartTable.cs`）。同じ 1 人が潜り直す形の拠点は、継承の間の代わりに「降り口の前」の場面を出し、「降りる」を押し続けて出立します（#1052。画面は `View/Trial/Meta/GateScene.cs`、配置は `View/Trial/Bridge/DepartGate.cs`）。最初の探索は、最初期の札 11 種を持って始まります。最初の束は、そのうち 10 種（スタンス以外の 9 種と水の構え）を 2 枚ずつ積んだ 20 枚です（#1038）。見た目の値は `docs/art_document/battle-visual-v1.md` §2.5 と §4.10 が持ちます。JS にしか無かった規則（継承 `InheritanceCore`、出立の枠 `DeckLimits`、探索の持ち物とイベント、手記の敵の頁、複数の敵、俊敏の一歩）は opt-in で C# に入れ、外したときの動きは従来の C# のまま。旧い `View/Depiction` `View/Exploration` の画面は触っていない（`Battle.unity` はそのまま動く）
- **凍結（経緯は `.claude/decisions/2026-09-12-battle-core-in-csharp.md`）:** アーマー（AP / 装備耐久 / 修理）、装備・Gold・ソウル経済、ショップ / 鍛冶屋 / サンクチュアリ。防御は Guard のみ

## Testing

Tests live in `__tests__/` subdirectories adjacent to source files (e.g., `src/domain/cards/decks/__tests__/deck.test.ts`).

**Pattern:** Pure function unit tests with `describe/it/expect`. Use `vi.spyOn(Math, 'random')` for deterministic shuffles. Create minimal fixtures via helper functions (e.g., `createTestCard()`).

## Skills Quick Reference

| Task | Skill |
| --- | --- |
| Add new card | `card-creator` |
| Add new enemy | `enemy-creator` |
| Add character class | `character-class-creator` |
| UI/UX work | `ui-ux-creator` |
| Character art / UI production pipeline | `visual-production-pipeline` |
| 前のセッションの続きを引き継ぐ | `session-successor` |
| セッション開始 / `/clear` の後 | `session-loader` |
| 次に着手する Issue を選んで実装・確認・PR まで回す（main だけ。設定は `.claude/issue-workflow.md`） | `issue-workflow`（グローバル） |

## Development Workflows

> 手順はスキルへ委譲し、本節は本リポの規約だけを持つ。この節の仕組みは life-editor の開発環境を移したものです（経緯は `.claude/decisions/2026-09-20-life-editor-workflow.md`）。

### 課題追跡は GitHub Issues が正

- **Issue を立てるのは chat-main だけ**（#367。経緯は `.claude/decisions/2026-10-06-issues-created-by-main-only.md`）。担当の作業場所のチャットは `gh issue create` を打たず、課題を申し送りとして `.claude/comm/handoff/` に 1 件 1 ファイルで書いて先へ進む。main が全 worktree の申し送りを回収し、重複を確かめて `issue-dispatch` で Issue にし、処理したファイルを消す。担当に許すのは自分宛て Issue へのコメントと PR の `Closes #n` だけ。共通の hook（`bash-guards.mjs`）が、設定の `issueCreation` が `main-only` なのを読んで、main 以外の `gh issue create` を止める。手順と書式は `.claude/comm/README.md`
- **起票先**: `gh issue create -R sunbreak-pro/original-card-battle`（main だけ）。テンプレートは `.github/ISSUE_TEMPLATE/` の 3 種（Known Issue / Roadmap Item / Human Task）
- **ラベル**: `type:` (bug / feature / task / human) × `prio:` (1 / 2 / 3 / 4) × `sev:` (blocking / important / minor) × `area:` (battle / cards / enemy / dungeon / world / ui / art / unity / docs / tooling) × `status:` (monitoring / workaround / frozen)
- **スコープの境界**: Issue は**プロダクトの課題専用**。Claude Code 環境やハーネス起因の問題は `docs/known-issues/` に置き、Issue にしない
- **優先順位は必須**: Issue には `prio:1`〜`prio:4` を必ず 1 つ付ける（1 = いま着手 / 2 = 次 / 3 = その後 / 4 = いつか）。`sev:` は影響の大きさ、`prio:` は着手の順番で、別の軸。次にやることは `npm run issues:next`（`prio` → `sev` → 番号の順。依存先が open の Issue は待ちに回る）。付け忘れは `npm run issues:next -- --check` が検出し、`npm run issues:prio -- --apply` が付与と注意のコメントを入れる（Actions では回さず、手元で打つ）。見直すのは親 Issue を閉じたときと、縦切りや束の振り返りのとき
- **着手前に必ず open を見る**: `gh issue list --label type:bug`。重複起票を避ける
- **1 Issue = 1 ブランチ = 1 PR**。PR 本文に `Closes #<n>` を書く
- **merge は共通の決まり（claude-dotfiles の `docs/development-flow.md` の「merge」）に従います。** 条件を満たした PR は、メインのチャットか、`issue-workflow` の仕上げ役が、共通の `merge-round` スキルで入れます。このリポジトリは PR に CI を走らせていません。`issue-workflow` の PR は、`.claude/issue-workflow.md` の「全体確認」が通った記録を検証の条件にします。それ以外の PR は、merge する人が先に `npm run build`・`npm run lint`・`npm run test:run` を打って確かめます。`.claude/settings.json`・`.claude/issue-workflow.md`・`.claude/agents/` などの承認が要るファイルを変える PR は、共通の hook が止め、こうだいさんの確認に回します。担当の作業場所の worktree から打つ merge も、同じ hook が止めます

### 進行役（pdca-harness）の PR とラベルは、起票と merge の決まりの例外

定時に動く進行役（`~/dev/pdca-harness`）が、`pdca:auto` の付いた open Issue を 1 件 1 PR にします（経緯は `.claude/decisions/2026-10-12-pdca-harness-exception.md`）。

- **例外にするもの:** 進行役が作る `pdca/*` ブランチの PR と、`state:*` `goal:*` のラベルです。進行役は Issue を作らず、merge もしません。進行役の PR も、上の merge の決まりで、メインのチャットが入れます
- **`pdca:auto` を付けるのは chat-main だけ**です。こうだいさんの指示があるときか、`.claude/comm/README.md` の「`pdca:auto` を付ける判断」に当たるときに付けます。付いた Issue は、同じ Issue を 2 者が実装しないように、`issue-workflow` で回しません（グローバルの `issue-workflow` スキルが、`pdca:auto` と `state:*` のラベルを見て共通の除外で外します）
- **拾われない Issue:** 本文が `.claude/` のパスを挙げるもの、除外ラベル（`type:human` `area:art` `area:world` `status:frozen` など）が付いたもの、題が判断を求めるもの、子 Issue を持つ親です。進行役は `.claude/` と `.github/` に書けません
- **判断が要るとき:** 進行役は `state:needs-human` を付け、Gmail で知らせます

### 判断の控えは life-editor

GitHub は在庫棚（課題の正確な台帳）、life-editor は献立表（何を作るか決めて記録する場所）。**Issue の代替にはしない**（経緯は `.claude/decisions/2026-09-20-life-editor-workflow.md`）。

- **置くもの**: 判断の控えと調査結果（Note）／次にやること（Todo）。プロジェクトを跨いで見たいものだけを上げる
- **置かないもの**: 課題の状態・担当・PR との結線。ステータスが 2 値しかなく、コメントが無く、後勝ち同期で記録が消えるため、元になる記録は GitHub Issue のまま
- **ローカルの `.claude/memory/` は残す**。per-chat の進捗は従来どおり task-tracker が書く
- **手順は `life-editor-bridge` スキル**。セッション頭に `node ~/.claude/skills/life-editor-bridge/scripts/le.mjs pull`、判断が出たら `note`、次にやることは `todo`
- **タグ**: `proj/original-card-battle` と `開発` が自動で付く。プロジェクト名の元になるファイルは `.claude/life-editor.json`
- **落ちても止まらない**。life-editor は移行中で、失敗したらスクリプトが止まるだけ。作業は先へ進める

### タスクの進捗は per-chat ファイル

並行チャットが 1 つのファイルの同じ行を書き換えると必ず衝突するため、チャットごとにファイルを分ける。

- **自分の名前**: `.claude/comm/.session-name`（git 非追跡）。`chat-` 接頭辞は付けない（例: `main`）
- **進捗**: `.claude/memory/chat-<self>.md` / **履歴**: `.claude/history/chat-<self>.md`
- **横断ビュー**: `.claude/memory/INDEX.md` / `.claude/history/INDEX.md` は **git 非追跡の生成物**。共通の hook（`project-session-start.mjs`）が SessionStart と task-tracker から再生成する。手編集も `git add` もしない
- **更新は task-tracker スキル経由**（手動編集しない）。開始時と完了時に書く中身は、task-tracker スキルが持ちます。計画書の置き場と、終わった計画書を移す先は、共通の `code-plan-editor` スキルに従います
- **tracker の更新を実装コミットに混ぜない**。共通の hook（`bash-guards.mjs`）が止める。意図的に同梱するときだけコマンドに `[tracker-ok]` を含める

### hooks

hook はこのリポジトリに置かず、claude-dotfiles の共通の 2 本を使います。SessionStart の `project-session-start.mjs` が進捗の索引を作り直し、PreToolUse(Bash) の `bash-guards.mjs` が commit・起票・merge を止める判定を当てます。どちらも `.claude/issue-workflow.md` の設定値を読みます。

### 禁止コマンド（`.claude/settings.json` の `deny`）

共通の禁止は、`git-workflow` スキルの「破壊的コマンドのガードレール」に従います。このリポジトリの `deny` は、共通の決まりでは確認だけで済む `git branch -D` も止めます。**merge のコマンドは、settings に allow も ask も置かず、共通の hook が振り分けます**。ask を置くと、hook の allow が効かないためです。

### 並行作業は担当ごとの worktree

> 置き場・作り方・ブランチの切り替え・main の取り込み・merge 済みの判定は、共通の `worktree-policy` スキルに従います。本節は、このリポジトリの担当の区分と決まりだけを持ちます。

worktree はリポジトリの外、設定の `worktreeRoot`（`../workspaces/original-card-battle`）の下の `<slug>` に置く（絶対パスで作る）。**1 つの担当の区分 = 1 worktree = 1 チャット**で、ブランチは Issue ごとに切り替える。

| slug | 担当 | 既定の `area:` | 主に触るパス（目安） |
| --- | --- | --- | --- |
| `cards` | カード設計・デッキ・習熟・敵ロースター | `cards` `enemy` | `src/constants/data/cards/`、`src/domain/cards/`、`docs/*_document/` のカードの章 |
| `design` | 見た目。UI / UX 設計・演出・立ち絵・素材 | `ui` `art` | `src/ui/`、`unity-port/**/View/`、`docs/art_document/`、`briefs/` |
| `battle` | 戦闘プログラム（C# の BattleCore が正） | `battle` `unity` | `unity-port/BattleCore/`、`unity-port/BattleCore.Tests/`、`src/domain/battles/` |
| `dungeon` | 探索プログラム。刻限 / 瘴気 / ノード | `dungeon` | `src/domain/dungeon/`、探索の C# と設計書 |
| `audit` | 監査。設計書と実装の整合、既知課題の棚卸し | `docs` `tooling` | 書かない（下の「読み取り専用」） |

- **宛先ラベルは `lane:<slug>`**。付けなければ `issue-workflow` が上の `area:` から既定の担当を決める（決め方は `.claude/issue-workflow.md` の「選び方」）
- **書いてよい設計書は、元になる文書の一覧が決める**。上の表の「主に触るパス」は目安で、設計書 1 本ごとの持ち主は `docs/SOURCES.md` §2 が決める。表と一覧が食い違えば一覧に従う。main を取り込んだあとに `npm run sources -- --lane <slug>` を打ち、自分が書く設計書と読む設計書を確かめる
- **担当の境目はファイルの持ち主で切る**。敵の由来や呼び名の文章も、置き場が `docs/enemy_document/` なら `cards` が書く。`design` が見た目の約束を変えたいときは、案を Issue に書き、main が `docs/vision/` に取り込む
- **世界観の元になる文書は main（chat-main）が持つ**（経緯は `.claude/decisions/2026-09-21-world-owned-by-main.md`）。正典は `docs/vision/world-v1.md` で、**舞台は龍神の封印の地、敵は竜・寄生・亡霊の 3 系統、崩壊まで約 1 年**（v5、#884）。`docs/vision/` `docs/Overall_document/` `docs/journal_document/` は main が書き、`area:world` の Issue は `lane:` を付けず main のチャットで扱う。敵の数値とロースター（`docs/enemy_document/`）は `cards` のまま
- **`audit` は読み取り専用**。整合監査の結果は申し送りにして main へ回し、修正は main が Issue にして持ち主の担当へ配る。書いてよいのは、自分の tracker（`memory/` `history/`）と `comm/outbox/` `comm/handoff/` だけ。コードの修正を含む Issue には `lane:audit` を付けない
- **待機ブランチは `chore/lane-<slug>`**。担当の作業場所はこのブランチで作り、ここに commit を積まない。Issue に着手するたびに、`<prefix>/<slug>-<issue>` を origin/main から切る
- **続きの PR は積む**。同じ担当が同じ設計書を触る PR を続けて出すときは、2 本目のブランチを merge 待ちの 1 本目から切る。2 本の PR が、設計書の冒頭の版の行と末尾の改訂履歴を同じ位置で書くので、どちらも main から切ると、片方が merge された時点でもう片方が必ずぶつかるためである。宛先と入れ方は、共通の決まり（claude-dotfiles の `docs/development-flow.md` の「merge」の「方式」）に従う。前の PR に直しが入ったら、`git merge <前のブランチ>` で取り込む。触る設計書が重ならない PR は積まない
- **試運転はメインだけ**。`npm run dev`・実ブラウザ検証・Unity Editor での手触り確認はメインで行い、各担当は `npm run build` / `npm run lint` / `npm run test:run` / `dotnet test` の静的検証まで。C# を触ったら、`npm run unity:check` で Unity のコンパイルを Editor を開かずに確かめる。Unity の NUnit 3.5 と netstandard2.1 にだけ無い API は、`dotnet test` では見つからないためである。例外は chat-main の `issue-workflow` の仕上げ役（`work-finishers`）だけで、動作確認のために `wf-*` の worktree で Vite の dev サーバーを 1 本ずつ、固定のポート 5273 で起動し、終わったら止める（`.claude/issue-workflow.md` の「動作確認」）。Unity Editor はメインだけのまま
- **worktree が消えないとき**。`git worktree remove` が `Permission denied` で失敗し、掴んでいるのが Orca のターミナルなら、`orca terminal list --json` でそのターミナルの handle を探し、`orca terminal close --terminal <handle>` で閉じてから消す
- **one writer per artifact**。同じファイルを 2 つの担当に触らせない
- **`.claude/comm/.session-name` と `.session-branch` を必ず書く**。ブランチを切り替えるたびに `.session-branch` を更新する。省略すると、共通の hook が担当の worktree からの merge と起票を止め、セッションの最初に警告を出す

## Document System

- **フロー**: Vision（`docs/vision/core.md`）→ 実装プラン → 完了したら規約を本ファイルへ統合。計画書の置き場と完了後の移し先は、共通の `code-plan-editor` スキルに従います。進め方の決定は決定の記録（`.claude/decisions/`）に置きます。進捗 / 履歴は per-chat（`memory/` `history/`、task-tracker 経由）
- **Known Issue**: `docs/known-issues/` に Root Cause + 再発防止を蓄積。発見時 `NNN-<slug>.md` 作成 + `INDEX.md` 更新、解決時 Status=Fixed。**類似バグはまず `INDEX.md` を grep**
- **元になる文書の一覧は `docs/SOURCES.md`**。主題ごとの元になる文書・持ち主の担当・旧版の一覧・食い違ったときに勝つ側を持つ。他の文書とスキルは同じ表を持たず、ここを指す。元になる文書を足す / 使わなくする / 持ち主を変えるコミットに一覧の行を含め、`npm run sources -- --check` を通す。版を上げるだけなら一覧は触らない（版は元になる文書の冒頭の Status が持つ）
- **Markdown の表は桁を揃えない**。1 マスが伸びるたびに表の全行が書き換わり、並行する PR がぶつかるため（known-issues 003）。`.md` は `.prettierrc.json` の `requirePragma` で prettier の対象から外し、表の空白は `npm run md:tables` で詰める。同じ元になる文書を触る PR を続けて出すときは前の PR の上に積む（上の「並行作業は担当ごとの worktree」）
- **元になる文書どうしの食い違いは直さず、main への申し送りにする**（`kind: conflict`、題は「元になる文書の食い違い: 〜」）。他の担当の元になる文書は書かない。どちらに従って進めたかを申し送りと PR 本文に 1 行で残す。Issue にするのは main。手順は `docs/SOURCES.md` §5
- **決定は設計書の本文へ入れ込む**。末尾の「〜の決定」節は記録で、入れ込むまでは、元になる文書の一覧の状態の欄に「未反映」と書く
- **パスの読み方**: 本ファイルの `docs/…` は `.claude/docs/…` を指す。リポジトリ直下の `docs/`（`reports/` `briefs/` `mockups/` `prompts/`）は HTML レポートと制作物の置き場で、元になる文書は置かない
- **設計書 vs 実装**: ゲーム数値は `docs/*_document/` の設計書を正とし、差分は設計書側か実装側へ寄せて解消

## References

| Resource | Contents |
| --- | --- |
| `memory/chat-<self>.md` | タスクトラッカー — 進行中 / 直近の完了 / 予定（per-chat） |
| `history/chat-<self>.md` | 変更履歴（降順、概要+変更点。per-chat） |
| `.github/ISSUE_TEMPLATE/` | Issue テンプレート 3 種（Known Issue / Roadmap Item / Human Task） |
| `README.md` | プロジェクト概要 |
| `.claude/decisions/` | 進め方の決定の記録（1 件 1 ファイル。ゲームの設計の決定は設計書の本文） |
| `.claude/docs/SOURCES.md` | 元になる文書の一覧（主題 → 元になる文書 → 持ち主の担当、旧版、食い違いの手順） |
| `.claude/docs/INDEX.md` | ドキュメント索引（標準構造 + ゲーム設計書） |
| `.claude/docs/vision/core.md` | Vision・設計原則 |
| `.claude/docs/*_document/` | Game design specs (battle, cards, camps, dungeon, enemies, items, journal) |
| `.claude/docs/code-explanation/` | Code analysis, vulnerability tracker, testing analysis |
| `.claude/docs/vision/plans/` | 計画書。進行中の計画と、保留中の将来機能（title・NPC・dark market・PixiJS）を置きます |
| `.claude/archive/` | 終えた計画書と過去の資料 |
| `.claude/docs/known-issues/` | Root Cause + 再発防止知見（INDEX + LESSONS_LEARNED） |
| `.claude/skills/` | プロジェクト固有スキル（Skills Quick Reference 参照） |
