# 取引所（Shop）実装手順書 (SHOP_IMPLEMENTATION_GUIDE_V1)

## 0. 前提条件

### 0.1 完了している必要があるタスク
- ✅ BaseCamp全体設計（Context API実装済み）
- ✅ Item型システムの導入
- ✅ InventoryContextの実装

### 0.2 依存関係
```
GameStateContext (encounterCount, saleTiming, currentSale)
  ↓
PlayerContext (gold, useGold, addGold)
  ↓
InventoryContext (items, addItem, removeItem, getEquippedIds)
  ↓
Shop Components (BuyTab, SellTab, ExchangeTab)
```

---

## Phase 1: データと型の準備（Week 1: Day 1-2）

### タスク 1.1: ShopTypes.ts の作成

**優先度:** 🔴 最高

```bash
# ディレクトリ作成
mkdir -p src/types
```

```typescript
// src/types/ShopTypes.ts (新規作成)

import type { ItemType, EquipmentSlot } from './ItemTypes';

/**
 * ショップ商品データ
 */
export interface ShopItem {
  id: string;
  targetItemId?: string;
  name: string;
  description: string;
  type: 'consumable' | 'teleport' | 'equipment_pack';
  basePrice: number;
  icon: string;
  packConfig?: EquipmentPackConfig;
}

export interface EquipmentPackConfig {
  guaranteedRarity: 'common' | 'rare' | 'epic';
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
  targetCategory?: 'consumable' | 'teleport' | 'equipment_pack';
  targetItemId?: string;
  discountRate: number;
  excludeRarities?: ('epic' | 'legendary')[];
}

export type ShopCategory = 'consumable' | 'teleport' | 'equipment_pack';

export interface MagicStoneExchange {
  totalValue: number;
  breakdown: {
    typeId: string;
    count: number;
    unitValue: number;
    totalValue: number;
  }[];
}
```

**✅ 完了チェック:**
- [ ] ShopTypes.ts が作成された
- [ ] コンパイルエラーがない

---

### タスク 1.2: GameStateContext の拡張

```typescript
// src/contexts/GameStateContext.tsx (修正)

import type { DailySale } from '../types/ShopTypes';

export interface GameState {
  currentScreen: GameScreen;
  battleMode: BattleMode;
  depth: Depth;
  encounterCount: number;         // ✨ 新規追加
  battleConfig?: BattleConfig;
  
  // Shop用
  saleTiming: boolean;            // ✨ 新規追加
  currentSale: DailySale | null;  // ✨ 新規追加
}

export const GameStateProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [gameState, setGameState] = useState<GameState>({
    currentScreen: 'camp',
    battleMode: null,
    depth: 1,
    encounterCount: 0,              // ✨ 初期値
    saleTiming: false,              // ✨ 初期値
    currentSale: null,              // ✨ 初期値
  });

  // ... 既存のコード

  // ✨ 新規追加：戦闘回数インクリメント
  const incrementEncounterCount = () => {
    setGameState(prev => {
      const newCount = prev.encounterCount + 1;
      return {
        ...prev,
        encounterCount: newCount,
        saleTiming: newCount >= 3, // 3回以上でセール更新フラグ
      };
    });
  };

  // ✨ 新規追加：セール更新
  const updateSale = (sale: DailySale | null) => {
    setGameState(prev => ({
      ...prev,
      currentSale: sale,
      saleTiming: false, // フラグをリセット
    }));
  };

  // ✨ 新規追加：ダンジョン入場時の処理
  const enterDungeon = () => {
    setGameState(prev => ({
      ...prev,
      currentScreen: 'dungeon',
      saleTiming: false, // セールフラグをリセット
    }));
  };

  return (
    <GameStateContext.Provider value={{ 
      gameState, 
      setGameState, 
      navigateTo, 
      startBattle, 
      returnToCamp,
      incrementEncounterCount,  // ✨ 追加
      updateSale,               // ✨ 追加
      enterDungeon,             // ✨ 追加
    }}>
      {children}
    </GameStateContext.Provider>
  );
};
```

**✅ 完了チェック:**
- [ ] encounterCount が追加された
- [ ] saleTiming が追加された
- [ ] currentSale が追加された
- [ ] incrementEncounterCount が実装された

---

### タスク 1.3: MagicStoneData.ts の作成

```bash
mkdir -p src/items/data
```

```typescript
// src/items/data/MagicStoneData.ts (新規作成)

import type { Item } from '../../types/ItemTypes';

export const MAGIC_STONE_ITEMS: Item[] = [
  {
    id: 'magic_stone_small_001',
    typeId: 'magic_stone_small',
    name: '魔石（小）',
    description: 'わずかな魔力を帯びた小さな石',
    itemType: 'magicStone',
    icon: '💎',
    magicStoneValue: 30,
    rarity: 'common',
    sellPrice: 30,
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1
  },
  {
    id: 'magic_stone_medium_001',
    typeId: 'magic_stone_medium',
    name: '魔石（中）',
    description: 'ほのかに光る魔石',
    itemType: 'magicStone',
    icon: '💎',
    magicStoneValue: 100,
    rarity: 'uncommon',
    sellPrice: 100,
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1
  },
  {
    id: 'magic_stone_large_001',
    typeId: 'magic_stone_large',
    name: '魔石（大）',
    description: '強い魔力を放つ貴重な魔石',
    itemType: 'magicStone',
    icon: '💎',
    magicStoneValue: 350,
    rarity: 'rare',
    sellPrice: 350,
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1
  },
];

export const MAGIC_STONE_RATES: Record<string, number> = {
  'magic_stone_small': 30,
  'magic_stone_medium': 100,
  'magic_stone_large': 350,
};

export function calculateMagicStoneValue(items: Item[]): number {
  return items
    .filter(item => item.itemType === 'magicStone')
    .reduce((sum, item) => {
      const value = item.magicStoneValue || 0;
      const count = item.stackCount || 1;
      return sum + (value * count);
    }, 0);
}
```

**✅ 完了チェック:**
- [ ] MagicStoneData.ts が作成された
- [ ] 3種類の魔石データが定義された
- [ ] calculateMagicStoneValue が実装された

---

### タスク 1.4: ShopData.ts の作成

```bash
mkdir -p src/camps/facilities/Shop/data
```

```typescript
// src/camps/facilities/Shop/data/ShopData.ts (新規作成)

import type { ShopItem, ShopCategory } from '../../../../types/ShopTypes';

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
    icon: "🧪"
  },
  {
    id: "shop_potion_medium",
    targetItemId: "potion_medium",
    name: "中回復ポーション",
    description: "HP+70回復",
    type: "consumable",
    basePrice: 120,
    icon: "🧪"
  },
  {
    id: "shop_potion_large",
    targetItemId: "potion_large",
    name: "大回復ポーション",
    description: "HP+150回復",
    type: "consumable",
    basePrice: 240,
    icon: "🧪"
  },
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
    icon: "🔮"
  },
  {
    id: "shop_teleport_blessed",
    targetItemId: "teleport_blessed",
    name: "転移石（祝福）",
    description: "80%の確率で帰還",
    type: "teleport",
    basePrice: 300,
    icon: "✨"
  },
  {
    id: "shop_teleport_emergency",
    targetItemId: "teleport_emergency",
    name: "転移石（緊急）",
    description: "60%の確率で帰還",
    type: "teleport",
    basePrice: 100,
    icon: "⚡"
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
        legendary: 0.0
      }
    }
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
        common: 0.60,
        rare: 0.35,
        epic: 0.05,
        legendary: 0.0
      }
    }
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
        common: 0.30,
        rare: 0.45,
        epic: 0.20,
        legendary: 0.05
      }
    }
  },
];

export const ALL_SHOP_ITEMS: ShopItem[] = [
  ...CONSUMABLE_ITEMS,
  ...TELEPORT_ITEMS,
  ...EQUIPMENT_PACKS,
];

export function getItemsByCategory(category: ShopCategory): ShopItem[] {
  switch (category) {
    case 'consumable':
      return CONSUMABLE_ITEMS;
    case 'teleport':
      return TELEPORT_ITEMS;
    case 'equipment_pack':
      return EQUIPMENT_PACKS;
    default:
      return [];
  }
}
```

**✅ 完了チェック:**
- [ ] ShopData.ts が作成された
- [ ] 3カテゴリの商品が定義された
- [ ] getItemsByCategory が実装された

---

## Phase 2: Shopコンポーネントの実装（Week 1-2: Day 3-7）

### タスク 2.1: Shop.tsx の骨組み

```bash
mkdir -p src/camps/facilities/Shop
```

```typescript
// src/camps/facilities/Shop/Shop.tsx (新規作成)

import { useState } from 'react';
import { usePlayer } from '../../../contexts/PlayerContext';
import { useGameState } from '../../../contexts/GameStateContext';
import { useInventory } from '../../../contexts/InventoryContext';
import BuyTab from './BuyTab';
import SellTab from './SellTab';
import ExchangeTab from './ExchangeTab';
import './Shop.css';

type ShopTab = 'buy' | 'sell' | 'exchange';

const Shop: React.FC = () => {
  const [activeTab, setActiveTab] = useState<ShopTab>('buy');
  const { player } = usePlayer();
  const { returnToCamp } = useGameState();
  const { items } = useInventory();

  // 魔石の総価値を計算
  const magicStoneValue = items
    .filter(item => item.itemType === 'magicStone')
    .reduce((sum, item) => {
      const value = item.magicStoneValue || 0;
      const count = item.stackCount || 1;
      return sum + (value * count);
    }, 0);

  return (
    <div className="shop-screen">
      <header className="shop-header">
        <h1>🏪 取引所 - Merchant's Exchange</h1>
        <div className="resources">
          <div className="gold">💰 {player.gold} G</div>
          <div className="magic-stones">💎 {magicStoneValue} G相当</div>
        </div>
      </header>

      <nav className="shop-tabs">
        <button
          className={activeTab === 'buy' ? 'active' : ''}
          onClick={() => setActiveTab('buy')}
        >
          購入 (Buy)
        </button>
        <button
          className={activeTab === 'sell' ? 'active' : ''}
          onClick={() => setActiveTab('sell')}
        >
          売却 (Sell)
        </button>
        <button
          className={activeTab === 'exchange' ? 'active' : ''}
          onClick={() => setActiveTab('exchange')}
        >
          魔石取引 (Exchange)
        </button>
      </nav>

      <div className="shop-content">
        {activeTab === 'buy' && <BuyTab />}
        {activeTab === 'sell' && <SellTab />}
        {activeTab === 'exchange' && <ExchangeTab />}
      </div>

      <button className="back-button" onClick={returnToCamp}>
        キャンプに戻る
      </button>
    </div>
  );
};

export default Shop;
```

**✅ 完了チェック:**
- [ ] Shop.tsx が作成された
- [ ] タブ切り替えが動作する
- [ ] リソース表示が正しい

---

### タスク 2.2: BuyTab の実装

```typescript
// src/camps/facilities/Shop/BuyTab.tsx (新規作成)

import { useState } from 'react';
import { usePlayer } from '../../../contexts/PlayerContext';
import { useGameState } from '../../../contexts/GameStateContext';
import { useInventory } from '../../../contexts/InventoryContext';
import { getItemsByCategory } from './data/ShopData';
import { calculateDiscountedPrice } from './utils/saleCalculator';
import type { ShopCategory, ShopItem } from '../../../types/ShopTypes';
import './BuyTab.css';

const BuyTab: React.FC = () => {
  const [selectedCategory, setSelectedCategory] = useState<ShopCategory>('consumable');
  const { player, useGold } = usePlayer();
  const { gameState } = useGameState();
  const { addItem } = useInventory();

  const items = getItemsByCategory(selectedCategory);
  const { currentSale } = gameState;

  const handleBuy = (shopItem: ShopItem) => {
    const price = calculateDiscountedPrice(shopItem.basePrice, currentSale, shopItem);

    if (player.gold < price) {
      alert('Goldが足りません！');
      return;
    }

    if (!confirm(`${shopItem.name} を ${price}G で購入しますか？`)) {
      return;
    }

    // Gold支払い
    if (!useGold(price)) {
      alert('購入に失敗しました');
      return;
    }

    // アイテム付与（Phase 1: 簡易実装）
    if (shopItem.type === 'equipment_pack') {
      // Phase 1では固定装備を6個付与
      // Phase 2で確率抽選を実装
      alert('装備パックを開封しました！（Phase 1: 簡易実装）');
      // TODO: openEquipmentPack(shopItem.packConfig)
    } else {
      // 消耗品・転移石
      // TODO: createItemFromId(shopItem.targetItemId)
      alert(`${shopItem.name} を購入しました！`);
    }
  };

  return (
    <div className="buy-tab">
      {/* カテゴリ選択 */}
      <nav className="category-tabs">
        <button
          className={selectedCategory === 'consumable' ? 'active' : ''}
          onClick={() => setSelectedCategory('consumable')}
        >
          消耗品
        </button>
        <button
          className={selectedCategory === 'teleport' ? 'active' : ''}
          onClick={() => setSelectedCategory('teleport')}
        >
          転移石
        </button>
        <button
          className={selectedCategory === 'equipment_pack' ? 'active' : ''}
          onClick={() => setSelectedCategory('equipment_pack')}
        >
          装備パック
        </button>
      </nav>

      {/* 商品グリッド */}
      <div className="items-grid">
        {items.map(item => {
          const price = calculateDiscountedPrice(item.basePrice, currentSale, item);
          const isOnSale = price < item.basePrice;

          return (
            <div key={item.id} className="shop-item-card">
              <div className="item-icon">{item.icon}</div>
              <div className="item-name">{item.name}</div>
              <div className="item-description">{item.description}</div>
              
              <div className="item-price">
                {isOnSale && (
                  <>
                    <span className="sale-badge">SALE!</span>
                    <span className="original-price">{item.basePrice} G</span>
                  </>
                )}
                <span className={isOnSale ? 'discounted-price' : 'normal-price'}>
                  {price} G
                </span>
              </div>

              <button
                className="buy-button"
                onClick={() => handleBuy(item)}
                disabled={player.gold < price}
              >
                {player.gold < price ? 'Gold不足' : '購入'}
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default BuyTab;
```

**✅ 完了チェック:**
- [ ] BuyTab.tsx が作成された
- [ ] カテゴリ選択が動作する
- [ ] 商品グリッドが表示される
- [ ] 購入処理が動作する（簡易版）

---

### タスク 2.3: saleCalculator.ts の作成

```typescript
// src/camps/facilities/Shop/utils/saleCalculator.ts (新規作成)

import type { DailySale, ShopItem } from '../../../../types/ShopTypes';

export function calculateDiscountedPrice(
  basePrice: number,
  sale: DailySale | null,
  item: ShopItem
): number {
  if (!sale) return basePrice;

  // Epic以上の装備パックは除外
  if (item.type === 'equipment_pack' && item.packConfig) {
    if (['epic', 'legendary'].includes(item.packConfig.guaranteedRarity)) {
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

export function generateDailySale(): DailySale {
  const patterns: DailySale[] = [
    {
      targetCategory: 'consumable',
      discountRate: 0.2
    },
    {
      targetCategory: 'teleport',
      discountRate: 0.15
    },
    {
      targetCategory: 'equipment_pack',
      discountRate: 0.1,
      excludeRarities: ['epic', 'legendary']
    },
    {
      targetItemId: 'shop_potion_large',
      discountRate: 0.3
    },
    {
      targetItemId: 'shop_teleport_blessed',
      discountRate: 0.25
    },
  ];

  return patterns[Math.floor(Math.random() * patterns.length)];
}
```

**✅ 完了チェック:**
- [ ] saleCalculator.ts が作成された
- [ ] calculateDiscountedPrice が実装された
- [ ] generateDailySale が実装された

---

### タスク 2.4: SellTab の実装

```typescript
// src/camps/facilities/Shop/SellTab.tsx (新規作成)

import { usePlayer } from '../../../contexts/PlayerContext';
import { useInventory } from '../../../contexts/InventoryContext';
import './SellTab.css';

const SellTab: React.FC = () => {
  const { addGold } = usePlayer();
  const { items, removeItem, getEquippedIds } = useInventory();

  const equippedIds = getEquippedIds();

  // 売却可能アイテムをフィルタリング
  const sellableItems = items.filter(item => {
    if (!item.canSell) return false;
    if (item.itemType === 'equipment' && equippedIds.includes(item.id)) {
      return false; // 装備中は除外
    }
    return true;
  });

  const handleSell = (item: any) => {
    if (!confirm(`${item.name} を ${item.sellPrice}G で売却しますか？`)) {
      return;
    }

    // アイテム削除
    removeItem(item.id);

    // Gold加算
    addGold(item.sellPrice);

    alert(`${item.name} を売却しました！`);
  };

  return (
    <div className="sell-tab">
      <h2>所持アイテム</h2>

      {sellableItems.length === 0 && (
        <p className="no-items">売却可能なアイテムがありません</p>
      )}

      <div className="items-grid">
        {sellableItems.map(item => {
          const isEquipped = equippedIds.includes(item.id);

          return (
            <div key={item.id} className="sell-item-card">
              <div className="item-icon">{item.icon}</div>
              <div className="item-name">{item.name}</div>
              <div className="item-description">{item.description}</div>

              {isEquipped && <div className="equipped-label">(装備中)</div>}

              <div className="item-sell-price">{item.sellPrice} G</div>

              <button
                className="sell-button"
                onClick={() => handleSell(item)}
                disabled={isEquipped}
              >
                {isEquipped ? '装備中' : '売却'}
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default SellTab;
```

**✅ 完了チェック:**
- [ ] SellTab.tsx が作成された
- [ ] 装備中フィルタリングが動作する
- [ ] 売却処理が動作する

---

### タスク 2.5: ExchangeTab の実装

```typescript
// src/camps/facilities/Shop/ExchangeTab.tsx (新規作成)

import { useState } from 'react';
import { usePlayer } from '../../../contexts/PlayerContext';
import { useInventory } from '../../../contexts/InventoryContext';
import './ExchangeTab.css';

const ExchangeTab: React.FC = () => {
  const [exchangeValue, setExchangeValue] = useState(0);
  const { addGold } = usePlayer();
  const { items, removeItem, updateItemStack } = useInventory();

  // 魔石リストを取得
  const magicStones = items
    .filter(item => item.itemType === 'magicStone')
    .sort((a, b) => (a.magicStoneValue || 0) - (b.magicStoneValue || 0));

  const totalValue = magicStones.reduce((sum, stone) => {
    const value = stone.magicStoneValue || 0;
    const count = stone.stackCount || 1;
    return sum + (value * count);
  }, 0);

  const handleExchange = () => {
    if (exchangeValue <= 0 || exchangeValue > totalValue) {
      alert('換金額が無効です');
      return;
    }

    if (!confirm(`魔石を ${exchangeValue}G 分換金しますか？`)) {
      return;
    }

    let remaining = exchangeValue;
    const toRemove: string[] = [];

    for (const stone of magicStones) {
      if (remaining <= 0) break;

      const stoneValue = stone.magicStoneValue || 0;
      const count = stone.stackCount || 1;
      const totalStoneValue = stoneValue * count;

      if (totalStoneValue <= remaining) {
        // この魔石を全て消費
        remaining -= totalStoneValue;
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
    toRemove.forEach(id => removeItem(id));

    // Gold加算
    addGold(exchangeValue);

    // リセット
    setExchangeValue(0);
    alert(`${exchangeValue}G を獲得しました！`);
  };

  return (
    <div className="exchange-tab">
      <h2>所持魔石</h2>

      {magicStones.length === 0 && (
        <p className="no-stones">魔石を所持していません</p>
      )}

      <div className="magic-stones-list">
        {magicStones.map(stone => {
          const value = stone.magicStoneValue || 0;
          const count = stone.stackCount || 1;
          const total = value * count;

          return (
            <div key={stone.id} className="magic-stone-item">
              <span className="stone-icon">{stone.icon}</span>
              <span className="stone-name">{stone.name}</span>
              <span className="stone-count">x {count}</span>
              <span className="stone-value">= {total} G</span>
            </div>
          );
        })}
      </div>

      <div className="total-value">
        <strong>合計価値: {totalValue} G</strong>
      </div>

      <div className="exchange-input">
        <label>換金する価値:</label>
        <input
          type="number"
          min="0"
          max={totalValue}
          value={exchangeValue}
          onChange={(e) => setExchangeValue(Number(e.target.value))}
        />
        <span>G （最大: {totalValue}G）</span>
      </div>

      <div className="exchange-result">
        換金後の獲得Gold: {exchangeValue} G
      </div>

      <button
        className="exchange-button"
        onClick={handleExchange}
        disabled={exchangeValue <= 0 || exchangeValue > totalValue}
      >
        換金する
      </button>
    </div>
  );
};

export default ExchangeTab;
```

**✅ 完了チェック:**
- [ ] ExchangeTab.tsx が作成された
- [ ] 魔石リストが表示される
- [ ] 換金処理が動作する

---

## Phase 3: セールシステムの統合（Week 2: Day 1-3）

### タスク 3.1: BattleScreen との連携

```typescript
// src/battles/battleUI/BattleScreen.tsx (修正)

const BattleScreen: React.FC<BattleScreenProps> = ({
  // ... props
}) => {
  const { incrementEncounterCount } = useGameState(); // ✨ 追加

  // 戦闘終了時の処理
  useEffect(() => {
    if (battleResult === 'victory') {
      // ✨ 戦闘回数をインクリメント
      incrementEncounterCount();
    }
  }, [battleResult, incrementEncounterCount]);

  // ... 残りのコード
};
```

**✅ 完了チェック:**
- [ ] BattleScreenで戦闘回数がインクリメントされる
- [ ] encounterCount >= 3 で saleTiming = true になる

---

### タスク 3.2: BaseCamp との連携

```typescript
// src/camps/campsUI/BaseCamp.tsx (修正)

import { useEffect } from 'react';
import { generateDailySale } from '../facilities/Shop/utils/saleCalculator';

const BaseCamp = () => {
  const { gameState, updateSale, enterDungeon } = useGameState();

  // マウント時にセール更新をチェック
  useEffect(() => {
    if (gameState.saleTiming) {
      const newSale = generateDailySale();
      updateSale(newSale);
    }
  }, [gameState.saleTiming, updateSale]);

  const facilities: FacilityCardProps[] = [
    {
      type: "dungeon",
      name: "深淵の入り口",
      description: "ダンジョン探索",
      icon: "🌀",
      isUnlocked: true,
      onEnter: () => {
        enterDungeon(); // ✨ セールフラグをリセット
      },
    },
    // ... 他の施設
  ];

  // ... 残りのコード
};
```

**✅ 完了チェック:**
- [ ] キャンプ帰還時にセールが更新される
- [ ] ダンジョン入場時にsaleTimingがfalseになる

---

## Phase 4: 装備生成システム（Week 2: Day 4-5）

### タスク 4.1: equipmentGenerator.ts の作成

```bash
mkdir -p src/items/utils
```

```typescript
// src/items/utils/equipmentGenerator.ts (新規作成)

import type { Item, EquipmentSlot } from '../../types/ItemTypes';
import type { EquipmentPackConfig } from '../../types/ShopTypes';

/**
 * ユニークIDを生成
 */
function generateUniqueId(): string {
  return `${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
}

/**
 * レアリティ抽選
 */
export function rollRarity(probabilities: {
  common: number;
  rare: number;
  epic: number;
  legendary: number;
}): 'common' | 'rare' | 'epic' | 'legendary' {
  const roll = Math.random();
  let cumulative = 0;

  for (const [rarity, prob] of Object.entries(probabilities)) {
    cumulative += prob;
    if (roll < cumulative) {
      return rarity as 'common' | 'rare' | 'epic' | 'legendary';
    }
  }

  return 'common';
}

/**
 * ランダムな装備を生成（Phase 1: 簡易実装）
 */
export function createRandomEquipment(
  slot: EquipmentSlot,
  rarity: 'common' | 'rare' | 'epic' | 'legendary'
): Item {
  // Phase 1: 固定装備を返す（仮実装）
  // Phase 2: EQUIPMENT_AND_ITEMS_DESIGN.mdから抽選

  const baseNames: Record<EquipmentSlot, string> = {
    weapon: '剣',
    armor: '鎧',
    helmet: '兜',
    boots: 'ブーツ',
    accessory1: '指輪',
    accessory2: 'お守り',
  };

  const rarityNames: Record<string, string> = {
    common: 'コモン',
    rare: 'レア',
    epic: 'エピック',
    legendary: 'レジェンド',
  };

  return {
    id: generateUniqueId(),
    typeId: `${slot}_${rarity}_template`,
    name: `${rarityNames[rarity]}の${baseNames[slot]}`,
    description: '仮の装備です',
    itemType: 'equipment',
    icon: '⚔️',
    equipmentSlot: slot,
    durability: 100,
    maxDurability: 100,
    effects: [],
    rarity: rarity,
    sellPrice: { common: 50, rare: 150, epic: 400, legendary: 1000 }[rarity],
    canSell: true,
    canDiscard: false,
  };
}

/**
 * 装備パック開封
 */
export function openEquipmentPack(config: EquipmentPackConfig): Item[] {
  const slots: EquipmentSlot[] = [
    'weapon',
    'armor',
    'helmet',
    'boots',
    'accessory1',
    'accessory2',
  ];

  const items: Item[] = [];

  for (const slot of slots) {
    const rarity = rollRarity(config.probabilities);
    const equipment = createRandomEquipment(slot, rarity);
    items.push(equipment);
  }

  return items;
}
```

**✅ 完了チェック:**
- [ ] equipmentGenerator.ts が作成された
- [ ] rollRarity が実装された
- [ ] createRandomEquipment が実装された（Phase 1: 簡易版）
- [ ] openEquipmentPack が実装された

---

### タスク 4.2: BuyTab に装備パック開封を統合

```typescript
// src/camps/facilities/Shop/BuyTab.tsx (修正)

import { openEquipmentPack } from '../../../items/utils/equipmentGenerator';

const BuyTab: React.FC = () => {
  // ... 既存のコード

  const handleBuy = (shopItem: ShopItem) => {
    // ... Gold支払い処理

    if (shopItem.type === 'equipment_pack' && shopItem.packConfig) {
      // ✨ 装備パック開封
      const newEquipments = openEquipmentPack(shopItem.packConfig);
      
      newEquipments.forEach(eq => {
        addItem(eq);
      });

      // TODO: 開封演出
      alert(`装備パックを開封！${newEquipments.length}個の装備を獲得しました！`);
    } else {
      // 消耗品・転移石
      // TODO: createItemFromId(shopItem.targetItemId)
    }
  };

  // ... 残りのコード
};
```

**✅ 完了チェック:**
- [ ] 装備パック購入時に6個の装備が生成される
- [ ] インベントリに正しく追加される

---

## Phase 5: CSS とアニメーション（Week 3）

### タスク 5.1: Shop.css

```css
/* src/camps/facilities/Shop/Shop.css */

.shop-screen {
  width: 100vw;
  height: 100vh;
  background: linear-gradient(135deg, #1a1a2e 0%, #2a2a3e 100%);
  display: flex;
  flex-direction: column;
  padding: 2rem;
  color: #e0d0f0;
}

.shop-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.shop-header h1 {
  font-size: 2.5rem;
  text-shadow: 0 0 20px rgba(218, 165, 32, 0.8);
}

.resources {
  display: flex;
  gap: 2rem;
  font-size: 1.5rem;
}

.shop-tabs {
  display: flex;
  gap: 1rem;
  justify-content: center;
  margin-bottom: 2rem;
}

.shop-tabs button {
  padding: 1rem 2rem;
  background: rgba(218, 165, 32, 0.2);
  border: 2px solid rgba(218, 165, 32, 0.5);
  border-radius: 8px;
  color: #e0d0f0;
  font-size: 1.2rem;
  cursor: pointer;
  transition: all 0.3s ease;
}

.shop-tabs button.active {
  background: rgba(218, 165, 32, 0.8);
  border-color: rgba(218, 165, 32, 1);
}

.shop-content {
  flex: 1;
  background: rgba(0, 0, 0, 0.3);
  border: 2px solid rgba(218, 165, 32, 0.3);
  border-radius: 12px;
  padding: 2rem;
  overflow-y: auto;
}

.back-button {
  margin-top: 1rem;
  padding: 1rem 2rem;
  background: rgba(100, 100, 100, 0.3);
  border: 2px solid rgba(150, 150, 150, 0.5);
  border-radius: 8px;
  color: #e0d0f0;
  font-size: 1.1rem;
  cursor: pointer;
}
```

### タスク 5.2: BuyTab.css

```css
/* src/camps/facilities/Shop/BuyTab.css */

.buy-tab {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.category-tabs {
  display: flex;
  gap: 1rem;
  margin-bottom: 2rem;
}

.category-tabs button {
  padding: 0.75rem 1.5rem;
  background: rgba(100, 100, 100, 0.3);
  border: 2px solid rgba(150, 150, 150, 0.5);
  border-radius: 6px;
  color: #e0d0f0;
  cursor: pointer;
}

.category-tabs button.active {
  background: rgba(218, 165, 32, 0.5);
  border-color: rgba(218, 165, 32, 0.8);
}

.items-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 1.5rem;
}

.shop-item-card {
  background: rgba(50, 50, 70, 0.6);
  border: 2px solid rgba(100, 100, 120, 0.5);
  border-radius: 8px;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  align-items: center;
  transition: all 0.3s ease;
}

.shop-item-card:hover {
  transform: translateY(-4px);
  border-color: rgba(218, 165, 32, 0.8);
  box-shadow: 0 8px 16px rgba(218, 165, 32, 0.3);
}

.item-icon {
  font-size: 3rem;
  margin-bottom: 0.5rem;
}

.item-name {
  font-size: 1.1rem;
  font-weight: bold;
  margin-bottom: 0.5rem;
}

.item-description {
  font-size: 0.9rem;
  color: #b0b0c0;
  margin-bottom: 1rem;
  text-align: center;
}

.item-price {
  display: flex;
  flex-direction: column;
  align-items: center;
  margin-bottom: 1rem;
}

.sale-badge {
  background: #ef4444;
  color: white;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-size: 0.8rem;
  font-weight: bold;
  margin-bottom: 0.25rem;
}

.original-price {
  text-decoration: line-through;
  color: #888;
  font-size: 0.9rem;
}

.discounted-price {
  font-size: 1.3rem;
  font-weight: bold;
  color: #4ade80;
}

.normal-price {
  font-size: 1.3rem;
  font-weight: bold;
  color: #daa520;
}

.buy-button {
  width: 100%;
  padding: 0.75rem;
  background: linear-gradient(135deg, #daa520 0%, #b8860b 100%);
  border: 2px solid #daa520;
  border-radius: 6px;
  color: white;
  font-weight: bold;
  cursor: pointer;
  transition: all 0.3s ease;
}

.buy-button:hover:not(:disabled) {
  transform: scale(1.05);
  box-shadow: 0 4px 12px rgba(218, 165, 32, 0.6);
}

.buy-button:disabled {
  background: rgba(100, 100, 100, 0.3);
  border-color: rgba(150, 150, 150, 0.5);
  color: #888;
  cursor: not-allowed;
}
```

**✅ 完了チェック:**
- [ ] Shop.css が作成された
- [ ] BuyTab.css が作成された
- [ ] スタイルが適用される

---

## テスト手順

### 基本動作テスト

```
□ Shop画面の表示
  □ リソース表示（Gold, 魔石価値）
  □ タブ切り替え

□ 購入機能
  □ 商品グリッド表示
  □ カテゴリ切り替え
  □ 購入処理
  □ Gold減算
  □ アイテム追加

□ 売却機能
  □ インベントリ表示
  □ 装備中フィルタリング
  □ 売却処理
  □ Gold加算

□ 魔石取引
  □ 魔石リスト表示
  □ 換金処理
  □ 正しいレート計算

□ セールシステム
  □ encounterCount増加
  □ saleTiming更新
  □ セール価格表示
  □ Epic除外
```

---

## トラブルシューティング

### よくあるエラー

**1. Items not appearing in Shop**
```
原因: ShopData.ts のインポートエラー
解決: パスを確認
```

**2. Sale not updating**
```
原因: BaseCampでuseEffectが動作していない
解決: dependency arrayを確認
```

**3. Equipment pack not opening**
```
原因: equipmentGenerator.ts のインポートミス
解決: インポートパスを確認
```

---

**END OF SHOP IMPLEMENTATION GUIDE**
