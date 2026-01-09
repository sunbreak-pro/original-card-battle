# 取引所詳細設計書 (SHOP_DESIGN_V1)

## 更新履歴

- V1.0: 初版作成（魔石レート調整、セールシステム、装備パック仕様確定）

---

## 1. 概要

取引所（Merchant's Exchange）は、冒険者がダンジョン探索で得た戦利品を資金（Gold）に換え、次なる冒険のための物資や装備を調達する経済活動の中心地です。

### 主な役割

1. **購入 (Buy)**: 消耗品、転移石、装備パック（ガチャ要素）の購入
2. **売却 (Sell)**: 不要な装備や収集アイテムの換金
3. **魔石取引 (Exchange)**: 希少資源である「魔石」の換金（魔石 → Gold）
4. **日替わりセール (Daily Sales)**: 特定商品が割引される

---

## 2. 詳細機能仕様

### 2.1 購入システム (Buying)

商品は以下の 3 カテゴリに分類して表示します。

#### 2.1.1 消耗品 (Consumables)

**基本仕様:**

- ポーション類、状態異常回復薬、バフアイテム
- 在庫: 無限（Phase 1）
- セール枠は一回の探索ごとに２アイテムが対象(成長ツリーによりアイテム数増加)

**主要商品例:**

```typescript
// ポーション（小）
{
  id: "shop_potion_small",
  name: "小回復ポーション",
  type: "consumable",
  basePrice: 50,
  effect: "HP+30回復"
}

// ポーション（中）
{
  id: "shop_potion_medium",
  name: "中回復ポーション",
  type: "consumable",
  basePrice: 120,
  effect: "HP+70回復"
}
```

#### 2.1.2 転移石 (Teleport Stones)

**基本仕様:**

- `return_system.md` に基づき、常に在庫を確保
- 3 種類を常時販売
- 日替わりセールの対象

**商品定義:**

```typescript
{
  id: "shop_teleport_normal",
  name: "転移石（通常）",
  type: "teleport",
  basePrice: 150,
  effect: "70%の確率で帰還"
}

{
  id: "shop_teleport_blessed",
  name: "転移石（祝福）",
  type: "teleport",
  basePrice: 300,
  effect: "80%の確率で帰還"
}

{
  id: "shop_teleport_emergency",
  name: "転移石（緊急）",
  type: "teleport",
  basePrice: 100,
  effect: "60%の確率で帰還"
}
```

#### 2.1.3 装備パック (Equipment Packs)

**基本仕様:**

- 中身がランダムな「袋」
- 購入時に即時開封され、インベントリに追加
- **1 パックで装備スロット 6 種類すべてが出現**（weapon, armor, helmet, boots, accessory1, accessory2）
- 各スロットから 1 個ずつ、合計 6 個の装備を獲得

**パック種類と確率:**

| パック名       | 価格  | 保証レアリティ | Common | Rare | Epic | Legendary |
| -------------- | ----- | -------------- | ------ | ---- | ---- | --------- |
| コモンパック   | 300G  | Common         | 100%   | 0%   | 0%   | 0%        |
| レアパック     | 500G  | Rare 以上      | 60%    | 35%  | 5%   | 0%        |
| エピックパック | 1000G | Epic 以上      | 30%    | 45%  | 20%  | 5%        |

**抽選ロジック:**

```typescript
interface EquipmentPack {
  id: string;
  name: string;
  basePrice: number;
  guaranteedRarity: 'common' | 'rare' | 'epic';
  probabilities: {
    common: number;
    rare: number;
    epic: number;
    legendary: number;
  };
}

// 例：レアパック
{
  id: "shop_pack_rare",
  name: "レア装備パック",
  basePrice: 500,
  guaranteedRarity: "rare",
  probabilities: {
    common: 0.60,
    rare: 0.35,
    epic: 0.05,
    legendary: 0.00
  }
}

// 開封時の処理
function openEquipmentPack(pack: EquipmentPack): Item[] {
  const slots: EquipmentSlot[] = ['weapon', 'armor', 'helmet', 'boots', 'accessory1', 'accessory2'];
  const items: Item[] = [];

  for (const slot of slots) {
    const rarity = rollRarity(pack.probabilities);
    const equipment = createRandomEquipment(slot, rarity);
    items.push(equipment);
  }

  return items; // 6個の装備を返す
}
```

---

### 2.2 売却システム (Selling)

**基本仕様:**

- プレイヤーのインベントリにあるアイテムを売却
- **装備中のアイテムは売却不可**（リストから除外）
- 売却価格は Item.sellPrice をそのまま使用

**売却可能アイテムのフィルタリング:**

```typescript
// 装備中のアイテムIDリスト
const equippedIds = getEquippedIds(); // ["weapon_001", "armor_003", ...]

// 売却可能アイテム
const sellableItems = items.filter((item) => {
  if (!item.canSell) return false; // 売却不可フラグ
  if (item.itemType === "equipment" && equippedIds.includes(item.id)) {
    return false; // 装備中は除外
  }
  return true;
});
```

**売却処理:**

```typescript
const handleSell = (item: Item) => {
  // 確認ダイアログ表示
  if (!confirm(`${item.name} を ${item.sellPrice}G で売却しますか？`)) {
    return;
  }

  // アイテム削除
  removeItem(item.id);

  // Gold加算
  addGold(item.sellPrice);

  // 売却エフェクト
  playSellAnimation();
};
```

**一括売却（将来拡張）:**

```typescript
// Phase 1では未実装
// Phase 2以降で「コモン装備をすべて選択」などのフィルタ機能を追加
```

---

### 2.3 魔石取引 (Magic Stone Exchange)

**基本仕様:**

- 魔石アイテムを Gold に換金
- 3 種類の魔石で異なるレート

**魔石レート:**

```typescript
const MAGIC_STONE_RATES = {
  magic_stone_small: 30, // 魔石（小）: 30G
  magic_stone_medium: 100, // 魔石（中）: 100G
  magic_stone_large: 350, // 魔石（大）: 350G
};
```

**UI 設計:**

- 所持魔石の総価値を表示
- スライダーまたは入力ボックスで換金する魔石の個数を指定
- 価値の低いものから順に消費

**換金処理:**

```typescript
const handleExchangeMagicStones = (targetValue: number) => {
  const magicStones = items
    .filter((item) => item.itemType === "magicStone")
    .sort((a, b) => (a.magicStoneValue || 0) - (b.magicStoneValue || 0)); // 価値の低い順

  let remaining = targetValue;
  const toRemove: string[] = [];

  for (const stone of magicStones) {
    if (remaining <= 0) break;

    const stoneValue = stone.magicStoneValue || 0;
    const count = stone.stackCount || 1;
    const totalValue = stoneValue * count;

    if (totalValue <= remaining) {
      // この魔石を全て消費
      remaining -= totalValue;
      toRemove.push(stone.id);
    } else {
      // 一部だけ消費
      const needCount = Math.ceil(remaining / stoneValue);
      remaining = 0;

      // スタック数を減らす
      updateItemStack(stone.id, count - needCount);
    }
  }

  // 魔石を削除
  toRemove.forEach((id) => removeItem(id));

  // Gold加算
  addGold(targetValue);

  // エフェクト
  playExchangeAnimation();
};
```

---

### 2.4 日替わりセール (Daily Sales)

**トリガー条件:**

- 帰還時（BaseCamp に戻った時）にセール内容を更新
- ダンジョンに入る時に `saleTiming = false` にリセット

**セール対象:**

- ランダムなカテゴリ（消耗品 / 転移石 / 装備パック）
- または特定の商品
- **Epic 以上の装備パックはセール対象外**

**割引率:**

- 10% ~ 30% OFF（ランダム）

**データ構造:**

```typescript
interface DailySale {
  targetCategory?: "consumable" | "teleport" | "equipment_pack";
  targetItemId?: string; // 特定商品指定の場合
  discountRate: number; // 0.1 = 10% OFF
  excludeRarities?: string[]; // ['epic', 'legendary'] = Epic以上は対象外
}

// GameStateContextに追加
interface GameState {
  // ... 既存フィールド
  encounterCount: number; // ✨ 新規
  saleTiming: boolean; // ✨ 新規
  currentSale: DailySale | null; // ✨ 新規
}
```

**セール生成ロジック:**

```typescript
function generateDailySale(): DailySale {
  const patterns = [
    // パターン1: カテゴリ全体
    { targetCategory: "consumable", discountRate: 0.2 },
    { targetCategory: "teleport", discountRate: 0.15 },
    {
      targetCategory: "equipment_pack",
      discountRate: 0.1,
      excludeRarities: ["epic", "legendary"], // ✅ Epic以上は除外
    },

    // パターン2: 特定商品
    { targetItemId: "shop_potion_large", discountRate: 0.3 },
    { targetItemId: "shop_teleport_blessed", discountRate: 0.25 },
  ];

  return patterns[Math.floor(Math.random() * patterns.length)];
}
```

**価格計算:**

```typescript
function calculatePrice(
  basePrice: number,
  sale: DailySale | null,
  item: ShopItem
): number {
  if (!sale) return basePrice;

  // Epic以上の装備パックはセール除外
  if (
    item.type === "equipment_pack" &&
    ["epic", "legendary"].includes(item.guaranteedRarity)
  ) {
    return basePrice;
  }

  // カテゴリセール
  if (sale.targetCategory && sale.targetCategory === item.type) {
    return Math.floor(basePrice * (1 - sale.discountRate));
  }

  // 特定商品セール
  if (sale.targetItemId && sale.targetItemId === item.id) {
    return Math.floor(basePrice * (1 - sale.discountRate));
  }

  return basePrice;
}
```

---

## 3. UI/UX デザイン

### 3.1 画面レイアウト

```
┌────────────────────────────────────────────────┐
│  🏪 取引所 - Merchant's Exchange              │
├────────────────────────────────────────────────┤
│                                                │
│  所持金: 1,250 G  魔石価値: 450 G             │
│                                                │
│  [購入(Buy)] [売却(Sell)] [魔石取引(Exchange)] │
│  ═════════  ──────  ───────────────            │
│                                                │
│  ┌──────────────────────────────────────────┐ │
│  │                                          │ │
│  │   選択中のタブの内容                     │ │
│  │                                          │ │
│  │   [消耗品] [転移石] [装備パック]         │ │
│  │                                          │ │
│  │   ┌──────┐ ┌──────┐ ┌──────┐            │ │
│  │   │商品1 │ │商品2 │ │商品3 │            │ │
│  │   │SALE! │ │      │ │      │            │ │
│  │   │100G  │ │150G  │ │300G  │            │ │
│  │   └──────┘ └──────┘ └──────┘            │ │
│  │                                          │ │
│  └──────────────────────────────────────────┘ │
│                                                │
│  [キャンプに戻る]                              │
│                                                │
└────────────────────────────────────────────────┘
```

### 3.2 購入タブ（Buy）

**カテゴリ選択:**

```
┌─────────────────────────────────────────────┐
│ [消耗品] [転移石] [装備パック]              │
│ ═══════  ────────  ────────────              │
└─────────────────────────────────────────────┘
```

**商品グリッド:**

```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  🧪          │  │  🧪          │  │  🧪          │
│ 小回復ポーション│  │ 中回復ポーション│  │ 大回復ポーション│
│              │  │              │  │  SALE! 20%  │
│   50 G       │  │  120 G       │  │  192 G      │
│  [購入]      │  │  [購入]      │  │  [購入]     │
└──────────────┘  └──────────────┘  └──────────────┘
```

**セール表示:**

- SALE バッジ（赤背景）
- 元の価格に取り消し線
- 割引後の価格を大きく表示

### 3.3 売却タブ（Sell）

**インベントリグリッド:**

```
所持アイテム:

┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  ⚔️         │  │  🛡️         │  │  👑          │
│ 剣士の剣     │  │ 戦士の鎧     │  │ 魔術士の帽子 │
│ (装備中)     │  │              │  │              │
│  - G        │  │  80 G       │  │  120 G      │
│  [---]      │  │  [売却]     │  │  [売却]     │
└──────────────┘  └──────────────┘  └──────────────┘
         ↑ 装備中は売却不可
```

**売却確認ポップアップ:**

```
┌─────────────────────────────────┐
│  戦士の鎧 を 80G で売却しますか？│
│                                 │
│  [はい]  [いいえ]               │
└─────────────────────────────────┘
```

### 3.4 魔石取引タブ（Exchange）

**魔石リスト:**

```
所持魔石:

魔石（小） x 10  = 300 G
魔石（中） x  3  = 300 G
魔石（大） x  1  = 350 G
───────────────────────────
合計価値         = 950 G

換金する価値: [______] G  (最大: 950G)
          または
[小 ▼▼] [中 ▼▼] [大 ▼▼]  個数指定

換金後の獲得Gold: 450 G

[換金する]  [キャンセル]
```

### 3.5 演出 (Feedback)

**購入成功:**

```
効果音: チャリーン♪（コインの音）
アニメーション:
  1. 商品カードがズームアップ
  2. 袋に入る
  3. プレイヤーの方へ飛んでくる
```

**装備パック開封:**

```
演出フロー:
1. 購入ボタン押下
   ↓
2. 画面中央に袋が出現
   ↓
3. ガタガタ揺れる（1秒）
   ↓
4. 光と共に装備アイコンが6個出現
   （レアリティに応じた光の色）
   - Common: 白
   - Rare: 青
   - Epic: 紫
   - Legendary: 金
   ↓
5. 各装備を順番に表示（0.5秒間隔）
   ↓
6. インベントリに追加完了
```

**魔石換金:**

```
効果音: パリーン♪（ガラスが割れる音）
アニメーション:
  1. 魔石が砕ける
  2. 金貨に変わる
  3. Goldカウンターがカウントアップ
```

---

## 4. データ構造定義

### 4.1 ShopTypes.ts

```typescript
// src/types/ShopTypes.ts (新規作成)

import type { ItemType, EquipmentSlot } from "./ItemTypes";

/**
 * ショップ商品データ
 */
export interface ShopItem {
  id: string; // 商品ID
  targetItemId?: string; // 実際のアイテムID（装備パック以外）
  name: string;
  description: string;
  type: "consumable" | "teleport" | "equipment_pack";
  basePrice: number;
  icon: string;

  // 装備パック用設定
  packConfig?: EquipmentPackConfig;
}

export interface EquipmentPackConfig {
  guaranteedRarity: "common" | "rare" | "epic";
  probabilities: {
    common: number;
    rare: number;
    epic: number;
    legendary: number;
  };
}

/**
 * セール情報
 */
export interface DailySale {
  targetCategory?: "consumable" | "teleport" | "equipment_pack";
  targetItemId?: string;
  discountRate: number; // 0.1 = 10% OFF
  excludeRarities?: ("epic" | "legendary")[]; // セール除外レアリティ
}

/**
 * 商品カテゴリ
 */
export type ShopCategory = "consumable" | "teleport" | "equipment_pack";

/**
 * 魔石換金情報
 */
export interface MagicStoneExchange {
  totalValue: number; // 所持魔石の総価値
  breakdown: {
    typeId: string;
    count: number;
    unitValue: number;
    totalValue: number;
  }[];
}
```

### 4.2 GameStateContext の拡張

```typescript
// src/contexts/GameStateContext.tsx (修正)

export interface GameState {
  currentScreen: GameScreen;
  battleMode: BattleMode;
  depth: Depth;
  encounterCount: number; // ✨ 新規: 戦闘回数
  battleConfig?: BattleConfig;

  // Shop用
  saleTiming: boolean; // ✨ 新規: セール更新フラグ
  currentSale: DailySale | null; // ✨ 新規: 現在のセール
}

// 初期値
const initialGameState: GameState = {
  currentScreen: "camp",
  battleMode: null,
  depth: 1,
  encounterCount: 0, // ✨ 0から開始
  saleTiming: false, // ✨ 初期はfalse
  currentSale: null, // ✨ セールなし
};
```

---

## 5. データファイル定義

### 5.1 ShopData.ts

```typescript
// src/camps/facilities/Shop/data/ShopData.ts (新規作成)

import type { ShopItem } from "../../../../types/ShopTypes";

/**
 * 消耗品カテゴリ
 */
export const CONSUMABLE_ITEMS: ShopItem[] = [
  {
    id: "shop_potion_small",
    targetItemId: "potion_small",
    name: "小回復ポーション",
    description: "HP+30回復",
    type: "consumable",
    basePrice: 50,
    icon: "🧪",
  },
  {
    id: "shop_potion_medium",
    targetItemId: "potion_medium",
    name: "中回復ポーション",
    description: "HP+70回復",
    type: "consumable",
    basePrice: 120,
    icon: "🧪",
  },
  {
    id: "shop_potion_large",
    targetItemId: "potion_large",
    name: "大回復ポーション",
    description: "HP+150回復",
    type: "consumable",
    basePrice: 240,
    icon: "🧪",
  },
  // ... 他の消耗品
];

/**
 * 転移石カテゴリ
 */
export const TELEPORT_ITEMS: ShopItem[] = [
  {
    id: "shop_teleport_normal",
    targetItemId: "teleport_normal",
    name: "転移石（通常）",
    description: "70%の確率で帰還",
    type: "teleport",
    basePrice: 150,
    icon: "🔮",
  },
  {
    id: "shop_teleport_blessed",
    targetItemId: "teleport_blessed",
    name: "転移石（祝福）",
    description: "80%の確率で帰還",
    type: "teleport",
    basePrice: 300,
    icon: "✨",
  },
  {
    id: "shop_teleport_emergency",
    targetItemId: "teleport_emergency",
    name: "転移石（緊急）",
    description: "60%の確率で帰還（低コスト）",
    type: "teleport",
    basePrice: 100,
    icon: "⚡",
  },
];

/**
 * 装備パックカテゴリ
 */
export const EQUIPMENT_PACKS: ShopItem[] = [
  {
    id: "shop_pack_common",
    name: "コモン装備パック",
    description: "6個の装備（Common確定）",
    type: "equipment_pack",
    basePrice: 300,
    icon: "📦",
    packConfig: {
      guaranteedRarity: "common",
      probabilities: {
        common: 1.0,
        rare: 0.0,
        epic: 0.0,
        legendary: 0.0,
      },
    },
  },
  {
    id: "shop_pack_rare",
    name: "レア装備パック",
    description: "6個の装備（Rare以上確定）",
    type: "equipment_pack",
    basePrice: 500,
    icon: "📦",
    packConfig: {
      guaranteedRarity: "rare",
      probabilities: {
        common: 0.6,
        rare: 0.35,
        epic: 0.05,
        legendary: 0.0,
      },
    },
  },
  {
    id: "shop_pack_epic",
    name: "エピック装備パック",
    description: "6個の装備（Epic以上確定）",
    type: "equipment_pack",
    basePrice: 1000,
    icon: "📦",
    packConfig: {
      guaranteedRarity: "epic",
      probabilities: {
        common: 0.3,
        rare: 0.45,
        epic: 0.2,
        legendary: 0.05,
      },
    },
  },
];

/**
 * 全商品リスト
 */
export const ALL_SHOP_ITEMS: ShopItem[] = [
  ...CONSUMABLE_ITEMS,
  ...TELEPORT_ITEMS,
  ...EQUIPMENT_PACKS,
];

/**
 * カテゴリ別商品取得
 */
export function getItemsByCategory(category: ShopCategory): ShopItem[] {
  switch (category) {
    case "consumable":
      return CONSUMABLE_ITEMS;
    case "teleport":
      return TELEPORT_ITEMS;
    case "equipment_pack":
      return EQUIPMENT_PACKS;
    default:
      return [];
  }
}
```

### 5.2 MagicStoneData.ts

```typescript
// src/items/data/MagicStoneData.ts (新規作成)

import type { Item } from "../../types/ItemTypes";

/**
 * 魔石アイテムデータ
 */
export const MAGIC_STONE_ITEMS: Item[] = [
  {
    id: "magic_stone_small_001", // 実際のID（インスタンス用）
    typeId: "magic_stone_small", // タイプID（種類識別用）
    name: "魔石（小）",
    description: "わずかな魔力を帯びた小さな石",
    itemType: "magicStone",
    icon: "💎",
    magicStoneValue: 30, // ✅ 30G
    rarity: "common",
    sellPrice: 30, // 売却価格 = 魔石価値
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1,
  },
  {
    id: "magic_stone_medium_001",
    typeId: "magic_stone_medium",
    name: "魔石（中）",
    description: "ほのかに光る魔石",
    itemType: "magicStone",
    icon: "💎",
    magicStoneValue: 100, // ✅ 100G
    rarity: "uncommon",
    sellPrice: 100,
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1,
  },
  {
    id: "magic_stone_large_001",
    typeId: "magic_stone_large",
    name: "魔石（大）",
    description: "強い魔力を放つ貴重な魔石",
    itemType: "magicStone",
    icon: "💎",
    magicStoneValue: 350, // ✅ 350G
    rarity: "rare",
    sellPrice: 350,
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1,
  },
];

/**
 * 魔石の換金レート定義
 */
export const MAGIC_STONE_RATES: Record<string, number> = {
  magic_stone_small: 30,
  magic_stone_medium: 100,
  magic_stone_large: 350,
};

/**
 * 魔石の総価値を計算
 */
export function calculateMagicStoneValue(items: Item[]): number {
  return items
    .filter((item) => item.itemType === "magicStone")
    .reduce((sum, item) => {
      const value = item.magicStoneValue || 0;
      const count = item.stackCount || 1;
      return sum + value * count;
    }, 0);
}
```

---

## 6. 実装手順書

### Phase 1: データ準備（Week 1: Day 1-2）

**Task 1.1: 型定義作成**

```
□ src/types/ShopTypes.ts 作成
  □ ShopItem型
  □ DailySale型
  □ MagicStoneExchange型
```

**Task 1.2: GameStateContext 拡張**

```
□ src/contexts/GameStateContext.tsx 修正
  □ encounterCount追加
  □ saleTiming追加
  □ currentSale追加
  □ incrementEncounterCount関数
  □ updateSale関数
```

**Task 1.3: 商品データ作成**

```
□ src/camps/facilities/Shop/data/ShopData.ts 作成
  □ CONSUMABLE_ITEMS定義
  □ TELEPORT_ITEMS定義
  □ EQUIPMENT_PACKS定義
  □ getItemsByCategory関数
```

**Task 1.4: 魔石データ作成**

```
□ src/items/data/MagicStoneData.ts 作成
  □ MAGIC_STONE_ITEMS定義
  □ MAGIC_STONE_RATES定義
  □ calculateMagicStoneValue関数
```

---

### Phase 2: Shop コンポーネント実装（Week 1-2: Day 3-7）

**Task 2.1: Shop.tsx 骨組み**

```typescript
// src/camps/facilities/Shop/Shop.tsx

import { useState } from "react";
import { usePlayer } from "../../../contexts/PlayerContext";
import { useGameState } from "../../../contexts/GameStateContext";
import { useInventory } from "../../../contexts/InventoryContext";
import BuyTab from "./BuyTab";
import SellTab from "./SellTab";
import ExchangeTab from "./ExchangeTab";
import "./Shop.css";

type ShopTab = "buy" | "sell" | "exchange";

const Shop: React.FC = () => {
  const [activeTab, setActiveTab] = useState<ShopTab>("buy");
  const { returnToCamp } = useGameState();

  return (
    <div className="shop-screen">
      <header className="shop-header">
        <h1>🏪 取引所 - Merchant's Exchange</h1>
        <div className="resources">
          <PlayerResources />
        </div>
      </header>

      <nav className="shop-tabs">
        <button
          className={activeTab === "buy" ? "active" : ""}
          onClick={() => setActiveTab("buy")}
        >
          購入 (Buy)
        </button>
        <button
          className={activeTab === "sell" ? "active" : ""}
          onClick={() => setActiveTab("sell")}
        >
          売却 (Sell)
        </button>
        <button
          className={activeTab === "exchange" ? "active" : ""}
          onClick={() => setActiveTab("exchange")}
        >
          魔石取引 (Exchange)
        </button>
      </nav>

      <div className="shop-content">
        {activeTab === "buy" && <BuyTab />}
        {activeTab === "sell" && <SellTab />}
        {activeTab === "exchange" && <ExchangeTab />}
      </div>

      <button className="back-button" onClick={returnToCamp}>
        キャンプに戻る
      </button>
    </div>
  );
};

export default Shop;
```

**Task 2.2: BuyTab 実装**

```
□ src/camps/facilities/Shop/BuyTab.tsx 作成
  □ カテゴリ選択UI
  □ 商品グリッド表示
  □ セール価格計算
  □ 購入処理
  □ 装備パック開封（Phase 1では固定装備）
```

**Task 2.3: SellTab 実装**

```
□ src/camps/facilities/Shop/SellTab.tsx 作成
  □ インベントリ表示
  □ 装備中フィルタリング
  □ 売却確認ダイアログ
  □ 売却処理
```

**Task 2.4: ExchangeTab 実装**

```
□ src/camps/facilities/Shop/ExchangeTab.tsx 作成
  □ 魔石リスト表示
  □ 換金額入力UI
  □ 換金処理
```

---

### Phase 3: 装備生成システム（Week 2: Day 1-3）

**Task 3.1: equipmentGenerator.ts 作成**

```typescript
// src/items/utils/equipmentGenerator.ts (新規作成)

import type { Item, EquipmentSlot } from "../../types/ItemTypes";

/**
 * ランダムな装備を生成
 */
export function createRandomEquipment(
  slot: EquipmentSlot,
  rarity: "common" | "rare" | "epic" | "legendary"
): Item {
  // Phase 1: 仮実装（固定装備を返す）
  // Phase 2: EQUIPMENT_AND_ITEMS_DESIGN.mdから抽選

  const equipmentPool = getEquipmentPoolBySlotAndRarity(slot, rarity);
  const template = selectRandom(equipmentPool);

  return {
    id: generateUniqueId(),
    typeId: template.id,
    name: template.name,
    description: template.description,
    itemType: "equipment",
    icon: template.icon,
    equipmentSlot: slot,
    durability: template.maxDurability,
    maxDurability: template.maxDurability,
    effects: template.effects,
    rarity: rarity,
    sellPrice: template.sellPrice,
    canSell: true,
    canDiscard: false,
  };
}

/**
 * レアリティ抽選
 */
export function rollRarity(probabilities: {
  common: number;
  rare: number;
  epic: number;
  legendary: number;
}): "common" | "rare" | "epic" | "legendary" {
  const roll = Math.random();
  let cumulative = 0;

  for (const [rarity, prob] of Object.entries(probabilities)) {
    cumulative += prob;
    if (roll < cumulative) {
      return rarity as "common" | "rare" | "epic" | "legendary";
    }
  }

  return "common"; // フォールバック
}

/**
 * 装備パック開封
 */
export function openEquipmentPack(pack: EquipmentPackConfig): Item[] {
  const slots: EquipmentSlot[] = [
    "weapon",
    "armor",
    "helmet",
    "boots",
    "accessory1",
    "accessory2",
  ];

  const items: Item[] = [];

  for (const slot of slots) {
    const rarity = rollRarity(pack.probabilities);
    const equipment = createRandomEquipment(slot, rarity);
    items.push(equipment);
  }

  return items; // 6個の装備
}
```

**Task 3.2: EquipmentData.ts 作成（Phase 1: 簡易版）**

```
□ src/items/data/EquipmentData.ts 作成
  □ 各スロット×レアリティの基本装備定義
  □ EQUIPMENT_AND_ITEMS_DESIGN.mdから抜粋
  □ 不完全なデータは後回し
```

---

### Phase 4: セールシステム統合（Week 2: Day 4-5）

**Task 4.1: セール生成ロジック**

```typescript
// src/camps/facilities/Shop/utils/saleGenerator.ts

import type { DailySale } from "../../../../types/ShopTypes";

export function generateDailySale(): DailySale {
  const patterns: DailySale[] = [
    {
      targetCategory: "consumable",
      discountRate: 0.2,
    },
    {
      targetCategory: "teleport",
      discountRate: 0.15,
    },
    {
      targetCategory: "equipment_pack",
      discountRate: 0.1,
      excludeRarities: ["epic", "legendary"], // ✅ Epic以上除外
    },
    // 特定商品
    {
      targetItemId: "shop_potion_large",
      discountRate: 0.3,
    },
    {
      targetItemId: "shop_teleport_blessed",
      discountRate: 0.25,
    },
  ];

  return patterns[Math.floor(Math.random() * patterns.length)];
}

export function calculateDiscountedPrice(
  basePrice: number,
  sale: DailySale | null,
  item: ShopItem
): number {
  if (!sale) return basePrice;

  // Epic以上の装備パックは除外
  if (item.type === "equipment_pack" && item.packConfig) {
    if (["epic", "legendary"].includes(item.packConfig.guaranteedRarity)) {
      return basePrice;
    }
  }

  // カテゴリセール
  if (sale.targetCategory === item.type) {
    return Math.floor(basePrice * (1 - sale.discountRate));
  }

  // 特定商品セール
  if (sale.targetItemId === item.id) {
    return Math.floor(basePrice * (1 - sale.discountRate));
  }

  return basePrice;
}
```

**Task 4.2: BattleScreen との連携**

```
□ BattleScreen.tsx修正
  □ 戦闘終了時にencounterCountをインクリメント
  □ encounterCount >= 3 で saleTiming = true
```

**Task 4.3: BaseCamp との連携**

```
□ BaseCamp.tsx修正
  □ マウント時にsaleTimingをチェック
  □ trueならセール更新
  □ Dungeon施設クリック時にsaleTiming = false
```

---

### Phase 5: UI/アニメーション（Week 3: Day 1-3）

**Task 5.1: CSS 実装**

```
□ Shop.css作成
  □ 商品グリッドレイアウト
  □ SALEバッジスタイル
  □ ホバーエフェクト
```

**Task 5.2: アニメーション実装**

```
□ 購入エフェクト
  □ コインアニメーション
  □ アイテム獲得演出

□ 装備パック開封演出
  □ 袋が揺れるアニメーション
  □ 光のエフェクト
  □ 装備アイコン出現

□ 魔石換金エフェクト
  □ 魔石が砕けるアニメーション
  □ Goldカウントアップ
```

---

## 7. Context API との連携

### 7.1 PlayerContext

```typescript
// 使用する関数
const { player, addGold, useGold } = usePlayer();

// 購入時
if (useGold(price)) {
  // 購入成功
} else {
  // Gold不足
}

// 売却時
addGold(item.sellPrice);
```

### 7.2 InventoryContext

```typescript
// 使用する関数
const {
  items,
  addItem,
  removeItem,
  getEquippedIds,
  getMagicStones,
  useMagicStones,
} = useInventory();

// 購入時
addItem(newItem);

// 売却時
removeItem(itemId);

// 装備中チェック
const equippedIds = getEquippedIds();
const isEquipped = equippedIds.includes(item.id);

// 魔石換金
const totalMagicStoneValue = getMagicStones();
useMagicStones(350); // 350G分の魔石を消費
```

### 7.3 GameStateContext

```typescript
// 使用する関数
const { gameState, setGameState, returnToCamp } = useGameState();

// セール確認
const { currentSale, saleTiming, encounterCount } = gameState;

// 戦闘回数インクリメント（BattleScreenで実行）
setGameState((prev) => ({
  ...prev,
  encounterCount: prev.encounterCount + 1,
  saleTiming: prev.encounterCount + 1 >= 3,
}));

// セール更新（BaseCampで実行）
if (saleTiming) {
  const newSale = generateDailySale();
  setGameState((prev) => ({
    ...prev,
    currentSale: newSale,
    saleTiming: false,
  }));
}

// ダンジョン入場時（BaseCampで実行）
setGameState((prev) => ({
  ...prev,
  saleTiming: false,
}));
```

---

## 8. テスト項目

### 8.1 購入システムテスト

```
□ 消耗品購入
  □ Gold支払い
  □ アイテム追加
  □ Gold不足時のエラー

□ 転移石購入
  □ 正常購入
  □ インベントリ追加確認

□ 装備パック購入
  □ パック開封
  □ 6個の装備取得
  □ レアリティ確率確認
```

### 8.2 売却システムテスト

```
□ 装備売却
  □ Gold加算
  □ アイテム削除

□ 装備中フィルタリング
  □ 装備中は非表示
  □ 装備解除後は表示
```

### 8.3 魔石取引テスト

```
□ 魔石換金
  □ 正しいレート計算
  □ 価値の低い順に消費
  □ Gold加算
```

### 8.4 セールシステムテスト

```
□ セール発動
  □ encounterCount >= 3
  □ 帰還時に更新
  □ ダンジョン入場でリセット

□ セール適用
  □ カテゴリセール
  □ 特定商品セール
  □ Epic以上は除外
```

---

## 9. 注意事項

### 9.1 データの不完全性

- 装備データは EQUIPMENT_AND_ITEMS_DESIGN.md を参照
- バフ/デバフの詳細は後回し
- 消耗品の効果は簡易実装

### 9.2 将来拡張

- 在庫制限システム
- 一括売却機能
- 品質システム（Quality）
- ショップ NPC との会話

### 9.3 実装優先度

```
Phase 1（最優先）:
- 基本購入・売却
- 固定装備パック
- シンプルなセール

Phase 2（中優先）:
- 確率抽選システム
- 装備パック演出
- 魔石取引

Phase 3（低優先）:
- 高度なアニメーション
- 在庫制限
- 一括売却
```

---

## 10. 参照ドキュメント

```
BASE_CAMP_DESIGN_V1
├── GUILD_DESIGN_V2.1
└── SHOP_DESIGN_V1 [本文書]
    ├── ShopData.ts [商品データ]
    ├── MagicStoneData.ts [魔石データ]
    ├── equipmentGenerator.ts [装備生成]
    └── return_system.md [転移石システム]
```

---

**次のステップ:** 実装手順書の詳細版を作成

## まとめ

取引所の設計が完成しました：

**主な決定事項:**

- ✅ ShopContext は不要（GameStateContext + ローカル state）
- ✅ 魔石レート: 小 30G / 中 100G / 大 350G
- ✅ パック価格: Common 300G / Rare 500G / Epic 1000G
- ✅ 装備パックは 1 パックで 6 個（全スロット）
- ✅ セールは encounterCount >= 3 で発動、帰還時に更新
- ✅ Epic 以上はセール対象外

**実装優先度:**

1. Phase 1: 基本購入・売却（固定装備）
2. Phase 2: 装備パック確率システム
3. Phase 3: 魔石取引・セール

実装準備が整いました！
