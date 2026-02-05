# Original Card Battle

オクトパストラベラー、Slay the Spire からインスピレーションを受けたターン制カードバトルRPG。
TypeScript + React を学びながら Claude Code と共に開発中。

## Tech Stack

| 技術 | バージョン |
|------|-----------|
| React | 19.2 |
| TypeScript | 5.9 |
| Vite | 7 |
| Node.js | LTS |

## Getting Started

```bash
npm install
npm run dev       # localhost:5173 で起動
npm run build     # TypeScript チェック + プロダクションビルド
npm run preview   # ビルド結果をプレビュー
npm run lint      # ESLint
```

テスト: `npm run test` / `npm run test:run` / `npm run test:coverage`（Vitest 4.0.18）

## Game Overview

### ゲームループ

```
キャラクター選択 → ベースキャンプ → ダンジョン探索 → バトル → 帰還
                      ↑                                    ↓
                      ←────── ソウル・アイテム獲得 ←────────┘
```

- **生存時:** ソウル + アイテムを全て持ち帰り
- **死亡時:** アイテムロスト + ライフ -1（ソウルは100%保存）
- **ライフ 0:** ゲームオーバー（フルリセット）

### キャラクタークラス（2種）

| クラス | 固有メカニクス | カード枚数 |
|--------|--------------|-----------|
| 剣士 (Swordsman) | 剣気ゲージ（エネルギー蓄積で強力な技を発動） | 全41枚 |
| 魔術師 (Mage) | 属性共鳴（属性連鎖でダメージ倍率上昇+フィールドバフ） | 全40枚 |

スターターデッキは各クラス15枚。

### 属性システム

| カテゴリ | 属性 |
|---------|------|
| 魔法系 | fire, ice, lightning, dark, light |
| 物理系 | physics, guard |
| ユーティリティ系 | buff, debuff, heal |

### バトルシステム

- ターン制カードバトル（コスト=AP消費）
- バフ/デバフオーナーシップ（付与者のフェーズでのみ持続時間減少）
- 複数敵バトル対応（ターゲット選択 + 自動リターゲット）
- 逃走システム（ボス戦では無効）
- 速度計算によるターン順序決定

### ダンジョン（5階層 × 5フロア）

| Depth | 名前 | 難易度 |
|-------|------|--------|
| 1 | 腐食 (Corruption) | 入門 |
| 2 | 狂乱 (Madness) | 中級 |
| 3 | 混沌 (Chaos) | 上級 |
| 4 | 虚無 (Void) | 高難度 |
| 5 | 深淵 (Abyss) | 最高難度 |

ノードタイプ: バトル / エリート / ボス / イベント / 休憩 / 宝箱

### キャンプ施設（5種 + ヘッダーUI）

| 施設 | 機能 |
|------|------|
| ギルド (Guild) | キャラ選択・昇格試験・噂バフ + **倉庫タブ**（アイテム・装備管理） |
| ショップ (Shop) | アイテム購入・売却・魔石交換（在庫管理・日替わり入荷） |
| 鍛冶屋 (Blacksmith) | 装備強化・修理・分解 |
| 聖域 (Sanctuary) | ソウルによるスキルツリー解放（HP/ゴールド/ユーティリティ/クラス） |
| ダンジョンゲート | 深度選択・出発 |
| **手記 (Journal)** | ヘッダーUI：デッキ編成・図鑑・攻略メモ・設定 |

### 進行システム

- **ライフ制:** 死亡でライフ消費、0でゲームオーバー
- **ソウル:** ダンジョンで獲得、聖域のスキルツリー解放に使用
- **カード熟練度:** 使用回数で派生カード解放
- **装備:** 6スロット × 5レアリティのステータスボーナス

## Architecture

```
src/
├── types/          # 全型定義（8ファイル + barrel export）
├── constants/      # 定数のみ（型定義なし）
├── contexts/       # React Context providers
│   ├── GameStateContext.tsx    # 画面ルーティング、depth、battleMode
│   ├── ResourceContext.tsx     # ゴールド、魔石
│   ├── PlayerContext.tsx       # キャラデータ + ランタイムバトルステート
│   ├── InventoryContext.tsx    # アイテム、装備、カード
│   ├── DungeonRunContext.tsx   # ダンジョン探索状態（バトル間で保持）
│   └── GuildContext.tsx        # ギルド状態（Guild内で局所利用、App.tsx階層には含まず）
├── domain/         # ビジネスロジック（純粋関数中心）
│   ├── battles/    # バトルシステム（フック、ロジック、計算）
│   ├── camps/      # キャンプ施設ロジック（データは constants/data/camps/）
│   ├── cards/      # デッキ管理、熟練度（カードデータは constants/data/cards/）
│   ├── characters/ # プレイヤー・敵キャラクター（データは constants/data/characters/）
│   ├── dungeon/    # ダンジョン生成・イベント
│   ├── item_equipment/ # アイテム・装備ロジック
│   └── save/       # セーブシステム
├── ui/             # React コンポーネント（画面別）
│   ├── html/           # 画面別コンポーネント
│   │   ├── battleHtml/     # バトル画面
│   │   ├── campsHtml/      # キャンプ施設UI
│   │   ├── dungeonHtml/    # ダンジョンマップ + preparations/
│   │   ├── cardHtml/       # カード表示
│   │   ├── characterSelectHtml/ # キャラ選択
│   │   └── componentsHtml/ # 共通コンポーネント（FacilityHeader, BackToCampButton, FacilityTabNav等）
│   └── css/            # スタイルシート（battle/, card/, camps/, core/, components/, pages/, animations/）
└── utils/          # ユーティリティ
```

### Context Provider 階層

```
GameStateProvider → ResourceProvider → PlayerProvider → InventoryProvider → DungeonRunProvider
```

バトル状態は `useBattleOrchestrator` フックが直接管理（別途Contextなし）。

## Implementation Status

| カテゴリ | 進捗 | 備考 |
|---------|------|------|
| バトルシステム | 98% | コア、複数敵、逃走、属性共鳴、マルチヒット完了。AoEカード未実装 |
| キャンプ施設 | 100% | 全6施設稼働（ショップ、ギルド、鍛冶屋、聖域、図書館、倉庫） |
| ダンジョン | 90% | マップ、ノード、イベント、5フロア進行、Depth 1-5 |
| 進行システム | 98% | ライフ、ソウル、聖域、装備耐久度、熟練度、カード派生、カスタムデッキ |
| セーブ | 実装済 | `src/domain/save/logic/saveManager.ts` |
| キャラ画像 | 90% | プレイヤー画像バトル表示。敵imagePath全設定済（画像ファイル未作成） |

### 未実装

- AoEカード（全敵同時ダメージ）
- EnemyFrame の SVG アイコン化（現在は絵文字）
- 敵画像アセット（全50体の imagePath は設定済、PNGファイル未作成）

## Coding Conventions

| 項目 | ルール |
|------|--------|
| 型名 | `PascalCase` |
| 関数名 | `camelCase` |
| 定数 | `UPPER_SNAKE_CASE` |
| UI テキスト | 日本語 |
| コード・コメント | 英語 |
| CSS サイジング | `vh/vw`（ボーダーのみ `px`） |
| CSS セレクタ | 親要素でスコープ: `.battle-screen .card { }` |
| import | `@/` パスエイリアス使用（`@/domain/...`, `@/types/...`） |
| 型 import | `import type` を使用（`verbatimModuleSyntax` 有効） |

### 変更禁止ファイル

- `src/domain/cards/decks/deck.ts`
- `src/domain/cards/decks/deckReducter.ts`

## Development History

| 日付 | 作業内容 | 進捗 |
|------|----------|------|
| 2026-02-05 | CLAUDE.md更新（タスク完了時アーカイブルール追記）、残存Summoner参照削除（code_overview/character/player.md、battle/class-abilities.md、src/types/characterTypes.ts、classAbilitySystem.ts）、完了タスク3件をarchive/へ移動 | 完了 |
| 2026-02-05 | 脆弱性修正 Phase 1 (Session 1-6): V-EXEC-01/02 (敵マルチアクション死亡後実行・デバフ上書き), V-EXEC-03 (マルチヒットガード/AP割り当て), V-EXEC-04 (敵turnCountインクリメント), V-PHASE-01/02 (敵フェーズバフタイミング), V-DMG-MANAGE-01 (デッドコード削除) | 完了 |
| 2026-02-05 | code_overview整合性修正: Summoner参照削除、CharacterClass 2クラス化、ファイルパス修正、行数更新(PlayerContext 675行等)、カード数修正(41枚→README修正)、useDeckManage.ts削除反映、title.ts/swordsmanCards.tsリネーム反映、Context階層にSettings/Toast追加 | 完了 |
| 2026-02-05 | 統合実装計画 V1.0: Phase 1A (バグ修正: セーブロード、鍛冶屋装備表示、敗北画面リトライ削除、NodeMap HP修正)、Phase 1C (AoEカードサポート)、Phase 1D (EnemyFrame SVGアイコン化)、Phase 2 (Summoner設計書6ファイル削除)、Phase 3 (装備AP警告)、Phase 5 (buffLogicテスト追加: 13テスト) | 完了 |
| 2026-02-05 | テスト分析ドキュメント作成: `.claude/code_overview/testing_analysis.md` - テスト基礎、既存インフラ分析、カバレッジギャップ、推奨アプローチ | 完了 |
| 2026-02-05 | 設計書・実装整合性修正: Soul System 100%転送修正、sanctuary_design.md V3.1ノート削除、shop_design.md Teleport Stone統一(V3.0)、journal_system_implementation_plan.md完全書き直し、exploration_limit→Lives System用語更新 | 完了 |
| 2026-02-04 | 設計書統合: BaseCamp施設7→5に統合、Library→Journal(ヘッダーUI)、Storage→Guild(タブ)、将来機能をfeature_plans/に分離 | 完了 |
| 2026-02-04 | カード設計書・実装整合性修正: 剣士7件値修正+風纏い追加(42→43枚)、魔術師5件値修正、設計書から召喚士記述削除 | 完了 |
| 2026-02-04 | Inn（宿屋）施設削除: 実装スコープ簡素化のため一時削除（将来再実装可能） | 完了 |
| 2026-02-04 | 設計書・実装差分修正: 敵速度計算バグ、クリティカル判定順序、アーマーブレイク、共鳴リセット、ElementType拡張(slash/impact) | 完了 |
| 2026-02-04 | Camp設計書更新: Library廃止マーク、Inn設計書新規作成、Shop/Sanctuary/Blacksmith/Camp実装整合性更新 | 完了 |
| 2026-02-04 | Phase 0: 召喚者クラス完全削除（2クラス版に移行） | 完了 |
| 2026-02-04 | .claude ディレクトリ整理、MEMORY.md削除、README表形式化 | 完了 |
| 2026-02-03 | Phase D: ショップ在庫、カード図鑑UI、Session 8-11 | 完了 |
| 2026-02-03 | Phase E: 宿屋（Inn）施設実装 | 完了 |
| 2026-02-03 | Session 12-24: カード・装備データ追加（73装備完了） | 完了 |
| 2026-02-02 | Session 5-7: 脆弱性修正（DoT/経済/ダンジョン） | 完了 |
| 2026-02-01 | Phase C完了、バトルコンテキスト簡素化、UI改善 | 完了 |
| 2026-02-01 | Session 1-4: 脆弱性修正（バトルロジック） | 完了 |
| 2025-01-31 | 属性統合（slash/impact→physics）、探索準備画面 | 完了 |
| 2025-01-30 | Phase C ほぼ完了、属性システム拡張 | 完了 |
| 2025-01-29 | Phase B完了、Phase C開始 | 完了 |
| 2025-01-28 | 型定義リファクタリング、パスエイリアス導入 | 完了 |
| 2025-01-26 | Phase 1: Buff/Debuff Ownership、開発スキル作成 | 完了 |
| 2025-01-25 | README.md 記録開始 | — |
| 2024-11-27 | GitHub連携 | — |

## References

- `.claude/docs/` — ゲーム設計仕様書（バトル、カード、キャンプ、ダンジョン、敵、アイテム、手記）
- `.claude/feature_plans/` — 将来機能の計画（クエスト、称号、NPC会話、闇市）
- `.claude/current_plans/` — 現在の実装計画・セッション追跡
- `.claude/memories/` — 開発で学んだ教訓、完了したリファクタリングガイド
