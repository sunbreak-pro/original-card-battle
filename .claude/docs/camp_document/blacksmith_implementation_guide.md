# 鍛冶屋（Blacksmith）実装手順書 (BLACKSMITH_IMPLEMENTATION_GUIDE_V1)

## 0. 前提条件

### 0.1 完了している必要があるタスク
- ✅ BaseCamp全体設計（Context API実装済み）
- ✅ Item型システムの導入
- ✅ InventoryContextの実装
- ✅ Shopの実装（装備生成ロジックが存在）

### 0.2 依存関係
```
ItemTypes (Equipment拡張)
  ↓
equipmentGenerator (品質・レベル付与)
  ↓
InventoryContext (装備管理、魔石消費)
  ↓
Blacksmith Components (UpgradeTab, DismantleTab)
```

---

## Phase 1: 品質システムの導入（Week 1: Day 1-3）

### タスク 1.1: ItemTypes.ts の拡張

**優先度:** 🔴 最高

```typescript
// src/types/ItemTypes.ts (拡張)

export type QualityType = 'poor' | 'normal' | 'good' | 'master';

export interface EquipmentItem extends Item {
  itemType: 'equipment';
  
  // ✨ 鍛冶屋拡張プロパティ
  level: 0 | 1 | 2 | 3;
  quality: QualityType;
  
  // 計算済みステータス（ベース × 品質 × レベル補正）
  stats: {
    atk?: number;
    def?: number;
    magic?: number;
    maxHp?: number;
    maxAp?: number;
  };
  
  // ベースステータス（計算用）
  baseStats: {
    atk?: number;
    def?: number;
    magic?: number;
    maxHp?: number;
    maxAp?: number;
  };
  
  // 解放スキル（Epic/Legendary装備のみ）
  unlockedSkills?: EquipmentEffect[];
  
  // 保護フラグ（Phase 2）
  isLocked?: boolean;
}

// 品質データの定義
export const QUALITY_MODIFIERS: Record<QualityType, number> = {
  poor: 0.95,
  normal: 1.0,
  good: 1.03,
  master: 1.05
};

export const QUALITY_NAMES: Record<QualityType, string> = {
  poor: '錆びた',
  normal: '',
  good: '鍛えられた',
  master: '名工の'
};

export const QUALITY_COLORS: Record<QualityType, string> = {
  poor: '#888888',
  normal: '#ffffff',
  good: '#4ade80',
  master: '#fbbf24'
};

// レベル補正
export const LEVEL_STAT_MODIFIERS: Record<number, number> = {
  0: 0.0,
  1: 0.1,
  2: 0.2,
  3: 0.3
};

export const LEVEL_AP_MODIFIERS: Record<number, number> = {
  0: 0.0,
  1: 0.2,
  2: 0.4,
  3: 0.6
};
```

**✅ 完了チェック:**
- [ ] QualityType が追加された
- [ ] EquipmentItem が拡張された
- [ ] 定数が定義された
- [ ] コンパイルエラーがない

---

### タスク 1.2: BlacksmithTypes.ts の作成

```bash
mkdir -p src/types
```

```typescript
// src/types/BlacksmithTypes.ts (新規作成)

import type { EquipmentItem, QualityType } from './ItemTypes';

/**
 * 強化オプション
 */
export type UpgradeOption = 'normal' | 'quality_focused' | 'max_quality';

export interface UpgradeConfig {
  option: UpgradeOption;
  goldCost: number;
  magicStoneCost: number;
  qualityUpgradeChances: QualityUpgradeChances;
  guaranteedMinQuality: QualityType | null;
}

/**
 * 品質上昇確率
 */
export interface QualityUpgradeChances {
  poor_to_normal: number;
  normal_to_good: number;
  good_to_master: number;
}

/**
 * 強化オプション別の設定
 */
export const UPGRADE_OPTIONS: Record<UpgradeOption, {
  name: string;
  description: string;
  goldMultiplier: number;
  magicStonePreference: 'auto' | 'medium_preferred' | 'large_only';
  qualityUpgradeChances: QualityUpgradeChances;
  guaranteedMinQuality: QualityType | null;
}> = {
  normal: {
    name: '通常強化',
    description: '基本的な強化。品質上昇確率は標準。',
    goldMultiplier: 1.0,
    magicStonePreference: 'auto',
    qualityUpgradeChances: {
      poor_to_normal: 0.40,
      normal_to_good: 0.20,
      good_to_master: 0.10
    },
    guaranteedMinQuality: null
  },
  quality_focused: {
    name: '品質重視強化',
    description: '魔石（中）以上を優先使用。品質上昇確率が高い。',
    goldMultiplier: 1.5,
    magicStonePreference: 'medium_preferred',
    qualityUpgradeChances: {
      poor_to_normal: 0.80,
      normal_to_good: 0.40,
      good_to_master: 0.15
    },
    guaranteedMinQuality: 'normal'
  },
  max_quality: {
    name: '最高品質狙い',
    description: '魔石（大）のみ使用。品質上昇確率が最も高い。',
    goldMultiplier: 2.0,
    magicStonePreference: 'large_only',
    qualityUpgradeChances: {
      poor_to_normal: 1.0,
      normal_to_good: 0.60,
      good_to_master: 0.25
    },
    guaranteedMinQuality: 'good'
  }
};

/**
 * 強化基本コスト（レアリティ・レベル別）
 */
export const BASE_UPGRADE_COSTS: Record<string, Record<number, { gold: number, magicStone: number }>> = {
  common: {
    1: { gold: 200, magicStone: 5 },
    2: { gold: 400, magicStone: 10 },
    3: { gold: 800, magicStone: 20 }
  },
  rare: {
    1: { gold: 400, magicStone: 10 },
    2: { gold: 800, magicStone: 20 },
    3: { gold: 1600, magicStone: 40 }
  },
  epic: {
    1: { gold: 800, magicStone: 20 },
    2: { gold: 1600, magicStone: 40 },
    3: { gold: 3200, magicStone: 80 }
  },
  legendary: {
    1: { gold: 1600, magicStone: 40 },
    2: { gold: 3200, magicStone: 80 },
    3: { gold: 6400, magicStone: 160 }
  }
};

/**
 * 修理コスト設定
 */
export const REPAIR_COST_PER_AP = 0.5;

export const REPAIR_RARITY_MULTIPLIER: Record<string, number> = {
  common: 1.0,
  rare: 1.5,
  epic: 2.0,
  legendary: 3.0
};

/**
 * 解体還元率
 */
export const DISMANTLE_RETURN_RATES: Record<string, number> = {
  common: 0.10,
  rare: 0.15,
  epic: 0.20,
  legendary: 0.25
};

/**
 * 解体ボーナス（Epic以上）
 */
export const DISMANTLE_BONUS_CHANCE = 0.20;
export const DISMANTLE_LEVEL_BONUS: Record<number, number> = {
  1: 0.05,
  2: 0.10,
  3: 0.15
};

/**
 * 強化結果
 */
export interface UpgradeResult {
  success: boolean;
  newLevel: number;
  oldQuality: QualityType;
  newQuality: QualityType;
  qualityUpgraded: boolean;
  skillUnlocked: boolean;
  newStats: EquipmentItem['stats'];
}

/**
 * 解体結果
 */
export interface DismantleResult {
  magicStones: {
    typeId: string;
    count: number;
  }[];
  bonusReceived: boolean;
}
```

**✅ 完了チェック:**
- [ ] BlacksmithTypes.ts が作成された
- [ ] すべての型定義が完了
- [ ] 定数テーブルが定義された

---

### タスク 1.3: equipmentGenerator.ts の更新

```typescript
// src/items/utils/equipmentGenerator.ts (更新)

import type { 
  EquipmentItem, 
  EquipmentSlot, 
  QualityType,
  QUALITY_MODIFIERS 
} from '../../types/ItemTypes';

/**
 * 品質を抽選
 */
export function rollQuality(): QualityType {
  const roll = Math.random();
  
  if (roll < 0.10) return 'poor';      // 10%
  if (roll < 0.80) return 'normal';    // 70%
  if (roll < 0.95) return 'good';      // 15%
  return 'master';                      // 5%
}

/**
 * ステータスを計算（品質 × レベル補正適用）
 */
export function calculateEquipmentStats(
  baseStats: EquipmentItem['baseStats'],
  quality: QualityType,
  level: number
): EquipmentItem['stats'] {
  const qualityMod = QUALITY_MODIFIERS[quality];
  const levelStatMod = LEVEL_STAT_MODIFIERS[level];
  const levelApMod = LEVEL_AP_MODIFIERS[level];
  
  return {
    atk: baseStats.atk 
      ? Math.floor(baseStats.atk * qualityMod * (1 + levelStatMod))
      : undefined,
    def: baseStats.def
      ? Math.floor(baseStats.def * qualityMod * (1 + levelStatMod))
      : undefined,
    magic: baseStats.magic
      ? Math.floor(baseStats.magic * qualityMod * (1 + levelStatMod))
      : undefined,
    maxHp: baseStats.maxHp
      ? Math.floor(baseStats.maxHp * qualityMod * (1 + levelStatMod))
      : undefined,
    maxAp: baseStats.maxAp
      ? Math.floor(baseStats.maxAp * (1 + levelApMod)) // APは品質の影響を受けない
      : undefined,
  };
}

/**
 * ランダムな装備を生成（品質・レベル付き）
 */
export function createRandomEquipment(
  slot: EquipmentSlot,
  rarity: 'common' | 'rare' | 'epic' | 'legendary'
): EquipmentItem {
  const template = getEquipmentTemplate(slot, rarity); // 後で実装
  
  // ✨ 品質とレベルを初期化
  const quality = rollQuality();
  const level = 0; // 常にLv0で生成
  
  // ✨ ステータス計算
  const stats = calculateEquipmentStats(template.baseStats, quality, level);
  
  return {
    id: generateUniqueId(),
    typeId: template.id,
    name: template.name,
    description: template.description,
    itemType: 'equipment',
    icon: template.icon,
    equipmentSlot: slot,
    
    // ✨ 鍛冶屋拡張プロパティ
    level: level,
    quality: quality,
    baseStats: template.baseStats,
    stats: stats,
    
    durability: stats.maxAp || 100,
    maxDurability: stats.maxAp || 100,
    effects: template.effects,
    rarity: rarity,
    sellPrice: template.sellPrice,
    canSell: true,
    canDiscard: false,
    unlockedSkills: template.unlockedSkills
  };
}
```

**✅ 完了チェック:**
- [ ] rollQuality が実装された
- [ ] calculateEquipmentStats が実装された
- [ ] createRandomEquipment が更新された
- [ ] すべての装備が品質・レベル付きで生成される

---

### タスク 1.4: 既存装備へのマイグレーション

```typescript
// src/items/utils/equipmentMigration.ts (新規作成)

import type { Item, EquipmentItem } from '../../types/ItemTypes';
import { calculateEquipmentStats } from './equipmentGenerator';

/**
 * 既存の装備に品質とレベルを付与
 */
export function migrateEquipmentToV2(item: Item): EquipmentItem | Item {
  if (item.itemType !== 'equipment') {
    return item; // 装備以外はそのまま
  }
  
  const equipment = item as any;
  
  // すでに移行済みの場合はスキップ
  if ('quality' in equipment && 'level' in equipment) {
    return equipment as EquipmentItem;
  }
  
  // ✨ 品質とレベルを追加
  const quality: QualityType = 'normal'; // 既存装備はすべてnormal
  const level = 0;
  
  // baseStatsが存在しない場合は現在のstatsをbaseStatsとする
  const baseStats = equipment.baseStats || {
    atk: equipment.atk,
    def: equipment.def,
    magic: equipment.magic,
    maxHp: equipment.maxHp,
    maxAp: equipment.maxDurability
  };
  
  // statsを再計算
  const stats = calculateEquipmentStats(baseStats, quality, level);
  
  return {
    ...equipment,
    level: level,
    quality: quality,
    baseStats: baseStats,
    stats: stats,
    itemType: 'equipment'
  } as EquipmentItem;
}

/**
 * インベントリ全体をマイグレーション
 */
export function migrateInventory(items: Item[]): Item[] {
  return items.map(item => migrateEquipmentToV2(item));
}
```

**InventoryContextに統合:**

```typescript
// src/contexts/InventoryContext.tsx (修正)

import { migrateInventory } from '../items/utils/equipmentMigration';

export const InventoryProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [items, setItems] = useState<Item[]>(() => {
    const stored = localStorage.getItem('inventory');
    if (stored) {
      const parsedItems = JSON.parse(stored);
      // ✨ マイグレーション実行
      return migrateInventory(parsedItems);
    }
    return [];
  });
  
  // ... 残りのコード
};
```

**✅ 完了チェック:**
- [ ] migrateEquipmentToV2 が実装された
- [ ] InventoryContext でマイグレーションが実行される
- [ ] 既存の装備に quality と level が付与される

---

## Phase 2: 強化システムの実装（Week 1: Day 4 - Week 2: Day 2）

### タスク 2.1: upgradeEquipment.ts の作成

```bash
mkdir -p src/camps/facilities/Blacksmith/logic
```

```typescript
// src/camps/facilities/Blacksmith/logic/upgradeEquipment.ts (新規作成)

import type { EquipmentItem } from '../../../../types/ItemTypes';
import type { UpgradeResult, UpgradeOption } from '../../../../types/BlacksmithTypes';
import { calculateEquipmentStats } from '../../../../items/utils/equipmentGenerator';
import { rollQualityUpgrade } from './qualityUpgrade';
import { UPGRADE_OPTIONS } from '../../../../types/BlacksmithTypes';

/**
 * 装備を強化
 */
export function upgradeEquipment(
  equipment: EquipmentItem,
  option: UpgradeOption
): UpgradeResult {
  const currentLevel = equipment.level;
  const newLevel = Math.min(currentLevel + 1, 3) as 0 | 1 | 2 | 3;
  
  if (newLevel === currentLevel) {
    return {
      success: false,
      newLevel: currentLevel,
      oldQuality: equipment.quality,
      newQuality: equipment.quality,
      qualityUpgraded: false,
      skillUnlocked: false,
      newStats: equipment.stats
    };
  }
  
  // ✨ 品質上昇判定
  const upgradeConfig = UPGRADE_OPTIONS[option];
  const qualityUpgradeResult = rollQualityUpgrade(
    equipment.quality,
    upgradeConfig.qualityUpgradeChances,
    upgradeConfig.guaranteedMinQuality
  );
  
  const newQuality = qualityUpgradeResult.newQuality;
  const qualityUpgraded = qualityUpgradeResult.upgraded;
  
  // ステータス再計算
  const newStats = calculateEquipmentStats(
    equipment.baseStats,
    newQuality,
    newLevel
  );
  
  // スキル解放判定
  const skillUnlocked = newLevel === 3 && !!equipment.unlockedSkills;
  
  return {
    success: true,
    newLevel: newLevel,
    oldQuality: equipment.quality,
    newQuality: newQuality,
    qualityUpgraded: qualityUpgraded,
    skillUnlocked: skillUnlocked,
    newStats: newStats
  };
}

/**
 * 強化結果を装備に適用
 */
export function applyUpgradeResult(
  equipment: EquipmentItem,
  result: UpgradeResult
): EquipmentItem {
  return {
    ...equipment,
    level: result.newLevel,
    quality: result.newQuality,
    stats: result.newStats,
    durability: result.newStats.maxAp || equipment.durability,
    maxDurability: result.newStats.maxAp || equipment.maxDurability
  };
}
```

**✅ 完了チェック:**
- [ ] upgradeEquipment が実装された
- [ ] applyUpgradeResult が実装された
- [ ] レベルアップロジックが正しい

---

### タスク 2.2: qualityUpgrade.ts の作成

```typescript
// src/camps/facilities/Blacksmith/logic/qualityUpgrade.ts (新規作成)

import type { QualityType } from '../../../../types/ItemTypes';
import type { QualityUpgradeChances } from '../../../../types/BlacksmithTypes';

/**
 * 品質上昇の判定結果
 */
export interface QualityUpgradeResult {
  upgraded: boolean;
  newQuality: QualityType;
}

/**
 * 品質の順序
 */
const QUALITY_ORDER: QualityType[] = ['poor', 'normal', 'good', 'master'];

/**
 * 次の品質を取得
 */
function getNextQuality(current: QualityType): QualityType | null {
  const index = QUALITY_ORDER.indexOf(current);
  if (index === -1 || index === QUALITY_ORDER.length - 1) {
    return null; // すでに最高品質
  }
  return QUALITY_ORDER[index + 1];
}

/**
 * 品質上昇を判定
 */
export function rollQualityUpgrade(
  currentQuality: QualityType,
  chances: QualityUpgradeChances,
  guaranteedMinQuality: QualityType | null
): QualityUpgradeResult {
  const nextQuality = getNextQuality(currentQuality);
  
  // すでに最高品質
  if (!nextQuality) {
    return {
      upgraded: false,
      newQuality: currentQuality
    };
  }
  
  // 上昇確率を取得
  let upgradeChance = 0;
  switch (currentQuality) {
    case 'poor':
      upgradeChance = chances.poor_to_normal;
      break;
    case 'normal':
      upgradeChance = chances.normal_to_good;
      break;
    case 'good':
      upgradeChance = chances.good_to_master;
      break;
  }
  
  // 判定
  const roll = Math.random();
  let resultQuality = currentQuality;
  let upgraded = false;
  
  if (roll < upgradeChance) {
    resultQuality = nextQuality;
    upgraded = true;
  }
  
  // ✨ 最低保証を適用
  if (guaranteedMinQuality) {
    const minIndex = QUALITY_ORDER.indexOf(guaranteedMinQuality);
    const currentIndex = QUALITY_ORDER.indexOf(resultQuality);
    
    if (currentIndex < minIndex) {
      resultQuality = guaranteedMinQuality;
      upgraded = true; // 保証による上昇もupgradedとする
    }
  }
  
  return {
    upgraded: upgraded,
    newQuality: resultQuality
  };
}
```

**✅ 完了チェック:**
- [ ] rollQualityUpgrade が実装された
- [ ] 最低保証が正しく適用される
- [ ] 確率計算が正しい

---

### タスク 2.3: calculateUpgradeCost.ts の作成

```typescript
// src/camps/facilities/Blacksmith/logic/calculateUpgradeCost.ts (新規作成)

import type { EquipmentItem } from '../../../../types/ItemTypes';
import type { UpgradeOption, UpgradeConfig } from '../../../../types/BlacksmithTypes';
import { 
  BASE_UPGRADE_COSTS,
  UPGRADE_OPTIONS 
} from '../../../../types/BlacksmithTypes';

/**
 * 強化コストを計算
 */
export function calculateUpgradeCost(
  equipment: EquipmentItem,
  option: UpgradeOption
): UpgradeConfig {
  const currentLevel = equipment.level;
  const targetLevel = currentLevel + 1;
  
  if (targetLevel > 3) {
    throw new Error('Cannot upgrade beyond level 3');
  }
  
  const rarity = equipment.rarity || 'common';
  const baseCost = BASE_UPGRADE_COSTS[rarity][targetLevel];
  
  if (!baseCost) {
    throw new Error(`No cost data for ${rarity} level ${targetLevel}`);
  }
  
  const optionConfig = UPGRADE_OPTIONS[option];
  
  return {
    option: option,
    goldCost: Math.floor(baseCost.gold * optionConfig.goldMultiplier),
    magicStoneCost: baseCost.magicStone,
    qualityUpgradeChances: optionConfig.qualityUpgradeChances,
    guaranteedMinQuality: optionConfig.guaranteedMinQuality
  };
}

/**
 * 魔石の充足確認
 */
export function checkMagicStoneAvailability(
  items: Item[],
  requiredValue: number,
  preference: 'auto' | 'medium_preferred' | 'large_only'
): {
  available: boolean;
  deficit: number;
  breakdown: {
    small: number;
    medium: number;
    large: number;
  };
} {
  const magicStones = items.filter(item => item.itemType === 'magicStone');
  
  let small = 0, medium = 0, large = 0;
  magicStones.forEach(stone => {
    const count = stone.stackCount || 1;
    switch (stone.typeId) {
      case 'magic_stone_small':
        small += count;
        break;
      case 'magic_stone_medium':
        medium += count;
        break;
      case 'magic_stone_large':
        large += count;
        break;
    }
  });
  
  // 優先度に応じた価値計算
  let totalValue = 0;
  
  switch (preference) {
    case 'large_only':
      totalValue = large * 350;
      break;
    
    case 'medium_preferred':
      totalValue = (large * 350) + (medium * 100) + (small * 30);
      break;
    
    case 'auto':
    default:
      totalValue = (large * 350) + (medium * 100) + (small * 30);
      break;
  }
  
  const available = totalValue >= requiredValue;
  const deficit = available ? 0 : (requiredValue - totalValue);
  
  return {
    available,
    deficit,
    breakdown: { small, medium, large }
  };
}
```

**✅ 完了チェック:**
- [ ] calculateUpgradeCost が実装された
- [ ] checkMagicStoneAvailability が実装された
- [ ] 強化オプション倍率が適用される

---

## Phase 3: UIコンポーネントの実装（Week 2: Day 3-5）

### タスク 3.1: Blacksmith.tsx の作成

```bash
mkdir -p src/camps/facilities/Blacksmith
```

```typescript
// src/camps/facilities/Blacksmith/Blacksmith.tsx (新規作成)

import { useState } from 'react';
import { useGameState } from '../../../contexts/GameStateContext';
import { usePlayer } from '../../../contexts/PlayerContext';
import { useInventory } from '../../../contexts/InventoryContext';
import UpgradeTab from './UpgradeTab';
import DismantleTab from './DismantleTab';
import './Blacksmith.css';

type BlacksmithTab = 'upgrade' | 'dismantle';

const Blacksmith: React.FC = () => {
  const [activeTab, setActiveTab] = useState<BlacksmithTab>('upgrade');
  const { returnToCamp } = useGameState();
  const { player } = usePlayer();
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
    <div className="blacksmith-screen">
      <header className="blacksmith-header">
        <h1>⚒️ 鍛冶屋 - The Blacksmith</h1>
        <div className="resources">
          <div className="gold">💰 {player.gold} G</div>
          <div className="magic-stones">💎 {magicStoneValue} G相当</div>
        </div>
      </header>
      
      <nav className="blacksmith-tabs">
        <button
          className={activeTab === 'upgrade' ? 'active' : ''}
          onClick={() => setActiveTab('upgrade')}
        >
          強化・修理
        </button>
        <button
          className={activeTab === 'dismantle' ? 'active' : ''}
          onClick={() => setActiveTab('dismantle')}
        >
          解体
        </button>
      </nav>
      
      <div className="blacksmith-content">
        {activeTab === 'upgrade' && <UpgradeTab />}
        {activeTab === 'dismantle' && <DismantleTab />}
      </div>
      
      <button className="back-button" onClick={returnToCamp}>
        キャンプに戻る
      </button>
    </div>
  );
};

export default Blacksmith;
```

**✅ 完了チェック:**
- [ ] Blacksmith.tsx が作成された
- [ ] タブ切り替えが動作する
- [ ] リソース表示が正しい

---

### タスク 3.2: UpgradeTab.tsx の作成

```typescript
// src/camps/facilities/Blacksmith/UpgradeTab.tsx (新規作成)

import { useState } from 'react';
import { usePlayer } from '../../../contexts/PlayerContext';
import { useInventory } from '../../../contexts/InventoryContext';
import type { EquipmentItem } from '../../../types/ItemTypes';
import type { UpgradeOption } from '../../../types/BlacksmithTypes';
import { calculateUpgradeCost, checkMagicStoneAvailability } from './logic/calculateUpgradeCost';
import { upgradeEquipment, applyUpgradeResult } from './logic/upgradeEquipment';
import { UPGRADE_OPTIONS } from '../../../types/BlacksmithTypes';
import { QUALITY_NAMES, QUALITY_COLORS } from '../../../types/ItemTypes';
import UpgradeOptionModal from './components/UpgradeOptionModal';
import './UpgradeTab.css';

const UpgradeTab: React.FC = () => {
  const [selectedEquipment, setSelectedEquipment] = useState<EquipmentItem | null>(null);
  const [showUpgradeModal, setShowUpgradeModal] = useState(false);
  const { player, useGold } = usePlayer();
  const { items, updateItem, useMagicStones } = useInventory();
  
  // 装備リストを取得（装備中も含む）
  const equipmentList = items.filter(item => item.itemType === 'equipment') as EquipmentItem[];
  
  const handleSelectEquipment = (equipment: EquipmentItem) => {
    setSelectedEquipment(equipment);
  };
  
  const handleUpgradeClick = () => {
    if (!selectedEquipment) return;
    if (selectedEquipment.level >= 3) {
      alert('この装備は最大レベルです');
      return;
    }
    setShowUpgradeModal(true);
  };
  
  const handleConfirmUpgrade = (option: UpgradeOption) => {
    if (!selectedEquipment) return;
    
    try {
      const config = calculateUpgradeCost(selectedEquipment, option);
      const optionData = UPGRADE_OPTIONS[option];
      
      // コスト確認
      if (player.gold < config.goldCost) {
        alert('Goldが足りません！');
        return;
      }
      
      const magicStoneCheck = checkMagicStoneAvailability(
        items,
        config.magicStoneCost,
        optionData.magicStonePreference
      );
      
      if (!magicStoneCheck.available) {
        alert(`魔石が ${magicStoneCheck.deficit}G 分足りません！`);
        return;
      }
      
      // Gold支払い
      if (!useGold(config.goldCost)) {
        alert('Gold支払いに失敗しました');
        return;
      }
      
      // 魔石消費
      if (!useMagicStones(config.magicStoneCost, optionData.magicStonePreference)) {
        alert('魔石消費に失敗しました');
        return;
      }
      
      // ✨ 強化実行
      const result = upgradeEquipment(selectedEquipment, option);
      const upgradedEquipment = applyUpgradeResult(selectedEquipment, result);
      
      // アイテム更新
      updateItem(selectedEquipment.id, upgradedEquipment);
      
      // 演出とメッセージ
      showUpgradeResult(result);
      
      // 選択を更新
      setSelectedEquipment(upgradedEquipment);
      setShowUpgradeModal(false);
      
    } catch (error) {
      console.error('Upgrade error:', error);
      alert('強化に失敗しました');
    }
  };
  
  const showUpgradeResult = (result: UpgradeResult) => {
    let message = `強化成功！ Lv${result.newLevel} になりました！\n`;
    
    if (result.qualityUpgraded) {
      message += `\n🎉 品質が上昇しました！\n${result.oldQuality} → ${result.newQuality}`;
      // TODO: 特別な演出
    }
    
    if (result.skillUnlocked) {
      message += '\n\n✨ 解放スキルが有効化されました！';
      // TODO: スキル解放演出
    }
    
    alert(message);
  };
  
  const handleRepair = () => {
    if (!selectedEquipment) return;
    
    const repairCost = calculateRepairCost(selectedEquipment);
    
    if (repairCost === 0) {
      alert('修理の必要はありません');
      return;
    }
    
    if (!confirm(`${repairCost}G で全回復しますか？`)) {
      return;
    }
    
    if (!useGold(repairCost)) {
      alert('Goldが足りません');
      return;
    }
    
    const repairedEquipment: EquipmentItem = {
      ...selectedEquipment,
      durability: selectedEquipment.maxDurability
    };
    
    updateItem(selectedEquipment.id, repairedEquipment);
    setSelectedEquipment(repairedEquipment);
    alert('修理が完了しました！');
  };
  
  const calculateRepairCost = (equipment: EquipmentItem): number => {
    const missing = equipment.maxDurability - equipment.durability;
    if (missing <= 0) return 0;
    
    const rarity = equipment.rarity || 'common';
    const multiplier = REPAIR_RARITY_MULTIPLIER[rarity] || 1.0;
    
    return Math.floor(missing * REPAIR_COST_PER_AP * multiplier);
  };
  
  return (
    <div className="upgrade-tab">
      <div className="equipment-list">
        <h3>装備リスト</h3>
        {equipmentList.map(eq => (
          <div
            key={eq.id}
            className={`equipment-item ${selectedEquipment?.id === eq.id ? 'selected' : ''}`}
            onClick={() => handleSelectEquipment(eq)}
          >
            <span className="icon">{eq.icon}</span>
            <div className="info">
              <div className="name" style={{ color: QUALITY_COLORS[eq.quality] }}>
                {QUALITY_NAMES[eq.quality]}{eq.name}
              </div>
              <div className="details">
                Lv{eq.level} / {eq.quality} / AP: {eq.durability}/{eq.maxDurability}
              </div>
            </div>
          </div>
        ))}
      </div>
      
      <div className="equipment-panel">
        {selectedEquipment ? (
          <>
            <h3>選択中の装備</h3>
            <div className="equipment-details">
              <div className="icon-large">{selectedEquipment.icon}</div>
              <div className="name-large" style={{ color: QUALITY_COLORS[selectedEquipment.quality] }}>
                {QUALITY_NAMES[selectedEquipment.quality]}{selectedEquipment.name}
              </div>
              <div className="quality">品質: {selectedEquipment.quality}</div>
              <div className="level">レベル: {selectedEquipment.level} / 3</div>
              <div className="durability">
                AP: {selectedEquipment.durability} / {selectedEquipment.maxDurability}
              </div>
              
              <div className="stats">
                {selectedEquipment.stats.atk && <div>ATK: {selectedEquipment.stats.atk}</div>}
                {selectedEquipment.stats.def && <div>DEF: {selectedEquipment.stats.def}</div>}
                {selectedEquipment.stats.magic && <div>Magic: {selectedEquipment.stats.magic}</div>}
              </div>
              
              <div className="actions">
                <button
                  onClick={handleUpgradeClick}
                  disabled={selectedEquipment.level >= 3}
                >
                  {selectedEquipment.level >= 3 ? '最大レベル' : '強化'}
                </button>
                
                <button
                  onClick={handleRepair}
                  disabled={selectedEquipment.durability === selectedEquipment.maxDurability}
                >
                  修理 ({calculateRepairCost(selectedEquipment)}G)
                </button>
              </div>
            </div>
          </>
        ) : (
          <div className="no-selection">装備を選択してください</div>
        )}
      </div>
      
      {showUpgradeModal && selectedEquipment && (
        <UpgradeOptionModal
          equipment={selectedEquipment}
          onConfirm={handleConfirmUpgrade}
          onCancel={() => setShowUpgradeModal(false)}
        />
      )}
    </div>
  );
};

export default UpgradeTab;
```

**✅ 完了チェック:**
- [ ] UpgradeTab.tsx が作成された
- [ ] 装備リスト表示
- [ ] 強化実行
- [ ] 修理実行

---

### タスク 3.3: UpgradeOptionModal.tsx の作成

```typescript
// src/camps/facilities/Blacksmith/components/UpgradeOptionModal.tsx (新規作成)

import type { EquipmentItem } from '../../../../types/ItemTypes';
import type { UpgradeOption } from '../../../../types/BlacksmithTypes';
import { useState } from 'react';
import { useInventory } from '../../../../contexts/InventoryContext';
import { calculateUpgradeCost, checkMagicStoneAvailability } from '../logic/calculateUpgradeCost';
import { UPGRADE_OPTIONS } from '../../../../types/BlacksmithTypes';
import './UpgradeOptionModal.css';

interface UpgradeOptionModalProps {
  equipment: EquipmentItem;
  onConfirm: (option: UpgradeOption) => void;
  onCancel: () => void;
}

const UpgradeOptionModal: React.FC<UpgradeOptionModalProps> = ({
  equipment,
  onConfirm,
  onCancel
}) => {
  const [selectedOption, setSelectedOption] = useState<UpgradeOption>('normal');
  const { items } = useInventory();
  
  const config = calculateUpgradeCost(equipment, selectedOption);
  const optionData = UPGRADE_OPTIONS[selectedOption];
  
  const magicStoneCheck = checkMagicStoneAvailability(
    items,
    config.magicStoneCost,
    optionData.magicStonePreference
  );
  
  return (
    <div className="modal-overlay" onClick={onCancel}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <h2>強化オプションを選択</h2>
        
        <div className="options">
          {(['normal', 'quality_focused', 'max_quality'] as UpgradeOption[]).map(option => {
            const opt = UPGRADE_OPTIONS[option];
            const cfg = calculateUpgradeCost(equipment, option);
            const check = checkMagicStoneAvailability(items, cfg.magicStoneCost, opt.magicStonePreference);
            
            return (
              <div
                key={option}
                className={`option-card ${selectedOption === option ? 'selected' : ''} ${!check.available ? 'unavailable' : ''}`}
                onClick={() => check.available && setSelectedOption(option)}
              >
                <div className="option-name">{opt.name}</div>
                <div className="option-description">{opt.description}</div>
                <div className="option-cost">
                  コスト: {cfg.goldCost}G + 魔石{cfg.magicStoneCost}
                </div>
                <div className="option-chances">
                  品質上昇確率:
                  <ul>
                    <li>poor→normal: {(opt.qualityUpgradeChances.poor_to_normal * 100).toFixed(0)}%</li>
                    <li>normal→good: {(opt.qualityUpgradeChances.normal_to_good * 100).toFixed(0)}%</li>
                    <li>good→master: {(opt.qualityUpgradeChances.good_to_master * 100).toFixed(0)}%</li>
                  </ul>
                </div>
                {opt.guaranteedMinQuality && (
                  <div className="option-guarantee">
                    最低保証: {opt.guaranteedMinQuality}
                  </div>
                )}
                {!check.available && (
                  <div className="option-unavailable">
                    魔石不足: {check.deficit}G
                  </div>
                )}
              </div>
            );
          })}
        </div>
        
        <div className="magic-stone-info">
          <h3>所持魔石:</h3>
          <div>小: {magicStoneCheck.breakdown.small}個 ({magicStoneCheck.breakdown.small * 30}G)</div>
          <div>中: {magicStoneCheck.breakdown.medium}個 ({magicStoneCheck.breakdown.medium * 100}G)</div>
          <div>大: {magicStoneCheck.breakdown.large}個 ({magicStoneCheck.breakdown.large * 350}G)</div>
        </div>
        
        <div className="modal-actions">
          <button onClick={() => onConfirm(selectedOption)} disabled={!magicStoneCheck.available}>
            強化する
          </button>
          <button onClick={onCancel}>
            キャンセル
          </button>
        </div>
      </div>
    </div>
  );
};

export default UpgradeOptionModal;
```

**✅ 完了チェック:**
- [ ] UpgradeOptionModal.tsx が作成された
- [ ] 3つのオプションが表示される
- [ ] 魔石の充足確認が動作する

---

## Phase 4: 解体システムの実装（Week 2-3）

### タスク 4.1: DismantleTab.tsx の作成

```typescript
// src/camps/facilities/Blacksmith/DismantleTab.tsx (新規作成)

import { useState } from 'react';
import { useInventory } from '../../../contexts/InventoryContext';
import type { EquipmentItem } from '../../../types/ItemTypes';
import { dismantleEquipment } from './logic/dismantleEquipment';
import { QUALITY_NAMES, QUALITY_COLORS } from '../../../types/ItemTypes';
import './DismantleTab.css';

const DismantleTab: React.FC = () => {
  const [selectedEquipment, setSelectedEquipment] = useState<EquipmentItem | null>(null);
  const { items, removeItem, addItem } = useInventory();
  
  const equipmentList = items.filter(item => item.itemType === 'equipment') as EquipmentItem[];
  
  const handleDismantle = () => {
    if (!selectedEquipment) return;
    
    // 警告確認
    const shouldWarn = 
      (selectedEquipment.rarity === 'rare' || selectedEquipment.rarity === 'epic' || selectedEquipment.rarity === 'legendary') ||
      selectedEquipment.level >= 1 ||
      selectedEquipment.quality === 'good' || selectedEquipment.quality === 'master';
    
    if (shouldWarn) {
      const confirmMessage = `
⚠️ 警告

${QUALITY_NAMES[selectedEquipment.quality]}${selectedEquipment.name}
(Lv${selectedEquipment.level}, ${selectedEquipment.quality})
を解体しようとしています。

この操作は取り消せません。
本当に解体しますか？
      `.trim();
      
      if (!confirm(confirmMessage)) {
        return;
      }
    }
    
    // 解体実行
    const result = dismantleEquipment(selectedEquipment);
    
    // アイテム削除
    removeItem(selectedEquipment.id);
    
    // 魔石を追加
    result.magicStones.forEach(stone => {
      // TODO: createMagicStoneItem 実装
      // addItem(createMagicStoneItem(stone.typeId, stone.count));
    });
    
    // メッセージ
    let message = '解体が完了しました！\n\n獲得魔石:\n';
    result.magicStones.forEach(stone => {
      message += `${stone.typeId} x ${stone.count}\n`;
    });
    
    if (result.bonusReceived) {
      message += '\n🎉 ボーナスで魔石（大）を追加獲得！';
    }
    
    alert(message);
    setSelectedEquipment(null);
  };
  
  // 解体予測
  const predictDismantle = (equipment: EquipmentItem) => {
    return dismantleEquipment(equipment); // 実際には実行しない（計算のみ）
  };
  
  return (
    <div className="dismantle-tab">
      <div className="equipment-list">
        <h3>装備リスト</h3>
        {equipmentList.map(eq => (
          <div
            key={eq.id}
            className={`equipment-item ${selectedEquipment?.id === eq.id ? 'selected' : ''}`}
            onClick={() => setSelectedEquipment(eq)}
          >
            <span className="icon">{eq.icon}</span>
            <div className="info">
              <div className="name" style={{ color: QUALITY_COLORS[eq.quality] }}>
                {QUALITY_NAMES[eq.quality]}{eq.name}
              </div>
              <div className="details">
                Lv{eq.level} / 売却: {eq.sellPrice}G
              </div>
            </div>
          </div>
        ))}
      </div>
      
      <div className="dismantle-panel">
        {selectedEquipment ? (
          <>
            <h3>解体予測</h3>
            <div className="equipment-info">
              <div className="icon-large">{selectedEquipment.icon}</div>
              <div className="name-large">
                {QUALITY_NAMES[selectedEquipment.quality]}{selectedEquipment.name}
              </div>
              <div>Lv{selectedEquipment.level} / {selectedEquipment.quality}</div>
            </div>
            
            <div className="dismantle-result">
              <h4>解体で獲得:</h4>
              {predictDismantle(selectedEquipment).magicStones.map((stone, i) => (
                <div key={i}>
                  {stone.typeId} x {stone.count}
                </div>
              ))}
              <div className="note">
                ※売却価格の{((DISMANTLE_RETURN_RATES[selectedEquipment.rarity || 'common'] || 0.1) * 100).toFixed(0)}%
              </div>
            </div>
            
            <button className="dismantle-button" onClick={handleDismantle}>
              解体する
            </button>
          </>
        ) : (
          <div className="no-selection">装備を選択してください</div>
        )}
      </div>
    </div>
  );
};

export default DismantleTab;
```

**✅ 完了チェック:**
- [ ] DismantleTab.tsx が作成された
- [ ] 解体予測が表示される
- [ ] 解体実行が動作する

---

### タスク 4.2: dismantleEquipment.ts の作成

```typescript
// src/camps/facilities/Blacksmith/logic/dismantleEquipment.ts (新規作成)

import type { EquipmentItem } from '../../../../types/ItemTypes';
import type { DismantleResult } from '../../../../types/BlacksmithTypes';
import {
  DISMANTLE_RETURN_RATES,
  DISMANTLE_BONUS_CHANCE,
  DISMANTLE_LEVEL_BONUS
} from '../../../../types/BlacksmithTypes';

/**
 * 装備を解体
 */
export function dismantleEquipment(equipment: EquipmentItem): DismantleResult {
  const rarity = equipment.rarity || 'common';
  const returnRate = DISMANTLE_RETURN_RATES[rarity] || 0.10;
  const baseReturn = Math.floor(equipment.sellPrice * returnRate);
  
  // 魔石に変換
  const magicStones = convertToMagicStones(baseReturn);
  
  // ボーナス判定（Epic以上）
  let bonusReceived = false;
  if (rarity === 'epic' || rarity === 'legendary') {
    const levelBonus = DISMANTLE_LEVEL_BONUS[equipment.level] || 0;
    const bonusChance = DISMANTLE_BONUS_CHANCE + levelBonus;
    
    if (Math.random() < bonusChance) {
      magicStones.push({ typeId: 'magic_stone_large', count: 1 });
      bonusReceived = true;
    }
  }
  
  return {
    magicStones,
    bonusReceived
  };
}

/**
 * 価値を魔石に変換
 */
function convertToMagicStones(value: number): { typeId: string, count: number }[] {
  const stones: { typeId: string, count: number }[] = [];
  let remaining = value;
  
  // 魔石（大）: 350G
  const largeCount = Math.floor(remaining / 350);
  if (largeCount > 0) {
    stones.push({ typeId: 'magic_stone_large', count: largeCount });
    remaining -= largeCount * 350;
  }
  
  // 魔石（中）: 100G
  const mediumCount = Math.floor(remaining / 100);
  if (mediumCount > 0) {
    stones.push({ typeId: 'magic_stone_medium', count: mediumCount });
    remaining -= mediumCount * 100;
  }
  
  // 魔石（小）: 30G
  const smallCount = Math.floor(remaining / 30);
  if (smallCount > 0) {
    stones.push({ typeId: 'magic_stone_small', count: smallCount });
    remaining -= smallCount * 30;
  }
  
  // 端数は切り捨て
  
  return stones;
}
```

**✅ 完了チェック:**
- [ ] dismantleEquipment が実装された
- [ ] convertToMagicStones が実装された
- [ ] ボーナス判定が動作する

---

## Phase 5: CSS とアニメーション（Week 3）

### タスク 5.1: Blacksmith.css

```css
/* src/camps/facilities/Blacksmith/Blacksmith.css */

.blacksmith-screen {
  width: 100vw;
  height: 100vh;
  background: linear-gradient(135deg, #2a1a1a 0%, #3a2a2a 100%);
  display: flex;
  flex-direction: column;
  padding: 2rem;
  color: #f0e0d0;
}

.blacksmith-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.blacksmith-header h1 {
  font-size: 2.5rem;
  text-shadow: 0 0 20px rgba(255, 100, 50, 0.8);
}

.resources {
  display: flex;
  gap: 2rem;
  font-size: 1.5rem;
}

.blacksmith-tabs {
  display: flex;
  gap: 1rem;
  justify-content: center;
  margin-bottom: 2rem;
}

.blacksmith-tabs button {
  padding: 1rem 2rem;
  background: rgba(255, 100, 50, 0.2);
  border: 2px solid rgba(255, 100, 50, 0.5);
  border-radius: 8px;
  color: #f0e0d0;
  font-size: 1.2rem;
  cursor: pointer;
  transition: all 0.3s ease;
}

.blacksmith-tabs button.active {
  background: rgba(255, 100, 50, 0.8);
  border-color: rgba(255, 100, 50, 1);
}

.blacksmith-content {
  flex: 1;
  background: rgba(0, 0, 0, 0.3);
  border: 2px solid rgba(255, 100, 50, 0.3);
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
  color: #f0e0d0;
  font-size: 1.1rem;
  cursor: pointer;
}

/* 品質上昇アニメーション */
@keyframes quality-upgrade {
  0% {
    transform: scale(1);
    filter: brightness(1);
  }
  50% {
    transform: scale(1.2);
    filter: brightness(2) hue-rotate(60deg);
  }
  100% {
    transform: scale(1);
    filter: brightness(1);
  }
}

.quality-upgraded {
  animation: quality-upgrade 1s ease-in-out;
}
```

**✅ 完了チェック:**
- [ ] Blacksmith.css が作成された
- [ ] スタイルが適用される
- [ ] アニメーションが定義された

---

## テスト項目

### 基本動作テスト

```
□ Blacksmith画面の表示
  □ リソース表示（Gold, 魔石価値）
  □ タブ切り替え

□ 強化機能
  □ 装備リスト表示
  □ 強化オプション選択
  □ レベルアップ
  □ 品質上昇判定
  □ スキル解放（Lv3）
  □ コスト支払い

□ 修理機能
  □ コスト計算
  □ AP回復

□ 解体機能
  □ 魔石変換
  □ ボーナス判定
  □ 警告ダイアログ
```

---

## トラブルシューティング

### よくあるエラー

**1. Quality not found**
```
原因: 既存装備にqualityがない
解決: migrateEquipmentToV2 を実行
```

**2. Stats calculation incorrect**
```
原因: baseStatsが未定義
解決: equipmentGenerator で baseStats を設定
```

**3. Upgrade cost not found**
```
原因: BASE_UPGRADE_COSTS にデータがない
解決: BlacksmithTypes.ts を確認
```

---

**END OF BLACKSMITH IMPLEMENTATION GUIDE**
