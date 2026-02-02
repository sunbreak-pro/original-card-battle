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

テストフレームワークは未導入。ブラウザで手動確認。

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

### キャラクタークラス（3種）

| クラス | 固有メカニクス | カード枚数 |
|--------|--------------|-----------|
| 剣士 (Swordsman) | 剣気ゲージ（エネルギー蓄積で強力な技を発動） | 全33枚 |
| 魔術師 (Mage) | 属性共鳴（属性連鎖でダメージ倍率上昇+フィールドバフ） | 全40枚 |
| 召喚士 (Summoner) | 召喚システム（最大3体の召喚獣、絆レベルで強化） | 全40枚 |

スターターデッキは各クラス15枚。

### 属性システム

| カテゴリ | 属性 |
|---------|------|
| 魔法系 | fire, ice, lightning, dark, light |
| 物理系 | physics, guard |
| 召喚系 | summon, enhance, sacrifice |
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

### キャンプ施設（6種）

| 施設 | 機能 |
|------|------|
| ショップ (Shop) | アイテム購入・売却・魔石交換（在庫管理・日替わり入荷・商人の手形） |
| 鍛冶屋 (Blacksmith) | 装備強化・分解 |
| 聖域 (Sanctuary) | ソウルによるスキルツリー解放（HP/ゴールド/ユーティリティ/クラス/探索） |
| 図書館 (Library) | カード図鑑（タグ/コスト/未解放フィルタ）・敵図鑑・カード派生ツリー・ゲームTips |
| 倉庫 (Storage) | アイテム・装備管理 |
| ギルド (Guild) | 試験バトル・デイリー/ウィークリークエスト・噂バフ購入 |

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

### バトルシステムフロー

```
BattleScreen → useBattleOrchestrator → useBattleState
                    ↓
    getInitialDeckCounts() → getCardDataByClass() → createInitialDeck()
                    ↓
    playerPhaseExecution / enemyPhaseExecution → damageCalculation
```

クラス別バトルフック（`useBattleOrchestrator` 内で統合）:
- Swordsman: `useSwordEnergy()` — エネルギーゲージ（`useClassAbility.ts`）
- Mage: `useElementalChain()` — 属性共鳴コンボ + ダメージ倍率（`useElementalChain.ts`）
- Summoner: `useSummonSystem()` — 召喚獣の生成・減衰・消滅（`useSummonSystem.ts`）

### ショップデータフロー

```
ShopListing (typeIdのみ) → ConsumableItemData (名前・価格・効果) → generateConsumableFromData() → Item
```

`ConsumableItemData.ts` が消耗品の単一データソース。

### ショップ在庫管理

```
ShopStockState (PlayerContext.progression) → shopStockLogic.ts → BuyTab.tsx
  ├── 常設品（7種、固定在庫）
  ├── 日替わり特売（3枠、重み付きランダム抽選）
  ├── エピック枠（Depth依存 5-20%で出現）
  └── 装備品（日替わりローテーション）
```

- 7-10戦闘ごとに自動入荷（`incrementBattleCount` → `hasNewStock` フラグ）
- 商人の手形で強制入荷（`forceRestock`）
- ベースキャンプのショップカードに入荷通知バッジ表示

## Implementation Status

| カテゴリ | 進捗 | 備考 |
|---------|------|------|
| バトルシステム | 98% | コア、複数敵、逃走、属性共鳴、マルチヒット完了。AoEカード未実装 |
| キャンプ施設 | 98% | 全6施設稼働。ショップ在庫管理＋図鑑フィルタ強化済 |
| ダンジョン | 90% | マップ、ノード、イベント、5フロア進行、Depth 1-5 |
| 進行システム | 98% | ライフ、ソウル、聖域、装備耐久度、熟練度、カード派生、カスタムデッキ |
| セーブ | 実装済 | `src/domain/save/logic/saveManager.ts` |
| キャラ画像 | 90% | プレイヤー画像バトル表示。敵imagePath全設定済（画像ファイル未作成） |

### 未実装

- AoEカード（全敵同時ダメージ）
- EnemyFrame の SVG アイコン化（現在は絵文字）
- 敵画像アセット（全50体の imagePath は設定済、PNGファイル未作成）
- 召喚士キャラクター画像（Summoner.png 存在するが内容は仮）

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

<details>
<summary>開発ログ（クリックで展開）</summary>

### 2024年11月27日
- GitHub連携

### 2025年1月20日〜23日
- Claude Max プラン加入

### 2025年1月25日
- README.md の記録開始

### 2025年1月26日
- デッキシステム統合（PlayerContext → バトルへのデータフロー確立）
- 魔術師クラス選択可能化
- Phase 1: Buff/Debuff Ownership System 実装
- MEMORY.md 整理、LESSONS_LEARNED.md 分離
- Claude 開発スキル作成（9個）

### 2025年1月28日
- MEMORY.md とコードの差分調査・修正
- 未使用コード調査（デッドファイル10個特定）
- ShopItem 削除 → ShopListing 置換
- 型定義リファクタリング（`src/types/` に集約、パスエイリアス導入）

### 2025年1月29日
- Phase B 完了（召喚士クラス、ショップ、カード派生、逃走システム等）
- Phase C 開始（探索準備画面、複数敵バトル、図書館、ダンジョンイベント等）
- 画像アセット再構成

### 2025年1月30日
- Phase C ほぼ完了（C1-C5 + B4 全完了）
- 属性システム拡張（6新属性追加）
- 全カードにelement追加

### 2025年1月31日
- slash/impact 属性を physics に統合
- カードデータを `src/constants/data/cards/` に移行
- 探索準備画面（preparations/）追加
- テレポートストーンアイテム実装

### 2026年2月1日
- バトルコンテキスト簡素化（BattleProviderStack/Player/Enemy/SessionContext 削除）
- キャンプデータ移行（`domain/camps/data/` → `constants/data/camps/`）
- 敵データ移行（`domain/characters/enemy/data/` → `constants/data/characters/enemy/`）
- typeConverters.ts 削除（未使用）
- 属性共鳴ダメージ倍率をカード実行に統合
- ElementalResonanceDisplay UI（Mage クラス）追加
- マルチヒットカード実行ループ実装（hit毎のダメージ計算）
- 装備耐久度システム（equipmentStats.ts）追加
- カスタムデッキ対応（PlayerContext の deckCards）
- プレイヤーキャラクター画像をバトルフィールドに表示
- 全50体の敵に imagePath フィールド追加
- soulSystem.ts デッドコード削除
- nav機能追加（BackToCampButton, FacilityTabNav コンポーネント）
- UI改善（FacilityHeader統合、キャンプ施設の統一ナビゲーション）
- 脆弱性修正セッション1-4（バトルステールクロージャ修正、バトルロジック改善）
- UI統合提案（CONSOLIDATION_PROPOSAL.md）作成

### 2026年2月2日
- 脆弱性修正セッション5完了（DoT/スタック倍率修正、出血・毒・火傷がスタック数で倍化）
- 脆弱性修正セッション6完了（クラス＆カードメカニクス修正）
- カードシステムリファクタ完了（category/rarity廃止、ElementType拡張）
- 脆弱性修正セッション7完了（経済＆ダンジョン修正、4件）
  - **鍛冶屋の魔石未消費バグ修正**: `spendBaseCampMagicStones()` を ResourceContext に追加、UpgradeTab でゴールドと魔石の両方を消費するよう修正
  - **ショップローテーション保存**: `shopRotationDay` を PlayerProgression/セーブデータに追加、save/load でラインナップが維持されるように
  - **フォールバック攻撃スケーリング**: 敵のフォールバック攻撃が固定5ダメージ→AIパターン平均の50%にスケーリング
  - **装備耐久度バトル中減算**: 既に実装済みであることを確認（onApDamage コールバック経由）

### 2026年2月3日
- **Phase D: ショップ在庫管理 + カード図鑑UIオーバーホール（全3セッション）**
- ショップ在庫コアシステム実装
  - `ShopStockState` 型 + `PlayerContext.progression` 経由で永続化
  - `ShopStockConstants.ts`（常設品7種、日替わりプール7種、エピックプール2種）
  - `shopStockLogic.ts`（初期化・在庫減少・入荷・戦闘カウント・商人の手形）
  - `ConsumableItemData` に `shopPrice` 追加（13アイテム）
- ショップUI + 入荷通知
  - BuyTab を3セクション構成にリライト（常設品・日替わり特売・装備品）
  - 在庫バッジ（残り数表示）+ 売切表示
  - 入荷バナー（「新商品入荷！」）+ ベースキャンプ通知バッジ
  - `NodeMap.tsx` の戦闘勝利時に戦闘カウンターをインクリメント
- カード図鑑UIオーバーホール
  - `CARD_TAG_LABEL_MAP` / `CARD_TAG_COLOR_MAP` 追加
  - フィルタ拡張: タグ・コスト範囲・未解放カード表示トグル
  - 統計バー: 解放率 + タグ別カウント（色付き）
  - 未解放カード「?」プレースホルダー表示
  - `CardDerivationTree` に `unlockedCardTypeIds` 連携
- **脆弱性修正セッション8完了**（デッドコード＆重複削除、11件）
  - 重複関数削除（`calculateCardEffect`、`decreaseBuffDebuffDuration`）
  - レガシーエイリアス削除（useBattleOrchestrator return object）
  - 重複定数統合（`SWORD_ENERGY_MAX`、`DEFAULT_DAMAGE_MODIFIER`）
  - バトル状態デュアルAPI統合、インベントリ重複方向マージ
  - クラスアビリティフック共通ラッパー抽出、敵ガードチェック共有述語
- **脆弱性修正セッション9完了**（命名＆ファイル整理、6件）
  - `DungeonRunContext.tsx` を `src/contexts/` に移動
  - ファイルリネーム: `deptManager` → `depthManager`、`tittle` → `title`、`swordmanCards` → `swordsmanCards`
  - テストデータ削除（`TestItemsData.ts`）、初期値をゼロデフォルトに変更

</details>

## References

- `.claude/MEMORY.md` — 現在のステータスと既知のバグ
- `.claude/LESSONS_LEARNED.md` — 開発で学んだ重要な教訓
- `.claude/todos/MASTER_IMPLEMENTATION_PLAN.md` — 実装ロードマップ
- `.claude/docs/` — ゲーム設計仕様書（バトル、カード、キャンプ、ダンジョン、敵、アイテム）
- `.claude/code_overview/` — コード静的分析ドキュメント（14ファイル、77件の脆弱性特定済、52件修正済）
