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
| 剣士 (Swordsman) | 剣気ゲージ（スキル/ガードで蓄積→攻撃で消費） | 全60枚 |
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
| ギルド (Guild) | 昇格試験・噂バフ・依頼・**倉庫**（アイテム・装備管理）の4タブ |
| ショップ (Shop) | アイテム購入・売却・魔石交換・**闇市場**（在庫管理・日替わり入荷・ボス討伐で闇市場更新） |
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
GameStateProvider → ResourceProvider → PlayerProvider → InventoryProvider → DungeonRunProvider → GuildProvider
```

バトル状態は `useBattleOrchestrator` フックが直接管理（別途Contextなし）。

## Implementation Status

| カテゴリ | 進捗 | 備考 |
|---------|------|------|
| バトルシステム | 98% | コア、複数敵、逃走、属性共鳴、マルチヒット完了。AoEカード未実装 |
| キャンプ施設 | 100% | 全5施設稼働（ショップ、ギルド[含倉庫+依頼]、鍛冶屋、聖域、ダンジョンゲート） |
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
| 2026-02-10 | Quest System実装: GuildProviderをApp.tsxプロバイダー階層に追加、QuestsTabをGuildContext連携に書き換え（ローカルstate→Context）、BattleScreenに敵討伐クエスト進捗追跡統合（defeat/elite/boss）、NodeMapに探索・宝箱・生存クエスト進捗追跡統合、クエストデータ全テンプレート日本語化（デイリー8種+ウィークリー5種）、報酬受取トースト通知、期限切れクエストCSS追加 | 完了 |
| 2026-02-07 | Journal戦術・記憶タブ改善: DeckTab全4タグ常時表示(0枚placeholder)、CardAddModalのcount+1サイクル方式+count-tagバッジ、DeckTab複数選択一括削除ボタン、MemoriesPage記憶タブをクラスフィルタ(プレイヤークラスのカードのみ表示)、カード3分類セクション(基礎/派生/才能)追加、デッキ更新時discoverCard自動呼出し統合 | 完了 |
| 2026-02-06 | バグ修正+UI一新: swordEnergyバグ修正(executePlayerPhase内onTurnStart追加)、Sanctuary currentRunSouls表示削除、デッキエディタ4タイプ別グリッド化(DeckTab.tsx)、カード追加モーダル(CardAddModal.tsx新規)、ダンジョン探索2パネルUI(ExplorationScreen+左パネル:ステータス/アイテム/ジャーナルタブ+右マップ)、探索中アイテム使用(useExplorationItemUsage.ts)、探索中Journal表示(DeckReadOnlyView showEditButton prop追加) | 完了 |
| 2026-02-06 | ニューゲームボタン+Continueボタンバグ修正: SettingsPage(Journal内)にニューゲームボタン追加(確認ダイアログ→Journal閉じる→character_select遷移)、SaveLoadUIからロードボタン削除、VictoryScreen.cssの.continue-buttonを.victory-screenでスコープ修正(CharacterSelectのContinueボタンがopacity:0で非表示だったバグ修正)、Settings.cssにnewgame-btnスタイル追加 | 完了 |
| 2026-02-06 | Journal Phase 3-5: Settings統合(SettingsPage.tsx、Settings.css作成、SaveLoadUIにloadDisabled prop追加)、Notes実装(NoteEditor.tsx、ThoughtsPage.tsx、Notes.css、JournalAnimations.css作成、20件上限ガード)、Library完全削除(Library/6ファイル+CSS削除、App.tsx・campConstants.ts・campTypes.ts・types/index.tsからlibrary参照除去)、FacilityHeaderから⚙️ボタン+SettingsModal削除 | 完了 |
| 2026-02-06 | Journal Phase 2 (Memories Page): 図鑑ページ実装。EquipmentEncyclopediaData.ts/EventEncyclopediaData.ts新規作成、MemoriesPage.tsx (カテゴリタブ: カード/敵/装備/イベント、検索・フィルタ・進捗表示)、Memories.css。JournalOverlay.tsx更新 | 完了 |
| 2026-02-06 | 共通カード・剣気システム修正: 共通カード20枚完全削除(commonCards.ts削除、CardCharacterClass型から"common"除去)、剣気消費カード使用制限(canPlayCard関数にswordEnergy判定追加、BattleScreen UI反映)、ターン開始時剣気+1(SwordEnergySystem.onTurnStart修正) | 完了 |
| 2026-02-05 | Dark Market（闘市場）実装: Shopの4番目タブとして追加。高価格（×1.8）でレア以上の装備・エピック消耗品を販売、ボス討伐後に在庫更新。DarkMarketTab.tsx、DarkMarketConstants.ts新規、campTypes.ts/campConstants.ts/shopStockLogic.ts拡張、BattleScreen.tsx連携 | 完了 |
| 2026-02-05 | カード図鑑UI改修（3分類システム）: CardEncyclopediaTab.tsx大幅改修、CardCategoryRow.tsx新規、cardClassification.ts/talentCardUnlock.ts新規、talentCardRegistry.ts新規。才能カード8枚(sw_027-034)にisTalentCard追加、派生解放Lv2→Lv1変更、3行レイアウトCSS追加 | 完了 |
| 2026-02-05 | Storage機能のGuild統合: 倉庫を独立施設からギルドのタブ（4つ目）に統合。StorageTab.tsx新規作成、Guild.css統合、App.tsx/campConstants.tsからStorage関連削除 | 完了 |
| 2026-02-05 | Swordsmanカードバランス調整: 剣気サイクル根本変更(攻撃=獲得→消費、スキル/ガード=獲得強化)、新規カード4枚(sw_044-047)、派生カード15枚追加、初期デッキ構成変更(攻撃6/スキル6/ガード3の15枚)。全60枚 | 完了 |
| 2026-02-06 | タスク管理ハイブリッド化: TODO.md新規作成（バックログ一覧+仕様リンク）、CLAUDE.md/README.mdのTask Completion Rule・References更新、current_plans/削除 | 完了 |
| 2026-02-05 | UI/UXデザインガイド作成: `.claude/docs/ui_ux_design_guide.md` - 設計哲学、カラーシステム（施設/ステータス/熟練度）、タイポグラフィ、スペーシング、コンポーネントパターン、アニメーション、アクセシビリティ | 完了 |
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

- `TODO.md` — タスク一覧（優先度、ステータス、仕様リンク）
- `.claude/docs/` — ゲーム設計仕様書（バトル、カード、キャンプ、ダンジョン、敵、アイテム、手記、UI/UXデザインガイド）
- `.claude/feature_plans/` — 機能の詳細仕様（クエスト、称号、NPC会話、闇市）
- `.claude/memories/` — 開発で学んだ教訓、完了したリファクタリングガイド
