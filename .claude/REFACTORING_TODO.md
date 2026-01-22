# Roguelike Card Battle - リファクタリング & 開発 TODO

> 作成日: 2025-01-21
> 優先度順: Interface整理 → Hook分割 → State分類 → バランス調整 → アセット統合 → 全体調整

---

## 目次

1. [Phase 1: Interface整理](#phase-1-interface整理)
2. [Phase 2: Hook分割](#phase-2-hook分割)
3. [Phase 3: State分類・統一](#phase-3-state分類統一)
4. [Phase 4: バランス調整](#phase-4-バランス調整)
5. [Phase 5: アセット統合（背景画像）](#phase-5-アセット統合背景画像)
6. [Phase 6: 全体調整・新機能](#phase-6-全体調整新機能)
7. [既知のバグ一覧](#既知のバグ一覧)

---

## Phase 1: Interface整理

### 1.1 キャラクター系Interfaceの再設計

**現状の問題点:**

- `Character` → `Player` → `ExtendedPlayer` の継承が複雑
- `Player`と`ExtendedPlayer`の境界が曖昧
- 戦闘時と永続データで同じInterfaceを使用している

**新設計案:**

```typescript
// ========== 基底型 ==========

/** 戦闘時の共通ステータス（Player/Enemy共通） */
interface BattleStats {
  hp: number;
  maxHp: number;
  ap: number; // Armor Points
  maxAp: number;
  guard: number;
  speed: number;
  buffDebuffs: BuffDebuffMap;
}

/** クラス固有能力の抽象型 */
type ClassAbilityState =
  | SwordEnergyState // Swordsman
  | ElementalState // Mage
  | SummonState; // Summoner

// ========== プレイヤー関連 ==========

/** プレイヤーの永続データ（セーブ対象） */
interface PlayerPersistentData {
  id: string;
  name: string;
  playerClass: CharacterClass;
  classGrade: string;
  level: number;

  // ステータス基礎値
  baseMaxHp: number;
  baseMaxAp: number;
  baseSpeed: number;

  // デッキ・装備
  deckCards: CardId[]; // カードIDの配列
  equipmentSlots: EquipmentSlots;

  // 称号・実績
  titles: string[];
}

/** プレイヤーのリソース管理 */
interface PlayerResources {
  // ゴールド（拠点/探索で分離）
  baseCampGold: number;
  explorationGold: number;

  // 魔石（拠点/探索で分離）
  baseCampMagicStones: MagicStones;
  explorationMagicStones: MagicStones;

  // 探索制限
  explorationLimit: ExplorationLimit;
}

/** プレイヤーの所持品管理 */
interface PlayerInventory {
  storage: StorageState; // 倉庫（最大100）
  inventory: InventoryState; // 所持品（最大20）
  equipmentInventory: EquipmentSlot[]; // 予備装備（最大3）
}

/** プレイヤーの進行度管理 */
interface PlayerProgression {
  sanctuaryProgress: SanctuaryProgress;
  unlockedDepths: Depth[];
  completedAchievements: string[];
}

/** プレイヤーの完全データ（全て統合） */
interface PlayerData {
  persistent: PlayerPersistentData;
  resources: PlayerResources;
  inventory: PlayerInventory;
  progression: PlayerProgression;
}

/** 戦闘時のプレイヤー状態 */
interface PlayerBattleState extends BattleStats {
  cardEnergy: number;
  maxCardEnergy: number;
  classAbility: ClassAbilityState;
  currentDeck: DeckState;
}

// ========== 敵関連 ==========

/** 敵の定義データ（マスターデータ） */
interface EnemyDefinition {
  id: string;
  name: string;
  nameJa: string;
  description: string;

  // 基礎ステータス
  baseMaxHp: number;
  baseMaxAp: number;
  baseSpeed: number;
  startingGuard: boolean;

  // AI
  actEnergy: number;
  aiPatterns: EnemyAIPattern[];

  // 表示
  imagePath?: string;
}

/** 戦闘時の敵状態 */
interface EnemyBattleState extends BattleStats {
  definitionId: string; // EnemyDefinitionへの参照
  definition: EnemyDefinition; // キャッシュ
  energy: number;
  phaseCount: number;
  turnCount: number;
  ref: React.RefObject<HTMLDivElement | null>;
}
```

**タスク:**

- [ ] **1.1.1** `src/domain/characters/types/` に新しい型定義ファイルを作成
  - [ ] `baseTypes.ts` - BattleStats, 共通型
  - [ ] `playerTypes.ts` - PlayerPersistentData, PlayerResources, PlayerInventory, PlayerProgression, PlayerData, PlayerBattleState
  - [ ] `enemyTypes.ts` - EnemyDefinition, EnemyBattleState
  - [ ] `classAbilityTypes.ts` - SwordEnergyState, ElementalState, SummonState

- [ ] **1.1.2** 既存の型定義を新構造にマイグレーション
  - [ ] 旧`Character`を`BattleStats`に置換
  - [ ] 旧`Player`/`ExtendedPlayer`を`PlayerData` + `PlayerBattleState`に分離
  - [ ] 旧`Enemy`を`EnemyDefinition`に置換
  - [ ] 旧`EnemyBattleState`を新`EnemyBattleState`に更新

- [ ] **1.1.3** 型変換ユーティリティの作成
  - [ ] `createPlayerBattleState(playerData: PlayerData): PlayerBattleState`
  - [ ] `createEnemyBattleState(definition: EnemyDefinition): EnemyBattleState`
  - [ ] `extractPersistentData(battleState: PlayerBattleState, currentData: PlayerData): PlayerData`

---

### 1.2 クラス固有能力Interfaceの設計

**Swordsman - 剣気システム（既存）**

```typescript
interface SwordEnergyState {
  type: "swordEnergy";
  current: number; // 0-10
  max: number; // 10
}
```

**Mage - 属性連鎖システム（新規）**

```typescript
type ElementType =
  | "fire"
  | "ice"
  | "lightning"
  | "earth"
  | "wind"
  | "dark"
  | "light";

interface ElementalState {
  type: "elemental";
  lastElement: ElementType | null;
  chainCount: number; // 同属性連続使用回数
  chargedElements: Map<ElementType, number>; // 蓄積属性パワー
}

// 効果例:
// - 2連鎖: 属性ダメージ+20%
// - 3連鎖: 属性固有デバフ付与
// - 4連鎖以上: 属性爆発（全体攻撃）
```

**Summoner - 召喚システム（新規）**

```typescript
type SummonType = "offensive" | "defensive" | "support";

interface Summon {
  id: string;
  name: string;
  type: SummonType;
  hp: number;
  maxHp: number;
  duration: number; // 残りターン数
  abilities: SummonAbility[];
}

interface SummonState {
  type: "summon";
  activeSummons: Summon[]; // 最大3体
  summonSlots: number; // 解放済みスロット数
  bondLevel: number; // 召喚獣との絆（強化係数）
}
```

**タスク:**

- [ ] **1.2.1** クラス固有システムの抽象化
  - [ ] `ClassAbilitySystem<T>` インターフェース作成
  - [ ] 共通メソッド定義: `initialize()`, `onCardPlay()`, `onTurnStart()`, `onTurnEnd()`, `calculateBonus()`

- [ ] **1.2.2** Swordsmanシステムを新インターフェースに適合
  - [ ] `swordEnergySystem.ts`をリファクタリング
  - [ ] `ClassAbilitySystem<SwordEnergyState>`として実装

- [ ] **1.2.3** Mageシステムの型定義作成（実装は後回し）
  - [ ] `elementalSystem.ts` 作成
  - [ ] `ElementalState` 型定義
  - [ ] `ClassAbilitySystem<ElementalState>`のスタブ実装

- [ ] **1.2.4** Summonerシステムの型定義作成（実装は後回し）
  - [ ] `summonSystem.ts` 作成
  - [ ] `SummonState`, `Summon` 型定義
  - [ ] `ClassAbilitySystem<SummonState>`のスタブ実装

---

### 1.3 カード型の拡張

**現状と拡張案:**

```typescript
interface Card {
  // 既存フィールド（維持）
  id: string;
  cardTypeId: string;
  name: string;
  description: string;
  cost: number;
  category: CardCategory;
  rarity: Rarity;

  // クラス関連（拡張）
  characterClass: CharacterClass | "common"; // commonは全クラス共通

  // Mage用（新規）
  element?: ElementType;
  elementalChainBonus?: number;

  // Summoner用（新規）
  summonId?: string; // 召喚するSummonのID
  summonEnhancement?: number; // 召喚獣強化値
  requiresSummon?: boolean; // 召喚獣が必要か

  // 既存フィールド（維持）
  baseDamage?: number;
  hitCount?: number;
  // ... その他既存フィールド
}
```

**タスク:**

- [ ] **1.3.1** Card型を拡張
  - [ ] `characterClass`を必須フィールドに
  - [ ] Mage用フィールド追加
  - [ ] Summoner用フィールド追加

- [ ] **1.3.2** 既存Swordsmanカードに`characterClass: "swordsman"`を追加

---

## Phase 2: Hook分割

### 2.1 現状の問題点

**`useBattle` hookの現状:**

- 770行以上のコード
- 20個の`useState` + 1個の`useReducer`
- 65個以上のプロパティを返却
- 責務が混在（状態管理、フェーズ制御、アニメーション、カード効果）

### 2.2 新Hook構成

```
src/hooks/
├── battle/
│   ├── useBattleOrchestrator.ts  # 戦闘全体の統括（既存useBattleの置き換え）
│   ├── useBattleState.ts         # HP/AP/Guard等の状態管理
│   ├── useBattlePhase.ts         # フェーズ管理（既存を拡張）
│   ├── useBattleDeck.ts          # デッキ管理（既存を拡張）
│   ├── useCardExecution.ts       # カード効果の実行
│   ├── useEnemyAI.ts             # 敵AIの行動決定
│   ├── useBattleAnimation.ts     # アニメーション制御
│   └── useBattleResult.ts        # 勝敗判定・統計
│
├── player/
│   ├── usePlayerBattleState.ts   # 戦闘時プレイヤー状態
│   ├── useClassAbility.ts        # クラス固有能力（汎用）
│   ├── useSwordEnergy.ts         # 剣気システム
│   ├── useElementalChain.ts      # 属性連鎖システム（Mage用）
│   └── useSummons.ts             # 召喚システム（Summoner用）
│
├── resource/
│   ├── useGold.ts                # ゴールド管理
│   ├── useMagicStones.ts         # 魔石管理
│   └── useExplorationResources.ts # 探索リソース統合
│
├── inventory/
│   ├── useInventory.ts           # 所持品管理
│   ├── useEquipment.ts           # 装備管理
│   └── useStorage.ts             # 倉庫管理
│
└── dungeon/
    ├── useDungeonRun.ts          # ダンジョン実行
    ├── useDungeonMap.ts          # マップ生成・ナビゲーション
    └── useNodeInteraction.ts     # ノード操作
```

### 2.3 各Hookの責務定義

**タスク:**

- [ ] **2.3.1** `useBattleState.ts` 作成

  ```typescript
  interface UseBattleStateReturn {
    // プレイヤー状態
    playerState: PlayerBattleState;
    updatePlayerHp: (delta: number) => void;
    updatePlayerAp: (delta: number) => void;
    updatePlayerGuard: (delta: number) => void;
    updatePlayerBuffs: (buffs: BuffDebuffMap) => void;

    // 敵状態
    enemies: EnemyBattleState[];
    updateEnemyState: (
      enemyId: string,
      updates: Partial<EnemyBattleState>,
    ) => void;
    removeEnemy: (enemyId: string) => void;

    // 派生値
    aliveEnemies: EnemyBattleState[];
    isPlayerAlive: boolean;
    areAllEnemiesDead: boolean;
  }
  ```

- [ ] **2.3.2** `useCardExecution.ts` 作成

  ```typescript
  interface UseCardExecutionReturn {
    executeCard: (
      card: Card,
      target?: EnemyBattleState,
    ) => Promise<CardExecutionResult>;
    canPlayCard: (card: Card) => boolean;
    getCardEffectPreview: (card: Card) => CardEffectPreview;
  }
  ```

- [ ] **2.3.3** `useEnemyAI.ts` 作成

  ```typescript
  interface UseEnemyAIReturn {
    determineEnemyAction: (enemy: EnemyBattleState) => EnemyAction;
    executeEnemyAction: (
      enemy: EnemyBattleState,
      action: EnemyAction,
    ) => Promise<void>;
    getNextActionPreview: (enemy: EnemyBattleState) => EnemyAction | null;
  }
  ```

- [ ] **2.3.4** `useClassAbility.ts` 作成（汎用）

  ```typescript
  interface UseClassAbilityReturn<T extends ClassAbilityState> {
    abilityState: T;
    onCardPlayed: (card: Card) => void;
    onTurnStart: () => void;
    onTurnEnd: () => void;
    getDamageModifier: () => DamageModifier;
    getAbilityUI: () => ReactNode; // クラス固有UI
  }
  ```

- [ ] **2.3.5** `useBattleOrchestrator.ts` 作成（統括）
  - [ ] 他のhookを組み合わせて戦闘全体を管理
  - [ ] 返却値は現在のuseBattleと互換性を持たせる（段階的移行のため）

- [ ] **2.3.6** 既存の`useBattle`から段階的に機能を移行
  - [ ] Step 1: `useBattleState`を抽出・使用
  - [ ] Step 2: `useCardExecution`を抽出・使用
  - [ ] Step 3: `useEnemyAI`を抽出・使用
  - [ ] Step 4: `useClassAbility`系を抽出・使用
  - [ ] Step 5: `useBattleOrchestrator`として再構成

---

## Phase 3: State分類・統一

### 3.1 Context再設計

**現状の問題点:**

- `PlayerContext`, `InventoryContext`, `GameStateContext`, `DungeonRunContext`が独立
- 状態の同期が困難
- 重複したデータ保持

**新設計:**

```typescript
// ========== リソース Context ==========
interface ResourceState {
  gold: {
    baseCamp: number;
    exploration: number;
  };
  magicStones: {
    baseCamp: MagicStones;
    exploration: MagicStones;
  };
  explorationLimit: ExplorationLimit;
}

// ========== プレイヤー Context ==========
interface PlayerState {
  persistent: PlayerPersistentData;
  inventory: PlayerInventory;
  progression: PlayerProgression;
}

// ========== ゲーム進行 Context ==========
interface GameProgressState {
  currentScreen: ScreenType;
  battleMode: BattleMode | null;
  activeDungeonRun: DungeonRun | null;
}

// ========== 戦闘 Context ==========
interface BattleContextState {
  isActive: boolean;
  playerBattleState: PlayerBattleState | null;
  enemies: EnemyBattleState[];
  phaseState: PhaseState;
  deckState: DeckState;
}
```

**タスク:**

- [ ] **3.1.1** `ResourceContext` 作成
  - [ ] gold管理（baseCamp/exploration分離）
  - [ ] magicStones管理
  - [ ] explorationLimit管理
  - [ ] `transferExplorationToBaseCamp()` 実装

- [ ] **3.1.2** `PlayerContext` 再設計
  - [ ] リソース部分を`ResourceContext`に移動
  - [ ] `PlayerPersistentData`のみ管理するように変更

- [ ] **3.1.3** `BattleContext` 作成
  - [ ] 戦闘状態の一元管理
  - [ ] `useBattle`の状態をこのContextに移行

- [ ] **3.1.4** Context Provider階層の整理
  ```tsx
  <ResourceProvider>
    <PlayerProvider>
      <GameProgressProvider>
        <BattleProvider>
          <App />
        </BattleProvider>
      </GameProgressProvider>
    </PlayerProvider>
  </ResourceProvider>
  ```

### 3.2 状態の永続化準備

**タスク:**

- [ ] **3.2.1** セーブデータ構造の定義

  ```typescript
  interface SaveData {
    version: string;
    player: PlayerPersistentData;
    resources: ResourceState;
    inventory: PlayerInventory;
    progression: PlayerProgression;
    timestamp: number;
  }
  ```

- [ ] **3.2.2** セーブ/ロード機能の骨組み作成（LocalStorage）
  - [ ] `saveGame(saveData: SaveData): void`
  - [ ] `loadGame(): SaveData | null`
  - [ ] `deleteSave(): void`

---

## Phase 4: バランス調整

### 4.1 カードバランス

**タスク:**

- [ ] **4.1.1** 現在の全カードをスプレッドシートで整理
  - [ ] カード名、コスト、効果、レアリティ
  - [ ] カテゴリ別の分布確認

- [ ] **4.1.2** コスト対効果の基準策定

  ```
  コスト0: 弱い効果 or デメリット付き
  コスト1: 基本効果（ダメージ5-8, ガード5-8）
  コスト2: 中程度効果（ダメージ10-15, ガード10-15, 複合効果）
  コスト3: 強力効果（ダメージ20+, 強力バフ/デバフ）
  ```

- [ ] **4.1.3** 剣気システムのバランス確認
  - [ ] 剣気獲得量の調整
  - [ ] 閾値効果の調整

- [ ] **4.1.4** 調整が必要なカードの修正

### 4.2 敵バランス

**タスク:**

- [ ] **4.2.1** 深度別の敵ステータス基準策定

  ```
  Depth 1: HP 30-50, ATK 5-8
  Depth 2: HP 50-80, ATK 8-12
  Depth 3: HP 80-120, ATK 12-18
  ...
  ```

- [ ] **4.2.2** ボス敵の設計
  - [ ] 各深度のボス能力定義
  - [ ] ボス専用AIパターン作成

- [ ] **4.2.3** エリート敵の調整
  - [ ] 通常敵より強化された能力
  - [ ] 特殊ドロップの設定

### 4.3 進行バランス

**タスク:**

- [ ] **4.3.1** ゴールド獲得量の調整
  - [ ] 戦闘報酬
  - [ ] 宝箱報酬
  - [ ] ショップ価格とのバランス

- [ ] **4.3.2** 装備ドロップ率の調整

- [ ] **4.3.3** カード獲得機会の調整

---

## Phase 5: アセット統合（背景画像）

### 5.1 既存アセット

**現在のアセット（`public/assets/images/`）:**

- `Sanctuary-background.png` - 聖域背景
- `Blacksmith-background.png` - 鍛冶屋背景
- `Shop-background.png` - ショップ背景
- `Storage-background.png` - 倉庫背景
- `depth_1-background.png` - 深度1戦闘背景

### 5.2 背景画像の適用

**タスク:**

- [ ] **5.2.1** 各施設画面への背景適用
  - [ ] `Sanctuary.tsx` に `Sanctuary-background.png` 適用
  - [ ] `Blacksmith.tsx` に `Blacksmith-background.png` 適用
  - [ ] `Shop.tsx` に `Shop-background.png` 適用
  - [ ] `Storage.tsx` に `Storage-background.png` 適用

- [ ] **5.2.2** 戦闘画面への背景適用
  - [ ] `BattleScreen.tsx` に深度別背景を適用
  - [ ] 深度に応じた背景切り替えロジック実装
  - [ ] 背景画像未設定時のフォールバック（現在のCSS背景を維持）

- [ ] **5.2.3** 背景適用用の共通コンポーネント/スタイル作成
  ```typescript
  // 例: BackgroundWrapper.tsx
  interface BackgroundWrapperProps {
    imagePath?: string;
    fallbackColor?: string;
    children: ReactNode;
  }
  ```

---

## Phase 6: 全体調整・新機能

### 6.1 未実装機能

**タスク:**

- [ ] **6.1.1** Library施設の実装
  - [ ] UI設計
  - [ ] カード図鑑機能
  - [ ] 敵図鑑機能
  - [ ] ゲームTips表示

- [ ] **6.1.2** キャラクター選択画面
  - [ ] Swordsman / Mage / Summoner 選択UI
  - [ ] 各クラスの説明表示
  - [ ] 初期デッキプレビュー
  - [ ] キャラクター作成フロー

- [ ] **6.1.3** Sanctuary UIの改善
  - [ ] スキルツリー表示の改善
  - [ ] 解放状況の可視化
  - [ ] 効果説明の追加

### 6.2 Mage実装（基本）

**タスク:**

- [ ] **6.2.1** Mage初期ステータス定義
- [ ] **6.2.2** 属性連鎖システム実装
- [ ] **6.2.3** Mage専用カード作成（最低20枚）
- [ ] **6.2.4** Mage専用UI（属性ゲージ）

### 6.3 Summoner実装（基本）

**タスク:**

- [ ] **6.3.1** Summoner初期ステータス定義
- [ ] **6.3.2** 召喚システム実装
- [ ] **6.3.3** Summoner専用カード作成（最低20枚）
- [ ] **6.3.4** Summoner専用UI（召喚獣表示）
- [ ] **6.3.5** 召喚獣データ定義

---

## 既知のバグ一覧

### 高優先度

| ID      | 概要                                                                   | 場所                           | 原因                                |
| ------- | ---------------------------------------------------------------------- | ------------------------------ | ----------------------------------- |
| BUG-001 | Depth選択後、basecampに戻ってから別Depthを選択してもDepth1が表示される | DungeonGate                    | DungeonRunContextの状態リセット不備 |
| BUG-002 | 階層の最終敵がボスではなく通常敵                                       | dungeonLogic.ts                | 最終行のノード生成時にtype判定なし  |
| BUG-003 | 最終敵を倒した後、強制的にbasecampに戻る                               | NodeMap.tsx / BattleScreen.tsx | 勝利後のフロー制御不備              |
| BUG-004 | ダンジョンゲートから再度バトルを挑めない                               | DungeonGate                    | isActive状態のリセット不備          |
| BUG-005 | ノードにカーソルを合わせても情報が表示されない                         | NodeMap.tsx                    | ツールチップ実装未完了              |

### 中優先度

| ID      | 概要                                                      | 場所                | 原因               |
| ------- | --------------------------------------------------------- | ------------------- | ------------------ |
| BUG-006 | encounterCountがDungeonRunContextとBattleScreenで二重管理 | 複数                | 設計不備           |
| BUG-007 | 複数敵対応のロジックが不完全（常にenemies[0]を参照）      | battleFlowManage.ts | グループ戦闘未対応 |
| BUG-008 | ダンジョンマップの接続がランダムで再現性なし              | dungeonLogic.ts     | シード値未実装     |

### 低優先度

| ID      | 概要                                                              | 場所                 | 原因   |
| ------- | ----------------------------------------------------------------- | -------------------- | ------ |
| BUG-009 | 装備効果（equipmentAtkPercent等）がダメージ計算に反映されていない | damageCalculation.ts | 未実装 |
| BUG-010 | カードマスタリー/ジェムレベルの上昇ロジックなし                   | -                    | 未実装 |

---

## 作業進捗管理

### Phase 1 進捗: 100% (完了)

- [x] 1.1 キャラクターInterface再設計
  - [x] baseTypes.ts 作成 (BattleStats, CharacterClass)
  - [x] classAbilityTypes.ts 作成 (SwordEnergyState, ElementalState, SummonState)
  - [x] playerTypes.ts 更新 (PlayerPersistentData, PlayerResources, PlayerInventory, PlayerProgression, PlayerData, PlayerBattleState)
  - [x] enemyType.ts 更新 (EnemyDefinition, EnemyBattleState)
  - [x] typeConverters.ts 作成 (型変換ユーティリティ)
- [x] 1.2 クラス固有能力Interface
  - [x] classAbilitySystem.ts 作成 (ClassAbilitySystem<T> interface, DamageModifier)
  - [x] swordEnergySystem.ts 更新 (ClassAbilitySystem実装)
  - [x] elementalSystem.ts 作成 (Mage用スタブ)
  - [x] summonSystem.ts 作成 (Summoner用スタブ)
- [x] 1.3 カード型拡張
  - [x] cardType.ts 更新 (characterClass必須化, Mage/Summonerフィールド追加)

### Phase 2 進捗: 100% (完了)

- [x] 2.3 Hook分割実装
  - [x] useBattleState.ts 作成 (Player/Enemy状態管理)
  - [x] useCardExecution.ts 作成 (カード効果実行)
  - [x] useEnemyAI.ts 作成 (敵AI行動決定・実行)
  - [x] useClassAbility.ts 作成 (クラス固有能力管理)
  - [x] useBattleOrchestrator.ts 作成 (統括フック)
  - [x] battleFlowManage.ts をre-exportに変更（後方互換性維持）
  - [x] cardExecutionTypes.ts 作成 (カード実行結果型)

### Phase 3 進捗: 100% (完了)

- [x] 3.1 Context再設計
  - [x] ResourceContext.tsx 作成 (gold/magicStones/explorationLimit管理)
  - [x] PlayerContext.tsx 再設計 (リソース部分をResourceContextに委譲)
  - [x] GameStateContext.tsx からencounterCount削除 (DungeonRunContextに一元化)
  - [x] App.tsx Provider階層更新
- [x] 3.2 永続化準備
  - [x] saveTypes.ts 作成 (SaveData型定義)
  - [x] saveManager.ts 作成 (LocalStorage save/load機能)

### Phase 4 進捗: 0%

- [ ] 4.1 カードバランス
- [ ] 4.2 敵バランス
- [ ] 4.3 進行バランス

### Phase 5 進捗: 0%

- [ ] 5.1 背景画像適用

### Phase 6 進捗: 0%

- [ ] 6.1 未実装機能
- [ ] 6.2 Mage実装
- [ ] 6.3 Summoner実装

---

## 備考

- 各Phaseは順番に進めることを推奨（依存関係あり）
- Phase 1-3は並行作業が難しいため、完了してから次へ
- Phase 4-5は比較的独立しているため、必要に応じて並行可能
- バグ修正はPhase 1-3完了後にまとめて行うことを推奨（リファクタリングで自然に解消されるものもあるため）
