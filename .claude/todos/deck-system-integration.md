# カードデッキシステム統合改善書

作成日: 2026-01-26
ステータス: 計画承認待ち
元計画書: カードデッキシステム改善計画書（2026-01-25）

---

## 1. 現状分析サマリー

### 1.1 確認された問題点

| 問題 | 影響度 | 確認ファイル |
|------|--------|--------------|
| データソースの二重定義 | 高 | `CharacterClassData.ts`（5枚）vs `initialDeckConfig.ts`（20枚） |
| PlayerContext.deckの未使用 | 高 | `useBattleOrchestrator.ts`が完全に無視 |
| BattleScreenでのハードコーディング | 中 | `Swordman_Status`直接参照 |
| クラス別対応の欠如 | 中 | 常に剣士固定のデッキ生成 |

### 1.2 現在のデータフロー（問題あり）

```
CharacterSelect.tsx
    ↓ initializeWithClass(classType)
PlayerContext
    ↓ classInfo.starterDeck → playerState.deck (5枚)
    ↓ playerData.persistent.deckCardIds に格納
BattleScreen.tsx
    ↓ deckCardIdsを読み込まない ← 【断絶ポイント】
    ↓ initialPlayerState作成（Swordman_Statusハードコーディング）
useBattleOrchestrator.ts
    ↓ INITIAL_DECK_COUNTS使用（20枚、剣士固定）
createInitialDeck()
    ↓ 常に同じ20枚デッキ
```

---

## 2. 統合方針

### 2.1 決定事項

| 項目 | 決定 | 理由 |
|------|------|------|
| 初期デッキ枚数 | **15枚** | Phase 2での調整を不要にする |
| データソース統合 | **initialDeckConfig.ts** を正とする | 枚数管理に適した構造 |
| データフロー修正 | **BattleScreen側を修正** | PlayerContext.deckを活用 |

### 2.2 目標データフロー（改善後）

```
CharacterSelect.tsx
    ↓ initializeWithClass(classType)
PlayerContext
    ↓ INITIAL_DECK_BY_CLASS[classType] を参照
    ↓ playerData.persistent.deckCardIds に格納
BattleScreen.tsx
    ↓ playerDataからdeckCardIdsを取得 ← 【修正ポイント】
    ↓ initialPlayerStateに含める
useBattleOrchestrator.ts
    ↓ initialPlayerState.deckCardIds使用 ← 【修正ポイント】
createInitialDeck()
    ↓ クラス別の正しいデッキ
```

---

## 3. Phase 1: 即時対応（統合版）

### 3.1 変更ファイル一覧

| ファイル | 変更種別 | 概要 |
|----------|----------|------|
| `initialDeckConfig.ts` | 拡張 | キャラクター別設定、15枚構成に変更 |
| `CharacterClassData.ts` | 修正 | starterDeck廃止、initialDeckCountsへの参照に変更 |
| `useBattleState.ts` | 修正 | InitialPlayerState型にdeckConfig追加 |
| `useBattleOrchestrator.ts` | 修正 | initialPlayerStateからデッキ設定を受け取る |
| `BattleScreen.tsx` | 修正 | playerDataからデッキ情報を取得してinitialPlayerStateに含める |
| `PlayerContext.tsx` | 修正 | initializeWithClassでINITIAL_DECK_BY_CLASSを使用 |

### 3.2 initialDeckConfig.ts 変更設計

**変更内容:**
- `INITIAL_DECK_BY_CLASS`を新規追加（キャラクター別設定）
- 剣士を20枚→15枚に調整
- 後方互換性のため`INITIAL_DECK_COUNTS`はエイリアスとして維持

**15枚構成（剣士）:**
```
sw_001: 3  // 素早い斬撃（4→3）
sw_003: 2  // コンボストライク（3→2）
sw_007: 2  // スラッシュ（3→2）
sw_013: 2  // 剣気集中
sw_037: 2  // 剣気バリア
sw_038: 2  // カウンタースタンス
sw_014: 2  // 瞑想
-----------
合計: 15枚
```

**削除カード:**
- `sw_027`（剣気解放）: 初期には強すぎる、Phase 3の習得システムで追加

### 3.3 CharacterClassData.ts 変更設計

**変更内容:**
- `starterDeck: Card[]`を削除
- `initialDeckCounts: Record<string, number>`を追加
- `totalCards: number`を追加（UI表示用）
- `getStarterDeckStacks()`関数を追加（スタック表示用）

**新規追加関数:**
- `getStarterDeckStacks(classType)`: UI用にスタック形式でカード情報を返す
- `getCardDataByClass(classType)`: クラス別のカードデータ配列を返す

### 3.4 BattleScreen.tsx 変更設計

**変更内容:**
- `playerData.persistent.deckCardIds`を取得
- `initialPlayerState`に`deckCardIds`または`deckConfig`を追加
- `Swordman_Status`のハードコーディングを`playerData`参照に変更

**修正箇所（行88-106付近）:**
```typescript
// 現在: Swordman_Statusをハードコーディング
// 変更後: playerDataから取得
const initialPlayerState = useMemo<InitialPlayerState>(() => ({
  currentHp: runtimeState.currentHp,
  currentAp: runtimeState.currentAp,
  maxHp: playerData.persistent.baseMaxHp,
  maxAp: playerData.persistent.baseMaxAp,
  name: playerData.persistent.name,
  playerClass: playerData.persistent.playerClass,
  classGrade: playerData.persistent.classGrade,
  speed: playerData.persistent.baseSpeed,
  cardActEnergy: 3, // TODO: PlayerDataに追加
  cardMasteryStore: runtimeState.cardMasteryStore,
  deckConfig: getInitialDeckCounts(playerData.persistent.playerClass),
}), [...]);
```

### 3.5 useBattleOrchestrator.ts 変更設計

**変更内容:**
- `InitialPlayerState`型に`deckConfig`を追加
- `initialDeckState`の生成ロジックを修正

**修正箇所（行169-178付近）:**
```typescript
// 現在: INITIAL_DECK_COUNTS固定
// 変更後: initialPlayerState.deckConfigを使用
const initialDeckState = useMemo(() => {
  const deckCounts = initialPlayerState?.deckConfig ?? INITIAL_DECK_COUNTS;
  const cardData = getCardDataByClass(initialPlayerState?.playerClass ?? 'swordsman');
  let initialDeck = createInitialDeck(deckCounts, cardData);

  if (initialPlayerState?.cardMasteryStore && initialPlayerState.cardMasteryStore.size > 0) {
    initialDeck = applyMasteryToCards(initialDeck, initialPlayerState.cardMasteryStore);
  }
  return { hand: [], drawPile: initialDeck, discardPile: [] };
}, [initialPlayerState]);
```

### 3.6 PlayerContext.tsx 変更設計

**変更内容:**
- `initializeWithClass`でINITIAL_DECK_BY_CLASSを使用
- `playerData.persistent.deckCardIds`の生成ロジックを修正

**修正箇所（行313-342付近）:**
```typescript
// INITIAL_DECK_BY_CLASSからデッキを生成
const deckCounts = INITIAL_DECK_BY_CLASS[classType];
const cardData = getCardDataByClass(classType);
const deck = createDeckFromCounts(deckCounts, cardData);

const playerWithStarterDeck: Player = {
  ...basePlayer,
  deck: deck,
};
```

---

## 4. 追加提案

### 4.1 提案1: cardActEnergyのPlayerData化

**現状:** `Swordman_Status.cardActEnergy`をハードコーディング
**提案:** `PlayerData.persistent.cardActEnergy`に追加

**影響ファイル:**
- `playerTypes.ts`: PlayerData型定義
- `PlayerContext.tsx`: playerData生成
- `BattleScreen.tsx`: 参照箇所

### 4.2 提案2: キャラクター選択画面のスタック表示

**現状:** starterDeckを個別カード表示（5枚）
**提案:** スタック表示（同種カードを「×3」形式）

**新規コンポーネント:**
```typescript
// StarterDeckPreview.tsx
interface DeckCardStack {
  card: Card;
  count: number;
}

function StarterDeckPreview({ characterClass }: { characterClass: CharacterClass }) {
  const deckStacks = getStarterDeckStacks(characterClass);
  // スタック形式でカード表示
}
```

### 4.3 提案3: デッキ検証ユーティリティ

**目的:** 開発時のデッキ設定ミス検出

```typescript
// deckValidator.ts
export function validateDeckConfig(
  deckCounts: Record<string, number>,
  availableCards: Card[]
): { valid: boolean; errors: string[] } {
  // cardTypeIdの存在確認
  // 合計枚数の確認
  // 必須カードの確認
}
```

---

## 5. 実装優先度

### Phase 1A（必須・即時）
1. `initialDeckConfig.ts`の拡張
2. `useBattleOrchestrator.ts`の修正
3. `BattleScreen.tsx`の修正

### Phase 1B（推奨・即時）
4. `CharacterClassData.ts`の修正
5. `PlayerContext.tsx`の修正

### Phase 1C（任意・後日）
6. キャラクター選択画面のスタック表示
7. デッキ検証ユーティリティ

---

## 6. リスクと対策

| リスク | 影響 | 対策 |
|--------|------|------|
| 既存セーブデータとの互換性 | デッキ枚数変更による不整合 | バージョンチェックとマイグレーション処理 |
| Card型のid生成 | createInitialDeck内でのユニークID生成が必要 | 既存ロジックを維持、cardTypeIdベースで管理 |
| テストの欠如 | 回帰バグのリスク | 手動テストチェックリスト作成 |

---

## 7. テストチェックリスト

### 機能テスト
- [ ] キャラクター選択→バトル開始で正しいデッキが生成される
- [ ] 手札が正しく5枚ドローされる
- [ ] デッキの総枚数が15枚である
- [ ] カード使用後の熟練度が正しく保存される

### 回帰テスト
- [ ] 既存のバトルフローが動作する
- [ ] 敵撃破後の報酬画面が表示される
- [ ] 敗北時の処理が正しく動作する
- [ ] セーブ/ロードが正常に動作する

---

## 8. 関連ファイル参照

### 変更対象
```
src/domain/battles/data/initialDeckConfig.ts
src/domain/characters/player/data/CharacterClassData.ts
src/domain/battles/managements/useBattleOrchestrator.ts
src/domain/battles/managements/useBattleState.ts
src/ui/battleHtml/BattleScreen.tsx
src/domain/camps/contexts/PlayerContext.tsx
```

### 参照のみ（変更禁止）
```
src/domain/cards/decks/deck.ts（IMMUTABLE）
src/domain/cards/decks/deckReducter.ts（IMMUTABLE）
```

### 関連参照
```
src/domain/cards/data/SwordmanCards.ts
src/domain/characters/player/data/PlayerData.ts
src/ui/characterSelectHtml/CharacterSelect.tsx
src/ui/characterSelectHtml/StarterDeckPreview.tsx
```

---

## 9. Phase 3: カード派生習得システム設計

### 9.1 新概念: カード派生システム

**既存設計との差分:**

| 項目 | 既存設計（library_design.md等） | 新規追加（Phase 3） |
|------|--------------------------------|---------------------|
| 熟練度上昇 | 使用回数ベース（閾値: 50, 150, 250...） | 同左（維持） |
| カード習得 | Lv5で「才能カード」解放（詳細未定義） | **習得元カード + 必要熟練度Lv** で新カード習得 |
| 習得タイミング | 未定義 | 熟練度Lv到達時に即座に習得可能 |

**新規概念: カード派生ツリー**
```
基本カード（初期デッキ所持）
    ↓ 熟練度Lv2
派生カード1（習得）
    ↓ 熟練度Lv4
派生カード2（習得）
```

### 9.2 カードデータ拡張設計

**Card型への追加プロパティ:**
```typescript
interface Card {
  // 既存プロパティ
  id: string;
  cardTypeId: string;
  name: string;
  // ...

  // 新規追加: カード習得システム用
  unlockSource?: string;       // 習得元カードのcardTypeId（nullなら初期所持）
  unlockMasteryLevel?: number; // 必要熟練度Lv（1-5）
  unlocksCards?: CardUnlock[]; // このカードから派生するカード一覧
}

interface CardUnlock {
  cardTypeId: string;    // 習得されるカードのID
  requiredLevel: number; // 必要熟練度Lv
}
```

### 9.3 剣士カード派生ツリー（構想案）

```
【初期デッキ（15枚）】
├── sw_001 素早い斬撃 ×3
│   ├── Lv2 → sw_005 二段斬り
│   ├── Lv3 → sw_019 旋風剣
│   └── Lv5 → sw_020 閃光剣
│
├── sw_003 コンボストライク ×2
│   ├── Lv2 → sw_006 三段突き
│   ├── Lv4 → sw_026 獅子奮迅
│   └── Lv5 → sw_036 滅剣
│
├── sw_007 スラッシュ ×2
│   ├── Lv2 → sw_017 断空斬
│   └── Lv4 → sw_023 裂空斬
│
├── sw_013 剣気集中 ×2
│   ├── Lv2 → sw_015 闘気解放
│   └── Lv4 → sw_033 剣神降臨
│
├── sw_014 瞑想 ×2
│   ├── Lv2 → sw_016 気合
│   └── Lv4 → sw_029 心眼
│
├── sw_037 剣気バリア ×2
│   └── Lv3 → sw_038 カウンタースタンス（※初期デッキにも含まれる）
│
└── sw_038 カウンタースタンス ×2
    └── Lv4 → sw_039 鉄壁の構え
```

### 9.4 習得UI要件

1. **カード習得通知（バトル終了時）**
   - 熟練度Lvアップ時にポップアップ表示
   - 習得可能なカードを選択（複数派生がある場合）

2. **カードツリー表示（Library施設）**
   - エンサイクロペディア内に「進化ツリー」タブを追加
   - 習得済み/未習得/習得可能を視覚的に表示

3. **デッキ編集画面への影響**
   - 習得済みカードのみデッキに追加可能
   - 各カードの熟練度と派生先を表示

---

## 10. 設計書反映計画

### 10.1 反映対象ファイル一覧

| 設計書 | パス | 反映内容 |
|--------|------|----------|
| NEW_CHARACTER_SYSTEM_DESIGN.md | `.claude/docs/card_document/` | カード派生システムの詳細追加 |
| SWORDSMAN_CARDS_40.md | `.claude/docs/card_document/` | 各カードにunlockSource/unlocksCards追加 |
| library_design.md | `.claude/docs/camp_document/` | エンサイクロペディアにカード派生ツリー表示追加 |
| game_design_master.md | `.claude/docs/Overall_document/` | 成長システムにカード派生を明記 |

### 10.2 NEW_CHARACTER_SYSTEM_DESIGN.md への追記

**追加セクション:**
```markdown
### 熟練度によるカード派生システム（新規）

カード使用による熟練度上昇で、新しいカードを習得できる。

【基本ルール】
- 各カードには「派生先カード」と「必要熟練度Lv」が設定される
- 熟練度Lv到達時に派生カードが習得可能になる
- 同じカードから複数の派生先がある場合、プレイヤーが選択

【データ構造】
- unlockSource: 習得元カードのcardTypeId
- unlockMasteryLevel: 必要熟練度Lv（1-5）
- unlocksCards: このカードから派生するカード一覧

【既存「才能カード解放」との統合】
- Lv5の「才能カード解放」は、派生ツリーの最終段階として位置づけ
- 特別な才能カードはLv5でのみ習得可能
```

### 10.3 SWORDSMAN_CARDS_40.md への追記

**各カード定義への追加:**
```markdown
## sw_001 素早い斬撃

【既存項目】
- コスト: 1
- 効果: 威力15、剣気+1

【新規追加: 派生情報】
- 習得元: なし（初期所持）
- 派生先:
  - sw_005 二段斬り（Lv2）
  - sw_019 旋風剣（Lv3）
  - sw_020 閃光剣（Lv5）

---

## sw_005 二段斬り

【既存項目】
- コスト: 2
- 効果: 威力12×2、剣気+2

【新規追加: 派生情報】
- 習得元: sw_001（素早い斬撃）Lv2
- 派生先: （設計中）
```

### 10.4 library_design.md への追記

**エンサイクロペディアセクションへの追加:**
```markdown
### 4.4 カード派生ツリー表示（新規）

┌────────────────────────────────────────────────────────┐
│  📕 Book of Encyclopedia - Card Evolution Tree         │
├────────────────────────────────────────────────────────┤
│  [Swordsman][Mage][Summoner]                           │
│                                                        │
│  ┌─────────────────────────────────────────────────┐  │
│  │  ⚔️ 素早い斬撃 [Lv3/5]                          │  │
│  │       │                                         │  │
│  │       ├── ⚔️ 二段斬り [習得済] ───┐            │  │
│  │       │                          │             │  │
│  │       ├── 🔒 旋風剣 [Lv3で解放]   │             │  │
│  │       │                          │             │  │
│  │       └── 🔒 閃光剣 [Lv5で解放]   │             │  │
│  │                                  ↓             │  │
│  │                          （さらなる派生）       │  │
│  └─────────────────────────────────────────────────┘  │
│                                                        │
│  凡例: ✅習得済  🔒未習得  ⭐習得可能                 │
└────────────────────────────────────────────────────────┘
```

### 10.5 game_design_master.md への追記

**成長システムセクションへの追加:**
```markdown
### カードの成長（詳細）

1. **熟練度システム**
   - カード使用回数で熟練度Lv上昇（Lv1→5）
   - コストベースの必要使用回数

2. **カード派生システム**（新規）
   - 熟練度Lv到達で新カードを習得
   - 初期デッキカード → 中級カード → 上級カード の派生ツリー
   - Libraryのエンサイクロペディアで派生状況を確認可能

3. **デッキ構築への影響**
   - 習得済みカードのみデッキに追加可能
   - 同カード最大4枚の制限は維持
```

---

## 11. デッキ編成システム改定

### 11.1 新デッキルール

**既存設計からの変更:**

| 項目 | 既存設計 | 新設計 |
|------|----------|--------|
| デッキ枚数 | 40枚固定 | **15〜50枚**（自由） |
| 同種カード上限 | 最大4枚 | **上限なし**（習得済みなら何枚でも可） |
| 編集場所 | Libraryのみ | **Library + 探索準備画面** |

**制約:**
- 最低枚数: 15枚（初期デッキと同数）
- 最大枚数: 50枚
- 習得済みカードのみ追加可能

### 11.2 デッキ編成の戦略性

**少枚数デッキ（15〜25枚）:**
- キーカードを引きやすい
- デッキ回転が速い
- リスク: 手札枯渇、山札切れ

**多枚数デッキ（35〜50枚）:**
- 多様な状況に対応可能
- 長期戦向き
- リスク: キーカードが引けない

### 11.3 UI要件

**Libraryでのデッキ編集（詳細）:**
- フル編集機能
- カード一覧（フィルタ/ソート）
- マナカーブ表示
- デッキ統計（平均コスト、カード種別比率）
- 複数Loadout保存（既存設計通り3セット）

**探索準備画面での簡易編集:**
- 保存済みLoadoutからの選択
- カード数枚の差し替え程度の微調整
- 「Libraryで詳細編集」ボタン → Library画面へ遷移

---

## 12. 探索準備画面（新規設計）

### 12.1 概要

**既存のダンジョンゲート画面を完全置き換え**

depth選択を廃止し、以下の機能に特化:
1. デッキ選択・簡易編集
2. 装備選択
3. アイテム準備（Storage ↔ Inventory）
4. 探索開始

### 12.2 depth進行システム

**自動進行方式:**
```
初回探索 → Depth 1
    ↓ クリア
次回探索 → Depth 2
    ↓ クリア
次回探索 → Depth 3
    ...
```

**進行ルール:**
- クリアしたDepthの次から自動開始
- 敗北時は同じDepthから再挑戦
- （将来検討）任意のDepthに戻る機能

### 12.3 画面構成案

```
┌────────────────────────────────────────────────────────────┐
│  ⚔️ 探索準備                           Next: Depth 3      │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  ┌─────────────────────┐  ┌─────────────────────────────┐ │
│  │ 📖 デッキ           │  │ 🎒 アイテム準備             │ │
│  │                     │  │                             │ │
│  │ Loadout: "攻撃特化" │  │ Storage → Inventory         │ │
│  │ カード数: 32枚      │  │                             │ │
│  │                     │  │ [ポーション] [→] [        ] │ │
│  │ [変更] [詳細編集→]  │  │ [回復薬]     [→] [        ] │ │
│  │                     │  │ [魔石(小)]   [→] [        ] │ │
│  └─────────────────────┘  │                             │ │
│                           │ Inventory: 3/20              │ │
│  ┌─────────────────────┐  └─────────────────────────────┘ │
│  │ 🗡️ 装備             │                                  │
│  │                     │                                  │
│  │ 武器: 鉄の剣        │                                  │
│  │ 防具: 革の鎧        │                                  │
│  │ アクセ: 力の指輪    │                                  │
│  │                     │                                  │
│  │ [変更]              │                                  │
│  └─────────────────────┘                                  │
│                                                            │
│           [🚪 探索開始]        [← キャンプに戻る]          │
└────────────────────────────────────────────────────────────┘
```

### 12.4 機能詳細

**デッキセクション:**
- 現在選択中のLoadout表示
- カード総数表示
- [変更] → Loadout一覧モーダル
- [詳細編集→] → Library画面へ遷移（状態保持）

**アイテム準備セクション:**
- Storage（倉庫）からInventory（持ち物）へアイテム移動
- ドラッグ&ドロップまたはボタンで移動
- Inventory上限表示（現在/最大）
- 装備中アイテムは移動不可

**装備セクション:**
- 現在の装備一覧
- [変更] → 装備選択モーダル
- 装備変更時は自動でInventoryから装備

### 12.5 設計書反映対象

| 設計書 | パス | 反映内容 |
|--------|------|----------|
| game_design_master.md | `.claude/docs/Overall_document/` | 探索フロー変更、depth自動進行 |
| library_design.md | `.claude/docs/camp_document/` | デッキルール変更（15〜50枚、同種上限撤廃） |
| danjeon_document内 | `.claude/docs/danjeon_document/` | ダンジョンゲート画面廃止、探索準備画面新設 |
| storage_design.md | `.claude/docs/camp_document/` | Storage↔Inventory連携の詳細 |

---

## 13. 実装優先度（全体・改定版）

### Phase 1（即時）: デッキシステム統合
1. `initialDeckConfig.ts`の拡張
2. `useBattleOrchestrator.ts`の修正
3. `BattleScreen.tsx`の修正
4. `CharacterClassData.ts`の修正
5. `PlayerContext.tsx`の修正

### Phase 2（中期）: 設計書反映
6. `NEW_CHARACTER_SYSTEM_DESIGN.md`への派生システム追記
7. `SWORDSMAN_CARDS_40.md`への派生情報追記
8. `library_design.md`への派生ツリーUI + デッキルール変更追記
9. `game_design_master.md`への成長システム + 探索フロー変更追記

### Phase 3（長期）: カード派生システム実装
10. Card型への派生プロパティ追加
11. 熟練度Lvアップ時の習得判定ロジック
12. 習得通知UI実装
13. Libraryエンサイクロペディアの派生ツリーUI実装

### Phase 4（長期）: 探索準備画面実装
14. ダンジョンゲート画面の廃止
15. 探索準備画面の新規作成
16. depth自動進行ロジック実装
17. Storage↔Inventory連携UI実装
18. Library画面への遷移・状態保持

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2026-01-25 | 元計画書作成 |
| 2026-01-26 | 統合改善書作成、15枚構成・BattleScreen側修正方針に決定 |
| 2026-01-26 | Phase 3カード派生システムと設計書反映計画を追加 |
| 2026-01-26 | デッキ編成システム改定（15〜50枚、同種上限撤廃）、探索準備画面新設計を追加 |
