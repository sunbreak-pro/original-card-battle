# カードシステムリファクタリング設計書

## 概要

本設計書は以下2つの要件を満たすためのリファクタリング計画を定義する：

1. **カードフィルタリングシステム**: ベースカードのみがデッキ構築時に選択可能で、派生カードはマスタリーレベルで解放
2. **Element/Category統合**: 現在のCategoryを廃止し、Elementに統合

---

## 1. 用語定義

| 用語 | 定義 |
|------|------|
| ベースカード | `derivedFrom === null` のカード。最初から選択可能 |
| 派生カード | `derivedFrom` に親カードのcardTypeIdが設定されたカード。親のマスタリーで解放 |
| 未設定カード | `derivedFrom === undefined` のカード。将来派生関係を設定予定 |
| マスタリーレベル | カード使用回数に基づく習熟度 (0-4) |

---

## 2. 設計方針

### 2.1 derivedFromの値による分類

```typescript
derivedFrom: null        // ベースカード → 最初から選択可能
derivedFrom: undefined   // 未設定 → 選択不可（データ設定待ち）
derivedFrom: "sw_001"    // 派生カード → 親カードのマスタリーで解放
```

### 2.2 Element統合後の構造

#### 新ElementType定義

```typescript
type ElementType =
  // 魔法属性（Mage primary）
  | "fire" | "ice" | "lightning" | "dark" | "light"
  // 物理属性（Swordsman primary）
  | "physics"
  // 召喚属性（Summoner primary）
  | "summon" | "enhance" | "sacrifice"
  // 機能分類（旧Category）
  | "attack"        // 旧 atk
  | "guard"         // 旧 def（既存）
  | "buff"          // 既存
  | "debuff"        // 既存
  | "heal"          // 既存
  // クラス固有
  | "classAbility"  // Swordsmanの剣気カード（旧swordEnergy）
  | "chain";        // Mage用、magic elementカードに自動付与
```

#### 廃止されるもの

- `CardCategory` 型
- `Rarity` 型
- `Card.category` フィールド
- `Card.rarity` フィールド
- `CARD_CATEGORY_NAMES` 定数
- `RARITY_COLORS` 定数

### 2.3 Category → Element 変換ルール

| 旧 category | 新 element |
|-------------|-----------|
| `atk` | `attack` |
| `def` | `guard` |
| `buff` | `buff` |
| `debuff` | `debuff` |
| `heal` | `heal` |
| `swordEnergy` | `classAbility` |

### 2.4 Mage用chain自動付与

- `characterClass === "mage"` の場合
- magic element（fire, ice, lightning, dark, light）を持つカードに
- 自動的に `chain` elementを追加

```typescript
function applyMageChainElement(card: Card, playerClass: CharacterClass): Card {
  if (playerClass !== "mage") return card;

  const hasMagicElement = card.element.some(e => MAGIC_ELEMENTS.has(e));
  if (hasMagicElement && !card.element.includes("chain")) {
    return { ...card, element: [...card.element, "chain"] };
  }
  return card;
}
```

---

## 3. 影響範囲

### 3.1 型定義ファイル

| ファイル | 変更内容 |
|---------|---------|
| `src/types/cardTypes.ts` | `CardCategory`, `Rarity` 削除。Card interfaceから`category`, `rarity`削除 |
| `src/types/characterTypes.ts` | `ElementType`に`attack`, `classAbility`, `chain`追加 |
| `src/types/campTypes.ts` | `CardFilterOptions`からrarity削除、category→element変更 |

### 3.2 定数ファイル

| ファイル | 変更内容 |
|---------|---------|
| `src/constants/cardConstants.ts` | `CARD_CATEGORY_NAMES`, `RARITY_COLORS`削除。`ELEMENT_LABEL_MAP`, `ELEMENT_COLOR_MAP`に新element追加 |

### 3.3 カードデータファイル

| ファイル | 変更内容 |
|---------|---------|
| `src/constants/data/cards/swordmanCards.ts` | 全カードから`category`, `rarity`削除。`element`配列に機能分類追加。`derivedFrom: null`追加 |
| `src/constants/data/cards/mageCards.ts` | 同上 |
| `src/constants/data/cards/summonerCards.ts` | 同上 |
| `src/constants/data/cards/cardDerivationRegistry.ts` | 既存カードの一部を派生カードとして登録 |

### 3.4 ロジックファイル

| ファイル | 変更内容 |
|---------|---------|
| `src/domain/cards/logic/cardUtils.ts` | `isBaseCard()`, `isCardUnlocked()`追加 |
| `src/domain/cards/logic/elementUtils.ts` | 新規作成。`applyMageChainElement()`等 |
| `src/domain/cards/state/masteryManager.ts` | 変更なし（既存利用） |

### 3.5 UIファイル

| ファイル | 変更内容 |
|---------|---------|
| `src/ui/html/dungeonHtml/preparations/DeckTab.tsx` | 解放状態フィルタリング追加、ロックカードのグレーアウト表示 |
| `src/ui/html/campsHtml/Library/CardEncyclopediaTab.tsx` | rarityフィルター削除、categoryフィルター→elementフィルター変更 |

### 3.6 category/rarityを参照する全ファイル（要確認・修正）

```
src/domain/characters/player/logic/swordEnergySystem.ts
src/domain/characters/enemy/logic/enemyAI.ts
src/constants/data/camps/ShopData.ts
src/constants/data/camps/CardEncyclopediaData.ts
src/domain/cards/state/card.ts
src/domain/cards/state/CardHandle.ts
```

---

## 4. 実装フェーズ

### Phase 1: 型定義の変更

1. `characterTypes.ts`: ElementTypeに新element追加
2. `cardTypes.ts`: CardCategory, Rarity型削除、Card interfaceから該当フィールド削除
3. `campTypes.ts`: CardFilterOptions更新

### Phase 2: 定数の更新

1. `cardConstants.ts`:
   - CARD_CATEGORY_NAMES, RARITY_COLORS削除
   - ELEMENT_LABEL_MAP, ELEMENT_COLOR_MAP更新
   - FUNCTIONAL_ELEMENTS, CLASS_ABILITY_ELEMENTS追加

### Phase 3: カードデータの更新

1. 全カードに`derivedFrom: null`追加（ベースカード化）
2. 全カードから`category`, `rarity`削除
3. 全カードの`element`配列に機能分類を追加
4. 一部カードを派生カードとして再定義（derivedFrom設定）

### Phase 4: ロジック実装

1. `cardUtils.ts`に追加:
   ```typescript
   isBaseCard(card: Card): boolean
   isCardUnlocked(card: Card, masteryStore: MasteryStore): boolean
   getUnlockedCards(allCards: Card[], masteryStore: MasteryStore): Card[]
   ```

2. `elementUtils.ts`新規作成:
   ```typescript
   applyMageChainElement(card: Card, playerClass: CharacterClass): Card
   hasElement(card: Card, element: ElementType): boolean
   getFunctionalElement(card: Card): ElementType | null
   ```

### Phase 5: UI更新

1. `DeckTab.tsx`:
   - 解放済みカードのみ選択可能に
   - 未解放カードをグレーアウト+ロックアイコン表示
   - elementベースのフィルタリング

2. `CardEncyclopediaTab.tsx`:
   - rarityフィルター削除
   - categoryフィルター→elementフィルター変更

### Phase 6: セーブデータ連携

1. `PlayerProgression`に`cardMastery: Record<string, number>`追加
2. ゲーム開始時はマスタリー0で初期化
3. バトル終了時にマスタリー情報を保存

---

## 5. 派生カードサンプル設計

既存カードの一部を派生カードとして再定義する例：

### Swordsman

```typescript
// ベースカード
sw_001: { name: "迅雷斬", derivedFrom: null, ... }

// 派生カード（sw_001のマスタリー2で解放）
sw_001_ex: {
  name: "迅雷斬・極",
  derivedFrom: "sw_001",
  unlockMasteryLevel: 2,
  ...
}
```

### 派生レジストリ例

```typescript
// cardDerivationRegistry.ts
export const SWORDSMAN_DERIVATIONS: DerivationEntry[] = [
  { parentCardTypeId: "sw_001", derivedCardTypeId: "sw_001_ex", requiredMastery: 2 },
  { parentCardTypeId: "sw_007", derivedCardTypeId: "sw_007_ex", requiredMastery: 3 },
];
```

---

## 6. UI仕様

### 6.1 デッキ構築画面

| カード状態 | 表示 | 操作 |
|-----------|------|------|
| 解放済み | 通常表示 | 選択可能 |
| 未解放（派生カード） | グレーアウト + ロックアイコン | 選択不可 |
| 未設定（derivedFrom: undefined） | 非表示 or グレーアウト | 選択不可 |

### 6.2 カード図鑑

- elementでフィルタリング可能
- 未解放カードも表示（解放条件を確認できるように）

---

## 7. 新定数定義

```typescript
// cardConstants.ts に追加

/** 機能分類Element */
export const FUNCTIONAL_ELEMENTS: ReadonlySet<ElementType> = new Set([
  "attack", "guard", "buff", "debuff", "heal",
]);

/** クラス固有Element */
export const CLASS_ABILITY_ELEMENTS: ReadonlySet<ElementType> = new Set([
  "classAbility", "chain",
]);

/** Element日本語表示名（追加分） */
// ELEMENT_LABEL_MAP に追加
attack: "攻撃",
classAbility: "固有技",
chain: "連鎖",

/** Elementカラー（追加分） */
// ELEMENT_COLOR_MAP に追加
attack: "#ff6b6b",
classAbility: "#ffd93d",
chain: "#6bcb77",
```

---

## 8. テスト項目

### 8.1 単体テスト

- [ ] `isBaseCard()`: derivedFrom: null → true
- [ ] `isBaseCard()`: derivedFrom: undefined → false
- [ ] `isBaseCard()`: derivedFrom: "sw_001" → false
- [ ] `isCardUnlocked()`: ベースカード → true
- [ ] `isCardUnlocked()`: 派生カード（親マスタリー不足） → false
- [ ] `isCardUnlocked()`: 派生カード（親マスタリー達成） → true
- [ ] `applyMageChainElement()`: Mage + fire element → chain追加
- [ ] `applyMageChainElement()`: Swordsman + fire element → chain追加なし

### 8.2 統合テスト

- [ ] デッキ構築画面でベースカードのみ選択可能
- [ ] マスタリー上昇で派生カードが解放される
- [ ] 図鑑でelementフィルタリングが機能する
- [ ] セーブ/ロードでマスタリー情報が保持される

---

## 9. 注意事項

1. **破壊的変更**: category, rarityフィールド削除は既存コードに影響大
2. **データ移行**: 全カードデータの更新が必要
3. **段階的実装推奨**: Phase 1-2（型・定数）→ Phase 3（データ）→ Phase 4-5（ロジック・UI）の順で

---

## 10. 未決定事項

- [ ] derivedFrom: undefined のカードの最終的な扱い（エラー表示 or 非表示）
- [ ] 派生カードとして再定義する既存カードの具体的な選定
- [ ] 派生カードの解放通知UI

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2026-02-02 | 初版作成 |
