# original-card-battle

オリジナルのターン性カードバトル。オクトパストラベラー、slay the spireからインスピレーションをもらい
typeScriptやReactを学びながらclaude codeと共に作成しようと思った。

## 11 月 27 日

git hub に連携した。

### 連携のやり方

まず最初に github からリポジトリを作成(READ.me 作成にチェック)　 1.
その後 Code メニューから中央の「Add file」をクリック。 2.
アップロードかファイル作成を選択 3.
desktop に作成したリポジトリ名と同じフォルダを作成する
VSCode を開く 4.
READ.me があれば OK！ 5.
なければターミナルから [git@github.com:sunbreak-pro/roguelike-card.git] 6.
もしそれでも無理なら AI に聞く 7.

## 1月20日〜23日の間(1月25日執筆)

### claude Maxプランに加入

加入した理由として、制限が早く使いずらい。計算したら五倍の値段で五倍の使用制限＋追加の出力向上＋より真剣に作成に取り組めるようになると仮定
以上の三つが理由。

## 1月25日

README.mdを描く習慣が全くなく、今更になって放棄していたことを忘れた。
手書きで書くのではなく、AIにプログラミング限定の日記をつけてもらうことを徹底するようにする(MEMORY.mdに指示済み)

## 1月26日

### デッキシステム統合を実装

PlayerContextで設定されたデッキがバトルで正しく使用されるようにデータフローを統合した。

**変更点:**

- `initialDeckConfig.ts`: クラス別デッキ設定(`INITIAL_DECK_BY_CLASS`)を追加。剣士は20枚から15枚に調整。
- `useBattleState.ts`: `InitialPlayerState`に`deckConfig`フィールドを追加。
- `BattleScreen.tsx`: ハードコードされた`Swordman_Status`を`playerData.persistent`参照に変更。
- `useBattleOrchestrator.ts`: `getCardDataByClass()`ヘルパーを追加し、クラス別デッキ生成に対応。
- `CharacterClassData.ts`: `getStarterDeckStacks()`関数を追加（キャラ選択画面用）。

**新しい剣士デッキ構成（15枚）:**

- sw_001 x3 (迅雷斬)
- sw_003 x2 (連撃)
- sw_007 x2 (斬りつける)
- sw_013 x2 (剣気集中)
- sw_037 x2 (剣気円盾)
- sw_039 x2 (不屈の精神)
- sw_014 x2 (瞑想)

### キャラクター選択画面のカード表示改善

スターターデッキのプレビューで、各カードタイプを1枚ずつのみ表示するように変更。

- 変更前: 15枚全て表示（同じカードが複数表示される）
- 変更後: 7種類のユニークカードを1枚ずつ表示
- ヘッダーには「15 cards」と総枚数を維持

### 魔術師クラスを選択可能に

魔術師(Mage)がキャラクター選択画面で選択可能になった。

**変更点:**

- `CharacterClassData.ts`: `MAGE_CARDS`インポート追加、`createMageStarterDeck()`関数作成
- `getCardDataByClass()`: mageケースで`MAGE_CARDS`を返すよう更新
- 魔術師エントリ: `isAvailable: true`、固有メカニクス「Elemental Resonance」に更新

**魔術師スターターデッキ構成（15枚）作成:**

- mg_001 x3 (火球) - 火属性基本攻撃
- mg_008 x2 (炎の矢) - 0コスト火属性
- mg_009 x2 (氷結) - 氷属性基本攻撃
- mg_017 x2 (雷撃) - 雷属性基本攻撃
- mg_033 x2 (光の槍) - 光属性基本攻撃
- mg_007 x2 (炎の壁) - 火属性ガード
- mg_037 x2 (癒しの光) - 光属性回復

### Phase 1: Buff/Debuff Ownership System 実装

バフ・デバフの持続時間減少タイミングの不具合を修正。

**問題点:**

- 敵がプレイヤーに「毒（持続3ターン）」を付与
- プレイヤーフェーズ開始時に持続時間が2に減少（間違い！）
- 正しくは敵フェーズ開始時に減少すべき

**修正内容:**

- `BuffOwner`型を追加（`'player' | 'enemy' | 'environment'`）
- `BuffDebuffState`に`appliedBy`フィールドを追加
- 新関数`decreaseBuffDebuffDurationForPhase(map, currentActor)`を実装
- 旧関数`decreaseBuffDebuffDuration()`を非推奨化
- `enemyPhaseExecution.ts`のパラメータ順序バグも修正

**変更ファイル:**

- `baffType.ts` - 型定義追加
- `buffLogic.ts` - 新しい持続時間減少ロジック
- `playerPhaseExecution.ts` - プレイヤーフェーズで'player'を指定
- `enemyPhaseExecution.ts` - 敵フェーズで'enemy'を指定、パラメータ順序修正
- `useCardExecution.ts` - カード効果に'player'を指定

### MEMORY.mdのCritical Lessons Learnedセクションを整理

コンテキストサイズ削減のため、詳細説明を別ファイルに分離。

**変更内容:**

- `.claude/LESSONS_LEARNED.md`を新規作成 - 詳細な説明、コード例、エラーメッセージを含む
- `.claude/MEMORY.md`の「Critical Lessons Learned」セクションをコンパクトなテーブル形式に変更（約30行→約10行）

**テーブル形式に含まれる5つの教訓:**
| 問題 | ルール |
|------|--------|
| CSS Class Collision | 親要素でスコープ: `.battle-screen .card {}` |
| Context Provider Scope | 画面間で状態を維持 → providerをツリー上位に配置 |
| React Hooks | トップレベルで呼び出し、条件付きreturnの前に |
| React 19 Refs | render中に`ref.current`使用不可 → `useState`を使用 |
| Language | UI: 日本語 / コード: 英語 |

### Claude開発スキルの作成

ゲーム開発を効率化するためのClaude Skills（9個）を作成。

**作成したスキル:**
| スキル名 | 用途 |
|---------|------|
| `card-creator` | 新カードの追加 |
| `enemy-creator` | 新敵キャラの追加 |
| `character-class-creator` | 新クラスの実装 |
| `battle-system` | バトルシステムの修正・追加 |
| `camp-facility` | キャンプ施設の機能追加 |
| `dungeon-system` | ダンジョン探索システムの修正 |
| `design-research` | 設計ドキュメントの検索 |
| `ui-ux-creator` | UI/UXデザインガイドライン |
| `debugging-error-prevention` | デバッグとエラー防止 |

**スキルの内容:**

- 説明文は英語、ゲーム内テキスト例は日本語を維持
- 各スキルにワークフロー、コード例、参照ファイル一覧を含む
- `debugging-error-prevention`には React 19 のベストプラクティスを反映

**配置場所:** `.claude/skill/`

## 1月28日

### MEMORY.mdと現在のコードの差分調査

MEMORY.mdに記載された情報と実際のコードベースを比較し、以下の差異を確認した。

#### MEMORY.mdに未記載の変更

| 変更内容                   | 詳細                                                                                         |
| -------------------------- | -------------------------------------------------------------------------------------------- |
| `EnemyDisplay.tsx`削除     | `EnemyFrame.tsx`と`PlayerFrame.tsx`に分割・リファクタリング                                  |
| `src/constants/`新設       | ドメイン層のデータをミラーする定数レイヤー（31ファイル）                                     |
| バトルコンテキスト分離     | `src/domain/battles/contexts/`に5ファイル新設（BattleProviderStack, BattleSessionContext等） |
| `GuildContext.tsx`新設     | `src/contexts/GuildContext.tsx`（MEMORY.mdでは`src/domain/camps/contexts/`と異なるパス）     |
| `nodeEventLogic.ts`新設    | ダンジョンノードイベント処理（ただし未統合）                                                 |
| `src/context/`ディレクトリ | コンテキストファイルの一部が`src/contexts/`に移動                                            |

#### 非推奨関数の移行が未完了

`decreaseBuffDebuffDuration()`は非推奨化されたが、`phaseLogic.ts`内で依然として使用中。
新関数`decreaseBuffDebuffDurationForPhase()`への移行が必要。

### 未使用・重複コードの調査結果

#### デッドファイル（他ファイルから一切importされていない: 10ファイル）

| ファイル                                            | 内容                                     | 理由                                                                  |
| --------------------------------------------------- | ---------------------------------------- | --------------------------------------------------------------------- |
| `domain/cards/state/CardHandle.ts`                  | `calculateCardEffect()`, `canPlayCard()` | 別実装に置き換え済み                                                  |
| `domain/cards/state/cardPlayLogic.ts`               | `calculateSwordEnergyGuard()`            | 別実装に置き換え済み                                                  |
| `domain/cards/data/summonerCards.ts`                | 空のスタブ                               | 召喚士クラス未実装                                                    |
| ~~`domain/dungeon/logic/nodeEventLogic.ts`~~        | ノードイベント処理                       | **復活 (1/28)** import修正、ConsumableItemData参照に変更              |
| ~~`domain/item_equipment/logic/generateItem.ts`~~   | アイテム生成                             | **復活 (1/28)** `generateConsumableFromData()`でshopLogic等から使用中 |
| `domain/battles/managements/damageManage.ts`        | ダメージ管理ラッパー                     | ロジックが直接呼び出しに変更                                          |
| `domain/battles/managements/useDeckManage.ts`       | デッキ管理                               | 参照なし                                                              |
| `domain/characters/utils/typeConverters.ts`         | 型変換ユーティリティ                     | 参照なし                                                              |
| `domain/characters/player/logic/summonSystem.ts`    | 召喚システム                             | 召喚士クラス未実装                                                    |
| `domain/characters/player/logic/elementalSystem.ts` | 属性共鳴システム                         | 魔術師の戦闘に未統合                                                  |

#### 未使用エクスポート

| ファイル                       | 関数名          | 備考                                                        |
| ------------------------------ | --------------- | ----------------------------------------------------------- |
| `battles/logic/battleLogic.ts` | `applyDamage()` | `damageCalculation.ts`の`applyDamageAllocation()`に置き換え |
| `battles/logic/battleLogic.ts` | `applyShield()` | 未使用                                                      |

#### 重複コード

| 問題                                         | 箇所                                                                                        |
| -------------------------------------------- | ------------------------------------------------------------------------------------------- |
| `canAfford()`が3箇所で異なるシグネチャで定義 | `shopLogic.ts`, `blacksmithLogic.ts`, `playerTypes.ts`                                      |
| `BUFF_EFFECTS`定数が2箇所に存在              | `constants/data/battles/buffData.ts`（未使用）と`domain/battles/data/buffData.ts`（使用中） |
| ガード初期化ロジックが重複                   | `enemyStateLogic.ts`と`phaseLogic.ts`                                                       |

### ShopItem削除 & ExchangeTabバグ修正 & Item正規化

3つの問題を一括で修正した。

#### 修正1: ExchangeTabの魔石→ゴールド変換がヘッダーに反映されないバグ

**原因:** `ExchangeTab`が`updatePlayerData()`でPlayerStateのみ更新し、ResourceContextが未更新だった。`FacilityHeader`は`useResources()`から読むため反映されなかった。

**修正:**

- `ResourceContext.tsx`: `setBaseCampMagicStones(newStones)`を追加
- `PlayerContext.tsx`: `updateBaseCampMagicStones()`を追加（ResourceContextへ委譲）
- `ExchangeTab.tsx`: 3箇所の`updatePlayerData()`を`updateBaseCampMagicStones()`に変更

#### 修正2: ShopItem型をShopListingに置換（データ重複の解消）

**変更前:** `ShopData.ts`のShopItem[]に名前・価格・アイコン等を直書き → `generateItem.ts`でも同じ情報をハードコード
**変更後:** `ConsumableItemData`が唯一のデータソース、ShopListingはtypeIdで参照するだけ

| ファイル                                           | 変更内容                                                                  |
| -------------------------------------------------- | ------------------------------------------------------------------------- |
| `ConsumableEffectTypes.ts`                         | `shopPrice?: number`を型に追加                                            |
| `domain/item_equipment/data/ConsumableItemData.ts` | 新規作成。shopPrice付き4アイテム定義                                      |
| `ShopTypes.ts`                                     | `ShopItem`を`ShopListing`に置換                                           |
| `domain/camps/data/ShopData.ts`                    | `CONSUMABLE_LISTINGS`/`TELEPORT_LISTINGS` + `resolveShopListing()`        |
| `generateItem.ts`                                  | `generateConsumableFromData(typeId)` - ConsumableItemDataベースの生成関数 |
| `shopLogic.ts`                                     | `purchaseItem(listing)` - ShopListingベースに変更                         |
| `BuyTab.tsx`                                       | `ResolvedShopListing`で表示                                               |

#### 修正3: nodeEventLogic.tsの正規化

- import元を修正: `ItemTypes.ts` → `generateItem.ts`
- ハードコードされたアイテム生成（2箇所）→ `generateConsumableFromData("healing_potion")`に置換

**データフロー（新アーキテクチャ）:**

```
ShopListing (typeIdのみ) → ConsumableItemData (名前・価格・効果) → generateConsumableFromData() → Item
```

### 型定義リファクタリング完了

分散していた型定義を`src/types/`に集約し、コードベースの保守性を大幅に向上させた。

#### 背景と問題点

| 問題 | 詳細 |
|------|------|
| 型定義の分散 | `domain/*/type(s)/`に7箇所に分散（命名も`type`と`types`で不統一） |
| constantsに型混入 | `characterConstants.ts`にinterfaceが混在 |
| 定数の重複 | `RARITY_SELL_PRICES`が2ファイルで重複定義 |
| 循環依存リスク | constants → domain/type → 他domain → constants |

#### 実装した解決策

**Phase 0: パスエイリアス設定**
- `tsconfig.app.json`: `paths: { "@/*": ["src/*"] }` 追加
- `vite.config.ts`: `resolve.alias: { '@': './src' }` 追加

**Phase 1-2: 型ファイル作成と関数移動**

| 新規作成 | 内容 |
|---------|------|
| `src/types/cardTypes.ts` | Depth, Rarity, CardCategory, Card 等 |
| `src/types/characterTypes.ts` | CharacterClass, CardCharacterClass, Player, Enemy 等 |
| `src/types/itemTypes.ts` | Item, EquipmentSlot, MagicStones 等 |
| `src/types/battleTypes.ts` | BuffDebuffType, DamageResult, CardExecutionResult 等 |
| `src/types/campTypes.ts` | 施設型, NodeStatus（統合版）等 |
| `src/types/dungeonTypes.ts` | DungeonNode, DungeonFloor 等 |
| `src/types/saveTypes.ts` | SaveData, SaveResult 等 |
| `src/types/index.ts` | barrel export |

**移動した関数（型ファイルからlogicへ）:**
- `buffLogic.ts`, `cardExecutionLogic.ts`, `cardUtils.ts`
- `characterUtils.ts`, `playerUtils.ts`, `enemyUtils.ts`
- `campUtils.ts`, `blacksmithUtils.ts`, `storageLogic.ts`
- `itemUtils.ts`

**Phase 3-6: 移行とクリーンアップ**
- 22個の旧型ファイルを一時的にre-export shimに変換
- ~95ファイルのimportを`@/types/`パスに更新
- 21個のshimファイルを削除
- 1個のshimを残存（`domain/cards/type/cardType.ts` - immutable deck用）

**Phase 4: 定数の重複解消**
- `campConstants.ts`の`RARITY_SELL_PRICES`削除 → `itemConstants.ts`に一本化
- `characterConstants.ts`の`ResonanceEffectConfig`削除 → `types/characterTypes.ts`に移動

#### 最終的なディレクトリ構造

```
src/
├── types/                    # 全型定義を集約（8ファイル）
│   ├── battleTypes.ts
│   ├── campTypes.ts
│   ├── cardTypes.ts
│   ├── characterTypes.ts
│   ├── dungeonTypes.ts
│   ├── itemTypes.ts
│   ├── saveTypes.ts
│   └── index.ts
│
├── constants/               # 定数のみ（型定義なし）
│   └── itemConstants.ts     # RARITY_SELL_PRICES はここのみ
│
└── domain/
    ├── cards/
    │   ├── type/
    │   │   └── cardType.ts  # 最小shim（immutable deck用）
    │   └── logic/
    │       └── cardUtils.ts # 移動した関数群
    └── .../logic/           # 各domainに移動した関数群
```

#### 使用方法

```typescript
// Before (分散していた)
import type { Card } from "../../../domain/cards/type/cardType";
import type { Player } from "../../characters/type/playerTypes";

// After (統一されたパス)
import type { Card } from '@/types/cardTypes';
import type { Player } from '@/types/characterTypes';
// または
import type { Card, Player } from '@/types';
```

#### 変更統計

| 項目 | 数 |
|------|-----|
| 新規作成（types/） | 8ファイル |
| 新規作成（logic/） | ~10ファイル |
| import更新 | ~95ファイル |
| shim削除 | 21ファイル |
| shim残存 | 1ファイル（immutable deck用） |

**詳細:** `.claude/todos/REFACTORING_PLAN_TYPES.md`
